using System;

namespace Roguelike.Rules
{
    /// <summary>The two shapes a thing she can take cover behind has on the ground.</summary>
    public enum CoverShapeKind { Box, Circle }

    /// <summary>
    /// The footprint on the ground of something she can take cover behind (taking cover, Kristian 2026-11-19, an
    /// experiment): a wall or a prop that blocks walking. A box <see cref="Width"/> along its own x by
    /// <see cref="Depth"/> along its own z, turned clockwise seen from above (as the engine turns about its up axis) by
    /// <see cref="Yaw"/> degrees, or a circle of <see cref="Radius"/>. Distances in metres.
    /// </summary>
    public readonly struct CoverShape
    {
        public readonly CoverShapeKind Kind;
        public readonly GroundPoint Centre;
        public readonly float Width;
        public readonly float Depth;
        public readonly float Yaw;
        public readonly float Radius;

        CoverShape(CoverShapeKind kind, GroundPoint centre, float width, float depth, float yaw, float radius)
        {
            Kind = kind;
            Centre = centre;
            Width = width;
            Depth = depth;
            Yaw = yaw;
            Radius = radius;
        }

        public static CoverShape Box(GroundPoint centre, float width, float depth, float yawDegrees = 0f) =>
            new CoverShape(CoverShapeKind.Box, centre, Math.Abs(width), Math.Abs(depth), yawDegrees, 0f);

        public static CoverShape Circle(GroundPoint centre, float radius) =>
            new CoverShape(CoverShapeKind.Circle, centre, 0f, 0f, 0f, Math.Abs(radius));

        // The box's own axes on the ground: its x (right) and its z (forward), as the engine turns them.
        GroundPoint Right
        {
            get
            {
                double a = Yaw * Math.PI / 180.0;
                return new GroundPoint((float)Math.Cos(a), (float)-Math.Sin(a));
            }
        }

        GroundPoint Forward
        {
            get
            {
                double a = Yaw * Math.PI / 180.0;
                return new GroundPoint((float)Math.Sin(a), (float)Math.Cos(a));
            }
        }

        GroundPoint ToLocal(GroundPoint p)
        {
            GroundPoint d = p - Centre;
            return new GroundPoint(d.Dot(Right), d.Dot(Forward));
        }

        GroundPoint ToWorld(GroundPoint local) => Centre + Right * local.X + Forward * local.Z;

        GroundPoint DirToWorld(GroundPoint local) => Right * local.X + Forward * local.Z;

        GroundPoint DirToLocal(GroundPoint d) => new GroundPoint(d.Dot(Right), d.Dot(Forward));

        /// <summary>The nearest point of the shape to <paramref name="p"/>: <paramref name="p"/> itself when it is inside.</summary>
        public GroundPoint Closest(GroundPoint p)
        {
            if (Kind == CoverShapeKind.Circle)
            {
                GroundPoint d = p - Centre;
                float length = d.Length;
                return length <= Radius ? p : Centre + d * (Radius / length);
            }
            GroundPoint l = ToLocal(p);
            float hx = Width * 0.5f, hz = Depth * 0.5f;
            return ToWorld(new GroundPoint(Clamp(l.X, -hx, hx), Clamp(l.Z, -hz, hz)));
        }

        /// <summary>The nearest point of the shape's edge to <paramref name="p"/>, inside or out.</summary>
        public GroundPoint Edge(GroundPoint p)
        {
            if (Distance(p) > 0f) return Closest(p);
            if (Kind == CoverShapeKind.Circle) return Centre + Outward(p) * Radius;
            GroundPoint l = ToLocal(p);
            float hx = Width * 0.5f, hz = Depth * 0.5f;
            if (hx - Math.Abs(l.X) <= hz - Math.Abs(l.Z)) return ToWorld(new GroundPoint(l.X >= 0f ? hx : -hx, l.Z));
            return ToWorld(new GroundPoint(l.X, l.Z >= 0f ? hz : -hz));
        }

        /// <summary>Metres from <paramref name="p"/> to the shape's edge; 0 when it is inside.</summary>
        public float Distance(GroundPoint p) => (p - Closest(p)).Length;

        /// <summary>
        /// The way out of the shape toward <paramref name="p"/>, flat and of length 1: from the nearest point of the shape
        /// to <paramref name="p"/>, round a box's corner too. A point inside (or on the edge) takes its nearest face's way out.
        /// </summary>
        public GroundPoint Outward(GroundPoint p)
        {
            GroundPoint off = p - Closest(p);
            float length = off.Length;
            if (length > 1e-5f) return off * (1f / length);
            if (Kind == CoverShapeKind.Circle)
            {
                GroundPoint d = p - Centre;
                float l = d.Length;
                return l > 1e-5f ? d * (1f / l) : new GroundPoint(1f, 0f);
            }
            GroundPoint local = ToLocal(p);
            float toX = Width * 0.5f - Math.Abs(local.X), toZ = Depth * 0.5f - Math.Abs(local.Z);
            GroundPoint face = toX <= toZ ? new GroundPoint(local.X >= 0f ? 1f : -1f, 0f) : new GroundPoint(0f, local.Z >= 0f ? 1f : -1f);
            return DirToWorld(face);
        }

        /// <summary>
        /// Metres along <paramref name="dir"/> (any length but zero) from <paramref name="from"/> before it enters the
        /// shape: 0 when <paramref name="from"/> is inside, <paramref name="max"/> when it does not within that.
        /// </summary>
        public float Entry(GroundPoint from, GroundPoint dir, float max)
        {
            float length = dir.Length;
            if (length < 1e-6f || max <= 0f) return max;
            GroundPoint d = dir * (1f / length);
            float enter, exit;
            if (!Clip(from, d, out enter, out exit) || exit < 0f || enter > max) return max;
            return Math.Max(0f, enter);
        }

        /// <summary>Does the straight segment from <paramref name="a"/> to <paramref name="b"/> pass through the shape?</summary>
        public bool Crosses(GroundPoint a, GroundPoint b)
        {
            GroundPoint ab = b - a;
            float length = ab.Length;
            if (length < 1e-6f) return Distance(a) <= 0f;
            float enter, exit;
            if (!Clip(a, ab * (1f / length), out enter, out exit)) return false;
            // Some part of the segment lies inside: more than a graze of a millimetre.
            return Math.Min(exit, length) - Math.Max(enter, 0f) > 1e-3f;
        }

        /// <summary>The stretch of a line from <paramref name="from"/> along unit <paramref name="d"/> inside the shape, as distances along it.</summary>
        bool Clip(GroundPoint from, GroundPoint d, out float enter, out float exit)
        {
            enter = float.NegativeInfinity;
            exit = float.PositiveInfinity;
            if (Kind == CoverShapeKind.Circle)
            {
                GroundPoint m = from - Centre;
                float b = m.Dot(d);
                float c = m.Dot(m) - Radius * Radius;
                float disc = b * b - c;
                if (disc < 0f) return false;
                float root = (float)Math.Sqrt(disc);
                enter = -b - root;
                exit = -b + root;
                return true;
            }
            GroundPoint o = ToLocal(from), v = DirToLocal(d);
            return Slab(o.X, v.X, Width * 0.5f, ref enter, ref exit) && Slab(o.Z, v.Z, Depth * 0.5f, ref enter, ref exit);
        }

        static bool Slab(float o, float v, float half, ref float enter, ref float exit)
        {
            if (Math.Abs(v) < 1e-7f) return o > -half + 1e-4f && o < half - 1e-4f; // along a face is not through it
            float t1 = (-half - o) / v, t2 = (half - o) / v;
            if (t1 > t2) { float s = t1; t1 = t2; t2 = s; }
            if (t1 > enter) enter = t1;
            if (t2 < exit) exit = t2;
            return enter <= exit;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        public override string ToString() =>
            Kind == CoverShapeKind.Circle ? $"circle {Centre} r {Radius:0.###}" : $"box {Centre} {Width:0.###} x {Depth:0.###} at {Yaw:0.#}";
    }

    /// <summary>
    /// Taking cover (Kristian, 2026-11-19, an experiment: "en slags cover-mekanikk. Med control-knappen, lene seg inntil
    /// vegger/props med litt høyde for å dukke unna skudd som kommer på avstand", a kind of cover mechanic: with the
    /// control key, lean against walls and props of some height to duck shots from afar). He likes a high tempo, so it
    /// is something she shoots from in bursts, not a place to sit. What she can take cover behind is every wall and every
    /// prop that blocks walking, tall or low (<see cref="CoverShape"/>). The numbers are in <see cref="FeelValues"/>.
    ///
    /// - Where she stands: against it on the side where she is, <see cref="FeelValues.CoverStandOff"/> from its edge
    ///   (<see cref="StandPlace"/>), round a box's corner as round a circle.
    /// - Is she covered against a shot from a point (<see cref="Shields"/>)? Only if the thing lies between her and the
    ///   shooter, and only if she is ducked or it is tall: while she stands a low prop covers her no more than anyone,
    ///   and a shooter that has walked round to her side sees her.
    /// - The slide (<see cref="Slide"/>): a move key slides her along it at the cover's share of run speed, staying
    ///   against it; a key more than <see cref="FeelValues.CoverSlideAngle"/> off its face does nothing.
    /// </summary>
    public static class TakeCoverRules
    {
        /// <summary>Is the thing within reach: <paramref name="reach"/> metres or less from her to its edge?</summary>
        public static bool InReach(CoverShape shape, GroundPoint her, float reach) => shape.Distance(her) <= reach;

        /// <summary>
        /// Where she stands against the shape from where she is: the nearest point of its edge, out by
        /// <paramref name="standOff"/> (from inside it, out by its nearest face).
        /// </summary>
        public static GroundPoint StandPlace(CoverShape shape, GroundPoint her, float standOff) =>
            shape.Edge(her) + shape.Outward(her) * standOff;

        /// <summary>
        /// Is she covered, standing at <paramref name="her"/> in cover against <paramref name="shape"/>, against a shot from
        /// <paramref name="shooter"/>? Only when the straight line between them passes through the shape, and only when she
        /// is ducked or the shape is tall cover.
        /// </summary>
        public static bool Shields(CoverShape shape, bool tall, bool ducked, GroundPoint her, GroundPoint shooter) =>
            (ducked || tall) && shape.Crosses(shooter, her);

        /// <summary>
        /// One step of the slide along the shape from <paramref name="place"/> for a move key held along
        /// <paramref name="move"/> (any length): <paramref name="step"/> metres along the face, the way of the key, and
        /// back against the shape. A key more than <paramref name="maxAngle"/> degrees off the face, toward it or away,
        /// does nothing, nor does no key.
        /// </summary>
        public static GroundPoint Slide(CoverShape shape, GroundPoint place, GroundPoint move, float step, float standOff, float maxAngle)
        {
            float length = move.Length;
            if (length < 1e-6f || step <= 0f) return place;
            GroundPoint m = move * (1f / length);
            GroundPoint n = shape.Outward(place);
            var face = new GroundPoint(-n.Z, n.X);
            float offFace = (float)(Math.Asin(Math.Min(1.0, Math.Abs(m.Dot(n)))) * 180.0 / Math.PI);
            if (offFace > maxAngle + 1e-3f) return place;
            float along = m.Dot(face);
            if (Math.Abs(along) < 1e-4f) return place;
            GroundPoint next = place + face * (along > 0f ? step : -step);
            return StandPlace(shape, next, standOff);
        }
    }
}
