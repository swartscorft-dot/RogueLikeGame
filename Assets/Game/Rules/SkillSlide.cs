using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The speed around a skill pressed on the move (decision 0004, on trial; every value a guess).
    ///
    /// <list type="bullet">
    /// <item><b>The slide.</b> From the share of run speed she has at the press, the speed eases down to a stand. From
    /// full speed it takes <c>slowdown</c> seconds, from a lower speed less: the time scales with the share she starts
    /// from. A smoothstep, so there is no kink at the press and none where she comes to a stand. A slowdown of 0 is the
    /// old dead stop.</item>
    /// <item><b>The rise.</b> When the skill lets her go, the speed she may move at eases back up to full, from the
    /// share she has then. From a stand it takes <c>recovery</c> seconds, from a higher speed less. Also a smoothstep.
    /// A recovery of 0 gives full speed at once.</item>
    /// </list>
    /// Shares are of run speed, 0 to 1; times are seconds since the press (the slide) or since she was let go (the rise).
    /// </summary>
    public static class SkillSlide
    {
        /// <summary>Seconds the slide takes from this share to a stand: the slowdown times the share.</summary>
        public static float DownTime(float fromShare, float slowdown) => Math.Max(0f, slowdown) * Clamp01(fromShare);

        /// <summary>Seconds the rise takes from this share to full speed: the recovery times what is missing.</summary>
        public static float UpTime(float fromShare, float recovery) => Math.Max(0f, recovery) * (1f - Clamp01(fromShare));

        /// <summary>The share of run speed <paramref name="since"/> seconds after the press. 0 at once for a slowdown of 0.</summary>
        public static float SlideShare(float fromShare, float since, float slowdown)
        {
            if (slowdown <= 0f) return 0f;
            float from = Clamp01(fromShare);
            if (since <= 0f) return from;
            float down = DownTime(from, slowdown);
            if (down <= 0f || since >= down) return 0f;
            return from * (1f - Smooth(since / down));
        }

        /// <summary>
        /// Metres slid in the first <paramref name="since"/> seconds after the press, at a run speed of
        /// <paramref name="runSpeed"/>: the integral of the slide. From full speed to a stand it is run speed times
        /// the slowdown, halved.
        /// </summary>
        public static float SlideDistance(float fromShare, float since, float slowdown, float runSpeed)
        {
            float from = Clamp01(fromShare);
            if (slowdown <= 0f || since <= 0f || from <= 0f) return 0f;
            float down = DownTime(from, slowdown);
            float x = Math.Min(1f, since / down);
            // The integral of 1 - (3u^2 - 2u^3) from 0 to x is x - x^3 + x^4 / 2.
            return runSpeed * from * down * (x - x * x * x + 0.5f * x * x * x * x);
        }

        /// <summary>The share of run speed she may move at <paramref name="since"/> seconds after she was let go.</summary>
        public static float RiseShare(float fromShare, float since, float recovery)
        {
            float from = Clamp01(fromShare);
            if (since < 0f) return from;
            float up = UpTime(from, recovery);
            if (up <= 0f || since >= up) return 1f;
            return from + (1f - from) * Smooth(since / up);
        }

        /// <summary>True once the rise is over: she may move at full speed again.</summary>
        public static bool IsRisen(float fromShare, float since, float recovery) => since >= UpTime(fromShare, recovery);

        static float Smooth(float x)
        {
            x = Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        static float Clamp01(float x) => Math.Max(0f, Math.Min(1f, x));
    }

    /// <summary>
    /// The arms stay up for a moment after a shot or a skill (decision 0004, <c>AimHold</c>, a guess): for
    /// <c>hold</c> seconds after the last one ended, aimed at the live cursor. A dash ends the hold at once, and so
    /// does the cursor leaving the front of the body. While a shot or a skill is going there is no hold: it is aiming
    /// anyway, and the hold is counted from when it ends.
    /// </summary>
    public sealed class ArmHold
    {
        float _until = float.NegativeInfinity;

        /// <summary>True while the arms are held up after a shot or a skill.</summary>
        public bool Holding { get; private set; }

        /// <summary>
        /// Once a frame. <paramref name="acting"/>: a shot or a skill is going. <paramref name="cursorInFront"/>: the
        /// cursor lies within the front angle of the body. <paramref name="dashing"/>: she dashes (or a dash just
        /// started). Returns <see cref="Holding"/>.
        /// </summary>
        public bool Update(float now, bool acting, float hold, bool cursorInFront, bool dashing)
        {
            if (dashing) _until = float.NegativeInfinity;
            else if (acting) _until = now + Math.Max(0f, hold);
            else if (!cursorInFront) _until = float.NegativeInfinity;
            Holding = !acting && !dashing && now < _until;
            return Holding;
        }

        /// <summary>Ends the hold at once.</summary>
        public void End()
        {
            _until = float.NegativeInfinity;
            Holding = false;
        }
    }
}
