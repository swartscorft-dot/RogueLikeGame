using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 143 in the log (so his first sessions can be read): on each basic shot's shot_fired, with precision in use, the mode,
    /// her speed share at the press and the lowest in the wind-up, the meter or the hidden speed, the cone, the deflection, the distance
    /// to the target nearest the aim, whether the straight line would have hit and whether the cone caused a miss, and the seconds since
    /// her last counter-strafe; a counter_strafe line for every move key pressed against her motion above 35 %. Off, none of it.
    /// </summary>
    public static class PrecisionLog
    {
        public const string CounterStrafe = "counter_strafe";
        public const string Mode = "precision", ShareAtPress = "share_press", ShareLowest = "share_lowest", Momentum = "momentum",
                            HiddenSpeed = "hidden_speed", Cone = "cone", Deflection = "deflection", Nearest = "nearest",
                            StraightHit = "straight_hit", ConeMiss = "cone_miss", SinceCounterStrafe = "since_counter_strafe", Share = "share";
    }

    /// <summary>The precision of one session (work order 143), filled by <see cref="SessionSummary"/>.</summary>
    public sealed class PrecisionStats
    {
        /// <summary>Basic shots that carry precision's extras, and of them: fired accurate (no cone), missed by the cone.</summary>
        public int Shots, Accurate, ConeMisses;
        /// <summary>Counter-strafes in the session.</summary>
        public int CounterStrafes;
        /// <summary>Minutes of the session.</summary>
        public float Minutes;
        /// <summary>The median seconds from a counter-strafe to a shot fired within <see cref="SessionSummary.CounterStrafeCounts"/> of it; NaN for none.</summary>
        public float CounterToShotMedian = float.NaN;
        public int CounterToShotCount;
    }

    public static partial class SessionSummary
    {
        /// <summary>s: a shot this soon after a counter-strafe counts toward the median time from one to the shot.</summary>
        public const float CounterStrafeCounts = 1f;

        /// <summary>Work order 143's counts, whole session; none for a log without precision's extras.</summary>
        public static PrecisionStats ComputePrecision(List<SessionEvent> events, double length)
        {
            var s = new PrecisionStats { Minutes = (float)(Math.Max(0.0, length) / 60.0) };
            if (events == null) return s;
            var times = new List<float>();
            foreach (SessionEvent e in events)
            {
                if (e.Type == PrecisionLog.CounterStrafe)
                {
                    s.CounterStrafes++;
                    continue;
                }
                if (e.Type != SessionEventTypes.ShotFired) continue;
                string cone = e.ExtraValue(PrecisionLog.Cone);
                if (string.IsNullOrEmpty(cone)) continue;
                s.Shots++;
                if (float.TryParse(cone, NumberStyles.Float, CultureInfo.InvariantCulture, out float c) && c <= 0f) s.Accurate++;
                if (e.ExtraValue(PrecisionLog.ConeMiss) == "true") s.ConeMisses++;
                if (float.TryParse(e.ExtraValue(PrecisionLog.SinceCounterStrafe), NumberStyles.Float, CultureInfo.InvariantCulture, out float since)
                    && since >= 0f && since <= CounterStrafeCounts) times.Add(since);
            }
            s.CounterToShotCount = times.Count;
            if (times.Count > 0)
            {
                times.Sort();
                int m = times.Count / 2;
                s.CounterToShotMedian = times.Count % 2 == 1 ? times[m] : (times[m - 1] + times[m]) * 0.5f;
            }
            return s;
        }

        /// <summary>Work order 143's block: nothing in a log without precision's extras.</summary>
        static void WritePrecision(StringBuilder sb, PrecisionStats s)
        {
            if (s == null || (s.Shots == 0 && s.CounterStrafes == 0)) return;
            sb.AppendLine();
            var t = new Table("Precision (work order 143), whole session", "");
            t.Add("Basic shots with a cone read", Int(s.Shots));
            t.Add("  fired accurate", s.Shots > 0 ? Int(s.Accurate) + " (" + Percent(s.Accurate / (float)s.Shots) + ")" : "-");
            t.Add("  misses the cone caused", Int(s.ConeMisses) + "  " + PerMinute(s.ConeMisses, s.Minutes) + " a minute, "
                  + (s.Shots > 0 ? Percent(s.ConeMisses / (float)s.Shots) : "-") + " of the shots");
            t.Add("Counter-strafes", Int(s.CounterStrafes) + "  " + PerMinute(s.CounterStrafes, s.Minutes) + " a minute");
            t.Add("  counter-strafe to the shot, median", Seconds(s.CounterToShotMedian) + "  (" + Int(s.CounterToShotCount) + " shots within "
                  + CounterStrafeCounts.ToString("0.#", CultureInfo.InvariantCulture) + " s)");
            t.Write(sb);
        }

        static string PerMinute(int count, float minutes) =>
            minutes > 0f ? (count / minutes).ToString("0.0", CultureInfo.InvariantCulture) : "-";
    }
}
