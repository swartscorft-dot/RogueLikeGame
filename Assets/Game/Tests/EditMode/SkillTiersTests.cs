using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 057 (decision 0013; Kristian, 2026-11-20: "vi har ikke noe tiers på upgrades og spells", and his choice of
    /// 2026-11-21, "Tiers på ferdigheter"): the floor reward's skill tiers as rules. Every skill's step and preview, the
    /// odds and luck's lean, floor 1's rule, an epic's change, the stream of their own, the run's list of skill upgrades
    /// with a tier taken, the record's and the summary's words, the switch, and the measurements of "keeping the game where
    /// it is" pinned as ranges.
    /// </summary>
    public class SkillTiersTests
    {
        static readonly SkillUpgradeDefinition[] s_Table = SkillUpgradeTable.Create();
        static readonly SkillDefinition[] s_Skills = SkillDefaults.Create();
        static readonly StatValues s_Stats = new StatValues();

        static SkillDefinition Skill(string name) => s_Skills.First(s => s.Name == name);
        static int SlotOf(string name) => Array.FindIndex(s_Skills, s => s.Name == name);
        static List<bool> StartHas() => s_Skills.Select(s => s.StartEquipped).ToList();
        static ulong Seed(int i) => LevelRandom.Mix((ulong)i * 0x9E3779B97F4A7C15UL + 12345UL) & LevelSeed.ShortMask;

        static List<SkillTierOffer> DrawFor(ulong seed, int floor, IReadOnlyList<int> slots, IReadOnlyList<SkillUpgradeDefinition> taken = null,
                                            float luck = 0f, SkillTierValues v = null, int draw = 0) =>
            SkillTiers.Draw(seed, floor, draw, slots, s_Skills, s_Table, taken ?? Array.Empty<SkillUpgradeDefinition>(), luck, v ?? new SkillTierValues(), s_Stats);

        // ---------------- The step and the preview ----------------

        [Test]
        public void Step_ForEveryOneOfTheTwelveSkills_IsADamageAndStaggerStep_SlidesACooldownStep()
        {
            var v = new SkillTierValues();
            Assert.That(s_Skills.Length, Is.EqualTo(12));
            foreach (SkillDefinition s in s_Skills)
            {
                SkillUpgradeDefinition step = SkillTiers.Step(s, v);
                Assert.That(step.Skill, Is.EqualTo(s.Name));
                Assert.That(step.Name, Is.EqualTo("rare"), "the word, not shown as an upgrade");
                Assert.That(step.Kind, Is.EqualTo(SkillUpgradeKind.Numbers));
                Assert.That(step.TierStep, Is.True, s.Name);
                Assert.That(s_Table.Any(u => u.TierStep), Is.False, "never in the table");
                if (s.Name == "Slide")
                {
                    Assert.That(s.Damage, Is.Zero);
                    Assert.That(step.Patches.Select(p => p.ToString()), Is.EqualTo(new[] { "Cooldown x 0.8" }));
                    Assert.That(step.Line, Is.EqualTo("-20% cooldown"));
                }
                else
                {
                    Assert.That(s.Damage, Is.GreaterThan(0), s.Name);
                    Assert.That(step.Patches.Select(p => p.ToString()), Is.EqualTo(new[] { "Damage x 1.3", "Stagger x 1.3" }), s.Name);
                    Assert.That(step.Line, Is.EqualTo("+30% damage and stagger"));
                }
            }
        }

        [Test]
        public void Preview_IsTheSkillAtItsTier_AndTheSkillItselfForACommon()
        {
            var v = new SkillTierValues();
            foreach (SkillDefinition s in s_Skills)
            {
                var common = new SkillTierOffer { Slot = SlotOf(s.Name), Skill = s.Name, Tier = UpgradeTier.Common };
                Assert.That(SkillTiers.Preview(s, common), Is.SameAs(s), "a common is the skill as it is");
                Assert.That(SkillTiers.Preview(s, null), Is.SameAs(s));
                var rare = new SkillTierOffer { Slot = SlotOf(s.Name), Skill = s.Name, Tier = UpgradeTier.Rare, Step = SkillTiers.Step(s, v) };
                SkillDefinition r = SkillTiers.Preview(s, rare);
                Assert.That(r, Is.Not.SameAs(s), "a copy");
                if (s.Name == "Slide")
                {
                    Assert.That(r.Cooldown, Is.EqualTo(s.Cooldown * 0.8f).Within(1e-4f));
                    Assert.That(r.Damage, Is.Zero);
                }
                else
                {
                    Assert.That(r.Damage, Is.EqualTo((int)Math.Round(s.Damage * (double)1.3f, MidpointRounding.AwayFromZero)), s.Name);
                    Assert.That(r.Stagger, Is.EqualTo(s.Stagger * 1.3f).Within(1e-3f), s.Name);
                    Assert.That(r.Cooldown, Is.EqualTo(s.Cooldown), s.Name);
                }
            }
            Assert.That(Skill("Long Shot").Damage, Is.EqualTo(300), "nothing written to the skill");
            // Named numbers, for the report: Long Shot 300 to 390, Fan Fire 30 to 39, Slide 12 s to 9.6 s.
            Assert.That(SkillTiers.Preview(Skill("Long Shot"), new SkillTierOffer { Tier = UpgradeTier.Rare, Step = SkillTiers.Step(Skill("Long Shot"), v) }).Damage, Is.EqualTo(390));
            Assert.That(SkillTiers.Preview(Skill("Fan Fire"), new SkillTierOffer { Tier = UpgradeTier.Rare, Step = SkillTiers.Step(Skill("Fan Fire"), v) }).Damage, Is.EqualTo(39));
        }

        [Test]
        public void Preview_OfAnEpic_HasTheStepAndItsChange()
        {
            var v = new SkillTierValues();
            SkillDefinition s = Skill("Long Shot");
            var epic = new SkillTierOffer
            {
                Skill = s.Name, Tier = UpgradeTier.Epic, Step = SkillTiers.Step(s, v),
                Change = SkillUpgradeTable.Find(s_Table, "Long Shot", "Snap Shot"),
            };
            Assert.That(epic.Rows.Count, Is.EqualTo(2));
            Assert.That(epic.Rows[0].TierStep, Is.True, "the step first");
            Assert.That(epic.Rows[1].Name, Is.EqualTo("Snap Shot"), "then the change");
            SkillDefinition p = SkillTiers.Preview(s, epic);
            Assert.That(p.Damage, Is.EqualTo(390));
            Assert.That(p.NoCharge, Is.True);
            Assert.That(p.Cooldown, Is.EqualTo(18f), "the card's cooldown is the epic's: Snap Shot's 6 s more");
        }

        // ---------------- The odds ----------------

        [TestCase(0f)]
        [TestCase(3f)]
        [TestCase(8f)]
        public void TheOdds_OverAHundredThousandDraws_AreTheSharesLuckGives(float luck)
        {
            const int draws = 100000;
            const double tolerance = 0.006; // about four standard deviations of a share near one half over 100 000 draws
            var v = new SkillTierValues();
            double[] shares = UpgradeTiers.Shares(v.Odds, luck, s_Stats);
            var counts = new int[3];
            int[] slots = { SlotOf("Long Shot") }; // a skill with changes open, so an epic stays an epic
            for (int i = 0; i < draws; i++)
                counts[(int)DrawFor(Seed(i), 2, slots, luck: luck)[0].Tier]++;
            for (int t = 0; t < 3; t++)
                Assert.That(counts[t] / (double)draws, Is.EqualTo(shares[t]).Within(tolerance), UpgradeTiers.Word((UpgradeTier)t) + " at luck " + luck);
            if (luck == 0f) Assert.That(shares, Is.EqualTo(new[] { 0.6, 0.3, 0.1 }).Within(1e-9));
        }

        // ---------------- Floor 1 ----------------

        [Test]
        public void FloorOne_OverTenThousandRunSeeds_AlwaysShowsARareOrBetter_TheLastCardRaised()
        {
            var off = new SkillTierValues { FirstRewardAtLeastRare = false };
            int raised = 0, raisedEpic = 0;
            for (int i = 0; i < 10000; i++)
            {
                ulong seed = Seed(i);
                List<int> slots = FloorRewards.Offer(seed, 1, StartHas(), 3);
                List<SkillTierOffer> on = DrawFor(seed, 1, slots);
                List<SkillTierOffer> without = DrawFor(seed, 1, slots, v: off);
                Assert.That(on.Any(o => o.Tiered), Is.True, "seed " + seed);
                if (without.Any(o => o.Tiered))
                {
                    Assert.That(Words(on), Is.EqualTo(Words(without)), "untouched when one was drawn");
                    continue;
                }
                raised++;
                for (int c = 0; c < on.Count - 1; c++) Assert.That(on[c].Tier, Is.EqualTo(UpgradeTier.Common), "the cards before the last stay");
                Assert.That(on[on.Count - 1].Tiered, Is.True, "the last card is the one raised");
                if (on[on.Count - 1].Tier == UpgradeTier.Epic) raisedEpic++;
            }
            Assert.That(raised / 10000.0, Is.EqualTo(0.216).Within(0.02), "0.6 cubed of the runs needed it");
            Assert.That(raisedEpic / (double)raised, Is.EqualTo(0.25).Within(0.04), "raised from rare and epic in their shares' proportion, 30 to 10");
        }

        [Test]
        public void TheFloorOneRule_LeavesTheOtherFloorsUntouched()
        {
            var off = new SkillTierValues { FirstRewardAtLeastRare = false };
            for (int i = 0; i < 3000; i++)
            {
                ulong seed = Seed(i);
                for (int floor = 2; floor <= 4; floor++)
                {
                    List<int> slots = FloorRewards.Offer(seed, floor, StartHas(), 3);
                    Assert.That(Words(DrawFor(seed, floor, slots)), Is.EqualTo(Words(DrawFor(seed, floor, slots, v: off))));
                }
            }
        }

        static List<string> Words(List<SkillTierOffer> offers) => offers.Select(o => o.Slot + " " + o).ToList();

        // ---------------- The epic's change ----------------

        [Test]
        public void AnEpicsChange_IsAChangeOfThatSkill_AndOpen()
        {
            int epics = 0;
            var allEpic = new SkillTierValues { Odds = new[] { 0f, 0f, 100f } };
            for (int i = 0; i < 2000; i++)
            {
                ulong seed = Seed(i);
                List<int> slots = FloorRewards.Offer(seed, 2, StartHas(), 3);
                foreach (SkillTierOffer o in DrawFor(seed, 2, slots, v: allEpic))
                {
                    Assert.That(o.Tier, Is.EqualTo(UpgradeTier.Epic));
                    Assert.That(o.Change, Is.Not.Null);
                    Assert.That(o.Change.Kind, Is.EqualTo(SkillUpgradeKind.Change), o.ToString());
                    Assert.That(o.Change.Skill, Is.EqualTo(o.Skill), o.ToString());
                    Assert.That(SkillUpgradeOffer.Open(s_Table, new[] { o.Skill }, Array.Empty<SkillUpgradeDefinition>()), Does.Contain(o.Change));
                    Assert.That(o.Rows, Is.EqualTo(new[] { o.Step, o.Change }));
                    epics++;
                }
            }
            Assert.That(epics, Is.EqualTo(6000));
        }

        [Test]
        public void ASkillWithNoChangeOpen_ComesAsRare()
        {
            var allEpic = new SkillTierValues { Odds = new[] { 0f, 0f, 100f } };
            // Snap Shot taken: Overcharge is excluded, and Long Shot has no change left.
            var taken = new[] { SkillUpgradeTable.Find(s_Table, "Long Shot", "Snap Shot") };
            Assert.That(SkillTiers.OpenChanges(s_Table, "Long Shot", taken), Is.Empty);
            for (int i = 0; i < 200; i++)
            {
                List<SkillTierOffer> offer = DrawFor(Seed(i), 2, new[] { SlotOf("Long Shot"), SlotOf("Fan Fire") }, taken, v: allEpic);
                Assert.That(offer[0].Tier, Is.EqualTo(UpgradeTier.Rare), "no change open: rare");
                Assert.That(offer[0].Change, Is.Null);
                Assert.That(offer[0].Rows, Is.EqualTo(new[] { offer[0].Step }));
                Assert.That(offer[1].Tier, Is.EqualTo(UpgradeTier.Epic));
                Assert.That(offer[1].Change.Name, Is.EqualTo("Walking Fire"), "Fan Fire's one change");
            }
        }

        // ---------------- The stream ----------------

        [Test]
        public void TheTiersStream_LeavesTheFloorRewardsSlotsTheSame_SeedForSeed()
        {
            for (int i = 0; i < 10000; i++)
            {
                ulong seed = Seed(i);
                int floor = 1 + i % 4;
                List<int> before = FloorRewards.Offer(seed, floor, StartHas(), 3);
                List<SkillTierOffer> tiers = DrawFor(seed, floor, before);
                Assert.That(tiers.Select(t => t.Slot), Is.EqualTo(before), "one per slot, in the same order");
                Assert.That(FloorRewards.Offer(seed, floor, StartHas(), 3), Is.EqualTo(before), "seed " + seed);
            }
            Assert.That(LevelRandom.Hash(SkillTiers.Stage), Is.Not.EqualTo(LevelRandom.Hash(FloorRewards.Stage)), "a stream of its own");
        }

        [Test]
        public void TheSameSeed_WithTheSamePicks_GivesTheSameTiers_AndDrawOneOthers()
        {
            int differ = 0;
            for (int i = 0; i < 1000; i++)
            {
                ulong seed = Seed(i);
                List<int> slots = FloorRewards.Offer(seed, 3, StartHas(), 3);
                Assert.That(Words(DrawFor(seed, 3, slots)), Is.EqualTo(Words(DrawFor(seed, 3, slots))));
                if (!Words(DrawFor(seed, 3, slots)).SequenceEqual(Words(DrawFor(seed, 3, slots, draw: 1)))) differ++;
            }
            Assert.That(differ, Is.GreaterThan(800), "Second Look's draw is another (the same three tiers by chance about one time in ten)");
        }

        [Test]
        public void NoSlots_NoTiers()
        {
            Assert.That(DrawFor(1, 1, new int[0]), Is.Empty);
            Assert.That(DrawFor(1, 1, null), Is.Empty);
        }

        // ---------------- The run's skill upgrades ----------------

        static SkillTierOffer Epic(string skill, string change) => new SkillTierOffer
        {
            Slot = SlotOf(skill), Skill = skill, Tier = UpgradeTier.Epic, Step = SkillTiers.Step(Skill(skill), new SkillTierValues()),
            Change = SkillUpgradeTable.Find(s_Table, skill, change),
        };

        static SkillTierOffer Rare(string skill) => new SkillTierOffer
        {
            Slot = SlotOf(skill), Skill = skill, Tier = UpgradeTier.Rare, Step = SkillTiers.Step(Skill(skill), new SkillTierValues()),
        };

        [Test]
        public void TakeTier_PatchesTheView_CountsTheEpicsChange_AndNotTheStep()
        {
            var run = new RunSkillUpgrades();
            run.TakeTier(Epic("Long Shot", "Overcharge"));
            run.TakeTier(Rare("Fan Fire"));
            run.TakeTier(new SkillTierOffer { Skill = "Slide", Tier = UpgradeTier.Common });
            IReadOnlyList<SkillDefinition> view = run.View(s_Skills);
            SkillDefinition longShot = view[SlotOf("Long Shot")];
            Assert.That(longShot.Damage, Is.EqualTo(390));
            Assert.That(longShot.Stagger, Is.EqualTo(19.5f).Within(1e-3f));
            Assert.That(longShot.PerfectScale, Is.EqualTo(3f), "Overcharge");
            Assert.That(longShot.PerfectTo - longShot.PerfectFrom, Is.EqualTo(0.1f).Within(1e-4f), "half as wide");
            Assert.That(view[SlotOf("Fan Fire")].Damage, Is.EqualTo(39));
            Assert.That(view[SlotOf("Slide")].Cooldown, Is.EqualTo(12f), "a common takes nothing");
            Assert.That(run.TierOf("Long Shot"), Is.EqualTo(UpgradeTier.Epic));
            Assert.That(run.TierOf("Fan Fire"), Is.EqualTo(UpgradeTier.Rare));
            Assert.That(run.TierOf("Slide"), Is.EqualTo(UpgradeTier.Common));
            Assert.That(run.TierOf("Quick Shot"), Is.EqualTo(UpgradeTier.Common));
            Assert.That(run.CountFor("Long Shot"), Is.EqualTo(1), "the epic's change is a pip");
            Assert.That(run.CountFor("Fan Fire"), Is.Zero, "the step is none");
            Assert.That(run.NamesFor("Long Shot"), Is.EqualTo(new[] { "Overcharge" }));
            Assert.That(run.NamesFor("Fan Fire"), Is.Empty);
            Assert.That(run.Text(), Is.EqualTo("Long Shot: Overcharge"));
            Assert.That(run.Count, Is.EqualTo(3), "Taken and Count as they are: both steps and the change");
            Assert.That(run.Taken[0].TierStep && run.Taken[1].Name == "Overcharge" && run.Taken[2].TierStep, Is.True, "in the order taken");
            Assert.That(run.Has("Long Shot", "Overcharge"), Is.True);
            Assert.That(Skill("Long Shot").Damage, Is.EqualTo(300), "nothing written to the settings' skill");

            var rareOnly = new RunSkillUpgrades();
            rareOnly.TakeTier(Rare("Tag Round"));
            Assert.That(rareOnly.Text(), Is.EqualTo("none"), "a step alone has no name in the lists");
        }

        [Test]
        public void TheElitesOffer_NeverOffersTheEpicsChange_NorWhatItExcludes()
        {
            var run = new RunSkillUpgrades();
            run.TakeTier(Epic("Long Shot", "Snap Shot"));
            var had = new HashSet<string> { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap", "Long Shot" };
            int longShots = 0;
            for (int i = 0; i < 2000; i++)
                foreach (SkillUpgradeDefinition u in SkillUpgradeOffer.Offer(Seed(i), 1 + i % 4, s_Table, had, run.Taken, 3))
                {
                    Assert.That(u.Name, Is.Not.EqualTo("Snap Shot"), "the epic's change is taken");
                    Assert.That(u.Name, Is.Not.EqualTo("Overcharge"), "and Snap Shot excludes it");
                    Assert.That(u.TierStep, Is.False, "a step is never offered");
                    if (u.Skill == "Long Shot") longShots++;
                }
            Assert.That(longShots, Is.Zero, "Long Shot has nothing left in the table");

            var other = new RunSkillUpgrades();
            other.TakeTier(Epic("Long Shot", "Overcharge"));
            for (int i = 0; i < 2000; i++)
                foreach (SkillUpgradeDefinition u in SkillUpgradeOffer.Offer(Seed(i), 1 + i % 4, s_Table, had, other.Taken, 3))
                    Assert.That(u.Name, Is.Not.EqualTo("Snap Shot").And.Not.EqualTo("Overcharge"));

            var rare = new RunSkillUpgrades();
            rare.TakeTier(Rare("Powder Keg"));
            had.Add("Powder Keg");
            bool twin = false;
            for (int i = 0; i < 2000 && !twin; i++)
                twin = SkillUpgradeOffer.Offer(Seed(i), 2, s_Table, had, rare.Taken, 3).Any(u => u.Name == "Twin Kegs");
            Assert.That(twin, Is.True, "a rare's step excludes nothing: its skill's upgrades are still offered");
        }

        [Test]
        public void Clear_ClearsTheTiers()
        {
            var run = new RunSkillUpgrades();
            run.TakeTier(Epic("Powder Keg", "Twin Kegs"));
            int version = run.Version;
            run.Clear();
            Assert.That(run.Count, Is.Zero);
            Assert.That(run.TierOf("Powder Keg"), Is.EqualTo(UpgradeTier.Common));
            Assert.That(run.CountFor("Powder Keg"), Is.Zero);
            Assert.That(run.Version, Is.GreaterThan(version));
            Assert.That(run.View(s_Skills), Is.SameAs(s_Skills));
        }

        // ---------------- The words ----------------

        [Test]
        public void TheCardsLines_AndTheListsWords()
        {
            SkillTierOffer epic = Epic("Long Shot", "Overcharge");
            Assert.That(SkillTiers.CardTierLine(epic), Is.EqualTo("EPIC   +30% damage and stagger"));
            Assert.That(SkillTiers.CardChangeLine(epic), Is.EqualTo("OVERCHARGE  The perfect band is half as wide; a perfect release deals three times the damage."));
            Assert.That(SkillTiers.CardTierLine(Rare("Fan Fire")), Is.EqualTo("RARE   +30% damage and stagger"));
            Assert.That(SkillTiers.CardTierLine(Rare("Slide")), Is.EqualTo("RARE   -20% cooldown"));
            Assert.That(SkillTiers.CardChangeLine(Rare("Fan Fire")), Is.EqualTo(""));
            Assert.That(SkillTiers.CardTierLine(new SkillTierOffer()), Is.EqualTo(""), "a common: nothing");
            Assert.That(SkillTiers.CardTierLine(null), Is.EqualTo(""));
        }

        [Test]
        public void RunRecord_SkillsText_WithTiers()
        {
            var r = new RunRecord("AAAAAAAA", 5);
            r.TakeSkill("Long Shot", UpgradeTier.Epic, "Overcharge");
            r.TakeSkill("Fan Fire", UpgradeTier.Rare);
            r.TakeSkill("Slide");
            Assert.That(r.SkillsText, Is.EqualTo("Long Shot (epic, Overcharge), Fan Fire (rare), Slide"));
            Assert.That(r.Skills, Is.EqualTo(new[] { "Long Shot", "Fan Fire", "Slide" }), "the names stay names");
            Assert.That(r.Picks, Is.EqualTo(3), "the change that came with a skill is not an elite pick");
        }

        static SessionEvent Ev(double t, string type, string name = null, params string[] extras)
        {
            var e = new SessionEvent(t, type, name);
            for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
            return e;
        }

        [Test]
        public void SessionSummary_ReadsTheNewExtras_AndAnOldLogAsBefore()
        {
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.RunStart, "ABCDEFGH"),
                Ev(1, SessionEventTypes.FloorEnter, "22222222", "floor", "1"),
                Ev(61, SessionEventTypes.RewardOffer, null, "floor", "1", "offered", "Long Shot | Fan Fire | Powder Keg", "tiers", "epic | rare | common"),
                Ev(61, SessionEventTypes.RewardTake, "Long Shot", "floor", "1", "tier", "epic", "with", "Overcharge"),
                Ev(90, SessionEventTypes.RewardOffer, null, "floor", "2", "offered", "Fan Fire | Slide | Tag Round", "tiers", "rare | common | common"),
                Ev(90, SessionEventTypes.RewardTake, "Fan Fire", "floor", "2", "tier", "rare", "with", ""),
                Ev(120, SessionEventTypes.RewardTake, "Slide", "floor", "3"),
                Ev(130, SessionEventTypes.RunStart, "JKLMNPQR"),
                Ev(131, SessionEventTypes.RewardOffer, null, "floor", "1", "offered", "Long Shot | Fan Fire | Powder Keg"),
                Ev(132, SessionEventTypes.RewardTake, "Long Shot", "floor", "1"),
                Ev(140, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            RunRow first = stats.Runs[0], old = stats.Runs[1];
            Assert.That(first.Skills, Is.EqualTo(new[] { "Long Shot", "Fan Fire", "Slide" }), "names, as before");
            Assert.That(first.SkillsWith, Is.EqualTo(new[] { "epic, Overcharge", "rare", "" }));
            Assert.That(old.Skills, Is.EqualTo(new[] { "Long Shot" }));
            Assert.That(old.SkillsWith, Is.EqualTo(new[] { "" }), "a log without the new fields");
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("  skills taken     Long Shot (epic, Overcharge), Fan Fire (rare), Slide" + Environment.NewLine, text);
            StringAssert.Contains("  skills taken     Long Shot" + Environment.NewLine, text, "an old log reads as before");
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithTheOrdersNumbers()
        {
            var values = new RunValues();
            Assert.That(values.SkillTiers, Is.True);
            Assert.That(values.StatsAndTiers, Is.True, "beside the stats' own, independent of it");
            Assert.That(values.SkillTierNumbers.Odds, Is.EqualTo(new[] { 60f, 30f, 10f }));
            Assert.That(values.SkillTierNumbers.FirstRewardAtLeastRare, Is.True);
            Assert.That(values.SkillTierNumbers.RareDamage, Is.EqualTo(1.3f));
            Assert.That(values.SkillTierNumbers.RareCooldown, Is.EqualTo(0.8f));
        }

        [Test]
        public void SwitchedOff_AThousandSeededFloorRewards_GiveNoTiers_AndTheSkillsAreTheSettingsNumbers()
        {
            var values = new RunValues { SkillTiers = false };
            for (int i = 0; i < 1000; i++)
            {
                ulong seed = Seed(i);
                var run = new RunSkillUpgrades();
                List<bool> has = StartHas();
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<int> slots = FloorRewards.Offer(seed, floor, has, values.SkillsOffered);
                    List<SkillTierOffer> tiers = SkillTiers.ForReward(values, seed, floor, 0, slots, s_Skills, s_Table, run.Taken, 0f);
                    Assert.That(tiers, Is.Empty);
                    run.TakeTier(tiers.Count > 0 ? tiers[0] : null); // as the pick does: no tier, nothing taken
                    has[slots[0]] = true;
                }
                Assert.That(run.Count, Is.Zero);
                Assert.That(run.View(s_Skills), Is.SameAs(s_Skills), "the settings' numbers");
            }
            Assert.That(SkillTiers.ForReward(new RunValues(), 7, 1, 0, new[] { 1, 2, 3 }, s_Skills, s_Table, null, 0f).Count, Is.EqualTo(3), "on: one per card");
        }

        [Test]
        public void WithTheStatsOff_LuckIsZero_AndTheTiersStillDraw()
        {
            var m = new RunModifiers { StatsAndTiers = false };
            m.Take(new Upgrade { Kind = UpgradeKind.Luck, Amount = 5f, Tiered = true });
            Assert.That(m.Stats.Luck, Is.Zero);
            List<SkillTierOffer> tiers = SkillTiers.ForReward(new RunValues { StatsAndTiers = false }, 99, 1, 0, new[] { 1, 2, 3 }, s_Skills, s_Table, null, m.Stats.Luck);
            Assert.That(tiers.Any(t => t.Tiered), Is.True, "floor 1 still shows a rare or better");
        }

        // ---------------- Keeping the game where it is (the order's measurements) ----------------

        sealed class Measured
        {
            public double RareOrBetter, Epic, RareOrBetterLater, EpicLater;
            public double RunsByFloor1, RunsByFloor2;
            public double RareTaken, EpicTaken;
            public readonly Dictionary<string, Dictionary<string, int>> Changes = new Dictionary<string, Dictionary<string, int>>();
        }

        /// <summary>
        /// 10 000 seeded runs through the rules alone: the four floor rewards of an act (slots by FloorRewards.Offer), the pick
        /// always the first card, as the test pilot picks. Elite upgrades are not taken: they are of skills she has, and an
        /// epic's change is of a skill she lacks, so they never meet.
        /// </summary>
        static Measured Measure(float luck, int runs = 10000)
        {
            var m = new Measured();
            var v = new SkillTierValues();
            int rewards = 0, rareOrBetter = 0, epic = 0, later = 0, rareLater = 0, epicLater = 0, by1 = 0, by2 = 0, rareTaken = 0, epicTaken = 0;
            for (int i = 0; i < runs; i++)
            {
                ulong seed = Seed(i);
                var run = new RunSkillUpgrades();
                List<bool> has = StartHas();
                bool seen = false;
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<int> slots = FloorRewards.Offer(seed, floor, has, 3);
                    List<SkillTierOffer> offer = SkillTiers.Draw(seed, floor, 0, slots, s_Skills, s_Table, run.Taken, luck, v, s_Stats);
                    rewards++;
                    bool rb = offer.Any(o => o.Tiered), ep = offer.Any(o => o.Tier == UpgradeTier.Epic);
                    if (rb) rareOrBetter++;
                    if (ep) epic++;
                    if (floor > 1)
                    {
                        later++;
                        if (rb) rareLater++;
                        if (ep) epicLater++;
                    }
                    seen |= rb;
                    if (floor == 1 && seen) by1++;
                    if (floor == 2 && seen) by2++;
                    foreach (SkillTierOffer o in offer.Where(o => o.Change != null))
                    {
                        if (!m.Changes.TryGetValue(o.Skill, out Dictionary<string, int> c)) m.Changes[o.Skill] = c = new Dictionary<string, int>();
                        c[o.Change.Name] = c.TryGetValue(o.Change.Name, out int n) ? n + 1 : 1;
                    }
                    SkillTierOffer pick = offer[0];
                    if (pick.Tier == UpgradeTier.Rare) rareTaken++;
                    if (pick.Tier == UpgradeTier.Epic) epicTaken++;
                    run.TakeTier(pick);
                    has[pick.Slot] = true;
                }
            }
            m.RareOrBetter = rareOrBetter / (double)rewards;
            m.Epic = epic / (double)rewards;
            m.RareOrBetterLater = rareLater / (double)later;
            m.EpicLater = epicLater / (double)later;
            m.RunsByFloor1 = by1 / (double)runs;
            m.RunsByFloor2 = by2 / (double)runs;
            m.RareTaken = rareTaken / (double)runs;
            m.EpicTaken = epicTaken / (double)runs;
            return m;
        }

        static string P(double share) => (share * 100.0).ToString("0.0", CultureInfo.InvariantCulture) + " %";

        [Test]
        public void Measurements_OverTenThousandRuns_AreWhereTheOrderPutsThem()
        {
            var report = new StringBuilder();
            foreach (float luck in new[] { 0f, 3f, 8f })
            {
                Measured m = Measure(luck);
                double[] s = UpgradeTiers.Shares(new SkillTierValues().Odds, luck, s_Stats);
                double rb = 1.0 - Math.Pow(s[0], 3), ep = 1.0 - Math.Pow(1.0 - s[2], 3);
                report.AppendLine(string.Format(CultureInfo.InvariantCulture,
                    "luck {0}: rewards with a rare or better {1} (floors 2-4 {2}, odds alone {3}), with an epic {4} (floors 2-4 {5}, odds alone {6}); runs with a rare or better by floor 1 {7}, by floor 2 {8}; of the four taken, rare {9:0.00}, epic {10:0.00}",
                    luck, P(m.RareOrBetter), P(m.RareOrBetterLater), P(rb), P(m.Epic), P(m.EpicLater), P(ep), P(m.RunsByFloor1), P(m.RunsByFloor2), m.RareTaken, m.EpicTaken));
                Assert.That(m.RunsByFloor1, Is.EqualTo(1.0), "floor 1 always");
                Assert.That(m.RareOrBetterLater, Is.EqualTo(rb).Within(0.02), "floors 2 to 4 are the odds' own");
                Assert.That(m.EpicLater, Is.EqualTo(ep).Within(0.02));
                if (luck == 0f)
                {
                    // The table's comment: about 78 % show a rare or better and 27 % an epic (the odds alone).
                    Assert.That(m.RareOrBetterLater, Is.InRange(0.76, 0.80));
                    Assert.That(m.EpicLater, Is.InRange(0.25, 0.29));
                    Assert.That(m.RareOrBetter, Is.InRange(0.81, 0.86), "with floor 1's rule");
                    Assert.That(m.RunsByFloor2, Is.EqualTo(1.0));
                    Assert.That(m.RareTaken, Is.InRange(1.15, 1.45), "of four skills taken (first card), about 1.3 rare");
                    Assert.That(m.EpicTaken, Is.InRange(0.30, 0.50), "and about 0.4 epic");
                }
                if (luck == 8f) Assert.That(m.RareOrBetterLater, Is.GreaterThan(0.95));
                if (luck == 0f)
                    foreach (KeyValuePair<string, Dictionary<string, int>> kv in m.Changes.OrderBy(k => k.Key))
                    {
                        int total = kv.Value.Values.Sum();
                        report.AppendLine("  " + kv.Key + " epic changes: " + string.Join(", ", kv.Value.OrderBy(c => c.Key).Select(c => c.Key + " " + c.Value + " (" + P(c.Value / (double)total) + ")")));
                    }
            }
            // Each pool skill's numbers at common and rare.
            var v = new SkillTierValues();
            foreach (SkillDefinition s in s_Skills.Where(x => !x.StartEquipped))
            {
                SkillDefinition r = SkillTiers.Preview(s, new SkillTierOffer { Skill = s.Name, Tier = UpgradeTier.Rare, Step = SkillTiers.Step(s, v) });
                report.AppendLine(string.Format(CultureInfo.InvariantCulture, "  {0}: damage {1} / {2}, stagger {3:0.##} / {4:0.##}, cooldown {5:0.##} / {6:0.##} s; its epic's changes: {7}",
                    s.Name, s.Damage, r.Damage, s.Stagger, r.Stagger, s.Cooldown, r.Cooldown,
                    string.Join(", ", SkillTiers.OpenChanges(s_Table, s.Name, null).Select(u => u.Name))));
                Assert.That(SkillTiers.OpenChanges(s_Table, s.Name, null), Is.Not.Empty, s.Name + " has a change for its epic");
            }
            TestContext.WriteLine(report.ToString());
        }

        /// <summary>
        /// Run seeds for Kristian (the report): a run whose floor-1 reward has an epic as its first card, and one with a
        /// rare, at luck 0 (no chest opened on floor 1). Found from the rules: the first such seeds after the one counted.
        /// </summary>
        [Test]
        public void SeedsForKristian_FloorOnesFirstCard_EpicAndRare()
        {
            ulong epic = 0, rare = 0;
            string epicText = "", rareText = "";
            for (ulong seed = 1; seed < 5000 && (epic == 0 || rare == 0); seed++)
            {
                List<int> slots = FloorRewards.Offer(seed, 1, StartHas(), 3);
                List<SkillTierOffer> offer = DrawFor(seed, 1, slots);
                string all = string.Join(", ", offer.Select(o => o.ToString()));
                if (epic == 0 && offer[0].Tier == UpgradeTier.Epic) { epic = seed; epicText = all; }
                if (rare == 0 && offer[0].Tier == UpgradeTier.Rare) { rare = seed; rareText = all; }
            }
            Assert.That(epic, Is.Not.Zero);
            Assert.That(rare, Is.Not.Zero);
            TestContext.WriteLine("epic first: seed " + epic + " (" + LevelSeed.ToText(epic) + "): " + epicText);
            TestContext.WriteLine("rare first: seed " + rare + " (" + LevelSeed.ToText(rare) + "): " + rareText);
        }
    }
}
