using System;
using System.Collections.Generic;

namespace Roguelike
{
    /// <summary>
    /// The optimisations of work order 032 (2026-11-20) by name, each on by default, so that the benchmark can measure a
    /// build with one of them off (-benchmarkOff name) and the tests can show the game is the same with it on and off. A
    /// switch changes only how fast the game runs: never what happens in it.
    /// </summary>
    public static class PerformanceSwitches
    {
        sealed class Entry
        {
            public string Name;
            public string What;
            public Func<bool> Get;
            public Action<bool> Set;
        }

        static readonly List<Entry> s_entries = new List<Entry>();

        static PerformanceSwitches()
        {
            Add("enemy-sight", "an enemy out of the camera's sight is not posed", () => EnemyLook.CullUnseen, v => EnemyLook.CullUnseen = v);
        }

        static void Add(string name, string what, Func<bool> get, Action<bool> set) =>
            s_entries.Add(new Entry { Name = name, What = what, Get = get, Set = set });

        /// <summary>Turns the switch of that name on or off; false when there is none.</summary>
        public static bool Set(string name, bool on)
        {
            foreach (Entry e in s_entries)
            {
                if (!string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase)) continue;
                e.Set(on);
                return true;
            }
            return false;
        }

        /// <summary>Every switch on again, as the game ships.</summary>
        public static void AllOn()
        {
            foreach (Entry e in s_entries) e.Set(true);
        }

        /// <summary>The names, one each.</summary>
        public static IEnumerable<string> Names()
        {
            foreach (Entry e in s_entries) yield return e.Name;
        }

        /// <summary>"name=on" or "name=off", one each, with what it does.</summary>
        public static IEnumerable<string> Describe()
        {
            foreach (Entry e in s_entries) yield return e.Name + "=" + (e.Get() ? "on" : "off") + " (" + e.What + ")";
        }
    }
}
