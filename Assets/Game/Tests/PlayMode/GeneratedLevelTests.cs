using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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
    /// Generated levels walked in grey boxes inside the ControlRoom scene (work order 006), driven through the intents
    /// of <see cref="GeneratedLevels"/>: the level is built far from the practice room with a floor per room and walls,
    /// she can walk from the start to every room and not through a wall, the enemies stand at their points with the
    /// level's health and stay dead, the boss's death clears the level, and going back removes it all.
    /// </summary>
    public class GeneratedLevelTests
    {
        const ulong Seed = 42UL;

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;

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
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        GeneratedLevel Build(ulong seed = Seed)
        {
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.Build(seed);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        [UnityTest]
        public IEnumerator NotInstalledByItself_InATestRun()
        {
            yield return null;
            Assert.That(GeneratedLevels.Current, Is.Null, "nothing runs unless a test makes it");
            Assert.That(Object.FindAnyObjectByType<GeneratedLevels>(), Is.Null);
        }

        [UnityTest]
        public IEnumerator Build_PutsTheLevelFarFromThePracticeRoom_AndHerAtItsStart()
        {
            GeneratedLevel level = Build();
            yield return null;
            LevelLayout layout = level.Layout;
            Assert.That(layout.Seed, Is.EqualTo(Seed));
            Assert.That(layout.Describe(), Is.EqualTo(LevelGenerator.Generate(Seed, new LevelValues()).Describe()), "the level of the seed");
            Assert.That(Flat(_player.Position, level.PlayerStart), Is.LessThan(0.3f), "she stands at its start");
            Assert.That(Flat(level.PlayerStart, new Vector3(500f, 0f, 0f)), Is.LessThan(0.01f), "500 m to one side");
            foreach (LevelRoom r in layout.Rooms)
                Assert.That(Flat(level.Middle(r), Vector3.zero), Is.GreaterThan(100f), "every room far from the practice room: " + r.Letter + r.Order);

            // Work order 051: a floor box per rectangle of a room's open cells (RoomShapes.OpenRects), so a carved room may have more.
            Assert.That(level.Floors, Is.EqualTo(layout.Rooms.Sum(r => RoomShapes.OpenRects(r).Count)), "a floor per rectangle of a room's open cells");
            Assert.That(level.Floors, Is.GreaterThanOrEqualTo(layout.Rooms.Count));
            Assert.That(level.Walls, Is.GreaterThan(layout.Rooms.Count));
            Assert.That(level.LowWalls, Is.GreaterThan(0), "walls on the camera's side are low");
            Assert.That(level.LowWalls, Is.LessThan(level.Walls));
            // Work order 009: and each prop has one.
            // Changed deliberately (work order 052): and the large props that block walking.
            Assert.That(level.GetComponentsInChildren<Collider>().Length, Is.EqualTo(level.Floors + level.Walls + level.Props.Count + level.LargeProps.Count(p => !PropSize.Of(p.Prop.Model).Walkable)),
                        "floors, walls and props have colliders");

            Assert.That(_levels.CurrentRoom, Is.Not.Null);
            Assert.That(_levels.CurrentRoom.Kind, Is.EqualTo(RoomKind.Entrance));
            StringAssert.Contains(layout.SeedText, _levels.StatusText());
            StringAssert.Contains("room 1 of " + layout.Rooms.Count, _levels.StatusText());
            StringAssert.Contains("v" + LevelGenerator.Version, _levels.StatusText());
        }

        [UnityTest]
        public IEnumerator SheCanWalk_FromTheStart_ToEveryRoom_ButNotThroughAWall()
        {
            GeneratedLevel level = Build();
            yield return null;
            LevelLayout layout = level.Layout;
            Vector3 Ground(Vector3 p)
            {
                Assert.That(NavMesh.SamplePosition(p, out NavMeshHit hit, 1f, NavMesh.AllAreas), "NavMesh at " + p + "\n" + LevelMap.Draw(layout));
                return hit.position;
            }

            Vector3 start = Ground(level.PlayerStart);
            var path = new NavMeshPath();
            foreach (LevelRoom r in layout.Rooms)
            {
                Assert.That(NavMesh.CalculatePath(start, Ground(level.Middle(r)), NavMesh.AllAreas, path), "a path to " + r.Letter + r.Order);
                Assert.That(path.status, Is.EqualTo(NavMeshPathStatus.PathComplete), "the whole way to " + r.Letter + r.Order + "\n" + LevelMap.Draw(layout));
            }

            // At each door, the wall cell beside it: 1.5 m either side of the wall, the straight line is blocked and the
            // way round goes through the door, much longer than the 3 m between the two points.
            // Changed deliberately (work order 051): the cell beside the door is its gate in the room beyond, carved; the first
            // wall cell along the wall with open floor on both sides is taken (a door with none is passed).
            double cs = layout.CellSize;
            int doorsTried = 0;
            foreach (LevelDoor d in layout.Doors)
            {
                int beside = int.MinValue;
                foreach (int k in new[] { d.From - 1, d.From + d.Width, d.From - 2, d.From + d.Width + 1, d.From - 3, d.From + d.Width + 2 })
                {
                    bool openA = d.Vertical ? layout.RoomAtCell(d.Line - 1, k) == layout.Rooms[d.RoomA] || layout.RoomAtCell(d.Line - 1, k) == layout.Rooms[d.RoomB]
                                            : layout.RoomAtCell(k, d.Line - 1) == layout.Rooms[d.RoomA] || layout.RoomAtCell(k, d.Line - 1) == layout.Rooms[d.RoomB];
                    bool openB = d.Vertical ? layout.RoomAtCell(d.Line, k) == layout.Rooms[d.RoomA] || layout.RoomAtCell(d.Line, k) == layout.Rooms[d.RoomB]
                                            : layout.RoomAtCell(k, d.Line) == layout.Rooms[d.RoomA] || layout.RoomAtCell(k, d.Line) == layout.Rooms[d.RoomB];
                    if (!openA || !openB) continue;
                    // And floor to stand on there (no prop on either point).
                    double alongK = (k + 0.5) * cs, lineK = d.Line * cs;
                    LevelPoint qa = d.Vertical ? new LevelPoint(lineK - 1.5, alongK) : new LevelPoint(alongK, lineK - 1.5);
                    LevelPoint qb = d.Vertical ? new LevelPoint(lineK + 1.5, alongK) : new LevelPoint(alongK, lineK + 1.5);
                    if (!NavMesh.SamplePosition(level.ToWorld(qa), out _, 0.5f, NavMesh.AllAreas) || !NavMesh.SamplePosition(level.ToWorld(qb), out _, 0.5f, NavMesh.AllAreas)) continue;
                    beside = k;
                    break;
                }
                if (beside == int.MinValue) continue;
                doorsTried++;
                double along = (beside + 0.5) * cs;
                double line = d.Line * cs;
                LevelPoint a = d.Vertical ? new LevelPoint(line - 1.5, along) : new LevelPoint(along, line - 1.5);
                LevelPoint b = d.Vertical ? new LevelPoint(line + 1.5, along) : new LevelPoint(along, line + 1.5);
                Vector3 pa = Ground(level.ToWorld(a)), pb = Ground(level.ToWorld(b));
                string where = "beside door " + d.RoomA + "-" + d.RoomB;
                Assert.That(NavMesh.Raycast(pa, pb, out _, NavMesh.AllAreas), Is.True, "the wall blocks the straight line " + where);
                Assert.That(NavMesh.CalculatePath(pa, pb, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete, "a way round " + where);
                float length = 0f;
                for (int i = 1; i < path.corners.Length; i++) length += Vector3.Distance(path.corners[i - 1], path.corners[i]);
                Assert.That(length, Is.GreaterThan(5f), "the way round goes through the door " + where);
            }
            Assert.That(doorsTried, Is.GreaterThan(0), "a wall cell beside a door with open floor on both sides");

            // And none out of the level: the ground just outside the start room's outer walls has no NavMesh.
            Assert.That(NavMesh.SamplePosition(level.PlayerStart + new Vector3(0f, 0f, 0f), out _, 0.5f, NavMesh.AllAreas), Is.True);
            Assert.That(NavMesh.SamplePosition(new Vector3(500f, 0f, -300f), out _, 5f, NavMesh.AllAreas), Is.False, "nothing far outside");
        }

        [UnityTest]
        public IEnumerator Enemies_StandAtTheirPoints_WithTheLevelsHealth()
        {
            // Work order 012: floor 5, for the act boss; every enemy stands at its place in the description (a pack round
            // its point, a line across it), and every kind has the floor's health for it, the act boss the boss's.
            _levels = GeneratedLevels.Ensure();
            GeneratedLevel level = _levels.Build(Seed, 5);
            yield return null;
            yield return null;
            LevelLayout layout = level.Layout;
            // Work order 008: a point holds a group of a kind (a pack is several swarmers round it), and each kind has
            // its own health; a brute the level's, the boss room's enemy the boss's.
            int enemies = layout.Rooms.Sum(r => Enumerable.Range(0, r.EnemyPoints.Count).Sum(i => r.GroupAt(i).Count));
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy is there to copy");
            Assert.That(level.Enemies.Count, Is.EqualTo(enemies), "the enemies of every point");
            Assert.That(level.Groups.Count, Is.EqualTo(layout.Rooms.Sum(r => r.EnemyPoints.Count)), "a group at every point");
            foreach (GeneratedLevel.PlacedGroup g in level.Groups)
            {
                LevelRoom r = g.Room;
                LevelPoint[] places = r.PlacesAt(g.Point);
                Assert.That(g.Members.Count, Is.EqualTo(g.Group.Count));
                for (int m = 0; m < g.Members.Count; m++)
                {
                    Enemy e = g.Members[m];
                    string where = "room " + r.Letter + r.Order + " point " + g.Point;
                    Assert.That(e.Kind, Is.EqualTo(g.Group.Kind), where);
                    Assert.That(Flat(e.Home, level.ToWorld(places[m])), Is.LessThan(0.3f), "home at its place: " + where);
                    Assert.That(e.ComesBack, Is.False);
                    var d = e.GetComponent<Damageable>();
                    // The elite of the elite room has its kind's health times the elite's share (work order 017).
                    int wanted = g.Group.Elite ? level.Values.EliteHealthOf(g.Group.Kind) : level.Values.HealthOf(g.Group.Kind, g.Group.Boss);
                    Assert.That(d.MaxHealth, Is.EqualTo(wanted), where);
                    Assert.That(d.CurrentHealth, Is.EqualTo(wanted));
                }
            }
            Assert.That(level.Boss, Is.Not.Null);
            Assert.That(level.Boss.GetComponent<Damageable>().MaxHealth, Is.EqualTo(18000)); // 9000 until work order 014
            Assert.That(level.Values.SwarmerHealth, Is.EqualTo(150));
            Assert.That(level.Values.ShooterHealth, Is.EqualTo(400));
            Assert.That(level.Values.EnemyHealth, Is.EqualTo(1200));
            Assert.That(level.Values.GuardHealth, Is.EqualTo(3500));
            Assert.That(_practiceEnemy.ComesBack, Is.True, "the practice enemy comes back as before");
            Assert.That(_practiceEnemy.HealthOverride, Is.EqualTo(0));
            Assert.That(_practiceEnemy.GetComponent<Damageable>().MaxHealth, Is.EqualTo(_practiceEnemy.Values.Health));
        }

        // Work order 012: a floor is finished when every enemy of its exit room is dead (it was the boss room's one enemy).
        [UnityTest]
        public IEnumerator AGeneratedEnemy_StaysDead_AndTheExitRoomClearsTheFloor()
        {
            GeneratedLevel level = Build();
            yield return null;
            yield return null;
            Enemy first = level.Enemies.First(e => !level.ExitEnemies.Contains(e));
            first.GetComponent<Damageable>().TakeHit(100000);
            yield return null;
            Assert.That(first.IsDead, Is.True);
            Assert.That(_levels.Cleared, Is.False, "not by any enemy");

            List<Enemy> exit = level.ExitEnemies.ToList();
            Assert.That(exit.Count, Is.GreaterThanOrEqualTo(9), "the exit room's fight");
            for (int i = 0; i < exit.Count - 1; i++) exit[i].GetComponent<Damageable>().TakeHit(100000);
            yield return null;
            yield return null;
            Assert.That(_levels.Cleared, Is.False, "one is left in the exit room");
            exit[exit.Count - 1].GetComponent<Damageable>().TakeHit(100000);
            yield return null;
            yield return null;
            Assert.That(_levels.Cleared, Is.True, "the exit room is cleared");
            StringAssert.Contains("FLOOR CLEARED", _levels.StatusText());
            StringAssert.Contains(level.Layout.SeedText, _levels.StatusText());

            yield return new WaitForSeconds(first.Values.RespawnDelay + 0.5f);
            Assert.That(first.IsDead, Is.True, "it does not come back");
            Assert.That(exit.All(e => e.IsDead), Is.True);
        }

        [UnityTest]
        public IEnumerator Rebuild_GivesTheSameLevel_ANewSeedAnother_AndBackRemovesIt()
        {
            Vector3 before = _player.Position;
            GeneratedLevel first = Build();
            string described = first.Layout.Describe();
            Vector3[] homes = first.Enemies.Select(e => e.Home).ToArray();
            yield return null;

            GeneratedLevel again = _levels.Rebuild();
            yield return null;
            Assert.That(first == null, "the old level is gone");
            Assert.That(again.Layout.Describe(), Is.EqualTo(described));
            Assert.That(again.Enemies.Select(e => e.Home).ToArray(), Is.EqualTo(homes));
            Assert.That(Object.FindObjectsByType<GeneratedLevel>(FindObjectsSortMode.None).Length, Is.EqualTo(1));

            GeneratedLevel other = _levels.BuildNew();
            yield return null;
            Assert.That(other.Layout.Seed, Is.Not.EqualTo(Seed));
            Assert.That(other.Layout.DescribeLayout(), Is.Not.EqualTo(again.Layout.DescribeLayout()));
            Assert.That(Flat(_player.Position, other.PlayerStart), Is.LessThan(0.3f));

            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(Object.FindObjectsByType<GeneratedLevel>(FindObjectsSortMode.None), Is.Empty, "the level is removed");
            Assert.That(Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None), Is.EqualTo(new Enemy[] { _practiceEnemy }), "and its enemies, of every kind");
            Assert.That(Flat(_player.Position, before), Is.LessThan(0.3f), "she is back where she was");
            Vector3 feet = new Vector3(_player.Position.x, 0f, _player.Position.z); // her root stands 1 m over the ground
            Assert.That(NavMesh.SamplePosition(feet, out _, 0.5f, NavMesh.AllAreas), "on the practice room's NavMesh");
            Assert.That(_levels.StatusText(), Is.Empty);
        }

        [UnityTest]
        public IEnumerator EnteringARoom_IsTracked_AndRecorded()
        {
            _folder = Path.Combine(Path.GetTempPath(), "GeneratedLevelTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            GeneratedLevel level = Build();
            yield return null;
            LevelRoom combat = level.Layout.Rooms.First(r => r.Kind == RoomKind.Combat);
            _player.Teleport(level.Middle(combat));
            yield return null;
            yield return null;
            Assert.That(_levels.CurrentRoom, Is.SameAs(combat));
            Assert.That(_levels.RoomsEntered, Is.EquivalentTo(new[] { 0, combat.Order }));
            StringAssert.Contains("room " + (combat.Order + 1) + " of " + level.Layout.Rooms.Count, _levels.StatusText());

            SessionEvent built = _recorder.Events.Single(e => e.Type == SessionEventTypes.LevelBuilt);
            Assert.That(built.Name, Is.EqualTo(level.Layout.SeedText));
            Assert.That(built.ExtraValue("version"), Is.EqualTo(LevelGenerator.Version.ToString()));
            Assert.That(built.ExtraValue("rooms"), Is.EqualTo(level.Layout.Rooms.Count.ToString()));
            var entered = _recorder.Events.Where(e => e.Type == SessionEventTypes.RoomEntered).ToList();
            Assert.That(entered.Select(e => e.ExtraValue("room")), Is.EqualTo(new[] { "0", combat.Order.ToString() }));
            Assert.That(entered.Select(e => e.Name), Is.EqualTo(new[] { "entrance", "combat" }));

            // The recorder listens to the level's enemies too.
            level.Enemies[0].GetComponent<Damageable>().TakeHit(100000);
            yield return null;
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.EnemyDeath), Is.EqualTo(1));

            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Generated level " + level.Layout.SeedText + "   rooms entered 2 of " + level.Layout.Rooms.Count, summary);
        }
    }
}
