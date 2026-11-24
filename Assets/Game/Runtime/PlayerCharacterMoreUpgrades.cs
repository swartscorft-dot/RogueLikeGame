using System.Collections.Generic;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Work order 111 (2026-11-24, every skill three upgrade choices) in <see cref="PlayerCharacter"/>: the one new hook its rows need
    /// here, Gun Slide's (<see cref="SkillDefinition.SlideShotDamage"/>): a basic shot that hits while Slide moves her deals that share
    /// more. Marked Opening's marks on a counter are read where a counter is (<see cref="SkillDefinition.CounterMarks"/>); the other
    /// seven rows change fields the game read before.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>A basic shot's damage, raised by the slide moving her now if its skill says so (Gun Slide); as it is otherwise.</summary>
        int SlideShotDamageNow(int damage)
        {
            if (!_keyMoving || _keyMoveSlot < 0) return damage;
            IReadOnlyList<SkillDefinition> skills = Skills;
            if (_keyMoveSlot >= skills.Count || skills[_keyMoveSlot] == null) return damage;
            return MoreUpgradeRules.SlideShot(damage, skills[_keyMoveSlot].SlideShotDamage);
        }
    }
}
