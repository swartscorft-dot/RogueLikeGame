using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The caster's numbers (work order 027, Kristian's request for a fifth kind, 2026-11-20; all first guesses): a
    /// hooded skeleton that keeps its distance as the shooter does and calls up bursts under her feet that go off after
    /// a moment. Its attacks do not come from where it stands: each places circles on the ground (pillar 4: every burst
    /// shows its circle for its whole wind-up, and the big cast can be countered).
    ///
    /// <see cref="BoneSpikes"/>: one circle where she stands when the wind-up begins, bursting at its end. <see cref="GraveRain"/>
    /// (blue, counterable): <see cref="RainCircles"/> circles one after another, the first where she stands when the wind-up
    /// begins and bursting at the end of the attack's wind-up, each next one <see cref="RainEvery"/> after the one before,
    /// where she stands then moved along the way she is moving by her velocity times <see cref="LeadSeconds"/> (at most
    /// <see cref="LeadMost"/>), bursting
    /// <see cref="RainLaterWindup"/> after it is placed (<see cref="CasterRules"/>). Every <see cref="BigCastEvery"/>th cast
    /// is a Grave Rain (<see cref="CasterTactics"/>). The brain's windup of Grave Rain is its first circle's, so its strike
    /// is the first burst and a counter can drop it only before then.
    /// </summary>
    [Serializable]
    public sealed class CasterValues
    {
        public const string BoneSpikes = "Bone Spikes";
        public const string GraveRain = "Grave Rain";

        public EnemyValues Enemy = new EnemyValues
        {
            Health = 800, MoveSpeed = 2.6f, AggroRange = 14f, LeashRange = 26f, RespawnDelay = 4f,
            Attacks = new[]
            {
                new EnemyAttackDefinition
                {
                    // Recovery: from the burst to its next cast.
                    Name = BoneSpikes, Shape = EnemyAttackShape.Circle, MinDistance = 4f, MaxDistance = 15f,
                    Windup = 1.0f, Recovery = 1.3f, Damage = 25, Radius = 2.4f,
                },
                new EnemyAttackDefinition
                {
                    // Windup: the first circle's, to the first burst; Recovery: from the first burst.
                    Name = GraveRain, Shape = EnemyAttackShape.Circle, MinDistance = 4f, MaxDistance = 15f,
                    Windup = 1.4f, Recovery = 1.6f, Damage = 20, Radius = 2.2f, Counterable = true,
                },
            },
        };
        public int BigCastEvery = 3;        // every third cast is a Grave Rain, the others Bone Spikes
        public int RainCircles = 3;         // circles of a Grave Rain
        public float RainEvery = 0.5f;      // s between two circles of a Grave Rain being placed
        public float RainLaterWindup = 1f;  // s from a later circle of a Grave Rain being placed to its burst
        // A later circle's lead (work order 034, 2026-11-20): her velocity times this, so it is where she will be about
        // when it bursts, no farther than LeadMost. 1.5 m whatever her speed before (work order 027): at her full run of
        // 6 m/s she was 4.5 m past each led circle when it burst, so a straight run escaped all three, which defeated the
        // cast. Now standing still takes all three, a straight run the second and third, a change of way none of those.
        public float LeadSeconds = 0.8f;    // s of her velocity
        public float LeadMost = 5f;         // m at most
        public float MovingOver = 0.5f;     // m/s: she counts as moving above this; slower, no lead
        public float KeepNear = 9f;         // m: it backs off to at least this far
        public float KeepFar = 13f;         // m: it walks closer when she is farther than this
        public float BackOffUnder = 6f;     // m: it starts to back off when she is nearer than this
        public float SightHeight = 0.5f;    // m over the ground of the line it sees her along, as the shooter's
        public float GroundSearch = 0.3f;   // m: a circle is placed only where the walkable ground is at most this far from its middle
        public float FadeSeconds = 0.35f;   // s a circle of a countered Grave Rain takes to fade
        public float BodyRadius = 0.35f;    // m, the capsule
        public float BodyHeight = 1.9f;     // m
    }

    /// <summary>Which cast comes next (work order 027): every <see cref="CasterValues.BigCastEvery"/>th is a Grave Rain.</summary>
    public sealed class CasterTactics
    {
        /// <summary>The casts begun so far.</summary>
        public int Casts { get; private set; }

        /// <summary>Is cast number <paramref name="number"/> (counted from 1) a Grave Rain?</summary>
        public static bool IsBigCast(int number, int every) => every > 0 && number > 0 && number % every == 0;

        /// <summary>The name of the next cast.</summary>
        public string Next(CasterValues v) => IsBigCast(Casts + 1, v.BigCastEvery) ? CasterValues.GraveRain : CasterValues.BoneSpikes;

        /// <summary>
        /// The next cast when its band holds her, counting it as begun; else null, and nothing is counted (the brain begins
        /// the windup of whatever this returns).
        /// </summary>
        public EnemyAttackDefinition Choose(float distance, CasterValues v)
        {
            EnemyAttackDefinition a = Peek(distance, v);
            if (a == null) return null;
            Casts++;
            return a;
        }

        /// <summary>
        /// The next cast when its band holds her, else null, counting nothing (work order 034: the turns to attack ask
        /// before the brain begins a wind-up, and a question must not move the cast order).
        /// </summary>
        public EnemyAttackDefinition Peek(float distance, CasterValues v)
        {
            EnemyAttackDefinition a = Find(v.Enemy, Next(v));
            return a == null || distance < a.MinDistance || distance > a.MaxDistance ? null : a;
        }

        /// <summary>The attack of this name in the values, or null.</summary>
        public static EnemyAttackDefinition Find(EnemyValues values, string name)
        {
            if (values?.Attacks == null) return null;
            foreach (EnemyAttackDefinition a in values.Attacks)
                if (a != null && a.Name == name) return a;
            return null;
        }
    }

    /// <summary>
    /// Where and when the caster's circles are placed and burst (work order 027), times in seconds from the start of the
    /// cast's wind-up. A circle hurts her when she touches it at its burst, as every circle telegraph does.
    /// </summary>
    public static class CasterRules
    {
        /// <summary>
        /// Where circle <paramref name="index"/> (from 0) of a Grave Rain is placed: the first where she stands; a later one
        /// where she stands, moved by <see cref="Lead"/> along the way she is moving.
        /// </summary>
        public static GroundPoint RainPlace(int index, GroundPoint her, GroundPoint velocity, CasterValues v) =>
            index <= 0 ? her : her + Lead(velocity, v);

        /// <summary>
        /// How far a later circle of a Grave Rain is moved from her, along the way she is moving (work order 034): her
        /// velocity times <see cref="CasterValues.LeadSeconds"/>, no longer than <see cref="CasterValues.LeadMost"/>; none
        /// when she moves no faster than <see cref="CasterValues.MovingOver"/>.
        /// </summary>
        public static GroundPoint Lead(GroundPoint velocity, CasterValues v)
        {
            float speed = velocity.Length;
            if (speed <= v.MovingOver || speed < 1e-5f) return new GroundPoint(0f, 0f);
            float lead = Math.Min(speed * Math.Max(0f, v.LeadSeconds), Math.Max(0f, v.LeadMost));
            return velocity * (lead / speed);
        }

        /// <summary>When circle <paramref name="index"/> of a Grave Rain is placed.</summary>
        public static float RainPlacedAt(int index, CasterValues v) => Math.Max(0, index) * v.RainEvery;

        /// <summary>How long circle <paramref name="index"/> of a Grave Rain shows before it bursts: the attack's windup for the first.</summary>
        public static float RainWindup(int index, CasterValues v) =>
            index <= 0 ? (CasterTactics.Find(v.Enemy, CasterValues.GraveRain)?.Windup ?? 0f) : v.RainLaterWindup;

        /// <summary>When circle <paramref name="index"/> of a Grave Rain bursts.</summary>
        public static float RainBurstAt(int index, CasterValues v) => RainPlacedAt(index, v) + RainWindup(index, v);

        /// <summary>From the start of a Grave Rain's wind-up to its last burst.</summary>
        public static float RainLastBurst(CasterValues v)
        {
            float last = 0f;
            for (int i = 0; i < v.RainCircles; i++) last = Math.Max(last, RainBurstAt(i, v));
            return last;
        }

        /// <summary>From the start of a Grave Rain's wind-up until the caster may cast again: its windup and recovery.</summary>
        public static float RainWhole(CasterValues v)
        {
            EnemyAttackDefinition rain = CasterTactics.Find(v.Enemy, CasterValues.GraveRain);
            return rain == null ? 0f : rain.Windup + rain.Recovery;
        }

        /// <summary>Does a circle of this radius at <paramref name="center"/> touch her at its burst?</summary>
        public static bool Hits(GroundPoint center, float radius, GroundPoint her, float herRadius) =>
            SkillRules.InCircle(center, radius, her, herRadius);

        /// <summary>
        /// A whole Grave Rain against a path of hers (for the tests and for tuning): where each circle is placed, and
        /// whether its burst touches her. <paramref name="at"/> gives where she is, and <paramref name="velocity"/> how she
        /// moves, at a time from the start of the wind-up.
        /// </summary>
        public static (GroundPoint place, bool hit)[] Rain(Func<float, GroundPoint> at, Func<float, GroundPoint> velocity,
                                                          CasterValues v, float herRadius)
        {
            EnemyAttackDefinition rain = CasterTactics.Find(v.Enemy, CasterValues.GraveRain);
            float radius = rain != null ? rain.Radius : 0f;
            var result = new (GroundPoint, bool)[Math.Max(0, v.RainCircles)];
            for (int i = 0; i < result.Length; i++)
            {
                float placed = RainPlacedAt(i, v);
                GroundPoint p = RainPlace(i, at(placed), velocity(placed), v);
                result[i] = (p, Hits(p, radius, at(RainBurstAt(i, v)), herRadius));
            }
            return result;
        }
    }
}
