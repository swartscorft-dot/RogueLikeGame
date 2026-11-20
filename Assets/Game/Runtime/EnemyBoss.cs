using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The act boss (work order 014, 2026-11-20): the crypt knight, 3 m tall, with a cleaver on a pole. Its numbers are
    /// <see cref="BossValues"/>, what it decides <see cref="BossTactics"/>.
    ///
    /// It wakes when she enters its room (<see cref="InRoom"/>, set by the level), comes near or hurts it, and never
    /// gives up while she is alive. Close it cleaves a wide cone in front of it or spins a circle round it, taking turns
    /// when both reach; farther, when it sees her (no wall or tall prop between, as the shooter's sight), it charges:
    /// a line toward where she stood when the windup began, shown and locked for the windup, cut at the first wall or
    /// tall prop and at the end of the walkable ground (a NavMesh raycast along it before it starts), a little short of
    /// either. At the strike it runs down the line at the attack's <see cref="EnemyAttackDefinition.Rush"/>, moved by its
    /// agent so it never leaves the NavMesh, hits her once if it passes over her, and stands its recovery where it stops.
    /// With nothing that fits, it walks toward her. Every attack's recovery is her opening.
    ///
    /// It carries a stagger bar as the guard does, half again as large; emptied, it is dazed as the guard is (a charge
    /// stops where it is). Below half its health it turns angry, once: its body is tinted warm, every windup is shorter,
    /// and a charge is followed at once by a second at where she stands then. At two thirds and one third of its health
    /// it calls a pack of swarmers, once each: the level puts them along its room's walls (<see cref="PackCaller"/>);
    /// without one (the practice room) they come round it. They come for her awake.
    ///
    /// Its bar on screen is <see cref="BossHealthBar"/>.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyBoss : Enemy
    {
        static readonly Color s_AngryTint = new Color(1f, 0.55f, 0.32f);

        public BossValues Tuning = new BossValues();
        /// <summary>Is a world point inside its room? Set by the level; she entering it wakes it. Null in the practice room.</summary>
        [NonSerialized] public Func<Vector3, bool> InRoom;
        /// <summary>Brings a pack of this many for it and returns them; set by the level. Null: they come round it.</summary>
        [NonSerialized] public Func<EnemyBoss, int, List<Enemy>> PackCaller;
        /// <summary>The practice enemy it was made from, to make the swarmers it calls in the practice room.</summary>
        [NonSerialized] public EnemyBrute Template;

        readonly BossTactics _tactics = new BossTactics();
        readonly List<Enemy> _called = new List<Enemy>();
        EnemyValues _angry;

        float _chargeReach;
        int _chargesLeft;
        bool _chaining;
        bool _rushing;
        bool _rushHit;
        EnemyAttackDefinition _rush;
        Vector3 _rushFrom, _rushDir;
        float _rushStart;

        public override EnemyKind Kind => EnemyKind.Boss;
        /// <summary>Its values; angry, a copy with the shorter windups.</summary>
        public override EnemyValues Values => _angry ?? Tuning.Enemy;
        public StaggerBar Stagger => _self != null ? _self.Stagger : null;
        public bool IsAngry => _tactics.Angry;
        public int PacksCalled => _tactics.Calls;
        /// <summary>The swarmers it has called, dead ones too.</summary>
        public IReadOnlyList<Enemy> Called => _called;
        /// <summary>True while its charge runs down the line.</summary>
        public bool IsCharging => _rushing;
        /// <summary>The length of the charge being wound up or run: its line as cut at the windup's start.</summary>
        public float ChargeReach => _chargeReach;
        /// <summary>Where the charge being run started, and its direction.</summary>
        public Vector3 ChargeFrom => _rushFrom;
        public Vector3 ChargeDirection => _rushDir;
        /// <summary>The share of its health left, 0 to 1.</summary>
        public float HealthShare => _self != null && _self.MaxHealth > 0 ? Mathf.Clamp01(_self.CurrentHealth / (float)_self.MaxHealth) : 0f;

        protected override void Awake()
        {
            base.Awake();
            _self.Stagger = new StaggerBar(Tuning.Stagger);
            if (GetComponent<BossHealthBar>() == null) gameObject.AddComponent<BossHealthBar>();
        }

        protected override void Update()
        {
            if (IsDead) _rushing = false;
            else
            {
                Wake();
                Temper();
            }
            base.Update();
        }

        void Wake()
        {
            if (IsAwake || Player == null || InRoom == null) return;
            if ((PlayerHealth == null || PlayerHealth.IsAlive) && InRoom(Player.Position)) Provoke();
        }

        /// <summary>Its anger and its calls, by the health it has left.</summary>
        void Temper()
        {
            float share = HealthShare;
            if (_tactics.BecomesAngry(share, Tuning))
            {
                _angry = Tuning.Angry();
                BodyColor = s_AngryTint;
                if (!IsDazed && BodyRenderer != null) EnemyFactory.Paint(BodyRenderer.sharedMaterial, BodyColor);
                Report(EnemyReportKind.Angered);
            }
            int due = _tactics.CallsDue(share, Tuning);
            for (int i = 0; i < due; i++) CallPack();
        }

        void CallPack()
        {
            List<Enemy> came = PackCaller != null ? PackCaller(this, Tuning.CallCount) : CallNear(Tuning.CallCount);
            foreach (Enemy e in came)
            {
                if (e == null) continue;
                if (e is EnemySwarmer s) s.Tuning.Enemy.LeashRange = Mathf.Max(s.Tuning.Enemy.LeashRange, Tuning.CalledLeash);
                e.Provoke();
                _called.Add(e);
            }
            Report(EnemyReportKind.CalledPack, count: came.Count);
        }

        /// <summary>A pack round it, on the NavMesh, beside it under the same parent (the practice room's spawns).</summary>
        List<Enemy> CallNear(int count)
        {
            var made = new List<Enemy>();
            if (Template == null) return made;
            List<Vector3> places = EnemyFactory.PackPlaces(Flat(transform.position), count, Tuning.CallNearRadius);
            for (int i = 0; i < places.Count; i++)
            {
                Vector3 p = places[i];
                if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) p = hit.position;
                Enemy e = EnemyFactory.Make(EnemyKind.Swarmer, p, transform.parent, Template, name + "_Called_" + (_called.Count + i + 1));
                if (e != null) made.Add(e);
            }
            return made;
        }

        // ---------------- Its attacks ----------------

        protected override EnemyAttackDefinition ChooseAttack(float distance, EnemyValues values) =>
            _tactics.Choose(distance, Sees(Player.Position), values);

        /// <summary>Does it see that point, with no wall or tall prop between at its sight height (as the shooter does)?</summary>
        public bool Sees(Vector3 point)
        {
            Vector3 from = Flat(transform.position), dir = Flat(point) - from;
            float length = dir.magnitude;
            return length < 1e-3f || Cover.ClearDistance(from, dir / length, length, Tuning.SightHeight) >= length - 1e-3f;
        }

        protected override void OnWindupStarted(EnemyAttackDefinition attack)
        {
            if (attack.Rush <= 0f) return;
            _chargeReach = ChargeLengthFrom(transform.position, AimDirection(), attack.Length);
            if (!_chaining) _chargesLeft = IsAngry ? Mathf.Max(1, Tuning.AngryCharges) : 1;
        }

        /// <summary>
        /// How far a charge from here along a flat direction may run (<see cref="BossTactics.ChargeLength"/>): its length,
        /// cut at the first wall or tall prop and at the first edge of the NavMesh on its line (a low prop is a hole in
        /// it), less <see cref="BossValues.ChargeStopShort"/> when cut.
        /// </summary>
        public float ChargeLengthFrom(Vector3 from, Vector3 dir, float length)
        {
            from = Flat(from);
            float wall = Cover.ClearDistance(from, dir, length, Tuning.SightHeight);
            float walkable = 0f;
            if (NavMesh.SamplePosition(from, out NavMeshHit start, 1f, NavMesh.AllAreas))
            {
                Vector3 end = start.position + dir * length;
                walkable = NavMesh.Raycast(start.position, end, out NavMeshHit edge, NavMesh.AllAreas)
                    ? Flat(edge.position - start.position).magnitude
                    : length;
            }
            return BossTactics.ChargeLength(length, wall, walkable, Tuning.ChargeStopShort);
        }

        /// <summary>The charge's line reaches as far as it was cut; the others their length.</summary>
        protected override float Reach(EnemyAttackDefinition attack) => attack.Rush > 0f ? _chargeReach : attack.Length;

        protected override bool StrikesItself(EnemyAttackDefinition attack)
        {
            if (attack.Rush <= 0f) return false;
            _rush = attack;
            _rushing = true;
            _rushHit = false;
            _rushFrom = transform.position;
            _rushDir = AimDirection();
            _rushStart = Time.time;
            Face(_rushDir);
            Report(EnemyReportKind.Charged, attack);
            return true;
        }

        protected override void AfterMove()
        {
            SendWave(); // work order 036
            if (!_rushing) return;
            HoldRecovery(); // it stands its whole recovery where it stops
            float s = Mathf.Min(_chargeReach, (Time.time - _rushStart) * _rush.Rush);
            Vector3 target = _rushFrom + _rushDir * s;
            Vector3 before = transform.position;
            Vector3 step = Flat(target - before);
            if (_agent.isOnNavMesh) _agent.Move(step);
            Vector3 after = transform.position;
            if (!_rushHit && Player != null
                && BossTactics.RushPasses(Ground(before), Ground(after), _rush.HalfWidth, Player.GroundPosition, Placeholders.PlayerRadius))
            {
                _rushHit = true;
                Land(_rush, true);
            }
            // The agent stops it at the NavMesh's edge; the line was cut to that already, so this is only a safeguard.
            bool stopped = Flat(after - target).magnitude > 0.5f;
            if (s >= _chargeReach - 1e-4f || stopped) EndCharge();
        }

        void EndCharge()
        {
            _rushing = false;
            if (!_rushHit) Land(_rush, false);
            if (_agent.isOnNavMesh) _agent.velocity = Vector3.zero;
            if (--_chargesLeft <= 0 || Player == null || (PlayerHealth != null && !PlayerHealth.IsAlive)) return;
            // Angry: a second charge at once, aimed at where she stands now, with its own windup and telegraph.
            EnemyAttackDefinition next = null;
            foreach (EnemyAttackDefinition a in Values.Attacks)
                if (a != null && a.Rush > 0f) { next = a; break; }
            _chaining = true;
            BeginWindup(next, Player.Position);
            _chaining = false;
        }

        protected override void OnDaze()
        {
            // Dazed mid-charge, it stops where it is.
            _rushing = false;
            _chargesLeft = 0;
        }

        protected override Vector3 LookVelocity => _rushing && _rush != null ? _rushDir * _rush.Rush : base.LookVelocity;

        // ---------------- Things that fly (work order 036, 2026-11-20) ----------------

        int _strikesSeen;

        /// <summary>Waves it has sent.</summary>
        public int WavesSent { get; private set; }

        /// <summary>
        /// With things that fly on, a Cleave that has just landed sends a wave straight ahead along the ground, from its
        /// front (<see cref="BossValues.WaveAttack"/>): she sidesteps it or dashes through it; walls stop it, props do not.
        /// When the Cleave covered her, the wave passes her: one swing hurts her once, by the Cleave. Its strike happened in
        /// this frame's step, where it stands, before it moved.
        /// </summary>
        void SendWave()
        {
            if (Strikes == _strikesSeen) return;
            _strikesSeen = Strikes;
            EnemyAttackDefinition cleave = Attack;
            if (!ThingsThatFly.On || cleave == null || cleave.Name != BossValues.Cleave || State != EnemyState.Recover || Player == null) return;
            GroundPoint me = Ground(transform.position);
            bool cleaved = EnemyBrain.Hits(cleave, me, Ground(AimPoint), Player.GroundPosition, Placeholders.PlayerRadius, Reach(cleave));
            Vector3 aim = AimDirection();
            var dir = new GroundPoint(aim.x, aim.z);
            ThingsThatFly.Ensure().Fire(this, Tuning.WaveAttack, ProjectileRules.Wave(Tuning, me + dir * Tuning.BodyRadius, dir), sparesHer: cleaved);
            WavesSent++;
            ThingsThatFly.TellLoosed(this, Tuning.WaveAttack, 1);
        }
    }
}
