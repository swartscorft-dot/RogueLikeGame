using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>A target that flashes when hit and refills a moment after it reaches zero.</summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class TrainingDummy : MonoBehaviour
    {
        const float FlashSeconds = 0.1f;
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        Damageable _damageable;
        Renderer _renderer;
        MaterialPropertyBlock _flash;
        float _deadSince = -1f;
        float _flashUntil;
        bool _flashing;

        public bool IsFlashing => _flashing;

        void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _renderer = GetComponent<Renderer>();
            _flash = new MaterialPropertyBlock();
            _flash.SetColor(s_BaseColor, Color.white);
            _damageable.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }

        void OnDamaged(int amount)
        {
            _flashUntil = Time.time + FlashSeconds;
            if (!_damageable.IsAlive) _deadSince = Time.time;
        }

        void Update()
        {
            if (_deadSince >= 0f && Time.time - _deadSince > Placeholders.DummyRefillDelay)
            {
                _damageable.Refill();
                _deadSince = -1f;
            }

            // The flash overrides the colour on this renderer only, so the shared material is never copied.
            bool flash = Time.time < _flashUntil;
            if (flash == _flashing || _renderer == null) return;
            _flashing = flash;
            _renderer.SetPropertyBlock(flash ? _flash : null);
        }
    }
}
