using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>A target as the rules see it: where it stands on the ground, how wide it is, whether it lives.</summary>
    public readonly struct TargetInfo
    {
        public readonly GroundPoint Position;
        public readonly float Radius;
        public readonly bool Alive;

        public TargetInfo(GroundPoint position, float radius, bool alive)
        {
            Position = position;
            Radius = radius;
            Alive = alive;
        }
    }

    /// <summary>Straight shots along the ground: line skills, and the basic shot toward the cursor.</summary>
    public static class LineShot
    {
        /// <summary>
        /// Does a straight shot from <paramref name="origin"/> toward <paramref name="aim"/>,
        /// <paramref name="range"/> long and 2 * <paramref name="halfWidth"/> wide, touch a circle?
        /// The shot reaches a target whose edge it touches, so it reaches up to
        /// halfWidth + targetRadius past its range. If aim equals origin the shot has no direction and hits nothing.
        /// </summary>
        public static bool Hits(GroundPoint origin, GroundPoint aim, float range, float halfWidth,
                                GroundPoint target, float targetRadius)
        {
            GroundPoint toAim = aim - origin;
            float aimLength = toAim.Length;
            if (aimLength < 1e-5f) return false;
            GroundPoint dir = toAim * (1f / aimLength);

            float along = (target - origin).Dot(dir);
            if (along < 0f) along = 0f;
            if (along > range) along = range;

            GroundPoint closest = origin + dir * along;
            return (target - closest).Length <= halfWidth + targetRadius;
        }

        /// <summary>
        /// The first living target a straight shot touches, as <see cref="Hits"/> measures touching: the one whose
        /// centre lies nearest along the line. Index into <paramref name="targets"/>, or -1 for a miss. The shot
        /// stops at that target: it does not pierce. On a tie the first in the list wins.
        /// </summary>
        public static int FirstHit(GroundPoint origin, GroundPoint aim, float range, float halfWidth,
                                   IReadOnlyList<TargetInfo> targets)
        {
            GroundPoint toAim = aim - origin;
            float aimLength = toAim.Length;
            if (aimLength < 1e-5f) return -1;
            GroundPoint dir = toAim * (1f / aimLength);

            int best = -1;
            float bestAlong = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                TargetInfo t = targets[i];
                if (!t.Alive || !Hits(origin, aim, range, halfWidth, t.Position, t.Radius)) continue;
                float along = (t.Position - origin).Dot(dir);
                if (along < bestAlong)
                {
                    best = i;
                    bestAlong = along;
                }
            }
            return best;
        }

        /// <summary>
        /// The unit direction of a shot aimed at a point: toward it, or along <paramref name="facing"/> when the point
        /// lies within <paramref name="minDistance"/> of the origin (a click on the character shoots where it faces).
        /// </summary>
        public static GroundPoint Direction(GroundPoint origin, GroundPoint point, GroundPoint facing, float minDistance = 0.1f)
        {
            GroundPoint d = point - origin;
            float length = d.Length;
            if (length >= minDistance && length > 1e-5f) return d * (1f / length);
            float f = facing.Length;
            return f > 1e-5f ? facing * (1f / f) : new GroundPoint(0f, 1f);
        }
    }
}
