using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The trial boss's drawings on the floor (work order 147): the game's own telegraph line (a copy of the boss's, red, thickening
    /// from <see cref="Thin"/> to <see cref="Thick"/> toward the strike), taken from a pool made once as it wakes and given back, so a
    /// frame of the fight makes nothing. Its colours are the game's: red, the counter's blue, and two of this order's, a pale grey for a
    /// pillar's shadow (a safe place) and a pale gold for the ward's closing circle. Drawn edges are the judged edges: each shape is
    /// drawn from the very numbers its hit is judged by.
    /// </summary>
    public sealed class BossTrialLines
    {
        public const float Height = 0.07f, Thin = 0.05f, Thick = 0.3f;
        public static readonly Color Red = new Color(0f, 0f, 0f, 0f); // no colour: the material's own red
        public static readonly Color Blue = new Color(0.15f, 0.45f, 1f);
        public static readonly Color Pale = new Color(0.82f, 0.84f, 0.86f);
        public static readonly Color Gold = new Color(1f, 0.82f, 0.35f);
        const int MostPoints = 256;

        readonly LineRenderer _template;
        readonly Transform _parent;
        readonly List<LineRenderer> _free = new List<LineRenderer>();
        readonly List<LineRenderer> _all = new List<LineRenderer>();
        readonly MaterialPropertyBlock _block = new MaterialPropertyBlock();
        readonly Vector3[] _points = new Vector3[MostPoints];

        public BossTrialLines(LineRenderer template, Transform parent, int ready)
        {
            _template = template;
            _parent = parent;
            for (int i = 0; i < ready; i++) Make();
        }

        /// <summary>Lines made (for tests: none is made in a frame once the pool is warm).</summary>
        public int Made => _all.Count;
        /// <summary>Lines shown now.</summary>
        public int Shown => _all.Count - _free.Count;

        void Make()
        {
            if (_template == null) return;
            GameObject go = Object.Instantiate(_template.gameObject, _parent, false);
            go.name = "BossTrialLine_" + (_all.Count + 1);
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            go.transform.localScale = Vector3.one;
            var l = go.GetComponent<LineRenderer>();
            l.useWorldSpace = true;
            l.enabled = false;
            _all.Add(l);
            _free.Add(l);
        }

        /// <summary>A line to draw on, shown, in a colour (<see cref="Red"/>: the telegraph's own).</summary>
        public LineRenderer Take(Color colour, bool loop)
        {
            if (_free.Count == 0) Make();
            if (_free.Count == 0) return null;
            LineRenderer l = _free[_free.Count - 1];
            _free.RemoveAt(_free.Count - 1);
            Paint(l, colour);
            l.loop = loop;
            l.widthMultiplier = Thin;
            l.enabled = true;
            return l;
        }

        /// <summary>Gives a line back (hidden); null is nothing. Returns null for the caller's field.</summary>
        public LineRenderer Give(LineRenderer l)
        {
            if (l == null) return null;
            l.enabled = false;
            if (!_free.Contains(l)) _free.Add(l);
            return null;
        }

        public void Paint(LineRenderer l, Color colour)
        {
            if (l == null) return;
            _block.Clear();
            if (colour.a > 0f)
            {
                _block.SetColor("_BaseColor", colour);
                _block.SetColor("_Color", colour);
            }
            l.SetPropertyBlock(_block);
        }

        /// <summary>The width of a telegraph at this share of its time: thin as it begins, thick as it strikes.</summary>
        public static float Width(float share) => Mathf.Lerp(Thin, Thick, Mathf.Clamp01(share));

        public void Circle(LineRenderer l, Vector3 middle, float radius)
        {
            if (l != null) GroundOutline.Circle(l, middle, Mathf.Max(0.01f, radius), Height);
        }

        public void Strip(LineRenderer l, Vector3 origin, Vector3 dir, float length, float halfWidth)
        {
            if (l != null) GroundOutline.Strip(l, origin, dir, Mathf.Max(0.01f, length), Mathf.Max(0.01f, halfWidth), Height);
        }

        public void Cone(LineRenderer l, Vector3 origin, Vector3 dir, float length, float angle)
        {
            if (l != null) GroundOutline.Cone(l, origin, dir, length, angle, Height);
        }

        /// <summary>
        /// An arc of a circle round <paramref name="middle"/>, through the sample directions <paramref name="from"/> to
        /// <paramref name="to"/> (inclusive, of <paramref name="samples"/> round, wrapping), at <paramref name="radius"/>: a piece of a
        /// ring that a pillar stops.
        /// </summary>
        public void Arc(LineRenderer l, Vector3 middle, float radius, int from, int to, int samples)
        {
            if (l == null || samples <= 0) return;
            int n = ((to - from) % samples + samples) % samples + 1;
            int step = Mathf.Max(1, Mathf.CeilToInt(n / (float)(MostPoints - 1)));
            int count = 0;
            for (int k = 0; k < n && count < MostPoints - 1; k += step) _points[count++] = At(middle, (from + k) % samples, samples, radius);
            _points[count++] = At(middle, to % samples, samples, radius);
            Set(l, count);
        }

        /// <summary>
        /// A shadow: the outline of the ground between the near distances and the far ones of the directions <paramref name="from"/>
        /// to <paramref name="to"/>, as a closed loop.
        /// </summary>
        public void Shadow(LineRenderer l, Vector3 middle, float[] near, float[] far, int from, int to)
        {
            if (l == null || near == null || far == null) return;
            int samples = near.Length;
            int n = ((to - from) % samples + samples) % samples + 1;
            int half = MostPoints / 2 - 1;
            int step = Mathf.Max(1, Mathf.CeilToInt(n / (float)half));
            int count = 0;
            for (int k = 0; k < n && count < half; k += step) { int i = (from + k) % samples; _points[count++] = At(middle, i, samples, near[i]); }
            int last = to % samples;
            _points[count++] = At(middle, last, samples, near[last]);
            _points[count++] = At(middle, last, samples, far[last]);
            for (int k = n - 1; k >= 0 && count < MostPoints - 1; k -= step) { int i = (from + k) % samples; _points[count++] = At(middle, i, samples, far[i]); }
            Set(l, count);
        }

        /// <summary>A straight line down the middle of a strip, its width set by the caller: the strip's fill.</summary>
        public void Band(LineRenderer l, Vector3 origin, Vector3 dir, float length)
        {
            if (l == null) return;
            l.positionCount = 2;
            l.SetPosition(0, new Vector3(origin.x, Height, origin.z));
            Vector3 end = origin + dir * Mathf.Max(0.01f, length);
            l.SetPosition(1, new Vector3(end.x, Height, end.z));
        }

        /// <summary>A rectangle on the ground (the room's floor outlined for Ruin).</summary>
        public void Rect(LineRenderer l, Vector3 a, Vector3 b)
        {
            if (l == null) return;
            l.positionCount = 4;
            l.SetPosition(0, new Vector3(a.x, Height, a.z));
            l.SetPosition(1, new Vector3(b.x, Height, a.z));
            l.SetPosition(2, new Vector3(b.x, Height, b.z));
            l.SetPosition(3, new Vector3(a.x, Height, b.z));
        }

        void Set(LineRenderer l, int count)
        {
            l.positionCount = count;
            for (int i = 0; i < count; i++) l.SetPosition(i, _points[i]);
        }

        static Vector3 At(Vector3 middle, int i, int samples, float r)
        {
            GroundPoint d = BossTrialRules.SampleDirection(i, samples);
            return new Vector3(middle.x + d.X * r, Height, middle.z + d.Z * r);
        }

        public void DestroyAll()
        {
            foreach (LineRenderer l in _all) if (l != null) Object.Destroy(l.gameObject);
            _all.Clear();
            _free.Clear();
        }
    }
}
