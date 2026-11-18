using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>What the player did this frame, already reduced to what the game cares about.</summary>
    public struct InputFrame
    {
        public Vector2 CursorScreen;
        public bool MoveHeld;           // right mouse button, held: move, and follow the cursor
        public bool ShootPressed;       // left mouse button went down: one basic shot toward the cursor
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
    /// Right-click moves to the point under the cursor, also when it is on an enemy, and held it follows the cursor.
    /// Left-click fires one basic shot toward the cursor (decision 0002): every shot needs its own click, and holding
    /// the button never repeats it. Space dashes toward the cursor. Q W E R and S D F G are the skill keys: the press,
    /// the release and the cursor all reach the character. Z spends a full focus gauge. A dead character takes no input.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        /// <summary>The skill keys, in slot order.</summary>
        public static readonly Key[] SkillKeys = { Key.Q, Key.W, Key.E, Key.R, Key.S, Key.D, Key.F, Key.G };

        static readonly Plane s_Ground = new Plane(Vector3.up, Vector3.zero);

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
        bool _moveHeldBefore;

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
                _moveHeldBefore = false;
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

            // Left button: one shot toward the cursor per press.
            if (input.ShootPressed) _player.Shoot(cursor);

            // Right button: the press orders the move; held, it steers the move after the cursor.
            bool movePressed = input.MoveHeld && !_moveHeldBefore;
            _moveHeldBefore = input.MoveHeld;
            if (movePressed) _player.MoveTo(cursor);
            else if (input.MoveHeld) _player.SteerTo(cursor);
        }

        static InputFrame? ReadDevices() => ReadRealDevices ? FrameFrom(Mouse.current, Keyboard.current) : null;

        /// <summary>The frame a mouse and a keyboard give now. Public so tests can read virtual devices the same way.</summary>
        public static InputFrame? FrameFrom(Mouse mouse, Keyboard keyboard)
        {
            if (mouse == null || keyboard == null) return null;
            var frame = new InputFrame
            {
                CursorScreen = mouse.position.ReadValue(),
                MoveHeld = mouse.rightButton.isPressed,
                ShootPressed = mouse.leftButton.wasPressedThisFrame,
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
    }
}
