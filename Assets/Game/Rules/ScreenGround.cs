using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// What a camera shows of the ground (work order 124): the four points where the rays through the corners of its picture meet a
    /// flat ground, in the ground's own x (east) and z (north), going round the picture's bottom left, bottom right, top right and top
    /// left. Plain numbers: the game hands in its real camera's place, axes and angle (<see cref="FromCamera"/>), and the generator a
    /// camera made from named numbers (<see cref="Design"/>), so the same rule answers both.
    /// </summary>
    public struct GroundQuad
    {
        public double X0, Z0, X1, Z1, X2, Z2, X3, Z3;
        /// <summary>False when the camera does not look down at the ground (nothing to ask).</summary>
        public bool Known;

        public void Corner(int i, out double x, out double z)
        {
            switch (i & 3)
            {
                case 0: x = X0; z = Z0; return;
                case 1: x = X1; z = Z1; return;
                case 2: x = X2; z = Z2; return;
                default: x = X3; z = Z3; return;
            }
        }
    }

    /// <summary>
    /// The camera's view of the ground as a rule (work order 124; Kristian ticked "Ja, bare når den synes": a shooter or a caster
    /// begins a draw only while it is on his screen). No engine: the runtime asks its camera and hands the numbers in.
    /// </summary>
    public static class ScreenGround
    {
        /// <summary>
        /// The metres along a corner's ray that stand for it when the ray does not come down to the ground (its top edge above the
        /// horizon): far beyond any room, so the picture's top is open as it is on screen.
        /// </summary>
        public const double FarWhenNoGround = 1000.0;

        /// <summary>
        /// The ground a camera shows: at (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>), looking along
        /// <paramref name="f"/> with <paramref name="u"/> up and <paramref name="r"/> right in its picture (unit vectors, x east, y up,
        /// z north), a vertical field of view of <paramref name="fovDegrees"/>, a picture <paramref name="aspect"/> times as wide as it
        /// is high; the ground the flat plane at height <paramref name="groundY"/>. As Unity's ray through each corner of the picture.
        /// </summary>
        public static GroundQuad FromCamera(double cx, double cy, double cz, double fx, double fy, double fz, double ux, double uy, double uz,
                                            double rx, double ry, double rz, double fovDegrees, double aspect, double groundY)
        {
            var q = new GroundQuad();
            double tv = Math.Tan(fovDegrees * 0.5 * Math.PI / 180.0), th = tv * Math.Max(1e-6, aspect);
            double h = cy - groundY;
            if (h <= 1e-6) return q; // under the ground or on it: nothing shown of it as a floor
            bool any = false;
            for (int i = 0; i < 4; i++)
            {
                double sx = i == 0 || i == 3 ? -1 : 1, sy = i < 2 ? -1 : 1;
                double dx = fx + ux * tv * sy + rx * th * sx, dy = fy + uy * tv * sy + ry * th * sx, dz = fz + uz * tv * sy + rz * th * sx;
                double x, z;
                if (dy < -1e-9)
                {
                    double t = h / -dy;
                    x = cx + dx * t;
                    z = cz + dz * t;
                    any = true;
                }
                else
                {
                    // Above the horizon: as far as the ray's way over the ground goes.
                    double l = Math.Sqrt(dx * dx + dz * dz);
                    x = cx + (l > 1e-9 ? dx / l : 0) * FarWhenNoGround;
                    z = cz + (l > 1e-9 ? dz / l : 0) * FarWhenNoGround;
                }
                switch (i)
                {
                    case 0: q.X0 = x; q.Z0 = z; break;
                    case 1: q.X1 = x; q.Z1 = z; break;
                    case 2: q.X2 = x; q.Z2 = z; break;
                    default: q.X3 = x; q.Z3 = z; break;
                }
            }
            q.Known = any;
            return q;
        }

        /// <summary>
        /// The ground a camera like the game's follow camera shows (<c>CameraRig</c>: no turn, pitched down by
        /// <paramref name="pitchDegrees"/>, <paramref name="distance"/> back along its line of view from her ground point at
        /// (<paramref name="herX"/>, <paramref name="herZ"/>), looking north).
        /// </summary>
        public static GroundQuad Design(double herX, double herZ, double distance, double pitchDegrees, double fovDegrees, double aspect)
        {
            double p = pitchDegrees * Math.PI / 180.0;
            double fy = -Math.Sin(p), fz = Math.Cos(p);
            double uy = Math.Cos(p), uz = Math.Sin(p);
            return FromCamera(herX, -fy * distance, herZ - fz * distance, 0, fy, fz, 0, uy, uz, 1, 0, 0, fovDegrees, aspect, 0);
        }

        /// <summary>
        /// Metres the point stands inside the quad's nearest edge (negative outside): the least of its distances in from the four
        /// edges. An unknown quad has no edges: everything is inside (<see cref="double.PositiveInfinity"/>).
        /// </summary>
        public static double Inside(GroundQuad q, double x, double z)
        {
            if (!q.Known) return double.PositiveInfinity;
            // The quad's turn, so "inside" is the same side of every edge whichever way round its corners go.
            double area = 0;
            for (int i = 0; i < 4; i++)
            {
                q.Corner(i, out double ax, out double az);
                q.Corner(i + 1, out double bx, out double bz);
                area += ax * bz - bx * az;
            }
            double turn = area >= 0 ? 1 : -1, least = double.PositiveInfinity;
            for (int i = 0; i < 4; i++)
            {
                q.Corner(i, out double ax, out double az);
                q.Corner(i + 1, out double bx, out double bz);
                double ex = bx - ax, ez = bz - az, l = Math.Sqrt(ex * ex + ez * ez);
                if (l < 1e-9) continue;
                double d = turn * (ex * (z - az) - ez * (x - ax)) / l;
                if (d < least) least = d;
            }
            return least;
        }

        /// <summary>Is the point at least <paramref name="margin"/> inside the quad (see <see cref="Inside(GroundQuad, double, double)"/>)?</summary>
        public static bool Shows(GroundQuad q, double x, double z, double margin) => Inside(q, x, z) >= margin - 1e-9;
    }
}
