using System;

namespace Roguelike.Rules
{
    /// <summary>How heavy a shot of hers is, for its weight on the screen and in the light (work order 064).</summary>
    public enum ShotWeight
    {
        /// <summary>The basic shot: four a second, so its kick and its light are small.</summary>
        Basic,
        /// <summary>Any skill's shot.</summary>
        Skill,
        /// <summary>Heavy Volley, Long Shot's perfect release, Scatter Blast: the strongest kick, and a longer hit stop.</summary>
        Heavy,
        /// <summary>The clean last tap of a full clean Triple Tap chain (work order 064 item 2): the heavy one's weight, and the most light.</summary>
        FullClean,
    }

    /// <summary>
    /// The weight of a shot going off (work order 064, Kristian, 2026-11-21: "savner også litt mer umf når man fyrer av,
    /// kanskje litt lysflash og trøkk"): a warm point light at the muzzle for a moment, a short kick of the camera back along
    /// the shot's line, a larger and shorter muzzle flash, and for the heavy skills a slightly longer hit stop. All first
    /// guesses with their defaults in code (no asset holds them); each turned to 0 (or 1 for a scale) is the morning's.
    /// Nothing here changes a hit, and nothing moves where the cursor aims (the camera's kick is drawn only).
    /// </summary>
    [Serializable]
    public sealed class FiringWeightValues
    {
        /// <summary>The master switch: off, every shot looks as it did this morning.</summary>
        public bool On = true;

        // The muzzle light: real point lights, pooled.
        public float LightSeconds = 0.05f;      // s of real time a light is on, fading from its peak to dark
        public float LightRange = 7f;           // m it reaches
        public float BasicLight = 2.5f;         // its peak intensity for the basic shot
        public float SkillLight = 5f;           // a skill's
        public float HeavyLight = 8f;           // a heavy skill's
        public float FullCleanLight = 10f;      // the full clean chain's last tap
        public float LightR = 1f, LightG = 0.78f, LightB = 0.45f; // warm: the muzzle flash's own orange-white
        public int MaxLights = 4;               // lights alive at once at most; the oldest is taken for a new one

        // The camera's kick: a few centimetres back along the shot's line and a fast return, in real time.
        public float KickSeconds = 0.08f;       // s from the shot to the camera back where it was
        public float KickPeakShare = 0.25f;     // the share of that time it takes to reach its furthest
        public float BasicKick = 0.025f;        // m: the basic shot's, small (four a second)
        public float SkillKick = 0.07f;         // m: a skill's
        public float HeavyKick = 0.12f;         // m: a heavy skill's and the full clean chain's
        public float MostKick = 0.15f;          // m: shots that follow each other fast never add up past this

        // The muzzle flash shape: larger and shorter.
        public float FlashSizeScale = 1.35f;    // its size times this
        public float FlashSecondsScale = 0.7f;  // its life times this

        // The heavy skills' hit stop (within what work order 038 made safe: the recast clock runs on real time through a hit
        // stop, and no frame adds more than RecastClock.MostPerFrame, 0.1 s).
        public float HeavyHitStop = 0.075f;     // s, up from the feel's 0.05; 0: the feel's, as before
    }

    /// <summary>The rules of a shot's weight (work order 064), with no engine in them.</summary>
    public static class FiringWeightRules
    {
        /// <summary>The weight of a skill's shot: the heavy three by name and moment, the full clean chain's last tap, the rest a skill's.</summary>
        public static ShotWeight Of(SkillDefinition skill, bool perfect, bool fullClean)
        {
            if (skill == null) return ShotWeight.Basic;
            if (fullClean) return ShotWeight.FullClean;
            if (skill.Name == "Heavy Volley" || skill.Name == "Scatter Blast" || (skill.Name == "Long Shot" && perfect)) return ShotWeight.Heavy;
            return ShotWeight.Skill;
        }

        /// <summary>True for a weight that takes the heavy hit stop.</summary>
        public static bool IsHeavy(ShotWeight w) => w == ShotWeight.Heavy || w == ShotWeight.FullClean;

        /// <summary>The hit stop of a shot that landed: the heavy one for a heavy weight (when above 0), else the feel's.</summary>
        public static float HitStop(ShotWeight w, float feelHitStop, FiringWeightValues v) =>
            v != null && v.On && IsHeavy(w) && v.HeavyHitStop > 0f ? Math.Max(feelHitStop, v.HeavyHitStop) : feelHitStop;

