using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of an offer's pop-up (work order 065, 2026-11-21, Kristian: "oppgraderingene trenger mer informasjon, de er
    /// ikke så forklarende. gjerne i midten som en større pop-up i det uiet"): the cards side by side in the middle of the
    /// screen, larger, over a dark veil. Sizes are layout pixels of a view 1080 high, its own (not the fight's interface values,
    /// work order 063's, which the producer joins at the merge). All first guesses of the order; nobody has seen them.
    /// </summary>
    [Serializable]
    public sealed class OfferValues
    {
        /// <summary>
        /// On: an offer is the pop-up in the middle with the cards that explain. Off: this morning's offer, the slim cards at
        /// the right edge with this morning's words (work orders 045, 055 and 057), and the old look's boxes as they were, so he
        /// can compare.
        /// </summary>
        public bool PopUp = true;
        /// <summary>A card's width and its least height (the order: "about 380 by 300 at 1080p"); a card is as high as its text needs.</summary>
        public float CardWidth = 380f;
        public float CardHeight = 300f;
        public float CardGap = 28f;
        /// <summary>The row of cards, its lean included, never wider than this share of the view (the order: 85 % at 1280 by 720).</summary>
        public float MostRowShare = 0.85f;
        /// <summary>
        /// How far a card leans: its top this share of its height right of its bottom. Less than the fight's 0.32 (a 300-pixel
        /// card would lean 96 pixels), still the kinetic cut.
        /// </summary>
        public float Slant = 0.16f;
        /// <summary>The dark veil over the paused game behind an offer: its black's opacity (the order's first guess, 55 %). 0: none.</summary>
        public float Veil = 0.55f;
        /// <summary>The row's middle at this share of the view's height, a little over the middle (the heading goes over it).</summary>
        public float RowMiddle = 0.52f;
        /// <summary>The space inside a card's edge, and between its parts.</summary>
        public float Pad = 20f;
        public float PartGap = 7f;
        /// <summary>Room kept at a part's right for its lean: a part about 100 pixels high leans 16 pixels.</summary>
        public float LeanRoom = 16f;
        /// <summary>The key's plate at a card's top left, and its numeral (the order: "the keys that pick are shown large").</summary>
        public float KeyPlate = 58f;
        public int KeySize = 44;
        /// <summary>The sizes of a card's parts: its name, its tier's word, the line under the name, its sentence, its numbers, a note, a cost.</summary>
        public int NameSize = 34;
        public int TierSize = 21;
        public int ForSize = 18;
        public int SentenceSize = 21;
        public int NumberSize = 25;
        public int NoteSize = 18;
        public int CostSize = 20;
        /// <summary>The offer's heading over the row, its line under it, and the footer under the row.</summary>
        public int HeadingSize = 40;
        public int SubSize = 21;
        public int FooterSize = 18;
        /// <summary>A sentence fits in this many lines of its size at the card's width (the order: "no sentence longer than two lines").</summary>
        public int MostSentenceLines = 2;
        /// <summary>The focus choice's card keeps this much room at its foot for the small drawing of the gauge.</summary>
        public float GaugeRoom = 22f;

        // ---------------- The old look (the interface's switch off): plain boxes side by side in the middle, in its 720 layout ----------------
        public float PlainCardWidth = 300f;
        public float PlainCardGap = 10f;
        public float PlainLeastHeight = 120f;
    }

    /// <summary>What a part of a card is: each has a size and an ink of its own.</summary>
    public enum OfferPartKind
    {
        /// <summary>The tier's word (and what a tier adds), in the tier's ink: "RARE", "EPIC   +30% damage and stagger".</summary>
        Tier,
        /// <summary>A small line under the name: a skill's key and cooldown, the skill an elite's upgrade is for.</summary>
        For,
        /// <summary>One plain sentence of what it does for her.</summary>
        Sentence,
        /// <summary>A number line, before and after: "3.8  »  4.0 shots a second".</summary>
        Numbers,
        /// <summary>A note: an epic's change and its sentence.</summary>
        Note,
        /// <summary>A cost, in the red: what it takes away.</summary>
        Cost,
    }

    /// <summary>One part of a card: its kind and its words.</summary>
    public struct OfferPart
    {
        public OfferPartKind Kind;
        public string Text;

        public OfferPart(OfferPartKind kind, string text)
        {
            Kind = kind;
            Text = text ?? "";
        }

        public override string ToString() => Kind + ": " + Text;
    }

    /// <summary>
    /// One card of an offer as it reads (work order 065): its name, its tier when it has one, one plain sentence, the numbers
    /// before and after, notes and a cost. Made once when the offer opens; the drawing measures and draws its parts.
    /// </summary>
    public sealed class OfferCard
    {
        public string Name = "";
        public bool Tiered;
        public UpgradeTier Tier;
        /// <summary>The tier's line: its word in capitals, and for a skill what the tier adds ("RARE   +30% damage and stagger"); "" without.</summary>
        public string TierLine = "";
        public string For = "";
        public string Sentence = "";
        public readonly List<string> Numbers = new List<string>();
        public readonly List<string> Notes = new List<string>();
        public string Cost = "";

        /// <summary>The parts under the name, in the order drawn; empty ones left out.</summary>
        public List<OfferPart> Parts()
        {
            var parts = new List<OfferPart>();
            if (TierLine.Length > 0) parts.Add(new OfferPart(OfferPartKind.Tier, TierLine));
            if (For.Length > 0) parts.Add(new OfferPart(OfferPartKind.For, For));
            if (Sentence.Length > 0) parts.Add(new OfferPart(OfferPartKind.Sentence, Sentence));
            foreach (string n in Numbers) if (!string.IsNullOrEmpty(n)) parts.Add(new OfferPart(OfferPartKind.Numbers, n));
            foreach (string n in Notes) if (!string.IsNullOrEmpty(n)) parts.Add(new OfferPart(OfferPartKind.Note, n));
            if (Cost.Length > 0) parts.Add(new OfferPart(OfferPartKind.Cost, Cost));
            return parts;
        }

        /// <summary>Everything under the name, a part a line.</summary>
        public string Body => string.Join("\n", Parts().Select(p => p.Text));

        /// <summary>The whole card as it reads: its name, then its parts.</summary>
        public override string ToString() => Name + "\n" + Body;
    }

    /// <summary>Her values now, that the cards' numbers are read from (work order 065): the run's and her own.</summary>
    public sealed class OfferContext
    {
        /// <summary>The run's modifiers with her picks so far (a card's "after" takes the card and takes it back).</summary>
        public RunModifiers Modifiers = new RunModifiers();
        public RunValues Run = new RunValues();
        public FeelValues Feel = new FeelValues();
        public KitValues Kit = new KitValues();
        public IdentityValues Identity = new IdentityValues();
        public FocusModeValues FocusValues = new FocusModeValues();
        /// <summary>The way the focus gauge works for her now.</summary>
        public FocusMode FocusMode = FocusMode.Today;
        /// <summary>Her own maximum health (before the run's steps), and her health now.</summary>
        public int OwnMaxHealth = Placeholders.PlayerHealth;
        public int Health = Placeholders.PlayerHealth;
        /// <summary>The skills she has, as she has them (patched by the run's upgrades), in the order of her list.</summary>
        public IReadOnlyList<SkillDefinition> Equipped = Array.Empty<SkillDefinition>();
        public int BasicShotDamage = Placeholders.BasicShotDamage;

        public HerStats Stats => (Modifiers ?? (Modifiers = new RunModifiers())).Stats;
        public int MaxHealth => Modifiers.MaxHealthFor(OwnMaxHealth);
        public SkillDefinition Skill(string name) => Equipped?.FirstOrDefault(s => s != null && s.Name == name);
    }

    /// <summary>
    /// What every card of an offer says (work order 065, Kristian, 2026-11-21: "oppgraderingene trenger mer informasjon, de er
    /// ikke så forklarende"): for each kind of chest upgrade, each skill of a floor's reward with its tier, each of the elite
    /// room's skill upgrades and each focus choice, its name, one plain sentence written for a player who has never seen it,
    /// and its numbers before and after, read from her values now (her stats, the skill as she has it), never written by hand,
    /// so they are always true: a chest card's "after" is her stats with the card taken. Where a thing has a cost, the cost is
    /// a line of its own. English, as the game's text is.
    /// </summary>
    public static class OfferTexts
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>Between a number before and after. The game's fonts (Barlow Condensed) have no arrow: the guillemet reads as one.</summary>
        public const string Arrow = "  »  ";
        /// <summary>Between the parts of a line under a card's name.</summary>
        public const string Dot = "  ·  ";

        // ---------------- Number words ----------------

        public static string Pct(double share) => (share * 100.0).ToString("0.#", s_Inv) + " %";
        public static string Plus(double share) => "+" + Pct(share);
        public static string Minus(double share) => "-" + Pct(share);
        public static string Sec(double seconds) => seconds.ToString("0.##", s_Inv) + " s";
        public static string Metres(double metres) => metres.ToString("0.#", s_Inv) + " m";
        public static string One(double x) => x.ToString("0.0", s_Inv);
        public static string Int(double x) => ((int)Math.Round(x, MidpointRounding.AwayFromZero)).ToString(s_Inv);
        static string Times(double x) => "×" + x.ToString("0.##", s_Inv);

        /// <summary>A number line: its label, the number before, the arrow, the number after, and a unit after it when there is one.</summary>
        public static string Line(string label, string before, string after, string unit = "") =>
            (string.IsNullOrEmpty(label) ? "" : label + "  ") + before + Arrow + after + (string.IsNullOrEmpty(unit) ? "" : " " + unit);

        // ---------------- A chest's upgrade ----------------

        /// <summary>The card of a chest's upgrade: its stat's name, its tier, its sentence and its numbers before and after taking it.</summary>
        public static OfferCard ForUpgrade(Upgrade u, OfferContext c)
        {
            c = c ?? new OfferContext();
            var card = new OfferCard { Name = Name(u), Tiered = u.Tiered, Tier = u.Tier, TierLine = u.Tiered ? u.TierWord.ToUpperInvariant() : "" };
            card.Sentence = Sentence(u, c);
            var labels = new List<string>();
            var units = new List<string>();
            var before = new List<string>();
            var after = new List<string>();
            Read(u, c, false, labels, units, before);
            RunModifiers m = c.Modifiers;
            m.Take(u);
            try
            {
                Read(u, c, true, null, null, after);
            }
            finally
            {
                m.TakeBack(u);
            }
            for (int i = 0; i < before.Count && i < after.Count; i++) card.Numbers.Add(Line(labels[i], before[i], after[i], units[i]));
            return card;
        }

        /// <summary>A chest upgrade's name, short and plain: its stat.</summary>
        public static string Name(Upgrade u)
        {
            switch (u.Kind)
            {
                case UpgradeKind.FlatDamage: return "Damage";
                case UpgradeKind.AttackSpeed: return "Attack speed";
                case UpgradeKind.CritChance: return "Crit chance";
                case UpgradeKind.CritDamage: return "Crit damage";
                case UpgradeKind.Luck: return "Luck";
                case UpgradeKind.MaxHealth: return "Health";
                case UpgradeKind.RunSpeed: return "Move speed";
                case UpgradeKind.Cooldown: return "Cooldown";
                case UpgradeKind.LongerBarrel: return "Reach";
                case UpgradeKind.HeavyHits: return "Stagger";
                case UpgradeKind.DashCooldown: return "Dash";
                case UpgradeKind.Mend: return "Mend";
                case UpgradeKind.LastingMarks: return "Lasting marks";
                case UpgradeKind.KeenFocus: return "Keen focus";
                case UpgradeKind.Damage: return "Damage dealt";
                case UpgradeKind.AllCooldowns: return "Cooldowns";
                case UpgradeKind.CleanPress: return "Clean press";
                case UpgradeKind.MarkedPrey: return "Marked prey";
                case UpgradeKind.Mending: return "Mending";
                case UpgradeKind.LongFocus: return "Long focus";
                case UpgradeKind.SecondDash: return "Second dash";
                case UpgradeKind.WideWindow: return "Wide window"; // work order 067
                case UpgradeKind.Blast: return "Blast";
                default: return "First mark";
            }
        }

        /// <summary>A chest upgrade's one sentence, for a player who has never seen it.</summary>
        public static string Sentence(Upgrade u, OfferContext c)
        {
            switch (u.Kind)
            {
                case UpgradeKind.FlatDamage: return "Every shot deals more; a skill adds it by its weight.";
                case UpgradeKind.AttackSpeed: return "Your basic shot fires faster.";
                case UpgradeKind.CritChance: return "More of your hits crit; a clean press always does.";
                case UpgradeKind.CritDamage: return "Your crits deal more, and so does every clean press.";
                case UpgradeKind.Luck: return "Rarer upgrades are offered more often.";
                case UpgradeKind.MaxHealth: return "More maximum health, and you heal as much now.";
                case UpgradeKind.RunSpeed: return "You run faster, and faster while you shoot.";
                case UpgradeKind.Cooldown: return (u.Skill ?? "") + " comes back sooner.";
                case UpgradeKind.LongerBarrel: return "Your basic shot reaches farther.";
                case UpgradeKind.HeavyHits: return "Your hits break an enemy's guard sooner.";
                case UpgradeKind.DashCooldown: return "Your dash comes back sooner.";
                case UpgradeKind.Mend: return "Heal now; your maximum stays.";
                case UpgradeKind.LastingMarks: return "Marks stay on an enemy longer.";
                case UpgradeKind.KeenFocus: return "Your focus gauge fills faster.";
                case UpgradeKind.Damage: return "Everything you do deals more.";
                case UpgradeKind.AllCooldowns: return "Every skill comes back a little sooner.";
                case UpgradeKind.CleanPress: return "A clean press and a perfect release deal more.";
                case UpgradeKind.MarkedPrey: return "Your hits deal more to an enemy that carries marks.";
                case UpgradeKind.Mending: return "Each room you clear heals you more.";
                case UpgradeKind.LongFocus:
                    // Work order 071: with Hair Trigger a part's gains last longer too, and the card says so.
                    return c != null && c.FocusMode == FocusMode.HairTrigger
                        ? (HairTriggerGains(c) ? "A part of focus primes your next skill, and powers you up, for longer." : "A part of focus primes your next skill for longer.")
                        : "Your focus window lasts longer.";
                case UpgradeKind.SecondDash: return "Hold one more dash before it must recharge.";
                case UpgradeKind.WideWindow: return "The clean part of a window and the perfect band are wider."; // work order 067, the order's words
                case UpgradeKind.Blast: return "Your area skills hit a wider area.";
                default: return (u.Skill ?? "") + " puts more marks on an unmarked enemy.";
            }
        }

        /// <summary>
        /// Her numbers that the upgrade changes, as they are now (<paramref name="taken"/> false) or with it taken (true): a
        /// label, a value and a unit each. The labels and units are filled only on the first call.
        /// </summary>
        static void Read(Upgrade u, OfferContext c, bool taken, List<string> labels, List<string> units, List<string> values)
        {
            HerStats s = c.Stats;
            RunModifiers m = c.Modifiers;
            FeelValues f = c.Feel ?? new FeelValues();
            void Add(string label, string value, string unit = "")
            {
                labels?.Add(label);
                units?.Add(unit);
                values.Add(value);
            }
            switch (u.Kind)
            {
                case UpgradeKind.FlatDamage:
                {
                    Add("basic shot", Int(m.ScaleDamage(m.Weapon.ShotDamage(c.BasicShotDamage + s.FlatFor(null), false)))); // the fifth review: her pistols' parts, as PlayerCharacter deals it
                    SkillDefinition heavy = Heaviest(c);
                    if (heavy != null) Add(heavy.Name, Int(m.ScaleDamage(heavy.Damage + s.FlatFor(heavy))));
                    break;
                }
                case UpgradeKind.AttackSpeed:
                {
                    // The fifth review (work order 077): her pistols' parts first (a barrel's longer recovery), then her attack
                    // speed, as PlayerCharacter.StatsRecovery reckons it. The gains of spent focus (work order 071) last a window
                    // only and are not on the card.
                    float interval = s.BasicShotInterval(f.BasicShotStartup, f.BasicShotActive,
                                                         m.Weapon.Recovery(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery));
                    Add("", One(interval > 0f ? 1.0 / interval : 0.0), "shots a second");
                    break;
                }
                case UpgradeKind.CritChance: Add("crit chance", Pct(s.CritChance)); break;
                case UpgradeKind.CritDamage: Add("a crit deals", Plus(s.CritDamage)); break;
                case UpgradeKind.Luck:
                {
                    StatValues v = c.Run?.Stats ?? m.StatValues ?? new StatValues();
                    Add("a chest's rare or better", Pct(1.0 - UpgradeTiers.Shares(v.ChestOdds, s.Luck, v)[0]));
                    break;
                }
                case UpgradeKind.MaxHealth:
                {
                    int max = m.MaxHealthFor(c.OwnMaxHealth);
                    Add("maximum health", Int(max));
                    Add("health now", Int(taken ? Math.Min(max, c.Health + Math.Max(0, u.Heal)) : c.Health));
                    break;
                }
                case UpgradeKind.Mend:
                    Add("health", Int(taken ? Math.Min(m.MaxHealthFor(c.OwnMaxHealth), c.Health + Math.Max(0, u.Heal)) : c.Health));
                    break;
                case UpgradeKind.RunSpeed:
                    Add("run speed", One(f.RunSpeed * s.MoveSpeed), "m a second");
                    Add("while you shoot", One(f.RunSpeed * s.MoveSpeed * m.Weapon.MovingShare(f.MovingShotSpeedShare)), "m a second"); // the fifth review: a grip's share
                    break;
                case UpgradeKind.Cooldown:
                {
                    SkillDefinition skill = c.Skill(u.Skill);
                    Add((u.Skill ?? "") + " cooldown", Sec(skill != null ? skill.Cooldown * s.CooldownShare(skill.Name) : 0f));
                    break;
                }
                case UpgradeKind.AllCooldowns:
                {
                    Add("every skill's cooldown", Minus(s.AllCooldowns));
                    SkillDefinition longest = Longest(c);
                    if (longest != null) Add(longest.Name + " cooldown", Sec(longest.Cooldown * s.CooldownShare(longest.Name)));
                    break;
                }
                case UpgradeKind.LongerBarrel: Add("reach", Metres(m.Weapon.Reach(f.BasicShotRange) + s.Reach)); break; // the fifth review: a sight's metres, as PlayerCharacter.BasicShotRange
                case UpgradeKind.HeavyHits: Add("stagger dealt", Pct(s.Stagger)); break;
                case UpgradeKind.DashCooldown: Add("dash cooldown", Sec(f.DashCooldown * s.DashCooldownShare)); break;
                case UpgradeKind.LastingMarks: Add("marks last", Sec((c.Kit ?? new KitValues()).MarkLifetime + m.MarkLifetimeBonus)); break;
                case UpgradeKind.KeenFocus: Add("focus gained", Pct(m.FocusGainShare)); break;
                case UpgradeKind.Damage:
                    Add("damage dealt", Pct(s.DamageShare));
                    Add("basic shot", Int(m.ScaleDamage(m.Weapon.ShotDamage(c.BasicShotDamage + s.FlatFor(null), false)))); // the fifth review: her pistols' parts
                    break;
                case UpgradeKind.CleanPress: Add("clean and perfect hits", Plus(s.CleanPress - 1f)); break;
                case UpgradeKind.MarkedPrey: Add("hits on a marked enemy", Plus(s.MarkedPrey - 1f)); break;
                case UpgradeKind.Mending:
                {
                    int max = m.MaxHealthFor(c.OwnMaxHealth);
                    RunValues run = c.Run ?? new RunValues();
                    Add("a cleared room heals", Int(FloorHeals.Amount(max, run.RoomHeal) + s.Mending));
                    Add("the elite room heals", Int(FloorHeals.Amount(max, run.EliteRoomHeal) + s.Mending));
                    break;
                }
                case UpgradeKind.LongFocus:
                {
                    FocusModeValues fv = c.FocusValues ?? new FocusModeValues();
                    if (c.FocusMode == FocusMode.HairTrigger)
                    {
                        Add("a part primes for", Sec(fv.PrimeSeconds * s.LongFocus));
                        if (HairTriggerGains(c)) Add("a part powers you up for", Sec(FocusGains.PartSeconds(fv, s.LongFocus))); // work order 071
                    }
                    else Add("focus window", Sec((c.FocusMode == FocusMode.SteadyHand ? fv.WindowSeconds : (c.Identity ?? new IdentityValues()).EmpoweredDuration) * s.LongFocus));
                    break;
                }
                case UpgradeKind.SecondDash: Add("dashes held", Int(s.DashCharges)); break;
                case UpgradeKind.WideWindow: WideWindowNumbers(c, s, Add); break; // work order 067
                case UpgradeKind.Blast: BlastNumbers(c, s, Add); break;
                default: // FirstMark
                {
                    SkillDefinition skill = c.Skill(u.Skill);
                    int marks = (skill != null ? skill.MarksAdded : 1) + m.FirstMarkBonus(u.Skill) + m.Meta.FirstMarkExtra;
                    Add("marks on an unmarked enemy", Int(marks));
                    break;
                }
            }
        }

        /// <summary>The most lines a card of work order 067's two names her skills on (the order: "at most three lines").</summary>
        public const int MostSkillLines = 3;

        /// <summary>Seconds to the hundredth, for the parts of a window: "0.20 s".</summary>
        static string Sec2(double seconds) => seconds.ToString("0.00", s_Inv) + " s";

        /// <summary>
        /// Wide window's numbers (work order 067): for each skill she has with a clean part (Backstep, Triple Tap) its width, and
        /// with a perfect band (Long Shot) its width, as her stats give them now, at most <see cref="MostSkillLines"/>; with none
        /// of them the stat itself. The clean part as the kit places it in the skill's window (<see cref="CleanPart.Of(KitValues, float, bool, float)"/>),
        /// the band as the layer lays it on the skill before her stats (<see cref="SkillDefinition.BeforeStats"/>): what the judge reads.
        /// </summary>
        static void WideWindowNumbers(OfferContext c, HerStats s, Action<string, string, string> add)
        {
            int lines = 0;
            KitValues kit = c.Kit ?? new KitValues();
            foreach (SkillDefinition skill in c.Equipped ?? Array.Empty<SkillDefinition>())
            {
                if (skill == null || lines >= MostSkillLines) continue;
                if (WideWindow.HasCleanPart(skill))
                    add(skill.Name + " clean part", Sec2(WideWindow.CleanPartOf(skill, kit, s.WideWindow).Width), "");
                else if (WideWindow.HasBand(skill) && !skill.NoCharge)
                {
                    SkillDefinition band = s.SkillLayer.Apply(skill.BeforeStats);
                    add(skill.Name + " perfect band", Sec2(band.PerfectTo - band.PerfectFrom), "");
                }
                else continue;
                lines++;
            }
            if (lines == 0) add("clean parts and perfect bands", Plus(s.WideWindow), "");
        }

        /// <summary>
        /// Blast's numbers (work order 067): for each area skill she has, its one area number as her stats make it now (a
        /// burst's radius, a cone's angle, a line's width), at most <see cref="MostSkillLines"/>; with none of them the stat itself.
        /// </summary>
        static void BlastNumbers(OfferContext c, HerStats s, Action<string, string, string> add)
        {
            int lines = 0;
            SkillStatLayer layer = s.SkillLayer;
            IReadOnlyList<string> areas = layer.AreaSkills ?? (c.Modifiers.StatValues ?? new StatValues()).BlastSkills; // the layer's own list
            foreach (SkillDefinition skill in c.Equipped ?? Array.Empty<SkillDefinition>())
            {
                if (skill == null || lines >= MostSkillLines || !Blast.IsArea(skill, areas)) continue;
                SkillDefinition now = layer.Apply(skill.BeforeStats);
                float n = Blast.Of(now);
                add(skill.Name + " " + Blast.Word(now), now.Shape == SkillShape.Cone ? n.ToString("0", s_Inv) + "°" : Metres(n), "");
                lines++;
            }
            if (lines == 0) add("area skills", Plus(s.Blast), "");
        }

        /// <summary>
        /// The skill as she would have it with these upgrades taken (work order 067): the upgrades on the skill before her
        /// stats' layer, then the layer as her stats give it now, as the run's view makes it. With no layer, the old way: the
        /// skill as she has it, a copy, patched.
        /// </summary>
        static SkillDefinition WithRows(SkillDefinition now, IEnumerable<SkillUpgradeDefinition> rows, OfferContext c)
        {
            if (now == null) return null;
            SkillDefinition after = now.BeforeStats.Copy();
            after.SetBeforeStats(null);
            foreach (SkillUpgradeDefinition u in rows ?? Array.Empty<SkillUpgradeDefinition>()) u?.ApplyTo(after);
            return c.Stats.SkillLayer.Apply(after);
        }

        /// <summary>The skill she has that her flat damage counts most in (the first of the heaviest), or null.</summary>
        static SkillDefinition Heaviest(OfferContext c)
        {
            SkillDefinition best = null;
            float most = 0f;
            foreach (SkillDefinition s in c.Equipped ?? Array.Empty<SkillDefinition>())
            {
                if (s == null || s.Damage <= 0) continue;
                float w = FlatDamage.WeightOf(s);
                if (w > most + 1e-6f)
                {
                    most = w;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>The skill she has with the longest cooldown (the first of them), or null.</summary>
        static SkillDefinition Longest(OfferContext c)
        {
            SkillDefinition best = null;
            foreach (SkillDefinition s in c.Equipped ?? Array.Empty<SkillDefinition>())
                if (s != null && (best == null || s.Cooldown > best.Cooldown + 1e-6f)) best = s;
            return best;
        }

        // ---------------- A floor's reward: a skill, with its tier (work order 057) ----------------

        /// <summary>
        /// What each skill is for and how it is pressed, in one sentence (tap, hold and release, charge, three taps); from each
        /// skill's own line of <see cref="SkillDefaults.Line"/>, shortened. By name; a skill not here keeps its own line.
        /// </summary>
        public static string SkillSentence(SkillDefinition s)
        {
            switch (s != null ? s.Name : "")
            {
                case "Quick Shot": return "Tap: a fast pistol shot down a line that marks what it hits.";
                case "Powder Keg": return "Tap to throw a keg that bursts after a fuse; shoot it to burst it at once.";
                case "Fan Fire": return "Hold to spray a cone of shots that marks what it hits.";
                case "Long Shot": return "Hold to charge, let go to fire; let go in the band for a perfect shot.";
                case "Scatter Blast": return "Tap: a shotgun cone that spends every mark it hits for damage.";
                case "Heavy Volley": return "Tap: a long cast, then a wide, heavy line that spends marks.";
                case "Backstep": return "Tap to hop back, tap again to shoot; a clean second tap spends the marks.";
                case "Triple Tap": return "Three taps, a shot each; each marks, and a clean third tap marks twice.";
                case "Tag Round": return "Tap: a light shot that tags the first enemy hit; a tagged enemy takes more.";
                case "Breaker Slug": return "Tap: a short, wide slug that spends marks to break an enemy's guard.";
                case "Slide": return "Tap to slide the way you move, shooting as you go.";
                case "Stopping Shot": return "Tap as an enemy winds up a blue attack, from its front, to daze it.";
                default: return s != null ? SkillDefaults.Line(s.Name) : "";
            }
        }

        /// <summary>
        /// The card of a skill offered as a floor's reward: its name, its tier's word and what the tier adds (057's words), its
        /// key, cooldown and marks on the line under the name, its sentence, and its numbers: for a rare or an epic before and
        /// after its tier, for a common what it deals; an epic's change with its sentence, and the change's cost in the red.
        /// <paramref name="skill"/> is the skill as offered, <paramref name="key"/> the key it will go on.
        /// </summary>
        public static OfferCard ForSkill(SkillDefinition skill, SkillTierOffer tier, string key, OfferContext c)
        {
            c = c ?? new OfferContext();
            bool tiered = tier != null && tier.Tiered;
            // Work order 067: the tier's rows on the skill before her stats' layer, then the layer (an epic's Slug, then Blast), as the view makes it.
            SkillDefinition shown = tiered ? WithRows(skill, tier.Rows, c) : skill;
            var card = new OfferCard
            {
                Name = skill != null ? skill.Name : "", Tiered = tiered, Tier = tiered ? tier.Tier : UpgradeTier.Common,
                TierLine = tiered ? SkillTiers.CardTierLine(tier) : "",
            };
            if (skill == null) return card;
            card.For = "key " + (string.IsNullOrEmpty(key) ? "?" : key) + Dot + "cooldown " + Sec(shown.Cooldown * c.Stats.CooldownShare(skill.Name))
                       + Dot + SkillDefaults.MarkRole(skill);
            card.Sentence = SkillSentence(skill);
            if (tiered) card.Numbers.AddRange(SkillDiff(skill, shown, c));
            else
            {
                string facts = Facts(skill, c);
                if (facts.Length > 0) card.Numbers.Add(facts);
            }
            if (tiered && tier.Change != null)
            {
                card.Notes.Add("+ " + tier.Change.Name.ToUpperInvariant() + ": " + UpgradeSentence(tier.Change));
                card.Cost = UpgradeCost(tier.Change);
            }
            return card;
        }

        /// <summary>A common skill's numbers, what a hit of it deals as she would have it: "damage 100  ·  stagger 3"; a skill with no damage its charges.</summary>
        static string Facts(SkillDefinition s, OfferContext c)
        {
            var parts = new List<string>();
            if (s.Damage > 0) parts.Add("damage " + Int(Hit(s, c)));
            if (s.Stagger > 0f) parts.Add("stagger " + (s.Stagger * c.Stats.Stagger).ToString("0.#", s_Inv));
            if (s.Charges > 1) parts.Add(s.Charges.ToString(s_Inv) + " charges");
            if (s.Damage <= 0 && s.MoveDistance != 0f) parts.Add((s.Input == SkillInput.KeyMove ? "slide " : "hop ") + Metres(Math.Abs(s.MoveDistance)));
            return string.Join(Dot, parts);
        }

        /// <summary>A hit of the skill as she would deal it, before a crit or a mark: its damage and her flat damage, times her share.</summary>
        static int Hit(SkillDefinition s, OfferContext c) => c.Modifiers.ScaleDamage(s.Damage + c.Stats.FlatFor(s));

        // ---------------- The elite room's skill upgrades (work order 017) ----------------

        /// <summary>
        /// The card of an elite room's skill upgrade: its name, the skill it is for with its key and whether it changes how the
        /// skill plays, its sentence, the skill's numbers before and after, and its cost in the red when it has one.
        /// <paramref name="now"/> is the skill as she has it now.
        /// </summary>
        public static OfferCard ForSkillUpgrade(SkillUpgradeDefinition u, SkillDefinition now, string key, OfferContext c)
        {
            c = c ?? new OfferContext();
            var card = new OfferCard { Name = u != null ? u.Name : "" };
            if (u == null) return card;
            card.For = u.Skill + Dot + "key " + (string.IsNullOrEmpty(key) ? "-" : key) + Dot + (u.Kind == SkillUpgradeKind.Change ? "changes how it plays" : "stronger");
            card.Sentence = UpgradeSentence(u);
            if (now != null)
            {
                SkillDefinition after = WithRows(now, new[] { u }, c); // work order 067: the upgrade first, then her stats' layer
                card.Numbers.AddRange(SkillDiff(now, after, c));
            }
            card.Cost = UpgradeCost(u);
            return card;
        }

        /// <summary>An elite room's upgrade (or an epic's change) in one sentence, its gain alone; its cost is <see cref="UpgradeCost"/>.</summary>
        public static string UpgradeSentence(SkillUpgradeDefinition u)
        {
            switch (u != null ? u.FullName : "")
            {
                case "Quick Shot: Double Tap": return "Holds two charges: fire it twice in a row.";
                case "Quick Shot: Quick Draw": return "Every basic shot that hits brings it back sooner.";
                case "Triple Tap: Fourth Tap": return "Four taps instead of three; the clean tap is the fourth.";
                case "Triple Tap: Tied Trigger": return "Every tap that hits brings Scatter Blast back sooner.";
                case "Scatter Blast: Slug": return "A narrow line that reaches far, instead of a short cone.";
                case "Scatter Blast: Point Blank": return "Up close it gets twice the bonus for every mark.";
                case "Backstep: Forward Step": return "The hop goes toward the cursor instead of away from it.";
                case "Backstep: Long Step": return "A longer hop, and more time for the shot after it.";
                case "Quick Shot: Heavy Round": return "Each shot hits much harder and staggers twice as much.";
                case "Triple Tap: Sharp Taps": return "Each tap deals more.";
                case "Scatter Blast: Fresh Shells": return "If it kills, it comes back in half the time.";
                case "Backstep: Second Step": return "Holds two charges: hop twice in a row.";
                case "Long Shot: Snap Shot": return "No charging: it fires at once, always as a perfect release.";
                case "Powder Keg: Twin Kegs": return "Holds two charges: two kegs down at once.";
                case "Fan Fire: Walking Fire": return "You keep your full speed while you hold it.";
                case "Heavy Volley: Quick Cast": return "Its cast is much shorter.";
                case "Tag Round: Spreading Tag": return "When a tagged enemy dies, the tag jumps to the nearest one.";
                case "Breaker Slug: Follow Through": return "If it dazes, it comes back in half the time.";
                case "Slide: Slide Shot": return "Basic shots that hit while you slide each add a mark.";
                case "Stopping Shot: Riposte": return "A counter makes Scatter Blast ready at once.";
                case "Long Shot: Overcharge": return "A perfect release deals three times the damage.";
                case "Powder Keg: Big Keg": return "A wider burst that hits harder.";
                case "Fan Fire: Marking Fire": return "It marks twice as often.";
                case "Heavy Volley: Execution": return "Twice the damage to a dazed enemy.";
                case "Tag Round: Deep Tag": return "A tagged enemy takes even more from you.";
                case "Slide: Third Wind": return "Holds three charges.";
                case "Stopping Shot: Wide Counter": return "The counter works from any side of the enemy.";
                default: return u != null ? u.Line : "";
            }
        }

        /// <summary>What an upgrade takes away, said on its own line in the red; "" for one with no cost.</summary>
        public static string UpgradeCost(SkillUpgradeDefinition u)
        {
            switch (u != null ? u.FullName : "")
            {
                case "Long Shot: Snap Shot": return "Its cooldown is 6 s longer.";
                case "Heavy Volley: Quick Cast": return "It deals 20 % less.";
                case "Long Shot: Overcharge": return "The perfect band is half as wide.";
                case "Scatter Blast: Point Blank": return "Farther than 2.5 m: no bonus, and the marks stay.";
                case "Scatter Blast: Slug": return "No more cone: it hits only what is on its line.";
                default: return "";
            }
        }

        /// <summary>
        /// The skill's numbers that differ between <paramref name="before"/> and <paramref name="after"/>, a line each, in a
        /// fixed order: what a hit deals as she would deal it, stagger, charges, presses, cooldown, the cast, reach, the shape,
        /// a burst's radius, the hop, the window, and what each of the elite room's changes switches on.
        /// </summary>
        public static List<string> SkillDiff(SkillDefinition before, SkillDefinition after, OfferContext c)
        {
            c = c ?? new OfferContext();
            var lines = new List<string>();
            if (before == null || after == null) return lines;
            void Diff(string label, string a, string b, string unit = "")
            {
                if (a != b) lines.Add(Line(label, a, b, unit));
            }
            Diff("damage", Int(Hit(before, c)), Int(Hit(after, c)));
            Diff("stagger", (before.Stagger * c.Stats.Stagger).ToString("0.#", s_Inv), (after.Stagger * c.Stats.Stagger).ToString("0.#", s_Inv));
            Diff("charges", before.Charges.ToString(s_Inv), after.Charges.ToString(s_Inv));
            Diff("taps", before.RecastCount.ToString(s_Inv), after.RecastCount.ToString(s_Inv));
            Diff("cooldown", Sec(before.Cooldown * c.Stats.CooldownShare(before.Name)), Sec(after.Cooldown * c.Stats.CooldownShare(after.Name)));
            if (before.NoCharge != after.NoCharge) Diff("charge time", before.NoCharge ? "none" : Sec(before.ChargeTime), after.NoCharge ? "none" : Sec(after.ChargeTime));
            Diff(after.Input == SkillInput.Casting ? "cast" : "startup", Sec(before.Startup), Sec(after.Startup));
            Diff("reach", Metres(before.Range), Metres(after.Range));
            Diff("area", Area(before), Area(after));
            if (before.Shape == SkillShape.Circle || after.Shape == SkillShape.Circle) Diff("burst radius", Metres(before.Radius), Metres(after.Radius));
            Diff(after.Input == SkillInput.KeyMove ? "slide" : "hop", Hop(before), Hop(after));
            Diff("time for the shot", Sec(before.RecastWindow), Sec(after.RecastWindow));
            Diff("marks", MarkEvery(before), MarkEvery(after));
            Diff("perfect release", Times(before.PerfectScale), Times(after.PerfectScale));
            Diff("perfect band", Sec(Math.Max(0f, before.PerfectTo - before.PerfectFrom)), Sec(Math.Max(0f, after.PerfectTo - after.PerfectFrom)));
            Diff("to a dazed enemy", Times(before.DazedDamageScale), Times(after.DazedDamageScale));
            Diff("a tagged enemy takes", Plus(before.TagDamageShare), Plus(after.TagDamageShare));
            Diff("the tag jumps", before.TagJump > 0f ? "up to " + Metres(before.TagJump) : "no", after.TagJump > 0f ? "up to " + Metres(after.TagJump) : "no");
            Diff("counters from", Front(before.CounterHalfAngle), Front(after.CounterHalfAngle));
            Diff("a basic hit cuts its cooldown by", Sec(before.BasicHitCooldownCut), Sec(after.BasicHitCooldownCut));
            Diff("a hit cuts " + (string.IsNullOrEmpty(after.HitCutsSkill) ? "its own" : after.HitCutsSkill + "'s") + " cooldown by", Sec(before.HitCooldownCut), Sec(after.HitCooldownCut));
            if (before.KillCooldownCut != after.KillCooldownCut) Diff("cooldown after a kill", Sec(After(before, before.KillCooldownCut, c)), Sec(After(after, after.KillCooldownCut, c)));
            if (before.DazeCooldownCut != after.DazeCooldownCut) Diff("cooldown after a daze", Sec(After(before, before.DazeCooldownCut, c)), Sec(After(after, after.DazeCooldownCut, c)));
            if (after.CloseRange > 0f || before.CloseRange > 0f)
                Diff("bonus per mark within " + Metres(Math.Max(before.CloseRange, after.CloseRange)), Pct(CloseBonus(before)), Pct(CloseBonus(after)));
            Diff("marks from a basic hit while sliding", before.SlideShotMarks.ToString(s_Inv), after.SlideShotMarks.ToString(s_Inv));
            if (before.Lock != after.Lock) Diff("while you use it", before.Lock == SkillLock.Light ? "full speed" : "slowed", after.Lock == SkillLock.Light ? "full speed" : "slowed");
            if (before.CounterReadies != after.CounterReadies)
                Diff("after a counter, " + (string.IsNullOrEmpty(after.CounterReadies) ? before.CounterReadies : after.CounterReadies),
                     string.IsNullOrEmpty(before.CounterReadies) ? "waits" : "ready", string.IsNullOrEmpty(after.CounterReadies) ? "waits" : "ready");
            return lines;
        }

        static string Area(SkillDefinition s) =>
            s.Shape == SkillShape.Cone ? "a cone " + s.Angle.ToString("0", s_Inv) + "°"
            : s.Shape == SkillShape.Line ? "a line " + Metres(2f * s.HalfWidth) + " wide"
            : "a circle";

        static string Hop(SkillDefinition s) =>
            s.MoveDistance == 0f ? "none" : Metres(Math.Abs(s.MoveDistance)) + (s.Input == SkillInput.KeyMove ? "" : s.MoveDistance < 0f ? " back" : " forward");

        static string MarkEvery(SkillDefinition s) => s.MarkInterval > 0f ? "every " + Sec(s.MarkInterval) : "every hit";

        static string Front(float halfAngle) => halfAngle >= 179.5f ? "every side" : "a " + (2f * halfAngle).ToString("0", s_Inv) + "° front";

        static float After(SkillDefinition s, float cut, OfferContext c) => s.Cooldown * c.Stats.CooldownShare(s.Name) * (1f - Math.Max(0f, Math.Min(1f, cut)));

        static float CloseBonus(SkillDefinition s) => s.MarkDamageBonus * (s.CloseRange > 0f ? s.CloseMarkScale : 1f);

        // ---------------- The focus choice (work order 025) ----------------

        /// <summary>
        /// The card of a focus choice: its name, its sentence, and its numbers against the gauge she has until she picks
        /// (today's), with what it takes away in the red.
        /// </summary>
        public static OfferCard ForFocus(FocusMode mode, OfferContext c)
        {
            c = c ?? new OfferContext();
            FocusModeValues v = c.FocusValues ?? new FocusModeValues();
            IdentityValues id = c.Identity ?? new IdentityValues();
            float longer = c.Stats.LongFocus;
            var card = new OfferCard { Name = FocusModeRules.Name(mode) };
            if (mode == FocusMode.HairTrigger)
            {
                card.Sentence = string.Format(s_Inv, "Z spends one of {0} parts: cooldowns shorten and your next skill is timed right.", Math.Max(1, v.Parts));
                card.Numbers.Add(Line("Z spends", "the full gauge", "one part of " + Math.Max(1, v.Parts).ToString(s_Inv)));
                card.Numbers.Add(Line("cooldowns cut by", Pct(id.CooldownCut), Pct(v.PartCooldownCut)));
                card.Numbers.Add(Line("next skill primed", "no", "for " + Sec(v.PrimeSeconds * longer)));
                // Work order 071: the gains come for a part's seconds instead of the window's.
                if (HairTriggerGains(c)) card.Numbers.Add(Line("powered up for", Sec(id.EmpoweredDuration * longer), Sec(FocusGains.PartSeconds(v, longer)) + " a part"));
                card.Cost = "No window in which every basic shot marks.";
            }
            else if (mode == FocusMode.SteadyHand)
            {
                card.Sentence = "Z spends the full gauge on a longer window in which two skills change shape.";
                card.Numbers.Add(Line("window", Sec(id.EmpoweredDuration * longer), Sec(v.WindowSeconds * longer)));
                SkillDefinition taps = c.Skill(v.MoreTapsSkill) ?? SkillDefaults.Create().FirstOrDefault(s => s.Name == v.MoreTapsSkill);
                if (taps != null) card.Numbers.Add(Line(v.MoreTapsSkill + " in it", taps.RecastCount.ToString(s_Inv), (taps.RecastCount + v.MoreTaps).ToString(s_Inv), "taps"));
                SkillDefinition twice = c.Skill(v.TwiceSkill) ?? SkillDefaults.Create().FirstOrDefault(s => s.Name == v.TwiceSkill);
                if (twice != null) card.Numbers.Add(Line(v.TwiceSkill + " in it", Math.Max(1, twice.HitCount).ToString(s_Inv), (Math.Max(1, twice.HitCount) * v.TwiceTimes).ToString(s_Inv), "blasts"));
            }
            else
            {
                // Work order 071: today's gauge powers her up for its window.
                card.Sentence = FocusGains.Any(id) ? "A full gauge: Z cuts cooldowns and powers you up while basic shots mark."
                                                   : "A full gauge: Z cuts every running cooldown, and basic shots mark for a while.";
                if (FocusGains.Any(id)) card.Numbers.Add(FocusGains.Words(id));
            }
            return card;
        }

        /// <summary>Work order 071: Hair Trigger's parts give the gains (a part's seconds above 0 and a gain set).</summary>
        static bool HairTriggerGains(OfferContext c) =>
            FocusGains.Any(c?.Identity ?? new IdentityValues()) && (c?.FocusValues ?? new FocusModeValues()).PartBoostSeconds > 0f;
    }

    /// <summary>
    /// Where an offer's cards stand (work order 065): side by side in the middle band of the view, the row centred, as wide as
    /// three of <see cref="OfferValues.CardWidth"/> with their gaps, scaled down when the row with its lean would take more than
    /// <see cref="OfferValues.MostRowShare"/> of the view's width. Layout pixels, 0,0 at the top left.
    /// </summary>
    public static class OfferLayout
    {
        /// <summary>The row's width, its lean included, for cards <paramref name="height"/> high at scale <paramref name="scale"/>.</summary>
        public static float RowWidth(int count, float height, float scale, OfferValues v) =>
            count <= 0 ? 0f : (count * v.CardWidth + (count - 1) * v.CardGap) * scale + v.Slant * height;

        /// <summary>The scale the row takes so that it fits: 1, or less when it would be wider than its share of the view.</summary>
        public static float Scale(int count, float height, float viewWidth, OfferValues v)
        {
            if (count <= 0) return 1f;
            float most = Math.Max(1f, v.MostRowShare * viewWidth);
            float at1 = RowWidth(count, height, 1f, v);
            if (at1 <= most) return 1f;
            float cards = (count * v.CardWidth + (count - 1) * v.CardGap);
            return Math.Max(0.1f, (most - v.Slant * height) / Math.Max(1f, cards));
        }

        /// <summary>Card <paramref name="index"/> of <paramref name="count"/>: centred side by side, the row's middle at its share of the view's height.</summary>
        public static UiBox Card(int index, int count, float height, float scale, float viewWidth, float viewHeight, OfferValues v)
        {
            float w = v.CardWidth * scale, gap = v.CardGap * scale;
            float total = count * w + Math.Max(0, count - 1) * gap;
            float left = (viewWidth - total) * 0.5f;
            float top = viewHeight * v.RowMiddle - height * 0.5f;
            return new UiBox(left + index * (w + gap), top, w, height);
        }

        /// <summary>The width a card's text is measured and drawn at: inside its pads, and the room for a part's lean.</summary>
        public static float TextWidth(float scale, OfferValues v) => Math.Max(10f, (v.CardWidth - 2f * v.Pad - v.LeanRoom) * scale);

        /// <summary>A size of the pop-up at its scale, never under 8 pixels.</summary>
        public static int Size(int size, float scale) => Math.Max(8, (int)Math.Round(size * scale, MidpointRounding.AwayFromZero));

        /// <summary>
        /// True when a point is on a card as it is seen: its rectangle leaning about its middle line by <paramref name="slant"/>
        /// (work order 055's rule, at the pop-up's own slant).
        /// </summary>
        public static bool Hit(UiBox r, float px, float py, float slant)
        {
            float x = px - slant * (r.CenterY - py);
            return x >= r.X && x < r.Right && py >= r.Y && py < r.Bottom;
        }
    }
}
