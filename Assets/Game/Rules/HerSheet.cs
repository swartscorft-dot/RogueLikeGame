using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>The groups her sheet's stats stand in (work order 095, the producer's grouping).</summary>
    public enum SheetGroup { Attack, Skills, DefenceAndMoves, Fortune }

    /// <summary>
    /// One line of her sheet's stats (work order 095): its group, its name, its value now, its change since the run's start
    /// ("+0.5", "-6 %"; empty when it did not change) and whether the stat stands at its cap (a step of it would do nothing).
    /// </summary>
    public struct SheetStat
    {
        public SheetGroup Group;
        public string Name, Value, Change;
        public bool Changed, AtCap;

        public override string ToString() => Name + "  " + Value + (Changed ? "  (" + Change + ")" : "") + (AtCap ? "  " + HerSheet.CapWord : "");
    }

    /// <summary>A line of a skill's panel on her sheet (work order 095): what it is ("damage", "cooldown") and its value.</summary>
    public struct SheetLine
    {
        public string Label, Value;

        public SheetLine(string label, string value)
        {
            Label = label ?? "";
            Value = value ?? "";
        }

        public override string ToString() => Label + "  " + Value;
    }

    /// <summary>
    /// What the panel beside a skill on her sheet says (work order 095): its name, its key, what it does in one line (the
    /// floor reward card's words), its numbers as she has them now (only the lines that apply to it), and what she took for it.
    /// </summary>
    public sealed class SheetSkill
    {
        public string Name = "", Key = "", Sentence = "";
        /// <summary>The skill's tier when rare or epic ("rare"), else "".</summary>
        public string Tier = "";
        public readonly List<SheetLine> Lines = new List<SheetLine>();
        /// <summary>What she took for it this run: the elite room's upgrades and the chests' steps that name it (the basic shot: her rate's and reach's steps and her pistols' parts).</summary>
        public readonly List<string> Taken = new List<string>();

        /// <summary>The value of the line with this label, or null.</summary>
        public string ValueOf(string label)
        {
            foreach (SheetLine l in Lines) if (l.Label == label) return l.Value;
            return null;
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder(Name);
            if (Sentence.Length > 0) sb.Append('\n').Append(Sentence);
            foreach (SheetLine l in Lines) sb.Append('\n').Append(l);
            if (Taken.Count > 0) sb.Append("\ntaken: ").Append(string.Join(", ", Taken));
            return sb.ToString();
        }
    }

    /// <summary>
    /// The numbers of her sheet's layout (work order 095), layout pixels of a view 1080 high as <see cref="MenuValues"/>; first
    /// guesses set by looking at pictures of it (captures\095), tuned by him.
    /// </summary>
    [Serializable]
    public sealed class SheetValues
    {
        /// <summary>The heading's word on its red plate.</summary>
        public string HeadingWord = "STATS";
        /// <summary>The stats' panel's width (two columns of groups), the gap to the skills' column, and that column's width.</summary>
        public float StatsWidth = 860f;
        public float ColumnGap = 64f;
        public float SkillsWidth = 720f;
        /// <summary>A group's heading line ("ATTACK") in the stats' columns: its height and its words.</summary>
        public float GroupHead = 30f; // round 1's 34 left the pause menu drawn at 0.87 to fit
        public int GroupHeadSize = 19;
        /// <summary>A skill's tile on the row: its size, the gap between, its key cap's height under it.</summary>
        public float Tile = 70f;
        public float TileGap = 8f;
        public float TileCap = 26f;
        /// <summary>The skill's panel: its line's label column, the label's and the value's words, the name's, the sentence's.</summary>
        public float LabelWidth = 150f;
        public int LabelSize = 19;
        public int ValueSize = 21;
        public int NameSize = 34;
        public int SentenceSize = 20;
        /// <summary>The cap's word after a value at its cap, small and gold.</summary>
        public int CapSize = 15;
        /// <summary>The line of keys under the stats: its height.</summary>
        public float HintHeight = 30f;
        /// <summary>
        /// How far the pause menu's panels (her stats over the keys) stand higher than its rows, beside "PAUSED" (round 1: with
        /// the sheet's groups the panels made the menu 1248 high and it was drawn at 0.87 to fit, small at 1280 by 720). 0: where
        /// work order 078 put them, level with the rows.
        /// </summary>
        public float PauseSideRise = 72f;
        /// <summary>The gap between the skills' row and the skill's panel under it.</summary>
        public float PanelGap = 20f;
    }

    /// <summary>
    /// Her sheet (work order 095, 2026-11-23, Kristian after three runs: "tab knappen eller escape viser alle stats som er
    /// relevant å vise. også når man hovrer spells i tab/escape. så ser man stats", and "har vi scaling på spells?": he did not
    /// know that a skill takes her flat damage by its weight, <see cref="FlatDamage.WeightOf"/>; the game never told him). Every
    /// stat that matters, in four groups, with its value now, its change since the run's start and its cap; and for each skill
    /// she has, its numbers as she has them now, the weight said plainly. It reads, never changes: every value from
    /// <see cref="HerStats"/>, the run's modifiers and the skill as she has it (the run's view: her upgrades, then Blast and Wide
    /// window), with the offers' cards' words (<see cref="OfferTexts"/>): where a card names the stat, its name; where it does
    /// not, the card's label of the number. English, as the game's text is.
    /// </summary>
    public static class HerSheet
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>The word after a stat at its cap.</summary>
        public const string CapWord = "MAX";

        /// <summary>A group's heading.</summary>
        public static string GroupWord(SheetGroup g) =>
            g == SheetGroup.Attack ? "ATTACK" : g == SheetGroup.Skills ? "SKILLS" : g == SheetGroup.DefenceAndMoves ? "DEFENCE AND MOVES" : "FORTUNE";

        /// <summary>The column a group stands in, of two: attack over defence and moves, skills over fortune (the two columns about as long).</summary>
        public static int ColumnOf(SheetGroup g) => g == SheetGroup.Attack || g == SheetGroup.DefenceAndMoves ? 0 : 1;

        /// <summary>The groups in the order drawn: the first column's, then the second's.</summary>
        public static readonly SheetGroup[] Order = { SheetGroup.Attack, SheetGroup.DefenceAndMoves, SheetGroup.Skills, SheetGroup.Fortune };

        // ---------------- The stats ----------------

        /// <summary>A stat as read: its value's words, its number (what the change is reckoned on), how the change is written, and its cap.</summary>
        struct Reading
        {
            public SheetGroup Group;
            public string Name, Value, Format, Unit;
            public double Number;
            public bool AtCap;
        }

        /// <summary>
        /// Her sheet's stats now, a line each in the groups' order (<see cref="Order"/>): read from <paramref name="c"/> (her run's
        /// modifiers, her feel, her kit, her health), and again with every step of the run taken back for the start (what the
        /// rack's pieces and her pistols' parts give is her start, as the pause menu has counted it since work order 050). The
        /// steps are put back in their order before it returns. <paramref name="loadout"/>: her pistols' parts' line, or null.
        /// </summary>
        public static List<SheetStat> Stats(OfferContext c, string loadout = null)
        {
            c = c ?? new OfferContext();
            RunModifiers m = c.Modifiers ?? (c.Modifiers = new RunModifiers());
            List<Reading> now = Read(c);
            var steps = new List<Upgrade>(m.Taken);
            List<Reading> start;
            for (int i = steps.Count - 1; i >= 0; i--) m.TakeBack(steps[i]);
            try
            {
                start = Read(c);
            }
            finally
            {
                foreach (Upgrade u in steps) m.Take(u);
            }
            var lines = new List<SheetStat>();
            foreach (SheetGroup g in Order)
            {
                for (int i = 0; i < now.Count; i++)
                {
                    Reading r = now[i];
                    if (r.Group != g) continue;
                    double d = i < start.Count ? r.Number - start[i].Number : 0.0;
                    string change = Signed(d, r.Format, r.Unit);
                    bool changed = change.Length > 0;
                    lines.Add(new SheetStat { Group = g, Name = r.Name, Value = r.Value, Change = changed ? change : "", Changed = changed, AtCap = r.AtCap });
                }
                if (g == SheetGroup.Attack && !string.IsNullOrEmpty(loadout))
                    lines.Add(new SheetStat { Group = g, Name = "Loadout", Value = loadout, Change = "" });
            }
            return lines;
        }

        /// <summary>A change written with its sign and unit ("+0.5", "-6 %"); empty when it rounds to nothing.</summary>
        static string Signed(double d, string format, string unit)
        {
            string text = Math.Abs(d).ToString(format, s_Inv);
            if (double.TryParse(text, NumberStyles.Float, s_Inv, out double shown) && shown == 0.0) return "";
            return (d >= 0 ? "+" : "-") + text + unit;
        }

        static List<Reading> Read(OfferContext c)
        {
            RunModifiers m = c.Modifiers;
            HerStats s = m.Stats;
            FeelValues f = c.Feel ?? new FeelValues();
            KitValues kit = c.Kit ?? new KitValues();
            RunValues run = c.Run ?? new RunValues();
            StatValues v = m.StatValues ?? new StatValues();
            var list = new List<Reading>();
            void Add(SheetGroup g, string name, string value, double number, string format, string unit = "", bool cap = false) =>
                list.Add(new Reading { Group = g, Name = name, Value = value, Number = number, Format = format, Unit = unit, AtCap = cap });

            // Attack: the flat damage and the basic shot as she deals it, her rate, the crits, her reach, her share and her stagger.
            Add(SheetGroup.Attack, "Damage", "+" + s.Damage.ToString("0.#", s_Inv), s.Damage, "0.#");
            int basic = BasicShotDealt(c);
            Add(SheetGroup.Attack, "Basic shot", basic.ToString(s_Inv), basic, "0");
            float interval = BasicInterval(c);
            double rate = interval > 0f ? 1.0 / interval : 0.0;
            Add(SheetGroup.Attack, "Attack speed", OfferTexts.One(rate) + " shots/s", rate, "0.0", "", s.AttackSpeedAtMost);
            Add(SheetGroup.Attack, "Crit chance", OfferTexts.Pct(s.CritChance), s.CritChance * 100.0, "0.#", " %", s.On && s.CritChance >= 1f - 1e-5f);
            Add(SheetGroup.Attack, "Crit damage", OfferTexts.Plus(s.CritDamage), s.CritDamage * 100.0, "0.#", " %");
            float reach = BasicReach(c);
            Add(SheetGroup.Attack, "Reach", OfferTexts.Metres(reach), reach, "0.#", " m", s.On && s.Reach >= v.MostReach - 1e-5f);
            Add(SheetGroup.Attack, "Damage dealt", OfferTexts.Plus(s.DamageShare - 1f), (s.DamageShare - 1f) * 100.0, "0.#", " %");
            Add(SheetGroup.Attack, "Stagger", OfferTexts.Pct(s.Stagger), s.Stagger * 100.0, "0.#", " %");

            // Skills: every skill's cut, the clean press, marked prey, the window and the area, the marks' time, focus.
            Add(SheetGroup.Skills, "Cooldowns", s.AllCooldowns > 0f ? OfferTexts.Minus(s.AllCooldowns) : "0 %", -s.AllCooldowns * 100.0, "0.#", " %",
                s.On && s.AllCooldowns >= v.MostAllCooldowns - 1e-5f);
            Add(SheetGroup.Skills, "Clean press", OfferTexts.Plus(s.CleanPress - 1f), (s.CleanPress - 1f) * 100.0, "0.#", " %", s.On && s.CleanPress - 1f >= v.MostCleanPress - 1e-5f);
            Add(SheetGroup.Skills, "Marked prey", OfferTexts.Plus(s.MarkedPrey - 1f), (s.MarkedPrey - 1f) * 100.0, "0.#", " %", s.On && s.MarkedPrey - 1f >= v.MostMarkedPrey - 1e-5f);
            Add(SheetGroup.Skills, "Wide window", OfferTexts.Plus(s.WideWindow), s.WideWindow * 100.0, "0.#", " %", s.On && s.WideWindow >= v.MostWideWindow - 1e-5f);
            Add(SheetGroup.Skills, "Blast", OfferTexts.Plus(s.Blast), s.Blast * 100.0, "0.#", " %", s.On && s.Blast >= v.MostBlast - 1e-5f);
            float marks = kit.MarkLifetime + m.MarkLifetimeBonus;
            Add(SheetGroup.Skills, "Marks last", OfferTexts.Sec(marks), marks, "0.##", " s");
            Add(SheetGroup.Skills, "Focus gained", OfferTexts.Pct(m.FocusGainShare), m.FocusGainShare * 100.0, "0.#", " %");
            float window = FocusWindow(c);
            Add(SheetGroup.Skills, "Focus window", OfferTexts.Sec(window), window, "0.##", " s", s.On && s.LongFocus - 1f >= v.MostLongFocus - 1e-5f);

            // Defence and moves: health now and most, a cleared room's heal, her speeds, her dashes.
            int max = m.MaxHealthFor(c.OwnMaxHealth);
            Add(SheetGroup.DefenceAndMoves, "Health", Math.Max(0, c.Health).ToString(s_Inv) + " / " + max.ToString(s_Inv), max, "0");
            int heal = FloorHeals.Amount(max, run.RoomHeal) + s.Mending;
            Add(SheetGroup.DefenceAndMoves, "Room heal", heal.ToString(s_Inv), heal, "0", "", s.On && s.Mending >= v.MostMending);
            double speed = f.RunSpeed * s.MoveSpeed;
            Add(SheetGroup.DefenceAndMoves, "Run speed", OfferTexts.One(speed) + " m/s", speed, "0.0");
            if (f.Sprint) // work order 092: only with the sprint on
            {
                double sprint = speed * Math.Max(1f, f.SprintSpeedShare);
                Add(SheetGroup.DefenceAndMoves, "Sprint speed", OfferTexts.One(sprint) + " m/s", sprint, "0.0");
            }
            Add(SheetGroup.DefenceAndMoves, "Dashes", s.DashCharges.ToString(s_Inv), s.DashCharges, "0", "", s.On && s.ExtraDashes >= v.MostSecondDash);
            float dash = f.DashCooldown * s.DashCooldownShare;
            Add(SheetGroup.DefenceAndMoves, "Dash cooldown", OfferTexts.Sec(dash), dash, "0.##", " s", 1f - m.Total(UpgradeKind.DashCooldown) <= m.CooldownFloor + 1e-5f);

            // Fortune: her luck, and what it does to a chest.
            double[] shares = UpgradeTiers.Shares(v.ChestOdds, s.Luck, v);
            Add(SheetGroup.Fortune, "Luck", s.Luck.ToString("0.#", s_Inv), s.Luck, "0.#", "", s.On && shares[0] <= 1e-6);
            Add(SheetGroup.Fortune, "Rare or better", OfferTexts.Pct(1.0 - shares[0]), (1.0 - shares[0]) * 100.0, "0.#", " %");
            return list;
        }

        /// <summary>The basic shot's damage as she deals it now, as PlayerCharacter deals it: its own and her flat damage, her pistols' parts, her share.</summary>
        public static int BasicShotDealt(OfferContext c)
        {
            RunModifiers m = c.Modifiers;
            return m.ScaleDamage(m.Weapon.ShotDamage(c.BasicShotDamage + m.Stats.FlatFor(null), false));
        }

        /// <summary>The time from one basic shot to the next now (her pistols' parts, then her attack speed), as the attack speed card reckons it.</summary>
        public static float BasicInterval(OfferContext c)
        {
            FeelValues f = c.Feel ?? new FeelValues();
            RunModifiers m = c.Modifiers;
            return m.Stats.BasicShotInterval(f.BasicShotStartup, f.BasicShotActive, m.Weapon.Recovery(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery));
        }

        /// <summary>The basic shot's reach now: the feel's with her pistols' parts, and her reach's steps.</summary>
        public static float BasicReach(OfferContext c) => c.Modifiers.Weapon.Reach((c.Feel ?? new FeelValues()).BasicShotRange) + c.Modifiers.Stats.Reach;

        /// <summary>The focus window as her gauge works now, as the Long focus card reckons it: a part's priming with Hair Trigger, the long window with Steady Hand, else today's.</summary>
        static float FocusWindow(OfferContext c)
        {
            FocusModeValues fv = c.FocusValues ?? new FocusModeValues();
            float longer = c.Modifiers.Stats.LongFocus;
            if (c.FocusMode == FocusMode.HairTrigger) return fv.PrimeSeconds * longer;
            return (c.FocusMode == FocusMode.SteadyHand ? fv.WindowSeconds : (c.Identity ?? new IdentityValues()).EmpoweredDuration) * longer;
        }

        /// <summary>A sheet line as the pause menu's panel reads a stat (work order 078's <see cref="MenuStat"/>): the same list, one code.</summary>
        public static MenuStat AsMenuStat(SheetStat s) => new MenuStat { Name = s.Name, Value = s.Value, Start = "", Change = s.Change ?? "", Changed = s.Changed };

        // ---------------- A skill's panel ----------------

        /// <summary>The basic shot's name on the sheet, and its one line.</summary>
        public const string BasicShotName = "Basic shot";
        public const string BasicShotSentence = "Click: a pistol shot toward the cursor. It takes your flat damage once.";

        /// <summary>
        /// The panel of a skill as she has it now (<paramref name="skill"/>; null: her basic shot), on key <paramref name="key"/>:
        /// what it does in the card's words, then only the lines that apply to it: damage as she deals it (base + flat, and her
        /// share), the weight said plainly ("takes 2 × your +6 damage: +12"), its hits or shots, its cooldown now (and its base when
        /// cut), its reach and its area (with Blast), its marks and what a mark adds, its window or charge (with Wide window), its
        /// crits, its stagger, and what is its own (a tag, a counter, a fuse). <paramref name="taken"/>: what she took for it.
        /// </summary>
        public static SheetSkill Skill(SkillDefinition skill, string key, OfferContext c, IReadOnlyList<string> taken = null, UpgradeTier tier = UpgradeTier.Common)
        {
            c = c ?? new OfferContext();
            if (c.Modifiers == null) c.Modifiers = new RunModifiers();
            var p = new SheetSkill { Key = key ?? "", Tier = tier == UpgradeTier.Common ? "" : UpgradeTiers.Word(tier) };
            if (taken != null) p.Taken.AddRange(taken);
            if (skill == null) BasicShot(p, c);
            else SkillLines(p, skill, c);
            return p;
        }

        static void BasicShot(SheetSkill p, OfferContext c)
        {
            RunModifiers m = c.Modifiers;
            HerStats s = m.Stats;
            p.Name = BasicShotName;
            p.Sentence = BasicShotSentence;
            int flat = s.FlatFor(null);
            p.Lines.Add(new SheetLine("damage", DamageWords(c.BasicShotDamage, flat, BasicShotDealt(c), m.Weapon.ShotDamage(c.BasicShotDamage + flat, false) != c.BasicShotDamage + flat, s.DamageShare)));
            p.Lines.Add(new SheetLine("your damage", WeightWords(null, s)));
            float interval = BasicInterval(c);
            p.Lines.Add(new SheetLine("fire rate", OfferTexts.One(interval > 0f ? 1.0 / interval : 0.0) + " shots/s, " + interval.ToString("0.###", s_Inv) + " s apart"));
            p.Lines.Add(new SheetLine("reach", OfferTexts.Metres(BasicReach(c))));
            p.Lines.Add(new SheetLine("crit", CritWords(s, null)));
        }

        static void SkillLines(SheetSkill p, SkillDefinition k, OfferContext c)
        {
            RunModifiers m = c.Modifiers;
            HerStats s = m.Stats;
            p.Name = k.Name;
            p.Sentence = OfferTexts.SkillSentence(k);
            bool deals = k.Damage > 0;
            if (deals)
            {
                int flat = s.FlatFor(k);
                string each = k.Input == SkillInput.Holding ? " a hit" : k.RecastCount > 1 && !k.OpeningMove ? " a shot" : k.HitCount > 1 ? " a hit" : "";
                string words = DamageWords(k.Damage, flat, m.ScaleDamage(k.Damage + flat), false, s.DamageShare, each);
                if (k.Input == SkillInput.Charge && !k.NoCharge)
                    words += ", at full charge (" + OfferTexts.Pct(k.MinChargeDamage) + " with none)";
                p.Lines.Add(new SheetLine("damage", words));
            }
            p.Lines.Add(new SheetLine("your damage", WeightWords(k, s)));
            string hits = HitWords(k);
            if (hits.Length > 0) p.Lines.Add(new SheetLine(k.Input == SkillInput.Holding ? "hits" : "shots", hits));

            float now = k.Cooldown * s.CooldownShare(k.Name);
            p.Lines.Add(new SheetLine("cooldown", OfferTexts.Sec(now) + (now < k.Cooldown - 1e-4f ? "  (base " + OfferTexts.Sec(k.Cooldown) + ")" : "")));

            string reach = ReachWords(k, s, m.StatValues ?? new StatValues());
            if (reach.Length > 0) p.Lines.Add(new SheetLine(k.Input == SkillInput.KeyMove ? "slide" : "reach", reach));

            string marks = MarkWords(k, m);
            if (marks.Length > 0)
            {
                p.Lines.Add(new SheetLine("marks", marks));
                float lifetime = (c.Kit ?? new KitValues()).MarkLifetime + m.MarkLifetimeBonus;
                p.Lines.Add(new SheetLine("a mark", "lasts " + OfferTexts.Sec(lifetime) + (s.MarkedPrey > 1f ? "; your hits deal " + OfferTexts.Plus(s.MarkedPrey - 1f) + " to a marked enemy" : "")));
            }

            string window = WindowWords(k, c, s, out string windowLabel);
            if (window.Length > 0) p.Lines.Add(new SheetLine(windowLabel, window));
            if (deals) p.Lines.Add(new SheetLine("crit", CritWords(s, k)));
            if (k.Stagger > 0f) p.Lines.Add(new SheetLine("stagger", (k.Stagger * s.Stagger).ToString("0.#", s_Inv) + (k.Input == SkillInput.Holding ? " a hit" : "")));
            OwnLines(p, k);
        }

        /// <summary>"232   (220 base + 12 flat, +10 % damage dealt)"; the base alone when nothing is added.</summary>
        static string DamageWords(int baseDamage, int flat, int dealt, bool parts, float share, string each = "")
        {
            string words = dealt.ToString(s_Inv) + each;
            if (flat == 0 && dealt == baseDamage) return words;
            string why = baseDamage.ToString(s_Inv) + " base" + (flat != 0 ? " + " + flat.ToString(s_Inv) + " flat" : "");
            if (parts) why += ", your pistols' parts";
            if (Math.Abs(share - 1f) > 1e-5f) why += ", " + OfferTexts.Plus(share - 1f) + " damage dealt";
            return words + "   (" + why + ")";
        }

        /// <summary>
        /// The weight said plainly (work order 095: "takes 2 × your +6 damage: +12"): the skill's weight times her flat damage and
        /// what it adds to a hit now; "takes none of your damage" for a weight of 0 (Slide).
        /// </summary>
        public static string WeightWords(SkillDefinition skill, HerStats s)
        {
            float w = FlatDamage.WeightOf(skill);
            if (w <= 0f) return "takes none of your damage";
            return "takes " + w.ToString("0.##", s_Inv) + " × your +" + s.Damage.ToString("0.#", s_Inv) + " damage: +" + FlatDamage.For(skill, s.Damage).ToString(s_Inv);
        }

        static string HitWords(SkillDefinition k)
        {
            var parts = new List<string>();
            if (k.RecastCount > 1 && k.OpeningMove) parts.Add("a hop, then " + (k.RecastCount - 1).ToString(s_Inv) + " shot" + (k.RecastCount > 2 ? "s" : ""));
            else if (k.RecastCount > 1) parts.Add(k.RecastCount.ToString(s_Inv) + " taps, a shot each");
            else if (k.Input == SkillInput.Holding)
            {
                int most = k.TickInterval > 0f ? (int)Math.Floor(k.HoldMax / k.TickInterval + 1e-4) + 1 : 1;
                parts.Add("a hit every " + OfferTexts.Sec(k.TickInterval) + " while held, " + most.ToString(s_Inv) + " in " + OfferTexts.Sec(k.HoldMax));
            }
            else if (k.HitCount > 1) parts.Add(k.HitCount.ToString(s_Inv) + " hits, " + OfferTexts.Sec(k.HitInterval) + " apart");
            if (k.Charges > 1) parts.Add(k.Charges.ToString(s_Inv) + " charges");
            return string.Join(", ", parts);
        }

        static string ReachWords(SkillDefinition k, HerStats s, StatValues v)
        {
            if (k.Input == SkillInput.KeyMove) return OfferTexts.Metres(Math.Abs(k.MoveDistance)) + " the way you move";
            var parts = new List<string> { (k.Input == SkillInput.Point ? "thrown up to " : "") + OfferTexts.Metres(k.Range) };
            SkillStatLayer layer = s.SkillLayer;
            bool area = Blast.IsArea(k, layer.AreaSkills ?? v.BlastSkills);
            if (k.Shape == SkillShape.Circle) parts.Add("a burst " + OfferTexts.Metres(k.Radius) + " round");
            else if (k.Shape == SkillShape.Cone) parts.Add("a cone " + k.Angle.ToString("0", s_Inv) + "°");
            else if (area) parts.Add("a line " + OfferTexts.Metres(2f * k.HalfWidth) + " wide");
            string words = string.Join(", ", parts);
            if (area && s.Blast > 0f) words += "  (" + OfferTexts.Plus(s.Blast) + " blast)";
            if (k.MoveDistance != 0f) words += "; hops " + OfferTexts.Metres(Math.Abs(k.MoveDistance)) + (k.MoveDistance < 0f ? " back" : " forward");
            return words;
        }

        static string MarkWords(SkillDefinition k, RunModifiers m)
        {
            var parts = new List<string>();
            if (k.MarksAdded > 0)
            {
                int first = k.MarksAdded + m.FirstMarkBonus(k.Name) + m.Meta.FirstMarkExtra;
                parts.Add("puts " + k.MarksAdded.ToString(s_Inv) + " on what it hits" + (first > k.MarksAdded ? ", " + first.ToString(s_Inv) + " on an unmarked enemy" : "")
                          + (k.MarkInterval > 0f ? ", once every " + OfferTexts.Sec(k.MarkInterval) : ""));
            }
            if (k.CleanTapMarks > 0) parts.Add("a clean last tap puts " + k.CleanTapMarks.ToString(s_Inv));
            if (k.MarkDamageBonus > 0f)
                parts.Add("spends every mark: " + OfferTexts.Plus(k.MarkDamageBonus * (k.CloseRange > 0f ? k.CloseMarkScale : 1f)) + " damage a mark"
                          + (k.CloseRange > 0f ? " within " + OfferTexts.Metres(k.CloseRange) : ""));
            if (k.CleanMarkDamageBonus > 0f) parts.Add("a clean shot spends every mark: " + OfferTexts.Plus(k.CleanMarkDamageBonus) + " damage a mark");
            if (k.MarkStaggerBonus > 0f) parts.Add("spends every mark: " + OfferTexts.Plus(k.MarkStaggerBonus) + " stagger a mark");
            return string.Join("; ", parts);
        }

        static string WindowWords(SkillDefinition k, OfferContext c, HerStats s, out string label)
        {
            label = "window";
            string wide = s.WideWindow > 0f ? "  (" + OfferTexts.Plus(s.WideWindow) + " wide window)" : "";
            if (k.RecastCount > 1)
            {
                string words = "the next press within " + OfferTexts.Sec(k.RecastWindow);
                if (WideWindow.HasCleanPart(k))
                    words += ", its clean part " + WideWindow.CleanPartOf(k, c.Kit ?? new KitValues(), s.WideWindow).Width.ToString("0.00", s_Inv) + " s" + wide;
                return words;
            }
            if (k.Input == SkillInput.Charge)
            {
                label = "charge";
                if (k.NoCharge) return "none: it fires at once, always as a perfect release";
                string words = "full in " + OfferTexts.Sec(k.ChargeTime) + ", fires by itself at " + OfferTexts.Sec(k.HoldMax);
                if (WideWindow.HasBand(k))
                    words += "; perfect band " + (k.PerfectTo - k.PerfectFrom).ToString("0.00", s_Inv) + " s (×" + k.PerfectScale.ToString("0.##", s_Inv) + ")" + wide;
                return words;
            }
            if (k.Input == SkillInput.Casting)
            {
                label = "cast";
                return OfferTexts.Sec(k.Startup) + " before the hit";
            }
            return "";
        }

        /// <summary>Her crit as it applies to this skill (null: the basic shot): the chance and what a crit adds; a clean press or a perfect release always crits, and her Clean press on them.</summary>
        static string CritWords(HerStats s, SkillDefinition k)
        {
            if (!s.On) return "no crits (her stats are off)";
            string words = OfferTexts.Pct(s.CritChance) + " chance, a crit deals " + OfferTexts.Plus(s.CritDamage);
            bool clean = k != null && WideWindow.HasCleanPart(k), band = k != null && WideWindow.HasBand(k);
            if (clean) words += "; a clean last press always crits";
            else if (band) words += "; a perfect release always crits";
            if ((clean || band) && s.CleanPress > 1f) words += ", " + OfferTexts.Plus(s.CleanPress - 1f) + " clean press";
            return words;
        }

        /// <summary>What is its own: a tag, a counter, a fuse.</summary>
        static void OwnLines(SheetSkill p, SkillDefinition k)
        {
            if (k.TagSeconds > 0f)
                p.Lines.Add(new SheetLine("tag", "the first enemy hit takes " + OfferTexts.Plus(k.TagDamageShare) + " from you for " + OfferTexts.Sec(k.TagSeconds)
                                                 + (k.TagJump > 0f ? "; it jumps up to " + OfferTexts.Metres(k.TagJump) : "")));
            if (k.CounterDaze > 0f)
                p.Lines.Add(new SheetLine("counter", "a blue attack from " + (k.CounterHalfAngle >= 179.5f ? "any side" : "a " + (2f * k.CounterHalfAngle).ToString("0", s_Inv) + "° front")
                                                     + ": dazed " + OfferTexts.Sec(k.CounterDaze) + ", focus " + OfferTexts.Plus(k.CounterFocus)));
            if (k.Fuse > 0f)
                p.Lines.Add(new SheetLine("fuse", "bursts after " + OfferTexts.Sec(k.Fuse) + "; shot through, at once and ×" + k.ShotBurstScale.ToString("0.##", s_Inv)));
        }

        /// <summary>
        /// What she took this run for a skill: the elite room's upgrades (<paramref name="eliteNames"/>), then each chest step that
        /// names it, by the card's name and its step ("Cooldown -5 %", "First mark +1"). For the basic shot (<paramref name="skill"/>
        /// null): her attack speed's and reach's steps, and her pistols' parts.
        /// </summary>
        public static List<string> TakenFor(string skill, RunModifiers m, IEnumerable<string> eliteNames = null)
        {
            var list = new List<string>();
            if (eliteNames != null) list.AddRange(eliteNames);
            if (m == null) return list;
            foreach (Upgrade u in m.Taken)
            {
                bool mine = skill == null ? u.Kind == UpgradeKind.AttackSpeed || u.Kind == UpgradeKind.LongerBarrel
                                          : (u.Kind == UpgradeKind.Cooldown || u.Kind == UpgradeKind.FirstMark) && u.Skill == skill;
                if (!mine) continue;
                string step = u.Kind == UpgradeKind.Cooldown ? OfferTexts.Minus(u.Amount)
                            : u.Kind == UpgradeKind.AttackSpeed ? OfferTexts.Plus(u.Amount)
                            : u.Kind == UpgradeKind.LongerBarrel ? "+" + OfferTexts.Metres(u.Amount)
                            : "+" + OfferTexts.Int(u.Amount);
                list.Add(OfferTexts.Name(u) + " " + step + (u.Tiered && u.Tier != UpgradeTier.Common ? " (" + u.TierWord + ")" : ""));
            }
            if (skill == null && m.Weapon.Any) list.AddRange(m.Weapon.Parts);
            return list;
        }
    }
}
