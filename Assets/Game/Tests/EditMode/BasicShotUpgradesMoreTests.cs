using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 141 (Kristian, 2026-11-26, his tick of all four: Chain Kill, Pass It On, Sixth Chamber, Hunter's Count): the four
    /// basic-shot upgrades' rules at each tier, the even draw of nine with Hunter's Count and Marking Rounds excluding each other,
    /// the words, and each switch. Chain Kill's bounces and Pass It On's marks go only from an enemy that died, to the nearest
    /// living one near it past cover (Ricochet's rule); nothing else picks a target.
    /// </summary>
    public class BasicShotUpgradesMoreTests
    {
        static readonly BasicShotUpgradeValues V = new BasicShotUpgradeValues();
        const BasicShotUpgradeKind Chain = BasicShotUpgradeKind.ChainKill, Sixth = BasicShotUpgradeKind.SixthChamber,
                                   Hunter = BasicShotUpgradeKind.HuntersCount, Pass = BasicShotUpgradeKind.PassItOn,
                                   Marking = BasicShotUpgradeKind.MarkingRounds, Pierce = BasicShotUpgradeKind.PiercingRound;
        static readonly BasicShotUpgradeKind[] Four = { Chain, Sixth, Hunter, Pass };

        static TargetInfo At(float x, float z = 0f, bool alive = true) => new TargetInfo(new GroundPoint(x, z), 0.5f, alive);

        // ---------------- The switches, the numbers, the list ----------------

        [Test]
        public void TheFour_AreOnByDefault_AtTheEndOfTheList_WithTheirFirstGuesses()
        {
            Assert.That(RunBasicShotUpgrades.All.Length, Is.EqualTo(9));
            Assert.That(RunBasicShotUpgrades.All.Skip(5), Is.EqualTo(Four), "after the five, in the order of the order");
            foreach (BasicShotUpgradeKind k in Four) Assert.That(V.On(k), Is.True, k.ToString());
            Assert.That(V.BounceShare, Is.EqualTo(new[] { 1f, 1f, 1.3f }));
            Assert.That(V.BounceReach, Is.EqualTo(new[] { 4f, 5.5f, 5.5f }));
            Assert.That(V.BounceMost, Is.EqualTo(new[] { 3, 3, 3 }));
            Assert.That(V.ShotsPerHeavy, Is.EqualTo(new[] { 6, 5, 5 }));
            Assert.That(V.HeavyScale, Is.EqualTo(new[] { 2f, 2f, 3f }));
            Assert.That(V.HeavyPierce, Is.EqualTo(1));
            Assert.That(V.HeavyPierceShare, Is.EqualTo(0.5f));
            Assert.That(V.HeavyStagger, Is.EqualTo(10f));
            Assert.That(V.HuntHits, Is.EqualTo(new[] { 3, 3, 2 }));
            Assert.That(V.HuntMarks, Is.EqualTo(new[] { 1, 2, 2 }));
            Assert.That(V.PassReach, Is.EqualTo(new[] { 5f, 7f, 7f }));
            Assert.That(V.PassOnAnyKill, Is.EqualTo(new[] { false, false, true }));
        }

        [Test]
        public void ARun_HoldsTheFour_TierAfterTier_AndClearsThemAndTheChambersCount()
        {
            var m = new RunModifiers();
            foreach (BasicShotUpgradeKind k in Four) Assert.That(m.BasicShot.Take(k), Is.EqualTo(1));
            Assert.That(m.BasicShot.Take(Sixth), Is.EqualTo(2));
            m.BasicShot.ChamberFired = 4;
            Assert.That(m.BasicShot.Held, Is.EqualTo(Four));
            m.BasicShot.Values = new BasicShotUpgradeValues { ChainKill = false };
            Assert.That(m.BasicShot.TierNow(Chain), Is.Zero, "its switch off: held, it does nothing");
            m.Clear();
            Assert.That(m.BasicShot.Any, Is.False);
            Assert.That(m.BasicShot.ChamberFired, Is.Zero);
        }

        // ---------------- The draw ----------------

        static Dictionary<BasicShotUpgradeKind, int> Count(RunBasicShotUpgrades held, BasicShotUpgradeValues v, int draws)
        {
            var count = RunBasicShotUpgrades.All.ToDictionary(k => k, k => 0);
            for (int i = 0; i < draws; i++)
            {
                BasicShotOffer o = BasicShotRules.Draw(LevelRandom.Stream(0x141UL + (ulong)i, BasicShotRules.Stage, i), held, v);
                if (o != null) count[o.Kind]++;
            }
            return count;
        }

        [Test]
        public void TheDraw_IsEvenAmongTheNine_NoLeaning()
        {
            const int draws = 18000;
            Dictionary<BasicShotUpgradeKind, int> none = Count(new RunBasicShotUpgrades(), V, draws);
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All)
                Assert.That(none[k], Is.InRange(draws / 9 - 250, draws / 9 + 250), k + " a ninth of the draws");
        }

        [Test]
        public void HuntersCount_AndMarkingRounds_ExcludeEachOther_TheDrawEvenAmongTheRest()
        {
            const int draws = 16000;
            var mr = new RunBasicShotUpgrades();
            mr.Take(Marking);
            Dictionary<BasicShotUpgradeKind, int> c = Count(mr, V, draws);
            Assert.That(c[Hunter], Is.Zero, "Marking Rounds held: Hunter's Count never offered");
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All.Where(k => k != Hunter))
                Assert.That(c[k], Is.InRange(draws / 8 - 250, draws / 8 + 250), k + " an eighth (Marking Rounds as its next tier)");
            var hc = new RunBasicShotUpgrades();
            hc.Take(Hunter);
            c = Count(hc, V, draws);
            Assert.That(c[Marking], Is.Zero, "Hunter's Count held: Marking Rounds never offered");
            Assert.That(c[Hunter], Is.InRange(draws / 8 - 250, draws / 8 + 250), "Hunter's Count comes back as its next tier");
            Assert.That(BasicShotMoreRules.Excluded(Pierce, hc), Is.False);
            Assert.That(BasicShotMoreRules.Excluded(Hunter, null), Is.False);
        }

        [Test]
        public void EachOfTheFourSwitchedOff_IsNeverOffered_AndAllNineOff_NoneIs()
        {
            foreach (BasicShotUpgradeKind k in Four)
            {
                var v = new BasicShotUpgradeValues();
                if (k == Chain) v.ChainKill = false;
                if (k == Sixth) v.SixthChamber = false;
                if (k == Hunter) v.HuntersCount = false;
                if (k == Pass) v.PassItOn = false;
                Assert.That(BasicShotRules.Open(new RunBasicShotUpgrades(), v), Has.No.Member(k));
                Assert.That(BasicShotRules.Open(new RunBasicShotUpgrades(), v).Count, Is.EqualTo(8));
            }
            var off = new BasicShotUpgradeValues
            {
                PiercingRound = false, Quickening = false, HotBarrel = false, MarkingRounds = false, TapOut = false,
                ChainKill = false, SixthChamber = false, HuntersCount = false, PassItOn = false,
            };
            Assert.That(BasicShotRules.Open(new RunBasicShotUpgrades(), off), Is.Empty);
        }

        /// <summary>
        /// The order: "If any pinned draw ... would move with the switches ON, say which". The floor reward's basic-shot card draws on
        /// its own stream, so only which upgrade that card shows moves; the skill card and the third card, and every chest, are as
        /// with the four off. Counted on a thousand runs of four rewards (the same picks), for the report.
        /// </summary>
        [Test]
        public void WithTheFourOn_OnlyTheBasicShotCardMoves_TheOtherCardsAreAsWithThemOff()
        {
            var on = new RunValues();
            var off = new RunValues();
            off.BasicShot.ChainKill = off.BasicShot.SixthChamber = off.BasicShot.HuntersCount = off.BasicShot.PassItOn = false;
            int screens = 0, shotMoved = 0, otherMoved = 0;
            for (int n = 0; n < 1000; n++)
            {
                ulong seed = LevelRandom.Mix(0x14100000UL + (ulong)n);
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<FloorRewardCard> a = MixedFloorRewards.Offer(seed, floor, 0, 3, new RunBasicShotUpgrades(), on.BasicShot, false, new List<SkillUpgradeDefinition>(), null, on.MixedReward);
                    List<FloorRewardCard> b = MixedFloorRewards.Offer(seed, floor, 0, 3, new RunBasicShotUpgrades(), off.BasicShot, false, new List<SkillUpgradeDefinition>(), null, off.MixedReward);
                    screens++;
                    Assert.That(a.Count, Is.EqualTo(b.Count));
                    for (int i = 0; i < a.Count; i++)
                    {
                        Assert.That(a[i].Kind, Is.EqualTo(b[i].Kind));
                        if (a[i].Kind == FloorRewardKind.BasicShot) { if (a[i].Shot.Kind != b[i].Shot.Kind) shotMoved++; }
                        else if (a[i].ToString() != b[i].ToString()) otherMoved++;
                    }
                }
            }
            TestContext.WriteLine("[141] " + screens + " floor rewards: the basic-shot card shows another upgrade with the four on in " + shotMoved + "; any other card moved in " + otherMoved);
            Assert.That(otherMoved, Is.Zero);
            Assert.That(shotMoved, Is.GreaterThan(0));
        }

        // ---------------- Chain Kill ----------------

        [Test]
        public void ChainKill_BouncesFromTheDead_ToTheNearestWithinItsReach_OnlyOnAKill_AtMostThree()
        {
            Assert.That(BasicShotMoreRules.BounceShare(0, V), Is.Zero);
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotMoreRules.BounceReach(t, V)), Is.EqualTo(new[] { 4f, 5.5f, 5.5f }));
            Assert.That(BasicShotMoreRules.BounceDamage(40, 1f), Is.EqualTo(40), "100 % of the shot");
            Assert.That(BasicShotMoreRules.BounceDamage(40, 1.3f), Is.EqualTo(52));
            // The dead at 0; living at 3, 6, 9 and 12 m in a row (each 3 m from the next), and one 4.5 m off to the side.
            var targets = new List<TargetInfo> { At(0f, 0f, false), At(3f), At(6f), At(9f), At(12f), At(0f, 4.5f) };
            var into = new List<int>();
            BasicShotMoreRules.Bounces(targets, 0, null, 4f, 3, null, i => false, into);
            Assert.That(into, Is.EqualTo(new[] { 1 }), "a bounce that does not kill ends it");
            BasicShotMoreRules.Bounces(targets, 0, null, 4f, 3, null, i => true, into);
            Assert.That(into, Is.EqualTo(new[] { 1, 2, 3 }), "each that kills bounces again, at most three");
            BasicShotMoreRules.Bounces(targets, 0, new[] { 1 }, 4f, 3, null, i => true, into);
            Assert.That(into, Is.Empty, "never to one the shot hit; the next is past 4 m (the one at 4.5 m too)");
            BasicShotMoreRules.Bounces(targets, 0, new[] { 1 }, 5.5f, 3, null, i => true, into);
            Assert.That(into, Is.EqualTo(new[] { 5 }), "tier II's 5.5 m reaches the one aside, the only one within it");
            // A wall across the row at x = 1.5 m: the one at 3 m is behind it, so the bounce goes past cover to the one aside.
            CoverLine wall = (from, to) => (from.X - 1.5f) * (to.X - 1.5f) < 0f ? System.Math.Abs(1.5f - from.X) : from.DistanceTo(to);
            BasicShotMoreRules.Bounces(targets, 0, null, 5.5f, 3, wall, i => false, into);
            Assert.That(into, Is.EqualTo(new[] { 5 }), "never through cover: the one behind the wall is not hit");
        }

        // ---------------- Sixth Chamber ----------------

        [Test]
        public void SixthChamber_EverySixthShotFired_HitOrMiss_ThenEveryFifth_DoubleThenTriple()
        {
            int fired = 0;
            var heavy = Enumerable.Range(1, 18).Where(_ => BasicShotMoreRules.ChamberShot(ref fired, BasicShotMoreRules.ShotsPerHeavy(1, V))).ToList();
            Assert.That(heavy.Count, Is.EqualTo(3), "shots 6, 12, 18");
            fired = 0;
            var shots = new List<int>();
            for (int i = 1; i <= 12; i++) if (BasicShotMoreRules.ChamberShot(ref fired, 6)) shots.Add(i);
            Assert.That(shots, Is.EqualTo(new[] { 6, 12 }));
            fired = 4;
            Assert.That(BasicShotMoreRules.NextIsHeavy(fired, 6), Is.False);
            fired = 5;
            Assert.That(BasicShotMoreRules.NextIsHeavy(fired, 6), Is.True, "five fired: the next is the heavy round");
            Assert.That(BasicShotMoreRules.ShotsPerHeavy(2, V), Is.EqualTo(5));
            Assert.That(BasicShotMoreRules.ShotsPerHeavy(0, V), Is.Zero);
            Assert.That(new[] { 1, 2, 3 }.Select(t => BasicShotMoreRules.HeavyDamage(40, BasicShotMoreRules.HeavyScale(t, V))), Is.EqualTo(new[] { 80, 80, 120 }));
        }

        [Test]
        public void SixthChamber_SharesThePierce_WithPiercingRound_NeverAddsIt()
        {
            Assert.That(BasicShotMoreRules.PierceTargets(false, 0, V), Is.Zero, "no heavy round, no Piercing Round: none");
            Assert.That(BasicShotMoreRules.PierceTargets(true, 0, V), Is.EqualTo(1), "the heavy round alone: through one");
            Assert.That(BasicShotMoreRules.PierceShare(true, 0, V), Is.EqualTo(0.5f));
            Assert.That(BasicShotMoreRules.PierceTargets(true, 1, V), Is.EqualTo(1), "with Piercing Round I: one, not two");
            Assert.That(BasicShotMoreRules.PierceShare(true, 2, V), Is.EqualTo(0.8f), "the larger share of the two");
            Assert.That(BasicShotMoreRules.PierceTargets(true, 3, V), Is.EqualTo(2), "Piercing Round III's two");
            Assert.That(BasicShotMoreRules.PierceTargets(false, 3, V), Is.EqualTo(2));
            Assert.That(BasicShotMoreRules.PierceShare(false, 1, V), Is.EqualTo(0.5f));
        }

        // ---------------- Hunter's Count ----------------

        [Test]
        public void HuntersCount_TheThirdHitOnOneEnemyMarks_ThenTwoMarks_ThenEverySecond()
        {
            int Marks(int tier, int hits)
            {
                int count = 0, made = 0;
                for (int i = 0; i < hits; i++) made += BasicShotMoreRules.HunterHit(ref count, tier, false, V);
                return made;
            }
            Assert.That(Marks(0, 12), Is.Zero, "not held: nothing (the kit's count decides)");
            Assert.That(Marks(1, 12), Is.EqualTo(4), "tier I: hits 3, 6, 9, 12");
            Assert.That(Marks(2, 12), Is.EqualTo(8), "tier II: 2 marks on each third hit");
            Assert.That(Marks(3, 12), Is.EqualTo(12), "tier III: 2 marks every second hit");
            int c = 2;
            Assert.That(BasicShotMoreRules.HunterHit(ref c, 1, true, V), Is.EqualTo(1), "the focus window: every hit marks once");
            Assert.That(c, Is.EqualTo(2), "and the count stands");
        }

        // ---------------- Pass It On ----------------

        [Test]
        public void PassItOn_ToTheNearestLivingWithinItsReach_UpToTheCap_OnABasicKill_ThenAnyKill()
        {
            Assert.That(BasicShotMoreRules.PassesOn(0, true, V), Is.False);
            Assert.That(BasicShotMoreRules.PassesOn(1, true, V), Is.True);
            Assert.That(BasicShotMoreRules.PassesOn(2, false, V), Is.False, "a skill's kill passes nothing until the last tier");
            Assert.That(BasicShotMoreRules.PassesOn(3, false, V), Is.True, "at the last tier any kill of hers");
            var targets = new List<TargetInfo> { At(0f), At(4.8f), At(0f, 6f), At(2f, 0f, false) };
            Assert.That(BasicShotMoreRules.PassTo(targets, 0, BasicShotMoreRules.PassReach(1, V), null), Is.EqualTo(1), "the one at 4.8 m (not the dead at 2)");
            targets[1] = At(5.5f);
            Assert.That(BasicShotMoreRules.PassTo(targets, 0, 5f, null), Is.EqualTo(-1), "none within 5 m");
            Assert.That(BasicShotMoreRules.PassTo(targets, 0, 7f, null), Is.EqualTo(1), "tier II's 7 m: the nearer of the two");
            Assert.That(BasicShotMoreRules.Passed(4, 3, 5), Is.EqualTo(2), "up to the cap");
            Assert.That(BasicShotMoreRules.Passed(3, 0, 5), Is.EqualTo(3));
        }

        // ---------------- The words ----------------

        [Test]
        public void TheCards_TheStatLines_AndHerSheet_SayEachOfTheFour()
        {
            Assert.That(Four.Select(BasicShotTexts.Name), Is.EqualTo(new[] { "Chain Kill", "Sixth Chamber", "Hunter's Count", "Pass It On" }));
            foreach (BasicShotUpgradeKind k in Four) Assert.That(BasicShotTexts.Sentence(k), Is.Not.Empty, k.ToString());
            Assert.That(BasicShotTexts.AddedUnit(Chain), Is.EqualTo("damage"));
            Assert.That(BasicShotTexts.AddedUnit(Sixth), Is.EqualTo("damage"));
            Assert.That(BasicShotTexts.AddedUnit(Hunter), Is.EqualTo("marks"));
            Assert.That(BasicShotTexts.AddedUnit(Pass), Is.EqualTo("marks"));
            Assert.That(BasicShotTexts.StatLine(Chain, 1, V), Is.EqualTo("Chain Kill I  a kill bounces 100 % within 4 m, up to 3 times"));
            Assert.That(BasicShotTexts.StatLine(Sixth, 3, V), Is.EqualTo("Sixth Chamber III  every 5th shot x3, through 1, +10 stagger"));
            Assert.That(BasicShotTexts.StatLine(Hunter, 2, V), Is.EqualTo("Hunter's Count II  2 marks every 3rd hit on one enemy"));
            Assert.That(BasicShotTexts.StatLine(Pass, 3, V), Is.EqualTo("Pass It On III  marks pass within 7 m, on any kill"));
            OfferCard card = BasicShotTexts.Card(new BasicShotOffer { Kind = Hunter, Tier = 1 }, V);
            Assert.That(card.KindLine, Is.EqualTo("BASIC SHOT UPGRADE"));
            Assert.That(card.Name, Is.EqualTo("Hunter's Count I"));
            Assert.That(card.Numbers.Single(), Does.Contain("a mark every 5th hit on anyone").And.Contain("a mark every 3rd hit on one enemy"));
            Assert.That(BasicShotTexts.Card(new BasicShotOffer { Kind = Sixth, Tier = 2 }, V).Numbers.Single(), Does.Contain("every 6th shot x2").And.Contain("every 5th shot x2"));

            // Her sheet: Sixth Chamber's count beside its tier (the shots fired toward the heavy round), the others their tier.
            var c = new OfferContext { Modifiers = new RunModifiers { StatsAndTiers = true, StatValues = new StatValues() } };
            c.Modifiers.BasicShot.Take(Sixth);
            c.Modifiers.BasicShot.Take(Pass);
            c.Modifiers.BasicShot.ChamberFired = 3;
            List<SheetStat> sheet = HerSheet.Stats(c).Where(s => s.Group == SheetGroup.BasicShot).ToList();
            Assert.That(sheet.Select(s => (s.Name, s.Value)), Is.EqualTo(new[] { ("Sixth Chamber", "I  3 / 6"), ("Pass It On", "I") }));
            c.Modifiers.BasicShot.ChamberFired = 5;
            Assert.That(HerSheet.Stats(c).First(s => s.Name == "Sixth Chamber").Value, Is.EqualTo("I  HEAVY NEXT"));
            c.Modifiers.BasicShot.Values = new BasicShotUpgradeValues { SixthChamber = false };
            Assert.That(HerSheet.Stats(c).First(s => s.Name == "Sixth Chamber").Value, Is.EqualTo("I  off"), "switched off: no count");
        }
    }
}
