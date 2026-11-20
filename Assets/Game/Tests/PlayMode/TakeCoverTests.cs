using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Taking cover on Left Ctrl (Kristian, 2026-11-19, an experiment), in the ControlRoom scene through the intents. The
    /// practice room has nothing to take cover behind, so each test builds its own as a generated level builds it: props
    /// from <see cref="PropFactory"/> and walls as grey cubes on the walls' layer, with <see cref="Cover"/> on the walls and
    /// the tall props and <see cref="Shelter"/> on all of them.
    ///
    /// She stands at the practice room's middle and everything is laid out ahead of her along <c>_ahead</c>, the axis
    /// that points most away from the practice brute (switched off), with the dummies put behind her. A shooter, when a
    /// test needs one, is spawned as the practice room spawns it, cannot walk (so it stays where the test puts it) and is
    /// woken by <see cref="Enemy.Provoke"/>. No assertion counts frames.
    /// </summary>
    public class TakeCoverTests
    {
        static readonly Vector3 Stand = Vector3.zero;

        PlayerCharacter _player;
        PlayerInputDriver _driver;
        PlayerHealth _health;
        FeelValues _feel;
        bool _takeCoverWas, _coverStopsWas, _quickDuckWas;
        Vector3 _ahead, _side;
        GeneratedLevels _levels;
        PlaySessionRecorder _recorder;
        string _folder;
        readonly List<Object> _made = new List<Object>();
        readonly List<PlayerReport> _reports = new List<PlayerReport>();
        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

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
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _feel = _player.Feel;
            _takeCoverWas = _feel.TakeCover;
            _coverStopsWas = _feel.CoverStopsShots;
            _feel.TakeCover = true;
            _feel.CoverStopsShots = true;
            _quickDuckWas = _feel.QuickDuck; _feel.QuickDuck = false; // work order 043: taking cover as it was, the quick duck off
            _reports.Clear();
            _frames.Clear();
            _idle = default;
            _player.Reported += r => _reports.Add(r);
            _folder = Path.Combine(Path.GetTempPath(), "TakeCoverTests-" + Guid.NewGuid().ToString("N"));

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
            _player.Teleport(Stand);
            yield return null;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_feel != null)
            {
                _feel.TakeCover = _takeCoverWas; // the feel values live in an asset
                _feel.CoverStopsShots = _coverStopsWas;
                _feel.QuickDuck = _quickDuckWas;
            }
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
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

        // ---------------- Taking cover ----------------

        [UnityTest]
        public IEnumerator HeldInReach_SheMovesToStandAgainstIt_AndDucks()
        {
            // A sarcophagus whose near face is 1.2 m ahead: in reach.
            GameObject sarcophagus = Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            Assert.That(sarcophagus.GetComponent<Cover>(), Is.Null, "a low prop");
            yield return null;

            float pressed = Time.time;
            _player.HoldCover(true);
            yield return Until(() => _player.IsDucked, 1f);
            float took = Time.time - pressed;

            Assert.That(_player.IsInCover, Is.True, "in cover");
            Assert.That(_player.IsDucked, Is.True, "and ducked");
            Assert.That(_player.CoverObject, Is.SameAs(sarcophagus.GetComponent<Shelter>()));
            Assert.That(_player.CoverIsTall, Is.False);
            Assert.That(took, Is.LessThanOrEqualTo(_feel.CoverSnapTime + 0.05f), "over at most the snap time");
            Assert.That(Along(_player.Position), Is.EqualTo(1.2f - _feel.CoverStandOff).Within(0.03f), "against its face, on her side");
            Assert.That(Across(_player.Position), Is.EqualTo(0f).Within(0.03f));
            Assert.That(_reports.Count(r => r.Kind == PlayerReportKind.CoverEntered), Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HeldOutOfReach_NothingHappens()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, _feel.CoverReach + 0.4f));
            yield return null;
            _player.HoldCover(true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.IsInCover, Is.False);
            Assert.That(FlatDistance(_player.Position, Stand), Is.LessThan(0.02f), "she did not move");
            Assert.That(_player.CoversTaken, Is.EqualTo(0));

            // Walking into reach with the key held takes cover there.
            _player.Steer(_ahead);
            yield return Until(() => _player.IsInCover, 1f);
            Assert.That(_player.IsInCover, Is.True, "in reach, the key still held takes cover");
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator TheKey_ThroughTheInput_TakesAndLeavesCover_AndTheBindingsGiveLeftCtrl()
        {
            InputBindings bindings = _driver.ActiveBindings;
            Assert.That(bindings.TakeCover, Is.EqualTo(Key.LeftCtrl), "the asset does not list it: the code's start value");
            StringAssert.Contains("LeftCtrl hold to take cover", bindings.HelpLine());
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.LeftCtrl));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = ScreenOf(Stand + _side * 3f) });
                for (int i = 0; i < 10 && !keyboard.leftCtrlKey.isPressed; i++) yield return null;
                Assert.That(PlayerInputDriver.FrameFrom(mouse, keyboard, bindings).Value.CoverHeld, Is.True, "Left Ctrl holds the cover key");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }

            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            yield return null;
            _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;
            _idle = new InputFrame { CursorScreen = ScreenOf(Stand + _side * 3f), CoverHeld = true };
            yield return Until(() => _player.IsDucked, 1f);
            Assert.That(_player.IsDucked, Is.True, "held through the input");
            _idle = new InputFrame { CursorScreen = _idle.CursorScreen };
            yield return null;
            yield return null;
            Assert.That(_player.IsInCover, Is.False, "let go through the input");
        }

        [UnityTest]
        public IEnumerator WithTheSwitchOff_TheKeyDoesNothing()
        {
            _feel.TakeCover = false;
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            yield return null;
            _player.HoldCover(true);
            yield return new WaitForSeconds(0.3f);
            Assert.That(_player.IsInCover, Is.False);
            Assert.That(_player.CoversTaken, Is.EqualTo(0));
            Assert.That(FlatDistance(_player.Position, Stand), Is.LessThan(0.02f), "she did not move");
            _player.Steer(_side);
            yield return new WaitForSeconds(0.2f);
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.3f), "the move keys work as always");
            _player.Stop();
        }

        // ---------------- Moving in cover ----------------

        [UnityTest]
        public IEnumerator TheSlide_KeepsHerAgainstTheCover_AtTheCoverShare_AndAMoveAwayDoesNothing()
        {
            Wall(Stand + _ahead * (1.2f + 0.15f), 8f, 0.3f); // its face 1.2 m ahead
            yield return Synced();
            yield return Ducked();
            float against = Along(_player.Position);
            Assert.That(against, Is.EqualTo(1.2f - _feel.CoverStandOff).Within(0.03f));

            Vector3 from = _player.Position;
            float started = Time.time;
            _player.Steer(_side);
            float worst = 0f;
            while (Time.time < started + 0.5f)
            {
                yield return null;
                worst = Mathf.Max(worst, Mathf.Abs(Along(_player.Position) - against));
            }
            float went = Vector3.Dot(Flat(_player.Position - from), _side);
            float expected = _feel.RunSpeed * _feel.CoverMoveShare * (Time.time - started);
            Assert.That(went, Is.EqualTo(expected).Within(0.15f), "along it at the cover share of run speed");
            Assert.That(worst, Is.LessThan(0.02f), "staying against it");
            Assert.That(_player.IsInCover, Is.True);
            Assert.That(_player.IsMoving, Is.False, "in cover she is never moving freely");

            Vector3 at = _player.Position;
            _player.Steer(-_ahead); // straight away from it
            yield return new WaitForSeconds(0.2f);
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.02f), "a move away from the cover does nothing");
            _player.Steer((-_ahead * 3f + _side).normalized); // 72 degrees off the face
            yield return new WaitForSeconds(0.2f);
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.02f), "nor one far off its face");
            _player.Steer((-_ahead + _side).normalized); // 45 degrees off the face, away
            yield return new WaitForSeconds(0.2f);
            Assert.That(Vector3.Dot(Flat(_player.Position - at), _side), Is.GreaterThan(0.2f), "a move 45 degrees away still slides along it");
            Assert.That(Along(_player.Position), Is.EqualTo(against).Within(0.02f));
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator TheSlide_WithARunSpeedUpgrade_IsFasterByIt()
        {
            // Work order 026 (the second review of 2026-11-20): a chest's run speed counts in cover too.
            Wall(Stand + _ahead * (1.2f + 0.15f), 8f, 0.3f);
            yield return Synced();
            yield return Ducked();
            var modifiers = new RunModifiers();
            modifiers.Take(new Upgrade { Kind = UpgradeKind.RunSpeed, Amount = 0.5f });
            _player.Modifiers = modifiers;
            try
            {
                Assume.That(_player.RunSpeedShare, Is.EqualTo(1.5f).Within(1e-4f));
                Vector3 from = _player.Position;
                float started = Time.time;
                _player.Steer(_side);
                while (Time.time < started + 0.4f) yield return null;
                float went = Vector3.Dot(Flat(_player.Position - from), _side);
                float expected = _feel.RunSpeed * _player.RunSpeedShare * _feel.CoverMoveShare * (Time.time - started);
                Assert.That(went, Is.EqualTo(expected).Within(0.15f), "along it at the cover share of her run speed with the upgrade");
                Assert.That(_player.IsInCover, Is.True);
                _player.Stop();
            }
            finally
            {
                _player.Modifiers = null;
            }
        }

        [UnityTest]
        public IEnumerator LettingGo_GivesFullMovementAtOnce()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            yield return null;
            yield return Ducked();
            Vector3 at = _player.Position;
            _player.Steer(-_ahead);
            yield return new WaitForSeconds(0.15f);
            Assume.That(FlatDistance(_player.Position, at), Is.LessThan(0.02f), "held, the move away does nothing");

            _player.HoldCover(false);
            Assert.That(_player.IsInCover, Is.False, "the key up leaves cover at once");
            float released = Time.time;
            // As for the move keys after a skill lock (WasdTests): the agent takes a frame or two to report its speed.
            yield return Until(() => _player.Speed >= _feel.RunSpeed - 0.3f, 1f);
            Assert.That(Time.time - released, Is.LessThan(0.05f), "at run speed at once, with no ease up");
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.3f));
            yield return new WaitForSeconds(0.2f);
            Assert.That(Vector3.Dot(Flat(_player.Position - at), -_ahead), Is.GreaterThan(0.8f), "and away from the cover");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.CoverLeft).Leaving, Is.EqualTo(CoverLeaving.Released));
            _player.Stop();
        }

        [UnityTest]
        public IEnumerator TheDash_LeavesCoverAtOnce_AndWorksAsAlways()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            yield return null;
            yield return Ducked();
            Vector3 at = _player.Position;
            Assert.That(_player.DashToward(at - _ahead * 6f), Is.EqualTo(IntentResult.Started));
            Assert.That(_player.IsInCover, Is.False, "out of cover at the dash");
            Assert.That(_reports.Last(r => r.Kind == PlayerReportKind.CoverLeft).Leaving, Is.EqualTo(CoverLeaving.Dash));
            yield return Until(() => !_player.IsDashing, 1f);
            yield return null;
            Assert.That(FlatDistance(_player.Position, at), Is.GreaterThan(_feel.DashDistance * 0.8f), "the whole dash");
            Assert.That(_player.IsInCover, Is.False, "the key still held, but nothing in reach");
        }

        // ---------------- Shooting from cover ----------------

        [UnityTest]
        public IEnumerator ABasicShot_StandsHerUp_WhereSheIs_AndSheDucksAgainAfterThePeekTime()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            yield return null;
            yield return Ducked();
            Vector3 at = _player.Position;

            _player.Shoot(at + _side * 5f);
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot));
            Assert.That(_player.IsDucked, Is.False, "the shot stands her up");
            Assert.That(_player.IsShootingOnTheMove, Is.False, "a standing shot");
            float worst = 0f;
            float deadline = Time.realtimeSinceStartup + 2f;
            while (_player.Action != ActionKind.None && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
                worst = Mathf.Max(worst, FlatDistance(_player.Position, at));
                Assert.That(_player.IsDucked, Is.False, "up for as long as the shot lasts");
            }
            float ended = Time.time;
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "it went off");
            Assert.That(_player.IsInCover, Is.True);
            Assert.That(_player.IsDucked, Is.False, "still up just after it");
            yield return Until(() => _player.IsDucked, 1f);
            float up = Time.time - ended;
            Assert.That(_player.IsDucked, Is.True, "ducked again, the key still held");
            Assert.That(up, Is.EqualTo(_feel.CoverPeekTime).Within(0.05f), "after the peek time");
            Assert.That(worst, Is.LessThan(0.02f), "she did not leave her place");
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.02f));
        }

        // ---------------- The shooter ----------------

        [UnityTest]
        public IEnumerator DuckedBehindALowProp_TheShooterDoesNotShootHer_AndItsLineStopsAtTheProp_AndTheRecorderLogsIt()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            EnemyShooter shooter = Shooter();
            yield return Synced();
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return Ducked();
            Vector3 her = _player.Position;
            int before = _health.Current;

            Bring(shooter, Stand + _ahead * 9f);
            Assert.That(shooter.Sees(her), Is.True, "it sees over the low prop, as before");
            Assert.That(shooter.SeesHer(), Is.False, "but not her, ducked behind it");
            shooter.Provoke();
            bool woundUp = false;
            float until = Time.time + 2f;
            while (Time.time < until)
            {
                yield return null;
                woundUp |= shooter.State == EnemyState.Windup;
            }
            Assert.That(shooter.IsAwake, Is.True, "it is awake");
            Assert.That(woundUp, Is.False, "and never winds up while she is ducked");
            Assert.That(shooter.Strikes, Is.EqualTo(0));

            // She stands for a shot: it sees her and winds up; she ducks again before its shot comes.
            _player.Shoot(her + _side * 5f);
            yield return Until(() => shooter.State == EnemyState.Windup, 2f);
            Assert.That(shooter.State, Is.EqualTo(EnemyState.Windup), "standing, she is seen");
            yield return Until(() => shooter.Strikes > 0, 3f);
            Assert.That(shooter.Strikes, Is.EqualTo(1));
            Assert.That(_player.IsDucked, Is.True, "ducked again when the shot came");
            Assert.That(shooter.StrikesLanded, Is.EqualTo(0), "the prop stopped it");
            Assert.That(_health.Current, Is.EqualTo(before));
            Assert.That(_player.EnemyShotsStoppedByCover, Is.EqualTo(1));
            float face = 9f - 1.2f; // from the shooter to the prop's face toward it, along the line
            float depth = (float)PropSize.Of(PropModel.Sarcophagus).Depth;
            Assert.That(shooter.LineReach, Is.EqualTo(face - depth).Within(0.1f), "its line ends at the prop");

            _player.HoldCover(false);
            yield return null;
            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Any(e => e.Type == SessionEventTypes.CoverEnter && e.Name == SessionWords.Low), "cover entered, low");
            Assert.That(events.Any(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.StoppedByCover), "the shot it stopped");
            Assert.That(events.Any(e => e.Type == SessionEventTypes.CoverLeave && e.Result == SessionWords.Released), "cover left");
            string summary = File.ReadAllText(_recorder.SummaryPath);
            StringAssert.Contains("Time in cover, share of the time", summary);
            StringAssert.Contains("stopped by the cover she ducked behind", summary);
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Session.StrikesStoppedByCover, Is.EqualTo(1));
            Assert.That(stats.InFights.CoverSeconds, Is.GreaterThan(1f), "time in cover in the fight");
            _recorder = null;
        }

        [UnityTest]
        public IEnumerator TheSameShooter_OnHerSide_HitsHer()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            EnemyShooter shooter = Shooter();
            yield return Synced();
            yield return Ducked();
            int before = _health.Current;
            Bring(shooter, Stand - _ahead * 8f);
            Assert.That(shooter.SeesHer(), Is.True, "from her side the prop is not between them");
            shooter.Provoke();
            yield return Until(() => shooter.Strikes > 0, 4f);
            Assert.That(_player.IsDucked, Is.True, "she stayed ducked");
            Assert.That(shooter.StrikesLanded, Is.EqualTo(1), "and it hit her");
            Assert.That(_health.Current, Is.LessThan(before));
            Assert.That(_player.EnemyShotsStoppedByCover, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator StandingBehindALowProp_ToShoot_SheCanBeHit()
        {
            Prop(PropModel.Sarcophagus, FaceAt(PropModel.Sarcophagus, 1.2f));
            EnemyShooter shooter = Shooter();
            yield return Synced();
            yield return Ducked();
            Vector3 at = _player.Position;
            int before = _health.Current;
            Bring(shooter, Stand + _ahead * 9f);
            shooter.Provoke();
            // She keeps clicking: every shot keeps her up, and the shooter's comes while she stands.
            float until = Time.time + 4f;
            bool everDucked = false;
            float firstClick = Time.time;
            while (shooter.Strikes == 0 && Time.time < until)
            {
                _player.Shoot(at + _side * 5f);
                yield return null;
                if (Time.time > firstClick + 0.05f) everDucked |= _player.IsDucked;
            }
            Assert.That(shooter.Strikes, Is.EqualTo(1), "it shot");
            Assert.That(everDucked, Is.False, "she stood the whole time");
            Assert.That(_player.IsInCover, Is.True, "in cover, up");
            Assert.That(shooter.StrikesLanded, Is.EqualTo(1), "standing, the low prop does not cover her");
            Assert.That(_health.Current, Is.LessThan(before));
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.02f), "and she never left her place");
        }

        // ---------------- Helpers ----------------

        IEnumerator Ducked()
        {
            _player.HoldCover(true);
            yield return Until(() => _player.IsDucked, 1f);
            Assert.That(_player.IsDucked, Is.True, "ducked in cover");
        }

        /// <summary>
        /// A shooter spawned as the practice room spawns it, unable to walk, and kept out of its reach to wake
        /// (<see cref="Bring"/> puts it where the test wants it): in reach it would wake at once and shoot before she is
        /// in cover.
        /// </summary>
        EnemyShooter Shooter()
        {
            _levels = GeneratedLevels.Ensure();
            List<Enemy> made = _levels.SpawnPractice(EnemyKind.Shooter);
            Assert.That(made.Count, Is.EqualTo(1));
            var shooter = (EnemyShooter)made[0];
            shooter.Tuning.Enemy.MoveSpeed = 0f;
            Vector3 parked = Stand + _ahead * 17f;
            Assume.That(17f, Is.GreaterThan(shooter.Values.AggroRange + 1f), "parked out of its reach to wake");
            Assert.That(shooter.GetComponent<NavMeshAgent>().Warp(parked), Is.True, "on the NavMesh at " + parked);
            return shooter;
        }

        static void Bring(EnemyShooter shooter, Vector3 at) =>
            Assert.That(shooter.GetComponent<NavMeshAgent>().Warp(at), Is.True, "on the NavMesh at " + at);

        /// <summary>Where a prop's centre goes for its near face to stand <paramref name="face"/> metres ahead of her, its long side across.</summary>
        Vector3 FaceAt(PropModel model, float face)
        {
            PropSize size = PropSize.Of(model);
            float half = size.Shape == PropShape.Circle ? (float)size.Radius : (float)size.Depth * 0.5f;
            return Stand + _ahead * (face + half);
        }

        float Along(Vector3 p) => Vector3.Dot(Flat(p - Stand), _ahead);
        float Across(Vector3 p) => Vector3.Dot(Flat(p - Stand), _side);

        static void Put(Transform t, Vector3 at) => t.position = new Vector3(at.x, t.position.y, at.z);

        /// <summary>A prop as a level builds it: on the walls' layer, cover when it is tall enough, and something to take cover behind.</summary>
        GameObject Prop(PropModel model, Vector3 at)
        {
            int rotation = Mathf.RoundToInt(Quaternion.LookRotation(_ahead).eulerAngles.y) % 360; // its long side across the line
            LevelProp prop = LevelProp.Make(model, new LevelPoint(at.x, at.z), rotation, PropPlace.Floor);
            GameObject go = PropFactory.Make(prop, Flat(at), null, LayerMask.NameToLayer("Default"), null, "TestProp_" + model, 1f);
            if (prop.IsCover(new PropValues())) go.AddComponent<Cover>();
            go.AddComponent<Shelter>();
            _made.Add(go);
            return go;
        }

        /// <summary>A grey wall as a level builds it, 3 m high, <paramref name="length"/> across the line ahead.</summary>
        GameObject Wall(Vector3 centre, float length, float thickness)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "TestWall";
            wall.transform.SetPositionAndRotation(new Vector3(centre.x, 1.5f, centre.z), Quaternion.LookRotation(_ahead));
            wall.transform.localScale = new Vector3(length, 3f, thickness);
            wall.AddComponent<Cover>();
            wall.AddComponent<Shelter>();
            _made.Add(wall);
            return wall;
        }

        /// <summary>Colliders made this frame are where their transforms are only once physics is synced.</summary>
        static IEnumerator Synced()
        {
            Physics.SyncTransforms();
            yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector2 ScreenOf(Vector3 world) => Camera.main.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
