using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// "Only when seen" for the warder's bolt and the lancer's charge (work order 140, part 2; Kristian: "Ja, begge"), as rules: the switch
    /// is on; the screen's part of the ranged rule (<see cref="RangedOpeningRules.MayBeginSeen"/>) is the one the shooter's and the
    /// caster's draw is asked through (<see cref="RangedOpeningRules.MayBegin"/>), the same margin and switch, with no grace at the waking.
    /// The play is WarderAndLancerOnScreenPlayTests'.
    /// </summary>
    public class WarderAndLancerOnScreenRulesTests
    {
        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(RangedOpeningRules.WarderAndLancerToo, Is.True, "on by default (his tick: \"Ja, begge\")");
        }

        [Test]
        public void TheScreensPart_IsTheShootersOwn_WithoutItsGrace()
        {
            var r = new RangedAroundValues();
            Assert.That(r.OnScreenOnly, Is.True);
            Assert.That(RangedOpeningRules.MayBeginSeen(r, true), Is.True, "on screen: it may");
            Assert.That(RangedOpeningRules.MayBeginSeen(r, false), Is.False, "off screen: it may not");
            Assert.That(RangedOpeningRules.MayBeginSeen(null, false), Is.True, "no rule for the fight (its switch off, no room's fight): as before");
            var screenOff = new RangedAroundValues { OnScreenOnly = false };
            Assert.That(RangedOpeningRules.MayBeginSeen(screenOff, false), Is.True, "the screen's part turned off: as before");
            // The shooter's own ask, out of its grace, says the same in every case: one rule, not a copy.
            foreach (bool seen in new[] { true, false })
            foreach (RangedAroundValues v in new[] { r, screenOff, null })
                Assert.That(RangedOpeningRules.MayBegin(v, seen, 0f, 100f, true), Is.EqualTo(RangedOpeningRules.MayBeginSeen(v, seen)));
            // No grace for them: in the shooter's first second it may not begin, the screen's part alone lets it.
            Assert.That(RangedOpeningRules.MayBegin(r, true, 0f, 0.5f, true), Is.False, "the shooter in its grace");
            Assert.That(RangedOpeningRules.MayBeginSeen(r, true), Is.True, "the warder or the lancer at the same moment");
        }
    }
}
