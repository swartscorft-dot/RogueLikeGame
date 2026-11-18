using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>What the character has been told to do. An order stays until it is done or replaced.</summary>
    public enum OrderKind { None, Move, AttackMove, AttackTarget }

    /// <summary>The committed action the character is locked in, if any.</summary>
    public enum ActionKind { None, BasicShot, Skill }

    public enum IntentResult { Started, Buffered, Rejected }

    /// <summary>
    /// The player character. Everything that drives it goes through the intent API:
    /// <see cref="MoveTo"/>, <see cref="AttackMoveTo"/>, <see cref="AttackTarget"/>,
    /// <see cref="DashToward"/>, <see cref="PressSkill"/>, <see cref="ReleaseSkill"/>, <see cref="AimAt"/>,
    /// <see cref="ActivateIdentity"/> and <see cref="Stop"/>.
    /// Input and tests both call these; nothing else moves the character.
    ///
    /// Orders (move, attack-move, attack-target) stay until done or replaced, and wait for a lock to end.
    /// Presses (dash, skill) made during a lock go through the input buffer and expire.
    /// A skill locks the character, and only the dash cancels it.
    ///
    /// The basic shot never locks, as in League. Before it has gone off, a move order, an attack on another
    /// target, a skill or a dash cancels it, and a cancelled shot costs nothing. After it has gone off,
    /// anything takes over at once. A shot that did go off makes the next one wait out the whole shot time,
    /// so stepping between shots never raises the fire rate.
    ///
    /// An attack command while walking on a move order, at a target in basic-attack range, is a shot on the move:
    /// the move goes on, slowed while the shot winds up and back to full speed shortly after it, and one shot is
    /// fired. A new move order changes where it goes and does not cancel that shot; a dash or a skill does.
    /// A basic shot turns the whole body only if the target lies outside the front angle; within it the body keeps
    /// its direction and CharacterAnimation turns the upper body toward the target.
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
        Damageable _orderTarget;
        bool _travelDirty = true;
        Vector3 _travelPoint;

        ActionKind _actionKind;
        Damageable _shotTarget;
        Vector3 _aimPoint;
        int _castSlot = -1;
        float _nextShotAt = float.NegativeInfinity; // the earliest the next basic shot may start
        float _shotStartedAt;
        bool _orderedDuringShot;                    // a new order came in while a basic shot was going
        bool _movingShot;                           // the basic shot in progress is a shot on the move
        bool _shotTurnsBody;                        // the basic shot in progress turned the whole body to its target
        float _moveShotFiredAt = float.NegativeInfinity;
        Damageable _pendingShotTarget;              // an attack command on the move that came before the next shot was allowed
        float _pendingShotAt;
        Vector3 _cursorPoint;
        bool _hasCursor;
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

        public OrderKind Order => _order;
        /// <summary>The target the order goes for: the chosen one (attack-target) or the one the click picked (attack-move).</summary>
        public Damageable OrderTarget => _orderTarget;
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
        /// <summary>True while a basic shot is fired on the move: the move order goes on under it.</summary>
        public bool IsShootingOnTheMove => _actionKind == ActionKind.BasicShot && _movingShot;
        /// <summary>True while the basic shot in progress turned the whole body to its target (it lay outside the front angle).</summary>
        public bool ShotTurnsBody => _actionKind == ActionKind.BasicShot && _shotTurnsBody;
        /// <summary>The speed the character may walk at now: the run speed, or less while it shoots on the move.</summary>
        public float SpeedLimit => _agent.speed;
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

        /// <summary>Order: walk to a point.</summary>
        public void MoveTo(Vector3 point) => SetOrder(OrderKind.Move, point, null);

        /// <summary>
        /// Order: attack-move to a point. The click picks the target: the living target nearest the point,
        /// if one is within the attack-move click radius. The character walks into range of it and shoots it,
        /// leaving nearer targets alone. With no target near the point, or once that target is dead,
        /// it walks to the point and shoots the target nearest to itself that is in basic-attack range.
        /// </summary>
        public void AttackMoveTo(Vector3 point)
        {
            Damageable picked = NearestTo(new GroundPoint(point.x, point.z), Feel.AttackMoveClickRadius);
            if (picked != null && TryShootOnTheMove(picked)) return;
            SetOrder(OrderKind.AttackMove, point, picked);
        }

        /// <summary>Order: shoot this target, walking into range first.</summary>
        public void AttackTarget(Damageable target)
        {
            if (target == null) return;
            if (TryShootOnTheMove(target)) return;
            SetOrder(OrderKind.AttackTarget, target.transform.position, target);
        }

        /// <summary>
        /// An attack command while walking on a move order, at a target within basic-attack range, does not replace
        /// the move: the character fires one basic shot on the move and walks on to where it was going. A command
        /// that comes before the next shot is allowed waits as a buffered press does, and lapses after the input
        /// buffer time. Returns false when the command is not one for a shot on the move.
        /// </summary>
        bool TryShootOnTheMove(Damageable target)
        {
            if (!IsWalkingOnMoveOrder || !target.IsAlive) return false;
            if (!Targeting.InRange(GroundPosition, target.GroundPosition, target.Radius, Feel.BasicAttackRange)) return false;
            if (_actionKind == ActionKind.None && Time.time >= _nextShotAt) StartBasicShot(target, Feel, onTheMove: true);
            else
            {
                _pendingShotTarget = target;
                _pendingShotAt = Time.time;
            }
            return true;
        }

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

            if (_actionKind != ActionKind.None)
            {
                if (_actionKind == ActionKind.Skill && !Feel.DashCancelsSkill) return BufferPress(PressKind.Dash, point);
                _action.Cancel();
                _cast.Cancel();
                EndAction();
            }
            _pendingShotTarget = null;
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
        /// </summary>
        public void AimAt(Vector3 point)
        {
            _cursorPoint = point;
            _hasCursor = true;
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
            _buffer.Clear();
            _pendingShotTarget = null;
            _moveShotFiredAt = float.NegativeInfinity;
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
                if (_shotTarget != null) _aimPoint = _shotTarget.transform.position;
                if (_action.ConsumeHit(now))
                {
                    // Only a shot that went off makes the next one wait.
                    _nextShotAt = _shotStartedAt + _action.Total;
                    if (_movingShot) _moveShotFiredAt = now;
                    if (ResolveBasicShot()) HitStop.Trigger(feel.HitStop);
                }
                if (_movingShot)
                {
                    // On the move: the move order goes on under the shot, at the speed ApplyMovementFeel sets.
                    // The body faces the target if the shot turned it, else the way it walks.
                    if (_order == OrderKind.Move)
                    {
                        Travel(_orderPoint);
                        if (Arrived()) ClearOrder();
                    }
                    if (_shotTurnsBody && _shotTarget != null) Face(Flat(_shotTarget.transform.position - transform.position));
                    else FaceMovement(feel);
                    // Once it has gone off, a press or an order other than the move takes over at once.
                    bool takenOver = _action.HitResolved && (_buffer.HasItem || (_order != OrderKind.Move && _order != OrderKind.None));
                    if (_action.IsRunning(now) && !takenOver) return;
                    _action.Cancel();
                    EndAction();
                }
                else
                {
                    // Once the shot has gone off, an order that came in during it does not wait for the recovery.
                    bool cutShort = _action.HitResolved && (_orderedDuringShot || _buffer.HasItem);
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

            // Free from here on: first a buffered press, then a waiting shot on the move, then the standing order.
            if (_pendingShotTarget != null && !_buffer.HasItem)
            {
                Damageable waiting = _pendingShotTarget;
                if (now - _pendingShotAt > feel.InputBuffer || !waiting.IsAlive) _pendingShotTarget = null;
                else if (now >= _nextShotAt)
                {
                    _pendingShotTarget = null;
                    if (IsWalkingOnMoveOrder
                        && Targeting.InRange(GroundPosition, waiting.GroundPosition, waiting.Radius, feel.BasicAttackRange))
                    {
                        StartBasicShot(waiting, feel, onTheMove: true);
                        return;
                    }
                }
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
            FaceMovement(feel);
        }

        void ApplyMovementFeel(FeelValues feel)
        {
            _agent.speed = feel.RunSpeed * MovingShot.SpeedShare(_movingShot && !_action.HitResolved, Time.time - _moveShotFiredAt,
                                                                 feel.MovingShotSpeedShare, feel.MovingShotRecovery);
            // The agent does not slow down by itself when its speed limit drops below how fast it is going.
            if (_agent.velocity.sqrMagnitude > _agent.speed * _agent.speed && !_dashing)
                _agent.velocity = _agent.velocity.normalized * _agent.speed;
            _agent.acceleration = feel.TimeToFullSpeed > 0f ? feel.RunSpeed / feel.TimeToFullSpeed : 10000f;
        }

        // ---------------- Orders ----------------

        void SetOrder(OrderKind kind, Vector3 point, Damageable target)
        {
            if (_actionKind == ActionKind.BasicShot && _movingShot && kind == OrderKind.Move)
            {
                // A new move during a shot on the move only changes where the character is going.
            }
            else if (_actionKind == ActionKind.BasicShot)
            {
                // An attack order that does not name another target lets the shot in progress go off.
                bool keepsShooting = kind != OrderKind.Move && (target == null || target == _shotTarget);
                if (_action.HitResolved || keepsShooting)
                {
                    _orderedDuringShot = true; // it takes over when the shot has gone off
                }
                else
                {
                    _action.Cancel(); // the shot has not gone off: it never will
                    EndAction();
                }
            }
            _order = kind;
            _orderPoint = point;
            _orderTarget = target;
            _travelDirty = true;
        }

        void ClearOrder()
        {
            _order = OrderKind.None;
            _orderTarget = null;
            _travelDirty = true;
        }

        void RunOrder(FeelValues feel)
        {
            switch (_order)
            {
                case OrderKind.Move:
                    Travel(_orderPoint);
                    if (Arrived()) ClearOrder();
                    break;

                case OrderKind.AttackMove:
                    // The target the click picked comes first, for as long as it lives.
                    if (_orderTarget != null && !_orderTarget.IsAlive) _orderTarget = null;
                    if (_orderTarget != null)
                    {
                        ShootOrApproach(_orderTarget, feel);
                        break;
                    }
                    Damageable nearest = NearestTo(GroundPosition, feel.BasicAttackRange);
                    if (nearest != null)
                    {
                        ShootWhenReady(nearest, feel);
                        break;
                    }
                    Travel(_orderPoint);
                    if (Arrived()) ClearOrder();
                    break;

                case OrderKind.AttackTarget:
                    if (_orderTarget == null || !_orderTarget.IsAlive)
                    {
                        ClearOrder();
                        Halt();
                        break;
                    }
                    ShootOrApproach(_orderTarget, feel);
                    break;
            }
        }

        void ShootOrApproach(Damageable target, FeelValues feel)
        {
            if (Targeting.InRange(GroundPosition, target.GroundPosition, target.Radius, feel.BasicAttackRange))
                ShootWhenReady(target, feel);
            else
                Travel(target.transform.position);
        }

        /// <summary>
        /// Shoots, or stands facing the target until the shot time of the last shot is over.
        /// That wait is what keeps a cut-short recovery from raising the fire rate.
        /// </summary>
        void ShootWhenReady(Damageable target, FeelValues feel)
        {
            if (Time.time >= _nextShotAt)
            {
                StartBasicShot(target, feel);
                return;
            }
            Halt();
            if (BodyMustTurnTo(target, feel)) Face(Flat(target.transform.position - transform.position));
        }

        /// <summary>True when a target lies outside the front angle, so a basic shot at it turns the whole body.</summary>
        bool BodyMustTurnTo(Damageable target, FeelValues feel)
        {
            Vector3 facing = Flat(transform.forward);
            Vector3 toTarget = Flat(target.transform.position - transform.position);
            return UpperBodyAim.BodyMustTurn(new GroundPoint(facing.x, facing.z), new GroundPoint(toTarget.x, toTarget.z), feel.ShotFrontAngle);
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

        /// <summary>The living target whose edge is nearest to a point and within the range of it, or null.</summary>
        Damageable NearestTo(GroundPoint from, float range)
        {
            IReadOnlyList<Damageable> all = Damageable.All;
            _targetScratch.Clear();
            for (int i = 0; i < all.Count; i++) _targetScratch.Add(all[i].Info);
            int index = Targeting.NearestInRange(from, _targetScratch, range);
            return index < 0 ? null : all[index];
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
        /// Starts a basic shot. Standing, the character stops; on the move it walks on. Either way the whole body
        /// turns to the target only if the target lies outside the front angle; within it, the upper body aims.
        /// </summary>
        void StartBasicShot(Damageable target, FeelValues feel, bool onTheMove = false)
        {
            if (!onTheMove) Halt();
            _shotTarget = target;
            _aimPoint = target.transform.position;
            _shotTurnsBody = BodyMustTurnTo(target, feel);
            if (_shotTurnsBody) Face(Flat(_aimPoint - transform.position));
            _actionKind = ActionKind.BasicShot;
            _movingShot = onTheMove;
            if (onTheMove) ShotsOnTheMove++;
            _orderedDuringShot = false;
            ActionsStarted++;
            ActionLeadTime = feel.BasicShotStartup;
            _shotStartedAt = Time.time;
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
        }

        void StartSkill(int slot, Vector3 point)
        {
            SkillDefinition skill = Skills[slot];
            _action.Cancel(); // a skill may start during a basic shot, and ends it
            _movingShot = false;
            _shotTurnsBody = false;
            _pendingShotTarget = null;
            Halt();
            _shotTarget = null;
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
            _shotTarget = null;
            _castSlot = -1;
            _movingShot = false;
            _shotTurnsBody = false;
        }

        Cooldown CooldownFor(int slot)
        {
            while (_cooldowns.Count <= slot) _cooldowns.Add(new Cooldown());
            return _cooldowns[slot];
        }

        bool ResolveBasicShot()
        {
            BasicShotsFired++;
            if (_shotTarget == null || !_shotTarget.IsAlive) return false;

            Look look = LookFor(null, 0f);
            Vector3 muzzle = NextMuzzle();
            Vector3 hit = _shotTarget.transform.position;
            Vector3 end = new Vector3(hit.x, muzzle.y, hit.z);
            SpawnFlash(look, muzzle, end - muzzle);
            SpawnStreak(look, muzzle, end, 1f);
            if (Deal(_shotTarget, Placeholders.BasicShotDamage) <= 0) return false;
            SpawnHit(look, _shotTarget, muzzle);
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
        Vector3 NextMuzzle()
        {
            Transform muzzle = _leftHandNext ? LeftMuzzle : RightMuzzle;
            if (muzzle == null) muzzle = _leftHandNext ? RightMuzzle : LeftMuzzle;
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
            Vector3 muzzle = NextMuzzle();
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
