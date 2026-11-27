using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The hurt guard on her (work order 147, section 7.1 of research R6): after a hit of the trial boss or its patterns she cannot be
    /// hit again for <see cref="BossTrialValues.HurtGuardSeconds"/>, so two patterns that overlap cannot take most of her health at once.
    /// Only in the trial fight: the trial boss puts this on her as it wakes and switches it on (<see cref="HurtGuardRule.Active"/>), and
    /// off as its fight ends. It is told by a flicker of her body: the game flashes no body of hers on a hit (a hit draws a spark on
    /// her, CombatEffects), so the flicker hides her own meshes every other <see cref="BossTrialValues.HurtFlickerHalf"/> through the
    /// guard, and shows them again at its end. Its one hook is in <see cref="PlayerHealth.TakeHit"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class HurtGuard : MonoBehaviour
    {
        public readonly HurtGuardRule Rule = new HurtGuardRule();
        /// <summary>The numbers it runs by: the trial boss's.</summary>
        public BossTrialValues Values = new BossTrialValues();

        readonly List<Renderer> _hidden = new List<Renderer>();
        readonly List<Renderer> _body = new List<Renderer>();
        bool _showingHidden;
        float _bodyFor = float.NaN; // the guard whose body was gathered: her meshes are looked up once a guard (a part she took may add one)

        /// <summary>True while the guard holds off a hit.</summary>
        public bool Holding => Rule.Holds(Time.time);
        /// <summary>True while her body is hidden by the flicker (for tests).</summary>
        public bool FlickerHidden => _showingHidden;
        /// <summary>Hits it held off this fight.</summary>
        public int HeldOff { get; private set; }

        /// <summary>The guard on her, made when missing.</summary>
        public static HurtGuard On(PlayerHealth health)
        {
            if (health == null) return null;
            if (!health.TryGetComponent(out HurtGuard g)) g = health.gameObject.AddComponent<HurtGuard>();
            return g;
        }

        /// <summary>Does the guard hold off a hit on her now? (<see cref="PlayerHealth.TakeHit"/>'s hook.)</summary>
        public static bool Holds(PlayerHealth health)
        {
            if (health == null || !health.TryGetComponent(out HurtGuard g) || !g.Rule.Active || !g.Rule.Holds(Time.time)) return false;
            g.HeldOff++;
            return true;
        }

        /// <summary>A hit took health from her (<see cref="PlayerHealth.TakeHit"/>'s hook): the guard starts, in the trial fight.</summary>
        public static void Took(PlayerHealth health)
        {
            if (health == null || !health.TryGetComponent(out HurtGuard g) || !g.Rule.Active) return;
            g.Rule.Took(Time.time, g.Values.HurtGuardSeconds);
        }

        /// <summary>The trial fight began or ended: the guard is on only through it.</summary>
        public void SetActive(bool on, BossTrialValues values)
        {
            if (values != null) Values = values;
            Rule.Active = on;
            if (!on)
            {
                Rule.Clear();
                Show();
            }
            HeldOff = 0;
        }

        void Update()
        {
            bool hide = Rule.Active && BossTrialRules.FlickerHidden(Time.time, Rule.Began, Values);
            if (hide == _showingHidden) return;
            if (hide) Hide();
            else Show();
        }

        void Hide()
        {
            if (!(_bodyFor == Rule.Began))
            {
                _bodyFor = Rule.Began;
                _body.Clear();
                foreach (Renderer r in GetComponentsInChildren<Renderer>(true))
                    if (r != null && !(r is LineRenderer) && !(r is TrailRenderer) && !(r is ParticleSystemRenderer) && r.GetComponent<CombatFx>() == null) _body.Add(r);
            }
            _hidden.Clear();
            foreach (Renderer r in _body)
                if (r != null && r.enabled)
                {
                    r.enabled = false;
                    _hidden.Add(r);
                }
            _showingHidden = true;
        }

        void Show()
        {
            foreach (Renderer r in _hidden) if (r != null) r.enabled = true;
            _hidden.Clear();
            _showingHidden = false;
        }

        void OnDisable() => Show();
    }
}
