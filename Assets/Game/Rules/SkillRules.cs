using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Counts a cooldown. Time is passed in, in seconds.
    /// Charges (work order 015, 2026-11-20): it holds <see cref="MaxCharges"/> uses, 1 unless a skill says more (Slide:
    /// 2). Each use spends one; the spent ones come back one at a time, each the cooldown after the one before (the first
    /// the cooldown after the use that spent it, when none was recharging). It is ready while one is left. With one
    /// charge it is the plain cooldown it always was.
    /// </summary>
    public sealed class Cooldown
    {
        float _readyAt = float.NegativeInfinity; // when the charge recharging now comes back
        int _spent;
        float _seconds;                          // the recharge time of the charges still to come back
        int _max = 1;

        /// <summary>The uses it can hold, at least 1. Lowering it drops the spent charges above the new number.</summary>
        public int MaxCharges
        {
            get => _max;
            set
            {
                _max = Math.Max(1, value);
                if (_spent > _max) _spent = _max;
            }
        }

        void Settle(float now)
        {
            while (_spent > 0 && now >= _readyAt)
            {
                _spent--;
                if (_spent > 0) _readyAt += _seconds;
            }
        }

        /// <summary>Charges ready now.</summary>
        public int Charges(float now)
        {
            Settle(now);
            return _max - _spent;
        }

        public bool IsReady(float now) => Charges(now) > 0;

        /// <summary>Seconds until it is ready: 0 while a charge is left.</summary>
        public float Remaining(float now) => IsReady(now) ? 0f : _readyAt - now;

        /// <summary>Seconds until the next charge comes back: 0 when all are ready.</summary>
        public float NextCharge(float now)
        {
            Settle(now);
            return _spent > 0 ? _readyAt - now : 0f;
        }

        /// <summary>A use: spends a charge, and starts its recharge if none was recharging. With none left it starts again.</summary>
        public void Start(float now, float seconds)
        {
            Settle(now);
            _seconds = Math.Max(0f, seconds);
            if (_spent == 0 || _spent >= _max) _readyAt = now + _seconds;
            if (_spent < _max) _spent++;
        }

        /// <summary>
        /// Cuts a running cooldown by a share of what is left: 0.4 leaves 3 s of 5 s (decision 0005, 2026-11-19: what
        /// spending focus does). With charges, the recharge running now. Returns the seconds cut; a cooldown with nothing
        /// recharging is left alone and returns 0.
        /// </summary>
        public float Cut(float now, float share)
        {
            float left = NextCharge(now);
            if (left <= 0f) return 0f;
            float cut = left * Math.Max(0f, Math.Min(1f, share));
            _readyAt -= cut;
            return cut;
        }

        /// <summary>
        /// Takes this many seconds off the recharge running now, no more than it has left (work order 017: Quick Draw,
        /// Tied Trigger, Fresh Shells, Follow Through). Returns the seconds cut; 0 with nothing recharging.
        /// </summary>
        public float CutSeconds(float now, float seconds)
        {
            float left = NextCharge(now);
            if (left <= 0f || seconds <= 0f) return 0f;
            float cut = Math.Min(left, seconds);
            _readyAt -= cut;
            Settle(now);
            return cut;
        }

        /// <summary>The charge recharging now comes back at once (work order 017: Riposte). Nothing when all are ready.</summary>
        public void MakeReady(float now)
        {
            Settle(now);
            if (_spent == 0) return;
            _readyAt = now;
            Settle(now);
        }
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
