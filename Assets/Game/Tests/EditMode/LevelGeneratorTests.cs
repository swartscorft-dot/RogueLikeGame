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
            // Since work order 012 the contents can send the generator to another attempt (no place for a room's
            // enemies), which brings another layout: the layouts are compared when both came of the same attempt, and
            // that must be nearly always.
            int contentsChanged = 0, sameAttempt = 0;
            for (ulong s = 0; s < 60; s++)
            {
                LevelLayout plain = Make(s), shifted = Make(s, warmUp: 5);
                Assert.That(LevelValidator.Validate(shifted), Is.Empty);
                if (shifted.Attempt != plain.Attempt) continue;
                sameAttempt++;
                Assert.That(shifted.DescribeLayout(), Is.EqualTo(plain.DescribeLayout()), "layout of seed " + s);
                if (shifted.Describe() != plain.Describe()) contentsChanged++;
            }
            Assert.That(sameAttempt, Is.GreaterThanOrEqualTo(54), "the same attempt for nearly every seed");
            Assert.That(contentsChanged, Is.GreaterThan(sameAttempt - 6), "the warm-up did change the contents, so the check above means something");

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

        /// <summary>
        /// The longest one generation may take, in milliseconds. Measured: see the output of the test (work order 012:
        /// about 19 ms at the slowest of a thousand seeds per floor number, 6 ms on the mean).
        /// </summary>
        // 50 until 2026-11-20: a floor takes about 40 ms at its slowest on a quiet machine, and with four copies of the
        // project under test at once it measured 50 to 63 and failed for load alone, five times in a day. The bound is
        // there to catch generation going wrong (seconds), not to time the machine.
        const double SlowestAllowedMs = 150;

        // Work order 012: a thousand seeds for each floor number of the act.
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_AllGenerate_AllValid_Quickly_AndMostlyDifferent(int floor)
        {
            LevelGenerator.Generate(424242UL, floor); // the first call pays for compiling the code
            var layouts = new HashSet<string>();
            int retried = 0, mostAttempts = 0;
            double slowest = 0, total = 0;
            ulong slowestSeed = 0;
            double smallest = double.MaxValue, largest = 0;
            string smallestSize = "", largestSize = "";
            var values = new LevelValues();
            int fewestCombat = int.MaxValue, mostCombat = 0, fewestExit = int.MaxValue, mostExit = 0;
            long combatHealth = 0, exitHealth = 0, combatRooms = 0, exitRooms = 0;
            var timer = new Stopwatch();
            for (ulong s = 0; s < 1000; s++)
            {
                timer.Restart();
                LevelLayout level = LevelGenerator.Generate(s, floor);
                timer.Stop();
                Assert.That(level.Floor, Is.EqualTo(floor));
                foreach (LevelRoom r in level.Rooms)
                {
                    int n = r.EnemyGroups.Where(g => !g.Boss && !g.Elite).Sum(g => g.Count); // the elite apart (work order 017)
                    long hp = r.EnemyGroups.Sum(g => (long)values.HealthOf(g.Kind, g.Boss) * g.Count);
                    if (r.Kind == RoomKind.Combat)
                    {
                        fewestCombat = Math.Min(fewestCombat, n); mostCombat = Math.Max(mostCombat, n);
                        combatHealth += hp; combatRooms++;
                    }
                    else if (r.Kind == RoomKind.Exit)
                    {
                        fewestExit = Math.Min(fewestExit, n); mostExit = Math.Max(mostExit, n);
                        exitHealth += hp; exitRooms++;
                        Assert.That(r.EnemyGroups.Count(g => g.Boss), Is.EqualTo(floor == 5 ? 1 : 0), "the act boss on floor 5 only, seed " + s);
                    }
                    else Assert.That(r.EnemyPoints, Is.Empty, "no enemies in the entrance or a treasure room");
                }
                double ms = timer.Elapsed.TotalMilliseconds;
                total += ms;
                if (ms > slowest) { slowest = ms; slowestSeed = s; }

                List<string> faults = LevelValidator.Validate(level);
                Assert.That(faults, Is.Empty, "seed " + s + " floor " + floor + "\n" + LevelMap.Draw(level));
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
                "LEVELSTATS floor {9} seeds 0-999: needed a second attempt {0}, most attempts {1}; slowest {2:0.00} ms (seed {3}), mean {4:0.000} ms; " +
                "distinct layouts {5} ({6:P1}); smallest {7}; largest {8}; combat rooms {10} to {11} enemies, mean health {12:0}; " +
                "exit rooms {13} to {14} enemies (act boss apart), mean health {15:0}",
                retried, mostAttempts, slowest, slowestSeed, total / 1000, layouts.Count, distinct, smallestSize, largestSize, floor,
                fewestCombat, mostCombat, combatHealth / (double)combatRooms, fewestExit, mostExit, exitHealth / (double)exitRooms));
            values.EnemyRange(RoomKind.Combat, floor, out int f, out int m);
            Assert.That(fewestCombat, Is.GreaterThanOrEqualTo(f));
            Assert.That(mostCombat, Is.LessThanOrEqualTo(m));
            Assert.That(slowest, Is.LessThan(SlowestAllowedMs), "slowest generation, ms");
            Assert.That(distinct, Is.GreaterThanOrEqualTo(0.99), "share of distinct layouts");
        }

        [Test]
        public void ACrampedGrid_NeedsLaterAttempts_AndStillGivesTheSameValidLevel()
        {
            // On a grid this small many first attempts cannot be finished, so the later attempts are used.
            // Work order 041: with the line off. A floor in a line fits a grid this small at its first attempt every time
            // (its rooms are taken back and moved within the attempt), and one too small for it fails every attempt.
            var cramped = new LevelValues { GridCells = 26, InALine = false }; // 30 until work order 012 made a floor smaller
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
            TestContext.WriteLine("LEVELCRAMPED grid " + cramped.GridCells + ": generated " + generated + " of 200, of which " + retried + " on a later attempt");
            Assert.That(retried, Is.GreaterThan(0), "some seeds needed a later attempt");
        }

        // ---------------- Golden seeds ----------------

        // These maps are what the generator of version 11 makes with its defaults (work order 041, 2026-11-20: the floor
        // in one row, LevelValues.InALine on, the treasure rooms between the combat rooms; the themes on and the casters
        // off; work order 044: the rooms' mood on, so each ossuary's skull and bone piles are banked at its far wall, the
        // only change from version 10's maps). Each room holds the groups its namesake holds in the version 9 maps below. A change that changes them must
        // raise LevelGenerator.Version and paste the new maps here, on purpose.
        static readonly Dictionary<string, string> Golden = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 11  8 rooms  48 enemies  104 x 96 m  themes C1 archive, C2 shrine, T3 archive, L4 ossuary, T5 archive, C6 archive, X7 throne room
                                    +-+-+-+-+-+-+-+-+
                                    |X7     oYo     |
                                    + #           # +
                                    |               |
                                    +               +
                                    |               |
                                    +   B    H      +
                                    |               |
                                    +               +
                                    |               |
                                    +        p      +
                                    |               |
                                    +      p        +
                                    |               |
                                    + #           # +
                                    |               |
                                    +-+ +-+-+-+-+-+-+
                                    |C6     ukik|
                                    +          i+
                                    |        p  |
                  +-+-+-+-+-+-+     +           +
                  |L4  ^~^    |     |k          |
                  +u h  e     +-+-+-+           +
                  |           |T5  u|           |
                  +      p    +     +       :   +
                  |    p          $             |
                  +q     : B  +     +           +
                  |           |     |           |
                  +           +-+-+-+          h+
                  |           |     |     B   u |
                  +-+-+-+ +-+-+     +-+-+-+-+-+-+
                      |T3   |
                      +  $  +
                      |u    |
                      +k    +
                      |i   k|
