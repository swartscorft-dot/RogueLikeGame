using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>One Grave Ram fight of a session (work order 149), as the Ram told it when its fight ended (<see cref="GraveRamEvents"/>).</summary>
    public sealed class GraveRamRow
    {
        /// <summary>s from its fight's waking to its end; how it ended (died, her death, removed).</summary>
        public double Seconds;
        public string Ended = "";
        /// <summary>Her damage taken by its attacks.</summary>
        public int Ram, Stomp, Stones;
        /// <summary>Rams begun, those that crashed, missed and met her; what the crashes were on (";" between).</summary>
        public int Rams, Crashes, Missed, Met;
        public string CrashedOn = "";
        /// <summary>Stopping Shot dropped a Ram's wind-up this many times.</summary>
        public int Countered;
        /// <summary>s of crash dazes, of those she shot in, and her damage in them.</summary>
        public double Daze, DazeShot;
        public int DazeDamage;
        /// <summary>Her share of the fight below the standing speed (2.1 m/s).</summary>
        public double Slow;
        /// <summary>Its hits the hurt guard spared her; s of phase 2.</summary>
        public int Guarded;
        public double PhaseTwo;
    }

    /// <summary>
    /// The Grave Ram's block of the session summary (work order 149), "Elite (work order 149)": one row a fight, nothing for a session
    /// without one.
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>The Grave Ram's fights of a session, in the order they ended.</summary>
        public static List<GraveRamRow> ComputeGraveRam(IReadOnlyList<SessionEvent> events)
        {
            var rows = new List<GraveRamRow>();
            if (events == null) return rows;
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;
            int Whole(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type != GraveRamEvents.Fight) continue;
                rows.Add(new GraveRamRow
                {
                    Seconds = Num(e, "seconds"), Ended = e.ExtraValue("ended") ?? "",
                    Ram = Whole(e, "ram"), Stomp = Whole(e, "stomp"), Stones = Whole(e, "stones"),
                    Rams = Whole(e, "rams"), Crashes = Whole(e, "crashes"), Missed = Whole(e, "missed"), Met = Whole(e, "met"),
                    CrashedOn = e.ExtraValue("crashed_on") ?? "", Countered = Whole(e, "countered"),
                    Daze = Num(e, "daze"), DazeShot = Num(e, "daze_shot"), DazeDamage = Whole(e, "daze_damage"),
                    Slow = Num(e, "slow"), Guarded = Whole(e, "guarded"), PhaseTwo = Num(e, "two"),
                });
            }
            return rows;
        }

        /// <summary>The block: a table, a row for each fight's measure, a column a fight.</summary>
        static void WriteGraveRam(StringBuilder sb, List<GraveRamRow> fights)
        {
            if (fights == null || fights.Count == 0) return;
            string Secs(double s) => Math.Max(0d, s).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            string N(int n) => n.ToString(CultureInfo.InvariantCulture);
            sb.AppendLine();
            var header = new string[fights.Count + 1];
            header[0] = "Elite (work order 149): " + GraveRamValues.Title;
            for (int i = 0; i < fights.Count; i++) header[i + 1] = "fight " + N(i + 1);
            var t = new Table(header);
            void Row(string name, Func<GraveRamRow, string> cell)
            {
                var cells = new string[fights.Count + 1];
                cells[0] = name;
                for (int i = 0; i < fights.Count; i++) cells[i + 1] = cell(fights[i]);
                t.Add(cells);
            }
            Row("Fight, wake to end", f => Secs(f.Seconds) + (f.Ended.Length > 0 ? ", " + f.Ended : ""));
            Row("Phase 2", f => Secs(f.PhaseTwo));
            Row("Her damage taken: Ram / Stomp / Stones", f => N(f.Ram) + " / " + N(f.Stomp) + " / " + N(f.Stones));
            Row("Rams: crashed / missed / met her", f => N(f.Rams) + ": " + N(f.Crashes) + " / " + N(f.Missed) + " / " + N(f.Met));
            Row("Crashed on", f => f.CrashedOn.Length > 0 ? f.CrashedOn.Replace(";", ", ") : "-");
            Row("Stopping Shot dropped a Ram", f => f.Countered > 0 ? "yes, " + N(f.Countered) : "no");
            Row("Crash dazes / she shot in them", f => Secs(f.Daze) + " / " + Secs(f.DazeShot));
            Row("Her damage in the crash dazes", f => N(f.DazeDamage));
            Row("Her time below 2.1 m/s", f => Math.Round(f.Slow * 100d).ToString(CultureInfo.InvariantCulture) + " %");
            Row("Hits the hurt guard spared", f => N(f.Guarded));
            t.Write(sb);
        }
    }
}
