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
    /// Rooms that are not boxes (work order 051; Kristian's words of 2026-11-20: "Now we have very clear squares per room ...
    /// not walking into large office landscapes with monsters in them"), built, in the ControlRoom scene through the intents of
    /// <see cref="GeneratedLevels"/>: every carved cell's open sides have a collider that stops shots; the NavMesh covers no
    /// carved cell; she cannot walk into one from any side, driven at it; a shot across a carved corner is stopped; every
    /// enemy of a carved room has a way to every open cell of it, the alcoves' too; the kit's pieces and the lights stay inside
    /// their budgets on floor 5; the small map draws the carved outline; and with the switch off the floor's objects are
    /// those of the same seed's floor of version 12 (044 and 046 merged). Writes what the shapes cost ("SHAPECOST").
    /// </summary>
    public class RoomShapesPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong Seed = 42UL;

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

        GeneratedLevel Build(int floor, Action<LevelValues> change = null, ulong seed = Seed)
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

        static string Where(GeneratedLevel level) => "seed " + level.Layout.SeedText + " floor " + level.Layout.Floor;

        /// <summary>The middle of a cell, on the ground, in the world.</summary>
        static Vector3 CellMiddle(GeneratedLevel level, int x, int y) =>
            level.ToWorld(new LevelPoint((x + 0.5) * level.Layout.CellSize, (y + 0.5) * level.Layout.CellSize));

        /// <summary>Every open side of every carved cell of the floor: the cell, and the step from it to the open cell.</summary>
        static List<(LevelRoom room, int x, int y, int dx, int dy)> OpenSides(LevelLayout layout)
        {
            var sides = new List<(LevelRoom, int, int, int, int)>();
            foreach (LevelRoom room in layout.Rooms)
                foreach ((int x, int y) in room.SolidList)
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                        if (room.IsOpen(x + dx, y + dy)) sides.Add((room, x, y, dx, dy));
            return sides;
        }

        [UnityTest]
        public IEnumerator ABuiltFloor_HasColliders_OnEveryCarvedSide_AndANavMeshOnNoCarvedCell()
        {
            foreach (int floor in new[] { 1, 5 })
            {
                GeneratedLevel level = Build(floor);
                yield return Frames(2);
                LevelLayout layout = level.Layout;
                string where = Where(level);
                Assert.That(layout.Shapes, Is.True, "on by default");
                List<(LevelRoom room, int x, int y, int dx, int dy)> sides = OpenSides(layout);
                Assert.That(sides.Count, Is.GreaterThan(layout.Rooms.Count * 4), "carved sides: " + where);
                float cs = layout.CellSize;
                var walls = new HashSet<GameObject>(level.WallBoxes);

                // A collider that stops shots on every open side, low and high, along its whole length.
                foreach ((LevelRoom room, int x, int y, int dx, int dy) in sides)
                    foreach (float along in new[] { -1.5f, 0f, 1.5f })
                        foreach (float height in new[] { 0.5f, 0.9f })
                        {
                            Vector3 dir = new Vector3(-dx, 0f, -dy);
                            Vector3 across = new Vector3(dy, 0f, dx);
                            Vector3 from = CellMiddle(level, x + dx, y + dy) + across * along + Vector3.up * height;
                            // A wall box of the floor on the cell's side (a prop or an enemy may stand before it).
                            RaycastHit[] hits = Physics.RaycastAll(from, dir, cs, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                            RaycastHit wall = hits.Where(h => walls.Contains(h.collider.gameObject)).OrderBy(h => h.distance).FirstOrDefault();
                            Assert.That(wall.collider, Is.Not.Null, "a wall box on the side of carved cell " + (x, y) + " toward " + (x + dx, y + dy) + ", " + where);
                            Assert.That(wall.distance, Is.LessThan(cs * 0.5f + 0.01f), "on the cell's side: " + (x, y) + " " + where);
                            Assert.That(wall.collider.GetComponent<Cover>(), Is.Not.Null, "it stops shots: " + wall.collider.name + " " + where);
                        }

                // No floor box under a carved cell, and the NavMesh nowhere over one.
                for (int i = 0; i < level.FloorBoxes.Count; i++)
                {
                    CellRect c = level.FloorCellsOf(i);
                    for (int fx = c.X; fx < c.Right; fx++)
                        for (int fy = c.Y; fy < c.Top; fy++)
                            Assert.That(layout.IsSolidCell(fx, fy), Is.False, "a floor box over carved cell " + (fx, fy) + " " + where);
                }
                NavMeshTriangulation mesh = NavMesh.CalculateTriangulation();
                Assert.That(mesh.vertices.Length, Is.GreaterThan(0));
                for (int t = 0; t + 2 < mesh.indices.Length; t += 3)
                {
                    Vector3 middle = (mesh.vertices[mesh.indices[t]] + mesh.vertices[mesh.indices[t + 1]] + mesh.vertices[mesh.indices[t + 2]]) / 3f;
                    LevelPoint p = level.ToLevel(middle);
                    int cx = Mathf.FloorToInt((float)(p.X / cs)), cy = Mathf.FloorToInt((float)(p.Y / cs));
                    Assert.That(layout.IsSolidCell(cx, cy), Is.False, "NavMesh over carved cell " + (cx, cy) + " " + where);
                }
                foreach (LevelRoom room in layout.Rooms)
                    foreach ((int x, int y) in room.SolidList)
                        if (NavMesh.SamplePosition(CellMiddle(level, x, y), out NavMeshHit hit, cs * 0.45f, NavMesh.AllAreas))
                        {
                            LevelPoint p = level.ToLevel(hit.position);
                            Assert.That(layout.IsSolidCell(Mathf.FloorToInt((float)(p.X / cs)), Mathf.FloorToInt((float)(p.Y / cs))), Is.False,
                                        "the NavMesh's nearest point to carved cell " + (x, y) + " is not in it, " + where);
                        }
                Assert.That(level.SolidCaps.All(c => c.GetComponent<Collider>() == null && c.layer == CryptKit.Layer), "the carved cells' tops have no collider");
            }
        }

        [UnityTest]
        public IEnumerator She_CannotWalkIntoACarvedCell_FromAnySide()
        {
            GeneratedLevel level = Build(1);
            yield return Frames(2);
            foreach (Enemy e in level.Enemies) e.gameObject.SetActive(false); // nothing in her way but the walls
            LevelLayout layout = level.Layout;
            float cs = layout.CellSize;
            Camera cam = Camera.main;
            Assert.That(cam, Is.Not.Null);
            // The sides with no prop near her way to them.
            List<(LevelRoom room, int x, int y, int dx, int dy)> sides = OpenSides(layout).Where(sd =>
            {
                var a = new LevelPoint((sd.x + sd.dx + 0.5) * cs, (sd.y + sd.dy + 0.5) * cs);
                var b = new LevelPoint((sd.x + 0.5 + sd.dx * 0.5) * cs, (sd.y + 0.5 + sd.dy * 0.5) * cs);
                return sd.room.Props.Concat(level.LargeProps.Where(l => l.Room == sd.room).Select(l => l.Prop)).All(p => p.Footprint.DistanceTo(a, b) > 1.2);
            }).ToList();
            // One side of each way, then more, up to twelve.
            var tried = new List<(LevelRoom room, int x, int y, int dx, int dy)>();
            foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                tried.AddRange(sides.Where(s => s.dx == dx && s.dy == dy).Take(3));
            Assert.That(tried.Select(s => (s.dx, s.dy)).Distinct().Count(), Is.EqualTo(4), "a carved side facing each way on this floor");
            foreach ((LevelRoom room, int x, int y, int dx, int dy) in tried)
            {
                Vector3 start = CellMiddle(level, x + dx, y + dy);
                _player.Teleport(start);
                yield return Frames(2);
                // Toward the carved cell, as the keys would: the screen's directions from the camera's view of the ground.
                Vector3 toward = new Vector3(-dx, 0f, -dy);
                Vector3 screenA = cam.WorldToScreenPoint(start), screenB = cam.WorldToScreenPoint(start + toward * 2f);
                Vector2 move = new Vector2(screenB.x - screenA.x, screenB.y - screenA.y).normalized;
                move = new Vector2(Mathf.Round(move.x), Mathf.Round(move.y));
                _player.GetComponent<PlayerInputDriver>().InputSource = () => new InputFrame { Move = move, CursorScreen = screenB };
                float deepest = float.NegativeInfinity, until = Time.time + 1.2f;
                double faceLine = (dx != 0 ? (dx > 0 ? x + 1 : x) : (dy > 0 ? y + 1 : y)) * cs;
                while (Time.time < until)
                {
                    yield return null;
                    LevelPoint p = level.ToLevel(_player.Position);
                    // How far past the carved cell's side she is, toward its inside (negative: still outside).
                    float past = (float)(dx != 0 ? (dx > 0 ? faceLine - p.X : p.X - faceLine) : (dy > 0 ? faceLine - p.Y : p.Y - faceLine));
                    deepest = Mathf.Max(deepest, past);
                }
                _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
                LevelPoint end = level.ToLevel(_player.Position);
                Assert.That(level.RoomAt(_player.Position), Is.SameAs(room), "she stays in the room's open cells, at " + end + ", driven at " + (x, y));
                Assert.That(deepest, Is.LessThan(-level.Values.WallThickness * 0.5f + 0.05f),
                            "she never reached the wall's face of carved cell " + (x, y) + " from " + (x + dx, y + dy) + ": " + deepest + " m past its side");
                float moved = Vector3.Distance(start, _player.Position);
                Assert.That(moved, Is.GreaterThan(0.5f), "she did walk toward it (" + moved + " m)");
            }
        }

        [UnityTest]
        public IEnumerator AShotAcrossACarvedCorner_IsStopped()
        {
            GeneratedLevel level = Build(5);
            yield return Frames(2);
            LevelLayout layout = level.Layout;
            float cs = layout.CellSize;
            int corners = 0;
            foreach (LevelRoom room in layout.Rooms)
                foreach ((int x, int y) in room.SolidList)
                    foreach ((int sx, int sy) in new[] { (1, 1), (1, -1), (-1, 1), (-1, -1) })
                    {
                        // A corner of the carved cell with open floor on both its sides and beyond it: a corner a shot can cut.
                        if (!room.IsOpen(x + sx, y) || !room.IsOpen(x, y + sy) || !room.IsOpen(x + sx, y + sy)) continue;
                        corners++;
                        var corner = new LevelPoint((x + (sx > 0 ? 1 : 0)) * cs, (y + (sy > 0 ? 1 : 0)) * cs);
                        // Shots from the open cell beside one of its sides to the open cell beside the other, crossing the
                        // carved cell's corner from 5 cm inside it to 75 cm.
                        for (int k = 0; k < 8; k++)
                        {
                            double inside = 0.05 + 0.1 * k;
                            var a = new LevelPoint(corner.X + sx * 2.0, corner.Y - sy * 2.0);
                            var b = new LevelPoint(corner.X - sx * (2.0 + 2 * inside), corner.Y + sy * 2.0);
                            Vector3 wa = level.ToWorld(a), wb = level.ToWorld(b);
                            float length = Vector3.Distance(wa, wb);
                            float clear = Cover.Line(new GroundPoint(wa.x, wa.z), new GroundPoint(wb.x, wb.z));
                            Assert.That(clear, Is.LessThan(length - 0.01f), "a shot cutting carved cell " + (x, y) + "'s corner " + (sx, sy) + " by " + inside + " m is stopped, " + Where(level));
                        }
                    }
            Assert.That(corners, Is.GreaterThan(0), "a carved corner to shoot across on this floor");
        }

        [UnityTest]
        public IEnumerator EveryEnemyOfACarvedRoom_HasAWay_ToEveryOpenCell_TheAlcovesToo()
        {
            int alcoves = 0;
            // Floors 1 and 5 of the first seeds from 40 whose floor has a room with alcoves, and of seed 42.
            var floors = new List<(ulong seed, int floor)> { (42UL, 1), (42UL, 5) };
            for (ulong s = 40UL; s < 400UL && floors.Count < 6; s++)
                foreach (int f in new[] { 1, 5 })
                    if (floors.Count < 6 && LevelGenerator.Generate(s, f).Rooms.Any(r => r.Shape == RoomShape.Alcoves)) floors.Add((s, f));
            foreach ((ulong seed, int floor) in floors)
                {
                    GeneratedLevel level = Build(floor, null, seed);
                    yield return Frames(2);
                    LevelLayout layout = level.Layout;
                    var path = new NavMeshPath();
                    foreach (KeyValuePair<int, List<Enemy>> pair in level.EnemiesByRoom)
                    {
                        LevelRoom room = layout.Rooms[pair.Key];
                        if (room.SolidCount == 0) continue;
                        if (room.Shape == RoomShape.Alcoves) alcoves++;
                        Enemy enemy = pair.Value.FirstOrDefault(e => e != null);
                        if (enemy == null) continue;
                        Assert.That(NavMesh.SamplePosition(enemy.transform.position, out NavMeshHit from, 1f, NavMesh.AllAreas), "the enemy on the NavMesh");
                        for (int x = room.Rect.X; x < room.Rect.Right; x++)
                            for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                            {
                                if (!room.IsOpen(x, y)) continue;
                                // A cell its props fill (the shrine's altar, braziers and basin in a corner) has no floor to reach.
                                if (!NavMesh.SamplePosition(CellMiddle(level, x, y), out NavMeshHit to, 1.9f, NavMesh.AllAreas))
                                {
                                    float cellSize = layout.CellSize;
                                    Assert.That(room.Props.Concat(level.LargeProps.Where(l => l.Room == room).Select(l => l.Prop)).Any(p => p.Footprint.DistanceTo(new LevelPoint((x + 0.5) * cellSize, (y + 0.5) * cellSize)) < 1.9),
                                                "an open cell with no NavMesh has props in it: " + (x, y) + " " + Where(level));
                                    continue;
                                }
                                Assert.That(NavMesh.CalculatePath(from.position, to.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete,
                                            "a way from " + enemy.name + " to open cell " + (x, y) + " of " + room.Letter + room.Order + " (" + RoomShapes.Word(room.Shape) + "), " + Where(level));
                            }
                    }
                }
            Assert.That(alcoves, Is.GreaterThan(0), "a room with alcoves among these floors");
        }

        [UnityTest]
        public IEnumerator Floor5_TheKitsPieces_AndTheLights_StayInsideTheirBudgets()
        {
            var counts = new Dictionary<bool, (int pieces, int walls, int caps, int floors)>();
            foreach (bool shapes in new[] { false, true })
            {
                // Work order 058: this is the budget of the carved shapes alone (work order 051), so the templates are off here; with
                // them on the same floor has 565 pieces against 327 without shapes (1.73 times). The templates' own budget is in
                // RoomTemplatesPlayTests.Floor5_TheKitsPieces_WithTheTemplatesOffAndOn.
                GeneratedLevel level = Build(5, v => { v.Shapes = shapes; v.Templates = false; });
                yield return Frames(2);
                counts[shapes] = (level.KitPieces.Count, level.Walls, level.SolidCaps.Count, level.Floors);
            }
            string line = string.Format(Inv, "SHAPECOST seed {0} floor 5: kit pieces {1} without the shapes, {2} with ({3:0.00} times); wall boxes {4} and {5}; floor boxes {6} and {7}; carved tops {8}",
                                        LevelSeed.ToText(Seed), counts[false].pieces, counts[true].pieces, counts[true].pieces / (double)counts[false].pieces,
                                        counts[false].walls, counts[true].walls, counts[false].floors, counts[true].floors, counts[true].caps);
            Debug.Log(line);
            TestContext.WriteLine(line);
            Assert.That(counts[true].pieces, Is.LessThanOrEqualTo(counts[false].pieces * 1.6), "the kit's pieces at most 1.6 times");

            // The lights: with the lanterns' lights on too, never more on at once than the kit's budget a room, the door's and hers.
            GeneratedLevel lit = Build(5, v => v.Crypt.LanternLights = true);
            yield return Frames(2);
            int budget = lit.Values.Crypt.LightsPerRoom + lit.Values.Signs.LightsMost + 1;
            if (lit.KitLights != null)
                foreach (KeyValuePair<int, List<Light>> pair in lit.KitLights.ByRoom)
                    Assert.That(pair.Value.Count, Is.LessThanOrEqualTo(lit.Values.Crypt.LightsPerRoom), "the lanterns' lights of room " + pair.Key);
            foreach (LevelRoom room in lit.Layout.Rooms)
            {
                _player.Teleport(lit.Middle(room));
                yield return Frames(3);
                int on = Object.FindObjectsByType<Light>().Count(l => l.isActiveAndEnabled && l.type != LightType.Directional
                    && (l.transform.root == lit.transform || (lit.Mood != null && lit.Mood.Root != null && l.transform.root == lit.Mood.Root.transform)));
                Assert.That(on, Is.LessThanOrEqualTo(budget), "lights on in " + room.Letter + room.Order);
            }
        }

        [UnityTest]
        public IEnumerator TheSmallMap_DrawsTheCarvedOutline()
        {
            FloorMap map = FloorMap.Ensure();
            GeneratedLevel level = Build(1);
            yield return Frames(2);
            map.Tick();
            LevelLayout layout = level.Layout;
            Assert.That(map.Layout, Is.SameAs(layout));
            int carved = 0;
            foreach (LevelRoom room in layout.Rooms)
            {
                IReadOnlyList<CellRect> fills = map.FillsOf(room.Order);
                foreach (CellRect c in fills)
                    for (int x = c.X; x < c.Right; x++)
                        for (int y = c.Y; y < c.Top; y++)
                            Assert.That(room.IsOpen(x, y), "the map fills only open cells: " + (x, y) + " of " + room.Letter + room.Order);
                Assert.That(fills.Sum(c => c.Width * c.Height), Is.EqualTo(room.Rect.Width * room.Rect.Height - room.SolidCount), "every open cell filled once");
                if (room.SolidCount == 0)
                {
                    Assert.That(map.OutlineOf(room.Order), Is.Empty, "a plain room's frame as before");
                    continue;
                }
                carved++;
                Assert.That(map.OutlineOf(room.Order).Select(e => (e.Vertical, e.Line, e.From, e.To)),
                            Is.EquivalentTo(RoomShapes.Outline(room).Select(e => (e.Vertical, e.Line, e.From, e.To))), "the carved outline of " + room.Letter + room.Order);
            }
            Assert.That(carved, Is.GreaterThan(0));
        }

        [UnityTest]
        public IEnumerator SwitchOff_TheFloorsObjects_AreVersion12s()
        {
            GeneratedLevel off = Build(1, v => { v.Shapes = false; v.Passages = false; }); // work order 059: version 12 had no passages
            yield return Frames(2);
            Assert.That(off.Layout.Shapes, Is.False);
            Assert.That(off.Layout.Rooms.All(r => r.SolidCount == 0), "no carved cell");
            Assert.That(off.SolidCaps, Is.Empty);
            Assert.That(off.Floors, Is.EqualTo(off.Layout.Rooms.Count), "a floor box per room, as before");
            Assert.That(off.transform.Find("SolidCaps"), Is.Null);
            float cs = off.Layout.CellSize;
            for (int i = 0; i < off.Layout.Rooms.Count; i++)
            {
                LevelRoom room = off.Layout.Rooms[i];
                Vector3 s = off.FloorBoxes[i].transform.localScale;
                Assert.That(off.FloorBoxes[i].name, Is.EqualTo("Floor_" + room.Letter + room.Order));
                Assert.That(s.x, Is.EqualTo(room.Rect.Width * cs).Within(1e-4f), "the room's whole rectangle");
                Assert.That(s.z, Is.EqualTo(room.Rect.Height * cs).Within(1e-4f));
            }
            // The floor's objects (floors, walls, the kit's pieces; the props apart, as the large props of work order 052 stand in
            // place of some with the mood on), name for name and place for place, as a floor built from the same seed's layout of
            // version 12 (the generator with the shapes off is version 12's, EditMode's RoomShapesTests) gives them.
            LevelLayout plain = LevelGenerator.Generate(off.Layout.Seed, 1, new LevelValues { Shapes = false, Passages = false });
            Assert.That(plain.Describe(), Is.EqualTo(off.Layout.Describe()), "the same floor");
            string Objects(GeneratedLevel l) => string.Join(";", l.FloorBoxes.Concat(l.WallBoxes).Concat(l.KitPieces.Select(p => p.Object))
                .Select(g => g.name + " " + g.transform.position.ToString("F3") + " " + g.transform.localScale.ToString("F3")));
            string first = Objects(off);
            GeneratedLevel again = GeneratedLevel.Build(plain, new LevelValues { Shapes = false, Passages = false, RoomMood = false }, null, null);
            yield return null;
            Assert.That(Objects(again), Is.EqualTo(first), "the same objects");
            Assert.That(again.KitPlan.Describe(), Is.EqualTo(off.KitPlan.Describe()));
            Object.Destroy(again.gameObject);
            Assert.That(first, Does.Not.Contain("SolidCap"));
        }
    }
}
