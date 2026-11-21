using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Her stats and the tiers (work order 050, decision 0012): each stat's rule, the skills' weights for flat damage, the
    /// cap on attack speed, a crit, a clean press and a perfect release as crits paid once, the tiers' odds with luck, an
    /// offer's rules, the measurements at the dummy as pinned ranges, and with the switch off her start and a thousand
    /// offers exactly as before.
    /// </summary>
    public class StatsAndTiersTests
    {
        static readonly string[] Four = { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };

        /// <summary>A line to the test's output (outside a test runner, to the console).</summary>
        static void Say(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (NullReferenceException) { Console.WriteLine(line); }
        }

        static RunModifiers On() => StatsDpsModel.Start(new RunValues());
        static RunModifiers Off() => StatsDpsModel.Start(new RunValues { StatsAndTiers = false });
        static Upgrade Step(UpgradeKind kind, float amount, int heal = 0, string skill = "") =>
            new Upgrade { Kind = kind, Amount = amount, Heal = heal, Skill = skill, Tiered = true };

        // ---------------- Each stat ----------------

        [Test]
        public void HerStart_WithTheSwitchOn_AndOff()
        {
            var feel = new FeelValues();
            HerStats on = On().Stats, off = Off().Stats;
            Assert.That(new RunValues().StatsAndTiers, Is.True, "on by default");
            Assert.That(on.Damage, Is.Zero);
            Assert.That(on.AttackSpeed, Is.EqualTo(0.85f).Within(1e-6f), "about 15 % slower");
            Assert.That(on.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery), Is.EqualTo(0.225f / 0.85f).Within(1e-5f));
            Assert.That(on.BasicShotRecovery(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery), Is.EqualTo(0.225f / 0.85f - 0.1f).Within(1e-5f),
                        "only the recovery takes the difference");
            Assert.That(on.CritChance, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(on.CritDamage, Is.EqualTo(0.5f).Within(1e-6f));
            Assert.That(on.Luck, Is.Zero);
            Assert.That(on.MoveSpeed, Is.EqualTo(1f));
            Assert.That(on.MaxHealthFor(100), Is.EqualTo(100));
            Assert.That(on.Reach + on.Stagger + on.DamageShare, Is.EqualTo(2f));

            Assert.That(off.Damage, Is.Zero);
            Assert.That(off.AttackSpeed, Is.EqualTo(1f));
            Assert.That(off.BasicShotRecovery(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery), Is.EqualTo(feel.BasicShotRecovery), "the feel's own");
            Assert.That(off.CritChance, Is.Zero);
            Assert.That(off.CritDamage, Is.Zero);
            Assert.That(off.Luck, Is.Zero);
        }

        [Test]
        public void EachStep_IsReadWhereItsStatIs()
        {
            RunModifiers m = On();
            m.Take(Step(UpgradeKind.FlatDamage, 3f));
            m.Take(Step(UpgradeKind.FlatDamage, 6f));
            m.Take(Step(UpgradeKind.AttackSpeed, 0.12f));
            m.Take(Step(UpgradeKind.CritChance, 0.04f));
            m.Take(Step(UpgradeKind.CritDamage, 0.15f));
            m.Take(Step(UpgradeKind.Luck, 2f));
            m.Take(Step(UpgradeKind.RunSpeed, 0.05f));
            m.Take(Step(UpgradeKind.MaxHealth, 20f, 20));
            m.Take(Step(UpgradeKind.Cooldown, 0.1f, skill: "Quick Shot"));
            m.Take(Step(UpgradeKind.LongerBarrel, 2f));
            m.Take(Step(UpgradeKind.HeavyHits, 0.3f));
            m.Take(Step(UpgradeKind.Damage, 0.15f));
            HerStats s = m.Stats;
            Assert.That(s.Damage, Is.EqualTo(9f));
            Assert.That(s.FlatFor(null), Is.EqualTo(9), "the basic shot: all of it");
            Assert.That(s.AttackSpeed, Is.EqualTo(0.85f * 1.12f).Within(1e-5f), "a step adds its share of her start's rate");
            Assert.That(s.CritChance, Is.EqualTo(0.09f).Within(1e-6f));
            Assert.That(s.CritDamage, Is.EqualTo(0.65f).Within(1e-6f));
            Assert.That(s.Luck, Is.EqualTo(2f));
            Assert.That(s.MoveSpeed, Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(s.MaxHealthFor(100), Is.EqualTo(120));
            Assert.That(s.CooldownShare("Quick Shot"), Is.EqualTo(0.9f).Within(1e-6f));
            Assert.That(s.Reach, Is.EqualTo(2f));
            Assert.That(s.Stagger, Is.EqualTo(1.3f).Within(1e-6f));
            Assert.That(s.DamageShare, Is.EqualTo(1.15f).Within(1e-6f));
            // The run's modifiers read the old stats through the same class.
            Assert.That(m.RunSpeedShare, Is.EqualTo(s.MoveSpeed));
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(s.MaxHealthFor(100)));
            Assert.That(m.CooldownShare("Quick Shot"), Is.EqualTo(s.CooldownShare("Quick Shot")));
            Assert.That(m.BasicShotRangeBonus, Is.EqualTo(s.Reach));
            Assert.That(m.StaggerShare, Is.EqualTo(s.Stagger));
            Assert.That(m.DamageShare, Is.EqualTo(s.DamageShare));
            // Reach: never more than three metres with the switch on.
            m.Take(Step(UpgradeKind.LongerBarrel, 3f));
            Assert.That(s.Reach, Is.EqualTo(3f));
            m.StatsAndTiers = false;
            Assert.That(s.Reach, Is.EqualTo(5f), "off: as before, the sum");
            Assert.That(s.Damage + s.CritChance + s.Luck, Is.Zero, "off: the new ones change nothing");
        }

        [Test]
        public void ThePiecesBetweenRuns_StepTheSameClass()
        {
            RunModifiers m = On();
            var values = new MetaValues();
            var state = new MetaState();
            state.Owned.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand, MetaPieces.SpareStep });
            state.Equipped.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand });
            MetaRack.Apply(state, values, m.Meta);
            Assert.That(m.Stats.MaxHealthFor(100), Is.EqualTo(120), "Thick Hide");
            Assert.That(m.Stats.DamageShare, Is.EqualTo(1.15f).Within(1e-6f), "Heavy Hand");
            m.Take(Step(UpgradeKind.MaxHealth, 10f, 10));
            m.Take(Step(UpgradeKind.Damage, 0.15f));
            Assert.That(m.Stats.MaxHealthFor(100), Is.EqualTo(130));
            Assert.That(m.Stats.DamageShare, Is.EqualTo(1.15f * 1.15f).Within(1e-5f));
            List<string> lines = m.Stats.Lines(0.225f, 10f, 100);
            Assert.That(lines.Single(l => l.StartsWith("Max health")), Is.EqualTo("Max health  130  (120)"), "the start is with the rack's pieces");
        }

        // ---------------- Flat damage by weight ----------------

        [Test]
        public void EverySkill_HasItsWeight_ForFlatDamage()
        {
            var expected = new Dictionary<string, float>
            {
                { "Quick Shot", 1f }, { "Powder Keg", 2f }, { "Fan Fire", 0.5f }, { "Long Shot", 2f }, { "Scatter Blast", 2f }, { "Heavy Volley", 2f },
                { "Backstep", 1f }, { "Triple Tap", 0.5f }, { "Tag Round", 1f }, { "Breaker Slug", 2f }, { "Slide", 0f }, { "Stopping Shot", 1f },
            };
            SkillDefinition[] all = SkillDefaults.Create();
            Assert.That(all.Select(s => s.Name), Is.EquivalentTo(expected.Keys), "every skill of the list has one");
            foreach (SkillDefinition s in all)
            {
                Assert.That(s.FlatDamageWeight, Is.LessThan(0f), s.Name + ": none set, the default by name");
                Assert.That(FlatDamage.WeightOf(s), Is.EqualTo(expected[s.Name]), s.Name);
            }
            Assert.That(FlatDamage.WeightOf(null), Is.EqualTo(1f), "the basic shot");
            Assert.That(FlatDamage.For(all.Single(s => s.Name == "Triple Tap"), 3f), Is.EqualTo(2), "1.5, half away from zero");
            Assert.That(FlatDamage.For(all.Single(s => s.Name == "Scatter Blast"), 3f), Is.EqualTo(6));
            // Set on the definition, it is what counts; a skill not named here goes by its kind.
            SkillDefinition set = all[0].Copy();
            set.FlatDamageWeight = 1.5f;
            Assert.That(FlatDamage.WeightOf(set), Is.EqualTo(1.5f));
            Assert.That(FlatDamage.WeightOf(new SkillDefinition { Name = "New", Damage = 50 }), Is.EqualTo(1f));
            Assert.That(FlatDamage.WeightOf(new SkillDefinition { Name = "New", Damage = 50, RecastCount = 3 }), Is.EqualTo(0.5f));
            Assert.That(FlatDamage.WeightOf(new SkillDefinition { Name = "New", Damage = 50, Input = SkillInput.Charge }), Is.EqualTo(2f));
            Assert.That(FlatDamage.WeightOf(new SkillDefinition { Name = "New", Damage = 0 }), Is.EqualTo(0f));
        }

        // ---------------- Attack speed's cap ----------------

        [Test]
        public void AttackSpeed_IsCapped_AndAStepAtTheCapIsNeverOffered()
        {
            var feel = new FeelValues();
            RunModifiers m = On();
            for (int i = 0; i < 20; i++) m.Take(Step(UpgradeKind.AttackSpeed, 0.06f));
            HerStats s = m.Stats;
            Assert.That(s.AttackSpeed, Is.EqualTo(1.3f).Within(1e-6f), "at most 1.3 of the feel's rate");
            Assert.That(s.AttackSpeedAtMost, Is.True);
            float recovery = s.BasicShotRecovery(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
            Assert.That(s.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery), Is.EqualTo(0.225f / 1.3f).Within(1e-5f));
            Assert.That(recovery, Is.GreaterThan(0.05f), "longer than the Shoot clip's fade in: each shot still shows");
            var values = new RunValues();
            for (ulong seed = 1; seed <= 300; seed++)
                Assert.That(ChestUpgrades.Offer(seed, 1, 2, Four, values, m, false).Any(u => u.Kind == UpgradeKind.AttackSpeed), Is.False, "seed " + seed);
        }

        // ---------------- Crits ----------------

        [Test]
        public void ACrit_DealsTheCritDamageMore_AndTheStreamCanBeSeeded()
        {
            Assert.That(CritRules.Apply(40, 0.5f), Is.EqualTo(60));
            Assert.That(CritRules.Apply(70, 0.6f), Is.EqualTo(112));
            Assert.That(CritRules.Apply(5, 0.5f), Is.EqualTo(8), "7.5, half away from zero");
            Assert.That(CritRules.Apply(0, 0.5f), Is.Zero);
            Assert.That(CritRules.Rolls(new LevelRandom(1), 0f), Is.False);
            Assert.That(CritRules.Rolls(new LevelRandom(1), 1f), Is.True);
            // The same seed, the same crits; about the chance of them.
            LevelRandom a = LevelRandom.Stream(42, "crit"), b = LevelRandom.Stream(42, "crit");
            int crits = 0;
            for (int i = 0; i < 100000; i++)
            {
                bool x = CritRules.Rolls(a, 0.05f);
                Assert.That(CritRules.Rolls(b, 0.05f), Is.EqualTo(x));
                if (x) crits++;
            }
            Assert.That(crits / 100000.0, Is.EqualTo(0.05).Within(0.003));
        }

        [Test]
        public void ACleanPress_AndAPerfectRelease_AreCrits_PaidOnce()
        {
            // The clean tap's 1.5 of work order 050 (work order 064 raised the code's default to 1.75; its numbers are in
            // FiringFeelRulesTests): the folding rule itself is what this pins.
            var kit = new KitValues { CleanDamage = 1.5f };
            var v = new StatValues();
            SkillDefinition tap = SkillDefaults.Create().Single(s => s.Name == "Triple Tap");
            SkillDefinition shot = SkillDefaults.Create().Single(s => s.Name == "Long Shot");
            // Before: a clean tap 1.5 times, a perfect release twice the full charge.
            int cleanBefore = (int)Math.Round(tap.Damage * kit.CleanDamage), perfectBefore = PoolRules.PerfectDamage(shot);
            Assert.That(cleanBefore, Is.EqualTo(105));
            Assert.That(perfectBefore, Is.EqualTo(600));
            // Now, at her start: the same, as a crit of +50 % on what is left of their own scale.
            int clean = CritRules.Apply((int)Math.Round(tap.Damage * CritRules.Folded(kit.CleanDamage, v)), v.StartCritDamage);
            int perfect = CritRules.Apply((int)Math.Round(SkillRules.Damage(shot, 1f) * shot.PerfectScale * CritRules.Folded(1f, v)), v.StartCritDamage);
            Assert.That(CritRules.Folded(kit.CleanDamage, v), Is.EqualTo(1f).Within(1e-6f), "a clean tap is a crit and nothing more");
            Assert.That(clean, Is.EqualTo(cleanBefore), "105, as before");
            Assert.That(perfect, Is.EqualTo(perfectBefore), "600, as before");
            // With crit damage +10 %, both rise with it, and once.
            Assert.That(CritRules.Apply((int)Math.Round(tap.Damage * CritRules.Folded(kit.CleanDamage, v)), 0.6f), Is.EqualTo(112));
            Assert.That(CritRules.Apply((int)Math.Round(SkillRules.Damage(shot, 1f) * shot.PerfectScale * CritRules.Folded(1f, v)), 0.6f), Is.EqualTo(640));
        }

        // ---------------- The tiers' odds ----------------

        [TestCase(0f, 0.75, 0.22, 0.03)]
        [TestCase(3f, 0.57, 0.355, 0.075)]
        [TestCase(8f, 0.27, 0.58, 0.15)]
        public void TheTiers_OverAHundredThousandDraws(float luck, double common, double rare, double epic)
        {
            var v = new StatValues();
            double[] shares = UpgradeTiers.Shares(v.ChestOdds, luck, v);
            Assert.That(shares[0], Is.EqualTo(common).Within(1e-6));
            Assert.That(shares[1], Is.EqualTo(rare).Within(1e-6));
            Assert.That(shares[2], Is.EqualTo(epic).Within(1e-6));
            var rng = LevelRandom.Stream(7, "tiers");
            var counts = new int[3];
            const int n = 100000;
            for (int i = 0; i < n; i++) counts[(int)UpgradeTiers.Draw(rng, shares)]++;
            Say(string.Format(CultureInfo.InvariantCulture, "luck {0}: common {1:0.000}, rare {2:0.000}, epic {3:0.000}",
                                                luck, counts[0] / (double)n, counts[1] / (double)n, counts[2] / (double)n));
            Assert.That(counts[0] / (double)n, Is.EqualTo(common).Within(0.006));
            Assert.That(counts[1] / (double)n, Is.EqualTo(rare).Within(0.006));
            Assert.That(counts[2] / (double)n, Is.EqualTo(epic).Within(0.003));
        }

        [Test]
        public void Luck_NeverMovesMoreThanAllOfTheCommons_AndTheRewardsOddsHaveNoCommon()
        {
            var v = new StatValues();
            double[] lots = UpgradeTiers.Shares(v.ChestOdds, 40f, v);
            Assert.That(lots[0], Is.EqualTo(0.0).Within(1e-9));
            Assert.That(lots.Sum(), Is.EqualTo(1.0).Within(1e-9));
            Assert.That(lots[1] / lots[2], Is.EqualTo((22.0 + 75.0 * 0.75) / (3.0 + 75.0 * 0.25)).Within(1e-6), "split as 6 to 2");
            double[] reward = UpgradeTiers.Shares(v.RewardOdds, 0f, v);
            Assert.That(reward, Is.EqualTo(new[] { 0.0, 0.7, 0.3 }).Within(1e-9));
        }

        // ---------------- An offer ----------------

        [Test]
        public void AnOffer_NeverShowsAStatTwice_ShowsThree_AndNothingUseless() // work order 065: three, not two
        {
            var values = new RunValues();
            var tiers = new int[3];
            var kinds = new HashSet<UpgradeKind>();
            for (ulong seed = 1; seed <= 2000; seed++)
            {
                RunModifiers m = On();
                bool full = seed % 3 == 0;
                for (int pick = 0; pick < 8; pick++)
                {
                    List<Upgrade> offer = ChestUpgrades.Offer(seed, 1 + pick / 2, 2 + pick % 2, Four, values, m, full, pick == 0 && seed % 5 == 0 ? 1 : 0);
                    Assert.That(offer.Count, Is.EqualTo(pick == 0 && seed % 5 == 0 ? 4 : 3), "seed " + seed); // work order 065: three a chest
                    Assert.That(offer.Select(u => u.Stat).Distinct().Count(), Is.EqualTo(offer.Count), "no stat twice, seed " + seed);
                    foreach (Upgrade u in offer)
                    {
                        Assert.That(u.Tiered, Is.True);
                        tiers[(int)u.Tier]++;
                        kinds.Add(u.Kind);
                        Assert.That(u.Text, Is.Not.Empty);
                        Assert.That(u.Short, Is.Not.Empty);
                        if (full) Assert.That(u.Kind, Is.Not.EqualTo(UpgradeKind.Mend), "no Mend at full health");
                        if (u.Kind == UpgradeKind.Damage) Assert.That(u.Tier, Is.EqualTo(UpgradeTier.Epic), "the share of damage is only an epic");
                        if (u.Kind == UpgradeKind.Cooldown) Assert.That(Four, Does.Contain(u.Skill));
                        Assert.That(Useful(u, m, values, full), Is.True, "nothing that does nothing: " + u.Short);
                    }
                    m.Take(offer[pick % offer.Count]);
                }
            }
            Assert.That(kinds.Count, Is.EqualTo(22), "every row comes up, work order 065's six too");
            Assert.That(tiers.All(t => t > 0), Is.True, "every tier comes up");
        }

        /// <summary>A step that changes something: the stat it names is not at its end.</summary>
        static bool Useful(Upgrade u, RunModifiers m, RunValues values, bool full)
        {
            var copy = new RunModifiers { StatsAndTiers = true, StatValues = values.Stats, CooldownFloor = values.CooldownFloor };
            foreach (Upgrade t in m.Taken) copy.Take(t);
            float Before(Func<HerStats, float> f) => f(copy.Stats);
            float before;
            switch (u.Kind)
            {
                case UpgradeKind.AttackSpeed: before = Before(s => s.AttackSpeed); copy.Take(u); return copy.Stats.AttackSpeed > before;
                case UpgradeKind.CritChance: before = Before(s => s.CritChance); copy.Take(u); return copy.Stats.CritChance > before;
                case UpgradeKind.LongerBarrel: before = Before(s => s.Reach); copy.Take(u); return copy.Stats.Reach > before;
                case UpgradeKind.Cooldown: before = copy.CooldownShare(u.Skill); copy.Take(u); return copy.CooldownShare(u.Skill) < before;
                case UpgradeKind.DashCooldown: before = copy.DashCooldownShare; copy.Take(u); return copy.DashCooldownShare < before;
                case UpgradeKind.Luck: return UpgradeTiers.Shares(values.Stats.ChestOdds, copy.Stats.Luck, values.Stats)[0] > 0.0;
                case UpgradeKind.Mend: return !full;
                default: return true;
            }
        }

        [Test]
        public void AnOffer_IsTheSameForTheSameRun_AndATierScalesTheCommonStep()
        {
            var values = new RunValues();
            for (ulong seed = 1; seed <= 300; seed++)
            {
                List<Upgrade> a = ChestUpgrades.Offer(seed, 2, 4, Four, values, On(), false);
                List<Upgrade> b = ChestUpgrades.Offer(seed, 2, 4, Four, values, On(), false);
                Assert.That(b.Select(u => u.Short + u.TierWord), Is.EqualTo(a.Select(u => u.Short + u.TierWord)));
                foreach (Upgrade u in a)
                {
                    UpgradeDefinition row = values.TieredUpgrades.Single(r => r.Kind == u.Kind);
                    float scale = row.MinTier == UpgradeTier.Epic ? 1f : u.Tier == UpgradeTier.Epic ? 3f : u.Tier == UpgradeTier.Rare ? 2f : 1f;
                    // Work order 065: a row with steps of its own (the six new kinds) takes them; the others scale as before.
                    bool own = row.RareAmount > 0f || row.EpicAmount > 0f;
                    float step = !own || u.Tier == UpgradeTier.Common ? row.Amount * scale : u.Tier == UpgradeTier.Epic ? row.EpicAmount : row.RareAmount;
                    Assert.That(u.Amount, Is.EqualTo(step).Within(1e-5f), u.Short);
                    Assert.That(TieredOffers.StepOf(row, u.Tier, values.Stats), Is.EqualTo(u.Amount), u.Short);
                    Assert.That(u.Heal, Is.EqualTo((int)Math.Round(row.Heal * (own ? step / row.Amount : scale))), u.Short);
                }
            }
            // The first guesses of a common step.
            UpgradeDefinition Row(UpgradeKind k) => values.TieredUpgrades.Single(r => r.Kind == k);
            Assert.That(Row(UpgradeKind.FlatDamage).Amount, Is.EqualTo(3f));
            Assert.That(Row(UpgradeKind.AttackSpeed).Amount, Is.EqualTo(0.06f));
            Assert.That(Row(UpgradeKind.CritChance).Amount, Is.EqualTo(0.04f));
            Assert.That(Row(UpgradeKind.CritDamage).Amount, Is.EqualTo(0.15f));
            Assert.That(Row(UpgradeKind.Luck).Amount, Is.EqualTo(1f));
            Assert.That(Row(UpgradeKind.MaxHealth).Amount, Is.EqualTo(10f));
            Assert.That(Row(UpgradeKind.LongerBarrel).Amount, Is.EqualTo(1f));
            Assert.That(Row(UpgradeKind.Damage).MinTier, Is.EqualTo(UpgradeTier.Epic));
        }

        // ---------------- With the switch off: as before ----------------

        /// <summary>The chest's draw as it was before work order 050, copied, to hold the switch-off draw to it.</summary>
        static List<Upgrade> OfferAsBefore(ulong runSeed, int floor, int chest, IReadOnlyList<string> equippedSkills, RunValues values,
                                           RunModifiers taken, bool atFullHealth, int extra)
        {
            LevelRandom rng = LevelRandom.Stream(runSeed, ChestUpgrades.Stage, floor * 64 + chest);
            var rows = Enumerable.Range(0, values.Upgrades?.Length ?? 0).ToList();
            rng.Shuffle(rows);
            var offer = new List<Upgrade>();
            foreach (int i in rows)
            {
                if (offer.Count >= values.UpgradesOffered + Math.Max(0, extra)) break;
                UpgradeDefinition d = values.Upgrades[i];
                if (!ChestUpgrades.MayOffer(d, taken, atFullHealth)) continue;
                string skill = d.Skill ?? "";
                if (d.Kind == UpgradeKind.Cooldown && skill.Length == 0)
                {
                    if (equippedSkills == null || equippedSkills.Count == 0) continue;
                    skill = equippedSkills[rng.Below(equippedSkills.Count)];
                }
                offer.Add(new Upgrade { Kind = d.Kind, Amount = d.Amount, Heal = d.Heal, Skill = skill });
            }
            return offer;
        }

        [Test]
        public void SwitchedOff_AThousandSeededOffers_AreTodaysExactly()
        {
            var values = new RunValues { StatsAndTiers = false };
            RunModifiers m = Off(), before = Off();
            for (ulong seed = 1; seed <= 1000; seed++)
            {
                int floor = 1 + (int)(seed % 5), chest = (int)(seed % 7);
                bool full = seed % 2 == 0;
                int extra = seed % 9 == 0 ? 1 : 0;
                List<Upgrade> now = ChestUpgrades.Offer(seed, floor, chest, Four, values, m, full, extra);
                List<Upgrade> then = OfferAsBefore(seed, floor, chest, Four, values, before, full, extra);
                Assert.That(now.Select(u => u.Kind + " " + u.Amount + " " + u.Heal + " " + u.Skill), Is.EqualTo(then.Select(u => u.Kind + " " + u.Amount + " " + u.Heal + " " + u.Skill)),
                            "seed " + seed);
                Assert.That(now.Any(u => u.Tiered), Is.False, "no tier shows");
                if (now.Count > 0)
                {
                    m.Take(now[0]);
                    before.Take(then[0]);
                }
                if (seed % 10 == 0)
                {
                    m.Clear();
                    before.Clear();
                }
            }
        }

        [Test]
        public void SwitchedOff_HerStart_IsTodays()
        {
            StatsDpsModel.Dps off = StatsDpsModel.Measure(Off());
            var feel = new FeelValues();
            Assert.That(off.Basic, Is.EqualTo(Placeholders.BasicShotDamage / (feel.BasicShotStartup + feel.BasicShotActive + feel.BasicShotRecovery)).Within(1e-3),
                        "40 every 0.225 s, no crit");
            HerStats s = Off().Stats;
            Assert.That(s.FlatFor(null) + s.FlatFor(SkillDefaults.Create()[4]), Is.Zero);
            Assert.That(s.CritChance, Is.Zero);
            Assert.That(s.AttackSpeed, Is.EqualTo(1f));
            Assert.That(Off().Stats.Lines(0.225f, 10f, 100).Single(l => l.StartsWith("Attack speed")), Is.EqualTo("Attack speed  4.4 shots/s  (4.4)"));
        }

        // ---------------- The measurements at the dummy ----------------

        [Test]
        public void TheMeasurements_AtTheDummy_HoldTheOrdersAims()
        {
            var today = new RunValues { StatsAndTiers = false };
            var now = new RunValues();
            StatsDpsModel.Dps start0 = StatsDpsModel.Measure(StatsDpsModel.Start(today));
            StatsDpsModel.Dps start1 = StatsDpsModel.Measure(StatsDpsModel.Start(now));
            var six0 = StatsDpsModel.AfterSixPicks(today);
            var six1 = StatsDpsModel.AfterSixPicks(now);
            Say(string.Format(CultureInfo.InvariantCulture,
                "start, today: basic {0:0.0}/s, rotation {1:0.0}/s; now: basic {2:0.0}/s ({3:0.0} %), rotation {4:0.0}/s ({5:0.0} %)",
                start0.Basic, start0.Rotation, start1.Basic, (start1.Basic / start0.Basic - 1) * 100, start1.Rotation, (start1.Rotation / start0.Rotation - 1) * 100));
            Say("six picks, today: basic " + six0.basic + "; rotation " + six0.rotation);
            Say("six picks, now:   basic " + six1.basic + "; rotation " + six1.rotation);
            Say(string.Format(CultureInfo.InvariantCulture, "six picks, now against today: basic {0:0.0} %, rotation {1:0.0} %",
                (six1.basic.Mean / six0.basic.Mean - 1) * 100, (six1.rotation.Mean / six0.rotation.Mean - 1) * 100));

            Assert.That(start1.Basic / start0.Basic, Is.InRange(0.85, 0.90), "the start 10 to 15 % lower with the basic shot");
            Assert.That(start1.Rotation / start0.Rotation, Is.InRange(0.92, 1.08), "and within 8 % with the whole rotation");
            Assert.That(six1.basic.Mean / six0.basic.Mean, Is.InRange(0.90, 1.10), "after six picks within 10 % of today's mean, basic");
            Assert.That(six1.rotation.Mean / six0.rotation.Mean, Is.InRange(0.90, 1.10), "and the rotation");
            Assert.That(six1.basic.Deviation, Is.GreaterThan(six0.basic.Deviation), "with a wider spread");
            Assert.That(six1.rotation.Deviation, Is.GreaterThan(six0.rotation.Deviation));
        }

        // ---------------- In words, and in the summary ----------------

        [Test]
        public void ATieredUpgrade_HasItsWords()
        {
            Assert.That(Step(UpgradeKind.FlatDamage, 3f).Short, Is.EqualTo("+3 dmg"));
            Assert.That(Step(UpgradeKind.AttackSpeed, 0.12f).Short, Is.EqualTo("+12% atk speed"));
            Assert.That(Step(UpgradeKind.CritChance, 0.04f).Short, Is.EqualTo("+4% crit"));
            Assert.That(Step(UpgradeKind.CritDamage, 0.45f).Short, Is.EqualTo("+45% crit dmg"));
            Assert.That(Step(UpgradeKind.Luck, 1f).Short, Is.EqualTo("+1 luck"));
            Assert.That(Step(UpgradeKind.Damage, 0.15f).Short, Is.EqualTo("+15% dmg"));
            StringAssert.Contains("damage to every shot", Step(UpgradeKind.FlatDamage, 3f).Text);
            Assert.That(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 3f, Tier = UpgradeTier.Rare, Tiered = true }.TierWord, Is.EqualTo("rare"));
            Assert.That(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 3f }.TierWord, Is.EqualTo(""), "an upgrade of the plain offer has none");
            Assert.That(UpgradeTiers.Rgb(UpgradeTier.Common), Is.EqualTo(new[] { 1f, 1f, 1f }), "white");
            Assert.That(UpgradeTiers.Rgb(UpgradeTier.Epic)[2], Is.LessThan(0.5f), "gold");
        }

        [Test]
        public void HerStatsInWords_ForThePauseMenu()
        {
            RunModifiers m = On();
            m.Take(Step(UpgradeKind.FlatDamage, 6f));
            m.Take(Step(UpgradeKind.Cooldown, 0.05f, skill: "Quick Shot"));
            List<string> lines = m.Stats.Lines(0.225f, 10f, 100);
            Assert.That(lines.Count, Is.EqualTo(16), "work order 065: five more lines (Cooldowns' is in its own line)");
            Assert.That(lines[0], Is.EqualTo("Damage  +6 a shot, skills by weight  (+0)"));
            Assert.That(lines[1], Is.EqualTo("Attack speed  3.8 shots/s  (3.8)"));
            Assert.That(lines[2], Is.EqualTo("Crit chance  5 %  (5 %)"));
            Assert.That(lines[3], Is.EqualTo("Crit damage  +50 %  (+50 %)"));
            Assert.That(lines.Single(l => l.StartsWith("Cooldowns")), Is.EqualTo("Cooldowns  Quick Shot -5 %  (none)"));
            Assert.That(lines.Single(l => l.StartsWith("Reach")), Is.EqualTo("Reach  10 m  (10 m)"));
            StringAssert.StartsWith("dmg +6, attack 3.78/s, crit 5% x1.5", m.Stats.OneLine(0.225f, 100));
        }

        [Test]
        public void TheSummary_ListsThePicksByTier_AndHerFinalStats()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0d, SessionEventTypes.SessionStart),
                new SessionEvent(1d, SessionEventTypes.RunStart, "ABCDEFGH"),
                Chest(2d, "+3 dmg", "common", "dmg +3"),
                Chest(3d, "+12% atk speed", "rare", "dmg +3, attack 4.0/s"),
                Chest(4d, "+10 hp", "common", "dmg +3, attack 4.0/s, hp 110"),
                new SessionEvent(5d, SessionEventTypes.RunEnd) { Result = "death" },
            };
            events[5].AddExtra("floor", "2");
            events[5].AddExtra("stats", "final line");
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            StringAssert.Contains("  picks by tier    common 2 (+3 dmg, +10 hp);  rare 1 (+12% atk speed);  epic 0", text);
            StringAssert.Contains("  final stats      final line", text);
        }

        static SessionEvent Chest(double t, string taken, string tier, string stats)
        {
            var e = new SessionEvent(t, SessionEventTypes.ChestOpen, taken);
            e.AddExtra("floor", "1");
            e.AddExtra("tier", tier);
            e.AddExtra("stats", stats);
            return e;
        }
    }
}
