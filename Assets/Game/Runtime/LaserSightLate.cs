using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The Laser Sight's beams are drawn late in the frame (work order 102): after <see cref="CharacterAnimation"/> has posed her
    /// arms and pistols in its LateUpdate (order 0) and the camera's rig has moved (order 0), so each beam leaves the muzzle
    /// where it is shown this frame, along the barrel as it points this frame. On the object that holds both beams
    /// (<see cref="LaserSight"/>), made the first time she has the part; it only asks her to draw them.
    /// </summary>
    [DefaultExecutionOrder(Order)]
    public sealed class LaserSightLate : MonoBehaviour
    {
        /// <summary>Its place in the frame's order: after every LateUpdate of order 0.</summary>
        public const int Order = 500;

        /// <summary>Whose beams.</summary>
        public PlayerCharacter Owner;

        void LateUpdate()
        {
            if (Owner != null) Owner.TickLaser();
        }
    }
}
