using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// For the tests written before work order 138 that pin what it changes (the eight keys of work order 015, a floor reward
    /// of skills only and the focus screen after floor 1): its switches turned off for that test alone, so the test still
    /// proves the game of before. Nothing is written to the settings asset: the skills' switch is set on a copy.
    /// </summary>
    public static class KitSwitchesForTests
    {
        /// <summary>
        /// Eight skill slots again (<see cref="KitValues.SixSlots"/> off) on a copy of her skill settings, made hers; the
        /// copy is returned so the test can destroy it. Call it before the bar is first read (or call
        /// <see cref="PlayerCharacter.EquipStartSkills"/> after it): the loadout is made again when the switch differs.
        /// </summary>
        public static SkillSettings EightSlots(PlayerCharacter player)
        {
            // The skills she had (the practice room hands her every one) are had again, in list order, on the eight keys.
            var had = new List<int>();
            for (int s = 0; s < player.Skills.Count; s++) if (player.IsEquipped(s)) had.Add(s);
            SkillSettings copy = Object.Instantiate(player.SkillSet);
            copy.Kit.SixSlots = false;
            player.SkillSet = copy;
            player.EquipStartSkills();
            foreach (int s in had) player.EquipSkill(s);
            return copy;
        }

        /// <summary>
        /// Every skill had, taken in list order after the start four (as the practice room hands them out): with eight slots
        /// the first eight sit on the keys of their places. For a test that switched the slots after the bar was made.
        /// </summary>
        public static void EverySkill(PlayerCharacter player)
        {
            player.EquipStartSkills();
            for (int s = 0; s < player.Skills.Count; s++) player.EquipSkill(s);
        }

        /// <summary>The floor reward of before (<see cref="RunValues.MixedFloorReward"/> off): three skills, and the focus screen after floor 1.</summary>
        public static void SkillsOnlyReward(GeneratedLevels levels) => levels.RunValues.MixedFloorReward = false;
    }
}
