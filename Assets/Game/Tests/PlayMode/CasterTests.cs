using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Animations;
using UnityEngine.InputSystem;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The caster (work order 027, Kristian's request for a fifth kind, 2026-11-20), in the ControlRoom scene, spawned
    /// through the practice room's intent (<see cref="GeneratedLevels.SpawnPractice"/>, its key F4) and fought through
    /// the character's intents: its model as Unity imports it; it keeps its distance, backs off and comes closer; it
    /// begins no cast it cannot see her for, and a begun cast goes off; Bone Spikes' circle shows where she stood for its
    /// wind-up, bursts on time and hurts her only inside it; Grave Rain places three on time where the rule says, standing
    /// still takes three hits and a turn after each avoids the second and third; Stopping Shot from its front in the first
    /// wind-up drops it and fades what is on the ground, from behind it does not; a circle off the walkable ground is not
    /// placed; a dead caster's placed spike still bursts and one not yet placed is not; its emission glows and its clips
    /// follow what it does; a floor 3 holds casters within the caps. Every number is read from the values.
    ///
    /// The practice brute is switched off (it stays in the scene as the template the spawns copy their telegraph from).
    /// She stands at <see cref="Stand"/> with health enough for every test; the spawn comes 12 m from her.
    /// </summary>
    public class CasterTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        const double ClipFrame = 1.0 / EnemyModelSpec.FramesPerSecond;

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
            _health.MaxHealth = 100000; // she must not fall in the middle of a test
            _health.RestoreFull();
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
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        /// <summary>One caster from the practice room's intent, tuned before its first frame.</summary>
        EnemyCaster Spawn(Action<CasterValues> tune = null)
        {
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Caster);
            Assert.That(made.Count, Is.EqualTo(1), "a caster spawned");
            var caster = made[0] as EnemyCaster;
            Assert.That(caster, Is.Not.Null, "of its own kind");
            tune?.Invoke(caster.Tuning);
            return caster;
        }

        /// <summary>She stands this far from the enemy, on the side she came from.</summary>
        void StandAt(Enemy e, float distance)
        {
            Vector3 dir = Flat(_player.Position - e.transform.position);
            dir = dir.sqrMagnitude > 1e-4f ? dir.normalized : Vector3.back;
            _player.Teleport(Flat(e.transform.position) + dir * distance);
        }

        /// <summary>Metres of walkable ground from a point along a flat direction, up to <paramref name="max"/>.</summary>
        static float Room(Vector3 from, Vector3 dir, float max, out Vector3 edge)
        {
            edge = from + dir * max;
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)) return 0f;
            if (!NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas)) return max;
            edge = hit.position;
            return Flat(hit.position, start.position);
        }

        /// <summary>Of the eight ways round, the one with the most walkable ground from the point.</summary>
        static Vector3 MostRoom(Vector3 from, float max, out float room)
        {
            Vector3 best = Vector3.forward;
            room = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float r = Room(from, dir, max, out _);
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        static bool Casting(EnemyCaster c, string cast) => c.State == EnemyState.Windup && c.Attack != null && c.Attack.Name == cast;

        // ---------------- The model ----------------

        [Test]
        public void TheModel_ImportsAsTheTableSays()
        {
            EnemyModelSpec spec = EnemyModelSpec.Caster;
            GameObject source = EnemyLook.Source(spec);
            Assert.That(source, Is.Not.Null, "loads from Resources/" + spec.ResourcePath);
            var root = new GameObject("Import " + spec.Name);
            _made.Add(root);
            GameObject model = Object.Instantiate(source, root.transform, false);
            SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Assert.That(skins.Length, Is.EqualTo(1), "one skinned mesh");
            SkinnedMeshRenderer skin = skins[0];
            long indices = 0;
            for (int s = 0; s < skin.sharedMesh.subMeshCount; s++) indices += skin.sharedMesh.GetIndexCount(s); // the mesh is not readable
            int triangles = (int)(indices / 3);
            float height = EnemyLook.BindHeight(skin, root.transform);
            EnemyLook.SkinBounds(skin, root.transform, out Vector3 lo, out Vector3 hi);
            AnimationClip[] clips = EnemyLook.Clips(spec);
            float walk = StrideSpeed(model, spec, "Walk"), run = StrideSpeed(model, spec, "Run");
            float castHand = HandOverHead(model, spec, "Cast", spec.Clip("Cast").Strike, "RightHand");
            float bigLeft = HandOverHead(model, spec, "CastBig", spec.Clip("CastBig").Strike, "LeftHand");
            float bigRight = HandOverHead(model, spec, "CastBig", spec.Clip("CastBig").Strike, "RightHand");
            Texture2D emission = Resources.Load<Texture2D>(spec.EmissionPath);
            Debug.Log(string.Format(s_Inv,
                "CASTERIMPORT {0}: height as bound {1:0.###} m (table {2}), in the file's pose {3:0.###} m, bottom {4:0.###}; skin '{5}' on {6} bones, {7} triangles; " +
                "takes: {8}; stride speeds as imported: Walk {9:0.###} m/s (spec {10}), Run {11:0.###} m/s (spec {12}); at the releases, over the head: " +
                "Cast's right hand {13:0.###} m, CastBig's hands {14:0.###} and {15:0.###} m; emission map {16}",
                spec.Name, height, spec.Height, hi.y - lo.y, lo.y, skin.name, skin.bones.Length, triangles,
                string.Join(", ", clips.Select(c => c.name + " " + c.length.ToString("0.###", s_Inv) + " s" + (c.isLooping ? " loop" : ""))),
                walk, spec.WalkStride, run, spec.RunStride, castHand, bigLeft, bigRight, emission != null ? emission.name : "none"));

            Assert.That(height, Is.EqualTo((float)spec.Height).Within((float)spec.Height * 0.05f), "1.9 m to the top of the hood as made");
            Assert.That(skin.bones.Length, Is.EqualTo(24), "24 bones");
            Assert.That(triangles, Is.EqualTo(8482).Within(85), "about 8,482 triangles");
            foreach (EnemyClipSpec c in spec.Clips)
            {
                AnimationClip clip = EnemyLook.FindClip(spec, c.Name);
                Assert.That(clip, Is.Not.Null, c.Name);
                Assert.That(clip.name, Is.EqualTo("Armature|" + c.Name), "the take's name in Unity");
                Assert.That(clip.length, Is.EqualTo((float)c.Length).Within((float)ClipFrame + 1e-4f), c.Name);
            }
            Assert.That(walk, Is.EqualTo((float)spec.WalkStride).Within((float)spec.WalkStride * 0.15f), "the walk's stride as the spec has it");
            Assert.That(run, Is.EqualTo((float)spec.RunStride).Within((float)spec.RunStride * 0.15f), "the run's stride as the spec has it");
            Assert.That(castHand, Is.GreaterThan(0f), "Cast throws the right hand up at its release");
            Assert.That(Mathf.Min(bigLeft, bigRight), Is.GreaterThan(0f), "CastBig throws both hands overhead at its release");
            Assert.That(emission, Is.Not.Null, "the emission map is beside it");
        }

        /// <summary>Plays one clip on the model at a time and calls back after each frame of 1/30 s is posed.</summary>
        static void Sample(GameObject model, AnimationClip clip, Action<double> posed)
        {
            if (!model.TryGetComponent(out Animator animator)) animator = model.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("Sample");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0);
                output.SetSourcePlayable(playable);
                graph.Play();
                int frames = Mathf.CeilToInt(clip.length * (float)EnemyModelSpec.FramesPerSecond);
                for (int i = 0; i <= frames; i++)
                {
                    double t = Math.Min(clip.length, i / EnemyModelSpec.FramesPerSecond);
                    playable.SetTime(t);
                    graph.Evaluate(0f);
                    posed(t);
                }
            }
            finally
            {
                graph.Destroy();
            }
        }

        /// <summary>As the import test of the other four measures it: each foot's sweep under the hips, two steps a cycle.</summary>
        static float StrideSpeed(GameObject model, EnemyModelSpec spec, string name)
        {
            AnimationClip clip = EnemyLook.FindClip(spec, name);
            if (clip == null || clip.length <= 0f) return 0f;
            Transform hips = EnemyLook.Find(model.transform, "Hips");
            float total = 0f;
            foreach (string side in new[] { "LeftFoot", "RightFoot" })
            {
                Transform foot = EnemyLook.Find(model.transform, side);
                if (foot == null || hips == null) return 0f;
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                Vector3 forward = Quaternion.Euler(0f, EnemyLook.FacingOf(model, model.transform.parent), 0f) * Vector3.forward;
                Sample(model, clip, _ =>
                {
                    float along = Vector3.Dot(foot.position - hips.position, forward);
                    lo = Mathf.Min(lo, along);
                    hi = Mathf.Max(hi, along);
                });
                total += hi - lo;
            }
            return total / clip.length;
        }

        /// <summary>Metres the hand is over the head at that time into the clip.</summary>
        static float HandOverHead(GameObject model, EnemyModelSpec spec, string name, double time, string hand)
        {
            AnimationClip clip = EnemyLook.FindClip(spec, name);
            Transform h = EnemyLook.Find(model.transform, hand), head = EnemyLook.Find(model.transform, "Head");
            if (clip == null || h == null || head == null) return float.NaN;
            float over = float.NaN;
            Sample(model, clip, t =>
            {
                if (Math.Abs(t - time) <= 0.5 / EnemyModelSpec.FramesPerSecond) over = h.position.y - head.position.y;
            });
            return over;
        }

        [UnityTest]
        public IEnumerator ItWearsItsModel_ItsEmissionGlows_AndItsClipsFollowWhatItDoes()
        {
            EnemyCaster caster = Spawn();
            yield return null;
            Assert.That(caster.Look, Is.Not.Null, "it wears its model");
            Assert.That(caster.Look.Spec, Is.SameAs(EnemyModelSpec.Caster));
            Texture emission = EnemyLook.EmissionOf(caster.Look.Skin);
            Assert.That(emission, Is.Not.Null, "its emission map is on");
            StringAssert.Contains("emission", emission.name);
            Color glow = caster.Look.Skin.sharedMaterial.GetColor("_EmissionColor");
            Debug.Log("CASTERGLOW emission " + emission.name + ", colour " + glow);
            Assert.That(glow.g, Is.GreaterThan(glow.r), "in the game's cyan");
            Assert.That(glow.b, Is.GreaterThan(0.5f), "bright");
            Assert.That(caster.ModelHeight, Is.EqualTo(1.9f).Within(1e-4f));

            // Far from her it walks closer, in its walk.
            Vector3 dir = MostRoom(caster.transform.position, 30f, out float room);
            _player.Teleport(Flat(caster.transform.position) + dir * Mathf.Min(room - 1f, 20f));
            yield return Until(() => caster.State == EnemyState.Chase && caster.GetComponent<NavMeshAgent>().velocity.magnitude > 1f, 4f);
            yield return For(0.3f);
            Assert.That(caster.Look.Playing, Is.EqualTo("Walk"), "at 2.6 m/s it walks");
            // Dazed it staggers; dead it plays its Death.
            caster.Body.DazeFor(1f);
            yield return null;
            Assert.That(caster.Look.Playing, Is.EqualTo("Stagger"));
            caster.Body.TakeHit(caster.Body.MaxHealth);
            yield return null;
            Assert.That(caster.Look.Playing, Is.EqualTo("Death"));
        }

        /// <summary>Waits for its next strike (its cast's first burst) and reads the clip it plays at that moment.</summary>
        IEnumerator MeasureRelease(EnemyCaster e, string cast, string clip)
        {
            int strikes = e.Strikes;
            double windupClip = -1;
            yield return Until(() =>
            {
                if (Casting(e, cast) && e.Look.Playing == clip) windupClip = e.Look.PlayingTime;
                return e.Strikes > strikes;
            }, 12f);
            Assert.That(e.Strikes, Is.GreaterThan(strikes), cast + " went off");
            Assert.That(e.Look.Playing, Is.EqualTo(clip), cast + " plays " + clip);
            EnemyClipSpec spec = e.Look.Spec.Clip(clip);
            double gap = e.Look.PlayingTime - spec.Strike;
            AttackClipPlan plan = e.Look.Plan;
            Debug.Log(string.Format(s_Inv, "ANIMSYNC caster {0}: windup {1:0.###} s; clip {2} ({3:0.###} s, release {4:0.###}) from {5:0.###} at x{6:0.###}, after it x{7:0.###}; at the game's strike {8:0.####} (gap {9:+0.####;-0.####;0} s)",
                cast, e.Attack.Windup, clip, spec.Length, spec.Strike, plan.Start, plan.WindupSpeed, plan.RecoverySpeed, e.Look.PlayingTime, gap));
            Assert.That(Math.Abs(gap), Is.LessThanOrEqualTo(ClipFrame), cast + ": the release on the game's strike");
            Assert.That(windupClip, Is.GreaterThanOrEqualTo(0), "the clip played in the windup");
        }

        [UnityTest]
        public IEnumerator EachCastsClip_ReleasesOnItsBurst()
        {
            EnemyCaster caster = Spawn();
            yield return MeasureRelease(caster, CasterValues.BoneSpikes, "Cast");
            caster.Tuning.BigCastEvery = 2; // the second cast a Grave Rain
            yield return MeasureRelease(caster, CasterValues.GraveRain, "CastBig");
        }

        // ---------------- Its distance and its sight ----------------

        [UnityTest]
        public IEnumerator ItKeepsItsDistance_BacksOffWhenSheComesNear_AndComesCloserWhenSheIsFar()
        {
            EnemyCaster caster = Spawn();
            CasterValues v = caster.Tuning;
            yield return For(2f);
            Assert.That(caster.IsAwake, Is.True, "12 m away, within its aggro range");
            float d = Flat(caster.transform.position, _player.Position);
            Assert.That(d, Is.InRange(v.KeepNear - 0.5f, v.KeepFar + 0.5f), "it stands off");

            // She walks up to 3 m on its far side: it backs off.
            Vector3 away = Flat(caster.transform.position - _player.Position).normalized;
            _player.Teleport(caster.transform.position + away * 3f);
            yield return Until(() => caster.IsBackingOff, 5f);
            Assert.That(caster.IsBackingOff, Is.True, "nearer than " + v.BackOffUnder + " m it backs off");
            yield return Until(() => Flat(caster.transform.position, _player.Position) >= v.KeepNear - 0.3f, 10f);
            Assert.That(Flat(caster.transform.position, _player.Position), Is.GreaterThanOrEqualTo(v.KeepNear - 0.3f), "back to its distance");
            yield return For(1f);
            Assert.That(caster.IsBackingOff, Is.False, "and stops");

            // She goes far: it comes closer.
            Vector3 dir = MostRoom(caster.transform.position, 30f, out float room);
            _player.Teleport(Flat(caster.transform.position) + dir * Mathf.Min(room - 1f, 20f));
            yield return null;
            float far = Flat(caster.transform.position, _player.Position);
            Assert.That(far, Is.GreaterThan(v.KeepFar + 1f), "she is farther than " + v.KeepFar + " m (" + room + " m of room)");
            // As the shooter, it casts again as soon as she is within its casts' reach (15 m), so it comes that near.
            float reach = v.Enemy.Attacks.Max(x => x.MaxDistance);
            yield return Until(() => Flat(caster.transform.position, _player.Position) <= reach + 0.3f && caster.State == EnemyState.Windup, 12f);
            float near = Flat(caster.transform.position, _player.Position);
            Debug.Log(string.Format(s_Inv, "CASTERDISTANCE stood off at {0:0.0} m; she went to {1:0.0} m and it came to {2:0.0} m", d, far, near));
            Assert.That(near, Is.LessThanOrEqualTo(reach + 0.3f), "it came closer, into its casts' reach");
            Assert.That(far - near, Is.GreaterThan(3f), "it walked toward her");
        }

        /// <summary>A wall between her and the caster, cover as a level's walls are.</summary>
        GameObject WallBetween(Enemy e)
        {
            Vector3 mid = (e.transform.position + _player.Position) * 0.5f;
            Vector3 across = e.transform.position - _player.Position;
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _made.Add(wall);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(mid.x, 1.5f, mid.z), Quaternion.LookRotation(new Vector3(across.x, 0f, across.z)));
            wall.transform.localScale = new Vector3(4f, 3f, 0.5f);
            wall.AddComponent<Cover>();
            Physics.SyncTransforms(); // frames without a window are far shorter than a physics step
            return wall;
        }

        [UnityTest]
        public IEnumerator ItBeginsNoCastItCannotSeeHerFor_ButABegunCastGoesOff()
        {
            EnemyCaster caster = Spawn(v => v.Enemy.MoveSpeed = 0f); // it would walk round: here it must stand behind the wall
            GameObject wall = WallBetween(caster);
            yield return null;
            Assert.That(caster.SeesHer(), Is.False, "the wall is between them");
            caster.Body.TakeHit(1);
            yield return For(3f);
            Assert.That(caster.IsAwake, Is.True);
            Assert.That(caster.CastsBegun, Is.EqualTo(0), "no cast through the wall");
            Assert.That(caster.Circles, Is.Empty);

            Object.Destroy(wall);
            yield return null;
            yield return Until(() => caster.State == EnemyState.Windup, 4f);
            Assert.That(caster.State, Is.EqualTo(EnemyState.Windup), "with the wall gone it casts");
            EnemyCaster.Circle circle = caster.Circles.Single();
            int health = _health.Current;
            WallBetween(caster);
            yield return null;
            Assert.That(caster.SeesHer(), Is.False, "a wall between them again");
            yield return Until(() => circle.HasBurst, 3f);
            Assert.That(circle.HasBurst, Is.True, "the cast begun goes off");
            Assert.That(circle.Hit, Is.True, "on her, who stood in it");
            Assert.That(health - _health.Current, Is.EqualTo(circle.Attack.Damage));
        }

        // ---------------- Bone Spikes ----------------

        [UnityTest]
        public IEnumerator BoneSpikes_ShowsWhereSheStood_ForItsWindup_BurstsOnTime_AndHurtsHerOnlyInsideIt()
        {
            EnemyCaster caster = Spawn();
            EnemyAttackDefinition spikes = CasterTactics.Find(caster.Tuning.Enemy, CasterValues.BoneSpikes);
            yield return Until(() => caster.State == EnemyState.Windup, 4f);
            Assert.That(caster.Attack.Name, Is.EqualTo(CasterValues.BoneSpikes), "its first cast");
            EnemyCaster.Circle c = caster.Circles.Single();
            Assert.That(c.Number, Is.EqualTo(1));
            Assert.That(Flat(c.Center, _player.Position), Is.LessThan(0.05f), "where she stands");
            Assert.That(Flat(c.Center, caster.AimPoint), Is.LessThan(1e-3f), "where she stood when the wind-up began");
            Assert.That(Flat(c.Center, caster.transform.position), Is.GreaterThan(4f), "not at the caster");
            Assert.That(c.IsShown, Is.True, "its outline on the ground");
            Assert.That(c.IsBlue, Is.False, "red: it cannot be countered");
            Assert.That(c.Radius, Is.EqualTo(spikes.Radius));
            Assert.That(c.BurstAt - c.PlacedAt, Is.EqualTo(spikes.Windup).Within(1e-4f));
            Assert.That(caster.IsTelegraphing, Is.False, "nothing drawn round the caster itself");
            float thin = c.Width;
            yield return For(spikes.Windup * 0.6f);
            Assert.That(c.IsShown, Is.True, "for its whole wind-up");
            Assert.That(c.Width, Is.GreaterThan(thin), "thickening");

            int health = _health.Current;
            yield return Until(() => c.HasBurst, 2f);
            float seen = Time.time;
            Assert.That(c.HasBurst, Is.True);
            Assert.That(seen - c.PlacedAt, Is.InRange(spikes.Windup - 1e-3f, spikes.Windup + 0.05f), "on time");
            Assert.That(c.Hit, Is.True, "she stood in it");
            Assert.That(health - _health.Current, Is.EqualTo(spikes.Damage));
            Assert.That(c.IsShown, Is.False, "gone once it has burst");

            // The next: she steps out sideways during its wind-up.
            yield return Until(() => caster.State == EnemyState.Windup && caster.CastsBegun == 2, 5f);
            Assert.That(caster.Attack.Name, Is.EqualTo(CasterValues.BoneSpikes), "its second cast");
            EnemyCaster.Circle c2 = caster.Circles.Last();
            Vector3 toCaster = Flat(caster.transform.position - _player.Position).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, toCaster);
            if (Room(_player.Position, side, 6f, out _) < 5f) side = -side;
            health = _health.Current;
            _player.Steer(side);
            yield return Until(() => Flat(_player.Position, c2.Center) >= c2.Radius + Placeholders.PlayerRadius + 0.3f, spikes.Windup);
            _player.Steer(Vector3.zero);
            Assert.That(c2.IsPending, Is.True, "out before it bursts");
            yield return Until(() => c2.HasBurst, 2f);
            Assert.That(c2.HasBurst, Is.True);
            Assert.That(c2.Hit, Is.False, "stepping out avoids it");
            Assert.That(_health.Current, Is.EqualTo(health));
        }

        // ---------------- Grave Rain ----------------

        /// <summary>The circles of the Grave Rain being cast, from its first.</summary>
        static List<EnemyCaster.Circle> RainOf(EnemyCaster c, int first) => c.Circles.Skip(first).ToList();

        [UnityTest]
        public IEnumerator GraveRain_TheThirdCast_PlacesThree_OnTime_WhereSheStands_AndStandingStillTakesThree()
        {
            EnemyCaster caster = Spawn();
            CasterValues v = caster.Tuning;
            EnemyAttackDefinition rain = CasterTactics.Find(v.Enemy, CasterValues.GraveRain);
            yield return Until(() => Casting(caster, CasterValues.GraveRain), 15f);
            Assert.That(Casting(caster, CasterValues.GraveRain), Is.True, "a Grave Rain");
            Assert.That(caster.CastsBegun, Is.EqualTo(3), "its third cast, after two Bone Spikes");
            Assert.That(caster.Circles.Take(2).All(x => x.Attack.Name == CasterValues.BoneSpikes), Is.True);
            int first = caster.Circles.Count - 1;
            float start = caster.Circles[first].PlacedAt;
            int health = _health.Current;
            var seen = new List<float> { Time.time };
            yield return Until(() =>
            {
                if (caster.Circles.Count - first > seen.Count) seen.Add(Time.time);
                return caster.Circles.Count - first >= v.RainCircles;
            }, 3f);
            List<EnemyCaster.Circle> circles = RainOf(caster, first);
            Assert.That(circles.Count, Is.EqualTo(3), "three circles");
            for (int i = 0; i < 3; i++)
            {
                EnemyCaster.Circle c = circles[i];
                string what = "circle " + (i + 1);
                Assert.That(c.Number, Is.EqualTo(i + 1), what);
                Assert.That(c.Attack, Is.SameAs(rain), what);
                Assert.That(c.IsBlue, Is.True, what + ": blue, counterable");
                Assert.That(c.Radius, Is.EqualTo(rain.Radius), what);
                Assert.That(c.PlacedAt - start, Is.EqualTo(CasterRules.RainPlacedAt(i, v)).Within(1e-4f), what + " placed on time");
                Assert.That(seen[i] - c.PlacedAt, Is.InRange(-1e-3f, 0.05f), what + " shown on time");
                Assert.That(c.BurstAt - start, Is.EqualTo(CasterRules.RainBurstAt(i, v)).Within(1e-4f), what + " bursts on time");
                Assert.That(Flat(c.Center, _player.Position), Is.LessThan(0.05f), what + ": she stands still, so where she stands");
            }
            yield return Until(() => circles.All(c => c.HasBurst), 3f);
            Assert.That(circles.All(c => c.HasBurst && c.Hit), Is.True, "standing still takes all three");
            Assert.That(health - _health.Current, Is.EqualTo(3 * rain.Damage));
            Debug.Log(string.Format(s_Inv, "CASTERRAIN placed at {0}; burst at {1} (s from its start)",
                string.Join(", ", circles.Select(c => (c.PlacedAt - start).ToString("0.###", s_Inv))),
                string.Join(", ", circles.Select(c => (c.BurstAt - start).ToString("0.###", s_Inv)))));
        }

        [UnityTest]
        public IEnumerator GraveRain_ATurnAfterEachCircle_AvoidsTheSecondAndTheThird()
        {
            EnemyCaster caster = Spawn(v => v.BigCastEvery = 1); // every cast a Grave Rain
            CasterValues values = caster.Tuning;
            // Across the line to the caster, then away from it, then back across: the way with room first.
            Vector3 toCaster = Flat(caster.transform.position - _player.Position).normalized;
            Vector3 a = Vector3.Cross(Vector3.up, toCaster);
            if (Room(_player.Position, a, 10f, out _) < Room(_player.Position, -a, 10f, out _)) a = -a;
            // Room enough for the circles led 0.8 s of her run ahead (work order 034): about 8 m each way.
            Vector3 b = Room(_player.Position, -toCaster, 10f, out _) >= 9f ? -toCaster : toCaster;
            Debug.Log(string.Format(s_Inv, "CASTERTURNROOM across {0:0.0} m, away {1:0.0} m", Room(_player.Position, a, 12f, out _), Room(_player.Position, -toCaster, 12f, out _)));
            yield return Until(() => Casting(caster, CasterValues.GraveRain), 4f);
            int first = caster.Circles.Count - 1;
            int health = _health.Current;
            var expected = new Vector3[3];
            _player.Steer(a);
            for (int n = 2; n <= 3; n++)
            {
                int want = n;
                yield return Until(() => caster.Circles.Count - first >= want, 2f);
                Assert.That(caster.Circles.Count - first, Is.EqualTo(n), "circle " + n + " placed");
                Vector3 vel = Flat(_player.Velocity);
                GroundPoint lead = CasterRules.Lead(new GroundPoint(vel.x, vel.z), values); // work order 034: her velocity times 0.8 s
                expected[n - 1] = Flat(_player.Position) + new Vector3(lead.X, 0f, lead.Z);
                yield return For(0.1f);
                _player.Steer(n == 2 ? b : -a);
            }
            List<EnemyCaster.Circle> circles = RainOf(caster, first);
            yield return Until(() => circles.All(c => c.HasBurst), 3f);
            _player.Steer(Vector3.zero);
            for (int i = 1; i < 3; i++)
                Assert.That(Flat(circles[i].Center, expected[i]), Is.LessThan(0.3f), "circle " + (i + 1) + " led along the way she was moving");
            Assert.That(circles.All(c => c.HasBurst), Is.True);
            Debug.Log("CASTERTURN hits " + string.Join(", ", circles.Select(c => c.Hit)) + "; her speed " + _player.SpeedLimit);
            Assert.That(circles[1].Hit, Is.False, "the turn avoids the second");
            Assert.That(circles[2].Hit, Is.False, "and the third");
            Assert.That(_health.Current, Is.EqualTo(health - (circles[0].Hit ? circles[0].Attack.Damage : 0)));
        }

        [UnityTest]
        public IEnumerator GraveRain_StoppingShotFromItsFront_InTheFirstWindup_DropsIt_AndWhatIsOnTheGroundFades()
        {
            _folder = Path.Combine(Path.GetTempPath(), "CasterTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            int stop = Slot("Stopping Shot");
            EnemyCaster caster = Spawn(v => v.BigCastEvery = 1);
            StandAt(caster, 8f); // within Stopping Shot's reach
            yield return Until(() => Casting(caster, CasterValues.GraveRain), 4f);
            int first = caster.Circles.Count - 1;
            yield return Until(() => caster.Circles.Count - first >= 2, 2f);
            Assume.That(caster.State, Is.EqualTo(EnemyState.Windup), "still in the first circle's wind-up");
            int health = _health.Current;
            Assert.That(_player.PressSkill(stop, caster.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => caster.Countered > 0 || caster.State != EnemyState.Windup, 2f);
            Assert.That(caster.Countered, Is.EqualTo(1), "countered from its front");
            Assert.That(caster.IsDazed, Is.True);
            List<EnemyCaster.Circle> circles = RainOf(caster, first);
            Assert.That(circles.Count, Is.EqualTo(2), "two were on the ground");
            Assert.That(circles.All(c => c.Faded && !c.HasBurst), Is.True, "they fade");
            // Past the time of its last burst, and still dazed (the counter's daze outlasts it): nothing more of it.
            float start = circles[0].PlacedAt;
            yield return For(start + CasterRules.RainLastBurst(caster.Tuning) + 0.2f - Time.time);
            Assume.That(caster.IsDazed, Is.True, "still dazed, so no new cast has begun");
            Assert.That(RainOf(caster, first).Count, Is.EqualTo(2), "nothing more is placed");
            Assert.That(circles.All(c => !c.HasBurst && !c.IsShown), Is.True, "none bursts, and they are gone");
            Assert.That(_health.Current, Is.EqualTo(health), "she stood in them and took nothing");

            // The recorder heard it, with the caster's own words.
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            bool Has(string type, string name, int count) => events.Count(e => e.Type == type && e.Name == name && e.Enemy == "caster" && e.EnemyId == caster.Id) == count;
            Assert.That(Has(SessionEventTypes.EnemyAttack, CasterValues.GraveRain, 1), "the cast");
            Assert.That(events.Count(e => e.Type == SessionEventTypes.CasterCircle && e.Result == SessionWords.Placed && e.EnemyId == caster.Id), Is.EqualTo(2), "two circles placed");
            Assert.That(Has(SessionEventTypes.EnemyCountered, CasterValues.GraveRain, 1), "the counter");
            Assert.That(Has(SessionEventTypes.CasterCircleFaded, CasterValues.GraveRain, 2), "two faded");
            Assert.That(events.Any(e => e.Type == SessionEventTypes.EnemyStrike && e.EnemyId == caster.Id), Is.False, "no burst");
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Enemies by kind, whole session", summary);
            StringAssert.Contains("caster", summary);
        }

        [UnityTest]
        public IEnumerator GraveRain_StoppingShotFromBehind_DoesNotCounter_AndAllThreeBurst()
        {
            int stop = Slot("Stopping Shot");
            EnemyCaster caster = Spawn(v => v.BigCastEvery = 1);
            StandAt(caster, 8f);
            yield return Until(() => Casting(caster, CasterValues.GraveRain), 4f);
            int first = caster.Circles.Count - 1;
            _player.Teleport(Flat(caster.transform.position) - caster.transform.forward * 5f);
            yield return null;
            Assume.That(caster.State, Is.EqualTo(EnemyState.Windup));
            int hits = caster.Body.HitsTaken;
            Assert.That(_player.PressSkill(stop, caster.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => caster.Body.HitsTaken > hits, 2f);
            Assert.That(caster.Body.HitsTaken, Is.GreaterThan(hits), "the shot hit it");
            Assert.That(caster.Countered, Is.EqualTo(0), "not from behind");
            Assert.That(caster.IsDazed, Is.False);
            yield return Until(() => RainOf(caster, first).Count >= 3 && RainOf(caster, first).All(c => c.HasBurst), 3f);
            List<EnemyCaster.Circle> circles = RainOf(caster, first);
            Assert.That(circles.Count, Is.EqualTo(3), "the rain goes on");
            Assert.That(circles.All(c => c.HasBurst && !c.Faded), Is.True);
        }

        // ---------------- The ground ----------------

        [UnityTest]
        public IEnumerator ACircleOffTheWalkableGround_IsNotPlaced()
        {
            EnemyCaster caster = Spawn(v => v.BigCastEvery = 1);
            CasterValues values = caster.Tuning;
            Assert.That(caster.OnWalkableGround(_player.Position), Is.True, "where she stands");
            Vector3 dir = MostRoom(_player.Position, 40f, out float room);
            Room(_player.Position, dir, 40f, out Vector3 edge);
            Assume.That(room, Is.GreaterThan(8f).And.LessThan(40f), "an edge of the floor to run at");
            Assert.That(caster.OnWalkableGround(edge + dir * 1.2f), Is.False, "past the edge of the floor");

            yield return Until(() => Casting(caster, CasterValues.GraveRain), 4f);
            int first = caster.Circles.Count - 1;
            float start = caster.Circles[first].PlacedAt;
            int off = caster.CirclesOffGround;
            // Her speed, measured; then she runs at the edge so that the second circle's place, led ahead of her (her velocity
            // times 0.8 s since work order 034), is past it.
            _player.Teleport(edge - dir * 6f);
            _player.Steer(dir);
            yield return For(0.06f);
            float speed = Flat(_player.Velocity).magnitude;
            float left = start + CasterRules.RainPlacedAt(1, values) - Time.time;
            _player.Teleport(edge - dir * (speed * left + 0.6f));
            _player.Steer(dir);
            yield return Until(() => caster.CirclesOffGround > off || caster.Circles.Count - first >= 2 || Time.time > start + 0.6f, 2f);
            _player.Steer(Vector3.zero);
            Debug.Log(string.Format(s_Inv, "CASTERGROUND her speed {0:0.0} m/s, {1:0.00} m from the edge at the second circle; off the ground {2}",
                speed, Flat(_player.Position, edge), caster.CirclesOffGround - off));
            Assert.That(caster.CirclesOffGround, Is.EqualTo(off + 1), "the second circle's place is off the ground");
            Assert.That(RainOf(caster, first).All(c => c.Number != 2), Is.True, "and it was not placed");
        }

        // ---------------- Death ----------------

        [UnityTest]
        public IEnumerator ADeadCastersPlacedSpike_StillBursts()
        {
            EnemyCaster caster = Spawn();
            yield return Until(() => Casting(caster, CasterValues.BoneSpikes), 4f);
            EnemyCaster.Circle c = caster.Circles.Single();
            int health = _health.Current;
            caster.Body.TakeHit(caster.Body.MaxHealth);
            yield return null;
            Assert.That(caster.IsDead, Is.True);
            Assert.That(caster.GetComponent<NavMeshAgent>().enabled, Is.False, "dead, no obstacle to anyone (work order 030's StayDead, work order 034)");
            Assert.That(c.IsShown, Is.True, "its circle stays");
            yield return Until(() => c.HasBurst, 2f);
            Assert.That(c.HasBurst && c.Hit, Is.True, "and goes off on her");
            Assert.That(health - _health.Current, Is.EqualTo(c.Attack.Damage));
        }

        [UnityTest]
        public IEnumerator ADeadCaster_PlacesNoMoreOfItsGraveRain()
        {
            EnemyCaster caster = Spawn(v => v.BigCastEvery = 1);
            yield return Until(() => Casting(caster, CasterValues.GraveRain), 4f);
            int first = caster.Circles.Count - 1;
            caster.Body.TakeHit(caster.Body.MaxHealth);
            yield return For(CasterRules.RainLastBurst(caster.Tuning) + 0.3f);
            Assert.That(caster.IsDead, Is.True);
            List<EnemyCaster.Circle> circles = RainOf(caster, first);
            Assert.That(circles.Count, Is.EqualTo(1), "the second and third were never placed");
            Assert.That(circles[0].HasBurst, Is.True, "the first, placed, went off");
            Assert.That(caster.CirclesOffGround, Is.EqualTo(0));
        }

        // ---------------- Work order 034: the turns, the floor's damage, the sounds and effects ----------------

        /// <summary>Two ranged wind-ups that are not enemies hold every ranged turn for this long.</summary>
        static void FillRangedTurns(float seconds)
        {
            Enemy.Turns.Clear();
            Enemy.Turns.Values = new TurnValues();
            Enemy.Turns.Began(-1001, TurnSort.Ranged, Time.time, seconds);
            Enemy.Turns.Began(-1002, TurnSort.Ranged, Time.time, seconds);
        }

        [UnityTest]
        public IEnumerator AFloorsCaster_WaitsItsTurnAsARangedEnemy_AndAnEliteCasterDoesNot()
        {
            EnemyCaster floor = Spawn();
            floor.TakesTurns = true; // as a generated floor's (GeneratedLevel.PlaceEnemies)
            Assert.That(floor.AttackSort, Is.EqualTo(TurnSort.Ranged));
            try
            {
                FillRangedTurns(4f);
                yield return Until(() => Enemy.Turns.IsWaiting(floor.Id, Time.time), 4f);
                Assert.That(Enemy.Turns.IsWaiting(floor.Id, Time.time), Is.True, "it asks for a turn");
                yield return For(1.5f);
                Assert.That(floor.CastsBegun, Is.EqualTo(0), "and waits while two ranged wind up");
                Assert.That(floor.State, Is.Not.EqualTo(EnemyState.Windup));
                Assert.That(floor.NextCast, Is.EqualTo(CasterValues.BoneSpikes), "asking counts no cast: its order holds");
                yield return Until(() => floor.State == EnemyState.Windup, 6f);
                Assert.That(floor.State, Is.EqualTo(EnemyState.Windup), "its turn came");
                Assert.That(floor.CastsBegun, Is.EqualTo(1));
                Assert.That(floor.Attack.Name, Is.EqualTo(CasterValues.BoneSpikes), "its first cast, the order untouched by the questions");
                floor.Body.TakeHit(floor.Body.MaxHealth);

                // An elite caster: of no sort for the turns, never held back.
                EnemyCaster elite = Spawn();
                elite.TakesTurns = true;
                EnemyFactory.MakeElite(elite, 2000, 1.25f, 4f / 3f, 0.5f);
                Assert.That(elite.IsElite, Is.True);
                FillRangedTurns(6f);
                yield return Until(() => elite.State == EnemyState.Windup, 4f);
                Assert.That(elite.State, Is.EqualTo(EnemyState.Windup), "an elite caster takes no turn");
                Assert.That(Enemy.Turns.IsWaiting(elite.Id, Time.time), Is.False);
            }
            finally
            {
                Enemy.Turns.Clear();
            }
        }

        [UnityTest]
        public IEnumerator AFloor2sCastersCircle_HurtsByTheFloorsScale_AndThePracticeRoomsByAllOfIt()
        {
            // The practice room's caster: unscaled.
            EnemyCaster practice = Spawn();
            Assert.That(practice.DamageScale, Is.EqualTo(1f));
            int got = -1;
            practice.Reported += r => { if (r.Kind == EnemyReportKind.Struck && r.Outcome == StrikeOutcome.Landed && got < 0) got = r.Damage; };
            yield return Until(() => got >= 0, 6f);
            EnemyAttackDefinition spikes = CasterTactics.Find(practice.Tuning.Enemy, CasterValues.BoneSpikes);
            Assert.That(got, Is.EqualTo(spikes.Damage), "the practice room's: all of its damage");
            _levels.ClearSpawned();

            // A floor 2 with the casters on: its casters are made with the floor's scale, and a circle of one hurts by it.
            var values = new LevelValues { Casters = true };
            _levels.Values = values;
            ulong seed = 0;
            while (!LevelGenerator.Generate(seed, 2, values).Rooms.Any(r => r.EnemyGroups.Any(g => g.Kind == EnemyKind.Caster && !g.Elite))) seed++;
            GeneratedLevel level = _levels.Build(seed, 2);
            yield return null;
            EnemyCaster caster = level.Enemies.OfType<EnemyCaster>().First(e => !e.IsElite);
            float scale = values.DamageScaleOf(2);
            Assert.That(scale, Is.EqualTo(0.6f)); // work order 046: floor 2 deals 0.6 (0.8 before)
            Assert.That(caster.DamageScale, Is.EqualTo(scale).Within(1e-5f), "the floor's scale");
            Assert.That(caster.TakesTurns, Is.True, "a floor's caster takes turns");
            Assert.That(caster.Body.MaxHealth, Is.EqualTo(values.CasterHealth));
            got = -1;
            caster.Reported += r => { if (r.Kind == EnemyReportKind.Struck && r.Outcome == StrikeOutcome.Landed && got < 0) got = r.Damage; };
            Vector3 toMiddle = Flat(level.Middle(level.Layout.Rooms.First(r => level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) && l.Contains(caster))) - caster.transform.position);
            Vector3 dir = toMiddle.sqrMagnitude > 1f ? toMiddle.normalized : -caster.transform.forward;
            _player.Teleport(Flat(caster.transform.position) + dir * 8f);
            yield return Until(() => got >= 0, 15f);
            Debug.Log(string.Format(s_Inv, "CASTERFLOOR2 seed {0}: a circle of a floor-2 caster hurt her by {1} ({2} times {3})", seed, got, scale, spikes.Damage));
            Assert.That(got, Is.EqualTo(Mathf.RoundToInt(spikes.Damage * scale)), "a floor-2 caster's Bone Spikes: 0.6 of 25 (work order 046)");
            _levels.BackToPracticeRoom();
        }

        [UnityTest]
        public IEnumerator ItsCasts_AskForTheWindupSoundAndEffect_ItsBursts_TheStrikeSoundAndTheCircleStrikeAtTheCircle()
        {
            PlaceholderSounds sounds = PlaceholderSounds.Ensure();
            CombatEffects fx = CombatEffects.Ensure();
            _made.Add(sounds.gameObject);
            _made.Add(fx.gameObject);
            var heard = new List<PlaceholderSoundRequest>();
            var drawn = new List<(FxPiece p, int frame)>();
            void Hear(PlaceholderSoundRequest r) => heard.Add(r);
            void Draw(CombatFx f) => drawn.Add((f.Piece, Time.frameCount));
            PlaceholderSounds.Requested += Hear;
            CombatEffects.Spawned += Draw;
            try
            {
                yield return null;
                EnemyCaster caster = Spawn(v => v.BigCastEvery = 2); // Bone Spikes, then a Grave Rain
                CombatFxValues fv = fx.Values;

                foreach (string cast in new[] { CasterValues.BoneSpikes, CasterValues.GraveRain })
                {
                    bool blue = cast == CasterValues.GraveRain;
                    int windupAt = -1;
                    yield return Until(() => { if (Casting(caster, cast)) windupAt = Time.frameCount; return windupAt >= 0; }, 10f);
                    Assert.That(windupAt, Is.GreaterThan(0), cast + " begun");
                    yield return null; // the sounds go at the frame's end
                    Assert.That(heard.Count(r => r.Frame == windupAt && r.Sound == (blue ? PlaceholderSound.WindupBlue : PlaceholderSound.WindupRed)), Is.EqualTo(1),
                                cast + ": the " + (blue ? "blue" : "red") + " wind-up sound; heard " + string.Join(", ", heard.Select(h => h.Sound + "@" + h.Frame)));
                    List<FxPiece> windups = drawn.Where(d => d.frame == windupAt && d.p.Moment == FxMoment.WindupStarted).Select(d => d.p).ToList();
                    Assert.That(windups.Count, Is.EqualTo(1), cast + ": the wind-up effect");
                    Assert.That(Flat(windups[0].At, caster.transform.position), Is.LessThan(0.05f), "on the caster");
                    Assert.That(windups[0].Rim, Is.EqualTo(blue ? fv.CounterRim : fv.StrikeRim), "in its telegraph's colour");
                    Assert.That(windups[0].Size, Is.EqualTo(fv.WindupSize * new FxSizes().KindScale(EnemyKind.Shooter)).Within(1e-4f), "a shooter's size");

                    EnemyCaster.Circle circle = caster.Circles.Last();
                    int burstAt = -1;
                    yield return Until(() => { if (circle.HasBurst) burstAt = Time.frameCount; return burstAt >= 0; }, 4f);
                    Assert.That(burstAt, Is.GreaterThan(0), cast + ": its first circle burst");
                    yield return null;
                    Assert.That(heard.Count(r => r.Frame == burstAt && r.Sound == PlaceholderSound.Strike), Is.EqualTo(1), cast + ": the strike sound as it bursts");
                    List<FxPiece> rings = drawn.Where(d => d.frame == burstAt && d.p.Moment == FxMoment.EnemyStruck && d.p.Shape == FxShape.Ring).Select(d => d.p).ToList();
                    List<FxPiece> stars = drawn.Where(d => d.frame == burstAt && d.p.Moment == FxMoment.EnemyStruck && d.p.Shape == FxShape.Spark).Select(d => d.p).ToList();
                    Assert.That(rings.Count, Is.EqualTo(1), cast + ": the circle strike's ring");
                    Assert.That(stars.Count, Is.EqualTo(1), cast + ": and its star");
                    Assert.That(Flat(rings[0].At, circle.Center), Is.LessThan(0.05f), "at the circle, not at the caster");
                    Assert.That(Flat(stars[0].At, circle.Center), Is.LessThan(0.05f));
                    Assert.That(Flat(circle.Center, caster.transform.position), Is.GreaterThan(3f), "which is away from the caster");
                    Assert.That(rings[0].Size, Is.EqualTo(circle.Radius).Within(1e-4f), "to the circle's edge");
                    Assert.That(rings[0].Rim, Is.EqualTo(blue ? fv.CounterRim : fv.StrikeRim), "red or blue as its telegraph");
                    Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name == "StrikeFx"), Is.EqualTo(0), "no flat stand-in");
                }
            }
            finally
            {
                PlaceholderSounds.Requested -= Hear;
                CombatEffects.Spawned -= Draw;
            }
        }

        // ---------------- The practice room and the floors ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoomsKey_SpawnsACaster()
        {
            // Work order 034: F4 went to main's focus choice (today's gauge); the caster's key is C, which nothing else uses.
            var others = new List<Key>
            {
                GeneratedLevels.NewLevelKey, GeneratedLevels.SameLevelKey, GeneratedLevels.PracticeRoomKey, GeneratedLevels.BossKey,
                GeneratedLevels.SwarmersKey, GeneratedLevels.ShooterKey, GeneratedLevels.GuardKey, GeneratedLevels.ClearSpawnsKey, RunHud.DevTextKey,
                GeneratedLevels.HairTriggerKey, GeneratedLevels.SteadyHandKey, GeneratedLevels.TodaysFocusKey, GeneratedLevels.RedrawKey,
                RunHud.PauseKey, RunHud.BetweenRunsStartKey, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            };
            InputBindings bindings = ScriptableObject.CreateInstance<InputBindings>(); // its start values
            _made.Add(bindings);
            others.AddRange(new[] { bindings.MoveUp, bindings.MoveDown, bindings.MoveLeft, bindings.MoveRight, bindings.Dash, bindings.Focus, bindings.Interact, bindings.TakeCover });
            others.AddRange(bindings.Skills.Where(b => b.Source == BindingSource.Key).Select(b => b.Key));
            InputBindings b = _player.GetComponent<PlayerInputDriver>().ActiveBindings; // the asset in use, as Kristian may have edited it
            others.AddRange(new[] { b.MoveUp, b.MoveDown, b.MoveLeft, b.MoveRight, b.Dash, b.Focus, b.Interact, b.TakeCover });
            if (b.Skills != null) others.AddRange(b.Skills.Where(x => x.Source == BindingSource.Key).Select(x => x.Key));
            Assert.That(GeneratedLevels.CasterKey, Is.EqualTo(Key.C));
            Assert.That(others, Has.No.Member(GeneratedLevels.CasterKey), "a free key");
            EnemyCaster caster = Spawn();
            yield return null;
            Assert.That(Flat(caster.transform.position, Stand), Is.EqualTo(GeneratedLevels.SpawnDistance).Within(0.5f), "12 m from her");
            Assert.That(caster.ComesBack, Is.False);
            Assert.That(caster.Body.MaxHealth, Is.EqualTo(caster.Tuning.Enemy.Health), "the practice room keeps its own health");
            Assert.That(caster.Body.Radius, Is.EqualTo(caster.Tuning.BodyRadius));
            Assert.That(_levels.Spawned, Has.Member(caster));
            _levels.ClearSpawned();
            yield return null;
            Assert.That(caster == null, Is.True, "F12 removes it");
        }

        [UnityTest]
        public IEnumerator AFloor3_HoldsCasters_WithinTheCaps()
        {
            _practiceEnemy.enabled = true;
            var values = new LevelValues { Casters = true }; // work order 034: they were off in the floors by default (on since work order 054)
            _levels.Values = values;
            ulong seed = 0;
            while (!LevelGenerator.Generate(seed, 3, values).Rooms.Any(r => r.EnemyGroups.Count(g => g.Kind == EnemyKind.Caster && !g.Elite) >= 1)) seed++;
            GeneratedLevel level = _levels.Build(seed, 3);
            yield return null;
            int casters = 0;
            foreach (LevelRoom r in level.Layout.Rooms)
            {
                List<Enemy> list = level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) ? l : new List<Enemy>();
                List<EnemyCaster> here = list.OfType<EnemyCaster>().ToList();
                Assert.That(here.Count(e => !e.IsElite), Is.LessThanOrEqualTo(values.CastersMost(r.Kind, 3)), "room " + r.Letter + r.Order);
                Assert.That(here.Count, Is.EqualTo(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Caster).Sum(g => g.Count)), "as the description says");
                foreach (EnemyCaster e in here)
                    Assert.That(e.Body.MaxHealth, Is.EqualTo(e.IsElite ? values.EliteHealthOf(EnemyKind.Caster) : values.CasterHealth), e.name);
                casters += here.Count;
            }
            Debug.Log("CASTERFLOOR seed " + seed + " floor 3 holds " + casters + " casters\n" + LevelMap.Draw(level.Layout));
            Assert.That(casters, Is.GreaterThan(0));
            _levels.BackToPracticeRoom();
        }
    }
}