+-+-+-+-+-+-+-+-+-+-+-+-+ +-+-+-+-+
|E0    *|C1         |C2           |
+       +    %      +    p  #h    +
|              p                p |
+   @   +           +   #        i+
|       |          B|          o A|
+      *+i         u+        #   i+
|       |k          |    b        |
+-+-+-+-+         : +     #       +
        |           |             |
        +    h      +-+-+-+-+-+-+-+
        |          k|
        +-+-+-+-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 11  8 rooms  80 enemies  104 x 96 m  themes C1 archive, C2 shrine, T3 archive, L4 ossuary, T5 archive, C6 archive, X7 throne room
                                    +-+-+-+-+-+-+-+-+
                                    |X7     oYo   # |
                                    + #             +
                                    |  H          p |
                                    +               +
                                    |               |
                                    +   B    H      +
                                    |               |
                                    +       G       +
                                    |               |
                                    +        B      +
                                    |               |
                                    +      p        +
                                    |               |
                                    + #           p +
                                    |              #|
                                    +-+ +-+-+-+-+-+-+
                                    |C6     ukik|
                                    +          i+
                                    |     p  B  |
                  +-+-+-+-+-+-+     +           +
                  |L4  ^~^    |     |k          |
                  +u H  e     +-+-+-+           +
                  |         p |T5ik |        p  |
                  +      B    +     +           +
                  |    b          $             |
                  +q p   : g  +     +     b :   +
                  |           |  u  |           |
                  +           +-+-+-+        % H+
                  |           |     |    ug     |
                  +-+-+-+ +-+-+     +-+-+-+-+-+-+
                      |T3   |
                      +  $ k+
                      |u    |
                      +k    +
                      |i    |
+-+-+-+-+-+-+-+-+-+-+-+-+ +-+-+-+-+
|E0    *|C1         |C2           |
+       +           +    b  #H    +
|           b  B                p |
+   @   +           +   #        i+
|       |          g|          o A|
+      *+i         u+        #   i+
|       |k  p       |    B        |
+-+-+-+-+         p +     #  p    +
        |       :   |             |
        +    H      +-+-+-+-+-+-+-+
        | %        k|
        +-+-+-+-+-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 11  8 rooms  49 enemies  100 x 84 m  themes C1 archive, C2 tomb hall, T3 archive, L4 ossuary, T5 archive, C6 shrine, X7 throne room
