using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How far a straight line along the ground runs from <paramref name="from"/> toward <paramref name="to"/> before it
    /// meets cover, in metres: the whole distance between them when nothing stands in the way. The game answers it with
    /// a physics query against the level's cover; tests answer it with walls of their own.
    /// </summary>
    public delegate float CoverLine(GroundPoint from, GroundPoint to);

    /// <summary>
    /// Cover stops her shots and skills (Kristian, 2026-11-19: "Skudd skal stoppes av vegg og søyler", shots are to be
    /// stopped by walls and pillars). Cover is a generated level's walls, at any height, and its props at least
    /// <see cref="PropValues.CoverHeight"/> tall (<see cref="LevelProp.IsCover"/>); she shoots over the lower ones.
    /// Enemies, dummies and she herself are never cover. The geometry of which targets a shape covers stays in
    /// <see cref="SkillRules"/> and <see cref="LineShot"/>; this adds what cover takes away from it:
    ///
    /// - A line shot ends at the first cover its centre line meets; it reaches nothing past that point
    ///   (<see cref="LineShot.Hits(GroundPoint, GroundPoint, float, float, GroundPoint, float, float)"/>). A shot whose
    ///   side grazes a pillar is not stopped. A line that pierces still stops there.
    /// - A cone hits a target in it only along a clear straight line from her to the target's centre.
    /// - A circle placed at a point lands on the aimed point, or, with cover between her and it, <see cref="ShortOfCover"/>
    ///   before the cover; it hits a target in it only along a clear line from its centre to the target's centre.
    ///
    /// With no cover query (null) every rule is as it was before cover.
    /// </summary>
    public static class CoverRules
    {
        /// <summary>How far before cover a circle that cover stops lands, in metres.</summary>
        public const float ShortOfCover = 0.3f;

        const float Slack = 1e-3f;

        /// <summary>
        /// Where along a line shot cover stops it: metres from the origin, or <paramref name="range"/> when nothing stops
        /// it within its range (or there is no cover query, or no direction).
        /// </summary>
        public static float LineStop(GroundPoint origin, GroundPoint aim, float range, CoverLine cover)
        {
            if (cover == null || range <= 0f) return range;
            GroundPoint toAim = aim - origin;
            float length = toAim.Length;
            if (length < 1e-5f) return range;
            GroundPoint end = origin + toAim * (range / length);
            float clear = cover(origin, end);
            return clear < range - Slack ? Math.Max(0f, clear) : range;
        }

        /// <summary>Is there a clear straight line from one point to the other? Always, with no cover query.</summary>
        public static bool Clear(GroundPoint from, GroundPoint to, CoverLine cover)
        {
            if (cover == null) return true;
            float distance = from.DistanceTo(to);
            return distance < Slack || cover(from, to) >= distance - Slack;
        }

        /// <summary>
        /// Where a circle skill lands: the aimed point pulled in to its range (<see cref="SkillRules.CircleCenter"/>), or,
        /// when cover stands between her and that point, <see cref="ShortOfCover"/> before the cover (never behind her).
        /// </summary>
        public static GroundPoint CircleCenter(SkillDefinition skill, GroundPoint origin, GroundPoint aim, CoverLine cover)
        {
            GroundPoint wanted = SkillRules.CircleCenter(skill, origin, aim);
            if (cover == null) return wanted;
            GroundPoint offset = wanted - origin;
            float length = offset.Length;
            if (length < 1e-5f) return wanted;
            float clear = cover(origin, wanted);
            if (clear >= length - Slack) return wanted;
            return origin + offset * (Math.Max(0f, clear - ShortOfCover) / length);
        }
    }

    /// <summary>
    /// One hit of a skill, used from <see cref="Origin"/> toward <see cref="Aim"/>, with the cover it meets worked out
    /// once (<see cref="CoverRules"/>): where a line stops and where a circle lands. <see cref="Hits"/> then answers for
    /// each target. With no cover query it is <see cref="SkillRules.Hits"/> exactly.
    /// </summary>
    public readonly struct SkillArea
    {
        public readonly SkillDefinition Skill;
        public readonly GroundPoint Origin;
        public readonly GroundPoint Aim;
        /// <summary>A line's length: its range, or less where cover stops it. The range for the other shapes.</summary>
        public readonly float Stop;
        /// <summary>Where a circle lands; the origin for the other shapes.</summary>
        public readonly GroundPoint Centre;
        readonly CoverLine _cover;

        public SkillArea(SkillDefinition skill, GroundPoint origin, GroundPoint aim, CoverLine cover)
        {
            Skill = skill;
            Origin = origin;
            Aim = aim;
            _cover = cover;
            Stop = skill.Shape == SkillShape.Line ? CoverRules.LineStop(origin, aim, skill.Range, cover) : skill.Range;
            Centre = skill.Shape == SkillShape.Circle ? CoverRules.CircleCenter(skill, origin, aim, cover) : origin;
        }

        /// <summary>True when cover cut a line short of its range.</summary>
        public bool Stopped => Skill.Shape == SkillShape.Line && Stop < Skill.Range;

        /// <summary>Does this hit touch the target, with the cover taken into account?</summary>
        public bool Hits(GroundPoint target, float targetRadius)
        {
            switch (Skill.Shape)
            {
                case SkillShape.Circle:
                    return SkillRules.InCircle(Centre, Skill.Radius, target, targetRadius) && CoverRules.Clear(Centre, target, _cover);
                case SkillShape.Cone:
                    return SkillRules.InCone(Origin, Aim, Skill.Range, Skill.Angle, target, targetRadius) && CoverRules.Clear(Origin, target, _cover);
                default:
                    return LineShot.Hits(Origin, Aim, Skill.Range, Skill.HalfWidth, target, targetRadius, Stop);
            }
        }
    }
}
