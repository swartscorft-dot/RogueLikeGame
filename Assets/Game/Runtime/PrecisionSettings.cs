using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The setting of work order 143, <c>Precision</c> (<see cref="PrecisionMode"/>: Off, Inertia (the default, his tick), Meter,
    /// Ghost), kept between sessions in PlayerPrefs under <see cref="Key"/> as the timing cue is (<see cref="TimingCueSettings"/>),
    /// read once <see cref="GameSettings"/> has loaded; its line on the settings screen's Trials page (<see cref="ChangeLine"/>).
    ///
    /// The mode in use (<see cref="ModeNow"/>): Off with the order's switch off. With nobody at the keys
    /// (<see cref="PlayerInputDriver.ReadRealDevices"/> off: every test, which drives her through her intents, and the benchmark)
    /// it is Off unless a test gives one (<see cref="ForTests"/>): so every older test plays today's game field for field and the
    /// benchmark's numbers stay comparable, as work order 143 asks ("Off is today's game, field for field: every older test's
    /// set-up switches it off"). With a player at the keys, the stored one.
    /// </summary>
    public static class PrecisionSettings
    {
        public const string Key = "Roguelike.Settings.Precision"; // work order 143: 0 Off, 1 Inertia, 2 Meter, 3 Ghost; none stored: Inertia

        /// <summary>A test's mode, whatever is stored and whoever is at the keys; null: none.</summary>
        public static PrecisionMode? ForTests;

        static bool s_loaded;
        static int s_stored = -1;
        static readonly System.Func<PrecisionMode> s_Source = () => ModeNow;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_stored = -1;
            ForTests = null;
            PrecisionValues.Live = new PrecisionValues(); // made anew with each Play session, as the interface's values
            PrecisionValues.ModeSource = s_Source;          // the rules read the mode in use from here (her sheet)
        }

        /// <summary>Follows GameSettings: read when it has loaded, the default again when it has unloaded.</summary>
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

        /// <summary>The mode chosen (stored, or the default), whatever the switch and the keys: the Trials page's value.</summary>
        public static PrecisionMode Chosen
        {
            get
            {
                Follow();
                return PrecisionLines.Stored(s_stored);
            }
        }

        /// <summary>The mode in use now (see the class).</summary>
        public static PrecisionMode ModeNow =>
            !PrecisionValues.On ? PrecisionMode.Off
            : ForTests.HasValue ? ForTests.Value
            : PlayerInputDriver.ReadRealDevices ? Chosen : PrecisionMode.Off;

        /// <summary>Chooses the mode and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void Choose(PrecisionMode mode, bool keep = true)
        {
            Follow();
            s_stored = (int)mode;
            if (!keep) return;
            PlayerPrefs.SetInt(Key, s_stored);
            PlayerPrefs.Save();
        }

        /// <summary>Is this the order's line on the Trials page.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.Precision;

        /// <summary>The settings screen's line changed a step: the next mode (back for a step left), kept. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (!Is(line)) return false;
            Choose(PrecisionLines.Stepped(Chosen, delta));
            return true;
        }

        /// <summary>The words of the line's value now: made once, never in a frame.</summary>
        public static string LineWords => PrecisionLines.WordsOf(Chosen);
    }
}
