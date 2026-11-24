using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The music over a session (work order 116), from the recorder's <see cref="MusicEvents.Change"/> lines: the session's
    /// seconds in each state (the recorder's clock, which stops in the pause as every other count of the summary does), from the
    /// first change to the session's end, and how many changes there were after the music's start.
    /// </summary>
    public sealed class MusicStats
    {
        public double Calm, Fight, Boss, Silent, Off;
        /// <summary>Changes of state after the first start (a change from none, its very first, not counted).</summary>
        public int Changes;
        /// <summary>The recorder's music lines read.</summary>
        public int Lines;
    }

    public static partial class SessionSummary
    {
        /// <summary>The music of a session (work order 116); a log with no music line gives none (0 lines).</summary>
        public static MusicStats ComputeMusic(IReadOnlyList<SessionEvent> events, double length)
        {
            var stats = new MusicStats();
            MusicState state = MusicState.None;
            double since = 0.0;
            bool begun = false;
            foreach (SessionEvent e in events)
            {
                if (e.Type != MusicEvents.Change) continue;
                stats.Lines++;
                if (begun) Add(stats, state, e.T - since);
                MusicState from = MusicRules.FromWord(e.ExtraValue("from"));
                MusicState to = MusicRules.FromWord(e.ExtraValue("to") ?? e.Name);
                if (from != MusicState.None) stats.Changes++;
                state = to;
                since = e.T;
                begun = true;
            }
            if (begun) Add(stats, state, length - since);
            return stats;
        }

        static void Add(MusicStats s, MusicState state, double seconds)
        {
            if (seconds <= 0.0) return;
            switch (state)
            {
                case MusicState.Calm: s.Calm += seconds; break;
                case MusicState.Fight: s.Fight += seconds; break;
                case MusicState.Boss: s.Boss += seconds; break;
                case MusicState.Silent: s.Silent += seconds; break;
                default: s.Off += seconds; break;
            }
        }

        /// <summary>
        /// The one line (work order 116): "calm 312.4 s, fight 120.0 s, boss 0.0 s, silent 4.5 s, off 0.0 s; 14 changes". Null for a
        /// session with no music line.
        /// </summary>
        public static string MusicLine(MusicStats s)
        {
            if (s == null || s.Lines == 0) return null;
            string S(double v) => v.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            return "calm " + S(s.Calm) + ", fight " + S(s.Fight) + ", boss " + S(s.Boss) + ", silent " + S(s.Silent) + ", off " + S(s.Off)
                   + "; " + s.Changes.ToString(CultureInfo.InvariantCulture) + (s.Changes == 1 ? " change" : " changes");
        }
    }
}
