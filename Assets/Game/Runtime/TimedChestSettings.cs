using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 153's line on the settings screen's ROOMS page, "Timed chest" (<see cref="TimedChestLines"/>): on (the default, nothing
    /// stored) or off, kept between sessions in PlayerPrefs under <see cref="Key"/> as the rooms' other trials are
    /// (<see cref="RoomTrialSettings"/>), read once <see cref="GameSettings"/> has loaded. Read as a floor's fights are set up
    /// (<see cref="InUse"/>): off, the floors set up after it hold no timed chest; the floor she stands on stays as it was set up.
    /// </summary>
    public static class TimedChestSettings
    {
        public const string Key = "Roguelike.Settings.TimedChest"; // work order 153: 1 on (the default, as nothing stored), 0 off

        static bool s_loaded;
        static bool s_on = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_on = true;
        }

        /// <summary>Follows GameSettings: read when it has loaded, the default again when it has unloaded.</summary>
        static void Follow()
        {
            if (GameSettings.IsLoaded && !s_loaded)
            {
                s_on = PlayerPrefs.GetInt(Key, 1) != 0;
                s_loaded = true;
            }
            else if (!GameSettings.IsLoaded && s_loaded)
            {
                s_loaded = false;
                s_on = true;
            }
        }

        /// <summary>The timed chest as chosen on the ROOMS page (on unless he turned it off).</summary>
        public static bool Chosen
        {
            get
            {
                Follow();
                return s_on;
            }
        }

        /// <summary>Whether a floor set up now with these values has a timed chest: its switch, its numbers and the page's choice all on.</summary>
        public static bool InUse(LevelValues values) => TimedChestRules.On(values ?? new LevelValues()) && Chosen;

        /// <summary>Turns it on or off and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void Set(bool on, bool keep = true)
        {
            Follow();
            s_on = on;
            if (!keep) return;
            PlayerPrefs.SetInt(Key, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Is this the order's line on the ROOMS page.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.TimedChest;

        /// <summary>The settings screen's line changed a step: on or off, kept. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (!Is(line)) return false;
            Set(!Chosen);
            return true;
        }

        /// <summary>The words of the line's value now (constant strings: nothing made in a frame).</summary>
        public static string LineWords => TimedChestLines.Words(Chosen);
    }
}
