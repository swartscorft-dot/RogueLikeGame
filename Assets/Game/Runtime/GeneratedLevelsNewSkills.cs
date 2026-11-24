using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The runs' part of work order 109 (the three new skills): the elite room's table as the switches have it. Her list of skills
    /// follows <see cref="RunValues.NewSkills"/> through <see cref="PlayerCharacter.NewSkillsOn"/>, set when she is found and when a
    /// run starts.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// The elite room's table (<see cref="UpgradeValues"/>) as the run's switches have it (<see cref="SkillUpgradeTable.Switched"/>):
        /// the table itself with them on. Read when an offer or a floor reward's tiers are drawn.
        /// </summary>
        public SkillUpgradeDefinition[] UpgradeTableNow => SkillUpgradeTable.Switched(UpgradeValues.Table, RunValues);
    }
}
