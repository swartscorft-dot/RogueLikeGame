using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Two waves (work order 136, <see cref="Roguelike.Rules.RoomWaveRules"/>): an enemy of a later wave is kept out of the game until its
    /// wave comes (its object switched off, so it cannot be hit, is in no list of the enemies in play and does nothing), and then stands at
    /// its point of arrival, awake: its room's fight is on, so it comes for her at its first step. That point is its home from then on
    /// (where it goes when its fight ends, and a shooter's or a caster's post: work order 098's "the place it was stood").
    /// </summary>
    public abstract partial class Enemy
    {
        /// <summary>True while it waits, switched off, for its wave (work order 136).</summary>
        public bool WaitsForItsWave { get; private set; }

        /// <summary>Takes it out of the game until its wave comes (work order 136): switched off where it stands.</summary>
        public void WaitForItsWave()
        {
            WaitsForItsWave = true;
            if (_self != null) _self.SetMaxHealth(StartHealth); // its floor's health while it waits, as its first frame gives it

            gameObject.SetActive(false);
        }

        /// <summary>Its wave came (work order 136): it stands at <paramref name="at"/> (on the NavMesh), its home from now on, switched on.</summary>
        public void ArriveAt(Vector3 at)
        {
            WaitsForItsWave = false;
            transform.position = at;
            _home = at;
            gameObject.SetActive(true);
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.Warp(at);
        }
    }
}
