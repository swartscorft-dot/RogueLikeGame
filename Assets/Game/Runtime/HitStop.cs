using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Freezes the game for a moment when a hit lands, by setting the time scale to zero.
    /// A hidden runner counts unscaled time and puts the time scale back.
    /// </summary>
    public sealed class HitStop : MonoBehaviour
    {
        static HitStop s_Runner;

        float _until;
        float _restoreScale = 1f;
        bool _frozen;
        float _scaleFound = 1f;
        int _letGoFrame = -1;

        public static bool IsActive => s_Runner != null && s_Runner._frozen;

        /// <summary>
        /// True when a hit stop holds this frame's game time: it is frozen, or let go during this frame, whose game time was
        /// set, at 0, when the frame began (work order 038: the recast window runs on through it, RecastClock).
        /// </summary>
        public static bool HoldsThisFrame => s_Runner != null && (s_Runner._frozen || s_Runner._letGoFrame == Time.frameCount);

        /// <summary>The time scale the hit stop found when it froze the game: its pace, which slows the recast window in a hit stop (work order 038).</summary>
        public static float ScaleFound => s_Runner != null ? s_Runner._scaleFound : 1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_Runner = null;

        /// <summary>Freeze for this many seconds of real time. A second hit during a freeze extends it.</summary>
        public static void Trigger(float seconds)
        {
            if (seconds <= 0f) return;
            if (s_Runner == null)
            {
                var go = new GameObject("HitStop") { hideFlags = HideFlags.HideInHierarchy };
                s_Runner = go.AddComponent<HitStop>();
            }
            s_Runner.Freeze(seconds);
        }

        void Freeze(float seconds)
        {
            if (!_frozen)
            {
                // Never a scale below 1 (the review of 2026-11-20, work order 026): a slower one belongs to a pause, a
                // choice or the end of a run, and GeneratedLevels sets that every frame after any hit stop has let go.
                // Kept, a 0.4 from the end's slow-down would come back after a new run had set 1.
                _restoreScale = Mathf.Max(1f, Time.timeScale);
                _scaleFound = Time.timeScale;
                Time.timeScale = 0f;
                _frozen = true;
            }
            _until = Mathf.Max(_until, Time.unscaledTime + seconds);
        }

        void Update()
        {
            if (_frozen && Time.unscaledTime >= _until) Release();
        }

        // A scene change or the end of Play mode must never leave the game frozen.
        void OnDestroy()
        {
            Release();
            if (s_Runner == this) s_Runner = null;
        }

        void Release()
        {
            if (!_frozen) return;
            _frozen = false;
            _letGoFrame = Time.frameCount;
            Time.timeScale = _restoreScale;
        }
    }
}