        /// <summary>The camera's kick for a weight, in metres; 0 with the switch off.</summary>
        public static float Kick(ShotWeight w, FiringWeightValues v)
        {
            if (v == null || !v.On) return 0f;
            switch (w)
            {
                case ShotWeight.Basic: return Math.Max(0f, v.BasicKick);
                case ShotWeight.Skill: return Math.Max(0f, v.SkillKick);
                default: return Math.Max(0f, v.HeavyKick);
            }
        }

        /// <summary>The muzzle light's peak for a weight; 0 with the switch off.</summary>
        public static float Light(ShotWeight w, FiringWeightValues v)
        {
            if (v == null || !v.On) return 0f;
            switch (w)
            {
                case ShotWeight.Basic: return Math.Max(0f, v.BasicLight);
                case ShotWeight.Skill: return Math.Max(0f, v.SkillLight);
                case ShotWeight.Heavy: return Math.Max(0f, v.HeavyLight);
                default: return Math.Max(0f, v.FullCleanLight);
            }
        }

        /// <summary>A light's intensity <paramref name="age"/> s after its shot: its peak, down to 0 at the end of its life; 0 after.</summary>
        public static float LightAt(float age, float seconds, float peak)
        {
            if (seconds <= 0f || age < 0f || age >= seconds) return 0f;
            return peak * (1f - age / seconds);
        }

        /// <summary>
        /// The kick's share of its distance <paramref name="age"/> s after its shot: up to 1 fast (by the peak share of its
        /// time), then eased back to 0 at its end; 0 before and after.
        /// </summary>
        public static float KickShape(float age, float seconds, float peakShare)
        {
            if (seconds <= 0f || age <= 0f || age >= seconds) return 0f;
            float peak = Math.Max(1e-3f, Math.Min(0.9f, peakShare)) * seconds;
            if (age < peak) return age / peak;
            float k = (age - peak) / (seconds - peak);
            return (1f - k) * (1f - k);
        }
    }

    /// <summary>
    /// The camera's kicks that run (work order 064): each a distance back along a shot's line, shaped by
    /// <see cref="FiringWeightRules.KickShape"/>. Their sum is the camera's offset from the rig's place, never longer than the
    /// most (<see cref="FiringWeightValues.MostKick"/>): twenty basic shots in a row do not walk the camera away. A fixed
    /// number of kicks is kept, the oldest giving way; nothing is allocated. Time is passed in, in seconds of real time.
    /// </summary>
    public sealed class CameraKicks
    {
        const int Capacity = 8;
        readonly float[] _at = new float[Capacity];
        readonly float[] _x = new float[Capacity];
        readonly float[] _z = new float[Capacity];
        int _next;

        public CameraKicks()
        {
            for (int i = 0; i < Capacity; i++) _at[i] = float.NegativeInfinity;
        }

        /// <summary>A shot went off along (dirX, dirZ) on the ground: the camera kicks <paramref name="metres"/> back against it.</summary>
        public void Add(float now, float dirX, float dirZ, float metres)
        {
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            if (metres <= 0f || len < 1e-6f) return;
            _at[_next] = now;
            _x[_next] = -dirX / len * metres;
            _z[_next] = -dirZ / len * metres;
            _next = (_next + 1) % Capacity;
        }

        /// <summary>The camera's offset on the ground now (x, z), clamped to <paramref name="most"/> metres.</summary>
        public void Offset(float now, float seconds, float peakShare, float most, out float x, out float z)
        {
            x = z = 0f;
            for (int i = 0; i < Capacity; i++)
            {
                float k = FiringWeightRules.KickShape(now - _at[i], seconds, peakShare);
                if (k <= 0f) continue;
                x += _x[i] * k;
                z += _z[i] * k;
            }
            float len = (float)Math.Sqrt(x * x + z * z);
            float cap = Math.Max(0f, most);
            if (len > cap && len > 1e-9f)
            {
                x *= cap / len;
                z *= cap / len;
            }
        }

        /// <summary>True while any kick runs.</summary>
        public bool Running(float now, float seconds)
        {
            for (int i = 0; i < Capacity; i++)
                if (now - _at[i] < seconds && now >= _at[i]) return true;
            return false;
        }

