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
    /// point the character faces, and each hand is turned so that the weapon in it points there. The hand
    /// keeps hold of its weapon exactly as it was placed. No clip in the library holds a pistol in each hand,
    /// and this way the guns always point where the shot goes.
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
        [Tooltip("Where a pistol sits in a hand. Read every frame, so it can be tuned while playing. Optional.")]
        public GripSettings Grip;
        [Tooltip("Seconds to raise the arms into the aim, and to lower them again.")]
        public float AimBlend = 0.08f;
        [Tooltip("Metres toward the target that the arms point at. Shorter makes them converge more.")]
        public float AimDistance = 6f;

        [Header("Turning the upper body toward the target (optional)")]
        [Tooltip("The spine bones from the hips up. A target to the side is aimed at by turning these a share each, and the arms do the rest.")]
        public Transform[] Spine;
        public Transform Neck;
        public Transform Head;
        [Tooltip("The share of the turn toward the target that the spine takes, spread evenly over its bones; the arms take the rest. The neck and head turn on to face the target.")]
        [Range(0f, 1f)] public float TorsoShare = 0.6f;
        [Tooltip("The upper body never turns more than this many degrees from the legs.")]
        public float MaxTwist = 90f;

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
        Vector3 _aimDirection = Vector3.forward;

        /// <summary>0 with the arms as the clip has them, 1 with both arms and weapons pointed at the target.</summary>
        public float AimWeight => _aimWeight;

        /// <summary>Degrees the upper body is turned from the legs this frame, clockwise seen from above.</summary>
        public float Twist { get; private set; }

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

            // A shot on the move keeps the legs running: it is shown as the run with the arms aimed on top.
            bool acting = Player.Action != ActionKind.None && !Player.IsShootingOnTheMove;
            CharacterPose pose = PoseRule.Pick(Player.IsDashing, acting, now < _dashPoseUntil, Player.Speed);
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

            bool aiming = Pose == CharacterPose.Shoot || Player.IsShootingOnTheMove;
            float wanted = aiming ? 1f : 0f;
            _aimWeight = AimBlend > 0f ? Mathf.MoveTowards(_aimWeight, wanted, Time.deltaTime / AimBlend) : wanted;

            // Where to aim: at the shot's target or the skill's aim point while acting; while the arms come down
            // again, the last such direction, so they do not swing round on the way.
            Vector3 body = Flat(Player.transform.forward).normalized;
            if (Player.Action != ActionKind.None)
            {
                Vector3 toAim = Flat(Player.AimPoint - Player.transform.position);
                _aimDirection = toAim.sqrMagnitude > 1e-4f ? toAim.normalized : body;
            }
            else if (_aimWeight <= 0f) _aimDirection = body;

            // The weapons are not animated, so they keep what was set last frame: start from how they are held.
            if (RightWeapon != null) RightWeapon.localRotation = _rightWeaponRest;
            if (LeftWeapon != null) LeftWeapon.localRotation = _leftWeaponRest;
            Twist = 0f;
            if (_aimWeight > 0f)
            {
                TurnUpperBody(Vector3.SignedAngle(body, _aimDirection, Vector3.up));
                Vector3 ahead = Player.transform.position + _aimDirection * AimDistance;
                AimArm(RightArm, RightForeArm, RightHand, RightWeapon, _rightWeaponRest, ahead);
                AimArm(LeftArm, LeftForeArm, LeftHand, LeftWeapon, _leftWeaponRest, ahead);
            }

            // Last, with the hands and the weapons turned as they will be shown: seat each weapon in its hand.
            SeatInHand(RightWeapon, RightHand, outwardSign: -1f);
            SeatInHand(LeftWeapon, LeftHand, outwardSign: 1f);
        }

        /// <summary>
        /// Puts the middle of the weapon where the grip settings say, measured from the wrist in the weapon's
        /// own directions: +X along the barrel, +Y toward its top. Its +Z is to the character's left when it
        /// points ahead, so "away from the body" is -Z for the right hand and +Z for the left.
        /// </summary>
        void SeatInHand(Transform weapon, Transform hand, float outwardSign)
        {
            if (Grip == null || weapon == null || hand == null) return;
            weapon.position = hand.position + weapon.rotation * new Vector3(Grip.Forward, Grip.Up, Grip.Outward * outwardSign);
        }

        /// <summary>
        /// Turns the upper body about the vertical toward a target <paramref name="degrees"/> to the side of where
        /// the legs face: the spine bones each a share of <see cref="TorsoShare"/>, so the turn is spread up the back
        /// instead of hinging at the waist, then the neck and the head the rest of the way, so the head faces the
        /// target. The arms, aimed after this, make up what the spine leaves. Blended in and out with the aim.
        /// </summary>
        void TurnUpperBody(float degrees)
        {
            float turn = Mathf.Clamp(degrees, -MaxTwist, MaxTwist) * _aimWeight;
            Twist = turn;
            if (Mathf.Abs(turn) < 0.01f) return;
            int spineBones = 0;
            if (Spine != null) foreach (Transform bone in Spine) if (bone != null) spineBones++;
            float torso = spineBones > 0 ? TorsoShare : 0f;
            if (spineBones > 0)
                foreach (Transform bone in Spine)
                    if (bone != null) bone.rotation = Quaternion.AngleAxis(turn * torso / spineBones, Vector3.up) * bone.rotation;
            float rest = turn * (1f - torso);
            if (Neck != null) Neck.rotation = Quaternion.AngleAxis(rest * 0.4f, Vector3.up) * Neck.rotation;
            if (Head != null) Head.rotation = Quaternion.AngleAxis(rest * (Neck != null ? 0.6f : 1f), Vector3.up) * Head.rotation;
        }

        /// <summary>
        /// Straightens one arm toward a point ahead, level with its shoulder, and turns the hand so that the
        /// weapon in it points there with its top up. The weapon stays in the hand as it was placed.
        /// </summary>
        void AimArm(Transform upper, Transform fore, Transform hand, Transform weapon, Quaternion weaponInHand, Vector3 ahead)
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
            // The weapon's +X along the aim and its +Y up; the hand is wherever it has to be to hold it so.
            Quaternion aimedWeapon = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up), Vector3.up);
            hand.rotation = Quaternion.Slerp(hand.rotation, aimedWeapon * Quaternion.Inverse(weaponInHand), _aimWeight);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
