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
    /// G4: the rest of the kit (skills six to eight and the focus gauge) and the enemy that fights back,
    /// in the built ControlRoom scene. Values come from the settings assets in the scene.
    /// Layout: player at (0, 0), Dummy_A at (4, 3), Dummy_B at (-4, 3), the enemy at home at (15, -10),
    /// which is beyond its aggro range of the start.
    /// </summary>
    public class KitTests
    {
        const int Quick = 0, Scatter = 4, Backstep = 6, Triple = 7;

        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // A is 2.3 m away here, B 5.8 m

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        FeelValues _feel;
        Damageable _a, _b;
        EnemyBrute _enemy;
        Damageable _enemyBody;

        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _enemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            Assert.That(_enemy, Is.Not.Null, "EnemyBrute in scene");
            _health = _player.GetComponent<PlayerHealth>();
            Assert.That(_health, Is.Not.Null, "the player has health");
            Assert.That(_enemy.Settings, Is.Not.Null, "the enemy uses its settings asset");
            _enemyBody = _enemy.GetComponent<Damageable>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();

            // The real mouse and keyboard must not steer a test.
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null;
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Skills six to eight ----------------

        [UnityTest]
        public IEnumerator ScatterBlast_HitsUpClose_AndNotBeyondItsShortRange()
        {
            SkillDefinition skill = Skill(Scatter);
            _player.Teleport(NearA);
            yield return null;
            Assume.That(Targeting.InRange(_player.GroundPosition, _a.GroundPosition, _a.Radius, skill.Range), Is.True, "A is close enough");
            Assume.That(Targeting.InRange(_player.GroundPosition, _b.GroundPosition, _b.Radius, skill.Range), Is.False, "B is not");

            Assert.That(_player.PressSkill(Scatter, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(Lost(_a), Is.EqualTo(skill.Damage));
            Assert.That(Lost(_b), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Backstep_MovesTheCharacterBack_AndShootsWhereItWasAimed()
        {
            SkillDefinition skill = Skill(Backstep);
            Assume.That(skill.MoveDistance, Is.LessThan(-1f), "it moves backward");
            _player.Teleport(NearA);
            yield return null;
            Vector3 from = _player.Position;
            Vector3 toA = Flat(_a.transform.position - from);
            Assume.That(toA.magnitude - skill.MoveDistance, Is.LessThan(skill.Range), "A is still in range after the step back");

            Assert.That(_player.PressSkill(Backstep, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Vector3 moved = Flat(_player.Position - from);
            Assert.That(moved.magnitude, Is.EqualTo(-skill.MoveDistance).Within(0.3f), "it moved its move distance");
            Assert.That(Vector3.Dot(moved.normalized, toA.normalized), Is.LessThan(-0.98f), "straight away from the aim");
            Assert.That(Lost(_a), Is.EqualTo(skill.Damage), "and the shot landed where it was aimed");
        }

        [UnityTest]
        public IEnumerator TripleTap_HitsThreeTimesInABurst()
        {
            SkillDefinition skill = Skill(Triple);
            Assume.That(skill.HitCount, Is.EqualTo(3));

            Assert.That(_player.PressSkill(Triple, _a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => _a.HitsTaken >= 1, 3f);
            float firstHit = Time.time;
            yield return Until(() => _a.HitsTaken >= 3, 3f);
            float lastHit = Time.time;
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(_a.HitsTaken, Is.EqualTo(3));
            Assert.That(Lost(_a), Is.EqualTo(3 * skill.Damage));
            Assert.That(lastHit - firstHit, Is.EqualTo(2f * skill.HitInterval).Within(0.06f), "the hits are a hit interval apart");
        }

        // ---------------- The focus gauge ----------------

        [UnityTest]
        public IEnumerator Focus_FillsWithTheDamageDealt_AndCannotBeSpentUntilFull()
        {
            SkillDefinition skill = Skill(Quick);
            Assert.That(_player.Focus, Is.EqualTo(0f));

            _player.PressSkill(Quick, _a.transform.position);
            yield return Until(() => _player.Action == ActionKind.None, 3f);

            Assert.That(_player.Focus, Is.EqualTo(skill.Damage * _player.Identity.FocusPerDamage).Within(1e-3f));
            Assert.That(_player.IsFocusFull, Is.False);
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Rejected));
            Assert.That(_player.IsEmpowered, Is.False);
        }

        [UnityTest]
        public IEnumerator Empowered_SkillsAndShotsDealMore_AndNoFocusIsGained()
        {
            SkillDefinition skill = Skill(Quick);
            IdentityValues identity = _player.Identity;
            _player.Teleport(NearA);
            yield return null;
            _player.FillFocus();
            Assert.That(_player.IsFocusFull, Is.True);

            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsEmpowered, Is.True);
            Assert.That(_player.Focus, Is.EqualTo(0f), "the gauge is spent");
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Rejected), "it cannot be started twice");

            _player.PressSkill(Quick, _a.transform.position);
            yield return Until(() => _player.Action == ActionKind.None, 3f);
            int skillDamage = Mathf.RoundToInt(skill.Damage * identity.EmpoweredDamage);
            Assert.That(Lost(_a), Is.EqualTo(skillDamage), "the skill deals more");

            _player.AttackTarget(_a);
            yield return Until(() => _a.HitsTaken >= 2, 3f);
            _player.Stop();
            Assert.That(Lost(_a) - skillDamage, Is.EqualTo(Mathf.RoundToInt(Placeholders.BasicShotDamage * identity.EmpoweredDamage)),
                        "and so does the basic shot");
            Assert.That(_player.Focus, Is.EqualTo(0f), "no focus while empowered");
            Assert.That(_player.EmpoweredRemaining, Is.GreaterThan(0f).And.LessThan(identity.EmpoweredDuration));
        }

        [UnityTest]
        public IEnumerator Empowered_EndsAfterItsDuration()
        {
            SkillDefinition skill = Skill(Scatter);
            IdentityValues identity = _player.Identity;
            _player.Teleport(NearA);
            yield return null;
            _player.FillFocus();
            float startedAt = Time.time;
            Assert.That(_player.ActivateIdentity(), Is.EqualTo(IntentResult.Started));

            yield return Until(() => !_player.IsEmpowered, identity.EmpoweredDuration + 3f);
            Assert.That(_player.IsEmpowered, Is.False);
            Assert.That(Time.time - startedAt, Is.EqualTo(identity.EmpoweredDuration).Within(0.1f));

            _player.PressSkill(Scatter, _a.transform.position);
            yield return Until(() => _player.Action == ActionKind.None, 3f);
            Assert.That(Lost(_a), Is.EqualTo(skill.Damage), "damage is back to normal");
            Assert.That(_player.Focus, Is.GreaterThan(0f), "and focus is gained again");
        }

        [UnityTest]
        public IEnumerator Input_Z_SpendsAFullGauge_AndDoesNothingOnAnEmptyOne()
        {
            UseFakeInput();
            _idle = new InputFrame { CursorScreen = ScreenOf(_a.transform.position) };

            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, IdentityPressed = true });
            Assert.That(_player.IsEmpowered, Is.False);

            _player.FillFocus();
            yield return Feed(new InputFrame { CursorScreen = _idle.CursorScreen, IdentityPressed = true });
            Assert.That(_player.IsEmpowered, Is.True);
        }

        // ---------------- The enemy ----------------

        [UnityTest]
        public IEnumerator Enemy_StandsAtHome_WhileThePlayerIsFarAway()
        {
            Assume.That(FlatDistance(_player.Position, _enemy.Home), Is.GreaterThan(_enemy.Values.AggroRange + 2f));
            yield return new WaitForSeconds(1f);

            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(FlatDistance(_enemy.transform.position, _enemy.Home), Is.LessThan(0.3f));
            Assert.That(_enemy.IsTelegraphing, Is.False);
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
        }

        [UnityTest]
        public IEnumerator Enemy_Hurt_WalksToThePlayer()
        {
            float before = FlatDistance(_enemy.transform.position, _player.Position);
            Assume.That(before, Is.GreaterThan(_enemy.Values.Attacks[1].MaxDistance + 3f), "too far to attack from where it stands");

            _enemyBody.TakeHit(10);
            yield return new WaitForSeconds(1f);

            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(FlatDistance(_enemy.transform.position, _player.Position),
                        Is.LessThan(before - _enemy.Values.MoveSpeed * 0.6f), "it came nearer at about its walking speed");
        }

        [UnityTest]
        public IEnumerator Enemy_GoesHome_WhenThePlayerIsFarAway()
        {
            _enemyBody.TakeHit(10);
            yield return new WaitForSeconds(1f);
            Assume.That(_enemy.State, Is.EqualTo(EnemyState.Chase));

            _player.Teleport(new Vector3(-19f, 0f, 19f));
            Assume.That(FlatDistance(_enemy.transform.position, _player.Position), Is.GreaterThan(_enemy.Values.LeashRange));
            yield return Until(() => _enemy.State == EnemyState.Idle, 1f);
            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Idle), "it gives up");
            yield return Until(() => FlatDistance(_enemy.transform.position, _enemy.Home) < 0.5f, 8f);
            Assert.That(FlatDistance(_enemy.transform.position, _enemy.Home), Is.LessThan(0.5f), "and walks home");
        }

        [UnityTest]
        public IEnumerator Enemy_Slam_HitsAPlayerWhoStaysInTheCircle()
        {
            yield return StandNextToTheEnemy();
            EnemyAttackDefinition slam = _enemy.Attack;
            Assert.That(_enemy.IsTelegraphing, Is.True, "the telegraph shows during the windup");
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth), "nothing lands before the strike");

            yield return Until(() => _enemy.Strikes >= 1, slam.Windup + 2f);
            Assert.That(_enemy.Strikes, Is.EqualTo(1));
            Assert.That(_enemy.StrikesLanded, Is.EqualTo(1));
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth - slam.Damage));
            Assert.That(_enemy.IsTelegraphing, Is.False, "the telegraph is gone after the strike");
        }

        [UnityTest]
        public IEnumerator Enemy_Slam_MissesAPlayerWhoLeavesTheCircle()
        {
            yield return StandNextToTheEnemy();
            EnemyAttackDefinition slam = _enemy.Attack;

            _player.Teleport(_enemy.Home + Vector3.left * (slam.Radius + 2.5f));
            yield return Until(() => _enemy.Strikes >= 1, slam.Windup + 2f);

            Assert.That(_enemy.Strikes, Is.EqualTo(1), "it strikes all the same");
            Assert.That(_enemy.StrikesLanded, Is.EqualTo(0));
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
        }

        [UnityTest]
        public IEnumerator Enemy_Slam_IsAvoidedByTheDashsIFrame()
        {
            Assume.That(_feel.DashInvulnerability, Is.GreaterThanOrEqualTo(0.08f));
            yield return StandNextToTheEnemy();

            // Dash at the last moment, along the circle rather than out of it: only the i-frame can save the player.
            yield return Until(() => _enemy.State != EnemyState.Windup || _enemy.WindupRemaining <= 0.05f, 3f);
            Assume.That(_enemy.State, Is.EqualTo(EnemyState.Windup), "the dash comes before the strike");
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 10f), Is.EqualTo(IntentResult.Started));

            yield return Until(() => _enemy.Strikes >= 1, 1f);
            Assert.That(_enemy.Strikes, Is.EqualTo(1));
            Assert.That(_health.HitsAvoided, Is.EqualTo(1), "the strike reached the player during the i-frame");
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
        }

        [UnityTest]
        public IEnumerator Enemy_Shockwave_HitsAPlayerWhoStaysInTheLine()
        {
            yield return StandInFrontOfTheEnemy();
            EnemyAttackDefinition wave = _enemy.Attack;

            yield return Until(() => _enemy.Strikes >= 1, wave.Windup + 2f);
            Assert.That(_enemy.StrikesLanded, Is.EqualTo(1));
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth - wave.Damage));
        }

        [UnityTest]
        public IEnumerator Enemy_Shockwave_IsAimedOnce_SoAStepToTheSideAvoidsIt()
        {
            yield return StandInFrontOfTheEnemy();
            EnemyAttackDefinition wave = _enemy.Attack;

            _player.Teleport(_player.Position + Vector3.forward * (wave.HalfWidth + 2f));
            yield return Until(() => _enemy.Strikes >= 1, wave.Windup + 2f);

            Assert.That(_enemy.Strikes, Is.EqualTo(1));
            Assert.That(_enemy.StrikesLanded, Is.EqualTo(0), "the line stayed where it was aimed");
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
        }

        [UnityTest]
        public IEnumerator Enemy_CanBeShotLikeADummy()
        {
            _player.Teleport(_enemy.Home + Vector3.left * 5f);
            yield return null;
            Assume.That(Targeting.InRange(_player.GroundPosition, _enemyBody.GroundPosition, _enemyBody.Radius, _feel.BasicAttackRange), Is.True);
            Assert.That(_enemyBody.MaxHealth, Is.EqualTo(_enemy.Values.Health), "its health comes from the settings");

            _player.AttackTarget(_enemyBody);
            yield return Until(() => _enemyBody.HitsTaken >= 1, 3f);
            Assert.That(Lost(_enemyBody), Is.EqualTo(Placeholders.BasicShotDamage));
        }

        [UnityTest]
        public IEnumerator Enemy_Dies_AndStandsAtHomeAgain()
        {
            _enemyBody.TakeHit(10);
            yield return new WaitForSeconds(0.5f);
            Assume.That(FlatDistance(_enemy.transform.position, _enemy.Home), Is.GreaterThan(0.5f), "it has left home");

            _enemyBody.TakeHit(_enemyBody.MaxHealth);
            yield return null;
            yield return null;
            Assert.That(_enemy.IsDead, Is.True);
            Assert.That(_enemy.GetComponentInChildren<MeshRenderer>().enabled, Is.False, "a dead enemy is not shown");

            yield return new WaitForSeconds(_enemy.Values.RespawnDelay + 0.5f);
            Assert.That(_enemy.IsDead, Is.False);
            Assert.That(_enemyBody.CurrentHealth, Is.EqualTo(_enemyBody.MaxHealth));
            Assert.That(FlatDistance(_enemy.transform.position, _enemy.Home), Is.LessThan(0.3f), "back at home");
            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Idle), "and calm");
        }

        // ---------------- The player's health ----------------

        [UnityTest]
        public IEnumerator Player_DiesAtZeroHealth_AndStandsAtTheStartAgain()
        {
            Vector3 start = _player.Position;
            _player.Teleport(new Vector3(5f, 0f, 5f));
            yield return null;
            _player.MoveTo(new Vector3(8f, 0f, 8f));

            Assert.That(_health.TakeHit(_health.MaxHealth - 1), Is.EqualTo(_health.MaxHealth - 1));
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.TakeHit(50), Is.EqualTo(1), "only what was left can be taken");
            Assert.That(_health.IsAlive, Is.False);
            Assert.That(_health.Deaths, Is.EqualTo(1));
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None), "a dead character drops its order");
            Assert.That(_health.TakeHit(10), Is.EqualTo(0), "and cannot be hit again");

            yield return new WaitForSeconds(Placeholders.PlayerRespawnDelay + 0.3f);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_health.Current, Is.EqualTo(_health.MaxHealth));
            Assert.That(FlatDistance(_player.Position, start), Is.LessThan(0.1f), "back at the start");
        }

        [UnityTest]
        public IEnumerator Input_ADeadCharacter_TakesNoInput()
        {
            UseFakeInput();
            _idle = new InputFrame { CursorScreen = ScreenOf(_player.Position + new Vector3(-5f, 0f, -4f)), MoveHeld = true };
            _health.TakeHit(_health.MaxHealth);
            Assume.That(_health.IsAlive, Is.False);
            Vector3 diedAt = _player.Position;

            yield return new WaitForSeconds(0.5f);
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None));
            Assert.That(FlatDistance(_player.Position, diedAt), Is.LessThan(0.05f));
        }

        // ---------------- Helpers ----------------

        /// <summary>Puts the player inside the Slam's distance band and waits for its windup.</summary>
        IEnumerator StandNextToTheEnemy()
        {
            _player.Teleport(_enemy.Home + Vector3.left * 2f);
            yield return Until(() => _enemy.State == EnemyState.Windup, 2f);
            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Windup), "the enemy winds up an attack");
            Assert.That(_enemy.Attack.Shape, Is.EqualTo(EnemyAttackShape.Circle), "up close it is the circle");
        }

        /// <summary>Puts the player inside the Shockwave's distance band and waits for its windup.</summary>
        IEnumerator StandInFrontOfTheEnemy()
        {
            _player.Teleport(_enemy.Home + Vector3.left * 7f);
            yield return Until(() => _enemy.State == EnemyState.Windup, 2f);
            Assert.That(_enemy.State, Is.EqualTo(EnemyState.Windup), "the enemy winds up an attack");
            Assert.That(_enemy.Attack.Shape, Is.EqualTo(EnemyAttackShape.Line), "at a distance it is the line");
        }

        SkillDefinition Skill(int slot) => _player.Skills[slot];

        static int Lost(Damageable d) => d.MaxHealth - d.CurrentHealth;

        void UseFakeInput() => _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;

        /// <summary>Gives the driver one frame, and waits until it has been read and a second, idle frame has passed.</summary>
        IEnumerator Feed(InputFrame frame)
        {
            _frames.Enqueue(frame);
            yield return null;
            yield return null;
            Assert.That(_frames.Count, Is.EqualTo(0), "the driver read the frame");
        }

        static Vector2 ScreenOf(Vector3 world) =>
            Camera.main.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
