using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The recorder's events of how much of the room she uses (work order 097, Kristian the morning of 2026-11-23 on the room whose
    /// doors shut behind her: "føles veldig riktig at den lukker seg, men skulle ønske man fikk brukt mer av rommet. nå blir det bare
    /// at man kiter det rundt seg"). Only during a room's fight, from its waking to its end; outside fights nothing new is written.
    /// Every place is in the room's own metres, measured from the door she came in by: "d" the depth straight in from that door's
    /// wall, "s" sideways from the door's middle, positive to her right as she faces in. So rooms of other shapes and turns compare.
    /// </summary>
    public static class RoomUseEvents
    {
        /// <summary>
        /// A room's fight began and its room as she meets it: floor, room (as the run box counts), order, kind, template; depth (m from
        /// the door's wall to the far wall), left (s of the room's left wall), rows and cols of its 2 m grid, cells (a row a depth band
        /// from the door in, joined by "/", "." walkable floor and "#" not), door (the door's width, m), covers (its tall and low
        /// covers, each "t" or "l", d, s, then "c" radius or "r" half width, half depth and turn in degrees; joined by ";").
        /// </summary>
        public const string Room = "fight_room";
        /// <summary>Her place in the fight: d, s (m, to 0.1). The first as the fight begins, then by <see cref="RoomUseRules.ShouldWrite"/>.</summary>
        public const string Place = "fight_at";
        /// <summary>An enemy of the fight died: enemy_id, and its place d, s.</summary>
        public const string Death = "fight_death";
        /// <summary>The fight ended: result why (cleared, her death, she left, another fight, floor gone), and her last place d, s.</summary>
        public const string End = "fight_end";
    }

    /// <summary>The numbers of work order 097's measure, first guesses of the order; the producer tunes them by reading summaries.</summary>
    public static class RoomUseValues
    {
        /// <summary>s at least between two of her places in the log: four a second at most, as the move sample (<c>PlaySessionRecorder.MoveSampleSeconds</c>).</summary>
        public const double SampleSeconds = 0.25;
        /// <summary>m she must have moved since the last place written before another is (the order: "more than half a metre").</summary>
        public const double SampleMetres = 0.5;
        /// <summary>
        /// The slack of the two comparisons above: a time or a distance this much short of the bound still counts. Unity's runtime
        /// adds floats wider than a float is (the drag hold's three tests of 2026-11-23); a stated slack makes the boundary the same
        /// under both runtimes.
        /// </summary>
        public const double Slack = 1e-6;
        /// <summary>m on a side of the grid's cells (the order: "a 2 m grid"): half a level cell (<see cref="LevelValues.CellSize"/> 4 m).</summary>
        public const double CellMetres = 2.0;
        /// <summary>Points a side of a cell the walkable test asks (4 by 4, 0.5 m apart): a cell is walkable floor when at least half are.</summary>
        public const int CellPointsPerSide = 4;
        /// <summary>m apart the points along her way between two places that mark the cells she passed through.</summary>
        public const double WayStep = 0.25;
        /// <summary>m from a cover's footprint that counts as at it (the order: "within 1.5 m").</summary>
        public const double CoverReach = 1.5;
        /// <summary>s in all of a fight within <see cref="CoverReach"/> of a cover that make it a cover used (the order: "for more than a second").</summary>
        public const double CoverSeconds = 1.0;
        /// <summary>The share of the fight's time the circle holds (the order: "the circle that holds 90 % of her time").</summary>
        public const double CircleShare = 0.9;
        /// <summary>m, the steps the circle's middle is moved by as it is found (<see cref="RoomUseRules.Measure"/>): from a cell down to an eighth of a metre.</summary>
        public static readonly double[] CircleSteps = { 2.0, 1.0, 0.5, 0.25, 0.125 };
        /// <summary>The map's three levels of her time in a cell, as shares of the fight's time: under this ':'...</summary>
        public const double LightShare = 0.02;
        /// <summary>...under this 'o', and this or more '@'. An even spread over a large room's hundred cells is 1 % a cell; a ring of a dozen cells 8 %.</summary>
        public const double HeavyShare = 0.06;
        /// <summary>The longest fights of a session drawn as a map (the order: "the three longest fights").</summary>
        public const int MapsShown = 3;
    }

    /// <summary>
    /// The room's own metres (work order 097): an origin at the middle of the door she came in by, the depth straight in from its wall,
    /// and the side to her right as she faces in. The map from the ground (x east, y north) is a turn by a multiple of 90 degrees, never a
    /// mirror, so a footprint keeps its shape and only its turn changes (<see cref="Turn"/>).
    /// </summary>
    public struct RoomFrame
    {
        public double OriginX, OriginY;
        /// <summary>The unit way in, and the unit way to her right as she faces in, on the ground.</summary>
        public double InX, InY, RightX, RightY;
        /// <summary>Degrees added to a footprint's clockwise turn (<see cref="PropFootprint.Rotation"/>) as it is put in the frame.</summary>
        public int Turn;

        /// <summary>A ground point in the frame: depth in from the door's wall, side from the door's middle (to her right positive).</summary>
        public void ToFrame(double x, double y, out double depth, out double side)
        {
            double dx = x - OriginX, dy = y - OriginY;
            depth = dx * InX + dy * InY;
            side = dx * RightX + dy * RightY;
        }

        /// <summary>The ground point of a place in the frame.</summary>
        public LevelPoint FromFrame(double depth, double side) =>
            new LevelPoint(OriginX + InX * depth + RightX * side, OriginY + InY * depth + RightY * side);

        /// <summary>The same frame with its origin at another point (the recorder's: the level's ground moved into the scene's).</summary>
        public RoomFrame MovedTo(double x, double y)
        {
            RoomFrame f = this;
            f.OriginX = x;
            f.OriginY = y;
            return f;
        }
    }

    /// <summary>A place of hers, or of an enemy's death, in a fight: the log's time, and the room's own metres.</summary>
    public struct RoomPlace
    {
        public double T, Depth, Side;

        public RoomPlace(double t, double depth, double side)
        {
            T = t;
            Depth = depth;
            Side = side;
        }
    }

    /// <summary>A tall or low cover of the room, its footprint in the room's own metres (x the side, y the depth).</summary>
    public sealed class RoomCover
    {
        public bool Tall;
        public PropFootprint Footprint;
    }

    /// <summary>
    /// A room as the measure sees it (work order 097): its walkable floor on a 2 m grid in the room's own metres from the door she came
    /// in by, row 0 the band nearest the door, column 0 at the room's left wall as she faces in; and its tall and low covers. Written
    /// into the fight's <see cref="RoomUseEvents.Room"/> event and read back from it, so the summary needs only the log.
    /// </summary>
    public sealed class RoomSpace
    {
        /// <summary>m from the door's wall to the room's far wall; and the side of its left wall (negative, or 0 for a door in a corner).</summary>
        public double Depth, Left;
        public int Rows, Cols;
        /// <summary>m across the door she came in by.</summary>
        public double DoorWidth;
        /// <summary>Row-major, row 0 nearest the door: walkable floor.</summary>
        public bool[] Walkable = Array.Empty<bool>();
        public readonly List<RoomCover> Covers = new List<RoomCover>();

        public bool IsWalkable(int row, int col) => row >= 0 && col >= 0 && row < Rows && col < Cols && Walkable[row * Cols + col];

        public int WalkableCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Walkable.Length; i++) if (Walkable[i]) n++;
                return n;
            }
        }

        /// <summary>
        /// The room's depth as the shares are taken of it: the far edge of the deepest band that holds any walkable floor (the
        /// room's whole depth unless a template walls its far end in); the grid's depth for a room with none.
        /// </summary>
        public double FloorDepth
        {
            get
            {
                for (int r = Rows - 1; r >= 0; r--)
                    for (int c = 0; c < Cols; c++)
                        if (Walkable[r * Cols + c]) return (r + 1) * RoomUseValues.CellMetres;
                return Depth;
            }
        }

        /// <summary>The cell of a place, or false outside the grid.</summary>
        public bool CellOf(double depth, double side, out int row, out int col)
        {
            row = (int)Math.Floor(depth / RoomUseValues.CellMetres);
            col = (int)Math.Floor((side - Left) / RoomUseValues.CellMetres);
            return row >= 0 && col >= 0 && row < Rows && col < Cols;
        }

        /// <summary>
        /// The room as built, seen from <paramref name="door"/> in <paramref name="frame"/> (<see cref="RoomUseRules.FrameOf"/>):
        /// a 2 m cell is walkable floor when at least half of its 4 by 4 points are on the room's own open floor
        /// (<see cref="LevelRoom.IsOpen"/>: a template's walls and pillars of stone are not) and inside no prop that blocks walking
        /// (all but those she walks over, <see cref="PropSize.Walkable"/>). <paramref name="props"/>: the room's props as they stand
        /// (the mood's large props among them, those a large prop took the place of left out). Its covers are those of work order
        /// 088's count (<see cref="FightCover.IsTall"/>, <see cref="FightCover.IsLow"/>).
        /// </summary>
        public static RoomSpace Make(LevelLayout level, LevelRoom room, LevelDoor door, RoomFrame frame, IReadOnlyList<LevelProp> props)
        {
            double cs = level.CellSize, cell = RoomUseValues.CellMetres;
            var space = new RoomSpace();
            CellRect r = room.Rect;
            double minS = double.MaxValue, maxS = double.MinValue, maxD = double.MinValue;
            foreach ((double x, double y) in new[] { (r.X * cs, r.Y * cs), (r.Right * cs, r.Y * cs), (r.Right * cs, r.Top * cs), (r.X * cs, r.Top * cs) })
            {
                frame.ToFrame(x, y, out double d, out double s);
                minS = Math.Min(minS, s);
                maxS = Math.Max(maxS, s);
                maxD = Math.Max(maxD, d);
            }
            space.Left = minS;
            space.Depth = maxD;
            space.Rows = Math.Max(0, (int)Math.Round(maxD / cell));
            space.Cols = Math.Max(0, (int)Math.Round((maxS - minS) / cell));
            space.DoorWidth = door != null ? door.Width * cs : 0d;
            space.Walkable = new bool[space.Rows * space.Cols];

            // The props that block walking, each with the box of ground round it, so a cell asks only those near it.
            var blocking = new List<(PropFootprint f, double minX, double minY, double maxX, double maxY)>();
            if (props != null)
                foreach (LevelProp p in props)
                {
                    if (p == null) continue;
                    if (FightCover.IsTall(p) || FightCover.IsLow(p))
                    {
                        frame.ToFrame(p.Position.X, p.Position.Y, out double d, out double s);
                        PropFootprint f = p.Footprint;
                        f.Centre = new LevelPoint(s, d);
                        f.Rotation = p.Rotation + frame.Turn;
                        space.Covers.Add(new RoomCover { Tall = FightCover.IsTall(p), Footprint = f });
                    }
                    if (PropSize.Of(p.Model).Walkable) continue;
                    PropFootprint ground = p.Footprint;
                    ground.Extent(out double x0, out double y0, out double x1, out double y1);
                    blocking.Add((ground, x0, y0, x1, y1));
                }

            int n = RoomUseValues.CellPointsPerSide;
            double step = cell / n;
            for (int row = 0; row < space.Rows; row++)
                for (int col = 0; col < space.Cols; col++)
                {
                    int free = 0;
                    for (int i = 0; i < n; i++)
                        for (int j = 0; j < n; j++)
                        {
                            LevelPoint g = frame.FromFrame(row * cell + (j + 0.5) * step, minS + col * cell + (i + 0.5) * step);
                            if (!room.IsOpen((int)Math.Floor(g.X / cs), (int)Math.Floor(g.Y / cs))) continue;
                            bool inside = false;
                            foreach (var b in blocking)
                                if (g.X >= b.minX && g.X <= b.maxX && g.Y >= b.minY && g.Y <= b.maxY && b.f.DistanceTo(g) <= 0d)
                                {
                                    inside = true;
                                    break;
                                }
                            if (!inside) free++;
                        }
                    space.Walkable[row * space.Cols + col] = 2 * free >= n * n;
                }
            return space;
        }

        // ---------------- The log ----------------

        static string M(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);

        /// <summary>Writes the room into its fight's event (<see cref="RoomUseEvents.Room"/>).</summary>
        public void WriteTo(SessionEvent e)
        {
            e.AddExtra("depth", M(Depth));
            e.AddExtra("left", M(Left));
            e.AddExtra("rows", Rows.ToString(CultureInfo.InvariantCulture));
            e.AddExtra("cols", Cols.ToString(CultureInfo.InvariantCulture));
            var cells = new StringBuilder(Rows * (Cols + 1));
            for (int row = 0; row < Rows; row++)
            {
                if (row > 0) cells.Append('/');
                for (int col = 0; col < Cols; col++) cells.Append(Walkable[row * Cols + col] ? '.' : '#');
            }
            e.AddExtra("cells", cells.ToString());
            e.AddExtra("door", M(DoorWidth));
            var covers = new StringBuilder();
            foreach (RoomCover c in Covers)
            {
                if (covers.Length > 0) covers.Append(';');
                PropFootprint f = c.Footprint;
                covers.Append(c.Tall ? 't' : 'l').Append(' ').Append(M(f.Centre.Y)).Append(' ').Append(M(f.Centre.X)).Append(' ');
                if (f.Shape == PropShape.Circle) covers.Append("c ").Append(M(f.Radius));
                else
                    covers.Append("r ").Append(M(f.HalfWidth)).Append(' ').Append(M(f.HalfDepth)).Append(' ')
                          .Append((((f.Rotation % 360) + 360) % 360).ToString(CultureInfo.InvariantCulture));
            }
            e.AddExtra("covers", covers.ToString());
        }

        /// <summary>The room of a fight's event, or null when the event holds none (or is not one).</summary>
        public static RoomSpace From(SessionEvent e)
        {
            if (e == null || e.Type != RoomUseEvents.Room) return null;
            double Num(string key) => double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
            int rows = (int)Num("rows"), cols = (int)Num("cols");
            string cells = e.ExtraValue("cells") ?? "";
            if (rows <= 0 || cols <= 0 || double.IsNaN(Num("depth"))) return null;
            var space = new RoomSpace
            {
                Depth = Num("depth"), Left = Num("left"), Rows = rows, Cols = cols, Walkable = new bool[rows * cols],
                DoorWidth = double.IsNaN(Num("door")) ? 0d : Num("door"),
            };
            string[] bands = cells.Split('/');
            for (int row = 0; row < rows && row < bands.Length; row++)
                for (int col = 0; col < cols && col < bands[row].Length; col++)
                    space.Walkable[row * cols + col] = bands[row][col] == '.';
            foreach (string one in (e.ExtraValue("covers") ?? "").Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string[] w = one.Split(' ');
                if (w.Length < 5) continue;
                double P(int i) => i < w.Length && double.TryParse(w[i], NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;
                var f = new PropFootprint { Centre = new LevelPoint(P(2), P(1)) };
                if (w[3] == "c")
                {
                    f.Shape = PropShape.Circle;
                    f.Radius = P(4);
                }
                else
                {
                    f.Shape = PropShape.Rectangle;
                    f.HalfWidth = P(4);
                    f.HalfDepth = P(5);
                    f.Rotation = (int)P(6);
                }
                space.Covers.Add(new RoomCover { Tall = w[0] == "t", Footprint = f });
            }
            return space;
        }
    }

    /// <summary>The measures of one room's fight (work order 097). NaN where a fight gives none (no place, no death).</summary>
    public sealed class RoomUseMeasures
    {
        public double Seconds;
        /// <summary>The 2 m cells of walkable floor she stood in or passed through, the room's walkable cells, and the share.</summary>
        public int CellsUsed, CellsWalkable;
        public double UsedShare = double.NaN;
        /// <summary>m of the room's depth (<see cref="RoomSpace.FloorDepth"/>) the shares are taken of.</summary>
        public double RoomDepth = double.NaN;
        /// <summary>Her farthest depth from the door in (m), and as a share of the room's depth (0 to 1).</summary>
        public double Farthest = double.NaN, FarthestShare = double.NaN;
        /// <summary>Her median depth over the fight's time (m), and as a share.</summary>
        public double MedianDepth = double.NaN, MedianDepthShare = double.NaN;
        /// <summary>m she went, place to place.</summary>
        public double Travel;
        /// <summary>
        /// m: the radius of the smallest circle (as found from the mean of her places) that held <see cref="RoomUseValues.CircleShare"/>
        /// of the fight's time. Small with a long <see cref="Travel"/>: kiting round a spot.
        /// </summary>
        public double Circle = double.NaN;
        /// <summary>The enemies that died in the fight, and their median depth (m) and its share: do they come to her or she to them.</summary>
        public int Deaths;
        public double DeathsDepth = double.NaN, DeathsDepthShare = double.NaN;
        /// <summary>s she sprinted in the fight and her jumps over low props (work order 092's events), filled by the summary.</summary>
        public double SprintSeconds;
        public int Jumps;
        /// <summary>The room's tall and low covers, and those she was within <see cref="RoomUseValues.CoverReach"/> of for more than <see cref="RoomUseValues.CoverSeconds"/>.</summary>
        public int Covers, CoversUsed;
        /// <summary>s of the fight's time she stood in each cell (row-major as <see cref="RoomSpace.Walkable"/>), and the cells she passed through; for the map.</summary>
        public double[] CellSeconds = Array.Empty<double>();
        public bool[] CellUsed = Array.Empty<bool>();
    }

    /// <summary>
    /// How much of the room she uses (work order 097): the frame from the door she came in by, the rule of when her place is written,
    /// the measures of a fight from its places, and the fight's room as a small text map. No engine.
    /// </summary>
    public static class RoomUseRules
    {
        /// <summary>
        /// The room's own metres seen from <paramref name="door"/>: the origin at the door's middle, in straight across its wall. A door
        /// on none of the room's walls, or none, is taken as on its south wall at its middle (the hand-made layouts' case).
        /// </summary>
        public static RoomFrame FrameOf(LevelLayout level, LevelRoom room, LevelDoor door)
        {
            double cs = level.CellSize;
            CellRect r = room.Rect;
            double mx = (r.X + r.Right) * 0.5 * cs, my = r.Y * cs;
            int wall = LevelGenerator.South;
            if (door != null)
            {
                door.Opening(cs, out LevelPoint a, out LevelPoint b);
                int w = LevelGeometry.WallOf(r, door);
                if (w >= 0)
                {
                    wall = w;
                    mx = (a.X + b.X) * 0.5;
                    my = (a.Y + b.Y) * 0.5;
                }
            }
            switch (wall)
            {
                case LevelGenerator.North: return new RoomFrame { OriginX = mx, OriginY = my, InX = 0, InY = -1, RightX = -1, RightY = 0, Turn = 180 };
                case LevelGenerator.West: return new RoomFrame { OriginX = mx, OriginY = my, InX = 1, InY = 0, RightX = 0, RightY = -1, Turn = -90 };
                case LevelGenerator.East: return new RoomFrame { OriginX = mx, OriginY = my, InX = -1, InY = 0, RightX = 0, RightY = 1, Turn = 90 };
                default: return new RoomFrame { OriginX = mx, OriginY = my, InX = 0, InY = 1, RightX = 1, RightY = 0, Turn = 0 };
            }
        }

        /// <summary>
        /// The sample rule: her place at <paramref name="t"/> is written when at least <see cref="RoomUseValues.SampleSeconds"/> have gone
        /// since the last one written and she is more than <see cref="RoomUseValues.SampleMetres"/> from it (both less the slack). So
        /// four lines a second at most, and none while she stands or shuffles: a place holds until the next.
        /// </summary>
        public static bool ShouldWrite(double lastT, double lastDepth, double lastSide, double t, double depth, double side)
        {
            if (t - lastT < RoomUseValues.SampleSeconds - RoomUseValues.Slack) return false;
            double dd = depth - lastDepth, ds = side - lastSide;
            double m = RoomUseValues.SampleMetres - RoomUseValues.Slack;
            return dd * dd + ds * ds > m * m;
        }

        /// <summary>
        /// The measures of a fight in <paramref name="space"/> from her places (in time order; each holds from its time to the next
        /// one's, the last to <paramref name="end"/>) and the enemies' deaths. Sprint and jumps are the summary's (from their own events).
        /// </summary>
        public static RoomUseMeasures Measure(RoomSpace space, IReadOnlyList<RoomPlace> places, double start, double end, IReadOnlyList<RoomPlace> deaths)
        {
            var m = new RoomUseMeasures { Seconds = Math.Max(0d, end - start) };
            int cells = space != null ? space.Rows * space.Cols : 0;
            m.CellSeconds = new double[cells];
            m.CellUsed = new bool[cells];
            m.CellsWalkable = space != null ? space.WalkableCount : 0;
            m.Covers = space != null ? space.Covers.Count : 0;
            double roomDepth = space != null ? space.FloorDepth : double.NaN;
            m.RoomDepth = roomDepth;
            double Share(double v) => roomDepth > 0d ? Math.Max(0d, Math.Min(1d, v / roomDepth)) : double.NaN;

            if (deaths != null && deaths.Count > 0)
            {
                var depths = new List<(double v, double w)>();
                foreach (RoomPlace d in deaths) depths.Add((d.Depth, 1d));
                m.Deaths = deaths.Count;
                m.DeathsDepth = Quantile(depths, 0.5);
                m.DeathsDepthShare = Share(m.DeathsDepth);
            }
            int n = places != null ? places.Count : 0;
            if (n == 0) return m;

            // Each place's time: from it to the next, the last to the end. A fight with no time weighs its places alike.
            var w = new double[n];
            double total = 0d;
            for (int i = 0; i < n; i++)
            {
                double until = i + 1 < n ? places[i + 1].T : Math.Max(end, places[i].T);
                w[i] = Math.Max(0d, until - places[i].T);
                total += w[i];
            }
            if (total <= 0d)
                for (int i = 0; i < n; i++) w[i] = 1d;

            var depthsW = new List<(double v, double w)>(n);
            double farthest = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                RoomPlace p = places[i];
                depthsW.Add((p.Depth, w[i]));
                farthest = Math.Max(farthest, p.Depth);
                if (i > 0)
                {
                    double dd = p.Depth - places[i - 1].Depth, ds = p.Side - places[i - 1].Side;
                    m.Travel += Math.Sqrt(dd * dd + ds * ds);
                }
                if (space != null && space.CellOf(p.Depth, p.Side, out int row, out int col)) m.CellSeconds[row * space.Cols + col] += w[i];
            }
            m.Farthest = Math.Max(0d, farthest);
            m.FarthestShare = Share(m.Farthest);
            m.MedianDepth = Quantile(depthsW, 0.5);
            m.MedianDepthShare = Share(m.MedianDepth);
            m.Circle = Circle(places, w);

            if (space == null) return m;
            // The cells she stood in or passed through: her places, and points every WayStep along the way from each to the next.
            void Mark(double depth, double side)
            {
                if (space.CellOf(depth, side, out int row, out int col)) m.CellUsed[row * space.Cols + col] = true;
            }
            for (int i = 0; i < n; i++)
            {
                Mark(places[i].Depth, places[i].Side);
                if (i == 0) continue;
                RoomPlace a = places[i - 1], b = places[i];
                double len = Math.Sqrt((b.Depth - a.Depth) * (b.Depth - a.Depth) + (b.Side - a.Side) * (b.Side - a.Side));
                int steps = (int)Math.Ceiling(len / RoomUseValues.WayStep);
                for (int k = 1; k < steps; k++)
                {
                    double f = k / (double)steps;
                    Mark(a.Depth + (b.Depth - a.Depth) * f, a.Side + (b.Side - a.Side) * f);
                }
            }
            for (int i = 0; i < cells; i++)
                if (m.CellUsed[i] && space.Walkable[i]) m.CellsUsed++;
            m.UsedShare = m.CellsWalkable > 0 ? m.CellsUsed / (double)m.CellsWalkable : double.NaN;

            // The covers she was at: within CoverReach of its footprint for more than CoverSeconds of the fight in all.
            foreach (RoomCover c in space.Covers)
            {
                double near = 0d;
                for (int i = 0; i < n; i++)
                    if (c.Footprint.DistanceTo(new LevelPoint(places[i].Side, places[i].Depth)) <= RoomUseValues.CoverReach + RoomUseValues.Slack) near += total > 0d ? w[i] : 0d;
                if (near > RoomUseValues.CoverSeconds + RoomUseValues.Slack) m.CoversUsed++;
            }
            return m;
        }

        /// <summary>
        /// m: the radius of the smallest circle that holds <see cref="RoomUseValues.CircleShare"/> of her time, as found from the mean of
        /// her places over time: its middle moved a step at a time to any of the eight ways round while that makes it smaller, the steps
        /// <see cref="RoomUseValues.CircleSteps"/> (2 m down to 0.125 m). So a ring keeps its middle (medians of a part of a turn would
        /// not), and a few runs away from her spot do not pull the circle off it (a mean alone would).
        /// </summary>
        static double Circle(IReadOnlyList<RoomPlace> places, double[] w)
        {
            int n = places.Count;
            var away = new List<(double v, double w)>(n);
            double Radius(double d, double s)
            {
                away.Clear();
                for (int i = 0; i < n; i++)
                {
                    double dd = places[i].Depth - d, ds = places[i].Side - s;
                    away.Add((Math.Sqrt(dd * dd + ds * ds), w[i]));
                }
                return Quantile(away, RoomUseValues.CircleShare);
            }
            double sw = 0d, cd = 0d, cs = 0d;
            for (int i = 0; i < n; i++)
            {
                sw += w[i];
                cd += w[i] * places[i].Depth;
                cs += w[i] * places[i].Side;
            }
            if (sw <= 0d) return double.NaN;
            cd /= sw;
            cs /= sw;
            double best = Radius(cd, cs);
            foreach (double step in RoomUseValues.CircleSteps)
                for (int moves = 0; moves < 64; moves++) // a guard: each move makes it smaller, so it stops by itself
                {
                    bool moved = false;
                    for (int way = 0; way < 8 && !moved; way++)
                    {
                        double a = way * Math.PI / 4d, d = cd + step * Math.Cos(a), s = cs + step * Math.Sin(a);
                        double r = Radius(d, s);
                        if (r >= best - 1e-9) continue;
                        best = r;
                        cd = d;
                        cs = s;
                        moved = true;
                    }
                    if (!moved) break;
                }
            return best;
        }

        /// <summary>The value below which <paramref name="q"/> of the weight lies (the first, in order of value, at which the weight so far reaches it).</summary>
        public static double Quantile(List<(double v, double w)> values, double q)
        {
            if (values == null || values.Count == 0) return double.NaN;
            values.Sort((a, b) => a.v.CompareTo(b.v));
            double total = 0d;
            foreach (var x in values) total += x.w;
            if (total <= 0d) return values[(int)Math.Min(values.Count - 1, Math.Floor(q * values.Count))].v;
            double so = 0d, want = q * total - 1e-9 * total;
            foreach (var x in values)
            {
                so += x.w;
                if (so >= want) return x.v;
            }
            return values[values.Count - 1].v;
        }

        // ---------------- The map ----------------

        /// <summary>
        /// The fight's room as text (work order 097), two characters a 2 m cell so a cell is about square in a terminal (the level's text
        /// map, <see cref="LevelMap"/>, draws a whole level from its layout, which the summary does not have: this one draws a room from
        /// its fight's event, in its manner of characters to a cell). The far wall at the top, the door she came in by at the bottom
        /// (^^ in the wall), her left on the left. A cell's first character: T or L the centre of a tall or low cover, # not walkable
        /// floor, . floor she never stood in, and her time in it by three levels of the fight's time: ':' under 2 %, 'o' under 6 %, '@'
        /// more (':' too for a cell she only passed through). Its second: x where an enemy died, X where more than one did.
        /// </summary>
        public static List<string> Map(RoomSpace space, RoomUseMeasures m, IReadOnlyList<RoomPlace> deaths)
        {
            var lines = new List<string>();
            if (space == null || space.Rows == 0 || space.Cols == 0) return lines;
            var died = new int[space.Rows * space.Cols];
            if (deaths != null)
                foreach (RoomPlace d in deaths)
                    if (space.CellOf(d.Depth, d.Side, out int row, out int col)) died[row * space.Cols + col]++;
            var cover = new char[space.Rows * space.Cols];
            foreach (RoomCover c in space.Covers)
                if (space.CellOf(c.Footprint.Centre.Y, c.Footprint.Centre.X, out int row, out int col)) cover[row * space.Cols + col] = c.Tall ? 'T' : 'L';
            double fight = m != null ? m.Seconds : 0d;

            string wall = "+" + new string('-', 2 * space.Cols) + "+";
            lines.Add(wall);
            for (int row = space.Rows - 1; row >= 0; row--)
            {
                var line = new StringBuilder(2 * space.Cols + 2).Append('|');
                for (int col = 0; col < space.Cols; col++)
                {
                    int i = row * space.Cols + col;
                    char first;
                    if (cover[i] != '\0') first = cover[i];
                    else if (!space.Walkable[i]) first = '#';
                    else
                    {
                        double s = m != null && i < m.CellSeconds.Length ? m.CellSeconds[i] : 0d;
                        bool used = m != null && i < m.CellUsed.Length && m.CellUsed[i];
                        double share = fight > 0d ? s / fight : 0d;
                        first = s <= 0d ? (used ? ':' : '.')
                              : share < RoomUseValues.LightShare ? ':' : share < RoomUseValues.HeavyShare ? 'o' : '@';
                    }
                    line.Append(first).Append(died[i] > 1 ? 'X' : died[i] == 1 ? 'x' : ' ');
                }
                lines.Add(line.Append('|').ToString());
            }
            var door = new StringBuilder(wall);
            for (int col = 0; col < space.Cols; col++)
            {
                double mid = space.Left + (col + 0.5) * RoomUseValues.CellMetres;
                if (Math.Abs(mid) < space.DoorWidth * 0.5) door[1 + 2 * col] = door[2 + 2 * col] = '^';
            }
            lines.Add(door.ToString());
            return lines;
        }
    }
}
