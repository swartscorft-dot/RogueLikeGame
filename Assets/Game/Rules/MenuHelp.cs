using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The menus' help (the follow-up to work order 128, from a friend's first run of the shared build, 2026-11-25): "Start spill
    /// knappen fra hovedmenyen funker ikke - man må selecte en character, men hvis man bare har et valg så kunne kanskje den bare
    /// være valgt? UI sier ikke noe om hva man må gjøre når man prøver å starte spill, så jeg knota litt med det" and "kanskje en
    /// feedback når man prøver å kjøpe en upgrade man ikke har råd til? Bare polish, men føler det er vanlig".
    ///
    /// On: on the character screen her slot is the chosen one and RUN LOADOUT always goes on with her (the cursor may still be
    /// moved along the row of slots to come), a click on her name plate goes on as Enter does, a dark slot chosen answers with a
    /// plain line on a plate over the row and the cursor comes back to her, and the character screen's and the loadout's lines of
    /// keys say in plain words what goes on and what starts the run. Wherever something is bought with Echoes, what she cannot
    /// afford is drawn dimmer, and a try at it flashes its price and the Echoes plate red with one shake, sounds the dull tick and
    /// says how many more are needed. Off: the menus as they were.
    /// </summary>
    public static class MenuHelpValues
    {
        /// <summary>The switch. On by default (the friend's words above).</summary>
        public static bool On = true;

        /// <summary>The veil over a card she cannot afford, drawn before she tries (its black over the card's face).</summary>
        public const float UnaffordableVeil = 0.42f;
        /// <summary>The refusal's flash and shake: how long, how far at its start (layout pixels), how many swings.</summary>
        public const float RefusalSeconds = 0.45f;
        public const float ShakePixels = 9f;
        public const float ShakeSwings = 3f;
        /// <summary>The refusal's sound: the placeholder sounds' dull tick (a chain's press off its clean part), at this gain.</summary>
        public const float RefusalSoundGain = 1f;

        /// <summary>The refused dark slot's line: a plate over the row of slots, its words' size.</summary>
        public const int RefusedSlotSize = 26;
        public const float RefusedSlotHeight = 46f;
        public const float RefusedSlotAbove = 104f;
        /// <summary>The small tag over her slot that says she is the one chosen.</summary>
        public const string ChosenWord = "CHOSEN";
        public const int ChosenSize = 18;
    }

    /// <summary>The menus' help's words and the refusal's motion (no engine).</summary>
    public static class MenuHelpRules
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The dark slot's answer, naming her (<see cref="StartMenuValues.Name"/>).</summary>
        public static string RefusedSlot(string herName) => "Only the " + Title(herName) + " can be chosen for now: the other slots are characters to come.";

        /// <summary>The refused buy's line: "Not enough Echoes: 25 more needed for Second Look."</summary>
        public static string NotEnough(int need, string currency, string what) =>
            "Not enough " + currency + ": " + Math.Max(1, need).ToString(s_Inv) + " more needed for " + what + ".";

        /// <summary>How much more she needs for a price, or 0 when she can pay it.</summary>
        public static int Need(int price, int currency) => price > currency ? price - currency : 0;

        /// <summary>The refusal's shake at <paramref name="t"/> seconds after the try: swings dying away, 0 after <see cref="MenuHelpValues.RefusalSeconds"/>.</summary>
        public static float Shake(float t)
        {
            if (t < 0f || t >= MenuHelpValues.RefusalSeconds) return 0f;
            float share = t / MenuHelpValues.RefusalSeconds;
            return MenuHelpValues.ShakePixels * (1f - share) * (float)Math.Sin(share * MenuHelpValues.ShakeSwings * 2.0 * Math.PI);
        }

        /// <summary>How red the price and the Echoes plate are at <paramref name="t"/> seconds after the try: 1 at once, 0 at the end.</summary>
        public static float Flash(float t) => t < 0f || t >= MenuHelpValues.RefusalSeconds ? 0f : 1f - t / MenuHelpValues.RefusalSeconds;

        /// <summary>"GUNSLINGER" as "Gunslinger".</summary>
        public static string Title(string name) =>
            string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1).ToUpperInvariant() + name.Substring(1).ToLowerInvariant();
    }
}
