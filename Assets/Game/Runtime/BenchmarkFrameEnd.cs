using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The benchmark's mark at the end of the frame's Updates and LateUpdates (work order 032, 2026-11-20): it runs after
    /// every other component's (its own file, so that its execution order holds), and tells <see cref="Benchmark"/> how
    /// long all of them took; after the LateUpdates it keeps the scenario staged (<see cref="BenchmarkStage.Keep"/>).
    /// </summary>
    [DefaultExecutionOrder(32000)]
    public sealed class BenchmarkFrameEnd : MonoBehaviour
    {
        public Benchmark Owner;

        void Update()
        {
            if (Owner != null) Owner.UpdatesEnded();
        }

        void LateUpdate()
        {
            if (Owner != null) Owner.LateUpdatesEnded();
        }
    }
}
