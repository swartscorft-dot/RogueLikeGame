using UnityEngine;

namespace Roguelike
{
    /// <summary>A ring on the ground around the character that shows the basic-attack range while aiming an attack-move.</summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class RangeIndicator : MonoBehaviour
    {
        const int Segments = 96;
        const float HeightAboveGround = 0.05f;

        public PlayerCharacter Player;

        LineRenderer _line;
        float _drawnRadius = -1f;

        public bool IsVisible => _line != null && _line.enabled;
        public float Radius => _drawnRadius;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = Segments;
            _line.enabled = false;
        }

        public void SetVisible(bool visible)
        {
            _line.enabled = visible;
            if (visible) Redraw();
        }

        void LateUpdate()
        {
            if (_line.enabled) Redraw();
        }

        void Redraw()
        {
            if (Player == null) return;
            _drawnRadius = Player.Feel.BasicAttackRange;
            Vector3 center = Player.Position;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                _line.SetPosition(i, new Vector3(center.x + Mathf.Cos(angle) * _drawnRadius, HeightAboveGround,
                                                 center.z + Mathf.Sin(angle) * _drawnRadius));
            }
        }
    }
}
