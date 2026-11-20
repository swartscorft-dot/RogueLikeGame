using System;

namespace Roguelike.Rules
{
    /// <summary>What a layer of a synthesised sound is made of: a sine (with a pitch slide), or noise.</summary>
    public enum SynthWave { Sine, Noise }

    /// <summary>
    /// One layer of a synthesised sound (work order 021, 2026-11-20): a sine sliding from one pitch to another, or noise,
    /// through an optional one-pole low-pass and high-pass (each may slide too), shaped by an attack, a decay and a
    /// release, optionally driven into a soft clip and wobbled in level. Times are in seconds from the sound's start,
    /// frequencies in Hz. A 0 leaves that part out.
    /// </summary>
    [Serializable]
    public sealed class SynthLayer
    {
        public SynthWave Wave = SynthWave.Sine;
        public float Start;              // s into the sound
        public float Length = 0.1f;      // s; cut at the sound's end
        public float From = 440f;        // Hz at its start (a sine)
        public float To;                 // Hz at its end, sliding exponentially; 0: no slide
        public float Gain = 1f;
        public float Attack = 0.002f;    // s from silence to full, linearly
        public float Decay;              // s per e down after the attack; 0 holds full
        public float Release = 0.01f;    // s down to silence at its end, linearly
        public float LowPass;            // Hz at its start; 0: none
        public float LowPassTo;          // Hz at its end; 0: as at its start
        public float HighPass;           // Hz at its start; 0: none
        public float HighPassTo;         // Hz at its end; 0: as at its start
        public float Drive;              // soft clip of this layer alone (tanh), 0: none; higher is rougher
        public float Tremolo;            // Hz of a wobble in its level; 0: none
        public float TremoloDepth = 0.5f; // 0 to 1: how deep the wobble goes
    }

    /// <summary>A whole sound: its length, its layers mixed, a soft clip of the mix, and the level it peaks at.</summary>
    [Serializable]
    public sealed class SynthRecipe
    {
        public float Seconds = 0.1f;
        /// <summary>Soft clip of the mix (tanh); 0: none.</summary>
        public float Drive;
        /// <summary>The level the finished samples peak at, under full scale; the game's volume does the rest.</summary>
        public float Peak = 0.9f;
        /// <summary>The noise's seed: the same seed gives the same sound every time.</summary>
        public uint Seed = 21;
        public SynthLayer[] Layers = new SynthLayer[0];
    }

    /// <summary>
    /// A small synthesiser (work order 021, 2026-11-20; the cut list's "placeholders": no sound files are added, every
    /// placeholder sound is made in code at start-up). Mono, at <see cref="SampleRate"/>. Every sound it makes starts and
    /// ends at exact silence (a fade of <see cref="EdgeFade"/> at each end, so it does not click), peaks at its recipe's
    /// <see cref="SynthRecipe.Peak"/> (never at or over full scale), holds no NaN, and is the same every time it is made:
    /// the noise comes from a fixed seed. No engine in it.
    /// </summary>
    public static class Synth
    {
        public const int SampleRate = 44100;
        /// <summary>s of fade at each end of every sound, to exact silence.</summary>
        public const float EdgeFade = 0.002f;
        /// <summary>The highest peak a recipe may ask for.</summary>
        public const float MostPeak = 0.98f;

        const double TwoPi = 2.0 * Math.PI;

        /// <summary>The samples of a recipe, mono, at <paramref name="sampleRate"/>.</summary>
        public static float[] Render(SynthRecipe recipe, int sampleRate = SampleRate)
        {
            int rate = Math.Max(8000, sampleRate);
            double seconds = recipe != null && recipe.Seconds > 0f ? recipe.Seconds : 0.0;
            int n = Math.Max(2, (int)Math.Round(seconds * rate));
            var mix = new double[n];
            if (recipe != null && recipe.Layers != null)
                for (int k = 0; k < recipe.Layers.Length; k++)
                    if (recipe.Layers[k] != null) AddLayer(mix, recipe.Layers[k], rate, LayerSeed(recipe.Seed, k));

            double drive = recipe != null ? Safe(recipe.Drive) : 0.0;
            double peak = 0.0;
            for (int i = 0; i < n; i++)
            {
                double v = mix[i];
                if (double.IsNaN(v) || double.IsInfinity(v)) v = 0.0;
                if (drive > 1e-3) v = Math.Tanh(drive * v);
                mix[i] = v;
                peak = Math.Max(peak, Math.Abs(v));
            }

            double target = recipe != null ? Math.Max(0.0, Math.Min(MostPeak, Safe(recipe.Peak))) : 0.0;
            double scale = peak > 1e-9 ? target / peak : 0.0;
            int edge = Math.Max(1, (int)Math.Round(EdgeFade * rate));
            var s = new float[n];
            for (int i = 0; i < n; i++)
            {
                double w = 1.0;
                if (i < edge) w = i / (double)edge;
                int fromEnd = n - 1 - i;
                if (fromEnd < edge) w = Math.Min(w, fromEnd / (double)edge);
                s[i] = (float)(mix[i] * scale * w);
            }
            return s;
        }

