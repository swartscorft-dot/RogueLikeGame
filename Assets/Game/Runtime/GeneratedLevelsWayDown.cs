using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Exit rooms of their own in a run (work order 103; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "tronrommet bare
    /// i etasje 5 og egne utgangsrom i etasje 1–4"): each floor of a run after the first is told the exit room of its own the floor
    /// before ended in (<see cref="WayDowns.Shown"/>), so its own exit room is another while one can stand (<see cref="WayDowns.Avoid"/>,
    /// <see cref="LevelLayout.ExitBefore"/>). A floor built alone, outside <see cref="EnterFloor"/>, is told none.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        string _wayDownBefore; // what the floor being built is told: set only while a run's floor is built
        string _wayDownShown;  // the exit room of its own the run's last floor ended in, or null

        /// <summary>The exit room of its own the run's floor before ended in (null on floor 1, or when it was none). For tests.</summary>
        public string WayDownBeforeThisFloor { get; private set; }

        /// <summary>Before a run's floor is built: floor 1 is told none (a new run), a later floor the one before's.</summary>
        void WayDownBefore(int floor)
        {
            if (floor <= 1) _wayDownShown = null;
            _wayDownBefore = _wayDownShown;
            WayDownBeforeThisFloor = _wayDownShown;
        }

        /// <summary>After a run's floor is built: what its exit room was, for the next; the floor alone is told nothing after.</summary>
        void WayDownShown(GeneratedLevel level)
        {
            _wayDownBefore = null;
            _wayDownShown = level != null ? WayDowns.Shown(level.Layout) : null;
        }
    }
}
