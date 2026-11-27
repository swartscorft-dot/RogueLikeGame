using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 152 in the game: the real basic shot, clicked through the input as he clicks it (the input driver runs before her
    /// frame), fired from a run in the control room under Inertia at two fixed frame steps of game time (1/30 s and 1/144 s,
    /// <see cref="Time.captureDeltaTime"/>). With <see cref="PrecisionValues.SameAtEveryFrameRate"/> on, the same cone at both, on
    /// the law's own continuous value; off, work order 143's frame number for number: the share the shot read is the lowest of the
    /// shares at the ends of the frames from the one before the click to the one before it went off.
    /// </summary>
    public class ConeAtEveryFrameRatePlayTests
    {
        static readonly float[] Steps = { 1f / 30f, 1f / 144f };
        /// <summary>s of game time she runs to full speed before the click.</summary>
        const float RunUp = 0.6f;
        /// <summary>m ahead of her, to her right of the run, where the cursor lies (nothing stands there).</summary>
        const float CursorAhead = 8f;
        /// <summary>Degrees within which the order asks the two to agree.</summary>
        const float Agree = 0.2f;
        /// <summary>The law's own cone for a single shot from a run with the stock gun (ConeAtEveryFrameRateTests, frames of 1/7200 s): 13.9°.</summary>
        const float LawsCone = 13.9f;
        /// <summary>Degrees the scene may differ from the rules' mirror (the agent on the NavMesh carries her body).</summary>
        const float SceneSlack = 0.3f;

        PlayerCharacter _p;
        PlayerInputDriver _driver;
        Vector2 _move;
        bool _click;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false;
            PrecisionSettings.ForTests = PrecisionMode.Inertia;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _p = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _driver.InputSource = Feed;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) d.transform.position = new Vector3(40f, d.transform.position.y, name == "Dummy_B" ? -40f : 40f);
            }
        }

        [TearDown]
        public void Restore()
        {
            if (_driver != null) _driver.InputSource = null;
            Time.captureDeltaTime = 0f;
            Time.timeScale = 1f;
            PrecisionSettings.ForTests = null;
            PrecisionValues.SameAtEveryFrameRate = true;
            PlayerInputDriver.ReadRealDevices = true;
            if (PrecisionSight.Current != null) Object.Destroy(PrecisionSight.Current.gameObject);
        }

        /// <summary>The input's frame: the move keys, the cursor ahead of her, and the click on one frame.</summary>
        InputFrame? Feed()
        {
            Camera cam = Camera.main;
            Vector3 at = _p != null ? _p.Position + new Vector3(1f, 0f, 1f).normalized * CursorAhead : Vector3.zero;
            var f = new InputFrame { Move = _move, CursorScreen = cam != null ? (Vector2)cam.WorldToScreenPoint(at) : Vector2.zero, ShootPressed = _click };
            _click = false;
            return f;
        }

        /// <summary>One shot clicked after a run at one frame step: its precision, and the shares at the ends of the frames around it.</summary>
        IEnumerator FireFromARun(float step, List<float> sharesBeforeItWentOff, PrecisionShot[] shot)
        {
            Time.captureDeltaTime = 0f;
            _move = Vector2.zero;
            _p.SeedPrecision(152u);
            _p.Teleport(new Vector3(-4f, 0f, 0f));
            yield return null;
            yield return null;
            Time.captureDeltaTime = step;
            _move = Vector2.right;
            float t0 = Time.time, deadline = Time.realtimeSinceStartup + 30f;
            while (Time.time - t0 < RunUp && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_p.Speed, Is.GreaterThan(_p.Feel.RunSpeed * 0.95f), "at full speed before the click");
            int fired = _p.BasicShotsFired, started = _p.ActionsStarted;
            sharesBeforeItWentOff.Clear();
            sharesBeforeItWentOff.Add(_p.PrecisionShare); // the end of the frame before the click's
            _click = true; // read by the input driver at the start of the next frame, before her own
            while (_p.BasicShotsFired == fired && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_p.BasicShotsFired == fired) sharesBeforeItWentOff.Add(_p.PrecisionShare);
            }
            Assert.That(_p.ActionsStarted, Is.EqualTo(started + 1), "the click started one shot");
            Assert.That(_p.BasicShotsFired, Is.EqualTo(fired + 1), "and it went off");
            shot[0] = _p.LastPrecisionShot;
            Time.captureDeltaTime = 0f;
            _move = Vector2.zero;
        }

        [UnityTest]
        public IEnumerator AShotFromARun_TheSameConeAt30And144FramesASecond_TheLawsOwn()
        {
            var cones = new float[Steps.Length];
            var shares = new List<float>();
            var shot = new PrecisionShot[1];
            var sb = new StringBuilder("A single shot from a run, Inertia, the stock gun:\n");
            foreach (bool on in new[] { true, false })
            {
                PrecisionValues.SameAtEveryFrameRate = on;
                for (int i = 0; i < Steps.Length; i++)
                {
                    yield return FireFromARun(Steps[i], shares, shot);
                    Assert.That(shot[0].Mode, Is.EqualTo(PrecisionMode.Inertia));
                    Assert.That(shot[0].ShareLowest, Is.LessThanOrEqualTo(shot[0].ShareAtPress + 1e-4f), "the lowest of the wind-up");
                    cones[i] = shot[0].Cone;
                    sb.AppendLine("  " + (on ? "repaired" : "as built") + " at " + Mathf.RoundToInt(1f / Steps[i]) + " fps: "
                                  + cones[i].ToString("0.00", CultureInfo.InvariantCulture) + "° (share "
                                  + shot[0].ShareLowest.ToString("0.000", CultureInfo.InvariantCulture) + ")");
                }
                if (on)
                {
                    Assert.That(cones[0], Is.EqualTo(cones[1]).Within(Agree), "the same cone at 30 and at 144 frames a second");
                    for (int i = 0; i < Steps.Length; i++) Assert.That(cones[i], Is.EqualTo(LawsCone).Within(SceneSlack), "the law's own cone");
                }
            }
            TestContext.WriteLine(sb.ToString());
            Debug.Log("ConeAtEveryFrameRatePlayTests:\n" + sb);
        }

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheShotReadsTheFramesEnds_AsWorkOrder143Built()
        {
            PrecisionValues.SameAtEveryFrameRate = false;
            var shares = new List<float>();
            var shot = new PrecisionShot[1];
            foreach (float step in Steps)
            {
                yield return FireFromARun(step, shares, shot);
                float lowest = float.PositiveInfinity;
                foreach (float s in shares) lowest = Mathf.Min(lowest, s);
                Assert.That(shot[0].ShareAtPress, Is.EqualTo(shares[0]), "the share at the click: the end of the frame before it");
                Assert.That(shot[0].ShareLowest, Is.EqualTo(lowest), "the lowest at the frames' ends up to the one before it went off");
                Assert.That(shot[0].Cone, Is.EqualTo(PrecisionRules.Cone(lowest, PrecisionValues.Live)).Within(1e-4f), "its cone");
            }
        }
    }
}
