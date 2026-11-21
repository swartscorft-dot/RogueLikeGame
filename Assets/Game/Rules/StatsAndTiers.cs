using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// How large a step a chest offers (decision 0012, 2026-11-20, Kristian: "we have no tiers on upgrades"; work order
    /// 050): common a small step, rare about twice it, epic about three times it or a percentage.
    /// </summary>
    public enum UpgradeTier { Common, Rare, Epic }

    /// <summary>
    /// The numbers of her stats in a run and of the tiers (decision 0012, the producer's proposal; work order 050), all
    /// first guesses, with their defaults in code: no asset holds them.
    /// </summary>
    [Serializable]
    public sealed class StatValues
    {
        /// <summary>
        /// The basic shot's rate she starts a run at, as a share of the rate the feel's numbers give (Kristian: "start a
        /// little slower than we do from the start"): 0.85, about 15 % slower. Each attack speed step adds its share of it.
        /// </summary>
        public float StartAttackSpeed = 0.85f;
        /// <summary>
        /// The cap, as a share of the feel's rate: 1.3, one shot every 0.173 s with the feel's 0.225 s. The shot's startup
        /// (0.08 s, the moment the Shoot clip's firing pose is lined up on) never changes; only the recovery after it is
        /// cut, and at the cap it is still 0.073 s, longer than the clip's 0.05 s fade in, so each shot's pose, kick and
        /// sound still show apart from the next.
        /// </summary>
        public float MostAttackSpeed = 1.3f;
        public float StartCritChance = 0.05f;  // the chance that a hit she deals is a crit
        public float StartCritDamage = 0.5f;   // a crit deals this much more
        /// <summary>The most the basic shot's reach grows in a run (m): today's Longer barrel, +1 m at most three times.</summary>
        public float MostReach = 3f;

        public float RareScale = 2f;           // a rare step is this many common steps
        public float EpicScale = 3f;           // an epic one this many, unless its row is an epic of its own (damage +15 %)
        /// <summary>A chest's odds of a common, a rare and an epic step (75 / 22 / 3).</summary>
        public float[] ChestOdds = { 75f, 22f, 3f };
        /// <summary>
        /// A floor's or the elite room's reward, where one offers a plain stat step (0 / 70 / 30). None does today: they
        /// offer skills, skill upgrades and the focus choice, which have no tiers in this order.
        /// </summary>
        public float[] RewardOdds = { 0f, 70f, 30f };
        /// <summary>Each point of luck moves this share of the common odds to rare, and the next to epic (6 % and 2 %).</summary>
        public float LuckToRare = 0.06f;
        public float LuckToEpic = 0.02f;
    }

    /// <summary>The tiers (work order 050): their odds as luck leans them, a draw, their words and colours.</summary>
    public static class UpgradeTiers
    {
        public static readonly UpgradeTier[] All = { UpgradeTier.Common, UpgradeTier.Rare, UpgradeTier.Epic };

        public static string Word(UpgradeTier tier) => tier == UpgradeTier.Epic ? "epic" : tier == UpgradeTier.Rare ? "rare" : "common";

        /// <summary>The card's edge: white common, teal rare (the HUD's accent), gold epic (the elite's). Red, green, blue, 0 to 1.</summary>
        public static float[] Rgb(UpgradeTier tier) =>
            tier == UpgradeTier.Epic ? new[] { 1f, 0.78f, 0.2f } : tier == UpgradeTier.Rare ? new[] { 0.25f, 0.85f, 0.85f } : new[] { 1f, 1f, 1f };

        /// <summary>How many common steps a step of this tier is.</summary>
        public static float Scale(UpgradeTier tier, StatValues v) =>
            tier == UpgradeTier.Epic ? v.EpicScale : tier == UpgradeTier.Rare ? v.RareScale : 1f;

        /// <summary>
        /// The shares of common, rare and epic (summing to 1) from the odds given and her luck: each point moves
        /// <see cref="StatValues.LuckToRare"/> of the common weight to rare and <see cref="StatValues.LuckToEpic"/> to epic,
        /// never more than all of it.
        /// </summary>
        public static double[] Shares(float[] odds, float luck, StatValues v)
        {
            double c = odds != null && odds.Length > 0 ? Math.Max(0f, odds[0]) : 1.0;
            double r = odds != null && odds.Length > 1 ? Math.Max(0f, odds[1]) : 0.0;
            double e = odds != null && odds.Length > 2 ? Math.Max(0f, odds[2]) : 0.0;
            double toRare = Math.Max(0f, luck) * Math.Max(0f, v.LuckToRare), toEpic = Math.Max(0f, luck) * Math.Max(0f, v.LuckToEpic);
            double moved = toRare + toEpic;
            if (moved > 1.0)
            {
                toRare /= moved;
                toEpic /= moved;
            }
            double total = c + r + e;
            if (total <= 0.0) return new[] { 1.0, 0.0, 0.0 };
            return new[] { c * (1.0 - toRare - toEpic) / total, (r + c * toRare) / total, (e + c * toEpic) / total };
        }

        /// <summary>One tier drawn from the shares.</summary>
        public static UpgradeTier Draw(LevelRandom rng, double[] shares)
        {
            double x = (rng.NextBits() >> 11) * (1.0 / (1UL << 53));
            if (x < shares[0]) return UpgradeTier.Common;
            return x < shares[0] + shares[1] ? UpgradeTier.Rare : UpgradeTier.Epic;
        }
    }

    /// <summary>
    /// The flat damage a skill adds (decision 0012: "a skill adds it times its own weight, a heavy skill counts it more
    /// than a quick one"; work order 050). The weight is <see cref="SkillDefinition.FlatDamageWeight"/> when it is set (0
    /// or more); the settings asset holds none, so by default it is the skill's by name: a quick single shot 1, each shot
    /// of a multi-shot skill (and each tick of a hold) 0.5, a heavy or charged skill 2, a skill that deals no damage 0;
    /// a skill not named here by its kind, the same way. The basic shot counts it once.
    /// </summary>
    public static class FlatDamage
    {
        public static float WeightOf(SkillDefinition skill)
        {
            if (skill == null) return 1f; // the basic shot
            if (skill.FlatDamageWeight >= 0f) return skill.FlatDamageWeight;
            switch (skill.Name)
            {
                case "Quick Shot": case "Backstep": case "Tag Round": case "Stopping Shot": return 1f;
                case "Triple Tap": case "Fan Fire": return 0.5f;
                case "Scatter Blast": case "Long Shot": case "Heavy Volley": case "Powder Keg": case "Breaker Slug": return 2f;
                case "Slide": return 0f;
            }
            if (skill.Damage <= 0) return 0f;
            if (skill.Input == SkillInput.Holding || skill.HitCount > 1 || (skill.RecastCount > 1 && !skill.OpeningMove)) return 0.5f;
            if (skill.Input == SkillInput.Charge || skill.Input == SkillInput.Casting || skill.Damage >= 150) return 2f;
            return 1f;
        }

        /// <summary>What a hit of this skill (null: the basic shot) adds with this much flat damage: rounded, half away from zero.</summary>
        public static int For(SkillDefinition skill, float flat) =>
            flat <= 0f ? 0 : (int)Math.Round(flat * WeightOf(skill), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Crits (decision 0012, work order 050). A hit that crits deals <see cref="StatValues.StartCritDamage"/> and the
    /// steps taken more. A clean press of a recast chain and a perfect charged shot are always a crit: playing well is
    /// the sure way to one. What a clean tap and a perfect release gave before as their own multiplier (1.5 and 2) is
    /// folded into that: their own scale is divided by a crit at her start, so at the start they deal what they dealt,
    /// and a crit damage step raises them as it raises any crit. They are never paid twice, and never roll. A clean press
    /// that spends marks instead (Backstep's clean spend) had no scale of its own to fold: its crit is folded instead
    /// (<see cref="ApplyFolded"/>), so at the start it too deals what it dealt, and a crit damage step raises it (the
    /// fourth review of 2026-11-21, work order 055: it was paid 1.5 on top, 120 for 80).
    /// </summary>
    public static class CritRules
    {
        /// <summary>A roll on the stream: true with the chance (0 never draws, 1 or more always crits without drawing).</summary>
        public static bool Rolls(LevelRandom rng, float chance)
        {
            if (chance <= 0f || rng == null) return false;
            if (chance >= 1f) return true;
            return (rng.NextBits() >> 11) * (1.0 / (1UL << 53)) < chance;
        }

        /// <summary>A crit's damage: <paramref name="damage"/> times 1 plus the crit damage, rounded, half away from zero.</summary>
        public static int Apply(int damage, float critDamage) =>
            damage <= 0 ? damage : (int)Math.Round(damage * (1.0 + Math.Max(0f, critDamage)), MidpointRounding.AwayFromZero);

        /// <summary>
        /// A clean tap's or a perfect release's own scale with the stats on: what it gives beyond the crit it now is (1.5
        /// over 1.5 is 1: a clean tap is a crit and nothing more; 2 over 1.5 leaves a perfect release a third more).
        /// </summary>
        public static float Folded(float scale, StatValues v) => scale / (1f + Math.Max(0f, v.StartCritDamage));

        /// <summary>
        /// A sure crit with nothing of its own folded in (a clean spend, work order 055): what a crit gives beyond one at
        /// her start, so the same damage at the start and more with each crit damage step. Rounded, half away from zero.
        /// </summary>
        public static int ApplyFolded(int damage, float critDamage, StatValues v) =>
            damage <= 0 ? damage
            : (int)Math.Round(damage * (1.0 + Math.Max(0f, critDamage)) / (1.0 + Math.Max(0f, v.StartCritDamage)), MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Her stats in a run (decision 0012, work order 050): one plain class, fed by the run's modifiers (the chests' steps
    /// taken and the rack's pieces between runs) and read by her where each is used. The stats that were there before
    /// (move speed, maximum health, cooldowns, reach, stagger, damage dealt) are here with the same numbers as before; the
    /// new ones (flat damage, attack speed, crit chance and crit damage, luck) only with the switch on
    /// (<see cref="RunModifiers.StatsAndTiers"/>, from <see cref="RunValues.StatsAndTiers"/> when a run starts). Off, every
    /// new one is the number that changes nothing: no flat damage, the feel's rate, no crit, no luck.
    /// </summary>
    public sealed class HerStats
    {
        readonly RunModifiers _m;

        public HerStats(RunModifiers modifiers) => _m = modifiers;

        public bool On => _m.StatsAndTiers;
        StatValues V => _m.StatValues ?? s_Defaults;
        static readonly StatValues s_Defaults = new StatValues();

        // ---------------- The new stats ----------------

        /// <summary>Flat damage added to every basic shot; a skill adds it times its weight (<see cref="FlatDamage"/>).</summary>
        public float Damage => On ? _m.Total(UpgradeKind.FlatDamage) : 0f;
        /// <summary>What a hit of this skill (null: the basic shot) adds now.</summary>
        public int FlatFor(SkillDefinition skill) => FlatDamage.For(skill, Damage);

        /// <summary>The basic shot's rate as a share of the feel's: the start's, raised by the steps, never over the cap. 1 off.</summary>
        public float AttackSpeed => On ? AttackSpeedWith(_m.Total(UpgradeKind.AttackSpeed)) : 1f;
        float AttackSpeedWith(float steps) => Math.Min(Math.Max(0.05f, V.MostAttackSpeed), Math.Max(0.05f, V.StartAttackSpeed) * (1f + Math.Max(0f, steps)));
        /// <summary>True when attack speed is at its cap: a step of it would do nothing.</summary>
        public bool AttackSpeedAtMost => On && AttackSpeed >= V.MostAttackSpeed - 1e-5f;

        /// <summary>
        /// The basic shot's recovery now: the time from shot to shot is the feel's divided by <see cref="AttackSpeed"/>,
        /// and only the recovery takes the difference; the startup and the active part stay as the feel has them. The
        /// feel's recovery off.
        /// </summary>
        public float BasicShotRecovery(float startup, float active, float recovery) =>
            On ? Math.Max(0f, (startup + active + recovery) / AttackSpeed - startup - active) : recovery;

        /// <summary>The time from one basic shot to the next with these feel numbers.</summary>
        public float BasicShotInterval(float startup, float active, float recovery) => startup + active + BasicShotRecovery(startup, active, recovery);

        /// <summary>The chance a hit crits, 0 to 1; 0 off.</summary>
        public float CritChance => On ? Clamp01(V.StartCritChance + _m.Total(UpgradeKind.CritChance)) : 0f;
        /// <summary>How much more a crit deals (0.5: +50 %); 0 off, where nothing crits.</summary>
        public float CritDamage => On ? Math.Max(0f, V.StartCritDamage + _m.Total(UpgradeKind.CritDamage)) : 0f;
        /// <summary>Luck: leans every tiered offer upward (<see cref="UpgradeTiers.Shares"/>). 0 off.</summary>
        public float Luck => On ? _m.Total(UpgradeKind.Luck) : 0f;

        // ---------------- The stats that were there before, as before ----------------

        /// <summary>The share of the feel's run speed she runs at: 1 plus each step's share.</summary>
        public float MoveSpeed => 1f + _m.Total(UpgradeKind.RunSpeed);
        /// <summary>Her maximum in the run from her own (work order 029): raised by the rack's share, then the chests' health added.</summary>
        public int MaxHealthFor(int own) =>
            (_m.Meta.MaxHealthShare != 0f ? (int)Math.Round(own * (1.0 + _m.Meta.MaxHealthShare), MidpointRounding.AwayFromZero) : own) + BonusMaxHealth;
        public int BonusMaxHealth => (int)Math.Round(_m.Total(UpgradeKind.MaxHealth));
        /// <summary>The share of damage dealt: 1 plus each damage share's step (an epic one with the switch on), times the rack's step.</summary>
        public float DamageShare => (1f + _m.Total(UpgradeKind.Damage)) * (1f + _m.Meta.DamageShare);
        /// <summary>The share of a skill's own cooldown it runs: 1 less each cut to it, never under the floor.</summary>
        public float CooldownShare(string skill) => Math.Max(_m.CooldownFloor, 1f - _m.Total(UpgradeKind.Cooldown, skill ?? ""));
        /// <summary>The share of the dash's own cooldown it runs: 1 less each cut, never under the floor; times Spare Step's.</summary>
        public float DashCooldownShare => Math.Max(_m.CooldownFloor, 1f - _m.Total(UpgradeKind.DashCooldown)) * _m.Meta.DashRechargeScale;
        /// <summary>Metres the basic shot reaches farther; with the switch on never more than <see cref="StatValues.MostReach"/>.</summary>
        public float Reach => On ? Math.Min(Math.Max(0f, V.MostReach), _m.Total(UpgradeKind.LongerBarrel)) : _m.Total(UpgradeKind.LongerBarrel);
        /// <summary>The share of stagger everything she does takes off a bar.</summary>
        public float Stagger => 1f + _m.Total(UpgradeKind.HeavyHits);

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        // ---------------- In words ----------------

        static string Pct(double share) => (share * 100.0).ToString("0.#", CultureInfo.InvariantCulture) + " %";
        static string Num(double v, string format = "0.#") => v.ToString(format, CultureInfo.InvariantCulture);

        /// <summary>
        /// Her stats for the pause menu, a line each: the value now and, in brackets, what she started the run with (the
        /// rack's pieces between runs are part of her start). <paramref name="baseInterval"/> is the feel's time from shot
        /// to shot, <paramref name="baseReach"/> its reach, <paramref name="ownMaxHealth"/> her own maximum.
        /// </summary>
        public List<string> Lines(float baseInterval, float baseReach, int ownMaxHealth)
        {
            float startRate = baseInterval > 0f ? (On ? AttackSpeedWith(0f) : 1f) / baseInterval : 0f;
            float rate = baseInterval > 0f ? AttackSpeed / baseInterval : 0f;
            int startHealth = _m.Meta.MaxHealthShare != 0f ? (int)Math.Round(ownMaxHealth * (1.0 + _m.Meta.MaxHealthShare), MidpointRounding.AwayFromZero) : ownMaxHealth;
            var lines = new List<string>
            {
                "Damage  +" + Num(Damage) + " a shot, skills by weight  (+0)",
                "Attack speed  " + Num(rate, "0.0") + " shots/s  (" + Num(startRate, "0.0") + ")",
                "Crit chance  " + Pct(CritChance) + "  (" + Pct(On ? Clamp01(V.StartCritChance) : 0f) + ")",
                "Crit damage  +" + Pct(CritDamage) + "  (+" + Pct(On ? V.StartCritDamage : 0f) + ")",
                "Luck  " + Num(Luck) + "  (0)",
                "Move speed  " + Pct(MoveSpeed) + "  (100 %)",
                "Max health  " + MaxHealthFor(ownMaxHealth).ToString(CultureInfo.InvariantCulture) + "  (" + startHealth.ToString(CultureInfo.InvariantCulture) + ")",
                "Cooldowns  " + CooldownWords() + "  (none)",
                "Reach  " + Num(baseReach + Reach) + " m  (" + Num(baseReach) + " m)",
                "Stagger  " + Pct(Stagger) + "  (100 %)",
                "Damage dealt  +" + Pct(DamageShare - 1f) + "  (+" + Pct(_m.Meta.DamageShare) + ")",
            };
            return lines;
        }

        string CooldownWords()
        {
            var parts = new List<string>();
            foreach (string skill in _m.Taken.Where(u => u.Kind == UpgradeKind.Cooldown).Select(u => u.Skill).Distinct())
                parts.Add(skill + " -" + Pct(1f - CooldownShare(skill)));
            float dash = _m.Total(UpgradeKind.DashCooldown);
            if (dash > 0f) parts.Add("dash -" + Pct(1f - Math.Max(_m.CooldownFloor, 1f - dash)));
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        /// <summary>Her stats in one short line, for the play session log and its summary.</summary>
        public string OneLine(float baseInterval, int ownMaxHealth) =>
            string.Format(CultureInfo.InvariantCulture,
                          "dmg +{0:0.#}, attack {1:0.00}/s, crit {2:0.#}% x{3:0.##}, luck {4:0.#}, speed {5:0.#}%, hp {6}, reach +{7:0.#} m, stagger {8:0.#}%, dmg dealt +{9:0.#}%",
                          Damage, baseInterval > 0f ? AttackSpeed / baseInterval : 0f, CritChance * 100f, 1f + CritDamage, Luck, MoveSpeed * 100f,
                          MaxHealthFor(ownMaxHealth), Reach, Stagger * 100f, (DamageShare - 1f) * 100f);
    }

    /// <summary>
    /// A chest's offer with the stats and tiers on (decision 0012, work order 050), in the place of
    /// <see cref="ChestUpgrades"/>' plain draw. For each card a tier is drawn (<see cref="StatValues.ChestOdds"/>, leaned by
    /// her luck), then a row of <see cref="RunValues.TieredUpgrades"/> that may come at that tier, and its step is the
    /// row's times the tier's scale (a row that is an epic of its own, damage +15 %, as it is). An offer never shows the
    /// same stat twice (flat and share damage are one stat, a skill's and the dash's cooldown another), and never a step
    /// that would do nothing: no Mend at full health, no attack speed at its cap, no crit chance at 100 %, no reach at its
    /// most, no luck once every common is moved, no cooldown cut on a cooldown at its floor, no first mark for a skill she
    /// lacks. A tier with nothing left to offer falls back to common, and then to the tiers above (work order 055). Drawn from the run's seed, the floor and the chest's
    /// place on a stream of its own, so the same run with the same picks offers the same.
    /// </summary>
    public static class TieredOffers
    {
        public const string Stage = "chest tiers";

        /// <summary>The stat a kind steps, for the rule that an offer shows each stat once.</summary>
        public static string StatOf(UpgradeKind kind)
        {
            switch (kind)
            {
                case UpgradeKind.Damage: case UpgradeKind.FlatDamage: return "damage";
                case UpgradeKind.Cooldown: case UpgradeKind.DashCooldown: return "cooldown";
                case UpgradeKind.AttackSpeed: return "attack speed";
                case UpgradeKind.CritChance: return "crit chance";
                case UpgradeKind.CritDamage: return "crit damage";
                case UpgradeKind.Luck: return "luck";
                case UpgradeKind.MaxHealth: return "max health";
                case UpgradeKind.RunSpeed: return "move speed";
                case UpgradeKind.LongerBarrel: return "reach";
                case UpgradeKind.HeavyHits: return "stagger";
                case UpgradeKind.Mend: return "heal";
                case UpgradeKind.LastingMarks: return "mark lifetime";
                case UpgradeKind.KeenFocus: return "focus";
                default: return "first mark";
            }
        }

        public static List<Upgrade> Offer(ulong runSeed, int floor, int chest, IReadOnlyList<string> equippedSkills, RunValues values,
                                          RunModifiers taken, bool atFullHealth, int extra = 0, float[] odds = null)
        {
            StatValues v = values.Stats ?? new StatValues();
            UpgradeDefinition[] table = values.TieredUpgrades ?? Array.Empty<UpgradeDefinition>();
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor * 64 + chest);
            double[] shares = UpgradeTiers.Shares(odds ?? v.ChestOdds, taken != null ? taken.Total(UpgradeKind.Luck) : 0f, v);
            int count = values.UpgradesOffered + Math.Max(0, extra);
            var offer = new List<Upgrade>();
            var shown = new HashSet<string>();
            while (offer.Count < count)
            {
                UpgradeTier tier = UpgradeTiers.Draw(rng, shares);
                Upgrade u = DrawAt(tier, rng, table, shown, equippedSkills, values, v, taken, atFullHealth);
                if (u == null && tier != UpgradeTier.Common) u = DrawAt(UpgradeTier.Common, rng, table, shown, equippedSkills, values, v, taken, atFullHealth);
                // The fourth review of 2026-11-21 (work order 055): then the tiers not yet tried, up, before giving up, so a
                // row that may come only higher is not hidden (a chest could open with no choice).
                for (UpgradeTier t = UpgradeTier.Rare; u == null && t <= UpgradeTier.Epic; t++)
                    if (t != tier) u = DrawAt(t, rng, table, shown, equippedSkills, values, v, taken, atFullHealth);
                if (u == null) break;
                shown.Add(StatOf(u.Kind));
                offer.Add(u);
            }
            return offer;
        }

        static Upgrade DrawAt(UpgradeTier tier, LevelRandom rng, UpgradeDefinition[] table, HashSet<string> shown, IReadOnlyList<string> equipped,
                              RunValues values, StatValues v, RunModifiers taken, bool atFullHealth)
        {
            var open = new List<UpgradeDefinition>();
            foreach (UpgradeDefinition d in table)
                if (d != null && d.MinTier <= tier && !shown.Contains(StatOf(d.Kind)) && Useful(d, equipped, values, v, taken, atFullHealth))
                    open.Add(d);
            if (open.Count == 0) return null;
            UpgradeDefinition row = open[rng.Below(open.Count)];
            string skill = row.Skill ?? "";
            if (row.Kind == UpgradeKind.Cooldown && skill.Length == 0)
            {
                List<string> cuttable = Cuttable(equipped, values, taken);
                skill = cuttable[rng.Below(cuttable.Count)];
            }
            float scale = row.MinTier == UpgradeTier.Epic ? 1f : UpgradeTiers.Scale(tier, v);
            return new Upgrade
            {
                Kind = row.Kind, Amount = row.Amount * scale, Heal = (int)Math.Round(row.Heal * scale, MidpointRounding.AwayFromZero),
                Skill = skill, Tier = tier, Tiered = true,
            };
        }

        /// <summary>The skills she has whose cooldown is not yet at its floor.</summary>
        static List<string> Cuttable(IReadOnlyList<string> equipped, RunValues values, RunModifiers taken)
        {
            var list = new List<string>();
            if (equipped == null) return list;
            foreach (string s in equipped)
                if (1f - (taken != null ? taken.Total(UpgradeKind.Cooldown, s) : 0f) > values.CooldownFloor + 1e-5f) list.Add(s);
            return list;
        }

        /// <summary>True when taking a step of this row now would change something.</summary>
        public static bool Useful(UpgradeDefinition d, IReadOnlyList<string> equipped, RunValues values, StatValues v, RunModifiers taken, bool atFullHealth)
        {
            float Total(UpgradeKind k) => taken != null ? taken.Total(k) : 0f;
            if (d.MaxTaken > 0 && taken != null && taken.CountOf(d.Kind) >= d.MaxTaken) return false;
            switch (d.Kind)
            {
                case UpgradeKind.Mend: return !atFullHealth;
                case UpgradeKind.AttackSpeed:
                    return Math.Max(0.05f, v.StartAttackSpeed) * (1f + Total(UpgradeKind.AttackSpeed)) < v.MostAttackSpeed - 1e-5f;
                case UpgradeKind.CritChance: return v.StartCritChance + Total(UpgradeKind.CritChance) < 1f - 1e-5f;
                case UpgradeKind.LongerBarrel: return Total(UpgradeKind.LongerBarrel) < v.MostReach - 1e-5f;
                case UpgradeKind.Luck: return UpgradeTiers.Shares(v.ChestOdds, Total(UpgradeKind.Luck), v)[0] > 1e-6;
                case UpgradeKind.DashCooldown: return 1f - Total(UpgradeKind.DashCooldown) > values.CooldownFloor + 1e-5f;
                case UpgradeKind.Cooldown:
                    return (d.Skill ?? "").Length > 0
                        ? 1f - (taken != null ? taken.Total(UpgradeKind.Cooldown, d.Skill) : 0f) > values.CooldownFloor + 1e-5f
                        : Cuttable(equipped, values, taken).Count > 0;
                case UpgradeKind.FirstMark: return equipped == null || equipped.Contains(d.Skill);
                default: return true;
            }
        }
    }
}
