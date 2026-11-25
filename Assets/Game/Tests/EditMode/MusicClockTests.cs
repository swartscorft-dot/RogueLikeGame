using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The music's clock alone (work order 116, a trial; Kristian, 2026-11-24: "Må høre det i spillet først"; <see cref="MusicClock"/>,
    /// <see cref="MusicConductor"/>, <see cref="MusicFade"/>): the next bar line from any moment, at 44.1 and 48 kHz; the least lead;
    /// a change cancelled when the state flips back before its line, and kept when the line is too close; the fades' levels over
    /// time; his two clips whole bars; ten minutes of the audio clock with no drift, the lines and every round of a loop on the
    /// sample; what the game's knowledge makes the wanted state; and the summary's line.
    /// </summary>
    public class MusicClockTests
    {
        static readonly int[] Rates = { 44100, 48000 };
        const long ExploreSamples = 661500;  // his Explore.wav: 15.000 s at 44.1 kHz (the producer's measure)
        const long FightSamples = 1323000;   // his Fight.wav: 30.000 s at 44.1 kHz
        const int ClipRate = 44100;

        static MusicValues V() => new MusicValues();

        // ---------------- The clock ----------------

        [Test]
        public void TheDefaults_Are128Bpm_FourBeatsABar_ABarIs1Point875Seconds_AndTheOrdersSwitchIsOn()
        {
            MusicValues v = V();
            Assert.That(v.On, Is.True, "the order's switch, on");
            Assert.That(v.Bpm, Is.EqualTo(128.0));
            Assert.That(v.BeatsABar, Is.EqualTo(4));
            Assert.That(MusicClock.BeatSeconds(v), Is.EqualTo(0.46875).Within(1e-12));
            Assert.That(MusicClock.BarSeconds(v), Is.EqualTo(1.875).Within(1e-12), "one bar: the longest she can wait for the fight music");
            Assert.That((v.CalmLevel, v.FightLevel), Is.EqualTo((0.22f, 0.38f)), "the producer's start: under her guns");
            Assert.That(v.CalmLevel < 1f && v.FightLevel < 1f && v.BossLevel < 1f, "under her basic shot always");
            Assert.That(v.VolumeStep, Is.EqualTo(SettingsRules.VolumeSteps), "100 % to start");
        }

        [Test]
        public void ABar_InSamples_Is90000At48k_And82687AndAHalfAt44k_RoundedOnceALine()
        {
            MusicValues v = V();
            Assert.That(MusicClock.BarsInSamples(v, 1, 48000), Is.EqualTo(90000));
            Assert.That(MusicClock.BarsInSamples(v, 2, 44100), Is.EqualTo(165375), "two bars: a whole number at 44.1 kHz");
            Assert.That(MusicClock.BarsInSamples(v, 1, 44100), Is.EqualTo(82688), "one: 82 687.5, rounded to the nearest");
            Assert.That(MusicClock.BarsInSamples(v, 3, 44100), Is.EqualTo(248063), "three: 248 062.5, rounded");
            Assert.That(MusicClock.BarsInSamples(v, 8, 44100), Is.EqualTo(ExploreSamples), "eight: his Explore");
        }

        [Test]
        public void TheNextBarLine_FromAnyMoment_IsTheFirstLineAtOrAfterIt()
        {
            MusicValues v = V();
            var random = new Random(116);
            foreach (int rate in Rates)
            {
                long origin = MusicClock.OriginAt(37.123456, rate);
                Assert.That(origin / (double)rate, Is.GreaterThanOrEqualTo(37.123456 - 1e-9), "the origin a whole sample at or after its start");
                Assert.That(origin / (double)rate - 37.123456, Is.LessThan(1.0 / rate), "and within a sample of it");
                for (int i = 0; i < 20000; i++)
                {
                    double t = origin / (double)rate + random.NextDouble() * 700.0;
                    long next = MusicClock.NextBar(v, origin, t, rate);
                    double line = MusicClock.LineTime(v, origin, next, rate);
                    Assert.That(line, Is.GreaterThanOrEqualTo(t - 1e-12), "at or after the moment");
                    Assert.That(MusicClock.LineTime(v, origin, next - 1, rate), Is.LessThan(t), "the line before it is before the moment");
                    Assert.That(line - t, Is.LessThanOrEqualTo(MusicClock.BarSeconds(v) + 1.0 / rate), "never more than a bar away");
                    long playing = MusicClock.BarAt(v, origin, t, rate);
                    Assert.That(MusicClock.LineTime(v, origin, playing, rate), Is.LessThanOrEqualTo(t), "the bar playing began at or before it");
                    Assert.That(next - playing, Is.InRange(0, 1));
                }
                // On a line exactly: that line.
                for (long k = 0; k < 400; k += 7)
                {
                    double line = MusicClock.LineTime(v, origin, k, rate);
                    Assert.That(MusicClock.NextBar(v, origin, line, rate), Is.EqualTo(k), "on line " + k + " at " + rate);
                    Assert.That(MusicClock.BarAt(v, origin, line, rate), Is.EqualTo(k));
                }
            }
        }

        // ---------------- His clips ----------------

        [Test]
        public void HisClips_AreWholeBars_ToTheSample_Explore8_Fight16_AndAClipASampleLongerIsNot()
        {
            MusicValues v = V();
            Assert.That(MusicClock.IsWholeBars(v, ExploreSamples, ClipRate, out long explore), Is.True);
            Assert.That(explore, Is.EqualTo(8));
            Assert.That(MusicClock.IsWholeBars(v, FightSamples, ClipRate, out long fight), Is.True);
            Assert.That(fight, Is.EqualTo(16));
            Assert.That(MusicClock.IsWholeBars(v, ExploreSamples + 1, ClipRate, out _), Is.False, "a sample longer: not whole bars");
            Assert.That(MusicClock.IsWholeBars(v, ExploreSamples - 1, ClipRate, out _), Is.False, "a sample shorter: not");
            Assert.That(MusicClock.IsWholeBars(v, 82688, ClipRate, out _), Is.False, "one bar at 44.1 kHz is not a whole number of samples");
        }

        // ---------------- Ten minutes, no drift ----------------

        [Test]
        public void TenMinutesOfTheAudioClock_EveryLineAndEveryRoundOfALoop_OnTheSample_NothingDrifts()
        {
            MusicValues v = V();
            foreach (int rate in Rates)
            {
                long origin = MusicClock.OriginAt(5.0, rate);
                long bars = (long)Math.Ceiling(600.0 / MusicClock.BarSeconds(v)); // 320 bars in ten minutes
                Assert.That(bars, Is.EqualTo(320));
                // Each line from the origin and its number alone: exactly the time of its bars, within half a sample, at every line.
                for (long k = 0; k <= bars; k++)
                {
                    double exact = k * MusicClock.BarSeconds(v) * rate;
                    long sample = MusicClock.LineSample(v, origin, k, rate) - origin;
                    Assert.That(Math.Abs(sample - exact), Is.LessThanOrEqualTo(0.5), "line " + k + " at " + rate);
                }
                Assert.That(MusicClock.LineSample(v, origin, bars, rate) - origin, Is.EqualTo((long)(600 * rate)), "bar 320 lands on 10:00.000 exactly");
                // A loop started on any line goes round on the engine's own loop: every round begins where its line is.
                foreach ((long samples, long loopBars) in new[] { (ExploreSamples, 8L), (FightSamples, 16L) })
                {
                    long outLoop = (long)Math.Round(samples * (double)rate / ClipRate); // the clip's length at the output rate
                    for (long start = 0; start < 16; start++)
                        for (long round = 0; start + round * loopBars <= bars; round++)
                        {
                            long wrap = MusicClock.LineSample(v, origin, start, rate) + round * outLoop;
                            long line = MusicClock.LineSample(v, origin, start + round * loopBars, rate);
                            Assert.That(wrap, Is.EqualTo(line), "a " + loopBars + "-bar loop from bar " + start + ", round " + round + ", at " + rate);
                        }
                }
            }
        }

        [Test]
        public void TheConductor_SteppedTenMinutesInRaggedFrames_LandsEveryChangeOnItsLine_InTime()
        {
            MusicValues v = V();
            foreach (int rate in Rates)
            {
                var c = new MusicConductor(v);
                var random = new Random(rate);
                double now = 12.345;
                MusicState want = MusicState.Calm;
                double nextFlip = 3.0;
                int landed = 0;
                double askedAt = double.NaN;
                while (now < 12.345 + 600.0)
                {
                    if (now >= nextFlip + 12.345)
                    {
                        want = want == MusicState.Calm ? MusicState.Fight : MusicState.Calm;
                        nextFlip += 2.0 + random.NextDouble() * 20.0;
                    }
                    MusicChange ch = c.Step(now, want, rate);
                    if (ch.Scheduled) askedAt = now;
                    if (ch.Landed)
                    {
                        landed++;
                        double line = c.LineTime(ch.LandedBar);
                        Assert.That(now - line, Is.InRange(0.0, 0.05), "seen within a frame of its line");
                        Assert.That(line - askedAt, Is.GreaterThanOrEqualTo(v.LeastLead - 1e-9), "never closer than the least lead to its ask");
                        Assert.That(line - askedAt, Is.LessThanOrEqualTo(MusicClock.BarSeconds(v) + v.LeastLead + 1e-9), "never more than a bar and the lead after it");
                        long fromOrigin = MusicClock.LineSample(v, c.Origin, ch.LandedBar, rate) - c.Origin;
                        Assert.That(Math.Abs(fromOrigin - ch.LandedBar * MusicClock.BarSeconds(v) * rate), Is.LessThanOrEqualTo(0.5), "on the sample of its bar");
                    }
                    now += 0.004 + random.NextDouble() * 0.03; // frames of 4 to 34 ms
                }
                Assert.That(landed, Is.GreaterThan(40), "many changes in ten minutes");
            }
        }

        // ---------------- The conductor ----------------

        [Test]
        public void TheFirstStart_IsTheLeastLeadAhead_OnBar0_AndLandsThere()
        {
            MusicValues v = V();
            var c = new MusicConductor(v);
            MusicChange ch = c.Step(10.0, MusicState.Calm, 48000);
            Assert.That(ch.Scheduled, Is.True);
            Assert.That(ch.ScheduledBar, Is.EqualTo(0));
            Assert.That(c.LineTime(0), Is.EqualTo(10.0 + v.LeastLead).Within(1.0 / 48000), "the origin: the least lead ahead");
            Assert.That(c.Step(10.04, MusicState.Calm, 48000).Landed, Is.False, "not before its line");
            ch = c.Step(10.06, MusicState.Calm, 48000);
            Assert.That(ch.Landed && ch.From == MusicState.None && ch.To == MusicState.Calm && ch.LandedBar == 0, Is.True);
            Assert.That(c.Playing, Is.EqualTo(MusicState.Calm));
            Assert.That(c.Step(11.0, MusicState.Calm, 48000).Scheduled, Is.False, "nothing more while nothing changes");
        }

        [Test]
        public void AChange_LandsOnTheNextLine_AndOneAskedInsideTheLeastLead_WaitsForTheLineAfter()
        {
            MusicValues v = V();
            var c = Started(v, out double origin);
            double bar = MusicClock.BarSeconds(v);
            // Asked half a bar into bar 3: lands on bar 4.
            MusicChange ch = c.Step(origin + 3.5 * bar, MusicState.Fight, 48000);
            Assert.That(ch.Scheduled && ch.ScheduledBar == 4, Is.True, "half a bar before the line: that line");
            ch = c.Step(origin + 4 * bar + 0.001, MusicState.Fight, 48000);
            Assert.That(ch.Landed && ch.To == MusicState.Fight && ch.LandedBar == 4, Is.True);
            // Asked just inside the least lead before line 7 (20 ms with a 50 ms lead): lands on line 8.
            ch = c.Step(origin + 7 * bar - 0.02, MusicState.Calm, 48000);
            Assert.That(ch.ScheduledBar, Is.EqualTo(8), "inside the least lead: the line after");
            ch = c.Step(origin + 8 * bar, MusicState.Calm, 48000);
            Assert.That(ch.Landed && ch.LandedBar == 8, Is.True, "landed on its line exactly");
            // Just outside it (60 ms before line 11): line 11.
            ch = c.Step(origin + 11 * bar - 0.06, MusicState.Fight, 48000);
            Assert.That(ch.ScheduledBar, Is.EqualTo(11), "outside the least lead: that line");
        }

        [Test]
        public void AChange_FlippedBackBeforeItsLine_IsCancelled_AndToAnotherState_IsCancelledAndTheNewOneTold()
        {
            MusicValues v = V();
            var c = Started(v, out double origin);
            double bar = MusicClock.BarSeconds(v);
            MusicChange ch = c.Step(origin + 2.2 * bar, MusicState.Fight, 48000);
            Assert.That(ch.ScheduledBar, Is.EqualTo(3));
            ch = c.Step(origin + 2.6 * bar, MusicState.Calm, 48000);
            Assert.That(ch.Cancelled && ch.CancelledState == MusicState.Fight && ch.CancelledBar == 3, Is.True, "flipped back: cancelled");
            Assert.That(ch.Scheduled || ch.Landed, Is.False, "and nothing else");
            Assert.That(c.HasPending, Is.False);
            ch = c.Step(origin + 3.1 * bar, MusicState.Calm, 48000);
            Assert.That(ch.Landed, Is.False, "nothing lands on line 3");
            Assert.That(c.Playing, Is.EqualTo(MusicState.Calm));
            // Fight waiting, then the boss's: the fight cancelled and the boss's told for the same line.
            c.Step(origin + 5.1 * bar, MusicState.Fight, 48000);
            ch = c.Step(origin + 5.2 * bar, MusicState.Boss, 48000);
            Assert.That(ch.Cancelled && ch.CancelledState == MusicState.Fight, Is.True);
            Assert.That(ch.Scheduled && ch.ScheduledState == MusicState.Boss && ch.ScheduledBar == 6, Is.True);
        }

        [Test]
        public void AFlipBack_InsideTheLeastLeadOfTheLine_IsTooLate_TheChangeLands_AndTheFlipBackIsItsOwnChangeAtTheLineAfter()
        {
            MusicValues v = V();
            var c = Started(v, out double origin);
            double bar = MusicClock.BarSeconds(v);
            c.Step(origin + 2.2 * bar, MusicState.Fight, 48000);
            MusicChange ch = c.Step(origin + 3 * bar - 0.01, MusicState.Calm, 48000);
            Assert.That(ch.Cancelled, Is.False, "10 ms before its line: told already");
            ch = c.Step(origin + 3 * bar + 0.005, MusicState.Calm, 48000);
            Assert.That(ch.Landed && ch.To == MusicState.Fight, Is.True, "it lands");
            ch = c.Step(origin + 3 * bar + 0.02, MusicState.Calm, 48000);
            Assert.That(ch.Scheduled && ch.ScheduledState == MusicState.Calm && ch.ScheduledBar == 4, Is.True, "back to calm on the next line");
        }

        [Test]
        public void Silence_AndTheSwitchOff_LandAtOnce_AndANewStartAfterSilence_IsOnTheNextLineOfTheSameClock()
        {
            MusicValues v = V();
            var c = Started(v, out double origin);
            double bar = MusicClock.BarSeconds(v);
            c.Step(origin + 4.5 * bar, MusicState.Fight, 48000);
            MusicChange ch = c.Step(origin + 4.6 * bar, MusicState.Silent, 48000);
            Assert.That(ch.Cancelled, Is.True, "her death cancels the change waiting");
            Assert.That(ch.Landed && ch.AtOnce && ch.To == MusicState.Silent && ch.LandedBar == 4, Is.True, "silence at once, in bar 4");
            ch = c.Step(origin + 9.3 * bar, MusicState.Calm, 48000);
            Assert.That(ch.Scheduled && ch.ScheduledBar == 10, Is.True, "a new run: calm at the next bar of the same clock");
            Assert.That(c.LineTime(10), Is.EqualTo(origin + 10 * bar).Within(1.0 / 48000));
            c.Step(origin + 10 * bar, MusicState.Calm, 48000);
            ch = c.Step(origin + 10.5 * bar, MusicState.None, 48000);
            Assert.That(ch.Landed && ch.AtOnce && ch.To == MusicState.None, Is.True, "off at once");
            ch = c.Step(origin + 12.5 * bar, MusicState.Fight, 48000);
            Assert.That(ch.ScheduledBar, Is.EqualTo(13), "on again: the next bar, the clock kept");
        }

        static MusicConductor Started(MusicValues v, out double origin)
        {
            var c = new MusicConductor(v);
            c.Step(100.0, MusicState.Calm, 48000);
            origin = c.LineTime(0);
            c.Step(origin, MusicState.Calm, 48000);
            Assert.That(c.Playing, Is.EqualTo(MusicState.Calm));
            return c;
        }

        // ---------------- The fades ----------------

        [Test]
        public void TheFades_CalmIsGoneWithinOneBeatOfTheFightsLine_TheFightFadesOverTwoBarsUnderTheCalm_SilenceOverTheEndsHalfSecond()
        {
            MusicValues v = V();
            Assert.That(MusicFade.OutSeconds(v, MusicState.Fight), Is.EqualTo(0.46875).Within(1e-12), "calm out over one beat");
            Assert.That(MusicFade.OutSeconds(v, MusicState.Boss), Is.EqualTo(0.46875).Within(1e-12));
            Assert.That(MusicFade.OutSeconds(v, MusicState.Calm), Is.EqualTo(3.75).Within(1e-12), "fight out over two bars");
            Assert.That(MusicFade.OutSeconds(v, MusicState.Silent), Is.EqualTo(0.5).Within(1e-9), "her death: the game's slow stop");
            Assert.That(MusicFade.OutSeconds(v, MusicState.None), Is.EqualTo(0.0), "off at once");

            const double line = 50.0;
            double len = MusicFade.OutSeconds(v, MusicState.Calm);
            Assert.That(MusicFade.Gain(line - 0.5, line, len), Is.EqualTo(1f), "full until the line");
            Assert.That(MusicFade.Gain(line, line, len), Is.EqualTo(1f).Within(1e-6), "full at it");
            Assert.That(MusicFade.Gain(line + len * 0.5, line, len), Is.EqualTo((float)Math.Sqrt(0.5)).Within(1e-5), "0.71 half-way (equal power)");
            Assert.That(MusicFade.Gain(line + len, line, len), Is.EqualTo(0f), "gone at its end");
            Assert.That(MusicFade.Gain(line + len + 1.0, line, len), Is.EqualTo(0f));
            float before = 1.1f;
            for (double t = line; t <= line + len; t += 0.01)
            {
                float g = MusicFade.Gain(t, line, len);
                Assert.That(g, Is.LessThanOrEqualTo(before), "never rising");
                before = g;
            }
            Assert.That(MusicFade.Gain(line + 0.0001, line, 0.0), Is.EqualTo(0f), "no fade: gone from the line");
            Assert.That(MusicFade.Gain(line, line, double.PositiveInfinity), Is.EqualTo(1f).Within(1e-6));
            Assert.That(MusicFade.Gain(1e9, double.PositiveInfinity, 0.0), Is.EqualTo(1f), "no fade told: full");
        }

        [Test]
        public void TheScreensShare_GlidesToHalfAndBack_OverAQuarterSecond()
        {
            MusicValues v = V();
            Assert.That(v.HeldShare, Is.EqualTo(0.5f));
            float s = 1f;
            s = MusicFade.Toward(s, v.HeldShare, 0.1f, v.HeldGlide);
            Assert.That(s, Is.EqualTo(0.6f).Within(1e-5));
            s = MusicFade.Toward(s, v.HeldShare, 0.2f, v.HeldGlide);
            Assert.That(s, Is.EqualTo(0.5f), "there, and no further");
            s = MusicFade.Toward(s, 1f, 1f, v.HeldGlide);
            Assert.That(s, Is.EqualTo(1f), "back");
            Assert.That(MusicFade.Toward(0.5f, 1f, 0.01f, 0f), Is.EqualTo(1f), "no glide: at once");
        }

        // ---------------- What the game wants ----------------

        [Test]
        public void TheWantedState_FromWhatTheGameKnows()
        {
            Assert.That(MusicRules.Target(false, true, true, true, true, true), Is.EqualTo(MusicState.None), "the switch off: none");
            Assert.That(MusicRules.Target(true, false, false, false, false, false), Is.EqualTo(MusicState.Calm), "no fight: calm");
            Assert.That(MusicRules.Target(true, false, false, false, false, true), Is.EqualTo(MusicState.Fight), "a room's fight: fight");
            Assert.That(MusicRules.Target(true, false, false, false, true, true), Is.EqualTo(MusicState.Boss), "the boss's room's fight: boss");
            Assert.That(MusicRules.Target(true, false, true, false, false, true), Is.EqualTo(MusicState.Silent), "her death: silent");
            Assert.That(MusicRules.Target(true, true, false, false, false, false), Is.EqualTo(MusicState.Silent), "the end screen: silent");
            Assert.That(MusicRules.Target(true, false, false, true, false, true), Is.EqualTo(MusicState.Calm), "the screen between runs: calm");
            foreach (MusicState s in Enum.GetValues(typeof(MusicState)).Cast<MusicState>())
                Assert.That(MusicRules.FromWord(MusicRules.Word(s)), Is.EqualTo(s), "the recorder's word and back: " + s);
            Assert.That(MusicClips.PathOf(MusicClips.Calm), Is.EqualTo("Music/Explore"));
            Assert.That(MusicClips.PathOf(MusicClips.Fight), Is.EqualTo("Music/Fight"));
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_SaysTheSecondsInEachState_AndTheChanges_AndNothingWithoutMusic()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0.0, SessionEventTypes.SessionStart),
                Music(1.0, MusicState.None, MusicState.Calm, 0),
                Music(11.0, MusicState.Calm, MusicState.Fight, 6),
                Music(31.0, MusicState.Fight, MusicState.Calm, 17),
                Music(41.0, MusicState.Calm, MusicState.Boss, 22),
                Music(61.0, MusicState.Boss, MusicState.Silent, 33),
                new SessionEvent(70.0, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Music.Calm, Is.EqualTo(20.0).Within(1e-9));
            Assert.That(stats.Music.Fight, Is.EqualTo(20.0).Within(1e-9));
            Assert.That(stats.Music.Boss, Is.EqualTo(20.0).Within(1e-9));
            Assert.That(stats.Music.Silent, Is.EqualTo(9.0).Within(1e-9), "to the session's end");
            Assert.That(stats.Music.Changes, Is.EqualTo(4), "the start is not a change");
            string text = SessionSummary.Format(stats);
            Assert.That(text, Does.Contain("Music calm 20.0 s, fight 20.0 s, boss 20.0 s, silent 9.0 s, off 0.0 s; 4 changes"));
            // Read back from the log's own lines.
            SessionEvent back = SessionEvent.Parse(Music(5.0, MusicState.Calm, MusicState.Fight, 3).ToJson());
            Assert.That((back.Type, back.Name, back.ExtraValue("from"), back.ExtraValue("bar")), Is.EqualTo((MusicEvents.Change, "fight", "calm", "3")));

            SessionStats none = SessionSummary.Compute(new List<SessionEvent> { new SessionEvent(0.0, SessionEventTypes.SessionStart), new SessionEvent(9.0, SessionEventTypes.SessionEnd) });
            Assert.That(SessionSummary.MusicLine(none.Music), Is.Null);
            Assert.That(SessionSummary.Format(none), Does.Not.Contain("Music "), "a session without music: no line");
        }

        static SessionEvent Music(double t, MusicState from, MusicState to, long bar)
        {
            var e = new SessionEvent(t, MusicEvents.Change, MusicRules.Word(to));
            e.AddExtra("from", MusicRules.Word(from));
            e.AddExtra("to", MusicRules.Word(to));
            e.AddExtra("bar", bar.ToString(System.Globalization.CultureInfo.InvariantCulture));
            return e;
        }
    }
}
