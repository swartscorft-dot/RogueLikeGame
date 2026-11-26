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
    /// Exit rooms of their own in a run (work order 103; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "tronrommet bare
    /// i etasje 5 og egne utgangsrom i etasje 1–4"), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>, with
    /// the defaults: a run whose floors 1 to 4 end in each of the three (the gate hall, the stair head, the sealed door), never one
    /// twice running; on each, the exit room's fight sleeps until she is past its line, then wakes, its one door shuts behind her,
    /// she can walk on the NavMesh from where she stands to the way down, and cleared the door opens, the floor's reward lands, and
    /// taking it takes her on to the next floor; floor 5 ends in the throne room with the act boss. Writes what it found ("EXITRUNPLAY").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames, never a frame's length.
    /// </summary>
    public class ExitRoomsInARunPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Names = { "gate-hall", "stair-head", "sealed-door" };

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;

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
            _health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_health != null) _health.Untouchable = false;
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        static void Kill(IEnumerable<Enemy> enemies)
        {
            foreach (Enemy e in enemies.ToList())
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        /// <summary>A whole NavMesh path from <paramref name="from"/> to the way down's place (its A cells' middle) of the room; its length, or -1.</summary>
        static float PathToTheWayDown(GeneratedLevel level, LevelRoom room, Vector3 from)
        {
            Assert.That(ExitRooms.WayDownLine(level.Layout, room, out _, out LevelPoint wayDown), Is.True, "the way down's line");
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 1.5f, NavMesh.AllAreas)) return -1;
            if (!NavMesh.SamplePosition(level.ToWorld(wayDown), out NavMeshHit end, 1.9f, NavMesh.AllAreas)) return -1;
            var path = new NavMeshPath();
            if (!NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) || path.status != NavMeshPathStatus.PathComplete) return -1;
            float length = 0;
            for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
            return length;
        }

        /// <summary>A run seed whose floors 1 to 4, each told the floor before's as the game tells it, end in all three of the rooms.</summary>
        static ulong RunShowingAllThree(LevelValues v)
        {
            for (ulong r = 1; r < 400; r++)
            {
                var shown = new List<string>();
                string before = null;
                for (int floor = 1; floor <= 4; floor++)
                {
                    before = WayDowns.Shown(LevelGenerator.Generate(RunSeeds.FloorSeed(r, floor), floor, v, exitBefore: before));
                    shown.Add(before);
                }
                if (shown.All(n => n != null) && shown.Distinct().Count() == 3) return r;
            }
            return 0;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ARun_FloorsOneToFourEndInEachOfTheThree_TheFightWakes_TheDoorShutsAndOpens_TheWayDownTakesHerOn_FloorFiveIsTheThrone()
        {
            var v = new LevelValues();
            Assert.That(v.ExitRoomsOfTheirOwn, Is.True, "the defaults (work order 103)");
            ulong runSeed = RunShowingAllThree(v);
            Assert.That(runSeed, Is.Not.Zero, "a run that shows all three");
            _levels.Values = v;
            GeneratedLevel level = _levels.StartRun(runSeed);
            Assert.That(level, Is.Not.Null);
            var lines = new List<string>();
            var seen = new HashSet<string>();
            string before = null;
            for (int floor = 1; floor <= 4; floor++)
            {
                yield return Frames(2);
                level = _levels.Level;
                Assert.That(_levels.Floor, Is.EqualTo(floor));
                LevelLayout layout = level.Layout;
                LevelRoom exit = layout.ExitRoom;
                string name = WayDowns.Shown(layout), where = "floor " + floor + " (" + layout.SeedText + ", " + name + ")";
                Assert.That(Names, Does.Contain(name), where + ": a room of its own");
                Assert.That(name, Is.Not.EqualTo(before), where + ": not the floor before's");
                Assert.That(_levels.WayDownBeforeThisFloor, Is.EqualTo(before), where + ": told the floor before's");
                Assert.That(exit.Theme, Is.EqualTo(RoomTheme.ExitHall), where);
                Assert.That(level.Boss, Is.Null, where + ": no act boss before floor 5");
                seen.Add(name);

                // Its fight sleeps until she is past its line; then it wakes and its one door shuts behind her.
                RoomFight fight = _levels.FightOf(exit);
                GeneratedLevels.RoomDoors doors = _levels.DoorsOf(exit);
                Assert.That(fight, Is.Not.Null, where + ": it holds its fight");
                Assert.That(doors, Is.Not.Null, where + ": it has its door");
                Assert.That(doors.Doors.Count, Is.EqualTo(1), where + ": one door, the way in");
                Assert.That(fight.On || doors.Shut, Is.False, where + ": asleep and open while she is away");
                LevelDoor door = LevelGeometry.EntryDoor(layout, exit);
                Vector3 inside = level.ToWorld(RoomDoorRules.PullInPlace(layout, exit, door, _levels.Values.Fights.PointIn + 1.5, 0d));
                _player.Teleport(inside);
                yield return Until(() => _levels.CurrentRoom == exit, 30);
                Assert.That(_levels.CurrentRoom, Is.SameAs(exit), where + ": she is in it");
                yield return Until(() => fight.On && doors.Shut, 30);
                Assert.That(fight.On, Is.True, where + ": past the line, the room wakes");
                Assert.That(doors.Shut, Is.True, where + ": its door shuts behind her");
                int enemies = level.EnemiesByRoom[exit.Order].Count;
                Assert.That(enemies, Is.EqualTo(exit.EnemyCount), where + ": every enemy stands");
                float walk = PathToTheWayDown(level, exit, _player.Position);
                Assert.That(walk, Is.GreaterThan(0f), where + ": a whole way on the NavMesh from her to the way down");

                // Cleared, the door opens, the floor is finished and its reward lands; taken, it takes her on.
                Kill(level.EnemiesByRoom[exit.Order]);
                yield return Until(() => !doors.Shut && _levels.Cleared, 120);
                Assert.That(doors.Shut, Is.False, where + ": cleared, the door opens");
                Assert.That(_levels.Cleared, Is.True, where + ": the floor is finished");
                yield return Until(() => _levels.FloorDrop != null, 120);
                RewardDrop drop = _levels.FloorDrop;
                Assert.That(drop, Is.Not.Null, where + ": the floor's reward lands");
                float until = Time.realtimeSinceStartup + 10f; // it flies before it lies still: real seconds, not frames
                while (!drop.Settled && Time.realtimeSinceStartup < until) yield return null;
                _player.Teleport(drop.Landing);
                yield return Frames(3);
                Assert.That(_levels.PickUpDrop(), Is.True, where + ": picked up");
                yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 60);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), where + ": the floor's reward comes");
                yield return new WaitForSecondsRealtime(0.5f); // past the choice's guard
                Assert.That(_levels.PickReward(0), Is.True, where);
                if (floor == 1 && !_levels.RunValues.MixedFloorReward) // changed deliberately (work order 138): no focus screen with the floor reward of three kinds
                {
                    yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice, 60);
                    yield return new WaitForSecondsRealtime(0.5f);
                    Assert.That(_levels.PickFocusChoice(0), Is.True, where + ": the focus choice after floor 1");
                }
                yield return Until(() => _levels.Floor == floor + 1 && _levels.Choice == GeneratedLevels.ChoiceKind.None, 120);
                Assert.That(_levels.Floor, Is.EqualTo(floor + 1), where + ": the way down took her on");
                lines.Add(string.Format(Inv, "floor {0} {1}: {2} enemies, walk to the way down {3:0.0} m", floor, name, enemies, walk));
                before = name;
            }
            Assert.That(seen, Is.EquivalentTo(Names), "each of the three met");

            yield return Frames(2);
            level = _levels.Level;
            Assert.That(ExitRooms.IsThrone(level.Layout.ExitRoom.Template?.Template), Is.True, "floor 5 ends in the throne room");
            Assert.That(level.Layout.ExitRoom.Theme, Is.EqualTo(RoomTheme.ThroneRoom));
            Assert.That(level.Boss, Is.Not.Null, "the act boss on floor 5");
            Assert.That(_levels.WayDownBeforeThisFloor, Is.EqualTo(before), "floor 5 told floor 4's, which changes nothing in a throne room");
            string line = "EXITRUNPLAY run " + LevelSeed.ToText(runSeed) + ": " + string.Join("; ", lines) + "; floor 5 " + level.Layout.ExitRoom.Template;
            Debug.Log(line);
            TestContext.WriteLine(line);
        }
    }
}
