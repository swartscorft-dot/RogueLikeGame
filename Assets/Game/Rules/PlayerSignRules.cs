using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of her two signs (work order 037, Kristian's session of 2026-11-20), all first guesses, with their
    /// defaults here in code: no asset holds them. In that session the last press of Triple Tap was clean 1 time of 79 and
    /// Backstep's 0 of 4 ("Jeg visste ikke at det fantes": he did not know a clean press existed), and the focus gauge
    /// stood full for 79 s on average before he spent it; asked, he chose a clear sign when it is full. The signs only
    /// show what already happens: nothing here changes when a press is clean, what it gives, how focus fills or which key
    /// spends it. Sizes in metres, times in seconds, on game time unless a line says otherwise.
    /// </summary>
    [Serializable]
    public sealed class PlayerSignValues
    {
        /// <summary>
        /// The ring on the floor round her and its once-a-session line. Off by default since work order 040 (Kristian's
        /// second session of 2026-11-20: he looks at the cast bar instead, <see cref="CastBarValues.On"/>).
        /// </summary>
        public bool CleanWindow = false;
        /// <summary>
        /// The key's strip on the skill bar and the tick when the clean part opens (work order 037; since work order 040 a
        /// switch of their own, for they stay when the ring is off). Off with <see cref="CleanWindow"/> off too: no ring, no
        /// mark on the key, no tick and no line: the clean press as it was before work order 037.
        /// </summary>
        public bool KeyStrip = true;
        /// <summary>Off: no flash, word, pulse or key beside the gauge when focus is full: the gauge as it was before.</summary>
        public bool FocusFull = true;

        // 1. The clean window: a thin ring on the floor round her closing toward a fixed band.
        public float BandInner = 1.0f;       // m: the band's inner edge, round her feet
        public float BandOuter = 1.35f;      // m: its outer edge. The ring's outer edge is inside the band exactly while a press is clean
        public float MostRadius = 3f;        // m: the ring never starts wider than this (a clean share near 0)
        public float LineSeconds = 4f;       // s of real time the once-a-session line shows

        // 2. Focus that is full.
        public float PulseSeconds = 1.5f;    // s: one calm pulse round her this often, as long as it stays full
        public float PulseDelay = 0.4f;      // s after it filled that the first pulse starts: the flash has the moment to itself
        public float PulseShowShare = 0.7f;  // of each pulse, the share in which the ring grows; the rest it is not drawn
        public float PulseFrom = 0.7f;       // m: a pulse's ring starts this wide...
        public float PulseTo = 1.6f;         // m: ...and grows to this
        public float FlashRing = 1.8f;       // m: the ring of the flash the moment it fills
        public float FlashRingSeconds = 0.35f;
        public float FlashStar = 1.1f;       // m: the star of that flash, on her chest
        public float FlashStarSeconds = 0.25f;
        public float WordSeconds = 1.2f;     // s the word FOCUS and its key show over her, rising and fading
        public float GaugeLow = 0.25f;       // the gauge's frame and key in the HUD pulse between this opacity...
        public float GaugeHigh = 0.85f;      // ...and this, once a pulse
    }

    /// <summary>
    /// Where the ring of the clean window is (work order 037). While a recast chain's next press can be clean (Triple Tap's
    /// last, Backstep's second; <see cref="RecastChain.HasCleanPress"/>), a thin ring lies on the floor round her and closes
    /// in a straight line toward a fixed band, on the chain's own clock: it stands at its start while the shot before has
    /// not gone off, reaches the band's outer edge exactly as the clean part of the window opens, and its inner edge
    /// exactly as the window runs out. So the ring (its outer edge) is inside the band exactly while a press is clean
    /// (<see cref="RecastChain.IsClean"/>), whatever the window and its clean share; primed (Hair Trigger, the whole window
    /// clean) it starts at the band's outer edge. The key on the skill bar shows the same in small: its mark is the share
    /// of the window left, its band the clean share at the end, so mark and ring agree.
    /// </summary>
    public static class CleanWindowSign
    {
        /// <summary>The line the HUD shows the first time in a session that a clean window opens.</summary>
        public const string Line = "Press again inside the band: a clean press";

        /// <summary>The smallest clean share the ring is drawn for: below it the ring would start far out of sight.</summary>
        const float LeastShare = 0.05f;

        /// <summary>Is there a ring at all: a chain runs, its next press can be clean, and its window has not run out.</summary>
        public static bool Shows(bool chainRuns, float cleanShare, float window, float sinceOpened) =>
            chainRuns && cleanShare > 0f && window > 0f && sinceOpened <= window;

        /// <summary>The ring's outer edge when the window opens (and before, while the shot before has not gone off).</summary>
        public static float StartRadius(float cleanShare, PlayerSignValues v)
        {
            float share = Math.Max(LeastShare, Math.Min(1f, cleanShare));
            float band = Math.Max(0f, v.BandOuter - v.BandInner);
            return Math.Min(Math.Max(v.BandOuter, v.MostRadius), v.BandInner + band / share);
        }

        /// <summary>
        /// The ring's outer edge <paramref name="sinceOpened"/> seconds after the window opened: its start before (a
        /// negative time); then to the band's outer edge as the clean part opens, and on to its inner edge at the window's
        /// end, and no further. With the start not held in by <see cref="PlayerSignValues.MostRadius"/> the two legs are
        /// one straight line at one speed.
        /// </summary>
        public static float RingRadius(float sinceOpened, float window, float cleanShare, PlayerSignValues v)
        {
            float start = StartRadius(cleanShare, v);
            if (sinceOpened <= 0f || window <= 0f) return start;
            float share = Math.Max(0f, Math.Min(1f, cleanShare));
            float cleanFrom = window * (1f - share);
            if (sinceOpened < cleanFrom) return start + (v.BandOuter - start) * (sinceOpened / cleanFrom);
            float clean = window - cleanFrom;
            float k = clean <= 1e-6f ? 1f : Math.Min(1f, (sinceOpened - cleanFrom) / clean);
            return v.BandOuter + (v.BandInner - v.BandOuter) * k;
        }

        /// <summary>Is the ring inside the band: exactly when a press now would be clean.</summary>
        public static bool InBand(float sinceOpened, float window, float cleanShare) =>
            sinceOpened >= 0f && cleanShare > 0f && RecastChain.IsClean(sinceOpened, window, cleanShare);

        // Work order 040 (Kristian's second session of 2026-11-20): the clean part may lie anywhere in the window
        // (CleanPart), not only at its end. The ring closes at one speed to the band's outer edge as the clean part opens,
        // to its inner edge as it ends, and on inward past the band at the same speed until the window runs out, so it is
        // inside the band exactly while a press is clean. For the last share the three below give what the three above do.

        /// <summary>The ring never closes tighter than this, in metres (work order 040: after an early clean part it closes on past the band).</summary>
        const float LeastRadius = 0.3f;

        /// <summary>Is there a ring at all, for a clean part placed anywhere: a chain runs, its next press can be clean, and its window has not run out.</summary>
        public static bool Shows(bool chainRuns, CleanPart clean, float window, float sinceOpened) =>
            chainRuns && clean.Any && window > 0f && sinceOpened <= window;

        /// <summary>The ring's outer edge when the window opens, for a clean part placed anywhere: as far out as it closes by the clean part's start.</summary>
        public static float StartRadius(CleanPart clean, PlayerSignValues v)
        {
            float band = Math.Max(0f, v.BandOuter - v.BandInner);
            float length = clean.To - clean.From;
            if (clean.From <= 0f || length <= 1e-6f) return v.BandOuter;
            return Math.Min(Math.Max(v.BandOuter, v.MostRadius), v.BandOuter + band / length * clean.From);
        }

        /// <summary>The ring's outer edge <paramref name="sinceOpened"/> seconds after the window opened, for a clean part placed anywhere.</summary>
        public static float RingRadius(float sinceOpened, CleanPart clean, PlayerSignValues v)
        {
            float start = StartRadius(clean, v);
            if (sinceOpened <= 0f) return start;
            if (sinceOpened < clean.From) return start + (v.BandOuter - start) * (sinceOpened / clean.From);
            float length = clean.To - clean.From;
            if (length <= 1e-6f) return v.BandInner;
            if (sinceOpened <= clean.To) return v.BandOuter + (v.BandInner - v.BandOuter) * ((sinceOpened - clean.From) / length);
            float speed = Math.Max(0f, v.BandOuter - v.BandInner) / length;
            return Math.Max(Math.Min(LeastRadius, v.BandInner), v.BandInner - speed * (sinceOpened - clean.To));
        }

        /// <summary>Is the ring inside the band, for a clean part placed anywhere: exactly when a press now would be clean.</summary>
        public static bool InBand(float sinceOpened, CleanPart clean) => sinceOpened >= 0f && clean.Contains(sinceOpened);

        /// <summary>
        /// The key's strip (work order 040): its band from this share of the strip, counted from its left end, where the
        /// mark (<see cref="MarkShare"/>) ends as the window runs out; and the band's width, the share of the window that is
        /// clean. For the last share, from 0 (the left end) and that share, as before.
        /// </summary>
        public static float KeyBandFrom(CleanPart clean, float window) =>
            window <= 0f || !clean.Any ? 0f : Math.Max(0f, Math.Min(1f, 1f - clean.To / window));

        public static float KeyBandWidth(CleanPart clean, float window) =>
            window <= 0f || !clean.Any ? 0f : Math.Max(0f, Math.Min(1f, (clean.To - clean.From) / window));

        /// <summary>The mark on the key, 1 to 0: the share of the window left (1 before it opens), where the ring is.</summary>
        public static float MarkShare(float sinceOpened, float window)
        {
            if (sinceOpened <= 0f || window <= 0f) return 1f;
            return Math.Max(0f, Math.Min(1f, 1f - sinceOpened / window));
        }

        /// <summary>Where a ring of this outer edge is on the key's strip, 1 at its start to 0 at the band's inner edge.</summary>
        public static float MarkOf(float radius, float cleanShare, PlayerSignValues v)
        {
            float start = StartRadius(cleanShare, v);
            float span = start - v.BandInner;
            return span <= 1e-6f ? 0f : Math.Max(0f, Math.Min(1f, (radius - v.BandInner) / span));
        }
    }

    /// <summary>
    /// The pulse of a full focus gauge (work order 037): about once every <see cref="PlayerSignValues.PulseSeconds"/> a
    /// thin ring of low brightness grows round her and is gone, from <see cref="PlayerSignValues.PulseDelay"/> after the
    /// gauge filled, for as long as it stays full; it steps down in brightness as it grows, so it fades rather than
    /// stops. The gauge in the HUD pulses with it, softly, between two opacities.
    /// </summary>
    public static class FocusSign
    {
        /// <summary>How far into its pulse, 0 to 1, a full gauge is <paramref name="sinceFull"/> seconds after it filled; -1 before the first.</summary>
        public static float Phase(float sinceFull, PlayerSignValues v)
        {
            float t = sinceFull - Math.Max(0f, v.PulseDelay);
            float period = Math.Max(0.05f, v.PulseSeconds);
            if (t < 0f) return -1f;
            float p = t / period;
            return p - (float)Math.Floor(p);
        }

        /// <summary>Is the pulse's ring drawn at this phase: while it grows.</summary>
        public static bool RingShown(float phase, PlayerSignValues v) => phase >= 0f && phase < Clamp01(v.PulseShowShare);

        /// <summary>The pulse ring's radius at this phase: from its start to its end while it grows.</summary>
        public static float RingRadius(float phase, PlayerSignValues v)
        {
            float show = Math.Max(1e-3f, Clamp01(v.PulseShowShare));
            float k = Clamp01(phase / show);
            float eased = 1f - (1f - k) * (1f - k); // fast out, slow at the end: calm
            return v.PulseFrom + (v.PulseTo - v.PulseFrom) * eased;
        }

        /// <summary>Its brightness step at this phase: 0 (low) for most of its growth, then 1 and 2, each fainter; -1 when not drawn.</summary>
        public static int Level(float phase, PlayerSignValues v)
        {
            if (!RingShown(phase, v)) return -1;
            float k = phase / Math.Max(1e-3f, Clamp01(v.PulseShowShare));
            return k < 0.6f ? 0 : k < 0.82f ? 1 : 2;
        }

        /// <summary>The opacity of the gauge's frame and key: low at the start of a pulse, high half way, low again.</summary>
        public static float GaugeAlpha(float sinceFull, PlayerSignValues v)
        {
            float period = Math.Max(0.05f, v.PulseSeconds);
            float t = Math.Max(0f, sinceFull) / period;
            float wave = 0.5f - 0.5f * (float)Math.Cos(2.0 * Math.PI * (t - Math.Floor(t)));
            return v.GaugeLow + (v.GaugeHigh - v.GaugeLow) * wave;
        }

        /// <summary>The word over her the moment it fills: FOCUS and the key it is spent with, as the bindings have it.</summary>
        public static string Word(string keyLabel) => string.IsNullOrEmpty(keyLabel) ? "FOCUS" : "FOCUS  " + keyLabel;

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }

    /// <summary>
    /// A line shown once a session (work order 037: the first time a clean window opens, the HUD says what it is): the
    /// first ask shows it for its seconds, and no ask after that shows it again. Time is passed in, in seconds.
    /// </summary>
    public sealed class OncePerSession
    {
        float _until = float.NegativeInfinity;

        /// <summary>True once it has been shown.</summary>
        public bool Done { get; private set; }

        /// <summary>Asks to show it now for this long. True the first time only.</summary>
        public bool TryShow(float now, float seconds)
        {
            if (Done) return false;
            Done = true;
            _until = now + Math.Max(0f, seconds);
            return true;
        }

        /// <summary>Is it showing now.</summary>
        public bool Showing(float now) => Done && now < _until;

        /// <summary>As at the start of a session. For tests.</summary>
        public void Forget()
        {
            Done = false;
            _until = float.NegativeInfinity;
        }
    }
}
