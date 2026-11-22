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
    /// The warder (work order 079, 2026-11-22, "Enda en fiendetype"), in the ControlRoom scene's practice room, fought through
    /// the character's intents (she shoots): its practice-room key spawns one, and with Shift a pack with one (two swarmers, a
    /// guard and the warder), wearing the caster's model larger and tinted; in a pack, within a few seconds two allies are
    /// warded, with their threads and shells; her shot on a warded swarmer deals the lessened damage and flashes the shell, on
    /// an unwarded enemy the full; she kills the warder: both wards are gone, both dazed, their next hit full, and the recorder
    /// says so; she staggers it: the same; the last ally dies: it throws the caster's bolt; nothing is left behind, a frame of
    /// its wards allocates nothing, and the engine's "Converting invalid MinMaxAABB" assert (work order 061) does not come.
    ///
    /// The practice brute is switched off (it stays in the scene as the template the factory copies its telegraph from). Its
    /// allies are made asleep (an aggro range of half a metre), placed so that her line to each is clear of the others.
    /// </summary>
    public class WarderTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        Transform _root;
        Vector3 _open;      // the way from Stand with the most walkable ground
        Vector3 _side;      // across it
        Vector3 _her;       // where she stands for a test
        string _folder;
        PlaySessionRecorder _recorder;
        int _made;
        int _asserts;
        readonly List<Object> _objects = new List<Object>();

        void OnLog(string message, string stack, LogType type)
        {
            if (message != null && message.Contains("Converting invalid MinMaxAABB")) _asserts++;
        }

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
            _side = Vector3.Cross(Vector3.up, _open);
            _her = Stand + _open * 2f;
            _player.Teleport(_her);
            _asserts = 0;
            Application.logMessageReceivedThreaded += OnLog;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            Application.logMessageReceivedThreaded -= OnLog;
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

        /// <summary>A point along her way and across it, on the walkable ground.</summary>
        Vector3 At(float along, float across)
        {
            Vector3 p = _her + _open * along + _side * across;
            return NavMesh.SamplePosition(Flat(p), out NavMeshHit hit, 2f, NavMesh.AllAreas) ? Flat(hit.position) : Flat(p);
        }

        T Make<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            if (_root == null) _root = new GameObject("WarderTestSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, Flat(at), _root, _practiceEnemy, "WarderTest_" + kind + "_" + (++_made));
            Assert.That(e, Is.InstanceOf<T>());
            return (T)e;
        }

        /// <summary>
        /// A pack with a warder, asleep but the warder: two swarmers 10 m off, 2.5 m to each side of her line, a guard 5 m off
        /// and 3 m aside, and the warder 13 m off on her line, so that it wards the two swarmers (the nearest) and her line to
        /// each is clear of the others.
        /// </summary>
        (EnemyWarder warder, EnemySwarmer a, EnemySwarmer b, EnemyGuard guard) Pack(Action<WarderValues> tune = null)
        {
            var a = Make<EnemySwarmer>(EnemyKind.Swarmer, At(10f, 2.5f));
            var b = Make<EnemySwarmer>(EnemyKind.Swarmer, At(10f, -2.5f));
            var guard = Make<EnemyGuard>(EnemyKind.Guard, At(5f, -3f));
            a.Tuning.Enemy.AggroRange = 0.5f;
            b.Tuning.Enemy.AggroRange = 0.5f;
            guard.Tuning.Enemy.AggroRange = 0.5f;
            var w = Make<EnemyWarder>(EnemyKind.Warder, At(13f, 0f));
            tune?.Invoke(w.Tuning);
            return (w, a, b, guard);
        }

        IEnumerator TwoWardsOn(EnemyWarder w, EnemySwarmer a, EnemySwarmer b)
        {
            float start = Time.time;
            yield return Until(() => w.Keeper.OnCount >= 2, 12f);
            float took = Time.time - start;
            Debug.Log(string.Format(s_Inv, "WARDERWARDS two on after {0:0.00} s (raise {1} s, pause {2} s)", took, w.Tuning.RaiseSeconds, w.Tuning.WardEvery));
            Assert.That(w.Keeper.OnCount, Is.EqualTo(2), "two allies warded");
            Assert.That(took, Is.LessThanOrEqualTo(w.Tuning.WardEvery + w.Tuning.RaiseSeconds + 1f), "within a few seconds");
            Assert.That(w.Wards(a) && w.Wards(b), Is.True, "the two swarmers, the nearest");
        }

        /// <summary>One shot of hers at an enemy, through her intent: what its body lost, and what a ward took off it.</summary>
        IEnumerator ShootAt(Enemy e, int[] lostAndOff)
        {
            int lost = 0, off = 0;
            void Lost(int n) => lost += n;
            void Off(int n) => off += n;
            e.Body.Damaged += Lost;
            e.Body.Warded += Off;
            try
            {
                _player.Shoot(e.transform.position);
                yield return Until(() => lost > 0, 3f);
            }
            finally
            {
                e.Body.Damaged -= Lost;
                e.Body.Warded -= Off;
            }
            lostAndOff[0] = lost;
            lostAndOff[1] = off;
        }

        // ---------------- The practice room ----------------

        [UnityTest]
        public IEnumerator ThePracticeRoomsKey_SpawnsAWarder_AndWithShiftAPackWithOne_InTheCastersModel_LargerAndTinted()
        {
            var others = new List<Key>
            {
                GeneratedLevels.NewLevelKey, GeneratedLevels.SameLevelKey, GeneratedLevels.PracticeRoomKey, GeneratedLevels.BossKey,
                GeneratedLevels.SwarmersKey, GeneratedLevels.ShooterKey, GeneratedLevels.GuardKey, GeneratedLevels.ClearSpawnsKey, GeneratedLevels.CasterKey,
                GeneratedLevels.BomberKey, RunHud.DevTextKey, GeneratedLevels.HairTriggerKey, GeneratedLevels.SteadyHandKey, GeneratedLevels.TodaysFocusKey,
                GeneratedLevels.RedrawKey, RunHud.PauseKey, RunHud.BetweenRunsStartKey, Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4,
            };
            InputBindings bindings = ScriptableObject.CreateInstance<InputBindings>();
            _objects.Add(bindings);
            others.AddRange(new[] { bindings.MoveUp, bindings.MoveDown, bindings.MoveLeft, bindings.MoveRight, bindings.Dash, bindings.Focus, bindings.Interact, bindings.TakeCover });
            others.AddRange(bindings.Skills.Where(x => x.Source == BindingSource.Key).Select(x => x.Key));
            InputBindings active = _player.GetComponent<PlayerInputDriver>().ActiveBindings;
            others.AddRange(new[] { active.MoveUp, active.MoveDown, active.MoveLeft, active.MoveRight, active.Dash, active.Focus, active.Interact, active.TakeCover });
            if (active.Skills != null) others.AddRange(active.Skills.Where(x => x.Source == BindingSource.Key).Select(x => x.Key));
            Assert.That(GeneratedLevels.WarderKey, Is.EqualTo(Key.V));
            Assert.That(others, Has.No.Member(GeneratedLevels.WarderKey), "a free key");
            Assert.That(DeveloperKeys.IsDeveloperKey(GeneratedLevels.WarderKey), Is.True, "a developer's key (work order 066)");

            _player.Teleport(Stand);
            yield return null;
            List<Enemy> one = _levels.SpawnWarder(false);
            Assert.That(one.Count, Is.EqualTo(1));
            var w = one[0] as EnemyWarder;
            Assert.That(w, Is.Not.Null, "of its own kind");
            yield return null;
            Assert.That(Flat(w.transform.position, Stand), Is.EqualTo(GeneratedLevels.SpawnDistance).Within(0.5f), "12 m from her");
            Assert.That(w.Body.MaxHealth, Is.EqualTo(w.Tuning.Enemy.Health));
            Assert.That(w.Stagger, Is.Not.Null, "a stagger bar");
            Assert.That(w.Look, Is.Not.Null, "it wears a model");
            Assert.That(w.Look.Spec, Is.SameAs(EnemyModelSpec.Warder));
            Assert.That(w.Look.Model.name, Is.EqualTo(EnemyModelSpec.Caster.Name), "the caster's, the hooded skeleton");
            Assert.That(w.transform.lossyScale.x, Is.EqualTo(w.Tuning.Shown).Within(1e-4f), "shown larger");
            Assert.That(w.BodyColor, Is.EqualTo(EnemyFactory.WarderTint), "tinted green");
            Debug.Log(string.Format(s_Inv, "WARDERLOOK model {0} shown {1:0.00} m high (a caster's {2:0.00} m), tint {3}",
                w.Look.Model.name, w.ModelHeight, EnemyModelSpec.Caster.Height, EnemyFactory.WarderTint));
            _levels.ClearSpawned();
            yield return null;
            Assert.That(w == null, Is.True, "F12 removes it");

            List<Enemy> pack = _levels.SpawnWarder(true);
            Assert.That(pack.Select(e => e.Kind), Is.EqualTo(new[] { EnemyKind.Swarmer, EnemyKind.Swarmer, EnemyKind.Guard, EnemyKind.Warder }),
                        "two swarmers and a guard and one warder");
            Vector3 middle = (pack[0].transform.position + pack[1].transform.position + pack[2].transform.position) / 3f;
            Assert.That(Flat(pack[3].transform.position, Stand), Is.GreaterThan(Flat(middle, Stand)), "the warder behind them");
            yield return Until(() => ((EnemyWarder)pack[3]).Keeper.OnCount > 0, 4f);
            Assert.That(((EnemyWarder)pack[3]).Keeper.OnCount, Is.GreaterThan(0), "he meets a ward within seconds");
            _levels.ClearSpawned();
            yield return null;
        }

        // ---------------- The wards, and her shots ----------------

        [UnityTest]
        public IEnumerator InAPack_TwoAlliesAreWarded_WithThreadAndShell_AndHerShotOnAWardedSwarmerDealsTheLessenedDamage_OnAnUnwardedTheFull()
        {
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, EnemyGuard guard) = Pack();
            yield return Until(() => w.Keeper.Wards.Count > 0, 3f);
            Enemy first = w.Raises(a) ? (Enemy)a : b;
            Assert.That(w.Raises(first), Is.True, "a raising began");
            Assert.That(w.ThreadShown(first), Is.True, "its thread grows");
            Assert.That(w.ShellShown(first), Is.False, "no shell before it is on");
            Assert.That(EnemyWarder.IsWarded(first), Is.False, "not lessened before it is on");
            yield return TwoWardsOn(w, a, b);
            foreach (Enemy e in new Enemy[] { a, b })
            {
                Assert.That(w.ThreadShown(e), Is.True, "the thread stands between them");
                Assert.That(w.ShellShown(e), Is.True, "the shell on it");
                Assert.That(EnemyWarder.IsWarded(e), Is.True);
            }
            Assert.That(EnemyWarder.IsWarded(guard), Is.False, "two at most");

            var shot = new int[2];
            int flashes = w.ShellFlashes;
            yield return ShootAt(a, shot);
            Debug.Log("WARDERSHOT on a warded swarmer: lost " + shot[0] + ", warded away " + shot[1]);
            Assert.That(shot[0], Is.GreaterThan(0), "hit");
            Assert.That(shot[1], Is.GreaterThan(0), "the ward took some off");
            Assert.That(shot[0], Is.EqualTo(WarderRules.Lessened(shot[0] + shot[1], w.Tuning.WardShare)), "the lessened damage, as the rules give it");
            Assert.That(w.ShellFlashes, Is.EqualTo(flashes + 1), "the shell flashed at the hit");
            yield return For(0.5f);
            yield return ShootAt(guard, shot);
            Debug.Log("WARDERSHOT on the unwarded guard: lost " + shot[0] + ", warded away " + shot[1]);
            Assert.That(shot[0], Is.GreaterThan(0));
            Assert.That(shot[1], Is.EqualTo(0), "the full damage");
            Assert.That(_asserts, Is.EqualTo(0), "no \"Converting invalid MinMaxAABB\"");
        }

        [UnityTest]
        public IEnumerator SheKillsTheWarder_BothWardsBreak_BothDazed_TheirNextHitFull_AndTheRecorderSaysSo()
        {
            _folder = Path.Combine(Path.GetTempPath(), "WarderTests-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack(v => v.Enemy.Health = Placeholders.BasicShotDamage);
            yield return TwoWardsOn(w, a, b);
            yield return For(0.5f); // the warder at its spot
            _player.Shoot(w.transform.position);
            yield return Until(() => w.IsDead, 3f);
            Assert.That(w.IsDead, Is.True, "shot dead");
            yield return null;
            Assert.That(w.Keeper.Wards, Is.Empty, "both wards gone");
            Assert.That(EnemyWarder.IsWarded(a) || EnemyWarder.IsWarded(b), Is.False);
            Assert.That(a.IsDazed && b.IsDazed, Is.True, "both dazed");
            Assert.That(w.Bursts, Is.EqualTo(2), "both shells burst");
            Assert.That(w.ShellShown(a), Is.False, "the shell no longer on it");
            yield return For(w.Tuning.BreakDaze + 0.1f);
            Assert.That(a.IsDazed, Is.False, "the daze is a moment");
            var shot = new int[2];
            yield return ShootAt(a, shot);
            Assert.That(shot[0], Is.GreaterThan(0));
            Assert.That(shot[1], Is.EqualTo(0), "its next hit is full");

            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Count(e => e.Type == SessionEventTypes.WardRaised && e.EnemyId == w.Id), Is.EqualTo(2));
            List<SessionEvent> broken = events.Where(e => e.Type == SessionEventTypes.WardBroken && e.EnemyId == w.Id).ToList();
            Assert.That(broken.Count, Is.EqualTo(2));
            Assert.That(broken.All(e => e.ExtraValue("why") == SessionWords.WarderKilled), Is.True, "why: the warder killed");
            _recorder.StopRecording();
            string summary = File.ReadAllText(_recorder.SummaryPath);
            Debug.Log("WARDERSUMMARY\n" + summary);
            StringAssert.Contains("Warders, whole session", summary);
            StringAssert.IsMatch(@"Warders killed\s+1", summary);
            Assert.That(_asserts, Is.EqualTo(0), "no \"Converting invalid MinMaxAABB\"");
        }

        [UnityTest]
        public IEnumerator SheStaggersTheWarder_BothWardsBreak_AndBothAreDazed()
        {
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack();
            yield return TwoWardsOn(w, a, b);
            // A hit of hers that empties its bar, as her staggering skills do (each hit takes its skill's stagger off it).
            w.Body.TakeHit(1, w.Stagger.Current + 1f);
            Assert.That(w.IsDazed, Is.True, "staggered");
            Assert.That(w.Keeper.Wards, Is.Empty, "both wards gone at once");
            Assert.That(a.IsDazed && b.IsDazed, Is.True, "both dazed");
            Assert.That(EnemyWarder.IsWarded(a) || EnemyWarder.IsWarded(b), Is.False);
            yield return For(0.2f);
            Assert.That(w.Keeper.Wards, Is.Empty, "none raised while it is dazed");
        }

        [UnityTest]
        public IEnumerator TheLastAllyDies_ItThrowsTheCastersBolt()
        {
            var ally = Make<EnemySwarmer>(EnemyKind.Swarmer, At(10f, 2f));
            ally.Tuning.Enemy.AggroRange = 0.5f;
            var w = Make<EnemyWarder>(EnemyKind.Warder, At(11f, 0f));
            int loosed = 0;
            void Loose(Enemy e, EnemyAttackDefinition attack, int count) { if (e == w) loosed += count; }
            ThingsThatFly.Loosed += Loose;
            try
            {
                yield return Until(() => w.Keeper.OnCount > 0, 4f);
                Assert.That(w.Wards(ally), Is.True, "it wards its one ally");
                Assert.That(w.IsAlone, Is.False);
                yield return For(1.5f);
                Assert.That(loosed, Is.EqualTo(0), "with an ally it shields, and throws nothing");
                ally.Body.TakeHit(ally.Body.CurrentHealth * 10);
                yield return null;
                yield return null;
                Assert.That(w.IsAlone, Is.True, "alone");
                Assert.That(w.Keeper.Wards, Is.Empty, "its ward gone with its ally");
                yield return Until(() => loosed > 0, 8f);
                Assert.That(loosed, Is.EqualTo(1), "it throws the bolt");
                Assert.That(w.Tuning.Enemy.Attacks.Single().Name, Is.EqualTo(CasterValues.BoneOrb));
            }
            finally
            {
                ThingsThatFly.Loosed -= Loose;
            }
        }

        // ---------------- What it leaves and what a frame costs ----------------

        static object s_junk;

        static (int objects, int materials, int meshes, int lines) Count() =>
            (Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length,
             Resources.FindObjectsOfTypeAll<Material>().Length, Resources.FindObjectsOfTypeAll<Mesh>().Length,
             Object.FindObjectsByType<LineRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None).Length);

        IEnumerator APackWardedAndGone(Action<EnemyWarder> whileOn = null)
        {
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack();
            yield return TwoWardsOn(w, a, b);
            whileOn?.Invoke(w);
            w.Body.TakeHit(w.Body.CurrentHealth * 10); // its wards break and their shells burst
            yield return For(w.Tuning.BurstSeconds + 0.2f);
            Object.Destroy(_root.gameObject);
            _root = null;
            yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator NothingIsLeftBehind_AndAFrameOfItsWardsAllocatesNothing()
        {
            yield return APackWardedAndGone(); // the first loads the models and their textures
            _health.RestoreFull();
            (int objects, int materials, int meshes, int lines) before = Count();
            bool allocChecked = false;
            yield return APackWardedAndGone(w =>
            {
                Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
                for (int i = 0; i < 10; i++) w.DrawWardsNow();
                Assert.That(() => { for (int i = 0; i < 300; i++) w.DrawWardsNow(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "a frame of its wards allocates nothing");
                allocChecked = true;
            });
            Assert.That(allocChecked, Is.True);
            (int objects, int materials, int meshes, int lines) after = Count();
            Debug.Log("WARDERLEFT before " + before + ", after " + after);
            Assert.That(after.objects, Is.EqualTo(before.objects), "no object left behind");
            Assert.That(after.materials, Is.EqualTo(before.materials), "no material");
            Assert.That(after.meshes, Is.EqualTo(before.meshes), "no mesh");
            Assert.That(after.lines, Is.EqualTo(before.lines), "no line");
            Assert.That(_asserts, Is.EqualTo(0), "no \"Converting invalid MinMaxAABB\" (work order 061): it makes no particle system");
        }
    }
}
