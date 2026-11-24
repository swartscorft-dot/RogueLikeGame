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
    /// Work order 081 (cleanup and speed): the floors are the same floors. Every plan the rules make for a floor (the
    /// generator's layout, the crypt kit's plan, the cave's rise in both its looks) is folded field for field into one number
    /// over three hundred floors (sixty run seeds, floors 1 to 5, as a run makes their seeds), pinned from the code before the
    /// order's changes (main 6adfc56): a change that makes any field of any of them differ changes the number. Unity's runtime
    /// folds its doubles to its own number (its Math is not .NET's), so each runtime has its own pin, both taken from the code
    /// before. And the rules' part of a floor's build, step by step, printed (nothing asserted about milliseconds: machines differ).
    /// </summary>
    public class FloorPlansStayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly FeelValues Feel = new FeelValues();
        const int SeedsOfRuns = 60;

        static ulong RunSeedOf(int i) => (ulong)i * 0x9E3779B97F4A7C15UL + 81UL;

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        static ulong F(float f) => (ulong)(uint)BitConverter.ToInt32(BitConverter.GetBytes(f), 0);
        static ulong B(bool b) => b ? 1UL : 0UL;
        static ulong I(int i) => (ulong)(long)i;

        /// <summary>The layout, every field the fingerprint tests read and each room's shape, solid cells and template.</summary>
        public static ulong OfLayout(LevelLayout l, ulong h)
        {
            h = FloorPrint.Of(l, h);
            foreach (LevelRoom r in l.Rooms)
            {
                h = Mix(h, (ulong)r.Shape);
                h = Mix(h, (ulong)r.SolidCount);
                foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                foreach (char c in r.Template?.ToString() ?? "-") h = Mix(h, c);
                h = Mix(h, (ulong)r.Letter);
                h = Mix(h, (ulong)r.Props.Count);
                h = Mix(h, (ulong)r.EnemyPoints.Count);
            }
            h = Mix(h, (ulong)l.Version);
            h = Mix(h, (ulong)l.Doors.Count);
            return h;
        }

        public static ulong OfKit(CryptKitPlan k, ulong h)
        {
            foreach (CryptKitSection s in k.Sections)
            {
                h = Mix(h, B(s.Vertical)); h = Mix(h, I(s.Line)); h = Mix(h, I(s.Cell)); h = Mix(h, B(s.Low)); h = Mix(h, B(s.Niche));
                h = Mix(h, I(s.Facing)); h = Mix(h, I(s.RoomMinus)); h = Mix(h, I(s.RoomPlus)); h = Mix(h, B(s.AtDoor));
            }
            foreach (CryptKitArch a in k.Arches) { h = Mix(h, B(a.Vertical)); h = Mix(h, I(a.Line)); h = Mix(h, I(a.Cell)); h = Mix(h, I(a.Door)); }
            foreach (CryptKitPoint p in k.Points) { h = Mix(h, I(p.X)); h = Mix(h, I(p.Y)); h = Mix(h, B(p.Pier)); }
            foreach (CryptKitLantern n in k.Lanterns) { h = Mix(h, I(n.Section)); h = Mix(h, I(n.Side)); h = Mix(h, I(n.Room)); }
            return Mix(h, (ulong)(k.Sections.Count * 7 + k.Arches.Count * 5 + k.Points.Count * 3 + k.Lanterns.Count));
        }

        /// <summary>The rise: every edge's height today and in the cave, every cell's space, rock top and cap, and its counts.</summary>
        public static ulong OfRise(CaveRise r, ulong h)
        {
            h = Mix(h, I(r.X0)); h = Mix(h, I(r.Y0)); h = Mix(h, I(r.Nx)); h = Mix(h, I(r.Ny)); h = Mix(h, I(r.Margin));
            h = Mix(h, F(r.CameraHeight)); h = Mix(h, F(r.CameraBack)); h = Mix(h, F(r.WallTop)); h = Mix(h, F(r.LowTop));
            for (int line = r.X0; line <= r.X0 + r.Nx; line++)
                for (int cell = r.Y0; cell < r.Y0 + r.Ny; cell++) { h = Mix(h, F(r.TodayOf(true, line, cell))); h = Mix(h, F(r.RiseOf(true, line, cell))); }
            for (int line = r.Y0; line <= r.Y0 + r.Ny; line++)
                for (int cell = r.X0; cell < r.X0 + r.Nx; cell++) { h = Mix(h, F(r.TodayOf(false, line, cell))); h = Mix(h, F(r.RiseOf(false, line, cell))); }
            for (int y = r.Y0; y < r.Y0 + r.Ny; y++)
                for (int x = r.X0; x < r.X0 + r.Nx; x++) { h = Mix(h, I(r.SpaceAt(x, y))); h = Mix(h, F(r.LidOf(x, y))); h = Mix(h, B(r.IsCapped(x, y))); }
            h = Mix(h, (ulong)r.Rays);
            h = Mix(h, (ulong)r.Lowered);
            return h;
        }

        /// <summary>
        /// The three hundred floors' plans, folded: (layout and kit, the rock look's rise, the dark look's rise). Work order 087: with
        /// the act's plan off (LevelValues.ActPlan), which is version 17's floors, field for field, but for the version: the version
        /// is folded as the 17 it was when the pins were taken.
        /// </summary>
        static (ulong layout, ulong rock, ulong dark) Prints()
        {
            var v = new LevelValues { ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 103: nor the exit rooms of their own
            ulong hl = 14695981039346656037UL, hr = hl, hd = hl;
            for (int i = 0; i < SeedsOfRuns; i++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeedOf(i), floor), floor, v);
                    Assert.That(l.Version, Is.EqualTo(LevelGenerator.Version)); // work order 088: version 17's floor with the act's plan and the rooms you can use off; 103: and the exit rooms of their own (the version is not folded)
                    l.Version = 17; // work order 087: version 17's floor but for the version
                    CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                    hl = OfKit(kit, OfLayout(l, hl));
                    hr = OfRise(CaveRise.Make(l, kit, v.CaveRules, v.WallHeight, v.NearWallHeight, Feel.CameraPitch, Feel.CameraDistance), hr);
                    hd = OfRise(CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance), hd);
                }
            return (hl, hr, hd);
        }

        static string Hex(ulong h) => "0x" + h.ToString("X16") + "UL";

        // Pinned from the code before the order's changes (main 6adfc56), each in its runtime. With a pin at 0 the test fails and
        // says what it found.
