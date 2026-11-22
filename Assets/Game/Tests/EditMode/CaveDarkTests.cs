using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// The cave, second attempt (work order 083), its rules: which wall sections are far and which near for the game's camera
    /// (<see cref="CaveWalls"/>); how high the dark look's walls stand (<see cref="CaveRise.MakeDark"/>: the far ones tall, the
    /// near ones a lip); that no tall wall ever stands between the camera and her, her room's floor or its doors' openings
    /// where the look's other walls leave them seen, in many rooms and in the templates of work order 072; the look's switch;
    /// and the camera distance's steps, the default 28 m.
    /// </summary>
    public class CaveDarkTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly FeelValues Feel = new FeelValues();

        static CaveRise Dark(LevelLayout l, LevelValues v, CryptKitPlan kit = null, float distance = -1f) =>
            CaveRise.MakeDark(l, kit ?? CryptKitPlan.Make(l, v.Crypt), v.CaveRules, v.WallHeight, Feel.CameraPitch, distance > 0f ? distance : Feel.CameraDistance);

        [Test]
        public void FarAndNear_ForTheGamesCamera_TheNorthAndSideWallsFar_TheSouthWallsNear_AsTheKitsLowWalls()
        {
            // A face toward the room on its north side (+1) of a horizontal line is that room's south wall: between the camera and it.
            Assert.That(CaveWalls.IsNearFace(false, 1, 0f), Is.True, "a room's south wall is near");
            Assert.That(CaveWalls.IsNearFace(false, -1, 0f), Is.False, "a room's north wall is far");
            Assert.That(CaveWalls.IsNearFace(true, 1, 0f), Is.False, "a room's west wall is far");
            Assert.That(CaveWalls.IsNearFace(true, -1, 0f), Is.False, "a room's east wall is far");
            // The rule turns with the camera: looking south, the north walls are the near ones; looking east, the west walls.
            Assert.That(CaveWalls.IsNearFace(false, -1, 180f), Is.True);
            Assert.That(CaveWalls.IsNearFace(false, 1, 180f), Is.False);
            Assert.That(CaveWalls.IsNearFace(true, 1, 90f), Is.True, "looking east, a room's west wall is near");
            Assert.That(CaveWalls.GameYaw, Is.EqualTo(0f), "the game's camera looks north (CameraRig turns it by its pitch only)");

            var v = new LevelValues();
            int far = 0, near = 0;
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL, 99UL })
                foreach (int floor in new[] { 1, 3, 5 })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    foreach (CryptKitSection s in CryptKitPlan.Make(l, v.Crypt).Sections)
                    {
                        bool isNear = CaveWalls.IsNear(s, CaveWalls.GameYaw);
                        Assert.That(isNear, Is.EqualTo(s.Low), "seed " + seed + " floor " + floor + ": " + (s.Vertical ? "V" : "H") + s.Line + "_" + s.Cell);
                        Assert.That(CaveWalls.IsFar(s, CaveWalls.GameYaw), Is.EqualTo(!isNear));
                        if (isNear) near++;
                        else far++;
                    }
                }
            Assert.That(far, Is.GreaterThan(near), "most of a floor's walls are far");
            TestContext.WriteLine(string.Format(Inv, "CAVEWALLS 4 seeds x 3 floors: {0} far sections, {1} near", far, near));
        }

        [Test]
        public void TheHeights_FarWallsRiseToTheFarHeight_HeldOnlyAtTheKits_NearWallsAreTheLip_ByTheValues()
        {
            var lines = new List<string>();
            foreach (float times in new[] { 3f, 2.5f, 4f })
            {
                var v = new LevelValues();
                v.CaveRules.FarHeight = v.CaveRules.HerHeight * times;
                int far = 0, raised = 0, held = 0, heldCarved = 0, heldPassage = 0, outline = 0, outlineRaised = 0;
                var watch = Stopwatch.StartNew();
                foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, 1, v);
                    CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                    CaveRise rise = Dark(l, v, kit);
                    Assert.That(rise.Rules.RockLids, Is.False, "no rock tops in the dark look");
                    Assert.That(rise.LidOf(l.Rooms[0].Rect.X - 1, l.Rooms[0].Rect.Y - 1), Is.EqualTo(0f));
                    foreach (CryptKitSection s in kit.Sections)
                    {
                        float h = rise.RiseOf(s.Vertical, s.Line, s.Cell);
                        if (s.Low)
                        {
                            Assert.That(h, Is.EqualTo(Math.Min(v.WallHeight, v.CaveRules.LipHeight)).Within(1e-5), "a near wall is the lip");
                            Assert.That(rise.TodayOf(s.Vertical, s.Line, s.Cell), Is.EqualTo(h).Within(1e-5));
                            continue;
                        }
                        far++;
                        bool carved = s.Vertical ? l.IsSolidCell(s.Line - 1, s.Cell) || l.IsSolidCell(s.Line, s.Cell) : l.IsSolidCell(s.Cell, s.Line - 1) || l.IsSolidCell(s.Cell, s.Line);
                        bool passage = (s.RoomMinus >= 0 && l.Rooms[s.RoomMinus].Kind == RoomKind.Passage) || (s.RoomPlus >= 0 && l.Rooms[s.RoomPlus].Kind == RoomKind.Passage);
                        bool tall = h >= v.CaveRules.FarHeight - 1e-4f;
                        if (!carved && !passage) { outline++; if (tall) outlineRaised++; }
                        if (tall) raised++;
                        else
                        {
                            Assert.That(h, Is.EqualTo(v.WallHeight).Within(1e-5), "a far wall the rule holds stays at the kit's height, never cut between");
                            held++;
                            if (carved) heldCarved++;
                            else if (passage) heldPassage++;
                        }
                    }
                    // No rise over a door: the kit has no section there and the rule none.
                    foreach (LevelDoor d in l.Doors)
                        for (int c = d.From; c < d.From + d.Width; c++)
                            Assert.That(rise.RiseOf(d.Vertical, d.Line, c), Is.EqualTo(0f), "a door's opening");
                }
                Assert.That(outlineRaised, Is.GreaterThan(outline * 0.75), times + " times her height: most far walls of the rooms' outlines stand tall");
                lines.Add(string.Format(Inv, "{0} times her height ({1:0.00} m): {2} far sections, {3} tall ({4:0}%), {5} held at the kit's height ({6} beside a carved cell, {7} of a passage); of the rooms' outlines {8} tall of {9}; three floors' rules {10:0} ms",
                                        times, v.CaveRules.FarHeight, far, raised, 100.0 * raised / far, held, heldCarved, heldPassage, outlineRaised, outline, watch.Elapsed.TotalMilliseconds));
            }
            foreach (string line in lines) TestContext.WriteLine("CAVEHEIGHTS " + line);
        }

        /// <summary>Her places in a space: a grid over every open cell (three a side), and the cells at its doors' mouths.</summary>
        static List<LevelPoint> Places(CaveRise rise, LevelRoom r) => rise.Points(r.Order, new[] { 0.6, 2.0, 3.4 });

        [Test]
        public void NoTallWall_EverHidesHer_HerRoomsFloor_OrItsDoors_ManyRooms_AndTheTemplates()
        {
            double[] fine = { 0.25, 0.75, 1.25, 1.75, 2.25, 2.75, 3.25, 3.75 };
            long asked = 0, rays = 0, heads = 0;
            int rooms = 0, templates = 0, moreTemplates = 0;
            var watch = Stopwatch.StartNew();
            var faults = new List<string>();
            // The floors as the default values make them (work order 072's second set off in the floors since 2026-11-22), and
            // with the second set on, so the rule stays asked on its rooms' outlines too (they are built in the room lab).
            foreach (LevelValues v in new[] { new LevelValues(), new LevelValues { MoreTemplates = true } })
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL, 5UL })
                foreach (int floor in new[] { 1, 4 })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    CaveRise rise = Dark(l, v);
                    rays += rise.Rays;
                    var shown = new bool[l.Rooms.Count];
                    foreach (LevelRoom r in l.Rooms)
                    {
                        rooms++;
                        if (r.Template != null)
                        {
                            templates++;
                            if (r.Template.Template.Since >= RoomTemplateLibrary.MoreSince) moreTemplates++;
                        }
                        CaveSight.Shown(l, r.Order, shown);
                        // Her space's floor and its doors' openings (no cell past a door: the tall walls may cover what is beyond).
                        List<LevelPoint> targets = rise.Guarded(r.Order, shown, fine);
                        Assert.That(targets.Count, Is.EqualTo(rise.Points(r.Order, fine).Count + 5 * l.Doors.Count(d => d.Joins(r.Order))), "her floor and each door's opening");
                        foreach (LevelPoint her in Places(rise, r))
                        {
                            Assert.That(l.RoomAt(her.X, her.Y), Is.SameAs(r), "her place is in the room");
                            // Her feet and her head.
                            heads++;
                            if (rise.NewlyHidden(her, her, shown) || rise.NewlyHidden(her, her, shown, v.CaveRules.HerHeight))
                                if (faults.Count < 12) faults.Add(string.Format(Inv, "seed {0} floor {1} {2}{3}: her at ({4:0.##}, {5:0.##}) is behind a tall wall", seed, floor, r.Letter, r.Order, her.X, her.Y));
                            foreach (LevelPoint t in targets)
                            {
                                if (!CaveRise.InView(her, t)) continue;
                                asked++;
                                if (rise.NewlyHidden(her, t, shown) && faults.Count < 12)
                                    faults.Add(string.Format(Inv, "seed {0} floor {1} {2}{3}: her at ({4:0.##}, {5:0.##}), a tall wall hides ({6:0.##}, {7:0.##}): {8}", seed, floor, r.Letter, r.Order, her.X, her.Y, t.X, t.Y, rise.Explain(her, t)));
                            }
                        }
                    }
                }
            Assert.That(faults, Is.Empty, "the dark look's tall walls hide what its other walls leave seen");
            Assert.That(templates, Is.GreaterThan(0), "rooms drawn by templates were asked about");
            Assert.That(moreTemplates, Is.GreaterThan(0), "and some of work order 072's");
            TestContext.WriteLine(string.Format(Inv, "CAVEDARKSIGHT 4 seeds x floors 1 and 4, the second set off and on: {0} rooms and passages ({1} by templates, {2} of 072's), {3} lines to the floor and the doors and {4} places' feet and head asked, none hidden; the rule asked {5} rays, {6:0} ms in all",
                                                rooms, templates, moreTemplates, asked, heads, rays, watch.Elapsed.TotalMilliseconds));
        }

        [Test]
        public void AtTheClosestCameraStep_StillNothingOfHerRoomHidden_WhenTheFloorIsMadeForIt()
        {
            var v = new LevelValues();
            double[] fine = { 0.5, 1.5, 2.5, 3.5 };
            float distance = CameraDistanceSteps.Distance(CameraDistanceSteps.Count - 1, Feel.CameraDistance);
            Assert.That(distance, Is.EqualTo(22f));
            int faults = 0, tall = 0;
            foreach (ulong seed in new[] { 42UL, 7UL })
            {
                LevelLayout l = LevelGenerator.Generate(seed, 1, v);
                CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                // The rule's camera: the distance in use, as the level passes it.
                CaveRise rise = Dark(l, v, kit, distance);
                Assert.That(rise.CameraHeight, Is.EqualTo(distance * Math.Sin(Feel.CameraPitch * Math.PI / 180.0)).Within(1e-3));
                tall += kit.Sections.Count(s => !s.Low && rise.RiseOf(s.Vertical, s.Line, s.Cell) >= v.CaveRules.FarHeight - 1e-4f);
                var shown = new bool[l.Rooms.Count];
                foreach (LevelRoom r in l.Rooms)
                {
                    CaveSight.Shown(l, r.Order, shown);
                    List<LevelPoint> targets = rise.Guarded(r.Order, shown, fine);
                    foreach (LevelPoint her in Places(rise, r))
                    {
                        if (rise.NewlyHidden(her, her, shown, v.CaveRules.HerHeight)) faults++;
                        foreach (LevelPoint t in targets)
                            if (CaveRise.InView(her, t) && rise.NewlyHidden(her, t, shown)) faults++;
                    }
                }
            }
            Assert.That(faults, Is.Zero);
            Assert.That(tall, Is.GreaterThan(0));
        }

        [Test]
        public void TheLook_ThreeWays_DarkByDefault_AndTheGeneratorReadsNoneOfIt()
        {
            var v = new LevelValues();
            Assert.That(v.Cave, Is.True, "the cave's master switch");
            Assert.That(v.CaveRules.Look, Is.EqualTo(CaveLook.Dark), "work order 083's look by default");
            Assert.That(Enum.GetValues(typeof(CaveLook)).Cast<CaveLook>(), Is.EqualTo(new[] { CaveLook.Before, CaveLook.Rock, CaveLook.Dark }));
            // 076's rock keeps its own switches and numbers, for the Rock look.
            Assert.That(v.CaveRules.TallWalls && v.CaveRules.RockLids, Is.True);
            Assert.That(v.CaveRules.RiseHeight, Is.EqualTo(9f));
            Assert.That(v.CaveRules.GuardDoorCells, Is.True, "076's rule as it was");
            Assert.That(v.CaveRules.GuardHerAndDoors, Is.False);
            // The dark look's values: the order's first guesses.
            Assert.That(v.CaveRules.FarHeight, Is.EqualTo(3f * v.CaveRules.HerHeight).Within(1e-4));
            Assert.That(v.CaveRules.LipHeight, Is.EqualTo(0.25f * v.CaveRules.HerHeight).Within(1e-4));
            CaveValues d = CaveRise.DarkRules(v.CaveRules);
            Assert.That(d.RiseHeight, Is.EqualTo(v.CaveRules.FarHeight));
            Assert.That(d.RockLids || d.GuardDoorCells || d.GuardNeighbours, Is.False);
            Assert.That(d.GuardHerAndDoors, Is.True);

            // The generator never reads it: the same floors in every look.
            foreach (ulong seed in new[] { 42UL, 7UL })
            {
                string Map(CaveLook look, bool cave)
                {
                    var values = new LevelValues { Cave = cave };
                    values.CaveRules.Look = look;
                    LevelLayout l = LevelGenerator.Generate(seed, 2, values);
                    return l.Version + "\n" + LevelMap.Draw(l) + "\n" + CryptKitPlan.Make(l, values.Crypt).Describe();
                }
                string dark = Map(CaveLook.Dark, true);
                Assert.That(Map(CaveLook.Rock, true), Is.EqualTo(dark));
                Assert.That(Map(CaveLook.Before, true), Is.EqualTo(dark));
                Assert.That(Map(CaveLook.Dark, false), Is.EqualTo(dark));
            }
        }

        [Test]
        public void TheCameraDistance_Steps28_25_22_AndBack_TheDefault28()
        {
            Assert.That(new FeelValues().CameraDistance, Is.EqualTo(28f), "the default stays (2026-11-19: 14 was too close, 28 \"much better\")");
            Assert.That(CameraDistanceSteps.Count, Is.EqualTo(3));
            Assert.That(CameraDistanceSteps.Distance(0, 28f), Is.EqualTo(28f), "step 0 is the feel's own distance");
            Assert.That(CameraDistanceSteps.Distance(0, 30f), Is.EqualTo(30f), "whatever the feel's asset says");
            Assert.That(CameraDistanceSteps.Distance(1, 28f), Is.EqualTo(25f));
            Assert.That(CameraDistanceSteps.Distance(2, 28f), Is.EqualTo(22f));
            int step = 0;
            var seen = new List<float>();
            for (int i = 0; i < 4; i++)
            {
                seen.Add(CameraDistanceSteps.Distance(step, 28f));
                step = CameraDistanceSteps.Next(step);
            }
            Assert.That(seen, Is.EqualTo(new[] { 28f, 25f, 22f, 28f }), "28, 25, 22 and back");
            Assert.That(CameraDistanceSteps.Previous(0), Is.EqualTo(2));
            Assert.That(CameraDistanceSteps.Previous(2), Is.EqualTo(1));
            Assert.That(CameraDistanceSteps.Step(-1), Is.EqualTo(0), "nothing stored: the default");
            Assert.That(CameraDistanceSteps.Step(7), Is.EqualTo(0), "a stored value that is no step: the default");
            Assert.That(CameraDistanceSteps.Next(7), Is.EqualTo(1));
        }
    }
}
