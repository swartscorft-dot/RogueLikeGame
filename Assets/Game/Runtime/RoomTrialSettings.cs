using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The rooms' trials on the settings screen's Trials page (work order 135; Kristian ticked on 2026-11-26 that the rooms' switches
    /// stand on the Trials page, all on when he comes home): "Line-ups" on (the default, nothing stored) or off, kept between sessions in
    /// PlayerPrefs under <see cref="LineUpsKey"/>, as the timing cue is (<see cref="TimingCueSettings"/>). Read where a floor is built
    /// (<see cref="InUse"/>, <see cref="GeneratedLevels"/>): off, the floors built after it are version 24's (<see cref="LevelValues.LineUps"/>);
    /// the floor she stands on stays as it was built.
    ///
    /// Read as the game's other settings are: nothing until <see cref="GameSettings.Load"/> (the HUD does it when it starts), so a test that
    /// never makes the HUD plays with the default whatever this machine has stored, and <see cref="GameSettings.Unload"/> puts the default
    /// back. A class of its own, not lines in GameSettings.cs: it follows GameSettings' own load by <see cref="GameSettings.IsLoaded"/>.
    /// </summary>
    public static class RoomTrialSettings
    {
        public const string LineUpsKey = "Roguelike.Settings.LineUps"; // work order 135: 1 on (the default, as nothing stored), 0 off

        static bool s_loaded;
        static bool s_lineUps = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_lineUps = true;
        }

        /// <summary>Follows GameSettings: read when it has loaded, the default again when it has unloaded.</summary>
        static void Follow()
        {
            if (GameSettings.IsLoaded && !s_loaded)
            {
                s_lineUps = PlayerPrefs.GetInt(LineUpsKey, 1) != 0;
                s_loaded = true;
            }
            else if (!GameSettings.IsLoaded && s_loaded)
            {
                s_loaded = false;
                s_lineUps = true;
            }
        }

        /// <summary>The line-ups as chosen on the Trials page (on unless he turned them off).</summary>
        public static bool LineUpsChosen
        {
            get
            {
                Follow();
                return s_lineUps;
            }
        }

        /// <summary>Whether a floor built now with these values has line-ups: their switch and the page's choice both on.</summary>
        public static bool InUse(LevelValues values) => (values == null || values.LineUps) && LineUpsChosen;

        /// <summary>Turns the line-ups on or off and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void SetLineUps(bool on, bool keep = true)
        {
            Follow();
            s_lineUps = on;
            if (!keep) return;
            PlayerPrefs.SetInt(LineUpsKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>The settings screen's line changed a step: "Line-ups" on or off. False for any other line.</summary>
        public static bool ChangeLine(SettingLine line, int delta)
        {
            if (line != SettingLine.LineUps) return false;
            SetLineUps(!LineUpsChosen);
            return true;
        }

        /// <summary>The words of the line's value now ("on", "off"): made once, never in a frame.</summary>
        public static string LineWords(SettingLine line) => RoomTrialLines.Words(LineUpsChosen);
    }
}
