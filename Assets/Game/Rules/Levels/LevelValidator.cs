using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Checks a level description against the rules of a level and says every fault it finds; an empty list means
    /// valid. It does not trust the generator: it reads only the description, so it can be run on any level.
    ///
    /// It checks: the rooms are listed in the order of the run; every room is inside the grid, of a legal size for its
    /// kind and over no other; the plan's number of rooms of each kind; every door joins two rooms that share that
    /// stretch of wall, is as wide as a door, and has wall on both sides of it, and no two doors join the same rooms;
    /// every room can be reached from the start room through doors; the main path runs start, the combat rooms, boss,
    /// each joined to the next by a door, and the boss room is last on it, has exactly one door and does not touch the
    /// start room; every reward room has exactly one door, to a combat room, and no combat room has two; no other
    /// door; the player's start is in the start room only; the number of enemy points of each room; and every start
    /// and enemy point is inside its room and far enough from its walls and doors, a combat room's enemies far enough
    /// from the door she enters by and from each other.
    /// </summary>
    public static class LevelValidator
    {
        const double Slack = 1e-9;

        public static List<string> Validate(LevelLayout level, LevelValues values = null)
        {
            values = values ?? new LevelValues();
            var faults = new List<string>();
            if (level == null)
            {
                faults.Add("no level");
                return faults;
            }
            LevelPlan plan = values.Plan;
            List<LevelRoom> rooms = level.Rooms;
            double cs = level.CellSize;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);

            if (cs <= 0) faults.Add("cell size " + cs + " is not positive");

            // The order of the run.
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Order != i) faults.Add($"room {i} in the list has order {rooms[i].Order}");

            // Each room: bounds, size, overlaps.
            foreach (LevelRoom r in rooms)
            {
                CellRect c = r.Rect;
                if (c.X < 0 || c.Y < 0 || c.Right > level.GridCells || c.Top > level.GridCells)
                    faults.Add($"{Name(r)} {c} is outside the grid of {level.GridCells}");
                RoomSizeRange size = plan.SizeOf(r.Kind);
                if (!size.Allows(c.Width) || !size.Allows(c.Height))
                    faults.Add($"{Name(r)} is {c.Width}x{c.Height}, not {size.Min} to {size.Max} on a side for a {r.Kind} room");
            }
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                    if (rooms[i].Rect.Overlaps(rooms[j].Rect)) faults.Add($"{Name(rooms[i])} overlaps {Name(rooms[j])}");

            // The plan.
            void Count(RoomKind kind, int wanted)
            {
                int n = rooms.Count(r => r.Kind == kind);
                if (n != wanted) faults.Add($"{n} {kind} rooms, the plan has {wanted}");
            }
            Count(RoomKind.Start, 1);
            Count(RoomKind.Combat, plan.CombatRooms);
            Count(RoomKind.Reward, plan.RewardRooms);
            Count(RoomKind.Boss, 1);

            // The doors.
            bool Known(int order) => order >= 0 && order < rooms.Count;
            var pairs = new HashSet<(int, int)>();
            foreach (LevelDoor d in level.Doors)
            {
                string label = $"door {d.RoomA}-{d.RoomB}";
                if (!Known(d.RoomA) || !Known(d.RoomB) || d.RoomA == d.RoomB)
                {
                    faults.Add(label + " does not join two rooms of the level");
                    continue;
                }
                if (!pairs.Add((Math.Min(d.RoomA, d.RoomB), Math.Max(d.RoomA, d.RoomB))))
                    faults.Add(label + " joins two rooms another door already joins");
                if (d.Width != values.DoorWidth) faults.Add($"{label} is {d.Width} cells wide, not {values.DoorWidth}");
                CellRect a = rooms[d.RoomA].Rect, b = rooms[d.RoomB].Rect;
                if (!LevelGeometry.SharedWall(a, b, out bool vertical, out int line, out int lo, out int hi)
                    || vertical != d.Vertical || line != d.Line)
                {
                    faults.Add($"{label}: the two rooms do not share that wall ({(d.Vertical ? "vertical" : "horizontal")} line {d.Line})");
                    continue;
                }
                if (d.Width < 1 || d.From < lo || d.From + d.Width > hi)
                    faults.Add($"{label} at {d.From}+{d.Width} is not on the stretch the rooms share ({lo} to {hi})");
                else if (d.From - 1 < lo || d.From + d.Width + 1 > hi)
                    faults.Add($"{label} at {d.From}+{d.Width} has no wall on both sides (shared stretch {lo} to {hi})");
            }

            // Reaching every room from the start.
            LevelRoom start = rooms.FirstOrDefault(r => r.Kind == RoomKind.Start);
            if (start != null)
            {
                var reached = new HashSet<int> { start.Order };
                var queue = new Queue<int>();
                queue.Enqueue(start.Order);
                while (queue.Count > 0)
                {
                    int at = queue.Dequeue();
                    foreach (LevelDoor d in level.Doors)
                    {
                        if (!d.Joins(at) || !Known(d.Other(at))) continue;
                        if (reached.Add(d.Other(at))) queue.Enqueue(d.Other(at));
                    }
                }
                foreach (LevelRoom r in rooms)
                    if (!reached.Contains(r.Order)) faults.Add($"{Name(r)} cannot be reached from the start room");
            }

            // The main path.
            List<LevelRoom> path = LevelGeometry.MainPath(level);
            var wantedPath = new List<RoomKind> { RoomKind.Start };
            wantedPath.AddRange(Enumerable.Repeat(RoomKind.Combat, plan.CombatRooms));
            wantedPath.Add(RoomKind.Boss);
            if (!path.Select(r => r.Kind).SequenceEqual(wantedPath))
                faults.Add("the main path runs " + string.Join(" ", path.Select(Name)) + ", not start, " + plan.CombatRooms + " combat rooms, boss");
            bool Joined(int x, int y) => level.Doors.Any(d => d.Joins(x) && d.Other(x) == y);
            for (int i = 1; i < path.Count; i++)
                if (!Joined(path[i - 1].Order, path[i].Order)) faults.Add($"no door from {Name(path[i - 1])} to {Name(path[i])} on the main path");
            LevelRoom boss = rooms.FirstOrDefault(r => r.Kind == RoomKind.Boss);
            if (boss != null)
            {
                if (path.Count > 0 && path[path.Count - 1] != boss) faults.Add("the boss room is not the last room of the main path");
                int doors = level.DoorsOf(boss.Order).Count();
                if (doors != 1) faults.Add($"the boss room has {doors} doors, not 1");
                if (start != null && boss.Rect.Touches(start.Rect)) faults.Add("the boss room touches the start room");
            }

            // The reward rooms.
            var rewardsOf = new Dictionary<int, int>();
            foreach (LevelRoom r in rooms.Where(x => x.Kind == RoomKind.Reward))
            {
                List<LevelDoor> doors = level.DoorsOf(r.Order).ToList();
                if (doors.Count != 1)
                {
                    faults.Add($"{Name(r)} has {doors.Count} doors, not 1");
                    continue;
                }
                int other = doors[0].Other(r.Order);
                if (!Known(other) || rooms[other].Kind != RoomKind.Combat)
                {
                    faults.Add($"{Name(r)}'s door does not lead to a combat room");
                    continue;
                }
                rewardsOf.TryGetValue(other, out int n);
                rewardsOf[other] = n + 1;
                if (n + 1 == 2) faults.Add($"{Name(rooms[other])} has two reward rooms");
            }

            // No other door: each joins two neighbours on the main path, or a reward room to its combat room.
            foreach (LevelDoor d in level.Doors)
            {
                if (!Known(d.RoomA) || !Known(d.RoomB)) continue;
                LevelRoom a = rooms[d.RoomA], b = rooms[d.RoomB];
                if (a.Kind == RoomKind.Reward || b.Kind == RoomKind.Reward) continue; // checked above
                int ia = path.IndexOf(a), ib = path.IndexOf(b);
                if (ia < 0 || ib < 0 || Math.Abs(ia - ib) != 1) faults.Add($"door {d.RoomA}-{d.RoomB} joins rooms that are not neighbours on the main path");
            }

            // What stands in the rooms.
            int combatNumber = 0;
            foreach (LevelRoom r in rooms)
            {
                if (r.Kind == RoomKind.Start && !r.PlayerStart.HasValue) faults.Add($"{Name(r)} has no player start");
                if (r.Kind != RoomKind.Start && r.PlayerStart.HasValue) faults.Add($"{Name(r)} has a player start but is not the start room");
                int wanted = r.Kind == RoomKind.Combat ? plan.EnemiesInCombatRoom(combatNumber++) : r.Kind == RoomKind.Boss ? 1 : 0;
                if (r.EnemyPoints.Count != wanted) faults.Add($"{Name(r)} has {r.EnemyPoints.Count} enemy points, not {wanted}");

                List<LevelDoor> doors = level.DoorsOf(r.Order).Where(d => Known(d.Other(r.Order))).ToList();
                void CheckPoint(LevelPoint p, string what)
                {
                    string where = $"{Name(r)}'s {what} at {p}";
                    if (!(p.X >= r.Rect.X * cs && p.X <= r.Rect.Right * cs && p.Y >= r.Rect.Y * cs && p.Y <= r.Rect.Top * cs))
                    {
                        faults.Add(where + " is outside the room");
                        return;
                    }
                    if (LevelGeometry.DistanceToWalls(p, r.Rect, cs) < values.WallClearance - Slack)
                        faults.Add(where + " is nearer a wall than " + values.WallClearance + " m");
                    if (doors.Any(d => LevelGeometry.DistanceToDoor(p, d, cs) < values.WallClearance - Slack))
                        faults.Add(where + " is nearer a door than " + values.WallClearance + " m");
                }
                if (r.PlayerStart.HasValue) CheckPoint(r.PlayerStart.Value, "player start");
                for (int i = 0; i < r.EnemyPoints.Count; i++) CheckPoint(r.EnemyPoints[i], "enemy " + i);

                if (r.Kind != RoomKind.Combat) continue;
                LevelDoor entry = LevelGeometry.EntryDoor(level, r);
                for (int i = 0; i < r.EnemyPoints.Count; i++)
                {
                    LevelPoint p = r.EnemyPoints[i];
                    if (entry != null && LevelGeometry.DistanceToDoor(p, entry, cs) < values.EnemyFromEntry - Slack)
                        faults.Add($"{Name(r)}'s enemy {i} at {p} is nearer the door she enters by than {values.EnemyFromEntry} m");
                    for (int j = i + 1; j < r.EnemyPoints.Count; j++)
                        if (p.DistanceTo(r.EnemyPoints[j]) < values.EnemySpacing - Slack)
                            faults.Add($"{Name(r)}'s enemies {i} and {j} are nearer each other than {values.EnemySpacing} m");
                }
            }
            return faults;
        }
    }
}
