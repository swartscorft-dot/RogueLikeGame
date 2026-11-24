using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The three new skills (work order 109) in the log: each jump of Ricochet with the target it hit, each cloud of Smoke Round
    /// with where it stood and how long, each volley of Deadeye with how many it shot at, and a Deadeye press refused for want
    /// of a marked enemy in sight (a skill_press, rejected, reason "no marked target"). Their hits, marks and spends are logged
    /// as every skill's are.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>One of her reports, if it is the new skills' own: logged, and true; anything else false.</summary>
        bool RecordNewSkills(PlayerReport r, double t)
        {
            switch (r.Kind)
            {
                case PlayerReportKind.SkillPressed when r.NoTarget:
                    Add(new SessionEvent(t, SessionEventTypes.SkillPress, SkillName(r.Slot))
                    {
                        Slot = r.Slot, Result = SessionWords.Rejected, Reason = SessionWords.NoMarkedTarget,
                    });
                    return true;
                case PlayerReportKind.RicochetJumped:
                    Add(new SessionEvent(t, SessionEventTypes.RicochetJump, SkillName(r.Slot))
                    {
                        Slot = r.Slot, Target = TargetKind(r.Target), Shot = r.Shot,
                    });
                    return true;
                case PlayerReportKind.SmokeLanded:
                {
                    var e = new SessionEvent(t, SessionEventTypes.SmokeCloud, SkillName(r.Slot)) { Slot = r.Slot };
                    e.AddExtra("seconds", Text(r.Seconds));
                    e.AddExtra("x", r.At.X.ToString("0.##", CultureInfo.InvariantCulture));
                    e.AddExtra("z", r.At.Z.ToString("0.##", CultureInfo.InvariantCulture));
                    Add(e);
                    return true;
                }
                case PlayerReportKind.DeadeyeFired:
                    Add(new SessionEvent(t, SessionEventTypes.DeadeyeVolley, SkillName(r.Slot)) { Slot = r.Slot, Shot = r.Shot });
                    return true;
                default:
                    return false;
            }
        }
    }
}
