using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 119's values (Kristian, 2026-11-25, after two runs with the toon look on her: "Samme på fiendene. Outline er for
    /// tyk. Kan vi fp henne i litt høyere oppløsning?"): the enemies' switch on by default and only inside the look's own, the ink
    /// line thinner than work order 115's 2.25 px, and her texture read less blurred than its 1.5 mip levels.
    /// </summary>
    public class EnemyToonLookRuleTests
    {
        [TearDown]
        public void PutBack()
        {
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
        }

        [Test]
        public void TheEnemiesSwitch_IsOnByDefault_AndOnlyInsideTheLooksOwn()
        {
            Assert.That(ToonLookValues.Enemies, Is.True, "the order: ToonLookValues.Enemies, new, ON");
            ToonLookValues.On = true;
            ToonLookValues.Enemies = true;
            Assert.That(ToonLookValues.EnemiesShown, Is.True);
            ToonLookValues.On = false;
            Assert.That(ToonLookValues.EnemiesShown, Is.False, "the pause menu's line turns her and them off together");
            ToonLookValues.On = true;
            ToonLookValues.Enemies = false;
            Assert.That(ToonLookValues.EnemiesShown, Is.False, "their own switch turns them alone off");
            Assert.That(ToonLookValues.On, Is.True, "and leaves her in it");
        }

        [Test]
        public void TheLine_IsThinnerThanWorkOrder115s_AndHerTextureIsReadLessBlurred()
        {
            var v = new ToonLookValues();
            Assert.That(v.InkWidth, Is.EqualTo(1.25f), "the producer's first guess after \"Outline er for tyk\" (2.25 before)");
            Assert.That(v.InkWidth, Is.LessThan(2.25f));
            Assert.That(v.DetailCalm, Is.EqualTo(0.5f), "the producer's first guess for \"litt høyere oppløsning\" (1.5 before)");
            Assert.That(v.DetailCalm, Is.GreaterThanOrEqualTo(0f));
        }
    }
}
