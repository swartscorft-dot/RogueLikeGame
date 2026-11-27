using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Tests
{
    /// <summary>
    /// Work order 146 (Kristian, 2026-11-27: "bossen stod fast mellom en trang path. så det var grise enkelt"): the boss's charge, its
    /// line cut short of her at the end of the walkable ground where its sight is clear, is not begun; it walks to her instead
    /// (<see cref="BossTactics.ChargeReachesHer"/>, <see cref="BossValues.NeverParked"/>).
    /// </summary>
    public class BossNeverParkedRulesTests
    {
        static EnemyAttackDefinition Charge(BossValues v)
        {
            foreach (EnemyAttackDefinition a in v.Enemy.Attacks) if (a.Name == BossValues.Charge) return a;
            return null;
        }

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(BossValues.NeverParked, Is.True);
        }

        [Test]
        public void AnUncutCharge_IsBegunAsBefore_WhereverSheIsInItsBand()
        {
            var v = new BossValues();
            EnemyAttackDefinition c = Charge(v);
            for (float d = c.MinDistance; d <= c.MaxDistance; d += 0.5f)
                Assert.That(BossTactics.ChargeReachesHer(c.Length, c.Length, d, c.HalfWidth, Placeholders.PlayerRadius), Is.True, d + " m, the line not cut");
        }

        [Test]
        public void ACutCharge_IsBegunOnlyWhenItsLinePassesOverHer()
        {
            var v = new BossValues();
            EnemyAttackDefinition c = Charge(v);
            float slack = c.HalfWidth + Placeholders.PlayerRadius;
            Assert.That(BossTactics.ChargeReachesHer(c.Length, 9f, 10f, c.HalfWidth, Placeholders.PlayerRadius), Is.True, "cut 1 m short of her middle: its strip still covers her");
            Assert.That(BossTactics.ChargeReachesHer(c.Length, 10f - slack, 10f, c.HalfWidth, Placeholders.PlayerRadius), Is.True, "its end's edge touches her edge");
            Assert.That(BossTactics.ChargeReachesHer(c.Length, 10f - slack - 0.01f, 10f, c.HalfWidth, Placeholders.PlayerRadius), Is.False, "a hair shorter: it would stop short of her");
            Assert.That(BossTactics.ChargeReachesHer(c.Length, 0f, 10f, c.HalfWidth, Placeholders.PlayerRadius), Is.False, "his fight: a charge cut to nothing at a pier's corner");
        }

        [Test]
        public void TheChoice_WithTheChargeHeldBack_WalksWhereItWouldHaveCharged_AndTheCloseAttacksAreAsBefore()
        {
            var v = new BossValues();
            for (float d = 0f; d <= 17f; d += 0.25f)
            {
                EnemyAttackDefinition before = BossTactics.Choose(d, true, null, v.Enemy.Attacks);
                EnemyAttackDefinition held = BossTactics.Choose(d, true, false, null, v.Enemy.Attacks);
                if (before != null && before.Rush > 0f) Assert.That(held, Is.Null, d + " m: the charge held back, it walks");
                else Assert.That(held, Is.SameAs(before), d + " m: as before");
                Assert.That(BossTactics.Choose(d, true, true, null, v.Enemy.Attacks), Is.SameAs(before), d + " m: a charge that reaches her, as before");
            }
        }
    }
}
