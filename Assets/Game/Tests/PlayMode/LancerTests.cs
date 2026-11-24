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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The lancer (work order 108, 2026-11-24, "Lansereren (ny fiende)": "stormer i rett linje langveisfra, varslet på kroppen, og
    /// blir ør når den treffer en vegg eller rekvisitt"), in the ControlRoom scene's practice room, fought through the character's
    /// intents: its practice-room key spawns one in the skeleton warrior's model, larger and tinted, with its lance; it charges her
    /// standing still and hits her, once, by the value, the wind-up read on its body (no telegraph on the floor, its lance lowered
    /// and its body leaned); she stepping aside after the wind-up is passed by, its line not turned; a low prop between them stops
    /// it short of the prop and dazes it (its bar's daze, its extra damage), and the recorder and the summary say so; Stopping Shot
    /// from its front during the wind-up counters it; nothing is allocated by its frame in the wind-up and the charge; and asked
    /// before it has woken it answers. Every number is read from its values. Nobody has seen or played it: these are numbers.
    ///
    /// The practice brute is switched off (it stays in the scene as the template the factory copies). She stands on the practice
    /// floor where it has the most room in a straight line; the lancer 13 m from her along it, in its band.
    /// </summary>
    public class LancerTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);
        const float Apart = 13f; // m from her the lancer stands: inside its band of 10 to 17 m

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        Transform _root;
        Vector3 _open;
        Vector3 _her;
        string _folder;
        PlaySessionRecorder _recorder;
        int _made;
        readonly List<Object> _objects = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
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
            _open = MostRoom(Stand, 30f, out float room);
            Assume.That(room, Is.GreaterThanOrEqualTo(Apart + 4f), "a stretch of floor to fight on");
            _her = Stand + _open * 2f;
            _player.Teleport(_her);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            foreach (Object o in _objects) if (o != null) Object.Destroy(o);
            _objects.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.Steer(Vector3.zero);
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

        static float Room(Vector3 from, Vector3 dir, float max)
        {
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)) return 0f;
            if (!NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas)) return max;
            return Flat(hit.position, start.position);
        }

        static Vector3 MostRoom(Vector3 from, float max, out float room)
        {
            Vector3 best = Vector3.forward;
            room = -1f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 dir = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                float r = Room(from, dir, max);
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        void Record()
        {
            _folder = Path.Combine(Path.GetTempPath(), "LancerTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
        }

        /// <summary>A lancer made where it will stand (its home), 13 m from her along the open way, tuned before its first frame.</summary>
        EnemyLancer Lancer(Action<LancerValues> tune = null)
        {
            if (_root == null) _root = new GameObject("LancerTestSpawns").transform;
            Enemy e = EnemyFactory.Make(EnemyKind.Lancer, Flat(_her + _open * Apart), _root, _practiceEnemy, "LancerTest_" + (++_made));
            Assert.That(e, Is.InstanceOf<EnemyLancer>());
            var l = (EnemyLancer)e;
            tune?.Invoke(l.Tuning);
            return l;
        }

        static EnemyAttackDefinition Charge(EnemyLancer l) => l.ChargeAttack;

        IEnumerator Windup(EnemyLancer l, float seconds = 5f)
        {
            yield return Until(() => l.State == EnemyState.Windup && l.Attack != null && l.Attack.Name == LancerValues.Charge, seconds);
            Assert.That(l.State, Is.EqualTo(EnemyState.Windup), "it winds up its charge at her 13 m away");
        }

        // ---------------- The practice room and its body ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoomsKey_SpawnsALancer_InTheSkeletonWarriorsModel_LargerAndTinted_WithALance()
        {
            Assert.That(GeneratedLevels.LancerKey, Is.EqualTo(UnityEngine.InputSystem.Key.K));
            Assert.That(DeveloperKeys.IsDeveloperKey(GeneratedLevels.LancerKey), Is.True, "a developer's key (work order 066)");
            InputBindings active = _player.GetComponent<PlayerInputDriver>().ActiveBindings;
            var bound = new List<UnityEngine.InputSystem.Key> { active.MoveUp, active.MoveDown, active.MoveLeft, active.MoveRight, active.Dash, active.Focus, active.Interact, active.TakeCover, active.Sprint, active.Sheet };
            if (active.Skills != null) bound.AddRange(active.Skills.Where(x => x.Source == BindingSource.Key).Select(x => x.Key));
            Assert.That(bound, Has.No.Member(GeneratedLevels.LancerKey), "a free key");

            _player.Teleport(Stand);
            yield return null;
            List<Enemy> made = _levels.SpawnLancer();
            Assert.That(made.Count, Is.EqualTo(1), "one lancer");
            var l = made[0] as EnemyLancer;
            Assert.That(l, Is.Not.Null, "of its own kind");
            yield return null;
            Assert.That(l.Kind, Is.EqualTo(EnemyKind.Lancer));
            Assert.That(l.Body.MaxHealth, Is.EqualTo(l.Tuning.Enemy.Health), "the practice room's health");
            Assert.That(l.Stagger, Is.Not.Null, "a stagger bar");
            Assert.That(l.ReadOnBody, Is.True, "read on its body");
            Assert.That(l.Look, Is.Not.Null, "it wears a model");
            Assert.That(l.Look.Spec, Is.SameAs(EnemyModelSpec.Lancer));
            Assert.That(l.Look.Model.name, Is.EqualTo(EnemyModelSpec.Brute.Name), "the skeleton warrior's");
            Assert.That(l.transform.lossyScale.x, Is.EqualTo(l.Tuning.Shown).Within(1e-4f), "shown larger");
            Assert.That(l.BodyColor, Is.EqualTo(EnemyFactory.LancerTint), "tinted");
            Color painted = l.BodyRenderer.sharedMaterial.color, tint = EnemyFactory.LancerTint;
            Assert.That(Mathf.Max(Mathf.Abs(painted.r - tint.r), Mathf.Abs(painted.g - tint.g), Mathf.Abs(painted.b - tint.b)), Is.LessThan(1e-3f), "its body painted in it");
            Assert.That(l.Lance, Is.Not.Null, "its lance");
            Renderer[] lance = l.Lance.GetComponentsInChildren<Renderer>();
            Assert.That(lance.Length, Is.EqualTo(2), "a shaft and a point");
            Assert.That(l.Lance.GetComponentsInChildren<Collider>().Length, Is.Zero, "no collider");
            Bounds b = lance[0].bounds;
            foreach (Renderer r in lance) b.Encapsulate(r.bounds);
            Assert.That(b.size.magnitude, Is.EqualTo(l.Tuning.LanceLength * l.Tuning.Shown).Within(0.3f * l.Tuning.Shown), "its length, shown");
            Renderer sword = l.Look.Renderers.FirstOrDefault(r => r != null && r.name.IndexOf("Sword", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sword != null) Assert.That(sword.enabled, Is.False, "its sword hidden");
            Debug.Log(string.Format(s_Inv, "LANCERLOOK model {0} shown {1:0.00} m high, tint {2}, lance {3:0.00} m, sword {4}",
                l.Look.Model.name, l.ModelHeight, EnemyFactory.LancerTint, b.size.magnitude, sword != null ? "hidden (" + sword.name + ")" : "not found"));
            _levels.ClearSpawned();
            yield return null;
            Assert.That(l == null, Is.True, "F12 removes it");
        }

        // ---------------- The charge: it hits her standing ----------------

        [UnityTest]
        public IEnumerator ItCharges_HerStandingStill_AndHitsHerOnce_ReadOnItsBody()
        {
            Record();
            EnemyLancer l = Lancer();
            EnemyAttackDefinition c = Charge(l);
            yield return Windup(l);
            Assert.That(l.IsTelegraphing, Is.False, "nothing on the floor");
            float began = Time.time;
            yield return Until(() => Time.time - began > c.Windup * l.Tuning.LowerShare + 0.05f || l.IsCharging, 3f);
            Assert.That(l.Lowered, Is.EqualTo(1f).Within(1e-4f), "its lance lowered and its body leaned through the wind-up");
            Assert.That(l.LanceAngle, Is.EqualTo(l.Tuning.LanceDownDegrees).Within(0.5f));
            Assert.That(Mathf.DeltaAngle(0f, l.Lean.localEulerAngles.x), Is.EqualTo(l.Tuning.LeanDegrees).Within(0.5f));
            int hp = _health.Current;
            yield return Until(() => l.IsCharging, 3f);
            Assert.That(l.IsCharging, Is.True, "the charge sets off");
            Assert.That(l.Charge.Speed, Is.EqualTo(c.Rush));
            yield return Until(() => !l.IsCharging, 3f);
            Assert.That(l.LastEnd, Is.EqualTo(LancerEnd.MetHer));
            Assert.That(hp - _health.Current, Is.EqualTo(c.Damage), "her damage, once (unscaled in the practice room)");
            Assert.That(Flat(l.transform.position, _her), Is.LessThan(c.HalfWidth + Placeholders.PlayerRadius + 0.6f), "it stopped at her");
            Assert.That(l.State, Is.EqualTo(EnemyState.Recover), "then it recovers");
            yield return For(0.3f);
            Assert.That(_health.Current, Is.EqualTo(hp - c.Damage), "once");
            List<SessionEvent> events = _recorder.Events.Where(e => e.Enemy == "lancer").ToList();
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyAttack && e.Name == LancerValues.Charge), Is.EqualTo(1));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == LancerValues.Charge && e.Result == SessionWords.Landed), Is.EqualTo(1));
            _levels.ClearSpawned();
        }

        // ---------------- She steps aside: it passes ----------------

        [UnityTest]
        public IEnumerator SheStepsAside_AfterTheWindup_ItPassesHer_OnTheLineItHad_AndRecovers()
        {
            EnemyLancer l = Lancer(t => t.Enemy.Attacks[0].Length = Apart + 1.5f); // it ends short of the floor's edge behind her
            EnemyAttackDefinition c = Charge(l);
            yield return Windup(l);
            yield return Until(() => l.IsCharging, 3f);
            Assert.That(l.IsCharging, Is.True);
            Vector3 from = l.transform.position;
            Vector3 dir = new Vector3(l.Charge.Direction.X, 0f, l.Charge.Direction.Z);
            Assert.That(Vector3.Angle(dir, -_open), Is.LessThan(2f), "fixed at her as the wind-up ended");
            Vector3 aside = _her + Vector3.Cross(Vector3.up, _open) * (c.HalfWidth + Placeholders.PlayerRadius + 1.5f);
            _player.Teleport(aside);
            _health.RestoreFull();
            int hp = _health.Current;
            yield return Until(() => !l.IsCharging, 3f);
            Assert.That(l.LastEnd, Is.EqualTo(LancerEnd.RanOut), "its whole length, nothing met");
            Assert.That(_health.Current, Is.EqualTo(hp), "beside the line: not hit");
            Vector3 run = Flat(l.transform.position - from);
            Assert.That(Vector3.Angle(run, dir), Is.LessThan(2f), "it did not turn to her");
            Assert.That(run.magnitude, Is.EqualTo(c.Length).Within(0.3f));
            Assert.That(l.IsDazed, Is.False, "no daze: it recovers");
            Assert.That(l.State, Is.EqualTo(EnemyState.Recover));
            _levels.ClearSpawned();
        }

        // ---------------- A low prop between them ----------------

        [UnityTest]
        public IEnumerator ALowPropBetweenThem_StopsIt_ShortOfIt_AndDazesIt_AndTheLogSaysCover()
        {
            Record();
            EnemyLancer l = Lancer();
            EnemyAttackDefinition c = Charge(l);
            // A low prop of the floor's kind (a collider and a shelter, no cover: she and the shooters see over it), 6 m in front of it.
            var prop = GameObject.CreatePrimitive(PrimitiveType.Cube);
            prop.name = "Prop_LancerTest_low";
            _objects.Add(prop);
            Vector3 at = Flat(l.transform.position) - _open * 6f;
            prop.transform.SetPositionAndRotation(at + Vector3.up * 0.4f, Quaternion.LookRotation(_open));
            prop.transform.localScale = new Vector3(1.4f, 0.8f, 0.6f);
            prop.AddComponent<Shelter>().LowProp = true;
            Physics.SyncTransforms();
            yield return Windup(l);
            int hp = _health.Current;
            yield return Until(() => l.Charges > 0 && !l.IsCharging, 4f);
            Assert.That(l.LastEnd, Is.EqualTo(LancerEnd.Crashed));
            Assert.That(l.LastStopper, Is.EqualTo(LancerStopper.LowProp));
            float reached = Vector3.Dot(Flat(l.transform.position) - Flat(_her + _open * Apart), -_open);
            Assert.That(reached, Is.LessThan(6f - 0.3f), "short of the prop");
            Assert.That(reached, Is.GreaterThan(6f - 0.3f - l.Tuning.StopShort - l.Tuning.StopProbeRadius - 1f), "at it");
            Assert.That(_health.Current, Is.EqualTo(hp), "behind it: not hit");
            Assert.That(l.IsDazed, Is.True, "dazed");
            Assert.That(l.Stagger.IsDazed(Time.time), Is.True, "by its bar, as a stagger");
            int dealt = l.Body.TakeHit(100);
            Assert.That(dealt, Is.EqualTo(Mathf.RoundToInt(100 * l.Tuning.Stagger.DazedDamageMultiplier)), "more damage while dazed");
            yield return For(l.Tuning.CrashDaze + 0.2f);
            Assert.That(l.IsDazed, Is.False, "for its daze, then up");
            List<SessionEvent> events = _recorder.Events.Where(e => e.Enemy == "lancer").ToList();
            SessionEvent strike = events.Single(e => e.Type == SessionEventTypes.EnemyStrike && e.Name == LancerValues.Charge);
            Assert.That(strike.Result, Is.EqualTo(SessionWords.StoppedByWall), "stopped by cover");
            Assert.That(strike.ExtraValue(SessionSummary.LancerCrashExtra), Is.EqualTo("low prop"));
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyDazed), Is.EqualTo(1));
            string summary = SessionSummary.Format(SessionSummary.Compute(_recorder.Events.ToList()));
            StringAssert.Contains("Lancers, whole session", summary);
            StringAssert.Contains("Crashes (dazed)", summary);
            _levels.ClearSpawned();
        }

        // ---------------- Stopping Shot ----------------

        [UnityTest]
        public IEnumerator StoppingShot_FromItsFront_DuringTheWindup_CountersIt()
        {
            Record();
            EnemyLancer l = Lancer();
            int stop = -1;
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == "Stopping Shot") stop = i;
            Assert.That(stop, Is.GreaterThanOrEqualTo(0), "the pool has Stopping Shot");
            _player.EquipSkill(stop);
            yield return Windup(l);
            // She stands in its line, in front of it, well inside the shot's range, and shoots it while it winds up.
            _player.Teleport(Flat(l.transform.position) - _open * 9f);
            _health.RestoreFull();
            int hp = _health.Current;
            Assert.That(_player.PressSkill(stop, l.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => l.Countered > 0 || l.IsCharging, 3f);
            Assert.That(l.Countered, Is.EqualTo(1));
            Assert.That(l.IsDazed, Is.True);
            Assert.That(l.IsCharging, Is.False, "the charge never starts");
            yield return For(1f);
            Assert.That(l.Charges, Is.Zero);
            Assert.That(_health.Current, Is.EqualTo(hp), "in its line, and not hit");
            Assert.That(_recorder.Events.Count(e => e.Enemy == "lancer" && e.Type == SessionEventTypes.EnemyCountered), Is.EqualTo(1), "the log has the counter");
            _player.EquipStartSkills();
            _levels.ClearSpawned();
        }

        // ---------------- The room lab ----------------

        [UnityTest, Timeout(300000)]
        public IEnumerator TheLabsPreset_StandsALancerInARoom_OnFloor4_AndItIsARoomOfAFloor()
        {
            int mix = Array.FindIndex(RoomLab.Mixes, m => m.Lancers > 0);
            Assert.That(mix, Is.GreaterThanOrEqualTo(0), "the preset");
            LabMix m = RoomLab.Mixes[mix];
            LabEntry nave = RoomLab.Entries(_levels.Values).First(e => e.Template != null && e.Kind == LabKind.Ordinary);
            ulong seed = 0;
            for (ulong s = 1; s <= 200 && seed == 0; s++)
                if (RoomLab.Build(nave, m, 0x1080000UL + s, _levels.Values).Rooms[1].EnemyGroups.Any(g => g.Kind == EnemyKind.Lancer)) seed = 0x1080000UL + s;
            Assert.That(seed, Is.Not.Zero, "a seed whose room holds its lancer");
            GeneratedLevel level = _levels.StartLab(nave, mix, seed);
            Assert.That(level, Is.Not.Null);
            yield return null;
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(4), "on floor 4");
            LevelRoom room = level.Layout.Rooms[1];
            Assert.That(RoomLab.Faults(level.Layout, m, _levels.Values), Is.Empty, "a room of a floor");
            List<Enemy> own = level.EnemiesByRoom[room.Order];
            EnemyLancer l = own.OfType<EnemyLancer>().SingleOrDefault();
            Assert.That(l, Is.Not.Null, "one lancer stands in it");
            Assert.That(l.Body.MaxHealth, Is.EqualTo(_levels.Values.LancerHealth), "a floor's health");
            Assert.That(l.DamageScale, Is.EqualTo(_levels.Values.DamageScaleOf(4)).Within(1e-5f), "and floor 4's damage scale");
            TestContext.WriteLine("LANCERLAB seed " + LevelSeed.ToText(seed) + ": " + string.Join(", ", own.GroupBy(e => e.LogWord).Select(g => g.Count() + " " + g.Key)));
            _levels.BackToPracticeRoom();
        }

        // ---------------- Nothing allocated a frame ----------------

        static byte[] s_junk;

        [UnityTest]
        public IEnumerator ItsFrame_InTheWindupAndTheCharge_AllocatesNothing()
        {
            EnemyLancer l = Lancer();
            yield return Windup(l);
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            l.FrameNow();
            Assert.That(() => { for (int i = 0; i < 300; i++) l.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of its wind-up allocates nothing");
            yield return Until(() => l.IsCharging, 3f);
            Assume.That(l.IsCharging, Is.True);
            l.FrameNow();
            Assert.That(() => { for (int i = 0; i < 300; i++) l.FrameNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of its charge allocates nothing");
            _levels.ClearSpawned();
        }

        // ---------------- Asked before it has woken ----------------

        [Test]
        public void ALancer_AskedBeforeItHasWoken_Answers()
        {
            var go = new GameObject("LancerBeforeAwake");
            _objects.Add(go);
            go.SetActive(false); // nothing on it wakes
            EnemyLancer l = go.AddComponent<EnemyLancer>();
            Assert.That(l.IsCharging, Is.False);
            Assert.That(l.Lowered, Is.EqualTo(0f));
            Assert.That(l.LanceAngle, Is.EqualTo(0f));
            Assert.That(l.Stagger, Is.Null);
            Assert.That(l.Lance, Is.Null);
            Assert.That(l.ChargeAttack, Is.Not.Null);
            Assert.That(l.LineClear, Is.False);
            Assert.That(l.IsDead, Is.True, "not yet a body: as every enemy before it wakes");
            Assert.That(l.LastEnd, Is.EqualTo(LancerEnd.None));
        }
    }
}
