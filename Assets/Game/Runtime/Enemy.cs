using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// What every kind of enemy shares (work order 008): it stands at home until the player comes near or hurts it,
    /// walks on the NavMesh, winds up its attacks with a telegraph on the ground and strikes, gives up and goes home
    /// when she is far away, dies, and tells the play session recorder all of it. What it decides is in
    /// <see cref="EnemyBrain"/>; the kinds (<see cref="EnemyBrute"/>, <see cref="EnemySwarmer"/>,
    /// <see cref="EnemyShooter"/>, <see cref="EnemyGuard"/>) give it their numbers and change only where they walk in
    /// the chase, whether they may attack, and how far a line or a cone reaches.
    ///
    /// The telegraph is a red outline on the ground that thickens until the strike. Its place is locked when the
    /// windup begins, so stepping out of it avoids the hit, and so does the dash's i-frame.
    /// Dead, it is gone for a moment and then stands at home with full health; one with <see cref="ComesBack"/> off
    /// stays gone.
    ///
    /// An enemy whose <see cref="Damageable"/> has a stagger bar is dazed when the bar is emptied: it stops where it
    /// is, the attack it was winding up is dropped with no strike, and it takes no step of its brain until the daze
    /// ends. One made from code is tinted while dazed.
    ///
    /// Since work order 011 it wears its kind's model instead of the capsule, with <see cref="EnemyValues.UseModels"/>
    /// on (<see cref="EnemyLook"/>): the clips follow what it does, and dead it plays its Death and lies there a moment
    /// before it is gone. The model's body is what the daze tints and a hit flashes.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public abstract class Enemy : MonoBehaviour
    {
        const float TelegraphHeight = 0.07f;
        const float TelegraphThin = 0.05f;
        const float TelegraphThick = 0.3f;
        const float StrikeFxSeconds = 0.2f;
        const float HomeSlack = 0.3f;
        const float RepathDistance = 0.25f;
        static readonly Color s_DazedTint = new Color(0.55f, 0.75f, 1f);
        static readonly Color s_CounterBlue = new Color(0.15f, 0.45f, 1f);
        MaterialPropertyBlock _telegraphBlock;

        static readonly List<Enemy> s_All = new List<Enemy>();
        static int s_NextId;

        /// <summary>Every enabled enemy, of every kind, in the order they were enabled.</summary>
        public static IReadOnlyList<Enemy> All => s_All;

        /// <summary>Raised when an enemy is enabled for the first time: for the play session recorder, which listens to each.</summary>
        public static event Action<Enemy> Appeared;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_All.Clear();
            s_NextId = 0;
            Appeared = null;
        }

        public PlayerCharacter Player;
        public PlayerHealth PlayerHealth;
        public LineRenderer Telegraph;
        public Material StrikeMaterial;

        /// <summary>
        /// For an enemy made at run time: the health it has instead of its values' health, when over 0. Set it before
        /// the enemy's first frame. The practice enemy leaves it at 0.
        /// </summary>
        [NonSerialized] public int HealthOverride;
        /// <summary>
        /// False for an enemy that stays dead once killed, as those of a generated level and those spawned in the
        /// practice room do. The practice enemy comes back.
        /// </summary>
        [NonSerialized] public bool ComesBack = true;
        /// <summary>The renderer of the body, tinted while dazed; set for an enemy made from code (<see cref="EnemyFactory"/>).</summary>
        [NonSerialized] public Renderer BodyRenderer;
        [NonSerialized] public Color BodyColor = Color.white;

        protected readonly EnemyBrain _brain = new EnemyBrain();
        EnemyLook _look;
        bool _hiddenDead;
        protected Damageable _self;
        protected NavMeshAgent _agent;
        Renderer[] _renderers;
        Vector3 _home;
        Vector3 _aim;
        Vector3 _destination;
        bool _walking;
        bool _provoked;
        bool _appeared;
        bool _tintedDazed;
        float _deadSince = -1f;

        public abstract EnemyKind Kind { get; }
        public abstract EnemyValues Values { get; }

        /// <summary>Unique among the enemies made since the game started; the recorder tells them apart by it.</summary>
        public int Id { get; private set; }
        public EnemyState State => _brain.State;
        /// <summary>The attack being wound up or recovered from, else null.</summary>
        public EnemyAttackDefinition Attack => _brain.Attack;
        public float WindupRemaining => _brain.WindupRemaining(Time.time);
        public bool IsTelegraphing => Telegraph != null && Telegraph.enabled;
        public bool IsDead => _self == null || !_self.IsAlive;
        public bool IsAwake => _self != null && _self.IsAlive && _brain.State != EnemyState.Idle;
        public bool IsDazed => _self != null && _self.IsDazed;
        public Vector3 Home => _home;
        public Damageable Body => _self;
        public int Strikes { get; private set; }
        public int StrikesLanded { get; private set; }
        /// <summary>Where the current attack was aimed when its windup began.</summary>
        public Vector3 AimPoint => _aim;
        /// <summary>Its model and the clips it plays (work order 011); null when it is the stand-in capsule.</summary>
        public EnemyLook Look => _look;
        /// <summary>The height of the top of its model's head, or a negative number when it has no model.</summary>
        public float ModelHeight => _look != null ? (float)_look.Spec.Height : -1f;

        /// <summary>
        /// Tells what it does as it happens: it wakes, starts an attack, strikes (and how that went), gives up, dies,
        /// is staggered and dazed. For the play session recorder; nothing in the game listens, and it changes nothing.
        /// </summary>
        public event Action<EnemyReport> Reported;

        protected void Report(EnemyReportKind kind, EnemyAttackDefinition attack = null, StrikeOutcome outcome = StrikeOutcome.NoEffect,
                              int damage = 0, float stagger = 0f, float staggerLeft = 0f) =>
            Reported?.Invoke(new EnemyReport
            {
                Kind = kind, Attack = attack?.Name, Outcome = outcome, Damage = damage, Id = Id, EnemyKind = Kind,
                Stagger = stagger, StaggerLeft = staggerLeft,
            });

        protected virtual void Awake()
        {
            Id = ++s_NextId;
            _self = GetComponent<Damageable>();
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.acceleration = 60f;
            if (Values.UseModels) WearModel();
            _renderers = GetComponentsInChildren<Renderer>();
            _home = transform.position;
            _self.Damaged += OnDamaged;
            _self.StaggerTaken += OnStaggerTaken;
            _self.Dazed += OnDazed;
            _self.DazeEnded += OnDazeEnded;
            if (Telegraph != null)
            {
                Telegraph.loop = true;
                Telegraph.useWorldSpace = true;
                Telegraph.enabled = false;
            }
        }

        /// <summary>The health it starts with and comes back with.</summary>
        public int StartHealth => HealthOverride > 0 ? HealthOverride : Values.Health;

        protected virtual void Start() => _self.SetMaxHealth(StartHealth);

        void OnEnable()
        {
            s_All.Add(this);
            if (_appeared) return;
            _appeared = true;
            Appeared?.Invoke(this);
        }

        void OnDisable() => s_All.Remove(this);

        protected virtual void OnDestroy()
        {
            // Taken out of the game awake (its level removed, the practice spawns cleared): the fight with it is over.
            if (_self != null && _self.IsAlive && _brain.State != EnemyState.Idle) Report(EnemyReportKind.Removed);
            if (_self != null)
            {
                _self.Damaged -= OnDamaged;
                _self.StaggerTaken -= OnStaggerTaken;
                _self.Dazed -= OnDazed;
                _self.DazeEnded -= OnDazeEnded;
            }
            if (BodyRenderer != null && BodyRenderer.sharedMaterial != null && BodyRenderer.sharedMaterial.name.StartsWith(EnemyFactory.OwnMaterialPrefix, StringComparison.Ordinal))
                Destroy(BodyRenderer.sharedMaterial);
            _look?.Dispose();
        }

        /// <summary>
        /// Puts its kind's model on it in place of the capsule (work order 011): the body the daze tints and the hit
        /// flashes is then the model's. The capsule stays, switched off; without the model it is left as it was.
        /// </summary>
        void WearModel()
        {
            _look = EnemyLook.Dress(this);
            if (_look == null) return;
            // The capsule's own material, made for this enemy, is no longer shown.
            if (BodyRenderer != null && BodyRenderer.sharedMaterial != null && BodyRenderer.sharedMaterial.name.StartsWith(EnemyFactory.OwnMaterialPrefix, StringComparison.Ordinal))
                Destroy(BodyRenderer.sharedMaterial);
            BodyRenderer = _look.Skin;
            BodyColor = Color.white;
            if (TryGetComponent(out HitFlash flash))
            {
                flash.Targets = _look.Renderers;
                flash.FlashColor = EnemyLook.FlashColor;
            }
        }

        void OnDamaged(int amount) => _provoked = true;

        /// <summary>Wakes it as a hurt would, from wherever she is within its leash: a pack wakes this way.</summary>
        public void Provoke() => _provoked = true;

        /// <summary>Times a counter dropped its attack (work order 015).</summary>
        public int Countered { get; private set; }

        /// <summary>
        /// A counter (work order 015, Stopping Shot): while it winds up a counterable attack and the shooter at
        /// <paramref name="from"/> is within <paramref name="halfAngle"/> degrees of where it faces, the attack is dropped
        /// with no strike and it is dazed for <paramref name="seconds"/>. True if it was.
        /// </summary>
        public bool TryCounter(Vector3 from, float halfAngle, float seconds)
        {
            if (!_self.IsAlive || _brain.State != EnemyState.Windup || !PoolRules.IsCounterable(_brain.Attack)) return false;
            Vector3 f = transform.forward;
            if (!PoolRules.InFront(Ground(transform.position), new GroundPoint(f.x, f.z), Ground(from), halfAngle)) return false;
            Countered++;
            _self.DazeFor(seconds); // OnDazed drops the windup with no strike
            return true;
        }

        /// <summary>True while the telegraph shows a counterable attack: drawn blue, not red (work order 015).</summary>
        public bool TelegraphIsCounterable => IsTelegraphing && PoolRules.IsCounterable(_brain.Attack);

        void OnStaggerTaken(float taken, float left) => Report(EnemyReportKind.StaggerTaken, stagger: taken, staggerLeft: left);

        void OnDazed()
        {
            // The windup is dropped with no strike, and it stands still until the daze ends.
            _brain.Cancel(Time.time);
            Halt();
            if (Telegraph != null) Telegraph.enabled = false;
            Report(EnemyReportKind.Dazed);
        }

        void OnDazeEnded() => Report(EnemyReportKind.DazeEnded);

        protected virtual void Update()
        {
            Act();
            // The model shows what it did this frame (work order 011).
            if (_look != null) _look.Tick(this, _agent.velocity, Time.time);
        }

        void Act()
        {
            EnemyValues values = Values;
            float now = Time.time;
            _agent.speed = values.MoveSpeed;

            if (!_self.IsAlive)
            {
                StayDead(now, values);
                return;
            }
            TintForDaze();
            if (_self.IsDazed)
            {
                Halt();
                DrawTelegraph(now);
                return;
            }

            Vector3 toPlayer = Flat(Player.Position - transform.position);
            float distance = toPlayer.magnitude;
            bool playerAlive = PlayerHealth == null || PlayerHealth.IsAlive;
            EnemyState before = _brain.State;
            bool mayAttack = before != EnemyState.Chase || MayAttack(toPlayer, distance);
            bool strike = _brain.Step(now, distance, playerAlive, _provoked, values, mayAttack);
            EnemyState state = _brain.State;

            if (before == EnemyState.Idle && state != EnemyState.Idle)
            {
                Report(EnemyReportKind.Woke);
                OnWoke();
            }
            if (state == EnemyState.Windup && before != EnemyState.Windup)
            {
                // The attack is aimed once, here. The player can leave the area until the strike.
                _aim = Player.Position;
                Face(toPlayer);
                OnWindupStarted(_brain.Attack);
                Report(EnemyReportKind.AttackStarted, _brain.Attack);
            }
            if (state == EnemyState.Idle && before != EnemyState.Idle)
            {
                _provoked = false;
                _self.Stagger?.Reset(); // gone home: a fresh bar for the next fight
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
                    Chase(toPlayer, distance);
                    break;
                default:
                    Halt();
                    break;
            }
            if (_walking) Face(Flat(_agent.velocity));
            AfterMove();

            DrawTelegraph(now);
        }

        // ---------------- What the kinds change ----------------

        /// <summary>In the chase, may it start an attack that fits the distance? Asked only in the chase.</summary>
        protected virtual bool MayAttack(Vector3 toPlayer, float distance) => true;

        /// <summary>Where it goes in the chase: straight at her, unless a kind says otherwise.</summary>
        protected virtual void Chase(Vector3 toPlayer, float distance) => Walk(Player.Position);

        /// <summary>When it wakes from standing at home.</summary>
        protected virtual void OnWoke() { }

        /// <summary>When a windup begins, after the aim is locked.</summary>
        protected virtual void OnWindupStarted(EnemyAttackDefinition attack) { }

        /// <summary>After it has walked this frame, alive and not dazed.</summary>
        protected virtual void AfterMove() { }

        /// <summary>How far a line or a cone of this attack reaches now: its length, unless something cuts it short.</summary>
        protected virtual float Reach(EnemyAttackDefinition attack) => attack.Length;

        /// <summary>
        /// At the strike: would it have reached her, but what she is in cover against stops it (taking cover, Kristian
        /// 2026-11-19, an experiment)? Never, unless a kind says otherwise: strikes on the ground ignore cover.
        /// </summary>
        protected virtual bool StoppedByHerCover(EnemyAttackDefinition attack, GroundPoint origin, GroundPoint aim) => false;

        // ---------------- Death and the strike ----------------

        void StayDead(float now, EnemyValues values)
        {
            if (_deadSince < 0f)
            {
                Report(EnemyReportKind.Died);
                _deadSince = now;
                _brain.Reset();
                Halt();
                if (Telegraph != null) Telegraph.enabled = false;
                _hiddenDead = false;
            }
            // The capsule is gone at once; a model plays its Death and lies there a moment first.
            if (!_hiddenDead && now - _deadSince >= ShownDeadFor(values))
            {
                _hiddenDead = true;
                SetVisible(false);
            }
            if (!ComesBack || now - _deadSince < values.RespawnDelay) return;

            _deadSince = -1f;
            _provoked = false;
            _agent.Warp(_home);
            _self.SetMaxHealth(StartHealth);
            SetVisible(true);
        }

        /// <summary>Seconds a dead enemy is still seen: none for the capsule; the Death and a moment lying for a model, until it comes back at the latest.</summary>
        public float ShownDeadFor(EnemyValues values)
        {
            if (_look == null) return 0f;
            float shown = _look.DeathSeconds + EnemyLook.LieSeconds;
            return ComesBack ? Mathf.Min(shown, values.RespawnDelay) : shown;
        }

        void Strike(EnemyAttackDefinition attack)
        {
            Strikes++;
            GroundPoint origin = Ground(transform.position);
            GroundPoint aim = Ground(_aim);
            float reach = Reach(attack);
            SpawnStrikeFx(attack, reach);

            // What she is in cover against stopped it (taking cover, 2026-11-19, an experiment): only a kind that says so.
            if (StoppedByHerCover(attack, origin, aim))
            {
                Player.NoteEnemyShotStoppedByCover();
                Report(EnemyReportKind.Struck, attack, StrikeOutcome.StoppedByCover);
                return;
            }
            if (!EnemyBrain.Hits(attack, origin, aim, Player.GroundPosition, Placeholders.PlayerRadius, reach))
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

        protected void Walk(Vector3 target)
        {
            if (_walking && Flat(target - _destination).magnitude < RepathDistance) return;
            if (!_agent.isOnNavMesh) return;
            _destination = target;
            _walking = true;
            _agent.isStopped = false;
            _agent.SetDestination(target);
        }

        protected void Halt()
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
            // A counterable attack is drawn blue instead of red (work order 015); an empty block gives the material's red.
            if (_telegraphBlock == null) _telegraphBlock = new MaterialPropertyBlock();
            _telegraphBlock.Clear();
            if (PoolRules.IsCounterable(attack))
            {
                _telegraphBlock.SetColor("_BaseColor", s_CounterBlue);
                _telegraphBlock.SetColor("_Color", s_CounterBlue);
            }
            Telegraph.SetPropertyBlock(_telegraphBlock);
            Vector3 origin = Flat(transform.position);
            switch (attack.Shape)
            {
                case EnemyAttackShape.Circle:
                    GroundOutline.Circle(Telegraph, origin, attack.Radius, TelegraphHeight);
                    break;
                case EnemyAttackShape.Cone:
                    GroundOutline.Cone(Telegraph, origin, AimDirection(), Reach(attack), attack.Angle, TelegraphHeight);
                    break;
                default:
                    GroundOutline.Strip(Telegraph, origin, AimDirection(), Reach(attack), attack.HalfWidth, TelegraphHeight);
                    break;
            }
        }

        void SpawnStrikeFx(EnemyAttackDefinition attack, float reach)
        {
            Vector3 origin = Flat(transform.position);
            GameObject fx;
            switch (attack.Shape)
            {
                case EnemyAttackShape.Circle:
                    fx = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    fx.transform.position = origin + Vector3.up * 0.04f;
                    fx.transform.localScale = new Vector3(attack.Radius * 2f, 0.02f, attack.Radius * 2f);
                    Destroy(fx.GetComponent<Collider>());
                    break;
                case EnemyAttackShape.Cone:
                    fx = EnemyFactory.Wedge(origin + Vector3.up * 0.04f, AimDirection(), reach, attack.Angle, StrikeFxSeconds);
                    break;
                default:
                {
                    Vector3 dir = AimDirection();
                    fx = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    fx.transform.SetPositionAndRotation(origin + dir * (reach * 0.5f) + Vector3.up * 0.04f,
                                                        Quaternion.LookRotation(dir, Vector3.up));
                    fx.transform.localScale = new Vector3(attack.HalfWidth * 2f, 0.04f, reach);
                    Destroy(fx.GetComponent<Collider>());
                    break;
                }
            }
            fx.name = "StrikeFx";
            if (StrikeMaterial != null) fx.GetComponent<Renderer>().sharedMaterial = StrikeMaterial;
            Destroy(fx, StrikeFxSeconds);
        }

        protected Vector3 AimDirection()
        {
            Vector3 dir = Flat(_aim - transform.position);
            return dir.sqrMagnitude > 1e-6f ? dir.normalized : Flat(transform.forward).normalized;
        }

        void TintForDaze()
        {
            bool dazed = _self.IsDazed;
            if (dazed == _tintedDazed || BodyRenderer == null) return;
            _tintedDazed = dazed;
            EnemyFactory.Paint(BodyRenderer.sharedMaterial, dazed ? s_DazedTint : BodyColor);
        }

        void SetVisible(bool visible)
        {
            foreach (Renderer r in _renderers)
            {
                if (r != null && r != Telegraph) r.enabled = visible;
            }
        }

        protected static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
        protected static GroundPoint Ground(Vector3 v) => new GroundPoint(v.x, v.z);
    }
}
