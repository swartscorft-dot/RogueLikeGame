using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 024 (2026-11-20), the effects as rules: the limits (the cap on live pieces, which goes first, what never
    /// goes, the cap per frame); which moment is drawn with which shapes, as a table; sizes by the enemy's size and by the
    /// marks spent; the boss's dust every half metre. Nobody can see here whether they look right.
    /// </summary>
    public class CombatFxRulesTests
    {
        // ---------------- The limits ----------------

        static FxBudget Budget(int most = 4) => new FxBudget(new FxLimits { MostLive = most });

        [Test]
        public void TheCap_NoMoreLiveThanItsMost()
        {
            FxBudget b = Budget(160);
            var dropped = new List<int>();
            for (int i = 0; i < 500; i++) b.Admit(i, FxTier.Plain, 1f, dropped);
            Assert.That(b.Live, Is.EqualTo(160));
            Assert.That(dropped.Count, Is.EqualTo(340), "one out for each one in past the cap");
            Assert.That(new FxLimits().MostLive, Is.EqualTo(160), "the order's first guess");
        }

        [Test]
        public void PastTheCap_TheSmallestGoesFirst_AndOfTheSameSize_TheOldest()
        {
            FxBudget b = Budget(4);
            var dropped = new List<int>();
            b.Admit(1, FxTier.Plain, 2f, dropped);
            b.Admit(2, FxTier.Plain, 0.5f, dropped);
            b.Admit(3, FxTier.Plain, 0.5f, dropped);
            b.Admit(4, FxTier.Plain, 3f, dropped);
            Assert.That(b.Admit(5, FxTier.Plain, 1f, dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(new[] { 2 }), "the smallest, and of the two smallest the older");
            Assert.That(b.Admit(6, FxTier.Plain, 1f, dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(new[] { 2, 3 }));
            Assert.That(b.Admit(7, FxTier.Plain, 4f, dropped), Is.True);
            Assert.That(dropped.Last(), Is.EqualTo(5), "of the two of 1 m, the older");
            Assert.That(b.IsLive(1) && b.IsLive(4) && b.IsLive(6) && b.IsLive(7), Is.True);
        }

        [Test]
        public void PastTheCap_AMinorGoesBeforeAPlain_WhateverItsSize()
        {
            FxBudget b = Budget(2);
            var dropped = new List<int>();
            b.Admit(1, FxTier.Plain, 0.1f, dropped);
            b.Admit(2, FxTier.Minor, 5f, dropped);
            Assert.That(b.Admit(3, FxTier.Plain, 1f, dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(new[] { 2 }));
        }

        [Test]
        public void ANewPieceLessThanAllLive_IsNotLetIn()
        {
            FxBudget b = Budget(2);
            var dropped = new List<int>();
            b.Admit(1, FxTier.Plain, 1f, dropped);
            b.Admit(2, FxTier.Plain, 1f, dropped);
            Assert.That(b.Admit(3, FxTier.Minor, 9f, dropped), Is.False, "a minor one does not push out a plain one");
            Assert.That(b.Admit(4, FxTier.Plain, 0.5f, dropped), Is.False, "nor a smaller one a larger");
            Assert.That(dropped, Is.Empty);
            Assert.That(b.Live, Is.EqualTo(2));
        }

        [Test]
        public void AKeyPiece_IsNeverDropped_AndComesInPastTheCap_WhenOnlyKeyOnesLive()
        {
            FxBudget b = Budget(3);
            var dropped = new List<int>();
            b.Admit(1, FxTier.Key, 0.1f, dropped);
            b.Admit(2, FxTier.Key, 0.1f, dropped);
            b.Admit(3, FxTier.Minor, 1f, dropped);
            Assert.That(b.Admit(4, FxTier.Key, 0.1f, dropped), Is.True);
            Assert.That(dropped, Is.EqualTo(new[] { 3 }), "the minor one goes, not a key one");
            for (int i = 5; i < 50; i++) Assert.That(b.Admit(i, FxTier.Plain, 10f, dropped), Is.False, "nothing can go: a plain one is not let in");
            Assert.That(b.Admit(50, FxTier.Key, 0.1f, dropped), Is.True, "a key one is, past the cap");
            Assert.That(b.Live, Is.EqualTo(4));
            Assert.That(b.IsLive(1) && b.IsLive(2) && b.IsLive(4) && b.IsLive(50), Is.True, "every key piece still live");
        }

        [Test]
        public void APieceThatGoes_FreesItsPlace()
        {
            FxBudget b = Budget(2);
            var dropped = new List<int>();
            b.Admit(1, FxTier.Plain, 1f, dropped);
            b.Admit(2, FxTier.Plain, 1f, dropped);
            b.Gone(1);
            Assert.That(b.Live, Is.EqualTo(1));
            Assert.That(b.Admit(3, FxTier.Minor, 0.1f, dropped), Is.True, "room again");
            Assert.That(dropped, Is.Empty);
            b.Gone(99); // unknown: nothing happens
            Assert.That(b.Live, Is.EqualTo(2));
        }

        [Test]
        public void ThePerFrameCap_LetsThroughSoManyAFrame_AgainTheNextFrame_AndAKeyOneAlways()
        {
            var limits = new FxLimits();
            var b = new FxBudget(limits);
            int let = 0;
            for (int i = 0; i < 30; i++) if (b.MayAsk(FxMoment.MarkAdded, 7, FxTier.Minor)) let++;
            Assert.That(let, Is.EqualTo(limits.MarksAddedPerFrame), "mark pops");
            let = 0;
            for (int i = 0; i < 30; i++) if (b.MayAsk(FxMoment.WindupStarted, 7, FxTier.Minor)) let++;
            Assert.That(let, Is.EqualTo(limits.WindupsPerFrame), "windups, counted apart");
            let = 0;
            for (int i = 0; i < 30; i++) if (b.MayAsk(FxMoment.StrikeHitHer, 7, FxTier.Plain)) let++;
            Assert.That(let, Is.EqualTo(limits.HitsOnHerPerFrame), "hit sparks on her");
            Assert.That(b.MayAsk(FxMoment.MarkAdded, 8, FxTier.Minor), Is.True, "a new frame");
            for (int i = 0; i < 30; i++) Assert.That(b.MayAsk(FxMoment.Countered, 8, FxTier.Key), Is.True, "a counter is never capped");
            for (int i = 0; i < 30; i++) Assert.That(b.MayAsk(FxMoment.MarksSpent, 8, FxTier.Key), Is.True, "nor five marks spent");
            Assert.That(limits.PerFrame(FxMoment.BossAngered), Is.EqualTo(0), "a rare moment has no cap");
        }

        [Test]
        public void TheKeyMoments_AreACounter_TheBossesAngerAndDeath_AndMarksSpentAtTheCap()
        {
            foreach (FxMoment m in CombatFxRules.All)
            {
                bool key = m == FxMoment.Countered || m == FxMoment.BossAngered || m == FxMoment.BossDied;
                Assert.That(CombatFxRules.TierOf(m, 1, 5) == FxTier.Key, Is.EqualTo(key), m.ToString());
            }
            Assert.That(CombatFxRules.TierOf(FxMoment.MarksSpent, 5, 5), Is.EqualTo(FxTier.Key), "five spent");
            Assert.That(CombatFxRules.TierOf(FxMoment.MarksSpent, 4, 5), Is.EqualTo(FxTier.Plain), "four spent");
            Assert.That(CombatFxRules.TierOf(FxMoment.MarkAdded), Is.EqualTo(FxTier.Minor), "it happens constantly");
            Assert.That(CombatFxRules.TierOf(FxMoment.WindupStarted), Is.EqualTo(FxTier.Minor));
        }

        // ---------------- Which moment is drawn with what ----------------

        [Test]
        public void EachMoment_IsDrawnWithItsShapes()
        {
            var table = new Dictionary<FxMoment, FxShape[]>
            {
                { FxMoment.EnemyStruck, new[] { FxShape.Ring, FxShape.Spark, FxShape.Wedge, FxShape.Streak, FxShape.SideSpikes } },
                { FxMoment.StrikeHitHer, new[] { FxShape.Spark } },
                { FxMoment.WindupStarted, new[] { FxShape.Converge } },
                { FxMoment.EnemyDied, new[] { FxShape.Spark, FxShape.Shards } },
                { FxMoment.BodyHidden, new[] { FxShape.Outline } },
                { FxMoment.EnemyAppeared, new[] { FxShape.Closing, FxShape.Spike } },
                { FxMoment.EnemyDazed, new[] { FxShape.Orbit } },
                { FxMoment.ArrowLoosed, new[] { FxShape.Streak } },
                { FxMoment.BossChargeStep, new[] { FxShape.Wedge, FxShape.Streak } },
                { FxMoment.BossChargeEnded, new[] { FxShape.Ring } },
                { FxMoment.BossAngered, new[] { FxShape.Ring, FxShape.Spike } },
                { FxMoment.BossCalled, new[] { FxShape.Ring } },
                { FxMoment.BossDied, new[] { FxShape.Spark, FxShape.Shards, FxShape.Ring } },
                { FxMoment.TagRoundTagged, new[] { FxShape.Diamond } },
                { FxMoment.BreakerSlugHit, new[] { FxShape.Ring } },
                { FxMoment.SlideStarted, new[] { FxShape.Wedge } },
                { FxMoment.SlideEnded, new[] { FxShape.Streak } },
                { FxMoment.Countered, new[] { FxShape.Spark, FxShape.Ring } },
                { FxMoment.PerfectRelease, new[] { FxShape.Ring, FxShape.Spark } },
                { FxMoment.MarksSpent, new[] { FxShape.Spark, FxShape.Spike } },
                { FxMoment.MarkAdded, new[] { FxShape.Spark } },
                { FxMoment.KegLying, new[] { FxShape.Closing } },
                { FxMoment.KegSetOffByShot, new[] { FxShape.Spark, FxShape.Ring } },
                { FxMoment.Dashed, new[] { FxShape.Flash, FxShape.Streak } },
                { FxMoment.HerHurt, new[] { FxShape.Spark } },
                { FxMoment.ChestOpened, new[] { FxShape.Spark, FxShape.Ring } },
                { FxMoment.ChoiceTaken, new[] { FxShape.Ring, FxShape.Spark } },
            };
            foreach (FxMoment m in CombatFxRules.All)
            {
                Assert.That(table.ContainsKey(m), Is.True, m + " is in the table");
                Assert.That(CombatFxRules.ShapesOf(m), Is.EquivalentTo(table[m]), m.ToString());
            }
            foreach (FxShape s in Enum.GetValues(typeof(FxShape)))
                Assert.That(CombatFxRules.All.Any(m => CombatFxRules.ShapesOf(m).Contains(s)), Is.True, s + " is used");
        }

        [Test]
        public void AStrike_ACircleIsARingAndAStar_AConeAWedge_ALineAStreakWithSpikesOffItsSides()
        {
            Assert.That(CombatFxRules.StrikeShapes(EnemyAttackShape.Circle), Is.EqualTo(new[] { FxShape.Ring, FxShape.Spark }));
            Assert.That(CombatFxRules.StrikeShapes(EnemyAttackShape.Cone), Is.EqualTo(new[] { FxShape.Wedge }));
            Assert.That(CombatFxRules.StrikeShapes(EnemyAttackShape.Line), Is.EqualTo(new[] { FxShape.Streak, FxShape.SideSpikes }));
            foreach (EnemyAttackShape a in Enum.GetValues(typeof(EnemyAttackShape)))
                foreach (FxShape s in CombatFxRules.StrikeShapes(a))
                    Assert.That(CombatFxRules.ShapesOf(FxMoment.EnemyStruck), Does.Contain(s));
        }

        [Test]
        public void AnArrival_IsDrawnOnlyInAFight()
        {
            Assert.That(CombatFxRules.AppearsInAFight(true), Is.True);
            Assert.That(CombatFxRules.AppearsInAFight(false), Is.False, "a floor's enemies made as she comes in: no");
        }

        [Test]
        public void TheChargesDust_EveryHalfMetre()
        {
            Assert.That(CombatFxRules.DustDue(0.2f, 0), Is.EqualTo(0));
            Assert.That(CombatFxRules.DustDue(0.5f, 0), Is.EqualTo(1));
            Assert.That(CombatFxRules.DustDue(2.3f, 1), Is.EqualTo(3), "four due by 2.3 m, one out already");
            Assert.That(CombatFxRules.DustDue(2.3f, 4), Is.EqualTo(0));
            Assert.That(CombatFxRules.DustDue(-1f, 0), Is.EqualTo(0), "backwards: none");
        }

        // ---------------- Sizes ----------------

        [Test]
        public void SizesByTheEnemy_SmallForASwarmer_LargerForAGuard_LargestForTheBoss()
        {
            var s = new FxSizes();
            Assert.That(s.KindScale(EnemyKind.Brute), Is.EqualTo(1f));
            Assert.That(s.KindScale(EnemyKind.Swarmer), Is.LessThan(s.KindScale(EnemyKind.Shooter)));
            Assert.That(s.KindScale(EnemyKind.Shooter), Is.LessThan(s.KindScale(EnemyKind.Brute)));
            Assert.That(s.KindScale(EnemyKind.Guard), Is.GreaterThan(s.KindScale(EnemyKind.Brute)));
            Assert.That(s.KindScale(EnemyKind.Boss), Is.GreaterThan(s.KindScale(EnemyKind.Guard)));
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind)))
                Assert.That(s.ShardCount(k), Is.InRange(5, 8), k + ": five to eight shards");
            Assert.That(s.ShardCount(EnemyKind.Swarmer), Is.LessThan(s.ShardCount(EnemyKind.Guard)));
        }

        [Test]
        public void MarksSpent_MoreAndLargerStarsWithMoreMarks_FiveUnmistakable()
        {
            var s = new FxSizes();
            Assert.That(s.MarkStars(0, 5), Is.EqualTo(0));
            Assert.That(s.MarkStarSize(0, 5), Is.EqualTo(0f));
            for (int m = 1; m < 5; m++)
            {
                Assert.That(s.MarkStars(m, 5), Is.EqualTo(m), m + ": one star a pip");
                Assert.That(s.MarkStarSize(m + 1, 5), Is.GreaterThan(s.MarkStarSize(m, 5)), m + " to " + (m + 1) + ": larger");
            }
            Assert.That(s.MarkStars(5, 5), Is.EqualTo(10), "at the cap twice as many");
            Assert.That(s.MarkStarSize(5, 5), Is.GreaterThan(s.MarkStarSize(4, 5) * 1.4f), "and much larger");
        }

        [Test]
        public void HerHurtSpark_LargerForABiggerHit_UpToItsMost()
        {
            var s = new FxSizes();
            Assert.That(s.HurtSize(0, 100), Is.EqualTo(s.HurtSmallest));
            Assert.That(s.HurtSize(30, 100), Is.GreaterThan(s.HurtSize(5, 100)));
            Assert.That(s.HurtSize(25, 100), Is.EqualTo(s.HurtLargest).Within(1e-5f), "a quarter of her health: the most");
            Assert.That(s.HurtSize(1000, 100), Is.EqualTo(s.HurtLargest).Within(1e-5f));
        }
    }
}
