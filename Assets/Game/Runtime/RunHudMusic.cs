using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's music lines (work order 116, a trial; Kristian, 2026-11-24: "Må høre det i spillet først"), as part of
    /// <see cref="RunHud"/>: "Music" turns the music on and off (<see cref="MusicValues.On"/>, the order's switch), and "Music
    /// volume" steps its master volume 0 to 100 % in tenths (<see cref="MusicValues.VolumeStep"/>), both in the session's
    /// <see cref="MusicPlayer.Values"/>: kept for the session, not saved. They stand before the attachments' line, which stays last.
    /// </summary>
    public sealed partial class RunHud
    {
        const string MusicOnLine = "Music:  on", MusicOffLine = "Music:  off";

        /// <summary>The volume line's words for each step, made once (the menu asks for them every frame it is up).</summary>
        static readonly string[] s_musicVolumeLines = MakeMusicVolumeLines();

        static string[] MakeMusicVolumeLines()
        {
            var lines = new string[SettingsRules.VolumeSteps + 1];
            for (int i = 0; i < lines.Length; i++)
                lines[i] = string.Format(CultureInfo.InvariantCulture, "Music volume:  {0} %", SettingsRules.Percent(i));
            return lines;
        }

        static MusicValues MusicNow => MusicPlayer.Values ?? (MusicPlayer.Values = new MusicValues());

        static bool IsMusicLine(MenuLine line) => line == MenuLine.Music || line == MenuLine.MusicVolume;

        /// <summary>The two lines, in the menu's order.</summary>
        void AddMusicLines()
        {
            _menu.Add(MenuLine.Music);
            _menu.Add(MenuLine.MusicVolume);
        }

        /// <summary>A music line's words now.</summary>
        static string MusicText(MenuLine line) =>
            line == MenuLine.Music ? (MusicNow.On ? MusicOnLine : MusicOffLine) : s_musicVolumeLines[SettingsRules.VolumeStep(MusicNow.VolumeStep)];

        /// <summary>The left or right arrow on a music line: the switch flips either way; the volume steps a tenth and stops at 0 and 100 %.</summary>
        static void ChangeMusic(MenuLine line, int delta)
        {
            MusicValues v = MusicNow;
            if (line == MenuLine.Music) v.On = !v.On;
            else v.VolumeStep = SettingsRules.StepVolume(v.VolumeStep, delta);
            SavedMusicAndLook.Keep(); // saved like every other setting (the follow-up to work order 128)
        }

        /// <summary>Enter or a click on a music line: the switch flips; the volume goes up a step, and round to 0 after 100 %, as the game's volume line does.</summary>
        static void PickMusic(MenuLine line)
        {
            MusicValues v = MusicNow;
            if (line == MenuLine.Music) v.On = !v.On;
            else v.VolumeStep = SettingsRules.VolumeStep(v.VolumeStep) >= SettingsRules.VolumeSteps ? 0 : SettingsRules.VolumeStep(v.VolumeStep) + 1;
            SavedMusicAndLook.Keep(); // saved like every other setting (the follow-up to work order 128)
        }
    }
}
