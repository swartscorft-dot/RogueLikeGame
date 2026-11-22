using System;

namespace Roguelike.Rules
{
    /// <summary>Where spent focus stands for the eye and the ear (work order 071).</summary>
    public enum FocusFeelPhase
    {
        /// <summary>Nothing to show: focus is filling.</summary>
        None,
        /// <summary>Ready and unspent: a quiet glow on her.</summary>
        Ready,
        /// <summary>The gains run: the aura, the light, the teal shots, the screen's edge.</summary>
        Running,
        /// <summary>The gains' last second: the aura flickers.</summary>
        Ending,
    }

    /// <summary>
    /// The numbers of how spent focus is felt (work order 071, Kristian, 2026-11-21: "glemmer å bruke den heller, fordi man
    /// ikke føler det"): at the moment of spending, while the gains run, in their last second, and while focus is ready. All
    /// first guesses with their defaults in code (no asset holds them), made without seeing or hearing them. Off
    /// (<see cref="On"/>): focus looks and sounds as before the order (the asset's pink empowered shots, nothing on her).
    /// Colours are red, green, blue, 0 to 1; the teal is the HUD's accent (0.25, 0.85, 0.85), brighter.
    /// </summary>
    [Serializable]
    public sealed class FocusFeelValues
    {
        /// <summary>The master switch.</summary>
        public bool On = true;

        // Her shots while the gains run (her empowered look: before the order a pink rim at 1.25 times the size).
        public float ShotRimR = 0.2f, ShotRimG = 1f, ShotRimB = 0.88f; // teal tracer
        public float ShotScale = 1.45f;     // every size of a shot's effect (flash, streak, spark) times this

        // The moment of spending: a burst that cannot be missed.
        public float BurstRadius = 7f;      // m: the ring of light that runs out from her on the ground
        public float BurstSeconds = 0.45f;  // s it takes
        public float BurstStar = 3.4f;      // m: the star at her chest
        public float BurstStarSeconds = 0.3f;
        public float BurstLight = 18f;      // the teal light's peak intensity at the moment, fading to the aura's over...
        public float BurstLightSeconds = 0.3f; // ...this many seconds of real time
        public float BurstLightRange = 14f; // m
        public float BurstFreeze = 0.06f;   // s of real time the game stands still (the hit stop's own rule; 0: none)
        public float BurstKick = 0.15f;     // m the camera is kicked (the firing weight's most; 0: none)
        public float PartShare = 0.6f;      // a Hair Trigger part's burst: this share of a full gauge's sizes, light and kick
        public float PartFreeze = 0.035f;   // s: and its shorter freeze
        public float SpendVolume = 0.75f;   // the spending sound, as a share of the basic shot's volume (louder than the full chime's 0.3)
        public float PartPitch = 1.15f;     // a part's spending sound a little higher than a full gauge's

        // While the gains run: she looks empowered.
        public float AuraLight = 4.5f;      // the teal light on her
        public float AuraRange = 5f;        // m
        public float AuraHeight = 1.2f;     // m over her feet
        public float AuraRing = 1.25f;      // m: the ring at her feet
        public float AuraOrbit = 0.9f;      // m: the teal stars turning round her chest
        public float AuraSpin = 220f;       // degrees a second

        // The last second: a cue that it is ending.
        public float EndingSeconds = 1f;    // s before the end the aura starts to flicker and the falling sound plays
        public float FlickerHz = 8f;        // the flicker's on-and-off a second
        public float FlickerLow = 0.15f;    // its off share of the aura's light
        public float EndingVolume = 0.5f;

        // Ready and unspent: a quiet glow on her, weaker than the aura so the two are told apart.
        public float ReadyLight = 1.4f;     // its brightest (the aura's is 4.5)
        public float ReadyLeast = 0.45f;    // its dimmest, as a share of its brightest
        public float ReadyPulseSeconds = 1.6f; // a slow, calm pulse
        public float ReadyVolume = 0.3f;    // a Hair Trigger part that fills (a full gauge has the placeholder sounds' chime)

