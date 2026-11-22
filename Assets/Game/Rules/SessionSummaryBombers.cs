using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The bombers' block of the session summary (work order 073): what is counted and how it is written (work order 081 moved it
    /// here from SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>
        /// The bombers (work order 073), whole session: their blasts (bomber_blast), those that hurt her (an enemy_strike of a
        /// bomber's Blast that landed), the enemies the blasts hurt and those they killed.
        /// </summary>
        static void ComputeBombers(List<SessionEvent> events, SessionStats stats)
        {
            const string Bomber = "bomber";
            int Number(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.BomberBlast)
                {
                    stats.BomberBlasts++;
                    stats.BlastEnemiesHurt += Number(e, "enemies");
                    stats.BlastKills += Number(e, "killed");
                }
                else if (e.Type == SessionEventTypes.EnemyStrike && e.Name == BomberValues.Blast && e.Result == SessionWords.Landed
                         && e.Enemy != null && e.Enemy.EndsWith(Bomber, StringComparison.Ordinal))
                    stats.BomberBlastsOnHer++;
            }
        }

        /// <summary>The bombers' lines (work order 073), only in a log that has a blast in it.</summary>
        static void WriteBombers(StringBuilder sb, SessionStats stats)
        {
            if (stats.BomberBlasts <= 0) return;
            sb.AppendLine();
            var t = new Table("Bombers, whole session", "");
            t.Add("Blasts / that hurt her", stats.BomberBlasts + " / " + stats.BomberBlastsOnHer);
            t.Add("Enemies hurt by a blast", Int(stats.BlastEnemiesHurt));
            t.Add("Enemies killed by a blast", Int(stats.BlastKills));
            t.Write(sb);
        }
    }
}
