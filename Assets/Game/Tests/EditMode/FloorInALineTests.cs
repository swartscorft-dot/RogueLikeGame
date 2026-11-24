using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// A floor in a line (work order 041, Kristian's second session of 2026-11-20: "I need the run to be more linear", and
    /// asked how linear, fully): the row's rule; a thousand seeds a floor number, each floor one row with its treasure rooms
    /// and its elite room on it, valid, as many rooms and enemies as version 9's floor of the seed (the same groups, room for
    /// room), and about as quick to make; with the switch off every floor is version 9's, field for field, against prints
    /// taken of version 9's code before this order touched it; the validator's rule of the row; and the plain rules of the
    /// door signs and of the small map.
    /// </summary>
    public class FloorInALineTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        // ---------------- The row's rule ----------------

        static string Letters(LevelPlan plan) => string.Concat(plan.LineBetween().Select(LevelRoom.KindLetter));

        [TestCase(3, "CTCTC")]
        [TestCase(4, "CCTCTC")]   // the plan's: combat, combat, treasure, elite, treasure, combat
        [TestCase(5, "CCTCCTC")]  // the order's first guess: combat, combat, treasure, elite, combat, treasure, combat
        [TestCase(6, "CCCTCCTC")]
        [TestCase(7, "CCCTCCTCC")]
        public void TheRow_PutsTheTreasureRoomsBeforeTheEliteRoom_AndOneCombatRoomPastIt_WhereThereIsRoom(int combats, string wanted)
        {
            Assert.That(Letters(new LevelPlan { CombatRooms = combats, TreasureRooms = 2, MoreCombatRooms = 0 }), Is.EqualTo(wanted)); // the row of that many (work order 046)
        }

        [Test]
        public void TheRow_NeverPutsATreasureRoomFirst_Last_OrNextToAnother_WhenThereAreGapsEnough()
        {
            for (int combats = 1; combats <= 9; combats++)
                for (int treasures = 0; treasures <= 4; treasures++)
                {
                    var plan = new LevelPlan { CombatRooms = combats, TreasureRooms = treasures, MoreCombatRooms = 0 }; // work order 046: the row of that many
                    List<RoomKind> line = plan.LineBetween();
                    string what = combats + " combat rooms, " + treasures + " treasure rooms: " + Letters(plan);
                    Assert.That(line.Count(k => k == RoomKind.Combat), Is.EqualTo(combats), what);
                    Assert.That(line.Count(k => k == RoomKind.Treasure), Is.EqualTo(treasures), what);
                    if (treasures > combats - 1) continue; // fewer gaps than treasure rooms: no plan of the game
                    if (line.Count > 0)
                    {
                        Assert.That(line[0], Is.EqualTo(RoomKind.Combat), "never first after the entrance: " + what);
                        Assert.That(line[line.Count - 1], Is.EqualTo(RoomKind.Combat), "never last before the exit: " + what);
                    }
                    for (int i = 1; i < line.Count; i++)
                        Assert.That(line[i] == RoomKind.Treasure && line[i - 1] == RoomKind.Treasure, Is.False, "never two side by side: " + what);
                    // One just before the elite room's place, always, when there is a treasure room and a gap before it.
                    int middle = combats / 2 + 1, combat = 0;
                    for (int i = 0; i < line.Count; i++)
                    {
                        if (line[i] == RoomKind.Combat) combat++;
                        if (line[i] == RoomKind.Combat && combat == middle && treasures > 0 && middle > 1)
                            Assert.That(line[i - 1], Is.EqualTo(RoomKind.Treasure), "a treasure room before the elite room: " + what);
                    }
                }
        }

        // ---------------- A thousand seeds ----------------

        static string Groups(LevelRoom r) => string.Join(",", r.EnemyGroups.Select(g => g.ToString()).OrderBy(x => x, StringComparer.Ordinal));

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_InALine_EachFloorIsOneRow_ValidAndAsFullAsVersion9s_AndAboutAsQuick(int floor)
        {
            // Work order 051: the rooms' carved outlines off, so this measures the line against the side rooms as it did; what the
            // shapes cost is measured on its own (RoomShapesTests.Generation_CostsAtMostHalfAsMuchAgain_ByTheMedian).
            var on = new LevelValues { Shapes = false, Passages = false, Warders = false, Bombers = false }; // work order 059: the rooms touching, as the row of 041 was measured; 087: the warders and bombers are put into the row's groups after version 9's floor is drawn, so they are left out here
            var off = new LevelValues { InALine = false };
            Assert.That(on.InALine, Is.True, "on by default");
            LevelGenerator.Generate(424242UL, floor, on); // the first calls pay for compiling the code
            LevelGenerator.Generate(424242UL, floor, off);
            List<RoomKind> wanted = new List<RoomKind> { RoomKind.Entrance };
            wanted.AddRange(on.Plan.LineBetween());
            wanted.Add(RoomKind.Exit);
            int eliteNumber = on.Plan.EliteRoomNumber(on.Plan.LineCombatRooms); // work order 046: of the row's five
            var timesOn = new List<double>();
            var timesOff = new List<double>();
            var layouts = new HashSet<string>();
            var timer = new Stopwatch();
            for (ulong s = 0; s < 1000; s++)
            {
                // The two, one after the other, so that a busy machine slows both alike.
                timer.Restart();
                LevelLayout level = LevelGenerator.Generate(s, floor, on);
                timesOn.Add(timer.Elapsed.TotalMilliseconds);
                timer.Restart();
                LevelLayout nine = LevelGenerator.Generate(s, floor, off);
                timesOff.Add(timer.Elapsed.TotalMilliseconds);
                string where = "seed " + s + " floor " + floor + "\n" + LevelMap.Draw(level);

                Assert.That(level.InALine, Is.True, where);
                Assert.That(LevelValidator.Validate(level, on), Is.Empty, where);
                // One row: the rooms in the order of the run, each joined to the one before it and to no other.
                Assert.That(level.Rooms.Select(r => r.Kind), Is.EqualTo(wanted), "the row's kinds: " + where);
                Assert.That(level.Doors.Count, Is.EqualTo(level.Rooms.Count - 1), where);
                for (int i = 0; i < level.Rooms.Count; i++)
                {
                    List<int> others = level.DoorsOf(i).Select(d => d.Other(i)).OrderBy(x => x).ToList();
                    var neighbours = new List<int>();
                    if (i > 0) neighbours.Add(i - 1);
                    if (i + 1 < level.Rooms.Count) neighbours.Add(i + 1);
                    Assert.That(others, Is.EqualTo(neighbours), "room " + i + " has one door in and one out, the entrance and the exit one: " + where);
                }
                Assert.That(level.Rooms[0].Kind, Is.EqualTo(RoomKind.Entrance));
                Assert.That(level.Rooms[level.Rooms.Count - 1].Kind, Is.EqualTo(RoomKind.Exit));
                // The elite room: the plan's combat room of the row.
                List<LevelRoom> combats = level.Rooms.Where(r => r.Kind == RoomKind.Combat).ToList();
                for (int i = 0; i < combats.Count; i++) Assert.That(combats[i].Elite, Is.EqualTo(i + 1 == eliteNumber), where);
                Assert.That(level.Rooms.Count(r => r.Kind == RoomKind.Treasure), Is.EqualTo(on.Plan.TreasureRooms), where);
                foreach (LevelRoom t in level.Rooms.Where(r => r.Kind == RoomKind.Treasure))
                    Assert.That(t.Props.Count(p => p.Model == PropModel.Chest), Is.EqualTo(1), "a chest in every treasure room: " + where);

                // As full as version 9's floor of the seed: as many rooms and enemies, and the same groups room for room; work
                // order 046: and one combat room more, the one after the elite room, as full as a combat room of its floor.
                Assert.That(level.Rooms.Count, Is.EqualTo(nine.Rooms.Count + on.Plan.MoreCombatRooms), where);
                LevelRoom more = combats[eliteNumber];
                FloorEnemies range = on.EnemiesOf(floor);
                Assert.That(more.EnemyCount, Is.InRange(range.Fewest, range.Most), "the further combat room: " + where);
                Assert.That(more.Elite, Is.False, where);
                Assert.That(level.Rooms.Sum(r => r.EnemyCount) - more.EnemyCount, Is.EqualTo(nine.Rooms.Sum(r => r.EnemyCount)), "enemies: " + where);
                List<string> mine = level.Rooms.Where(r => (r.Kind == RoomKind.Combat && r != more) || r.Kind == RoomKind.Exit).Select(Groups).ToList();
                List<string> theirs = LevelGeometry.MainPath(nine).Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).Select(Groups).ToList();
                Assert.That(mine, Is.EqualTo(theirs), "the same groups, room for room: " + where);
                layouts.Add(level.DescribeLayout());
            }
            timesOn.Sort();
            timesOff.Sort();
            double medianOn = timesOn[500], medianOff = timesOff[500];
            TestContext.WriteLine(string.Format(s_Inv, "LINESTATS floor {0}: median {1:0.00} ms in a line, {2:0.00} ms with the switch off ({3:0.00} times); slowest {4:0.0} and {5:0.0} ms; distinct layouts {6}",
                                                floor, medianOn, medianOff, medianOn / Math.Max(1e-6, medianOff), timesOn[999], timesOff[999], layouts.Count));
            Assert.That(layouts.Count, Is.GreaterThanOrEqualTo(990), "distinct layouts");
            // About as quick (the middle of a thousand, not the slowest: the machine is busy): measured 1.2 times.
            Assert.That(medianOn, Is.LessThan(1.6 * medianOff + 0.5), "median generation in a line, ms, against the switch off");
        }

        // ---------------- The switch off: version 9's floors ----------------

        /// <summary>
        /// Seeds 0 to 999 of each floor number with the defaults, one after the other. <c>print</c>: <see cref="Print"/> of every
        /// floor, taken of version 9's code (the generator as it was before work order 041, unchanged, compiled and run outside
        /// the Editor on the Mono the Editor ships). <c>described</c>: an FNV-1a hash of every floor's whole description
        /// (<see cref="LevelLayout.Describe"/>) as the Editor prints it, taken of this order's code with the switch off at its
        /// first run, a pin against later changes: version 9's code printed it otherwise outside the Editor, its lengths
        /// differing there in their last bits (the same code both ways; the rooms, doors, enemies and maps alike).
        /// </summary>
        static readonly Dictionary<int, (string print, string described)> Version9 = new Dictionary<int, (string, string)>
        {
            [1] = ("F80F1AAF96981A39", "D68C11A3FCB6ECE2"),
            [2] = ("A8C81BE62D21E3C1", "5EF836D7DED3E5A6"),
            [3] = ("8597C68B78F0AC73", "50E40FCF373FF1B3"),
            [4] = ("B94BD8D37566689C", "0CA801786E7B30B2"),
            [5] = ("E181246DC9ACA33F", "482503B0AC62305D"),
        };

        /// <summary>
        /// Every field of a floor but its version, as an FNV-1a hash, no printing of numbers coming into it: its whole numbers
        /// as they are and its lengths in millionths of a metre (bit for bit they differed between the Editor and the Mono the
        /// prints were taken on, in the last bits only, so the rooms, doors and maps matched and the bits did not).
        /// </summary>
        sealed class Print
        {
            public ulong Hash = 14695981039346656037UL;

            void B(ulong v)
            {
                for (int i = 0; i < 8; i++)
                {
                    Hash ^= (v >> (8 * i)) & 0xFF;
                    Hash *= 1099511628211UL;
                }
            }

            void I(long v) => B((ulong)v);
            void D(double v) => B((ulong)(long)Math.Round(v * 1e6));
            void P(LevelPoint p) { D(p.X); D(p.Y); }

            public void Add(LevelLayout l)
            {
                B(l.Seed); I(l.GridCells); D(l.CellSize); I(l.Attempt); I(l.Floor);
                I(l.Rooms.Count);
                foreach (LevelRoom r in l.Rooms)
                {
                    I((int)r.Kind); I(r.Rect.X); I(r.Rect.Y); I(r.Rect.Width); I(r.Rect.Height); I(r.Order); I(r.Elite ? 1 : 0); I((int)r.Theme);
                    I(r.PlayerStart.HasValue ? 1 : 0);
                    if (r.PlayerStart.HasValue) P(r.PlayerStart.Value);
                    I(r.EnemyPoints.Count);
                    for (int i = 0; i < r.EnemyPoints.Count; i++)
                    {
                        P(r.EnemyPoints[i]);
                        EnemyGroup g = r.GroupAt(i);
                        I((int)g.Kind); I(g.Count); I(g.Boss ? 1 : 0); I(g.Elite ? 1 : 0);
                        LevelPoint[] m = r.PlacesAt(i);
                        I(m.Length);
                        foreach (LevelPoint q in m) P(q);
                    }
                    I(r.Props.Count);
                    foreach (LevelProp p in r.Props)
                    {
                        I((int)p.Model); P(p.Position); I(p.Rotation); I((int)p.Place); I((int)p.Shape);
                        D(p.Width); D(p.Depth); D(p.Radius); D(p.Height); I(p.Slender ? 1 : 0);
                    }
                    I(r.PropsWanted); I(r.PropsLeftOutByLane);
                }
                I(l.Doors.Count);
                foreach (LevelDoor d in l.Doors) { I(d.RoomA); I(d.RoomB); I(d.Vertical ? 1 : 0); I(d.Line); I(d.From); I(d.Width); }
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_WithTheSwitchOff_AreVersion9sFloors_FieldForField(int floor)
        {
            // Work order 044: the rooms' mood off too, as version 9 had none: with it on, an ossuary's piles are banked at its
            // far wall (pinned in RoomMoodPlanTests), and every other field of every floor is the same either way.
            var off = new LevelValues { InALine = false, RoomMood = false, Casters = false, Templates = false, Passages = false, ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false }; // work order 054: version 9 had the casters off; 058: nor templates; 059: nor passages; 087: nor the act's plan; 103: nor the exit rooms of their own
            var print = new Print();
            ulong described = 14695981039346656037UL;
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, off);
                Assert.That(level.InALine, Is.False);
                print.Add(level);
                foreach (byte b in Encoding.UTF8.GetBytes(level.Describe().Replace("version " + LevelGenerator.Version + " ", "version 9 ")))
                {
                    described ^= b;
                    described *= 1099511628211UL;
                }
            }
            TestContext.WriteLine("VERSION9 floor " + floor + " print " + print.Hash.ToString("X16") + " described " + described.ToString("X16"));
            string fields = print.Hash.ToString("X16"), whole = described.ToString("X16");
            string both = "every field " + fields + " (version 9's " + Version9[floor].print + "), every whole description " + whole
                          + " (the Editor's pin " + Version9[floor].described + ")";
            Assert.That(fields, Is.EqualTo(Version9[floor].print), "every field of a thousand floors, against version 9's: " + both);
            Assert.That(whole, Is.EqualTo(Version9[floor].described), "every floor's whole description, against the Editor's print of it: " + both);
        }

        // ---------------- The validator's rule of the row ----------------

        static LevelLayout Line(ulong seed = 42UL) => LevelGenerator.Generate(seed, new LevelValues());

        static void AssertFault(LevelLayout level, LevelValues values, string expected)
        {
            List<string> faults = LevelValidator.Validate(level, values);
            Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  " + string.Join("\n  ", faults) + "\n" + LevelMap.Draw(level));
        }

        [Test]
        public void Validator_ReportsARoomOfTheRowWithThreeDoors()
        {
            LevelLayout level = Line();
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            LevelRoom t = level.Rooms.First(r => r.Kind == RoomKind.Treasure);
            LevelDoor d = level.DoorsOf(t.Order).First();
            level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = t.Order, Vertical = d.Vertical, Line = d.Line, From = d.From, Width = d.Width });
            AssertFault(level, new LevelValues(), "has 3 doors, and a room of the row has 2");
        }

        [Test]
        public void Validator_ReportsADeadEndOnTheRow()
        {
            LevelLayout level = Line();
            LevelRoom t = level.Rooms.First(r => r.Kind == RoomKind.Treasure);
            level.Doors.RemoveAll(d => d.Joins(t.Order) && d.Other(t.Order) == t.Order + 1);
            AssertFault(level, new LevelValues(), "has 1 doors, and a room of the row has 2");
        }

        [Test]
        public void Validator_ReportsATreasureRoomOutOfItsPlaceOnTheRow()
        {
            LevelLayout level = Line();
            LevelRoom t = level.Rooms.First(r => r.Kind == RoomKind.Treasure), c = level.Rooms.First(r => r.Kind == RoomKind.Combat); // work order 059: past the first passage
            Assume.That(c.Kind, Is.EqualTo(RoomKind.Combat));
            t.Kind = RoomKind.Combat;
            c.Kind = RoomKind.Treasure;
            AssertFault(level, new LevelValues(), "the main path runs");
        }

        [Test]
        public void Validator_WithTheSwitchOn_ReportsAFloorWithSideRooms()
        {
            LevelLayout sides = LevelGenerator.Generate(42UL, new LevelValues { InALine = false });
            Assume.That(LevelValidator.Validate(sides, new LevelValues { InALine = false }), Is.Empty);
            AssertFault(sides, new LevelValues(), "the floor has side rooms and the values' InALine is on");
            // And read as a row, as it would have to be: its rooms are out of the row, and its treasure rooms are dead ends.
            sides.InALine = true;
            AssertFault(sides, new LevelValues(), "the main path runs");
            AssertFault(sides, new LevelValues(), "doors, and a room of the row has 2");
        }

        [Test]
        public void Validator_FindsNothingWrong_WithAFloorInALine_AndItsMapReads()
        {
            LevelLayout level = Line();
            Assert.That(LevelValidator.Validate(level), Is.Empty);
            string map = LevelMap.Draw(level);
            // Work order 059: a passage's label is its letter alone.
            foreach (LevelRoom r in level.Rooms) Assert.That(map, Does.Contain(r.Kind == RoomKind.Passage ? "P" : r.Letter + r.Order.ToString(s_Inv)), map);
            // Work order 082 (review 6, finding 31): a P for each passage, not one P anywhere for all of them.
            int passages = level.Rooms.Count(r => r.Kind == RoomKind.Passage);
            Assert.That(passages, Is.GreaterThan(0));
            Assert.That(map.Count(ch => ch == 'P'), Is.EqualTo(passages), "a P for each of the " + passages + " passages\n" + map);
            Assert.That(map, Does.Contain("version " + LevelGenerator.Version + " "));
            Assert.That(level.Describe(), Does.Contain(" in a line with passages\n"));
            Assert.That(LevelGenerator.Generate(42UL, new LevelValues { Passages = false }).Describe(), Does.Contain(" in a line\n"));
            Assert.That(LevelGenerator.Generate(42UL, new LevelValues { InALine = false }).Describe(), Does.Not.Contain("in a line"));
        }

        // ---------------- The door signs' rules ----------------

        static LevelRoom Room(RoomKind kind, int order, bool elite = false) => new LevelRoom { Kind = kind, Order = order, Elite = elite };

        [Test]
        public void ADoorsSign_IsTheRoomItLeadsOnTo_GoldRedAndTheExitsOwn()
        {
            Assert.That(DoorSignRules.SignOf(Room(RoomKind.Treasure, 3), 1, 5), Is.EqualTo(DoorSign.Treasure));
            Assert.That(DoorSignRules.SignOf(Room(RoomKind.Combat, 4, elite: true), 1, 5), Is.EqualTo(DoorSign.Elite));
            Assert.That(DoorSignRules.SignOf(Room(RoomKind.Combat, 2), 3, 5), Is.EqualTo(DoorSign.None), "an ordinary combat room: nothing");
            Assert.That(DoorSignRules.SignOf(Room(RoomKind.Entrance, 0), 3, 5), Is.EqualTo(DoorSign.None));
            for (int floor = 1; floor <= 4; floor++) Assert.That(DoorSignRules.SignOf(Room(RoomKind.Exit, 7), floor, 5), Is.EqualTo(DoorSign.Exit));
            Assert.That(DoorSignRules.SignOf(Room(RoomKind.Exit, 7), 5, 5), Is.EqualTo(DoorSign.Boss), "the act's last floor: the boss");
            Assert.That(DoorSignRules.SignOf(null, 1, 5), Is.EqualTo(DoorSign.None));

            var door = new LevelDoor { RoomA = 2, RoomB = 3 };
            Assert.That(DoorSignRules.Beyond(door), Is.EqualTo(3));
            Assert.That(DoorSignRules.Before(door), Is.EqualTo(2));
            Assert.That(DoorSignRules.Beyond(new LevelDoor { RoomA = 5, RoomB = 4 }), Is.EqualTo(5), "the later room, whichever is A");

            var v = new DoorSignValues();
            Rgb gold = DoorSignRules.ColourOf(DoorSign.Treasure, v), red = DoorSignRules.ColourOf(DoorSign.Elite, v), exit = DoorSignRules.ColourOf(DoorSign.Exit, v);
            Assert.That(gold.R > gold.B && gold.G > gold.B && gold.G > 0.5f, "gold: warm, " + gold);
            Assert.That(red.R > 0.8f && red.G < 0.3f && red.B < 0.3f, "red: " + red);
            Assert.That(exit.B > exit.G && exit.R > exit.G, "the exit's: violet, " + exit);
            Rgb dim = DoorSignRules.DimOf(gold, v);
            Assert.That(dim.R + dim.G + dim.B, Is.LessThan(0.5f * (gold.R + gold.G + gold.B)), "dim is darker");
        }

        [Test]
        public void ASign_IsLit_FromTheRoomBefore_UntilTheRoomBeyondIsDone_TheDoorBackAlwaysDim()
        {
            var door = new LevelDoor { RoomA = 2, RoomB = 3 };
            Assert.That(DoorSignRules.Lit(DoorSign.Treasure, door, here: 2, beyondDone: false), Is.True, "the door on, seen from her room");
            Assert.That(DoorSignRules.Lit(DoorSign.Treasure, door, here: 3, beyondDone: false), Is.False, "the door she came in by: dim");
            Assert.That(DoorSignRules.Lit(DoorSign.Treasure, door, here: 4, beyondDone: false), Is.False, "a door behind her");
            Assert.That(DoorSignRules.Lit(DoorSign.Treasure, door, here: -1, beyondDone: false), Is.False, "outside the rooms");
            Assert.That(DoorSignRules.Lit(DoorSign.Treasure, door, here: 2, beyondDone: true), Is.False, "the room beyond is done: dim");
            Assert.That(DoorSignRules.Lit(DoorSign.None, door, here: 2, beyondDone: false), Is.False, "no sign, nothing lit");

            Assert.That(DoorSignRules.Done(RoomKind.Treasure, chestOpened: true, cleared: false), Is.True, "a treasure room: its chest opened");
            Assert.That(DoorSignRules.Done(RoomKind.Treasure, chestOpened: false, cleared: true), Is.False);
            Assert.That(DoorSignRules.Done(RoomKind.Combat, chestOpened: false, cleared: true), Is.True, "any other: cleared");
            Assert.That(DoorSignRules.Done(RoomKind.Exit, chestOpened: true, cleared: false), Is.False);
        }

        [Test]
        public void TheSigns_AreConvexPiecesInsideTheSquare_GoingRoundAnticlockwise_AndEachIsItsOwn()
        {
            var seen = new HashSet<string>();
            foreach (DoorSign sign in new[] { DoorSign.Treasure, DoorSign.Elite, DoorSign.Exit, DoorSign.Boss })
            {
                List<double[]> parts = DoorSignRules.ShapeOf(sign);
                Assert.That(parts, Is.Not.Empty, sign.ToString());
                var picture = new StringBuilder();
                for (int y = 0; y < 12; y++)
                    for (int x = 0; x < 12; x++) picture.Append(DoorSignRules.Inside(parts, -1 + (x + 0.5) / 6.0, 1 - (y + 0.5) / 6.0) ? '#' : '.');
                Assert.That(seen.Add(picture.ToString()), "each sign its own shape: " + sign);
                foreach (double[] p in parts)
                {
                    Assert.That(p.Length % 2 == 0 && p.Length >= 6, sign.ToString());
                    Assert.That(p.All(c => c >= -1 && c <= 1), "inside the square: " + sign);
                    Assert.That(DoorSignRules.TwiceArea(p), Is.GreaterThan(0), "anticlockwise: " + sign);
                    double[] small = DoorSignRules.Shrunk(p, 0.5);
                    Assert.That(DoorSignRules.TwiceArea(small), Is.EqualTo(DoorSignRules.TwiceArea(p) * 0.25).Within(1e-9), "a quarter the area at half the size");
                    int n = p.Length / 2;
                    for (int i = 0; i < n; i++)
                    {
                        // Convex: every corner turns left.
                        double ax = p[2 * i], ay = p[2 * i + 1], bx = p[2 * ((i + 1) % n)], by = p[2 * ((i + 1) % n) + 1];
                        double cx = p[2 * ((i + 2) % n)], cy = p[2 * ((i + 2) % n) + 1];
                        Assert.That((bx - ax) * (cy - by) - (by - ay) * (cx - bx), Is.GreaterThanOrEqualTo(-1e-9), "convex: " + sign);
                    }
                }
            }
            Assert.That(DoorSignRules.ShapeOf(DoorSign.None), Is.Empty);
        }

        // ---------------- The small map's rules ----------------

        [Test]
        public void TheMap_ShowsARoomHere_Entered_OneStepAhead_OrNotAtAll()
        {
            Assert.That(FloorMapRules.Shown(isHere: true, entered: true, beforeEntered: true), Is.EqualTo(MapShown.Here));
            Assert.That(FloorMapRules.Shown(isHere: false, entered: true, beforeEntered: true), Is.EqualTo(MapShown.Entered));
            Assert.That(FloorMapRules.Shown(isHere: false, entered: false, beforeEntered: true), Is.EqualTo(MapShown.Ahead), "the next room, once the one before it is entered");
            Assert.That(FloorMapRules.Shown(isHere: false, entered: false, beforeEntered: false), Is.EqualTo(MapShown.Hidden), "two steps ahead: nothing");

            Assert.That(FloorMapRules.Filled(MapShown.Here) && FloorMapRules.Filled(MapShown.Entered), Is.True);
            Assert.That(FloorMapRules.Filled(MapShown.Ahead) || FloorMapRules.Filled(MapShown.Hidden), Is.False, "ahead: an outline only");
            Assert.That(FloorMapRules.Dim(MapShown.Entered, done: true), Is.True, "a cleared room is filled dim");
            Assert.That(FloorMapRules.Dim(MapShown.Entered, done: false), Is.False);
            Assert.That(FloorMapRules.Dim(MapShown.Here, done: true), Is.False, "the room she is in is lit");

            Assert.That(FloorMapRules.DoorShown(MapShown.Entered, MapShown.Ahead), Is.True);
            Assert.That(FloorMapRules.DoorShown(MapShown.Ahead, MapShown.Hidden), Is.False);
        }

        [Test]
        public void TheMap_ShowsARoomsSign_UntilItIsDone_ATreasureRoomThenLosesItsGold()
        {
            var v = new DoorSignValues();
            Assert.That(FloorMapRules.SignStrength(DoorSign.Treasure, MapShown.Ahead, RoomKind.Treasure, false, v), Is.EqualTo(1f), "seen ahead: its gold and its sign");
            Assert.That(FloorMapRules.SignStrength(DoorSign.Treasure, MapShown.Entered, RoomKind.Treasure, true, v), Is.EqualTo(0f), "its chest opened: no gold");
            Assert.That(FloorMapRules.SignStrength(DoorSign.Elite, MapShown.Entered, RoomKind.Combat, true, v), Is.EqualTo(v.DimShare), "cleared: dim");
            Assert.That(FloorMapRules.SignStrength(DoorSign.Elite, MapShown.Hidden, RoomKind.Combat, false, v), Is.EqualTo(0f), "not shown: nothing");
            Assert.That(FloorMapRules.SignStrength(DoorSign.None, MapShown.Here, RoomKind.Combat, false, v), Is.EqualTo(0f));
        }

        [Test]
        public void TheMap_SeesOneStepAheadAlongTheRow_AndFitsTheFloorInItsCorner()
        {
            // Work order 059: the rooms touching; past a passage, PassagesTests.TheMapsRoomBefore_PassesOverAPassage_AndTheRoomNumbersSkipThem.
            LevelLayout level = LevelGenerator.Generate(42UL, new LevelValues { Passages = false });
            for (int i = 0; i < level.Rooms.Count; i++) Assert.That(FloorMapRules.Before(level, level.Rooms[i]), Is.EqualTo(i - 1), "room " + i);
            // With side rooms, a treasure room is seen from its combat room.
            LevelLayout sides = LevelGenerator.Generate(42UL, new LevelValues { InALine = false });
            foreach (LevelRoom t in sides.Rooms.Where(r => r.Kind == RoomKind.Treasure))
                Assert.That(sides.Rooms[FloorMapRules.Before(sides, t)].Kind, Is.EqualTo(RoomKind.Combat));

            FloorMapRules.Fit(100, 80, 120, 200, out double w, out double h, out double scale);
            Assert.That(h, Is.EqualTo(120).Within(1e-9));
            Assert.That(w, Is.EqualTo(150).Within(1e-9));
            FloorMapRules.Fit(300, 80, 120, 200, out w, out h, out scale);
            Assert.That(w, Is.EqualTo(200).Within(1e-9), "no wider than its most");
            Assert.That(h, Is.LessThan(120));
            Assert.That(scale, Is.EqualTo(200 / 300.0).Within(1e-9));
            Assert.That(new DoorSignValues().MapHeightShare, Is.EqualTo(1f / 6f).Within(1e-6), "about a sixth of the screen's height");
        }
    }
}
