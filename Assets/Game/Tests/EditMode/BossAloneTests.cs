using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The boss alone in its room (work order 130; Kristian, 2026-11-25: "bossrommet. der tenker jeg bare bossen skal være"), the
    /// generator's version 24: the switch, the boss room's list of enemies on a thousand seeds, what left it (counted by kind, for
    /// the producer's report), every floor without the act boss version 23's field for field, the validator's rule, and version 24's
    /// pins and golden maps. With the switch off every floor is version 23's: RangedAroundTheRoomTests' version 23 pins and maps,
    /// held to their OLD numbers with the switch named off. Work order 135 (changed deliberately): version 24 is the line-ups named off
    /// (LevelValues.LineUps), so every value here names them off and version 24's pins and maps keep their numbers; with the line-ups
    /// named off these are the very floors of version 24 (LineUpsTests' switch-off pins are these).
    /// </summary>
    public class BossAloneTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static LevelValues On => new LevelValues { LineUps = false };
        static LevelValues Off => new LevelValues { BossAlone = false, LineUps = false };
        static LevelValues LancersOn => new LevelValues { Lancers = true, LineUps = false };
        static LevelValues LancersOff => new LevelValues { Lancers = false, LineUps = false };

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // RangedAroundTheRoomTests' fold, with the posts: the very fold of version 23's pins.
        static ulong Fold(LevelLayout level, ulong h)
        {
            h = FloorPrint.Of(level, h);
            foreach (LevelRoom r in level.Rooms)
            {
                h = Mix(h, (ulong)r.Shape);
                h = Mix(h, (ulong)r.SolidCount);
                foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                foreach (char ch in r.Template?.ToString() ?? "-") h = Mix(h, ch);
                h = Mix(h, (ulong)r.CoverTallAdded);
                h = Mix(h, (ulong)r.CoverLowAdded);
                foreach (EnemyGroup g in r.EnemyGroups)
                {
                    RangedPlace p = g.Ranged;
                    if (p == null) continue;
                    h = Mix(h, (ulong)p.Post);
                    h = Mix(h, (ulong)p.Rung);
                    h = Mix(h, (ulong)p.Cluster);
                    h = Mix(h, (ulong)p.ClusterSize);
                }
            }
            return h;
        }

        // Work order 155: from the run's set, read only; kept for a later test's reading (keep), or let go as read (the walk's last reader).
        static List<LevelLayout> Run(ulong runSeed, LevelValues v, int floors, bool keep = true)
        {
            var list = new List<LevelLayout>();
            string before = null;
            for (int floor = 1; floor <= floors; floor++)
            {
                LevelLayout l = keep ? SharedFloors.Generate(RunSeeds.FloorSeed(runSeed, floor), floor, v, before) : SharedFloors.Once(RunSeeds.FloorSeed(runSeed, floor), floor, v, before);
                list.Add(l);
                before = WayDowns.Shown(l);
            }
            return list;
        }

        /// <summary>RangedAroundTheRoomTests' two thousand floors and a hundred runs of five, folded, as its version 23 pins.</summary>
        // Work order 155: keep, the floors kept for a later test of the run that walks them again (the pins of the version after); else let go as read.
        static ulong Print(LevelValues v, bool keep)
        {
            SharedFloors.Prefetch(v, Enumerable.Range(0, 2000).Select(k => ((ulong)k * 7919UL + 13UL, k % 5 + 1))); // work order 155: a batch at a time, read in this order
            SharedFloors.PrefetchRuns(v, Enumerable.Range(0, 100).Select(r => (ulong)r * 104729UL + 103UL), 5);
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = Fold((keep ? SharedFloors.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v) : SharedFloors.Once(s * 7919UL + 13UL, (int)(s % 5) + 1, v)), h); // work order 155: from the run's set, read only
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5, keep)) h = Fold(l, h);
            return h;
        }

        static LevelRoom BossRoom(LevelLayout l) => l.Rooms.FirstOrDefault(r => r.EnemyGroups.Any(g => g != null && g.Boss));

        /// <summary>One room as text, every field the generator sets on it (its enemies, props, shape, template, cover, posts).</summary>
        static string RoomText(LevelLayout l, LevelRoom r)
        {
            var sb = new StringBuilder();
            sb.Append(r.Order).Append(' ').Append(r.Kind).Append(r.Elite ? " elite " : " ").Append(r.Rect).Append(RoomShapes.Describe(r));
            sb.Append(" template ").Append(r.Template?.ToString() ?? "-").Append(" cover ").Append(r.CoverTallAdded).Append('/').Append(r.CoverLowAdded);
            if (r.PlayerStart.HasValue) sb.Append(" start ").Append(r.PlayerStart.Value);
            for (int i = 0; i < r.EnemyPoints.Count; i++)
            {
                EnemyGroup g = r.GroupAt(i);
                sb.Append(" enemy ").Append(r.EnemyPoints[i]).Append(' ').Append(g);
                if (g.Ranged != null) sb.Append(" post ").Append(g.Ranged.Post).Append(' ').Append(g.Ranged.Rung).Append(' ').Append(g.Ranged.Cluster).Append(' ').Append(g.Ranged.ClusterSize);
                foreach (LevelPoint m in r.PlacesAt(i)) sb.Append(' ').Append(m);
            }
            sb.Append(" theme ").Append(r.Theme);
            foreach (LevelProp p in r.Props) sb.Append(" prop ").Append(p);
            return sb.ToString();
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion24_AndOnlyOnAFloorInARow()
        {
            Assert.That(new LevelValues().BossAlone, Is.True, "on by default (work order 130, his words)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(25), "work order 130's version, 24; work order 135's, 25");
            Assert.That(LevelBossAlone.On(On), Is.True);
            Assert.That(LevelBossAlone.On(Off), Is.False);
            Assert.That(LevelBossAlone.On(new LevelValues { InALine = false }), Is.False, "the floors before the line are as they were");
            Assert.That(LevelGenerator.Generate(0x130UL, 5, On).Version, Is.EqualTo(25));
            Assert.That(LevelGenerator.Generate(0x130UL, 5, Off).Version, Is.EqualTo(25), "off: version 23's floor, but for the number");
        }

        [Test]
        public void Clear_TakesEveryGroupButTheBoss_OutOfTheRoomThatHoldsIt_AndNothingElse()
        {
            var fights = new List<List<EnemyGroup>>
            {
                new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 3), new EnemyGroup(EnemyKind.Shooter) },
                new List<EnemyGroup> { new EnemyGroup(EnemyKind.Boss, 1, boss: true), new EnemyGroup(EnemyKind.Guard), new EnemyGroup(EnemyKind.Swarmer, 4), new EnemyGroup(EnemyKind.Caster) },
            };
            List<EnemyGroup> left = LevelBossAlone.Clear(fights, Off);
            Assert.That(left, Is.Empty, "off: nothing");
            Assert.That(fights[1].Count, Is.EqualTo(4));
            left = LevelBossAlone.Clear(fights, On);
            Assert.That(fights[0].Count, Is.EqualTo(2), "a room without the boss keeps what it holds");
            Assert.That(fights[1].Count, Is.EqualTo(1));
            Assert.That(fights[1][0].Boss, Is.True);
            Assert.That(left.Select(g => g.Kind), Is.EqualTo(new[] { EnemyKind.Guard, EnemyKind.Swarmer, EnemyKind.Caster }));
            Assert.That(left.Sum(g => g.Count), Is.EqualTo(6));
        }

        [Test]
        public void TheValidator_AsksTheBossRoomForTheBossAlone_WithTheSwitchOn()
        {
            LevelLayout l = LevelGenerator.Generate(0x130UL, 5);
            Assert.That(LevelValidator.Validate(l, On), Is.Empty);
            LevelRoom boss = BossRoom(l);
            Assert.That(boss, Is.Not.Null);
            Assert.That(LevelBossAlone.Asks(l, boss, On), Is.True);
            Assert.That(LevelBossAlone.Asks(l, boss, Off), Is.False);
            // A guard put back beside it: a fault with the switch on.
            boss.EnemyGroups.Add(new EnemyGroup(EnemyKind.Guard));
            boss.EnemyPoints.Add(new LevelPoint(boss.Rect.X * l.CellSize + 6, boss.Rect.Y * l.CellSize + 6));
            boss.EnemyPlaces.Add(new[] { boss.EnemyPoints[boss.EnemyPoints.Count - 1] });
            Assert.That(LevelValidator.Validate(l, On).Any(f => f.Contains("besides the act boss")), Is.True, string.Join("\n", LevelValidator.Validate(l, On)));
            // A floor 1 to 4 exit room is not asked.
            LevelLayout four = LevelGenerator.Generate(0x130UL, 4);
            LevelRoom exit = four.Rooms.First(r => r.Kind == RoomKind.Exit);
            Assert.That(LevelBossAlone.Asks(four, exit, On), Is.False);
            Assert.That(exit.EnemyGroups.Count, Is.GreaterThan(1), "floor 4's exit room holds what it held");
        }

        // ---------------- A thousand seeds of the boss floor ----------------

        /// <summary>
        /// The boss room's list of enemies is the boss alone on a thousand seeds, every floor valid; what left it, by kind, the
        /// same seeds with the switch off; and the rooms before it, field for field, where the floor was made on the same attempt.
        /// </summary>
        [Test, Timeout(3600000)] // the long sweeps' own time limit (see EveryFloorWithoutTheBoss_IsVersion23s_FieldForField)
        public void AThousandSeeds_TheBossRoomHoldsTheBossAlone_AndWhatLeftIt_IsCounted()
        {
            var left = new Dictionary<EnemyKind, int>();
            var groups = new Dictionary<EnemyKind, int>();
            int floors = 0, sameAttempt = 0, roomsBefore = 0, roomsBeforeSame = 0, bossSamePoint = 0;
            int fewest = int.MaxValue, most = 0, total = 0;
            // Work order 155: made a batch at a time and read in this order; the switch on's are version 24's floor 5, which LineUpsTests reads again.
            SharedFloors.Prefetch(On, Enumerable.Range(0, 1000).Select(k => ((ulong)k, 5)));
            SharedFloors.Prefetch(Off, Enumerable.Range(0, 1000).Select(k => ((ulong)k, 5)));
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout on = SharedFloors.Generate(s, 5, On), off = SharedFloors.Generate(s, 5, Off); // kept: BossRulesTests' floor 5 reads the switch off's again
                floors++;
                List<string> faults = LevelValidator.Validate(on, On);
                if (faults.Count > 0) Assert.Fail("seed " + s + ": " + string.Join("; ", faults));
                LevelRoom room = BossRoom(on);
                Assert.That(room, Is.Not.Null, "seed " + s + ": a boss room");
                Assert.That(room.Kind, Is.EqualTo(RoomKind.Exit));
                Assert.That(room.EnemyGroups.Count, Is.EqualTo(1), "seed " + s + ": the boss alone, not " + string.Join(", ", room.EnemyGroups));
                Assert.That(room.EnemyGroups[0].Boss && room.EnemyGroups[0].Kind == EnemyKind.Boss && room.EnemyGroups[0].Count == 1, Is.True);
                Assert.That(on.Rooms.Where(r => r != room).All(r => r.EnemyGroups.All(g => !g.Boss)), Is.True, "one boss on the floor");

                LevelRoom was = BossRoom(off);
                int n = 0;
                foreach (EnemyGroup g in was.EnemyGroups)
                {
                    if (g.Boss) continue;
                    left[g.Kind] = (left.TryGetValue(g.Kind, out int c) ? c : 0) + g.Count;
                    groups[g.Kind] = (groups.TryGetValue(g.Kind, out int k) ? k : 0) + 1;
                    n += g.Count;
                }
                fewest = Math.Min(fewest, n);
                most = Math.Max(most, n);
                total += n;
                if (on.Attempt != off.Attempt) continue;
                sameAttempt++;
                if (room.EnemyPoints[0].DistanceTo(was.EnemyPoints[was.EnemyGroups.FindIndex(g => g.Boss)]) < 1e-9) bossSamePoint++;
                foreach (LevelRoom r in on.Rooms)
                {
                    if (r.Order >= room.Order) continue;
                    roomsBefore++;
                    if (RoomText(on, r) == RoomText(off, off.Rooms[r.Order])) roomsBeforeSame++;
                }
            }
            string kinds = string.Join(", ", left.OrderByDescending(kv => kv.Value).Select(kv => string.Format(Inv, "{0} {1} ({2:0.00} a room, in {3} groups)", EnemyKinds.Word(kv.Key), kv.Value, kv.Value / (double)floors, groups[kv.Key])));
            TestContext.WriteLine(string.Format(Inv, "BOSSALONE floor 5, {0} seeds: left the boss room {1} enemies in all, {2:0.00} a room ({3} to {4}): {5}", floors, total, total / (double)floors, fewest, most, kinds));
            TestContext.WriteLine(string.Format(Inv, "BOSSALONE floor 5: made on the same attempt as with the switch off {0} of {1}; of those the boss on the same point {2}; the rooms before the boss room the same, field for field, {3} of {4}",
                                                sameAttempt, floors, bossSamePoint, roomsBeforeSame, roomsBefore));
            Assert.That(roomsBeforeSame, Is.EqualTo(roomsBefore), "every room before the boss room is version 23's where the floor was made on the same attempt");
            Assert.That(bossSamePoint, Is.EqualTo(sameAttempt), "the boss stands where it stood");
        }

        /// <summary>What left the boss room on the benchmark's own floor 5 (BenchmarkStage's run seed BENCH032), for the report.</summary>
        [Test]
        public void TheBenchmarksFloor5_WhatLeftTheBossRoom()
        {
            ulong run = LevelSeed.TryParse("BENCH032", out ulong s) ? s : 32UL; // BenchmarkStage.RunSeed, as it reads it
            ulong seed = RunSeeds.FloorSeed(run, 5);
            LevelLayout on = LevelGenerator.Generate(seed, 5, On), off = LevelGenerator.Generate(seed, 5, Off);
            LevelRoom was = BossRoom(off);
            string gone = string.Join(", ", was.EnemyGroups.Where(g => !g.Boss).Select(g => g.ToString()));
            TestContext.WriteLine("BOSSALONE the benchmark's floor 5 (" + LevelSeed.ToText(seed) + "): left the boss room " + was.EnemyGroups.Where(g => !g.Boss).Sum(g => g.Count) + " enemies: " + gone);
            TestContext.WriteLine("BOSSALONE the benchmark's floor 5 in a run (floors 1 to 5 of BENCH032): " + string.Join(", ", Run(run, Off, 5, false)[4].Rooms.Where(r => r.EnemyGroups.Any(g => g.Boss)).SelectMany(r => r.EnemyGroups).Where(g => !g.Boss).Select(g => g.ToString())));
            Assert.That(BossRoom(on).EnemyGroups.Count, Is.EqualTo(1));
            Assert.That(was.EnemyGroups.Count(g => !g.Boss), Is.GreaterThan(0), "with the switch off the room held more than the boss");
        }

        // ---------------- Every floor without the act boss is version 23's ----------------

        /// <summary>
        /// The floors of version 23's pins (two thousand floors and a hundred runs of five) that hold no act boss (floors 1 to 4) are,
        /// with the switch on, field for field what they are with it off (version 23's: RangedAroundTheRoomTests' pins, switch named off).
        /// </summary>
        // The long sweeps' own time limit (as FloorCharacterTests' and, since 340b4be, RoomShapesTests'): in Unity this sweep took 203 s
        // and timed out at the default 180 s on 2026-11-26 with other runs sharing the machine (it passed there on a quieter one). The
        // limit is for a hang, not for speed.
        [Test, Timeout(3600000)]
        public void EveryFloorWithoutTheBoss_IsVersion23s_FieldForField()
        {
            int compared = 0;
            // Work order 155: the pins' floors, from the run's set (version 24's pins below and RangedAroundTheRoomTests' version 23 pin read
            // them again), made a batch at a time; read only, each compared as before.
            foreach (LevelValues v in new[] { On, Off })
            {
                SharedFloors.Prefetch(v, Enumerable.Range(0, 2000).Where(k => k % 5 + 1 < v.FloorsPerAct).Select(k => ((ulong)k * 7919UL + 13UL, k % 5 + 1)));
                SharedFloors.PrefetchRuns(v, Enumerable.Range(0, 100).Select(r => (ulong)r * 104729UL + 103UL), 5); // the runs of five, as the pins read them
            }
            for (ulong s = 0; s < 2000; s++)
            {
                int floor = (int)(s % 5) + 1;
                if (floor >= On.FloorsPerAct) continue;
                ulong seed = s * 7919UL + 13UL;
                LevelLayout a = SharedFloors.Generate(seed, floor, On), b = SharedFloors.Generate(seed, floor, Off);
                Assert.That(Fold(a, 0UL), Is.EqualTo(Fold(b, 0UL)), "seed " + seed + " floor " + floor);
                Assert.That(a.Describe(), Is.EqualTo(b.Describe()), "seed " + seed + " floor " + floor);
                compared++;
            }
            for (ulong r = 0; r < 100; r++)
            {
                List<LevelLayout> a = Run(r * 104729UL + 103UL, On, 4), b = Run(r * 104729UL + 103UL, Off, 4);
                for (int i = 0; i < 4; i++)
                {
                    Assert.That(Fold(a[i], 0UL), Is.EqualTo(Fold(b[i], 0UL)), "run " + r + " floor " + (i + 1));
                    compared++;
                }
            }
            TestContext.WriteLine("BOSSALONE floors without the act boss compared, field for field: " + compared);
            Assert.That(compared, Is.EqualTo(1600 + 400));
        }

        // ---------------- Version 24's pins ----------------

        /// <summary>Version 24's pin with the lancers named on (the default). Each runtime has its own number: .NET's from the .NET rules run of this code, Unity's from a filtered Unity EditMode run of it.</summary>
        [Test, Timeout(3600000)] // the long sweeps' own time limit (see EveryFloorWithoutTheBoss_IsVersion23s_FieldForField)
        public void Version24_LancersOn_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOn, true); // work order 155: LineUpsTests' switch-off pin reads these floors again
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x5F07D15C837EAF2CUL; // Unity's EditMode run of this code (work order 130's filtered run, 2026-11-25)
#else
            const ulong pinned = 0x4B58BEAA19D7D624UL; // .NET, this code (the .NET rules run of work order 130, 2026-11-25)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("BOSSALONEPRINT LANCERS ON " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 24's floors, the lancers on, found " + found);
        }

        /// <summary>Version 24's pin with the lancers named off.</summary>
        [Test, Timeout(3600000)] // the long sweeps' own time limit (see EveryFloorWithoutTheBoss_IsVersion23s_FieldForField)
        public void Version24_LancersOff_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOff, false);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x5E8A1BC8237BF32CUL; // Unity's EditMode run of this code (work order 130's filtered run, 2026-11-25)
