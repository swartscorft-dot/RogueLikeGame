using System;

namespace Roguelike.Rules
{
    public enum EnemyAttackShape
    {
        /// <summary>A circle of Radius around the enemy.</summary>
        Circle,
        /// <summary>A strip from the enemy toward where the player stood when the windup began: Length long, 2 * HalfWidth wide.</summary>
        Line,
        /// <summary>A wedge from the enemy toward where the player stood when the windup began: Length long, Angle degrees wide in all.</summary>
        Cone,
    }

    /// <summary>One attack of an enemy: when it is used, how long its telegraph shows, and what it hits.</summary>
    [Serializable]
    public sealed class EnemyAttackDefinition
    {
        public string Name = "Attack";
        public EnemyAttackShape Shape = EnemyAttackShape.Circle;
        public float MinDistance = 0f;   // m: used when the player is at least this far away
        public float MaxDistance = 3f;   // m: and at most this far away
        public float Windup = 1f;        // s the telegraph shows before the strike
        public float Recovery = 1f;      // s the enemy stands still after the strike
        public int Damage = 20;
        public float Radius = 3f;        // m, Circle
        public float Length = 12f;       // m, Line
        public float HalfWidth = 1.2f;   // m, Line
        public float Angle = 90f;        // degrees in all, Cone
        public bool Counterable = false; // work order 015: Stopping Shot from its front drops it; its telegraph is blue (PoolRules.IsCounterable)
        /// <summary>
        /// m/s the enemy runs along its Line at the strike, hitting what it passes, instead of striking from where it
        /// stands (work order 014: the boss's charge). 0, every other attack: it strikes in place.
        /// </summary>
        public float Rush = 0f;

        // Melee you read (decision 0010; work order 035, 2026-11-20): what the swarmer's, the brute's and the guard's new
        // attacks are besides their shape (MeleeAttacks). Every attack from before leaves them at their defaults.
        /// <summary>An ordinary attack: in reach, on a short cycle, little damage; it takes no turn to attack (<see cref="AttackTurns.TakesTurn"/>).</summary>
        public bool Ordinary = false;
        /// <summary>s from the start of one use's windup to the earliest start of the next: a skill's own cooldown. 0: none.</summary>
        public float Cooldown = 0f;
        /// <summary>What it does besides striking where it stands: hop to her, lunge at her, pull her in (<see cref="MeleeMove"/>).</summary>
        public MeleeMove Move = MeleeMove.None;
        /// <summary>s of it: a hop or a lunge takes the last this many seconds of the windup; a pull takes this long on her.</summary>
        public float MoveSeconds = 0f;
        /// <summary>s she is stunned when it lands (the brute's Shield bash). 0: none.</summary>
        public float Stun = 0f;
        /// <summary>m in front of the enemy she is pulled to (the guard's Drag).</summary>
        public float PullTo = 0f;
        /// <summary>The skill it likes to follow: chosen first, its own cooldown or not, right after that one (the guard's Crush after a Drag).</summary>
        public string Follows = "";

        public EnemyAttackDefinition Copy() => (EnemyAttackDefinition)MemberwiseClone();
    }

    /// <summary>
    /// The numbers every kind of enemy has. The brute's live in the enemy settings asset; since work order 008 the
    /// other kinds carry theirs inside their own values class (<see cref="SwarmerValues"/> and the others).
    /// </summary>
    [Serializable]
    public sealed class EnemyValues
    {
        public int Health = 3000;
        public float MoveSpeed = 3.5f;     // m/s
        public float AggroRange = 9f;      // m: it wakes when the player comes this near, or when it is hurt
        public float LeashRange = 22f;     // m: it gives up when the player is this far away
        public float RespawnDelay = 4f;    // s from its death until it stands at home again
        public EnemyAttackDefinition[] Attacks = DefaultAttacks();
        /// <summary>
        /// Work order 011: it wears its kind's model and plays its clips; off, it is the coloured capsule as before. Read
        /// when it is made. The practice brute's, in the enemy settings asset, goes for every enemy made from it.
        /// </summary>
        public bool UseModels = true;

