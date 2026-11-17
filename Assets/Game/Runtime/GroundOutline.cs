using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Draws the outline of an area on the ground into a looping LineRenderer: a strip, a circle or a wedge.
    /// The player's skill aim and the enemy's telegraph both use it, so the two read the same way.
    /// </summary>
    public static class GroundOutline
    {
        const int CircleSegments = 32;
        const int ArcSegments = 12;

        /// <summary>A strip from the origin along the direction: <paramref name="length"/> long, 2 * <paramref name="halfWidth"/> wide.</summary>
        public static void Strip(LineRenderer line, Vector3 origin, Vector3 dir, float length, float halfWidth, float height)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir) * halfWidth;
            line.positionCount = 4;
            line.SetPosition(0, Lift(origin - side, height));
            line.SetPosition(1, Lift(origin + side, height));
            line.SetPosition(2, Lift(origin + dir * length + side, height));
            line.SetPosition(3, Lift(origin + dir * length - side, height));
        }

        public static void Circle(LineRenderer line, Vector3 center, float radius, float height)
        {
            line.positionCount = CircleSegments;
            for (int i = 0; i < CircleSegments; i++)
            {
                float a = i / (float)CircleSegments * Mathf.PI * 2f;
                line.SetPosition(i, Lift(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius, height));
            }
        }

        /// <summary>A wedge from the origin along the direction: <paramref name="length"/> long, <paramref name="angle"/> degrees wide in all.</summary>
        public static void Cone(LineRenderer line, Vector3 origin, Vector3 dir, float length, float angle, float height)
        {
            line.positionCount = ArcSegments + 2;
            line.SetPosition(0, Lift(origin, height));
            for (int i = 0; i <= ArcSegments; i++)
            {
                float yaw = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)ArcSegments);
                line.SetPosition(i + 1, Lift(origin + Quaternion.Euler(0f, yaw, 0f) * dir * length, height));
            }
        }

        static Vector3 Lift(Vector3 v, float height) => new Vector3(v.x, height, v.z);
    }
}
