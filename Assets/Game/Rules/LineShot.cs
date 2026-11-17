namespace Roguelike.Rules
{
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
    }
}
