using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Rage when a part is lost (work order 129, a form order: a rule of the fight). When work order 122 (bones that fly) was planned
    /// Kristian ticked that a lost part "skal påvirke fienden"; asked on 2026-11-25 at about 18:50, after playing it, WHAT it should do
    /// (weaker attacks, slower, or takes more damage were offered), he wrote his own answer: "De sikter dårligere, men fyrer av mye
    /// oftere. en slags rage mode." (They aim worse, but fire much more often. A kind of rage mode.)
    ///
    /// From the moment an enemy has lost its first part by work order 122's rule (<see cref="BoneBreakRules.PartsDue"/> with
    /// <see cref="BoneBreakValues.Live"/>: the same rules value that takes the part off, not the picture of it, so with 122's look
    /// switched off it rages at the same share of its health) it is enraged for the rest of its life: it stands a share of its rest
    /// after each strike (<see cref="RestShare"/>), and each attack's aim is thrown off by a random angle (<see cref="ShotDegrees"/>
    /// for a shot or a bolt, <see cref="ChargeDegrees"/> for the lancer's charge, <see cref="StrikeDegrees"/> for a melee strike),
    /// drawn as the aim is fixed today so that what it shows (its body, its draw, its lance) points where the attack will really
    /// go. Its wind-ups and tells keep their length. No new sign: the missing part is the sign. Every value is a first guess he
    /// tunes by playing.
    /// </summary>
    [Serializable]
    public sealed class PartRageValues
    {
        /// <summary>
        /// The order's switch (work order 129; Kristian 2026-11-25: "De sikter dårligere, men fyrer av mye oftere. en slags rage
        /// mode."). On by default. Off, every enemy attacks exactly as before the order: its whole rest after each strike, its aim
        /// where she stands. Not kept between starts: every Play starts on.
        /// </summary>
        public static bool On = true;

        /// <summary>The values the game rages by (one set, read at each strike's rest and each wind-up's start).</summary>
        public static readonly PartRageValues Live = new PartRageValues();

        /// <summary>
        /// The share of its rest after a strike (an attack's <see cref="EnemyAttackDefinition.Recovery"/>) an enraged enemy stands
        /// before it may begin the next: a half (the shooters, the casters and the warder; since the merge of 2026-11-26 the melee
        /// kinds have their own, <see cref="MeleeRestShare"/>). The producer's reading of "fyrer av mye oftere": the rest between attacks is cut, the
        /// wind-up is NOT (she must still read and dodge each attack), so a shooter's arrow comes every 0.9 + 0.9 = 1.8 s instead of
        /// 0.9 + 1.8 = 2.7 s, half as many again. 1 is today's rest; 0 would leave none. A skill's own cooldown
        /// (<see cref="EnemyAttackDefinition.Cooldown"/>), the turns to attack (<see cref="AttackTurns"/>) and the rule that a shooter
        /// draws only on screen (<see cref="RangedOpeningRules"/>) are not touched.
        /// </summary>
        public float RestShare = 0.5f;

        /// <summary>
        /// The share of its rest after a strike an enraged MELEE kind stands (the kinds that strike in reach, <see cref="PartRageRules.Melee"/>:
        /// the swarmer, the brute, the guard, the lancer, and the bomber, whose blast has no rest): three quarters. Kristian's tick of
        /// 2026-11-25, "Nærkamp raser svakere" (melee rages weaker): at <see cref="RestShare"/>'s half the brute's Stroke went from
        /// 545 to 828 damage a minute in her reach, which he found too much; at 0.75 it is 658 (PartRageMeasureTests). The shooters, the casters and the warder
        /// keep <see cref="RestShare"/>. 1 is today's rest (the merge of 2026-11-26).
        /// </summary>
        public float MeleeRestShare = 0.75f;

        /// <summary>
        /// Degrees to either side a shot or a bolt (the archer's arrows, the caster's orbs, the warder's bolt) is thrown off by at
        /// most, at random, the same for the whole of one attack (a fan turns as one, a volley's second arrow keeps the first's
        /// angle): 12. At 8.5 m that is up to 1.8 m beside her; an arrow meets her within 0.85 m of its line, an orb within 1.4 m.
        /// </summary>
        public float ShotDegrees = 12f;

        /// <summary>
        /// Degrees to either side the lancer's charge is thrown off by at most: 6, half a shot's. Its body turns to that line
        /// through the wind-up and it runs down it (its tell is its lance and its lean, so the lance points where it will run). At its
        /// 14 m that is up to 1.5 m beside her; its line meets her within 1.1 m.
        /// </summary>
        public float ChargeDegrees = 6f;

        /// <summary>
        /// Degrees to either side a melee strike's direction is thrown off by at most (the brute's Stroke, the guard's Fist and Crush,
        /// the swarmer's Bite, which are wedges 70 to 140 degrees wide at arm's reach): 0, its aim left alone. The producer's choice: a
        /// few degrees off changes no hit of so wide a wedge at 2 m, and more would show a skeleton swinging beside her at arm's
        /// length, which reads as a fault, not as rage. Raised, the strike's wedge and its facing turn as a shot's do.
        /// </summary>
        public float StrikeDegrees = 0f;
    }

    /// <summary>What an enemy's aim is, for its rage (work order 129).</summary>
    public enum RageAim
    {
        /// <summary>Nothing to throw off: the bomber's blast is a ring round itself; the boss never rages.</summary>
        None,
        /// <summary>A shot or a bolt that flies: the archer, the caster, the warder (<see cref="PartRageValues.ShotDegrees"/>).</summary>
        Shot,
        /// <summary>A charge down a straight line: the lancer (<see cref="PartRageValues.ChargeDegrees"/>).</summary>
        Charge,
        /// <summary>A melee strike: the brute, the guard, the swarmer (<see cref="PartRageValues.StrikeDegrees"/>).</summary>
        Strike,
    }

    /// <summary>The rules of rage when a part is lost (work order 129), plain numbers for the enemies that rage.</summary>
    public static class PartRageRules
    {
        /// <summary>
        /// Can this kind rage? Only a kind that loses parts under work order 122 (<see cref="BoneBreakRules.Breaks"/>): every kind
        /// with a skeleton's body, the elites too. Not the act boss: an armoured knight that loses no part, and none is invented for it.
        /// </summary>
        public static bool Rages(EnemyKind kind) => BoneBreakRules.Breaks(kind);

        /// <summary>
        /// Is its rage due with <paramref name="health"/> of <paramref name="max"/> left: a kind that rages, with at least one part
        /// due by work order 122's rule (<see cref="BoneBreakRules.PartsDue"/>, with <paramref name="parts"/>, the game's
        /// <see cref="BoneBreakValues.Live"/>). Not 122's switch: the look off, the rage still comes at the same share.
        /// </summary>
        public static bool Due(EnemyKind kind, int health, int max, BoneBreakValues parts) =>
            Rages(kind) && BoneBreakRules.PartsDue(health, max, parts) >= 1;

        /// <summary>The kind's aim, for its rage.</summary>
        public static RageAim AimOf(EnemyKind kind)
        {
            switch (kind)
            {
                case EnemyKind.Shooter:
                case EnemyKind.Caster:
                case EnemyKind.Warder:
                    return RageAim.Shot;
                case EnemyKind.Lancer:
                    return RageAim.Charge;
                case EnemyKind.Brute:
                case EnemyKind.Guard:
                case EnemyKind.Swarmer:
                    return RageAim.Strike;
                default:
                    return RageAim.None; // the bomber's blast, and the boss, which never rages
            }
        }

        /// <summary>Degrees to either side an enraged one of this kind's aim is thrown off by at most (0: left alone).</summary>
        public static float Degrees(EnemyKind kind, PartRageValues v)
        {
            if (v == null || !Rages(kind)) return 0f;
            switch (AimOf(kind))
            {
                case RageAim.Shot: return Math.Max(0f, v.ShotDegrees);
                case RageAim.Charge: return Math.Max(0f, v.ChargeDegrees);
                case RageAim.Strike: return Math.Max(0f, v.StrikeDegrees);
                default: return 0f;
            }
        }

        /// <summary>
        /// Does this kind strike in reach (the merge of 2026-11-26, "Nærkamp raser svakere"): the swarmer, the brute, the guard (their
        /// strikes at arm's length), the lancer (its charge meets her body) and the bomber (its blast round itself; it has no rest, so
        /// its share changes nothing). Not the shooter, the caster or the warder (shots and bolts that fly), nor the boss (never rages).
        /// </summary>
        public static bool Melee(EnemyKind kind) =>
            kind == EnemyKind.Swarmer || kind == EnemyKind.Brute || kind == EnemyKind.Guard || kind == EnemyKind.Lancer || kind == EnemyKind.Bomber;

        /// <summary>
        /// The share of its rest after a strike this kind stands: while enraged with the switch on, <see cref="PartRageValues.MeleeRestShare"/>
        /// for a melee kind (<see cref="Melee"/>) and <see cref="PartRageValues.RestShare"/> for the others; else exactly 1 (today's rest).
        /// </summary>
        public static float RestShare(EnemyKind kind, bool enraged, PartRageValues v) =>
            PartRageValues.On && enraged && v != null ? Math.Max(0f, Melee(kind) ? v.MeleeRestShare : v.RestShare) : 1f;

        /// <summary>
        /// The angle (degrees, + turns to the left seen from above) an attack's aim is thrown off by: <paramref name="signed"/> (a
        /// number from -1 up to 1 from its own seeded stream) times the kind's width, while enraged with the switch on; else 0.
        /// </summary>
        public static float Angle(EnemyKind kind, bool enraged, double signed, PartRageValues v)
        {
            if (!PartRageValues.On || !enraged) return 0f;
            float width = Degrees(kind, v);
            if (width <= 0f) return 0f;
            double s = signed < -1.0 ? -1.0 : signed > 1.0 ? 1.0 : signed;
            return (float)(s * width);
        }

        /// <summary>A direction on the ground turned by <paramref name="degrees"/> (+ to the left seen from above); itself, exactly, at 0.</summary>
        public static GroundPoint Turn(GroundPoint dir, float degrees)
        {
            if (degrees == 0f) return dir;
            double a = degrees * Math.PI / 180.0;
            float cos = (float)Math.Cos(a), sin = (float)Math.Sin(a);
            return new GroundPoint(dir.X * cos - dir.Z * sin, dir.X * sin + dir.Z * cos);
        }

        /// <summary>Degrees from <paramref name="a"/> to <paramref name="b"/> on the ground, 0 to 180.</summary>
        public static float DegreesBetween(GroundPoint a, GroundPoint b)
        {
            float la = a.Length, lb = b.Length;
            if (la < 1e-6f || lb < 1e-6f) return 0f;
            double c = a.Dot(b) / (la * (double)lb);
            c = c < -1.0 ? -1.0 : c > 1.0 ? 1.0 : c;
            return (float)(Math.Acos(c) * 180.0 / Math.PI);
        }

        /// <summary>
        /// The seed of one enemy's rage stream (its aim's angles), from the fight's own seeded source as the floors take theirs: the
        /// floor's seed (<see cref="LevelLayout.Seed"/>, from the run's seed), the floor and the enemy's name on it (the place the
        /// level stood it, the same for the same seed), so a run with the same seed draws the same angles. Outside a floor (the
        /// practice room) the floor's seed is 0 and its number among the enemies made is mixed in.
        /// </summary>
        public static ulong Seed(ulong floorSeed, int floor, string name, int number)
        {
            ulong h = 0xCBF29CE484222325UL; // FNV-1a, 64 bits: the same on every runtime (a string's own hash is not)
            unchecked
            {
                h = Mix(h, floorSeed);
                h = Mix(h, (ulong)(uint)floor);
                h = Mix(h, (ulong)(uint)number);
                if (name != null)
                    for (int i = 0; i < name.Length; i++)
                    {
                        h ^= name[i];
                        h *= 0x100000001B3UL;
                    }
                h ^= 129UL; // work order 129's own stream: no other stream of the game is drawn from
            }
            return h == 0 ? 129UL : h;
        }

        static ulong Mix(ulong h, ulong v)
        {
            unchecked
            {
                for (int i = 0; i < 8; i++)
                {
                    h ^= (v >> (8 * i)) & 0xFF;
                    h *= 0x100000001B3UL;
                }
            }
            return h;
        }
    }
}
