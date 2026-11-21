using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 071 (Kristian, 2026-11-21: "så må vi reworke focus, det føles ræva ... føler man må få mere attackdmg,
    /// attackspeed, cooldown osv."), the gains of spent focus as rules: each gain from its value, in each of the three ways
    /// of the gauge and with the chest's stats; nothing gained outside its seconds; the cooldown floor; the cards' numbers;
    /// and with the gains set to none (1, 1, 1, 0) focus exactly as before. Then how the gains are felt
    /// (<see cref="FocusFeelRules"/>): the phases, the flicker, the ready glow weaker than the aura, the edge, the sounds.
    /// </summary>
    public class FocusGainsTests
    {
        static readonly FocusModeValues Ways = new FocusModeValues();
        static readonly FeelValues Feel = new FeelValues();

        static IdentityValues None() => new IdentityValues { EmpoweredDamageScale = 1f, EmpoweredFireRate = 1f, EmpoweredCooldownRate = 1f, EmpoweredMoveShare = 0f };

        static RunModifiers StatsOn()
        {
            var m = new RunModifiers { StatsAndTiers = true, CooldownFloor = new RunValues().CooldownFloor };
            return m;
        }

        // ---------------- The values ----------------

        [Test]
        public void TheGains_AreTheOrdersFirstGuesses_AndTheOldMultiplierStaysOne()
        {
            var v = new IdentityValues();
            Assert.That(v.EmpoweredDamage, Is.EqualTo(1f), "the asset's number, left at 1");
            Assert.That(v.EmpoweredDamageScale, Is.EqualTo(1.35f), "+35 % damage");
            Assert.That(v.EmpoweredFireRate, Is.EqualTo(1.4f), "40 % faster shots");
            Assert.That(v.EmpoweredCooldownRate, Is.EqualTo(1.6f), "cooldowns 60 % faster");
            Assert.That(v.EmpoweredMoveShare, Is.EqualTo(0.1f), "10 % faster run");
            Assert.That(v.EmpoweredDuration, Is.EqualTo(6f), "the window stays 6 s");
            Assert.That(v.CooldownCut, Is.EqualTo(0.4f), "the cut at the moment of spending stays");
            Assert.That(Ways.PartBoostSeconds, Is.EqualTo(2f), "a Hair Trigger part: 6 s over three parts");
            Assert.That(FocusGains.IsNone(v), Is.False);
            Assert.That(FocusGains.IsNone(None()), Is.True);
        }

        // ---------------- Damage ----------------

        [Test]
        public void TodaysWindow_DealsThirtyFivePercentMore_ForItsSixSeconds_AndNothingAfter()
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(v);
            Assert.That(gauge.Scale(100, 0f, v), Is.EqualTo(100), "before");
            Assert.That(gauge.TryActivate(10f, v), Is.True);
            Assert.That(gauge.IsBoosted(10f), Is.True);
            Assert.That(gauge.Scale(100, 10f, v), Is.EqualTo(135));
            Assert.That(gauge.Scale(40, 15.9f, v), Is.EqualTo(54), "the basic shot's 40");
            Assert.That(gauge.BoostRemaining(12f), Is.EqualTo(4f).Within(1e-4f));
            Assert.That(gauge.BoostSeconds, Is.EqualTo(6f).Within(1e-4f));
            Assert.That(gauge.IsBoosted(16.01f), Is.False);
            Assert.That(gauge.Scale(100, 16.01f, v), Is.EqualTo(100), "after");
        }

        [Test]
        public void HairTrigger_APartGivesTheGainsForTwoSeconds_ThreeAtOnceForSix_AndOpensNoWindow()
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(v);
            Assert.That(gauge.TrySpendPart(v, 3, 1f, FocusGains.PartSeconds(Ways)), Is.True);
            Assert.That(gauge.IsEmpowered(1f), Is.False, "no window: basic shots do not mark, and focus keeps filling");
            Assert.That(gauge.IsBoosted(1f), Is.True);
            Assert.That(gauge.Scale(100, 2.9f, v), Is.EqualTo(135));
            Assert.That(gauge.Scale(100, 3.01f, v), Is.EqualTo(100), "two seconds");
            Assert.That(gauge.Add(5f, v, 2f), Is.EqualTo(5f), "focus keeps filling in a part's seconds, as before: no window");
            gauge.Fill(v);

            Assert.That(gauge.TrySpendPart(v, 3, 10f, 2f), Is.True);
            Assert.That(gauge.TrySpendPart(v, 3, 10.5f, 2f), Is.True);
            Assert.That(gauge.BoostRemaining(10.5f), Is.EqualTo(3.5f).Within(1e-4f), "a second part adds to what is left");
            Assert.That(gauge.BoostSeconds, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(gauge.TrySpendPart(v, 3, 11f, 2f), Is.True, "the third");
            Assert.That(gauge.BoostRemaining(11f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(gauge.TrySpendPart(v, 3, 11f, 2f), Is.False, "the gauge is empty");
            Assert.That(gauge.BoostRemaining(11f), Is.EqualTo(5f).Within(1e-4f), "a refused press adds nothing");

            var all = new FocusGauge();
            all.Fill(v);
            for (int i = 0; i < 3; i++) all.TrySpendPart(v, 3, 0f, 2f);
            Assert.That(all.BoostRemaining(0f), Is.EqualTo(6f).Within(1e-4f), "a full gauge part by part: the window's six seconds");

            var none = new FocusGauge();
            none.Fill(v);
            none.TrySpendPart(v, 3);
            Assert.That(none.IsBoosted(0f), Is.False, "the old call gives no gains");
            none.TrySpendPart(v, 3, 0f, 0f);
            Assert.That(none.IsBoosted(0f), Is.False, "nor a part of no seconds");
        }

        [Test]
        public void SteadyHand_TheGainsRunItsTenSeconds_AndLongFocus_LengthensEveryWay()
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(v);
            Assert.That(gauge.TryActivate(0f, v, Ways.WindowSeconds * 1.5f), Is.True, "Long focus at its most: +50 %");
            Assert.That(gauge.IsBoosted(14.9f), Is.True);
            Assert.That(gauge.IsBoosted(15.01f), Is.False, "fifteen seconds");
            Assert.That(gauge.IsEmpowered(14.9f), Is.True, "with the window: basic shots mark, as before");
            Assert.That(FocusGains.PartSeconds(Ways, 1.5f), Is.EqualTo(3f).Within(1e-5f), "a part's two seconds, half again");
            Assert.That(FocusGains.PartSeconds(Ways), Is.EqualTo(2f));
        }

        [Test]
        public void TheDamage_MultipliesWithTheRunsDamageAndTheWindowsOwn()
        {
            var v = new IdentityValues { EmpoweredDamage = 1.2f };
            var gauge = new FocusGauge();
            gauge.Fill(v);
            gauge.TryActivate(0f, v);
            Assert.That(gauge.DamageMultiplier(1f, v), Is.EqualTo(1.2f * 1.35f).Within(1e-5f), "the two multiply");
            RunModifiers m = StatsOn();
            m.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.1f });
            int dealt = m.ScaleDamage(100); // the run's share first, as Deal does
            Assert.That(dealt, Is.EqualTo(110));
            Assert.That(new FocusGauge().Scale(dealt, 1f, v), Is.EqualTo(110), "no gains outside the window");
            Assert.That(gauge.Scale(dealt, 1f, v), Is.EqualTo((int)Math.Round(110 * 1.2f * 1.35f)));
        }

        // ---------------- Attack speed ----------------

        [Test]
        public void TheBasicShot_FiresFortyPercentFaster_OnlyItsRecoveryCut_AndNeverUnderTheLeast()
        {
            var v = new IdentityValues();
            float s = Feel.BasicShotStartup, a = Feel.BasicShotActive, r = Feel.BasicShotRecovery; // 0.08, 0.02, 0.125: 0.225 s
            float rate = FocusGains.FireRate(true, v);
            Assert.That(rate, Is.EqualTo(1.4f));
            Assert.That(FocusGains.FireRate(false, v), Is.EqualTo(1f), "outside the gains");
            float cut = FocusGains.Recovery(s, a, r, rate, v.EmpoweredLeastRecovery);
            Assert.That(s + a + cut, Is.EqualTo(0.225f / 1.4f).Within(1e-5f), "the interval over the rate: 0.161 s");
            Assert.That(FocusGains.Recovery(s, a, r, 1f, v.EmpoweredLeastRecovery), Is.EqualTo(r), "a rate of 1: as it is");

            // With her stats (work order 050): the start's 0.85, then the gains on top: they multiply.
            RunModifiers m = StatsOn();
            float stat = m.Stats.BasicShotRecovery(s, a, r);
            Assert.That(s + a + stat, Is.EqualTo(0.225f / 0.85f).Within(1e-5f), "0.265 s at a run's start");
            Assert.That(s + a + FocusGains.Recovery(s, a, stat, rate, v.EmpoweredLeastRecovery), Is.EqualTo(0.225f / 0.85f / 1.4f).Within(1e-5f), "0.189 s: 5.3 shots a second for 3.8");
            // At the attack speed cap (1.3) the gains would cut the recovery to 0.024 s: it stays at 0.05 s, a shot's pose still seen.
            for (int i = 0; i < 10; i++) m.Take(new Upgrade { Kind = UpgradeKind.AttackSpeed, Amount = 0.06f });
            Assert.That(m.Stats.AttackSpeedAtMost, Is.True);
            float atCap = m.Stats.BasicShotRecovery(s, a, r);
            float boosted = FocusGains.Recovery(s, a, atCap, rate, v.EmpoweredLeastRecovery);
            Assert.That(boosted, Is.EqualTo(0.05f).Within(1e-5f));
            Assert.That(FocusGains.Recovery(s, a, 0.03f, rate, 0.05f), Is.EqualTo(0.03f), "a recovery shorter than the least already is left alone");
        }

        // ---------------- Cooldowns ----------------

        [Test]
        public void ACooldown_RunsSixtyPercentFaster_BesideTheCutAtTheMomentOfSpending()
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            var cooldown = new Cooldown();
            cooldown.Start(0f, 10f);
            gauge.Fill(v);
            Assert.That(gauge.TryActivate(2f, v), Is.True);
            Assert.That(cooldown.Cut(2f, v.CooldownCut), Is.EqualTo(3.2f).Within(1e-4f), "the cut stays: 40 % of the 8 s left");
            float rate = FocusGains.CooldownRate(gauge.IsBoosted(2f), v, 1f, 0.5f);
            Assert.That(rate, Is.EqualTo(1.6f));
            // Frames of a sixtieth, as the character ticks them: what the gains ran, times the rate less one, comes off.
            float t = 2f, last = 2f, readyAt = -1f;
            while (t < 9f && readyAt < 0f)
            {
                t += 1f / 60f;
                float extra = FocusGains.ExtraCooldownSeconds(gauge.BoostedSeconds(last, t), FocusGains.CooldownRate(true, v, 1f, 0.5f));
                cooldown.CutSeconds(t, extra);
                last = t;
                if (cooldown.IsReady(t)) readyAt = t;
            }
            Assert.That(readyAt, Is.EqualTo(2f + 4.8f / 1.6f).Within(0.02f), "the 4.8 s left take 3 s");
            Assert.That(gauge.BoostedSeconds(1f, 3f), Is.EqualTo(1f).Within(1e-5f), "only the seconds inside");
            Assert.That(gauge.BoostedSeconds(7f, 9f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(gauge.BoostedSeconds(9f, 10f), Is.EqualTo(0f), "nothing after");
            Assert.That(FocusGains.CooldownRate(false, v, 1f, 0.5f), Is.EqualTo(1f), "outside the gains");
        }

        [Test]
        public void TheFloor_Holds_NoCooldownTakesLessThanHalfItsOwn()
        {
            var v = new IdentityValues();
            float floor = new RunValues().CooldownFloor;
            Assert.That(floor, Is.EqualTo(0.5f));
            Assert.That(FocusGains.CooldownRate(true, v, 1f, floor), Is.EqualTo(1.6f), "no cuts: the gains' rate");
            Assert.That(FocusGains.CooldownRate(true, v, 0.8f, floor), Is.EqualTo(1.6f).Within(1e-5f), "Cooldowns at its most (-20 %): just at the floor");
            Assert.That(FocusGains.CooldownRate(true, v, 0.7f, floor), Is.EqualTo(1.4f).Within(1e-5f), "past it: slowed to the floor");
            Assert.That(FocusGains.CooldownRate(true, v, 0.5f, floor), Is.EqualTo(1f), "at the floor: no faster");
            for (float share = 0.5f; share <= 1.0001f; share += 0.05f)
            {
                float seconds = 10f * share / FocusGains.CooldownRate(true, v, share, floor);
                Assert.That(seconds, Is.GreaterThanOrEqualTo(10f * floor - 1e-4f), "share " + share);
            }
            // Her stats: a skill's own cut and every skill's, as the run reads them (HerStats.CooldownShare).
            RunModifiers m = StatsOn();
            m.Take(new Upgrade { Kind = UpgradeKind.AllCooldowns, Amount = 0.2f });
            m.Take(new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.2f, Skill = "Scatter Blast" });
            Assert.That(FocusGains.CooldownRate(true, v, m.CooldownShare("Quick Shot"), m.CooldownFloor), Is.EqualTo(1.6f).Within(1e-5f));
            Assert.That(FocusGains.CooldownRate(true, v, m.CooldownShare("Scatter Blast"), m.CooldownFloor), Is.EqualTo(1.2f).Within(1e-5f), "0.6 over 0.5");
            Assert.That(FocusGains.ExtraCooldownSeconds(0.5f, 1.6f), Is.EqualTo(0.3f).Within(1e-6f));
            Assert.That(FocusGains.ExtraCooldownSeconds(0.5f, 1f), Is.EqualTo(0f));
        }

        [Test]
        public void TheDash_ItsCooldownCut_BySeconds_AsASkillsIs()
        {
            var dash = new Dash();
            Assert.That(dash.TryStart(0f, Feel), Is.True);
            Assert.That(dash.CooldownRemaining(1f), Is.EqualTo(Feel.DashCooldown - 1f).Within(1e-4f));
            Assert.That(dash.CutCooldownSeconds(1f, 2f), Is.EqualTo(2f).Within(1e-5f));
            Assert.That(dash.CooldownRemaining(1f), Is.EqualTo(Feel.DashCooldown - 3f).Within(1e-4f));
            Assert.That(dash.CutCooldownSeconds(1f, 100f), Is.EqualTo(Feel.DashCooldown - 3f).Within(1e-4f), "no more than it has left");
            Assert.That(dash.CanStart(1f), Is.True);
            Assert.That(dash.CutCooldownSeconds(1f, 1f), Is.EqualTo(0f), "ready: nothing to cut");
            dash.MaxCharges = 2;
            dash.TryStart(5f, Feel);
            Assert.That(dash.CutCooldownSeconds(5f, 1f), Is.EqualTo(1f).Within(1e-5f), "with charges, the recharge running");
        }

        // ---------------- Move speed ----------------

        [Test]
        public void SheRunsTenPercentFaster_OnHerMoveSpeed()
        {
            var v = new IdentityValues();
            Assert.That(FocusGains.MoveScale(true, v), Is.EqualTo(1.1f).Within(1e-6f));
            Assert.That(FocusGains.MoveScale(false, v), Is.EqualTo(1f));
            RunModifiers m = StatsOn();
            m.Take(new Upgrade { Kind = UpgradeKind.RunSpeed, Amount = 0.05f });
            Assert.That(m.RunSpeedShare * FocusGains.MoveScale(true, v), Is.EqualTo(1.05f * 1.1f).Within(1e-5f), "they multiply");
        }

        // ---------------- None: focus as before ----------------

        [Test]
        public void WithTheGainsSetToNone_FocusIsExactlyAsBefore()
        {
            IdentityValues v = None();
            var gauge = new FocusGauge();
            gauge.Fill(v);
            gauge.TryActivate(0f, v);
            Assert.That(gauge.Scale(100, 1f, v), Is.EqualTo(100), "no damage more (decision 0005's 1)");
            Assert.That(FocusGains.FireRate(gauge.IsBoosted(1f), v), Is.EqualTo(1f));
            Assert.That(FocusGains.Recovery(0.08f, 0.02f, 0.125f, FocusGains.FireRate(true, v), v.EmpoweredLeastRecovery), Is.EqualTo(0.125f));
            Assert.That(FocusGains.CooldownRate(true, v, 1f, 0.5f), Is.EqualTo(1f));
            Assert.That(FocusGains.MoveScale(true, v), Is.EqualTo(1f));
            Assert.That(gauge.IsEmpowered(5.9f), Is.True, "the window as it was");
            Assert.That(gauge.IsEmpowered(6.01f), Is.False);
            string[] today = FocusModeRules.Lines(FocusMode.Today, Ways, 0.4f, v);
            Assert.That(today, Is.EqualTo(new[] { "A full gauge: Z cuts every running cooldown.", "For a while every basic shot that hits marks.", "" }), "the old words");
            Assert.That(FocusModeRules.Lines(FocusMode.HairTrigger, Ways, 0.4f, v)[1], Is.EqualTo("Each part cuts every running cooldown by 15% of what is left."));
            Assert.That(FocusModeRules.Lines(FocusMode.SteadyHand, Ways, 0.4f, v)[1], Is.EqualTo("For 10 s every basic shot that hits marks."));
            var c = new OfferContext { Identity = v };
            Assert.That(OfferTexts.ForFocus(FocusMode.HairTrigger, c).Numbers.Count, Is.EqualTo(3), "Hair Trigger's card as before");
            Assert.That(OfferTexts.ForFocus(FocusMode.Today, c).Sentence, Is.EqualTo("A full gauge: Z cuts every running cooldown, and basic shots mark for a while."));
        }

        [Test]
        public void EndingTheWindow_OrAClearGauge_EndsTheGains()
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(v);
            gauge.TryActivate(0f, v);
            gauge.EndWindow();
            Assert.That(gauge.IsBoosted(1f), Is.False, "the focus choice taken ends them (work order 033's rule)");
            gauge.Fill(v);
            gauge.TrySpendPart(v, 3, 0f, 2f);
            gauge.Clear();
            Assert.That(gauge.IsBoosted(0.5f), Is.False, "a run's clean slate");
            Assert.That(gauge.BoostSeconds, Is.EqualTo(0f));
        }

        // ---------------- The words ----------------

        [Test]
        public void TheCardsAndTheChoice_SayTheGains_FromHerValues()
        {
            var v = new IdentityValues();
            Assert.That(FocusGains.Words(v), Is.EqualTo("Powered up: +35% damage, 40% faster shots, cooldowns 60% faster, 10% faster run."));
            var faster = new IdentityValues { EmpoweredFireRate = 1.5f };
            StringAssert.Contains("50% faster shots", FocusGains.Words(faster), "read from the values, not written by hand");
            foreach (FocusMode mode in new[] { FocusMode.Today, FocusMode.HairTrigger, FocusMode.SteadyHand })
            {
                string[] lines = FocusModeRules.Lines(mode, Ways, 0.4f, v);
                Assert.That(lines.Length, Is.EqualTo(3));
                Assert.That(lines.All(l => l.Length > 0 && l.Length <= 90), Is.True, string.Join(" / ", lines));
            }
            StringAssert.Contains("powers you up for 2 s", FocusModeRules.Lines(FocusMode.HairTrigger, Ways, 0.4f, v)[1]);
            StringAssert.Contains("15%", FocusModeRules.Lines(FocusMode.HairTrigger, Ways, 0.4f, v)[1]);
            StringAssert.Contains("powered up", FocusModeRules.Lines(FocusMode.SteadyHand, Ways, 0.4f, v)[1]);

            var c = new OfferContext();
            OfferCard hair = OfferTexts.ForFocus(FocusMode.HairTrigger, c);
            Assert.That(hair.Numbers.Last(), Is.EqualTo("powered up for  6 s" + OfferTexts.Arrow + "2 s a part"));
            c.Modifiers = StatsOn();
            c.Modifiers.Take(new Upgrade { Kind = UpgradeKind.LongFocus, Amount = 0.5f });
            Assert.That(OfferTexts.ForFocus(FocusMode.HairTrigger, c).Numbers.Last(), Is.EqualTo("powered up for  9 s" + OfferTexts.Arrow + "3 s a part"), "Long focus in both");
            OfferCard today = OfferTexts.ForFocus(FocusMode.Today, new OfferContext());
            Assert.That(today.Sentence.Length, Is.InRange(15, 80));
            Assert.That(today.Numbers.Single(), Is.EqualTo(FocusGains.Words(v)));
        }

        // ---------------- How it is felt ----------------

        [Test]
        public void ThePhases_ReadyRunningEnding_AndTheFlicker()
        {
            var f = new FocusFeelValues();
            Assert.That(FocusFeelRules.Phase(false, false, 0f, f), Is.EqualTo(FocusFeelPhase.None));
            Assert.That(FocusFeelRules.Phase(true, false, 0f, f), Is.EqualTo(FocusFeelPhase.Ready));
            Assert.That(FocusFeelRules.Phase(true, true, 4f, f), Is.EqualTo(FocusFeelPhase.Running), "the gains over ready (Hair Trigger with parts left)");
            Assert.That(FocusFeelRules.Phase(false, true, 1.5f, f), Is.EqualTo(FocusFeelPhase.Running));
            Assert.That(FocusFeelRules.Phase(false, true, 0.9f, f), Is.EqualTo(FocusFeelPhase.Ending), "the last second");
            Assert.That(FocusFeelRules.AuraLevel(FocusFeelPhase.Running, 3f, f), Is.EqualTo(1f));
            Assert.That(FocusFeelRules.AuraLevel(FocusFeelPhase.None, 0f, f), Is.EqualTo(0f));
            int on = 0, off = 0, flips = 0;
            bool last = FocusFeelRules.FlickerOn(1f, f);
            for (float r = 1f; r > 0f; r -= 0.001f)
            {
                float level = FocusFeelRules.AuraLevel(FocusFeelPhase.Ending, r, f);
                if (level >= 1f) on++;
                else
                {
                    off++;
                    Assert.That(level, Is.EqualTo(f.FlickerLow));
                }
                bool now = FocusFeelRules.FlickerOn(r, f);
                if (now != last) flips++;
                last = now;
            }
            Assert.That(on, Is.EqualTo(off).Within(20), "half on, half off");
            Assert.That(flips, Is.InRange(15, 17), "eight times a second, on and off");
            Assert.That(FocusFeelRules.FlickerOn(0.01f, f), Is.False, "it ends on an off");
        }

        [Test]
        public void TheReadyGlow_PulsesSlowly_AndIsAlwaysWeakerThanTheAura()
        {
            var f = new FocusFeelValues();
            Assert.That(FocusFeelRules.ReadyLevel(0f, f), Is.EqualTo(1f).Within(1e-5f), "brightest when it comes");
            Assert.That(FocusFeelRules.ReadyLevel(f.ReadyPulseSeconds * 0.5f, f), Is.EqualTo(f.ReadyLeast).Within(1e-5f));
            float brightestReady = 0f;
            for (float t = 0f; t < 5f; t += 0.01f)
                brightestReady = Math.Max(brightestReady, FocusFeelRules.LightIntensity(FocusFeelPhase.Ready, FocusFeelRules.ReadyLevel(t, f), 99f, 1f, f));
            float aura = FocusFeelRules.LightIntensity(FocusFeelPhase.Running, 1f, 99f, 1f, f);
            Assert.That(brightestReady, Is.EqualTo(f.ReadyLight).Within(1e-4f));
            Assert.That(aura, Is.EqualTo(f.AuraLight));
            Assert.That(brightestReady, Is.LessThan(aura * 0.5f), "the two told apart");
            Assert.That(FocusFeelRules.LightIntensity(FocusFeelPhase.None, 0f, 99f, 1f, f), Is.EqualTo(0f));
        }

        [Test]
        public void TheBurst_PeaksAtTheMomentOfSpending_AndFadesInItsSeconds_APartsSmaller()
        {
            var f = new FocusFeelValues();
            Assert.That(FocusFeelRules.BurstLight(0f, 1f, f), Is.EqualTo(f.BurstLight));
            Assert.That(FocusFeelRules.BurstLight(f.BurstLightSeconds * 0.5f, 1f, f), Is.EqualTo(f.BurstLight * 0.25f).Within(1e-4f));
            Assert.That(FocusFeelRules.BurstLight(f.BurstLightSeconds, 1f, f), Is.EqualTo(0f));
            Assert.That(FocusFeelRules.BurstLight(0f, f.PartShare, f), Is.EqualTo(f.BurstLight * f.PartShare).Within(1e-4f));
            Assert.That(FocusFeelRules.LightIntensity(FocusFeelPhase.Running, 1f, 0f, 1f, f), Is.EqualTo(f.BurstLight), "the burst over the aura");
            Assert.That(f.BurstLight, Is.GreaterThan(f.AuraLight * 3f), "a flash that cannot be missed");
            Assert.That(f.BurstFreeze, Is.InRange(0.01f, 0.1f), "a few hundredths, under the recast clock's most a frame (0.1 s)");
            Assert.That(f.PartFreeze, Is.LessThan(f.BurstFreeze));
        }

        [Test]
        public void TheEdge_ShowsOnlyWhileTheGainsRun_AtItsStrength_AndItsDepth()
        {
            var f = new FocusFeelValues();
            Assert.That(FocusFeelRules.EdgeAlpha(FocusFeelPhase.Running, 1f, f), Is.EqualTo(f.EdgeStrength));
            Assert.That(FocusFeelRules.EdgeAlpha(FocusFeelPhase.Ending, f.FlickerLow, f), Is.EqualTo(f.EdgeStrength * f.FlickerLow).Within(1e-6f));
            Assert.That(FocusFeelRules.EdgeAlpha(FocusFeelPhase.Ready, 1f, f), Is.EqualTo(0f), "not for ready");
            Assert.That(FocusFeelRules.EdgeAlpha(FocusFeelPhase.None, 1f, f), Is.EqualTo(0f));
            Assert.That(FocusFeelRules.EdgeDepth(1080f, f), Is.EqualTo(75.6f).Within(1e-3f), "75.6 px at 1920 by 1080");
            Assert.That(FocusFeelRules.EdgeDepth(720f, f), Is.EqualTo(50.4f).Within(1e-3f), "50.4 px at 1280 by 720");
            f.On = false;
            Assert.That(FocusFeelRules.EdgeAlpha(FocusFeelPhase.Running, 1f, f), Is.EqualTo(0f), "off: none");
        }

        [Test]
        public void APartThatFills_HasItsChime_ButNotTheFullGauge_WhichHasThePlaceholdersOwn()
        {
            Assert.That(FocusFeelRules.PartReadySound(true, 0, 1, false), Is.True);
            Assert.That(FocusFeelRules.PartReadySound(true, 1, 2, false), Is.True);
            Assert.That(FocusFeelRules.PartReadySound(true, 2, 3, true), Is.False, "the full gauge's chime is the placeholder sounds'");
            Assert.That(FocusFeelRules.PartReadySound(true, 2, 1, false), Is.False, "a part spent");
            Assert.That(FocusFeelRules.PartReadySound(false, 0, 1, true), Is.False, "today's gauge: the full chime");
        }

        [Test]
        public void ItsThreeSounds_AreTheirLength_FromSilenceToSilence_AndAllDifferent()
        {
            SynthRecipe[] recipes = { FocusFeelRules.SpendRecipe(), FocusFeelRules.EndingRecipe(), FocusFeelRules.ReadyRecipe() };
            float[] lengths = { 0.7f, 0.6f, 0.25f };
            var made = recipes.Select(r => Synth.Render(r)).ToList();
            for (int i = 0; i < made.Count; i++)
            {
                float[] s = made[i];
                Assert.That(s.Length, Is.EqualTo((int)Math.Round(lengths[i] * Synth.SampleRate)));
                Assert.That(s.Any(x => float.IsNaN(x) || float.IsInfinity(x)), Is.False);
                float peak = s.Max(x => Math.Abs(x));
                Assert.That(peak, Is.GreaterThan(0.3f).And.LessThanOrEqualTo(0.9f + 1e-4f));
                Assert.That(s[0], Is.EqualTo(0f));
                Assert.That(s[s.Length - 1], Is.EqualTo(0f));
            }
            Assert.That(made[0].SequenceEqual(made[1]) || made[1].SequenceEqual(made[2]), Is.False);
            float[] full = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.FocusFull));
            Assert.That(made[2].Length == full.Length && made[2].SequenceEqual(full), Is.False, "the part's chime is not the full gauge's");
            int n = full.Length / 4; // the same stretch of each, from its start
            Assert.That(Crossings(made[0], 0, n), Is.LessThan(Crossings(full, 0, n) / 2), "the spending sound is much lower than the full chime");
        }

        static int Crossings(float[] s, int from, int to)
        {
            int n = 0;
            for (int i = Math.Max(1, from); i < Math.Min(to, s.Length); i++) if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }
    }
}
