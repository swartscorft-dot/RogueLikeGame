using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 086 in the session summary: the doors that close behind her (on each floor's "room fights" line: how many times
    /// a room's doors closed, and the seconds from closing to opening, mean and longest), and the held duck (the seconds she was
    /// down, in all and the median, beside the ducks; and under the defence, what a duck stopped, by the enemy and its attack).
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>A room_door event into its run's floor: a closing counted, an opening's seconds kept.</summary>
        static void CountRoomDoor(RunRow run, SessionEvent e, int floor)
        {
            if (!run.RoomFights.TryGetValue(floor, out RoomFightCounts counts)) run.RoomFights[floor] = counts = new RoomFightCounts();
            if (e.Name == "closed") counts.DoorsClosed++;
            else if (e.Name == "opened" && float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                counts.DoorSeconds.Add(s);
        }

        /// <summary>
        /// The doors' words on a floor's "room fights" line: ", doors closed 3 (closing to opening mean 24.1 s, longest 40.2 s)";
        /// nothing for a floor where none closed (a log from before, or the switch off).
        /// </summary>
        public static string DoorsWords(RoomFightCounts c)
        {
            if (c == null || c.DoorsClosed == 0) return "";
            string words = ", doors closed " + c.DoorsClosed.ToString(CultureInfo.InvariantCulture);
            if (c.DoorSeconds.Count == 0) return words + " (none opened yet)";
            return words + " (closing to opening mean " + Seconds(c.DoorSeconds.Average()) + ", longest " + Seconds(c.DoorSeconds.Max()) + ")";
        }

        /// <summary>The held duck's seconds down and what the ducks stopped, of one scope's events.</summary>
        static void ComputeHeldDuck(ScopeStats s, List<SessionEvent> events)
        {
            var seconds = new List<float>();
            foreach (SessionEvent e in events)
                if (e.Type == SessionEventTypes.DuckEnd && float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float d))
                    seconds.Add(d);
            s.DuckSecondsTotal = seconds.Sum();
            s.DuckSecondsMedian = seconds.Count > 0 ? MedianOf(seconds) : float.NaN;
            s.StoppedByDuckWhat.Clear();
            foreach (IGrouping<string, SessionEvent> g in events.Where(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.StoppedByCover)
                                                                 .GroupBy(e => ((e.Enemy ?? "?") + " " + (e.Name ?? "?")).Trim())
                                                                 .OrderByDescending(g => g.Count()).ThenBy(g => g.Key, StringComparer.Ordinal))
                s.StoppedByDuckWhat.Add(new KeyValuePair<string, int>(g.Key, g.Count()));
        }

        static float MedianOf(List<float> values)
        {
            List<float> sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5f;
        }

        /// <summary>"12.4 s, 1.8 s": the seconds down in all and the median; "-" with no duck that ended.</summary>
        static string DuckSecondsCell(ScopeStats s) =>
            float.IsNaN(s.DuckSecondsMedian) ? "-" : Seconds(s.DuckSecondsTotal) + ", " + Seconds(s.DuckSecondsMedian);

        /// <summary>"shooter Arrow 3, shooter Fan 1": what the ducks stopped, most first; "-" with nothing.</summary>
        static string StoppedWhatCell(ScopeStats s) =>
            s.StoppedByDuckWhat.Count == 0 ? "-" : string.Join(", ", s.StoppedByDuckWhat.Select(p => p.Key + " " + p.Value.ToString(CultureInfo.InvariantCulture)));
    }
}
