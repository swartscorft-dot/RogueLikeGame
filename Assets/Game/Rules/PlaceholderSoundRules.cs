using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The placeholder sounds (work order 021, 2026-11-20): what makes heard what already happens, until real sounds
    /// replace them (the cut list: "music and finished audio beyond placeholders" wait until after the demo).
    /// </summary>
    public enum PlaceholderSound
    {
        EnemyHit,      // her shot or skill damaged a target
        EnemyDies,     // an enemy's health reached 0
        WindupRed,     // an enemy began the windup of an attack
        WindupBlue,    // the same, of a counterable attack
        Strike,        // an enemy's strike landed, whether or not it hit her
        Arrow,         // the shooter's shot was let go
        Hurt,          // she took damage
        HerDeath,      // her health reached 0
        Dash,          // she dashed
        Dazed,         // an enemy was dazed: its bar emptied, or a counter
        BossCharge,    // the boss began to run its charge
        BossAnger,     // the boss turned angry
        BossCall,      // the boss called a pack
        ChestOpens,    // a chest was opened
        ChoiceTaken,   // a floor reward or a chest's upgrade was picked
        FloorCleared,  // the last enemy of the exit room died
        ActCleared,    // the act was cleared
        Menu,          // the pause menu opened, closed, or its line moved
        FocusFull,     // her focus gauge filled
    }

    /// <summary>
    /// What the game reports that a placeholder sound answers (work order 021): the moments, named for what happened,
    /// not for the sound. The game turns its own reports into these; <see cref="PlaceholderSoundRules.SoundFor"/> picks the sound.
    /// </summary>
    public enum SoundMoment
    {
        EnemyHit, EnemyDied, WindupStarted, EnemyStruck, EnemyDazed, BossCharged, BossAngered, BossCalled,
        HerHurt, HerDied, Dashed, ChestOpened, ChoiceTaken, FloorCleared, ActCleared, MenuOpened, MenuClosed, MenuMoved, FocusFilled,
    }

    /// <summary>One sound's volume and cap: one line of <see cref="PlaceholderSoundValues.Sounds"/>.</summary>
    [Serializable]
    public struct PlaceholderSoundSetting
    {
        public PlaceholderSound Sound;
        /// <summary>The share of the basic shot's volume it plays at (the basic shot is the loudest thing in the game).</summary>
        public float Volume;
        /// <summary>At most this many plays of it in any one second; more are dropped.</summary>
        public int PerSecond;

        public PlaceholderSoundSetting(PlaceholderSound sound, float volume, int perSecond)
        {
            Sound = sound;
            Volume = volume;
            PerSecond = perSecond;
        }
    }

    /// <summary>
    /// The numbers of the placeholder sounds (work order 021, 2026-11-20), all first guesses, with their defaults here in
    /// code: no asset holds them. Quiet beside the guns: every sound starts between a fifth and a half of the basic
    /// shot's volume, the hit tick lowest, the boss and her own pain highest. The pause menu's volume setting scales all
    /// of it through the listener, as it does the guns; there is no second setting.
    /// </summary>
    [Serializable]
    public sealed class PlaceholderSoundValues
    {
        /// <summary>Off: no placeholder sound at all.</summary>
        public bool On = true;
        /// <summary>Over every placeholder's own volume.</summary>
        public float Master = 1f;
        /// <summary>Each play's pitch moves by up to this share up or down (0.04 = 4 %), so a crowd does not tick like a machine.</summary>
        public float PitchSpread = 0.04f;
        /// <summary>Several of one sound in one frame are one play, this much louder for each one more...</summary>
        public float MergeStep = 0.15f;
        /// <summary>...but never more than this many times its volume.</summary>
        public float MergeMost = 1.5f;
        /// <summary>Her hurt sound for the smallest hit plays at this share of its volume...</summary>
        public float HurtSmallest = 0.65f;
        /// <summary>...and at full for a hit of this share of her maximum health or more.</summary>
        public float HurtFullAt = 0.25f;

        /// <summary>Each sound's volume and cap per second.</summary>
        public PlaceholderSoundSetting[] Sounds = Defaults();

        public static PlaceholderSoundSetting[] Defaults() => new[]
        {
            new PlaceholderSoundSetting(PlaceholderSound.EnemyHit, 0.2f, 12),
            new PlaceholderSoundSetting(PlaceholderSound.EnemyDies, 0.3f, 6),
            new PlaceholderSoundSetting(PlaceholderSound.WindupRed, 0.35f, 4),
            new PlaceholderSoundSetting(PlaceholderSound.WindupBlue, 0.4f, 4),
            new PlaceholderSoundSetting(PlaceholderSound.Strike, 0.35f, 6),
            new PlaceholderSoundSetting(PlaceholderSound.Arrow, 0.3f, 6),
            new PlaceholderSoundSetting(PlaceholderSound.Hurt, 0.5f, 8),
            new PlaceholderSoundSetting(PlaceholderSound.HerDeath, 0.45f, 2),
            new PlaceholderSoundSetting(PlaceholderSound.Dash, 0.25f, 6),
            new PlaceholderSoundSetting(PlaceholderSound.Dazed, 0.35f, 6),
            new PlaceholderSoundSetting(PlaceholderSound.BossCharge, 0.5f, 3),
            new PlaceholderSoundSetting(PlaceholderSound.BossAnger, 0.5f, 2),
            new PlaceholderSoundSetting(PlaceholderSound.BossCall, 0.5f, 2),
            new PlaceholderSoundSetting(PlaceholderSound.ChestOpens, 0.35f, 3),
            new PlaceholderSoundSetting(PlaceholderSound.ChoiceTaken, 0.3f, 4),
            new PlaceholderSoundSetting(PlaceholderSound.FloorCleared, 0.4f, 2),
            new PlaceholderSoundSetting(PlaceholderSound.ActCleared, 0.45f, 2),
            new PlaceholderSoundSetting(PlaceholderSound.Menu, 0.25f, 20),
            new PlaceholderSoundSetting(PlaceholderSound.FocusFull, 0.3f, 2),
        };

        /// <summary>The line for a sound: its own if listed, else its default.</summary>
        public PlaceholderSoundSetting Setting(PlaceholderSound sound)
        {
            if (Sounds != null)
                foreach (PlaceholderSoundSetting s in Sounds)
                    if (s.Sound == sound) return s;
            foreach (PlaceholderSoundSetting s in Defaults())
                if (s.Sound == sound) return s;
            return new PlaceholderSoundSetting(sound, 0.2f, 4);
        }

        public float Volume(PlaceholderSound sound) => Setting(sound).Volume;
        public int PerSecond(PlaceholderSound sound) => Setting(sound).PerSecond;
    }

    /// <summary>The rules of the placeholder sounds (work order 021, 2026-11-20), with no engine in them.</summary>
    public static class PlaceholderSoundRules
    {
        /// <summary>Every sound, in order.</summary>
        public static readonly PlaceholderSound[] All = (PlaceholderSound[])Enum.GetValues(typeof(PlaceholderSound));

        /// <summary>
        /// The Resources path a recorded clip must have to be played in place of a synthesised sound: the folder
        /// "Sounds/Placeholders" under any Resources folder, and the sound's name ("Sounds/Placeholders/EnemyHit").
        /// </summary>
        public const string ResourceFolder = "Sounds/Placeholders";

        public static string ResourcePath(PlaceholderSound sound) => ResourceFolder + "/" + sound;

        /// <summary>
        /// The sound a moment gives, or none. A windup is blue for a counterable attack (pillar 4: a windup she can hear is
        /// one she can dodge looking elsewhere, and the blue one she can counter); the shooter's strike is its arrow let go.
        /// An ordinary attack's windup (work order 035: a melee enemy's attack in reach on a short cycle) has no sound, so
        /// that the windup sounds still mean a skill; its strike has the strike's sound as every strike does.
        /// </summary>
        public static PlaceholderSound? SoundFor(SoundMoment moment, EnemyKind kind = EnemyKind.Brute, bool counterable = false, bool ordinary = false)
        {
            switch (moment)
            {
                case SoundMoment.EnemyHit: return PlaceholderSound.EnemyHit;
                case SoundMoment.EnemyDied: return PlaceholderSound.EnemyDies;
                case SoundMoment.WindupStarted:
                    if (ordinary) return null;
                    return counterable ? PlaceholderSound.WindupBlue : PlaceholderSound.WindupRed;
                case SoundMoment.EnemyStruck: return kind == EnemyKind.Shooter ? PlaceholderSound.Arrow : PlaceholderSound.Strike;
                case SoundMoment.EnemyDazed: return PlaceholderSound.Dazed;
                case SoundMoment.BossCharged: return PlaceholderSound.BossCharge;
                case SoundMoment.BossAngered: return PlaceholderSound.BossAnger;
                case SoundMoment.BossCalled: return PlaceholderSound.BossCall;
                case SoundMoment.HerHurt: return PlaceholderSound.Hurt;
                case SoundMoment.HerDied: return PlaceholderSound.HerDeath;
                case SoundMoment.Dashed: return PlaceholderSound.Dash;
                case SoundMoment.ChestOpened: return PlaceholderSound.ChestOpens;
                case SoundMoment.ChoiceTaken: return PlaceholderSound.ChoiceTaken;
                case SoundMoment.FloorCleared: return PlaceholderSound.FloorCleared;
                case SoundMoment.ActCleared: return PlaceholderSound.ActCleared;
                case SoundMoment.MenuOpened:
                case SoundMoment.MenuClosed:
                case SoundMoment.MenuMoved: return PlaceholderSound.Menu;
                case SoundMoment.FocusFilled: return PlaceholderSound.FocusFull;
                default: return null;
            }
        }

        /// <summary>
        /// Paused is silent: while the pause menu or a choice is up nothing new starts, except the menu's tick and the
        /// choice-taken chord, which answer what she does on those screens.
        /// </summary>
        public static bool PlaysWhilePaused(PlaceholderSound sound) => sound == PlaceholderSound.Menu || sound == PlaceholderSound.ChoiceTaken;

        public static bool MayStart(PlaceholderSound sound, bool paused) => !paused || PlaysWhilePaused(sound);

        /// <summary>
        /// Louder for a bigger hit: her hurt sound's share of its volume for a hit of <paramref name="amount"/> on a
        /// maximum of <paramref name="max"/>, from <see cref="PlaceholderSoundValues.HurtSmallest"/> for the smallest to
        /// 1 at <see cref="PlaceholderSoundValues.HurtFullAt"/> of her maximum.
        /// </summary>
        public static float HurtGain(int amount, int max, PlaceholderSoundValues values)
        {
            float smallest = Clamp01(values.HurtSmallest);
            if (amount <= 0) return smallest;
            float full = Math.Max(1f, values.HurtFullAt * Math.Max(1, max));
            return smallest + (1f - smallest) * Clamp01(amount / full);
        }

        /// <summary>Several of one sound in one frame, as one: this many times its volume (1 for one, never over <paramref name="most"/>).</summary>
        public static float Merged(int count, float step, float most) =>
            count <= 1 ? 1f : Math.Min(Math.Max(1f, most), 1f + Math.Max(0f, step) * (count - 1));

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>One play the crowd rule lets through: the sound, how many asked for it this frame, and its gain.</summary>
    public struct SoundPlay
    {
        public PlaceholderSound Sound;
        public int Asked;
        /// <summary>Times its volume: the loudest of those that asked (her hurt sound for a bigger hit), louder for a merge.</summary>
        public float Gain;
    }

    /// <summary>
    /// The crowd rule (work order 021, 2026-11-20): a room holds up to twenty-four enemies, and they must not roar. Per
    /// sound: all that ask for it in one frame are one play, a little louder (<see cref="PlaceholderSoundRules.Merged"/>);
    /// and at most <see cref="PlaceholderSoundSetting.PerSecond"/> plays in any one second, the rest dropped. Asks are
    /// gathered through the frame and let through once, at its end (<see cref="Flush"/>). Time is passed in, in seconds.
    /// </summary>
    public sealed class SoundCrowd
    {
        readonly int[] _asked = new int[PlaceholderSoundRules.All.Length];
        readonly float[] _gain = new float[PlaceholderSoundRules.All.Length];
        readonly List<float>[] _played = new List<float>[PlaceholderSoundRules.All.Length];
        readonly List<SoundPlay> _out = new List<SoundPlay>();

        public SoundCrowd()
        {
            for (int i = 0; i < _played.Length; i++) _played[i] = new List<float>();
        }

        /// <summary>A sound is asked for this frame, at <paramref name="gain"/> times its volume.</summary>
        public void Ask(PlaceholderSound sound, float gain = 1f)
        {
            int i = (int)sound;
            if (i < 0 || i >= _asked.Length) return;
            _gain[i] = _asked[i] == 0 ? gain : Math.Max(_gain[i], gain);
            _asked[i]++;
        }

        /// <summary>Is anything asked for and not yet let through?</summary>
        public bool Waiting
        {
            get
            {
                foreach (int n in _asked) if (n > 0) return true;
                return false;
            }
        }

        /// <summary>
        /// The end of a frame at <paramref name="now"/>: one play for each sound asked for, unless it has played its cap
        /// in the second before. The list is reused by the next call.
        /// </summary>
        public IReadOnlyList<SoundPlay> Flush(float now, PlaceholderSoundValues values)
        {
            _out.Clear();
            for (int i = 0; i < _asked.Length; i++)
            {
                int asked = _asked[i];
                if (asked == 0) continue;
                _asked[i] = 0;
                var sound = (PlaceholderSound)i;
                List<float> played = _played[i];
                played.RemoveAll(t => now - t >= 1f - 1e-4f || t > now); // a second, less a hair for the clock's rounding
                if (played.Count >= Math.Max(0, values.PerSecond(sound))) continue;
                played.Add(now);
                _out.Add(new SoundPlay
                {
                    Sound = sound, Asked = asked,
                    Gain = _gain[i] * PlaceholderSoundRules.Merged(asked, values.MergeStep, values.MergeMost),
                });
            }
            return _out;
        }

        /// <summary>Forgets what was asked and played.</summary>
        public void Clear()
        {
            Array.Clear(_asked, 0, _asked.Length);
            foreach (List<float> p in _played) p.Clear();
        }
    }

    /// <summary>
    /// Small differences (work order 021): each play's pitch moves by a few percent, from a seeded source, so twenty
    /// swarmers hit at once do not sound like a machine gun of the same tick. The same seed gives the same pitches.
    /// </summary>
    public sealed class PitchVariation
    {
        uint _state;

        public PitchVariation(uint seed) => _state = seed == 0u ? 0x2545F491u : seed;

        /// <summary>The next pitch: 1, moved by up to <paramref name="spread"/> up or down.</summary>
        public float Next(float spread)
        {
            _state ^= _state << 13;
            _state ^= _state >> 17;
            _state ^= _state << 5;
            double u = _state / (double)uint.MaxValue * 2.0 - 1.0; // -1 to 1
            float s = Math.Max(0f, spread);
            return (float)(1.0 + u * s);
        }
    }
}
