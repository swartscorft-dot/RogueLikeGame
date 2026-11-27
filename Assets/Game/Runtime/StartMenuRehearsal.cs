using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The main menu's entry "Rehearsal: the boss" (work order 148; Kristian's tick of 2026-11-27, "Også «generalprøve» med siste
    /// bygg"), as part of <see cref="StartMenu"/>: one line, REHEARSAL, at <see cref="RehearsalValues.MenuPlace"/> (right under START
    /// RUN), shown only while a build is stored and the switch is on (<see cref="GeneratedLevels.HasRehearsalBuild"/>, asked as the
    /// main menu is made, never in a frame); picked, the rehearsal starts (<see cref="GeneratedLevels.StartRehearsal()"/>) and the
    /// menu goes. The main menu is where a run is started today (START RUN), and where a rehearsal's end screen goes back to.
    /// </summary>
    public sealed partial class StartMenu
    {
        /// <summary>The main menu's lines were made: the rehearsal's line goes in, while a build is stored.</summary>
        void RehearsalLine()
        {
            GeneratedLevels levels = Levels;
            if (levels == null || !levels.HasRehearsalBuild || _main.Contains(MainLine.Rehearsal)) return;
            _main.Insert(System.Math.Min(RehearsalValues.MenuPlace, _main.Count), MainLine.Rehearsal);
        }

        /// <summary>REHEARSAL picked: the rehearsal starts and the menu goes. False when none could start (no build now, the switch off).</summary>
        bool PickRehearsal()
        {
            GeneratedLevels levels = Levels;
            if (levels == null) return false;
            GeneratedLevel level = levels.StartRehearsal();
            if (level != null || levels.InRun) Close();
            return level != null;
        }
    }
}