        public static EnemyAttackDefinition[] DefaultAttacks() => new[]
        {
            new EnemyAttackDefinition
            {
                Name = "Slam", Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 3f,
                Windup = 0.9f, Recovery = 1f, Damage = 20, Radius = 3f,
            },
            new EnemyAttackDefinition
            {
                Name = "Shockwave", Shape = EnemyAttackShape.Line, MinDistance = 3f, MaxDistance = 11f,
                Windup = 1.1f, Recovery = 1.2f, Damage = 30, Length = 12f, HalfWidth = 1.2f, Counterable = true,
            },
        };
    }

    public enum EnemyState
    {
        /// <summary>Standing at home, or walking back to it.</summary>
        Idle,
        /// <summary>Walking toward the player until an attack fits the distance.</summary>
        Chase,
        /// <summary>Standing still with the telegraph of an attack showing.</summary>
        Windup,
        /// <summary>Standing still after a strike.</summary>
        Recover,
    }

    /// <summary>
    /// What the enemy does, as a state machine with no engine in it. Each frame it is told the time,
    /// how far away the player is and whether the player lives, and it says whether it strikes now.
    /// Walking, the telegraph and the hit itself are the caller's business.
    /// </summary>
    public sealed class EnemyBrain
    {
        float _stateSince;

        public EnemyState State { get; private set; } = EnemyState.Idle;
        /// <summary>The attack being wound up or recovered from, else null.</summary>
        public EnemyAttackDefinition Attack { get; private set; }

        /// <summary>Back to standing at home, as after a death.</summary>
        public void Reset()
        {
            State = EnemyState.Idle;
            Attack = null;
        }

        /// <summary>0 to 1 through the windup of the current attack. 0 outside a windup.</summary>
        public float WindupProgress(float now)
        {
            if (State != EnemyState.Windup || Attack == null) return 0f;
            if (Attack.Windup <= 0f) return 1f;
            float p = (now - _stateSince) / Attack.Windup;
            return p < 0f ? 0f : p > 1f ? 1f : p;
        }

        /// <summary>Seconds until the strike. 0 outside a windup.</summary>
        public float WindupRemaining(float now)
        {
            if (State != EnemyState.Windup || Attack == null) return 0f;
            float left = _stateSince + Attack.Windup - now;
            return left < 0f ? 0f : left;
        }

        /// <summary>
        /// Advances to now. Returns true on the one step in which the current attack strikes;
        /// <see cref="Attack"/> is then the attack that struck.
        /// </summary>
        /// <param name="distance">Metres between the enemy and the player.</param>
        /// <param name="provoked">The enemy has been hurt since it last stood at home.</param>
        /// <param name="mayAttack">
        /// False holds it in the chase even when an attack fits the distance: a shooter that cannot see her, or one
        /// that is backing off. The brute passes true.
        /// </param>
        /// <param name="choose">
        /// Picks the attack for a distance in the chase (work order 014: the boss's own choice); null, every other kind,
        /// takes <see cref="AttackFor"/>.
        /// </param>
        /// <param name="held">
        /// Its room's fight is on (work order 070, <see cref="RoomFight"/>): it wakes from home wherever she is, with no range
        /// asked, and gives up for no distance; only her death sends it home. False, every enemy outside a room's fight: as
        /// before work order 070.
        /// </param>
        public bool Step(float now, float distance, bool playerAlive, bool provoked, EnemyValues values, bool mayAttack = true,
                         Func<float, EnemyValues, EnemyAttackDefinition> choose = null, bool held = false)
        {
            switch (State)
            {
                case EnemyState.Idle:
                    if (playerAlive && (held || (distance <= values.LeashRange && (distance <= values.AggroRange || provoked))))
                        Enter(EnemyState.Chase, now);
                    return false;

                case EnemyState.Chase:
                    if (!playerAlive || (!held && distance > values.LeashRange))
                    {
                        Attack = null;
                        Enter(EnemyState.Idle, now);
                        return false;
                    }
                    EnemyAttackDefinition fit = !mayAttack ? null : choose != null ? choose(distance, values) : AttackFor(distance, values);
                    if (fit != null)
                    {
                        Attack = fit;
                        Enter(EnemyState.Windup, now);
                    }
                    return false;

                case EnemyState.Windup:
                    if (now - _stateSince < Attack.Windup) return false;
                    Enter(EnemyState.Recover, now);
                    return true;

                case EnemyState.Recover:
                    if (now - _stateSince < Attack.Recovery) return false;
                    Attack = null;
                    Enter(EnemyState.Chase, now);
                    return false;
            }
            return false;
        }

