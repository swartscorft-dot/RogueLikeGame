using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Makes a floor from a seed and the floor's number in its act: the same seed and number always give the same floor
    /// (for the same <see cref="Version"/> and the same values), and every seed gives one that
    /// <see cref="LevelValidator"/> finds no fault in.
    ///
    /// Layout (its own stream, "layout"). The entrance is put in the middle of the grid. Each room of the main path
    /// (the combat rooms in order, then the exit room) is put against a side of the room before it, sharing a stretch
    /// of wall at least a door and a wall cell either side of it long: no corridors. Sizes are drawn per room. A room
    /// may not overlap another or leave the grid, the path may not go the same way more than
    /// <see cref="LevelValues.MostInARow"/> times running, and the exit room may not touch the entrance (so it does not
    /// fold back on itself). The way on reads (decision 0008, 2026-11-19, work order 012): a room's way out is never
    /// on the wall of its way in, and the opposite wall is tried first, the two side walls after it in a drawn order.
    /// When a room fits nowhere, the room before it is taken back and put in its next place, and so on back along the
    /// path. Then the treasure rooms: combat rooms are taken in a drawn order and each gets one against a side wall
    /// (one that holds neither its door in nor its door out), until there are enough. Last, each door is put at a drawn
    /// place on the stretch its two rooms share, with wall on both sides of it.
    ///
    /// When an attempt cannot be finished (no place for a room even after taking rooms back, too many rooms put down
    /// and taken back, no place for the treasure rooms, or no place for a room's enemies), the next attempt starts again
    /// from a new stream made from the seed and the attempt's number. The result is still a function of the seed and
    /// the floor's number alone.
    ///
    /// Contents (their own stream, "contents"; work order 012): the player's start in the middle of the entrance. Each
    /// combat room draws how many enemies it holds from its floor's range (<see cref="LevelValues.EnemiesOf"/>), the
    /// exit room <see cref="LevelValues.ExitRoomShare"/> as many, and a mix from its floor's table; the enemies are
    /// shared out into groups (packs of swarmers, lines of shooters, brutes side by side, guards alone), and each group
    /// gets a point: shooters at the far side of the room from the door in, guards and brutes between, swarmers
    /// anywhere, every enemy of every group clear of the walls, the doors and the way in, and of the other groups. On the
    /// act's last floor the exit room holds the act boss, at its middle where it can stand, with a full room of others.
    /// Nothing in the treasure rooms.
    ///
    /// Version 2 (work order 008) gave each enemy point a kind of enemy. Version 3 (work order 009) added the props on
    /// their own stream ("props") after the contents, unless <see cref="LevelValues.Props"/> is off; the props stage
    /// never makes an attempt fail (a prop with no place is left out). Version 4 (work order 012): the chain of rooms,
    /// two treasure rooms, the floor's number, and the enemies in groups.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Raise this whenever a change makes old seeds give other levels, and update the golden maps in the tests.</summary>
        public const int Version = 5; // 5: work order 014, fuller rooms and the act boss its own kind

        public const string LayoutStage = "layout";
        public const string ContentsStage = "contents";
        public const string PropsStage = "props";

        // The sides of a room, as the ways a path can step.
        public const int North = 0, East = 1, South = 2, West = 3;

        public static int Opposite(int side) => (side + 2) % 4;

        /// <summary>The first floor of an act, for callers that do not say.</summary>
        public static LevelLayout Generate(ulong seed, LevelValues values = null, int contentsWarmUp = 0, int propsWarmUp = 0) =>
            Generate(seed, 1, values, contentsWarmUp, propsWarmUp);

        /// <summary>
        /// The floor of this seed and number (1 to <see cref="LevelValues.FloorsPerAct"/>). <paramref name="contentsWarmUp"/>
        /// throws away that many draws at the start of the contents stage, and <paramref name="propsWarmUp"/> of the props
        /// stage; they are there for the tests that show a change to one stage leaves the others alone.
        /// </summary>
        public static LevelLayout Generate(ulong seed, int floor, LevelValues values = null, int contentsWarmUp = 0, int propsWarmUp = 0)
        {
            values = values ?? new LevelValues();
            floor = Math.Max(1, Math.Min(values.FloorsPerAct, floor));
            List<string> faults = null;
            for (int attempt = 0; attempt < values.Attempts; attempt++)
            {
                var level = new LevelLayout
                {
                    Seed = seed, Version = Version, GridCells = values.GridCells, CellSize = values.CellSize, Attempt = attempt + 1,
                    Floor = floor,
                };
                if (!TryLayout(level, LevelRandom.Stream(seed, LayoutStage, attempt), values)) continue;

                LevelRandom contents = LevelRandom.Stream(seed, ContentsStage, attempt);
                for (int i = 0; i < contentsWarmUp; i++) contents.NextBits();
                if (!PlaceContents(level, contents, values)) continue;

                faults = LevelValidator.Validate(level, values);
                if (faults.Count > 0) continue;
                if (!values.Props) return level;

                // The props never send the generator to another attempt: that would change the rooms of an old seed.
                LevelRandom props = LevelRandom.Stream(seed, PropsStage, attempt);
                for (int i = 0; i < propsWarmUp; i++) props.NextBits();
                PropPlacer.Place(level, props, values);
                faults = LevelValidator.Validate(level, values);
                if (faults.Count == 0) return level;
                throw new InvalidOperationException("The props of seed " + LevelSeed.ToText(seed) + " break a rule: " + string.Join("; ", faults));
            }
            throw new InvalidOperationException("No valid level for seed " + LevelSeed.ToText(seed) + " floor " + floor + " in " + values.Attempts + " attempts"
                                                + (faults != null ? ": " + string.Join("; ", faults) : ""));
        }

        // ---------------- Layout ----------------

        struct Placement
        {
            public int Side;
            public CellRect Rect;
        }

        static bool TryLayout(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            LevelPlan plan = v.Plan;
            int grid = v.GridCells;
            int need = v.DoorWidth + 2;

            var kinds = new List<RoomKind> { RoomKind.Entrance };
            for (int i = 0; i < plan.CombatRooms; i++) kinds.Add(RoomKind.Combat);
            kinds.Add(RoomKind.Exit);

            var path = new List<CellRect>();
            var steps = new List<int>(); // steps[i]: the side of room i that room i + 1 stands against
            int placements = 0;

            DrawSize(rng, plan.EntranceSize, out int sw, out int sh);
            path.Add(new CellRect((grid - sw) / 2, (grid - sh) / 2, sw, sh));
            if (!Fits(path[0], grid, path, 0)) return false;

            bool TooStraight(int side)
            {
                if (steps.Count < v.MostInARow) return false;
                for (int k = steps.Count - v.MostInARow; k < steps.Count; k++)
                    if (steps[k] != side) return false;
                return true;
            }

            bool PlaceMain(int i)
            {
                if (i == kinds.Count) return true;
                DrawSize(rng, plan.SizeOf(kinds[i]), out int w, out int h);
                // The way on (work order 012): from the entrance any way; after it straight on first, then a turn to
                // either side in a drawn order, never back through the wall she came in by.
                List<int> sides;
                if (steps.Count == 0)
                {
                    sides = new List<int> { North, East, South, West };
                    rng.Shuffle(sides);
                }
                else
                {
                    int on = steps[steps.Count - 1];
                    var turns = new List<int> { (on + 1) % 4, (on + 3) % 4 };
                    rng.Shuffle(turns);
                    sides = new List<int> { on, turns[0], turns[1] };
                }
                foreach (Placement c in Candidates(rng, path[i - 1], w, h, need, sides))
                {
                    if (placements >= v.PlacementsPerAttempt) return false;
                    if (TooStraight(c.Side)) continue;
                    if (!Fits(c.Rect, grid, path, path.Count)) continue;
                    if (kinds[i] == RoomKind.Exit && c.Rect.Touches(path[0])) continue;
                    placements++;
                    path.Add(c.Rect);
                    steps.Add(c.Side);
                    if (PlaceMain(i + 1)) return true;
                    path.RemoveAt(path.Count - 1);
                    steps.RemoveAt(steps.Count - 1);
                }
                return false;
            }

            if (!PlaceMain(1)) return false;

            // The treasure rooms, each against a side wall of a different combat room (path rooms 1 to CombatRooms).
            var taken = new List<CellRect>(path);
            var treasureOf = new Dictionary<int, CellRect>();
            var combats = Enumerable.Range(1, plan.CombatRooms).ToList();
            rng.Shuffle(combats);
            foreach (int c in combats)
            {
                if (treasureOf.Count == plan.TreasureRooms) break;
                DrawSize(rng, plan.TreasureSize, out int w, out int h);
                int wayIn = Opposite(steps[c - 1]), wayOut = steps[c];
                var sides = new List<int> { North, East, South, West }.Where(s => s != wayIn && s != wayOut).ToList();
                rng.Shuffle(sides);
                foreach (Placement p in Candidates(rng, path[c], w, h, need, sides))
                {
                    if (!Fits(p.Rect, grid, taken, taken.Count)) continue;
                    treasureOf[c] = p.Rect;
                    taken.Add(p.Rect);
                    break;
                }
            }
            if (treasureOf.Count < plan.TreasureRooms) return false;

            // The rooms in the order of the run: each treasure room right after its combat room.
            var parentOrder = new List<int>(); // for each room, the order of the room it is entered from (-1 for the entrance)
            int previousOnPath = -1;
            for (int i = 0; i < path.Count; i++)
            {
                int order = level.Rooms.Count;
                level.Rooms.Add(new LevelRoom { Kind = kinds[i], Rect = path[i], Order = order });
                parentOrder.Add(previousOnPath);
                previousOnPath = order;
                if (treasureOf.TryGetValue(i, out CellRect treasure))
                {
                    level.Rooms.Add(new LevelRoom { Kind = RoomKind.Treasure, Rect = treasure, Order = level.Rooms.Count });
                    parentOrder.Add(order);
                }
            }

            // The doors, in the order of the run.
            for (int r = 1; r < level.Rooms.Count; r++)
            {
                LevelDoor door = DoorBetween(rng, level.Rooms[parentOrder[r]], level.Rooms[r], v.DoorWidth);
                if (door == null) return false;
                level.Doors.Add(door);
            }
            return true;
        }

        static void DrawSize(LevelRandom rng, RoomSizeRange range, out int w, out int h)
        {
            w = rng.Range(range.Min, range.Max);
            h = rng.Range(range.Min, range.Max);
        }

        /// <summary>
        /// Every place for a room of w by h against a side of <paramref name="a"/>, sharing at least
        /// <paramref name="need"/> cells of wall with it: the sides in the order given, and the places along each side in
        /// a drawn order.
        /// </summary>
        static IEnumerable<Placement> Candidates(LevelRandom rng, CellRect a, int w, int h, int need, List<int> sides)
        {
            var result = new List<Placement>();
            foreach (int side in sides)
            {
                var along = new List<Placement>();
                if (side == East || side == West)
                {
                    int x = side == East ? a.Right : a.X - w;
                    for (int y = a.Y - h + need; y <= a.Top - need; y++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                else
                {
                    int y = side == North ? a.Top : a.Y - h;
                    for (int x = a.X - w + need; x <= a.Right - need; x++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                rng.Shuffle(along);
                result.AddRange(along);
            }
            return result;
        }

        /// <summary>Inside the grid and over none of the first <paramref name="count"/> rooms.</summary>
        static bool Fits(CellRect r, int grid, List<CellRect> rooms, int count)
        {
            if (r.X < 0 || r.Y < 0 || r.Right > grid || r.Top > grid) return false;
            for (int i = 0; i < count; i++)
                if (r.Overlaps(rooms[i])) return false;
            return true;
        }

        /// <summary>A door at a drawn place on the stretch of wall the two rooms share, with a wall cell either side; null if they share too little.</summary>
        static LevelDoor DoorBetween(LevelRandom rng, LevelRoom first, LevelRoom second, int width)
        {
            if (!LevelGeometry.SharedWall(first.Rect, second.Rect, out bool vertical, out int line, out int lo, out int hi)) return null;
            if (hi - lo < width + 2) return null;
            return new LevelDoor
            {
                RoomA = Math.Min(first.Order, second.Order),
                RoomB = Math.Max(first.Order, second.Order),
                Vertical = vertical,
                Line = line,
                From = rng.Range(lo + 1, hi - 1 - width),
                Width = width,
            };
        }

        // ---------------- Contents ----------------

        static bool PlaceContents(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.Kind == RoomKind.Entrance)
                {
                    room.PlayerStart = room.Middle(v.CellSize);
                    continue;
                }
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                List<EnemyGroup> groups = DrawGroups(room.Kind, level.Floor, rng, v);
                if (!PlaceGroups(level, room, groups, rng, v)) return false;
            }
            return true;
        }

        /// <summary>
        /// What a room of this kind holds on this floor (work order 012), as groups: how many, drawn from the floor's
        /// range; a mix drawn from the floor's table; guards (each alone) as the mix says, twice as many in an exit room,
        /// no more than the floor allows; the rest shared out by the mix's shares, then into packs of swarmers of
        /// <see cref="LevelValues.PackFewest"/> to <see cref="LevelValues.PackMost"/>, lines of shooters and brutes side by
        /// side. On the act's last floor the exit room has the act boss besides.
        /// </summary>
        public static List<EnemyGroup> DrawGroups(RoomKind kind, int floor, LevelRandom rng, LevelValues v)
        {
            var groups = new List<EnemyGroup>();
            v.EnemyRange(kind, floor, out int fewest, out int most);
            if (most <= 0) return groups;
            int total = rng.Range(fewest, most);
            FloorEnemies f = v.EnemiesOf(floor);
            EnemyMix mix = f.Mixes != null && f.Mixes.Length > 0 ? f.Mixes[rng.Below(f.Mixes.Length)] : new EnemyMix("brutes", 0f, 0f, 1f, 0);

            int guardRoom = v.GuardsMost(kind, floor);
            if (kind == RoomKind.Exit && floor >= v.FloorsPerAct)
            {
                groups.Add(new EnemyGroup(EnemyKind.Boss, 1, boss: true)); // a guard until work order 014
                guardRoom--;
            }
            int guards = Math.Max(0, Math.Min(guardRoom, mix.Guards * (kind == RoomKind.Exit ? 2 : 1)));
            guards = Math.Min(guards, Math.Max(0, total - 2)); // room for two other kinds
            for (int i = 0; i < guards; i++) groups.Add(new EnemyGroup(EnemyKind.Guard));

            int rest = total - guards;
            int[] counts = Shares(rest, new[] { mix.Swarmers, mix.Shooters, mix.Brutes });
            int swarmers = counts[0], shooters = counts[1], brutes = counts[2];
            AddSplit(groups, EnemyKind.Shooter, shooters, Math.Max(1, v.ShooterLineMost));
            AddSplit(groups, EnemyKind.Brute, brutes, Math.Max(1, v.BruteLineMost));
            AddSplit(groups, EnemyKind.Swarmer, swarmers, Math.Max(1, v.PackMost));
            return groups;
        }

        /// <summary>
        /// <paramref name="total"/> shared by the shares: each kind with a share gets at least one (while there are
        /// enough), the floor of its share otherwise; what is left over or short goes to the first kind with a share
        /// (the swarmers), then the others.
        /// </summary>
        public static int[] Shares(int total, float[] shares)
        {
            var c = new int[shares.Length];
            if (total <= 0) return c;
            float sum = shares.Where(s => s > 0f).Sum();
            if (sum <= 0f)
            {
                c[c.Length - 1] = total;
                return c;
            }
            for (int k = 0; k < shares.Length; k++)
                if (shares[k] > 0f) c[k] = Math.Max(1, (int)Math.Floor(shares[k] / sum * total + 1e-6));
            int first = Array.FindIndex(shares, s => s > 0f);
            int diff = total - c.Sum();
            if (diff > 0) c[first] += diff;
            // Too many: taken back down to one each, the first kind first; with fewer than one each, the last kinds go without.
            for (int k = 0; diff < 0 && k < c.Length; k++)
            {
                int index = (first + k) % c.Length;
                int take = Math.Min(-diff, Math.Max(0, c[index] - 1));
                c[index] -= take;
                diff += take;
            }
            for (int index = c.Length - 1; diff < 0 && index >= 0; index--)
            {
                int take = Math.Min(-diff, c[index]);
                c[index] -= take;
                diff += take;
            }
            return c;
        }

        /// <summary>Splits <paramref name="count"/> of a kind into as few groups as hold at most <paramref name="most"/> each, as even as they can be.</summary>
        static void AddSplit(List<EnemyGroup> groups, EnemyKind kind, int count, int most)
        {
            if (count <= 0) return;
            int n = (count + most - 1) / most;
            for (int i = 0; i < n; i++)
            {
                int size = count / n + (i < count % n ? 1 : 0);
                groups.Add(new EnemyGroup(kind, size));
            }
        }

        /// <summary>
        /// A point for each group (work order 012), from the places on a grid in the room (<see cref="LevelValues.PointStep"/>
        /// apart) in a drawn order: the act boss at the middle if it can stand there, then the shooters' lines at the far
        /// side from the door in, then guards and brutes between, then the swarmers anywhere; a group takes the first place
        /// where every one of its enemies keeps the rules, its band first and anywhere after. False when a group has none.
        /// </summary>
        static bool PlaceGroups(LevelLayout level, LevelRoom room, List<EnemyGroup> groups, LevelRandom rng, LevelValues v)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();

            var places = new List<LevelPoint>();
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1;
            int ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    places.Add(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step));
            rng.Shuffle(places);
            double Far(LevelPoint p) => entry != null ? LevelGeometry.DistanceToDoor(p, entry, cs) : 0d;
            double farthest = places.Count > 0 ? places.Max(Far) : 0d;

            bool Fits(EnemyGroup g, LevelPoint p, out LevelPoint[] members)
            {
                members = LevelGeometry.MemberPlaces(level, room, p, g, v);
                foreach (LevelPoint m in members)
                    if (LevelGeometry.EnemyPlaceFault(level, room, m, entry, doors, v) != null) return false;
                for (int k = 0; k < room.EnemyPoints.Count; k++)
                {
                    if (room.EnemyPoints[k].DistanceTo(p) < v.EnemySpacing - 1e-9) return false;
                    foreach (LevelPoint other in room.EnemyPlaces[k])
                        foreach (LevelPoint m in members)
                            if (other.DistanceTo(m) < v.MemberSpacing - 1e-9) return false;
                }
                return true;
            }

            bool Put(EnemyGroup g, LevelPoint p, LevelPoint[] members)
            {
                room.EnemyPoints.Add(p);
                room.EnemyGroups.Add(g);
                room.EnemyPlaces.Add(members);
                return true;
            }

            bool Place(EnemyGroup g, double lo, double hi)
            {
                foreach (bool banded in new[] { true, false })
                    foreach (LevelPoint p in places)
                    {
                        if (banded && entry != null)
                        {
                            double d = Far(p) / Math.Max(1e-9, farthest);
                            if (d < lo - 1e-9 || d > hi + 1e-9) continue;
                        }
                        if (Fits(g, p, out LevelPoint[] members)) return Put(g, p, members);
                    }
                return false;
            }

            int Rank(EnemyGroup g) => g.Boss ? 0 : g.Kind == EnemyKind.Shooter ? 1 : g.Kind == EnemyKind.Guard ? 2 : g.Kind == EnemyKind.Brute ? 3 : 4;
            foreach (EnemyGroup g in groups.OrderBy(Rank).ToList())
            {
                if (g.Boss && Fits(g, room.Middle(cs), out LevelPoint[] at))
                {
                    Put(g, room.Middle(cs), at);
                    continue;
                }
                bool done = g.Kind == EnemyKind.Shooter ? Place(g, v.FarBand, 1d)
                          : g.Kind == EnemyKind.Swarmer ? Place(g, 0d, 1d)
                          : Place(g, v.MiddleBandFrom, v.MiddleBandTo);
                if (!done) return false;
            }
            return true;
        }
    }

    /// <summary>Measures shared by the generator and the validator.</summary>
    public static class LevelGeometry
    {
        /// <summary>
        /// Whether two rooms meet along a side, and where: the wall line, whether it runs north and south (vertical),
        /// and the stretch of it they share, from cell <paramref name="lo"/> up to, not including, <paramref name="hi"/>.
        /// False when they do not meet along a side (apart, overlapping, or only at a corner).
        /// </summary>
        public static bool SharedWall(CellRect a, CellRect b, out bool vertical, out int line, out int lo, out int hi)
        {
            vertical = false;
            line = lo = hi = 0;
            if (a.Overlaps(b)) return false;
            if (a.Right == b.X || b.Right == a.X)
            {
                vertical = true;
                line = a.Right == b.X ? a.Right : a.X;
                lo = Math.Max(a.Y, b.Y);
                hi = Math.Min(a.Top, b.Top);
            }
            else if (a.Top == b.Y || b.Top == a.Y)
            {
                line = a.Top == b.Y ? a.Top : a.Y;
                lo = Math.Max(a.X, b.X);
                hi = Math.Min(a.Right, b.Right);
            }
            return hi > lo;
        }

        /// <summary>
        /// The wall of the room a door is on (work order 012): <see cref="LevelGenerator.North"/>, East, South or West;
        /// -1 when the door is on none of its walls.
        /// </summary>
        public static int WallOf(CellRect room, LevelDoor door)
        {
            if (door.Vertical) return door.Line == room.X ? LevelGenerator.West : door.Line == room.Right ? LevelGenerator.East : -1;
            return door.Line == room.Y ? LevelGenerator.South : door.Line == room.Top ? LevelGenerator.North : -1;
        }

        /// <summary>The rooms of the main path (all but the treasure rooms), in the order of the run.</summary>
        public static List<LevelRoom> MainPath(LevelLayout level) =>
            level.Rooms.Where(r => r.Kind != RoomKind.Treasure).OrderBy(r => r.Order).ToList();

        /// <summary>
        /// The door she comes into a room by: for a room of the main path, the door to the room before it on the path;
        /// for a treasure room, its one door. Null if there is none.
        /// </summary>
        public static LevelDoor EntryDoor(LevelLayout level, LevelRoom room)
        {
            if (room.Kind == RoomKind.Treasure) return level.DoorsOf(room.Order).FirstOrDefault();
            List<LevelRoom> path = MainPath(level);
            int at = path.IndexOf(room);
            if (at <= 0) return null;
            int before = path[at - 1].Order;
            return level.Doors.FirstOrDefault(d => d.Joins(room.Order) && d.Other(room.Order) == before);
        }

        /// <summary>The door she leaves a room of the main path by, to the next room on the path; null for the exit room.</summary>
        public static LevelDoor ExitDoor(LevelLayout level, LevelRoom room)
        {
            List<LevelRoom> path = MainPath(level);
            int at = path.IndexOf(room);
            if (at < 0 || at + 1 >= path.Count) return null;
            int next = path[at + 1].Order;
            return level.Doors.FirstOrDefault(d => d.Joins(room.Order) && d.Other(room.Order) == next);
        }

        /// <summary>
        /// Where the enemies of a group at point <paramref name="p"/> stand (work order 012): one on the point; a pack of
        /// swarmers evenly round a circle of <see cref="LevelValues.PackRadius"/>, the first to the north; a line side by
        /// side, <see cref="LevelValues.LineSpacing"/> apart, along the wall of the door she comes in by (so it faces her
        /// from the far side), centred on the point.
        /// </summary>
        public static LevelPoint[] MemberPlaces(LevelLayout level, LevelRoom room, LevelPoint p, EnemyGroup g, LevelValues v)
        {
            int n = Math.Max(1, g.Count);
            var result = new LevelPoint[n];
            if (n == 1)
            {
                result[0] = p;
                return result;
            }
            if (g.IsPack)
            {
                for (int i = 0; i < n; i++)
                {
                    double a = i * Math.PI * 2.0 / n;
                    result[i] = new LevelPoint(p.X + Math.Sin(a) * v.PackRadius, p.Y + Math.Cos(a) * v.PackRadius);
                }
                return result;
            }
            LevelDoor entry = EntryDoor(level, room);
            bool alongY = entry != null && entry.Vertical;
            for (int i = 0; i < n; i++)
            {
                double off = (i - (n - 1) * 0.5) * v.LineSpacing;
                result[i] = alongY ? new LevelPoint(p.X, p.Y + off) : new LevelPoint(p.X + off, p.Y);
            }
            return result;
        }

        /// <summary>
        /// The first rule a place for one enemy breaks, in words, or null: inside its room, at least
        /// <see cref="LevelValues.WallClearance"/> from its walls and from every door of the room, and at least
        /// <see cref="LevelValues.EnemyFromEntry"/> from the door she comes in by.
        /// </summary>
        public static string EnemyPlaceFault(LevelLayout level, LevelRoom room, LevelPoint p, LevelDoor entry, List<LevelDoor> doors, LevelValues v)
        {
            double cs = level.CellSize;
            const double slack = 1e-9;
            if (!(p.X >= room.Rect.X * cs && p.X <= room.Rect.Right * cs && p.Y >= room.Rect.Y * cs && p.Y <= room.Rect.Top * cs)) return "is outside the room";
            if (DistanceToWalls(p, room.Rect, cs) < v.WallClearance - slack) return "is nearer a wall than " + v.WallClearance + " m";
            foreach (LevelDoor d in doors)
                if (DistanceToDoor(p, d, cs) < v.WallClearance - slack) return "is nearer a door than " + v.WallClearance + " m";
            if (entry != null && DistanceToDoor(p, entry, cs) < v.EnemyFromEntry - slack) return "is nearer the door she enters by than " + v.EnemyFromEntry + " m";
            return null;
        }

        /// <summary>Metres from a point to the nearest point of a door's opening.</summary>
        public static double DistanceToDoor(LevelPoint p, LevelDoor door, double cellSize)
        {
            door.Opening(cellSize, out LevelPoint a, out LevelPoint b);
            return DistanceToSegment(p, a, b);
        }

        public static double DistanceToSegment(LevelPoint p, LevelPoint a, LevelPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            double t = len2 > 0 ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2 : 0;
            t = Math.Max(0, Math.Min(1, t));
            return p.DistanceTo(new LevelPoint(a.X + t * dx, a.Y + t * dy));
        }

        /// <summary>Metres from a point inside a room to the nearest of its four walls.</summary>
        public static double DistanceToWalls(LevelPoint p, CellRect r, double cellSize) =>
            Math.Min(Math.Min(p.X - r.X * cellSize, r.Right * cellSize - p.X), Math.Min(p.Y - r.Y * cellSize, r.Top * cellSize - p.Y));
    }
}
