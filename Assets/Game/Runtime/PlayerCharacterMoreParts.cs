using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Work order 110's two new knobs on her basic shot (the five more parts, behind <see cref="MetaValues.MoreParts"/>), as part
    /// of <see cref="PlayerCharacter"/>; both are read through her pistols' parts' one place (<see cref="AttachmentEffects"/>):
    /// - the Reflex Sight's crit chance on a basic shot (<see cref="AttachmentEffects.ShotCritChance"/>), only with her stats on,
    ///   where anything crits; the skills' crits are hers as before;
    /// - the Quick-Draw Grip's first shot after a dash or a skill (<see cref="AttachmentEffects.ShotStartup"/>): the first basic
    ///   shot started after a dash or a skill started (any skill, a recast too) since the last basic shot has the shorter startup;
    ///   the clip enters as much nearer its firing moment (<see cref="ActionLeadTime"/>), and the recovery after it is the
    ///   feel's, so the shots after it keep their rhythm.
    /// Neither draws anything. Without a part that turns them, every shot is as before, field for field.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>Basic shots started, counted here only to tell the others apart from <see cref="ActionsStarted"/>.</summary>
        int _basicShotsStarted;
        /// <summary>The dashes and skills started when the last basic shot started.</summary>
        int _othersAtLastShot;

        /// <summary>True for the shot just started (after its own count) when a dash or a skill started since the last basic shot.</summary>
        public bool LastShotWasFirstAfterDashOrSkill { get; private set; }

        /// <summary>Basic shots that went off with a part's quicker first-shot startup (work order 110). For tests and the log.</summary>
        public int QuickDraws { get; private set; }

        /// <summary>
        /// The startup of the basic shot starting now (called once a shot, after <see cref="ActionsStarted"/> counted it): the
        /// feel's, or her pistols' parts' for the first after a dash or a skill.
        /// </summary>
        float BasicShotStartupNow(FeelValues feel)
        {
            _basicShotsStarted++;
            int others = DashesStarted + ActionsStarted - _basicShotsStarted;
            LastShotWasFirstAfterDashOrSkill = others != _othersAtLastShot;
            _othersAtLastShot = others;
            if (Modifiers == null) return feel.BasicShotStartup;
            float startup = Modifiers.Weapon.ShotStartup(feel.BasicShotStartup, LastShotWasFirstAfterDashOrSkill);
            if (startup != feel.BasicShotStartup) QuickDraws++;
            return startup;
        }

        /// <summary>The chance a hit of hers crits (her stats on): hers, and for a basic shot her pistols' parts' too.</summary>
        float CritChanceOf(bool basicShot) => basicShot ? Modifiers.Weapon.ShotCritChance(Modifiers.Stats.CritChance) : Modifiers.Stats.CritChance;
    }
}
