using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 152: the shot's cone the same at every frame rate. Four cases at 30, 60, 144 and 240 frames a second (fixed
    /// steps), with <see cref="PrecisionValues.SameAtEveryFrameRate"/> off (work order 143's frame, as built) and on: held fire on
    /// the move (the first shot and the twelfth, steady), a single shot from a run, a shot pressed just before a counter-strafe, a
    /// shot pressed as she lets go of the keys; and for the counter-strafe and letting go, the earliest moment a shot can go off
    /// accurate. Each against the law's own continuous run (frames of 1/7200 s) with the same presses at the same moments.
    ///
    /// What is the game's own code here: the law (<see cref="PrecisionRules.InertiaFrame"/>, <see cref="PrecisionRules.GhostFrame"/>,
    /// <see cref="PrecisionRules.MeterStep"/> and work order 152's <see cref="PrecisionMoment"/>), the ease of a shot on the move
    /// (<see cref="MovingShot"/>), hold to fire's frame (<see cref="HoldToFireRules.ClickGoesThisFrame"/>,
    /// <see cref="ClickToShoot.MustWait"/>), the cone (<see cref="PrecisionRules.Cone(float, PrecisionValues)"/>). What is MIRRORED
    /// from PlayerCharacter (Update: PrecisionBefore, UpdateFrame, PrecisionAfter; the input driver before it), as the grips'
    /// analyst mirrored it (scratch branch, GripsProposalNumbersTests at 28095e6, whose 60 and 144 fps numbers it gives again): the
    /// order of a frame, a press first, then the cap set from the ease at the frame's moment, then the shot that goes off, then the
    /// law run over the step toward that cap.
    /// </summary>
    public class ConeAtEveryFrameRateTests
    {
        static readonly PrecisionValues V = new PrecisionValues();
        static readonly FeelValues F = new FeelValues();
        const float Run = 6f;

        /// <summary>The frame rates of the order, and the continuous law's (a multiple of each, so every frame's moment is one of its own).</summary>
        static readonly int[] Rates = { 30, 60, 144, 240 };
        const int Continuous = 7200;
        /// <summary>Degrees within which the order asks the cases to agree.</summary>
        const float Agree = 0.2f;

        /// <summary>Seconds before the counter-strafe the shot of that case is pressed (at the first frame at or after it).</summary>
        const double CounterClickBefore = 0.07;
        /// <summary>Seconds into the run the single shot is pressed (a moment of every one of the four frame rates).</summary>
        const double SingleClickAt = 0.5;
        /// <summary>Held fire's shots: the twelfth is steady.</summary>
        const int HeldShots = 12;

        [TearDown]
        public void PutBack() => PrecisionValues.SameAtEveryFrameRate = true;

        public enum Case { HeldFire, SingleFromRun, CounterStrafe, LetGo }

        // ---------------- The frame mirror ----------------

        /// <summary>
        /// One run of frames of 1/<paramref name="fps"/> s. She runs along +Z at full speed with the key held from 0.3 s before
        /// time 0; the case decides the keys after 0 and the clicks; presses go only on frames that are moments of
        /// <paramref name="pressFps"/> (the continuous run given the presses of a coarser one). Returns the cones of the shots, in order.
        /// </summary>
        public static List<float> Mirror(PrecisionMode mode, int fps, Case c, double clickAt, int shots, int pressFps = 0)
        {
            double dt = 1.0 / fps;
            int pressEvery = pressFps > 0 ? fps / pressFps : 1;
            bool repaired = PrecisionValues.SameAtEveryFrameRate;
            float vx = 0f, vz = Run, gx = 0f, gz = Run, momentum = 1f;
            var cones = new List<float>();
            bool easing = false;
            float easeFrom = 1f, easeAt = 0f, firedAfter = -1f;
            bool acting = false, resolved = false, waiting = false, clicked = false;
            float shotAt = 0f, startup = F.BasicShotStartup, total = F.BasicShotStartup + F.BasicShotActive + F.BasicShotRecovery;
            float nextShotAt = float.NegativeInfinity;
            float up = OffTheMark.MovingShotRecovery(F), down = F.MovingShotSlowdown, slow = F.MovingShotSpeedShare;
            float lowest = 1f, prevShare = 1f;      // work order 143's frame: the lowest at frames' ends, the share at the last end
            var watch = new WindUpWatch();          // work order 152's
            float motionZ = Run, heldBefore = 1f, capClock = 0f, capNowShare = 1f;
            int first = -(int)Math.Round(0.3 * fps);

            float Cap(float now) => easing ? MovingShot.SpeedShare(easeFrom, now - easeAt, firedAfter, slow, down, up) : 1f;
            // As PlayerCharacter's IPrecisionCurve.ShareAt: the cap's share u seconds into the step against the frame's own.
            var ahead = new Curve(u => capNowShare > 1e-4f ? Cap(capClock + u) / capNowShare : 1f);
            float Share() =>
                mode == PrecisionMode.Inertia ? (float)Math.Sqrt(vx * vx + vz * vz) / Run
                : mode == PrecisionMode.Ghost ? (float)Math.Sqrt(gx * gx + gz * gz) / Run
                : PrecisionRules.MeterShare(momentum, V.AccurateShare);
            float KeyZ(float now) => c == Case.CounterStrafe ? (now >= 0f ? -1f : 1f) : c == Case.LetGo ? (now >= 0f ? 0f : 1f) : 1f;

            void StartShot(float now, float keyZ)
            {
                // StartBasicShot: on the move if a key is held or (Inertia) her body slides on; the ease from the cap where it stands.
                bool onTheMove = keyZ != 0f || (mode == PrecisionMode.Inertia && Share() * Run > V.InertiaMovingSpeed);
                if (onTheMove)
                {
                    easeFrom = Cap(now);
                    easeAt = now;
                    firedAfter = -1f;
                    easing = true;
                }
                shotAt = now;
                acting = true;
                resolved = false;
                lowest = prevShare;                 // PrecisionShotStarted
                watch.Start(prevShare, startup);
            }

            for (int n = first; n < first + 400000; n++)
            {
                float t = (float)(n * dt);
                float keyZ = KeyZ(t);
                bool mayPress = ((n % pressEvery) + pressEvery) % pressEvery == 0;
                // The input driver (order −100): held fire asks when a click would go this frame; a click is pressed once.
                bool press = false;
                if (c == Case.HeldFire)
                    press = mayPress && t >= 0f && cones.Count + (acting && !resolved ? 1 : 0) < shots
                            && HoldToFireRules.ClickGoesThisFrame(false, acting, acting && resolved, t, nextShotAt);
                else if (!clicked && mayPress && t >= clickAt - 1e-7)
                {
                    press = true;
                    clicked = true;
                }
                if (press)
                {
                    if (ClickToShoot.MustWait(acting, t, nextShotAt)) waiting = true;
                    else StartShot(t, keyZ);
                }
                // ApplyMovementFeel: the cap from the ease at the frame's moment.
                if (easing && MovingShot.IsOver(t - easeAt, firedAfter, down, up)) easing = false;
                float capNow = Cap(t);
                if (acting)
                {
                    if (!resolved && t - shotAt >= startup)
                    {
                        // ConsumeHit, then DeflectBasicShot.
                        resolved = true;
                        nextShotAt = shotAt + total;
                        if (easing) firedAfter = t - easeAt;
                        float share = repaired ? (watch.Reached ? watch.Lowest : Math.Min(watch.Lowest, prevShare)) : Math.Min(lowest, prevShare);
                        watch.Stop();
                        cones.Add(PrecisionRules.Cone(share, V));
                        if (cones.Count >= shots) break;
                    }
                    if (t >= shotAt + total)
                    {
                        acting = false;
                        if (waiting && mayPress && t >= nextShotAt)
                        {
                            waiting = false;
                            StartShot(t, keyZ);
                        }
                    }
                }
                // PrecisionAfter: her real velocity (the meter and the ghost: as instant as today), a counter-strafe, then the law.
                float fdt = (float)dt;
                float realZ = keyZ * capNow * Run;
                motionZ += (realZ - motionZ) * (1f - (float)Math.Exp(-fdt / V.MotionSeconds));
                bool counter = keyZ != heldBefore && keyZ != 0f
                               && PrecisionRules.IsCounterStrafe(new GroundPoint(0f, keyZ), new GroundPoint(0f, motionZ), Run, V);
                heldBefore = keyZ;
                if (mode == PrecisionMode.Meter && counter)
                {
                    momentum = 0f;
                    if (repaired) watch.Note(PrecisionRules.MeterShare(0f, V.AccurateShare)); // the instant it is emptied is in the wind-up
                }
                capClock = t;
                capNowShare = Cap(t);
                IPrecisionCurve curve = keyZ != 0f ? ahead : null; // MoveAlong noted a wish this frame
                if (repaired)
                {
                    if (mode == PrecisionMode.Inertia)
                        PrecisionMoment.InertiaFrame(ref vx, ref vz, 0f, keyZ, keyZ != 0f ? capNow * Run : 0f, curve, fdt, Run, V, ref watch);
                    else if (mode == PrecisionMode.Ghost)
                        PrecisionMoment.GhostFrame(ref gx, ref gz, 0f, realZ, curve, fdt, Run, V, 1f, ref watch);
                    else momentum = PrecisionMoment.MeterFrame(momentum, Math.Abs(realZ) / Run, curve, fdt, V, V.AccurateShare, 1f, ref watch);
                }
                else
                {
                    if (mode == PrecisionMode.Inertia) PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, keyZ, keyZ != 0f ? capNow * Run : 0f, fdt, V);
                    else if (mode == PrecisionMode.Ghost) PrecisionRules.GhostFrame(ref gx, ref gz, 0f, realZ, fdt, V);
                    else momentum = PrecisionRules.MeterStep(momentum, Math.Abs(realZ) / Run, fdt, V, V.AccurateShare);
                }
                float s = Share();
                if (acting && !resolved) lowest = Math.Min(lowest, s);
                prevShare = s;
            }
            return cones;
        }

        /// <summary>The mirror's curve (her character is its own in the game).</summary>
        sealed class Curve : IPrecisionCurve
        {
            readonly Func<float, float> _at;
            public Curve(Func<float, float> at) => _at = at;
            public float ShareAt(float seconds) => _at(seconds);
        }

        /// <summary>The first frame's moment at or after <paramref name="at"/> at <paramref name="fps"/>.</summary>
        static double OnAFrame(double at, int fps) => Math.Ceiling(at * fps - 1e-9) / fps;

        /// <summary>The cone of one case at one frame rate (held fire: the first and the steady shot), and the continuous law's with the same presses.</summary>
        public struct Reading
        {
            public float First, Steady, ContinuousFirst, ContinuousSteady;
        }

        public static Reading Read(PrecisionMode mode, Case c, int fps)
        {
            bool repaired = PrecisionValues.SameAtEveryFrameRate;
            double click = c == Case.SingleFromRun ? SingleClickAt : c == Case.CounterStrafe ? OnAFrame(-CounterClickBefore, fps) : 0.0;
            int shots = c == Case.HeldFire ? HeldShots : 1;
            List<float> at = Mirror(mode, fps, c, click, shots);
            PrecisionValues.SameAtEveryFrameRate = true; // the continuous law: the repaired run at 1/7200 s, the presses of this frame rate
            List<float> law = Mirror(mode, Continuous, c, click, shots, fps);
            PrecisionValues.SameAtEveryFrameRate = repaired;
            return new Reading { First = at[0], Steady = at[at.Count - 1], ContinuousFirst = law[0], ContinuousSteady = law[law.Count - 1] };
        }

        /// <summary>The earliest moment after the counter-strafe or letting go (time 0) at which a shot pressed on a frame goes off accurate, s.</summary>
        public static double EarliestAccurate(PrecisionMode mode, Case c, int fps)
        {
            for (int k = -(int)Math.Round(0.15 * fps); k < fps / 2; k++)
                if (Mirror(mode, fps, c, k / (double)fps, 1)[0] <= 0f) return k / (double)fps + F.BasicShotStartup;
            return double.NaN;
        }

        // ---------------- The table, and the order's demand ----------------

        [Test]
        public void TheFourCases_AtFourFrameRates_AgreeWithTheContinuousLaw_WithTheSwitchOn()
        {
            var sb = new StringBuilder();
            foreach (PrecisionMode mode in new[] { PrecisionMode.Inertia, PrecisionMode.Meter, PrecisionMode.Ghost })
            {
                foreach (bool on in new[] { false, true })
                {
                    PrecisionValues.SameAtEveryFrameRate = on;
                    sb.AppendLine(mode + ", " + (on ? "repaired (work order 152)" : "as built (work order 143)")
                                  + " | held fire first / steady | single shot from a run | pressed 70 ms before a counter-strafe | pressed as she lets go"
                                  + " | earliest accurate after a counter-strafe / letting go");
                    foreach (int fps in Rates)
                    {
                        Reading held = Read(mode, Case.HeldFire, fps), single = Read(mode, Case.SingleFromRun, fps);
                        Reading counter = Read(mode, Case.CounterStrafe, fps), letGo = Read(mode, Case.LetGo, fps);
                        sb.AppendLine("  " + fps + " fps | " + D(held.First) + " / " + D(held.Steady) + " (law " + D(held.ContinuousFirst) + " / " + D(held.ContinuousSteady) + ")"
                                      + " | " + D(single.First) + " (law " + D(single.ContinuousFirst) + ")"
                                      + " | " + D(counter.First) + " (law " + D(counter.ContinuousFirst) + ")"
                                      + " | " + D(letGo.First) + " (law " + D(letGo.ContinuousFirst) + ")"
                                      + " | " + Ms(EarliestAccurate(mode, Case.CounterStrafe, fps)) + " / " + Ms(EarliestAccurate(mode, Case.LetGo, fps)));
                        if (!on) continue;
                        string at = mode + " at " + fps + " fps: ";
                        Assert.That(held.First, Is.EqualTo(held.ContinuousFirst).Within(Agree), at + "held fire's first shot, the law's");
                        Assert.That(held.Steady, Is.EqualTo(held.ContinuousSteady).Within(Agree), at + "held fire's steady shot, the law's with the same presses");
                        Assert.That(single.First, Is.EqualTo(single.ContinuousFirst).Within(Agree), at + "a single shot from a run");
                        Assert.That(counter.First, Is.EqualTo(counter.ContinuousFirst).Within(Agree), at + "a shot pressed before a counter-strafe");
                        Assert.That(letGo.First, Is.EqualTo(letGo.ContinuousFirst).Within(Agree), at + "a shot pressed as she lets go");
                    }
                    double cc = EarliestAccurate(mode, Case.CounterStrafe, Continuous), cl = EarliestAccurate(mode, Case.LetGo, Continuous);
                    if (on) sb.AppendLine("  the continuous law: earliest accurate " + Ms(cc) + " / " + Ms(cl));
                }
            }
            // The cases whose presses are moments of all four frame rates agree with each other too.
            PrecisionValues.SameAtEveryFrameRate = true;
            foreach (PrecisionMode mode in new[] { PrecisionMode.Inertia, PrecisionMode.Meter, PrecisionMode.Ghost })
                foreach (Case c in new[] { Case.HeldFire, Case.SingleFromRun, Case.LetGo })
                {
                    Reading at30 = Read(mode, c, 30);
                    foreach (int fps in Rates)
                        Assert.That(Read(mode, c, fps).First, Is.EqualTo(at30.First).Within(Agree), mode + " " + c + ": the first shot at " + fps + " fps as at 30");
                }
            TestContext.WriteLine(sb.ToString());
        }

        [Test]
        public void Inertia_HeldFireWithTheStockGun_LandsOnTheLawsOwn13Point9_AtEveryFrameRate()
        {
            PrecisionValues.SameAtEveryFrameRate = true;
            foreach (int fps in Rates)
            {
                Reading held = Read(PrecisionMode.Inertia, Case.HeldFire, fps);
                Assert.That(held.First, Is.EqualTo(13.9f).Within(Agree), fps + " fps: the first shot, the analyst's continuous 13.9 degrees");
            }
            // Steady at 240 fps, whose held shots start on the very moment the last one's time is over (54 frames): the law's 11.2.
            Assert.That(Read(PrecisionMode.Inertia, Case.HeldFire, 240).Steady, Is.EqualTo(11.2f).Within(Agree), "240 fps: steady, the analyst's continuous 11.2 degrees");
        }

        [Test]
        public void SwitchOff_IsWorkOrder143sFrame_TheAnalystsNumbers()
        {
            PrecisionValues.SameAtEveryFrameRate = false;
            Reading at60 = Read(PrecisionMode.Inertia, Case.HeldFire, 60), at144 = Read(PrecisionMode.Inertia, Case.HeldFire, 144);
            Assert.That(at60.First, Is.EqualTo(16.3f).Within(0.05f), "60 fps, the first shot: the analyst's 16.3");
            Assert.That(at60.Steady, Is.EqualTo(13.9f).Within(0.05f), "60 fps, steady: 13.9");
            Assert.That(at144.First, Is.EqualTo(14.5f).Within(0.05f), "144 fps, the first shot: 14.5");
            Assert.That(at144.Steady, Is.EqualTo(12.1f).Within(0.05f), "144 fps, steady: 12.1");
        }

        // ---------------- PrecisionMoment's own rules ----------------

        [Test]
        public void WithNothingWatchedAndAHeldCap_TheLawIsWorkOrder143s_NumberForNumber()
        {
            foreach (float dt in new[] { 1f / 30f, 1f / 60f, 1f / 144f, 1f / 240f, 0.0003f })
            {
                float ax = 1f, az = 5f, bx = 1f, bz = 5f, gx = 0f, gz = 6f, hx = 0f, hz = 6f, m = 0.4f, mm = 0.4f;
                var none = new WindUpWatch();
                for (int i = 0; i < 40; i++)
                {
                    float wish = i < 20 ? -1f : 0f;
                    PrecisionRules.InertiaFrame(ref ax, ref az, 0f, wish, 3f, dt, V);
                    PrecisionMoment.InertiaFrame(ref bx, ref bz, 0f, wish, 3f, null, dt, Run, V, ref none);
                    PrecisionRules.GhostFrame(ref gx, ref gz, 0f, wish * 3f, dt, V, 1.2f);
                    PrecisionMoment.GhostFrame(ref hx, ref hz, 0f, wish * 3f, null, dt, Run, V, 1.2f, ref none);
                    m = PrecisionRules.MeterStep(m, i < 20 ? 0.9f : 0.1f, dt, V, V.AccurateShare, 1.5f);
                    mm = PrecisionMoment.MeterFrame(mm, i < 20 ? 0.9f : 0.1f, null, dt, V, V.AccurateShare, 1.5f, ref none);
                }
                Assert.That(bx, Is.EqualTo(ax).Within(1e-5f), "Inertia x");
                Assert.That(bz, Is.EqualTo(az).Within(1e-5f), "Inertia z");
                Assert.That(hz, Is.EqualTo(gz).Within(1e-5f), "the ghost");
                Assert.That(mm, Is.EqualTo(m).Within(1e-5f), "the meter");
            }
        }

        [Test]
        public void TheWindUpsMoment_InsideAFrame_IsTheLawRunToThatMoment()
        {
            // Letting go at full speed; the wind-up ends 50 ms in: at 30 fps that is 16.7 ms into the second frame.
            const float windUp = 0.05f;
            float vx = 0f, vz = Run;
            var watch = new WindUpWatch();
            watch.Start(1f, windUp);
            for (int i = 0; i < 3; i++) PrecisionMoment.InertiaFrame(ref vx, ref vz, 0f, 0f, 0f, null, 1f / 30f, Run, V, ref watch);
            Assert.That(watch.Reached, Is.True);
            float ux = 0f, uz = Run;
            PrecisionRules.InertiaFrame(ref ux, ref uz, 0f, 0f, 0f, windUp, V);
            Assert.That(watch.Lowest, Is.EqualTo(uz / Run).Within(1e-4f), "the share at 50 ms, as the law gives it run straight to 50 ms");
            // Its lowest inside a step: a counter-strafe held on passes through a stand between two frames' ends.
            float cx = 0f, cz = Run;
            var w = new WindUpWatch();
            w.Start(1f, 0.08f);
            float lowestAtEnds = 1f;
            for (int i = 0; i < 3; i++)
            {
                PrecisionMoment.InertiaFrame(ref cx, ref cz, 0f, -1f, Run, null, 1f / 30f, Run, V, ref w);
                lowestAtEnds = Math.Min(lowestAtEnds, Math.Abs(cz) / Run);
            }
            Assert.That(w.Lowest, Is.LessThan(0.02f), "the stand between the frames' ends");
            Assert.That(w.Lowest, Is.LessThan(lowestAtEnds), "lower than any frame's end");
        }

        [Test]
        public void AStepTheLawDidNotRun_PassesTheWindUpsTimeOn()
        {
            var w = new WindUpWatch();
            w.Start(0.9f, 0.05f);
            w.Pass(0.03f, 0.8f);
            Assert.That(w.Reached, Is.False);
            Assert.That(w.Lowest, Is.EqualTo(0.8f));
            w.Pass(0.03f, 0.7f);
            Assert.That(w.Reached, Is.True);
            Assert.That(w.Lowest, Is.EqualTo(0.7f));
            w.Pass(0.03f, 0.1f);
            Assert.That(w.Lowest, Is.EqualTo(0.7f), "past the moment: not the wind-up's");
        }

        static string D(double v) => v.ToString("0.0", CultureInfo.InvariantCulture) + "°";
        static string Ms(double s) => double.IsNaN(s) ? "none" : (s * 1000).ToString("0", CultureInfo.InvariantCulture) + " ms";
    }
}
