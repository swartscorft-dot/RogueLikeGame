using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The hurt guard of work order 149 (R6 7.1; R7 5.1: "the hurt guard of 0.8 s"), its smallest version, behind its switch
    /// (<see cref="EliteValues.Unique"/>: only the Grave Ram begins it). After a hit of the Ram's that hurt her, nothing hurts her for
    /// <see cref="GraveRamValues.HurtGuardSeconds"/> (any hit then removes no health), and her body flickers (her skinned meshes hidden
    /// for half of each <see cref="GraveRamValues.HurtGuardFlicker"/>). One place reads it: <see cref="PlayerHealth.TakeHit"/>, right after
    /// its <see cref="PlayerHealth.Untouchable"/> line (<see cref="Holds"/>).
    ///
    /// Work order 147 builds the boss's own hurt guard (its Runtime/HurtGuard.cs, a check at the same place in TakeHit) in another
    /// folder: the producer folds the two into one at the merge. This one keeps its state here, statically for her one health, and its
    /// flicker in a small component put on her the first time; it allocates nothing in a frame.
    /// </summary>
    public sealed class EliteHurtGuard : MonoBehaviour
    {
        static PlayerHealth s_for;
        static float s_at = float.NegativeInfinity;
        static GraveRamValues s_values;
        static EliteHurtGuard s_flicker;

        SkinnedMeshRenderer[] _body;
        bool[] _was;
        bool _hidden;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_for = null;
            s_at = float.NegativeInfinity;
            s_values = null;
            s_flicker = null;
        }

        /// <summary>A hit of the Ram's hurt her now: the guard begins (again from now).</summary>
        public static void Begin(PlayerHealth health, GraveRamValues v)
        {
            if (health == null || v == null) return;
            s_for = health;
            s_at = Time.time;
            s_values = v;
            if (s_flicker == null || s_flicker.gameObject != health.gameObject)
            {
                s_flicker = health.GetComponent<EliteHurtGuard>();
                if (s_flicker == null) s_flicker = health.gameObject.AddComponent<EliteHurtGuard>();
            }
            s_flicker.Grab();
        }

        /// <summary>Does the guard hold her now: no hit removes health? Read by <see cref="PlayerHealth.TakeHit"/>.</summary>
        public static bool Holds(PlayerHealth health) =>
            health != null && health == s_for && s_values != null && GraveRamRules.Guards(Time.time - s_at, s_values);

        /// <summary>Did the guard spare her a hit just now (it holds)? For the Ram's count.</summary>
        public static bool Spared(PlayerHealth health) => Holds(health);

        /// <summary>Seconds since the guard began, negative with none. For tests.</summary>
        public static float Since(PlayerHealth health) => health != null && health == s_for ? Time.time - s_at : -1f;

        /// <summary>Ends it at once (a test, a new run).</summary>
        public static void Clear()
        {
            s_at = float.NegativeInfinity;
            if (s_flicker != null) s_flicker.ShowAll();
        }

        /// <summary>Her body as it is shown now: her skinned meshes, those shown as the guard began.</summary>
        void Grab()
        {
            if (_body == null)
            {
                _body = GetComponentsInChildren<SkinnedMeshRenderer>(true);
                _was = new bool[_body.Length];
            }
            if (_hidden) return;
            for (int i = 0; i < _body.Length; i++) _was[i] = _body[i] != null && _body[i].enabled;
        }

        void LateUpdate()
        {
            if (_body == null) return;
            PlayerHealth h = s_for;
            float since = h != null && h.gameObject == gameObject ? Time.time - s_at : -1f;
            bool hide = s_values != null && GraveRamRules.Guards(since, s_values) && !GraveRamRules.GuardShows(since, s_values) && (h == null || h.IsAlive);
            if (hide == _hidden) return;
            if (hide)
            {
                for (int i = 0; i < _body.Length; i++) if (_body[i] != null && _was[i]) _body[i].enabled = false;
                _hidden = true;
            }
            else ShowAll();
        }

        void ShowAll()
        {
            if (!_hidden || _body == null) return;
            for (int i = 0; i < _body.Length; i++) if (_body[i] != null && _was[i]) _body[i].enabled = true;
            _hidden = false;
        }

        void OnDisable() => ShowAll();

        /// <summary>True while her body is hidden by a flicker. For tests.</summary>
        public bool Hidden => _hidden;
    }
}
