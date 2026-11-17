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
    /// <see cref="DashToward"/>, <see cref="CastAt"/> and <see cref="Stop"/>.
    /// Input and tests both call these; nothing else moves the character.
    ///
    /// Orders (move, attack-move, attack-target) stay until done or replaced, and wait for a lock to end.
    /// Presses (dash, skill) made during a lock go through the input buffer and expire.
    /// Only the dash cancels a shot or skill in progress.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class PlayerCharacter : MonoBehaviour
    {
        const float ArriveDistance = 0.05f;
        const float BeamHeight = 0.6f;
        const float BeamSeconds = 0.1f;
        const float BasicBeamWidth = 0.1f;

        public FeelSettings Settings;
        public Material BasicShotMaterial;
        public Material SkillMaterial;

        enum PressKind { Dash, Skill }

        struct Press
        {
            public PressKind Kind;
            public Vector3 Point;
        }

        readonly FeelValues _fallbackFeel = new FeelValues();
        readonly Dash _dash = new Dash();
        readonly ActionTimeline _action = new ActionTimeline();
        readonly InputBuffer<Press> _buffer = new InputBuffer<Press>();
        readonly List<TargetInfo> _targetScratch = new List<TargetInfo>();

        NavMeshAgent _agent;

        OrderKind _order;
        Vector3 _orderPoint;
        Damageable _orderTarget;
        bool _travelDirty = true;
        Vector3 _travelPoint;

        ActionKind _actionKind;
        Damageable _shotTarget;
        Vector3 _aimPoint;

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
        public ActionPhase ActionPhase => _actionKind == ActionKind.None ? ActionPhase.None : _action.Phase(Time.time);
        public bool IsDashing => _dashing;
        public bool IsLocked => _dashing || _actionKind != ActionKind.None;
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
                if (!Feel.DashCancelsSkill) return BufferPress(PressKind.Dash, point);
                _action.Cancel();
                EndAction();
            }
            return StartDash(point) ? IntentResult.Started : IntentResult.Rejected;
        }

        /// <summary>Press: cast the placeholder skill at a point. Locks the character for startup, active and recovery.</summary>
        public IntentResult CastAt(Vector3 point)
        {
            if (IsLocked) return BufferPress(PressKind.Skill, point);
            StartSkill(point);
            return IntentResult.Started;
        }

        /// <summary>Put the character somewhere else at once and clear what it was doing. For tests and tools.</summary>
        public void Teleport(Vector3 point)
        {
            _action.Cancel();
            EndAction();
            _dashing = false;
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

            if (_actionKind != ActionKind.None)
            {
                if (_action.ConsumeHit(now)) ResolveHit(feel);
                if (_action.IsRunning(now)) return;
                EndAction();
            }

            // Free from here on: first a buffered press, then the standing order.
            if (_buffer.TryTake(now, feel.InputBuffer, out Press press))
            {
                if (press.Kind == PressKind.Skill)
                {
                    StartSkill(press.Point);
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
                        StartBasicShot(nearest, feel);
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
                StartBasicShot(target, feel);
            else
                Travel(target.transform.position);
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

        IntentResult BufferPress(PressKind kind, Vector3 point)
        {
            _buffer.Store(new Press { Kind = kind, Point = point }, Time.time);
            return IntentResult.Buffered;
        }

        void StartBasicShot(Damageable target, FeelValues feel)
        {
            Halt();
            _shotTarget = target;
            _aimPoint = target.transform.position;
            Face(Flat(_aimPoint - transform.position));
            BeginAction(ActionKind.BasicShot, feel);
        }

        void StartSkill(Vector3 point)
        {
            FeelValues feel = Feel;
            Halt();
            _shotTarget = null;
            _aimPoint = point;
            if (feel.FaceCursorOnCast) Face(Flat(point - transform.position));
            BeginAction(ActionKind.Skill, feel);
        }

        void BeginAction(ActionKind kind, FeelValues feel)
        {
            _actionKind = kind;
            ActionsStarted++;
            // G2: the placeholder skill uses the basic shot's timings.
            _action.Start(Time.time, feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
        }

        void EndAction()
        {
            _actionKind = ActionKind.None;
            _shotTarget = null;
        }

        void ResolveHit(FeelValues feel)
        {
            bool landed = _actionKind == ActionKind.BasicShot ? ResolveBasicShot() : ResolveSkill();
            if (landed) HitStop.Trigger(feel.HitStop);
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

        bool ResolveSkill()
        {
            SkillShotsFired++;
            Vector3 dir = Flat(_aimPoint - transform.position);
            if (dir.sqrMagnitude < 1e-6f) return false;
            dir.Normalize();

            SpawnBeam(transform.position, transform.position + dir * Placeholders.SkillRange,
                      Placeholders.SkillHalfWidth * 2f, SkillMaterial);

            GroundPoint origin = GroundPosition;
            var aim = new GroundPoint(_aimPoint.x, _aimPoint.z);
            bool landed = false;
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (!LineShot.Hits(origin, aim, Placeholders.SkillRange, Placeholders.SkillHalfWidth, d.GroundPosition, d.Radius))
                    continue;
                if (d.TakeHit(Placeholders.SkillDamage) <= 0) continue;
                SkillShotHits++;
                landed = true;
            }
            return landed;
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
