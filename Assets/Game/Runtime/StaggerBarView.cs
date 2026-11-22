using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// A target's stagger bar, shown over it (work order 008): a flat bar that faces the camera, just above where its
    /// marks show (<see cref="MarkPips"/>), in the same look (vertex colours, glowing under the bloom). A dark frame,
    /// and a pale fill for the points left. While the target is dazed the bar is empty and the frame flashes. Hidden
    /// while the target is dead. Created from code on a target with a bar; it is a picture only.
    /// </summary>
    public sealed class StaggerBarView : MonoBehaviour
    {
        const float Width = 1.8f;        // m
        const float Height = 0.22f;      // m
        const float Border = 0.04f;      // m of frame round the fill
        const float OverHead = 1.25f;    // m over the top of the target: above the row of mark pips
        const float FlashPeriod = 0.3f;  // s, while dazed

        static readonly Color s_Frame = new Color(0.1f, 0.1f, 0.1f);
        static readonly Color s_Fill = new Color(1f, 0.85f, 0.45f);
        static readonly Color s_Flash = new Color(0.55f, 0.75f, 1f);
        const float FillGlow = 2f;
        const float FlashGlow = 3f;

        Damageable _target;
        Enemy _enemy; // work order 042: over an ordinary enemy or an elite the small bars draw the stagger instead (EnemyBars)
        Mesh _mesh;
        MeshRenderer _renderer;
        float _height;
        float _shownFill = -1f;
        int _shownFlash = -1;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _tris = new List<int>();

        /// <summary>The share of the bar shown filled now. For tests.</summary>
        public float Shown => Mathf.Max(0f, _shownFill);
        /// <summary>True while it is drawn. For tests.</summary>
        public bool Visible => _renderer != null && _renderer.enabled;
        /// <summary>True while it shows the flash of a daze, lit or between flashes. For tests.</summary>
        public bool Flashing => _shownFlash >= 0;

        /// <summary>The bar over a target, created on it the first time.</summary>
        public static StaggerBarView For(Damageable target, Material material)
        {
            if (target == null) return null;
            StaggerBarView view = target.GetComponentInChildren<StaggerBarView>(true);
            if (view != null) return view;

            float top = TopOf(target);

            var go = new GameObject("StaggerBar");
            go.transform.SetParent(target.transform, false);
            view = go.AddComponent<StaggerBarView>();
            view._target = target;
            view._enemy = target.GetComponent<Enemy>();
            view._height = top + OverHead;
            view._mesh = new Mesh { name = "StaggerBar" };
            view._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = view._mesh;
            view._renderer = go.AddComponent<MeshRenderer>();
            view._renderer.sharedMaterial = material;
            view._renderer.shadowCastingMode = ShadowCastingMode.Off;
            view._renderer.receiveShadows = false;
            return view;
        }

        /// <summary>The top of what is drawn for a target over its ground point (not its stagger bar), or its model's head.</summary>
        static float TopOf(Damageable target)
        {
            float top = 2f;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                top = float.NegativeInfinity;
                foreach (Renderer r in renderers)
                    if (!(r is LineRenderer) && r.GetComponent<StaggerBarView>() == null) top = Mathf.Max(top, r.bounds.max.y);
                if (float.IsNegativeInfinity(top)) top = 2f;
                else top -= target.transform.position.y;
            }
            // Over an enemy's model, its head (work order 011): a skinned mesh's box is not where its head is.
            if (target.TryGetComponent(out Enemy enemy) && enemy.ModelHeight > 0f) top = enemy.ModelHeight;
            return top;
        }

        /// <summary>
        /// Placed again over its target as it is shown now (the second reviews of 2026-11-20, work order 033): a bar made
        /// before an elite was scaled up sat inside its head.
        /// </summary>
        public void PlaceAgain()
        {
            if (_target != null) _height = TopOf(_target) + OverHead;
        }

        void LateUpdate()
        {
            if (_target == null || _target.Stagger == null) return;
            transform.position = _target.transform.position + Vector3.up * _height;
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;

            _renderer.enabled = _target.IsAlive && !EnemyBars.Covers(_enemy); // work order 042: under the small health bar instead
            float fill = _target.Stagger.Fill;
            bool dazed = _target.IsDazed;
            int flash = dazed ? (Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f ? 1 : 0) : -1;
            if (Mathf.Abs(fill - _shownFill) < 1e-4f && flash == _shownFlash) return;
            _shownFill = fill;
            _shownFlash = flash;
            Build(fill, flash);
        }

        void Build(float fill, int flash)
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            Color frame = flash == 1 ? Drawn(s_Flash, FlashGlow) : Drawn(s_Frame, 1f);
            Quad(-Width * 0.5f, Width * 0.5f, Height * 0.5f, 0f, frame);
            float inner = Width - 2f * Border;
            if (fill > 0f)
            {
                float left = -inner * 0.5f;
                // The camera looks along local +Z: the fill lies a little nearer it.
                Quad(left, left + inner * Mathf.Clamp01(fill), Height * 0.5f - Border, -0.02f, Drawn(s_Fill, FillGlow));
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>A rectangle in the local XY plane, drawn from both sides.</summary>
        void Quad(float x0, float x1, float halfHeight, float z, Color color)
        {
            int b = _verts.Count;
            _verts.Add(new Vector3(x0, -halfHeight, z));
            _verts.Add(new Vector3(x0, halfHeight, z));
            _verts.Add(new Vector3(x1, halfHeight, z));
            _verts.Add(new Vector3(x1, -halfHeight, z));
            for (int i = 0; i < 4; i++) _colors.Add(color);
            // Work order 081: the same twelve indices in the same order, added one by one (no two arrays made at every rebuild).
            _tris.Add(b); _tris.Add(b + 1); _tris.Add(b + 2); _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 3);
            _tris.Add(b); _tris.Add(b + 2); _tris.Add(b + 1); _tris.Add(b); _tris.Add(b + 3); _tris.Add(b + 2);
        }

        static Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            return new Color(c.r * glow, c.g * glow, c.b * glow, 1f);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
