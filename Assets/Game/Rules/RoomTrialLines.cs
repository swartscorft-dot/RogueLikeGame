namespace Roguelike.Rules
{
    /// <summary>
    /// The rooms' trials on the settings screen's Trials page (work order 135; Kristian ticked on 2026-11-26 that the rooms' switches
    /// stand on the Trials page, all on when he comes home): "Line-ups" on or off (<see cref="LevelValues.LineUps"/>, work order 135),
    /// read where a floor is built, so a change holds from the next floor built. The game keeps it in RoomTrialSettings.
    /// </summary>
    public static class RoomTrialLines
    {
        public const string LineUpsName = "Line-ups";

        /// <summary>Is this one of the rooms' lines.</summary>
        public static bool Is(SettingLine line) => line == SettingLine.LineUps;

        /// <summary>The line's name on the page.</summary>
        public static string Name(SettingLine line) => LineUpsName;

        /// <summary>The line's words: on or off.</summary>
        public static string Words(bool on) => on ? GraphicsRules.On : GraphicsRules.Off;
    }
}
