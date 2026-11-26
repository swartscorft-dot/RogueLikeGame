using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>The menus' help's rules (the follow-up to work order 128): the switch on, the words, how much more she needs, the one dying shake and the flash.</summary>
    public class MenuHelpRulesTests
    {
        [Test]
        public void TheSwitch_IsOnByDefault() => Assert.That(MenuHelpValues.On, Is.True);

        [Test]
        public void TheWords_SayHowManyMore_AndNameHer()
        {
            Assert.That(MenuHelpRules.NotEnough(25, "Echoes", "Second Look"), Is.EqualTo("Not enough Echoes: 25 more needed for Second Look."));
            Assert.That(MenuHelpRules.Need(60, 45), Is.EqualTo(15));
            Assert.That(MenuHelpRules.Need(40, 45), Is.Zero, "she can pay it");
            Assert.That(MenuHelpRules.RefusedSlot("GUNSLINGER"), Is.EqualTo("Only the Gunslinger can be chosen for now: the other slots are characters to come."));
            Assert.That(MenuHelpRules.Title("GUNSLINGER"), Is.EqualTo("Gunslinger"));
        }

        [Test]
        public void TheShake_SwingsOnceAndDiesAway_AndTheFlashFades()
        {
            Assert.That(MenuHelpRules.Shake(0f), Is.EqualTo(0f).Within(1e-4f), "it starts at rest");
            float most = 0f;
            for (float t = 0f; t < MenuHelpValues.RefusalSeconds; t += 0.005f) most = System.Math.Max(most, System.Math.Abs(MenuHelpRules.Shake(t)));
            Assert.That(most, Is.GreaterThan(MenuHelpValues.ShakePixels * 0.5f).And.LessThanOrEqualTo(MenuHelpValues.ShakePixels));
            Assert.That(MenuHelpRules.Shake(MenuHelpValues.RefusalSeconds), Is.Zero);
            Assert.That(MenuHelpRules.Shake(-1f), Is.Zero);
            Assert.That(MenuHelpRules.Flash(0f), Is.EqualTo(1f));
            Assert.That(MenuHelpRules.Flash(MenuHelpValues.RefusalSeconds * 0.5f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(MenuHelpRules.Flash(MenuHelpValues.RefusalSeconds), Is.Zero);
        }
    }
}
