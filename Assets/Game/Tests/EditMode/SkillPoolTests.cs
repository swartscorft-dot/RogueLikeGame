using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 015 (2026-11-20): the pool of eight skills for the four free keys, the bar that keeps a skill and its key
    /// apart, Long Shot's perfect band, charges, Tag Round's tag, Breaker Slug's stagger per mark and the counter's front.
    /// </summary>
    public class SkillPoolTests
    {
        static SkillDefinition[] Kit => SkillDefaults.Create();
        static int Index(string name) => System.Array.FindIndex(Kit, s => s.Name == name);

        // ---------------- The skills ----------------

        [Test]
        public void TheKit_HasTwelveSkills_FourToStart_EightInThePool()
        {
            SkillDefinition[] kit = Kit;
            Assert.That(kit.Length, Is.EqualTo(15)); // changed deliberately (work order 109): its three after the twelve
            Assert.That(kit.Where(s => s.StartEquipped).Select(s => s.Name),
                        Is.EqualTo(new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" }));
            Assert.That(kit.Where(s => !s.StartEquipped).Select(s => s.Name), Is.EquivalentTo(new[]
            {
                "Powder Keg", "Fan Fire", "Long Shot", "Heavy Volley", "Tag Round", "Breaker Slug", "Slide", "Stopping Shot",
                "Ricochet", "Smoke Round", "Deadeye", // changed deliberately (work order 109): eleven in the pool
            }));
            foreach (SkillDefinition s in kit)
            {
                Assert.That(SkillDefaults.Line(s.Name), Is.Not.Empty, s.Name + " has its line for the reward screen");
                Assert.That(s.Charges, Is.GreaterThanOrEqualTo(1), s.Name);
            }
            Assert.That(SkillDefaults.MarkRole(kit[Index("Breaker Slug")]), Is.EqualTo("spends marks for stagger"));
            Assert.That(SkillDefaults.MarkRole(kit[Index("Tag Round")]), Is.EqualTo("builds marks"));
            Assert.That(SkillDefaults.MarkRole(kit[Index("Slide")]), Is.EqualTo("no marks"));
        }

        // ---------------- The bar ----------------

        [Test]
        public void TheBar_StartSkillsSitOnTheirOwnKeys_TheOthersAreEmpty()
        {
            var bar = new SkillLoadout();
            bar.Start(Kit, all: false);
            Assert.That(Enumerable.Range(0, SkillLoadout.KeyCount).Select(bar.SkillOn), Is.EqualTo(new[] { 0, -1, -1, -1, 4, -1, 6, 7 }));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(1));
            Assert.That(bar.Has(0) && bar.Has(4) && bar.Has(6) && bar.Has(7), Is.True);
            Assert.That(Enumerable.Range(0, 12).Count(bar.Has), Is.EqualTo(4));
        }

        [Test]
        public void TheBar_ATakenSkillGoesOnTheFirstFreeKey_AndStaysThere()
        {
            var bar = new SkillLoadout();
            bar.Start(Kit, all: false);
            int slide = Index("Slide"), tag = Index("Tag Round"), keg = Index("Powder Keg"), stop = Index("Stopping Shot");
            Assert.That(bar.Take(slide), Is.EqualTo(1), "the first free key: W's place");
            Assert.That(bar.Take(tag), Is.EqualTo(2));
            Assert.That(bar.Take(slide), Is.EqualTo(1), "taken again, it stays where it is");
            Assert.That(bar.Take(keg), Is.EqualTo(3));
            Assert.That(bar.Take(stop), Is.EqualTo(5));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(-1), "eight skills on eight keys");
            Assert.That(bar.SkillOn(1), Is.EqualTo(slide));
            Assert.That(bar.KeyOf(stop), Is.EqualTo(5));
            Assert.That(bar.Take(Index("Fan Fire")), Is.EqualTo(-1), "no key left: had, but on none");
            Assert.That(bar.Has(Index("Fan Fire")), Is.True);
        }

        [Test]
        public void TheBar_ANewRun_StartsFromTheStartFourAgain()
        {
            var bar = new SkillLoadout();
            bar.Start(Kit, all: false);
            bar.Take(Index("Slide"));
            bar.Start(Kit, all: false);
            Assert.That(bar.Has(Index("Slide")), Is.False);
            Assert.That(bar.SkillOn(1), Is.EqualTo(-1));
            Assert.That(bar.FirstFreeKey, Is.EqualTo(1));
        }

        [Test]
        public void TheBar_WithEverySkill_TheFirstEightSitOnTheKeysOfTheirPlaces()
        {
            var bar = new SkillLoadout();
            bar.Start(Kit, all: true);
            Assert.That(Enumerable.Range(0, SkillLoadout.KeyCount).Select(bar.SkillOn), Is.EqualTo(Enumerable.Range(0, 8)));
            Assert.That(Enumerable.Range(0, 12).All(bar.Has), Is.True);
        }

        // ---------------- The offer ----------------

        [Test]
        public void TheOffer_NeverHoldsASkillSheHas_AndIsTheSameForTheSameRun()
        {
            var bar = new SkillLoadout();
            bar.Start(Kit, all: false);
            var seen = new HashSet<int>();
            for (ulong run = 0; run < 200; run++)
            {
                List<bool> has = bar.HasList(12);
                List<int> offer = FloorRewards.Offer(run, 1, has, 3);
                Assert.That(offer.Count, Is.EqualTo(3));
                Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                foreach (int s in offer) Assert.That(has[s], Is.False, "never one she has");
                Assert.That(FloorRewards.Offer(run, 1, has, 3), Is.EqualTo(offer), "the same run, the same offer");
                seen.UnionWith(offer);
            }
            Assert.That(seen.OrderBy(s => s), Is.EqualTo(new[] { 1, 2, 3, 5, 8, 9, 10, 11 }), "drawn out of the eight in the pool");
        }

        // ---------------- The perfect band ----------------

        [TestCase(0.5f, false)]
        [TestCase(0.79f, false)]
        [TestCase(0.8f, true)]
        [TestCase(0.9f, true)]
        [TestCase(1.0f, true)]
        [TestCase(1.01f, false)]
        [TestCase(2.5f, false)]
        public void LongShot_PerfectBand_From08To10Seconds(float charged, bool perfect)
        {
            SkillDefinition longShot = Kit[Index("Long Shot")];
            Assert.That(PoolRules.IsPerfect(longShot, charged), Is.EqualTo(perfect));
        }

        [Test]
        public void PerfectBand_ASkillWithNone_IsNeverPerfect_AndThePerfectDamageIsTwiceTheFullCharge()
        {
            Assert.That(PoolRules.IsPerfect(Kit[Index("Quick Shot")], 0.9f), Is.False);
            SkillDefinition longShot = Kit[Index("Long Shot")];
            longShot.PerfectFrom = longShot.PerfectTo = 0f;
            Assert.That(PoolRules.IsPerfect(longShot, 0.9f), Is.False);
            SkillDefinition fresh = Kit[Index("Long Shot")];
            Assert.That(PoolRules.PerfectDamage(fresh), Is.EqualTo(2 * SkillRules.Damage(fresh, 1f)));
        }

        [Test]
        public void SkillCast_ChargedSeconds_StopsAtTheRelease_AndKnowsALetGoFromARunOut()
        {
            SkillDefinition longShot = Kit[Index("Long Shot")];
            var cast = new SkillCast();
            cast.Start(longShot, 10f);
            cast.Release(10.9f);
            Assert.That(cast.ChargedSeconds(11.5f), Is.EqualTo(0.9f).Within(1e-4f));
            Assert.That(cast.ReleasedByKey, Is.True);
            var held = new SkillCast();
            held.Start(longShot, 10f);
            Assert.That(held.ChargedSeconds(20f), Is.EqualTo(longShot.HoldMax).Within(1e-4f), "it fires by itself at HoldMax");
            Assert.That(held.ReleasedByKey, Is.False);
        }

        // ---------------- Charges ----------------

        [Test]
        public void Charges_TwoUses_ThenNone_OneComesBackAtATime()
        {
            var c = new Cooldown { MaxCharges = 2 };
            Assert.That(c.Charges(0f), Is.EqualTo(2));
            c.Start(0f, 12f);
            Assert.That(c.Charges(0f), Is.EqualTo(1));
            Assert.That(c.IsReady(0f), Is.True);
            c.Start(1f, 12f);
            Assert.That(c.Charges(1f), Is.EqualTo(0));
            Assert.That(c.IsReady(1f), Is.False);
            Assert.That(c.Remaining(1f), Is.EqualTo(11f).Within(1e-4f), "the first comes back 12 s after the first use");
            Assert.That(c.Charges(12f), Is.EqualTo(1));
            Assert.That(c.NextCharge(12f), Is.EqualTo(12f).Within(1e-4f), "the second only then starts its 12 s");
            Assert.That(c.Charges(23.9f), Is.EqualTo(1));
            Assert.That(c.Charges(24f), Is.EqualTo(2));
            Assert.That(c.NextCharge(24f), Is.EqualTo(0f));
        }

        [Test]
        public void Charges_OneCharge_IsThePlainCooldown_AndTheCutCutsTheRechargeRunning()
        {
            var c = new Cooldown();
            c.Start(0f, 5f);
            Assert.That(c.IsReady(4.9f), Is.False);
            Assert.That(c.Remaining(2f), Is.EqualTo(3f).Within(1e-4f));
            Assert.That(c.Cut(2f, 0.4f), Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(c.IsReady(3.8f), Is.True);
            var two = new Cooldown { MaxCharges = 2 };
            two.Start(0f, 10f);
            Assert.That(two.Cut(5f, 0.5f), Is.EqualTo(2.5f).Within(1e-4f));
            Assert.That(two.Charges(7.5f), Is.EqualTo(2));
        }

        // ---------------- Tag Round ----------------

        [Test]
        public void Tag_AddsItsShare_ForItsTime_AndANewTagStartsItAgain()
        {
            var tag = new Tag();
            Assert.That(tag.Scale(100, 0f), Is.EqualTo(100), "untagged");
            tag.Apply(0f, 8f, 0.1f);
            Assert.That(tag.Scale(100, 0f), Is.EqualTo(110));
            Assert.That(tag.Scale(300, 7.9f), Is.EqualTo(330));
            Assert.That(tag.Scale(100, 8f), Is.EqualTo(100), "gone after 8 s");
            tag.Apply(0f, 8f, 0.1f);
            tag.Apply(6f, 8f, 0.1f);
            Assert.That(tag.Scale(100, 13.9f), Is.EqualTo(110), "the second tag started the 8 s again");
            Assert.That(tag.Scale(100, 14f), Is.EqualTo(100));
            tag.Apply(20f, 8f, 0.1f);
            tag.Apply(20f, 8f, 0.1f);
            Assert.That(tag.Scale(100, 21f), Is.EqualTo(110), "it does not stack");
        }

        // ---------------- Breaker Slug ----------------

        [Test]
        public void BreakerSlug_ThirtyPercentMoreStaggerPerMark_NoMoreDamage()
        {
            SkillDefinition slug = Kit[Index("Breaker Slug")];
            Assert.That(PoolRules.SpenderStagger(slug.Stagger, 0, slug.MarkStaggerBonus), Is.EqualTo(80f).Within(1e-3f));
            Assert.That(PoolRules.SpenderStagger(slug.Stagger, 1, slug.MarkStaggerBonus), Is.EqualTo(104f).Within(1e-3f));
            Assert.That(PoolRules.SpenderStagger(slug.Stagger, 5, slug.MarkStaggerBonus), Is.EqualTo(200f).Within(1e-3f));
            Assert.That(MarkRules.Spends(slug, false), Is.True, "it takes the marks");
            Assert.That(MarkRules.SpenderDamage(slug.Damage, 5, MarkRules.BonusPerMark(slug, false)), Is.EqualTo(slug.Damage), "no more damage");
        }

        // ---------------- The counter ----------------

        [TestCase(0f, true)]
        [TestCase(45f, true)]
        [TestCase(59.9f, true)]
        [TestCase(60.5f, false)]
        [TestCase(90f, false)]
        [TestCase(180f, false)]
        [TestCase(-59.9f, true)]
        [TestCase(-75f, false)]
        public void Counter_SheMustBeWithinSixtyDegreesOfWhereItFaces(float degrees, bool inFront)
        {
            var enemy = new GroundPoint(3f, 4f);
            var facing = new GroundPoint(0f, 1f);
            double r = degrees * System.Math.PI / 180.0;
            var her = new GroundPoint(enemy.X + (float)(5 * System.Math.Sin(r)), enemy.Z + (float)(5 * System.Math.Cos(r)));
            Assert.That(PoolRules.InFront(enemy, facing, her, 60f), Is.EqualTo(inFront));
        }

        [Test]
        public void Counter_TheBrutesShockwaveAndTheGuardsSweepAreCounterable_TheSlamsAreNot()
        {
            EnemyAttackDefinition[] brute = EnemyValues.DefaultAttacks();
            Assert.That(PoolRules.IsCounterable(brute.Single(a => a.Name == "Shockwave")), Is.True);
            Assert.That(PoolRules.IsCounterable(brute.Single(a => a.Name == "Slam")), Is.False);
            EnemyAttackDefinition[] guard = new GuardValues().Enemy.Attacks;
            Assert.That(PoolRules.IsCounterable(guard.Single(a => a.Name == "Sweep")), Is.True);
            Assert.That(guard.Single(a => a.Name == "Sweep").Counterable, Is.True, "marked as data");
            Assert.That(new EnemyAttackDefinition().Counterable, Is.False, "off by default");
        }
    }
}
