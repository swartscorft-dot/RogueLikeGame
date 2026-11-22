using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's light (work order 085; Kristian, 2026-11-22: "det er mørkere i rommet ... at lysilider rundt er
    /// hovedkilden til sol"), as part of <see cref="RunHud"/>: a line under the greybox view, beside the camera's lines, that steps
    /// "as before", "lamplit A", "lamplit B", "lamplit C" and back (<see cref="LightSteps"/>), kept in PlayerPrefs as the camera's
    /// lines are (<see cref="GameSettings.LightStep"/>). The floor she is on follows it at once (<see cref="LamplitView"/>); the
    /// default is lamplit C (Kristian, 2026-11-22, having played the four: "C er best").
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The line's words for each step, made once (the menu asks for them every frame it is up).</summary>
        static readonly string[] s_lightLines = MakeLightLines();

        static string[] MakeLightLines()
        {
            var lines = new string[LightSteps.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = "Light:  " + LightSteps.Names[i];
            return lines;
        }

        /// <summary>The line's words now.</summary>
        string LightText => s_lightLines[LightSteps.Step(GameSettings.LightStep)];

        /// <summary>One step on (Enter, a click, the right arrow) or back (the left arrow), and kept.</summary>
        void StepLight(int delta)
        {
            int step = GameSettings.LightStep;
            GameSettings.SetLightStep(delta < 0 ? LightSteps.Previous(step) : LightSteps.Next(step));
        }
    }
}
