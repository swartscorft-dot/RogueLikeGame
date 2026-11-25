using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's camera angle (work order 121; Kristian, 2026-11-25: "Kan vi få en kopi av diablo, en av league og en av
    /// path of exile? Så kan vi vurdere de forskjellige"), as part of <see cref="RunHud"/>: a line just above the camera's kick
    /// and distance that steps "Ours", "Diablo", "League", "PoE" and back (<see cref="CameraAngles"/>), kept in PlayerPrefs as
    /// the distance's step is (<see cref="GameSettings.CameraAngleStep"/>). The camera follows it the same frame
    /// (<see cref="CameraRig"/> reads it every frame); the distance line's steps scale the angle's own distance. Shown only
    /// while <see cref="CameraAngles.On"/>. The default stays ours.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The line's words for each angle, made once (the menu asks for them every frame it is up).</summary>
        static readonly string[] s_angleLines = MakeAngleLines();

        static string[] MakeAngleLines()
        {
            var lines = new string[CameraAngles.Count];
            for (int i = 0; i < lines.Length; i++) lines[i] = "Camera angle:  " + CameraAngles.Names[i];
            return lines;
        }

        /// <summary>The line's words now.</summary>
        string CameraAngleText => s_angleLines[CameraAngles.Step(GameSettings.CameraAngleStep)];

        /// <summary>One step on (Enter, a click, the right arrow) or back (the left arrow), and kept.</summary>
        void StepCameraAngle(int delta)
        {
            int step = GameSettings.CameraAngleStep;
            GameSettings.SetCameraAngleStep(delta < 0 ? CameraAngles.Previous(step) : CameraAngles.Next(step));
        }
    }
}
