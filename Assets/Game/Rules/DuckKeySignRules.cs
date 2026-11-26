using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The duck's key shown, and a slightly larger reach (work order 132, 2026-11-25). A friend's note from a first run, passed on
    /// by Kristian: "Er endel objekter som får orange rektangel rundt seg on-hover, skjønte ikke hvordan man interacter med dem.
    /// Kanskje ditche rektangelen om man ikke kan interacte?" (the rectangle is the quick duck's rim of work order 043: the
    /// nearest low prop in reach, where a tap of the cover key would duck her). Kristian's own answer: "Ctrl/shift når det er
    /// mulig. Kanskje gjøre området litt større". The producer's reading: while the rim shows, a small sign at it names the key
    /// the cover action is bound to; and the REACH within which a duck is possible (and so the rim shows) is a little larger.
    /// The rim itself is as it was. Every number is a first guess he tunes by playing.
    /// </summary>
    public static class DuckKeySignValues
    {
        /// <summary>
        /// The order's switch: on, the key sign stands at the rim while it shows and the duck's reach is
        /// <see cref="ReachFactor"/> times <see cref="FeelValues.DuckReach"/>; off, the rim and the reach are exactly as before
        /// work order 132. On by default (his words: "Ctrl/shift når det er mulig. Kanskje gjøre området litt større").
        /// </summary>
        public static bool On = true;

        /// <summary>
        /// The duck's reach (her middle to a low prop's edge, <see cref="FeelValues.DuckReach"/>, 1 m) times this while the switch is
        /// on: the producer's first guess, a quarter more (1.25 m). It moves where the rim first shows, where a tap ducks her, and
        /// where she may go down again at the same prop with the key held (work order 086). Read live.
        /// </summary>
        public static float ReachFactor = 1.25f;

        /// <summary>Layout pixels (of a view 1080 high) of the key's letters: small, under the chest prompt's 18.</summary>
        public const int KeySize = 14;
        /// <summary>The plate's height as a share of the letters' size, and its least width in layout pixels (the prompt's own key plate's).</summary>
        public const float PlateHeightShare = 1.45f, PlateLeastWidth = 24f;
        /// <summary>Layout pixels of room either side of the key's letters on its plate.</summary>
        public const float PlatePad = 5f;
        /// <summary>Layout pixels from the rim's edge on the screen to the plate: beside the prop, never over it.</summary>
        public const float Gap = 6f;
        /// <summary>How opaque the sign is drawn: quiet, as it is on screen often.</summary>
        public const float Alpha = 0.8f;
    }

    /// <summary>The rules of the duck's key sign (work order 132; see <see cref="DuckKeySignValues"/>). No engine.</summary>
    public static class DuckKeySignRules
    {
        /// <summary>The duck's reach in use: <paramref name="duckReach"/> times the factor with the switch on, else as it was.</summary>
        public static float Reach(float duckReach) => Reach(DuckKeySignValues.On, duckReach, DuckKeySignValues.ReachFactor);

        /// <summary>The same, with the switch and the factor given (for tests). A factor under 1 never shrinks it below the feel's.</summary>
        public static float Reach(bool on, float duckReach, float factor) => on ? duckReach * Math.Max(1f, factor) : duckReach;

        /// <summary>
        /// The words on the sign for a key, from the name the input system gives it: "LeftCtrl" and "RightCtrl" read "Ctrl", the
        /// two Shifts "Shift", the two Alts "Alt", a digit its number, "None" (no key bound) nothing; any other key its own name.
        /// </summary>
        public static string KeyLabel(string keyName)
        {
            if (string.IsNullOrEmpty(keyName) || keyName == "None") return "";
            if (keyName.EndsWith("Ctrl", StringComparison.Ordinal)) return "Ctrl";
            if (keyName.EndsWith("Shift", StringComparison.Ordinal)) return "Shift";
            if (keyName.EndsWith("Alt", StringComparison.Ordinal)) return "Alt";
            if (keyName.StartsWith("Digit", StringComparison.Ordinal) && keyName.Length > 5) return keyName.Substring(5);
            return keyName;
        }

        /// <summary>
        /// Where the sign goes on the screen (layout pixels, y down), given the rim's box on the screen and where she is: just
        /// beside the box on the side away from her, at the height of the box's top, so it covers neither the prop nor her.
        /// Returns the plate's left edge; <paramref name="top"/> is its top.
        /// </summary>
        public static float PlateLeft(float rimLeft, float rimRight, float rimTop, float herX, float plateWidth, float plateHeight, out float top)
        {
            top = rimTop - plateHeight * 0.5f;
            bool herOnTheLeft = herX <= (rimLeft + rimRight) * 0.5f;
            return herOnTheLeft ? rimRight + DuckKeySignValues.Gap : rimLeft - DuckKeySignValues.Gap - plateWidth;
        }
    }
}
