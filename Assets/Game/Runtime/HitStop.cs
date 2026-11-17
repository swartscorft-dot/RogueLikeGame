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

        public static bool IsActive => s_Runner != null && s_Runner._frozen;

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
                _restoreScale = Time.timeScale;
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
            Time.timeScale = _restoreScale;
        }
    }
}
