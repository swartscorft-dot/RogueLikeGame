using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Work order 032 (2026-11-20): an enemy out of the camera's sight is not posed (<see cref="EnemyLook.CullUnseen"/>),
    /// and nothing of the game changes for it. In the ControlRoom scene, on floor 1 of a fixed seed, with the camera
    /// pointed by its rig at a point of the test's choosing: an enemy out of sight keeps its clips' times but its graph
    /// is not evaluated; on the frame it comes into sight it stands in the very pose of a twin (same kind, made the same
    /// frame, doing the same) that was in sight all along; asking for a bone poses it at once; an enemy fighting her out
    /// of sight strikes on the game's time and its clip is where the plan puts it; with the switch off every enemy is
    /// evaluated every frame, as before.
    /// </summary>
    public class EnemyOutOfSightTests
    {
        const ulong Seed = 32032UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        CameraRig _rig;
        Transform _look;
        Transform _targetBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            EnemyLook.CullUnseen = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _rig = Object.FindAnyObjectByType<CameraRig>();
            Assert.That(_rig, Is.Not.Null, "the game's camera rig");
            _targetBefore = _rig.Target;
            _look = new GameObject("WhereTheCameraLooks").transform;
        }

        [TearDown]
        public void PutBack()
        {
            EnemyLook.CullUnseen = true;
            if (_rig != null) _rig.Target = _targetBefore;
            if (_look != null) Object.Destroy(_look.gameObject);
            if (GeneratedLevels.Current != null) Object.Destroy(GeneratedLevels.Current.gameObject);
        }

        /// <summary>Points the game's camera at a place on the ground, and lets it get there.</summary>
        IEnumerator LookAt(Vector3 at)
        {
            _look.position = new Vector3(at.x, 0f, at.z);
            _rig.Target = _look;
            yield return null;
            yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => new Vector2(a.x - b.x, a.z - b.z).magnitude;

        /// <summary>Two asleep enemies of one kind, neither an elite, far from each other and from her.</summary>
        (Enemy, Enemy) FarTwins(GeneratedLevel level)
        {
            List<Enemy> asleep = level.Enemies.Where(e => e != null && e.Look != null && !e.IsElite && !e.IsAwake
                                                          && Flat(e.transform.position, _player.Position) > 30f).ToList();
            foreach (Enemy a in asleep)
                foreach (Enemy b in asleep)
                    if (a != b && a.Kind == b.Kind && Flat(a.transform.position, b.transform.position) > 45f) return (a, b);
            return (null, null);
        }

        [UnityTest]
        public IEnumerator OutOfSight_ItIsNotEvaluated_AndOnTheFrameItComesIntoSightItPosesAsItsTwinInSightAllAlong()
        {
            GeneratedLevel level = _levels.Build(Seed, 1);
            yield return null;
            (Enemy a, Enemy b) = FarTwins(level);
            Assert.That(a, Is.Not.Null, "two enemies of a kind far apart on the floor");

            // The camera on A: A is in sight, B is not.
            yield return LookAt(a.transform.position);
            int bEvaluations = b.Look.Evaluations, bTicks = b.Look.Ticks, aEvaluations = a.Look.Evaluations;
            for (int i = 0; i < 20; i++) yield return null;
            Assert.That(a.Look.InSight, Is.True);
            Assert.That(b.Look.InSight, Is.False);
            Assert.That(a.Look.Evaluations, Is.GreaterThanOrEqualTo(aEvaluations + 20), "in sight: evaluated every frame");
            Assert.That(b.Look.Evaluations, Is.EqualTo(bEvaluations), "out of sight: not evaluated");
            Assert.That(b.Look.Ticks, Is.GreaterThanOrEqualTo(bTicks + 20), "but shown every frame: its clips' times run");
            Assert.That(b.Look.Posed, Is.False);
            Assert.That(b.Look.Playing, Is.EqualTo(a.Look.Playing));
            Assert.That(b.Look.PlayingTime, Is.EqualTo(a.Look.PlayingTime).Within(1e-6), "its clip's time is its twin's");

            // The camera on B: on its first frame in sight it is evaluated, and it stands as A does at the same moment.
            yield return LookAt(b.transform.position);
            Assert.That(b.Look.InSight, Is.True);
            Assert.That(b.Look.Posed, Is.True);
            Assert.That(b.Look.Evaluations, Is.GreaterThan(bEvaluations));
            Assert.That(a.Look.InSight, Is.False, "now A is out of sight");
            a.Look.PoseNow(); // A as it stands at this moment, for the comparison
            Transform[] ta = a.Look.Model.GetComponentsInChildren<Transform>(true), tb = b.Look.Model.GetComponentsInChildren<Transform>(true);
            Assert.That(tb.Length, Is.EqualTo(ta.Length));
            for (int i = 0; i < ta.Length; i++)
            {
                Assert.That(Quaternion.Angle(ta[i].localRotation, tb[i].localRotation), Is.LessThan(0.01f), ta[i].name);
                Assert.That((ta[i].localPosition - tb[i].localPosition).magnitude, Is.LessThan(1e-4f), ta[i].name);
            }
        }

        [UnityTest]
        public IEnumerator AskingForABone_PosesItAtOnce()
        {
            GeneratedLevel level = _levels.Build(Seed, 1);
            yield return null;
            (Enemy a, Enemy b) = FarTwins(level);
            yield return LookAt(a.transform.position);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(b.Look.Posed, Is.False);
            int before = b.Look.Evaluations;
            Assert.That(b.Look.Bone("Hips"), Is.Not.Null);
            Assert.That(b.Look.Posed, Is.True, "a bone is asked for: it is posed for this moment");
            Assert.That(b.Look.Evaluations, Is.EqualTo(before + 1));
        }

        [UnityTest]
        public IEnumerator OutOfSight_AnEnemyFightingHerStrikesOnTheGamesTime_ItsClipWhereThePlanPutsIt()
        {
            GeneratedLevel level = _levels.Build(Seed, 1);
            yield return null;
            Enemy brute = level.Enemies.FirstOrDefault(e => e != null && e.Kind == EnemyKind.Brute && !e.IsElite && e.Look != null);
            Assert.That(brute, Is.Not.Null, "a brute on the floor");
            _player.Teleport(brute.transform.position + Vector3.back * 2f);
            brute.Body.SetMaxHealth(1000000);
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            // The camera far away, at the floor's start: the fight is out of sight.
            yield return LookAt(level.PlayerStart + Vector3.back * 200f);
            brute.Provoke();
            float began = -1f, struck = -1f;
            EnemyAttackDefinition attack = null;
            int evaluations = -1;
            int strikes = brute.Strikes;
            float until = Time.time + 15f;
            while (Time.time < until && struck < 0f)
            {
                if (began < 0f && brute.State == EnemyState.Windup)
                {
                    began = Time.time;
                    attack = brute.Attack;
                    evaluations = brute.Look.Evaluations;
                }
                if (began >= 0f && brute.State == EnemyState.Windup)
                {
                    Assert.That(brute.Look.InSight, Is.False, "out of sight");
                    EnemyClipSpec clip = brute.Look.Spec.ClipForAttack(attack.Name);
                    if (clip != null && brute.Look.Has(clip.Name))
                    {
                        Assert.That(brute.Look.Playing, Is.EqualTo(clip.Name), "its attack's clip is the one it plays");
                        Assert.That(brute.Look.PlayingTime, Is.EqualTo(brute.Look.Plan.InWindup(Time.time - began)).Within(1e-3),
                                    "the clip's time is where the plan puts it");
                    }
                }
                if (began >= 0f && brute.Strikes > strikes) struck = Time.time;
                yield return null;
            }
            Assert.That(began, Is.GreaterThan(0f), "it wound up");
            Assert.That(struck, Is.GreaterThan(0f), "it struck");
            Assert.That(struck - began, Is.EqualTo(attack.Windup).Within(0.05f), "on the game's time: its windup");
            Assert.That(brute.Look.Evaluations, Is.EqualTo(evaluations), "not evaluated while out of sight");
        }

        [UnityTest]
        public IEnumerator SwitchOff_EveryEnemyIsEvaluatedEveryFrame()
        {
            EnemyLook.CullUnseen = false;
            GeneratedLevel level = _levels.Build(Seed, 1);
            yield return null;
            (Enemy a, Enemy b) = FarTwins(level);
            yield return LookAt(a.transform.position);
            int before = b.Look.Evaluations;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(b.Look.InSight, Is.True, "with the switch off all count as in sight");
            Assert.That(b.Look.Evaluations, Is.GreaterThanOrEqualTo(before + 10));
        }
    }
}
