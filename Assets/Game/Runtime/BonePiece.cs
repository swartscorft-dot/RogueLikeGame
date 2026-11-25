using System;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// One loose piece of bone of <see cref="BonePieces"/> (work order 122): it only tells its pool how hard it landed, so that a
    /// whole piece that lands hard enough breaks once more in two.
    /// </summary>
    public sealed class BonePiece : MonoBehaviour
    {
        [NonSerialized] public BonePieces Owner;
        [NonSerialized] public int Index;

        void OnCollisionEnter(Collision collision)
        {
            if (Owner != null) Owner.Landed(Index, collision.relativeVelocity.magnitude);
        }
    }
}
