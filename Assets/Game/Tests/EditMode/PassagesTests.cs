using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Passages between the rooms (work order 059; Kristian, 2026-11-20: "bedre bruk av mappet ... litt dungen crawler følelse
    /// trenger vi, ikke inn i store kontorlandskap med monstre i", and his choice of 2026-11-21, "Ganger mellom rommene"; the
    /// research report d's proposal C; decision 0016). Over four hundred seeds of every floor number with the defaults: every floor
    /// valid, its passages' rules and the walk over cells kept, every passage holding nothing with its middle on its path, no gate
    /// where a passage comes in, every other room keeping work order 051's rules, the enemies of every room and the templates of
    /// the same attempt those of the floor with the passages off, room for room. The cost against the switch off. With the switch
    /// off the floors are version 15's, field for field. The streams. The validator's reports, one by one. The templates with a
    /// passage at each end, every case. The signs, the map's rules, the room numbers, the mood's plan and the text map. Writes
    /// what it measures ("PASSAGESTATS", "PASSAGETIME", "PASSAGESOFFPRINT", "PASSAGETEMPLATES", "PASSAGEWALK", "PASSAGEMAP").
    /// </summary>
    public class PassagesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly LevelValues Off = new LevelValues { Passages = false };
        static readonly LevelValues Version15 = new LevelValues { Passages = false, MoreTemplates = false, ActPlan = false, UsableRooms = false }; // work order 072; 087: nor the act's plan

        static string Groups(LevelRoom r) => string.Join(",", r.EnemyGroups.Select(g => g.ToString()));

        static bool OnPath(LevelRoom passage, LevelPoint p, double cs)
        {
            for (int y = passage.Rect.Y; y < passage.Rect.Top; y++)
                for (int x = passage.Rect.X; x < passage.Rect.Right; x++)
                    if (passage.IsOpen(x, y) && p.X >= x * cs - 1e-9 && p.X <= (x + 1) * cs + 1e-9 && p.Y >= y * cs - 1e-9 && p.Y <= (y + 1) * cs + 1e-9)
                        return true;
            return false;
        }

        static int Bends(List<(int x, int y)> path)
        {
            int bends = 0;
            for (int i = 1; i + 1 < path.Count; i++)
                if (path[i].x - path[i - 1].x != path[i + 1].x - path[i].x || path[i].y - path[i - 1].y != path[i + 1].y - path[i].y) bends++;
            return bends;
        }

        // ---------------- A thousand seeds ----------------

        // Work order 059: four hundred seeds of each floor number (the order asked for two thousand; the producer's word of
        // 2026-11-21 kept the sweeps as small as still proves the rule: the generator's validator holds every floor to every
        // passage rule and the walk over cells as it makes it, in the game too; a thousand a floor took 55 s each in the Editor,
        // and the report's numbers are a thousand a floor's, run on .NET). The floors with the passages off, for the enemies and
        // the templates, are the first 200.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FourHundredSeeds_EveryFloorWithPassages_IsValid_ItsPassagesHoldNothing_AndItsRoomsAreThoseOfTheSwitchOff(int floor)
        {
            var values = new LevelValues();
            PassageValues pv = values.PassageRules;
            const int seeds = 400, compared = 200;
            var depths = new Dictionary<int, int>();
            int passages = 0, jogged = 0, mostAttempts = 0, sameAttempt = 0, templatesSame = 0, templatedRooms = 0, fellBack = 0, entered = 0;
            long pathCells = 0;
            int widest = 0, tallest = 0;
            string largest = "";
            for (ulong s = 0; s < seeds; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, values);
                string where = "seed " + s + " floor " + floor;
                Assert.That(level.Passages && level.InALine, where);
                Assert.That(level.GridCells, Is.EqualTo(pv.GridCells));
                List<string> faults = LevelValidator.Validate(level, values);
                Assert.That(faults, Is.Empty, where + "\n" + LevelMap.Draw(level));
                mostAttempts = Math.Max(mostAttempts, level.Attempt);
                CellRect box = level.Bounds();
                if (box.Width * box.Height > widest * tallest)
                {
                    widest = box.Width;
                    tallest = box.Height;
                    largest = box.Width + " x " + box.Height + " cells (" + where + ")";
                }
                Assert.That(box.X >= 0 && box.Y >= 0 && box.Right <= pv.GridCells && box.Top <= pv.GridCells, "inside the grid, " + where);
                Assert.That(Passages.WalkFault(level), Is.Null, where);

                double cs = level.CellSize;
                foreach (LevelRoom r in level.Rooms)
                {
                    string name = r.Letter + r.Order.ToString(Inv) + " " + where;
                    if (r.Kind == RoomKind.Passage)
                    {
                        passages++;
                        Assert.That(r.EnemyPoints.Count + r.Props.Count, Is.EqualTo(0), "a passage holds nothing: " + name);
                        Assert.That(r.Theme == RoomTheme.None && r.Shape == RoomShape.None && !r.Elite && !r.PlayerStart.HasValue, name);
                        Assert.That(OnPath(r, r.Middle(cs), cs), "the middle of its rectangle on or at the edge of its path: " + name + "\n" + LevelMap.Draw(level));
                        int depth = Passages.DepthOf(level, r);
                        depths[depth] = depths.TryGetValue(depth, out int had) ? had + 1 : 1;
                        List<(int x, int y)> path = Passages.PathOf(level, r);
                        if (Bends(path) > 0) jogged++;
                        pathCells += path.Count;
                        continue;
                    }
                    // Every room that is not a passage keeps work order 051's rules, and has no gate where a passage comes in.
                    var broken = new List<string>();
                    Assert.That(RoomShapes.Faults(level, r, values, broken), Is.EqualTo(0), name + ": " + string.Join("; ", broken));
                    LevelDoor entry = LevelGeometry.EntryDoor(level, r);
                    if (entry != null)
                    {
                        Assert.That(level.Rooms[entry.Other(r.Order)].Kind, Is.EqualTo(RoomKind.Passage), "entered from a passage: " + name);
                        Assert.That(RoomShapes.HasGate(r, entry), Is.False, "no gate where a passage comes in: " + name);
                        Assert.That(RoomShapes.GateCells(level, r), Is.Empty, name);
                        entered++;
                    }
                    if (r.Template != null)
                    {
                        templatedRooms++;
                        if (r.Shape != RoomShape.Template) fellBack++;
                    }
                }

                if (s >= compared) continue;
                // The same seed with the passages off: the same enemies in every room, room for room; the same templates when the
                // floors came of the same attempt (the templates are drawn on the attempt's stream, as with the passages off).
                LevelLayout plain = LevelGenerator.Generate(s, floor, Off);
                List<LevelRoom> rooms = level.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList();
                Assert.That(rooms.Select(r => r.Kind), Is.EqualTo(plain.Rooms.Select(r => r.Kind)), "the row, " + where);
                for (int i = 0; i < rooms.Count; i++)
                {
                    Assert.That(Groups(rooms[i]), Is.EqualTo(Groups(plain.Rooms[i])), "the enemies of room " + i + ", " + where);
                    Assert.That(rooms[i].Elite, Is.EqualTo(plain.Rooms[i].Elite), where);
                }
                if (level.Attempt != plain.Attempt) continue;
                sameAttempt++;
                bool same = true;
                for (int i = 0; i < rooms.Count; i++)
                    if (rooms[i].Template?.Template != plain.Rooms[i].Template?.Template) same = false;
                Assert.That(same, "the same templates room for room on the same attempt, " + where);
                templatesSame++;
            }
            string stats = string.Format(Inv,
                "PASSAGESTATS floor {0} seeds 0-{1}: passages {2}, by depth {3}; jogged {4} ({5:P1}); passage {6:0.0} m a floor ({7:0.0} cells); largest floor {8}; " +
                "most attempts {9}; rooms entered from a passage {10}, none with a gate; templated rooms {11}, stamped as drawn {12:P2}; " +
                "of the first {13} seeds {14} on the same attempt as with the passages off, the same templates on {15}",
                floor, seeds - 1, passages, string.Join(", ", depths.OrderBy(k => k.Key).Select(k => k.Key + " cells " + (k.Value / (double)passages).ToString("P1", Inv))),
                jogged, jogged / (double)passages, pathCells * 4.0 / seeds, pathCells / (double)seeds, largest, mostAttempts, entered,
                templatedRooms, 1 - fellBack / (double)Math.Max(1, templatedRooms), compared, sameAttempt, templatesSame);
            TestContext.WriteLine(stats);
            Assert.That(widest, Is.LessThanOrEqualTo(pv.GridCells));
            Assert.That(mostAttempts, Is.LessThanOrEqualTo(values.Attempts / 8), "well under the attempts allowed");
            Assert.That(sameAttempt, Is.GreaterThanOrEqualTo(compared * 9 / 10), "nearly every seed on the same attempt either way");
            Assert.That(depths.Keys.Min(), Is.GreaterThanOrEqualTo(pv.Fewest));
            Assert.That(depths.Keys.Max(), Is.LessThanOrEqualTo(pv.Most));
            Assert.That(jogged, Is.GreaterThan(0), "some passages jog");
        }

        // ---------------- What it costs ----------------

        [Test]
        public void Generation_CostsAtMostTwiceThePassagesOff_ByTheMedian()
        {
            var on = new LevelValues();
            LevelGenerator.Generate(424242UL, 3, on);
            LevelGenerator.Generate(424242UL, 3, Off);
            var tOn = new List<double>();
            var tOff = new List<double>();
            var timer = new Stopwatch();
            // In turns, so a busy moment of the machine falls on both alike.
            for (ulong s = 0; s < 250; s++)
            {
                int floor = 1 + (int)(s % 5);
                timer.Restart();
                LevelGenerator.Generate(s, floor, Off);
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
            TestContext.WriteLine(string.Format(Inv, "PASSAGETIME median of 250 floors: passages off {0:0.000} ms, on {1:0.000} ms, {2:0.00} times", mOff, mOn, mOn / mOff));
            Assert.That(mOn, Is.LessThanOrEqualTo(mOff * 2), "the median with the passages at most twice without");
        }

        // ---------------- The switch off ----------------

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>
        /// Two thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the passages off, every field folded into one
        /// number (<see cref="FloorPrint"/>, each room's shape and solid cells, and its template). On .NET 9 the number from these
        /// floors with the passages off and from version 15's generator (the code before work order 059, its defaults) was the
        /// same; Unity's runtime folds its doubles to its own number, pinned from the first Unity run (two thousand floors, as
        /// work order 058's: the order asked for five thousand, the producer's word kept the sweeps small).
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloors_AreVersion15s_FieldForField()
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, Version15); // work order 072: version 15 had none of the second set
                Assert.That(level.Passages, Is.False);
                Assert.That(level.Rooms.All(r => r.Kind != RoomKind.Passage), "no passage, seed " + level.SeedText);
                Assert.That(level.Describe(), Does.Not.Contain("passages"));
                h = FloorPrint.Of(level, h);
                foreach (LevelRoom r in level.Rooms)
                {
                    h = Mix(h, (ulong)r.Shape);
                    h = Mix(h, (ulong)r.SolidCount);
                    foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                    foreach (char c in r.Template?.ToString() ?? "-") h = Mix(h, c);
                }
            }
            // With the pin at 0 the test fails and says the number it found: the pin is taken from the first Unity run.
            const ulong pinned = 0xFAC7DAE9FE4DEE85UL; // the first Unity run (EditMode, filtered, on runner/059-passages, 2026-11-21); on .NET 9 these floors and version 15's both give 0x12C7B7AF80F09CE3
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("PASSAGESOFFPRINT " + found);
            if (pinned == 0UL) Assert.Fail("the pin is to be taken from the first Unity run: this run found " + found + " for the two thousand floors of version 15 (the passages off)");
            Assert.That(h, Is.EqualTo(pinned), "the two thousand floors of version 15, found " + found);
        }

        /// <summary>
        /// The switch off, or the line off, gives a floor without passages, and the floor's first line says which (seed 42). Work
        /// order 082 (review 6, finding 32): the comment here promised three hundred floors compared to the byte; the floors of
        /// version 15 are held by SwitchOff_TwoThousandFloors_... and the golden maps.
        /// </summary>
        [Test]
        public void SwitchOff_OrTheLineOff_GiveNoPassages()
        {
            Assert.That(LevelGenerator.Generate(42UL, 1, Off).Passages, Is.False);
            Assert.That(LevelGenerator.Generate(42UL, 1, new LevelValues { InALine = false }).Passages, Is.False);
            Assert.That(LevelGenerator.Generate(42UL, 1, new LevelValues { InALine = false }).Rooms.Any(r => r.Kind == RoomKind.Passage), Is.False);
            LevelLayout on = LevelGenerator.Generate(42UL, 1);
            Assert.That(on.Passages, Is.True);
            Assert.That(on.Describe().Split('\n')[0], Does.EndWith(" in a line with passages"));
            Assert.That(LevelGenerator.Generate(42UL, 1, Off).Describe().Split('\n')[0], Does.Not.Contain("passages"));
            Assert.That(on.Version, Is.EqualTo(19)); // work order 072; 087: 18; 088: 19
        }

        // ---------------- The streams ----------------

        [Test]
        public void TheSameSeed_GivesTheSameFloor_AndAWarmUpOnThePassagesStream_MovesOnlyThePassages()
        {
            int moved = 0, compared = 0;
            for (ulong s = 0; s < 60; s++)
            {
                int floor = 1 + (int)(s % 5);
                LevelLayout a = LevelGenerator.Generate(s, floor), b = LevelGenerator.Generate(s, floor);
                Assert.That(b.Describe(), Is.EqualTo(a.Describe()), "the same seed twice, seed " + s);
                LevelLayout warm;
                LevelGenerator.PassagesWarmUpForTests = 3;
                try { warm = LevelGenerator.Generate(s, floor); }
                finally { LevelGenerator.PassagesWarmUpForTests = 0; }
                Assert.That(LevelValidator.Validate(warm), Is.Empty, "seed " + s);
                if (warm.Attempt != a.Attempt) continue;
                compared++;
                List<LevelRoom> ra = a.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList(), rw = warm.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList();
                Assert.That(rw.Select(r => r.Rect), Is.EqualTo(ra.Select(r => r.Rect)), "every room where it was, seed " + s);
                Assert.That(rw.Select(r => r.Kind.ToString() + r.Elite + r.Template), Is.EqualTo(ra.Select(r => r.Kind.ToString() + r.Elite + r.Template)), "seed " + s);
                string Passage(LevelLayout l) => string.Join(";", l.Rooms.Where(r => r.Kind == RoomKind.Passage).Select(r => r.Rect + RoomShapes.Describe(r) + string.Join(",", r.SolidList)))
                                                 + string.Join(";", l.Doors.Select(d => d.Line + "@" + d.From));
                if (Passage(warm) != Passage(a)) moved++;
            }
            Assert.That(compared, Is.GreaterThanOrEqualTo(54), "the same attempt for nearly every seed");
            Assert.That(moved, Is.GreaterThan(compared / 2), "the warm-up did move passages, so the check above means something");
        }

        // ---------------- The validator ----------------

        /// <summary>
        /// A small floor: an entrance 5 x 3 at (10, 10), an exit 8 x 8 <paramref name="depth"/> cells north of it, and a passage
        /// between them of the given cells (along from the entrance's west wall, depth from its north wall), its doors at its
        /// first and last cell.
        /// </summary>
        static LevelLayout Mini(int depth, params (int a, int d)[] cells)
        {
            var level = new LevelLayout { Seed = 1, Version = LevelGenerator.Version, GridCells = 64, CellSize = 4f, Attempt = 1, Floor = 1, InALine = true, Passages = true };
            var e = new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(10, 10, 5, 3), Order = 0 };
            e.PlayerStart = e.Middle(4f);
            var p = new LevelRoom { Kind = RoomKind.Passage, Order = 1 };
            var x = new LevelRoom { Kind = RoomKind.Exit, Rect = new CellRect(9, 13 + depth, 8, 8), Order = 2 };
            int x0 = cells.Min(c => c.a), x1 = cells.Max(c => c.a), d0 = cells.Min(c => c.d), d1 = cells.Max(c => c.d);
            p.Rect = new CellRect(10 + x0, 13 + d0, x1 - x0 + 1, d1 - d0 + 1);
            for (int yy = p.Rect.Y; yy < p.Rect.Top; yy++)
                for (int xx = p.Rect.X; xx < p.Rect.Right; xx++)
                    if (!cells.Contains((xx - 10, yy - 13))) p.SetSolid(xx, yy, true);
            level.Rooms.Add(e);
            level.Rooms.Add(p);
            level.Rooms.Add(x);
            level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = 13, From = 10 + cells[0].a, Width = 1 });
            level.Doors.Add(new LevelDoor { RoomA = 1, RoomB = 2, Vertical = false, Line = 13 + depth, From = 10 + cells[cells.Length - 1].a, Width = 1 });
            return level;
        }

        static void AssertReports(LevelLayout level, string expected, LevelValues values = null)
        {
            List<string> faults = LevelValidator.Validate(level, values ?? new LevelValues());
            Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  " + string.Join("\n  ", faults) + "\n" + LevelMap.Draw(level));
        }

        static List<string> PassageFaults(LevelLayout level)
        {
            var faults = new List<string>();
            Passages.Faults(level, new LevelValues(), faults);
            return faults;
        }

        [Test]
        public void TheSmallFloor_KeepsEveryPassageRule()
        {
            Assert.That(PassageFaults(Mini(3, (2, 0), (2, 1), (2, 2))), Is.Empty, "straight");
            Assert.That(PassageFaults(Mini(3, (1, 0), (1, 1), (2, 1), (3, 1), (3, 2))), Is.Empty, "a jog of two");
            Assert.That(PassageFaults(Mini(2, (2, 0), (2, 1))), Is.Empty, "two deep");
            Assert.That(Passages.WalkFault(Mini(3, (1, 0), (1, 1), (2, 1), (2, 2))), Is.Null);
        }

        [Test]
        public void Validator_ReportsAPassageTwoCellsWide() =>
            AssertReports(Mini(3, (1, 0), (2, 0), (1, 1), (2, 1), (1, 2), (2, 2)), "branches");

        [Test]
        public void Validator_ReportsAPassageThatBranches() =>
            AssertReports(Mini(3, (1, 0), (1, 1), (1, 2), (2, 1)), "branches");

        [Test]
        public void Validator_ReportsAPassageWithThreeBends() =>
            AssertReports(Mini(4, (1, 0), (1, 1), (2, 1), (2, 2), (2, 3), (3, 3)), "bends 3 times", new LevelValues { PassageRules = new PassageValues { Most = 4 } });

        [Test]
        public void Validator_ReportsABendNextToARoom() =>
            AssertReports(Mini(3, (1, 0), (2, 0), (2, 1), (2, 2)), "in a cell next to one of its rooms");

        [Test]
        public void Validator_ReportsAPassageDeeperThanItsMost()
        {
            LevelLayout level = Mini(4, (2, 0), (2, 1), (2, 2), (2, 3));
            AssertReports(level, "4 cells deep, and a passage 2 to 3");
            AssertReports(level, "runs straight over more than 3 cells");
        }

        [Test]
        public void Validator_ReportsAPassageTouchingAThirdRoom()
        {
            LevelLayout level = Mini(3, (2, 0), (2, 1), (2, 2));
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Treasure, Rect = new CellRect(13, 14, 3, 1), Order = 3 });
            AssertReports(level, "P1 touches T3, which is not one of its two rooms");
        }

        [Test]
        public void Validator_ReportsTwoRoomsTouching()
        {
            LevelLayout level = Mini(3, (2, 0), (2, 1), (2, 2));
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Treasure, Rect = new CellRect(5, 10, 5, 3), Order = 3 });
            AssertReports(level, "E0 touches T3, and on a floor with passages the rooms stand apart");
        }

        [Test]
        public void Validator_ReportsAPassageDoorWithoutTheRoomsWallOnBothSides()
        {
            // At the entrance's west corner: its wall does not run on a cell beyond the door.
            AssertReports(Mini(3, (0, 0), (0, 1), (0, 2)), "has no wall on both sides: E0's wall does not run on a cell beyond it");
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            LevelDoor d = level.Doors[0];
            Assert.That(Passages.DoorFault(level, d), Is.Null);
            // A second door beside it on the room's wall.
            level.Doors.Add(new LevelDoor { RoomA = d.RoomA, RoomB = d.RoomB, Vertical = d.Vertical, Line = d.Line, From = d.From + 1, Width = 1 });
            Assert.That(Passages.DoorFault(level, d), Does.Contain("another door"));
        }

        [Test]
        public void Validator_ReportsAnEnemyAPropOrAThemeInAPassage()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            LevelRoom p = level.Rooms.First(r => r.Kind == RoomKind.Passage);
            (int x, int y) cell = Passages.PathOf(level, p)[1];
            var at = new LevelPoint((cell.x + 0.5) * 4, (cell.y + 0.5) * 4);
            p.EnemyPoints.Add(at);
            p.EnemyGroups.Add(new EnemyGroup(EnemyKind.Swarmer));
            p.EnemyPlaces.Add(new[] { at });
            AssertReports(level, p.Letter + p.Order.ToString(Inv) + " holds enemies");
            p.EnemyPoints.Clear(); p.EnemyGroups.Clear(); p.EnemyPlaces.Clear();
            p.Props.Add(LevelProp.Make(PropModel.Urns, at, 0, PropPlace.Floor));
            AssertReports(level, "holds 1 props, and a passage none");
            p.Props.Clear();
            p.Theme = RoomTheme.Shrine;
            AssertReports(level, "and a passage has no theme");
        }

        [Test]
        public void Validator_ReportsTheWalkBroken_APassagesPathCellMadeSolid()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            LevelRoom p = level.Rooms.First(r => r.Kind == RoomKind.Passage);
            List<(int x, int y)> path = Passages.PathOf(level, p);
            p.SetSolid(path[1].x, path[1].y, true);
            AssertReports(level, "the walk over cells from her start reaches");
        }

        [Test]
        public void Validator_ReportsAFloorWithPassages_UnderValuesWithTheSwitchOff()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            AssertReports(level, "the floor has passages and the values' Passages is off", Off);
            AssertReports(LevelGenerator.Generate(42UL, 1, Off), "the floor has no passages and the values' Passages is on");
        }

        // ---------------- The templates with a passage at each end ----------------

        /// <summary>
        /// Every template of the library in all eight orientations, for every draw of its ? cells and every pair of door marks a
        /// passage can use (an I on the wall she comes in by, an O on another), stamped on a floor with passages with a passage two
        /// cells deep at each door and a small room beyond each: no gate (the passage is the threshold), and work order 051's rules
        /// (<see cref="RoomShapes.Faults"/>, no filling) and work order 058's (<see cref="RoomTemplates.Check"/>) hold. What lies
        /// beside the door in is written out: the cells either side of it inside the room, by the template's own marks. Work order
        /// 072: the library holds the second set too, so every one of it is held here as well (1096 cases with the eleven).
        /// </summary>
        [Test]
        public void EveryTemplate_WithAPassageAtEachEnd_KeepsTheRules_InEveryCase()
        {
            var v = new LevelValues();
            int cases = 0;
            var beside = new Dictionary<string, int>();
            foreach (RoomTemplate t in RoomTemplateLibrary.All)
                foreach (int wall in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                    foreach (bool mirror in new[] { false, true })
                    {
                        TemplatePlacement o = t.Oriented(wall, mirror);
                        var rect = new CellRect(20, 20, o.Width, o.Height);
                        var ins = new List<(int side, int at)>();
                        var outs = new List<(int side, int at)>();
                        foreach (int side in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                        {
                            bool alongX = side == LevelGenerator.North || side == LevelGenerator.South;
                            int lo = alongX ? rect.X : rect.Y, hi = alongX ? rect.Right : rect.Top;
                            if (side == wall) ins.AddRange(RoomTemplates.DoorPlaces(rect, o, side, lo, hi, 'I', 1).Select(a => (side, a)));
                            else outs.AddRange(RoomTemplates.DoorPlaces(rect, o, side, lo, hi, 'O', 1).Select(a => (side, a)));
                        }
                        Assert.That(ins, Is.Not.Empty, t.Name);
                        var outList = t.For == RoomTemplateFor.Exit ? new List<(int side, int at)?> { null } : outs.Select(x => ((int, int)?)x).ToList();
                        for (int bits = 0; bits < 1 << t.Drawn.Count; bits++)
                            foreach ((int side, int at) i in ins)
                                foreach ((int side, int at)? x in outList)
                                {
                                    cases++;
                                    LevelLayout level = Stamped(t, o, rect, bits, i, x, out LevelRoom room);
                                    string label = $"{t.Name} in {RoomTemplates.WallWord(wall)}{(mirror ? " mirrored" : "")} door in {i} out {x} ? {bits}";
                                    var broken = new List<string>();
                                    Assert.That(RoomShapes.Faults(level, room, v, broken), Is.EqualTo(0), label + ": " + string.Join("; ", broken));
                                    var check = new List<string>();
                                    RoomTemplates.Check(level, check);
                                    Assert.That(check, Is.Empty, label);
                                    Assert.That(RoomShapes.GateCells(level, room), Is.Empty, label);
                                    // Beside the door in, inside the room: what the template puts there.
                                    List<(int x, int y)> cells = RoomShapes.CellsBesideTheDoorIn(level, room);
                                    string key = t.Name + " " + string.Concat(cells.Select(c => room.IsSolid(c.x, c.y) ? '#' : RoomTemplates.MarkAt(room, c.x, c.y)));
                                    beside[key] = beside.TryGetValue(key, out int n) ? n + 1 : 1;
                                }
                    }
            TestContext.WriteLine("PASSAGETEMPLATES " + cases + " cases, all keep the rules; beside the door in (the cells a gate would take, as the template has them): "
                                  + string.Join(", ", beside.OrderBy(k => k.Key).Select(k => k.Key + " x" + k.Value)));
            Assert.That(cases, Is.GreaterThan(1096), "work order 072: the eleven's 1096 cases and the second set's"); // 1656 on 2026-11-21
        }

        /// <summary>A floor with passages round one stamped template room: a 3 x 3 room two cells off its door in and one two cells off its door out, a passage straight between.</summary>
        static LevelLayout Stamped(RoomTemplate t, TemplatePlacement o, CellRect rect, int bits, (int side, int at) i, (int side, int at)? x, out LevelRoom room)
        {
            var level = new LevelLayout { Seed = 0, Version = LevelGenerator.Version, GridCells = 64, CellSize = 4f, Attempt = 1, Floor = 1, InALine = true, Shapes = true, Templates = true, Passages = true };
            CellRect Beyond(int side, int at, int gap) =>
                side == LevelGenerator.North ? new CellRect(at - 1, rect.Top + gap, 3, 3)
                : side == LevelGenerator.South ? new CellRect(at - 1, rect.Y - gap - 3, 3, 3)
                : side == LevelGenerator.East ? new CellRect(rect.Right + gap, at - 1, 3, 3)
                : new CellRect(rect.X - gap - 3, at - 1, 3, 3);
            CellRect PassageRect(int side, int at, int gap) =>
                side == LevelGenerator.North ? new CellRect(at, rect.Top, 1, gap)
                : side == LevelGenerator.South ? new CellRect(at, rect.Y - gap, 1, gap)
                : side == LevelGenerator.East ? new CellRect(rect.Right, at, gap, 1)
                : new CellRect(rect.X - gap, at, gap, 1);
            LevelDoor Door(int side, int at, bool roomSide, int a, int b)
            {
                bool vertical = side == LevelGenerator.East || side == LevelGenerator.West;
                int line = side == LevelGenerator.North ? (roomSide ? rect.Top : rect.Top + 2) : side == LevelGenerator.South ? (roomSide ? rect.Y : rect.Y - 2)
                         : side == LevelGenerator.East ? (roomSide ? rect.Right : rect.Right + 2) : (roomSide ? rect.X : rect.X - 2);
                return new LevelDoor { RoomA = a, RoomB = b, Vertical = vertical, Line = line, From = at, Width = 1 };
            }
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = Beyond(i.side, i.at, 2), Order = 0 });
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Passage, Rect = PassageRect(i.side, i.at, 2), Order = 1 });
            room = new LevelRoom
            {
                Kind = t.For == RoomTemplateFor.Exit ? RoomKind.Exit : RoomKind.Combat, Elite = t.For == RoomTemplateFor.Elite, Rect = rect, Order = 2, Template = o,
            };
            level.Rooms.Add(room);
            level.Doors.Add(Door(i.side, i.at, false, 0, 1));
            level.Doors.Add(Door(i.side, i.at, true, 1, 2));
            if (x.HasValue)
            {
                level.Rooms.Add(new LevelRoom { Kind = RoomKind.Passage, Rect = PassageRect(x.Value.side, x.Value.at, 2), Order = 3 });
                level.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = Beyond(x.Value.side, x.Value.at, 2), Order = 4 });
                level.Doors.Add(Door(x.Value.side, x.Value.at, true, 2, 3));
                level.Doors.Add(Door(x.Value.side, x.Value.at, false, 3, 4));
            }
            for (int ly = 0; ly < o.Height; ly++)
                for (int lx = 0; lx < o.Width; lx++)
                    if (o.MarkAt(lx, ly) == '#') room.SetSolid(rect.X + lx, rect.Y + ly, true);
            for (int q = 0; q < t.Drawn.Count; q++)
            {
                if ((bits & (1 << q)) == 0) continue;
                o.Local(t.Drawn[q].ax, t.Drawn[q].ay, out int lx, out int ly);
                room.SetSolid(rect.X + lx, rect.Y + ly, true);
            }
            RoomShapes.Gate(level, room);
            room.Shape = RoomShape.Template;
            return level;
        }

        // ---------------- The signs, the map and the room numbers ----------------

        /// <summary>A floor of seed 42 with passages, and the passage before the first room of this kind (the elite room for Elite).</summary>
        static LevelLayout Before(Func<LevelRoom, bool> which, out LevelRoom passage, out LevelRoom room)
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 5);
            room = level.Rooms.First(which);
            passage = level.Rooms[room.Order - 1];
            return level;
        }

        [Test]
        public void ADoorsSign_PassesThroughAPassage_BothEndsShowIt_LitFromTheirOwnRoom_AndDimOnceTheRoomIsDone()
        {
            foreach ((string what, Func<LevelRoom, bool> which, DoorSign sign) in new (string, Func<LevelRoom, bool>, DoorSign)[]
                     {
                         ("treasure", r => r.Kind == RoomKind.Treasure, DoorSign.Treasure),
                         ("elite", r => r.Elite, DoorSign.Elite),
                         ("exit", r => r.Kind == RoomKind.Exit, DoorSign.Boss),
                     })
            {
                LevelLayout level = Before(which, out LevelRoom passage, out LevelRoom room);
                Assert.That(passage.Kind, Is.EqualTo(RoomKind.Passage), what);
                LevelDoor mouth = level.Doors.First(d => d.RoomB == passage.Order), far = level.Doors.First(d => d.RoomA == passage.Order);
                int before = mouth.RoomA;
                Assert.That(DoorSignRules.SignedRoom(level, mouth), Is.EqualTo(room.Order), "the mouth's sign is the room past the passage: " + what);
                Assert.That(DoorSignRules.SignedRoom(level, far), Is.EqualTo(room.Order), what);
                foreach (LevelDoor d in new[] { mouth, far })
                    Assert.That(DoorSignRules.SignOf(level.Rooms[DoorSignRules.SignedRoom(level, d)], level.Floor, 5), Is.EqualTo(sign), what);
                // Lit while she is in the room before the door, not done; the mouth's from the room before, the far end's from the passage.
                Assert.That(DoorSignRules.Lit(sign, mouth, before, false), Is.True, what);
                Assert.That(DoorSignRules.Lit(sign, mouth, passage.Order, false), Is.False, what);
                Assert.That(DoorSignRules.Lit(sign, far, passage.Order, false), Is.True, what);
                Assert.That(DoorSignRules.Lit(sign, far, before, false), Is.False, what);
                Assert.That(DoorSignRules.Lit(sign, mouth, before, true) || DoorSignRules.Lit(sign, far, passage.Order, true), Is.False, "both dim once done: " + what);
                // The door out of the room: the passage it opens on and the room past it.
                Assert.That(DoorSignRules.Beyond(mouth), Is.EqualTo(passage.Order));
            }
            // Without passages a sign is its door's later room, as before.
            LevelLayout plain = LevelGenerator.Generate(42UL, 5, Off);
            foreach (LevelDoor d in plain.Doors) Assert.That(DoorSignRules.SignedRoom(plain, d), Is.EqualTo(DoorSignRules.Beyond(d)));
        }

        [Test]
        public void TheMapsRoomBefore_PassesOverAPassage_AndTheRoomNumbersSkipThem()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            List<LevelRoom> rooms = level.Rooms;
            for (int i = 1; i < rooms.Count; i++)
            {
                LevelRoom r = rooms[i];
                int before = FloorMapRules.Before(level, r);
                if (r.Kind == RoomKind.Passage) Assert.That(before, Is.EqualTo(i - 1), "a passage's is the room before it");
                else
                {
                    Assert.That(rooms[i - 1].Kind, Is.EqualTo(RoomKind.Passage));
                    Assert.That(before, Is.EqualTo(i - 2), "a room's is the room before its passage");
                }
            }
            // So once a room is entered, its next passage and the room after show one step ahead, never more.
            var entered = new HashSet<int> { 4 };
            var ahead = rooms.Where(r => !entered.Contains(r.Order) && entered.Contains(FloorMapRules.Before(level, r))).Select(r => r.Order).ToList();
            Assert.That(ahead, Is.EquivalentTo(new[] { 5, 6 }));

            Assert.That(LevelGeometry.RoomCount(level), Is.EqualTo(9));
            Assert.That(rooms.Where(r => r.Kind != RoomKind.Passage).Select(r => LevelGeometry.RoomNumber(level, r)), Is.EqualTo(Enumerable.Range(0, 9)));
            Assert.That(LevelGeometry.RoomNumber(level, rooms[1]), Is.EqualTo(1), "a passage: the rooms before it");
            Assert.That(RunWords.Room(rooms[1].Kind, LevelGeometry.RoomNumber(level, rooms[1]), 9), Is.EqualTo("Passage"));
            LevelRoom combat = rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite);
            Assert.That(RunWords.Room(combat.Kind, LevelGeometry.RoomNumber(level, combat), LevelGeometry.RoomCount(level)), Is.EqualTo("Room 2 of 9"));
            // Without passages the numbers are the orders, as before.
            LevelLayout plain = LevelGenerator.Generate(42UL, 1, Off);
            Assert.That(LevelGeometry.RoomCount(plain), Is.EqualTo(plain.Rooms.Count));
            foreach (LevelRoom r in plain.Rooms)
            {
                Assert.That(LevelGeometry.RoomNumber(plain, r), Is.EqualTo(r.Order));
                Assert.That(FloorMapRules.Before(plain, r), Is.EqualTo(LevelGeometry.EntryDoor(plain, r)?.Other(r.Order) ?? -1));
            }
        }

        // ---------------- The mood and the map ----------------

        [Test]
        public void TheMoodsPlan_MakesNoRoomForAPassage_AndTheTextMapShowsItAsAStripMarkedP()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            RoomMoodPlan plan = RoomMoodPlan.Make(level, new LevelValues());
            Assert.That(plan.Rooms.Select(m => m.Room), Is.EqualTo(level.Rooms.Where(r => r.Kind != RoomKind.Passage)), "a plan for every room that is not a passage, in order");
            foreach (MoodRoom mr in plan.Rooms)
            {
                LevelDoor entry = LevelGeometry.EntryDoor(level, mr.Room);
                if (entry != null) Assert.That(mr.FarSide, Is.EqualTo(LevelGenerator.Opposite(LevelGeometry.WallOf(mr.Room.Rect, entry))), "the far side across from the door from the passage");
            }
            // The mood's stream: a passage draws nothing, so a plan made of the floor with its passages' rooms listed or not is the same.
            string map = LevelMap.Draw(level);
            TestContext.WriteLine("PASSAGEMAP\n" + map);
            string[] lines = map.Split('\n');
            CellRect box = level.Bounds();
            int rows = 2 * box.Height + 1;
            char At(int gx, int gy) { string l = lines[1 + rows - 1 - gy]; return gx < l.Length ? l[gx] : ' '; }
            foreach (LevelRoom p in level.Rooms.Where(r => r.Kind == RoomKind.Passage))
            {
                List<(int x, int y)> path = Passages.PathOf(level, p);
                (int x, int y) first = path.OrderByDescending(c => c.y).ThenBy(c => c.x).First();
                Assert.That(At(2 * (first.x - box.X) + 1, 2 * (first.y - box.Y) + 1), Is.EqualTo('P'), "its label in its first open cell from the north-west\n" + map);
                foreach ((int x, int y) in path)
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        (int x, int y) n = (x + dx, y + dy);
                        if (path.Contains(n)) continue;
                        bool door = level.DoorsOf(p.Order).Any(d => d.CellPairs().Any(c => (c.Key == (x, y) && c.Value == n) || (c.Value == (x, y) && c.Key == n)));
                        char wall = At(2 * (x - box.X) + 1 + dx, 2 * (y - box.Y) + 1 + dy);
                        Assert.That(wall, Is.EqualTo(door ? ' ' : dx != 0 ? '|' : '-'), "a strip one cell wide: its sides walled, its ends open, at " + (x, y) + "\n" + map);
                    }
            }
        }

        // ---------------- The walk, measured ----------------

        /// <summary>
        /// The walk from her start to the exit room's middle (work order 059, the producer's word of 2026-11-21): the shortest way
        /// over a grid of points a metre apart (each the middle of a square of the floor), a step to any of the eight round it,
        /// along the floor's open cells, across a cell's edge only within one room or through a door, never cutting a corner a
        /// wall stands on. Its length in metres, and how much of it is in passages.
        /// </summary>
        public static double Walk(LevelLayout level, out double inPassages)
        {
            inPassages = 0;
            double cs = level.CellSize;
            const int k = 4; // points a cell, on a side (cells of 4 m)
            CellRect box = level.Bounds();
            int nx = box.Width * k, ny = box.Height * k;
            var owner = new LevelRoom[box.Width * box.Height];
            for (int y = 0; y < box.Height; y++)
                for (int x = 0; x < box.Width; x++) owner[y * box.Width + x] = level.RoomAtCell(box.X + x, box.Y + y);
            var doors = new HashSet<(int, int, int, int)>();
            foreach (LevelDoor d in level.Doors)
                foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                {
                    doors.Add((pair.Key.x, pair.Key.y, pair.Value.x, pair.Value.y));
                    doors.Add((pair.Value.x, pair.Value.y, pair.Key.x, pair.Key.y));
                }
            LevelRoom Owner(int cx, int cy) => cx < 0 || cy < 0 || cx >= box.Width || cy >= box.Height ? null : owner[cy * box.Width + cx];
            bool Cross(int ax, int ay, int bx, int by) // orthogonal neighbours' cells, box-relative
            {
                LevelRoom a = Owner(ax, ay), b = Owner(bx, by);
                if (a == null || b == null) return false;
                if (ax == bx && ay == by) return true;
                return a == b || doors.Contains((ax + box.X, ay + box.Y, bx + box.X, by + box.Y));
            }
            bool Step(int i, int j, int i2, int j2)
            {
                if (i2 < 0 || j2 < 0 || i2 >= nx || j2 >= ny) return false;
                int ci = i / k, cj = j / k, ci2 = i2 / k, cj2 = j2 / k;
                if (i == i2 || j == j2) return Cross(ci, cj, ci2, cj2);
                // A diagonal step: both ways round it open.
                return Cross(ci, cj, i2 / k, cj) && Cross(i2 / k, cj, ci2, cj2) && Cross(ci, cj, ci, j2 / k) && Cross(ci, j2 / k, ci2, cj2);
            }
            int Index(LevelPoint p) => Math.Max(0, Math.Min(ny - 1, (int)Math.Floor((p.Y / cs - box.Y) * k))) * nx + Math.Max(0, Math.Min(nx - 1, (int)Math.Floor((p.X / cs - box.X) * k)));
            LevelRoom start = level.Rooms[0];
            int from = Index(start.PlayerStart ?? start.Middle(cs)), to = Index(level.ExitRoom.Middle(cs));
            var dist = new double[nx * ny];
            var back = new int[nx * ny];
            for (int q = 0; q < dist.Length; q++) { dist[q] = double.PositiveInfinity; back[q] = -1; }
            var heap = new List<(double d, int at)>();
            void Push(double d, int at)
            {
                heap.Add((d, at));
                for (int c = heap.Count - 1; c > 0;)
                {
                    int p = (c - 1) / 2;
                    if (heap[p].d <= heap[c].d) break;
                    (heap[p], heap[c]) = (heap[c], heap[p]);
                    c = p;
                }
            }
            (double d, int at) Pop()
            {
                (double d, int at) top = heap[0];
                heap[0] = heap[heap.Count - 1];
                heap.RemoveAt(heap.Count - 1);
                for (int c = 0; ;)
                {
                    int l = 2 * c + 1, r = l + 1, m = c;
                    if (l < heap.Count && heap[l].d < heap[m].d) m = l;
                    if (r < heap.Count && heap[r].d < heap[m].d) m = r;
                    if (m == c) break;
                    (heap[m], heap[c]) = (heap[c], heap[m]);
                    c = m;
                }
                return top;
            }
            dist[from] = 0;
            Push(0, from);
            double unit = cs / k;
            while (heap.Count > 0)
            {
                (double d, int at) = Pop();
                if (d > dist[at]) continue;
                if (at == to) break;
                int i = at % nx, j = at / nx;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        if (!Step(i, j, i + di, j + dj)) continue;
                        int n = (j + dj) * nx + i + di;
                        double nd = d + unit * (di != 0 && dj != 0 ? Math.Sqrt(2) : 1);
                        if (nd >= dist[n]) continue;
                        dist[n] = nd;
                        back[n] = at;
                        Push(nd, n);
                    }
            }
            if (double.IsInfinity(dist[to])) return double.PositiveInfinity;
            for (int at = to; back[at] >= 0; at = back[at])
            {
                int b = back[at];
                LevelRoom r1 = Owner(at % nx / k, at / nx / k), r2 = Owner(b % nx / k, b / nx / k);
                if (r1 != null && r2 != null && r1.Kind == RoomKind.Passage && r2.Kind == RoomKind.Passage) inPassages += dist[at] - dist[b];
                else if ((r1 != null && r1.Kind == RoomKind.Passage) || (r2 != null && r2.Kind == RoomKind.Passage)) inPassages += (dist[at] - dist[b]) * 0.5;
            }
            return dist[to];
        }

        [Test]
        public void TheWalk_ReachesTheExitRoomsMiddle_ThroughEveryPassage()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            double walk = Walk(level, out double inPassages);
            Assert.That(walk, Is.LessThan(double.PositiveInfinity));
            Assert.That(inPassages, Is.GreaterThanOrEqualTo(8 * 2 * 4 * 0.9), "eight passages at least two cells each");
            LevelLayout plain = LevelGenerator.Generate(42UL, 1, Off);
            Assert.That(Walk(plain, out double none), Is.LessThan(walk));
            Assert.That(none, Is.EqualTo(0));
        }

        /// <summary>
        /// The producer's measure (2026-11-21): over a thousand seeds a floor, with the passages off and on, the walk from her
        /// start to the exit room's middle, its time at her run (<see cref="FeelValues.RunSpeed"/>) with no fight, and the share of
        /// it in passages. Run by name; it takes about a minute on .NET, four in the Editor.
        /// </summary>
        [Explicit, Test, Timeout(3600000)]
        public void Measure_TheWalk_WithThePassagesOffAndOn() => MeasureWalk(new LevelValues());

        /// <summary>The same with every passage two cells deep (<see cref="PassageValues.Most"/> 2), the shortest the order allows.</summary>
        [Explicit, Test, Timeout(3600000)]
        public void Measure_TheWalk_WithEveryPassageTwoDeep() => MeasureWalk(new LevelValues { PassageRules = new PassageValues { Most = 2 } });

        /// <summary>The same with every passage three cells deep (<see cref="PassageValues.Fewest"/> 3).</summary>
        [Explicit, Test, Timeout(3600000)]
        public void Measure_TheWalk_WithEveryPassageThreeDeep() => MeasureWalk(new LevelValues { PassageRules = new PassageValues { Fewest = 3 } });

        static void MeasureWalk(LevelValues values)
        {
            double run = new FeelValues().RunSpeed;
            var lines = new List<string>();
            double allOff = 0, allOn = 0, allIn = 0;
            int n = 0;
            for (int floor = 1; floor <= 5; floor++)
            {
                double off = 0, on = 0, inP = 0;
                const int seeds = 1000;
                for (ulong s = 0; s < seeds; s++)
                {
                    off += Walk(LevelGenerator.Generate(s, floor, Off), out _);
                    on += Walk(LevelGenerator.Generate(s, floor, values), out double p);
                    inP += p;
                }
                off /= seeds; on /= seeds; inP /= seeds;
                allOff += off; allOn += on; allIn += inP; n++;
                lines.Add(string.Format(Inv, "floor {0}: off {1:0.0} m ({2:0.0} s), on {3:0.0} m ({4:0.0} s), +{5:P1}, in passages {6:0.0} m ({7:P1} of the walk)",
                                        floor, off, off / run, on, on / run, on / off - 1, inP, inP / on));
            }
            lines.Add(string.Format(Inv, "all floors: off {0:0.0} m ({1:0.0} s), on {2:0.0} m ({3:0.0} s), +{4:P1}, in passages {5:0.0} m ({6:P1})",
                                    allOff / n, allOff / n / run, allOn / n, allOn / n / run, allOn / allOff - 1, allIn / n, allIn / allOn));
            TestContext.WriteLine("PASSAGEWALK passages " + values.PassageRules.Fewest + " to " + values.PassageRules.Most + " deep, at " + run + " m/s:\n" + string.Join("\n", lines));
        }
    }
}
