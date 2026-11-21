using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The cooldown buffer for every skill (work order 066, 2026-11-21). Work order 064 gave Backstep's first press a buffer:
    /// a press made up to <see cref="KitValues.CooldownBuffer"/> before its cooldown ends is kept and goes when the skill is
    /// ready. In his play of that morning twelve of his Backstep presses were refused on cooldown, six of them close to
    /// ready; asked whether it should hold for every skill, Kristian chose "Ja, alle ferdigheter" (yes, every skill).
    ///
    /// The rules: a press with at most the buffer left (and some left) waits; earlier, it is refused as before; 0 gives the
    /// morning's refusal. A skill with a charge left is not on cooldown, so nothing here touches it. Once the skill is
    /// ready the press counts as made at that moment (the ordinary input buffer and a free recovery judge it from there).
    /// It is dropped when she is stunned or pulled, dies, or a choice, the pause menu or an end screen comes up.
    /// </summary>
    public static class CooldownBufferRules
    {
        /// <summary>
        /// True when a press with <paramref name="cooldownLeft"/> seconds of its skill's cooldown left waits for it: some is
        /// left and no more than <paramref name="buffer"/>. With a buffer of 0 or less, never (refused, as this morning).
        /// </summary>
        public static bool Waits(float cooldownLeft, float buffer) => buffer > 0f && cooldownLeft > 0f && cooldownLeft <= buffer;

        /// <summary>
        /// When a press that waited for its cooldown counts as made: the moment the skill became ready, or the press itself
        /// if that was later (it never was, but a cooldown cut by focus makes the stored moment a little late: never early).
        /// </summary>
        public static float CountsFrom(float pressedAt, float readyAt) => Math.Max(pressedAt, readyAt);

        /// <summary>
        /// True while a press waiting for its cooldown is kept: false once she is dead, stunned or pulled, or a choice, the
        /// pause menu or an end screen is up.
        /// </summary>
        public static bool Kept(bool alive, bool held, bool choiceUp, bool screenUp) => alive && !held && !choiceUp && !screenUp;

        /// <summary>
        /// True while it still waits for a cooldown that has not ended, at most <paramref name="buffer"/> plus the ordinary
        /// <paramref name="inputBuffer"/> after the press: a cooldown that has not ended by then (none does; the bound is
        /// a guard) drops it.
        /// </summary>
        public static bool StillInTime(float pressedAt, float now, float buffer, float inputBuffer) =>
            now - pressedAt <= Math.Max(0f, buffer) + Math.Max(0f, inputBuffer);
    }
}
