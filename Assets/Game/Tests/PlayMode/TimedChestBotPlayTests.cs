using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The numbers of work order 153 (the timed chest) for him, from a TEST BOT, not from play: on floors 2 to 5 of three run seeds, the
    /// floor's chest room is fought by work order 136's stand-in for her (she walks toward the nearest enemy of the room in play until she
    /// is within <see cref="Closes"/> of it and shoots it with the basic shot, the camera following her; her health filled again every
    /// frame, the damage she took counted), and it also kills the nearest enemy in play within <see cref="KillReach"/> every so many
    /// seconds: the room's own fight at his pace (<see cref="HisPace"/>, 136's bot's), then the chest opened beside it and its wave fought
    /// from the room's middle at a practised pace and, on the same floor built again, at a slow one (<see cref="Practised"/>,
    /// <see cref="Slow"/>). Counted: how often a floor has a chest, each wave's kinds and count, its clear time, the share of the reward
    /// left, the Echoes, the damage she took, against the room's own fight. Writes "CHESTBOT" lines. Explicit: about a quarter of an hour,
    /// run by name only. Frames without a window are short: the fights run at <see cref="Speed"/> times game speed.
    /// </summary>
    public class TimedChestBotPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] RunSeedTexts = { "", "WAVES236", "CHEST253" }; // "": the benchmark's run (BenchmarkStage.RunSeed)
        const float Closes = 8f;          // m from the nearest enemy she walks to before she stands and shoots
        const float MostSeconds = 90f;    // game seconds a fight is fought at most (the reward reaches its floor at 99 s)
        const float Speed = 4f;           // game time's speed in the fights
        const float KillReach = 12f;      // m within which the stand-in kills (136's bot's)
        const float HisPace = 2f;         // s between its kills in the room's own fight (136's bot: his sessions' median pace)
        const float Practised = 2f;       // s between its kills in the chest's wave, practised: his pace
        const float Slow = 4f;            // s between its kills in the chest's wave, slow: half his pace

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
            PlayerInputDriver.ReadRealDevices = false;
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

        static Vector3 OnNavMesh(Vector3 p) => NavMesh.SamplePosition(p, out NavMeshHit hit, 4f, NavMesh.AllAreas) ? hit.position : p;

        static float Median(List<float> v)
        {
            if (v.Count == 0) return float.NaN;
            v.Sort();
            return v.Count % 2 == 1 ? v[v.Count / 2] : 0.5f * (v[v.Count / 2 - 1] + v[v.Count / 2]);
        }

        sealed class Chest
        {
            public int Floor;
            public string Kinds = "", Room = "";
            public int Count, Whole, Echoes, Damage, RoomDamage;
            public float Seconds, RoomSeconds, Share = float.NaN;
            public bool Cleared;
        }

        /// <summary>The stand-in fights while <paramref name="enemies"/> holds one alive (those not yet in play counted too), at most <see cref="MostSeconds"/>; the damage she took and the seconds.</summary>
        IEnumerator Fight(Func<List<Enemy>> enemies, float every, Action<float, int> done)
        {
            _health.RestoreFull();
            _hurts.Clear();
            float start = Time.time;
            Time.timeScale = Speed;
            float until = Time.time + MostSeconds, nextKill = Time.time + every;
            while (Time.time < until && enemies().Any(e => e != null && !e.IsDead))
            {
                Enemy target = null;
                float best = float.PositiveInfinity;
                foreach (Enemy e in enemies())
                {
                    if (e == null || e.IsDead || !e.isActiveAndEnabled) continue;
                    float d = Flat(e.transform.position, _player.Position);
                    if (d < best) { best = d; target = e; }
                }
                if (target != null && Time.time >= nextKill && best <= KillReach)
                {
                    target.Body.TakeHit(100000000);
                    nextKill = Time.time + every;
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
            Time.timeScale = 1f;
            _shoot = false;
            _move = Vector2.zero;
            done(Time.time - start, _hurts.Sum(h => h.amount));
        }

        /// <summary>One floor's chest: the room's own fight at his pace (or cleared at once), the chest opened beside it, its wave fought from the middle at <paramref name="every"/>.</summary>
        IEnumerator FightAChest(ulong run, int floor, float every, bool roomAtHisPace, List<Chest> into, Action<bool> held)
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.Build(RunSeeds.FloorSeed(run, floor), floor);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            GeneratedLevels.TimedChest t = _levels.TimedChestOfFloor;
            held(t != null);
            if (t == null) yield break;
            var c = new Chest { Floor = floor, Whole = t.Whole, Room = t.Room.Letter + "" + t.Room.Order + " " + (t.Room.LineUp != null ? t.Room.LineUp.Name : "") };
            LevelRoom room = t.Room;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, _levels.Values.Fights.PointIn + 1.5, 0d)));
            RoomFight fight = _levels.FightOf(room);
            for (int i = 0; i < 60 && !fight.On; i++) yield return null;
            List<Enemy> Own() => level.EnemiesByRoom[room.Order];
            if (roomAtHisPace) yield return Fight(Own, HisPace, (s, d) => { c.RoomSeconds = s; c.RoomDamage = d; });
            for (int i = 0; i < 40 && (fight.On || Own().Any(e => e != null && !e.IsDead)); i++)
            {
                foreach (Enemy e in Own().ToList()) if (e != null && !e.IsDead) e.Body.TakeHit(100000000);
                yield return Frames(10);
            }
            GeneratedLevels.RoomDoors doors = _levels.DoorsOf(room);
            for (int i = 0; i < 300 && (t.State != GeneratedLevels.TimedChestState.Openable || (doors != null && doors.Shut)); i++) yield return null;
            Assert.That(t.State, Is.EqualTo(GeneratedLevels.TimedChestState.Openable), "floor " + floor + ": the chest can be opened");
            Vector3 middle = OnNavMesh(level.Middle(room));
            Vector3 toward = new Vector3(middle.x - t.Place.x, 0f, middle.z - t.Place.z);
            _player.Teleport(OnNavMesh(t.Place + (toward.sqrMagnitude > 1e-4f ? toward.normalized : Vector3.forward) * 1.4f));
            yield return Frames(2);
            Assert.That(_levels.UseTimedChest(), Is.True, "floor " + floor + ": it opens");
            _player.Teleport(middle); // she turns from it into the room
            c.Count = t.Wave.Count;
            c.Kinds = t.Kinds;
            yield return Fight(() => t.Wave, every, (s, d) => { c.Seconds = s; c.Damage = d; });
            c.Cleared = t.State == GeneratedLevels.TimedChestState.Won;
            if (c.Cleared)
            {
                c.Share = (float)_levels.TimedChestShare;
                c.Echoes = _levels.TimedChestEchoes;
                c.Seconds = t.WonAt - t.OpenedAt;
            }
            into.Add(c);
            foreach (Enemy e in t.Wave) if (e != null && !e.IsDead) e.Body.TakeHit(100000000);
            yield return Frames(3);
        }

        string Words(IEnumerable<Chest> chests)
        {
            List<Chest> all = chests.ToList();
            if (all.Count == 0) return "none";
            List<Chest> c = all.Where(x => x.Cleared).ToList();
            return string.Format(Inv,
                "{0} chests ({1} cleared); wave count mean {2:0.0}; clear time median {3:0.0} s (range {4:0.0} to {5:0.0}); reward left median {6:0} % (range {7:0} to {8:0} %); Echoes taken mean {9:0.0} of {10:0.0}; her damage in the wave mean {11:0.0}",
                all.Count, c.Count, all.Average(x => x.Count), Median(c.Select(x => x.Seconds).ToList()), c.Count > 0 ? c.Min(x => x.Seconds) : float.NaN,
                c.Count > 0 ? c.Max(x => x.Seconds) : float.NaN, 100f * Median(c.Select(x => x.Share).ToList()), c.Count > 0 ? 100f * c.Min(x => x.Share) : float.NaN,
                c.Count > 0 ? 100f * c.Max(x => x.Share) : float.NaN, c.Count > 0 ? c.Average(x => x.Echoes) : 0, all.Average(x => x.Whole), all.Average(x => x.Damage));
        }

        [Explicit("about a quarter of an hour: the numbers for the report, run by name"), UnityTest, Timeout(3600000)]
        public IEnumerator OnFloors2To5_ThreeRuns_TheChestsWave_PractisedAndSlow_AgainstTheRoomsOwnFight_TheBotsNumbers()
        {
            var practised = new List<Chest>();
            var slow = new List<Chest>();
            int floors = 0, held = 0;
            foreach (string text in RunSeedTexts)
            {
                ulong run = BenchmarkStage.RunSeed;
                if (text.Length > 0) Assert.That(LevelSeed.TryParse(text, out run), Is.True, text);
                for (int floor = 2; floor <= 5; floor++)
                {
                    floors++;
                    bool has = false;
                    yield return FightAChest(run, floor, Practised, true, practised, h => has = h);
                    if (!has) continue;
                    held++;
                    yield return FightAChest(run, floor, Slow, false, slow, h => { });
                    Chest p = practised[practised.Count - 1], s = slow.Count > 0 ? slow[slow.Count - 1] : null;
                    string line = string.Format(Inv,
                        "CHESTBOT run {0} floor {1} room {2}: the room's own fight {3:0.0} s, her damage {4}; its wave {5} ({6}), {7} Echoes whole; practised: {8:0.0} s, {9:0} % left, {10} Echoes, damage {11}{12}; slow: {13}",
                        text.Length > 0 ? text : "benchmark", floor, p.Room, p.RoomSeconds, p.RoomDamage, p.Kinds, p.Count, p.Whole, p.Seconds, 100f * p.Share, p.Echoes, p.Damage,
                        p.Cleared ? "" : " (not cleared)",
                        s == null ? "-" : string.Format(Inv, "{0:0.0} s, {1:0} % left, {2} Echoes, damage {3}{4}", s.Seconds, 100f * s.Share, s.Echoes, s.Damage, s.Cleared ? "" : " (not cleared)"));
                    Debug.Log(line);
                    TestContext.WriteLine(line);
                }
            }
            for (int floor = 2; floor <= 5; floor++)
            {
                string line = string.Format(Inv, "CHESTBOT floor {0} practised (a kill every {1:0} s): {2}\nCHESTBOT floor {0} slow (a kill every {3:0} s): {4}\nCHESTBOT floor {0} the room's own fight at his pace: median {5:0.0} s, her damage mean {6:0.0}",
                    floor, Practised, Words(practised.Where(c => c.Floor == floor)), Slow, Words(slow.Where(c => c.Floor == floor)),
                    Median(practised.Where(c => c.Floor == floor).Select(c => c.RoomSeconds).ToList()), practised.Where(c => c.Floor == floor).Select(c => (float)c.RoomDamage).DefaultIfEmpty(0f).Average());
                Debug.Log(line);
                TestContext.WriteLine(line);
            }
            string all = string.Format(Inv, "CHESTBOT all: floors with a chest {0} of {1}; practised: {2}; slow: {3}; the room's own fight at his pace: median {4:0.0} s, her damage mean {5:0.0}",
                held, floors, Words(practised), Words(slow), Median(practised.Select(c => c.RoomSeconds).ToList()), practised.Select(c => (float)c.RoomDamage).DefaultIfEmpty(0f).Average());
            Debug.Log(all);
            TestContext.WriteLine(all);
            Assert.That(held, Is.EqualTo(floors), "every floor from 2 has a chest");
        }
    }
}
