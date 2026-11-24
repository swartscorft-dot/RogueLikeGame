using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 115's values (the toon look on her model, a trial): the switch on by default, the ink line's width in pixels
    /// in proportion to the screen's height from the producer's start (2 to 2.5 px at 1080 rows), and the glows under the bloom's
    /// threshold (1.2) as the game's other small glows are.
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
            Assert.That(v.InkWidth, Is.InRange(2f, 2.5f), "the producer's start: 2 to 2.5 px at 1080 rows");
            Assert.That(v.InkPixels(1080f), Is.EqualTo(v.InkWidth).Within(1e-5f));
            Assert.That(v.InkPixels(720f), Is.EqualTo(v.InkWidth * 720f / 1080f).Within(1e-5f), "at 1280 by 720, two thirds of it");
            Assert.That(v.InkPixels(2160f), Is.EqualTo(v.InkWidth * 2f).Within(1e-5f));
            Assert.That(v.InkPixels(-5f), Is.EqualTo(0f));
        }

        [Test]
        public void TheGlows_StayUnderTheBloom_AndTheShadowToneIsCoolAndDark()
        {
            var v = new ToonLookValues();
            Assert.That(v.RimStrength * Max(v.RimColour), Is.LessThan(BloomThreshold), "the edge light never blooms");
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
