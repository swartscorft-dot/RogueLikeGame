using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The floor reward's tiers (decision 0013; Kristian, 2026-11-20: "vi har ikke noe tiers på upgrades og spells", and his
    /// choice of 2026-11-21, "Tiers på ferdigheter"; work order 057), as part of <see cref="GeneratedLevels"/>. With
    /// <see cref="RunValues.SkillTiers"/> on, each card of the floor reward draws a tier (<see cref="SkillTiers.Draw"/>), kept
    /// beside the offered slots wherever they go (a drop on the ground, Second Look's redraw, the choice closing); a rare or
    /// epic card's skill is taken with its step and an epic's change into the run's skill upgrades
    /// (<see cref="RunSkillUpgrades.TakeTier"/>), and the record and the recorder are told. A card is drawn with its tier's
    /// word and numeral ink (kinetic) or edge (the old look). Off, nothing here draws, and every card, list and number is as
    /// before. The slots (<see cref="OfferedSkills"/>) are drawn as before, the tiers on a stream of their own.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// The tiers of the floor reward's cards on screen, in the order of <see cref="OfferedSkills"/> (work order 057); empty
        /// with the switch off. A missing or empty entry is an untiered card.
        /// </summary>
        public IReadOnlyList<SkillTierOffer> OfferedSkillTiers => _offeredSkillTiers;

        readonly List<SkillTierOffer> _offeredSkillTiers = new List<SkillTierOffer>();

        /// <summary>The tier of the floor reward's card <paramref name="index"/>, or null (an untiered card).</summary>
        public SkillTierOffer OfferedTierAt(int index) => index >= 0 && index < _offeredSkillTiers.Count ? _offeredSkillTiers[index] : null;

        /// <summary>
        /// The tiers of the offered slots, drawn again (<paramref name="draw"/> 0 for the floor's reward, 1 for Second Look's
        /// redraw), from her luck (0 with the stats off), the elite room's table and the skill upgrades taken. None with the switch off.
        /// </summary>
        void DrawSkillTiers(PlayerCharacter player, int draw)
        {
            _offeredSkillTiers.Clear();
            if (!RunValues.SkillTiers || player == null || _offeredSkills.Count == 0) return;
            _offeredSkillTiers.AddRange(SkillTiers.ForReward(RunValues, RunSeed ?? 0UL, Floor, draw, _offeredSkills, player.Skills, UpgradeTableNow,
                                                             SkillUpgrades.Taken, Modifiers.Stats.Luck, Modifiers.StatValues));
        }

        /// <summary>The recorder is told of the floor reward on screen: its skills, and with the switch on each card's tier.</summary>
        void NoteRewardOffer(PlayerCharacter player)
        {
            if (PlaySessionRecorder.Current == null) return;
            if (_mixedOn) { NoteMixedRewardOffer(player); return; } // work order 138: each card's name and kind
            List<string> names = _offeredSkills.Select(s => player.Skills[s].Name).ToList();
            List<string> tiers = RunValues.SkillTiers && _offeredSkillTiers.Count > 0
                ? _offeredSkills.Select((s, i) => OfferedTierAt(i) != null ? OfferedTierAt(i).Word : UpgradeTiers.Word(UpgradeTier.Common)).ToList()
                : null;
            PlaySessionRecorder.Current.NoteRewardOffered(Floor, names, tiers);
        }

        /// <summary>
        /// Card <paramref name="index"/>'s skill was equipped: a rare or epic card's step and change are taken with it, and the
        /// record and the recorder are told of the skill with its tier. With no tier, as before.
        /// </summary>
        void TookRewardTier(int index, string name)
        {
            SkillTierOffer tier = OfferedTierAt(index);
            bool tiered = tier != null && tier.Tiered;
            if (tiered) SkillUpgrades.TakeTier(tier);
            string change = tiered && tier.Change != null ? tier.Change.Name : null;
            Record?.TakeSkill(name, tiered ? tier.Tier : UpgradeTier.Common, change);
            PlaySessionRecorder.Current?.NoteRewardTaken(Floor, name, tiered ? tier.Word : null, tiered ? change ?? "" : null);
        }

        /// <summary>
        /// A floor reward card's text in the kinetic skin (work order 057): a common card's is today's, word for word; a rare or
        /// epic card's begins with its tier's word and what it adds, then today's line of key, cooldown (the tiered skill's) and
        /// mark role and the skill's own line, and an epic's ends with its change's name and line. Its numeral's ink is the tier's.
        /// </summary>
        string RewardCardText(int index, SkillDefinition s, string key, out UiInk ink)
        {
            SkillTierOffer tier = OfferedTierAt(index);
            bool tiered = tier != null && tier.Tiered;
            ink = InterfaceRules.TierInk(tiered, tiered ? tier.Tier : UpgradeTier.Common);
            SkillDefinition shown = tiered ? SkillTiers.Preview(s, tier) : s;
            string today = string.Format(CultureInfo.InvariantCulture, "key {0}   cooldown {1:0.#} s   {2}\n{3}",
                                         key, shown.Cooldown, SkillDefaults.MarkRole(s), SkillDefaults.Line(s.Name));
            if (!tiered) return today;
            return SkillTiers.CardTierLine(tier) + "\n" + today + (tier.Change != null ? "\n" + SkillTiers.CardChangeLine(tier) : "");
        }

        /// <summary>
        /// A floor reward card's text in the old look (<see cref="DrawChoice"/>): today's, and for a rare or epic card the same
        /// words as the kinetic card's, the tier's line under the first and an epic's change last. Public for tests.
        /// </summary>
        public string RewardChoiceText(int index)
        {
            PlayerCharacter player = _player;
            if (player == null || index < 0 || index >= _offeredSkills.Count) return "";
            InputBindings bindings = player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            SkillDefinition s = player.Skills[_offeredSkills[index]];
            SkillTierOffer tier = OfferedTierAt(index);
            bool tiered = tier != null && tier.Tiered;
            SkillDefinition shown = tiered ? SkillTiers.Preview(s, tier) : s;
            string first = string.Format(CultureInfo.InvariantCulture, "{0}   {1}   key {2}   cooldown {3:0.#} s   {4}", index + 1, s.Name,
                                         bindings != null ? bindings.SkillLabel(player.FirstFreeKey) : "?", // the key it will go on (work order 015)
                                         shown.Cooldown, SkillDefaults.MarkRole(s));
            if (!tiered) return first + "\n" + SkillDefaults.Line(s.Name);
            return first + "\n" + SkillTiers.CardTierLine(tier) + "\n" + SkillDefaults.Line(s.Name) + (tier.Change != null ? "\n" + SkillTiers.CardChangeLine(tier) : "");
        }

        /// <summary>
        /// The old look's row height for the floor reward: today's 74 with no tiered card, else as high as the longest card's
        /// text needs at the row's width (work order 055's rule: nothing a card says is cut off).
        /// </summary>
        float RewardRowHeight(float rowWidth, GUIStyle style)
        {
            const float today = 74f;
            bool any = false;
            for (int i = 0; i < _offeredSkillTiers.Count; i++) any |= _offeredSkillTiers[i] != null && _offeredSkillTiers[i].Tiered;
            if (!any || style == null) return today;
            float most = today;
            for (int i = 0; i < _offeredSkills.Count; i++) most = Mathf.Max(most, Mathf.Ceil(style.CalcHeight(new GUIContent(RewardChoiceText(i)), rowWidth)) + 2f);
            return most;
        }
    }
}
