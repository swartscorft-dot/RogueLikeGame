using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 154, its number 1, in the ControlRoom scene through the intents: Quick Shot's cooldown running, focus spent, and
    /// every frame a fixed step of game time (Time.captureDeltaTime): 1/30, 1/60 and 1/144 s, 0.2 and 0.33 s (a loaded machine's),
    /// 1 ms (a game running uncapped) and 0.3 ms (the batch run's own frames). At each, with <see cref="ExactCooldowns"/> on (as it
    /// ships) and off (as before the order): what is left of the cooldown at every frame against the rule (what was left after
    /// the cut, run 1.6 times as fast), and the frame it is ready on. On, the rule's at every step; off, the number is written down
    /// (it differs only late in a session, with short frames: the clock this run is at is written with it, and
    /// <c>ExactCooldownsTests</c> proves the off case at every hour with Unity's own clock as a float). Nobody has played it.
    /// </summary>
    public class FocusCooldownFrameStepTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here (FocusFeltTests')
        const int Quick = 0;
        /// <summary>The real seconds one wait may take at most, however busy the machine is (3438 frames of 0.3 ms at a busy machine's 60 ms each: 206 s).</summary>
        const float RealLimit = 600f;

        PlayerCharacter _player;
        Damageable _a;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.MeleeYouRead = false;
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
        }

        [TearDown]
        public void CleanUp()
        {
            ExactCooldowns.On = true;
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator UntilInGameTime(Func<bool> condition, float gameSeconds)
        {
            float end = Time.time + gameSeconds, deadline = Time.realtimeSinceStartup + RealLimit;
            while (!condition() && Time.time < end && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerable<TestCaseData> Steps()
        {
            foreach ((string name, float step) in new[] { ("30fps", 1f / 30f), ("60fps", 1f / 60f), ("144fps", 1f / 144f), ("200ms", 0.2f), ("330ms", 0.33f), ("1ms", 0.001f), ("03ms", 0.0003f) })
                foreach (bool on in new[] { true, false })
                    yield return new TestCaseData(step, on).SetName("FasterCooldown_AtAFixedStep_" + name + (on ? "_On" : "_Off")).Returns(null);
        }

        [UnityTest, Timeout(1500000)]
        [TestCaseSource(nameof(Steps))]
        public IEnumerator FasterCooldown_AtEveryFrameStep_IsTheRules(float step, bool on)
        {
            ExactCooldowns.On = on;
            IdentityValues id = _player.Identity;
            float rate = id.EmpoweredCooldownRate;
            Time.captureDeltaTime = 1f / 60f; // the press and its shot at a person's frames: only the frames after the spend are the step's
            _player.Teleport(NearA);
            yield return null;
            Assert.That(_player.PressSkill(Quick, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return UntilInGameTime(() => _player.Action == ActionKind.None && _player.SkillGateRemaining <= 0f, 3f);
            Assume.That(_player.SkillCooldownRemaining(Quick), Is.GreaterThan(1.5f), "Quick Shot's 3 s running");
            Time.captureDeltaTime = step; // from the next frame on, every frame this long
            yield return null;

            _player.FillFocus();
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            float spentAt = Time.time;
            float afterCut = _player.SkillCooldownRemaining(Quick);
            Assert.That(_player.FocusCooldownRate(Quick), Is.EqualTo(rate).Within(1e-5f));
            float worst = 0f, took = -1f, designed = -1f;
            int frames = 0;
            float deadline = Time.realtimeSinceStartup + RealLimit;
            while (took < 0f && Time.realtimeSinceStartup < deadline && Time.time - spentAt < 4f)
            {
                yield return null;
                frames++;
                float left = _player.SkillCooldownRemaining(Quick);
                float law = Mathf.Max(0f, afterCut - rate * (Time.time - spentAt));
                worst = Mathf.Max(worst, Mathf.Abs(left - law));
                if (designed < 0f && law <= 0f) designed = Time.time - spentAt;
                if (left <= 0f) took = Time.time - spentAt;
            }
            Time.captureDeltaTime = 0f;
            string line = string.Format(Inv, "[154] frames of {0:0.####} s, ExactCooldowns {1}, the clock at the spend {2:0.0} s: {3:0.000} s left after the cut; ready after {4:0.0000} s ({5} frames), the rule's {6:0.0000} s ({7:0.000} s by the rule without frames); what was left at most {8:0.00000} s off the rule's",
                                        step, on ? "on" : "off", spentAt, afterCut, took, frames, designed, afterCut / rate, worst);
            TestContext.WriteLine(line);
            Debug.Log(line);
            Assert.That(took, Is.GreaterThan(0f), "ready within 4 s of game time: " + line);
            if (on)
            {
                Assert.That(took, Is.EqualTo(designed), "on: ready on the rule's frame: " + line);
                Assert.That(worst, Is.LessThan(1e-3f), "on: what was left the rule's at every frame: " + line);
            }
            // Off, young in a session and at the order's steps, today's numbers are the rule's too (ExactCooldownsTests: from 34 minutes
            // on, a frame or two late at 60 or 144 a second; far off with frames under a millisecond).
            else if (spentAt < 2000f && step >= 1f / 144f)
                Assert.That(Mathf.Abs(took - designed), Is.LessThanOrEqualTo(step + 1e-4f), "off, young in a session at the order's steps: " + line);
        }
    }
}
