using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The rehearsal (work order 148, 2026-11-27): a start right in the throne room with the build of his last run. Kristian
    /// ticked "Også «generalprøve» med siste bygg" (the option's words: a switch that starts right in the throne room with the
    /// upgrades from your last run), after a whole run that day ("bossen skal være veldig vanskelig. masse mechanics og ting å
    /// dodge") and his tick that the hard boss is tuned for several tries ("Flere forsøk forventet"), when a run to the boss takes
    /// about 13 minutes. Every number here is a first guess.
    /// </summary>
    public static class RehearsalValues
    {
        /// <summary>
        /// The order's switch, on (his tick of 2026-11-27: "Også «generalprøve» med siste bygg"). Off: no entry on the main menu,
        /// nothing stored when a run ends, nothing changed anywhere.
        /// </summary>
        public static bool On = true;

        /// <summary>The stored build's own file, beside between-runs.json under the game's data folder, never inside it.</summary>
        public const string FileName = "rehearsal-build.json";

        /// <summary>
        /// Only a run that reached this floor replaces the stored build (the order: "only a run that reached at least floor 2
        /// replaces it", so a death in the first room does not wipe a good build).
        /// </summary>
        public const int KeepsFromFloor = 2;

        /// <summary>
        /// The main menu's entry "Rehearsal: the boss" sits at this place of its lines: right under START RUN (the producer's
        /// choice: he presses it right after dying at the boss, and START RUN keeps the top place it has).
        /// </summary>
        public const int MenuPlace = 1;

        /// <summary>
        /// Metres outside the throne room's door she is put, in the room or passage before it (the producer's first guess: a step
        /// and a half, short of the room, so she walks in and the entrance plays the ordinary way).
        /// </summary>
        public const float DoorStandOff = 1.5f;

        /// <summary>How far (m) the point outside the door may be moved to stand on the walkable floor.</summary>
        public const float DoorSnap = 2f;
    }

    /// <summary>A skill taken at a tier in the stored run (work order 057's rare or epic): its name and tier.</summary>
    public sealed class RehearsalSkillTier
    {
        public string Skill = "";
        public UpgradeTier Tier;
    }

    /// <summary>A basic-shot upgrade held in the stored run (work order 138): its kind and the tier held.</summary>
    public sealed class RehearsalShotTier
    {
        public BasicShotUpgradeKind Kind;
        public int Tier;
    }

    /// <summary>
    /// The build of the last run (work order 148): what she carried when it ended, as a fresh start on floor 5 needs it to be
    /// the same character. Held: her skills on their keys and in the order she had them, the chests' and the cards' stat steps
    /// with their tiers (her stats, and her health's maximum through them), the skill upgrades with the tiers' steps and
    /// changes, the skills' tiers, the basic-shot upgrades and their tiers, the run's focus choice, the rack's pieces and the
    /// ranks as they were applied (<see cref="RunMetaEffects"/>), and her pistols' parts (<see cref="AttachmentEffects"/>).
    /// <see cref="MaxHealth"/> and <see cref="Stats"/> are what they came to, kept to check the rebuilt build against, never
    /// set from. Left out on purpose: the spent counters of a run (Second Look's redraws left and Knight's Resolve's blows
    /// left are stored full, as a run starts with them; Sixth Chamber's count), the game's own settings (the cooldown floor,
    /// the stats' numbers and switches: today's are used), and what a run holds that is not her (health now, focus,
    /// cooldowns, the floors, the record, the Echoes earned).
    /// </summary>
    public sealed class RehearsalBuild
    {
        /// <summary>The file's version; a newer one is not read (no entry).</summary>
        public const int CurrentVersion = 1;

        public int Version = CurrentVersion;
        /// <summary>The run it is from: its seed's text.</summary>
        public string Run = "";
        /// <summary>The floor that run reached.</summary>
        public int Floor;
        /// <summary>How it ended: "death", "act cleared" or "left".</summary>
        public string Ended = "";
        /// <summary>When it was kept, for the log.</summary>
        public string Saved = "";

        /// <summary>The skill on each key of her bar, in key order ("" for an empty key).</summary>
        public List<string> Keys = new List<string>();
        /// <summary>Every skill she had, those on keys in key order first, then any she had on no key: taken back in this order.</summary>
        public List<string> Skills = new List<string>();
        /// <summary>The chests' and the cards' stat steps, in the order taken (<see cref="RunModifiers.Taken"/>).</summary>
        public List<Upgrade> Upgrades = new List<Upgrade>();
        /// <summary>The skill upgrades in the order taken, the tiers' steps and changes among them (<see cref="RunSkillUpgrades.Taken"/>).</summary>
        public List<SkillUpgradeDefinition> SkillUpgrades = new List<SkillUpgradeDefinition>();
        /// <summary>The skills taken at a tier above common.</summary>
        public List<RehearsalSkillTier> SkillTiers = new List<RehearsalSkillTier>();
        /// <summary>The basic-shot upgrades held, in the order first taken.</summary>
        public List<RehearsalShotTier> BasicShot = new List<RehearsalShotTier>();
        /// <summary>The run's focus choice (work order 025).</summary>
        public FocusMode Focus = FocusMode.Today;
        public bool FocusOffered;
        public bool FocusTaken;
        /// <summary>The rack's pieces and the ranks, as they were put on the run.</summary>
        public RunMetaEffects Meta = new RunMetaEffects();
        /// <summary>Her pistols' parts, as they were fitted for the run.</summary>
        public AttachmentEffects Weapon = new AttachmentEffects();
        /// <summary>Her health's maximum as the run ended: checked against the rebuilt build, never set from.</summary>
        public int MaxHealth;
        /// <summary>Her stats in one line as the run ended (<see cref="HerStats.OneLine"/>): checked, never set from; "" with the stats off.</summary>
        public string Stats = "";

        /// <summary>The fields that say which run it is from, not what she carries: left out of <see cref="RehearsalRules.Differences"/>.</summary>
        public static readonly string[] RunFields = { "Version", "Run", "Floor", "Ended", "Saved" };

        /// <summary>
        /// What a run's rules hold of her now: the modifiers, the skill upgrades and the focus choice; <paramref name="keys"/> the
        /// skill on each key, <paramref name="had"/> every skill had in the order to take them back. A copy: nothing of the
        /// run's is held by it.
        /// </summary>
        public static RehearsalBuild Capture(RunModifiers modifiers, RunSkillUpgrades skillUpgrades, RunFocusChoice focus,
                                             IList<string> keys, IList<string> had)
        {
            var b = new RehearsalBuild();
            if (keys != null) b.Keys.AddRange(keys);
            if (had != null) b.Skills.AddRange(had);
            if (modifiers != null)
            {
                foreach (Upgrade u in modifiers.Taken) b.Upgrades.Add(RehearsalJson.Copy(u));
                RehearsalJson.CopyFields(modifiers.Meta, b.Meta);
                b.Meta.RedrawsLeft = b.Meta.Redraws; // a run starts with them all (MetaRack.Apply)
                b.Meta.ResolvesLeft = b.Meta.Resolves; // work order 106: the same
                RehearsalJson.CopyFields(modifiers.Weapon, b.Weapon);
                foreach (BasicShotUpgradeKind k in modifiers.BasicShot.Held)
                    b.BasicShot.Add(new RehearsalShotTier { Kind = k, Tier = modifiers.BasicShot.TierOf(k) });
            }
            if (skillUpgrades != null)
            {
                foreach (SkillUpgradeDefinition u in skillUpgrades.Taken) b.SkillUpgrades.Add(RehearsalJson.Copy(u));
                foreach (string name in b.Skills)
                {
                    UpgradeTier t = skillUpgrades.TierOf(name);
                    if (t != UpgradeTier.Common) b.SkillTiers.Add(new RehearsalSkillTier { Skill = name, Tier = t });
                }
            }
            if (focus != null)
            {
                b.Focus = focus.Mode;
                b.FocusOffered = focus.Offered;
                b.FocusTaken = focus.Taken;
            }
            return b;
        }

        /// <summary>
        /// Puts the build on a run's rules, cleared of what they held of hers first: the stat steps, the rack's pieces and the
        /// ranks, her pistols' parts and the basic-shot upgrades on <paramref name="modifiers"/> (its settings, the cooldown floor
        /// and the stats' numbers, are left as the caller set them); the skill upgrades and tiers; the focus choice. The skills
        /// themselves go on her bar by the caller, in <see cref="Skills"/>' order. <paramref name="shotValues"/>,
        /// <paramref name="kitPerMark"/> and <paramref name="markCap"/> are what a basic-shot card puts on with its first take.
        /// </summary>
        public void ApplyTo(RunModifiers modifiers, RunSkillUpgrades skillUpgrades, RunFocusChoice focus,
                            BasicShotUpgradeValues shotValues, int kitPerMark, int markCap)
        {
            if (modifiers != null)
            {
                modifiers.Clear();
                foreach (Upgrade u in Upgrades) modifiers.Take(RehearsalJson.Copy(u));
                RehearsalJson.CopyFields(Meta, modifiers.Meta);
                RehearsalJson.CopyFields(Weapon, modifiers.Weapon);
                if (BasicShot.Count > 0)
                {
                    modifiers.BasicShot.Values = shotValues ?? new BasicShotUpgradeValues();
                    modifiers.BasicShot.KitPerMark = kitPerMark;
                    modifiers.BasicShot.MarkCap = markCap;
                }
                foreach (RehearsalShotTier s in BasicShot)
                    for (int t = modifiers.BasicShot.TierOf(s.Kind); t < s.Tier && t < BasicShotUpgradeValues.Tiers; t++) modifiers.BasicShot.Take(s.Kind);
            }
            if (skillUpgrades != null)
            {
                skillUpgrades.Clear();
                foreach (SkillUpgradeDefinition u in SkillUpgrades) skillUpgrades.Take(RehearsalJson.Copy(u));
                // A tier with no rows of its own: the rows are in the list above, taken in their order; only the skill's tier is set.
                foreach (RehearsalSkillTier t in SkillTiers)
                    if (t != null && t.Tier != UpgradeTier.Common) skillUpgrades.TakeTier(new SkillTierOffer { Skill = t.Skill, Tier = t.Tier });
            }
            if (focus != null)
            {
                focus.Clear();
                if (FocusOffered || FocusTaken) focus.Offer();
                if (FocusTaken) focus.Take(Focus);
            }
        }
    }

    /// <summary>The rehearsal's rules (work order 148), with no engine.</summary>
    public static class RehearsalRules
    {
        /// <summary>
        /// Does a run that ended now replace the stored build: the switch on, not the room lab's run nor a rehearsal, and it
        /// reached <see cref="RehearsalValues.KeepsFromFloor"/> or further.
        /// </summary>
        public static bool Keeps(bool on, bool inLab, bool inRehearsal, int floorReached) =>
            on && !inLab && !inRehearsal && floorReached >= RehearsalValues.KeepsFromFloor;

        /// <summary>
        /// What differs between two builds, field for field, as "Meta.DamageShare: 0.1 / 0.2"; empty when they are the same
        /// build. The fields of which run it is from (<see cref="RehearsalBuild.RunFields"/>) are not compared.
        /// </summary>
        public static List<string> Differences(RehearsalBuild a, RehearsalBuild b)
        {
            var found = new List<string>();
            if (a == null || b == null)
            {
                if (a != b) found.Add("one of the two is missing");
                return found;
            }
            foreach (FieldInfo f in RehearsalJson.Fields(typeof(RehearsalBuild)))
            {
                if (Array.IndexOf(RehearsalBuild.RunFields, f.Name) >= 0) continue;
                Diff(f.Name, f.GetValue(a), f.GetValue(b), found);
            }
            return found;
        }

        static void Diff(string path, object a, object b, List<string> found)
        {
            if (a == null || b == null || RehearsalJson.IsLeaf(a.GetType()))
            {
                string x = RehearsalJson.Write(a).Trim(), y = RehearsalJson.Write(b).Trim();
                if (x != y) found.Add(path + ": " + x + " / " + y);
                return;
            }
            if (a is IList la && b is IList lb)
            {
                if (la.Count != lb.Count) found.Add(path + ": " + la.Count.ToString(CultureInfo.InvariantCulture) + " / " + lb.Count.ToString(CultureInfo.InvariantCulture) + " items");
                for (int i = 0; i < Math.Min(la.Count, lb.Count); i++) Diff(path + "[" + i.ToString(CultureInfo.InvariantCulture) + "]", la[i], lb[i], found);
                return;
            }
            foreach (FieldInfo f in RehearsalJson.Fields(a.GetType())) Diff(path + "." + f.Name, f.GetValue(a), f.GetValue(b), found);
        }
    }

    /// <summary>
    /// The stored build's file (work order 148): one small JSON file of its own at a path the caller gives (the game's beside
    /// between-runs.json; tests give a temporary one). Reading never throws and never changes the file: a missing, unreadable,
    /// damaged or newer one is no build (null), and the menu shows no entry; one kept by an older game
    /// that names a skill, an upgrade or a kind this game no longer has is read with those dropped and the rest kept
    /// (<see cref="RehearsalJson"/>; a skill no longer hers is left out as the rehearsal starts); the next run that reaches floor 2 writes a good one
    /// over it. Writing goes through a temporary file, so a write cut short leaves the last build whole.
    /// </summary>
    public static class RehearsalFile
    {
        /// <summary>The build at <paramref name="path"/>, or null (no file, not readable now, damaged, a newer version). Never throws.</summary>
        public static RehearsalBuild Load(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
                return Parse(File.ReadAllText(path, Encoding.UTF8));
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A build from its text, or null when it is not one this game reads. Never throws.</summary>
        public static RehearsalBuild Parse(string text)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(text)) return null;
                // Read into a build of version 0: a file that names no version is not one (it would read as an empty build).
                RehearsalBuild b = RehearsalJson.Read(text, new RehearsalBuild { Version = 0 });
                if (b == null || b.Version < 1 || b.Version > RehearsalBuild.CurrentVersion) return null;
                return b;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Writes the build. False, with the reason, when it could not; never throws.</summary>
        public static bool Save(string path, RehearsalBuild build, out string error)
        {
            error = "";
            try
            {
                string folder = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                string temp = path + ".tmp";
                File.WriteAllText(temp, RehearsalJson.Write(build), new UTF8Encoding(false));
                if (File.Exists(path)) File.Replace(temp, path, null);
                else File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }

    /// <summary>
    /// The stored build's JSON (work order 148), written and read here with no engine and no package: every public field of
    /// the build's classes by its name, numbers that come back exactly (a float as its shortest round trip), an enum by its
    /// name, lists and arrays, objects within objects. Fields it does not know are passed over; a field the file lacks keeps
    /// its default. Anything else throws <see cref="FormatException"/> (reading) or <see cref="NotSupportedException"/> (a
    /// field of a kind it cannot write).
    /// </summary>
    public static class RehearsalJson
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>The public fields of a class, in their order.</summary>
        public static FieldInfo[] Fields(Type t) => t.GetFields(BindingFlags.Public | BindingFlags.Instance);

        /// <summary>A value written as one word: a string, a switch, a number, an enum.</summary>
        public static bool IsLeaf(Type t) => t == typeof(string) || t == typeof(bool) || t == typeof(int) || t == typeof(float) || t.IsEnum;

        public static string Write(object value)
        {
            var sb = new StringBuilder();
            Value(sb, value, 0);
            sb.Append('\n');
            return sb.ToString();
        }

        public static T Read<T>(string text) where T : class => Read<T>(text, null);

        /// <summary>As <see cref="Read{T}(string)"/>, into <paramref name="into"/>: a field the text lacks keeps the value it has there.</summary>
        public static T Read<T>(string text, T into) where T : class
        {
            var p = new Parser(text);
            object root = p.Value();
            p.End();
            return (T)Bind(root, typeof(T), into, true);
        }

        /// <summary>A copy of an object of public fields, through its JSON: nothing of the first is held by it.</summary>
        public static T Copy<T>(T value) where T : class => value == null ? null : (T)Bind(new Parser(Write(value)).Value(), value.GetType(), null);

        /// <summary>
        /// Every public field of <paramref name="from"/> put on <paramref name="into"/>, the same class: a word or a number as
        /// it is, a list of words copied into the list there (a list that is read-only as a field), anything else through
        /// <see cref="Copy"/>.
        /// </summary>
        public static void CopyFields(object from, object into)
        {
            if (from == null || into == null) return;
            foreach (FieldInfo f in Fields(from.GetType()))
            {
                object v = f.GetValue(from);
                if (v is IList list && f.IsInitOnly && f.GetValue(into) is IList there)
                {
                    there.Clear();
                    foreach (object item in list) there.Add(item == null || IsLeaf(item.GetType()) ? item : Bind(new Parser(Write(item)).Value(), item.GetType(), null));
                }
                else if (!f.IsInitOnly) f.SetValue(into, v == null || IsLeaf(v.GetType()) ? v : Bind(new Parser(Write(v)).Value(), f.FieldType, null));
            }
        }

        // ---------------- Writing ----------------

        static void Value(StringBuilder sb, object v, int depth)
        {
            switch (v)
            {
                case null: sb.Append("null"); return;
                case string s: Quote(sb, s); return;
                case bool b: sb.Append(b ? "true" : "false"); return;
                case int i: sb.Append(i.ToString(Inv)); return;
                case float f:
                    if (float.IsNaN(f) || float.IsInfinity(f)) Quote(sb, f.ToString("R", Inv));
                    else sb.Append(f.ToString("R", Inv));
                    return;
                case Enum e: Quote(sb, e.ToString()); return;
                case IList list:
                {
                    sb.Append('[');
                    for (int i = 0; i < list.Count; i++)
                    {
                        if (i > 0) sb.Append(", ");
                        Value(sb, list[i], depth + 1);
                    }
                    sb.Append(']');
                    return;
                }
            }
            Type t = v.GetType();
            if (!t.IsClass) throw new NotSupportedException("the stored build cannot hold a " + t.Name);
            FieldInfo[] fields = Fields(t);
            sb.Append('{');
            for (int i = 0; i < fields.Length; i++)
            {
                if (i > 0) sb.Append(',');
                if (depth == 0) sb.Append("\n  ");
                else sb.Append(' ');
                Quote(sb, fields[i].Name);
                sb.Append(": ");
                Value(sb, fields[i].GetValue(v), depth + 1);
            }
            sb.Append(depth == 0 ? "\n}" : " }");
        }

        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
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
                        if (ch < ' ') sb.Append("\\u").Append(((int)ch).ToString("x4", Inv));
                        else sb.Append(ch);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- Reading ----------------

        /// <summary>A number as its text, parsed when its field's kind is known (so a float comes back exactly).</summary>
        sealed class Number
        {
            public string Text;
        }

        /// <summary>
        /// A node as a value of <paramref name="t"/>. A file kept from an older (or newer) game may name what this game no longer
        /// has: an item of a list that cannot be read (an upgrade of a kind that is gone, a tier of a basic-shot upgrade that is
        /// gone) is dropped and the rest kept; on the <paramref name="root"/> object a name of an enum that is gone keeps the
        /// field's default. Anything else that is not as written throws.
        /// </summary>
        static object Bind(object node, Type t, object into, bool root = false)
        {
            if (t == typeof(string))
            {
                if (node == null) return "";
                return node as string ?? throw new FormatException("a word expected");
            }
            if (t == typeof(bool)) return node is bool b ? b : throw new FormatException("true or false expected");
            if (t == typeof(int)) return node is Number n ? int.Parse(n.Text, NumberStyles.Integer, Inv) : throw new FormatException("a whole number expected");
            if (t == typeof(float))
            {
                if (node is Number f) return float.Parse(f.Text, NumberStyles.Float, Inv);
                if (node is string fs) return float.Parse(fs, NumberStyles.Float, Inv);
                throw new FormatException("a number expected");
            }
            if (t.IsEnum)
            {
                if (!(node is string e) || e.Length == 0 || char.IsDigit(e[0]) || e[0] == '-') throw new FormatException("a name expected for " + t.Name);
                return Enum.Parse(t, e, false);
            }
            if (t.IsArray)
            {
                if (node == null) return Array.CreateInstance(t.GetElementType(), 0);
                if (!(node is List<object> items)) throw new FormatException("a list expected");
                Array a = Array.CreateInstance(t.GetElementType(), items.Count);
                for (int i = 0; i < items.Count; i++) a.SetValue(Bind(items[i], t.GetElementType(), null), i);
                return a;
            }
            if (t.IsGenericType && typeof(IList).IsAssignableFrom(t))
            {
                Type item = t.GetGenericArguments()[0];
                var list = (IList)(into ?? Activator.CreateInstance(t));
                list.Clear();
                if (node == null) return list;
                if (!(node is List<object> items)) throw new FormatException("a list expected");
                foreach (object i in items)
                {
                    object one;
                    try { one = Bind(i, item, null); }
                    catch (Exception e) when (e is FormatException || e is ArgumentException || e is OverflowException) { continue; } // dropped: the rest kept
                    list.Add(one);
                }
                return list;
            }
            if (!t.IsClass) throw new NotSupportedException("the stored build cannot hold a " + t.Name);
            if (node == null) return null;
            if (!(node is Dictionary<string, object> o)) throw new FormatException("an object expected for " + t.Name);
            object made = into ?? Activator.CreateInstance(t);
            foreach (FieldInfo f in Fields(t))
            {
                if (!o.TryGetValue(f.Name, out object v)) continue;
                if (f.IsInitOnly)
                {
                    object there = f.GetValue(made);
                    if (there != null) Bind(v, f.FieldType, there);
                }
                else if (root && f.FieldType.IsEnum)
                {
                    try { f.SetValue(made, Bind(v, f.FieldType, null)); }
                    catch (Exception e) when (e is FormatException || e is ArgumentException) { } // a name this game does not have: the default
                }
                else f.SetValue(made, Bind(v, f.FieldType, null));
            }
            return made;
        }

        /// <summary>A small JSON reader: objects, lists, strings, numbers (kept as their text), true, false and null.</summary>
        sealed class Parser
        {
            readonly string _t;
            int _i;

            public Parser(string text) => _t = text ?? "";

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
                            if (_i + 4 > _t.Length || !int.TryParse(_t.Substring(_i, 4), NumberStyles.HexNumber, Inv, out int code))
                                throw new FormatException("a bad \\u at " + _i);
                            sb.Append((char)code);
                            _i += 4;
                            break;
                        default: throw new FormatException("a bad escape at " + _i);
                    }
                }
            }

            Number Number()
            {
                int from = _i;
                if (_t[_i] == '-') _i++;
                while (_i < _t.Length && (char.IsDigit(_t[_i]) || _t[_i] == '.' || _t[_i] == 'e' || _t[_i] == 'E' || _t[_i] == '+' || _t[_i] == '-')) _i++;
                string text = _t.Substring(from, _i - from);
                if (!double.TryParse(text, NumberStyles.Float, Inv, out _)) throw new FormatException("a bad number at " + from);
                return new Number { Text = text };
            }
        }
    }
}
