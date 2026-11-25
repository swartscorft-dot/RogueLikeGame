using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The act boss (work order 014, 2026-11-20), in the ControlRoom scene: spawned through the practice room's intent
    /// (F8, <see cref="GeneratedLevels.SpawnPractice"/>) or standing in a generated floor 5, and fought by hurting it
    /// and moving her with <see cref="PlayerCharacter.Teleport"/>. Its model as imported; it wakes as she enters its room
    /// and does not go home; each attack's telegraph shows for its windup, its strike lands on time and only inside its
    /// shape; the charge runs its line, hits her once, stops short of a wall and ends on the NavMesh; the stagger bar
    /// dazes it; the calls and the anger. Every number is read from <see cref="BossValues"/>.
    ///
    /// The practice brute is switched off (it stays as the template the spawns copy). She stands at
    /// <see cref="Stand"/>; the boss comes 12 m north of her.
    /// </summary>
    public class EnemyBossTests
    {
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;
        readonly List<Object> _made = new List<Object>();

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
            _health = _player.GetComponent<PlayerHealth>();
            _practiceEnemy = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(_practiceEnemy, Is.Not.Null, "the practice enemy, to copy");
            _practiceEnemy.enabled = false;
            _levels = GeneratedLevels.Ensure();
            _player.Teleport(Stand);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
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

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        void Record()
        {
            _folder = Path.Combine(Path.GetTempPath(), "EnemyBossTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
        }

        EnemyBoss SpawnBoss()
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Boss);
            Assert.That(made.Count, Is.EqualTo(1), "one boss");
            Assert.That(made[0], Is.InstanceOf<EnemyBoss>());
            return (EnemyBoss)made[0];
        }

        static EnemyAttackDefinition Named(EnemyBoss boss, string name) => boss.Values.Attacks.First(a => a.Name == name);

        // ---------------- The model ----------------

        [Test]
        public void TheModel_ImportsAsTheTableSays_TheCleaverFollowsTheRightHand()
        {
            EnemyModelSpec spec = EnemyModelSpec.Boss;
            GameObject source = EnemyLook.Source(spec);
            Assert.That(source, Is.Not.Null, "loads from Resources/" + spec.ResourcePath);
            var root = new GameObject("Import boss");
            _made.Add(root);
            GameObject model = Object.Instantiate(source, root.transform, false);
            SkinnedMeshRenderer skin = model.GetComponentsInChildren<SkinnedMeshRenderer>(true)
                                            .OrderByDescending(s => s.sharedMesh != null ? s.sharedMesh.vertexCount : 0).First();
            float height = EnemyLook.BindHeight(skin, root.transform);
            Assert.That(height, Is.EqualTo((float)spec.Height).Within((float)spec.Height * 0.05f), "3.3 m as bound, to the horns' tips");
            Assert.That(skin.bones.Length, Is.EqualTo(24), "24 bones");
            var takes = new List<string>();
            foreach (EnemyClipSpec c in spec.Clips)
            {
                AnimationClip clip = EnemyLook.FindClip(spec, c.Name);
                Assert.That(clip, Is.Not.Null, c.Name);
                Assert.That(clip.length, Is.EqualTo((float)c.Length).Within(1f / 30f + 1e-4f), c.Name);
                takes.Add(c.Name + " " + clip.length.ToString("0.###") + " s");
            }
            Assert.That(EnemyLook.FindClip(spec, "Slam"), Is.Not.Null, "the Slam is in the file, not played");

            Renderer cleaver = model.GetComponentsInChildren<Renderer>(true).FirstOrDefault(r => r != skin && r.name.IndexOf("Cleaver", StringComparison.OrdinalIgnoreCase) >= 0);
            Assert.That(cleaver, Is.Not.Null, "the cleaver, a mesh of its own");
            Transform hand = EnemyLook.Find(model.transform, "RightHand");
            Assert.That(hand, Is.Not.Null);
            // In the hand's space the cleaver stays where it is, whatever the clip and the time.
            var seen = new List<Vector3>();
            foreach ((string name, double t) in new[] { ("Idle", 0.0), ("Idle", 2.0), ("Attack", 1.067), ("Spin", 0.967), ("Run", 0.3) })
            {
                Pose(model, EnemyLook.FindClip(spec, name), t, () => seen.Add(hand.InverseTransformPoint(Centre(cleaver)) * hand.lossyScale.x));
            }
            float drift = seen.Max(p => Vector3.Distance(p, seen[0]));
            Debug.Log("BOSSIMPORT height as bound " + height.ToString("0.###") + " m, bones " + skin.bones.Length + ", cleaver '" + cleaver.name + "' "
                      + cleaver.GetType().Name + " under '" + (cleaver.transform.parent != null ? cleaver.transform.parent.name : "-") + "', drift from the hand "
                      + drift.ToString("0.###") + " m; takes " + string.Join(", ", takes));
            Assert.That(drift, Is.LessThan(0.05f), "the cleaver follows the right hand");
        }

        /// <summary>Poses the model at that time into the clip, and calls back while it is so posed.</summary>
        static void Pose(GameObject model, AnimationClip clip, double t, Action posed)
        {
            if (!model.TryGetComponent(out Animator animator)) animator = model.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("Pose");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0);
                output.SetSourcePlayable(playable);
                graph.Play();
                playable.SetTime(t);
                graph.Evaluate(0f);
                posed();
            }
            finally
            {
                graph.Destroy();
            }
        }

        static Vector3 Centre(Renderer r)
        {
            if (!(r is SkinnedMeshRenderer s)) return r.bounds.center;
            var baked = new Mesh();
            try
            {
                s.BakeMesh(baked, true);
                Vector3 sum = Vector3.zero;
                Vector3[] verts = baked.vertices;
                foreach (Vector3 v in verts) sum += v;
                return s.transform.position + s.transform.rotation * (sum / Mathf.Max(1, verts.Length));
            }
            finally
            {
                Object.Destroy(baked);
            }
        }

        // ---------------- F8, the bar ----------------

        [UnityTest]
        public IEnumerator F8_SpawnsOneBoss_WithAThirdOfItsHealth_ItsBarShowsWhileItIsAwakeAndAlive()
        {
            EnemyBoss boss = SpawnBoss();
            yield return null;
            yield return null;
            Assert.That(Flat(boss.transform.position, Stand), Is.EqualTo(GeneratedLevels.SpawnDistance).Within(0.5f));
            Assert.That(boss.Body.MaxHealth, Is.EqualTo(Mathf.RoundToInt(boss.Tuning.Enemy.Health / 3f)), "a third of its health");
            Assert.That(boss.Body.Radius, Is.EqualTo(boss.Tuning.BodyRadius));
            Assert.That(boss.GetComponent<NavMeshAgent>().radius, Is.EqualTo(1.2f));
            Assert.That(boss.ModelHeight, Is.EqualTo((float)EnemyModelSpec.Boss.Height).Within(0.01f), "it wears the knight");
            Assert.That(boss.Look.Measured.Lift, Is.EqualTo(0f), "on its feet: the cape under them does not lift it");
            Assert.That(boss.Stagger.Max, Is.EqualTo(150f));
            Assert.That(boss.ComesBack, Is.False);
            var bar = boss.GetComponent<BossHealthBar>();
            Assert.That(bar, Is.Not.Null);
            Assert.That(bar.Showing, Is.False, "asleep, no bar");
            boss.Body.TakeHit(1);
            yield return Until(() => boss.IsAwake, 1f);
            Assert.That(bar.Showing, Is.True, "awake, its bar");
            Assert.That(bar.HealthShown, Is.LessThan(1f));
            boss.Body.TakeHit(1000000);
            yield return null;
            yield return null;
            Assert.That(boss.IsDead);
            Assert.That(bar.Showing, Is.False, "dead, no bar");
            _levels.ClearSpawned();
        }

        // ---------------- Cleave and Spin ----------------

        /// <summary>Waits for the next windup of an attack of that name; the time it began, or -1.</summary>
        static IEnumerator NextWindup(EnemyBoss boss, string name, Action<float> began, float timeout = 8f)
        {
            float at = -1f;
            float deadline = Time.realtimeSinceStartup + timeout;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (boss.State == EnemyState.Windup && boss.Attack != null && boss.Attack.Name == name)
                {
                    at = Time.time;
                    break;
                }
                yield return null;
            }
            began(at);
        }

        /// <summary>From a windup's start, until it strikes: checks the telegraph shows all through it, and gives the strike's time.</summary>
        IEnumerator UntilStrike(EnemyBoss boss, float began, Action<float> struck)
        {
            int strikes = boss.Strikes;
            bool shownThroughout = true;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (boss.Strikes == strikes && Time.realtimeSinceStartup < deadline)
            {
                if (!boss.IsTelegraphing) shownThroughout = false;
                yield return null;
            }
            Assert.That(boss.Strikes, Is.GreaterThan(strikes), "it struck");
            Assert.That(shownThroughout, Is.True, "the telegraph showed all through the windup");
            struck(Time.time);
        }

        [UnityTest]
        public IEnumerator CleaveAndSpin_TakeTurns_StrikeOnTime_HurtOnlyInsideTheirShape()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return null;
            Vector3 at = boss.transform.position;
            _player.Teleport(at + Vector3.back * 3f);
            boss.Body.TakeHit(1);

            // The cleave: she stands in its cone; it lands on time.
            float began = -1f, struck = -1f;
            yield return NextWindup(boss, BossValues.Cleave, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "a cleave");
            Assert.That(boss.Look.Playing, Is.EqualTo("Attack"));
            _health.RestoreFull();
            int hp = _health.Current;
            yield return UntilStrike(boss, began, t => struck = t);
            Assert.That(struck - began, Is.EqualTo(Named(boss, BossValues.Cleave).Windup).Within(0.1f), "on time");
            Assert.That(hp - _health.Current, Is.EqualTo(40), "inside the cone");
            // Its recovery: it stands.
            Assert.That(boss.State, Is.EqualTo(EnemyState.Recover));

            // Then the spin, not a cleave again: she stays in its circle.
            yield return NextWindup(boss, BossValues.Spin, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "the spin follows the cleave");
            Assert.That(boss.Look.Playing, Is.EqualTo("Spin"));
            _health.RestoreFull();
            hp = _health.Current;
            yield return UntilStrike(boss, began, t => struck = t);
            Assert.That(struck - began, Is.EqualTo(Named(boss, BossValues.Spin).Windup).Within(0.1f));
            Assert.That(hp - _health.Current, Is.EqualTo(35), "inside the circle");

            // A cleave again: she steps behind it, out of the cone, and is not hit.
            yield return NextWindup(boss, BossValues.Cleave, t => began = t);
            Assert.That(began, Is.GreaterThan(0f));
            _player.Teleport(at + Vector3.forward * 3f);
            _health.RestoreFull();
            hp = _health.Current;
            yield return UntilStrike(boss, began, t => struck = t);
            Assert.That(_health.Current, Is.EqualTo(hp), "behind it, out of the cone");

            // A spin: she steps out of the circle.
            _player.Teleport(at + Vector3.back * 3f);
            yield return NextWindup(boss, BossValues.Spin, t => began = t);
            Assert.That(began, Is.GreaterThan(0f));
            _player.Teleport(at + Vector3.back * (Named(boss, BossValues.Spin).Radius + 1.5f));
            _health.RestoreFull();
            hp = _health.Current;
            yield return UntilStrike(boss, began, t => struck = t);
            Assert.That(_health.Current, Is.EqualTo(hp), "out of the circle");

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyAttack && e.Enemy == "boss" && e.Name == BossValues.Cleave), Is.EqualTo(2));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Enemy == "boss" && e.Result == SessionWords.Landed), Is.EqualTo(2));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Enemy == "boss" && e.Result == SessionWords.OutOfArea), Is.EqualTo(2));
            _levels.ClearSpawned();
        }

        // ---------------- The charge ----------------

        [UnityTest]
        public IEnumerator Charge_RunsItsLine_HitsHerOnce_StopsShortOfAWall_EndsOnTheNavMesh_ThenStands()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            yield return null;
            Vector3 from = boss.transform.position;
            Vector3 dir = (new Vector3(Stand.x, 0f, Stand.z) - new Vector3(from.x, 0f, from.z)).normalized;
            // A wall 1 m behind her, across the line: it cuts the 14 m line at 13 m.
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.transform.SetPositionAndRotation(new Vector3(Stand.x, 1.5f, Stand.z) + dir * 1.25f, Quaternion.LookRotation(dir));
            wall.transform.localScale = new Vector3(8f, 3f, 0.5f);
            wall.AddComponent<Cover>();
            yield return null;
            Physics.SyncTransforms();
            EnemyAttackDefinition charge = Named(boss, BossValues.Charge);
            boss.Body.TakeHit(1);

            float began = -1f;
            yield return NextWindup(boss, BossValues.Charge, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "she is 12 m away and it sees her: a charge");
            Assert.That(boss.IsTelegraphing, Is.True);
            float expected = BossTactics.ChargeLength(charge.Length, 13f, float.MaxValue, boss.Tuning.ChargeStopShort);
            Assert.That(boss.ChargeReach, Is.EqualTo(expected).Within(0.1f), "cut at the wall, a little short of it");
            Assert.That(Flat(boss.AimPoint, Stand), Is.LessThan(0.1f), "aimed where she stood");
            _health.RestoreFull();
            int hp = _health.Current;
            float telegraphEnd = -1f;
            yield return Until(() => boss.IsCharging, 3f);
            telegraphEnd = Time.time;
            Assert.That(boss.IsCharging, Is.True);
            Assert.That(telegraphEnd - began, Is.EqualTo(charge.Windup).Within(0.1f), "it runs when the windup ends");
            Assert.That(boss.Look.Playing, Is.EqualTo("Run"), "it plays its run");
            float runStart = Time.time;
            int hits = 0, last = _health.Current;
            float off = 0f;
            while (boss.IsCharging && Time.time - runStart < 3f)
            {
                if (_health.Current < last) hits++;
                last = _health.Current;
                Vector3 p = boss.transform.position - from;
                p.y = 0f;
                off = Mathf.Max(off, Vector3.Cross(dir, p).magnitude);
                yield return null;
            }
            if (_health.Current < last) hits++;
            float ran = Time.time - runStart;
            Assert.That(boss.IsCharging, Is.False, "it stopped");
            Assert.That(ran, Is.EqualTo(boss.ChargeReach / charge.Rush).Within(0.15f), "at its speed");
            Assert.That(off, Is.LessThan(0.2f), "along the line");
            Assert.That(hits, Is.EqualTo(1), "it hit her once");
            Assert.That(hp - _health.Current, Is.EqualTo(charge.Damage));
            Vector3 end = boss.transform.position;
            Assert.That(Flat(end, from + dir * boss.ChargeReach), Is.LessThan(0.3f), "at the end of its cut line");
            Assert.That(Flat(end, from), Is.LessThan(13f - 0.5f), "short of the wall");
            Assert.That(NavMesh.SamplePosition(end, out NavMeshHit onMesh, 0.2f, NavMesh.AllAreas), Is.True, "on the NavMesh");

            // Its recovery: it stands where it stopped.
            yield return For(charge.Recovery - 0.2f);
            Assert.That(boss.State, Is.EqualTo(EnemyState.Recover));
            Assert.That(Flat(boss.transform.position, end), Is.LessThan(0.05f), "it stands");

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Count(e => e.Type == SessionEventTypes.BossCharge && e.Enemy == "boss"), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == BossValues.Charge && e.Result == SessionWords.Landed), Is.EqualTo(1));
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Enemies by kind, whole session", summary);
            StringAssert.Contains("boss", summary);
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator Charge_SteppingOutOfItsLine_DuringTheWindup_AvoidsIt()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            yield return null;
            Vector3 from = boss.transform.position;
            Vector3 dir = (new Vector3(Stand.x, 0f, Stand.z) - new Vector3(from.x, 0f, from.z)).normalized;
            EnemyAttackDefinition charge = Named(boss, BossValues.Charge);
            boss.Body.TakeHit(1);

            float began = -1f;
            yield return NextWindup(boss, BossValues.Charge, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "a charge at her 12 m away");
            // She steps aside, out of the 3 m wide line, while it winds up: the line stays where it was locked.
            Vector3 aside = Stand + Vector3.Cross(Vector3.up, dir) * (charge.HalfWidth + 3f);
            _player.Teleport(aside);
            _health.RestoreFull();
            int hp = _health.Current;
            yield return Until(() => boss.IsCharging, 3f);
            Assert.That(boss.IsCharging, Is.True);
            Assert.That(Flat(boss.AimPoint, Stand), Is.LessThan(0.1f), "still aimed where she stood");
            yield return Until(() => !boss.IsCharging, 3f);
            Assert.That(boss.IsCharging, Is.False);
            Assert.That(_health.Current, Is.EqualTo(hp), "beside the line, not hit");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == BossValues.Charge
                                                    && e.Result == SessionWords.OutOfArea), Is.EqualTo(1));
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator Charge_IsTheBlueAttack_StoppingShotFromTheFront_DropsIt_AndDazesTheBoss()
        {
            EnemyBoss boss = SpawnBoss();
            yield return null;
            int stop = -1;
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == "Stopping Shot") stop = i;
            Assert.That(stop, Is.GreaterThanOrEqualTo(0), "the pool has Stopping Shot");
            _player.EquipSkill(stop);
            Assert.That(Named(boss, BossValues.Charge).Counterable, Is.True);
            Assert.That(Named(boss, BossValues.Cleave).Counterable, Is.False, "the close attacks stay red");
            Assert.That(Named(boss, BossValues.Spin).Counterable, Is.False);
            Vector3 from = boss.transform.position;
            Vector3 dir = (new Vector3(Stand.x, 0f, Stand.z) - new Vector3(from.x, 0f, from.z)).normalized;
            boss.Body.TakeHit(1);

            float began = -1f;
            yield return NextWindup(boss, BossValues.Charge, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "a charge at her 12 m away");
            Assert.That(boss.TelegraphIsCounterable, Is.True, "drawn blue");
            // She stands in its line, in front of it, well inside the shot's range, and shoots it while it winds up.
            _player.Teleport(from + dir * 9f);
            _health.RestoreFull();
            int hp = _health.Current;
            Assert.That(_player.PressSkill(stop, boss.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => boss.Countered > 0 || boss.IsCharging, 3f);
            Assert.That(boss.Countered, Is.EqualTo(1));
            Assert.That(boss.IsDazed, Is.True);
            Assert.That(boss.IsCharging, Is.False, "the charge never starts");
            yield return For(1f);
            Assert.That(boss.IsCharging, Is.False);
            Assert.That(_health.Current, Is.EqualTo(hp), "in its line, and not hit");
            _player.EquipStartSkills();
            _levels.ClearSpawned();
        }

        // ---------------- The stagger bar ----------------

        [UnityTest]
        public IEnumerator StaggerBar_Emptied_DazesIt_TheWindupIsDropped_ACharge_Stops()
        {
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.Enemy.MoveSpeed = 0f;
            yield return null;
            Vector3 at = boss.transform.position;
            _player.Teleport(at + Vector3.back * 3f);
            boss.Body.TakeHit(1);
            float began = -1f;
            yield return NextWindup(boss, BossValues.Cleave, t => began = t);
            Assert.That(began, Is.GreaterThan(0f));
            int strikes = boss.Strikes;
            boss.Body.TakeHit(1, boss.Tuning.Stagger.Max * 0.5f);
            yield return null;
            Assert.That(boss.IsDazed, Is.False, "half the bar is not enough");
            boss.Body.TakeHit(1, boss.Tuning.Stagger.Max);
            yield return null;
            Assert.That(boss.IsDazed, Is.True, "the empty bar dazes it");
            Assert.That(boss.State, Is.Not.EqualTo(EnemyState.Windup), "the windup is dropped");
            Assert.That(boss.IsTelegraphing, Is.False);
            yield return null;
            Assert.That(boss.Look.Playing, Is.EqualTo("Stagger"));
            yield return For(1f);
            Assert.That(boss.Strikes, Is.EqualTo(strikes), "no strike");
            int hp = boss.Body.CurrentHealth;
            boss.Body.TakeHit(100);
            Assert.That(hp - boss.Body.CurrentHealth, Is.EqualTo(Mathf.RoundToInt(100 * boss.Tuning.Stagger.DazedDamageMultiplier)), "it takes more damage");
            yield return Until(() => !boss.IsDazed, boss.Tuning.Stagger.DazeSeconds + 1f);
            yield return null;
            Assert.That(boss.Stagger.Current, Is.EqualTo(boss.Stagger.Max), "full again, as the guard's");

            // Dazed in the middle of a charge, it stops where it is.
            boss.Tuning.Enemy.MoveSpeed = 3f;
            _player.Teleport(Stand);
            yield return Until(() => boss.IsCharging, 10f);
            Assert.That(boss.IsCharging, Is.True, "a charge");
            yield return null;
            boss.Body.TakeHit(1, boss.Tuning.Stagger.Max);
            yield return null;
            Assert.That(boss.IsCharging, Is.False);
            Vector3 stopped = boss.transform.position;
            yield return For(0.5f);
            Assert.That(Flat(boss.transform.position, stopped), Is.LessThan(0.05f), "it stays where it was dazed");
            _levels.ClearSpawned();
        }

        // ---------------- Calls and anger ----------------

        [UnityTest]
        public IEnumerator AtTwoThirdsAndOneThird_SixSwarmersCome_OnceEach()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            yield return null;
            yield return null;
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f));
            yield return null;
            yield return null;
            Assert.That(boss.PacksCalled, Is.EqualTo(1));
            Assert.That(boss.Called.Count, Is.EqualTo(6));
            Assert.That(boss.Called.All(e => e is EnemySwarmer));
            yield return Until(() => boss.Called.All(e => e.IsAwake), 1f);
            Assert.That(boss.Called.All(e => e.IsAwake), "they come awake");
            Assert.That(boss.Called.All(e => Flat(e.transform.position, boss.transform.position) < boss.Tuning.CallNearRadius + 1f), "round it, in the practice room");
            boss.Body.TakeHit(max / 10);
            yield return null;
            Assert.That(boss.PacksCalled, Is.EqualTo(1), "once");
            boss.Body.TakeHit(boss.Body.CurrentHealth - Mathf.FloorToInt(max * 0.33f));
            yield return null;
            yield return null;
            Assert.That(boss.PacksCalled, Is.EqualTo(2));
            Assert.That(boss.Called.Count, Is.EqualTo(12));
            boss.Body.TakeHit(max / 10);
            yield return null;
            Assert.That(boss.Called.Count, Is.EqualTo(12), "once each");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.BossCall && e.ExtraValue("called") == "6"), Is.EqualTo(2));
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.BossAnger), Is.EqualTo(1), "and below half it turned angry");
            _levels.ClearSpawned();
        }

        [UnityTest]
        public IEnumerator BelowHalf_Angry_ShorterWindups_TintedWarm_AndTheChargeComesTwice()
        {
            Record();
            EnemyBoss boss = SpawnBoss();
            boss.Tuning.CallsAt = new float[0]; // no packs in the way here
            yield return null;
            yield return null;
            Color calm = boss.BodyRenderer.sharedMaterial.color;
            float calmWindup = Named(boss, BossValues.Charge).Windup;
            boss.Body.TakeHit(boss.Body.MaxHealth / 2 + 10);
            yield return null;
            Assert.That(boss.IsAngry, Is.True);
            Color angry = boss.BodyRenderer.sharedMaterial.color;
            Assert.That(angry, Is.Not.EqualTo(calm), "tinted");
            Assert.That(angry.r, Is.GreaterThan(angry.b), "warm");
            Assert.That(Named(boss, BossValues.Charge).Windup, Is.EqualTo(calmWindup * 0.8f).Within(1e-5f));

            float began = -1f;
            yield return NextWindup(boss, BossValues.Charge, t => began = t);
            Assert.That(began, Is.GreaterThan(0f), "a charge at her 12 m away");
            yield return Until(() => boss.IsCharging, 3f);
            Assert.That(Time.time - began, Is.EqualTo(calmWindup * 0.8f).Within(0.1f), "the shorter windup");
            yield return Until(() => !boss.IsCharging, 3f);
            // The second, at once, aimed at where she stands now.
            yield return null;
            Assert.That(boss.State, Is.EqualTo(EnemyState.Windup));
            Assert.That(boss.Attack.Name, Is.EqualTo(BossValues.Charge));
            Assert.That(boss.IsTelegraphing, Is.True, "its own telegraph");
            Assert.That(Flat(boss.AimPoint, _player.Position), Is.LessThan(0.1f));
            yield return Until(() => boss.IsCharging, 3f);
            yield return Until(() => !boss.IsCharging, 3f);
            yield return null;
            Assert.That(boss.State, Is.EqualTo(EnemyState.Recover), "two, then its recovery");
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.BossCharge), Is.EqualTo(2));
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.BossAnger), Is.EqualTo(1));
            _levels.ClearSpawned();
        }

        // ---------------- In its floor ----------------

        [UnityTest]
        public IEnumerator InFloor5_ItWakesAsSheEntersItsRoom_DoesNotGoHome_CallsAlongTheWalls_AndGoesWithTheFloor()
        {
            _practiceEnemy.enabled = true;
            GeneratedLevel level = _levels.Build(42UL, 5);
            yield return null;
            var boss = level.Boss as EnemyBoss;
            Assert.That(boss, Is.Not.Null, "the act boss is the boss");
            Assert.That(boss.Body.MaxHealth, Is.EqualTo(level.Values.BossHealth));
            LevelRoom exit = level.Layout.ExitRoom;
            Assert.That(level.RoomAt(boss.transform.position), Is.EqualTo(exit));
            yield return For(0.3f);
            Assert.That(boss.IsAwake, Is.False, "asleep while she is elsewhere");

            // She steps into its room, far from it: onto the middle of its open cell nearest the south-west corner of its
            // rectangle. Work order 056 (the first Unity run of the night's combination, 2026-11-21): this put her 2.5 m in
            // from that corner, in the corner cell itself, which work order 051 may carve (an octagon's, a cross's: this
            // seed's exit room is an octagon), and a carved cell is in no room, as it is behind the walls.
            // Work order 070 (a room is a fight): the room wakes, the boss with it, once she is past the room's point, 5 m in
            // from the door she came by (the room's door nearest her): the nearest such cell to that corner.
            double cs = level.Layout.CellSize;
            int ix = -1, iy = -1;
            for (int d = 0; d < exit.Rect.Width + exit.Rect.Height && ix < 0; d++)
                for (int i = 0; i <= d && ix < 0; i++)
                {
                    int cx = exit.Rect.X + i, cy = exit.Rect.Y + d - i;
                    var at = new LevelPoint((cx + 0.5) * cs, (cy + 0.5) * cs);
                    if (exit.Rect.Contains(cx, cy) && level.Layout.RoomAtCell(cx, cy) == exit
                        && RoomFightRules.PastThePoint(level.Layout, exit, RoomFightRules.DoorSheCameBy(level.Layout, exit, at), at, level.Values.Fights)) { ix = cx; iy = cy; }
                }
            Assert.That(ix, Is.GreaterThanOrEqualTo(0), "an open cell in its room");
            Vector3 inside = level.ToWorld(new LevelPoint((ix + 0.5) * cs, (iy + 0.5) * cs));
            Assume.That(Flat(inside, boss.transform.position), Is.GreaterThan(boss.Tuning.Enemy.AggroRange));
            _player.Teleport(inside);
            yield return Until(() => _levels.EntranceHolds || boss.IsAwake, 1f);
            _levels.SkipEntrance(); // work order 130 (changed deliberately): its entrance comes first; skipped, the room wakes as at the point
            yield return Until(() => boss.IsAwake, 1f);
            Assert.That(boss.IsAwake, Is.True, "she entered its room");
            Assert.That(boss.GetComponent<BossHealthBar>().Showing, Is.True);

            // She leaves, far: it does not give up.
            _player.Teleport(level.PlayerStart);
            yield return For(2f);
            Assert.That(boss.IsAwake, Is.True, "it does not go home");
            Assert.That(Flat(boss.transform.position, boss.Home), Is.GreaterThan(1f), "it comes after her");

            // Its first call: six swarmers of the floor's health along the walls of its room, awake.
            int max = boss.Body.MaxHealth;
            boss.Body.TakeHit(max - Mathf.FloorToInt(max * 0.66f));
            yield return null;
            yield return null;
            Assert.That(level.Called.Count, Is.EqualTo(6));
            foreach (Enemy e in level.Called)
            {
                Assert.That(level.RoomAt(e.transform.position), Is.EqualTo(exit), e.name + " in its room");
                LevelPoint p = level.ToLevel(e.transform.position);
                Assert.That(LevelGeometry.DistanceToWalls(p, exit.Rect, cs), Is.LessThan(level.Values.WallClearance + 1.5), e.name + " along a wall");
                Assert.That(e.Body.MaxHealth, Is.EqualTo(level.Values.SwarmerHealth));
                Assert.That(NavMesh.SamplePosition(e.transform.position, out NavMeshHit _, 0.3f, NavMesh.AllAreas), Is.True);
            }
            Assert.That(level.ExitEnemies.Contains(level.Called[0]), Is.False, "the floor does not wait for them");
            yield return Until(() => level.Called.All(e => e.IsAwake), 1f);
            Assert.That(level.Called.All(e => e.IsAwake), "they come for her");

            // Leaving the floor takes it, and its bar, away.
            _levels.BackToPracticeRoom();
            yield return null;
            yield return null;
            Assert.That(boss == null, "gone with the floor");
        }

        /// <summary>
        /// Work order 056 (the first Unity run of the night's combination, 2026-11-21): on floor 5 with the rooms carved (work
        /// order 051, a gate inside every door in), she walks into the exit room through its door and its gate with the move
        /// keys, as a player does, from 4 m before the door to 5 m past it: she is in its room from the step she is past the
        /// door, still far from the boss, and it wakes. The floor's other enemies are switched off, so none stops her.
        /// </summary>
        [UnityTest, Timeout(180000)]
        public IEnumerator InFloor5_WithTheShapes_SheWalksInThroughTheDoorAndItsGate_IsInItsRoom_AndItWakes(
            [Values(42UL, 0x12345678UL, 7UL, 20261121UL)] ulong seed)
        {
            _practiceEnemy.enabled = true;
            _levels.Values = new LevelValues { Passages = false }; // work order 059: a gate stands only where rooms touch; a passage's mouth has none
            GeneratedLevel level = _levels.Build(seed, 5);
            yield return null;
            Assert.That(level.Layout.Shapes, Is.True, "the rooms are carved");
            var boss = level.Boss as EnemyBoss;
            Assert.That(boss, Is.Not.Null, "the act boss");
            foreach (Enemy e in level.Enemies.ToList())
                if (e != boss) e.gameObject.SetActive(false);
            LevelRoom exit = level.Layout.ExitRoom;
            LevelDoor door = LevelGeometry.EntryDoor(level.Layout, exit);
            Assert.That(door, Is.Not.Null, "its door in");
            Assert.That(RoomShapes.HasGate(exit, door), Is.True, "a gate inside its door");
            door.Opening(level.Layout.CellSize, out LevelPoint a, out LevelPoint b);
            Vector3 wa = level.ToWorld(a), wb = level.ToWorld(b);
            Vector3 middle = (wa + wb) * 0.5f;
            Vector3 along = (wb - wa).normalized;
            Vector3 into = new Vector3(-along.z, 0f, along.x);
            if (Vector3.Dot(level.Middle(exit) - middle, into) < 0f) into = -into;

            _player.Teleport(middle - into * 4f);
            yield return For(0.3f);
            Assert.That(level.RoomAt(_player.Position), Is.Not.EqualTo(exit), "before the door, not yet in its room");
            Assert.That(boss.IsAwake, Is.False, "asleep while she is outside its room");

            float until = Time.time + 8f, realUntil = Time.realtimeSinceStartup + 60f;
            float reached = float.MinValue, firstInRoom = float.MinValue, farWhenIn = -1f;
            while (Time.time < until && Time.realtimeSinceStartup < realUntil && reached < 5f)
            {
                _player.Steer(into);
                yield return null;
                _levels.SkipEntrance(); // work order 130 (changed deliberately): its entrance, as she crosses the point, skipped at once
                reached = Mathf.Max(reached, Vector3.Dot(_player.Position - middle, into));
                if (firstInRoom == float.MinValue && level.RoomAt(_player.Position) == exit)
                {
                    firstInRoom = Vector3.Dot(_player.Position - middle, into);
                    farWhenIn = Flat(_player.Position, boss.transform.position);
                }
            }
            _player.Stop();
            yield return null;
            _levels.SkipEntrance(); // work order 130: begun on the loop's last step
            yield return null;
            yield return null;
            Debug.Log($"[056] seed {LevelSeed.ToText(seed)} floor 5: exit room {exit.Shape}, in its room {firstInRoom:0.00} m past the door, "
                      + $"{farWhenIn:0.0} m from the boss; she got {reached:0.00} m in; awake {boss.IsAwake}");
            Assert.That(reached, Is.GreaterThanOrEqualTo(5f), "she walked in through the door and its gate");
            Assert.That(firstInRoom, Is.GreaterThan(-0.5f).And.LessThan((float)level.Layout.CellSize), "in its room once past the door, in its gate's slot");
            Assert.That(level.RoomAt(_player.Position), Is.EqualTo(exit), "she is in its room");
            Assert.That(_levels.CurrentRoom, Is.EqualTo(exit), "the floor knows she is in its room");
            Assert.That(farWhenIn, Is.GreaterThan(boss.Tuning.Enemy.AggroRange), "far from it as she came in: the room wakes it, not its reach");
            Assert.That(boss.IsAwake, Is.True, "she entered its room: it wakes");
            Assert.That(boss.GetComponent<BossHealthBar>().Showing, Is.True);
            _levels.BackToPracticeRoom();
            yield return null;
        }
    }
}
