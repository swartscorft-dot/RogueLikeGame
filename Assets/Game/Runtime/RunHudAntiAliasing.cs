using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's line "Anti-aliasing" (work order 125; Kristian, 2026-11-25, of work order 119's pictures: "man ser mange
    /// fragmenterte pixler rundt omkring, det ser veldig dårlig ut"), as part of <see cref="RunHud"/>: it flips
    /// <see cref="AntiAliasingValues.On"/> either way, and the game's camera follows it from its next frame
    /// (<c>Assets/Game/Look/ScreenLook.cs</c>), so that he can compare the picture with and without. It stands just above the
    /// toon look's line. Not kept between starts.
    /// </summary>
    public sealed partial class RunHud
    {
        const string AntiAliasingOnLine = "Anti-aliasing:  on";
        const string AntiAliasingOffLine = "Anti-aliasing:  off";

        /// <summary>The line's words now (two fixed strings: the menu asks every frame it is up).</summary>
        string AntiAliasingText => AntiAliasingValues.On ? AntiAliasingOnLine : AntiAliasingOffLine;

        /// <summary>Either way it flips.</summary>
        void FlipAntiAliasing() => AntiAliasingValues.On = !AntiAliasingValues.On;
    }
}
