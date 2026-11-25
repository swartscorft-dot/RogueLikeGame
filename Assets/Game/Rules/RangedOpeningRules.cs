namespace Roguelike.Rules
{
    /// <summary>
    /// When a shooter or a caster of a room's fight may begin a draw or a cast (work order 124, parts 1 and 2; Kristian ticked "Ja, bare
    /// når den synes" and "Ja, med ett sekunds pause"). A rule of the fight, not of the generator: the floor hands each room's fight the
    /// values (<see cref="RoomFight.Ranged"/>, null with <see cref="LevelValues.RangedAroundTheRoom"/> off), the enemy asks this before it
    /// begins, and nothing else of its attack changes: a draw already begun is finished, and while it may not begin it does what it does
    /// today while it cannot attack (holds its post, or keeps its distance). The warder's ward and the lancer's charge are not asked.
    ///
    /// - Part 1, only on his screen: it may begin only while it stands at least <see cref="RangedAroundValues.ScreenMargin"/> inside what
    ///   the camera shows of the ground (<see cref="ScreenGround"/>, the real camera's view handed in by the game).
    /// - Part 2, a second's grace: one that was within its reach of her as its room's fight woke may not begin before
    ///   <see cref="RangedAroundValues.GraceSeconds"/> have passed since the waking.
    /// </summary>
    public static class RangedOpeningRules
    {
        /// <summary>Is the second's grace still running for it (part 2)?</summary>
        public static bool InGrace(RangedAroundValues r, float startedAt, float now, bool inReachAtWaking) =>
            r != null && r.WakeGrace && inReachAtWaking && now - startedAt < r.GraceSeconds;

        /// <summary>
        /// May it begin a draw or a cast now: true without the values (the switch off, no room's fight); else not in its grace (part 2)
        /// and, with part 1 on, standing on his screen (<paramref name="onScreen"/>, asked of the real camera with the margin).
        /// </summary>
        public static bool MayBegin(RangedAroundValues r, bool onScreen, float startedAt, float now, bool inReachAtWaking)
        {
            if (r == null) return true;
            if (InGrace(r, startedAt, now, inReachAtWaking)) return false;
            return !r.OnScreenOnly || onScreen;
        }
    }
}
