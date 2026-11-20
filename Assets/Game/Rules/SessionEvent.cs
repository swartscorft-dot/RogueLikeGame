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

        // The kit (decision 0005, 2026-11-19).
        public const string MarkAdd = "mark_add";             // marks put on a target: name is what added them, marks the count after, added how many
        public const string MarksSpent = "marks_spent";       // a spender's hit took a target's marks: marks taken, damage of that hit, focus gained from them
        public const string MarksLost = "marks_lost";         // marks lost unused: result says how (fell off, died, reset)
        public const string RecastPress = "recast_press";     // a press that fired the next shot of a chain: shot is which, clean whether it was
        public const string ChainEnd = "chain_end";           // a recast chain ended: shot is how many it fired, result complete or early
        public const string CooldownCut = "cooldown_cut";     // spending focus cut the running cooldowns: cut.<name> is the seconds cut

        // Every enemy event says which enemy (enemy_id) and its kind (enemy) since work order 008.
        public const string EnemyWake = "enemy_wake";
        public const string EnemyAttack = "enemy_attack";     // the enemy started an attack's windup: name is the attack
        public const string EnemyStrike = "enemy_strike";     // the strike: landed, out of the area, the dash, or no effect
        public const string EnemyGaveUp = "enemy_gave_up";    // it went back to standing at home; result "removed" when it was taken out of the game awake
        public const string EnemyDeath = "enemy_death";
        public const string StaggerTaken = "stagger_taken";   // a hit took stagger off its bar: stagger is the points taken, bar the points left
        public const string EnemyDazed = "enemy_dazed";       // its stagger bar was emptied: the daze began
        public const string DazeEnded = "daze_ended";         // the daze ended, the bar is full again
        // The act boss (work order 014). Its attacks are enemy_attack and enemy_strike as every enemy's.
        public const string BossCharge = "boss_charge";       // its charge began to run down the line: name is the attack
        public const string BossCall = "boss_call";           // it called a pack: called is how many came
        public const string BossAnger = "boss_anger";         // it turned angry: shorter windups, double charges
        // The caster (work order 027). Its casts are enemy_attack, each circle's burst an enemy_strike named the cast.
        public const string CasterCircle = "caster_circle";   // a circle of its cast: name the cast, result placed or off the ground, circle which (from 1)
        public const string CasterCircleFaded = "caster_circle_faded"; // a circle of a countered Grave Rain faded without bursting: name, circle
        public const string EnemyCountered = "enemy_countered"; // Stopping Shot dropped its cast: name the cast
        public const string PlayerDamaged = "player_damaged";
        public const string PlayerDeath = "player_death";

        public const string Move = "move";                    // a sample, a few times a second, of whether she is moving

        public const string LevelBuilt = "level_built";       // a generated level was built: name is its seed's text, with version and rooms
        public const string RoomEntered = "room_entered";     // she entered a room of the generated level: room is its place in the run, name its kind

        // Runs (work order 012, decision 0008).
        public const string RunStart = "run_start";           // a run started: name is the run seed's text
        public const string FloorEnter = "floor_enter";       // she entered a floor of the run: floor its number, name its seed's text
        public const string FloorFinish = "floor_finish";     // the floor's exit room was cleared: floor, seconds it took, health left
        public const string RewardOffer = "reward_offer";     // the floor reward was offered: floor, offered the skills joined by " | "
        public const string RewardTake = "reward_take";       // a floor reward was taken: floor, name the skill
        public const string ChestOpen = "chest_open";         // a chest was opened and an upgrade taken: floor, room, offered joined by " | ", name the one taken
        public const string FloorHeal = "floor_heal";         // work order 023: she was healed as a floor's reward was taken: floor, healed (what was given), health after
        public const string RoomHeal = "room_heal";           // work order 030: every enemy of a combat room is dead and she was healed: floor, room, elite "true" for the elite room, healed (what was given), health after
        public const string RunEnd = "run_end";               // the run ended: result death, act cleared or left; floor, room and its kind, seconds since the run started
        // The elite room and the skill upgrades (work order 017).
        public const string EliteEnter = "elite_enter";       // she entered the floor's elite room for the first time: floor, room
        public const string EliteClear = "elite_clear";       // every enemy of the elite room is dead: floor, seconds since she first entered it, health left
        public const string SkillUpgradeOffer = "skill_upgrade_offer"; // floor, offered the upgrades ("Skill: Name (kind)") joined by " | "; heal "true" when none was left
        public const string SkillUpgradeTake = "skill_upgrade_take";   // an upgrade was taken: floor, name "Skill: Name", kind
        // The focus choice (work order 025). A focus_spent says what it was spent as: "as" today, part (Hair Trigger) or window (Steady Hand).
        public const string FocusChoiceOffer = "focus_choice_offer";   // floor, offered the two ways joined by " | "
        public const string FocusChoiceTake = "focus_choice_take";     // floor, name the way taken ("Hair Trigger")
        public const string PrimeUsed = "prime_used";                  // a primed skill used Hair Trigger's priming: name the skill, result what it gained
        public const string PrimeLapsed = "prime_lapsed";              // a priming ran out unused
        // What lasts between runs (work order 029), only with that trial switched on.
        public const string MetaLoadout = "meta_loadout";     // a run started: name the pieces equipped joined by " | ", currency, budget, used
        public const string MetaEarn = "meta_earn";           // the run earned: name what from (floor, elite room, act boss), amount, floor
        public const string MetaFeat = "meta_feat";           // a feat done for the first time, when the run ended: name its id
        // Taking cover (Kristian 2026-11-19, an experiment). A shot it stopped is an enemy_strike with result "stopped by cover".
        public const string CoverEnter = "cover_enter";       // she took cover: name is "tall" or "low", what she is behind
        public const string CoverLeave = "cover_leave";       // she left cover: result says how (released, dash, moving skill, other)
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

        public const string NotEquipped = "not equipped";     // why a skill press was rejected: the slot holds no skill yet
        public const string FellOff = "fell off";             // how marks were lost
        public const string Died = "died";
        public const string Reset = "reset";
        public const string Complete = "complete";            // how a recast chain ended
        public const string Early = "early";
        public const string Removed = "removed";              // an enemy taken out of the game awake
        public const string Placed = "placed";                // a caster's circle (work order 027): placed on the ground...
        public const string OffTheGround = "off the ground";  // ...or not, its place off the walkable ground

        // Taking cover (2026-11-19, an experiment).
        public const string StoppedByCover = "stopped by cover"; // how an enemy strike went: what she was in cover behind stopped it
        public const string Tall = "tall";                    // what she took cover behind
        public const string Low = "low";
        public const string Released = "released";            // how she left cover: the key went up
        public const string DashLeft = "dash";
        public const string MovingSkill = "moving skill";
        public const string Other = "other";
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
        public int? Marks;           // marks: the count after (mark_add), taken (marks_spent), lost (marks_lost)
        public int? Shot;            // which shot of a recast chain (recast_press), how many it fired (chain_end)
        public bool? Clean;          // a recast press in the clean part of its window
        public float? Focus;         // focus gained: from the damage of a hit, from the marks taken, from a clean tap
        public string Enemy;         // the kind of enemy: on its own events, and on a hit that landed on it (work order 008)
        public int? EnemyId;         // which enemy, unique in the session
        public float? Stagger;       // stagger points taken off a bar
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
            if (Marks.HasValue) sb.Append(",\"marks\":").Append(Marks.Value.ToString(CultureInfo.InvariantCulture));
            if (Shot.HasValue) sb.Append(",\"shot\":").Append(Shot.Value.ToString(CultureInfo.InvariantCulture));
            WriteBool(sb, "clean", Clean);
            WriteNumber(sb, "focus", Focus);
            WriteString(sb, "enemy", Enemy);
            if (EnemyId.HasValue) sb.Append(",\"enemy_id\":").Append(EnemyId.Value.ToString(CultureInfo.InvariantCulture));
            WriteNumber(sb, "stagger", Stagger);
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
                case "marks": Marks = (int)ParseDouble(value); break;
                case "shot": Shot = (int)ParseDouble(value); break;
                case "clean": Clean = value == "true"; break;
                case "focus": Focus = (float)ParseDouble(value); break;
                case "enemy": Enemy = value; break;
                case "enemy_id": EnemyId = (int)ParseDouble(value); break;
                case "stagger": Stagger = (float)ParseDouble(value); break;
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
