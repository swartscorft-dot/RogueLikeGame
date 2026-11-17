using UnityEngine;

namespace Roguelike
{
    /// <summary>Flashes a renderer white for a moment each time the Damageable on this object is hit.</summary>
    [RequireComponent(typeof(Damageable))]
    public sealed class HitFlash : MonoBehaviour
    {
        const float FlashSeconds = 0.1f;
        static readonly int s_BaseColor = Shader.PropertyToID("_BaseColor");

        public Renderer Target;

        Damageable _damageable;
        MaterialPropertyBlock _flash;
        float _flashUntil;
        bool _flashing;

        public bool IsFlashing => _flashing;

        void Awake()
        {
            _damageable = GetComponent<Damageable>();
            _flash = new MaterialPropertyBlock();
            _flash.SetColor(s_BaseColor, Color.white);
            _damageable.Damaged += OnDamaged;
        }

        void OnDestroy()
        {
            if (_damageable != null) _damageable.Damaged -= OnDamaged;
        }

        void OnDamaged(int amount) => _flashUntil = Time.time + FlashSeconds;

        void Update()
        {
            // The flash overrides the colour on this renderer only, so the shared material is never copied.
            bool flash = Time.time < _flashUntil;
            if (flash == _flashing || Target == null) return;
            _flashing = flash;
            Target.SetPropertyBlock(flash ? _flash : null);
        }
    }
}
