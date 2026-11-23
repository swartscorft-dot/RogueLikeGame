using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The held duck (work order 086; Kristian's answer of 2026-11-22: "vil at den skal være hold for duck. Så kan man gjøre det så
    /// lenge man vil"; <see cref="HeldDuckRules"/>), in the ControlRoom scene through the intents (<see cref="PlayerCharacter.HoldCover"/>,
    /// <see cref="PlayerCharacter.Shoot"/>, <see cref="PlayerCharacter.PressSkill"/>): the key held beside a low prop keeps her down
    /// far past the tap's 0.5 s and the move keys do nothing; let go she is up in that call; the short wait; a click brings her up
    /// and the shot goes off at once, and with the key still held she is down again after it; a ready skill brings her up, one on
    /// its cooldown does not; held five seconds behind a prop it stops every arrow that crosses it and no orb; a key held through a
    /// teleport, or held while she walks up to a prop, is no press; the pause menu's line and the help line say "hold"; the
    /// recorder's seconds down; nothing allocates while she is held down.
    ///
    /// The practice room has nothing to duck behind: each test builds its props as a level builds them (as
    /// <see cref="QuickDuckTests"/> does). Enemies are spawned as the practice room spawns them and cannot walk.
    /// </summary>
    public class QuickDuckHeldTests
    {
        static readonly Vector3 Stand = Vector3.zero;

        PlayerCharacter _player;
        PlayerHealth _health;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas, _quickDuckWas, _duckHoldWas, _downAgainWas;
        Vector3 _ahead, _side;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        PlaySessionRecorder _recorder;
        RunHud _hud;
        string _folder;
        readonly List<Object> _made = new List<Object>();
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly List<ThingsThatFly.Shown> _shown = new List<ThingsThatFly.Shown>();

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
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _quickDuckWas = _feel.QuickDuck;
            _duckHoldWas = _feel.DuckHold;
            _downAgainWas = _feel.DuckHoldDownAgain;
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
            _feel.QuickDuck = true;
            _feel.DuckHold = true;
            _feel.DuckHoldDownAgain = true;
            _reports.Clear();
            _player.Reported += r => _reports.Add(r);
            _folder = Path.Combine(Path.GetTempPath(), "QuickDuckHeldTests-" + Guid.NewGuid().ToString("N"));

            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null, "the practice enemy");
            brute.enabled = false;
            Vector3 away = Flat(Stand - brute.transform.position);
            Vector3[] axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            _ahead = axes.OrderByDescending(a => Vector3.Dot(a, away.normalized)).First();
            _side = Vector3.Cross(Vector3.up, _ahead);
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d != null) Put(d.transform, Stand - _ahead * 13f + _side * (k++ * 3f - 7.5f));
            }
            _fly = ThingsThatFly.Ensure();
            _fly.Values.On = true;
            _player.Teleport(Stand);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_player != null) _player.HoldCover(false);
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
                _feel.QuickDuck = _quickDuckWas;
                _feel.DuckHold = _duckHoldWas;
                _feel.DuckHoldDownAgain = _downAgainWas;
            }
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null)
            {
                _levels.ClearSpawned();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            if (TimingFeedback.Current != null) Object.Destroy(TimingFeedback.Current.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Down while held ----------------

        [UnityTest]
        public IEnumerator HeldDown_SheStaysDownLongPastTheTapsTime_TheMoveKeysDoNothing_AndLetGoSheIsUpAtOnce_ThenTheShortWait()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            Vector3 at = _player.Position;
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True, "the key went down beside a low prop: down");
            Assert.That(_player.DuckUpAt, Is.EqualTo(float.PositiveInfinity), "no longest time");
            float down = _player.DuckDownAt;
            yield return GameTime(0.3f);
            _player.Steer(_side); // the move keys while she is held down
            float worst = 0f;
            Time.timeScale = 4f;
            float until = Time.time + 3f;
            while (Time.time < until)
            {
                _player.HoldCover(true);
                yield return null;
                worst = Mathf.Max(worst, FlatDistance(_player.Position, at));
                Assert.That(_player.IsQuickDucking, Is.True, "still down at " + (Time.time - down).ToString("0.00") + " s, the key held");
            }
            Time.timeScale = 1f;
            Assert.That(Time.time - down, Is.GreaterThan(_feel.DuckSeconds * 4f), "far past the tap's time");
            Assert.That(worst, Is.LessThan(0.01f), "the move keys did not move her: she stays at the prop");
            Assert.That(_player.IsMoving, Is.False);
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.DuckEnded), Is.EqualTo(0), "it never ended");

            _player.HoldCover(false);
            Assert.That(_player.IsQuickDucking, Is.False, "let go: up in that very call");
            PlayerReport ended = _reports.Last(r => r.Kind == PlayerReportKind.DuckEnded);
            Assert.That(ended.DuckEnd, Is.EqualTo(DuckEnd.Released));
            Assert.That(ended.Seconds, Is.EqualTo(Time.time - down).Within(1e-3f), "how long she was down");
            Assert.That(_player.DuckWaitLeft, Is.EqualTo(_feel.DuckHoldWait).Within(1e-3f), "the short wait, 0.2 s");
            yield return Until(() => _player.Speed > 0.5f, 1f);
            Assert.That(_player.Speed, Is.GreaterThan(0.5f), "up, the move key held moves her at once");
            _player.Stop();
            _player.Teleport(at);
            yield return null;
            Assert.That(_player.DuckWaitLeft, Is.EqualTo(0f), "a teleport clears the wait");
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True);
            _player.HoldCover(false);
            float rose = Time.time;
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.False, "pressed again at once: the wait");
            _player.HoldCover(false);
            yield return Until(() => Time.time >= rose + _feel.DuckHoldWait, 1f);
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True, "0.2 s after: down again");
            Assert.That(_player.DucksTaken, Is.EqualTo(3));
            // A press in the wait, the key then held on: down as the wait ends.
            _player.HoldCover(false);
            rose = Time.time;
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.False, "in the wait");
            float deadline = Time.realtimeSinceStartup + 2f;
            while (!_player.IsQuickDucking && Time.realtimeSinceStartup < deadline)
            {
                _player.HoldCover(true);
                yield return null;
            }
            Assert.That(_player.IsQuickDucking, Is.True, "the press in the wait was kept: down when it ended");
            Assert.That(_player.DuckDownAt - rose, Is.GreaterThanOrEqualTo(_feel.DuckHoldWait - 1e-3f).And.LessThan(_feel.DuckHoldWait + 0.1f));
        }

        // ---------------- A shot or a skill while down ----------------

        [UnityTest]
        public IEnumerator HeldDown_AClickBringsHerUpAndTheShotGoesOffAtOnce_AndWithTheKeyStillHeld_SheIsDownAgainAfterIt()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True);
            yield return GameTime(1f);
            _player.Shoot(Stand + _side * 5f);
            Assert.That(_player.IsQuickDucking, Is.False, "the click brings her up");
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "and the shot starts in the same call");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.DuckEnded).DuckEnd, Is.EqualTo(DuckEnd.Acted));
            Assert.That(_player.DuckDownAgainAt, Is.Not.Null, "the key still held: she will go down again at the prop");
            float upAt = Time.time;
            bool wasUpThroughTheShot = true;
            float downAgain = -1f;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (downAgain < 0f && Time.realtimeSinceStartup < deadline)
            {
                _player.HoldCover(true);
                yield return null;
                if (_player.Action != ActionKind.None && _player.IsQuickDucking) wasUpThroughTheShot = false;
                if (_player.IsQuickDucking) downAgain = Time.time;
            }
            Assert.That(_player.BasicShotsFired, Is.GreaterThanOrEqualTo(1), "the shot went off");
            Assert.That(wasUpThroughTheShot, Is.True, "never down while her shot ran");
            Assert.That(downAgain, Is.GreaterThan(0f), "down again, the key held");
            Assert.That(downAgain - upAt, Is.GreaterThanOrEqualTo(_feel.DuckHoldWait - 1e-3f), "not before the wait");
            Assert.That(_player.DucksTaken, Is.EqualTo(2));

            // Let go: up, and nothing owed; held again with no press, she stays up.
            _player.HoldCover(false);
            Assert.That(_player.IsQuickDucking, Is.False);
            Assert.That(_player.DuckDownAgainAt, Is.Null);
        }

        [UnityTest]
        public IEnumerator HeldDown_AReadySkillBringsHerUpAndStarts_OneOnItsCooldownLeavesHerDown()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            int slot = FirstReadySkill();
            Vector3 aim = Stand + _side * 5f;
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True);
            Assert.That(_player.PressSkill(slot, aim), Is.EqualTo(IntentResult.Started), "a ready skill: it starts at once");
            Assert.That(_player.IsQuickDucking, Is.False, "and she is up");
            _player.ReleaseSkill(slot);
            yield return Until(() => _player.ActiveSkillSlot < 0, 3f);
            Assume.That(_player.SkillCooldownRemaining(slot), Is.GreaterThan(0.5f), "its cooldown runs");
            // Down again (the key held), and the same skill pressed on its cooldown: she stays down.
            yield return Until(() => _player.IsQuickDucking, 2f);
            Assert.That(_player.IsQuickDucking, Is.True, "down again after the skill, the key held");
            IntentResult r = _player.PressSkill(slot, aim);
            Assert.That(r, Is.Not.EqualTo(IntentResult.Started));
            Assert.That(_player.IsQuickDucking, Is.True, "a skill on its cooldown does not bring her up");
        }

        // ---------------- What it stops ----------------

        /// <summary>
        /// Two shooters beyond the prop, each with its shot on the way as she goes down; one off to her side whose line does not
        /// cross the prop; a caster beyond it. Held five seconds: the two arrows that cross the prop are stopped (a shooter does
        /// not start another while the prop hides her: it would walk round, and these cannot walk), every arrow from the side and
        /// every orb is not.
        /// </summary>
        [UnityTest]
        public IEnumerator HeldFiveSeconds_TheDuckStopsEveryArrowThatCrossesTheProp_AndNothingElse()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            EnemyShooter far1 = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _ahead * 9f);
            EnemyShooter far2 = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _ahead * 10f + _side * 0.8f);
            EnemyShooter aside = Spawn<EnemyShooter>(EnemyKind.Shooter, Stand + _side * 8f - _ahead * 2f);
            EnemyCaster caster = Spawn<EnemyCaster>(EnemyKind.Caster, Stand + _ahead * 11f - _side * 0.6f);
            var heldFrom = new float[1];
            var across = new List<EnemyReport>();
            var sideways = new List<EnemyReport>();
            var orbs = new List<EnemyReport>();
            void During(Enemy e, List<EnemyReport> into) => e.Reported += r => { if (r.Kind == EnemyReportKind.Struck && heldFrom[0] > 0f) into.Add(r); };
            During(far1, across);
            During(far2, across);
            During(aside, sideways);
            During(caster, orbs);
            foreach (Enemy e in new Enemy[] { far1, far2, aside, caster }) e.Provoke();
            bool OnItsWay(EnemyShooter s) => s.State == EnemyState.Windup || Eta(s, s.Tuning.ArrowSpeed, s.Tuning.ArrowRadius) < 5f;
            yield return Until(() => OnItsWay(far1) && OnItsWay(far2), 8f);
            Assume.That(OnItsWay(far1) && OnItsWay(far2), "both shots beyond the prop on their way");
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True);
            heldFrom[0] = Time.time;
            Time.timeScale = 2f;
            float until = Time.time + 5f;
            while (Time.time < until)
            {
                _player.HoldCover(true);
                yield return null;
                Assert.That(_player.IsQuickDucking, Is.True, "held down all five seconds");
            }
            Time.timeScale = 1f;
            _levels.ClearSpawned();
            Assert.That(across.Count, Is.GreaterThanOrEqualTo(2), "the two shots beyond the prop came: " + across.Count);
            Assert.That(across.Select(s => s.Outcome), Is.All.EqualTo(StrikeOutcome.StoppedByCover), "every arrow that crossed the prop was stopped");
            Assert.That(sideways.Count, Is.GreaterThanOrEqualTo(1), "the shooter at her side kept shooting");
            Assert.That(sideways.Where(s => s.Outcome == StrikeOutcome.StoppedByCover), Is.Empty, "an arrow that does not cross the prop is not stopped");
            Assert.That(orbs.Where(s => s.Outcome == StrikeOutcome.StoppedByCover), Is.Empty, "no orb is: it passes over a low prop");
            Assert.That(_player.ShotsStoppedByDuck, Is.EqualTo(across.Count), "and nothing else was stopped");
            TestContext.WriteLine("HELDDUCK five seconds: across the prop " + across.Count + " (all stopped), from the side " + sideways.Count + ", caster " + orbs.Count
                                  + " (" + string.Join(", ", orbs.Select(o => o.Attack + " " + o.Outcome)) + ")");
        }

        // ---------------- No press, no duck ----------------

        [UnityTest]
        public IEnumerator TheKeyHeldThroughATeleport_OrWhileSheWalksUpToAProp_IsNoPress()
        {
            Vector3 beside = Stand + _side * 6f;
            Prop(PropModel.Sarcophagus, beside + _ahead * (0.5f + (float)PropSize.Of(PropModel.Sarcophagus).Depth * 0.5f));
            yield return Synced();
            _player.HoldCover(true); // held, with nothing in reach
            yield return null;
            Assert.That(_player.IsQuickDucking, Is.False);
            _player.Teleport(beside);
            for (int i = 0; i < 5; i++)
            {
                _player.HoldCover(true);
                yield return null;
            }
            Assert.That(_player.IsQuickDucking, Is.False, "a key held all along is no press");
            // Walked up to it with the key held: still none.
            _player.Teleport(beside - _side * 3f);
            yield return null;
            _player.Steer(_side);
            float until = Time.time + 1.2f;
            while (Time.time < until)
            {
                _player.HoldCover(true);
                yield return null;
                Assert.That(_player.IsQuickDucking, Is.False, "walking up to it with the key held");
            }
            _player.Stop();
            _player.Teleport(beside);
            yield return null;
            _player.HoldCover(false);
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True, "pressed there, it ducks");
        }

        // ---------------- The words ----------------

        [UnityTest]
        public IEnumerator ThePauseMenusKeyLine_AndTheHelpLine_SayHold()
        {
            _hud = RunHud.Ensure();
            yield return null;
            string line = _hud.KeyLines().Single(l => l.StartsWith("Cover"));
            Assert.That(line, Does.Contain("(hold: duck by a low prop)").And.Not.Contain("tap"));
            InputBindings b = _player.GetComponent<PlayerInputDriver>().ActiveBindings;
            Assert.That(b.HelpLine(_player.QuickDuckOn, _player.DuckIsHeld), Does.Contain("hold to duck by a low prop"));
            Assert.That(GreyboxRules.LegendWord(GreyboxPart.LowCover), Does.Contain("hold Ctrl"));
            Assert.That(GreyboxRules.LegendWord(GreyboxPart.LowCover).Length + 2, Is.LessThanOrEqualTo(GreyboxRules.DevBoxLineMost), "the legend's line fits");
            _feel.DuckHold = false;
            Assert.That(_hud.KeyLines().Single(l => l.StartsWith("Cover")), Does.Contain("tap to duck by a low prop"), "off: the tap's words, as before");
        }

        // ---------------- The recorder ----------------

        [UnityTest]
        public IEnumerator TheRecorder_GivesTheSecondsDown_AndTheSummaryItsLines()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            _player.HoldCover(true);
            float down = Time.time;
            Time.timeScale = 4f;
            float until = Time.time + 2f;
            while (Time.time < until)
            {
                _player.HoldCover(true);
                yield return null;
            }
            Time.timeScale = 1f;
            _player.HoldCover(false);
            float held = Time.time - down;
            yield return null;
            _recorder.StopRecording();
            SessionEvent end = _recorder.Events.Last(e => e.Type == SessionEventTypes.DuckEnd);
            Assert.That(end.Result, Is.EqualTo(SessionWords.Released));
            Assert.That(float.Parse(end.ExtraValue("seconds"), System.Globalization.CultureInfo.InvariantCulture), Is.EqualTo(held).Within(0.01f));
            SessionStats stats = SessionSummary.Compute(_recorder.Events);
            Assert.That(stats.Session.DuckSecondsTotal, Is.EqualTo(held).Within(0.01f));
            StringAssert.Contains("seconds down: total, median", File.ReadAllText(_recorder.SummaryPath));
            _recorder = null;
        }

        // ---------------- Its frame ----------------

        [UnityTest]
        public IEnumerator HeldDown_ItsFrameAllocatesNothing()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return Synced();
            _player.HoldCover(true);
            Assert.That(_player.IsQuickDucking, Is.True);
            MethodInfo m = typeof(PlayerCharacter).GetMethod("UpdateDuck", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(m, Is.Not.Null, "the duck's step");
            var step = (Action<FeelValues, float>)Delegate.CreateDelegate(typeof(Action<FeelValues, float>), _player, m);
            FeelValues feel = _player.Feel;
            for (int i = 0; i < 10; i++) { _player.HoldCover(true); step(feel, Time.time); }
            Assert.That(() => { for (int i = 0; i < 300; i++) { _player.HoldCover(true); step(feel, Time.time); } }, AllocExt.AllocatingGCMemory(Is.Not),
                        "a frame of the held duck allocates nothing");
            Assert.That(_player.IsQuickDucking, Is.True);
        }

        // ---------------- Helpers ----------------

        int FirstReadySkill()
        {
            for (int slot = 0; slot < _player.Skills.Count; slot++)
                if (_player.IsEquipped(slot) && _player.SkillCooldownRemaining(slot) <= 0f && _player.Skills[slot].Input == SkillInput.Normal
                    && _player.Skills[slot].RecastCount <= 1 && _player.Skills[slot].MoveDistance == 0f && _player.Skills[slot].Cooldown > 1f)
                    return slot;
            Assert.Fail("no plain skill ready");
            return -1;
        }

        /// <summary>Seconds until the first thing <paramref name="owner"/> loosed that still flies meets her, at its speed (as in <see cref="QuickDuckTests"/>); infinity with none.</summary>
        float Eta(Enemy owner, float speed, float radius)
        {
            _fly.ListFlying(_shown);
            float best = float.PositiveInfinity;
            foreach (ThingsThatFly.Shown s in _shown)
            {
                if (s.Owner != owner || s.Ending || s.Passed) continue;
                float d = FlatDistance(s.Position, _player.Position) - radius - Placeholders.PlayerRadius;
                best = Mathf.Min(best, Mathf.Max(0f, d) / Mathf.Max(0.1f, speed));
            }
            return best;
        }

        T Spawn<T>(EnemyKind kind, Vector3 at) where T : Enemy
        {
            _levels = _levels != null ? _levels : GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(kind);
            Assert.That(made.Count, Is.EqualTo(1), kind + " spawned");
            var e = (T)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);
            return e;
        }

        static List<EnemyReport> Strikes(Enemy e)
        {
            var list = new List<EnemyReport>();
            e.Reported += r => { if (r.Kind == EnemyReportKind.Struck) list.Add(r); };
            return list;
        }

        Vector3 FaceAt(PropModel model, Vector3 way, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            return Stand + way * (face + half);
        }

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>A prop as a level builds it (as <see cref="QuickDuckTests"/> builds one).</summary>
        GameObject Prop(PropModel model, Vector3 at)
        {
            Vector3 toHer = Flat(Stand - at);
            int rotation = toHer.sqrMagnitude > 1e-6f ? (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360 : 0;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        static IEnumerator Synced()
        {
            Physics.SyncTransforms();
            yield return null;
        }

        static IEnumerator GameTime(float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 5f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline) yield return null;
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