        public void Clear()
        {
            for (int i = 0; i < Capacity; i++) _at[i] = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// The basic shot's sound made several (work order 064, Kristian, 2026-11-21: "kan vi også lage litt variasjoner i
    /// venstrelick angripet? blir veldig slitsomt for ørene å høre samme lyd gjenta seg"). The basic shot is a recording
    /// (gunshot_54, the ShotFx asset's), not a sound made in code; the variations are made of it as it plays: the click's
    /// brightness (a low-pass on the voice), the body's pitch, the tail's length (a fade over its end). Never the same one
    /// twice in a row, a small random pitch and volume on each shot, and the basic shot a little quieter than the skills'.
    /// Clips under Resources/Sounds/Shots named "BasicShot_1", "BasicShot_2" ... are the variations' own clips when two or
    /// more are there. First guesses: nobody has heard them.
    /// </summary>
    [Serializable]
    public sealed class ShotVariationValues
    {
        /// <summary>Off: the basic shot sounds as this morning (one sound, the asset's pitch spread, its volume).</summary>
        public bool On = true;
        /// <summary>The variations, one entry each; their count is the shortest of the three.</summary>
        public float[] Brightness = { 22000f, 9000f, 15000f, 6000f }; // Hz: the low-pass on the voice (22000: open, the recording as it is)
        public float[] Pitch = { 1f, 0.95f, 1.04f, 0.98f };           // the body's pitch, times the look's
        public float[] Tail = { 1f, 0.75f, 0.9f, 0.6f };              // the share of the clip's length that sounds before it is faded out
        public float TailFade = 0.04f;          // s the fade at the end of a shortened tail takes
        public float PitchJitter = 0.04f;       // each shot's pitch moves by up to this share up or down (instead of the asset's 0.05)
        public float VolumeJitterDb = 1.5f;     // and its volume by up to this many decibels
        public float GainDb = -3f;              // the basic shot this much quieter than this morning, so the skills stand out
        public string ClipFolder = "Sounds/Shots/BasicShot"; // Resources path of the clips' names, "_1", "_2" ... after it
    }

    /// <summary>One variation of a shot's sound as it plays (work order 064).</summary>
    public struct ShotVariation
    {
        public int Index;
        public float Cutoff;   // Hz of the low-pass; at 20000 or more, none
        public float Pitch;    // times the look's pitch, with its jitter
        public float Volume;   // times the look's volume, with its jitter and the gain
        public float Tail;     // the share of the clip that sounds; 1 the whole
        public float TailFade; // s
    }

    /// <summary>
    /// Picks the basic shot's variation, never the same twice in a row, from a stream of its own seeded for tests
    /// (<see cref="Seed"/>), with its jitter (work order 064). Nothing is allocated per shot.
    /// </summary>
    public sealed class ShotVariationPicker
    {
        public const string Stage = "shot-variation";
        LevelRandom _rng = LevelRandom.Stream(0UL, Stage);
        int _last = -1;

        /// <summary>The index the last pick gave, or -1.</summary>
        public int Last => _last;

        public void Seed(ulong seed)
        {
            _rng = LevelRandom.Stream(seed, Stage);
            _last = -1;
        }

        /// <summary>How many variations the values hold.</summary>
        public static int Count(ShotVariationValues v) =>
            v == null || v.Brightness == null || v.Pitch == null || v.Tail == null ? 0 : Math.Min(v.Brightness.Length, Math.Min(v.Pitch.Length, v.Tail.Length));

        /// <summary>The next index among <paramref name="count"/>: any but the last one (with one, always 0).</summary>
        public int NextIndex(int count)
        {
            if (count <= 1)
            {
                _last = 0;
                return 0;
            }
            int pick = _last < 0 || _last >= count ? _rng.Below(count) : (_last + 1 + _rng.Below(count - 1)) % count;
            _last = pick;
            return pick;
        }

        /// <summary>A uniform number from -1 to 1 off the stream.</summary>
        float Signed() => (float)((_rng.NextBits() >> 11) * (1.0 / (1UL << 53)) * 2.0 - 1.0);

        /// <summary>The next variation: its index, its recipe, and this shot's jitter on pitch and volume.</summary>
        public ShotVariation Next(ShotVariationValues v)
        {
            int count = Count(v);
            int i = NextIndex(Math.Max(1, count));
            float pitchJitter = 1f + Math.Max(0f, v != null ? v.PitchJitter : 0f) * Signed();
            float db = (v != null ? v.GainDb : 0f) + Math.Max(0f, v != null ? v.VolumeJitterDb : 0f) * Signed();
            return new ShotVariation
            {
                Index = i,
                Cutoff = count > 0 ? v.Brightness[i] : 22000f,
                Pitch = (count > 0 ? v.Pitch[i] : 1f) * pitchJitter,
                Volume = DbToGain(db),
                Tail = count > 0 ? Math.Max(0.05f, Math.Min(1f, v.Tail[i])) : 1f,
                TailFade = v != null ? Math.Max(0f, v.TailFade) : 0f,
            };
        }

        /// <summary>A level in decibels as a gain: -3 dB is about 0.708.</summary>
        public static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);
    }
}
