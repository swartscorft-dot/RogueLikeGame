using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How her speed reaches her basic shot's accuracy (work order 143; the setting on the settings screen's Trials page). Kristian,
    /// 2026-11-26: "også vil jeg innføre en presisjons statestikk. litt som cs i bevegelse. man har momentum. jo høyere den er, jo mer
    /// usikker skyter man (altså en cone blir bredere), og som i cs. så kan man counter strafe for å stoppe fortere"; his ticks: "Ekte
    /// treghet, måleren som valg" and "Bare grunnskuddet".
    /// - Off: today's game, field for field.
    /// - Inertia (the default, his tick): her body moves by a law with a short time to full speed and to a stand, the opposite key
    ///   braking much faster; accuracy reads her real speed.
    /// - Meter (his choice to compare): movement instant as today; a momentum value builds while she runs, falls when she stands and
    ///   is emptied by a tap of the opposite key; accuracy reads it.
    /// - Ghost (the producer's third, research r4's first choice, told to him): movement instant as today; a hidden velocity follows
    ///   hers by Counter-Strike's own law scaled to 6 m/s; accuracy reads its speed.
    /// The order is the enum's: a stored value is its number.
    /// </summary>
    public enum PrecisionMode { Off, Inertia, Meter, Ghost }

    /// <summary>
    /// The numbers of work order 143 (research r4's, every one a first guess he tunes by playing). One instance lives in
    /// <see cref="Live"/>; not in the feel spec's table nor in the settings asset: these defaults are what the game reads.
    /// </summary>
    [Serializable]
    public sealed class PrecisionValues
    {
        /// <summary>
        /// The order's switch (work order 143, his words above): on, the setting below works and its default is
        /// <see cref="DefaultMode"/>; off, the game is exactly as before the order whatever the setting holds: no cone, no inertia,
        /// nothing drawn, nothing on her sheet, no line on the Trials page.
        /// </summary>
        public static bool On = true;

        /// <summary>The numbers the game reads (reachable from tests and the Inspector's tools).</summary>
        public static PrecisionValues Live = new PrecisionValues();

        /// <summary>The setting's default while he has stored none: Inertia, his tick ("Ekte treghet, måleren som valg").</summary>
        public const PrecisionMode DefaultMode = PrecisionMode.Inertia;

        /// <summary>
        /// Where the rules learn the mode in use: the game's stored setting (PrecisionSettings), given at start. Null (a rule test):
        /// Off unless a test gives one.
        /// </summary>
        public static Func<PrecisionMode> ModeSource;

        /// <summary>The mode in use now: Off with the switch off or no source.</summary>
        public static PrecisionMode ModeNow => !On || ModeSource == null ? PrecisionMode.Off : ModeSource();

        // ---------------- The accuracy curve, shared by the three ----------------

        /// <summary>Accurate at or below this share of her run speed (35 %: 2.1 m/s at 6; CS's own 34 %).</summary>
        public float AccurateShare = 0.35f;
        /// <summary>The cone's full width at full run speed, in degrees (24: the width that can be felt with the 1 m line, research r4 finding 1). It rises in a straight line from 0 at <see cref="AccurateShare"/>; a sprint stays at it.</summary>
        public float FullCone = 24f;
        /// <summary>The draw's outer part: no two draws past this share of the half cone come in a row (the outer third).</summary>
        public float OuterShare = 2f / 3f;
        /// <summary>Draws in one bag: shuffled strata of the centre-weighted (triangular) draw, so every bag holds the draw's shape exactly.</summary>
        public int BagSize = 18;
        /// <summary>The bag's seed outside a run (the practice room); in a run it is the run's seed.</summary>
        public uint PracticeSeed = 143;

        // ---------------- The stat ("Precision", her sheet's BASIC SHOT group) ----------------

        /// <summary>The most the moving cone can be cut by (60 %: "capped at 60 %").</summary>
        public float MostCut = 0.6f;
        /// <summary>The Laser Sight's cut of the moving cone (−40 %: the one thing in this order that moves the stat, research r4 C4.1).</summary>
        public float LaserSightCut = 0.4f;

        // ---------------- Inertia (her body moves by it; research r4's "proposed" column) ----------------

        /// <summary>Friction: each second her speed falls by max(speed, <see cref="InertiaStopSpeed"/>) times this.</summary>
        public float InertiaFriction = 8f;
        /// <summary>m/s: under it friction is a constant 20 m/s² (2.5 × 8), so she comes to a stand.</summary>
        public float InertiaStopSpeed = 2.5f;
        /// <summary>m/s² along the held direction, up to the speed the caps allow (times the cap over <see cref="ReferenceRunSpeed"/> above it, CS's own scale, so a sprint is reached).</summary>
        public float InertiaAcceleration = 60f;
        /// <summary>m/s² more while the held direction points against her motion: the counter-strafe.</summary>
        public float InertiaBrake = 60f;
        /// <summary>m/s: under Inertia she counts as moving (her poses, a shot on the move, the log's sample) while her body is faster than this with no key held (the poses' own <see cref="PoseRule.MoveThreshold"/>).</summary>
        public float InertiaMovingSpeed = PoseRule.MoveThreshold;
        /// <summary>s: the longest small step the law is run in, so it is the same at any frame rate (a frame is cut into equal steps no longer than this).</summary>
        public float InertiaStep = 1f / 1000f;

        // ---------------- The meter (movement instant as today) ----------------

        /// <summary>s for the momentum to rise from 0 to 1, running at full speed.</summary>
        public float MeterRise = 0.5f;
        /// <summary>s for it to fall from 1 to 0, at a stand.</summary>
        public float MeterFall = 0.3f;

        // ---------------- The counter-strafe (the meter's empty, and the log's line in every mode) ----------------

        /// <summary>Degrees: a move key pressed more than this from her motion of the last <see cref="MotionSeconds"/> is a counter-strafe.</summary>
        public float CounterAngle = 135f;
        /// <summary>s: her motion is her velocity averaged over about this long (an exponential average with this time constant).</summary>
        public float MotionSeconds = 0.1f;
        /// <summary>That motion must be above this share of run speed for a press against it to be a counter-strafe (the accurate share: 35 %).</summary>
        public float CounterAboveShare = 0.35f;

        // ---------------- The ghost (movement instant as today; the CS:GO law scaled to 6 m/s) ----------------

        /// <summary>m/s² (CS's sv_accelerate 5.5 at 6 m/s), times the speed followed over <see cref="ReferenceRunSpeed"/> above it.</summary>
        public float GhostAcceleration = 33f;
        /// <summary>CS's sv_friction 5.2 a second.</summary>
        public float GhostFriction = 5.2f;
        /// <summary>m/s: CS's sv_stopspeed 80 of 250, at 6 m/s.</summary>
        public float GhostStopSpeed = 1.92f;

        /// <summary>m/s: the run speed the m/s² above are given for (the feel's 6).</summary>
        public float ReferenceRunSpeed = 6f;

        // ---------------- The practice room's task "Stop and shoot" ----------------

        /// <summary>Metres at least from her to the statue for a hit to count (the order's statue stands 10 m away; the teaching room's stand 6 m from her start, so she steps back to this).</summary>
        public float StopShotMetres = 8f;
        /// <summary>"After a run": her speed share was at least this ...</summary>
        public float StopShotRunShare = 0.8f;
        /// <summary>... within this many seconds before the press, and the shot went off accurate.</summary>
        public float StopShotRunWithin = 0.6f;
        /// <summary>Hits that must count.</summary>
        public int StopShotHits = 3;

        /// <summary>
        /// Work order 152's switch (the grips' analyst's finding of 2026-11-27: "a shot reads her speed as it stood at the end of the
        /// frame before the shot goes off, so the cone depends on the frame rate"; held fire on the move, stock gun, 16.3° then
        /// 13.9° at 60 fps, 14.5° then 12.1° at 144, the law's own 13.9° then 11.2°). On: the law follows the speed it aims for at
        /// each of its small steps and a shot reads its share at the moment its wind-up ends, inside the frame
        /// (<see cref="PrecisionMoment"/>), so the same press at the same moment gives the same cone on every machine; Inertia,
        /// the meter and the ghost alike. Off: work order 143's frame, number for number.
        /// </summary>
        public static bool SameAtEveryFrameRate = true;
    }

    /// <summary>The rules of work order 143's precision: the cone, the three ways her speed reaches it, the draw. No engine.</summary>
    public static class PrecisionRules
    {
        const double Deg = Math.PI / 180.0;

        /// <summary>The cone's full width in degrees at this share of run speed: 0 at or under the accurate share, then a straight line to the full cone at full speed, held there above it.</summary>
        public static float Cone(float share, PrecisionValues v, float accurateShare)
        {
            float a = Math.Max(0f, Math.Min(0.99f, accurateShare));
            if (share <= a) return 0f;
            return v.FullCone * Math.Min(1f, (share - a) / (1f - a));
        }

        /// <summary><see cref="Cone(float, PrecisionValues, float)"/> with the values' own accurate share.</summary>
        public static float Cone(float share, PrecisionValues v) => Cone(share, v, v.AccurateShare);

        /// <summary>The meter's cone: the full cone times the momentum.</summary>
        public static float MeterCone(float momentum, PrecisionValues v) => v.FullCone * Clamp01(momentum);

        /// <summary>The share of run speed a momentum stands for (for the speed bar and the log): the accurate share at 0, full speed at 1.</summary>
        public static float MeterShare(float momentum, float accurateShare) => accurateShare + (1f - accurateShare) * Clamp01(momentum);

        /// <summary>A cone after her Precision's cut (0 to the most, <see cref="PrecisionValues.MostCut"/>).</summary>
        public static float AfterCut(float cone, float cut, PrecisionValues v) => cone * (1f - Math.Max(0f, Math.Min(v.MostCut, cut)));

        /// <summary>The cone's real width in metres at <paramref name="distance"/> on the ground: 2 × d × tan(half).</summary>
        public static float WidthAt(float coneDegrees, float distance) =>
            coneDegrees <= 0f || distance <= 0f ? 0f : (float)(2.0 * distance * Math.Tan(coneDegrees * 0.5 * Deg));

        /// <summary>A direction on the ground turned by <paramref name="degrees"/>, clockwise seen from above (to the right is positive, as <see cref="UpperBodyAim.TwistAngle"/>).</summary>
        public static GroundPoint Turn(GroundPoint dir, float degrees)
        {
            if (degrees == 0f) return dir;
            double a = degrees * Deg, c = Math.Cos(a), s = Math.Sin(a);
            return new GroundPoint((float)(dir.X * c + dir.Z * s), (float)(-dir.X * s + dir.Z * c));
        }

        /// <summary>The momentum a speed share asks for: 0 at the accurate share, 1 at full speed.</summary>
        public static float MeterTarget(float share, float accurateShare) => Clamp01((share - accurateShare) / Math.Max(0.01f, 1f - accurateShare));

        /// <summary>The meter one step on: toward its target, rising in <see cref="PrecisionValues.MeterRise"/> from 0 to 1, falling in <see cref="PrecisionValues.MeterFall"/> times <paramref name="settle"/> from 1 to 0.</summary>
        public static float MeterStep(float momentum, float share, float dt, PrecisionValues v, float accurateShare, float settle = 1f)
        {
            float target = MeterTarget(share, accurateShare);
            if (dt <= 0f) return momentum;
            if (momentum < target) return Math.Min(target, momentum + dt / Math.Max(1e-4f, v.MeterRise));
            return Math.Max(target, momentum - dt / Math.Max(1e-4f, v.MeterFall * Math.Max(0.01f, settle)));
        }

        /// <summary>
        /// A press against her motion: the held direction (<paramref name="held"/>, any length, zero for none) more than
        /// <see cref="PrecisionValues.CounterAngle"/> from her motion (<paramref name="motion"/>, m/s), that motion above
        /// <see cref="PrecisionValues.CounterAboveShare"/> of <paramref name="runSpeed"/>.
        /// </summary>
        public static bool IsCounterStrafe(GroundPoint held, GroundPoint motion, float runSpeed, PrecisionValues v)
        {
            float h = held.Length, m = motion.Length;
            if (h < 1e-5f || runSpeed <= 0f || m <= v.CounterAboveShare * runSpeed) return false;
            double cos = held.Dot(motion) / (h * m);
            return cos < Math.Cos(v.CounterAngle * Deg);
        }

        /// <summary>
        /// Inertia, one frame of <paramref name="dt"/> seconds in equal steps no longer than <see cref="PrecisionValues.InertiaStep"/>:
        /// friction (speed −= max(speed, stop) × friction × step), the brake while the held direction points against her motion,
        /// then acceleration along the held direction up to <paramref name="cap"/> m/s. <paramref name="wishX"/>, <paramref name="wishZ"/>:
        /// the held direction (length 1) or zero to coast. The velocity is in m/s on the ground.
        /// </summary>
        public static void InertiaFrame(ref float vx, ref float vz, float wishX, float wishZ, float cap, float dt, PrecisionValues v)
        {
            if (dt <= 0f) return;
            int steps = Math.Max(1, (int)Math.Ceiling(dt / Math.Max(1e-4f, v.InertiaStep) - 1e-6));
            float h = dt / steps;
            bool wish = wishX * wishX + wishZ * wishZ > 1e-8f;
            float accel = v.InertiaAcceleration * Math.Max(1f, cap / Math.Max(0.1f, v.ReferenceRunSpeed));
            for (int i = 0; i < steps; i++)
            {
                Friction(ref vx, ref vz, v.InertiaFriction, v.InertiaStopSpeed, h);
                if (!wish) continue;
                if (vx * wishX + vz * wishZ < 0f) Slow(ref vx, ref vz, v.InertiaBrake * h);
                Accelerate(ref vx, ref vz, wishX, wishZ, cap, accel * h);
            }
        }

        /// <summary>
        /// The ghost, one frame: the CS:GO law at 6 m/s (no brake) following her real velocity (<paramref name="realX"/>,
        /// <paramref name="realZ"/>, m/s): friction, then acceleration along her real direction up to her real speed. Friction is
        /// divided by <paramref name="settle"/> (a longer settle, a slower ghost to a stand).
        /// </summary>
        public static void GhostFrame(ref float gx, ref float gz, float realX, float realZ, float dt, PrecisionValues v, float settle = 1f)
        {
            if (dt <= 0f) return;
            int steps = Math.Max(1, (int)Math.Ceiling(dt / Math.Max(1e-4f, v.InertiaStep) - 1e-6));
            float h = dt / steps;
            float speed = (float)Math.Sqrt(realX * realX + realZ * realZ);
            float wx = speed > 1e-4f ? realX / speed : 0f, wz = speed > 1e-4f ? realZ / speed : 0f;
            float accel = v.GhostAcceleration * Math.Max(1f, speed / Math.Max(0.1f, v.ReferenceRunSpeed));
            float friction = v.GhostFriction / Math.Max(0.01f, settle);
            for (int i = 0; i < steps; i++)
            {
                Friction(ref gx, ref gz, friction, v.GhostStopSpeed, h);
                if (speed > 1e-4f) Accelerate(ref gx, ref gz, wx, wz, speed, accel * h);
            }
        }

        static void Friction(ref float vx, ref float vz, float friction, float stop, float h)
        {
            float speed = (float)Math.Sqrt(vx * vx + vz * vz);
            if (speed <= 0f) return;
            float after = Math.Max(0f, speed - Math.Max(speed, stop) * friction * h);
            float k = after / speed;
            vx *= k;
            vz *= k;
        }

        static void Slow(ref float vx, ref float vz, float by)
        {
            float speed = (float)Math.Sqrt(vx * vx + vz * vz);
            if (speed <= 0f) return;
            float k = Math.Max(0f, speed - by) / speed;
            vx *= k;
            vz *= k;
        }

        static void Accelerate(ref float vx, ref float vz, float wx, float wz, float cap, float most)
        {
            float add = cap - (vx * wx + vz * wz);
            if (add <= 0f) return;
            float a = Math.Min(add, most);
            vx += wx * a;
            vz += wz * a;
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>
    /// The draw of the shot's deflection (work order 143; research r4: random misses with the cursor on the target are what players
    /// hate most, so the draw is centre-weighted and no two wide ones come in a row): a seeded bag of
    /// <see cref="PrecisionValues.BagSize"/> values in −1 to 1, one from each equal stratum of the triangular draw (so each bag
    /// holds its shape exactly), shuffled, with the outer ones (past <see cref="PrecisionValues.OuterShare"/>) never next to each
    /// other, also across two bags. A draw times the half cone is the deflection. Its own small generator (xorshift), the same in
    /// Unity and under .NET. Nothing is allocated after it is made.
    /// </summary>
    public sealed class PrecisionBag
    {
        float[] _bag = new float[0];
        float[] _inner = new float[0];
        float[] _outer = new float[0];
        int _next;
        uint _state = 1;
        bool _lastOuter;
        float _outerShare = 2f / 3f;

        /// <summary>Draws taken since it was seeded.</summary>
        public int Drawn { get; private set; }

        public PrecisionBag(uint seed, PrecisionValues v) => Seed(seed, v);

        /// <summary>Starts again from a seed (a run's, or the practice room's).</summary>
        public void Seed(uint seed, PrecisionValues v)
        {
            int n = Math.Max(3, v.BagSize);
            if (_bag.Length != n)
            {
                _bag = new float[n];
                _inner = new float[n];
                _outer = new float[n];
            }
            _outerShare = Math.Max(0f, Math.Min(1f, v.OuterShare));
            _state = seed == 0u ? 0x9E3779B9u : seed;
            _next = n;
            _lastOuter = false;
            Drawn = 0;
        }

        /// <summary>The next draw, −1 to 1, centre-weighted.</summary>
        public float Next()
        {
            if (_next >= _bag.Length) Fill();
            float x = _bag[_next++];
            _lastOuter = IsOuter(x);
            Drawn++;
            return x;
        }

        /// <summary>True for a draw in the outer part.</summary>
        public bool IsOuter(float x) => Math.Abs(x) > _outerShare + 1e-6f;

        void Fill()
        {
            int n = _bag.Length, inner = 0, outer = 0;
            for (int i = 0; i < n; i++)
            {
                float p = (i + Uniform()) / n;
                float x = p <= 0.5f ? (float)Math.Sqrt(2.0 * p) - 1f : 1f - (float)Math.Sqrt(2.0 * (1.0 - p));
                if (IsOuter(x)) _outer[outer++] = x;
                else _inner[inner++] = x;
            }
            Shuffle(_inner, inner);
            Shuffle(_outer, outer);
            // The outer ones go into distinct gaps between the inner ones (gap g lies before inner g), so two never touch; the first
            // gap only if the last draw before was not outer. More outer than gaps (a tiny bag): the rest go at the end.
            int gaps = inner + 1, first = _lastOuter ? 1 : 0;
            int k = 0, w = 0;
            int placed = 0;
            for (int g = 0; g < gaps; g++)
            {
                int left = gaps - g, need = outer - placed;
                bool put = g >= first && need > 0 && (left <= need || Uniform() * left < need); // need of the gaps left, each as likely
                if (put) _bag[w++] = _outer[placed++];
                if (g < inner) _bag[w++] = _inner[k++];
            }
            while (placed < outer) _bag[w++] = _outer[placed++];
            _next = 0;
        }

        void Shuffle(float[] a, int count)
        {
            for (int i = count - 1; i > 0; i--)
            {
                int j = (int)(Uniform() * (i + 1));
                if (j > i) j = i;
                float t = a[i];
                a[i] = a[j];
                a[j] = t;
            }
        }

        float Uniform()
        {
            uint x = _state;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _state = x;
            return (x >> 8) * (1f / 16777216f);
        }
    }

    /// <summary>
    /// The setting's line on the settings screen's Trials page (work order 143: "changeable on the settings screen's trials page and
    /// stored like the other trials"): "Precision", stepping Off, Inertia, Meter, Ghost; shown only with the order's switch on.
    /// Her sheet's line in the BASIC SHOT group: "Precision", the spread at a run with its reduction.
    /// </summary>
    public static class PrecisionLines
    {
        public const string Name = "Precision";
        public static readonly string[] Words = { "Off", "Inertia", "Meter", "Ghost" };
        public const int Count = 4;

        /// <summary>A stored number as a mode: one of the four, or the default for anything else (none stored).</summary>
        public static PrecisionMode Stored(int stored) => stored >= 0 && stored < Count ? (PrecisionMode)stored : PrecisionValues.DefaultMode;

        /// <summary>The mode a step of <paramref name="delta"/> leads to: forward for a step up or Enter, back for a step down, round.</summary>
        public static PrecisionMode Stepped(PrecisionMode mode, int delta) => (PrecisionMode)((((int)mode + (delta < 0 ? -1 : 1)) % Count + Count) % Count);

        /// <summary>The mode's words on its line.</summary>
        public static string WordsOf(PrecisionMode mode) => Words[(int)mode < 0 || (int)mode >= Count ? 0 : (int)mode];

        /// <summary>Her sheet's value: "Spread at a run 24°", and with a cut "Spread at a run 24° to 14°" (the interface's font has no arrow).</summary>
        public static string SheetWords(float full, float atRun) =>
            "Spread at a run " + Degrees(full) + (Math.Abs(atRun - full) >= 0.5f ? " to " + Degrees(atRun) : "");

        static string Degrees(float d) => d.ToString("0", System.Globalization.CultureInfo.InvariantCulture) + "°";
    }
}
