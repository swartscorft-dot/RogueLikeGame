using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 055 (the fourth review of 2026-11-21), the faults that are rules: a reward drop whose every place is
    /// refused has no landing, never a refused point; a clean spend's sure crit is folded, so Backstep deals at the start
    /// what it dealt before the stats; the summary measures the basic shot against her real interval when the log has it;
    /// a chest's draw tries the tiers above when the rolled one and common have nothing left.
    /// </summary>
    public class ReviewFix4RulesTests
    {
        static readonly RewardDropValues V = new RewardDropValues();

        // ---------------- 5. A reward with nowhere to land ----------------

        [Test]
        public void ADrop_WhoseEveryPlaceIsRefused_HasNoLanding_NotARefusedPoint()
        {
            var fell = new GroundPoint(4f, 3f);
            var middle = new GroundPoint(6f, 5f);
            bool found = RewardDropRules.TryLanding(fell, middle, _ => false, (a, b) => true, V, out GroundPoint _);
            Assert.That(found, Is.False, "nothing she can stand at: no landing");

            // Only the middle can be stood at, and nothing round it: the middle, and it is taken.
            found = RewardDropRules.TryLanding(fell, middle, p => p.DistanceTo(middle) < 1e-3f, (a, b) => true, V, out GroundPoint at);
            Assert.That(found, Is.True);
            Assert.That(at.DistanceTo(middle), Is.LessThan(1e-3f));

            // Every ring refused and the start refused: never the start given back.
            var refused = new HashSet<GroundPoint>();
            found = RewardDropRules.TryLanding(fell, middle, p => { refused.Add(p); return false; }, (a, b) => true, V, out GroundPoint _);
            Assert.That(found, Is.False);
            Assert.That(refused.Count, Is.GreaterThan(10), "every ring was asked");
        }

        [Test]
        public void ADrop_WithAPlaceRoundIt_LandsThere_AsBefore()
        {
            var fell = new GroundPoint(4f, 3f);
            var middle = new GroundPoint(6f, 5f);
            bool Standable(GroundPoint p) => p.X > 0.5f && p.Z > 0.5f && p.X < 11.5f && p.Z < 9.5f;
            Assert.That(RewardDropRules.TryLanding(fell, middle, Standable, (a, b) => true, V, out GroundPoint at), Is.True);
            Assert.That(at.X, Is.EqualTo(RewardDropRules.Landing(fell, middle, Standable, (a, b) => true, V).X).Within(1e-6f), "the same place as Landing");
            Assert.That(Standable(at), Is.True);
        }

        // ---------------- 4. A clean Backstep ----------------

        [Test]
        public void ACleanSpend_SureCrit_IsFolded_SoAtTheStartItDealsWhatItDealt_AndACritDamageStepRaisesIt()
        {
            SkillDefinition step = SkillDefaults.Create().Single(s => s.Name == "Backstep");
            Assume.That(MarkRules.HasCleanSpend(step), Is.True);
            var v = new StatValues();
            foreach (int marks in new[] { 0, 1, 3, 5 })
            {
                int before = MarkRules.SpenderDamage(step.Damage, marks, step.CleanMarkDamageBonus); // the stats off
                Assert.That(CritRules.ApplyFolded(before, v.StartCritDamage, v), Is.EqualTo(before), marks + " marks: as before the stats");
                Assert.That(CritRules.Apply(before, v.StartCritDamage), Is.GreaterThan(before), marks + " marks: the plain crit paid 1.5 on top (the fault)");
                Assert.That(CritRules.ApplyFolded(before, v.StartCritDamage + 0.15f, v), Is.GreaterThan(before), marks + " marks: a crit damage step raises it");
            }
            Assert.That(CritRules.ApplyFolded(80, v.StartCritDamage + 0.15f, v), Is.EqualTo(88), "80 times 1.65 over 1.5");
        }

        // ---------------- 8. The basic shot's interval in the summary ----------------

        static List<SessionEvent> ContinuousShots(float interval, bool withInterval)
        {
            var feel = new FeelValues();
            var start = new SessionEvent(0d, SessionEventTypes.SessionStart);
            start.AddExtra("feel.BasicShotStartup", feel.BasicShotStartup.ToString(CultureInfo.InvariantCulture));
            start.AddExtra("feel.BasicShotActive", feel.BasicShotActive.ToString(CultureInfo.InvariantCulture));
            start.AddExtra("feel.BasicShotRecovery", feel.BasicShotRecovery.ToString(CultureInfo.InvariantCulture));
            var events = new List<SessionEvent> { start };
            if (withInterval)
            {
                var e = new SessionEvent(0d, SessionEventTypes.ShotInterval);
                e.AddExtra("seconds", interval.ToString("0.#####", CultureInfo.InvariantCulture));
                events.Add(e);
            }
            for (int k = 0; k * (double)interval <= 20d; k++) events.Add(new SessionEvent(k * (double)interval, SessionEventTypes.ShotStart, SessionWords.BasicShot));
            events.Add(new SessionEvent(20d, SessionEventTypes.SessionEnd));
            return events;
        }

        [Test]
        public void TheSummary_MeasuresTheBasicShot_AgainstHerRealInterval_WhenTheLogHasIt()
        {
            var feel = new FeelValues();
            var stats = new HerStats(new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() });
            float interval = float.Parse(stats.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery)
                                              .ToString("0.#####", CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
            Assume.That(interval, Is.GreaterThan(feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery + 0.01f), "a run starts slower");

            ActionRow shot = SessionSummary.Compute(ContinuousShots(interval, true)).Session.Row(SessionWords.BasicShot);
            Assert.That(shot.CooldownUse, Is.EqualTo(1f).Within(0.02f), "a shot every interval of hers: all she could");
            Assert.That(shot.Cooldown, Is.EqualTo(interval).Within(1e-5f), "her interval, not the feel's");

            ActionRow old = SessionSummary.Compute(ContinuousShots(interval, false)).Session.Row(SessionWords.BasicShot);
            float feelTime = feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery;
            int allowed = 1 + (int)System.Math.Floor(20d / feelTime + 1e-4);
            Assert.That(old.CooldownUse, Is.EqualTo(shot.Started / (float)allowed).Within(1e-4f), "an old log without it, read as before");
        }

        // ---------------- 12. A chest's tiers ----------------

        [Test]
        public void AChestsDraw_TriesTheTiersAbove_WhenTheRolledOneAndCommonHaveNothingLeft()
        {
            var values = new RunValues
            {
                StatsAndTiers = true,
                UpgradesOffered = 2,
                TieredUpgrades = new[] { new UpgradeDefinition(UpgradeKind.Damage, 0.15f, minTier: UpgradeTier.Epic) },
                Stats = new StatValues { ChestOdds = new[] { 1f, 0f, 0f } }, // every card rolls common
            };
            List<Upgrade> offer = TieredOffers.Offer(12345UL, 1, 2, new[] { "Quick Shot" }, values, new RunModifiers(), true);
            Assert.That(offer.Count, Is.EqualTo(1), "the one row there is, though it may only come epic");
            Assert.That(offer[0].Kind, Is.EqualTo(UpgradeKind.Damage));
            Assert.That(offer[0].Tier, Is.EqualTo(UpgradeTier.Epic));
        }
    }
}
