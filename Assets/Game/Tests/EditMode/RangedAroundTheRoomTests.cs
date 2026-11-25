using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Ranged enemies around the room (work order 124; Kristian, 2026-11-25: "det er litt kjedelig at alle range alltid er gruppert
    /// bakerst. De kan være litt mer strategisk plassert rundt i rommet", and he ticked "Ja, bare når den synes", "Ja, med ett sekunds
    /// pause", "Blanding" and "Fra etasje 4"), the generator's version 23: the switch and its parts, the camera's view of the ground as a
    /// rule, the opening rule of the fight, the validator's rules for the patterns, and the pins: with the switch off every floor is
    /// version 22's, field for field (118's pin, its OLD numbers); version 23 has two pins and two sets of golden maps, the lancers named on
    /// and named off, so the producer can turn the lancers off by default with one line and no pin taken again. The thousand seeds a floor
    /// are <see cref="RangedAroundTheRoomFloorsTests"/>.
    /// </summary>
    public class RangedAroundTheRoomTests
    {
        // Work order 130 (changed deliberately): version 23's floors are the boss alone named off (LevelValues.BossAlone), so its pins and maps keep their numbers.
        static LevelValues LancersOn => new LevelValues { Lancers = true, BossAlone = false };
        static LevelValues LancersOff => new LevelValues { Lancers = false, BossAlone = false };
        static LevelValues Version22 => new LevelValues { Lancers = true, RangedAroundTheRoom = false, BossAlone = false };

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // As LancersInTheFloorsTests folds its versions' floors (FloorCharacterTests' and ExitRoomsInTheFloorsTests' fold).
        static ulong Fold(LevelLayout level, ulong h, bool posts)
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
                if (!posts) continue;
                // Version 23: where its rules stood each ranged group.
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

        static List<LevelLayout> Run(ulong runSeed, LevelValues v, int floors)
        {
            var list = new List<LevelLayout>();
            string before = null;
            for (int floor = 1; floor <= floors; floor++)
            {
                LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(runSeed, floor), floor, v, exitBefore: before);
                list.Add(l);
                before = WayDowns.Shown(l);
            }
            return list;
        }

        /// <summary>Two thousand floors (seeds s * 7919 + 13, floors 1 to 5 in turn) and a hundred runs of five, folded: the very floors and fold of version 22's pin (LancersInTheFloorsTests).</summary>
        static ulong Print(LevelValues v, bool posts)
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = Fold(LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v), h, posts);
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5)) h = Fold(l, h, posts);
            return h;
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion23_AndEveryPartOn()
        {
            var v = new LevelValues();
            Assert.That(v.RangedAroundTheRoom, Is.True, "on by default (work order 124)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(24), "work order 124's version, 23; work order 130's, 24");
            RangedAroundValues r = v.RangedAround;
            Assert.That(r.OnScreenOnly && r.WakeGrace && r.NearFlank && r.PairsAndNests && r.Ladder && r.OneAtTheBack && r.WarderInThePack && r.Crossfire && r.TellsTheLog,
                        Is.True, "every part on");
            Assert.That(r.GraceSeconds, Is.EqualTo(1.0f), "his tick: about a second");
            Assert.That(r.NearFlankShare, Is.EqualTo(0.6), "the order's first guess");
            Assert.That(r.SinglesShare + r.PairShare + r.NestShare, Is.EqualTo(1.0).Within(1e-9), "the mix's shares");
            Assert.That(r.CrossfireFromFloor, Is.EqualTo(4), "his tick: from floor 4");
            Assert.That(LevelGenerator.Generate(0x124UL, 4).Version, Is.EqualTo(24));
            Assert.That(LevelGenerator.Generate(0x124UL, 4, Version22).Version, Is.EqualTo(24), "off: version 22's floor, but for the number");
        }

        /// <summary>The design view's numbers are the default camera's (FeelValues): the floors' rule asks the camera the game starts with.</summary>
        [Test]
        public void TheDesignView_IsTheDefaultCamerasNumbers_AndItsGround()
        {
            var feel = new FeelValues();
            RangedAroundValues r = new LevelValues().RangedAround;
            Assert.That(r.ViewDistance, Is.EqualTo(feel.CameraDistance), "FeelValues.CameraDistance");
            Assert.That(r.ViewPitch, Is.EqualTo(feel.CameraPitch), "FeelValues.CameraPitch");
            Assert.That(r.ViewFieldOfView, Is.EqualTo(feel.FieldOfView), "FeelValues.FieldOfView");
            // metrics.md section 1: 7.7 m south, 11.3 m north, 13.3 m to each side at her depth.
            GroundQuad q = LevelRanged.DesignView(new LevelPoint(0, 0), r);
            Assert.That(q.Known, Is.True);
            Assert.That(q.Z0, Is.EqualTo(-7.7).Within(0.05), "the bottom edge");
            Assert.That(q.Z2, Is.EqualTo(11.3).Within(0.05), "the top edge");
            Assert.That(ScreenGround.Inside(q, 13.25, 0), Is.EqualTo(0).Within(0.1), "the side at her depth, 13.3 m");
            Assert.That(ScreenGround.Inside(q, 0, 0), Is.EqualTo(7.7).Within(0.06), "her nearest edge is the bottom one");
            Assert.That(ScreenGround.Shows(q, 0, 10.7, 0.5), Is.True, "G2: 10.8 m north (the top edge is 11.27 m: 10.77 with the margin)");
            Assert.That(ScreenGround.Shows(q, 0, 10.8, 0.5), Is.False);
            Assert.That(ScreenGround.Shows(q, 0, -7.2, 0.5), Is.True, "G2: 7.2 m south");
            Assert.That(ScreenGround.Shows(q, 0, -7.3, 0.5), Is.False);
        }

        /// <summary>The game's real camera, handed in by its axes, gives the ground the same rule does, at any angle.</summary>
        [Test]
        public void TheGround_FromACamerasAxes_IsTheRulesGround_AtAnyAngle()
        {
            foreach (double pitch in new[] { 40.0, 55.0, 70.0 })
                foreach (double distance in new[] { 18.0, 28.0 })
                {
                    double p = pitch * Math.PI / 180.0;
                    double fy = -Math.Sin(p), fz = Math.Cos(p);
                    GroundQuad a = ScreenGround.Design(3, 4, distance, pitch, 30, 16.0 / 9.0);
                    GroundQuad b = ScreenGround.FromCamera(3, Math.Sin(p) * distance, 4 - Math.Cos(p) * distance, 0, fy, fz, 0, Math.Cos(p), Math.Sin(p), 1, 0, 0, 30, 16.0 / 9.0, 0);
                    Assert.That(a.X0, Is.EqualTo(b.X0).Within(1e-9));
                    Assert.That(a.Z2, Is.EqualTo(b.Z2).Within(1e-9));
                    Assert.That(ScreenGround.Shows(a, 3, 4, 1), Is.True, "she is on her own screen at pitch " + pitch);
                }
            // A camera turned (a yaw of 90 degrees: looking east) shows the ground east of her, not north.
            GroundQuad east = ScreenGround.FromCamera(-16.06, 22.94, 0, Math.Cos(55 * Math.PI / 180), -Math.Sin(55 * Math.PI / 180), 0,
                                                      Math.Sin(55 * Math.PI / 180), Math.Cos(55 * Math.PI / 180), 0, 0, 0, -1, 30, 16.0 / 9.0, 0);
            Assert.That(ScreenGround.Shows(east, 10, 0, 0.5), Is.True, "10 m ahead of a camera looking east");
            Assert.That(ScreenGround.Shows(east, 0, 12, 0.5), Is.True, "12 m to its side");
            Assert.That(ScreenGround.Shows(east, 0, 15, 0.5), Is.False, "15 m to its side is off");
            // A camera that does not look down at the ground shows no floor to ask: everything is let through.
            GroundQuad none = ScreenGround.FromCamera(0, 10, 0, 0, 1, 0, 0, 0, -1, 1, 0, 0, 30, 1.6, 0);
            Assert.That(none.Known, Is.False);
            Assert.That(ScreenGround.Shows(none, 100, 100, 0.5), Is.True);
        }

        // ---------------- The opening rule of the fight (parts 1 and 2) ----------------

        [Test]
        public void TheOpening_WaitsASecondAfterTheWaking_ForOneInReach_AndOnlyOnScreen()
        {
            RangedAroundValues r = new LevelValues().RangedAround;
            Assert.That(RangedOpeningRules.MayBegin(null, false, 0f, 0.1f, true), Is.True, "no values (the switch off, no fight): as before");
            Assert.That(RangedOpeningRules.MayBegin(r, true, 10f, 10.5f, true), Is.False, "in reach at the waking: not in the first second");
            Assert.That(RangedOpeningRules.MayBegin(r, true, 10f, 11.0f, true), Is.True, "after it");
            Assert.That(RangedOpeningRules.MayBegin(r, true, 10f, 10.2f, false), Is.True, "out of reach at the waking: no grace");
            Assert.That(RangedOpeningRules.MayBegin(r, false, 10f, 20f, false), Is.False, "off screen: never begins");
            var noScreen = new RangedAroundValues { OnScreenOnly = false };
            Assert.That(RangedOpeningRules.MayBegin(noScreen, false, 10f, 20f, false), Is.True, "part 1 off");
            var noGrace = new RangedAroundValues { WakeGrace = false };
            Assert.That(RangedOpeningRules.MayBegin(noGrace, true, 10f, 10.1f, true), Is.True, "part 2 off");
        }

        // ---------------- The pins ----------------

        /// <summary>
        /// With the ranged enemies around the room off (the lancers named on) every floor is version 22's, field for field: the same
        /// floors and runs as version 22's own pin (LancersInTheFloorsTests.Version22_...), held to that pin's OLD numbers, taken by work
        /// order 118.
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloorsAndAHundredRuns_AreVersion22s_FieldForField()
        {
            ulong h = Print(Version22, false);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0xFE9160D84BC9D5EEUL; // version 22's, Unity's EditMode run (work order 118's filtered run, 2026-11-25)
#else
            const ulong pinned = 0x48C9546D2550F782UL; // version 22's, .NET (the .NET rules run of work order 118, 2026-11-25)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("RANGEDOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the ranged enemies around the room off: version 22's floors, found " + found);
        }

        /// <summary>Version 23's pin with the lancers named on (the default today). Each runtime has its own number: .NET's from the .NET rules run of this code, Unity's from a filtered Unity EditMode run of it.</summary>
        [Test]
        public void Version23_LancersOn_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOn, true);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0xC3D33E230B076707UL; // Unity's EditMode run of this code (work order 124's second round, 2026-11-25: crossfire from the side too; 0x25DD83C6CFB6137B with it north or south only)
