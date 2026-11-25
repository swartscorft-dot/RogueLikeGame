using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>The kinds of card on the floor reward of three kinds (work order 138, part 2).</summary>
    public enum FloorRewardKind
    {
        /// <summary>A new skill, while a slot is free: drawn as before, with its tier.</summary>
        Skill,
        /// <summary>A basic-shot upgrade (part 3), drawn evenly.</summary>
        BasicShot,
        /// <summary>A way of the focus gauge (Hair Trigger or Steady Hand), only while none is taken and never after floor 3.</summary>
        Focus,
        /// <summary>An upgrade of a skill she has, from the elite room's table.</summary>
        SkillUpgrade,
        /// <summary>One large stat step, at the reward odds (<see cref="StatValues.RewardOdds"/>: rare or epic).</summary>
        Stat,
    }

    /// <summary>The floor reward of three kinds' numbers (work order 138), first guesses of the draft (section 2, Option 1).</summary>
    [Serializable]
    public sealed class MixedRewardValues
    {
        public int Cards = 3;            // cards a floor reward shows, each of another kind
        public int FocusLastFloor = 3;   // a focus way is offered after floors 1 to this one only ("never after floor 3")
    }

    /// <summary>One card of the floor reward of three kinds: its kind and what it gives.</summary>
    public sealed class FloorRewardCard
    {
        public FloorRewardKind Kind;
        /// <summary>A skill card's skill, its place in her list.</summary>
        public int Slot = -1;
        public FocusMode Focus;
        public SkillUpgradeDefinition Upgrade;
        public Upgrade Stat;
        public BasicShotOffer Shot;

        /// <summary>What kind it is, in the words the card says first.</summary>
        public string KindWord => MixedFloorRewards.KindWord(Kind);
        /// <summary>The log's word for its kind.</summary>
        public string LogWord => MixedFloorRewards.LogWord(Kind);

        public override string ToString() => Kind + (Kind == FloorRewardKind.Skill ? " " + Slot
                                             : Kind == FloorRewardKind.BasicShot && Shot != null ? " " + Shot.Short
                                             : Kind == FloorRewardKind.Focus ? " " + Focus
                                             : Kind == FloorRewardKind.SkillUpgrade && Upgrade != null ? " " + Upgrade.FullName
                                             : Kind == FloorRewardKind.Stat && Stat != null ? " " + Stat.Short : "");
    }

    /// <summary>
    /// The floor reward of three kinds (work order 138, part 2; his ticks of 2026-11-25: "Tre kort av tre slag", "Kortet blir et
    /// annet slag"; the draft's Option 1). After floors 1 to 4: a new skill while a slot is free, a basic-shot upgrade, and a
    /// third card drawn from a focus way (only while none is taken, never after <see cref="MixedRewardValues.FocusLastFloor"/>),
    /// an upgrade of a skill she has, or one large stat step. With the slots full there is no skill card and another kind takes
    /// its place: never the same kind twice on one screen. A kind with nothing to give is not drawn. Drawn from the run's seed,
    /// the floor and the draw on streams of their own, so the same run with the same picks offers the same; the skill card's
    /// skill is the one the reward of before would have shown first (<see cref="FloorRewards.Offer"/>).
    /// </summary>
    public static class MixedFloorRewards
    {
        public const string Stage = "mixed floor reward";

        /// <summary>The third card's kinds, in the order they are listed before the draw.</summary>
        public static readonly FloorRewardKind[] ThirdKinds = { FloorRewardKind.Focus, FloorRewardKind.SkillUpgrade, FloorRewardKind.Stat };

        public static string KindWord(FloorRewardKind k)
        {
            switch (k)
            {
                case FloorRewardKind.Skill: return "NEW SKILL";
                case FloorRewardKind.BasicShot: return BasicShotTexts.KindWord;
                case FloorRewardKind.Focus: return "FOCUS WAY";
                case FloorRewardKind.SkillUpgrade: return "SKILL UPGRADE";
                default: return "STAT STEP";
            }
        }

        public static string LogWord(FloorRewardKind k)
        {
            switch (k)
            {
                case FloorRewardKind.Skill: return "skill";
                case FloorRewardKind.BasicShot: return "basic shot";
                case FloorRewardKind.Focus: return "focus";
                case FloorRewardKind.SkillUpgrade: return "skill upgrade";
                default: return "stat";
            }
        }

        /// <summary>True when a focus way may be offered after this floor: none taken, and not after the last floor for it.</summary>
        public static bool FocusOpen(int floor, bool focusTaken, MixedRewardValues v) =>
            !focusTaken && floor >= 1 && floor <= (v ?? new MixedRewardValues()).FocusLastFloor;

        /// <summary>
        /// The cards of one floor reward, in the order shown: the skill card first, then the basic-shot card, then the others.
        /// <paramref name="skillSlot"/>: the skill a skill card would give (-1 when no slot is free or no skill is left);
        /// <paramref name="openUpgrades"/>: the elite table's upgrades she may still take (<see cref="SkillUpgradeOffer.Open"/>);
        /// <paramref name="stat"/>: the large stat step drawn for this reward (null when none, as with her stats off).
        /// </summary>
        public static List<FloorRewardCard> Offer(ulong runSeed, int floor, int draw, int skillSlot, RunBasicShotUpgrades held,
                                                  BasicShotUpgradeValues shotValues, bool focusTaken, IReadOnlyList<SkillUpgradeDefinition> openUpgrades,
                                                  Upgrade stat, MixedRewardValues v)
        {
            v = v ?? new MixedRewardValues();
            var cards = new List<FloorRewardCard>();
            int most = Math.Max(0, v.Cards);
            if (most == 0) return cards;
            if (skillSlot >= 0) cards.Add(new FloorRewardCard { Kind = FloorRewardKind.Skill, Slot = skillSlot });
            LevelRandom shotRng = LevelRandom.Stream(runSeed, BasicShotRules.Stage, floor * 8 + draw);
            BasicShotOffer shot = BasicShotRules.Draw(shotRng, held, shotValues);
            if (shot != null && cards.Count < most) cards.Add(new FloorRewardCard { Kind = FloorRewardKind.BasicShot, Shot = shot });
            var pool = new List<FloorRewardKind>();
            foreach (FloorRewardKind k in ThirdKinds)
            {
                if (k == FloorRewardKind.Focus && !FocusOpen(floor, focusTaken, v)) continue;
                if (k == FloorRewardKind.SkillUpgrade && (openUpgrades == null || openUpgrades.Count == 0)) continue;
                if (k == FloorRewardKind.Stat && stat == null) continue;
                pool.Add(k);
            }
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor * 8 + draw);
            while (cards.Count < most && pool.Count > 0)
            {
                int at = rng.Below(pool.Count);
                FloorRewardKind k = pool[at];
                pool.RemoveAt(at);
                var card = new FloorRewardCard { Kind = k };
                if (k == FloorRewardKind.Focus) card.Focus = FocusModeRules.Options[rng.Below(FocusModeRules.Options.Length)];
                else if (k == FloorRewardKind.SkillUpgrade) card.Upgrade = openUpgrades[rng.Below(openUpgrades.Count)];
                else card.Stat = stat;
                cards.Add(card);
            }
            return cards;
        }

        /// <summary>
        /// The large stat step of a floor reward: the first of a chest's tiered offer drawn at the reward's odds
        /// (<see cref="StatValues.RewardOdds"/>, 70 % rare and 30 % epic, the hook of work order 050 none used before), on a
        /// stream of its own (the run's seed mixed with this one's word), so no chest's offer moves. Null with her stats off.
        /// </summary>
        public static Upgrade StatStep(ulong runSeed, int floor, int draw, IReadOnlyList<string> equippedSkills, RunValues values, RunModifiers taken, bool atFullHealth)
        {
            if (values == null || !values.StatsAndTiers) return null;
            ulong seed = LevelRandom.Mix(runSeed ^ LevelRandom.Hash(Stage + " stat"));
            StatValues sv = values.Stats ?? new StatValues();
            List<Upgrade> offer = TieredOffers.Offer(seed, floor, draw, equippedSkills, values, taken, atFullHealth, 0, sv.RewardOdds);
            return offer.Count > 0 ? offer[0] : null;
        }
    }
}
