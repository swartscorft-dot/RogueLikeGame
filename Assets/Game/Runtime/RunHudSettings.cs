using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The Esc menu tidied (work order 128; Kristian, 2026-11-25: "også må du gjerne rydde opp i ESC menyen (fordele de på grafikk
    /// instillinger/lyd osv)"), as part of <see cref="RunHud"/>, with <see cref="SettingsScreenValues.On"/>: its top list is the
    /// actions only, Resume, New run (or Start a run), Same run again (in a run, under New run where it read best before),
    /// Settings, Leave to the practice room (in a floor) and Quit (in a built player); "Settings" opens the settings screen
    /// (<see cref="SettingsScreen"/>), where every setting line now stands on a page with its saved value and its key as before.
    /// The lines' own code (<see cref="ChangeSetting"/>, <see cref="MenuText"/>) is unchanged: the screen calls it. Off, the long
    /// list as before.
    /// </summary>
    public sealed partial class RunHud
    {
        static readonly string[] s_HintNoSettings = { "Up", "Down", "pick", "Enter", "use", "Esc", "resume" };
        static readonly bool[] s_HintNoSettingsCap = { true, true, false, true, false, true, false };

        /// <summary>The Esc menu's line of keys: without "&lt; &gt; change" when no setting stands in its list (the short list).</summary>
        string[] PauseHint => _firstSetting < 0 && SettingsScreenValues.On ? s_HintNoSettings : s_Hint;
        bool[] PauseHintCap => _firstSetting < 0 && SettingsScreenValues.On ? s_HintNoSettingsCap : s_HintCap;

        /// <summary>The short list, when the switch is on (false: the long list is built as before).</summary>
        bool BuildSettingsMenu()
        {
            if (!SettingsScreenValues.On) return false;
            _menu.Clear();
            _menu.Add(MenuLine.Resume);
            _menu.Add(MenuLine.Restart);
            if (_levels != null && _levels.InRun) _menu.Add(MenuLine.SameRun);
            _menu.Add(MenuLine.Settings);
            if (InFloor) _menu.Add(MenuLine.Leave);
            if (!UnityEngine.Application.isEditor) _menu.Add(MenuLine.Quit);
            if (Selected >= _menu.Count) Selected = _menu.Count - 1;
            return true;
        }

        /// <summary>
        /// With the switch on, a setting line is on a page of the settings screen, not in the top list: picking it (a test, the
        /// old intent) still does what picking it did.
        /// </summary>
        static bool OnASettingsPage(MenuLine line) => SettingsScreenValues.On && IsSetting(line);

        /// <summary>The settings screen, from the Esc menu.</summary>
        void OpenSettings() => SettingsScreen.Ensure().Open(SettingsFrom.PauseMenu);

        /// <summary>The settings screen went: the Esc menu's lists are made again (a value its keys' list shows may have changed).</summary>
        public void SettingsClosed() => _pauseLinesStale = true;

        /// <summary>The Esc menu's line that a settings screen's line is (every line but the graphics ones, which are the screen's own).</summary>
        public static bool MenuLineOf(SettingLine line, out MenuLine menuLine)
        {
            switch (line)
            {
                case SettingLine.Volume: menuLine = MenuLine.Volume; return true;
                case SettingLine.Music: menuLine = MenuLine.Music; return true;
                case SettingLine.MusicVolume: menuLine = MenuLine.MusicVolume; return true;
                case SettingLine.DashDirection: menuLine = MenuLine.DashDirection; return true;
                case SettingLine.DamageNumbers: menuLine = MenuLine.DamageNumbers; return true;
                case SettingLine.CameraKick: menuLine = MenuLine.CameraKick; return true;
                case SettingLine.CameraDistance: menuLine = MenuLine.CameraDistance; return true;
                case SettingLine.Attachments: menuLine = MenuLine.Attachments; return true;
                case SettingLine.Greybox: menuLine = MenuLine.Greybox; return true;
                case SettingLine.Light: menuLine = MenuLine.Light; return true;
                case SettingLine.ToonLook: menuLine = MenuLine.ToonLook; return true;
                case SettingLine.BetweenRuns: menuLine = MenuLine.BetweenRuns; return true;
                case SettingLine.CameraAngle: menuLine = MenuLine.CameraAngle; return true;
                default: menuLine = MenuLine.Resume; return false;
            }
        }

    }
}
