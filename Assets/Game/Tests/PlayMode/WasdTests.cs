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
    /// W A S D movement and the new skill keys (decision 0003). Frames are fed to the input driver, or the character
    /// is steered through its intent; the real mouse and keyboard never are. Some tests read virtual devices the way
    /// the game reads the real ones, through the bindings in the settings asset.
    /// </summary>
    public class WasdTests
    {
        PlayerCharacter _player;
        PlayerInputDriver _driver;
        CharacterAnimation _view;
        FeelValues _feel;
        Damageable _a;
        readonly Queue<InputFrame> _frames = new Queue<InputFrame>();
        InputFrame _idle;

        float Cycle => _feel.BasicShotStartup + _feel.BasicShotActive + _feel.BasicShotRecovery;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _driver = Object.FindAnyObjectByType<PlayerInputDriver>();
            _view = _player.GetComponentInChildren<CharacterAnimation>();
            _feel = _player.Feel;
            _a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            _frames.Clear();
            _idle = default;
            _driver.InputSource = () => null;
        }

        [TearDown]
        public void RestoreTimeAndInput()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Movement ----------------

        [UnityTest]
        public IEnumerator EachMoveKey_MovesHerThatWayOnTheScreen_AtRunSpeed()
        {
            UseFakeInput();
            var keys = new (string name, Vector2 move, Vector2 screen)[]
            {
                ("W", new Vector2(0f, 1f), Vector2.up), ("S", new Vector2(0f, -1f), Vector2.down),
                ("A", new Vector2(-1f, 0f), Vector2.left), ("D", new Vector2(1f, 0f), Vector2.right),
            };
            foreach (var key in keys)
            {
                _player.Teleport(new Vector3(-6f, 0f, -2f));
                yield return null;
                yield return null;
                Vector3 from = _player.Position;
                Vector2 screenFrom = Camera.main.WorldToScreenPoint(from);
                _idle = new InputFrame { CursorScreen = ScreenOf(from + Vector3.forward * 3f), Move = key.move };
                yield return new WaitForSeconds(0.4f);
                Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.3f), key.name + ": at run speed");
                Vector2 screenTo = Camera.main.WorldToScreenPoint(_player.Position);
                Vector2 onScreen = (screenTo - screenFrom).normalized;
                Assert.That(Vector2.Dot(onScreen, key.screen), Is.GreaterThan(0.97f), key.name + ": that way on the screen");
                _idle = new InputFrame { CursorScreen = _idle.CursorScreen };
                yield return null;
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator ADiagonal_IsNoFasterThanStraight()
        {
            UseFakeInput();
            _player.Teleport(new Vector3(-6f, 0f, -4f));
            yield return null;
            Vector3 from = _player.Position;
            _idle = new InputFrame { CursorScreen = ScreenOf(from + Vector3.forward * 3f), Move = new Vector2(1f, 1f) };
            yield return new WaitForSeconds(0.4f);
            Assert.That(_player.Speed, Is.EqualTo(_feel.RunSpeed).Within(0.3f), "W and D together: run speed, not 1.4 times");
            float travelled = Flat(_player.Position - from).magnitude;
            Assert.That(travelled, Is.LessThan(_feel.RunSpeed * 0.45f + 0.2f));
        }

        [UnityTest]
        public IEnumerator LettingGo_StopsHerAtOnce()
        {
            UseFakeInput();
            _player.Teleport(new Vector3(-6f, 0f, -2f));
            yield return null;
            Vector2 cursor = ScreenOf(new Vector3(-6f, 0f, 2f));
            _idle = new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f) };
            yield return new WaitForSeconds(0.3f);
            Assume.That(_player.Speed, Is.GreaterThan(5f));
            _idle = new InputFrame { CursorScreen = cursor };
            yield return null;
            yield return null;
            Vector3 stopped = _player.Position;
            yield return new WaitForSeconds(0.2f);
            Assert.That(FlatDistance(_player.Position, stopped), Is.LessThan(0.05f), "no key: she stands");
            Assert.That(_player.Speed, Is.LessThan(0.1f));
        }

        [UnityTest]
        public IEnumerator PushedAgainstTheEdge_SheSlidesAlongIt_InsteadOfSticking()
        {
            // The floor ends at x = 20. Held toward +X and +Z, she reaches the edge and slides along it toward +Z.
            _player.Teleport(new Vector3(18.5f, 0f, -6f));
            yield return null;
            _player.Steer(new Vector3(1f, 0f, 1f));
            yield return new WaitForSeconds(0.8f);
            Vector3 at = _player.Position;
            yield return new WaitForSeconds(0.5f);
            _player.Stop();
            Assert.That(_player.Position.x, Is.LessThan(20.05f), "she stays on the floor");
            Assert.That(_player.Position.z - at.z, Is.GreaterThan(1f), "and keeps going along the edge");
        }

        [UnityTest]
        public IEnumerator TheDash_GoesTowardTheCursor_WhateverKeyIsHeld()
        {
            // The dash direction is a setting since decision 0004: test the cursor one whatever the asset says.
            InputBindings bindings = Object.Instantiate(_driver.ActiveBindings);
            bindings.DashDirection = DashDirection.TowardCursor;
            _driver.Bindings = bindings;
            UseFakeInput();
            _player.Teleport(new Vector3(0f, 0f, -2f));
            yield return null;
            Vector3 from = _player.Position;
            Vector2 cursor = ScreenOf(from + Vector3.left * 6f);
            _idle = new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f) }; // W held: up the screen
            _frames.Enqueue(new InputFrame { CursorScreen = cursor, Move = new Vector2(0f, 1f), DashPressed = true });
            yield return Until(() => _player.IsDashing, 1f);
            Vector3 dashFrom = _player.Position;
            yield return Until(() => !_player.IsDashing, 1f);
            Vector3 dashed = Flat(_player.Position - dashFrom);
            Assert.That(Vector3.Dot(dashed.normalized, Vector3.left), Is.GreaterThan(0.95f), "the dash went toward the cursor, not up the screen");
            Assert.That(dashed.magnitude, Is.GreaterThan(_feel.DashDistance * 0.7f));
        }

        // ---------------- Keys and buttons ----------------

        [UnityTest]
        public IEnumerator ReadFromDevices_WASDGiveTheMove_RightClickFiresTheLastSlot_AndDoesNotMove()
        {
            InputBindings bindings = _driver.ActiveBindings;
            Assume.That(bindings.Skills.Length, Is.EqualTo(8));
            Assume.That(bindings.Skills[7].Source, Is.EqualTo(BindingSource.RightMouse), "the right button is bound to the eighth slot");
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                _player.Teleport(new Vector3(2f, 0f, 1f));
                yield return null;
                Vector2 screen = ScreenOf(_a.transform.position);

                InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                for (int i = 0; i < 10 && !keyboard.wKey.isPressed; i++) yield return null;
                InputFrame frame = PlayerInputDriver.FrameFrom(mouse, keyboard, bindings).Value;
                Assert.That(frame.Move, Is.EqualTo(new Vector2(1f, 1f)), "W and D: up and to the right");

                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                InputSystem.QueueStateEvent(mouse, new MouseState { position = screen }.WithButton(MouseButton.Right));
                for (int i = 0; i < 10 && !mouse.rightButton.isPressed; i++) yield return null;
                frame = PlayerInputDriver.FrameFrom(mouse, keyboard, bindings).Value;
                Assert.That(frame.Move, Is.EqualTo(Vector2.zero), "the right button gives no movement");
                Assert.That(frame.SkillsPressed, Is.EqualTo(1 << 7), "it presses the eighth slot");

                UseFakeInput();
                Vector3 stood = _player.Position;
                _frames.Enqueue(frame);
                yield return null;
                yield return null;
                Assert.That(_player.ActiveSkillSlot, Is.EqualTo(7), "Triple Tap starts on the right button");
                yield return new WaitForSeconds(0.3f);
                Assert.That(FlatDistance(_player.Position, stood), Is.LessThan(0.05f), "and she did not move");
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator EachOfTheEightBindings_StartsItsOwnSkill_ReadFromTheSettings()
        {
            InputBindings bindings = _driver.ActiveBindings;
            Assume.That(bindings.Skills.Length, Is.EqualTo(_player.Skills.Count));
            var keyboard = InputSystem.AddDevice<Keyboard>();
            var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                for (int slot = 0; slot < bindings.Skills.Length; slot++)
                {
                    _player.Teleport(new Vector3(2f, 0f, 1f));
                    yield return null;
                    Vector2 screen = ScreenOf(_a.transform.position);
                    InputBinding b = bindings.Skills[slot];
                    InputSystem.QueueStateEvent(keyboard, b.Source == BindingSource.Key ? new KeyboardState(b.Key) : new KeyboardState());
                    MouseState ms = new MouseState { position = screen };
                    if (b.Source == BindingSource.RightMouse) ms = ms.WithButton(MouseButton.Right);
                    if (b.Source == BindingSource.LeftMouse) ms = ms.WithButton(MouseButton.Left);
                    InputSystem.QueueStateEvent(mouse, ms);
                    InputFrame frame = default;
                    for (int i = 0; i < 10; i++)
                    {
                        yield return null;
                        frame = PlayerInputDriver.FrameFrom(mouse, keyboard, bindings).Value;
                        if ((frame.SkillsHeld & (1 << slot)) != 0) break;
                    }
                    Assert.That(frame.SkillsHeld, Is.EqualTo(1 << slot), b.Label + " holds slot " + (slot + 1) + " and only it");

                    UseFakeInput();
                    _frames.Enqueue(new InputFrame { CursorScreen = screen, SkillsPressed = 1 << slot, SkillsHeld = 1 << slot });
                    yield return null;
                    yield return null;
                    Assert.That(_player.ActiveSkillSlot, Is.EqualTo(slot), b.Label + " starts " + _player.Skills[slot].Name);
                    _idle = new InputFrame { CursorScreen = screen }; // let go
                    yield return Until(() => _player.Action == ActionKind.None, 4f);
                    InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                    InputSystem.QueueStateEvent(mouse, new MouseState { position = screen });
                    yield return null;
                    _driver.InputSource = () => null;
                }
            }
            finally
            {
                InputSystem.RemoveDevice(keyboard);
                InputSystem.RemoveDevice(mouse);
            }
        }

        [UnityTest]
        public IEnumerator TheSkillBarAndTheHelpLine_ShowTheRealBindings()
        {
            yield return null;
            InputBindings bindings = _driver.ActiveBindings;
            Assert.That(bindings.SkillLabel(0), Is.EqualTo("Q"));
            Assert.That(bindings.SkillLabel(1), Is.EqualTo("1"));
            Assert.That(bindings.SkillLabel(7), Is.EqualTo("RMB"));
            string help = bindings.HelpLine();
            StringAssert.Contains("WASD move", help);
            StringAssert.Contains("Q 1 2 3 4 R F RMB skills", help);
        }

        // ---------------- Movement and actions ----------------

        [UnityTest]
        public IEnumerator AMoveKeyDuringASkillLock_DoesNothing_AndMovesHerWhenTheLockEnds()
        {
            const int Volley = 5; // a cast: locked for a while
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            Assert.That(_player.PressSkill(Volley, _a.transform.position), Is.EqualTo(IntentResult.Started));
            _player.ReleaseSkill(Volley);
            Vector3 at = _player.Position;
            _player.Steer(Vector3.back);
            yield return new WaitForSeconds(0.15f);
            Assume.That(_player.IsLocked, Is.True, "still in the skill");
            Assert.That(FlatDistance(_player.Position, at), Is.LessThan(0.05f), "a move key does nothing while a skill locks her");
            Assert.That(_player.ActiveSkillSlot, Is.EqualTo(Volley), "and does not cancel it");
            yield return Until(() => !_player.IsLocked, 4f);
            float endedAt = Time.time;
            Vector3 freeAt = _player.Position;
            // Decision 0004: she starts at once, and her speed eases up to run speed over the skill speed recovery.
            yield return Until(() => _player.Speed > 0.1f, 1f);
            float movingAfter = Time.time - endedAt;
            yield return Until(() => _player.Speed >= _feel.RunSpeed - 0.05f, 1f);
            float fullAfter = Time.time - endedAt;
            _player.Stop();
            Assert.That(movingAfter, Is.LessThan(0.05f), "the key still held moves her as soon as the lock ends");
            Assert.That(fullAfter, Is.LessThan(_feel.SkillSpeedRecovery + 0.1f), "and she is at run speed after the ease back up");
        }

        [UnityTest]
        public IEnumerator AMoveKeyInAStandingShotsWindUp_DoesNotCancelIt_ItBecomesAShotOnTheMove()
        {
            _player.Teleport(new Vector3(2f, 0f, 1f));
            yield return null;
            _player.Shoot(_a.transform.position);
            yield return Until(() => _player.Action == ActionKind.BasicShot, 1f);
            Assume.That(_player.ActionPhase, Is.EqualTo(ActionPhase.Startup), "in the wind-up");
            Assume.That(_player.IsShootingOnTheMove, Is.False, "a standing shot");
            _player.Steer(Vector3.back);
            yield return null;
            Assert.That(_player.Action, Is.EqualTo(ActionKind.BasicShot), "the shot is not cancelled");
            Assert.That(_player.IsShootingOnTheMove, Is.True, "it is now a shot on the move");
            yield return Until(() => _a.HitsTaken >= 1, 2f);
            _player.Stop();
            Assert.That(_a.HitsTaken, Is.EqualTo(1), "and it goes off");
            Assert.That(_player.ShotsOnTheMove, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator HoldingS_WithTheCursorUpTheScreen_RhythmClicks_BackpedalAtTheCappedSpeed_WithoutFlipping()
        {
            Vector3 up = _driver.ScreenToGround(new Vector2(0f, 1f));
            Vector3 down = -up;
            _player.Teleport(new Vector3(0f, 0f, 6f));
            yield return null;
            _player.Steer(down);
            yield return new WaitForSeconds(0.3f);
            int shotsBefore = _player.BasicShotsFired, clicks = 0;
            float start = Time.time, worst = 180f, fastest = 0f;
            bool settled = false;
            float deadline = Time.realtimeSinceStartup + 6f;
            while (Time.time < start + 2.5f && Time.realtimeSinceStartup < deadline)
            {
                Vector3 aim = _player.Position + up * 8f; // the cursor up the screen, ahead of where she backs away from
                _player.AimAt(aim);
                if (Time.time >= start + clicks * (Cycle + 0.03f)) { _player.Shoot(aim); clicks++; }
                yield return null;
                float facingUp = Vector3.Angle(Flat(_player.transform.forward), up);
                if (!settled && facingUp < 10f && Time.time > start + 0.3f) settled = true;
                if (settled)
                {
                    worst = Mathf.Min(worst, 180f - facingUp);
                    if (Time.time > start + 0.6f) fastest = Mathf.Max(fastest, _player.Speed);
                }
            }
            _player.Stop();
            Assert.That(_player.BasicShotsFired - shotsBefore, Is.GreaterThanOrEqualTo(8), "shooting in rhythm the whole time");
            Assert.That(settled, Is.True, "she turned to face up the screen");
            Assert.That(worst, Is.GreaterThan(165f), "and never swung back toward the way she moves");
            Assert.That(fastest, Is.LessThan(_feel.RunSpeed * _feel.BackpedalSpeedShare + 0.25f), "backpedalling at no more than the capped speed");
            if (_view != null) Assert.That(_view.Pose, Is.EqualTo(CharacterPose.ShootBack), "in the backward jog");
        }

        [UnityTest]
        public IEnumerator MovingSideways_WithTheCursorAhead_TheLegsRunAndTheUpperBodyTwists()
        {
            Vector3 up = _driver.ScreenToGround(new Vector2(0f, 1f));
            Vector3 right = _driver.ScreenToGround(new Vector2(1f, 0f));
            _player.Teleport(new Vector3(-8f, 0f, -4f));
            yield return null;
            _player.Steer(right);
            yield return new WaitForSeconds(0.3f);
            int clicks = 0;
            float start = Time.time, worstTurn = 0f, twist = 0f;
            bool back = false;
            float deadline = Time.realtimeSinceStartup + 4f;
            while (Time.time < start + 1.2f && Time.realtimeSinceStartup < deadline)
            {
                Vector3 aim = _player.Position + up * 8f; // 90 degrees off her run
                _player.AimAt(aim);
                if (Time.time >= start + clicks * (Cycle + 0.03f)) { _player.Shoot(aim); clicks++; }
                yield return null;
                worstTurn = Mathf.Max(worstTurn, Vector3.Angle(Flat(_player.transform.forward), right));
                if (_view != null)
                {
                    if (_view.AimWeight >= 1f) twist = Mathf.Max(twist, Mathf.Abs(_view.Twist));
                    if (_view.Pose == CharacterPose.ShootBack) back = true;
                }
            }
            _player.Stop();
            Assert.That(worstTurn, Is.LessThan(3f), "the body keeps facing the way she runs");
            Assert.That(_player.FacingMode, Is.EqualTo(ShotFacing.Mode.Free));
            if (_view != null)
            {
                Assert.That(twist, Is.GreaterThan(70f), "the upper body is turned toward the aim");
                Assert.That(back, Is.False, "the legs run forward");
            }
        }

        // ---------------- Helpers ----------------

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
