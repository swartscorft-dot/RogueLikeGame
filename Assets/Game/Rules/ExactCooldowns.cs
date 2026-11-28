namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 154 (the tests' health, 2026-11-28; the one game change the order allows): a cooldown keeps the moment it is
    /// ready again exactly (a double), so that what is left of it is the same at every frame rate and at every hour of a session.
    ///
    /// What it repairs: while spent focus's gains run (work order 071), every running cooldown, the dash's too, is cut each frame
    /// by the frame's seconds times 0.6 (<see cref="FocusGains.ExtraCooldownSeconds"/>). The moment it is ready again was a float
    /// of the game's clock, which is held to the nearest 0.12 ms after 17 minutes of play, 0.24 ms after 34 and 0.49 ms after
    /// 68: a cut smaller than that step was rounded to a whole step every frame, so with short frames late in a session the
    /// faster cooldown ran far too fast or too slow. Proven in <c>ExactCooldownsTests</c> (EditMode, the game's clock as Unity
    /// keeps it) and <c>FocusCooldownFrameStepTests</c> (PlayMode): what was left of Quick Shot's 1.65 s after the cut, run 1.6
    /// times as fast (1.031 s by the rule), ended after 0.825 s with frames of 0.3 ms at 83 minutes of play (FocusFeltTests'
    /// failure in every full PlayMode run, 0.84 to 0.88) and after 1.092 s with frames of 1 ms (a game running uncapped); at 30,
    /// 60 or 144 frames a second, and at 0.2 or 0.33 s a frame, it was right to the frame for the first 34 minutes of a session,
    /// and one or two frames late after that (1.050 s for 1.033 at 60 a second). A player at 60 frames a second gets nothing he
    /// can feel from the repair: at most a frame sooner, late in a long session.
    ///
    /// On (as it ships): the ready moment is kept as a double. Off: every step of it is rounded to a float as before, today's
    /// numbers bit for bit (<c>ExactCooldownsTests</c> proves it). Read where a cooldown's time is kept
    /// (<see cref="Cooldown"/>, <see cref="Dash"/>).
    /// </summary>
    public static class ExactCooldowns
    {
        /// <summary>The order's switch: on, a cooldown's ready moment is exact; off, a float as before work order 154.</summary>
        public static bool On = true;

        /// <summary>A ready moment as it is kept: itself with the switch on, rounded to a float (as before) with it off.</summary>
        public static double Keep(double t) => On ? t : (float)t;
    }
}
