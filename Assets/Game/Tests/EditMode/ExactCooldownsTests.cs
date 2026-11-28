using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 154 (the tests' health, 2026-11-28), its number 1: FocusFeltTests' "what was left, 1.6 times as fast" measured
    /// 0.84 to 0.88 s for the 1.03 s of the rule in every full PlayMode run, and passed alone. The cause, proven here with the
    /// game's clock as Unity keeps it (a double, read as a float: <c>Time.time</c>): while the gains run, every running cooldown is
    /// cut each frame by the frame's seconds times 0.6, and the moment it is ready again was a float of that clock, held to the
    /// nearest 0.12 ms after 17 minutes, 0.24 ms after 34 and 0.49 ms after 68. A cut smaller than that step was rounded to a
    /// whole step every frame: with the batch run's frames of 0.3 ms late in a full run the faster cooldown ended after 0.825 s;
    /// with frames of 1 ms after 1.092 s. At 30, 60 and 144 frames a second, and at 0.2 and 0.33 s a frame, it was right to the
    /// frame for 34 minutes, and a frame or two late after (never more than 0.05 s). <see cref="ExactCooldowns"/> (on) keeps the
    /// moment exact: every frame length, at every hour of a session, gives
    /// the rule's number to the frame. Off: today's numbers, bit for bit (a float model of the cooldown as it was, here).
    ///
    /// Frames as the character ticks them (<c>PlayerCharacter.TickFocusGains</c>): the seconds the gains ran since the last frame,
    /// times the rate less one, come off what is left; the spend in a frame after its tick.
    /// </summary>
    public class ExactCooldownsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The frame steps of the order (1/30, 1/60, 1/144, 0.2 and 0.33 s), and the batch run's 0.3 ms and an uncapped 1 ms.</summary>
        static readonly double[] Steps = { 1.0 / 30.0, 1.0 / 60.0, 1.0 / 144.0, 0.2, 0.33, 0.001, 0.0003 };
        /// <summary>The clock's age at the spend, s: a fresh start, 17 minutes, 34 minutes, 83 minutes (a full PlayMode run's late classes), three hours.</summary>
        static readonly double[] Clocks = { 10.0, 1000.0, 2100.0, 5000.0, 10800.0 };

        const float QuickShotsLeft = 2.75f;  // s: Quick Shot's 3 s, a quarter of a second after the press (FocusFeltTests')
        const float CutAtTheSpend = 0.4f;    // IdentityValues.CooldownCut
        const float Rate = 1.6f;             // IdentityValues.EmpoweredCooldownRate

        [TearDown]
        public void PutTheSwitchBack() => ExactCooldowns.On = true;

        /// <summary>Unity's Time.time: the clock is a double, read as a float.</summary>
        static float Now(double clock) => (float)clock;

        /// <summary>The cooldown as it was before work order 154, one charge: its ready moment a float.</summary>
        sealed class TodaysCooldown
        {
            float _readyAt = float.NegativeInfinity;
            public void Start(float now, float seconds) => _readyAt = now + seconds;
            public float Remaining(float now) => now >= _readyAt ? 0f : _readyAt - now;
            public float Cut(float now, float share)
            {
                float left = Remaining(now);
                if (left <= 0f) return 0f;
                float cut = left * Math.Max(0f, Math.Min(1f, share));
                _readyAt -= cut;
                return cut;
            }
            public float CutSeconds(float now, float seconds)
            {
                float left = Remaining(now);
                if (left <= 0f || seconds <= 0f) return 0f;
                float cut = Math.Min(left, seconds);
                _readyAt -= cut;
                return cut;
            }
        }

        struct Ran
        {
            public float Took;        // s of game time from the spend to the first frame it is ready
            public float Designed;    // the same by the rule: the first frame at which what was left, run 1.6 times as fast, is gone
            public float WorstOff;    // s: the most what was left differed from the rule's at any frame
            public bool SameAsToday;  // every frame's Remaining equal to the float model's, bit for bit
        }

        /// <summary>
        /// Quick Shot's cooldown running, focus spent at <paramref name="clock"/> s, frames of <paramref name="step"/> s: the gains
        /// tick each frame, and the cooldown is read after it. With a <see cref="TodaysCooldown"/> beside it, fed the same.
        /// </summary>
        static Ran Run(double clock, double step, bool dash = false)
        {
            var v = new IdentityValues();
            var gauge = new FocusGauge();
            var cooldown = new Cooldown();
            var dashRules = new Dash();
            var today = new TodaysCooldown();
            var feel = new FeelValues { DashCooldown = 3f };
            double started = clock - (3.0 - QuickShotsLeft);
            if (dash) Assert.That(dashRules.TryStart(Now(started), feel), Is.True);
            else cooldown.Start(Now(started), 3f);
            today.Start(Now(started), 3f);
            float spentAt = Now(clock);
            gauge.Fill(v);
            Assert.That(gauge.TryActivate(spentAt, v), Is.True);
            float afterCut = dash ? (dashRules.CooldownRemaining(spentAt) - dashRules.CutCooldown(spentAt, CutAtTheSpend))
                                  : (cooldown.Remaining(spentAt) - cooldown.Cut(spentAt, CutAtTheSpend));
            today.Cut(spentAt, CutAtTheSpend);
            float rate = FocusGains.CooldownRate(true, v);
            Assert.That(rate, Is.EqualTo(Rate));
            var ran = new Ran { Took = -1f, Designed = -1f, SameAsToday = true };
            float last = spentAt;
            for (int k = 1; k < 200000 && ran.Took < 0f; k++)
            {
                float now = Now(clock + k * step);
                float extra = FocusGains.ExtraCooldownSeconds(gauge.BoostedSeconds(last, now), rate);
                last = now;
                if (extra > 0f)
                {
                    if (dash) dashRules.CutCooldownSeconds(now, extra);
                    else cooldown.CutSeconds(now, extra);
                    today.CutSeconds(now, extra);
                }
                float left = dash ? dashRules.CooldownRemaining(now) : cooldown.Remaining(now);
                if (left != today.Remaining(now)) ran.SameAsToday = false;
                float law = Math.Max(0f, afterCut - Rate * (now - spentAt));
                ran.WorstOff = Math.Max(ran.WorstOff, Math.Abs(left - law));
                if (ran.Designed < 0f && law <= 0f) ran.Designed = now - spentAt;
                if (left <= 0f) ran.Took = now - spentAt;
            }
            return ran;
        }

        static string Ms(double s) => (s * 1000.0).ToString("0.###", Inv);

        [Test]
        public void TheFasterCooldown_IsTheRulesAtEveryFrameStep_AndEveryHourOfASession_AndOffAsToday()
        {
            var table = new StringBuilder("[154] what was left of Quick Shot's cooldown after the spend (1.65 s), run 1.6 times as fast: s until ready, on / off (the rule's to the frame)\n");
            table.Append("[154]   clock at the spend |");
            foreach (double step in Steps) table.Append(" frame " + Ms(step) + " ms |");
            table.AppendLine();
            var offFaults = new List<string>();
            foreach (double clock in Clocks)
            {
                table.Append("[154]   " + clock.ToString("0", Inv) + " s |");
                foreach (double step in Steps)
                {
                    ExactCooldowns.On = true;
                    Ran on = Run(clock, step);
                    ExactCooldowns.On = false;
                    Ran off = Run(clock, step);
                    ExactCooldowns.On = true;
                    table.Append(" " + on.Took.ToString("0.000", Inv) + " / " + off.Took.ToString("0.000", Inv) + " (" + on.Designed.ToString("0.000", Inv) + ") |");
                    string at = "clock " + clock.ToString("0", Inv) + " s, frames of " + Ms(step) + " ms";
                    // On: the rule's number to the frame, and what is left the rule's at every frame within a tenth of a millisecond.
                    Assert.That(on.Took, Is.EqualTo(on.Designed), at + ": on, ready on the rule's frame");
                    Assert.That(on.WorstOff, Is.LessThan(1e-4f), at + ": on, what is left the rule's at every frame");
                    // Off: the cooldown exactly as it was, frame for frame.
                    Assert.That(off.SameAsToday, Is.True, at + ": off, every frame's Remaining as before work order 154, bit for bit");
                    if (Math.Abs(off.Took - off.Designed) > 0.05f) offFaults.Add(at + ": " + off.Took.ToString("0.000", Inv));
                }
                table.AppendLine();
            }
            TestContext.WriteLine(table.ToString());
            Console.WriteLine(table.ToString());
            // The fault the switch repairs, as FocusFeltTests met it late in a full run: frames of 0.3 ms at 83 minutes.
            ExactCooldowns.On = false;
            Ran was = Run(5000.0, 0.0003);
            ExactCooldowns.On = true;
            Assert.That(was.Took, Is.LessThan(0.9f), "off, late in a session with short frames: far sooner than the rule's 1.031 s (" + was.Took + ")");
            Assert.That(offFaults, Is.Not.Empty, "off, some frame length and hour give another number than the rule's");
            foreach (double clock in Clocks)
                foreach (double step in new[] { 1.0 / 30.0, 1.0 / 60.0, 1.0 / 144.0, 0.2, 0.33 })
                    Assert.That(offFaults.Exists(f => f.StartsWith("clock " + clock.ToString("0", Inv) + " s, frames of " + Ms(step) + " ms:", StringComparison.Ordinal)), Is.False,
                                "off, at the order's frame steps (30 to 144 a second, 0.2 and 0.33 s) the number was already the rule's: clock " + clock + ", frames of " + step);
        }

        [Test]
        public void TheDashsCooldown_TheSame_OnExact_OffAsToday()
        {
            foreach (double clock in Clocks)
                foreach (double step in Steps)
                {
                    string at = "the dash, clock " + clock.ToString("0", Inv) + " s, frames of " + Ms(step) + " ms";
                    ExactCooldowns.On = true;
                    Ran on = Run(clock, step, dash: true);
                    ExactCooldowns.On = false;
                    Ran off = Run(clock, step, dash: true);
                    ExactCooldowns.On = true;
                    Assert.That(on.Took, Is.EqualTo(on.Designed), at + ": on, ready on the rule's frame");
                    Assert.That(on.WorstOff, Is.LessThan(1e-4f), at + ": on, the rule's at every frame");
                    Assert.That(off.SameAsToday, Is.True, at + ": off, as before work order 154, bit for bit");
                }
        }

        [Test]
        public void ACooldownsOwnUses_AreAsBefore_WithTheSwitchOnOrOff_ChargesToo()
        {
            foreach (bool switchOn in new[] { true, false })
            {
                ExactCooldowns.On = switchOn;
                string on = switchOn ? "on" : "off";
                var c = new Cooldown { MaxCharges = 2 };
                c.Start(1f, 5f);
                c.Start(2f, 5f);
                Assert.That(c.IsReady(2f), Is.False, on + ": both charges spent");
                Assert.That(c.Remaining(2f), Is.EqualTo(4f), on + ": the first back 5 s after its use");
                Assert.That(c.Charges(6f), Is.EqualTo(1), on + ": one back at 6");
                Assert.That(c.NextCharge(6f), Is.EqualTo(5f), on + ": the next a whole cooldown later");
                Assert.That(c.Cut(6f, 0.4f), Is.EqualTo(2f), on + ": 40 % of the 5 s left");
                Assert.That(c.CutSeconds(6f, 1f), Is.EqualTo(1f), on);
                Assert.That(c.NextCharge(6f), Is.EqualTo(2f), on);
                c.MakeReady(6f);
                Assert.That(c.Charges(6f), Is.EqualTo(2), on + ": made ready");
                var d = new Dash();
                Assert.That(d.TryStart(1f, new FeelValues { DashCooldown = 1.5f }, 0.5f), Is.True, on);
                Assert.That(d.CooldownRemaining(1.25f), Is.EqualTo(0.5f), on + ": the dash's 0.75 s");
                Assert.That(d.CanStart(1.75f), Is.True, on);
            }
        }
    }
}
