using System;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// For the tests of work order 033: does what it is given once, in its Update, before every other component's Update
    /// of that frame (its own file, so that its execution order holds), as a key read by a component that happens to run
    /// first would.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class ActsFirstInFrame : MonoBehaviour
    {
        public Action Act;
        /// <summary>The frame it acted in, or -1.</summary>
        public int ActedAt = -1;

        void Update()
        {
            if (Act == null) return;
            Action act = Act;
            Act = null;
            ActedAt = Time.frameCount;
            act();
        }
    }
}
