using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 144's line on the settings screen's Trials page, "Sight opening" (<see cref="SightLines"/>): the brackets travel
    /// and the pointer stays (variant P, the default, <see cref="SightValues.PointerStays"/>), or the whole ring spreads (his
    /// sheet's sixth drawing), kept between sessions in PlayerPrefs under <see cref="Key"/> as work order 143's Precision is
    /// (<see cref="PrecisionSettings"/>), read once <see cref="GameSettings"/> has loaded.
    ///
    /// The opening in use (<see cref="PointerStaysNow"/>): with nobody at the keys (<see cref="PlayerInputDriver.ReadRealDevices"/>
    /// off: every test, the benchmark) the switch's own value, so a test sets <see cref="SightValues.PointerStays"/>; with a player
    /// at the keys the stored one, or the switch's own when none is stored.
    /// </summary>
    public static class SightSettings
    {
        public const string Key = "Roguelike.Settings.SightOpening"; // work order 144: 1 the pointer stays (brackets), 0 the whole ring; none stored: SightValues.PointerStays

        static bool s_loaded;
        static int s_stored = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_stored = -1;
            SightValues.Live = new SightValues(); // made anew with each Play session, as the interface's values
        }

        /// <summary>Follows GameSettings: read when it has loaded, none stored again when it has unloaded.</summary>
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

        /// <summary>The opening chosen (stored, or the switch's own): the Trials page's value.</summary>
        public static bool PointerStaysChosen
        {
            get
            {
                Follow();
                return SightLines.PointerStays(s_stored);
            }
        }

        /// <summary>The opening in use now (see the class).</summary>
        public static bool PointerStaysNow => PlayerInputDriver.ReadRealDevices ? PointerStaysChosen : SightValues.PointerStays;

        /// <summary>Chooses the opening and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void Choose(bool pointerStays, bool keep = true)
        {
            Follow();
            s_stored = pointerStays ? 1 : 0;
            if (!keep) return;
            PlayerPrefs.SetInt(Key, s_stored);
            PlayerPrefs.Save();
        }

        /// <summary>Is this the order's line on the Trials page.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.SightOpening;

        /// <summary>The settings screen's line changed a step: the other opening, kept. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (!Is(line)) return false;
            Choose(!PointerStaysChosen);
            return true;
        }

        /// <summary>The words of the line's value now (constant strings: nothing made in a frame).</summary>
        public static string LineWords => SightLines.WordsOf(PointerStaysChosen);
    }
}
