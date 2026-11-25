using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// Six skill slots instead of eight (work order 138; Kristian, 2026-11-25: "Jeg tror også vi har litt for mange spell
    /// slots. Kanskje fjerne 2 spell slots", and his tick "To av de tomme": four skills from the start, two found on the way),
    /// with <see cref="KitValues.SixSlots"/> on. A key is still its place in the bindings' eight skill keys (Q, 1, 2, 3, R, 4,
    /// F, the right button); with six slots the keys 3 and 4 (places 3 and 5) hold no skill and nothing is put on them. The
    /// start skills sit on a fixed table of keys (<see cref="StartKeys"/>), so nothing slides onto 1 and 2; the two found
    /// skills go on 1, then 2. Off, eight keys and the start rule of work order 015 (a start skill on the key of its own
    /// place in the list), field for field.
    /// </summary>
    public static class SkillSlots
    {
        /// <summary>The keys in use with six slots, by their place in the bindings' skill keys: Q, 1, 2, R, F and the right button.</summary>
        public static readonly int[] SixKeys = { 0, 1, 2, 4, 6, 7 };
        /// <summary>The keys in use with the switch off: all eight, as before.</summary>
        public static readonly int[] EightKeys = { 0, 1, 2, 3, 4, 5, 6, 7 };

        /// <summary>
        /// The start skills' keys with six slots, a fixed table by name (the draft of 2026-11-26, section 3: Quick Shot on Q,
        /// Scatter Blast on R, Backstep on F, Triple Tap on the right button: where they sat before). A start skill not in it
        /// goes on the first free key.
        /// </summary>
        public static readonly KeyValuePair<string, int>[] StartKeys =
        {
            new KeyValuePair<string, int>("Quick Shot", 0),
            new KeyValuePair<string, int>("Scatter Blast", 4),
            new KeyValuePair<string, int>("Backstep", 6),
            new KeyValuePair<string, int>("Triple Tap", 7),
        };

        /// <summary>The keys in use, in key order.</summary>
        public static IReadOnlyList<int> Keys(bool six) => six ? SixKeys : EightKeys;

        /// <summary>True when this key holds skills: every key off, all but 3 and 4 on.</summary>
        public static bool InUse(int key, bool six) => key >= 0 && key < SkillLoadout.KeyCount && (!six || Array.IndexOf(SixKeys, key) >= 0);

        /// <summary>The key of the start table for a skill of this name, or -1 when it has none.</summary>
        public static int StartKeyOf(string name)
        {
            if (string.IsNullOrEmpty(name)) return -1;
            foreach (KeyValuePair<string, int> k in StartKeys)
                if (k.Key == name) return k.Value;
            return -1;
        }
    }
}
