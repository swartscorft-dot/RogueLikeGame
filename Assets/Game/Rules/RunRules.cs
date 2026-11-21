using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// A run's seeds (decision 0008, 2026-11-19; work order 012). A run has a seed of its own, shown as eight characters
    /// like a floor's (<see cref="LevelSeed"/>); floor n's seed is made from the run's seed and n, so the same run seed
    /// gives the same five floors, and every floor of a run has another.
    /// </summary>
    public static class RunSeeds
    {
        /// <summary>The seed of floor <paramref name="floor"/> (from 1) of the run with this seed: eight characters too.</summary>
        public static ulong FloorSeed(ulong runSeed, int floor)
        {
            unchecked
            {
                ulong state = LevelRandom.Mix(runSeed ^ LevelRandom.Hash("floor"));
                return LevelRandom.Mix(state + (ulong)(uint)floor * 0x9E3779B97F4A7C15UL) & LevelSeed.ShortMask;
            }
        }
    }

    /// <summary>
    /// The floor reward (work order 012): after floors 1 to 4 of an act she is offered
    /// <see cref="RunValues.SkillsOffered"/> of the skills she does not have yet (all that are left if fewer), drawn from
    /// the run's seed and the floor's number, so the same run with the same picks offers the same choices.
    /// </summary>
    public static class FloorRewards
    {
        public const string Stage = "floor reward";

        /// <summary>The slots offered, in the order drawn: never one she has.</summary>
        public static List<int> Offer(ulong runSeed, int floor, IReadOnlyList<bool> equipped, int count)
        {
            var lacking = new List<int>();
            for (int slot = 0; slot < equipped.Count; slot++)
                if (!equipped[slot]) lacking.Add(slot);
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor);
            rng.Shuffle(lacking);
            return lacking.Take(Math.Max(0, count)).ToList();
        }
    }

    /// <summary>The kinds of chest upgrade (work order 012): small steps that can be felt and stack over a run.</summary>
    public enum UpgradeKind
    {
        /// <summary>Maximum health up by Amount, and heals <see cref="UpgradeDefinition.Heal"/>.</summary>
        MaxHealth,
        /// <summary>Damage dealt up by the share Amount.</summary>
        Damage,
        /// <summary>One named skill's cooldown down by the share Amount: a skill she has, drawn when offered.</summary>
        Cooldown,
        /// <summary>Run speed up by the share Amount.</summary>
        RunSpeed,
        /// <summary>A named skill's hit on a target with no marks adds Amount more marks.</summary>
        FirstMark,
        /// <summary>
        /// The dash's cooldown down by the share Amount, stacking (work order 017, from Kristian's ruling on the dash, 2026-11-20:
        /// "it stays scarce, but can be upgraded during the run"). Never under <see cref="RunValues.CooldownFloor"/> of its own.
        /// </summary>
        DashCooldown,
        // Work order 025 (2026-11-20, Kristian: "more to choose in a run"): five more steps, each felt, none changing how she plays.
        /// <summary>Heals Heal now; her maximum stays. Never offered at full health.</summary>
        Mend,
        /// <summary>The basic shot reaches Amount m farther. At most <see cref="UpgradeDefinition.MaxTaken"/> in a run.</summary>
        LongerBarrel,
        /// <summary>Marks stay Amount s longer before they fall off.</summary>
        LastingMarks,
        /// <summary>Focus fills by the share Amount faster, from everything that fills it.</summary>
        KeenFocus,
        /// <summary>Everything she does takes the share Amount more off a stagger bar.</summary>
        HeavyHits,
        // Work order 050 (decision 0012, Kristian's words of 2026-11-20: "flat damage from chests", "attack speed", "crit/crit
        // dmg", "luck"): offered only with RunValues.StatsAndTiers on, read through HerStats.
        /// <summary>Amount damage added to every basic shot; a skill adds it times its weight (<see cref="Rules.FlatDamage"/>).</summary>
        FlatDamage,
        /// <summary>The basic shot's rate up by the share Amount of her start's, to the cap. Not the skills' cooldowns, nor any window.</summary>
        AttackSpeed,
        /// <summary>The chance a hit crits up by Amount.</summary>
        CritChance,
        /// <summary>A crit deals the share Amount more.</summary>
        CritDamage,
        /// <summary>Amount points of luck: every tiered offer leans upward.</summary>
        Luck,
        // Work order 065 (2026-11-21, Kristian: "og mere stats"; the producer's message of the same day): six kinds no row had,
        // offered only with RunValues.StatsAndTiers on, read through HerStats, each capped there.
        /// <summary>Every skill's cooldown down by the share Amount, beside a skill's own cuts; never under the cooldown floor.</summary>
        AllCooldowns,
        /// <summary>A clean press and a perfect release deal the share Amount more, on whatever their own bonus is.</summary>
        CleanPress,
        /// <summary>Her hits on an enemy that carries marks deal the share Amount more.</summary>
        MarkedPrey,
        /// <summary>Amount health more from every cleared combat room, on top of the room's own heal.</summary>
        Mending,
        /// <summary>The focus window (and Hair Trigger's priming) lasts the share Amount longer.</summary>
        LongFocus,
        /// <summary>Amount more dashes held before the dash must recharge (an epic only, once a run).</summary>
        SecondDash,
    }

    /// <summary>One row of the chest table: a kind, how much, and for some a skill.</summary>
    [Serializable]
    public sealed class UpgradeDefinition
    {
        public UpgradeKind Kind;
        public float Amount;
        public int Heal;
        /// <summary>The skill it names; empty for Cooldown means one she has, drawn when offered.</summary>
        public string Skill = "";
        /// <summary>Above 0: offered no more once a run has taken it this many times (work order 025: Longer barrel, 3).</summary>
        public int MaxTaken;
        /// <summary>
        /// Work order 050, the tiered table only: the lowest tier the row may come at. Epic: an epic of its own, offered as
        /// it is and never scaled (damage +15 %, decision 0012: "5 % can be saved for somewhat stronger things").
        /// </summary>
        public UpgradeTier MinTier;
        /// <summary>
        /// Work order 065, the tiered table only: a rare's and an epic's step of their own (above 0), where a row's tiers are not
        /// twice and three times its common step (the new kinds: Cooldowns 3 / 5 / 8 %). 0: the tier's scale, as before.
        /// </summary>
        public float RareAmount, EpicAmount;

        public UpgradeDefinition() { }

        public UpgradeDefinition(UpgradeKind kind, float amount, int heal = 0, string skill = "", int maxTaken = 0, UpgradeTier minTier = UpgradeTier.Common,
                                 float rare = 0f, float epic = 0f)
        {
            Kind = kind;
            Amount = amount;
            Heal = heal;
            Skill = skill;
            MaxTaken = maxTaken;
            MinTier = minTier;
            RareAmount = rare;
            EpicAmount = epic;
        }
    }

    /// <summary>One upgrade as offered and taken: a row of the table with its skill filled in.</summary>
    public sealed class Upgrade
    {
        public UpgradeKind Kind;
        public float Amount;
        public int Heal;
        public string Skill = "";
        /// <summary>Work order 050: its tier, and whether it came from the tiered offer (off: false, and nothing shows a tier).</summary>
        public UpgradeTier Tier;
        public bool Tiered;

        /// <summary>"common", "rare" or "epic"; "" for an upgrade of the plain offer.</summary>
        public string TierWord => Tiered ? UpgradeTiers.Word(Tier) : "";
        /// <summary>The stat it steps (<see cref="TieredOffers.StatOf"/>).</summary>
        public string Stat => TieredOffers.StatOf(Kind);

        static string Pct(float share) => (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        static string Int(float v) => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);
        static string Num(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>What it does, in one line of plain words, for the choice on screen.</summary>
        public string Text =>
            Kind == UpgradeKind.MaxHealth ? "+" + Int(Amount) + " maximum health, and heal " + Heal.ToString(CultureInfo.InvariantCulture)
            : Kind == UpgradeKind.Damage ? "+" + Pct(Amount) + " damage dealt"
            : Kind == UpgradeKind.Cooldown ? "-" + Pct(Amount) + " cooldown on " + Skill
            : Kind == UpgradeKind.RunSpeed ? "+" + Pct(Amount) + " run speed"
            : Kind == UpgradeKind.DashCooldown ? "-" + Pct(Amount) + " dash cooldown"
            // Work order 025.
            : Kind == UpgradeKind.Mend ? "Mend: heal " + Heal.ToString(CultureInfo.InvariantCulture) + " now (your maximum stays)"
            : Kind == UpgradeKind.LongerBarrel ? "Longer barrel: the basic shot reaches " + Num(Amount) + " m farther"
            : Kind == UpgradeKind.LastingMarks ? "Lasting marks: marks stay " + Num(Amount) + " s longer"
            : Kind == UpgradeKind.KeenFocus ? "Keen focus: focus fills " + Pct(Amount) + " faster"
            : Kind == UpgradeKind.HeavyHits ? "Heavy hits: " + Pct(Amount) + " more off a stagger bar"
            // Work order 050.
            : Kind == UpgradeKind.FlatDamage ? "+" + Num(Amount) + " damage to every shot (skills by their weight)"
            : Kind == UpgradeKind.AttackSpeed ? "+" + Pct(Amount) + " attack speed (the basic shot)"
            : Kind == UpgradeKind.CritChance ? "+" + Pct(Amount) + " crit chance"
            : Kind == UpgradeKind.CritDamage ? "+" + Pct(Amount) + " crit damage"
            : Kind == UpgradeKind.Luck ? "+" + Num(Amount) + " luck: rarer upgrades come more often"
            : NewText ?? "+" + Int(Amount) + " mark from " + Skill + "'s hit on a target with none";

        /// <summary>Work order 065: the six new kinds' line; null for the others.</summary>
        string NewText =>
            Kind == UpgradeKind.AllCooldowns ? "-" + Pct(Amount) + " cooldown on every skill"
            : Kind == UpgradeKind.CleanPress ? "+" + Pct(Amount) + " damage on a clean press or a perfect release"
            : Kind == UpgradeKind.MarkedPrey ? "+" + Pct(Amount) + " damage to an enemy that carries marks"
            : Kind == UpgradeKind.Mending ? "+" + Int(Amount) + " health from every cleared room"
            : Kind == UpgradeKind.LongFocus ? "+" + Pct(Amount) + " longer focus window"
            : Kind == UpgradeKind.SecondDash ? "+" + Int(Amount) + " dash held"
            : null;

        /// <summary>Work order 065: the six new kinds' short form; null for the others.</summary>
        string NewShort =>
            Kind == UpgradeKind.AllCooldowns ? "-" + Pct(Amount) + " cd all"
            : Kind == UpgradeKind.CleanPress ? "+" + Pct(Amount) + " clean"
            : Kind == UpgradeKind.MarkedPrey ? "+" + Pct(Amount) + " vs marked"
            : Kind == UpgradeKind.Mending ? "+" + Int(Amount) + " room heal"
            : Kind == UpgradeKind.LongFocus ? "+" + Pct(Amount) + " focus time"
            : Kind == UpgradeKind.SecondDash ? "+" + Int(Amount) + " dash"
            : null;

        /// <summary>The short form for the box on screen and the log.</summary>
        public string Short =>
            Kind == UpgradeKind.MaxHealth ? "+" + Int(Amount) + " hp"
            : Kind == UpgradeKind.Damage ? "+" + Pct(Amount) + " dmg"
            : Kind == UpgradeKind.Cooldown ? "-" + Pct(Amount) + " cd " + Skill
            : Kind == UpgradeKind.RunSpeed ? "+" + Pct(Amount) + " speed"
            : Kind == UpgradeKind.DashCooldown ? "-" + Pct(Amount) + " cd dash"
            : Kind == UpgradeKind.Mend ? "heal " + Heal.ToString(CultureInfo.InvariantCulture)
            : Kind == UpgradeKind.LongerBarrel ? "+" + Num(Amount) + " m reach"
            : Kind == UpgradeKind.LastingMarks ? "+" + Num(Amount) + " s marks"
            : Kind == UpgradeKind.KeenFocus ? "+" + Pct(Amount) + " focus"
            : Kind == UpgradeKind.HeavyHits ? "+" + Pct(Amount) + " stagger"
            : Kind == UpgradeKind.FlatDamage ? "+" + Num(Amount) + " dmg" // work order 050
            : Kind == UpgradeKind.AttackSpeed ? "+" + Pct(Amount) + " atk speed"
            : Kind == UpgradeKind.CritChance ? "+" + Pct(Amount) + " crit"
            : Kind == UpgradeKind.CritDamage ? "+" + Pct(Amount) + " crit dmg"
            : Kind == UpgradeKind.Luck ? "+" + Num(Amount) + " luck"
            : NewShort ?? "+" + Int(Amount) + " first mark " + Skill;

        public override string ToString() => Short;
    }

    /// <summary>
    /// The numbers of a run (decision 0008, work order 012), all first guesses, with their defaults in code: how many
    /// skills a floor reward offers, how near a chest she must stand to open it, how many upgrades a chest offers, and
    /// the table they are drawn from.
    /// </summary>
    [Serializable]
    public sealed class RunValues
    {
        public int SkillsOffered = 3;
        public float ChestReach = 2f;          // m from her to the chest's footprint middle
        public int UpgradesOffered = 2;
        /// <summary>
        /// How many upgrades a chest offers with <see cref="StatsAndTiers"/> on (work order 065, 2026-11-21, Kristian: "jeg syntes
        /// også kisten skal ha flere valg, gjerne 3"): three, of three different stats. Off, <see cref="UpgradesOffered"/> (2), as before.
        /// </summary>
        public int TieredUpgradesOffered = 3;
        /// <summary>What a chest offers now, before Deep Chests' one more: three with the stats on, two off (work order 065).</summary>
        public int ChestOffers => StatsAndTiers ? TieredUpgradesOffered : UpgradesOffered;
        public float CooldownFloor = 0.5f;     // a skill's cooldown never goes under this share of its own
        /// <summary>
        /// A breath between floors (work order 023, 2026-11-20): when a floor's reward is taken (floors 1 to 4) she is
        /// healed by this share of her maximum (<see cref="FloorHeals"/>). None before: only a chest's +10 healed, and the
        /// test pilot's flights of 2026-11-20 saw her health only go down across an act.
        /// </summary>
        public float FloorHeal = 0.35f;
        /// <summary>
        /// Health back inside a floor (work order 030, lever A): when every enemy of a combat room is dead she is healed by
        /// this share of her maximum, once per room (<see cref="RoomHeals"/>). None before: the test pilot's flights of
        /// 2026-11-20 saw a floor-3 room cost the Kiter 60 to 77 of her 100 with nothing back until the floor's end.
        /// Flown (work order 030, round 1): 38 to 66 health back a run, the Kiter's lowest health at a floor's end 80
        /// instead of 15; alone it moved no pilot past floor 3.
        /// </summary>
        public float RoomHeal = 0.08f;
        /// <summary>The elite room's heal instead of <see cref="RoomHeal"/> (work order 030, lever A), besides its upgrade; none before.</summary>
        public float EliteRoomHeal = 0.20f;
        public UpgradeDefinition[] Upgrades = DefaultUpgrades();

        /// <summary>
        /// Her stats and the tiers (decision 0012, Kristian's words of 2026-11-20; work order 050), on by default: she starts
        /// a run with a slower basic shot (<see cref="StatValues.StartAttackSpeed"/>) and a chance to crit, and a chest offers
        /// <see cref="TieredUpgrades"/> by tier (<see cref="TieredOffers"/>). Off, her start, every offer and every number are
        /// as before: the chest draws from <see cref="Upgrades"/>, and nothing new is read.
        /// </summary>
        public bool StatsAndTiers = true;
        /// <summary>The numbers of her stats and of the tiers (work order 050).</summary>
        public StatValues Stats = new StatValues();
        /// <summary>What a chest offers with <see cref="StatsAndTiers"/> on, each row's Amount its common step (work order 050).</summary>
        public UpgradeDefinition[] TieredUpgrades = DefaultTieredUpgrades();

        /// <summary>
        /// Tiers on the floor reward's skills (decision 0013; Kristian's choice of 2026-11-21, "Tiers på ferdigheter"; work
        /// order 057), on by default: each card draws common, rare or epic, and a rare or epic skill is taken with its step
        /// (<see cref="Rules.SkillTiers"/>). Off, the floor reward, its cards, the skills she takes and every number are as
        /// before. Independent of <see cref="StatsAndTiers"/>: with that off her luck is 0 and these still work.
        /// </summary>
        public bool SkillTiers = true;
        /// <summary>The numbers of the skills' tiers (work order 057).</summary>
        public SkillTierValues SkillTierNumbers = new SkillTierValues();

        /// <summary>
        /// The tiered table (work order 050), each at its common step; rare is twice it, epic three times. First guesses
        /// of the order: damage +3, attack speed +6 %, crit chance +4 %, crit damage +15 %, luck +1, maximum health +10
        /// (and heal 10), reach +1 m (three in a run at most, as the Longer barrel was); move speed, cooldown, stagger
        /// and the other rows as today's steps. The damage share of today (+5 %) is no longer a common: it is an epic of
        /// its own, three of today's steps (+15 %).
        /// </summary>
        public static UpgradeDefinition[] DefaultTieredUpgrades() => new[]
        {
            new UpgradeDefinition(UpgradeKind.FlatDamage, 3f),
            new UpgradeDefinition(UpgradeKind.AttackSpeed, 0.06f),
            new UpgradeDefinition(UpgradeKind.CritChance, 0.04f),
            new UpgradeDefinition(UpgradeKind.CritDamage, 0.15f),
            new UpgradeDefinition(UpgradeKind.Luck, 1f),
            new UpgradeDefinition(UpgradeKind.MaxHealth, 10f, heal: 10),
            new UpgradeDefinition(UpgradeKind.RunSpeed, 0.05f),
            new UpgradeDefinition(UpgradeKind.Cooldown, 0.05f),
            new UpgradeDefinition(UpgradeKind.LongerBarrel, 1f),
            new UpgradeDefinition(UpgradeKind.HeavyHits, 0.1f),
            new UpgradeDefinition(UpgradeKind.DashCooldown, 0.08f),
            new UpgradeDefinition(UpgradeKind.Mend, 0f, heal: 30),
            new UpgradeDefinition(UpgradeKind.LastingMarks, 2f),
            new UpgradeDefinition(UpgradeKind.KeenFocus, 0.1f),
            new UpgradeDefinition(UpgradeKind.FirstMark, 1f, skill: "Quick Shot"),
            new UpgradeDefinition(UpgradeKind.Damage, 0.15f, minTier: UpgradeTier.Epic),
            // Work order 065 (Kristian, 2026-11-21: "og mere stats"): six new kinds, each with its own common, rare and epic
            // step (first guesses of the order and of the producer's message of the same day); their caps are in StatValues.
            new UpgradeDefinition(UpgradeKind.AllCooldowns, 0.03f, rare: 0.05f, epic: 0.08f),
            new UpgradeDefinition(UpgradeKind.CleanPress, 0.08f, rare: 0.14f, epic: 0.20f),
            new UpgradeDefinition(UpgradeKind.MarkedPrey, 0.06f, rare: 0.10f, epic: 0.15f),
            new UpgradeDefinition(UpgradeKind.Mending, 3f, rare: 5f, epic: 8f),
            new UpgradeDefinition(UpgradeKind.LongFocus, 0.10f, rare: 0.18f, epic: 0.25f),
            new UpgradeDefinition(UpgradeKind.SecondDash, 1f, maxTaken: 1, minTier: UpgradeTier.Epic), // an epic of its own, once a run
        };

        /// <summary>
        /// The order's examples: +10 maximum health and heal 10; +5 % damage; -5 % cooldown on a skill she has; +5 % run
        /// speed; +1 mark from Quick Shot's first hit. Work order 017: -8 % dash cooldown. Work order 025: Mend (heal 30),
        /// Longer barrel (+1 m, at most three), Lasting marks (+2 s), Keen focus (+10 %), Heavy hits (+10 % stagger).
        /// </summary>
        public static UpgradeDefinition[] DefaultUpgrades() => new[]
        {
            new UpgradeDefinition(UpgradeKind.MaxHealth, 10f, heal: 10),
            new UpgradeDefinition(UpgradeKind.Damage, 0.05f),
            new UpgradeDefinition(UpgradeKind.Cooldown, 0.05f),
            new UpgradeDefinition(UpgradeKind.RunSpeed, 0.05f),
            new UpgradeDefinition(UpgradeKind.FirstMark, 1f, skill: "Quick Shot"),
            new UpgradeDefinition(UpgradeKind.DashCooldown, 0.08f),
            new UpgradeDefinition(UpgradeKind.Mend, 0f, heal: 30),
            new UpgradeDefinition(UpgradeKind.LongerBarrel, 1f, maxTaken: 3),
            new UpgradeDefinition(UpgradeKind.LastingMarks, 2f),
            new UpgradeDefinition(UpgradeKind.KeenFocus, 0.1f),
            new UpgradeDefinition(UpgradeKind.HeavyHits, 0.1f),
        };
    }

    /// <summary>
    /// The heal when a combat room is cleared (work order 030, lever A): <see cref="RunValues.RoomHeal"/> of her maximum,
    /// <see cref="RunValues.EliteRoomHeal"/> for the elite room, once per room of a floor. A new floor starts a new one.
    /// </summary>
    public sealed class RoomHeals
    {
        readonly HashSet<int> _healed = new HashSet<int>();

        /// <summary>What clearing this room heals now: the room's share of her maximum the first time, 0 after (and for a share of 0).</summary>
        public int Heal(int room, bool elite, int maxHealth, RunValues values)
        {
            if (values == null || _healed.Contains(room)) return 0;
            _healed.Add(room);
            return FloorHeals.Amount(maxHealth, elite ? values.EliteRoomHeal : values.RoomHeal);
        }

        public bool Healed(int room) => _healed.Contains(room);

        /// <summary>A new floor: no room healed yet.</summary>
        public void Clear() => _healed.Clear();
    }

    /// <summary>The heal between floors (work order 023): a share of her maximum, rounded to the nearest point; none for a share of 0.</summary>
    public static class FloorHeals
    {
        public static int Amount(int maxHealth, float share) =>
            maxHealth <= 0 || share <= 0f ? 0 : (int)Math.Round(maxHealth * (double)share, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// The chests (work order 012): each offers <see cref="RunValues.UpgradesOffered"/> different rows of the table,
    /// drawn from the run's seed, the floor's number and the chest's place in the floor, so the same run offers the same.
    /// A Cooldown row names one of the skills she has, drawn too. Work order 025: a row the run may not take now is passed
    /// over in the drawn order (Mend at full health, a row taken its most times), so a larger table changes what a given
    /// run seed's chests offer.
    /// </summary>
    public static class ChestUpgrades
    {
        public const string Stage = "chest";

        /// <summary>True when the run may be offered this row now: not Mend at full health, not a row taken its most times.</summary>
        public static bool MayOffer(UpgradeDefinition d, RunModifiers taken, bool atFullHealth)
        {
            if (d == null) return false;
            if (d.Kind == UpgradeKind.Mend && atFullHealth) return false;
            if (d.MaxTaken > 0 && taken != null && taken.CountOf(d.Kind) >= d.MaxTaken) return false;
            return true;
        }

        /// <summary>The offer with nothing taken and her hurt: every row may come up.</summary>
        public static List<Upgrade> Offer(ulong runSeed, int floor, int chest, IReadOnlyList<string> equippedSkills, RunValues values, int extra = 0) =>
            Offer(runSeed, floor, chest, equippedSkills, values, null, false, extra);

        /// <summary>
        /// <paramref name="extra"/> more than the values say (work order 029: Deep Chests, the first chest of a floor); the
        /// first ones drawn are the same either way.
        /// </summary>
        public static List<Upgrade> Offer(ulong runSeed, int floor, int chest, IReadOnlyList<string> equippedSkills, RunValues values,
                                          RunModifiers taken, bool atFullHealth, int extra = 0)
        {
            // Work order 050: with the stats and tiers on, the tiered offer; off, the draw below, as before.
            if (values.StatsAndTiers) return TieredOffers.Offer(runSeed, floor, chest, equippedSkills, values, taken, atFullHealth, extra);
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor * 64 + chest);
            var rows = Enumerable.Range(0, values.Upgrades?.Length ?? 0).ToList();
            rng.Shuffle(rows);
            var offer = new List<Upgrade>();
            foreach (int i in rows)
            {
                if (offer.Count >= values.UpgradesOffered + Math.Max(0, extra)) break;
                UpgradeDefinition d = values.Upgrades[i];
                if (!MayOffer(d, taken, atFullHealth)) continue;
                string skill = d.Skill ?? "";
                if (d.Kind == UpgradeKind.Cooldown && skill.Length == 0)
                {
                    if (equippedSkills == null || equippedSkills.Count == 0) continue;
                    skill = equippedSkills[rng.Below(equippedSkills.Count)];
                }
                offer.Add(new Upgrade { Kind = d.Kind, Amount = d.Amount, Heal = d.Heal, Skill = skill });
            }
            return offer;
        }
    }

    /// <summary>
    /// What a run has taken from its chests (work order 012): a plain list, read where the values are used (damage dealt,
    /// maximum health, a skill's cooldown, run speed, a skill's first mark, and since work order 017 the dash's cooldown;
    /// since work order 025 the basic shot's reach, the marks' lifetime, focus gained and stagger dealt, and a Mend is only
    /// listed: its heal is given when it is taken). It never writes to the settings. Cleared
    /// when a run starts or ends.
    /// </summary>
    public sealed class RunModifiers
    {
        readonly List<Upgrade> _taken = new List<Upgrade>();
        public float CooldownFloor = 0.5f;
        /// <summary>
        /// Work order 050: the stats and tiers switch for this run (set from <see cref="RunValues.StatsAndTiers"/> when a run
        /// starts, as the cooldown floor is) and their numbers. Off, which a bare list is, every number here is as before.
        /// </summary>
        public bool StatsAndTiers;
        public StatValues StatValues = new StatValues();
        /// <summary>Her stats in the run (work order 050): the one class the numbers below are read through.</summary>
        public HerStats Stats { get; }

        public RunModifiers() => Stats = new HerStats(this);

        public IReadOnlyList<Upgrade> Taken => _taken;
        public int Count => _taken.Count;

        public void Take(Upgrade u)
        {
            if (u != null) _taken.Add(u);
        }

        /// <summary>
        /// Takes this very upgrade back off (its last place in the list); false when it was not taken. Work order 065: an offer's
        /// card reads her stats with the card taken and then takes it back, so its "after" is what taking it gives.
        /// </summary>
        public bool TakeBack(Upgrade u)
        {
            for (int i = _taken.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_taken[i], u))
                {
                    _taken.RemoveAt(i);
                    return true;
                }
            return false;
        }

        /// <summary>
        /// The pieces of the rack equipped for this run (work order 029, decision 0009, a proposal he has not ruled on): empty
        /// unless what lasts between runs is switched on, and empty they change none of the numbers here.
        /// </summary>
        public RunMetaEffects Meta { get; } = new RunMetaEffects();

        public void Clear()
        {
            _taken.Clear();
            Meta.Clear();
        }

        float Sum(UpgradeKind kind, string skill = null)
        {
            float s = 0f;
            foreach (Upgrade u in _taken)
                if (u.Kind == kind && (skill == null || u.Skill == skill)) s += u.Amount;
            return s;
        }

        /// <summary>The steps of this kind taken, added up (for one named skill when <paramref name="skill"/> is given). Work order 050.</summary>
        public float Total(UpgradeKind kind, string skill = null) => Sum(kind, skill);

        // Since work order 050 the stats below are read through HerStats (Stats), with the same numbers as before.
        /// <summary>Maximum health added.</summary>
        public int BonusMaxHealth => Stats.BonusMaxHealth;
        /// <summary>Her maximum in the run from her own (work order 029): raised by the rack's share, then the chests' health added.</summary>
        public int MaxHealthFor(int own) => Stats.MaxHealthFor(own);
        /// <summary>The share of damage dealt: 1 plus each damage upgrade's share, times the rack's step (work order 029).</summary>
        public float DamageShare => Stats.DamageShare;
        public int ScaleDamage(int damage) => damage <= 0 ? damage : (int)Math.Round(damage * DamageShare, MidpointRounding.AwayFromZero);
        /// <summary>The share of a skill's own cooldown it runs: 1 less each cut to it, never under <see cref="CooldownFloor"/>.</summary>
        public float CooldownShare(string skill) => Stats.CooldownShare(skill);
        public float RunSpeedShare => Stats.MoveSpeed;
        public int FirstMarkBonus(string skill) => (int)Math.Round(Sum(UpgradeKind.FirstMark, skill ?? ""));
        /// <summary>
        /// The share of the dash's own cooldown it runs (work order 017): 1 less each cut, never under <see cref="CooldownFloor"/>;
        /// times Spare Step's longer recharge (work order 029).
        /// </summary>
        public float DashCooldownShare => Stats.DashCooldownShare;

        // Work order 025 (2026-11-20).
        /// <summary>How many of this kind the run has taken.</summary>
        public int CountOf(UpgradeKind kind) => _taken.Count(u => u.Kind == kind);
        /// <summary>Metres the basic shot reaches farther (Longer barrel); work order 050: never over its most with the stats on.</summary>
        public float BasicShotRangeBonus => Stats.Reach;
        /// <summary>Seconds marks stay longer before they fall off (Lasting marks).</summary>
        public float MarkLifetimeBonus => Sum(UpgradeKind.LastingMarks);
        /// <summary>The share of focus she gains from anything: 1 plus each Keen focus's share.</summary>
        public float FocusGainShare => 1f + Sum(UpgradeKind.KeenFocus);
        /// <summary>The share of stagger everything she does takes off a bar: 1 plus each Heavy hits' share.</summary>
        public float StaggerShare => Stats.Stagger;

        /// <summary>The upgrades taken, in short form, the same ones counted together: "+10 hp x2, +5% dmg".</summary>
        public string ShortText()
        {
            if (_taken.Count == 0) return "none";
            var sb = new StringBuilder();
            foreach (IGrouping<string, Upgrade> g in _taken.GroupBy(u => u.Short))
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(g.Key);
                if (g.Count() > 1) sb.Append(" x").Append(g.Count().ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
