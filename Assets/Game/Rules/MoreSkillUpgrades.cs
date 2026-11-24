using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The two small new kinds of change of work order 111's rows (2026-11-24, every skill three upgrade choices), with no engine in
    /// them: Gun Slide's basic shot during the slide (<see cref="SkillDefinition.SlideShotDamage"/>) and Marked Opening's marks on a
    /// counter (<see cref="SkillDefinition.CounterMarks"/>). The other seven rows change fields the game already reads.
    /// </summary>
    public static class MoreUpgradeRules
    {
        /// <summary>A basic shot's damage when it hits during a slide that deals <paramref name="share"/> more (0.5: half as much again), rounded half away from zero; 0 or less: as it is.</summary>
        public static int SlideShot(int damage, float share) =>
            share <= 0f || damage <= 0 ? damage : (int)Math.Round(damage * (1.0 + share), MidpointRounding.AwayFromZero);
    }
}
