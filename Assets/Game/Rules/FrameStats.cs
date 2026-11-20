using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// What the benchmark (work order 032, 2026-11-20) says about a list of numbers taken once a frame: how many, the
    /// mean, a percentile and the largest. A percentile is the nearest-rank one: the smallest value with at least that
    /// share of the values at or below it (the 95th of 100 frames is the 95th smallest). Nothing is read from the engine.
    /// </summary>
    public static class FrameStats
    {
        /// <summary>The mean of the values; 0 for none.</summary>
        public static double Mean(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            double sum = 0;
            for (int i = 0; i < values.Count; i++) sum += values[i];
            return sum / values.Count;
        }

        /// <summary>The largest value; 0 for none.</summary>
        public static double Max(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            double max = values[0];
            for (int i = 1; i < values.Count; i++) max = Math.Max(max, values[i]);
            return max;
        }

        /// <summary>
        /// The nearest-rank percentile (0 to 100) of the values, which need not be sorted; 0 for none. 100 is the largest,
        /// 0 the smallest.
        /// </summary>
        public static double Percentile(IReadOnlyList<double> values, double percent)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
            return PercentileOfSorted(sorted, percent);
        }

        /// <summary>As <see cref="Percentile"/>, of values already sorted from the smallest.</summary>
        public static double PercentileOfSorted(IReadOnlyList<double> sorted, double percent)
        {
            if (sorted == null || sorted.Count == 0) return 0;
            double p = Math.Max(0, Math.Min(100, percent));
            int rank = (int)Math.Ceiling(p / 100.0 * sorted.Count); // 1-based
            return sorted[Math.Max(0, Math.Min(sorted.Count - 1, rank - 1))];
        }

        /// <summary>The middle of several runs' numbers (the mean of the two middle ones for an even count); 0 for none.</summary>
        public static double Median(IReadOnlyList<double> values)
        {
            if (values == null || values.Count == 0) return 0;
            var sorted = new List<double>(values);
            sorted.Sort();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
        }
    }
}
