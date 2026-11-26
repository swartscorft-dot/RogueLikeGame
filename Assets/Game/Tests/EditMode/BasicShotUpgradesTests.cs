using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 138, part 3 (Kristian, 2026-11-25: "muligheter for å oppgradere autoattacks, med pierce og andre ting som
    /// feks on-hit, build up, oftere stacks osv"; his ticks "Fem, én fra hver retning" and "Nei, jevnt trukket"): the five
    /// basic-shot upgrades' rules at each tier, the even draw, the run's list of them, and their words. Each switch off: never
    /// offered and, held, nothing.
    /// </summary>
    public class BasicShotUpgradesTests
    {
        static readonly BasicShotUpgradeValues V = new BasicShotUpgradeValues();
        /// <summary>
        /// Work order 141: the five's draw pinned with work order 141's four switched off for the test (each card a fifth of the
        /// draws, as before they existed); with them on, each card is a ninth (BasicShotUpgradesMoreTests).
        /// </summary>
        static BasicShotUpgradeValues FiveOnly(BasicShotUpgradeValues v)
        {
            v.ChainKill = v.SixthChamber = v.HuntersCount = v.PassItOn = false;
            return v;
        }
        static readonly BasicShotUpgradeValues V5 = FiveOnly(new BasicShotUpgradeValues());
        const BasicShotUpgradeKind Pierce = BasicShotUpgradeKind.PiercingRound, Quick = BasicShotUpgradeKind.Quickening,
                                   Hot = BasicShotUpgradeKind.HotBarrel, Marking = BasicShotUpgradeKind.MarkingRounds, Tap = BasicShotUpgradeKind.TapOut;

        // ---------------- The switches, the run's list ----------------

        [Test]
        public void EverySwitch_IsOnByDefault_AndTheNumbersAreTheFirstGuesses()
        {
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All) Assert.That(V.On(k), Is.True, k.ToString());
            Assert.That(new RunValues().BasicShot, Is.Not.Null);
            Assert.That(V.PierceShare, Is.EqualTo(new[] { 0.5f, 0.8f, 0.8f }));
            Assert.That(V.PierceTargets, Is.EqualTo(new[] { 1, 1, 2 }));
            Assert.That(V.QuickeningSeconds, Is.EqualTo(new[] { 0.1f, 0.15f, 0.2f }));
            Assert.That(V.HeatPerHit, Is.EqualTo(new[] { 0.03f, 0.04f, 0.04f }));
            Assert.That(V.HeatMostHits, Is.EqualTo(new[] { 10, 10, 15 }));
            Assert.That(V.HeatHoldSeconds, Is.EqualTo(1.5f));
            Assert.That(V.HitsPerMark, Is.EqualTo(new[] { 4, 3, 2 }));
            Assert.That(V.TapOutBelowCap, Is.EqualTo(new[] { 0, 1, 1 }));
            Assert.That(V.TapOutBonus, Is.EqualTo(new[] { 0.2f, 0.2f, 0.3f }));
        }

        [Test]
        public void ARun_TakesTierAfterTier_NeverPastTheThird_AndClearsWithTheModifiers()
        {
            var m = new RunModifiers();
            RunBasicShotUpgrades held = m.BasicShot;
            Assert.That(held.Any, Is.False);
            Assert.That(held.Take(Hot), Is.EqualTo(1));
            Assert.That(held.Take(Pierce), Is.EqualTo(1));
            Assert.That(held.Take(Hot), Is.EqualTo(2));
            Assert.That(held.Take(Hot), Is.EqualTo(3));
            Assert.That(held.Take(Hot), Is.EqualTo(3), "never past the third");
            Assert.That(held.Held, Is.EqualTo(new[] { Hot, Pierce }), "in the order first taken");
            Assert.That(held.TierNow(Hot), Is.EqualTo(3));
            held.Values = new BasicShotUpgradeValues { HotBarrel = false };
            Assert.That(held.TierNow(Hot), Is.Zero, "its switch off: held, it does nothing");
            Assert.That(held.TierOf(Hot), Is.EqualTo(3), "still held");
            m.Clear();
            Assert.That(held.Any, Is.False, "cleared with the run's modifiers");
            Assert.That(held.TierOf(Hot), Is.Zero);
        }

        // ---------------- The even draw ----------------

        /// <summary>Work order 138's five, the kinds its draw tests count.</summary>
        static readonly BasicShotUpgradeKind[] Five = { Pierce, Quick, Hot, Marking, Tap };

        static Dictionary<BasicShotUpgradeKind, int> Count(RunBasicShotUpgrades held, BasicShotUpgradeValues v, int draws)
        {
            var count = RunBasicShotUpgrades.All.ToDictionary(k => k, k => 0);
            for (int i = 0; i < draws; i++)
            {
                BasicShotOffer o = BasicShotRules.Draw(LevelRandom.Stream(0xB45EUL + (ulong)i, BasicShotRules.Stage, i), held, v);
                if (o != null) count[o.Kind]++;
            }
            return count;
        }

        [Test]
        public void TheDraw_IsEven_AndStaysEvenOnceSheHasOne_NoLeaning()
        {
            const int draws = 10000;
            Dictionary<BasicShotUpgradeKind, int> none = Count(new RunBasicShotUpgrades(), V5, draws);
            foreach (BasicShotUpgradeKind k in Five)
                Assert.That(none[k], Is.InRange(draws / 5 - 300, draws / 5 + 300), k + " a fifth of the draws");
            // With one held (his tick "Nei, jevnt trukket"): it comes no more often than the others, only as its next tier.
            var held = new RunBasicShotUpgrades();
            held.Take(Pierce);
            Dictionary<BasicShotUpgradeKind, int> one = Count(held, V5, draws);
            foreach (BasicShotUpgradeKind k in Five)
                Assert.That(one[k], Is.InRange(draws / 5 - 300, draws / 5 + 300), k + " still a fifth with Piercing Round held");
            BasicShotOffer again = Enumerable.Range(0, 200).Select(i => BasicShotRules.Draw(LevelRandom.Stream((ulong)i, BasicShotRules.Stage, 0), held, V))
                                             .First(o => o.Kind == Pierce);
            Assert.That(again.Tier, Is.EqualTo(2), "one she has comes back as its next tier");
        }

        [Test]
        public void TheDraw_LeavesOutOneAtItsLastTier_AndOneSwitchedOff_AndGivesNullWithNoneOpen()
        {
            var held = new RunBasicShotUpgrades();
            for (int i = 0; i < 3; i++) held.Take(Tap);
            var v = FiveOnly(new BasicShotUpgradeValues { Quickening = false });
            Dictionary<BasicShotUpgradeKind, int> c = Count(held, v, 4000);
            Assert.That(c[Tap], Is.Zero, "Tap Out III held: never offered again");
            Assert.That(c[Quick], Is.Zero, "switched off: never offered");
            foreach (BasicShotUpgradeKind k in new[] { Pierce, Hot, Marking }) Assert.That(c[k], Is.InRange(1333 - 150, 1333 + 150), k + " a third");
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All) for (int i = 0; i < 3; i++) held.Take(k);
            Assert.That(BasicShotRules.Draw(LevelRandom.Stream(1UL, BasicShotRules.Stage, 0), held, V), Is.Null, "all five at III: none open");
            Assert.That(BasicShotRules.Open(new RunBasicShotUpgrades(), FiveOnly(new BasicShotUpgradeValues
            {
                PiercingRound = false, Quickening = false, HotBarrel = false, MarkingRounds = false, TapOut = false,
            })), Is.Empty, "every switch off: none");
        }

        // ---------------- Piercing Round ----------------

        static TargetInfo At(float x, float z = 0f, bool alive = true) => new TargetInfo(new GroundPoint(x, z), 0.5f, alive);

        [Test]
        public void PiercingRound_EachTiersShareAndCount()
        {
            Assert.That(BasicShotRules.PierceShare(0, V), Is.Zero);
            Assert.That(BasicShotRules.PierceTargets(0, V), Is.Zero);
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotRules.PierceShare(t, V)), Is.EqualTo(new[] { 0.5f, 0.8f, 0.8f }));
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotRules.PierceTargets(t, V)), Is.EqualTo(new[] { 1, 1, 2 }));
            Assert.That(BasicShotRules.PierceDamage(54, 0.5f), Is.EqualTo(27));
            Assert.That(BasicShotRules.PierceDamage(41, 0.5f), Is.EqualTo(21), "rounded half away");
            Assert.That(BasicShotRules.PierceDamage(1, 0.1f), Is.EqualTo(1), "at least 1 for a shot that dealt any");
            Assert.That(BasicShotRules.PierceDamage(0, 0.5f), Is.Zero);
        }

        [Test]
        public void PiercingRound_GoesOnAlongHerLineOnly_NearestFirst_StoppedByCover_NeverBehindHer()
        {
            var origin = new GroundPoint(0f, 0f);
            var aim = new GroundPoint(10f, 0f);
            // In the list out of order: the first hit at 3 m; on the line at 6, 4.5 and 8; one off the line at (5, 3); one dead
            // on the line at 5; one behind her at -2.
            var targets = new List<TargetInfo> { At(6f), At(3f), At(5f, 3f), At(4.5f), At(5f, 0f, false), At(-2f), At(8f) };
            var into = new List<int>();
            BasicShotRules.PiercedTargets(origin, aim, 10f, 0.5f, targets, float.PositiveInfinity, 1, 1, into);
            Assert.That(into, Is.EqualTo(new[] { 3 }), "tier I: the next on the line, 4.5 m");
            BasicShotRules.PiercedTargets(origin, aim, 10f, 0.5f, targets, float.PositiveInfinity, 1, 2, into);
            Assert.That(into, Is.EqualTo(new[] { 3, 0 }), "tier III: two more, 4.5 then 6 m; never the one off the line, the dead one or one behind her");
            BasicShotRules.PiercedTargets(origin, aim, 10f, 0.5f, targets, 5f, 1, 2, into);
            Assert.That(into, Is.EqualTo(new[] { 3 }), "cover at 5 m stops it: the one at 6 m is not reached");
            BasicShotRules.PiercedTargets(origin, aim, 6.5f, 0.5f, targets, float.PositiveInfinity, 1, 5, into);
            Assert.That(into, Is.EqualTo(new[] { 3, 0 }), "the one at 8 m is past the reach");
            BasicShotRules.PiercedTargets(origin, aim, 10f, 0.5f, targets, float.PositiveInfinity, 1, 0, into);
            Assert.That(into, Is.Empty, "none held: none");
        }

        // ---------------- Quickening ----------------

        [Test]
        public void Quickening_EachTiersSeconds_AndNeverUnderTheRunsCooldownFloor()
        {
            Assert.That(BasicShotRules.QuickeningSeconds(0, V), Is.Zero);
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotRules.QuickeningSeconds(t, V)), Is.EqualTo(new[] { 0.1f, 0.15f, 0.2f }));
            // A 10 s cooldown at the floor of 0.5: at most 5 s off one recharge, whatever came off it already.
            Assert.That(BasicShotRules.QuickeningRoom(10f, 0.5f, 0f), Is.EqualTo(5f).Within(1e-5f));
            Assert.That(BasicShotRules.QuickeningRoom(10f, 0.5f, 4.95f), Is.EqualTo(0.05f).Within(1e-4f));
            Assert.That(BasicShotRules.QuickeningRoom(10f, 0.5f, 6f), Is.Zero, "past it: no more");
        }

        // ---------------- Hot Barrel ----------------

        [Test]
        public void HotBarrel_BuildsAHitAtATime_ToItsMost_AndAMissOrAPauseDropsIt()
        {
            var heat = new HotBarrelHeat();
            float t = 0f;
            Assert.That(heat.Share(t, 1, V), Is.Zero, "the first hit in a row deals +0 %");
            for (int hit = 1; hit <= 12; hit++)
            {
                heat.Hit(t, V);
                t += 0.3f;
                Assert.That(heat.Share(t, 1, V), Is.EqualTo(0.03f * System.Math.Min(hit, 10)).Within(1e-5f), "after " + hit + " hits");
            }
            Assert.That(heat.Warmth(t, 1, V), Is.EqualTo(1f).Within(1e-5f), "full heat: the flash at its warmest");
            Assert.That(heat.Share(t, 2, V), Is.EqualTo(0.4f).Within(1e-5f), "tier II: +4 % a hit to +40 %");
            Assert.That(heat.Share(t, 3, V), Is.EqualTo(0.48f).Within(1e-5f), "tier III: 12 of its 15 hits so far");
            Assert.That(HotBarrelHeat.Most(3, V), Is.EqualTo(0.6f).Within(1e-5f));
            Assert.That(heat.Miss(t, V), Is.EqualTo(12));
            Assert.That(heat.Share(t, 1, V), Is.Zero, "a miss drops it all");
            heat.Hit(t, V);
            heat.Hit(t + 0.1f, V);
            Assert.That(heat.Share(t + 0.1f + V.HeatHoldSeconds - 0.01f, 1, V), Is.EqualTo(0.06f).Within(1e-5f), "held just under 1.5 s");
            Assert.That(heat.Share(t + 0.1f + V.HeatHoldSeconds + 0.01f, 1, V), Is.Zero, "1.5 s with no basic hit: dropped");
            Assert.That(heat.Share(t, 0, V), Is.Zero, "not held: nothing");
        }

        // ---------------- Marking Rounds ----------------

        [Test]
        public void MarkingRounds_EveryFourthThenThirdThenSecondHit_InPlaceOfTheKitsFifth()
        {
            Assert.That(BasicShotRules.HitsPerMark(0, 5, V), Is.EqualTo(5), "none: the kit's own");
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotRules.HitsPerMark(t, 5, V)), Is.EqualTo(new[] { 4, 3, 2 }));
            // The kit's count (BasicShotMarks) with it: marks on hits 4, 8, 12 at tier I.
            var count = new BasicShotMarks();
            var marked = Enumerable.Range(1, 12).Where(_ => count.Hit(BasicShotRules.HitsPerMark(1, 5, V), false)).ToList();
            Assert.That(marked.Count, Is.EqualTo(3));
        }

        // ---------------- Tap Out ----------------

        [Test]
        public void TapOut_AtTheCap_ThenFromOneUnder_ForTwentyThenThirtyPercentAMark()
        {
            Assert.That(BasicShotRules.TapsOut(5, 0, 5, V), Is.False, "none held");
            Assert.That(BasicShotRules.TapsOut(5, 1, 5, V), Is.True, "tier I at the cap");
            Assert.That(BasicShotRules.TapsOut(4, 1, 5, V), Is.False);
            Assert.That(BasicShotRules.TapsOut(4, 2, 5, V), Is.True, "tier II from 4");
            Assert.That(BasicShotRules.TapsOut(3, 3, 5, V), Is.False);
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotRules.TapOutBonus(t, V)), Is.EqualTo(new[] { 0.2f, 0.2f, 0.3f }));
            Assert.That(MarkRules.SpenderDamage(40, 5, BasicShotRules.TapOutBonus(1, V)), Is.EqualTo(80), "double damage at 5, as the draft says");
            Assert.That(MarkRules.SpenderDamage(40, 5, BasicShotRules.TapOutBonus(3, V)), Is.EqualTo(100));
        }

        // ---------------- The words ----------------

        [Test]
        public void TheCard_SaysItsKindAtAGlance_ItsNameAndTier_AndItsNumbersBeforeAndAfter()
        {
            OfferCard card = BasicShotTexts.Card(new BasicShotOffer { Kind = Pierce, Tier = 2 }, V);
            Assert.That(card.KindLine, Is.EqualTo("BASIC SHOT UPGRADE"));
            Assert.That(card.Name, Is.EqualTo("Piercing Round II"));
            Assert.That(card.TierLine, Is.EqualTo("TIER II OF III"));
            Assert.That(card.Parts().First().Kind, Is.EqualTo(OfferPartKind.Kind), "the kind first under the name");
            Assert.That(card.Numbers.Single(), Does.Contain("50 % to 1 more enemy").And.Contain("80 % to 1 more enemy"));
            Assert.That(BasicShotTexts.Card(new BasicShotOffer { Kind = Marking, Tier = 1 }, V).Numbers.Single(),
                        Does.Contain("a mark every 5th hit").And.Contain("a mark every 4th hit"));
            Assert.That(BasicShotTexts.StatLine(Hot, 1, V), Is.EqualTo("Hot Barrel I  +3 % a hit, to +30 %"));
            Assert.That(BasicShotTexts.StatLine(Tap, 2, V), Is.EqualTo("Tap Out II  from 4 marks, +20 % a mark"));
            Assert.That(BasicShotTexts.StatLine(Quick, 3, V), Is.EqualTo("Quickening III  -0.2 s every cooldown a hit"));
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All) Assert.That(BasicShotTexts.Sentence(k), Is.Not.Empty);
            Assert.That(new OfferCard { Name = "x" }.Parts().Any(p => p.Kind == OfferPartKind.Kind), Is.False, "every other card as before");
        }

        [Test]
        public void HerSheet_AndThePauseMenusPanel_ListTheUpgradesSheHolds_UnderBasicShot_AndNoneBefore()
        {
            var c = new OfferContext { Modifiers = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() } };
            List<SheetStat> before = HerSheet.Stats(c);
            Assert.That(before.Any(s => s.Group == SheetGroup.BasicShot), Is.False, "none held: no group");
            c.Modifiers.BasicShot.Take(Pierce);
            c.Modifiers.BasicShot.Take(Pierce);
            c.Modifiers.BasicShot.Take(Hot);
            List<SheetStat> after = HerSheet.Stats(c);
            Assert.That(after.Take(before.Count).Select(s => s.Name + s.Value), Is.EqualTo(before.Select(s => s.Name + s.Value)), "the lines of before");
            Assert.That(after.Skip(before.Count).Select(s => (s.Group, s.Name, s.Value)),
                        Is.EqualTo(new[] { (SheetGroup.BasicShot, "Piercing Round", "II"), (SheetGroup.BasicShot, "Hot Barrel", "I") }));
            Assert.That(HerSheet.GroupWord(SheetGroup.BasicShot), Is.EqualTo("BASIC SHOT"));
            Assert.That(HerSheet.ColumnOf(SheetGroup.BasicShot), Is.EqualTo(1), "in the shorter column, under fortune");
        }

        [Test]
        public void HerStats_ListTheUpgradesSheHolds_AndNoneBefore()
        {
            var m = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() };
            var stats = new HerStats(m);
            List<string> before = stats.Lines(0.225f, 10f, 100);
            m.BasicShot.Take(Pierce);
            m.BasicShot.Take(Marking);
            List<string> after = stats.Lines(0.225f, 10f, 100);
            Assert.That(after.Take(before.Count), Is.EqualTo(before), "the lines of before, unchanged");
            Assert.That(after.Skip(before.Count), Is.EqualTo(new[] { "Piercing Round I  50 % to 1 more enemy", "Marking Rounds I  a mark every 4th hit" }));
        }
    }
}
