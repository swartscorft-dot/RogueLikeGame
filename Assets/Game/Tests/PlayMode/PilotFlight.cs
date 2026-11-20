using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    public enum FlightKind { Act, Floor3, Floor5, Boss, Check }

    /// <summary>
    /// One flight (work order 022, 2026-11-20): a pilot through a whole act from a run seed (until the act is cleared, she
    /// dies, or it is stuck), or started on floor 3 or 5 with eight skills and full health (the run's seed, the floors
    /// before finished for tests: their rewards are taken and then put back to the start four and the first four of the
    /// pool), or against the act boss alone in the practice room at its full health with the same eight. A check flight is
    /// a pack of five swarmers and a shooter in the practice room, flown at time scale 1 and at the flights' scale to see
    /// that the rules hold at it.
    ///
    /// It runs at <see cref="Scale"/> (time scale; ROGUELIKE_PILOT_SCALE sets another). It records the flight with the
    /// play session recorder into <see cref="Folder"/>, under the system's temp path, never the project, and takes its
    /// numbers from the recorder's events and summary. Stuck: thirty seconds of game time with no damage dealt, no
    /// enemy killed, no room entered and the pilot's goal no nearer by a metre. Each flight adds its line to
    /// flights.tsv and its details to its own text file, and rewrites table.txt from every line there.
    /// </summary>
    public sealed class PilotFlight
    {
        public const float DefaultScale = 6f;
        public const float StuckSeconds = 30f;
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        public static string Folder => Path.Combine(Path.GetTempPath(), "RoguelikePilotFlights");

        public static float Scale
        {
            get
            {
                string text = Environment.GetEnvironmentVariable("ROGUELIKE_PILOT_SCALE");
                return float.TryParse(text, NumberStyles.Float, s_Inv, out float s) && s > 0f ? s : DefaultScale;
            }
        }

        public readonly PilotKind PilotKind;
        public readonly FlightKind Kind;
        public readonly string Seed;
        public float TimeScale;
        public string Name => PilotKind + "_" + Kind + "_" + Seed + (Kind == FlightKind.Check ? "_x" + TimeScale.ToString("0.#", s_Inv) : "");

        // What came of it.
        public string Ended = "running";
        public string Where = "";
        public float Seconds;
        public float RealSeconds;
        public int Frames;
        public int Health, MaxHealth;
        public int DamageTaken;
        public float Dps = float.NaN;
        public float FightSeconds;
        public int FloorsFinished;
        public string Details = "";
        public string Line = "";

        readonly PlayerCharacter _player;
        readonly PlayerHealth _health;
        readonly GeneratedLevels _levels;
        PlaySessionRecorder _rec;
        Pilot _pilot;
        PilotEyes _eyes;
        readonly PilotView _view = new PilotView();
        float _t0;
        int _scanned;
        float _lastProgress;
        float _bestGoal = float.MaxValue;
        Vector3 _lastGoal;
        EnemyBoss _boss;
        int _bossId;
        int _bossCountered;
        readonly List<int> _calledIds = new List<int>();
        int _calledDead;
        readonly List<Enemy> _spawned = new List<Enemy>();

        sealed class RoomRec
        {
            public int Floor, Order;
            public RoomKind Kind;
            public string Enemies = "";
            public readonly HashSet<int> Ids = new HashSet<int>();
            public readonly List<KeyValuePair<double, double>> Spans = new List<KeyValuePair<double, double>>();
            public int HealthLeft = -1;
        }

        sealed class FloorRec
        {
            public int Floor;
            public double From, To = -1;
            public int Health = -1;
            public string Skill = "";
            public string Chests = "";
        }

        readonly List<RoomRec> _rooms = new List<RoomRec>();
        readonly List<FloorRec> _floors = new List<FloorRec>();
        RoomRec _inRoom;
        GeneratedLevel _levelSeen;

        public PilotFlight(PilotKind pilot, FlightKind kind, string seed, PlayerCharacter player, PlayerHealth health, GeneratedLevels levels,
                           float timeScale = -1f)
        {
            PilotKind = pilot;
            Kind = kind;
            Seed = seed;
            _player = player;
            _health = health;
            _levels = levels;
            TimeScale = timeScale > 0f ? timeScale : Scale;
        }

        public static Pilot Make(PilotKind kind, PilotBody body) =>
            kind == PilotKind.Turret ? new Turret(body) : kind == PilotKind.Kiter ? (Pilot)new Kiter(body) : new Ace(body);

        double T => Time.time - _t0;

        /// <summary>The start four and the first four of the pool, on the keys (work order 022's floors 3 and 5 and the boss).</summary>
        public static void EquipEight(PlayerCharacter player)
        {
            player.EquipStartSkills();
            int taken = 0;
            for (int slot = 0; slot < player.Skills.Count && taken < 4; slot++)
            {
                if (player.Skills[slot] == null || player.Skills[slot].StartEquipped) continue;
                player.EquipSkill(slot);
                taken++;
            }
        }

        void StartRecording()
        {
            string folder = Path.Combine(Folder, "sessions", Name);
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
            _rec = PlaySessionRecorder.StartRecording(folder);
            _t0 = Time.time;
        }

        public IEnumerator Run()
        {
            Time.timeScale = 1f;
            if (Kind == FlightKind.Boss || Kind == FlightKind.Check)
            {
                // In the practice room the practice brute would join the fight: it is switched off, and stays the template.
                EnemyBrute practice = Object.FindAnyObjectByType<EnemyBrute>();
                if (practice != null) practice.enabled = false;
            }
            ulong seed = 0;
            if (Kind != FlightKind.Boss && Kind != FlightKind.Check && !LevelSeed.TryParse(Seed, out seed))
                throw new ArgumentException("not a seed: " + Seed);

            switch (Kind)
            {
                case FlightKind.Act:
                    StartRecording();
                    _levels.StartRun(seed);
                    break;
                case FlightKind.Floor3:
                case FlightKind.Floor5:
                {
                    int floor = Kind == FlightKind.Floor3 ? 3 : 5;
                    _levels.StartRun(seed);
                    yield return null;
                    while (_levels.Floor < floor)
                    {
                        _levels.FinishFloorForTests();
                        for (int i = 0; i < 600 && _levels.Choice != GeneratedLevels.ChoiceKind.FloorReward; i++) yield return null;
                        if (!_levels.PickReward(0)) throw new InvalidOperationException("no floor reward on floor " + _levels.Floor);
                        yield return null;
                    }
                    EquipEight(_player);
                    _health.RestoreFull();
                    StartRecording();
                    break;
                }
                case FlightKind.Boss:
                {
                    EquipEight(_player);
                    _health.RestoreFull();
                    StartRecording();
                    foreach (Enemy e in _levels.SpawnPractice(EnemyKind.Boss))
                    {
                        if (e is EnemyBoss b) b.HealthOverride = b.Tuning.Enemy.Health; // the full 18000, not the practice third
                        _spawned.Add(e);
                    }
                    break;
                }
                case FlightKind.Check:
                {
                    _player.EquipStartSkills();
                    _health.RestoreFull();
                    StartRecording();
                    _spawned.AddRange(_levels.SpawnPractice(EnemyKind.Swarmer));
                    _spawned.AddRange(_levels.SpawnPractice(EnemyKind.Shooter));
                    break;
                }
            }

            var body = new PilotBody(_player, _health);
            _pilot = Make(PilotKind, body);
            _eyes = new PilotEyes(_player, _levels);
            _lastProgress = Time.time;
            float realStart = Time.realtimeSinceStartup;
            float cap = Kind == FlightKind.Act ? 45f * 60f : Kind == FlightKind.Boss ? 10f * 60f : Kind == FlightKind.Check ? 120f : 15f * 60f;
            Time.timeScale = TimeScale;
            NewFloor();

            while (true)
            {
                yield return null;
                Frames++;
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FloorReward)
                {
                    EndFloor();
                    if (Kind != FlightKind.Act)
                    {
                        Ended = "floor cleared";
                        Where = "floor " + _levels.Floor;
                        break;
                    }
                    _levels.PickReward(0); // the first skill offered
                    if (_floors.Count > 0) _floors[_floors.Count - 1].Skill = _levels.SkillsTaken.LastOrDefault() ?? "";
                    continue;
                }
                if (_levels.Choice == GeneratedLevels.ChoiceKind.Chest)
                {
                    string taken = _levels.OfferedUpgrades.Count > 0 ? _levels.OfferedUpgrades[0].Short : "";
                    _levels.PickUpgrade(0); // the first upgrade
                    if (_floors.Count > 0) _floors[_floors.Count - 1].Chests += (_floors[_floors.Count - 1].Chests.Length > 0 ? ", " : "") + taken;
                    continue;
                }
                if (!_health.IsAlive)
                {
                    Ended = "died";
                    break;
                }
                if (_levels.ActCleared)
                {
                    EndFloor();
                    Ended = "act cleared";
                    Where = "floor " + _levels.Floor;
                    break;
                }
                if ((Kind == FlightKind.Boss || Kind == FlightKind.Check) && _spawned.Count > 0 && _spawned.All(e => e == null || e.IsDead)
                    && Enemy.All.All(e => e == null || e.IsDead || !e.IsAwake))
                {
                    Ended = Kind == FlightKind.Boss ? "boss killed" : "all killed";
                    break;
                }
                if (_levels.Level != _levelSeen) NewFloor();
                TrackRoom();
                TrackBoss();

                _eyes.Look(_view);
                _pilot.Tick(_view);
                if (_pilot.WantsChest) _levels.OpenChest();

                if (Progressed()) _lastProgress = Time.time;
                if (Time.time - _lastProgress > StuckSeconds)
                {
                    Ended = "stuck";
                    Where = WhereNow() + ": " + StuckWhy();
                    break;
                }
                if (T > cap)
                {
                    Ended = "time cap";
                    Where = WhereNow() + ": over " + (cap / 60f).ToString("0", s_Inv) + " min of game time";
                    break;
                }
            }

            Seconds = (float)T;
            RealSeconds = Time.realtimeSinceStartup - realStart;
            Time.timeScale = 1f;
            TrackBoss();
            CloseRoom();
            Health = _health.Current;
            MaxHealth = _health.Max;
            if (Ended == "died") Where = WhereNow();
            Analyse();
            _rec.StopRecording();
            Write();
        }

        // ---------------- Tracking ----------------

        void NewFloor()
        {
            CloseRoom();
            _levelSeen = _levels.Level;
            if (_levelSeen == null && Kind != FlightKind.Boss && Kind != FlightKind.Check) return;
            _floors.Add(new FloorRec { Floor = _levelSeen != null ? _levels.Floor : 0, From = T });
            if (_levelSeen != null && _levelSeen.Boss is EnemyBoss b) _boss = b;
            if (_boss == null)
                foreach (Enemy e in _spawned)
                    if (e is EnemyBoss sb) _boss = sb;
            if (_boss != null) _bossId = _boss.Id;
        }

        void EndFloor()
        {
            if (_floors.Count == 0) return;
            FloorRec f = _floors[_floors.Count - 1];
            if (f.To >= 0) return;
            f.To = T;
            f.Health = _health.Current;
            FloorsFinished++;
        }

        void TrackRoom()
        {
            LevelRoom room = _levels.CurrentRoom;
            int floor = _levels.Floor;
            if (_inRoom != null && room != null && _inRoom.Order == room.Order && _inRoom.Floor == floor) return;
            CloseRoom();
            if (room == null || _levels.Level == null) return;
            RoomRec rec = _rooms.FirstOrDefault(r => r.Floor == floor && r.Order == room.Order);
            if (rec == null)
            {
                rec = new RoomRec { Floor = floor, Order = room.Order, Kind = room.Kind };
                if (_levels.Level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list))
                {
                    foreach (Enemy e in list)
                        if (e != null) rec.Ids.Add(e.Id);
                    rec.Enemies = string.Join(", ", list.Where(e => e != null).GroupBy(e => e.Kind)
                                                    .Select(g => g.Count() + " " + EnemyKinds.Word(g.Key)));
                }
                _rooms.Add(rec);
            }
            rec.Spans.Add(new KeyValuePair<double, double>(T, -1));
            _inRoom = rec;
        }

        void CloseRoom()
        {
            if (_inRoom == null) return;
            int last = _inRoom.Spans.Count - 1;
            _inRoom.Spans[last] = new KeyValuePair<double, double>(_inRoom.Spans[last].Key, T);
            _inRoom.HealthLeft = _health.Current;
            _inRoom = null;
        }

        void TrackBoss()
        {
            if (_boss == null) return;
            _bossCountered = _boss.Countered;
            foreach (Enemy e in _boss.Called)
                if (e != null && !_calledIds.Contains(e.Id)) _calledIds.Add(e.Id);
            _calledDead = _boss.Called.Count(e => e == null || e.IsDead);
        }

        bool Progressed()
        {
            bool progress = false;
            IReadOnlyList<SessionEvent> events = _rec.Events;
            for (; _scanned < events.Count; _scanned++)
            {
                SessionEvent e = events[_scanned];
                if ((e.Type == SessionEventTypes.Hit && (e.Damage ?? 0) > 0) || e.Type == SessionEventTypes.EnemyDeath
                    || e.Type == SessionEventTypes.RoomEntered || e.Type == SessionEventTypes.FloorEnter)
                    progress = true;
            }
            if (_pilot.HasGoal)
            {
                if (Vector3.Distance(_pilot.Goal, _lastGoal) > 2f)
                {
                    _lastGoal = _pilot.Goal;
                    _bestGoal = float.MaxValue;
                }
                float d = Vector2.Distance(new Vector2(_player.Position.x, _player.Position.z), new Vector2(_pilot.Goal.x, _pilot.Goal.z));
                if (d < _bestGoal - 1f)
                {
                    _bestGoal = d;
                    progress = true;
                }
            }
            return progress;
        }

        string WhereNow()
        {
            if (_levels.Level == null) return "practice room";
            LevelRoom room = _levels.CurrentRoom;
            return string.Format(s_Inv, "floor {0} room {1}", _levels.Floor, room != null ? (room.Order + 1) + " (" + room.Kind.ToString().ToLowerInvariant() + ")" : "none (outside the rooms)");
        }

        string StuckWhy()
        {
            Vector3 p = _player.Position;
            var sb = new StringBuilder();
            sb.Append(string.Format(s_Inv, "the pilot was \"{0}\" at ({1:0.0}, {2:0.0})", _pilot.Mode, p.x, p.z));
            if (_pilot.HasGoal)
                sb.Append(string.Format(s_Inv, ", its goal ({0:0.0}, {1:0.0}) {2:0.0} m away", _pilot.Goal.x, _pilot.Goal.z,
                                        Vector2.Distance(new Vector2(p.x, p.z), new Vector2(_pilot.Goal.x, _pilot.Goal.z))));
            var foes = _view.Enemies.Where(e => e.Awake || (_view.HasMap && e.Room == _view.HereRoom)).ToList();
            sb.Append(string.Format(s_Inv, "; {0} foes in view", foes.Count));
            foreach (EnemySeen e in foes.OrderBy(e => e.Distance).Take(3))
                sb.Append(string.Format(s_Inv, "; {0} {1:0.0} m, {2}{3}{4}", EnemyKinds.Word(e.Kind), e.Distance, e.Awake ? "awake" : "asleep",
                                        e.Visible ? ", seen" : ", behind cover", e.Room >= 0 ? ", room " + (e.Room + 1) : ""));
            return sb.ToString();
        }

        // ---------------- The numbers ----------------

        static string F(float v, string format = "0.0") => float.IsNaN(v) ? "-" : v.ToString(format, s_Inv);
        static string F(double v, string format = "0.0") => double.IsNaN(v) ? "-" : v.ToString(format, s_Inv);

        static bool In(double t, List<KeyValuePair<double, double>> spans) => spans.Any(s => t >= s.Key && (s.Value < 0 || t <= s.Value));

        void Analyse()
        {
            List<SessionEvent> events = _rec.Events.ToList();
            SessionStats stats = SessionSummary.Compute(events);
            var sb = new StringBuilder();
            sb.Append(string.Format(s_Inv, "{0}: {1} pilot, {2} flight, seed {3}, time scale {4}\n", Name, PilotKind, Kind, Seed, F(TimeScale, "0.#")));
            DamageTaken = events.Where(e => e.Type == SessionEventTypes.PlayerDamaged).Sum(e => e.Damage ?? 0);
            if (Ended == "died")
            {
                SessionEvent killer = events.LastOrDefault(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.Landed);
                if (killer != null) Where += " to " + killer.Enemy + " " + killer.Name;
            }
            sb.Append(string.Format(s_Inv, "ended: {0}{1}; {2} s of game time ({3} min), {4} s real, {5} frames, mean frame {6} ms of game time; health {7}/{8}\n",
                                    Ended, Where.Length > 0 ? " (" + Where + ")" : "", F(Seconds), F(Seconds / 60f, "0.0"), F(RealSeconds), Frames,
                                    F(Frames > 0 ? Seconds / Frames * 1000f : 0f, "0.0"), Health, MaxHealth));

            // Rooms.
            sb.Append("\nrooms (fight: first of its enemies awake to the last dead; + = not all dead):\n");
            sb.Append("floor  room  kind      fight s  taken  left  enemies | hits taken | skills used\n");
            foreach (RoomRec r in _rooms)
            {
                var wakes = events.Where(e => e.Type == SessionEventTypes.EnemyWake && e.EnemyId.HasValue && r.Ids.Contains(e.EnemyId.Value)).Select(e => e.T).ToList();
                var deaths = events.Where(e => e.Type == SessionEventTypes.EnemyDeath && e.EnemyId.HasValue && r.Ids.Contains(e.EnemyId.Value)).Select(e => e.T).ToList();
                bool all = r.Ids.Count > 0 && deaths.Count >= r.Ids.Count;
                string fight = wakes.Count == 0 ? "-" : F((all ? deaths.Max() : Seconds) - wakes.Min()) + (all ? "" : "+");
                int taken = events.Where(e => e.Type == SessionEventTypes.PlayerDamaged && In(e.T, r.Spans)).Sum(e => e.Damage ?? 0);
                string hits = string.Join(", ", events.Where(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.Landed && In(e.T, r.Spans))
                                                      .GroupBy(e => e.Enemy + " " + e.Name).Select(g => g.Key + " x" + g.Count()));
                string skills = string.Join(", ", events.Where(e => e.Type == SessionEventTypes.SkillStart && In(e.T, r.Spans))
                                                        .GroupBy(e => e.Name).Select(g => g.Key + " " + g.Count()));
                int shots = events.Count(e => e.Type == SessionEventTypes.ShotStart && In(e.T, r.Spans));
                sb.Append(string.Format(s_Inv, "{0,5}  {1,4}  {2,-8}  {3,7}  {4,5}  {5,4}  {6} | {7} | {8}{9}shots {10}\n", r.Floor, r.Order + 1,
                                        r.Kind.ToString().ToLowerInvariant(), fight, taken, r.HealthLeft, r.Enemies.Length > 0 ? r.Enemies : "none",
                                        hits.Length > 0 ? hits : "none", skills, skills.Length > 0 ? ", " : "", shots));
            }

            // Floors.
            sb.Append("\nfloors:\nfloor  seconds  health  skill taken  chests\n");
            foreach (FloorRec f in _floors)
                sb.Append(string.Format(s_Inv, "{0,5}  {1,7}  {2,6}  {3}  {4}\n", f.Floor, f.To >= 0 ? F(f.To - f.From) : F(Seconds - f.From) + "+",
                                        f.Health >= 0 ? f.Health.ToString(s_Inv) : "-", f.Skill.Length > 0 ? f.Skill : "-", f.Chests.Length > 0 ? f.Chests : "-"));

            // The run.
            ScopeStats fights = stats.InFights;
            FightSeconds = fights != null ? fights.Seconds : 0f;
            Dps = fights != null && fights.Seconds > 0f ? fights.TotalDamage / fights.Seconds : float.NaN;
            sb.Append(string.Format(s_Inv, "\ndamage dealt in fights: {0} in {1} s of fighting = {2} a second (the rooms were sized for 250)\n",
                                    fights != null ? fights.TotalDamage : 0, F(FightSeconds), F(Dps, "0")));
            if (fights != null)
                foreach (ActionRow row in fights.Rows.Where(r => r.Damage > 0).OrderByDescending(r => r.Damage))
                    sb.Append(string.Format(s_Inv, "  {0,-14} {1,7}  {2,5}%  started {3}\n", row.Name, row.Damage, F(row.DamageShare * 100f, "0"), row.Started));
            sb.Append(string.Format(s_Inv, "damage taken: {0}\n", DamageTaken));
            int takenAll = Math.Max(1, stats.EnemyKinds.Sum(k => k.DamageTaken));
            foreach (EnemyKindRow k in stats.EnemyKinds.Where(k => k.DamageTaken > 0).OrderByDescending(k => k.DamageTaken))
            {
                sb.Append(string.Format(s_Inv, "  {0,-8} {1,5}  {2,5}%  ({3} killed)\n", k.Kind, k.DamageTaken, F(100f * k.DamageTaken / takenAll, "0"), k.Kills));
                foreach (KeyValuePair<string, int> a in k.DamageByAttack)
                {
                    int count = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.Landed && e.Enemy == k.Kind && e.Name == a.Key);
                    sb.Append(string.Format(s_Inv, "    {0,-10} {1,5}  {2,5}%  {3} hits\n", a.Key, a.Value, F(100f * a.Value / takenAll, "0"), count));
                }
            }
            if (fights != null)
                sb.Append(string.Format(s_Inv, "enemy strikes in fights: {0} attacks, {1} landed, {2} out of the area, {3} avoided by the dash; {4} dashes\n",
                                        fights.EnemyAttacks, fights.StrikesLanded, fights.StrikesOutOfArea, fights.StrikesAvoidedByDash, fights.Dashes));
            sb.Append(string.Format(s_Inv, "kit: {0} marks added, {1} spender hits ({2} marks each), {3} clean presses of {4} last presses, focus spent {5} times, {6} perfect Long Shots, {7} counters\n",
                                    stats.Kit.MarksAdded, stats.Kit.SpenderHits, F(stats.Kit.MarksPerSpenderHit, "0.0"), stats.Kit.CleanPresses, stats.Kit.LastPresses,
                                    fights != null ? fights.FocusSpent : 0, _player.PerfectReleases, _player.Counters));

            // The boss.
            if (_bossId > 0)
            {
                var bossEvents = events.Where(e => e.EnemyId == _bossId).ToList();
                double wake = bossEvents.Where(e => e.Type == SessionEventTypes.EnemyWake).Select(e => e.T).DefaultIfEmpty(double.NaN).First();
                double death = bossEvents.Where(e => e.Type == SessionEventTypes.EnemyDeath).Select(e => e.T).DefaultIfEmpty(double.NaN).First();
                sb.Append(string.Format(s_Inv, "\nthe boss: {0}; {1} s from waking to {2}\n", double.IsNaN(death) ? "not killed" : "killed",
                                        F((double.IsNaN(death) ? Seconds : death) - wake), double.IsNaN(death) ? "the end of the flight" : "its death"));
                foreach (var g in bossEvents.Where(e => e.Type == SessionEventTypes.EnemyAttack).GroupBy(e => e.Name))
                {
                    var strikes = bossEvents.Where(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == g.Key).ToList();
                    int landed = strikes.Count(e => e.Result == SessionWords.Landed);
                    sb.Append(string.Format(s_Inv, "  {0,-7} {1} wound up, {2} struck, {3} landed for {4}, {5} avoided ({6} by the dash)\n", g.Key, g.Count(), strikes.Count,
                                            landed, strikes.Where(e => e.Result == SessionWords.Landed).Sum(e => e.Damage ?? 0),
                                            strikes.Count - landed, strikes.Count(e => e.Result == SessionWords.AvoidedByDash)));
                }
                int fromCalled = events.Where(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.Landed && e.EnemyId.HasValue && _calledIds.Contains(e.EnemyId.Value))
                                       .Sum(e => e.Damage ?? 0);
                sb.Append(string.Format(s_Inv, "  dazed {0} times, countered {1} times, angered {2}; packs called {3} ({4} swarmers, {5} killed by the end), {6} damage from them\n",
                                        bossEvents.Count(e => e.Type == SessionEventTypes.EnemyDazed), _bossCountered,
                                        bossEvents.Any(e => e.Type == SessionEventTypes.BossAnger) ? "yes" : "no",
                                        bossEvents.Count(e => e.Type == SessionEventTypes.BossCall), _calledIds.Count, _calledDead, fromCalled));
            }
            Details = sb.ToString();
        }

        void Write()
        {
            Directory.CreateDirectory(Folder);
            string Clean(string s) => (s ?? "").Replace('\t', ' ').Replace('\n', ' ');
            Line = string.Join("\t", Name, PilotKind.ToString(), Kind.ToString(), Seed, Clean(Ended), Clean(Where), F(Seconds), F(Seconds / 60f, "0.0"),
                               Health + "/" + MaxHealth, FloorsFinished.ToString(s_Inv), F(Dps, "0"), F(FightSeconds), DamageTaken.ToString(s_Inv),
                               F(RealSeconds), F(TimeScale, "0.#"), DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", s_Inv));
            File.AppendAllText(Path.Combine(Folder, "flights.tsv"), Line + "\n", new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(Folder, Name + ".txt"), Details, new UTF8Encoding(false));
            WriteTable();
            Debug.Log("Pilot flight " + Line + "\n" + Details);
        }

        /// <summary>The table of every flight in flights.tsv, the latest line of each, aligned.</summary>
        public static void WriteTable()
        {
            string path = Path.Combine(Folder, "flights.tsv");
            if (!File.Exists(path)) return;
            var latest = new Dictionary<string, string[]>();
            var order = new List<string>();
            foreach (string line in File.ReadAllLines(path))
            {
                string[] cells = line.Split('\t');
                if (cells.Length < 15) continue;
                if (!latest.ContainsKey(cells[0])) order.Add(cells[0]);
                latest[cells[0]] = cells;
            }
            var rows = new List<string[]> { new[] { "flight", "ended", "where", "game s", "min", "health", "floors done", "dps in fights", "fight s", "taken", "real s", "scale" } };
            foreach (string name in order)
            {
                string[] c = latest[name];
                rows.Add(new[] { c[0], c[4], c[5], c[6], c[7], c[8], c[9], c[10], c[11], c[12], c[13], c[14] });
            }
            int[] widths = new int[rows[0].Length];
            foreach (string[] r in rows)
                for (int i = 0; i < r.Length; i++) widths[i] = Math.Max(widths[i], Math.Min(i == 2 ? 120 : 60, r[i].Length));
            var sb = new StringBuilder();
            foreach (string[] r in rows)
            {
                for (int i = 0; i < r.Length; i++) sb.Append((i == 2 ? r[i] : r[i]).PadRight(widths[i] + 2));
                sb.Append('\n');
            }
            File.WriteAllText(Path.Combine(Folder, "table.txt"), sb.ToString(), new UTF8Encoding(false));
        }
    }
}
