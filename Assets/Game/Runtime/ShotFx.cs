using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    public enum ShotFxKind { Flash, Streak, Spark, Wedge, Ring, Outline }

    /// <summary>
    /// The colours of an effect: the outer shape in the rim colour and the inner shape in the core colour, each
    /// made brighter by its glow. A glow above 1 lifts the colour above white in the HDR picture, so the bloom
    /// on the camera picks it up and nothing else in the scene; at 1 the colour is exactly as picked.
    /// </summary>
    public struct ShotPaint
    {
        public Color Rim, Core;
        public float RimGlow, CoreGlow;

        public ShotPaint(Color rim, Color core, float rimGlow = 1f, float coreGlow = 1f)
        {
            Rim = rim;
            Core = core;
            RimGlow = rimGlow;
            CoreGlow = coreGlow;
        }
    }

    /// <summary>
    /// One short, flat effect of a shot, drawn as a mesh with an outer shape in the rim colour and an inner
    /// shape in the core colour, and removed when its time is up. It is only a picture: it has no collider
    /// and the hit has already landed when it appears. It runs on game time, so the hit stop holds it.
    /// - Flash: a spiky star at the muzzle, its long spike along the shot. It pops and shrinks.
    /// - Streak: a tapered bar from the muzzle to where the shot went, widest by its pointed end.
    ///   It appears whole, then pulls into its end and thins out.
    /// - Spark: a round spiky star, where a target was hit or as the burst of an explosion. It pops and shrinks.
    /// - Wedge: a jagged, level slice of a disc from its apex over a cone, that blows outward to its edge.
    /// - Ring: a ring on the ground that grows to the edge of a circle and thins out: a shockwave.
    /// - Outline: a thin ring on the ground exactly at the edge of a circle, that thins out: the area hit.
    /// Flash, streak and spark face the camera.
    /// </summary>
    public sealed class ShotFx : MonoBehaviour
    {
        const float GroundHeight = 0.06f;
        const float CoreLift = 0.01f; // m the inner shape lies in front of the outer one
        const float OutlineWidth = 0.22f; // m

        Mesh _mesh;
        float _age;
        Color _rim, _core;

        /// <summary>Every effect spawned since the game started. For tests.</summary>
        public static int Spawned { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Spawned = 0;

        public ShotFxKind Kind { get; private set; }
        /// <summary>Where it starts: the muzzle (flash, streak), the point hit (spark), the apex (wedge), the centre (ring, outline).</summary>
        public Vector3 Origin { get; private set; }
        /// <summary>Streak: where the shot went. Others: the same as the origin.</summary>
        public Vector3 End { get; private set; }
        /// <summary>The flat direction of the shot or the cone. Zero for a spark, a ring or an outline.</summary>
        public Vector3 Direction { get; private set; }
        /// <summary>Flash, spark: its size. Streak: its width. Wedge, ring, outline: its radius.</summary>
        public float Size { get; private set; }
        /// <summary>Wedge: its angle in degrees.</summary>
        public float Angle { get; private set; }
        public float Seconds { get; private set; }
        public float Age => _age;
        /// <summary>The brightest channel of its core colour as drawn, in linear light. Above 1 it glows under bloom.</summary>
        public float CoreBrightness => Mathf.Max(_core.r, Mathf.Max(_core.g, _core.b));

        // ---------------- Spawning ----------------

        public static ShotFx Flash(Vector3 muzzle, Vector3 direction, float size, float seconds, ShotPaint paint, Material material)
        {
            if (size <= 0f || seconds <= 0f) return null;
            ShotFx fx = Create(ShotFxKind.Flash, muzzle, muzzle, direction, size, seconds, paint, material);
            fx.transform.rotation = AlongShot(muzzle, direction);
            fx.BuildStar(new[] { 1f, 0.42f, 0.55f, 0.3f, 0.22f, 0.3f, 0.55f, 0.42f }, 0.16f, 0.12f, 0.55f);
            fx.Animate(0f);
            return fx;
        }

        /// <summary>A spark at a point. It is drawn <paramref name="towardCamera"/> metres nearer the camera, so what it is on does not hide it.</summary>
        public static ShotFx Spark(Vector3 at, float size, float seconds, ShotPaint paint, Material material, float towardCamera = 0f)
        {
            if (size <= 0f || seconds <= 0f) return null;
            ShotFx fx = Create(ShotFxKind.Spark, at, at, Vector3.zero, size, seconds, paint, material);
            Camera cam = Camera.main;
            if (cam != null && towardCamera > 0f) fx.transform.position = at + (cam.transform.position - at).normalized * towardCamera;
            Vector3 spin = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            fx.transform.rotation = FacingCamera(at, spin);
            fx.BuildStar(new[] { 1f, 0.5f, 0.8f, 0.5f, 1f, 0.5f, 0.8f, 0.5f }, 0.22f, 0f, 0.5f);
            fx.Animate(0f);
            return fx;
        }

        public static ShotFx Streak(Vector3 from, Vector3 to, float width, float seconds, ShotPaint paint, Material material)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (width <= 0f || seconds <= 0f || length < 1e-3f) return null;
            ShotFx fx = Create(ShotFxKind.Streak, from, to, delta / length, width, seconds, paint, material);
            fx.transform.position = to; // the pointed end stays where the shot went; the tail pulls in toward it
            fx.transform.rotation = AlongShot(to, delta / length);
            fx.BuildStreak(Mathf.Clamp(width * 2.5f / length, 0.02f, 0.4f));
            fx.Animate(0f);
            return fx;
        }

        /// <summary>A level wedge from its apex, at the apex's height, over <paramref name="angle"/> degrees around the direction.</summary>
        public static ShotFx Wedge(Vector3 apex, Vector3 direction, float angle, float radius, float seconds, ShotPaint paint, Material material)
        {
            if (radius <= 0f || seconds <= 0f || angle <= 0f) return null;
            ShotFx fx = Create(ShotFxKind.Wedge, apex, apex, Flat(direction).normalized, radius, seconds, paint, material);
            fx.Angle = angle;
            fx.Animate(0f);
            return fx;
        }

        public static ShotFx Ring(Vector3 centre, float radius, float seconds, ShotPaint paint, Material material)
        {
            if (radius <= 0f || seconds <= 0f) return null;
            Vector3 ground = new Vector3(centre.x, GroundHeight, centre.z);
            ShotFx fx = Create(ShotFxKind.Ring, ground, ground, Vector3.zero, radius, seconds, paint, material);
            fx.Animate(0f);
            return fx;
        }

        public static ShotFx Outline(Vector3 centre, float radius, float seconds, ShotPaint paint, Material material)
        {
            if (radius <= 0f || seconds <= 0f) return null;
            Vector3 ground = new Vector3(centre.x, GroundHeight * 0.5f, centre.z);
            ShotFx fx = Create(ShotFxKind.Outline, ground, ground, Vector3.zero, radius, seconds, paint, material);
            fx.Animate(0f);
            return fx;
        }

        static ShotFx Create(ShotFxKind kind, Vector3 origin, Vector3 end, Vector3 direction, float size, float seconds,
                             ShotPaint paint, Material material)
        {
            var go = new GameObject("ShotFx_" + kind);
            go.transform.position = origin;
            var fx = go.AddComponent<ShotFx>();
            fx.Kind = kind;
            fx.Origin = origin;
            fx.End = end;
            fx.Direction = direction;
            fx.Size = size;
            fx.Seconds = seconds;
            fx._rim = Drawn(paint.Rim, paint.RimGlow);
            fx._core = Drawn(paint.Core, paint.CoreGlow);
            fx._mesh = new Mesh { name = go.name };
            fx._mesh.MarkDynamic();
            go.AddComponent<MeshFilter>().sharedMesh = fx._mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            Spawned++;
            LamplitView.ShotWentOff(kind, origin, direction, size, fx._core); // work order 085: at a lamplit level the shot lights the room
            return fx;
        }

        /// <summary>
        /// The vertex colour for a picked colour. Vertex colours are not converted from the colour space they
        /// were picked in, so that is done here and the colour shows on screen as it looks in the Inspector.
        /// The glow multiplies it afterward, in linear light, so the hue stays and only the brightness rises.
        /// </summary>
        static Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>
        /// A rotation whose local +X lies along the direction and whose local +Z points at the camera, so the
        /// flat shape is seen face on. Without a camera the shape lies flat, facing up.
        /// </summary>
        static Quaternion FacingCamera(Vector3 at, Vector3 direction)
        {
            Camera cam = Camera.main;
            Vector3 toCamera = cam != null ? (cam.transform.position - at).normalized : Vector3.up;
            Vector3 along = direction - Vector3.Dot(direction, toCamera) * toCamera;
            if (along.sqrMagnitude < 1e-6f) along = Vector3.Cross(toCamera, Vector3.right);
            along.Normalize();
            Vector3 side = Vector3.Cross(toCamera, along);
            return Quaternion.LookRotation(toCamera, side);
        }

        /// <summary>
        /// A rotation whose local +X lies exactly along the shot and whose local Y is across it as seen from
        /// the camera, so a flat shape along the shot covers its real path and shows its full width.
        /// </summary>
        static Quaternion AlongShot(Vector3 at, Vector3 direction)
        {
            Camera cam = Camera.main;
            Vector3 toCamera = cam != null ? (cam.transform.position - at).normalized : Vector3.up;
            Vector3 along = direction.normalized;
            Vector3 side = Vector3.Cross(toCamera, along);
            if (side.sqrMagnitude < 1e-6f) side = Vector3.Cross(Vector3.up, along);
            if (side.sqrMagnitude < 1e-6f) return Quaternion.identity;
            side.Normalize();
            return Quaternion.LookRotation(Vector3.Cross(along, side), side);
        }

        // ---------------- Running ----------------

        void Update()
        {
            _age += Time.deltaTime;
            float t = _age / Seconds;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Animate(t);
        }

        void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }

        void Animate(float t)
        {
            switch (Kind)
            {
                case ShotFxKind.Flash:
                case ShotFxKind.Spark:
                    // A quick pop to full size, then a shrink to nothing.
                    float pop = t < 0.2f ? Mathf.Lerp(0.7f, 1.1f, t / 0.2f) : 1.1f * (1f - Sq((t - 0.2f) / 0.8f));
                    transform.localScale = Vector3.one * (Size * pop);
                    break;
                case ShotFxKind.Streak:
                    float length = Vector3.Distance(Origin, End) * (1f - t * t);
                    transform.localScale = new Vector3(Mathf.Max(1e-3f, length), Size * (1f - t), 1f);
                    break;
                case ShotFxKind.Wedge:
                    float outer = Size * Mathf.Lerp(0.55f, 1f, EaseOut(Mathf.Clamp01(t / 0.35f)));
                    BuildBand(outer * Mathf.Pow(t, 1.5f), outer, Angle, jagged: true, coreShare: 0.12f);
                    break;
                case ShotFxKind.Ring:
                    float edge = Size * Mathf.Lerp(0.25f, 1f, EaseOut(t));
                    BuildBand(edge - Size * 0.35f * (1f - t), edge, 360f, jagged: false);
                    break;
                case ShotFxKind.Outline:
                    BuildBand(Size - Mathf.Min(Size, OutlineWidth) * (1f - t * t), Size, 360f, jagged: false);
                    break;
            }
        }

        // ---------------- Shapes ----------------

        /// <summary>
        /// A star around the origin in the local XY plane, its first spike along +X. Spike lengths are given
        /// in order around it; between two spikes the edge comes in to <paramref name="inner"/>. The inner,
        /// core-coloured star is the same shape at <paramref name="coreScale"/>. All of it is moved
        /// <paramref name="shift"/> along +X.
        /// </summary>
        void BuildStar(float[] spikes, float inner, float shift, float coreScale)
        {
            int n = spikes.Length;
            var vertices = new Vector3[2 * (1 + 2 * n)];
            var colors = new Color[vertices.Length];
            var triangles = new int[2 * 3 * 2 * n];
            int tri = 0;
            for (int layer = 0; layer < 2; layer++)
            {
                float scale = layer == 0 ? 1f : coreScale;
                float z = layer == 0 ? 0f : CoreLift;
                Color c = layer == 0 ? _rim : _core;
                int b = layer * (1 + 2 * n);
                vertices[b] = new Vector3(shift, 0f, z);
                colors[b] = c;
                for (int i = 0; i < 2 * n; i++)
                {
                    float a = Mathf.PI * i / n;
                    float r = (i % 2 == 0 ? spikes[i / 2] : inner) * scale;
                    vertices[b + 1 + i] = new Vector3(shift + Mathf.Cos(a) * r, Mathf.Sin(a) * r, z);
                    colors[b + 1 + i] = c;
                    triangles[tri++] = b;
                    triangles[tri++] = b + 1 + i;
                    triangles[tri++] = b + 1 + (i + 1) % (2 * n);
                }
            }
            SetMesh(vertices, colors, triangles);
        }

        /// <summary>
        /// A tapered bar along local -X from its pointed end at the origin to its tail at -1, one unit wide
        /// at the shoulder, which lies <paramref name="head"/> of the length behind the point.
        /// </summary>
        void BuildStreak(float head)
        {
            // Outline, from the point back: the shoulder, a body that narrows a little, then a taper to the tail.
            const float body = 0.55f; // where the taper to the tail begins
            var vertices = new[]
            {
                new Vector3(0f, 0f, 0f), new Vector3(-head, 0.5f, 0f), new Vector3(-head, -0.5f, 0f),
                new Vector3(-body, 0.36f, 0f), new Vector3(-body, -0.36f, 0f), new Vector3(-1f, 0f, 0f),
                new Vector3(-head * 0.35f, 0f, CoreLift), new Vector3(-head * 1.2f, 0.2f, CoreLift), new Vector3(-head * 1.2f, -0.2f, CoreLift),
                new Vector3(-body, 0.13f, CoreLift), new Vector3(-body, -0.13f, CoreLift), new Vector3(-0.9f, 0f, CoreLift),
            };
            var colors = new[] { _rim, _rim, _rim, _rim, _rim, _rim, _core, _core, _core, _core, _core, _core };
            var triangles = new[]
            {
                0, 1, 2, 1, 3, 2, 2, 3, 4, 3, 5, 4,
                6, 7, 8, 7, 9, 8, 8, 9, 10, 9, 11, 10,
            };
            SetMesh(vertices, colors, triangles);
        }

        /// <summary>
        /// A band on the ground from <paramref name="inner"/> to <paramref name="outer"/> metres, over
        /// <paramref name="angle"/> degrees centred on the direction (all the way round for 360). The outer
        /// <paramref name="coreShare"/> of it is core-coloured. A jagged band has a spiky outer edge.
        /// </summary>
        void BuildBand(float inner, float outer, float angle, bool jagged, float coreShare = 0.28f)
        {
            inner = Mathf.Clamp(inner, 0f, outer);
            float mid = Mathf.Lerp(inner, outer, 1f - coreShare);
            int segments = Mathf.Max(8, Mathf.CeilToInt(angle / 4f));
            bool closed = angle >= 359.9f;
            Vector3 forward = Direction.sqrMagnitude > 0f ? Direction : Vector3.forward;
            var vertices = new Vector3[(segments + 1) * 4];
            var colors = new Color[vertices.Length];
            var triangles = new int[segments * 12];
            int tri = 0;
            for (int i = 0; i <= segments; i++)
            {
                float yaw = closed ? 360f * i / segments : Mathf.Lerp(-angle * 0.5f, angle * 0.5f, (float)i / segments);
                Vector3 d = Quaternion.Euler(0f, yaw, 0f) * forward;
                float spike = jagged && i % 2 == 1 && i < segments ? 1.12f : 1f;
                int v = i * 4;
                vertices[v] = d * inner;
                vertices[v + 1] = d * mid;
                vertices[v + 2] = d * mid + Vector3.up * CoreLift;
                vertices[v + 3] = d * (mid + (outer - mid) * spike) + Vector3.up * CoreLift;
                colors[v] = colors[v + 1] = _rim;
                colors[v + 2] = colors[v + 3] = _core;
                if (i == segments) break;
                for (int band = 0; band < 2; band++)
                {
                    int a = v + band * 2, c = a + 4;
                    triangles[tri++] = a; triangles[tri++] = a + 1; triangles[tri++] = c + 1;
                    triangles[tri++] = a; triangles[tri++] = c + 1; triangles[tri++] = c;
                }
            }
            SetMesh(vertices, colors, triangles);
        }

        void SetMesh(Vector3[] vertices, Color[] colors, int[] triangles)
        {
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
        }

        static float Sq(float x) => x * x;
        static float EaseOut(float x) => 1f - Sq(1f - x);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
