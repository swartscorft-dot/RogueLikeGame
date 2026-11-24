using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 099 (Kristian 2026-11-24, "Rang som kjøpes mange ganger"): the ranks bought between runs, as rules. The
    /// price of every rank of every stat; buying with too little, at the last rank, of a stat that is not there; a save of
    /// before read as rank 0 of everything, a save with ranks written and read back, a newer one kept aside; what ranks alone,
    /// the rack alone and both together do to her stats (with the chests' steps too); nothing bought is the trial off, field
    /// for field; the recorder's line in the summary; the runs a kind of run needs for the store, as the report's table. The
    /// save is only ever written under a temporary folder of this test.
    /// </summary>
    public class BetweenRunsRanksTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "BetweenRunsRanksTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string SavePath => Path.Combine(_folder, "between-runs.json");

        static readonly int[] Prices = { 40, 55, 80, 110, 155, 215, 300, 420, 590, 825 };

        // ---------------- The table and the price ----------------

        [Test]
        public void TheTable_IsTheOrders_FourStats_EachWithItsStepAndItsLastRank()
        {
            var v = new MetaValues();
            Assert.That(v.Ranks.Select(r => r.Name), Is.EqualTo(new[] { "Health", "Damage", "Crit chance", "Room heal" }));
            Assert.That(v.Ranks.Select(r => r.Step), Is.EqualTo(new[] { 0.10f, 0.06f, 0.03f, 0.02f }));
            Assert.That(v.Ranks.Select(r => r.Most), Is.EqualTo(new[] { 10, 10, 8, 5 }));
            Assert.That(v.Ranks.Select(r => r.Id).Distinct().Count(), Is.EqualTo(4), "every stat its own id");
            Assert.That(v.FirstPrice, Is.EqualTo(40));
            Assert.That(v.PriceGrowth, Is.EqualTo(1.4f));
            Assert.That(v.Ranks.Any(r => v.Pieces.Any(p => p.Id == r.Id)), Is.False, "no rank is a piece of the rack");
        }

        [Test]
        public void ThePrice_OfEveryRankOfEveryStat_TheFirstTimesTheGrowth_RoundedToTheNearest5()
        {
            var v = new MetaValues();
            for (int k = 1; k <= 10; k++) Assert.That(MetaRanks.Price(v, k), Is.EqualTo(Prices[k - 1]), "rank " + k);
            foreach (MetaRank r in v.Ranks)
            {
                var s = new MetaState { Currency = 100000 };
                for (int k = 1; k <= r.Most; k++)
                {
                    Assert.That(MetaRanks.NextPrice(s, v, r), Is.EqualTo(Prices[k - 1]), r.Name + " rank " + k);
                    int before = s.Currency;
                    Assert.That(MetaRanks.Buy(s, v, r.Id), Is.EqualTo(MetaRankBuy.Bought), r.Name + " rank " + k);
                    Assert.That(before - s.Currency, Is.EqualTo(Prices[k - 1]), r.Name + " rank " + k + " paid");
                    Assert.That(MetaRanks.RankOf(s, r), Is.EqualTo(k));
                }
                Assert.That(MetaRanks.NextPrice(s, v, r), Is.Zero, r.Name + " at its last rank: no price");
                Assert.That(MetaRanks.WholePrice(v, r), Is.EqualTo(Prices.Take(r.Most).Sum()), r.Name);
            }
            Assert.That(MetaRanks.WholePrice(v, MetaRanks.Find(v, MetaRanks.Health)), Is.EqualTo(2790));
            Assert.That(MetaRanks.WholePrice(v, MetaRanks.Find(v, MetaRanks.CritChance)), Is.EqualTo(1375));
            Assert.That(MetaRanks.WholePrice(v, MetaRanks.Find(v, MetaRanks.RoomHeal)), Is.EqualTo(440));
            Assert.That(MetaRanks.WholePrice(v), Is.EqualTo(2790 + 2790 + 1375 + 440), "the whole store: 7395");

            // The rule, not the list: another first price and growth.
            v.FirstPrice = 30;
            v.PriceGrowth = 1.5f;
            Assert.That(Enumerable.Range(1, 5).Select(k => MetaRanks.Price(v, k)), Is.EqualTo(new[] { 30, 45, 70, 100, 150 }), "30, 45, 67.5, 101.25, 151.9");
            Assert.That(MetaRanks.Price(v, 0), Is.Zero, "no rank 0 to buy");
        }

        // ---------------- Buying ----------------

        [Test]
        public void Buying_WithTooLittle_TakesNothing()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 39 };
            Assert.That(MetaRanks.CanBuy(s, v, MetaRanks.Health), Is.EqualTo(MetaRankBuy.NotEnough));
            Assert.That(MetaRanks.Buy(s, v, MetaRanks.Health), Is.EqualTo(MetaRankBuy.NotEnough));
            Assert.That(s.Currency, Is.EqualTo(39), "nothing taken");
            Assert.That(s.RankOf(MetaRanks.Health), Is.Zero);
            Assert.That(s.HasRanks, Is.False);

            s.Currency = 40;
            Assert.That(MetaRanks.Buy(s, v, MetaRanks.Health), Is.EqualTo(MetaRankBuy.Bought), "exactly enough");
            Assert.That(s.Currency, Is.Zero);
            Assert.That(MetaRanks.Buy(s, v, MetaRanks.Health), Is.EqualTo(MetaRankBuy.NotEnough), "the second costs 55");
            Assert.That(s.RankOf(MetaRanks.Health), Is.EqualTo(1));
        }

        [Test]
        public void Buying_AtTheLastRank_TakesNothing_AndOfAStatNotThere_IsUnknown()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 100000 };
            for (int k = 0; k < 5; k++) MetaRanks.Buy(s, v, MetaRanks.RoomHeal);
            int left = s.Currency;
            Assert.That(MetaRanks.CanBuy(s, v, MetaRanks.RoomHeal), Is.EqualTo(MetaRankBuy.AtMost));
            Assert.That(MetaRanks.Buy(s, v, MetaRanks.RoomHeal), Is.EqualTo(MetaRankBuy.AtMost));
            Assert.That(s.Currency, Is.EqualTo(left), "nothing taken at the last rank");
            Assert.That(s.RankOf(MetaRanks.RoomHeal), Is.EqualTo(5));

            Assert.That(MetaRanks.Buy(s, v, "no-such-stat"), Is.EqualTo(MetaRankBuy.Unknown));
            Assert.That(MetaRanks.Buy(s, v, MetaPieces.ThickHide), Is.EqualTo(MetaRankBuy.Unknown), "a piece of the rack is no rank");
            Assert.That(MetaRanks.Buy(s, v, null), Is.EqualTo(MetaRankBuy.Unknown));
            Assert.That(s.Currency, Is.EqualTo(left));
            Assert.That(s.Owned, Is.Empty, "buying a rank owns no piece");
            Assert.That(MetaRack.PointsUsed(s, v), Is.Zero, "and takes no budget point");
        }

        [Test]
        public void ARankBought_IsNeitherEquippedNorTakenOff_AndCountsWhateverTheRackHolds()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 1000 };
            s.Feats.AddRange(MetaFeats.All.Select(MetaFeats.Id));
            MetaRanks.Buy(s, v, MetaRanks.Damage);
            Assert.That(MetaRack.Budget(s, v) - MetaRack.PointsLeft(s, v), Is.Zero, "no point used");
            Assert.That(MetaRack.Equip(s, v, MetaRanks.Damage), Is.EqualTo(MetaEquip.Unknown), "not a piece to equip");
            Assert.That(MetaRack.TakeOff(s, v, MetaRanks.Damage), Is.False);
            var m = new RunModifiers();
            MetaRack.Apply(s, v, m.Meta);
            MetaRanks.Apply(s, v, m.Meta);
            Assert.That(m.DamageShare, Is.EqualTo(1.06f).Within(1e-6f), "nothing equipped: the rank counts");
        }

        // ---------------- The save ----------------

        [Test]
        public void ASaveOfBefore_NoRanks_IsReadAsRank0OfEverything_AndLosesNothing()
        {
            Directory.CreateDirectory(_folder);
            const string before =
                "{\n  \"version\": 1,\n  \"currency\": 45,\n  \"earnedInAll\": 100,\n  \"runs\": 3,\n  \"owned\": [\"thick-hide\", \"steady-start\"],\n"
                + "  \"equipped\": [\"thick-hide\"],\n  \"feats\": [\"clear-floor-1\"],\n  \"struckOut\": \"Fan Fire\",\n  \"attachments\": [\"laser-sight\"],\n  \"fitted\": [\"laser-sight\"]\n}\n";
            File.WriteAllText(SavePath, before);
            var v = new MetaValues();
            MetaState s = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(kept, Is.Empty);
            foreach (MetaRank r in v.Ranks) Assert.That(MetaRanks.RankOf(s, r), Is.Zero, r.Name);
            Assert.That(s.HasRanks, Is.False);
            Assert.That(s.Currency, Is.EqualTo(45));
            Assert.That(s.EarnedInAll, Is.EqualTo(100));
            Assert.That(s.Runs, Is.EqualTo(3));
            Assert.That(s.Owned, Is.EqualTo(new[] { MetaPieces.ThickHide, MetaPieces.SteadyStart }));
            Assert.That(s.Equipped, Is.EqualTo(new[] { MetaPieces.ThickHide }));
            Assert.That(s.Feats, Is.EqualTo(new[] { "clear-floor-1" }));
            Assert.That(s.StruckOut, Is.EqualTo("Fan Fire"));
            Assert.That(s.AttachmentsOwned, Is.EqualTo(new[] { WeaponAttachments.LaserSight }));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.LaserSight }));

            // Written again with no rank bought: version 1, every character as it was read.
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            Assert.That(File.ReadAllText(SavePath), Is.EqualTo(before), "a save of before stays a save of before");
        }

        [Test]
        public void ASaveWithRanks_IsWrittenAsVersion2_AndReadBack_KeepingEverything()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 1000, EarnedInAll = 2000, Runs = 9 };
            s.Owned.Add(MetaPieces.HeavyHand);
            s.Equipped.Add(MetaPieces.HeavyHand);
            for (int k = 0; k < 3; k++) MetaRanks.Buy(s, v, MetaRanks.Health);
            MetaRanks.Buy(s, v, MetaRanks.CritChance);
            Assert.That(s.Currency, Is.EqualTo(1000 - 40 - 55 - 80 - 40));
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            string text = File.ReadAllText(SavePath);
            StringAssert.StartsWith("{\n  \"version\": 2,\n", text, "a rank bought: the version steps to 2");
            StringAssert.EndsWith(",\n  \"ranks\": {\"crit-chance\": 1, \"health\": 3}\n}\n", text, "the ranks last, by id");

            MetaState back = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(back.Version, Is.EqualTo(MetaState.CurrentVersion));
            Assert.That(back.RankOf(MetaRanks.Health), Is.EqualTo(3));
            Assert.That(back.RankOf(MetaRanks.CritChance), Is.EqualTo(1));
            Assert.That(back.RankOf(MetaRanks.Damage), Is.Zero);
            Assert.That(back.Currency, Is.EqualTo(s.Currency));
            Assert.That(back.EarnedInAll, Is.EqualTo(2000));
            Assert.That(back.Equipped, Is.EqualTo(new[] { MetaPieces.HeavyHand }));
            Assert.That(MetaJson.Write(back), Is.EqualTo(text), "read and written again: the same");

            // The copy keeps them, apart.
            MetaState copy = back.Copy();
            copy.Ranks[MetaRanks.Health] = 9;
            Assert.That(back.RankOf(MetaRanks.Health), Is.EqualTo(3), "a copy, not the same table");
            Assert.That(copy.RankOf(MetaRanks.CritChance), Is.EqualTo(1));
        }

        [Test]
        public void ASaveWithRanksTheTableDoesNotKnow_OrOverTheLast_KeepsThem_AndAppliesOnlyWhatTheTableAllows()
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SavePath, "{\"version\":2,\"currency\":5,\"ranks\":{\"health\":14,\"speed\":2,\"damage\":0,\"crit-chance\":-3,\"room-heal\":\"x\"}}");
            var v = new MetaValues();
            MetaState s = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(s.RankOf(MetaRanks.Health), Is.EqualTo(14), "kept as paid for (a retune may lower the last rank)");
            Assert.That(MetaRanks.RankOf(s, MetaRanks.Find(v, MetaRanks.Health)), Is.EqualTo(10), "counted to its last");
            Assert.That(s.RankOf("speed"), Is.EqualTo(2), "a stat the table does not know: kept, not applied");
            Assert.That(s.Ranks.ContainsKey(MetaRanks.Damage) || s.Ranks.ContainsKey(MetaRanks.CritChance), Is.False, "0 and under dropped");
            Assert.That(s.Ranks.ContainsKey(MetaRanks.RoomHeal), Is.False, "not a number: passed over");
            var m = new RunModifiers();
            MetaRanks.Apply(s, v, m.Meta);
            Assert.That(m.Meta.RankHealthShare, Is.EqualTo(1f).Within(1e-5f), "ten ranks of +10 %");
            Assert.That(m.Meta.Ranks, Is.EqualTo(new[] { "Health 10" }));

            File.WriteAllText(SavePath, "{\"version\":2,\"ranks\":[1,2]}");
            MetaSaveFile.Load(SavePath, v, out how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Damaged), "ranks that are not an object: not a save this game can read");
            Assert.That(File.Exists(kept), Is.True, "kept aside, not deleted");
        }

        [Test]
        public void ASaveOfANewerVersionThanThis_IsKeptAside_AsTodaysRuleSays()
        {
            Directory.CreateDirectory(_folder);
            string text = "{ \"version\": 3, \"currency\": 500, \"ranks\": {\"health\": 2} }";
            File.WriteAllText(SavePath, text);
            MetaState s = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out string kept);
            Assert.That(how, Is.EqualTo(MetaLoad.Newer));
            Assert.That(s.Currency, Is.Zero, "an empty save");
            Assert.That(s.HasRanks, Is.False);
            Assert.That(kept, Does.StartWith(SavePath + ".newer-"));
            Assert.That(File.ReadAllText(kept), Is.EqualTo(text), "the newer one kept as it was");
        }

        // ---------------- On her stats ----------------

        static MetaState WithRanks(MetaValues v, int health, int damage, int crit, int heal)
        {
            var s = new MetaState { Currency = 100000 };
            for (int k = 0; k < health; k++) MetaRanks.Buy(s, v, MetaRanks.Health);
            for (int k = 0; k < damage; k++) MetaRanks.Buy(s, v, MetaRanks.Damage);
            for (int k = 0; k < crit; k++) MetaRanks.Buy(s, v, MetaRanks.CritChance);
            for (int k = 0; k < heal; k++) MetaRanks.Buy(s, v, MetaRanks.RoomHeal);
            s.Feats.AddRange(MetaFeats.All.Select(MetaFeats.Id));
            return s;
        }

        static RunModifiers Run(MetaState s, MetaValues v)
        {
            var m = new RunModifiers { StatsAndTiers = true };
            MetaRack.Apply(s, v, m.Meta);
            MetaRanks.Apply(s, v, m.Meta);
            return m;
        }

        [Test]
        public void TheRanksAlone_RaiseHealthDamageCritAndRoomHeal_ByTheirSteps()
        {
            var v = new MetaValues();
            var run = new RunValues();
            RunModifiers m = Run(WithRanks(v, 3, 2, 2, 2), v);
            Assert.That(m.Meta.Any, Is.False, "no piece of the rack");
            Assert.That(m.Meta.Ranks, Is.EqualTo(new[] { "Health 3", "Damage 2", "Crit chance 2", "Room heal 2" }));
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(130), "+30 % of her own");
            Assert.That(m.MaxHealthFor(85), Is.EqualTo(111), "85 x 1.3 = 110.5, half away from zero");
            Assert.That(m.DamageShare, Is.EqualTo(1.12f).Within(1e-5f), "+12 %");
            Assert.That(m.ScaleDamage(50), Is.EqualTo(56));
            Assert.That(m.Stats.CritChance, Is.EqualTo(0.05f + 0.06f).Within(1e-5f), "5 % at the start and 6 points");
            Assert.That(m.Stats.RoomHealShare(run, false), Is.EqualTo(0.12f).Within(1e-5f), "8 % and 4 points");
            Assert.That(m.Stats.RoomHealShare(run, true), Is.EqualTo(0.24f).Within(1e-5f), "the elite room's 20 % and 4 points");
            Assert.That(new RoomHeals().Heal(1, false, 130, run, m.Meta.RankRoomHeal), Is.EqualTo(16), "12 % of 130 = 15.6");
            Assert.That(new RoomHeals().Heal(1, false, 130, run), Is.EqualTo(10), "without them, 8 % of 130 = 10.4, as before");
        }

        [Test]
        public void TheRackAlone_IsAsBefore_AndTheRanksAddToIt_WithTheChestsOnTop()
        {
            var v = new MetaValues();
            MetaState rackOnly = WithRanks(v, 0, 0, 0, 0);
            foreach (string id in new[] { MetaPieces.ThickHide, MetaPieces.HeavyHand })
            {
                MetaRack.Buy(rackOnly, v, id);
                Assert.That(MetaRack.Equip(rackOnly, v, id), Is.EqualTo(MetaEquip.Equipped), id);
            }
            RunModifiers rack = Run(rackOnly, v);
            Assert.That(rack.MaxHealthFor(100), Is.EqualTo(120), "Thick Hide's +20 % as before");
            Assert.That(rack.DamageShare, Is.EqualTo(1.15f).Within(1e-6f), "Heavy Hand's +15 % as before");
            Assert.That(rack.Stats.CritChance, Is.EqualTo(0.05f).Within(1e-6f));
            Assert.That(rack.Meta.AnyRanks, Is.False);

            MetaState both = rackOnly.Copy();
            for (int k = 0; k < 3; k++) MetaRanks.Buy(both, v, MetaRanks.Health);
            for (int k = 0; k < 2; k++) MetaRanks.Buy(both, v, MetaRanks.Damage);
            for (int k = 0; k < 2; k++) MetaRanks.Buy(both, v, MetaRanks.CritChance);
            RunModifiers m = Run(both, v);
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(150), "the shares added: 20 % and 30 %");
            Assert.That(m.DamageShare, Is.EqualTo(1.27f).Within(1e-5f), "15 % and 12 % added");
            Assert.That(m.Stats.CritChance, Is.EqualTo(0.11f).Within(1e-5f));

            // The chests' steps on top, by the rules they had.
            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10f, Heal = 10 });
            m.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.15f });
            m.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.04f });
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(160), "the chests' health after the shares");
            Assert.That(m.DamageShare, Is.EqualTo(1.15f * 1.27f).Within(1e-5f), "the chests' share times the rack's and the ranks'");
            Assert.That(m.Stats.CritChance, Is.EqualTo(0.15f).Within(1e-5f), "points added");

            // With the stats and tiers off there is no crit at all, ranks or not; health and damage count as the rack's do.
            m.StatsAndTiers = false;
            Assert.That(m.Stats.CritChance, Is.Zero);
            Assert.That(m.MaxHealthFor(100), Is.EqualTo(160));
        }

        [Test]
        public void CritChance_NeverOver100Percent_AndAChestNeverOffersAStepThatWouldDoNothing()
        {
            var v = new MetaValues();
            RunModifiers m = Run(WithRanks(v, 0, 0, 8, 0), v);
            Assert.That(m.Stats.CritChance, Is.EqualTo(0.29f).Within(1e-5f), "5 % and 24 points");
            m.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.7f });
            Assert.That(m.Stats.CritChance, Is.EqualTo(0.99f).Within(1e-5f));
            m.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.04f });
            Assert.That(m.Stats.CritChance, Is.EqualTo(1f), "to 100 % and no more");
            var row = new UpgradeDefinition(UpgradeKind.CritChance, 0.04f);
            Assert.That(TieredOffers.Useful(row, null, new RunValues(), new StatValues(), m, false), Is.False, "at 100 % no crit step is offered");
        }

        // ---------------- The switch off ----------------

        /// <summary>Every public field and property of the effects, as words: what "field for field" compares.</summary>
        static string Fields(RunMetaEffects e)
        {
            var parts = new List<string>();
            foreach (FieldInfo f in typeof(RunMetaEffects).GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                object x = f.GetValue(e);
                parts.Add(f.Name + "=" + (x is System.Collections.IEnumerable list && !(x is string) ? string.Join("|", list.Cast<object>()) : Convert.ToString(x, System.Globalization.CultureInfo.InvariantCulture)));
            }
            return string.Join(", ", parts);
        }

        [Test]
        public void NothingApplied_IsTheTrialOff_FieldForField_WhateverTheSaveHolds()
        {
            var v = new MetaValues();
            MetaState s = WithRanks(v, 10, 10, 8, 5);
            Assert.That(MetaRanks.WholePrice(v), Is.EqualTo(100000 - s.Currency), "everything bought");

            // A run started with the trial off never reaches the save: its effects are a fresh one's, every field.
            var off = new RunModifiers();
            Assert.That(Fields(off.Meta), Is.EqualTo(Fields(new RunMetaEffects())));

            // Ranks applied, then a run's end clears them: a fresh one's again, every field, and every number as before.
            RunModifiers m = Run(s, v);
            Assert.That(Fields(m.Meta), Is.Not.EqualTo(Fields(new RunMetaEffects())), "applied, they show");
            m.Clear();
            Assert.That(Fields(m.Meta), Is.EqualTo(Fields(new RunMetaEffects())), "cleared, field for field");
            Assert.That(m.Meta.ToString(), Is.EqualTo(new RunMetaEffects().ToString()));
            var plain = new RunModifiers { StatsAndTiers = true };
            m.StatsAndTiers = true;
            var run = new RunValues();
            for (int own = 1; own <= 300; own += 7) Assert.That(m.MaxHealthFor(own), Is.EqualTo(plain.MaxHealthFor(own)));
            Assert.That(m.DamageShare, Is.EqualTo(plain.DamageShare));
            Assert.That(m.Stats.CritChance, Is.EqualTo(plain.Stats.CritChance));
            Assert.That(m.Stats.RoomHealShare(run, false), Is.EqualTo(run.RoomHeal));
            Assert.That(m.Stats.RoomHealShare(run, true), Is.EqualTo(run.EliteRoomHeal));

            // And an empty table applies nothing whatever the save holds.
            var empty = new MetaValues { Ranks = new MetaRank[0] };
            var none = new RunModifiers();
            MetaRanks.Apply(s, empty, none.Meta);
            Assert.That(Fields(none.Meta), Is.EqualTo(Fields(new RunMetaEffects())));
        }

        // ---------------- In words ----------------

        [Test]
        public void TheWords_RankOutOfItsMost_WhatTheNextGives_ThePrice_AndWhetherSheCanPay()
        {
            var v = new MetaValues();
            MetaRank health = MetaRanks.Find(v, MetaRanks.Health), heal = MetaRanks.Find(v, MetaRanks.RoomHeal);
            var s = new MetaState { Currency = 100 };
            Assert.That(MetaRanks.RankWords(s, health), Is.EqualTo("rank 0 of 10"));
            Assert.That(MetaRanks.StateWords(s, v, health), Is.EqualTo("price 40 Echoes"));
            Assert.That(MetaRanks.NextWords(s, health), Is.EqualTo("Next rank: +10 % of her own health."));
            MetaRanks.Buy(s, v, MetaRanks.Health);
            Assert.That(MetaRanks.StateWords(s, v, health), Is.EqualTo("price 55 Echoes"), "60 left");
            MetaRanks.Buy(s, v, MetaRanks.Health);
            Assert.That(MetaRanks.RankWords(s, health), Is.EqualTo("rank 2 of 10"));
            Assert.That(MetaRanks.StateWords(s, v, health), Is.EqualTo("80 Echoes, 75 more"), "5 left");
            Assert.That(MetaRanks.NextWords(s, health), Is.EqualTo("Next rank: +10 % of her own health.  Now +20 %."));
            s.Currency = 10000;
            for (int k = 0; k < 5; k++) MetaRanks.Buy(s, v, MetaRanks.RoomHeal);
            Assert.That(MetaRanks.StateWords(s, v, heal), Is.EqualTo("last rank"));
            Assert.That(MetaRanks.NextWords(s, heal), Is.EqualTo("Every rank bought: +10 points room heal."));
            Assert.That(MetaRanks.Gives(MetaRanks.Find(v, MetaRanks.Damage), 3), Is.EqualTo("+18 % damage"));
            Assert.That(MetaRanks.Gives(MetaRanks.Find(v, MetaRanks.CritChance), 2), Is.EqualTo("+6 points crit chance"));
            Assert.That(MetaRanks.Held(s, v), Is.EqualTo("Health 2, Room heal 5"));
            Assert.That(MetaRanks.Held(new MetaState(), v), Is.Null);
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_ListsTheRanksBought_AndARunsRanks_OnlyWhenThereWereAny()
        {
            SessionEvent E(double t, string type, string name, params string[] extras)
            {
                var e = new SessionEvent(t, type, name);
                for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
                return e;
            }
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.MetaRank, "Health", "rank", "1", "price", "40", "left", "60"),
                E(1, SessionEventTypes.MetaRank, "Damage", "rank", "1", "price", "40", "left", "20"),
                E(2, SessionEventTypes.RunStart, "AAAAAAAA"),
                E(3, SessionEventTypes.FloorEnter, "s1", "floor", "1"),
                E(3, SessionEventTypes.MetaLoadout, "", "currency", "20", "budget", "3", "used", "0", "ranks", "Health 1, Damage 1"),
                E(9, SessionEventTypes.RunEnd, null),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.RanksBought, Is.EqualTo(new[] { "Health rank 1 for 40 (60 left)", "Damage rank 1 for 40 (20 left)" }));
            Assert.That(stats.Runs[0].MetaRanks, Is.EqualTo("Health 1, Damage 1"));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Ranks bought between runs   Health rank 1 for 40 (60 left);  Damage rank 1 for 40 (20 left)", text);
            StringAssert.Contains("between runs     equipped nothing;  ranks Health 1, Damage 1;  earned 0", text);

            // None bought, none held: not a word of them.
            var plain = new List<SessionEvent>
            {
                E(0, SessionEventTypes.RunStart, "BBBBBBBB"),
                E(1, SessionEventTypes.MetaLoadout, "Steady Start", "currency", "40", "budget", "4", "used", "1"),
                E(9, SessionEventTypes.RunEnd, null),
            };
            string before = SessionSummary.Format(SessionSummary.Compute(plain));
            Assert.That(before.Split('\n').Any(l => l.Contains("Ranks bought") || l.Contains(";  ranks ")), Is.False);
            StringAssert.Contains("between runs     equipped Steady Start;  earned 0", before);
        }

        // ---------------- The runs it takes (the report's table) ----------------

        /// <summary>
        /// What a run earns (MetaValues' PerFloor, PerElite, PerBoss, and PerRoom with the attachments on) with the floors as
        /// they are made today (LevelPlan: five combat rooms a floor in a line, the elite room among them, five floors an act),
        /// and how many runs of a kind the ranks take: their first rank each, half of each stat's ranks, half the Echoes of the
        /// whole store, and all of it; beside the rack's whole price. Pinned here as the report's table says them.
        /// </summary>
        [Test]
        public void TheRunsItTakes_TheReportsTable()
        {
            var v = new MetaValues();
            var plan = new LevelPlan();
            int ordinary = plan.LineCombatRooms - 1; // the elite room is one of them
            Assert.That(ordinary, Is.EqualTo(4));
            int floor3 = 2 * v.PerFloor + 2 * v.PerElite;                        // died on floor 3, its elite room not cleared
            int floor3WithItsElite = floor3 + v.PerElite;                         // ... after it
            int act = 5 * v.PerFloor + 5 * v.PerElite + v.PerBoss;               // the act cleared, every elite room
            int floor3Rooms = floor3 + 2 * ordinary * v.PerRoom;                  // with the attachments on (their rooms counted)
            int actRooms = act + 5 * ordinary * v.PerRoom;
            Assert.That(new[] { floor3, floor3WithItsElite, act, floor3Rooms, actRooms }, Is.EqualTo(new[] { 50, 65, 225, 74, 285 }));

            int first = v.Ranks.Sum(r => MetaRanks.Price(v, 1));
            int threeOfOne = Enumerable.Range(1, 3).Sum(k => MetaRanks.Price(v, k));
            int threeOfEach = 4 * threeOfOne;
            int halfRanks = v.Ranks.Sum(r => Enumerable.Range(1, (r.Most + 1) / 2).Sum(k => MetaRanks.Price(v, k)));
            int whole = MetaRanks.WholePrice(v);
            int rack = MetaRack.WholePrice(v);
            Assert.That(new[] { first, threeOfOne, threeOfEach, halfRanks, whole / 2, whole, rack }, Is.EqualTo(new[] { 160, 175, 700, 1340, 3697, 7395, 810 }));

            int Runs(int need, int per) => (need + per - 1) / per;
            int[] Row(int need) => new[] { Runs(need, floor3), Runs(need, act), Runs(need, floor3Rooms), Runs(need, actRooms) };
            Assert.That(Row(first), Is.EqualTo(new[] { 4, 1, 3, 1 }), "the first rank of each");
            Assert.That(Row(threeOfOne), Is.EqualTo(new[] { 4, 1, 3, 1 }), "one stat's first three");
            Assert.That(Row(threeOfEach), Is.EqualTo(new[] { 14, 4, 10, 3 }), "every stat's first three");
            Assert.That(Row(halfRanks), Is.EqualTo(new[] { 27, 6, 19, 5 }), "half of each stat's ranks (5, 5, 4, 3)");
            Assert.That(Row(whole / 2), Is.EqualTo(new[] { 74, 17, 50, 13 }), "half the Echoes of the whole store");
            Assert.That(Row(whole), Is.EqualTo(new[] { 148, 33, 100, 26 }), "everything");
            Assert.That(Row(rack), Is.EqualTo(new[] { 17, 4, 11, 3 }), "the rack's whole price, beside");
            Assert.That(MetaRanks.Price(v, 1), Is.LessThanOrEqualTo(floor3), "a first rank from a run that dies on floor 3");
            Assert.That(MetaRanks.Price(v, 1), Is.LessThanOrEqualTo(v.PerFloor + 2 * v.PerElite), "and from one that dies on floor 2 after its elite room");
            Assert.That(whole, Is.GreaterThan(5 * rack), "a true sink: far longer than the rack");
        }

        // ---------------- The screen's place ----------------

        [Test]
        public void TheScreensPanels_TheRanksToTheRight_NoneOverlapping_InsideTheView_TheRackTheLargest()
        {
            foreach (float viewWidth in new[] { 1280f, 960f, 1706.7f })
            foreach (bool weapon in new[] { false, true })
            {
                string at = viewWidth + (weapon ? " with the weapon" : "");
                BetweenRunsLayout.Panels(viewWidth, weapon, true, out LayoutRect rack, out LayoutRect gun, out LayoutRect ranks);
                Assert.That(ranks.Width, Is.GreaterThan(0f), at);
                Assert.That(rack.X, Is.GreaterThanOrEqualTo(BetweenRunsLayout.Margin - 1e-3f), at);
                Assert.That(ranks.Right, Is.LessThanOrEqualTo(viewWidth - BetweenRunsLayout.Margin + 1e-3f), at + ": inside the view");
                Assert.That(ranks.X, Is.GreaterThanOrEqualTo((weapon ? gun.Right : rack.Right) + BetweenRunsLayout.Gap - 1e-3f), at + ": to the right, apart");
                if (weapon) Assert.That(gun.X, Is.GreaterThanOrEqualTo(rack.Right + BetweenRunsLayout.Gap - 1e-3f), at);
                Assert.That(rack.Width, Is.GreaterThan(ranks.Width), at + ": the rack the larger part");
                Assert.That(rack.Width, Is.GreaterThanOrEqualTo(BetweenRunsLayout.LeastRack), at);
            }
            BetweenRunsLayout.Panels(1280f, false, true, out LayoutRect r1, out _, out LayoutRect k1);
            Assert.That((r1.Width, k1.Width), Is.EqualTo((680f, 360f)), "16:9 without the weapon: both at their widths");
            BetweenRunsLayout.Panels(1280f, true, true, out LayoutRect r2, out LayoutRect g2, out LayoutRect k2);
            Assert.That((r2.Width, g2.Width, k2.Width), Is.EqualTo((520f, 404f, 300f)), "with the weapon: the rack to its least, the ranks to theirs, the weapon the rest");

            // No rank: 060's rule exactly.
            foreach (bool weapon in new[] { false, true })
            {
                BetweenRunsLayout.Panels(1280f, weapon, false, out LayoutRect a, out LayoutRect b, out LayoutRect none);
                BetweenRunsLayout.Panels(1280f, weapon, out LayoutRect a0, out LayoutRect b0);
                Assert.That((a.X, a.Width, b.X, b.Width, none.Width), Is.EqualTo((a0.X, a0.Width, b0.X, b0.Width, 0f)));
            }
        }
    }
}
