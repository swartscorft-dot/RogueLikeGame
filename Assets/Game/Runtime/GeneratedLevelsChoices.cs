using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The choices of a run of <see cref="GeneratedLevels"/>: the floor's reward, the chests and their upgrades, the elite room's
    /// skill upgrade, and a choice opened and closed (work order 081 moved them here from GeneratedLevels.cs, code unchanged).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        void OfferFloorReward()
        {
            PlayerCharacter player = FindPlayer();
            if (OfferMixedFloorReward(player)) return; // work order 138: the floor reward of three kinds, with its switch on (GeneratedLevelsMixedReward.cs)
            var equipped = new List<bool>();
            for (int slot = 0; slot < player.Skills.Count; slot++) equipped.Add(player.IsEquipped(slot));
            StrikeOutOfOffer(player, equipped); // work order 029: Not That One
            _offeredSkills.Clear();
            _offeredSkills.AddRange(FloorRewards.Offer(RunSeed ?? 0UL, Floor, equipped, RunValues.SkillsOffered));
            // Work order 138: six slots with the reward of before (its three kinds off), the slots full after floor 2: a skill
            // taken now would sit on no key, so nothing is offered, as when no skill is left. Eight slots: as before.
            if (player.Kit.SixSlots && player.FirstFreeKey < 0) _offeredSkills.Clear();
            if (_offeredSkills.Count == 0)
            {
                if (FocusChoiceDue) OfferFocusChoice(); // nothing left to offer, but the focus choice still comes (work order 025)
                else if (!EliteRewardFirst()) EnterFloor(Floor + 1); // nothing left to offer; an elite's drop lying first (work order 047)
                return;
            }
            DrawSkillTiers(player, 0); // work order 057: each card's tier, with the switch on (GeneratedLevelsSkillTiers.cs)
            NoteRewardOffer(player);
            OpenChoice(ChoiceKind.FloorReward);
        }

        /// <summary>
        /// Takes offered skill <paramref name="index"/> (0, 1 or 2): it is equipped in its own slot for the run, the game
        /// goes on, and she is put at the next floor's entrance with the health she has. False when no reward is offered.
        /// </summary>
        public bool PickReward(int index)
        {
            PickUpForAPick(ChoiceKind.FloorReward); // work order 042: its drop, if it lies, is picked up first
            if (_mixedOn) return PickMixedReward(index); // work order 138: a card of three kinds (GeneratedLevelsMixedReward.cs)
            if (Choice != ChoiceKind.FloorReward || index < 0 || index >= _offeredSkills.Count) return false;
            CardPicked(index); // work order 045: its card flashes as it goes
            PlayerCharacter player = FindPlayer();
            int slot = _offeredSkills[index];
            player.EquipSkill(slot);
            string name = player.Skills[slot].Name;
            _skillsTaken.Add(name);
            TookRewardTier(index, name); // work order 057: a rare or epic card's step and change taken with it; the record and the recorder told
            // A breath between floors (work order 023): a share of her maximum back as the reward is taken.
            if (_health != null)
            {
                int healed = _health.Heal(FloorHeals.Amount(_health.Max, RunValues.FloorHeal));
                Record?.FloorHealed(Floor, healed);
                PlaySessionRecorder.Current?.NoteFloorHeal(Floor, healed, _health.Current);
            }
            if (LabTookTheReward()) return true; // work order 084: in the room lab she stays in the room
            // Work order 025: after floor 1's reward the focus choice comes up, still paused, before floor 2.
            if (FocusChoiceDue)
            {
                OfferFocusChoice();
                return true;
            }
            CloseChoice();
            EnterFloor(Floor + 1);
            return true;
        }

        /// <summary>The chests of this floor, in the order of the rooms.</summary>
        public List<GeneratedLevel.PlacedProp> Chests() =>
            _level == null ? new List<GeneratedLevel.PlacedProp>() : _level.Props.Where(p => p.Prop.Model == PropModel.Chest).ToList();

        /// <summary>The chest she can open now: unopened, within reach, in a run; null if none.</summary>
        public GeneratedLevel.PlacedProp ChestInReach()
        {
            if (!InRun || _level == null || _player == null || Choice != ChoiceKind.None) return null;
            foreach (GeneratedLevel.PlacedProp chest in _level.Props) // no list made per call (work order 026)
            {
                if (chest.Prop.Model != PropModel.Chest || _chestsOpened.Contains(chest.Room.Order) || chest.Object == null) continue;
                Vector3 c = chest.Object.transform.position, p = _player.Position;
                if (new Vector2(c.x - p.x, c.z - p.z).magnitude <= RunValues.ChestReach) return chest;
            }
            return null;
        }

        /// <summary>
        /// The chest the prompt shows over now: <see cref="ChestInReach"/>, found once a frame and not once per GUI event
        /// (the review of 2026-11-20, work order 026).
        /// </summary>
        public GeneratedLevel.PlacedProp ChestPrompt
        {
            get
            {
                if (_promptFrame != Time.frameCount)
                {
                    _promptFrame = Time.frameCount;
                    _promptChest = ChestInReach();
                }
                return _promptChest;
            }
        }

        int _promptFrame = -1;
        GeneratedLevel.PlacedProp _promptChest;

        /// <summary>Opens the chest in reach, if any (the interact key). True when one opened.</summary>
        public bool OpenChest() => OpenChest(ChestInReach());

        /// <summary>
        /// Opens this chest, once, in a run: its upgrades are offered (three, work order 065; two with the stats off) and the game pauses. False for a chest already
        /// opened, outside a run, or while another choice is up.
        /// </summary>
        public bool OpenChest(GeneratedLevel.PlacedProp chest)
        {
            if (chest == null || !InRun || Choice != ChoiceKind.None || Paused || _chestsOpened.Contains(chest.Room.Order)) return false; // not under the pause menu (work order 026)
            PlayerCharacter player = FindPlayer();
            var equipped = new List<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot)) equipped.Add(player.Skills[slot].Name);
            int extra = FirstChestExtra; // work order 029: Deep Chests, asked before this chest counts
            _chestsOpened.Add(chest.Room.Order);
            _offeredUpgrades.Clear();
            // Work order 025: no Mend at full health, no row the run has taken its most times.
            bool fullHealth = _health == null || _health.Current >= _health.Max;
            _offeredUpgrades.AddRange(ChestUpgrades.Offer(RunSeed ?? 0UL, Floor, chest.Room.Order, equipped, RunValues, Modifiers, fullHealth, extra));
            if (_offeredUpgrades.Count == 0) return true;
            _openChest = chest;
            OpenChoice(ChoiceKind.Chest);
            return true;
        }

        /// <summary>Takes offered upgrade <paramref name="index"/> (0 to 2; 3 with Deep Chests) into the run's upgrades; the game goes on. False with no chest open.</summary>
        public bool PickUpgrade(int index)
        {
            if (Choice != ChoiceKind.Chest || index < 0 || index >= _offeredUpgrades.Count) return false;
            CardPicked(index); // work order 045
            Upgrade u = _offeredUpgrades[index];
            Modifiers.Take(u);
            FindPlayer()?.RefreshDashCharges(); // work order 065: a second dash is held at once
            Record?.TakeUpgrade(u.Short, u.Tiered ? u.Tier : UpgradeTier.Common); // work order 078: with its tier, for the end screen
            if (_health != null)
            {
                _health.RefreshMax();
                if (u.Heal > 0) _health.Heal(u.Heal);
            }
            PlaySessionRecorder.Current?.NoteChestOpened(Floor, _openChest != null ? _openChest.Room.Order : -1,
                                                         _offeredUpgrades.Select(o => o.Short).ToList(), u.Short,
                                                         u.Tiered ? u : null, u.Tiered ? StatsLine() : null); // work order 050: its stat, tier and size, and her stats after
            _openChest = null;
            CloseChoice();
            return true;
        }

        /// <summary>
        /// The elite room was cleared, in a run (work order 017): the skill upgrade choice, once a floor; none left to
        /// offer, a full heal instead, said on screen.
        /// </summary>
        void EliteRoomFinished()
        {
            EliteRoomDone = true;
            PlayerCharacter player = FindPlayer();
            PlaySessionRecorder.Current?.NoteEliteCleared(Floor, EliteRoomEntered ? Time.time - _eliteEnteredAt : 0f, _health != null ? _health.Current : 0);
            var had = new HashSet<string>();
            for (int slot = 0; slot < player.Skills.Count; slot++)
                if (player.IsEquipped(slot) && player.Skills[slot] != null) had.Add(player.Skills[slot].Name);
            _offeredSkillUpgrades.Clear();
            _offeredSkillUpgrades.AddRange(SkillUpgradeOffer.Offer(RunSeed ?? 0UL, Floor, UpgradeTableNow, had, SkillUpgrades.Taken,
                                                                   UpgradeValues.Offered + Modifiers.Meta.EliteOffersExtra)); // work order 029: Knight's Trophy
            SkillUpgradeOffers++;
            BetweenRunsEliteCleared(); // work order 029
            PlaySessionRecorder.Current?.NoteSkillUpgradeOffered(Floor, _offeredSkillUpgrades.Select(u => u.FullName + " (" + u.KindWord + ")").ToList(),
                                                                 _offeredSkillUpgrades.Count == 0);
            if (_offeredSkillUpgrades.Count == 0)
            {
                if (_health != null) _health.RestoreFull();
                Say("ELITE ROOM CLEARED   no upgrade left to take: fully healed");
                return;
            }
            OpenChoice(ChoiceKind.SkillUpgrade);
        }

        /// <summary>
        /// Takes offered skill upgrade <paramref name="index"/> (0, 1 or 2) into the run's upgrades (work order 017): the
        /// skill is read through it from now on, and the game goes on. False with no skill upgrade offered.
        /// </summary>
        public bool PickSkillUpgrade(int index)
        {
            PickUpForAPick(ChoiceKind.SkillUpgrade); // work order 042: its drop, if it lies, is picked up first
            if (Choice != ChoiceKind.SkillUpgrade || index < 0 || index >= _offeredSkillUpgrades.Count) return false;
            CardPicked(index); // work order 045
            SkillUpgradeDefinition u = _offeredSkillUpgrades[index];
            SkillUpgrades.Take(u);
            Record?.TakeSkillUpgrade(u.FullName);
            PlaySessionRecorder.Current?.NoteSkillUpgradeTaken(Floor, u.FullName, u.KindWord);
            if (!FloorRewardFollows()) CloseChoice(); // work order 042: the floor's reward, when it was picked up with this one
            return true;
        }

        /// <summary>For tests (work order 017): kills every enemy of this floor's elite room, so it is cleared on the next frame.</summary>
        public void FinishEliteRoomForTests()
        {
            if (_level == null) return;
            FinishedForTests(true); // work order 042: its choice comes up as before, without a drop
            foreach (Enemy e in _level.EliteRoomEnemies)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(100000000);
        }

        void Say(string line)
        {
            _notice = line;
            _noticeUntil = Time.unscaledTime + NoticeSeconds;
            Debug.Log(line);
        }

        /// <summary>For tests: kills every enemy of this floor's exit room, so the floor is finished on the next frame.</summary>
        public void FinishFloorForTests()
        {
            if (_level == null) return;
            FinishedForTests(false); // work order 042: its choice comes up as before, without a drop
            foreach (Enemy e in _level.ExitEnemies)
                if (e != null && !e.IsDead) e.GetComponent<Damageable>().TakeHit(1000000);
        }

        void OpenChoice(ChoiceKind kind)
        {
            if (LeaveADrop(kind)) return; // work order 042: what she earns by killing lands on the ground first (GeneratedLevelsDrops.cs)
            if (Choice == ChoiceKind.None) _timeScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f; // a hit stop's 0 is not kept
            Choice = kind;
            Time.timeScale = 0f;
            PlayerInputDriver.HeldByChoice = true;
            GuardTheChoice(); // work order 042: no key or click picks for a moment, and only a fresh press then
        }

        void CloseChoice()
        {
            if (Choice == ChoiceKind.None) return;
            // A chest whose choice closes without a pick (an end screen) is not used up (the second reviews of 2026-11-20,
            // work order 033). A pick lets go of the chest first.
            if (Choice == ChoiceKind.Chest && _openChest != null) _chestsOpened.Remove(_openChest.Room.Order);
            Choice = ChoiceKind.None;
            _offeredSkills.Clear();
            _offeredSkillTiers.Clear(); // work order 057
            _offeredUpgrades.Clear();
            _offeredSkillUpgrades.Clear();
            _openChest = null;
            Time.timeScale = _timeScaleBefore;
            PlayerInputDriver.HeldByChoice = false;
        }
    }
}
