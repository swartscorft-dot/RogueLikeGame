namespace Roguelike.Rules
{
    /// <summary>
    /// The chain's clean parts as Kristian set them on the evening of 2026-11-26 (work order 142's second round), each with its
    /// own switch: ON gives his new number as the kit's default (<see cref="KitValues"/>), off gives the number before, field for
    /// field, so a test that pins the older number sets the switch off in its set-up. Read when a kit is made.
    /// </summary>
    public static class KitTimingChoices
    {
        /// <summary>
        /// Part 4: the last press's clean part opens at 0.36 s after the shot before it (he ticked "0,36–0,66" against his last
        /// 101 third presses of the evening: 64 % clean at 0.42 to 0.66, 82 % at 0.36 to 0.66). Its end stays 0.66 s. Off, 0.42 s.
        /// </summary>
        public static bool LastPressAt036 = true;

        /// <summary>
        /// Part 3: Backstep's clean part opens with the hop's end (0 s), its end unchanged at 0.23 s. Of his 45 second presses that
        /// evening, 9 were early by 6 to 29 ms, all inside the 0.03 s that came before the clean part. Off, 0.03 s.
        /// </summary>
        public static bool BackstepFromHopsEnd = true;

        /// <summary>The last press's clean part's start: 0.36 s, or 0.42 s with the switch off.</summary>
        public static float LastPressCleanFrom => LastPressAt036 ? 0.36f : 0.42f;

        /// <summary>The clean part's start after an opening move (Backstep's hop): 0 s, or 0.03 s with the switch off.</summary>
        public static float OpeningMoveCleanFrom => BackstepFromHopsEnd ? 0f : 0.03f;
    }
}
