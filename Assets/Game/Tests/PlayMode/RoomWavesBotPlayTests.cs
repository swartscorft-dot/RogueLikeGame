using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The numbers of work order 136 (two waves) for him, from a TEST BOT, not from play: on floors 1 to 5 of two run seeds, every combat
    /// room but the elite room is fought by a simple stand-in for her (WarderAndLancerOnScreenPlayTests' stand-in: she walks toward the
    /// nearest enemy of the room in play until she is within <see cref="Closes"/> of it, and shoots it with the basic shot, the camera
    /// following her; her health filled again every frame, the damage she took counted with its time), three times: version 24's rooms
    /// (the line-ups and the waves off: everything wakes at once), work order 135 alone (the line-ups on, the waves off), and with the
    /// waves. Counted for each fight: the kind of its last kill, the sameness of its kill order (Kendall's tau-b of the kill times against
    /// small, melee, ranged, as research R3's rooms.py), the seconds from the waking to her first hit on a ranged enemy, the clear time, the
    /// damage she took, the damage in its first <see cref="Window"/> s, the longest time in the fight with no enemy of it in play, the
    /// arrivals that had to wait for a point, and the most enemies awake at once; and for each later wave the damage in the
    /// <see cref="Window"/> s after its first enemy stood (by its kind and size), and how many of it were held back for want of a point.
    /// The stand-in also kills the nearest enemy in play within <see cref="KillReach"/> every <see cref="KillEvery"/> s, his pace. Writes "WAVESBOT" lines. Explicit: about a quarter of an hour, run by name only.
    ///
    /// Frames without a window are short (about 0.3 ms): the fights run at <see cref="Speed"/> times game speed and every wait counts
    /// game seconds.
    /// </summary>
    public class RoomWavesBotPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] RunSeedTexts = { "", "WAVES236" }; // "": the benchmark's run (BenchmarkStage.RunSeed)
        const float Closes = 8f;          // m from the nearest enemy she walks to before she stands and shoots
        const float MostSeconds = 60f;    // game seconds a room is fought at most
        const float Speed = 4f;           // game time's speed in the fights
        const float Window = 6f;          // s after a waking or a wave's arrival whose damage is counted (the producer's 6 s)
        // The stand-in's basic shot alone cleared 5 of 40 rooms in a minute (the first measure): so it also kills at his pace, the
        // nearest enemy in play within KillReach every KillEvery s (his sessions of 2026-11-25: a median 19.8 s and about 10 enemies a fight).
        const float KillEvery = 2f;
        const float KillReach = 12f;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        Vector3 _aimAt;
        bool _shoot;
        Vector2 _move;
        readonly List<(float t, int amount)> _hurts = new List<(float, int)>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = Input;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Damaged += OnDamaged;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void OnDamaged(int amount) => _hurts.Add((Time.time, amount));

        InputFrame? Input()
        {
            Camera cam = Camera.main;
            if (cam == null) return null;
            Vector3 screen = cam.WorldToScreenPoint(new Vector3(_aimAt.x, 0f, _aimAt.z));
            return new InputFrame { CursorScreen = new Vector2(screen.x, screen.y), ShootPressed = _shoot, ShootHeld = _shoot, Move = _move };
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Research R3's tiers of his sentence: "the small ones first, then the chargers, then the ranged".</summary>
        static int Tier(EnemyKind k) =>
            k == EnemyKind.Swarmer || k == EnemyKind.Bomber ? 1 : k == EnemyKind.Shooter || k == EnemyKind.Caster || k == EnemyKind.Warder ? 3 : 2;

        sealed class Wave
        {
            public string Kinds;
            public int Count, Held;
            public float Damage;
            public bool Deadly;
        }

        sealed class Fight
        {
            public bool Cleared;
            public float Seconds, FirstRangedHit = -1f, LongestEmpty, Tau = float.NaN, Opening;
            public int Damage, MostAwake, Enemies, Waves = 1, WaitTries, Waited;
            public bool HasRanged, LastRanged, OpeningDeadly;
            public readonly List<Wave> Later = new List<Wave>();
        }

        static float Median(List<float> v)
        {
            if (v.Count == 0) return float.NaN;
            v.Sort();
            return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5f * (v[v.Count / 2 - 1] + v[v.Count / 2]);
        }

        sealed class Tally
        {
            public readonly List<Fight> Fights = new List<Fight>();

            public override string ToString()
            {
                if (Fights.Count == 0) return "no fights";
                List<Fight> c = Fights.Where(f => f.Cleared).ToList();
                List<Fight> ranged = c.Where(f => f.HasRanged).ToList();
                List<float> taus = c.Where(f => !float.IsNaN(f.Tau)).Select(f => f.Tau).ToList();
                List<float> hits = Fights.Where(f => f.FirstRangedHit >= 0f).Select(f => f.FirstRangedHit).ToList();
                return string.Format(Inv,
                    "{0} fights ({1} cleared, {2} with waves); last kill ranged {3}/{4} ({5:0} %); sameness median {6:0.00} over {7}; first hit on a ranged enemy median {8:0.0} s ({9} fights); clear time median {10:0.0} s; damage taken mean {11:0.0}; first {19:0} s after the waking: mean {20:0.0}, worst {21:0}, deadly {22}; longest time with none in play: most {12:0.00} s, mean {13:0.00} s; arrivals that waited {14} (tries {15}); most awake at once: mean {16:0.0}, most {17}; enemies a fight mean {18:0.0}",
                    Fights.Count, c.Count, Fights.Count(f => f.Waves > 1), ranged.Count(f => f.LastRanged), ranged.Count,
                    ranged.Count > 0 ? 100.0 * ranged.Count(f => f.LastRanged) / ranged.Count : double.NaN, Median(taus), taus.Count,
                    Median(hits), hits.Count, Median(c.Select(f => f.Seconds).ToList()), Fights.Average(f => f.Damage),
                    Fights.Max(f => f.LongestEmpty), Fights.Average(f => f.LongestEmpty), Fights.Sum(f => f.Waited), Fights.Sum(f => f.WaitTries),
                    Fights.Average(f => f.MostAwake), Fights.Max(f => f.MostAwake), Fights.Average(f => f.Enemies),
                    Window, Fights.Average(f => f.Opening), Fights.Max(f => f.Opening), Fights.Count(f => f.OpeningDeadly));
            }

            /// <summary>The later waves, by their kinds and counts: how many, the damage in their first seconds (mean, worst), how many of them were held back.</summary>
            public string WavesWords()
            {
                List<Wave> all = Fights.SelectMany(f => f.Later).ToList();
                if (all.Count == 0) return "no later waves";
                return string.Join("; ", all.GroupBy(w => w.Kinds).OrderBy(g => g.Key).Select(g => string.Format(Inv,
                    "{0}: {1} waves, {2:0} s after its first stood: mean {3:0.0}, worst {4:0}, deadly {5}; held back {6} of {7} enemies in {8} waves",
                    g.Key, g.Count(), Window, g.Average(w => w.Damage), g.Max(w => w.Damage), g.Count(w => w.Deadly), g.Sum(w => w.Held), g.Sum(w => w.Count),
                    g.Count(w => w.Held > 0))));
            }
        }

        static float KendallTauB(IReadOnlyList<float> x, IReadOnlyList<int> y)
        {
            int conc = 0, disc = 0, tx = 0, ty = 0;
            for (int i = 0; i < x.Count; i++)
                for (int j = i + 1; j < x.Count; j++)
                {
                    int dx = Math.Sign(x[i] - x[j]), dy = Math.Sign(y[i] - y[j]);
                    if (dx == 0 && dy == 0) continue;
                    if (dx == 0) tx++;
                    else if (dy == 0) ty++;
                    else if (dx == dy) conc++;
                    else disc++;
                }
            double den = Math.Sqrt((double)(conc + disc + tx) * (conc + disc + ty));
            return den > 0 ? (float)((conc - disc) / den) : float.NaN;
        }

        float DamageBetween(float from, float to)
        {
            float sum = 0f;
            foreach ((float t, int amount) in _hurts) if (t >= from && t < to) sum += amount;
            return sum;
        }

        IEnumerator FightThem(Action<LevelValues> mode, Dictionary<int, Tally> tallies)
        {
            foreach (string text in RunSeedTexts)
            {
                ulong run = BenchmarkStage.RunSeed;
                if (text.Length > 0) Assert.That(LevelSeed.TryParse(text, out run), Is.True, text);
                for (int floor = 1; floor <= 5; floor++)
                {
                    if (!tallies.ContainsKey(floor)) tallies[floor] = new Tally();
                    _levels.Values = new LevelValues();
                    mode(_levels.Values);
                    GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(run, floor), floor);
                    Assert.That(level, Is.Not.Null);
                    yield return Frames(2);
                    List<LevelRoom> rooms = level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat && !r.Elite).OrderBy(r => r.Order).ToList();
                    foreach (LevelRoom room in rooms)
                    {
                        var fight = new Fight();
                        yield return FightRoom(level, room, fight);
                        tallies[floor].Fights.Add(fight);
                    }
                }
            }
            Time.timeScale = 1f;
        }

        IEnumerator FightRoom(GeneratedLevel level, LevelRoom room, Fight result)
        {
            _health.RestoreFull();
            List<Enemy> own = level.EnemiesByRoom[room.Order].Where(e => e != null).ToList();
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "room " + room.Letter + room.Order + " holds its fight");
            GeneratedLevels.RoomWaves w = _levels.WavesOf(room);
            result.Waves = w?.Count ?? 1;
            result.Enemies = own.Count;
            result.HasRanged = own.Any(e => Tier(e.Kind) == 3);
            var deaths = new List<(float t, int tier)>();
            float wokeAt = -1f;
            var handlers = new List<(Damageable, Action<int>)>();
            foreach (Enemy e in own)
            {
                if (Tier(e.Kind) != 3) continue;
                Damageable d = e.Body;
                Action<int> h = _ => { if (result.FirstRangedHit < 0f && wokeAt >= 0f) result.FirstRangedHit = Time.time - wokeAt; };
                d.Damaged += h;
                handlers.Add((d, h));
            }
            var dead = new HashSet<Enemy>();
            LevelLayout layout = level.Layout;
            LevelDoor door = LevelGeometry.EntryDoor(layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(layout, room, door, _levels.Values.Fights.PointIn + 1.5, 0d)));
            for (int i = 0; i < 60 && !fight.On; i++) yield return null;
            wokeAt = Time.time;
            _hurts.Clear();
            Time.timeScale = Speed;
            float until = Time.time + MostSeconds, emptySince = -1f, nextKill = Time.time + KillEvery;
            while (Time.time < until && own.Any(e => !e.IsDead))
            {
                Enemy target = null;
                float best = float.PositiveInfinity;
                int awake = 0, inPlay = 0;
                foreach (Enemy e in own)
                {
                    if (e.IsDead)
                    {
                        if (dead.Add(e)) deaths.Add((Time.time - wokeAt, Tier(e.Kind)));
                        continue;
                    }
                    if (!e.isActiveAndEnabled) continue;
                    inPlay++;
                    if (e.IsAwake) awake++;
                    float d = Flat(e.transform.position, _player.Position);
                    if (d < best) { best = d; target = e; }
                }
                result.MostAwake = Math.Max(result.MostAwake, awake);
                if (inPlay == 0)
                {
                    if (emptySince < 0f) emptySince = Time.time;
                    result.LongestEmpty = Math.Max(result.LongestEmpty, Time.time - emptySince);
                }
                else emptySince = -1f;
                if (target != null && Time.time >= nextKill && best <= KillReach)
                {
                    target.Body.TakeHit(100000000);
                    nextKill = Time.time + KillEvery;
                }
                if (target != null)
                {
                    _aimAt = target.transform.position;
                    _shoot = true;
                    Vector3 to = target.transform.position - _player.Position;
                    _move = best > Closes ? new Vector2(Mathf.Abs(to.x) > 0.4f * best ? Mathf.Sign(to.x) : 0f, Mathf.Abs(to.z) > 0.4f * best ? Mathf.Sign(to.z) : 0f) : Vector2.zero;
                }
                else
                {
                    _shoot = false;
                    _move = Vector2.zero;
                }
                if (_health.Current < _health.Max) _health.RestoreFull();
                yield return null;
            }
            foreach (Enemy e in own)
                if (e.IsDead && dead.Add(e)) deaths.Add((Time.time - wokeAt, Tier(e.Kind)));
            Time.timeScale = 1f;
            _shoot = false;
            _move = Vector2.zero;
            int most = _health.Max;
            result.Cleared = own.All(e => e.IsDead);
            result.Seconds = Time.time - wokeAt;
            result.Damage = (int)DamageBetween(float.NegativeInfinity, float.PositiveInfinity);
            result.Opening = DamageBetween(wokeAt, wokeAt + Window);
            result.OpeningDeadly = result.Opening >= most;
            if (w != null)
            {
                result.WaitTries = w.WaitTries;
                result.Waited = w.Waited;
                float disc = _levels.Values.WaveRules.DiscSeconds;
                for (int k = 2; k <= w.Count; k++)
                {
                    List<GeneratedLevels.WaveArrival> of = w.Arrived.Where(a => a.Wave == k).ToList();
                    List<Enemy> members = w.Waves[k - 1];
                    var wave = new Wave { Kinds = RoomWaveRules.KindsWords(members.Where(e => e != null).Select(e => e.Kind).ToList()), Count = members.Count };
                    if (of.Count == 0)
                    {
                        wave.Held = members.Count;
                        result.Later.Add(wave);
                        continue;
                    }
                    float first = of.Min(a => a.Since);
                    wave.Held = of.Count(a => a.Since > first + 0.01f) + (members.Count - of.Count);
                    float stood = first + disc;
                    wave.Damage = DamageBetween(stood, stood + Window);
                    wave.Deadly = wave.Damage >= most;
                    result.Later.Add(wave);
                }
            }
            if (deaths.Count > 0) result.LastRanged = deaths[deaths.Count - 1].tier == 3;
            if (result.Cleared && deaths.Select(d => d.tier).Distinct().Count() >= 2)
                result.Tau = KendallTauB(deaths.Select(d => d.t).ToList(), deaths.Select(d => d.tier).ToList());
            foreach ((Damageable d, Action<int> h) in handlers)
                if (d != null) d.Damaged -= h;
            foreach (Enemy e in own)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
            yield return Frames(3);
        }

        [Explicit("about a quarter of an hour: the numbers for the report, run by name"), UnityTest, Timeout(3600000)]
        public IEnumerator OnFloors1To5_Version24s_LineUpsAlone_AndWithWaves_TheBotsNumbers()
        {
            var v24 = new Dictionary<int, Tally>();
            var lineUps = new Dictionary<int, Tally>();
            var waves = new Dictionary<int, Tally>();
            yield return FightThem(v => { v.LineUps = false; v.Waves = false; }, v24);
            yield return FightThem(v => { v.Waves = false; }, lineUps);
            yield return FightThem(v => { }, waves);
            Report(v24, lineUps, waves);
            Assert.That(waves.Values.Sum(t => t.Fights.Count(f => f.Waves > 1)), Is.GreaterThan(0), "rooms with waves were fought");
        }

        static void Report(Dictionary<int, Tally> v24, Dictionary<int, Tally> lineUps, Dictionary<int, Tally> waves)
        {
            for (int floor = 1; floor <= 5; floor++)
            {
                string line = string.Format(Inv,
                    "WAVESBOT floor {0} version 24 (all at once): {1}\nWAVESBOT floor {0} line-ups alone (135): {2}\nWAVESBOT floor {0} waves ON: {3}\nWAVESBOT floor {0} later waves: {4}",
                    floor, v24[floor], lineUps[floor], waves[floor], waves[floor].WavesWords());
                Debug.Log(line);
                TestContext.WriteLine(line);
            }
            Tally All(Dictionary<int, Tally> d)
            {
                var t = new Tally();
                foreach (Tally x in d.Values) t.Fights.AddRange(x.Fights);
                return t;
            }
            string all = string.Format(Inv, "WAVESBOT all floors version 24: {0}\nWAVESBOT all floors line-ups alone: {1}\nWAVESBOT all floors waves ON: {2}\nWAVESBOT all floors later waves: {3}",
                                       All(v24), All(lineUps), All(waves), All(waves).WavesWords());
            Debug.Log(all);
            TestContext.WriteLine(all);
        }
    }
}
