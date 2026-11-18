using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>What the player did this frame, already reduced to what the game cares about.</summary>
    public struct InputFrame
    {
        public Vector2 CursorScreen;
        public bool MoveHeld;           // right mouse button, held: move, or attack the target under the cursor
        public bool AttackMovePressed;  // A
        public bool ConfirmPressed;     // left mouse button
        public bool CancelPressed;      // Esc
        public bool DashPressed;        // Space
        public bool IdentityPressed;    // Z: spend a full focus gauge on the empowered mode
        /// <summary>Skill keys that went down this frame. Bit 0 is the first skill key (Q), bit 1 the second (W), and so on.</summary>
        public int SkillsPressed;
        /// <summary>Skill keys that are down this frame, same bits. A key that went down this frame counts as down.</summary>
        public int SkillsHeld;
    }

    /// <summary>
    /// Turns one <see cref="InputFrame"/> per frame into intents on the <see cref="PlayerCharacter"/>.
    /// The frame normally comes from the mouse and keyboard; tests set <see cref="InputSource"/> to feed their own.
    ///
    /// Right-click (held) on the ground moves and follows the cursor; on a target it attacks that target.
    /// Left-click is an attack-move at the cursor (on a target: attack that target; on the ground: attack-move there,
    /// going for the target nearest the click). A first shows the range ring while aiming it;
    /// left-click then confirms it as above,
    /// and right-click or Esc cancels the aim. Space dashes toward the cursor.
    /// Q W E R and S D F G are the skill keys: the press, the release and the cursor all reach the character.
    /// Z spends a full focus gauge. A dead character takes no input.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        /// <summary>The skill keys, in slot order.</summary>
        public static readonly Key[] SkillKeys = { Key.Q, Key.W, Key.E, Key.R, Key.S, Key.D, Key.F, Key.G };

        static readonly Plane s_Ground = new Plane(Vector3.up, Vector3.zero);

        public RangeIndicator RangeRing;

        /// <summary>
        /// False shuts out the real mouse and keyboard for every driver, from its first frame on.
        /// Tests set it before they load the scene: setting <see cref="InputSource"/> afterwards is one frame too late,
        /// and whatever the person at the computer is doing would steer the test.
        /// </summary>
        public static bool ReadRealDevices = true;

        /// <summary>Where frames come from. Null means the mouse and keyboard. Return null for "no input this frame".</summary>
        public Func<InputFrame?> InputSource;

        PlayerCharacter _player;
        PlayerHealth _health;
        Camera _camera;
        int _skillsHeldBefore;

        public bool IsAimingAttackMove { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => ReadRealDevices = true;

        void Awake()
        {
            _player = GetComponent<PlayerCharacter>();
            _health = GetComponent<PlayerHealth>();
        }

        void Update()
        {
            InputFrame? read = InputSource != null ? InputSource() : ReadDevices();
            if (read == null) return;
            InputFrame input = read.Value;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            if (!TryGroundPoint(input.CursorScreen, out Vector3 cursor)) return;

            if (_health != null && !_health.IsAlive)
            {
                _skillsHeldBefore = 0;
                SetAiming(false);
                return;
            }

            _player.AimAt(cursor);
            if (input.IdentityPressed) _player.ActivateIdentity();
            if (input.DashPressed) _player.DashToward(cursor);

            int held = input.SkillsHeld | input.SkillsPressed;
            for (int slot = 0; slot < SkillKeys.Length; slot++)
            {
                int bit = 1 << slot;
                if ((input.SkillsPressed & bit) != 0) _player.PressSkill(slot, cursor);
                if ((_skillsHeldBefore & bit) != 0 && (held & bit) == 0) _player.ReleaseSkill(slot);
            }
            _skillsHeldBefore = held;

            if (input.AttackMovePressed) SetAiming(true);
            if (IsAimingAttackMove && input.CancelPressed) SetAiming(false);
            // Left-click is the attack-move at the cursor, with or without A first ("smart cast", Kristian 2026-11-18).
            if (input.ConfirmPressed)
            {
                Damageable target = TargetUnder(cursor);
                if (target != null) _player.AttackTarget(target);
                else _player.AttackMoveTo(cursor);
                SetAiming(false);
            }

            if (input.MoveHeld)
            {
                SetAiming(false);
                Damageable target = TargetUnder(cursor);
                if (target != null) _player.AttackTarget(target);
                else _player.MoveTo(cursor);
            }
        }

        static InputFrame? ReadDevices()
        {
            if (!ReadRealDevices) return null;
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return null;

            var frame = new InputFrame
            {
                CursorScreen = mouse.position.ReadValue(),
                MoveHeld = mouse.rightButton.isPressed,
                AttackMovePressed = keyboard.aKey.wasPressedThisFrame,
                ConfirmPressed = mouse.leftButton.wasPressedThisFrame,
                CancelPressed = keyboard.escapeKey.wasPressedThisFrame,
                DashPressed = keyboard.spaceKey.wasPressedThisFrame,
                IdentityPressed = keyboard.zKey.wasPressedThisFrame,
            };
            for (int slot = 0; slot < SkillKeys.Length; slot++)
            {
                var key = keyboard[SkillKeys[slot]];
                if (key.wasPressedThisFrame) frame.SkillsPressed |= 1 << slot;
                if (key.isPressed) frame.SkillsHeld |= 1 << slot;
            }
            return frame;
        }

        void SetAiming(bool aiming)
        {
            IsAimingAttackMove = aiming;
            if (RangeRing != null && RangeRing.IsVisible != aiming) RangeRing.SetVisible(aiming);
        }

        bool TryGroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = _camera.ScreenPointToRay(screen);
            if (s_Ground.Raycast(ray, out float distance))
            {
                point = ray.GetPoint(distance);
                return true;
            }
            point = default;
            return false;
        }

        /// <summary>The living target under the cursor, with a little slack around its edge. Nearest wins.</summary>
        static Damageable TargetUnder(Vector3 cursor)
        {
            var point = new GroundPoint(cursor.x, cursor.z);
            IReadOnlyList<Damageable> all = Damageable.All;
            Damageable best = null;
            float bestGap = float.PositiveInfinity;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!d.IsAlive) continue;
                float gap = Targeting.Gap(point, d.GroundPosition, d.Radius);
                if (gap <= Placeholders.CursorPickSlack && gap < bestGap)
                {
                    best = d;
                    bestGap = gap;
                }
            }
            return best;
        }
    }
}
