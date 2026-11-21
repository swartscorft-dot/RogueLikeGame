using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The cast bar in the interface's kinetic skin (decision 0011: "the cast bar is a short slanted blade just above her
    /// head"; work order 045), as part of <see cref="CastBar"/>. The same bar, the same rules (<see cref="CastBarRules"/>),
    /// drawn small at the figure: the dim lead-in, the clean zone in teal (lit, framed white), the white marker, the skill's
    /// name small over it in the heavy italic, and on a press the blade flashes teal (clean) or white (plain) and goes.
    /// The bar under the middle of the screen is not drawn while the skin is on.
    /// </summary>
    public sealed partial class CastBar
    {
        /// <summary>True when it is drawn in the kinetic skin: the blade over her head, not the bar under the middle.</summary>
        public bool Kinetic { get; private set; }
        /// <summary>The bar under the middle of the screen is drawn now (the skin off).</summary>
        public bool CentreBarDrawn => Showing && !Kinetic;
        /// <summary>The blade over her head is drawn now, and where (layout pixels of a view 1080 high).</summary>
        public bool BladeDrawn => Showing && Kinetic && _bladeOnScreen;
        public Rect Blade { get; private set; }

        float _herTop = 2f;
        bool _bladeOnScreen;

        /// <summary>Where the blade is this frame: over the top of her head on the screen.</summary>
        void PlaceBlade()
        {
            Kinetic = InterfaceStyle.Kinetic;
            _bladeOnScreen = false;
            if (!Kinetic || _player == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 head = cam.WorldToScreenPoint(_player.Position + Vector3.up * _herTop);
            if (head.z <= 0f) return;
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            UiBox b = InterfaceRules.Blade(head.x / scale, (Screen.height - head.y) / scale, InterfaceStyle.V);
            Blade = new Rect(b.X, b.Y, b.W, b.H);
            _bladeOnScreen = true;
        }

        /// <summary>The height of the top of what is drawn for her over her feet (her renderers, not the effects), found once.</summary>
        float TopOfHer()
        {
            if (_player == null) return 2f;
            float top = float.NegativeInfinity;
            foreach (Renderer r in _player.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.GetComponent<CombatFx>() != null) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            if (float.IsNegativeInfinity(top)) return 2f;
            return Mathf.Clamp(top - _player.Position.y, 0.5f, 4f);
        }

        void DrawBlade()
        {
            if (!_bladeOnScreen) return;
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            Rect bar = Blade;
            float pivot = bar.center.y;
            CastBarView view = View;
            bool lit = Up ? Lit : _flashLit;
            string label = Up ? _label : _flashLabel;

            // Work order 063: grown with the master scale and the blade's own factor; the clean part's band framed white
            // always, so it is clearly marked before it lights (item 2b).
            float b = InterfaceRules.BladeFactor(v), f = InterfaceRules.Fight(v);
            // Work order 063, item 4 (the first picture's blade): on its dark plate, two red slashes at its start.
            InterfaceStyle.Plate(bar, pivot, v.Depth * b, v.Outline * b);
            if (v.Slash > 0f)
            {
                float w = v.Slash * b, gap = w * 0.8f;
                for (int i = 1; i <= 2; i++)
                    InterfaceStyle.Slanted(new Rect(bar.x - i * (w + gap), bar.y, w, bar.height), pivot, InterfaceStyle.C(v.Red));
            }
            InterfaceStyle.Slanted(bar, pivot, InterfaceStyle.C(v.Back));
            float dim = Mathf.Clamp01(view.DimTo);
            if (dim > 0f) InterfaceStyle.Slanted(new Rect(bar.x, bar.y, bar.width * dim, bar.height), pivot, InterfaceStyle.C(v.Grey, 0.35f));
            float marker = Mathf.Clamp01(view.Marker);
            InterfaceStyle.Slanted(new Rect(bar.x, bar.y + 2f * b, bar.width * marker, bar.height - 4f * b), pivot, InterfaceStyle.C(v.White, 0.35f));
            if (view.HasZone)
            {
                var zone = new Rect(bar.x + bar.width * Mathf.Clamp01(view.ZoneFrom), bar.y - 1f * b, bar.width * Mathf.Clamp01(view.ZoneTo - view.ZoneFrom), bar.height + 2f * b);
                InterfaceStyle.Slanted(zone, pivot, lit ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Teal, 0.55f));
                InterfaceStyle.SlantedFrame(zone, pivot, InterfaceStyle.C(v.White, lit ? 1f : 0.7f), (lit ? 1.5f : 1f) * b);
            }
            float mw = v.MarkerWidth * b;
            InterfaceStyle.Slanted(new Rect(bar.x + bar.width * marker - mw * 0.5f, bar.y - 3f * b, mw, bar.height + 6f * b), pivot, InterfaceStyle.C(v.White));
            if (Flash != CastBarFlash.None)
            {
                Color c = Flash == CastBarFlash.Clean ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White);
                InterfaceStyle.Slanted(new Rect(bar.x - 2f * b, bar.y - 2f * b, bar.width + 4f * b, bar.height + 4f * b), pivot, new Color(c.r, c.g, c.b, 0.6f * FlashAlpha));
            }
            if (!string.IsNullOrEmpty(label))
            {
                int size = InterfaceRules.Size(v.BladeNameSize, f);
                float h = size * 1.3f;
                InterfaceStyle.Label(new Rect(bar.x + v.Slant * bar.height, bar.y - h - 1f * b, bar.width, h), label,
                                     InterfaceStyle.Text(true, size, TextAnchor.LowerCenter), InterfaceStyle.C(v.White), true);
            }
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }
    }
}