#else
            const ulong pinned = 0x7DEEE9B0606E1190UL; // .NET, this code (the .NET rules run of work order 124's second round, 2026-11-25: crossfire from the side too)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("RANGEDPRINT LANCERS ON " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 23's floors, the lancers on, found " + found);
        }

        /// <summary>Version 23's pin with the lancers named off: the floors the game makes if the producer turns the lancers off by default.</summary>
        [Test]
        public void Version23_LancersOff_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(LancersOff, true);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x7B08321A30B49AD7UL; // Unity's EditMode run of this code (work order 124's second round, 2026-11-25: crossfire from the side too; 0xC26BF39017A4010A with it north or south only)
#else
            const ulong pinned = 0xA55893AF50F6CDCBUL; // .NET, this code (the .NET rules run of work order 124's second round, 2026-11-25: crossfire from the side too)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("RANGEDPRINT LANCERS OFF " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 23's floors, the lancers off, found " + found);
        }

        // ---------------- The golden maps of version 23 ----------------

        // Work order 124: what the generator of version 23 makes with its defaults, the lancers named on and named off (each golden seed's
        // floors 1, 4 and 5). Floors 1 to 3 hold no lancer, so floor 1's map is one for both.
        [TestCase("WALKTHRU", 1, true)]
        [TestCase("WALKTHRU", 4, true)]
        [TestCase("WALKTHRU", 5, true)]
        [TestCase("RUNNER42", 1, true)]
        [TestCase("RUNNER42", 4, true)]
        [TestCase("RUNNER42", 5, true)]
        [TestCase("WALKTHRU", 1, false)]
        [TestCase("WALKTHRU", 4, false)]
        [TestCase("WALKTHRU", 5, false)]
        [TestCase("RUNNER42", 1, false)]
        [TestCase("RUNNER42", 4, false)]
        [TestCase("RUNNER42", 5, false)]
        public void GoldenSeeds_KeepVersion23sMaps(string seedText, int floor, bool lancers)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(24), "the version was raised: update the golden maps and this number together");
            // Work order 130 (changed deliberately): version 24's floor 5 maps are BossAloneTests'; these are version 23's, the boss alone named off, but for the number.
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor, lancers ? LancersOn : LancersOff)).Replace("version " + LevelGenerator.Version + " ", "version 23 ");
            string key = seedText + "/" + floor + (lancers || floor < 4 ? "" : "/lancers off");
            TestContext.WriteLine("GOLDEN23 " + key + "\n" + map);
            Assert.That(GoldenVersion23.ContainsKey(key), "no golden map for " + key + "\n" + map);
            Assert.That(map, Is.EqualTo(GoldenVersion23[key].Replace("\r\n", "\n")), "map of seed " + key + "\n" + map);
        }

        static readonly Dictionary<string, string> GoldenVersion23 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 23  17 rooms  57 enemies  148 x 88 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
                                                                +-+-+-+
                                                                |L8!  |
                                          +-+-+-+-+-+-+-+     +-+   p +-+
                                          |C1bik k     u|     |    e  [ |
                                          +             +   +-+         +-+
                                          | :           |   |   [ =   h   |
                                          +        p    +-+-+       #     +
                                          |      %       P       p    b# !|
                                          +         # +-+-+-+ +-+    #+-+ +
                                          |           |     | | |     | | |
                                          +       # +-+     + +-+ #   +-+ +
                                          |         |       |   [     =   |
                                          +       +-+       +&            +
                                          |   h   |         |!            |
                                          +     p +         +-+-+ +-+-+-+-+
                                          |       |             |P|
+-+-+-+-+-+-+-+-+-+-+       +-+-+-+-+-+   +-+-+ +-+             + +
|h16 :              |       |C14      |       |P|               | |
+  h+-+   +-+       +   +-+-+  # b    +       + +               + +
|   | |   | |       |   |i    p       |       | |               | |
+-+ +-+   +-+ +-+   +   +Ao       #   +     +-+ +-+           +-+ +-+
  |           | |   |   |i h          |     |T12  |           |T6   |
+-+           +-+   +-+-+   # : :     +-+-+-+     +           +  $  +
|            B       P           #     P         k|           |    k|
+                   +-+-+-+-+   p     +-+-+-+ i$i +           +    i+
|      p            |       |         |     |     |           |u   k|
+-+  :        +-+   +       +-+-+-+-+-+     +-+-+-+           +-+ +-+
  |        p  | |   |                                           |P|
+-+ +-+   +-+ +-+   +               +-+-+-+ +-+-+ +-+-+         + +
|   | |   | |       |               |C2   | |   | |k  |         | |
+   +-+   +-+       +               +     +-+   +-+   +   +-+-+-+ +-+-+
|             b  %  |               |k                |   |C4         |
+-+-+-+-+-+-+-+-+-+-+     +-+-+     +               h +   +   +-+ +-+ +-+
                          |E0*|     |      %    : p   |   |   | | | |   |
                      +-+-+   +-+-+-+  :  #           +-+-+  #+-+ +-+#  +
                      |        P           b  p        P       # : # h *|
                      +   @   +-+-+-+     +-+#  +-+   +-+-+           oA+
                      |       |     |     | | u | |   |   |     b      *|
                      +-+    *+     +-+-+-+ +-+-+ +-+-+   +        p  p +
                        |     |                           |    #        |
                        +-+-+-+                           +   +-+ +-+   +
                                                          |   | | | |   |
                                                          +   +-+ +-+ +-+
                                                          |           |
                                                          +-+-+-+-+-+-+
",
            ["WALKTHRU/4"] = @"seed WALKTHRU  floor 4  version 23  17 rooms  84 enemies  116 x 136 m  themes C2 archive, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 stair-head
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p k+
        |        P  | | %           |
        +   @   +-+ +-+    b    B   +
        |       | |          g      |
        +-+    *+ +-+-+-+ #         +
          |     |       |           |
          +-+-+-+       +-+ :    :  +
                          |h       k|
                          +-+  h  hi+
                            |       |
                            +    p  +
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
                    |        :    |
                    + q  q      h +
                    |  h          |
                    +-+  q    +-+-+
                      |       |
                      +-+     +
                        |   b |
                    +-+-+g    +-+-+
                    |             |
                    +             +
                    | p B  :   c  |
                    +             +   +-+-+-+-+-+
                    |  ~~~~       |   |Lw !h    |
                    +-+-+-+-+ +-+-+   +   +-+ p +-+
                            |P|       |B  | |     |
+-+-+-+-+-+-+-+-+-+-+       + +       +  [+-+ x [p+-+
|X16           h    |       | |       |             |
+   +-+-+b  +-+-+h  +     +-+ +-+     +    h  #     +
|   |   |   |   |   |     |T6   |     |         e # |
+ p +   +  x+   +   +     +k    +-+-+-+     b  #   l+
|   |   |   |   |   |     |      P                  |
+   +-+-+   +-+-+:  +     +  $  +-+-+-+  =+-+=   g+-+
| p            c    |     |   u |     |   | |     |
+                   +     +-+-+-+     +   +-+   +-+
|  %       p     :  |                 |   ! h   |
+     #             +                 +-+-+-+ +-+
|  B   c           u|                       |P|
+        #          +                       + +-+
|                   |                       |   |
+                   +                       +-+ +
|x                  |                         | |
+-+-+-+-+-+ +-+-+-+-+                   +-+-+-+ +-+-+-+-+-+
          |P|                           |C10        k ik u|
          + +                           +              h b+
          | |                           |   #             |
          + +-+-+ +-+-+-+     +-+-+-+   +# h             :+
          |C14  | |     |     |T12u |   |    %      B     |
      +-+-+b h  +-+     +-+-+-+ i   +-+-+             p   +
      |i        x:  #    P      $    P     c g  p         |
      +A o #            +-+-+-+ i   +-+-+   +-+     +-+   +
      |i    g      p    |     |     |   |   | |    l| |  u|
      +l   c     h      +     +-+-+-+   +-+-+ +-+-+-+ +-+-+
      |  #    #      #  |
      +-+-+    B+-+     +
          |     | |     |
          +-+-+-+ +-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 23  17 rooms  94 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
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
| # h         # |   |  l *A       |   |Lw !h   &|
+ +-+ h     +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |    p  | | |           |P|       |B  | |     |
+ +-+g    p +-+ +           + +       +  =+-+ x [p+-+
|              c|           | |       |            l|
+       G c p   +         +-+ +-+     +    h      # +
| B           x |         |T6   |     |   #     e   |
+ +-+b      +-+ +         +i    +-+-+-+     b #    x+
| | |   h   | | |         |k     P                  |
+ +-+ # #  #+-+ +         +  $  +-+-+-+  [+-+[   g+-+
|  x         :  |         |  u  |     |   | |     |
+ #           # +         +-+-+-+     +   +-+   +-+
|  w            |                     |     c   |
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
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 23  17 rooms  58 enemies  136 x 136 m  themes C2 shrine, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
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
      |C2       |
      +        b+
      |  #      |
      +      #  +                                   +-+-+ +-+-+ +-+-+
      |         |                                   |X16| |   | |   |
      +-+     +-+                                   +   +-+   +-+h  +
        |     |                                     |               |
      +-+     +-+                                   + +-+     : +-+ +
      |  :      |                                   | | |       | | |
      +         +                                   + +-+      :+-+ +
      |  #   #  |                                   |       p       |
      +-+     +-+                                   +    p          +
        |  p #|                                     |               |
      +-+     +-+                                   + +-+      b+-+ +
      |   h o   |                                   | | |       | | |
      +    *  p +                                   + +-+       +-+ +
      |     A   |                                   |               |
      +-+ +-+-+-+                                   +   +-+   +-+   +
        |P|                                         |   | |   | |   |
        + +                                         +   +-+   +-+   +
        | |                                         |           :   |
        + +                   +-+-+-+-+-+           +   %           +
        | |                   |L8       |           |               |
+-+-+-+-+ +-+-+               + # +-+   +-+         +-+-+-+-+ +-+-+-+
|C4           |               |   | |B    |                 |P|
+   p         +               +  [+-+     +-+             +-+ +
|             |               |         p   |             |   |
+ +-+    q+-+ +     +-+-+-+   +            #+             + +-+
| | |     | | |     |T6k k|   |     #       |             | |
+ +-+     +-+ +-+-+-+     +-+-+       #   e&+       +-+-+-+ +-+
|              P        $  P       =    p  &|       |C14Ai    |
+ +-+  p  +-+ +-+-+-+     +-+-+  =+-+[    +-+     +-+         +-+
| | |:    | | |     |   u |   |   | |  h  |       |   # o    #  |
+ +-+     +-+ +     +-+-+-+   +   +-+   +-+       +        h    +
|      b      |               |   =     |         |  b      p   |
+-+  h      +-+               +-+-+-+ +-+         +             +
  |   ~~~^  |                       |P|           |     :  :    |
  +-+-+-+-+-+                     +-+ +           + #    #    # +
                                  |   |           |          p  |
                                  + +-+           +           +-+
                                  | |             |           |
                                +-+ +-+           +-+-+ +-+-+-+
                                |C10  |               |P|
                                +     +-+             + +
                                |       |             | |
                                +    # #+           +-+ +-+
                                |       |           |T12 u|
                                +   b   +-+-+-+-+-+-+   i +
                                |    %         P        $ |
                                +  p          +-+-+-+   i +
                                |          :  |     |     |
                                +             +     +-+-+-+
                                |          h i|
                                +        p   k+
                                |k            |
                                +-+-+-+-+-+-+-+
",
            ["RUNNER42/4"] = @"seed RUNNER42  floor 4  version 23  17 rooms  88 enemies  144 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 shrine, T12 vault, C14 archive, X16 exit hall  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 gate-hall
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
                                      +i       b+
                                      |         |
                                      +    #  # +
                                      |      h  |
                                      +-+B  # +-+
                                        |     |
                                      +-+     +-+
                                      |  %   : k|
                +-+-+-+   +-+-+-+     +       h +
                |C4  !|   |# c  |     |   p     |
    +-+-+-+     +& p  +   +#    + +-+-+      c  +
    |T6k  |     |     |   |   # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    B          +-+-+   +     +
    |     |     |  =            |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |               |
      + +       +  h  +-+=   p [+
      | |       |l   h| |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |   b |
    +     +     +-+-+-+   +-+-+-+
    |   : |
+-+-+  q  +-+-+     +-+-+-+-+-+-+-+
|           c |     |C1w    h     |
+   q   : p   +-+-+ +     h      g+
| p w :        P  | |    #        |
+       e     +-+ +-+       h#    +
|     h    B  | |        p #      |
+-+-+     +-+-+ +-+-+-+           +
    |h    |           |          *|
    +     +           +-+-+B   o A+
    |^~l~~|               |      *|
    +-+-+-+               +b      +
                          |   #   |
                          +  p    +
                          |       |
                          +       +                 +-+-+-+-+-+-+-+-+-+-+
                          |       |                 |X16        :       |
                          + +-+-+-+                 +   ##  +-+   +-+   +
                          |P|                       |    h b| |   | |   |
                          + +     +-+-+-+-+-+-+     +   +-+ +-+   +-+ +-+
                          | |     |C14   b   k|     |   | |           |
                        +-+ +-+   +   :   +-+ + +-+-+   +-+      B  g +-+
                        |T12  |   |     B | | | |P                      |
                        +     +-+-+       +-+ +-+ +-+                   +
                        |      P            g     | |     x         p   |
                        + i$i +-+-+  #    +-+ +-+-+ +  #+-+   c       +-+
                        |     |   |     h | | |     |  c| |          h|
                        +-+-+-+   +   p   +-+ +     +   +-+g+-+  %+-+ +-+
                                  |u        h |     |       | |   | | h |
                                  +    #w h   +     + p     +-+  x+-+   +
                                  |  %        |     | u w               |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 23  17 rooms  96 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
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
                          |       |                 |X16    oYo   |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |w  | | |
                          + +     +-+-+-+-+-+-+     + B +-+   +-+ +-+
                          | |     |C14   b    |     |      p        |
                        +-+ +-+   +%      +-+ +     +               +
                        |T12  |   |     B | | |     |    h    p     |
                        +     +-+-+       +-+ +-+-+ +       G     h +
                        |      P        :   g  P  | |               |
                        + i$i +-+-+  #    +-+ +-+ +-+   :     b     +
                        |     |   |     c | | | |          p   : h  |
                        +-+-+-+   +   p  :+-+ + +-+-+   +-+  g+-+ +-+
                                  |         hi|     |   | |   | | |
                                  +    #w h   +     + ##+-+ # +-+#+
                                  |u         l|     |    c    c   |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
            ["WALKTHRU/4/lancers off"] = @"seed WALKTHRU  floor 4  version 23  17 rooms  84 enemies  116 x 136 m  themes C2 archive, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 stair-head
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p k+
        |        P  | | %           |
        +   @   +-+ +-+    b    B   +
        |       | |          g      |
        +-+    *+ +-+-+-+ #         +
          |     |       |           |
          +-+-+-+       +-+ :    :  +
                          |h       k|
                          +-+  h  hi+
                            |       |
                            +    p  +
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
                    |        :    |
                    + q  q      h +
                    |  h          |
                    +-+  q    +-+-+
                      |       |
                      +-+     +
                        |   b |
                    +-+-+g    +-+-+
                    |             |
                    +             +
                    | p B  :   c  |
                    +             +   +-+-+-+-+-+
                    |  ~~~~       |   |L8 !h    |
                    +-+-+-+-+ +-+-+   +   +-+ p +-+
                            |P|       |B  | |     |
+-+-+-+-+-+-+-+-+-+-+       + +       +  [+-+ x [p+-+
|X16           h    |       | |       |             |
+   +-+-+b  +-+-+h  +     +-+ +-+     +    h #    # +
|   |   |   |   |   |     |T6   |     |         e   |
+ p +   +  x+   +   +     +k    +-+-+-+     b     # +
|   |   |   |   |   |     |      P                 &|
+   +-+-+   +-+-+:  +     +  $  +-+-+-+  =+-+  = g+-+
| p            c    |     |   u |     |   | |w    |
+                   +     +-+-+-+     +   +-+   +-+
|  %       p     :  |                 |   ! h   |
+     #             +                 +-+-+-+ +-+
|  B   c           u|                       |P|
+        #          +                       + +-+
|                   |                       |   |
+                   +                       +-+ +
|x                  |                         | |
+-+-+-+-+-+ +-+-+-+-+                   +-+-+-+ +-+-+-+-+-+
          |P|                           |C10        k ik u|
          + +                           +              h b+
          | |                           |   #             |
          + +-+-+ +-+-+-+     +-+-+-+   +# h             :+
          |C14  | |     |     |T12u |   |    %      B     |
      +-+-+b h  +-+     +-+-+-+ i   +-+-+             p   +
      |         x:  #    P      $    P     c g  p         |
      +i  #             +-+-+-+ i   +-+-+   +-+     +-+   +
      |A o  g      p    |     |     |   |   | |    i| |  u|
      +i   c     h      +     +-+-+-+   +-+-+ +-+-+-+ +-+-+
      |  #    #      #  |
      +-+-+    B+-+     +
          |     | |     |
          +-+-+-+ +-+-+-+
",
            ["WALKTHRU/5/lancers off"] = @"seed WALKTHRU  floor 5  version 23  17 rooms  94 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
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
| # h         # |   | p  *A       |   |L8 !h   &|
+ +-+ h     +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |    p  | | |           |P|       |B  | |     |
+ +-+g    p +-+ +           + +       +  =+-+ x [p+-+
|              c|           | |       |             |
+       G c p   +         +-+ +-+     +    h  #    &+
| B           x |         |T6   |     |   #     e   |
+ +-+b      +-+ +         +i    +-+-+-+ #   b      x+
| | |   h   | | |         |k     P                  |
+ +-+ # #  #+-+ +         +  $  +-+-+-+  [+-+  [ g+-+
|  x         :  |         |  u  |     |   | |w    |
+ #           # +         +-+-+-+     +   +-+   +-+
|  w            |                     |     c   |
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
            ["RUNNER42/4/lancers off"] = @"seed RUNNER42  floor 4  version 23  17 rooms  88 enemies  144 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 shrine, T12 vault, C14 archive, X16 exit hall  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 gate-hall
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
                                      +i       b+
                                      |         |
                                      +    #  # +
                                      |      h  |
                                      +-+B  # +-+
                                        |     |
                                      +-+     +-+
                                      |  %   : k|
                +-+-+-+   +-+-+-+     +       h +
                |C4  !|   |# c  |     |   p     |
    +-+-+-+     +  p  +   +#    + +-+-+      c  +
    |T6k  |     |     |   |   # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    B          +-+-+   +     +
    |     |     |  =            |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |               |
      + +       + [h  +-+=   p  +
      | |       |    h| |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |   b |
    +     +     +-+-+-+   +-+-+-+
    |     |
+-+-+    q+-+-+     +-+-+-+-+-+-+-+
|     : q   c |     |C1w    h     |
+         p   +-+-+ +     h      g+
| p  :      w  P  | |    #        |
+       e     +-+ +-+       h#    +
|     h    B  | |        p #      |
+-+-+     +-+-+ +-+-+-+           +
    |h    |           |          *|
    +     +           +-+-+B   o A+
    | ^~~ |               |      *|
    +-+-+-+               +b      +
                          |   #   |
                          +  p    +
                          |       |
                          +       +                 +-+-+-+-+-+-+-+-+-+-+
                          |       |                 |X16        :       |
                          + +-+-+-+                 +   ##  +-+   +-+   +
                          |P|                       |    h b| |   | |   |
                          + +     +-+-+-+-+-+-+     +   +-+ +-+   +-+ +-+
                          | |     |C14   b   k|     |   | |           |
                        +-+ +-+   +   :   +-+ + +-+-+   +-+      B  g +-+
                        |T12  |   |     B | | | |P                      |
                        +     +-+-+       +-+ +-+ +-+                   +
                        |      P            g     | |     x         p   |
                        + i$i +-+-+  #    +-+ +-+-+ +  #+-+   c       +-+
                        |     |   |     h | | |     |  c| |          h|
                        +-+-+-+   +   p   +-+ +     +   +-+g+-+  %+-+ +-+
                                  |u        h |     |       | |   | | h |
                                  +    #w h   +     + p     +-+  x+-+   +
                                  |  %        |     | u w               |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+-+-+-+
",
            ["RUNNER42/5/lancers off"] = @"seed RUNNER42  floor 5  version 23  17 rooms  96 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
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
                          |       |                 |X16    oYo   |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |w  | | |
                          + +     +-+-+-+-+-+-+     + B +-+   +-+ +-+
                          | |     |C14   b    |     |      p        |
                        +-+ +-+   +%      +-+ +     +               +
                        |T12  |   |     B | | |     |    h    p     |
                        +     +-+-+       +-+ +-+-+ +       G     h +
                        |      P        :   g  P  | |               |
                        + i$i +-+-+  #    +-+ +-+ +-+   :     b     +
                        |     |   |     c | | | |          p   : h  |
                        +-+-+-+   +   p  :+-+ + +-+-+   +-+  g+-+ +-+
                                  |         hi|     |   | |   | | |
                                  +    #w h   +     + ##+-+ # +-+#+
                                  |u          |     |    c    c   |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };

        // ---------------- The validator ----------------

        static (LevelLayout level, LevelRoom room, int index) FindPost(LevelValues v, int floor, RangedPost post)
        {
            for (ulong s = 1; s < 400; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, floor, v);
                foreach (LevelRoom r in l.Rooms)
                    for (int i = 0; i < r.EnemyGroups.Count; i++)
                        if (r.EnemyGroups[i].Ranged?.Post == post) return (l, r, i);
            }
            return (null, null, -1);
        }

        /// <summary>The validator holds the posts to their patterns: a near flank moved to the back, a crossfire on floor 3, two groups at the back, and posts with the switch off are faults.</summary>
        [Test]
        public void TheValidator_HoldsEachPostToItsPattern()
        {
            LevelValues v = LancersOn;
            (LevelLayout level, LevelRoom room, int i) = FindPost(v, 2, RangedPost.NearFlank);
            Assert.That(level, Is.Not.Null, "a floor 2 with a near flank");
            Assert.That(LevelValidator.Validate(level, v), Is.Empty);
            LevelPoint was = room.EnemyPoints[i];
            room.EnemyPoints[i] = new LevelPoint(room.Middle(level.CellSize).X, room.Rect.Top * level.CellSize - 2.0);
            Assert.That(string.Join("; ", LevelValidator.Validate(level, v)), Does.Contain("NearFlank"), "a near flank at the far wall");
            room.EnemyPoints[i] = was;
            Assert.That(string.Join("; ", LevelValidator.Validate(level, Version22)), Does.Contain("work order 124 and its switch is off"), "off, no such posts");

            // Two groups at the back.
            int other = room.EnemyGroups.FindIndex(g => g.Ranged != null && g.Ranged.Post != RangedPost.Far && g.Ranged.Cluster == 0 && g.Ranged.Post != RangedPost.NearFlank);
            if (room.EnemyGroups.Any(g => g.Ranged?.Post == RangedPost.Far) && other >= 0)
            {
                RangedPlace keep = room.EnemyGroups[other].Ranged;
                room.EnemyGroups[other].Ranged = new RangedPlace { Post = RangedPost.Far };
                Assert.That(string.Join("; ", LevelValidator.Validate(level, v)), Does.Contain("ranged groups at the back"));
                room.EnemyGroups[other].Ranged = keep;
            }

            // A crossfire on floor 3.
            (LevelLayout l4, LevelRoom r4, int c) = FindPost(v, 4, RangedPost.Crossfire);
            Assert.That(l4, Is.Not.Null, "a floor 4 with a crossfire");
            Assert.That(LevelValidator.Validate(l4, v), Is.Empty);
            l4.Floor = 3;
            Assert.That(string.Join("; ", LevelValidator.Validate(l4, v)), Does.Contain("before floor 4"));
        }
    }
}
