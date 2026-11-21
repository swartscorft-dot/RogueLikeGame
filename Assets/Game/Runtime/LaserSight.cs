using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The Laser Sight's line (work order 060; decision 0014, the producer's proposal, a draft Kristian has not ruled on; his
    /// own example of 2026-11-20, "laser sikte"): while the part is fitted in a run, a thin line in her colour runs from her
    /// gun along her aim to where the basic shot would end now, and where it ends on a target a small, brighter square shows
    /// it (<see cref="PlayerCharacter.TickLaser"/> says when and where; this only draws).
    ///
    /// Code-made, no asset, as <see cref="QuickDuckRim"/> is: a flat band <see cref="Width"/> across lying level at the
    /// height of the gun, in her warm orange (<see cref="QuickDuckRim.Colour"/>) at <see cref="Glow"/>, and the square
    /// <see cref="DotSize"/> across at <see cref="DotGlow"/>, both dim enough to stay under the bloom, drawn with the shot
    /// effects' material (unlit, in the colours of its vertices). Two objects, made the first time it shows, in the scene
    /// (they go with it); each mesh is made once, and a frame only moves, turns and stretches them, so nothing is allocated
    /// per frame. All first guesses.
    /// </summary>
    public sealed class LaserSight
    {
        /// <summary>Metres across the line.</summary>
        public const float Width = 0.035f;
        /// <summary>The line's brightness: under the bloom's threshold.</summary>
        public const float Glow = 0.6f;
        /// <summary>Metres across the square where it ends on a target.</summary>
        public const float DotSize = 0.15f;
        /// <summary>The square's brightness, brighter than the line, still under the bloom.</summary>
        public const float DotGlow = 0.9f;

        GameObject _go, _dot;
        MeshRenderer _renderer, _dotRenderer;
        Mesh _mesh, _dotMesh;

        /// <summary>True while the line shows.</summary>
        public bool Shown => _go != null && _go.activeSelf;
        /// <summary>Where it starts and ends, in the world (at the gun's height), while it shows; the last ones after.</summary>
        public Vector3 From { get; private set; }
        public Vector3 To { get; private set; }
        /// <summary>The target it ends on, or null (it ends at cover, or at the shot's full reach).</summary>
        public Damageable Target { get; private set; }
        /// <summary>True while the square shows on a target.</summary>
        public bool DotShown => _dot != null && _dot.activeSelf;
        /// <summary>The line's object, or null before it first showed; for tests.</summary>
        public GameObject Object => _go;
        /// <summary>Frames it was shown; for tests.</summary>
        public int Shows { get; private set; }

        /// <summary>Shows it from <paramref name="from"/> to <paramref name="to"/>, ending on <paramref name="target"/> (or null), drawn with <paramref name="material"/>.</summary>
        public void Show(Vector3 from, Vector3 to, Damageable target, Material material)
        {
            Vector3 along = to - from;
            along.y = 0f;
            float length = along.magnitude;
            if (length < 1e-3f)
            {
                Hide();
                return;
            }
            if (_go == null) Make();
            From = from;
            To = to;
            Target = target;
            Shows++;
            Quaternion turn = Quaternion.LookRotation(along / length, Vector3.up);
            _go.transform.SetPositionAndRotation(from, turn);
            _go.transform.localScale = new Vector3(1f, 1f, length);
            if (!_go.activeSelf) _go.SetActive(true);
            bool dot = target != null;
            if (dot) _dot.transform.SetPositionAndRotation(to, turn);
            if (_dot.activeSelf != dot) _dot.SetActive(dot);
            if (material != null && _renderer.sharedMaterial != material)
            {
                _renderer.sharedMaterial = material;
                _dotRenderer.sharedMaterial = material;
            }
            bool drawn = _renderer.sharedMaterial != null;
            if (_renderer.enabled != drawn)
            {
                _renderer.enabled = drawn;
                _dotRenderer.enabled = drawn;
            }
        }

        /// <summary>Hides it.</summary>
        public void Hide()
        {
            Target = null;
            if (_go != null && _go.activeSelf) _go.SetActive(false);
            if (_dot != null && _dot.activeSelf) _dot.SetActive(false);
        }

        /// <summary>Destroys its objects and meshes, from her teardown (as the quick duck's rim, work order 047).</summary>
        public void Dispose()
        {
            Target = null;
            if (_go != null) UnityEngine.Object.Destroy(_go);
            if (_dot != null) UnityEngine.Object.Destroy(_dot);
            if (_mesh != null) UnityEngine.Object.Destroy(_mesh);
            if (_dotMesh != null) UnityEngine.Object.Destroy(_dotMesh);
            _go = _dot = null;
            _renderer = _dotRenderer = null;
            _mesh = _dotMesh = null;
        }

        void Make()
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? QuickDuckRim.Colour.linear : QuickDuckRim.Colour;
            // The line: a band across x, from 0 to 1 along z, stretched to its length; the square: centred.
            _mesh = Band("LaserSight", -Width * 0.5f, Width * 0.5f, 0f, 1f, Scaled(c, Glow));
            _dotMesh = Band("LaserSightDot", -DotSize * 0.5f, DotSize * 0.5f, -DotSize * 0.5f, DotSize * 0.5f, Scaled(c, DotGlow));
            _go = Holder("LaserSight", _mesh, out _renderer);
            _dot = Holder("LaserSightDot", _dotMesh, out _dotRenderer);
        }

        static Color Scaled(Color c, float glow) => new Color(c.r * glow, c.g * glow, c.b * glow, 1f);

        static GameObject Holder(string name, Mesh mesh, out MeshRenderer renderer)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            renderer = go.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.enabled = false;
            go.SetActive(false);
            return go;
        }

        /// <summary>A flat rectangle lying level, x from <paramref name="x0"/> to <paramref name="x1"/>, z from <paramref name="z0"/> to <paramref name="z1"/>; both windings.</summary>
        static Mesh Band(string name, float x0, float x1, float z0, float z1, Color c)
        {
            var mesh = new Mesh { name = name };
            mesh.vertices = new[] { new Vector3(x0, 0f, z0), new Vector3(x1, 0f, z0), new Vector3(x1, 0f, z1), new Vector3(x0, 0f, z1) };
            mesh.colors = new[] { c, c, c, c };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2, 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