#if UNITY_5_3_OR_NEWER
        const ulong PinLayout = 0x41D0091227C66DAEUL, PinRock = 0x884ECAFF2CCC551FUL, PinDark = 0x7349308A15E0BC76UL; // Unity's EditMode run, 6adfc56
#else
        const ulong PinLayout = 0x16D25A63977E65A1UL, PinRock = 0x884ECAFF2CCC551FUL, PinDark = 0x7349308A15E0BC76UL; // .NET, 6adfc56
#endif

        [Test]
        public void ThreeHundredFloors_TheirLayoutsKitPlansAndCaveRises_AreTheFloorsOfBefore_FieldForField()
        {
            (ulong layout, ulong rock, ulong dark) = Prints();
            string found = "layout and kit " + Hex(layout) + ", rock " + Hex(rock) + ", dark " + Hex(dark);
            TestContext.WriteLine("FLOORPLANSPRINT " + found);
            if (PinLayout == 0UL || PinRock == 0UL || PinDark == 0UL) Assert.Fail("the pins are to be taken from the code before: this run found " + found);
            Assert.That(layout, Is.EqualTo(PinLayout), "the layouts and the kit's plans of the three hundred floors, found " + found);
            Assert.That(rock, Is.EqualTo(PinRock), "the rock look's rise of the three hundred floors, found " + found);
            Assert.That(dark, Is.EqualTo(PinDark), "the dark look's rise of the three hundred floors, found " + found);
        }

        /// <summary>
        /// The cave's sight rule on several threads (work order 081, <see cref="CaveRise.Threads"/>) against the rule on one thread,
        /// the code as it was, on the same floors in this runtime: the same rise, rays and lowered rays, field for field, in both
        /// looks (the rock look's tops keep it on one thread) and with each guard of the dark look.
        /// </summary>
        [Test]
        public void TheSightRule_OnSeveralThreads_IsTheRuleOnOneThread_FieldForField()
        {
            var v = new LevelValues();
            bool was = CaveRise.Threads;
            try
            {
                int floors = 0;
                long lowered = 0;
                for (int i = 0; i < 12; i++)
                    for (int floor = 1; floor <= 5; floor += 2)
                    {
                        LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeedOf(i + 500), floor), floor, v);
                        CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                        var lookValues = new List<CaveValues> { v.CaveRules };
                        lookValues.Add(new CaveValues { GuardHerAndDoors = false });                     // 076's guards, no tops
                        lookValues.Add(new CaveValues { RockLids = false, GuardNeighbours = true });     // round 2's rule
                        foreach (CaveValues cv in lookValues)
                        {
                            CaveRise.Threads = false;
                            CaveRise oneDark = CaveRise.MakeDark(l, kit, cv, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance);
                            CaveRise oneRock = CaveRise.Make(l, kit, cv, v.WallHeight, v.NearWallHeight, Feel.CameraPitch, Feel.CameraDistance);
                            CaveRise.Threads = true;
                            CaveRise manyDark = CaveRise.MakeDark(l, kit, cv, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance);
                            CaveRise manyRock = CaveRise.Make(l, kit, cv, v.WallHeight, v.NearWallHeight, Feel.CameraPitch, Feel.CameraDistance);
                            Assert.That(manyDark.Rays, Is.EqualTo(oneDark.Rays), "the rays, seed " + l.SeedText);
                            Assert.That(manyDark.Lowered, Is.EqualTo(oneDark.Lowered), "the lowered rays, seed " + l.SeedText);
                            Assert.That(OfRise(manyDark, 1UL), Is.EqualTo(OfRise(oneDark, 1UL)), "the dark look's rise, seed " + l.SeedText);
                            Assert.That(OfRise(manyRock, 1UL), Is.EqualTo(OfRise(oneRock, 1UL)), "the rock look's rise, seed " + l.SeedText);
                            lowered += oneDark.Lowered + oneRock.Lowered;
                        }
                        floors++;
                    }
                Assert.That(floors, Is.EqualTo(36));
                Assert.That(lowered, Is.GreaterThan(0), "the rule lowered rises: the comparison saw it work");
            }
            finally { CaveRise.Threads = was; }
        }

        [Test]
        public void TheFold_SeesAChange_OfOneEdgeOrOneRay()
        {
            var v = new LevelValues();
            LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeedOf(1), 2), 2, v);
            CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
            CaveRise a = CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance);
            CaveRise b = CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance);
            Assert.That(OfRise(b, 1UL), Is.EqualTo(OfRise(a, 1UL)), "the same plan folds to the same number");
            b.Rays++;
            Assert.That(OfRise(b, 1UL), Is.Not.EqualTo(OfRise(a, 1UL)), "one ray more is seen");
            CaveRise c = CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight + 0.01f, Feel.CameraPitch, Feel.CameraDistance);
            Assert.That(OfRise(c, 1UL), Is.Not.EqualTo(OfRise(a, 1UL)), "another wall top is seen");
            CryptKitPlan k2 = CryptKitPlan.Make(l, v.Crypt);
            k2.Sections[0].Niche = !k2.Sections[0].Niche;
            Assert.That(OfKit(k2, 1UL), Is.Not.EqualTo(OfKit(kit, 1UL)), "one niche is seen");
        }

        /// <summary>
        /// The rules' part of a floor's build, step by step, for floors 1 to 5 of five run seeds: the generator, the kit's plan, the
        /// cave's rise (the dark look, the game's), the mood's plan. The middle of three runs of each, after one to warm up.
        /// </summary>
        [Test]
        public void TheRulesOfAFloorsBuild_StepByStep_Table()
        {
            var v = new LevelValues();
            var lines = new List<string> { "BUILDRULES  floor  generator  kit plan  cave rise (dark)  the same on one thread  mood plan  (ms, the middle of 3; mean over 5 run seeds)" };
            double Middle(Func<object> make)
            {
                make();
                var t = new List<double>();
                for (int k = 0; k < 3; k++)
                {
                    var w = Stopwatch.StartNew();
                    make();
                    t.Add(w.Elapsed.TotalMilliseconds);
                }
                t.Sort();
                return t[1];
            }
            for (int floor = 1; floor <= 5; floor++)
            {
                double gen = 0, kitMs = 0, cave = 0, caveOne = 0, mood = 0;
                long rays = 0;
                for (int i = 0; i < 5; i++)
                {
                    ulong seed = RunSeeds.FloorSeed(RunSeedOf(i), floor);
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                    gen += Middle(() => LevelGenerator.Generate(seed, floor, v));
                    kitMs += Middle(() => CryptKitPlan.Make(l, v.Crypt));
                    cave += Middle(() => CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance));
                    bool was = CaveRise.Threads;
                    CaveRise.Threads = false;
                    try { caveOne += Middle(() => CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance)); }
                    finally { CaveRise.Threads = was; }
                    mood += Middle(() => RoomMoodPlan.Make(l, v));
                    rays += CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance).Rays;
                }
                lines.Add(string.Format(Inv, "BUILDRULES  {0}  {1,8:0.0}  {2,8:0.0}  {3,8:0.0}  {4,8:0.0}  {5,8:0.0}  (cave rays {6})", floor, gen / 5, kitMs / 5, cave / 5, caveOne / 5, mood / 5, rays / 5));
            }
            TestContext.WriteLine(string.Join("\n", lines));
            Assert.That(lines.Count, Is.EqualTo(6));
        }
    }
}
