using System;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 063 (his words of 2026-11-21: "må være litt større ... vanskelig å se coolddowns nede. så litt høyere opp
    /// og en del større"; "hp indikator og focus er for høyt på karakteren, må være mer under"; "gjerne en liten visual på
    /// spell uiet når noe er av cd"), as plain rules: the new values' defaults; the scales at 1 with the lift and the gap at
    /// 0 give this morning's rectangles exactly; the strip and each box at 1920 by 1080 and 1280 by 720 on screen and lifted
    /// over the bottom edge; the strip clear of the strokes when she stands at the middle; the cooldown's number the largest
    /// thing in a cooling box; when a box flashes ready and how the kick runs; the recast window's shares on its box.
    /// </summary>
    public class InterfaceLargerTests
    {
        static InterfaceValues V => new InterfaceValues();

        /// <summary>This morning's look: every new scale at 1, the lift and the gap in metres at 0.</summary>
        static InterfaceValues Morning() => new InterfaceValues
        {
            FightScale = 1f, StripScale = 1f, StripLift = 0f, CooldownGrow = 1f, UnderFeetMetres = 0f, RecastGrow = 1f, BladeScale = 1f, RunBoxScale = 1f,
            Depth = 0f, Outline = 0f, StripPlate = false, Slash = 0f, Halftone = 0f, CellEdge = 0f, ReadyFlash = false,
            CoolingShade = new Rgba(0f, 0f, 0f, 0f), CoolingIconAlpha = 1f,
        };

        // ---------------- 4. Less flat ----------------

        [Test]
        public void TheDepthValues_AreOnByDefault_AndEachTurnsOff()
        {
            InterfaceValues v = V;
            Assert.That(v.Depth, Is.GreaterThan(0f), "a dark plate offset behind the boxes");
            Assert.That(v.Outline, Is.GreaterThan(0f), "a hard outline");
            Assert.That(v.StripPlate, Is.True, "one plate behind the strip");
            Assert.That(v.Slash, Is.GreaterThan(0f), "hot stripes at the cut edges");
            Assert.That(v.Halftone, Is.InRange(0.01f, 1f), "a halftone accent");
            Assert.That(v.CellEdge, Is.InRange(0.01f, 1f), "the boxes edged apart on the plate");
            Assert.That(v.DepthInk.R + v.DepthInk.G + v.DepthInk.B, Is.LessThan(0.2f), "the plate is the palette's black");
            InterfaceValues m = Morning();
            Assert.That(m.Depth + m.Outline + m.Slash + m.Halftone + m.CellEdge, Is.Zero, "every one at 0 is this morning's flat look");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void TheStripsPlate_HoldsTheDashToFocus_LeavesTheCoverTagOutside_AndStaysOnScreen(float w, float h)
        {
            InterfaceValues v = V;
            float width = LayoutWidth(w, h);
            UiBox plate = InterfaceRules.StripPlate(8, width, v), left = InterfaceRules.LeftEnd(8, width, v), right = InterfaceRules.RightEnd(8, width, v);
            UiBox cover = InterfaceRules.CoverTag(8, width, v);
            Assert.That(plate.X, Is.LessThan(left.X));
            Assert.That(plate.Right, Is.GreaterThan(right.Right));
            Assert.That(plate.Y, Is.LessThan(left.Y));
            Assert.That(plate.Bottom, Is.GreaterThan(left.Bottom));
            Assert.That(plate.Bottom + (v.Depth + v.Outline) * InterfaceRules.Strip(v), Is.LessThan(v.LayoutHeight), "its plate and shadow on screen");
            Assert.That(cover.Right, Is.LessThan(plate.X), "the cover tag outside it");
            Assert.That(v.Slash, Is.LessThanOrEqualTo(v.StripPlatePad), "the stripes inside its pad, clear of the boxes");
        }

        /// <summary>The view's width in layout pixels (1080 high) for a screen of this size.</summary>
        static float LayoutWidth(float w, float h) => w / (h / 1080f);

        // ---------------- The values ----------------

        [Test]
        public void TheNewValues_HaveTheirFirstGuesses()
        {
            InterfaceValues v = V;
            Assert.That(v.FightScale, Is.EqualTo(1.25f), "the master scale");
            Assert.That(InterfaceRules.Strip(v), Is.EqualTo(1.6f).Within(1e-4f), "the strip's boxes 1.6 times this morning's");
            Assert.That(v.StripLift, Is.EqualTo(0.09f), "9 % of the height over the bottom");
            Assert.That(v.UnderFeetMetres, Is.EqualTo(0.35f), "0.35 m under her feet");
            Assert.That(v.ReadyFlash, Is.True);
            Assert.That(v.ReadyFlashSeconds, Is.EqualTo(0.25f));
            Assert.That(v.BladeScale, Is.EqualTo(1.5f), "the blade 1.5 times on top of the master scale (item 2b)");
            Assert.That(v.RecastGrow, Is.GreaterThan(1f), "a lit box a little larger than its neighbours");
            Assert.That(InterfaceRules.Fight(v) * v.RunBoxScale, Is.EqualTo(1f).Within(1e-4f), "the run box at this morning's size: the map stands beside it");
        }

        [Test]
        public void WithTheScalesAt1_AndTheLiftAndTheGapAt0_TheRectanglesAreThisMorningsExactly()
        {
            InterfaceValues v = Morning();
            // The strip: eight 56 by 54 slots 5 apart, 22 over the bottom of a 1920 by 1080 view.
            UiBox first = InterfaceRules.Slot(0, 8, 1920f, v), last = InterfaceRules.Slot(7, 8, 1920f, v);
            Assert.That(first.X, Is.EqualTo(718.5f).Within(1e-3f));
            Assert.That(first.Y, Is.EqualTo(1004f).Within(1e-3f));
            Assert.That(first.W, Is.EqualTo(56f));
            Assert.That(first.H, Is.EqualTo(54f));
            Assert.That(last.X, Is.EqualTo(1145.5f).Within(1e-3f));
            UiBox left = InterfaceRules.LeftEnd(8, 1920f, v), right = InterfaceRules.RightEnd(8, 1920f, v);
            Assert.That(left.X, Is.EqualTo(665.5f).Within(1e-3f));
            Assert.That(left.W, Is.EqualTo(44f));
            Assert.That(right.X, Is.EqualTo(1210.5f).Within(1e-3f));
            UiBox face = InterfaceRules.Face(first, v);
            Assert.That(face.H, Is.EqualTo(54f - 13f * 1.25f - 2f).Within(1e-3f), "the face over the key's plate, as before");
            Assert.That(InterfaceRules.CooldownFont(v), Is.EqualTo(21));
            Assert.That(InterfaceRules.KeyFont(v), Is.EqualTo(13));
            // The strokes: 70 by 7, 8 apart, 16 under her feet; the blade 150 by 10, 18 over her head.
            UiBox h = InterfaceRules.HealthStroke(960f, 540f, v), f = InterfaceRules.FocusStroke(960f, 540f, v);
            Assert.That(new[] { h.X, h.Y, h.W, h.H }, Is.EqualTo(new[] { 886f, 556f, 70f, 7f }).Within(1e-3f));
            Assert.That(new[] { f.X, f.Y, f.W, f.H }, Is.EqualTo(new[] { 964f, 556f, 70f, 7f }).Within(1e-3f));
            Assert.That(InterfaceRules.UnderFeetGap(540f, 600f, v), Is.EqualTo(16f), "no metres: this morning's 16 pixels");
            UiBox b = InterfaceRules.Blade(800f, 400f, v);
            Assert.That(new[] { b.X, b.Y, b.W, b.H }, Is.EqualTo(new[] { 725f, 372f, 150f, 10f }).Within(1e-3f));
            Assert.That(InterfaceRules.Grown(first, v.RecastGrow).X, Is.EqualTo(first.X), "a lit box no larger");
        }

        // ---------------- Sizes that hold (item 5) ----------------

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void TheStripAndEachBox_AreOnScreen_LiftedOverTheBottomEdge_AndTheBoxes16TimesThisMornings(float w, float h)
        {
            InterfaceValues v = V, morning = Morning();
            float width = LayoutWidth(w, h);
            float lift = v.LayoutHeight * v.StripLift;
            Assert.That(lift, Is.EqualTo(97.2f).Within(1e-3f), "9 % of 1080 (this morning 22)");
            UiBox whole = InterfaceRules.WholeStrip(8, width, v);
            Assert.That(whole.X, Is.GreaterThan(0f), "on screen at the left");
            Assert.That(whole.Right, Is.LessThan(width), "and at the right");
            Assert.That(whole.Bottom, Is.EqualTo(v.LayoutHeight - lift).Within(1e-3f), "lifted by 9 % of the height");
            for (int key = 0; key < 8; key++)
            {
                UiBox box = InterfaceRules.Slot(key, 8, width, v), was = InterfaceRules.Slot(key, 8, width, morning);
                Assert.That(box.Bottom, Is.EqualTo(v.LayoutHeight - lift).Within(1e-3f), "box " + key + " over the bottom edge by the lift");
                Assert.That(box.Y, Is.GreaterThan(0f));
                Assert.That(box.W, Is.EqualTo(was.W * 1.6f).Within(1e-3f), "1.6 times as wide");
                Assert.That(box.H, Is.EqualTo(was.H * 1.6f).Within(1e-3f), "and as high");
                if (key > 0) Assert.That(box.X, Is.GreaterThan(InterfaceRules.Slot(key - 1, 8, width, v).Right), "apart");
            }
            UiBox lit = InterfaceRules.Grown(InterfaceRules.Slot(0, 8, width, v), v.RecastGrow);
            Assert.That(lit.Bottom, Is.LessThan(v.LayoutHeight), "a lit box stays on screen");
        }

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void TheStrip_IsClearOfTheStrokesAndTheBlade_WhenSheStandsAtTheMiddle(float w, float h)
        {
            InterfaceValues v = V;
            float width = LayoutWidth(w, h);
            // Her feet at the view's middle (the camera looks at her), and the gap of 0.35 m with the game's camera.
            var feel = new FeelValues();
            float perMetre = v.LayoutHeight / (2f * feel.CameraDistance * (float)Math.Tan(feel.FieldOfView * 0.5 * Math.PI / 180.0));
            float under = InterfaceRules.UnderFeetGap(540f, 540f + v.UnderFeetMetres * perMetre, v);
            Assert.That(under, Is.EqualTo(25.2f).Within(0.2f), "0.35 m is about 25 layout pixels (this morning 16)");
            UiBox stroke = InterfaceRules.HealthStroke(width * 0.5f, 540f, under, v);
            UiBox number = InterfaceRules.HealthNumber(width * 0.5f, 540f, under, v);
            float stateWords = stroke.Bottom + 2f + InterfaceRules.Size(v.StateWordSize, InterfaceRules.Fight(v)) * 1.3f;
            UiBox whole = InterfaceRules.WholeStrip(8, width, v);
            Assert.That(Math.Max(number.Bottom, stateWords), Is.LessThan(whole.Y - 100f), "the strokes, their number and words well over the strip");
            Assert.That(InterfaceRules.Blade(width * 0.5f, 450f, v).Y, Is.GreaterThan(0f));
            Assert.That(whole.Y, Is.GreaterThan(v.LayoutHeight * 0.75f), "the strip in the bottom quarter: clear of the run box and the map at the top right");
        }

        [Test]
        public void TheCooldownsNumber_IsTheLargestThingInACoolingBox_AndTheKeySmall()
        {
            InterfaceValues v = V;
            int number = InterfaceRules.CooldownFont(v);
            Assert.That(number, Is.EqualTo(42), "21 × 1.6 × 1.25");
            // The heavy condensed italic's figures stand about 0.7 of its size: 42 gives about 29 layout pixels (at least 26 asked).
            Assert.That(number * 0.7f, Is.GreaterThanOrEqualTo(26f));
            Assert.That(number, Is.GreaterThan(InterfaceRules.Size(v.ShortNameSize, InterfaceRules.Strip(v))));
            Assert.That(number, Is.GreaterThan(InterfaceRules.KeyFont(v) * 2), "the key small beside it");
            Assert.That(number * 1.25f, Is.LessThan(InterfaceRules.Face(InterfaceRules.Slot(0, 8, 1920f, v), v).H), "a line of it fits the face's height");
            Assert.That(v.CoolingShade.A, Is.GreaterThanOrEqualTo(0.5f), "the shade dark enough to read without the number");
        }

        // ---------------- Off cooldown (item 2) ----------------

        [Test]
        public void ABoxFlashesReady_OnlyWhenItsSkillWaitedAndNoLonger_TheSameSkill_AndNothingQuietsIt()
        {
            Assert.That(InterfaceRules.CameReady(true, false, true, false), Is.True);
            Assert.That(InterfaceRules.CameReady(false, false, true, false), Is.False, "it was ready already");
            Assert.That(InterfaceRules.CameReady(true, true, true, false), Is.False, "still waiting");
            Assert.That(InterfaceRules.CameReady(true, false, false, false), Is.False, "another skill on the key");
            Assert.That(InterfaceRules.CameReady(true, false, true, true), Is.False, "paused, a choice, a reset, a frame not drawn");
        }

        [Test]
        public void TheFlash_FadesOverAQuarterSecond_AndTheKickJumpsAndSettles()
        {
            InterfaceValues v = V;
            Assert.That(InterfaceRules.ReadyFlashAlpha(0f, v), Is.EqualTo(1f));
            Assert.That(InterfaceRules.ReadyFlashAlpha(0.125f, v), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(InterfaceRules.ReadyFlashAlpha(0.25f, v), Is.Zero);
            Assert.That(InterfaceRules.ReadyFlashAlpha(-1f, v), Is.Zero, "nothing before it");
            float most = v.ReadyKick * InterfaceRules.Strip(v);
            float top = InterfaceRules.ReadyKickNow(v.ReadyKickRise * v.ReadyFlashSeconds, v);
            Assert.That(top, Is.EqualTo(most).Within(1e-3f), "up to its height at once");
            Assert.That(InterfaceRules.ReadyKickNow(0.2f, v), Is.InRange(0f, top * 0.5f), "settling");
            Assert.That(InterfaceRules.ReadyKickNow(0.25f, v), Is.Zero, "settled");
            Assert.That(InterfaceRules.ReadyFlashAlpha(0.1f, new InterfaceValues { ReadyFlash = false }), Is.Zero, "off: this morning's");
        }

        // ---------------- The recast window on its box (item 2b) ----------------

        [Test]
        public void TheRecastWindow_OnItsBox_IsWhereTheClockIs_ItsCleanPartAtItsShares()
        {
            Assert.That(InterfaceRules.RecastShares(0.6f, 0.15f, 0.1f, 0.3f, out float now, out float from, out float to), Is.True);
            Assert.That(now, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(from, Is.EqualTo(1f / 6f).Within(1e-5f));
            Assert.That(to, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(InterfaceRules.RecastShares(0.6f, -1f, 0.1f, 0.3f, out _, out _, out _), Is.False, "not open yet");
            Assert.That(InterfaceRules.RecastShares(0.6f, 0.61f, 0.1f, 0.3f, out _, out _, out _), Is.False, "run out");
            Assert.That(InterfaceRules.RecastShares(0.6f, 0.2f, 0f, 0f, out _, out from, out to), Is.True, "a press that cannot be clean");
            Assert.That(to - from, Is.Zero, "has no band");
            var bar = new UiBox(100f, 50f, 80f, 10f);
            UiBox band = InterfaceRules.RecastBand(bar, 0.25f, 0.5f);
            Assert.That(band.X, Is.EqualTo(120f).Within(1e-4f));
            Assert.That(band.W, Is.EqualTo(20f).Within(1e-4f));
        }
    }
}
