using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>What one piece of an effect is to be: its shape, place, size, time and colours, and how it moves (work order 024).</summary>
    public struct FxPiece
    {
        public FxShape Shape;
        public FxMoment Moment;
        public FxTier Tier;
        /// <summary>Where it starts: the centre, the apex, the point hit; a streak's tail.</summary>
        public Vector3 At;
        /// <summary>A streak's pointed end. Others: unused.</summary>
        public Vector3 End;
        /// <summary>The flat direction of a flash, a wedge or side spikes.</summary>
        public Vector3 Direction;
        /// <summary>Its size in metres: a star's, a ring's radius, a streak's width, the length of side spikes' strip.</summary>
        public float Size;
        /// <summary>A wedge's angle in degrees; side spikes: the half width of their strip.</summary>
        public float Angle;
        /// <summary>Seconds it takes to go; for one held (<see cref="Holds"/>), the seconds after the hold.</summary>
        public float Seconds;
        public Color Rim, Core;
        public float RimGlow, CoreGlow;
        /// <summary>m/s it drifts, and m/s² it falls by.</summary>
        public Vector3 Velocity;
        public float Gravity;
        /// <summary>Metres it is drawn nearer the camera, so what it is on does not hide it.</summary>
        public float TowardCamera;
        /// <summary>Kept over this, at this offset, while it lives.</summary>
        public Transform Follow;
        public Vector3 FollowOffset;
        /// <summary>Held fully shown while this is true, up to <see cref="HoldMost"/> seconds; then it goes over its seconds.</summary>
        public Func<bool> Holds;
        public float HoldMost;
        /// <summary>Runs on real time, for what happens while the game is held by a choice.</summary>
        public bool Unscaled;
        /// <summary>Shards, converge, orbit: how many; and the size of each, in metres.</summary>
        public int Count;
        public float PieceSize;
        /// <summary>Orbit: degrees a second it turns.</summary>
        public float Spin;
        /// <summary>A trailing streak: the most metres it trails behind what it follows.</summary>
        public float TrailLength;
    }

    /// <summary>
    /// One piece of an effect of work order 024 (2026-11-20), in the style of the shot effects that Kristian approved on
    /// 2026-11-18 ("love the effects, exactly the style I had in mind"; <see cref="ShotFx"/>): a flat mesh made in code, an
    /// outer shape in a rim colour and an inner one in a core colour, with the shot effects' material, the core lifted
    /// above white so that the camera's bloom lights it and nothing else. No particle system, texture or shader. It is a
    /// picture only, with no collider, and runs on game time so the hit stop holds it (on real time if it says so).
    ///
    /// The shapes are <see cref="FxShape"/>: the shot effects' six, drawn exactly as they draw them, and new ones built
    /// the same way. A shape that does not change as it plays (a star, a diamond, the orbit, side spikes) shares one mesh
    /// per shape and colours with every piece like it, made once; one that is rebuilt as it plays (a band on the ground,
    /// shards, converging spikes, a streak) has its own, destroyed with it.
    /// </summary>
    public sealed class CombatFx : MonoBehaviour
    {
        const float CoreLift = 0.01f;      // m the inner shape lies in front of the outer one
        const float OutlineWidth = 0.22f;  // m
        const int MostShared = 64;         // shared meshes kept; past it a piece makes its own

        static readonly Dictionary<(FxShape, Color, Color), Mesh> s_Shared = new Dictionary<(FxShape, Color, Color), Mesh>();

        /// <summary>Pieces alive now, of every effect. For tests.</summary>
        public static int Alive { get; private set; }
        /// <summary>Meshes made once and shared. For tests: they are all that may stay after the pieces are gone.</summary>
        public static int SharedMeshes => s_Shared.Count;
        /// <summary>Raised as a piece goes, by its time or removed.</summary>
        public static event Action<CombatFx> Ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_Shared.Clear();
            Alive = 0;
            Ended = null;
        }

        FxPiece _p;
        Mesh _mesh;
        bool _ownMesh;
        float _age;
        bool _holding;
        bool _follows;
        float _held;
        Color _rim, _core;
        uint _seed;
        Vector3 _velocity;
        Quaternion _facing;

        public int Id { get; private set; }
        /// <summary>What it was made as.</summary>
        public FxPiece Piece => _p;
        public FxShape Shape => _p.Shape;
        public FxMoment Moment => _p.Moment;
        public float Age => _age;
        public bool IsHolding => _holding;
        /// <summary>The brightest channel of its core colour as drawn, in linear light. Above 1 it glows under bloom.</summary>
        public float CoreBrightness => Mathf.Max(_core.r, Mathf.Max(_core.g, _core.b));

        // ---------------- Making ----------------

        /// <summary>A piece, or null when it has no size or time.</summary>
        public static CombatFx Make(FxPiece p, int id, Material material)
        {
            if (p.Size <= 0f || (p.Seconds <= 0f && p.Holds == null)) return null;
            if (p.Shape == FxShape.Streak && (p.End - p.At).sqrMagnitude < 1e-6f && p.Follow == null) return null;
            var go = new GameObject("CombatFx_" + p.Moment + "_" + p.Shape);
            var fx = go.AddComponent<CombatFx>();
            fx.Id = id;
            fx._p = p;
            fx._seed = (uint)(id * 2654435761u) | 1u;
            fx._rim = Drawn(p.Rim, p.RimGlow);
            fx._core = Drawn(p.Core, p.CoreGlow);
            fx._velocity = p.Velocity;
            fx._holding = p.Holds != null;
            fx._follows = p.Follow != null;
            go.transform.position = p.At;
            var filter = go.AddComponent<MeshFilter>();
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            fx.Begin();
            filter.sharedMesh = fx._mesh;
            Alive++;
            fx.Animate(0f);
            return fx;
        }

        void Begin()
        {
            Vector3 at = _p.Follow != null ? _p.Follow.position + _p.FollowOffset : _p.At;
            switch (_p.Shape)
            {
                case FxShape.Flash:
                    _facing = AlongShot(at, Flat(_p.Direction).sqrMagnitude > 1e-6f ? Flat(_p.Direction).normalized : Vector3.forward);
                    Shared(() => BuildStar(new[] { 1f, 0.42f, 0.55f, 0.3f, 0.22f, 0.3f, 0.55f, 0.42f }, 0.16f, 0.12f, 0.55f));
                    break;
                case FxShape.Spark:
                    _facing = FacingCamera(at, Quaternion.Euler(0f, Next() * 360f, 0f) * Vector3.forward);
                    Shared(() => BuildStar(new[] { 1f, 0.5f, 0.8f, 0.5f, 1f, 0.5f, 0.8f, 0.5f }, 0.22f, 0f, 0.5f));
                    break;
                case FxShape.Spike:
                    // Its long spike straight up, as the camera sees it.
                    _facing = FacingCamera(at, Vector3.up);
                    Shared(() => BuildStar(new[] { 1f, 0.3f, 0.42f, 0.22f, 0.3f, 0.22f, 0.42f, 0.3f }, 0.13f, 0f, 0.5f));
                    break;
                case FxShape.Diamond:
                    _facing = FacingCamera(at, Vector3.up);
                    Shared(() => BuildStar(new[] { 1f, 0.75f, 1f, 0.75f }, 0.36f, 0f, 0.5f));
                    break;
                case FxShape.Orbit:
                    _facing = Quaternion.identity;
                    Shared(BuildOrbit);
                    break;
                case FxShape.SideSpikes:
                    _facing = Quaternion.LookRotation(Flat(_p.Direction).sqrMagnitude > 1e-6f ? Flat(_p.Direction).normalized : Vector3.forward, Vector3.up);
                    Shared(BuildSideSpikes);
                    break;
                case FxShape.Streak:
                {
                    Own();
                    Vector3 delta = _p.Follow != null ? Flat(_p.Direction) * Mathf.Max(0.01f, _p.TrailLength) : _p.End - _p.At;
                    float length = Mathf.Max(1e-3f, delta.magnitude);
                    _facing = AlongShot(_p.Follow != null ? at : _p.End, delta / length);
                    BuildStreak(Mathf.Clamp(_p.Size * 2.5f / length, 0.02f, 0.4f));
                    break;
                }
                case FxShape.Shards:
                case FxShape.Converge:
                    _facing = FacingCamera(at, Quaternion.Euler(0f, Next() * 360f, 0f) * Vector3.forward);
                    Own();
                    break;
                default: // the bands on the ground: wedge, ring, outline, closing
                    _facing = Quaternion.identity;
                    Own();
                    break;
            }
            transform.rotation = _facing;
        }

        void Own()
        {
            _mesh = new Mesh { name = gameObject.name };
            _mesh.MarkDynamic();
            _ownMesh = true;
        }

        /// <summary>The shared mesh of this shape in these colours, made once; past the most kept, one of its own.</summary>
        void Shared(Action build)
        {
            var key = (_p.Shape, _rim, _core);
            if (s_Shared.TryGetValue(key, out Mesh mesh) && mesh != null)
            {
                _mesh = mesh;
                return;
            }
            _mesh = new Mesh { name = "CombatFx_" + _p.Shape };
            build();
            if (s_Shared.Count < MostShared) s_Shared[key] = _mesh;
            else _ownMesh = true;
        }

        /// <summary>
        /// The vertex colour for a picked colour, as the shot effects make it: converted from the colour space it was
        /// picked in, then made brighter by its glow in linear light.
        /// </summary>
        static Color Drawn(Color picked, float glow)
        {
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? picked.linear : picked;
            float k = Mathf.Max(0f, glow);
            return new Color(c.r * k, c.g * k, c.b * k, 1f);
        }

        /// <summary>Local +X along the direction as seen from the camera, local +Z at the camera (the shot effects' rule).</summary>
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

        /// <summary>Local +X exactly along the direction, local Y across it as seen from the camera (the shot effects' rule).</summary>
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

        /// <summary>The next number of its own small source, 0 to 1: never Unity's Random, which the game's own draws use.</summary>
        float Next()
        {
            _seed ^= _seed << 13;
            _seed ^= _seed >> 17;
            _seed ^= _seed << 5;
            return (_seed & 0xFFFFFF) / (float)0x1000000;
        }

        // ---------------- Running ----------------

        void Update()
        {
            float dt = _p.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            if (_holding)
            {
                _held += dt;
                bool holds;
                try { holds = _p.Holds(); }
                catch (Exception) { holds = false; }
                // What it follows gone (killed, its level removed): it lets go where it was.
                if (!holds || _held >= _p.HoldMost || (_follows && _p.Follow == null)) _holding = false;
                if (_holding)
                {
                    Animate(0f);
                    return;
                }
            }
            _age += dt;
            float t = _p.Seconds > 0f ? _age / _p.Seconds : 1f;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Animate(t);
        }

        void OnDestroy()
        {
            Alive = Mathf.Max(0, Alive - 1);
            if (_ownMesh && _mesh != null) Destroy(_mesh);
            Ended?.Invoke(this);
        }

        void Animate(float t)
        {
            float dt = _p.Unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
            Vector3 at;
            if (_follows) at = _p.Follow != null ? _p.Follow.position + _p.FollowOffset : transform.position - TowardCameraOffset();
            else
            {
                _velocity += Vector3.down * (_p.Gravity * (t > 0f ? dt : 0f));
                at = t > 0f ? transform.position - TowardCameraOffset() + _velocity * dt : _p.At;
            }
            transform.position = at + TowardCameraOffset();

            switch (_p.Shape)
            {
                case FxShape.Flash:
                case FxShape.Spark:
                case FxShape.Spike:
                case FxShape.Diamond:
                    // A quick pop to full size, then a shrink to nothing, as the shot effects' flash and spark.
                    float pop = t < 0.2f ? Mathf.Lerp(0.7f, 1.1f, t / 0.2f) : 1.1f * (1f - Sq((t - 0.2f) / 0.8f));
                    transform.localScale = Vector3.one * (_p.Size * pop);
                    break;
                case FxShape.Streak:
                    if (_follows)
                    {
                        // A trail: its point on what it follows, its tail back along the way it came, no longer than its most.
                        float trail = Mathf.Min(_p.TrailLength, Flat(at - _p.At).magnitude) * (1f - t * t);
                        transform.rotation = _facing;
                        transform.localScale = new Vector3(Mathf.Max(1e-3f, trail), _p.Size * (1f - t), 1f);
                    }
                    else
                    {
                        transform.position = _p.End + TowardCameraOffset();
                        float length = Vector3.Distance(_p.At, _p.End) * (1f - t * t);
                        transform.localScale = new Vector3(Mathf.Max(1e-3f, length), _p.Size * (1f - t), 1f);
                    }
                    break;
                case FxShape.Wedge:
                    float outer = _p.Size * Mathf.Lerp(0.55f, 1f, EaseOut(Mathf.Clamp01(t / 0.35f)));
                    BuildBand(outer * Mathf.Pow(t, 1.5f), outer, _p.Angle, jagged: true, coreShare: 0.12f);
                    break;
                case FxShape.Ring:
                    float edge = _p.Size * Mathf.Lerp(0.25f, 1f, EaseOut(t));
                    BuildBand(edge - _p.Size * 0.35f * (1f - t), edge, 360f, jagged: false);
                    break;
                case FxShape.Outline:
                    BuildBand(_p.Size - Mathf.Min(_p.Size, OutlineWidth) * (1f - t * t), _p.Size, 360f, jagged: false);
                    break;
                case FxShape.Closing:
                    // Closing in to its centre over its whole time, as thin as an outline.
                    float rim = _p.Size * (1f - t);
                    BuildBand(rim - Mathf.Min(rim, OutlineWidth), rim, 360f, jagged: false, coreShare: 0.4f);
                    break;
                case FxShape.Shards:
                    BuildShards(t);
                    break;
                case FxShape.Converge:
                    BuildConverge(t);
                    break;
                case FxShape.Orbit:
                    transform.rotation = Quaternion.Euler(0f, (_held + _age) * _p.Spin, 0f);
                    transform.localScale = Vector3.one * (_p.Size * (1f - t));
                    break;
                case FxShape.SideSpikes:
                    float out_ = t < 0.2f ? Mathf.Lerp(0.6f, 1.1f, t / 0.2f) : 1.1f * (1f - Sq((t - 0.2f) / 0.8f));
                    transform.localScale = new Vector3(Mathf.Max(1e-3f, _p.Angle * out_), 1f, _p.Size);
                    break;
            }
        }

        Vector3 TowardCameraOffset()
        {
            if (_p.TowardCamera <= 0f) return Vector3.zero;
            Camera cam = Camera.main;
            if (cam == null) return Vector3.zero;
            Vector3 from = _p.Follow != null ? _p.Follow.position + _p.FollowOffset : _p.At;
            return (cam.transform.position - from).normalized * _p.TowardCamera;
        }

        // ---------------- Shapes ----------------

        /// <summary>
        /// The shot effects' star: around the origin in the local XY plane, its first spike along +X; spike lengths in
        /// order around it, the edge coming in to <paramref name="inner"/> between them; the core the same shape at
        /// <paramref name="coreScale"/>; all moved <paramref name="shift"/> along +X.
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

        /// <summary>The shot effects' streak: a tapered bar along local -X from its point at the origin to its tail at -1.</summary>
        void BuildStreak(float head)
        {
            const float body = 0.55f;
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
        /// The shot effects' band on the ground, from <paramref name="inner"/> to <paramref name="outer"/> metres over
        /// <paramref name="angle"/> degrees about the direction, its outer <paramref name="coreShare"/> core-coloured.
        /// </summary>
        void BuildBand(float inner, float outer, float angle, bool jagged, float coreShare = 0.28f)
        {
            outer = Mathf.Max(0f, outer);
            inner = Mathf.Clamp(inner, 0f, outer);
            float mid = Mathf.Lerp(inner, outer, 1f - coreShare);
            int segments = Mathf.Max(8, Mathf.CeilToInt(angle / 4f));
            bool closed = angle >= 359.9f;
            Vector3 forward = Flat(_p.Direction).sqrMagnitude > 1e-6f ? Flat(_p.Direction).normalized : Vector3.forward;
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

        /// <summary>
        /// Shards: <see cref="FxPiece.Count"/> small triangles in the local XY plane (facing the camera), each pointing
        /// out along its own way from the centre, flying out to <see cref="FxPiece.Size"/> metres as they shrink away.
        /// </summary>
        void BuildShards(float t)
        {
            int n = Mathf.Max(1, _p.Count);
            var vertices = new Vector3[n * 6];
            var colors = new Color[vertices.Length];
            var triangles = new int[n * 6];
            uint seed = (uint)Id * 747796405u + 2891336453u;
            float fly = EaseOut(t);
            float shrink = 1f - t * t;
            for (int i = 0; i < n; i++)
            {
                float j1 = Hash(ref seed), j2 = Hash(ref seed);
                float a = (i + 0.25f + 0.5f * j1) / n * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var side = new Vector3(-d.y, d.x, 0f);
                float dist = _p.Size * fly * (0.7f + 0.3f * j2);
                float s = _p.PieceSize * shrink * (0.8f + 0.4f * j1);
                Vector3 c = d * dist;
                for (int layer = 0; layer < 2; layer++)
                {
                    float k = layer == 0 ? 1f : 0.5f;
                    Vector3 z = layer == 0 ? Vector3.zero : new Vector3(0f, 0f, CoreLift);
                    int v = i * 6 + layer * 3;
                    vertices[v] = c + d * (s * k) + z;
                    vertices[v + 1] = c - d * (s * 0.5f * k) + side * (s * 0.45f * k) + z;
                    vertices[v + 2] = c - d * (s * 0.5f * k) - side * (s * 0.45f * k) + z;
                    colors[v] = colors[v + 1] = colors[v + 2] = layer == 0 ? _rim : _core;
                    triangles[v] = v;
                    triangles[v + 1] = v + 1;
                    triangles[v + 2] = v + 2;
                }
            }
            SetMesh(vertices, colors, triangles);
        }

        /// <summary>
        /// Converging spikes: <see cref="FxPiece.Count"/> thin spikes in the local XY plane (facing the camera), pointing
        /// in at the centre from <see cref="FxPiece.Size"/> metres out, pulling in to it and shrinking: the eye is drawn
        /// to the centre.
        /// </summary>
        void BuildConverge(float t)
        {
            int n = Mathf.Max(3, _p.Count);
            var vertices = new Vector3[n * 6];
            var colors = new Color[vertices.Length];
            var triangles = new int[n * 6];
            float p = t * t; // slow, then fast: pulled in
            float tip = _p.Size * 0.3f * (1f - p);
            float tail = _p.Size * (1f - 0.85f * p);
            float width = _p.Size * 0.07f * (1f - 0.5f * t);
            for (int i = 0; i < n; i++)
            {
                float a = (i + 0.5f) / n * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f);
                var side = new Vector3(-d.y, d.x, 0f);
                for (int layer = 0; layer < 2; layer++)
                {
                    float k = layer == 0 ? 1f : 0.4f;
                    Vector3 z = layer == 0 ? Vector3.zero : new Vector3(0f, 0f, CoreLift);
                    int v = i * 6 + layer * 3;
                    vertices[v] = d * tip + z;
                    vertices[v + 1] = d * tail + side * (width * k) + z;
                    vertices[v + 2] = d * tail - side * (width * k) + z;
                    colors[v] = colors[v + 1] = colors[v + 2] = layer == 0 ? _rim : _core;
                    triangles[v] = v;
                    triangles[v + 1] = v + 1;
                    triangles[v + 2] = v + 2;
                }
            }
            SetMesh(vertices, colors, triangles);
        }

        /// <summary>
        /// The orbit: five small four-pointed stars evenly on a level circle of radius 1, each lying level so the camera
        /// above sees it; the piece turns it about its centre.
        /// </summary>
        void BuildOrbit()
        {
            const int stars = 5, points = 4;
            const float star = 0.36f, inner = 0.14f;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int s = 0; s < stars; s++)
            {
                float a = s * Mathf.PI * 2f / stars;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                for (int layer = 0; layer < 2; layer++)
                {
                    float k = layer == 0 ? 1f : 0.5f;
                    Vector3 lift = Vector3.up * (layer == 0 ? 0f : CoreLift);
                    int b = vertices.Count;
                    vertices.Add(c + lift);
                    colors.Add(layer == 0 ? _rim : _core);
                    for (int i = 0; i < 2 * points; i++)
                    {
                        float q = Mathf.PI * i / points + a;
                        float r = (i % 2 == 0 ? star : inner) * k;
                        vertices.Add(c + new Vector3(Mathf.Cos(q) * r, 0f, Mathf.Sin(q) * r) + lift);
                        colors.Add(layer == 0 ? _rim : _core);
                        triangles.Add(b);
                        triangles.Add(b + 1 + i);
                        triangles.Add(b + 1 + (i + 1) % (2 * points));
                    }
                }
            }
            SetMesh(vertices.ToArray(), colors.ToArray(), triangles.ToArray());
        }

        /// <summary>
        /// Side spikes: level triangles off both edges of a strip along local +Z from 0 to 1, its edges at local x = ±1;
        /// the piece scales x by the strip's half width and z by its length.
        /// </summary>
        void BuildSideSpikes()
        {
            const int perSide = 5;
            var vertices = new List<Vector3>();
            var colors = new List<Color>();
            var triangles = new List<int>();
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < perSide; i++)
                {
                    float z = (i + 0.5f + (side > 0 ? 0.25f : -0.25f)) / perSide;
                    float half = 0.045f, reach = i % 2 == 0 ? 0.75f : 0.5f;
                    for (int layer = 0; layer < 2; layer++)
                    {
                        float k = layer == 0 ? 1f : 0.45f;
                        Vector3 lift = Vector3.up * (layer == 0 ? 0f : CoreLift);
                        int b = vertices.Count;
                        vertices.Add(new Vector3(side * 0.85f, 0f, z - half * k) + lift);
                        vertices.Add(new Vector3(side * (0.85f + reach * k), 0f, z) + lift);
                        vertices.Add(new Vector3(side * 0.85f, 0f, z + half * k) + lift);
                        for (int c = 0; c < 3; c++) colors.Add(layer == 0 ? _rim : _core);
                        triangles.Add(b);
                        triangles.Add(b + 1);
                        triangles.Add(b + 2);
                    }
                }
            }
            SetMesh(vertices.ToArray(), colors.ToArray(), triangles.ToArray());
        }

        void SetMesh(Vector3[] vertices, Color[] colors, int[] triangles)
        {
            _mesh.Clear();
            _mesh.vertices = vertices;
            _mesh.colors = colors;
            _mesh.triangles = triangles;
            _mesh.RecalculateBounds();
        }

        static float Hash(ref uint state)
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0xFFFFFF) / (float)0x1000000;
        }

        static float Sq(float x) => x * x;
        static float EaseOut(float x) => 1f - Sq(1f - x);
        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
