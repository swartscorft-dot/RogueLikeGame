using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 139 as rules: the parts' drawbacks halved (<see cref="WeaponPartValues.GentlerCosts"/>) for all nine, each
    /// in its own unit, every benefit standing, and off the table of before field for field; what is fitted is what the next
    /// run uses (the halved numbers); the five tabs and the three pages; the mount points inside the model's box; how the model
    /// turns (a drag, the coast and its stop, the tip's limit, the wheel's limits, the keys, R, the slow turn by itself); her
    /// numbers as fitted and with a preview; and where the three pages put their parts (nothing overlapping, inside the band).
    /// </summary>
    public class UpgradeTabsTests
    {
        bool _gentler;

        [SetUp]
        public void KeepSwitch() => _gentler = WeaponPartValues.GentlerCosts;

        [TearDown]
        public void PutSwitchBack() => WeaponPartValues.GentlerCosts = _gentler;

        static WeaponAttachment[] Table(bool gentler)
        {
            WeaponPartValues.GentlerCosts = gentler;
            return WeaponAttachments.Create();
        }

        static WeaponAttachment Of(WeaponAttachment[] t, string id) => t.First(p => p.Id == id);

        // ---------------- The drawbacks halved ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndSoAreTheTabs()
        {
            Assert.That(new[] { WeaponPartValues.GentlerCosts, UpgradeTabsValues.On }, Is.EqualTo(new[] { true, true }), "his ticks: \"Halver alle ulempene\", \"Tre faner, tre former\"");
        }

        [Test]
        public void EveryCost_IsHalfWhatItWas_InItsOwnUnit()
        {
            WeaponAttachment[] before = Table(false), after = Table(true);
            // Each cost's step away from the stock gun, before and after: half.
            void Half(string id, Func<WeaponAttachment, float> cost, float neutral)
            {
                float b = cost(Of(before, id)) - neutral, a = cost(Of(after, id)) - neutral;
                Assert.That(b, Is.Not.EqualTo(0f).Within(1e-6f), id + " had a cost");
                Assert.That(a, Is.EqualTo(b * 0.5f).Within(1e-5f), id + ": half the step of " + b);
            }
            Half(WeaponAttachments.LaserSight, p => p.HalfWidthScale, 1f);
            Half(WeaponAttachments.GhostRing, p => p.ReachAdd, 0f);
            Half(WeaponAttachments.BullBarrel, p => p.IntervalScale, 1f);
            Half(WeaponAttachments.RunnersGrip, p => p.MovingDamageScale, 1f);
            Half(WeaponAttachments.ReflexSight, p => p.ReachAdd, 0f);
            Half(WeaponAttachments.LongBarrel, p => p.IntervalScale, 1f);
            Half(WeaponAttachments.Compensator, p => p.DamageScale, 1f);
            Half(WeaponAttachments.WeightedGrip, p => p.MovingSpeedShare, new FeelValues().MovingShotSpeedShare);
            Half(WeaponAttachments.QuickDrawGrip, p => p.DamageScale, 1f);
        }

        [Test]
        public void EveryBenefit_Stands()
        {
            WeaponAttachment[] before = Table(false), after = Table(true);
            Assert.That(after.Length, Is.EqualTo(9));
            foreach (WeaponAttachment b in before)
            {
                WeaponAttachment a = Of(after, b.Id);
                Assert.That(new object[] { a.Name, a.Slot, a.Price, a.Line, a.Stagger, a.ShowsLine, a.CritChanceAdd, a.FirstShotStartupScale, a.MorePart },
                            Is.EqualTo(new object[] { b.Name, b.Slot, b.Price, b.Line, b.Stagger, b.ShowsLine, b.CritChanceAdd, b.FirstShotStartupScale, b.MorePart }), b.Name);
            }
            // The benefits that share a knob with no cost: the Ghost Ring's width, the Bull Barrel's damage, the Runner's Grip's
            // speed, the Long Barrel's reach, the Compensator's pace.
            Assert.That(Of(after, WeaponAttachments.GhostRing).HalfWidthScale, Is.EqualTo(1.6f));
            Assert.That(Of(after, WeaponAttachments.BullBarrel).DamageScale, Is.EqualTo(1.35f));
            Assert.That(Of(after, WeaponAttachments.RunnersGrip).MovingSpeedShare, Is.EqualTo(0.75f));
            Assert.That(Of(after, WeaponAttachments.LongBarrel).ReachAdd, Is.EqualTo(3f));
            Assert.That(Of(after, WeaponAttachments.Compensator).IntervalScale, Is.EqualTo(0.8f));
            Assert.That(Of(after, WeaponAttachments.LaserSight).ReachAdd, Is.EqualTo(2f));
        }

        [Test]
        public void TheCostLines_SayTheNewNumbers()
        {
            Assert.That(Table(true).Select(p => p.CostLine), Is.EqualTo(new[]
            {
                "The shot's line is 15 % narrower: aim truer.", "Reach 9 m.", "17.5 % slower between shots.", "Shots on the move deal 10 % less.",
                "Reach 9.25 m.", "10 % slower between shots.", "Each shot deals 10 % less.", "Shooting on the move slows you to 42.5 %, not 50 %.", "Each shot deals 5 % less.",
            }));
        }

        [Test]
        public void SwitchedOff_TheTableIsTodays_FieldForField()
        {
            WeaponAttachment[] off = Table(false);
            Assert.That(off.Select(p => p.CostLine), Is.EqualTo(new[]
            {
                "The shot's line is narrower: aim truer.", "Reach 8 m.", "35 % slower between shots.", "Shots on the move deal 20 % less.",
                "Reach 8.5 m.", "20 % slower between shots.", "Each shot deals 20 % less.", "Shooting on the move slows you to 35 %, not 50 %.", "Each shot deals 10 % less.",
            }));
            Assert.That(off.Select(p => new[] { p.HalfWidthScale, p.ReachAdd, p.IntervalScale, p.MovingDamageScale, p.DamageScale, p.MovingSpeedShare }), Is.EqualTo(new[]
            {
                new[] { 0.7f, 2f, 1f, 1f, 1f, 0f }, new[] { 1.6f, -2f, 1f, 1f, 1f, 0f }, new[] { 1f, 0f, 1.35f, 1f, 1.35f, 0f }, new[] { 1f, 0f, 1f, 0.8f, 1f, 0.75f },
                new[] { 1f, -1.5f, 1f, 1f, 1f, 0f }, new[] { 1f, 3f, 1.2f, 1f, 1f, 0f }, new[] { 1f, 0f, 0.8f, 1f, 0.8f, 0f }, new[] { 1f, 0f, 1f, 1f, 1f, 0.35f }, new[] { 1f, 0f, 1f, 1f, 0.9f, 0f },
            }));
        }

        [Test]
        public void WhatIsFitted_IsWhatTheNextRunUses_WithTheHalvedCosts()
        {
            WeaponPartValues.GentlerCosts = true;
            var v = new MetaValues();
            var s = new MetaState { Currency = 500 };
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.GhostRing), Is.EqualTo(MetaBuy.Bought));
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.BullBarrel), Is.EqualTo(MetaBuy.Bought));
            Assert.That(WeaponRack.Buy(s, v, WeaponAttachments.WeightedGrip), Is.EqualTo(MetaBuy.Bought));
            var e = new AttachmentEffects();
            WeaponRack.Apply(s, v, e);
            var f = new FeelValues();
            Assert.That(e.Reach(f.BasicShotRange), Is.EqualTo(9f).Within(1e-5f), "the Ghost Ring's 9 m");
            Assert.That(e.Interval(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery), Is.EqualTo(0.225f * 1.175f).Within(1e-5f), "the Bull Barrel's 17.5 %");
            Assert.That(e.ShotDamage(40, false), Is.EqualTo(54), "its benefit stands: 35 % harder");
            Assert.That(e.MovingShare(f.MovingShotSpeedShare), Is.EqualTo(0.425f).Within(1e-6f), "the Weighted Grip's 42.5 %");
            Assert.That(e.Parts, Is.EqualTo(new[] { "Ghost Ring", "Bull Barrel", "Weighted Grip" }));
        }

        // ---------------- The tabs ----------------

        [Test]
        public void FiveTabs_ThreePagesBetweenStartRunAndStats()
        {
            Assert.That(UpgradeTabsFlow.TabWords, Is.EqualTo(new[] { "START RUN", "WEAPON", "RANKS", "RACK", "STATS" }));
            Assert.That(Enumerable.Range(0, 5).Select(UpgradeTabsFlow.TabScreen), Is.EqualTo(new[] { MenuScreen.Character, MenuScreen.Upgrades, MenuScreen.Upgrades, MenuScreen.Upgrades, MenuScreen.Stats }));
            Assert.That(new[] { 1, 2, 3 }.Select(UpgradeTabsFlow.TabPage), Is.EqualTo(new[] { UpgradesPage.Weapon, UpgradesPage.Ranks, UpgradesPage.Rack }));
            foreach (UpgradesPage p in new[] { UpgradesPage.Weapon, UpgradesPage.Ranks, UpgradesPage.Rack })
                Assert.That(UpgradeTabsFlow.TabPage(UpgradeTabsFlow.TabOf(MenuScreen.Upgrades, p)), Is.EqualTo(p));
            Assert.That(UpgradeTabsFlow.TabOf(MenuScreen.Loadout, UpgradesPage.Rack), Is.EqualTo(0));
            Assert.That(UpgradeTabsFlow.TabOf(MenuScreen.Stats, UpgradesPage.Rack), Is.EqualTo(4));
            Assert.That(UpgradeTabsFlow.Step(UpgradesPage.Rack, 1), Is.EqualTo(UpgradesPage.Weapon), "Tab goes round");
            Assert.That(UpgradeTabsFlow.Step(UpgradesPage.Weapon, -1), Is.EqualTo(UpgradesPage.Rack), "Q goes round the other way");
        }

        // ---------------- The mount points ----------------

        [Test]
        public void TheMountPoints_AreInsideTheModelsBox_AndFaceAWay()
        {
            foreach (WeaponSlot slot in new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip })
            {
                WeaponMount m = WeaponStageValues.MountOf(slot);
                foreach (float c in new[] { m.At.X, m.At.Y, m.At.Z }) Assert.That(Math.Abs(c), Is.LessThanOrEqualTo(1f), slot + " inside the box");
                Assert.That(m.Forward.Length, Is.GreaterThan(0.5f), slot + " faces a way");
                Assert.That(m.Up.Length, Is.GreaterThan(0.5f), slot + " has a top");
            }
            Assert.That(WeaponStageValues.Sight.At.Y, Is.GreaterThan(0.5f), "the sight on top (on the barrel's top, 0.8 of the box: the block behind it rises higher)");
            Assert.That(WeaponStageValues.Barrel.At.X, Is.EqualTo(1f), "the barrel at the muzzle (+X)");
            Assert.That(WeaponStageValues.Grip.At.Y, Is.LessThan(0f), "the grip below");
            Assert.That(WeaponStageValues.ModelPath, Is.EqualTo("Weapon/Pistol"), "the one named place");
        }

        // ---------------- The turn ----------------

        static void Run(WeaponTurn t, float seconds, float dt = 1f / 60f)
        {
            for (float s = 0f; s < seconds; s += dt) t.Step(dt);
        }

        [Test]
        public void ADrag_TurnsIt_AndAReleaseLetsItCoastAndStop()
        {
            var t = new WeaponTurn();
            float yaw0 = t.Yaw, pitch0 = t.Pitch;
            for (int i = 0; i < 10; i++)
            {
                t.Drag(20f, 5f);
                t.Step(1f / 60f);
            }
            Assert.That(t.Yaw - yaw0, Is.EqualTo(200f * WeaponStageValues.DegreesPerPixel).Within(1e-3f), "right turns it by the pixels dragged");
            Assert.That(t.Pitch - pitch0, Is.EqualTo(50f * WeaponStageValues.DegreesPerPixel).Within(1e-3f), "down tips it");
            t.EndDrag();
            float atRelease = t.Yaw;
            Run(t, 0.1f);
            Assert.That(t.Yaw, Is.GreaterThan(atRelease + 1f), "it coasts on");
            Run(t, 2.5f);
            float stopped = t.Yaw;
            Assert.That(t.YawSpeed, Is.EqualTo(0f), "and stops");
            Run(t, 0.5f);
            Assert.That(t.Yaw, Is.EqualTo(stopped), "stopped, before the slow turn by itself starts");
            Assert.That(stopped - atRelease, Is.LessThan(WeaponStageValues.MostCoastSpeed * WeaponStageValues.CoastSeconds + 1f), "a little inertia, not a spin");
        }

        [Test]
        public void TheTip_IsHeldInsideItsLimit()
        {
            var t = new WeaponTurn();
            t.Drag(0f, 10000f);
            t.Step(1f / 60f);
            Assert.That(t.Pitch, Is.EqualTo(WeaponStageValues.MostPitch));
            t.Drag(0f, -100000f);
            t.Step(1f / 60f);
            Assert.That(t.Pitch, Is.EqualTo(-WeaponStageValues.MostPitch));
        }

        [Test]
        public void TheWheel_StaysInsideItsLimits()
        {
            var t = new WeaponTurn();
            Assert.That(t.Distance, Is.EqualTo(WeaponStageValues.RestDistance));
            t.Wheel(-1f);
            Assert.That(t.Distance, Is.EqualTo(WeaponStageValues.RestDistance - WeaponStageValues.WheelStep).Within(1e-5f), "toward her: nearer");
            for (int i = 0; i < 100; i++) t.Wheel(-1f);
            Assert.That(t.Distance, Is.EqualTo(WeaponStageValues.NearestDistance));
            for (int i = 0; i < 100; i++) t.Wheel(1f);
            Assert.That(t.Distance, Is.EqualTo(WeaponStageValues.FarthestDistance));
            Assert.That(WeaponStageValues.NearestDistance, Is.LessThan(WeaponStageValues.RestDistance));
            Assert.That(WeaponStageValues.FarthestDistance, Is.GreaterThan(WeaponStageValues.RestDistance));
        }

        [Test]
        public void TwoKeysTurnIt_AndRSetsItBack()
        {
            var t = new WeaponTurn();
            float yaw0 = t.Yaw;
            t.KeyTurn(1);
            Run(t, 1f, 0.01f);
            Assert.That(t.Yaw - yaw0, Is.EqualTo(WeaponStageValues.KeyTurnSpeed).Within(2f), "right, at its speed");
            t.KeyTurn(-1);
            Run(t, 0.5f, 0.01f);
            Assert.That(t.Yaw - yaw0, Is.EqualTo(WeaponStageValues.KeyTurnSpeed * 0.5f).Within(2f), "and left");
            t.KeyTurn(0);
            t.Wheel(3f);
            t.Reset();
            Assert.That(new[] { t.Yaw, t.Pitch, t.Distance, t.YawSpeed }, Is.EqualTo(new[] { WeaponStageValues.RestYaw, WeaponStageValues.RestPitch, WeaponStageValues.RestDistance, 0f }));
        }

        [Test]
        public void LeftAlone_ItTurnsSlowlyByItself()
        {
            var t = new WeaponTurn();
            float yaw0 = t.Yaw;
            Run(t, WeaponStageValues.AutoTurnAfter - 0.1f);
            Assert.That(t.Yaw, Is.EqualTo(yaw0), "not at once");
            Assert.That(t.Moving, Is.False, "still: the stage does not draw");
            Run(t, 0.2f + WeaponStageValues.AutoTurnEaseIn + 2f);
            Assert.That(t.Yaw - yaw0, Is.EqualTo(WeaponStageValues.AutoTurnSpeed * 2f).Within(WeaponStageValues.AutoTurnSpeed * 0.75f), "then slowly");
            Assert.That(t.Moving, Is.True);
            t.Drag(1f, 0f);
            Assert.That(t.Idle, Is.EqualTo(0f), "a touch stops it waiting again");
        }

        // ---------------- Her numbers ----------------

        static OfferContext Context(MetaState s, MetaValues v)
        {
            var c = new OfferContext { Modifiers = new RunModifiers() };
            WeaponRack.Apply(s, v, c.Modifiers.Weapon);
            return c;
        }

        [Test]
        public void HerNumbers_AreTheRunsAsFitted_AndAPreviewChangesThem()
        {
            WeaponPartValues.GentlerCosts = true;
            var v = new MetaValues();
            var s = new MetaState { Currency = 0 };
            var now = new double[WeaponNumbers.Count];
            OfferContext c = Context(s, v);
            WeaponNumbers.Of(c, now);
            Assert.That(now[(int)WeaponNumber.Damage], Is.EqualTo(HerSheet.BasicShotDealt(c)));
            Assert.That(now[(int)WeaponNumber.Reach], Is.EqualTo(10.0).Within(1e-5), "the stock gun's 10 m");
            Assert.That(now[(int)WeaponNumber.Width], Is.EqualTo(1.0).Within(1e-5), "1 m wide");
            Assert.That(now[(int)WeaponNumber.MovingSpeed], Is.EqualTo(0.5).Within(1e-5));
            Assert.That(now[(int)WeaponNumber.ShotsASecond], Is.EqualTo(1.0 / HerSheet.BasicInterval(c)).Within(1e-6));

            // The Bull Barrel under the cursor, not owned: harder, slower, a stagger.
            var then = new double[WeaponNumbers.Count];
            WeaponNumbers.Preview(s, v, WeaponSlot.Barrel, WeaponAttachments.BullBarrel, c.Modifiers.Weapon);
            WeaponNumbers.Of(c, then);
            Assert.That(WeaponNumbers.Better(WeaponNumber.Damage, now[0], then[0]), Is.EqualTo(1), "teal: better");
            Assert.That(WeaponNumbers.Better(WeaponNumber.ShotsASecond, now[1], then[1]), Is.EqualTo(-1), "red: worse");
            Assert.That(then[(int)WeaponNumber.Stagger], Is.EqualTo(3.0));
            Assert.That(WeaponNumbers.Change(WeaponNumber.Reach, now[2], then[2]), Is.EqualTo(""), "the reach does not change");
            Assert.That(s.AttachmentsFitted, Is.Empty, "a preview fits nothing");

            // "none" under the cursor with a part fitted: the slot as the stock gun's.
            s.AttachmentsOwned.Add(WeaponAttachments.LongBarrel);
            s.AttachmentsFitted.Add(WeaponAttachments.LongBarrel);
            c = Context(s, v);
            WeaponNumbers.Of(c, now);
            Assert.That(now[(int)WeaponNumber.Reach], Is.EqualTo(13.0).Within(1e-5));
            WeaponNumbers.Preview(s, v, WeaponSlot.Barrel, null, c.Modifiers.Weapon);
            WeaponNumbers.Of(c, then);
            Assert.That(WeaponNumbers.Change(WeaponNumber.Reach, now[2], then[2]), Is.EqualTo("-3 m"));
            Assert.That(WeaponNumbers.Better(WeaponNumber.FirstShot, 0.08, 0.02), Is.EqualTo(1), "a quicker first shot is better");
        }

        [Test]
        public void TheNumbersWords_ReadWell()
        {
            Assert.That(WeaponNumbers.Words(WeaponNumber.Damage, 54.0), Is.EqualTo("54"));
            Assert.That(WeaponNumbers.Words(WeaponNumber.Reach, 9.25), Is.EqualTo("9.25 m"));
            Assert.That(WeaponNumbers.Words(WeaponNumber.MovingSpeed, 0.425), Is.EqualTo("42.5 %"));
            Assert.That(WeaponNumbers.Words(WeaponNumber.FirstShot, 0.02), Is.EqualTo("0.02 s"));
            Assert.That(WeaponNumbers.Change(WeaponNumber.Damage, 40, 54), Is.EqualTo("+14"));
            Assert.That(WeaponNumbers.Change(WeaponNumber.MovingSpeed, 0.5, 0.425), Is.EqualTo("-7.5 %"));
        }

        // ---------------- Where the pages put things ----------------

        [Test]
        public void ThePages_PutNothingOverAnything_InsideTheirBand()
        {
            var rows = new List<int> { 4, 4, 4 };
            var weapon = new List<LayoutRect> { UpgradeTabsLayout.Lists, UpgradeTabsLayout.Stage, UpgradeTabsLayout.PartPanel, UpgradeTabsLayout.Numbers };
            for (int i = 0; i < weapon.Count; i++)
                for (int k = i + 1; k < weapon.Count; k++)
                    Assert.That(weapon[i].Overlaps(weapon[k]), Is.False, "WEAPON's panels " + i + " and " + k);
            foreach (LayoutRect r in weapon)
            {
                Assert.That(r.X, Is.GreaterThanOrEqualTo(UpgradeTabsLayout.Left));
                Assert.That(r.Right, Is.LessThanOrEqualTo(UpgradeTabsLayout.Right));
                Assert.That(r.Bottom, Is.LessThanOrEqualTo(UpgradeTabsLayout.Bottom));
            }
            LayoutRect lastRow = UpgradeTabsLayout.ListRowRect(2, 3, rows);
            Assert.That(lastRow.Bottom, Is.LessThanOrEqualTo(UpgradeTabsLayout.Lists.Bottom), "the lists' last row inside their panel");
            Assert.That(UpgradeTabsLayout.NumberRow(WeaponNumbers.Count - 1).Bottom, Is.LessThanOrEqualTo(UpgradeTabsLayout.Numbers.Bottom), "the numbers inside theirs");
            Assert.That(UpgradeTabsLayout.Stage.Width, Is.GreaterThan(UpgradeTabsLayout.PlanWidth * 0.45f), "the model large, in the middle");
            Assert.That(UpgradeTabsLayout.RanksPanel(4).Bottom, Is.LessThanOrEqualTo(UpgradeTabsLayout.Bottom), "four ranks fit");
            Assert.That(UpgradeTabsLayout.RackList(11).Bottom, Is.LessThanOrEqualTo(UpgradeTabsLayout.Bottom), "eleven pieces fit");
            Assert.That(UpgradeTabsLayout.RackList(11).Overlaps(UpgradeTabsLayout.RackPanel), Is.False, "the rack's list and its panel");
            for (int i = 0; i < 4; i++)
                Assert.That(UpgradeTabsLayout.StepRect(UpgradeTabsLayout.RankRowRect(i, 4), 9, 10).Right, Is.LessThanOrEqualTo(UpgradeTabsLayout.RankRowRect(i, 4).X + UpgradeTabsLayout.RankNameWidth + UpgradeTabsLayout.RankStepsWidth + 1f), "ten steps in their room");
            Assert.That(UpgradeTabsLayout.Fit(1920f, out float x0), Is.EqualTo(1f));
            Assert.That(x0, Is.EqualTo(0f));
            Assert.That(UpgradeTabsLayout.Fit(1440f, out float x1), Is.EqualTo(0.75f), "a 4:3 view: the plan fitted");
            Assert.That(x1, Is.EqualTo(0f));
        }
    }
}
