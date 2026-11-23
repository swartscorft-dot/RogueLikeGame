using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The door closes behind her (work order 086; Kristian's answer of 2026-11-22: "Døra stenges bak deg"; <see cref="RoomDoorRules"/>),
    /// in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/> and <see cref="PlayerCharacter"/>, on floors with
    /// passages and templates (the defaults). She walks in straight and at an angle, at two speeds and two frame steps: the doors shut
    /// at the line with her inside, never with her outside or in a doorway, and walking back she stays in. She dashes through the open
    /// door, and across the line: they shut behind her mid-dash, and a dash back stops at them. A shut door stops an enemy, her shot
    /// and the shots' line both ways, and the NavMesh has no way through it. Clearing the room opens both ways, the blocks sink out of
    /// sight, the way on is walkable, and the recorder tells how long they stood. A hit from the passage leaves them open while she is
    /// out there; one of the room's left in the passage is pulled in before they shut. The elite room, the exit room and the boss's
    /// shut theirs; a treasure room has none. A death inside opens them and ends the run; a restart's floor has none shut. The switch
    /// off is as on main (no door made, the room wakes by sight again). The room lab. The greybox view draws a shut one as a door.
    /// Nothing allocates while they stand or stand open.
    ///
    /// She cannot be hurt unless a test says so. Frames without a window are short (about 0.3 ms): the waits count game seconds or frames.
    /// </summary>
    public class RoomDoorsPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;
        /// <summary>Metres: half the block's thickness, the least she ever is from a shut door's line.</summary>
        const float HalfBlock = 0.25f;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        FeelValues _feel;
        float _runSpeedWas;
        string _folder;
        bool _hadPref;
        int _pref;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _hadPref = PlayerPrefs.HasKey(GameSettings.GreyboxKey);
            _pref = PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _feel = _player.Feel;
            _runSpeedWas = _feel.RunSpeed;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_feel != null) _feel.RunSpeed = _runSpeedWas; // the feel values live in an asset
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_player != null)
            {
                _player.Stop();
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
            }
            if (_health != null) _health.Untouchable = false;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.GreyboxKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.GreyboxKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 10f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        GeneratedLevel StartRun(Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            Assert.That(level.Layout.Passages && level.Layout.Templates, "a floor with passages and templates");
            return level;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>A ground point <paramref name="depth"/> m straight in from the middle of <paramref name="door"/> into <paramref name="room"/> (negative: out through it).</summary>
        static Vector3 InFrom(GeneratedLevel level, LevelRoom room, LevelDoor door, double depth) =>
            level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, depth, 0d));

        /// <summary>The flat way straight into the room through the door.</summary>
        static Vector3 Inward(GeneratedLevel level, LevelRoom room, LevelDoor door)
        {
            Vector3 d = InFrom(level, room, door, 1.0) - InFrom(level, room, door, 0.0);
            d.y = 0f;
            return d.normalized;
        }

        IEnumerator At(Vector3 point, LevelRoom room)
        {
            _player.Teleport(point);
            for (int i = 0; i < 30 && _levels.CurrentRoom != room; i++) yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(room), "she is in " + room.Letter + room.Order);
        }

        static (LevelRoom room, LevelRoom passage, List<Enemy> own) FirstRoom(GeneratedLevel level)
        {
            LevelLayout layout = level.Layout;
            LevelRoom passage = layout.Rooms[1], room = layout.Rooms[2];
            Assert.That((passage.Kind, room.Kind), Is.EqualTo((RoomKind.Passage, RoomKind.Combat)));
            return (room, passage, level.EnemiesByRoom[room.Order]);
        }

        /// <summary>Metres from her to the nearest of the room's doors' openings.</summary>
        float ToDoors(GeneratedLevel level, GeneratedLevels.RoomDoors d) =>
            (float)RoomDoorRules.NearestDoor(level.Layout, d.Doors, level.ToLevel(_player.Position));

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        static bool NavMeshWay(Vector3 from, Vector3 to)
        {
            if (!NavMesh.SamplePosition(from, out NavMeshHit a, 1f, NavMesh.AllAreas) || !NavMesh.SamplePosition(to, out NavMeshHit b, 1f, NavMesh.AllAreas)) return false;
            var path = new NavMeshPath();
            return NavMesh.CalculatePath(a.position, b.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }

        /// <summary>She stands past the line of the first room: the room wakes, and its doors shut. The room's doors.</summary>
        IEnumerator ShutIn(GeneratedLevel level, LevelRoom room, Action<GeneratedLevels.RoomDoors> done)
        {
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            yield return At(InFrom(level, room, door, 5.5), room);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            Assert.That(d, Is.Not.Null, "the room has its doors");
            yield return Until(() => d.Shut, 10);
            Assert.That(d.Shut, Is.True, "past the line: shut");
            done(d);
        }

        // ---------------- She walks in ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator SheWalksIn_StraightAndAtAnAngle_AtTwoSpeedsAndTwoFrameSteps_TheDoorsShutWithHerInside_NeverOutsideOrInADoorway_AndWalkingBackSheStaysIn()
        {
            var runs = new (float speed, float scale, float angle, bool must)[] { (1f, 1f, 0f, true), (1.6f, 1f, 0f, true), (1f, 4f, 0f, true), (1f, 1f, 18f, false), (1.6f, 4f, -18f, false) };
            int shutRuns = 0;
            foreach ((float speed, float scale, float angle, bool must) in runs)
            {
                string run = "speed x" + speed.ToString(Inv) + ", time x" + scale.ToString(Inv) + ", " + angle.ToString(Inv) + " degrees";
                _feel.RunSpeed = _runSpeedWas * speed;
                GeneratedLevel level = StartRun();
                yield return Frames(2);
                var (room, passage, _) = FirstRoom(level);
                LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
                GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
                Assert.That(d, Is.Not.Null);
                Assert.That(d.Doors.Count, Is.EqualTo(2), "a combat room: the way in and the way on");
                yield return At(InFrom(level, room, door, -3.0), passage);
                Vector3 way = Quaternion.Euler(0f, angle, 0f) * Inward(level, room, door);
                Time.timeScale = scale;
                _player.Steer(way);
                float shutAt = -1f;
                float until = Time.time + 8f;
                while (Time.time < until && (shutAt < 0f || Time.time < shutAt + 0.6f))
                {
                    yield return null;
                    if (!d.Shut) continue;
                    if (shutAt < 0f)
                    {
                        shutAt = Time.time;
                        Assert.That(RoomDoorRules.IsInside(level.Layout, room, d.Doors, level.ToLevel(_player.Position), _levels.Values.Fights.DoorClear - 0.05),
                                    "the frame they shut she is inside (" + ToDoors(level, d).ToString("0.00", Inv) + " m from a door): " + run);
                    }
                    Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "never shut out: " + run);
                    Assert.That(ToDoors(level, d), Is.GreaterThan(HalfBlock), "never in a doorway: " + run);
                }
                if (must) Assert.That(shutAt, Is.GreaterThan(0f), "they shut as she walked in: " + run);
                else Assume.That(shutAt, Is.GreaterThan(0f), "she walked far enough in to wake it: " + run);
                shutRuns++;
                // She turns and walks back to the way in: she stays in the room, against the shut door.
                _player.Steer(-Inward(level, room, door));
                until = Time.time + 2.5f;
                float nearest = float.PositiveInfinity;
                while (Time.time < until)
                {
                    yield return null;
                    Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "walking back she stays in: " + run);
                    nearest = Mathf.Min(nearest, ToDoors(level, d));
                    Assert.That(nearest, Is.GreaterThan(HalfBlock), "and not in the doorway: " + run);
                }
                Assert.That(d.Shut, Is.True);
                Assert.That(NavMeshWay(_player.Position, InFrom(level, room, door, -3.0)), Is.False, "no way out on the NavMesh: " + run);
                TestContext.WriteLine("ROOMDOORS walk " + run + ": shut " + (shutAt > 0f ? "yes" : "no") + ", nearest a door walking back " + nearest.ToString("0.00", Inv) + " m");
                _player.Stop();
                Time.timeScale = 1f;
            }
            Assert.That(shutRuns, Is.GreaterThanOrEqualTo(3));
        }

        [UnityTest]
        public IEnumerator SheDashesThroughTheOpenDoor_AndAcrossTheLine_TheyShutBehindHerMidDash_AndADashBackStopsAtThem()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            var (room, passage, _) = FirstRoom(level);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            double point = RoomFightRules.PointDepth(level.Layout, room, door, _levels.Values.Fights);

            // Through the open door: from 2 m in the passage, one dash (6 m) to 4 m in, short of the line.
            yield return At(InFrom(level, room, door, -2.0), passage);
            _player.StartFresh();
            Assert.That(_player.DashToward(InFrom(level, room, door, 12.0)), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 120);
            yield return Frames(2);
            Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "the dash took her through the open doorway");
            Assert.That(d.Shut, Is.False, "short of the line: open");

            // Across the line: from 1.6 m in, a dash 6 m in: the room wakes mid-dash and they shut with her well in.
            yield return At(InFrom(level, room, door, 1.6), room);
            yield return Frames(2);
            Assume.That(RoomFightRules.PastThePoint(level.Layout, room, door, level.ToLevel(_player.Position), _levels.Values.Fights), Is.False);
            _player.StartFresh();
            Assert.That(_player.DashToward(InFrom(level, room, door, 12.0)), Is.EqualTo(IntentResult.Started));
            bool shutWhileDashing = false;
            for (int i = 0; i < 200 && _player.IsDashing; i++)
            {
                yield return null;
                if (d.Shut)
                {
                    if (_player.IsDashing) shutWhileDashing = true;
                    Assert.That(ToDoors(level, d), Is.GreaterThanOrEqualTo(_levels.Values.Fights.DoorClear - 0.05f), "she is clear of the doors when they shut");
                }
            }
            yield return Until(() => d.Shut, 10);
            Assert.That(d.Shut, Is.True, "past the line: shut");
            TestContext.WriteLine("ROOMDOORS dash across the line: shut while she dashed " + shutWhileDashing + "; the line at " + point.ToString("0.0", Inv) + " m");

            // A dash back at the way in: she stops at the shut door, in the room.
            yield return Frames(3);
            _player.StartFresh();
            Assert.That(_player.DashToward(InFrom(level, room, door, -8.0)), Is.EqualTo(IntentResult.Started));
            for (int i = 0; i < 200 && _player.IsDashing; i++)
            {
                yield return null;
                Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "the dash back stops at the door");
                Assert.That(ToDoors(level, d), Is.GreaterThan(HalfBlock));
            }
            yield return Frames(2);
            Assert.That(level.RoomAt(_player.Position), Is.SameAs(room));
            Assert.That(d.Shut, Is.True);
        }

        // ---------------- A shut door stops ----------------

        [UnityTest]
        public IEnumerator AShutDoor_StopsAnEnemy_HerShot_AndTheShotsLineBothWays_AndTheNavMeshHasNoWayThroughIt()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            yield return Frames(3); // the NavMesh carved

            Vector3 inside = InFrom(level, room, door, 3.0), outside = InFrom(level, room, door, -3.0);
            Assert.That(NavMeshWay(inside, outside), Is.False, "no way through the shut door on the NavMesh");
            Assert.That(NavMesh.Raycast(inside, outside, out NavMeshHit _, NavMesh.AllAreas), Is.True, "a straight walk meets it");
            // The shots' line (every shot, hers and theirs, asks cover so): stopped at the door, both ways.
            Vector3 dir = (outside - inside).normalized;
            float across = Flat(inside, outside);
            Assert.That(Cover.ClearDistance(new Vector3(inside.x, 0f, inside.z), dir, across, Cover.RayHeight), Is.LessThan(3.05f).And.GreaterThan(2.5f), "from inside: stopped at the door");
            Assert.That(Cover.ClearDistance(new Vector3(outside.x, 0f, outside.z), -dir, across, Cover.RayHeight), Is.LessThan(3.05f).And.GreaterThan(2.5f), "from the passage: stopped at the door");

            // One of the room's, put in the passage, comes at her and cannot get in.
            Enemy e = own.FirstOrDefault(x => x != null && !x.IsDead && x is MeleeEnemy);
            Assume.That(e, Is.Not.Null, "a melee enemy in the room");
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(InFrom(level, room, door, -3.0)), Is.True);
            _player.Teleport(InFrom(level, room, door, 3.0));
            yield return Frames(2);
            Assert.That(d.Shut, Is.True, "she was moved within the room: still shut");
            Time.timeScale = 3f;
            float until = Time.time + 4f;
            while (Time.time < until)
            {
                yield return null;
                Assert.That(level.RoomAt(e.transform.position), Is.Not.SameAs(room), "the enemy in the passage never gets in");
            }
            Time.timeScale = 1f;
            Assert.That(e.IsAwake, Is.True, "it was after her all along");
            // Her shot at it, through the door: stopped at the door, it takes nothing.
            int health = e.Body.CurrentHealth;
            int stoppedBefore = _player.ShotsStoppedByCover;
            int firedBefore = _player.BasicShotsFired;
            _player.Shoot(e.transform.position);
            yield return Until(() => _player.BasicShotsFired > firedBefore, 120);
            yield return Frames(2);
            Assert.That(_player.BasicShotsFired, Is.GreaterThan(firedBefore), "she shot");
            Assert.That(_player.ShotsStoppedByCover, Is.GreaterThan(stoppedBefore), "the door stopped it");
            Assert.That(e.Body.CurrentHealth, Is.EqualTo(health), "it took nothing");
        }

        // ---------------- They open ----------------

        [UnityTest]
        public IEnumerator ClearingTheRoom_OpensBothWays_TheBlocksSinkOutOfSight_TheWayOnIsWalkable_AndTheRecorderTellsHowLongTheyStood()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeRoomDoorsTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            Assert.That(d.Blocks.Count, Is.EqualTo(d.Doors.Count), "a block a door: the way in and the way on");
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.Shut && b.Box.enabled && b.CoverPart.enabled && b.Obstacle.enabled, "shut: its collider, its cover and its obstacle on");
                Assert.That(b.Obstacle.carving && !b.Obstacle.carveOnlyStationary, "it carves the NavMesh at once");
                Assert.That(b.GetComponent<Shelter>(), Is.Null, "no duck or cover at a door");
                Assert.That(b.gameObject.layer, Is.EqualTo(level.WallLayer));
                Assert.That(b.transform.IsChildOf(level.transform), "it goes with the floor");
            }
            yield return GameTime(_levels.Values.Fights.DoorRiseSeconds + 0.1f);
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.Raised, Is.EqualTo(1f), "risen");
                Assert.That(b.BlockRenderer.enabled, Is.True, "seen");
                Assert.That(b.Block.position.y, Is.EqualTo(b.transform.position.y).Within(1e-3f), "standing in the doorway");
            }
            yield return GameTime(1f);
            Kill(own);
            yield return Until(() => !d.Shut, 10);
            Assert.That(d.Shut, Is.False, "cleared: open");
            foreach (RoomDoor b in d.Blocks) Assert.That(!b.Shut && !b.Box.enabled && !b.CoverPart.enabled && !b.Obstacle.enabled, "all of it off at once");
            yield return GameTime(_levels.Values.Fights.DoorSinkSeconds + 0.1f);
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.Raised, Is.EqualTo(0f), "sunk");
                Assert.That(b.BlockRenderer.enabled, Is.False, "and not drawn");
            }
            yield return Frames(3);
            LevelDoor wayIn = LevelGeometry.EntryDoor(level.Layout, room), wayOn = LevelGeometry.ExitDoor(level.Layout, room);
            Assert.That(wayOn, Is.Not.Null);
            Assert.That(NavMeshWay(_player.Position, InFrom(level, room, wayIn, -2.0)), Is.True, "the way back is walkable");
            Assert.That(NavMeshWay(_player.Position, InFrom(level, room, wayOn, -2.0)), Is.True, "the way on is walkable");

            List<SessionEvent> doors = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomDoor).ToList();
            Assert.That(doors.Select(e => e.Name), Is.EqualTo(new[] { "closed", "opened" }));
            Assert.That(doors[0].ExtraValue("floor"), Is.EqualTo("1"));
            Assert.That(doors[0].ExtraValue("room"), Is.EqualTo((LevelGeometry.RoomNumber(level.Layout, room) + 1).ToString(Inv)));
            Assert.That(doors[1].ExtraValue("why"), Is.EqualTo("cleared"));
            float stood = float.Parse(doors[1].ExtraValue("seconds"), Inv);
            Assert.That(stood, Is.GreaterThan(1f).And.LessThan(30f), "how long they stood");
            _recorder.StopRecording();
            StringAssert.Contains("doors closed 1 (closing to opening mean", File.ReadAllText(_recorder.SummaryPath));
            _recorder = null;
        }

        // ---------------- The wake cases ----------------

        [UnityTest]
        public IEnumerator AHitFromThePassage_LeavesThemOpenWhileSheIsOutThere_AndOneLeftInThePassage_IsPulledInBeforeTheyShut()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            (LevelRoom room, LevelRoom passage, List<Enemy> own) = FirstRoom(level);
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, room);
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            // In the passage, a hit wakes the room: they stay open while she is out there.
            yield return At(InFrom(level, room, door, -3.0), passage);
            Enemy hit = own.OrderBy(e => Flat(e.transform.position, _player.Position)).First();
            hit.Body.TakeHit(1);
            yield return Frames(3);
            Assert.That(_levels.FightOf(room).On, Is.True, "the room woke");
            Time.timeScale = 2f;
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                yield return null;
                if (level.RoomAt(_player.Position) != room) Assert.That(d.Shut, Is.False, "she is outside the room: never shut out");
            }
            Time.timeScale = 1f;

            // One of them dazed in the passage; she steps in 2 m: they wait for it, then pull it in, and shut.
            Enemy left = own.First(e => e != null && !e.IsDead && e != hit);
            Assert.That(left.GetComponent<NavMeshAgent>().Warp(InFrom(level, room, door, -3.5)), Is.True);
            left.Body.DazeFor(8f);
            yield return At(InFrom(level, room, door, 2.2), room);
            float inAt = Time.time;
            yield return Frames(3);
            Assert.That(d.Shut, Is.False, "one of them in the passage: they wait");
            Assert.That(level.RoomAt(left.transform.position), Is.Not.SameAs(room));
            yield return Until(() => d.Shut, 2000);
            Assert.That(d.Shut, Is.True, "shut after the wait");
            Assert.That(Time.time - inAt, Is.GreaterThanOrEqualTo(_levels.Values.Fights.DoorPullAfter - 0.05f).And.LessThan(_levels.Values.Fights.DoorPullAfter + 1.5f),
                        "after the wait of " + _levels.Values.Fights.DoorPullAfter.ToString(Inv) + " s");
            Assert.That(d.PulledIn, Is.GreaterThanOrEqualTo(1), "it was pulled in");
            Assert.That(RoomDoorRules.IsInside(level.Layout, room, d.Doors, level.ToLevel(left.transform.position), _levels.Values.Fights.DoorClear), Is.True,
                        "it stands inside the room, clear of the doors");
            foreach (Enemy e in own.Where(x => x != null && !x.IsDead))
                Assert.That(level.RoomAt(e.transform.position), Is.SameAs(room), "no enemy of the room left on the other side: " + e.LogWord);
        }

        [UnityTest]
        public IEnumerator TheEliteRoom_TheExitRoom_AndTheBossRoom_ShutTheirDoors_ATreasureRoomHasNone()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            foreach (LevelRoom treasure in level.Layout.Rooms.Where(r => r.Kind == RoomKind.Treasure))
                Assert.That(_levels.DoorsOf(treasure), Is.Null, "a treasure room holds no fight, and has no door");
            foreach (LevelRoom room in new[] { level.Layout.EliteRoom, level.Layout.ExitRoom })
            {
                Assert.That(room, Is.Not.Null);
                GeneratedLevels.RoomDoors d = null;
                yield return ShutIn(level, room, x => d = x);
                Assert.That(d.Blocks.Count, Is.EqualTo(d.Doors.Count));
                if (room == level.Layout.ExitRoom) Assert.That(d.Doors.Count, Is.EqualTo(1), "the exit room: its way in");
                Kill(level.EnemiesByRoom[room.Order]);
                yield return Until(() => !d.Shut, 10);
                Assert.That(d.Shut, Is.False, room.Letter + " cleared: open");
                yield return Frames(3);
            }

            _levels.Values = new LevelValues();
            GeneratedLevel five = _levels.Build(RunSeeds.FloorSeed(RunSeed, 5), 5);
            yield return Frames(2);
            Assert.That(five.Boss, Is.Not.Null);
            GeneratedLevels.RoomDoors boss = null;
            yield return ShutIn(five, five.Layout.ExitRoom, x => boss = x);
            Assert.That(five.Boss.HeldByFight, Is.True, "the boss is in its room's fight, shut in with her");
        }

        [UnityTest]
        public IEnumerator ADeathInside_OpensThem_AndEndsTheRun_AndARestartsFloorHasNoneShut()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RoguelikeRoomDoorsTests_" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelRoom room = FirstRoom(level).room;
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            _health.Untouchable = false;
            _health.TakeHit(_health.Max * 10);
            yield return Until(() => !d.Shut, 10);
            Assert.That(d.Shut, Is.False, "her death opens them");
            yield return Frames(3);
            Assert.That(_levels.InRun, Is.False, "the run ended");
            Assert.That(_recorder.Events.Last(e => e.Type == SessionEventTypes.RoomDoor).ExtraValue("why"), Is.EqualTo("her death"));
            _health.RestoreFull();
            _health.Untouchable = true;
            GeneratedLevel again = StartRun();
            yield return Frames(3);
            Assert.That(again, Is.Not.SameAs(level));
            Assert.That(_levels.RoomDoorList.Any(x => x.Shut), Is.False, "the new floor: none shut");
            Assert.That(Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, "and the old floor's blocks are gone with it");
        }

        // ---------------- The switch off ----------------

        /// <summary>A point of the room short of its line, 1.6 m and more from its doors, within some enemy's range; null with none.</summary>
        static (Vector3 at, Enemy near)? ShortOfTheLineWithinRange(GeneratedLevel level, LevelRoom room, LevelValues v)
        {
            LevelLayout l = level.Layout;
            LevelDoor door = LevelGeometry.EntryDoor(l, room);
            var doors = new List<LevelDoor>();
            RoomDoorRules.DoorsOf(l, room, doors);
            double point = RoomFightRules.PointDepth(l, room, door, v.Fights);
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            for (double depth = 1.8; depth < point - 0.6; depth += 0.4)
                for (double along = -12; along <= 12; along += 1)
                {
                    LevelPoint p = RoomDoorRules.PullInPlace(l, room, door, depth, along);
                    if (l.RoomAt(p.X, p.Y) != room || RoomDoorRules.NearestDoor(l, doors, p) < 1.6) continue;
                    Vector3 w = level.ToWorld(p);
                    if (!NavMesh.SamplePosition(w, out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) continue;
                    Enemy near = own.FirstOrDefault(e => Flat(e.transform.position, w) <= e.Values.AggroRange - 0.5f);
                    if (near != null) return (hit.position, near);
                }
            return null;
        }

        [UnityTest]
        public IEnumerator WithTheDoorsOn_TheRoomSleepsWithHerWithinAnEnemysRange_ShortOfTheLine_AndWithThemOff_ItWakesBySight_AndNoDoorIsMade()
        {
            foreach (bool on in new[] { true, false })
            {
                GeneratedLevel level = StartRun(v => v.Fights.Doors = on);
                yield return Frames(2);
                LevelRoom found = null;
                (Vector3 at, Enemy near)? stand = null;
                foreach (LevelRoom r in level.Layout.Rooms.Where(r => _levels.FightOf(r) != null))
                {
                    stand = ShortOfTheLineWithinRange(level, r, _levels.Values);
                    if (stand == null) continue;
                    found = r;
                    break;
                }
                Assume.That(found, Is.Not.Null, "a room with a stand short of its line within one of its enemies' range");
                RoomFight fight = _levels.FightOf(found);
                Assert.That(fight.WakesOnlyByLineOrHurt, Is.EqualTo(on));
                yield return At(stand.Value.at, found);
                yield return Frames(20);
                if (on)
                {
                    Assert.That(fight.On, Is.False, "the doors on: within " + stand.Value.near.LogWord + "'s range, short of the line, the room sleeps");
                    Assert.That(level.EnemiesByRoom[found.Order].Any(e => e.IsAwake), Is.False);
                    LevelDoor door = LevelGeometry.EntryDoor(level.Layout, found);
                    yield return At(InFrom(level, found, door, 5.5), found);
                    yield return Until(() => _levels.DoorsOf(found).Shut, 10);
                    Assert.That(fight.StartedBy, Is.EqualTo(RoomFightStart.Point), "it woke at its line");
                    Assert.That(_levels.DoorsOf(found).Shut, Is.True);
                }
                else
                {
                    Assert.That(fight.On && fight.StartedBy == RoomFightStart.Seen, "the doors off: it saw her by its range, as on main");
                    Assert.That(_levels.RoomDoorList, Is.Empty, "no doors' state");
                    yield return At(InFrom(level, found, LevelGeometry.EntryDoor(level.Layout, found), 6.0), found);
                    yield return Frames(10);
                    Assert.That(Object.FindObjectsByType<RoomDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None), Is.Empty, "no door is ever made");
                    Assert.That(NavMeshWay(_player.Position, InFrom(level, found, LevelGeometry.EntryDoor(level.Layout, found), -3.0)), Is.True, "the way back stands open");
                }
                _levels.BackToPracticeRoom();
                yield return Frames(3);
            }
        }

        // ---------------- The room lab and the greybox ----------------

        [UnityTest]
        public IEnumerator TheRoomLab_ShutsItsPassageBehindHer_AndOpensItWhenCleared()
        {
            LabEntry entry = RoomLab.Entries(_levels.Values).First(e => e.Kind == LabKind.Ordinary && e.Template != null);
            GeneratedLevel level = _levels.StartLab(entry, 0, 0x0860B0B0UL);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelRoom room = level.Layout.Rooms[1];
            LevelDoor door = level.Layout.Doors[0];
            GeneratedLevels.RoomDoors d = _levels.DoorsOf(room);
            Assert.That(d, Is.Not.Null, "the lab's room has its doors");
            Assert.That(d.Doors, Does.Contain(door), "the lab's passage is the way in");
            Assert.That(d.Shut, Is.False, "open while she is in the passage");
            _player.Teleport(level.ToWorld(RoomDoorRules.PullInPlace(level.Layout, room, door, 6.0, 0d)));
            yield return Until(() => d.Shut, 20);
            Assert.That(d.Shut, Is.True, "past the line: shut, as in a floor");
            Kill(level.EnemiesByRoom[room.Order]);
            yield return Until(() => !d.Shut, 10);
            Assert.That(d.Shut, Is.False, "cleared: open");
            yield return Until(() => _levels.LabCleared, 60);
            Assert.That(_levels.LabCleared, Is.True);
            _levels.BackToPracticeRoom();
            yield return Frames(2);
        }

        [UnityTest]
        public IEnumerator InTheGreyboxView_AShutDoorIsDrawn_AsTheDoorItIs_InTheDoorsColour()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            LevelRoom room = FirstRoom(level).room;
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(2);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, "the greybox view is on");
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            yield return GameTime(_levels.Values.Fights.DoorRiseSeconds + 0.1f);
            Color door = CryptKit.Picked(new GreyboxValues().Door);
            foreach (RoomDoor b in d.Blocks)
            {
                Assert.That(b.BlockRenderer.enabled && !b.BlockRenderer.forceRenderingOff, "drawn in the greybox view");
                Assert.That(b.BlockRenderer.sharedMaterial.color, Is.EqualTo(door), "in the doors' orange");
            }
            // Turned off and on again with the door shut: drawn as it is all along.
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(2);
            GameSettings.SetGreybox(true, keep: false);
            yield return Frames(2);
            foreach (RoomDoor b in d.Blocks) Assert.That(b.BlockRenderer.enabled && !b.BlockRenderer.forceRenderingOff, "still drawn");
        }

        // ---------------- Their frame ----------------

        [UnityTest]
        public IEnumerator NothingAllocates_WhileTheDoorsStand_OrStandOpen()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            var (room, passage, _) = FirstRoom(level);
            // Open, she in the passage, the floor's every room's doors looked at.
            yield return At(InFrom(level, room, LevelGeometry.EntryDoor(level.Layout, room), -3.0), passage);
            for (int i = 0; i < 10; i++) _levels.TickRoomDoorsForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomDoorsForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "open: nothing");
            GeneratedLevels.RoomDoors d = null;
            yield return ShutIn(level, room, x => d = x);
            yield return GameTime(_levels.Values.Fights.DoorRiseSeconds + 0.1f);
            for (int i = 0; i < 10; i++) _levels.TickRoomDoorsForTests();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.TickRoomDoorsForTests(); }, AllocExt.AllocatingGCMemory(Is.Not), "shut, standing: nothing");
            Assert.That(d.Shut, Is.True);
        }
    }
}
