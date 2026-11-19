using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The one enemy of G4: it walks up to the player and strikes with telegraphed attacks.
    /// What it decides is in <see cref="EnemyBrain"/>; this component walks, draws the telegraph and lands the hit.
    ///
    /// The telegraph is a red outline on the ground that thickens until the strike. Its place is locked
    /// when the windup begins, so stepping out of it avoids the hit, and so does the dash's i-frame.
    /// It stands at home until the player comes near or hurts it, and goes home when the player is far away.
    /// Dead, it is gone for a moment and then stands at home with full health; one with <see cref="ComesBack"/> off
    /// stays gone.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyBrute : MonoBehaviour
    {
        const float TelegraphHeight = 0.07f;
        const float TelegraphThin = 0.05f;
        const float TelegraphThick = 0.3f;
        const float StrikeFxSeconds = 0.2f;
        const float HomeSlack = 0.3f;
        const float RepathDistance = 0.25f;

        public EnemySettings Settings;
        public PlayerCharacter Player;
        public PlayerHealth PlayerHealth;
        public LineRenderer Telegraph;
        public Material StrikeMaterial;

        /// <summary>
        /// For an enemy made at run time (a generated level's): the health it has instead of the settings' health,
        /// when over 0. Set it before the enemy's first frame. The practice enemy leaves it at 0.
        /// </summary>
        [System.NonSerialized] public int HealthOverride;
        /// <summary>
        /// False for an enemy that stays dead once killed, as a generated level's do. The practice enemy comes back.
        /// </summary>
        [System.NonSerialized] public bool ComesBack = true;

        readonly EnemyBrain _brain = new EnemyBrain();
        readonly EnemyValues _fallback = new EnemyValues();

        Damageable _self;
        NavMeshAgent _agent;
        Renderer[] _renderers;
        Vector3 _home;
        Vector3 _aim;
        Vector3 _destination;
        bool _walking;
        bool _provoked;
        float _deadSince = -1f;

        public EnemyValues Values => Settings != null ? Settings.Values : _fallback;
        public EnemyState State => _brain.State;
        /// <summary>The attack being wound up or recovered from, else null.</summary>
        public EnemyAttackDefinition Attack => _brain.Attack;
        public float WindupRemaining => _brain.WindupRemaining(Time.time);
        public bool IsTelegraphing => Telegraph != null && Telegraph.enabled;
        public bool IsDead => !_self.IsAlive;
        public Vector3 Home => _home;
        public int Strikes { get; private set; }
        public int StrikesLanded { get; private set; }

        /// <summary>
        /// Tells what it does as it happens: it wakes, starts an attack, strikes (and how that went), gives up, dies.
        /// For the play session recorder; nothing in the game listens, and it changes nothing.
        /// </summary>
        public event System.Action<EnemyReport> Reported;

        void Report(EnemyReportKind kind, EnemyAttackDefinition attack = null, StrikeOutcome outcome = StrikeOutcome.NoEffect, int damage = 0) =>
            Reported?.Invoke(new EnemyReport { Kind = kind, Attack = attack?.Name, Outcome = outcome, Damage = damage });

        void Awake()
        {
            _self = GetComponent<Damageable>();
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.acceleration = 60f;
            _renderers = GetComponentsInChildren<Renderer>();
            _home = transform.position;
            _self.Damaged += OnDamaged;
            if (Telegraph != null)
            {
                Telegraph.loop = true;
                Telegraph.useWorldSpace = true;
                Telegraph.enabled = false;
            }
        }

        /// <summary>The health it starts with and comes back with.</summary>
        public int StartHealth => HealthOverride > 0 ? HealthOverride : Values.Health;

        void Start() => _self.SetMaxHealth(StartHealth);

        void OnDestroy()
        {
            if (_self != null) _self.Damaged -= OnDamaged;
        }

        void OnDamaged(int amount) => _provoked = true;

        void Update()
        {
            EnemyValues values = Values;
            float now = Time.time;
            _agent.speed = values.MoveSpeed;

            if (!_self.IsAlive)
            {
                StayDead(now, values);
                return;
            }

            Vector3 toPlayer = Flat(Player.Position - transform.position);
            bool playerAlive = PlayerHealth == null || PlayerHealth.IsAlive;
            EnemyState before = _brain.State;
            bool strike = _brain.Step(now, toPlayer.magnitude, playerAlive, _provoked, values);
            EnemyState state = _brain.State;

            if (before == EnemyState.Idle && state != EnemyState.Idle) Report(EnemyReportKind.Woke);
            if (state == EnemyState.Windup && before != EnemyState.Windup)
            {
                // The attack is aimed once, here. The player can leave the area until the strike.
                _aim = Player.Position;
                Face(toPlayer);
                Report(EnemyReportKind.AttackStarted, _brain.Attack);
            }
            if (state == EnemyState.Idle && before != EnemyState.Idle)
            {
                _provoked = false;
                Report(EnemyReportKind.GaveUp);
            }
            if (strike) Strike(_brain.Attack);

            switch (state)
            {
                case EnemyState.Idle:
                    if (Flat(_home - transform.position).magnitude > HomeSlack) Walk(_home);
                    else Halt();
                    break;
                case EnemyState.Chase:
                    Walk(Player.Position);
                    break;
                default:
                    Halt();
                    break;
            }
            if (_walking) Face(Flat(_agent.velocity));

            DrawTelegraph(now);
        }

        void StayDead(float now, EnemyValues values)
        {
            if (_deadSince < 0f)
            {
                Report(EnemyReportKind.Died);
                _deadSince = now;
                _brain.Reset();
                Halt();
                if (Telegraph != null) Telegraph.enabled = false;
                SetVisible(false);
            }
            if (!ComesBack || now - _deadSince < values.RespawnDelay) return;

            _deadSince = -1f;
            _provoked = false;
            _agent.Warp(_home);
            _self.SetMaxHealth(StartHealth);
            SetVisible(true);
        }

        void Strike(EnemyAttackDefinition attack)
        {
            Strikes++;
            GroundPoint origin = Ground(transform.position);
            GroundPoint aim = Ground(_aim);
            SpawnStrikeFx(attack);

            if (!EnemyBrain.Hits(attack, origin, aim, Player.GroundPosition, Placeholders.PlayerRadius))
            {
                Report(EnemyReportKind.Struck, attack, StrikeOutcome.OutOfArea);
                return;
            }
            bool invulnerable = Player.IsInvulnerable; // read before the hit: the dash's i-frame is what TakeHit checks
            int applied = PlayerHealth != null ? PlayerHealth.TakeHit(attack.Damage) : 0;
            if (applied > 0) StrikesLanded++;
            Report(EnemyReportKind.Struck, attack,
                   applied > 0 ? StrikeOutcome.Landed : invulnerable ? StrikeOutcome.AvoidedByDash : StrikeOutcome.NoEffect, applied);
        }

        // ---------------- Walking ----------------

        void Walk(Vector3 target)
        {
            if (_walking && Flat(target - _destination).magnitude < RepathDistance) return;
            _destination = target;
            _walking = true;
            _agent.isStopped = false;
            _agent.SetDestination(target);
        }

        void Halt()
        {
            if (!_walking) return;
            _walking = false;
            if (_agent.isOnNavMesh) _agent.ResetPath();
            _agent.velocity = Vector3.zero;
        }

        void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        // ---------------- What the player sees ----------------

        void DrawTelegraph(float now)
        {
            if (Telegraph == null) return;
            EnemyAttackDefinition attack = _brain.Attack;
            bool show = _brain.State == EnemyState.Windup && attack != null;
            if (Telegraph.enabled != show) Telegraph.enabled = show;
            if (!show) return;

            Telegraph.widthMultiplier = Mathf.Lerp(TelegraphThin, TelegraphThick, _brain.WindupProgress(now));
            Vector3 origin = Flat(transform.position);
            if (attack.Shape == EnemyAttackShape.Circle)
                GroundOutline.Circle(Telegraph, origin, attack.Radius, TelegraphHeight);
            else
                GroundOutline.Strip(Telegraph, origin, AimDirection(), attack.Length, attack.HalfWidth, TelegraphHeight);
        }

        void SpawnStrikeFx(EnemyAttackDefinition attack)
        {
            Vector3 origin = Flat(transform.position);
            GameObject fx;
            if (attack.Shape == EnemyAttackShape.Circle)
            {
                fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                fx.transform.position = origin + Vector3.up * 0.04f;
                fx.transform.localScale = new Vector3(attack.Radius * 2f, 0.02f, attack.Radius * 2f);
            }
            else
            {
                Vector3 dir = AimDirection();
                fx = GameObject.CreatePrimitive(PrimitiveType.Cube);
                fx.transform.SetPositionAndRotation(origin + dir * (attack.Length * 0.5f) + Vector3.up * 0.04f,
                                                    Quaternion.LookRotation(dir, Vector3.up));
                fx.transform.localScale = new Vector3(attack.HalfWidth * 2f, 0.04f, attack.Length);
            }
            fx.name = "StrikeFx";
            Destroy(fx.GetComponent<Collider>());
            if (StrikeMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = StrikeMaterial;
            Destroy(fx, StrikeFxSeconds);
        }

        Vector3 AimDirection()
        {
            Vector3 dir = Flat(_aim - transform.position);
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Flat(transform.forward).normalized;
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer r in _renderers)
            {
                if (r != null && r != Telegraph) r.enabled = visible;
            }
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static GroundPoint Ground(Vector3 v) => new GroundPoint(v.x, v.z);
    }
}
