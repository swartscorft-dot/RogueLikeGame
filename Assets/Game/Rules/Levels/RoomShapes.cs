using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The outline a room's rectangle is carved to (work order 051: the research report d's proposal A, "the same rectangles,
    /// carved, with gates"; Kristian's words of 2026-11-20: "Now we have very clear squares per room. A room does not have to
    /// be a square; or it may well be one in the 'backend', but it feels very unnatural that everything is a square ... We
    /// need a bit of a dungeon-crawler feeling, not walking into large office landscapes with monsters in them"). None: no
    /// shape was drawn (every room with <see cref="LevelValues.Shapes"/> off, and hand-made layouts). Plain: the rectangle
    /// with only its gate. The others are said by <see cref="RoomShapes"/>.
    /// </summary>
    public enum RoomShape { None, Plain, Notch, Cross, Apse, Alcoves, Aisles, Octagon }

    /// <summary>
    /// The numbers of the rooms' carved outlines (work order 051). Data, all first guesses; lengths in cells (4 m) unless they
    /// say metres. The generator reads them, so changing one changes the floors of old seeds (with the shapes on).
    /// </summary>
    [Serializable]
    public sealed class ShapeValues
    {
        // What every carved room keeps (the validator holds every room to it)
        public int MiddleSquare = 3;           // an open square this many cells on a side (12 m) stays round every room's middle
        public int ExitMiddleSquare = 4;       // 16 m in the exit room: the act boss stands at its middle, and no prop within 8 m of it
        public int TreasureMiddleSquare = 1;   // the treasure room (3 x 3, 12 m) cannot keep 12 m beside its gate: its middle cell, the chest's
        public float DoorStraightIn = 6f;      // m of open floor straight in behind every door, on both its sides
        public float MostSolidShare = 0.25f;   // of a room's cells at most this share solid, its gate counted (the report's 25 %)
        public float ExitMostSolidShare = 0.3f; // the exit room's, so it can take the report's cross of 2 x 2 corners

        // Which shapes a room may take
        public int LargeFrom = 6;              // a combat room this many cells on its shorter side or more may take alcoves and pillar aisles
        public int BigFrom = 7;                // a side this long takes the larger cuts: 2 cells at a cross's corners and an apse's ends, a stepped apse, two aisles
        public int AlcoveFrom = 5;             // a wall with no door at least this long may hold alcoves
        public int Draws = 6;                  // draws of a room's shape that may break a rule before it falls back to its gate alone

        // How likely each shape is, among those a room may take (the elite room's grand ones times EliteGrand)
        public float NotchWeight = 1f;
        public float CrossWeight = 1f;
        public float ApseWeight = 1f;
        public float AlcovesWeight = 1f;
        public float AislesWeight = 1f;
        public float OctagonWeight = 1.5f;
        public float EliteGrand = 2f;          // the elite room: aisles, cross and octagon this many times as likely
    }

    /// <summary>
    /// One face of wall toward a room (work order 051): a side of its rectangle less its doors, or a carved cell's open side.
    /// <see cref="Side"/> is the wall it stands for, so <see cref="PropRules.Inward"/> of it points into the room; on a
    /// vertical line (x = <see cref="Line"/>) it runs over the cells y = <see cref="From"/> to <see cref="To"/> (not
    /// including it), on a horizontal one over x. <see cref="Low"/>: the low wall the camera looks over (a face with the room
    /// to its north).
    /// </summary>
    public struct WallFace
    {
        public int Side;
        public bool Vertical;
        public int Line, From, To;
        public bool Carved;
        public bool Low => Side == LevelGenerator.South;
    }

    /// <summary>
    /// The rooms' carved outlines (work order 051; the research report d's proposal A). A stage of the generator on its own
    /// stream of the seed (<see cref="Stage"/>), after the layout and before the contents, so a seed keeps its rooms'
    /// places, sizes, order and doors: some cells of a room's rectangle become solid, and <see cref="LevelLayout.RoomAtCell"/>
    /// answers no room for them, so the walls, the kit, the colliders, the NavMesh and the maps follow by themselves.
    ///
    /// The shapes (each drawn in its room's own frame: the far wall is the one opposite the door in). Notch: one or two
    /// corners cut, 1 x 1 or 2 x 1. Cross: all four corners cut, 2 cells along a side of <see cref="ShapeValues.BigFrom"/>
    /// or more, else 1 (a chamfered room in a small one, a cross with arms three cells wide in a large one). Apse: the two
    /// ends of the far wall, 2 cells each along a wall of BigFrom, and stepped one row in when the room is BigFrom deep: a
    /// recess at the far wall, where the shrine's altar and the throne stand. Alcoves: along each wall with no door at least
    /// <see cref="ShapeValues.AlcoveFrom"/> long (one or both, drawn), every third cell solid from one end (drawn), so the
    /// wall holds chapels two cells wide and one deep between piers a cell wide. Aisles: a row of single solid cells, a pier
    /// each, one cell in from a long wall (both long walls in a room BigFrom wide), every other cell. Octagon: each corner
    /// stepped, three cells. And in every room the gate (<see cref="Gate"/>).
    ///
    /// Which a room may take: the entrance a notch; a treasure room nothing but its gate (a small pocket); a combat room a
    /// notch, a cross or an apse, from <see cref="ShapeValues.LargeFrom"/> on its shorter side also alcoves and aisles, and
    /// the elite room BigFrom wide also the octagon (its grand ones favoured, <see cref="ShapeValues.EliteGrand"/>); the exit
    /// room the grandest: the octagon, the cross, the apse or the aisles (with a door in its north wall only the apse or the
    /// aisles, so the throne keeps its place there). Each room's shape is drawn by the weights, never the
    /// shape of the room before it in the run while it may take another, so no two rooms in a row are alike. A draw that
    /// breaks a rule of <see cref="Faults"/> is drawn again, up to <see cref="ShapeValues.Draws"/> times, and then the room
    /// keeps only its gate (Plain, "fell back").
    /// </summary>
    public static class RoomShapes
    {
        public const string Stage = "shapes";

        public static string Word(RoomShape shape)
        {
            switch (shape)
            {
                case RoomShape.Plain: return "plain";
                case RoomShape.Notch: return "notch";
                case RoomShape.Cross: return "cross";
                case RoomShape.Apse: return "apse";
                case RoomShape.Alcoves: return "alcoves";
                case RoomShape.Aisles: return "aisles";
                case RoomShape.Octagon: return "octagon";
                default: return "none";
            }
        }

        /// <summary>The shapes a room may take, by its kind and size (see the class).</summary>
        public static List<RoomShape> ShapesFor(LevelLayout level, LevelRoom room, ShapeValues v)
        {
            var list = new List<RoomShape>();
            int small = Math.Min(room.Rect.Width, room.Rect.Height);
            switch (room.Kind)
            {
                case RoomKind.Entrance:
                    list.Add(RoomShape.Notch);
                    break;
                case RoomKind.Treasure:
                    break;
                case RoomKind.Exit:
                    // The throne stands at the north wall's middle (work order 028): with a door in the north wall, beside its
                    // gate, the north corners stay whole for it.
                    bool northDoor = level.DoorsOf(room.Order).Any(d => LevelGeometry.WallOf(room.Rect, d) == LevelGenerator.North);
                    if (!northDoor)
                    {
                        list.Add(RoomShape.Octagon);
                        list.Add(RoomShape.Cross);
                    }
                    list.Add(RoomShape.Apse);
                    list.Add(RoomShape.Aisles);
                    break;
                default:
                    list.Add(RoomShape.Notch);
                    list.Add(RoomShape.Cross);
                    list.Add(RoomShape.Apse);
                    if (small >= v.LargeFrom)
                    {
                        list.Add(RoomShape.Alcoves);
                        list.Add(RoomShape.Aisles);
                    }
                    if (room.Elite && small >= v.BigFrom) list.Add(RoomShape.Octagon);
                    break;
            }
            return list;
        }

        static float WeightOf(RoomShape s, LevelRoom room, ShapeValues v)
        {
            float w;
            switch (s)
            {
                case RoomShape.Notch: w = v.NotchWeight; break;
                case RoomShape.Cross: w = v.CrossWeight; break;
                case RoomShape.Apse: w = v.ApseWeight; break;
                case RoomShape.Alcoves: w = v.AlcovesWeight; break;
                case RoomShape.Aisles: w = v.AislesWeight; break;
                case RoomShape.Octagon: w = v.OctagonWeight; break;
                default: w = 0f; break;
            }
            if (room.Elite && (s == RoomShape.Aisles || s == RoomShape.Cross || s == RoomShape.Octagon)) w *= v.EliteGrand;
            return Math.Max(0f, w);
        }

        static double Unit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));

        static RoomShape Pick(List<RoomShape> options, LevelRoom room, ShapeValues v, LevelRandom rng)
        {
            double total = 0;
            foreach (RoomShape s in options) total += WeightOf(s, room, v);
            double pick = Unit(rng) * total;
            foreach (RoomShape s in options)
            {
                double w = WeightOf(s, room, v);
                if (pick < w) return s;
                pick -= w;
            }
            return options[options.Count - 1];
        }

        /// <summary>
        /// Carves every room of the floor (see the class), from the stream given, and marks the floor as carved
        /// (<see cref="LevelLayout.Shapes"/>). The rooms in the order of the run.
        /// </summary>
        public static void Carve(LevelLayout level, LevelRandom rng, LevelValues values)
        {
            ShapeValues v = values.ShapeRules ?? new ShapeValues();
            level.Shapes = true;
            RoomShape before = RoomShape.None;
            foreach (LevelRoom room in level.Rooms)
            {
                room.ClearSolid();
                room.Shape = RoomShape.None;
                room.ShapeRedraws = 0;
                room.ShapeFellBack = false;
                List<RoomShape> options = ShapesFor(level, room, v);
                if (options.Count > 1) options.Remove(before);
                bool drawn = false;
                for (int d = 0; d < v.Draws && options.Count > 0; d++)
                {
                    RoomShape shape = Pick(options, room, v, rng);
                    room.ClearSolid();
                    Gate(level, room);
                    int gate = room.SolidCount;
                    // A draw must carve something beyond the gate, leave no pocket, and break no rule.
                    if (Draw(level, room, shape, rng, v) && FillPockets(level, room, v) && room.SolidCount > gate && Faults(level, room, values, null) == 0)
                    {
                        room.Shape = shape;
                        drawn = true;
                        break;
                    }
                    room.ShapeRedraws++;
                }
                if (!drawn)
                {
                    room.ClearSolid();
                    Gate(level, room);
                    room.Shape = RoomShape.Plain;
                    room.ShapeFellBack = options.Count > 0;
                    if (Faults(level, room, values, null) > 0)
                    {
                        room.ClearSolid();
                        room.Shape = RoomShape.None;
                    }
                }
                before = room.Shape;
            }
        }

        // ---------------- Cells in a room's frame ----------------

        /// <summary>The length of a room's wall of this side, in cells, and how deep the room is from it.</summary>
        static int LengthOf(CellRect r, int side) => side == LevelGenerator.North || side == LevelGenerator.South ? r.Width : r.Height;
        static int DepthOf(CellRect r, int side) => side == LevelGenerator.North || side == LevelGenerator.South ? r.Height : r.Width;

        /// <summary>The cell <paramref name="along"/> its wall of <paramref name="side"/> (west to east, south to north) and <paramref name="depth"/> in from it.</summary>
        static void CellAt(CellRect r, int side, int along, int depth, out int x, out int y)
        {
            switch (side)
            {
                case LevelGenerator.North: x = r.X + along; y = r.Top - 1 - depth; break;
                case LevelGenerator.South: x = r.X + along; y = r.Y + depth; break;
                case LevelGenerator.East: x = r.Right - 1 - depth; y = r.Y + along; break;
                default: x = r.X + depth; y = r.Y + along; break;
            }
        }

        static void Put(LevelRoom room, int side, int along, int depth, HashSet<(int, int)> kept)
        {
            CellAt(room.Rect, side, along, depth, out int x, out int y);
            if (room.Rect.Contains(x, y) && !kept.Contains((x, y))) room.SetSolid(x, y, true);
        }

        /// <summary>
        /// The cell inside the room at cell <paramref name="i"/> of a door's opening, and the step straight into the room
        /// from it; false when the door is not on the room's rectangle.
        /// </summary>
        public static bool InnerCell(LevelRoom room, LevelDoor door, int i, out int x, out int y, out int dx, out int dy)
        {
            CellRect r = room.Rect;
            x = y = dx = dy = 0;
            if (door.Vertical)
            {
                if (door.Line == r.X) { x = r.X; dx = 1; }
                else if (door.Line == r.Right) { x = r.Right - 1; dx = -1; }
                else return false;
                y = i;
            }
            else
            {
                if (door.Line == r.Y) { y = r.Y; dy = 1; }
                else if (door.Line == r.Top) { y = r.Top - 1; dy = -1; }
                else return false;
                x = i;
            }
            return r.Contains(x, y);
        }

        /// <summary>
        /// The room's gate (work order 041's doors, work order 051): just inside the door she comes in by, the cells either
        /// side of its opening along the wall are solid, so the room is entered through a slot one cell (4 m) wide and deep
        /// and is not seen whole from the room before; a cell left alone between a gate cell and the room's corner is filled
        /// too (no one-cell pocket). Nothing in the entrance, which has no door in.
        /// </summary>
        public static void Gate(LevelLayout level, LevelRoom room)
        {
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            if (entry == null) return;
            CellRect r = room.Rect;
            int ax = entry.Vertical ? 0 : 1, ay = entry.Vertical ? 1 : 0;
            if (!InnerCell(room, entry, entry.From, out int fx, out int fy, out _, out _)) return;
            if (!InnerCell(room, entry, entry.From + entry.Width - 1, out int lx, out int ly, out _, out _)) return;
            void Side(int gx, int gy, int sx, int sy)
            {
                if (!r.Contains(gx, gy)) return;
                room.SetSolid(gx, gy, true);
                if (r.Contains(gx + sx, gy + sy) && !r.Contains(gx + 2 * sx, gy + 2 * sy)) room.SetSolid(gx + sx, gy + sy, true);
            }
            Side(fx - ax, fy - ay, -ax, -ay);
            Side(lx + ax, ly + ay, ax, ay);
        }

        /// <summary>The cells a room must keep open whatever its shape: every door's cell inside and the cells straight in behind it.</summary>
        static HashSet<(int, int)> KeptOpen(LevelLayout level, LevelRoom room, ShapeValues v)
        {
            var kept = new HashSet<(int, int)>();
            int deep = StraightInCells(level, v);
            foreach (LevelDoor d in level.DoorsOf(room.Order))
                for (int i = d.From; i < d.From + d.Width; i++)
                {
                    if (!InnerCell(room, d, i, out int x, out int y, out int dx, out int dy)) continue;
                    for (int k = 0; k < deep; k++) kept.Add((x + dx * k, y + dy * k));
                }
            return kept;
        }

        static int StraightInCells(LevelLayout level, ShapeValues v) =>
            Math.Max(1, (int)Math.Ceiling(v.DoorStraightIn / Math.Max(1e-6, level.CellSize) - 1e-9));

        /// <summary>Marks the cells of a shape (see the class); false when the room has no place for it (alcoves without a wall to hold them).</summary>
        static bool Draw(LevelLayout level, LevelRoom room, RoomShape shape, LevelRandom rng, ShapeValues v)
        {
            CellRect r = room.Rect;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            int wayIn = entry != null ? LevelGeometry.WallOf(r, entry) : -1;
            int far = wayIn >= 0 ? LevelGenerator.Opposite(wayIn) : LevelGenerator.North;
            HashSet<(int, int)> kept = KeptOpen(level, room, v); // a cell behind a door is left out of any shape
            switch (shape)
            {
                case RoomShape.Notch:
                {
                    var corners = new List<int> { 0, 1, 2, 3 };
                    rng.Shuffle(corners);
                    // The entrance: never a corner on the wall of its door, where its two braziers stand by the door.
                    if (room.Kind == RoomKind.Entrance)
                        corners.RemoveAll(c => level.DoorsOf(room.Order).Any(d => CornerOnWall(c, LevelGeometry.WallOf(r, d))));
                    int k = Math.Min(corners.Count, 1 + rng.Below(2));
                    for (int c = 0; c < k; c++)
                    {
                        int form = rng.Below(3); // 1 x 1, 2 along x, 2 along y
                        Corner(room, corners[c], form == 1 ? 2 : 1, form == 2 ? 2 : 1, kept);
                    }
                    return true;
                }
                case RoomShape.Cross:
                {
                    int a = r.Width >= v.BigFrom ? 2 : 1, b = r.Height >= v.BigFrom ? 2 : 1;
                    // The larger corners only where the room's share allows them beside its gate (three cells at most).
                    if (4 * a * b + 3 > ShareOf(room, v) * r.Width * r.Height) a = b = 1;
                    for (int c = 0; c < 4; c++) Corner(room, c, a, b, kept);
                    return true;
                }
                case RoomShape.Octagon:
                    for (int c = 0; c < 4; c++)
                    {
                        Corner(room, c, 2, 1, kept);
                        Corner(room, c, 1, 2, kept);
                    }
                    return true;
                case RoomShape.Apse:
                {
                    int length = LengthOf(r, far), depth = DepthOf(r, far);
                    int ends = length >= v.BigFrom ? 2 : 1;
                    for (int a = 0; a < ends; a++)
                    {
                        Put(room, far, a, 0, kept);
                        Put(room, far, length - 1 - a, 0, kept);
                    }
                    if (depth >= v.BigFrom)
                    {
                        Put(room, far, 0, 1, kept);
                        Put(room, far, length - 1, 1, kept);
                    }
                    return true;
                }
                case RoomShape.Alcoves:
                {
                    var walls = new List<int>();
                    foreach (int side in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                    {
                        bool door = false;
                        foreach (LevelDoor d in level.DoorsOf(room.Order))
                            if (LevelGeometry.WallOf(r, d) == side) door = true;
                        if (!door && LengthOf(r, side) >= v.AlcoveFrom) walls.Add(side);
                    }
                    if (walls.Count == 0) return false;
                    rng.Shuffle(walls);
                    int count = walls.Count > 1 && rng.Below(2) == 0 ? 2 : 1;
                    int before = room.SolidCount;
                    bool mirror = rng.Below(2) == 0;
                    for (int w = 0; w < count; w++)
                    {
                        int length = LengthOf(r, walls[w]);
                        for (int a = 0; a < length; a++)
                        {
                            if ((mirror ? length - 1 - a : a) % 3 != 0) continue;
                            CellAt(r, walls[w], a, 0, out int x, out int y);
                            if (!kept.Contains((x, y))) room.SetSolid(x, y, true);
                        }
                    }
                    return room.SolidCount >= before + 2; // at least two piers, or it is no row of alcoves
                }
                case RoomShape.Aisles:
                {
                    bool alongX = r.Width > r.Height || (r.Width == r.Height && rng.Below(2) == 0);
                    // The exit room's aisles run north and south, a nave to the throne at the north wall.
                    if (room.Kind == RoomKind.Exit) alongX = false;
                    int length = alongX ? r.Width : r.Height, across = alongX ? r.Height : r.Width;
                    int a0 = alongX ? LevelGenerator.South : LevelGenerator.West, a1 = alongX ? LevelGenerator.North : LevelGenerator.East;
                    var rows = new List<int>();
                    if (across >= v.BigFrom)
                    {
                        rows.Add(a0);
                        rows.Add(a1);
                    }
                    else rows.Add(rng.Below(2) == 0 ? a0 : a1);
                    int offset = length % 2 == 0 ? 1 + rng.Below(2) : 1;
                    int pillars = 0;
                    foreach (int side in rows)
                        for (int a = offset; a <= length - 2; a += 2)
                        {
                            CellAt(r, side, a, 1, out int x, out int y);
                            if (kept.Contains((x, y)) || ThinWith(room, x, y)) continue;
                            room.SetSolid(x, y, true);
                            pillars++;
                        }
                    return pillars >= 2 * rows.Count; // at least two piers a row, or it is no aisle
                }
                default:
                    return true;
            }
        }

        /// <summary>
        /// Fills every open cell walled on three sides that no door needs (a one-cell pocket a shape left beside a gate or a
        /// corner), until none is left; false when one that a door needs is walled so.
        /// </summary>
        static bool FillPockets(LevelLayout level, LevelRoom room, ShapeValues v)
        {
            HashSet<(int, int)> kept = KeptOpen(level, room, v);
            CellRect r = room.Rect;
            for (bool again = true; again;)
            {
                again = false;
                for (int y = r.Y; y < r.Top; y++)
                    for (int x = r.X; x < r.Right; x++)
                    {
                        if (room.IsSolid(x, y)) continue;
                        int walled = (Blocked(level, room, x, y, 1, 0) ? 1 : 0) + (Blocked(level, room, x, y, -1, 0) ? 1 : 0)
                                   + (Blocked(level, room, x, y, 0, 1) ? 1 : 0) + (Blocked(level, room, x, y, 0, -1) ? 1 : 0);
                        if (walled < 3) continue;
                        if (kept.Contains((x, y))) return false;
                        room.SetSolid(x, y, true);
                        again = true;
                    }
            }
            return true;
        }

        /// <summary>Corner <paramref name="c"/> (0 south-west, 1 south-east, 2 north-east, 3 north-west) is on the wall of this side.</summary>
        static bool CornerOnWall(int c, int side) =>
            side == LevelGenerator.South ? c == 0 || c == 1 : side == LevelGenerator.North ? c == 2 || c == 3
            : side == LevelGenerator.East ? c == 1 || c == 2 : side == LevelGenerator.West && (c == 0 || c == 3);

        /// <summary>A corner cut <paramref name="a"/> cells along x and <paramref name="b"/> along y; corners 0 south-west, 1 south-east, 2 north-east, 3 north-west.</summary>
        static void Corner(LevelRoom room, int corner, int a, int b, HashSet<(int, int)> kept)
        {
            CellRect r = room.Rect;
            bool east = corner == 1 || corner == 2, north = corner == 2 || corner == 3;
            for (int i = 0; i < a; i++)
                for (int j = 0; j < b; j++)
                {
                    int x = east ? r.Right - 1 - i : r.X + i, y = north ? r.Top - 1 - j : r.Y + j;
                    if (r.Contains(x, y) && !kept.Contains((x, y))) room.SetSolid(x, y, true);
                }
        }

        /// <summary>True when a solid cell here would touch another only at a corner with open cells on the other diagonal.</summary>
        static bool ThinWith(LevelRoom room, int x, int y)
        {
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sy = -1; sy <= 1; sy += 2)
                    if (room.IsSolid(x + sx, y + sy) && room.IsOpen(x + sx, y) && room.IsOpen(x, y + sy)) return true;
            return false;
        }

        // ---------------- The rules ----------------

        /// <summary>
        /// Every rule a room's carved outline breaks, in words, added to <paramref name="faults"/> (when given); the count.
        /// Nothing for a room with no solid cell and no shape. The rules (work order 051): no more than
        /// <see cref="ShapeValues.MostSolidShare"/> of its cells solid (<see cref="ShapeValues.ExitMostSolidShare"/> in the exit room); its open cells one area, joined side to side; an open
        /// square of <see cref="ShapeValues.MiddleSquare"/> cells on a side round its middle (<see cref="ShapeValues.ExitMiddleSquare"/>
        /// in the exit room, <see cref="ShapeValues.TreasureMiddleSquare"/> in a treasure room); behind every door's opening its
        /// cell and the cells straight in for <see cref="ShapeValues.DoorStraightIn"/> open; no open cell walled on three sides
        /// (a dead-end slot one cell, 3.5 m between the walls' faces, wide: nothing to be stuck in, no line that cannot be
        /// side-stepped); no two solid cells touching only at a corner with open cells on the other diagonal (no thin corner a
        /// shot could slip through); and on a carved floor (<see cref="LevelLayout.Shapes"/>) a room with a shape has its gate
        /// (<see cref="Gate"/>).
        /// </summary>
        public static int Faults(LevelLayout level, LevelRoom room, LevelValues values, List<string> faults)
        {
            bool shaped = level.Shapes && room.Shape != RoomShape.None;
            if (room.SolidCount == 0 && !shaped) return 0;
            ShapeValues v = values?.ShapeRules ?? new ShapeValues();
            CellRect r = room.Rect;
            int w = r.Width, h = r.Height, count = 0;
            if (w <= 0 || h <= 0) return 0;
            void Add(string f)
            {
                count++;
                faults?.Add(f);
            }

            // The share.
            float share = ShareOf(room, v);
            if (room.SolidCount > share * w * h + 1e-9)
                Add($"has {room.SolidCount} of its {w * h} cells solid, more than {share:P0}");

            // One area.
            int open = w * h - room.SolidCount, reached = 0;
            if (open <= 0) Add("has no open cell");
            else
            {
                var seen = new bool[w * h];
                var queue = new Queue<int>();
                for (int k = 0; k < w * h && queue.Count == 0; k++)
                    if (!room.IsSolid(r.X + k % w, r.Y + k / w))
                    {
                        seen[k] = true;
                        queue.Enqueue(k);
                    }
                while (queue.Count > 0)
                {
                    int k = queue.Dequeue(), cx = k % w, cy = k / w;
                    reached++;
                    void Step(int nx, int ny)
                    {
                        if (nx < 0 || ny < 0 || nx >= w || ny >= h) return;
                        int n = ny * w + nx;
                        if (seen[n] || room.IsSolid(r.X + nx, r.Y + ny)) return;
                        seen[n] = true;
                        queue.Enqueue(n);
                    }
                    Step(cx + 1, cy);
                    Step(cx - 1, cy);
                    Step(cx, cy + 1);
                    Step(cx, cy - 1);
                }
                if (reached != open) Add($"has its open cells in more than one area ({reached} of {open} joined)");
            }

            // The open square round the middle.
            int s = room.Kind == RoomKind.Exit ? v.ExitMiddleSquare : room.Kind == RoomKind.Treasure ? v.TreasureMiddleSquare : v.MiddleSquare;
            s = Math.Max(1, Math.Min(s, Math.Min(w, h)));
            if (!MiddleSquareOpen(room, s))
                Add($"keeps no open square of {s} cells ({s * level.CellSize:0} m) round its middle");

            // Behind every door.
            int deep = StraightInCells(level, v);
            foreach (LevelDoor d in level.DoorsOf(room.Order))
                for (int i = d.From; i < d.From + d.Width; i++)
                {
                    if (!InnerCell(room, d, i, out int x, out int y, out int dx, out int dy)) continue;
                    for (int k = 0; k < deep; k++)
                    {
                        int cx = x + dx * k, cy = y + dy * k;
                        if (!r.Contains(cx, cy)) break;
                        if (room.IsSolid(cx, cy))
                        {
                            Add($"has a solid cell ({cx},{cy}) within {v.DoorStraightIn:0.#} m straight in behind door {d.RoomA}-{d.RoomB}");
                            break;
                        }
                    }
                }

            // No dead-end slot, no thin corner.
            for (int cy = r.Y; cy < r.Top; cy++)
                for (int cx = r.X; cx < r.Right; cx++)
                {
                    if (!room.IsSolid(cx, cy))
                    {
                        int walled = (Blocked(level, room, cx, cy, 1, 0) ? 1 : 0) + (Blocked(level, room, cx, cy, -1, 0) ? 1 : 0)
                                   + (Blocked(level, room, cx, cy, 0, 1) ? 1 : 0) + (Blocked(level, room, cx, cy, 0, -1) ? 1 : 0);
                        if (walled >= 3) Add($"has a dead-end slot at ({cx},{cy}), walled on {walled} sides");
                    }
                    if (cx + 1 < r.Right && cy + 1 < r.Top)
                    {
                        bool a = room.IsSolid(cx, cy), b = room.IsSolid(cx + 1, cy), c = room.IsSolid(cx, cy + 1), dd = room.IsSolid(cx + 1, cy + 1);
                        if ((a && dd && !b && !c) || (b && c && !a && !dd))
                            Add($"has two solid cells touching only at the corner ({cx + 1},{cy + 1}), open on the other diagonal");
                    }
                }

            // The gate.
            if (shaped)
            {
                LevelDoor entry = LevelGeometry.EntryDoor(level, room);
                if (entry != null && !HasGate(room, entry)) Add($"has no gate inside door {entry.RoomA}-{entry.RoomB}");
            }
            return count;
        }

        /// <summary>The share of a room's cells that may be solid.</summary>
        static float ShareOf(LevelRoom room, ShapeValues v) => room.Kind == RoomKind.Exit ? v.ExitMostSolidShare : v.MostSolidShare;

        /// <summary>The cells either side of a door's opening, just inside the room, are solid: its gate (<see cref="Gate"/>).</summary>
        public static bool HasGate(LevelRoom room, LevelDoor door)
        {
            int ax = door.Vertical ? 0 : 1, ay = door.Vertical ? 1 : 0;
            if (!InnerCell(room, door, door.From, out int fx, out int fy, out _, out _)) return false;
            if (!InnerCell(room, door, door.From + door.Width - 1, out int lx, out int ly, out _, out _)) return false;
            return room.IsSolid(fx - ax, fy - ay) && room.IsSolid(lx + ax, ly + ay);
        }

        /// <summary>An open square <paramref name="s"/> cells on a side, inside the room, with the room's middle inside it (not on its edge).</summary>
        public static bool MiddleSquareOpen(LevelRoom room, int s)
        {
            CellRect r = room.Rect;
            for (int x0 = 0; x0 + s <= r.Width; x0++)
            {
                if (!(2 * x0 < r.Width && r.Width < 2 * (x0 + s))) continue;
                for (int y0 = 0; y0 + s <= r.Height; y0++)
                {
                    if (!(2 * y0 < r.Height && r.Height < 2 * (y0 + s))) continue;
                    bool all = true;
                    for (int i = 0; i < s && all; i++)
                        for (int j = 0; j < s && all; j++)
                            if (room.IsSolid(r.X + x0 + i, r.Y + y0 + j)) all = false;
                    if (all) return true;
                }
            }
            return false;
        }

        /// <summary>The side of an open cell toward (dx, dy) is walled: a solid cell of the room, or the room's wall with no door in it there.</summary>
        static bool Blocked(LevelLayout level, LevelRoom room, int x, int y, int dx, int dy)
        {
            int nx = x + dx, ny = y + dy;
            if (room.Rect.Contains(nx, ny)) return room.IsSolid(nx, ny);
            bool vertical = dx != 0;
            int line = vertical ? Math.Max(x, nx) : Math.Max(y, ny), cell = vertical ? y : x;
            foreach (LevelDoor d in level.DoorsOf(room.Order))
                if (d.Vertical == vertical && d.Line == line && cell >= d.From && cell < d.From + d.Width) return false;
            return true;
        }

        // ---------------- Distances to the carved cells ----------------

        /// <summary>Metres from a point to the nearest solid cell of the room; 0 inside one; infinity when it has none.</summary>
        public static double DistanceToSolid(LevelRoom room, LevelPoint p, double cs)
        {
            if (room.SolidCount == 0) return double.PositiveInfinity;
            double best = double.PositiveInfinity;
            foreach ((int x, int y) in room.SolidList)
            {
                double dx = Math.Max(Math.Max(x * cs - p.X, 0), p.X - (x + 1) * cs);
                double dy = Math.Max(Math.Max(y * cs - p.Y, 0), p.Y - (y + 1) * cs);
                double d = dx * dx + dy * dy;
                if (d < best) best = d;
            }
            return Math.Sqrt(best);
        }

        /// <summary>
        /// Metres from a footprint to the nearest solid cell of the room, 0 when it reaches into one, looked for no farther
        /// than <paramref name="upTo"/> (that, or more, when none is nearer); infinity when the room has none.
        /// </summary>
        public static double DistanceToSolid(LevelRoom room, PropFootprint f, double cs, double upTo = double.PositiveInfinity)
        {
            if (room.SolidCount == 0) return double.PositiveInfinity;
            f.Extent(out double minX, out double minY, out double maxX, out double maxY);
            double best = upTo;
            foreach ((int x, int y) in room.SolidList)
            {
                double dx = Math.Max(Math.Max(x * cs - maxX, 0), minX - (x + 1) * cs);
                double dy = Math.Max(Math.Max(y * cs - maxY, 0), minY - (y + 1) * cs);
                if (Math.Sqrt(dx * dx + dy * dy) > best) continue; // its box is already farther than the nearest found
                var cell = new PropFootprint
                {
                    Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5,
                };
                double d = f.DistanceTo(cell);
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>
        /// Metres from a point in a room to its nearest wall: the four sides of its rectangle or a carved cell (work order
        /// 051: distances to walls are measured to the outline, not the rectangle).
        /// </summary>
        public static double DistanceToWalls(LevelRoom room, LevelPoint p, double cs) =>
            Math.Min(LevelGeometry.DistanceToWalls(p, room.Rect, cs), DistanceToSolid(room, p, cs));

        /// <summary>
        /// The rule a place for one enemy (or her start) breaks against the carved cells, or null: it stands on an open cell of
        /// its room, at least <paramref name="clear"/> from every solid cell (work order 051).
        /// </summary>
        public static string PlaceFault(LevelRoom room, LevelPoint p, double cs, double clear)
        {
            if (room.SolidCount == 0) return null;
            int x = (int)Math.Floor(p.X / cs), y = (int)Math.Floor(p.Y / cs);
            if (room.IsSolid(x, y)) return "is on a solid cell";
            if (NearSolid(room, p, cs, clear)) return "is nearer a carved wall than " + clear.ToString(CultureInfo.InvariantCulture) + " m";
            return null;
        }

        /// <summary>A solid cell of the room is nearer the point than <paramref name="within"/>: the cells round its own first, all when within is more than a cell.</summary>
        public static bool NearSolid(LevelRoom room, LevelPoint p, double cs, double within)
        {
            if (room.SolidCount == 0) return false;
            if (within > cs) return DistanceToSolid(room, p, cs) < within - 1e-9;
            int px = (int)Math.Floor(p.X / cs), py = (int)Math.Floor(p.Y / cs);
            double w2 = (within - 1e-9) * (within - 1e-9);
            for (int x = px - 1; x <= px + 1; x++)
                for (int y = py - 1; y <= py + 1; y++)
                {
                    if (!room.IsSolid(x, y)) continue;
                    double dx = Math.Max(Math.Max(x * cs - p.X, 0), p.X - (x + 1) * cs);
                    double dy = Math.Max(Math.Max(y * cs - p.Y, 0), p.Y - (y + 1) * cs);
                    if (dx * dx + dy * dy < w2) return true;
                }
            return false;
        }

        /// <summary>
        /// Marks the squares of a grid (<paramref name="nx"/> by <paramref name="ny"/>, <paramref name="step"/> apart from
        /// (<paramref name="x0"/>, <paramref name="y0"/>), row by row) whose middles are nearer a solid cell of the room than
        /// <paramref name="within"/> (inside one, too): the lane's and the walkable reckoning's grids (work order 051).
        /// </summary>
        public static void MarkNearSolid(LevelRoom room, double cs, double x0, double y0, double step, int nx, int ny, double within, bool[] mark)
        {
            if (room.SolidCount == 0) return;
            double w2 = (within - 1e-9) * (within - 1e-9);
            foreach ((int x, int y) in room.SolidList)
            {
                double ax = x * cs, bx = (x + 1) * cs, ay = y * cs, by = (y + 1) * cs;
                int i0 = Math.Max(0, (int)Math.Floor((ax - within - x0) / step)), i1 = Math.Min(nx - 1, (int)Math.Ceiling((bx + within - x0) / step));
                int j0 = Math.Max(0, (int)Math.Floor((ay - within - y0) / step)), j1 = Math.Min(ny - 1, (int)Math.Ceiling((by + within - y0) / step));
                for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        double cx = x0 + (i + 0.5) * step, cy = y0 + (j + 0.5) * step;
                        double dx = Math.Max(Math.Max(ax - cx, 0), cx - bx), dy = Math.Max(Math.Max(ay - cy, 0), cy - by);
                        if (dx * dx + dy * dy < w2) mark[j * nx + i] = true;
                    }
            }
        }

        // ---------------- The outline ----------------

        /// <summary>
        /// The room's open cells as rectangles (work order 051): each row's runs of open cells, each taken as far north as the
        /// rows above it run the same; the first starts at its south-west-most open cell. The rectangle alone for a room with
        /// no solid cell. The floor boxes and the small map's fill are these.
        /// </summary>
        public static List<CellRect> OpenRects(LevelRoom room)
        {
            var list = new List<CellRect>();
            CellRect r = room.Rect;
            if (room.SolidCount == 0)
            {
                list.Add(r);
                return list;
            }
            int w = r.Width, h = r.Height;
            var covered = new bool[w * h];
            bool Free(int i, int j) => !covered[j * w + i] && !room.IsSolid(r.X + i, r.Y + j);
            for (int j = 0; j < h; j++)
                for (int i = 0; i < w; i++)
                {
                    if (!Free(i, j)) continue;
                    int run = 1;
                    while (i + run < w && Free(i + run, j)) run++;
                    int up = 1;
                    while (j + up < h)
                    {
                        bool all = true;
                        for (int k = 0; k < run && all; k++) all = Free(i + k, j + up);
                        // The row above must not run on past the rectangle's ends, or it would be cut in two.
                        if (all && i > 0 && Free(i - 1, j + up)) all = false;
                        if (all && i + run < w && Free(i + run, j + up)) all = false;
                        if (!all) break;
                        up++;
                    }
                    for (int b = 0; b < up; b++)
                        for (int k = 0; k < run; k++) covered[(j + b) * w + i + k] = true;
                    list.Add(new CellRect(r.X + i, r.Y + j, run, up));
                }
            return list;
        }

        /// <summary>One stretch of a room's outline: on a vertical line (x = Line) from y = From to To, or a horizontal one over x.</summary>
        public struct Edge
        {
            public bool Vertical;
            public int Line, From, To;
        }

        /// <summary>
        /// The room's outline as it is (work order 051): every side of an open cell that has no open cell of the room beyond it,
        /// joined into stretches along each line. Its four sides for a room with no solid cell.
        /// </summary>
        public static List<Edge> Outline(LevelRoom room)
        {
            var edges = new List<Edge>();
            CellRect r = room.Rect;
            for (int x = r.X; x <= r.Right; x++)
            {
                int from = int.MinValue;
                for (int y = r.Y; y <= r.Top; y++)
                {
                    bool edge = y < r.Top && room.IsOpen(x - 1, y) != room.IsOpen(x, y);
                    if (edge && from == int.MinValue) from = y;
                    if (!edge && from != int.MinValue)
                    {
                        edges.Add(new Edge { Vertical = true, Line = x, From = from, To = y });
                        from = int.MinValue;
                    }
                }
            }
            for (int y = r.Y; y <= r.Top; y++)
            {
                int from = int.MinValue;
                for (int x = r.X; x <= r.Right; x++)
                {
                    bool edge = x < r.Right && room.IsOpen(x, y - 1) != room.IsOpen(x, y);
                    if (edge && from == int.MinValue) from = x;
                    if (!edge && from != int.MinValue)
                    {
                        edges.Add(new Edge { Vertical = false, Line = y, From = from, To = x });
                        from = int.MinValue;
                    }
                }
            }
            return edges;
        }

        /// <summary>
        /// Every face of wall toward the room (work order 051), for a prop that wants a wall behind it (a banner, a bone wall,
        /// a shelf): the sides of its rectangle less its doors' openings, then each carved cell's open sides, cell by cell. A
        /// later prop asks for these, stands with its back at a face (<see cref="PropValues.WallFace"/> past its line for a wall
        /// piece) and faces <see cref="PropRules.Inward"/> of its <see cref="WallFace.Side"/>; <see cref="PropRules.Fault"/>
        /// measures it to the nearest face, carved or not (<see cref="NearestFace"/>).
        /// </summary>
        public static List<WallFace> WallFaces(LevelLayout level, LevelRoom room)
        {
            var faces = new List<WallFace>();
            CellRect r = room.Rect;
            void Side(int side, bool vertical, int line, int from, int to)
            {
                var gaps = new List<(int lo, int hi)>();
                foreach (LevelDoor d in level.DoorsOf(room.Order))
                    if (d.Vertical == vertical && d.Line == line) gaps.Add((d.From, d.From + d.Width));
                gaps.Sort((a, b) => a.lo.CompareTo(b.lo));
                int at = from;
                foreach ((int lo, int hi) in gaps)
                {
                    if (lo > at) faces.Add(new WallFace { Side = side, Vertical = vertical, Line = line, From = at, To = Math.Min(lo, to) });
                    at = Math.Max(at, hi);
                }
                if (at < to) faces.Add(new WallFace { Side = side, Vertical = vertical, Line = line, From = at, To = to });
            }
            Side(LevelGenerator.West, true, r.X, r.Y, r.Top);
            Side(LevelGenerator.East, true, r.Right, r.Y, r.Top);
            Side(LevelGenerator.South, false, r.Y, r.X, r.Right);
            Side(LevelGenerator.North, false, r.Top, r.X, r.Right);
            faces.AddRange(CarvedFaces(room));
            return faces;
        }

        /// <summary>The open sides of the room's solid cells, as faces of wall toward it (a cell's east side is a west wall for the cell beyond it).</summary>
        public static List<WallFace> CarvedFaces(LevelRoom room) => new List<WallFace>(FacesOf(room));

        /// <summary>The room's carved faces, worked out once while its solid cells stay as they are (not to be changed).</summary>
        static List<WallFace> FacesOf(LevelRoom room)
        {
            if (room.ShapeCache is List<WallFace> cached) return cached;
            var faces = new List<WallFace>();
            room.ShapeCache = faces;
            foreach ((int x, int y) in room.SolidList)
            {
                if (room.IsOpen(x + 1, y)) faces.Add(new WallFace { Side = LevelGenerator.West, Vertical = true, Line = x + 1, From = y, To = y + 1, Carved = true });
                if (room.IsOpen(x - 1, y)) faces.Add(new WallFace { Side = LevelGenerator.East, Vertical = true, Line = x, From = y, To = y + 1, Carved = true });
                if (room.IsOpen(x, y + 1)) faces.Add(new WallFace { Side = LevelGenerator.South, Vertical = false, Line = y + 1, From = x, To = x + 1, Carved = true });
                if (room.IsOpen(x, y - 1)) faces.Add(new WallFace { Side = LevelGenerator.North, Vertical = false, Line = y, From = x, To = x + 1, Carved = true });
            }
            return faces;
        }

        /// <summary>
        /// The face of wall nearest a footprint's back, and how far its back is from the face's line (negative past it): the
        /// side of the room's rectangle (<see cref="PropFootprint.NearestSide"/>) unless a carved face that it stands in front
        /// of (overlapping it along the face) is nearer.
        /// </summary>
        public static WallFace NearestFace(LevelRoom room, PropFootprint f, double cs, out double back)
        {
            int side = f.NearestSide(room.Rect, cs, out back);
            CellRect r = room.Rect;
            var best = new WallFace
            {
                Side = side, Vertical = side == LevelGenerator.East || side == LevelGenerator.West,
                Line = side == LevelGenerator.West ? r.X : side == LevelGenerator.East ? r.Right : side == LevelGenerator.South ? r.Y : r.Top,
                From = side == LevelGenerator.East || side == LevelGenerator.West ? r.Y : r.X,
                To = side == LevelGenerator.East || side == LevelGenerator.West ? r.Top : r.Right,
            };
            if (room.SolidCount == 0) return best;
            f.Extent(out double minX, out double minY, out double maxX, out double maxY);
            foreach (WallFace face in FacesOf(room))
            {
                double lo = face.From * cs, hi = face.To * cs, line = face.Line * cs;
                double a0 = face.Vertical ? minY : minX, a1 = face.Vertical ? maxY : maxX;
                if (a1 <= lo + 1e-9 || a0 >= hi - 1e-9) continue;
                double d;
                switch (face.Side)
                {
                    case LevelGenerator.West: d = minX - line; break;
                    case LevelGenerator.East: d = line - maxX; break;
                    case LevelGenerator.South: d = minY - line; break;
                    default: d = line - maxY; break;
                }
                if (d < -1e-6) continue; // not wholly in front of the face
                if (d < back - 1e-9)
                {
                    back = d;
                    best = face;
                }
            }
            return best;
        }

        /// <summary>The rooms' shapes and solid cells as text, for the level's description: empty for a room with no shape.</summary>
        public static string Describe(LevelRoom room)
        {
            if (room.Shape == RoomShape.None && room.SolidCount == 0) return "";
            var sb = new StringBuilder();
            sb.Append(" shape ").Append(Word(room.Shape));
            if (room.SolidCount > 0)
            {
                sb.Append(" solid");
                foreach ((int x, int y) in room.SolidList) sb.Append(' ').Append(x.ToString(CultureInfo.InvariantCulture)).Append(',').Append(y.ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }
    }
}
