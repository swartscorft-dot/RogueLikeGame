using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>One boss fight of a session (work order 106), as the boss told it when the fight ended (<see cref="BossFightEvents"/>).</summary>
    public sealed class BossFightRow
    {
        /// <summary>s in phase one: from its waking to the break, or to the end of a fight that never broke.</summary>
        public double PhaseOne;
        /// <summary>s in phase two: from the break to the end; 0 without a break.</summary>
        public double PhaseTwo;
        public bool Broke;
        public int Slams;
        public int Landed;
        public int Avoided;
        /// <summary>How it ended: died, her death, removed.</summary>
        public string Ended = "";
    }

    /// <summary>The boss's line of the session summary (work order 106): its two phases and its Ground Slams, each fight in turn.</summary>
    public static partial class SessionSummary
    {
        /// <summary>The boss fights of a session, in the order they ended; none for a log from before, or with the finale off.</summary>
        public static List<BossFightRow> ComputeBossFights(IReadOnlyList<SessionEvent> events)
        {
            var rows = new List<BossFightRow>();
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;
            int Whole(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type != BossFightEvents.Fight) continue;
                rows.Add(new BossFightRow
                {
                    PhaseOne = Num(e, "one"), PhaseTwo = Num(e, "two"), Broke = e.ExtraValue("broke") == "1",
                    Slams = Whole(e, "slams"), Landed = Whole(e, "landed"), Avoided = Whole(e, "avoided"), Ended = e.ExtraValue("ended") ?? "",
                });
            }
            return rows;
        }

        /// <summary>
        /// The boss's line, one line for the whole session: "1 fight: phase one 62.0 s, phase two 48.3 s, Ground Slams 5 (landed 1,
        /// avoided 4), died". Several fights are written one after the other, apart by ";  ". Null for a session with none.
        /// </summary>
        public static string BossFightLine(IReadOnlyList<BossFightRow> fights)
        {
            if (fights == null || fights.Count == 0) return null;
            string Secs(double s) => Math.Max(0d, s).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            string Int(int n) => n.ToString(CultureInfo.InvariantCulture);
            string One(BossFightRow f) =>
                "phase one " + Secs(f.PhaseOne) + ", " + (f.Broke ? "phase two " + Secs(f.PhaseTwo) : "no break")
                + ", " + BossFinale.Slam + "s " + Int(f.Slams) + " (landed " + Int(f.Landed) + ", avoided " + Int(f.Avoided) + ")"
                + (f.Ended.Length > 0 ? ", " + f.Ended : "");
            return Int(fights.Count) + (fights.Count == 1 ? " fight: " : " fights: ") + string.Join(";  ", fights.Select(One));
        }
    }
}
