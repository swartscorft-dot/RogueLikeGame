using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Where a pistol sits in a hand, in one asset. The game reads it every frame, so a change in the
    /// Inspector shows at once while playing and is kept. The rig has no finger bones, so the hands stay
    /// open; this only moves the pistol in the hand.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Grip Settings", fileName = "Grip")]
    public sealed class GripSettings : ScriptableObject
    {
        [Tooltip("Metres from the wrist to the middle of the pistol, along the barrel. More puts the pistol farther out past the hand.")]
        public float Forward = 0.17f;

        [Tooltip("Metres from the wrist to the middle of the pistol, toward the top of the pistol. Less lowers the pistol into the hand.")]
        public float Up = 0.04f;

        [Tooltip("Metres from the wrist to the middle of the pistol, away from the body. The left hand mirrors the right.")]
        public float Outward = -0.02f;

        [ContextMenu("Reset to the start values")]
        void ResetToStartValues()
        {
            Forward = 0.17f;
            Up = 0.04f;
            Outward = -0.02f;
        }
    }
}
