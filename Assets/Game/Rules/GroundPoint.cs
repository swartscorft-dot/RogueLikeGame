using System;

namespace Roguelike.Rules
{
    /// <summary>A point on the ground plane (X, Z in world space).</summary>
    public readonly struct GroundPoint
    {
        public readonly float X;
        public readonly float Z;

        public GroundPoint(float x, float z) { X = x; Z = z; }

        public static GroundPoint operator +(GroundPoint a, GroundPoint b) => new GroundPoint(a.X + b.X, a.Z + b.Z);
        public static GroundPoint operator -(GroundPoint a, GroundPoint b) => new GroundPoint(a.X - b.X, a.Z - b.Z);
        public static GroundPoint operator *(GroundPoint a, float s) => new GroundPoint(a.X * s, a.Z * s);

        public float Dot(GroundPoint o) => X * o.X + Z * o.Z;
        public float Length => (float)Math.Sqrt(X * X + Z * Z);
        public float DistanceTo(GroundPoint o) => (o - this).Length;

        public override string ToString() => $"({X:0.###}, {Z:0.###})";
    }
}