        // The screen's edge (FocusEdge): a teal frame under every interface element while the gains run.
        public float EdgeStrength = 0.32f;  // its opacity at its strongest; 0: none
        public float EdgeWidth = 0.07f;     // its depth, as a share of the view's height
        public float EdgeR = 0.25f, EdgeG = 0.85f, EdgeB = 0.85f;

        public float LightR = 0.3f, LightG = 1f, LightB = 0.9f; // the teal light's colour
    }

    /// <summary>The rules of how spent focus is felt (work order 071), with no engine in them.</summary>
    public static class FocusFeelRules
    {
        /// <summary>Where it stands: the gains running (their last second apart), else ready, else nothing.</summary>
        public static FocusFeelPhase Phase(bool ready, bool boosted, float remaining, FocusFeelValues v)
        {
            v = v ?? new FocusFeelValues();
            if (boosted) return remaining <= v.EndingSeconds ? FocusFeelPhase.Ending : FocusFeelPhase.Running;
            return ready ? FocusFeelPhase.Ready : FocusFeelPhase.None;
        }

        /// <summary>
        /// The aura's level now, 0 to 1: full while the gains run, and in their last second on and off
        /// <see cref="FocusFeelValues.FlickerHz"/> times a second, its off at <see cref="FocusFeelValues.FlickerLow"/>.
        /// <paramref name="remaining"/> is the gains' seconds left.
        /// </summary>
        public static float AuraLevel(FocusFeelPhase phase, float remaining, FocusFeelValues v)
        {
            v = v ?? new FocusFeelValues();
            if (phase == FocusFeelPhase.Running) return 1f;
            if (phase != FocusFeelPhase.Ending) return 0f;
            return FlickerOn(remaining, v) ? 1f : Clamp01(v.FlickerLow);
        }

        /// <summary>True in the on half of the last second's flicker, counted from the end, so it ends on an off.</summary>
        public static bool FlickerOn(float remaining, FocusFeelValues v)
        {
            float hz = Math.Max(0.1f, v.FlickerHz);
            double half = remaining * hz * 2.0;
            return ((long)Math.Floor(half) & 1L) == 1L;
        }

        /// <summary>The ready glow's level, <see cref="FocusFeelValues.ReadyLeast"/> to 1: a slow cosine pulse from its brightest.</summary>
        public static float ReadyLevel(float since, FocusFeelValues v)
        {
            v = v ?? new FocusFeelValues();
            float least = Clamp01(v.ReadyLeast);
            if (v.ReadyPulseSeconds <= 0f) return 1f;
            double k = 0.5 + 0.5 * Math.Cos(2.0 * Math.PI * Math.Max(0f, since) / v.ReadyPulseSeconds);
            return (float)(least + (1.0 - least) * k);
        }

        /// <summary>
        /// The teal light's intensity: the ready glow's (never as bright as the aura's), the aura's times its level, and over
        /// it the burst at the moment of spending, from its peak to nothing over its seconds (<paramref name="sinceBurst"/>, real time).
        /// </summary>
        public static float LightIntensity(FocusFeelPhase phase, float level, float sinceBurst, float burstShare, FocusFeelValues v)
        {
            v = v ?? new FocusFeelValues();
            float own = phase == FocusFeelPhase.Ready ? Math.Min(v.ReadyLight, v.AuraLight) * level
                      : phase == FocusFeelPhase.Running || phase == FocusFeelPhase.Ending ? v.AuraLight * level : 0f;
            return Math.Max(own, BurstLight(sinceBurst, burstShare, v));
        }

        /// <summary>The burst's light <paramref name="since"/> s after spending: its peak, easing out to nothing.</summary>
        public static float BurstLight(float since, float share, FocusFeelValues v)
        {
            if (v.BurstLightSeconds <= 0f || since < 0f || since >= v.BurstLightSeconds) return 0f;
            float k = 1f - since / v.BurstLightSeconds;
            return Math.Max(0f, v.BurstLight * share) * k * k;
        }

