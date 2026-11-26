using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 138, part 2 (Kristian, 2026-11-25: "Jeg syntes også det er rart å alltid få en fokus upgrade hver gang etter
    /// første flor"; his ticks "Tre kort av tre slag" and "Kortet blir et annet slag"): the floor reward of three kinds' rules.
    /// A thousand runs of four rewards each, every card picked by a stream of the test's own: never two of a kind on a screen,
    /// no skill card with six slots full, a focus way never after floor 3 and never once one is taken, the skill card first and
    /// the basic shot's after it, and the large stat step at the reward odds (never a common).
    /// </summary>
    public class MixedFloorRewardsTests
    {
        static readonly SkillDefinition[] Kit = SkillDefaults.Create();
        static readonly SkillUpgradeDefinition[] Table = SkillUpgradeTable.Create();

        [Test]
        public void TheSwitch_IsOnByDefault_ThreeCards_FocusUntilFloor3()
        {
            var run = new RunValues();
            Assert.That(run.MixedFloorReward, Is.True, "his tick: three cards of three kinds");
            Assert.That(run.MixedReward.Cards, Is.EqualTo(3));
            Assert.That(run.MixedReward.FocusLastFloor, Is.EqualTo(3));
            Assert.That(MixedFloorRewards.FocusOpen(3, false, run.MixedReward), Is.True);
            Assert.That(MixedFloorRewards.FocusOpen(4, false, run.MixedReward), Is.False, "never after floor 3");
            Assert.That(MixedFloorRewards.FocusOpen(1, true, run.MixedReward), Is.False, "never once taken");
        }

        sealed class Run
        {
            public readonly SkillLoadout Bar = new SkillLoadout { SixSlots = true };
            public readonly RunBasicShotUpgrades Shot = new RunBasicShotUpgrades();
            public readonly RunModifiers Modifiers = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() };
            public readonly List<SkillUpgradeDefinition> Upgrades = new List<SkillUpgradeDefinition>();
            public bool FocusTaken;
            public int FocusTakes;
            public Run() => Bar.Start(Kit, all: false);
        }

        static List<FloorRewardCard> Offer(ulong seed, int floor, Run r, RunValues values)
        {
            var equipped = Enumerable.Range(0, Kit.Length).Select(r.Bar.Has).ToList();
            int skill = r.Bar.FirstFreeKey >= 0 ? FloorRewards.Offer(seed, floor, equipped, values.SkillsOffered).DefaultIfEmpty(-1).First() : -1;
            var had = new HashSet<string>(Enumerable.Range(0, Kit.Length).Where(r.Bar.Has).Select(i => Kit[i].Name));
            List<SkillUpgradeDefinition> open = SkillUpgradeOffer.Open(Table, had, r.Upgrades);
            Upgrade stat = MixedFloorRewards.StatStep(seed, floor, 0, had.ToList(), values, r.Modifiers, false);
            return MixedFloorRewards.Offer(seed, floor, 0, skill, r.Shot, values.BasicShot, r.FocusTaken, open, stat, values.MixedReward);
        }

        [Test]
        public void AThousandRuns_ThreeKindsAScreen_NeverTwoAlike_NoSkillWithSixFull_FocusOnlyUntilFloor3AndOnce()
        {
            var values = new RunValues();
            var seen = new Dictionary<FloorRewardKind, int>();
            int screens = 0, skillCardsFull = 0, threeCards = 0;
            for (int n = 0; n < 1000; n++)
            {
                ulong seed = LevelRandom.Mix(0x13802000UL + (ulong)n);
                var r = new Run();
                LevelRandom pick = LevelRandom.Stream(seed, "the test's picks", 0);
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<FloorRewardCard> cards = Offer(seed, floor, r, values);
                    screens++;
                    string at = "seed " + n + " floor " + floor + ": " + string.Join(", ", cards);
                    Assert.That(cards.Select(c => c.Kind).Distinct().Count(), Is.EqualTo(cards.Count), "never two of a kind: " + at);
                    Assert.That(cards.Count, Is.LessThanOrEqualTo(3));
                    if (cards.Count == 3) threeCards++;
                    bool full = r.Bar.FirstFreeKey < 0;
                    if (full) Assert.That(cards.Any(c => c.Kind == FloorRewardKind.Skill), Is.False, "no skill card with six full: " + at);
                    else Assert.That(cards[0].Kind, Is.EqualTo(FloorRewardKind.Skill), "the skill card first while a slot is free: " + at);
                    if (full) skillCardsFull++;
                    int shotAt = cards.FindIndex(c => c.Kind == FloorRewardKind.BasicShot);
                    Assert.That(shotAt, Is.EqualTo(full ? 0 : 1), "a basic-shot card after the skill card: " + at);
                    if (floor > 3 || r.FocusTaken) Assert.That(cards.Any(c => c.Kind == FloorRewardKind.Focus), Is.False, "focus: " + at);
                    foreach (FloorRewardCard c in cards)
                    {
                        seen[c.Kind] = seen.TryGetValue(c.Kind, out int k) ? k + 1 : 1;
                        if (c.Kind == FloorRewardKind.Skill) Assert.That(r.Bar.Has(c.Slot), Is.False, "a skill she lacks");
                        if (c.Kind == FloorRewardKind.Stat) Assert.That(c.Stat.Tier, Is.Not.EqualTo(UpgradeTier.Common), "a large step: the reward odds give no common");
                        if (c.Kind == FloorRewardKind.SkillUpgrade) Assert.That(r.Upgrades, Has.No.Member(c.Upgrade));
                    }
                    // Her pick, any card.
                    FloorRewardCard taken = cards[pick.Below(cards.Count)];
                    switch (taken.Kind)
                    {
                        case FloorRewardKind.Skill: r.Bar.Take(taken.Slot); break;
                        case FloorRewardKind.BasicShot: r.Shot.Take(taken.Shot.Kind); break;
                        case FloorRewardKind.Focus: r.FocusTaken = true; r.FocusTakes++; break;
                        case FloorRewardKind.SkillUpgrade: r.Upgrades.Add(taken.Upgrade); break;
                        default: r.Modifiers.Take(taken.Stat); break;
                    }
                }
                Assert.That(r.FocusTakes, Is.LessThanOrEqualTo(1), "a focus way taken at most once a run");
                Assert.That(r.Bar.FirstFreeKey >= 0 || Enumerable.Range(0, 8).Count(k => r.Bar.SkillOn(k) >= 0) == 6, Is.True);
            }
            TestContext.WriteLine("[138] " + screens + " screens, " + threeCards + " of three cards, " + skillCardsFull + " with six full; cards by kind: "
                                  + string.Join(", ", seen.OrderBy(k => k.Key).Select(k => k.Key + " " + k.Value)));
            foreach (FloorRewardKind k in new[] { FloorRewardKind.Skill, FloorRewardKind.BasicShot, FloorRewardKind.Focus, FloorRewardKind.SkillUpgrade, FloorRewardKind.Stat })
                Assert.That(seen.ContainsKey(k), Is.True, k + " is offered in some runs");
            Assert.That(threeCards, Is.EqualTo(screens), "every screen of a run's four shows three cards");
            Assert.That(skillCardsFull, Is.GreaterThan(0), "runs that took two skills met screens with six full");
        }

        [Test]
        public void TheSameRunAndPicks_OfferTheSame()
        {
            var values = new RunValues();
            string A() => string.Join(", ", Offer(77UL, 2, new Run(), values));
            Assert.That(A(), Is.EqualTo(A()));
        }

        [Test]
        public void WithHerStatsOff_NoStatCard_AndWithNothingOpen_FewerCards()
        {
            var values = new RunValues { StatsAndTiers = false };
            Assert.That(MixedFloorRewards.StatStep(5UL, 1, 0, new List<string>(), values, new RunModifiers(), false), Is.Null);
            var shotOff = new BasicShotUpgradeValues { PiercingRound = false, Quickening = false, HotBarrel = false, MarkingRounds = false, TapOut = false,
                                                       ChainKill = false, SixthChamber = false, HuntersCount = false, PassItOn = false }; // work order 141: its four too
            List<FloorRewardCard> cards = MixedFloorRewards.Offer(5UL, 4, 0, -1, new RunBasicShotUpgrades(), shotOff, true, new List<SkillUpgradeDefinition>(), null, null);
            Assert.That(cards, Is.Empty, "nothing to give: no card (the reward is skipped, as when no skill was left)");
            cards = MixedFloorRewards.Offer(5UL, 1, 0, 3, new RunBasicShotUpgrades(), shotOff, false, new List<SkillUpgradeDefinition>(), null, null);
            Assert.That(cards.Select(c => c.Kind), Is.EqualTo(new[] { FloorRewardKind.Skill, FloorRewardKind.Focus }), "a kind with nothing to give is not drawn");
        }

        [Test]
        public void EachKind_SaysWhatItIs()
        {
            Assert.That(MixedFloorRewards.KindWord(FloorRewardKind.Skill), Is.EqualTo("NEW SKILL"));
            Assert.That(MixedFloorRewards.KindWord(FloorRewardKind.BasicShot), Is.EqualTo("BASIC SHOT UPGRADE"));
            Assert.That(MixedFloorRewards.KindWord(FloorRewardKind.Focus), Is.EqualTo("FOCUS WAY"));
            Assert.That(MixedFloorRewards.KindWord(FloorRewardKind.SkillUpgrade), Is.EqualTo("SKILL UPGRADE"));
            Assert.That(MixedFloorRewards.KindWord(FloorRewardKind.Stat), Is.EqualTo("STAT STEP"));
            Assert.That(new[] { FloorRewardKind.Skill, FloorRewardKind.BasicShot, FloorRewardKind.Focus, FloorRewardKind.SkillUpgrade, FloorRewardKind.Stat }
                        .Select(MixedFloorRewards.LogWord), Is.EqualTo(new[] { "skill", "basic shot", "focus", "skill upgrade", "stat" }));
        }
    }
}
