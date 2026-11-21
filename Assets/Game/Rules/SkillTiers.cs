using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the skills' tiers at the floor reward (decision 0013, the producer's proposal; work order 057; Kristian,
    /// 2026-11-20: "vi har ikke noe tiers på upgrades og spells", and his choice of 2026-11-21, "Tiers på ferdigheter"). All
    /// first guesses, with their defaults in code: no asset holds them.
    /// </summary>
    [Serializable]
    public sealed class SkillTierValues
    {
        /// <summary>
        /// A floor-reward card's odds of a common, a rare and an epic, before luck (60 / 30 / 10; the producer's guess: a
        /// floor reward is "relatively strong", decision 0008, so it is rarer than a chest's 75 / 22 / 3). About 78 % of floor
        /// rewards show a rare or better and 27 % an epic.
        /// </summary>
        public float[] Odds = { 60f, 30f, 10f };
        /// <summary>Floor 1's reward always holds a rare or better, so the tiers are met on every run's first floor (decision 0013).</summary>
        public bool FirstRewardAtLeastRare = true;
        /// <summary>A rare or epic skill's Damage and Stagger times this (+30 %): one plain step for every skill (decision 0013).</summary>
        public float RareDamage = 1.3f;
        /// <summary>Instead, for a skill whose Damage is 0 (Slide), its Cooldown times this (20 % shorter): the same weight in the one number it has.</summary>
        public float RareCooldown = 0.8f;
    }

    /// <summary>
    /// One floor-reward card's tier (work order 057): the skill offered (its slot and name), the tier drawn, the tier's
    /// numbers step (<see cref="SkillTiers.Step"/>, null for a common), the epic's change (a Change row of the elite room's
    /// table, null otherwise), and what is taken with the skill when the card is picked (<see cref="Rows"/>: the step, then
    /// the change). <see cref="Line"/> is what the tier adds, in plain words for the card; an epic's line is the rare's, and
    /// its change is shown by the card on a line of its own.
    /// </summary>
    public sealed class SkillTierOffer
    {
        public int Slot;
        public string Skill = "";
        public UpgradeTier Tier;
        public SkillUpgradeDefinition Step;
        public SkillUpgradeDefinition Change;
        /// <summary>What the tier adds, its step's line ("+30% damage and stagger", "-20% cooldown"); "" for a common.</summary>
        public string Line => Tiered && Step != null ? Step.Line : "";

        static readonly SkillUpgradeDefinition[] s_None = Array.Empty<SkillUpgradeDefinition>();

        /// <summary>True for a rare or an epic: a card with a tier to show and to take.</summary>
        public bool Tiered => Tier != UpgradeTier.Common;
        /// <summary>"common", "rare" or "epic" (<see cref="UpgradeTiers.Word"/>).</summary>
        public string Word => UpgradeTiers.Word(Tier);

        /// <summary>What is taken with the skill: the step, then the change; none for a common.</summary>
        public IReadOnlyList<SkillUpgradeDefinition> Rows =>
            !Tiered || Step == null ? s_None : Change != null ? new[] { Step, Change } : new[] { Step };

        public override string ToString() => Skill + " (" + Word + (Change != null ? ", " + Change.Name : "") + ")";
    }

    /// <summary>
    /// The skills' tiers at the floor reward (decision 0013; work order 057). Each of the reward's cards draws a tier,
    /// common, rare or epic, from <see cref="SkillTierValues.Odds"/> leaned by her luck as a chest's are (the chest's own
    /// rule, <see cref="UpgradeTiers.Shares"/>). A common is the skill as it is. A rare is the skill with its numbers step
    /// (<see cref="Step"/>). An epic is the rare's step and one of the skill's own Change upgrades of the elite room's table,
    /// drawn from those still open; a skill with none open comes as rare. Both are written into the run's list of skill
    /// upgrades (<see cref="RunSkillUpgrades.TakeTier"/>), which the elite room already reads: one system. Floor 1's reward
    /// always holds a rare or better (<see cref="SkillTierValues.FirstRewardAtLeastRare"/>): when none was drawn, its last
    /// card's tier is drawn again from rare and epic alone. Drawn on a stream of its own, so the slots the floor reward offers
    /// (<see cref="FloorRewards.Offer"/>) are the same with the tiers on or off; the same run seed with the same picks draws
    /// the same tiers.
    /// </summary>
    public static class SkillTiers
    {
        public const string Stage = "floor reward tiers";
        /// <summary>The step row's name: the tier's word, never shown as an upgrade.</summary>
        public const string StepName = "rare";

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>
        /// A skill's tier step (rare and epic alike): Damage and Stagger times <see cref="SkillTierValues.RareDamage"/>, or for a
        /// skill that deals no damage (Slide) its Cooldown times <see cref="SkillTierValues.RareCooldown"/>. A numbers row with
        /// <see cref="SkillUpgradeDefinition.TierStep"/> set: taken with the skill, never in the table, never offered, no pip,
        /// no name in the lists.
        /// </summary>
        public static SkillUpgradeDefinition Step(SkillDefinition skill, SkillTierValues v)
        {
            v = v ?? new SkillTierValues();
            string name = skill != null ? skill.Name : "";
            bool damage = skill == null || skill.Damage != 0;
            SkillPatch[] patches = damage
                ? new[] { SkillPatch.Scale(nameof(SkillDefinition.Damage), v.RareDamage), SkillPatch.Scale(nameof(SkillDefinition.Stagger), v.RareDamage) }
                : new[] { SkillPatch.Scale(nameof(SkillDefinition.Cooldown), v.RareCooldown) };
            return new SkillUpgradeDefinition(name, StepName, SkillUpgradeKind.Numbers, StepLine(damage, v), null, patches) { TierStep = true };
        }

        /// <summary>"+30% damage and stagger", or "-20% cooldown" for a skill with no damage.</summary>
        static string StepLine(bool damage, SkillTierValues v) =>
            damage
                ? "+" + Math.Round((v.RareDamage - 1f) * 100f).ToString("0", s_Inv) + "% damage and stagger"
                : "-" + Math.Round((1f - v.RareCooldown) * 100f).ToString("0", s_Inv) + "% cooldown";

        /// <summary>
        /// One <see cref="SkillTierOffer"/> per offered slot, in the same order; an empty list with no slots.
        /// <paramref name="draw"/> is 0 for the floor's reward and 1 for Second Look's redraw. <paramref name="skills"/> is the
        /// skill list the slots index; <paramref name="table"/> the elite room's table and <paramref name="taken"/> the run's
        /// skill upgrades taken, which an epic's change is drawn against (<see cref="SkillUpgradeOffer.Open"/>).
        /// </summary>
        public static List<SkillTierOffer> Draw(ulong runSeed, int floor, int draw, IReadOnlyList<int> slots, IReadOnlyList<SkillDefinition> skills,
                                                IEnumerable<SkillUpgradeDefinition> table, IReadOnlyList<SkillUpgradeDefinition> taken,
                                                float luck, SkillTierValues values, StatValues stats)
        {
            var offers = new List<SkillTierOffer>();
            if (slots == null || slots.Count == 0) return offers;
            values = values ?? new SkillTierValues();
            stats = stats ?? new StatValues();
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor * 16 + draw);
            double[] shares = UpgradeTiers.Shares(values.Odds, luck, stats);

            // Each card's tier, in the order shown.
            var tiers = new UpgradeTier[slots.Count];
            bool rareOrBetter = false;
            for (int i = 0; i < tiers.Length; i++)
            {
                tiers[i] = UpgradeTiers.Draw(rng, shares);
                rareOrBetter |= tiers[i] != UpgradeTier.Common;
            }
            // Floor 1 shows a rare or better: the last card is drawn again from rare and epic alone, in their shares' proportion.
            if (floor == 1 && values.FirstRewardAtLeastRare && !rareOrBetter)
            {
                double rare = shares[1], epic = shares[2];
                double x = (rng.NextBits() >> 11) * (1.0 / (1UL << 53));
                tiers[tiers.Length - 1] = rare + epic <= 0.0 || x * (rare + epic) < rare ? UpgradeTier.Rare : UpgradeTier.Epic;
            }

            // The steps, and each epic's change, drawn in card order after the tiers.
            for (int i = 0; i < slots.Count; i++)
            {
                int slot = slots[i];
                SkillDefinition skill = skills != null && slot >= 0 && slot < skills.Count ? skills[slot] : null;
                var offer = new SkillTierOffer { Slot = slot, Skill = skill != null ? skill.Name : "", Tier = skill != null ? tiers[i] : UpgradeTier.Common };
                if (offer.Tier == UpgradeTier.Epic)
                {
                    List<SkillUpgradeDefinition> open = OpenChanges(table, offer.Skill, taken);
                    if (open.Count == 0) offer.Tier = UpgradeTier.Rare; // nothing left to change it by: it comes as rare
                    else offer.Change = open[rng.Below(open.Count)];
                }
                if (offer.Tiered) offer.Step = Step(skill, values);
                offers.Add(offer);
            }
            return offers;
        }

        /// <summary>
        /// The floor reward's tiers as the run draws them (work order 057): none with <see cref="RunValues.SkillTiers"/> off,
        /// else <see cref="Draw"/> with the run's numbers.
        /// </summary>
        public static List<SkillTierOffer> ForReward(RunValues values, ulong runSeed, int floor, int draw, IReadOnlyList<int> slots,
                                                     IReadOnlyList<SkillDefinition> skills, IEnumerable<SkillUpgradeDefinition> table,
                                                     IReadOnlyList<SkillUpgradeDefinition> taken, float luck, StatValues stats = null) =>
            values == null || !values.SkillTiers ? new List<SkillTierOffer>()
                : Draw(runSeed, floor, draw, slots, skills, table, taken, luck, values.SkillTierNumbers, stats ?? values.Stats);

        /// <summary>The Change rows of the elite room's table still open for this skill (<see cref="SkillUpgradeOffer.Open"/>), in table order.</summary>
        public static List<SkillUpgradeDefinition> OpenChanges(IEnumerable<SkillUpgradeDefinition> table, string skill, IReadOnlyList<SkillUpgradeDefinition> taken) =>
            SkillUpgradeOffer.Open(table, new[] { skill ?? "" }, taken).Where(u => u.Kind == SkillUpgradeKind.Change && !u.TierStep).ToList();

        /// <summary>The skill as she would have it at the card's tier: patched by its rows (a copy); the skill itself for a common.</summary>
        public static SkillDefinition Preview(SkillDefinition skill, SkillTierOffer offer) =>
            skill == null || offer == null || !offer.Tiered ? skill : RunSkillUpgrades.Patched(skill, offer.Rows);

        /// <summary>The card's first line for a rare or an epic, "RARE   +30% damage and stagger"; "" for a common.</summary>
        public static string CardTierLine(SkillTierOffer offer) =>
            offer == null || !offer.Tiered ? "" : offer.Word.ToUpperInvariant() + "   " + offer.Line;

        /// <summary>An epic card's last line, its change's name in capitals and its line ("OVERCHARGE  The perfect band ..."); "" otherwise.</summary>
        public static string CardChangeLine(SkillTierOffer offer) =>
            offer == null || offer.Change == null ? "" : offer.Change.Name.ToUpperInvariant() + "  " + offer.Change.Line;

        /// <summary>What a skill came with, for the lists: "epic, Overcharge", "rare"; "" for a common.</summary>
        public static string WithText(UpgradeTier tier, string change) =>
            tier == UpgradeTier.Common ? "" : UpgradeTiers.Word(tier) + (string.IsNullOrEmpty(change) ? "" : ", " + change);

        /// <summary>A skill's name with what it came with: "Long Shot (epic, Overcharge)", "Fan Fire (rare)", "Slide".</summary>
        public static string Named(string skill, string with) => string.IsNullOrEmpty(with) ? skill ?? "" : (skill ?? "") + " (" + with + ")";
    }
}