        /// <summary>The screen's edge's opacity: its strength times the aura's level while the gains run; 0 otherwise.</summary>
        public static float EdgeAlpha(FocusFeelPhase phase, float level, FocusFeelValues v)
        {
            v = v ?? new FocusFeelValues();
            if (!v.On || (phase != FocusFeelPhase.Running && phase != FocusFeelPhase.Ending)) return 0f;
            return Clamp01(v.EdgeStrength) * Clamp01(level);
        }

        /// <summary>The screen's edge's depth in pixels for a view <paramref name="height"/> high (layout or real: it is a share).</summary>
        public static float EdgeDepth(float height, FocusFeelValues v) => Math.Max(0f, (v ?? new FocusFeelValues()).EdgeWidth) * Math.Max(0f, height);

        /// <summary>
        /// A Hair Trigger part filled (<paramref name="before"/> to <paramref name="now"/> parts full), and not the last, whose
        /// full gauge rings the placeholder sounds' own chime: one soft sound of its own.
        /// </summary>
        public static bool PartReadySound(bool hairTrigger, int before, int now, bool full) => hairTrigger && now > before && !full;

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        // ---------------- Its sounds, made in code (no sound file) ----------------

        /// <summary>
        /// The spending sound: a low boom falling (a rough 95 Hz to 38 Hz), a dull rush of noise, and over it a bright teal
        /// rise (A5 to A6 with its octave): louder and lower than the full chime. 0.7 s.
        /// </summary>
        public static SynthRecipe SpendRecipe() => Recipe(0.7f, 1.4f,
            Rough(Sine(95f, 38f, 0f, 0.7f, 1f, 0.003f, 0.22f, 0.1f), 2.2f),
            Noise(0f, 0.35f, 0.55f, 0.004f, 0.09f, 1600f, 300f, 0.05f),
            Sine(880f, 1760f, 0.02f, 0.5f, 0.35f, 0.03f, 0.18f, 0.08f),
            Sine(1760f, 3520f, 0.02f, 0.4f, 0.12f, 0.03f, 0.1f, 0.06f));

        /// <summary>The falling sound of the last second: a soft tone sliding down an octave and a half, wobbling. 0.6 s.</summary>
        public static SynthRecipe EndingRecipe() => Recipe(0.6f, 0f,
            Wobble(Sine(880f, 330f, 0f, 0.6f, 1f, 0.01f, 0.3f, 0.12f), 7f, 0.35f),
            Sine(1320f, 495f, 0f, 0.5f, 0.25f, 0.01f, 0.18f, 0.1f));

        /// <summary>A Hair Trigger part ready: one soft low chime, D5 with a quiet fifth, lower and shorter than the full chime. 0.25 s.</summary>
        public static SynthRecipe ReadyRecipe() => Recipe(0.25f, 0f,
            Sine(587f, 0f, 0f, 0.25f, 1f, 0.003f, 0.08f, 0.04f),
            Sine(880f, 0f, 0f, 0.2f, 0.3f, 0.003f, 0.05f, 0.04f));

        static SynthRecipe Recipe(float seconds, float drive, params SynthLayer[] layers) =>
            new SynthRecipe { Seconds = seconds, Drive = drive, Peak = 0.9f, Seed = 71, Layers = layers };

        static SynthLayer Sine(float from, float to, float start, float length, float gain, float attack, float decay, float release) =>
            new SynthLayer { Wave = SynthWave.Sine, From = from, To = to, Start = start, Length = length, Gain = gain, Attack = attack, Decay = decay, Release = release };

        static SynthLayer Noise(float start, float length, float gain, float attack, float decay, float low, float lowTo, float release) =>
            new SynthLayer { Wave = SynthWave.Noise, Start = start, Length = length, Gain = gain, Attack = attack, Decay = decay, Release = release, LowPass = low, LowPassTo = lowTo };

        static SynthLayer Rough(SynthLayer layer, float drive)
        {
            layer.Drive = drive;
            return layer;
        }

        static SynthLayer Wobble(SynthLayer layer, float hz, float depth)
        {
            layer.Tremolo = hz;
            layer.TremoloDepth = depth;
            return layer;
        }
    }
}
