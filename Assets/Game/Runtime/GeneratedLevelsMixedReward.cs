using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The floor reward of three kinds (work order 138, part 2; Kristian, 2026-11-25: "Jeg syntes også det er rart å alltid få
    /// en fokus upgrade hver gang etter første flor", and his ticks "Tre kort av tre slag" and "Kortet blir et annet slag"), as
    /// part of <see cref="GeneratedLevels"/>, with <see cref="RunValues.MixedFloorReward"/> on: after floors 1 to 4 the reward
    /// shows a new skill while a slot is free (with its tier, as before), a basic-shot upgrade (part 3), and a third card that
    /// is a focus way (only while none is taken, never after floor 3), an upgrade of a skill she has, or one large stat step
    /// (<see cref="MixedFloorRewards.Offer"/>). Never two of a kind on one screen. The focus screen after floor 1 is gone: focus
    /// is a card she may take or never take. Taking any card heals her as before (<see cref="RunValues.FloorHeal"/>); the drop
    /// on the ground, its guard and the tiers are as before. The cards are in <see cref="OfferedRewardCards"/>, the skill card
    /// first; its skill is the one slot in <see cref="OfferedSkills"/> (with its tier at place 0), so every path that reads the
    /// skill (the drop, the tiers, Second Look) is the one of before. Off, nothing here runs.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        readonly List<FloorRewardCard> _mixedCards = new List<FloorRewardCard>();
        /// <summary>True while the floor reward on offer (on screen, or waiting in its drop) is the one of three kinds.</summary>
        bool _mixedOn;

        /// <summary>The cards of the floor reward of three kinds, in the order shown (the skill card first); empty with the switch off.</summary>
        public IReadOnlyList<FloorRewardCard> OfferedRewardCards => _mixedOn ? (IReadOnlyList<FloorRewardCard>)_mixedCards : s_NoCards;
        static readonly FloorRewardCard[] s_NoCards = new FloorRewardCard[0];

        /// <summary>True while the floor reward on screen is the one of three kinds.</summary>
        public bool MixedRewardShown => _mixedOn && Choice == ChoiceKind.FloorReward;

        /// <summary>How many cards the floor reward on screen shows: the three kinds', or the skills of before.</summary>
        int RewardCardCount => MixedRewardShown ? _mixedCards.Count : _offeredSkills.Count;

        /// <summary>The colour of a card's kind line, so the kinds are told apart at a glance: a skill teal, a basic shot gold, a focus way this blue, the others white.</summary>
        static readonly Color s_KindFocus = new Color(0.35f, 0.68f, 1f, 1f);

        /// <summary>
        /// The floor reward of three kinds is drawn and put up (the drop first, as before). Returns false with the switch off,
        /// and the reward of before is offered instead.
        /// </summary>
        bool OfferMixedFloorReward(PlayerCharacter player)
        {
            _mixedOn = false;
            _mixedCards.Clear();
            if (!RunValues.MixedFloorReward) return false;
            ulong seed = RunSeed ?? 0UL;
            // The skill card: while a slot is free, the skill the reward of before would have shown first, with its tier.
            var equipped = new List<bool>();
            for (int slot = 0; slot < player.Skills.Count; slot++) equipped.Add(player.IsEquipped(slot));
            StrikeOutOfOffer(player, equipped); // work order 029: Not That One
            _offeredSkills.Clear();
            if (player.FirstFreeKey >= 0)
            {
                List<int> skills = FloorRewards.Offer(seed, Floor, equipped, RunValues.SkillsOffered);
                if (skills.Count > 0) _offeredSkills.Add(skills[0]);
            }
            DrawSkillTiers(player, 0);
            // The third card's pools: her skills' open upgrades, and one large stat step at the reward odds.
            var had = new HashSet<string>();
            var names = new List<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot) && player.Skills[slot] != null)
                {
                    had.Add(player.Skills[slot].Name);
                    names.Add(player.Skills[slot].Name);
                }
            List<SkillUpgradeDefinition> open = SkillUpgradeOffer.Open(UpgradeTableNow, had, SkillUpgrades.Taken);
            bool fullHealth = _health == null || _health.Current >= _health.Max;
            Upgrade stat = MixedFloorRewards.StatStep(seed, Floor, 0, names, RunValues, Modifiers, fullHealth);
            _mixedCards.AddRange(MixedFloorRewards.Offer(seed, Floor, 0, _offeredSkills.Count > 0 ? _offeredSkills[0] : -1, Modifiers.BasicShot,
                                                         RunValues.BasicShot, FocusChoice.Taken, open, stat, RunValues.MixedReward));
            NoteShotUpgradeUse(); // what the basic-shot upgrades did on the floor just finished
            if (_mixedCards.Count == 0)
            {
                _offeredSkills.Clear();
                _offeredSkillTiers.Clear();
                if (!EliteRewardFirst()) EnterFloor(Floor + 1); // nothing left to give: as when no skill was left
                return true;
            }
            _mixedOn = true;
            NoteRewardOffer(player);
            OpenChoice(ChoiceKind.FloorReward);
            return true;
        }

        /// <summary>
        /// Takes card <paramref name="index"/> of the floor reward of three kinds: what its kind gives, the heal of before, and
        /// on to the next floor (no focus screen comes after it). False when no such card is offered.
        /// </summary>
        bool PickMixedReward(int index)
        {
            if (Choice != ChoiceKind.FloorReward || index < 0 || index >= _mixedCards.Count) return false;
            CardPicked(index); // work order 045: its card flashes as it goes
            PlayerCharacter player = FindPlayer();
            FloorRewardCard card = _mixedCards[index];
            string taken = TakeMixedCard(player, card);
            Debug.Log("Floor reward (" + card.LogWord + "): " + taken);
            if (_health != null)
            {
                int healed = _health.Heal(FloorHeals.Amount(_health.Max, RunValues.FloorHeal));
                Record?.FloorHealed(Floor, healed);
                PlaySessionRecorder.Current?.NoteFloorHeal(Floor, healed, _health.Current);
            }
            _mixedOn = false;
            _mixedCards.Clear();
            if (LabTookTheReward()) return true; // work order 084: in the room lab she stays in the room
            CloseChoice();
            EnterFloor(Floor + 1);
            return true;
        }

        /// <summary>What a card gives, taken into the run, the record and the log; returns what was taken, in words.</summary>
        string TakeMixedCard(PlayerCharacter player, FloorRewardCard card)
        {
            switch (card.Kind)
            {
                case FloorRewardKind.Skill:
                {
                    player.EquipSkill(card.Slot);
                    string name = player.Skills[card.Slot].Name;
                    _skillsTaken.Add(name);
                    // As TookRewardTier (work order 057), with the card's kind in the log's line.
                    SkillTierOffer tier = OfferedTierAt(0);
                    bool tiered = tier != null && tier.Tiered;
                    if (tiered) SkillUpgrades.TakeTier(tier);
                    string change = tiered && tier.Change != null ? tier.Change.Name : null;
                    Record?.TakeSkill(name, tiered ? tier.Tier : UpgradeTier.Common, change);
                    PlaySessionRecorder.Current?.NoteMixedRewardTaken(Floor, name, card.LogWord, tiered ? tier.Word : null, tiered ? change ?? "" : null);
                    return name;
                }
                case FloorRewardKind.BasicShot:
                {
                    Modifiers.BasicShot.Values = RunValues.BasicShot;
                    Modifiers.BasicShot.KitPerMark = player.Kit.BasicShotsPerMark;
                    Modifiers.BasicShot.MarkCap = player.Kit.MaxMarks;
                    Modifiers.BasicShot.Take(card.Shot.Kind);
                    Record?.TakeUpgrade(card.Shot.Short, BasicShotTexts.InkTier(card.Shot.Tier));
                    PlaySessionRecorder.Current?.NoteMixedRewardTaken(Floor, card.Shot.Short, card.LogWord);
                    return card.Shot.Short;
                }
                case FloorRewardKind.Focus:
                {
                    // Work order 025's take, from a card: the run's way from now on; a window open now ends here, as there.
                    FocusChoice.Offer();
                    FocusChoice.Take(card.Focus);
                    string name = FocusModeRules.Name(card.Focus);
                    Record?.TakeFocusChoice(name);
                    PlaySessionRecorder.Current?.NoteMixedRewardTaken(Floor, name, card.LogWord);
                    PlaySessionRecorder.Current?.NoteFocusChoiceTaken(Floor, name);
                    player.EndFocusWindow();
                    return name;
                }
                case FloorRewardKind.SkillUpgrade:
                {
                    SkillUpgrades.Take(card.Upgrade);
                    Record?.TakeSkillUpgrade(card.Upgrade.FullName);
                    PlaySessionRecorder.Current?.NoteMixedRewardTaken(Floor, card.Upgrade.FullName, card.LogWord);
                    PlaySessionRecorder.Current?.NoteSkillUpgradeTaken(Floor, card.Upgrade.FullName, card.Upgrade.KindWord);
                    return card.Upgrade.FullName;
                }
                default:
                {
                    // As a chest's pick (PickUpgrade): the step into the run's modifiers, a second dash at once, her maximum.
                    Upgrade u = card.Stat;
                    Modifiers.Take(u);
                    player.RefreshDashCharges();
                    Record?.TakeUpgrade(u.Short, u.Tiered ? u.Tier : UpgradeTier.Common);
                    if (_health != null)
                    {
                        _health.RefreshMax();
                        if (u.Heal > 0) _health.Heal(u.Heal);
                    }
                    PlaySessionRecorder.Current?.NoteMixedRewardTaken(Floor, u.Short, card.LogWord, u.Tiered ? u.TierWord : null);
                    return u.Short;
                }
            }
        }

        /// <summary>Second Look on the floor reward of three kinds: the skill card drawn again, one skill (none when no skill card shows).</summary>
        int RedrawCount => _mixedOn ? 1 : RunValues.SkillsOffered;

        /// <summary>After Second Look drew the skill again: the skill card is a new card with the new skill.</summary>
        void MixedSkillRedrawn()
        {
            if (!_mixedOn || _offeredSkills.Count == 0) return;
            for (int i = 0; i < _mixedCards.Count; i++)
                if (_mixedCards[i].Kind == FloorRewardKind.Skill) _mixedCards[i] = new FloorRewardCard { Kind = FloorRewardKind.Skill, Slot = _offeredSkills[0] };
        }

        // ---------------- The cards' words ----------------

        /// <summary>The words of a skill card's kind: "NEW SKILL   SLOT 5 OF 6", the slot it would fill (the order's).</summary>
        string SkillKindLine(PlayerCharacter player) =>
            MixedFloorRewards.KindWord(FloorRewardKind.Skill) + "   SLOT " + (player.SlotsFilled + 1).ToString(CultureInfo.InvariantCulture)
            + " OF " + player.BarCount.ToString(CultureInfo.InvariantCulture);

        /// <summary>Card <paramref name="index"/> of the floor reward of three kinds as it reads (<see cref="OfferTexts"/>), its kind first.</summary>
        OfferCard MixedCardFor(int index, OfferContext context, InputBindings bindings)
        {
            PlayerCharacter player = _player;
            FloorRewardCard c = _mixedCards[index];
            OfferCard card;
            switch (c.Kind)
            {
                case FloorRewardKind.Skill:
                    card = OfferTexts.ForSkill(player.Skills[c.Slot], OfferedTierAt(0), bindings != null ? bindings.SkillLabel(player.FirstFreeKey) : "?", context);
                    card.KindLine = SkillKindLine(player);
                    return card;
                case FloorRewardKind.BasicShot:
                    card = BasicShotTexts.Card(c.Shot, RunValues.BasicShot, player.Kit.BasicShotsPerMark, player.Kit.MaxMarks);
                    return card;
                case FloorRewardKind.Focus:
                    card = OfferTexts.ForFocus(c.Focus, context);
                    break;
                case FloorRewardKind.SkillUpgrade:
                {
                    int slot = player.SlotOf(c.Upgrade.Skill), key = player.KeyOfSkill(slot);
                    SkillDefinition now = slot >= 0 && slot < player.Skills.Count ? player.Skills[slot] : null;
                    card = OfferTexts.ForSkillUpgrade(c.Upgrade, now, bindings != null && key >= 0 ? bindings.SkillLabel(key) : "-", context);
                    break;
                }
                default:
                    card = OfferTexts.ForUpgrade(c.Stat, context);
                    break;
            }
            card.KindLine = c.KindWord;
            return card;
        }

        /// <summary>A card of three kinds at the right edge (the pop-up off): its name, and its words with its kind first; its ink by its tier.</summary>
        void MixedEdgeCard(int index, OfferContext context, InputBindings bindings, out string title, out string text, out UiInk ink)
        {
            OfferCard card = MixedCardFor(index, context ?? OfferContextNow(), bindings);
            title = card.Name;
            text = card.Body;
            ink = InterfaceRules.TierInk(card.Tiered, card.Tier);
        }

        /// <summary>A card of three kinds in the old look's plain row (the pop-up off): its number, kind and name, then its words.</summary>
        string MixedPlainText(int index)
        {
            InputBindings bindings = _player != null && _player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            OfferCard card = MixedCardFor(index, OfferContextNow(), bindings);
            return (index + 1).ToString(CultureInfo.InvariantCulture) + "   " + card.Name.ToUpperInvariant() + "\n" + card.Body;
        }

        /// <summary>The old look's row height for the cards of three kinds: as high as the longest text needs (nothing cut off).</summary>
        float MixedRowHeight(float rowWidth, GUIStyle style)
        {
            float most = 74f;
            if (style == null) return most;
            for (int i = 0; i < _mixedCards.Count; i++) most = Mathf.Max(most, Mathf.Ceil(style.CalcHeight(new GUIContent(MixedPlainText(i)), rowWidth)) + 2f);
            return most;
        }

        /// <summary>The heading of the floor reward of three kinds.</summary>
        string MixedHeading(int count) =>
            string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose one reward (1-{1} or click)", Floor, count);

        /// <summary>The ink of card <paramref name="index"/>'s kind line in the pop-up, a colour a kind.</summary>
        Color KindInk(int index, float light, InterfaceValues v)
        {
            FloorRewardKind kind = MixedRewardShown && index >= 0 && index < _mixedCards.Count ? _mixedCards[index].Kind : FloorRewardKind.Skill;
            switch (kind)
            {
                case FloorRewardKind.Skill: return InterfaceStyle.C(v.Teal, light);
                case FloorRewardKind.BasicShot: return InterfaceStyle.C(v.Gold, light);
                case FloorRewardKind.Focus: return new Color(s_KindFocus.r, s_KindFocus.g, s_KindFocus.b, light);
                default: return InterfaceStyle.C(v.White, light); // a skill upgrade or a stat step: one third kind a screen (grey read as switched off in the pictures)
            }
        }

        // ---------------- The log ----------------

        /// <summary>The recorder is told of the floor reward of three kinds: each card's name and kind, and the skill card's tier.</summary>
        void NoteMixedRewardOffer(PlayerCharacter player)
        {
            if (PlaySessionRecorder.Current == null) return;
            var names = new List<string>();
            var kinds = new List<string>();
            foreach (FloorRewardCard c in _mixedCards)
            {
                kinds.Add(c.LogWord);
                switch (c.Kind)
                {
                    case FloorRewardKind.Skill: names.Add(player.Skills[c.Slot].Name); break;
                    case FloorRewardKind.BasicShot: names.Add(c.Shot.Short); break;
                    case FloorRewardKind.Focus: names.Add(FocusModeRules.Name(c.Focus)); break;
                    case FloorRewardKind.SkillUpgrade: names.Add(c.Upgrade.FullName); break;
                    default: names.Add(c.Stat.Short); break;
                }
            }
            SkillTierOffer tier = OfferedTierAt(0);
            PlaySessionRecorder.Current.NoteMixedRewardOffered(Floor, names, kinds,
                                                               _offeredSkills.Count > 0 && RunValues.SkillTiers ? (tier != null ? tier.Word : UpgradeTiers.Word(UpgradeTier.Common)) : null);
        }

        /// <summary>
        /// What each basic-shot upgrade she holds did since the last time (work order 138: "logs its use in the session file (how
        /// often it fired, what it added)"): a line each, at a floor's reward and at the run's end; then counted afresh.
        /// </summary>
        void NoteShotUpgradeUse()
        {
            PlayerCharacter player = _player;
            if (player == null) return;
            BasicShotUpgradeTally tally = player.ShotTally;
            RunBasicShotUpgrades held = Modifiers.BasicShot;
            if (PlaySessionRecorder.Current != null && held.Any)
                foreach (BasicShotUpgradeKind k in held.Held)
                    PlaySessionRecorder.Current.NoteBasicShotUse(Floor, BasicShotTexts.Short(k, held.TierOf(k)), tally.Fired(k), tally.Added(k), BasicShotTexts.AddedUnit(k));
            tally.Clear();
        }
    }
}
