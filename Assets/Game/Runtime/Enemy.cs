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
    /// stays gone. From the moment it dies its NavMesh agent is switched off, so it is no obstacle to anyone, and it is
    /// switched on again when it comes back (work order 030: a dead guard's agent, left on in a doorway, made her
    /// avoid an enemy that was no longer there, and the test pilot's flights of 2026-11-20 stood stuck in the door).
    ///
    /// An enemy whose <see cref="Damageable"/> has a stagger bar is dazed when the bar is emptied: it stops where it
    /// is, the attack it was winding up is dropped with no strike, and it takes no step of its brain until the daze
    /// ends. One made from code is tinted while dazed.
    ///
    /// Since work order 011 it wears its kind's model instead of the capsule, with <see cref="EnemyValues.UseModels"/>
    /// on (<see cref="EnemyLook"/>): the clips follow what it does, and dead it plays its Death and lies there a moment
    /// before it is gone. The model's body is what the daze tints and a hit flashes.
    ///
    /// Turns to attack (work order 023, 2026-11-20; pillar 4): one that <see cref="TakesTurns"/> (a generated floor's)
    /// asks <see cref="Turns"/> before it begins a wind-up from the chase, and while it may not it stays in the chase,
    /// doing what it does between attacks (the brute walks up, the shooter keeps its distance and its aim) until its
    /// turn. The practice room's enemies take no turns, and neither does an elite (work order 030).
    ///
    /// An elite (work order 017, <see cref="EnemyFactory.MakeElite"/>) is one of its kind made tougher: more health, its
    /// strikes' damage times <see cref="DamageScale"/>, larger, tinted gold, with a stagger bar; the log names it
    /// <see cref="LogWord"/>.
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

        /// <summary>
        /// The picture of a strike in place of the flat stand-in (work order 024, 2026-11-20, <see cref="CombatEffects"/>):
        /// asked at every strike from where an enemy stands, with the attack and how far it reaches; true when it drew the
        /// strike, false to leave the stand-in. Null, as in a test run: the stand-in. A picture only: it changes nothing.
        /// </summary>
        public static Func<Enemy, EnemyAttackDefinition, float, bool> StrikeDrawer;

        /// <summary>
        /// The same for a strike that goes off away from where the enemy stands (work order 034: a caster's circle as it
        /// bursts), asked with the attack and the place on the ground; true when it drew it. Null: the kind's own stand-in.
        /// </summary>
        public static Func<Enemy, EnemyAttackDefinition, Vector3, bool> StrikeAtDrawer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_All.Clear();
            s_NextId = 0;
            Appeared = null;
            Turns.Clear();
            StrikeDrawer = null;
            StrikeAtDrawer = null;
        }

        /// <summary>
        /// The turns to attack of the enemies that take them (work order 023, <see cref="AttackTurns"/>): one for the
        /// game, as she fights one floor at a time. Its <see cref="AttackTurns.Values"/> turn it off.
        /// </summary>
        public static readonly AttackTurns Turns = new AttackTurns();

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
        /// <summary>
        /// True for an enemy of a generated floor (work order 023): it takes turns to attack (<see cref="Turns"/>). False,
        /// the practice room's enemies: never held back.
        /// </summary>
        [NonSerialized] public bool TakesTurns;
        /// <summary>The renderer of the body, tinted while dazed; set for an enemy made from code (<see cref="EnemyFactory"/>).</summary>
        [NonSerialized] public Renderer BodyRenderer;
        [NonSerialized] public Color BodyColor = Color.white;
        /// <summary>The elite of an elite room (work order 017).</summary>
        [NonSerialized] public bool IsElite;
        /// <summary>Its strikes deal this times their attack's damage (work order 017: an elite's 1.25); 1 for every other enemy.</summary>
        [NonSerialized] public float DamageScale = 1f;

        /// <summary>Its kind as the log writes it, "elite " before it for an elite (work order 017).</summary>
        public string LogWord => (IsElite ? "elite " : "") + EnemyKinds.Word(Kind);

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
        Func<float, EnemyValues, EnemyAttackDefinition> _choose;

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
        /// <summary>
        /// The height of the top of its model's head as shown, or a negative number when it has no model: the model's own
        /// height times its scale (the second reviews of 2026-11-20, work order 033: an elite is shown 4/3 as large, and
        /// its marks, bar, stars and words sat inside its head).
        /// </summary>
        public float ModelHeight => _look != null ? (float)_look.Spec.Height * transform.lossyScale.y : -1f;

        /// <summary>
        /// Tells what it does as it happens: it wakes, starts an attack, strikes (and how that went), gives up, dies,
        /// is staggered and dazed. For the play session recorder; nothing in the game listens, and it changes nothing.
        /// </summary>
        public event Action<EnemyReport> Reported;

        protected void Report(EnemyReportKind kind, EnemyAttackDefinition attack = null, StrikeOutcome outcome = StrikeOutcome.NoEffect,
                              int damage = 0, float stagger = 0f, float staggerLeft = 0f, int count = 0) =>
            Reported?.Invoke(new EnemyReport
            {
                Kind = kind, Attack = attack?.Name, Outcome = outcome, Damage = damage, Id = Id, EnemyKind = Kind,
                Stagger = stagger, StaggerLeft = staggerLeft, Count = count, Elite = IsElite,
            });

        protected virtual void Awake()
        {
            Id = ++s_NextId;
            _self = GetComponent<Damageable>();
            _agent = GetComponent<NavMeshAgent>();
            _agent.updateRotation = false;
            _agent.angularSpeed = 0f;
            _agent.acceleration = 60f;
            _choose = ChooseAttack;
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

        void OnDisable()
        {
            s_All.Remove(this);
            Turns.Forget(Id);
        }

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
            Turns.Ended(Id);
            Halt();
            if (Telegraph != null) Telegraph.enabled = false;
            OnDaze();
            Report(EnemyReportKind.Dazed);
        }

        void OnDazeEnded() => Report(EnemyReportKind.DazeEnded);

        protected virtual void Update()
        {
            Act();
            // The model shows what it did this frame (work order 011); a corpse already hidden is not posed any more
            // (work order 026, the second review of 2026-11-20). No other culling: combat reads the poses.
            if (_look != null && !(_hiddenDead && IsDead)) _look.Tick(this, LookVelocity, Time.time);
        }

        /// <summary>The speed over the ground its legs show: the agent's, unless a kind moves itself (the boss's charge).</summary>
        protected virtual Vector3 LookVelocity => _agent.enabled ? _agent.velocity : Vector3.zero;

        void Act()
        {
            EnemyValues values = Values;
            float now = Time.time;
            if (_agent.enabled) _agent.speed = values.MoveSpeed;

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
            // Its turn (work order 023): asked only when it would begin a wind-up now; held, it goes on with the chase.
            // An elite takes no turn (work order 023's rule, made so in work order 030 now that elites exist).
            TurnSort sort = TakesTurns && !IsElite ? AttackSort : TurnSort.None;
            // An ordinary attack takes no turn (work order 035, AttackTurns.TakesTurn).
            if (before == EnemyState.Chase && mayAttack && Turns.Holds(sort) && playerAlive && distance <= values.LeashRange
                && AttackTurns.TakesTurn(WouldChoose(distance, values)))
                mayAttack = Turns.Ask(Id, sort, now);
            bool strike = _brain.Step(now, distance, playerAlive, _provoked, values, mayAttack, _choose);
            EnemyState state = _brain.State;
            if (before == EnemyState.Windup && state != EnemyState.Windup) Turns.Ended(Id);

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
                if (TakesTurns && AttackTurns.TakesTurn(_brain.Attack)) Turns.Began(Id, sort, now, _brain.Attack.Windup);
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
                    if (ChasesThrough(_brain.Attack)) Chase(toPlayer, distance); // work order 035: the swarmer's bite
                    else Halt();
                    break;
            }
            if (_walking) Face(Flat(_agent.velocity));
            AfterMove();

            DrawTelegraph(now);
        }

        // ---------------- What the kinds change ----------------

        /// <summary>
        /// Its sort for the turns to attack (work order 023): by its kind (<see cref="AttackTurns.SortOf"/>), unless a kind
        /// says otherwise. None, the act boss: never held back.
        /// </summary>
        public virtual TurnSort AttackSort => AttackTurns.SortOf(Kind);

        /// <summary>In the chase, may it start an attack that fits the distance? Asked only in the chase.</summary>
        protected virtual bool MayAttack(Vector3 toPlayer, float distance) => true;

        /// <summary>Where it goes in the chase: straight at her, unless a kind says otherwise.</summary>
        protected virtual void Chase(Vector3 toPlayer, float distance) => Walk(Player.Position);

        /// <summary>
        /// In the chase, the attack for this distance, or null to keep walking (work order 014: the boss picks its own);
        /// the first in its values' list whose band holds her, unless a kind says otherwise.
        /// </summary>
        protected virtual EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values) =>
            EnemyBrain.AttackFor(distance, values);

        /// <summary>
        /// The attack <see cref="ChooseAttack"/> would give now, asked for the turns to attack before the brain's step
        /// (work order 034): the same, unless a kind's choice counts what it gives (the caster's casts), when it must not
        /// count a question.
        /// </summary>
        protected virtual EnemyAttackDefinition WouldChoose(float distance, EnemyValues values) => ChooseAttack(distance, values);

        /// <summary>
        /// At the strike, before anything is hit: true when the kind strikes this attack itself, over time (work order
        /// 014: the boss's charge runs down its line and hits her as it passes, through <see cref="Land"/>). Never, unless
        /// a kind says otherwise.
        /// </summary>
        protected virtual bool StrikesItself(EnemyAttackDefinition attack) => false;

        /// <summary>When it is dazed, after the windup is dropped (work order 014: a charge stops where it is).</summary>
        protected virtual void OnDaze() { }

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

        /// <summary>
        /// True while what it is about to do is read on its body, not on the floor (decision 0010; work order 035, the
        /// melee kinds with their switch on, <see cref="MeleeEnemy"/>): no telegraph and no strike drawn on the ground,
        /// and the effects draw a glint on it instead of the windup's star. Never, unless a kind says otherwise.
        /// </summary>
        public virtual bool ReadOnBody => false;

        /// <summary>Does it keep chasing her through this attack's windup and recovery (work order 035: the swarmer's bite)? Never, unless a kind says so.</summary>
        protected virtual bool ChasesThrough(EnemyAttackDefinition attack) => false;

        // ---------------- Death and the strike ----------------

        void StayDead(float now, EnemyValues values)
        {
            if (_deadSince < 0f)
            {
                Turns.Forget(Id);
                Report(EnemyReportKind.Died);
                _deadSince = now;
                _brain.Reset();
                Halt();
                // No obstacle to anyone from now on (work order 030): its agent takes no part in avoidance while dead.
                _agent.enabled = false;
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
            transform.position = _home;
            _agent.enabled = true; // an obstacle again, at home (work order 030)
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
            if (StrikesItself(attack)) return;
            GroundPoint origin = Ground(transform.position);
            GroundPoint aim = Ground(_aim);
            float reach = Reach(attack);
            if (!ReadOnBody) SpawnStrikeFx(attack, reach); // work order 035: nothing flat on the floor for one read on its body

            // What she is in cover against stopped it (taking cover, 2026-11-19, an experiment): only a kind that says so.
            if (StoppedByHerCover(attack, origin, aim))
            {
                Player.NoteEnemyShotStoppedByCover();
                Report(EnemyReportKind.Struck, attack, StrikeOutcome.StoppedByCover);
                return;
            }
            Land(attack, EnemyBrain.Hits(attack, origin, aim, Player.GroundPosition, Placeholders.PlayerRadius, reach));
        }

        /// <summary>
        /// The strike's end: when it reached her, she takes its damage (none in the dash's i-frame); either way it is
        /// reported. A kind that strikes itself (<see cref="StrikesItself"/>) calls it once for the strike.
        /// </summary>
        protected void Land(EnemyAttackDefinition attack, bool reached)
        {
            if (!reached)
            {
                Report(EnemyReportKind.Struck, attack, StrikeOutcome.OutOfArea);
                return;
            }
            bool invulnerable = Player.IsInvulnerable; // read before the hit: the dash's i-frame is what TakeHit checks
            int damage = DamageScale == 1f ? attack.Damage : Mathf.RoundToInt(attack.Damage * Mathf.Max(0f, DamageScale)); // an elite's (work order 017)
            int applied = PlayerHealth != null ? PlayerHealth.TakeHit(damage) : 0;
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

        /// <summary>
        /// Starts the windup of this attack at once, aimed at <paramref name="aim"/> and locked there, as the brain does
        /// from the chase (work order 014: the angry boss's second charge, straight after the first).
        /// </summary>
        protected void BeginWindup(EnemyAttackDefinition attack, Vector3 aim)
        {
            if (attack == null || _brain.State == EnemyState.Idle) return;
            _brain.Begin(attack, Time.time);
            _aim = aim;
            Face(Flat(aim - transform.position));
            OnWindupStarted(attack);
            Report(EnemyReportKind.AttackStarted, attack);
        }

        /// <summary>Starts its recovery again from now: it stands its whole recovery after a strike that moved it.</summary>
        protected void HoldRecovery() => _brain.HoldRecovery(Time.time);

        protected void Face(Vector3 flatDir)
        {
            if (flatDir.sqrMagnitude < 0.01f) return;
            transform.rotation = Quaternion.LookRotation(flatDir.normalized, Vector3.up);
        }

        // ---------------- What the player sees ----------------

        void DrawTelegraph(float now)
        {
            if (Telegraph == null) return;
            EnemyAttackDefinition attack = _brain.Attack;
            bool show = _brain.State == EnemyState.Windup && attack != null && !ReadOnBody; // work order 035: read on its body
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
            if (StrikeDrawer != null && StrikeDrawer(this, attack, reach)) return; // work order 024: drawn in the shot effects' style
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
