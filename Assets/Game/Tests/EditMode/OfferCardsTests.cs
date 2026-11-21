using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 065 (Kristian, 2026-11-21: "oppgraderingene trenger mer informasjon", "kisten skal ha flere valg, gjerne 3",
    /// "og mere stats"): what every card of an offer says, from given values (each chest kind's sentence and its numbers before
    /// and after, a skill's tiers, an epic's change, an elite's upgrade, a focus choice); a chest of three different stats over
    /// ten thousand seeded offers, fewer only when the pool is short, and how often each kind comes up; each new kind's steps,
    /// cap and what it changes; with the stats off the old chest of two; the pop-up's layout at 1920 and 1280 wide.
    /// </summary>
    public class OfferCardsTests
    {
        static readonly string[] Four = { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
        const string A = OfferTexts.Arrow;

        static void Say(string line)
        {
            try { TestContext.WriteLine(line); }
            catch (NullReferenceException) { Console.WriteLine(line); }
        }

        static Upgrade Step(UpgradeKind kind, float amount, int heal = 0, string skill = "", UpgradeTier tier = UpgradeTier.Common) =>
            new Upgrade { Kind = kind, Amount = amount, Heal = heal, Skill = skill, Tiered = true, Tier = tier };

        /// <summary>Her at a run's start with the stats on, hurt to 72 of 100, the start four as she has them.</summary>
        static OfferContext Start(RunModifiers m = null)
        {
            SkillDefinition[] all = SkillDefaults.Create();
            return new OfferContext
            {
                Modifiers = m ?? StatsDpsModel.Start(new RunValues()), Health = 72, OwnMaxHealth = 100,
                Equipped = all.Where(s => Four.Contains(s.Name)).ToList(),
            };
        }

        static Upgrade AtTier(UpgradeDefinition row, UpgradeTier tier, RunValues values) =>
            new Upgrade
            {
                Kind = row.Kind, Amount = TieredOffers.StepOf(row, tier, values.Stats), Skill = row.Kind == UpgradeKind.Cooldown && row.Skill.Length == 0 ? "Scatter Blast" : row.Skill,
                Heal = (int)Math.Round(row.Heal * (row.Amount > 0f && (row.RareAmount > 0f || row.EpicAmount > 0f) ? TieredOffers.StepOf(row, tier, values.Stats) / row.Amount : UpgradeTiers.Scale(tier, values.Stats)), MidpointRounding.AwayFromZero),
                Tier = tier, Tiered = true,
            };

        // ---------------- A chest's cards ----------------

        /// <summary>Every row of the chest's table at every tier, written out in full as it reads at her start (for the report).</summary>
        [Test]
        public void EveryChestCard_AtEveryTier_HasANameASentenceAndItsNumbers()
        {
            var values = new RunValues();
            foreach (UpgradeDefinition row in values.TieredUpgrades)
                foreach (UpgradeTier tier in UpgradeTiers.All)
                {
                    if (tier < row.MinTier) continue;
                    OfferCard card = OfferTexts.ForUpgrade(AtTier(row, tier, values), Start());
                    Say(card.ToString().Replace("\n", "  |  "));
                    Assert.That(card.Name, Is.Not.Empty);
                    Assert.That(card.Sentence, Does.EndWith("."), card.Name + ": one plain sentence");
                    Assert.That(card.Sentence.Length, Is.InRange(15, 80), card.Name + ": two lines at most on the card");
                    Assert.That(card.Numbers, Is.Not.Empty, card.Name + ": its numbers");
                    foreach (string n in card.Numbers) StringAssert.Contains(A, n, card.Name + ": before and after");
                    Assert.That(card.TierLine, Is.EqualTo(UpgradeTiers.Word(tier).ToUpperInvariant()));
                }
        }

        [Test]
        public void TheChestCards_AtHerStart_ReadAsWritten()
        {
            var values = new RunValues();
            OfferCard Card(UpgradeKind kind, UpgradeTier tier = UpgradeTier.Common) =>
                OfferTexts.ForUpgrade(AtTier(values.TieredUpgrades.First(r => r.Kind == kind), tier, values), Start());

            OfferCard speed = Card(UpgradeKind.AttackSpeed, UpgradeTier.Rare);
            Assert.That(speed.Name, Is.EqualTo("Attack speed"));
            Assert.That(speed.TierLine, Is.EqualTo("RARE"));
            Assert.That(speed.Sentence, Is.EqualTo("Your basic shot fires faster."));
            Assert.That(speed.Numbers, Is.EqualTo(new[] { "3.8" + A + "4.2 shots a second" }));
            Assert.That(Card(UpgradeKind.FlatDamage).Numbers, Is.EqualTo(new[] { "basic shot  40" + A + "43", "Scatter Blast  220" + A + "226" }));
            Assert.That(Card(UpgradeKind.CritChance).Numbers, Is.EqualTo(new[] { "crit chance  5 %" + A + "9 %" }));
            Assert.That(Card(UpgradeKind.CritDamage).Numbers, Is.EqualTo(new[] { "a crit deals  +50 %" + A + "+65 %" }));
            Assert.That(Card(UpgradeKind.Luck).Numbers, Is.EqualTo(new[] { "a chest's rare or better  25 %" + A + "31 %" }));
            Assert.That(Card(UpgradeKind.MaxHealth).Numbers, Is.EqualTo(new[] { "maximum health  100" + A + "110", "health now  72" + A + "82" }));
            Assert.That(Card(UpgradeKind.Mend).Numbers, Is.EqualTo(new[] { "health  72" + A + "100" }));
            Assert.That(Card(UpgradeKind.Cooldown).Numbers, Is.EqualTo(new[] { "Scatter Blast cooldown  6 s" + A + "5.7 s" }));
            Assert.That(Card(UpgradeKind.Cooldown).Sentence, Is.EqualTo("Scatter Blast comes back sooner."));
            Assert.That(Card(UpgradeKind.LongerBarrel).Numbers, Is.EqualTo(new[] { "reach  10 m" + A + "11 m" }));
            Assert.That(Card(UpgradeKind.HeavyHits).Numbers, Is.EqualTo(new[] { "stagger dealt  100 %" + A + "110 %" }));
            Assert.That(Card(UpgradeKind.DashCooldown).Numbers, Is.EqualTo(new[] { "dash cooldown  10 s" + A + "9.2 s" }));
            Assert.That(Card(UpgradeKind.LastingMarks).Numbers, Is.EqualTo(new[] { "marks last  6 s" + A + "8 s" }));
            Assert.That(Card(UpgradeKind.KeenFocus).Numbers, Is.EqualTo(new[] { "focus gained  100 %" + A + "110 %" }));
            Assert.That(Card(UpgradeKind.FirstMark).Numbers, Is.EqualTo(new[] { "marks on an unmarked enemy  1" + A + "2" }));
            Assert.That(Card(UpgradeKind.Damage, UpgradeTier.Epic).Numbers, Is.EqualTo(new[] { "damage dealt  100 %" + A + "115 %", "basic shot  40" + A + "46" }));
            // Work order 065's six.
            Assert.That(Card(UpgradeKind.AllCooldowns).Numbers, Is.EqualTo(new[] { "every skill's cooldown  -0 %" + A + "-3 %", "Backstep cooldown  10 s" + A + "9.7 s" }));
            Assert.That(Card(UpgradeKind.CleanPress).Numbers, Is.EqualTo(new[] { "clean and perfect hits  +0 %" + A + "+8 %" }));
            Assert.That(Card(UpgradeKind.MarkedPrey).Numbers, Is.EqualTo(new[] { "hits on a marked enemy  +0 %" + A + "+6 %" }));
            Assert.That(Card(UpgradeKind.Mending).Numbers, Is.EqualTo(new[] { "a cleared room heals  8" + A + "11", "the elite room heals  20" + A + "23" }));
            Assert.That(Card(UpgradeKind.LongFocus).Numbers, Is.EqualTo(new[] { "focus window  6 s" + A + "6.6 s" }));
            Assert.That(Card(UpgradeKind.SecondDash, UpgradeTier.Epic).Numbers, Is.EqualTo(new[] { "dashes held  1" + A + "2" }));
        }

        [Test]
        public void AChestCardsAfter_IsHerStatWithTheCardTaken_AndTheCardTakesNothing()
        {
            var values = new RunValues();
            OfferContext c = Start();
            c.Modifiers.Take(Step(UpgradeKind.AttackSpeed, 0.12f));
            c.Modifiers.Take(Step(UpgradeKind.CritChance, 0.08f));
            Upgrade speed = AtTier(values.TieredUpgrades.First(r => r.Kind == UpgradeKind.AttackSpeed), UpgradeTier.Common, values);
            int before = c.Modifiers.Count;
            OfferCard card = OfferTexts.ForUpgrade(speed, c);
            Assert.That(c.Modifiers.Count, Is.EqualTo(before), "the card took its upgrade back");
            var feel = new FeelValues();
            string now = (1.0 / c.Stats.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery)).ToString("0.0", CultureInfo.InvariantCulture);
            c.Modifiers.Take(speed);
            string after = (1.0 / c.Stats.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery)).ToString("0.0", CultureInfo.InvariantCulture);
            Assert.That(card.Numbers.Single(), Is.EqualTo(now + A + after + " shots a second"), "computed from her values now, not by hand");
            Assert.That(OfferTexts.ForUpgrade(Step(UpgradeKind.CritChance, 0.04f), c).Numbers.Single(), Is.EqualTo("crit chance  13 %" + A + "17 %"));
        }

        [Test]
        public void LongFocus_SaysHerWayOfTheGauge()
        {
            OfferContext c = Start();
            Upgrade u = Step(UpgradeKind.LongFocus, 0.1f);
            c.FocusMode = FocusMode.SteadyHand;
            Assert.That(OfferTexts.ForUpgrade(u, c).Numbers.Single(), Is.EqualTo("focus window  10 s" + A + "11 s"));
            c.FocusMode = FocusMode.HairTrigger;
            OfferCard hair = OfferTexts.ForUpgrade(u, c);
            Assert.That(hair.Numbers.Single(), Is.EqualTo("a part primes for  4 s" + A + "4.4 s"));
            Assert.That(hair.Sentence, Is.EqualTo("A part of focus primes your next skill for longer."));
        }

        // ---------------- A floor's reward ----------------

        [Test]
        public void ASkillCard_SaysItsKeyCooldownSentence_AndATiersNumbers()
        {
            var values = new RunValues();
            SkillDefinition[] all = SkillDefaults.Create();
            SkillDefinition longShot = all.Single(s => s.Name == "Long Shot"), slide = all.Single(s => s.Name == "Slide");
            OfferCard common = OfferTexts.ForSkill(longShot, null, "5", Start());
            Say(common.ToString().Replace("\n", "  |  "));
            Assert.That(common.Name, Is.EqualTo("Long Shot"));
            Assert.That(common.TierLine, Is.Empty, "a common card says no tier");
            Assert.That(common.For, Is.EqualTo("key 5" + OfferTexts.Dot + "cooldown 12 s" + OfferTexts.Dot + "no marks"));
            Assert.That(common.Sentence, Is.EqualTo("Hold to charge, let go to fire; let go in the band for a perfect shot."));
            Assert.That(common.Numbers, Is.EqualTo(new[] { "damage 300" + OfferTexts.Dot + "stagger 15" }));

            var rare = new SkillTierOffer { Skill = "Long Shot", Tier = UpgradeTier.Rare, Step = SkillTiers.Step(longShot, values.SkillTierNumbers) };
            OfferCard r = OfferTexts.ForSkill(longShot, rare, "5", Start());
            Say(r.ToString().Replace("\n", "  |  "));
            Assert.That(r.TierLine, Is.EqualTo("RARE   +30% damage and stagger"), "057's words");
            Assert.That(r.Numbers, Is.EqualTo(new[] { "damage  300" + A + "390", "stagger  15" + A + "19.5" }));

            SkillUpgradeDefinition overcharge = SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Long Shot", "Overcharge");
            var epic = new SkillTierOffer { Skill = "Long Shot", Tier = UpgradeTier.Epic, Step = SkillTiers.Step(longShot, values.SkillTierNumbers), Change = overcharge };
            OfferCard e = OfferTexts.ForSkill(longShot, epic, "5", Start());
            Say(e.ToString().Replace("\n", "  |  "));
            Assert.That(e.TierLine, Does.StartWith("EPIC"));
            Assert.That(e.Numbers, Is.EqualTo(new[] { "damage  300" + A + "390", "stagger  15" + A + "19.5", "perfect release  ×2" + A + "×3", "perfect band  0.2 s" + A + "0.1 s" }));
            Assert.That(e.Notes, Is.EqualTo(new[] { "+ OVERCHARGE: A perfect release deals three times the damage." }));
            Assert.That(e.Cost, Is.EqualTo("The perfect band is half as wide."), "the cost on its own line");

            var slideRare = new SkillTierOffer { Skill = "Slide", Tier = UpgradeTier.Rare, Step = SkillTiers.Step(slide, values.SkillTierNumbers) };
            OfferCard s2 = OfferTexts.ForSkill(slide, slideRare, "6", Start());
            Assert.That(s2.Numbers, Is.EqualTo(new[] { "cooldown  12 s" + A + "9.6 s" }));
            Assert.That(s2.For, Does.Contain("cooldown 9.6 s"), "the tiered skill's cooldown");
        }

        [Test]
        public void EverySkill_HasItsSentence_OfTwoLinesAtMost_SayingHowItIsPressed()
        {
            foreach (SkillDefinition s in SkillDefaults.Create())
            {
                string line = OfferTexts.SkillSentence(s);
                Say(s.Name + ": " + line);
                Assert.That(line, Is.Not.EqualTo(SkillDefaults.Line(s.Name)), s.Name + ": its own, shortened");
                Assert.That(line.Length, Is.InRange(30, 80), s.Name);
                Assert.That(line, Does.StartWith("Tap").Or.StartWith("Hold").Or.StartWith("Three taps"), s.Name + ": how it is pressed");
            }
        }

        // ---------------- The elite room's upgrades ----------------

        [Test]
        public void EveryEliteUpgrade_HasASentence_ItsNumbers_AndACostWhereItTrades()
        {
            SkillDefinition[] all = SkillDefaults.Create();
            foreach (SkillUpgradeDefinition u in SkillUpgradeTable.Create())
            {
                SkillDefinition now = all.Single(s => s.Name == u.Skill);
                OfferCard card = OfferTexts.ForSkillUpgrade(u, now, "2", Start());
                Say(card.ToString().Replace("\n", "  |  "));
                Assert.That(card.Name, Is.EqualTo(u.Name));
                StringAssert.StartsWith(u.Skill + OfferTexts.Dot + "key 2", card.For);
                Assert.That(card.Sentence, Does.EndWith("."));
                Assert.That(card.Sentence.Length, Is.InRange(15, 80), u.FullName);
                Assert.That(card.Numbers, Is.Not.Empty, u.FullName + ": what it changes, in numbers");
            }
            SkillDefinition longShot = all.Single(s => s.Name == "Long Shot");
            OfferCard snap = OfferTexts.ForSkillUpgrade(SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Long Shot", "Snap Shot"), longShot, "5", Start());
            Assert.That(snap.Numbers, Is.EqualTo(new[] { "cooldown  12 s" + A + "18 s", "charge time  1 s" + A + "none" }));
            Assert.That(snap.Cost, Is.EqualTo("Its cooldown is 6 s longer."));
            OfferCard heavy = OfferTexts.ForSkillUpgrade(SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Quick Shot", "Heavy Round"), all[0], "1", Start());
            Assert.That(heavy.Numbers, Is.EqualTo(new[] { "damage  100" + A + "160", "stagger  3" + A + "6" }));
            Assert.That(heavy.Cost, Is.Empty);
            // As she has the skill now: a Heavy Round on a Quick Shot she took at rare starts from the rare's numbers.
            SkillDefinition rareQuick = RunSkillUpgrades.Patched(all[0], new[] { SkillTiers.Step(all[0], new SkillTierValues()) });
            Assert.That(OfferTexts.ForSkillUpgrade(SkillUpgradeTable.Find(SkillUpgradeTable.Create(), "Quick Shot", "Heavy Round"), rareQuick, "1", Start()).Numbers[0],
                        Is.EqualTo("damage  130" + A + "208"));
        }

        // ---------------- The focus choice ----------------

        [Test]
        public void TheFocusChoices_SayWhatChanges_AgainstTodaysGauge()
        {
            OfferCard hair = OfferTexts.ForFocus(FocusMode.HairTrigger, Start());
            OfferCard steady = OfferTexts.ForFocus(FocusMode.SteadyHand, Start());
            Say(hair.ToString().Replace("\n", "  |  "));
            Say(steady.ToString().Replace("\n", "  |  "));
            Assert.That(hair.Name, Is.EqualTo("Hair Trigger"));
            Assert.That(hair.Numbers, Is.EqualTo(new[] { "Z spends  the full gauge" + A + "one part of 3", "cooldowns cut by  40 %" + A + "15 %", "next skill primed  no" + A + "for 4 s" }));
            Assert.That(hair.Cost, Is.Not.Empty, "what it takes away");
            Assert.That(steady.Numbers, Is.EqualTo(new[] { "window  6 s" + A + "10 s", "Triple Tap in it  3" + A + "5 taps", "Scatter Blast in it  1" + A + "2 blasts" }));
            Assert.That(steady.Sentence.Length, Is.LessThanOrEqualTo(80));
            Assert.That(hair.Sentence.Length, Is.LessThanOrEqualTo(80));
        }

        // ---------------- The six new kinds ----------------

        [Test]
        public void TheSixNewKinds_TheirSteps_TheirCaps_AndNothingWithTheSwitchOff()
        {
            var values = new RunValues();
            UpgradeDefinition Row(UpgradeKind k) => values.TieredUpgrades.Single(r => r.Kind == k);
            float[] Steps(UpgradeKind k) => UpgradeTiers.All.Where(t => t >= Row(k).MinTier).Select(t => TieredOffers.StepOf(Row(k), t, values.Stats)).ToArray();
            Assert.That(Steps(UpgradeKind.AllCooldowns), Is.EqualTo(new[] { 0.03f, 0.05f, 0.08f }));
            Assert.That(Steps(UpgradeKind.CleanPress), Is.EqualTo(new[] { 0.08f, 0.14f, 0.20f }));
            Assert.That(Steps(UpgradeKind.MarkedPrey), Is.EqualTo(new[] { 0.06f, 0.10f, 0.15f }));
            Assert.That(Steps(UpgradeKind.Mending), Is.EqualTo(new[] { 3f, 5f, 8f }));
            Assert.That(Steps(UpgradeKind.LongFocus), Is.EqualTo(new[] { 0.10f, 0.18f, 0.25f }));
            Assert.That(Steps(UpgradeKind.SecondDash), Is.EqualTo(new[] { 1f }), "an epic only");
            Assert.That(Row(UpgradeKind.SecondDash).MaxTaken, Is.EqualTo(1));
            // The older rows keep work order 050's steps exactly (the producer, 2026-11-21: "do not retune the older steps").
            Assert.That(Row(UpgradeKind.RunSpeed).Amount, Is.EqualTo(0.05f));
            Assert.That(Row(UpgradeKind.RunSpeed).RareAmount + Row(UpgradeKind.RunSpeed).EpicAmount, Is.Zero);
            Assert.That(Row(UpgradeKind.DashCooldown).Amount, Is.EqualTo(0.08f));
            Assert.That(Row(UpgradeKind.HeavyHits).Amount, Is.EqualTo(0.1f));
            Assert.That(Row(UpgradeKind.MaxHealth).Amount, Is.EqualTo(10f));

            RunModifiers m = StatsDpsModel.Start(values);
            HerStats s = m.Stats;
            Assert.That(s.AllCooldowns + s.Mending + s.ExtraDashes, Is.Zero);
            Assert.That(s.CleanPress * s.MarkedPrey * s.LongFocus, Is.EqualTo(1f));
            Assert.That(s.DashCharges, Is.EqualTo(1));
            m.Take(Step(UpgradeKind.AllCooldowns, 0.05f));
            m.Take(Step(UpgradeKind.Cooldown, 0.05f, skill: "Quick Shot"));
            m.Take(Step(UpgradeKind.CleanPress, 0.14f));
            m.Take(Step(UpgradeKind.MarkedPrey, 0.1f));
            m.Take(Step(UpgradeKind.Mending, 5f));
            m.Take(Step(UpgradeKind.LongFocus, 0.18f));
            m.Take(Step(UpgradeKind.SecondDash, 1f));
            Assert.That(s.AllCooldowns, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(s.CooldownShare("Quick Shot"), Is.EqualTo(0.9f).Within(1e-6f), "beside a skill's own cut");
            Assert.That(s.CooldownShare("Backstep"), Is.EqualTo(0.95f).Within(1e-6f), "every skill");
            Assert.That(m.CooldownShare("Backstep"), Is.EqualTo(s.CooldownShare("Backstep")), "read where cooldowns are");
            Assert.That(s.CleanPress, Is.EqualTo(1.14f).Within(1e-6f));
            Assert.That(s.MarkedPrey, Is.EqualTo(1.1f).Within(1e-6f));
            Assert.That(s.Mending, Is.EqualTo(5));
            Assert.That(s.LongFocus, Is.EqualTo(1.18f).Within(1e-6f));
            Assert.That(s.DashCharges, Is.EqualTo(2));
            // The caps.
            for (int i = 0; i < 20; i++)
            {
                m.Take(Step(UpgradeKind.AllCooldowns, 0.08f));
                m.Take(Step(UpgradeKind.Cooldown, 0.05f, skill: "Quick Shot"));
                m.Take(Step(UpgradeKind.CleanPress, 0.2f));
                m.Take(Step(UpgradeKind.MarkedPrey, 0.15f));
                m.Take(Step(UpgradeKind.Mending, 8f));
                m.Take(Step(UpgradeKind.LongFocus, 0.25f));
                m.Take(Step(UpgradeKind.SecondDash, 1f));
            }
            Assert.That(s.AllCooldowns, Is.EqualTo(0.2f).Within(1e-6f), "every skill at most 20 % shorter");
            Assert.That(s.CooldownShare("Backstep"), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(s.CooldownShare("Quick Shot"), Is.EqualTo(values.CooldownFloor), "never under the floor");
            Assert.That(s.CleanPress, Is.EqualTo(1.5f).Within(1e-6f));
            Assert.That(s.MarkedPrey, Is.EqualTo(1.3f).Within(1e-6f));
            Assert.That(s.Mending, Is.EqualTo(15));
            Assert.That(s.LongFocus, Is.EqualTo(1.5f).Within(1e-6f));
            Assert.That(s.DashCharges, Is.EqualTo(2));
            foreach (UpgradeKind k in new[] { UpgradeKind.AllCooldowns, UpgradeKind.CleanPress, UpgradeKind.MarkedPrey, UpgradeKind.Mending, UpgradeKind.LongFocus, UpgradeKind.SecondDash })
                Assert.That(TieredOffers.Useful(Row(k), Four, values, values.Stats, m, false), Is.False, k + " at its cap is never offered");
            // Off: none of them does anything.
            m.StatsAndTiers = false;
            Assert.That(s.AllCooldowns + s.Mending + s.ExtraDashes, Is.Zero);
            Assert.That(s.CleanPress * s.MarkedPrey * s.LongFocus, Is.EqualTo(1f));
            // In words: the pause menu's list and the summary's line name them.
            m.StatsAndTiers = true;
            List<string> lines = s.Lines(0.225f, 10f, 100);
            Assert.That(lines.Single(l => l.StartsWith("Clean press")), Is.EqualTo("Clean press  +50 %  (+0 %)"));
            Assert.That(lines.Single(l => l.StartsWith("Marked prey")), Is.EqualTo("Marked prey  +30 %  (+0 %)"));
            Assert.That(lines.Single(l => l.StartsWith("Mending")), Is.EqualTo("Mending  +15 a room  (+0)"));
            Assert.That(lines.Single(l => l.StartsWith("Focus time")), Is.EqualTo("Focus time  +50 %  (+0 %)"));
            Assert.That(lines.Single(l => l.StartsWith("Dashes")), Is.EqualTo("Dashes  2  (1)"));
            StringAssert.StartsWith("Cooldowns  every skill -20 %, Quick Shot -50 %", lines.Single(l => l.StartsWith("Cooldowns")));
            StringAssert.EndsWith(", cd all -20%, clean +50%, marked +30%, room heal +15, focus time +50%, dashes 2", s.OneLine(0.225f, 100));
        }

        // ---------------- Three in a chest ----------------

        /// <summary>The table of work order 050, before this order's six, with its two a chest: to compare how often each kind came up.</summary>
        static RunValues Before065()
        {
            var v = new RunValues { TieredUpgradesOffered = 2 };
            v.TieredUpgrades = v.TieredUpgrades.Where(r => r.Kind <= UpgradeKind.Luck).ToArray();
            return v;
        }

        /// <summary>
        /// Runs of five floors, two chests a floor (the floor plan's two treasure rooms), each chest's first card taken (as the
        /// test pilot takes it), her hurt in one run of two: <paramref name="runs"/> times ten offers.
        /// </summary>
        static Dictionary<UpgradeKind, int> Offers(RunValues values, int runs, out int offers, out int cards, Action<List<Upgrade>, RunModifiers> each = null)
        {
            var seen = new Dictionary<UpgradeKind, int>();
            offers = cards = 0;
            for (int run = 1; run <= runs; run++)
            {
                RunModifiers m = StatsDpsModel.Start(values);
                ulong seed = (ulong)run * 104729UL;
                for (int k = 0; k < 10; k++)
                {
                    List<Upgrade> offer = ChestUpgrades.Offer(seed, 1 + k / 2, 2 + 3 * (k % 2), Four, values, m, run % 2 == 0);
                    each?.Invoke(offer, m);
                    offers++;
                    cards += offer.Count;
                    foreach (UpgradeKind kind in offer.Select(u => u.Kind).Distinct()) seen[kind] = seen.TryGetValue(kind, out int n) ? n + 1 : 1;
                    if (offer.Count > 0) m.Take(offer[0]);
                }
            }
            return seen;
        }

        [Test]
        public void TenThousandSeededOffers_ShowThreeDifferentStats_AndHowOftenEachKindComesUp()
        {
            var values = new RunValues();
            Assert.That(values.ChestOffers, Is.EqualTo(3));
            Dictionary<UpgradeKind, int> now = Offers(values, 1000, out int offers, out int cards, (offer, m) =>
            {
                Assert.That(offer.Count, Is.EqualTo(3), "three a chest");
                Assert.That(offer.Select(u => u.Stat).Distinct().Count(), Is.EqualTo(3), "of three different stats");
            });
            Assert.That(offers, Is.EqualTo(10000));
            Dictionary<UpgradeKind, int> then = Offers(Before065(), 1000, out int offersThen, out _);
            Say("Kind: share of 10 000 offers that show it, this order (three a chest, 22 rows)  /  before it (two a chest, 16 rows)");
            foreach (UpgradeKind kind in Enum.GetValues(typeof(UpgradeKind)).Cast<UpgradeKind>())
            {
                double a = now.TryGetValue(kind, out int x) ? x / (double)offers : 0.0, b = then.TryGetValue(kind, out int y) ? y / (double)offersThen : 0.0;
                Say(string.Format(CultureInfo.InvariantCulture, "  {0,-14} {1,6:0.0} %   {2,6:0.0} %", kind, a * 100.0, b * 100.0));
            }
            // The stats he asked for on 2026-11-20 are not crowded out by the new ones: at least half as often as before.
            foreach (UpgradeKind kind in new[] { UpgradeKind.AttackSpeed, UpgradeKind.FlatDamage, UpgradeKind.CritChance, UpgradeKind.CritDamage })
                Assert.That(now[kind] / (double)offers, Is.GreaterThan(0.5 * then[kind] / offersThen), kind + " offered at least half as often as before");
            foreach (UpgradeKind kind in new[] { UpgradeKind.AllCooldowns, UpgradeKind.CleanPress, UpgradeKind.MarkedPrey, UpgradeKind.Mending, UpgradeKind.LongFocus })
                Assert.That(now[kind] / (double)offers, Is.InRange(0.05, 0.3), kind + ": comes up");
            Assert.That(now[UpgradeKind.SecondDash] / (double)offers, Is.InRange(0.001, 0.05), "the second dash: an epic, rare");
        }

        [Test]
        public void AShortPool_OffersWhatThereIs_AndTheHigherTiersAreTriedFirst()
        {
            var values = new RunValues
            {
                TieredUpgrades = new[] { new UpgradeDefinition(UpgradeKind.FlatDamage, 3f), new UpgradeDefinition(UpgradeKind.Damage, 0.15f, minTier: UpgradeTier.Epic) },
            };
            for (ulong seed = 1; seed <= 300; seed++)
            {
                List<Upgrade> offer = ChestUpgrades.Offer(seed, 1, 2, Four, values, StatsDpsModel.Start(values), false);
                Assert.That(offer.Count, Is.EqualTo(1), "flat damage and the damage share are one stat: one card");
            }
            values.TieredUpgrades = new[] { new UpgradeDefinition(UpgradeKind.FlatDamage, 3f), new UpgradeDefinition(UpgradeKind.Luck, 1f) };
            Assert.That(ChestUpgrades.Offer(9, 1, 2, Four, values, StatsDpsModel.Start(values), false).Count, Is.EqualTo(2), "two stats: two cards");
        }

        [Test]
        public void WithTheStatsOff_TheChestIsTheOldChest_OfTwo()
        {
            var values = new RunValues { StatsAndTiers = false };
            Assert.That(values.ChestOffers, Is.EqualTo(2));
            Assert.That(values.UpgradesOffered, Is.EqualTo(2), "the old count");
            for (ulong seed = 1; seed <= 500; seed++)
            {
                List<Upgrade> offer = ChestUpgrades.Offer(seed, 1 + (int)(seed % 5), 2, Four, values, StatsDpsModel.Start(values), seed % 2 == 0);
                Assert.That(offer.Count, Is.EqualTo(2));
                Assert.That(offer.All(u => !u.Tiered && u.Kind < UpgradeKind.FlatDamage), Is.True, "the old table: no tier, no new kind");
            }
        }

        [Test]
        public void AFiveFloorRun_OpensTenChests()
        {
            var level = new LevelValues();
            var counts = new List<int>();
            for (ulong seed = 1; seed <= 40; seed++)
            {
                int chests = 0;
                for (int floor = 1; floor <= level.FloorsPerAct; floor++)
                {
                    LevelLayout layout = LevelGenerator.Generate(RunSeeds.FloorSeed(seed * 7919UL, floor), floor, level);
                    chests += layout.Rooms.Sum(r => r.Props.Count(p => p.Model == PropModel.Chest));
                }
                counts.Add(chests);
            }
            Say("chests in a run of five floors over 40 runs: min " + counts.Min() + ", max " + counts.Max() + ", mean " + counts.Average().ToString("0.0", CultureInfo.InvariantCulture));
            Assert.That(counts.All(n => n == 2 * level.FloorsPerAct), Is.True, "two treasure rooms a floor, a chest in each");
        }

        // ---------------- The pop-up's place ----------------

        [TestCase(1920f, 3)]
        [TestCase(1920f, 4)]
        [TestCase(1440f, 3)]
        [TestCase(1440f, 4)]
        public void TheRow_StandsInTheMiddle_NeverWiderThanItsShare(float viewWidth, int count)
        {
            var v = new OfferValues();
            const float viewHeight = 1080f;
            float height = v.CardHeight;
            float scale = OfferLayout.Scale(count, height, viewWidth, v);
            float row = OfferLayout.RowWidth(count, height, scale, v);
            Say(string.Format(CultureInfo.InvariantCulture, "{0} cards in a view {1} wide: scale {2:0.000}, row {3:0} ({4:0.0} %)", count, viewWidth, scale, row, row / viewWidth * 100f));
            Assert.That(row, Is.LessThanOrEqualTo(v.MostRowShare * viewWidth + 0.01f));
            if (count == 3 && viewWidth >= 1920f) Assert.That(scale, Is.EqualTo(1f), "three cards at 16:9 at their full size");
            UiBox first = OfferLayout.Card(0, count, height, scale, viewWidth, viewHeight, v), last = OfferLayout.Card(count - 1, count, height, scale, viewWidth, viewHeight, v);
            Assert.That((first.X + last.Right) * 0.5f, Is.EqualTo(viewWidth * 0.5f).Within(0.01f), "centred");
            Assert.That(first.X - v.Slant * height * 0.5f, Is.GreaterThan(0f), "its lean inside the view");
            Assert.That(last.Right + v.Slant * height * 0.5f, Is.LessThan(viewWidth));
            Assert.That(first.CenterY, Is.EqualTo(viewHeight * v.RowMiddle).Within(0.01f), "in the middle band");
            for (int i = 1; i < count; i++)
                Assert.That(OfferLayout.Card(i, count, height, scale, viewWidth, viewHeight, v).X - OfferLayout.Card(i - 1, count, height, scale, viewWidth, viewHeight, v).Right,
                            Is.EqualTo(v.CardGap * scale).Within(0.01f), "side by side, a gap between, never overlapping");
            Assert.That(first.W, Is.EqualTo(380f * scale).Within(0.01f));
        }

        [Test]
        public void ACardsHit_IsItsLeaningShape()
        {
            var r = new UiBox(100f, 100f, 380f, 300f);
            const float slant = 0.16f;
            Assert.That(OfferLayout.Hit(r, r.Right + slant * 150f - 3f, r.Y + 2f, slant), Is.True, "the seen upper right tip");
            Assert.That(OfferLayout.Hit(r, r.X - slant * 150f + 3f, r.Bottom - 2f, slant), Is.True, "the seen lower left tip");
            Assert.That(OfferLayout.Hit(r, r.Right - 3f, r.Bottom - 2f, slant), Is.False, "beside the seen lower right corner");
            Assert.That(OfferLayout.Hit(r, r.X + 3f, r.Y + 2f, slant), Is.False);
            Assert.That(OfferLayout.Hit(r, r.CenterX, r.CenterY, slant), Is.True);
        }
    }
}
