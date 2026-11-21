using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The shapes and materials of the rooms' mood (work order 044), all made in code: no model, texture or shader is added.
    /// The materials use the shaders the game's code already asks for by name: URP Simple Lit (<see cref="PropFactory.ShaderName"/>)
    /// for what the light falls on (rubble, bones, candles, wax, the dais, the carpet, the coins), on one small palette
    /// texture made here so that a room's lit pieces are one mesh and one draw; and URP Particles/Unlit (as the door signs'
    /// fallback asks for it) with its blend set by its properties alone, no keyword: added light for the pools, the flames
    /// and the air, and a doubled multiply for the stains, cracks, missing tiles and patches (0.5 leaves the floor as it is,
    /// less darkens it, more lightens it).
    ///
    /// The lit pieces are faceted and flat-shaded, as the props' painted look: every triangle has its own vertices and its
    /// face's normal.
    /// </summary>
    public static class MoodShapes
    {
        public const string UnlitShaderName = "Universal Render Pipeline/Particles/Unlit";

        /// <summary>The palette's colours, by their place in the texture.</summary>
        public enum Paint { StoneDark, StoneLight, Bone, Wax, Gold, CarpetRed, CarpetTrim, DaisStone }

        static readonly Color[] s_palette =
        {
            new Color(0.30f, 0.27f, 0.26f), new Color(0.50f, 0.46f, 0.43f), new Color(0.86f, 0.80f, 0.66f), new Color(0.93f, 0.88f, 0.74f),
            new Color(1.00f, 0.78f, 0.30f), new Color(0.46f, 0.13f, 0.12f), new Color(0.74f, 0.55f, 0.30f), new Color(0.47f, 0.43f, 0.41f),
        };

        public static Vector2 Uv(Paint p) => new Vector2(((int)p + 0.5f) / s_palette.Length, 0.5f);

        /// <summary>The palette, 8 by 1, point filtered (the caller destroys it).</summary>
        public static Texture2D MakePalette()
        {
            var t = new Texture2D(s_palette.Length, 1, TextureFormat.RGBA32, false) { name = "Mood_Palette", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            t.SetPixels(s_palette);
            t.Apply(false, true);
            return t;
        }

        /// <summary>A soft round spot for the air's particles, its colour falling to black at its edge (the caller destroys it).</summary>
        public static Texture2D MakeSoftSpot()
        {
            const int n = 64;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { name = "Mood_SoftSpot", wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float d = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    float v = d * d * (3f - 2f * d);
                    px[y * n + x] = new Color(v, v, v, v);
                }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        /// <summary>A Simple Lit material on the palette, without smoothness or highlights, as the props' (the caller destroys it).</summary>
        public static Material MakeLit(Texture2D palette)
        {
            Shader shader = Shader.Find(PropFactory.ShaderName);
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = "Mood_Lit" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", palette);
            m.mainTexture = palette;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            m.color = Color.white;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return m;
        }

        public enum Blend { Add, Multiply2x }

        /// <summary>A Particles/Unlit material blended by its properties alone, drawn after the solid world, writing no depth (the caller destroys it).</summary>
        public static Material MakeUnlit(string name, Blend blend, Texture texture, int queue)
        {
            Shader shader = Shader.Find(UnlitShaderName);
            if (shader == null) shader = Shader.Find("Sprites/Default");
            var m = new Material(shader) { name = name };
            if (texture != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
                m.mainTexture = texture;
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.white);
            BlendMode src = blend == Blend.Add ? BlendMode.One : BlendMode.DstColor;
            BlendMode dst = blend == Blend.Add ? BlendMode.One : BlendMode.SrcColor;
            m.SetFloat("_SrcBlend", (float)src);
            m.SetFloat("_DstBlend", (float)dst);
            m.SetFloat("_SrcBlendAlpha", (float)src);
            m.SetFloat("_DstBlendAlpha", (float)dst);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)CullMode.Off);
            m.renderQueue = queue;
            return m;
        }

        /// <summary>A colour as picked, in the light the shaders add up in (linear when the project is), times a level.</summary>
        public static Color Linear(Rgb c, float level)
        {
            Color p = new Color(c.R, c.G, c.B, 1f);
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
            return new Color(l.r * level, l.g * level, l.b * level, 1f);
        }

        /// <summary>A multiply value (0.5 neutral) as the shader is given it: in linear light the vertex colour is read as picked.</summary>
        public static Color Mul(float v)
        {
            v = Mathf.Clamp01(v);
            return new Color(v, v, v, 1f);
        }

        /// <summary>
        /// A mesh being put together: lit triangles (flat-shaded, a palette colour each) or coloured ones (for the unlit
        /// materials). Wound so that each lit face looks away from the shape's middle and each flat one up.
        /// </summary>
        public sealed class Build
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public int Count => V.Count;
            public int Triangles => T.Count / 3;

            /// <summary>A lit face, wound away from <paramref name="middle"/>.</summary>
            public void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 middle, Paint paint)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-12f) return;
                if (Vector3.Dot(n, (a + b + c) / 3f - middle) < 0f)
                {
                    Vector3 t = b; b = c; c = t;
                    n = -n;
                }
                n.Normalize();
                Vector2 uv = Uv(paint);
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                U.Add(uv); U.Add(uv); U.Add(uv);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }

            /// <summary>A lit flat face, wound to look up.</summary>
            public void Up(Vector3 a, Vector3 b, Vector3 c, Paint paint) => Face(a, b, c, (a + b + c) / 3f - Vector3.up, paint);

            /// <summary>A coloured triangle; one that is flat is wound to look up. Returns its first vertex.</summary>
            public int Tri(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.y < 0f)
                {
                    Vector3 t = b; b = c; c = t;
                    Color tc = cb; cb = cc; cc = tc;
                }
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                C.Add(ca); C.Add(cb); C.Add(cc);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                return i;
            }

            public Mesh ToMesh(string name, bool lit)
            {
                var mesh = new Mesh { name = name };
                if (V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(V);
                if (lit)
                {
                    mesh.SetNormals(N);
                    mesh.SetUVs(0, U);
                }
                else mesh.SetColors(C);
                mesh.SetTriangles(T, 0);
                mesh.RecalculateBounds();
                return mesh;
            }
        }

        static float Jit(ref uint s)
        {
            s ^= s << 13;
            s ^= s >> 17;
            s ^= s << 5;
            return (s & 0xFFFFFF) / (float)0xFFFFFF; // 0..1
        }

        /// <summary>A faceted chunk of stone (or bone), a jittered box half sunk in the floor, turned by <paramref name="yaw"/>.</summary>
        public static void Chunk(Build b, Vector3 at, float size, float yaw, ref uint seed, Paint paint)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            float sx = size * (0.4f + 0.25f * Jit(ref seed)), sy = size * (0.25f + 0.2f * Jit(ref seed)), sz = size * (0.35f + 0.25f * Jit(ref seed));
            var p = new Vector3[8];
            for (int k = 0; k < 8; k++)
            {
                var c = new Vector3((k & 1) == 0 ? -sx : sx, (k & 2) == 0 ? -sy * 0.3f : sy, (k & 4) == 0 ? -sz : sz);
                c += new Vector3((Jit(ref seed) - 0.5f) * sx * 0.6f, (Jit(ref seed) - 0.5f) * sy * 0.5f, (Jit(ref seed) - 0.5f) * sz * 0.6f);
                p[k] = at + q * c;
            }
            Vector3 mid = at + Vector3.up * sy * 0.35f;
            int[,] quads = { { 0, 1, 3, 2 }, { 4, 6, 7, 5 }, { 0, 4, 5, 1 }, { 2, 3, 7, 6 }, { 0, 2, 6, 4 }, { 1, 5, 7, 3 } };
            for (int f = 0; f < 6; f++)
            {
                if (f == 0) continue; // the bottom, in the floor
                b.Face(p[quads[f, 0]], p[quads[f, 1]], p[quads[f, 2]], mid, paint);
                b.Face(p[quads[f, 0]], p[quads[f, 2]], p[quads[f, 3]], mid, paint);
            }
        }

        /// <summary>A prism of <paramref name="sides"/> sides standing on <paramref name="at"/>: its sides and its top.</summary>
        public static void Prism(Build b, Vector3 at, float radius, float height, int sides, float yaw, Paint side, Paint top)
        {
            Vector3 mid = at + Vector3.up * height * 0.5f, cap = at + Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (yaw + i * 360f / sides) * Mathf.Deg2Rad, a1 = (yaw + (i + 1) * 360f / sides) * Mathf.Deg2Rad;
                var d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius;
                var d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius;
                b.Face(at + d0, at + d1, cap + d1, mid, side);
                b.Face(at + d0, cap + d1, cap + d0, mid, side);
                b.Up(cap, cap + d0, cap + d1, top);
            }
        }

        /// <summary>A cone of <paramref name="sides"/> sides: a heap.</summary>
        public static void Cone(Build b, Vector3 at, float radius, float height, int sides, float yaw, Paint paint)
        {
            Vector3 tip = at + Vector3.up * height, mid = at + Vector3.up * height * 0.3f;
            for (int i = 0; i < sides; i++)
            {
                float a0 = (yaw + i * 360f / sides) * Mathf.Deg2Rad, a1 = (yaw + (i + 1) * 360f / sides) * Mathf.Deg2Rad;
                b.Face(at + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius, at + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius, tip, mid, paint);
            }
        }

        /// <summary>A box turned by <paramref name="yaw"/> standing on <paramref name="at"/>: its top and four sides.</summary>
        public static void Slab(Build b, Vector3 at, float width, float depth, float height, float yaw, Paint side, Paint top)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            float w = width * 0.5f, d = depth * 0.5f;
            Vector3 P(float x, float y, float z) => at + q * new Vector3(x, y, z);
            Vector3 mid = at + Vector3.up * height * 0.5f;
            Vector3 a = P(-w, 0, -d), bb = P(w, 0, -d), c = P(w, 0, d), dd = P(-w, 0, d);
            Vector3 a1 = P(-w, height, -d), b1 = P(w, height, -d), c1 = P(w, height, d), d1 = P(-w, height, d);
            b.Up(a1, b1, c1, top); b.Up(a1, c1, d1, top);
            b.Face(a, bb, b1, mid, side); b.Face(a, b1, a1, mid, side);
            b.Face(bb, c, c1, mid, side); b.Face(bb, c1, b1, mid, side);
            b.Face(c, dd, d1, mid, side); b.Face(c, d1, c1, mid, side);
            b.Face(dd, a, a1, mid, side); b.Face(dd, a1, d1, mid, side);
        }

        /// <summary>A flat lit polygon, jittered at its rim: a wax pool, a coin.</summary>
        public static void FlatBlob(Build b, Vector3 at, float radius, int sides, ref uint seed, Paint paint)
        {
            for (int i = 0; i < sides; i++)
            {
                float j0 = 0.75f + 0.25f * Jit(ref seed);
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                float j1 = i + 1 == sides ? 1f : 0.75f + 0.25f * Jit(ref seed);
                b.Up(at, at + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius * j0, at + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius * j1, paint);
            }
        }

        /// <summary>A bone: a thin four-sided shaft with a knob at each end, lying on the floor.</summary>
        public static void Bone(Build b, Vector3 at, float length, float yaw, ref uint seed)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            float r = length * 0.07f;
            Vector3 e0 = at + q * new Vector3(-length * 0.4f, r, 0f), e1 = at + q * new Vector3(length * 0.4f, r, 0f);
            Vector3 mid = (e0 + e1) * 0.5f;
            Vector3 side = q * Vector3.forward * r, up = Vector3.up * r;
            Vector3[] ring = { side, up, -side, -up };
            for (int i = 0; i < 4; i++)
            {
                Vector3 s0 = ring[i] * 0.7f, s1 = ring[(i + 1) % 4] * 0.7f;
                b.Face(e0 + s0, e1 + s0, e1 + s1, mid, Paint.Bone);
                b.Face(e0 + s0, e1 + s1, e0 + s1, mid, Paint.Bone);
            }
            Chunk(b, e0 - Vector3.up * r, r * 3.2f, yaw + 45f, ref seed, Paint.Bone);
            Chunk(b, e1 - Vector3.up * r, r * 3.2f, yaw - 45f, ref seed, Paint.Bone);
        }

        /// <summary>A soft coloured disc on the floor: brightest at its middle, black (nothing added) at its rim. Returns its first vertex and how many.</summary>
        public static void Pool(Build b, Vector3 at, float radius, Color colour, int segments, out int first, out int count)
        {
            first = b.Count;
            Color mid = colour * 0.5f, rim = Color.black;
            mid.a = rim.a = 1f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 d0 = new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)), d1 = new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1));
                b.Tri(at, at + d0 * radius * 0.45f, at + d1 * radius * 0.45f, colour, mid, mid);
                b.Tri(at + d0 * radius * 0.45f, at + d0 * radius, at + d1 * radius, mid, rim, rim);
                b.Tri(at + d0 * radius * 0.45f, at + d1 * radius, at + d1 * radius * 0.45f, mid, rim, mid);
            }
            count = b.Count - first;
        }

        /// <summary>A flame: two crossed upright teardrops, a hot core at its foot and its tip dimmer, seen from every side.</summary>
        public static void Flame(Build b, Vector3 at, float height, float width, Color core, Color tip, out int first, out int count)
        {
            first = b.Count;
            for (int k = 0; k < 2; k++)
            {
                Vector3 across = (k == 0 ? Vector3.right : Vector3.forward) * width;
                Vector3 bottom = at, top = at + Vector3.up * height, l = at + Vector3.up * height * 0.3f - across, r = at + Vector3.up * height * 0.3f + across;
                b.Tri(bottom, l, top, core, core * 0.7f, tip);
                b.Tri(bottom, top, r, core, tip, core * 0.7f);
            }
            count = b.Count - first;
        }

        /// <summary>A soft blob for the doubled multiply: <paramref name="middle"/> at its middle, neutral (0.5) at its jittered rim.</summary>
        public static void Blob(Build b, Vector3 at, float radius, Color middle, ref uint seed, int sides = 10)
        {
            Color rim = Mul(0.5f);
            float first = 0.7f + 0.3f * Jit(ref seed), prev = first;
            for (int i = 0; i < sides; i++)
            {
                float next = i + 1 == sides ? first : 0.6f + 0.4f * Jit(ref seed);
                float a0 = i * Mathf.PI * 2f / sides, a1 = (i + 1) * Mathf.PI * 2f / sides;
                b.Tri(at, at + new Vector3(Mathf.Sin(a0), 0f, Mathf.Cos(a0)) * radius * prev, at + new Vector3(Mathf.Sin(a1), 0f, Mathf.Cos(a1)) * radius * next, middle, rim, rim);
                prev = next;
            }
        }

        /// <summary>A crack for the doubled multiply: a jagged line of a few steps, dark, fading at its ends.</summary>
        public static void Crack(Build b, Vector3 at, float length, float yaw, ref uint seed)
        {
            var q = Quaternion.Euler(0f, yaw, 0f);
            int steps = 5;
            Color dark = Mul(0.14f), end = Mul(0.5f);
            Vector3 prev = at + q * new Vector3(-length * 0.5f, 0f, 0f);
            float w = 0.02f + 0.015f * Jit(ref seed);
            for (int i = 1; i <= steps; i++)
            {
                Vector3 next = at + q * new Vector3(-length * 0.5f + length * i / steps, 0f, i == steps ? 0f : (Jit(ref seed) - 0.5f) * length * 0.18f);
                Vector3 dir = (next - prev).normalized, side = Vector3.Cross(Vector3.up, dir) * w * (i == 1 || i == steps ? 0.6f : 1f);
                Color c0 = i == 1 ? end : dark, c1 = i == steps ? end : dark;
                b.Tri(prev - side, prev + side, next + side, c0, c0, c1);
                b.Tri(prev - side, next + side, next - side, c0, c1, c1);
                prev = next;
            }
        }

        /// <summary>The carpet: a long strip from <paramref name="from"/> to <paramref name="to"/>, red with a trim along its edges, its edges worn ragged.</summary>
        public static void Carpet(Build b, Vector3 from, Vector3 to, float width, ref uint seed)
        {
            Vector3 along = to - from;
            float length = along.magnitude;
            if (length < 0.1f) return;
            Vector3 dir = along / length, side = Vector3.Cross(Vector3.up, dir);
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.5f));
            float half = width * 0.5f, trim = width * 0.12f;
            float[] across = { -half, -half + trim, half - trim, half };
            Vector3[] prev = null;
            for (int i = 0; i <= steps; i++)
            {
                Vector3 c = from + dir * (length * i / steps);
                var row = new Vector3[4];
                for (int k = 0; k < 4; k++)
                {
                    float ragged = (k == 0 || k == 3) ? (Jit(ref seed) - 0.5f) * 0.1f : 0f;
                    row[k] = c + side * (across[k] + ragged);
                }
                if (prev != null)
                    for (int k = 0; k < 3; k++)
                    {
                        Paint p = k == 1 ? Paint.CarpetRed : Paint.CarpetTrim;
                        b.Up(prev[k], prev[k + 1], row[k + 1], p);
                        b.Up(prev[k], row[k + 1], row[k], p);
                    }
                prev = row;
            }
        }
    }
}
