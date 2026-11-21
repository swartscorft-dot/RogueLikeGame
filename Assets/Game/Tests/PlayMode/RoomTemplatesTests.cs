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
    /// Rooms drawn by hand, built (work order 058; Kristian's words of 2026-11-20 and 2026-11-21; the research report d's
    /// proposal B), in the ControlRoom scene through the intents of <see cref="GeneratedLevels"/>: for ten seeds on floors 1 and
    /// 5, every templated room has a complete NavMesh path from its door in to its door out and to every one of its enemies, and
    /// no NavMesh over a solid cell; and with the switch off the floor's objects are those of the same seed's floor of version
    /// 14. Writes what it found ("TEMPLATEPATHS").
    /// </summary>
    public class RoomTemplatesPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

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

        GeneratedLevel Build(ulong seed, int floor, Action<LevelValues> change = null)
        {
            _levels = GeneratedLevels.Ensure();
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>The middle of the cell inside the room at a door's opening, on the ground, in the world.</summary>
        static Vector3 InsideDoor(GeneratedLevel level, LevelRoom room, LevelDoor door)
        {
            Assert.That(RoomShapes.InnerCell(room, door, door.From, out int x, out int y, out _, out _), "the door is on the room");
            float cs = level.Layout.CellSize;
            return level.ToWorld(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs));
        }

        [UnityTest]
        public IEnumerator TenSeeds_EveryTemplatedRoom_HasAWayFromItsDoorInToItsDoorOutAndToEveryEnemy_AndNoNavMeshOverASolidCell()
        {
            int rooms = 0, ways = 0, triangles = 0;
            var path = new NavMeshPath();
            foreach (int floor in new[] { 1, 5 })
                for (ulong seed = 0; seed < 10; seed++)
                {
                    GeneratedLevel level = Build(seed, floor);
                    yield return Frames(2);
                    LevelLayout layout = level.Layout;
                    float cs = layout.CellSize;
                    string where = "seed " + layout.SeedText + " floor " + floor;
                    Assert.That(layout.Templates, Is.True, "on by default: " + where);

                    // No NavMesh over a solid cell.
                    NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
                    for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
                    {
                        Vector3 middle = (mesh.vertices[mesh.indices[t]] + mesh.vertices[mesh.indices[t + 1]] + mesh.vertices[mesh.indices[t + 2]]) / 3f;
                        LevelPoint p = level.ToLevel(middle);
                        int cx = Mathf.FloorToInt((float)(p.X / cs)), cy = Mathf.FloorToInt((float)(p.Y / cs));
                        Assert.That(layout.IsSolidCell(cx, cy), Is.False, "NavMesh over solid cell " + (cx, cy) + " " + where);
                        triangles++;
                    }

                    foreach (LevelRoom room in layout.Rooms)
                    {
                        if (room.Template == null) continue;
                        rooms++;
                        string name = room.Letter + room.Order + " (" + room.Template + ", " + RoomShapes.Word(room.Shape) + ") " + where;
                        LevelDoor wayIn = LevelGeometry.EntryDoor(layout, room), wayOut = LevelGeometry.ExitDoor(layout, room);
                        Assert.That(NavMesh.SamplePosition(InsideDoor(level, room, wayIn), out NavMeshHit from, 1.9f, NavMesh.AllAreas), "NavMesh inside the door in: " + name);
                        var targets = new List<(string what, Vector3 at)>();
                        if (wayOut != null) targets.Add(("the door out", InsideDoor(level, room, wayOut)));
                        if (level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> enemies))
                            foreach (Enemy e in enemies)
                                if (e != null) targets.Add((e.name, e.transform.position));
                        foreach ((string what, Vector3 at) in targets)
                        {
                            Assert.That(NavMesh.SamplePosition(at, out NavMeshHit to, 1.9f, NavMesh.AllAreas), "NavMesh at " + what + ": " + name);
                            Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                        "a whole way from the door in to " + what + ": " + name);
                            ways++;
                        }
                    }
                    Object.Destroy(_levels.gameObject);
                    _levels = null;
                    yield return null;
                }
            string line = string.Format(Inv, "TEMPLATEPATHS ten seeds on floors 1 and 5: {0} templated rooms, {1} whole ways from a door in, {2} NavMesh triangles off the solid cells", rooms, ways, triangles);
            Debug.Log(line);
            TestContext.WriteLine(line);
            Assert.That(rooms, Is.GreaterThan(20 * 3), "the elite room, the exit room and some combat rooms of every floor");
        }

        /// <summary>What the templates cost the kit on floor 5 of seed 42, against the same floor with them off (written, not judged: 051's budgets are RoomShapesPlayTests').</summary>
        [UnityTest]
        public IEnumerator Floor5_TheKitsPieces_WithTheTemplatesOffAndOn()
        {
            var counts = new Dictionary<bool, (int pieces, int walls, int caps, int floors, int lights)>();
            foreach (bool templates in new[] { false, true })
            {
                GeneratedLevel level = Build(42UL, 5, v => v.Templates = templates);
                yield return Frames(2);
                counts[templates] = (level.KitPieces.Count, level.Walls, level.SolidCaps.Count, level.Floors,
                                     level.KitLights != null ? level.KitLights.ByRoom.Sum(p => p.Value.Count) : 0);
                Object.Destroy(_levels.gameObject);
                _levels = null;
                yield return null;
            }
            string line = string.Format(Inv, "TEMPLATECOST seed 42 floor 5: kit pieces {0} with the templates off, {1} on ({2:0.00} times); wall boxes {3} and {4}; floor boxes {5} and {6}; carved tops {7} and {8}; the lanterns' lights {9} and {10}",
                                        counts[false].pieces, counts[true].pieces, counts[true].pieces / (double)counts[false].pieces, counts[false].walls, counts[true].walls,
                                        counts[false].floors, counts[true].floors, counts[false].caps, counts[true].caps, counts[false].lights, counts[true].lights);
            Debug.Log(line);
            TestContext.WriteLine(line);
            Assert.That(counts[true].pieces, Is.GreaterThan(0));
            // The templates' budget (the producer, 2026-11-21, with the measured numbers beside it): seed 42's floor 5 has 565 kit
            // pieces with the templates on against 396 off, 1.43 times (rooms drawn by hand have more wall to dress). At most
            // 1.6 times, the margin work order 051 gave its shapes. What it costs a frame has not been measured.
            Assert.That(counts[true].pieces, Is.LessThanOrEqualTo(counts[false].pieces * 1.6), "the kit's pieces with the templates at most 1.6 times those without");

            // The lights, as RoomShapesPlayTests judges them, with the templates on: with the lanterns' lights on too, never more
            // on at once than the kit's budget a room, the door's and hers.
            GeneratedLevel lit = Build(42UL, 5, v => v.Crypt.LanternLights = true);
            yield return Frames(2);
            int budget = lit.Values.Crypt.LightsPerRoom + lit.Values.Signs.LightsMost + 1, most = 0;
            if (lit.KitLights != null)
                foreach (KeyValuePair<int, List<Light>> pair in lit.KitLights.ByRoom)
                    Assert.That(pair.Value.Count, Is.LessThanOrEqualTo(lit.Values.Crypt.LightsPerRoom), "the lanterns' lights of room " + pair.Key);
            foreach (LevelRoom room in lit.Layout.Rooms)
            {
                _player.Teleport(lit.Middle(room));
                yield return Frames(3);
                int on = Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled && l.type != LightType.Directional
                    && (l.transform.root == lit.transform || (lit.Mood != null && lit.Mood.Root != null && l.transform.root == lit.Mood.Root.transform)));
                most = Math.Max(most, on);
                Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
            }
            string lights = "TEMPLATELIGHTS seed 42 floor 5 with the templates on and the lanterns' lights on: at most " + most + " lights on at once, the budget " + budget;
            Debug.Log(lights);
            TestContext.WriteLine(lights);
        }

        [UnityTest]
        public IEnumerator SwitchOff_TheFloorsObjects_AreVersion14s()
        {
            GeneratedLevel off = Build(42UL, 1, v => v.Templates = false);
            yield return Frames(2);
            Assert.That(off.Layout.Templates, Is.False);
            Assert.That(off.Layout.Rooms.All(r => r.Template == null && r.Shape != RoomShape.Template), "no template");
            // The floor's objects (floors, walls, the kit's pieces), name for name and place for place, as a floor built from the
            // same seed's layout with the templates off (version 14's, EditMode's RoomTemplatesTests) gives them.
            LevelLayout plain = LevelGenerator.Generate(off.Layout.Seed, 1, new LevelValues { Templates = false });
            Assert.That(plain.Describe(), Is.EqualTo(off.Layout.Describe()), "the same floor");
            Assert.That(plain.Describe(), Does.Not.Contain("template"));
            string Objects(GeneratedLevel l) => string.Join(";", l.FloorBoxes.Concat(l.WallBoxes).Concat(l.KitPieces.Select(p => p.Object))
                .Select(g => g.name + " " + g.transform.position.ToString("F3") + " " + g.transform.localScale.ToString("F3")));
            string first = Objects(off);
            GeneratedLevel again = GeneratedLevel.Build(plain, new LevelValues { Templates = false, RoomMood = false }, null, null);
            yield return null;
            Assert.That(Objects(again), Is.EqualTo(first), "the same objects");
            Assert.That(again.KitPlan.Describe(), Is.EqualTo(off.KitPlan.Describe()));
            Object.Destroy(again.gameObject);

            // And with them on, the same seed's floor is another (its rooms drawn by hand).
            Object.Destroy(_levels.gameObject);
            _levels = null;
            yield return null;
            GeneratedLevel on = Build(42UL, 1);
            yield return Frames(2);
            Assert.That(on.Layout.Rooms.Any(r => r.Shape == RoomShape.Template), "a room stamped from its template");
        }
    }
}
