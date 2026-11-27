using System;
using System.Collections;
using System.Globalization;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 143, precision and momentum, in the game: Off is today's game (and the mode with nobody at the keys); Inertia's
    /// times measured on her body against the rules' (to full speed, letting go, the counter-strafe) and a dash that exits at run
    /// speed; a shot on the move turned by the bag's draw inside its cone, a standing shot straight; a miss the cone caused counted,
    /// the straight line's hit with it; a counter-strafe told and the meter emptied by it; the sight's brackets as wide as the cone
    /// at the cursor, its settle tick, nothing allocated in its frame. Her runs go along Z = 0 toward +X from (-8, 0).
    /// </summary>
    public class PrecisionPlayTests
    {
        static readonly Vector3 Start = new Vector3(-8f, 0f, 0f);
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _p;
        FeelValues _feel;
        Damageable _a, _b, _c;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            PrecisionSettings.ForTests = null;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _p = Object.FindAnyObjectByType<PlayerCharacter>();
            _feel = _p.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            Place(_a, 40f, 40f);
            Place(_b, -40f, 40f);
            Place(_c, 40f, -40f);
            _p.Teleport(Start);
            yield return null;
        }

        [TearDown]
        public void Restore()
        {
            Time.timeScale = 1f;
            PrecisionSettings.ForTests = null;
            PrecisionValues.On = true;
            PlayerInputDriver.ReadRealDevices = true;
            if (PrecisionSight.Current != null) Object.Destroy(PrecisionSight.Current.gameObject);
        }

        // ---------------- Off ----------------

        [UnityTest]
        public IEnumerator Off_IsTodaysGame_AndTheModeWithNobodyAtTheKeys()
        {
            Assert.That(PrecisionSettings.ModeNow, Is.EqualTo(PrecisionMode.Off), "nobody at the keys and no test's mode: Off");
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            PrecisionValues.On = false;
            Assert.That(PrecisionSettings.ModeNow, Is.EqualTo(PrecisionMode.Off), "the order's switch off: Off whatever is asked");
            PrecisionValues.On = true;
            PrecisionSettings.ForTests = PrecisionMode.Off;
            _p.Steer(Vector3.right);
            yield return null;
            yield return null;
            Assert.That(_p.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.01f), "instant to full speed, as today");
            // A stand as today: the agent reads standing within a frame or two of the keys let go (Halt, then its own step).
            int frames = 0;
            for (; frames < 6 && (frames == 0 || _p.Speed > 1e-3f); frames++)
            {
                _p.Steer(Vector3.zero);
                yield return null;
            }
            Assert.That(_p.Speed, Is.EqualTo(0f).Within(1e-3f), "a stand, as today");
            Assert.That(frames, Is.LessThanOrEqualTo(3), "within a frame or two, not slid to a stand");
            Place(_a, _p.Position.x, 8f);
            _p.Steer(Vector3.right);
            yield return RunFor(Vector3.right, 0.3f);
            int fired = _p.BasicShotsFired;
            _p.Shoot(_a.transform.position);
            yield return Until(() => _p.BasicShotsFired > fired, 2f, Vector3.right, _a);
            Assert.That(_p.LastPrecisionShot.Mode, Is.EqualTo(PrecisionMode.Off), "nothing worked out for the shot");
            Assert.That(_p.PrecisionCone, Is.EqualTo(0f));
            Assert.That(_p.ConeMisses, Is.EqualTo(0));
        }

        // ---------------- Inertia ----------------

        [UnityTest]
        public IEnumerator Inertia_HerBodysTimes_AreTheRules()
        {
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            float run = _feel.RunSpeed;
            yield return null;
            Assert.That(_p.Speed, Is.EqualTo(0f).Within(1e-3f), "she stands");
            // Stand to full speed.
            float t0 = Time.time, x0 = _p.Position.x, toFull = -1f, toFullM = 0f, firstFrame = -1f;
            while (Time.time - t0 < 2f)
            {
                _p.Steer(Vector3.right);
                yield return null;
                if (firstFrame < 0f) firstFrame = _p.Speed;
                if (_p.Speed >= run - 0.02f) { toFull = Time.time - t0; toFullM = _p.Position.x - x0; break; }
            }
            yield return RunFor(Vector3.right, 0.2f);
            // Let go.
            t0 = Time.time;
            x0 = _p.Position.x;
            float letAccurate = -1f, letStand = -1f, letStandM = 0f;
            while (Time.time - t0 < 2f)
            {
                _p.Steer(Vector3.zero);
                yield return null;
                if (letAccurate < 0f && _p.Speed <= 0.35f * run) letAccurate = Time.time - t0;
                if (_p.Speed < 0.02f) { letStand = Time.time - t0; letStandM = _p.Position.x - x0; break; }
            }
            yield return RunFor(Vector3.right, 0.5f);
            // The opposite key.
            t0 = Time.time;
            x0 = _p.Position.x;
            float counterAccurate = -1f, counterStand = -1f, counterStandM = 0f;
            while (Time.time - t0 < 2f)
            {
                _p.Steer(Vector3.left);
                yield return null;
                float vx = _p.Velocity.x;
                if (counterAccurate < 0f && vx <= 0.35f * run) counterAccurate = Time.time - t0;
                if (vx <= 0.02f) { counterStand = Time.time - t0; counterStandM = _p.Position.x - x0; break; }
            }
            _p.Steer(Vector3.zero);
            PrecisionRulesTestsTimes rules = PrecisionRulesTestsTimes.Of(Time.deltaTime > 0f ? Time.deltaTime : 0.0003f);
            string line = "Inertia in the game (frames of " + (Time.deltaTime * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms): to full " + Ms(toFull) + " " + M(toFullM)
                          + "; let go: accurate " + Ms(letAccurate) + ", a stand " + Ms(letStand) + " " + M(letStandM)
                          + "; counter-strafe: accurate " + Ms(counterAccurate) + ", a stand " + Ms(counterStand) + " " + M(counterStandM)
                          + "   (the rules: " + Ms(rules.ToFull) + ", " + Ms(rules.LetGoAccurate) + ", " + Ms(rules.LetGoStand) + ", " + Ms(rules.CounterAccurate) + ", " + Ms(rules.CounterStand) + ")";
            TestContext.WriteLine(line);
            Debug.Log("PrecisionPlayTests: " + line);
            Assert.That(firstFrame, Is.LessThan(run * 0.5f), "not at full speed in the first frame: she accelerates");
            const float within = 0.03f;
            Assert.That(toFull, Is.EqualTo(rules.ToFull).Within(within), "stand to full speed");
            Assert.That(letAccurate, Is.EqualTo(rules.LetGoAccurate).Within(within), "let go: to accurate");
            Assert.That(letStand, Is.EqualTo(rules.LetGoStand).Within(within), "let go: to a stand");
            Assert.That(counterAccurate, Is.EqualTo(rules.CounterAccurate).Within(within), "counter-strafe: to accurate");
            Assert.That(counterStand, Is.EqualTo(rules.CounterStand).Within(within), "counter-strafe: to a stand");
        }

        [UnityTest]
        public IEnumerator Inertia_ADashExitsAtRunSpeed_AndSheSlidesToAStand()
        {
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            yield return null;
            Assert.That(_p.DashToward(_p.Position + Vector3.right * 6f), Is.EqualTo(IntentResult.Started));
            float deadline = Time.realtimeSinceStartup + 5f;
            while (_p.IsDashing && Time.realtimeSinceStartup < deadline) yield return null;
            yield return null;
            Assert.That(_p.Speed, Is.GreaterThan(_feel.RunSpeed * 0.8f), "out of the dash at about run speed, not a free stop");
            Assert.That(_p.Velocity.x, Is.GreaterThan(0f), "along the dash");
            yield return Wait(0.5f);
            Assert.That(_p.Speed, Is.LessThan(0.02f), "and to a stand within half a second, no key held");
        }

        // ---------------- The shot's cone ----------------

        [UnityTest]
        public IEnumerator AShotOnTheMove_IsTurnedByTheBagsDraw_AStandingShotGoesStraight()
        {
            PrecisionSettings.ForTests = PrecisionMode.Ghost;
            const uint seed = 5u;
            _p.SeedPrecision(seed);
            float expected = new PrecisionBag(seed, PrecisionValues.Live).Next();
            yield return RunFor(Vector3.right, 0.8f);
            Place(_a, _p.Position.x + 3f, 7f);
            int fired = _p.BasicShotsFired;
            _p.Shoot(_a.transform.position);
            yield return Until(() => _p.BasicShotsFired > fired, 2f, Vector3.right, _a);
            PrecisionShot s = _p.LastPrecisionShot;
            Assert.That(s.Mode, Is.EqualTo(PrecisionMode.Ghost));
            Assert.That(s.Cone, Is.GreaterThan(5f), "a cone on the move");
            Assert.That(s.ShareLowest, Is.LessThanOrEqualTo(s.ShareAtPress + 1e-4f), "the lowest of the wind-up");
            Assert.That(s.Deflection, Is.EqualTo(expected * s.Cone * 0.5f).Within(1e-3f), "the bag's first draw times the half cone");
            Assert.That(s.StraightHit, Is.True, "the straight line meets the dummy");
            // Standing a second: the ghost settles; the shot is not drawn.
            yield return RunFor(Vector3.zero, 1f);
            Assert.That(_p.PrecisionCone, Is.EqualTo(0f), "accurate at a stand");
            int hits = _a.HitsTaken;
            fired = _p.BasicShotsFired;
            _p.Shoot(_a.transform.position);
            yield return Until(() => _p.BasicShotsFired > fired, 2f, Vector3.zero, _a);
            Assert.That(_p.LastPrecisionShot.Cone, Is.EqualTo(0f));
            Assert.That(_p.LastPrecisionShot.Deflection, Is.EqualTo(0f));
            Assert.That(_a.HitsTaken, Is.EqualTo(hits + 1), "a standing shot hits where it aims");
        }

        [UnityTest]
        public IEnumerator AMissTheConeCaused_IsCounted_WithTheStraightLinesHit()
        {
            PrecisionSettings.ForTests = PrecisionMode.Ghost;
            uint seed = 1u;
            for (; seed < 5000u; seed++)
                if (new PrecisionBag(seed, PrecisionValues.Live).Next() > 0.95f) break;
            _p.SeedPrecision(seed);
            yield return RunFor(Vector3.right, 0.8f);
            Place(_a, _p.Position.x, 9.5f);
            int fired = _p.BasicShotsFired, hits = _a.HitsTaken, misses = _p.ConeMisses;
            _p.Shoot(_a.transform.position);
            yield return Until(() => _p.BasicShotsFired > fired, 2f, Vector3.right, _a);
            PrecisionShot s = _p.LastPrecisionShot;
            TestContext.WriteLine("seed " + seed + ": cone " + s.Cone + ", deflection " + s.Deflection);
            Assume.That(s.Cone, Is.GreaterThan(14f), "a wide cone at the shot");
            Assert.That(s.StraightHit, Is.True, "the straight line would have hit");
            Assert.That(s.ConeMiss, Is.True, "the deflected one missed");
            Assert.That(_p.ConeMisses, Is.EqualTo(misses + 1));
            Assert.That(_a.HitsTaken, Is.EqualTo(hits), "the dummy took nothing");
            Assert.That(s.NearestDistance, Is.EqualTo(9.5f).Within(0.6f), "the distance to the target nearest the aim");
        }

        // ---------------- The counter-strafe and the meter ----------------

        [UnityTest]
        public IEnumerator ACounterStrafe_IsTold_AndEmptiesTheMeter()
        {
            PrecisionSettings.ForTests = PrecisionMode.Meter;
            int told = 0;
            Action<PlayerReport> listen = r => { if (r.Kind == PlayerReportKind.CounterStrafed) told++; };
            _p.Reported += listen;
            try
            {
                yield return RunFor(Vector3.right, 0.7f);
                Assert.That(_p.Momentum, Is.EqualTo(1f).Within(0.01f), "full after a run");
                int before = _p.CounterStrafes;
                _p.Steer(Vector3.left);
                yield return null;
                Assert.That(_p.CounterStrafes, Is.EqualTo(before + 1), "the opposite key against her run");
                Assert.That(told, Is.EqualTo(1), "and told");
                Assert.That(_p.Momentum, Is.LessThan(0.01f), "the meter emptied at once");
                Assert.That(_p.PrecisionCone, Is.LessThan(0.3f), "accurate at once");
                yield return RunFor(Vector3.left, 0.1f);
                Assert.That(_p.CounterStrafes, Is.EqualTo(before + 1), "held on: no second one");
                Assert.That(_p.PrecisionCone, Is.EqualTo(4.8f).Within(0.6f), "held on, it builds again: research r4's 4.8 degrees after 0.1 s");
            }
            finally
            {
                _p.Reported -= listen;
            }
        }

        // ---------------- The sight ----------------

        [UnityTest]
        public IEnumerator TheSight_IsAsWideAsTheConeAtTheCursor_TicksOnASettle_AndAllocatesNothing()
        {
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            PrecisionSight sight = PrecisionSight.Ensure();
            yield return RunFor(Vector3.right, 0.5f, aim: new Vector3(4f, 0f, 8f));
            _p.Steer(Vector3.right);
            _p.AimAt(_p.Position + new Vector3(0f, 0f, 8f));
            yield return null;
            sight.Tick();
            ConeAtTheCursor c = sight.Cone;
            Assert.That(c.Shown, Is.True);
            Assert.That(c.Degrees, Is.EqualTo(24f).Within(0.5f), "full run speed: the full cone");
            Assert.That(c.Distance, Is.EqualTo(8f).Within(0.3f), "her distance to the cursor");
            Assert.That(c.Metres, Is.EqualTo(PrecisionRules.WidthAt(c.Degrees, c.Distance)).Within(1e-3f), "the cone's real width there");
            Assert.That(c.Pixels, Is.GreaterThan(10f), "open on the screen");
            Assert.That(Math.Abs(c.AlongX * c.AcrossX + c.AlongY * c.AcrossY), Is.LessThan(1e-3f), "the brackets stand across the aim");
            Assert.That(sight.PracticeExtras, Is.True, "the control room is no run: the practice room's extras show");
            Assert.That(() => sight.Tick(), NotAllocating, "nothing allocated in its frame");
            int ticks = sight.Ticks;
            yield return RunFor(Vector3.zero, 0.5f);
            sight.Tick();
            Assert.That(sight.Cone.Pixels, Is.EqualTo(0f).Within(1e-3f), "closed at a stand");
            Assert.That(sight.Ticks, Is.EqualTo(ticks + 1), "one soft tick as she crossed into accuracy");
            PrecisionSettings.ForTests = PrecisionMode.Off;
            yield return null;
            sight.Tick();
            Assert.That(sight.Cone.Shown, Is.False, "Off: nothing drawn");
        }

        // ---------------- Helpers ----------------

        IEnumerator RunFor(Vector3 dir, float seconds, Vector3? aim = null)
        {
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 20f;
            while (Time.time - t0 < seconds && Time.realtimeSinceStartup < deadline)
            {
                _p.Steer(dir);
                if (aim.HasValue) _p.AimAt(_p.Position + aim.Value);
                yield return null;
            }
        }

        IEnumerator Wait(float seconds)
        {
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 20f;
            while (Time.time - t0 < seconds && Time.realtimeSinceStartup < deadline) yield return null;
        }

        IEnumerator Until(Func<bool> done, float seconds, Vector3 dir, Damageable aimAt)
        {
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 20f;
            while (!done() && Time.time - t0 < seconds && Time.realtimeSinceStartup < deadline)
            {
                _p.Steer(dir);
                if (aimAt != null) _p.AimAt(aimAt.transform.position);
                yield return null;
            }
            Assert.That(done(), Is.True, "in time");
        }

        static void Place(Damageable d, float x, float z)
        {
            Vector3 p = d.transform.position;
            d.transform.position = new Vector3(x, p.y, z);
        }

        static string Ms(float s) => (s * 1000f).ToString("0", CultureInfo.InvariantCulture) + " ms";
        static string M(float m) => m.ToString("0.00", CultureInfo.InvariantCulture) + " m";
    }

    /// <summary>Inertia's times from the rules, at a frame length, for the game's own measure to be held against.</summary>
    public struct PrecisionRulesTestsTimes
    {
        public float ToFull, LetGoAccurate, LetGoStand, CounterAccurate, CounterStand;

        public static PrecisionRulesTestsTimes Of(float dt)
        {
            PrecisionValues v = PrecisionValues.Live;
            const float run = 6f;
            var t = new PrecisionRulesTestsTimes();
            float vx = 0f, vz = 0f, time = 0f;
            while (vz < run - 0.02f && time < 2f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, 1f, run, dt, v);
                time += dt;
            }
            t.ToFull = time;
            Stop(dt, v, run, 0f, out t.LetGoAccurate, out t.LetGoStand);
            Stop(dt, v, run, -1f, out t.CounterAccurate, out t.CounterStand);
            return t;
        }

        static void Stop(float dt, PrecisionValues v, float run, float wish, out float accurate, out float stand)
        {
            float vx = 0f, vz = run, time = 0f;
            accurate = stand = -1f;
            while (time < 2f)
            {
                PrecisionRules.InertiaFrame(ref vx, ref vz, 0f, wish, run, dt, v);
                time += dt;
                if (accurate < 0f && vz <= 0.35f * run) accurate = time;
                if (vz <= 0.02f) { stand = time; break; }
            }
        }
    }
}
