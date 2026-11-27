using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 149's line on the settings screen's Trials page, "Elites" (<see cref="EliteLines"/>): "Unique" (floor 1's Grave Ram,
    /// <see cref="EliteValues.Unique"/>) or "As before" (today's elite), kept between sessions in PlayerPrefs under <see cref="Key"/> as
    /// work order 144's sight opening is (<see cref="SightSettings"/>), read once <see cref="GameSettings"/> has loaded. It is read when a
    /// floor is built: a change shows from the next floor 1.
    ///
    /// The choice in use (<see cref="UniqueNow"/>): with nobody at the keys (<see cref="PlayerInputDriver.ReadRealDevices"/> off: every test,
    /// the benchmark) the switch's own value, so a test sets <see cref="EliteValues.Unique"/>; with a player at the keys the stored one, or
    /// the switch's own when none is stored.
    /// </summary>
    public static class EliteSettings
    {
        public const string Key = "Roguelike.Settings.Elites"; // work order 149: 1 unique, 0 as before; none stored: EliteValues.Unique

        static bool s_loaded;
        static int s_stored = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_stored = -1;
        }

        static void Follow()
        {
            if (GameSettings.IsLoaded && !s_loaded)
            {
                s_stored = PlayerPrefs.GetInt(Key, -1);
                s_loaded = true;
            }
            else if (!GameSettings.IsLoaded && s_loaded)
            {
                s_loaded = false;
                s_stored = -1;
            }
        }

        /// <summary>The choice (stored, or the switch's own): the Trials page's value.</summary>
        public static bool UniqueChosen
        {
            get
            {
                Follow();
                return EliteLines.Unique(s_stored);
            }
        }

        /// <summary>The choice in use now (see the class).</summary>
        public static bool UniqueNow => PlayerInputDriver.ReadRealDevices ? UniqueChosen : EliteValues.Unique;

        /// <summary>Chooses and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void Choose(bool unique, bool keep = true)
        {
            Follow();
            s_stored = unique ? 1 : 0;
            if (!keep) return;
            PlayerPrefs.SetInt(Key, s_stored);
            PlayerPrefs.Save();
        }

        /// <summary>Is this the order's line on the Trials page.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.Elites;

        /// <summary>The settings screen's line changed a step: the other choice, kept. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (!Is(line)) return false;
            Choose(!UniqueChosen);
            return true;
        }

        /// <summary>The words of the line's value now (constant strings: nothing made in a frame).</summary>
        public static string LineWords => EliteLines.WordsOf(UniqueChosen);
    }
}
