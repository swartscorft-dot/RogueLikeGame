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
    /// Exit rooms of their own (work order 089; Kristian, 2026-11-22: "Ja, tronen bare på etasje 5"), in the ControlRoom scene through
    /// the intents of <see cref="GeneratedLevels"/>: the room lab builds each of the third set with the exit presets of floors 1 to 4
    /// (floor 1's, floor 2's, floor 3's with its warder, floor 4's with its warder and bombers) without a fault, she starts in its
    /// passage and can walk on the NavMesh from there to the way down; the greybox draws it (the fight's line, the door, every prop
    /// in its table's colour); its fight wakes at the line, and cleared, the exit works: the floor's reward comes. And in a run with
    /// the switch on, floor 2 ends in one, and clearing it finishes the floor. Writes what it found ("EXITLAB", "EXITRUN").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames, never a frame's length.
    /// </summary>
    public class ExitRoomsPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly int[] ExitPresets = { 0, 4, 5, 6 }; // RoomLab.Mixes: floor 1, floor 2, floor 3 with a warder, floor 4 with a warder and bombers

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
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
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.GetComponent<PlayerInputDriver>().InputSource = null;
            if (_health != null) _health.Untouchable = false;
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
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

        /// <summary>A ground point of the lab's room on its open floor at <paramref name="depth"/> m in from its door in (south), nearest the door's line.</summary>
        static Vector3 AtDepth(GeneratedLevel level, double depth)
        {
            LevelLayout l = level.Layout;
            LevelRoom room = l.Rooms[1];
            LevelDoor door = l.Doors[0];
            double cs = l.CellSize, x = (door.From + 0.5) * cs, y = room.Rect.Y * cs + depth;
            for (int k = 0; k < room.Rect.Width && l.RoomAt(x, y) != room; k++)
                x = ((k % 2 == 0 ? door.From + k / 2 + 1 : door.From - k / 2 - 1) + 0.5) * cs;
            Assert.That(l.RoomAt(x, y), Is.SameAs(room), "an open point at that depth");
            return level.ToWorld(new LevelPoint(x, y));
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

        [UnityTest, Timeout(600000)]
        public IEnumerator TheLab_BuildsEachWithTheExitPresetsOfFloorsOneToFour_SheWalksToTheWayDown_AndTheGreyboxDrawsIt()
        {
            List<LabEntry> entries = RoomLab.EntriesOf(LabKind.Exit, _levels.Values).Where(e => ExitRooms.IsWayDown(e.Template)).ToList();
            Assert.That(entries.Select(e => e.Name), Is.EqualTo(new[] { "gate-hall", "stair-head", "sealed-door" }), "listed under their names");
            var lines = new List<string>();
            foreach (LabEntry entry in entries)
                foreach (int mix in ExitPresets)
                {
                    GeneratedLevel level = _levels.StartLab(entry, mix, 0x0890890UL + (ulong)mix);
                    Assert.That(level, Is.Not.Null, entry + " was built");
                    yield return Frames(2);
                    string where = entry.Name + " " + RoomLab.Mixes[mix];
                    LevelRoom room = level.Layout.Rooms[1];
                    Assert.That(RoomLab.Faults(level.Layout, RoomLab.Mixes[mix], _levels.Values), Is.Empty, where);
                    Assert.That(room.Template.Template, Is.SameAs(entry.Template), where);
                    Assert.That(room.Theme, Is.EqualTo(RoomTheme.ExitHall), where + ": an exit hall");
                    Assert.That(_levels.CurrentRoom, Is.SameAs(level.Layout.Rooms[0]), where + ": she starts in the passage");
                    Assert.That(level.Boss, Is.Null, where + ": no act boss before floor 5");
                    int enemies = level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count : 0;
                    Assert.That(enemies, Is.EqualTo(room.EnemyCount), where + ": every enemy stands");
                    float walk = PathToTheWayDown(level, room, level.PlayerStart);
                    Assert.That(walk, Is.GreaterThan(0f), where + ": a whole way on the NavMesh from her start to the way down");

                    // The greybox draws it: the fight's line, the door, every prop's box in its table's colour.
                    GameSettings.SetGreybox(true, keep: false);
                    yield return Frames(2);
                    Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True, where + ": the greybox on");
                    Assert.That(_levels.Greybox.FightLinePieces, Is.GreaterThan(0), where + ": the fight's line drawn");
                    Assert.That(_levels.Greybox.DoorPieces, Is.GreaterThan(0), where + ": the door drawn");
                    int high = _levels.Greybox.PropParts.Count(p => p == GreyboxPart.HighCover), low = _levels.Greybox.PropParts.Count(p => p == GreyboxPart.LowCover);
                    Assert.That(high, Is.GreaterThanOrEqualTo(room.Props.Count(FightCover.IsTall)), where + ": its tall props as high cover");
                    Assert.That(low, Is.GreaterThanOrEqualTo(room.Props.Count(FightCover.IsLow)), where + ": its low props as low cover");
                    StringAssert.Contains("GREYBOX", _levels.StatusText(), where + ": the legend");
                    StringAssert.Contains("ROOM LAB  exit: " + entry.Name, _levels.StatusText(), where + ": the lab's box names it");
                    GameSettings.SetGreybox(false, keep: false);
                    yield return Frames(1);
                    lines.Add(string.Format(Inv, "{0}: {1} enemies, walk to the way down {2:0.0} m, greybox {3} high {4} low", where, enemies, walk, high, low));
                }
            string line = "EXITLAB " + string.Join("; ", lines);
            Debug.Log(line);
            TestContext.WriteLine(line);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator InTheLab_EachWakesAtTheLine_AndCleared_TheExitWorks_TheFloorsRewardComes()
        {
            foreach (LabEntry entry in RoomLab.EntriesOf(LabKind.Exit, _levels.Values).Where(e => ExitRooms.IsWayDown(e.Template)).ToList())
            {
                GeneratedLevel level = _levels.StartLab(entry, 5, 0x089C1EAUL); // floor 3 with a warder
                Assert.That(level, Is.Not.Null);
                yield return Frames(2);
                LevelRoom room = level.Layout.Rooms[1];
                RoomFight fight = _levels.FightOf(room);
                Assert.That(fight, Is.Not.Null, entry + ": it holds its fight");
                Assert.That(fight.On, Is.False, entry + ": asleep while she is in the passage");
                double point = RoomFightRules.PointDepth(level.Layout, room, level.Layout.Doors[0], _levels.Values.Fights);
                Assert.That(point, Is.EqualTo(_levels.Values.Fights.PointIn).Within(1e-4), entry + ": the line 5 m in");
                _player.Teleport(AtDepth(level, point + 1.0));
                yield return Until(() => fight.On, 30);
                Assert.That(fight.On, Is.True, entry + ": past the line, the room wakes");
                Assert.That(level.EnemiesByRoom[room.Order].All(e => e.Fight == fight), Is.True, entry + ": every enemy in its fight");
                Kill(level.EnemiesByRoom[room.Order]);
                // Killed, the floor's reward lands on the ground (work order 042); picked up, its choice opens.
                yield return Until(() => _levels.FloorDrop != null, 120);
                Assert.That(_levels.LabCleared, Is.True, entry + ": cleared");
                RewardDrop drop = _levels.FloorDrop;
                Assert.That(drop, Is.Not.Null, entry + ": the exit works, the floor's reward lands");
                float until = Time.realtimeSinceStartup + 10f; // it flies before it lies still: real seconds, not frames
                while (!drop.Settled && Time.realtimeSinceStartup < until) yield return null;
                Assert.That(drop.Settled, Is.True, entry + ": the drop lies still");
                _player.Teleport(drop.Landing);
                yield return Frames(3);
                Assert.That(_levels.DropInReach(), Is.SameAs(drop), entry + ": in her reach where it lies, " + Vector3.Distance(_player.Position, drop.Landing).ToString("0.00", Inv) + " m");
                Assert.That(_levels.PickUpDrop(), Is.True, entry + ": picked up");
                yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 60);
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), entry + ": the floor's reward comes");
                yield return new WaitForSecondsRealtime(0.5f); // past the choice's guard
                Assert.That(_levels.PickReward(0), Is.True);
                yield return Frames(2);
                Assert.That(_levels.InLab && _levels.Choice == GeneratedLevels.ChoiceKind.None, Is.True, entry + ": taken, she stays in the lab");
            }
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator InARun_WithTheSwitchOn_FloorTwoEndsInARoomOfItsOwn_AndClearingItFinishesTheFloor()
        {
            var v = new LevelValues { ExitRoomsOfTheirOwn = true };
            ulong seed = 0;
            for (ulong s = 1; s < 40 && seed == 0; s++)
                if (LevelGenerator.Generate(s, 2, v).ExitRoom.Shape == RoomShape.Template) seed = s;
            Assert.That(seed, Is.Not.Zero);
            _levels.Values = v;
            GeneratedLevel level = _levels.Build(seed, 2);
            Assert.That(level, Is.Not.Null);
            yield return Frames(2);
            LevelRoom exit = level.Layout.ExitRoom;
            Assert.That(ExitRooms.IsWayDown(exit.Template.Template), Is.True, "floor 2's exit room is one of the third set");
            Assert.That(exit.Theme, Is.EqualTo(RoomTheme.ExitHall));
            Assert.That(level.Boss, Is.Null, "no act boss on floor 2");
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, exit);
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            float walk = PathToTheWayDown(level, exit, level.ToWorld(new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5)));
            Assert.That(walk, Is.GreaterThan(0f), "a whole way on the NavMesh from its door in to the way down");
            // Its fight wakes when she is in it, and cleared, the floor is finished.
            LevelPoint mid = exit.Middle(level.Layout.CellSize);
            _player.Teleport(level.ToWorld(mid));
            yield return Until(() => _levels.CurrentRoom == exit, 30);
            RoomFight fight = _levels.FightOf(exit);
            yield return Until(() => fight.On, 30);
            Assert.That(fight.On, Is.True, "in it, the room is awake");
            Kill(level.ExitEnemies);
            yield return Until(() => _levels.Cleared, 120);
            Assert.That(_levels.Cleared, Is.True, "the exit room cleared: the floor is finished");
            string line = string.Format(Inv, "EXITRUN seed {0} floor 2: {1}, {2} enemies, walk from its door to the way down {3:0.0} m", LevelSeed.ToText(seed), exit.Template, exit.EnemyCount, walk);
            Debug.Log(line);
            TestContext.WriteLine(line);
        }
    }
}
