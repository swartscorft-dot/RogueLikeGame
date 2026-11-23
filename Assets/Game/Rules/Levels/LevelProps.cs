using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The prop models of the first biome, the Crypt (work order 009). Work order 028 (Kristian, 2026-11-19 and 2026-11-20:
    /// "more of the crypt") added the seven after Urns; the eleven before them keep their numbers. Work order 052 added the
    /// eight large props after Basin, modelled for the rooms' compositions (the concept pictures; Kristian's choice of
    /// 2026-11-20, "build toward them"): only the rooms' mood puts them down (<see cref="RoomMoodPlan"/>), never the props stage.
    /// </summary>
    public enum PropModel
    {
        Altar, BonePile, Brazier, Chest, GraveMarker, Pillar, PillarBroken, Rubble, Sarcophagus, Statue, Urns,
        Coffin, SkullPile, Candelabra, BoneThrone, Bookshelf, Cage, Basin,
        KnightStatue, GreatAltar, Dais, Banner, GreatBrazier, CoinHeap, BoneWall, CandleCluster,
    }

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
        /// <summary>
        /// Slender (work order 028, the candelabra): tall by its height but thin, so it stops no shot, is not
        /// <see cref="LevelProp.IsCover"/> and carries no shelter; it still blocks walking with its small footprint.
        /// </summary>
        public bool Slender;
        /// <summary>It has a front, which the rules turn the way it is to face (work order 028).</summary>
        public bool HasFront;
        /// <summary>
        /// Its front is its own -Z as Unity imports it, so it is turned half round from the rules' usual (+Z front) to face
        /// a way (work order 028: the bookshelf, whose back is +Z as the order believed; the order believed the throne's
        /// front -Z too, and its mesh says +Z: its tall back lies at -Z. The import test reads both from the meshes).
        /// </summary>
        public bool FrontMinusZ;
        /// <summary>
        /// A wall piece (work order 028): it wants a wall behind it, its back within <see cref="PropValues.HandWidth"/>
        /// of the wall's inner face, facing into the room, never at the low south wall and never in front of a niche.
        /// </summary>
        public bool WallPiece;
        /// <summary>It has an "_emission" map beside its texture, which glows in the game's cyan (work order 028).</summary>
        public bool Emission;
        /// <summary>
        /// One of work order 052's eight large props: put down only by the rooms' compositions (<see cref="RoomMoodPlan"/>,
        /// behind <see cref="LevelValues.LargeProps"/>), never by the props stage, so no theme's recipe names it.
        /// </summary>
        public bool Large;
        /// <summary>
        /// Walked over or under (work order 052: the dais, sunk to a low platform; the banner on its wall): no collider at all,
        /// so not in the NavMesh. It is slender too: it stops no shot and carries no shelter.
        /// </summary>
        public bool Walkable;
        /// <summary>Its emission map glows in its own colours, not in the game's cyan (work order 052: the candles' flames, the coins' gold).</summary>
        public bool OwnGlow;
        /// <summary>
        /// Metres its model's pivot stands over the ground (work order 052): the banner hangs from its top at
        /// <see cref="BannerTop"/>; the dais is sunk so that <see cref="DaisShows"/> of it shows. 0 for the rest.
        /// </summary>
        public double ModelUp;
        /// <summary>
        /// Its model's pivot is at the middle of its back, not of its footprint (work order 052: the banner and the bone wall,
        /// whose backs go against a wall): the model is drawn half its depth back from its footprint's middle.
        /// </summary>
        public bool PivotAtBack;

        public string Name => "Crypt_" + Model + "_01";
        /// <summary>Where it is under a Resources folder: the model, and its texture with "_base_color" after it.</summary>
        public string ResourcePath => "Environment/Crypt/" + Name + "/" + Name;
        public string TexturePath => ResourcePath + "_base_color";
        /// <summary>Its emission map, "_emission" after the model's path (work order 028).</summary>
        public string EmissionPath => ResourcePath + "_emission";
        /// <summary>The turn, in degrees, that has its front face the way its own +Z would: 180 for a -Z front.</summary>
        public int FrontTurn => FrontMinusZ ? 180 : 0;

        static PropSize Box(PropModel m, double w, double d, double h, char letter, bool anyAngle = false, bool atWall = false) =>
            new PropSize { Model = m, Shape = PropShape.Rectangle, Width = w, Depth = d, Height = h, Letter = letter, AnyAngle = anyAngle, AtWall = atWall };

        static PropSize Round(PropModel m, double r, double h, char letter, bool anyAngle = false, bool atWall = false) =>
            new PropSize { Model = m, Shape = PropShape.Circle, Radius = r, Height = h, Letter = letter, AnyAngle = anyAngle, AtWall = atWall };

        PropSize With(bool slender = false, bool front = false, bool frontMinusZ = false, bool wallPiece = false, bool emission = false)
        {
            Slender = slender;
            HasFront = front || frontMinusZ;
            FrontMinusZ = frontMinusZ;
            WallPiece = wallPiece;
            Emission = emission;
            return this;
        }

        /// <summary>Work order 052: a large prop, and how it is drawn and walked.</summary>
        PropSize Large052(bool walkable = false, bool ownGlow = false, double modelUp = 0, bool pivotAtBack = false)
        {
            Large = true;
            Walkable = walkable;
            if (walkable) Slender = true;
            OwnGlow = ownGlow;
            ModelUp = modelUp;
            PivotAtBack = pivotAtBack;
            return this;
        }

        /// <summary>Metres of the dais model (0.6 m high, too high to step onto) that show over the floor: it is sunk to a low platform walked over (work order 052).</summary>
        public const double DaisShows = 0.15;
        /// <summary>Metres over the floor where a banner's top (the back of its brackets) hangs on the wall: just under the kit wall's 3 m top (work order 052).</summary>
        public const double BannerTop = 2.9;

        /// <summary>
        /// The eleven, by <see cref="PropModel"/>. Heights are the ones the models were made to (Unity's bounds agree to
        /// the millimetre); footprints are their bounds as Unity imports them (work order 009's import test), rounded up
        /// to the next 5 cm: a circle for the round ones (half the larger side), a rectangle for the rest.
        /// Work order 028: the seven after them, the same way from their import test (the bounds in the comments are
        /// Unity's), and the statue and the cage may stand against a wall. The candelabra's footprint is its foot and stem,
        /// a little inside the reach of its arms: it is slender.
        /// </summary>
        public static readonly PropSize[] Crypt =
        {
            Box(PropModel.Altar, 1.2, 1.3, 1.3, 'A', atWall: true).With(front: true),           // bounds 1.165 x 1.274
            Round(PropModel.BonePile, 0.45, 0.6, '~', atWall: true),          // 0.820 x 0.827
            Round(PropModel.Brazier, 0.75, 1.1, '*'),                         // 1.488 x 1.491
            Box(PropModel.Chest, 1.05, 0.75, 0.9, '$').With(front: true),     // 1.022 x 0.742
            Box(PropModel.GraveMarker, 1.1, 0.7, 1.3, '!', atWall: true).With(front: true), // 1.079 x 0.671
            Round(PropModel.Pillar, 0.6, 3.0, '#', anyAngle: true),           // 1.102 x 1.127
            Round(PropModel.PillarBroken, 0.6, 1.2, '%', anyAngle: true),     // 1.130 x 1.135
            Box(PropModel.Rubble, 1.6, 1.5, 0.8, ':', anyAngle: true),        // 1.569 x 1.458
            Box(PropModel.Sarcophagus, 1.9, 1.1, 1.0, '='),                   // 1.856 x 1.061
            Box(PropModel.Statue, 1.05, 1.1, 2.6, '&', atWall: true).With(front: true),     // 1.042 x 1.072
            Box(PropModel.Urns, 1.0, 1.05, 1.1, 'u', atWall: true),           // 0.974 x 1.003
            // Work order 028
            Box(PropModel.Coffin, 2.15, 1.15, 1.0, '[').With(emission: true),                            // 2.100 x 1.124
            Round(PropModel.SkullPile, 0.85, 0.88, '^', anyAngle: true).With(emission: true),            // 1.600 x 1.518
            Round(PropModel.Candelabra, 0.35, 1.8, 'i').With(slender: true, emission: true),             // 0.834 x 0.496
            Box(PropModel.BoneThrone, 2.15, 1.75, 2.6, 'Y', atWall: true).With(front: true, wallPiece: true, emission: true), // 2.104 x 1.732; front +Z
            Box(PropModel.Bookshelf, 2.05, 0.7, 2.17, 'k', atWall: true).With(frontMinusZ: true, wallPiece: true, emission: true), // 2.000 x 0.696; front -Z
            Box(PropModel.Cage, 1.25, 1.25, 2.0, 'q', atWall: true).With(emission: true),                // 1.214 x 1.219
            Round(PropModel.Basin, 0.75, 1.07, 'o').With(emission: true),                                // 1.396 x 1.400
            // Work order 052, the eight large props (their bounds as the import reads them, Blender's table in brackets): the
            // knight and the great altar face +Z (the sword's glowing line lies at +0.22 m; of the altar's glowing points below
            // 1.3 m, 12 face +Z and 7 -Z); the dais is sunk to a low platform; the banner hangs from its top on a wall, its
            // face +Z; the bone wall's flat back goes against a wall, its face +Z.
            Box(PropModel.KnightStatue, 1.4, 1.4, 3.0, 'K', atWall: true).With(front: true, emission: true).Large052(),                 // 1.382 x 1.396
            Box(PropModel.GreatAltar, 3.0, 1.55, 1.82, 'T').With(front: true, emission: true).Large052(),                              // 3.000 x 1.501
            Box(PropModel.Dais, 4.0, 4.0, 0.6, '_', atWall: true).With(emission: true).Large052(walkable: true, modelUp: DaisShows - 0.6), // 3.982 x 4.000
            Box(PropModel.Banner, 1.5, 0.25, 2.5, 'b', atWall: true).With(front: true, wallPiece: true).Large052(walkable: true, modelUp: BannerTop, pivotAtBack: true), // 1.478 x 0.225
            Round(PropModel.GreatBrazier, 0.65, 1.5, '@').With(emission: true).Large052(),                                             // 1.243 x 1.230
            Box(PropModel.CoinHeap, 2.0, 1.85, 0.63, 'c', anyAngle: true).With(emission: true).Large052(ownGlow: true),                // 2.000 x 1.843
            Box(PropModel.BoneWall, 3.0, 0.5, 2.5, 'W', atWall: true).With(front: true, wallPiece: true, emission: true).Large052(pivotAtBack: true), // 3.000 x 0.500
            Round(PropModel.CandleCluster, 0.35, 1.0, ',', anyAngle: true, atWall: true).With(emission: true).Large052(ownGlow: true),              // 0.637 x 0.654
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

        /// <summary>
        /// Metres from the footprint to one wall line of its room (work order 028): <see cref="LevelGenerator.North"/>,
        /// East, South or West; negative when it reaches past it.
        /// </summary>
        public double DistanceToSide(CellRect room, double cellSize, int side)
        {
            Extent(out double minX, out double minY, out double maxX, out double maxY);
            switch (side)
            {
                case LevelGenerator.North: return room.Top * cellSize - maxY;
                case LevelGenerator.East: return room.Right * cellSize - maxX;
                case LevelGenerator.South: return minY - room.Y * cellSize;
                default: return minX - room.X * cellSize;
            }
        }

        /// <summary>The side of its room whose wall line is nearest the footprint, and how far.</summary>
        public int NearestSide(CellRect room, double cellSize, out double distance)
        {
            int best = LevelGenerator.North;
            distance = double.MaxValue;
            for (int side = 0; side < 4; side++)
            {
                double d = DistanceToSide(room, cellSize, side);
                if (d < distance - 1e-9)
                {
                    distance = d;
                    best = side;
                }
            }
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
        /// <summary>Slender (work order 028, <see cref="PropSize.Slender"/>): it stops no shot, whatever its height.</summary>
        public bool Slender;
        /// <summary>
        /// Put for the room's fight (work order 088, <see cref="FightCover"/>): a tall or low cover the generator added. It keeps
        /// <see cref="PropValues.CoverSpacing"/> from the other props, in place of the open floor's <see cref="PropValues.FloorSpacing"/>
        /// (the joined floor keeps the ways between them, <see cref="JoinedFloor"/>).
        /// </summary>
        public bool Fight;

        public static LevelProp Make(PropModel model, LevelPoint position, int rotation, PropPlace place)
        {
            PropSize s = PropSize.Of(model);
            return new LevelProp
            {
                Model = model, Position = position, Rotation = rotation, Place = place, Shape = s.Shape,
                Width = s.Shape == PropShape.Rectangle ? s.Width : 0, Depth = s.Shape == PropShape.Rectangle ? s.Depth : 0,
                Radius = s.Shape == PropShape.Circle ? s.Radius : 0, Height = s.Height, Slender = s.Slender,
            };
        }

        /// <summary>
        /// The way its front faces on the ground (work order 028), as a unit (x east, y north): its own +Z turned by its
        /// rotation, or -Z for a model whose front is there (<see cref="PropSize.FrontMinusZ"/>).
        /// </summary>
        public LevelPoint Front
        {
            get
            {
                double a = (Rotation + PropSize.Of(Model).FrontTurn) * Math.PI / 180.0;
                return new LevelPoint(Math.Sin(a), Math.Cos(a));
            }
        }

        /// <summary>The ground it covers, in square metres.</summary>
        public double Area => Shape == PropShape.Circle ? Math.PI * Radius * Radius : Width * Depth;

        public PropFootprint Footprint => new PropFootprint
        {
            Shape = Shape, Centre = Position, Rotation = Rotation, HalfWidth = Width * 0.5, HalfDepth = Depth * 0.5, Radius = Radius,
        };

        public char Letter => PropSize.Of(Model).Letter;

        /// <summary>
        /// Does it stop shots (Kristian, 2026-11-19)? A prop as tall as <see cref="PropValues.CoverHeight"/> or taller
        /// does; she shoots over a lower one, which still blocks walking. Its height is its table entry's
        /// (<see cref="PropSize.Crypt"/>); a millimetre of slack, so a 1.2 m prop against a 1.2 m float counts. A slender
        /// one (work order 028, the candelabra) never does, however tall.
        /// </summary>
        public bool IsCover(PropValues v) => !Slender && Height >= v.CoverHeight - 1e-3;

        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture, "{0} {1} at {2} turned {3} ({4} {5})", Place.ToString().ToLowerInvariant(), Model, Position, Rotation,
                          Shape == PropShape.Circle ? "circle" : "rect",
                          Shape == PropShape.Circle ? Radius.ToString("R", CultureInfo.InvariantCulture)
                          : Width.ToString("R", CultureInfo.InvariantCulture) + "x" + Depth.ToString("R", CultureInfo.InvariantCulture));
    }
}
