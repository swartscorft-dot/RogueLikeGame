using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The duck's key sign and its slightly larger reach (work order 132; Kristian, 2026-11-25: "Ctrl/shift når det er mulig. Kanskje
    /// gjøre området litt større"), as the rules see it: the switch on by default and off as before, the reach a quarter more with
    /// it on and the feel's own value untouched, the key's words, and where the plate stands beside the rim.
    /// </summary>
    public class DuckKeySignRulesTests
    {
        static readonly FeelValues Feel = new FeelValues();
        // A sarcophagus as a level stands it (QuickDuckRulesTests): 1.9 m along x, 1.1 m along z, its near face 0.6 m north of her.
        static readonly CoverShape Sarcophagus = CoverShape.Box(new GroundPoint(0f, 1.15f), 1.9f, 1.1f);

        [Test]
        public void TheSwitch_IsOnByDefault_TheFactorAQuarterMore_AndTheFeelsReachIsUntouched()
        {
            Assert.That(DuckKeySignValues.On, Is.True, "his words: \"Ctrl/shift når det er mulig. Kanskje gjøre området litt større\"");
            Assert.That(DuckKeySignValues.ReachFactor, Is.EqualTo(1.25f), "the producer's first guess: a quarter more");
            Assert.That(Feel.DuckReach, Is.EqualTo(1f), "the feel's value stays 1 m: the factor is its own value");
            Assert.That(DuckKeySignRules.Reach(Feel.DuckReach), Is.EqualTo(1.25f), "in use: 1.25 m");
        }

        [Test]
        public void SwitchOff_TheReachIsTheFeelsAsBefore()
        {
            Assert.That(DuckKeySignRules.Reach(false, 1f, 1.25f), Is.EqualTo(1f));
            Assert.That(DuckKeySignRules.Reach(false, 0.8f, 2f), Is.EqualTo(0.8f));
            Assert.That(DuckKeySignRules.Reach(true, 1f, 1.5f), Is.EqualTo(1.5f), "the factor moves it");
            Assert.That(DuckKeySignRules.Reach(true, 1f, 0.5f), Is.EqualTo(1f), "never smaller than the feel's");
        }

        [Test]
        public void TheLargerReach_TakesInAPropWhoseFaceIsUpTo125mOff_AndNoFarther()
        {
            float reach = DuckKeySignRules.Reach(true, Feel.DuckReach, 1.25f);
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.45f), Feel.DuckReach), Is.False, "1.05 m: out of the old reach");
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.45f), reach), Is.True, "in the new");
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.64f), reach), Is.True, "1.24 m");
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.7f), reach), Is.False, "1.3 m: out");
        }

        [Test]
        public void ADuck_FromTheNewEdge_IsStillBehindTheProp_ForAShooterStraightBeyondIt()
        {
            var herAtTheEdge = new GroundPoint(0f, -0.64f); // 1.24 m from its face
            Assert.That(QuickDuckRules.Shields(Sarcophagus, herAtTheEdge, new GroundPoint(0f, 10f), ProjectileKind.Arrow), Is.True, "an arrow from beyond it");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, herAtTheEdge, new GroundPoint(3f, 10f), ProjectileKind.Arrow), Is.True, "and from a little to the side");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, herAtTheEdge, new GroundPoint(0f, 10f), ProjectileKind.Orb), Is.False, "an orb passes over, as before");
        }

        [TestCase("LeftCtrl", "Ctrl")]
        [TestCase("RightCtrl", "Ctrl")]
        [TestCase("LeftShift", "Shift")]
        [TestCase("RightAlt", "Alt")]
        [TestCase("Digit4", "4")]
        [TestCase("C", "C")]
        [TestCase("Space", "Space")]
        [TestCase("None", "")]
        [TestCase("", "")]
        public void TheKeysWords(string keyName, string words) => Assert.That(DuckKeySignRules.KeyLabel(keyName), Is.EqualTo(words));

        [Test]
        public void ThePlate_StandsBesideTheRimsBox_OnTheSideAwayFromHer_NeverOverIt()
        {
            // The rim's box on the screen from x 900 to 960, its top at 500; the plate 30 by 20.
            float left = DuckKeySignRules.PlateLeft(900f, 960f, 500f, 880f, 30f, 20f, out float top);
            Assert.That(left, Is.EqualTo(960f + DuckKeySignValues.Gap), "she is on the left: it stands on the right");
            Assert.That(top, Is.EqualTo(490f), "centred on the box's top line");
            left = DuckKeySignRules.PlateLeft(900f, 960f, 500f, 990f, 30f, 20f, out _);
            Assert.That(left + 30f, Is.EqualTo(900f - DuckKeySignValues.Gap), "she is on the right: it stands on the left");
            Assert.That(left + 30f <= 900f || left >= 960f, Is.True, "never over the box");
        }

        [Test]
        public void ItsLook_IsSmallerThanTheChestPrompt_AndQuiet()
        {
            var v = new InterfaceValues();
            Assert.That(DuckKeySignValues.KeySize, Is.LessThan(v.PromptSize), "smaller than the chest prompt's words");
            Assert.That(DuckKeySignValues.Alpha, Is.LessThan(1f).And.GreaterThan(0.5f), "quiet, and readable");
        }
    }
}
