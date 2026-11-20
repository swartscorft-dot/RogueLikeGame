using System;

namespace Roguelike.Rules
{
    /// <summary>The kinds of enemy (work order 008). Each asks the player something different.</summary>
    public enum EnemyKind
    {
        /// <summary>Walks up and strikes with two telegraphed attacks (G4).</summary>
        Brute,
        /// <summary>Many, weak and fast, in packs: can you clear a crowd?</summary>
        Swarmer,
        /// <summary>Stands off and shoots a line: do you keep moving?</summary>
        Shooter,
        /// <summary>Big, slow, with a stagger bar: do you use the right skill at the right time?</summary>
        Guard,
        /// <summary>The act boss (work order 014): the crypt knight, with a cleave, a spin and a charge (<see cref="BossValues"/>).</summary>
        Boss,
        /// <summary>
        /// Stands off and calls up bursts under her feet that go off after a moment (work order 027, Kristian's request for
        /// a fifth kind, 2026-11-20): do you keep moving to somewhere new, and can you reach it or silence it? (<see cref="CasterValues"/>)
        /// </summary>
        Caster,
    }

    public static class EnemyKinds
    {
        /// <summary>The kind as the play session log writes it: "brute", "swarmer", "shooter", "guard", "boss", "caster".</summary>
        public static string Word(EnemyKind kind) =>
            kind == EnemyKind.Brute ? "brute" : kind == EnemyKind.Swarmer ? "swarmer" : kind == EnemyKind.Shooter ? "shooter"
            : kind == EnemyKind.Boss ? "boss" : kind == EnemyKind.Caster ? "caster" : "guard";

        /// <summary>
        /// The letter for an enemy point on a level's text map: b brute, s swarmer, p a pack of swarmers, h shooter,
        /// g guard, c caster (work order 027; casters never stand in a line). Small letters, so they do not read as the
        /// rooms' capitals. The act boss is G (work order 014).
        /// </summary>
        public static char Letter(EnemyKind kind, int count) =>
            kind == EnemyKind.Swarmer ? (count > 1 ? 'p' : 's') : kind == EnemyKind.Brute ? 'b' : kind == EnemyKind.Shooter ? 'h'
            : kind == EnemyKind.Boss ? 'G' : kind == EnemyKind.Caster ? 'c' : 'g';
    }

    /// <summary>
    /// The swarmer's numbers (work order 008, first guesses): many, weak, fast. One short strike on a circle around
    /// it; when one wakes, the swarmers near it wake; they keep a little apart when they stand around her.
    /// </summary>
    [Serializable]
    public sealed class SwarmerValues
    {
        public EnemyValues Enemy = new EnemyValues
        {
            Health = 300, MoveSpeed = 5f, AggroRange = 9f, LeashRange = 22f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    Name = "Lunge", Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 1.2f,
                    Windup = 0.4f, Recovery = 0.6f, Damage = 8, Radius = 1.2f,
                },
            },
        };
        public float PackWakeRange = 10f;   // m: when one wakes, every swarmer this near it wakes too
        public float Spacing = 0.8f;        // m kept between two swarmers
        public float StopShort = 0.9f;      // m from her where it stops walking
        public int PracticePack = 5;        // swarmers in a pack spawned in the practice room (F9)
        public float BodyRadius = 0.3f;     // m, the capsule
        public float BodyHeight = 1.1f;     // m
    }

    /// <summary>
    /// The shooter's numbers (work order 008, first guesses): it keeps its distance and shoots a line at where she
    /// stood when it began to aim. It needs to see her.
    /// </summary>
    [Serializable]
    public sealed class ShooterValues
    {
        public EnemyValues Enemy = new EnemyValues
        {
            Health = 800, MoveSpeed = 3f, AggroRange = 14f, LeashRange = 26f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    // Recovery: the time after a shot before it aims again, so 2.4 s from shot to shot.
                    Name = "Shot", Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = 14f,
                    Windup = 0.9f, Recovery = 1.5f, Damage = 15, Length = 14f, HalfWidth = 0.5f,
                },
            },
        };
        public float KeepNear = 8f;         // m: it backs off to at least this far
        public float KeepFar = 12f;         // m: it walks closer when she is farther than this
        public float BackOffUnder = 6f;     // m: it starts to back off when she is nearer than this
        public float SightHeight = 0.5f;    // m over the ground of the line it sees her along, under the low walls' top
        public float BodyRadius = 0.3f;     // m, the capsule: thin
        public float BodyHeight = 2.4f;     // m: tall
    }

    /// <summary>
    /// The guard's numbers (work order 008, first guesses): big and slow, two heavy attacks as the brute's, and a
    /// stagger bar that, emptied, dazes it.
    /// </summary>
    [Serializable]
    public sealed class GuardValues
    {
        public EnemyValues Enemy = new EnemyValues
        {
            Health = 6000, MoveSpeed = 2.5f, AggroRange = 9f, LeashRange = 22f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    Name = "Slam", Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 3f,
                    Windup = 1.2f, Recovery = 1.2f, Damage = 35, Radius = 4f,
                },
                new EnemyAttackDefinition
                {
                    Name = "Sweep", Shape = EnemyAttackShape.Cone, MinDistance = 3f, MaxDistance = 5.5f,
                    Windup = 1.0f, Recovery = 1.2f, Damage = 30, Length = 6f, Angle = 120f, Counterable = true,
                },
            },
        };
        public StaggerValues Stagger = new StaggerValues();
        public float BodyRadius = 0.9f;     // m, the capsule: large
        public float BodyHeight = 2.8f;     // m
    }
}
