using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>One room's fight as work order 097 measures it: where it was, its room, her places and the deaths, and the measures.</summary>
    public sealed class RoomUseRow
    {
        /// <summary>The run it was fought in, from 1 as the session's runs are counted; 0 outside a run (the room lab, the practice room).</summary>
        public int Run;
        public string RunSeed = "";
        public int Floor, Room, Order;
        public string Kind = "", Template = "";
        /// <summary>How many of its enemies woke as it began (its room_fight), 0 when the log has no such line.</summary>
        public int Enemies;
        public double Start, End;
        /// <summary>How it ended (<see cref="RoomUseEvents.End"/>'s result), or what closed it in the log ("floor left", "run ended", "session ended").</summary>
        public string Ended = "";
        public RoomSpace Space;
        public readonly List<RoomPlace> Places = new List<RoomPlace>();
        public readonly List<RoomPlace> Deaths = new List<RoomPlace>();
        public RoomUseMeasures Measures = new RoomUseMeasures();
    }

    /// <summary>The medians of the measures over the fights of one kind of room in a session (work order 097).</summary>
    public sealed class RoomUseMedians
    {
        public string Kind = "";
        public int Fights;
        public double Seconds = double.NaN, UsedShare = double.NaN, FarthestShare = double.NaN, MedianDepthShare = double.NaN, Travel = double.NaN,
                      Circle = double.NaN, DeathsDepthShare = double.NaN, SprintSeconds = double.NaN, Jumps = double.NaN, CoversUsedShare = double.NaN;
    }

    /// <summary>
    /// Work order 097 in the session summary: how much of the room she uses. Each room's fight with her places in the log
    /// (<see cref="RoomUseEvents"/>, from her waking of the room to its end) gets its measures (<see cref="RoomUseRules.Measure"/>), the
    /// seconds she sprinted in it and her jumps (work order 092's sprint_end and sprint_jump). The summary gives one line near its top
    /// (<see cref="OneLine"/>, the three numbers later form orders are judged against), and at its end a block: a table a run of its
    /// fights, the medians of each measure by kind of room, and the three longest fights drawn as maps. Nothing for a log without
    /// those events (from before 097).
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>The rows of every room's fight with her places, in the order they began.</summary>
        public static List<RoomUseRow> ComputeRoomUse(IReadOnlyList<SessionEvent> sorted)
        {
            var rows = new List<RoomUseRow>();
            RoomUseRow current = null;
            SessionEvent lastFight = null;
            int run = 0, runs = 0;
            string seed = "";
            void Close(double t, string why)
            {
                if (current == null) return;
                current.End = Math.Max(current.Start, t);
                if (current.Ended.Length == 0) current.Ended = why;
                current = null;
            }
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
            int Whole(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;

            foreach (SessionEvent e in sorted)
            {
                switch (e.Type)
                {
                    case SessionEventTypes.RunStart:
                        Close(e.T, "run ended");
                        run = ++runs;
                        seed = e.Name ?? "";
                        break;
                    case SessionEventTypes.RunEnd:
                        Close(e.T, "run ended");
                        run = 0;
                        break;
                    case LabRoomEvents.Room:
                        Close(e.T, "floor left");
                        run = 0;
                        break;
                    case SessionEventTypes.FloorEnter:
                    case SessionEventTypes.LevelBuilt:
                        Close(e.T, "floor left");
                        break;
                    case SessionEventTypes.SessionEnd:
                        Close(e.T, "session ended");
                        break;
                    case SessionEventTypes.RoomFight:
                        lastFight = e;
                        break;
                    case RoomUseEvents.Room:
                    {
                        Close(e.T, "another fight");
                        current = new RoomUseRow
                        {
                            Run = run, RunSeed = run > 0 ? seed : "", Floor = Whole(e, "floor"), Room = Whole(e, "room"), Order = Whole(e, "order"),
                            Kind = e.ExtraValue("kind") ?? "", Template = e.ExtraValue("template") ?? "", Start = e.T, End = e.T, Space = RoomSpace.From(e),
                        };
                        if (lastFight != null && lastFight.ExtraValue("floor") == e.ExtraValue("floor") && lastFight.ExtraValue("order") == e.ExtraValue("order"))
                            current.Enemies = Whole(lastFight, "woke");
                        rows.Add(current);
                        break;
                    }
                    case RoomUseEvents.Place:
                        if (current != null && !double.IsNaN(Num(e, "d")) && !double.IsNaN(Num(e, "s")))
                            current.Places.Add(new RoomPlace(e.T, Num(e, "d"), Num(e, "s")));
                        break;
                    case RoomUseEvents.Death:
                        if (current != null && !double.IsNaN(Num(e, "d")) && !double.IsNaN(Num(e, "s")))
                            current.Deaths.Add(new RoomPlace(e.T, Num(e, "d"), Num(e, "s")));
                        break;
                    case RoomUseEvents.End:
                        if (current == null) break;
                        if (!double.IsNaN(Num(e, "d")) && !double.IsNaN(Num(e, "s"))) current.Places.Add(new RoomPlace(e.T, Num(e, "d"), Num(e, "s")));
                        current.Ended = e.Result ?? "";
                        Close(e.T, "");
                        break;
                }
            }
            if (current != null) Close(sorted.Count > 0 ? sorted[sorted.Count - 1].T : current.Start, "session ended");

            // The sprint's stretches (each ends at its sprint_end, its seconds long) and the jumps, over each fight's span.
            var sprints = new List<KeyValuePair<double, double>>();
            foreach (SessionEvent e in sorted)
                if (e.Type == SessionEventTypes.SprintEnd && double.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double s) && s > 0d)
                    sprints.Add(new KeyValuePair<double, double>(e.T - s, e.T));
            foreach (RoomUseRow r in rows)
            {
                r.Measures = RoomUseRules.Measure(r.Space, r.Places, r.Start, r.End, r.Deaths);
                r.Measures.SprintSeconds = Overlap(sprints, new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(r.Start, r.End) });
                r.Measures.Jumps = sorted.Count(e => e.Type == SessionEventTypes.SprintJump && e.T >= r.Start && e.T <= r.End);
            }
            return rows;
        }

        /// <summary>The medians of each measure over the fights of each kind of room, combat first, then elite and exit, then any other.</summary>
        public static List<RoomUseMedians> RoomUseByKind(IReadOnlyList<RoomUseRow> rows)
        {
            var result = new List<RoomUseMedians>();
            IEnumerable<string> kinds = new[] { "combat", "elite", "exit" }.Concat(rows.Select(r => r.Kind).Where(k => k != "combat" && k != "elite" && k != "exit").Distinct());
            foreach (string kind in kinds)
            {
                List<RoomUseRow> of = rows.Where(r => r.Kind == kind && r.Places.Count > 0).ToList();
                if (of.Count == 0) continue;
                double Median(Func<RoomUseMeasures, double> pick)
                {
                    var values = of.Select(r => pick(r.Measures)).Where(v => !double.IsNaN(v)).Select(v => (v, 1d)).ToList();
                    return values.Count == 0 ? double.NaN : MedianOfValues(values);
                }
                result.Add(new RoomUseMedians
                {
                    Kind = kind, Fights = of.Count, Seconds = Median(m => m.Seconds), UsedShare = Median(m => m.UsedShare), FarthestShare = Median(m => m.FarthestShare),
                    MedianDepthShare = Median(m => m.MedianDepthShare), Travel = Median(m => m.Travel), Circle = Median(m => m.Circle),
                    DeathsDepthShare = Median(m => m.DeathsDepthShare), SprintSeconds = Median(m => m.SprintSeconds), Jumps = Median(m => m.Jumps),
                    CoversUsedShare = Median(m => m.Covers > 0 ? m.CoversUsed / (double)m.Covers : double.NaN),
                });
            }
            return result;
        }

        /// <summary>The middle value (the mean of the two middle ones for an even count), as the summary's medians are told.</summary>
        static double MedianOfValues(List<(double v, double w)> values)
        {
            List<double> sorted = values.Select(x => x.v).OrderBy(v => v).ToList();
            int n = sorted.Count;
            return n % 2 == 1 ? sorted[n / 2] : (sorted[n / 2 - 1] + sorted[n / 2]) * 0.5;
        }

        /// <summary>
        /// The one line that names it (work order 097), over the session's combat rooms' fights: "used a median of 34 % of a combat
        /// room's floor; 90 % of a fight inside a circle of 5.2 m; farthest depth 81 % of the room (12 combat room fights)". The
        /// circle and the depth are medians too. Null for a log with no combat room's fight with places.
        /// </summary>
        public static string OneLine(IReadOnlyList<RoomUseRow> rows)
        {
            RoomUseMedians combat = RoomUseByKind(rows).FirstOrDefault(k => k.Kind == "combat");
            if (combat == null) return null;
            return "used a median of " + Pct(combat.UsedShare) + " of a combat room's floor; 90 % of a fight inside a circle of " + Metres(combat.Circle)
                   + "; farthest depth " + Pct(combat.FarthestShare) + " of the room (" + combat.Fights.ToString(CultureInfo.InvariantCulture)
                   + " combat room fight" + (combat.Fights == 1 ? "" : "s") + ")";
        }

        static string Pct(double share) => double.IsNaN(share) ? "-" : (share * 100d).ToString("0", CultureInfo.InvariantCulture) + " %";
        static string Metres(double m) => double.IsNaN(m) ? "-" : m.ToString("0.0", CultureInfo.InvariantCulture) + " m";
        static string Secs(double s) => double.IsNaN(s) ? "-" : s.ToString("0.0", CultureInfo.InvariantCulture) + " s";
        static string CountText(double n) => double.IsNaN(n) ? "-" : n.ToString("0.#", CultureInfo.InvariantCulture);

        static readonly string[] RoomUseHeader =
        {
            "  Floor", "Room", "Kind", "Template", "Time", "Enemies", "Floor used", "Farthest", "Median depth", "Travel", "90% circle", "Deaths' depth", "Sprint", "Jumps", "Covers used",
        };

        /// <summary>The block at the summary's end (work order 097); nothing for a log with no fight's places.</summary>
        static void WriteRoomUse(StringBuilder sb, IReadOnlyList<RoomUseRow> rows)
        {
            if (rows == null || rows.Count == 0) return;
            sb.AppendLine();
            sb.AppendLine("Room use in fights (work order 097): her place in each room's fight, in the room's own metres from the door she came in by");
            sb.AppendLine("  floor used: the 2 m cells of the room's walkable floor she stood in or passed through; farthest and median depth: from that");
            sb.AppendLine("  door in, of the room's depth; travel: metres she went; 90% circle: the radius of the smallest circle that held 90 %");
            sb.AppendLine("  of the fight's time (small, with a long travel: kiting round a spot); deaths' depth: the enemies' median (do they come to");
            sb.AppendLine("  her, or she to them); covers used: the room's tall and low covers she was within 1.5 m of for more than 1 s, of all of them.");
            foreach (IGrouping<int, RoomUseRow> run in rows.GroupBy(r => r.Run))
            {
                RoomUseRow first = run.First();
                sb.AppendLine(run.Key > 0 ? "Run " + Empty(first.RunSeed) : "Not in a run (the room lab, the practice room)");
                var t = new Table(RoomUseHeader);
                foreach (RoomUseRow r in run)
                {
                    RoomUseMeasures m = r.Measures;
                    t.Add("  " + Int(r.Floor), Int(r.Room), Empty(r.Kind), r.Template.Length == 0 ? "-" : r.Template, Secs(m.Seconds), Int(r.Enemies),
                          Pct(m.UsedShare) + " (" + Int(m.CellsUsed) + "/" + Int(m.CellsWalkable) + ")", Pct(m.FarthestShare), Pct(m.MedianDepthShare),
                          r.Places.Count == 0 ? "-" : Metres(m.Travel), Metres(m.Circle), Pct(m.DeathsDepthShare), Secs(m.SprintSeconds), Int(m.Jumps),
                          Int(m.CoversUsed) + " of " + Int(m.Covers));
                }
                t.Write(sb);
            }
            List<RoomUseMedians> kinds = RoomUseByKind(rows);
            if (kinds.Count > 0)
            {
                sb.AppendLine("Medians by kind of room, whole session");
                var k = new Table("  Kind", "Fights", "Time", "Floor used", "Farthest", "Median depth", "Travel", "90% circle", "Deaths' depth", "Sprint", "Jumps", "Covers used");
                foreach (RoomUseMedians x in kinds)
                    k.Add("  " + x.Kind, Int(x.Fights), Secs(x.Seconds), Pct(x.UsedShare), Pct(x.FarthestShare), Pct(x.MedianDepthShare), Metres(x.Travel),
                          Metres(x.Circle), Pct(x.DeathsDepthShare), Secs(x.SprintSeconds), CountText(x.Jumps), Pct(x.CoversUsedShare));
                k.Write(sb);
            }
            List<RoomUseRow> longest = rows.Where(r => r.Space != null && r.Places.Count > 0)
                                           .OrderByDescending(r => r.Measures.Seconds).ThenBy(r => r.Start).Take(RoomUseValues.MapsShown).ToList();
            if (longest.Count == 0) return;
            sb.AppendLine("The " + (longest.Count == 1 ? "longest fight" : Int(longest.Count) + " longest fights")
                          + ", drawn: the far wall at the top, the door she came in by at the bottom (^^), her left on the left; 2 m a cell.");
            sb.AppendLine("  her time in a cell: '.' none, ':' under 2 % of the fight (or passed through), 'o' under 6 %, '@' more;  # not floor;");
            sb.AppendLine("  T, L the middle of a tall or a low cover;  x an enemy died there, X more than one.");
            foreach (RoomUseRow r in longest)
            {
                RoomUseMeasures m = r.Measures;
                sb.Append("  ").Append(r.Run > 0 ? "run " + Int(r.Run) + ", " : "").Append("floor ").Append(Int(r.Floor)).Append(" room ").Append(Int(r.Room))
                  .Append(" (").Append(Empty(r.Kind)).Append(r.Template.Length > 0 ? ", " + r.Template : "").Append("), ").Append(Secs(m.Seconds))
                  .Append(", ").Append(Int(r.Enemies)).Append(" enemies: floor used ").Append(Pct(m.UsedShare)).Append(", 90% circle ").Append(Metres(m.Circle))
                  .Append(", farthest ").Append(Pct(m.FarthestShare)).Append(", travel ").Append(Metres(m.Travel)).AppendLine();
                foreach (string line in RoomUseRules.Map(r.Space, m, r.Deaths)) sb.Append("    ").AppendLine(line);
            }
        }
    }
}
