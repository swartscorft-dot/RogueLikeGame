using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How work order 143's cone is shown (the order: "the cone is always SHOWN at the cursor ... without it the player blames the
    /// game"), in the interface's 1080-high layout pixels. Until the reticle he picked (round 1's sheet 2, "the arm along the ring")
    /// is built by work order 144, two plain brackets either side of the cursor, perpendicular to the aim, whose inner edges stand
    /// where the cone's real edges are at the cursor's distance on the ground (work order 142's rule: a drawn edge is the judged
    /// edge, line widths counted: each bracket's dark outline lies only on its outer side). Nothing snaps to an enemy. In the
    /// practice room only, the cone on the ground and a small speed bar under her with the accurate share's line. First guesses.
    /// </summary>
    [Serializable]
    public sealed class PrecisionSightValues
    {
        /// <summary>A bracket's length along the aim, and its width, in layout px.</summary>
        public float BracketLength = 14f, BracketWidth = 2f;
        /// <summary>px the dark outline adds on a bracket's outer side and at its two ends.</summary>
        public float DarkMore = 2f;
        public float DarkAlpha = 0.75f;
        /// <summary>The brackets' opacity.</summary>
        public float Alpha = 0.9f;
        /// <summary>The cone on the ground (practice room only): its lines' width in px and opacity.</summary>
        public float GroundLineWidth = 1.5f, GroundAlpha = 0.35f;
        /// <summary>The speed bar (practice room only): its size, how far under her feet it stands, the share its right end stands for.</summary>
        public float BarWidth = 56f, BarHeight = 5f, BarBelow = 26f, BarMostShare = 1.3f;
        /// <summary>The sounds: the soft tick as she crosses into accuracy, and a cone's miss (a bullet's whizz past, not a wall's spark).</summary>
        public float TickVolume = 0.25f, WhizzVolume = 0.4f;
        /// <summary>s: no tick again sooner than this after one (she stops and starts in a jiggle).</summary>
        public float TickGap = 0.25f;
    }

    /// <summary>The cone at the cursor, as measured once a frame: the one entry the reticle's drawing reads (work order 144 takes it over).</summary>
    public struct ConeAtTheCursor
    {
        /// <summary>True when it is to be shown.</summary>
        public bool Shown;
        /// <summary>The cone's full width in degrees (after her Precision's cut).</summary>
        public float Degrees;
        /// <summary>Metres from her to the cursor on the ground.</summary>
        public float Distance;
        /// <summary>The cone's real width at that distance, in metres (2 × d × tan(half)).</summary>
        public float Metres;
        /// <summary>That width on the screen, in screen pixels (not layout pixels): the gap between the brackets' inner edges.</summary>
        public float Pixels;
        /// <summary>The cursor on the screen (pixels from the top left), the aim's way on the screen and across it (unit vectors, y down).</summary>
        public float SightX, SightY, AlongX, AlongY, AcrossX, AcrossY;
        /// <summary>Her feet on the screen, and the cone's two edges at the cursor's distance on the ground (practice room's cone).</summary>
        public float FeetX, FeetY, LeftX, LeftY, RightX, RightY;
        /// <summary>The share of run speed the accuracy reads, and the accurate share (the speed bar).</summary>
        public float Share, AccurateShare;
    }

    /// <summary>The order's two sounds, made in code (no sound file).</summary>
    public static class PrecisionSounds
    {
        /// <summary>The soft settle tick: a short, quiet, dry click a little lower than the timing click.</summary>
        public static SynthRecipe Tick() => new SynthRecipe
        {
            Seconds = 0.04f, Drive = 0f, Peak = 0.6f, Seed = 143,
            Layers = new[]
            {
                new SynthLayer { Wave = SynthWave.Noise, Start = 0f, Length = 0.012f, Gain = 0.8f, Attack = 0.0003f, Decay = 0.002f, Release = 0.003f, HighPass = 1800f, LowPass = 6000f },
                new SynthLayer { Wave = SynthWave.Sine, From = 1400f, To = 1250f, Start = 0f, Length = 0.02f, Gain = 0.3f, Attack = 0.0003f, Decay = 0.004f, Release = 0.004f },
            },
        };

        /// <summary>The cone's miss: a thin whizz falling in pitch, as a bullet passing by (a wall's hit is a spark and a thud).</summary>
        public static SynthRecipe Whizz() => new SynthRecipe
        {
            Seconds = 0.16f, Drive = 0f, Peak = 0.7f, Seed = 1431,
            Layers = new[]
            {
                new SynthLayer { Wave = SynthWave.Noise, Start = 0f, Length = 0.15f, Gain = 0.9f, Attack = 0.02f, Decay = 0.06f, Release = 0.04f, HighPass = 3500f, HighPassTo = 1200f, LowPass = 9000f, LowPassTo = 3000f },
                new SynthLayer { Wave = SynthWave.Sine, From = 2600f, To = 1500f, Start = 0.01f, Length = 0.12f, Gain = 0.15f, Attack = 0.02f, Decay = 0.05f, Release = 0.03f },
            },
        };
    }
}
