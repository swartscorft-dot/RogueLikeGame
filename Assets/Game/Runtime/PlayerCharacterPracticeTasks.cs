using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The first run's teaching room (work order 133's follow-up; <see cref="PracticeTasks"/>): the skill a clean-press task is about
    /// is ready when the task begins (the producer's decision: she does not wait out its cooldown there). A new file of this partial
    /// class: nothing else here changes.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>The slot's skill ready now: its charge recharging comes back at once (<see cref="Rules.Cooldown.MakeReady"/>). Nothing when it is.</summary>
        public void MakeSkillReady(int slot)
        {
            if (slot < 0 || slot >= Skills.Count) return;
            CooldownFor(slot).MakeReady(Time.time);
        }
    }
}
