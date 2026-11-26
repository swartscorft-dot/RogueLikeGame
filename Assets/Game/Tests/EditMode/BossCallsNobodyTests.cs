using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The boss calls nobody (work order 140, part 1; Kristian, 2026-11-26, asked whether the boss alone on floor 5 should still call its
    /// packs of six at two thirds and one third of its health, ticked "Ingen flokker"): with <see cref="BossValues.NoPacks"/> on it calls
    /// no pack at any share of its health, however it falls; its anger is as before; off, it calls as before (BossRulesTests' own test of
    /// the calls, with the switch named off). The shares and the count stay as they were, so the switch alone gives the old fight back.
    /// </summary>
    public class BossCallsNobodyTests
    {
        bool _before;

        [SetUp]
        public void Keep() => _before = BossValues.NoPacks;

        [TearDown]
        public void PutBack() => BossValues.NoPacks = _before;

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheOldNumbersStay()
        {
            Assert.That(BossValues.NoPacks, Is.True, "on by default (his tick: \"Ingen flokker\")");
            var v = new BossValues();
            Assert.That(v.CallsAt, Is.EqualTo(new[] { 0.66f, 0.33f }), "the shares are left as they were: the switch alone turns the calls off");
            Assert.That(v.CallCount, Is.EqualTo(6));
        }

        [Test]
        public void On_ItCallsNoPack_AtAnyShareOfItsHealth_StepByStepOrAtOnce()
        {
            BossValues.NoPacks = true;
            var v = new BossValues();
            var t = new BossTactics();
            for (float share = 1f; share >= 0f; share -= 0.01f) Assert.That(t.CallsDue(share, v), Is.EqualTo(0), "at " + share);
            Assert.That(t.CallsDue(0f, v), Is.EqualTo(0));
            Assert.That(t.Calls, Is.EqualTo(0), "none counted");
            var once = new BossTactics();
            Assert.That(once.CallsDue(0.05f, v), Is.EqualTo(0), "one hit past both shares calls none");
            Assert.That(once.Calls, Is.EqualTo(0));
        }

        [Test]
        public void On_ItsAnger_IsAsBefore()
        {
            BossValues.NoPacks = true;
            var v = new BossValues();
            var t = new BossTactics();
            Assert.That(t.BecomesAngry(0.51f, v), Is.False);
            Assert.That(t.BecomesAngry(0.49f, v), Is.True, "under half: angry, once");
            Assert.That(t.BecomesAngry(0.2f, v), Is.False);
        }

        [Test]
        public void Off_ItCallsAsBefore_TwoPacks_OnceEach()
        {
            BossValues.NoPacks = false;
            var v = new BossValues();
            var t = new BossTactics();
            Assert.That(t.CallsDue(0.67f, v), Is.EqualTo(0));
            Assert.That(t.CallsDue(0.66f, v), Is.EqualTo(1));
            Assert.That(t.CallsDue(0.33f, v), Is.EqualTo(1));
            Assert.That(t.Calls, Is.EqualTo(2));
            Assert.That(new BossTactics().CallsDue(0.2f, v), Is.EqualTo(2), "one hit past both calls both");
        }
    }
}
