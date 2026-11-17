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
        public bool MoveHeld;           // right mouse button, held
        public bool AttackMovePressed;  // A
        public bool ConfirmPressed;     // left mouse button
        public bool CancelPressed;      // Esc
        public bool DashPressed;        // Space
        public bool SkillPressed;       // Q
    }

    /// <summary>
    /// Turns one <see cref="InputFrame"/> per frame into intents on the <see cref="PlayerCharacter"/>.
    /// The frame normally comes from the mouse and keyboard; tests set <see cref="InputSource"/> to feed their own.
    ///
    /// Right-click (held) moves and follows the cursor. A starts aiming an attack-move and shows the range ring;
    /// left-click confirms it (on a target: attack that target; on the ground: attack-move there),
    /// right-click or Esc cancels it. Space dashes toward the cursor. Q casts at the cursor.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        static readonly Plane s_Ground = new Plane(Vector3.up, Vector3.zero);

        public RangeIndicator RangeRing;

        /// <summary>Where frames come from. Null means the mouse and keyboard. Return null for "no input this frame".</summary>
        public Func<InputFrame?> InputSource;

        PlayerCharacter _player;
        Camera _camera;

        public bool IsAimingAttackMove { get; private set; }

        void Awake() => _player = GetComponent<PlayerCharacter>();

        void Update()
        {
            InputFrame? read = InputSource != null ? InputSource() : ReadDevices();
            if (read == null) return;
            InputFrame input = read.Value;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            if (!TryGroundPoint(input.CursorScreen, out Vector3 cursor)) return;

            if (input.DashPressed) _player.DashToward(cursor);
            if (input.SkillPressed) _player.CastAt(cursor);

            if (input.AttackMovePressed) SetAiming(true);
            if (IsAimingAttackMove && input.CancelPressed) SetAiming(false);
            if (IsAimingAttackMove && input.ConfirmPressed)
            {
                Damageable target = TargetUnder(cursor);
                if (target != null) _player.AttackTarget(target);
                else _player.AttackMoveTo(cursor);
                SetAiming(false);
            }

            if (input.MoveHeld)
            {
                SetAiming(false);
                _player.MoveTo(cursor);
            }
        }

        static InputFrame? ReadDevices()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;
            if (mouse == null || keyboard == null) return null;

            return new InputFrame
            {
                CursorScreen = mouse.position.ReadValue(),
                MoveHeld = mouse.rightButton.isPressed,
                AttackMovePressed = keyboard.aKey.wasPressedThisFrame,
                ConfirmPressed = mouse.leftButton.wasPressedThisFrame,
                CancelPressed = keyboard.escapeKey.wasPressedThisFrame,
                DashPressed = keyboard.spaceKey.wasPressedThisFrame,
                SkillPressed = keyboard.qKey.wasPressedThisFrame,
            };
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
