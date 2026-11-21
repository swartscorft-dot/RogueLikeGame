using System;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// For the tests of work order 055: does what it is given in its LateUpdate, after every other component's LateUpdate
    /// of the frame (the camera's move, the skill bar's and the cast bar's places; its own file, so that its execution
    /// order holds), so a test sees the frame as it was drawn.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    public sealed class LooksAtTheFramesEnd : MonoBehaviour
    {
        public Action Look;

        void LateUpdate() => Look?.Invoke();
    }
}
