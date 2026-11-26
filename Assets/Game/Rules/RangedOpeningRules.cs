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
    ///
    /// Work order 140 (<see cref="WarderToo"/>, <see cref="LancerToo"/>): the warder's bolt and the beginning of the lancer's charge are asked part 1 too,
    /// by the same test of his screen with the same margin (<see cref="MayBeginSeen"/>), not part 2.
    /// </summary>
    public static class RangedOpeningRules
    {
        /// <summary>
        /// Work order 140's second part, ON by default. Told on 2026-11-26 that of the enemies that attack from afar only the warder still
        /// throws its bolt from off his screen and that the lancer begins its charge from off it (12 to 16 m), Kristian ticked
        /// "Ja, begge": on, a warder alone begins its bolt and a lancer begins its charge's wind-up only while it stands on his screen,
        /// asked as a shooter's draw is (part 1 here, its margin, the real camera); while it may not, it does what it did while it could
        /// not attack (the warder walks to its spot, the lancer keeps its distance and faces her); a charge or a bolt already begun
        /// goes on wherever she walks. The second's grace at the waking (part 2) is not theirs. Off: both are as before, field for field.
        /// This switch is the warder's half; the lancer's is <see cref="LancerToo"/>. One switch for the whole game, so it is static;
        /// tests set it and put it back.
        /// </summary>
        public static bool WarderToo = true;

        /// <summary>
        /// Work order 140's second part, the lancer's half: OFF by default, against his tick ("Ja, begge"), until he has answered. Built and
        /// measured (WarderAndLancerOnScreenPlayTests, floors 4 and 5 of three run seeds, a stand-in for her that walks to the nearest
        /// enemy and shoots it): before, 2.5 (floor 4) and 4.3 (floor 5) charges a minute a lancer, 35 of 61 begun off screen; with this
        /// on, NONE in 18 lancer-minutes. The lancer keeps 12 to 16 m from her and charges from 10 to 17 m, and his screen reaches only
        /// about 7.2 m toward its bottom and 10.8 m toward its top of her (13.3 m to the sides) inside the margin, so a lancer above or below
        /// her is never on screen inside its band and holds there for ever: the different fault work order 140 warned of. On: the rule as
        /// he ticked it, for the comparison; how the lancer should come into view is his to choose (the report's question).
        /// </summary>
        public static bool LancerToo = false;

        /// <summary>
        /// Part 1 alone, his screen: true without the values; else, with <see cref="RangedAroundValues.OnScreenOnly"/> on, only standing
        /// on it (<paramref name="onScreen"/>, asked of the real camera with <see cref="RangedAroundValues.ScreenMargin"/>).
        /// </summary>
        public static bool MayBeginSeen(RangedAroundValues r, bool onScreen) => r == null || !r.OnScreenOnly || onScreen;

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
            return MayBeginSeen(r, onScreen);
        }
    }
}
