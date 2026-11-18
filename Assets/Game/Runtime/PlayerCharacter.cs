using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>What the character has been told to do: walk somewhere. An order stays until it is done or replaced.</summary>
    public enum OrderKind { None, Move }

    /// <summary>The committed action the character is locked in, if any.</summary>
    public enum ActionKind { None, BasicShot, Skill }

    public enum IntentResult { Started, Buffered, Rejected }

    /// <summary>
    /// The player character. Everything that drives it goes through the intent API:
    /// <see cref="MoveTo"/>, <see cref="SteerTo"/>, <see cref="Shoot"/>, <see cref="DashToward"/>,
    /// <see cref="PressSkill"/>, <see cref="ReleaseSkill"/>, <see cref="AimAt"/>, <see cref="ActivateIdentity"/>
    /// and <see cref="Stop"/>. Input and tests both call these; nothing else moves the character.
    ///
    /// The only order is a move. It stays until done or replaced, and waits for a lock to end.
    /// Presses (dash, skill) made during a lock go through the input buffer and expire.
    /// A skill locks the character, and only the dash cancels it.
    ///
    /// The basic shot goes where the cursor points (decision 0002, Kristian 2026-11-18): a straight line toward the
    /// cursor's ground point, aimed at the live cursor until it goes off, hitting the first living target the line
    /// touches and only that one. With nothing in the line it is a miss that still fires. A click never moves her:
    /// standing she shoots from where she stands; walking on a move order she shoots on the move, the move going on,
    /// slowed while the shot winds up and back to full speed after it.
    ///
    /// Click to shoot: one click, one shot, no auto-attack. A click while she cannot shoot yet waits and fires the
    /// moment the cycle allows; only one waits (<see cref="ClickToShoot"/>). A move order drops it; so do a dash and
    /// a skill. A shot that went off makes the next one wait out the whole shot time, so clicking never raises
    /// the fire rate.
    ///
    /// The basic shot never locks, as in League. Before a standing shot has gone off, a move order, a skill or a dash
    /// cancels it, and a cancelled shot costs nothing. After it has gone off, anything takes over at once. A shot on
    /// the move goes on under a new move order. A basic shot turns the whole body only if the aim lies outside the
    /// front angle; within it the body keeps its direction and CharacterAnimation turns the upper body.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        const float ArriveDistance = 0.05f;
        /// <summary>Where a shot starts without a pistol to fire it from: this high over the character's ground point.</summary>
        public const float FallbackMuzzleHeight = 0.6f;
        /// <summary>Metres between streaks side by side across a line skill. A line narrower than twice this shows one streak.</summary>
        public const float LineStreakSpacing = 0.5f;

        public FeelSettings Settings;
        public SkillSettings SkillSet;
        [Tooltip("How each shot looks. Optional: without it the shots use the start values.")]
        public ShotFxSettings ShotLooks;
        [Tooltip("The material of the shot effects: unlit, drawn in the colours of their vertices.")]
        public Material ShotFxMaterial;
        [Tooltip("The barrel end of the pistol in the right hand. Optional: without a muzzle, shots start at the character.")]
        public Transform RightMuzzle;
        public Transform LeftMuzzle;
        [Tooltip("Plays the shot sounds. Optional: without it the shots are silent.")]
        public ShotSounds Sounds;

        enum PressKind { Dash, Skill }

        struct Press
        {
            public PressKind Kind;
            public Vector3 Point;
            public int Slot; // which skill, for a skill press
        }

        readonly FeelValues _fallbackFeel = new FeelValues();
        readonly Dash _dash = new Dash();
        readonly ActionTimeline _action = new ActionTimeline();
        readonly InputBuffer<Press> _buffer = new InputBuffer<Press>();
        readonly List<TargetInfo> _targetScratch = new List<TargetInfo>();
        readonly ClickToShoot _clicks = new ClickToShoot();
        readonly ShotFacing _shotFacing = new ShotFacing();
        Vector3 _runDirection = Vector3.forward;    // the way she last moved, kept through a hit stop
        readonly SkillCast _cast = new SkillCast();
        readonly List<Cooldown> _cooldowns = new List<Cooldown>();
        readonly SkillDefinition[] _fallbackSkills = SkillDefaults.Create();
        readonly FocusGauge _focus = new FocusGauge();
        readonly IdentityValues _fallbackIdentity = new IdentityValues();
        readonly ShotLook _fallbackBasicLook = ShotFxDefaults.Basic();
        readonly Dictionary<string, ShotLook> _fallbackLooks = new Dictionary<string, ShotLook>();

        NavMeshAgent _agent;

        OrderKind _order;
        Vector3 _orderPoint;
        bool _travelDirty = true;
        Vector3 _travelPoint;

        ActionKind _actionKind;
        Vector3 _aimPoint;
        Vector3 _shotCursor;                        // where the basic shot aims: the latest click or cursor report
        int _castSlot = -1;
        float _nextShotAt = float.NegativeInfinity; // the earliest the next basic shot may start
        float _shotStartedAt;
        bool _orderedDuringShot;                    // a move order came in while a standing basic shot was going
        bool _movingShot;                           // the basic shot in progress is a shot on the move
        bool _shotTurnsBody;                        // the basic shot in progress turned the whole body to its aim
        // The speed around shots on the move (MovingShot.SpeedShare): eased down from the share it had when the
        // latest such shot started, and back up once that shot has gone off and the slow-down is done.
        bool _easing;
        float _easeStartedAt;
        float _easeFrom = 1f;
        float _easeFiredAfter = -1f;
        Vector3 _cursorPoint;
        bool _hasCursor;     // a cursor report came after the skill in use started
        bool _cursorKnown;   // a cursor report came at all: where she looks
        bool _bufferedSkillReleased;
        float _castMoved;      // how far the skill in use has moved the character so far
        Vector3 _castMoveDir;  // along the aim it was pressed with

        bool _leftHandNext; // the bullets of one shot come from one hand, and the next shot from the other

        bool _dashing;
        Vector3 _dashFrom;
        Vector3 _dashTo;

        // ---------------- State, for the readout and for tests ----------------

        public FeelValues Feel => Settings != null ? Settings.Values : _fallbackFeel;
        public Vector3 Position => transform.position;
        public GroundPoint GroundPosition => new GroundPoint(transform.position.x, transform.position.z);
        public float Speed => _agent.velocity.magnitude;
        public Vector3 Velocity => _agent.velocity;

        public OrderKind Order => _order;
        /// <summary>True once the cursor has been reported; <see cref="LookPoint"/> is then where it is on the ground.</summary>
        public bool HasLookPoint => _cursorKnown;
        public Vector3 LookPoint => _cursorPoint;
        public ActionKind Action => _actionKind;
        /// <summary>The phase of the lock. For a skill, holding or charging counts as Active.</summary>
        public ActionPhase ActionPhase
        {
            get
            {
                if (_actionKind == ActionKind.BasicShot) return _action.Phase(Time.time);
                if (_actionKind != ActionKind.Skill) return ActionPhase.None;
                switch (_cast.Phase(Time.time))
                {
                    case SkillPhase.Startup: return ActionPhase.Startup;
                    case SkillPhase.Channel: return ActionPhase.Active;
                    case SkillPhase.Active: return ActionPhase.Active;
                    case SkillPhase.Recovery: return ActionPhase.Recovery;
                    default: return ActionPhase.None;
                }
            }
        }

        /// <summary>Seconds from the start of the current shot or skill to its first hit, as known when it started.</summary>
        public float ActionLeadTime { get; private set; }

        // ---------------- Skills, for the skill bar, the aim indicator and tests ----------------

        public IReadOnlyList<SkillDefinition> Skills =>
            SkillSet != null && SkillSet.Skills != null && SkillSet.Skills.Length > 0 ? SkillSet.Skills : _fallbackSkills;

        /// <summary>The slot of the skill in use, or -1.</summary>
        public int ActiveSkillSlot => _actionKind == ActionKind.Skill ? _castSlot : -1;
        public SkillDefinition ActiveSkill => _actionKind == ActionKind.Skill ? _cast.Skill : null;
        public SkillPhase SkillPhase => _actionKind == ActionKind.Skill ? _cast.Phase(Time.time) : SkillPhase.None;
        /// <summary>0 to 1, for a bar: the cast, the hold or the charge of the skill in use.</summary>
        public float SkillProgress => _actionKind == ActionKind.Skill ? _cast.Progress(Time.time) : 0f;
        public float SkillCharge => _actionKind == ActionKind.Skill ? _cast.Charge(Time.time) : 0f;
        /// <summary>Where the skill in use is aimed.</summary>
        public Vector3 AimPoint => _aimPoint;
        public float SkillCooldownRemaining(int slot) =>
            slot >= 0 && slot < Skills.Count ? CooldownFor(slot).Remaining(Time.time) : 0f;

        // ---------------- The identity gauge: focus ----------------

        public IdentityValues Identity => SkillSet != null && SkillSet.Identity != null ? SkillSet.Identity : _fallbackIdentity;
        public float Focus => _focus.Focus;
        public bool IsFocusFull => _focus.IsFull(Identity);
        public bool IsEmpowered => _focus.IsEmpowered(Time.time);
        public float EmpoweredRemaining => _focus.EmpoweredRemaining(Time.time);
        /// <summary>For the bar, 0 to 1: how full the gauge is, or while empowered how much of the mode is left.</summary>
        public float FocusBarFill => _focus.BarFill(Time.time, Identity);
        public bool IsDashing => _dashing;
        /// <summary>True while the character cannot start anything new: in a dash or a skill. A basic shot does not lock.</summary>
        public bool IsLocked => _dashing || _actionKind == ActionKind.Skill;
        /// <summary>True from the moment a basic shot has gone off until its recovery is over or is cut short.</summary>
        public bool IsInShotRecovery => _actionKind == ActionKind.BasicShot && _action.HitResolved;
        public bool IsInvulnerable => _dash.IsInvulnerable(Time.time);
        public float DashCooldownRemaining => _dash.CooldownRemaining(Time.time);
        public bool HasBufferedPress => _buffer.HasItem;
        /// <summary>True while a click waits for its shot.</summary>
        public bool HasPendingShot => _clicks.Waiting;
        /// <summary>True while a basic shot is fired on the move: the move order goes on under it.</summary>
        public bool IsShootingOnTheMove => _actionKind == ActionKind.BasicShot && _movingShot;
        /// <summary>True while the basic shot in progress turned the whole body to its aim (it lay outside the front angle).</summary>
        public bool ShotTurnsBody => _actionKind == ActionKind.BasicShot && _shotTurnsBody;
        /// <summary>The speed the character may walk at now: the run speed, or less while it shoots on the move.</summary>
        public float SpeedLimit => _agent.speed;
        /// <summary>
        /// True while the speed is eased down or back up around a shot on the move: the shooting gait, forward or
        /// (with the body turned to a target behind) backward.
        /// </summary>
        public bool InShootingGait => _easing || _shotFacing.State != ShotFacing.Mode.Free;
        /// <summary>
        /// Where the body faces around shots that must turn it: free, held turned to the aim (backpedalling while she
        /// keeps shooting), or turning back to the run (<see cref="ShotFacing"/>).
        /// </summary>
        public ShotFacing.Mode FacingMode => _shotFacing.State;
        /// <summary>Counts the shots and skill hits fired from a pistol, for the recoil. Which hand and how hard is in the next two.</summary>
        public int KickCount { get; private set; }
        public bool KickLeftHand { get; private set; }
        public float KickStrength { get; private set; }
        /// <summary>Counts every basic shot that started on the move.</summary>
        public int ShotsOnTheMove { get; private set; }

        public int DashesStarted { get; private set; }
        /// <summary>Counts every basic shot and skill that started its startup, whether or not it went on to fire.</summary>
        public int ActionsStarted { get; private set; }
        public int BasicShotsFired { get; private set; }
        public int BasicShotHits { get; private set; }
        public int SkillShotsFired { get; private set; }
        public int SkillShotHits { get; private set; }

        void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.autoBraking = true;
            _agent.stoppingDistance = 0f;
            ApplyMovementFeel(Feel);
        }

        // ---------------- Intent API ----------------

        /// <summary>Order: walk to a point. It drops a click that waits for its shot.</summary>
        public void MoveTo(Vector3 point) => OrderMove(point, steering: false);

        /// <summary>
        /// The held move button following the cursor. It walks to the point as <see cref="MoveTo"/> does, but it
        /// keeps a click that waits for its shot, and it never cancels a standing shot: that shot goes off first,
        /// and the walk takes over after it.
        /// </summary>
        public void SteerTo(Vector3 point) => OrderMove(point, steering: true);

        /// <summary>
        /// Press: one basic shot toward a point on the ground, the cursor. It never moves her: standing she shoots
        /// from where she stands, walking on a move order she shoots on the move. The shot aims at the live cursor
        /// (<see cref="AimAt"/>) until it goes off. A click while she cannot shoot yet waits for the next shot.
        /// </summary>
        public void Shoot(Vector3 point)
        {
            _shotCursor = point;
            if (!_clicks.Click(Busy, Time.time, _nextShotAt)) return;
            StartBasicShot(Feel, onTheMove: IsWalkingOnMoveOrder);
        }

        /// <summary>True while she cannot start a basic shot now: a shot, a skill or a dash is going.</summary>
        bool Busy => _actionKind != ActionKind.None || _dashing;

        /// <summary>True while the character walks on a move order and is free to shoot on the move.</summary>
        bool IsWalkingOnMoveOrder =>
            _order == OrderKind.Move && !_dashing && _actionKind != ActionKind.Skill
            && (_actionKind == ActionKind.None || _movingShot)
            && _agent.isOnNavMesh && (_agent.pathPending || _agent.remainingDistance > ArriveDistance);

        /// <summary>Drop the order. A shot or dash in progress still finishes.</summary>
        public void Stop()
        {
            ClearOrder();
            if (!IsLocked) Halt();
        }

        /// <summary>
        /// Press: dash toward a point. Cancels a shot or skill in progress.
        /// Rejected while on cooldown or already dashing.
        /// </summary>
        public IntentResult DashToward(Vector3 point)
        {
            if (_dashing || !_dash.CanStart(Time.time)) return IntentResult.Rejected;

            _clicks.Clear(); // a dash drops a waiting click
            _shotFacing.End(turnBack: false); // and ends the backpedal at once: the dash faces its own way
            if (_actionKind != ActionKind.None)
            {
                if (_actionKind == ActionKind.Skill && !Feel.DashCancelsSkill) return BufferPress(PressKind.Dash, point);
                _action.Cancel();
                _cast.Cancel();
                EndAction();
            }
            _easing = false; // a dash ends the slow of a shot on the move at once
            return StartDash(point) ? IntentResult.Started : IntentResult.Rejected;
        }

        /// <summary>
        /// Press: the key of a skill went down, with the cursor over this point.
        /// Rejected while the skill is on cooldown. Buffered while the character is locked.
        /// The cooldown starts when the skill starts, so a skill cancelled by a dash is still spent.
        /// </summary>
        public IntentResult PressSkill(int slot, Vector3 point)
        {
            if (slot < 0 || slot >= Skills.Count) return IntentResult.Rejected;
            if (!CooldownFor(slot).IsReady(Time.time)) return IntentResult.Rejected;
            if (IsLocked)
            {
                _bufferedSkillReleased = false;
                return BufferPress(PressKind.Skill, point, slot);
            }
            StartSkill(slot, point);
            return IntentResult.Started;
        }

        /// <summary>
        /// The key of a skill went up. Ends a Holding skill and fires a Charge skill.
        /// If that press is still waiting in the buffer, the skill starts as already released.
        /// </summary>
        public void ReleaseSkill(int slot)
        {
            if (_actionKind == ActionKind.Skill && _castSlot == slot) _cast.Release(Time.time);
            else if (_buffer.HasItem) _bufferedSkillReleased = true;
        }

        /// <summary>
        /// Where the cursor is. A skill that is being held or charged follows it, from the first report
        /// after the skill started. Without a report it stays aimed at the point it was pressed with.
        /// A basic shot follows it until it goes off, and she looks toward it.
        /// </summary>
        public void AimAt(Vector3 point)
        {
            _cursorPoint = point;
            _hasCursor = true;
            _cursorKnown = true;
            _shotCursor = point;
        }

        /// <summary>
        /// Press: spend a full focus gauge on the empowered mode. It takes no time and locks nothing,
        /// so it can be pressed in the middle of a skill. Rejected unless the gauge is full.
        /// </summary>
        public IntentResult ActivateIdentity() =>
            _focus.TryActivate(Time.time, Identity) ? IntentResult.Started : IntentResult.Rejected;

        /// <summary>Fills the focus gauge. For tests and tools.</summary>
        public void FillFocus() => _focus.Fill(Identity);

        /// <summary>Press and let go of the first skill at once. Shorthand for tests and tools.</summary>
        public IntentResult CastAt(Vector3 point)
        {
            IntentResult result = PressSkill(0, point);
            if (result != IntentResult.Rejected) ReleaseSkill(0);
            return result;
        }

        /// <summary>Put the character somewhere else at once and clear what it was doing. For tests and tools.</summary>
        public void Teleport(Vector3 point)
        {
            _action.Cancel();
            _cast.Cancel();
            EndAction();
            _dashing = false;
            _hasCursor = false;
            _cursorKnown = false;
            _buffer.Clear();
            _clicks.Clear();
            _shotFacing.End(turnBack: false);
            _easing = false;
            ClearOrder();
            _agent.Warp(new Vector3(point.x, 0f, point.z));
            Halt();
        }

        // ---------------- Simulation ----------------

        void Update()
        {
            FeelValues feel = Feel;
            float now = Time.time;
            ApplyMovementFeel(feel);

            if (_dashing)
            {
                Vector3 target = Vector3.Lerp(_dashFrom, _dashTo, _dash.Progress(now));
                // NavMeshAgent.Move keeps the character on the NavMesh, so a dash stops at walls and edges.
                _agent.Move(Flat(target - transform.position));
                if (_dash.IsDashing(now)) return;
                _dashing = false;
            }

            if (_actionKind == ActionKind.BasicShot)
            {
                // The aim follows the live cursor until the shot goes off, and stays where it went after.
                if (!_action.HitResolved) _aimPoint = AimEnd(_shotCursor);
                if (_action.ConsumeHit(now))
                {
                    // Only a shot that went off makes the next one wait.
                    _nextShotAt = _shotStartedAt + _action.Total;
                    if (_movingShot && _easing) _easeFiredAfter = now - _easeStartedAt;
                    if (ResolveBasicShot()) HitStop.Trigger(feel.BasicShotHitStop);
                }
                if (_movingShot)
                {
                    // On the move: the move order goes on under the shot, at the speed ApplyMovementFeel sets.
                    // The body faces the aim if the shot turned it, else the way it walks.
                    if (_order == OrderKind.Move)
                    {
                        Travel(_orderPoint);
                        if (Arrived()) ClearOrder();
                    }
                    UpdateFacing(feel);
                    // Once it has gone off, a buffered press takes over at once.
                    bool takenOver = _action.HitResolved && _buffer.HasItem;
                    if (_action.IsRunning(now) && !takenOver) return;
                    _action.Cancel();
                    EndAction();
                }
                else
                {
                    // Once the shot has gone off, a move order that came in during it does not wait for the recovery.
                    // A waiting click does not cut it short: the recovery runs out exactly when the next shot may
                    // start, so the arms stay up between shots clicked in rhythm.
                    bool cutShort = _action.HitResolved && (_orderedDuringShot || _buffer.HasItem);
                    UpdateFacing(feel);
                    if (_action.IsRunning(now) && !cutShort) return;
                    _action.Cancel();
                    EndAction();
                }
            }
            else if (_actionKind == ActionKind.Skill)
            {
                SkillDefinition skill = _cast.Skill;
                // A skill that moves the character does so over its startup, along the aim it was pressed with.
                if (skill.MoveDistance != 0f)
                {
                    float moved = skill.MoveDistance * _cast.StartupProgress(now);
                    if (moved != _castMoved)
                    {
                        _agent.Move(_castMoveDir * (moved - _castMoved));
                        _castMoved = moved;
                    }
                }
                // A skill that is held or charged follows the cursor until the key goes up.
                if (_hasCursor && _cast.Phase(now) == SkillPhase.Channel)
                {
                    _aimPoint = _cursorPoint;
                    if (feel.FaceCursorOnCast) Face(Flat(_aimPoint - transform.position));
                }

                float charge = _cast.Charge(now);
                bool landed = false;
                for (int hits = _cast.TakeHits(now); hits > 0; hits--) landed |= ResolveSkillHit(skill, charge);
                if (landed) HitStop.Trigger(feel.HitStop);
                if (_cast.IsRunning(now)) return;
                EndAction();
            }

            // Free from here on: first a buffered press, then a waiting click, then the order.
            if (!_buffer.HasItem && _clicks.TakeWhenAllowed(Busy, now, _nextShotAt))
            {
                StartBasicShot(feel, onTheMove: IsWalkingOnMoveOrder);
                return;
            }
            if (_buffer.TryTake(now, feel.InputBuffer, out Press press))
            {
                if (press.Kind == PressKind.Skill)
                {
                    StartSkill(press.Slot, press.Point);
                    if (_bufferedSkillReleased) _cast.Release(now);
                    return;
                }
                if (StartDash(press.Point)) return;
            }

            RunOrder(feel);
            UpdateFacing(feel);
        }

        /// <summary>
        /// The body's facing, every frame she is free or in a basic shot. Normally it faces the way she moves, at the
        /// movement turn time. Around a shot that had to turn the body, <see cref="ShotFacing"/> holds it toward the
        /// live aim, then turns it back to the run, both at the shot turn rate, so she backpedals while she keeps
        /// shooting instead of flipping round and back on every shot.
        /// </summary>
        void UpdateFacing(FeelValues feel)
        {
            Vector3 v = Flat(_agent.velocity);
            if (v.sqrMagnitude > 0.25f) _runDirection = v.normalized;
            bool moving = _order == OrderKind.Move;
            Vector3 aim = Flat(AimEnd(_shotCursor) - transform.position);
            bool aimInsideRunFront = moving && !UpperBodyAim.BodyMustTurn(
                new GroundPoint(_runDirection.x, _runDirection.z), new GroundPoint(aim.x, aim.z), feel.ShotFrontAngle);
            bool facingRun = Vector3.Angle(Flat(transform.forward), _runDirection) < 1f;
            switch (_shotFacing.Update(Time.time, moving, aimInsideRunFront, facingRun))
            {
                case ShotFacing.Mode.Held:
                    TurnAtShotRate(aim, feel);
                    break;
                case ShotFacing.Mode.TurningBack:
                    TurnAtShotRate(_runDirection, feel);
                    break;
                default:
                    FaceMovement(feel);
                    break;
            }
        }

        /// <summary>Turns the body toward a direction at the shot turn rate (<see cref="FeelValues.ShotTurnTime"/> for a half turn).</summary>
        void TurnAtShotRate(Vector3 flatDir, FeelValues feel)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            float yaw = transform.eulerAngles.y;
            float want = Mathf.Atan2(flatDir.x, flatDir.z) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0f, ShotFacing.Turn(yaw, want, feel.ShotTurnTime, Time.deltaTime), 0f);
        }

        void ApplyMovementFeel(FeelValues feel)
        {
            // A shot on the move that was cancelled before it went off ends the slow at once.
            if (_easing && _easeFiredAfter < 0f && !_movingShot) _easing = false;
            if (_easing && MovingShot.IsOver(Time.time - _easeStartedAt, _easeFiredAfter, feel.MovingShotSlowdown, feel.MovingShotRecovery))
                _easing = false;
            _agent.speed = feel.RunSpeed * CurrentSpeedShare(feel);
            // The agent does not slow down by itself when its speed limit drops below how fast it is going.
            if (_agent.velocity.sqrMagnitude > _agent.speed * _agent.speed && !_dashing)
                _agent.velocity = _agent.velocity.normalized * _agent.speed;
            _agent.acceleration = feel.TimeToFullSpeed > 0f ? feel.RunSpeed / feel.TimeToFullSpeed : 10000f;
        }

        // ---------------- Orders ----------------

        /// <summary>
        /// Gives the move order. A fresh one (a click) drops a waiting click and cancels a standing shot that has not
        /// gone off; the held button (<paramref name="steering"/>) does neither. During a shot on the move it only
        /// changes where she is going.
        /// </summary>
        void OrderMove(Vector3 point, bool steering)
        {
            if (!steering)
            {
                _clicks.Clear();
                _shotFacing.End(turnBack: true); // a new move order ends the backpedal: she turns to the new way at the rate
            }
            if (_actionKind == ActionKind.BasicShot && !_movingShot)
            {
                if (_action.HitResolved || steering)
                {
                    _orderedDuringShot = true; // it takes over when the shot has gone off
                }
                else
                {
                    _action.Cancel(); // the shot has not gone off: it never will
                    EndAction();
                }
            }
            _order = OrderKind.Move;
            _orderPoint = point;
            _travelDirty = true;
        }

        void ClearOrder()
        {
            _order = OrderKind.None;
            _travelDirty = true;
        }

        void RunOrder(FeelValues feel)
        {
            if (_order != OrderKind.Move) return;
            Travel(_orderPoint);
            if (Arrived()) ClearOrder();
        }

        /// <summary>True when an aim direction lies outside the front angle, so a basic shot along it turns the whole body.</summary>
        bool BodyMustTurnToward(Vector3 direction, FeelValues feel)
        {
            Vector3 facing = Flat(transform.forward);
            return UpperBodyAim.BodyMustTurn(new GroundPoint(facing.x, facing.z), new GroundPoint(direction.x, direction.z), feel.ShotFrontAngle);
        }

        /// <summary>
        /// Where a basic shot aimed at the cursor point ends: the full range out, along the line toward the point,
        /// or along where she faces when the point is on her.
        /// </summary>
        Vector3 AimEnd(Vector3 cursor)
        {
            Vector3 facing = Flat(transform.forward);
            GroundPoint dir = LineShot.Direction(GroundPosition, new GroundPoint(cursor.x, cursor.z), new GroundPoint(facing.x, facing.z));
            return transform.position + new Vector3(dir.X, 0f, dir.Z) * Feel.BasicShotRange;
        }

        void Travel(Vector3 point)
        {
            if (!_travelDirty && (point - _travelPoint).sqrMagnitude < 1e-6f) return;
            _travelPoint = point;
            _travelDirty = false;
            _agent.isStopped = false;
            _agent.SetDestination(point);
        }

        bool Arrived() => !_agent.pathPending && _agent.remainingDistance <= ArriveDistance;

        void Halt()
        {
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.velocity = Vector3.zero;
            _travelDirty = true;
        }

        // ---------------- Dash ----------------

        bool StartDash(Vector3 point)
        {
            FeelValues feel = Feel;
            if (!_dash.TryStart(Time.time, feel)) return false;

            Vector3 dir = Flat(point - transform.position);
            if (dir.sqrMagnitude < 1e-6f) dir = Flat(transform.forward);
            dir.Normalize();

            Halt();
            _dashFrom = transform.position;
            _dashTo = _dashFrom + dir * feel.DashDistance;
            _dashing = true;
            DashesStarted++;
            Face(dir);
            return true;
        }

        // ---------------- Shots ----------------

        IntentResult BufferPress(PressKind kind, Vector3 point, int slot = -1)
        {
            _buffer.Store(new Press { Kind = kind, Point = point, Slot = slot }, Time.time);
            return IntentResult.Buffered;
        }

        /// <summary>
        /// Starts a basic shot toward the cursor. Standing, the character stops; on the move it walks on. Either way
        /// the whole body turns to the aim only if it lies outside the front angle; within it, the upper body aims.
        /// </summary>
        void StartBasicShot(FeelValues feel, bool onTheMove)
        {
            if (!onTheMove) Halt();
            _aimPoint = AimEnd(_shotCursor);
            _shotTurnsBody = BodyMustTurnToward(Flat(_aimPoint - transform.position), feel);
            // A shot that must turn the body starts the hold: she turns to the aim at the shot turn rate, and the
            // upper body and arms cover what the body has not turned yet. A shot while held renews it.
            _shotFacing.ShotStarted(_shotTurnsBody, Time.time, feel.ShotTurnHold);
            _actionKind = ActionKind.BasicShot;
            _movingShot = onTheMove;
            if (onTheMove)
            {
                ShotsOnTheMove++;
                // Ease down from the speed it has now: full speed, or wherever a chained shot left it.
                _easeFrom = CurrentSpeedShare(feel);
                _easeStartedAt = Time.time;
                _easeFiredAfter = -1f;
                _easing = true;
            }
            _orderedDuringShot = false;
            ActionsStarted++;
            ActionLeadTime = feel.BasicShotStartup;
            _shotStartedAt = Time.time;
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
        }

        void StartSkill(int slot, Vector3 point)
        {
            SkillDefinition skill = Skills[slot];
            _clicks.Clear(); // a skill drops a waiting click
            _shotFacing.End(turnBack: false); // and ends the backpedal at once: the skill faces the cursor
            _action.Cancel(); // a skill may start during a basic shot, and ends it
            _movingShot = false;
            _shotTurnsBody = false;
            _easing = false; // a skill ends the slow of a shot on the move at once
            Halt();
            _aimPoint = point;
            _castSlot = slot;
            _hasCursor = false; // follow only a cursor that is reported after this skill started, never a stale one
            _castMoved = 0f;
            Vector3 toAim = Flat(point - transform.position);
            _castMoveDir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Flat(transform.forward).normalized;
            if (Feel.FaceCursorOnCast) Face(Flat(point - transform.position));

            _actionKind = ActionKind.Skill;
            ActionsStarted++;
            ActionLeadTime = skill.Input == SkillInput.Charge ? 0f : skill.Startup;
            _cast.Start(skill, Time.time);
            CooldownFor(slot).Start(Time.time, skill.Cooldown);
        }

        void EndAction()
        {
            _actionKind = ActionKind.None;
            _castSlot = -1;
            _movingShot = false;
            _shotTurnsBody = false;
        }

        Cooldown CooldownFor(int slot)
        {
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            return _cooldowns[slot];
        }

        /// <summary>
        /// The basic shot goes off along its aim: it hits the first living target its line touches
        /// (<see cref="LineShot.FirstHit"/>), or misses. Either way it fires: the streak runs to the target it hit,
        /// or out to the full range. True if it dealt damage.
        /// </summary>
        bool ResolveBasicShot()
        {
            BasicShotsFired++;
            FeelValues feel = Feel;
            Look look = LookFor(null, 0f);
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            PlaySound(look); // on a hit or a miss alike

            GroundPoint origin = GroundPosition;
            var aim = new GroundPoint(_aimPoint.x, _aimPoint.z);
            IReadOnlyList<Damageable> all = Damageable.All;
            _targetScratch.Clear();
            for (int i = 0; i < all.Count; i++) _targetScratch.Add(all[i].Info);
            int index = LineShot.FirstHit(origin, aim, feel.BasicShotRange, feel.BasicShotHalfWidth, _targetScratch);
            Damageable hit = index >= 0 ? all[index] : null;

            Vector3 endPoint = hit != null ? hit.transform.position : _aimPoint;
            Vector3 end = new Vector3(endPoint.x, muzzle.y, endPoint.z);
            SpawnFlash(look, muzzle, end - muzzle);
            SpawnStreak(look, muzzle, end, 1f);
            if (hit == null) return false;
            if (Deal(hit, Placeholders.BasicShotDamage) <= 0) return false;
            SpawnHit(look, hit, muzzle);
            BasicShotHits++;
            return true;
        }

        /// <summary>
        /// Deals damage to a target: more while empowered, and what was dealt fills the focus gauge.
        /// Returns the amount actually removed.
        /// </summary>
        int Deal(Damageable target, int damage)
        {
            IdentityValues identity = Identity;
            int applied = target.TakeHit(_focus.Scale(damage, Time.time, identity));
            _focus.AddDamage(applied, identity, Time.time);
            return applied;
        }

        /// <summary>One hit of a skill: every living target its area touches takes the damage. True if any did.</summary>
        bool ResolveSkillHit(SkillDefinition skill, float charge)
        {
            SkillShotsFired++;
            GroundPoint origin = GroundPosition;
            Vector3 flatAim = Flat(_aimPoint - transform.position);
            // With nothing to aim at (the cursor on the character), a line or cone goes where the character faces.
            if (flatAim.sqrMagnitude < 1e-6f && skill.Shape != SkillShape.Circle) flatAim = Flat(transform.forward);
            var aim = new GroundPoint(origin.X + flatAim.x, origin.Z + flatAim.z);

            Look look = LookFor(skill, charge);
            Vector3 shotPoint = SpawnSkillFx(skill, look, origin, aim);
            PlaySound(look); // once per hit of a burst, per tick of a hold, per landing

            int damage = SkillRules.Damage(skill, charge);
            bool landed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!SkillRules.Hits(skill, origin, aim, d.GroundPosition, d.Radius)) continue;
                if (Deal(d, damage) <= 0) continue;
                SpawnHit(look, d, shotPoint);
                SkillShotHits++;
                landed = true;
            }
            return landed;
        }

        // ---------------- Effects: flat, bright shapes that show a shot, and a disc for an area ----------------
        // They are pictures only. The hit has landed when they appear, and nothing they do changes it.
        // How each shot looks is in the ShotFx settings asset, read at the moment of the shot.

        /// <summary>A look as it is used for one shot: scaled by the charge and by the empowered mode, with its glow.</summary>
        struct Look
        {
            public ShotLook Values;
            public float Scale;     // every size
            public float Width;     // streak width and flash size on top of Scale: the charge
            public ShotPaint Paint;
        }

        /// <summary>The look's sound, once, in this frame: its volume times the master, its pitch spread at random.</summary>
        void PlaySound(Look look)
        {
            ShotLook values = look.Values;
            if (Sounds == null || values == null || values.Sound == null) return;
            float master = ShotLooks != null ? ShotLooks.SoundVolume : 1f;
            float spread = Mathf.Max(0f, values.SoundPitchSpread);
            float pitch = values.SoundPitch * (1f + Random.Range(-spread, spread));
            if (IsEmpowered && ShotLooks != null) pitch *= ShotLooks.EmpoweredPitch;
            Sounds.Play(values.Sound, values.SoundVolume * master, pitch, values.SoundStart);
        }

        Look LookFor(SkillDefinition skill, float charge)
        {
            ShotLook values = skill == null
                ? (ShotLooks != null && ShotLooks.Basic != null ? ShotLooks.Basic : _fallbackBasicLook)
                : (ShotLooks != null ? ShotLooks.For(skill.Name) : null) ?? FallbackLook(skill.Name);
            bool empowered = IsEmpowered;
            float empoweredScale = ShotLooks != null ? ShotLooks.EmpoweredScale : ShotFxSettings.StartEmpoweredScale;
            Color rim = empowered ? (ShotLooks != null ? ShotLooks.EmpoweredRim : ShotFxSettings.StartEmpoweredRim) : values.Rim;
            return new Look
            {
                Values = values,
                Scale = empowered ? empoweredScale : 1f,
                Width = skill != null && skill.Input == SkillInput.Charge ? Mathf.Lerp(1f, values.FullChargeScale, charge) : 1f,
                Paint = new ShotPaint(rim, values.Core,
                                      ShotLooks != null ? ShotLooks.RimGlow : ShotFxSettings.StartRimGlow,
                                      ShotLooks != null ? ShotLooks.CoreGlow : ShotFxSettings.StartCoreGlow),
            };
        }

        ShotLook FallbackLook(string skillName)
        {
            if (!_fallbackLooks.TryGetValue(skillName, out ShotLook look))
                _fallbackLooks[skillName] = look = ShotFxDefaults.For(skillName);
            return look;
        }

        /// <summary>How many streaks go side by side for a line of this width: one, or one per spacing across it.</summary>
        public static int StreaksAcross(float width) => Mathf.Max(1, Mathf.FloorToInt(width / LineStreakSpacing + 1e-4f));

        /// <summary>
        /// The barrel end the next shot fires from, taking turns between the hands. Without pistols, a point
        /// over the character's ground point at the height the old beams were drawn at.
        /// </summary>
        Vector3 NextMuzzle(out bool leftHand)
        {
            Transform muzzle = _leftHandNext ? LeftMuzzle : RightMuzzle;
            if (muzzle == null) muzzle = _leftHandNext ? RightMuzzle : LeftMuzzle;
            leftHand = muzzle != null && muzzle == LeftMuzzle;
            _leftHandNext = !_leftHandNext;
            if (muzzle != null) return muzzle.position;
            Vector3 p = transform.position;
            return new Vector3(p.x, FallbackMuzzleHeight, p.z);
        }

        /// <summary>The effects of one hit of a skill. Returns the height and place the hit sparks go from.</summary>
        Vector3 SpawnSkillFx(SkillDefinition skill, Look look, GroundPoint origin, GroundPoint aim)
        {
            Vector3 dir = new Vector3(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

            if (skill.Shape == SkillShape.Circle)
            {
                // The area hit is a thin ring at the circle's edge. The bang is a big burst over the middle,
                // drawn in front of whatever stands in it, and a shockwave that runs out to the edge.
                GroundPoint c = SkillRules.CircleCenter(skill, origin, aim);
                var centre = new Vector3(c.X, 0f, c.Z);
                float blast = look.Values.BlastSeconds;
                ShotFx.Outline(centre, skill.Radius, blast * 1.6f, look.Paint, ShotFxMaterial);
                ShotFx.Ring(centre, skill.Radius, blast, look.Paint, ShotFxMaterial);
                ShotFx.Spark(centre + Vector3.up * 1f, skill.Radius * 1.1f * look.Scale, blast, look.Paint, ShotFxMaterial,
                             towardCamera: skill.Radius + 1f);
                return centre + Vector3.up * 1f;
            }

            // Streaks run level, at the height of the muzzle, and end where the area ends: the range is
            // measured from the character, as the hit is.
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            Vector3 level = new Vector3(origin.X, muzzle.y, origin.Z);
            SpawnFlash(look, muzzle, dir);
            if (skill.Shape == SkillShape.Cone)
            {
                int rays = Mathf.Max(1, look.Values.Rays);
                for (int i = 0; i < rays; i++)
                {
                    // Five or more spread from edge to edge; fewer, as in rapid fire, go anywhere in the cone.
                    float yaw = rays >= 5
                        ? Mathf.Lerp(-skill.Angle * 0.5f, skill.Angle * 0.5f, i / (rays - 1f))
                        : Random.Range(-skill.Angle * 0.5f, skill.Angle * 0.5f);
                    SpawnStreak(look, muzzle, level + Quaternion.Euler(0f, yaw, 0f) * dir * skill.Range, 1f);
                }
                // The blast comes out of the gun, level with the streaks, so it reads as one with them from the
                // camera. It reaches as far as the cone does.
                Vector3 apex = level + dir * Mathf.Max(0f, Vector3.Dot(muzzle - level, dir));
                // A wide flat shape glowing as hard as a thin streak would bloom into a white blob: its edge glows less.
                ShotPaint blast = look.Paint;
                blast.CoreGlow = Mathf.Lerp(1f, blast.CoreGlow, 0.4f);
                ShotFx.Wedge(apex, dir, skill.Angle, skill.Range - Vector3.Distance(apex, level), look.Values.BlastSeconds,
                             blast, ShotFxMaterial);
                return muzzle;
            }

            // A line pierces everything in its strip, so its streaks run its whole range. A wide one sends
            // several side by side, spread evenly across its width.
            float width = skill.HalfWidth * 2f;
            int count = StreaksAcross(width);
            Vector3 across = new Vector3(dir.z, 0f, -dir.x);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = across * ((i + 0.5f) / count - 0.5f) * width;
                SpawnStreak(look, muzzle + offset, level + dir * skill.Range + offset, 1f);
            }
            return muzzle;
        }

        /// <summary>Tells the animation that a pistol fired, which hand and how hard it kicks (the look's recoil, scaled like its flash).</summary>
        void Kick(bool leftHand, Look look)
        {
            KickLeftHand = leftHand;
            KickStrength = look.Values.Recoil * look.Scale * look.Width;
            KickCount++;
        }

        /// <summary>The share of run speed it may walk at now: 1, or eased around a shot on the move.</summary>
        float CurrentSpeedShare(FeelValues feel) =>
            _easing
                ? MovingShot.SpeedShare(_easeFrom, Time.time - _easeStartedAt, _easeFiredAfter, feel.MovingShotSpeedShare,
                                        feel.MovingShotSlowdown, feel.MovingShotRecovery)
                : 1f;

        void SpawnFlash(Look look, Vector3 muzzle, Vector3 direction)
        {
            ShotFx.Flash(muzzle, Flat(direction).sqrMagnitude > 1e-8f ? Flat(direction).normalized : Flat(transform.forward).normalized,
                         look.Values.FlashSize * look.Scale * look.Width, look.Values.FlashSeconds, look.Paint, ShotFxMaterial);
        }

        void SpawnStreak(Look look, Vector3 from, Vector3 to, float widthScale)
        {
            ShotFx.Streak(from, to, look.Values.StreakWidth * look.Scale * look.Width * widthScale, look.Values.StreakSeconds,
                          look.Paint, ShotFxMaterial);
        }

        /// <summary>A spark on the side of a hit target that faces where the shot came from, at the shot's height.</summary>
        void SpawnHit(Look look, Damageable target, Vector3 from)
        {
            Vector3 centre = new Vector3(target.transform.position.x, from.y, target.transform.position.z);
            Vector3 back = Flat(from - centre);
            Vector3 at = back.sqrMagnitude > 1e-6f ? centre + back.normalized * Mathf.Min(target.Radius, back.magnitude) : centre;
            ShotFx.Spark(at, look.Values.HitSize * look.Scale, look.Values.HitSeconds, look.Paint, ShotFxMaterial, target.Radius + 0.2f);
        }

        // ---------------- Facing ----------------

        void FaceMovement(FeelValues feel)
        {
            Vector3 v = Flat(_agent.velocity);
            if (v.sqrMagnitude < 0.01f) return;
            Quaternion want = Quaternion.LookRotation(v.normalized, Vector3.up);
            transform.rotation = feel.TurnTime <= 0f
                ? want
                : Quaternion.RotateTowards(transform.rotation, want, 180f / feel.TurnTime * Time.deltaTime);
        }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 1e-6f) return;
            transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
