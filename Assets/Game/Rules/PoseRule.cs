namespace Roguelike.Rules
{
    /// <summary>What the character's body is shown doing.</summary>
    public enum CharacterPose { Idle, Run, Shoot, Dash }

    /// <summary>Which pose the character model shows, from what the character is doing.</summary>
    public static class PoseRule
    {
        /// <summary>Below this speed, in m/s, the character counts as standing.</summary>
        public const float MoveThreshold = 0.1f;

        /// <summary>
        /// A dash in progress wins over everything. A shot or skill comes next, so a shot that starts while
        /// the dash animation is still finishing (<paramref name="dashRecovering"/>) cuts that animation short.
        /// </summary>
        public static CharacterPose Pick(bool dashing, bool acting, bool dashRecovering, float speed)
        {
            if (dashing) return CharacterPose.Dash;
            if (acting) return CharacterPose.Shoot;
            if (dashRecovering) return CharacterPose.Dash;
            return speed > MoveThreshold ? CharacterPose.Run : CharacterPose.Idle;
        }
    }
}
