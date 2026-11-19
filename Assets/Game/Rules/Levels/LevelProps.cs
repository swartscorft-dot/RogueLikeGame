using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>The prop models of the first biome, the Crypt (work order 009).</summary>
    public enum PropModel { Altar, BonePile, Brazier, Chest, GraveMarker, Pillar, PillarBroken, Rubble, Sarcophagus, Statue, Urns }

    /// <summary>The shape of a prop's footprint on the ground.</summary>
    public enum PropShape { Rectangle, Circle }

    /// <summary>
    /// What a prop is there for, which decides the rules it keeps (work order 009). Floor: cover and obstacles in the
    /// open floor, kept <see cref="PropValues.FloorSpacing"/> from every other prop. Wall: stands against a wall.
    /// Set: a room's set pieces (braziers by a door or in corners, the reward room's chest, the boss room's statues
    /// and pillars). Every prop but a Wall one keeps <see cref="PropValues.WallClearance"/> from the walls.
    /// </summary>
    public enum PropPlace { Floor, Wall, Set }

    /// <summary>
    /// One model's size as the game takes it: its footprint (a rectangle <see cref="Width"/> along its own x by
    /// <see cref="Depth"/> along its own z, or a circle of <see cref="Radius"/>) and its height, in metres, from the
    /// import check of work order 009. A footprint is a little larger than the model's bounds, rounded up.
    /// </summary>
    public sealed class PropSize
    {
        public PropModel Model;
        public PropShape Shape;
        public double Width, Depth, Radius;
        public double Height;
        /// <summary>May turn by any multiple of 15 degrees; the rest by multiples of 90.</summary>
        public bool AnyAngle;
        /// <summary>May stand against a wall (<see cref="PropPlace.Wall"/>).</summary>
        public bool AtWall;
        /// <summary>The character for it on a level's text map.</summary>
        public char Letter;

        public string Name => "Crypt_" + Model + "_01";
        /// <summary>Where it is under a Resources folder: the model, and its texture with "_base_color" after it.</summary>
        public string ResourcePath => "Environment/Crypt/" + Name + "/" + Name;
        public string TexturePath => ResourcePath + "_base_color";

        static PropSize Box(PropModel m, double w, double d, double h, char letter, bool anyAngle = false, bool atWall = false) =>
            new PropSize { Model = m, Shape = PropShape.Rectangle, Width = w, Depth = d, Height = h, Letter = letter, AnyAngle = anyAngle, AtWall = atWall };

        static PropSize Round(PropModel m, double r, double h, char letter, bool anyAngle = false, bool atWall = false) =>
            new PropSize { Model = m, Shape = PropShape.Circle, Radius = r, Height = h, Letter = letter, AnyAngle = anyAngle, AtWall = atWall };

        /// <summary>
        /// The eleven, by <see cref="PropModel"/>. Heights are the ones the models were made to (Unity's bounds agree to
        /// the millimetre); footprints are their bounds as Unity imports them (work order 009's import test), rounded up
        /// to the next 5 cm: a circle for the round ones (half the larger side), a rectangle for the rest.
        /// </summary>
        public static readonly PropSize[] Crypt =
        {
            Box(PropModel.Altar, 1.2, 1.3, 1.3, 'A', atWall: true),           // bounds 1.165 x 1.274
            Round(PropModel.BonePile, 0.45, 0.6, '~', atWall: true),          // 0.820 x 0.827
            Round(PropModel.Brazier, 0.75, 1.1, '*'),                         // 1.488 x 1.491
            Box(PropModel.Chest, 1.05, 0.75, 0.9, '$'),                       // 1.022 x 0.742
            Box(PropModel.GraveMarker, 1.1, 0.7, 1.3, '!', atWall: true),     // 1.079 x 0.671
            Round(PropModel.Pillar, 0.6, 3.0, '#', anyAngle: true),           // 1.102 x 1.127
            Round(PropModel.PillarBroken, 0.6, 1.2, '%', anyAngle: true),     // 1.130 x 1.135
            Box(PropModel.Rubble, 1.6, 1.5, 0.8, ':', anyAngle: true),        // 1.569 x 1.458
            Box(PropModel.Sarcophagus, 1.9, 1.1, 1.0, '='),                   // 1.856 x 1.061
            Box(PropModel.Statue, 1.05, 1.1, 2.6, '&'),                       // 1.042 x 1.072
            Box(PropModel.Urns, 1.0, 1.05, 1.1, 'u', atWall: true),           // 0.974 x 1.003
        };

        public static PropSize Of(PropModel model) => Crypt[(int)model];
    }

    /// <summary>
    /// A prop's footprint on the level's ground: a circle, or a rectangle turned clockwise (seen from above, as the
    /// engine turns about its up axis) by <see cref="Rotation"/> degrees. Distances between footprints, points, segments
    /// and a room's walls, in metres; 0 when they touch or overlap.
    /// </summary>
    public struct PropFootprint
    {
        public PropShape Shape;
        public LevelPoint Centre;
        public double HalfWidth, HalfDepth, Radius;
        public int Rotation;

        public static PropFootprint Of(PropSize size, LevelPoint centre, int rotation) => new PropFootprint
        {
            Shape = size.Shape, Centre = centre, Rotation = rotation,
            HalfWidth = size.Width * 0.5, HalfDepth = size.Depth * 0.5, Radius = size.Radius,
        };

        /// <summary>The same footprint grown by <paramref name="by"/> on every side (a rectangle stays a rectangle).</summary>
        public PropFootprint Grown(double by)
        {
            PropFootprint f = this;
            f.HalfWidth += by;
            f.HalfDepth += by;
            f.Radius += by;
            return f;
        }

        // The rectangle's own axes on the ground: x (its width) and z (its depth), turned clockwise by the rotation.
        void Axes(out double ux, out double uy, out double vx, out double vy)
        {
            double a = Rotation * Math.PI / 180.0, c = Math.Cos(a), s = Math.Sin(a);
            ux = c; uy = -s;   // its x
            vx = s; vy = c;    // its z
        }

        /// <summary>The rectangle's four corners (for a circle, the corners of the square around it).</summary>
        public LevelPoint[] Corners()
        {
            if (Shape == PropShape.Circle)
                return new[]
                {
                    new LevelPoint(Centre.X - Radius, Centre.Y - Radius), new LevelPoint(Centre.X + Radius, Centre.Y - Radius),
                    new LevelPoint(Centre.X + Radius, Centre.Y + Radius), new LevelPoint(Centre.X - Radius, Centre.Y + Radius),
                };
            Axes(out double ux, out double uy, out double vx, out double vy);
            double w = HalfWidth, d = HalfDepth, x = Centre.X, y = Centre.Y;
            return new[]
            {
                new LevelPoint(x - ux * w - vx * d, y - uy * w - vy * d), new LevelPoint(x + ux * w - vx * d, y + uy * w - vy * d),
                new LevelPoint(x + ux * w + vx * d, y + uy * w + vy * d), new LevelPoint(x - ux * w + vx * d, y - uy * w + vy * d),
            };
        }

        /// <summary>The smallest box of the ground around it, square to the grid.</summary>
        public void Extent(out double minX, out double minY, out double maxX, out double maxY)
        {
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            foreach (LevelPoint p in Corners())
            {
                minX = Math.Min(minX, p.X); maxX = Math.Max(maxX, p.X);
                minY = Math.Min(minY, p.Y); maxY = Math.Max(maxY, p.Y);
            }
        }

        /// <summary>Metres from a point to the footprint; 0 inside it.</summary>
        public double DistanceTo(LevelPoint p)
        {
            if (Shape == PropShape.Circle) return Math.Max(0, p.DistanceTo(Centre) - Radius);
            Axes(out double ux, out double uy, out double vx, out double vy);
            double dx = p.X - Centre.X, dy = p.Y - Centre.Y;
            double lx = Math.Abs(dx * ux + dy * uy) - HalfWidth, lz = Math.Abs(dx * vx + dy * vy) - HalfDepth;
            double ox = Math.Max(0, lx), oz = Math.Max(0, lz);
            return Math.Sqrt(ox * ox + oz * oz);
        }

        /// <summary>Metres from a segment to the footprint; 0 when they cross or touch.</summary>
        public double DistanceTo(LevelPoint a, LevelPoint b)
        {
            if (Shape == PropShape.Circle) return Math.Max(0, LevelGeometry.DistanceToSegment(Centre, a, b) - Radius);
            if (SegmentsCross(a, b, Corners())) return 0;
            double best = Math.Min(DistanceTo(a), DistanceTo(b));
            foreach (LevelPoint c in Corners()) best = Math.Min(best, LevelGeometry.DistanceToSegment(c, a, b));
            return best;
        }

        /// <summary>Metres between two footprints; 0 when they touch or overlap.</summary>
        public double DistanceTo(PropFootprint o)
        {
            if (Shape == PropShape.Circle && o.Shape == PropShape.Circle)
                return Math.Max(0, Centre.DistanceTo(o.Centre) - Radius - o.Radius);
            if (Shape == PropShape.Circle) return Math.Max(0, o.DistanceTo(Centre) - Radius);
            if (o.Shape == PropShape.Circle) return Math.Max(0, DistanceTo(o.Centre) - o.Radius);
            // Two rectangles: apart, the nearest pair of points has a corner of one of them in it.
            LevelPoint[] mine = Corners(), theirs = o.Corners();
            double best = double.MaxValue;
            foreach (LevelPoint c in mine) best = Math.Min(best, o.DistanceTo(c));
            foreach (LevelPoint c in theirs) best = Math.Min(best, DistanceTo(c));
            for (int i = 0; i < 4 && best > 0; i++)
                if (SegmentsCross(mine[i], mine[(i + 1) % 4], theirs)) best = 0;
            return best;
        }

        /// <summary>
        /// Metres from the footprint to the nearest wall of a room it stands in (the four sides of its rectangle of
        /// cells); negative when it reaches out of the room.
        /// </summary>
        public double DistanceToWalls(CellRect room, double cellSize)
        {
            if (Shape == PropShape.Circle) return LevelGeometry.DistanceToWalls(Centre, room, cellSize) - Radius;
            double best = double.MaxValue;
            foreach (LevelPoint c in Corners()) best = Math.Min(best, LevelGeometry.DistanceToWalls(c, room, cellSize));
            return best;
        }

        static bool SegmentsCross(LevelPoint a, LevelPoint b, LevelPoint[] polygon)
        {
            for (int i = 0; i < polygon.Length; i++)
                if (Cross(a, b, polygon[i], polygon[(i + 1) % polygon.Length])) return true;
            return false;
        }

        static bool Cross(LevelPoint p1, LevelPoint p2, LevelPoint q1, LevelPoint q2)
        {
            double d1 = Side(q1, q2, p1), d2 = Side(q1, q2, p2), d3 = Side(p1, p2, q1), d4 = Side(p1, p2, q2);
            return ((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) && ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0));
        }

        static double Side(LevelPoint a, LevelPoint b, LevelPoint p) => (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
    }

    /// <summary>
    /// One prop of a level (work order 009): which model, where its pivot stands on the ground (the middle of its
    /// footprint) in metres, its turn in degrees (clockwise from above, as the engine turns about its up axis), what it
    /// is there for, and its footprint, copied from <see cref="PropSize.Crypt"/> so the validator reads only the
    /// description.
    /// </summary>
    public sealed class LevelProp
    {
        public PropModel Model;
        public LevelPoint Position;
        public int Rotation;
        public PropPlace Place;
        public PropShape Shape;
        /// <summary>A rectangle's sides, along its own x and z before it turns; 0 for a circle.</summary>
        public double Width, Depth;
        /// <summary>A circle's radius; 0 for a rectangle.</summary>
        public double Radius;
        public double Height;

        public static LevelProp Make(PropModel model, LevelPoint position, int rotation, PropPlace place)
        {
            PropSize s = PropSize.Of(model);
            return new LevelProp
            {
                Model = model, Position = position, Rotation = rotation, Place = place, Shape = s.Shape,
                Width = s.Shape == PropShape.Rectangle ? s.Width : 0, Depth = s.Shape == PropShape.Rectangle ? s.Depth : 0,
                Radius = s.Shape == PropShape.Circle ? s.Radius : 0, Height = s.Height,
            };
        }

        public PropFootprint Footprint => new PropFootprint
        {
            Shape = Shape, Centre = Position, Rotation = Rotation, HalfWidth = Width * 0.5, HalfDepth = Depth * 0.5, Radius = Radius,
        };

        public char Letter => PropSize.Of(Model).Letter;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1} at {2} turned {3} ({4} {5})", Place.ToString().ToLowerInvariant(), Model, Position, Rotation,
                          Shape == PropShape.Circle ? "circle" : "rect",
                          Shape == PropShape.Circle ? Radius.ToString("R", CultureInfo.InvariantCulture)
                          : Width.ToString("R", CultureInfo.InvariantCulture) + "x" + Depth.ToString("R", CultureInfo.InvariantCulture));
    }
}
