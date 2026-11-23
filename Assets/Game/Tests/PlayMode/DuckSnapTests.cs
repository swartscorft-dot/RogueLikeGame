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
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// A snappier held duck (work order 092; Kristian 2026-11-23: "funker mye bedre, skulle gjerne vært mer snappy", and his answer:
    /// getting down, and down again after a shot, were what felt slow). In the ControlRoom scene, driven through the input driver's
    /// frames at a fixed 60 frames a second of game time (<see cref="Time.captureDeltaTime"/>), so frames and seconds are those of the
    /// game at 60 fps:
    /// - the measurement, with the values before (DuckHoldWait 0.2, down again after the shot's recovery, the pose's blend 0.08 s) and
    ///   after, written to the test's output: the press to the rule down and to the pose down; her own shot with the key held to down
    ///   again (rule and pose); the release to a shot started; the reach;
    /// - an arrow that meets her in the very frame of the press is stopped (the input runs first in the frame);
    /// - a press while she cannot duck (a skill) is kept, the key held, and she goes down the moment she is free.
    /// </summary>
    public class DuckSnapTests
    {
        const float Frame = 1f / 60f;
        static readonly Vector3 Stand = Vector3.zero;

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        CharacterAnimation _look;
        FeelValues _feel;
        GeneratedLevels _levels;
        ThingsThatFly _fly;
        Vector3 _ahead, _side;
        InputFrame _input;
        bool _takeCoverWas, _quickDuckWas, _duckHoldWas, _downAgainWas, _shotLeftWas, _keptWas;
        float _waitWas, _blendWas;
        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            Time.captureDeltaTime = 0f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _driver = _player.GetComponent<PlayerInputDriver>();
            _input = default;
            _driver.InputSource = () => _input;
            _health = _player.GetComponent<PlayerHealth>();
            _health.MaxHealth = 100000;
            _health.RestoreFull();
            _look = Object.FindAnyObjectByType<CharacterAnimation>();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _quickDuckWas = _feel.QuickDuck;
            _duckHoldWas = _feel.DuckHold;
            _downAgainWas = _feel.DuckHoldDownAgain;
            _shotLeftWas = _feel.DuckDownWhenShotLeft;
            _keptWas = _feel.DuckPressKeptWhenBusy;
            _waitWas = _feel.DuckHoldWait;
            _blendWas = _look != null ? _look.DuckBlend : 0f;
            _feel.TakeCover = _feel.QuickDuck = _feel.DuckHold = _feel.DuckHoldDownAgain = true;

            var brute = Object.FindAnyObjectByType<EnemyBrute>();
            Assert.That(brute, Is.Not.Null);
            brute.enabled = false;
            Vector3 away = Flat(Stand - brute.transform.position);
            Vector3[] axes = { Vector3.forward, Vector3.right, Vector3.back, Vector3.left };
            _ahead = axes.OrderByDescending(a => Vector3.Dot(a, away.normalized)).First();
            _side = Vector3.Cross(Vector3.up, _ahead);
            int k = 0;
            foreach (string name in new[] { "Dummy_A", "Dummy_B", "Dummy_C", "Dummy_D", "Dummy_E", "Dummy_F" })
            {
                GameObject d = GameObject.Find(name);
                if (d == null) continue;
                Vector3 to = Stand - _ahead * 13f + _side * (k++ * 3f - 7.5f);
                d.transform.position = new Vector3(to.x, d.transform.position.y, to.z);
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
            Time.captureDeltaTime = 0f;
            if (_player != null) _player.HoldCover(false);
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas;
                _feel.QuickDuck = _quickDuckWas;
                _feel.DuckHold = _duckHoldWas;
                _feel.DuckHoldDownAgain = _downAgainWas;
                _feel.DuckDownWhenShotLeft = _shotLeftWas;
                _feel.DuckPressKeptWhenBusy = _keptWas;
                _feel.DuckHoldWait = _waitWas;
            }
            if (_look != null) _look.DuckBlend = _blendWas;
            foreach (Object o in _made) if (o != null) Object.Destroy(o);
            _made.Clear();
            if (_levels != null)
            {
                _levels.ClearSpawned();
                Object.Destroy(_levels.gameObject);
            }
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The measurement ----------------

        struct Measured
        {
            public int PressToRule, PressToPose, ShotToRule, ShotToPose, ReleaseToShot;
        }

        [UnityTest]
        public IEnumerator Measure_TheHeldDuck_BeforeAndAfter_InFramesAt60()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return null;
            Time.captureDeltaTime = Frame;

            // Before: the values of main fa61ce7.
            _feel.DuckHoldWait = 0.2f;
            _feel.DuckDownWhenShotLeft = false;
            _feel.DuckPressKeptWhenBusy = false;
            if (_look != null) _look.DuckBlend = 0.08f;
            var before = new Measured();
            yield return MeasureOnce(m => before = m);
            // After: this order's.
            _feel.DuckHoldWait = _waitWas;
            _feel.DuckDownWhenShotLeft = true;
            _feel.DuckPressKeptWhenBusy = true;
            if (_look != null) _look.DuckBlend = _blendWas;
            var after = new Measured();
            yield return MeasureOnce(m => after = m);
            Time.captureDeltaTime = 0f;

            string S(int frames) => frames + " frames (" + (frames * Frame).ToString("0.000", CultureInfo.InvariantCulture) + " s)";
            TestContext.WriteLine("DUCKSNAP at 60 fps, before (0.2 s wait, down again after the shot's recovery, pose blend 0.08 s) -> after (0.05 s, when the shot has left, 0.033 s):");
            TestContext.WriteLine("  press to the rule down (an arrow stopped): " + S(before.PressToRule) + " -> " + S(after.PressToRule));
            TestContext.WriteLine("  press to the pose all the way down: " + S(before.PressToPose) + " -> " + S(after.PressToPose));
            TestContext.WriteLine("  her shot with the key held to the rule down again: " + S(before.ShotToRule) + " -> " + S(after.ShotToRule));
            TestContext.WriteLine("  her shot with the key held to the pose down again: " + S(before.ShotToPose) + " -> " + S(after.ShotToPose));
            TestContext.WriteLine("  the key up to a shot started: " + S(before.ReleaseToShot) + " -> " + S(after.ReleaseToShot));
            TestContext.WriteLine("  the reach: " + _feel.DuckReach.ToString("0.0#", CultureInfo.InvariantCulture) + " m from her middle to the prop's edge");

            Assert.That(after.PressToRule, Is.EqualTo(0), "down in the frame of the press");
            Assert.That(after.ReleaseToShot, Is.EqualTo(0), "the shot starts in the frame of the release");
            Assert.That(after.ShotToRule, Is.LessThan(before.ShotToRule), "down again sooner after her shot");
            Assert.That(after.ShotToRule * Frame, Is.LessThanOrEqualTo(_feel.BasicShotStartup + _feel.BasicShotActive + 2f * Frame), "as soon as the shot has left");
            if (_look != null) Assert.That(after.PressToPose, Is.LessThan(before.PressToPose), "the pose follows sooner");
        }

        IEnumerator MeasureOnce(System.Action<Measured> done)
        {
            var m = new Measured();
            _player.Teleport(Stand);
            _input = default;
            yield return Frames(20); // up, the wait over, the pose up
            Vector3 aim = Stand + _side * 5f;

            // The press: frames from the frame the driver gives it (the next) to the rule down, and to the pose all the way down. The
            // coroutine runs after the frame's Updates and before its LateUpdates: the pose seen now is the last frame's.
            _input.CoverHeld = true;
            _input.CursorScreen = Screen(aim);
            int press = Time.frameCount + 1;
            m.PressToRule = m.PressToPose = -1;
            for (int f = 0; f < 30 && (m.PressToRule < 0 || m.PressToPose < 0); f++)
            {
                yield return null;
                if (m.PressToRule < 0 && _player.IsQuickDucking) m.PressToRule = Time.frameCount - press;
                if (m.PressToPose < 0 && (_look == null || _look.DuckWeight >= 1f)) m.PressToPose = Time.frameCount - 1 - press;
            }
            Assert.That(_player.IsQuickDucking, Is.True, "down");
            yield return Frames(10);

            // Her shot with the key held: frames to down again (the rule, and the pose all the way down).
            _input.ShootPressed = true;
            yield return null;
            int shot = Time.frameCount;
            _input.ShootPressed = false;
            Assert.That(_player.IsQuickDucking, Is.False, "up for the shot");
            m.ShotToRule = m.ShotToPose = -1;
            for (int f = 0; f < 60 && (m.ShotToRule < 0 || m.ShotToPose < 0); f++)
            {
                yield return null;
                if (m.ShotToRule < 0 && _player.IsQuickDucking) m.ShotToRule = Time.frameCount - shot;
                if (m.ShotToRule >= 0 && m.ShotToPose < 0 && (_look == null || _look.DuckWeight >= 1f)) m.ShotToPose = Time.frameCount - 1 - shot;
            }
            Assert.That(m.ShotToRule, Is.GreaterThan(0), "down again, the key held");
            yield return Frames(30);

            // The release, and a click in the same frame: the shot starts in that frame.
            int shots = _player.ActionsStarted;
            _input.CoverHeld = false;
            _input.ShootPressed = true;
            yield return null;
            _input.ShootPressed = false;
            m.ReleaseToShot = _player.ActionsStarted > shots ? 0 : 1;
            for (int f = 0; f < 30 && _player.ActionsStarted == shots; f++)
            {
                yield return null;
                m.ReleaseToShot++;
            }
            yield return Frames(30);
            done(m);
        }

        // ---------------- An arrow in the frame of the press ----------------

        [UnityTest]
        public IEnumerator AnArrow_ThatMeetsHerInTheFrameOfThePress_IsStopped()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            EnemyShooter shooter = Shooter(Stand + _ahead * 9f);
            yield return null;
            Time.captureDeltaTime = Frame;
            var ends = new List<(StrikeOutcome outcome, int frame)>();
            shooter.Reported += r => { if (r.Kind == EnemyReportKind.Struck) ends.Add((r.Outcome, Time.frameCount)); };

            // The control: an arrow from beyond the prop, no press: the frame it meets her, counted from the frame it was loosed.
            int loosed = Loose(shooter);
            yield return Until(() => ends.Count > 0, 120);
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(ends[0].outcome, Is.EqualTo(StrikeOutcome.Landed), "standing, it meets her");
            int meets = ends[0].frame - loosed;
            ends.Clear();
            yield return Frames(5);

            // The same arrow, the key pressed through the driver so that it goes down in that frame.
            loosed = Loose(shooter);
            for (int f = 1; f < meets; f++) yield return null;
            _input.CoverHeld = true; // read by the driver at the top of the next frame: the frame the arrow meets her
            yield return Until(() => ends.Count > 0, 120);
            Time.captureDeltaTime = 0f;
            Assert.That(ends.Count, Is.EqualTo(1));
            Assert.That(_player.DuckDownAt, Is.GreaterThan(0f));
            Assert.That(ends[0].frame - loosed, Is.EqualTo(meets), "it ended in the frame it would have met her");
            Assert.That(ends[0].outcome, Is.EqualTo(StrikeOutcome.StoppedByCover), "pressed in that very frame: stopped");
            TestContext.WriteLine("DUCKSNAP the arrow met her " + meets + " frames after it was loosed; pressed in that frame, it was " + ends[0].outcome);
        }

        // ---------------- A press while she cannot duck ----------------

        [UnityTest]
        public IEnumerator APress_WhileSheCannotDuck_IsKept_AndPutsHerDownTheMomentSheCan()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _ahead, 0.5f));
            yield return null;
            int slot = -1;
            for (int s = 0; s < _player.Skills.Count && slot < 0; s++)
                if (_player.IsEquipped(s) && _player.SkillCooldownRemaining(s) <= 0f && _player.Skills[s].Input == SkillInput.Normal
                    && _player.Skills[s].Lock != SkillLock.Light && _player.Skills[s].RecastCount <= 1 && _player.Skills[s].MoveDistance == 0f)
                    slot = s;
            Assume.That(slot, Is.GreaterThanOrEqualTo(0), "a committed skill ready");
            Assert.That(_player.PressSkill(slot, Stand + _side * 5f), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(slot);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
            _input.CoverHeld = true; // pressed through the driver in the next frame, in the skill
            yield return null;
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill), "still in the skill at the press");
            Assert.That(_player.IsQuickDucking, Is.False, "in the skill: not yet");
            float until = Time.realtimeSinceStartup + 4f;
            while (_player.Action == ActionKind.Skill && Time.realtimeSinceStartup < until) yield return null;
            yield return null;
            yield return null;
            Assert.That(_player.IsQuickDucking, Is.True, "the press was kept: down once the skill let her go, the key held");

            _feel.DuckPressKeptWhenBusy = false;
            _input.CoverHeld = false;
            yield return Frames(10);
            Assert.That(_player.SkillCooldownRemaining(slot), Is.GreaterThan(0f));
            int other = -1;
            for (int s = 0; s < _player.Skills.Count && other < 0; s++)
                if (s != slot && _player.IsEquipped(s) && _player.SkillCooldownRemaining(s) <= 0f && _player.Skills[s].Input == SkillInput.Normal
                    && _player.Skills[s].Lock != SkillLock.Light && _player.Skills[s].RecastCount <= 1 && _player.Skills[s].MoveDistance == 0f)
                    other = s;
            if (other < 0) yield break; // no second skill to show the switch off with
            Assert.That(_player.PressSkill(other, Stand + _side * 5f), Is.Not.EqualTo(IntentResult.Rejected)); // it may wait out the first's recovery
            _player.ReleaseSkill(other);
            until = Time.realtimeSinceStartup + 4f;
            while (_player.Action != ActionKind.Skill && Time.realtimeSinceStartup < until) yield return null;
            Assert.That(_player.Action, Is.EqualTo(ActionKind.Skill));
            _input.CoverHeld = true;
            yield return null;
            until = Time.realtimeSinceStartup + 4f;
            while (_player.Action == ActionKind.Skill && Time.realtimeSinceStartup < until) yield return null;
            yield return Frames(3);
            Assert.That(_player.IsQuickDucking, Is.False, "the switch off: the press was lost, as before");
        }

        // ---------------- Helpers ----------------

        int Loose(EnemyShooter shooter)
        {
            Vector3 from = Stand + _ahead * 2.6f;
            var bow = new GroundPoint(from.x, from.z);
            Projectile p = ProjectileRules.Arrow(shooter.Tuning, bow, new GroundPoint(-_ahead.x, -_ahead.z));
            EnemyAttackDefinition arrow = shooter.Values.Attacks.FirstOrDefault(x => x != null && x.Name == ShooterValues.Arrow)
                                          ?? shooter.Values.Attacks.FirstOrDefault(x => x != null && x.Damage > 0);
            Assert.That(arrow, Is.Not.Null, "its arrow");
            _fly.Fire(shooter, arrow, p);
            return Time.frameCount;
        }

        EnemyShooter Shooter(Vector3 at)
        {
            _levels = GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(made.Count, Is.EqualTo(1));
            var e = (EnemyShooter)made[0];
            e.Values.MoveSpeed = 0f;
            Assert.That(e.GetComponent<NavMeshAgent>().Warp(at), Is.True);
            e.enabled = false; // it looses nothing of its own
            return e;
        }

        Vector2 Screen(Vector3 world)
        {
            Camera cam = Camera.main;
            Vector3 s = cam != null ? cam.WorldToScreenPoint(world) : Vector3.zero;
            return new Vector2(s.x, s.y);
        }

        Vector3 FaceAt(PropModel model, Vector3 way, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            return Stand + way * (face + half);
        }

        /// <summary>A prop as a level builds it (as <see cref="QuickDuckHeldTests"/> builds one).</summary>
        GameObject Prop(PropModel model, Vector3 at)
        {
            Vector3 toHer = Flat(Stand - at);
            int rotation = toHer.sqrMagnitude > 1e-6f ? (Mathf.RoundToInt(Quaternion.LookRotation(toHer).eulerAngles.y / 90f) * 90) % 360 : 0;
            var values = new PropValues();
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(values)) go.AddComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, values);
            if (!prop.Slender) go.AddComponent<Shelter>().LowProp = PropHeights.Low(model);
            _made.Add(go);
            Physics.SyncTransforms();
            return go;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Until(System.Func<bool> condition, int frames)
        {
            for (int i = 0; i < frames && !condition(); i++) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
