using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The bar's slots of <see cref="PlayerCharacter"/> (work order 138; Kristian, 2026-11-25: "Kanskje fjerne 2 spell slots",
    /// his tick "To av de tomme"): with <see cref="KitValues.SixSlots"/> on, six of the eight skill keys hold skills (Q, 1, 2,
    /// R, F and the right button) and keys 3 and 4 nothing (<see cref="SkillSlots"/>). A key is still its place in the bindings'
    /// skill keys (<see cref="KeyCount"/> stays eight), so the input and every key's label are as before; what draws or lists
    /// the bar goes through these. Off, eight slots, every key in use.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>How many slots the bar has: six with the switch on, eight off.</summary>
        public int BarCount => Loadout.SlotsInUse;

        /// <summary>The key of the bar's slot <paramref name="index"/> (0 the leftmost), in key order; -1 out of range.</summary>
        public int BarKey(int index) => Loadout.KeyAt(index);

        /// <summary>True when the key holds skills: with six slots every key but 3 and 4.</summary>
        public bool KeyInUse(int key) => Loadout.InUse(key);

        /// <summary>How many of the bar's slots hold a skill now (a skill card says "slot n of six" from it: n is this plus one).</summary>
        public int SlotsFilled
        {
            get
            {
                int n = 0;
                for (int i = 0; i < BarCount; i++)
                    if (SkillOnKey(BarKey(i)) >= 0) n++;
                return n;
            }
        }
    }
}
