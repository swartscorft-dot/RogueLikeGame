using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// What the trial fight decides with no engine in it (work order 147): its phase by its health, the wind-ups by phase, where the
    /// circles and seeds go, the shapes' hits, the ward, the crash, the hurt guard. The choosing of attacks is
    /// <see cref="BossTrialChooser"/>'s; the shadows of Ruin <see cref="RuinShadows"/>'.
    /// </summary>
    public static class BossTrialRules
    {
        /// <summary>The phase at this share of its health: 1 above <see cref="BossTrialValues.PhaseTwoBelow"/>, 3 under <see cref="BossTrialValues.PhaseThreeBelow"/>, else 2.</summary>
        public static int PhaseOf(float share, BossTrialValues v) => share < v.PhaseThreeBelow ? 3 : share < v.PhaseTwoBelow ? 2 : 1;

        /// <summary>A phase-1 time in this phase: times its factor, never under its floor (and never above itself).</summary>
        public static float Windup(float phaseOne, float floor, int phase, BossTrialValues v)
        {
            float f = v.WindupFactor[Clamp(phase, 1, 3)];
            float t = phaseOne * f;
            return Math.Min(phaseOne, Math.Max(floor, t));
        }

        /// <summary>The gap after a recovery before the next wind-up, in this phase.</summary>
        public static float Gap(int phase, BossTrialValues v) => v.Gap[Clamp(phase, 1, 3)];

        /// <summary>
        /// Is this attack one the dash alone answers (7.1, "dash-only")? The Slam: its ring cannot be walked out of. At most one per
        /// <see cref="BossTrialValues.DashOnlyEvery"/>.
        /// </summary>
        public static bool DashOnly(BossTrialAttack a) => a == BossTrialAttack.Slam;

        /// <summary>A body attack (the boss itself strikes): the Cleave and the Charge. Every other leaves a pattern on the ground.</summary>
        public static bool IsBody(BossTrialAttack a) => a == BossTrialAttack.Cleave || a == BossTrialAttack.Charge;

        /// <summary>A big pattern (7.1): Spokes, Seeds, the Slam's ring, Judgement.</summary>
        public static bool IsBig(BossTrialAttack a) =>
            a == BossTrialAttack.Spokes || a == BossTrialAttack.Seeds || a == BossTrialAttack.Slam || a == BossTrialAttack.Judgement;

        /// <summary>New in phase 2 (7.2): Spokes, Slam, Seeds; Judgement comes by its health shares.</summary>
        public static int FirstPhase(BossTrialAttack a) =>
            a == BossTrialAttack.Spokes || a == BossTrialAttack.Slam || a == BossTrialAttack.Seeds ? 2 : a == BossTrialAttack.Judgement ? 3 : 1;

        // ---------------- Grave Marks ----------------

        /// <summary>
        /// Where a Grave Mark is placed: where she stands, moved along her motion by her velocity times
        /// <see cref="BossTrialValues.MarkLeadSeconds"/>, at most <see cref="BossTrialValues.MarkLeadMost"/> (with
        /// <see cref="BossTrialValues.GraveMarksLead"/> on, as the caster's Grave Rain); none when she stands.
        /// </summary>
        public static GroundPoint MarkPlace(GroundPoint her, GroundPoint velocity, bool lead, BossTrialValues v)
        {
            if (!lead) return her;
            float speed = velocity.Length;
            if (speed <= v.MarkMovingOver || speed < 1e-5f) return her;
            float ahead = Math.Min(speed * Math.Max(0f, v.MarkLeadSeconds), Math.Max(0f, v.MarkLeadMost));
            return her + velocity * (ahead / speed);
        }

        /// <summary>s from the start of Grave Marks' wind-up that circle <paramref name="index"/> (from 0) is placed.</summary>
        public static float MarkPlacedAt(int index, BossTrialValues v) => Math.Max(0, index) * v.MarkEvery;

        /// <summary>s from the start of the wind-up to its last burst: the whole of Grave Marks' wind-up.</summary>
        public static float MarksWhole(float burst, BossTrialValues v) => MarkPlacedAt(v.Marks - 1, v) + burst;

        // ---------------- Spokes ----------------

        /// <summary>The direction of spoke <paramref name="index"/> of a set (0, the first; 1, the second turned by <see cref="BossTrialValues.SpokeTurn"/>), from its aim.</summary>
        public static GroundPoint SpokeDirection(GroundPoint aim, int set, int index, BossTrialValues v)
        {
            double a = Math.Atan2(aim.Z, aim.X);
            double step = 2.0 * Math.PI / Math.Max(1, v.SpokeCount);
            double turn = set == 0 ? 0.0 : v.SpokeTurn * Math.PI / 180.0;
            double t = a + index * step + turn;
            return new GroundPoint((float)Math.Cos(t), (float)Math.Sin(t));
        }

        /// <summary>Does a spoke from the origin along a direction, <paramref name="length"/> long, touch her?</summary>
        public static bool SpokeHits(GroundPoint origin, GroundPoint dir, float length, float halfWidth, GroundPoint her, float herRadius) =>
            length > 0f && LineShot.Hits(origin, origin + dir, length, halfWidth, her, herRadius);

        /// <summary>How far through its fill a spoke is, 0 to 1: the inner outline's share of its half width.</summary>
        public static float Fill(float since, float fill) => fill <= 0f ? 1f : Clamp01(since / fill);

        // ---------------- The Wave and the Shockwave: bands that move ----------------

        /// <summary>
        /// Does a band moving outward touch her this step: its middle went from <paramref name="before"/> to <paramref name="now"/> m
        /// (from where it began, along its line or round its middle), its half depth <paramref name="half"/>, she <paramref name="at"/> m
        /// out with radius <paramref name="herRadius"/>? Swept, so no step passes over her.
        /// </summary>
        public static bool BandTouches(float before, float now, float half, float at, float herRadius) =>
            at + herRadius >= Math.Min(before, now) - half && at - herRadius <= Math.Max(before, now) + half;

        /// <summary>Where a ring's middle is <paramref name="since"/> s after the landing; past its end, beyond it.</summary>
        public static float RingRadius(float since, BossTrialValues v) => v.RingFrom + Math.Max(0f, since) * v.RingSpeed;

        /// <summary>s from the landing until the ring has passed its last metre.</summary>
        public static float RingSeconds(BossTrialValues v) => v.RingSpeed > 0f ? (v.RingTo - v.RingFrom) / v.RingSpeed : 0f;

        /// <summary>The index of the direction nearest this one among <paramref name="samples"/> evenly round, the first along +x.</summary>
        public static int SampleOf(float dx, float dz, int samples)
        {
            if (samples <= 0) return 0;
            double a = Math.Atan2(dz, dx);
            if (a < 0) a += 2.0 * Math.PI;
            int i = (int)Math.Round(a / (2.0 * Math.PI) * samples);
            return ((i % samples) + samples) % samples;
        }

        /// <summary>The direction of sample <paramref name="i"/> of <paramref name="samples"/>.</summary>
        public static GroundPoint SampleDirection(int i, int samples)
        {
            double a = 2.0 * Math.PI * i / Math.Max(1, samples);
            return new GroundPoint((float)Math.Cos(a), (float)Math.Sin(a));
        }

        /// <summary>
        /// Is she behind what stops the ring: the clear distance in her direction (from the table looked up once at the landing) is
        /// shorter than her distance? The table is the drawing's too, so the drawn ring and the judged one are the same.
        /// </summary>
        public static bool Sheltered(float[] clear, GroundPoint middle, GroundPoint her)
        {
            if (clear == null || clear.Length == 0) return false;
            GroundPoint d = her - middle;
            return d.Length > clear[SampleOf(d.X, d.Z, clear.Length)];
        }

        // ---------------- Seeds ----------------

        /// <summary>
        /// The places three seeds are thrown to, before the ground and his screen are asked: round her at
        /// <paramref name="distance"/>, a third of a turn apart, the first at <paramref name="turn"/> radians.
        /// </summary>
        public static GroundPoint SeedPlace(GroundPoint her, int index, int count, double turn, float distance)
        {
            double a = turn + index * 2.0 * Math.PI / Math.Max(1, count);
            return her + new GroundPoint((float)Math.Cos(a), (float)Math.Sin(a)) * distance;
        }

        /// <summary>Does a basic shot's line from her origin toward its aim, as far as <paramref name="reach"/>, touch a seed?</summary>
        public static bool ShotTouchesSeed(GroundPoint origin, GroundPoint aim, float reach, float halfWidth, GroundPoint seed, float seedRadius) =>
            reach > 0f && LineShot.Hits(origin, aim, reach, halfWidth, seed, seedRadius);

        // ---------------- Judgement ----------------

        /// <summary>The closing circle's radius <paramref name="since"/> s into the ward: from <see cref="BossTrialValues.WardCircleFrom"/> down to <see cref="BossTrialValues.WardCircleTo"/>.</summary>
        public static float WardRadius(float since, BossTrialValues v) =>
            v.WardCircleFrom + (v.WardCircleTo - v.WardCircleFrom) * Clamp01(v.WardSeconds > 0f ? since / v.WardSeconds : 1f);

        /// <summary>The s into the ward of the Grave Marks it drops on her: every <see cref="BossTrialValues.WardMarkEvery"/>, none in the last <see cref="BossTrialValues.WardMarksStopBefore"/>.</summary>
        public static int WardMarks(BossTrialValues v)
        {
            int n = 0;
            if (v.WardMarkEvery <= 0f) return 0;
            for (float t = v.WardMarkEvery; t <= v.WardSeconds - v.WardMarksStopBefore + 1e-4f; t += v.WardMarkEvery) n++;
            return n;
        }

        /// <summary>The Judgement due at this share, if any not yet come (each share once, highest first): its index, else -1.</summary>
        public static int JudgementDue(float share, int come, BossTrialValues v) =>
            v.JudgementAt != null && come < v.JudgementAt.Length && share <= v.JudgementAt[come] ? come : -1;

        // ---------------- The crash ----------------

        /// <summary>
        /// Did a charge end in a crash (7.2: "ends at a pillar or wall")? Only a charge cut short of its length; and then only when, from
        /// where it stopped, a wall or tall prop stands within its body's radius and <see cref="BossTrialValues.CrashSlack"/> in front of
        /// it along one of its three lines (<paramref name="nearestAhead"/>, the nearest of the three).
        /// </summary>
        public static bool Crashed(float reach, float length, float nearestAhead, float bodyRadius, BossTrialValues v) =>
            reach < length - 1e-3f && nearestAhead <= bodyRadius + v.CrashSlack + 1e-4f;

        // ---------------- The hurt guard ----------------

        /// <summary>Is her body hidden at this moment of a guard begun at <paramref name="began"/>: the flicker's every other half.</summary>
        public static bool FlickerHidden(float now, float began, BossTrialValues v)
        {
            if (v.HurtFlickerHalf <= 0f || now < began || now >= began + v.HurtGuardSeconds) return false;
            return ((int)((now - began) / v.HurtFlickerHalf) & 1) == 1;
        }

        static int Clamp(int i, int lo, int hi) => i < lo ? lo : i > hi ? hi : i;
        static float Clamp01(float f) => f < 0f ? 0f : f > 1f ? 1f : f;
    }

    /// <summary>
    /// The hurt guard on her side (work order 147, 7.1): after a hit of the trial boss or its patterns she cannot be hit again for
    /// <see cref="BossTrialValues.HurtGuardSeconds"/>. Only in the trial fight: on while one is awake. With no engine in it.
    /// </summary>
    public sealed class HurtGuardRule
    {
        float _until = float.NegativeInfinity;

        /// <summary>When the guard running began; meaningless when none runs.</summary>
        public float Began { get; private set; } = float.NegativeInfinity;
        /// <summary>True while a trial fight is on: only then does a hit start a guard.</summary>
        public bool Active;

        /// <summary>Does the guard hold off a hit now?</summary>
        public bool Holds(float now) => now < _until;

        /// <summary>A hit landed now: the guard starts (only with <see cref="Active"/> on).</summary>
        public void Took(float now, float seconds)
        {
            if (!Active || seconds <= 0f) return;
            Began = now;
            _until = now + seconds;
        }

        /// <summary>No guard: the fight over.</summary>
        public void Clear()
        {
            _until = float.NegativeInfinity;
            Began = float.NegativeInfinity;
        }
    }

    /// <summary>What the chooser is told at a choice (work order 147): the moment, the fight's state, her distance and the attacks' fits.</summary>
    public struct BossTrialSituation
    {
        public float Now;
        public int Phase;
        public float WokeAt;
        /// <summary>When the last recovery ended (or a transition or a Judgement); its gap runs from there.</summary>
        public float LastEnded;
        public float Distance;
        /// <summary>Its body is on his screen.</summary>
        public bool OnScreen;
        /// <summary>A charge at her now fits: it sees her, and its line, as cut, reaches her (work order 146).</summary>
        public bool ChargeFits;
        /// <summary>A slam at her fits: within its reach, seen when far.</summary>
        public bool SlamFits;
        /// <summary>A pattern is still on the ground (seeds, a ring, circles): only a body attack may begin (the overlap rule).</summary>
        public bool GroundLive;
        /// <summary>When the last big pattern ended; and when the last dash-only attack began.</summary>
        public float LastBigEnded, LastDashOnly;
    }

    /// <summary>
    /// How the trial boss chooses (work order 147, section 7.3): phase 1 opens with a fixed teaching order; phase 2 opens with its new
    /// attacks, each alone and none new for <see cref="BossTrialValues.NothingNewAfter"/> after each; then equal-weight draws among the
    /// attacks that are eligible (in its distance band, under its in-a-row limit, enough others since its last use, the 10 s dash rule,
    /// the overlap rule, its own spacing) from a seeded source. Nothing begins before <see cref="BossTrialValues.FirstAttackAfter"/>
    /// after the waking, within the phase's gap after a recovery, or with its body off his screen. Judgement is not chosen here: it
    /// comes by its health shares. With no engine in it.
    /// </summary>
    public sealed class BossTrialChooser
    {
        const int Kinds = 8;
        readonly Random _rng;
        readonly int[] _since = new int[Kinds];
        readonly float[] _lastStart = new float[Kinds];
        readonly BossTrialAttack[] _pool = new BossTrialAttack[Kinds];
        int _taught;
        float _teachAsked = -1f;
        int _opened;
        float _openedAt = float.NegativeInfinity;
        float _openAsked = -1f;

        public BossTrialChooser(int seed)
        {
            _rng = new Random(seed);
            for (int i = 0; i < Kinds; i++)
            {
                _since[i] = 1000;
                _lastStart[i] = float.NegativeInfinity;
            }
        }

        /// <summary>The phase it chooses for; <see cref="EnterPhase"/> moves it.</summary>
        public int Phase { get; private set; } = 1;
        public BossTrialAttack Last { get; private set; }
        public int InARow { get; private set; }
        /// <summary>The teaching order's place: how many of it have come or been passed over.</summary>
        public int Taught => _taught;
        /// <summary>Phase 2's opening attacks that have come.</summary>
        public int Opened => _opened;
        /// <summary>When an attack last began (negative infinity: never).</summary>
        public float LastStart(BossTrialAttack a) => _lastStart[(int)a];
        /// <summary>Other attacks since its last use.</summary>
        public int Since(BossTrialAttack a) => _since[(int)a];

        /// <summary>A new phase begins (after its transition): its opening starts.</summary>
        public void EnterPhase(int phase)
        {
            Phase = phase;
            _opened = 0;
            _openedAt = float.NegativeInfinity;
            _openAsked = -1f;
        }

        /// <summary>The attack to begin now, or None to walk on. Counts what it gives.</summary>
        public BossTrialAttack Choose(in BossTrialSituation s, BossTrialValues v)
        {
            if (!Ready(s, v)) return BossTrialAttack.None;
            BossTrialAttack a = Pick(s, v);
            if (a != BossTrialAttack.None) Began(a, s.Now);
            return a;
        }

        /// <summary>May anything begin now: past the first wait, the phase's gap, its body on his screen.</summary>
        public static bool Ready(in BossTrialSituation s, BossTrialValues v) =>
            s.OnScreen && s.Now >= s.WokeAt + v.FirstAttackAfter - 1e-4f && s.Now >= s.LastEnded + BossTrialRules.Gap(s.Phase, v) - 1e-4f;

        BossTrialAttack Pick(in BossTrialSituation s, BossTrialValues v)
        {
            // Phase 1: the teaching order first, each when it fits; one that will not fit is passed over after a wait.
            if (Phase == 1 && v.Teaching != null && _taught < v.Teaching.Length)
            {
                while (_taught < v.Teaching.Length && !BossTrialValues.IsOn(v.Teaching[_taught])) _taught++;
                if (_taught < v.Teaching.Length)
                {
                    BossTrialAttack next = v.Teaching[_taught];
                    if (Fits(next, s, v, teaching: true))
                    {
                        _taught++;
                        _teachAsked = -1f;
                        return next;
                    }
                    if (_teachAsked < 0f) _teachAsked = s.Now;
                    if (s.Now - _teachAsked < v.TeachWaitMost) return BossTrialAttack.None;
                    _taught++; // passed over
                    _teachAsked = -1f;
                }
            }
            // Phase 2 on: its new attacks first, each alone, none new for a while after each.
            if (Phase >= 2 && v.PhaseTwoOpening != null && _opened < v.PhaseTwoOpening.Length)
            {
                while (_opened < v.PhaseTwoOpening.Length && !BossTrialValues.IsOn(v.PhaseTwoOpening[_opened])) _opened++;
                // The first at once (after the transition's gap); each later one when the one before has ended and 3 s more have passed.
                bool rested = _opened == 0 || (s.LastBigEnded >= _openedAt && s.Now >= s.LastBigEnded + v.NothingNewAfter - 1e-4f)
                              || s.Now - _openedAt >= v.OpeningWaitMost; // a pattern that never told its end does not hold the opening for ever
                if (_opened < v.PhaseTwoOpening.Length && rested && Phase == 2)
                {
                    BossTrialAttack next = v.PhaseTwoOpening[_opened];
                    if (Fits(next, s, v, teaching: true) && !s.GroundLive)
                    {
                        _opened++;
                        _openedAt = s.Now;
                        _openAsked = -1f;
                        return next;
                    }
                    if (_openAsked < 0f) _openAsked = s.Now;
                    if (s.Now - _openAsked >= v.TeachWaitMost)
                    {
                        _opened++; // passed over: it comes by the draws
                        _openAsked = -1f;
                    }
                }
            }
            // The draws: equal weights among the eligible.
            int n = 0;
            for (int k = 1; k < Kinds; k++)
            {
                var a = (BossTrialAttack)k;
                if (a == BossTrialAttack.Judgement || !Eligible(a, s, v)) continue;
                _pool[n++] = a;
            }
            return n == 0 ? BossTrialAttack.None : _pool[_rng.Next(n)];
        }

        /// <summary>Is the attack eligible in the draws now (7.3)?</summary>
        public bool Eligible(BossTrialAttack a, in BossTrialSituation s, BossTrialValues v)
        {
            if (!Fits(a, s, v, teaching: false)) return false;
            // A new attack of phase 2 is drawn only once its opening has come (or with the opening over).
            if (v.PhaseTwoOpening != null && Phase == 2)
                for (int i = _opened; i < v.PhaseTwoOpening.Length; i++)
                    if (v.PhaseTwoOpening[i] == a) return false;
            int most = a == BossTrialAttack.Cleave ? v.CleaveInARowMost : v.InARowMost;
            if (a == Last && InARow >= most) return false;
            if ((a == BossTrialAttack.Spokes || a == BossTrialAttack.Seeds || a == BossTrialAttack.Slam) && _since[(int)a] < v.OthersBetween) return false;
            return true;
        }

        /// <summary>
        /// Does the attack fit now, by its switch, its phase, its band, its own spacing, the dash rule and the overlap rule? The
        /// teaching order and the openings ask this only, not the in-a-row and between rules.
        /// </summary>
        public bool Fits(BossTrialAttack a, in BossTrialSituation s, BossTrialValues v, bool teaching)
        {
            if (!BossTrialValues.IsOn(a) || BossTrialRules.FirstPhase(a) > Math.Max(Phase, s.Phase)) return false;
            if (s.GroundLive && !BossTrialRules.IsBody(a)) return false; // the overlap rule: one ground pattern and one body attack at once
            float since = s.Now - _lastStart[(int)a];
            switch (a)
            {
                case BossTrialAttack.Cleave:
                    return s.Distance <= v.CleaveWithin;
                case BossTrialAttack.Charge:
                    return s.ChargeFits && s.Distance >= v.ChargeFrom && s.Distance <= v.ChargeTo && since >= v.ChargeEvery - 1e-4f;
                case BossTrialAttack.GraveMarks:
                    return since >= v.MarksEvery - 1e-4f;
                case BossTrialAttack.Spokes:
                    return s.Distance <= v.SpokesWithin;
                case BossTrialAttack.Seeds:
                    return true;
                case BossTrialAttack.Slam:
                    return s.SlamFits && s.Now - s.LastDashOnly >= v.DashOnlyEvery - 1e-4f && s.Now - s.LastBigEnded >= v.BigPatternApart - 1e-4f;
                default:
                    return false;
            }
        }

        /// <summary>An attack began (chosen here, or a combo's or Judgement's): the counts move.</summary>
        public void Began(BossTrialAttack a, float now)
        {
            if (a == BossTrialAttack.None) return;
            for (int k = 1; k < Kinds; k++) if (k != (int)a && _since[k] < 1000) _since[k]++;
            _since[(int)a] = 0;
            _lastStart[(int)a] = now;
            InARow = a == Last ? InARow + 1 : 1;
            Last = a;
        }
    }
}
