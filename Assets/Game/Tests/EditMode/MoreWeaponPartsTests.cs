using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 110 (Kristian, 2026-11-24: yes to "Tre valg per plass, hver med en buff og en kostnad. Det som synes av dem er
    /// lite og stille"): five more parts for her pistols, so each slot holds three, as rules. The table (on by default, prices in
    /// the four's range); each new part's knobs on the basic shot's numbers alone and with one part in each other slot (every
    /// loadout of the nine); each a buff and a cost against the stock gun; one part a slot; buying, fitting, saving and reading
    /// back; her sheet's basic shot; and the switch off (<see cref="MetaValues.MoreParts"/>): the store, a run's effects and the
    /// end screen's words are 060's four, field for field, and a save that owns one of the five keeps it. The save is only ever
    /// written under a temporary folder of this test.
    /// </summary>
    public class MoreWeaponPartsTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "MoreWeaponPartsTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string SavePath => Path.Combine(_folder, "between-runs.json");

        static readonly FeelValues Feel = new FeelValues();

        static readonly string[] Five =
        {
            WeaponAttachments.ReflexSight, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
            WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
        };

        static readonly string[] Four = { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip };

        static WeaponAttachment Part(string id) => WeaponRack.Find(new MetaValues(), id);

        static AttachmentEffects With(params string[] ids)
        {
            var e = new AttachmentEffects();
            foreach (string id in ids) e.Add(Part(id));
            return e;
        }

        /// <summary>The values as they were before this order: the table's first four rows only (060's table).</summary>
        static MetaValues BeforeValues()
        {
            var v = new MetaValues();
            v.Parts = WeaponAttachments.Create().Where(p => !p.MorePart).ToArray();
            return v;
        }

        // ---------------- The table ----------------

        [Test]
        public void TheSwitch_IsOn_AndTheStoreHoldsNine_ThreeASlot_TheFourFirstAsTheyWere()
        {
            var v = new MetaValues();
            Assert.That(v.MoreParts, Is.True, "on by default");
            Assert.That(v.Parts.Length, Is.EqualTo(9));
            Assert.That(v.Parts.Take(4).Select(p => p.Id), Is.EqualTo(Four), "060's four first, in their order");
            Assert.That(v.Parts.Skip(4).Select(p => p.Id), Is.EqualTo(Five), "the five after them, at the table's end");
            Assert.That(v.Parts.Select(p => p.MorePart), Is.EqualTo(new[] { false, false, false, false, true, true, true, true, true }));
            Assert.That(WeaponRack.InSlotOrder(v).Select(p => p.Name), Is.EqualTo(new[]
            {
                "Laser Sight", "Ghost Ring", "Reflex Sight",
                "Bull Barrel", "Long Barrel", "Compensator",
                "Runner's Grip", "Weighted Grip", "Quick-Draw Grip",
            }));
            foreach (WeaponSlot slot in WeaponAttachments.SlotsOf(WeaponKind.Pistols))
                Assert.That(v.Parts.Count(p => p.Slot == slot && p.Weapon == WeaponKind.Pistols), Is.EqualTo(3), slot + ": three to choose between");
            Assert.That(v.Parts.Skip(4).Select(p => p.Slot), Is.EqualTo(new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Barrel, WeaponSlot.Grip, WeaponSlot.Grip }));
            Assert.That(v.Parts.Skip(4).Select(p => p.Price), Is.EqualTo(new[] { 35, 40, 45, 45, 55 }));
            Assert.That(v.Parts.All(p => p.Price >= 30 && p.Price <= 60), "every price in the four's range, 30 to 60");
            Assert.That(WeaponRack.WholePrice(v), Is.EqualTo(370), "the whole set: 150 and 220");
            Assert.That(v.Parts.Select(p => p.Id).Distinct().Count(), Is.EqualTo(9), "one id each");
            Assert.That(v.Parts.Skip(4).Select(p => p.Line), Is.EqualTo(new[]
            {
                "+10 % crit chance on basic shots.", "Reach 13 m.", "20 % less time between shots.", "Each shot staggers 3.",
                "The first shot after a dash or a skill goes off at once.",
            }));
            Assert.That(v.Parts.Skip(4).Select(p => p.CostLine), Is.EqualTo(new[]
            {
                "Reach 8.5 m.", "20 % slower between shots.", "Each shot deals 20 % less.", "Shooting on the move slows you to 35 %, not 50 %.",
                "Each shot deals 10 % less.",
            }));
            Assert.That(v.Parts.Skip(4).All(p => !p.ShowsLine), "none of the five draws anything");
        }

        [Test]
        public void TheFour_AreRowForRowAsBefore()
        {
            WeaponAttachment[] now = new MetaValues().Parts.Take(4).ToArray(), before = BeforeValues().Parts;
            for (int i = 0; i < 4; i++)
            {
                WeaponAttachment a = now[i], b = before[i];
                Assert.That(new object[] { a.Id, a.Name, a.Weapon, a.Slot, a.Price, a.Line, a.CostLine, a.ReachAdd, a.HalfWidthScale, a.DamageScale, a.IntervalScale, a.Stagger, a.MovingSpeedShare, a.MovingDamageScale, a.ShowsLine, a.CritChanceAdd, a.FirstShotStartupScale, a.MorePart },
                            Is.EqualTo(new object[] { b.Id, b.Name, b.Weapon, b.Slot, b.Price, b.Line, b.CostLine, b.ReachAdd, b.HalfWidthScale, b.DamageScale, b.IntervalScale, b.Stagger, b.MovingSpeedShare, b.MovingDamageScale, b.ShowsLine, 0f, 1f, false }), a.Name);
            }
        }

        // ---------------- Each part alone, on the shot's numbers ----------------

        [Test]
        public void ReflexSight_Crit5To15_Reach85_NothingElse()
        {
            AttachmentEffects e = With(WeaponAttachments.ReflexSight);
            Assert.That(e.ShotCritChance(0.05f), Is.EqualTo(0.15f).Within(1e-6f), "her start's 5 % to 15 %");
            Assert.That(e.ShotCritChance(0.95f), Is.EqualTo(1f), "never over 1");
            Assert.That(e.ShotCritChance(0f), Is.Zero, "with her stats off nothing crits");
            Assert.That(e.Reach(Feel.BasicShotRange), Is.EqualTo(8.5f));
            Assert.That(e.HalfWidth(Feel.BasicShotHalfWidth), Is.EqualTo(0.5f));
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(40));
            Assert.That(e.Interval(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.225f).Within(1e-6f));
            Assert.That(e.ShotStagger, Is.Zero);
            Assert.That(e.MovingShare(0.5f), Is.EqualTo(0.5f));
            Assert.That(e.ShotStartup(0.08f, true), Is.EqualTo(0.08f));
            Assert.That(e.ShowsLine, Is.False);
            Assert.That(e.ToString(), Does.EndWith(", crit +0.1"));
        }

        [Test]
        public void LongBarrel_Reach13_ATwentiethSlowerBetweenShots_TheStartupKept()
        {
            AttachmentEffects e = With(WeaponAttachments.LongBarrel);
            Assert.That(e.Reach(Feel.BasicShotRange), Is.EqualTo(13f));
            Assert.That(e.Interval(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.27f).Within(1e-5f), "0.225 to 0.27 s");
            Assert.That(e.Recovery(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.17f).Within(1e-5f), "only the recovery grows: 0.125 to 0.17 s");
            Assert.That(e.ShotStartup(Feel.BasicShotStartup, true), Is.EqualTo(Feel.BasicShotStartup));
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40));
            Assert.That(e.HalfWidth(0.5f), Is.EqualTo(0.5f));
            Assert.That(e.ShotCritChance(0.05f), Is.EqualTo(0.05f));
        }

        [Test]
        public void Compensator_AFifthLessBetweenShots_32AShot()
        {
            AttachmentEffects e = With(WeaponAttachments.Compensator);
            Assert.That(e.Interval(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.18f).Within(1e-5f), "0.225 to 0.18 s");
            Assert.That(e.Recovery(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.08f).Within(1e-5f), "the startup kept, the recovery 0.125 to 0.08 s");
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(32));
            Assert.That(e.ShotDamage(46, false), Is.EqualTo(37), "a chest's +6 through it too: 36.8, rounded");
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(32), "the same on the move");
            Assert.That(e.Reach(10f), Is.EqualTo(10f));
            Assert.That(e.ShotStagger, Is.Zero);
        }

        [Test]
        public void WeightedGrip_Stagger3_OnTheMoveAndBackpedalling_At035()
        {
            AttachmentEffects e = With(WeaponAttachments.WeightedGrip);
            Assert.That(e.ShotStagger, Is.EqualTo(3f));
            Assert.That(e.MovingShare(Feel.MovingShotSpeedShare), Is.EqualTo(0.35f), "3 to 2.1 m/s at her 6 m/s");
            Assert.That(e.BackpedalShare(Feel.BackpedalSpeedShare), Is.EqualTo(0.35f));
            Assert.That(Feel.RunSpeed * e.MovingShare(Feel.MovingShotSpeedShare), Is.EqualTo(2.1f).Within(1e-5f));
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(40));
            Assert.That(e.Interval(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.225f).Within(1e-6f));
        }

        [Test]
        public void QuickDrawGrip_TheFirstShotAfterADashOrASkill_In002_36AShot()
        {
            AttachmentEffects e = With(WeaponAttachments.QuickDrawGrip);
            Assert.That(e.ShotStartup(Feel.BasicShotStartup, true), Is.EqualTo(0.02f).Within(1e-6f), "0.08 to 0.02 s, the first after a dash or a skill");
            Assert.That(e.ShotStartup(Feel.BasicShotStartup, false), Is.EqualTo(Feel.BasicShotStartup), "every other shot the feel's");
            Assert.That(e.Interval(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(0.225f).Within(1e-6f), "the rhythm kept");
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(36));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(36));
            Assert.That(e.MovingShare(0.5f), Is.EqualTo(0.5f));
            Assert.That(e.Reach(10f), Is.EqualTo(10f));
            Assert.That(e.ToString(), Does.EndWith(", first shot startup x0.25"));
        }

        [Test]
        public void TheEmptyEffects_AreTheStockGun_ForTheTwoNewKnobs_AndTheFoursLineIsAsBefore()
        {
            var e = new AttachmentEffects();
            foreach (float c in new[] { 0f, 0.05f, 0.5f, 1f }) Assert.That(e.ShotCritChance(c), Is.EqualTo(c));
            foreach (float s in new[] { 0f, 0.04f, 0.08f, 0.3f })
            {
                Assert.That(e.ShotStartup(s, true), Is.EqualTo(s));
                Assert.That(e.ShotStartup(s, false), Is.EqualTo(s));
            }
            Assert.That(e.ToString(), Is.EqualTo("reach +0 m, width x1, damage x1, interval x1, stagger +0, moving 0, moving damage x1, line off"), "060's words");
            foreach (string id in Four) Assert.That(With(id).ToString(), Does.Not.Contain("crit").And.Not.Contain("first shot"), id + ": its line as before");
            e.Add(Part(WeaponAttachments.QuickDrawGrip));
            e.Add(Part(WeaponAttachments.ReflexSight));
            e.Clear();
            Assert.That(e.ShotCritChance(0.05f), Is.EqualTo(0.05f));
            Assert.That(e.ShotStartup(0.08f, true), Is.EqualTo(0.08f));
        }

        // ---------------- With one part in each other slot: every loadout of the nine ----------------

        [Test]
        public void EveryLoadout_OneASlot_TheKnobsCombineAsTheirRulesSay()
        {
            var v = new MetaValues();
            List<WeaponAttachment> sights = v.Parts.Where(p => p.Slot == WeaponSlot.Sight).ToList();
            List<WeaponAttachment> barrels = v.Parts.Where(p => p.Slot == WeaponSlot.Barrel).ToList();
            List<WeaponAttachment> grips = v.Parts.Where(p => p.Slot == WeaponSlot.Grip).ToList();
            sights.Insert(0, null);
            barrels.Insert(0, null);
            grips.Insert(0, null);
            int loadouts = 0;
            foreach (WeaponAttachment a in sights)
            foreach (WeaponAttachment b in barrels)
            foreach (WeaponAttachment c in grips)
            {
                var parts = new[] { a, b, c }.Where(p => p != null).ToArray();
                var s = new MetaState { Currency = 1000 };
                foreach (WeaponAttachment p in parts) Assert.That(WeaponRack.Buy(s, v, p.Id), Is.EqualTo(MetaBuy.Bought));
                var e = new AttachmentEffects();
                WeaponRack.Apply(s, v, e);
                string what = parts.Length == 0 ? "stock" : string.Join(" + ", parts.Select(p => p.Name));
                Assert.That(e.Parts, Is.EquivalentTo(parts.Select(p => p.Name)), what);

                float reach = 10f + parts.Sum(p => p.ReachAdd);
                Assert.That(e.Reach(10f), Is.EqualTo(Math.Max(AttachmentEffects.LeastReach, reach)).Within(1e-5f), what + ": reach");
                float width = parts.Aggregate(1f, (k, p) => k * p.HalfWidthScale);
                Assert.That(e.HalfWidth(0.5f), Is.EqualTo(0.5f * width).Within(1e-5f), what + ": width");
                float damage = parts.Aggregate(1f, (k, p) => k * p.DamageScale);
                float moving = parts.Aggregate(damage, (k, p) => k * p.MovingDamageScale);
                Assert.That(e.ShotDamage(40, false), Is.EqualTo((int)Math.Round(40 * (double)damage, MidpointRounding.AwayFromZero)), what + ": damage");
                Assert.That(e.ShotDamage(40, true), Is.EqualTo((int)Math.Round(40 * (double)moving, MidpointRounding.AwayFromZero)), what + ": damage on the move");
                float interval = parts.Aggregate(1f, (k, p) => k * p.IntervalScale);
                Assert.That(e.Interval(0.08f, 0.02f, 0.125f), Is.EqualTo(0.225f * interval).Within(1e-5f), what + ": interval");
                Assert.That(e.ShotStagger, Is.EqualTo(parts.Sum(p => p.Stagger)), what + ": stagger");
                float share = c != null && c.MovingSpeedShare > 0f ? c.MovingSpeedShare : 0.5f;
                Assert.That(e.MovingShare(0.5f), Is.EqualTo(share), what + ": on the move, the grip's");
                Assert.That(e.BackpedalShare(0.5f), Is.EqualTo(share), what + ": backpedalling, the grip's");
                Assert.That(e.ShotCritChance(0.05f), Is.EqualTo(0.05f + parts.Sum(p => p.CritChanceAdd)).Within(1e-6f), what + ": crit");
                float startup = parts.Aggregate(1f, (k, p) => k * p.FirstShotStartupScale);
                Assert.That(e.ShotStartup(0.08f, true), Is.EqualTo(0.08f * startup).Within(1e-6f), what + ": the first shot after a dash");
                Assert.That(e.ShotStartup(0.08f, false), Is.EqualTo(0.08f), what + ": any other shot");
                Assert.That(e.ShowsLine, Is.EqualTo(a != null && a.Id == WeaponAttachments.LaserSight), what + ": only the Laser Sight shows its beams");
                loadouts++;
            }
            Assert.That(loadouts, Is.EqualTo(64), "four choices a slot (none or one of three), three slots");
        }

        [Test]
        public void TheShortestReach_IsGhostRingWithQuickDrawOrReflex_StillOver4()
        {
            // The sights that shorten and nothing else does: no loadout reaches the least reach.
            Assert.That(With(WeaponAttachments.GhostRing).Reach(10f), Is.EqualTo(8f));
            Assert.That(With(WeaponAttachments.ReflexSight, WeaponAttachments.Compensator, WeaponAttachments.QuickDrawGrip).Reach(10f), Is.EqualTo(8.5f));
            Assert.That(With(WeaponAttachments.LaserSight, WeaponAttachments.LongBarrel).Reach(10f), Is.EqualTo(15f), "the longest: 15 m, before a chest's Longer barrel");
        }

        // ---------------- A buff and a cost each ----------------

        /// <summary>A basic shot's numbers that a part can turn, each with which way is better for her (true: more is better).</summary>
        static IEnumerable<(string what, double value, bool more)> Numbers(AttachmentEffects e)
        {
            yield return ("reach", e.Reach(10f), true);
            yield return ("width", e.HalfWidth(0.5f), true);
            yield return ("damage", e.ShotDamage(40, false), true);
            yield return ("damage on the move", e.ShotDamage(40, true), true);
            yield return ("interval", e.Interval(0.08f, 0.02f, 0.125f), false);
            yield return ("stagger", e.ShotStagger, true);
            yield return ("speed on the move", e.MovingShare(0.5f), true);
            yield return ("crit", e.ShotCritChance(0.05f), true);
            yield return ("first shot after a dash", e.ShotStartup(0.08f, true), false);
        }

        [Test]
        public void EachNewPart_IsABuffAndACost_NeitherAFewPerCent()
        {
            var stock = Numbers(new AttachmentEffects()).ToList();
            foreach (string id in Five)
            {
                var mine = Numbers(With(id)).ToList();
                var better = new List<string>();
                var worse = new List<string>();
                for (int i = 0; i < stock.Count; i++)
                {
                    double s = stock[i].value, m = mine[i].value;
                    if (Math.Abs(m - s) < 1e-6) continue;
                    double change = s != 0 ? Math.Abs(m - s) / Math.Abs(s) : 1.0;
                    Assert.That(change, Is.GreaterThanOrEqualTo(0.1), id + ": " + stock[i].what + " changes by at least a tenth, not a few per cent");
                    ((m > s) == stock[i].more ? better : worse).Add(stock[i].what);
                }
                TestContext.WriteLine(id + ": better " + string.Join(", ", better) + "; worse " + string.Join(", ", worse));
                Assert.That(better, Is.Not.Empty, id + ": a buff");
                Assert.That(worse, Is.Not.Empty, id + ": a cost");
            }
        }

        /// <summary>A basic shot's damage per second at a standing target (or on the move), as WeaponAttachmentsTests reckons it, the sight's crit with it.</summary>
        static double Dps(RunModifiers m, bool onTheMove)
        {
            HerStats stats = m.Stats;
            int hit = m.Weapon.ShotDamage(Placeholders.BasicShotDamage + stats.FlatFor(null), onTheMove);
            double expected = 1.0 + m.Weapon.ShotCritChance(stats.CritChance) * stats.CritDamage;
            double interval = stats.BasicShotInterval(Feel.BasicShotStartup, Feel.BasicShotActive, m.Weapon.Recovery(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery));
            return hit * m.DamageShare * expected / interval;
        }

        static RunModifiers Modifiers(bool stats, string part)
        {
            var values = new RunValues();
            var m = new RunModifiers { StatsAndTiers = stats, StatValues = values.Stats, CooldownFloor = values.CooldownFloor };
            if (part != null) m.Weapon.Add(Part(part));
            return m;
        }

        // The model's damage per second of each new part against the stock gun, pinned (the producer's numbers, first guesses).
        // Unlike 060's four, two of the five cost damage per second as their price (Long Barrel its rate, Quick-Draw Grip a tenth
        // a shot), and the Reflex Sight's crits give a little (+4.9 % at her start): decision 0014's "within 5 %" held for the four.
        [TestCase(false, 1.0, 0.8333, 1.0, 1.0, 0.9, TestName = "TheFiveParts_DamagePerSecond_AgainstStock_StatsOff")]
        [TestCase(true, 1.0488, 0.8333, 1.0, 1.0, 0.9, TestName = "TheFiveParts_DamagePerSecond_AgainstStock_StatsOnAtHerStart")]
        public void TheFiveParts_DamagePerSecond(bool stats, double reflex, double longBarrel, double compensator, double weighted, double quickDraw)
        {
            double stock = Dps(Modifiers(stats, null), false);
            double[] pinned = { reflex, longBarrel, compensator, weighted, quickDraw };
            for (int i = 0; i < Five.Length; i++)
            {
                double share = Dps(Modifiers(stats, Five[i]), false) / stock;
                TestContext.WriteLine(Five[i] + " standing " + share.ToString("0.0000"));
                Assert.That(share, Is.EqualTo(pinned[i]).Within(0.001), Five[i]);
            }
            Assert.That(Dps(Modifiers(stats, WeaponAttachments.WeightedGrip), true) / Dps(Modifiers(stats, null), true), Is.EqualTo(1.0).Within(0.001), "the Weighted Grip deals the same on the move: it costs speed, not damage");
        }

        // ---------------- One part a slot ----------------

        [Test]
        public void EachSlotsThree_OneAtATime_TheOneReplaced_StaysOwned()
        {
            var v = new MetaValues();
            foreach (WeaponSlot slot in WeaponAttachments.SlotsOf(WeaponKind.Pistols))
            {
                var s = new MetaState { Currency = 1000 };
                List<WeaponAttachment> three = v.Parts.Where(p => p.Slot == slot).ToList();
                foreach (WeaponAttachment p in three)
                {
                    Assert.That(WeaponRack.Buy(s, v, p.Id), Is.EqualTo(MetaBuy.Bought), p.Name);
                    Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { p.Id }), p.Name + ": bought and fitted, the slot's other off");
                    Assert.That(WeaponRack.FittedIn(s, v, WeaponKind.Pistols, slot), Is.SameAs(p));
                }
                Assert.That(s.AttachmentsOwned, Is.EqualTo(three.Select(p => p.Id)), "all three owned");
                foreach (WeaponAttachment p in three)
                {
                    AttachmentUse expected = WeaponRack.IsFitted(s, p.Id) ? AttachmentUse.TakenOff : AttachmentUse.Fitted;
                    Assert.That(WeaponRack.Use(s, v, p.Id), Is.EqualTo(expected), p.Name);
                    Assert.That(s.AttachmentsFitted.Count, Is.LessThanOrEqualTo(1), p.Name + ": one a slot");
                }
                Assert.That(s.Currency, Is.EqualTo(1000 - three.Sum(p => p.Price)), "each price once");
            }
            // Tidied: three sights fitted in a hand-edited save, the first kept.
            var t = new MetaState();
            t.AttachmentsOwned.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.Compensator });
            t.AttachmentsFitted.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.LaserSight, WeaponAttachments.Compensator, WeaponAttachments.GhostRing });
            MetaRack.Tidy(t, v);
            Assert.That(t.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.Compensator }));
        }

        // ---------------- Buying, fitting, saving, reading back ----------------

        [Test]
        public void BuyingEveryPart_FittingOneASlot_SavingAndReadingBack_TheSameLoadout()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 400 };
            foreach (WeaponAttachment p in WeaponRack.InSlotOrder(v)) Assert.That(WeaponRack.Buy(s, v, p.Id), Is.EqualTo(MetaBuy.Bought), p.Name);
            Assert.That(s.Currency, Is.EqualTo(30), "400 less the whole set's 370");
            Assert.That(WeaponRack.NextBuy(s, v).Words(), Is.EqualTo("every part owned"));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.Compensator, WeaponAttachments.QuickDrawGrip }),
                        "each slot's last bought is fitted, the slot's others off");
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.LongBarrel), Is.EqualTo(AttachmentFit.Fitted));
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.Compensator), Is.EqualTo(AttachmentFit.Fitted), "and back");
            Assert.That(WeaponRack.FittedWords(s, v), Is.EqualTo("Reflex Sight | Compensator | Quick-Draw Grip"));

            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            string text = File.ReadAllText(SavePath);
            StringAssert.Contains("\"fitted\": [\"reflex-sight\", \"quick-draw-grip\", \"compensator\"]", text); // the barrel fitted last
            StringAssert.Contains("\"version\": 1", text, "the save keeps its shape");
            MetaState back = MetaSaveFile.Load(SavePath, v, out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(back.AttachmentsOwned, Is.EqualTo(s.AttachmentsOwned));
            Assert.That(back.AttachmentsFitted, Is.EqualTo(s.AttachmentsFitted));
            var e = new AttachmentEffects();
            WeaponRack.Apply(back, v, e);
            Assert.That(e.Parts, Is.EqualTo(new[] { "Reflex Sight", "Compensator", "Quick-Draw Grip" }), "in the table's order");
            Assert.That(e.Reach(10f), Is.EqualTo(8.5f));
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(29), "40 x 0.8 x 0.9 = 28.8");
            Assert.That(e.ShotCritChance(0.05f), Is.EqualTo(0.15f).Within(1e-6f));
            Assert.That(e.ShotStartup(0.08f, true), Is.EqualTo(0.02f).Within(1e-6f));
        }

        [Test]
        public void NextBuy_WithTheNine()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 37 };
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            Assert.That(WeaponRack.NextBuy(s, v).Words(), Is.EqualTo("enough for Ghost Ring (30), Reflex Sight (35)"));
            s.Currency = 30;
            s.AttachmentsOwned.AddRange(Four.Skip(1));
            Assert.That(WeaponRack.NextBuy(s, v).Words(), Is.EqualTo("Reflex Sight at 35: 5 more"), "the cheapest of the five");
        }

        // ---------------- The switch off ----------------

        [Test]
        public void Off_TheStoreIsTheFour_FieldForField_AndTheFiveAreNotSold()
        {
            var off = new MetaValues { MoreParts = false };
            MetaValues before = BeforeValues();
            Assert.That(WeaponRack.InSlotOrder(off).Select(p => p.Id), Is.EqualTo(WeaponRack.InSlotOrder(before).Select(p => p.Id)));
            Assert.That(WeaponRack.WholePrice(off), Is.EqualTo(150));
            foreach (string id in Five)
            {
                Assert.That(WeaponRack.Find(off, id), Is.Null, id + ": not in the store");
                var s = new MetaState { Currency = 100 };
                Assert.That(WeaponRack.Buy(s, off, id), Is.EqualTo(MetaBuy.Unknown), id);
                Assert.That(WeaponRack.Use(s, off, id), Is.EqualTo(AttachmentUse.Unknown), id);
                Assert.That(s.Currency, Is.EqualTo(100));
            }
            foreach (int currency in new[] { 0, 28, 30, 37, 75, 200 })
            {
                var a = new MetaState { Currency = currency };
                var b = new MetaState { Currency = currency };
                Assert.That(WeaponRack.NextBuy(a, off).Words(), Is.EqualTo(WeaponRack.NextBuy(b, before).Words()), "the end screen's words at " + currency);
            }
        }

        [Test]
        public void Off_ASaveThatOwnsTheFive_KeepsThem_UnshownAndUnused_TheRunIsTheStockGunOrTheFours()
        {
            var off = new MetaValues { MoreParts = false };
            var s = new MetaState { Currency = 12 };
            s.AttachmentsOwned.AddRange(Five);
            s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            s.AttachmentsFitted.AddRange(new[] { WeaponAttachments.ReflexSight, WeaponAttachments.BullBarrel, WeaponAttachments.QuickDrawGrip });
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            MetaState read = MetaSaveFile.Load(SavePath, off, out _, out _);
            Assert.That(read.AttachmentsOwned, Is.EqualTo(s.AttachmentsOwned), "kept while off");
            Assert.That(read.AttachmentsFitted, Is.EqualTo(s.AttachmentsFitted));

            var e = new AttachmentEffects();
            WeaponRack.Apply(read, off, e);
            var expected = new AttachmentEffects();
            expected.Add(WeaponRack.Find(BeforeValues(), WeaponAttachments.BullBarrel));
            Assert.That(e.Parts, Is.EqualTo(new[] { "Bull Barrel" }), "only the four's are applied");
            Assert.That(e.ToString(), Is.EqualTo(expected.ToString()), "field for field the Bull Barrel's alone");
            Assert.That(WeaponRack.FittedWords(read, off), Is.EqualTo("Bull Barrel"));
            Assert.That(WeaponRack.FittedIn(read, off, WeaponKind.Pistols, WeaponSlot.Sight), Is.Null, "the sight slot reads empty");
            Assert.That(WeaponRack.NextBuy(read, off).Words(), Is.EqualTo("Laser Sight at 30: 18 more"), "the five not offered");

            // Fitting the Laser Sight while off takes the hidden Reflex Sight off its slot (one a slot), which stays owned.
            read.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            Assert.That(WeaponRack.Fit(read, off, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentFit.Fitted));
            Assert.That(read.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.BullBarrel, WeaponAttachments.QuickDrawGrip, WeaponAttachments.LaserSight }));
            Assert.That(read.AttachmentsOwned, Does.Contain(WeaponAttachments.ReflexSight));

            // On again: the five are there, fitted as they were left.
            var on = new MetaValues();
            MetaRack.Tidy(read, on);
            WeaponRack.Apply(read, on, e);
            Assert.That(e.Parts, Is.EqualTo(new[] { "Laser Sight", "Bull Barrel", "Quick-Draw Grip" }));
        }

        [Test]
        public void TheSave_IsOnlyEverATemporaryOne_InTheseTests()
        {
            Assert.That(Path.GetFullPath(SavePath), Does.StartWith(Path.GetFullPath(Path.GetTempPath())), "under the machine's temporary folder");
            Assert.That(SavePath, Does.Contain("MoreWeaponPartsTests-"), "a folder of this test's own");
        }

        // ---------------- Her sheet ----------------

        static OfferContext Context(bool stats, params string[] ids)
        {
            var values = new RunValues();
            var m = new RunModifiers { StatsAndTiers = stats, StatValues = values.Stats, CooldownFloor = values.CooldownFloor };
            foreach (string id in ids) m.Weapon.Add(Part(id));
            return new OfferContext { Modifiers = m, Run = values };
        }

        [Test]
        public void HerSheet_TheBasicShot_ShowsEachNewPartsNumbers()
        {
            SheetSkill stock = HerSheet.Skill(null, "LMB", Context(true));
            Assert.That(stock.Lines.Select(l => l.Label), Is.EqualTo(new[] { "damage", "your damage", "fire rate", "reach", "crit" }), "the stock gun's lines as before");
            Assert.That(stock.ValueOf("crit"), Does.StartWith("5 % chance"));

            SheetSkill reflex = HerSheet.Skill(null, "LMB", Context(true, WeaponAttachments.ReflexSight));
            Assert.That(reflex.ValueOf("crit"), Does.StartWith("15 % chance, a crit deals +50 %"));
            Assert.That(reflex.ValueOf("reach"), Is.EqualTo("8.5 m"));
            Assert.That(HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.ReflexSight)).ValueOf("crit"), Is.EqualTo("no crits (her stats are off)"));

            SheetSkill longBarrel = HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.LongBarrel));
            Assert.That(longBarrel.ValueOf("reach"), Is.EqualTo("13 m"));
            Assert.That(longBarrel.ValueOf("fire rate"), Is.EqualTo("3.7 shots/s, 0.27 s apart"));

            SheetSkill compensator = HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.Compensator));
            Assert.That(compensator.ValueOf("fire rate"), Is.EqualTo("5.6 shots/s, 0.18 s apart"));
            Assert.That(compensator.ValueOf("damage"), Does.StartWith("32"));

            SheetSkill weighted = HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.WeightedGrip));
            Assert.That(weighted.ValueOf("stagger"), Is.EqualTo("3 a hit"));
            Assert.That(HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.BullBarrel)).ValueOf("stagger"), Is.EqualTo("3 a hit"), "the Bull Barrel's stagger now said too");

            SheetSkill quick = HerSheet.Skill(null, "LMB", Context(false, WeaponAttachments.QuickDrawGrip));
            Assert.That(quick.ValueOf("first shot"), Is.EqualTo("after a dash or a skill it goes off in 0.02 s, not 0.08 s"));
            Assert.That(quick.ValueOf("damage"), Does.StartWith("36"));

            foreach (string id in Five)
            {
                OfferContext c = Context(true, id);
                Assert.That(HerSheet.TakenFor(null, c.Modifiers), Is.EqualTo(new[] { Part(id).Name }), id + ": named among what the basic shot has");
            }
        }
    }
}