#else
            const ulong pinned = 0x916B5BDEE24CBA07UL; // .NET, this code (the .NET rules run of work order 130, 2026-11-25)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("BOSSALONEPRINT LANCERS OFF " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 24's floors, the lancers off, found " + found);
        }

        // ---------------- The golden maps of version 24 ----------------

        // Work order 130: version 23's golden seeds (RangedAroundTheRoomTests), floor 5, the lancers named on and off. Floors 1 and 4 of
        // the same seeds are version 23's maps, but for the number (asked here against the switch off).
        [TestCase("WALKTHRU", true)]
        [TestCase("RUNNER42", true)]
        [TestCase("WALKTHRU", false)]
        [TestCase("RUNNER42", false)]
        public void GoldenSeeds_KeepVersion24sMaps(string seedText, bool lancers)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(25), "the version was raised: update the golden maps and this number together");
            // Work order 135 (changed deliberately): version 25's maps are LineUpsTests'; these are version 24's, the line-ups named off, but for the number.
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, 5, lancers ? LancersOn : LancersOff)).Replace("version " + LevelGenerator.Version + " ", "version 24 ");
            string key = seedText + "/5" + (lancers ? "" : "/lancers off");
            TestContext.WriteLine("GOLDEN24 " + key + "\n" + map);
            Assert.That(GoldenVersion24.ContainsKey(key), "no golden map for " + key + "\n" + map);
            Assert.That(map, Is.EqualTo(GoldenVersion24[key].Replace("\r\n", "\n")), "map of seed " + key + "\n" + map);
            foreach (int floor in new[] { 1, 4 })
            {
                var named = lancers ? LancersOn : LancersOff;
                var off = lancers ? new LevelValues { Lancers = true, BossAlone = false, LineUps = false } : new LevelValues { Lancers = false, BossAlone = false, LineUps = false };
                Assert.That(LevelMap.Draw(LevelGenerator.Generate(seed, floor, named)), Is.EqualTo(LevelMap.Draw(LevelGenerator.Generate(seed, floor, off))), "floor " + floor);
            }
        }

        static readonly Dictionary<string, string> GoldenVersion24 = new Dictionary<string, string>
        {
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 24  17 rooms  71 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p  +
        |        P  | | %           |
        +   @   +-+ +-+    b    B   +
        |       | |          g     k|
        +-+    *+ +-+-+-+ #        i+
          |     |       |           |
          +-+-+-+       +-+  : :    +
                          |c       k|
                          +-+# h  hi+
                            |       |
                            +    p k+
                            |       |
                            + +-+-+-+
                            |P|
                          +-+ +
                          |   |
                          + +-+
                          | |
                    +-+-+-+ +-+-+-+
                    |C4           |
                    +             +
                    |    #   #    |
                    +           h +
                    |  h       #  |
                    +-+       +-+-+
                      |       |
                      +-+     +
                        |   b |
                    +-+-+g    +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       | p B o    c  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| #           # |   |  l *A       |   |Lw !h   &|
+ +-+       +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |       | | |           |P|       |B  | |     |
+ +-+       +-+ +           + +       +  =+-+ x [p+-+
|               |           | |       |            l|
+       G       +         +-+ +-+     +    h      # +
|               |         |T6   |     |   #     e   |
+ +-+       +-+ +         +i    +-+-+-+     b #    x+
| | |       | | |         |k     P                  |
+ +-+       +-+ +         +  $  +-+-+-+  [+-+[   g+-+
|               |         |  u  |     |   | |     |
+ #           # +         +-+-+-+     +   +-+   +-+
|               |                     |     c   |
+-+-+-+-+-+ +-+-+                     +-+-+-+ +-+
          |P|                               |P|
          + +-+                             + +-+
          |   |                             |   |
          +-+ +                             +-+ +
            | |                               | |
            + +-+-+ +-+-+-+             +-+-+-+ +-+-+-+-+-+
            |C14  | |     |             |C10            ki|
        +-+-+b B  +-+     +             +i             h b+
        |           # h   |             |   #             |
        +*  #             +   +-+-+-+   +# h    :        k+
        |A o  g      p    |   |T12 u|   |    %      B     |
        +*   c  p         +-+-+ i   +-+-+             p   +
        |  #     #   : #   P    $    P     c g  p         |
        +-+-+     +-+     +-+-+ i   +-+-+   +-+     +-+   +
            |   h | |     |   |     |   |   | |   u | |   |
            +-+-+-+ +-+-+-+   +-+-+-+   +-+-+ +-+-+-+ +-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 24  17 rooms  75 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
                                        +-+-+
                                        |E0 |
                                      +-+   +
                                      |     |
                                      +   @ +-+
                                      |       |
                                      +       +
                                      |*    * |
                                      +-+ +-+-+
                                        |P|
                                        + +
                                        | |
                                      +-+ +-+-+-+
                                      |C2    k  |
                                      +i        +
                                      |     #c #|
                                      +    #    +
                                      |      h  |
                                      +-+B    +-+
                                        |     |
                                      +-+     +-+
                                      |  %   : k|
                +-+-+-+   +-+-+-+     +       h +
                |C4  !|   |# c  |     |   p     |
    +-+-+-+     +& p  +   + #   + +-+-+      c  +
    |T6k  |     |     |   |w  # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+l   B          +-+-+   +     +
    |     |     |  =            |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |               |
      + +       +  h  +-+=   c [+
      | |       |l  #h| |       |
    +-+ +-+     +     + +-+  p  +
    |L8   |     | !   |   |     |
    +     +     +-+-+-+   +-+-+-+
    |  :  |
+-+-+x    +-+-+     +-+-+-+-+-+-+-+
|b  :  s  q c |     |C10!! !  ! &!|
+         B   +-+-+ +     c # =  g+
|   s :        P  | |   [         |
+       e     +-+ +-+     # b    &+
| l   h    g  | |        p    [   |
+-+-+     +-+-+ +-+-+-+           +
    |h   x|           | =        #|
    +     +           +-+-+B  [  l+
    |^~l~~|               |     # |
    +-+-+-+               +x   p  +
                          |      x|
                          += h    +
                          |   #   |
                          +    h  +                 +-+-+-+-+-+-+-+
                          |       |                 |X16          |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     +   +-+   +-+ +-+
                          | |     |C14   b    |     |               |
                        +-+ +-+   +%      +-+ +     +               +
                        |T12  |   |     B | | |     |               |
                        +     +-+-+       +-+ +-+-+ +       G      o+
                        |      P        :   g  P  | |              Y|
                        + i$i +-+-+  #    +-+ +-+ +-+              o+
                        |     |   |     c | | | |                   |
                        +-+-+-+   +   p  :+-+ + +-+-+   +-+   +-+ +-+
                                  |         hi|     |   | |   | | |
                                  +    #w h   +     + # +-+   +-+#+
                                  |u         l|     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
            ["WALKTHRU/5/lancers off"] = @"seed WALKTHRU  floor 5  version 24  17 rooms  71 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p  +
        |        P  | | %           |
        +   @   +-+ +-+    b    B   +
        |       | |          g     k|
        +-+    *+ +-+-+-+ #        i+
          |     |       |           |
          +-+-+-+       +-+  : :    +
                          |c       k|
                          +-+# h  hi+
                            |       |
                            +    p k+
                            |       |
                            + +-+-+-+
                            |P|
                          +-+ +
                          |   |
                          + +-+
                          | |
                    +-+-+-+ +-+-+-+
                    |C4           |
                    +             +
                    |    #   #    |
                    +           h +
                    |  h       #  |
                    +-+       +-+-+
                      |       |
                      +-+     +
                        |   b |
                    +-+-+g    +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       |   B o    c  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| #           # |   | p  *A       |   |L8 !h   &|
+ +-+       +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |       | | |           |P|       |B  | |     |
+ +-+       +-+ +           + +       +  =+-+ x [p+-+
|               |           | |       |             |
+       G       +         +-+ +-+     +    h  #    &+
|               |         |T6   |     |   #     e   |
+ +-+       +-+ +         +i    +-+-+-+ #   b      x+
| | |       | | |         |k     P                  |
+ +-+       +-+ +         +  $  +-+-+-+  [+-+  [ g+-+
|               |         |  u  |     |   | |w    |
+ #           # +         +-+-+-+     +   +-+   +-+
|               |                     |     c   |
+-+-+-+-+-+ +-+-+                     +-+-+-+ +-+
          |P|                               |P|
          + +-+                             + +-+
          |   |                             |   |
          +-+ +                             +-+ +
            | |                               | |
            + +-+-+ +-+-+-+             +-+-+-+ +-+-+-+-+-+
            |C14  | |     |             |C10            ki|
        +-+-+b B  +-+     +             +i             h b+
        |           # h   |             |   #             |
        +*  #             +   +-+-+-+   +# h    :        k+
        |A o  g      p    |   |T12 u|   |    %      B     |
        +*   c  p         +-+-+ i   +-+-+             p   +
        |  #     #   : #   P    $    P     c g  p         |
        +-+-+     +-+     +-+-+ i   +-+-+   +-+     +-+   +
            |   h | |     |   |     |   |   | |   u | |   |
            +-+-+-+ +-+-+-+   +-+-+-+   +-+-+ +-+-+-+ +-+-+
",
            ["RUNNER42/5/lancers off"] = @"seed RUNNER42  floor 5  version 24  17 rooms  75 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
                                        +-+-+
                                        |E0 |
                                      +-+   +
                                      |     |
                                      +   @ +-+
                                      |       |
                                      +       +
                                      |*    * |
                                      +-+ +-+-+
                                        |P|
                                        + +
                                        | |
                                      +-+ +-+-+-+
                                      |C2    k  |
                                      +i        +
                                      |     #c #|
                                      +    #    +
                                      |      h  |
                                      +-+B    +-+
                                        |     |
                                      +-+     +-+
                                      |  %   : k|
                +-+-+-+   +-+-+-+     +       h +
                |C4  !|   |# c  |     |   p     |
    +-+-+-+     +& p  +   + #   + +-+-+      c  +
    |T6k  |     |     |   |w  # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    B          +-+-+   +     +
    |     |     |  =            |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |               |
      + +       +  h  +-+=   c [+
      | |       |   #h| |       |
    +-+ +-+     +     + +-+  p  +
    |L8   |     | !   |   |     |
    +     +     +-+-+-+   +-+-+-+
    |     |
+-+-+x   q+-+-+     +-+-+-+-+-+-+-+
|b     p    c |     |C10!! !    &!|
+         B   +-+-+ +     c # =  g+
|   p :        P  | | # [         |
+ :     e q   +-+ +-+     # b    &+
|     h    g  | |        p    [   |
+-+-+     +-+-+ +-+-+-+          #+
    |h   x|           | =         |
    +     +           +-+-+B  [  !+
    | ^~~ |               |     = |
    +-+-+-+               +x   p  +
                          |      x|
                          += h    +
                          |   #   |
                          +    h  +                 +-+-+-+-+-+-+-+
                          |       |                 |X16          |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     +   +-+   +-+ +-+
                          | |     |C14   b    |     |               |
                        +-+ +-+   +%      +-+ +     +               +
                        |T12  |   |     B | | |     |               |
                        +     +-+-+       +-+ +-+-+ +       G      o+
                        |      P        :   g  P  | |              Y|
                        + i$i +-+-+  #    +-+ +-+ +-+              o+
                        |     |   |     c | | | |                   |
                        +-+-+-+   +   p  :+-+ + +-+-+   +-+   +-+ +-+
                                  |         hi|     |   | |   | | |
                                  +    #w h   +     + # +-+   +-+#+
                                  |u          |     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };
    }
}
