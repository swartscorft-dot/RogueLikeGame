using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The hurt guard on her (research R6 7.1), ONE guard for its two sources since the merge of 2026-11-27 (W3), which folded work order
    /// 147's HurtGuard and work order 149's EliteHurtGuard into this class and one check: after a hit that hurt her she cannot be hurt
    /// again for <see cref="HurtGuardRule.Seconds"/>, so two attacks that overlap cannot take most of her health at once.
    ///
    /// Its sources, each behind its own order's switch: work order 147's trial boss switches it on as it wakes and off as its fight ends
    /// (<see cref="SetActive"/>; with BossValues.Trial off no trial boss wakes), and while it is on every hit that hurts her starts it
    /// (<see cref="Took"/>); work order 149's Grave Ram starts it by a hit of its own that hurt her (<see cref="Begin"/>; with
    /// EliteValues.Unique off no Ram stands). It is told by a flicker of her body: the game flashes no body of hers on a hit (a hit draws
    /// a spark on her, CombatEffects), so the flicker hides her own meshes every other <see cref="HurtGuardRule.FlickerHalf"/> through the
    /// guard, and shows them again at its end. Its one hook is in <see cref="PlayerHealth.TakeHit"/>.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public sealed class HurtGuard : MonoBehaviour
    {
        public readonly HurtGuardRule Rule = new HurtGuardRule();

        readonly List<Renderer> _hidden = new List<Renderer>();
        readonly List<Renderer> _body = new List<Renderer>();
        bool _showingHidden;
        float _bodyFor = float.NaN; // the guard whose body was gathered: her meshes are looked up once a guard (a part she took may add one)

        /// <summary>True while the guard holds off a hit.</summary>
        public bool Holding => Rule.Holds(Time.time);
        /// <summary>True while her body is hidden by the flicker (for tests).</summary>
        public bool FlickerHidden => _showingHidden;
        /// <summary>Hits it held off since the trial fight began (work order 147's count).</summary>
        public int HeldOff { get; private set; }

        /// <summary>The guard on her, made when missing.</summary>
        public static HurtGuard On(PlayerHealth health)
        {
            if (health == null) return null;
            if (!health.TryGetComponent(out HurtGuard g)) g = health.gameObject.AddComponent<HurtGuard>();
            return g;
        }

        /// <summary>Does the guard hold off a hit on her now? (<see cref="PlayerHealth.TakeHit"/>'s one hook: the hit is counted held off.)</summary>
        public static bool Holds(PlayerHealth health)
        {
            if (health == null || !health.TryGetComponent(out HurtGuard g) || !g.Rule.Holds(Time.time)) return false;
            g.HeldOff++;
            return true;
        }

        /// <summary>Is the guard on her now? Counts nothing (work order 149's Ram asks it for its own count of the hits it spared).</summary>
        public static bool Guarding(PlayerHealth health) =>
            health != null && health.TryGetComponent(out HurtGuard g) && g.Rule.Holds(Time.time);

        /// <summary>A hit took health from her (<see cref="PlayerHealth.TakeHit"/>'s hook): the guard starts, in the trial fight.</summary>
        public static void Took(PlayerHealth health)
        {
            if (health == null || !health.TryGetComponent(out HurtGuard g)) return;
            g.Rule.Took(Time.time);
        }

        /// <summary>A hit of a source of its own hurt her now (work order 149: the Grave Ram's): the guard begins, again from now.</summary>
        public static void Begin(PlayerHealth health)
        {
            HurtGuard g = On(health);
            if (g != null) g.Rule.Begin(Time.time);
        }

        /// <summary>Ends a guard running on her at once and shows her (a test, a new run).</summary>
        public static void Clear(PlayerHealth health)
        {
            if (health == null || !health.TryGetComponent(out HurtGuard g)) return;
            g.Rule.Clear();
            g.Show();
        }

        /// <summary>The trial fight began or ended: its hits start the guard only through it; its end ends a guard running.</summary>
        public void SetActive(bool on)
        {
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
            bool hide = Rule.Holds(Time.time) && HurtGuardRule.FlickerHidden(Time.time, Rule.Began);
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
