using System.Collections.Generic;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The skills on the skill keys, in key order (Q W E R, S D F G), and the identity gauge's numbers, in one asset.
    /// The game reads them live, so a change in the Inspector applies to the next use of a skill and is kept.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Skill Settings", fileName = "Skills")]
    public sealed class SkillSettings : ScriptableObject
    {
        public SkillDefinition[] Skills = SkillDefaults.Create();
        public IdentityValues Identity = new IdentityValues();

        [ContextMenu("Reset to the start values")]
        void ResetToStartValues()
        {
            Skills = SkillDefaults.Create();
            Identity = new IdentityValues();
        }

        /// <summary>
        /// Brings an older asset up to the current set of skills without touching what is tuned:
        /// a skill that is already here, by name, keeps its values; a missing one is added with its start values;
        /// the order becomes the key order. A skill with a name of its own is kept, at the end.
        /// Returns true if anything changed.
        /// </summary>
        public bool EnsureStartSkills()
        {
            SkillDefinition[] start = SkillDefaults.Create();
            var have = (Skills ?? new SkillDefinition[0]).Where(s => s != null).ToList();
            var result = new List<SkillDefinition>();
            foreach (SkillDefinition s in start)
            {
                SkillDefinition mine = have.FirstOrDefault(h => h.Name == s.Name);
                result.Add(mine ?? s);
                if (mine != null) have.Remove(mine);
            }
            result.AddRange(have);

            bool changed = Skills == null || Skills.Length != result.Count || !Skills.SequenceEqual(result);
            if (Identity == null)
            {
                Identity = new IdentityValues();
                changed = true;
            }
            if (changed) Skills = result.ToArray();
            return changed;
        }
    }
}
