using System;

namespace Roguelike.Rules
{
    public enum EnemyAttackShape
    {
        /// <summary>A circle of Radius around the enemy.</summary>
        Circle,
        /// <summary>A strip from the enemy toward where the player stood when the windup began: Length long, 2 * HalfWidth wide.</summary>
        Line,
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
    }

    /// <summary>The numbers of the one enemy of G4. They live in the enemy settings asset.</summary>
    [Serializable]
    public sealed class EnemyValues
    {
        public int Health = 3000;
        public float MoveSpeed = 3.5f;     // m/s
        public float AggroRange = 9f;      // m: it wakes when the player comes this near, or when it is hurt
        public float LeashRange = 22f;     // m: it gives up when the player is this far away
        public float RespawnDelay = 4f;    // s from its death until it stands at home again
        public EnemyAttackDefinition[] Attacks = DefaultAttacks();

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
                Windup = 1.1f, Recovery = 1.2f, Damage = 30, Length = 12f, HalfWidth = 1.2f,
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
        public bool Step(float now, float distance, bool playerAlive, bool provoked, EnemyValues values)
        {
            switch (State)
            {
                case EnemyState.Idle:
                    if (playerAlive && distance <= values.LeashRange && (distance <= values.AggroRange || provoked))
                        Enter(EnemyState.Chase, now);
                    return false;

                case EnemyState.Chase:
                    if (!playerAlive || distance > values.LeashRange)
                    {
                        Attack = null;
                        Enter(EnemyState.Idle, now);
                        return false;
                    }
                    EnemyAttackDefinition fit = AttackFor(distance, values);
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
            return attack.Shape == EnemyAttackShape.Circle
                ? SkillRules.InCircle(origin, attack.Radius, target, targetRadius)
                : LineShot.Hits(origin, aim, attack.Length, attack.HalfWidth, target, targetRadius);
        }
    }
}
