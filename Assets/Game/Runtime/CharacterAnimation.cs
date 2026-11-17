using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Shows what the <see cref="PlayerCharacter"/> is doing on a rigged model. It only reads the character;
    /// control never depends on it, and a scene without a rigged model simply has no such component.
    ///
    /// The Animator's controller has four states with no transitions between them: Idle, Run, Shoot and Dash.
    /// This component picks the state (<see cref="PoseRule"/>) and cross-fades to it.
    ///
    /// The clip timings belong to the model's clips, so the scene builder sets them:
    /// - The Shoot clip is entered so that <see cref="ShootFireTime"/> falls on the moment the shot goes off.
    /// - The Dash clip plays from <see cref="DashClipStart"/> to <see cref="DashClipEnd"/> over the dash and
    ///   <see cref="DashRecovery"/> seconds after it.
    /// - The hips are held over the character, so a clip that travels (a roll) does not leave it.
    /// </summary>
    public sealed class CharacterAnimation : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string RunState = "Run";
        public const string ShootState = "Shoot";
        public const string DashState = "Dash";

        const float FadeToIdle = 0.15f;
        const float FadeToRun = 0.1f;
        const float FadeToShoot = 0.05f;
        const float FadeToDash = 0.03f;

        public PlayerCharacter Player;
        public Animator Animator;
        public Transform Hips;

        [Header("Timings of this model's clips")]
        [Tooltip("Seconds into the Shoot clip at which the shot goes off.")]
        public float ShootFireTime;
        [Tooltip("The part of the Dash clip to play, in seconds.")]
        public float DashClipStart;
        public float DashClipEnd;
        [Tooltip("Seconds the dash animation may run on after the dash itself has ended.")]
        public float DashRecovery = 0.25f;
        [Tooltip("The speed, in m/s, at which the Run clip's steps match the ground.")]
        public float RunClipSpeed = 6f;

        static readonly int s_Idle = Animator.StringToHash(IdleState);
        static readonly int s_Run = Animator.StringToHash(RunState);
        static readonly int s_Shoot = Animator.StringToHash(ShootState);
        static readonly int s_Dash = Animator.StringToHash(DashState);

        int _seenDashes;
        int _seenActions;
        float _dashPoseUntil = float.NegativeInfinity;
        Vector3 _hipsRest;
        bool _started;

        public CharacterPose Pose { get; private set; }

        /// <summary>How far, on the ground plane, the hips are from where they rest over the character.</summary>
        public float HipsDrift
        {
            get
            {
                if (Hips == null || Animator == null) return 0f;
                Vector3 local = Animator.transform.InverseTransformPoint(Hips.position);
                return new Vector2(local.x - _hipsRest.x, local.z - _hipsRest.z).magnitude;
            }
        }

        void Start()
        {
            if (Player == null || Animator == null) { enabled = false; return; }
            Animator.applyRootMotion = false;
            if (Hips != null) _hipsRest = Animator.transform.InverseTransformPoint(Hips.position);
            _seenDashes = Player.DashesStarted;
            _seenActions = Player.ActionsStarted;
            Pose = CharacterPose.Idle;
            Animator.Play(s_Idle, 0, 0f);
            _started = true;
        }

        void Update()
        {
            FeelValues feel = Player.Feel;
            float now = Time.time;

            bool newDash = Player.DashesStarted != _seenDashes;
            bool newAction = Player.ActionsStarted != _seenActions;
            _seenDashes = Player.DashesStarted;
            _seenActions = Player.ActionsStarted;
            if (newDash) _dashPoseUntil = now + feel.DashDuration + DashRecovery;

            CharacterPose pose = PoseRule.Pick(Player.IsDashing, Player.Action != ActionKind.None, now < _dashPoseUntil, Player.Speed);
            bool restart = (pose == CharacterPose.Dash && newDash) || (pose == CharacterPose.Shoot && newAction);
            if (pose != Pose || restart) Show(pose, feel);

            Animator.speed = SpeedFor(Pose, feel);
        }

        void Show(CharacterPose pose, FeelValues feel)
        {
            Pose = pose;
            switch (pose)
            {
                case CharacterPose.Idle:
                    Animator.CrossFadeInFixedTime(s_Idle, FadeToIdle, 0);
                    break;
                case CharacterPose.Run:
                    Animator.CrossFadeInFixedTime(s_Run, FadeToRun, 0);
                    break;
                case CharacterPose.Shoot:
                    // Enter the clip one startup before its firing moment, so the two fall together.
                    Animator.CrossFadeInFixedTime(s_Shoot, FadeToShoot, 0, Mathf.Max(0f, ShootFireTime - feel.BasicShotStartup));
                    break;
                case CharacterPose.Dash:
                    Animator.CrossFadeInFixedTime(s_Dash, FadeToDash, 0, DashClipStart);
                    break;
            }
        }

        float SpeedFor(CharacterPose pose, FeelValues feel)
        {
            switch (pose)
            {
                case CharacterPose.Run:
                    return RunClipSpeed > 0f ? Mathf.Clamp(Player.Speed / RunClipSpeed, 0.5f, 2f) : 1f;
                case CharacterPose.Dash:
                    float shown = feel.DashDuration + DashRecovery;
                    return shown > 0f ? Mathf.Max(0.1f, (DashClipEnd - DashClipStart) / shown) : 1f;
                default:
                    return 1f;
            }
        }

        /// <summary>After the Animator has posed the skeleton: put the hips back over the character, keeping their height.</summary>
        void LateUpdate()
        {
            if (!_started || Hips == null) return;
            Transform root = Animator.transform;
            Vector3 local = root.InverseTransformPoint(Hips.position);
            local.x = _hipsRest.x;
            local.z = _hipsRest.z;
            Hips.position = root.TransformPoint(local);
        }
    }
}
