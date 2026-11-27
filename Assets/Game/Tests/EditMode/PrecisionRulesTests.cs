using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 143, precision and momentum, as rules: the cone's curve (accurate at or under 35 % of run speed, a straight line
    /// to 24 degrees at full speed, held there in a sprint), its width at a distance, the deflection's turn, the three ways her speed
    /// reaches it (Inertia's law with its times against research r4's table, at 60 frames a second and at 0.3 ms frames; the meter's
    /// rise, fall and empty; the ghost's CS timing), the counter-strafe's rule, and the draw's bag (centre-weighted, no two outer
    /// draws in a row across bags, the same from the same seed). The mode in use: Off with no source and with the switch off.
    /// </summary>
    public class PrecisionRulesTests
    {
        static readonly PrecisionValues V = new PrecisionValues();
        const float Run = 6f;

        [TearDown]
        public void PutBack()
        {
            PrecisionValues.On = true;
            PrecisionValues.ModeSource = null;
        }

        [Test]
        public void TheCone_AccurateUnder35Percent_ThenAStraightLineTo24_AndHeldInASprint()
        {
            Assert.That(PrecisionRules.Cone(0f, V), Is.EqualTo(0f));
            Assert.That(PrecisionRules.Cone(0.35f, V), Is.EqualTo(0f), "at the accurate share itself: accurate");
            Assert.That(PrecisionRules.Cone(0.65f, V), Is.EqualTo(24f * 0.3f / 0.65f).Within(1e-4f), "research r4: 11.1 degrees at 0.65");
            Assert.That(PrecisionRules.Cone(0.73f, V), Is.EqualTo(14.03f).Within(0.01f), "research r4: 14.0 degrees at 0.73");
            Assert.That(PrecisionRules.Cone(1f, V), Is.EqualTo(24f));
            Assert.That(PrecisionRules.Cone(1.3f, V), Is.EqualTo(24f), "a sprint stays at 24");
            Assert.That(PrecisionRules.MeterCone(0.5f, V), Is.EqualTo(12f));
            Assert.That(PrecisionRules.AfterCut(24f, 0.4f, V), Is.EqualTo(14.4f).Within(1e-4f), "the Laser Sight's −40 %");
            Assert.That(PrecisionRules.AfterCut(24f, 0.9f, V), Is.EqualTo(24f * 0.4f).Within(1e-4f), "capped at 60 %");
        }

        [Test]
        public void TheWidthAtADistance_IsTwoDTimesTheTangentOfTheHalf()
        {
            Assert.That(PrecisionRules.WidthAt(24f, 10f), Is.EqualTo(2.0 * 10.0 * Math.Tan(12.0 * Math.PI / 180.0)).Within(1e-4));
            Assert.That(PrecisionRules.WidthAt(0f, 10f), Is.EqualTo(0f));
            Assert.That(PrecisionRules.WidthAt(10f, 0f), Is.EqualTo(0f));
        }

        [Test]
        public void TheTurn_IsClockwiseSeenFromAbove()
        {
            GroundPoint east = PrecisionRules.Turn(new GroundPoint(0f, 1f), 90f);
            Assert.That(east.X, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(east.Z, Is.EqualTo(0f).Within(1e-5f));
            Assert.That(UpperBodyAim.TwistAngle(new GroundPoint(0f, 1f), PrecisionRules.Turn(new GroundPoint(0f, 1f), 7f)), Is.EqualTo(7f).Within(1e-3f),
                        "the same sign as the upper body's twist");
        }

        // ---------------- Inertia's times ----------------

        /// <summary>What Inertia's law gives at frames of <paramref name="dt"/>: seconds and metres for each of research r4's lines.</summary>
        public struct Times
        {
            public float ToFull, ToFullM, LetGoAccurate, LetGoAccurateM, LetGoStand, LetGoStandM, CounterAccurate, CounterAccurateM, CounterStand, CounterStandM;
        }

        public static Times InertiaTimes(float dt, PrecisionValues v, float run = Run)
        {
            var t = new Times();
            // Stand to full speed.
            float vx = 0f, vz = 0f, time = 0f, metres = 0f;
            while (vz < run - 1e-3f && time < 2f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 1f, run, dt, v);
                time += dt;
                metres += vz * dt;
            }
            t.ToFull = time;
            t.ToFullM = metres;
            // Let go from full speed: to accurate (35 %), to a stand.
            Stop(dt, v, run, 0f, out t.LetGoAccurate, out t.LetGoAccurateM, out t.LetGoStand, out t.LetGoStandM);
            // The opposite key from full speed.
            Stop(dt, v, run, -1f, out t.CounterAccurate, out t.CounterAccurateM, out t.CounterStand, out t.CounterStandM);
            return t;
        }

        static void Stop(float dt, PrecisionValues v, float run, float wish, out float accurate, out float accurateM, out float stand, out float standM)
        {
            float vx = 0f, vz = run, time = 0f, metres = 0f;
            accurate = accurateM = stand = standM = float.NaN;
            while (time < 2f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, wish, run, dt, v);
                time += dt;
                metres += Math.Max(0f, vz) * dt;
                if (float.IsNaN(accurate) && vz <= v.AccurateShare * run) { accurate = time; accurateM = metres; }
                if (vz <= 1e-3f) { stand = time; standM = metres; break; }
            }
        }

        [Test]
        public void Inertia_MatchesResearchR4sProposedColumn_AtSixtyFramesASecond()
        {
            Times t = InertiaTimes(1f / 60f, V);
            TestContext.WriteLine(Describe("60 frames a second", t));
            // Research r4 (60 frames a second): 200 ms / 0.80 m to full; let go 133 ms (0.44 m) to accurate, 233 ms (0.51 m) to a stand;
            // the opposite key 33 ms (0.07 m) to accurate, 50 ms (0.05 m) to a stand. Within a frame or two: the research's script runs
            // the law once a frame, this in small steps.
            Assert.That(t.ToFull, Is.EqualTo(0.200f).Within(0.034f), "stand to full speed");
            Assert.That(t.LetGoAccurate, Is.EqualTo(0.133f).Within(0.034f), "let go: to accurate");
            Assert.That(t.LetGoStand, Is.EqualTo(0.233f).Within(0.034f), "let go: to a stand");
            Assert.That(t.CounterAccurate, Is.EqualTo(0.033f).Within(0.034f), "counter-strafe: to accurate");
            Assert.That(t.CounterStand, Is.EqualTo(0.050f).Within(0.034f), "counter-strafe: to a stand");
            Assert.That(t.CounterAccurate, Is.LessThan(t.LetGoAccurate / 2f), "the opposite key brakes much faster");
        }

        [Test]
        public void Inertia_IsTheSameAtAnyFrameRate()
        {
            Times slow = InertiaTimes(1f / 30f, V), fast = InertiaTimes(0.0003f, V), mid = InertiaTimes(1f / 144f, V);
            TestContext.WriteLine(Describe("30 frames a second", slow));
            TestContext.WriteLine(Describe("144 frames a second", mid));
            TestContext.WriteLine(Describe("0.3 ms frames", fast));
            // The time a threshold is crossed is counted in whole frames, so it differs by at most a frame of the slower run.
            Assert.That(slow.ToFull, Is.EqualTo(fast.ToFull).Within(1f / 30f + 1e-3f));
            Assert.That(slow.LetGoStand, Is.EqualTo(fast.LetGoStand).Within(1f / 30f + 1e-3f));
            Assert.That(mid.CounterAccurate, Is.EqualTo(fast.CounterAccurate).Within(1f / 144f + 1e-3f));
            // The speed after the same time is the same to the step.
            float ax = 0f, az = 0f, bx = 0f, bz = 0f;
            for (int i = 0; i < 6; i++) PrecisionRules.InertiaFrame(ref ax, ref az, 0f, 1f, Run, 1f / 60f, V);
            for (int i = 0; i < 400; i++) PrecisionRules.InertiaFrame(ref bx, ref bz, 0f, 1f, Run, 0.1f / 400f, V);
            Assert.That(az, Is.EqualTo(bz).Within(0.02f), "0.1 s from a stand, in 6 frames or in 400");
        }

        [Test]
        public void Inertia_ReachesTheSprintsSpeed_AndFrictionBringsHerDownToALowerCap()
        {
            float vx = 0f, vz = 0f;
            for (int i = 0; i < 120; i++) PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 1f, 7.8f, 1f / 60f, V);
            Assert.That(vz, Is.EqualTo(7.8f).Within(0.01f), "the sprint's 7.8 m/s is reached: the acceleration scales with the cap above 6");
            vz = 6f;
            float time = 0f;
            while (vz > 3.0001f && time < 1f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 1f, 3f, 1f / 240f, V);
                time += 1f / 240f;
            }
            Assert.That(vz, Is.EqualTo(3f).Within(0.01f), "down to the cap of a shot on the move, and held there");
            Assert.That(time, Is.LessThan(0.15f), "friction brings her down to it within the slow-down's 0.15 s");
        }

        [Test]
        public void Inertia_TurnsAndSlidesAlong_NoSpeedFromNothing()
        {
            float vx = 0f, vz = 0f;
            PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 0f, Run, 0.1f, V);
            Assert.That(vx == 0f && vz == 0f, "no key, no speed");
            vz = Run;
            for (int i = 0; i < 60; i++) PrecisionRules.InertiaFrame(ref vx, ref vz, 1f, 0f, Run, 1f / 60f, V);
            Assert.That(vx, Is.EqualTo(Run).Within(0.01f), "a quarter turn: full speed the new way after a second");
            Assert.That(Math.Abs(vz), Is.LessThan(0.01f), "and nothing left of the old way");
        }

        // ---------------- The ghost ----------------

        [Test]
        public void TheGhost_HasCounterStrikesTiming()
        {
            // Her real velocity is instant: full speed at once, a stand at once, the other way at once.
            float gx = 0f, gz = 0f, dt = 1f / 60f, time = 0f;
            while (gz < Run - 1e-2f && time < 2f)
            {
                PrecisionRules.GhostFrame(ref gx, ref gz, 0f, Run, dt, V);
                time += dt;
            }
            TestContext.WriteLine("ghost: stand to full " + Ms(time));
            Assert.That(time, Is.EqualTo(0.55f).Within(0.05f), "research r4: 550 ms to full at the CS law");
            float accurate = Until(Run, 0f, dt, out float stand);
            TestContext.WriteLine("ghost: let go, accurate after " + Ms(accurate) + ", a stand after " + Ms(stand));
            Assert.That(accurate, Is.EqualTo(0.200f).Within(0.034f), "research r4: accurate 200 ms after letting go");
            Assert.That(stand, Is.EqualTo(0.417f).Within(0.034f), "and a stand after 417 ms");
            float counter = Until(Run, -Run, dt, out _);
            TestContext.WriteLine("ghost: counter-strafe, accurate after " + Ms(counter));
            Assert.That(counter, Is.EqualTo(0.083f).Within(0.034f), "research r4: accurate 83 ms after a counter-strafe");
        }

        static float Until(float from, float real, float dt, out float stand)
        {
            float gx = 0f, gz = from, time = 0f, accurate = float.NaN;
            stand = float.NaN;
            while (time < 2f)
            {
                PrecisionRules.GhostFrame(ref gx, ref gz, 0f, real, dt, V);
                time += dt;
                if (float.IsNaN(accurate) && Math.Abs(gz) <= V.AccurateShare * Run && gz >= -1e-3f) accurate = time;
                if (float.IsNaN(stand) && Math.Abs(gz) <= 1e-3f) stand = time;
                if (!float.IsNaN(accurate) && (real < 0f || !float.IsNaN(stand))) break;
            }
            return accurate;
        }

        // ---------------- The meter ----------------

        [Test]
        public void TheMeter_RisesIn05_FallsIn03()
        {
            float m = 0f;
            for (int i = 0; i < 6; i++) m = PrecisionRules.MeterStep(m, 1f, 1f / 60f, V, V.AccurateShare);
            Assert.That(PrecisionRules.MeterCone(m, V), Is.EqualTo(4.8f).Within(0.01f), "research r4: 4.8 degrees after 0.1 s");
            for (int i = 0; i < 24; i++) m = PrecisionRules.MeterStep(m, 1f, 1f / 60f, V, V.AccurateShare);
            Assert.That(m, Is.EqualTo(1f).Within(1e-4f), "full after 0.5 s");
            for (int i = 0; i < 6; i++) m = PrecisionRules.MeterStep(m, 0f, 1f / 60f, V, V.AccurateShare);
            Assert.That(PrecisionRules.MeterCone(m, V), Is.EqualTo(16f).Within(0.01f), "research r4: 16 degrees 0.1 s after a stand");
            for (int i = 0; i < 12; i++) m = PrecisionRules.MeterStep(m, 0f, 1f / 60f, V, V.AccurateShare);
            Assert.That(m, Is.EqualTo(0f), "empty 0.3 s after a stand");
            Assert.That(PrecisionRules.MeterTarget(0.65f, V.AccurateShare), Is.EqualTo(0.3f / 0.65f).Within(1e-5f));
            Assert.That(PrecisionRules.MeterShare(0f, 0.35f), Is.EqualTo(0.35f));
            Assert.That(PrecisionRules.MeterShare(1f, 0.35f), Is.EqualTo(1f));
        }

        [Test]
        public void ACounterStrafe_IsAKeyMoreThan135DegreesFromHerMotion_AboveTheAccurateShare()
        {
            var north = new GroundPoint(0f, Run);
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(0f, -1f), north, Run, V), Is.True, "straight back");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(0.5f, -1f), north, Run, V), Is.True, "153 degrees");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(1f, -0.84f), north, Run, V), Is.False, "130 degrees: not more than 135");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(1f, -1.2f), north, Run, V), Is.True, "140 degrees");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(1f, 0f), north, Run, V), Is.False, "a quarter turn");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(0f, -1f), new GroundPoint(0f, 0.3f * Run), Run, V), Is.False, "her motion under 35 %");
            Assert.That(PrecisionRules.IsCounterStrafe(new GroundPoint(0f, 0f), north, Run, V), Is.False, "no key");
        }

        // ---------------- The draw ----------------

        [Test]
        public void TheBag_IsCentreWeighted_NoTwoOuterInARow_AcrossBags()
        {
            var bag = new PrecisionBag(7u, V);
            int n = V.BagSize * 200, outer = 0, centre = 0;
            bool last = false;
            double sum = 0.0;
            for (int i = 0; i < n; i++)
            {
                float x = bag.Next();
                Assert.That(x, Is.InRange(-1f, 1f));
                bool o = bag.IsOuter(x);
                Assert.That(o && last, Is.False, "two outer draws in a row at draw " + i);
                last = o;
                if (o) outer++;
                if (Math.Abs(x) <= 0.5f) centre++;
                sum += x;
            }
            // The triangular draw: 1/9 of it past two thirds of the half cone, 3/4 within the inner half.
            Assert.That(outer, Is.EqualTo(n / 9), "exactly the triangular draw's share in every bag");
            Assert.That(centre / (double)n, Is.EqualTo(0.75).Within(0.01), "three quarters within half the half cone");
            Assert.That(sum / n, Is.EqualTo(0.0).Within(0.01), "no side favoured");
        }

        [Test]
        public void TheBag_FromTheSameSeed_DrawsTheSame()
        {
            var a = new PrecisionBag(42u, V);
            var b = new PrecisionBag(42u, V);
            var c = new PrecisionBag(43u, V);
            bool differs = false;
            for (int i = 0; i < 50; i++)
            {
                float x = a.Next();
                Assert.That(b.Next(), Is.EqualTo(x));
                differs |= c.Next() != x;
            }
            Assert.That(differs, Is.True, "another seed, another sequence");
            a.Seed(42u, V);
            b.Seed(42u, V);
            Assert.That(a.Next(), Is.EqualTo(b.Next()), "seeded again, from the start again");
            Assert.That(a.Drawn, Is.EqualTo(1));
        }

        // ---------------- The mode in use ----------------

        [Test]
        public void TheMode_IsOffWithNoSource_AndWithTheSwitchOff()
        {
            PrecisionValues.ModeSource = null;
            Assert.That(PrecisionValues.ModeNow, Is.EqualTo(PrecisionMode.Off), "no source (a rule test): Off");
            PrecisionValues.ModeSource = () => PrecisionMode.Ghost;
            Assert.That(PrecisionValues.ModeNow, Is.EqualTo(PrecisionMode.Ghost));
            PrecisionValues.On = false;
            Assert.That(PrecisionValues.ModeNow, Is.EqualTo(PrecisionMode.Off), "the order's switch off: Off whatever is stored");
            Assert.That(PrecisionValues.DefaultMode, Is.EqualTo(PrecisionMode.Inertia), "his tick: Inertia by default");
        }

        static string Ms(float s) => (s * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms";
        static string M(float m) => m.ToString("0.00", CultureInfo.InvariantCulture) + " m";

        public static string Describe(string title, Times t) =>
            "inertia, " + title + ": to full " + Ms(t.ToFull) + " " + M(t.ToFullM)
            + "; let go: accurate " + Ms(t.LetGoAccurate) + " " + M(t.LetGoAccurateM) + ", a stand " + Ms(t.LetGoStand) + " " + M(t.LetGoStandM)
            + "; counter-strafe: accurate " + Ms(t.CounterAccurate) + " " + M(t.CounterAccurateM) + ", a stand " + Ms(t.CounterStand) + " " + M(t.CounterStandM);
    }
}
