using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Work order 138 in the log, so his play can be read: the floor reward of three kinds (its reward_offer names each card
    /// and its kind, its reward_take the card taken and its kind), and what each basic-shot upgrade she holds did (a
    /// basic_shot_use line each, at every floor's reward and at the run's end: how often it worked and what it added).
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A floor reward of three kinds offered: each card's name and kind in the order shown, and the skill card's tier with tiers on.</summary>
        public void NoteMixedRewardOffered(int floor, IList<string> names, IList<string> kinds, string skillTier = null)
        {
            if (skillTier == null) Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", names), "kinds", string.Join(" | ", kinds));
            else Note(SessionEventTypes.RewardOffer, null, "floor", Int(floor), "offered", string.Join(" | ", names), "kinds", string.Join(" | ", kinds), "tier", skillTier);
        }

        /// <summary>A card of the floor reward of three kinds taken: its name and kind; a rare or epic one's tier, and an epic skill's change.</summary>
        public void NoteMixedRewardTaken(int floor, string name, string kind, string tier = null, string with = null)
        {
            if (string.IsNullOrEmpty(tier) || tier == UpgradeTiers.Word(UpgradeTier.Common)) Note(SessionEventTypes.RewardTake, name, "floor", Int(floor), "kind", kind);
            else if (with == null) Note(SessionEventTypes.RewardTake, name, "floor", Int(floor), "kind", kind, "tier", tier);
            else Note(SessionEventTypes.RewardTake, name, "floor", Int(floor), "kind", kind, "tier", tier, "with", with);
        }

        /// <summary>What a basic-shot upgrade did since its last line: how often it worked and what it added, in its unit.</summary>
        public void NoteBasicShotUse(int floor, string name, int fired, float added, string unit) =>
            Note(SessionEventTypes.BasicShotUse, name, "floor", Int(floor), "fired", Int(fired), "added", added.ToString("0.##", CultureInfo.InvariantCulture), "unit", unit);
    }
}
