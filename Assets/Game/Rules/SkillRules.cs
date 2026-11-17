using System;

namespace Roguelike.Rules
{
    /// <summary>Counts a cooldown. Time is passed in, in seconds.</summary>
    public sealed class Cooldown
    {
        float _readyAt = float.NegativeInfinity;

        public bool IsReady(float now) => now >= _readyAt;
        public float Remaining(float now) => now >= _readyAt ? 0f : _readyAt - now;
        public void Start(float now, float seconds) => _readyAt = now + Math.Max(0f, seconds);
    }

    /// <summary>What a skill hits and how hard. Targets are circles on the ground; a touched edge counts.</summary>
    public static class SkillRules
    {
        /// <summary>The point itself if it is within range of the origin, else the point at range in its direction.</summary>
        public static GroundPoint ClampToRange(GroundPoint origin, GroundPoint point, float range)
        {
            GroundPoint offset = point - origin;
            float length = offset.Length;
            if (length <= range || length < 1e-5f) return point;
            return origin + offset * (range / length);
        }

        public static bool InCircle(GroundPoint center, float radius, GroundPoint target, float targetRadius) =>
            center.DistanceTo(target) <= radius + targetRadius;

        /// <summary>
        /// A wedge from the origin toward the aim, <paramref name="range"/> long and
        /// <paramref name="angleDegrees"/> wide in all. With no direction (aim equals origin) it hits nothing.
        /// </summary>
        public static bool InCone(GroundPoint origin, GroundPoint aim, float range, float angleDegrees,
                                  GroundPoint target, float targetRadius)
        {
            GroundPoint toAim = aim - origin;
            float aimLength = toAim.Length;
            if (aimLength < 1e-5f) return false;

            GroundPoint toTarget = target - origin;
            float distance = toTarget.Length;
            if (distance - targetRadius > range) return false;
            if (distance <= targetRadius) return true; // standing on it

            double cos = toTarget.Dot(toAim) / (distance * aimLength);
            double offAxis = Math.Acos(Math.Max(-1.0, Math.Min(1.0, cos)));
            double reach = Math.Asin(Math.Min(1.0, targetRadius / distance)); // the target's own width, as an angle
            return offAxis <= angleDegrees * 0.5 * Math.PI / 180.0 + reach;
        }

        /// <summary>The centre of a Circle skill: the aimed point, pulled in to the skill's range.</summary>
        public static GroundPoint CircleCenter(SkillDefinition skill, GroundPoint origin, GroundPoint aim) =>
            ClampToRange(origin, aim, skill.Range);

        /// <summary>Does one hit of this skill, used from the origin toward the aim, touch the target?</summary>
        public static bool Hits(SkillDefinition skill, GroundPoint origin, GroundPoint aim,
                                GroundPoint target, float targetRadius)
        {
            switch (skill.Shape)
            {
                case SkillShape.Circle:
                    return InCircle(CircleCenter(skill, origin, aim), skill.Radius, target, targetRadius);
                case SkillShape.Cone:
                    return InCone(origin, aim, skill.Range, skill.Angle, target, targetRadius);
                default:
                    return LineShot.Hits(origin, aim, skill.Range, skill.HalfWidth, target, targetRadius);
            }
        }

        /// <summary>
        /// Damage of one hit. A Charge skill deals MinChargeDamage of its damage with no charge
        /// and all of it at full charge, in a straight line between. Other skills ignore the charge.
        /// </summary>
        public static int Damage(SkillDefinition skill, float charge)
        {
            if (skill.Input != SkillInput.Charge) return skill.Damage;
            float c = charge < 0f ? 0f : charge > 1f ? 1f : charge;
            float min = skill.MinChargeDamage < 0f ? 0f : skill.MinChargeDamage > 1f ? 1f : skill.MinChargeDamage;
            return (int)Math.Round(skill.Damage * (min + (1f - min) * c));
        }
    }
}
