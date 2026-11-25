using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The first run's teaching room (work order 133's follow-up; <see cref="PracticeTasks"/>): the run's clock held while she is
    /// taught, so the run's time, the floor's time and everything measured from them (the run box, the end screen, the session log's
    /// run and floor seconds, his records) do not count the teaching. A new file of this partial class: nothing else here changes.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Holds the run's and the floor's clocks at this moment (called every frame the teaching holds them): they start from where
        /// she is let go, as if the run began then. Nothing outside a run.
        /// </summary>
        public void HoldRunClock()
        {
            if (!InRun) return;
            _runStartedAt = Time.time;
            _floorStartedAt = Time.time;
        }

        /// <summary>The seconds the run's clock shows now (for the teaching's tests): 0 outside a run.</summary>
        public float RunClockSeconds => InRun ? Time.time - _runStartedAt : 0f;
    }
}
