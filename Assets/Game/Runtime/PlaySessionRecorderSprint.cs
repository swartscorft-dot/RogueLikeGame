using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The sprint (work order 092) in the log: a sprint's end with its seconds and why, a jump over a low prop, a click or a skill key
    /// that did nothing while she sprinted. Its stagger is the stun's line (<see cref="SessionEventTypes.PlayerStunned"/>, named
    /// <see cref="SprintRules.StaggerName"/>).
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>One of her reports, if it is the sprint's: logged, and true; anything else false.</summary>
        bool RecordSprint(PlayerReport r, double t)
        {
            switch (r.Kind)
            {
                case PlayerReportKind.SprintStarted:
                    return true; // its end carries its seconds
                case PlayerReportKind.SprintEnded:
                {
                    var e = new SessionEvent(t, SessionEventTypes.SprintEnd) { Result = r.By };
                    e.AddExtra("seconds", Text(r.Seconds));
                    Add(e);
                    return true;
                }
                case PlayerReportKind.SprintJumped:
                {
                    var e = new SessionEvent(t, SessionEventTypes.SprintJump, r.By);
                    e.AddExtra("metres", Text(r.Metres));
                    Add(e);
                    return true;
                }
                case PlayerReportKind.SprintRefused:
                    Add(new SessionEvent(t, SessionEventTypes.SprintRefused, r.Slot >= 0 ? SkillName(r.Slot) : SessionWords.BasicShot)
                    {
                        Slot = r.Slot >= 0 ? r.Slot : (int?)null,
                    });
                    return true;
                default:
                    return false;
            }
        }
    }
}
