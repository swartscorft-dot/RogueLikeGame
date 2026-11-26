using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where the timing of a clean press is shown besides the combo bar at the bottom of the screen (work order 134; a friend
    /// of Kristian's who played the shared build, 2026-11-25: "Jeg skulle gjerne hatt den visuelle hjelpen på når jeg skal trykke
    /// på siktet mitt. Så slipper jeg å flytte blikket fra det jeg skyter på til den baren i bunn av skjermen"; Kristian: "Vi bør
    /// prøve noe kreativt her, noe timing basert på våpen mekanikker eller noe"). Three cues to compare, one at a time, and the
    /// bar alone as today. The bar and the skill box's light stay as they are with every cue.
    /// </summary>
    public enum TimingCue
    {
        /// <summary>Today: the combo bar and the skill box only (the default until he has chosen).</summary>
        BarOnly = 0,
        /// <summary>A revolver's cylinder round the sight: the next chamber turns into a fixed notch at the top exactly while the press is clean.</summary>
        Cylinder = 1,
        /// <summary>The friend's own example: a ring closing in on the sight, a band round it marking the clean part.</summary>
        Ring = 2,
        /// <summary>Nothing at the sight: her pistols' muzzles light up step by step, brightest while the press is clean.</summary>
        Hammer = 3,
    }

    /// <summary>
    /// The order's switch and the cues' numbers (work order 134), every one a first guess for him to tune by playing. Sizes at the
    /// sight in layout pixels of a screen 1080 high (scaled with the screen's height); angles in degrees, clockwise from the top;
    /// times in seconds of the chain's own clock unless a line says real time.
    /// </summary>
    [Serializable]
    public sealed class TimingAtTheSightValues
    {
        /// <summary>
        /// The order's switch (work order 134, ON with his words "Vi bør prøve noe kreativt her, noe timing basert på våpen
        /// mekanikker": the cues exist and can be chosen). Off, the setting is ignored: nothing new is drawn or sounded, the
        /// clean window's old tick is as it was, and the temporary key does nothing.
        /// </summary>
        public static bool On = true;

        /// <summary>The numbers every cue reads (the running cue shows them in the Inspector).</summary>
        public static TimingAtTheSightValues Live = new TimingAtTheSightValues();

        // ---------------- The cylinder ----------------
        public float CylinderRadius = 34f;    // px: the middle line of the ring of chambers round the sight; its inside (about 26 px) is left open, the enemy under the cursor seen (30 in the first pictures: too small to read at a glance)
        public float CylinderStroke = 3f;     // px: the ring's own line (drawn on a dark line wider, so it reads on a lit floor and on a white dummy)
        public float ChamberRadius = 7.5f;    // px: a chamber
        public int LeastChambers = 3;         // chambers' spacing as if there were at least three (Backstep's one chamber turns as one of three)
        public float NotchMost = 24f;         // degrees: the notch's width at most (a chamber's middle inside it is in the notch)
        public float NotchLeast = 8f;         // degrees: and at least, for a chain of many presses
        public float NotchClear = 30f;        // degrees the chamber before stands clear of the notch's middle when the next one's turn begins
        public float IndexSeconds = 0.08f;    // s (game time): the cylinder's quick turn after a press, to the next chamber's place; done before its window opens
        public float LockSeconds = 0.06f;     // s (game time): a clean press holds its chamber still in the notch, lit, before the turn (the "short hard stop")
        public float LockKick = 2.5f;         // px: and the ring jolts outward by this much at the lock, back in that time
        public float SlipBack = 8f;           // degrees a missed chamber slips back as the cue goes (40 in the first pictures carried it back into the notch: read as clean)
        public float MissDrop = 0.15f;        // and the cylinder sinks in by this share of its radius as it fades
        public float PostLength = 12f;        // px: each of the notch's two posts, standing out from the ring
        public float PostWidth = 5f;          // px
        public float PostGap = 3f;            // px between the ring of chambers' outer edge and a post

        // ---------------- The ring ----------------
        public float BandInner = 26f;         // px: the band round the sight that marks the clean part, its inner edge (the inside left open)
        public float BandOuter = 40f;         // px: its outer edge; the closing ring is inside it exactly while a press is clean
        public float RingStroke = 3f;         // px: the closing ring's line (on a dark line 2 px wider)
        public float RingMost = 110f;         // px: farther out than this the ring is not drawn
        public float RingFadeIn = 20f;        // px inside RingMost over which it comes up from nothing
        public float RingLeast = 12f;         // px: after the band it closes on to here, no further

        // ---------------- The hammer: her muzzles' light ----------------
        public int HammerSteps = 3;           // steps the light comes up in before the clean part
        public float HammerStepLevel = 0.18f; // each step's share of the brightest (3 steps: 0.18, 0.36, 0.54), so the brightest is the clean part alone
        public float HammerLate = 0.15f;      // its share after the clean part, until the window runs out
        public float GlowSize = 0.6f;         // m across, a glow at the brightest, at each muzzle (about 43 px at our camera: 28 m, 30 degrees)
        public float GlowLeast = 0.55f;       // its size at the faintest step, as a share of GlowSize
        public float GlowPeak = 1.6f;         // how bright the glow is at the brightest (added light; the shots' own flash is about 4; 2.4 in the first pictures blew out to a white blob)
        public float LightPeak = 3f;          // a point light between her hands at the brightest, so the floor round her shows it too; 0: none
        public float LightRange = 2.6f;       // m

        // ---------------- The click ----------------
        public float ClickVolume = 0.12f;     // the click's own level (a voice's volume, not a share of the shots': work order 131 levels those), under the shots' 0.4 to 0.9 and the old tick's ~0.14
        public float ClickPitch = 1f;         // the first recast press's pitch
        public float ClickStep = 2f;          // semitones higher for each press after it in a chain (Triple Tap: press 2 at 1, press 3 a tone up)

        // ---------------- All of them ----------------
        public float FadeInSeconds = 0.06f;   // s (game time) the cue comes up in when a chain starts
        public float LingerSeconds = 0.3f;    // s (game time) it stays after the chain ends, showing how the last press fell, then it is gone
        public float FlashSeconds = 0.15f;    // s (game time) a clean press's flash
        public float NameSeconds = 1f;        // s (real) the cue's name shows at the sight after the temporary key
        public float NameBelow = 58f;         // px below the sight that the name stands
        public int NameSize = 22;             // px of a screen 1080 high
    }

    /// <summary>
    /// The setting (work order 134): <c>TimingCue</c>, one of <see cref="Names"/>, saved as the camera angle is, and
    /// <c>TimingSound</c> on or off, on by default with any cue but "Bar only". Stepping as the menu's other lines step. Plain data.
    /// </summary>
    public static class TimingCues
    {
        public const int Count = 4;
        /// <summary>The step a player gets who has none stored: today's, the bar only (the order: until he has chosen).</summary>
        public const TimingCue Default = TimingCue.BarOnly;
        /// <summary>The words of each on a menu's line and at the sight.</summary>
        public static readonly string[] Names = { "Bar only", "Cylinder", "Ring", "Hammer" };

        /// <summary>The sound's stored value: none stored (its default follows the cue), off, on.</summary>
        public const int SoundNotStored = -1, SoundOff = 0, SoundOn = 1;

        /// <summary>A stored value made a cue: anything that is not one is the default.</summary>
        public static TimingCue Step(int stored) => stored >= 0 && stored < Count ? (TimingCue)stored : Default;

        /// <summary>The cue after this one: Bar only, Cylinder, Ring, Hammer and back.</summary>
        public static TimingCue Next(TimingCue cue) => (TimingCue)(((int)Step((int)cue) + 1) % Count);

        /// <summary>The cue before this one.</summary>
        public static TimingCue Previous(TimingCue cue) => (TimingCue)(((int)Step((int)cue) + Count - 1) % Count);

        /// <summary>A stored sound value made one: anything else is none stored.</summary>
        public static int StoredSound(int stored) => stored == SoundOff || stored == SoundOn ? stored : SoundNotStored;

        /// <summary>The sound's default with this cue: on with any cue but the bar only.</summary>
        public static bool SoundByDefault(TimingCue cue) => Step((int)cue) != TimingCue.BarOnly;

        /// <summary>Is the click on: the stored value when there is one, else the cue's default.</summary>
        public static bool Sound(int stored, TimingCue cue) => StoredSound(stored) == SoundNotStored ? SoundByDefault(cue) : stored == SoundOn;

        /// <summary>The cue in use with the order's switch: off, the bar only whatever is stored.</summary>
        public static TimingCue InUse(TimingCue chosen) => TimingAtTheSightValues.On ? Step((int)chosen) : TimingCue.BarOnly;

        /// <summary>The click in use with the order's switch: off, none whatever is stored.</summary>
        public static bool SoundInUse(int stored, TimingCue chosen) => TimingAtTheSightValues.On && Sound(stored, Step((int)chosen));
    }

    /// <summary>
    /// The one clock every cue runs on (work order 134): the seconds since the press's window opened, exactly as the judge reads
    /// them (<c>PlayerCharacter.RecastSinceOpened</c>, the chain's own clock, the hit stop's seconds in it: work order 038), and
    /// before the window opens (the shot before it still in its startup) a time below 0 counted from that shot's press, so a cue
    /// can already be moving toward the clean part: never 0 or more until the window is open.
    /// </summary>
    public static class TimingCueClock
    {
        /// <summary>The most a time before the window opens may come up to: a hair under 0, never inside the window.</summary>
        public const float BeforeOpen = -1e-3f;

        /// <summary>
        /// <paramref name="sinceOpened"/>: the judge's seconds since the window opened (below 0 before it does);
        /// <paramref name="sincePress"/>: seconds since the press of the shot before it; <paramref name="lead"/>: that shot's startup.
        /// </summary>
        public static float Since(float sinceOpened, float sincePress, float lead)
        {
            if (sinceOpened >= 0f) return sinceOpened;
            float l = Math.Max(0f, lead);
            float p = Math.Max(0f, Math.Min(l, sincePress));
            return Math.Min(BeforeOpen, p - l);
        }

        /// <summary>The edge the judge allows at either end of a clean part (CleanPart's own: the clock's rounding).</summary>
        public const float Edge = 1e-5f;
    }

    /// <summary>
    /// The cylinder (work order 134, the producer's proposal for "våpen mekanikker"): a thin ring of chambers round the sight, a
    /// revolver's cylinder seen from behind, and a fixed notch at the top. The chamber of the press whose window is open or comes
    /// next turns toward the notch at one speed, and its middle is inside the notch exactly while that press is clean: the notch's
    /// width IS the clean part, and the speed comes from the window's own times (the notch's width over the clean part's
    /// length), so a longer wait is a longer turn. One chamber a press that fires a shot: Triple Tap's three (the first shot's
    /// already fired), Backstep's one (its hop fires nothing). Angles in degrees, clockwise from the notch.
    /// </summary>
    public static class CylinderCue
    {
        /// <summary>The chambers of a chain of <paramref name="count"/> presses: one a press that fires (an opening move fires nothing).</summary>
        public static int Chambers(int count, bool openingMove) => Math.Max(0, openingMove ? count - 1 : count);

        /// <summary>The chamber of press <paramref name="press"/> (1 on), or -1 for a press with none (an opening move).</summary>
        public static int ChamberOf(int press, bool openingMove)
        {
            int c = openingMove ? press - 2 : press - 1;
            return c >= 0 ? c : -1;
        }

        /// <summary>Degrees between two chambers: as if there were at least <see cref="TimingAtTheSightValues.LeastChambers"/>.</summary>
        public static float Spacing(int chambers, TimingAtTheSightValues v) => 360f / Math.Max(1, Math.Max(chambers, v.LeastChambers));

        /// <summary>
        /// How far ahead of its clean part a press's turn begins, in lengths of that clean part: from the press before (its lead,
        /// the shot's startup) to the clean part's middle. The notch is made narrow enough that the chamber before has turned clear.
        /// </summary>
        public static float Reach(CleanPart clean, float lead)
        {
            float width = clean.Width;
            if (width <= 0f) return 0f;
            return (Math.Max(0f, lead) + (clean.From + clean.To) * 0.5f) / width;
        }

        /// <summary>
        /// The notch's width for a chain whose presses reach at most <paramref name="mostReach"/> (<see cref="Reach"/>): as wide as
        /// <see cref="TimingAtTheSightValues.NotchMost"/>, narrower where a turn must begin farther back, so that when a chamber's turn
        /// begins the chamber before stands <see cref="TimingAtTheSightValues.NotchClear"/> clear of the notch; never under
        /// <see cref="TimingAtTheSightValues.NotchLeast"/>. Fixed for the chain: the speed of each turn follows from it.
        /// </summary>
        public static float Notch(float spacing, float mostReach, TimingAtTheSightValues v)
        {
            float room = Math.Max(0f, spacing - v.NotchClear) / (Math.Max(0f, mostReach) + 0.5f);
            return Math.Max(v.NotchLeast, Math.Min(v.NotchMost, room));
        }

        /// <summary>Degrees a second the cylinder turns at for this clean part: the notch's width over the clean part's length (0 with none).</summary>
        public static float Speed(CleanPart clean, float notch) => clean.Width > 0f ? notch / clean.Width : 0f;

        /// <summary>
        /// Where the chamber of the press waited for stands, <paramref name="since"/> seconds after its window opened (below 0: before,
        /// <see cref="TimingCueClock"/>): at the notch's middle at the clean part's middle, turning at <see cref="Speed"/>.
        /// </summary>
        public static float LiveAngle(float since, CleanPart clean, float notch) =>
            clean.Width > 0f ? (since - (clean.From + clean.To) * 0.5f) * Speed(clean, notch) : -notch;

        /// <summary>The cylinder's turn: the first chamber's angle, when chamber <paramref name="live"/> stands at <paramref name="liveAngle"/> (chambers follow each other a spacing apart, the later ones behind).</summary>
        public static float Turn(float liveAngle, int live, float spacing) => liveAngle + Math.Max(0, live) * spacing;

        /// <summary>Where chamber <paramref name="chamber"/> stands at the cylinder's turn <paramref name="turn"/>.</summary>
        public static float AngleOf(int chamber, float turn, float spacing) => turn - Math.Max(0, chamber) * spacing;

        /// <summary>
        /// Is a chamber at this angle in the notch: its middle inside the notch's width, with the judge's edge at either end turned
        /// into degrees at this speed (so it is in the notch exactly when the clean part holds the time).
        /// </summary>
        public static bool InNotch(float angle, float notch, float speed) => Math.Abs(angle) <= notch * 0.5f + TimingCueClock.Edge * Math.Max(0f, speed);

        /// <summary>What the cylinder says now: the window is open and its chamber is in the notch.</summary>
        public static bool SaysClean(float sinceOpened, float window, CleanPart clean, float notch) =>
            sinceOpened >= 0f && sinceOpened <= window + TimingCueClock.Edge && clean.Any && InNotch(LiveAngle(sinceOpened, clean, notch), notch, Speed(clean, notch));
    }

    /// <summary>
    /// The closing ring (work order 134, the friend's own example: "siktet har en sirkel som snevres inn og må treffe innenfor ett
    /// felt"): a band round the sight marks the clean part; a ring closes in on the sight at one speed and is inside the band
    /// exactly while the press is clean (the band's width over the clean part's length is its speed). One ring a press; for a
    /// chain the next ring begins as the last one is pressed. Radii in layout pixels.
    /// </summary>
    public static class RingCue
    {
        /// <summary>Pixels a second the ring closes at for this clean part (0 with none).</summary>
        public static float Speed(CleanPart clean, TimingAtTheSightValues v) =>
            clean.Width > 0f ? Math.Max(0f, v.BandOuter - v.BandInner) / clean.Width : 0f;

        /// <summary>
        /// The ring's radius <paramref name="since"/> seconds after its window opened (below 0: before): at the band's outer edge
        /// as the clean part opens, its inner edge as it ends, on inward after it to <see cref="TimingAtTheSightValues.RingLeast"/>.
        /// </summary>
        public static float Radius(float since, CleanPart clean, TimingAtTheSightValues v)
        {
            if (clean.Width <= 0f) return v.RingMost;
            float r = v.BandOuter + (clean.From - since) * Speed(clean, v);
            return Math.Max(Math.Min(v.RingLeast, v.BandInner), r);
        }

        /// <summary>Is a ring of this radius inside the band (the judge's edge at either end turned into pixels at this speed).</summary>
        public static bool InBand(float radius, float speed, TimingAtTheSightValues v)
        {
            float edge = TimingCueClock.Edge * Math.Max(0f, speed);
            return radius <= v.BandOuter + edge && radius >= v.BandInner - edge;
        }

        /// <summary>How much of the ring shows at this radius: none past <see cref="TimingAtTheSightValues.RingMost"/>, coming up over <see cref="TimingAtTheSightValues.RingFadeIn"/>.</summary>
        public static float Shows(float radius, TimingAtTheSightValues v)
        {
            if (radius >= v.RingMost) return 0f;
            return v.RingFadeIn <= 0f ? 1f : Math.Max(0f, Math.Min(1f, (v.RingMost - radius) / v.RingFadeIn));
        }

        /// <summary>What the ring says now: the window is open and the ring is inside the band.</summary>
        public static bool SaysClean(float sinceOpened, float window, CleanPart clean, TimingAtTheSightValues v) =>
            sinceOpened >= 0f && sinceOpened <= window + TimingCueClock.Edge && clean.Any && InBand(Radius(sinceOpened, clean, v), Speed(clean, v), v);
    }

    /// <summary>
    /// The hammer (work order 134: nothing at the sight; the corner of the eye and the ear). Her pistols are one mesh each, with no
    /// hammer or cylinder that can be turned, so it is the muzzles' light: dark until the window opens, then up in
    /// <see cref="TimingAtTheSightValues.HammerSteps"/> steps toward the clean part, at its brightest exactly while the press is
    /// clean, faint after it until the window runs out. A share of the brightest, 0 to 1.
    /// </summary>
    public static class HammerCue
    {
        /// <summary>The share of the brightest that is the brightest: the clean part alone has it.</summary>
        public const float Brightest = 1f;

        /// <summary>The light <paramref name="since"/> seconds after the window opened (below 0: before), in a window this long with this clean part.</summary>
        public static float Level(float since, float window, CleanPart clean, TimingAtTheSightValues v)
        {
            if (since < 0f || since > window + TimingCueClock.Edge || !clean.Any) return 0f;
            if (clean.Contains(since)) return Brightest;
            if (since > clean.To) return Math.Max(0f, Math.Min(v.HammerStepLevel, v.HammerLate));
            int steps = Math.Max(1, v.HammerSteps);
            int step = clean.From > 0f ? Math.Min(steps - 1, (int)(since / clean.From * steps)) : steps - 1;
            return Math.Max(0f, Math.Min(0.95f, (step + 1) * v.HammerStepLevel));
        }

        /// <summary>What the hammer says now: its light at the brightest.</summary>
        public static bool SaysClean(float sinceOpened, float window, CleanPart clean, TimingAtTheSightValues v) =>
            Level(sinceOpened, window, clean, v) >= Brightest;
    }

    /// <summary>
    /// The click (work order 134): a short mechanical click as the clean part OPENS, a hammer being cocked, made in code
    /// (<see cref="Synth"/>), quiet under the shots; across a chain its pitch steps up press by press. A clean press has its own
    /// sound already (the clean tick, work order 064): the click is never played for it. Where it is on, the clean window's old
    /// tick (work order 037, the same moment) stands aside: one sound at that moment, not two.
    /// </summary>
    public static class TimingClick
    {
        /// <summary>A hammer cocked: a bright first catch, a lower, softer second a moment later, both gone in hundredths.</summary>
        public static SynthRecipe Recipe() => new SynthRecipe
        {
            Seconds = 0.07f, Drive = 1.2f, Peak = 0.9f, Seed = 134,
            Layers = new[]
            {
                // The first catch: a band of noise high up and a short metallic ping.
                new SynthLayer { Wave = SynthWave.Noise, Start = 0f, Length = 0.02f, Gain = 1f, Attack = 0.0003f, Decay = 0.0025f, Release = 0.004f, HighPass = 2500f, LowPass = 9000f },
                new SynthLayer { Wave = SynthWave.Sine, From = 3100f, To = 2800f, Start = 0f, Length = 0.025f, Gain = 0.35f, Attack = 0.0003f, Decay = 0.005f, Release = 0.004f },
                // The second, the cylinder settling: lower and softer, 28 ms on.
                new SynthLayer { Wave = SynthWave.Noise, Start = 0.028f, Length = 0.02f, Gain = 0.55f, Attack = 0.0003f, Decay = 0.003f, Release = 0.004f, HighPass = 1200f, LowPass = 5000f },
                new SynthLayer { Wave = SynthWave.Sine, From = 1900f, To = 1700f, Start = 0.028f, Length = 0.025f, Gain = 0.25f, Attack = 0.0003f, Decay = 0.006f, Release = 0.004f },
            },
        };

        /// <summary>The click's pitch for press <paramref name="press"/> (2 the first recast press) of a chain: a step up for each after the first.</summary>
        public static float Pitch(int press, TimingAtTheSightValues v)
        {
            int k = Math.Max(0, press - 2);
            return Math.Max(0.1f, v.ClickPitch) * (float)Math.Pow(2.0, k * v.ClickStep / 12.0);
        }
    }
}
