using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The joined floor (work order 088, point 1; work order 072's step 2: its PlayMode run found props closing bays, alcoves and
    /// strips of a room into pockets, in its second set of templates and in cross-hall, knights-apse and nave-apse of the first).
    /// After the props stand, every piece of a room's walkable floor is joined to its doors, and a way of whole cells runs through
    /// it. Two rules, on the room's own floor (its rectangle less its carved cells), with no NavMesh: the same answer in the rules,
    /// the generator and the tests.
    ///
    /// - No pocket. On a grid of <see cref="PropValues.LaneGrid"/> (0.5 m), a square is free when its middle is at least
    ///   <see cref="CoverValues.JoinRadius"/> from the room's walls (its four sides less the doors' openings), from every carved cell
    ///   and from every prop's footprint. Every square that is free in the room with no prop at all, and joined to a doorway in it
    ///   side to side, is still joined with the props (or is under one): a prop, alone or with a wall or another prop, never shuts
    ///   floor off. A gap narrower than twice the radius (1.6 m) shuts what is behind it: she walks through, not squeezes.
    /// - A way through. In a combat or exit room, the cells (4 m) whose middles are at least <see cref="CoverValues.WayClear"/>
    ///   from every prop join, side to side, the cell inside one door to the cell inside every other (a room with one door: to its
    ///   middle's cell): the way work order 072's PlayMode test walks, its cell middles a metre from the NavMesh's edge. Asked only
    ///   where the room with no prop has that way.
    ///
    /// The generator asks <see cref="Keeps"/> of every place a prop is tried at, after every other rule (the lane among them), and a
    /// place it refuses counts as one the lane refused: the prop takes its next place in its own order (it is moved), and is left out
    /// when none is left within its tries (it is dropped). The validator asks <see cref="Fault"/>. Passages have neither rule.
    /// </summary>
    public sealed class JoinedFloor
    {
        const double Slack = 1e-9;

        readonly LevelLayout _level;
        readonly LevelRoom _room;
        readonly double _x0, _y0, _step, _radius, _clear, _cs;
        readonly int _nx, _ny;
        readonly bool[] _wall;     // within the radius of a wall or a carved cell
        readonly bool[] _props;    // within the radius of a prop standing
        readonly bool[] _joined0;  // free and joined in the room with no prop
        readonly bool[] _work;
        readonly bool[] _reached;
        readonly int[] _queue;
        readonly List<int> _starts = new List<int>();

        // The way through, on the room's cells.
        readonly int _cw, _ch;
        readonly bool[] _cellOpen;
        readonly List<LevelProp> _standing = new List<LevelProp>();
        readonly List<int> _wayFrom = new List<int>();
        readonly List<List<int>> _wayTo = new List<List<int>>();
        readonly bool _way;
        readonly bool[] _cellReached;
        readonly int[] _cellQueue;

        /// <summary>Is the room one the rules are asked of: any but a passage.</summary>
        public static bool Asks(LevelRoom room) => room != null && room.Kind != RoomKind.Passage;

        public JoinedFloor(LevelLayout level, LevelRoom room, LevelValues values)
        {
            values = values ?? new LevelValues();
            PropValues pv = values.PropRules ?? new PropValues();
            CoverValues cv = values.CoverRules ?? new CoverValues();
            _level = level;
            _room = room;
            _cs = level.CellSize;
            _step = pv.LaneGrid > 0 ? pv.LaneGrid : 0.5;
            _radius = Math.Max(0.05, cv.JoinRadius);
            _clear = Math.Max(0, cv.WayClear);
            _x0 = room.Rect.X * _cs;
            _y0 = room.Rect.Y * _cs;
            _nx = Math.Max(1, (int)Math.Round(room.Rect.Width * _cs / _step));
            _ny = Math.Max(1, (int)Math.Round(room.Rect.Height * _cs / _step));
            int n = _nx * _ny;
            _wall = new bool[n];
            _props = new bool[n];
            _joined0 = new bool[n];
            _work = new bool[n];
            _reached = new bool[n];
            _queue = new int[n];

            var doors = new List<LevelDoor>(level.DoorsOf(room.Order));
            List<(LevelPoint a, LevelPoint b)> walls = Walls(room.Rect, _cs, doors);
            for (int j = 0; j < _ny; j++)
                for (int i = 0; i < _nx; i++)
                {
                    LevelPoint c = Centre(i, j);
                    if (LevelGeometry.DistanceToWalls(c, room.Rect, _cs) >= _radius) continue;
                    foreach ((LevelPoint a, LevelPoint b) w in walls)
                        if (LevelGeometry.DistanceToSegment(c, w.a, w.b) < _radius - Slack)
                        {
                            _wall[j * _nx + i] = true;
                            break;
                        }
                }
            RoomShapes.MarkNearSolid(room, _cs, _x0, _y0, _step, _nx, _ny, _radius, _wall);

            // The doorways: the row of squares inside the room along each door's opening.
            foreach (LevelDoor d in doors)
            {
                double lo = d.From * _cs, hi = (d.From + d.Width) * _cs;
                if (d.Vertical && (d.Line == room.Rect.X || d.Line == room.Rect.Right))
                {
                    int i = d.Line == room.Rect.X ? 0 : _nx - 1;
                    for (int j = 0; j < _ny; j++)
                        if (Centre(i, j).Y > lo && Centre(i, j).Y < hi) _starts.Add(j * _nx + i);
                }
                else if (!d.Vertical && (d.Line == room.Rect.Y || d.Line == room.Rect.Top))
                {
                    int j = d.Line == room.Rect.Y ? 0 : _ny - 1;
                    for (int i = 0; i < _nx; i++)
                        if (Centre(i, j).X > lo && Centre(i, j).X < hi) _starts.Add(j * _nx + i);
                }
            }
            Flood(_wall, null, _joined0);

            // The way through: the room's cells, the door's inner cells, the middle's.
            _cw = room.Rect.Width;
            _ch = room.Rect.Height;
            _cellOpen = new bool[_cw * _ch];
            _cellReached = new bool[_cw * _ch];
            _cellQueue = new int[_cw * _ch];
            for (int y = 0; y < _ch; y++)
                for (int x = 0; x < _cw; x++)
                    _cellOpen[y * _cw + x] = room.IsOpen(room.Rect.X + x, room.Rect.Y + y);
            if (room.Kind == RoomKind.Combat || room.Kind == RoomKind.Exit)
            {
                var ends = new List<List<int>>();
                foreach (LevelDoor d in doors)
                {
                    var cells = new List<int>();
                    for (int k = 0; k < d.Width; k++)
                        if (RoomShapes.InnerCell(room, d, d.From + k, out int cx, out int cy, out _, out _) && room.IsOpen(cx, cy))
                            cells.Add((cy - room.Rect.Y) * _cw + (cx - room.Rect.X));
                    if (cells.Count > 0) ends.Add(cells);
                }
                if (ends.Count == 1)
                {
                    LevelPoint m = room.Middle(_cs);
                    int mx = (int)Math.Floor(m.X / _cs), my = (int)Math.Floor(m.Y / _cs);
                    if (room.IsOpen(mx, my)) ends.Add(new List<int> { (my - room.Rect.Y) * _cw + (mx - room.Rect.X) });
                }
                if (ends.Count >= 2)
                {
                    _wayFrom.AddRange(ends[0]);
                    for (int e = 1; e < ends.Count; e++) _wayTo.Add(ends[e]);
                    _way = WayHolds(null); // only where the room with no prop has the way
                }
            }
        }

        LevelPoint Centre(int i, int j) => new LevelPoint(_x0 + (i + 0.5) * _step, _y0 + (j + 0.5) * _step);

        LevelPoint CellMiddle(int k) => new LevelPoint((_room.Rect.X + k % _cw + 0.5) * _cs, (_room.Rect.Y + k / _cw + 0.5) * _cs);

        /// <summary>The room's four sides, less the doors' openings, as segments (as the lane has them).</summary>
        static List<(LevelPoint, LevelPoint)> Walls(CellRect r, double cs, List<LevelDoor> doors)
        {
            var result = new List<(LevelPoint, LevelPoint)>();
            void Side(bool vertical, int line, int from, int to)
            {
                var gaps = new List<(int lo, int hi)>();
                foreach (LevelDoor d in doors)
                    if (d.Vertical == vertical && d.Line == line) gaps.Add((d.From, d.From + d.Width));
                gaps.Sort((a, b) => a.lo.CompareTo(b.lo));
                int at = from;
                foreach ((int lo, int hi) g in gaps)
                {
                    if (g.lo > at) Add(vertical, line, at, Math.Min(g.lo, to));
                    at = Math.Max(at, g.hi);
                }
                if (at < to) Add(vertical, line, at, to);
            }
            void Add(bool vertical, int line, int a, int b) =>
                result.Add(vertical ? (new LevelPoint(line * cs, a * cs), new LevelPoint(line * cs, b * cs))
                                    : (new LevelPoint(a * cs, line * cs), new LevelPoint(b * cs, line * cs)));
            Side(true, r.X, r.Y, r.Top);
            Side(true, r.Right, r.Y, r.Top);
            Side(false, r.Y, r.X, r.Right);
            Side(false, r.Top, r.X, r.Right);
            return result;
        }

        void Cells(PropFootprint f, double grow, out int i0, out int j0, out int i1, out int j1)
        {
            f.Extent(out double minX, out double minY, out double maxX, out double maxY);
            i0 = Math.Max(0, (int)Math.Floor((minX - grow - _x0) / _step));
            j0 = Math.Max(0, (int)Math.Floor((minY - grow - _y0) / _step));
            i1 = Math.Min(_nx - 1, (int)Math.Ceiling((maxX + grow - _x0) / _step));
            j1 = Math.Min(_ny - 1, (int)Math.Ceiling((maxY + grow - _y0) / _step));
        }

        void Mark(PropFootprint f, bool[] into)
        {
            Cells(f, _radius, out int i0, out int j0, out int i1, out int j1);
            for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                    if (!into[j * _nx + i] && f.DistanceTo(Centre(i, j)) < _radius - Slack) into[j * _nx + i] = true;
        }

        /// <summary>Floods from the doorways over the squares neither blocked nor in <paramref name="also"/>, into <paramref name="reached"/>.</summary>
        void Flood(bool[] blocked, bool[] also, bool[] reached)
        {
            Array.Clear(reached, 0, reached.Length);
            int head = 0, tail = 0;
            bool Open(int k) => !blocked[k] && (also == null || !also[k]);
            foreach (int k in _starts)
                if (Open(k) && !reached[k])
                {
                    reached[k] = true;
                    _queue[tail++] = k;
                }
            while (head < tail)
            {
                int k = _queue[head++], i = k % _nx, j = k / _nx;
                if (i + 1 < _nx) Step(k + 1);
                if (i > 0) Step(k - 1);
                if (j + 1 < _ny) Step(k + _nx);
                if (j > 0) Step(k - _nx);
            }
            void Step(int m)
            {
                if (reached[m] || !Open(m)) return;
                reached[m] = true;
                _queue[tail++] = m;
            }
        }

        /// <summary>A prop stands: its footprint takes the squares within the radius, and its cells' middles are measured to it.</summary>
        public void Block(LevelProp prop)
        {
            if (prop == null || PropSize.Of(prop.Model).Walkable) return;
            Mark(prop.Footprint, _props);
            _standing.Add(prop);
        }

        /// <summary>
        /// Would the floor keep both rules with <paramref name="extra"/> standing too (see the class)? True when it does; false when
        /// it would shut off a piece of floor joined before, or close the way through.
        /// </summary>
        public bool Keeps(LevelProp extra)
        {
            if (extra == null || PropSize.Of(extra.Model).Walkable) return true;
            return PocketsWith(extra) == 0 && (!_way || WayHolds(extra));
        }

        /// <summary>The squares joined in the room with no prop that are free with the props (and <paramref name="extra"/>) and not joined: the pocket's size.</summary>
        int PocketsWith(LevelProp extra)
        {
            Array.Copy(_props, _work, _props.Length);
            if (extra != null) Mark(extra.Footprint, _work);
            for (int k = 0; k < _work.Length; k++) _work[k] |= _wall[k];
            Flood(_work, null, _reached);
            int cut = 0;
            for (int k = 0; k < _joined0.Length; k++)
                if (_joined0[k] && !_work[k] && !_reached[k]) cut++;
            return cut;
        }

        bool CellClear(int k, LevelProp extra)
        {
            if (!_cellOpen[k]) return false;
            LevelPoint m = CellMiddle(k);
            foreach (LevelProp p in _standing)
                if (p.Footprint.DistanceTo(m) < _clear - Slack) return false;
            return extra == null || PropSize.Of(extra.Model).Walkable || extra.Footprint.DistanceTo(m) >= _clear - Slack;
        }

        /// <summary>The way through holds with the props standing (and <paramref name="extra"/>); with none standing, the room's own way.</summary>
        bool WayHolds(LevelProp extra)
        {
            if (_wayFrom.Count == 0 || _wayTo.Count == 0) return true;
            Array.Clear(_cellReached, 0, _cellReached.Length);
            int head = 0, tail = 0;
            foreach (int k in _wayFrom)
                if (!_cellReached[k] && CellClear(k, extra))
                {
                    _cellReached[k] = true;
                    _cellQueue[tail++] = k;
                }
            while (head < tail)
            {
                int k = _cellQueue[head++], x = k % _cw, y = k / _cw;
                if (x + 1 < _cw) Step(k + 1);
                if (x > 0) Step(k - 1);
                if (y + 1 < _ch) Step(k + _cw);
                if (y > 0) Step(k - _cw);
            }
            void Step(int m)
            {
                if (_cellReached[m] || !CellClear(m, extra)) return;
                _cellReached[m] = true;
                _cellQueue[tail++] = m;
            }
            foreach (List<int> to in _wayTo)
            {
                bool any = false;
                foreach (int k in to) any |= _cellReached[k];
                if (!any) return false;
            }
            return true;
        }

        /// <summary>The rule the props standing break, in words, or null: the validator's (see the class).</summary>
        public string Fault()
        {
            int cut = PocketsWith(null);
            if (cut > 0)
                return string.Format(CultureInfo.InvariantCulture, "has {0:0.##} m² of floor its props shut off from its doors (a gap under {1:0.##} m)",
                                     cut * _step * _step, 2 * _radius);
            if (_way && !WayHolds(null))
                return string.Format(CultureInfo.InvariantCulture, "has no way of whole cells from door to door whose middles are {0:0.##} m from every prop", _clear);
            return null;
        }

        /// <summary>The rule a room's props break, in words, or null (the validator's; nothing asked of a passage).</summary>
        public static string FaultOf(LevelLayout level, LevelRoom room, LevelValues values, IEnumerable<LevelProp> props = null)
        {
            if (!Asks(room)) return null;
            var floor = new JoinedFloor(level, room, values);
            foreach (LevelProp p in props ?? room.Props) floor.Block(p);
            return floor.Fault();
        }

        /// <summary>The share of the room's floor that is free and joined with the props standing, of what is in the room with none (work order 088's measure of open floor).</summary>
        public double OpenShare()
        {
            int before = 0, after = 0;
            for (int k = 0; k < _joined0.Length; k++)
                if (_joined0[k]) before++;
            PocketsWith(null);
            for (int k = 0; k < _joined0.Length; k++)
                if (_joined0[k] && !_work[k] && _reached[k]) after++;
            return before > 0 ? after / (double)before : 1;
        }
    }
}
