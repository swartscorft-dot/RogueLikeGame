using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// For the tests of work order 142 (CleanPressJudgedTests), made to hold late in a long run by work order 154: while the game is
    /// stepped at a fixed <see cref="Frame"/> of game time, the frame after each recast window opens is half of one, so that no
    /// frame lands on an edge of a clean part (its own file, so that its execution order holds: after every other LateUpdate).
    ///
    /// Why: a window opens on a frame and its clock runs in whole frames from there, and Triple Tap's clean part ends 0.3 s after
    /// it opens, eighteen frames of 1/60 s exactly: a frame stood on the edge itself. Young in a run the clock read it to a
    /// thousandth of a millisecond; late in a full PlayMode run (the game's clock, a float, held to 0.12 or 0.24 ms) it read it a
    /// quarter of a millisecond either side, and the edge frame was clean or not by the clock's rounding (299.8 ms in the full
    /// run's log), not by the rule. Half a frame after the opening, every frame stands 8.3 ms from both edges. Nothing of the
    /// game changes: the frames are the test's.
    /// </summary>
    [DefaultExecutionOrder(30000)]
    public sealed class HalfAFrameAfterEachOpening : MonoBehaviour
    {
        public PlayerCharacter Player;
        /// <summary>The test's fixed frame, s of game time; it acts only while the game is stepped at it.</summary>
        public float Frame;
        /// <summary>The half frames it has made.</summary>
        public int Halved { get; private set; }

        bool _halfNow;

        void LateUpdate()
        {
            if (Player == null || Frame <= 0f) return;
            if (_halfNow)
            {
                _halfNow = false;
                if (Time.captureDeltaTime == Frame * 0.5f) Time.captureDeltaTime = Frame;
            }
            // The frame a window opened on: its clock reads exactly 0 (the window opens at the frame's own time).
            if (Time.captureDeltaTime == Frame && Player.IsRecastWindowOpen && Player.RecastSinceOpened == 0f)
            {
                Time.captureDeltaTime = Frame * 0.5f;
                _halfNow = true;
                Halved++;
            }
        }
    }
}
