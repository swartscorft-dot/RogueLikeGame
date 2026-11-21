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
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The bomber (work order 073, 2026-11-21, "Ny fiendetype"), in the ControlRoom scene's practice room, fought through the
    /// character's intents (she stands, steps away, dashes, shoots): its practice-room key spawns one, wearing the swarmer's
    /// model smaller and tinted; it runs at her and lights at the range, and stays; she standing is hurt once, by the value;
    /// she stepping out before the end is not; she dashing through at the end is not; shot dead among three swarmers it drops,
    /// lights its short fuse, and they die by its blast, and the recorder says so; two side by side, the second blows one
    /// fuse after the first; nothing is left behind and a frame of its fuse allocates nothing; its fuse and its blast are
    /// heard and drawn with what exists, and the engine's "Converting invalid MinMaxAABB" assert (work order 061) does not
    /// come; a floor 2 with them on makes them with the floor's health and damage scale. Every number is read from the values.
    ///
    /// The practice brute is switched off (it stays in the scene as the template the factory copies its telegraph from). She
    /// stands on the practice floor where it has the most room in a straight line, with health enough for every test.
    /// </summary>
    public class BomberTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        Transform _root;
        Vector3 _open;      // the way from Stand with the most walkable ground
        Vector3 _her;       // where she stands for a test: 6 m along it, with room behind her and in front
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
            Assume.That(room, Is.GreaterThanOrEqualTo(17f), "a stretch of floor to fight on");
            _her = Stand + _open * 6f;
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

        /// <summary>Metres of walkable ground from a point along a flat direction, up to <paramref name="max"/>.</summary>
        static float Room(Vector3 from, Vector3 dir, float max)
        {
            if (!NavMesh.SamplePosition(Flat(from), out NavMeshHit start, 2f, NavMesh.AllAreas)) return 0f;
            if (!NavMesh.Raycast(start.position, start.position + dir * max, out NavMeshHit hit, NavMesh.AllAreas)) return max;
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
                float r = Room(from, dir, max);
                if (r > room) { room = r; best = dir; }
            }
            return best;
        }

        /// <summary>An enemy of a kind made where it will stand (its home), as the practice room's keys make them, tuned before its first frame.</summary>
        T Make<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            if (_root == null) _root = new GameObject("BomberTestSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, Flat(at), _root, _practiceEnemy, "BomberTest_" + kind + "_" + (++_made));
            Assert.That(e, Is.InstanceOf<T>());
            return (T)e;
        }

        EnemyBomber Bomber(Vector3 at, Action<BomberValues> tune = null)
        {
            var b = Make<EnemyBomber>(EnemyKind.Bomber, at);
            tune?.Invoke(b.Tuning);
            return b;
        }

        // ---------------- The practice room ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoomsKey_SpawnsABomber_InTheSwarmersModel_SmallerAndTinted()
        {
            var others = new List<Key>
            {
                GeneratedLevels.NewLevelKey, GeneratedLevels.SameLevelKey, GeneratedLevels.PracticeRoomKey, GeneratedLevels.BossKey,
                GeneratedLevels.SwarmersKey, GeneratedLevels.ShooterKey, GeneratedLevels.GuardKey, GeneratedLevels.ClearSpawnsKey, GeneratedLevels.CasterKey,
                RunHud.DevTextKey, GeneratedLevels.HairTriggerKey, GeneratedLevels.SteadyHandKey, GeneratedLevels.TodaysFocusKey, GeneratedLevels.RedrawKey,
                RunHud.PauseKey, RunHud.BetweenRunsStartKey, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            };
            InputBindings bindings = ScriptableObject.CreateInstance<InputBindings>(); // its start values
            _objects.Add(bindings);
            others.AddRange(new[] { bindings.MoveUp, bindings.MoveDown, bindings.MoveLeft, bindings.MoveRight, bindings.Dash, bindings.Focus, bindings.Interact, bindings.TakeCover });
            others.AddRange(bindings.Skills.Where(b => b.Source == BindingSource.Key).Select(b => b.Key));
            InputBindings active = _player.GetComponent<PlayerInputDriver>().ActiveBindings; // the asset in use
            others.AddRange(new[] { active.MoveUp, active.MoveDown, active.MoveLeft, active.MoveRight, active.Dash, active.Focus, active.Interact, active.TakeCover });
            if (active.Skills != null) others.AddRange(active.Skills.Where(x => x.Source == BindingSource.Key).Select(x => x.Key));
            Assert.That(GeneratedLevels.BomberKey, Is.EqualTo(Key.B));
            Assert.That(others, Has.No.Member(GeneratedLevels.BomberKey), "a free key");
            Assert.That(DeveloperKeys.IsDeveloperKey(GeneratedLevels.BomberKey), Is.True, "a developer's key (work order 066): on in the Editor, off in a built player");

            _player.Teleport(Stand);
            yield return null;
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Bomber);
            Assert.That(made.Count, Is.EqualTo(1), "one bomber");
            var bomber = made[0] as EnemyBomber;
            Assert.That(bomber, Is.Not.Null, "of its own kind");
            yield return null;
            Assert.That(Flat(bomber.transform.position, Stand), Is.EqualTo(GeneratedLevels.SpawnDistance).Within(0.5f), "12 m from her");
            Assert.That(bomber.ComesBack, Is.False);
            Assert.That(bomber.Body.MaxHealth, Is.EqualTo(bomber.Tuning.Enemy.Health), "the practice room keeps its own health");
            Assert.That(bomber.Body.Radius, Is.EqualTo(bomber.Tuning.BodyRadius));
            Assert.That(bomber.DamageScale, Is.EqualTo(1f), "unscaled in the practice room");
            Assert.That(bomber.Look, Is.Not.Null, "it wears a model");
            Assert.That(bomber.Look.Spec, Is.SameAs(EnemyModelSpec.Bomber));
            Assert.That(bomber.Look.Model.name, Is.EqualTo(EnemyModelSpec.Swarmer.Name), "the swarmer's, the ghoul");
            Assert.That(bomber.transform.lossyScale.x, Is.EqualTo(bomber.Tuning.Shown).Within(1e-4f), "shown smaller");
            Assert.That(bomber.ModelHeight, Is.EqualTo((float)EnemyModelSpec.Swarmer.Height * bomber.Tuning.Shown).Within(1e-3f));
            Assert.That(bomber.BodyColor, Is.EqualTo(EnemyFactory.BomberTint), "tinted ember orange");
            Color painted = bomber.BodyRenderer.sharedMaterial.color, tint = EnemyFactory.BomberTint;
            Assert.That(Mathf.Max(Mathf.Abs(painted.r - tint.r), Mathf.Abs(painted.g - tint.g), Mathf.Abs(painted.b - tint.b)), Is.LessThan(1e-3f), "its body painted in it");
            Assert.That(bomber.Fuse.IsLit, Is.False, "12 m from her, asleep: nothing lit");
            Assert.That(bomber.CircleShown, Is.False);
            Debug.Log(string.Format(s_Inv, "BOMBERLOOK model {0} shown {1:0.00} m high (a swarmer's {2:0.00} m), tint {3}, capsule radius {4} m",
                bomber.Look.Model.name, bomber.ModelHeight, EnemyModelSpec.Swarmer.Height, EnemyFactory.BomberTint, bomber.Tuning.BodyRadius));
            Assert.That(_levels.Spawned, Has.Member(bomber));
            _levels.ClearSpawned();
            yield return null;
            Assert.That(bomber == null, Is.True, "F12 removes it");
        }

        // ---------------- The fuse and the blast on her ----------------

        [UnityTest]
        public IEnumerator ItRunsAtHer_LightsAtTheRange_AndStays_AndSheStanding_IsHurtOnce_ByTheValue()
        {
            EnemyBomber b = Bomber(_her + _open * 8f);
            Vector3 home = b.transform.position;
            int health = _health.Current;
            int landed = 0;
            float struckAt = -1f;
            b.Reported += r =>
            {
                if (r.Kind != EnemyReportKind.Struck) return;
                struckAt = Time.time;
                if (r.Outcome == StrikeOutcome.Landed) landed++;
            };
            yield return Until(() => b.Fuse.IsLit, 5f);
            Assert.That(b.Fuse.IsLit, Is.True, "it lit");
            Assert.That(b.Fuse.Cause, Is.EqualTo(FuseCause.Range));
            float atLight = Flat(b.transform.position, _player.Position);
            Assert.That(atLight, Is.LessThanOrEqualTo(b.Tuning.FuseRange + 0.3f), "at the range");
            Assert.That(Flat(home, b.transform.position), Is.GreaterThan(4f), "it ran at her");
            Assert.That(b.CircleShown && b.FillShown, Is.True, "its circle on the ground from the lighting");
            Vector3 litAt = b.transform.position;
            bool shown = true, stayed = true, filling = true;
            float lastFill = -1f, widest = 0f;
            int frames = 0;
            float deadline = Time.realtimeSinceStartup + 3f;
            while (b.Blasts == 0 && Time.realtimeSinceStartup < deadline)
            {
                shown &= b.CircleShown && b.FillShown;
                stayed &= Flat(b.transform.position, litAt) < 0.1f;
                filling &= b.FillRadius >= lastFill - 1e-4f;
                lastFill = b.FillRadius;
                widest = Mathf.Max(widest, b.SwellNow);
                frames++;
                yield return null;
            }
            Debug.Log(string.Format(s_Inv, "BOMBERSTAND lit {0:0.00} m from her, {1} frames of fuse, blew {2:0.000} s after it lit (fuse {3} s); swelled to {4:0.00}; she lost {5}",
                atLight, frames, struckAt - b.Fuse.LitAt, b.Fuse.Seconds, widest, health - _health.Current));
            Assert.That(b.Blasts, Is.EqualTo(1), "it blew");
            Assert.That(shown, Is.True, "the circle on the ground every frame of the fuse");
            Assert.That(stayed, Is.True, "lit, it stays where it is");
            Assert.That(filling, Is.True, "the circle fills");
            Assert.That(widest, Is.GreaterThan(1.2f), "it swells");
            Assert.That(struckAt - b.Fuse.LitAt, Is.GreaterThanOrEqualTo(b.Tuning.FuseSeconds - 1e-4f), "never before its whole fuse");
            Assert.That(b.LastBlast.HerInside, Is.True);
            Assert.That(health - _health.Current, Is.EqualTo(b.Tuning.BlastAttack.Damage), "hurt by the value");
            Assert.That(landed, Is.EqualTo(1));
            Assert.That(b.IsDead, Is.True, "gone with its blast");
            Assert.That(b.CircleShown || b.FillShown, Is.False);
            Assert.That(b.Look.Renderers.All(r => !r.enabled), Is.True, "and not seen");
            yield return For(1f);
            Assert.That(health - _health.Current, Is.EqualTo(b.Tuning.BlastAttack.Damage), "once");
            Assert.That(b.Blasts, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator SheStepsOut_BeforeTheEnd_IsUnhurt()
        {
            EnemyBomber b = Bomber(_her + _open * 8f);
            StrikeOutcome outcome = StrikeOutcome.Landed;
            b.Reported += r => { if (r.Kind == EnemyReportKind.Struck) outcome = r.Outcome; };
            yield return Until(() => b.Fuse.IsLit, 5f);
            Assert.That(b.Fuse.IsLit, Is.True);
            int health = _health.Current;
            Vector3 away = Flat(_player.Position - b.transform.position).normalized;
            _player.Steer(away); // she walks back the way she came
            yield return Until(() => b.Blasts > 0, 3f);
            float apart = Flat(_player.Position, b.transform.position);
            _player.Steer(Vector3.zero);
            Debug.Log(string.Format(s_Inv, "BOMBERSTEP she walked out at {0:0.0} m/s: {1:0.00} m from its middle at the blast (its circle {2} m, her footprint {3} m)",
                _player.SpeedLimit, apart, b.Tuning.BlastRadius, Placeholders.PlayerRadius));
            Assert.That(b.Blasts, Is.EqualTo(1));
            Assert.That(b.LastBlast.HerInside, Is.False, "out of the circle");
            Assert.That(outcome, Is.EqualTo(StrikeOutcome.OutOfArea));
            Assert.That(_health.Current, Is.EqualTo(health), "unhurt");
        }

        [UnityTest]
        public IEnumerator SheDashesThrough_AtTheEnd_IsUnhurt()
        {
            EnemyBomber b = Bomber(_her + _open * 8f);
            StrikeOutcome outcome = StrikeOutcome.Landed;
            bool protectedAtBlast = false;
            b.Reported += r =>
            {
                if (r.Kind != EnemyReportKind.Struck) return;
                outcome = r.Outcome;
                protectedAtBlast = _player.IsInvulnerable;
            };
            yield return Until(() => b.Fuse.IsLit, 5f);
            Assert.That(b.Fuse.IsLit, Is.True);
            int health = _health.Current;
            yield return Until(() => b.Fuse.Left(Time.time) <= 0.05f, 3f);
            Assume.That(b.Blasts, Is.EqualTo(0), "not blown yet");
            Vector3 through = b.transform.position + Flat(b.transform.position - _player.Position).normalized * 3f;
            Assert.That(_player.DashToward(through), Is.EqualTo(IntentResult.Started), "the dash");
            yield return Until(() => b.Blasts > 0, 2f);
            Debug.Log(string.Format(s_Inv, "BOMBERDASH at the blast she was {0} the circle, in the dash's protected part {1}; {2}",
                b.LastBlast.HerInside ? "inside" : "outside", protectedAtBlast, outcome));
            Assert.That(b.Blasts, Is.EqualTo(1));
            Assert.That(b.LastBlast.HerInside, Is.True, "through it: inside the circle as it blew");
            Assert.That(protectedAtBlast, Is.True, "in the dash's protected part");
            Assert.That(outcome, Is.EqualTo(StrikeOutcome.AvoidedByDash));
            Assert.That(_health.Current, Is.EqualTo(health), "unhurt");
        }

        // ---------------- The blast on its own side ----------------

        [UnityTest]
        public IEnumerator ShotDeadAmongThreeSwarmers_ItDropsOnItsShortFuse_TheyDieByItsBlast_AndTheRecorderSaysSo()
        {
            _folder = Path.Combine(Path.GetTempPath(), "BomberTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            // Asleep (no aggro range to speak of), and with one basic shot's health, so it dies where it stands.
            EnemyBomber b = Bomber(_her + _open * 8f, v => { v.Enemy.AggroRange = 0.5f; v.Enemy.Health = Placeholders.BasicShotDamage; });
            Vector3 at = b.transform.position, side = Vector3.Cross(Vector3.up, _open);
            var swarmers = new List<EnemySwarmer>
            {
                Make<EnemySwarmer>(EnemyKind.Swarmer, at + _open * 1.4f),
                Make<EnemySwarmer>(EnemyKind.Swarmer, at + _open * 0.6f + side * 1.3f),
                Make<EnemySwarmer>(EnemyKind.Swarmer, at + _open * 0.6f - side * 1.3f),
            };
            foreach (EnemySwarmer s in swarmers) s.Tuning.Enemy.AggroRange = 0.5f;
            yield return null;
            Assert.That(swarmers.All(s => s.Body.MaxHealth <= b.Tuning.EnemyDamage), Is.True, "the blast is enough to kill each");
            _player.Shoot(at);
            yield return Until(() => b.IsDead, 3f);
            Assert.That(b.IsDead, Is.True, "shot dead");
            Assert.That(b.Fuse.IsLit, Is.True, "it does not vanish: it drops and lights");
            Assert.That(b.Fuse.Cause, Is.EqualTo(FuseCause.Dropped));
            Assert.That(b.Fuse.Seconds, Is.EqualTo(b.Tuning.ShortFuseSeconds), "the short fuse");
            Assert.That(b.CircleShown, Is.True, "the same circle, where it fell");
            Assert.That(Flat(b.CircleCentre, at), Is.LessThan(0.3f));
            Assert.That(swarmers.All(s => !s.IsDead), Is.True, "they live until it blows");
            yield return Until(() => b.Blasts > 0, 2f);
            Assert.That(b.Blasts, Is.EqualTo(1));
            yield return null; // each death is reported in its own update
            Assert.That(swarmers.All(s => s.IsDead), Is.True, "they die by its blast");
            Assert.That(b.LastBlast.Hurt, Is.EqualTo(3));
            Assert.That(b.LastBlast.Killed, Is.EqualTo(3));
            Assert.That(b.LastBlast.HerInside, Is.False, "she was 8 m off");

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            SessionEvent fuse = events.Single(e => e.Type == SessionEventTypes.EnemyAttack && e.EnemyId == b.Id);
            Assert.That(fuse.Name, Is.EqualTo(BomberValues.Blast));
            Assert.That(fuse.Enemy, Is.EqualTo("bomber"));
            Assert.That(fuse.ExtraValue("fuse"), Is.EqualTo(SessionWords.FuseDropped));
            SessionEvent blast = events.Single(e => e.Type == SessionEventTypes.BomberBlast && e.EnemyId == b.Id);
            Assert.That(blast.ExtraValue("enemies"), Is.EqualTo("3"), "how many enemies it hit");
            Assert.That(blast.ExtraValue("killed"), Is.EqualTo("3"));
            Assert.That(events.Single(e => e.Type == SessionEventTypes.EnemyStrike && e.EnemyId == b.Id).Result, Is.EqualTo(SessionWords.OutOfArea), "and her: not hit");
            Assert.That(events.Count(e => e.Type == SessionEventTypes.EnemyDeath && e.Enemy == "swarmer"), Is.EqualTo(3));
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            Debug.Log("BOMBERSUMMARY\n" + summary);
            StringAssert.Contains("Bombers, whole session", summary);
            StringAssert.IsMatch(@"Enemies killed by a blast\s+3", summary);
        }

        [UnityTest]
        public IEnumerator TwoBombersSideBySide_TheSecondBlowsOneFuseAfterTheFirst()
        {
            // The first 2.2 m from her lights as it wakes; the second, asleep, 1.6 m behind it, is lit by the first's blast.
            EnemyBomber first = Bomber(_her + _open * 2.2f);
            EnemyBomber second = Bomber(_her + _open * 3.8f, v => v.Enemy.AggroRange = 0.5f);
            int health = _health.Current;
            float firstBlew = -1f, secondBlew = -1f;
            first.Reported += r => { if (r.Kind == EnemyReportKind.Blasted) firstBlew = Time.time; };
            second.Reported += r => { if (r.Kind == EnemyReportKind.Blasted) secondBlew = Time.time; };
            yield return Until(() => first.Blasts > 0, 3f);
            Assert.That(first.Blasts, Is.EqualTo(1));
            Assert.That(first.Fuse.Cause, Is.EqualTo(FuseCause.Range));
            Assert.That(first.LastBlast.Lit, Is.EqualTo(1), "it set the other off");
            Assert.That(second.Fuse.IsLit, Is.True);
            Assert.That(second.Fuse.Cause, Is.EqualTo(FuseCause.Chain));
            Assert.That(second.Fuse.LitAt, Is.EqualTo(firstBlew).Within(1e-5f), "lit at the first's blast");
            Assert.That(second.Fuse.Seconds, Is.EqualTo(second.Tuning.FuseSeconds), "its own full fuse");
            Assert.That(second.Blasts, Is.EqualTo(0), "never an instant chain");
            Assert.That(second.IsDead, Is.False, "not hurt by it");
            Assert.That(second.CircleShown, Is.True);
            yield return Until(() => second.Blasts > 0, 3f);
            Debug.Log(string.Format(s_Inv, "BOMBERCHAIN the first blew at {0:0.000} s, the second at {1:0.000} s: {2:0.000} s after (fuse {3} s)",
                firstBlew, secondBlew, secondBlew - firstBlew, second.Tuning.FuseSeconds));
            Assert.That(second.Blasts, Is.EqualTo(1));
            Assert.That(secondBlew - firstBlew, Is.GreaterThanOrEqualTo(second.Tuning.FuseSeconds - 1e-4f), "one fuse after the first");
            Assert.That(secondBlew - firstBlew, Is.LessThan(second.Tuning.FuseSeconds + 0.1f));
            Assert.That(first.IsDead && second.IsDead, Is.True);
            Assert.That(health - _health.Current, Is.EqualTo(first.Tuning.BlastAttack.Damage), "she was in the first's circle only");
        }

        // ---------------- What it leaves and what a frame costs ----------------

        static object s_junk;

        static (int objects, int materials, int meshes, int lines) Count() =>
            (Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
             Resources.FindObjectsOfTypeAll<Material>().Length, Resources.FindObjectsOfTypeAll<Mesh>().Length,
             Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

        IEnumerator OneBlastAndGone(Action<EnemyBomber> whileLit = null)
        {
            EnemyBomber b = Bomber(_her + _open * 2.2f);
            yield return Until(() => b.Fuse.IsLit, 2f);
            Assert.That(b.Fuse.IsLit, Is.True);
            whileLit?.Invoke(b);
            yield return Until(() => b.Blasts > 0, 2f);
            Assert.That(b.Blasts, Is.EqualTo(1));
            yield return For(0.6f); // its spark and stand-in are gone after 0.3 s
            Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name == "StrikeFx" || r.name.StartsWith("ShotFx_", StringComparison.Ordinal)), Is.EqualTo(0),
                        "its blast's effects are gone");
            Object.Destroy(_root.gameObject);
            _root = null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator NothingIsLeftBehind_AndAFrameOfItsFuseAllocatesNothing()
        {
            yield return OneBlastAndGone(); // the first loads the model and its textures
            _health.RestoreFull();
            (int objects, int materials, int meshes, int lines) before = Count();
            bool allocChecked = false;
            yield return OneBlastAndGone(b =>
            {
                // The measure is the test framework's own (it counts the GC.Alloc samples on this thread); first, that it sees one.
                Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
                for (int i = 0; i < 10; i++) b.DrawFuseNow();
                Assert.That(() => { for (int i = 0; i < 300; i++) b.DrawFuseNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of its fuse allocates nothing");
                allocChecked = true;
            });
            Assert.That(allocChecked, Is.True);
            (int objects, int materials, int meshes, int lines) after = Count();
            Debug.Log("BOMBERLEFT before " + before + ", after " + after);
            Assert.That(after.objects, Is.EqualTo(before.objects), "no object left behind");
            Assert.That(after.materials, Is.EqualTo(before.materials), "no material");
            Assert.That(after.meshes, Is.EqualTo(before.meshes), "no mesh");
            Assert.That(after.lines, Is.EqualTo(before.lines), "no line");
        }

        // ---------------- Heard and seen ----------------

        [UnityTest]
        public IEnumerator ItsFuse_AsksForTheRedWindup_ItsBlast_TheStrikeSoundAndTheCircleStrikeWhereItBlew_AndNoInvalidBoundsAssert()
        {
            float volume = AudioListener.volume;
            AudioListener.volume = 0f; // nothing out of the speakers: the requests are what is tested
            PlaceholderSounds sounds = PlaceholderSounds.Ensure();
            CombatEffects fx = CombatEffects.Ensure();
            _objects.Add(sounds.gameObject);
            _objects.Add(fx.gameObject);
            var heard = new List<PlaceholderSoundRequest>();
            var drawn = new List<(FxPiece p, int frame)>();
            int asserts = 0;
            void Hear(PlaceholderSoundRequest r) => heard.Add(r);
            void Draw(CombatFx f) => drawn.Add((f.Piece, Time.frameCount));
            void OnLog(string message, string stack, LogType type) { if (message != null && message.Contains("Converting invalid MinMaxAABB")) asserts++; }
            PlaceholderSounds.Requested += Hear;
            CombatEffects.Spawned += Draw;
            Application.logMessageReceivedThreaded += OnLog;
            try
            {
                yield return null;
                EnemyBomber b = Bomber(_her + _open * 2.2f);
                int litFrame = -1, blewFrame = -1;
                b.Reported += r =>
                {
                    if (r.Kind == EnemyReportKind.AttackStarted) litFrame = Time.frameCount;
                    if (r.Kind == EnemyReportKind.Struck) blewFrame = Time.frameCount;
                };
                yield return Until(() => litFrame > 0, 2f);
                Assert.That(litFrame, Is.GreaterThan(0), "lit");
                yield return null; // the sounds go at the frame's end
                CombatFxValues fv = fx.Values;
                Assert.That(heard.Count(r => r.Frame == litFrame && r.Sound == PlaceholderSound.WindupRed), Is.EqualTo(1),
                            "its fuse: the red wind-up; heard " + string.Join(", ", heard.Select(h => h.Sound + "@" + h.Frame)));
                List<FxPiece> windups = drawn.Where(d => d.frame == litFrame && d.p.Moment == FxMoment.WindupStarted).Select(d => d.p).ToList();
                Assert.That(windups.Count, Is.EqualTo(1), "the wind-up's star");
                Assert.That(Flat(windups[0].At, b.transform.position), Is.LessThan(0.05f), "on it");
                Assert.That(windups[0].Rim, Is.EqualTo(fv.StrikeRim), "red");
                Assert.That(windups[0].Size, Is.EqualTo(fv.WindupSize * new FxSizes().KindScale(EnemyKind.Swarmer)).Within(1e-4f), "a swarmer's size");

                yield return Until(() => blewFrame > 0, 2f);
                Assert.That(blewFrame, Is.GreaterThan(0), "blew");
                Vector3 centre = b.LastBlast.Centre;
                yield return null;
                Assert.That(heard.Count(r => r.Frame == blewFrame && r.Sound == PlaceholderSound.Strike), Is.EqualTo(1), "the strike sound as it blows");
                List<FxPiece> rings = drawn.Where(d => d.frame == blewFrame && d.p.Moment == FxMoment.EnemyStruck && d.p.Shape == FxShape.Ring).Select(d => d.p).ToList();
                List<FxPiece> stars = drawn.Where(d => d.frame == blewFrame && d.p.Moment == FxMoment.EnemyStruck && d.p.Shape == FxShape.Spark).Select(d => d.p).ToList();
                Assert.That(rings.Count, Is.EqualTo(1), "the circle strike's ring");
                Assert.That(stars.Count, Is.EqualTo(1), "and its star");
                Assert.That(Flat(rings[0].At, centre), Is.LessThan(0.05f), "where it blew");
                Assert.That(rings[0].Size, Is.EqualTo(b.Tuning.BlastRadius).Within(1e-4f), "to the circle's edge");
                Assert.That(rings[0].Rim, Is.EqualTo(fv.StrikeRim));
                Assert.That(Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Count(r => r.name == "StrikeFx"), Is.EqualTo(0), "no flat stand-in");
                yield return For(1f);
                Assert.That(asserts, Is.EqualTo(0), "no \"Converting invalid MinMaxAABB\" (work order 061): it makes no particle system");
            }
            finally
            {
                PlaceholderSounds.Requested -= Hear;
                CombatEffects.Spawned -= Draw;
                Application.logMessageReceivedThreaded -= OnLog;
                AudioListener.volume = volume;
            }
        }

        // ---------------- The floors ----------------

        [UnityTest]
        public IEnumerator AFloor2_WithThemOn_MakesItsBombers_WithTheFloorsHealthAndDamageScale()
        {
            _practiceEnemy.enabled = true;
            var values = new LevelValues { Bombers = true };
            _levels.Values = values;
            ulong seed = 0;
            while (!LevelGenerator.Generate(seed, 2, values).Rooms.Any(r => r.EnemyGroups.Any(g => g.Kind == EnemyKind.Bomber))) seed++;
            GeneratedLevel level = _levels.Build(seed, 2);
            yield return null;
            LevelRoom first = LevelGeometry.MainPath(level.Layout).First(r => r.Kind == RoomKind.Combat);
            int bombers = 0;
            foreach (LevelRoom r in level.Layout.Rooms)
            {
                List<Enemy> list = level.EnemiesByRoom.TryGetValue(r.Order, out List<Enemy> l) ? l : new List<Enemy>();
                List<EnemyBomber> here = list.OfType<EnemyBomber>().ToList();
                Assert.That(here.Count, Is.EqualTo(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Bomber).Sum(g => g.Count)), "room " + r.Letter + r.Order + " as the description says");
                if (r == first) Assert.That(here, Is.Empty, "none in the first room");
                foreach (EnemyBomber e in here)
                {
                    Assert.That(e.Body.MaxHealth, Is.EqualTo(values.BomberHealth), e.name);
                    Assert.That(e.DamageScale, Is.EqualTo(values.DamageScaleOf(2)).Within(1e-5f), "the floor's scale");
                    Assert.That(e.TakesTurns, Is.True, "a floor's enemy (it takes no turn to blow: of no sort)");
                    Assert.That(e.IsElite, Is.False);
                }
                bombers += here.Count;
            }
            Debug.Log("BOMBERFLOOR seed " + seed + " floor 2 holds " + bombers + " bombers\n" + LevelMap.Draw(level.Layout));
            Assert.That(bombers, Is.GreaterThan(0));
            Assert.That(BomberRules.HerDamage(new BomberValues(), values.DamageScaleOf(2)), Is.EqualTo(12), "a floor 2's blast on her: 0.6 of 20");
            _levels.BackToPracticeRoom();
        }
    }
}
