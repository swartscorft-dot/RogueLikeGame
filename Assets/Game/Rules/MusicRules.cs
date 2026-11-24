using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// What the music plays (work order 116, a trial): CALM while no room's fight is on, FIGHT while one is, BOSS while the
    /// boss's room's fight is, SILENT from her death or the act's end until a new run, NONE with the switch off (or before it
    /// first started). Kristian, 2026-11-24, of his own loops: "Må høre det i spillet først".
    /// </summary>
    public enum MusicState { None, Calm, Fight, Boss, Silent }

    /// <summary>
    /// The numbers of the music (work order 116, 2026-11-24), all first guesses with their defaults here in code: the producer's
    /// start, for Kristian to tune by hearing. Read live by <c>MusicPlayer</c>; the session's one instance is
    /// <c>MusicPlayer.Values</c>, and the pause menu's two lines change <see cref="On"/> and <see cref="VolumeStep"/> in it (kept
    /// for the session, not saved).
    /// </summary>
    [Serializable]
    public sealed class MusicValues
    {
        /// <summary>
        /// The order's switch (work order 116, ON). Kristian sent his loops and, asked what was wrong with them, answered "Må høre
        /// det i spillet først": he wants to hear them in the game before he judges them. Off: no music, no audio source made, no
        /// clip loaded, as before the order. The pause menu's "Music" line turns it.
        /// </summary>
        public bool On = true;

        /// <summary>Beats a minute of the music's one clock: both his loops are at 128.0 BPM (measured by the producer).</summary>
        public double Bpm = 128.0;
        /// <summary>Beats a bar: four, so a bar is 1.875 s at 128 BPM.</summary>
        public int BeatsABar = 4;

        /// <summary>
        /// s: the least time between asking for a change and the bar line it lands on. A change asked for closer to the next line
        /// than this waits for the line after: the audio must be told before the line, a frame and a mixing buffer or two ahead
        /// (about 16 ms and 21 to 23 ms). Also the time from the music's very first ask to its first line (its start).
        /// </summary>
        public double LeastLead = 0.05;

        /// <summary>Beats over which the calm loop fades out from the line the fight loop starts on (the producer's start: gone within one beat).</summary>
        public double OutToFightBeats = 1.0;
        /// <summary>Bars over which the fight loop fades out from the line the calm loop comes in on, under it (the producer's start: two bars, 3.75 s).</summary>
        public double OutToCalmBars = 2.0;

        /// <summary>The calm loop's level, a share of her basic shot's volume as every placeholder sound is counted: under her guns always.</summary>
        public float CalmLevel = 0.22f;
        /// <summary>The fight loop's level, the same way (the producer's start: 0.38).</summary>
        public float FightLevel = 0.38f;
        /// <summary>The boss's loop's level (a clip named Boss, else the fight loop at this level): the fight's until he hears one.</summary>
        public float BossLevel = 0.38f;
        /// <summary>The short sound when a room is cleared (a clip named RoomCleared, if there is one): the fight's level.</summary>
        public float ClearedLevel = 0.38f;

        /// <summary>The share of its level the music keeps while the pause menu or a choice screen is up: it plays on, this much quieter (0.5 = half).</summary>
        public float HeldShare = 0.5f;
        /// <summary>Real s over which it goes to and from <see cref="HeldShare"/> as a screen opens or closes: no step in the level.</summary>
        public float HeldGlide = 0.25f;

        /// <summary>
        /// s over which the music fades out at her death and at the act's end: the time the game takes to slow to a stop before the
        /// end screen shows (HudRules.EndSlowSeconds, 0.5 s), so that the end screen is silent.
        /// </summary>
        public double EndFade = HudRules.EndSlowSeconds;

        /// <summary>
        /// The master music volume's step, 0 to <see cref="SettingsRules.VolumeSteps"/> (tenths: 0 to 100 %), over every level here;
        /// the pause menu's "Music volume" line steps it. 100 % to start.
        /// </summary>
        public int VolumeStep = SettingsRules.DefaultVolumeStep;

        /// <summary>The master music volume now, 0 to 1.</summary>
        public float Volume => SettingsRules.Volume(VolumeStep);

        /// <summary>The level a state's loop plays at (0 for none and silence).</summary>
        public float Level(MusicState state)
        {
            switch (state)
            {
                case MusicState.Calm: return CalmLevel;
                case MusicState.Fight: return FightLevel;
                case MusicState.Boss: return BossLevel;
                default: return 0f;
            }
        }
    }

    /// <summary>Where the music's clips are found (work order 116): Resources/Music/&lt;name&gt;. A clip that is not there is silence for its state.</summary>
    public static class MusicClips
    {
        public const string Folder = "Music";
        /// <summary>The calm loop: his "Explore" (15.000 s, 8 bars at 128 BPM, made from his two exploring takes).</summary>
        public const string Calm = "Explore";
        /// <summary>The fight loop: his "Fight" (30.000 s, 16 bars at 128 BPM, a hit on its first sample).</summary>
        public const string Fight = "Fight";
        /// <summary>The boss's loop, if he makes one; none yet, so the boss's room plays the fight loop.</summary>
        public const string Boss = "Boss";
        /// <summary>A short sound for a room cleared, if he makes one; none yet, so nothing.</summary>
        public const string Cleared = "RoomCleared";

        public static string PathOf(string name) => Folder + "/" + name;
    }

    /// <summary>
    /// The music's one clock (work order 116): <see cref="MusicValues.Bpm"/> beats a minute, <see cref="MusicValues.BeatsABar"/> a
    /// bar, counted on the audio clock in whole output samples from the music's start (its origin, a whole sample). Every bar
    /// line is worked out from the origin and its number alone, rounded once to the nearest sample, never summed: so nothing
    /// drifts however long it plays. At 48 kHz a bar is exactly 90 000 samples; at 44.1 kHz it is 82 687.5, so every other line
    /// falls half a sample from a whole one and is rounded to the nearest (11 µs early or late, the same every time).
    /// </summary>
    public static class MusicClock
    {
        public static double BeatSeconds(MusicValues v) => 60.0 / v.Bpm;
        public static double BarSeconds(MusicValues v) => BeatSeconds(v) * v.BeatsABar;

        /// <summary>Output samples from the origin to bar line <paramref name="bar"/>, rounded once to the nearest whole sample.</summary>
        public static long BarsInSamples(MusicValues v, long bar, int rate) =>
            (long)Math.Round(bar * (double)v.BeatsABar * 60.0 * rate / v.Bpm, MidpointRounding.AwayFromZero);

        /// <summary>The output sample bar line <paramref name="bar"/> falls on.</summary>
        public static long LineSample(MusicValues v, long origin, long bar, int rate) => origin + BarsInSamples(v, bar, rate);

        /// <summary>The audio clock's time (s) of bar line <paramref name="bar"/>.</summary>
        public static double LineTime(MusicValues v, long origin, long bar, int rate) => LineSample(v, origin, bar, rate) / (double)rate;

        /// <summary>The music's origin for a start at <paramref name="time"/>: the first whole output sample at or after it.</summary>
        public static long OriginAt(double time, int rate) => (long)Math.Ceiling(time * rate - 1e-6);

        /// <summary>
        /// Samples within which a time is taken to be on a line: a time worked out from a line's sample (its sample over the rate)
        /// can come back a hair under it in doubles, and is still that line's.
        /// </summary>
        const double SampleSlack = 1e-6;

        /// <summary>The bar playing at <paramref name="time"/>: the number of the last line at or before it (negative before the origin).</summary>
        public static long BarAt(MusicValues v, long origin, double time, int rate)
        {
            double at = time * rate + SampleSlack;
            long k = (long)Math.Floor((time - origin / (double)rate) / BarSeconds(v));
            while (LineSample(v, origin, k + 1, rate) <= at) k++;
            while (LineSample(v, origin, k, rate) > at) k--;
            return k;
        }

        /// <summary>The first bar line at or after <paramref name="time"/>, by its number.</summary>
        public static long NextBar(MusicValues v, long origin, double time, int rate)
        {
            long k = BarAt(v, origin, time, rate);
            return LineSample(v, origin, k, rate) >= time * rate - SampleSlack ? k : k + 1;
        }

        /// <summary>How many bars a clip of <paramref name="samples"/> samples at <paramref name="rate"/> holds (not rounded).</summary>
        public static double BarsOf(MusicValues v, long samples, int rate) => samples * v.Bpm / (60.0 * v.BeatsABar * rate);

        /// <summary>
        /// True when a clip is a whole number of bars to the sample: its samples are exactly those of a whole number of bars at its
        /// own rate (his Explore: 661 500 samples at 44.1 kHz, 8 bars; his Fight: 1 323 000, 16 bars).
        /// </summary>
        public static bool IsWholeBars(MusicValues v, long samples, int rate, out long bars)
        {
            bars = (long)Math.Round(BarsOf(v, samples, rate));
            if (bars <= 0) return false;
            double exact = bars * (double)v.BeatsABar * 60.0 * rate / v.Bpm;
            return Math.Abs(exact - samples) < 1e-6;
        }
    }

    /// <summary>The music's fades (work order 116): what share of its level a voice has at a moment, and how long a change's fade is.</summary>
    public static class MusicFade
    {
        /// <summary>
        /// The share of its level a voice fading out from <paramref name="start"/> over <paramref name="length"/> s has at
        /// <paramref name="now"/>: 1 until the start, then down an equal-power curve (cos of a quarter turn: at the half-way point
        /// 0.71, so the old and the new together hold the loudness through a crossing), 0 at its end and after. With no fade, 1 up
        /// to the start and 0 from it.
        /// </summary>
        public static float Gain(double now, double start, double length)
        {
            if (now < start) return 1f;
            if (length <= 0.0 || now >= start + length) return 0f;
            double x = (now - start) / length;
            return (float)Math.Cos(x * Math.PI * 0.5);
        }

        /// <summary>
        /// s over which the loop that played fades out when the music changes to <paramref name="to"/>: to calm the fight loop goes
        /// over <see cref="MusicValues.OutToCalmBars"/> bars; to a fight (or the boss) the calm loop over
        /// <see cref="MusicValues.OutToFightBeats"/> beats; to silence over <see cref="MusicValues.EndFade"/>; off at once.
        /// </summary>
        public static double OutSeconds(MusicValues v, MusicState to)
        {
            switch (to)
            {
                case MusicState.Calm: return Math.Max(0.0, v.OutToCalmBars) * MusicClock.BarSeconds(v);
                case MusicState.Fight:
                case MusicState.Boss: return Math.Max(0.0, v.OutToFightBeats) * MusicClock.BeatSeconds(v);
                case MusicState.Silent: return Math.Max(0.0, v.EndFade);
                default: return 0.0;
            }
        }

        /// <summary>A share moved toward <paramref name="target"/> by <paramref name="seconds"/> of a glide that crosses the whole of 0 to 1 in <paramref name="glide"/> s.</summary>
        public static float Toward(float share, float target, float seconds, float glide)
        {
            if (glide <= 0f || seconds < 0f) return target;
            float step = seconds / glide;
            if (share < target) return Math.Min(target, share + step);
            if (share > target) return Math.Max(target, share - step);
            return share;
        }
    }

    /// <summary>What the music should play now, from what the game already knows (work order 116).</summary>
    public static class MusicRules
    {
        /// <summary>
        /// The state the music should be in: none with the switch off; silent from her death or the run's end until the end screen
        /// is answered; calm under the screen between runs; the boss's while the boss's room's fight is on; the fight's while any
        /// room's fight is on (whatever the room, the room lab's too); else calm (the practice room, a floor between fights).
        /// </summary>
        public static MusicState Target(bool on, bool ended, bool herDead, bool betweenRuns, bool bossFight, bool anyFight)
        {
            if (!on) return MusicState.None;
            if (ended || herDead) return MusicState.Silent;
            if (betweenRuns) return MusicState.Calm;
            if (bossFight) return MusicState.Boss;
            if (anyFight) return MusicState.Fight;
            return MusicState.Calm;
        }

        /// <summary>The recorder's word for a state.</summary>
        public static string Word(MusicState state)
        {
            switch (state)
            {
                case MusicState.Calm: return "calm";
                case MusicState.Fight: return "fight";
                case MusicState.Boss: return "boss";
                case MusicState.Silent: return "silent";
                default: return "none";
            }
        }

        /// <summary>The state a recorder's word names (<see cref="MusicState.None"/> for anything else).</summary>
        public static MusicState FromWord(string word)
        {
            switch (word)
            {
                case "calm": return MusicState.Calm;
                case "fight": return MusicState.Fight;
                case "boss": return MusicState.Boss;
                case "silent": return MusicState.Silent;
                default: return MusicState.None;
            }
        }

        /// <summary>A state that is a loop on the bar (calm, fight, the boss's), not silence or none.</summary>
        public static bool IsLoop(MusicState state) => state == MusicState.Calm || state == MusicState.Fight || state == MusicState.Boss;
    }

    /// <summary>What one step of the <see cref="MusicConductor"/> changed: at most a landing, a cancel and a new change, in that order.</summary>
    public struct MusicChange
    {
        /// <summary>A change landed: on its bar line (a loop's), or at once (silence, the switch off).</summary>
        public bool Landed;
        public MusicState From, To;
        public long LandedBar;
        /// <summary>The landing was at once, not on a line: to silence or none.</summary>
        public bool AtOnce;
        /// <summary>A change waiting for its line was cancelled: the state flipped back, or to another, before the line.</summary>
        public bool Cancelled;
        public MusicState CancelledState;
        public long CancelledBar;
        /// <summary>A change was asked for: <see cref="ScheduledState"/>'s loop from its first sample at bar line <see cref="ScheduledBar"/>.</summary>
        public bool Scheduled;
        public MusicState ScheduledState;
        public long ScheduledBar;
    }

    /// <summary>
    /// The music's changes on the bar (work order 116), with no engine in it: told the audio clock's time and the state the game
    /// wants each frame (<see cref="Step"/>), it says when a change is to land. A loop's change lands on the NEXT bar line at
    /// least <see cref="MusicValues.LeastLead"/> away; the very first start makes the clock's origin that far ahead. A change still
    /// waiting is cancelled if the wanted state flips back (or to another) before its line, unless the line is closer than the
    /// least lead: then it is taken as told, lands, and the flip back is a change of its own at the line after. Silence and the
    /// switch off land at once. It allocates nothing.
    /// </summary>
    public sealed class MusicConductor
    {
        public MusicValues Values;

        public MusicConductor(MusicValues values) => Values = values ?? new MusicValues();

        /// <summary>The clock has started: it has its origin and its rate.</summary>
        public bool Started { get; private set; }
        /// <summary>The output sample bar 0 falls on.</summary>
        public long Origin { get; private set; }
        /// <summary>The output sample rate the clock counts in (taken at its start).</summary>
        public int Rate { get; private set; }
        /// <summary>The state that is heard now (the last that landed).</summary>
        public MusicState Playing { get; private set; } = MusicState.None;
        /// <summary>A change waits for its bar line.</summary>
        public bool HasPending { get; private set; }
        public MusicState Pending { get; private set; }
        public long PendingBar { get; private set; }

        /// <summary>The audio clock's time of a bar line (0 before the start).</summary>
        public double LineTime(long bar) => Started ? MusicClock.LineTime(Values, Origin, bar, Rate) : 0.0;

        /// <summary>The bar playing at a time (0 before the start).</summary>
        public long BarAt(double time) => Started ? MusicClock.BarAt(Values, Origin, time, Rate) : 0;

        /// <summary>The line the waiting change lands on.</summary>
        public double PendingLine => HasPending ? LineTime(PendingBar) : double.NaN;

        /// <summary>
        /// One look: <paramref name="now"/> on the audio clock (s), the state the game wants, and the output rate (used only to start
        /// the clock). A change that landed is all a step tells; the next step takes up what is wanted then.
        /// </summary>
        public MusicChange Step(double now, MusicState target, int rate)
        {
            var c = new MusicChange();
            double lead = Math.Max(0.0, Values.LeastLead);
            if (HasPending && now >= LineTime(PendingBar))
            {
                c.Landed = true;
                c.From = Playing;
                c.To = Pending;
                c.LandedBar = PendingBar;
                Playing = Pending;
                HasPending = false;
                return c;
            }
            if (HasPending)
            {
                bool committed = now >= LineTime(PendingBar) - lead;
                if (Pending == target || committed) return c; // as asked, or too close to its line to take back
                c.Cancelled = true;
                c.CancelledState = Pending;
                c.CancelledBar = PendingBar;
                HasPending = false;
            }
            if (target == Playing) return c;
            if (!MusicRules.IsLoop(target))
            {
                c.Landed = true;
                c.AtOnce = true;
                c.From = Playing;
                c.To = target;
                c.LandedBar = BarAt(now);
                Playing = target;
                return c;
            }
            long bar;
            if (!Started)
            {
                Started = true;
                Rate = Math.Max(1, rate);
                Origin = MusicClock.OriginAt(now + lead, Rate);
                bar = 0;
            }
            else bar = MusicClock.NextBar(Values, Origin, now + lead, Rate);
            HasPending = true;
            Pending = target;
            PendingBar = bar;
            c.Scheduled = true;
            c.ScheduledState = target;
            c.ScheduledBar = bar;
            return c;
        }
    }

    /// <summary>The recorder's line of a change of the music's state (work order 116): from, to, and the bar it landed on.</summary>
    public static class MusicEvents
    {
        /// <summary>Its name is the state it changed to; extras "from", "to", "bar" (the bar's number on the music's clock, from 0).</summary>
        public const string Change = "music";
    }
}
