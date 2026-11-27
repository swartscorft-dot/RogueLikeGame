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
    /// The numbers of work order 137 (the caller) for him, from a TEST BOT, not from play: on floors 2 to 5 of two run seeds, every combat
    /// room but the elite room fought by work order 136's stand-in (<see cref="RoomWavesBotPlayTests"/>: she walks toward her target until
    /// within <see cref="Closes"/> of it and shoots it, the camera following her; her health filled every frame, the damage counted; she kills
    /// her target at his pace, one every <see cref="KillEvery"/> s within <see cref="KillReach"/>), three times: work order 136's rooms (the
    /// callers off), the callers on with her target always the nearest enemy in play (the called swarmers too), and the callers on with
    /// her target the caller while it lives (the way his words say a caller is meant to be met). Counted for each fight: the kind of its
    /// last kill, the sameness of its kill order (Kendall's tau-b of the kill times against small, melee, ranged; the caller read as ranged,
    /// its called left out, as research R3's rooms.py should read them), the seconds from the waking to her first hit on a ranged enemy, its
    /// clear time and damage, whether its room lacks a core kind; and in a caller's room the time to her first hit on the caller and to its
    /// death, the swarmers it called, and whether it died first, in the fight's first half or last. Writes "CALLERSBOT" lines. Explicit:
    /// about half an hour, run by name only.
    /// </summary>
    public class RoomCallersBotPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] RunSeedTexts = { "", "WAVES236" }; // 136's: "" is the benchmark's run (BenchmarkStage.RunSeed)
        const float Closes = 8f;          // m from her target she walks to before she stands and shoots
        const float MostSeconds = 60f;    // game seconds a room is fought at most
        const float Speed = 4f;           // game time's speed in the fights
        const float KillEvery = 2f;       // 136's pace: one kill every 2 s (his sessions of 2026-11-25)
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

        /// <summary>Research R3's tiers of his sentence ("the small ones first, then the chargers, then the ranged"), the caller ranged.</summary>
        static int Tier(EnemyKind k) =>
            k == EnemyKind.Swarmer || k == EnemyKind.Bomber ? 1
            : k == EnemyKind.Shooter || k == EnemyKind.Caster || k == EnemyKind.Warder || k == EnemyKind.Caller ? 3 : 2;

        sealed class Fight
        {
            public bool Cleared, HasCaller, HasRanged, LastRanged, LacksACore;
            public float Seconds, FirstRangedHit = -1f, Tau = float.NaN;
            public int Damage;
            // A caller's room.
            public float CallerFirstHit = -1f, CallerDied = -1f;
            public int Called, Fell;
            public bool CallerFirst, CallerFirstHalf, CallerLast;
        }

        static float Median(List<float> v)
        {
            if (v.Count == 0) return float.NaN;
            v.Sort();
            return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5f * (v[v.Count / 2 - 1] + v[v.Count / 2]);
        }

        static string Pct(int n, int of) => of > 0 ? string.Format(Inv, "{0}/{1} ({2:0} %)", n, of, 100.0 * n / of) : "0/0";

        sealed class Tally
        {
            public readonly List<Fight> Fights = new List<Fight>();

            /// <summary>Research R3's four numbers on these fights.</summary>
            public string Four()
            {
                if (Fights.Count == 0) return "no fights";
                List<Fight> c = Fights.Where(f => f.Cleared).ToList();
                List<Fight> ranged = c.Where(f => f.HasRanged).ToList();
                List<float> taus = c.Where(f => !float.IsNaN(f.Tau)).Select(f => f.Tau).ToList();
                List<float> hits = Fights.Where(f => f.FirstRangedHit >= 0f).Select(f => f.FirstRangedHit).ToList();
                return string.Format(Inv,
                    "{0} fights ({1} cleared); last kill ranged {2}; sameness median {3:0.00} over {4}; first hit on a ranged enemy median {5:0.0} s ({6} fights); lacking a core kind {7}; clear time median {8:0.0} s; damage taken mean {9:0.0}",
                    Fights.Count, c.Count, Pct(ranged.Count(f => f.LastRanged), ranged.Count), Median(taus), taus.Count, Median(hits), hits.Count,
                    Pct(Fights.Count(f => f.LacksACore), Fights.Count), Median(c.Select(f => f.Seconds).ToList()), Fights.Average(f => f.Damage));
            }

            /// <summary>The caller's rooms against the rest of the same fights.</summary>
            public string Callers()
            {
                List<Fight> with = Fights.Where(f => f.HasCaller).ToList();
                List<Fight> without = Fights.Where(f => !f.HasCaller).ToList();
                if (with.Count == 0) return "no caller's room";
                List<float> hit = with.Where(f => f.CallerFirstHit >= 0f).Select(f => f.CallerFirstHit).ToList();
                List<float> died = with.Where(f => f.CallerDied >= 0f).Select(f => f.CallerDied).ToList();
                return string.Format(Inv,
                    "{0} caller's rooms of {1} ({2:0} %); first hit on it median {3:0.0} s ({4} fights); its death median {5:0.0} s ({6} died of {0}); swarmers called a fight mean {7:0.0}, fell with it mean {8:0.0}; it died first {9}, in the first half {10}, last {11}; with one: cleared {12}, clear time median {13:0.0} s, damage mean {14:0.0}; without (same floors): cleared {15}, clear time median {16:0.0} s, damage mean {17:0.0}",
                    with.Count, Fights.Count, 100.0 * with.Count / Math.Max(1, Fights.Count), Median(hit), hit.Count, Median(died), died.Count,
                    with.Average(f => f.Called), with.Average(f => f.Fell), Pct(with.Count(f => f.CallerFirst), with.Count),
                    Pct(with.Count(f => f.CallerFirstHalf), with.Count), Pct(with.Count(f => f.CallerLast), with.Count),
                    Pct(with.Count(f => f.Cleared), with.Count), Median(with.Where(f => f.Cleared).Select(f => f.Seconds).ToList()), with.Average(f => f.Damage),
                    Pct(without.Count(f => f.Cleared), without.Count), Median(without.Where(f => f.Cleared).Select(f => f.Seconds).ToList()),
                    without.Count > 0 ? without.Average(f => f.Damage) : double.NaN);
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

        IEnumerator FightThem(bool callers, bool callerFirst, Dictionary<int, Tally> tallies)
        {
            foreach (string text in RunSeedTexts)
            {
                ulong run = BenchmarkStage.RunSeed;
                if (text.Length > 0) Assert.That(LevelSeed.TryParse(text, out run), Is.True, text);
                for (int floor = 2; floor <= 5; floor++)
                {
                    if (!tallies.ContainsKey(floor)) tallies[floor] = new Tally();
                    _levels.Values = new LevelValues { Callers = callers };
                    GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(run, floor), floor);
                    Assert.That(level, Is.Not.Null);
                    yield return Frames(2);
                    List<LevelRoom> rooms = level.Layout.Rooms.Where(r => r.Kind == RoomKind.Combat && !r.Elite).OrderBy(r => r.Order).ToList();
                    foreach (LevelRoom room in rooms)
                    {
                        var fight = new Fight();
                        yield return FightRoom(level, room, fight, callerFirst);
                        tallies[floor].Fights.Add(fight);
                    }
                }
            }
            Time.timeScale = 1f;
        }

        IEnumerator FightRoom(GeneratedLevel level, LevelRoom room, Fight result, bool callerFirst)
        {
            _health.RestoreFull();
            List<Enemy> own = level.EnemiesByRoom[room.Order].Where(e => e != null).ToList();
            RoomFight fight = _levels.FightOf(room);
            Assert.That(fight, Is.Not.Null, "room " + room.Letter + room.Order + " holds its fight");
            GeneratedLevels.RoomCaller rc = _levels.CallerOf(room);
            EnemyCaller caller = rc?.Caller;
            result.HasCaller = caller != null;
            result.HasRanged = own.Any(e => Tier(e.Kind) == 3);
            result.LacksACore = own.Select(e => Tier(e.Kind)).Distinct().Count() < 3;
            var deaths = new List<(float t, int tier, Enemy e)>();
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
            float until = Time.time + MostSeconds, nextKill = Time.time + KillEvery;
            var inPlay = new List<Enemy>();
            while (Time.time < until && own.Any(e => !e.IsDead))
            {
                inPlay.Clear();
                foreach (Enemy e in own)
                {
                    if (e.IsDead)
                    {
                        if (dead.Add(e)) deaths.Add((Time.time - wokeAt, Tier(e.Kind), e));
                        continue;
                    }
                    if (e.isActiveAndEnabled) inPlay.Add(e);
                }
                if (rc != null) foreach (Enemy e in rc.Called) if (e != null && !e.IsDead && e.isActiveAndEnabled) inPlay.Add(e);
                Enemy target = null;
                float best = float.PositiveInfinity;
                if (callerFirst && caller != null && !caller.IsDead)
                {
                    target = caller;
                    best = Flat(caller.transform.position, _player.Position);
                }
                else
                    foreach (Enemy e in inPlay)
                    {
                        float d = Flat(e.transform.position, _player.Position);
                        if (d < best) { best = d; target = e; }
                    }
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
                if (e.IsDead && dead.Add(e)) deaths.Add((Time.time - wokeAt, Tier(e.Kind), e));
            Time.timeScale = 1f;
            _shoot = false;
            _move = Vector2.zero;
            result.Cleared = own.All(e => e.IsDead);
            result.Seconds = Time.time - wokeAt;
            result.Damage = _hurts.Sum(h => h.amount);
            if (deaths.Count > 0) result.LastRanged = deaths[deaths.Count - 1].tier == 3;
            if (result.Cleared && deaths.Select(d => d.tier).Distinct().Count() >= 2)
                result.Tau = KendallTauB(deaths.Select(d => d.t).ToList(), deaths.Select(d => d.tier).ToList());
            if (caller != null)
            {
                result.Called = rc.Called.Count;
                result.Fell = rc.Fell;
                if (caller.FirstHitAt >= 0f) result.CallerFirstHit = caller.FirstHitAt - wokeAt;
                if (caller.DiedAt >= 0f)
                {
                    result.CallerDied = caller.DiedAt - wokeAt;
                    int at = deaths.FindIndex(d => d.e == caller);
                    result.CallerFirst = at == 0;
                    result.CallerLast = at == deaths.Count - 1;
                    result.CallerFirstHalf = result.Cleared && result.CallerDied <= 0.5f * result.Seconds + 1e-3f;
                }
            }
            foreach ((Damageable d, Action<int> h) in handlers)
                if (d != null) d.Damaged -= h;
            foreach (Enemy e in own)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
            yield return Frames(3);
        }

        [Explicit("about half an hour: the numbers for the report, run by name"), UnityTest, Timeout(5400000)]
        public IEnumerator OnFloors2To5_136sRooms_TheCallersNearestFirst_AndTheCallerFirst_TheBotsNumbers()
        {
            var before = new Dictionary<int, Tally>();
            var nearest = new Dictionary<int, Tally>();
            var callerFirst = new Dictionary<int, Tally>();
            yield return FightThem(false, false, before);
            yield return FightThem(true, false, nearest);
            yield return FightThem(true, true, callerFirst);
            Tally All(Dictionary<int, Tally> d)
            {
                var t = new Tally();
                foreach (Tally x in d.Values) t.Fights.AddRange(x.Fights);
                return t;
            }
            for (int floor = 2; floor <= 5; floor++)
            {
                string line = string.Format(Inv,
                    "CALLERSBOT floor {0} before (136, no caller): {1}\nCALLERSBOT floor {0} callers, nearest first: {2}\nCALLERSBOT floor {0} callers, nearest first, its rooms: {3}\nCALLERSBOT floor {0} callers, the caller first: {4}\nCALLERSBOT floor {0} callers, the caller first, its rooms: {5}",
                    floor, before[floor].Four(), nearest[floor].Four(), nearest[floor].Callers(), callerFirst[floor].Four(), callerFirst[floor].Callers());
                Debug.Log(line);
                TestContext.WriteLine(line);
            }
            string all = string.Format(Inv,
                "CALLERSBOT all floors before (136): {0}\nCALLERSBOT all floors callers, nearest first: {1}\nCALLERSBOT all floors callers, nearest first, its rooms: {2}\nCALLERSBOT all floors callers, the caller first: {3}\nCALLERSBOT all floors callers, the caller first, its rooms: {4}",
                All(before).Four(), All(nearest).Four(), All(nearest).Callers(), All(callerFirst).Four(), All(callerFirst).Callers());
            Debug.Log(all);
            TestContext.WriteLine(all);
            Assert.That(All(nearest).Fights.Count(f => f.HasCaller), Is.GreaterThan(0), "callers' rooms were fought");
        }
    }
}
