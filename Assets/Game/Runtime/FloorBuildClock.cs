using System.Diagnostics;

namespace Roguelike
{
    /// <summary>
    /// Work order 081 (cleanup and speed): where the time of a floor's start goes, step by step. The run's start and the
    /// build of a floor mark their steps here (<see cref="Step"/>) between <see cref="Begin"/> and <see cref="End"/>; the
    /// last start's steps stay readable until the next begins (<see cref="Count"/>, <see cref="NameOf"/>, <see cref="MsOf"/>),
    /// for the build-time table of the order's test. Outside a begun start a step does nothing. It only reads the clock and
    /// writes into arrays made once: it changes nothing of what is built, and allocates nothing.
    /// </summary>
    public static class FloorBuildClock
    {
        /// <summary>The most steps one start keeps; later ones are added to the last.</summary>
        public const int MostSteps = 64;

        static readonly string[] s_names = new string[MostSteps];
        static readonly long[] s_ticks = new long[MostSteps];
        static int s_count;
        static long s_last;
        static long s_start;
        static long s_total;
        static bool s_open;

        /// <summary>True between a begin that opened the clock and its end.</summary>
        public static bool Open => s_open;

        /// <summary>
        /// Opens the clock for a start and forgets the last one's steps; true if this call opened it. A begin while it is
        /// open (a floor's build inside a run's start) does nothing and is false: only the one that opened it ends it.
        /// </summary>
        public static bool Begin()
        {
            if (s_open) return false;
            s_open = true;
            s_count = 0;
            s_total = 0;
            s_start = s_last = Stopwatch.GetTimestamp();
            return true;
        }

        /// <summary>The time since the last step (or the begin) is the step of this name. Nothing while the clock is not open.</summary>
        public static void Step(string name)
        {
            if (!s_open) return;
            long now = Stopwatch.GetTimestamp();
            if (s_count < MostSteps)
            {
                s_names[s_count] = name;
                s_ticks[s_count] = now - s_last;
                s_count++;
            }
            else s_ticks[MostSteps - 1] += now - s_last;
            s_last = now;
        }

        /// <summary>Closes the clock when <paramref name="opened"/> (what its begin answered): the rest since the last step is "the rest".</summary>
        public static void End(bool opened)
        {
            if (!opened || !s_open) return;
            Step("the rest");
            s_total = s_last - s_start;
            s_open = false;
        }

        /// <summary>The steps of the last start.</summary>
        public static int Count => s_count;
        public static string NameOf(int i) => s_names[i];
        public static double MsOf(int i) => s_ticks[i] * 1000.0 / Stopwatch.Frequency;
        /// <summary>The last start from its begin to its end, in ms.</summary>
        public static double TotalMs => s_total * 1000.0 / Stopwatch.Frequency;
    }
}
