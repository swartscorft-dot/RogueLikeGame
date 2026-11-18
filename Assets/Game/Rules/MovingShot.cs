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
    /// the move, and it slows while the shot winds up, then speeds up again after the shot has gone off.
    /// </summary>
    public static class MovingShot
    {
        /// <summary>
        /// The share of run speed. While a moving shot winds up (<paramref name="windingUp"/>): the slow share.
        /// From the moment it goes off (<paramref name="sinceFired"/> 0 or more): back up to 1 in a straight line
        /// over <paramref name="recoverSeconds"/>. Otherwise (no moving shot, or a negative time): 1.
        /// </summary>
        public static float SpeedShare(bool windingUp, float sinceFired, float slowShare, float recoverSeconds)
        {
            float slow = Math.Max(0f, Math.Min(1f, slowShare));
            if (windingUp) return slow;
            if (sinceFired < 0f) return 1f;
            if (recoverSeconds <= 0f || sinceFired >= recoverSeconds) return 1f;
            return slow + (1f - slow) * (sinceFired / recoverSeconds);
        }
    }
}
