using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
    /// Work order 107, the warder that reads (Kristian's tick of 2026-11-24, "vokteren som ikke leses"), in the ControlRoom scene's
    /// practice room, fought through her intents (she shoots), with the pack of work order 079's tests (two swarmers, a guard, the
    /// warder behind them):
    ///
    /// - the switch off is today: no body of its own, its glows the caster's cyan, its threads from its chest at their old width,
    ///   its rings at theirs, no hum, no break heard, no mark on a warded hit's number, no line; and a warder woken on an object
    ///   that is off (its switch read in Awake, as <see cref="HerAwakeOrderTests"/> asks) wakes without a word in the log;
    /// - on: its own body (a staff taller than it with the ward's light on top, a halo, its glows green), the thread from that
    ///   light, the thread and the ring wider; turned off live it hides and gives the glows back;
    /// - a warded hit's number carries the mark, an unwarded one's does not; the line shows by the target at the first warded
    ///   hit, and not at the next;
    /// - the hum starts with the first ward on, goes on while one stands, and stops with the last; a ward that falls is heard,
    ///   one whose ally died is not;
    /// - a frame while a ward stands (its wards drawn, its body, the sounds' frame, the feedback's frame) allocates nothing.
    ///
    /// Nobody has played it. The practice brute is switched off (it stays as the template the factory copies its telegraph from);
    /// the warder's allies are made asleep (an aggro range of half a metre), placed so her line to each is clear of the others.
    /// </summary>
    public class WarderReadsTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Vector3 Stand = new Vector3(8f, 0f, -6f);

        PlayerCharacter _player;
        PlayerHealth _health;
        EnemyBrute _practiceEnemy;
        GeneratedLevels _levels;
        Transform _root;
        Vector3 _open, _side, _her;
        int _made;
        readonly List<Object> _objects = new List<Object>();
        readonly List<PlaceholderSoundRequest> _requested = new List<PlaceholderSoundRequest>();

        void OnRequested(PlaceholderSoundRequest r) => _requested.Add(r);

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
            PlaceholderSounds.Requested += OnRequested;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            PlaceholderSounds.Requested -= OnRequested;
            _requested.Clear();
            foreach (Object o in _objects) if (o != null) Object.Destroy(o);
            _objects.Clear();
            if (_root != null) Object.Destroy(_root.gameObject);
            _root = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (PlaceholderSounds.Current != null) Object.Destroy(PlaceholderSounds.Current.gameObject);
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            if (_player != null) _player.Steer(Vector3.zero);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers (as work order 079's WarderTests) ----------------

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

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
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

        Vector3 At(float along, float across)
        {
            Vector3 p = _her + _open * along + _side * across;
            return NavMesh.SamplePosition(Flat(p), out NavMeshHit hit, 2f, NavMesh.AllAreas) ? Flat(hit.position) : Flat(p);
        }

        T Make<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            if (_root == null) _root = new GameObject("WarderReadsTestSpawns").transform;
            Enemy e = EnemyFactory.Make(kind, Flat(at), _root, _practiceEnemy, "WarderReadsTest_" + kind + "_" + (++_made));
            Assert.That(e, Is.InstanceOf<T>());
            return (T)e;
        }

        /// <summary>
        /// The warder made on an object that is off, so it wakes only when <paramref name="tune"/> has set its values (its switch
        /// is read as it wakes); then switched on.
        /// </summary>
        EnemyWarder MakeWarderTuned(Vector3 at, Action<WarderValues> tune)
        {
            var holder = new GameObject("WarderReadsTest_Holder");
            holder.SetActive(false);
            if (_root == null) _root = new GameObject("WarderReadsTestSpawns").transform;
            holder.transform.SetParent(_root, false);
            Enemy e = EnemyFactory.Make(EnemyKind.Warder, Flat(at), holder.transform, _practiceEnemy, "WarderReadsTest_Warder_" + (++_made));
            var w = (EnemyWarder)e;
            tune?.Invoke(w.Tuning);
            holder.SetActive(true);
            return w;
        }

        (EnemyWarder warder, EnemySwarmer a, EnemySwarmer b, EnemyGuard guard) Pack(Action<WarderValues> tune = null)
        {
            var a = Make<EnemySwarmer>(EnemyKind.Swarmer, At(10f, 2.5f));
            var b = Make<EnemySwarmer>(EnemyKind.Swarmer, At(10f, -2.5f));
            var guard = Make<EnemyGuard>(EnemyKind.Guard, At(5f, -3f));
            a.Tuning.Enemy.AggroRange = 0.5f;
            b.Tuning.Enemy.AggroRange = 0.5f;
            guard.Tuning.Enemy.AggroRange = 0.5f;
            EnemyWarder w = tune == null ? Make<EnemyWarder>(EnemyKind.Warder, At(13f, 0f)) : MakeWarderTuned(At(13f, 0f), tune);
            return (w, a, b, guard);
        }

        static IEnumerator TwoWardsOn(EnemyWarder w)
        {
            yield return Until(() => w.Keeper.OnCount >= 2, 12f);
            Assert.That(w.Keeper.OnCount, Is.EqualTo(2), "two allies warded");
        }

        /// <summary>A shot of hers at an enemy, through her intent, waiting until it lands (again, up to three times, if it misses one that moves).</summary>
        IEnumerator ShootAt(Enemy e)
        {
            int lost = 0;
            void Lost(int n) => lost += n;
            e.Body.Damaged += Lost;
            try
            {
                for (int tries = 0; tries < 3 && lost == 0; tries++)
                {
                    // One that came to cling to her is shot from a step back: a shot from inside its body can pass it.
                    Vector3 away = Flat(_player.Position - e.transform.position);
                    if (away.magnitude < 2.5f)
                    {
                        Vector3 back = e.transform.position + (away.sqrMagnitude > 1e-4f ? away.normalized : -_open) * 4f;
                        if (NavMesh.SamplePosition(Flat(back), out NavMeshHit hit, 2f, NavMesh.AllAreas)) _player.Teleport(hit.position);
                        yield return null;
                    }
                    _player.Shoot(e.transform.position);
                    yield return Until(() => lost > 0, 1.5f);
                }
            }
            finally
            {
                e.Body.Damaged -= Lost;
            }
            Assert.That(lost, Is.GreaterThan(0), "her shot landed on " + e.name);
        }

        static LineRenderer Child(EnemyWarder w, string name) =>
            w.GetComponentsInChildren<LineRenderer>(true).FirstOrDefault(l => l.name == name);

        static List<Color> Glows(Enemy e) =>
            e.Look.Renderers.Select(r => r.sharedMaterial).Where(m => m != null && m.HasProperty("_EmissionColor") && m.IsKeywordEnabled("_EMISSION"))
             .Select(m => m.GetColor("_EmissionColor")).ToList();

        // ---------------- The switch off is today ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_IsToday_NoBody_TheCastersGlows_TheThreadsAndRingsAsBefore_NoHum_NoMark_NoLine()
        {
            PlaceholderSounds sounds = PlaceholderSounds.Ensure();
            TimingFeedback feedback = TimingFeedback.Ensure();
            var caster = Make<EnemyCaster>(EnemyKind.Caster, At(3f, 6f));
            caster.Tuning.Enemy.AggroRange = 0.5f;
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack(v => v.Reads = false);
            yield return null;
            Assert.That(w.Tuning.Reads, Is.False);
            Assert.That(w.ReadBody, Is.Null, "no body of its own");
            Assert.That(w.transform.Find(WarderBody.RootName), Is.Null);
            Assert.That(w.GetComponentsInChildren<Transform>(true).Any(t => t.name == WarderBody.StaffName || t.name == WarderBody.HaloName), Is.False);
            Assert.That(Glows(w), Is.EqualTo(Glows(caster)), "its glows the caster's own cyan, as before");
            Assert.That(Glows(w).Count, Is.GreaterThan(0), "the caster's model has its glows");

            yield return TwoWardsOn(w);
            foreach (Enemy e in new Enemy[] { a, b })
            {
                Assert.That(w.ThreadShown(e) && w.ShellShown(e), Is.True);
            }
            foreach (int i in new[] { 0, 1 })
            {
                LineRenderer thread = Child(w, "WardThread_" + i), shell = Child(w, "WardShell_" + i);
                Assert.That(thread.widthMultiplier, Is.EqualTo(w.Tuning.ThreadWidth), "the thread at its width, as before");
                Assert.That(shell.widthMultiplier, Is.EqualTo(w.Tuning.ShellWidth).Or.EqualTo(w.Tuning.ShellHitWidth), "the ring at its width, as before");
                Vector3 from = thread.GetPosition(0), want = Flat(w.transform.position) + Vector3.up * w.Tuning.ThreadHeight;
                Assert.That(Vector3.Distance(from, want), Is.LessThan(0.6f), "the thread from its chest, as before");
            }
            Assert.That(sounds.HumOn || sounds.HumStarts > 0, Is.False, "no hum");
            Assert.That(EnemyWarder.HummingWardsOn, Is.EqualTo(0));
            Assert.That(EnemyWarder.ReaderWarding(a.Body), Is.Null, "no warder that reads wards it");

            yield return ShootAt(a);
            yield return null;
            List<FeedbackShown> numbers = feedback.Numbers.Where(n => n.Target == a.Body).ToList();
            Assert.That(numbers.Count, Is.EqualTo(1), "her hit's number");
            Assert.That(numbers[0].Warded, Is.False, "no mark on it");
            Assert.That(feedback.WardHintShowing, Is.False, "no line");
            Assert.That(feedback.WardHintsShown, Is.EqualTo(0));

            w.Body.TakeHit(w.Body.CurrentHealth * 10);
            yield return Frames(3);
            Assert.That(a.IsDazed && b.IsDazed, Is.True, "the wards broke as before, both dazed");
            Assert.That(sounds.WardBreaksHeard, Is.EqualTo(0), "no break heard");
            Assert.That(_requested.Any(r => r.Sound == PlaceholderSound.WardBreaks || r.Sound == PlaceholderSound.WardHum), Is.False);
        }

        // ---------------- Its own body ----------------

        [UnityTest]
        public IEnumerator On_ItsOwnBody_AStaffTallerThanItWithTheLightOnTop_AHalo_GreenGlows_TheThreadFromTheLight_AndOffLiveHidesIt()
        {
            var caster = Make<EnemyCaster>(EnemyKind.Caster, At(3f, 6f));
            caster.Tuning.Enemy.AggroRange = 0.5f;
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack();
            Assert.That(w.Tuning.Reads, Is.True, "on by default");
            WarderBody body = w.ReadBody;
            Assert.That(body, Is.Not.Null, "made as it wakes");
            Assert.That(body.Shown, Is.True);
            yield return null;
            WarderReadValues r = w.Tuning.Reading;
            Assert.That(body.Staff, Is.Not.Null);
            Assert.That(body.StaffRenderer.enabled && body.OrbRenderer.enabled, Is.True, "the staff and its light drawn");
            float top = body.Orb.position.y + r.OrbRadius - w.transform.position.y;
            Assert.That(top, Is.EqualTo(r.StaffTop).Within(0.05f), "its light's top where its values put it");
            Assert.That(top, Is.GreaterThan(w.ModelHeight + 0.4f), "over its own head");
            Assert.That(Flat(body.Orb.position, w.transform.position), Is.EqualTo(new Vector2(r.StaffSide, r.StaffAhead).magnitude).Within(0.05f), "at its side");
            Assert.That(body.Halo, Is.Not.Null);
            Assert.That(body.Halo.enabled, Is.True, "its halo");
            Assert.That(body.Halo.transform.TransformPoint(body.Halo.GetPosition(0)).y - w.transform.position.y, Is.EqualTo(w.ModelHeight + r.HaloOver).Within(0.05f), "over its head");
            Assert.That(body.GlowsMadeGreen, Is.GreaterThan(0), "its glows green");
            Assert.That(Glows(w), Is.Not.EqualTo(Glows(caster)), "not the caster's cyan");
            Color glow = Glows(w)[0];
            Assert.That(glow.g, Is.GreaterThan(glow.b), "green over blue");
            Debug.Log(string.Format(s_Inv, "WARDERREADSBODY staff top {0:0.00} m (its head {1:0.00} m), at its side {2:0.00} m, halo r {3:0.00} m, {4} glows green ({5}), the caster's {6}",
                                    top, w.ModelHeight, Flat(body.Orb.position, w.transform.position), r.HaloRadius, body.GlowsMadeGreen, glow, Glows(caster)[0]));

            yield return TwoWardsOn(w);
            yield return null;
            foreach (int i in new[] { 0, 1 })
            {
                LineRenderer thread = Child(w, "WardThread_" + i), shell = Child(w, "WardShell_" + i);
                Assert.That(Vector3.Distance(thread.GetPosition(0), body.LightPoint), Is.LessThan(0.3f), "the thread from its light");
                Assert.That(thread.widthMultiplier, Is.GreaterThanOrEqualTo(r.ThreadWidth - 1e-4f), "the thread wider");
                Assert.That(thread.widthMultiplier, Is.GreaterThan(2f * w.Tuning.ThreadWidth));
                Assert.That(shell.widthMultiplier, Is.GreaterThan(2f * w.Tuning.ShellWidth), "the ring wider");
            }
            // It works: its light swells while a ward is on.
            float most = 1f;
            float until = Time.time + r.PulseSeconds * 1.2f;
            while (Time.time < until)
            {
                most = Mathf.Max(most, body.Swell);
                yield return null;
            }
            Assert.That(most, Is.GreaterThan(1f + r.PulseDepth * 0.5f), "its light pulses while it wards");

            // Turned off live: hidden, the caster's glows back; on again: shown.
            List<Color> green = Glows(w);
            w.Tuning.Reads = false;
            yield return Frames(2);
            Assert.That(body.Shown, Is.False);
            Assert.That(body.StaffRenderer.enabled || body.OrbRenderer.enabled || body.Halo.enabled, Is.False, "nothing of it drawn");
            Assert.That(Glows(w), Is.EqualTo(Glows(caster)), "its own glows back");
            Assert.That(Child(w, "WardThread_0").widthMultiplier, Is.EqualTo(w.Tuning.ThreadWidth), "the thread as before");
            w.Tuning.Reads = true;
            yield return Frames(2);
            Assert.That(body.Shown && body.StaffRenderer.enabled && body.Halo.enabled, Is.True, "back");
            Assert.That(Glows(w), Is.EqualTo(green));

            // Dead, none of it.
            w.Body.TakeHit(w.Body.CurrentHealth * 10);
            yield return Frames(2);
            Assert.That(body.Shown, Is.False, "dead: none of it");
            Assert.That(body.OrbRenderer.enabled, Is.False);
        }

        [UnityTest]
        public IEnumerator AWarderWokenOnAnObjectThatIsOff_WakesWithoutAWord_AndHasItsBody()
        {
            // Its switch and its model are read as it wakes: woken late (on an object switched on after it was put together), it
            // must say nothing in the log (the test framework fails on an error) and come up whole.
            EnemyWarder w = MakeWarderTuned(At(8f, 0f), null);
            yield return null;
            Assert.That(w.isActiveAndEnabled, Is.True);
            Assert.That(w.Look, Is.Not.Null, "it wears its model");
            Assert.That(w.ReadBody, Is.Not.Null, "its body");
            Assert.That(w.ReadBody.Shown, Is.True);
            yield return Frames(3);
            Assert.That(w.ReadBody.Shown, Is.True, "still, frames later");
        }

        // ---------------- The mark on a warded hit's number, and the line once a run ----------------

        [UnityTest]
        public IEnumerator AWardedHitsNumber_CarriesTheMark_AnUnwardedOnesDoesNot_AndTheLineShowsOnce()
        {
            TimingFeedback feedback = TimingFeedback.Ensure();
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, EnemyGuard guard) = Pack();
            yield return TwoWardsOn(w);
            Assert.That(EnemyWarder.ReaderWarding(a.Body), Is.SameAs(w));
            Assert.That(feedback.WardHintsShown, Is.EqualTo(0));

            yield return ShootAt(a);
            yield return null;
            FeedbackShown na = feedback.Numbers.Single(n => n.Target == a.Body);
            Assert.That(na.Warded, Is.True, "the warded hit's number carries the mark");
            Assert.That(na.Kind, Is.EqualTo(NumberKind.Plain), "an ordinary hit, drawn green with the shield");
            Assert.That(feedback.WardHintShowing, Is.True, "the line shows");
            Assert.That(feedback.WardHintTarget, Is.SameAs(a.Body), "by the target");
            Assert.That(feedback.WardHintsShown, Is.EqualTo(1));
            Debug.Log(string.Format(s_Inv, "WARDERREADSMARK number {0} over the warded swarmer, warded {1}; the line \"{2}\" showing {3}", na.Sum, na.Warded, WarderReadRules.HintText, feedback.WardHintShowing));

            yield return For(0.7f);
            yield return ShootAt(guard);
            yield return null;
            FeedbackShown ng = feedback.Numbers.Single(n => n.Target == guard.Body);
            Assert.That(ng.Warded, Is.False, "an unwarded hit's number as before");

            yield return For(0.5f);
            yield return ShootAt(b);
            yield return null;
            Assert.That(feedback.Numbers.Single(n => n.Target == b.Body).Warded, Is.True, "the other warded one's number marked too");
            Assert.That(feedback.WardHintsShown, Is.EqualTo(1), "the line not again this run");
            yield return For(w.Tuning.Reading.HintSeconds + 0.2f);
            Assert.That(feedback.WardHintShowing, Is.False, "gone after its time");
            guard.Body.TakeHit(guard.Body.CurrentHealth * 10); // the guard, awake and big, came between her and them
            yield return Frames(2);
            yield return ShootAt(a);
            Assert.That(feedback.WardHintsShown, Is.EqualTo(1), "never again that run");
        }

        // ---------------- The ward heard ----------------

        [UnityTest]
        public IEnumerator TheHum_StartsWithTheFirstWard_GoesOnWhileOneStands_StopsWithTheLast_AndAFallingWardIsHeard()
        {
            PlaceholderSounds sounds = PlaceholderSounds.Ensure();
            (EnemyWarder w, EnemySwarmer a, EnemySwarmer b, _) = Pack();
            yield return Frames(2);
            Assert.That(sounds.HumOn, Is.False, "no ward yet: no hum");
            yield return Until(() => w.Keeper.OnCount >= 1, 6f);
            yield return Frames(2);
            Assert.That(sounds.HumOn, Is.True, "the first ward on: the hum");
            Assert.That(sounds.HumStarts, Is.EqualTo(1));
            Assert.That(_requested.Count(r => r.Sound == PlaceholderSound.WardHum), Is.EqualTo(1), "asked once");
            yield return TwoWardsOn(w);
            yield return Frames(2);
            Assert.That(sounds.HumStarts, Is.EqualTo(1), "the second ward starts nothing more");
            Assert.That(sounds.HumOn, Is.True);

            // One ward goes with its ally: silent, and the hum goes on.
            a.Body.TakeHit(a.Body.CurrentHealth * 10);
            yield return Frames(3);
            Assert.That(w.Keeper.OnCount, Is.EqualTo(1));
            Assert.That(sounds.HumOn, Is.True, "a ward stands: the hum goes on");
            Assert.That(sounds.WardBreaksHeard, Is.EqualTo(0), "a ward whose ally died is not heard falling");

            // The last falls with its warder: heard, and the hum stops.
            w.Body.TakeHit(w.Body.CurrentHealth * 10);
            yield return Frames(3);
            Assert.That(w.Keeper.OnCount, Is.EqualTo(0));
            Assert.That(sounds.HumOn, Is.False, "the last gone: the hum stops");
            Assert.That(sounds.HumStops, Is.EqualTo(1));
            Assert.That(sounds.HumPlaying, Is.False, "its voice stopped");
            Assert.That(sounds.WardBreaksHeard, Is.EqualTo(1), "the ward that fell is heard");
            Assert.That(_requested.Count(r => r.Sound == PlaceholderSound.WardBreaks), Is.EqualTo(1));
            Assert.That(b.IsDazed, Is.True);
        }

        // ---------------- What a frame costs ----------------

        static object s_junk;

        [UnityTest]
        public IEnumerator AFrameWhileAWardStands_AllocatesNothing()
        {
            PlaceholderSounds sounds = PlaceholderSounds.Ensure();
            TimingFeedback feedback = TimingFeedback.Ensure();
            (EnemyWarder w, EnemySwarmer a, _, _) = Pack();
            yield return TwoWardsOn(w);
            yield return ShootAt(a); // the line up, the number with its mark
            yield return For(0.8f); // the number gone, the line still showing
            Assert.That(sounds.HumOn, Is.True);
            Assert.That(feedback.WardHintShowing, Is.True);
            void Frame()
            {
                w.DrawWardsNow();
                w.ShowBodyNow();
                sounds.Tick();
                feedback.Tick();
            }
            for (int i = 0; i < 10; i++) Frame();
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            Assert.That(() => { for (int i = 0; i < 300; i++) Frame(); }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not),
                        "a frame while a ward stands allocates nothing");
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    if (EnemyWarder.HummingWardsOn < 0 || EnemyWarder.ReaderWarding(a.Body) == null) s_junk = null;
                }
            }, AllocExt.AllocatingGCMemory(NUnit.Framework.Is.Not), "asking who wards whom allocates nothing");
        }
    }
}
