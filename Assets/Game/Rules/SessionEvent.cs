using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>The kinds of event in a play session log, as they are written in its "type" field.</summary>
    public static class SessionEventTypes
    {
        public const string SessionStart = "session_start";   // the date, the commit, the Unity version and the tuning in use
        public const string Skill = "skill";                  // one skill of the kit as it was at the start: slot, name, input, cooldown
        public const string SessionEnd = "session_end";

        public const string SkillPress = "skill_press";       // a skill key went down: started, buffered or rejected
        public const string SkillRelease = "skill_release";   // a skill key went up
        public const string SkillStart = "skill_start";       // a skill started, at its press or from the buffer
        public const string SkillFired = "skill_fired";       // one hit of a skill went off (a burst hit, a tick, a landing): hit says whether it hit anything
        public const string SkillEnd = "skill_end";           // a skill let her go: result says how, held and charge for Holding and Charge
        public const string BufferDropped = "buffer_dropped"; // a buffered skill or dash press ran out before it could start
        public const string Click = "click";                  // the basic shot's button: started or waiting
        public const string ClickDropped = "click_dropped";   // a waiting click was dropped by a dash or a skill
        public const string ShotStart = "shot_start";         // a basic shot started, at its click or when its waiting click was taken
        public const string ShotFired = "shot_fired";         // a basic shot went off: hit says whether it hit
        public const string DashPress = "dash_press";         // the dash key: started, buffered or rejected
        public const string DashStart = "dash_start";
        public const string Hit = "hit";                      // damage the player dealt: name is the skill or the basic shot
        public const string FocusPress = "focus_press";       // the empowered mode's key: started or rejected
        public const string FocusFull = "focus_full";
        public const string FocusSpent = "focus_spent";
        public const string EmpoweredEnd = "empowered_end";

        public const string EnemyWake = "enemy_wake";
        public const string EnemyAttack = "enemy_attack";     // the enemy started an attack's windup: name is the attack
        public const string EnemyStrike = "enemy_strike";     // the strike: landed, out of the area, the dash, or no effect
        public const string EnemyGaveUp = "enemy_gave_up";    // it went back to standing at home
        public const string EnemyDeath = "enemy_death";
        public const string PlayerDamaged = "player_damaged";
        public const string PlayerDeath = "player_death";

        public const string Move = "move";                    // a sample, a few times a second, of whether she is moving
    }

    /// <summary>The words a log uses in its "result" field, and the names of the two actions that are not skills.</summary>
    public static class SessionWords
    {
        public const string BasicShot = "Basic shot";
        public const string Dash = "Dash";

        public const string Started = "started";
        public const string Buffered = "buffered";
        public const string Rejected = "rejected";
        public const string Waiting = "waiting";

        public const string Cooldown = "cooldown";            // why a press was rejected
        public const string Dashing = "dashing";

        public const string RanOut = "ran out";               // how a skill ended
        public const string CancelledByDash = "cancelled by dash";
        public const string Cancelled = "cancelled";

        public const string Landed = "landed";                // how an enemy strike went
        public const string OutOfArea = "out of the area";
        public const string AvoidedByDash = "avoided by the dash";
        public const string NoEffect = "no effect";

        public const string Dummy = "dummy";                  // what a hit landed on
        public const string Enemy = "enemy";
    }

    /// <summary>
    /// One line of a play session log: what happened, and when, in seconds since the session started. Which of the
    /// other fields are set depends on the type (<see cref="SessionEventTypes"/>); the rest stay null and are not
    /// written. A log is one JSON object per line, written and read here by hand so the rules need no engine.
    /// </summary>
    public sealed class SessionEvent
    {
        public double T;
        public string Type;
        public string Name;          // the skill, the basic shot, the dash, the enemy's attack
        public int? Slot;            // the skill's slot
        public string Result;        // what became of a press, how a skill ended, how a strike went
        public string Reason;        // why a press was rejected
        public float? CooldownLeft;  // s of cooldown left at a rejected press
        public int? Damage;
        public string Target;        // dummy or enemy
        public bool? Empowered;
        public bool? Hit;            // a shot or a skill hit that went off: did it hit anything. On skill_end: did the skill fire at all
        public bool? Moving;
        public float? Held;          // s the key of a Holding or Charge skill counted as held
        public float? Charge;        // 0 to 1, the charge a Charge skill fired with
        public float? Cooldown;      // s, a skill of the kit
        public string Input;         // the input type of a skill of the kit
        /// <summary>Anything else, in the order written: the session's date, commit and tuning. Values are kept as text.</summary>
        public List<KeyValuePair<string, string>> Extra;

        public SessionEvent() { }

        public SessionEvent(double t, string type, string name = null)
        {
            T = t;
            Type = type;
            Name = name;
        }

        /// <summary>The value of an extra field, or null.</summary>
        public string ExtraValue(string key)
        {
            if (Extra == null) return null;
            foreach (KeyValuePair<string, string> pair in Extra)
                if (pair.Key == key) return pair.Value;
            return null;
        }

        public void AddExtra(string key, string value)
        {
            if (Extra == null) Extra = new List<KeyValuePair<string, string>>();
            Extra.Add(new KeyValuePair<string, string>(key, value ?? ""));
        }

        // ---------------- Writing ----------------

        /// <summary>The event as one line of JSON, without the line break.</summary>
        public string ToJson()
        {
            var sb = new StringBuilder(96);
            AppendJson(sb);
            return sb.ToString();
        }

        public void AppendJson(StringBuilder sb)
        {
            sb.Append("{\"t\":").Append(T.ToString("0.000", CultureInfo.InvariantCulture));
            WriteString(sb, "type", Type);
            WriteString(sb, "name", Name);
            if (Slot.HasValue) sb.Append(",\"slot\":").Append(Slot.Value.ToString(CultureInfo.InvariantCulture));
            WriteString(sb, "result", Result);
            WriteString(sb, "reason", Reason);
            WriteNumber(sb, "cooldown_left", CooldownLeft);
            if (Damage.HasValue) sb.Append(",\"damage\":").Append(Damage.Value.ToString(CultureInfo.InvariantCulture));
            WriteString(sb, "target", Target);
            WriteBool(sb, "empowered", Empowered);
            WriteBool(sb, "hit", Hit);
            WriteBool(sb, "moving", Moving);
            WriteNumber(sb, "held", Held);
            WriteNumber(sb, "charge", Charge);
            WriteNumber(sb, "cooldown", Cooldown);
            WriteString(sb, "input", Input);
            if (Extra != null)
                foreach (KeyValuePair<string, string> pair in Extra) WriteString(sb, pair.Key, pair.Value);
            sb.Append('}');
        }

        static void WriteString(StringBuilder sb, string key, string value)
        {
            if (value == null) return;
            sb.Append(",\"").Append(key).Append("\":");
            Quote(sb, value);
        }

        static void WriteNumber(StringBuilder sb, string key, float? value)
        {
            if (!value.HasValue) return;
            float v = value.Value;
            if (float.IsNaN(v) || float.IsInfinity(v)) v = 0f;
            sb.Append(",\"").Append(key).Append("\":").Append(v.ToString("0.###", CultureInfo.InvariantCulture));
        }

        static void WriteBool(StringBuilder sb, string key, bool? value)
        {
            if (!value.HasValue) return;
            sb.Append(",\"").Append(key).Append("\":").Append(value.Value ? "true" : "false");
        }

        static void Quote(StringBuilder sb, string s)
        {
            sb.Append('"');
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': sb.Append("\\\""); break;
                    case '\\': sb.Append("\\\\"); break;
                    case '\n': sb.Append("\\n"); break;
                    case '\r': sb.Append("\\r"); break;
                    case '\t': sb.Append("\\t"); break;
                    default:
                        if (c < ' ') sb.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        else sb.Append(c);
                        break;
                }
            }
            sb.Append('"');
        }

        // ---------------- Reading ----------------

        /// <summary>
        /// Reads one line of a log. Null for a line that is empty or not a JSON object. Fields it does not know go
        /// to <see cref="Extra"/>; a nested object or array is kept there as its raw text.
        /// </summary>
        public static SessionEvent Parse(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;
            int i = 0;
            SkipSpace(line, ref i);
            if (i >= line.Length || line[i] != '{') return null;
            i++;
            var e = new SessionEvent();
            try
            {
                while (true)
                {
                    SkipSpace(line, ref i);
                    if (i >= line.Length) return null;
                    if (line[i] == '}') break;
                    if (line[i] == ',') { i++; continue; }
                    string key = ReadString(line, ref i);
                    SkipSpace(line, ref i);
                    if (i >= line.Length || line[i] != ':') return null;
                    i++;
                    SkipSpace(line, ref i);
                    string value = ReadValue(line, ref i, out bool quoted);
                    e.Set(key, value, quoted);
                }
            }
            catch (FormatException)
            {
                return null;
            }
            return e.Type == null ? null : e;
        }

        void Set(string key, string value, bool quoted)
        {
            switch (key)
            {
                case "t": T = ParseDouble(value); break;
                case "type": Type = value; break;
                case "name": Name = value; break;
                case "slot": Slot = (int)ParseDouble(value); break;
                case "result": Result = value; break;
                case "reason": Reason = value; break;
                case "cooldown_left": CooldownLeft = (float)ParseDouble(value); break;
                case "damage": Damage = (int)ParseDouble(value); break;
                case "target": Target = value; break;
                case "empowered": Empowered = value == "true"; break;
                case "hit": Hit = value == "true"; break;
                case "moving": Moving = value == "true"; break;
                case "held": Held = (float)ParseDouble(value); break;
                case "charge": Charge = (float)ParseDouble(value); break;
                case "cooldown": Cooldown = (float)ParseDouble(value); break;
                case "input": Input = value; break;
                default: AddExtra(key, value); break;
            }
        }

        static double ParseDouble(string s) =>
            double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : 0d;

        static void SkipSpace(string s, ref int i)
        {
            while (i < s.Length && char.IsWhiteSpace(s[i])) i++;
        }

        static string ReadValue(string s, ref int i, out bool quoted)
        {
            quoted = false;
            if (i >= s.Length) throw new FormatException();
            char c = s[i];
            if (c == '"')
            {
                quoted = true;
                return ReadString(s, ref i);
            }
            if (c == '{' || c == '[')
            {
                // Kept raw: the log writes none, but a hand-edited line may have one.
                int start = i, depth = 0;
                bool inString = false;
                for (; i < s.Length; i++)
                {
                    char d = s[i];
                    if (inString)
                    {
                        if (d == '\\') i++;
                        else if (d == '"') inString = false;
                        continue;
                    }
                    if (d == '"') inString = true;
                    else if (d == '{' || d == '[') depth++;
                    else if (d == '}' || d == ']')
                    {
                        depth--;
                        if (depth == 0) { i++; return s.Substring(start, i - start); }
                    }
                }
                throw new FormatException();
            }
            int from = i;
            while (i < s.Length && s[i] != ',' && s[i] != '}' && !char.IsWhiteSpace(s[i])) i++;
            string word = s.Substring(from, i - from);
            return word == "null" ? null : word;
        }

        static string ReadString(string s, ref int i)
        {
            if (i >= s.Length || s[i] != '"') throw new FormatException();
            i++;
            var sb = new StringBuilder();
            while (i < s.Length)
            {
                char c = s[i++];
                if (c == '"') return sb.ToString();
                if (c != '\\') { sb.Append(c); continue; }
                if (i >= s.Length) break;
                char esc = s[i++];
                switch (esc)
                {
                    case 'n': sb.Append('\n'); break;
                    case 'r': sb.Append('\r'); break;
                    case 't': sb.Append('\t'); break;
                    case 'b': sb.Append('\b'); break;
                    case 'f': sb.Append('\f'); break;
                    case 'u':
                        if (i + 4 > s.Length) throw new FormatException();
                        sb.Append((char)int.Parse(s.Substring(i, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                        i += 4;
                        break;
                    default: sb.Append(esc); break;
                }
            }
            throw new FormatException();
        }
    }

    /// <summary>
    /// When the play session recorder may start by itself (it may always be started on purpose, as tests do).
    /// Never in batch mode, where the test suites run without a window; never in the scene the Unity test runner makes
    /// to start a PlayMode run ("InitTestScene..."), so not in the Editor's Test Runner either; and never for a
    /// character whose input driver is shut off from the real mouse and keyboard, as every PlayMode test does before it
    /// loads a scene.
    /// </summary>
    public static class SessionRecordingRule
    {
        public const string TestRunnerScenePrefix = "InitTestScene";

        /// <summary>At the start of Play: may the recorder be installed at all?</summary>
        public static bool MayStartByItself(string firstSceneName, bool batchMode) =>
            !batchMode && (firstSceneName == null || !firstSceneName.StartsWith(TestRunnerScenePrefix, StringComparison.Ordinal));

        /// <summary>When it finds a character: may it record this one? Only one played with the real devices.</summary>
        public static bool MayRecordCharacter(bool readsRealDevices) => readsRealDevices;
    }
}
