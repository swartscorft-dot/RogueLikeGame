using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// A rehearsal's end screen (work order 148), as part of <see cref="RunHud"/>: its title says it was a rehearsal, two lines
    /// before the seed's say the fight's length and what killed her (the last enemy strike that landed on her, when one did), and its first two
    /// answers are "Enter  again" (a new rehearsal at once) and "Main menu" (the way back to the start). Esc is the practice room's,
    /// as on every end screen. Any other end screen is as before.
    /// </summary>
    public sealed partial class RunHud
    {
        public const string RehearsalDied = "Rehearsal: you died";
        public const string RehearsalCleared = "Rehearsal: the boss is dead";
        public const string RehearsalAgain = "Enter  again";
        public const string RehearsalMainMenu = "Main menu";
        public const string RehearsalPracticeRoom = "Practice room";

        /// <summary>
        /// The end screen's first (0) or second (1) answer while it is a rehearsal's; null otherwise, and for the third, which is
        /// Esc's practice room as always.
        /// </summary>
        static string RehearsalEndButton(int answer)
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            if (levels == null || !levels.RehearsalEnding) return null;
            if (answer == 0) return RehearsalAgain;
            if (answer == 1) return StartMenu.Current != null ? RehearsalMainMenu : RehearsalPracticeRoom;
            return null;
        }

        /// <summary>The end screen's lines were made: a rehearsal's title, and its fight's lines before the seed's (the last).</summary>
        void AddRehearsalEndLines(bool died)
        {
            if (_levels == null || !_levels.RehearsalEnding || _endLines.Count == 0) return;
            _endLines[0] = died ? RehearsalDied : RehearsalCleared;
            int at = _endLines.Count - 1;
            float fight = _levels.RehearsalFightSeconds;
            _endLines.Insert(at++, "Rehearsal fight  " + (fight >= 0f ? RunWords.Clock(fight, tenths: true) : "not begun"));
            if (died && _levels.RehearsalKilledBy.Length > 0) _endLines.Insert(at, "Killed by  " + _levels.RehearsalKilledBy);
        }
    }
}
