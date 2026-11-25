using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// Work order 121's camera angles, their rules: the four and their numbers; "Ours" today's camera field for field; the
    /// stepping as the menu's other lines step; the distance's steps on top of an angle; the switch; the projection the numbers
    /// were chosen by (her height on the screen, the ground seen), checked against a camera worked out point by point; and what
    /// the floor's camera-made parts, built for ours, do at each other angle (measured and written, "CAMERAANGLES ...", for
    /// the report: the order mends nothing of them).
    /// </summary>
    public class CameraAnglesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly FeelValues Feel = new FeelValues();
        const float HerHeight = 1.8f;  // m, CaveValues.HerHeight
        const float Wide = 16f / 9f;   // his screen's shape

        [Test]
        public void TheFour_InTheOrdersOrder_OursByDefault_OursIsTheFeelsOwnCamera()
        {
            Assert.That(CameraAngles.Count, Is.EqualTo(4));
            Assert.That(CameraAngles.Names, Is.EqualTo(new[] { "Ours", "Diablo", "League", "PoE" }), "the order's line: Ours / Diablo / League / PoE");
            Assert.That(CameraAngles.Presets.Select(p => p.Name), Is.EqualTo(CameraAngles.Names));
            Assert.That(CameraAngles.Default, Is.EqualTo(CameraAngles.Ours), "the default angle is not this order's to change");
            Assert.That(CameraAngles.On, Is.True, "the order's switch on by default");
            CameraAngle ours = CameraAngles.Presets[CameraAngles.Ours];
            Assert.That(ours.Pitch, Is.EqualTo(Feel.CameraPitch), "ours: today's 55 degrees");
            Assert.That(ours.FieldOfView, Is.EqualTo(Feel.FieldOfView), "30");
            Assert.That(ours.Distance, Is.EqualTo(Feel.CameraDistance), "28 m");
            Assert.That((Feel.CameraPitch, Feel.FieldOfView, Feel.CameraDistance), Is.EqualTo((55f, 30f, 28f)), "the feel's camera itself untouched");
        }

        [Test]
        public void TheThreeGames_TheirNumbers_AndHerHeightOnTheScreen_AsTheSourcesMeasureThatGamesCharacter()
        {
            // The numbers (sources beside them in CameraAngles and in the order's report).
            CameraAngle d = CameraAngles.Presets[CameraAngles.Diablo], l = CameraAngles.Presets[CameraAngles.League], p = CameraAngles.Presets[CameraAngles.PathOfExile];
            Assert.That((d.Pitch, d.FieldOfView, d.Distance), Is.EqualTo((47f, 35f, 15.6f)), "Diablo IV, Far (the PC default)");
            Assert.That((l.Pitch, l.FieldOfView, l.Distance), Is.EqualTo((56f, 40f, 15.3f)), "League, the locked camera at the furthest zoom");
            Assert.That((p.Pitch, p.FieldOfView, p.Distance), Is.EqualTo((53f, 44f, 12.6f)), "Path of Exile");
            // Diablo IV's pitch from its camera file's offset (9.56, 9.56, 14.5), z up.
            Assert.That(Math.Atan2(14.5, 9.56 * Math.Sqrt(2)) * 180 / Math.PI, Is.EqualTo(47.0).Within(0.05));
            Assert.That(0.6109 * 180 / Math.PI, Is.EqualTo(35.0).Within(0.01), "its aFOV in radians");
            // Her height on the screen: the share each game's character has at its default (measured from the pictures named).
            Assert.That(CameraAngles.HeightShare(d.Pitch, d.FieldOfView, d.Distance, HerHeight), Is.EqualTo(0.136).Within(0.001), "Diablo IV's at Far: 13.6 %");
            Assert.That(CameraAngles.HeightShare(l.Pitch, l.FieldOfView, l.Distance, HerHeight), Is.EqualTo(0.100).Within(0.001), "a champion of a human's size: 10 %");
            Assert.That(CameraAngles.HeightShare(p.Pitch, p.FieldOfView, p.Distance, HerHeight), Is.EqualTo(0.120).Within(0.001), "Path of Exile's: 12 %");
            // League's own camera, 2250 units at 56 / 40: its view box on the minimap (4135 by 2290 units measured) is about what it sees.
            Assert.That(CameraAngles.GroundAhead(56f, 40f, 2250f) + CameraAngles.GroundBehind(56f, 40f, 2250f), Is.EqualTo(2100).Within(60), "its depth seen: about 2100 units, the box's 2290 measured roughly");
            // Each of the three nearer than ours, she larger on the screen: every one of the three games shows her bigger than ours does.
            double ours = CameraAngles.HeightShare(55f, 30f, 28f, HerHeight);
            foreach (CameraAngle a in new[] { d, l, p }) Assert.That(CameraAngles.HeightShare(a.Pitch, a.FieldOfView, a.Distance, HerHeight), Is.GreaterThan(ours * 1.3), a.Name);
        }

        [Test]
        public void Stepping_OnAndBack_RoundTheFour_AndAnythingElseStoredIsOurs()
        {
            int s = CameraAngles.Ours;
            var on = new List<int>();
            for (int i = 0; i < 5; i++) on.Add(s = CameraAngles.Next(s));
            Assert.That(on, Is.EqualTo(new[] { 1, 2, 3, 0, 1 }));
            s = CameraAngles.Ours;
            var back = new List<int>();
            for (int i = 0; i < 5; i++) back.Add(s = CameraAngles.Previous(s));
            Assert.That(back, Is.EqualTo(new[] { 3, 2, 1, 0, 3 }));
            foreach (int bad in new[] { -1, 4, 99, int.MinValue }) Assert.That(CameraAngles.Step(bad), Is.EqualTo(CameraAngles.Ours), "stored " + bad);
            for (int i = 0; i < CameraAngles.Count; i++) Assert.That(CameraAngles.Step(i), Is.EqualTo(i));
        }

        [Test]
        public void Ours_IsTheFeelsOwnCamera_FieldForField_AtEveryDistanceStep_WhateverTheFeelHolds()
        {
            foreach ((float pitch, float fov, float distance) feel in new[] { (55f, 30f, 28f), (48.5f, 35f, 31f), (60f, 25f, 14f) })
                for (int step = -1; step <= CameraDistanceSteps.Count; step++)
                {
                    CameraAngle a = CameraAngles.InUse(CameraAngles.Ours, step, feel.pitch, feel.fov, feel.distance);
                    Assert.That(a.Pitch, Is.EqualTo(feel.pitch));
                    Assert.That(a.FieldOfView, Is.EqualTo(feel.fov));
                    Assert.That(a.Distance, Is.EqualTo(CameraDistanceSteps.Distance(step, feel.distance)), "the distance step's own distance, as before");
                    Assert.That(a.Name, Is.EqualTo("Ours"));
                }
            Assert.That(CameraAngles.InUse(CameraAngles.Ours, 1, 55f, 30f, 28f).Distance, Is.EqualTo(25f));
            Assert.That(CameraAngles.InUse(CameraAngles.Ours, 2, 55f, 30f, 28f).Distance, Is.EqualTo(22f));
        }

        [Test]
        public void AnotherAngle_ItsOwnNumbers_AndTheDistanceStepsScaleItsOwnDistance_AsTheyScaleTheFeels()
        {
            for (int a = 1; a < CameraAngles.Count; a++)
            {
                CameraAngle p = CameraAngles.Presets[a];
                CameraAngle at0 = CameraAngles.InUse(a, 0, 55f, 30f, 28f);
                Assert.That((at0.Name, at0.Pitch, at0.FieldOfView, at0.Distance), Is.EqualTo((p.Name, p.Pitch, p.FieldOfView, p.Distance)), p.Name + " at the distance's step 0");
                Assert.That(CameraAngles.InUse(a, 0, 40f, 50f, 10f).Distance, Is.EqualTo(p.Distance), "not the feel's numbers");
                Assert.That(CameraAngles.InUse(a, 1, 55f, 30f, 28f).Distance, Is.EqualTo(p.Distance * 25f / 28f).Within(1e-4f), p.Name + " at the first closer step");
                Assert.That(CameraAngles.InUse(a, 2, 55f, 30f, 28f).Distance, Is.EqualTo(p.Distance * 22f / 28f).Within(1e-4f), p.Name + " at the second");
                Assert.That(CameraAngles.InUse(a, 1, 55f, 30f, 28f).Pitch, Is.EqualTo(p.Pitch), "a step moves it in, it does not tilt it");
                Assert.That(CameraAngles.InUse(a, 1, 55f, 30f, 28f).FieldOfView, Is.EqualTo(p.FieldOfView));
            }
        }

        [Test]
        public void WithTheSwitchOff_EveryStoredAngleIsOurs()
        {
            try
            {
                CameraAngles.On = false;
                for (int a = 0; a < CameraAngles.Count; a++)
                {
                    CameraAngle x = CameraAngles.InUse(a, 1, 55f, 30f, 28f);
                    Assert.That((x.Name, x.Pitch, x.FieldOfView, x.Distance), Is.EqualTo(("Ours", 55f, 30f, 25f)));
                }
            }
            finally { CameraAngles.On = true; }
        }

        // ---------------- The projection the numbers were chosen by ----------------

        /// <summary>A point's place on the screen through a camera at this angle looking at the origin, worked out step by step: (x, y) from -1 to 1.</summary>
        static (double x, double y) Project(CameraAngle a, float aspect, double px, double py, double pz)
        {
            double p = a.Pitch * Math.PI / 180.0, t = Math.Tan(a.FieldOfView * Math.PI / 360.0);
            // The camera's place and axes: looking north and down by the pitch.
            double cy = a.Distance * Math.Sin(p), cz = -a.Distance * Math.Cos(p);
            double fy = -Math.Sin(p), fz = Math.Cos(p);   // forward
            double uy = Math.Cos(p), uz = Math.Sin(p);    // up
            double dx = px, dy = py - cy, dz = pz - cz;
            double z = dy * fy + dz * fz, y = dy * uy + dz * uz, x = dx;
            return (x / z / (t * aspect), y / z / t);
        }

        [Test]
        public void TheProjection_HerHeightAndTheGroundSeen_AsACameraWorkedOutPointByPointSeesThem()
        {
            foreach (CameraAngle a in CameraAngles.Presets)
            {
                (double x, double y) feet = Project(a, Wide, 0, 0, 0), head = Project(a, Wide, 0, HerHeight, 0);
                Assert.That(feet.y, Is.EqualTo(0).Within(1e-9), a.Name + ": her feet in the middle");
                Assert.That((head.y - feet.y) / 2.0, Is.EqualTo(CameraAngles.HeightShare(a.Pitch, a.FieldOfView, a.Distance, HerHeight)).Within(1e-9), a.Name + ": her height's share");
                double halfWidth = CameraAngles.GroundWidthAtHer(a.FieldOfView, a.Distance, Wide) / 2.0;
                Assert.That(Project(a, Wide, halfWidth, 0, 0).x, Is.EqualTo(1).Within(1e-9), a.Name + ": the ground's width at her feet reaches the screen's side");
                Assert.That(Project(a, Wide, 0, 0, -CameraAngles.GroundBehind(a.Pitch, a.FieldOfView, a.Distance)).y, Is.EqualTo(-1).Within(1e-9), a.Name + ": the ground behind her reaches the bottom edge");
                Assert.That(Project(a, Wide, 0, 0, CameraAngles.GroundAhead(a.Pitch, a.FieldOfView, a.Distance)).y, Is.EqualTo(1).Within(1e-9), a.Name + ": the ground ahead reaches the top edge");
                double share = CameraAngles.HeightShare(a.Pitch, a.FieldOfView, a.Distance, HerHeight);
                Assert.That(CameraAngles.DistanceForShare(a.Pitch, a.FieldOfView, HerHeight, share), Is.EqualTo(a.Distance).Within(1e-3), a.Name + ": the distance for a share is its inverse");
                // A circle on the ground at her feet: squashed by about sin(pitch).
                double r = 0.5;
                double w = Project(a, Wide, r, 0, 0).x * Wide, top = Project(a, Wide, 0, 0, r).y, bottom = Project(a, Wide, 0, 0, -r).y;
                Assert.That((top - bottom) / (2 * w), Is.EqualTo(CameraAngles.CircleSquash(a.Pitch)).Within(0.01), a.Name + ": a small circle at her feet, about sin(pitch)");
            }
            // Ours, in numbers: 7.3 % of the screen's height (79 px at 1080 rows), 26.7 m of ground across at her feet.
            Assert.That(CameraAngles.HeightShare(55f, 30f, 28f, HerHeight), Is.EqualTo(0.0726).Within(0.0005));
            Assert.That(CameraAngles.GroundWidthAtHer(30f, 28f, Wide), Is.EqualTo(26.68).Within(0.01));
        }

        [Test]
        public void TheNumbers_WhatEachAngleShows_Written()
        {
            foreach (CameraAngle a in CameraAngles.Presets)
            {
                double share = CameraAngles.HeightShare(a.Pitch, a.FieldOfView, a.Distance, HerHeight);
                double width = CameraAngles.GroundWidthAtHer(a.FieldOfView, a.Distance, Wide);
                double behind = CameraAngles.GroundBehind(a.Pitch, a.FieldOfView, a.Distance), ahead = CameraAngles.GroundAhead(a.Pitch, a.FieldOfView, a.Distance);
                double camHeight = a.Distance * Math.Sin(a.Pitch * Math.PI / 180.0);
                Assert.That(share, Is.GreaterThan(0.03).And.LessThan(0.2), a.Name + ": she stays a figure on the screen, neither a dot nor a close-up");
                Assert.That(a.Pitch, Is.GreaterThan(30f).And.LessThan(90f), a.Name);
                Assert.That(ahead, Is.LessThan(1000.0), a.Name + ": the top edge sees the ground, not the horizon");
                TestContext.WriteLine(string.Format(Inv,
                    "CAMERAANGLES {0}: pitch {1:0.#}, field of view {2:0.#}, distance {3:0.#} m (camera {4:0.#} m up); her 1.8 m is {5:0.0} % of the screen's height ({6:0} px at 1080 rows); at 16:9 {7:0.#} m of ground across at her feet ({8:0.0} of her heights), {9:0.#} m seen behind her and {10:0.#} m ahead; a circle on the ground {11:0.00} as tall as wide",
                    a.Name, a.Pitch, a.FieldOfView, a.Distance, camHeight, 100 * share, 1080 * share, width, width / HerHeight, behind, ahead, CameraAngles.CircleSquash(a.Pitch)));
            }
        }

        // ---------------- What the floor's camera-made parts do at another angle (measured for the report) ----------------

        [Test]
        public void TheDarkLooksFarWalls_BuiltForOurs_SeenAtEachAngle_AndTheSightRulesViewOfTheGround_Measured()
        {
            var v = new LevelValues();
            var lines = new List<string>();
            int[] tallerThanAllowed = new int[CameraAngles.Count], heldMore = new int[CameraAngles.Count];
            int farTall = 0;
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
                foreach (int floor in new[] { 1, 4 })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                    // The floor as the game builds it: the rule run for ours' camera (CaveView passes the feel's pitch and the distance in use).
                    CaveRise ours = CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, Feel.CameraPitch, Feel.CameraDistance);
                    for (int a = 0; a < CameraAngles.Count; a++)
                    {
                        CameraAngle angle = CameraAngles.Presets[a];
                        // The rule run for this angle's camera: every far wall it holds lower than ours' stands, at this angle, in a line
                        // from the camera to her, her room's floor or its doors that the other walls leave free.
                        CaveRise at = CaveRise.MakeDark(l, kit, v.CaveRules, v.WallHeight, angle.Pitch, angle.Distance);
                        foreach (CryptKitSection s in kit.Sections)
                        {
                            if (s.Low) continue;
                            float built = ours.RiseOf(s.Vertical, s.Line, s.Cell), wanted = at.RiseOf(s.Vertical, s.Line, s.Cell);
                            if (a == 0 && built >= v.CaveRules.FarHeight - 1e-4f) farTall++;
                            if (built > wanted + 1e-4f) tallerThanAllowed[a]++;
                            if (wanted > built + 1e-4f) heldMore[a]++;
                        }
                    }
                }
            Assert.That(tallerThanAllowed[CameraAngles.Ours], Is.Zero, "ours: the floor is built for it");
            for (int a = 0; a < CameraAngles.Count; a++)
            {
                CameraAngle angle = CameraAngles.Presets[a];
                double behind = CameraAngles.GroundBehind(angle.Pitch, angle.FieldOfView, angle.Distance), ahead = CameraAngles.GroundAhead(angle.Pitch, angle.FieldOfView, angle.Distance);
                double side = CameraAngles.GroundWidthAtHer(angle.FieldOfView, angle.Distance, Wide) / 2.0;
                lines.Add(string.Format(Inv,
                    "{0}: of {1} far walls the floors stand tall for ours, {2} stand where this angle's sight rule would hold them at the kit's height (a tall wall in a line to her, her floor or her doors), {3} the rule would let stand that ours holds; the ground seen {4:0.#} m behind her, {5:0.#} ahead, {6:0.#} to each side at her feet, against the sight rule's {7:0.#}, {8:0.#} and {9:0.#} (CaveRise.InView): {10}",
                    angle.Name, farTall, tallerThanAllowed[a], heldMore[a], behind, ahead, side, CaveRise.ViewBehind, CaveRise.ViewAhead, CaveRise.ViewSide,
                    behind <= CaveRise.ViewBehind && ahead <= CaveRise.ViewAhead && side <= CaveRise.ViewSide ? "inside it" : "PAST it (floor seen there is not guarded)"));
            }
            foreach (string line in lines) TestContext.WriteLine("CAMERAANGLES walls, 3 seeds x floors 1 and 4: " + line);
        }

        [Test]
        public void TheNearLip_HidesHerFeet_OnlyThisCloseBehindIt_AtEachAngle()
        {
            var c = new CaveValues();
            float lip = Math.Min(new LevelValues().WallHeight, c.LipHeight);
            foreach (CameraAngle a in CameraAngles.Presets)
            {
                // A line from her feet up to the camera passes over a lip x m in front of her (toward the camera) at x·tan(pitch).
                double hides = lip / Math.Tan(a.Pitch * Math.PI / 180.0);
                Assert.That(hides, Is.LessThan(0.5), a.Name + ": closer than her own radius (0.5 m) behind the lip, which she cannot stand");
                TestContext.WriteLine(string.Format(Inv, "CAMERAANGLES lip {0}: the {1:0.##} m lip hides the floor only {2:0.##} m beyond it", a.Name, lip, hides));
            }
        }
    }
}
