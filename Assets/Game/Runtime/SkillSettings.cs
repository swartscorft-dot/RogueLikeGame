using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The skills on the skill keys, in key order (Q, W, E, R, D), in one asset.
    /// The game reads them live, so a change in the Inspector applies to the next use of a skill and is kept.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Skill Settings", fileName = "Skills")]
    public sealed class SkillSettings : ScriptableObject
    {
        public SkillDefinition[] Skills = SkillDefaults.Create();

        [ContextMenu("Reset to the G3 start values")]
        void ResetToStartValues() => Skills = SkillDefaults.Create();
    }
}
