using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 092 in the session summary: the sprint. Beside the time on the move, for the whole session and in fights: the
    /// seconds sprinting and their share of the time (of the fights' time in the "In fights" column), the jumps over low props, the
    /// staggers from a hit while sprinting, and the clicks and skill keys that did nothing while she sprinted (so it shows if not
    /// being able to shoot frustrates). A sprint is a stretch that ends at its sprint_end, its seconds long.
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>The sprint's switch on at the session's start, or any of it in the log.</summary>
        static bool HasSprintIn(SessionEvent start, List<SessionEvent> events) =>
            (start != null && start.ExtraValue("feel.Sprint") == "true")
            || events.Any(e => e.Type == SessionEventTypes.SprintEnd || e.Type == SessionEventTypes.SprintJump || e.Type == SessionEventTypes.SprintRefused);

        /// <summary>The sprint's numbers of one scope: its seconds from all the sprints' stretches over the scope's, the counts from its events.</summary>
        static void ComputeSprint(ScopeStats s, List<SessionEvent> all, List<SessionEvent> events, List<KeyValuePair<double, double>> spans)
        {
            var sprints = new List<KeyValuePair<double, double>>();
            foreach (SessionEvent e in all)
                if (e.Type == SessionEventTypes.SprintEnd
                    && float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) && seconds > 0f)
                    sprints.Add(new KeyValuePair<double, double>(e.T - seconds, e.T));
            s.SprintSeconds = (float)Overlap(sprints, spans);
            s.SprintJumps = events.Count(e => e.Type == SessionEventTypes.SprintJump);
            s.SprintStaggers = events.Count(e => e.Type == SessionEventTypes.PlayerStunned && e.Name == SprintRules.StaggerName);
            s.SprintRefused = events.Count(e => e.Type == SessionEventTypes.SprintRefused);
        }

        /// <summary>The sprint's lines in the first table, under the time on the move and the ducks.</summary>
        static void AddSprintRows(Table t, ScopeStats a, ScopeStats f, bool fights)
        {
            t.Add("Sprinting: time, share of the time", Duration(a.SprintSeconds) + ", " + Percent(a.SprintShare),
                  fights ? Duration(f.SprintSeconds) + ", " + Percent(f.SprintShare) : "-");
            t.Add("  jumps over low props", Int(a.SprintJumps), fights ? Int(f.SprintJumps) : "-");
            t.Add("  staggers from a hit while sprinting", Int(a.SprintStaggers), fights ? Int(f.SprintStaggers) : "-");
            t.Add("  shots and skills pressed that did nothing", Int(a.SprintRefused), fights ? Int(f.SprintRefused) : "-");
        }
    }
}
