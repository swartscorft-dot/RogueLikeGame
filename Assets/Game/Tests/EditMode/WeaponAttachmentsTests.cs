using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 060 (2026-11-21; decision 0014, the producer's proposal, a draft Kristian has not ruled on): her pistols'
    /// parts as rules. Each part's knobs through the effects, and the empty effects, which are the stock gun for every method;
    /// buying (it fits), fitting one a slot, taking off, a click in each state, a save tidied; what the end screen says the
    /// Echoes buy; the save (the round trip, a save with no parts written exactly as before, a save of before read with
    /// none); the rooms' Echoes and every total of 029 as it was; the power of each part against the stock gun; and where the
    /// screen's panels go. The save is only ever written under a temporary folder of this test.
    /// </summary>
    public class WeaponAttachmentsTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder() => _folder = Path.Combine(Path.GetTempPath(), "WeaponAttachmentsTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void RemoveFolder()
        {
            if (Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        string SavePath => Path.Combine(_folder, "between-runs.json");

        static readonly FeelValues Feel = new FeelValues();

        static WeaponAttachment Part(string id) => WeaponRack.Find(new MetaValues(), id);

        static AttachmentEffects With(params string[] ids)
        {
            var e = new AttachmentEffects();
            foreach (string id in ids) e.Add(Part(id));
            return e;
        }

        // ---------------- The switch and the table ----------------

        [Test]
        public void TheSwitch_IsOffInCode_AndTheFourParts_AreTheOrdersTable()
        {
            var v = new MetaValues();
            Assert.That(v.Attachments, Is.False, "off by default");
            Assert.That(v.Enabled, Is.False, "and the trial too");
            Assert.That(v.PerRoom, Is.EqualTo(3));
            Assert.That(v.Parts.Select(p => p.Id), Is.EqualTo(new[] { "laser-sight", "ghost-ring", "bull-barrel", "runners-grip" }));
            Assert.That(v.Parts.Select(p => p.Name), Is.EqualTo(new[] { "Laser Sight", "Ghost Ring", "Bull Barrel", "Runner's Grip" }));
            Assert.That(v.Parts.Select(p => p.Slot), Is.EqualTo(new[] { WeaponSlot.Sight, WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip }));
            Assert.That(v.Parts.Select(p => p.Price), Is.EqualTo(new[] { 30, 30, 40, 50 }));
            Assert.That(WeaponRack.WholePrice(v), Is.EqualTo(150), "the whole set");
            Assert.That(v.Parts.Select(p => p.Line), Is.EqualTo(new[]
            {
                "A red beam on her pistols. Reach 12 m.", "A wider shot, forgiving up close.", // work order 102: the Laser Sight's words no longer promise where the shot ends
                "Each shot hits 35 % harder and staggers.", "Shooting on the move slows you to 75 %, not 50 %.",
            }));
            Assert.That(v.Parts.Select(p => p.CostLine), Is.EqualTo(new[]
            {
                "The shot's line is narrower: aim truer.", "Reach 8 m.", "35 % slower between shots.", "Shots on the move deal 20 % less.",
            }));
            Assert.That(MetaRack.WholePrice(v), Is.EqualTo(810), "the rack's own price is as it was: parts are not pieces");
            Assert.That(v.Pieces.Length, Is.EqualTo(10));
        }

        // ---------------- The loadout: a weapon has slots, a slot holds one part ----------------

        [Test]
        public void TheData_SaysWhichWeaponEachPartBelongsTo_ThePistolsThreeSlots()
        {
            var v = new MetaValues();
            Assert.That(WeaponAttachments.Weapons, Is.EqualTo(new[] { WeaponKind.Pistols }), "one weapon in this order");
            Assert.That(WeaponAttachments.SlotsOf(WeaponKind.Pistols), Is.EqualTo(new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip }));
            Assert.That(WeaponAttachments.WeaponName(WeaponKind.Pistols), Is.EqualTo("Pistols"));
            Assert.That(v.Parts.All(p => p.Weapon == WeaponKind.Pistols), "the four are the pistols'");
            Assert.That(v.Parts.Select(p => p.Id).Distinct().Count(), Is.EqualTo(v.Parts.Length), "one id each, across every weapon");
            Assert.That(WeaponRack.InSlotOrder(v).Select(p => p.Id), Is.EqualTo(new[] { "laser-sight", "ghost-ring", "bull-barrel", "runners-grip" }));
        }

        [Test]
        public void ASecondWeaponsParts_AreRowsOfData_TheirSlotIsTheirWeapons_AndTheSaveKeepsItsShape()
        {
            // A weapon that does not exist yet, as a value of the enum would make it: its own sight.
            var other = (WeaponKind)1;
            var v = new MetaValues();
            v.Parts = v.Parts.Concat(new[]
            {
                new WeaponAttachment("test-choke", "Test Choke", other, WeaponSlot.Sight, 10, "Narrower.", "") { HalfWidthScale = 0.5f },
                new WeaponAttachment("test-choke-2", "Test Choke II", other, WeaponSlot.Sight, 10, "Narrower still.", "") { HalfWidthScale = 0.25f },
            }).ToArray();
            var s = new MetaState { Currency = 200 };
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.LaserSight), Is.EqualTo(MetaBuy.Bought));
            Assert.That(WeaponRack.Buy(s, v, "test-choke"), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.LaserSight, "test-choke" }), "another weapon's sight does not take the pistols' off");
            Assert.That(WeaponRack.Buy(s, v, "test-choke-2"), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.LaserSight, "test-choke-2" }), "its own slot holds one");
            Assert.That(WeaponRack.FittedIn(s, v, other, WeaponSlot.Sight).Id, Is.EqualTo("test-choke-2"));

            var pistols = new AttachmentEffects();
            WeaponRack.Apply(s, v, pistols, WeaponKind.Pistols);
            Assert.That(pistols.Parts, Is.EqualTo(new[] { "Laser Sight" }), "a weapon's effects hold only its own parts");
            var second = new AttachmentEffects();
            WeaponRack.Apply(s, v, second, other);
            Assert.That(second.Parts, Is.EqualTo(new[] { "Test Choke II" }));

            // The save: the same two lists of ids.
            s.AttachmentsFitted.Add("test-choke"); // two in its slot, as a hand-edited save might have
            MetaRack.Tidy(s, v);
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.LaserSight, "test-choke-2" }), "one a weapon's slot, the first kept");
            string text = MetaJson.Write(s);
            StringAssert.Contains("\"attachments\": [\"laser-sight\", \"test-choke\", \"test-choke-2\"]", text);
            StringAssert.Contains("\"fitted\": [\"laser-sight\", \"test-choke-2\"]", text);
        }

        [Test]
        public void APartWithNoCost_IsBoughtAndFittedAsAnyOther()
        {
            // "Ikke nødvendigvis et bytte" (Kristian, 2026-11-21): a plain gain is a part like any other; nothing refuses it.
            var v = new MetaValues();
            v.Parts = v.Parts.Concat(new[] { new WeaponAttachment("test-gain", "Test Gain", WeaponKind.Pistols, WeaponSlot.Barrel, 5, "Reach 11 m.", "") { ReachAdd = 1f } }).ToArray();
            var s = new MetaState { Currency = 5 };
            Assert.That(WeaponRack.Buy(s, v, "test-gain"), Is.EqualTo(MetaBuy.Bought));
            var e = new AttachmentEffects();
            WeaponRack.Apply(s, v, e);
            Assert.That(e.Reach(10f), Is.EqualTo(11f));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(40), "and nothing taken");
            Assert.That(e.Recovery(0.08f, 0.02f, 0.125f), Is.EqualTo(0.125f));
        }

        // ---------------- The effects ----------------

        [Test]
        public void TheEmptyEffects_AreTheStockGun_ForEveryMethod()
        {
            var e = new AttachmentEffects();
            Assert.That(e.Any, Is.False);
            Assert.That(e.ShowsLine, Is.False);
            Assert.That(e.ShotStagger, Is.Zero);
            foreach (float reach in new[] { 0.5f, 3f, 4f, 10f, 13f }) Assert.That(e.Reach(reach), Is.EqualTo(reach), "reach " + reach + ": even under the least");
            foreach (float w in new[] { 0.01f, 0.1f, 0.5f, 2f }) Assert.That(e.HalfWidth(w), Is.EqualTo(w));
            for (int d = -5; d <= 400; d += 7)
            {
                Assert.That(e.ShotDamage(d, false), Is.EqualTo(d));
                Assert.That(e.ShotDamage(d, true), Is.EqualTo(d));
            }
            Assert.That(e.Recovery(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(Feel.BasicShotRecovery));
            Assert.That(e.Recovery(0.3f, 0.1f, 0.01f), Is.EqualTo(0.01f));
            foreach (float share in new[] { 0f, 0.3f, 0.5f, 1f })
            {
                Assert.That(e.MovingShare(share), Is.EqualTo(share));
                Assert.That(e.BackpedalShare(share), Is.EqualTo(share));
            }
            // And a run's modifiers start with them empty and clear them.
            var m = new RunModifiers();
            Assert.That(m.Weapon.Any, Is.False);
            m.Weapon.Add(Part(WeaponAttachments.BullBarrel));
            Assert.That(m.Weapon.Any, Is.True);
            m.Clear();
            Assert.That(m.Weapon.Any, Is.False);
            Assert.That(m.Weapon.ToString(), Is.EqualTo(new AttachmentEffects().ToString()));
        }

        [Test]
        public void LaserSight_Reach12_HalfWidth035_ShowsTheLine()
        {
            AttachmentEffects e = With(WeaponAttachments.LaserSight);
            Assert.That(e.Parts, Is.EqualTo(new[] { "Laser Sight" }));
            Assert.That(e.Reach(Feel.BasicShotRange), Is.EqualTo(12f));
            Assert.That(e.HalfWidth(Feel.BasicShotHalfWidth), Is.EqualTo(0.35f).Within(1e-6f));
            Assert.That(e.ShowsLine, Is.True);
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(40));
            Assert.That(e.Recovery(Feel.BasicShotStartup, Feel.BasicShotActive, Feel.BasicShotRecovery), Is.EqualTo(Feel.BasicShotRecovery));
            Assert.That(e.MovingShare(0.5f), Is.EqualTo(0.5f));
            Assert.That(e.ShotStagger, Is.Zero);
            Assert.That(e.HalfWidth(0.12f), Is.EqualTo(0.1f).Within(1e-6f), "never under 0.1 m");
        }

        [Test]
        public void GhostRing_HalfWidth08_Reach8_NeverUnder4()
        {
            AttachmentEffects e = With(WeaponAttachments.GhostRing);
            Assert.That(e.Reach(Feel.BasicShotRange), Is.EqualTo(8f));
            Assert.That(e.HalfWidth(Feel.BasicShotHalfWidth), Is.EqualTo(0.8f).Within(1e-6f));
            Assert.That(e.ShowsLine, Is.False);
            Assert.That(e.Reach(5f), Is.EqualTo(4f), "never under 4 m");
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40));
        }

        [Test]
        public void BullBarrel_54ADamage_Stagger3_AndAThirdMoreBetweenShots_TheStartupKept()
        {
            AttachmentEffects e = With(WeaponAttachments.BullBarrel);
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(54));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(54), "on the move too");
            Assert.That(e.ShotDamage(46, false), Is.EqualTo(62), "62.1, rounded");
            Assert.That(e.ShotDamage(2, false), Is.EqualTo(3), "2.7, rounded half away from zero");
            Assert.That(e.ShotStagger, Is.EqualTo(3f));
            float s = Feel.BasicShotStartup, a = Feel.BasicShotActive, r = Feel.BasicShotRecovery;
            Assert.That(e.Recovery(s, a, r), Is.EqualTo(0.225f * 1.35f - 0.1f).Within(1e-6f), "0.20375 s: the recovery takes it all");
            Assert.That(e.Interval(s, a, r), Is.EqualTo(0.30375f).Within(1e-6f));
            Assert.That(e.Reach(10f), Is.EqualTo(10f));
            Assert.That(e.HalfWidth(0.5f), Is.EqualTo(0.5f));

            // With her attack speed (decision 0012) on top: her start's 15 % slower, as the feel's was.
            var m = new RunModifiers { StatsAndTiers = true };
            m.Weapon.Add(Part(WeaponAttachments.BullBarrel));
            float interval = m.Stats.BasicShotInterval(s, a, m.Weapon.Recovery(s, a, r));
            Assert.That(interval, Is.EqualTo(0.30375f / 0.85f).Within(1e-5f), "0.357 s, as decision 0014 says");
        }

        [Test]
        public void RunnersGrip_75PercentOnTheMove_AndTheBackpedalCap_32OnTheMove()
        {
            AttachmentEffects e = With(WeaponAttachments.RunnersGrip);
            Assert.That(e.MovingShare(Feel.MovingShotSpeedShare), Is.EqualTo(0.75f));
            Assert.That(e.BackpedalShare(Feel.BackpedalSpeedShare), Is.EqualTo(0.75f));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(32));
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(40), "standing: all of it");
            Assert.That(e.ShotDamage(46, true), Is.EqualTo(37), "36.8, rounded");
            Assert.That(e.Recovery(0.08f, 0.02f, 0.125f), Is.EqualTo(0.125f));
        }

        [Test]
        public void SeveralParts_AddsAdd_ScalesMultiply_TheLargestMovingShare_AnyLine()
        {
            AttachmentEffects e = With(WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip);
            Assert.That(e.Parts, Is.EqualTo(new[] { "Laser Sight", "Bull Barrel", "Runner's Grip" }));
            Assert.That(e.Reach(10f), Is.EqualTo(12f));
            Assert.That(e.ShowsLine, Is.True);
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(54));
            Assert.That(e.ShotDamage(40, true), Is.EqualTo(43), "40 x 1.35 x 0.8 = 43.2");
            Assert.That(e.MovingShare(0.5f), Is.EqualTo(0.75f));
            var two = new AttachmentEffects();
            two.Add(new WeaponAttachment { MovingSpeedShare = 0.6f });
            two.Add(new WeaponAttachment { MovingSpeedShare = 0.7f });
            two.Add(new WeaponAttachment { MovingSpeedShare = 0.65f });
            Assert.That(two.MovingShare(0.5f), Is.EqualTo(0.7f), "the largest");
            two.Add(new WeaponAttachment { ReachAdd = 1f });
            two.Add(new WeaponAttachment { ReachAdd = -3f });
            Assert.That(two.Reach(10f), Is.EqualTo(8f), "adds add");
            two.Add(new WeaponAttachment { HalfWidthScale = 2f });
            two.Add(new WeaponAttachment { HalfWidthScale = 0.5f });
            two.Add(new WeaponAttachment { HalfWidthScale = 1.5f });
            Assert.That(two.HalfWidth(0.5f), Is.EqualTo(0.75f).Within(1e-6f), "scales multiply");
        }

        // ---------------- Buying, fitting, taking off ----------------

        [Test]
        public void Buying_NotEnough_AlreadyOwned_BuyingFits_AndAPartReplaced_StaysOwned()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 29 };
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.LaserSight), Is.EqualTo(MetaBuy.NotEnough));
            Assert.That(s.Currency, Is.EqualTo(29), "nothing taken");
            Assert.That(s.AttachmentsOwned, Is.Empty);
            Assert.That(WeaponRack.Buy(s, v, "no-such-part"), Is.EqualTo(MetaBuy.Unknown));

            s.Currency = 100;
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.LaserSight), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.Currency, Is.EqualTo(70), "once");
            Assert.That(WeaponRack.IsFitted(s, WeaponAttachments.LaserSight), Is.True, "buying fits it at once");
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.LaserSight), Is.EqualTo(MetaBuy.AlreadyOwned));
            Assert.That(s.Currency, Is.EqualTo(70));

            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.GhostRing), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.Currency, Is.EqualTo(40));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { WeaponAttachments.GhostRing }), "it replaces what its slot held");
            Assert.That(s.AttachmentsOwned, Is.EqualTo(new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing }), "the one replaced stays owned");
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.BullBarrel), Is.EqualTo(MetaBuy.Bought));
            Assert.That(s.AttachmentsFitted, Is.EquivalentTo(new[] { WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel }), "another slot: both");
            Assert.That(s.Currency, Is.Zero);

            // The rack is never touched.
            Assert.That(s.Owned, Is.Empty);
            Assert.That(s.Equipped, Is.Empty);
            Assert.That(MetaRack.PointsUsed(s, v), Is.Zero, "parts have no points");
        }

        [Test]
        public void OnePartASlot_FitReplaces_TakeOff_LeavesTheStockGun()
        {
            var v = new MetaValues();
            var s = new MetaState();
            s.AttachmentsOwned.AddRange(new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.RunnersGrip });
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.BullBarrel), Is.EqualTo(AttachmentFit.NotOwned));
            Assert.That(WeaponRack.Fit(s, v, "gone"), Is.EqualTo(AttachmentFit.Unknown));
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentFit.Fitted));
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentFit.AlreadyFitted));
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.RunnersGrip), Is.EqualTo(AttachmentFit.Fitted));
            Assert.That(WeaponRack.Fit(s, v, WeaponAttachments.GhostRing), Is.EqualTo(AttachmentFit.Fitted));
            Assert.That(s.AttachmentsFitted, Is.EquivalentTo(new[] { WeaponAttachments.RunnersGrip, WeaponAttachments.GhostRing }));
            Assert.That(WeaponRack.FittedIn(s, v, WeaponKind.Pistols, WeaponSlot.Sight).Id, Is.EqualTo(WeaponAttachments.GhostRing));
            Assert.That(WeaponRack.FittedIn(s, v, WeaponKind.Pistols, WeaponSlot.Barrel), Is.Null, "the stock barrel");
            Assert.That(WeaponRack.TakeOff(s, v, WeaponAttachments.GhostRing), Is.True);
            Assert.That(WeaponRack.TakeOff(s, v, WeaponAttachments.GhostRing), Is.False, "not on any more");
            Assert.That(WeaponRack.FittedIn(s, v, WeaponKind.Pistols, WeaponSlot.Sight), Is.Null);
            Assert.That(WeaponRack.Owns(s, WeaponAttachments.GhostRing), Is.True, "taken off, still owned");

            var effects = new AttachmentEffects();
            WeaponRack.Apply(s, v, effects);
            Assert.That(effects.Parts, Is.EqualTo(new[] { "Runner's Grip" }));
            effects.Add(Part(WeaponAttachments.BullBarrel));
            WeaponRack.Apply(s, v, effects);
            Assert.That(effects.Parts, Is.EqualTo(new[] { "Runner's Grip" }), "cleared first");
            WeaponRack.TakeOff(s, v, WeaponAttachments.RunnersGrip);
            WeaponRack.Apply(s, v, effects);
            Assert.That(effects.Any, Is.False);
        }

        [Test]
        public void Use_InEachState()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 35 };
            Assert.That(WeaponRack.Use(s, v, WeaponAttachments.BullBarrel), Is.EqualTo(AttachmentUse.NotEnough), "not owned, too dear");
            Assert.That(s.Currency, Is.EqualTo(35));
            Assert.That(WeaponRack.Use(s, v, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentUse.Bought), "not owned: bought");
            Assert.That(WeaponRack.IsFitted(s, WeaponAttachments.LaserSight), Is.True, "and fitted");
            Assert.That(s.Currency, Is.EqualTo(5));
            Assert.That(WeaponRack.Use(s, v, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentUse.TakenOff), "fitted: taken off");
            Assert.That(WeaponRack.IsFitted(s, WeaponAttachments.LaserSight), Is.False);
            Assert.That(WeaponRack.Use(s, v, WeaponAttachments.LaserSight), Is.EqualTo(AttachmentUse.Fitted), "owned: fitted");
            Assert.That(s.Currency, Is.EqualTo(5), "never paid twice");
            Assert.That(WeaponRack.Use(s, v, "gone"), Is.EqualTo(AttachmentUse.Unknown));
        }

        [Test]
        public void Tidy_DropsUnknownAndDoubled_TakesOffWhatIsNotOwned_OneASlot_TheFirstKept()
        {
            var v = new MetaValues();
            var s = new MetaState();
            s.AttachmentsOwned.AddRange(new[] { "laser-sight", "gone-part", "laser-sight", null, "bull-barrel", "ghost-ring" });
            s.AttachmentsFitted.AddRange(new[] { "ghost-ring", "laser-sight", "runners-grip", "bull-barrel", "bull-barrel", "gone-part" });
            MetaRack.Tidy(s, v); // the one place a save is tidied: it tidies the parts too
            Assert.That(s.AttachmentsOwned, Is.EqualTo(new[] { "laser-sight", "bull-barrel", "ghost-ring" }));
            Assert.That(s.AttachmentsFitted, Is.EqualTo(new[] { "ghost-ring", "bull-barrel" }),
                        "the second sight off (the first kept), the grip not owned, the barrel once, the unknown dropped");
        }

        // ---------------- What the end screen says ----------------

        [Test]
        public void NextBuy_WhatTheEchoesBuy_OrWhatTheNextNeeds()
        {
            var v = new MetaValues();
            var s = new MetaState { Currency = 30 };
            WeaponNextBuy n = WeaponRack.NextBuy(s, v);
            Assert.That(n.Affordable.Select(p => p.Id), Is.EqualTo(new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing }));
            Assert.That(n.Words(), Is.EqualTo("enough for Laser Sight (30), Ghost Ring (30)"));

            s.Currency = 37;
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            Assert.That(WeaponRack.NextBuy(s, v).Words(), Is.EqualTo("enough for Ghost Ring (30)"));

            s.Currency = 28;
            s.AttachmentsOwned.Add(WeaponAttachments.GhostRing);
            n = WeaponRack.NextBuy(s, v);
            Assert.That(n.Affordable, Is.Empty);
            Assert.That(n.Cheapest.Id, Is.EqualTo(WeaponAttachments.BullBarrel));
            Assert.That(n.More, Is.EqualTo(12));
            Assert.That(n.Words(), Is.EqualTo("Bull Barrel at 40: 12 more"));

            s.Currency = 0;
            Assert.That(WeaponRack.NextBuy(new MetaState(), v).Words(), Is.EqualTo("Laser Sight at 30: 30 more"), "the first of the cheapest");

            s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            s.AttachmentsOwned.Add(WeaponAttachments.RunnersGrip);
            n = WeaponRack.NextBuy(s, v);
            Assert.That(n.AllOwned, Is.True);
            Assert.That(n.Words(), Is.EqualTo("every part owned"));
        }

        // ---------------- The save ----------------

        /// <summary>Today's writer (work order 029's) of this state, character for character, pinned.</summary>
        const string WrittenBefore =
            "{\n  \"version\": 1,\n  \"currency\": 45,\n  \"earnedInAll\": 100,\n  \"runs\": 3,\n  \"owned\": [\"thick-hide\", \"steady-start\"],\n"
            + "  \"equipped\": [\"thick-hide\"],\n  \"feats\": [\"clear-floor-1\"],\n  \"struckOut\": \"Fan Fire\"\n}\n";

        const string EmptyBefore =
            "{\n  \"version\": 1,\n  \"currency\": 0,\n  \"earnedInAll\": 0,\n  \"runs\": 0,\n  \"owned\": [],\n  \"equipped\": [],\n  \"feats\": [],\n  \"struckOut\": \"\"\n}\n";

        static MetaState Before()
        {
            var s = new MetaState { Currency = 45, EarnedInAll = 100, Runs = 3, StruckOut = "Fan Fire" };
            s.Owned.AddRange(new[] { MetaPieces.ThickHide, MetaPieces.SteadyStart });
            s.Equipped.Add(MetaPieces.ThickHide);
            s.Feats.Add("clear-floor-1");
            return s;
        }

        [Test]
        public void TheSave_WithNoParts_IsWrittenExactlyAsBefore_CharacterForCharacter()
        {
            Assert.That(MetaJson.Write(Before()), Is.EqualTo(WrittenBefore));
            Assert.That(MetaJson.Write(new MetaState()), Is.EqualTo(EmptyBefore));
            Assert.That(MetaJson.Write(Before()), Does.Not.Contain("attachments").And.Not.Contain("fitted"));
            // And on disk, the same bytes (UTF-8, no mark).
            Assert.That(MetaSaveFile.Save(SavePath, Before(), out string error), Is.True, error);
            Assert.That(File.ReadAllBytes(SavePath), Is.EqualTo(new System.Text.UTF8Encoding(false).GetBytes(WrittenBefore)));
        }

        [Test]
        public void ASaveOfBefore_ReadsWithNoParts()
        {
            MetaState s = MetaJson.Read(WrittenBefore);
            Assert.That(s.AttachmentsOwned, Is.Empty);
            Assert.That(s.AttachmentsFitted, Is.Empty);
            Assert.That(s.Currency, Is.EqualTo(45));
            Directory.CreateDirectory(_folder);
            File.WriteAllText(SavePath, WrittenBefore);
            MetaState loaded = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(loaded.AttachmentsOwned, Is.Empty);
            Assert.That(loaded.AttachmentsFitted, Is.Empty);
            Assert.That(loaded.Owned, Is.EqualTo(new[] { MetaPieces.ThickHide, MetaPieces.SteadyStart }));
        }

        [Test]
        public void TheSave_RoundTrip_KeepsTheParts_AndTheVersionStays1()
        {
            MetaState s = Before();
            s.AttachmentsOwned.AddRange(new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip });
            s.AttachmentsFitted.AddRange(new[] { WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip });
            string text = MetaJson.Write(s);
            Assert.That(text, Is.EqualTo(WrittenBefore.Substring(0, WrittenBefore.Length - 3)
                                         + ",\n  \"attachments\": [\"laser-sight\", \"ghost-ring\", \"bull-barrel\", \"runners-grip\"],\n"
                                         + "  \"fitted\": [\"ghost-ring\", \"bull-barrel\", \"runners-grip\"]\n}\n"), "the two lists after the rest");
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            MetaState back = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(back.Version, Is.EqualTo(MetaState.CurrentVersion), "read into this code's version (work order 099: 2); on disk it stays 1, below");
            Assert.That(back.AttachmentsOwned, Is.EqualTo(s.AttachmentsOwned));
            Assert.That(back.AttachmentsFitted, Is.EqualTo(s.AttachmentsFitted));
            Assert.That(back.Owned, Is.EqualTo(s.Owned));
            Assert.That(back.Currency, Is.EqualTo(45));
            StringAssert.Contains("\"version\": 1,", File.ReadAllText(SavePath));

            // Only one of the two lists: both are written.
            var owned = new MetaState();
            owned.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            StringAssert.EndsWith("\"struckOut\": \"\",\n  \"attachments\": [\"laser-sight\"],\n  \"fitted\": []\n}\n", MetaJson.Write(owned));

            // The copy keeps them, apart.
            MetaState copy = back.Copy();
            Assert.That(copy.AttachmentsOwned, Is.EqualTo(back.AttachmentsOwned));
            Assert.That(copy.AttachmentsFitted, Is.EqualTo(back.AttachmentsFitted));
            copy.AttachmentsFitted.Clear();
            Assert.That(back.AttachmentsFitted.Count, Is.EqualTo(3), "a copy, not the same lists");
        }

        [Test]
        public void TheSave_KeepsThePartsWhileTheSwitchIsOff_AndTheRunsCommitWritesThemBackUnchanged()
        {
            var v = new MetaValues(); // the attachments off, as in code
            Assert.That(v.Attachments, Is.False);
            MetaState s = Before();
            s.AttachmentsOwned.AddRange(new[] { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel });
            s.AttachmentsFitted.AddRange(new[] { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel });
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
            MetaState read = MetaSaveFile.Load(SavePath, v, out _, out _);
            MetaRack.Commit(read, v, new RunEarnings { FloorsCleared = 1, ElitesCleared = 1 });
            Assert.That(read.AttachmentsOwned, Is.EqualTo(s.AttachmentsOwned));
            Assert.That(read.AttachmentsFitted, Is.EqualTo(s.AttachmentsFitted));
            Assert.That(MetaSaveFile.Save(SavePath, read, out error), Is.True, error);
            StringAssert.Contains("\"attachments\": [\"laser-sight\", \"bull-barrel\"],\n  \"fitted\": [\"laser-sight\", \"bull-barrel\"]", File.ReadAllText(SavePath));
        }

        [Test]
        public void TheSave_IsOnlyEverATemporaryOne_InTheseTests()
        {
            Assert.That(Path.GetFullPath(SavePath), Does.StartWith(Path.GetFullPath(Path.GetTempPath())), "under the machine's temporary folder");
            Assert.That(SavePath, Does.Contain("WeaponAttachmentsTests-"), "a folder of this test's own");
        }

        // ---------------- Earnings ----------------

        [TestCase(0, 0, false, 0, TestName = "Rooms0_DiedOnFloor1_Nothing_As029")]
        [TestCase(0, 1, false, 15, TestName = "Rooms0_DiedOnFloor1_AfterItsEliteRoom_15_As029")]
        [TestCase(1, 1, false, 25, TestName = "Rooms0_DiedOnFloor2_25_As029")]
        [TestCase(1, 0, false, 10, TestName = "Rooms0_DiedOnFloor2_EliteRoomWalkedPast_10_As029")]
        [TestCase(3, 2, false, 60, TestName = "Rooms0_LeftOnFloor4_60_As029")]
        [TestCase(4, 4, false, 100, TestName = "Rooms0_DiedToTheBoss_100_As029")]
        [TestCase(5, 5, true, 225, TestName = "Rooms0_ActCleared_225_As029")]
        [TestCase(5, 0, true, 150, TestName = "Rooms0_ActCleared_NoEliteRoom_150_As029")]
        public void WithNoRooms_EveryTotalIs029s(int floors, int elites, bool boss, int expected)
        {
            var v = new MetaValues();
            var e = new RunEarnings { FloorsCleared = floors, ElitesCleared = elites, BossKilled = boss };
            Assert.That(e.RoomsCleared, Is.Zero);
            Assert.That(e.FromRooms(v), Is.Zero);
            Assert.That(e.Total(v), Is.EqualTo(expected));
            Assert.That(e.Total(v), Is.EqualTo(e.FromFloors(v) + e.FromElites(v) + e.FromBoss(v)));
        }

        [Test]
        public void WithRooms_AWorkedExample()
        {
            var v = new MetaValues();
            // Floor 1 cleared, its elite room and its four ordinary combat rooms (a floor in a line has five, the elite among them).
            var floor1 = new RunEarnings { FloorsCleared = 1, ElitesCleared = 1, RoomsCleared = 4 };
            Assert.That(floor1.FromRooms(v), Is.EqualTo(12));
            Assert.That(floor1.Total(v), Is.EqualTo(37), "10 + 4 x 3 + 15: a sight, after one floor");
            Assert.That(new RunEarnings { FloorsCleared = 1, RoomsCleared = 4 }.Total(v), Is.EqualTo(22), "the elite room walked past");
            Assert.That(new RunEarnings { RoomsCleared = 1 }.Total(v), Is.EqualTo(3), "died in floor 1's third room, the first combat room cleared");
            Assert.That(new RunEarnings { FloorsCleared = 1, ElitesCleared = 1, RoomsCleared = 6 }.Total(v), Is.EqualTo(43), "died on floor 2 after two of its rooms");
            var act = new RunEarnings { FloorsCleared = 5, ElitesCleared = 5, RoomsCleared = 20, BossKilled = true };
            Assert.That(act.Total(v), Is.EqualTo(285), "the act cleared, every room: 225 + 60");
            Assert.That(act.Total(v), Is.GreaterThanOrEqualTo(WeaponRack.WholePrice(v)), "the whole set in one cleared act");

            var s = new MetaState { Currency = 2 };
            MetaRack.Commit(s, v, floor1);
            Assert.That(s.Currency, Is.EqualTo(39));
            Assert.That(s.EarnedInAll, Is.EqualTo(37));
        }

        // ---------------- Power: each part against the stock gun ----------------

        /// <summary>A basic shot's damage per second at a standing target, with the feel's numbers: damage over interval, crits as their expected value.</summary>
        static double Dps(RunModifiers m, bool onTheMove)
        {
            FeelValues f = Feel;
            HerStats stats = m.Stats;
            int hit = m.Weapon.ShotDamage(Placeholders.BasicShotDamage + stats.FlatFor(null), onTheMove);
            double expected = 1.0 + stats.CritChance * stats.CritDamage;
            double interval = stats.BasicShotInterval(f.BasicShotStartup, f.BasicShotActive, m.Weapon.Recovery(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery));
            return hit * m.DamageShare * expected / interval;
        }

        static RunModifiers Modifiers(int state, string part)
        {
            var values = new RunValues();
            var m = new RunModifiers { StatsAndTiers = state > 0, StatValues = values.Stats, CooldownFloor = values.CooldownFloor };
            if (state == 2)
            {
                m.Take(new Upgrade { Kind = UpgradeKind.FlatDamage, Amount = 6f });
                m.Take(new Upgrade { Kind = UpgradeKind.AttackSpeed, Amount = 0.12f });
            }
            if (part != null) m.Weapon.Add(Part(part));
            return m;
        }

        // A test of THESE FOUR parts as the proposal made them (each a trade), not a rule of attachments: Kristian, 2026-11-21,
        // "ikke nødvendigvis et bytte", a later part may be a plain gain, and nothing in the rules refuses one
        // (APartWithNoCost_IsBoughtAndFittedAsAnyOther). When a part is meant to be a gain, it does not belong in this list.
        // state 0: the stats and tiers off; 1: on, at her start; 2: on, after +6 flat damage and +12 % attack speed.
        // Work order 082 (review 6, finding 10): renamed "...PartsTable...": the damage per second here is the test's own model of
        // the parts' table (Dps below), not the player; the player's flat damage through the barrel is PlayMode
        // WeaponAttachmentsTests.BullBarrel_WithSixFlatDamage_Deals62_TheFlatDamageTakesTheBarrel.
        [TestCase(0, 177.78, 177.78, 177.78, 177.78, 142.22, TestName = "TheFirstFourPartsTable_KeepsTheBasicShotsDamagePerSecond_Within5PercentOfStock_StatsOff")]
        [TestCase(1, 154.89, 154.89, 154.89, 154.89, 123.91, TestName = "TheFirstFourPartsTable_KeepsTheBasicShotsDamagePerSecond_Within5PercentOfStock_StatsOnAtHerStart")]
        [TestCase(2, 199.50, 199.50, 199.18, 199.50, 160.47, TestName = "TheFirstFourPartsTable_KeepsTheBasicShotsDamagePerSecond_Within5PercentOfStock_StatsOnAfterFlat6AndAttackSpeed12")]
        public void TheFirstFourPartsTable_KeepsTheBasicShotsDamagePerSecond(int state, double stock, double laser, double bull, double runnerStanding, double runnerMoving)
        {
            double s = Dps(Modifiers(state, null), false);
            Assert.That(s, Is.EqualTo(stock).Within(0.01), "the stock gun");
            var parts = new[] { WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip };
            var pinned = new[] { laser, laser, bull, runnerStanding };
            for (int i = 0; i < parts.Length; i++)
            {
                double d = Dps(Modifiers(state, parts[i]), false);
                Assert.That(d, Is.EqualTo(pinned[i]).Within(0.01), parts[i]);
                Assert.That(d / s, Is.InRange(0.95, 1.05), parts[i] + ": within 5 % of the stock gun, standing");
                TestContext.WriteLine(parts[i] + " standing " + d.ToString("0.00") + " against " + s.ToString("0.00") + " (" + (d / s).ToString("0.0000") + ")");
            }
            double moving = Dps(Modifiers(state, WeaponAttachments.RunnersGrip), true);
            double stockMoving = Dps(Modifiers(state, null), true);
            Assert.That(stockMoving, Is.EqualTo(s), "the stock gun deals the same on the move");
            Assert.That(moving, Is.EqualTo(runnerMoving).Within(0.01));
            Assert.That(moving / stockMoving, Is.EqualTo(0.8).Within(0.01), "Runner's Grip on the move: 0.8 of stock");
            TestContext.WriteLine("runners-grip on the move " + moving.ToString("0.00") + " (" + (moving / stockMoving).ToString("0.0000") + ")");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_NamesTheWeapon_OnlyWhenTheRunHadIt_AndTheRoomsShowByThemselves()
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
                E(1, SessionEventTypes.MetaLoadout, "Steady Start", "currency", "40", "budget", "4", "used", "1"),
                E(9, SessionEventTypes.RunEnd, null),
                E(10, SessionEventTypes.RunStart, "BBBBBBBB"),
                E(11, SessionEventTypes.FloorEnter, "s1", "floor", "1"),
                E(11, SessionEventTypes.MetaLoadout, "", "currency", "40", "budget", "4", "used", "0", "weapon", "Laser Sight | Bull Barrel"),
                E(12, SessionEventTypes.MetaEarn, "room", "amount", "3", "floor", "1"),
                E(13, SessionEventTypes.MetaEarn, "room", "amount", "3", "floor", "1"),
                E(20, SessionEventTypes.MetaEarn, "elite room", "amount", "15", "floor", "1"),
                E(30, SessionEventTypes.MetaEarn, "floor", "amount", "10", "floor", "1"),
                E(32, SessionEventTypes.RunEnd, null),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs[0].MetaWeapon, Is.Null);
            Assert.That(stats.Runs[1].MetaWeapon, Is.EqualTo("Laser Sight, Bull Barrel"));
            Assert.That(stats.Runs[1].MetaEarned, Is.EqualTo(31));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("between runs     equipped Steady Start;  earned 0;  new feats -", text, "the first run's line as before");
            StringAssert.Contains("between runs     equipped nothing;  weapon Laser Sight, Bull Barrel;  earned 31 (room 6, elite room 15, floor 10);  new feats -", text);
        }

        // ---------------- The screen's panels ----------------

        [TestCase(960f, TestName = "Layout_At960_InsideTheView_NoOverlap_TheRackAt520")]
        [TestCase(1152f, TestName = "Layout_At1152_InsideTheView_NoOverlap")]
        [TestCase(1280f, TestName = "Layout_At1280_SideBySide_Centred")]
        [TestCase(1707f, TestName = "Layout_At1707_SideBySide_Centred")]
        public void Layout(float viewWidth)
        {
            // Off: 029's rack, in the middle.
            BetweenRunsLayout.Panels(viewWidth, false, out LayoutRect rackOff, out LayoutRect none);
            Assert.That(rackOff.X, Is.EqualTo((viewWidth - 680f) * 0.5f));
            Assert.That(rackOff.Width, Is.EqualTo(680f));
            Assert.That(none.Width, Is.Zero, "no weapon panel");

            BetweenRunsLayout.Panels(viewWidth, true, out LayoutRect rack, out LayoutRect weapon);
            rack.Height = weapon.Height = 600f;
            rack.Y = weapon.Y = BetweenRunsLayout.Top(720f, 600f);
            Assert.That(rack.X, Is.GreaterThanOrEqualTo(0f));
            Assert.That(weapon.Right, Is.LessThanOrEqualTo(viewWidth), "inside the view");
            Assert.That(rack.Overlaps(weapon), Is.False, "no overlap");
            Assert.That(weapon.X, Is.GreaterThan(rack.Right), "the weapon to the right of the rack");
            Assert.That(rack.Width, Is.GreaterThanOrEqualTo(520f), "never under 520");
            Assert.That(weapon.Width, Is.GreaterThan(300f), "room enough for its rows");
            if (viewWidth >= 1280f)
            {
                Assert.That(rack.Width, Is.EqualTo(680f), "at its width");
                Assert.That(weapon.Width, Is.EqualTo(440f));
                Assert.That(rack.X, Is.EqualTo(viewWidth - weapon.Right).Within(1e-3f), "centred");
            }
            else
            {
                Assert.That(rack.X, Is.GreaterThanOrEqualTo(12f));
                Assert.That(weapon.Right, Is.LessThanOrEqualTo(viewWidth - 12f + 1e-3f));
            }
            if (viewWidth == 960f)
            {
                Assert.That(rack.Width, Is.EqualTo(520f));
                Assert.That(weapon.Width, Is.EqualTo(400f));
            }
            TestContext.WriteLine(viewWidth + ": rack " + rack + ", weapon " + weapon);
            Assert.That(BetweenRunsLayout.Top(720f, 800f), Is.EqualTo(6f), "never above 029's top");
        }
    }
}
