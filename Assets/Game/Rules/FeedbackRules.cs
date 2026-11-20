using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the timing feedback (work order 019, 2026-11-20), all first guesses, with their defaults here in
    /// code: no asset holds them. Pillar 2, "high tempo with skill expression": a timing skill that gives no sign of
    /// success is guessing, so the moments that take skill show a word, her hits show their damage, and the three best
    /// moments sound a short tone. Sizes and distances on screen are in pixels of a view 720 high, as the skill bar's.
    /// </summary>
    [Serializable]
    public sealed class FeedbackValues
    {
        // The words (part 1).
        public float WordSeconds = 0.7f;    // s a word shows, rising and fading
        public float WordRise = 34f;        // px a word rises over its life
        public int MaxWords = 8;            // words on screen at once; the newest win
        public int WordSize = 26;           // px: CLEAN, COUNTER, BOOM, DAZED, x3
        public int BigWordSize = 34;        // px: PERFECT, and the marks at the cap
        public int SmallWordSize = 17;      // px: EARLY, LATE
        public float Pop = 1.3f;            // a word or number starts this much larger and settles
        public float PopSeconds = 0.08f;    // s it takes to settle
        public float FadeFrom = 0.55f;      // the share of its life after which it fades out
        /// <summary>
        /// Long Shot let go outside its band says EARLY or LATE only after a charge of at least this share of its
        /// charge time (half way): a tap with no charge is a choice, not a miss.
        /// </summary>
        public float MissFromCharge = 0.5f;

        // The damage numbers (part 2).
        public float NumberSeconds = 0.6f;  // s a number shows after the latest hit it counts
        public float NumberRise = 28f;      // px a number rises over its life
        public float MergeSeconds = 0.15f;  // a hit this soon after the latest hit of a target's number joins it
        /// <summary>
        /// Grace on top of <see cref="MergeSeconds"/> for the frame a hit is taken in: Fan Fire ticks exactly every 0.15 s,
        /// and without it a frame early or late would join one tick and split the next.
        /// </summary>
        public float MergeGrace = 0.03f;
        public int MaxNumbers = 24;         // numbers on screen at once; the newest win
        public int NumberSize = 17;         // px: an ordinary hit
        public int BigNumberSize = 24;      // px: marks spent, a perfect release, a hit on a dazed target
        public int HurtNumberSize = 20;     // px: damage she takes, red
        public bool DamageNumbers = true;   // shown, unless the pause menu's setting says otherwise

        // The tones (part 3).
        public bool Tones = true;           // off: no tone at all
        public float ToneVolume = 0.33f;    // the share of the basic shot's volume a tone plays at
    }

    /// <summary>
    /// The moments that take skill (work order 019): each gives one word. Work order 035: Stunned and Pulled, two things
    /// that happen to her, a word over her each. Work order 043: Ducked, a quick duck that stopped something, over her.
    /// </summary>
    public enum FeedbackMoment { Clean, Perfect, Counter, MarksSpent, Boom, Dazed, Early, Late, Stunned, Pulled, Ducked }

    /// <summary>The colours of the feedback, named for where they come from; the game picks the colour for each.</summary>
    public enum FeedbackTint
    {
        Clean,     // the green of the skill bar's clean zone
        Counter,   // the blue of a counterable telegraph
        Marks,     // the orange of the mark pips
        MarksFull, // the red of the mark pips at the cap
        Boom,      // orange
        Dazed,     // the light blue of the daze tint
        Miss,      // grey
        Plain,     // white: an ordinary hit's number
        Hurt,      // red: damage she takes
    }

    public enum FeedbackSize { Small, Normal, Large }

    /// <summary>A word as it shows: its text, colour and size, and its rank among words of the same moment on one target.</summary>
    public struct FeedbackWord
    {
        public string Text;
        public FeedbackTint Tint;
        public FeedbackSize Size;
        /// <summary>
        /// Two words for one target in the same moment (a clean spend that empties a stagger bar: CLEAN, x5 and DAZED in
        /// one frame): the higher rank stays. Across moments the newer word always wins.
        /// </summary>
        public int Rank;
    }

    /// <summary>What a missed timing was.</summary>
    public enum TimingMiss { None, Early, Late }

    /// <summary>
    /// What a damage number stands for, weakest first: a hit that spent marks, was a perfect release or landed on a
    /// dazed target shows larger and in the colour of its word. A number that sums several hits shows the strongest.
    /// </summary>
    /// <summary>Heal (work order 030): health back when a combat room is cleared, a green "+8" over her.</summary>
    public enum NumberKind { Plain, Dazed, Marks, MarksFull, Perfect, Hurt, Heal }

    /// <summary>The rules of the timing feedback (work order 019, 2026-11-20), with no engine in them.</summary>
    public static class FeedbackRules
    {
        /// <summary>
        /// The word each moment gives: CLEAN (a clean tap, or Backstep's clean second press), PERFECT (Long Shot let go in
        /// its band), COUNTER (Stopping Shot on a blue attack from the front), "x3" (marks spent: how many were taken off
        /// that target, red and larger at the cap), BOOM (a keg set off by a shot), DAZED (a stagger bar emptied), and
        /// EARLY or LATE (a press that missed its timing). <paramref name="marks"/> and <paramref name="cap"/> are for marks.
        /// </summary>
        public static FeedbackWord Word(FeedbackMoment moment, int marks = 0, int cap = 0)
        {
            switch (moment)
            {
                case FeedbackMoment.Clean: return new FeedbackWord { Text = "CLEAN", Tint = FeedbackTint.Clean, Size = FeedbackSize.Normal, Rank = 4 };
                case FeedbackMoment.Perfect: return new FeedbackWord { Text = "PERFECT", Tint = FeedbackTint.Clean, Size = FeedbackSize.Large, Rank = 5 };
                case FeedbackMoment.Counter: return new FeedbackWord { Text = "COUNTER", Tint = FeedbackTint.Counter, Size = FeedbackSize.Normal, Rank = 6 };
                case FeedbackMoment.MarksSpent:
                    bool full = cap > 0 && marks >= cap;
                    return new FeedbackWord
                    {
                        Text = "x" + Math.Max(0, marks).ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Tint = full ? FeedbackTint.MarksFull : FeedbackTint.Marks, Size = full ? FeedbackSize.Large : FeedbackSize.Normal, Rank = 2,
                    };
                case FeedbackMoment.Boom: return new FeedbackWord { Text = "BOOM", Tint = FeedbackTint.Boom, Size = FeedbackSize.Normal, Rank = 1 };
                case FeedbackMoment.Dazed: return new FeedbackWord { Text = "DAZED", Tint = FeedbackTint.Dazed, Size = FeedbackSize.Normal, Rank = 3 };
                case FeedbackMoment.Early: return new FeedbackWord { Text = "EARLY", Tint = FeedbackTint.Miss, Size = FeedbackSize.Small, Rank = 0 };
                // Work order 035: in the blue of the attacks that do it, which Stopping Shot could have countered.
                case FeedbackMoment.Stunned: return new FeedbackWord { Text = "STUNNED", Tint = FeedbackTint.Counter, Size = FeedbackSize.Normal, Rank = 3 };
                case FeedbackMoment.Pulled: return new FeedbackWord { Text = "PULLED", Tint = FeedbackTint.Counter, Size = FeedbackSize.Normal, Rank = 3 };
                // Work order 043: a timing that worked, in the green of the other timings' words.
                case FeedbackMoment.Ducked: return new FeedbackWord { Text = "DUCKED", Tint = FeedbackTint.Clean, Size = FeedbackSize.Normal, Rank = 3 };
                default: return new FeedbackWord { Text = "LATE", Tint = FeedbackTint.Miss, Size = FeedbackSize.Small, Rank = 0 };
            }
        }

        /// <summary>The word of a missed timing, or none.</summary>
        public static FeedbackMoment? Word(TimingMiss miss) =>
            miss == TimingMiss.Early ? FeedbackMoment.Early : miss == TimingMiss.Late ? FeedbackMoment.Late : (FeedbackMoment?)null;

        /// <summary>
        /// A recast press (Triple Tap's last press, Backstep's shot) that missed its timing: the press that fires the last
        /// shot of a chain whose skill has a clean press, and was not clean, came before the clean part of its window. A
        /// press after the window is no shot at all (the chain is over), and the earlier presses of a chain have no timing.
        /// <paramref name="shot"/> is which shot of the chain the press fired.
        /// </summary>
        public static TimingMiss RecastMiss(SkillDefinition skill, int shot, bool clean) => RecastMiss(skill, shot, clean, false);

        /// <summary>
        /// As above, and <paramref name="late"/>: the press came after the clean part, in the window (work order 040: the
        /// clean part comes early, so a press can now miss it either way): LATE, not EARLY.
        /// </summary>
        public static TimingMiss RecastMiss(SkillDefinition skill, int shot, bool clean, bool late)
        {
            if (skill == null || clean || skill.RecastCount < 2 || shot < skill.RecastCount) return TimingMiss.None;
            return RecastChain.HasCleanPress(skill) ? (late ? TimingMiss.Late : TimingMiss.Early) : TimingMiss.None;
        }

        /// <summary>
        /// A charge (Long Shot) that missed its perfect band: let go before it (EARLY) or after it, or held until it fired by
        /// itself (LATE), having charged at least <paramref name="fromShare"/> of its charge time. Less charge than that is
        /// a choice, and says nothing; so does a skill with no band, and a release inside it.
        /// </summary>
        public static TimingMiss ChargeMiss(SkillDefinition skill, float chargedSeconds, bool perfect, float fromShare)
        {
            if (perfect || skill == null || skill.Input != SkillInput.Charge || !(skill.PerfectTo > skill.PerfectFrom)) return TimingMiss.None;
            float full = skill.ChargeTime > 0f ? skill.ChargeTime : skill.PerfectTo;
            if (chargedSeconds < Math.Max(0f, fromShare) * full - 1e-4f) return TimingMiss.None;
            if (PoolRules.IsPerfect(skill, chargedSeconds)) return TimingMiss.None;
            return chargedSeconds < skill.PerfectFrom ? TimingMiss.Early : TimingMiss.Late;
        }

        /// <summary>The kind of a number for a hit that spent marks (at the cap or not), was perfect, or landed on a dazed target.</summary>
        public static NumberKind HitKind(bool perfect, int marksSpent, int cap, bool onDazed)
        {
            if (perfect) return NumberKind.Perfect;
            if (marksSpent > 0) return cap > 0 && marksSpent >= cap ? NumberKind.MarksFull : NumberKind.Marks;
            return onDazed ? NumberKind.Dazed : NumberKind.Plain;
        }

        /// <summary>The colour of a number: its word's for a hit that stands out, white for an ordinary one, red for hers.</summary>
        public static FeedbackTint NumberTint(NumberKind kind)
        {
            switch (kind)
            {
                case NumberKind.Perfect: return FeedbackTint.Clean;
                case NumberKind.MarksFull: return FeedbackTint.MarksFull;
                case NumberKind.Marks: return FeedbackTint.Marks;
                case NumberKind.Dazed: return FeedbackTint.Dazed;
                case NumberKind.Hurt: return FeedbackTint.Hurt;
                case NumberKind.Heal: return FeedbackTint.Clean; // work order 030: green
                default: return FeedbackTint.Plain;
            }
        }

        /// <summary>True for a number shown larger: marks spent, a perfect release, a hit on a dazed target.</summary>
        public static bool IsBig(NumberKind kind) => kind == NumberKind.Dazed || kind == NumberKind.Marks || kind == NumberKind.MarksFull || kind == NumberKind.Perfect;

        // ---------------- The setting ----------------

        /// <summary>The stored damage numbers setting: none (the default rules), off, on.</summary>
        public const int NoOverride = -1;
        public const int NumbersOff = 0;
        public const int NumbersOn = 1;

        /// <summary>Are damage numbers shown? The pause menu's choice if one is stored, else the default.</summary>
        public static bool ShowsDamageNumbers(int stored, bool fallback) =>
            stored == NumbersOn || (stored != NumbersOff && fallback);

        /// <summary>A stored value made safe: on, off, or none.</summary>
        public static int StoredNumbers(int stored) => stored == NumbersOn || stored == NumbersOff ? stored : NoOverride;

        // ---------------- Showing: a fading share of the life ----------------

        /// <summary>The opacity of something <paramref name="age"/> s into a life of <paramref name="life"/> s: full, then down to 0 from the fade share.</summary>
        public static float Alpha(float age, float life, float fadeFrom)
        {
            if (life <= 0f || age >= life) return 0f;
            float k = Math.Max(0f, age) / life;
            float from = Math.Max(0f, Math.Min(0.999f, fadeFrom));
            return k <= from ? 1f : 1f - (k - from) / (1f - from);
        }

        /// <summary>The size factor <paramref name="age"/> s after it appeared: <paramref name="pop"/>, down to 1 over <paramref name="popSeconds"/>.</summary>
        public static float PopScale(float age, float pop, float popSeconds)
        {
            if (popSeconds <= 0f || age >= popSeconds) return 1f;
            float k = Math.Max(0f, age) / popSeconds;
            return pop + (1f - pop) * k;
        }
    }

    /// <summary>A word on the board: which target it is over (the game's own key), the word, and when it came.</summary>
    public struct BoardWord
    {
        public int Id;
        public int Target;
        public FeedbackWord Word;
        public float Born;
        /// <summary>Which moment it came in: words of the same moment on one target are ranked (<see cref="FeedbackWord.Rank"/>).</summary>
        public int Moment;
    }

    /// <summary>
    /// The words on screen (work order 019): at most one per target at a time, a new one replacing the old (but in the
    /// same moment the higher rank stays); at most <see cref="FeedbackValues.MaxWords"/> in all, the oldest going first
    /// so the newest win. A word lives <see cref="FeedbackValues.WordSeconds"/>. Time is passed in, in seconds.
    /// </summary>
    public sealed class WordBoard
    {
        readonly List<BoardWord> _words = new List<BoardWord>(); // oldest first
        int _nextId = 1;

        public IReadOnlyList<BoardWord> Words => _words;

        /// <summary>Puts a word over a target. Returns its id, or 0 when a word of a higher rank from the same moment keeps the place.</summary>
        public int Add(int target, FeedbackWord word, float now, int moment, int max)
        {
            for (int i = 0; i < _words.Count; i++)
            {
                if (_words[i].Target != target) continue;
                if (_words[i].Moment == moment && _words[i].Word.Rank > word.Rank) return 0;
                _words.RemoveAt(i);
                break;
            }
            var added = new BoardWord { Id = _nextId++, Target = target, Word = word, Born = now, Moment = moment };
            _words.Add(added);
            while (_words.Count > Math.Max(1, max)) _words.RemoveAt(0);
            return added.Id;
        }

        /// <summary>Takes off every word that has lived its time by now.</summary>
        public void Expire(float now, float seconds)
        {
            for (int i = _words.Count - 1; i >= 0; i--)
                if (now - _words[i].Born >= seconds) _words.RemoveAt(i);
        }

        public void Clear() => _words.Clear();
    }

    /// <summary>A damage number: whose, the sum of the hits it counts, when the first and the latest came.</summary>
    public struct TallyNumber
    {
        public int Id;
        public int Target;
        public int Sum;
        public int Hits;
        public float First;
        public float Last;
        public NumberKind Kind;
    }

    /// <summary>
    /// The damage numbers (work order 019): each hit shows its damage over its target, but a hit that comes no more than
    /// the merge window (<see cref="FeedbackValues.MergeSeconds"/>, with its grace) after the latest hit of that target's
    /// number joins it: the number shows the sum, so a held skill or a burst does not spray numbers. A hit after the
    /// window starts a new number. A number lives <see cref="FeedbackValues.NumberSeconds"/> from its latest hit, and
    /// shows the strongest kind of the hits it counts. At most <see cref="FeedbackValues.MaxNumbers"/>, the newest win.
    /// Time is passed in, in seconds.
    /// </summary>
    public sealed class DamageTally
    {
        readonly List<TallyNumber> _numbers = new List<TallyNumber>(); // oldest first
        int _nextId = 1;

        public IReadOnlyList<TallyNumber> Numbers => _numbers;

        /// <summary>
        /// Counts a hit of <paramref name="damage"/> on a target. True when it joined an open number, false when it started
        /// one (or was no damage). <paramref name="window"/> is the merge window with its grace.
        /// </summary>
        public bool Add(int target, int damage, NumberKind kind, float now, float window, int max)
        {
            if (damage <= 0) return false;
            for (int i = _numbers.Count - 1; i >= 0; i--)
            {
                if (_numbers[i].Target != target) continue;
                TallyNumber open = _numbers[i];
                // Only the target's latest number can be open.
                if (now - open.Last > window + 1e-5f) break;
                open.Sum += damage;
                open.Hits++;
                open.Last = Math.Max(open.Last, now);
                if (kind > open.Kind) open.Kind = kind;
                _numbers[i] = open;
                return true;
            }
            _numbers.Add(new TallyNumber { Id = _nextId++, Target = target, Sum = damage, Hits = 1, First = now, Last = now, Kind = kind });
            while (_numbers.Count > Math.Max(1, max)) _numbers.RemoveAt(0);
            return false;
        }

        /// <summary>Takes off every number whose latest hit is <paramref name="seconds"/> or more ago.</summary>
        public void Expire(float now, float seconds)
        {
            for (int i = _numbers.Count - 1; i >= 0; i--)
                if (now - _numbers[i].Last >= seconds) _numbers.RemoveAt(i);
        }

        public void Clear() => _numbers.Clear();
    }

    /// <summary>The three tones (work order 019): a clean tap, a perfect release, a counter. A missed timing has none.</summary>
    public enum FeedbackTone { Clean, Perfect, Counter }

    /// <summary>
    /// The tones, made in code at start-up (no sound files; work order 019): a sine or two with a fast attack and a short
    /// decay, each under 0.2 s. A clean tap is one bright note (G6, 1568 Hz, with its octave); a perfect release two
    /// rising notes (D6 1175 Hz, then G6 1568 Hz); a counter a lower, harder note (G4, 392 Hz, with its fifth and a third
    /// harmonic, driven a little into a soft clip). The samples peak at <see cref="Peak"/>; the volume is the game's.
    /// </summary>
    public static class ToneRules
    {
        public const int SampleRate = 44100;
        public const float Attack = 0.004f;   // s to full
        public const float EndFade = 0.006f;  // s to silence at the very end, so it does not click
        public const float Peak = 0.9f;

        public const float CleanFrequency = 1568f;
        public const float PerfectLow = 1175f, PerfectHigh = 1568f;
        public const float CounterFrequency = 392f;

        /// <summary>How long a tone is, in seconds.</summary>
        public static float Seconds(FeedbackTone tone)
        {
            switch (tone)
            {
                case FeedbackTone.Clean: return 0.12f;
                case FeedbackTone.Perfect: return 0.17f;
                default: return 0.15f;
            }
        }

        /// <summary>The samples of a tone, mono, at <paramref name="sampleRate"/>, peaking at <see cref="Peak"/>.</summary>
        public static float[] Samples(FeedbackTone tone, int sampleRate = SampleRate)
        {
            int rate = Math.Max(8000, sampleRate);
            float seconds = Seconds(tone);
            int n = (int)Math.Round(seconds * rate);
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                double t = i / (double)rate;
                double v;
                switch (tone)
                {
                    case FeedbackTone.Clean:
                        v = Note(t, CleanFrequency, 0.035) + 0.35 * Note(t, CleanFrequency * 2f, 0.02);
                        break;
                    case FeedbackTone.Perfect:
                        const double second = 0.075;
                        v = t < second
                            ? Note(t, PerfectLow, 0.03)
                            : Note(t - second, PerfectHigh, 0.035) + 0.3 * Note(t - second, PerfectHigh * 2f, 0.02);
                        break;
                    default:
                        double raw = Note(t, CounterFrequency, 0.045) + 0.5 * Note(t, CounterFrequency * 1.5f, 0.04)
                                     + 0.3 * Note(t, CounterFrequency * 3f, 0.025);
                        v = Math.Tanh(2.2 * raw);
                        break;
                }
                double fromEnd = (n - 1 - i) / (double)rate;
                if (fromEnd < EndFade) v *= fromEnd / EndFade;
                s[i] = (float)v;
            }
            float peak = 0f;
            foreach (float v in s) peak = Math.Max(peak, Math.Abs(v));
            if (peak > 1e-6f)
                for (int i = 0; i < n; i++) s[i] *= Peak / peak;
            return s;
        }

        /// <summary>One sine from t = 0: up over the attack, then down by e every <paramref name="decay"/> s.</summary>
        static double Note(double t, double frequency, double decay)
        {
            if (t < 0) return 0;
            double rise = t < Attack ? t / Attack : 1;
            return rise * Math.Exp(-t / decay) * Math.Sin(2 * Math.PI * frequency * t);
        }
    }
}
