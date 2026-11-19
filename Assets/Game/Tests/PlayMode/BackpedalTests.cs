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
    /// Shooting behind her while she runs (decision 0002 trial, Kristian 2026-11-18): a shot that must turn the body
    /// turns it at the shot turn rate, she stays turned toward the aim, backpedalling, for a hold time after the last
    /// shot, and then turns back to her run at the same rate. Inside the front angle the body never turns.
    /// The runs go along Z = 0 toward +X from (-8, 0); the dummies are moved where a test needs them.
    /// </summary>
    public class BackpedalTests
    {
        static readonly Vector3 RunFrom = new Vector3(-8f, 0f, 0f);
        static readonly Vector3 RunTo = new Vector3(14f, 0f, 0f);

        PlayerCharacter _player;
        CharacterAnimation _view;
        FeelValues _feel;
        Damageable _a, _b, _c;

        float Cycle => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            Object.FindAnyObjectByType<PlayerInputDriver>().InputSource = () => null;
            // The dummies stand where the scene has them, off the runs along Z = 0 and off the shots behind.
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest]
        public IEnumerator AShotBehindHer_TurnsTheBodyOverTime_AndStillHitsTheTargetAtTheCursor()
        {
            Assume.That(_feel.ShotTurnTime, Is.GreaterThan(0.1f), "a turn that takes time");
            yield return RunUntilX(-1f);
            Place(_a, _player.Position.x - 5f, 0f); // straight behind her on her path
            yield return null;
            float yawBefore = Yaw();
            float firstFrameTurn = -1f, hitAt = -1f, turnedAtHit = 0f;
            float maxStep = 0f;
            float last = yawBefore;
            _player.Shoot(_a.transform.position);
            Assert.That(_player.ShotTurnsBody, Is.True, "a target straight behind is outside the front angle");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (Time.realtimeSinceStartup < deadline && (hitAt < 0f || Time.time < hitAt + 0.3f))
            {
                _player.AimAt(_a.transform.position);
                int hits = _a.HitsTaken;
                yield return null;
                float yaw = Yaw();
                float step = Mathf.Abs(Mathf.DeltaAngle(last, yaw));
                if (Time.deltaTime > 0f) maxStep = Mathf.Max(maxStep, step / Time.deltaTime);
                last = yaw;
                if (firstFrameTurn < 0f) firstFrameTurn = Mathf.Abs(Mathf.DeltaAngle(yawBefore, yaw));
                if (hitAt < 0f && _a.HitsTaken > hits)
                {
                    hitAt = Time.time;
                    turnedAtHit = Mathf.Abs(Mathf.DeltaAngle(yawBefore, yaw));
                }
            }
            Assert.That(firstFrameTurn, Is.LessThan(90f), "not turned round in one frame");
            Assert.That(maxStep, Is.LessThan(180f / _feel.ShotTurnTime * 1.15f), "never faster than the shot turn rate");
            Assert.That(hitAt, Is.GreaterThan(0f), "the shot hit the target at the cursor");
            Assert.That(turnedAtHit, Is.LessThan(170f), "before the body had finished turning: the upper body covered the rest");
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.left), Is.GreaterThan(0.98f), "and then she faces the aim");
            Assert.That(_player.FacingMode, Is.EqualTo(ShotFacing.Mode.Held));
        }

        [UnityTest]
        public IEnumerator RhythmClicksBehind_KeepHerFacingTheAim_WithNoSwingBackBetweenShots()
        {
            yield return RunUntilX(-3f);
            int shotsBefore = _player.BasicShotsFired, clicks = 0;
            float start = Time.time, worst = 180f;
            bool settled = false, backGait = false, otherGait = false;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (_player.BasicShotsFired - shotsBefore < 3 && Time.realtimeSinceStartup < deadline)
            {
                Vector3 behind = _player.Position + Vector3.left * 8f;
                _player.AimAt(behind);
                if (clicks < 3 && Time.time >= start + clicks * (Cycle + 0.03f)) { _player.Shoot(behind); clicks++; }
                yield return null;
                float fromRun = Vector3.Angle(Flat(_player.transform.forward), Vector3.right);
                if (!settled && fromRun > 170f) settled = true;
                if (settled)
                {
                    worst = Mathf.Min(worst, fromRun);
                    if (_view != null && Time.time > start + 0.4f)
                    {
                        if (_view.Pose == CharacterPose.ShootBack) backGait = true;
                        else otherGait = true;
                    }
                }
            }
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(3), "three shots");
            Assert.That(settled, Is.True, "she turned to face the aim");
            Assert.That(worst, Is.GreaterThan(165f), "and from then on never swung back toward her run between the shots");
            Assert.That(_player.FacingMode, Is.EqualTo(ShotFacing.Mode.Held));
            if (_view != null)
            {
                Assert.That(backGait, Is.True, "backpedalling in the backward gait");
                Assert.That(otherGait, Is.False, "the whole time");
            }
        }

        [UnityTest]
        public IEnumerator AfterTheHold_SheTurnsBackOverTime_AndEndsFacingHerRun()
        {
            yield return RunUntilX(-3f);
            Vector3 behind = _player.Position + Vector3.left * 8f;
            _player.Shoot(behind);
            yield return Until(() => _player.BasicShotsFired >= 1, 1f);
            float firedAt = Time.time;
            float turnBackAt = -1f, freeAt = -1f, last = Yaw();
            var steps = new List<(float seconds, float degrees)>();
            float deadline = Time.realtimeSinceStartup + 3f;
            // On until she is free, and one rate window past that, so the end of the turn is measured over a whole window.
            while ((freeAt < 0f || Time.time < freeAt + RateWindow) && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                float yaw = Yaw();
                if (turnBackAt >= 0f) steps.Add((Time.deltaTime, Mathf.Abs(Mathf.DeltaAngle(last, yaw))));
                last = yaw;
                if (turnBackAt < 0f && _player.FacingMode == ShotFacing.Mode.TurningBack) turnBackAt = Time.time;
                if (turnBackAt >= 0f && freeAt < 0f && _player.FacingMode == ShotFacing.Mode.Free) freeAt = Time.time;
            }
            Assert.That(turnBackAt, Is.GreaterThan(0f), "the hold ran out");
            Assert.That(turnBackAt - firedAt, Is.GreaterThanOrEqualTo(_feel.ShotTurnHold - 0.1f), "not before its time after the shot");
            Assert.That(freeAt - turnBackAt, Is.GreaterThan(_feel.ShotTurnTime * 0.6f), "turning back took time, not one frame");
            Assert.That(FastestTurn(steps), Is.LessThan(180f / _feel.ShotTurnTime * 1.15f), "at the shot turn rate");
            yield return null;
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.99f), "and she faces her run again");
        }

        [UnityTest]
        public IEnumerator TheCursorBackInsideTheFrontAngle_EndsTheHold()
        {
            yield return RunUntilX(-3f);
            _player.Shoot(_player.Position + Vector3.left * 8f);
            yield return Until(() => _player.FacingMode == ShotFacing.Mode.Held && _player.BasicShotsFired >= 1, 1f);
            _player.AimAt(_player.Position + Vector3.right * 8f); // the cursor back ahead of her run
            yield return null;
            yield return null;
            Assert.That(_player.FacingMode, Is.Not.EqualTo(ShotFacing.Mode.Held), "the hold ends at once");
            yield return Until(() => _player.FacingMode == ShotFacing.Mode.Free, 1f);
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.99f), "turned back to her run");
        }

        [UnityTest]
        public IEnumerator ADash_EndsTheHoldAtOnce() => HoldEndedBy(() =>
            Assert.That(_player.DashToward(_player.Position + Vector3.right * 6f), Is.EqualTo(IntentResult.Started)), ShotFacing.Mode.Free);

        [UnityTest]
        public IEnumerator ASkill_EndsTheHoldAtOnce() => HoldEndedBy(() =>
            Assert.That(_player.PressSkill(0, _player.Position + Vector3.forward * 6f), Is.Not.EqualTo(IntentResult.Rejected)), ShotFacing.Mode.Free);

        IEnumerator HoldEndedBy(Action takeOver, ShotFacing.Mode expected)
        {
            yield return RunUntilX(-3f);
            _player.Shoot(_player.Position + Vector3.left * 8f);
            yield return Until(() => _player.FacingMode == ShotFacing.Mode.Held && _player.BasicShotsFired >= 1, 1f);
            Assume.That(_player.FacingMode, Is.EqualTo(ShotFacing.Mode.Held));
            takeOver();
            Assert.That(_player.FacingMode, Is.EqualTo(expected));
            _player.ReleaseSkill(0);
        }

        [UnityTest]
        public IEnumerator InsideTheFrontAngle_TheBodyNeverTurns_AndTheForwardGaitPlays()
        {
            Assume.That(_feel.ShotFrontAngle, Is.GreaterThan(100f), "a wide front angle");
            float angle = _feel.ShotFrontAngle - 8f; // well past 90, still inside
            yield return RunUntilX(-3f);
            int shotsBefore = _player.BasicShotsFired;
            float worstTurn = 0f;
            bool back = false, forward = false;
            float start = Time.time;
            float deadline = Time.realtimeSinceStartup + 3f;
            int clicks = 0;
            while (Time.time < start + 3f * Cycle + 0.3f && Time.realtimeSinceStartup < deadline)
            {
                Vector3 aim = _player.Position + Quaternion.AngleAxis(-angle, Vector3.up) * Vector3.right * 8f;
                _player.AimAt(aim);
                if (clicks < 3 && Time.time >= start + clicks * (Cycle + 0.03f)) { _player.Shoot(aim); clicks++; Assert.That(_player.ShotTurnsBody, Is.False, "inside the front angle"); }
                yield return null;
                worstTurn = Mathf.Max(worstTurn, Vector3.Angle(Flat(_player.transform.forward), Vector3.right));
                if (_view != null && _player.InShootingGait)
                {
                    if (_view.Pose == CharacterPose.ShootBack) back = true;
                    if (_view.Pose == CharacterPose.ShootMove) forward = true;
                }
            }
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.EqualTo(3));
            Assert.That(worstTurn, Is.LessThan(3f), "the body kept facing her run at " + angle + " degrees");
            Assert.That(_player.FacingMode, Is.EqualTo(ShotFacing.Mode.Free));
            if (_view != null)
            {
                Assert.That(forward, Is.True, "the forward shooting gait");
                Assert.That(back, Is.False, "never the backward one");
            }
        }

        // ---------------- Helpers ----------------

        IEnumerator RunUntilX(float x)
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.Steer(Vector3.right); // held, as a key
            yield return Until(() => _player.Position.x >= x, 4f);
            Assume.That(_player.Position.x, Is.GreaterThanOrEqualTo(x), "the character got there");
        }

        float Yaw() => _player.transform.eulerAngles.y;

        /// <summary>The shortest stretch a turn rate is measured over: one frame at 60 frames a second.</summary>
        const float RateWindow = 1f / 60f;

        /// <summary>
        /// The fastest turn, in degrees a second, over any run of consecutive frames lasting at least
        /// <see cref="RateWindow"/>; a frame longer than that is a run of its own. Measured per frame instead, a step
        /// the game allows at any frame length, such as the last fraction of a degree she snaps to when the turn
        /// back ends, reads as thousands of degrees a second when frames are a fraction of a millisecond long, as
        /// they are without a window. Runs that end before a whole window are covered by the ones that start earlier.
        /// </summary>
        static float FastestTurn(List<(float seconds, float degrees)> steps)
        {
            float fastest = 0f;
            for (int i = 0; i < steps.Count; i++)
            {
                float seconds = 0f, degrees = 0f;
                for (int j = i; j < steps.Count && seconds < RateWindow; j++)
                {
                    seconds += steps[j].seconds;
                    degrees += steps[j].degrees;
                }
                if (seconds < RateWindow) break;
                fastest = Mathf.Max(fastest, degrees / seconds);
            }
            return fastest;
        }

        static void Place(Damageable d, float x, float z)
        {
            Vector3 p = d.transform.position;
            d.transform.position = new Vector3(x, p.y, z);
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
