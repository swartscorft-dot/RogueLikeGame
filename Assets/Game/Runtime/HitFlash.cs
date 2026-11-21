using System;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Flashes a renderer white for a moment each time the Damageable on this object is hit. An enemy's model
    /// (work order 011) flashes all its meshes, set in <see cref="Targets"/>, with <see cref="FlashColor"/> brighter
    /// than white: its colour multiplies the painted texture, so white alone would not show.
    /// </summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class HitFlash : MonoBehaviour
    {
        const float FlashSeconds = 0.1f;
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        public Renderer Target;
        /// <summary>When set, flashed instead of <see cref="Target"/>.</summary>
        [NonSerialized] public Renderer[] Targets;
        [NonSerialized] public Color FlashColor = Color.white;
        /// <summary>Work order 064: a crit's flash, a hard white frame: pure white this many times over, for one flash.</summary>
        [NonSerialized] public float CritBrightness = 4f;

        Damageable _damageable;
        MaterialPropertyBlock _flash;
        float _flashUntil;
        bool _flashing;
        bool _crit, _critShown; // work order 064: the flash running is a crit's

        public bool IsFlashing => _flashing;

        void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _flash = new MaterialPropertyBlock();
            _damageable.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }

        void OnDamaged(int amount) => _flashUntil = Time.time + FlashSeconds;

        /// <summary>The hit just taken was a crit of hers (work order 064): its flash is a hard white frame.</summary>
        public void Crit()
        {
            _flashUntil = Time.time + FlashSeconds;
            _crit = true;
        }

        /// <summary>True while the flash running is a crit's.</summary>
        public bool IsCritFlash => _flashing && _critShown;

        void Update()
        {
            // The flash overrides the colour on these renderers only, so the shared material is never copied.
            bool flash = Time.time < _flashUntil;
            if (!flash) _crit = false;
            if ((flash == _flashing && _crit == _critShown) || (Target == null && Targets == null)) return;
            _flashing = flash;
            _critShown = _crit;
            _flash.SetColor(s_BaseColor, _crit ? Color.white * CritBrightness : FlashColor);
            if (Targets == null)
            {
                Target.SetPropertyBlock(flash ? _flash : null);
                return;
            }
            foreach (Renderer r in Targets)
                if (r != null) r.SetPropertyBlock(flash ? _flash : null);
        }
    }
}
