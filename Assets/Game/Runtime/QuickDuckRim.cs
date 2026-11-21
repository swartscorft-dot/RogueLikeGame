using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The quick duck's rim light (work order 043, Kristian's answer of 2026-11-20 to the cover report: "it is not
    /// intuitive which props"): while a tap of the cover key would duck her, the nearest low prop in reach
    /// (<see cref="PlayerCharacter.DuckRimOn"/>) gets a faint lit edge, so she knows before she presses. Only that one.
    ///
    /// Code-made, no asset: a thin flat band in her colour (the effects' warm orange, <see cref="CombatFxValues.HerRim"/>,
    /// as <see cref="PlayerSigns"/> draws her own signs) lying round the prop's footprint just outside it, at
    /// <see cref="TopShare"/> of the height of its top: an edge on the prop, not a mark on the floor. Drawn with the shot
    /// effects' material (unlit, in the colours of its vertices), dim enough to stay under the bloom.
    ///
    /// One object, made the first time it shows, in the scene (it goes with the scene); its mesh is made again only when
    /// the prop it is on changes, into lists it keeps, so nothing is allocated per frame. All first guesses.
    /// </summary>
    public sealed class QuickDuckRim
    {
        /// <summary>Metres across the band.</summary>
        public const float Width = 0.07f;
        /// <summary>Metres from the footprint's edge to the band's inner edge.</summary>
        public const float Outset = 0.03f;
        /// <summary>The band's height as a share of the height of the prop's top.</summary>
        public const float TopShare = 0.9f;
        /// <summary>Its brightness: faint, under the bloom's threshold.</summary>
        public const float Glow = 0.85f;
        /// <summary>Her colour: the effects' warm orange (<see cref="CombatFxValues.HerRim"/>).</summary>
        public static readonly Color Colour = new Color(1f, 0.6f, 0.15f);

        const int CircleSegments = 40;
        static readonly float[] s_CornerX = { -1f, 1f, 1f, -1f }, s_CornerZ = { -1f, -1f, 1f, 1f }; // a box's corners in turn

        GameObject _go;
        MeshFilter _filter;
        MeshRenderer _renderer;
        Mesh _mesh;
        readonly List<Vector3> _vertices = new List<Vector3>();
        readonly List<Color> _colors = new List<Color>();
        readonly List<int> _triangles = new List<int>();
        readonly List<Renderer> _found = new List<Renderer>();

        /// <summary>The prop it is on, or null while it is not shown.</summary>
        public Shelter On { get; private set; }
        /// <summary>True while it shows on a prop.</summary>
        public bool Shown => On != null && _go != null && _go.activeSelf;
        /// <summary>The middle of the footprint it lies round, at its height, in the world; and that height over the ground.</summary>
        public Vector3 Centre { get; private set; }
        public float Height { get; private set; }
        /// <summary>The object it is drawn with, or null before it first showed; for tests.</summary>
        public GameObject Object => _go;

        /// <summary>Shows it on <paramref name="prop"/>, whose footprint is <paramref name="shape"/>, drawn with <paramref name="material"/>.</summary>
        public void Show(Shelter prop, CoverShape shape, Material material)
        {
            if (prop == null)
            {
                Hide();
                return;
            }
            if (_go == null) Make();
            if (prop != On || !_go.activeSelf)
            {
                On = prop;
                Build(prop, shape);
                _go.SetActive(true);
            }
            if (material != null && _renderer.sharedMaterial != material) _renderer.sharedMaterial = material;
            _renderer.enabled = _renderer.sharedMaterial != null;
        }

        /// <summary>Hides it.</summary>
        public void Hide()
        {
            On = null;
            if (_go != null && _go.activeSelf) _go.SetActive(false);
        }

        /// <summary>
        /// Destroys its object and its mesh, from her teardown (the third review of 2026-11-20, work order 047): the scene
        /// took the object, but nothing took the mesh, so one was left per Play session with domain reload off.
        /// </summary>
        public void Dispose()
        {
            On = null;
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
            _go = null;
            _filter = null;
            _renderer = null;
            _mesh = null;
        }

        void Make()
        {
            _go = new GameObject("QuickDuckRim");
            _filter = _go.AddComponent<MeshFilter>();
            _renderer = _go.AddComponent<MeshRenderer>();
            _renderer.shadowCastingMode = ShadowCastingMode.Off;
            _renderer.receiveShadows = false;
            if (_mesh == null)
            {
                _mesh = new Mesh { name = "QuickDuckRim" };
                _mesh.MarkDynamic();
            }
            _filter.sharedMesh = _mesh;
            _go.SetActive(false);
        }

        /// <summary>The band round the footprint, in the world, at its height on this prop.</summary>
        void Build(Shelter prop, CoverShape shape)
        {
            Height = TopOf(prop) * TopShare;
            Centre = new Vector3(shape.Centre.X, Height, shape.Centre.Z);
            _vertices.Clear();
            _colors.Clear();
            _triangles.Clear();
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? Colour.linear : Colour;
            c = new Color(c.r * Glow, c.g * Glow, c.b * Glow, 1f);
            if (shape.Kind == CoverShapeKind.Circle)
            {
                float inner = shape.Radius + Outset, outer = inner + Width;
                for (int i = 0; i < CircleSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / CircleSegments;
                    var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    _vertices.Add(Centre + d * inner);
                    _vertices.Add(Centre + d * outer);
                }
            }
            else
            {
                // The box's own axes on the ground, as CoverShape turns them: its x and its z.
                float yaw = shape.Yaw * Mathf.Deg2Rad;
                var right = new Vector3(Mathf.Cos(yaw), 0f, -Mathf.Sin(yaw));
                var forward = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                float hx = shape.Width * 0.5f, hz = shape.Depth * 0.5f;
                for (int i = 0; i < 4; i++)
                {
                    // Each corner, out by the outset and by the band, along both of its faces (a mitred corner).
                    _vertices.Add(Centre + right * (s_CornerX[i] * (hx + Outset)) + forward * (s_CornerZ[i] * (hz + Outset)));
                    _vertices.Add(Centre + right * (s_CornerX[i] * (hx + Outset + Width)) + forward * (s_CornerZ[i] * (hz + Outset + Width)));
                }
            }
            int n = _vertices.Count / 2;
            for (int i = 0; i < _vertices.Count; i++) _colors.Add(c);
            for (int i = 0; i < n; i++)
            {
                int a = i * 2, b = ((i + 1) % n) * 2;
                // Both windings: seen from above at any turn.
                _triangles.Add(a); _triangles.Add(a + 1); _triangles.Add(b + 1);
                _triangles.Add(a); _triangles.Add(b + 1); _triangles.Add(b);
                _triangles.Add(a); _triangles.Add(b + 1); _triangles.Add(a + 1);
                _triangles.Add(a); _triangles.Add(b); _triangles.Add(b + 1);
            }
            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetColors(_colors);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.RecalculateBounds();
        }

        /// <summary>The height of the prop's top over the ground: its drawn model's, else its collider's.</summary>
        float TopOf(Shelter prop)
        {
            float top = float.NegativeInfinity;
            prop.GetComponentsInChildren(false, _found);
            foreach (Renderer r in _found)
                if (r != null && r.gameObject != _go) top = Mathf.Max(top, r.bounds.max.y);
            _found.Clear();
            if (float.IsNegativeInfinity(top) && prop.TryGetComponent(out Collider c)) top = c.bounds.max.y;
            return float.IsNegativeInfinity(top) ? 1f : Mathf.Max(0.1f, top - prop.transform.position.y);
        }
    }
}
