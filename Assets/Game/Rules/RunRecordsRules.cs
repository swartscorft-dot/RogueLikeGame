using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// His records across runs for the start menu's STATS tab (work order 114: "Stats og annet morro"; the order's list: runs
    /// started, deaths, acts cleared, the deepest floor, kills, the longest run, Echoes earned in all, feats). The game kept none
    /// of the first six before: they start at zero on 2026-11-24 and are kept from then on, in a save of their own beside the
    /// others (<see cref="RecordsFile"/>; not in between-runs.json, whose version other orders are changing). Echoes earned in
    /// all and the feats are the between-runs trial's own (<see cref="MetaState.EarnedInAll"/>, <see cref="MetaState.Feats"/>)
    /// and are read from there, not kept twice.
    /// </summary>
    public sealed class RunRecords
    {
        /// <summary>The records file's version: 1, the first.</summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        public int RunsStarted;
        public int Deaths;
        public int ActsCleared;
        /// <summary>The deepest floor reached in a run (0 before the first).</summary>
        public int DeepestFloor;
        public int Kills;
        /// <summary>The longest run, in game seconds (a run's own clock, the end screen's).</summary>
        public float LongestRunSeconds;

        public RunRecords Copy() => (RunRecords)MemberwiseClone();
    }

    /// <summary>What a run adds to the records (work order 114).</summary>
    public static class RecordsRules
    {
        /// <summary>A run started.</summary>
        public static void NoteStarted(RunRecords r)
        {
            if (r == null) return;
            r.RunsStarted++;
        }

        /// <summary>A run ended: how, the floor it reached, its kills and its time.</summary>
        public static void NoteEnded(RunRecords r, RunOutcome outcome, int floor, int kills, float seconds)
        {
            if (r == null) return;
            if (outcome == RunOutcome.Died) r.Deaths++;
            if (outcome == RunOutcome.ActCleared) r.ActsCleared++;
            if (floor > r.DeepestFloor) r.DeepestFloor = floor;
            if (kills > 0) r.Kills += kills;
            if (!float.IsNaN(seconds) && seconds > r.LongestRunSeconds) r.LongestRunSeconds = seconds;
        }

        /// <summary>
        /// The STATS tab's lines, name and value each, in the order's order. <paramref name="meta"/>: the trial's save, or null
        /// with the trial off (its two lines then say so).
        /// </summary>
        public static void Lines(RunRecords r, MetaState meta, string currencyName, List<string> names, List<string> values)
        {
            names.Clear();
            values.Clear();
            r = r ?? new RunRecords();
            CultureInfo inv = CultureInfo.InvariantCulture;
            void Add(string n, string v)
            {
                names.Add(n);
                values.Add(v);
            }
            Add("Runs started", r.RunsStarted.ToString(inv));
            Add("Deaths", r.Deaths.ToString(inv));
            Add("Acts cleared", r.ActsCleared.ToString(inv));
            Add("Deepest floor", r.DeepestFloor > 0 ? "floor " + r.DeepestFloor.ToString(inv) : "-");
            Add("Kills", r.Kills.ToString(inv));
            Add("Longest run", r.LongestRunSeconds > 0f ? RunWords.Clock(r.LongestRunSeconds) : "-");
            Add((currencyName ?? "Echoes") + " earned in all", meta != null ? meta.EarnedInAll.ToString(inv) : "trial off");
            int feats = Enum.GetValues(typeof(MetaFeat)).Length;
            Add("Feats", meta != null ? meta.Feats.Count.ToString(inv) + " of " + feats.ToString(inv) : "trial off");
        }
    }

    /// <summary>How the records file was read (work order 114).</summary>
    public enum RecordsLoad
    {
        /// <summary>No file: zeros.</summary>
        Missing,
        /// <summary>Read; an older file's fields it lacks are zeros.</summary>
        Read,
        /// <summary>Not a records file this game can read: renamed beside itself (never deleted), and zeros.</summary>
        Damaged,
        /// <summary>A newer version's: left as it is and never written over by this game; zeros shown.</summary>
        Newer,
        /// <summary>There but not readable now: left alone, never written over; tried again the next time.</summary>
        Unreadable,
    }

    /// <summary>
    /// The records' own small JSON file (work order 114), at a path the caller gives (the game's beside between-runs.json under
    /// the game's data folder; tests give a temporary one). Read and written as the between-runs save is (work order 029 and
    /// 033's rules, simpler): reading never throws, a missing or older file reads as zeros where it says nothing, a damaged one is
    /// kept aside renamed, a newer or an unreadable one is never written over; writing goes through a temporary file.
    /// </summary>
    public static class RecordsFile
    {
        public static RunRecords Load(string path, out RecordsLoad how, out string keptAs)
        {
            keptAs = "";
            how = RecordsLoad.Missing;
            var zeros = new RunRecords();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return zeros;
                string text = File.ReadAllText(path, Encoding.UTF8);
                RunRecords read;
                try
                {
                    read = RecordsJson.Read(text);
                }
                catch (FormatException)
                {
                    how = RecordsLoad.Damaged;
                    keptAs = KeepAside(path);
                    return zeros;
                }
                if (read.Version > RunRecords.CurrentVersion)
                {
                    how = RecordsLoad.Newer;
                    return zeros;
                }
                read.Version = RunRecords.CurrentVersion;
                how = RecordsLoad.Read;
                return read;
            }
            catch (Exception)
            {
                how = RecordsLoad.Unreadable;
                return zeros;
            }
        }

        /// <summary>May the records be written over what was read: not over a newer version's file, nor one that could not be read.</summary>
        public static bool MayWrite(RecordsLoad how) => how != RecordsLoad.Newer && how != RecordsLoad.Unreadable;

        /// <summary>Writes the records. False, with the reason, when it could not; never throws.</summary>
        public static bool Save(string path, RunRecords r, out string error)
        {
            error = "";
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, RecordsJson.Write(r), new UTF8Encoding(false));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        static string KeepAside(string path)
        {
            try
            {
                string to = path + ".damaged-" + DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                for (int n = 2; File.Exists(to); n++) to = path + ".damaged-" + n.ToString(CultureInfo.InvariantCulture);
                File.Move(path, to);
                return to;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    /// <summary>The records' JSON (work order 114): a flat object of numbers; fields it does not know are passed over.</summary>
    public static class RecordsJson
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        public static string Write(RunRecords r)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(r.Version.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"runsStarted\": ").Append(r.RunsStarted.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"deaths\": ").Append(r.Deaths.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"actsCleared\": ").Append(r.ActsCleared.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"deepestFloor\": ").Append(r.DeepestFloor.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"kills\": ").Append(r.Kills.ToString(s_Inv)).Append(",\n");
            sb.Append("  \"longestRunSeconds\": ").Append(r.LongestRunSeconds.ToString("0.###", s_Inv)).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Reads the records; a field it lacks is zero (an older file). Throws <see cref="FormatException"/> on what is not such an object.</summary>
        public static RunRecords Read(string text)
        {
            var r = new RunRecords { Version = 0 };
            string t = (text ?? "").Trim();
            if (t.Length < 2 || t[0] != '{' || t[t.Length - 1] != '}') throw new FormatException("not an object");
            string body = t.Substring(1, t.Length - 2);
            bool sawVersion = false;
            foreach (string part in body.Split(','))
            {
                if (part.Trim().Length == 0) continue;
                int colon = part.IndexOf(':');
                if (colon < 0) throw new FormatException("no colon in " + part);
                string key = part.Substring(0, colon).Trim();
                string value = part.Substring(colon + 1).Trim();
                if (key.Length < 2 || key[0] != '"' || key[key.Length - 1] != '"') throw new FormatException("a key without quotes: " + key);
                key = key.Substring(1, key.Length - 2);
                if (!double.TryParse(value, NumberStyles.Float, s_Inv, out double n))
                {
                    if (value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal)) continue; // a later version's words
                    throw new FormatException("not a number: " + value);
                }
                switch (key)
                {
                    case "version": r.Version = (int)n; sawVersion = true; break;
                    case "runsStarted": r.RunsStarted = (int)n; break;
                    case "deaths": r.Deaths = (int)n; break;
                    case "actsCleared": r.ActsCleared = (int)n; break;
                    case "deepestFloor": r.DeepestFloor = (int)n; break;
                    case "kills": r.Kills = (int)n; break;
                    case "longestRunSeconds": r.LongestRunSeconds = (float)n; break;
                }
            }
            if (!sawVersion) r.Version = 0; // before the first version: read as it is, what it lacks zero
            return r;
        }
    }
}
