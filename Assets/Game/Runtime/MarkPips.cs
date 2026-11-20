using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The marks on a target, shown over it (decision 0005, 2026-11-19): a row of flat diamonds that face the camera,
    /// in the look of the shot effects (<see cref="ShotFx"/>: an outer shape in a rim colour, an inner one in a core
    /// colour, glowing under the bloom). One lit diamond per mark and a dark one for each mark still free up to the
    /// cap; at the cap the lit ones turn red, ready to spend. In the last second before they fall off they blink.
    /// Nothing shows on a target that carries none. Created from code, on the target, the first time it is marked;
    /// it is a picture only.
    /// </summary>
    public sealed class MarkPips : MonoBehaviour
    {
        const float PipSize = 0.5f;     // m from tip to tip
        const float CoreShare = 0.55f;  // the inner diamond's size against the outer
        const float Spacing = 0.62f;    // m between pip centres
        const float OverHead = 0.6f;    // m over the top of what it is drawn over
        const float BlinkFrom = 1f;     // s before the marks fall off that the pips blink
        const float BlinkPeriod = 0.25f;

        static readonly Color s_Rim = new Color(1f, 0.55f, 0.1f);
        static readonly Color s_Core = new Color(1f, 0.95f, 0.65f);
        static readonly Color s_FullRim = new Color(1f, 0.15f, 0.08f);
        static readonly Color s_FullCore = new Color(1f, 0.75f, 0.55f);
        static readonly Color s_Empty = new Color(0.12f, 0.12f, 0.12f);
        static readonly Color s_TagRim = new Color(0.2f, 1f, 0.45f);
        static readonly Color s_TagCore = new Color(0.8f, 1f, 0.85f);
        bool _shownTag;
        const float RimGlow = 2f;
        const float CoreGlow = 3f;

        Damageable _target;
        Mesh _mesh;
        MeshRenderer _renderer;
        float _height;
        int _shownCount = -1;
        int _shownMax = -1;
        bool _shownLit;
        readonly List<Vector3> _verts = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _tris = new List<int>();

        /// <summary>The cap, for the dark pips. The character sets it when it marks.</summary>
        public int Max { get; set; } = 5;
        /// <summary>The marks the pips show now. For tests.</summary>
        public int Shown => _shownLit ? Mathf.Max(0, _shownCount) : 0;
        /// <summary>True while any pip is drawn. For tests.</summary>
        public bool Visible => _renderer != null && _renderer.enabled;

        /// <summary>The pips over a target, created on it the first time.</summary>
        public static MarkPips For(Damageable target, Material material)
        {
            if (target == null) return null;
            MarkPips pips = target.GetComponentInChildren<MarkPips>(true);
            if (pips != null) return pips;

            // The top of what is drawn for it, before the pips are there to count.
            float top = 2f;
            Renderer[] renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                top = float.NegativeInfinity;
                // Not over a stagger bar (work order 008): that one is drawn above the pips.
                foreach (Renderer r in renderers)
                    if (r.GetComponent<StaggerBarView>() == null) top = Mathf.Max(top, r.bounds.max.y);
                top -= target.transform.position.y;
            }
            // Over an enemy's model, its head (work order 011): a skinned mesh's box is not where its head is.
            if (target.TryGetComponent(out Enemy enemy) && enemy.ModelHeight > 0f) top = enemy.ModelHeight;

            var go = new GameObject("MarkPips");
            go.transform.SetParent(target.transform, false);
            pips = go.AddComponent<MarkPips>();
            pips._target = target;
            pips._height = top + OverHead;
            pips._mesh = new Mesh { name = "MarkPips" };
            pips._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = pips._mesh;
            pips._renderer = go.AddComponent<MeshRenderer>();
            pips._renderer.sharedMaterial = material;
            pips._renderer.shadowCastingMode = ShadowCastingMode.Off;
            pips._renderer.receiveShadows = false;
            pips._renderer.enabled = false;
            return pips;
        }

        void LateUpdate()
        {
            if (_target == null) return;
            int count = _target.MarkCount;
            float left = _target.MarksFallOffIn;
            bool lit = count > 0 && (left > BlinkFrom || Mathf.Repeat(left, BlinkPeriod) > BlinkPeriod * 0.5f);

            // Over the target, level whatever it does, and face on to the camera.
            transform.position = _target.transform.position + Vector3.up * _height;
            Camera cam = Camera.main;
            if (cam != null) transform.rotation = cam.transform.rotation;

            bool tagged = _target.IsTagged;
            _renderer.enabled = count > 0 || tagged;
            if (count == _shownCount && Max == _shownMax && lit == _shownLit && tagged == _shownTag) return;
            _shownCount = count;
            _shownMax = Max;
            _shownLit = lit;
            _shownTag = tagged;
            Build(count, Mathf.Max(count, Max), lit);
        }

        /// <summary>True while the tag's sign is drawn (work order 015). For tests.</summary>
        public bool TagShown => _renderer != null && _renderer.enabled && _shownTag;

        void Build(int count, int slots, bool lit)
        {
            _verts.Clear();
            _colors.Clear();
            _tris.Clear();
            // Tag Round's tag (work order 015): a larger green diamond left of the row, while it lasts.
            if (_shownTag)
            {
                var at = new Vector3(-(slots + 1) * Spacing * 0.5f - 0.15f, 0f, 0f);
                Diamond(at, PipSize * 0.65f, Drawn(s_TagRim, RimGlow));
                Diamond(at + new Vector3(0f, 0f, -0.02f), PipSize * 0.65f * CoreShare, Drawn(s_TagCore, CoreGlow));
            }
            bool full = count >= Max;
            Color rim = Drawn(full ? s_FullRim : s_Rim, RimGlow);
            Color core = Drawn(full ? s_FullCore : s_Core, CoreGlow);
            Color empty = Drawn(s_Empty, 1f);
            float start = -(slots - 1) * Spacing * 0.5f;
            for (int i = 0; i < slots; i++)
            {
                var centre = new Vector3(start + i * Spacing, 0f, 0f);
                bool on = i < count && lit;
                // The camera looks along local +Z: the inner shape lies a little nearer it.
                Diamond(centre, PipSize * 0.5f, on ? rim : empty);
                if (on) Diamond(centre + new Vector3(0f, 0f, -0.02f), PipSize * 0.5f * CoreShare, core);
            }
            _mesh.Clear();
            _mesh.SetVertices(_verts);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_tris, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>A diamond in the local XY plane, drawn from both sides.</summary>
        void Diamond(Vector3 c, float r, Color color)
        {
            int b = _verts.Count;
            _verts.Add(c + new Vector3(0f, r, 0f));
            _verts.Add(c + new Vector3(r * 0.8f, 0f, 0f));
            _verts.Add(c + new Vector3(0f, -r, 0f));
            _verts.Add(c + new Vector3(-r * 0.8f, 0f, 0f));
            for (int i = 0; i < 4; i++) _colors.Add(color);
            _tris.AddRange(new[] { b, b + 1, b + 2, b, b + 2, b + 3 });
            _tris.AddRange(new[] { b, b + 2, b + 1, b, b + 3, b + 2 });
        }

        /// <summary>The vertex colour for a picked colour, made brighter by its glow, as the shot effects do it.</summary>
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
