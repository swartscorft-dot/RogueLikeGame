using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's line "Toon look (trial)" (work order 115; Kristian, 2026-11-24, of the key art drawn of her: "Klarer vi å
    /// få modellen i spillet til å ha mer av det?"), as part of <see cref="RunHud"/>: it flips <see cref="ToonLookValues.On"/>
    /// either way, and her model follows it the same frame (<see cref="ToonLook"/>), so that he can compare the two looks while
    /// playing. It stands just above the weapon attachments' line, which stays last. Not kept between starts.
    /// </summary>
    public sealed partial class RunHud
    {
        const string ToonLookOnLine = "Toon look (trial):  on";
        const string ToonLookOffLine = "Toon look (trial):  off";

        /// <summary>The line's words now (two fixed strings: the menu asks every frame it is up).</summary>
        string ToonLookText => ToonLookValues.On ? ToonLookOnLine : ToonLookOffLine;

        /// <summary>Either way it flips.</summary>
        void FlipToonLook()
        {
            ToonLookValues.On = !ToonLookValues.On;
            SavedMusicAndLook.Keep(); // saved like every other setting (the follow-up to work order 128)
        }
    }
}
