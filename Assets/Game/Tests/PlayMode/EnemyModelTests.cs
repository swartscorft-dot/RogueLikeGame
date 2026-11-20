using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The enemies in their models (work order 011), in the ControlRoom scene, spawned through the practice room's
    /// intents and fought by standing where their attacks reach: each kind wears its model at its height with the
    /// capsule switched off and its bar and pips over its head; standing it plays Idle and moving its walk or run; each
    /// attack's clip reaches its strike on the frame the game's strike lands; the archer's arm points down its line as
    /// it lets go; a daze leaves the attack clip at once and ends in Idle; dead it plays Death, lies a moment and is
    /// gone, and the practice brute stands up as itself. Writes a line per attack with the clip's timing ("ANIMSYNC").
    ///
    /// The practice brute is switched off but for its own tests, so it does not join in.
    /// </summary>
    public class EnemyModelTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        const double ClipFrame = 1.0 / EnemyModelSpec.FramesPerSecond;

        PlayerCharacter _player;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            ThingsThatFly.Ensure().Values.On = false; // work order 036: these are the tests of the attacks as they were
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator For(float seconds)
        {
            float end = Time.time + seconds;
            while (Time.time < end) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        Enemy Spawn(EnemyKind kind)
        {
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.GreaterThanOrEqualTo(1), kind + " spawned");
            return made[0];
        }

        /// <summary>She stands this far from the enemy, on the side she came from.</summary>
        void StandAt(Enemy e, float distance)
        {
            Vector3 dir = Flat(Stand - e.transform.position);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.back;
            _player.Teleport(Flat(e.transform.position) + dir * distance);
        }

        // ---------------- The models ----------------

        [UnityTest]
        public IEnumerator EveryKind_WearsItsModel_AtItsHeight_WithTheCapsuleOff_AndItsBarOverItsHead()
        {
            var all = new List<Enemy> { _practiceEnemy, Spawn(EnemyKind.Swarmer), Spawn(EnemyKind.Shooter), Spawn(EnemyKind.Guard), Spawn(EnemyKind.Brute) };
            yield return null;
            // The pips of marks, made as a mark makes them; they take their place over it in their next LateUpdate.
            foreach (Enemy e in all) MarkPips.For(e.Body, null);
            yield return null;
            yield return null;
            foreach (Enemy e in all)
            {
                EnemyModelSpec spec = EnemyModelSpec.Of(e.Kind);
                string what = e.name + " (" + e.Kind + ")";
                Assert.That(e.Look, Is.Not.Null, what + " wears a model");
                Assert.That(e.Look.Spec, Is.SameAs(spec));
                Transform body = e.transform.Find("Body");
                Assert.That(body == null || !body.gameObject.activeInHierarchy, what + ": the capsule is off");
                Transform facing = e.transform.Find("Facing");
                Assert.That(facing == null || !facing.gameObject.activeInHierarchy, what + ": and its nose");
                Assert.That(e.transform.Cast<Transform>().Count(t => t.name == EnemyLook.PivotName), Is.EqualTo(1), what + ": one model");
                Assert.That(e.Look.Skin, Is.Not.Null);
                Assert.That(e.Look.Skin.enabled && e.Look.Skin.gameObject.activeInHierarchy, Is.True, what + ": shown");
                Assert.That(e.BodyRenderer, Is.SameAs(e.Look.Skin), what + ": the model's body is the one tinted");

                // As high as the table says in the pose it was made in, and standing on the floor in Idle.
                float made = EnemyLook.BindHeight(e.Look.Skin, e.transform);
                Assert.That(made, Is.EqualTo((float)spec.Height).Within((float)spec.Height * 0.05f), what + " height as made");
                Assert.That(EnemyLook.SkinBounds(e.Look.Skin, e.transform, out Vector3 lo, out Vector3 hi), Is.True);
                Assert.That(lo.y, Is.EqualTo(0f).Within(0.1f), what + " on the floor");
                Assert.That(hi.y - lo.y, Is.InRange((float)spec.Height * 0.75f, (float)spec.Height * 1.05f), what + " standing in Idle");
                // Facing the way the enemy faces: it looks ahead, not to a side or back (its stance may stand it a little bladed).
                Assert.That(Mathf.Abs(EnemyLook.FacingOf(e.Look.Model, e.transform)), Is.LessThan(45f), what + " faces forward");
                // (The shooter sees her from 12 m and is already at it.)
                if (!e.IsAwake) Assert.That(e.Look.Playing, Is.EqualTo("Idle"), what + " stands in Idle");

                foreach (Renderer r in e.Look.Renderers)
                    Assert.That(EnemyLook.TextureOf(r), Is.Not.Null, what + ": " + r.name + " has its texture");
                Debug.Log(string.Format(s_Inv, "ENEMYMODEL {0}: {5:0.###} m high as made (scale {6:0.###}), {1:0.###} m standing in Idle, bottom {2:0.###}, facing {3:0.#} deg; textures {4}",
                    what, hi.y - lo.y, lo.y, EnemyLook.FacingOf(e.Look.Model, e.transform),
                    string.Join(", ", e.Look.Renderers.Select(r => r.name + "=" + (EnemyLook.TextureOf(r) != null ? EnemyLook.TextureOf(r).name : "none"))),
                    made, e.Look.Measured.Scale));

                // The pips and, on the guard, the stagger bar sit above its head.
                Transform head = e.Look.Bone("head_end") ?? e.Look.Bone("Head");
                Assert.That(head, Is.Not.Null, what + " has a head");
                MarkPips pips = e.GetComponentInChildren<MarkPips>(true);
                Assert.That(pips.transform.position.y, Is.GreaterThan(head.position.y), what + ": the pips over the head");
                Assert.That(pips.transform.position.y - head.position.y, Is.LessThan(1.5f), what + ": not far over it");
                if (e is EnemyGuard)
                {
                    StaggerBarView bar = e.GetComponentInChildren<StaggerBarView>();
                    Assert.That(bar, Is.Not.Null);
                    Assert.That(bar.transform.position.y, Is.GreaterThan(pips.transform.position.y), "the bar over the pips");
                    Assert.That(bar.transform.position.y - head.position.y, Is.LessThan(2.5f));
                }
            }
        }

        [UnityTest]
        public IEnumerator Standing_PlaysIdle_Moving_PlaysItsWalkOrRun_AtTheSpeedOfItsFeet()
        {
            var guard = Spawn(EnemyKind.Guard);
            var swarmer = Spawn(EnemyKind.Swarmer);
            yield return For(0.3f);
            Assert.That(guard.Look.Playing, Is.EqualTo("Idle"));
            Assert.That(swarmer.Look.Playing, Is.EqualTo("Idle"));

            // Hurt from 12 m, they come for her: the guard walks, the swarmer runs.
            guard.Body.TakeHit(1);
            swarmer.Body.TakeHit(1);
            yield return For(0.6f);
            Assert.That(guard.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(guard.Look.Playing, Is.EqualTo("Walk"), "the guard walks at " + guard.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude);
            double expected = EnemyGaits.Rate(guard.GetComponent<UnityEngine.AI.NavMeshAgent>().velocity.magnitude,
                                              EnemyModelSpec.Guard.WalkStride * guard.Look.Measured.Scale);
            Assert.That(guard.Look.PlayingRate, Is.EqualTo(expected).Within(0.15), "at the speed of its stride");
            Assert.That(guard.Look.Weight("Walk"), Is.EqualTo(1f).Within(1e-3f), "blended in by now");
            Assert.That(swarmer.Look.Playing, Is.EqualTo("Run"), "the swarmer is faster than its crouched walk suits");
            double before = guard.Look.PlayingTime;
            yield return For(0.1f);
            Assert.That(guard.Look.PlayingTime, Is.Not.EqualTo(before), "the walk plays on with game time");
        }

        // ---------------- The attacks ----------------

        /// <summary>
        /// Waits for the enemy's next strike and reads the clip it plays at that moment: the name, its time and the
        /// clip's strike time, and the gap between the two.
        /// </summary>
        IEnumerator MeasureStrike(Enemy e, string attack, string clip, Action<double> gap)
        {
            int strikes = e.Strikes;
            double windupClip = -1;
            AttackClipPlan plan = default;
            yield return Until(() =>
            {
                if (e.State == EnemyState.Windup && e.Look.Playing == clip)
                {
                    windupClip = e.Look.PlayingTime;
                    plan = e.Look.Plan;
                }
                return e.Strikes > strikes;
            }, 12f);
            Assert.That(e.Strikes, Is.GreaterThan(strikes), e.Kind + " struck");
            Assert.That(e.Attack, Is.Not.Null);
            Assert.That(e.Attack.Name, Is.EqualTo(attack), e.Kind + " struck with its " + attack);
            Assert.That(e.Look.Playing, Is.EqualTo(clip), e.Kind + " " + attack + " plays " + clip);
            EnemyClipSpec spec = e.Look.Spec.Clip(clip);
            double g = e.Look.PlayingTime - spec.Strike;
            Debug.Log(string.Format(s_Inv,
                "ANIMSYNC {0} {1}: windup {2:0.###} s, recovery {3:0.###} s; clip {4} ({5:0.###} s, strike {6:0.###}) starts at {7:0.###}, " +
                "holds {8:0.###} s, windup at x{9:0.###}, after the strike x{10:0.###}; clip at the game's strike {11:0.####} (gap {12:+0.####;-0.####;0} s; " +
                "last windup frame {13:0.####})",
                e.Kind, attack, e.Attack.Windup, e.Attack.Recovery, clip, spec.Length, spec.Strike, plan.Start, plan.Hold,
                plan.WindupSpeed, plan.RecoverySpeed, e.Look.PlayingTime, g, windupClip));
            Assert.That(Math.Abs(g), Is.LessThanOrEqualTo(ClipFrame), e.Kind + " " + attack + ": the clip's strike on the game's");
            Assert.That(windupClip, Is.GreaterThanOrEqualTo(0), "the clip played in the windup");
            Assert.That(windupClip, Is.LessThanOrEqualTo(spec.Strike + 1e-6), "short of its strike before it");
            gap(g);
        }

        IEnumerator BruteStrikes(float distance, string attack, string clip)
        {
            _practiceEnemy.enabled = true;
            StandAt(_practiceEnemy, distance);
            yield return MeasureStrike(_practiceEnemy, attack, clip, _ => { });
        }

        [UnityTest]
        public IEnumerator Brute_Slam_PlaysSlam_StrikingWithTheGame() => BruteStrikes(2f, "Slam", "Slam");

        [UnityTest]
        public IEnumerator Brute_Shockwave_PlaysAttack_StrikingWithTheGame() => BruteStrikes(6f, "Shockwave", "Attack");

        [UnityTest]
        public IEnumerator Swarmer_Lunge_PlaysAttack_StrikingWithTheGame()
        {
            Enemy e = Spawn(EnemyKind.Swarmer);
            StandAt(e, 1f);
            yield return MeasureStrike(e, "Lunge", "Attack", _ => { });
        }

        [UnityTest]
        public IEnumerator Guard_Slam_PlaysSlam_StrikingWithTheGame()
        {
            Enemy e = Spawn(EnemyKind.Guard);
            StandAt(e, 2f);
            yield return MeasureStrike(e, "Slam", "Slam", _ => { });
        }

        [UnityTest]
        public IEnumerator Guard_Sweep_PlaysSweep_StrikingWithTheGame()
        {
            Enemy e = Spawn(EnemyKind.Guard);
            StandAt(e, 4.2f);
            yield return MeasureStrike(e, "Sweep", "Sweep", _ => { });
        }

        [UnityTest]
        public IEnumerator Shooter_Shot_PlaysShoot_ReleasingDownItsLine()
        {
            Enemy e = Spawn(EnemyKind.Shooter);
            yield return null;
            Assert.That(e.Look.HasBow, Is.True, "the archer has its bow");
            float restBefore = e.Look.BowRestAngleBefore, restNow = e.Look.BowUpAngle();
            StandAt(e, 9f);
            yield return MeasureStrike(e, "Shot", "Shoot", _ => { });

            // The pose of the strike's frame: the left arm, which the arrow leaves along, points down the line.
            Vector3 line = Flat(e.AimPoint - e.transform.position).normalized;
            float off = Vector3.SignedAngle(line, e.Look.ShotDirection(), Vector3.up);
            float armOff = Vector3.SignedAngle(line, e.Look.LeftArmDirection(), Vector3.up);
            float shootingBow = e.Look.BowUpAngle();
            Debug.Log(string.Format(s_Inv,
                "ANIMSYNC shooter release: the arrow (right hand through left) {0:0.#} deg off the line, the left arm {5:0.#} (body turned {1:0.#} deg, " +
                "the table said {6}); bow {2:0.#} deg off upright at rest as exported, {3:0.#} at rest as shown, {4:0.#} shooting",
                off, e.Look.Measured.ShotTurn, restBefore, restNow, shootingBow, armOff, EnemyModelSpec.Shooter.ShotLeftOfFacing));
            Assert.That(Mathf.Abs(off), Is.LessThan(10f), "the arrow leaves down the telegraphed line");
            Assert.That(restNow, Is.LessThan(30f), "at rest the bow hangs roughly upright");
        }

        // ---------------- The daze and death ----------------

        [UnityTest]
        public IEnumerator Guard_DazedInItsWindup_LeavesTheAttackAtOnce_StaggersThenIdles()
        {
            Enemy guard = Spawn(EnemyKind.Guard);
            StandAt(guard, 2f);
            yield return Until(() => guard.State == EnemyState.Windup, 5f);
            yield return For(0.3f);
            Assume.That(guard.State, Is.EqualTo(EnemyState.Windup));
            Assume.That(guard.Look.Weight("Slam"), Is.GreaterThan(0.9f));

            guard.Body.TakeHit(1, 1e6f);
            Assert.That(guard.IsDazed, Is.True);
            yield return null;
            Assert.That(guard.Look.Playing, Is.EqualTo("Stagger"));
            Assert.That(guard.Look.Weight("Slam"), Is.EqualTo(0f), "the attack clip is left at once");
            Assert.That(guard.Look.Weight("Stagger"), Is.EqualTo(1f));
            // Tinted while dazed, on the model's own material; the hit flashes over every mesh of it.
            Assert.That(guard.Look.Skin.sharedMaterial.color, Is.Not.EqualTo(Color.white), "the model is tinted while dazed");
            Assert.That(guard.GetComponent<HitFlash>().IsFlashing, Is.True);
            var block = new MaterialPropertyBlock();
            guard.Look.Skin.GetPropertyBlock(block);
            Assert.That(block.GetColor("_BaseColor"), Is.EqualTo(EnemyLook.FlashColor), "the flash brightens the model");

            // She goes far away; when the daze is over it gives up and stands.
            _player.Teleport(Stand + Vector3.back * 30f);
            yield return For((float)EnemyModelSpec.Guard.Clip("Stagger").Length + 0.2f);
            Assert.That(guard.IsDazed, Is.True, "still dazed after its Stagger");
            Assert.That(guard.Look.Playing, Is.EqualTo("Stagger"), "it holds the Stagger's last frame");
            Assert.That(guard.Look.PlayingTime, Is.EqualTo(EnemyModelSpec.Guard.Clip("Stagger").Length).Within(ClipFrame));
            yield return Until(() => !guard.IsDazed, 3f);
            yield return For(0.4f);
            Assert.That(guard.Look.Playing, Is.EqualTo("Idle"), "after the daze, Idle");
            Assert.That(guard.Look.Weight("Idle"), Is.EqualTo(1f).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator AKilledSwarmer_PlaysDeath_LiesThere_ThenIsGone()
        {
            Enemy e = Spawn(EnemyKind.Swarmer);
            yield return null;
            e.Body.TakeHit(e.Body.MaxHealth);
            yield return null;
            yield return null;
            Assert.That(e.IsDead, Is.True);
            Assert.That(e.Look.Playing, Is.EqualTo("Death"));
            Assert.That(e.Look.Skin.enabled, Is.True, "it is seen falling");
            float shown = e.ShownDeadFor(e.Values);
            Assert.That(shown, Is.EqualTo(e.Look.DeathSeconds + EnemyLook.LieSeconds).Within(1e-4f));
            yield return For(e.Look.DeathSeconds - 0.1f);
            Assert.That(e.Look.Skin.enabled, Is.True, "it lies there");
            yield return For(EnemyLook.LieSeconds + 0.2f);
            Assert.That(e.Look.PlayingTime, Is.EqualTo(e.Look.DeathSeconds).Within(ClipFrame), "on the last frame of its Death");
            Assert.That(e.GetComponentsInChildren<Renderer>().Where(r => !(r is LineRenderer)).All(r => !r.enabled), Is.True, "then it is gone");
            yield return For(e.Values.RespawnDelay);
            Assert.That(e.IsDead, Is.True, "and it stays dead");
        }

        [UnityTest]
        public IEnumerator ThePracticeBrute_KilledInItsWindup_LeavesTheAttack_Dies_AndStandsUpAsItself()
        {
            _practiceEnemy.enabled = true;
            StandAt(_practiceEnemy, 2f);
            yield return Until(() => _practiceEnemy.State == EnemyState.Windup, 5f);
            yield return For(0.2f);
            Assume.That(_practiceEnemy.Look.Weight("Slam"), Is.GreaterThan(0.9f));
            _practiceEnemy.Body.TakeHit(_practiceEnemy.Body.MaxHealth);
            yield return null;
            Assert.That(_practiceEnemy.Look.Playing, Is.EqualTo("Death"));
            Assert.That(_practiceEnemy.Look.Weight("Slam"), Is.EqualTo(0f), "the attack clip is left at once");

            _player.Teleport(Stand + Vector3.back * 30f);
            yield return Until(() => !_practiceEnemy.IsDead, _practiceEnemy.Values.RespawnDelay + 2f);
            yield return null;
            Assert.That(_practiceEnemy.IsDead, Is.False, "it comes back");
            Assert.That(_practiceEnemy.Look.Playing, Is.EqualTo("Idle"), "standing");
            Assert.That(_practiceEnemy.Look.Weight("Idle"), Is.EqualTo(1f), "as itself at once");
            Assert.That(_practiceEnemy.Look.Skin.enabled, Is.True, "and seen");
        }
    }
}
