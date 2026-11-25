using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// What the timing cues at her sight read of her (work order 134, <see cref="TimingAtTheSight"/>). They read only: nothing
    /// here changes her aim, a window or a shot.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>
        /// A point on the cursor's ray as the input driver last read it (work order 094): her aim's point raised to the height its
        /// ray was met at (the gun's, or the floor's), so through the camera it lies where the cursor is in the frame that is
        /// drawn. The cue is drawn round it when no real mouse is read (tests, pictures); with one, round the mouse itself.
        /// </summary>
        public Vector3 CursorOnItsRay => new Vector3(_shotCursor.x, _cursorOnCamera ? _cursorHeight : 0f, _shotCursor.z);
    }
}
