using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Music on or off, the music's volume and the toon look, saved like every other setting (the follow-up to work order 128: he
    /// answered "yes, save them" to the question of the report). Until then the three lasted only until the game stopped. They are
    /// kept in PlayerPrefs beside <see cref="GameSettings"/>' under their own keys, saved when a line changes them once they were read (the Esc menu's
    /// or the settings screen's: the lines' own code tells <see cref="Keep"/>), and read when Play starts, as the graphics
    /// settings are (<see cref="GraphicsChoices"/>: never in a test run, never with <see cref="SettingsScreenValues.On"/> off). With
    /// nothing saved, today's defaults stand: the music on at 100 %, the toon look on.
    /// </summary>
    public static class SavedMusicAndLook
    {
        public const string MusicKey = "Roguelike.Settings.Music";             // 1 on (the default), 0 off
        public const string MusicVolumeKey = "Roguelike.Settings.MusicVolume"; // the step, 0 to 10 (10 the default)
        public const string ToonLookKey = "Roguelike.Settings.ToonLook";       // 1 on (the default), 0 off

        /// <summary>Every key it may write, for tests that save and restore them.</summary>
        public static readonly string[] Keys = { MusicKey, MusicVolumeKey, ToonLookKey };

        static bool s_loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_loaded = false;

        /// <summary>
        /// The saved values were read (by itself when Play starts, or by a test that saves and restores the keys): only then does a
        /// change write. A test run never reads them, so the many tests that flip the music or the toon look never write his.
        /// </summary>
        public static bool IsLoaded => s_loaded;

        /// <summary>Forgets that they were read: changes write nothing until the next <see cref="Load"/>. For tests.</summary>
        public static void Unload() => s_loaded = false;

        /// <summary>Puts the saved values on the session's music values and the toon look's switch; a key not saved leaves today's default.</summary>
        public static void Load()
        {
            if (!SettingsScreenValues.On) return;
            MusicValues m = MusicPlayer.Values ?? (MusicPlayer.Values = new MusicValues());
            if (PlayerPrefs.HasKey(MusicKey)) m.On = PlayerPrefs.GetInt(MusicKey, 1) != 0;
            if (PlayerPrefs.HasKey(MusicVolumeKey)) m.VolumeStep = SettingsRules.VolumeStep(PlayerPrefs.GetInt(MusicVolumeKey, SettingsRules.DefaultVolumeStep));
            if (PlayerPrefs.HasKey(ToonLookKey)) ToonLookValues.On = PlayerPrefs.GetInt(ToonLookKey, 1) != 0;
            s_loaded = true;
        }

        /// <summary>The three as they stand now, saved (a line changed one of them).</summary>
        public static void Keep()
        {
            if (!SettingsScreenValues.On || !s_loaded) return;
            MusicValues m = MusicPlayer.Values;
            if (m != null)
            {
                PlayerPrefs.SetInt(MusicKey, m.On ? 1 : 0);
                PlayerPrefs.SetInt(MusicVolumeKey, SettingsRules.VolumeStep(m.VolumeStep));
            }
            PlayerPrefs.SetInt(ToonLookKey, ToonLookValues.On ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
