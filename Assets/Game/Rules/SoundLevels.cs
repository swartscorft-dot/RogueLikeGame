using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How loud the game is (work order 131). A friend of Kristian's, after his first run of the shared build (version 6,
    /// 78ae31b), 2026-11-25: "Lyden er helt sjukt høy første gang man logger inn, også er det forskjell i volum på de høye
    /// skytelydene". Measured with ffmpeg (ebur128's momentary loudness over 400 ms, its sample and true peak) on every clip as the
    /// code plays it, times the volume it is played at: at a full master volume her basic shot played at -11.9 LUFS momentary, her
    /// first minute of fight (the basic shot four times a second over the fight loop) at -9.7 LUFS integrated, and the loudest
    /// moment (three skills, a kill and the focus spend in one frame over that) peaked at +4.7 dBTP: over full scale, so it clipped.
    /// The start menu (the calm loop and the room tone) sat at -33.7 LUFS: the music is not the loud part. The shot recordings
    /// were levelled by their PEAKS in the ShotFx asset (the shotgun's 0.876 is the others' 0.65 lifted by its 2.6 dB lower peak),
    /// so by LOUDNESS they stood apart: the basic shot 1.1 dB OVER Quick Shot though meant 3 dB under it, Long Shot 3.6 dB under it
    /// though it is a heavy shot; 4.7 dB from the loudest to the quietest.
    ///
    /// Three things, all behind <see cref="On"/>: a master trim under the volume setting (<see cref="MasterTrimDb"/>); the volume
    /// step for someone who has never set one (<see cref="FirstStartVolumeStep"/>; a saved step is never changed); and a gain per
    /// shot recording, by its clip's name (<see cref="ClipGains"/>), so the shots sit together by loudness. No sound file is changed.
    /// First choices from the measurement: nobody has listened to them.
    /// </summary>
    [Serializable]
    public sealed class SoundLevelsValues
    {
        /// <summary>
        /// The order's switch (work order 131, ON), for the friend's words: "Lyden er helt sjukt høy første gang man logger inn, også
        /// er det forskjell i volum på de høye skytelydene". Off: no trim, the first start at the top step (10 of 10) as before, and
        /// every shot at the volume it had, field for field. One switch for the whole game, so it is static; tests set it and put it back.
        /// </summary>
        public static bool On = true;

        /// <summary>The values the game plays with (one set).</summary>
        public static readonly SoundLevelsValues Live = new SoundLevelsValues();

        /// <summary>
        /// dB under every volume step, on the listener (the game's own volume): -6 dB. Chosen from the measurement: the loudest
        /// moment measured (+4.7 dBTP with the shots levelled) comes to -1.3 dBTP at the top step, so it cannot clip; her ordinary
        /// fight (with the shots levelled, -14.7 LUFS at full) comes to -20.7 LUFS at the top step.
        /// </summary>
        public float MasterTrimDb = -6f;

        /// <summary>
        /// The volume step (0 to 10) for someone who has never set one: 8 (80 %, -1.9 dB). With the trim, her ordinary fight sits at
        /// -22.6 LUFS integrated (short-term at most -22.6) and the loudest moment peaks at -3.3 dBTP: about the -23 LUFS of
        /// broadcast loudness (EBU R 128) and within the consoles' -24 +-2, and two steps left above it for a player who wants it
        /// louder (at 10 of 10 the trim alone would leave none). Read only when no step is saved; a saved one is kept as it is.
        /// </summary>
        public int FirstStartVolumeStep = 8;

        /// <summary>
        /// The gain of each shot recording, in dB, by its clip's name (the file's name without its ending), over the volume the ShotFx
        /// asset gives its look. Each is (its role's target) minus (its loudness as played), both momentary loudness at a full master
        /// with no trim, measured with ffmpeg on 2026-11-25 (the clip from its look's start, at its pitch, stereo; the basic shot over
        /// its four variations, which differ by 0.3 dB among themselves, so they need no gain of their own). The roles, each with its
        /// reason: a skill's single shot -14.0 LUFS; the heavy guns 1 dB over it (the deagle and the shotgun: Long Shot, Heavy Volley,
        /// Scatter Blast are the heavy shots of work order 064's ShotWeight.Heavy; Powder Keg, Breaker Slug and Deadeye share their
        /// clips); a stream of shots 3 dB under it (the basic shot, four a second, its 3 dB under the skills kept from work order 064's
        /// GainDb, "so the skills stand out"; Fan Fire's ticks, six a second, the same). A clip not listed plays as it did.
        /// </summary>
        public ClipGain[] ClipGains =
        {
            new ClipGain("gunshot_54", -5.1f),     // the basic shot: -11.9 as played (0.55 x its -3 dB), target -17.0 (a stream, 3 dB under a skill)
            new ClipGain("gunshot_42", -1.0f),     // Quick Shot, Backstep, Triple Tap, Tag Round, Ricochet, Stopping Shot: -13.0 at 0.65, target -14.0
            new ClipGain("gunshot_6", -3.8f),      // Fan Fire's tick: -13.2 at 0.4, target -17.0 (a stream, six a second)
            new ClipGain("deagle_50ae", 3.6f),     // Long Shot, Heavy Volley, Deadeye: -16.6 at 0.65, target -13.0 (heavy); 0.65 becomes 0.98, under 1
            new ClipGain("shotgun_blast", 0.3f),   // Scatter Blast, Breaker Slug, Powder Keg (at 0.85 pitch): -13.3 at 0.876, target -13.0 (heavy)
        };
    }

    /// <summary>One shot recording's gain (work order 131): its clip's name and dB.</summary>
    [Serializable]
    public struct ClipGain
    {
        public string Clip;
        public float GainDb;

        public ClipGain(string clip, float gainDb)
        {
            Clip = clip;
            GainDb = gainDb;
        }
    }

    /// <summary>The rules of the sound's levels (work order 131), with no engine in them.</summary>
    public static class SoundLevels
    {
        /// <summary>A level in decibels as a linear gain: -6 dB is about 0.501.</summary>
        public static float DbToGain(float db) => (float)Math.Pow(10.0, db / 20.0);

        /// <summary>The master trim as a gain on the listener: 1 with the switch off.</summary>
        public static float Trim(SoundLevelsValues v = null) =>
            SoundLevelsValues.On ? DbToGain((v ?? SoundLevelsValues.Live).MasterTrimDb) : 1f;

        /// <summary>The listener's volume for a step (0 to 10): the step's own (<see cref="SettingsRules.Volume"/>) times the trim.</summary>
        public static float ListenerVolume(int step, SoundLevelsValues v = null) => SettingsRules.Volume(step) * Trim(v);

        /// <summary>
        /// The step for someone with none saved: <see cref="SoundLevelsValues.FirstStartVolumeStep"/>, or with the switch off the top
        /// step as before (<see cref="SettingsRules.DefaultVolumeStep"/>).
        /// </summary>
        public static int FirstStartStep(SoundLevelsValues v = null) =>
            SoundLevelsValues.On ? SettingsRules.VolumeStep((v ?? SoundLevelsValues.Live).FirstStartVolumeStep) : SettingsRules.DefaultVolumeStep;

        /// <summary>The step to use at a start: the saved one as it is (made safe, 0 to 10) when there is one, else the first start's.</summary>
        public static int StepAtStart(bool saved, int savedStep, SoundLevelsValues v = null) =>
            saved ? SettingsRules.VolumeStep(savedStep) : FirstStartStep(v);

        /// <summary>A clip's gain in dB (0 for a clip not listed, or none), whatever the switch.</summary>
        public static float ClipGainDb(string clip, SoundLevelsValues v = null)
        {
            ClipGain[] gains = (v ?? SoundLevelsValues.Live).ClipGains;
            if (string.IsNullOrEmpty(clip) || gains == null) return 0f;
            for (int i = 0; i < gains.Length; i++)
                if (string.Equals(gains[i].Clip, clip, StringComparison.Ordinal)) return gains[i].GainDb;
            return 0f;
        }

        /// <summary>The factor a clip's volume is played at: its gain with the switch on, 1 with it off. Nothing allocated.</summary>
        public static float ClipGainOf(string clip, SoundLevelsValues v = null) =>
            SoundLevelsValues.On ? DbToGain(ClipGainDb(clip, v)) : 1f;
    }
}