        static void AddLayer(double[] mix, SynthLayer layer, int rate, uint seed)
        {
            int start = (int)Math.Round(Math.Max(0.0, Safe(layer.Start)) * rate);
            int length = (int)Math.Round(Math.Max(0.0, Safe(layer.Length)) * rate);
            int end = Math.Min(mix.Length, start + length);
            if (end - start < 2) return;

            double span = (end - start) / (double)rate; // cut at the sound's end: it is released there
            double attack = Math.Max(0.0005, Safe(layer.Attack));
            double decay = Safe(layer.Decay);
            double release = Math.Max(0.0005, Safe(layer.Release));
            double from = Math.Max(1.0, Safe(layer.From));
            double to = layer.To > 0f ? Math.Max(1.0, Safe(layer.To)) : from;
            double lowFrom = Safe(layer.LowPass), lowTo = layer.LowPassTo > 0f ? Safe(layer.LowPassTo) : lowFrom;
            double highFrom = Safe(layer.HighPass), highTo = layer.HighPassTo > 0f ? Safe(layer.HighPassTo) : highFrom;
            double drive = Safe(layer.Drive);
            double driveNorm = drive > 1e-3 ? Math.Tanh(drive) : 1.0;
            double tremolo = Safe(layer.Tremolo);
            double depth = Math.Max(0.0, Math.Min(1.0, Safe(layer.TremoloDepth)));
            double gain = Safe(layer.Gain);

            uint noise = seed == 0u ? 0x9E3779B9u : seed;
            double phase = 0.0, low = 0.0, high = 0.0;
            for (int i = start; i < end; i++)
            {
                double t = (i - start) / (double)rate;
                double k = span > 0.0 ? t / span : 0.0;
                double x;
                if (layer.Wave == SynthWave.Noise)
                {
                    // xorshift32: a fixed seed, the same noise every time.
                    noise ^= noise << 13;
                    noise ^= noise >> 17;
                    noise ^= noise << 5;
                    x = noise / (double)uint.MaxValue * 2.0 - 1.0;
                }
                else
                {
                    x = Math.Sin(phase);
                    phase += TwoPi * Slide(from, to, k) / rate;
                    if (phase > TwoPi) phase -= TwoPi;
                }
                if (lowFrom > 0.0)
                {
                    low += OnePole(Slide(lowFrom, lowTo, k), rate) * (x - low);
                    x = low;
                }
                if (highFrom > 0.0)
                {
                    high += OnePole(Slide(highFrom, highTo, k), rate) * (x - high);
                    x -= high;
                }
                if (drive > 1e-3) x = Math.Tanh(drive * x) / driveNorm;

                double env = t < attack ? t / attack : decay > 0.0 ? Math.Exp(-(t - attack) / decay) : 1.0;
                double left = span - t;
                if (left < release) env *= Math.Max(0.0, left / release);
                if (tremolo > 0.0) env *= 1.0 - depth * 0.5 * (1.0 - Math.Cos(TwoPi * tremolo * t));
                mix[i] += gain * env * x;
            }
        }

        /// <summary>From <paramref name="a"/> to <paramref name="b"/> exponentially, <paramref name="k"/> of the way (0 to 1).</summary>
        static double Slide(double a, double b, double k)
        {
            if (a <= 0.0 || b <= 0.0 || Math.Abs(a - b) < 1e-9) return a;
            return a * Math.Pow(b / a, k);
        }

        /// <summary>The coefficient of a one-pole filter at this cut-off.</summary>
        static double OnePole(double hz, int rate) => 1.0 - Math.Exp(-TwoPi * Math.Max(1.0, hz) / rate);

        static double Safe(float v) => float.IsNaN(v) || float.IsInfinity(v) ? 0.0 : v;

        static uint LayerSeed(uint seed, int layer) => seed * 2654435761u + (uint)(layer + 1) * 40503u + 1u;
    }
}
