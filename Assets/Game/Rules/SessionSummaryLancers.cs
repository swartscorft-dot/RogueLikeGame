using System;
using System.Collections.Generic;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>The lancers' counts in a session (work order 108), filled by <see cref="SessionSummary"/>.</summary>
    public sealed class LancerStats
    {
        /// <summary>Charges wound up (an enemy_attack of a lancer's <see cref="LancerValues.Charge"/>).</summary>
        public int Charges;
        /// <summary>Its charge met her and hurt her.</summary>
        public int Landed;
        /// <summary>She was out of its line (she stepped aside, or it ran out short of her) or in the dash's protected part.</summary>
        public int Avoided;
        /// <summary>A wall, a shut door or a prop stopped it with her behind it: cover stopped it.</summary>
        public int StoppedByCover;
        /// <summary>Stopping Shot from its front dropped its wind-up (an enemy_countered of a lancer).</summary>
        public int Countered;
        /// <summary>Its charge crashed (on a wall, a shut door or a prop, whether she was behind it or not): it was dazed.</summary>
        public int Crashes;
        /// <summary>Of the crashes, what stopped it: by the log's words (<see cref="LancerRules.StopperWord"/>), in the order first met.</summary>
        public readonly List<KeyValuePair<string, int>> CrashedOn = new List<KeyValuePair<string, int>>();
    }

    /// <summary>
    /// The lancers' block of the session summary (work order 108): what is counted and how it is written. In the table of the
    /// enemies by kind the lancer has its row of its own, and under it its Charge's strikes, landed and avoided, as every kind's
    /// attacks (work order 035); a crash with her behind what stopped it is neither (its strike is "stopped by a wall", counted
    /// in the defence table's "stopped by a wall or prop"): here it is "stopped by cover", beside the counters and the crashes.
    /// </summary>
    public static partial class SessionSummary
    {
        const string LancerWord = "lancer";

        static bool OfALancer(SessionEvent e) => e.Enemy != null && e.Enemy.EndsWith(LancerWord, StringComparison.Ordinal);

        /// <summary>
        /// The lancers (work order 108), whole session: charges wound up, landed, avoided (out of its area or the dash), stopped by
        /// cover (its strike stopped by a wall or prop), countered, and crashes (a strike with the "by" of what stopped it).
        /// </summary>
        public static LancerStats ComputeLancers(List<SessionEvent> events)
        {
            var s = new LancerStats();
            if (events == null) return s;
            foreach (SessionEvent e in events)
            {
                if (!OfALancer(e)) continue;
                if (e.Type == SessionEventTypes.EnemyAttack && e.Name == LancerValues.Charge) s.Charges++;
                else if (e.Type == SessionEventTypes.EnemyCountered) s.Countered++;
                else if (e.Type == SessionEventTypes.EnemyStrike && e.Name == LancerValues.Charge)
                {
                    if (e.Result == SessionWords.Landed) s.Landed++;
                    else if (e.Result == SessionWords.OutOfArea || e.Result == SessionWords.AvoidedByDash) s.Avoided++;
                    else if (e.Result == SessionWords.StoppedByWall) s.StoppedByCover++;
                    string by = e.ExtraValue(LancerCrashExtra);
                    if (string.IsNullOrEmpty(by)) continue;
                    s.Crashes++;
                    int at = s.CrashedOn.FindIndex(p => p.Key == by);
                    if (at < 0) s.CrashedOn.Add(new KeyValuePair<string, int>(by, 1));
                    else s.CrashedOn[at] = new KeyValuePair<string, int>(by, s.CrashedOn[at].Value + 1);
                }
            }
            return s;
        }

        /// <summary>The extra on a lancer's crashed strike that says what stopped it ("wall", "door", "tall prop", "low prop", "edge").</summary>
        public const string LancerCrashExtra = "by";

        /// <summary>The lancers' lines (work order 108), only in a log that has a lancer's charge in it.</summary>
        static void WriteLancers(StringBuilder sb, LancerStats s)
        {
            if (s == null || s.Charges <= 0) return;
            sb.AppendLine();
            var t = new Table("Lancers, whole session", "");
            t.Add("Charges / countered", s.Charges + " / " + s.Countered);
            t.Add("Charge landed / avoided / stopped by cover", s.Landed + " / " + s.Avoided + " / " + s.StoppedByCover);
            var on = new StringBuilder();
            foreach (KeyValuePair<string, int> p in s.CrashedOn) on.Append(on.Length > 0 ? ", " : "").Append(p.Key).Append(' ').Append(p.Value);
            t.Add("Crashes (dazed)", s.Crashes + (on.Length > 0 ? " (" + on + ")" : ""));
            t.Write(sb);
        }
    }
}
