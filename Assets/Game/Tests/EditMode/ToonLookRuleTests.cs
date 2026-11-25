using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 115's values (the toon look on her model, a trial): the switch on by default, the ink line's width in pixels
    /// in proportion to the screen's height from the producer's start (2 to 2.5 px at 1080 rows), and the glows under the bloom's
    /// threshold (1.2) as the game's other small glows are; since the follow-up, the fixed teal edge off and the edge light from the
    /// room's own lights on and modest.
    /// </summary>
    public class ToonLookRuleTests
    {
        /// <summary>The bloom's threshold in the scene's post-processing volume (Assets/Game/Settings/PostProcessing.asset).</summary>
        const float BloomThreshold = 1.2f;

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(ToonLookValues.On, Is.True, "the order: ToonLookValues.On, new, ON");
        }

        [Test]
        public void TheInkLine_IsTheProducersStartAt1080Rows_AndInProportionElsewhere()
        {
            var v = new ToonLookValues();
            // Work order 119 thinned it (Kristian, 2026-11-25: "Outline er for tyk."): 1.25, the producer's first guess, from 2.25.
            Assert.That(v.InkWidth, Is.InRange(1f, 1.5f), "work order 119: about 1.25 px at 1080 rows (2 to 2.5 before)");
            Assert.That(v.InkPixels(1080f), Is.EqualTo(v.InkWidth).Within(1e-5f));
            Assert.That(v.InkPixels(720f), Is.EqualTo(v.InkWidth * 720f / 1080f).Within(1e-5f), "at 1280 by 720, two thirds of it");
            Assert.That(v.InkPixels(2160f), Is.EqualTo(v.InkWidth * 2f).Within(1e-5f));
            Assert.That(v.InkPixels(-5f), Is.EqualTo(0f));
        }

        [Test]
        public void TheGlows_StayUnderTheBloom_AndTheShadowToneIsCoolAndDark()
        {
            var v = new ToonLookValues();
            Assert.That(v.RimStrength, Is.EqualTo(0f), "the fixed teal edge is off (Kristian, 2026-11-24: the light is a reflection of the place, not on her)");
            Assert.That(v.LampEdgeOn, Is.True, "the edge light from the room's own lights is on");
            Assert.That(v.LampEdgeStrength, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.8f), "a share of the light reaching her: modest, no glow");
            Assert.That(v.LampEdgeStart, Is.GreaterThanOrEqualTo(0.5f).And.LessThan(1f), "only her outer edge");
            Assert.That(v.LampEdgeSoftness, Is.GreaterThan(0f).And.LessThan(0.2f), "a thin line");
            Assert.That(v.KitTealGlow, Is.LessThan(BloomThreshold).And.GreaterThan(0f), "her pistols' teal lights glow, at most at the bloom's threshold");
            Assert.That(v.BodyTealGlow, Is.LessThan(BloomThreshold));
            Assert.That(Max(v.ShadowTint), Is.LessThan(v.LitLevel * 0.5f), "the shadow tone well under the lit one: two tones, not a gradient");
            Assert.That(v.ShadowTint.B, Is.GreaterThan(v.ShadowTint.R).And.GreaterThan(v.ShadowTint.G), "slightly cool, not grey");
            Assert.That(v.LitSoftness, Is.LessThan(0.1f), "a hard edge between the tones");
            Assert.That(v.RimColour.G, Is.GreaterThan(v.RimColour.R), "teal");
            Assert.That(v.RimColour.B, Is.GreaterThan(v.RimColour.R), "teal");
            Assert.That(Max(v.InkColour), Is.LessThan(0.1f), "a dark ink");
        }

        static float Max(Rgb c) => System.Math.Max(c.R, System.Math.Max(c.G, c.B));
    }
}
