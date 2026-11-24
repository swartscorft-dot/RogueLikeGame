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
        /// <summary>
        /// Small and fast, runs at her and blows up, its blast hurting its own side as much as her (work order 073,
        /// 2026-11-21, "Ny fiendetype"): do you move now, and do you shoot the right one first? (<see cref="BomberValues"/>)
        /// </summary>
        Bomber,
        /// <summary>
        /// A slow support at the back that wards its allies (they take much less damage while it lives) until it is killed or
        /// staggered, when every ward breaks and the warded are dazed (work order 079, 2026-11-22, "Enda en fiendetype"):
        /// whom do you shoot first? (<see cref="WarderValues"/>)
        /// </summary>
        Warder,
        /// <summary>
        /// Keeps far from her with a clear line, winds up on its body and charges in a straight line, fast; a wall or a prop
        /// stops it and leaves it dazed (work order 108, 2026-11-24, "Lansereren (ny fiende)"): do you stand in the open, or
        /// put cover between you? (<see cref="LancerValues"/>)
        /// </summary>
        Lancer,
    }

    public static class EnemyKinds
    {
        /// <summary>The kind as the play session log writes it: "brute", "swarmer", "shooter", "guard", "boss", "caster", "bomber", "warder".</summary>
        public static string Word(EnemyKind kind) =>
            kind == EnemyKind.Brute ? "brute" : kind == EnemyKind.Swarmer ? "swarmer" : kind == EnemyKind.Shooter ? "shooter"
            : kind == EnemyKind.Boss ? "boss" : kind == EnemyKind.Caster ? "caster" : kind == EnemyKind.Bomber ? "bomber"
            : kind == EnemyKind.Warder ? "warder" : kind == EnemyKind.Lancer ? "lancer" : "guard"; // work order 079; 108 the lancer

        /// <summary>
        /// The letter for an enemy point on a level's text map: b brute, s swarmer, p a pack of swarmers, h shooter,
        /// g guard, c caster (work order 027; casters never stand in a line), x bomber (work order 073; each alone), w warder (work order 079; each alone). Small
        /// letters, so they do not read as the rooms' capitals. The act boss is G (work order 014).
        /// </summary>
        public static char Letter(EnemyKind kind, int count) =>
            kind == EnemyKind.Swarmer ? (count > 1 ? 'p' : 's') : kind == EnemyKind.Brute ? 'b' : kind == EnemyKind.Shooter ? 'h'
            : kind == EnemyKind.Boss ? 'G' : kind == EnemyKind.Caster ? 'c' : kind == EnemyKind.Bomber ? 'x'
            : kind == EnemyKind.Warder ? 'w' : kind == EnemyKind.Lancer ? 'l' : 'g'; // work order 079; 108 the lancer, l (each alone)
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
        /// <summary>Its attacks with melee you read on (work order 035, <see cref="MeleeAttacks.Swarmer"/>): the Bite and the Leap.</summary>
        public EnemyAttackDefinition[] Melee = MeleeAttacks.Swarmer();
        /// <summary>
        /// Does it leap (work order 039)? Off: it has no skill at all, it runs at her and bites fast, and the Leap of
        /// <see cref="Melee"/> is never chosen (Kristian, 2026-11-20, after his second session: "I did not mean that the small
        /// ones should charge in addition to running fast. They just run fast and hammer away when they are close"). On: the
        /// Leap as work order 035 built it, at the damage it had, to compare.
        /// </summary>
        public bool Leaps = false;
        public float PackWakeRange = 10f;   // m: when one wakes, every swarmer this near it wakes too
        public float Spacing = 0.8f;        // m kept between two swarmers
        public float StopShort = 0.9f;      // m from her where it stops walking
        public int PracticePack = 5;        // swarmers in a pack spawned in the practice room (F9)
        public float BodyRadius = 0.3f;     // m, the capsule
        public float BodyHeight = 1.1f;     // m

        [NonSerialized] EnemyAttackDefinition[] _withoutLeap, _withoutLeapOf;

        /// <summary>
        /// What it fights with, melee you read on (work order 039): <see cref="Melee"/> when it <see cref="Leaps"/>, else
        /// the same set without the Leap. Made once for a set, not per question.
        /// </summary>
        public EnemyAttackDefinition[] MeleeNow
        {
            get
            {
                if (Leaps || Melee == null) return Melee;
                if (!ReferenceEquals(_withoutLeapOf, Melee))
                {
                    int n = 0;
                    foreach (EnemyAttackDefinition a in Melee) if (a == null || a.Move != MeleeMove.Leap) n++;
                    var kept = new EnemyAttackDefinition[n];
                    n = 0;
                    foreach (EnemyAttackDefinition a in Melee) if (a == null || a.Move != MeleeMove.Leap) kept[n++] = a;
                    _withoutLeap = kept;
                    _withoutLeapOf = Melee;
                }
                return _withoutLeap;
            }
        }
    }

    /// <summary>
    /// The shooter's numbers (work order 008, first guesses): it keeps its distance and shoots a line at where she
    /// stood when it began to aim. It needs to see her.
    /// </summary>
    [Serializable]
    public sealed class ShooterValues
    {
        /// <summary>
        /// The farthest it shoots from, every shot of it (the Shot, the arrow, the fan, the elite's volley): 11 m (work order 087;
        /// Kristian, 2026-11-22, told that the ranged enemies fight from off screen, the camera showing 11.3 m ahead of her and
        /// 7.7 m behind: "Trekk fiendene nærmere"; an attack must be read on the enemy's body, and the body on screen). 14 m before.
        /// </summary>
        public const float ShootsFrom = 11f;

        public EnemyValues Enemy = new EnemyValues
        {
            Health = 800, MoveSpeed = 3f, AggroRange = 14f, LeashRange = 26f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    // Recovery: the time after a shot before it aims again, so 2.4 s from shot to shot.
                    Name = "Shot", Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = ShootsFrom,
                    Windup = 0.9f, Recovery = 1.5f, Damage = 15, Length = 14f, HalfWidth = 0.5f,
                },
            },
        };
        // Work order 087 ("Trekk fiendene nærmere", above): it keeps 7 to 10 m, inside the 11.3 m the screen shows ahead of her
        // (8 to 12 m before).
        public float KeepNear = 7f;         // m: it backs off to at least this far (8 before)
        public float KeepFar = 10f;         // m: it walks closer when she is farther than this (12 before)
        public float BackOffUnder = 6f;     // m: it starts to back off when she is nearer than this
        public float SightHeight = 0.5f;    // m over the ground of the line it sees her along, under the low walls' top

        // ---------------- Shooters that hold their place (work order 098, 2026-11-24; ShooterPostRules) ----------------

        /// <summary>
        /// The switch: a shooter of a room's fight holds its post, the place the generator stood it in the room. Awake, it does not
        /// walk toward her because she is far: out of its reach (<see cref="ShootsFrom"/>) it stands at its post facing her and
        /// waits, in its reach it shoots by the rules and numbers above, and it steps only inside <see cref="HoldRadius"/> of its
        /// post, to get its line to her back round a prop and to back off when she comes nearer than <see cref="BackOffUnder"/>.
        /// Kristian's choice of 2026-11-24, asked what should pull him further into a room (the evening of 11-23: a median of 17 %
        /// of a combat room's floor used, 90 % of a fight inside 8.0 m, "nå blir det bare at man kiter det rundt seg"): "Skyttere som
        /// holder plassen" (the shooters stay behind cover further in and do not walk toward her; she has to go to them). Off: it
        /// keeps 7 to 10 m from her and walks to her as before, field for field. A shooter with no room's fight (the practice room)
        /// moves as before either way.
        /// </summary>
        public bool HoldsPlace = true;
        /// <summary>m round its post a shooter that holds its place may step in, never out (work order 098; the producer's starting value).</summary>
        public float HoldRadius = 3f;

        public float BodyRadius = 0.3f;     // m, the capsule: thin
        public float BodyHeight = 2.4f;     // m: tall

        // ---------------- Things that fly (work order 036, 2026-11-20; decision 0010; first guesses) ----------------

        public const string Arrow = "Arrow";
        public const string ArrowFan = "Arrow Fan";
        public const string Volley = "Volley";

        /// <summary>
        /// Its shots with things that fly on, in place of the Shot of its <see cref="Enemy"/> values (which stays, for the switch off): an
        /// arrow that leaves the bow at the release and flies; the fan's draw is a little longer; the volley is an elite's
        /// ordinary shot. Shot's damage and windup and its band of 14 m (036 had raised it to 17 m only for a test case at
        /// 16 m; work order 039 put it back and tests at 8 m and 13 m; work order 087: 11 m, <see cref="ShootsFrom"/>). The recovery is 1.2 s, the Shot's 1.5 s before
        /// (work order 039, which asked about 1.1 s): a moving player now steps out of most arrows, so it shoots a little
        /// more often to keep the air busy, about 2.1 s from shot to shot. 1.1 s measured the same on average but kept time
        /// with a run back and forth over 12 m (2 s a leg), so that a run of releases in a row fell where she turned back
        /// into the spot it was aimed at.
        /// Work order 046 (the test pilot's flights of the night of 2026-11-20): 1.8 s, about 2.7 s from shot to shot. At
        /// 1.2 s the arrow was the first or second source of her damage in every round (a quarter to a third of it, the
        /// elite's volley and fans besides), and the pilots died on floor 2; Kristian's third session had the shooter first
        /// too (266, 25 arrows landed). Its speed, its lead and its damage are as they were.
        /// </summary>
        public EnemyAttackDefinition[] Arrows =
        {
            new EnemyAttackDefinition
            {
                Name = Arrow, Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = ShootsFrom,
                Windup = 0.9f, Recovery = 1.8f, Damage = 15, Length = 30f, HalfWidth = 0.35f,
            },
            new EnemyAttackDefinition
            {
                Name = ArrowFan, Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = ShootsFrom,
                Windup = 1.2f, Recovery = 1.8f, Damage = 15, Length = 30f, HalfWidth = 0.35f,
            },
            new EnemyAttackDefinition
            {
                // Recovery: from the first arrow; the second leaves VolleyGap into it.
                Name = Volley, Shape = EnemyAttackShape.Line, MinDistance = 0f, MaxDistance = ShootsFrom,
                Windup = 0.9f, Recovery = 1.8f, Damage = 15, Length = 30f, HalfWidth = 0.35f,
            },
        };
        // Work order 039 (Kristian's second session of 2026-11-20: 38 of 40 arrows in the open landed; "the shot comes a
        // little too fast"): an arrow she can see coming and step out of. At 13 m/s an arrow takes almost a second over
        // 12 m (it was 24 m/s), and it is aimed only a little ahead of her (a share of 0.35 of the full lead, at most
        // 2.5 m; it was all of it, up to 5 m): standing or drifting she is hit, running at it or away from it she is hit,
        // walking into its path she is hit; running across she is missed behind, and a sidestep a quarter second after the
        // release takes her out of its way at 12 m.
        public float ArrowSpeed = 13f;      // m/s
        public float ArrowRadius = 0.35f;   // m, its hit size: her footprint is met within this of its line
        public float ArrowFlight = 30f;     // m, its longest flight
        public float LeadShare = 0.35f;     // its aim leads her by her velocity times the arrow's flight time, times this
        public float LeadMost = 2.5f;       // m, the lead at most
        public int FanEvery = 3;            // every third shot is a fan...
        // Work order 046 (the test pilot's flights of the night of 2026-11-20): from floor 4 (3 before). With the danger by
        // floor eased, the fan was the end of three of the five pilots that died on floor 3, and an eighth of her damage.
        public int FanFromFloor = 4;        // ...from this floor on, and for an elite on any floor
        public int FanArrows = 3;
        // Degrees between two arrows of a fan: 16 (12 before work order 039). With slow arrows a fan is a pattern to step
        // through: 10 m from the bow two arrows 12 degrees apart leave her middle a band of 0.4 m to pass between them
        // (2.1 m between their lines less 0.85 m of reach, hers and its, to each side); 16 degrees leave 1.1 m.
        public float FanDegrees = 16f;      // degrees between two arrows of a fan
        public float FanWindupLook = 1.6f;  // the fan's wind-up effect on the body, this times the size of the others
        public int VolleyArrows = 2;        // an elite's ordinary shot: arrows one after the other...
        public float VolleyGap = 0.25f;     // ...s apart, each aimed when it leaves
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
        /// <summary>Its attacks with melee you read on (work order 035, <see cref="MeleeAttacks.Guard"/>): the Fist, the Drag and the Crush.</summary>
        public EnemyAttackDefinition[] Melee = MeleeAttacks.Guard();
        public StaggerValues Stagger = new StaggerValues();
        public float BodyRadius = 0.9f;     // m, the capsule: large
        public float BodyHeight = 2.8f;     // m
    }
}