        /// <summary>
        /// Drops the attack being wound up or recovered from, with no strike, and goes back to the chase: a daze does
        /// this (<see cref="StaggerBar"/>). Nothing when it stands at home.
        /// </summary>
        public void Cancel(float now)
        {
            if (State == EnemyState.Idle) return;
            Attack = null;
            Enter(EnemyState.Chase, now);
        }

        /// <summary>
        /// Gives up and goes home at once, whatever it was doing (work order 070): its room's fight ended, and every enemy of
        /// the room goes home together. Nothing when it stands at home.
        /// </summary>
        public void GoHome(float now)
        {
            if (State == EnemyState.Idle) return;
            Attack = null;
            Enter(EnemyState.Idle, now);
        }

        /// <summary>
        /// Starts the recovery again from now (work order 014): an attack that moves the enemy through its strike (the
        /// charge's run) holds the recovery off until it stops. Nothing outside a recovery.
        /// </summary>
        public void HoldRecovery(float now)
        {
            if (State == EnemyState.Recover) _stateSince = now;
        }

        /// <summary>
        /// Starts the windup of this attack at once, whatever it was doing but standing at home (work order 014: the
        /// angry boss's second charge follows the first with no recovery between).
        /// </summary>
        public void Begin(EnemyAttackDefinition attack, float now)
        {
            if (State == EnemyState.Idle || attack == null) return;
            Attack = attack;
            Enter(EnemyState.Windup, now);
        }

        void Enter(EnemyState state, float now)
        {
            State = state;
            _stateSince = now;
        }

        /// <summary>The first attack in the list whose distance band holds the player, or null.</summary>
        public static EnemyAttackDefinition AttackFor(float distance, EnemyValues values)
        {
            if (values.Attacks == null) return null;
            foreach (EnemyAttackDefinition attack in values.Attacks)
            {
                if (attack != null && distance >= attack.MinDistance && distance <= attack.MaxDistance) return attack;
            }
            return null;
        }

        /// <summary>
        /// Does the attack, struck from the origin with the aim it locked at the start of the windup, touch the target?
        /// </summary>
        public static bool Hits(EnemyAttackDefinition attack, GroundPoint origin, GroundPoint aim,
                                GroundPoint target, float targetRadius)
        {
            return Hits(attack, origin, aim, target, targetRadius, attack.Length);
        }

        /// <summary>As above, with a line or a cone <paramref name="length"/> long: a shot cut short by a wall.</summary>
        public static bool Hits(EnemyAttackDefinition attack, GroundPoint origin, GroundPoint aim,
                                GroundPoint target, float targetRadius, float length)
        {
            switch (attack.Shape)
            {
                case EnemyAttackShape.Circle: return SkillRules.InCircle(origin, attack.Radius, target, targetRadius);
                case EnemyAttackShape.Cone: return SkillRules.InCone(origin, aim, length, attack.Angle, target, targetRadius);
                default: return LineShot.Hits(origin, aim, length, attack.HalfWidth, target, targetRadius);
            }
        }
    }
}