+-+-+-+-+-+-+-+-+
|X7     oYoh    |
+ #           # +
|               |
+        p      +
|               |
+          b    +
|               |
+               +                         +-+-+-+-+
|               |                         |E0     |
+               +-+-+-+-+-+-+-+           +       +
|               |C6   *A*     |           |       |
+   B p         +             +           +   @   +
|                   #  o      |           |       |
+ #           # +   H      #  +           +       +
|               |      p      |           |*    * |
+-+-+-+-+-+-+-+-+             +       +-+-+-+ +-+-+
                |   p         |       |C1       |
                +        b    +       +         +
                |             |       |        :|
                + #    #    # +       + p       +
                |             |       |         |
                +             +       +       % +
                |             |       |         |
                +-+-+-+ +-+-+-+       +  b      +
                    |T5   |           |       p |
                    +i $ k+           +u        +
                    |k    |           |k    h   |
                    +u    +           +         +
                    |     |           |         |
                +-+-+-+ +-+-+     +-+-+-+ +-+-+-+
                |L4 q       |     |C2        &|
                +     p   e +     +           +
                |h          |     |  #       !|
                +   p       +-+-+-+     =  =  +
                |~          |T3 uk|           |
                +~       :  +     +  p        +
                |^            $               |
                +           +     +   [b =    +
                |           |     | h      p !|
                +           +-+-+-+#          +
                |u    b     |     |    !      |
                +-+-+-+-+-+-+     +-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 11  8 rooms  80 enemies  100 x 84 m  themes C1 archive, C2 tomb hall, T3 archive, L4 ossuary, T5 archive, C6 shrine, X7 throne room
+-+-+-+-+-+-+-+-+
|X7    oYo H    |
+ #           # +
|               |
+        B      +
|     p         |
+          g    +
|               |
+       G       +                         +-+-+-+-+
|    p          |                         |E0     |
+          p    +-+-+-+-+-+-+-+           +       +
|               |C6   *A*     |           |       |
+   H B         +             +           +   @   +
|                   #  o      |           |       |
+ #           # +   H      #  +           +       +
|               |      b      |           |*    * |
+-+-+-+-+-+-+-+-+          p  +       +-+-+-+ +-+-+
                |   p         |       |C1       |
                +        B    +       +         +
                |             |       |        :|
                + #    #    # +       + p       +
                |             |       |         |
                +             +       +    p  % +
                |             |       |        k|
                +-+-+-+ +-+-+-+       +  B      +
                    |T5   |           |         |
                    +i $ k+           +u       b+
                    |k    |           |     H   |
                    +u    +           +         +
                    |     |           |         |
                +-+-+-+ +-+-+     +-+-+-+ +-+-+-+
                |L4 q      g|     |C2        &|
                +     B     +     +       #   +
                |e          |     |          !|
                +   b  :    +-+-+-+  [ = B    +
                |~          |T3 uk|         p |
                +~    p     +     +  b        +
                |^            $               |
                +   p       +     +    g =   =+
                |           |     | H      p  |
                +   H       +-+-+-+#          +
                |u          |     |    ! !    |
                +-+-+-+-+-+-+     +-+-+-+-+-+-+
",
        };

        // These maps are what the generator of version 9 made with its defaults (work order 034, 2026-11-20: main's
        // version 7 and work order 028's room themes merged, the themes on and the casters off). The rooms, doors,
        // enemies and elites are those of main's version 7 maps below, field for field; the props and the first line's
        // legend of themes are work order 028's. Work order 041: version 10 with LevelValues.InALine off makes them still,
        // but for the version in the first line.
        static readonly Dictionary<string, string> GoldenVersion9 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 9  8 rooms  48 enemies  88 x 96 m  themes C1 archive, T2 vault, C3 shrine, L4 ossuary, C5 archive, T6 vault, X7 throne room
            +-+-+-+
            |T2  k|
            + i$i +
            |     |
            +     +
            |     |
+-+-+-+-+-+-+-+ +-+-+-+-+-+-+-+-+-+
|E0     |C1       k |C3           |
+      *+    %      +     #  h    +
|       |           |             |
+   @   +      B    +         #  i+
|                           p  o A|
+       + :         +   #        i+
|      *|           |      p      |
+-+-+-+-+      h    +       #     +
        |u        p |  b          |
        +          k+-+-+ +-+-+-+-+-+
        |           | |L4       :   |
        +-+-+-+-+-+-+ +             +
                      |             |
                      +             +
                      |    p      e |
                      +  ^ ^       ^+
                      |         ~h  |
                      +             +
                      | p   B    ~ q|
                      +-+-+-+-+ +-+-+-+
                            |C5    k  |
                            +k        +-+-+-+
                            |u        |T6   |
                            +         +   i +
                            |B     p      $ |
            +-+-+-+-+-+-+-+-+         +   i +
            |X7    oYo      |         |     |
            + # H         # +  :    % +-+-+-+
            |                         |
            +               +h        +
            |               |         |
            +               +-+-+-+-+-+
            |               |
            +               +
            |     B         |
            +        p      +
            |               |
            +               +
            |      p        |
            + #           # +
            |               |
            +-+-+-+-+-+-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 9  8 rooms  80 enemies  88 x 96 m  themes C1 archive, T2 vault, C3 shrine, L4 ossuary, C5 archive, T6 vault, X7 throne room
            +-+-+-+
            |T2   |
            + i$i +
            |     |
            +     +
            |     |
+-+-+-+-+-+-+-+ +-+-+-+-+-+-+-+-+-+
|E0     |C1 k     k |C3           |
+      *+           +     #  H   i+
|       |           |          o A|
+   @   +    p g    +            i+
|                           p # p |
+       + :         +   #         +
|      *|           |      B      |
+-+-+-+-+      H    +       #     +
        |u  p     B |  b          |
        +           +-+-+ +-+-+-+-+-+
        | b         | |L4        ~  |
        +-+-+-+-+-+-+ +             +
                      |       :     |
                      +             +
                      |b   p    p e |
                      +      ^    ~ +
                      |   ^    ~ H  |
                      +             +
                      | B   g     ^ |
                      +-+-+-+-+ +-+-+-+
                            |C5    k  |
                            +k        +-+-+-+
                            |u        |T6   |
                            +         +   i +
                            |g     B      $ |
            +-+-+-+-+-+-+-+-+  p      +   i +
            |X7    oYo      |     b   |     |
            + # H         # +   p   % +-+-+-+
            |                         |
            +               + H       +
            |  p            |      :  |
            +               +-+-+-+-+-+
            |               |
            +       G       +
            |     B    p    |
            +        B      +
            |               |
            +               +
            |      H        |
            + #       p   # +
            |               |
            +-+-+-+-+-+-+-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 9  8 rooms  49 enemies  92 x 80 m  themes C1 archive, T2 vault, C3 tomb hall, T4 archive, L5 ossuary, C6 shrine, X7 throne room
                                  +-+-+-+-+
                                  |E0     |
                                  +       +
                                  |       |
                                  +   @   +
                                  |       |
                                  +       +
                                  |*    * |
        +-+-+-+-+-+-+-+-+     +-+-+-+ +-+-+
        |X7    oYo      |     |C1       |
        + #           # +     + %       +
        |         p     |     |        :|
        +               +     +         +
        |       h       |     |         |
        +               +     +      p  +
        |   b           |     |        k|
        +               +     +    p    +-+-+-+
        |       p       |     | b       |T2   |
        +               +     +u      h +   i +
        |            B  |     |             $ |
        +               +     +         +   i +
        |               |     |         |    u|
        + #           # + +-+-+-+ +-+-+-+-+-+-+
        |               | |C3         |
+-+-+-+-+-+ +-+-+-+-+-+-+ +b          +
|C6           |           |   [   [  !|
+             +-+-+-+-+-+-+           +
|             |L5   ~   : |      p  p!|
+   #    p #  +      e    +   [   [   +
|             |    p  ^               |
+*            +  ~        +          &+
|A o                p     |      #    |
+*            + ~h        +       h # +
|             |           |      !    |
+  # p #    # +        b  +-+ +-+-+-+-+
| H           |          u|T4  k|
+             +-+-+-+-+-+-+k    +
|            b|           |     |
+-+-+-+-+-+-+-+           +  $  +
                          | u   |
                          +-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 9  8 rooms  80 enemies  92 x 80 m  themes C1 archive, T2 vault, C3 tomb hall, T4 archive, L5 ossuary, C6 shrine, X7 throne room
                                  +-+-+-+-+
                                  |E0     |
                                  +       +
                                  |       |
                                  +   @   +
                                  |       |
                                  +       +
                                  |*    * |
        +-+-+-+-+-+-+-+-+     +-+-+-+ +-+-+
        |X7    oYo      |     |C1       |
        + #           # +     + %       +
        |    p    B     |     |        :|
        +               +     +         +
        |       H    p  |     |         |
        +     p         +     +      b  +
        |   g           |     |        k|
        +       G       +     +    p    +-+-+-+
        |               |     | B       |T2   |
        +        B    H +     +u      H +   i +
        |               |     |             $ |
        +               +     +    p    +   i +
        |               |     |         |    u|
        + #           # + +-+-+-+ +-+-+-+-+-+-+
        |               | |C3       # |
+-+-+-+-+-+ +-+-+-+-+-+-+ +g          +
|C6           |           |   [   [  !|
+             +-+-+-+-+-+-+ p         +
|             |Lb   ~   : |      b  B!|
+   #  p p #  +      g    +   [       +
|             |    p  ^           p   |
+*            +  ~        +          &+
|A o                p     |   [ #     |
+*            +H e        +       H   +
|  #          |  ~        |      !    |
+    b #    # +        B  +-+ +-+-+-+-+
| H           |q         u|T4   |
+             +-+-+-+-+-+-+k   u+
|            B|           |     |
+-+-+-+-+-+-+-+           +k $  +
                          |     |
                          +-+-+-+
",
        };

        // Work order 034: the casters on (LevelValues.Casters), the themes on, so that where the casters stand stays
        // pinned while they are switched off in the game: floor 5 of each golden seed (floor 1 holds none either way). The
        // rooms are those of the maps above with casters c in place of a shooter or a brute after the caps, and the
        // props move round them.
        static readonly Dictionary<string, string> GoldenCasters = new Dictionary<string, string>
        {
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 9  8 rooms  80 enemies  88 x 96 m  themes C1 archive, T2 vault, C3 shrine, L4 ossuary, C5 archive, T6 vault, X7 throne room
            +-+-+-+
            |T2   |
            + i$i +
            |     |
            +     +
            |     |
+-+-+-+-+-+-+-+ +-+-+-+-+-+-+-+-+-+
|E0     |C1 k     k |C3           |
+      *+           +     #  H    +
|       |           |            i|
+   @   +    p g    +    p     o A+
|                           c#  c |
+       + :         +   #         +
|      *|           |      B      |
+-+-+-+-+      H    +       #     +
        |u  p     B |    p        |
        +           +-+-+ +-+-+-+-+-+
        | b      c  | |L4        ~  |
        +-+-+-+-+-+-+ +             +
                      |       :     |
                      +             +
                      |g ^ b    p e |
                      +     ^     ~ +
                      |   p     ~H  |
                      +             +
                      | B   c     ^ |
                      +-+-+-+-+ +-+-+-+
                            |C5    k  |
                            +k        +-+-+-+
                            |u        |T6   |
                            +         +   i +
                            |g     B      $ |
            +-+-+-+-+-+-+-+-+  p      +   i +
            |X7    oYo      |     b   |     |
            + # H         # +   c   % +-+-+-+
            |          p              |
            +               + H  :  p +
            |  c            |         |
            +               +-+-+-+-+-+
            |               |
            +       G       +
            |     B    p    |
            +        b      +
            |               |
            +               +
            |      c        |
            + #       p   # +
            | c             |
            +-+-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 9  8 rooms  80 enemies  92 x 80 m  themes C1 archive, T2 vault, C3 tomb hall, T4 archive, L5 ossuary, C6 shrine, X7 throne room
                                  +-+-+-+-+
                                  |E0     |
                                  +       +
                                  |       |
                                  +   @   +
                                  |       |
                                  +       +
                                  |*    * |
        +-+-+-+-+-+-+-+-+     +-+-+-+ +-+-+
        |X7    oYo      |     |C1       |
        + #           # +     + %       +
        |    p    c     |     |        :|
        +               +     +  p      +
        |       H    p  |     |         |
        +     b         +     +      B  +
        |   g           |     |        k|
        +       G       +     +    p    +-+-+-+
        |               |     | c       |T2   |
        +        B    c +     +u      H +   i +
        |               |     |             $ |
        +               +     +         +   i +
        |        p      |     |     c   |    u|
        + #           # + +-+-+-+ +-+-+-+-+-+-+
        |               | |C3         |
+-+-+-+-+-+ +-+-+-+-+-+-+ +g          +
|C6           |           |   [   [  !|
+             +-+-+-+-+-+-+ p         +
|     c       |Lc   ~   : |      b  B!|
+   #    p #  +      g    +   [       +
|             |    b  ^           p   |
+*            +  p        +        # &+
|A o                p     |   [  #    |
+*            +H e ~      +       H   +
|             |  ~        |    c !    |
+  # c #    # +        B  +-+ +-+-+-+-+
| H           |q         u|T4   |
+             +-+-+-+-+-+-+k   u+
|    p       B|           |     |
+-+-+-+-+-+-+-+           +k $  +
                          |     |
                          +-+-+-+
",
        };

        // These maps are what main's generator of version 7 made (work order 012: the chain of rooms, two treasure rooms,
        // the floor's number and the enemies in groups; the props as in version 3; work order 014, 2026-11-20: fuller rooms,
        // and the act boss its own kind, still G; work order 017: the elite room L with its elite e; work order 023: at
        // most three brutes and three shooters a room, four in an exit room, the rest swarmers; version 7, work order 030:
        // the two together, the elite counted apart from the caps), copied unchanged from main (work order 034). Version 9
        // with the themes off and the casters off (the default) makes them still, but for the version in the first line.
        static readonly Dictionary<string, string> GoldenThemesOff = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 7  8 rooms  48 enemies  88 x 96 m
            +-+-+-+
            |T2   |
            + *$* +
            |     |
            +     +
            |     |
+-+-+-+-+-+-+-+ +-+-+-+-+-+-+-+-+-+
|E0     |C1        *|C3           |
+      *+           +        h   ~+
|       |   %       |      %      |
+   @   +      B    +   #         +
|                           p     |
+       +           +             +
|      *|           | =    p      |
+-+-+-+-+      h    +             +
        |         p |* b          |
        +  &  %     +-+-+ +-+-+-+-+-+
        |!          | |L4           |
        +-+-+-+-+-+-+ +     :       +
                      |             |
                      +          &  +
                      |    p      e |
                      +             +
                      |      #   h u|
                      +             +
                      | p   B      *|
                      +-+-+-+-+ +-+-+-+
                            |C5       |
                            +         +-+-+-+
                            |         |T6   |
                            +         +   * +
                            |B     p      $ |
            +-+-+-+-+-+-+-+-+         +   * +
            |X7             |   #     |     |
            + # H         # +        u+-+-+-+
            |                         |
            +               +h     #  +
            |               |        *|
            +               +-+-+-+-+-+
            |&              |
            +A              +
            |&    B         |
            +        p      +
            |               |
            +               +
            |      p        |
            + #           # +
            |               |
            +-+-+-+-+-+-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 7  8 rooms  80 enemies  88 x 96 m
            +-+-+-+
            |T2   |
            + *$* +
            |     |
            +     +
            |     |
+-+-+-+-+-+-+-+ +-+-+-+-+-+-+-+-+-+
|E0     |C1        *|C3           |
+      *+   %       +        H   ~+
|       |           |      %      |
+   @   +    p g    +   #         +
|                           p   p |
+       +           +             +
|      *|           | =    B      |
+-+-+-+-+      H    +             +
        |   p     B |* b          |
        +  &  %     +-+-+ +-+-+-+-+-+
        |!b         | |L4           |
        +-+-+-+-+-+-+ +     :       +
                      |             |
                      +             +
                      |b & p    p e |
                      +             +
                      |      #   H u|
                      +             +
                      | B   g      *|
                      +-+-+-+-+ +-+-+-+
                            |C5       |
                            +      #  +-+-+-+
                            |         |T6   |
                            +         +   * +
                            |g     B      $ |
            +-+-+-+-+-+-+-+-+  p      +   * +
            |X7             |     b   |     |
            + # H         # +   p    u+-+-+-+
            |                         |
            +               + H    #  +
            |  p            |        *|
            +               +-+-+-+-+-+
            |&              |
            +A      G       +
            |&    B    p    |
            +        B      +
            |               |
            +               +
            |      H        |
            + #       p   # +
            |               |
            +-+-+-+-+-+-+-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 7  8 rooms  49 enemies  92 x 80 m
                                  +-+-+-+-+
                                  |E0     |
                                  +       +
                                  |       |
                                  +   @   +
                                  |       |
                                  +       +
                                  |*    * |
        +-+-+-+-+-+-+-+-+     +-+-+-+ +-+-+
        |X7    &A&      |     |C1      *|
        + #           # +     +         +
        |         p     |     |         |
        +               +     +       = +
        |       h       |     |  :      |
        +               +     +      p  +
        |   b           |     |         |
        +               +     +    p    +-+-+-+
        |       p       |     | b       |T2   |
        +               +     +!    = h +   * +
        |            B  |     |             $ |
        +               +     +         +   * +
        |               |     |         |     |
        + #           # + +-+-+-+ +-+-+-+-+-+-+
        |               | |C3         |
+-+-+-+-+-+ +-+-+-+-+-+-+ +b          +
|C6   !      *|           |u   :      |
+             +-+-+-+-+-+-+           +
|             |L5        !|      p  p |
+        p  & +      e    +           +
|             |    p         #        |
+     :       + :         +        %  +
|        =          p     |           |
+ :           +  h        +       h   +
|             |           |          *|
+    p        +        b  +-+ +-+-+-+-+
| H           |* &        |T4   |
+             +-+-+-+-+-+-+     +
|*           b|           |     |
+-+-+-+-+-+-+-+           + *$* +
                          |     |
                          +-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 7  8 rooms  80 enemies  92 x 80 m
                                  +-+-+-+-+
                                  |E0     |
                                  +       +
                                  |       |
                                  +   @   +
                                  |       |
                                  +       +
                                  |*    * |
        +-+-+-+-+-+-+-+-+     +-+-+-+ +-+-+
        |X7    &A&      |     |C1      *|
        + #           # +     +         +
        |    p    B     |     |         |
        +               +     +         +
        |       H    p  |     | =      :|
        +     p         +     +      b  +
        |   g           |     |         |
        +       G       +     +    p    +-+-+-+
        |               |     | B       |T2   |
        +        B    H +     +!      H +   * +
        |               |     |             $ |
        +               +     +    p  = +   * +
        |               |     |         |     |
        + #           # + +-+-+-+ +-+-+-+-+-+-+
        |               | |C3         |
+-+-+-+-+-+ +-+-+-+-+-+-+ +g          +
|C6   !      *|           |    :      |
+             +-+-+-+-+-+-+ p         +
|             |Lb        !|      b  B |
+      p p  & +*     g    +          u+
|             |    p         #    p   |
+     :       +           +        %  +
|        =          p :   |           |
+ :           +H e        +       H   +
|             |           |          *|
+    b        +        B  +-+ +-+-+-+-+
| H           |* &        |T4   |
+             +-+-+-+-+-+-+     +
|*           B|           |     |
+-+-+-+-+-+-+-+           + *$* +
                          |     |
                          +-+-+-+
",
        };

        // Work order 012: each golden seed pins its floor 1 and its floor 5 (the act boss's).
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_KeepTheirMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(11), "the version was raised: update the golden maps and this number together");
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor));
            string key = seedText + "/" + floor;
            TestContext.WriteLine("GOLDEN " + key + "\n" + map);
            Assert.That(Golden.ContainsKey(key), "no golden map for " + key + "\n" + map);
            Assert.That(map, Is.EqualTo(Golden[key].Replace("\r\n", "\n")), "map of seed " + key + "\n" + map);
        }

        // Work order 041: with the line off, the floor is version 9's, so the maps are version 9's, but for the version number.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_WithTheLineOff_KeepVersion9sMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            LevelLayout level = LevelGenerator.Generate(seed, floor, new LevelValues { InALine = false, RoomMood = false }); // work order 044: version 9 had no mood
            Assert.That(level.InALine, Is.False);
            Assert.That(level.Version, Is.EqualTo(LevelGenerator.Version));
            string map = LevelMap.Draw(level).Replace("version " + LevelGenerator.Version + " ", "version 9 ");
            string key = seedText + "/" + floor;
            Assert.That(map, Is.EqualTo(GoldenVersion9[key].Replace("\r\n", "\n")), "map of seed " + key + " with the line off\n" + map);
        }

        // Work order 028: with the themes off, the props are exactly those of before. Work order 034: and with the casters
        // off (the default) the whole floor is main's version 7's, so the maps are main's, but for the version number. Work
        // order 041: with the line off, as these floors have side rooms.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_WithThemesOff_KeepMainsVersion7Maps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            LevelLayout level = LevelGenerator.Generate(seed, floor, new LevelValues { Themes = false, InALine = false });
            Assert.That(level.Rooms.All(r => r.Theme == RoomTheme.None), "no room has a theme");
            string map = LevelMap.Draw(level).Replace("version " + LevelGenerator.Version + " ", "version 7 ");
            string key = seedText + "/" + floor;
            Assert.That(map, Is.EqualTo(GoldenThemesOff[key].Replace("\r\n", "\n")), "map of seed " + key + " with the themes off\n" + map);
        }

        // Work order 034: the casters' placement, pinned with the switch on. Work order 041: with the line off, as these
        // floors have side rooms (version 9's maps, but for the version number).
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_WithCastersOn_KeepTheirMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            LevelLayout level = LevelGenerator.Generate(seed, floor, new LevelValues { Casters = true, InALine = false, RoomMood = false }); // work order 044: version 9 had no mood
            string map = LevelMap.Draw(level).Replace("version " + LevelGenerator.Version + " ", "version 9 ");
            string key = seedText + "/" + floor;
            TestContext.WriteLine("GOLDEN CASTERS " + key + "\n" + map);
            Assert.That(level.Rooms.Any(r => r.EnemyGroups.Any(g => g.Kind == EnemyKind.Caster)), "it holds casters");
            Assert.That(map, Is.EqualTo(GoldenCasters[key].Replace("\r\n", "\n")), "map of seed " + key + " with the casters on\n" + map);
        }
        // ---------------- The validator ----------------

        const ulong Sample = 42UL;

        static LevelRoom First(LevelLayout l, RoomKind kind) => l.Rooms.First(r => r.Kind == kind);

        static void AssertFault(Action<LevelLayout> breakIt, string expected) => AssertFault(breakIt, expected, new LevelValues());

        /// <summary>Work order 041: the faults of the treasure rooms off the sides, on a floor with the line off.</summary>
        static void AssertSideRoomFault(Action<LevelLayout> breakIt, string expected) => AssertFault(breakIt, expected, new LevelValues { InALine = false });

        static void AssertFault(Action<LevelLayout> breakIt, string expected, LevelValues values)
        {
            LevelLayout level = LevelGenerator.Generate(Sample, values);
            Assume.That(LevelValidator.Validate(level, values), Is.Empty);
            breakIt(level);
            List<string> faults = LevelValidator.Validate(level, values);
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
            AssertFault(l => { var r = First(l, RoomKind.Exit); r.Rect.X = l.GridCells - 2; }, "outside the grid");

        [Test]
        public void Validator_ReportsARoomOfTheWrongSize() =>
            AssertFault(l => First(l, RoomKind.Combat).Rect.Width = 9, "not 5 to 7 on a side");

        [Test]
        public void Validator_ReportsRoomsThatOverlap() =>
            AssertFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Treasure);
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
            AssertFault(l => { var r = First(l, RoomKind.Treasure); l.Doors.RemoveAll(d => d.Joins(r.Order)); }, "cannot be reached from the entrance");

        [Test]
        public void Validator_ReportsTwoDoorsBetweenTheSameRooms_AndABossRoomWithTwoDoors() =>
            AssertFault(l =>
            {
                LevelDoor d = l.DoorsOf(First(l, RoomKind.Exit).Order).First();
                l.Doors.Add(new LevelDoor { RoomA = d.RoomA, RoomB = d.RoomB, Vertical = d.Vertical, Line = d.Line, From = d.From, Width = d.Width });
            }, "the exit room has 2 doors");

        [Test]
        public void Validator_ReportsTheSameRoomsJoinedTwice() =>
            AssertFault(l => l.Doors.Add(l.Doors[0]), "another door already joins");

        [Test]
        public void Validator_ReportsAMainPathOutOfOrder() =>
            AssertFault(l =>
            {
                LevelRoom boss = First(l, RoomKind.Exit), combat = First(l, RoomKind.Combat);
                boss.Kind = RoomKind.Combat;
                combat.Kind = RoomKind.Exit;
            }, "the main path runs");

        [Test]
        public void Validator_ReportsAMissingDoorOnTheMainPath() =>
            AssertFault(l => l.Doors.RemoveAll(d => d.Joins(First(l, RoomKind.Exit).Order)), "no door from");

        [Test]
        public void Validator_ReportsABossRoomTouchingTheStartRoom() =>
            AssertFault(l =>
            {
                LevelRoom start = First(l, RoomKind.Entrance);
                First(l, RoomKind.Exit).Rect = new CellRect(start.Rect.Right, start.Rect.Top, 8, 8);
            }, "the exit room touches the entrance");

        [Test]
        public void Validator_ReportsARewardRoomWithTwoDoors() => // work order 041: the side rooms' rule, with the line off
            AssertSideRoomFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Treasure);
                LevelDoor d = l.DoorsOf(reward.Order).First();
                l.Doors.Add(new LevelDoor { RoomA = reward.Order, RoomB = First(l, RoomKind.Entrance).Order, Vertical = d.Vertical, Line = d.Line, From = d.From, Width = d.Width });
            }, "has 2 doors, not 1");

        [Test]
        public void Validator_ReportsARewardRoomNotOffACombatRoom() => // work order 041: the side rooms' rule, with the line off
            AssertSideRoomFault(l =>
            {
                LevelRoom reward = First(l, RoomKind.Treasure);
                LevelDoor d = l.DoorsOf(reward.Order).First();
                d.RoomA = First(l, RoomKind.Entrance).Order;
                d.RoomB = reward.Order;
            }, "does not lead to a combat room");

        [Test]
        public void Validator_ReportsACombatRoomWithTwoRewardRooms() => // work order 041: the side rooms' rule, with the line off
            AssertSideRoomFault(l =>
            {
                List<LevelRoom> rewards = l.Rooms.Where(r => r.Kind == RoomKind.Treasure).ToList();
                int combat = l.DoorsOf(rewards[0].Order).First().Other(rewards[0].Order);
                LevelDoor d = l.DoorsOf(rewards[1].Order).First();
                d.RoomA = Math.Min(combat, rewards[1].Order);
                d.RoomB = Math.Max(combat, rewards[1].Order);
            }, "has two treasure rooms");

        [Test]
        public void Validator_ReportsADoorBetweenRoomsThatAreNotNeighbours() =>
            AssertFault(l =>
            {
                List<LevelRoom> path = LevelGeometry.MainPath(l);
                l.Doors.Add(new LevelDoor { RoomA = path[0].Order, RoomB = path[2].Order, Width = 1 });
            }, "not neighbours on the main path");

        [Test]
        public void Validator_ReportsTheWrongNumberOfRoomsOfAKind() =>
            AssertFault(l => First(l, RoomKind.Treasure).Kind = RoomKind.Combat, "Combat rooms, the plan has 4");

        [Test]
        public void Validator_ReportsRoomsListedOutOfOrder() =>
            AssertFault(l => { LevelRoom r = l.Rooms[1]; l.Rooms[1] = l.Rooms[2]; l.Rooms[2] = r; }, "in the list has order");

        [Test]
        public void Validator_ReportsAMissingPlayerStart() =>
            AssertFault(l => First(l, RoomKind.Entrance).PlayerStart = null, "has no player start");

        [Test]
        public void Validator_ReportsTheWrongNumberOfEnemyPoints() =>
            AssertFault(l => First(l, RoomKind.Exit).EnemyPoints.Clear(), "enemy groups for 0 enemy points");

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
                LevelRoom r = First(l, RoomKind.Exit);
                r.EnemyPoints[0] = new LevelPoint(r.Rect.X * l.CellSize + 0.5, r.Rect.Y * l.CellSize + 10);
            }, "nearer a wall");

        [Test]
        public void Validator_ReportsAPointTooNearADoor() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Entrance);
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

        // ---------------- The chain (work order 012) ----------------

        [Test]
        public void Validator_ReportsADoorOutOnTheWallOfTheDoorIn() =>
            AssertFault(l =>
            {
                LevelRoom r = First(l, RoomKind.Combat);
                LevelDoor wayIn = LevelGeometry.EntryDoor(l, r), wayOut = LevelGeometry.ExitDoor(l, r);
                wayOut.Vertical = wayIn.Vertical;
                wayOut.Line = wayIn.Line;
            }, "door out is on the wall of its door in");

        [Test]
        public void Validator_ReportsATreasureDoorNotOnASideWall() => // work order 041: the side rooms' rule, with the line off
            AssertSideRoomFault(l =>
            {
                LevelRoom t = First(l, RoomKind.Treasure);
                LevelDoor door = l.DoorsOf(t.Order).First();
                LevelRoom combat = l.Rooms[door.Other(t.Order)];
                LevelDoor wayIn = LevelGeometry.EntryDoor(l, combat);
                door.Vertical = wayIn.Vertical;
                door.Line = wayIn.Line;
            }, "is not on a side wall");

        [Test]
        public void Generator_EveryRoomsWayOn_IsNotOnTheWallOfItsWayIn_AndMostlyOpposite()
        {
            int rooms = 0, opposite = 0;
            for (ulong s = 0; s < 300; s++)
            {
                LevelLayout l = Make(s);
                foreach (LevelRoom r in LevelGeometry.MainPath(l))
                {
                    LevelDoor wayIn = LevelGeometry.EntryDoor(l, r), wayOut = LevelGeometry.ExitDoor(l, r);
                    if (wayIn == null || wayOut == null) continue;
                    int a = LevelGeometry.WallOf(r.Rect, wayIn), b = LevelGeometry.WallOf(r.Rect, wayOut);
                    Assert.That(a, Is.Not.EqualTo(b), "seed " + s + " room " + r.Order);
                    rooms++;
                    if (b == LevelGenerator.Opposite(a)) opposite++;
                }
            }
            TestContext.WriteLine("CHAIN rooms with a way in and out " + rooms + ", out on the opposite wall " + opposite);
            Assert.That(opposite, Is.GreaterThan(rooms / 3), "straight on is tried first");
        }

        // ---------------- The plan ----------------

        [Test]
        public void Plan_IsFourCombatRooms_TwoTreasureRooms_AndAnExitRoomAsLargeAsTheOldBossRoom()
        {
            // Work order 012 (decision 0008): it was five combat rooms, four reward rooms and a boss room.
            var plan = new LevelPlan();
            Assert.That(plan.CombatRooms, Is.EqualTo(4));
            Assert.That(plan.TreasureRooms, Is.EqualTo(2));
            Assert.That((plan.ExitSize.Min, plan.ExitSize.Max), Is.EqualTo((8, 8)));
            LevelLayout level = Make(Sample);
            Assert.That(level.Rooms.Count, Is.EqualTo(1 + 4 + 2 + 1));
            Assert.That(level.Rooms.Last(r => r.Kind != RoomKind.Treasure).Kind, Is.EqualTo(RoomKind.Exit));
        }

        [Test]
        public void Map_ShowsEveryRoom_TheStart_AndEveryEnemy()
        {
            LevelLayout level = Make(Sample);
            string map = LevelMap.Draw(level);
            foreach (LevelRoom r in level.Rooms) Assert.That(map, Does.Contain(r.Letter + r.Order.ToString(CultureInfo.InvariantCulture)), map);
            Assert.That(map.Count(c => c == '@'), Is.EqualTo(1), map);
            // Work order 008: each enemy point shows the small letter of its kind, not a dot; work order 012 adds the
            // capitals of lines (H, B) and the act boss (G).
            const string letters = "bsphgHBGe"; // work order 017: e the elite
            Assert.That(map.Split('\n').Skip(1).Sum(line => line.Count(c => letters.IndexOf(c) >= 0)), Is.EqualTo(level.Rooms.Sum(r => r.EnemyPoints.Count)), map);
            foreach (LevelRoom r in level.Rooms)
                foreach (EnemyGroup g in r.EnemyGroups) Assert.That(map, Does.Contain(g.Letter.ToString()), map);
        }
    }
}
