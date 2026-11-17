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
    /// <see cref="DashToward"/>, <see cref="PressSkill"/>, <see cref="ReleaseSkill"/>, <see cref="AimAt"/>
    /// and <see cref="Stop"/>.
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
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        const float ArriveDistance = 0.05f;
        const float BeamHeight = 0.6f;
        const float BeamSeconds = 0.1f;
        const float BasicBeamWidth = 0.1f;
        const float DiscSeconds = 0.2f;

        public FeelSettings Settings;
        public SkillSettings SkillSet;
        public Material BasicShotMaterial;
        public Material SkillMaterial;

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
        Vector3 _cursorPoint;
        bool _hasCursor;
        bool _bufferedSkillReleased;

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
        public bool IsDashing => _dashing;
        /// <summary>True while the character cannot start anything new: in a dash or a skill. A basic shot does not lock.</summary>
        public bool IsLocked => _dashing || _actionKind == ActionKind.Skill;
        /// <summary>True from the moment a basic shot has gone off until its recovery is over or is cut short.</summary>
        public bool IsInShotRecovery => _actionKind == ActionKind.BasicShot && _action.HitResolved;
        public bool IsInvulnerable => _dash.IsInvulnerable(Time.time);
        public float DashCooldownRemaining => _dash.CooldownRemaining(Time.time);
        public bool HasBufferedPress => _buffer.HasItem;

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
        public void AttackMoveTo(Vector3 point) =>
            SetOrder(OrderKind.AttackMove, point, NearestTo(new GroundPoint(point.x, point.z), Feel.AttackMoveClickRadius));

        /// <summary>Order: shoot this target, walking into range first.</summary>
        public void AttackTarget(Damageable target)
        {
            if (target == null) return;
            SetOrder(OrderKind.AttackTarget, target.transform.position, target);
        }

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
                if (_action.ConsumeHit(now))
                {
                    // Only a shot that went off makes the next one wait.
                    _nextShotAt = _shotStartedAt + _action.Total;
                    if (ResolveBasicShot()) HitStop.Trigger(feel.HitStop);
                }
                // Once the shot has gone off, an order that came in during it does not wait for the recovery.
                bool cutShort = _action.HitResolved && (_orderedDuringShot || _buffer.HasItem);
                if (_action.IsRunning(now) && !cutShort) return;
                _action.Cancel();
                EndAction();
            }
            else if (_actionKind == ActionKind.Skill)
            {
                SkillDefinition skill = _cast.Skill;
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

            // Free from here on: first a buffered press, then the standing order.
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
            _agent.speed = feel.RunSpeed;
            _agent.acceleration = feel.TimeToFullSpeed > 0f ? feel.RunSpeed / feel.TimeToFullSpeed : 10000f;
        }

        // ---------------- Orders ----------------

        void SetOrder(OrderKind kind, Vector3 point, Damageable target)
        {
            if (_actionKind == ActionKind.BasicShot)
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
            Face(Flat(target.transform.position - transform.position));
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

        void StartBasicShot(Damageable target, FeelValues feel)
        {
            Halt();
            _shotTarget = target;
            _aimPoint = target.transform.position;
            Face(Flat(_aimPoint - transform.position));
            _actionKind = ActionKind.BasicShot;
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
            Halt();
            _shotTarget = null;
            _aimPoint = point;
            _castSlot = slot;
            _hasCursor = false; // follow only a cursor that is reported after this skill started, never a stale one
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

            SpawnBeam(transform.position, _shotTarget.transform.position, BasicBeamWidth, BasicShotMaterial);
            if (_shotTarget.TakeHit(Placeholders.BasicShotDamage) <= 0) return false;
            BasicShotHits++;
            return true;
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

            SpawnSkillFx(skill, origin, aim);

            int damage = SkillRules.Damage(skill, charge);
            bool landed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!SkillRules.Hits(skill, origin, aim, d.GroundPosition, d.Radius)) continue;
                if (d.TakeHit(damage) <= 0) continue;
                SkillShotHits++;
                landed = true;
            }
            return landed;
        }

        // ---------------- Effects: grey-box shapes that show the area that was hit ----------------

        void SpawnSkillFx(SkillDefinition skill, GroundPoint origin, GroundPoint aim)
        {
            Vector3 from = transform.position;
            Vector3 dir = new Vector3(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

            switch (skill.Shape)
            {
                case SkillShape.Circle:
                    GroundPoint c = SkillRules.CircleCenter(skill, origin, aim);
                    SpawnDisc(new Vector3(c.X, 0f, c.Z), skill.Radius, SkillMaterial);
                    break;
                case SkillShape.Cone:
                    const int rays = 5;
                    for (int i = 0; i < rays; i++)
                    {
                        float yaw = Mathf.Lerp(-skill.Angle * 0.5f, skill.Angle * 0.5f, i / (rays - 1f));
                        SpawnBeam(from, from + Quaternion.Euler(0f, yaw, 0f) * dir * skill.Range, BasicBeamWidth, SkillMaterial);
                    }
                    break;
                default:
                    SpawnBeam(from, from + dir * skill.Range, skill.HalfWidth * 2f, SkillMaterial);
                    break;
            }
        }

        void SpawnDisc(Vector3 groundCenter, float radius, Material material)
        {
            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            fx.name = "ShotFx";
            Destroy(fx.GetComponent<Collider>());
            fx.transform.position = groundCenter + Vector3.up * 0.03f;
            fx.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);
            if (material != null) fx.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(fx, DiscSeconds);
        }

        void SpawnBeam(Vector3 from, Vector3 to, float width, Material material)
        {
            Vector3 delta = Flat(to - from);
            float length = delta.magnitude;
            if (length < 1e-3f) return;

            GameObject fx = GameObject.CreatePrimitive(PrimitiveType.Cube);
            fx.name = "ShotFx";
            Destroy(fx.GetComponent<Collider>());
            Vector3 start = new Vector3(from.x, BeamHeight, from.z);
            fx.transform.SetPositionAndRotation(start + delta * 0.5f, Quaternion.LookRotation(delta / length, Vector3.up));
            fx.transform.localScale = new Vector3(width, 0.1f, length);
            if (material != null) fx.GetComponent<Renderer>().sharedMaterial = material;
            Destroy(fx, BeamSeconds);
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
