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
    ///   A skill that is held for longer than the firing pose lasts goes back to <see cref="ShootFireTime"/>
    ///   at <see cref="ShootHoldEnd"/>, so the character does not holster its guns in the middle of a skill.
    /// - The Dash clip plays from <see cref="DashClipStart"/> to <see cref="DashClipEnd"/> over the dash and
    ///   <see cref="DashRecovery"/> seconds after it.
    /// - The hips are held over the character, so a clip that travels (a roll) does not leave it.
    ///
    /// If the arm bones are set, the Shoot pose is finished in code: both arms are straightened toward the
    /// point the character faces, and both weapons are turned to point there. No clip in the library holds
    /// a pistol in each hand, and this way the guns always point where the shot goes.
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
        [Tooltip("Seconds into the Shoot clip at which the firing pose ends. 0 means the clip is never looped back.")]
        public float ShootHoldEnd;
        [Tooltip("The part of the Dash clip to play, in seconds.")]
        public float DashClipStart;
        public float DashClipEnd;
        [Tooltip("Seconds the dash animation may run on after the dash itself has ended.")]
        public float DashRecovery = 0.25f;
        [Tooltip("The speed, in m/s, at which the Run clip's steps match the ground.")]
        public float RunClipSpeed = 6f;

        [Header("Aiming in the Shoot pose (optional)")]
        public Transform RightArm;
        public Transform RightForeArm;
        public Transform RightHand;
        [Tooltip("The holder of the weapon in the right hand. Its local +X is the barrel and +Y the top of the weapon.")]
        public Transform RightWeapon;
        public Transform LeftArm;
        public Transform LeftForeArm;
        public Transform LeftHand;
        public Transform LeftWeapon;
        [Tooltip("Seconds to raise the arms into the aim, and to lower them again.")]
        public float AimBlend = 0.08f;
        [Tooltip("Metres ahead of the character that the arms point at. Shorter makes them converge more.")]
        public float AimDistance = 6f;

        static readonly int s_Idle = Animator.StringToHash(IdleState);
        static readonly int s_Run = Animator.StringToHash(RunState);
        static readonly int s_Shoot = Animator.StringToHash(ShootState);
        static readonly int s_Dash = Animator.StringToHash(DashState);

        int _seenDashes;
        int _seenActions;
        float _dashPoseUntil = float.NegativeInfinity;
        Vector3 _hipsRest;
        bool _started;
        float _aimWeight;
        Quaternion _rightWeaponRest = Quaternion.identity;
        Quaternion _leftWeaponRest = Quaternion.identity;

        /// <summary>0 with the arms as the clip has them, 1 with both arms and weapons pointed at the target.</summary>
        public float AimWeight => _aimWeight;

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
            if (RightWeapon != null) _rightWeaponRest = RightWeapon.localRotation;
            if (LeftWeapon != null) _leftWeaponRest = LeftWeapon.localRotation;
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
            // A Shoot clip with a firing moment starts over for every shot; one without is just a stance and plays on.
            bool restart = (pose == CharacterPose.Dash && newDash)
                        || (pose == CharacterPose.Shoot && newAction && ShootFireTime > 0f);
            if (pose != Pose || restart) Show(pose);
            else if (pose == CharacterPose.Shoot && PastTheFiringPose()) Animator.CrossFadeInFixedTime(s_Shoot, FadeToRun, 0, ShootFireTime);

            Animator.speed = SpeedFor(Pose, feel);
        }

        /// <summary>True when the Shoot clip has played past its firing pose while the shot or skill is still going.</summary>
        bool PastTheFiringPose()
        {
            if (ShootHoldEnd <= ShootFireTime || Animator.IsInTransition(0)) return false;
            AnimatorStateInfo state = Animator.GetCurrentAnimatorStateInfo(0);
            return state.shortNameHash == s_Shoot && state.normalizedTime * state.length >= ShootHoldEnd;
        }

        void Show(CharacterPose pose)
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
                    // Enter the clip as long before its firing moment as the shot or skill takes to its first hit.
                    Animator.CrossFadeInFixedTime(s_Shoot, FadeToShoot, 0, Mathf.Max(0f, ShootFireTime - Player.ActionLeadTime));
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

        /// <summary>
        /// After the Animator has posed the skeleton: put the hips back over the character, keeping their
        /// height, and in the Shoot pose point the arms and the weapons at the target.
        /// </summary>
        void LateUpdate()
        {
            if (!_started) return;
            if (Hips != null)
            {
                Transform root = Animator.transform;
                Vector3 local = root.InverseTransformPoint(Hips.position);
                local.x = _hipsRest.x;
                local.z = _hipsRest.z;
                Hips.position = root.TransformPoint(local);
            }

            float wanted = Pose == CharacterPose.Shoot ? 1f : 0f;
            _aimWeight = AimBlend > 0f ? Mathf.MoveTowards(_aimWeight, wanted, Time.deltaTime / AimBlend) : wanted;

            // The weapons are not animated, so they keep what was set last frame: start from how they are held.
            if (RightWeapon != null) RightWeapon.localRotation = _rightWeaponRest;
            if (LeftWeapon != null) LeftWeapon.localRotation = _leftWeaponRest;
            if (_aimWeight <= 0f) return;

            Vector3 ahead = Player.transform.position + Flat(Player.transform.forward).normalized * AimDistance;
            AimArm(RightArm, RightForeArm, RightHand, RightWeapon, ahead);
            AimArm(LeftArm, LeftForeArm, LeftHand, LeftWeapon, ahead);
        }

        /// <summary>Straightens one arm toward a point ahead, level with its shoulder, and turns its weapon to point there.</summary>
        void AimArm(Transform upper, Transform fore, Transform hand, Transform weapon, Vector3 ahead)
        {
            if (upper == null || fore == null || hand == null) return;
            Vector3 dir = new Vector3(ahead.x, upper.position.y, ahead.z) - upper.position;
            if (dir.sqrMagnitude < 1e-6f) return;
            dir.Normalize();

            upper.rotation = Quaternion.Slerp(upper.rotation,
                Quaternion.FromToRotation(fore.position - upper.position, dir) * upper.rotation, _aimWeight);
            fore.rotation = Quaternion.Slerp(fore.rotation,
                Quaternion.FromToRotation(hand.position - fore.position, dir) * fore.rotation, _aimWeight);

            if (weapon == null) return;
            // The weapon's +X along the aim and its +Y up.
            Quaternion aimed = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            weapon.rotation = Quaternion.Slerp(weapon.rotation, aimed, _aimWeight);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
