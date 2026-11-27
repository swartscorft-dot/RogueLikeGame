using System.Collections.Generic;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The sight's shapes as one mesh of coloured triangles (work order 144): arc bands with radial ends, discs and convex
    /// polygons, each edge fading over <see cref="Feather"/> (the box filter part A's pictures were reduced with), drawn in the
    /// order they are added. In the interface's layout px round the sight's middle, y down, angles in degrees clockwise from the
    /// top. IMGUI's rounded rectangles cannot cut an arc with radial ends or draw the arm's head, hence a mesh; its lists are made
    /// once and filled again only when the sight changes, so nothing is allocated in a frame.
    /// </summary>
    sealed class SightShapes
    {
        public readonly List<Vector3> Points = new List<Vector3>(8192);
        public readonly List<Color32> Colours = new List<Color32>(8192);
        public readonly List<int> Triangles = new List<int>(24576);

        /// <summary>Layout px an edge fades over (a screen pixel at the screen's scale).</summary>
        public float Feather = 1f;
        /// <summary>Degrees between an arc's points.</summary>
        public float Step = 3f;

        const int MostCorners = 32;
        readonly Vector2[] _a = new Vector2[MostCorners], _b = new Vector2[MostCorners], _c = new Vector2[MostCorners];

        public void Clear()
        {
            Points.Clear();
            Colours.Clear();
            Triangles.Clear();
        }

        /// <summary>The point <paramref name="r"/> out from <paramref name="c"/> at <paramref name="degrees"/>.</summary>
        public static Vector2 Polar(Vector2 c, float r, float degrees)
        {
            float a = degrees * Mathf.Deg2Rad;
            return new Vector2(c.x + r * Mathf.Sin(a), c.y - r * Mathf.Cos(a));
        }

        int Add(Vector2 p, Color32 c, byte alpha)
        {
            Points.Add(new Vector3(p.x, p.y, 0f));
            c.a = alpha;
            Colours.Add(c);
            return Points.Count - 1;
        }

        void Quad(int a, int b, int c, int d)
        {
            Triangles.Add(a); Triangles.Add(b); Triangles.Add(c);
            Triangles.Add(a); Triangles.Add(c); Triangles.Add(d);
        }

        static byte Scaled(byte a, float k) => (byte)Mathf.Clamp(Mathf.RoundToInt(a * Mathf.Clamp01(k)), 0, 255);

        /// <summary>
        /// A band between radii <paramref name="r0"/> and <paramref name="r1"/> round <paramref name="c"/>, from angle
        /// <paramref name="a0"/> to <paramref name="a1"/> (a0 &lt; a1; 360 or more: a whole ring), its ends cut along the radius.
        /// Its edges are the given ones: the fade runs half a feather either side of each.
        /// </summary>
        public void Band(Vector2 c, float r0, float r1, float a0, float a1, Color32 col)
        {
            if (r1 <= r0 || a1 <= a0 || col.a == 0) return;
            float h = Feather * 0.5f;
            byte alpha = col.a;
            float inner = r0 + h, outer = r1 - h;
            if (outer < inner)
            {
                // Thinner than the fade: one line at the middle, as faint as it is thin.
                alpha = Scaled(alpha, (r1 - r0) / Mathf.Max(1e-4f, Feather));
                inner = outer = (r0 + r1) * 0.5f;
            }
            float fadeIn = Mathf.Max(0f, r0 - h), fadeOut = r1 + h;
            bool whole = a1 - a0 >= 359.999f;
            float mid = Mathf.Max(1e-3f, (r0 + r1) * 0.5f);
            float e = whole ? 0f : h / mid * Mathf.Rad2Deg; // half a feather along the arc at its middle, in degrees
            float s0 = a0 + e, s1 = a1 - e;                  // the solid part's angles
            if (!whole && s1 < s0)
            {
                alpha = Scaled(alpha, (a1 - a0) / Mathf.Max(1e-4f, 2f * e));
                s0 = s1 = (a0 + a1) * 0.5f;
            }
            int n = whole ? Mathf.Max(8, Mathf.CeilToInt(360f / Step)) : Mathf.Max(1, Mathf.CeilToInt((s1 - s0) / Step));
            int columns = whole ? n + 1 : n + 3;
            int first = Points.Count;
            for (int k = 0; k < columns; k++)
            {
                float ang;
                bool faded = false;
                if (whole) ang = a0 + 360f * k / n;
                else if (k == 0) { ang = a0 - e; faded = true; }
                else if (k == columns - 1) { ang = a1 + e; faded = true; }
                else ang = s0 + (s1 - s0) * (k - 1) / n;
                byte inside = faded ? (byte)0 : alpha;
                Add(Polar(c, fadeIn, ang), col, 0);
                Add(Polar(c, inner, ang), col, inside);
                Add(Polar(c, outer, ang), col, inside);
                Add(Polar(c, fadeOut, ang), col, 0);
            }
            for (int k = 0; k < columns - 1; k++)
            {
                int p = first + k * 4, q = p + 4;
                Quad(p, q, q + 1, p + 1);
                Quad(p + 1, q + 1, q + 2, p + 2);
                Quad(p + 2, q + 2, q + 3, p + 3);
            }
        }

        /// <summary>A filled disc.</summary>
        public void Disc(Vector2 c, float r, Color32 col)
        {
            if (r <= 0f || col.a == 0) return;
            float h = Feather * 0.5f;
            byte alpha = col.a;
            float solid = r - h;
            if (solid < 0f)
            {
                alpha = Scaled(alpha, r / Mathf.Max(1e-4f, h));
                solid = 0f;
            }
            int n = Mathf.Max(12, Mathf.CeilToInt(360f / Step));
            int centre = Add(c, col, alpha);
            int first = Points.Count;
            for (int k = 0; k <= n; k++)
            {
                float ang = 360f * k / n;
                Add(Polar(c, solid, ang), col, alpha);
                Add(Polar(c, r + h, ang), col, 0);
            }
            for (int k = 0; k < n; k++)
            {
                int p = first + k * 2, q = p + 2;
                Triangles.Add(centre); Triangles.Add(p); Triangles.Add(q);
                Quad(p, q, q + 1, p + 1);
            }
        }

        /// <summary>
        /// A convex polygon of <paramref name="count"/> corners, filled <paramref name="fill"/>, with an outline
        /// <paramref name="outline"/> px wide lying wholly inside its edge (work order 142's rule: the drawn edge is the shape's
        /// edge, outline included), as part A's inked polygons.
        /// </summary>
        public void InkedPolygon(Vector2[] corners, int count, Color32 fill, Color32 dark, float outline)
        {
            Polygon(corners, count, dark);
            int eroded = Erode(corners, count, outline, _c);
            if (eroded >= 3) Polygon(_c, eroded, fill);
        }

        /// <summary>A filled convex polygon with faded edges.</summary>
        public void Polygon(Vector2[] corners, int count, Color32 col)
        {
            if (count < 3 || col.a == 0) return;
            float h = Feather * 0.5f;
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < count; i++) middle += corners[i];
            middle /= count;
            int centre = Add(middle, col, col.a);
            int first = Points.Count;
            for (int i = 0; i < count; i++)
            {
                Vector2 p = corners[i];
                Vector2 n1 = Outward(corners[(i + count - 1) % count], p, middle), n2 = Outward(p, corners[(i + 1) % count], middle);
                Vector2 m = (n1 + n2).normalized;
                float cos = Mathf.Max(0.35f, Vector2.Dot(m, n1));
                Vector2 off = m * (h / cos);
                Add(p - off, col, col.a);
                Add(p + off, col, 0);
            }
            for (int i = 0; i < count; i++)
            {
                int p = first + i * 2, q = first + ((i + 1) % count) * 2;
                Triangles.Add(centre); Triangles.Add(p); Triangles.Add(q);
                Quad(p, q, q + 1, p + 1);
            }
        }

        /// <summary>The unit normal of edge a→b pointing away from <paramref name="middle"/> (the polygon is convex).</summary>
        static Vector2 Outward(Vector2 a, Vector2 b, Vector2 middle)
        {
            Vector2 d = b - a;
            var n = new Vector2(d.y, -d.x);
            float len = n.magnitude;
            if (len < 1e-6f) return Vector2.zero;
            n /= len;
            return Vector2.Dot(n, (a + b) * 0.5f - middle) < 0f ? -n : n;
        }

        /// <summary>The convex polygon shrunk by <paramref name="by"/> px on every side (each edge's half-plane moved in, the corners where they meet); returns its corners' count.</summary>
        int Erode(Vector2[] corners, int count, float by, Vector2[] into)
        {
            Vector2 middle = Vector2.zero;
            for (int i = 0; i < count; i++) middle += corners[i];
            middle /= count;
            int n = count;
            for (int i = 0; i < count; i++) _a[i] = corners[i];
            Vector2[] from = _a, to = _b;
            for (int e = 0; e < count && n >= 3; e++)
            {
                Vector2 p = corners[e], q = corners[(e + 1) % count];
                Vector2 normal = Outward(p, q, middle);
                float limit = Vector2.Dot(normal, p) - by; // inside: dot(normal, x) <= limit
                int m = 0;
                for (int i = 0; i < n && m < MostCorners - 1; i++)
                {
                    Vector2 a = from[i], b = from[(i + 1) % n];
                    float da = Vector2.Dot(normal, a) - limit, db = Vector2.Dot(normal, b) - limit;
                    if (da <= 0f) to[m++] = a;
                    if ((da < 0f && db > 0f) || (da > 0f && db < 0f)) to[m++] = a + (b - a) * (da / (da - db));
                }
                n = m;
                Vector2[] t = from; from = to; to = t;
            }
            for (int i = 0; i < n; i++) into[i] = from[i];
            return n;
        }
    }
}
