using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 093 in the session summary (one guard drags at a time, <see cref="DragHold"/>): under the defence, beside the
    /// pulls it already lists, the guards' Drags that pulled her, the Drags held back by the rule, and the shortest time between
    /// two Drags pulling her, so that a session shows whether the pulls still stack (Kristian 2026-11-23, floor 3: "når man blir
    /// dratt inn i alle sammen og insta drept"). With the rule on the shortest can be no less than
    /// <see cref="MeleeAttacks.DragOneAtATime"/> and a Drag's wind-up together.
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>A Drag that pulled her: her player_pulled event named by the guard's Drag.</summary>
        static bool IsDragPull(SessionEvent e) => e.Type == SessionEventTypes.PlayerPulled && e.Name == MeleeAttacks.Drag;

        /// <summary>The Drags of one scope's events: landed, held back, and the shortest seconds between two that landed.</summary>
        static void ComputeDrags(ScopeStats s, List<SessionEvent> events)
        {
            List<double> landed = events.Where(IsDragPull).Select(e => e.T).OrderBy(t => t).ToList();
            s.DragsLanded = landed.Count;
            s.DragsHeldBack = events.Count(e => e.Type == SessionEventTypes.DragHeldBack);
            s.DragGapShortest = float.NaN;
            for (int i = 1; i < landed.Count; i++)
            {
                float gap = (float)(landed[i] - landed[i - 1]);
                if (float.IsNaN(s.DragGapShortest) || gap < s.DragGapShortest) s.DragGapShortest = gap;
            }
        }

        /// <summary>The Defence table's two lines of the Drags, under "Stunned / pulled".</summary>
        static void AddDragRows(Table d, ScopeStats a, ScopeStats f, bool fights)
        {
            d.Add("  Drags landed / held back by the rule", Int(a.DragsLanded) + " / " + Int(a.DragsHeldBack),
                  fights ? Int(f.DragsLanded) + " / " + Int(f.DragsHeldBack) : "-");
            d.Add("  shortest between two Drags landing", Seconds(a.DragGapShortest), fights ? Seconds(f.DragGapShortest) : "-");
        }
    }
}
