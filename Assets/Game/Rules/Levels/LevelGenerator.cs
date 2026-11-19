using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Makes a level from a seed: the same seed always gives the same level (for the same <see cref="Version"/> and
    /// the same values), and every seed gives one that <see cref="LevelValidator"/> finds no fault in.
    ///
    /// Layout (its own stream, "layout"). The start room is put in the middle of the grid. Each room of the main path
    /// (the combat rooms in order, then the boss room) is put against a side of the room before it, sharing a stretch
    /// of wall at least a door and a wall cell either side of it long: no corridors. Sizes are drawn per room. A room
    /// may not overlap another or leave the grid, the path may not go the same way more than
    /// <see cref="LevelValues.MostInARow"/> times running (so it wanders), and the boss room may not touch the start
    /// room (so it does not fold back on itself). When a room fits nowhere, the room before it is taken back and put
    /// in its next place, and so on back along the path. Then the reward rooms: combat rooms are taken in a drawn order
    /// and each gets one against a free side, until there are enough. Last, each door is put at a drawn place on the
    /// stretch its two rooms share, with wall on both sides of it.
    ///
    /// When an attempt cannot be finished (no place for a room even after taking rooms back, too many rooms put down
    /// and taken back, or no place for the reward rooms), the next attempt starts again from a new stream made from
    /// the seed and the attempt's number. The result is still a function of the seed alone.
    ///
    /// Contents (their own stream, "contents"): the player's start in the middle of the start room; in each combat
    /// room an enemy point per group of the plan's entry for it (<see cref="LevelPlan.CombatEnemies"/>), away from the
    /// walls, from the door she enters by and from each other, and then the room's groups dealt out to its points in
    /// a drawn order; one enemy point in the middle of the boss room with the plan's boss group; nothing in the
    /// reward rooms.
    ///
    /// Version 2 (work order 008) gave each enemy point a kind of enemy. Only the contents changed: the layout stream
    /// is as it was, so old seeds keep their rooms and doors.
    ///
    /// Props (their own stream, "props"; work order 009, version 3): after the contents, the props of every room
    /// (<see cref="PropPlacer"/>), unless <see cref="LevelValues.Props"/> is off. The layout and contents streams are
    /// as they were, and the props stage never makes an attempt fail (a prop with no place is left out), so old seeds
    /// keep their rooms, doors and enemy points.
    /// </summary>
    public static class LevelGenerator
    {
        /// <summary>Raise this whenever a change makes old seeds give other levels, and update the golden maps in the tests.</summary>
        public const int Version = 3;

        public const string LayoutStage = "layout";
        public const string ContentsStage = "contents";
        public const string PropsStage = "props";

        // The sides of a room, as the ways a path can step.
        const int North = 0, East = 1, South = 2, West = 3;

        /// <summary>
        /// The level of this seed. <paramref name="contentsWarmUp"/> throws away that many draws at the start of the
        /// contents stage, and <paramref name="propsWarmUp"/> of the props stage; they are there for the tests that show
        /// a change to one stage leaves the others alone.
        /// </summary>
        public static LevelLayout Generate(ulong seed, LevelValues values = null, int contentsWarmUp = 0, int propsWarmUp = 0)
        {
            values = values ?? new LevelValues();
            List<string> faults = null;
            for (int attempt = 0; attempt < values.Attempts; attempt++)
            {
                var level = new LevelLayout
                {
                    Seed = seed, Version = Version, GridCells = values.GridCells, CellSize = values.CellSize, Attempt = attempt + 1,
                };
                if (!TryLayout(level, LevelRandom.Stream(seed, LayoutStage, attempt), values)) continue;

                LevelRandom contents = LevelRandom.Stream(seed, ContentsStage, attempt);
                for (int i = 0; i < contentsWarmUp; i++) contents.NextBits();
                PlaceContents(level, contents, values);

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
            throw new InvalidOperationException("No valid level for seed " + LevelSeed.ToText(seed) + " in " + values.Attempts + " attempts"
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

            var kinds = new List<RoomKind> { RoomKind.Start };
            for (int i = 0; i < plan.CombatRooms; i++) kinds.Add(RoomKind.Combat);
            kinds.Add(RoomKind.Boss);

            var path = new List<CellRect>();
            var steps = new List<int>(); // steps[i]: the side of room i that room i + 1 stands against
            int placements = 0;

            DrawSize(rng, plan.StartSize, out int sw, out int sh);
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
                foreach (Placement c in Candidates(rng, path[i - 1], w, h, need))
                {
                    if (placements >= v.PlacementsPerAttempt) return false;
                    if (TooStraight(c.Side)) continue;
                    if (!Fits(c.Rect, grid, path, path.Count)) continue;
                    if (kinds[i] == RoomKind.Boss && c.Rect.Touches(path[0])) continue;
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

            // The reward rooms, each against a different combat room (path rooms 1 to CombatRooms).
            var taken = new List<CellRect>(path);
            var rewardOf = new Dictionary<int, CellRect>();
            var combats = Enumerable.Range(1, plan.CombatRooms).ToList();
            rng.Shuffle(combats);
            foreach (int c in combats)
            {
                if (rewardOf.Count == plan.RewardRooms) break;
                DrawSize(rng, plan.RewardSize, out int w, out int h);
                foreach (Placement p in Candidates(rng, path[c], w, h, need))
                {
                    if (!Fits(p.Rect, grid, taken, taken.Count)) continue;
                    rewardOf[c] = p.Rect;
                    taken.Add(p.Rect);
                    break;
                }
            }
            if (rewardOf.Count < plan.RewardRooms) return false;

            // The rooms in the order of the run: each reward room right after its combat room.
            var parentOrder = new List<int>(); // for each room, the order of the room it is entered from (-1 for the start)
            int previousOnPath = -1;
            for (int i = 0; i < path.Count; i++)
            {
                int order = level.Rooms.Count;
                level.Rooms.Add(new LevelRoom { Kind = kinds[i], Rect = path[i], Order = order });
                parentOrder.Add(previousOnPath);
                previousOnPath = order;
                if (rewardOf.TryGetValue(i, out CellRect reward))
                {
                    level.Rooms.Add(new LevelRoom { Kind = RoomKind.Reward, Rect = reward, Order = level.Rooms.Count });
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
        /// <paramref name="need"/> cells of wall with it: the sides in a drawn order, and the places along each side in a
        /// drawn order.
        /// </summary>
        static IEnumerable<Placement> Candidates(LevelRandom rng, CellRect a, int w, int h, int need)
        {
            var sides = new List<int> { North, East, South, West };
            rng.Shuffle(sides);
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

        static void PlaceContents(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            double cs = v.CellSize;
            int combatNumber = 0;
            foreach (LevelRoom room in level.Rooms)
            {
                switch (room.Kind)
                {
                    case RoomKind.Start:
                        room.PlayerStart = room.Middle(cs);
                        break;
                    case RoomKind.Boss:
                        // One point, in the middle; with more than one group in the plan the rest have no place, and the validator says so.
                        room.EnemyPoints.Add(room.Middle(cs));
                        EnemyGroup[] boss = v.Plan.GroupsFor(RoomKind.Boss, 0);
                        if (boss.Length > 0) room.EnemyGroups.Add(boss[0].Copy());
                        break;
                    case RoomKind.Combat:
                        EnemyGroup[] groups = v.Plan.CombatGroups(combatNumber++);
                        PlaceEnemies(level, room, groups.Length, rng, v);
                        // Which point gets which group is drawn (work order 008).
                        var dealt = groups.Select(g => g.Copy()).ToList();
                        rng.Shuffle(dealt);
                        room.EnemyGroups.AddRange(dealt.Take(room.EnemyPoints.Count));
                        break;
                }
            }
        }

        static void PlaceEnemies(LevelLayout level, LevelRoom room, int count, LevelRandom rng, LevelValues v)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();

            var places = new List<LevelPoint>();
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1;
            int ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
            {
                for (int j = 0; j < ny; j++)
                {
                    var p = new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step);
                    if (entry != null && LevelGeometry.DistanceToDoor(p, entry, cs) < v.EnemyFromEntry) continue;
                    if (doors.Any(d => LevelGeometry.DistanceToDoor(p, d, cs) < clear)) continue;
                    places.Add(p);
                }
            }
            rng.Shuffle(places);
            foreach (LevelPoint p in places)
            {
                if (room.EnemyPoints.Count == count) break;
                if (room.EnemyPoints.All(q => q.DistanceTo(p) >= v.EnemySpacing)) room.EnemyPoints.Add(p);
            }
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

        /// <summary>The rooms of the main path (all but the reward rooms), in the order of the run.</summary>
        public static List<LevelRoom> MainPath(LevelLayout level) =>
            level.Rooms.Where(r => r.Kind != RoomKind.Reward).OrderBy(r => r.Order).ToList();

        /// <summary>
        /// The door she comes into a room by: for a room of the main path, the door to the room before it on the path;
        /// for a reward room, its one door. Null if there is none.
        /// </summary>
        public static LevelDoor EntryDoor(LevelLayout level, LevelRoom room)
        {
            if (room.Kind == RoomKind.Reward) return level.DoorsOf(room.Order).FirstOrDefault();
            List<LevelRoom> path = MainPath(level);
            int at = path.IndexOf(room);
            if (at <= 0) return null;
            int before = path[at - 1].Order;
            return level.Doors.FirstOrDefault(d => d.Joins(room.Order) && d.Other(room.Order) == before);
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
