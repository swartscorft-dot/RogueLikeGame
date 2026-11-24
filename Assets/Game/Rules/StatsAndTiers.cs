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

        // Work order 065 (2026-11-21, Kristian: "og mere stats"): the caps of the six new kinds, read only with the switch on.
        // First guesses of the order (Cooldowns, Clean press) and of the producer's message of the same day (the other four).
        /// <summary>Every skill's cooldown at most this much shorter from the Cooldowns steps (20 %), and never under the floor.</summary>
        public float MostAllCooldowns = 0.20f;
        /// <summary>A clean press and a perfect release at most this much more (+50 %).</summary>
        public float MostCleanPress = 0.50f;
        /// <summary>Her hits on a marked enemy at most this much more (+30 %).</summary>
        public float MostMarkedPrey = 0.30f;
        /// <summary>At most this much more health from a cleared room (+15).</summary>
        public int MostMending = 15;
        /// <summary>The focus window at most this much longer (+50 %).</summary>
        public float MostLongFocus = 0.50f;
        /// <summary>At most this many more dashes held (1: a second dash; the row is also taken once a run at most).</summary>
        public int MostSecondDash = 1;

        // Work order 067 (his choice of 2026-11-21 for the evening, "Wide window og Blast"): the caps of the two, read only with
        // the switch on. First guesses of the order.
        /// <summary>The clean parts and perfect bands at most this much wider (+50 %): Backstep's 0.20 s clean part 0.30 s at most.</summary>
        public float MostWideWindow = 0.50f;
        /// <summary>Her area skills' area at most this much larger (+40 %): Powder Keg's burst 2.5 m, 3.5 m at most.</summary>
        public float MostBlast = 0.40f;
        /// <summary>
        /// The skills Blast grows, by name (work order 067: the order's list): the burst (Powder Keg), the cones (Fan Fire,
        /// Scatter Blast, and its line with Slug) and the wide lines (Heavy Volley, Breaker Slug). The thin single shots, the
        /// hop, the slide and the counter have no area.
        /// </summary>
        public string[] BlastSkills = { "Powder Keg", "Fan Fire", "Scatter Blast", "Heavy Volley", "Breaker Slug" };
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
        public float CritChance => On ? Clamp01(V.StartCritChance + _m.Total(UpgradeKind.CritChance) + _m.Meta.RankCritChance) : 0f; // work order 099: and the ranks' points
        /// <summary>How much more a crit deals (0.5: +50 %); 0 off, where nothing crits.</summary>
        public float CritDamage => On ? Math.Max(0f, V.StartCritDamage + _m.Total(UpgradeKind.CritDamage)) : 0f;
        /// <summary>Luck: leans every tiered offer upward (<see cref="UpgradeTiers.Shares"/>). 0 off.</summary>
        public float Luck => On ? _m.Total(UpgradeKind.Luck) : 0f;

        // ---------------- Work order 065's six (2026-11-21, "og mere stats"): each capped, each nothing with the switch off ----------------

        /// <summary>The share every skill's cooldown is cut by beside its own cuts (Cooldowns), to its cap; 0 off.</summary>
        public float AllCooldowns => On ? Math.Min(Math.Max(0f, V.MostAllCooldowns), Math.Max(0f, _m.Total(UpgradeKind.AllCooldowns))) : 0f;
        /// <summary>A clean press's and a perfect release's damage times this (Clean press): 1 plus the steps, to the cap; 1 off.</summary>
        public float CleanPress => On ? 1f + Math.Min(Math.Max(0f, V.MostCleanPress), Math.Max(0f, _m.Total(UpgradeKind.CleanPress))) : 1f;
        /// <summary>Her hits on an enemy that carries marks times this (Marked prey); 1 off.</summary>
        public float MarkedPrey => On ? 1f + Math.Min(Math.Max(0f, V.MostMarkedPrey), Math.Max(0f, _m.Total(UpgradeKind.MarkedPrey))) : 1f;
        /// <summary>Health more from every cleared combat room (Mending), to its cap; 0 off.</summary>
        public int Mending => On ? Math.Min(Math.Max(0, V.MostMending), (int)Math.Round(Math.Max(0f, _m.Total(UpgradeKind.Mending)), MidpointRounding.AwayFromZero)) : 0;
        /// <summary>The focus window's length (and Hair Trigger's priming) times this (Long focus); 1 off.</summary>
        public float LongFocus => On ? 1f + Math.Min(Math.Max(0f, V.MostLongFocus), Math.Max(0f, _m.Total(UpgradeKind.LongFocus))) : 1f;
        /// <summary>Dashes held more than the rack gives (Second dash); 0 off.</summary>
        public int ExtraDashes => On ? Math.Min(Math.Max(0, V.MostSecondDash), (int)Math.Round(Math.Max(0f, _m.Total(UpgradeKind.SecondDash)), MidpointRounding.AwayFromZero)) : 0;
        /// <summary>The dashes she holds: the rack's (Spare Step) and the second dash's.</summary>
        public int DashCharges => Math.Max(1, _m.Meta.DashCharges) + ExtraDashes;

        // ---------------- Work order 067's two (his choice of 2026-11-21: "Wide window og Blast"): each capped, each nothing with the switch off ----------------

        /// <summary>The share clean parts and perfect bands are wider by (Wide window, <see cref="Rules.WideWindow"/>), to its cap; 0 off.</summary>
        public float WideWindow => On ? Math.Min(Math.Max(0f, V.MostWideWindow), Math.Max(0f, _m.Total(UpgradeKind.WideWindow))) : 0f;
        /// <summary>The share her area skills' area is larger by (Blast, <see cref="Rules.Blast"/>), to its cap; 0 off.</summary>
        public float Blast => On ? Math.Min(Math.Max(0f, V.MostBlast), Math.Max(0f, _m.Total(UpgradeKind.Blast))) : 0f;
        /// <summary>The two as the layer laid on her skills as she has them (<see cref="SkillStatLayer"/>); none off.</summary>
        public SkillStatLayer SkillLayer => On ? new SkillStatLayer(Blast, WideWindow, V.BlastSkills) : default;

        // ---------------- The stats that were there before, as before ----------------

        /// <summary>The share of the feel's run speed she runs at: 1 plus each step's share.</summary>
        public float MoveSpeed => 1f + _m.Total(UpgradeKind.RunSpeed);
        /// <summary>
        /// Her maximum in the run from her own (work order 029): raised by the rack's share and the ranks' (work order 099,
        /// added to it, as two of the rack's pieces add), then the chests' health added.
        /// </summary>
        public int MaxHealthFor(int own) =>
            (StartHealthShare != 0f ? (int)Math.Round(own * (1.0 + StartHealthShare), MidpointRounding.AwayFromZero) : own) + BonusMaxHealth;
        /// <summary>The share of her own health she starts a run with more: the rack's and the ranks' (work order 099), added.</summary>
        float StartHealthShare => _m.Meta.MaxHealthShare + _m.Meta.RankHealthShare;
        public int BonusMaxHealth => (int)Math.Round(_m.Total(UpgradeKind.MaxHealth));
        /// <summary>The share of damage dealt: 1 plus each damage share's step (an epic one with the switch on), times the rack's step and the ranks' (work order 099, added to the rack's).</summary>
        public float DamageShare => (1f + _m.Total(UpgradeKind.Damage)) * (1f + _m.Meta.DamageShare + _m.Meta.RankDamageShare);
        /// <summary>
        /// The share of her maximum a cleared room heals (work order 030's <see cref="RunValues.RoomHeal"/>, the elite room's
        /// <see cref="RunValues.EliteRoomHeal"/>), and since work order 099 the ranks' points added to it; Mending's health is after.
        /// </summary>
        public float RoomHealShare(RunValues run, bool elite) =>
            (run == null ? 0f : elite ? run.EliteRoomHeal : run.RoomHeal) + _m.Meta.RankRoomHeal;
        /// <summary>
        /// The share of a skill's own cooldown it runs: 1 less each cut to it, and since work order 065 less every skill's
        /// cut (<see cref="AllCooldowns"/>, 0 off), never under the floor.
        /// </summary>
        public float CooldownShare(string skill) => Math.Max(_m.CooldownFloor, 1f - _m.Total(UpgradeKind.Cooldown, skill ?? "") - AllCooldowns);
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
            int startHealth = StartHealthShare != 0f ? (int)Math.Round(ownMaxHealth * (1.0 + StartHealthShare), MidpointRounding.AwayFromZero) : ownMaxHealth;
            var lines = new List<string>
            {
                "Damage  +" + Num(Damage) + " a shot, skills by weight  (+0)",
                "Attack speed  " + Num(rate, "0.0") + " shots/s  (" + Num(startRate, "0.0") + ")",
                "Crit chance  " + Pct(CritChance) + "  (" + Pct(On ? Clamp01(V.StartCritChance + _m.Meta.RankCritChance) : 0f) + ")",
                "Crit damage  +" + Pct(CritDamage) + "  (+" + Pct(On ? V.StartCritDamage : 0f) + ")",
                "Luck  " + Num(Luck) + "  (0)",
                "Move speed  " + Pct(MoveSpeed) + "  (100 %)",
                "Max health  " + MaxHealthFor(ownMaxHealth).ToString(CultureInfo.InvariantCulture) + "  (" + startHealth.ToString(CultureInfo.InvariantCulture) + ")",
                "Cooldowns  " + CooldownWords() + "  (none)",
                "Reach  " + Num(baseReach + Reach) + " m  (" + Num(baseReach) + " m)",
                "Stagger  " + Pct(Stagger) + "  (100 %)",
                "Damage dealt  +" + Pct(DamageShare - 1f) + "  (+" + Pct(_m.Meta.DamageShare + _m.Meta.RankDamageShare) + ")",
            };
            AddNewLines(lines);
            return lines;
        }

        /// <summary>Work order 065: the six new stats' lines, after those of work order 050 (Cooldowns' is in its line above).</summary>
        void AddNewLines(List<string> lines)
        {
            lines.Add("Clean press  +" + Pct(CleanPress - 1f) + "  (+0 %)");
            lines.Add("Marked prey  +" + Pct(MarkedPrey - 1f) + "  (+0 %)");
            lines.Add("Mending  +" + Mending.ToString(CultureInfo.InvariantCulture) + " a room  (+0)");
            lines.Add("Focus time  +" + Pct(LongFocus - 1f) + "  (+0 %)");
            lines.Add("Dashes  " + DashCharges.ToString(CultureInfo.InvariantCulture) + "  (" + Math.Max(1, _m.Meta.DashCharges).ToString(CultureInfo.InvariantCulture) + ")");
            // Work order 067: after 065's.
            lines.Add("Wide window  +" + Pct(WideWindow) + "  (+0 %)");
            lines.Add("Blast  +" + Pct(Blast) + "  (+0 %)");
        }

        string CooldownWords()
        {
            var parts = new List<string>();
            if (AllCooldowns > 0f) parts.Add("every skill -" + Pct(AllCooldowns)); // work order 065
            foreach (string skill in _m.Taken.Where(u => u.Kind == UpgradeKind.Cooldown).Select(u => u.Skill).Distinct())
                parts.Add(skill + " -" + Pct(1f - CooldownShare(skill)));
            float dash = _m.Total(UpgradeKind.DashCooldown);
            if (dash > 0f) parts.Add("dash -" + Pct(1f - Math.Max(_m.CooldownFloor, 1f - dash)));
            return parts.Count == 0 ? "none" : string.Join(", ", parts);
        }

        /// <summary>Her stats in one short line, for the play session log and its summary; work order 065's six at its end.</summary>
        public string OneLine(float baseInterval, int ownMaxHealth) =>
            string.Format(CultureInfo.InvariantCulture,
                          "dmg +{0:0.#}, attack {1:0.00}/s, crit {2:0.#}% x{3:0.##}, luck {4:0.#}, speed {5:0.#}%, hp {6}, reach +{7:0.#} m, stagger {8:0.#}%, dmg dealt +{9:0.#}%",
                          Damage, baseInterval > 0f ? AttackSpeed / baseInterval : 0f, CritChance * 100f, 1f + CritDamage, Luck, MoveSpeed * 100f,
                          MaxHealthFor(ownMaxHealth), Reach, Stagger * 100f, (DamageShare - 1f) * 100f)
            + string.Format(CultureInfo.InvariantCulture, ", cd all -{0:0.#}%, clean +{1:0.#}%, marked +{2:0.#}%, room heal +{3}, focus time +{4:0.#}%, dashes {5}",
                            AllCooldowns * 100f, (CleanPress - 1f) * 100f, (MarkedPrey - 1f) * 100f, Mending, (LongFocus - 1f) * 100f, DashCharges)
            + string.Format(CultureInfo.InvariantCulture, ", wide window +{0:0.#}%, blast +{1:0.#}%", WideWindow * 100f, Blast * 100f); // work order 067
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
                // Work order 065: every skill's cooldown is the cooldown stat, as one skill's and the dash's are.
                case UpgradeKind.AllCooldowns: return "cooldown";
                case UpgradeKind.CleanPress: return "clean press";
                case UpgradeKind.MarkedPrey: return "marked prey";
                case UpgradeKind.Mending: return "mending";
                case UpgradeKind.LongFocus: return "focus time";
                case UpgradeKind.SecondDash: return "second dash";
                case UpgradeKind.WideWindow: return "wide window"; // work order 067: each a stat of its own
                case UpgradeKind.Blast: return "blast";
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
            int count = values.TieredUpgradesOffered + Math.Max(0, extra); // work order 065: three (UpgradesOffered, two, is the plain chest's)
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
            float amount = StepOf(row, tier, v);
            // A heal grows with its row's step: by the tier's scale, or for a row with steps of its own by the step's share.
            float healScale = row.Amount > 0f && (row.RareAmount > 0f || row.EpicAmount > 0f) ? amount / row.Amount : scale;
            return new Upgrade
            {
                Kind = row.Kind, Amount = amount, Heal = (int)Math.Round(row.Heal * healScale, MidpointRounding.AwayFromZero),
                Skill = skill, Tier = tier, Tiered = true,
            };
        }

        /// <summary>
        /// A row's step at a tier: an epic-only row's own amount; a rare's or an epic's own step where the row has one (work order
        /// 065's new kinds); else the common step times the tier's scale, as work order 050 made it.
        /// </summary>
        public static float StepOf(UpgradeDefinition row, UpgradeTier tier, StatValues v)
        {
            if (row == null) return 0f;
            if (row.MinTier == UpgradeTier.Epic) return row.Amount;
            if (tier == UpgradeTier.Epic && row.EpicAmount > 0f) return row.EpicAmount;
            if (tier == UpgradeTier.Rare && row.RareAmount > 0f) return row.RareAmount;
            return row.Amount * UpgradeTiers.Scale(tier, v ?? new StatValues());
        }

        /// <summary>
        /// The skills she has whose cooldown is not yet at its floor (since work order 065 with every skill's cut counted, to
        /// its cap, whatever the run's switch).
        /// </summary>
        static List<string> Cuttable(IReadOnlyList<string> equipped, RunValues values, RunModifiers taken)
        {
            var list = new List<string>();
            if (equipped == null) return list;
            float all = AllCut(values, taken);
            foreach (string s in equipped)
                if (1f - (taken != null ? taken.Total(UpgradeKind.Cooldown, s) : 0f) - all > values.CooldownFloor + 1e-5f) list.Add(s);
            return list;
        }

        /// <summary>Every skill's cut taken (work order 065), to its cap.</summary>
        static float AllCut(RunValues values, RunModifiers taken) =>
            taken == null ? 0f : Math.Min((values.Stats ?? new StatValues()).MostAllCooldowns, Math.Max(0f, taken.Total(UpgradeKind.AllCooldowns)));

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
                case UpgradeKind.CritChance: return v.StartCritChance + Total(UpgradeKind.CritChance) + (taken != null ? taken.Meta.RankCritChance : 0f) < 1f - 1e-5f; // work order 099: the ranks' points too
                case UpgradeKind.LongerBarrel: return Total(UpgradeKind.LongerBarrel) < v.MostReach - 1e-5f;
                case UpgradeKind.Luck: return UpgradeTiers.Shares(v.ChestOdds, Total(UpgradeKind.Luck), v)[0] > 1e-6;
                case UpgradeKind.DashCooldown: return 1f - Total(UpgradeKind.DashCooldown) > values.CooldownFloor + 1e-5f;
                case UpgradeKind.Cooldown:
                    return (d.Skill ?? "").Length > 0
                        ? 1f - (taken != null ? taken.Total(UpgradeKind.Cooldown, d.Skill) : 0f) - AllCut(values, taken) > values.CooldownFloor + 1e-5f
                        : Cuttable(equipped, values, taken).Count > 0;
                case UpgradeKind.FirstMark: return equipped == null || equipped.Contains(d.Skill);
                // Work order 065: a new kind at its cap would do nothing; every skill's cut also not when every skill is at the floor.
                case UpgradeKind.AllCooldowns: return Total(UpgradeKind.AllCooldowns) < v.MostAllCooldowns - 1e-5f && (equipped == null || Cuttable(equipped, values, taken).Count > 0);
                case UpgradeKind.CleanPress: return Total(UpgradeKind.CleanPress) < v.MostCleanPress - 1e-5f;
                case UpgradeKind.MarkedPrey: return Total(UpgradeKind.MarkedPrey) < v.MostMarkedPrey - 1e-5f;
                case UpgradeKind.Mending: return Total(UpgradeKind.Mending) < v.MostMending - 1e-5f;
                case UpgradeKind.LongFocus: return Total(UpgradeKind.LongFocus) < v.MostLongFocus - 1e-5f;
                case UpgradeKind.SecondDash: return Total(UpgradeKind.SecondDash) < v.MostSecondDash - 1e-5f;
                // Work order 067: never offered at its cap.
                case UpgradeKind.WideWindow: return Total(UpgradeKind.WideWindow) < v.MostWideWindow - 1e-5f;
                case UpgradeKind.Blast: return Total(UpgradeKind.Blast) < v.MostBlast - 1e-5f;
                default: return true;
            }
        }
    }
}
