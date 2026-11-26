namespace Roguelike.Rules
{
    /// <summary>
    /// What a recast press is answered as (work order 142; Kristian, 2026-11-26, playing with the ring cue on: "selvom jeg for
    /// sirkelen innenfor strekene får jeg feil. det samme gjelder den opprinnelige baren og"): every answer that says clean or
    /// not follows the JUDGED timing of the press (<see cref="RecastTiming.Clean"/>: inside its own window's clean part, as
    /// every cue draws it), not only whether it was the chain's rewarded press (the clean tap or clean spend, the last press).
    /// Triple Tap's second press timed clean was answered by the cast bar's flash as plain, the look of a press that missed,
    /// in every build since work order 069 gave that press a clean part of its own. The rewarded press keeps its stronger
    /// answer where it has one (its damage, its focus, the CLEAN word and tone, the big number): nothing of that moves here.
    /// </summary>
    public static class CleanPressAnswers
    {
        /// <summary>
        /// The order's switch (work order 142, ON with his words above: a press inside the shown window gets the good answer).
        /// Off, the cast bar's flash follows the rewarded press alone, as before the order.
        /// </summary>
        public static bool On = true;

        /// <summary>
        /// Is a recast press answered as clean: the rewarded press always; with the switch on, any press whose timing was judged
        /// clean too.
        /// </summary>
        public static bool AnsweredClean(bool rewarded, RecastTiming timing) => rewarded || (On && timing == RecastTiming.Clean);
    }
}
