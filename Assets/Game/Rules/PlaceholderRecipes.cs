namespace Roguelike.Rules
{
    /// <summary>
    /// The recipe of each placeholder sound (work order 021, 2026-11-20), as data for <see cref="Synth"/>: short, dry and
    /// plain, to inform, not to decorate. All first guesses, made without hearing them; a number here can be tuned
    /// without touching the synthesiser. Each call gives a new recipe. Frequencies in Hz, times in seconds. Notes are
    /// named where a sound is a tune: C5 523, E5 659, G5 784, C6 1047, and so on.
    /// </summary>
    public static class PlaceholderRecipes
    {
        /// <summary>How long each sound is, as the work order's table has it.</summary>
        public static float Seconds(PlaceholderSound sound) => For(sound).Seconds;

        public static SynthRecipe For(PlaceholderSound sound)
        {
            switch (sound)
            {
                case PlaceholderSound.EnemyHit:
                    // A dull, short tick: low-passed noise over a low sine thump.
                    return Recipe(0.05f, 1.5f,
                        Noise(0f, 0.05f, 0.7f, 0.0008f, 0.01f, low: 1800f),
                        Sine(150f, 90f, 0f, 0.05f, 0.8f, 0.001f, 0.014f));
                case PlaceholderSound.EnemyDies:
                    // A low thump falling in pitch, and a short crumble of noise.
                    return Recipe(0.25f, 1.5f,
                        Sine(170f, 50f, 0f, 0.25f, 1f, 0.002f, 0.07f, release: 0.03f),
                        Wobble(Noise(0.03f, 0.22f, 0.45f, 0.01f, 0.06f, low: 1400f, lowTo: 350f, release: 0.03f), 28f, 0.8f));
                case PlaceholderSound.WindupRed:
                    // A soft low swell that rises a little.
                    return Recipe(0.25f, 1.2f,
                        Sine(120f, 150f, 0f, 0.25f, 1f, 0.16f, 0f, release: 0.07f),
                        Sine(240f, 300f, 0f, 0.25f, 0.2f, 0.16f, 0f, release: 0.07f),
                        Noise(0f, 0.25f, 0.15f, 0.16f, 0f, low: 300f, release: 0.07f));
                case PlaceholderSound.WindupBlue:
                    // A clear, higher two-note rise (E5 then B5), nothing like the red swell.
                    return Recipe(0.25f, 0f,
                        Sine(659f, 0f, 0f, 0.12f, 1f, 0.004f, 0.06f, release: 0.02f),
                        Sine(1318f, 0f, 0f, 0.12f, 0.2f, 0.004f, 0.04f, release: 0.02f),
                        Sine(988f, 0f, 0.11f, 0.14f, 1f, 0.004f, 0.07f, release: 0.03f),
                        Sine(1976f, 0f, 0.11f, 0.14f, 0.2f, 0.004f, 0.04f, release: 0.03f));
                case PlaceholderSound.Strike:
                    // A heavy low impact.
                    return Recipe(0.2f, 1.6f,
                        Rough(Sine(95f, 42f, 0f, 0.2f, 1f, 0.0015f, 0.06f, release: 0.03f), 2.5f),
                        Noise(0f, 0.12f, 0.7f, 0.001f, 0.025f, low: 700f, lowTo: 200f));
                case PlaceholderSound.Arrow:
                    // A short bright twang and a hiss.
                    return Recipe(0.15f, 1.2f,
                        Rough(Sine(620f, 560f, 0f, 0.15f, 0.8f, 0.001f, 0.035f), 1.8f),
                        Sine(1240f, 1120f, 0f, 0.15f, 0.25f, 0.001f, 0.02f),
                        Noise(0f, 0.15f, 0.45f, 0.004f, 0.045f, low: 10000f, high: 3000f));
                case PlaceholderSound.Hurt:
                    // A sharp, mid-pitched crack; the game plays it louder for a bigger hit.
                    return Recipe(0.15f, 1.8f,
                        Noise(0f, 0.15f, 1f, 0.0005f, 0.018f, low: 5000f, high: 900f),
                        Rough(Sine(480f, 320f, 0f, 0.15f, 0.7f, 0.001f, 0.035f), 2f));
                case PlaceholderSound.HerDeath:
                    // A slow falling tone.
                    return Recipe(0.8f, 1.1f,
                        Sine(440f, 110f, 0f, 0.8f, 1f, 0.02f, 0.45f, release: 0.2f),
                        Sine(220f, 55f, 0f, 0.8f, 0.4f, 0.02f, 0.45f, release: 0.2f),
                        Sine(660f, 165f, 0f, 0.8f, 0.15f, 0.02f, 0.25f, release: 0.2f));
                case PlaceholderSound.Dash:
                    // A short whoosh: noise swept up through a band.
                    return Recipe(0.2f, 1f,
                        Noise(0f, 0.2f, 1f, 0.06f, 0.06f, low: 900f, lowTo: 6500f, high: 350f, highTo: 2200f, release: 0.05f));
                case PlaceholderSound.Dazed:
                    // A crack and a short high ring, wobbling a little.
                    return Recipe(0.3f, 1.3f,
                        Noise(0f, 0.06f, 0.9f, 0.0005f, 0.012f, low: 7000f, high: 1200f),
                        Wobble(Sine(1760f, 0f, 0.008f, 0.29f, 0.5f, 0.002f, 0.08f, release: 0.03f), 14f, 0.6f),
                        Sine(2350f, 0f, 0.008f, 0.25f, 0.25f, 0.002f, 0.05f, release: 0.03f));
                case PlaceholderSound.BossCharge:
                    // A low rumble that swells.
                    return Recipe(0.6f, 1.4f,
                        Wobble(Noise(0f, 0.6f, 1f, 0.42f, 0f, low: 220f, lowTo: 480f, release: 0.14f), 16f, 0.4f),
                        Rough(Sine(50f, 68f, 0f, 0.6f, 0.7f, 0.42f, 0f, release: 0.14f), 1.5f));
                case PlaceholderSound.BossAnger:
                    // A growl: low noise and a rough low tone, wobbling together.
                    return Recipe(0.8f, 1.6f,
                        Wobble(Noise(0f, 0.8f, 0.8f, 0.06f, 0.5f, low: 450f, release: 0.2f), 23f, 0.7f),
                        Wobble(Rough(Sine(78f, 62f, 0f, 0.8f, 1f, 0.06f, 0.5f, release: 0.2f), 4f), 23f, 0.5f));
                case PlaceholderSound.BossCall:
                    // A horn: two low notes (D3 then G3), driven for a brassy edge.
                    return Recipe(0.7f, 1.2f,
                        Rough(Sine(147f, 0f, 0f, 0.34f, 1f, 0.04f, 0f, release: 0.05f), 2.2f),
                        Sine(294f, 0f, 0f, 0.34f, 0.3f, 0.05f, 0f, release: 0.05f),
                        Rough(Sine(196f, 0f, 0.33f, 0.37f, 1f, 0.04f, 0.6f, release: 0.12f), 2.2f),
                        Sine(392f, 0f, 0.33f, 0.37f, 0.3f, 0.05f, 0.6f, release: 0.12f));
                case PlaceholderSound.ChestOpens:
                    // A small rising arpeggio of three notes: C6, E6, G6.
                    return Recipe(0.4f, 0f,
                        Note(1047f, 0f, 0.16f, 0.07f),
                        Note(1319f, 0.1f, 0.16f, 0.07f),
                        Note(1568f, 0.2f, 0.2f, 0.08f));
                case PlaceholderSound.ChoiceTaken:
                    // A firm two-note chord: C5 and G5, an octave on top.
                    return Recipe(0.3f, 1.2f,
                        Sine(523f, 0f, 0f, 0.3f, 1f, 0.004f, 0.12f, release: 0.04f),
                        Sine(784f, 0f, 0f, 0.3f, 1f, 0.004f, 0.12f, release: 0.04f),
                        Sine(1046f, 0f, 0f, 0.3f, 0.2f, 0.004f, 0.08f, release: 0.04f));
                case PlaceholderSound.FloorCleared:
                    // Three rising notes: C5, E5, G5, the last held a little.
                    return Recipe(0.6f, 0f,
                        Note(523f, 0f, 0.18f, 0.08f),
                        Note(659f, 0.15f, 0.18f, 0.08f),
                        Note(784f, 0.3f, 0.3f, 0.15f));
                case PlaceholderSound.ActCleared:
                    // Five notes, a small fanfare: G4, C5, E5, G5, and C6 held over a C5 and G5.
                    return Recipe(1.2f, 0f,
                        Note(392f, 0f, 0.14f, 0.08f),
                        Note(523f, 0.12f, 0.14f, 0.08f),
                        Note(659f, 0.24f, 0.14f, 0.08f),
                        Note(784f, 0.36f, 0.2f, 0.1f),
                        Note(1047f, 0.52f, 0.68f, 0.35f, release: 0.15f),
                        Sine(523f, 0f, 0.52f, 0.68f, 0.5f, 0.006f, 0.35f, release: 0.15f),
                        Sine(784f, 0f, 0.52f, 0.68f, 0.4f, 0.006f, 0.35f, release: 0.15f));
                case PlaceholderSound.Menu:
                    // A tiny tick.
                    return Recipe(0.03f, 0f,
                        Sine(2200f, 0f, 0f, 0.03f, 1f, 0.0005f, 0.006f, release: 0.005f),
                        Noise(0f, 0.03f, 0.3f, 0.0005f, 0.003f, high: 4000f, release: 0.005f));
                default: // FocusFull
                    // A soft single chime: E6 with two quieter partials.
                    return Recipe(0.3f, 0f,
                        Sine(1319f, 0f, 0f, 0.3f, 1f, 0.002f, 0.1f, release: 0.04f),
                        Sine(2637f, 0f, 0f, 0.3f, 0.3f, 0.002f, 0.05f, release: 0.04f),
                        Sine(3956f, 0f, 0f, 0.3f, 0.12f, 0.002f, 0.03f, release: 0.04f));
            }
        }

        static SynthRecipe Recipe(float seconds, float drive, params SynthLayer[] layers) =>
            new SynthRecipe { Seconds = seconds, Drive = drive, Peak = 0.9f, Seed = 21, Layers = layers };

        static SynthLayer Sine(float from, float to, float start, float length, float gain, float attack, float decay, float release = 0.01f) =>
            new SynthLayer
            {
                Wave = SynthWave.Sine, From = from, To = to, Start = start, Length = length, Gain = gain,
                Attack = attack, Decay = decay, Release = release,
            };

        static SynthLayer Noise(float start, float length, float gain, float attack, float decay,
                                float low = 0f, float lowTo = 0f, float high = 0f, float highTo = 0f, float release = 0.01f) =>
            new SynthLayer
            {
                Wave = SynthWave.Noise, Start = start, Length = length, Gain = gain, Attack = attack, Decay = decay, Release = release,
                LowPass = low, LowPassTo = lowTo, HighPass = high, HighPassTo = highTo,
            };

        /// <summary>A plain note of a tune: a pure sine, a fast attack and a decay.</summary>
        static SynthLayer Note(float hz, float start, float length, float decay, float release = 0.02f) =>
            Sine(hz, 0f, start, length, 1f, 0.003f, decay, release);

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
