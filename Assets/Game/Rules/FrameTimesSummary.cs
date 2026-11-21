using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>One of a part's slowest frames in the summary (work order 074), with what the recorder saw in the half second before it.</summary>
    public sealed class FrameHitchRow
    {
        public double Ms;
        /// <summary>Seconds since the session started (the log's clock) when it was seen.</summary>
        public double At;
        /// <summary>Seconds into its part (game time, as the floor times are).</summary>
        public double Into;
        /// <summary>What happened in the <see cref="FrameTimeEvents.CauseWindow"/> before it, in words joined by "; "; "" for nothing.</summary>
        public string Causes = "";
    }

    /// <summary>One line of the frame times' table: a floor of a run, or the practice room (all its stretches in one).</summary>
    public sealed class FramePartRow
    {
        public string Label;
        public readonly FrameHistogram Frames = new FrameHistogram();
        /// <summary>Its slowest frames, slowest first, at most <see cref="FrameTimeEvents.HitchesPerPart"/>.</summary>
        public readonly List<FrameHitchRow> Hitches = new List<FrameHitchRow>();
    }

    /// <summary>The frame times of a play session (work order 074): where it ran, a row per part, and the whole session.</summary>
    public sealed class FrameTimesStats
    {
        /// <summary>True when the log has any frame times: only then does the summary have the block (an older log has none).</summary>
        public bool Any;
        /// <summary>"editor" or "player", from the log's frame setup; "" when not known.</summary>
        public string RanIn = "";
        public double RefreshHz = double.NaN;
        /// <summary>QualitySettings.vSyncCount at the start: 0 off, 1 every refresh, 2 every second; -1 not known.</summary>
        public int VSync = -1;
        /// <summary>Application.targetFrameRate at the start: -1 the platform's default; null not known.</summary>
        public int? TargetFps;
        /// <summary>Whether the FrameTimingManager gave the main thread's and the GPU's times; null not known.</summary>
        public bool? FrameTiming;
        public readonly List<FramePartRow> Parts = new List<FramePartRow>();
        public readonly FrameHistogram Session = new FrameHistogram();
        public FramePartRow Part(string label) => Parts.FirstOrDefault(p => p.Label == label);
    }

    /// <summary>
    /// The summary's frame times block (work order 074), computed from the log's <see cref="FrameTimeEvents"/> and written
    /// in a method of its own, under every existing line. Per part (each floor of each run, and the practice room) and for
    /// the whole session: frames counted, the mean frame time and the frames a second it makes, the 95th and 99th
    /// percentile from the histogram, the slowest frame and how far into the part it was, the frames over 16.7 ms and over
    /// 33.3 ms, and the main thread's and the GPU's means where the FrameTimingManager gave them; one line above saying
    /// where it ran, the display's refresh rate and vsync; and under the table each part's five slowest frames with what
    /// the recorder saw in the half second before each: that is how a stutter gets a cause.
    /// </summary>
    public static class FrameTimesSummary
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>A part's name in the summary: "Run 2, floor 3", "Practice room".</summary>
        public static string Label(string part, int run, int floor)
        {
            if (part != FrameTimeEvents.PartFloor) return "Practice room";
            return run > 0 ? "Run " + run.ToString(Inv) + ", floor " + floor.ToString(Inv) : "Floor " + floor.ToString(Inv);
        }

        static string LabelOf(SessionEvent e) =>
            Label(e.ExtraValue("part"), FrameTimeEvents.Number(e, "run"), FrameTimeEvents.Number(e, "floor"));

        public static FrameTimesStats Compute(IReadOnlyList<SessionEvent> input)
        {
            var s = new FrameTimesStats();
            if (input == null) return s;
            List<SessionEvent> events = input.Where(e => e != null && e.Type != null).OrderBy(e => e.T).ToList();

            SessionEvent setup = events.FirstOrDefault(e => e.Type == FrameTimeEvents.Setup);
            if (setup != null)
            {
                s.RanIn = setup.ExtraValue("ran_in") ?? "";
                if (double.TryParse(setup.ExtraValue("refresh_hz"), NumberStyles.Float, Inv, out double hz)) s.RefreshHz = hz;
                if (int.TryParse(setup.ExtraValue("vsync"), NumberStyles.Integer, Inv, out int v)) s.VSync = v;
                if (int.TryParse(setup.ExtraValue("target_fps"), NumberStyles.Integer, Inv, out int fps)) s.TargetFps = fps;
                string timing = setup.ExtraValue("frame_timing");
                if (timing != null) s.FrameTiming = timing == "true";
            }

            foreach (SessionEvent e in events.Where(e => e.Type == FrameTimeEvents.Times))
            {
                string label = LabelOf(e);
                FramePartRow row = s.Part(label);
                if (row == null) s.Parts.Add(row = new FramePartRow { Label = label });
                FrameHistogram h = FrameTimeEvents.Read(e);
                row.Frames.Merge(h);
                s.Session.Merge(h);
                s.Any = true;
            }

            List<KeyValuePair<double, string>> notes = Notes(events);
            foreach (SessionEvent e in events.Where(e => e.Type == FrameTimeEvents.Hitch))
            {
                string label = LabelOf(e);
                FramePartRow row = s.Part(label);
                if (row == null) s.Parts.Add(row = new FramePartRow { Label = label });
                double at = FrameTimeEvents.Double(e, "at");
                row.Hitches.Add(new FrameHitchRow
                {
                    Ms = FrameTimeEvents.Double(e, "ms"), At = at, Into = FrameTimeEvents.Double(e, "into"), Causes = Causes(notes, at),
                });
            }
            // The practice room's stretches are one row: its five slowest over them all. The sort is stable: of two equal, the earlier.
            foreach (FramePartRow row in s.Parts)
            {
                List<FrameHitchRow> slowest = row.Hitches.OrderByDescending(h => h.Ms).Take(FrameTimeEvents.HitchesPerPart).ToList();
                row.Hitches.Clear();
                row.Hitches.AddRange(slowest);
            }
            return s;
        }

        // ---------------- What the recorder saw ----------------

        /// <summary>
        /// The events that may cost a frame, as (time, words), in the order of time: a floor built, a room entered, a run
        /// started or ended, an offer opened or answered, enemies waking and dying, her death and the hits she took, focus
        /// spent, the act boss's call, and the first use of each skill, of the basic shot and of the dash (of the session,
        /// or of the floor she is on), when what it shows is drawn for the first time.
        /// </summary>
        public static List<KeyValuePair<double, string>> Notes(IReadOnlyList<SessionEvent> events)
        {
            var notes = new List<KeyValuePair<double, string>>();
            var inSession = new HashSet<string>();
            var inPart = new HashSet<string>();
            string partWords = "in the practice room";
            foreach (SessionEvent e in events)
            {
                string word = null;
                switch (e.Type)
                {
                    case SessionEventTypes.SessionStart: word = "session started"; break;
                    case SessionEventTypes.LevelBuilt: word = "floor built"; break;
                    case SessionEventTypes.FloorEnter:
                        inPart.Clear();
                        partWords = "on this floor";
                        break;
                    case SessionEventTypes.RoomEntered: word = "room entered" + (string.IsNullOrEmpty(e.Name) ? "" : " (" + e.Name + ")"); break;
                    case SessionEventTypes.RunStart: word = "run started"; break;
                    case SessionEventTypes.RunEnd:
                        word = "run ended" + (string.IsNullOrEmpty(e.Result) ? "" : " (" + e.Result + ")");
                        inPart.Clear();
                        partWords = "in the practice room";
                        break;
                    case SessionEventTypes.RewardOffer: word = "floor reward offered"; break;
                    case SessionEventTypes.SkillUpgradeOffer: word = "skill upgrade offered"; break;
                    case SessionEventTypes.FocusChoiceOffer: word = "focus choice offered"; break;
                    case SessionEventTypes.ChestOpen: word = "chest upgrade taken"; break;
                    case SessionEventTypes.RewardTake:
                    case SessionEventTypes.SkillUpgradeTake:
                    case SessionEventTypes.FocusChoiceTake: word = "offer answered"; break;
                    case SessionEventTypes.EnemyWake: word = "enemy woke"; break;
                    case SessionEventTypes.EnemyDeath: word = "enemy died"; break;
                    case SessionEventTypes.PlayerDeath: word = "she died"; break;
                    case SessionEventTypes.PlayerDamaged: word = "she was hit"; break;
                    case SessionEventTypes.FocusSpent: word = "focus spent"; break;
                    case SessionEventTypes.BossCall: word = "boss called a pack"; break;
                    case SessionEventTypes.ShotStart:
                    case SessionEventTypes.SkillStart:
                    case SessionEventTypes.DashStart:
                    {
                        string name = e.Type == SessionEventTypes.DashStart ? SessionWords.Dash
                                    : e.Type == SessionEventTypes.ShotStart ? SessionWords.BasicShot : e.Name ?? "?";
                        if (inSession.Add(name)) word = "first " + name + " of the session";
                        else if (!inPart.Contains(name)) word = "first " + name + " " + partWords;
                        inPart.Add(name);
                        break;
                    }
                }
                if (word != null) notes.Add(new KeyValuePair<double, string>(e.T, word));
            }
            return notes;
        }

        /// <summary>
        /// What the recorder saw in the <see cref="FrameTimeEvents.CauseWindow"/> before a frame seen at <paramref name="at"/>
        /// (and in that same frame), in order, the same words counted once with "(x n)"; "" for nothing.
        /// </summary>
        public static string Causes(List<KeyValuePair<double, string>> notes, double at)
        {
            const double Edge = 0.0005; // the log's times are to the millisecond
            var words = new List<string>();
            var counts = new Dictionary<string, int>();
            foreach (KeyValuePair<double, string> note in notes)
            {
                if (note.Key < at - FrameTimeEvents.CauseWindow - Edge) continue;
                if (note.Key > at + Edge) break;
                if (counts.TryGetValue(note.Value, out int n)) counts[note.Value] = n + 1;
                else
                {
                    counts[note.Value] = 1;
                    words.Add(note.Value);
                }
            }
            return string.Join("; ", words.Select(w => counts[w] > 1 ? w + " (x" + counts[w].ToString(Inv) + ")" : w));
        }

        // ---------------- Writing ----------------

        /// <summary>The block, with a blank line before it; nothing for a log without frame times.</summary>
        public static void Write(StringBuilder sb, FrameTimesStats s)
        {
            if (s == null || !s.Any) return;
            sb.AppendLine();
            // Not the words "between runs": with that trial switched off the summary must not have them (BetweenRunsTests).
            sb.AppendLine("Frame times, in play only (not under the pause menu, an offer, the end screen or the rack before a run)");
            sb.Append("  ran ").Append(s.RanIn == "editor" ? "in the Editor" : s.RanIn == "player" ? "in a built player" : "where not known")
              .Append("   display ").Append(double.IsNaN(s.RefreshHz) || s.RefreshHz <= 0d ? "?" : s.RefreshHz.ToString("0.#", Inv)).Append(" Hz")
              .Append("   ").Append(VSyncWords(s.VSync))
              .Append("   target fps ").Append(s.TargetFps == null ? "?" : s.TargetFps.Value < 0 ? "default" : s.TargetFps.Value.ToString(Inv))
              .AppendLine(s.FrameTiming == false ? "   main thread and GPU not given (FrameTimingManager off)" : "");
            sb.Append("  in ms; the 95th and 99th percentile from a histogram of ").Append(FrameHistogram.BucketMs.ToString("0.##", Inv))
              .Append(" ms steps (to ").Append(FrameHistogram.BucketMs.ToString("0.##", Inv))
              .AppendLine(" ms), the slowest frame exact; Main and GPU the FrameTimingManager's means of the main thread and the GPU");

            var rows = new List<string[]>
            {
                new[] { "", "Frames", "Mean", "Fps", "95th", "99th", "Slowest", "into it", "Over 16.7", "Over 33.3", "Main", "GPU" },
            };
            foreach (FramePartRow p in s.Parts)
            {
                FrameHitchRow slowest = p.Hitches.FirstOrDefault();
                rows.Add(Cells(p.Label, p.Frames, slowest != null ? SessionSummary.Duration((float)slowest.Into) : "-"));
            }
            rows.Add(Cells("Whole session", s.Session, "-"));
            WriteColumns(sb, rows);

            if (s.Parts.All(p => p.Hitches.Count == 0)) return;
            sb.AppendLine();
            sb.Append("Hitches: the ").Append(FrameTimeEvents.HitchesPerPart).Append(" slowest frames of each, and what the recorder saw in the ")
              .Append(FrameTimeEvents.CauseWindow.ToString("0.0##", Inv)).AppendLine(" s before");
            foreach (FramePartRow p in s.Parts.Where(p => p.Hitches.Count > 0))
            {
                sb.Append("  ").AppendLine(p.Label);
                for (int i = 0; i < p.Hitches.Count; i++)
                {
                    FrameHitchRow h = p.Hitches[i];
                    sb.Append("    ").Append((i + 1).ToString(Inv)).Append(". ").Append(h.Ms.ToString("0.00", Inv)).Append(" ms, ")
                      .Append(SessionSummary.Duration((float)h.Into)).Append(" into it (log ").Append(h.At.ToString("0.000", Inv)).Append(" s): ")
                      .AppendLine(h.Causes.Length == 0 ? "nothing the recorder saw" : h.Causes);
                }
            }
        }

        static string VSyncWords(int vsync) =>
            vsync < 0 ? "vsync ?" : vsync == 0 ? "vsync off" : vsync == 1 ? "vsync on (every refresh)"
            : "vsync on (every " + vsync.ToString(Inv) + " refreshes)";

        static string[] Cells(string label, FrameHistogram h, string into) => new[]
        {
            label, h.Frames.ToString(Inv), Ms(h.MeanMs), Num(h.Fps, "0"), Ms(h.Percentile(0.95)), Ms(h.Percentile(0.99)),
            h.Frames > 0 ? Ms(h.MaxMs) : "-", into, h.Over60.ToString(Inv), h.Over30.ToString(Inv), Ms(h.MainMeanMs), Ms(h.GpuMeanMs),
        };

        static string Ms(double v) => Num(v, "0.00");
        static string Num(double v, string format) => double.IsNaN(v) || double.IsInfinity(v) ? "-" : v.ToString(format, Inv);

        /// <summary>Columns as the summary's tables have them: the first left-aligned, the rest right-aligned, two spaces apart.</summary>
        static void WriteColumns(StringBuilder sb, List<string[]> rows)
        {
            int columns = rows.Max(r => r.Length);
            var widths = new int[columns];
            foreach (string[] row in rows)
                for (int c = 0; c < row.Length; c++) widths[c] = Math.Max(widths[c], (row[c] ?? "").Length);
            foreach (string[] row in rows)
            {
                var line = new StringBuilder();
                for (int c = 0; c < columns; c++)
                {
                    string cell = c < row.Length ? row[c] ?? "" : "";
                    if (c == 0) line.Append(cell.PadRight(widths[c]));
                    else line.Append("  ").Append(cell.PadLeft(widths[c]));
                }
                sb.AppendLine(line.ToString().TrimEnd());
            }
        }
    }
}
