using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Rooms that are not boxes (work order 051; Kristian's words of 2026-11-20: "Now we have very clear squares per room ...
    /// not walking into large office landscapes with monsters in them"; the research report d's proposal A). Over two thousand
    /// seeds of every floor number with the shapes on: every room keeps every rule of its outline, every door has its gate,
    /// the enemies, props, eye-catchers, drops' landing places and her start stand on open cells, a path on the open cells joins
    /// each door to the next and the middle to every enemy, the layout is the same seed's without the shapes, neighbouring rooms
    /// differ, and generation costs no more than about half as much again. With the shapes off five thousand floors are version
    /// 11's, field for field. And the validator reports each rule's fault, the walls and the maps follow the outline.
    /// Writes what it measures ("SHAPESTATS", "SHAPETIME").
    /// </summary>
    public class RoomShapesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        // ---------------- Two thousand seeds a floor ----------------

        /// <summary>A path on the room's open cells, side to side, from one cell to another.</summary>
        static bool Path(LevelRoom room, (int x, int y) from, (int x, int y) to)
        {
            if (!room.IsOpen(from.x, from.y) || !room.IsOpen(to.x, to.y)) return false;
            var seen = new HashSet<(int, int)> { from };
            var queue = new Queue<(int, int)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                (int x, int y) = queue.Dequeue();
                if ((x, y) == to) return true;
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    if (room.IsOpen(x + dx, y + dy) && seen.Add((x + dx, y + dy))) queue.Enqueue((x + dx, y + dy));
            }
            return false;
        }

        static (int, int) CellOf(LevelPoint p, double cs) => ((int)Math.Floor(p.X / cs), (int)Math.Floor(p.Y / cs));

        static (int, int) InnerOf(LevelRoom room, LevelDoor d)
        {
            Assert.That(RoomShapes.InnerCell(room, d, d.From, out int x, out int y, out _, out _), "the door is on the room");
            return (x, y);
        }

        /// <summary>The straight way between two points stays on the room's open cells, looked at every 10 cm.</summary>
        static bool OpenWay(LevelRoom room, LevelPoint a, LevelPoint b, double cs)
        {
            int n = Math.Max(1, (int)Math.Ceiling(a.DistanceTo(b) / 0.1));
            for (int i = 0; i <= n; i++)
            {
                var p = new LevelPoint(a.X + (b.X - a.X) * i / n, a.Y + (b.Y - a.Y) * i / n);
                (int x, int y) = CellOf(p, cs);
                if (!room.IsOpen(x, y)) return false;
            }
            return true;
        }

        // Work order 082 (review 6, finding 4): the sweep is the one place that checks the mood's pieces, the wear, the large
        // props, the enemies' paths, the doors' paths and the drops' landings against the carved cells: with the templates and
        // the passages off (051's drawn shapes, as they were) and, a thousand seeds a floor, with the defaults (the floor he
        // plays, templated rooms and passages). With the defaults the passages themselves are passed (PassagesTests' own), and
        // so are the gate (a passage's door has none), the comparison with the shapes off and the shares of the drawn shapes.
        [TestCase(1, false)]
        [TestCase(2, false)]
        [TestCase(3, false)]
        [TestCase(4, false)]
        [TestCase(5, false)]
        [TestCase(1, true)]
        [TestCase(2, true)]
        [TestCase(3, true)]
        [TestCase(4, true)]
        [TestCase(5, true)]
        public void TwoThousandSeeds_EveryRoomKeepsTheRules_AndEverythingStandsOnOpenCells(int floor, bool defaults)
        {
            var values = defaults ? new LevelValues() : new LevelValues { Templates = false, Passages = false }; // work order 058: 051's drawn shapes, as they were; 059: touching rooms, a gate in each
            var off = new LevelValues { Shapes = false, Templates = false, Passages = false };
            var shapes = new Dictionary<string, int>();
            int rooms = 0, fellBack = 0, bare = 0, redraws = 0, floorsAllDiffer = 0, pairs = 0, pairsDiffer = 0, drops = 0, pieces = 0, largeProps = 0, wallLarge = 0, earlier = 0;
            int templated = 0, enemyPaths = 0;
            int seeds = defaults ? 1000 : 2000;
            for (ulong s = 0; s < (ulong)seeds; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, values);
                string where = "seed " + s + " floor " + floor;
                double cs = level.CellSize;
                Assert.That(level.Shapes, Is.True, where);
                Assert.That(level.Templates && level.Passages, Is.EqualTo(defaults), where);
                List<string> faults = LevelValidator.Validate(level, values);
                Assert.That(faults, Is.Empty, where + "\n" + LevelMap.Draw(level));

                // A seed keeps its rooms' places, sizes, order and doors. Work order 054: with the casters on, the plain rooms'
                // contents may find no place on an attempt the carved ones fit (seed 1513 of floor 5), and the floor with the
                // shapes off is the next attempt's; never the other way round (an attempt fails only where it failed without them).
                if (!defaults)
                {
                    LevelLayout plain = LevelGenerator.Generate(s, floor, off);
                    Assert.That(level.Attempt, Is.LessThanOrEqualTo(plain.Attempt), "no later attempt with the shapes on, " + where);
                    if (level.Attempt == plain.Attempt)
                        Assert.That(level.DescribeLayout(), Is.EqualTo(plain.DescribeLayout()), "the same layout with the shapes on and off, " + where);
                    else earlier++;
                }

                RoomMoodPlan mood = RoomMoodPlan.Make(level, values);
                bool allDiffer = true;
                for (int i = 0; i < level.Rooms.Count; i++)
                {
                    LevelRoom room = level.Rooms[i];
                    if (room.Kind == RoomKind.Passage) continue; // work order 082: a passage's own rules are PassagesTests'
                    if (room.Template != null) templated++;
                    string name = room.Letter + room.Order.ToString(Inv) + " " + where;
                    rooms++;
                    string key = (room.Elite ? "elite" : room.Kind.ToString().ToLowerInvariant()) + " " + RoomShapes.Word(room.Shape);
                    shapes[key] = shapes.TryGetValue(key, out int had) ? had + 1 : 1;
                    if (room.ShapeFellBack) fellBack++;
                    if (room.Shape == RoomShape.None) bare++;
                    redraws += room.ShapeRedraws;
                    if (i > 0)
                    {
                        pairs++;
                        if (level.Rooms[i - 1].Shape != room.Shape) pairsDiffer++;
                        else allDiffer = false;
                    }

                    // Every rule of its outline, and its gate.
                    var broken = new List<string>();
                    Assert.That(RoomShapes.Faults(level, room, values, broken), Is.EqualTo(0), name + ": " + string.Join("; ", broken));
                    Assert.That(room.Shape, Is.Not.EqualTo(RoomShape.None), "a shape drawn: " + name);
                    LevelDoor entry = LevelGeometry.EntryDoor(level, room);
                    if (entry != null && !level.Passages) Assert.That(RoomShapes.HasGate(room, entry), "a gate inside the door in: " + name);

                    // Her start, the enemies, the props on open cells, clear of the carved walls.
                    if (room.PlayerStart.HasValue)
                    {
                        Assert.That(level.RoomAt(room.PlayerStart.Value.X, room.PlayerStart.Value.Y), Is.SameAs(room), "her start on an open cell: " + name);
                        Assert.That(RoomShapes.DistanceToSolid(room, room.PlayerStart.Value, cs), Is.GreaterThanOrEqualTo(values.WallClearance - 1e-9));
                    }
                    for (int k = 0; k < room.EnemyPoints.Count; k++)
                        foreach (LevelPoint m in room.PlacesAt(k).Concat(new[] { room.EnemyPoints[k] }))
                        {
                            Assert.That(level.RoomAt(m.X, m.Y), Is.SameAs(room), "an enemy on an open cell: " + name + " at " + m);
                            Assert.That(RoomShapes.DistanceToSolid(room, m, cs), Is.GreaterThanOrEqualTo(values.WallClearance - 1e-9), "clear of a carved wall: " + name);
                            Assert.That(Path(room, CellOf(room.Middle(cs), cs), CellOf(m, cs)), "a path on the open cells from the middle to the enemy at " + m + ": " + name);
                            enemyPaths++;
                        }
                    foreach (LevelProp p in room.Props)
                    {
                        double d = RoomShapes.DistanceToSolid(room, p.Footprint, cs);
                        Assert.That(d, Is.GreaterThan(1e-9), "a prop off the carved cells: " + p + " " + name);
                        if (p.Place != PropPlace.Wall) Assert.That(d, Is.GreaterThanOrEqualTo(values.PropRules.WallClearance - 1e-9), p + " " + name);
                    }

                    // The eye-catchers (work order 044) never on a carved cell.
                    MoodRoom mr = mood.Rooms.First(r => r.Room == room); // work order 059: a passage has no plan, so the plan is not by order
                    foreach (CompositionPiece c in mr.Pieces)
                    {
                        pieces++;
                        if (c.Kind == CompositionKind.Carpet)
                        {
                            foreach ((int x, int y) in room.SolidList)
                            {
                                var cell = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5 };
                                Assert.That(cell.DistanceTo(c.At, c.To), Is.GreaterThanOrEqualTo(c.Width * 0.5), "the carpet off the carved cells: " + name);
                            }
                        }
                        else Assert.That(RoomMoodPlan.OnCarved(room, c, cs), Is.False, c.Kind + " off the carved cells: " + name);
                    }
                    foreach (WearPiece w in mr.Wear) Assert.That(RoomShapes.DistanceToSolid(room, w.At, cs), Is.GreaterThan(w.Reach), "wear off the carved cells: " + name);
                    if (mr.LargePropPlace.HasValue) Assert.That(level.RoomAt(mr.LargePropPlace.Value.X, mr.LargePropPlace.Value.Y), Is.SameAs(room));
                    // The large props (work order 052) off the carved cells, and the ones at a wall against a real face of the room.
                    List<WallFace> faces = RoomShapes.WallFaces(level, room);
                    foreach (LevelProp p in mr.LargeProps)
                    {
                        largeProps++;
                        Assert.That(level.RoomAt(p.Position.X, p.Position.Y), Is.SameAs(room), "a large prop on an open cell: " + p + " " + name);
                        Assert.That(RoomShapes.DistanceToSolid(room, p.Footprint, cs), Is.GreaterThan(1e-9), "a large prop off the carved cells: " + p + " " + name);
                        if (p.Place != PropPlace.Wall || !PropSize.Of(p.Model).WallPiece) continue;
                        wallLarge++;
                        WallFace near = RoomShapes.NearestFace(room, p.Footprint, cs, out _);
                        p.Footprint.Extent(out double minX, out double minY, out double maxX, out double maxY);
                        double lo = near.Vertical ? minY : minX, hi = near.Vertical ? maxY : maxX;
                        Assert.That(faces.Any(f => f.Side == near.Side && f.Vertical == near.Vertical && f.Line == near.Line && f.From * cs <= lo + 1e-6 && hi <= f.To * cs + 1e-6),
                                    "a large prop at a wall stands against a face of the room's wall: " + p + " " + name);
                    }

                    // A path from each door to the next.
                    List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();
                    for (int a = 0; a + 1 < doors.Count; a++)
                        Assert.That(Path(room, InnerOf(room, doors[a]), InnerOf(room, doors[a + 1])), "a path from door to door: " + name);

                    // Where a reward drop lands, from where each enemy fell, asked of the layout as the game asks the NavMesh.
                    if (room.EnemyPoints.Count > 0)
                    {
                        LevelPoint middle = room.Middle(cs);
                        bool Standable(GroundPoint g)
                        {
                            var p = new LevelPoint(g.X, g.Z);
                            if (level.RoomAt(p.X, p.Y) != room) return false;
                            if (RoomShapes.NearSolid(room, p, cs, 0.5 + values.WallThickness * 0.5)) return false;
                            if (LevelGeometry.DistanceToWalls(p, room.Rect, cs) < 0.5 + values.WallThickness * 0.5) return false;
                            return room.Props.All(q => q.Footprint.DistanceTo(p) >= 0.5);
                        }
                        foreach (LevelPoint fell in room.EnemyPoints)
                        {
                            GroundPoint at = RewardDropRules.Landing(new GroundPoint((float)fell.X, (float)fell.Y), new GroundPoint((float)middle.X, (float)middle.Y),
                                                                     Standable, (g, h) => OpenWay(room, new LevelPoint(g.X, g.Z), new LevelPoint(h.X, h.Z), cs), new RewardDropValues());
                            Assert.That(level.RoomAt(at.X, at.Z), Is.SameAs(room), "a drop lands on an open cell: " + name);
                            drops++;
                        }
                    }
                }
                if (allDiffer) floorsAllDiffer++;
            }
            string stats = string.Format(Inv, "SHAPESTATS floor {0} seeds 0-{1}: rooms {2}, fell back to the gate alone {3} ({4:P2}), plain rectangles {5}, redraws {6}; " +
                                              "neighbouring rooms of different shapes {7} of {8} ({9:P1}), floors whose every neighbour differs {10} ({11:P1}); drops landed {12}, eye-catcher pieces {13}, large props {15} ({16} at a wall); " +
                                              "shapes: {14}",
                                         floor, seeds - 1, rooms, fellBack, fellBack / (double)rooms, bare, redraws, pairsDiffer, pairs, pairsDiffer / (double)pairs,
                                         floorsAllDiffer, floorsAllDiffer / (double)seeds, drops, pieces,
                                         string.Join(", ", shapes.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)), largeProps, wallLarge);
            TestContext.WriteLine(stats + "; floors on an earlier attempt than with the shapes off " + earlier + "; templated rooms " + templated + ", enemy paths " + enemyPaths); // work order 054
            Assert.That(drops, Is.GreaterThan(seeds), "drops asked where they land");
            Assert.That(enemyPaths, Is.GreaterThan(seeds), "paths to the enemies asked");
            if (defaults)
            {
                Assert.That(templated, Is.GreaterThan(seeds), "templated rooms among them");
                return;
            }
            Assert.That(bare, Is.EqualTo(0), "no room kept its plain rectangle");
            Assert.That(fellBack / (double)rooms, Is.LessThan(0.01), "fewer than one room in a hundred fell back to its gate alone");
            Assert.That(floorsAllDiffer / (double)seeds, Is.GreaterThanOrEqualTo(0.95), "on 95 % of floors no two rooms in a row have the same shape");
        }

        // ---------------- What it costs ----------------

        [Test]
        public void Generation_CostsAtMostHalfAsMuchAgain_ByTheMedian()
        {
            var on = new LevelValues { Templates = false, Passages = false }; // work order 058: what 051's shapes cost alone (059: without the passages)
            var off = new LevelValues { Shapes = false, Templates = false, Passages = false };
            LevelGenerator.Generate(424242UL, 3, on);
            LevelGenerator.Generate(424242UL, 3, off);
            var tOn = new List<double>();
            var tOff = new List<double>();
            var timer = new Stopwatch();
            // In turns, so a busy moment of the machine falls on both alike.
            for (ulong s = 0; s < 600; s++)
            {
                int floor = 1 + (int)(s % 5);
                timer.Restart();
                LevelGenerator.Generate(s, floor, off);
                timer.Stop();
                tOff.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                LevelGenerator.Generate(s, floor, on);
                timer.Stop();
                tOn.Add(timer.Elapsed.TotalMilliseconds);
            }
            tOn.Sort();
            tOff.Sort();
            double mOn = tOn[tOn.Count / 2], mOff = tOff[tOff.Count / 2];
            TestContext.WriteLine(string.Format(Inv, "SHAPETIME median of 600 floors: shapes off {0:0.000} ms, on {1:0.000} ms, {2:0.00} times", mOff, mOn, mOn / mOff));
            Assert.That(mOn, Is.LessThanOrEqualTo(mOff * 1.5), "the median with the shapes at most 1.5 times without");
        }

        // ---------------- The switch off ----------------

        /// <summary>
        /// Five thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the shapes off, every field of them
        /// folded into one number (on its own branch, with four combat rooms, version 11's; merged with work order 046,
        /// version 12's). With them off the floors are version 11's, field for field: on .NET 9 the number from these
        /// floors with the shapes off and from version 11's generator (the code before work order 051) was the same,
        /// 0xC2132F238AACE2CE, and their 5000 descriptions the same to the byte; Unity's runtime folds its own doubles to the number
        /// pinned here (taken from this test's first run in the Editor's runtime, on the code with the shapes off).
        /// </summary>
        [Test]
        public void SwitchOff_FiveThousandFloors_AreVersion12s_FieldForField()
        {
            var off = new LevelValues { Shapes = false, Casters = false, Templates = false, Passages = false, ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false }; // work order 103: nor exit rooms of their own; 054: version 12 had the casters off; 058: nor templates; 059: nor passages; 087: nor the act's plan
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 5000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, off);
                Assert.That(level.Shapes, Is.False);
                Assert.That(level.Rooms.All(r => r.SolidCount == 0 && r.Shape == RoomShape.None), "no carved cell, seed " + level.SeedText);
                Assert.That(level.Describe(), Does.Not.Contain("carved").And.Not.Contain(" shape "));
                h = FloorPrint.Of(level, h);
            }
            // Work order 051 merged with 046 (version 13): with five combat rooms the floors are version 12's, and their number is
            // one only Unity's runtime gives (the .NET one differs: it folds its doubles otherwise). Pinned from the first
            // Unity run on this code (EditMode on runner/night-d, 2026-11-21; a second copy of the project found the same
            // number the same morning). With the pin at 0 the test fails and says the number it found.
            const ulong pinned = 0x70DF85B31E0B9FF2UL;
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("SHAPESOFFPRINT " + found);
            if (pinned == 0UL) Assert.Fail("the pin is to be taken from the first Unity run: this run found " + found + " for the five thousand floors of version 12 (the shapes off)");
            Assert.That(h, Is.EqualTo(pinned), "the five thousand floors of version 12, found " + found);
        }

        // ---------------- The rules, one by one ----------------

        /// <summary>A floor of two rooms side by side, the second 7 x 7 entered through a door in its west wall at its fourth cell.</summary>
        static LevelLayout TwoRooms(out LevelRoom room)
        {
            var level = new LevelLayout { Seed = 1, Version = LevelGenerator.Version, GridCells = 48, CellSize = 4f, Attempt = 1, Floor = 1, Shapes = true };
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(10, 10, 4, 7), Order = 0 });
            room = new LevelRoom { Kind = RoomKind.Combat, Rect = new CellRect(14, 10, 7, 7), Order = 1, Shape = RoomShape.Plain };
            level.Rooms.Add(room);
            level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = 14, From = 13, Width = 1 });
            RoomShapes.Gate(level, room);
            return level;
        }

        static List<string> FaultsOf(LevelLayout level, LevelRoom room)
        {
            var list = new List<string>();
            RoomShapes.Faults(level, room, new LevelValues(), list);
            return list;
        }

        [Test]
        public void TheGate_TwoCellsBesideTheDoorInsideTheRoom_AndARoomWithoutItIsAFault()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            Assert.That(room.IsSolid(14, 12) && room.IsSolid(14, 14), "either side of the door's cell inside");
            Assert.That(room.IsOpen(14, 13) && room.IsOpen(15, 13), "the slot and the cell behind it open");
            Assert.That(room.SolidCount, Is.EqualTo(2));
            Assert.That(FaultsOf(level, room), Is.Empty);
            Assert.That(level.RoomAtCell(14, 12), Is.Null, "a carved cell is no room's");
            Assert.That(level.RoomRectAtCell(14, 12), Is.SameAs(room));
            room.ClearSolid();
            Assert.That(FaultsOf(level, room), Has.Some.Contains("no gate"));
            Assert.That(LevelValidator.Validate(level, new LevelValues()), Has.Some.Contains("no gate"));
        }

        [Test]
        public void TheGate_NextToACorner_FillsTheCellLeftAlone()
        {
            var level = new LevelLayout { Seed = 1, Version = LevelGenerator.Version, GridCells = 48, CellSize = 4f, Attempt = 1, Floor = 1, Shapes = true };
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(10, 10, 4, 6), Order = 0 });
            var room = new LevelRoom { Kind = RoomKind.Combat, Rect = new CellRect(14, 10, 5, 5), Order = 1, Shape = RoomShape.Plain };
            level.Rooms.Add(room);
            level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = 14, From = 12, Width = 1 });
            RoomShapes.Gate(level, room);
            Assert.That(room.SolidList, Is.EquivalentTo(new[] { (14, 10), (14, 11), (14, 13), (14, 14) }), "the gate and the corners beside it: no one-cell pocket");
            Assert.That(FaultsOf(level, room), Is.Empty);
        }

        [Test]
        public void EachRule_IsReported()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            // A dead-end slot: an open cell walled on three sides.
            room.SetSolid(18, 16, true);
            room.SetSolid(20, 15, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("dead-end slot"));
            room.SetSolid(18, 16, false);
            room.SetSolid(20, 15, false);
            // Two solid cells touching only at a corner.
            room.SetSolid(17, 11, true);
            room.SetSolid(18, 12, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("touching only at the corner"));
            room.SetSolid(17, 11, false);
            room.SetSolid(18, 12, false);
            // The middle.
            room.SetSolid(17, 13, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("open square"));
            room.SetSolid(17, 13, false);
            // Behind a door.
            room.SetSolid(15, 13, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("straight in behind door"));
            room.SetSolid(15, 13, false);
            // One area: a cell walled off in a corner by a wall of solid cells.
            foreach ((int x, int y) in new[] { (19, 16), (19, 15), (20, 15) }) room.SetSolid(x, y, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("more than one area").Or.Some.Contains("dead-end slot"));
            foreach ((int x, int y) in new[] { (19, 16), (19, 15), (20, 15) }) room.SetSolid(x, y, false);
            // The share.
            for (int x = 15; x <= 20; x++) room.SetSolid(x, 10, true);
            for (int x = 15; x <= 20; x++) room.SetSolid(x, 16, true);
            Assert.That(FaultsOf(level, room), Has.Some.Contains("cells solid, more than"));
            for (int x = 15; x <= 20; x++) room.SetSolid(x, 10, false);
            for (int x = 15; x <= 20; x++) room.SetSolid(x, 16, false);
            Assert.That(FaultsOf(level, room), Is.Empty, "and back to its gate alone, nothing");
        }

        [Test]
        public void AnEnemy_OrAProp_OnACarvedCell_IsAFault()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            var v = new LevelValues();
            var onSolid = new LevelPoint(14.5 * 4, 12.5 * 4);
            Assert.That(LevelGeometry.EnemyPlaceFault(level, room, onSolid, null, level.DoorsOf(1).ToList(), v), Does.Contain("solid cell"));
            var beside = new LevelPoint(15 * 4 + 0.5, 12.5 * 4); // half a metre off the gate cell's face
            Assert.That(LevelGeometry.EnemyPlaceFault(level, room, beside, null, level.DoorsOf(1).ToList(), v), Does.Contain("carved wall"));
            LevelProp prop = LevelProp.Make(PropModel.Pillar, new LevelPoint(15 * 4 + 0.3, 12.5 * 4), 0, PropPlace.Floor);
            Assert.That(PropRules.Fault(level, room, prop, room.Props, v.PropRules), Does.Contain("carved cell"));
            LevelProp near = LevelProp.Make(PropModel.Pillar, new LevelPoint(15 * 4 + 0.9, 12.5 * 4), 0, PropPlace.Floor);
            Assert.That(PropRules.Fault(level, room, near, room.Props, v.PropRules), Does.Contain("nearer a wall"));
        }

        // ---------------- The walls, the kit and the maps follow ----------------

        [Test]
        public void TheKitsPlan_WallsACarvedCellsOpenSides_WithPiersAtTheirEnds()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            room.SetSolid(17, 13, false);
            room.SetSolid(16, 12, true); // a pier of its own in the open floor
            CryptKitPlan plan = CryptKitPlan.Make(level, new CryptKitValues());
            bool Section(bool vertical, int line, int cell) => plan.Sections.Any(s => s.Vertical == vertical && s.Line == line && s.Cell == cell);
            Assert.That(Section(true, 16, 12) && Section(true, 17, 12), "its west and east sides");
            Assert.That(Section(false, 12, 16) && Section(false, 13, 16), "its south and north sides");
            Assert.That(plan.Sections.First(s => !s.Vertical && s.Line == 13 && s.Cell == 16).Low, "its north side low, the room to its north");
            Assert.That(plan.Sections.First(s => !s.Vertical && s.Line == 12 && s.Cell == 16).Low, Is.False, "its south side full");
            foreach ((int x, int y) in new[] { (16, 12), (17, 12), (16, 13), (17, 13) })
                Assert.That(plan.Points.Any(p => p.X == x && p.Y == y && p.Pier), "a pier at its corner " + (x, y));
            // The gate's cells: walled toward the slot and the room.
            Assert.That(Section(false, 13, 14) && Section(false, 14, 14), "the slot's sides");
        }

        [Test]
        public void TheTextMap_DrawsTheCarvedOutline()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            room.SetSolid(17, 13, false);
            room.SetSolid(16, 12, true);
            string map = LevelMap.Draw(level);
            string[] lines = map.Split('\n');
            CellRect box = level.Bounds();
            int rows = 2 * box.Height + 1;
            char At(int gx, int gy) { string l = lines[1 + rows - 1 - gy]; return gx < l.Length ? l[gx] : ' '; }
            // Cell (16, 12): its west and east walls, and its middle blank.
            int cx = 2 * (16 - box.X) + 1, cy = 2 * (12 - box.Y) + 1;
            Assert.That(At(cx - 1, cy), Is.EqualTo('|'), map);
            Assert.That(At(cx + 1, cy), Is.EqualTo('|'), map);
            Assert.That(At(cx, cy - 1), Is.EqualTo('-'), map);
            Assert.That(At(cx, cy + 1), Is.EqualTo('-'), map);
            Assert.That(At(cx, cy), Is.EqualTo(' '), map);
        }

        [Test]
        public void TheOpenRectangles_CoverEveryOpenCellOnce_AndTheOutlineIsItsEdges()
        {
            for (ulong s = 0; s < 200; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, 1 + (int)(s % 5));
                foreach (LevelRoom room in level.Rooms)
                {
                    var cover = new Dictionary<(int, int), int>();
                    foreach (CellRect c in RoomShapes.OpenRects(room))
                        for (int x = c.X; x < c.Right; x++)
                            for (int y = c.Y; y < c.Top; y++) cover[(x, y)] = cover.TryGetValue((x, y), out int n) ? n + 1 : 1;
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                            Assert.That(cover.TryGetValue((x, y), out int n) ? n : 0, Is.EqualTo(room.IsOpen(x, y) ? 1 : 0), "cell " + (x, y) + " of " + room.Letter + room.Order);
                    int edges = 0;
                    for (int x = room.Rect.X - 1; x <= room.Rect.Right; x++)
                        for (int y = room.Rect.Y - 1; y <= room.Rect.Top; y++)
                        {
                            if (room.IsOpen(x, y) != room.IsOpen(x + 1, y)) edges++;
                            if (room.IsOpen(x, y) != room.IsOpen(x, y + 1)) edges++;
                        }
                    Assert.That(RoomShapes.Outline(room).Sum(e => e.To - e.From), Is.EqualTo(edges), "the outline's length, " + room.Letter + room.Order);
                }
            }
        }

        [Test]
        public void TheDoorSigns_HangOverOpenFloor_InTheRoomBeforeTheDoor()
        {
            var signs = new DoorSignValues();
            for (ulong s = 0; s < 500; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, 1 + (int)(s % 5));
                double cs = level.CellSize;
                foreach (LevelDoor door in level.Doors)
                {
                    LevelRoom before = level.Rooms[DoorSignRules.Before(door)];
                    door.Opening(cs, out LevelPoint a, out LevelPoint b);
                    var middle = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                    LevelPoint m = before.Middle(cs);
                    double ix = door.Vertical ? Math.Sign(m.X - middle.X) : 0, iy = door.Vertical ? 0 : Math.Sign(m.Y - middle.Y);
                    foreach (double out_ in new double[] { signs.OutFromWall, signs.LightOut })
                        Assert.That(level.RoomAt(middle.X + ix * out_, middle.Y + iy * out_), Is.SameAs(before), "the sign and its light over open floor, seed " + s);
                }
            }
        }

        [Test]
        public void WallFaces_GiveTheCarvedFaces_ForALaterProp()
        {
            LevelLayout level = TwoRooms(out LevelRoom room);
            room.SetSolid(17, 13, false);
            room.SetSolid(16, 12, true);
            List<WallFace> faces = RoomShapes.WallFaces(level, room);
            Assert.That(faces.Count(f => !f.Carved), Is.EqualTo(5), "the four sides, the west one broken by the door");
            Assert.That(faces.Where(f => f.Carved).Select(f => (f.Side, f.Vertical, f.Line, f.From)),
                        Is.SupersetOf(new[] { (LevelGenerator.East, true, 16, 12), (LevelGenerator.West, true, 17, 12), (LevelGenerator.North, false, 12, 16), (LevelGenerator.South, false, 13, 16) }));
            // A statue with its back to the pier's east face keeps every rule, as at a wall.
            LevelProp statue = LevelProp.Make(PropModel.Statue, new LevelPoint(17 * 4 + 0.1 + 0.55, 12.5 * 4), 90, PropPlace.Wall);
            Assert.That(PropRules.Fault(level, room, statue, room.Props, new LevelValues().PropRules), Is.Null);
            WallFace near = RoomShapes.NearestFace(room, statue.Footprint, 4, out double back);
            Assert.That(near.Carved && near.Side == LevelGenerator.West && near.Line == 17, "the pier's face behind it");
            Assert.That(back, Is.EqualTo(0.1).Within(1e-6));
        }
    }

    /// <summary>Every field of a floor folded into one number, FNV-1a over 64 bits (work order 051: to compare thousands of floors with an older generator's).</summary>
    static class FloorPrint
    {
        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        static ulong D(double d) => (ulong)BitConverter.DoubleToInt64Bits(d);

        public static ulong Of(LevelLayout l, ulong h)
        {
            h = Mix(h, l.Seed); h = Mix(h, (ulong)l.Floor); h = Mix(h, (ulong)l.Attempt); h = Mix(h, (ulong)l.GridCells); h = Mix(h, l.InALine ? 1UL : 0UL);
            foreach (LevelRoom r in l.Rooms)
            {
                h = Mix(h, (ulong)r.Kind); h = Mix(h, (ulong)r.Order); h = Mix(h, r.Elite ? 1UL : 0UL); h = Mix(h, (ulong)r.Theme);
                h = Mix(h, (ulong)r.Rect.X); h = Mix(h, (ulong)r.Rect.Y); h = Mix(h, (ulong)r.Rect.Width); h = Mix(h, (ulong)r.Rect.Height);
                if (r.PlayerStart.HasValue) { h = Mix(h, D(r.PlayerStart.Value.X)); h = Mix(h, D(r.PlayerStart.Value.Y)); }
                for (int i = 0; i < r.EnemyPoints.Count; i++)
                {
                    h = Mix(h, D(r.EnemyPoints[i].X)); h = Mix(h, D(r.EnemyPoints[i].Y));
                    EnemyGroup g = r.GroupAt(i);
                    h = Mix(h, (ulong)g.Kind); h = Mix(h, (ulong)g.Count); h = Mix(h, g.Boss ? 1UL : 0UL); h = Mix(h, g.Elite ? 1UL : 0UL);
                    foreach (LevelPoint m in r.PlacesAt(i)) { h = Mix(h, D(m.X)); h = Mix(h, D(m.Y)); }
                }
                foreach (LevelProp p in r.Props) { h = Mix(h, (ulong)p.Model); h = Mix(h, D(p.Position.X)); h = Mix(h, D(p.Position.Y)); h = Mix(h, (ulong)p.Rotation); h = Mix(h, (ulong)p.Place); }
            }
            foreach (LevelDoor d in l.Doors) { h = Mix(h, (ulong)d.RoomA); h = Mix(h, (ulong)d.RoomB); h = Mix(h, d.Vertical ? 1UL : 0UL); h = Mix(h, (ulong)d.Line); h = Mix(h, (ulong)d.From); h = Mix(h, (ulong)d.Width); }
            return h;
        }
    }
}
