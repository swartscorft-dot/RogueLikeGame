using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 040 (Kristian's second session of 2026-11-20), as rules: where the clean part of a recast window lies
    /// for each chain and under each focus way, early in the window now (0.10 to 0.30 s after it opens) and as before with
    /// its switch off; that a press that waited in the buffer is never clean; that a press after the clean part says LATE;
    /// the ring of work order 037 for a clean part anywhere; the cast bar's contents at a given moment, against the judge,
    /// for a chain, a charge, a hold and a cast; and how soon she is back at 90 % of run speed after a shot, before and after.
    /// </summary>
    public class CleanEarlyAndCastBarRulesTests
    {
        static readonly PlayerSignValues V = new PlayerSignValues();
        const int Slot = 7;

        static SkillDefinition Skill(string name) => SkillDefaults.Create().First(s => s.Name == name);

        /// <summary>A chain of this skill up to its last press, with the window for it opened at <paramref name="opened"/>.</summary>
        static RecastChain UpToTheLast(SkillDefinition skill, float opened)
        {
            var chain = new RecastChain();
            float now = opened - 1f;
            chain.Start(Slot, skill.RecastCount, skill.RecastWindow, skill.OpeningMove);
            for (int press = 2; press < skill.RecastCount; press++)
            {
                chain.ShotWentOff(now);
                now += 0.1f;
                chain.Recast();
            }
            chain.ShotWentOff(opened);
            Assert.That(chain.NextIsLast, Is.True);
            return chain;
        }

        static IEnumerable<TestCaseData> Chains()
        {
            yield return new TestCaseData(Skill("Triple Tap")).SetName("Clean_TripleTap");
            yield return new TestCaseData(Skill("Backstep")).SetName("Clean_Backstep");
            yield return new TestCaseData(FocusModeRules.InWindow(Skill("Triple Tap"), new FocusModeValues())).SetName("Clean_TripleTapInSteadyHand");
            SkillDefinition longStep = Skill("Backstep");
            longStep.RecastWindow += 0.2f; // the Long Step upgrade (work order 017): a window of 0.8 s
            yield return new TestCaseData(longStep).SetName("Clean_BackstepLongStep");
        }

        // ---------------- 1. Where the clean part lies ----------------

        [Test]
        public void TheKit_PlacesItEarly_From010To030_WithTheOldShareBesideIt_AndTheSwitchOn()
        {
            var kit = new KitValues();
            Assert.That(kit.CleanEarly, Is.True, "the new placement as the game ships");
            Assert.That(kit.CleanFrom, Is.EqualTo(0.10f));
            Assert.That(kit.CleanTo, Is.EqualTo(0.30f));
            Assert.That(kit.CleanShare, Is.EqualTo(1f / 3f).Within(1e-6f), "the old number, beside them");
            CleanPart early = CleanPart.Of(kit, 0.6f);
            Assert.That(early.From, Is.EqualTo(0.10f).Within(1e-6f));
            Assert.That(early.To, Is.EqualTo(0.30f).Within(1e-6f));
            kit.CleanEarly = false;
            CleanPart last = CleanPart.Of(kit, 0.6f);
            Assert.That(last.From, Is.EqualTo(0.40f).Within(1e-5f), "off: the last third, as before");
            Assert.That(last.To, Is.EqualTo(0.60f).Within(1e-6f));
            Assert.That(CleanPart.Early(0.2f, 0.1f, 0.3f).To, Is.EqualTo(0.2f), "never past the window's end");
            Assert.That(CleanPart.Whole(0.6f).From, Is.EqualTo(0f));
            Assert.That(default(CleanPart).Any, Is.False);
        }

        [TestCaseSource(nameof(Chains))]
        public void TheLastPress_IsClean_From010To030AfterTheWindowOpened_EarlyBefore_LateAfter(SkillDefinition skill)
        {
            var kit = new KitValues();
            const float opened = 10f;
            RecastChain chain = UpToTheLast(skill, opened);
            CleanPart part = CleanPart.Of(kit, chain.Window);
            Assert.That(part.From, Is.EqualTo(0.10f).Within(1e-6f), skill.Name);
            Assert.That(part.To, Is.EqualTo(0.30f).Within(1e-6f), skill.Name + ": the same seconds whatever the window's length");
            RecastPress At(float since, bool buffered = false) => chain.Judge(Slot, opened + since, part, true, buffered);
            Assert.That(At(0.05f), Is.EqualTo(RecastPress.Shot), "0.05 s in: too early, a plain shot");
            Assert.That(At(0.10f), Is.EqualTo(RecastPress.Clean), "0.10 s in: clean");
            Assert.That(At(0.20f), Is.EqualTo(RecastPress.Clean), "0.20 s in: clean");
            Assert.That(At(0.30f), Is.EqualTo(RecastPress.Clean), "0.30 s in: clean, both edges inside");
            Assert.That(At(0.45f), Is.EqualTo(RecastPress.Shot), "0.45 s in: late, a plain shot");
            Assert.That(part.IsAfter(0.45f), Is.True, "and late, not early");
            Assert.That(part.IsAfter(0.05f), Is.False);
            Assert.That(At(chain.Window + 0.01f), Is.EqualTo(RecastPress.None), "after the window: no shot, as before");
            Assert.That(At(0.20f, buffered: true), Is.EqualTo(RecastPress.Shot), "a press that waited from before the window opened is never clean");
            Assert.That(At(0f, buffered: true), Is.EqualTo(RecastPress.Shot), "nor the instant it opens");
        }

        [Test]
        public void APressBeforeTheWindowOpens_IsEarly_AndWaits_AndThePressesBeforeTheLastAreNeverClean()
        {
            SkillDefinition tap = Skill("Triple Tap");
            var chain = new RecastChain();
            chain.Start(Slot, tap.RecastCount, tap.RecastWindow);
            CleanPart part = CleanPart.Of(new KitValues(), tap.RecastWindow);
            Assert.That(chain.Judge(Slot, 10.1f, part, true), Is.EqualTo(RecastPress.Early), "before the first shot went off");
            chain.ShotWentOff(10.15f);
            Assert.That(chain.NextIsLast, Is.False);
            Assert.That(chain.Judge(Slot, 10.35f, part, true), Is.EqualTo(RecastPress.Shot), "the second press, in the clean seconds, is a plain shot");
        }

        [Test]
        public void WithTheSwitchOff_TheLastThirdIsClean_AsBefore_AndTheOldJudgeAgrees()
        {
            var kit = new KitValues { CleanEarly = false };
            SkillDefinition tap = Skill("Triple Tap");
            const float opened = 10f;
            RecastChain chain = UpToTheLast(tap, opened);
            CleanPart part = CleanPart.Of(kit, chain.Window);
            for (float since = 0f; since <= chain.Window + 1e-4f; since += 0.0031f)
                Assert.That(chain.Judge(Slot, opened + since, part, true), Is.EqualTo(chain.Judge(Slot, opened + since, kit.CleanShare, true)), "at " + since);
            Assert.That(chain.Judge(Slot, opened + 0.2f, part, true), Is.EqualTo(RecastPress.Shot), "0.2 s in: early, as before");
            Assert.That(chain.Judge(Slot, opened + 0.5f, part, true), Is.EqualTo(RecastPress.Clean), "0.5 s in: clean, as before");
        }

        // ---------------- 2. The focus ways ----------------

        [Test]
        public void SteadyHand_TripleTapsFifthPress_IsTheCleanOne_InTheSameEarlySeconds()
        {
            SkillDefinition five = FocusModeRules.InWindow(Skill("Triple Tap"), new FocusModeValues());
            Assert.That(five.RecastCount, Is.EqualTo(5));
            var chain = new RecastChain();
            chain.Start(Slot, five.RecastCount, five.RecastWindow);
            CleanPart part = CleanPart.Of(new KitValues(), five.RecastWindow);
            float t = 10f;
            for (int press = 2; press <= 5; press++)
            {
                chain.ShotWentOff(t);
                RecastPress judged = chain.Judge(Slot, t + 0.2f, part, true);
                Assert.That(judged, Is.EqualTo(press == 5 ? RecastPress.Clean : RecastPress.Shot), "press " + press + " at 0.2 s");
                chain.Recast();
                t += 0.4f;
            }
        }

        [Test]
        public void HairTrigger_APrimedChainsLastPress_IsCleanAnywhereInTheWindow_ABufferedOneToo()
        {
            SkillDefinition tap = Skill("Triple Tap");
            Assert.That(FocusModeRules.UseFor(tap), Is.EqualTo(PrimeUse.CleanPress));
            const float opened = 10f;
            RecastChain chain = UpToTheLast(tap, opened);
            CleanPart part = CleanPart.Of(new KitValues(), chain.Window);
            foreach (float since in new[] { 0f, 0.05f, 0.2f, 0.45f, 0.59f })
            foreach (bool buffered in new[] { false, true })
            {
                RecastPress judged = FocusModeRules.Primed(chain.Judge(Slot, opened + since, part, true, buffered), chain.NextIsLast, true);
                Assert.That(judged, Is.EqualTo(RecastPress.Clean), "primed, at " + since + (buffered ? ", buffered" : ""));
            }
            CleanPart whole = CleanPart.Whole(chain.Window);
            Assert.That(whole.Contains(0f) && whole.Contains(chain.Window), Is.True, "what the signs show: the whole window");
        }

        // ---------------- 3. The word for a miss ----------------

        [Test]
        public void APressAfterTheCleanPart_SaysLATE_OneBefore_EARLY()
        {
            SkillDefinition tap = Skill("Triple Tap"), step = Skill("Backstep");
            Assert.That(FeedbackRules.RecastMiss(tap, 3, clean: false, late: true), Is.EqualTo(TimingMiss.Late));
            Assert.That(FeedbackRules.RecastMiss(tap, 3, clean: false, late: false), Is.EqualTo(TimingMiss.Early));
            Assert.That(FeedbackRules.RecastMiss(step, 2, clean: false, late: true), Is.EqualTo(TimingMiss.Late));
            Assert.That(FeedbackRules.RecastMiss(tap, 3, clean: true, late: false), Is.EqualTo(TimingMiss.None));
            Assert.That(FeedbackRules.RecastMiss(tap, 2, clean: false, late: true), Is.EqualTo(TimingMiss.None), "the second press has no timing");
        }

        // ---------------- 4. The ring of work order 037, for a clean part anywhere ----------------

        [Test]
        public void TheRing_IsInsideItsBand_ExactlyWhileThePressIsClean_ForTheEarlyPlacement()
        {
            const float window = 0.6f;
            CleanPart part = CleanPart.Of(new KitValues(), window);
            float last = float.PositiveInfinity;
            for (float since = 0f; since <= window + 1e-4f; since += 0.0029f)
            {
                float r = CleanWindowSign.RingRadius(since, part, V);
                bool inBand = r <= V.BandOuter + 1e-4f && r >= V.BandInner - 1e-4f;
                bool clean = part.Contains(since);
                if (System.Math.Abs(since - part.From) > 2e-4f && System.Math.Abs(since - part.To) > 2e-4f)
                {
                    Assert.That(inBand, Is.EqualTo(clean), "at " + since);
                    Assert.That(CleanWindowSign.InBand(since, part), Is.EqualTo(clean));
                }
                Assert.That(r, Is.LessThanOrEqualTo(last + 1e-6f), "it only closes");
                Assert.That(r, Is.GreaterThan(0f));
                last = r;
            }
            Assert.That(CleanWindowSign.RingRadius(part.From, part, V), Is.EqualTo(V.BandOuter).Within(1e-4f));
            Assert.That(CleanWindowSign.RingRadius(part.To, part, V), Is.EqualTo(V.BandInner).Within(1e-4f));
            Assert.That(CleanWindowSign.RingRadius(window, part, V), Is.LessThan(V.BandInner - 0.1f), "past the band by the window's end");
            Assert.That(CleanWindowSign.Shows(true, part, window, window + 0.01f), Is.False);
        }

        [Test]
        public void TheRingForTheLastShare_IsTheRingAsItWas()
        {
            const float window = 0.6f, share = 1f / 3f;
            CleanPart part = CleanPart.Last(window, share);
            Assert.That(CleanWindowSign.StartRadius(part, V), Is.EqualTo(CleanWindowSign.StartRadius(share, V)).Within(1e-4f));
            for (float since = -0.1f; since <= window; since += 0.013f)
                Assert.That(CleanWindowSign.RingRadius(since, part, V), Is.EqualTo(CleanWindowSign.RingRadius(since, window, share, V)).Within(1e-4f), "at " + since);
            Assert.That(CleanWindowSign.KeyBandFrom(part, window), Is.EqualTo(0f).Within(1e-6f), "its band at the strip's left end");
            Assert.That(CleanWindowSign.KeyBandWidth(part, window), Is.EqualTo(share).Within(1e-5f));
        }

        [Test]
        public void TheKeysStrip_HasItsBandWhereTheEarlyCleanPartIs()
        {
            const float window = 0.6f;
            CleanPart part = CleanPart.Of(new KitValues(), window);
            float from = CleanWindowSign.KeyBandFrom(part, window), width = CleanWindowSign.KeyBandWidth(part, window);
            Assert.That(from, Is.EqualTo(0.5f).Within(1e-5f), "the mark (the share left) is 0.5 to 0.8333 while it is clean");
            Assert.That(from + width, Is.EqualTo(1f - 0.1f / 0.6f).Within(1e-5f));
            Assert.That(new PlayerSignValues().CleanWindow, Is.False, "the ring is off by default now");
            Assert.That(new PlayerSignValues().KeyStrip, Is.True, "the key's strip and the tick stay");
        }

        // ---------------- 5. The cast bar ----------------

        [TestCaseSource(nameof(Chains))]
        public void TheCastBar_ForAChain_HasItsDimPartZoneAndMarker_WhereTheJudgeHasThem(SkillDefinition skill)
        {
            const float opened = 10f;
            RecastChain chain = UpToTheLast(skill, opened);
            CleanPart part = CleanPart.Of(new KitValues(), chain.Window);
            float lead = skill.Startup, window = chain.Window, total = lead + window;

            // Before the window opens: the marker in the dim part, which ends where the window opens.
            CastBarView before = CastBarRules.Recast(lead, window, lead * 0.5f, -1f, part);
            Assert.That(before.Kind, Is.EqualTo(CastBarKind.Recast));
            Assert.That(before.Seconds, Is.EqualTo(total).Within(1e-5f), "from the press before to the end of the window");
            Assert.That(before.DimTo, Is.EqualTo(lead / total).Within(1e-5f));
            Assert.That(before.Marker, Is.EqualTo(0.5f * lead / total).Within(1e-5f));
            Assert.That(before.Marker, Is.LessThan(before.DimTo));
            Assert.That(before.MarkerInZone, Is.False);
            Assert.That(CastBarRules.Recast(lead, window, lead * 3f, -1f, part).Marker, Is.EqualTo(before.DimTo).Within(1e-5f), "it waits at the window's start");
            Assert.That(before.ZoneFrom, Is.EqualTo((lead + 0.1f) / total).Within(1e-5f));
            Assert.That(before.ZoneTo, Is.EqualTo((lead + 0.3f) / total).Within(1e-5f));

            int clean = 0, plain = 0;
            for (float since = 0f; since <= window; since += 0.0037f)
            {
                CastBarView v = CastBarRules.Recast(lead, window, lead + since, chain.SinceOpened(opened + since), part);
                bool judged = chain.Judge(Slot, opened + since, part, true) == RecastPress.Clean;
                Assert.That(v.Marker, Is.EqualTo((lead + since) / total).Within(1e-5f));
                if (System.Math.Abs(since - part.From) > 2e-4f && System.Math.Abs(since - part.To) > 2e-4f)
                    Assert.That(v.MarkerInZone, Is.EqualTo(judged), "the marker in the zone exactly when a press is clean, at " + since);
                if (judged) clean++;
                else plain++;
            }
            Assert.That(clean, Is.GreaterThan(0));
            Assert.That(plain, Is.GreaterThan(0));
            Assert.That(CastBarRules.Recast(lead, window, 9f, window + 1f, part).Marker, Is.EqualTo(1f).Within(1e-5f), "at the end at most");
        }

        [Test]
        public void TheCastBar_ForAPressThatCannotBeClean_HasNoZone()
        {
            CastBarView v = CastBarRules.Recast(0.15f, 0.6f, 0.05f, -1f, default);
            Assert.That(v.HasZone, Is.False);
            Assert.That(v.MarkerInZone, Is.False);
            Assert.That(CastBarRules.Recast(0f, 0f, 0f, 0f, default).Kind, Is.EqualTo(CastBarKind.None));
        }

        [Test]
        public void TheCastBar_ForLongShotsCharge_RunsToWhereItFiresByItself_WithThePerfectBandTheZone()
        {
            SkillDefinition shot = Skill("Long Shot");
            CastBarRules.PerfectBand(shot, false, new FocusModeValues(), out float from, out float to);
            Assert.That(from, Is.EqualTo(shot.PerfectFrom));
            Assert.That(to, Is.EqualTo(shot.PerfectTo));
            for (float charged = 0f; charged <= shot.HoldMax; charged += 0.011f)
            {
                CastBarView v = CastBarRules.Charge(charged, shot.HoldMax, from, to);
                Assert.That(v.Kind, Is.EqualTo(CastBarKind.Charge));
                Assert.That(v.Seconds, Is.EqualTo(shot.HoldMax), "start of the charge to where it fires by itself");
                Assert.That(v.Marker, Is.EqualTo(charged / shot.HoldMax).Within(1e-5f));
                bool perfect = PoolRules.IsPerfect(shot, charged);
                if (System.Math.Abs(charged - from) > 1e-3f && System.Math.Abs(charged - to) > 1e-3f)
                    Assert.That(v.MarkerInZone, Is.EqualTo(perfect), "the zone is the perfect band, at " + charged);
            }
            // Primed by Hair Trigger: perfect from half a charge on, to its end.
            CastBarRules.PerfectBand(shot, true, new FocusModeValues(), out float pFrom, out float pTo);
            Assert.That(pFrom, Is.EqualTo(0.5f * shot.ChargeTime).Within(1e-5f));
            Assert.That(pTo, Is.EqualTo(shot.HoldMax).Within(1e-5f));
            Assert.That(FocusModeRules.PrimedPerfect(0.6f, new FocusModeValues()), Is.True);
            Assert.That(CastBarRules.Charge(0.6f * shot.ChargeTime, shot.HoldMax, pFrom, pTo).MarkerInZone, Is.True);
            Assert.That(CastBarRules.Charge(0.4f * shot.ChargeTime, shot.HoldMax, pFrom, pTo).MarkerInZone, Is.False);
        }

        [Test]
        public void TheCastBar_ForFanFiresHold_ShowsItsStartupDim_ThenHowLongItCanBeHeld()
        {
            SkillDefinition fan = Skill("Fan Fire");
            CastBarView v = CastBarRules.Hold(fan.Startup + fan.HoldMax * 0.5f, fan.Startup, fan.HoldMax);
            Assert.That(v.Kind, Is.EqualTo(CastBarKind.Hold));
            Assert.That(v.Seconds, Is.EqualTo(fan.Startup + fan.HoldMax).Within(1e-5f));
            Assert.That(v.DimTo, Is.EqualTo(fan.Startup / v.Seconds).Within(1e-5f));
            Assert.That(v.Marker, Is.EqualTo((fan.Startup + fan.HoldMax * 0.5f) / v.Seconds).Within(1e-5f));
            Assert.That(v.HasZone, Is.False, "no place to press: how long it can be held");
            Assert.That(CastBarRules.Hold(99f, fan.Startup, fan.HoldMax).Marker, Is.EqualTo(1f));
            SkillDefinition volley = Skill("Heavy Volley");
            CastBarView cast = CastBarRules.Cast(volley.Startup * 0.25f, volley.Startup);
            Assert.That(cast.Kind, Is.EqualTo(CastBarKind.Cast));
            Assert.That(cast.Marker, Is.EqualTo(0.25f).Within(1e-5f));
            Assert.That(cast.HasZone, Is.False);
        }

        [Test]
        public void TheFlash_FadesOverItsSeconds_AndIsTheColourOfWhatThePressWas()
        {
            var v = new CastBarValues();
            Assert.That(v.On, Is.True, "the bar is on by default");
            Assert.That(CastBarRules.FlashAlpha(0f, v.FlashSeconds), Is.EqualTo(1f));
            Assert.That(CastBarRules.FlashAlpha(v.FlashSeconds * 0.5f, v.FlashSeconds), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(CastBarRules.FlashAlpha(v.FlashSeconds, v.FlashSeconds), Is.EqualTo(0f), "and goes");
            Assert.That(CastBarRules.FlashAlpha(-0.1f, v.FlashSeconds), Is.EqualTo(0f));
            Assert.That(CastBarRules.OfPress(true), Is.EqualTo(CastBarFlash.Clean));
            Assert.That(CastBarRules.OfPress(false), Is.EqualTo(CastBarFlash.Plain));
        }

        // ---------------- 6. Quicker off the mark after shooting ----------------

        /// <summary>The feel as the game reads it: the settings asset's numbers where they differ from the code's start values.</summary>
        static FeelValues AsTheAssetHasIt(bool quick) => new FeelValues { MovingShotRecovery = 0.25f, QuickOffTheMark = quick };

        [Test]
        public void TheQuickNumbers_StandBesideTheOldOnes_AndTheSwitchPicks()
        {
            FeelValues on = AsTheAssetHasIt(true), off = AsTheAssetHasIt(false);
            Assert.That(new FeelValues().QuickOffTheMark, Is.True, "on as the game ships");
            Assert.That(OffTheMark.MovingShotRecovery(on), Is.EqualTo(on.MovingShotRecoveryQuick));
            Assert.That(OffTheMark.MovingShotRecovery(off), Is.EqualTo(0.25f));
            Assert.That(OffTheMark.ShotTurnHold(on), Is.EqualTo(on.ShotTurnHoldQuick));
            Assert.That(OffTheMark.ShotTurnHold(off), Is.EqualTo(off.ShotTurnHold));
            Assert.That(OffTheMark.SkillSpeedRecovery(on, chainShot: true), Is.EqualTo(on.ChainSpeedRecoveryQuick));
            Assert.That(OffTheMark.SkillSpeedRecovery(on, chainShot: false), Is.EqualTo(on.SkillSpeedRecovery), "only the chains' shots");
            Assert.That(OffTheMark.SkillSpeedRecovery(off, chainShot: true), Is.EqualTo(off.SkillSpeedRecovery));
            Assert.That(on.RunSpeed, Is.EqualTo(off.RunSpeed), "her top speed as it was");
            Assert.That(on.MovingShotSlowdown, Is.EqualTo(off.MovingShotSlowdown), "and how she slows while a shot goes off");
        }

        static IEnumerable<TestCaseData> AfterShots()
        {
            yield return new TestCaseData("basic shot, aim ahead", 0.248f, 0.156f);
            yield return new TestCaseData("basic shot, aim behind", 0.598f, 0.406f);
            yield return new TestCaseData("Triple Tap, aim behind", 0.528f, 0.336f);
            yield return new TestCaseData("Backstep's shot, from a stand", 0.201f, 0.133f);
            yield return new TestCaseData("Backstep's shot, pressed at a run", 0.143f, 0.094f);
        }

        static float SecondsTo90(string what, FeelValues f)
        {
            switch (what)
            {
                case "basic shot, aim ahead": return OffTheMark.ShotOnTheMove(f);
                case "basic shot, aim behind": return OffTheMark.Backpedal(f, f.BasicShotStartup, basicShot: true);
                case "Triple Tap, aim behind": return OffTheMark.Backpedal(f, Skill("Triple Tap").Startup, basicShot: false);
                case "Backstep's shot, from a stand": return OffTheMark.Rise(f, 0f, chainShot: true);
                default: // pressed at full run: the slide has left this share when the shot goes off and lets her go
                    return OffTheMark.Rise(f, SkillSlide.SlideShare(1f, Skill("Backstep").Startup, f.SkillSlowdown), chainShot: true);
            }
        }

        [TestCaseSource(nameof(AfterShots))]
        public void FromTheShotGoingOff_To90PercentOfRunSpeed_IsAboutAThirdQuicker(string what, float before, float after)
        {
            float old = SecondsTo90(what, AsTheAssetHasIt(false)), quick = SecondsTo90(what, AsTheAssetHasIt(true));
            TestContext.WriteLine($"{what}: {old:0.000} s before, {quick:0.000} s after ({quick / old:0.00} of it)");
            Assert.That(old, Is.EqualTo(before).Within(0.002f), "before");
            Assert.That(quick, Is.EqualTo(after).Within(0.002f), "after");
            Assert.That(quick / old, Is.InRange(0.6f, 0.72f), "about a third quicker");
        }

        [Test]
        public void TheOtherSkillsRise_AndTheLightSkillsAimedAhead_AreAsTheyWere()
        {
            Assert.That(OffTheMark.Rise(AsTheAssetHasIt(true), 0f, chainShot: false), Is.EqualTo(OffTheMark.Rise(AsTheAssetHasIt(false), 0f, chainShot: false)));
        }
    }
}
