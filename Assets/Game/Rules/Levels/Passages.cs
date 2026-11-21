using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the passages between the rooms (work order 059; Kristian, 2026-11-20: "bedre bruk av mappet ... litt dungen
    /// crawler følelse trenger vi, ikke inn i store kontorlandskap med monstre i", and his choice of 2026-11-21, "Ganger mellom
    /// rommene"; the research report d's proposal C, its first order, and decision 0016). Data, all first guesses, lengths in
    /// cells (4 m). The generator reads them, so changing one changes the floors of old seeds (with the passages on).
    /// The producer's word of 2026-11-21, on Kristian's "likte lengden på hver etasje" (he liked each floor's length, before the
    /// templates made it about a tenth longer): the passages at the short end of what the order allows, 2 to 3 cells deep and
    /// as near a third of them jogging as the rooms' facing allows (the order's first guesses were 2 to 4, and a half).
    /// </summary>
    [Serializable]
    public sealed class PassageValues
    {
        public int GridCells = 64;        // cells on a side of the square a floor with passages must fit in (256 m): rooms apart need more ground than 48. LevelValues.GridCells stays 48: FightsOf lays out version 9's floor on it
        public int Fewest = 2;            // cells between two rooms of the row, the passage's depth: 8 m, the shortest that reads as a passage and not a thick doorway
        public int Most = 3;              // 12 m, about 2 s at her run (6 m/s). From 3 the camera (about 11 m ahead to the north) no longer shows the next door from the room. The order's first guess was 4 (16 m); the producer's word kept it short
        public int StraightMost = 3;      // no straight run of a passage is longer (12 m, the report's rule); a deeper passage always jogs
        public float JogShare = 1f;       // of the passages deep enough to jog (3 cells) and not made to, this share jogs where the two rooms face each other over enough cells for a jog (about half of them do; a template's door marks narrow it): 1 jogs every one that can, about a quarter of all passages with 2 and 3 deep drawn alike, the nearest to the producer's "a jog on about a third" (the order's first guess, 0.5 with 2 to 4 deep, gave about a half)
        public int JogMost = 2;           // cells sideways at a jog, at most
        public int FaceMore = 1;          // a room put more than StraightMost away faces the room before over this many cells more than a door needs, so its passage can jog
    }

    /// <summary>
    /// The passages between the rooms of the row (work order 059; Kristian's words of 2026-11-20 and his choice of 2026-11-21;
    /// the research report d's proposal C; decision 0016). On a floor with <see cref="LevelLayout.Passages"/> the rooms of the row
    /// stand apart, and a passage one cell wide (4 m between its wall lines, 3.5 m between the faces) runs from a door in the wall
    /// of each room to a door in the wall of the next. A passage is a room of its own kind (<see cref="RoomKind.Passage"/>) in the
    /// row between the two it joins: its rectangle is its bounding box, the cells off its path solid (work order 051's mask), so
    /// <see cref="LevelLayout.RoomAtCell"/> answers it only for its path and the walls, the kit, the colliders, the floor boxes,
    /// the NavMesh and both maps follow by themselves. It holds nothing, and nothing ever closes it.
    ///
    /// Its shape: straight out of the first room's wall, a jog of 1 to <see cref="PassageValues.JogMost"/> cells sideways in a
    /// row that is next to neither room (or none), and straight on into the next room: at most two bends, no straight run longer
    /// than <see cref="PassageValues.StraightMost"/>, its depth (cells between the two rooms' walls) from
    /// <see cref="PassageValues.Fewest"/> to <see cref="PassageValues.Most"/>. Its rules are <see cref="Faults"/>, and the walk of
    /// <see cref="WalkFault"/> proves at the level of cells that every open cell of the floor is reached from her start.
    /// </summary>
    public static class Passages
    {
        public const string Stage = "passages";

        public static bool IsPassage(LevelRoom room) => room != null && room.Kind == RoomKind.Passage;

        // ---------------- Facing, bands and the join ----------------

        /// <summary>
        /// The stretch along which <paramref name="b"/> faces <paramref name="a"/> across a gap, <paramref name="b"/> standing off
        /// <paramref name="a"/>'s side <paramref name="side"/>: from cell <paramref name="lo"/> up to, not including,
        /// <paramref name="hi"/> (x for a north or south side, y for an east or west one); false when <paramref name="b"/> is not
        /// beyond that side or they do not face each other.
        /// </summary>
        public static bool Facing(CellRect a, CellRect b, int side, out int lo, out int hi)
        {
            lo = hi = 0;
            bool beyond;
            switch (side)
            {
                case LevelGenerator.North: beyond = b.Y >= a.Top; break;
                case LevelGenerator.South: beyond = b.Top <= a.Y; break;
                case LevelGenerator.East: beyond = b.X >= a.Right; break;
                case LevelGenerator.West: beyond = b.Right <= a.X; break;
                default: return false;
            }
            if (!beyond) return false;
            bool alongX = side == LevelGenerator.North || side == LevelGenerator.South;
            lo = alongX ? Math.Max(a.X, b.X) : Math.Max(a.Y, b.Y);
            hi = alongX ? Math.Min(a.Right, b.Right) : Math.Min(a.Top, b.Top);
            return hi > lo;
        }

        /// <summary>The cells between the two facing walls, across the gap (0 when they do not face each other).</summary>
        public static int Gap(CellRect a, CellRect b, int side)
        {
            switch (side)
            {
                case LevelGenerator.North: return b.Y - a.Top;
                case LevelGenerator.South: return a.Y - b.Top;
                case LevelGenerator.East: return b.X - a.Right;
                case LevelGenerator.West: return a.X - b.Right;
                default: return 0;
            }
        }

        /// <summary>
        /// The band of a join (work order 059): the rectangle between the two rooms' facing walls, the gap deep, over the stretch
        /// where they face each other less one cell at each end. A passage always lies inside its band, and the bands keep every
        /// passage a cell from everything but its two rooms. An empty rectangle when they do not face each other over three cells.
        /// </summary>
        public static CellRect Band(CellRect a, CellRect b, int side)
        {
            if (!Facing(a, b, side, out int lo, out int hi) || hi - lo < 3) return new CellRect();
            int gap = Gap(a, b, side);
            switch (side)
            {
                case LevelGenerator.North: return new CellRect(lo + 1, a.Top, hi - lo - 2, gap);
                case LevelGenerator.South: return new CellRect(lo + 1, b.Top, hi - lo - 2, gap);
                case LevelGenerator.East: return new CellRect(a.Right, lo + 1, gap, hi - lo - 2);
                default: return new CellRect(b.Right, lo + 1, gap, hi - lo - 2);
            }
        }

        /// <summary>A passage of this depth must jog: deeper than <see cref="PassageValues.StraightMost"/>.</summary>
        public static bool MustJog(int gap, PassageValues pv) => gap > pv.StraightMost;

        /// <summary>A passage of this depth may jog: three cells or more, so its turn row is next to neither room.</summary>
        public static bool MayJog(int gap) => gap >= 3;

        /// <summary>The door places of the room after that a passage from <paramref name="x1"/> may jog to: 1 to <see cref="PassageValues.JogMost"/> cells sideways.</summary>
        public static List<int> JogsFrom(int x1, List<int> ins, PassageValues pv)
        {
            var list = new List<int>();
            foreach (int c in ins)
            {
                int d = Math.Abs(c - x1);
                if (d >= 1 && d <= pv.JogMost) list.Add(c);
            }
            return list;
        }

        /// <summary>
        /// The door places of the room before from which a passage of this depth reaches one of the room after's
        /// (<paramref name="ins"/>): straight on when it may run straight, or by a jog when it may jog.
        /// </summary>
        public static List<int> Starts(List<int> outs, List<int> ins, int gap, PassageValues pv)
        {
            bool must = MustJog(gap, pv), may = MayJog(gap);
            var list = new List<int>();
            foreach (int c in outs)
                if ((!must && ins.Contains(c)) || (may && JogsFrom(c, ins, pv).Count > 0)) list.Add(c);
            return list;
        }

        /// <summary>A passage of this depth can join a door place of <paramref name="outs"/> to one of <paramref name="ins"/> (see <see cref="Starts"/>).</summary>
        public static bool Joinable(List<int> outs, List<int> ins, int gap, PassageValues pv)
        {
            pv = pv ?? new PassageValues();
            return outs.Count > 0 && ins.Count > 0 && Starts(outs, ins, gap, pv).Count > 0;
        }

        // ---------------- The row ----------------

        /// <summary>The room before this one on the row, a passage between passed through (by its place in the run); -1 for none.</summary>
        public static int RoomBefore(LevelLayout level, LevelRoom room)
        {
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            if (entry == null) return -1;
            int before = entry.Other(room.Order);
            if (before < 0 || before >= level.Rooms.Count) return before;
            if (!IsPassage(room) && IsPassage(level.Rooms[before]))
            {
                LevelDoor back = LevelGeometry.EntryDoor(level, level.Rooms[before]);
                return back == null ? before : back.Other(before);
            }
            return before;
        }

        /// <summary>The room a passage leads on to: the later room of its door that is not <paramref name="from"/>; -1 for none.</summary>
        public static int RoomAfter(LevelLayout level, LevelRoom passage, LevelDoor from = null)
        {
            int best = -1;
            foreach (LevelDoor d in level.DoorsOf(passage.Order))
            {
                if (d == from) continue;
                int other = d.Other(passage.Order);
                if (other > passage.Order && other > best) best = other;
            }
            return best;
        }

        // ---------------- The path ----------------

        /// <summary>
        /// A passage's open cells as a path from the end at its door from the room before to the end at the other (work order
        /// 059); in the order of the open cells when it is no path (the validator says why).
        /// </summary>
        public static List<(int x, int y)> PathOf(LevelLayout level, LevelRoom passage)
        {
            var open = new List<(int x, int y)>();
            CellRect r = passage.Rect;
            for (int y = r.Y; y < r.Top; y++)
                for (int x = r.X; x < r.Right; x++)
                    if (passage.IsOpen(x, y)) open.Add((x, y));
            (int x, int y)? first = null;
            LevelDoor entry = LevelGeometry.EntryDoor(level, passage);
            if (entry != null)
                foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in entry.CellPairs())
                {
                    if (passage.IsOpen(pair.Key.x, pair.Key.y)) first = pair.Key;
                    else if (passage.IsOpen(pair.Value.x, pair.Value.y)) first = pair.Value;
                }
            if (first == null) return open;
            var path = new List<(int x, int y)> { first.Value };
            var seen = new HashSet<(int, int)> { first.Value };
            while (true)
            {
                (int x, int y) at = path[path.Count - 1];
                (int x, int y)? next = null;
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    (int x, int y) n = (at.x + dx, at.y + dy);
                    if (passage.IsOpen(n.x, n.y) && !seen.Contains(n)) { next = n; break; }
                }
                if (next == null) break;
                path.Add(next.Value);
                seen.Add(next.Value);
            }
            return path.Count == open.Count ? path : open;
        }

        /// <summary>The cells between the passage's two rooms' walls: its rectangle across its doors' lines.</summary>
        public static int DepthOf(LevelLayout level, LevelRoom passage)
        {
            LevelDoor d = level.DoorsOf(passage.Order).FirstOrDefault();
            if (d == null) return Math.Max(passage.Rect.Width, passage.Rect.Height);
            return d.Vertical ? passage.Rect.Width : passage.Rect.Height;
        }

        // ---------------- The rules ----------------

        /// <summary>
        /// The rule a door that joins a passage breaks, or null (work order 059): it is the passage's end, so it is not held to
        /// a wall on both sides of the stretch the two share (that stretch is the passage's own end). Instead it is on the wall
        /// of its room, and that wall runs on at least one cell beyond it on both sides, with no other door there.
        /// </summary>
        public static string DoorFault(LevelLayout level, LevelDoor d)
        {
            LevelRoom a = level.Rooms[d.RoomA], b = level.Rooms[d.RoomB];
            if (IsPassage(a) && IsPassage(b)) return "joins two passages";
            LevelRoom room = IsPassage(a) ? b : a;
            string name = room.Letter + room.Order.ToString(CultureInfo.InvariantCulture);
            int wall = LevelGeometry.WallOf(room.Rect, d);
            if (wall < 0) return "is not on a wall of " + name;
            int start = d.Vertical ? room.Rect.Y : room.Rect.X, end = d.Vertical ? room.Rect.Top : room.Rect.Right;
            if (d.From - 1 < start || d.From + d.Width + 1 > end)
                return $"has no wall on both sides: {name}'s wall does not run on a cell beyond it ({start} to {end})";
            foreach (LevelDoor o in level.DoorsOf(room.Order))
                if (o != d && o.Vertical == d.Vertical && o.Line == d.Line && o.From < d.From + d.Width + 1 && o.From + o.Width > d.From - 1)
                    return $"has another door of {name} beside it on its wall";
            return null;
        }

        /// <summary>
        /// Every rule a floor's passages break, added to <paramref name="faults"/> (work order 059). Every passage: exactly two
        /// doors, one at each end of its path, each on the outer side of its end cell, the two on opposite sides of its rectangle;
        /// its depth from <see cref="PassageValues.Fewest"/> to <see cref="PassageValues.Most"/>; its rectangle the bounding box of
        /// its open cells, which are one path (4-connected, each with at most two open neighbours in it, exactly two with one);
        /// at most two bends, none in a cell next to either of its rooms; no straight run longer than
        /// <see cref="PassageValues.StraightMost"/>; its rectangle touching no room but its two and no other passage; no shape, no
        /// theme, not elite, nothing standing in it. And on the floor: no two rooms that are not passages touch.
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues values, List<string> faults)
        {
            PassageValues pv = values?.PassageRules ?? new PassageValues();
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            List<LevelRoom> rooms = level.Rooms;
            foreach (LevelRoom p in rooms)
            {
                if (!IsPassage(p)) continue;
                string name = Name(p);
                List<LevelDoor> doors = level.DoorsOf(p.Order).ToList();
                if (doors.Count != 2) faults.Add($"{name} has {doors.Count} doors, and a passage 2");
                if (p.Shape != RoomShape.None) faults.Add($"{name} has the shape {RoomShapes.Word(p.Shape)}, and a passage none");
                if (p.Theme != RoomTheme.None) faults.Add($"{name} is a {RoomThemes.Word(p.Theme)}, and a passage has no theme");
                if (p.Elite) faults.Add($"{name} is marked elite");
                if (p.EnemyPoints.Count > 0) faults.Add($"{name} holds enemies");
                if (p.Props.Count > 0) faults.Add($"{name} holds {p.Props.Count} props, and a passage none");
                if (p.PlayerStart.HasValue) faults.Add($"{name} has her start");

                // Its open cells: the bounding box, one path.
                var open = new HashSet<(int, int)>();
                int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
                for (int y = p.Rect.Y; y < p.Rect.Top; y++)
                    for (int x = p.Rect.X; x < p.Rect.Right; x++)
                        if (p.IsOpen(x, y))
                        {
                            open.Add((x, y));
                            x0 = Math.Min(x0, x); y0 = Math.Min(y0, y); x1 = Math.Max(x1, x + 1); y1 = Math.Max(y1, y + 1);
                        }
                if (open.Count == 0)
                {
                    faults.Add($"{name} has no open cell");
                    continue;
                }
                if (!new CellRect(x0, y0, x1 - x0, y1 - y0).Equals(p.Rect)) faults.Add($"{name}'s rectangle {p.Rect} is not the bounding box of its open cells");
                int Neighbours(int x, int y) => (open.Contains((x + 1, y)) ? 1 : 0) + (open.Contains((x - 1, y)) ? 1 : 0)
                                              + (open.Contains((x, y + 1)) ? 1 : 0) + (open.Contains((x, y - 1)) ? 1 : 0);
                int ends = 0;
                bool branched = false;
                foreach ((int x, int y) in open)
                {
                    int n = Neighbours(x, y);
                    if (n > 2) branched = true;
                    if (n == 1) ends++;
                }
                if (branched) faults.Add($"{name} branches: a cell has more than two open neighbours in it, and a passage is one path one cell wide");
                if (ends != 2) faults.Add($"{name} has {ends} ends, and a passage 2");
                var reach = new HashSet<(int, int)>();
                var queue = new Queue<(int x, int y)>();
                (int, int) seed = open.First();
                reach.Add(seed);
                queue.Enqueue(seed);
                while (queue.Count > 0)
                {
                    (int x, int y) c = queue.Dequeue();
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        (int, int) n = (c.x + dx, c.y + dy);
                        if (open.Contains(n) && reach.Add(n)) queue.Enqueue(n);
                    }
                }
                if (reach.Count != open.Count) faults.Add($"{name}'s open cells are not one path ({reach.Count} of {open.Count} joined)");

                // Its doors: one at each end, on the outer side of its end cell, on opposite sides of its rectangle.
                var walls = new List<int>();
                var endsUsed = new HashSet<(int, int)>();
                foreach (LevelDoor d in doors)
                {
                    int wall = LevelGeometry.WallOf(p.Rect, d);
                    if (wall < 0)
                    {
                        faults.Add($"{name}'s door {d.RoomA}-{d.RoomB} is not on a side of its rectangle");
                        continue;
                    }
                    walls.Add(wall);
                    foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                    {
                        (int x, int y) inside = p.Rect.Contains(pair.Key.x, pair.Key.y) ? pair.Key : pair.Value;
                        if (!open.Contains(inside)) faults.Add($"{name}'s door {d.RoomA}-{d.RoomB} opens on a solid cell {inside}");
                        else if (Neighbours(inside.x, inside.y) != 1 && open.Count > 1) faults.Add($"{name}'s door {d.RoomA}-{d.RoomB} is not at an end of its path, at {inside}");
                        else endsUsed.Add(inside);
                    }
                }
                if (walls.Count == 2 && walls[1] != LevelGenerator.Opposite(walls[0])) faults.Add($"{name}'s two doors are not on opposite sides of its rectangle");
                if (doors.Count == 2 && endsUsed.Count != 2) faults.Add($"{name}'s two doors are not at its two ends");

                // Its depth, its bends and its straight runs.
                int depth = DepthOf(level, p);
                if (depth < pv.Fewest || depth > pv.Most) faults.Add($"{name} is {depth} cells deep, and a passage {pv.Fewest} to {pv.Most}");
                List<(int x, int y)> path = PathOf(level, p);
                if (path.Count == open.Count && path.Count >= 2 && !branched && doors.Count > 0)
                {
                    bool vertical = doors[0].Vertical; // the doors' lines run along x = const: the passage runs east and west
                    int Across((int x, int y) c) => vertical ? c.x - p.Rect.X : c.y - p.Rect.Y;
                    int bends = 0, run = 1;
                    for (int i = 1; i < path.Count; i++)
                    {
                        bool turned = i + 1 < path.Count
                                      && (path[i].x - path[i - 1].x != path[i + 1].x - path[i].x || path[i].y - path[i - 1].y != path[i + 1].y - path[i].y);
                        run++;
                        if (run > pv.StraightMost)
                        {
                            faults.Add($"{name} runs straight over more than {pv.StraightMost} cells ({pv.StraightMost * level.CellSize:0} m) at {path[i]}");
                            run = int.MinValue;
                        }
                        if (!turned) continue;
                        bends++;
                        int across = Across(path[i]);
                        if (across == 0 || across == depth - 1) faults.Add($"{name} bends at {path[i]}, in a cell next to one of its rooms");
                        run = 1;
                    }
                    if (bends > 2) faults.Add($"{name} bends {bends} times, and a passage at most twice");
                }

                // It touches only its two rooms.
                var own = new HashSet<int>(doors.Select(d => d.Other(p.Order)));
                foreach (LevelRoom r in rooms)
                {
                    if (r == p || own.Contains(r.Order) || !p.Rect.Touches(r.Rect)) continue;
                    faults.Add(IsPassage(r) ? $"{name} touches the passage {Name(r)}" : $"{name} touches {Name(r)}, which is not one of its two rooms");
                }
            }
            // No two rooms that are not passages touch.
            for (int i = 0; i < rooms.Count; i++)
                for (int j = i + 1; j < rooms.Count; j++)
                    if (!IsPassage(rooms[i]) && !IsPassage(rooms[j]) && rooms[i].Rect.Touches(rooms[j].Rect))
                        faults.Add($"{Name(rooms[i])} touches {Name(rooms[j])}, and on a floor with passages the rooms stand apart");
        }

        /// <summary>
        /// The walk over cells (work order 059), or null when it reaches everything: 4-connected from the cell of her start, it
        /// steps between two cells only when both are open cells of one room (a passage counted), or when the edge between them
        /// is a door's opening (<see cref="LevelDoor.CellPairs"/>). It must reach every open cell of every room and passage: what
        /// the door graph says, proved at the level of cells.
        /// </summary>
        public static string WalkFault(LevelLayout level)
        {
            LevelRoom start = level.Rooms.FirstOrDefault(r => r.Kind == RoomKind.Entrance);
            if (start == null) return "the walk has no entrance to start from";
            double cs = level.CellSize;
            LevelPoint at = start.PlayerStart ?? start.Middle(cs);
            (int x, int y) from = ((int)Math.Floor(at.X / cs), (int)Math.Floor(at.Y / cs));
            if (level.RoomAtCell(from.x, from.y) == null) return "the walk starts on no open cell, at " + from;
            var doorEdges = new HashSet<((int, int), (int, int))>();
            foreach (LevelDoor d in level.Doors)
                foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                {
                    doorEdges.Add((pair.Key, pair.Value));
                    doorEdges.Add((pair.Value, pair.Key));
                }
            var owner = new Dictionary<(int, int), LevelRoom>();
            int total = 0;
            foreach (LevelRoom r in level.Rooms)
                for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                    for (int x = r.Rect.X; x < r.Rect.Right; x++)
                        if (level.RoomAtCell(x, y) == r)
                        {
                            owner[(x, y)] = r;
                            total++;
                        }
            var reached = new HashSet<(int, int)> { from };
            var queue = new Queue<(int x, int y)>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                (int x, int y) c = queue.Dequeue();
                LevelRoom here = owner[c];
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    (int x, int y) n = (c.x + dx, c.y + dy);
                    if (reached.Contains(n) || !owner.TryGetValue(n, out LevelRoom there)) continue;
                    if (there != here && !doorEdges.Contains((c, n))) continue;
                    reached.Add(n);
                    queue.Enqueue(n);
                }
            }
            if (reached.Count == total) return null;
            foreach (KeyValuePair<(int, int), LevelRoom> pair in owner)
                if (!reached.Contains(pair.Key))
                    return $"the walk over cells from her start reaches {reached.Count} of the floor's {total} open cells: not {pair.Value.Letter}{pair.Value.Order.ToString(CultureInfo.InvariantCulture)}'s cell {pair.Key}";
            return null;
        }
    }

    public static partial class LevelGeometry
    {
        /// <summary>
        /// A room's place among the floor's rooms that are not passages, from 0 (work order 059: the run box's "Room k of n"
        /// counts the rooms, not the passages between them); for a passage, the rooms before it. Its order on a floor without.
        /// </summary>
        public static int RoomNumber(LevelLayout level, LevelRoom room)
        {
            if (level == null || room == null) return -1;
            int n = 0;
            List<LevelRoom> rooms = level.Rooms;
            for (int i = 0; i < rooms.Count && rooms[i].Order < room.Order; i++)
                if (rooms[i].Kind != RoomKind.Passage) n++;
            return n;
        }

        /// <summary>The floor's rooms that are not passages (work order 059); all its rooms on a floor without.</summary>
        public static int RoomCount(LevelLayout level)
        {
            if (level == null) return 0;
            int n = 0;
            List<LevelRoom> rooms = level.Rooms;
            for (int i = 0; i < rooms.Count; i++)
                if (rooms[i].Kind != RoomKind.Passage) n++;
            return n;
        }
    }
}
