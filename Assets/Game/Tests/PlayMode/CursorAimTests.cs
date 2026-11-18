using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The basic shot goes where the cursor points (decision 0002): a straight line toward the cursor's ground point,
    /// hitting the first living target it touches, or missing. A click never moves her. Right click only moves.
    /// And while she is free she looks toward the cursor. Values come from the settings asset in the scene.
    /// The tests move the dummies where they need them; the scene is loaded fresh for every test.
    /// </summary>
    public class CursorAimTests
    {
        PlayerCharacter _player;
        PlayerInputDriver _driver;
        CharacterAnimation _view;
        FeelValues _feel;
        Damageable _a, _b, _c;
        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

        float Cycle => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;

            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _b = GameObject.Find("Dummy_B").GetComponent<Damageable>();
            _c = GameObject.Find("Dummy_C").GetComponent<Damageable>();
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null; // the real mouse and keyboard must not steer a test
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The line ----------------

        [UnityTest]
        public IEnumerator AShot_HitsTheNearestTargetOnTheLine_NotOneBehindIt()
        {
            yield return Standing(Vector3.zero);
            Place(_a, 0f, 4f);
            Place(_b, 0f, 7f);
            Place(_c, 8f, -8f);
            yield return null;

            _player.Shoot(_b.transform.position); // aimed at the farther one, through the nearer
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            ShotFx streak = NewestStreak();
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the nearer target on the line is hit");
            Assert.That(_b.HitsTaken, Is.EqualTo(0), "the one behind it is not: the shot does not pierce");
            Assert.That(streak, Is.Not.Null);
            Assert.That(FlatDistance(streak.End, _a.transform.position), Is.LessThan(0.05f), "the streak ends at the target it hit");
        }

        [UnityTest]
        public IEnumerator ATargetJustInsideTheHalfWidth_IsHit_AndOneJustOutside_IsMissed()
        {
            yield return Standing(Vector3.zero);
            float touch = _feel.BasicShotHalfWidth + _a.Radius;
            Place(_a, touch - 0.05f, 5f);
            Place(_b, -6f, -6f);
            Place(_c, 8f, -8f);
            yield return null;
            _player.Shoot(new Vector3(0f, 0f, 9f));
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return new WaitForSeconds(Cycle + 0.05f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "its edge just inside the line's half-width: hit");

            Place(_a, touch + 0.05f, 5f);
            yield return null;
            _player.Shoot(new Vector3(0f, 0f, 9f));
            yield return Until(() => _player.BasicShotsFired >= 2, 2f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "just outside: missed");
            Assert.That(_player.BasicShotHits, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator BeyondTheRange_IsAMiss()
        {
            yield return Standing(Vector3.zero);
            Place(_a, 0f, _feel.BasicShotRange + _feel.BasicShotHalfWidth + _a.Radius + 0.3f);
            Place(_b, -6f, -6f);
            Place(_c, 8f, -8f);
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(0), "the target lies beyond the range");
        }

        [UnityTest]
        public IEnumerator AMiss_StillFires_ShowsItsStreakToFullRange_AndCostsTheCycle()
        {
            yield return Standing(Vector3.zero);
            Vector3 open = new Vector3(0f, 0f, -20f); // open floor behind her start
            float firstAt = -1f, secondAt = -1f;
            _player.Shoot(open);
            float deadline = Time.realtimeSinceStartup + 3f;
            ShotFx streak = null;
            while (secondAt < 0f && Time.realtimeSinceStartup < deadline)
            {
                int before = _player.BasicShotsFired;
                yield return null;
                if (_player.BasicShotsFired > before)
                {
                    if (firstAt < 0f)
                    {
                        firstAt = Time.time;
                        streak = NewestStreak();
                        _player.Shoot(open); // at once: it must wait for the cycle the miss cost
                    }
                    else secondAt = Time.time;
                }
            }
            Assert.That(firstAt, Is.GreaterThan(0f), "the miss went off");
            Assert.That(_player.BasicShotHits, Is.EqualTo(0), "and hit nothing");
            Assert.That(streak, Is.Not.Null, "it shows its streak");
            float reach = FlatDistance(streak.End, _player.Position);
            Assert.That(reach, Is.EqualTo(_feel.BasicShotRange).Within(0.1f), "out to the full range");
            Vector3 dir = Flat(streak.End - _player.Position).normalized;
            Assert.That(Vector3.Dot(dir, Vector3.back), Is.GreaterThan(0.999f), "toward the cursor");
            Assert.That(secondAt - firstAt, Is.GreaterThanOrEqualTo(Cycle - 0.02f), "and the next shot waited out the miss's cycle");
        }

        [UnityTest]
        public IEnumerator TheAim_FollowsTheCursorMovedDuringTheStartup()
        {
            yield return Standing(Vector3.zero);
            Place(_a, 3f, 3f);
            Place(_b, -6f, -6f);
            Place(_c, 8f, -8f);
            yield return null;
            _player.Shoot(new Vector3(-5f, 0f, 5f)); // the click: open floor to the other side
            yield return null;
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "still in the startup");
            _player.AimAt(_a.transform.position); // the cursor moves onto A before the shot goes off
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the shot went where the cursor was when it fired, not where it was clicked");
        }

        [UnityTest]
        public IEnumerator AClick_DoesNotMoveAStandingCharacter()
        {
            yield return Standing(new Vector3(2f, 0f, 1f));
            Vector3 stood = _player.Position;
            _player.Shoot(new Vector3(2f, 0f, 16f)); // on the floor, beyond the range: she could walk there, and must not
            yield return new WaitForSeconds(Cycle * 3f);
            Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "it fires");
            Assert.That(FlatDistance(_player.Position, stood), Is.LessThan(0.02f), "from where she stands: no walking into range");
            Assert.That(_player.Order, Is.EqualTo(OrderKind.None));
        }

        // ---------------- Input, fed frames ----------------

        [UnityTest]
        public IEnumerator Input_LeftClicksAtDifferentCursorPoints_ShootInThoseDirections()
        {
            UseFakeInput();
            yield return Standing(Vector3.zero);
            Place(_a, 4f, 3f);
            Place(_b, -4f, 3f);
            Place(_c, 8f, -8f);
            yield return null;
            yield return null;

            Click(_b.transform.position);
            yield return Until(() => _b.HitsTaken >= 1, 2f);
            NewestStreak();
            Assert.That(_b.HitsTaken, Is.EqualTo(1), "a click on B's side shoots B");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));

            yield return new WaitForSeconds(Cycle);
            Click(new Vector3(4f, 0f, 3f) * 2f);
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            NewestStreak();
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "a click past A, on its line, shoots A");

            yield return new WaitForSeconds(Cycle);
            int shots = _player.BasicShotsFired;
            Click(new Vector3(0f, 0f, -6f));
            yield return Until(() => _player.BasicShotsFired > shots, 2f);
            ShotFx streak = NewestStreak();
            Assert.That(streak, Is.Not.Null);
            Assert.That(Vector3.Dot(Flat(streak.End - _player.Position).normalized, Vector3.back), Is.GreaterThan(0.99f), "a click behind her shoots behind her");
            Assert.That(FlatDistance(_player.Position, Vector3.zero), Is.LessThan(0.05f), "and none of the clicks moved her");
        }

        [UnityTest]
        public IEnumerator Input_RightClickOnAnEnemy_Moves_AndDoesNotShoot()
        {
            UseFakeInput();
            yield return Standing(new Vector3(-6f, 0f, 0f));
            yield return null;
            Vector3 onA = _a.transform.position;
            _frames.Enqueue(new InputFrame { CursorScreen = ScreenOf(onA), MoveHeld = true });
            yield return null;
            yield return null;
            Assert.That(_player.Order, Is.EqualTo(OrderKind.Move), "a right click on a dummy is a move");
            Vector3 from = _player.Position;
            yield return new WaitForSeconds(0.4f);
            Assert.That(FlatDistance(_player.Position, onA), Is.LessThan(FlatDistance(from, onA) - 1f), "she walks toward that point");
            Assert.That(_player.BasicShotsFired, Is.EqualTo(0), "and does not shoot");
            Assert.That(_a.HitsTaken, Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator Input_TheAKey_DoesNothing_AndAHeldLeftButton_IsOnePress()
        {
            Assert.That(GameObject.Find("RangeRing"), Is.Null, "there is no range ring any more");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                yield return Standing(new Vector3(2f, 0f, 1f));
                UseFakeInput();
                Vector2 screen = ScreenOf(new Vector3(2f, 0f, 6f));

                // A, read from a keyboard the way the game reads the real one.
                _idle = new InputFrame { CursorScreen = screen };
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.A));
                // The input system takes the events in at the start of a later frame.
                for (int i = 0; i < 10 && !keyboard.aKey.isPressed; i++) yield return null;
                InputFrame? withA = PlayerInputDriver.FrameFrom(mouse, keyboard);
                Assert.That(keyboard.aKey.isPressed, Is.True, "A is down on the test keyboard");
                Assert.That(withA.HasValue, Is.True);
                InputFrame frame = withA.Value;
                Assert.That(frame.ShootPressed || frame.MoveHeld || frame.DashPressed || frame.IdentityPressed
                            || frame.SkillsPressed != 0 || frame.SkillsHeld != 0, Is.False, "A gives nothing the game acts on");
                _frames.Enqueue(frame);
                yield return null;
                yield return null;
                yield return new WaitForSeconds(Cycle);
                Assert.That(_player.Order, Is.EqualTo(OrderKind.None));
                Assert.That(_player.BasicShotsFired, Is.EqualTo(0));
                Assert.That(_player.HasPendingShot, Is.False);
                Assert.That(_player.Action, Is.EqualTo(ActionKind.None));

                // The left button held over several input updates: one press.
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                int presses = 0;
                for (int i = 0; i < 5; i++)
                {
                    var state = new MouseState { position = screen }.WithButton(MouseButton.Left);
                    InputSystem.QueueStateEvent(mouse, state);
                    yield return null;
                    InputFrame? held = PlayerInputDriver.FrameFrom(mouse, keyboard);
                    if (held.HasValue && held.Value.ShootPressed) presses++;
                    if (held.HasValue) _frames.Enqueue(held.Value);
                }
                yield return new WaitForSeconds(Cycle * 2f);
                Assert.That(presses, Is.EqualTo(1), "holding the left button is one press");
                Assert.That(_player.BasicShotsFired, Is.EqualTo(1), "and one shot");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        // ---------------- Looking toward the cursor ----------------

        [UnityTest]
        public IEnumerator Look_StandingWithTheCursorToTheSide_TurnsTheHead_NotTheBodyNorThePistols()
        {
            CharacterAnimation view = RiggedView();
            Assume.That(view.Head != null && view.RightWeapon != null && view.LeftWeapon != null, Is.True);
            yield return Standing(Vector3.zero);
            Vector3 ahead = _player.Position + Flat(_player.transform.forward) * 5f;
            yield return Aim(ahead, 0.5f);
            Assert.That(Mathf.Abs(view.Look), Is.LessThan(3f), "with the cursor ahead she looks ahead");

            // The idle clip moves the pistols a little, so compare them at the same moments of the clip: a whole
            // cycle of samples looking ahead, then samples looking to the side, each matched to the nearest moment.
            Vector3 headAhead = HeadForward(view);
            Quaternion model = view.Animator.transform.rotation, body = _player.transform.rotation;
            var aheadSamples = new List<(float phase, Vector3 right, Vector3 left)>();
            float cycle = view.Animator.GetCurrentAnimatorStateInfo(0).length;
            float until = Time.time + cycle + 0.1f;
            while (Time.time < until)
            {
                _player.AimAt(ahead);
                yield return null;
                aheadSamples.Add((IdlePhase(view), view.RightWeapon.position, view.LeftWeapon.position));
            }

            Vector3 side = _player.Position + Quaternion.AngleAxis(45f, Vector3.up) * Flat(_player.transform.forward) * 5f;
            yield return Aim(side, 0.6f);
            float turned = Vector3.SignedAngle(Flat(headAhead), Flat(HeadForward(view)), Vector3.up);
            Assert.That(turned, Is.GreaterThan(25f), "the head turned toward the cursor 45 degrees to the right");
            Assert.That(view.Look, Is.EqualTo(45f).Within(5f));
            Assert.That(Quaternion.Angle(_player.transform.rotation, body), Is.LessThan(0.5f), "the character has not turned");
            Assert.That(Quaternion.Angle(view.Animator.transform.rotation, model), Is.LessThan(0.5f), "nor has the model");

            float worst = 0f;
            int compared = 0;
            until = Time.time + 0.5f;
            while (Time.time < until)
            {
                _player.AimAt(side);
                yield return null;
                float phase = IdlePhase(view);
                (float phase, Vector3 right, Vector3 left) best = aheadSamples[0];
                foreach (var sample in aheadSamples) if (Mathf.Abs(sample.phase - phase) < Mathf.Abs(best.phase - phase)) best = sample;
                if (Mathf.Abs(best.phase - phase) > 0.003f) continue;
                compared++;
                worst = Mathf.Max(worst, Vector3.Distance(best.right, view.RightWeapon.position), Vector3.Distance(best.left, view.LeftWeapon.position));
            }
            Assume.That(compared, Is.GreaterThan(3), "enough moments to compare");
            Assert.That(worst, Is.LessThan(0.01f), "both pistols stay where the clip has them");
            Assert.That(view.AimWeight, Is.EqualTo(0f), "the arms stay down");
        }

        [UnityTest]
        public IEnumerator Look_WithTheCursorStraightBehind_TheHeadIsAtTheLimit_AndDoesNotFlip()
        {
            CharacterAnimation view = RiggedView();
            yield return Standing(Vector3.zero);
            Vector3 back = -Flat(_player.transform.forward);
            Vector3 leftOfBehind = _player.Position + Quaternion.AngleAxis(-170f, Vector3.up) * Flat(_player.transform.forward) * 5f;
            yield return Aim(_player.Position + Quaternion.AngleAxis(-100f, Vector3.up) * Flat(_player.transform.forward) * 5f, 0.4f);
            yield return Aim(leftOfBehind, 0.6f);
            Assert.That(view.Look, Is.EqualTo(-view.LookLimit).Within(2f), "behind and a little to the left: at the limit on the left");

            yield return Aim(_player.Position + Quaternion.AngleAxis(-3f, Vector3.up) * back * 5f, 0.4f);
            Assert.That(view.Look, Is.EqualTo(-view.LookLimit).Within(2f), "straight behind, a hair to the right: it holds the left");
            yield return Aim(_player.Position + Quaternion.AngleAxis(3f, Vector3.up) * back * 5f, 0.4f);
            Assert.That(view.Look, Is.EqualTo(-view.LookLimit).Within(2f), "a hair to the left again: still the left");

            yield return Aim(_player.Position + Quaternion.AngleAxis(120f, Vector3.up) * Flat(_player.transform.forward) * 5f, 0.6f);
            Assert.That(view.Look, Is.EqualTo(view.LookLimit).Within(2f), "clearly on the right: it goes to the right");
        }

        [UnityTest]
        public IEnumerator Look_IsOff_DuringASkillLock_AndADash()
        {
            CharacterAnimation view = RiggedView();
            const int Fan = 2; // a holding skill: locked for as long as the key is down
            yield return Standing(Vector3.zero);
            Vector3 side = _player.Position + Quaternion.AngleAxis(60f, Vector3.up) * Flat(_player.transform.forward) * 5f;
            yield return Aim(side, 0.6f);
            Assume.That(view.Look, Is.GreaterThan(40f), "looking to the side");

            Assert.That(_player.PressSkill(Fan, _player.Position + Flat(_player.transform.forward) * 5f), Is.EqualTo(IntentResult.Started));
            float deadline = Time.time + 0.5f;
            while (Time.time < deadline)
            {
                _player.AimAt(side);
                yield return null;
            }
            Assume.That(_player.IsLocked, Is.True, "still in the skill");
            Assert.That(Mathf.Abs(view.Look), Is.LessThan(5f), "no look while a skill locks her");
            _player.ReleaseSkill(Fan);
            yield return Until(() => !_player.IsLocked, 3f);
            // The held skill turned her toward the cursor; look 60 degrees from where she faces now.
            side = _player.Position + Quaternion.AngleAxis(60f, Vector3.up) * Flat(_player.transform.forward) * 5f;
            yield return Aim(side, 0.6f);
            Assume.That(view.Look, Is.GreaterThan(30f), "looking again once free");

            float before = view.Look;
            Assert.That(_player.DashToward(_player.Position + Vector3.left * 6f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 1f);
            Assert.That(Mathf.Abs(view.Look), Is.LessThan(before * 0.5f), "and it lets go during a dash");
        }

        [UnityTest]
        public IEnumerator Look_AShotFiredWhileLooking_StillGoesToTheCursor()
        {
            RiggedView();
            yield return Standing(Vector3.zero);
            Place(_a, 3.5f, 3.5f);
            Place(_b, -6f, -6f);
            Place(_c, 8f, -8f);
            yield return Aim(_a.transform.position, 0.5f);
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.BasicShotsFired >= 1, 2f);
            ShotFx streak = NewestStreak();
            yield return new WaitForSeconds(0.1f);
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "the shot goes to the cursor");
            Assert.That(FlatDistance(streak.End, _a.transform.position), Is.LessThan(0.05f));
        }

        // ---------------- Helpers ----------------

        IEnumerator Standing(Vector3 at)
        {
            _player.Teleport(at);
            yield return null;
            yield return null;
        }

        /// <summary>Reports the cursor at a point every frame for a while, as the input driver does.</summary>
        IEnumerator Aim(Vector3 point, float seconds)
        {
            float until = Time.time + seconds;
            float deadline = Time.realtimeSinceStartup + seconds + 2f;
            while (Time.time < until && Time.realtimeSinceStartup < deadline)
            {
                _player.AimAt(point);
                yield return null;
            }
        }

        static float IdlePhase(CharacterAnimation view) => view.Animator.GetCurrentAnimatorStateInfo(0).normalizedTime % 1f;

        /// <summary>Waits, reporting the cursor, until the idle clip is back at a phase (within half a frame or so).</summary>
        IEnumerator UntilPhase(CharacterAnimation view, float phase, Vector3 cursor)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            float last = IdlePhase(view);
            while (Time.realtimeSinceStartup < deadline)
            {
                _player.AimAt(cursor);
                yield return null;
                float now = IdlePhase(view);
                bool crossed = last <= now ? (last < phase && phase <= now) : (phase > last || phase <= now);
                last = now;
                if (crossed) yield break;
            }
        }

        static Vector3 HeadForward(CharacterAnimation view) => view.Head.TransformDirection(s_headLocal ?? (s_headLocal = HeadLocal(view)).Value);
        static Vector3? s_headLocal;
        static Vector3 HeadLocal(CharacterAnimation view) => view.Head.InverseTransformDirection(Flat(view.Player.transform.forward).normalized);

        static Vector3 PistolPosition(Transform weapon) => weapon.position;

        CharacterAnimation RiggedView()
        {
            if (_view == null) Assert.Ignore("the scene was built without a rigged player model");
            s_headLocal = null;
            return _view;
        }

        static void Place(Damageable d, float x, float z)
        {
            Vector3 p = d.transform.position;
            d.transform.position = new Vector3(x, p.y, z);
        }

        readonly HashSet<ShotFx> _seenStreaks = new HashSet<ShotFx>();

        /// <summary>A streak that was not there at the last call: the one the latest shot made.</summary>
        ShotFx NewestStreak()
        {
            ShotFx found = null;
            foreach (ShotFx fx in Object.FindObjectsByType<ShotFx>())
                if (fx.Kind == ShotFxKind.Streak && _seenStreaks.Add(fx)) found = fx;
            return found;
        }

        /// <summary>A left click at a ground point, the cursor then resting there (the shot aims at the live cursor).</summary>
        void Click(Vector3 ground)
        {
            Vector2 screen = ScreenOf(ground);
            _frames.Enqueue(new InputFrame { CursorScreen = screen, ShootPressed = true });
            _idle = new InputFrame { CursorScreen = screen };
        }

        void UseFakeInput() => _driver.InputSource = () => _frames.Count > 0 ? _frames.Dequeue() : _idle;

        static Vector2 ScreenOf(Vector3 world) => Camera.main.WorldToScreenPoint(new Vector3(world.x, 0f, world.z));

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static float FlatDistance(Vector3 a, Vector3 b) => Flat(a - b).magnitude;
    }
}
