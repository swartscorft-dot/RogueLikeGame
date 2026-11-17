using System.Collections.Generic;

namespace Roguelike.Rules
{
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

    /// <summary>Which target a basic attack or a click picks. Distances are measured to the target's edge.</summary>
    public static class Targeting
    {
        /// <summary>Distance from a point to a target's edge. Zero inside the target.</summary>
        public static float Gap(GroundPoint from, GroundPoint target, float targetRadius)
        {
            float gap = from.DistanceTo(target) - targetRadius;
            return gap < 0f ? 0f : gap;
        }

        public static bool InRange(GroundPoint from, GroundPoint target, float targetRadius, float range) =>
            Gap(from, target, targetRadius) <= range;

        /// <summary>
        /// Index of the living target whose edge is nearest to <paramref name="from"/> and within
        /// <paramref name="range"/>, or -1. On a tie the first in the list wins.
        /// </summary>
        public static int NearestInRange(GroundPoint from, IReadOnlyList<TargetInfo> targets, float range)
        {
            int best = -1;
            float bestGap = float.PositiveInfinity;
            for (int i = 0; i < targets.Count; i++)
            {
                TargetInfo t = targets[i];
                if (!t.Alive) continue;
                float gap = Gap(from, t.Position, t.Radius);
                if (gap <= range && gap < bestGap)
                {
                    best = i;
                    bestGap = gap;
                }
            }
            return best;
        }
    }
}
