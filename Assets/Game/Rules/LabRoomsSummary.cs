using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The room lab's events in the play session's log (work order 084): a room built (its template, kind, mix, floor and
    /// seed, and how long the build took), cleared (the seconds from its fight's start, or for a treasure room from its build
    /// to its chest), and left (how: built again, another room, she died, back to the practice room).
    /// </summary>
    public static class LabRoomEvents
    {
        public const string Room = "lab_room";   // name: the template's name (or "drawn shape", "treasure room"); kind, mix, floor, seed, ms
        public const string Clear = "lab_clear"; // seconds
        public const string End = "lab_end";     // result: how it was left
    }

    /// <summary>One lab room of a session: its template, kind, mix and seed, and what happened in it over every try.</summary>
    public sealed class LabRoomRow
    {
        public string Name = "", Kind = "", Mix = "", Seed = "";
        public int Tries, Clears, Deaths, Damage;
        public readonly List<float> ClearSeconds = new List<float>();
        public readonly List<float> BuildMs = new List<float>();

        public float MeanClear => ClearSeconds.Count > 0 ? ClearSeconds.Average() : float.NaN;
        public float BestClear => ClearSeconds.Count > 0 ? ClearSeconds.Min() : float.NaN;
        public float DamagePerTry => Tries > 0 ? Damage / (float)Tries : float.NaN;
    }

    /// <summary>
    /// The summary's table of the room lab (work order 084): one row for each lab room (the same template, kind, mix and seed
    /// built again counts as another try of the same room), in the order first built, with its tries, how many were cleared,
    /// the time to clear (the mean and the best), the damage she took (in all and a try) and her deaths, so two versions of a
    /// room can be compared from the logs. A try runs from its lab_room event to the lab_end after it (or the next lab_room,
    /// a run's start, the session's end); her damage and deaths in that span are the try's.
    /// </summary>
    public static class LabRoomsSummary
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public static List<LabRoomRow> Compute(IReadOnlyList<SessionEvent> events)
        {
            var rows = new List<LabRoomRow>();
            var byKey = new Dictionary<string, LabRoomRow>();
            LabRoomRow open = null;
            foreach (SessionEvent e in events)
            {
                if (e == null) continue;
                if (e.Type == LabRoomEvents.Room)
                {
                    string name = e.Name ?? "?", kind = e.ExtraValue("kind") ?? "", mix = e.ExtraValue("mix") ?? "", seed = e.ExtraValue("seed") ?? "";
                    string key = name + "|" + kind + "|" + mix + "|" + seed;
                    if (!byKey.TryGetValue(key, out LabRoomRow row))
                    {
                        byKey[key] = row = new LabRoomRow { Name = name, Kind = kind, Mix = mix, Seed = seed };
                        rows.Add(row);
                    }
                    row.Tries++;
                    if (float.TryParse(e.ExtraValue("ms"), NumberStyles.Float, Inv, out float ms)) row.BuildMs.Add(ms);
                    open = row;
                    continue;
                }
                if (open == null) continue;
                if (e.Type == LabRoomEvents.End || e.Type == SessionEventTypes.RunStart)
                {
                    open = null;
                    continue;
                }
                if (e.Type == LabRoomEvents.Clear)
                {
                    open.Clears++;
                    if (float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, Inv, out float s)) open.ClearSeconds.Add(s);
                }
                else if (e.Type == SessionEventTypes.PlayerDamaged) open.Damage += e.Damage ?? 0;
                else if (e.Type == SessionEventTypes.PlayerDeath) open.Deaths++;
            }
            return rows;
        }

        static string Seconds(float s) => float.IsNaN(s) ? "-" : s.ToString("0.0", Inv) + " s";

        /// <summary>The table, under its title; nothing at all for a session without the lab.</summary>
        public static void Write(StringBuilder sb, IReadOnlyList<LabRoomRow> rows)
        {
            if (rows == null || rows.Count == 0) return;
            var lines = new List<string[]>
            {
                new[] { "Room lab (work order 084), per room", "Kind", "Mix", "Seed", "Tries", "Cleared", "Clear mean", "Clear best", "Damage", "Per try", "Deaths" },
            };
            foreach (LabRoomRow r in rows)
                lines.Add(new[]
                {
                    r.Name, r.Kind, r.Mix, r.Seed, r.Tries.ToString(Inv), r.Clears.ToString(Inv), Seconds(r.MeanClear), Seconds(r.BestClear),
                    r.Damage.ToString(Inv), float.IsNaN(r.DamagePerTry) ? "-" : r.DamagePerTry.ToString("0", Inv), r.Deaths.ToString(Inv),
                });
            int columns = lines[0].Length;
            var widths = new int[columns];
            foreach (string[] line in lines)
                for (int c = 0; c < columns; c++) widths[c] = Math.Max(widths[c], line[c].Length);
            sb.AppendLine();
            foreach (string[] line in lines)
            {
                var text = new StringBuilder();
                for (int c = 0; c < columns; c++)
                {
                    if (c == 0) text.Append(line[c].PadRight(widths[c]));
                    else if (c < 4) text.Append("  ").Append(line[c].PadRight(widths[c]));
                    else text.Append("  ").Append(line[c].PadLeft(widths[c]));
                }
                sb.AppendLine(text.ToString().TrimEnd());
            }
            List<float> builds = rows.SelectMany(r => r.BuildMs).ToList();
            if (builds.Count > 0)
                sb.AppendLine(string.Format(Inv, "Lab rooms built: {0}, in {1:0} ms on average, {2:0} ms at most", builds.Count, builds.Average(), builds.Max()));
        }
    }
}
