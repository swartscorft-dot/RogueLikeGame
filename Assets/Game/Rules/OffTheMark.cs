using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// How fast she is back at run speed after shooting (work order 040, Kristian's second session of 2026-11-20: "I think
    /// the character can accelerate a little faster still after one has stopped shooting and is to run again"). Her
    /// acceleration itself is instant (<see cref="FeelValues.TimeToFullSpeed"/> 0); what holds her back after a shot is a
    /// share of run speed that eases back up:
    /// - after a basic shot on the move, the ease back from the slow share (<see cref="MovingShot"/>), once the shot has
    ///   gone off and the slow-down is done: <see cref="FeelValues.MovingShotRecovery"/>;
    /// - after a shot that turned her to an aim behind her (a basic shot or a light skill: Triple Tap), the backpedal cap
    ///   (<see cref="BackpedalCap"/>): held for <see cref="FeelValues.ShotTurnHold"/> after the last such shot started, then
    ///   eased out over the same recovery;
    /// - after a committed skill (Backstep's shot), the rise from a stand (<see cref="SkillSlide.RiseShare"/>):
    ///   <see cref="FeelValues.SkillSpeedRecovery"/>.
    /// With <see cref="FeelValues.QuickOffTheMark"/> on, the numbers beside them stand in, each about a third quicker; the
    /// rise only after a recast chain's shot (the order asked for the basic shot and the chains). The slow-down while a shot
    /// is going off, the slide of a committed skill and her top speed are as they were.
    /// </summary>
    public static class OffTheMark
    {
        /// <summary>The ease back to full speed after a shot on the move, and out of the backpedal cap.</summary>
        public static float MovingShotRecovery(FeelValues f) =>
            f.QuickOffTheMark ? f.MovingShotRecoveryQuick : f.MovingShotRecovery;

        /// <summary>How long she stays turned to an aim behind her after the last such shot started.</summary>
        public static float ShotTurnHold(FeelValues f) => f.QuickOffTheMark ? f.ShotTurnHoldQuick : f.ShotTurnHold;

        /// <summary>The ease back to full speed when a committed skill lets her go; <paramref name="chainShot"/>: a recast chain's shot.</summary>
        public static float SkillSpeedRecovery(FeelValues f, bool chainShot) =>
            f.QuickOffTheMark && chainShot ? f.ChainSpeedRecoveryQuick : f.SkillSpeedRecovery;

        // ---------------- The numbers, from the curves themselves (for the report and the tests) ----------------

        const float Step = 1e-4f; // s between the samples of a curve
        const float Most = 5f;     // s: no curve is searched further than this

        /// <summary>
        /// Seconds from a basic shot on the move going off to her limit being back at <paramref name="share"/> of run speed,
        /// aim ahead (no backpedal). <paramref name="fromShare"/>: the share when the shot started (1 at a run; the slow share
        /// when she keeps clicking).
        /// </summary>
        public static float ShotOnTheMove(FeelValues f, float fromShare = 1f, float share = 0.9f)
        {
            float fired = Math.Max(0f, f.BasicShotStartup);
            for (float t = fired; t < fired + Most; t += Step)
                if (MovingShot.SpeedShare(fromShare, t, fired, f.MovingShotSpeedShare, f.MovingShotSlowdown, MovingShotRecovery(f)) >= share - 1e-6f)
                    return t - fired;
            return Most;
        }

        /// <summary>
        /// Seconds from a shot that turned her to an aim behind her going off (<paramref name="startup"/> after it started)
        /// to her limit being back at <paramref name="share"/> of run speed, running on away from the aim: the backpedal cap
        /// held and eased out, and for a basic shot (<paramref name="basicShot"/>) the ease of a shot on the move under it.
        /// </summary>
        public static float Backpedal(FeelValues f, float startup, bool basicShot, float share = 0.9f)
        {
            var cap = new BackpedalCap();
            float hold = ShotTurnHold(f), up = MovingShotRecovery(f);
            float fired = Math.Max(0f, startup);
            for (float t = 0f; t < fired + Most; t += Step)
            {
                float limit = cap.Update(t < hold, t, f.BackpedalSpeedShare, f.MovingShotSlowdown, up);
                if (basicShot) limit = Math.Min(limit, MovingShot.SpeedShare(1f, t, fired, f.MovingShotSpeedShare, f.MovingShotSlowdown, up));
                if (t >= fired && t >= hold && limit >= share - 1e-6f) return t - fired;
            }
            return Most;
        }

        /// <summary>
        /// Seconds from a committed skill letting her go (its last hit) to her limit being back at <paramref name="share"/>
        /// of run speed, from <paramref name="fromShare"/> (0: from a stand; what the slide had left when she pressed it on
        /// the move). <paramref name="chainShot"/>: a recast chain's shot.
        /// </summary>
        public static float Rise(FeelValues f, float fromShare, bool chainShot, float share = 0.9f)
        {
            float recovery = SkillSpeedRecovery(f, chainShot);
            for (float t = 0f; t < Most; t += Step)
                if (SkillSlide.RiseShare(fromShare, t, recovery) >= share - 1e-6f) return t;
            return Most;
        }
    }
}
