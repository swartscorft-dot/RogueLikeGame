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
    /// More rooms drawn by hand, built (work order 072; Kristian, the evening of 2026-11-21: "Flere rom-maler"), in the
    /// ControlRoom scene through the intents of <see cref="GeneratedLevels"/>: on floors chosen so that every template of the
    /// library's second set is built at least once, every room of the second set has a whole NavMesh path from its door in to its
    /// door out (the exit room: to its middle) and to every one of its enemies, and a way from door to door over its cells whose
    /// middles stand on the NavMesh a metre at least from its edge (as PassagesPlayTests holds the passages' cells): she walks
    /// through, not squeezes. The kit's pieces with the second set are RoomTemplatesPlayTests' budget. Writes what it found
    /// ("MOREPATHS").
    ///
    /// Frames without a window are short (about 0.3 ms): the waits here count frames, never a frame's length.
    /// </summary>
    public class MoreRoomTemplatesPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // Seeds and floors whose rooms hold every template of the second set between them, stamped (found on .NET with the
        // defaults of version 17: 39's floor 3 two-islands, lopsided, pit, ring, hourglass, throne-wide; 49's floor 3 alcoves,
        // stepped, gallery, pillared-hall, broken-ring; 9's floor 3 dais, horseshoe, arena; 2's floor 1 throne-pillars). The test
        // says so if one is missing.
        static readonly (ulong seed, int floor)[] Floors = { (39UL, 3), (49UL, 3), (9UL, 3), (2UL, 1) };

        PlayerCharacter _player;
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
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (FloorMap.Current != null) Object.Destroy(FloorMap.Current.gameObject);
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static Vector3 CellMiddle(GeneratedLevel level, int x, int y) =>
            level.ToWorld(new LevelPoint((x + 0.5) * level.Layout.CellSize, (y + 0.5) * level.Layout.CellSize));

        /// <summary>The cell inside the room at a door's opening (its first cell).</summary>
        static (int x, int y) InsideDoor(LevelRoom room, LevelDoor door)
        {
            Assert.That(RoomShapes.InnerCell(room, door, door.From, out int x, out int y, out _, out _), "the door is on the room");
            return (x, y);
        }

        [UnityTest]
        public IEnumerator TheSecondSetsRooms_SheWalksFromDoorToDoor_AMetreFromTheNavMeshsEdge_AndReachesEveryEnemy()
        {
            var built = new HashSet<string>();
            int rooms = 0, ways = 0, clearCells = 0, openCells = 0;
            var path = new NavMeshPath();
            foreach ((ulong seed, int floor) in Floors)
            {
                _levels = GeneratedLevels.Ensure();
                _levels.Values = new LevelValues();
                GeneratedLevel level = _levels.Build(seed, floor);
                Assert.That(level, Is.Not.Null, "a level was built");
                yield return Frames(2);
                LevelLayout layout = level.Layout;
                string where = "seed " + layout.SeedText + " floor " + floor;
                foreach (LevelRoom room in layout.Rooms)
                {
                    if (room.Template == null || room.Template.Template.Since != RoomTemplateLibrary.MoreSince) continue;
                    Assert.That(room.Shape, Is.EqualTo(RoomShape.Template), "stamped as drawn: " + room.Template + " " + where);
                    rooms++;
                    built.Add(room.Template.Template.Name);
                    string name = room.Letter + room.Order + " (" + room.Template + ") " + where;

                    // Every open cell whose middle stands on the NavMesh a metre from its edge (a cell under a prop is not).
                    var clear = new HashSet<(int, int)>();
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                        {
                            if (!room.IsOpen(x, y)) continue;
                            openCells++;
                            if (NavMesh.SamplePosition(CellMiddle(level, x, y), out NavMeshHit on, 0.3f, NavMesh.AllAreas)
                                && NavMesh.FindClosestEdge(on.position, out NavMeshHit edge, NavMesh.AllAreas) && edge.distance >= 1f)
                                clear.Add((x, y));
                        }
                    clearCells += clear.Count;

                    // A way over those cells, side to side, from the cell inside the door in to the one inside the door out (the exit
                    // room: its middle cell).
                    LevelDoor wayIn = LevelGeometry.EntryDoor(layout, room), wayOut = LevelGeometry.ExitDoor(layout, room);
                    (int x, int y) from = InsideDoor(room, wayIn);
                    LevelPoint mid = room.Middle(layout.CellSize);
                    (int x, int y) to = wayOut != null ? InsideDoor(room, wayOut) : ((int)Math.Floor(mid.X / layout.CellSize), (int)Math.Floor(mid.Y / layout.CellSize));
                    Assert.That(clear.Contains(from), "the cell inside the door in a metre from the NavMesh's edge: " + name);
                    Assert.That(clear.Contains(to), "the cell inside the door out (or the middle) a metre from the NavMesh's edge: " + name);
                    var seen = new HashSet<(int, int)> { from };
                    var queue = new Queue<(int x, int y)>();
                    queue.Enqueue(from);
                    while (queue.Count > 0)
                    {
                        (int x, int y) c = queue.Dequeue();
                        foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        {
                            (int, int) n = (c.x + dx, c.y + dy);
                            if (clear.Contains(n) && seen.Add(n)) queue.Enqueue(n);
                        }
                    }
                    Assert.That(seen.Contains(to), "a way from door to door over cells a metre from the NavMesh's edge: " + name);

                    // And the NavMesh's own path, to the door out and to every enemy.
                    Assert.That(NavMesh.SamplePosition(CellMiddle(level, from.x, from.y), out NavMeshHit start, 0.5f, NavMesh.AllAreas), name);
                    var targets = new List<(string what, Vector3 at)> { ("the door out or the middle", CellMiddle(level, to.x, to.y)) };
                    if (level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> enemies))
                        foreach (Enemy e in enemies)
                            if (e != null) targets.Add((e.name, e.transform.position));
                    foreach ((string what, Vector3 at) in targets)
                    {
                        Assert.That(NavMesh.SamplePosition(at, out NavMeshHit end, 1.9f, NavMesh.AllAreas), "NavMesh at " + what + ": " + name);
                        Assert.That(NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                    "a whole way from the door in to " + what + ": " + name);
                        ways++;
                    }
                }
                Object.Destroy(_levels.gameObject);
                _levels = null;
                yield return null;
            }
            string line = string.Format(Inv, "MOREPATHS {0} floors: {1} rooms of the second set ({2} of its {3} templates), {4} whole ways from a door in; {5} of their {6} open cells a metre from the NavMesh's edge at the middle (the others under a prop or beside one)",
                                        Floors.Length, rooms, built.Count, RoomTemplateLibrary.All.Count(t => t.Since == RoomTemplateLibrary.MoreSince), ways, clearCells, openCells);
            Debug.Log(line);
            TestContext.WriteLine(line);
            var missing = RoomTemplateLibrary.All.Where(t => t.Since == RoomTemplateLibrary.MoreSince && !built.Contains(t.Name)).Select(t => t.Name).ToList();
            Assert.That(missing, Is.Empty, "every template of the second set built on these floors");
        }
    }
}
