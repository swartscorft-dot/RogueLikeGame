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

        public UpgradeDefinition() { }

        public UpgradeDefinition(UpgradeKind kind, float amount, int heal = 0, string skill = "")
        {
            Kind = kind;
            Amount = amount;
            Heal = heal;
            Skill = skill;
        }
    }

    /// <summary>One upgrade as offered and taken: a row of the table with its skill filled in.</summary>
    public sealed class Upgrade
    {
        public UpgradeKind Kind;
        public float Amount;
        public int Heal;
        public string Skill = "";

        static string Pct(float share) => (share * 100f).ToString("0.#", CultureInfo.InvariantCulture) + "%";
        static string Int(float v) => ((int)Math.Round(v)).ToString(CultureInfo.InvariantCulture);

        /// <summary>What it does, in one line of plain words, for the choice on screen.</summary>
        public string Text =>
            Kind == UpgradeKind.MaxHealth ? "+" + Int(Amount) + " maximum health, and heal " + Heal.ToString(CultureInfo.InvariantCulture)
            : Kind == UpgradeKind.Damage ? "+" + Pct(Amount) + " damage dealt"
            : Kind == UpgradeKind.Cooldown ? "-" + Pct(Amount) + " cooldown on " + Skill
            : Kind == UpgradeKind.RunSpeed ? "+" + Pct(Amount) + " run speed"
            : Kind == UpgradeKind.DashCooldown ? "-" + Pct(Amount) + " dash cooldown"
            : "+" + Int(Amount) + " mark from " + Skill + "'s hit on a target with none";

        /// <summary>The short form for the box on screen and the log.</summary>
        public string Short =>
            Kind == UpgradeKind.MaxHealth ? "+" + Int(Amount) + " hp"
            : Kind == UpgradeKind.Damage ? "+" + Pct(Amount) + " dmg"
            : Kind == UpgradeKind.Cooldown ? "-" + Pct(Amount) + " cd " + Skill
            : Kind == UpgradeKind.RunSpeed ? "+" + Pct(Amount) + " speed"
            : Kind == UpgradeKind.DashCooldown ? "-" + Pct(Amount) + " cd dash"
            : "+" + Int(Amount) + " first mark " + Skill;

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
        public float CooldownFloor = 0.5f;     // a skill's cooldown never goes under this share of its own
        public UpgradeDefinition[] Upgrades = DefaultUpgrades();

        /// <summary>
        /// The order's examples: +10 maximum health and heal 10; +5 % damage; -5 % cooldown on a skill she has; +5 % run
        /// speed; +1 mark from Quick Shot's first hit. Work order 017: -8 % dash cooldown.
        /// </summary>
        public static UpgradeDefinition[] DefaultUpgrades() => new[]
        {
            new UpgradeDefinition(UpgradeKind.MaxHealth, 10f, heal: 10),
            new UpgradeDefinition(UpgradeKind.Damage, 0.05f),
            new UpgradeDefinition(UpgradeKind.Cooldown, 0.05f),
            new UpgradeDefinition(UpgradeKind.RunSpeed, 0.05f),
            new UpgradeDefinition(UpgradeKind.FirstMark, 1f, skill: "Quick Shot"),
            new UpgradeDefinition(UpgradeKind.DashCooldown, 0.08f),
        };
    }

    /// <summary>
    /// The chests (work order 012): each offers <see cref="RunValues.UpgradesOffered"/> different rows of the table,
    /// drawn from the run's seed, the floor's number and the chest's place in the floor, so the same run offers the same.
    /// A Cooldown row names one of the skills she has, drawn too.
    /// </summary>
    public static class ChestUpgrades
    {
        public const string Stage = "chest";

        public static List<Upgrade> Offer(ulong runSeed, int floor, int chest, IReadOnlyList<string> equippedSkills, RunValues values)
        {
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor * 64 + chest);
            var rows = Enumerable.Range(0, values.Upgrades?.Length ?? 0).ToList();
            rng.Shuffle(rows);
            var offer = new List<Upgrade>();
            foreach (int i in rows)
            {
                if (offer.Count >= values.UpgradesOffered) break;
                UpgradeDefinition d = values.Upgrades[i];
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
    /// maximum health, a skill's cooldown, run speed, a skill's first mark, and since work order 017 the dash's cooldown). It never writes to the settings. Cleared
    /// when a run starts or ends.
    /// </summary>
    public sealed class RunModifiers
    {
        readonly List<Upgrade> _taken = new List<Upgrade>();
        public float CooldownFloor = 0.5f;

        public IReadOnlyList<Upgrade> Taken => _taken;
        public int Count => _taken.Count;

        public void Take(Upgrade u)
        {
            if (u != null) _taken.Add(u);
        }

        public void Clear() => _taken.Clear();

        float Sum(UpgradeKind kind, string skill = null)
        {
            float s = 0f;
            foreach (Upgrade u in _taken)
                if (u.Kind == kind && (skill == null || u.Skill == skill)) s += u.Amount;
            return s;
        }

        /// <summary>Maximum health added.</summary>
        public int BonusMaxHealth => (int)Math.Round(Sum(UpgradeKind.MaxHealth));
        /// <summary>The share of damage dealt: 1 plus each damage upgrade's share.</summary>
        public float DamageShare => 1f + Sum(UpgradeKind.Damage);
        public int ScaleDamage(int damage) => damage <= 0 ? damage : (int)Math.Round(damage * DamageShare, MidpointRounding.AwayFromZero);
        /// <summary>The share of a skill's own cooldown it runs: 1 less each cut to it, never under <see cref="CooldownFloor"/>.</summary>
        public float CooldownShare(string skill) => Math.Max(CooldownFloor, 1f - Sum(UpgradeKind.Cooldown, skill ?? ""));
        public float RunSpeedShare => 1f + Sum(UpgradeKind.RunSpeed);
        public int FirstMarkBonus(string skill) => (int)Math.Round(Sum(UpgradeKind.FirstMark, skill ?? ""));
        /// <summary>The share of the dash's own cooldown it runs (work order 017): 1 less each cut, never under <see cref="CooldownFloor"/>.</summary>
        public float DashCooldownShare => Math.Max(CooldownFloor, 1f - Sum(UpgradeKind.DashCooldown));

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
