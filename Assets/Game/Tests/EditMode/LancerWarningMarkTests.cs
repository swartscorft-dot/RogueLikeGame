using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The lancer's warning mark (work order 145; Kristian, 2026-11-27: "Utenomfra, men et lite varsel ikon kan være nice. Et
    /// utropstegn"), as rules: the switch is on and the lancer's own rule stays off; when the mark is up (the table of its
    /// charge, his screen, its life and the switch); where on the screen's edge it stands for a lancer in each direction, inside
    /// the insets, and over the skill strip. The play is LancerWarningMarkPlayTests'.
    /// </summary>
    public class LancerWarningMarkTests
    {
        const float W = 1920f, H = 1080f; // a view 1080 high in layout px, 16:9
        const float Eps = 1e-3f;

        float _inset, _bottom, _height, _width, _outline, _gap;

        [SetUp]
        public void Keep()
        {
            _inset = LancerWarningMark.Inset;
            _bottom = LancerWarningMark.BottomInset;
            _height = LancerWarningMark.Height;
            _width = LancerWarningMark.Width;
            _outline = LancerWarningMark.Outline;
            _gap = LancerWarningMark.DotGap;
        }

        [TearDown]
        public void PutBack()
        {
            LancerWarningMark.Inset = _inset;
            LancerWarningMark.BottomInset = _bottom;
            LancerWarningMark.Height = _height;
            LancerWarningMark.Width = _width;
            LancerWarningMark.Outline = _outline;
            LancerWarningMark.DotGap = _gap;
        }

        // ---------------- The switch and the values ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheLancersOwnRuleStaysOff()
        {
            Assert.That(LancerWarningMark.On, Is.True, "his words: \"et lite varsel ikon kan være nice. Et utropstegn\"");
            Assert.That(RangedOpeningRules.LancerToo, Is.False, "\"Utenomfra\": the lancer charges from off his screen as before");
            Assert.That(LancerWarningMark.ScreenMargin, Is.EqualTo(0f), "the plain edge of what the camera shows, without the shooters' margin");
            Assert.That(LancerWarningMark.Pool, Is.GreaterThanOrEqualTo(3), "a mark for each lancer of a room");
        }

        [Test]
        public void TheMark_IsSmall_AndInTheLancersBlue()
        {
            Assert.That(LancerWarningMark.Height, Is.InRange(20f, 48f), "about the size of the strip's small signs, in layout px of a view 1080 high");
            Assert.That(LancerWarningMark.BarHeight, Is.GreaterThan(LancerWarningMark.Width), "a bar over a dot: an exclamation mark");
            Assert.That(LancerWarningMark.BarHeight + LancerWarningMark.DotGap + LancerWarningMark.Width, Is.EqualTo(LancerWarningMark.Height).Within(Eps));
            Assert.That(LancerWarningMark.Outline, Is.GreaterThan(0f), "a dark outline, as the HUD's other small signs");
            Rgba c = LancerWarningMark.Colour;
            Assert.That(new[] { c.R, c.G, c.B, c.A }, Is.EqualTo(new[] { 0.15f, 0.45f, 1f, 1f }), "the glint's blue (CombatFxValues.CounterRim): the lancer's charge can be countered");
            Assert.That(MeleeYouReadRules.GlintIsBlue(LancerValues.ChargeAttack()), Is.True, "the lancer's own glint is blue");
        }

        // ---------------- When it is up ----------------

        [Test]
        public void ItIsUp_OnlyForACharge_BegunOffScreen_AliveAndOn_UntilItIsSeen()
        {
            // The table: the first step of a charge (wind-up or run) with every combination.
            foreach (bool on in new[] { true, false })
            foreach (bool alive in new[] { true, false })
            foreach (bool inCharge in new[] { true, false })
            foreach (bool onScreen in new[] { true, false })
            {
                var t = new LancerWarningMark.Track();
                bool up = LancerWarningMark.Step(ref t, on, alive, inCharge, onScreen);
                bool expected = on && alive && inCharge && !onScreen;
                Assert.That(up, Is.EqualTo(expected), $"on {on}, alive {alive}, in its charge {inCharge}, on screen {onScreen}");
                Assert.That(t.InCharge, Is.EqualTo(on && alive && inCharge), "followed only through a charge, with the switch on");
            }
        }

        [Test]
        public void BegunOffScreen_ItStays_ThenGoes_WhenSeen_AndDoesNotComeBack_InTheSameCharge()
        {
            var t = new LancerWarningMark.Track();
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.True, "its wind-up begins off screen: up");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.True, "the wind-up goes on off screen: up");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.True, "its run, still off screen: up");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, true), Is.False, "on screen: gone (he sees the lancer)");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.False, "off screen again in the same charge: not again");
            Assert.That(LancerWarningMark.Step(ref t, true, true, false, false), Is.False, "the charge over");
            Assert.That(t.InCharge, Is.False, "let go");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.True, "its next charge, off screen: up again");
        }

        [Test]
        public void ItGoes_WhenTheChargeEnds_WhenItDies_AndWithTheSwitchOff()
        {
            var t = new LancerWarningMark.Track();
            LancerWarningMark.Step(ref t, true, true, true, false);
            Assert.That(LancerWarningMark.Step(ref t, true, true, false, false), Is.False, "the charge over (it recovers, or a counter dazed it)");
            t = new LancerWarningMark.Track();
            LancerWarningMark.Step(ref t, true, true, true, false);
            Assert.That(LancerWarningMark.Step(ref t, true, false, true, false), Is.False, "dead");
            t = new LancerWarningMark.Track();
            LancerWarningMark.Step(ref t, true, true, true, false);
            Assert.That(LancerWarningMark.Step(ref t, false, true, true, false), Is.False, "the switch turned off");
            Assert.That(t.InCharge || t.Marked, Is.False, "nothing kept");
        }

        [Test]
        public void BegunOnScreen_ItGetsNone_ForTheWholeCharge()
        {
            var t = new LancerWarningMark.Track();
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, true), Is.False, "its wind-up begins on screen");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.False, "then off screen (she walked away): still none");
            Assert.That(LancerWarningMark.Step(ref t, true, true, true, false), Is.False, "its run off screen: none");
        }

        // ---------------- Where it stands ----------------

        static void Bounds(float w, float h, out float l, out float t, out float r, out float b) => LancerWarningMark.Bounds(w, h, out l, out t, out r, out b);

        static (float x, float y) Place(float hx, float hy, float dx, float dy, float w = W, float h = H)
        {
            Assert.That(LancerWarningMark.Place(hx, hy, dx, dy, w, h, out float x, out float y), Is.True, "placed");
            return (x, y);
        }

        [Test]
        public void ForALancerInEachDirection_ItStandsAtThatEdge_OnTheLineFromHer()
        {
            Bounds(W, H, out float l, out float t, out float r, out float b);
            float hx = W * 0.5f, hy = H * 0.5f;
            var up = Place(hx, hy, 0f, -1f);
            Assert.That(up.x, Is.EqualTo(hx).Within(Eps)); Assert.That(up.y, Is.EqualTo(t).Within(Eps), "above: the top edge, straight over her");
            var down = Place(hx, hy, 0f, 1f);
            Assert.That(down.x, Is.EqualTo(hx).Within(Eps)); Assert.That(down.y, Is.EqualTo(b).Within(Eps), "below: the bottom edge (over the strip), straight under her");
            var right = Place(hx, hy, 1f, 0f);
            Assert.That(right.x, Is.EqualTo(r).Within(Eps)); Assert.That(right.y, Is.EqualTo(hy).Within(Eps), "right: the right edge, level with her");
            var left = Place(hx, hy, -1f, 0f);
            Assert.That(left.x, Is.EqualTo(l).Within(Eps)); Assert.That(left.y, Is.EqualTo(hy).Within(Eps), "left: the left edge");
            // The corners: the way straight to each corner of the box ends in it.
            foreach ((float cx, float cy) in new[] { (l, t), (r, t), (r, b), (l, b) })
            {
                var c = Place(hx, hy, cx - hx, cy - hy);
                Assert.That(c.x, Is.EqualTo(cx).Within(0.01f)); Assert.That(c.y, Is.EqualTo(cy).Within(0.01f), $"the corner ({cx}, {cy})");
            }
            // A way between: on the line from her, at the edge it meets first.
            var steep = Place(hx, hy, 1f, -2f); // up and to the right, steeper than the corner: the top edge
            Assert.That(steep.y, Is.EqualTo(t).Within(Eps));
            Assert.That((steep.x - hx) / (steep.y - hy), Is.EqualTo(-0.5f).Within(1e-4f), "on the line");
            var flat = Place(hx, hy, 3f, 1f); // right and a little down: the right edge
            Assert.That(flat.x, Is.EqualTo(r).Within(Eps));
            Assert.That((flat.y - hy) / (flat.x - hx), Is.EqualTo(1f / 3f).Within(1e-4f), "on the line");
            // She is not at the middle: the line starts at her.
            var fromCorner = Place(300f, 200f, 0f, 1f);
            Assert.That(fromCorner.x, Is.EqualTo(300f).Within(Eps)); Assert.That(fromCorner.y, Is.EqualTo(b).Within(Eps));
        }

        [Test]
        public void EveryWay_TheWholeMark_IsInsideTheInsets_AtBothShapesOfScreen()
        {
            float hw = LancerWarningMark.HalfWidth, hh = LancerWarningMark.HalfHeight;
            foreach (float w in new[] { W, 1440f, 2520f }) // 16:9, 4:3 and 21:9, all 1080 high in layout px
                for (int deg = 0; deg < 360; deg++)
                {
                    double a = deg * Math.PI / 180.0;
                    var p = Place(w * 0.5f, H * 0.5f, (float)Math.Cos(a), (float)Math.Sin(a), w, H);
                    Assert.That(p.x - hw, Is.GreaterThanOrEqualTo(LancerWarningMark.Inset - Eps), $"{w}: {deg} degrees, the left inset");
                    Assert.That(p.x + hw, Is.LessThanOrEqualTo(w - LancerWarningMark.Inset + Eps), $"{w}: {deg} degrees, the right inset");
                    Assert.That(p.y - hh, Is.GreaterThanOrEqualTo(LancerWarningMark.Inset - Eps), $"{w}: {deg} degrees, the top inset");
                    Assert.That(p.y + hh, Is.LessThanOrEqualTo(H - LancerWarningMark.BottomInset + Eps), $"{w}: {deg} degrees, the bottom inset");
                    // And at an edge of the box: one of its sides touches an inset.
                    Bounds(w, H, out float l, out float t, out float r, out float b);
                    float edge = Math.Min(Math.Min(Math.Abs(p.x - l), Math.Abs(p.x - r)), Math.Min(Math.Abs(p.y - t), Math.Abs(p.y - b)));
                    Assert.That(edge, Is.LessThan(0.01f), $"{w}: {deg} degrees, at the edge");
                }
        }

        [Test]
        public void HerPointOutsideTheBox_IsTakenAtItsNearestPoint_AndNoWayGivesNone()
        {
            Bounds(W, H, out float l, out float t, out float r, out float b);
            var p = Place(W * 0.5f, H - 20f, 0f, 1f); // she stands low on the screen, under the bottom inset
            Assert.That(p.y, Is.EqualTo(b).Within(Eps));
            Assert.That(LancerWarningMark.Place(W * 0.5f, H * 0.5f, 0f, 0f, W, H, out _, out _), Is.False, "a lancer on her point: no way to show");
        }

        [Test]
        public void KeptOffTheTopPlate_EveryWay_OnTheSameLine()
        {
            var v = new InterfaceValues();
            UiBox plate = InterfacePlate.Whole(v);
            float hw = LancerWarningMark.HalfWidth, hh = LancerWarningMark.HalfHeight, inset = LancerWarningMark.Inset;
            float hx = W * 0.5f, hy = H * 0.5f;
            int moved = 0;
            for (int deg = 0; deg < 360; deg++)
            {
                double a = deg * Math.PI / 180.0;
                float dx = (float)Math.Cos(a), dy = (float)Math.Sin(a);
                Assert.That(LancerWarningMark.Place(hx, hy, dx, dy, W, H, plate, out float x, out float y), Is.True);
                bool clear = x - hw >= plate.X + plate.W + inset - 0.01f || y - hh >= plate.Y + plate.H + inset - 0.01f;
                Assert.That(clear, Is.True, $"{deg} degrees: ({x:0.0}, {y:0.0}) is off the plate ({plate.X}, {plate.Y}, {plate.W}, {plate.H})");
                float cross = (x - hx) * dy - (y - hy) * dx;
                Assert.That(Math.Abs(cross), Is.LessThan(0.05f), $"{deg} degrees: on the line from her");
                LancerWarningMark.Place(hx, hy, dx, dy, W, H, out float x0, out float y0);
                if (Math.Abs(x - x0) > 0.01f || Math.Abs(y - y0) > 0.01f) moved++;
            }
            Assert.That(moved, Is.GreaterThan(0).And.LessThan(90), "only the ways to the top left are moved");
            Assert.That(LancerWarningMark.Place(hx, hy, 0f, -1f, W, H, plate, out float ux, out _), Is.True);
            Assert.That(ux, Is.EqualTo(hx).Within(Eps), "straight up: as without the plate");
        }

        [Test]
        public void TheBottomInset_KeepsTheMarkOverTheSkillStrip()
        {
            var v = new InterfaceValues();
            UiBox slot = InterfaceRules.Slot(0, 6, W, v);
            float lowest = H - LancerWarningMark.BottomInset; // the mark's box's foot at its lowest
            Assert.That(lowest, Is.LessThan(slot.Y - 10f), $"the strip's boxes' top {slot.Y:0.0} px; the mark's foot {lowest:0.0}");
            Assert.That(LancerWarningMark.Inset, Is.InRange(8f, 64f), "a small inset at the other three edges");
        }

        [Test]
        public void TheGamesCamera_ShowsALancer13mBelowHer_OffScreen_And10AndAHalfAboveHer_On()
        {
            // The view of the ground of the game's own camera (CameraAngles' ours: 55 degrees, 30 of view, 28 m), 16:9 and 4:3: the
            // places the play tests put their lancers.
            foreach (double aspect in new[] { 16.0 / 9.0, 4.0 / 3.0 })
            {
                GroundQuad q = ScreenGround.Design(0, 0, CameraAngles.OursDistance, CameraAngles.OursPitch, CameraAngles.OursFieldOfView, aspect);
                Assert.That(ScreenGround.Shows(q, 0, -13, LancerWarningMark.ScreenMargin), Is.False, "13 m below her: off screen");
                Assert.That(ScreenGround.Shows(q, 15, 0, LancerWarningMark.ScreenMargin), Is.False, "15 m to her side: off screen");
                Assert.That(ScreenGround.Shows(q, 0, 10.5, LancerWarningMark.ScreenMargin), Is.True, "10.5 m above her: on screen");
                Assert.That(ScreenGround.Shows(q, 0, -7, LancerWarningMark.ScreenMargin), Is.True, "7 m below her: on screen");
            }
        }
    }
}
