namespace Roguelike.Rules
{
    /// <summary>
    /// What the character's body is shown doing. ShootMove is the gait for moving forward while shooting, ShootBack
    /// the one for moving backward while shooting (the body turned to a target behind).
    /// </summary>
    public enum CharacterPose { Idle, Run, Shoot, Dash, ShootMove, ShootBack }

    /// <summary>Which pose the character model shows, from what the character is doing.</summary>
    public static class PoseRule
    {
        /// <summary>Below this speed, in m/s, the character counts as standing.</summary>
        public const float MoveThreshold = 0.1f;

        /// <summary>
        /// A dash in progress wins over everything. Walking while slowed by a shot on the move
        /// (<paramref name="shootingGait"/>) comes next, so the legs keep going under the shot: the backward gait when
        /// she moves more backward than forward relative to where the body faces (<paramref name="movingBackward"/>),
        /// else the forward one. So a body turned square to the path (sideways) gets whichever of the two its
        /// small lean toward forward or backward picks. A shot or skill
        /// comes next, so a shot that starts while the dash animation is still finishing
        /// (<paramref name="dashRecovering"/>) cuts that animation short.
        /// </summary>
        public static CharacterPose Pick(bool dashing, bool acting, bool dashRecovering, float speed, bool shootingGait = false,
                                         bool movingBackward = false)
        {
            if (dashing) return CharacterPose.Dash;
            if (shootingGait && speed > MoveThreshold) return movingBackward ? CharacterPose.ShootBack : CharacterPose.ShootMove;
            if (acting) return CharacterPose.Shoot;
            if (dashRecovering) return CharacterPose.Dash;
            return speed > MoveThreshold ? CharacterPose.Run : CharacterPose.Idle;
        }
    }
}
