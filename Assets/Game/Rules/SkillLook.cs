using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How her skills are shown (work order 123), as numbers. Kristian, 2026-11-25, after two runs: "Jeg vil heller ikke at alle
    /// abilities får en cone/telegraf etter man har skutt de, prosjektiler og røyk/flammer skal heller vise hvordan spellsene
    /// utspiller seg". The producer's reading, put to him while this was built: where one of HER skills draws a flat shape on the
    /// ground at or after the moment she fires it (the aim outline from the press on, the cone's wedge, the circle's rings, the lying
    /// keg's circles, the smoke's flat discs, the rings under a hit), the shape goes, and the skill is shown by what it is: tracers
    /// that fly, dust and sparks that run out to a blast's edge, smoke where the smoke stands, the keg itself while it lies. What
    /// she sees while she HOLDS a key to aim before it fires (Long Shot's charge) stays; the enemies' warnings are not touched.
    ///
    /// Every effect fills the place the rule affects and lasts as long as it does (<see cref="SkillLookRules"/>): a tracer runs to
    /// the range or the cover the hit stops at, a cone's outer tracers lie on its edges, a blast's ring stops at its radius, the
    /// smoke's puffs reach its radius and are gone when its time is out, the keg is drawn exactly while it lies. All first guesses;
    /// nobody has played it.
    /// </summary>
    [Serializable]
    public sealed class SkillLookValues
    {
        /// <summary>
        /// The order's switch, ON by default (work order 123, his words above). Off: every skill draws exactly what it drew before
        /// (the outline from the press to the last hit, the wedge, the rings and outlines, the keg's circle, the smoke's discs), field
        /// for field. Read live, at each moment it decides something. One switch for the whole game, so it is static; tests set it
        /// and put it back.
        /// </summary>
        public static bool EffectsNotShapes = true;

        /// <summary>The values the game draws with (one set).</summary>
        public static readonly SkillLookValues Live = new SkillLookValues();

        // ---------------- The pool ----------------
        public int PoolStart = 64;           // pieces made when it starts and lent out
        public int PoolGrow = 32;            // pieces made when the pool is out
        public int MostPieces = 320;         // pieces live at most: past it a new one is not drawn (a cap: she fired 990 skills in 25 minutes)

        // ---------------- A cone's tracers (Scatter Blast, Fan Fire) ----------------
        public float ConeInnerJitter = 0.8f; // a cone of fewer than five tracers: share of its own slice an inner tracer may wander (its outer two lie on the edges)
        public float KickSize = 0.55f;       // m across: the dust kicked up under the end of each tracer of a cone, where its reach ends
        public float KickSeconds = 0.3f;     // s it shows
        public float KickHeight = 0.25f;     // m over the floor, its middle
        public float KickRise = 0.8f;        // m/s it rises
        public int BlastSmokePuffs = 3;      // grey puffs at the muzzle after Scatter Blast
        public int TickSmokePuffs = 1;       // ...after each of Fan Fire's ticks
        public float MuzzleSmokeSize = 0.5f; // m across
        public float MuzzleSmokeSeconds = 0.55f;
        public float MuzzleSmokeRise = 0.6f; // m/s up and along the shot

        // ---------------- A circle's blast (Powder Keg) ----------------
        public int RingPuffs = 22;           // dust puffs running out from the middle to the radius
        public int RingSparks = 14;          // sparks running out with them
        public float RingPuffSize = 1f;      // m across (0.75 read faint from the game's camera in the second pictures)
        public float RingSparkSize = 0.24f;  // m across
        public float RingHeight = 0.35f;     // m over the floor
        public float ArriveShare = 0.7f;     // share of the blast's seconds at which the ring reaches the radius; it fades there after
        public int AfterSmokePuffs = 7;      // the smoke after: grey puffs left inside the circle
        public float AfterSmokeSeconds = 0.9f;
        public float AfterSmokeSize = 1.1f;  // m across
        public float AfterSmokeReach = 0.6f; // share of the radius the smoke after stands within (its puffs' edges)

        // ---------------- The throw (Powder Keg, Smoke Round): over the startup, from her hand to where it lands ----------------
        public float ThrowPeak = 1.4f;       // m the arc rises over the straight line at its middle
        public float ThrowTrailEvery = 0.03f; // s between the wisps (smoke) or sparks (keg) it leaves

        // ---------------- The keg while it lies ----------------
        public float KegRadius = 0.36f;      // m, the barrel's (seen from 28 m up: about 0.3 was lost behind a dummy in the first pictures)
        public float KegHeight = 0.65f;      // m
        public float FuseEvery = 0.06f;      // s between the sparks off its fuse
        public float FuseSparkSize = 0.3f;   // m across (0.16 could not be seen from the game's camera in the first pictures)
        public float FuseSparkSeconds = 0.25f;
        public float FuseSparkRise = 1.3f;   // m/s

        // ---------------- Smoke Round's cloud ----------------
        public int SmokePuffs = 22;          // puffs that fill its circle: the first ring of them on its edge, the rest inside
        public int SmokeEdgePuffs = 11;      // of them, on the edge
        public float SmokePuffShare = 0.5f;  // a puff's width as a share of the cloud's radius
        public float SmokeLowest = 0.35f;    // m over the floor, the lowest puff's middle
        public float SmokeHighest = 1.7f;    // m, the highest's: about her height, as the discs before
        public float SmokeAlpha = 0.6f;      // a puff's middle: see-through enough that she reads what is in it (work order 109); 0.34 was a faint fog in the second pictures
        public float SmokeFadeIn = 0.15f;    // s it takes to come up after it lands
        public float SmokeFadeOut = 0.35f;   // s of fading that END when its time is out: it is drawn until the rule ends, never after
        public float SmokeRise = 0.04f;      // m/s a puff drifts up: never out to the side, so the circle stays the rule's
        public float SmokeSpin = 9f;         // degrees a second a puff turns

        // ---------------- Slide's dust (in place of the dust wedge) ----------------
        public int SlideDustPuffs = 4;
        public float SlideDustSize = 0.5f;
        public float SlideDustSeconds = 0.35f;
        public float SlideDustSpeed = 2.4f;  // m/s back from where she starts
        public float SlideDustSpread = 40f;  // degrees across

        // ---------------- Chips in place of the rings under a hit (Breaker Slug, a counter) ----------------
        public int SlugChips = 8;
        public float SlugChipReach = 1f;     // m they fly
        public float SlugChipSize = 0.14f;
        public float SlugChipSeconds = 0.28f;
        public int CounterChips = 10;
        public float CounterChipReach = 1.3f;
        public float CounterChipSize = 0.16f;
        public float CounterChipSeconds = 0.34f;

        // ---------------- Colours, as picked ----------------
        // Her kit's extras are quiet (the sprint's rule 5): the sparks glow at most at 1, under the bloom's threshold (1.2), as the
        // game's other small glows are; dust and smoke do not glow.
        public Rgba Dust = new Rgba(0.56f, 0.5f, 0.42f, 0.85f);  // its alpha: a puff's middle (0.55 read faint in the second pictures)
        public Rgba Smoke = new Rgba(0.62f, 0.62f, 0.6f, 1f);   // SmokeCloudView.Grey: the smoke's own grey; its alpha is SmokeAlpha
        public Rgba SparkRim = new Rgba(1f, 0.55f, 0.16f, 1f);
        public Rgba SparkCore = new Rgba(1f, 0.9f, 0.6f, 1f);
        public float SparkGlow = 1f;         // at most 1: under the bloom's threshold
        public Rgba KegWood = new Rgba(0.42f, 0.25f, 0.12f, 1f);
        public Rgba KegHoop = new Rgba(0.16f, 0.14f, 0.13f, 1f);
        public Rgba RoundShell = new Rgba(0.5f, 0.52f, 0.5f, 1f); // Smoke Round's canister
    }

    /// <summary>
    /// The rules of how a skill is shown (work order 123): what is drawn when, and where each effect reaches, so that an effect shows
    /// exactly what the rule does. No engine; the runtime draws by them (<c>SkillEffects</c>, <c>SkillIndicator</c>).
    /// </summary>
    public static class SkillLookRules
    {
        /// <summary>
        /// Whether the aim outline of the skill in use is drawn in this phase. Off (<paramref name="effectsNotShapes"/> false): as
        /// before, from the press to its last hit (every phase but none and the recovery). On: only while the key is HELD to aim a
        /// skill that has not fired yet (a Charge skill charging, Long Shot); from the release, the press of any other skill, its
        /// cast and its hits, it is not drawn.
        /// </summary>
        public static bool OutlineShows(bool effectsNotShapes, SkillInput input, SkillPhase phase)
        {
            bool before = phase != SkillPhase.None && phase != SkillPhase.Recovery;
            if (!effectsNotShapes) return before;
            return input == SkillInput.Charge && phase == SkillPhase.Channel;
        }

        /// <summary>
        /// The yaw in degrees, from the aim, of tracer <paramref name="i"/> of <paramref name="count"/> across a cone of
        /// <paramref name="angle"/> degrees. Five or more: evenly from edge to edge (as before). Two to four: the first and the last on
        /// the cone's two edges, the inner ones each in its own share of the cone, moved by <paramref name="random"/> (0 to 1) within
        /// <paramref name="jitter"/> of it: every tick shows the cone's whole width. One: anywhere in it, by the random.
        /// </summary>
        public static float ConeTracerYaw(int i, int count, float angle, float random, float jitter)
        {
            float half = angle * 0.5f;
            if (count <= 1) return -half + angle * Clamp01(random);
            float even = -half + angle * i / (count - 1f);
            if (count >= 5 || i == 0 || i == count - 1) return even;
            float slice = angle / (count - 1f);
            float moved = even + (Clamp01(random) - 0.5f) * Clamp01(jitter) * slice;
            return Math.Max(-half, Math.Min(half, moved));
        }

        /// <summary>
        /// How far from a blast's middle its ring's pieces are, at <paramref name="t"/> (0 to 1) of its seconds: out from the middle,
        /// slowing, to <paramref name="reach"/> at <paramref name="arrive"/> of its time, and there after. Never past it.
        /// </summary>
        public static float RingReach(float reach, float t, float arrive)
        {
            float a = Math.Max(1e-3f, Math.Min(1f, arrive));
            float k = Clamp01(t / a);
            return Math.Max(0f, reach) * (1f - (1f - k) * (1f - k));
        }

        /// <summary>
        /// The share of its full alpha a puff of smoke shows at <paramref name="now"/>: none before <paramref name="from"/>, up over
        /// <paramref name="fadeIn"/>, full, then down over <paramref name="fadeOut"/> to none AT <paramref name="until"/>, none after.
        /// It is drawn exactly while the cloud stands.
        /// </summary>
        public static float SmokeShare(float now, float from, float until, float fadeIn, float fadeOut)
        {
            if (now < from || now >= until) return 0f;
            float up = fadeIn > 0f ? Clamp01((now - from) / fadeIn) : 1f;
            float down = fadeOut > 0f ? Clamp01((until - now) / fadeOut) : 1f;
            return Math.Min(up, down);
        }

        /// <summary>
        /// Where puff <paramref name="i"/> of <paramref name="count"/> of a cloud of <paramref name="radius"/> stands, from its middle,
        /// for puffs <paramref name="puffRadius"/> across their half: the first <paramref name="edge"/> evenly round the edge with their
        /// outer side ON the radius, the rest inside, spread by the golden angle. No puff reaches past the radius.
        /// </summary>
        public static GroundPoint SmokePuffAt(int i, int count, int edge, float radius, float puffRadius)
        {
            float inner = Math.Max(0f, radius - puffRadius);
            int onEdge = Math.Max(0, Math.Min(edge, count));
            if (i < onEdge)
            {
                double a = 2.0 * Math.PI * i / onEdge;
                return new GroundPoint((float)(Math.Cos(a) * inner), (float)(Math.Sin(a) * inner));
            }
            int rest = Math.Max(1, count - onEdge), j = i - onEdge;
            float r = inner * 0.8f * (float)Math.Sqrt((j + 0.5) / rest);
            double b = j * 2.39996323 + 0.5;
            return new GroundPoint((float)(Math.Cos(b) * r), (float)(Math.Sin(b) * r));
        }

        /// <summary>The arc's height over the straight line from where it was thrown to where it lands, at <paramref name="k"/> (0 to 1): 0 at both ends.</summary>
        public static float ThrowLift(float k, float peak)
        {
            float c = Clamp01(k);
            return 4f * Math.Max(0f, peak) * c * (1f - c);
        }

        /// <summary>The farthest any piece of a circle drawn by puffs reaches from its middle: the puff's middle out plus its half.</summary>
        public static float Edge(float fromMiddle, float halfAcross) => fromMiddle + Math.Max(0f, halfAcross);

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
