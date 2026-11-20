using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;

namespace Roguelike.Rules
{
    /// <summary>
    /// What a skill upgrade is (work order 017, 2026-11-20, Kristian: "upgrade paths during the run that change how you
    /// play and what the optimal rotation is"): Numbers, the same skill stronger; Change, it is played differently
    /// afterwards. The choice on screen shows which.
    /// </summary>
    public enum SkillUpgradeKind { Numbers, Change }

    /// <summary>How a patch changes a field of the run's copy of a skill.</summary>
    public enum PatchOp
    {
        /// <summary>The field becomes Value (a number, a switch: 0 off, else on; an enum: its number), or Text for a word.</summary>
        Set,
        /// <summary>The field is multiplied by Value.</summary>
        Scale,
        /// <summary>Value is added to the field.</summary>
        Add,
    }

    /// <summary>
    /// One change to one field of a skill, by the field's name (<see cref="SkillDefinition"/>): a line of data, so a
    /// "numbers" upgrade needs no code (work order 017). A whole-number field is rounded after a Scale or an Add.
    /// </summary>
    [Serializable]
    public sealed class SkillPatch
    {
        public string Field = "";
        public PatchOp Op;
        public float Value;
        public string Text = "";

        public SkillPatch() { }

        public SkillPatch(string field, PatchOp op, float value, string text = "")
        {
            Field = field;
            Op = op;
            Value = value;
            Text = text ?? "";
        }

        public static SkillPatch Set(string field, float value) => new SkillPatch(field, PatchOp.Set, value);
        public static SkillPatch SetText(string field, string text) => new SkillPatch(field, PatchOp.Set, 0f, text);
        public static SkillPatch Scale(string field, float by) => new SkillPatch(field, PatchOp.Scale, by);
        public static SkillPatch Add(string field, float by) => new SkillPatch(field, PatchOp.Add, by);

        static readonly Dictionary<string, FieldInfo> s_Fields =
            typeof(SkillDefinition).GetFields(BindingFlags.Public | BindingFlags.Instance).ToDictionary(f => f.Name, f => f);

        /// <summary>True when <see cref="Field"/> names a field of a skill that this patch can change.</summary>
        public bool IsValid =>
            s_Fields.TryGetValue(Field ?? "", out FieldInfo f)
            && (f.FieldType == typeof(float) || f.FieldType == typeof(int) || f.FieldType == typeof(bool) || f.FieldType.IsEnum
                || (f.FieldType == typeof(string) && Op == PatchOp.Set));

        /// <summary>Changes the field on <paramref name="skill"/>. A field it does not know, or cannot change so, is left alone.</summary>
        public void ApplyTo(SkillDefinition skill)
        {
            if (skill == null || !IsValid) return;
            FieldInfo f = s_Fields[Field];
            Type t = f.FieldType;
            if (t == typeof(string))
            {
                f.SetValue(skill, Text ?? "");
                return;
            }
            double now = t == typeof(float) ? (float)f.GetValue(skill)
                       : t == typeof(int) ? (int)f.GetValue(skill)
                       : t == typeof(bool) ? ((bool)f.GetValue(skill) ? 1 : 0)
                       : Convert.ToInt32(f.GetValue(skill), CultureInfo.InvariantCulture);
            double next = Op == PatchOp.Set ? Value : Op == PatchOp.Scale ? now * Value : now + Value;
            if (t == typeof(float)) f.SetValue(skill, (float)next);
            else if (t == typeof(int)) f.SetValue(skill, (int)Math.Round(next, MidpointRounding.AwayFromZero));
            else if (t == typeof(bool)) f.SetValue(skill, Math.Abs(next) > 1e-6);
            else f.SetValue(skill, Enum.ToObject(t, (int)Math.Round(next)));
        }

