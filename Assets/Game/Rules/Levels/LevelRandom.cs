using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The level generator's random numbers: SplitMix64, written out here so that the same seed gives the same numbers
    /// on every machine and in every version (System.Random and UnityEngine.Random promise neither).
    ///
    /// Each stage of generation draws from a stream of its own (<see cref="Stream"/>), made from the level seed, the
    /// stage's name and the attempt number, so a change to what one stage draws leaves the other stages as they were.
    /// </summary>
    public sealed class LevelRandom
    {
        const ulong Golden = 0x9E3779B97F4A7C15UL;

        ulong _state;

        public LevelRandom(ulong state) => _state = state;

        /// <summary>The stream of one stage of one attempt at a level.</summary>
        public static LevelRandom Stream(ulong seed, string stage, int attempt = 0)
        {
            unchecked
            {
                ulong state = Mix(seed ^ Hash(stage));
                state = Mix(state + (ulong)(uint)attempt * Golden);
                return new LevelRandom(state);
            }
        }

        /// <summary>The next 64 random bits.</summary>
        public ulong NextBits()
        {
            unchecked
            {
                _state += Golden;
                return Mix(_state);
            }
        }

        /// <summary>A whole number from 0 up to, not including, <paramref name="count"/>, every one as likely.</summary>
        public int Below(int count)
        {
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count));
            ulong n = (ulong)count;
            // Draws under this would make the low numbers a little more likely; they are drawn again.
            ulong reject = (ulong.MaxValue - n + 1) % n;
            while (true)
            {
                ulong r = NextBits();
                if (r >= reject) return (int)(r % n);
            }
        }

        /// <summary>A whole number from <paramref name="min"/> to <paramref name="max"/>, both included.</summary>
        public int Range(int min, int max) => max <= min ? min : min + Below(max - min + 1);

        /// <summary>Puts the list in a random order (Fisher-Yates).</summary>
        public void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Below(i + 1);
                T t = list[i];
                list[i] = list[j];
                list[j] = t;
            }
        }

        /// <summary>SplitMix64's finaliser: every bit of the input spreads over every bit of the output.</summary>
        public static ulong Mix(ulong z)
        {
            unchecked
            {
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }
        }

        /// <summary>FNV-1a over the name's characters: a stage's name as a number.</summary>
        public static ulong Hash(string name)
        {
            unchecked
            {
                ulong h = 0xCBF29CE484222325UL;
                foreach (char c in name ?? "")
                {
                    h ^= c;
                    h *= 0x100000001B3UL;
                }
                return h;
            }
        }
    }

    /// <summary>
    /// A level seed as the player sees it: the 64-bit number written in base 32 with an alphabet that leaves out the
    /// look-alikes 0, O, 1 and I, at least <see cref="ShortLength"/> characters long. Seeds the game draws itself
    /// (<see cref="Fresh"/>) fit in 40 bits, so they show as exactly 8 characters; any 64-bit number still has a text
    /// (up to 13 characters) and reads back to the same number.
    /// </summary>
    public static class LevelSeed
    {
        public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
        public const int ShortLength = 8;
        public const int MaxLength = 13;
        /// <summary>The bits a seed of <see cref="ShortLength"/> characters can hold.</summary>
        public const ulong ShortMask = (1UL << (5 * ShortLength)) - 1UL;

        public static string ToText(ulong seed)
        {
            var chars = new List<char>(MaxLength);
            do
            {
                chars.Add(Alphabet[(int)(seed & 31UL)]);
                seed >>= 5;
            } while (seed != 0UL);
            while (chars.Count < ShortLength) chars.Add(Alphabet[0]);
            chars.Reverse();
            return new string(chars.ToArray());
        }

        /// <summary>
        /// Reads a seed's text back into its number. Small letters count as capitals; spaces and dashes are skipped.
        /// False for an empty text, a character outside the alphabet, or a number over 64 bits.
        /// </summary>
        public static bool TryParse(string text, out ulong seed)
        {
            seed = 0UL;
            if (string.IsNullOrEmpty(text)) return false;
            int digits = 0;
            foreach (char raw in text)
            {
                if (raw == ' ' || raw == '-') continue;
                int value = Alphabet.IndexOf(char.ToUpperInvariant(raw));
                if (value < 0) return false;
                if (seed >> 59 != 0UL) return false; // another five bits would not fit
                seed = (seed << 5) | (ulong)value;
                digits++;
            }
            return digits > 0;
        }

        /// <summary>A new seed of 8 characters, from any number that changes between calls (the clock, a counter).</summary>
        public static ulong Fresh(ulong entropy) => LevelRandom.Mix(entropy ^ 0x5EED5EED5EED5EEDUL) & ShortMask;
    }
}
