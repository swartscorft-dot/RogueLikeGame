using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The setting of work order 134, <c>TimingCue</c> (<see cref="TimingCues.Names"/>: "Bar only", the default until he has
    /// chosen, "Cylinder", "Ring", "Hammer") and <c>TimingSound</c> on or off (on by default with any cue but "Bar only"), kept
    /// between sessions in PlayerPrefs under <see cref="CueKey"/> and <see cref="SoundKey"/> as the camera angle is
    /// (<see cref="GameSettings.CameraAngleKey"/>). A plain static way to read and set it, as <see cref="GameSettings.CameraNow"/>:
    /// <see cref="CueNow"/>, <see cref="SoundNow"/>, <see cref="SetCue"/>, <see cref="SetSound"/>. The menus are being rebuilt by
    /// work order 128 in another folder: no menu line is this order's; the producer puts the lines on 128's Trials page at the
    /// merge. Until then the temporary key steps through the cues (<see cref="TimingAtTheSight.StepKey"/>).
    ///
    /// Read as the game's other settings are: nothing until <see cref="GameSettings.Load"/> (the HUD does it when it starts), so a
    /// test that never makes the HUD plays with the defaults whatever this machine has stored, and <see cref="GameSettings.Unload"/>
    /// puts the defaults back. It is a class of its own, not lines in GameSettings.cs, so the merge with work order 128 touches
    /// nothing of that file: it follows GameSettings' own load by looking at <see cref="GameSettings.IsLoaded"/>.
    /// </summary>
    public static class TimingCueSettings
    {
        public const string CueKey = "Roguelike.Settings.TimingCue";     // work order 134: 0 bar only (the default), 1 cylinder, 2 ring, 3 hammer (TimingCues)
        public const string SoundKey = "Roguelike.Settings.TimingSound"; // work order 134: -1 none stored (on with any cue but the bar only), 0 off, 1 on

        static bool s_loaded;
        static int s_cue = (int)TimingCues.Default;
        static int s_sound = TimingCues.SoundNotStored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_cue = (int)TimingCues.Default;
            s_sound = TimingCues.SoundNotStored;
            TimingAtTheSightValues.Live = new TimingAtTheSightValues(); // made anew with each Play session, as the interface's values
        }

        /// <summary>Follows GameSettings: read when it has loaded, the defaults again when it has unloaded.</summary>
        static void Follow()
        {
            if (GameSettings.IsLoaded && !s_loaded)
            {
                s_cue = (int)TimingCues.Step(PlayerPrefs.GetInt(CueKey, (int)TimingCues.Default));
                s_sound = TimingCues.StoredSound(PlayerPrefs.GetInt(SoundKey, TimingCues.SoundNotStored));
                s_loaded = true;
            }
            else if (!GameSettings.IsLoaded && s_loaded)
            {
                s_loaded = false;
                s_cue = (int)TimingCues.Default;
                s_sound = TimingCues.SoundNotStored;
            }
        }

        /// <summary>The cue chosen, as stored (whatever the order's switch says).</summary>
        public static TimingCue CueChosen
        {
            get
            {
                Follow();
                return TimingCues.Step(s_cue);
            }
        }

        /// <summary>The sound's stored value: <see cref="TimingCues.SoundNotStored"/>, <see cref="TimingCues.SoundOff"/> or <see cref="TimingCues.SoundOn"/>.</summary>
        public static int SoundStored
        {
            get
            {
                Follow();
                return s_sound;
            }
        }

        /// <summary>The cue in use: the one chosen, or the bar only with the order's switch off.</summary>
        public static TimingCue CueNow => TimingCues.InUse(CueChosen);

        /// <summary>The click in use: the stored value or the cue's default; never with the order's switch off.</summary>
        public static bool SoundNow => TimingCues.SoundInUse(SoundStored, CueChosen);

        /// <summary>Chooses the cue and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void SetCue(TimingCue cue, bool keep = true)
        {
            Follow();
            s_cue = (int)TimingCues.Step((int)cue);
            if (!keep) return;
            PlayerPrefs.SetInt(CueKey, s_cue);
            PlayerPrefs.Save();
        }

        /// <summary>Turns the click on or off and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void SetSound(bool on, bool keep = true)
        {
            Follow();
            s_sound = on ? TimingCues.SoundOn : TimingCues.SoundOff;
            if (!keep) return;
            PlayerPrefs.SetInt(SoundKey, s_sound);
            PlayerPrefs.Save();
        }

        /// <summary>Forgets a stored sound value: its default follows the cue again (a test keeps nothing).</summary>
        public static void ForgetSound(bool keep = true)
        {
            Follow();
            s_sound = TimingCues.SoundNotStored;
            if (!keep) return;
            PlayerPrefs.DeleteKey(SoundKey);
            PlayerPrefs.Save();
        }
    }
}