        public override string ToString() =>
            Field + (Op == PatchOp.Set ? " = " + (Text.Length > 0 ? Text : Value.ToString("0.###", CultureInfo.InvariantCulture))
                    : Op == PatchOp.Scale ? " x " + Value.ToString("0.###", CultureInfo.InvariantCulture)
                    : " + " + Value.ToString("0.###", CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// One upgrade of one skill (work order 017): the skill's name, the upgrade's name, one line of plain words on what
    /// changes for the player, its kind, the upgrades of the same skill it cannot be taken with, and the patches it makes
    /// to the run's copy of the skill. What a Change does beyond its patches is a field of the skill the game reads
    /// (<see cref="SkillDefinition.BasicHitCooldownCut"/> and the others of work order 017), set by a patch too.
    /// </summary>
    [Serializable]
    public sealed class SkillUpgradeDefinition
    {
        public string Skill = "";
        public string Name = "";
        public SkillUpgradeKind Kind;
        public string Line = "";
        public string[] Excludes = Array.Empty<string>();
        public SkillPatch[] Patches = Array.Empty<SkillPatch>();

        public SkillUpgradeDefinition() { }

        public SkillUpgradeDefinition(string skill, string name, SkillUpgradeKind kind, string line, string[] excludes, params SkillPatch[] patches)
        {
            Skill = skill;
            Name = name;
            Kind = kind;
            Line = line;
            Excludes = excludes ?? Array.Empty<string>();
            Patches = patches ?? Array.Empty<SkillPatch>();
        }

        /// <summary>"Quick Shot: Double Tap", for the log and the record.</summary>
        public string FullName => Skill + ": " + Name;
        /// <summary>"numbers" or "change", as the choice shows it.</summary>
        public string KindWord => Kind == SkillUpgradeKind.Change ? "change" : "numbers";

        /// <summary>True when the two cannot both be taken: one of the same skill names the other among its exclusions.</summary>
        public bool CannotGoWith(SkillUpgradeDefinition other) =>
            other != null && other.Skill == Skill
            && ((Excludes != null && Excludes.Contains(other.Name)) || (other.Excludes != null && other.Excludes.Contains(Name)));

        public void ApplyTo(SkillDefinition skill)
        {
            if (Patches == null) return;
            foreach (SkillPatch p in Patches) p?.ApplyTo(skill);
        }

        public override string ToString() => FullName;
    }

    /// <summary>
    /// The first table of skill upgrades (work order 017, 2026-11-20). All first guesses; names are placeholders. Every
    /// row is data: a Numbers row is only patches, a Change row is patches too, some of them switching on a field the
    /// game reads in one plain hook (a hit, a kill, a daze, a counter, a basic shot during Slide).
    /// </summary>
    public static class SkillUpgradeTable
    {
        static readonly string[] None = Array.Empty<string>();
        const SkillUpgradeKind N = SkillUpgradeKind.Numbers, C = SkillUpgradeKind.Change;

        static SkillUpgradeDefinition Row(string skill, string name, SkillUpgradeKind kind, string line, params SkillPatch[] patches) =>
            new SkillUpgradeDefinition(skill, name, kind, line, None, patches);

        static SkillUpgradeDefinition Row(string skill, string name, SkillUpgradeKind kind, string line, string excludes, params SkillPatch[] patches) =>
            new SkillUpgradeDefinition(skill, name, kind, line, new[] { excludes }, patches);

        public static SkillUpgradeDefinition[] Create() => new[]
        {
            // The start skills first: the order's first two of each, then their thirds.
            Row("Quick Shot", "Double Tap", C, "Holds two charges: fire it twice in a row.",
                SkillPatch.Add(nameof(SkillDefinition.Charges), 1)),
            Row("Quick Shot", "Quick Draw", C, "Every basic shot that hits takes 0.3 s off its cooldown.",
                SkillPatch.Add(nameof(SkillDefinition.BasicHitCooldownCut), 0.3f)),
            Row("Triple Tap", "Fourth Tap", C, "Four presses instead of three; the clean press is the fourth.",
                SkillPatch.Add(nameof(SkillDefinition.RecastCount), 1)),
            Row("Triple Tap", "Tied Trigger", C, "Every tap that hits takes 0.5 s off Scatter Blast's cooldown.",
                SkillPatch.Add(nameof(SkillDefinition.HitCooldownCut), 0.5f), SkillPatch.SetText(nameof(SkillDefinition.HitCutsSkill), "Scatter Blast")),
            Row("Scatter Blast", "Slug", C, "A narrow line that reaches 10 m instead of a short cone: no walking in.", "Point Blank",
                SkillPatch.Set(nameof(SkillDefinition.Shape), (float)SkillShape.Line), SkillPatch.Set(nameof(SkillDefinition.Range), 10f),
                SkillPatch.Set(nameof(SkillDefinition.HalfWidth), 0.4f)),
            Row("Scatter Blast", "Point Blank", C, "Within 2.5 m: twice the bonus per mark. Farther: no bonus, and the marks stay.", "Slug",
                SkillPatch.Set(nameof(SkillDefinition.CloseRange), 2.5f), SkillPatch.Set(nameof(SkillDefinition.CloseMarkScale), 2f)),
            Row("Backstep", "Forward Step", C, "The hop goes toward the cursor instead of away from it.",
                SkillPatch.Scale(nameof(SkillDefinition.MoveDistance), -1f)),
            Row("Backstep", "Long Step", N, "The hop is 8 m, and the window for the shot is 0.2 s longer.",
                SkillPatch.Scale(nameof(SkillDefinition.MoveDistance), 1.6f), SkillPatch.Add(nameof(SkillDefinition.RecastWindow), 0.2f)),
            Row("Quick Shot", "Heavy Round", N, "60% more damage and twice the stagger.",
                SkillPatch.Scale(nameof(SkillDefinition.Damage), 1.6f), SkillPatch.Scale(nameof(SkillDefinition.Stagger), 2f)),
            Row("Triple Tap", "Sharp Taps", N, "Each tap deals 40% more damage.",
                SkillPatch.Scale(nameof(SkillDefinition.Damage), 1.4f)),
            Row("Scatter Blast", "Fresh Shells", C, "If it kills something, its cooldown is halved.",
                SkillPatch.Set(nameof(SkillDefinition.KillCooldownCut), 0.5f)),
            Row("Backstep", "Second Step", C, "Holds two charges.",
                SkillPatch.Add(nameof(SkillDefinition.Charges), 1)),

            // One for each pool skill.
            Row("Long Shot", "Snap Shot", C, "No charging: it fires at once, always as a perfect release. 6 s more cooldown.", "Overcharge",
                SkillPatch.Set(nameof(SkillDefinition.NoCharge), 1f), SkillPatch.Add(nameof(SkillDefinition.Cooldown), 6f)),
            Row("Powder Keg", "Twin Kegs", C, "Holds two charges: two kegs down at once.",
                SkillPatch.Add(nameof(SkillDefinition.Charges), 1)),
            Row("Fan Fire", "Walking Fire", C, "You are not slowed while you hold it.",
                SkillPatch.Set(nameof(SkillDefinition.Lock), (float)SkillLock.Light)),
            Row("Heavy Volley", "Quick Cast", C, "The cast is 0.6 s instead of 1 s; it deals 20% less.",
                SkillPatch.Set(nameof(SkillDefinition.Startup), 0.6f), SkillPatch.Scale(nameof(SkillDefinition.Damage), 0.8f)),
            Row("Tag Round", "Spreading Tag", C, "When a tagged target dies, the tag jumps to the nearest enemy within 6 m, renewed.",
                SkillPatch.Set(nameof(SkillDefinition.TagJump), 6f)),
            Row("Breaker Slug", "Follow Through", C, "If it dazes its target, its cooldown is halved.",
                SkillPatch.Set(nameof(SkillDefinition.DazeCooldownCut), 0.5f)),
            Row("Slide", "Slide Shot", C, "Basic shots that hit during the slide each add a mark.",
                SkillPatch.Set(nameof(SkillDefinition.SlideShotMarks), 1f)),
            Row("Stopping Shot", "Riposte", C, "A counter makes Scatter Blast ready at once.",
                SkillPatch.SetText(nameof(SkillDefinition.CounterReadies), "Scatter Blast")),

            // The rest.
            Row("Long Shot", "Overcharge", C, "The perfect band is half as wide; a perfect release deals three times the damage.", "Snap Shot",
                SkillPatch.Add(nameof(SkillDefinition.PerfectFrom), 0.05f), SkillPatch.Add(nameof(SkillDefinition.PerfectTo), -0.05f),
                SkillPatch.Set(nameof(SkillDefinition.PerfectScale), 3f)),
            Row("Powder Keg", "Big Keg", N, "A third wider and 30% more damage.",
                SkillPatch.Scale(nameof(SkillDefinition.Radius), 4f / 3f), SkillPatch.Scale(nameof(SkillDefinition.Damage), 1.3f)),
            Row("Fan Fire", "Marking Fire", N, "It marks twice as often.",
                SkillPatch.Scale(nameof(SkillDefinition.MarkInterval), 0.5f)),
            Row("Heavy Volley", "Execution", N, "Twice the damage to a dazed target.",
                SkillPatch.Set(nameof(SkillDefinition.DazedDamageScale), 2f)),
            Row("Tag Round", "Deep Tag", N, "A tagged target takes 20% more instead of 10%.",
                SkillPatch.Scale(nameof(SkillDefinition.TagDamageShare), 2f)),
            Row("Slide", "Third Wind", N, "Holds three charges.",
                SkillPatch.Add(nameof(SkillDefinition.Charges), 1)),
            Row("Stopping Shot", "Wide Counter", N, "The counter works from any side of the enemy.",
                SkillPatch.Set(nameof(SkillDefinition.CounterHalfAngle), 180f)),
        };

        /// <summary>The row of this skill and name in a table, or null.</summary>
        public static SkillUpgradeDefinition Find(IEnumerable<SkillUpgradeDefinition> table, string skill, string name) =>
            table?.FirstOrDefault(u => u != null && u.Skill == skill && u.Name == name);
    }

    /// <summary>
    /// The skill upgrades a run has taken (work order 017), in order, and the run's own view of the skills: a copy of
    /// each definition, patched by the upgrades taken for it in the order taken (<see cref="View"/>). Nothing is ever
    /// written to the definitions it is given: the settings asset, and the skills the practice room reads, stay as they
    /// are. A new run starts with none (<see cref="Clear"/>).
    /// </summary>
    public sealed class RunSkillUpgrades
    {
        readonly List<SkillUpgradeDefinition> _taken = new List<SkillUpgradeDefinition>();
        IReadOnlyList<SkillDefinition> _viewOf;
        SkillDefinition[] _view;
        int _viewVersion = -1;

        public IReadOnlyList<SkillUpgradeDefinition> Taken => _taken;
        public int Count => _taken.Count;
        /// <summary>Raised by one at every change: a view made before it is stale.</summary>
        public int Version { get; private set; }

        public void Take(SkillUpgradeDefinition upgrade)
        {
            if (upgrade == null) return;
            _taken.Add(upgrade);
            Version++;
        }

        public void Clear()
        {
            if (_taken.Count == 0) return;
            _taken.Clear();
            Version++;
        }

        public bool Has(string skill, string name) => _taken.Any(u => u.Skill == skill && u.Name == name);

        /// <summary>How many upgrades a skill has, by its name: the pips on its box.</summary>
        public int CountFor(string skill) => _taken.Count(u => u.Skill == skill);

        /// <summary>The names of a skill's upgrades, in the order taken.</summary>
        public List<string> NamesFor(string skill) => _taken.Where(u => u.Skill == skill).Select(u => u.Name).ToList();

        /// <summary>
        /// The run's view of these skills: the same list when no upgrade is taken, else a copy of every skill, each patched
        /// by its upgrades in the order taken. Made again only when the list or the upgrades change.
        /// </summary>
        public IReadOnlyList<SkillDefinition> View(IReadOnlyList<SkillDefinition> skills)
        {
            if (skills == null || _taken.Count == 0) return skills;
            if (_view != null && ReferenceEquals(_viewOf, skills) && _viewVersion == Version) return _view;
            _viewOf = skills;
            _viewVersion = Version;
            _view = new SkillDefinition[skills.Count];
            for (int i = 0; i < skills.Count; i++) _view[i] = Patched(skills[i], _taken);
            return _view;
        }

        /// <summary>A copy of the skill with every upgrade of the list that is its own applied, in order; null for null.</summary>
        public static SkillDefinition Patched(SkillDefinition skill, IEnumerable<SkillUpgradeDefinition> upgrades)
        {
            if (skill == null) return null;
            SkillDefinition copy = skill.Copy();
            foreach (SkillUpgradeDefinition u in upgrades)
                if (u != null && u.Skill == skill.Name) u.ApplyTo(copy);
            return copy;
        }

        /// <summary>"Quick Shot: Double Tap, Backstep: Long Step"; "none" without.</summary>
        public string Text() => _taken.Count == 0 ? "none" : string.Join(", ", _taken.Select(u => u.FullName));
    }

    /// <summary>
    /// The offer when an elite room is cleared (work order 017): <see cref="SkillUpgradeValues.Offered"/> upgrades, drawn
    /// from the run's seed and the floor's number, each for a skill she has now, never one she has taken or one an
    /// upgrade she has taken excludes, and for as many different skills as the table allows. At least one is a Change
    /// whenever one can be offered. Fewer left, what is left; none, an empty offer (the run heals her instead). The same
    /// run seed with the same picks offers the same upgrades.
    /// </summary>
    public static class SkillUpgradeOffer
    {
        public const string Stage = "skill upgrade";

        /// <summary>The upgrades she may still be offered: of skills she has, not taken, not excluded by one taken.</summary>
        public static List<SkillUpgradeDefinition> Open(IEnumerable<SkillUpgradeDefinition> table, ICollection<string> skillsHad,
                                                        IReadOnlyList<SkillUpgradeDefinition> taken)
        {
            var open = new List<SkillUpgradeDefinition>();
            if (table == null || skillsHad == null) return open;
            foreach (SkillUpgradeDefinition u in table)
            {
                if (u == null || !skillsHad.Contains(u.Skill)) continue;
                if (taken != null && taken.Any(t => t == u || (t.Skill == u.Skill && t.Name == u.Name) || t.CannotGoWith(u))) continue;
                open.Add(u);
            }
            return open;
        }

        public static List<SkillUpgradeDefinition> Offer(ulong runSeed, int floor, IEnumerable<SkillUpgradeDefinition> table,
                                                         ICollection<string> skillsHad, IReadOnlyList<SkillUpgradeDefinition> taken, int count)
        {
            List<SkillUpgradeDefinition> open = Open(table, skillsHad, taken);
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor);
            rng.Shuffle(open);
            var picks = new List<SkillUpgradeDefinition>();
            count = Math.Max(0, count);
            // Different skills first, in the drawn order; then, if the table has too few skills, any that are left.
            foreach (SkillUpgradeDefinition u in open)
                if (picks.Count < count && picks.All(p => p.Skill != u.Skill)) picks.Add(u);
            foreach (SkillUpgradeDefinition u in open)
                if (picks.Count < count && !picks.Contains(u)) picks.Add(u);
            // At least one Change when one can be offered: the first drawn Change takes the place of the last Numbers
            // pick, of its own skill if one is picked (so the skills stay as many), else the last.
            if (picks.Count > 0 && picks.All(p => p.Kind != SkillUpgradeKind.Change))
            {
                SkillUpgradeDefinition change = open.FirstOrDefault(u => u.Kind == SkillUpgradeKind.Change && !picks.Contains(u));
                if (change != null)
                {
                    int at = picks.FindLastIndex(p => p.Skill == change.Skill);
                    if (at < 0) at = picks.Count - 1;
                    picks[at] = change;
                }
            }
            return picks;
        }
    }

    /// <summary>The numbers of the skill upgrades and the elite room's offer (work order 017), first guesses.</summary>
    [Serializable]
    public sealed class SkillUpgradeValues
    {
        public int Offered = 3;                 // upgrades offered when an elite room is cleared
        public SkillUpgradeDefinition[] Table = SkillUpgradeTable.Create();
    }
}
