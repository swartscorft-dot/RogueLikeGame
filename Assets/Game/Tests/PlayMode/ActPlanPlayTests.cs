using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
    /// The act's plan in the floors (work order 087; Kristian, 2026-11-22: both new kinds into the floors, the warder from floor 3
    /// and the bomber from floor 4, and "Trekk fiendene nærmere"), in the ControlRoom scene through <see cref="GeneratedLevels"/>,
    /// with the default values: a floor 3 of a fixed seed builds a warder in a room, of the floor's health and damage scale, and
    /// when she crosses the room's point it wards an ally as in the practice room; a floor 4 builds a bomber that runs at her
    /// and blows; the session summary names both kinds and has their blocks. A shooter and a caster of a floor 3 room shoot at
    /// her only from within 11 m. She cannot be hurt. The waits count game seconds, sped up.
    /// </summary>
    public class ActPlanPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            if (_health != null) _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues { Waves = false }; // work order 136 (changed deliberately): its rooms wake whole, as the rules it measures were built on
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                if (_health != null) _health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static ulong RunSeedOf(int i) => (ulong)i * 0x9E3779B97F4A7C15UL + 87UL;

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>The first floor of this number, of run seeds 0, 1, 2 ... (as ActPlanTests makes them), with a combat room that has what is wanted: its seed and the room's order.</summary>
        static (ulong seed, int order) Find(int floor, Func<LevelRoom, bool> wants, LevelValues v)
        {
            for (int i = 0; i < 400; i++)
            {
                ulong seed = RunSeeds.FloorSeed(RunSeedOf(i), floor);
                LevelLayout layout = LevelGenerator.Generate(seed, floor, v);
                LevelRoom firstRoom = LevelGeometry.MainPath(layout).First(r => r.Kind == RoomKind.Combat);
                LevelRoom room = layout.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Combat && r != firstRoom && wants(r));
                if (room != null) return (seed, room.Order);
            }
            Assert.Fail("no floor " + floor + " of the first 400 run seeds has such a room");
            return (0UL, -1);
        }

        static int Of(LevelRoom r, EnemyKind k) => r.EnemyGroups.Where(g => g.Kind == k).Sum(g => g.Count);

        /// <summary>A ground point <paramref name="depth"/> m straight in from the middle of <paramref name="door"/>, into <paramref name="room"/>.</summary>
        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            LevelPoint p;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: p = new LevelPoint(mx, my + depth); break;
                case LevelGenerator.North: p = new LevelPoint(mx, my - depth); break;
                case LevelGenerator.West: p = new LevelPoint(mx + depth, my); break;
                default: p = new LevelPoint(mx - depth, my); break;
            }
            return level.ToWorld(p);
        }

        /// <summary>Builds the floor, and stands her past the room's point (5.5 m in from its door in): every one of its enemies is awake.</summary>
        IEnumerator Into(ulong seed, int floor, int order, Action<GeneratedLevel, LevelRoom, List<Enemy>> built)
        {
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null);
            yield return null;
            LevelRoom room = level.Layout.Rooms[order];
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            built(level, room, own);
            _player.Teleport(InFrom(level, room, LevelGeometry.EntryDoor(level.Layout, room), 5.5));
            for (int i = 0; i < 30 && !own.All(e => e == null || e.IsDead || e.IsAwake); i++) yield return null;
            if (!own.All(e => e.IsAwake))
            {
                // Not seen past the point from where she was put (the room fight's own tests walk her through the passage): a
                // hurt wakes the room as one fight, as any waking of one of its enemies does (work order 070).
                Debug.Log("ACTPLANPLAY " + room.Letter + room.Order + " not woken by her stand (" + own.Count(e => e.IsAwake) + " of " + own.Count + " awake): a hurt wakes it");
                own.First(e => !e.IsAwake).Body.TakeHit(1);
                for (int i = 0; i < 30 && !own.All(e => e == null || e.IsDead || e.IsAwake); i++) yield return null;
            }
            Assert.That(own.All(e => e.IsAwake), "every enemy of " + room.Letter + room.Order + " awake");
        }

        static IEnumerator ForGameSeconds(float seconds, Func<bool> done)
        {
            float until = Time.time + seconds;
            while (Time.time < until && !done()) yield return null;
        }

        // ---------------- The warder and the bomber in the floors ----------------

        [UnityTest]
        public IEnumerator AFloor3sWarder_WardsInItsRoomsFight_AFloor4sBomber_RunsAtHerAndBlows_AndTheSummaryNamesThem()
        {
            LevelValues v = _levels.Values;
            Assert.That(v.WardersInFloors && v.BombersInFloors, Is.True, "both on in the floors by default (work order 087)");
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeActPlanTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);

            // Floor 3: a room with a warder.
            (ulong seed3, int order3) = Find(3, r => Of(r, EnemyKind.Warder) == 1, v);
            EnemyWarder warder = null;
            yield return Into(seed3, 3, order3, (level, room, own) =>
            {
                Assert.That(level.Layout.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Bomber), Is.False, "no bomber on floor 3");
                List<EnemyWarder> here = own.OfType<EnemyWarder>().ToList();
                Assert.That(here.Count, Is.EqualTo(1), "the room's warder, as its description says");
                warder = here[0];
                Assert.That(warder.Body.MaxHealth, Is.EqualTo(v.WarderHealth), "the floor's health for it");
                Assert.That(warder.DamageScale, Is.EqualTo(v.DamageScaleOf(3)).Within(1e-5f), "the floor's damage scale");
                Assert.That(warder.IsElite, Is.False);
                Debug.Log("ACTPLANPLAY floor 3 seed " + level.Layout.SeedText + " room " + room.Letter + room.Order + ": " + string.Join(", ", own.Select(e => e.LogWord)));
            });
            Time.timeScale = 4f;
            yield return ForGameSeconds(15f, () => warder.Keeper.OnCount > 0);
            Time.timeScale = 1f;
            Assert.That(warder.Keeper.Raised, Is.GreaterThan(0), "it raised a ward on an ally in the room's fight");
            Assert.That(warder.Keeper.OnCount, Is.GreaterThan(0), "and the ward is on, as in the practice room");
            Assert.That(_recorder.Events.Any(e => e.Type == SessionEventTypes.WardRaised && e.EnemyId == warder.Id), Is.True, "the recorder heard it");

            // Floor 4: a room with a bomber.
            (ulong seed4, int order4) = Find(4, r => Of(r, EnemyKind.Bomber) >= 1, v);
            var bombers = new List<EnemyBomber>();
            yield return Into(seed4, 4, order4, (level, room, own) =>
            {
                bombers.AddRange(own.OfType<EnemyBomber>());
                Assert.That(bombers.Count, Is.EqualTo(Of(room, EnemyKind.Bomber)), "the room's bombers, as its description says");
                foreach (EnemyBomber b in bombers)
                {
                    Assert.That(b.Body.MaxHealth, Is.EqualTo(v.BomberHealth), "the floor's health for it");
                    Assert.That(b.DamageScale, Is.EqualTo(v.DamageScaleOf(4)).Within(1e-5f), "the floor's damage scale");
                    Assert.That(b.IsElite, Is.False);
                }
                Debug.Log("ACTPLANPLAY floor 4 seed " + level.Layout.SeedText + " room " + room.Letter + room.Order + ": " + string.Join(", ", own.Select(e => e.LogWord)));
            });
            var ids = new HashSet<int>(bombers.Select(b => b.Id));
            Time.timeScale = 4f;
            yield return ForGameSeconds(15f, () => _recorder.Events.Any(e => e.Type == SessionEventTypes.BomberBlast && e.EnemyId.HasValue && ids.Contains(e.EnemyId.Value)));
            Time.timeScale = 1f;
            Assert.That(_recorder.Events.Any(e => e.Type == SessionEventTypes.BomberBlast && e.EnemyId.HasValue && ids.Contains(e.EnemyId.Value)), Is.True,
                        "a bomber of the room ran at her and blew, as in the practice room");

            _levels.BackToPracticeRoom();
            yield return null;
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            Debug.Log("ACTPLANSUMMARY\n" + summary);
            StringAssert.IsMatch(@"(?m)^warder\s", summary, "the kinds' table has the warder's line");
            StringAssert.IsMatch(@"(?m)^bomber\s", summary, "and the bomber's");
            StringAssert.Contains("Warders, whole session", summary);
            StringAssert.Contains("Bombers, whole session", summary);
        }

        // ---------------- The ranged enemies shoot from within 11 m ----------------

        [UnityTest]
        public IEnumerator AFloor3sShooterAndCaster_ShootAtHer_OnlyFromWithin11m()
        {
            // Work order 135 (changed deliberately): on version 24's floor 3, the line-ups named off, so the room found is the one this test was
            // written on; with them on the first such room was another (AAA4H9KP's full room C14), whose caster, standing about 11 m behind
            // the shooters, did not begin a cast in the 20 game seconds. The 11 m band is the ranged kinds' rule, not the rooms'.
            _levels.Values.LineUps = false;
            LevelValues v = _levels.Values;
            (ulong seed, int order) = Find(3, r => Of(r, EnemyKind.Shooter) >= 1 && r.EnemyGroups.Any(g => g.Kind == EnemyKind.Caster && !g.Elite) && !r.Elite, v);
            var from = new Dictionary<EnemyKind, List<float>> { [EnemyKind.Shooter] = new List<float>(), [EnemyKind.Caster] = new List<float>() };
            var listeners = new List<(Enemy, Action<EnemyReport>)>();
            float woke = 0f;
            var firstShot = new Dictionary<EnemyKind, float>();
            yield return Into(seed, 3, order, (level, room, own) =>
            {
                foreach (Enemy e in own)
                {
                    if (e.Kind != EnemyKind.Shooter && e.Kind != EnemyKind.Caster) continue;
                    // Work order 098: a shooter holding its place never comes to her where she stands; the band of 11 m is asked of
                    // one that comes, as before (ShootersHoldTheirPlaceTests has the shooter in its reach shooting with the rule on).
                    if (e is EnemyShooter shooter) shooter.Tuning.HoldsPlace = false;
                    // Work order 101: the caster the same (CastersHoldTheirPlaceTests has the caster in its reach casting with the rule on).
                    if (e is EnemyCaster caster) caster.Tuning.HoldsPlace = false;
                    Enemy me = e;
                    Action<EnemyReport> heard = r =>
                    {
                        if (r.Kind != EnemyReportKind.AttackStarted) return;
                        from[me.Kind].Add(Flat(me.transform.position, _player.Position));
                        if (!firstShot.ContainsKey(me.Kind)) firstShot[me.Kind] = Time.time - woke;
                    };
                    e.Reported += heard;
                    listeners.Add((e, heard));
                }
                Debug.Log("ACTPLANPLAY ranged floor 3 seed " + level.Layout.SeedText + " room " + room.Letter + room.Order + ": "
                          + string.Join(", ", own.Select(e => e.LogWord + " " + Flat(e.transform.position, _player.Position).ToString("0.0", Inv) + " m from her start")));
            });
            woke = Time.time;
            Time.timeScale = 4f;
            yield return ForGameSeconds(20f, () => from[EnemyKind.Shooter].Count >= 2 && from[EnemyKind.Caster].Count >= 2);
            Time.timeScale = 1f;
            foreach ((Enemy e, Action<EnemyReport> heard) in listeners) e.Reported -= heard;
            foreach (KeyValuePair<EnemyKind, List<float>> k in from)
            {
                Debug.Log(string.Format(Inv, "ACTPLANPLAY {0}: {1} shots begun, from {2} m; the first {3:0.0} s after the room woke", EnemyKinds.Word(k.Key), k.Value.Count,
                                        string.Join(", ", k.Value.Select(d => d.ToString("0.0", Inv))), firstShot.TryGetValue(k.Key, out float t) ? t : -1f));
                Assert.That(k.Value.Count, Is.GreaterThan(0), "the " + EnemyKinds.Word(k.Key) + " shot at her");
                Assert.That(k.Value.Max(), Is.LessThanOrEqualTo(11f + 0.05f), "the " + EnemyKinds.Word(k.Key) + " began every shot within 11 m of her");
            }
        }
    }
}
