using System;
using System.Collections;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Aiming with the upper body and shooting on the move, for the basic shot. Values come from the settings
    /// asset in the scene; the character is driven through its intent API.
    /// Layout: player at (0, 0) facing +Z, Dummy_A at (4, 3), Dummy_B at (-4, 3), Dummy_C at (0, 10).
    /// The runs below go along Z = 0 toward +X, which passes A and B at 3 m to the side.
    /// </summary>
    public class MoveShotTests
    {
        static readonly Vector3 RunFrom = new Vector3(-6f, 0f, 0f);
        static readonly Vector3 RunTo = new Vector3(10f, 0f, 0f);

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        CharacterAnimation _view;
        FeelValues _feel;
        Damageable _a, _b, _c;

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
            Assert.That(_player.Settings, Is.Not.Null, "the player uses the settings asset");
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Shooting on the move ----------------

        [UnityTest]
        public IEnumerator OnTheMove_AnAttackInRange_KeepsTheMove_SlowsForOneShot_ThenRunsOnAtFullSpeed_AndArrives()
        {
            yield return RunUntilX(-1f);
            Assume.That(InRange(_a), Is.True, "A is in basic-attack range here");
            Assume.That(TurnsBodyFor(_a), Is.False, "and within the front angle");

            _player.Shoot(_a.transform.position);
            float startedAt = Time.time;
            Assert.That(_player.IsShootingOnTheMove, Is.True, "it shoots on the move");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move), "the move order stays");
            yield return null;
            yield return null;
            Assert.That(_player.SpeedLimit, Is.LessThan(_feel.RunSpeed), "it starts slowing at once");
            Assert.That(_player.SpeedLimit, Is.GreaterThan(_feel.RunSpeed * _feel.MovingShotSpeedShare + 0.05f), "but eases down, not in a jump");
            Assert.That(_player.ShotTurnsBody, Is.False, "the target is within the front angle");
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.98f), "so the body keeps facing the way it runs");

            float firedAt = -1f;
            float slowAt = startedAt + _feel.MovingShotSlowdown;
            yield return Until(() =>
            {
                if (firedAt < 0f && _a.HitsTaken >= 1) firedAt = Time.time;
                return Time.time >= slowAt;
            }, 2f);
            Assert.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(0.1f), "at the slow share when the slow-down is done");
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(0.4f), "and walking at that speed");

            yield return Until(() => _a.HitsTaken >= 1, 2f);
            if (firedAt < 0f) firedAt = Time.time;
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "one shot, and it hit");
            float due = Mathf.Max(firedAt, slowAt) + _feel.MovingShotRecovery + 0.03f;
            yield return Until(() => Time.time >= due - 0.06f, 3f);
            Assert.That(_player.SpeedLimit, Is.LessThan(_feel.RunSpeed), "not yet at full speed shortly before the recovery time is up");
            yield return Until(() => Time.time >= due, 3f);
            Assert.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed).Within(1e-3f), "back to full speed after the recovery time");
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.4f), "and running at it");

            yield return Until(() => _player.Order == OrderKind.None, 5f);
            Assert.That(FlatDistance(_player.Position, RunTo), Is.LessThan(0.2f), "it arrives where it was going");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "no second shot without a second command");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1));
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnTheMove_AClickAtOpenFloor_IsAShotOnTheMove_TheMoveGoesOn_AndArrives()
        {
            yield return RunUntilX(-3f);
            int shotsBefore = _player.BasicShotsFired;
            _player.Shoot(new Vector3(-3f, 0f, -14f)); // open floor, far beyond the range: no in-range condition any more
            Assert.That(_player.IsShootingOnTheMove, Is.True, "a click while walking is always a shot on the move");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move), "the move order stays");
            yield return Until(() => _player.BasicShotsFired > shotsBefore, 2f);
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(1), "the shot went off, a miss");
            Assert.That(_player.BasicShotHits, Is.EqualTo(0));
            yield return Until(() => _player.Order == OrderKind.None, 6f);
            Assert.That(FlatDistance(_player.Position, RunTo), Is.LessThan(0.2f), "she still arrives where she was going");
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Standing_AnAttack_FiresOnce_AndSheStands()
        {
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            _player.Shoot(_a.transform.position);
            Assert.That(_player.IsShootingOnTheMove, Is.False);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "a click gives no order: she does not walk");
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            float cycle = _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;
            yield return new WaitForSeconds(cycle * 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "standing, one command is one shot (click to shoot)");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None));
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OnTheMove_AnAimBehindHer_TurnsTheBodyForTheShot_WhileItKeepsGoing_ThenFacesTheMoveAgain()
        {
            yield return RunUntilX(-3.4f);
            Vector3 behind = _player.Position + Vector3.left * 8f;
            float xAtCommand = _player.Position.x;
            int shots = _player.BasicShotsFired;

            _player.Shoot(behind);
            Assert.That(_player.IsShootingOnTheMove, Is.True);
            Assert.That(_player.ShotTurnsBody, Is.True, "straight behind is outside the front angle");
            yield return new WaitForSeconds(_feel.ShotTurnTime + 0.05f);
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.left), Is.GreaterThan(0.98f), "the body has turned to the aim, at the shot turn rate");
            yield return Until(() => _player.BasicShotsFired > shots, 2f);
            Assert.That(_player.Position.x, Is.GreaterThan(xAtCommand + 0.3f), "it kept going along its path meanwhile");
            Assert.That(Mathf.Abs(_player.Position.z), Is.LessThan(0.3f), "on its path");

            yield return Until(() => _player.FacingMode == ShotFacing.Mode.Free, 3f);
            yield return null;
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.98f), "then faces the way it goes again");
        }

        [UnityTest]
        public IEnumerator OnTheMove_ANewMoveOrderDuringTheWindUp_ChangesTheWay_ButDoesNotCancelTheShot()
        {
            yield return RunUntilX(-1f);
            _player.Shoot(_a.transform.position);
            Assume.That(_player.IsShootingOnTheMove, Is.True);
            var newTarget = new Vector3(8f, 0f, -4f);
            _player.MoveTo(newTarget);
            Assert.That(_player.IsShootingOnTheMove, Is.True, "the shot goes on");

            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "and goes off");
            yield return Until(() => _player.Order == OrderKind.None, 5f);
            Assert.That(FlatDistance(_player.Position, newTarget), Is.LessThan(0.2f), "to the new destination");
        }

        [UnityTest]
        public IEnumerator OnTheMove_ADashDuringTheWindUp_CancelsTheShot()
        {
            yield return RunUntilX(-1f);
            _player.Shoot(_a.transform.position);
            Assume.That(_player.IsShootingOnTheMove, Is.True);
            Assert.That(_player.DashToward(RunTo), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsShootingOnTheMove, Is.False);

            float due = Time.time + _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery + 0.1f;
            yield return Until(() => Time.time >= due, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(0), "the shot never went off");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OnTheMove_ASkill_StopsFacesAndLocksAsBefore()
        {
            yield return RunUntilX(-1f);
            Assert.That(_player.PressSkill(0, _a.transform.position), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsLocked, Is.True, "a skill locks");
            Assert.That(_player.IsShootingOnTheMove, Is.False);
            yield return null;
            Assert.That(_player.Speed, Is.LessThan(0.2f), "and stops the character");
            if (_feel.FaceCursorOnCast)
                Assert.That(Vector3.Dot(Flat(_player.transform.forward), Flat(_a.transform.position - _player.Position).normalized),
                            Is.GreaterThan(0.99f), "facing the aim point");
        }

        // ---------------- Aiming with the upper body ----------------

        [UnityTest]
        public IEnumerator Standing_ATargetWithinTheFrontAngle_TurnsOnlyTheUpperBody_AndBothPistolsPointAtIt()
        {
            Assume.That(_view != null && _view.RightWeapon != null && _view.LeftWeapon != null, Is.True, "a figure with pistols");
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            Vector3 facing = Flat(_player.transform.forward);
            Assume.That(TurnsBodyFor(_a), Is.False, "A is within the front angle");
            Assume.That(Mathf.Abs(Vector3.SignedAngle(facing, Flat(_a.transform.position - _player.Position), Vector3.up)), Is.GreaterThan(20f),
                        "and well to the side");

            _player.Shoot(_a.transform.position);
            yield return Until(() => _view.AimWeight >= 1f, 2f);
            yield return null;
            Assert.That(_player.ShotTurnsBody, Is.False);
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), facing), Is.GreaterThan(0.999f), "the body has not turned");
            Assert.That(Mathf.Abs(_view.Twist), Is.GreaterThan(10f), "the upper body has");
            Vector3 toTarget = Flat(_a.transform.position - _player.Position).normalized;
            foreach (Transform weapon in new[] { _view.RightWeapon, _view.LeftWeapon })
                Assert.That(Vector3.Dot(weapon.right, toTarget), Is.GreaterThan(0.97f), weapon.name + " points at the target");
        }

        [UnityTest]
        public IEnumerator Standing_AnAimOutsideTheFrontAngle_TurnsTheWholeBody()
        {
            _player.Teleport(new Vector3(0f, 0f, 3.5f));
            yield return null;
            Vector3 behind = _player.Position - Flat(_player.transform.forward) * 6f;
            _player.Shoot(behind);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 1f);
            Assert.That(_player.ShotTurnsBody, Is.True, "straight behind is outside the front angle");
            yield return new WaitForSeconds(_feel.ShotTurnTime + 0.05f);
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Flat(behind - _player.Position).normalized),
                        Is.GreaterThan(0.99f), "the body has turned to the aim");
        }

        // ---------------- Helpers ----------------

        /// <summary>Starts the run from <see cref="RunFrom"/> to <see cref="RunTo"/> and waits until the character has passed x.</summary>
        // ---------------- Fire rate, and the shooting gait ----------------

        [UnityTest]
        public IEnumerator FireRate_IsFromTheSettings_TwoCommandsACycleApartGiveTwoShots_AFasterPairToo_ButNotSooner()
        {
            float cycle = _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

            // Two commands a cycle apart: two shots, a cycle apart.
            yield return RunUntilX(-1f);
            Assume.That(InRange(_a), Is.True, "A is in basic-attack range here");
            int hitsBefore = _a.HitsTaken;
            var hitTimes = new System.Collections.Generic.List<float>();
            _player.Shoot(_a.transform.position);
            float first = Time.time;
            bool secondGiven = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (hitTimes.Count < 2 && Time.realtimeSinceStartup < deadline)
            {
                if (!secondGiven && Time.time >= first + cycle + 0.01f) { _player.Shoot(_a.transform.position); secondGiven = true; }
                yield return null;
                if (_a.HitsTaken - hitsBefore > hitTimes.Count) hitTimes.Add(Time.time);
            }
            Assert.That(hitTimes.Count, Is.EqualTo(2), "two commands a cycle apart give two shots");
            Assert.That(hitTimes[1] - hitTimes[0], Is.GreaterThanOrEqualTo(cycle - 0.02f), "no closer together than a cycle");
            Assert.That(hitTimes[1] - hitTimes[0], Is.LessThan(cycle + 0.12f), "and the second as soon as it was given");
            Assert.That(hitTimes[0] - first, Is.LessThan(_feel.BasicShotStartup + 0.06f), "the first goes off after the startup from the settings");

            // Two commands closer together than the cycle allows, the second early in the first shot's cycle:
            // the second is not lost; it waits and goes off a whole cycle after the first, not sooner (click to shoot).
            yield return RunUntilX(-1f);
            hitsBefore = _a.HitsTaken;
            hitTimes.Clear();
            _player.Shoot(_a.transform.position);
            first = Time.time;
            yield return Until(() => Time.time >= first + cycle * 0.2f, 1f);
            _player.Shoot(_a.transform.position);
            deadline = Time.realtimeSinceStartup + 3f;
            while (hitTimes.Count < 2 && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                if (_a.HitsTaken - hitsBefore > hitTimes.Count) hitTimes.Add(Time.time);
            }
            Assert.That(hitTimes.Count, Is.EqualTo(2), "a faster pair gives two shots: the second click waited");
            Assert.That(hitTimes[1] - hitTimes[0], Is.GreaterThanOrEqualTo(cycle - 0.02f), "the second no sooner than a cycle after the first");
            Assert.That(hitTimes[1] - hitTimes[0], Is.LessThan(cycle + 0.06f), "and as soon as the cycle allows");
            yield return new WaitForSeconds(cycle * 2f);
            Assert.That(_a.HitsTaken - hitsBefore, Is.EqualTo(2), "and no third");
        }

        [UnityTest]
        public IEnumerator OnTheMove_TheLegsShowTheShootingGait_ThenTheRunAgain()
        {
            CharacterAnimation view = RiggedView();
            yield return RunUntilX(-1f);
            Assume.That(TurnsBodyFor(_a), Is.False, "A is within the front angle");
            Assert.That(InState(view, CharacterAnimation.RunState), Is.True, "running shows the run");

            _player.Shoot(_a.transform.position);
            yield return Until(() => view.Pose == CharacterPose.ShootMove, 0.5f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.ShootMove), "a shot on the move shows the shooting gait");
            yield return new WaitForSeconds(_feel.ShootGaitFade + 0.02f);
            Assert.That(_player.InShootingGait, Is.True, "still slowed by the shot");
            Assert.That(InState(view, CharacterAnimation.ShootMoveState), Is.True, "the Animator is in the shooting gait");
            float expected = view.GaitRate(view.ShootRunClipSpeed);
            Assert.That(view.Animator.speed, Is.EqualTo(expected).Within(expected * 0.25f + 0.05f), "its steps are played at her actual speed");

            yield return Until(() => !_player.InShootingGait, 2f);
            Assert.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed).Within(1e-3f), "the gait ends when the speed is back up");
            yield return Until(() => view.Pose == CharacterPose.Run, 0.5f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Run), "and the run comes back");
            yield return new WaitForSeconds(_feel.ShootGaitFade + 0.05f);
            Assert.That(InState(view, CharacterAnimation.RunState), Is.True, "the Animator is in Run again");
        }

        [UnityTest]
        public IEnumerator OnTheMove_ChainedShots_StayInTheShootingGait()
        {
            CharacterAnimation view = RiggedView();
            float cycle = _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;
            yield return RunUntilX(-1.5f);
            Assume.That(InRange(_a), Is.True, "A is in basic-attack range here");

            int shotsBefore = _player.BasicShotsFired, given = 0;
            float start = Time.time;
            bool inGait = false, leftGait = false, leftAnimatorGait = false;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (_player.BasicShotsFired - shotsBefore < 3 && Time.realtimeSinceStartup < deadline)
            {
                if (given < 3 && Time.time >= start + given * (cycle + 0.03f)) { _player.Shoot(_a.transform.position); given++; }
                yield return null;
                if (view.Pose == CharacterPose.ShootMove) inGait = true;
                else if (inGait) leftGait = true;
                if (inGait && Time.time > start + _feel.ShootGaitFade + 0.05f && !InState(view, CharacterAnimation.ShootMoveState))
                    leftAnimatorGait = true;
            }
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(3), "three shots on the move");
            Assert.That(_player.ShotsOnTheMove, Is.GreaterThanOrEqualTo(3));
            Assert.That(inGait, Is.True, "in the shooting gait");
            Assert.That(leftGait, Is.False, "and never back in the run between the shots");
            Assert.That(leftAnimatorGait, Is.False, "nor did the Animator leave the gait");
        }

        [UnityTest]
        public IEnumerator OnTheMove_ATargetBehind_ShowsTheBackwardGait_ThenTheRun()
        {
            CharacterAnimation view = RiggedView();
            yield return RunUntilX(-3.4f);
            _player.Shoot(_player.Position + Vector3.left * 8f); // straight behind her

            yield return Until(() => view.Pose == CharacterPose.ShootBack, 0.5f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.ShootBack), "moving away from a target behind shows the backward gait");
            Assert.That(Vector3.Dot(Flat(_player.Velocity), Flat(_player.transform.forward)), Is.LessThan(0f), "she moves against where the body faces");
            yield return new WaitForSeconds(_feel.ShootGaitFade + 0.02f);
            Assert.That(InState(view, CharacterAnimation.ShootBackState), Is.True, "the Animator is in the backward gait");
            Assert.That(view.Animator.speed, Is.EqualTo(view.GaitRate(view.ShootBackClipSpeed)).Within(0.3f), "its steps played at her actual speed");

            yield return Until(() => !_player.InShootingGait, 2f);
            yield return Until(() => view.Pose == CharacterPose.Run, 0.5f);
            Assert.That(view.Pose, Is.EqualTo(CharacterPose.Run), "and the run comes back");
            yield return new WaitForSeconds(_feel.ShootGaitFade + 0.05f);
            Assert.That(InState(view, CharacterAnimation.RunState), Is.True, "the Animator is in Run again");
        }

        CharacterAnimation RiggedView()
        {
            if (_view == null) Assert.Ignore("the scene was built without a rigged player model");
            return _view;
        }

        static bool InState(CharacterAnimation view, string stateName)
        {
            Animator animator = view.Animator;
            return animator.GetCurrentAnimatorStateInfo(0).IsName(stateName)
                || (animator.IsInTransition(0) && animator.GetNextAnimatorStateInfo(0).IsName(stateName));
        }

        IEnumerator RunUntilX(float x)
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.MoveTo(RunTo);
            yield return Until(() => _player.Position.x >= x, 4f);
            Assume.That(_player.Position.x, Is.GreaterThanOrEqualTo(x), "the character got there");
            Assume.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.4f), "at full speed");
        }

        bool InRange(Damageable d) => _player.GroundPosition.DistanceTo(d.GroundPosition) - d.Radius <= _feel.BasicShotRange;

        bool TurnsBodyFor(Damageable d)
        {
            Vector3 f = Flat(_player.transform.forward);
            Vector3 t = Flat(d.transform.position - _player.Position);
            return UpperBodyAim.BodyMustTurn(new GroundPoint(f.x, f.z), new GroundPoint(t.x, t.z), _feel.ShotFrontAngle);
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
