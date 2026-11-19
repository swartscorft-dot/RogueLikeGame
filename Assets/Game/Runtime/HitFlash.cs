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

        Damageable _damageable;
        MaterialPropertyBlock _flash;
        float _flashUntil;
        bool _flashing;

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

        void Update()
        {
            // The flash overrides the colour on these renderers only, so the shared material is never copied.
            bool flash = Time.time < _flashUntil;
            if (flash == _flashing || (Target == null && Targets == null)) return;
            _flashing = flash;
            _flash.SetColor(s_BaseColor, FlashColor);
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
