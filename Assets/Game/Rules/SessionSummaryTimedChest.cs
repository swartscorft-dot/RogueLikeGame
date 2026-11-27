using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>One floor's timed chest of a session (work order 153), as the game wrote it (<see cref="TimedChestEvents"/>).</summary>
    public sealed class TimedChestRow
    {
        public int Floor, Room;
        /// <summary>How it ended: taken, died, not opened, left.</summary>
        public string Ended = "";
        public bool Opened;
        /// <summary>s from the room's clearing to the opening, and from the opening to its wave's last kill; under 0 for none.</summary>
        public double AfterClear = -1d, Clear = -1d;
        /// <summary>Its wave's kinds ("swarmer 4, brute 2") and count.</summary>
        public string Kinds = "";
        public int Count;
        /// <summary>The share of its whole left at the last kill (under 0: not cleared), the Echoes it held whole and those taken, her damage taken in its fight.</summary>
        public double Share = -1d;
        public int Whole, Echoes, Damage;
    }

    /// <summary>The timed chest's block of the session summary (work order 153), "Timed chest (work order 153)": one row a floor that held one, nothing for a session without one.</summary>
    public static partial class SessionSummary
    {
        /// <summary>The timed chests of a session, in the order they were written.</summary>
        public static List<TimedChestRow> ComputeTimedChests(IReadOnlyList<SessionEvent> events)
        {
            var rows = new List<TimedChestRow>();
            if (events == null) return rows;
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : -1d;
            int Whole(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type != TimedChestEvents.Chest) continue;
                rows.Add(new TimedChestRow
                {
                    Floor = Whole(e, "floor"), Room = Whole(e, "room"), Ended = e.Name ?? "", Opened = e.ExtraValue("opened") == "true",
                    AfterClear = Num(e, "after_clear"), Clear = Num(e, "clear"), Kinds = e.ExtraValue("kinds") ?? "", Count = Whole(e, "count"),
                    Share = Num(e, "share"), Whole = Whole(e, "whole"), Echoes = Whole(e, "echoes"), Damage = Whole(e, "damage"),
                });
            }
            return rows;
        }

        /// <summary>The block: a table, a row for each measure, a column a chest.</summary>
        static void WriteTimedChests(StringBuilder sb, List<TimedChestRow> chests)
        {
            if (chests == null || chests.Count == 0) return;
            string Secs(double s) => s < 0d ? "-" : s.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            string N(int n) => n.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine();
            var header = new string[chests.Count + 1];
            header[0] = "Timed chest (work order 153)";
            for (int i = 0; i < chests.Count; i++) header[i + 1] = "floor " + N(chests[i].Floor) + ", room " + N(chests[i].Room);
            var t = new Table(header);
            void Row(string name, Func<TimedChestRow, string> cell)
            {
                var cells = new string[chests.Count + 1];
                cells[0] = name;
                for (int i = 0; i < chests.Count; i++) cells[i + 1] = cell(chests[i]);
                t.Add(cells);
            }
            Row("Opened", c => c.Opened ? "yes, " + Secs(c.AfterClear) + " after the room was cleared" : "no");
            Row("Its wave", c => c.Opened ? c.Kinds + " (" + N(c.Count) + ")" : "-");
            Row("Time to clear it", c => Secs(c.Clear));
            Row("Reward left / Echoes taken", c => c.Share < 0d ? "- / " + N(c.Echoes) + " of " + N(c.Whole)
                                                   : Math.Round(c.Share * 100d).ToString(CultureInfo.InvariantCulture) + " % / " + N(c.Echoes) + " of " + N(c.Whole));
            Row("Her damage taken in it", c => c.Opened ? N(c.Damage) : "-");
            Row("Ended", c => c.Ended);
            t.Write(sb);
        }
    }
}
