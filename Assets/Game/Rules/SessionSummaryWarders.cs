using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The warders' block of the session summary (work order 079): what is counted and how it is written (work order 081 moved it
    /// here from SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>
        /// The warders (work order 079), whole session: wards raised (ward_raised), wards ended (ward_broken) by why, the damage
        /// they took off her hits, and warders killed (an enemy_death of a warder).
        /// </summary>
        static void ComputeWarders(List<SessionEvent> events, SessionStats stats)
        {
            const string Warder = "warder";
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.WardRaised) stats.WardsRaised++;
                else if (e.Type == SessionEventTypes.WardBroken)
                {
                    stats.WardedAway += e.Damage ?? 0;
                    string why = e.ExtraValue("why");
                    if (why == SessionWords.WardAllyGone || why == SessionWords.WardWentHome) continue;
                    stats.WardsBroken++;
                    if (why == SessionWords.WarderKilled) stats.WardsBrokenByKill++;
                    else if (why == SessionWords.WarderStaggered) stats.WardsBrokenByStagger++;
                    else if (why == SessionWords.WardTooFar) stats.WardsBrokenByDistance++;
                }
                else if (e.Type == SessionEventTypes.EnemyDeath && e.Enemy != null && e.Enemy.EndsWith(Warder, StringComparison.Ordinal))
                    stats.WardersKilled++;
            }
        }

        /// <summary>The warders' lines (work order 079), only in a log that has a ward in it.</summary>
        static void WriteWarders(StringBuilder sb, SessionStats stats)
        {
            if (stats.WardsRaised <= 0) return;
            sb.AppendLine();
            var t = new Table("Warders, whole session", "");
            t.Add("Wards raised / broken", stats.WardsRaised + " / " + stats.WardsBroken);
            t.Add("Broken by its death / stagger / distance", stats.WardsBrokenByKill + " / " + stats.WardsBrokenByStagger + " / " + stats.WardsBrokenByDistance);
            t.Add("Warded away (damage)", Int(stats.WardedAway));
            t.Add("Warders killed", Int(stats.WardersKilled));
            t.Write(sb);
        }
    }
}
