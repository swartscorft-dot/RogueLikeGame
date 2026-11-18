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
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest]
        public IEnumerator Standing_OneCommandIsOneShot_ASecondShotNeedsASecondCommand()
        {
            _player.Teleport(NearA);
            yield return null;
            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1));
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "three cycles later: still one shot");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "she stands");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.None));

            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 2, 3f);
            Assert.That(_a.HitsTaken, Is.EqualTo(2), "a second command, a second shot");
        }

        [UnityTest]
        public IEnumerator ClickingEveryFrame_FiresOncePerCycle_NeverSooner()
        {
            _player.Teleport(NearA);
            yield return null;
            var hits = new List<float>();
            float start = Time.time;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.time < start + Cycle * 4.5f && Time.realtimeSinceStartup < deadline)
            {
                _player.AttackTarget(_a); // a click every frame: far faster than she can shoot
                int before = _a.HitsTaken;
                yield return null;
                if (_a.HitsTaken > before) hits.Add(Time.time);
            }
            _player.Stop();
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
            _player.AttackTarget(_a);
            yield return null;
            Assume.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "the first shot is in its startup");
            Assume.That(_player.IsInShotRecovery, Is.False);
            _player.AttackTarget(_a); // a click in the first part of the cycle

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
            _player.AttackTarget(_a);
            float start = Time.time;
            int clicks = 1;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (clicks < 10 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (Time.time < start + Cycle * 0.9f) { _player.AttackTarget(_a); clicks++; }
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
            Assume.That(Targeting.InRange(_player.GroundPosition, _b.GroundPosition, _b.Radius, _feel.BasicAttackRange), Is.True, "B is in range too");
            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            Assume.That(_player.IsInShotRecovery, Is.True, "in the first shot's recovery");

            _player.AttackTarget(_a); // waits ...
            _player.AttackTarget(_b); // ... and is replaced
            yield return Until(() => _b.HitsTaken >= 1, 3f);
            yield return new WaitForSeconds(Cycle * 2f);
            Assert.That(_b.HitsTaken, Is.EqualTo(1), "the waiting shot went at the latest target");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "and not at the one it replaced");
        }

        [UnityTest]
        public IEnumerator AWaitingShot_IsDroppedByAMoveOrder() => WaitingShotDroppedBy(() => _player.MoveTo(_player.Position + Vector3.back * 2f));

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
            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            _player.AttackTarget(_a);
            Assert.That(_player.HasPendingShot, Is.True, "a click in the recovery waits for the next shot");

            takeOver();
            Assert.That(_player.HasPendingShot, Is.False, "and is dropped");
            _player.ReleaseSkill(0);
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "no second basic shot");
        }

        [UnityTest]
        public IEnumerator OnTheMove_AWaitingShot_IsDroppedByAMoveOrder()
        {
            _player.Teleport(new Vector3(-6f, 0f, 0f));
            yield return null;
            _player.MoveTo(new Vector3(10f, 0f, 0f));
            yield return Until(() => _player.Position.x >= -1f, 4f);
            Assume.That(Targeting.InRange(_player.GroundPosition, _a.GroundPosition, _a.Radius, _feel.BasicAttackRange), Is.True);

            _player.AttackTarget(_a);
            Assume.That(_player.IsShootingOnTheMove, Is.True);
            _player.AttackTarget(_a); // during the shot's startup: waits
            Assert.That(_player.HasPendingShot, Is.True, "a second click on the move waits");
            _player.MoveTo(new Vector3(10f, 0f, 2f));
            Assert.That(_player.HasPendingShot, Is.False, "a new move order drops it");
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "only the shot already going");
        }

        [UnityTest]
        public IEnumerator OnTheMove_HoldingTheMoveButton_KeepsAWaitingShot()
        {
            _player.Teleport(new Vector3(-6f, 0f, 0f));
            yield return null;
            _player.MoveTo(new Vector3(10f, 0f, 0f));
            yield return Until(() => _player.Position.x >= -1f, 4f);
            Assume.That(Targeting.InRange(_player.GroundPosition, _a.GroundPosition, _a.Radius, _feel.BasicAttackRange), Is.True);

            _player.AttackTarget(_a);
            _player.AttackTarget(_a); // waits
            int shotsBefore = _player.BasicShotsFired;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_player.BasicShotsFired - shotsBefore < 2 && Time.realtimeSinceStartup < deadline)
            {
                _player.SteerTo(new Vector3(10f, 0f, 0f)); // the held right button, every frame
                yield return null;
            }
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(2), "steering with the held button does not drop the waiting shot");
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }
    }
}
