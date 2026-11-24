using System.Collections.Generic;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>The three new skills' own counts in a session (work order 109), whole session.</summary>
    public sealed class NewSkillStats
    {
        /// <summary>Ricochet's uses that went off, the hits they made (the first and every jump), and the jumps alone.</summary>
        public int RicochetUses, RicochetHits, RicochetJumps;
        /// <summary>Smoke Round's clouds.</summary>
        public int SmokeClouds;
        /// <summary>Deadeye's volleys, the shots in them (one a marked enemy), and its presses refused for want of a marked enemy in sight.</summary>
        public int DeadeyeVolleys, DeadeyeShots, DeadeyeRefused;

        public bool Any => RicochetUses + SmokeClouds + DeadeyeVolleys + DeadeyeRefused > 0;
    }

    /// <summary>The three new skills' block of the session summary (work order 109): what is counted, and how it is written.</summary>
    public static partial class SessionSummary
    {
        /// <summary>
        /// From the log: Ricochet's skill_fired and its hits (a hit event of it) and ricochet_jump; Smoke Round's smoke_cloud;
        /// Deadeye's deadeye_volley (its shots in "shot") and its presses rejected for "no marked target".
        /// </summary>
        static NewSkillStats ComputeNewSkills(List<SessionEvent> events)
        {
            var s = new NewSkillStats();
            foreach (SessionEvent e in events)
            {
                switch (e.Type)
                {
                    case SessionEventTypes.SkillFired when e.Name == NewSkillRules.Ricochet:
                        s.RicochetUses++;
                        break;
                    case SessionEventTypes.Hit when e.Name == NewSkillRules.Ricochet:
                        s.RicochetHits++;
                        break;
                    case SessionEventTypes.RicochetJump:
                        s.RicochetJumps++;
                        break;
                    case SessionEventTypes.SmokeCloud:
                        s.SmokeClouds++;
                        break;
                    case SessionEventTypes.DeadeyeVolley:
                        s.DeadeyeVolleys++;
                        s.DeadeyeShots += e.Shot ?? 0;
                        break;
                    case SessionEventTypes.SkillPress when e.Reason == SessionWords.NoMarkedTarget:
                        s.DeadeyeRefused++;
                        break;
                }
            }
            return s;
        }

        /// <summary>The block, only in a log where one of the three was used or pressed.</summary>
        static void WriteNewSkills(StringBuilder sb, NewSkillStats s)
        {
            if (s == null || !s.Any) return;
            sb.AppendLine();
            var t = new Table("The new skills (work order 109), whole session", "");
            t.Add("Ricochet: uses / hits / of them jumps", s.RicochetUses + " / " + s.RicochetHits + " / " + s.RicochetJumps);
            t.Add("Smoke Round: clouds", Int(s.SmokeClouds));
            t.Add("Deadeye: volleys / shots / presses refused (no marked enemy)", s.DeadeyeVolleys + " / " + s.DeadeyeShots + " / " + s.DeadeyeRefused);
            t.Write(sb);
        }
    }
}
