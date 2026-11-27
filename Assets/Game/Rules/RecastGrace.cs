using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// One frame of grace at the END of every clean part (work order 142, second round): a press is read at the earliest on the
    /// frame after the picture it answers, so a press answering the last frame drawn clean was judged a frame later, late (measured:
    /// drawn at 283.6 ms, judged at 300.3 ms, late; the same at Triple Tap's third press and Backstep's). A PRESS (never what is
    /// drawn: the drawings and "a press now would be clean" keep the clean part as it is) is judged against the clean part with its
    /// end moved by this frame's step of the window's clock, at most <see cref="MostSeconds"/>: exactly the press on the frame after
    /// the last drawn-clean frame. Not a share of the window; nothing at the clean part's start.
    /// </summary>
    public static class RecastGrace
    {
        /// <summary>The order's switch (ON: his "selvom jeg for sirkelen innenfor strekene får jeg feil"); off, no grace, as before.</summary>
        public static bool On = true;

        /// <summary>The most the end moves: one frame of a person's 60 a second.</summary>
        public const float MostSeconds = 1f / 60f;

        /// <summary>
        /// The grace for a press read in a frame whose step on the window's clock was <paramref name="frameSeconds"/> (its game
        /// seconds, or the real seconds a hit stop froze): that step, capped; 0 with the switch off.
        /// </summary>
        public static float Seconds(float frameSeconds) => On ? Math.Max(0f, Math.Min(MostSeconds, frameSeconds)) : 0f;

        /// <summary>A clean part with its end moved by <paramref name="grace"/> seconds (none: the part itself).</summary>
        public static CleanPart Graced(CleanPart part, float grace) => grace > 0f && part.Any ? new CleanPart(part.From, part.To + grace) : part;
    }
}
