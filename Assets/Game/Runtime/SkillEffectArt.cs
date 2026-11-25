using System;
using System.Diagnostics;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// What her skills' smoke, fire, dust, embers and the keg's starburst are drawn from in the drawn look (work order 126,
    /// 2026-11-25): the project's own shader (<c>Resources/SkillEffects/SkillEffectToon.shader</c>, in a Resources folder so that a
    /// built player keeps it), one texture of shapes and one of tiling noise, both made here in code, and one flat square per
    /// shape. No asset from outside.
    ///
    /// The texture's channels are fields, not pictures (the shader cuts them at named thresholds, one screen pixel wide), in half
    /// floats (eight bits stepped the edges into stairs in the first pictures): r the shape's edge field (0.5 on its outline, a
    /// distance scaled so that its deepest point is <see cref="Deepest"/>), g its tone field (smoke and dust: how a lump faces
    /// the room's light, each lump a sphere lit from the upper left; fire: how hot, in rings from its middle; the starburst: the
    /// inner star's own edge field), b a smooth low noise (how ragged an eaten edge is), a how far its front stands toward the
    /// camera (a share of its half: the shader's depth, so it meets the floor and figures in a curve). Shapes: four puffs of smoke
    /// (a cauliflower of round lumps), four fire balls, two flame tongues, an ember, the starburst (the shot effects' own star,
    /// its spikes and its inner star, at twice the size).
    ///
    /// Made once a run, when the first scene loads with the look on (about a quarter of a second, at loading, not in a fight),
    /// or else the first time an effect is drawn.
    /// </summary>
    public static class SkillEffectArt
    {
        /// <summary>Where the shader is, under Resources, and its name.</summary>
        public const string ShaderPath = "SkillEffects/SkillEffectToon";
        public const string ShaderName = "Roguelike/Skill Effect Toon";

        /// <summary>The shapes, each a flat square of half-width 1 in its local XY plane.</summary>
        public enum Shape { Puff0, Puff1, Puff2, Puff3, Fire0, Fire1, Fire2, Fire3, Tongue0, Tongue1, Ember, Star }

        public const int PuffShapes = 4, FireShapes = 4, TongueShapes = 2;

        /// <summary>The starburst's longest spike reaches this share of its square's half (the rest is margin).</summary>
        public const float StarFill = 0.94f;

        /// <summary>The edge field at a shape's deepest point.</summary>
        public const float Deepest = 0.86f;

        // Texels a shape's cell is across (the starburst's twice): 160 holds a puff two metres across at the close camera at
        // about two screen pixels a texel; the fields are smooth, so that is sharp, and it is made in a quarter of a second.
        const int Cell = 160, Cells = 4, Size = Cell * Cells, NoiseSize = 128, Mips = 4;
        // The room's light on a lump, in the square's own axes (x right, y up, z toward the camera): from the upper left, as the
        // game's key light falls in the practice room and the toon look's lit side is.
        static readonly Vector3 Light = new Vector3(-0.45f, 0.62f, 0.64f).normalized;

        static Texture2D s_atlas, s_noise;
        static Mesh[] s_quads;
        static Shader s_shader;
        static bool s_looked;
        static float[] s_field;

        /// <summary>How long the textures took to make, ms (for the report).</summary>
        public static float BuildMilliseconds { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_atlas = s_noise = null;
            s_quads = null;
            s_shader = null;
            s_looked = false;
        }

        /// <summary>Made as the first scene loads, with the look on: its quarter of a second is spent at loading, not in a fight.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Warm()
        {
            if (SkillLookValues.EffectsNotShapes && SkillLookValues.Live.ToonLook && Ready) Ensure();
        }

        /// <summary>The shader, or null where it is missing or not supported (the effects then draw as work order 123 drew them).</summary>
        public static Shader Shader
        {
            get
            {
                if (s_looked) return s_shader;
                s_looked = true;
                s_shader = Resources.Load<Shader>(ShaderPath);
                if (s_shader == null) s_shader = Shader.Find(ShaderName);
                if (s_shader != null && !s_shader.isSupported) s_shader = null;
                return s_shader;
            }
        }

        public static bool Ready => Shader != null;

        /// <summary>A new material on the shader with the two textures (one for every piece; each piece's own values go in a property block).</summary>
        public static Material MakeMaterial()
        {
            Ensure();
            var m = new Material(Shader) { name = "SkillEffects_Drawn" };
            m.SetTexture("_MainTex", s_atlas);
            m.SetTexture("_NoiseTex", s_noise);
            m.renderQueue = 3000;
            return m;
        }

        public static Mesh Quad(Shape shape)
        {
            Ensure();
            return s_quads[(int)shape];
        }

        static void Ensure()
        {
            if (s_atlas != null && s_noise != null && s_quads != null) return;
            var clock = Stopwatch.StartNew();
            s_field = PeriodicNoise(NoiseSize, 7);
            var noisePx = new Color32[NoiseSize * NoiseSize];
            for (int i = 0; i < noisePx.Length; i++)
            {
                byte b = (byte)Math.Round(Clamp01(s_field[i]) * 255f);
                noisePx[i] = new Color32(b, b, b, 255);
            }
            s_noise = new Texture2D(NoiseSize, NoiseSize, TextureFormat.RGBA32, true, true)
            {
                name = "SkillEffects_Noise", wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear,
                hideFlags = HideFlags.DontSave,
            };
            s_noise.SetPixels32(noisePx);
            s_noise.Apply(true, true);

            var px = new Color[Size * Size];
            for (int v = 0; v < PuffShapes; v++) Lumps(px, v, 0, v, fire: false);
            for (int v = 0; v < FireShapes; v++) Lumps(px, v, 1, 100 + v, fire: true);
            for (int v = 0; v < TongueShapes; v++) Tongue(px, v, 2, v);
            Ember(px, 0, 3);
            Star(px);
            s_atlas = new Texture2D(Size, Size, TextureFormat.RGBAHalf, Mips, true)
            {
                name = "SkillEffects_Shapes", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 2,
                hideFlags = HideFlags.DontSave,
            };
            s_atlas.SetPixels(px);
            s_atlas.Apply(true, true);

            s_quads = new Mesh[Enum.GetValues(typeof(Shape)).Length];
            for (int i = 0; i < s_quads.Length; i++)
            {
                var shape = (Shape)i;
                int col, row, span = 1;
                if (shape <= Shape.Puff3) { col = i; row = 0; }
                else if (shape <= Shape.Fire3) { col = i - (int)Shape.Fire0; row = 1; }
                else if (shape <= Shape.Tongue1) { col = i - (int)Shape.Tongue0; row = 2; }
                else if (shape == Shape.Ember) { col = 0; row = 3; }
                else { col = 2; row = 2; span = 2; }
                s_quads[i] = BuildQuad("SkillEffects_" + shape, col / (float)Cells, row / (float)Cells, span / (float)Cells);
            }
            s_field = null;
            BuildMilliseconds = (float)clock.Elapsed.TotalMilliseconds;
        }

        static Mesh BuildQuad(string name, float u0, float v0, float span)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { new Vector3(-1f, -1f, 0f), new Vector3(1f, -1f, 0f), new Vector3(1f, 1f, 0f), new Vector3(-1f, 1f, 0f) };
            mesh.uv = new[] { new Vector2(u0, v0), new Vector2(u0 + span, v0), new Vector2(u0 + span, v0 + span), new Vector2(u0, v0 + span) };
            mesh.uv2 = new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        // ---------------- The shapes ----------------

        /// <summary>
        /// A puff of smoke (round lumps round a larger middle one, each a sphere: its tone is how it faces the light) or a fire ball
        /// (fewer, larger lumps: rounder; its tone is how hot, in rings from a point a little under its middle, made uneven by the
        /// noise), in cell (<paramref name="col"/>, <paramref name="row"/>).
        /// </summary>
        static void Lumps(Color[] px, int col, int row, int seed, bool fire)
        {
            var rnd = new System.Random(17 + seed * 31);
            int k = fire ? 5 : 6 + (seed % 2);
            var cx = new float[k + 1];
            var cy = new float[k + 1];
            var r = new float[k + 1];
            var z = new float[k + 1];
            cx[0] = 0f; cy[0] = fire ? -0.04f : -0.05f; r[0] = fire ? 0.62f : 0.54f; z[0] = 0f;
            double turn = rnd.NextDouble() * Math.PI * 2.0;
            for (int j = 1; j <= k; j++)
            {
                double a = turn + Math.PI * 2.0 * (j - 1) / k + (rnd.NextDouble() - 0.5) * 0.5;
                float dist = (fire ? 0.36f : 0.4f) + 0.06f * (float)rnd.NextDouble();
                float rr = (fire ? 0.36f : 0.3f) + 0.08f * (float)rnd.NextDouble() + 0.05f * (float)Math.Sin(a);
                cx[j] = (float)Math.Cos(a) * dist;
                cy[j] = (float)Math.Sin(a) * dist * 0.92f - 0.02f;
                float reach = (float)Math.Sqrt(cx[j] * cx[j] + cy[j] * cy[j]);
                r[j] = Math.Min(rr, 0.93f - reach);
                z[j] = -0.06f + 0.1f * (float)rnd.NextDouble();
            }
            var d = new float[Cell * Cell];
            var tone = new float[Cell * Cell];
            var front = new float[Cell * Cell];
            float deepest = 1e-4f, highest = 1e-4f;
            float ox = (float)rnd.NextDouble(), oy = (float)rnd.NextDouble();
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float lx = (x + 0.5f) / Cell * 2f - 1f, ly = (y + 0.5f) / Cell * 2f - 1f;
                float bestD = float.NegativeInfinity, bestH = float.NegativeInfinity, nearest = float.PositiveInfinity;
                int win = 0, near = 0;
                for (int j = 0; j <= k; j++)
                {
                    float dx = lx - cx[j], dy = ly - cy[j];
                    float q = (float)Math.Sqrt(dx * dx + dy * dy);
                    float dj = r[j] - q;
                    if (dj > bestD) bestD = dj;
                    if (q < r[j])
                    {
                        float h = z[j] + (float)Math.Sqrt(r[j] * r[j] - q * q);
                        if (h > bestH) { bestH = h; win = j; }
                    }
                    if (-dj < nearest) { nearest = -dj; near = j; }
                }
                int i = y * Cell + x;
                d[i] = bestD;
                if (bestD > deepest) deepest = bestD;
                front[i] = Math.Max(0f, bestH);
                if (front[i] > highest) highest = front[i];
                int w = bestH > float.NegativeInfinity ? win : near;
                float nx = (lx - cx[w]) / r[w], ny = (ly - cy[w]) / r[w];
                float len = nx * nx + ny * ny;
                if (len > 1f) { float s = 1f / (float)Math.Sqrt(len); nx *= s; ny *= s; len = 1f; }
                float nz = (float)Math.Sqrt(Math.Max(0f, 1f - len));
                if (fire)
                {
                    // Hottest a little under the middle (fire rises: its top is cooler), in rings made uneven by the noise.
                    float hx = lx, hy = ly + 0.12f;
                    float radial = 1f - (float)Math.Sqrt(hx * hx + hy * hy) / 0.9f;
                    tone[i] = Clamp01(0.08f + 0.95f * radial + 0.22f * (Noise(lx * 0.25f + oy, ly * 0.25f + ox) - 0.5f));
                }
                else tone[i] = 0.5f + 0.5f * (nx * Light.x + ny * Light.y + nz * Light.z);
            }
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                int i = y * Cell + x;
                float lx = (x + 0.5f) / Cell * 2f - 1f, ly = (y + 0.5f) / Cell * 2f - 1f;
                Put(px, col, row, x, y, 0.5f + d[i] * ((Deepest - 0.5f) / deepest), tone[i], Noise(lx * 0.22f + ox, ly * 0.22f + oy), front[i] / highest);
            }
        }

        /// <summary>A flame tongue: a teardrop pointing up, its tip bent to one side; hottest low and deep in it.</summary>
        static void Tongue(Color[] px, int col, int row, int variant)
        {
            const int points = 72;
            var xs = new float[points];
            var ys = new float[points];
            float bend = variant == 0 ? 0.2f : -0.14f, m = variant == 0 ? 1.5f : 1.8f, a = variant == 0 ? 0.62f : 0.58f, b = 0.9f;
            for (int i = 0; i < points; i++)
            {
                double t = Math.PI * 2.0 * i / points;
                float yy = b * (float)Math.Cos(t);
                float xx = a * (float)Math.Sin(t) * (float)Math.Pow(Math.Abs(Math.Sin(t * 0.5)), m);
                xx += bend * (yy + b) * (yy + b) / (4f * b * b) * (yy > 0f ? 1f : 0.3f);
                xs[i] = xx;
                ys[i] = yy;
            }
            var d = new float[Cell * Cell];
            float deepest = 1e-4f;
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float lx = (x + 0.5f) / Cell * 2f - 1f, ly = (y + 0.5f) / Cell * 2f - 1f;
                float dd = PolygonDistance(lx, ly, xs, ys);
                d[y * Cell + x] = dd;
                if (dd > deepest) deepest = dd;
            }
            float o = 0.37f * (variant + 1);
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                int i = y * Cell + x;
                float lx = (x + 0.5f) / Cell * 2f - 1f, ly = (y + 0.5f) / Cell * 2f - 1f;
                float depth = Math.Max(0f, d[i]) / deepest;
                float g = Clamp01(0.55f * depth + 0.5f * (0.45f - 0.5f * ly));
                Put(px, col, row, x, y, 0.5f + d[i] * ((Deepest - 0.5f) / deepest), g, Noise(lx * 0.22f + o, ly * 0.22f - o), (float)Math.Sqrt(depth));
            }
        }

        /// <summary>An ember: a round spark, hottest in its middle.</summary>
        static void Ember(Color[] px, int col, int row)
        {
            const float radius = 0.8f;
            for (int y = 0; y < Cell; y++)
            for (int x = 0; x < Cell; x++)
            {
                float lx = (x + 0.5f) / Cell * 2f - 1f, ly = (y + 0.5f) / Cell * 2f - 1f;
                float dd = radius - (float)Math.Sqrt(lx * lx + ly * ly);
                Put(px, col, row, x, y, 0.5f + dd * ((Deepest - 0.5f) / radius), Clamp01(dd / radius), 0.5f, 0f);
            }
        }

        /// <summary>
        /// The starburst: the shot effects' spark star (ShotFx.Spark: eight spikes of 1, 0.5, 0.8, 0.5 ... round, the edge coming in to
        /// 0.22 between them, the first along +X; its inner star the same at half the size), in the two-by-two cells at the upper
        /// right. Its edge field is the outer star's, its tone field the inner star's.
        /// </summary>
        static void Star(Color[] px)
        {
            float[] spikes = { 1f, 0.5f, 0.8f, 0.5f, 1f, 0.5f, 0.8f, 0.5f };
            const float inner = 0.22f;
            int n = spikes.Length;
            var xs = new float[2 * n];
            var ys = new float[2 * n];
            var xi = new float[2 * n];
            var yi = new float[2 * n];
            for (int i = 0; i < 2 * n; i++)
            {
                double a = Math.PI * i / n;
                float rr = (i % 2 == 0 ? spikes[i / 2] : inner) * StarFill;
                xs[i] = (float)Math.Cos(a) * rr;
                ys[i] = (float)Math.Sin(a) * rr;
                xi[i] = xs[i] * 0.5f;
                yi[i] = ys[i] * 0.5f;
            }
            const int span = Cell * 2, x0 = Cell * 2, y0 = Cell * 2;
            const float k = 1.2f;
            for (int y = 0; y < span; y++)
            for (int x = 0; x < span; x++)
            {
                float lx = (x + 0.5f) / span * 2f - 1f, ly = (y + 0.5f) / span * 2f - 1f;
                float outer = PolygonDistance(lx, ly, xs, ys), core = PolygonDistance(lx, ly, xi, yi);
                px[(y0 + y) * Size + x0 + x] = new Color(Clamp01(0.5f + outer * k), Clamp01(0.5f + core * k), 0.5f, 0f);
            }
        }

        // ---------------- Helpers ----------------

        static void Put(Color[] px, int col, int row, int x, int y, float r, float g, float b, float a)
        {
            px[(row * Cell + y) * Size + col * Cell + x] = new Color(Clamp01(r), Clamp01(g), Clamp01((b - 0.2f) / 0.6f), Clamp01(a));
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

        /// <summary>Signed distance to a closed polygon (inside positive).</summary>
        static float PolygonDistance(float px, float py, float[] xs, float[] ys)
        {
            int n = xs.Length;
            float best = float.PositiveInfinity;
            bool inside = false;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float ex = xs[i] - xs[j], ey = ys[i] - ys[j];
                float wx = px - xs[j], wy = py - ys[j];
                float t = (wx * ex + wy * ey) / Math.Max(1e-9f, ex * ex + ey * ey);
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float dx = wx - ex * t, dy = wy - ey * t;
                float dd = dx * dx + dy * dy;
                if (dd < best) best = dd;
                if ((ys[i] > py) != (ys[j] > py) && px < (xs[j] - xs[i]) * (py - ys[i]) / (ys[j] - ys[i]) + xs[i]) inside = !inside;
            }
            float dist = (float)Math.Sqrt(best);
            return inside ? dist : -dist;
        }

        /// <summary>The tiling noise read at (u, v) in tiles (bilinear, wrapping).</summary>
        static float Noise(float u, float v)
        {
            float fx = u * NoiseSize, fy = v * NoiseSize;
            int ix = (int)Math.Floor(fx), iy = (int)Math.Floor(fy);
            float tx = fx - ix, ty = fy - iy;
            int x0 = ((ix % NoiseSize) + NoiseSize) % NoiseSize, y0 = ((iy % NoiseSize) + NoiseSize) % NoiseSize;
            int x1 = (x0 + 1) % NoiseSize, y1 = (y0 + 1) % NoiseSize;
            float a = s_field[y0 * NoiseSize + x0], b = s_field[y0 * NoiseSize + x1], c = s_field[y1 * NoiseSize + x0], d = s_field[y1 * NoiseSize + x1];
            float top = a + (b - a) * tx, bottom = c + (d - c) * tx;
            return top + (bottom - top) * ty;
        }

        /// <summary>Smooth value noise that tiles, three octaves (4, 8 and 16 cells across), stretched to 0 to 1.</summary>
        static float[] PeriodicNoise(int size, int seed)
        {
            var field = new float[size * size];
            var rnd = new System.Random(seed);
            float amp = 0.5f;
            for (int o = 0; o < 3; o++)
            {
                int cells = 4 << o;
                var lattice = new float[cells * cells];
                for (int i = 0; i < lattice.Length; i++) lattice[i] = (float)rnd.NextDouble();
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float fx = x * cells / (float)size, fy = y * cells / (float)size;
                    int ix = (int)fx, iy = (int)fy;
                    float tx = fx - ix, ty = fy - iy;
                    tx = tx * tx * (3f - 2f * tx);
                    ty = ty * ty * (3f - 2f * ty);
                    int x1 = (ix + 1) % cells, y1 = (iy + 1) % cells;
                    float a = lattice[iy * cells + ix], b = lattice[iy * cells + x1], c = lattice[y1 * cells + ix], d = lattice[y1 * cells + x1];
                    field[y * size + x] += amp * ((a + (b - a) * tx) * (1f - ty) + (c + (d - c) * tx) * ty);
                }
                amp *= 0.5f;
            }
            float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
            foreach (float f in field) { if (f < lo) lo = f; if (f > hi) hi = f; }
            for (int i = 0; i < field.Length; i++) field[i] = (field[i] - lo) / Math.Max(1e-5f, hi - lo);
            return field;
        }
    }
}
