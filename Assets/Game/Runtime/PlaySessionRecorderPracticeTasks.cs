using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The first run's teaching room in the session log (work order 133's follow-up): a line "practice_tasks" when it began (the run
    /// began with it), when the tasks were finished or ended, and when she walked on (the run's clock starts then). A new file of this
    /// partial class: nothing else here changes.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>One line of the teaching: what happened (<see cref="PracticeTaskLog"/>), the tasks done and skipped of how many, and the seconds since it began.</summary>
        public void NotePracticeTasks(string what, int done, int skipped, int tasks, float seconds) =>
            Note(PracticeTaskLog.Type, what, "done", Int(done), "skipped", Int(skipped), "tasks", Int(tasks), "seconds", Text(seconds));
    }
}
