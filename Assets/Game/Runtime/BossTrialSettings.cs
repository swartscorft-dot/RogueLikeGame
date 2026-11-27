using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 147's line on the settings screen's Trials page, "Boss": "Trial" (the default) or "As before", kept between sessions
    /// in PlayerPrefs under <see cref="Key"/> as work order 143's Precision is, read once <see cref="GameSettings"/> has loaded.
    ///
    /// The trial fight is in use (<see cref="InUse"/>) with <see cref="BossValues.Trial"/> on and the line on "Trial", for a player at
    /// the keys. With nobody at the keys (<see cref="PlayerInputDriver.ReadRealDevices"/> off: every test, which drives her through her
    /// intents, and the benchmark) it is in use only when a test asks (<see cref="ForTests"/>): so every older test meets work order
    /// 146's boss, field for field. Read by the boss once, as it wakes from its making (Awake).
    /// </summary>
    public static class BossTrialSettings
    {
        public const string Key = "Roguelike.Settings.BossFight"; // work order 147: 1 Trial, 0 As before; none stored: Trial
        /// <summary>The line's name and its two words.</summary>
        public const string Name = "Boss", TrialWords = "Trial", BeforeWords = "As before";

        /// <summary>A test's choice, whoever is at the keys; null: none.</summary>
        public static bool? ForTests;

        static bool s_loaded;
        static int s_stored = -1;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_stored = -1;
            ForTests = null;
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

        /// <summary>The line's value: the trial (stored, or the default), whatever the switch and the keys.</summary>
        public static bool Chosen
        {
            get
            {
                Follow();
                return s_stored != 0;
            }
        }

        /// <summary>Is the trial fight in use now (see the class)?</summary>
        public static bool InUse =>
            BossValues.Trial && (ForTests.HasValue ? ForTests.Value : PlayerInputDriver.ReadRealDevices && Chosen);

        /// <summary>Chooses and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void Choose(bool trial, bool keep = true)
        {
            Follow();
            s_stored = trial ? 1 : 0;
            if (!keep) return;
            PlayerPrefs.SetInt(Key, s_stored);
            PlayerPrefs.Save();
        }

        /// <summary>Is this the order's line on the Trials page.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.BossFight;

        /// <summary>The settings screen's line changed a step (either way: there are two): the other value, kept. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (!Is(line) || delta == 0) return false;
            Choose(!Chosen);
            return true;
        }

        /// <summary>The words of the line's value now (constants: nothing made in a frame).</summary>
        public static string LineWords => Chosen ? TrialWords : BeforeWords;
    }
}
