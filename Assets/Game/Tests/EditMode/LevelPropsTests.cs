using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The props of a generated level (work order 009): the same seed gives the same props; over a thousand seeds every
    /// level is valid with them, quickly, and its rooms, doors and enemy points are those it has without them; the props
    /// stream leaves the other stages alone; each room kind gets its set; and the validator reports each prop fault.
    /// </summary>
    public class LevelPropsTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static LevelLayout Make(ulong seed, int propsWarmUp = 0) => LevelGenerator.Generate(seed, new LevelValues(), 0, propsWarmUp);

        /// <summary>Work order 028: with the room themes off, the rooms are dressed as before them; the sets of work order 009 are tested so.</summary>
        // Work order 051: and with the rooms' carved outlines off, as the sets were made for the rectangles (a cross or an
        // octagon takes the exit room's corners, where its four pillars stood).
        static LevelLayout MakeWithoutThemes(ulong seed) => LevelGenerator.Generate(seed, new LevelValues { Themes = false, Shapes = false });

        static int PropCount(LevelLayout l) => l.Rooms.Sum(r => r.Props.Count);

        // ---------------- The same seed, the same props ----------------

        [Test]
        public void SameSeed_GivesTheSameProps()
        {
            foreach (ulong s in new[] { 0UL, 42UL, 12345UL, ulong.MaxValue })
            {
                LevelLayout a = Make(s), b = Make(s);
                Assert.That(PropCount(a), Is.GreaterThan(10), "seed " + s);
                Assert.That(b.Describe(), Is.EqualTo(a.Describe()), "seed " + s);
                Assert.That(string.Join("\n", b.Rooms.SelectMany(r => r.Props).Select(p => p.ToString())),
                            Is.EqualTo(string.Join("\n", a.Rooms.SelectMany(r => r.Props).Select(p => p.ToString()))));
            }
        }

        [Test]
        public void PropsStream_IsIndependent_OfTheLayoutAndTheContents()
        {
            int propsChanged = 0;
            for (ulong s = 0; s < 60; s++)
            {
                LevelLayout plain = Make(s), shifted = Make(s, propsWarmUp: 5);
                Assert.That(shifted.DescribeLayout(), Is.EqualTo(plain.DescribeLayout()), "layout of seed " + s);
                Assert.That(shifted.Describe(props: false), Is.EqualTo(plain.Describe(props: false)), "rooms, doors and enemy points of seed " + s);
                Assert.That(LevelValidator.Validate(shifted), Is.Empty, "seed " + s);
                if (shifted.Describe() != plain.Describe()) propsChanged++;
            }
            Assert.That(propsChanged, Is.GreaterThan(50), "the warm-up did change the props, so the check above means something");

            // Draws from the props stream move neither of the others, and its first draw is its own.
            ulong layoutFirst = LevelRandom.Stream(9UL, LevelGenerator.LayoutStage).NextBits();
            ulong contentsFirst = LevelRandom.Stream(9UL, LevelGenerator.ContentsStage).NextBits();
            LevelRandom p = LevelRandom.Stream(9UL, LevelGenerator.PropsStage);
            ulong propsFirst = p.NextBits();
            for (int i = 0; i < 10; i++) p.NextBits();
            Assert.That(LevelRandom.Stream(9UL, LevelGenerator.LayoutStage).NextBits(), Is.EqualTo(layoutFirst));
            Assert.That(LevelRandom.Stream(9UL, LevelGenerator.ContentsStage).NextBits(), Is.EqualTo(contentsFirst));
            Assert.That(propsFirst, Is.Not.EqualTo(layoutFirst).And.Not.EqualTo(contentsFirst));
        }

        [Test]
        public void PropsOff_GivesNoProps_AndTheSameLevelOtherwise()
        {
            var off = new LevelValues { Props = false };
            for (ulong s = 0; s < 30; s++)
            {
                LevelLayout without = LevelGenerator.Generate(s, off), with = Make(s);
                Assert.That(PropCount(without), Is.EqualTo(0));
                Assert.That(LevelValidator.Validate(without, off), Is.Empty);
                Assert.That(with.Describe(props: false), Is.EqualTo(without.Describe(props: false)), "seed " + s);
            }
        }

        // ---------------- A thousand seeds ----------------

        /// <summary>The longest one generation with props may take, in milliseconds. Measured: see the output of the test.</summary>
        const double SlowestAllowedMs = 150; // 50 until 2026-11-20: see LevelGeneratorTests, the same bound for the same reason (main and work order 028 alike)

        [Test]
        public void AThousandSeeds_AllValidWithProps_KeepTheirRoomsAndEnemies_AndAreQuick()
        {
            var off = new LevelValues { Props = false };
            Make(424242UL); // the first call pays for compiling the code
            int least = int.MaxValue, most = 0, leastSeed = 0, mostSeed = 0;
            long total = 0;
            double slowest = 0, time = 0;
            ulong slowestSeed = 0;
            int rooms = 0, roomsShort = 0, roomsShortByLane = 0, propsShort = 0;
            var shortByKind = new Dictionary<RoomKind, int>();
            var byKind = new Dictionary<RoomKind, (int rooms, int props)>();
            var timer = new Stopwatch();
            for (ulong s = 0; s < 1000; s++)
            {
                timer.Restart();
                LevelLayout level = Make(s);
                timer.Stop();
                double ms = timer.Elapsed.TotalMilliseconds;
                time += ms;
                if (ms > slowest) { slowest = ms; slowestSeed = s; }

                Assert.That(LevelValidator.Validate(level), Is.Empty, "seed " + s + "\n" + LevelMap.Draw(level));
                Assert.That(level.Describe(props: false), Is.EqualTo(LevelGenerator.Generate(s, off).Describe(props: false)),
                            "seed " + s + ": the props changed its rooms, doors or enemy points");

                int n = PropCount(level);
                total += n;
                if (n < least) { least = n; leastSeed = (int)s; }
                if (n > most) { most = n; mostSeed = (int)s; }
                foreach (LevelRoom r in level.Rooms)
                {
                    rooms++;
                    byKind.TryGetValue(r.Kind, out var k);
                    byKind[r.Kind] = (k.rooms + 1, k.props + r.Props.Count);
                    if (r.Props.Count >= r.PropsWanted) continue;
                    roomsShort++;
                    propsShort += r.PropsWanted - r.Props.Count;
                    shortByKind.TryGetValue(r.Kind, out int sk);
                    shortByKind[r.Kind] = sk + 1;
                    if (r.PropsLeftOutByLane > 0) roomsShortByLane++;
                }
            }
            TestContext.WriteLine(string.Format(s_Inv,
                "PROPSTATS seeds 0-999: props per level least {0} (seed {1}), most {2} (seed {3}), mean {4:0.00}; " +
                "per room mean {5}; rooms with fewer props than asked {6} of {7} ({8} props), by kind [{9}], of which with a place refused by the lane {10}; " +
                "slowest generation {11:0.00} ms (seed {12}), mean {13:0.000} ms",
                least, leastSeed, most, mostSeed, total / 1000.0,
                string.Join(", ", byKind.OrderBy(k => k.Key).Select(k => k.Key + " " + (k.Value.props / (double)k.Value.rooms).ToString("0.00", s_Inv))),
                roomsShort, rooms, propsShort,
                string.Join(", ", shortByKind.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)), roomsShortByLane,
                slowest, slowestSeed, time / 1000));
            Assert.That(slowest, Is.LessThan(SlowestAllowedMs), "slowest generation with props, ms");
            Assert.That(least, Is.GreaterThan(0));
        }

        // Work order 028: with the themes off (RoomThemeTests has the themes' sets).
        [Test]
        public void EachKindOfRoom_GetsItsSet()
        {
            int startsWhole = 0, rewardsWhole = 0, bossesWhole = 0, combats = 0, rewards = 0, seeds = 300;
            var v = new PropValues();
            for (ulong s = 0; s < (ulong)seeds; s++)
            {
                LevelLayout level = MakeWithoutThemes(s);
                double cs = level.CellSize;
                foreach (LevelRoom r in level.Rooms)
                {
                    var models = r.Props.Select(p => p.Model).ToList();
                    switch (r.Kind)
                    {
                        case RoomKind.Entrance:
                            Assert.That(models, Is.All.EqualTo(PropModel.Brazier), "seed " + s);
                            if (models.Count != 2) break;
                            // Either side of the door she leaves by, near it.
                            LevelDoor exit = level.DoorsOf(r.Order).First();
                            exit.Opening(cs, out LevelPoint a, out LevelPoint b);
                            double Along(LevelPoint p) => exit.Vertical ? p.Y : p.X;
                            double lo = Math.Min(Along(a), Along(b)), hi = Math.Max(Along(a), Along(b));
                            var sides = r.Props.Select(p => Along(p.Position) < lo ? -1 : Along(p.Position) > hi ? 1 : 0).OrderBy(x => x).ToList();
                            bool near = r.Props.All(p => LevelGeometry.DistanceToDoor(p.Position, exit, cs) < 6.0);
                            if (sides.SequenceEqual(new[] { -1, 1 }) && near) startsWhole++;
                            break;
                        case RoomKind.Treasure:
                            rewards++;
                            if (models.Count == 3 && models[0] == PropModel.Chest && models.Count(m => m == PropModel.Brazier) == 2
                                && Math.Abs(r.Props[0].Position.DistanceTo(r.Middle(cs)) - v.ChestBack) < 0.5) rewardsWhole++;
                            break;
                        case RoomKind.Exit:
                            if (models.Count(m => m == PropModel.Altar) == 1 && models.Count(m => m == PropModel.Statue) == 2
                                && models.Count(m => m == PropModel.Pillar) == 4 && r.Props.First(p => p.Model == PropModel.Altar).Place == PropPlace.Wall)
                                bossesWhole++;
                            break;
                        case RoomKind.Combat:
                            combats++;
                            Assert.That(r.PropsWanted, Is.InRange(v.CombatFewest, v.CombatMost), "seed " + s);
                            Assert.That(models.Count(m => m == PropModel.Brazier), Is.LessThanOrEqualTo(v.CornerBraziers), "seed " + s);
                            foreach (LevelProp p in r.Props.Where(p => p.Model != PropModel.Brazier))
                                Assert.That(p.Place == PropPlace.Wall ? v.WallModels : v.FloorModels, Does.Contain(p.Model), "seed " + s + " " + p);
                            break;
                    }
                }
            }
            TestContext.WriteLine(string.Format(s_Inv,
                "PROPSETS seeds 0-{0}: start rooms with both braziers by the door {1}; reward rooms with chest and two braziers {2} of {3}; " +
                "boss rooms with altar, two statues and four pillars {4}", seeds - 1, startsWhole, rewardsWhole, rewards, bossesWhole));
            Assert.That(startsWhole, Is.GreaterThanOrEqualTo(seeds * 95 / 100), "start rooms with their two braziers");
            Assert.That(rewardsWhole, Is.GreaterThanOrEqualTo(rewards * 95 / 100), "reward rooms with their set");
            Assert.That(bossesWhole, Is.GreaterThanOrEqualTo(seeds * 90 / 100), "boss rooms with their set");
        }

        // Work order 028: with the themes off (a theme's counts grow with the room's size too: RoomThemeTests).
        [Test]
        public void BiggerCombatRooms_AskForMoreProps()
        {
            var asked = new Dictionary<int, List<int>>();
            for (ulong s = 0; s < 100; s++)
                foreach (LevelRoom r in MakeWithoutThemes(s).Rooms.Where(r => r.Kind == RoomKind.Combat))
                {
                    int cells = r.Rect.Width * r.Rect.Height;
                    if (!asked.TryGetValue(cells, out List<int> list)) asked[cells] = list = new List<int>();
                    list.Add(r.PropsWanted);
                }
            Assert.That(asked[25].Max(), Is.EqualTo(4), "the smallest room");
            Assert.That(asked[49].Min(), Is.EqualTo(8), "the largest room");
            int previous = 0;
            foreach (int cells in asked.Keys.OrderBy(c => c))
            {
                Assert.That(asked[cells].Min(), Is.GreaterThanOrEqualTo(previous), cells + " cells");
                previous = asked[cells].Max();
            }
        }

        // ---------------- The validator ----------------

        const ulong Sample = 42UL;

        static LevelRoom First(LevelLayout l, RoomKind kind) => l.Rooms.First(r => r.Kind == kind);

        static void AssertFault(Action<LevelLayout> breakIt, string expected)
        {
            LevelLayout level = Make(Sample);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            breakIt(level);
            List<string> faults = LevelValidator.Validate(level);
            Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  "
                        + string.Join("\n  ", faults) + "\n" + LevelMap.Draw(level));
        }

        static LevelPoint Shift(LevelPoint p, double dx, double dy) => new LevelPoint(p.X + dx, p.Y + dy);

        static LevelPoint TowardMiddle(LevelLayout l, LevelRoom r, LevelPoint p, double by)
        {
            LevelPoint m = r.Middle(l.CellSize);
            double d = p.DistanceTo(m);
            return d < 1e-9 ? p : new LevelPoint(p.X + (m.X - p.X) / d * by, p.Y + (m.Y - p.Y) / d * by);
        }

        [Test]
        public void Validator_FindsNothingWrong_WithTheSamplesProps()
        {
            LevelLayout level = Make(Sample);
            Assert.That(PropCount(level), Is.GreaterThan(10));
            Assert.That(First(level, RoomKind.Entrance).Props.Count, Is.EqualTo(2), "the sample has its start braziers");
            Assert.That(level.Rooms.Any(r => r.Kind == RoomKind.Combat && r.Props.Any(p => p.Place == PropPlace.Floor)), "and an open-floor prop");
            Assert.That(LevelValidator.Validate(level), Is.Empty);
        }

        [Test]
        public void Validator_ReportsAPropTurnedOffItsSteps() =>
            AssertFault(l => First(l, RoomKind.Entrance).Props[0].Rotation = 45, "is turned 45 degrees, not a multiple of 90");

        [Test]
        public void Validator_ReportsAPillarTurnedOffItsSteps() =>
            AssertFault(l => First(l, RoomKind.Exit).Props.First(p => p.Model == PropModel.Pillar).Rotation = 20, "not a multiple of 15");

        [Test]
        public void Validator_ReportsAPropAtAWallThatMayNotStandAtOne() =>
            AssertFault(l => First(l, RoomKind.Entrance).Props[0].Place = PropPlace.Wall, "stands at a wall, and may not");

        [Test]
        public void Validator_ReportsAPropOutsideItsRoom() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Treasure);
                r.Props[0].Position = new LevelPoint(r.Rect.Right * l.CellSize + 0.2, r.Middle(l.CellSize).Y);
            }, "reaches out of its room");

        [Test]
        public void Validator_ReportsAPropTooNearAWall() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
                LevelProp p = r.Props[0];
                p.Position = new LevelPoint(r.Rect.X * l.CellSize + p.Radius + 0.2, r.Middle(l.CellSize).Y + 3);
            }, "is nearer a wall than 0.5 m");

        [Test]
        public void Validator_ReportsAWallPropAwayFromTheWalls() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Exit);
                // Work order 028: the throne room's wall prop is the throne (the altar until then).
                // Work order 058: 2 m off the middle, inside the open square every exit room keeps (9 m off it is by a carved corner of the throne arcade).
                r.Props.First(p => p.Place == PropPlace.Wall).Position = Shift(r.Middle(l.CellSize), 2, 2);
            }, "is meant to stand at a wall");

        [Test]
        public void Validator_ReportsAPropTooNearADoor() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
                LevelDoor d = l.DoorsOf(r.Order).First();
                d.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                var mid = new LevelPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                r.Props[0].Position = TowardMiddle(l, r, mid, 2.0);
            }, "is nearer door");

        [Test]
        public void Validator_ReportsAPropTooNearThePlayersStart() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
                r.Props[0].Position = Shift(r.PlayerStart.Value, 1.5, 0);
            }, "is nearer the player's start than 2 m");

        [Test]
        public void Validator_ReportsAPropTooNearAnEnemyPoint() =>
            AssertFault(l =>
            {
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat && x.EnemyPoints.Count > 0 && x.Props.Count > 0);
                r.Props[0].Position = TowardMiddle(l, r, r.EnemyPoints[0], 0.5);
            }, "is nearer enemy point");

        [Test]
        public void Validator_ReportsAPropInTheBossRoomsMiddle() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Exit);
                r.Props.First(p => p.Model == PropModel.Pillar).Position = Shift(r.Middle(l.CellSize), 5, 3);
            }, "is nearer the exit room's middle than 8 m");

        [Test]
        public void Validator_ReportsPropsThatOverlap() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Treasure);
                LevelProp p = r.Props[0];
                r.Props.Add(LevelProp.Make(p.Model, Shift(p.Position, 0.3, 0), p.Rotation, p.Place));
            }, "overlaps the Chest");

        [Test]
        public void Validator_ReportsOpenFloorPropsNearerThanTheirSpacing() =>
            AssertFault(l =>
            {
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat && x.Props.Any(p => p.Place == PropPlace.Floor));
                LevelProp f = r.Props.First(p => p.Place == PropPlace.Floor);
                // A broken pillar 2.4 m from its middle: clear of it (its footprint reaches 1.1 m at most), but nearer than 3 m.
                r.Props.Add(LevelProp.Make(PropModel.PillarBroken, TowardMiddle(l, r, f.Position, 2.4), 0, PropPlace.Floor));
            }, "and the open floor keeps 3 m");

        [Test]
        public void Validator_ReportsAPropThatDoesNotBelongInItsRoom() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
                LevelProp p = r.Props[0];
                r.Props[0] = LevelProp.Make(PropModel.Statue, p.Position, 0, PropPlace.Set);
            }, "does not belong in a Entrance room");

        [Test]
        public void Validator_ReportsAFootprintThatIsNotItsModels() =>
            AssertFault(l => First(l, RoomKind.Treasure).Props[0].Width = 0.2, "has a footprint that is not its model's");

        [Test]
        public void Validator_ReportsTooManyPropsInARoom() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
                r.Props.Add(LevelProp.Make(PropModel.Brazier, Shift(r.PlayerStart.Value, 4, 4), 0, PropPlace.Set));
            }, "has 3 props, a Entrance room at most 2");

        [Test]
        public void Validator_ReportsABlockedLane_ToTheMiddle() =>
            AssertFault(l =>
            {
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat);
                r.Props.Add(LevelProp.Make(PropModel.Pillar, r.Middle(l.CellSize), 0, PropPlace.Floor));
            }, "has no clear lane 3 m wide");

        [Test]
        public void Validator_ReportsABlockedLane_BetweenDoors()
        {
            AssertFault(l =>
            {
                // A row of sarcophagi across the room, wall to wall, between the door she enters by and the room's others.
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat && l.DoorsOf(x.Order).Count() >= 2);
                LevelDoor entry = LevelGeometry.EntryDoor(l, r);
                double cs = l.CellSize;
                CellRect c = r.Rect;
                bool across = entry.Vertical; // a door in a vertical wall: the row runs north and south
                double at = entry.Vertical
                    ? (entry.Line == c.X ? c.X * cs + 3.6 : c.Right * cs - 3.6)
                    : (entry.Line == c.Y ? c.Y * cs + 3.6 : c.Top * cs - 3.6);
                double from = (across ? c.Y : c.X) * cs, to = (across ? c.Top : c.Right) * cs;
                for (double s = from + 1.0; s < to - 0.5; s += 1.9)
                    r.Props.Add(LevelProp.Make(PropModel.Sarcophagus, across ? new LevelPoint(at, s) : new LevelPoint(s, at), across ? 90 : 0, PropPlace.Set));
            }, "has no clear lane");
        }

        // ---------------- Footprints ----------------

        [Test]
        public void Footprint_Distances()
        {
            var circle = PropFootprint.Of(PropSize.Of(PropModel.Pillar), new LevelPoint(0, 0), 0);
            var rect = PropFootprint.Of(PropSize.Of(PropModel.Sarcophagus), new LevelPoint(5, 0), 0); // 1.9 along x, 1.1 along y
            Assert.That(circle.DistanceTo(new LevelPoint(2, 0)), Is.EqualTo(1.4).Within(1e-9));
            Assert.That(rect.DistanceTo(new LevelPoint(5, 2)), Is.EqualTo(2 - 0.55).Within(1e-9));
            Assert.That(circle.DistanceTo(rect), Is.EqualTo(5 - 0.95 - 0.6).Within(1e-9));
            var turned = PropFootprint.Of(PropSize.Of(PropModel.Sarcophagus), new LevelPoint(5, 0), 90); // now 1.9 along y
            Assert.That(turned.DistanceTo(new LevelPoint(5, 2)), Is.EqualTo(2 - 0.95).Within(1e-9));
            Assert.That(turned.DistanceTo(new LevelPoint(4, 0)), Is.EqualTo(1 - 0.55).Within(1e-9));
            Assert.That(rect.DistanceTo(turned), Is.EqualTo(0), "overlapping");
            var far = PropFootprint.Of(PropSize.Of(PropModel.Sarcophagus), new LevelPoint(10, 0), 45);
            Assert.That(rect.DistanceTo(far), Is.GreaterThan(2.5).And.LessThan(5 - 0.95));
            Assert.That(rect.DistanceTo(new LevelPoint(0, 3), new LevelPoint(10, 3)), Is.EqualTo(3 - 0.55).Within(1e-9));
            Assert.That(rect.DistanceTo(new LevelPoint(0, 0), new LevelPoint(10, 0)), Is.EqualTo(0), "a segment through it");
            Assert.That(PropRules.Facing(0, 1), Is.EqualTo(0));
            Assert.That(PropRules.Facing(1, 0), Is.EqualTo(90));
            Assert.That(PropRules.Facing(0, -1), Is.EqualTo(180));
            Assert.That(PropRules.Facing(-1, 0), Is.EqualTo(270));
        }
    }
}
