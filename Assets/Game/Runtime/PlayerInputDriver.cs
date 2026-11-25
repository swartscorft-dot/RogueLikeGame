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
        public bool ShootHeld;          // the shoot button is down this frame (work order 026: after a choice it must go up before a shot)
        public bool DashPressed;        // Space
        public bool IdentityPressed;    // Z: spend a full focus gauge on the empowered mode
        /// <summary>
        /// Skill bindings that went down this frame. Bit 0 is the first skill key, bit 1 the second, and so on; the skill
        /// on each key is the character's (work order 015: <see cref="PlayerCharacter.SkillOnKey"/>).
        /// </summary>
        public int SkillsPressed;
        /// <summary>Skill bindings that are down this frame, same bits. One that went down this frame counts as down.</summary>
        public int SkillsHeld;
        /// <summary>The cover key is down this frame (Left Ctrl to start with; Kristian 2026-11-19, an experiment).</summary>
        public bool CoverHeld;
        /// <summary>The sprint key is down this frame (Left Shift to start with; work order 092).</summary>
        public bool SprintHeld;
        /// <summary>
        /// The fourth review of 2026-11-21 (work order 055): the move keys were read one by one (<see cref="MoveKeysRead"/>),
        /// and one of them went down this frame (<see cref="MovePressed"/>), whatever the axis came to: the opposite key
        /// pressed while one is held gives a zero axis, and is a press all the same. A frame that gives only
        /// <see cref="Move"/> (tests, tools) leaves them false, and a press is read from the axis as before.
        /// </summary>
        public bool MoveKeysRead;
        public bool MovePressed;
    }

    /// <summary>
    /// Turns one <see cref="InputFrame"/> per frame into intents on the <see cref="PlayerCharacter"/>.
    /// The frame normally comes from the mouse and keyboard; tests set <see cref="InputSource"/> to feed their own.
    ///
    /// W A S D move her relative to the screen (decision 0003): up the screen is away from the camera along the
    /// ground, and a diagonal is as fast as straight. The left button fires one basic shot toward the cursor; held, it
    /// fires again at the shot's own pace (work order 132, <see cref="HoldToFireInput"/>; off, it never repeats). Space dashes toward the cursor, or the way she moves if the bindings say so. The skill bindings (Q 1 2 3 4 R F and the right mouse button
    /// to start with) are in the <see cref="InputBindings"/> asset: the press, the release and the cursor all reach
    /// the character. Z spends a full focus gauge. Left Ctrl, held, takes cover (Kristian 2026-11-19, an experiment): it
    /// is given every frame, as the move keys are. A dead character takes no input.
    /// </summary>
    [RequireComponent(typeof(PlayerCharacter))]
    // Work order 092 ("å komme seg ned" was slow): before every other Update of the frame, so a press reaches her in the frame it is
    // made, before anything that flies at her takes its step (it ran in no set order before: the same frame or the next).
    [DefaultExecutionOrder(-100)]
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

        /// <summary>
        /// True while a choice is on screen (work order 012: the floor reward, a chest): the game is paused and the keys
        /// and clicks pick from the choice, so none reaches the character.
        /// </summary>
        public static bool HeldByChoice
        {
            get => s_heldByChoice;
            set
            {
                if (s_heldByChoice && !value) s_letGoFrame = Time.frameCount;
                if (!s_heldByChoice && value) s_holds++;
                s_heldByChoice = value;
            }
        }

        /// <summary>
        /// True while the pause menu or a run's end screen is up (work order 018): the game is paused or slowing to a
        /// stop, and no key or click reaches the character.
        /// </summary>
        public static bool HeldByScreen
        {
            get => s_heldByScreen;
            set
            {
                if (s_heldByScreen && !value) s_letGoFrame = Time.frameCount;
                if (!s_heldByScreen && value) s_holds++;
                s_heldByScreen = value;
            }
        }

        /// <summary>
        /// True on the frame a choice or a screen let go of the input (the review of 2026-11-20, work order 026): no
        /// press of that frame reaches her, whichever component's Update runs first, for the key or click that answered
        /// it is a press of that frame too. The interact key (<see cref="GeneratedLevels"/>) asks the same.
        /// </summary>
        public static bool LetGoThisFrame => s_letGoFrame == Time.frameCount;

        static bool s_heldByChoice;
        static bool s_heldByScreen;
        static int s_letGoFrame = -1;
        static int s_holds; // holds begun since the game started: a hold the driver never saw itself still counts

        /// <summary>Where frames come from. Null means the mouse and keyboard. Return null for "no input this frame".</summary>
        public Func<InputFrame?> InputSource;

        PlayerCharacter _player;
        PlayerHealth _health;
        Camera _camera;
        int _skillsHeldBefore;
        int _waitForUp;          // skill keys that must be seen up before they count again (work order 026)
        bool _shootWaitsForUp;   // and the shoot button
        Vector2 _moveBefore;     // the move keys of the last frame read, to tell a key going down (work order 047)
        int _holdsSeen;
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
        static void ResetStatics()
        {
            ReadRealDevices = true;
            s_heldByChoice = false;
            s_heldByScreen = false;
            s_letGoFrame = -1;
            s_holds = 0;
        }

        void Awake()
        {
            _player = GetComponent<PlayerCharacter>();
            _health = GetComponent<PlayerHealth>();
            _holdsSeen = s_holds;
        }

        void Update()
        {
            if (HeldByChoice || HeldByScreen)
            {
                WaitForKeysUp();
                return;
            }
            if (_holdsSeen != s_holds)
            {
                // A choice or a screen held the input since the last frame, perhaps opened and closed between two of them.
                _holdsSeen = s_holds;
                WaitForKeysUp();
            }
            InputFrame? read = InputSource != null ? InputSource() : ReadDevices();
            if (read == null)
            {
                // No input this frame: no key is down, so none waits any more (work order 026).
                _waitForUp = 0;
                _shootWaitsForUp = false;
                _moveBefore = Vector2.zero;
                return;
            }
            InputFrame input = read.Value;

            // Work order 026 (the review of 2026-11-20): after a choice or a screen held the input, a skill key or the
            // shoot button that was not down going into the hold counts again only once it has been seen up, so the key
            // or the click that answered it fires nothing, also when its press reaches the input a frame late. On the
            // frame of the letting go the dash and focus presses are dropped too. The move keys, the cover key and a
            // skill key held into the hold come through as they are.
            if (LetGoThisFrame)
            {
                WaitForKeysUp();
                input.DashPressed = false;
                input.IdentityPressed = false;
            }
            _waitForUp &= input.SkillsHeld | input.SkillsPressed;
            input.SkillsPressed &= ~_waitForUp;
            input.SkillsHeld &= ~_waitForUp;
            if (_shootWaitsForUp) _shootWaitsForUp = input.ShootPressed || input.ShootHeld;
            if (_shootWaitsForUp) input.ShootPressed = false;

            if (_camera == null) _camera = Camera.main;
            if (_camera == null) return;
            if (!TryGroundPoint(input.CursorScreen, out Vector3 cursor)) return;
            Vector3 aim = ShotAim(input.CursorScreen, cursor, out float aimHeight); // work order 094: the shot's, a line's and a cone's; the cursor's ground point for what lands on the floor
            Vector3 lag = CameraLag(); // work order 094: both as the cursor lies in the frame that will be drawn
            cursor += lag;
            aim += lag;

            if (_health != null && !_health.IsAlive)
            {
                _skillsHeldBefore = 0;
                _player.Steer(Vector3.zero);
                _player.HoldCover(false);
                _player.HoldSprint(false);
                return;
            }

            // Movement first, so a shot this frame knows whether she is moving; the cover key with it.
            // With whether a move key went down: a quick duck ends on a key going down, not on one let go (work order 047);
            // read key by key where the keys were read (work order 055), else from the axis.
            _player.Steer(ScreenToGround(input.Move), input.MoveKeysRead ? input.MovePressed
                                                      : Rules.QuickDuckRules.MoveKeyWentDown(_moveBefore.x, _moveBefore.y, input.Move.x, input.Move.y));
            _moveBefore = input.Move;
            _player.HoldCover(input.CoverHeld);
            _player.HoldSprint(input.SprintHeld); // work order 092
            _player.AimAt(aim, cursor, aimHeight);
            if (input.IdentityPressed) _player.ActivateIdentity();
            if (input.DashPressed) _player.DashToward(DashPoint(cursor));

            // The bits are keys; the skill on each key comes from her bar (work order 015). An empty key does nothing.
            int held = input.SkillsHeld | input.SkillsPressed;
            for (int key = 0; key < 32; key++)
            {
                int bit = 1 << key;
                int slot = _player.SkillOnKey(key);
                if (slot < 0) continue;
                if ((input.SkillsPressed & bit) != 0) _player.PressSkill(slot, _player.PlacedOnTheGround(slot) ? cursor : aim);
                if ((_skillsHeldBefore & bit) != 0 && (held & bit) == 0) _player.ReleaseSkill(slot);
            }
            _skillsHeldBefore = held;

            if (input.ShootPressed || HoldToFireInput.FiresHeld(_player, ActiveBindings, input.ShootHeld, _shootWaitsForUp)) _player.Shoot(aim); // work order 132: held, at the shot's own pace
        }

        /// <summary>While the input is held, and on the frame it is let go: every key not held into the hold, and the shoot button, wait to be seen up.</summary>
        void WaitForKeysUp()
        {
            _waitForUp = ~_skillsHeldBefore;
            _shootWaitsForUp = true;
        }

        /// <summary>
        /// The point the dash goes toward, as the bindings choose (decision 0004): the cursor, or a dash length along
        /// the held move direction, or along where she faces when no move key is held. Read after the move keys, so it
        /// is this frame's direction. The pause menu's setting, when chosen, overrides the asset's (work order 018, <see cref="GameSettings"/>).
        /// </summary>
        public Vector3 DashPoint(Vector3 cursor)
        {
            if (GameSettings.Dash(ActiveBindings.DashDirection) != DashDirection.TowardMovement) return cursor;
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
            bool Down(Key k) => k != Key.None && keyboard[k].wasPressedThisFrame; // work order 055: each move key's own press
            var frame = new InputFrame
            {
                CursorScreen = mouse.position.ReadValue(),
                Move = new Vector2(Axis(bindings.MoveRight, bindings.MoveLeft), Axis(bindings.MoveUp, bindings.MoveDown)),
                ShootPressed = bindings.Shoot.Control(mouse, keyboard)?.wasPressedThisFrame ?? false,
                ShootHeld = bindings.Shoot.Control(mouse, keyboard)?.isPressed ?? false,
                DashPressed = bindings.Dash != Key.None && keyboard[bindings.Dash].wasPressedThisFrame,
                IdentityPressed = bindings.Focus != Key.None && keyboard[bindings.Focus].wasPressedThisFrame,
                CoverHeld = bindings.TakeCover != Key.None && keyboard[bindings.TakeCover].isPressed,
                SprintHeld = bindings.Sprint != Key.None && keyboard[bindings.Sprint].isPressed, // work order 092
                MoveKeysRead = true,
                MovePressed = Down(bindings.MoveRight) || Down(bindings.MoveLeft) || Down(bindings.MoveUp) || Down(bindings.MoveDown),
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

        /// <summary>
        /// Work order 094 (Kristian 2026-11-23: pointing east or west the Laser Sight's line was off the mouse, up the screen;
        /// north or south it was on it): the point the basic shot, its line and every skill that is a line or a cone from her
        /// aim at. The shot is drawn and flies at the gun's height (<see cref="PlayerCharacter.ShotHeight"/>), and the camera
        /// looks down at a slant, so the cursor's ray is met with the level plane at that height, not with the ground: the
        /// drawn line then passes through the cursor on the screen. Laid on the ground under that point, as every aim point is.
        /// With <see cref="Rules.FeelValues.AimAtShotHeight"/> off, or a ray that never meets it, the cursor's ground point as before
        /// (and a <paramref name="height"/> of 0).
        /// </summary>
        Vector3 ShotAim(Vector2 screen, Vector3 ground, out float height)
        {
            height = 0f;
            if (!_player.Feel.AimAtShotHeight) return ground;
            float at = _player.ShotHeight;
            Ray ray = _camera.ScreenPointToRay(screen);
            if (!new Plane(Vector3.up, new Vector3(0f, at, 0f)).Raycast(ray, out float distance)) return ground;
            height = at;
            Vector3 point = ray.GetPoint(distance);
            return new Vector3(point.x, 0f, point.z);
        }

        CameraRig _rig;       // work order 094: the follow camera's rig, looked up once per camera
        Camera _rigOfCamera;

        /// <summary>
        /// Work order 094, her aim while she runs (measured: the Laser Sight's line 0.9 degrees behind the cursor at a run, at 60
        /// frames a second): she has already moved this frame when the driver reads (the NavMesh agent moves her before the
        /// Updates), but the camera follows her only in its LateUpdate, so the cursor's ray is through the camera as the frame
        /// before left it. The ground under the cursor in the frame that will be drawn lies that much further on: from where the
        /// camera looks now to where its rig's target is. Zero for a camera without a rig, and with
        /// <see cref="Rules.FeelValues.AimAtShotHeight"/> off.
        /// </summary>
        Vector3 CameraLag()
        {
            if (!_player.Feel.AimAtShotHeight) return Vector3.zero;
            if (_rigOfCamera != _camera)
            {
                _rigOfCamera = _camera;
                _rig = _camera.GetComponent<CameraRig>();
            }
            Transform cam = _camera.transform;
            if (_rig == null || _rig.Target == null || cam.forward.y > -1e-3f) return Vector3.zero;
            Vector3 looksAt = cam.position + cam.forward * (cam.position.y / -cam.forward.y); // where its view meets the ground (CameraRig)
            Vector3 target = _rig.Target.position;
            return new Vector3(target.x - looksAt.x, 0f, target.z - looksAt.z);
        }
    }
}
