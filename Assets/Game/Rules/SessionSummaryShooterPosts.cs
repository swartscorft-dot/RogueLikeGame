using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Shooters that hold their place (work order 098) over a session: the rooms' fights that held shooters, from the recorder's
    /// <see cref="ShooterPostEvents.Post"/> lines (one a shooter a fight). Work order 101: the casters' the same, from their own
    /// <see cref="CasterPostEvents.Post"/> lines ("shooter" below reads "caster" for them).
    /// </summary>
    public sealed class ShooterPostStats
    {
        /// <summary>Shooters told, and how many of them held a post.</summary>
        public int Shooters, Held;
        /// <summary>Rooms' fights that held shooters; of them, those in which she came within a shooter's reach, and those whose every shooter died.</summary>
        public int Rooms, RoomsReached, RoomsCleared;
        /// <summary>Medians over those rooms: s from the room waking until she first came within any shooter's reach, and until its last shooter died.</summary>
        public double ReachMedian = double.NaN, LastDeathMedian = double.NaN;
    }

    public static partial class SessionSummary
    {
        /// <summary>The shooters' posts of a session (work order 098); a log with no such line gives none (0 shooters).</summary>
        public static ShooterPostStats ComputeShooterPosts(IReadOnlyList<SessionEvent> events) => ComputePosts(events, ShooterPostEvents.Post);

        /// <summary>The casters' posts of a session (work order 101), counted as the shooters' are; a log with no such line gives none.</summary>
        public static ShooterPostStats ComputeCasterPosts(IReadOnlyList<SessionEvent> events) => ComputePosts(events, CasterPostEvents.Post);

        /// <summary>The posts of the lines of one type (work order 098's count, for the shooters' and, since work order 101, the casters' lines).</summary>
        static ShooterPostStats ComputePosts(IReadOnlyList<SessionEvent> events, string type)
        {
            var stats = new ShooterPostStats();
            var rooms = new Dictionary<string, List<(double reach, double died)>>();
            var order = new List<string>();
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
            foreach (SessionEvent e in events)
            {
                if (e.Type != type) continue;
                stats.Shooters++;
                if (e.ExtraValue("held") == "1") stats.Held++;
                // One room's fight: its floor, its room, the moment it woke.
                string key = (e.ExtraValue("floor") ?? "") + "/" + (e.ExtraValue("order") ?? "") + "/" + (e.ExtraValue("woke") ?? "");
                if (!rooms.TryGetValue(key, out List<(double, double)> list))
                {
                    rooms[key] = list = new List<(double, double)>();
                    order.Add(key);
                }
                list.Add((Num(e, "reach"), Num(e, "died")));
            }
            stats.Rooms = order.Count;
            var reach = new List<(double v, double w)>();
            var last = new List<(double v, double w)>();
            foreach (string key in order)
            {
                List<(double reach, double died)> list = rooms[key];
                List<double> reached = list.Select(x => x.reach).Where(v => !double.IsNaN(v)).ToList();
                if (reached.Count > 0) reach.Add((reached.Min(), 1d));
                if (list.All(x => !double.IsNaN(x.died))) last.Add((list.Max(x => x.died), 1d));
            }
            stats.RoomsReached = reach.Count;
            stats.RoomsCleared = last.Count;
            if (reach.Count > 0) stats.ReachMedian = MedianOfValues(reach);
            if (last.Count > 0) stats.LastDeathMedian = MedianOfValues(last);
            return stats;
        }

        /// <summary>
        /// The one line beside the room use's (work order 098): "7 of 7 shooters held a post, in 3 rooms; from the room waking she came
        /// within a shooter's reach after a median of 6.2 s (3 rooms), and the last shooter fell after a median of 21.0 s (2 rooms)".
        /// Null for a session with no shooter in a room's fight.
        /// </summary>
        public static string ShooterPostLine(ShooterPostStats s) => PostLine(s, "shooter");

        /// <summary>
        /// The casters' line, the second beside it (work order 101), worded as the shooters': "3 of 3 casters held a post, in 2 rooms;
        /// from the room waking she came within a caster's reach after a median of 5.0 s (2 rooms), and the last caster fell after a
        /// median of 19.5 s (2 rooms)". Null for a session with no caster in a room's fight.
        /// </summary>
        public static string CasterPostLine(ShooterPostStats s) => PostLine(s, "caster");

        static string PostLine(ShooterPostStats s, string word)
        {
            if (s == null || s.Shooters == 0) return null;
            string Rooms(int n) => n.ToString(CultureInfo.InvariantCulture) + (n == 1 ? " room" : " rooms");
            return s.Held.ToString(CultureInfo.InvariantCulture) + " of " + s.Shooters.ToString(CultureInfo.InvariantCulture) + " " + word
                   + (s.Shooters == 1 ? "" : "s") + " held a post, in " + Rooms(s.Rooms) + "; from the room waking she came within a " + word + "'s reach after a median of "
                   + Secs(s.ReachMedian) + " (" + Rooms(s.RoomsReached) + "), and the last " + word + " fell after a median of " + Secs(s.LastDeathMedian)
                   + " (" + Rooms(s.RoomsCleared) + ")";
        }
    }
}
