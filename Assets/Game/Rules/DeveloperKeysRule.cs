using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// Whether the developer's keys are on (work order 066, 2026-11-21: a Windows build was zipped for his friends with
    /// them working; asked whether he wanted a full friends' build, Kristian chose "Bare utviklertastene av", only the
    /// developer keys off). On in the Editor always; in a built player only when it was started with
    /// <see cref="Argument"/>, or with the benchmark's argument (<see cref="BenchmarkArgument"/>), so that the benchmark,
    /// the developer's own tool, runs and draws as it did before the switch (the developer's text on in the practice room).
    /// Which keys are the developer's is the game's (DeveloperKeys.cs), not a rule's: a key is an engine type.
    /// </summary>
    public static class DeveloperKeysRule
    {
        /// <summary>The command-line argument that turns them on in a built player (work order 066).</summary>
        public const string Argument = "-dev";
        /// <summary>The benchmark's own argument (the same as Benchmark.Argument): it turns them on too.</summary>
        public const string BenchmarkArgument = "-benchmark";

        /// <summary>
        /// On in the Editor; in a player, when one of <paramref name="args"/> is exactly <see cref="Argument"/> or
        /// <see cref="BenchmarkArgument"/> (any case, nothing more: "-developer" is not it). The first argument, the program's
        /// own path, counts like any other: a program is never called "-dev".
        /// </summary>
        public static bool On(bool inEditor, IEnumerable<string> args)
        {
            if (inEditor) return true;
            if (args == null) return false;
            foreach (string a in args)
            {
                if (a == null) continue;
                string t = a.Trim();
                if (string.Equals(t, Argument, StringComparison.OrdinalIgnoreCase)) return true;
                if (string.Equals(t, BenchmarkArgument, StringComparison.OrdinalIgnoreCase)) return true;
            }
            return false;
        }
    }
}
