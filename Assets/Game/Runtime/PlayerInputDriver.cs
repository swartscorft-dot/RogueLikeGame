using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>What the player did this frame, already reduced to what the game cares about.</summary>
    public struct InputFrame
    {
        public Vector2 CursorScreen;
        /// <summary>The move keys, relative to the screen: x to the right, y up the screen. Each -1, 0 or 1; not yet normalised.</summary>
        public Vector2 Move;
        public bool ShootPressed;       // the shoot button went down: one basic shot toward the cursor
        public bool DashPressed;        // Space
        public bool IdentityPressed;    // Z: spend a full focus gauge on the empowered mode
        /// <summary>Skill bindings that went down this frame. Bit 0 is the first skill slot, bit 1 the second, and so on.</summary>
        public int SkillsPressed;
        /// <summary>Skill bindings that are down this frame, same bits. One that went down this frame counts as down.</summary>
        public int SkillsHeld;
        /// <summary>The cover key is down this frame (Left Ctrl to start with; Kristian 2026-11-19, an experiment).</summary>
        public bool CoverHeld;
    }

    /// <summary>
    /// Turns one <see cref="InputFrame"/> per frame into intents on the <see cref="PlayerCharacter"/>.
    /// The frame normally comes from the mouse and keyboard; tests set <see cref="InputSource"/> to feed their own.
    ///
    /// W A S D move her relative to the screen (decision 0003): up the screen is away from the camera along the
    /// ground, and a diagonal is as fast as straight. The left button fires one basic shot toward the cursor; holding
    /// it never repeats. Space dashes toward the cursor, or the way she moves if the bindings say so. The skill bindings (Q 1 2 3 4 R F and the right mouse button
    /// to start with) are in the <see cref="InputBindings"/> asset: the press, the release and the cursor all reach
    /// the character. Z spends a full focus gauge. Left Ctrl, held, takes cover (Kristian 2026-11-19, an experiment): it
    /// is given every frame, as the move keys are. A dead character takes no input.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    public sealed class PlayerInputDriver : MonoBehaviour
    {
        static readonly Plane s_Ground = new Plane(Vector3.up, Vector3.zero);

        [Tooltip("The keys and buttons. Without it the start bindings are used.")]
        public InputBindings Bindings;

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
        InputBindings _fallback;

        /// <summary>The bindings in use: the asset, or the start values.</summary>
        public InputBindings ActiveBindings
        {
            get
            {
                if (Bindings != null) return Bindings;
                if (_fallback == null) _fallback = ScriptableObject.CreateInstance<InputBindings>();
                return _fallback;
            }
        }

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
                _player.Steer(Vector3.zero);
                _player.HoldCover(false);
                return;
            }

            // Movement first, so a shot this frame knows whether she is moving; the cover key with it.
            _player.Steer(ScreenToGround(input.Move));
            _player.HoldCover(input.CoverHeld);
            _player.AimAt(cursor);
            if (input.IdentityPressed) _player.ActivateIdentity();
            if (input.DashPressed) _player.DashToward(DashPoint(cursor));

            int held = input.SkillsHeld | input.SkillsPressed;
            for (int slot = 0; slot < 32; slot++)
            {
                int bit = 1 << slot;
                if ((input.SkillsPressed & bit) != 0) _player.PressSkill(slot, cursor);
                if ((_skillsHeldBefore & bit) != 0 && (held & bit) == 0) _player.ReleaseSkill(slot);
            }
            _skillsHeldBefore = held;

            if (input.ShootPressed) _player.Shoot(cursor);
        }

        /// <summary>
        /// The point the dash goes toward, as the bindings choose (decision 0004): the cursor, or a dash length along
        /// the held move direction, or along where she faces when no move key is held. Read after the move keys, so it
        /// is this frame's direction.
        /// </summary>
        public Vector3 DashPoint(Vector3 cursor)
        {
            if (ActiveBindings.DashDirection != DashDirection.TowardMovement) return cursor;
            Vector3 dir = _player.MoveInput;
            if (dir.sqrMagnitude < 1e-6f) dir = new Vector3(_player.transform.forward.x, 0f, _player.transform.forward.z);
            if (dir.sqrMagnitude < 1e-6f) return cursor;
            return _player.Position + dir.normalized * Mathf.Max(1f, _player.Feel.DashDistance);
        }

        /// <summary>
        /// The move keys as a direction on the ground: up the screen is the camera's view direction laid flat, right is
        /// the camera's right. Normalised, so a diagonal is no faster than straight; zero with no key.
        /// </summary>
        public Vector3 ScreenToGround(Vector2 move)
        {
            Camera cam = _camera != null ? _camera : Camera.main;
            if (move.sqrMagnitude < 1e-6f || cam == null) return Vector3.zero;
            Vector3 up = cam.transform.forward; up.y = 0f;
            Vector3 right = cam.transform.right; right.y = 0f;
            if (up.sqrMagnitude < 1e-6f) up = Vector3.forward;
            Vector3 dir = up.normalized * move.y + right.normalized * move.x;
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Vector3.zero;
        }

        InputFrame? ReadDevices() => ReadRealDevices ? FrameFrom(Mouse.current, Keyboard.current, ActiveBindings) : null;

        /// <summary>The frame a mouse and a keyboard give now, read through the bindings. Public so tests can read virtual devices the same way.</summary>
        public static InputFrame? FrameFrom(Mouse mouse, Keyboard keyboard, InputBindings bindings)
        {
            if (mouse == null || keyboard == null || bindings == null) return null;
            float Axis(Key positive, Key negative) =>
                (positive != Key.None && keyboard[positive].isPressed ? 1f : 0f) - (negative != Key.None && keyboard[negative].isPressed ? 1f : 0f);
            var frame = new InputFrame
            {
                CursorScreen = mouse.position.ReadValue(),
                Move = new Vector2(Axis(bindings.MoveRight, bindings.MoveLeft), Axis(bindings.MoveUp, bindings.MoveDown)),
                ShootPressed = bindings.Shoot.Control(mouse, keyboard)?.wasPressedThisFrame ?? false,
                DashPressed = bindings.Dash != Key.None && keyboard[bindings.Dash].wasPressedThisFrame,
                IdentityPressed = bindings.Focus != Key.None && keyboard[bindings.Focus].wasPressedThisFrame,
                CoverHeld = bindings.TakeCover != Key.None && keyboard[bindings.TakeCover].isPressed,
            };
            InputBinding[] skills = bindings.Skills ?? Array.Empty<InputBinding>();
            for (int slot = 0; slot < skills.Length && slot < 32; slot++)
            {
                var control = skills[slot].Control(mouse, keyboard);
                if (control == null) continue;
                if (control.wasPressedThisFrame) frame.SkillsPressed |= 1 << slot;
                if (control.isPressed) frame.SkillsHeld |= 1 << slot;
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
