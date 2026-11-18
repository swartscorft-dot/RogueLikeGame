using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Whether a basic shot turns the whole body, or only the upper body. A target within the front angle,
    /// to either side of where the body faces, is shot with a twist of the spine and the arms, and the legs
    /// and hips keep their direction. A target outside it makes the whole body turn to it.
    /// Directions are on the ground; their length does not matter.
    /// </summary>
    public static class UpperBodyAim
    {
        /// <summary>Degrees from the body's facing to the target, clockwise seen from above (to the right is positive), -180 to 180.</summary>
        public static float TwistAngle(GroundPoint facing, GroundPoint toTarget)
        {
            double cross = facing.Z * toTarget.X - facing.X * toTarget.Z;
            double dot = facing.X * toTarget.X + facing.Z * toTarget.Z;
            if (Math.Abs(cross) < 1e-9 && Math.Abs(dot) < 1e-9) return 0f;
            return (float)(Math.Atan2(cross, dot) * 180.0 / Math.PI);
        }

        /// <summary>
        /// True when the body must turn to face the target: it lies more than <paramref name="frontAngle"/>
        /// degrees to one side of the facing. A target on the character itself, with no direction, never turns it.
        /// </summary>
        public static bool BodyMustTurn(GroundPoint facing, GroundPoint toTarget, float frontAngle)
        {
            if (toTarget.X * toTarget.X + toTarget.Z * toTarget.Z < 1e-12) return false;
            if (facing.X * facing.X + facing.Z * facing.Z < 1e-12) return true;
            return Math.Abs(TwistAngle(facing, toTarget)) > frontAngle;
        }
    }

    /// <summary>
    /// The speed of a character who shoots on the move: an attack command while it walks on a move order keeps
    /// the move. When the shot starts, the speed eases down from whatever it is to the slow share; once the shot
    /// has gone off and the slow-down is over, it eases back up to full. Both ways follow a smoothstep, so there is
    /// no kink at either end. A new shot on the move eases down again from the speed at that moment.
    /// </summary>
    public static class MovingShot
    {
        /// <summary>
        /// The share of run speed, <paramref name="sinceStart"/> seconds after a shot on the move started.
        /// <paramref name="fromShare"/>: the share when it started. <paramref name="firedAfter"/>: seconds from the
        /// start to the shot going off, or negative while it has not. The slow-down takes <paramref name="slowdown"/>
        /// seconds; the speed-up starts when the shot has gone off and the slow-down is done, and takes
        /// <paramref name="recovery"/> seconds.
        /// </summary>
        public static float SpeedShare(float fromShare, float sinceStart, float firedAfter, float slowShare, float slowdown, float recovery)
        {
            float slow = Clamp01(slowShare);
            float from = Clamp01(fromShare);
            if (sinceStart < 0f) return from;
            if (sinceStart < slowdown) return Lerp(from, slow, Smooth(sinceStart / slowdown));
            float up = UpStart(firedAfter, slowdown);
            if (sinceStart <= up) return slow;
            if (recovery <= 0f) return 1f;
            return Lerp(slow, 1f, Smooth((sinceStart - up) / recovery));
        }

        /// <summary>Seconds after the start at which the speed-up begins: when both the shot has gone off and the slow-down is over. Infinity while the shot has not gone off.</summary>
        public static float UpStart(float firedAfter, float slowdown) =>
            firedAfter < 0f ? float.PositiveInfinity : Math.Max(firedAfter, Math.Max(0f, slowdown));

        /// <summary>True once the speed is back to full: the speed-up has run its whole time.</summary>
        public static bool IsOver(float sinceStart, float firedAfter, float slowdown, float recovery) =>
            sinceStart >= UpStart(firedAfter, slowdown) + Math.Max(0f, recovery);

        static float Smooth(float x)
        {
            x = Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Lerp(float a, float b, float t) => a + (b - a) * t;
        static float Clamp01(float x) => Math.Max(0f, Math.Min(1f, x));
    }
}
