using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// Which skill press that waited is still good when a skill's free recovery ends and the next skill may start
    /// (decision 0004, amended 2026-11-19: Kristian ruled that in the free recovery she looks free, so a skill key that
    /// does nothing there reads as a missed key).
    ///
    /// <list type="bullet">
    /// <item><b>After a free recovery</b> a press is good if it was made at or after the moment the skill let her go
    /// (its last hit, or the end of the hold), less the buffer time. So every press in the free recovery fires when it
    /// ends, however early in it, and so does one made just before the last hit. One made earlier than that, while she
    /// was visibly committed, is dropped.</item>
    /// <item><b>Otherwise</b> (a lock, a dash, the free recovery switched off) a press is good while it is no older
    /// than the buffer time, as the input buffer has always taken it.</item>
    /// </list>
    /// Times are seconds on one clock; the buffer time is <c>FeelValues.InputBuffer</c>.
    /// </summary>
    public static class SkillGate
    {
        /// <summary>The earliest a press may have been made and still fire after a free recovery.</summary>
        public static float EarliestGoodPress(float letGoAt, float window) => letGoAt - Math.Max(0f, window);

        /// <summary>
        /// True if a press made at <paramref name="pressedAt"/> fires when it is taken at <paramref name="now"/>.
        /// <paramref name="afterFreeRecovery"/>: it waited out a skill's free recovery, which let her go at
        /// <paramref name="letGoAt"/>. A press younger than the window is always good.
        /// </summary>
        public static bool PressStillGood(float pressedAt, float now, float window, bool afterFreeRecovery, float letGoAt)
        {
            if (now - pressedAt <= window) return true;
            return afterFreeRecovery && pressedAt >= EarliestGoodPress(letGoAt, window);
        }
    }
}
