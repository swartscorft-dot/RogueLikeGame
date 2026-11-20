using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// For the tests of work order 026: picks the floor reward in its Update, before the input driver's (its own file so
    /// that its execution order holds), as <see cref="GeneratedLevels"/> does on a 1 key when its Update happens to run
    /// first. The driver then reads the very frame the choice let go of the input in.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class PicksTheRewardFirst : MonoBehaviour
    {
        public GeneratedLevels Levels;
        /// <summary>The frame it picked in, or -1.</summary>
        public int PickedAt = -1;

        void Update()
        {
            if (PickedAt < 0 && Levels != null && Levels.Choice == GeneratedLevels.ChoiceKind.FloorReward && Levels.PickReward(0))
                PickedAt = Time.frameCount;
        }
    }
}
