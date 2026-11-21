using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 029 (2026-11-20; decision 0009, a proposal he has not ruled on): what lasts between runs, as rules. What
    /// a run earns for every way it can end; the save's round trip, a missing file, a damaged file, a newer version; the
    /// budget and its feats; buying and equipping; each piece's effect on a run's modifiers as numbers; and nothing
    /// equipped is the same as the switch off. The save is only ever written under a temporary folder of this test.
    /// </summary>
    public class BetweenRunsTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "BetweenRunsTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string SavePath => Path.Combine(_folder, "between-runs.json");

        static MetaState Rich(int currency = 10000, bool allFeats = true)
        {
            var s = new MetaState { Currency = currency };
            if (allFeats) s.Feats.AddRange(MetaFeats.All.Select(MetaFeats.Id));
            return s;
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOffInCode_AndTheCurrencyIsNamedEchoes()
        {
            var v = new MetaValues();
            Assert.That(v.Enabled, Is.False, "off by default");
            Assert.That(v.CurrencyName, Is.EqualTo("Echoes"));
            Assert.That(v.Pieces.Select(p => p.Name), Is.EqualTo(new[]
            {
                "Steady Start", "Marked Target", "Second Look", "Not That One", "Spare Step", "Deep Chests", "Thick Hide",
                "Thick Hide II", "Heavy Hand", "Knight's Trophy",
            }), "the order's table, in its order");
            Assert.That(v.Pieces.Select(p => p.Id).Distinct().Count(), Is.EqualTo(v.Pieces.Length), "every piece its own id");
        }

        // ---------------- What a run earns ----------------

        static RunEarnings Run(int floors, int elites, bool boss)
        {
            var e = new RunEarnings { FloorsCleared = floors, ElitesCleared = elites, BossKilled = boss };
            return e;
        }

        [TestCase(0, 0, false, 0, TestName = "Earns_DiedOnFloor1_Nothing")]
        [TestCase(0, 1, false, 15, TestName = "Earns_DiedOnFloor1_AfterItsEliteRoom_15")]
        [TestCase(1, 1, false, 25, TestName = "Earns_DiedOnFloor2_AfterFloor1AndItsEliteRoom_25")]
        [TestCase(1, 0, false, 10, TestName = "Earns_DiedOnFloor2_EliteRoomWalkedPast_10")]
        [TestCase(3, 2, false, 60, TestName = "Earns_LeftOnFloor4_60")]
        [TestCase(4, 4, false, 100, TestName = "Earns_DiedToTheBoss_100")]
        [TestCase(5, 5, true, 225, TestName = "Earns_ActCleared_EveryEliteRoom_225")]
        [TestCase(5, 0, true, 150, TestName = "Earns_ActCleared_NoEliteRoom_150")]
        public void WhatARunEarns(int floors, int elites, bool boss, int expected)
        {
            var v = new MetaValues();
            RunEarnings e = Run(floors, elites, boss);
            Assert.That(e.Total(v), Is.EqualTo(expected));
            Assert.That(e.FromFloors(v) + e.FromElites(v) + e.FromBoss(v), Is.EqualTo(expected));

            // Kept, never lost: on top of what she had.
            var s = new MetaState { Currency = 7 };
            MetaRack.Commit(s, v, e);
            Assert.That(s.Currency, Is.EqualTo(7 + expected));
            Assert.That(s.EarnedInAll, Is.EqualTo(expected));
            Assert.That(e.TotalAfter, Is.EqualTo(7 + expected));
            MetaRack.Commit(s, v, e);
            Assert.That(s.Currency, Is.EqualTo(7 + expected), "a run is counted once");
            Assert.That(s.Runs, Is.EqualTo(1));
        }

        [Test]
        public void TheWholeRack_Costs810_AndTheTrophyIsNotSold()
        {
            var v = new MetaValues();
            Assert.That(MetaRack.WholePrice(v), Is.EqualTo(40 + 60 + 60 + 80 + 120 + 60 + 80 + 160 + 150));
            Assert.That(MetaRack.Find(v, MetaPieces.KnightsTrophy).Sold, Is.False);
        }

        // ---------------- The budget and the feats ----------------

        [Test]
        public void TheBudget_StartsAt3_GrowsByOnePerFeat_OnceEach_To8()
        {
            var v = new MetaValues();
            var s = new MetaState();
            Assert.That(MetaRack.Budget(s, v), Is.EqualTo(3));

            var first = new RunEarnings();
            first.Do(MetaFeat.FirstFloor);
            first.Do(MetaFeat.FirstFloor);
            Assert.That(MetaRack.Commit(s, v, first), Is.EqualTo(new[] { MetaFeat.FirstFloor }));
            Assert.That(MetaRack.Budget(s, v), Is.EqualTo(4));

            var again = new RunEarnings();
            again.Do(MetaFeat.FirstFloor);
            Assert.That(MetaRack.Commit(s, v, again), Is.Empty, "done before: nothing new");
            Assert.That(MetaRack.Budget(s, v), Is.EqualTo(4), "the same feat raises it once");

            var rest = new RunEarnings();
            foreach (MetaFeat f in MetaFeats.All) rest.Do(f);
            Assert.That(MetaRack.Commit(s, v, rest), Is.EqualTo(new[] { MetaFeat.EliteAboveHalf, MetaFeat.ReachFloor3, MetaFeat.ReachFloor5, MetaFeat.ActBoss }));
            Assert.That(MetaRack.Budget(s, v), Is.EqualTo(8), "all five: 8");

            v.MostBudget = 6;
            Assert.That(MetaRack.Budget(s, v), Is.EqualTo(6), "never over the most");
        }

        [Test]
        public void KillingTheActBoss_GivesKnightsTrophy_NotBought()
        {
            var v = new MetaValues();
            var s = new MetaState();
            Assert.That(MetaRack.Buy(s, v, MetaPieces.KnightsTrophy), Is.EqualTo(MetaBuy.NotForSale));
            var run = Run(5, 0, true);
            run.Do(MetaFeat.ActBoss);
            MetaRack.Commit(s, v, run);
            Assert.That(s.Owns(MetaPieces.KnightsTrophy), Is.True);
            Assert.That(s.Currency, Is.EqualTo(150), "nothing paid for it");
            Assert.That(MetaRack.Equip(s, v, MetaPieces.KnightsTrophy), Is.EqualTo(MetaEquip.Equipped));
        }

        // ---------------- Buying ----------------

        [Test]
        public void Buying_NotEnough_AlreadyOwned_AndTheSecondRank()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 79 };
            Assert.That(MetaRack.Buy(s, v, MetaPieces.ThickHide), Is.EqualTo(MetaBuy.NotEnough));
            Assert.That(s.Currency, Is.EqualTo(79), "nothing taken");
            Assert.That(s.Owned, Is.Empty);

            s.Currency = 300;
            Assert.That(MetaRack.Buy(s, v, MetaPieces.ThickHide2), Is.EqualTo(MetaBuy.NeedsFirst), "the second rank after the first");
            Assert.That(MetaRack.Buy(s, v, MetaPieces.ThickHide), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.Currency, Is.EqualTo(220));
            Assert.That(MetaRack.Buy(s, v, MetaPieces.ThickHide), Is.EqualTo(MetaBuy.AlreadyOwned));
            Assert.That(s.Currency, Is.EqualTo(220), "once");
            Assert.That(MetaRack.Buy(s, v, MetaPieces.ThickHide2), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.Currency, Is.EqualTo(60));
            Assert.That(s.IsEquipped(MetaPieces.ThickHide), Is.False, "buying does not equip");
            Assert.That(MetaRack.Buy(s, v, "no-such-piece"), Is.EqualTo(MetaBuy.Unknown));
        }

        // ---------------- Equipping ----------------

        [Test]
        public void Equipping_WithinAndOverTheBudget_AndWhatANeedTakesOff()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 1000 };
            foreach (string id in new[] { MetaPieces.HeavyHand, MetaPieces.SteadyStart, MetaPieces.ThickHide, MetaPieces.ThickHide2 })
                Assert.That(MetaRack.Buy(s, v, id), Is.EqualTo(MetaBuy.Bought), id);

            Assert.That(MetaRack.Equip(s, v, MetaPieces.MarkedTarget), Is.EqualTo(MetaEquip.NotOwned));
            Assert.That(MetaRack.Equip(s, v, MetaPieces.HeavyHand), Is.EqualTo(MetaEquip.Equipped), "3 of 3");
            Assert.That(MetaRack.PointsUsed(s, v), Is.EqualTo(3));
            Assert.That(MetaRack.PointsLeft(s, v), Is.Zero);
            Assert.That(MetaRack.Equip(s, v, MetaPieces.SteadyStart), Is.EqualTo(MetaEquip.OverBudget));
            Assert.That(MetaRack.Equip(s, v, MetaPieces.HeavyHand), Is.EqualTo(MetaEquip.AlreadyEquipped));

            Assert.That(MetaRack.TakeOff(s, v, MetaPieces.HeavyHand), Is.True);
            Assert.That(MetaRack.TakeOff(s, v, MetaPieces.HeavyHand), Is.False, "not on any more");
            Assert.That(MetaRack.Equip(s, v, MetaPieces.ThickHide2), Is.EqualTo(MetaEquip.NeedsFirst), "the second rank on top of the first");
            Assert.That(MetaRack.Equip(s, v, MetaPieces.ThickHide), Is.EqualTo(MetaEquip.Equipped));
            Assert.That(MetaRack.Equip(s, v, MetaPieces.ThickHide2), Is.EqualTo(MetaEquip.Equipped), "2 + 1 = 3 of 3");
            Assert.That(MetaRack.TakeOff(s, v, MetaPieces.ThickHide), Is.True);
            Assert.That(s.Equipped, Is.Empty, "taking the first off takes the second off too");
        }

        // ---------------- Each piece on a run's modifiers ----------------

        static RunModifiers WithOnly(string id, string struck = "")
        {
            var v = new MetaValues();
            MetaState s = Rich();
            Assert.That(MetaRack.Buy(s, v, id) == MetaBuy.Bought || s.Owns(id) || MetaRack.Find(v, id).Sold == false, id);
            if (!s.Owns(id)) s.Owned.Add(id);
            Assert.That(MetaRack.Equip(s, v, id), Is.EqualTo(MetaEquip.Equipped), id);
            var m = new RunModifiers();
            MetaRack.Apply(s, v, m.Meta, struck);
            return m;
        }

        [Test]
        public void SteadyStart_StartsWithHalfTheFocus()
        {
            RunModifiers m = WithOnly(MetaPieces.SteadyStart);
            Assert.That(m.Meta.StartFocusShare, Is.EqualTo(0.5f));
            Assert.That(m.Meta.Pieces, Is.EqualTo(new[] { "Steady Start" }));
        }

        [Test]
        public void MarkedTarget_OneMarkMoreOnAFirstHit_AndSecondLook_OneRedraw()
        {
            Assert.That(WithOnly(MetaPieces.MarkedTarget).Meta.FirstMarkExtra, Is.EqualTo(1));
            RunModifiers m = WithOnly(MetaPieces.SecondLook);
            Assert.That(m.Meta.Redraws, Is.EqualTo(1));
            Assert.That(m.Meta.RedrawsLeft, Is.EqualTo(1));
        }

        [Test]
        public void NotThatOne_StrikesOutTheSkillChosen()
        {
            Assert.That(WithOnly(MetaPieces.NotThatOne, "Fan Fire").Meta.StruckOut, Is.EqualTo("Fan Fire"));
            Assert.That(WithOnly(MetaPieces.SteadyStart, "Fan Fire").Meta.StruckOut, Is.EqualTo(""), "only with the piece");

            var pool = new List<string> { "Powder Keg", "Fan Fire", "Long Shot" };
            Assert.That(MetaRack.StruckOut("", pool), Is.EqualTo("Powder Keg"), "the pool's first when none is kept");
            Assert.That(MetaRack.StruckOut("Fan Fire", pool), Is.EqualTo("Fan Fire"));
            Assert.That(MetaRack.StruckOut("Fan Fire", pool, 1), Is.EqualTo("Long Shot"));
            Assert.That(MetaRack.StruckOut("Long Shot", pool, 1), Is.EqualTo("Powder Keg"), "round");
            Assert.That(MetaRack.StruckOut("Powder Keg", pool, -1), Is.EqualTo("Long Shot"));
            Assert.That(MetaRack.StruckOut("Gone", pool), Is.EqualTo("Powder Keg"));
        }

        [Test]
        public void SpareStep_TwoDashCharges_EachAThirdLonger()
        {
            RunModifiers m = WithOnly(MetaPieces.SpareStep);
            Assert.That(m.Meta.DashCharges, Is.EqualTo(2));
            Assert.That(m.DashCooldownShare, Is.EqualTo(4f / 3f).Within(1e-5f));
            m.Take(new Upgrade { Kind = UpgradeKind.DashCooldown, Amount = 0.08f });
            Assert.That(m.DashCooldownShare, Is.EqualTo(0.92f * 4f / 3f).Within(1e-5f), "with a chest's step too");

            // The dash itself: two, then none; the first back after a third more than its cooldown.
            var feel = new FeelValues();
            var dash = new Dash { MaxCharges = 2 };
            float share = 4f / 3f;
            Assert.That(dash.ChargesReady(0f), Is.EqualTo(2));
            Assert.That(dash.TryStart(0f, feel, share), Is.True);
            Assert.That(dash.TryStart(0.5f, feel, share), Is.True, "the second charge");
            Assert.That(dash.TryStart(0.6f, feel, share), Is.False, "not a third");
            float back = feel.DashCooldown * share;
            Assert.That(dash.CooldownRemaining(0.6f), Is.EqualTo(back - 0.6f).Within(1e-4f));
            Assert.That(dash.CanStart(back - 0.01f), Is.False);
            Assert.That(dash.ChargesReady(back + 0.01f), Is.EqualTo(1));
            Assert.That(dash.ChargesReady(2f * back + 0.01f), Is.EqualTo(2), "one at a time");
            dash.Clear();
            Assert.That(dash.ChargesReady(1f), Is.EqualTo(2), "a clean slate");
        }

        [Test]
        public void TheDash_WithOneCharge_IsTheDashItAlwaysWas()
        {
            var feel = new FeelValues();
            var a = new Dash();
            var b = new Dash { MaxCharges = 3 };
            b.MaxCharges = 1;
            foreach (Dash d in new[] { a, b })
            {
                Assert.That(d.TryStart(1f, feel), Is.True);
                Assert.That(d.CanStart(1f + feel.DashCooldown - 0.01f), Is.False);
                Assert.That(d.CooldownRemaining(2f), Is.EqualTo(feel.DashCooldown - 1f).Within(1e-4f));
                Assert.That(d.CutCooldown(2f, 0.5f), Is.EqualTo((feel.DashCooldown - 1f) * 0.5f).Within(1e-4f));
                Assert.That(d.ChargesReady(2f), Is.Zero);
            }
        }

        [Test]
        public void DeepChests_TheFirstChestOffersOneMore_TheSameOnesFirst() // work order 065: three a chest, four with Deep Chests
        {
            RunModifiers m = WithOnly(MetaPieces.DeepChests);
            Assert.That(m.Meta.FirstChestExtra, Is.EqualTo(1));
            var values = new RunValues();
            var skills = new[] { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
            for (ulong seed = 1; seed < 40; seed++)
            {
                List<Upgrade> two = ChestUpgrades.Offer(seed, 1, 3, skills, values);
                List<Upgrade> three = ChestUpgrades.Offer(seed, 1, 3, skills, values, m.Meta.FirstChestExtra);
                Assert.That(two.Count, Is.EqualTo(values.ChestOffers), "work order 065: three with the stats on");
                Assert.That(three.Count, Is.EqualTo(values.ChestOffers + 1));
                Assert.That(three.Take(two.Count).Select(u => u.Short), Is.EqualTo(two.Select(u => u.Short)), "seed " + seed);
                Assert.That(three.Select(u => u.Kind).Distinct().Count(), Is.EqualTo(three.Count), "different rows");
            }
        }

        [Test]
        public void ThickHide_Plus20Percent_AndTheSecondRank_Plus20More()
        {
            RunModifiers m = WithOnly(MetaPieces.ThickHide);
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(120));
            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10f, Heal = 10 });
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(130), "the chests' health on top");

            var v = new MetaValues();
            MetaState s = Rich();
            MetaRack.Buy(s, v, MetaPieces.ThickHide);
            MetaRack.Buy(s, v, MetaPieces.ThickHide2);
            MetaRack.Equip(s, v, MetaPieces.ThickHide);
            MetaRack.Equip(s, v, MetaPieces.ThickHide2);
            var both = new RunModifiers();
            MetaRack.Apply(s, v, both.Meta);
            Assert.That(both.MaxHealthFor(100), Is.EqualTo(140));
            Assert.That(MetaRack.PointsUsed(s, v), Is.EqualTo(3));
        }

        [Test]
        public void HeavyHand_Plus15PercentDamage_TimesTheChestsSteps()
        {
            RunModifiers m = WithOnly(MetaPieces.HeavyHand);
            Assert.That(m.DamageShare, Is.EqualTo(1.15f).Within(1e-6f));
            Assert.That(m.ScaleDamage(100), Is.EqualTo(115));
            Assert.That(m.ScaleDamage(40), Is.EqualTo(46));
            m.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f });
            Assert.That(m.DamageShare, Is.EqualTo(1.05f * 1.15f).Within(1e-5f));
        }

        [Test]
        public void KnightsTrophy_OneSkillUpgradeMore()
        {
            RunModifiers m = WithOnly(MetaPieces.KnightsTrophy);
            Assert.That(m.Meta.EliteOffersExtra, Is.EqualTo(1));
            var had = new HashSet<string> { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
            var values = new SkillUpgradeValues();
            List<SkillUpgradeDefinition> four = SkillUpgradeOffer.Offer(5, 1, values.Table, had, Array.Empty<SkillUpgradeDefinition>(), values.Offered + m.Meta.EliteOffersExtra);
            Assert.That(four.Count, Is.EqualTo(4));
            Assert.That(four.Select(u => u.Skill).Distinct().Count(), Is.EqualTo(4), "one for each of her four skills");
        }

        [Test]
        public void NothingEquipped_IsTheSwitchOff_EveryNumberAsBefore()
        {
            var v = new MetaValues();
            MetaState s = Rich();
            foreach (MetaPiece p in v.Pieces) if (p.Sold) MetaRack.Buy(s, v, p.Id); // owning changes nothing; equipping does
            var withRack = new RunModifiers();
            MetaRack.Apply(s, v, withRack.Meta, "Fan Fire");
            var off = new RunModifiers();
            Assert.That(withRack.Meta.Any, Is.False);
            Assert.That(withRack.Meta.ToString(), Is.EqualTo(new RunMetaEffects().ToString()));
            var chests = new[]
            {
                new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f }, new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10f },
                new Upgrade { Kind = UpgradeKind.DashCooldown, Amount = 0.08f }, new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f },
            };
            for (int step = 0; step <= chests.Length; step++)
            {
                if (step > 0)
                {
                    withRack.Take(chests[step - 1]);
                    off.Take(chests[step - 1]);
                }
                Assert.That(withRack.DamageShare, Is.EqualTo(off.DamageShare));
                Assert.That(withRack.DashCooldownShare, Is.EqualTo(off.DashCooldownShare));
                Assert.That(withRack.BonusMaxHealth, Is.EqualTo(off.BonusMaxHealth));
                for (int own = 1; own <= 300; own += 7) Assert.That(withRack.MaxHealthFor(own), Is.EqualTo(own + off.BonusMaxHealth));
                for (int dmg = 0; dmg <= 500; dmg += 3) Assert.That(withRack.ScaleDamage(dmg), Is.EqualTo(off.ScaleDamage(dmg)));
            }
            // And cleared at a run's end, whatever was on.
            RunModifiers m = WithOnly(MetaPieces.HeavyHand);
            m.Clear();
            Assert.That(m.Meta.Any, Is.False);
            Assert.That(m.DamageShare, Is.EqualTo(1f));
        }

        // ---------------- Second Look's redraw ----------------

        [Test]
        public void TheRedraw_NeverOffersASkillShHas_PrefersOthers_AndIsTheSameForTheSameRun()
        {
            var had = new List<bool> { true, false, false, false, false, true, true, true, false, false, false, false };
            List<int> first = FloorRewards.Offer(77, 2, had, 3);
            List<int> again = RewardRedraw.Offer(77, 2, had, 3, first);
            Assert.That(again.Count, Is.EqualTo(3));
            Assert.That(again.All(s => !had[s]), "never one she has");
            Assert.That(again.Intersect(first), Is.Empty, "five others left: none of those shown");
            Assert.That(RewardRedraw.Offer(77, 2, had, 3, first), Is.EqualTo(again), "the same run redraws the same");

            var few = new List<bool> { true, true, true, true, false, false, false, true };
            List<int> shown = FloorRewards.Offer(5, 1, few, 3);
            List<int> refill = RewardRedraw.Offer(5, 1, few, 3, shown);
            Assert.That(refill.OrderBy(s => s), Is.EqualTo(shown.OrderBy(s => s)), "only three left: the same three");
        }

        // ---------------- The save ----------------

        [Test]
        public void TheSave_RoundTrip_KeepsEverything()
        {
            var v = new MetaValues();
            MetaState s = Rich(345);
            s.Owned.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.SteadyStart, MetaPieces.NotThatOne });
            s.Equipped.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.NotThatOne });
            s.EarnedInAll = 999;
            s.Runs = 12;
            s.StruckOut = "Fan \"Fire\" \\ one";
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            Assert.That(File.Exists(SavePath), Is.True);
            Assert.That(File.Exists(SavePath + ".tmp"), Is.False, "no temporary file left");

            MetaState back = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(kept, Is.Empty);
            Assert.That(back.Version, Is.EqualTo(MetaState.CurrentVersion));
            Assert.That(back.Currency, Is.EqualTo(345));
            Assert.That(back.EarnedInAll, Is.EqualTo(999));
            Assert.That(back.Runs, Is.EqualTo(12));
            Assert.That(back.Owned, Is.EquivalentTo(s.Owned.Concat(new[] { MetaPieces.KnightsTrophy })), "and the trophy her boss feat gives");
            Assert.That(back.Equipped, Is.EqualTo(s.Equipped));
            Assert.That(back.Feats, Is.EqualTo(s.Feats));
            Assert.That(back.StruckOut, Is.EqualTo(s.StruckOut));

            // Written again over the old one.
            back.Currency = 1;
            Assert.That(MetaSaveFile.Save(SavePath, back, out error), Is.True, error);
            Assert.That(MetaSaveFile.Load(SavePath, v, out how, out _).Currency, Is.EqualTo(1));
            StringAssert.Contains("\"version\": 1", File.ReadAllText(SavePath));
        }

        [Test]
        public void AMissingSave_IsAnEmptyOne()
        {
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Missing));
            Assert.That(kept, Is.Empty);
            Assert.That(s.Currency, Is.Zero);
            Assert.That(s.Owned, Is.Empty);
            Assert.That(Directory.Exists(_folder), Is.False, "reading writes nothing");
        }

        [TestCase("{ \"version\": 1, \"currency\": ", TestName = "ADamagedSave_CutShort_IsKeptAside_AndAnEmptyOneGiven")]
        [TestCase("not json at all", TestName = "ADamagedSave_NotJson_IsKeptAside_AndAnEmptyOneGiven")]
        [TestCase("[1, 2, 3]", TestName = "ADamagedSave_NotAnObject_IsKeptAside_AndAnEmptyOneGiven")]
        [TestCase("{ \"currency\": 40 }", TestName = "ADamagedSave_NoVersion_IsKeptAside_AndAnEmptyOneGiven")]
        [TestCase("{ \"version\": 1, \"owned\": 5 }", TestName = "ADamagedSave_AListThatIsNot_IsKeptAside_AndAnEmptyOneGiven")]
        [TestCase("", TestName = "ADamagedSave_Empty_IsKeptAside_AndAnEmptyOneGiven")]
        public void ADamagedSave(string text)
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SavePath, text);
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Damaged));
            Assert.That(s.Currency, Is.Zero);
            Assert.That(s.Owned, Is.Empty);
            Assert.That(File.Exists(SavePath), Is.False, "moved aside");
            Assert.That(kept, Does.StartWith(SavePath + ".damaged-"));
            Assert.That(File.ReadAllText(kept), Is.EqualTo(text), "kept as it was, not deleted");

            // The new one is written beside it.
            s.Currency = 5;
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            Assert.That(File.Exists(kept), Is.True);
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _).Currency, Is.EqualTo(5));
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
        }

        [Test]
        public void ASaveOfANewerVersion_IsKeptAside_AndAnEmptyOneGiven()
        {
            Directory.CreateDirectory(_folder);
            string text = "{ \"version\": 2, \"currency\": 500, \"owned\": [\"thick-hide\"], \"somethingNew\": { \"a\": [1, true, null] } }";
            File.WriteAllText(SavePath, text);
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Newer));
            Assert.That(s.Currency, Is.Zero, "an empty save");
            Assert.That(kept, Does.StartWith(SavePath + ".newer-"));
            Assert.That(File.ReadAllText(kept), Is.EqualTo(text));
        }

        [Test]
        public void ASaveWithFieldsItDoesNotKnow_IsRead_AndTidied()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SavePath,
                "{\"version\":1,\"currency\":-20,\"extra\":[{\"x\":1.5e2}],\"owned\":[\"thick-hide\",\"gone-piece\",\"thick-hide\",\"heavy-hand\",\"steady-start\"]," +
                "\"equipped\":[\"heavy-hand\",\"steady-start\",\"marked-target\",\"thick-hide-2\"],\"feats\":[\"reach-floor-3\",\"no-such-feat\"],\"struckOut\":\"Slide\"}");
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(s.Currency, Is.Zero, "never under 0");
            Assert.That(s.Owned, Is.EqualTo(new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand, MetaPieces.SteadyStart }), "unknown and twice dropped");
            Assert.That(s.Feats, Is.EqualTo(new[] { "reach-floor-3" }));
            Assert.That(s.Equipped, Is.EqualTo(new[] { MetaPieces.HeavyHand, MetaPieces.SteadyStart }), "not owned dropped; 3 + 1 within the budget of 4");
            Assert.That(s.StruckOut, Is.EqualTo("Slide"));

            // Over the budget: the last equipped come off.
            File.WriteAllText(SavePath, "{\"version\":1,\"owned\":[\"heavy-hand\",\"steady-start\"],\"equipped\":[\"heavy-hand\",\"steady-start\"]}");
            s = MetaSaveFile.Load(SavePath, new MetaValues(), out how, out _);
            Assert.That(s.Equipped, Is.EqualTo(new[] { MetaPieces.HeavyHand }));
        }

        [Test]
        public void AFailedWrite_SaysWhy_AndThrowsNothing()
        {
            Directory.CreateDirectory(_folder);
            string blocked = Path.Combine(_folder, "a-folder-in-the-way");
            Directory.CreateDirectory(blocked);
            Assert.That(MetaSaveFile.Save(blocked, new MetaState(), out string error), Is.False);
            Assert.That(error, Is.Not.Empty);
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_HasALineForWhatLasts_OnlyWhenTheRunHadIt()
        {
            SessionEvent E(double t, string type, string name, params string[] extras)
            {
                var e = new SessionEvent(t, type, name);
                for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
                return e;
            }
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.RunStart, "AAAAAAAA"),
                E(1, SessionEventTypes.FloorEnter, "s1", "floor", "1"),
                E(9, SessionEventTypes.RunEnd, null),
                E(10, SessionEventTypes.RunStart, "BBBBBBBB"),
                E(11, SessionEventTypes.FloorEnter, "s1", "floor", "1"),
                E(11, SessionEventTypes.MetaLoadout, "Steady Start | Thick Hide", "currency", "40", "budget", "4", "used", "3"),
                E(20, SessionEventTypes.MetaEarn, "elite room", "amount", "15", "floor", "1"),
                E(30, SessionEventTypes.MetaEarn, "floor", "amount", "10", "floor", "1"),
                E(31, SessionEventTypes.MetaFeat, "clear-floor-1"),
                E(32, SessionEventTypes.RunEnd, null),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs[0].HasMeta, Is.False);
            RunRow on = stats.Runs[1];
            Assert.That(on.HasMeta, Is.True);
            Assert.That(on.MetaEquipped, Is.EqualTo("Steady Start, Thick Hide"));
            Assert.That(on.MetaEarned, Is.EqualTo(25));
            Assert.That(on.MetaFeats, Is.EqualTo(new[] { "clear-floor-1" }));
            string text = SessionSummary.Format(stats);
            Assert.That(text.Split('\n').Count(l => l.Contains("between runs")), Is.EqualTo(1), "one line, for the run that had it");
            StringAssert.Contains("between runs     equipped Steady Start, Thick Hide;  earned 25 (elite room 15, floor 10);  new feats clear-floor-1", text);
        }
    }
}
