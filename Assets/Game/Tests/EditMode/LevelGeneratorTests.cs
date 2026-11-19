using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The level generator, its validator and its text map (work order 006): the same seed gives the same level, every
    /// seed gives a valid one, seeds differ, three golden seeds keep their maps until the version is raised, the
    /// validator reports each fault it lists, and the seed's text and the stages' streams behave.
    /// </summary>
    public class LevelGeneratorTests
    {
        static LevelLayout Make(ulong seed, int warmUp = 0) => LevelGenerator.Generate(seed, new LevelValues(), warmUp);

        // ---------------- Randomness and seeds ----------------

        [Test]
        public void Random_IsSplitMix64()
        {
            // The reference outputs of SplitMix64 started at 0.
            var r = new LevelRandom(0UL);
            Assert.That(r.NextBits(), Is.EqualTo(0xE220A8397B1DCDAFUL));
            Assert.That(r.NextBits(), Is.EqualTo(0x6E789E6AA1B965F4UL));
            Assert.That(r.NextBits(), Is.EqualTo(0x06C45D188009454FUL));
        }

        [Test]
        public void Random_BelowAndRange_StayInBounds_AndReachEveryValue()
        {
            var r = LevelRandom.Stream(99UL, "test");
            var seen = new HashSet<int>();
            for (int i = 0; i < 2000; i++)
            {
                int v = r.Range(5, 7);
                Assert.That(v, Is.InRange(5, 7));
                seen.Add(v);
                Assert.That(r.Below(3), Is.InRange(0, 2));
            }
            Assert.That(seen, Is.EquivalentTo(new[] { 5, 6, 7 }));
        }

        [Test]
        public void SeedText_RoundTrips()
        {
            var seeds = new List<ulong> { 0UL, 1UL, 31UL, 32UL, LevelSeed.ShortMask, LevelSeed.ShortMask + 1, ulong.MaxValue, 0xDEADBEEFUL };
            var r = LevelRandom.Stream(7UL, "seeds");
            for (int i = 0; i < 500; i++) seeds.Add(r.NextBits());
            foreach (ulong s in seeds)
            {
                string text = LevelSeed.ToText(s);
                Assert.That(text.Length, Is.InRange(LevelSeed.ShortLength, LevelSeed.MaxLength), text);
                Assert.That(text.All(c => LevelSeed.Alphabet.IndexOf(c) >= 0), text);
                Assert.That(LevelSeed.TryParse(text, out ulong back), Is.True, text);
                Assert.That(back, Is.EqualTo(s), text);
                Assert.That(LevelSeed.TryParse(text.ToLowerInvariant(), out back) && back == s, "small letters read as capitals: " + text);
            }
        }

        [Test]
        public void SeedText_OfAFreshSeed_IsEightCharacters_WithoutLookAlikes()
        {
            Assert.That(LevelSeed.Alphabet, Does.Not.Contain("0").And.Not.Contain("O").And.Not.Contain("1").And.Not.Contain("I"));
            Assert.That(LevelSeed.Alphabet.Distinct().Count(), Is.EqualTo(32));
            for (ulong e = 0; e < 200; e++)
            {
                ulong seed = LevelSeed.Fresh(e * 7919UL);
                Assert.That(LevelSeed.ToText(seed).Length, Is.EqualTo(8));
            }
            Assert.That(LevelSeed.TryParse("0ABCDEFG", out _), Is.False, "0 is not in the alphabet");
            Assert.That(LevelSeed.TryParse("OABCDEFG", out _), Is.False, "O is not in the alphabet");
            Assert.That(LevelSeed.TryParse("", out _), Is.False);
            Assert.That(LevelSeed.TryParse("ZZZZZZZZZZZZZZ", out _), Is.False, "over 64 bits");
            Assert.That(LevelSeed.TryParse("ABCD-EFGH", out ulong dashed) && LevelSeed.TryParse("ABCDEFGH", out ulong plain) && dashed == plain);
        }

        [Test]
        public void Stages_AreIndependent_DrawsAddedToTheContentsLeaveTheLayout()
        {
            int contentsChanged = 0;
            for (ulong s = 0; s < 60; s++)
            {
                LevelLayout plain = Make(s), shifted = Make(s, warmUp: 5);
                Assert.That(shifted.DescribeLayout(), Is.EqualTo(plain.DescribeLayout()), "layout of seed " + s);
                Assert.That(LevelValidator.Validate(shifted), Is.Empty);
                if (shifted.Describe() != plain.Describe()) contentsChanged++;
            }
            Assert.That(contentsChanged, Is.GreaterThan(50), "the warm-up did change the contents, so the check above means something");

            // And the streams themselves: draws from one stage do not move another.
            LevelRandom a = LevelRandom.Stream(5UL, LevelGenerator.LayoutStage);
            ulong first = a.NextBits();
            LevelRandom c = LevelRandom.Stream(5UL, LevelGenerator.ContentsStage);
            for (int i = 0; i < 10; i++) c.NextBits();
            Assert.That(LevelRandom.Stream(5UL, LevelGenerator.LayoutStage).NextBits(), Is.EqualTo(first));
            Assert.That(LevelRandom.Stream(5UL, LevelGenerator.ContentsStage).NextBits(), Is.Not.EqualTo(first));
            Assert.That(LevelRandom.Stream(5UL, LevelGenerator.LayoutStage, 1).NextBits(), Is.Not.EqualTo(first), "a new attempt, a new stream");
        }

        // ---------------- The same seed, the same level ----------------

        [Test]
        public void SameSeed_GivesTheSameLevel_TwiceInARow_AndAfterOtherSeeds()
        {
            ulong[] seeds = { 0UL, 1UL, 12345UL, 0xDEADBEEFUL, ulong.MaxValue, LevelSeed.ShortMask };
            foreach (ulong s in seeds)
            {
                string first = Make(s).Describe();
                Assert.That(Make(s).Describe(), Is.EqualTo(first), "twice in a row, seed " + s);
                for (ulong other = 500; other < 540; other++) Make(other);
                Assert.That(Make(s).Describe(), Is.EqualTo(first), "after other seeds, seed " + s);
                Assert.That(LevelMap.Draw(Make(s)), Is.EqualTo(LevelMap.Draw(Make(s))));
            }
        }

        // ---------------- A thousand seeds ----------------

        /// <summary>The longest one generation may take, in milliseconds. Measured: see the output of the test.</summary>
        const double SlowestAllowedMs = 50;

        [Test]
        public void AThousandSeeds_AllGenerate_AllValid_Quickly_AndMostlyDifferent()
        {
            Make(424242UL); // the first call pays for compiling the code
            var layouts = new HashSet<string>();
            int retried = 0, mostAttempts = 0;
            double slowest = 0, total = 0;
            ulong slowestSeed = 0;
            double smallest = double.MaxValue, largest = 0;
            string smallestSize = "", largestSize = "";
            var timer = new Stopwatch();
            for (ulong s = 0; s < 1000; s++)
            {
                timer.Restart();
                LevelLayout level = Make(s);
                timer.Stop();
                double ms = timer.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > slowest) { slowest = ms; slowestSeed = s; }

                List<string> faults = LevelValidator.Validate(level);
                Assert.That(faults, Is.Empty, "seed " + s + "\n" + LevelMap.Draw(level));
                Assert.That(level.Seed, Is.EqualTo(s));
                Assert.That(level.Version, Is.EqualTo(LevelGenerator.Version));
                if (level.Attempt > 1) retried++;
                mostAttempts = Math.Max(mostAttempts, level.Attempt);
                layouts.Add(level.DescribeLayout());

                CellRect box = level.Bounds();
                double w = box.Width * level.CellSize, h = box.Height * level.CellSize;
                if (w * h < smallest) { smallest = w * h; smallestSize = w + " x " + h + " m (seed " + s + ")"; }
                if (w * h > largest) { largest = w * h; largestSize = w + " x " + h + " m (seed " + s + ")"; }
            }
            double distinct = layouts.Count / 1000.0;
            TestContext.WriteLine(string.Format(CultureInfo.InvariantCulture,
                "LEVELSTATS seeds 0-999: needed a second attempt {0}, most attempts {1}; slowest {2:0.00} ms (seed {3}), mean {4:0.000} ms; " +
                "distinct layouts {5} ({6:P1}); smallest {7}; largest {8}",
                retried, mostAttempts, slowest, slowestSeed, total / 1000, layouts.Count, distinct, smallestSize, largestSize));
            Assert.That(slowest, Is.LessThan(SlowestAllowedMs), "slowest generation, ms");
            Assert.That(distinct, Is.GreaterThanOrEqualTo(0.99), "share of distinct layouts");
        }

        [Test]
        public void ACrampedGrid_NeedsLaterAttempts_AndStillGivesTheSameValidLevel()
        {
            // On a grid this small many first attempts cannot be finished, so the later attempts are used.
            var cramped = new LevelValues { GridCells = 30 };
            int retried = 0, generated = 0;
            for (ulong s = 0; s < 200; s++)
            {
                LevelLayout level;
                try { level = LevelGenerator.Generate(s, cramped); }
                catch (InvalidOperationException) { continue; }
                generated++;
                Assert.That(LevelValidator.Validate(level, cramped), Is.Empty, "seed " + s);
                Assert.That(LevelGenerator.Generate(s, cramped).Describe(), Is.EqualTo(level.Describe()), "seed " + s);
                if (level.Attempt > 1) retried++;
            }
            TestContext.WriteLine("LEVELCRAMPED grid 30: generated " + generated + " of 200, of which " + retried + " on a later attempt");
            Assert.That(retried, Is.GreaterThan(0), "some seeds needed a later attempt");
        }

        // ---------------- Golden seeds ----------------

        // These maps are what the generator of version 1 makes. A change that changes them must raise
        // LevelGenerator.Version and paste the new maps here, on purpose.
        static readonly Dictionary<string, string> Golden = new Dictionary<string, string>
        {
            ["22222229"] = @"seed 22222229  version 1  11 rooms  92 x 88 m
                                    +-+-+-+
                                    |R8   |
            +-+-+-+   +-+-+-+       +     +
            |R4   |   |R6   |       |     |
            +     +   +     +       +     +
            |     |   |     |       |     |
            +     +   +     +     +-+-+ +-+-+-+
            |     |   |     |     |C7         |
            +-+ +-+-+-+-+ +-+-+-+-+           +
            |C3       |C5         |           |
    +-+-+-+-+         +           +           +
    |S0     |.            .       |         . |
    +       +         +           +           +
    |       |         |        .              |
    +   @   +         +           +           +
    |       |         |           |      .    |
    +       +         +           +-+-+-+ +-+-+
    |       |         |           |C9         |
+-+-+-+-+ +-+-+-+ +-+-+-+-+-+-+-+-+           +
|R2   |C1           |             |           |
+     +             +             +    .      +
|                   |             |           |
+     +           . +             +           +
|     |             |             |           |
+-+-+-+             +             +           +
      |             |             |   .       |
      +             +             +     .     +
      |             |             |           |
      +-+-+-+-+-+-+-+         +-+-+-+-+-+-+ +-+
                              |B10            |
                              +               +
                              |               |
                              +               +
                              |               |
                              +               +
                              |               |
                              +       .       +
                              |               |
                              +               +
                              |               |
                              +               +
                              |               |
                              +               +
                              |               |
                              +-+-+-+-+-+-+-+-+
",
            ["WALKTHRU"] = @"seed WALKTHRU  version 1  11 rooms  96 x 92 m
          +-+-+-+-+-+-+-+-+-+-+
          |S0     |C1         |
          +       +           +
          |       |           |
          +   @   +           +
          |                   |
          +       +           +
          |       |           |
          +-+-+-+-+           +
                  |           |
      +-+-+-+-+-+-+         . +
      |C4         |           |
      +           +-+-+-+-+ +-+-+
      |        .  |C2           |
+-+-+-+    .      +             +-+-+-+
|R5   |           |             |R3   |
+     +           +             +     +
|                 |                   |
+     +           +             +     +
|     |                         |     |
+-+-+-+           +             +-+-+-+-+-+-+-+-+
      |           |  .          |B10            |
      +           +-+-+-+-+-+-+-+               +
      |           |             |               |
      +-+-+-+-+ +-+             +               +
      |C6         |             |               |
      +           +             +               +
      |  .        |             |               |
      +           +             +       .       +
      |           |             |               |
      +           +-+-+-+-+-+-+-+               +
      |           |C8           |               |
      +           +        .   .+               +
      |                                         |
      +    .      +             +               +
      |           |             |               |
      +-+-+-+-+ +-+             +-+-+-+-+-+-+-+-+
            |R7   |             |
            +     +             +
            |     |          .  |
            +     +-+-+-+-+ +-+-+
            |     |     |R9   |
            +-+-+-+     +     +
                        |     |
                        +     +
                        |     |
                        +-+-+-+
",
            ["RUNNER42"] = @"seed RUNNER42  version 1  11 rooms  76 x 96 m
                    +-+-+-+-+
                    |S0     |
                    +       +
                    |       |
                    +   @   +
                    |       |
+-+-+-+-+-+-+-+-+   +       +
|B10            |   |       |
+               +-+-+-+ +-+-+
|               |C1       |
+               +         +-+-+-+-+-+-+
|               |         |R2   |R4   |
+               +         +     +     +
|               |     .         |     |
+       .       +         +     +     +
|               |         |     |     |
+               +         +-+-+-+-+ +-+
|               |         |C3         |
+               +         +           +
|               |                     |
+               +         +           +
|               |         |           |
+-+-+-+-+-+-+ +-+-+-+-+-+-+           +
          |C8       . |   |           |
          +   .       +   +           +
          |           |   |    .      |
    +-+-+-+          .+   +           +
    |R9   |           |   |           |
    +     +           + +-+-+-+ +-+-+-+
    |                 | |C5       |
    +     +           + +         +
    |     |           | |         |
    +-+-+-+-+ +-+-+-+-+-+         +
          |C7     .     |         |
          +   .         +         +
          |             |         |
          +             +         +
          |                    .  |
          +             +         +
          |             |    .    |
          +             +         +
          |             |         |
          +             +-+-+-+ +-+
          |             |   |R6   |
          +-+-+-+-+-+-+-+   +     +
                            |     |
                            +     +
                            |     |
                            +-+-+-+
",
        };

        [TestCase("22222229")]
        [TestCase("WALKTHRU")]
        [TestCase("RUNNER42")]
        public void GoldenSeeds_KeepTheirMaps(string seedText)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(1), "the version was raised: update the golden maps and this number together");
            string map = LevelMap.Draw(Make(seed));
            TestContext.WriteLine("GOLDEN " + seedText + "\n" + map);
            Assert.That(map, Is.EqualTo(Golden[seedText].Replace("\r\n", "\n")), "map of seed " + seedText + "\n" + map);
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

        [Test]
        public void Validator_FindsNothingWrong_WithAGeneratedLevel()
        {
            LevelLayout level = Make(Sample);
            Assert.That(LevelValidator.Validate(level), Is.Empty);
        }

        [Test]
        public void Validator_ReportsARoomOutsideTheGrid() =>
            AssertFault(l => { var r = First(l, RoomKind.Boss); r.Rect.X = l.GridCells - 2; }, "outside the grid");

        [Test]
        public void Validator_ReportsARoomOfTheWrongSize() =>
            AssertFault(l => First(l, RoomKind.Combat).Rect.Width = 9, "not 5 to 7 on a side");

        [Test]
        public void Validator_ReportsRoomsThatOverlap() =>
            AssertFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Reward);
                CellRect combat = l.Rooms[l.DoorsOf(reward.Order).First().Other(reward.Order)].Rect;
                reward.Rect = new CellRect(combat.X + 1, combat.Y + 1, 3, 3);
            }, "overlaps");

        [Test]
        public void Validator_ReportsADoorOffTheSharedWall() =>
            AssertFault(l => l.Doors[0].Line += 1, "do not share that wall");

        [Test]
        public void Validator_ReportsADoorWithoutWallOnBothSides() =>
            AssertFault(l =>
            {
                LevelDoor d = l.Doors[0];
                LevelGeometry.SharedWall(l.Rooms[d.RoomA].Rect, l.Rooms[d.RoomB].Rect, out _, out _, out int lo, out _);
                d.From = lo;
            }, "no wall on both sides");

        [Test]
        public void Validator_ReportsADoorOfTheWrongWidth() =>
            AssertFault(l => l.Doors[0].Width = 2, "cells wide");

        [Test]
        public void Validator_ReportsARoomThatCannotBeReached() =>
            AssertFault(l => { var r = First(l, RoomKind.Reward); l.Doors.RemoveAll(d => d.Joins(r.Order)); }, "cannot be reached from the start room");

        [Test]
        public void Validator_ReportsTwoDoorsBetweenTheSameRooms_AndABossRoomWithTwoDoors() =>
            AssertFault(l =>
            {
                LevelDoor d = l.DoorsOf(First(l, RoomKind.Boss).Order).First();
                l.Doors.Add(new LevelDoor { RoomA = d.RoomA, RoomB = d.RoomB, Vertical = d.Vertical, Line = d.Line, From = d.From, Width = d.Width });
            }, "the boss room has 2 doors");

        [Test]
        public void Validator_ReportsTheSameRoomsJoinedTwice() =>
            AssertFault(l => l.Doors.Add(l.Doors[0]), "another door already joins");

        [Test]
        public void Validator_ReportsAMainPathOutOfOrder() =>
            AssertFault(l =>
            {
                LevelRoom boss = First(l, RoomKind.Boss), combat = First(l, RoomKind.Combat);
                boss.Kind = RoomKind.Combat;
                combat.Kind = RoomKind.Boss;
            }, "the main path runs");

        [Test]
        public void Validator_ReportsAMissingDoorOnTheMainPath() =>
            AssertFault(l => l.Doors.RemoveAll(d => d.Joins(First(l, RoomKind.Boss).Order)), "no door from");

        [Test]
        public void Validator_ReportsABossRoomTouchingTheStartRoom() =>
            AssertFault(l =>
            {
                LevelRoom start = First(l, RoomKind.Start);
                First(l, RoomKind.Boss).Rect = new CellRect(start.Rect.Right, start.Rect.Top, 8, 8);
            }, "the boss room touches the start room");

        [Test]
        public void Validator_ReportsARewardRoomWithTwoDoors() =>
            AssertFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Reward);
                LevelDoor d = l.DoorsOf(reward.Order).First();
                l.Doors.Add(new LevelDoor { RoomA = reward.Order, RoomB = First(l, RoomKind.Start).Order, Vertical = d.Vertical, Line = d.Line, From = d.From, Width = d.Width });
            }, "has 2 doors, not 1");

        [Test]
        public void Validator_ReportsARewardRoomNotOffACombatRoom() =>
            AssertFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Reward);
                LevelDoor d = l.DoorsOf(reward.Order).First();
                d.RoomA = First(l, RoomKind.Start).Order;
                d.RoomB = reward.Order;
            }, "does not lead to a combat room");

        [Test]
        public void Validator_ReportsACombatRoomWithTwoRewardRooms() =>
            AssertFault(l =>
            {
                List<LevelRoom> rewards = l.Rooms.Where(r => r.Kind == RoomKind.Reward).ToList();
                int combat = l.DoorsOf(rewards[0].Order).First().Other(rewards[0].Order);
                LevelDoor d = l.DoorsOf(rewards[1].Order).First();
                d.RoomA = Math.Min(combat, rewards[1].Order);
                d.RoomB = Math.Max(combat, rewards[1].Order);
            }, "has two reward rooms");

        [Test]
        public void Validator_ReportsADoorBetweenRoomsThatAreNotNeighbours() =>
            AssertFault(l =>
            {
                List<LevelRoom> path = LevelGeometry.MainPath(l);
                l.Doors.Add(new LevelDoor { RoomA = path[0].Order, RoomB = path[2].Order, Width = 1 });
            }, "not neighbours on the main path");

        [Test]
        public void Validator_ReportsTheWrongNumberOfRoomsOfAKind() =>
            AssertFault(l => First(l, RoomKind.Reward).Kind = RoomKind.Combat, "Combat rooms, the plan has 5");

        [Test]
        public void Validator_ReportsRoomsListedOutOfOrder() =>
            AssertFault(l => { LevelRoom r = l.Rooms[1]; l.Rooms[1] = l.Rooms[2]; l.Rooms[2] = r; }, "in the list has order");

        [Test]
        public void Validator_ReportsAMissingPlayerStart() =>
            AssertFault(l => First(l, RoomKind.Start).PlayerStart = null, "has no player start");

        [Test]
        public void Validator_ReportsTheWrongNumberOfEnemyPoints() =>
            AssertFault(l => First(l, RoomKind.Boss).EnemyPoints.Clear(), "has 0 enemy points, not 1");

        [Test]
        public void Validator_ReportsAPointOutsideItsRoom() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Combat);
                r.EnemyPoints[0] = new LevelPoint(r.Rect.Right * l.CellSize + 3, r.Rect.Y * l.CellSize + 3);
            }, "is outside the room");

        [Test]
        public void Validator_ReportsAPointTooNearAWall() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Boss);
                r.EnemyPoints[0] = new LevelPoint(r.Rect.X * l.CellSize + 0.5, r.Rect.Y * l.CellSize + 10);
            }, "nearer a wall");

        [Test]
        public void Validator_ReportsAPointTooNearADoor() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Start);
                LevelDoor d = l.DoorsOf(r.Order).First();
                d.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                r.PlayerStart = new LevelPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
            }, "nearer a door");

        [Test]
        public void Validator_ReportsAnEnemyTooNearTheDoorSheEntersBy() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Combat);
                LevelDoor d = LevelGeometry.EntryDoor(l, r);
                d.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                LevelPoint mid = new LevelPoint((a.X + b.X) / 2, (a.Y + b.Y) / 2);
                LevelPoint centre = r.Middle(l.CellSize);
                // 3 m from the opening's middle, into the room.
                double dx = d.Vertical ? Math.Sign(centre.X - mid.X) * 3 : 0, dy = d.Vertical ? 0 : Math.Sign(centre.Y - mid.Y) * 3;
                r.EnemyPoints[0] = new LevelPoint(mid.X + dx, mid.Y + dy);
            }, "nearer the door she enters by");

        [Test]
        public void Validator_ReportsEnemiesTooNearEachOther() =>
            AssertFault(l =>
            {
                LevelRoom r = l.Rooms.First(x => x.Kind == RoomKind.Combat && x.EnemyPoints.Count >= 2);
                LevelPoint p = r.EnemyPoints[0];
                r.EnemyPoints[1] = new LevelPoint(p.X, p.Y);
            }, "nearer each other");

        // ---------------- The plan ----------------

        [Test]
        public void Plan_EnemiesClimbFromOneToThree()
        {
            var plan = new LevelPlan();
            Assert.That(Enumerable.Range(0, plan.CombatRooms).Select(plan.EnemiesInCombatRoom), Is.EqualTo(new[] { 1, 1, 2, 2, 3 }));
        }

        [Test]
        public void Map_ShowsEveryRoom_TheStart_AndEveryEnemy()
        {
            LevelLayout level = Make(Sample);
            string map = LevelMap.Draw(level);
            foreach (LevelRoom r in level.Rooms) Assert.That(map, Does.Contain(r.Letter + r.Order.ToString(CultureInfo.InvariantCulture)), map);
            Assert.That(map.Count(c => c == '@'), Is.EqualTo(1), map);
            Assert.That(map.Split('\n').Skip(1).Sum(line => line.Count(c => c == '.')), Is.EqualTo(level.Rooms.Sum(r => r.EnemyPoints.Count)), map);
        }
    }
}
