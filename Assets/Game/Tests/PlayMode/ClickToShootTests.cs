using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Click to shoot (Kristian 2026-11-18): every basic shot needs its own attack command, and a command that comes
    /// while a shot is still in its cycle waits for the next shot, one at most. Values come from the settings asset
    /// in the scene; the character is driven through its intent API.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3), Dummy_C at (0, 10).
    /// </summary>
    public class ClickToShootTests
    {
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here, B 6.3 m

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        FeelValues _feel;
        Damageable _a, _b;

        float Cycle => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest]
        public IEnumerator Standing_OneCommandIsOneShot_ASecondShotNeedsASecondCommand()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "three cycles later: still one shot");
            Assert.That(_player.IsMoving, Is.False, "she stands");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));

            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 2, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(2), "a second command, a second shot");
        }

        [UnityTest]
        public IEnumerator ClickingEveryFrame_FiresOncePerCycle_NeverSooner()
        {
            // Work order 154: every frame 1/60 s of game time. A hit is seen on the frame it lands in: with frames of up to 0.1 s
            // beside a full run, the frame a hit was seen in lay up to a frame after it, and two hits seen 0.187 s apart were a
            // cycle apart (2026-11-26).
            Time.captureDeltaTime = FixedStep;
            _player.Teleport(NearA);
            yield return null;
            var hits = new List<float>();
            float start = Time.time;
            float deadline = Time.realtimeSinceStartup + 60f;
            while (Time.time < start + Cycle * 4.5f && Time.realtimeSinceStartup < deadline)
            {
                _player.Shoot(_a.transform.position); // a click every frame: far faster than she can shoot
                int before = _a.HitsTaken;
                yield return null;
                if (_a.HitsTaken > before) hits.Add(Time.time);
            }
            _player.Stop();
            Time.captureDeltaTime = 0f;
            Assert.That(hits.Count, Is.InRange(4, 5), "one shot per cycle over four and a half cycles");
            for (int i = 1; i < hits.Count; i++)
            {
                Assert.That(hits[i] - hits[i - 1], Is.GreaterThanOrEqualTo(Cycle - 0.02f), "shot " + (i + 1) + " no sooner than a cycle after the last");
                Assert.That(hits[i] - hits[i - 1], Is.LessThan(Cycle + 0.06f), "and as soon as the cycle allows");
            }
        }

        [UnityTest]
        public IEnumerator AClickEarlyInTheShot_IsNotLost_ItIsTheNextShot_ACycleLater()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return null;
            Assume.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "the first shot is in its startup");
            Assume.That(_player.IsInShotRecovery, Is.False);
            _player.Shoot(_a.transform.position); // a click in the first part of the cycle

            var hits = new List<float>();
            float deadline = Time.realtimeSinceStartup + 3f;
            while (hits.Count < 2 && Time.realtimeSinceStartup < deadline)
            {
                int before = _a.HitsTaken;
                yield return null;
                if (_a.HitsTaken > before) hits.Add(Time.time);
            }
            Assert.That(hits.Count, Is.EqualTo(2), "the early click gives the next shot");
            Assert.That(hits[1] - hits[0], Is.EqualTo(Cycle).Within(0.04f), "exactly a cycle after the first");
        }

        [UnityTest]
        public IEnumerator TenClicksInOneCycle_GiveOneWaitingShot()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            float start = Time.time;
            int clicks = 1;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (clicks < 10 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (Time.time < start + Cycle * 0.9f) { _player.Shoot(_a.transform.position); clicks++; }
                else break;
            }
            Assume.That(clicks, Is.GreaterThanOrEqualTo(5), "several clicks inside the first cycle");
            yield return new WaitForSeconds(Cycle * 4f);
            Assert.That(_a.HitsTaken, Is.EqualTo(2), clicks + " clicks in one cycle: the shot in progress and one waiting shot");
        }

        [UnityTest]
        public IEnumerator ANewerClick_ReplacesTheWaitingOne_TheShotGoesAtTheLatestTarget()
        {
            _player.Teleport(NearA);
            yield return null;
            Assume.That(InReach(_player.GroundPosition, _b.GroundPosition, _b.Radius, _feel.BasicShotRange), Is.True, "B is in range too");
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assume.That(_player.IsInShotRecovery, Is.True, "in the first shot's recovery");

            _player.Shoot(_a.transform.position); // waits ...
            _player.Shoot(_b.transform.position); // ... and is replaced
            yield return Until(() => _b.HitsTaken >= 1, 3f);
            yield return new WaitForSeconds(Cycle * 2f);
            Assert.That(_b.HitsTaken, Is.EqualTo(1), "the waiting shot went at the latest target");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "and not at the one it replaced");
        }

        [UnityTest]
        public IEnumerator AWaitingShot_IsKept_WhenAMoveKeyIsPressed()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            _player.Shoot(_a.transform.position);
            Assert.That(_player.HasPendingShot, Is.True, "a click in the recovery waits for the next shot");
            _player.Steer(Vector3.back);
            Assert.That(_player.HasPendingShot, Is.True, "a move key does not drop it");
            yield return Until(() => _player.BasicShotsFired >= 2, 2f);
            _player.Stop();
            Assert.That(_player.BasicShotsFired, Is.EqualTo(2), "it fires, on the move");
        }

        [UnityTest]
        public IEnumerator AWaitingShot_IsDroppedByADash() =>
            WaitingShotDroppedBy(() => Assert.That(_player.DashToward(_player.Position + Vector3.back * 5f), Is.EqualTo(IntentResult.Started)));

        [UnityTest]
        public IEnumerator AWaitingShot_IsDroppedByASkill() =>
            WaitingShotDroppedBy(() => Assert.That(_player.PressSkill(0, _player.Position + Vector3.back * 5f), Is.Not.EqualTo(IntentResult.Rejected)));

        IEnumerator WaitingShotDroppedBy(Action takeOver)
        {
            _player.Teleport(NearA);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            _player.Shoot(_a.transform.position);
            Assert.That(_player.HasPendingShot, Is.True, "a click in the recovery waits for the next shot");

            takeOver();
            Assert.That(_player.HasPendingShot, Is.False, "and is dropped");
            _player.ReleaseSkill(0);
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "no second basic shot");
        }

        [UnityTest]
        public IEnumerator OnTheMove_HoldingAMoveKey_KeepsAWaitingShot()
        {
            _player.Teleport(new Vector3(-6f, 0f, 0f));
            yield return null;
            _player.Steer(Vector3.right);
            yield return Until(() => _player.Position.x >= -1f, 4f);
            Assume.That(InReach(_player.GroundPosition, _a.GroundPosition, _a.Radius, _feel.BasicShotRange), Is.True);

            _player.Shoot(_a.transform.position);
            _player.Shoot(_a.transform.position); // waits
            int shotsBefore = _player.BasicShotsFired;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_player.BasicShotsFired - shotsBefore < 2 && Time.realtimeSinceStartup < deadline)
            {
                _player.Steer(Vector3.right); // the held key, every frame, as the input gives it
                yield return null;
            }
            _player.Stop();
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(2), "holding a move key does not drop the waiting shot");
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>
        /// Work order 154: s of game time a frame (Time.captureDeltaTime) in the tests here that judge a rule of the game's time by
        /// its frames: beside a full EditMode run a frame carried up to a third of a second, and these tests failed by it on
        /// 2026-11-26 (alone, every one passed).
        /// </summary>
        const float FixedStep = 1f / 60f;

        /// <summary>True when a target's edge is within a reach of a point on the ground.</summary>
        static bool InReach(GroundPoint from, GroundPoint target, float radius, float reach) => from.DistanceTo(target) - radius <= reach;
    }
}
