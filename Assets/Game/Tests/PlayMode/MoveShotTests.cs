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

            _player.AttackTarget(_a);
            Assert.That(_player.IsShootingOnTheMove, Is.True, "it shoots on the move");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move), "the move order stays");
            yield return null;
            yield return null;
            Assert.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(1e-3f), "slowed while the shot winds up");
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed * _feel.MovingShotSpeedShare).Within(0.4f), "and walking at that speed");
            Assert.That(_player.ShotTurnsBody, Is.False, "the target is within the front angle");
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.98f), "so the body keeps facing the way it runs");

            yield return Until(() => _a.HitsTaken >= 1, 2f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "one shot, and it hit");
            float firedAt = Time.time;
            float due = firedAt + _feel.MovingShotRecovery + 0.03f;
            yield return Until(() => Time.time >= due, 2f);
            Assert.That(_player.SpeedLimit, Is.EqualTo(_feel.RunSpeed).Within(1e-3f), "back to full speed after the recovery time");
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.4f), "and running at it");

            yield return Until(() => _player.Order == OrderKind.None, 5f);
            Assert.That(FlatDistance(_player.Position, RunTo), Is.LessThan(0.2f), "it arrives where it was going");
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "no second shot without a second command");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1));
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator OnTheMove_ATargetOutOfRange_IsAttackedAsBefore()
        {
            yield return RunUntilX(-3f);
            Assume.That(InRange(_c), Is.False, "C is out of range");

            _player.AttackTarget(_c);
            Assert.That(_player.IsShootingOnTheMove, Is.False);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackTarget), "the attack order takes over");
            yield return Until(() => _c.HitsTaken >= 1, 6f);
            Assert.That(_c.HitsTaken, Is.GreaterThanOrEqualTo(1), "it walks into range and shoots");
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Standing_AnAttack_IsAsBefore_TheOrderTakesOverAndRepeats()
        {
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            _player.AttackTarget(_a);
            Assert.That(_player.IsShootingOnTheMove, Is.False);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.AttackTarget));
            yield return Until(() => _a.HitsTaken >= 2, 3f);
            Assert.That(_a.HitsTaken, Is.GreaterThanOrEqualTo(2), "standing, the attack order keeps shooting");
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator OnTheMove_ATargetOutsideTheFrontAngle_TurnsTheBodyForTheShot_WhileItKeepsGoing_ThenFacesTheMoveAgain()
        {
            yield return RunUntilX(-3.4f);
            Assume.That(InRange(_b), Is.True, "B is in range here");
            Assume.That(TurnsBodyFor(_b), Is.True, "and behind the front angle");
            float xAtCommand = _player.Position.x;

            _player.AttackTarget(_b);
            Assert.That(_player.IsShootingOnTheMove, Is.True);
            Assert.That(_player.ShotTurnsBody, Is.True);
            yield return null;
            yield return null;
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Flat(_b.transform.position - _player.Position).normalized),
                        Is.GreaterThan(0.98f), "the body faces the target");
            yield return Until(() => _b.HitsTaken >= 1, 2f);
            Assert.That(_b.HitsTaken, Is.EqualTo(1));
            Assert.That(_player.Position.x, Is.GreaterThan(xAtCommand + 0.3f), "it kept going along its path meanwhile");
            Assert.That(Mathf.Abs(_player.Position.z), Is.LessThan(0.3f), "on its path");

            yield return Until(() => !_player.IsShootingOnTheMove, 2f);
            yield return null;
            yield return null;
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Vector3.right), Is.GreaterThan(0.98f), "then faces the way it goes again");
        }

        [UnityTest]
        public IEnumerator OnTheMove_ANewMoveOrderDuringTheWindUp_ChangesTheWay_ButDoesNotCancelTheShot()
        {
            yield return RunUntilX(-1f);
            _player.AttackTarget(_a);
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
            _player.AttackTarget(_a);
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

            _player.AttackTarget(_a);
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
        public IEnumerator Standing_ATargetOutsideTheFrontAngle_TurnsTheWholeBody()
        {
            _player.Teleport(new Vector3(0f, 0f, 3.5f));
            yield return null;
            Assume.That(InRange(_a), Is.True);
            Assume.That(TurnsBodyFor(_a), Is.True, "A is outside the front angle");

            _player.AttackTarget(_a);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 1f);
            Assert.That(_player.ShotTurnsBody, Is.True);
            Assert.That(Vector3.Dot(Flat(_player.transform.forward), Flat(_a.transform.position - _player.Position).normalized),
                        Is.GreaterThan(0.99f), "the body faces the target");
        }

        // ---------------- Helpers ----------------

        /// <summary>Starts the run from <see cref="RunFrom"/> to <see cref="RunTo"/> and waits until the character has passed x.</summary>
        IEnumerator RunUntilX(float x)
        {
            _player.Teleport(RunFrom);
            yield return null;
            _player.MoveTo(RunTo);
            yield return Until(() => _player.Position.x >= x, 4f);
            Assume.That(_player.Position.x, Is.GreaterThanOrEqualTo(x), "the character got there");
            Assume.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.4f), "at full speed");
        }

        bool InRange(Damageable d) => Targeting.InRange(_player.GroundPosition, d.GroundPosition, d.Radius, _feel.BasicAttackRange);

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
