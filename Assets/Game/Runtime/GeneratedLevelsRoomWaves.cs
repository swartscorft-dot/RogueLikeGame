using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Two waves (work order 136; Kristian's ticks of 2026-11-25: waves "Ja, i de fleste rom", the next wave "Når halve første er død";
    /// the rules in <see cref="RoomWaveRules"/>, the numbers in <see cref="LevelValues.WaveRules"/>, the switch <see cref="LevelValues.Waves"/>
    /// with the Trials page's "Waves").
    ///
    /// As a floor's fights are given out (<see cref="SetUpRoomFights"/>), each room that brings its enemies in waves (from its place on
    /// the floor, <see cref="RoomWaveRules.WavesOf"/>) has them split (<see cref="RoomWaveRules.Split"/>): wave 1 stays where the floor
    /// put it and wakes with the room at the line, as before; every enemy of a later wave is taken out of the game
    /// (<see cref="Enemy.WaitForItsWave"/>) and is counted alive: the room is not cleared, the doors do not open, the reward does not
    /// come and the run box's "enemies left" counts it, until it is dead. Every frame, after the fights are stepped and before the doors:
    /// once the wave before has lost half its count (rounded up) and none is still arriving, the next wave starts. Each of its enemies is
    /// given a point of the room (<see cref="RoomWaveRules.Pick"/>: on his screen inside the ranged rule's margin, 7 to 11 m from her, never
    /// below her on his screen, apart from the others, on the NavMesh with a path to her); a disc shows there for
    /// <see cref="RoomWaveValues.DiscSeconds"/> (<see cref="PlaceArrival"/>, the enemy's own wind-up outline in the waves' colour), and then
    /// the enemy stands there awake (<see cref="Enemy.ArriveAt"/>); a shooter's or a caster's second's grace runs from then
    /// (<see cref="RangedOpening.Arrived"/>). Those with no good point wait and are tried again <see cref="RoomWaveValues.WaitSeconds"/> later.
    /// The recorder hears each wave (<see cref="PlaySessionRecorder.NoteRoomWave"/>) and each room_fight gets the room's number of waves.
    ///
    /// Off (the switch, the page's line, the room fights off): nothing here does anything, and every room wakes whole as on work order 135.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The width of the disc's outline as its second begins and as it ends: an enemy's own wind-up outline's (Enemy.TelegraphThin, TelegraphThick).</summary>
        const float DiscThin = 0.05f, DiscThick = 0.3f;
        /// <summary>m over the floor the disc is drawn at: an enemy's wind-up outline's height.</summary>
        const float DiscHeight = 0.07f;
        /// <summary>m around her that her place is looked for on the NavMesh, for the path to an arrival's point.</summary>
        const float HerOnNavMeshWithin = 1.5f;

        /// <summary>One enemy of a wave on its way in: its point, when its disc began, the disc.</summary>
        public sealed class WaveArrival
        {
            public Enemy Enemy;
            /// <summary>Its wave's number, from 1.</summary>
            public int Wave;
            public Vector3 At;
            public float Since;
            public LineRenderer Disc;
        }

        /// <summary>One room's waves (work order 136).</summary>
        public sealed class RoomWaves
        {
            public LevelRoom Room;
            public RoomFight Fight;
            /// <summary>Its waves' enemies, wave 1 first.</summary>
            public readonly List<List<Enemy>> Waves = new List<List<Enemy>>();
            /// <summary>Waves started: 0 before the room woke, 1 once it woke (wave 1 standing), and so on.</summary>
            public int Started;
            /// <summary>The game time the last wave started.</summary>
            public float StartedAt = float.NegativeInfinity;
            /// <summary>The enemies of the wave arriving that found no point yet, and when they are tried again.</summary>
            public readonly List<Enemy> Waiting = new List<Enemy>();
            public float NextTryAt;
            /// <summary>The enemies standing in their discs.</summary>
            public readonly List<WaveArrival> Arriving = new List<WaveArrival>();
            /// <summary>For the test bot and the report: tries that found too few good points, and the enemies that had to wait for one.</summary>
            public int WaitTries, Waited;
            /// <summary>True for the first try of a wave or of a group of it (the limit at one moment), when the enemies short of a point are counted in <see cref="Waited"/>.</summary>
            public bool NewGroup;
            /// <summary>Every arrival so far, for the tests (distance from her as it began, its point).</summary>
            public readonly List<WaveArrival> Arrived = new List<WaveArrival>();
            /// <summary>The room's points an arrival may be tried at, made the first time; and, for each, its place on the NavMesh (asked once).</summary>
            internal List<LevelPoint> Points;
            internal Vector3[] OnMesh;
            internal sbyte[] OnMeshKnown; // 0 not asked, 1 on it, -1 not
            internal LevelRandom Rng;

            /// <summary>How many waves the room brings.</summary>
            public int Count => Waves.Count;
            /// <summary>True while an enemy of the wave started last waits for a point or stands in its disc.</summary>
            public bool StillArriving => Waiting.Count > 0 || Arriving.Count > 0;
        }

        readonly Dictionary<int, RoomWaves> _roomWaves = new Dictionary<int, RoomWaves>();
        readonly List<RoomWaves> _roomWaveList = new List<RoomWaves>();
        readonly List<LevelPoint> _waveTaken = new List<LevelPoint>();
        NavMeshPath _wavePath;
        bool _wavesOn;
        static Material s_discMaterial;

        /// <summary>True when this floor was set up with waves (work order 136): its switch and the Trials page's line both on.</summary>
        public bool WavesOnThisFloor => _wavesOn;
        /// <summary>The rooms of this floor that bring their enemies in more than one wave, in the order of the rooms.</summary>
        public IReadOnlyList<RoomWaves> RoomWaveList => _roomWaveList;
        /// <summary>A room's waves, or null (one wave: it wakes whole).</summary>
        public RoomWaves WavesOf(LevelRoom room) => room != null && _roomWaves.TryGetValue(room.Order, out RoomWaves w) ? w : null;
        /// <summary>The number of waves a room of this floor brings: 1 for one that wakes whole (and every room with the waves off).</summary>
        public int WaveCountOf(LevelRoom room) => WavesOf(room)?.Count ?? 1;

        /// <summary>From <see cref="SetUpRoomFights"/>, the fights given out: each room's waves, its later waves' enemies taken out of the game.</summary>
        void SetUpRoomWaves()
        {
            ForgetRoomWaves();
            _wavesOn = _level != null && _fightList.Count > 0 && RoomTrialSettings.WavesInUse(Values);
            if (!_wavesOn) return;
            LevelLayout layout = _level.Layout;
            RoomWaveValues rules = Values.WaveRules;
            var kinds = new List<EnemyKind>();
            var elites = new List<bool>();
            var members = new List<Enemy>();
            foreach (RoomFight f in _fightList)
            {
                LevelRoom room = f.Room >= 0 && f.Room < layout.Rooms.Count ? layout.Rooms[f.Room] : null;
                int waves = RoomWaveRules.WavesOf(layout, room, rules);
                if (waves <= 1 || !_level.EnemiesByRoom.TryGetValue(f.Room, out List<Enemy> list)) continue;
                kinds.Clear();
                elites.Clear();
                members.Clear();
                foreach (Enemy e in list)
                {
                    if (e == null) continue;
                    members.Add(e);
                    kinds.Add(e.Kind);
                    elites.Add(e.IsElite);
                }
                int[] split = RoomWaveRules.Split(kinds, elites, room.LineUp, waves);
                int count = RoomWaveRules.Count(split);
                if (count <= 1) continue;
                var w = new RoomWaves { Room = room, Fight = f };
                for (int k = 0; k < count; k++) w.Waves.Add(new List<Enemy>());
                for (int i = 0; i < members.Count; i++) w.Waves[split[i] - 1].Add(members[i]);
                for (int k = 1; k < count; k++)
                    foreach (Enemy e in w.Waves[k]) e.WaitForItsWave();
                _roomWaves[room.Order] = w;
                _roomWaveList.Add(w);
            }
        }

        /// <summary>The floor goes: its waves with it (their discs go with its root).</summary>
        void ForgetRoomWaves()
        {
            _roomWaves.Clear();
            _roomWaveList.Clear();
            _waveTaken.Clear();
            _wavesOn = false;
        }

        /// <summary>
        /// The enemies of a room that wait, out of the game, for their wave (alive): the doors count them in play, so they do not open on
        /// a room whose next wave is still to come. 0 with the waves off. Allocates nothing.
        /// </summary>
        int WaitingFor(int room)
        {
            if (!_roomWaves.TryGetValue(room, out RoomWaves w)) return 0;
            int n = 0;
            for (int k = 1; k < w.Waves.Count; k++)
            {
                List<Enemy> wave = w.Waves[k];
                for (int i = 0; i < wave.Count; i++)
                    if (wave[i] != null && !wave[i].IsDead && wave[i].WaitsForItsWave) n++;
            }
            return n;
        }

        static int DeadOf(List<Enemy> wave)
        {
            int n = 0;
            for (int i = 0; i < wave.Count; i++) if (wave[i] == null || wave[i].IsDead) n++;
            return n;
        }

        /// <summary>Every frame, after the fights are stepped and before the doors (work order 136). Nothing is allocated while no wave starts or arrives.</summary>
        void TickRoomWaves()
        {
            if (_roomWaveList.Count == 0 || _level == null || _player == null) return;
            float now = Time.time;
            RoomWaveValues rules = Values.WaveRules;
            for (int r = 0; r < _roomWaveList.Count; r++) // every frame: no LINQ, nothing made
            {
                RoomWaves w = _roomWaveList[r];
                TickDiscs(w, now, rules);
                if (!w.Fight.On) continue;
                if (w.Started == 0)
                {
                    // The room woke: wave 1 stands where the floor put it, awake with the room.
                    w.Started = 1;
                    w.StartedAt = now;
                    NoteWave(w, "start", 1, -1, "", 0);
                    continue;
                }
                // The rest of a wave: after a wait for a point, or, held back by the limit at one moment, once the group before has stood.
                if (w.Waiting.Count > 0 && now >= w.NextTryAt && (rules.MostAtOnce <= 0 || w.Arriving.Count == 0)) TryArrivals(w, now, rules, false);
                if (w.Started >= w.Count) continue;
                List<Enemy> before = w.Waves[w.Started - 1];
                int dead = DeadOf(before);
                if (!RoomWaveRules.NextDue(before.Count, dead, w.StillArriving, rules)) continue;
                StartWave(w, now, before.Count - dead, rules);
            }
        }

        /// <summary>The next wave of a room starts: its living enemies wait for points, as many as can arrive now are given theirs.</summary>
        void StartWave(RoomWaves w, float now, int beforeAlive, RoomWaveValues rules)
        {
            w.Started++;
            w.NewGroup = true;
            w.StartedAt = now;
            w.Waiting.Clear();
            foreach (Enemy e in w.Waves[w.Started - 1])
                if (e != null && !e.IsDead && e.WaitsForItsWave) w.Waiting.Add(e);
            string arrivals = TryArrivals(w, now, rules, true);
            NoteWave(w, "start", w.Started, beforeAlive, arrivals, w.Waiting.Count);
        }

        /// <summary>
        /// Gives as many of the waiting enemies of the room's arriving wave as can arrive now their points and discs
        /// (<see cref="RoomWaveRules.Pick"/>); the rest wait <see cref="RoomWaveValues.WaitSeconds"/>. The arrivals as the recorder writes them.
        /// </summary>
        string TryArrivals(RoomWaves w, float now, RoomWaveValues rules, bool first)
        {
            if (w.Waiting.Count == 0) return "";
            LevelLayout layout = _level.Layout;
            Vector3 herWorld = _player.Position;
            LevelPoint her = _level.ToLevel(herWorld);
            Camera cam = Camera.main;
            GroundQuad view = InLevel(ViewOfTheGround(cam, 0f)); // the camera's view in the floor's own metres, as the room's points are
            double upX = 0, upZ = 1; // with no camera, the follow camera's: looking north
            if (cam != null)
            {
                Vector3 f = cam.transform.forward;
                if (f.x * f.x + f.z * f.z > 1e-6f)
                {
                    upX = f.x;
                    upZ = f.z;
                }
            }
            float margin = Values.RangedAround != null ? Values.RangedAround.ScreenMargin : 0f;
            if (w.Points == null)
            {
                w.Points = RoomWaveRules.RoomPoints(layout, w.Room, Values, rules.PointStep);
                w.OnMesh = new Vector3[w.Points.Count];
                w.OnMeshKnown = new sbyte[w.Points.Count];
                w.Rng = LevelRandom.Stream(layout.Seed, RoomWaveRules.Stage, w.Room.Order);
            }
            _waveTaken.Clear();
            for (int i = 0; i < w.Arriving.Count; i++) _waveTaken.Add(_level.ToLevel(w.Arriving[i].At));
            AddInPlay(_level.EnemiesByRoom, w.Room.Order);
            AddInPlay(_level.CalledByRoom, w.Room.Order);
            bool herOnMesh = NavMesh.SamplePosition(herWorld, out NavMeshHit herHit, HerOnNavMeshWithin, NavMesh.AllAreas);
            if (_wavePath == null) _wavePath = new NavMeshPath();
            bool Reachable(int i)
            {
                if (w.OnMeshKnown[i] == 0)
                {
                    bool on = NavMesh.SamplePosition(_level.ToWorld(w.Points[i]), out NavMeshHit hit, rules.OnNavMeshWithin, NavMesh.AllAreas)
                              && _level.RoomAt(hit.position) == w.Room;
                    w.OnMeshKnown[i] = (sbyte)(on ? 1 : -1);
                    if (on) w.OnMesh[i] = hit.position;
                }
                if (w.OnMeshKnown[i] < 0) return false;
                // Not behind anything she cannot walk through: a whole path from her to it.
                return !herOnMesh || (NavMesh.CalculatePath(herHit.position, w.OnMesh[i], NavMesh.AllAreas, _wavePath) && _wavePath.status == NavMeshPathStatus.PathComplete);
            }
            int wanted = rules.MostAtOnce > 0 ? Mathf.Min(w.Waiting.Count, rules.MostAtOnce) : w.Waiting.Count; // the producer's ruling: at most so many at one moment
            List<int> kept = RoomWaveRules.Pick(w.Points, wanted, her, view, margin, upX, upZ, _waveTaken, rules, w.Rng, Reachable);
            var words = new StringBuilder();
            for (int k = 0; k < kept.Count; k++)
            {
                Enemy e = w.Waiting[k];
                Vector3 at = w.OnMesh[kept[k]];
                var a = new WaveArrival { Enemy = e, Wave = w.Started, At = at, Since = now };
                a.Disc = PlaceArrival(e, at, rules);
                w.Arriving.Add(a);
                w.Arrived.Add(a);
                if (words.Length > 0) words.Append(';');
                Vector3 seen = cam != null ? cam.WorldToViewportPoint(at) : new Vector3(0.5f, 0.5f, 0f);
                words.Append(((float)_level.ToLevel(at).DistanceTo(her)).ToString("0.##", CultureInfo.InvariantCulture)).Append('|')
                     .Append(seen.x.ToString("0.###", CultureInfo.InvariantCulture)).Append('|').Append(seen.y.ToString("0.###", CultureInfo.InvariantCulture));
            }
            w.Waiting.RemoveRange(0, kept.Count);
            if (w.Waiting.Count > 0) w.NextTryAt = now + Mathf.Max(0f, rules.WaitSeconds);
            if (kept.Count < wanted)
            {
                // Too few good points: the rest wait for one (not those the limit at one moment holds back).
                w.WaitTries++;
                if (w.NewGroup) w.Waited += wanted - kept.Count;
            }
            w.NewGroup = false;
            if (!first && kept.Count > 0) NoteWave(w, "arrival", w.Started, -1, words.ToString(), w.Waiting.Count, now - w.StartedAt);
            return words.ToString();
        }

        /// <summary>The places of the living enemies of a room in play (switched on), into the points an arrival keeps apart from.</summary>
        void AddInPlay(IReadOnlyDictionary<int, List<Enemy>> byRoom, int room)
        {
            if (!byRoom.TryGetValue(room, out List<Enemy> list)) return;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead && list[i].isActiveAndEnabled) _waveTaken.Add(_level.ToLevel(list[i].transform.position));
        }

        /// <summary>
        /// The tell of one arrival (work order 136), the method a later order's arrivals can use too (137's caller): a disc on the ground at
        /// <paramref name="at"/>, the game's ground outline of an enemy's wind-up (a copy of <paramref name="enemy"/>'s own, else a plain
        /// line) round <see cref="RoomWaveValues.DiscRadius"/>, in <see cref="RoomWaveValues.DiscTint"/>, thickening over its second as a
        /// wind-up's does. It is the floor's child (it goes with the floor). The caller of it brings the enemy in when the second is over.
        /// </summary>
        public LineRenderer PlaceArrival(Enemy enemy, Vector3 at, RoomWaveValues rules)
        {
            LineRenderer line;
            Transform root = _level != null ? _level.transform : null;
            if (enemy != null && enemy.Telegraph != null)
            {
                GameObject copy = Instantiate(enemy.Telegraph.gameObject, root, false);
                line = copy.GetComponent<LineRenderer>();
            }
            else
            {
                var go = new GameObject();
                if (root != null) go.transform.SetParent(root, false);
                line = go.AddComponent<LineRenderer>();
                if (s_discMaterial == null) s_discMaterial = new Material(Shader.Find("Sprites/Default")) { name = "ArrivalDisc" };
                line.sharedMaterial = s_discMaterial;
            }
            line.gameObject.name = "ArrivalDisc";
            line.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            line.loop = true;
            line.useWorldSpace = true;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var tint = new Color(rules.DiscTint.R, rules.DiscTint.G, rules.DiscTint.B);
            line.startColor = line.endColor = tint;
            var block = new MaterialPropertyBlock();
            block.SetColor("_BaseColor", tint);
            block.SetColor("_Color", tint);
            line.SetPropertyBlock(block);
            line.widthMultiplier = DiscThin;
            GroundOutline.Circle(line, new Vector3(at.x, 0f, at.z), Mathf.Max(0.05f, rules.DiscRadius), DiscHeight);
            line.enabled = true;
            return line;
        }

        /// <summary>The discs of a room's arrivals: thickening, and over at their second's end, when each enemy stands there awake.</summary>
        void TickDiscs(RoomWaves w, float now, RoomWaveValues rules)
        {
            for (int i = w.Arriving.Count - 1; i >= 0; i--)
            {
                WaveArrival a = w.Arriving[i];
                float share = rules.DiscSeconds > 0f ? (now - a.Since) / rules.DiscSeconds : 1f;
                if (share < 1f)
                {
                    if (a.Disc != null) a.Disc.widthMultiplier = Mathf.Lerp(DiscThin, DiscThick, share);
                    continue;
                }
                if (a.Disc != null) Destroy(a.Disc.gameObject);
                a.Disc = null;
                w.Arriving.RemoveAt(i);
                if (w.Arriving.Count == 0 && w.Waiting.Count > 0 && rules.MostAtOnce > 0)
                {
                    // A group has stood: the next begins one interval later (the producer's ruling).
                    w.NextTryAt = Mathf.Max(w.NextTryAt, now + Mathf.Max(0f, rules.WaitSeconds));
                    w.NewGroup = true;
                }
                Enemy e = a.Enemy;
                if (e == null || e.IsDead) continue;
                e.ArriveAt(a.At);
                RangedOpening opening = e is EnemyShooter s ? s.Opening : e is EnemyCaster c ? c.Opening : null;
                opening?.Arrived(w.Fight, now);
            }
        }

        /// <summary>A wave started or some of it came after waiting: told to the recorder (work order 136).</summary>
        void NoteWave(RoomWaves w, string name, int wave, int beforeAlive, string arrivals, int waiting, float waited = 0f)
        {
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null) return;
            List<Enemy> members = w.Waves[wave - 1];
            var kinds = new List<EnemyKind>(members.Count);
            foreach (Enemy e in members) if (e != null) kinds.Add(e.Kind);
            int floor = Floor > 0 ? Floor : _level.Layout.Floor;
            recorder.NoteRoomWave(name, floor, LevelGeometry.RoomNumber(_level.Layout, w.Room) + 1, w.Room.Order, wave, w.Count,
                                  RoomWaveRules.KindsWords(kinds), kinds.Count, Time.time - w.Fight.StartedAt, beforeAlive, arrivals, waiting, waited);
        }

        /// <summary>What the camera shows of the ground at <paramref name="height"/> (<see cref="ScreenGround"/>, as the ranged rule asks it); unknown with no camera.</summary>
        static GroundQuad ViewOfTheGround(Camera c, float height)
        {
            if (c == null) return new GroundQuad();
            Transform t = c.transform;
            Vector3 p = t.position, f = t.forward, u = t.up, r = t.right;
            return ScreenGround.FromCamera(p.x, p.y, p.z, f.x, f.y, f.z, u.x, u.y, u.z, r.x, r.y, r.z, c.fieldOfView, c.aspect, height);
        }

        /// <summary>A view of the ground in the world's metres moved into the floor's own (<see cref="GeneratedLevel.ToLevel"/>).</summary>
        GroundQuad InLevel(GroundQuad q)
        {
            if (!q.Known || _level == null) return q;
            LevelPoint o = _level.ToLevel(Vector3.zero);
            q.X0 += o.X; q.Z0 += o.Y;
            q.X1 += o.X; q.Z1 += o.Y;
            q.X2 += o.X; q.Z2 += o.Y;
            q.X3 += o.X; q.Z3 += o.Y;
            return q;
        }

        /// <summary>For the tests: one step of the waves, as a frame takes it.</summary>
        public void TickRoomWavesForTests() => TickRoomWaves();
    }
}
