using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The rules every prop of a level keeps (work order 009), shared by the generator, which keeps them, and the
    /// validator, which checks them. A prop's footprint is inside its room; it overlaps no other prop's; it is at least
    /// <see cref="PropValues.WallClearance"/> from the walls, except one that stands at a wall, which is at most
    /// <see cref="PropValues.AtWallMost"/> from one; at least <see cref="PropValues.FromDoor"/> from every door's opening
    /// of its room; at least <see cref="PropValues.FromPoints"/> from the player's start and every enemy point; an
    /// open-floor prop at least <see cref="PropValues.FloorSpacing"/> from every other prop; nothing within
    /// <see cref="PropValues.BossMiddleClear"/> of the boss room's middle; turned by a multiple of 90 degrees, or of
    /// 15 for the models that may (<see cref="PropSize.AnyAngle"/>). And each room keeps a clear lane
    /// (<see cref="Lane"/>).
    /// </summary>
    public static class PropRules
    {
        const double Slack = 1e-9;

        public static bool RotationAllowed(PropModel model, int rotation) =>
            rotation >= 0 && rotation < 360 && rotation % (PropSize.Of(model).AnyAngle ? 15 : 90) == 0;

        /// <summary>The models a room of this kind may hold.</summary>
        public static PropModel[] ModelsFor(RoomKind kind, PropValues v) =>
            kind == RoomKind.Entrance ? new[] { PropModel.Brazier }
            : kind == RoomKind.Treasure ? new[] { PropModel.Chest, PropModel.Brazier }
            : kind == RoomKind.Exit ? new[] { PropModel.Altar, PropModel.Statue, PropModel.Pillar }
            : v.FloorModels.Concat(v.WallModels).Concat(new[] { PropModel.Brazier }).Distinct().ToArray();

        /// <summary>The most props a room of this kind may hold.</summary>
        public static int MostIn(RoomKind kind, PropValues v) =>
            kind == RoomKind.Entrance ? 2 : kind == RoomKind.Treasure ? 3 : kind == RoomKind.Exit ? 7 : v.CombatMost;

        /// <summary>The way a prop faces when its own z points along (x, y) on the ground: 0 north, 90 east, 180 south, 270 west.</summary>
        public static int Facing(double x, double y)
        {
            int deg = (int)Math.Round(Math.Atan2(x, y) * 180.0 / Math.PI);
            return ((deg % 360) + 360) % 360;
        }

        /// <summary>
        /// The first rule the prop breaks, said in words, with <paramref name="others"/> in its room beside it; null if
        /// none. With <paramref name="all"/> it adds every rule it breaks there, not just the first.
        /// </summary>
        public static string Fault(LevelLayout level, LevelRoom room, LevelProp prop, IReadOnlyList<LevelProp> others, PropValues v,
                                   List<string> all = null)
        {
            double cs = level.CellSize;
            PropFootprint f = prop.Footprint;
            string first = null;
            string N(double x) => x.ToString(CultureInfo.InvariantCulture);
            // Notes a fault; true when that is enough (only the first is wanted).
            bool Broken(string fault)
            {
                first = first ?? fault;
                all?.Add(fault);
                return all == null;
            }

            if (!RotationAllowed(prop.Model, prop.Rotation)
                && Broken($"is turned {prop.Rotation} degrees, not a multiple of {(PropSize.Of(prop.Model).AnyAngle ? 15 : 90)}")) return first;
            if (prop.Place == PropPlace.Wall && !PropSize.Of(prop.Model).AtWall && Broken("stands at a wall, and may not")) return first;

            double wall = f.DistanceToWalls(room.Rect, cs);
            if (wall < -Slack && Broken("reaches out of its room")) return first;
            if (prop.Place == PropPlace.Wall)
            {
                if (wall > v.AtWallMost + Slack && Broken($"is meant to stand at a wall and is {wall:0.##} m from the nearest")) return first;
            }
            else if (wall < v.WallClearance - Slack && Broken("is nearer a wall than " + N(v.WallClearance) + " m")) return first;

            foreach (LevelDoor d in level.DoorsOf(room.Order))
            {
                d.Opening(cs, out LevelPoint a, out LevelPoint b);
                if (f.DistanceTo(a, b) < v.FromDoor - Slack && Broken($"is nearer door {d.RoomA}-{d.RoomB} than {N(v.FromDoor)} m")) return first;
            }
            if (room.PlayerStart.HasValue && f.DistanceTo(room.PlayerStart.Value) < v.FromPoints - Slack
                && Broken("is nearer the player's start than " + N(v.FromPoints) + " m")) return first;
            // Every enemy, not only its group's point (work order 012: a pack or a line stands round its point).
            for (int i = 0; i < room.EnemyPoints.Count; i++)
            {
                bool near = f.DistanceTo(room.EnemyPoints[i]) < v.FromPoints - Slack;
                foreach (LevelPoint m in room.PlacesAt(i)) near |= f.DistanceTo(m) < v.FromPoints - Slack;
                if (near && Broken($"is nearer enemy point {i} or one of its enemies than {N(v.FromPoints)} m")) return first;
            }
            if (room.Kind == RoomKind.Exit && f.DistanceTo(room.Middle(cs)) < v.BossMiddleClear - Slack
                && Broken("is nearer the exit room's middle than " + N(v.BossMiddleClear) + " m")) return first;

            foreach (LevelProp o in others)
            {
                if (ReferenceEquals(o, prop)) continue;
                double apart = f.DistanceTo(o.Footprint);
                if (apart <= Slack)
                {
                    if (Broken("overlaps the " + o.Model + " at " + o.Position)) return first;
                }
                else if ((prop.Place == PropPlace.Floor || o.Place == PropPlace.Floor) && apart < v.FloorSpacing - Slack
                         && Broken($"is {apart:0.##} m from the {o.Model} at {o.Position}, and the open floor keeps {N(v.FloorSpacing)} m")) return first;
            }
            return first;
        }

        /// <summary>
        /// A room's clear lane (work order 009), on a grid of <see cref="PropValues.LaneGrid"/>: a grid square is open
        /// when its middle is at least half the lane's width from every prop's footprint and from the room's walls (its
        /// four sides less the doors' openings). The lane is clear when the open squares join, side to side, the doorway
        /// of every door of the room to every other door's and, but in a reward room (where the chest stands), to the
        /// room's middle. No NavMesh: the same answer in the rules, the generator and the tests.
        /// </summary>
        public sealed class Lane
        {
            readonly double _x0, _y0, _step, _half;
            readonly int _nx, _ny;
            readonly bool[] _blocked;
            readonly List<List<int>> _targets = new List<List<int>>();
            readonly bool[] _reached;
            readonly int[] _queue;

            public Lane(LevelLayout level, LevelRoom room, PropValues v)
            {
                double cs = level.CellSize;
                _step = v.LaneGrid;
                _half = v.LaneWidth * 0.5;
                _x0 = room.Rect.X * cs;
                _y0 = room.Rect.Y * cs;
                _nx = Math.Max(1, (int)Math.Round(room.Rect.Width * cs / _step));
                _ny = Math.Max(1, (int)Math.Round(room.Rect.Height * cs / _step));
                _blocked = new bool[_nx * _ny];
                _reached = new bool[_nx * _ny];
                _queue = new int[_nx * _ny];

                List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();
                List<(LevelPoint a, LevelPoint b)> walls = Walls(room.Rect, cs, doors);
                for (int j = 0; j < _ny; j++)
                    for (int i = 0; i < _nx; i++)
                    {
                        LevelPoint c = Centre(i, j);
                        if (LevelGeometry.DistanceToWalls(c, room.Rect, cs) >= _half) continue;
                        foreach ((LevelPoint a, LevelPoint b) w in walls)
                            if (LevelGeometry.DistanceToSegment(c, w.a, w.b) < _half - Slack)
                            {
                                _blocked[j * _nx + i] = true;
                                break;
                            }
                    }

                // The doorways: the row of squares inside the room along each door's opening.
                foreach (LevelDoor d in doors)
                {
                    var cells = new List<int>();
                    double lo = d.From * cs, hi = (d.From + d.Width) * cs;
                    if (d.Vertical && (d.Line == room.Rect.X || d.Line == room.Rect.Right))
                    {
                        int i = d.Line == room.Rect.X ? 0 : _nx - 1;
                        for (int j = 0; j < _ny; j++)
                            if (Centre(i, j).Y > lo && Centre(i, j).Y < hi) cells.Add(j * _nx + i);
                    }
                    else if (!d.Vertical && (d.Line == room.Rect.Y || d.Line == room.Rect.Top))
                    {
                        int j = d.Line == room.Rect.Y ? 0 : _ny - 1;
                        for (int i = 0; i < _nx; i++)
                            if (Centre(i, j).X > lo && Centre(i, j).X < hi) cells.Add(j * _nx + i);
                    }
                    _targets.Add(cells);
                }
                if (room.Kind != RoomKind.Treasure)
                {
                    LevelPoint m = room.Middle(cs);
                    var cells = new List<int>();
                    for (int j = 0; j < _ny; j++)
                        for (int i = 0; i < _nx; i++)
                            if (Centre(i, j).DistanceTo(m) <= _step) cells.Add(j * _nx + i);
                    _targets.Add(cells);
                }
            }

            LevelPoint Centre(int i, int j) => new LevelPoint(_x0 + (i + 0.5) * _step, _y0 + (j + 0.5) * _step);

            /// <summary>The room's four sides, less the doors' openings, as segments.</summary>
            static List<(LevelPoint, LevelPoint)> Walls(CellRect r, double cs, List<LevelDoor> doors)
            {
                var result = new List<(LevelPoint, LevelPoint)>();
                void Side(bool vertical, int line, int from, int to)
                {
                    var gaps = doors.Where(d => d.Vertical == vertical && d.Line == line).Select(d => (lo: d.From, hi: d.From + d.Width))
                                    .OrderBy(g => g.lo).ToList();
                    int at = from;
                    foreach (var g in gaps)
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

            void Cells(PropFootprint f, out int i0, out int j0, out int i1, out int j1)
            {
                f.Extent(out double minX, out double minY, out double maxX, out double maxY);
                i0 = Math.Max(0, (int)Math.Floor((minX - _half - _x0) / _step));
                j0 = Math.Max(0, (int)Math.Floor((minY - _half - _y0) / _step));
                i1 = Math.Min(_nx - 1, (int)Math.Ceiling((maxX + _half - _x0) / _step));
                j1 = Math.Min(_ny - 1, (int)Math.Ceiling((maxY + _half - _y0) / _step));
            }

            /// <summary>Closes the squares a prop's footprint, grown by half the lane, covers.</summary>
            public void Block(PropFootprint f)
            {
                Cells(f, out int i0, out int j0, out int i1, out int j1);
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                        if (!_blocked[j * _nx + i] && f.DistanceTo(Centre(i, j)) < _half - Slack) _blocked[j * _nx + i] = true;
            }

            /// <summary>True when the lane joins every doorway (and the middle) to the first, with <paramref name="extra"/> standing there too.</summary>
            public bool Clear(PropFootprint? extra = null)
            {
                if (_targets.Count < 2) return true;
                int ei0 = 0, ej0 = 0, ei1 = -1, ej1 = -1;
                if (extra.HasValue) Cells(extra.Value, out ei0, out ej0, out ei1, out ej1);
                bool Open(int k)
                {
                    if (_blocked[k]) return false;
                    if (!extra.HasValue) return true;
                    int i = k % _nx, j = k / _nx;
                    return i < ei0 || i > ei1 || j < ej0 || j > ej1 || extra.Value.DistanceTo(Centre(i, j)) >= _half - Slack;
                }

                Array.Clear(_reached, 0, _reached.Length);
                int head = 0, tail = 0;
                foreach (int k in _targets[0])
                    if (Open(k) && !_reached[k])
                    {
                        _reached[k] = true;
                        _queue[tail++] = k;
                    }
                while (head < tail)
                {
                    int k = _queue[head++], i = k % _nx, j = k / _nx;
                    void Step(int ni, int nj)
                    {
                        if (ni < 0 || nj < 0 || ni >= _nx || nj >= _ny) return;
                        int n = nj * _nx + ni;
                        if (_reached[n] || !Open(n)) return;
                        _reached[n] = true;
                        _queue[tail++] = n;
                    }
                    Step(i + 1, j);
                    Step(i - 1, j);
                    Step(i, j + 1);
                    Step(i, j - 1);
                }
                for (int t = 1; t < _targets.Count; t++)
                    if (!_targets[t].Any(k => _reached[k])) return false;
                return true;
            }
        }
    }

    /// <summary>
    /// The props stage of the generator (work order 009), on its own stream ("props"), after the layout and the
    /// contents, so neither changes for an old seed. Every prop is put at the first of its places to try that keeps
    /// every rule of <see cref="PropRules"/> (the clear lane last); one that has no such place is left out.
    ///
    /// Start room: a brazier either side of the door she leaves by, as near the door as the rules allow; nothing else.
    /// Combat rooms: from <see cref="PropValues.CombatFewest"/> props in the smallest to <see cref="PropValues.CombatMost"/>
    /// in the largest, by floor area: braziers in <see cref="PropValues.CornerBraziers"/> corners drawn from the four;
    /// of the rest, <see cref="PropValues.WallShare"/> along the walls (grave markers, urns, bone piles, facing into the
    /// room) and the others in the open floor (pillars, broken pillars, sarcophagi, rubble, statues), each model and
    /// turn drawn, at places tried in a drawn order. Reward rooms: a chest in the middle facing the door, set
    /// <see cref="PropValues.ChestBack"/> past the middle seen from the door so the middle itself stays walkable, and a
    /// brazier either side of it. Boss room: an altar at the wall opposite the door facing into the room, a statue either side
    /// of it, and a pillar near each corner. A set piece goes at the nearest place to its wanted one, within
    /// <see cref="PropValues.SetSearch"/>, that keeps the rules.
    /// </summary>
    public static class PropPlacer
    {
        // Places around a wanted one, nearest first, every 0.25 m within the search.
        static double[] s_offsetsFor;
        static List<(double x, double y)> s_offsets;

        static List<(double x, double y)> Around(double radius)
        {
            if (s_offsets != null && s_offsetsFor != null && s_offsetsFor[0] == radius) return s_offsets;
            var list = new List<(double x, double y)>();
            int n = (int)Math.Floor(radius / 0.25);
            for (int i = -n; i <= n; i++)
                for (int j = -n; j <= n; j++)
                    if (i * i + j * j <= n * n) list.Add((i * 0.25, j * 0.25));
            s_offsets = list.OrderBy(o => o.x * o.x + o.y * o.y).ThenBy(o => o.y).ThenBy(o => o.x).ToList();
            s_offsetsFor = new[] { radius };
            return s_offsets;
        }

        public static void Place(LevelLayout level, LevelRandom rng, LevelValues values)
        {
            foreach (LevelRoom room in level.Rooms)
            {
                var placer = new Room(level, room, values.PropRules);
                switch (room.Kind)
                {
                    case RoomKind.Entrance: placer.Start(); break;
                    case RoomKind.Combat: placer.Combat(rng, values.Plan.CombatSize); break;
                    case RoomKind.Treasure: placer.Reward(); break;
                    case RoomKind.Exit: placer.Boss(rng); break;
                }
            }
        }

        sealed class Room
        {
            readonly LevelLayout _level;
            readonly LevelRoom _room;
            readonly PropValues _v;
            readonly PropRules.Lane _lane;
            readonly double _cs;

            public Room(LevelLayout level, LevelRoom room, PropValues v)
            {
                _level = level;
                _room = room;
                _v = v;
                _cs = level.CellSize;
                _lane = new PropRules.Lane(level, room, v);
            }

            /// <summary>Adds the prop if it keeps every rule; <paramref name="laneTries"/> counts places the lane alone refused.</summary>
            bool TryAdd(LevelProp p, ref int laneTries)
            {
                if (PropRules.Fault(_level, _room, p, _room.Props, _v) != null) return false;
                if (!_lane.Clear(p.Footprint))
                {
                    laneTries++;
                    return false;
                }
                _room.Props.Add(p);
                _lane.Block(p.Footprint);
                return true;
            }

            void Want(int count) => _room.PropsWanted += count;

            void Missed(int laneTries)
            {
                if (laneTries > 0) _room.PropsLeftOutByLane++;
            }

            /// <summary>A set piece at the nearest place to <paramref name="wanted"/> that keeps the rules.</summary>
            bool Near(PropModel model, PropPlace place, int rotation, LevelPoint wanted)
            {
                Want(1);
                int laneTries = 0;
                if (TryNear(model, place, rotation, wanted, ref laneTries)) return true;
                Missed(laneTries);
                return false;
            }

            bool TryNear(PropModel model, PropPlace place, int rotation, LevelPoint wanted, ref int laneTries)
            {
                int tries = 0;
                foreach ((double x, double y) in Around(_v.SetSearch))
                {
                    if (tries >= _v.LaneTriesPerProp) break;
                    if (TryAdd(LevelProp.Make(model, new LevelPoint(wanted.X + x, wanted.Y + y), rotation, place), ref tries))
                    {
                        laneTries += tries;
                        return true;
                    }
                }
                laneTries += tries;
                return false;
            }

            /// <summary>A prop at the first of <paramref name="places"/> that keeps the rules.</summary>
            void First(PropModel model, PropPlace place, IEnumerable<(LevelPoint at, int rotation)> places)
            {
                Want(1);
                int laneTries = 0;
                foreach ((LevelPoint at, int rotation) in places)
                {
                    if (laneTries >= _v.LaneTriesPerProp) break;
                    if (TryAdd(LevelProp.Make(model, at, rotation, place), ref laneTries)) return;
                }
                Missed(laneTries);
            }

            // The door's opening, the unit along it, and the unit into this room.
            void DoorFrame(LevelDoor d, out LevelPoint a, out LevelPoint b, out double tx, out double ty, out double nx, out double ny)
            {
                d.Opening(_cs, out a, out b);
                tx = d.Vertical ? 0 : 1;
                ty = d.Vertical ? 1 : 0;
                LevelPoint m = _room.Middle(_cs);
                nx = d.Vertical ? Math.Sign(m.X - a.X) : 0;
                ny = d.Vertical ? 0 : Math.Sign(m.Y - a.Y);
            }

            public void Start()
            {
                List<LevelRoom> path = LevelGeometry.MainPath(_level);
                LevelDoor exit = path.Count > 1 ? _level.Doors.FirstOrDefault(d => d.Joins(_room.Order) && d.Other(_room.Order) == path[1].Order) : null;
                exit = exit ?? _level.DoorsOf(_room.Order).FirstOrDefault();
                if (exit == null) return;
                DoorFrame(exit, out LevelPoint a, out LevelPoint b, out double tx, out double ty, out double nx, out double ny);
                double r = PropSize.Of(PropModel.Brazier).Radius;
                double along = _v.FromDoor + r + 0.25, into = _v.WallClearance + r + 0.25;
                Near(PropModel.Brazier, PropPlace.Set, 0, new LevelPoint(a.X - tx * along + nx * into, a.Y - ty * along + ny * into));
                Near(PropModel.Brazier, PropPlace.Set, 0, new LevelPoint(b.X + tx * along + nx * into, b.Y + ty * along + ny * into));
            }

            public void Reward()
            {
                LevelDoor door = _level.DoorsOf(_room.Order).FirstOrDefault();
                LevelPoint m = _room.Middle(_cs);
                int facing = 0;
                if (door != null)
                {
                    DoorFrame(door, out _, out _, out _, out _, out double nx, out double ny);
                    facing = PropRules.Facing(-nx, -ny);
                    m = new LevelPoint(m.X + nx * _v.ChestBack, m.Y + ny * _v.ChestBack);
                }
                if (!Near(PropModel.Chest, PropPlace.Set, facing, m)) return;
                LevelProp chest = _room.Props[_room.Props.Count - 1];
                double a = facing * Math.PI / 180.0, ux = Math.Cos(a), uy = -Math.Sin(a); // the chest's own x
                double off = PropSize.Of(PropModel.Chest).Width * 0.5 + _v.BesideGap + PropSize.Of(PropModel.Brazier).Radius;
                LevelPoint c = chest.Position;
                Near(PropModel.Brazier, PropPlace.Set, 0, new LevelPoint(c.X - ux * off, c.Y - uy * off));
                Near(PropModel.Brazier, PropPlace.Set, 0, new LevelPoint(c.X + ux * off, c.Y + uy * off));
            }

            public void Boss(LevelRandom rng)
            {
                LevelDoor door = _level.DoorsOf(_room.Order).FirstOrDefault();
                CellRect r = _room.Rect;
                LevelPoint m = _room.Middle(_cs);
                double nx = 0, ny = 1;
                if (door != null) DoorFrame(door, out _, out _, out _, out _, out nx, out ny);
                // The far wall, along the way in.
                double halfAlong = (nx != 0 ? r.Width : r.Height) * _cs * 0.5;
                int facing = PropRules.Facing(-nx, -ny);
                PropSize altar = PropSize.Of(PropModel.Altar), statue = PropSize.Of(PropModel.Statue);
                double back = halfAlong - _v.AtWallGap - altar.Depth * 0.5;
                var altarAt = new LevelPoint(m.X + nx * back, m.Y + ny * back);
                Near(PropModel.Altar, PropPlace.Wall, facing, altarAt);

                double tx = -ny, ty = nx; // along the far wall
                double side = altar.Width * 0.5 + _v.StatueGap + statue.Width * 0.5;
                double statueBack = halfAlong - _v.WallClearance - statue.Depth * 0.5 - 0.25;
                foreach (int s in new[] { -1, 1 })
                    Near(PropModel.Statue, PropPlace.Set, facing,
                         new LevelPoint(m.X + nx * statueBack + tx * side * s, m.Y + ny * statueBack + ty * side * s));

                double inset = _v.BossPillarInset;
                double x0 = r.X * _cs + inset, x1 = r.Right * _cs - inset, y0 = r.Y * _cs + inset, y1 = r.Top * _cs - inset;
                foreach (LevelPoint corner in new[] { new LevelPoint(x0, y0), new LevelPoint(x1, y0), new LevelPoint(x1, y1), new LevelPoint(x0, y1) })
                    Near(PropModel.Pillar, PropPlace.Set, 15 * rng.Below(24), corner);
            }

            public void Combat(LevelRandom rng, RoomSizeRange size)
            {
                CellRect r = _room.Rect;
                int cells = r.Width * r.Height, fewest = size.Min * size.Min, most = size.Max * size.Max;
                double t = most > fewest ? Math.Max(0, Math.Min(1, (cells - fewest) / (double)(most - fewest))) : 0;
                int count = _v.CombatFewest + (int)Math.Round(t * (_v.CombatMost - _v.CombatFewest), MidpointRounding.AwayFromZero);
                int braziers = Math.Min(_v.CornerBraziers, count);
                int rest = count - braziers;
                int along = (int)Math.Round(rest * _v.WallShare, MidpointRounding.AwayFromZero);
                int open = rest - along;

                // Braziers in drawn corners: the next corner when one has no place.
                double br = PropSize.Of(PropModel.Brazier).Radius, inset = _v.WallClearance + br + 0.1;
                double x0 = r.X * _cs, x1 = r.Right * _cs, y0 = r.Y * _cs, y1 = r.Top * _cs;
                var corners = new List<LevelPoint>
                {
                    new LevelPoint(x0 + inset, y0 + inset), new LevelPoint(x1 - inset, y0 + inset),
                    new LevelPoint(x1 - inset, y1 - inset), new LevelPoint(x0 + inset, y1 - inset),
                };
                rng.Shuffle(corners);
                Want(braziers);
                int placed = 0, cornerLaneTries = 0;
                foreach (LevelPoint c in corners)
                {
                    if (placed == braziers) break;
                    if (TryNear(PropModel.Brazier, PropPlace.Set, 0, c, ref cornerLaneTries)) placed++;
                }
                for (int k = placed; k < braziers; k++) Missed(cornerLaneTries);

                // The open floor: places on a grid, in a drawn order for each prop.
                var grid = new List<LevelPoint>();
                double step = _v.PlaceStep;
                for (double x = x0 + step; x < x1 - 1e-9; x += step)
                    for (double y = y0 + step; y < y1 - 1e-9; y += step)
                        grid.Add(new LevelPoint(x, y));
                for (int k = 0; k < open; k++)
                {
                    PropModel model = _v.FloorModels[rng.Below(_v.FloorModels.Length)];
                    int rotation = PropSize.Of(model).AnyAngle ? 15 * rng.Below(24) : 90 * rng.Below(4);
                    rng.Shuffle(grid);
                    First(model, PropPlace.Floor, grid.Select(p => (p, rotation)));
                }

                // Along the walls: backs to a wall, facing into the room.
                for (int k = 0; k < along; k++)
                {
                    PropModel model = _v.WallModels[rng.Below(_v.WallModels.Length)];
                    PropSize s = PropSize.Of(model);
                    double off = _v.AtWallGap + (s.Shape == PropShape.Circle ? s.Radius : s.Depth * 0.5);
                    var places = new List<(LevelPoint, int)>();
                    for (double x = x0 + step; x < x1 - 1e-9; x += step)
                    {
                        places.Add((new LevelPoint(x, y0 + off), PropRules.Facing(0, 1)));
                        places.Add((new LevelPoint(x, y1 - off), PropRules.Facing(0, -1)));
                    }
                    for (double y = y0 + step; y < y1 - 1e-9; y += step)
                    {
                        places.Add((new LevelPoint(x0 + off, y), PropRules.Facing(1, 0)));
                        places.Add((new LevelPoint(x1 - off, y), PropRules.Facing(-1, 0)));
                    }
                    rng.Shuffle(places);
                    First(model, PropPlace.Wall, places);
                }
            }
        }
    }
}
