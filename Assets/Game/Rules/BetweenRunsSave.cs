using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>How a save was read (work order 029).</summary>
    public enum MetaLoad
    {
        /// <summary>No file: an empty save.</summary>
        Missing,
        /// <summary>Read as it was.</summary>
        Read,
        /// <summary>Not a save this game can read: kept beside the new one, renamed, and an empty save given.</summary>
        Damaged,
        /// <summary>A save of a newer version: kept beside the new one, renamed, and an empty save given.</summary>
        Newer,
        /// <summary>
        /// A file that is there but could not be read now (locked for a moment by another program): not an empty save, and
        /// nothing may be saved over it (the second reviews of 2026-11-20, work order 033). It is left alone, and tried
        /// again the next time the save is needed.
        /// </summary>
        Unreadable,
    }

    /// <summary>
    /// The save of what lasts between runs (work order 029, decision 0009, a proposal he has not ruled on): one small JSON
    /// file at a path given by the caller (the game's is under <c>Application.persistentDataPath</c>; tests give a
    /// temporary one). It holds the version, the currency, what is owned, what is equipped, the feats done and the pool
    /// skill struck out. Reading never throws: a missing file is an empty save; a damaged one, or one of a newer version,
    /// is renamed beside it (never deleted) and gives an empty save. Writing goes through a temporary file, so a write cut
    /// short leaves the last save whole; a write that fails says why and throws nothing. Work order 033: a file that is
    /// there but cannot be read now is not an empty save, and a replace that fails keeps the last good save and the new
    /// one beside it.
    /// </summary>
    public static class MetaSaveFile
    {
        /// <summary>
        /// Where a new save is kept beside the last good one when replacing that one failed (work order 033): read by
        /// <see cref="Load"/> when it is the newer of the two.
        /// </summary>
        public static string BesidePath(string path) => path + ".new";

        /// <summary>
        /// Reads the save at <paramref name="path"/>, tidied against the values. Never throws. Work order 033 (the second
        /// reviews of 2026-11-20): a save kept beside it (<see cref="BesidePath"/>) is read too, the newer of the two first,
        /// and the other when that one is missing, damaged or newer than this game; but never past one that is there and
        /// cannot be read now (<see cref="MetaLoad.Unreadable"/>), so an older one is never read and then saved over it.
        /// </summary>
        public static MetaState Load(string path, MetaValues values, out MetaLoad how, out string keptAs)
        {
            string beside = string.IsNullOrEmpty(path) ? null : BesidePath(path);
            bool besideFirst = false;
            try
            {
                besideFirst = beside != null && File.Exists(beside)
                              && (!File.Exists(path) || File.GetLastWriteTimeUtc(beside) >= File.GetLastWriteTimeUtc(path));
            }
            catch (Exception)
            {
                besideFirst = false;
            }
            MetaState first = LoadOne(besideFirst ? beside : path, values, out how, out keptAs);
            if (how == MetaLoad.Read || how == MetaLoad.Unreadable || beside == null) return first;
            MetaState other = LoadOne(besideFirst ? path : beside, values, out MetaLoad otherHow, out string otherKept);
            if (otherHow == MetaLoad.Missing) return first;
            if (keptAs.Length == 0) keptAs = otherKept;
            how = otherHow;
            return otherHow == MetaLoad.Read ? other : first;
        }

        /// <summary>Reads one file: as <see cref="Load"/> did before work order 033, but a file that cannot be read is Unreadable.</summary>
        static MetaState LoadOne(string path, MetaValues values, out MetaLoad how, out string keptAs)
        {
            keptAs = "";
            how = MetaLoad.Missing;
            var empty = new MetaState();
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return empty;
                string text = File.ReadAllText(path, Encoding.UTF8);
                MetaState read;
                try
                {
                    read = MetaJson.Read(text);
                }
                catch (FormatException)
                {
                    how = MetaLoad.Damaged;
                    keptAs = KeepAside(path, "damaged");
                    return empty;
                }
                if (read.Version > MetaState.CurrentVersion)
                {
                    how = MetaLoad.Newer;
                    keptAs = KeepAside(path, "newer");
                    return empty;
                }
                read.Version = MetaState.CurrentVersion;
                MetaRack.Tidy(read, values);
                how = MetaLoad.Read;
                return read;
            }
            catch (Exception)
            {
                // A file that is there and cannot be read now (locked, no rights): left alone, and not an empty save that
                // could be written over it (work order 033).
                how = MetaLoad.Unreadable;
                return empty;
            }
        }

        /// <summary>Writes the save. False, with the reason, when it could not; never throws.</summary>
        public static bool Save(string path, MetaState state, out string error)
        {
            error = "";
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, MetaJson.Write(state), new UTF8Encoding(false));
                if (!File.Exists(path)) File.Move(temp, path);
                else
                {
                    try
                    {
                        File.Replace(temp, path, null);
                    }
                    catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is PlatformNotSupportedException)
                    {
                        // Work order 033 (the second reviews of 2026-11-20): the last good save is never deleted before the new
                        // one is in place. It stays as it is, the new one is kept beside it (read by Load as the newer), and
                        // the failure is said.
                        string beside = BesidePath(path);
                        File.Copy(temp, beside, true);
                        File.Delete(temp);
                        error = "could not replace " + path + " (" + e.Message + "); the new save is kept beside it as " + beside;
                        return false;
                    }
                }
                // In place: one kept beside before is older now, and not needed.
                try
                {
                    if (File.Exists(BesidePath(path))) File.Delete(BesidePath(path));
                }
                catch (Exception)
                {
                    // Left: Load reads the newer of the two.
                }
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        /// <summary>Renames the file beside itself, "name.json.damaged-20261120-101500" and a number if that is taken.</summary>
        static string KeepAside(string path, string why)
        {
            try
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
                string to = path + "." + why + "-" + stamp;
                for (int n = 2; File.Exists(to); n++) to = path + "." + why + "-" + stamp + "-" + n.ToString(CultureInfo.InvariantCulture);
                File.Move(path, to);
                return to;
            }
            catch (Exception)
            {
                return "";
            }
        }
    }

    /// <summary>
    /// The save's JSON (work order 029), written and read here with no engine and no package: an object of numbers,
    /// strings and lists of strings. Fields it does not know are passed over, so a later version can add some.
    /// </summary>
    public static class MetaJson
    {
        public static string Write(MetaState s)
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            sb.Append("  \"version\": ").Append(s.Version.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"currency\": ").Append(s.Currency.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"earnedInAll\": ").Append(s.EarnedInAll.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"runs\": ").Append(s.Runs.ToString(CultureInfo.InvariantCulture)).Append(",\n");
            sb.Append("  \"owned\": ").Append(List(s.Owned)).Append(",\n");
            sb.Append("  \"equipped\": ").Append(List(s.Equipped)).Append(",\n");
            sb.Append("  \"feats\": ").Append(List(s.Feats)).Append(",\n");
            sb.Append("  \"struckOut\": ").Append(Quote(s.StruckOut ?? "")).Append('\n');
            sb.Append("}\n");
            return sb.ToString();
        }

        /// <summary>Reads a save; throws <see cref="FormatException"/> for anything that is not one.</summary>
        public static MetaState Read(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) throw new FormatException("empty");
            var p = new Parser(text);
            object root = p.Value();
            p.End();
            if (!(root is Dictionary<string, object> o)) throw new FormatException("not an object");
            if (!o.TryGetValue("version", out object v) || !(v is double)) throw new FormatException("no version");
            var s = new MetaState { Version = Int(v) };
            s.Currency = o.TryGetValue("currency", out object c) && c is double ? Int(c) : 0;
            s.EarnedInAll = o.TryGetValue("earnedInAll", out object e) && e is double ? Int(e) : 0;
            s.Runs = o.TryGetValue("runs", out object r) && r is double ? Int(r) : 0;
            Strings(o, "owned", s.Owned);
            Strings(o, "equipped", s.Equipped);
            Strings(o, "feats", s.Feats);
            s.StruckOut = o.TryGetValue("struckOut", out object so) && so is string str ? str : "";
            return s;
        }

        static int Int(object d)
        {
            double x = (double)d;
            if (double.IsNaN(x) || double.IsInfinity(x)) throw new FormatException("not a number");
            return (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, Math.Round(x)));
        }

        static void Strings(Dictionary<string, object> o, string key, List<string> into)
        {
            if (!o.TryGetValue(key, out object v) || v == null) return;
            if (!(v is List<object> list)) throw new FormatException(key + " is not a list");
            foreach (object item in list)
                if (item is string s) into.Add(s);
        }

        static string List(List<string> items)
        {
            var sb = new StringBuilder("[");
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(Quote(items[i]));
            }
            return sb.Append(']').ToString();
        }

        static string Quote(string s)
        {
            var sb = new StringBuilder("\"");
            foreach (char ch in s ?? "")
            {
                switch (ch)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(ch);
                        break;
                }
            }
            return sb.Append('"').ToString();
        }

        /// <summary>A small JSON reader: objects, lists, strings, numbers, true, false and null.</summary>
        sealed class Parser
        {
            readonly string _t;
            int _i;

            public Parser(string text) => _t = text;

            void Space()
            {
                while (_i < _t.Length && char.IsWhiteSpace(_t[_i])) _i++;
            }

            char Peek()
            {
                Space();
                if (_i >= _t.Length) throw new FormatException("ends too soon");
                return _t[_i];
            }

            void Expect(char ch)
            {
                if (Peek() != ch) throw new FormatException("expected '" + ch + "' at " + _i);
                _i++;
            }

            public void End()
            {
                Space();
                if (_i != _t.Length) throw new FormatException("more after the end at " + _i);
            }

            public object Value()
            {
                char ch = Peek();
                if (ch == '{') return Object();
                if (ch == '[') return Array();
                if (ch == '"') return String();
                if (ch == '-' || char.IsDigit(ch)) return Number();
                if (Word("true")) return true;
                if (Word("false")) return false;
                if (Word("null")) return null;
                throw new FormatException("unexpected '" + ch + "' at " + _i);
            }

            bool Word(string w)
            {
                if (string.CompareOrdinal(_t, _i, w, 0, w.Length) != 0) return false;
                _i += w.Length;
                return true;
            }

            Dictionary<string, object> Object()
            {
                var o = new Dictionary<string, object>();
                Expect('{');
                if (Peek() == '}')
                {
                    _i++;
                    return o;
                }
                while (true)
                {
                    if (Peek() != '"') throw new FormatException("a key expected at " + _i);
                    string key = String();
                    Expect(':');
                    o[key] = Value();
                    char ch = Peek();
                    _i++;
                    if (ch == '}') return o;
                    if (ch != ',') throw new FormatException("',' or '}' expected at " + (_i - 1));
                }
            }

            List<object> Array()
            {
                var list = new List<object>();
                Expect('[');
                if (Peek() == ']')
                {
                    _i++;
                    return list;
                }
                while (true)
                {
                    list.Add(Value());
                    char ch = Peek();
                    _i++;
                    if (ch == ']') return list;
                    if (ch != ',') throw new FormatException("',' or ']' expected at " + (_i - 1));
                }
            }

            string String()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (_i >= _t.Length) throw new FormatException("a string does not end");
                    char ch = _t[_i++];
                    if (ch == '"') return sb.ToString();
                    if (ch != '\\')
                    {
                        sb.Append(ch);
                        continue;
                    }
                    if (_i >= _t.Length) throw new FormatException("a string does not end");
                    char esc = _t[_i++];
                    switch (esc)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_i + 4 > _t.Length || !int.TryParse(_t.Substring(_i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                                throw new FormatException("a bad \\u at " + _i);
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw new FormatException("a bad escape at " + _i);
                    }
                }
            }

            double Number()
            {
                int from = _i;
                if (_t[_i] == '-') _i++;
                while (_i < _t.Length && (char.IsDigit(_t[_i]) || _t[_i] == '.' || _t[_i] == 'e' || _t[_i] == 'E' || _t[_i] == '+' || _t[_i] == '-')) _i++;
                if (!double.TryParse(_t.Substring(from, _i - from), NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
                    throw new FormatException("a bad number at " + from);
                return d;
            }
        }
    }
}
