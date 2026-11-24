using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 107 (the warder that reads; Kristian's tick of 2026-11-24, "vokteren som ikke leses"), as rules: the switch is
    /// on and off gives the ward's widths as before; on, the thread and the ring are wider and pulse, a hit flashes the ring
    /// wider still; the break is heard for the reasons that daze and not for a ward simply gone; the hum's gate starts with the
    /// first ward and stops with the last, once each; the line shows once a run (a new run, once again); a warded number's mark:
    /// an ordinary one green, a crit's and a clean press's ink kept, the shield a shield; the tally marks the target's latest
    /// number and keeps it marked as hits join it; the two new sounds are in the table after the others, quiet, and by name.
    /// Nobody has seen or heard any of it here.
    /// </summary>
    public class WarderReadsRulesTests
    {
        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOn_AndOff_TheWidthsAreAsBefore()
        {
            var v = new WarderValues();
            Assert.That(v.Reads, Is.True, "on by default");
            Assert.That(v.Reading, Is.Not.Null);
            v.Reads = false;
            foreach (float now in new[] { 0f, 0.2f, 0.45f, 1.3f, 17.7f })
            {
                Assert.That(WarderReadRules.ThreadWidth(v, now), Is.EqualTo(v.ThreadWidth), "the thread as before");
                Assert.That(WarderReadRules.ShellWidth(v, now, false), Is.EqualTo(v.ShellWidth), "the ring as before");
                Assert.That(WarderReadRules.ShellWidth(v, now, true), Is.EqualTo(v.ShellHitWidth), "a hit's flash as before");
            }
            Assert.That(WarderReadRules.ShellHitWidth(v), Is.EqualTo(v.ShellHitWidth));
            Assert.That(WarderReadRules.FromLight(v), Is.False, "the thread from its chest, as before");
            Assert.That(WarderReadRules.Hums(v) || WarderReadRules.BreakSounds(v) || WarderReadRules.MarksNumbers(v) || WarderReadRules.Hints(v), Is.False,
                        "nothing heard, marked or told");
            Assert.That(WarderReadRules.On(null), Is.False);
        }

        [Test]
        public void TheWardsOwnNumbers_AreNotChanged()
        {
            var v = new WarderValues();
            Assert.That(v.WardRange, Is.EqualTo(9f));
            Assert.That(v.WardShare, Is.EqualTo(0.6f));
            Assert.That(v.MostWards, Is.EqualTo(2));
            Assert.That(v.BreakDaze, Is.EqualTo(0.6f));
            Assert.That(v.RaiseSeconds, Is.EqualTo(1f));
            Assert.That(v.ThreadWidth, Is.EqualTo(0.06f), "the morning's look kept reachable by the switch");
            Assert.That(v.ShellWidth, Is.EqualTo(0.08f));
            Assert.That(v.ShellHitWidth, Is.EqualTo(0.25f));
        }

        // ---------------- The ward seen ----------------

        [Test]
        public void On_TheThreadAndTheRing_AreWider_AndPulse_AndAHitFlashesTheRingWiderStill()
        {
            var v = new WarderValues();
            WarderReadValues r = v.Reading;
            float lowThread = float.MaxValue, highThread = 0f, lowShell = float.MaxValue, highShell = 0f;
            for (int i = 0; i <= 200; i++)
            {
                float now = i * r.PulseSeconds / 100f;
                float t = WarderReadRules.ThreadWidth(v, now), s = WarderReadRules.ShellWidth(v, now, false);
                lowThread = Math.Min(lowThread, t);
                highThread = Math.Max(highThread, t);
                lowShell = Math.Min(lowShell, s);
                highShell = Math.Max(highShell, s);
                Assert.That(WarderReadRules.ShellWidth(v, now, true), Is.EqualTo(r.ShellHitWidth), "a hit's flash");
            }
            Assert.That(lowThread, Is.EqualTo(r.ThreadWidth).Within(1e-4f), "the thread at rest");
            Assert.That(lowThread, Is.GreaterThanOrEqualTo(2f * v.ThreadWidth), "at least twice the morning's");
            Assert.That(highThread, Is.EqualTo(r.ThreadWidth * (1f + r.PulseDepth)).Within(1e-3f), "it swells at a pulse");
            Assert.That(lowShell, Is.EqualTo(r.ShellWidth).Within(1e-4f));
            Assert.That(lowShell, Is.GreaterThanOrEqualTo(2f * v.ShellWidth));
            Assert.That(highShell, Is.EqualTo(r.ShellWidth * (1f + r.PulseDepth)).Within(1e-3f));
            Assert.That(r.ShellHitWidth, Is.GreaterThan(highShell), "a hit's flash wider than the pulse");
            Assert.That(WarderReadRules.FromLight(v), Is.True, "the thread from its light");
        }

        [Test]
        public void ThePulse_IsSmooth_From0To1_OnceAPeriod()
        {
            Assert.That(WarderReadRules.Pulse(0f, 0.9f), Is.EqualTo(0f).Within(1e-5f));
            Assert.That(WarderReadRules.Pulse(0.45f, 0.9f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(WarderReadRules.Pulse(0.9f, 0.9f), Is.EqualTo(0f).Within(1e-4f));
            Assert.That(WarderReadRules.Pulse(1000.45f, 0.9f), Is.InRange(0f, 1f));
            Assert.That(WarderReadRules.Pulse(1f, 0f), Is.EqualTo(0f), "no period: no pulse");
            float last = WarderReadRules.Pulse(0f, 0.9f);
            for (int i = 1; i <= 45; i++)
            {
                float p = WarderReadRules.Pulse(i * 0.01f, 0.9f);
                Assert.That(p, Is.GreaterThanOrEqualTo(last - 1e-6f), "rising to its top");
                last = p;
            }
        }

        // ---------------- The ward heard ----------------

        [TestCase(WardEnd.WarderKilled, true)]
        [TestCase(WardEnd.WarderStaggered, true)]
        [TestCase(WardEnd.TooFar, true)]
        [TestCase(WardEnd.AllyGone, false)]
        [TestCase(WardEnd.WentHome, false)]
        public void TheBreak_IsHeard_ForTheReasonsThatDaze(WardEnd why, bool heard)
        {
            Assert.That(WarderReadRules.BreakHeard(why), Is.EqualTo(heard));
            Assert.That(WarderReadRules.BreakHeard(why), Is.EqualTo(WarderRules.Dazes(why)));
        }

        [Test]
        public void TheHum_StartsWithTheFirstWard_AndStopsWithTheLast_OnceEach()
        {
            var gate = new HumGate();
            int[] wardsOn = { 0, 0, 1, 1, 2, 2, 1, 2, 1, 0, 0 };
            var changes = wardsOn.Select(n => gate.Step(WarderReadRules.HumWanted(n, true, false))).ToArray();
            Assert.That(changes, Is.EqualTo(new[] { false, false, true, false, false, false, false, false, false, true, false }));
            Assert.That(gate.Starts, Is.EqualTo(1), "started with the first ward on");
            Assert.That(gate.Stops, Is.EqualTo(1), "stopped with the last gone");
            Assert.That(gate.On, Is.False);
            Assert.That(WarderReadRules.HumWanted(2, false, false), Is.False, "the placeholder sounds off: no hum");
            Assert.That(WarderReadRules.HumWanted(2, true, true), Is.False, "paused: silent");
        }

        [Test]
        public void TheTwoSounds_AreAfterTheOthers_InTheTable_QuietBesideTheGuns_ByName()
        {
            var v = new PlaceholderSoundValues();
            Assert.That((int)PlaceholderSound.WardHum, Is.EqualTo(38), "after the thirty-eight, which keep their places");
            Assert.That((int)PlaceholderSound.WardBreaks, Is.EqualTo(39));
            foreach (PlaceholderSound s in new[] { PlaceholderSound.WardHum, PlaceholderSound.WardBreaks })
            {
                Assert.That(v.Sounds.Count(x => x.Sound == s), Is.EqualTo(1), s + ": its own line");
                Assert.That(v.Volume(s), Is.InRange(0.2f, 0.35f), s + ": quiet");
                Assert.That(PlaceholderSoundRules.ResourcePath(s), Is.EqualTo("Sounds/Placeholders/" + s), "his recording replaces it by its name");
                Assert.That(PlaceholderSoundRules.MayStart(s, paused: true), Is.False, "paused is silent");
            }
            Assert.That(v.Volume(PlaceholderSound.WardHum), Is.EqualTo(v.Volume(PlaceholderSound.EnemyHit)), "the hum as quiet as the quietest");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WardStanding), Is.EqualTo(PlaceholderSound.WardHum));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WardFell), Is.EqualTo(PlaceholderSound.WardBreaks));
            float[] hum = Synth.Render(PlaceholderRecipes.For(PlaceholderSound.WardHum));
            Assert.That(hum[0], Is.EqualTo(0f), "the loop starts in silence");
            Assert.That(hum[hum.Length - 1], Is.EqualTo(0f), "and ends in it: looped, it breathes, it does not click");
        }

        // ---------------- The first time, told once ----------------

        [Test]
        public void TheLine_ShowsOnceARun_AndOnceAgainInTheNext()
        {
            var once = new OncePerRun();
            object runA = new object(), runB = new object();
            Assert.That(once.Take(runA), Is.True, "the first warded hit of the run");
            for (int i = 0; i < 20; i++) Assert.That(once.Take(runA), Is.False, "never again that run");
            Assert.That(once.ShownIn(runA), Is.True);
            Assert.That(once.Take(runB), Is.True, "a new run: once again");
            Assert.That(once.Take(runB), Is.False);
            Assert.That(once.Taken, Is.EqualTo(2));
            var practice = new OncePerRun();
            Assert.That(practice.Take(null), Is.True, "the practice room before a run counts as one of its own");
            Assert.That(practice.Take(null), Is.False);
            Assert.That(WarderReadRules.HintText, Is.EqualTo("Warded: kill the warder"));
        }

        [Test]
        public void TheLine_IsFull_ThenFades_AndIsGoneAtItsEnd()
        {
            var r = new WarderReadValues();
            Assert.That(WarderReadRules.HintAlpha(0f, r), Is.EqualTo(1f));
            Assert.That(WarderReadRules.HintAlpha(r.HintSeconds * r.HintFadeFrom * 0.9f, r), Is.EqualTo(1f));
            Assert.That(WarderReadRules.HintAlpha(r.HintSeconds * 0.9f, r), Is.InRange(0.01f, 0.99f));
            Assert.That(WarderReadRules.HintAlpha(r.HintSeconds, r), Is.EqualTo(0f));
            Assert.That(r.HintSeconds, Is.InRange(1.5f, 4f), "long enough to read five words, short enough to be gone in the fight");
        }

        // ---------------- The mark on a warded hit's number ----------------

        [Test]
        public void AWardedNumber_AnOrdinaryOneTurnsGreen_ACritsAndACleanPresssInkKept()
        {
            Assert.That(WarderReadRules.GreenInk(NumberKind.Plain), Is.True);
            foreach (NumberKind k in new[] { NumberKind.Crit, NumberKind.Perfect, NumberKind.Dazed, NumberKind.Marks, NumberKind.MarksFull, NumberKind.Hurt, NumberKind.Heal })
                Assert.That(WarderReadRules.GreenInk(k), Is.False, k.ToString());
        }

        [Test]
        public void TheShield_IsAShield_FlatTopStraightSidesPointedBottom()
        {
            Assert.That(WarderReadRules.InShield(0f, 0.02f), Is.True, "its top middle");
            Assert.That(WarderReadRules.InShield(-0.97f, 0.05f) && WarderReadRules.InShield(0.97f, 0.3f), Is.True, "its straight sides");
            Assert.That(WarderReadRules.InShield(0f, 0.97f), Is.True, "its point");
            Assert.That(WarderReadRules.InShield(0.5f, 0.97f), Is.False, "narrow at the point");
            Assert.That(WarderReadRules.InShield(0.9f, 0.85f), Is.False, "the sides curve in");
            Assert.That(WarderReadRules.InShield(0f, -0.01f) || WarderReadRules.InShield(0f, 1.01f) || WarderReadRules.InShield(1.01f, 0.2f), Is.False, "nothing outside");
            // Its width shrinks all the way from the shoulder to the point.
            float last = 2f;
            for (int i = 0; i <= 20; i++)
            {
                float v = WarderReadRules.ShieldShoulder + (1f - WarderReadRules.ShieldShoulder) * i / 20f, w = 0f;
                for (int k = 0; k <= 1000; k++) if (WarderReadRules.InShield(k / 1000f, v)) w = k / 1000f;
                Assert.That(w, Is.LessThanOrEqualTo(last + 1e-6f));
                last = w;
            }
        }

        [Test]
        public void TheTally_MarksTheTargetsLatestNumber_AndKeepsItMarkedAsHitsJoinIt()
        {
            var tally = new DamageTally();
            const float window = 0.18f;
            tally.Add(1, 16, NumberKind.Plain, 1f, window, 24);
            tally.Add(2, 40, NumberKind.Plain, 1f, window, 24);
            Assert.That(tally.MarkWarded(1), Is.True);
            Assert.That(tally.Numbers.Single(n => n.Target == 1).Warded, Is.True, "the warded hit's number marked");
            Assert.That(tally.Numbers.Single(n => n.Target == 2).Warded, Is.False, "an unwarded hit's number as before");
            tally.Add(1, 16, NumberKind.Crit, 1.1f, window, 24);
            TallyNumber joined = tally.Numbers.Single(n => n.Target == 1);
            Assert.That(joined.Sum, Is.EqualTo(32));
            Assert.That(joined.Warded, Is.True, "still marked when a hit joins it");
            Assert.That(joined.Kind, Is.EqualTo(NumberKind.Crit), "its kind as the rules give it");
            tally.Add(1, 16, NumberKind.Plain, 2f, window, 24);
            Assert.That(tally.Numbers.Last(n => n.Target == 1).Warded, Is.False, "a new number starts unmarked");
            Assert.That(tally.MarkWarded(9), Is.False, "a target with no number");
        }
    }
}
