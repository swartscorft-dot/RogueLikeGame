using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>What a doorway says of the room it leads on to (work order 041): nothing for an ordinary room.</summary>
    public enum DoorSign { None, Treasure, Elite, Exit, Boss }

    /// <summary>
    /// The numbers of the door signs and of the small map (work order 041). All first guesses, kept together here with their
    /// defaults in code. Colours as picked (sRGB); lengths in metres. The generator never reads them.
    /// </summary>
    [Serializable]
    public sealed class DoorSignValues
    {
        // The colours (Kristian's answer of 2026-11-20: marked doors; the treasure rooms gold, the elite room red, the exit
        // the most ominous).
        public Rgb Gold = new Rgb(1f, 0.74f, 0.22f);       // a treasure room: warm gold
        public Rgb Red = new Rgb(1f, 0.16f, 0.1f);         // the elite room
        public Rgb Exit = new Rgb(0.62f, 0.22f, 1f);       // the exit room: a cold violet, nothing else in the game is
        public Rgb Boss = new Rgb(0.78f, 0.1f, 0.95f);     // the exit room of the act's last floor, the act boss's: deeper
        public float DimShare = 0.3f;                      // a dim sign's colour, this share of its lit one, greyed half way
        public float RimGlow = 2f;                         // the sign's outer shape, as the mark pips' rim
        public float CoreGlow = 3f;                        // its inner shape, which only a lit sign has

        // The sign over the doorway
        public float SignSize = 1.5f;                      // m across
        public float OverDoor = 0.4f;                      // m over the arch's top (the wall's without the kit)
        public float OutFromWall = 0.6f;                   // m from the door's line into the room before it

        // The light spilling from the doorway on to her side: one a floor, at the lit doorway, none while no doorway is lit
        public float LightRange = 7f;
        public float LightIntensity = 2.5f;
        public float LightHeight = 1.2f;                   // m over the floor
        public float LightOut = 1.4f;                      // m from the door's line into the room she is in
        public int LightsMost = 1;                         // the door signs' share of the light budget: lights on at once

        // The small map (FloorMapRules)
        public float MapHeightShare = 1f / 6f;             // of the view's height, at most
        public float MapWidthMost = 200f;                  // of the 720-high layout, at most
        public float LargeHeightShare = 0.72f;             // held open over the middle
    }

    /// <summary>
    /// The doors that say what is behind them (work order 041; Kristian's second session of 2026-11-20: "It is not quite
    /// intuitive where the chest rooms are; I end up double-checking several rooms for them", and his answers: marked
    /// doors, and opened chests and cleared rooms shown). As plain rules.
    ///
    /// A door's sign is the room it leads on to: of its two rooms, the later in the run (the one she comes into through it).
    /// A treasure room's is the gold chest, the elite room's the red horned helm, the exit room's the violet skull, and on the
    /// act's last floor, where the exit room holds the act boss, the crowned skull; an ordinary combat room's none. A sign is
    /// lit while she stands in the room before the door (the door on, seen from her room) and the room beyond is not done;
    /// dim otherwise: the door she came in by is always dim, and so is every door to a room that is done. A treasure room is
    /// done when its chest is opened; any other room when it held enemies and every one is dead.
    /// </summary>
    public static class DoorSignRules
    {
        /// <summary>The sign of a room on this floor: what a door into it shows.</summary>
        public static DoorSign SignOf(LevelRoom room, int floor, int floorsPerAct)
        {
            if (room == null) return DoorSign.None;
            if (room.Kind == RoomKind.Treasure) return DoorSign.Treasure;
            if (room.Kind == RoomKind.Exit) return floor >= floorsPerAct ? DoorSign.Boss : DoorSign.Exit;
            return room.Elite ? DoorSign.Elite : DoorSign.None;
        }

        /// <summary>The room a door leads on to: the later of its two in the run.</summary>
        public static int Beyond(LevelDoor door) => Math.Max(door.RoomA, door.RoomB);

        /// <summary>The room a door is the way on from: the earlier of its two.</summary>
        public static int Before(LevelDoor door) => Math.Min(door.RoomA, door.RoomB);

        /// <summary>A room is done: a treasure room once its chest is opened; any other once it held enemies and all are dead.</summary>
        public static bool Done(RoomKind kind, bool chestOpened, bool cleared) => kind == RoomKind.Treasure ? chestOpened : cleared;

        /// <summary>Lit: a sign, she is in the room before the door (<paramref name="here"/>, by its place in the run), and the room beyond is not done.</summary>
        public static bool Lit(DoorSign sign, LevelDoor door, int here, bool beyondDone) =>
            sign != DoorSign.None && door != null && here == Before(door) && !beyondDone;

        /// <summary>The sign's colour, lit.</summary>
        public static Rgb ColourOf(DoorSign sign, DoorSignValues v)
        {
            v = v ?? new DoorSignValues();
            switch (sign)
            {
                case DoorSign.Treasure: return v.Gold;
                case DoorSign.Elite: return v.Red;
                case DoorSign.Exit: return v.Exit;
                case DoorSign.Boss: return v.Boss;
                default: return new Rgb(1f, 1f, 1f);
            }
        }

        /// <summary>A dim sign's colour: <see cref="DoorSignValues.DimShare"/> of the lit one, half way to its grey.</summary>
        public static Rgb DimOf(Rgb c, DoorSignValues v)
        {
            float k = (v ?? new DoorSignValues()).DimShare;
            float grey = (c.R + c.G + c.B) / 3f;
            return new Rgb((c.R + grey) * 0.5f * k, (c.G + grey) * 0.5f * k, (c.B + grey) * 0.5f * k);
        }

        /// <summary>
        /// The sign's shape: convex pieces, each a list of points (x right, y up) inside the square from -1 to 1, anticlockwise.
        /// Drawn as they are for the outer shape, and each shrunk about its own middle for the inner one (<see cref="Shrunk"/>).
        /// The chest: a box and its lid; the helm: a shield with two horns; the skull: a cranium and its jaw; the crowned skull:
        /// the skull under three points.
        /// </summary>
        public static List<double[]> ShapeOf(DoorSign sign)
        {
            var parts = new List<double[]>();
            switch (sign)
            {
                case DoorSign.Treasure:
                    parts.Add(Box(-0.85, -0.75, 0.85, 0.1));                               // the box
                    parts.Add(Arc(0, 0.1, 0.85, 0.55, 0, Math.PI, 9));                     // the lid
                    break;
                case DoorSign.Elite:
                    parts.Add(new[] { -0.55, 0.45, -0.55, -0.15, 0.0, -0.85, 0.55, -0.15, 0.55, 0.45 }); // the shield
                    parts.Add(new[] { -0.55, 0.45, -0.3, 0.45, -0.95, 0.95 });             // the left horn
                    parts.Add(new[] { 0.3, 0.45, 0.55, 0.45, 0.95, 0.95 });                // the right horn
                    break;
                case DoorSign.Exit:
                case DoorSign.Boss:
                    double top = sign == DoorSign.Boss ? -0.1 : 0.05;
                    parts.Add(Arc(0, top, 0.7, 0.7, 0, 2 * Math.PI, 14));                 // the cranium
                    parts.Add(Box(-0.4, top - 0.9, 0.4, top - 0.35));                      // the jaw
                    if (sign == DoorSign.Boss)
                    {
                        parts.Add(new[] { -0.75, 0.45, -0.25, 0.45, -0.6, 1.0 });          // the crown's three points
                        parts.Add(new[] { -0.25, 0.45, 0.25, 0.45, 0.0, 1.0 });
                        parts.Add(new[] { 0.25, 0.45, 0.75, 0.45, 0.6, 1.0 });
                    }
                    break;
            }
            return parts;
        }

        /// <summary>A piece shrunk to <paramref name="share"/> about the middle of its points.</summary>
        public static double[] Shrunk(double[] piece, double share)
        {
            double cx = 0, cy = 0;
            int n = piece.Length / 2;
            for (int i = 0; i < n; i++)
            {
                cx += piece[2 * i];
                cy += piece[2 * i + 1];
            }
            cx /= n;
            cy /= n;
            var s = new double[piece.Length];
            for (int i = 0; i < n; i++)
            {
                s[2 * i] = cx + (piece[2 * i] - cx) * share;
                s[2 * i + 1] = cy + (piece[2 * i + 1] - cy) * share;
            }
            return s;
        }

        /// <summary>True when the point is inside one of the pieces (for drawing the sign on the map).</summary>
        public static bool Inside(List<double[]> parts, double x, double y)
        {
            foreach (double[] p in parts)
            {
                int n = p.Length / 2;
                bool inside = n >= 3;
                for (int i = 0; i < n && inside; i++)
                {
                    double ax = p[2 * i], ay = p[2 * i + 1], bx = p[2 * ((i + 1) % n)], by = p[2 * ((i + 1) % n) + 1];
                    if ((bx - ax) * (y - ay) - (by - ay) * (x - ax) < 0) inside = false;
                }
                if (inside) return true;
            }
            return false;
        }

        static double[] Box(double x0, double y0, double x1, double y1) => new[] { x0, y0, x1, y0, x1, y1, x0, y1 };

        /// <summary>Points round an ellipse from angle <paramref name="from"/> to <paramref name="to"/>, anticlockwise; less than a whole turn closes with the chord.</summary>
        static double[] Arc(double cx, double cy, double rx, double ry, double from, double to, int steps)
        {
            bool whole = to - from >= 2 * Math.PI - 1e-9;
            int count = whole ? steps : steps + 1;
            var p = new double[count * 2];
            for (int i = 0; i < count; i++)
            {
                double a = from + (to - from) * i / steps;
                p[2 * i] = cx + rx * Math.Cos(a);
                p[2 * i + 1] = cy + ry * Math.Sin(a);
            }
            return p;
        }

        /// <summary>Twice the area a piece's points go round: positive when they go anticlockwise.</summary>
        public static double TwiceArea(double[] p)
        {
            int n = p.Length / 2;
            double area = 0;
            for (int i = 0; i < n; i++)
            {
                int j = (i + 1) % n;
                area += p[2 * i] * p[2 * j + 1] - p[2 * j] * p[2 * i + 1];
            }
            return area;
        }
    }

    /// <summary>How a room shows on the small map (work order 041).</summary>
    public enum MapShown { Hidden, Ahead, Entered, Here }

    /// <summary>
    /// The small map in the corner (work order 041; Kristian's answers of 2026-11-20: a small map in the corner, and opened
    /// chests and cleared rooms shown), as plain rules. The map draws the floor's rooms as their outlines, to scale, and the
    /// doors between those it shows. A room she is in shows lit, with a dot where she is; a room she has been in shows
    /// filled, dim once it is done (<see cref="DoorSignRules.Done"/>); a room she has not been in shows only as an outline,
    /// and only once the room she comes into it from has been entered: she sees one step ahead, not the whole floor. A room
    /// with a sign shows its sign's colour and its sign (the door's), while it is not done; done, a treasure room loses its
    /// gold (grey like the rest) and any other's sign goes dim. A door shows when both its rooms do.
    /// </summary>
    public static class FloorMapRules
    {
        /// <summary>The room she comes into this one from (the other room of its door in), by its place in the run; -1 for none.</summary>
        public static int Before(LevelLayout level, LevelRoom room)
        {
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            return entry == null ? -1 : entry.Other(room.Order);
        }

        /// <summary>How a room shows: here, entered, one step ahead of a room entered, or not at all.</summary>
        public static MapShown Shown(bool isHere, bool entered, bool beforeEntered) =>
            isHere ? MapShown.Here : entered ? MapShown.Entered : beforeEntered ? MapShown.Ahead : MapShown.Hidden;

        /// <summary>The room is filled: here or entered. One ahead is an outline only.</summary>
        public static bool Filled(MapShown shown) => shown == MapShown.Here || shown == MapShown.Entered;

        /// <summary>Its fill is dim: entered (not here) and done.</summary>
        public static bool Dim(MapShown shown, bool done) => shown == MapShown.Entered && done;

        /// <summary>
        /// How its sign shows: 1 lit, a share for dim, 0 none. None for a room without one or not shown, and for a treasure
        /// room whose chest is opened (it loses its gold); dim for any other room that is done.
        /// </summary>
        public static float SignStrength(DoorSign sign, MapShown shown, RoomKind kind, bool done, DoorSignValues v)
        {
            if (sign == DoorSign.None || shown == MapShown.Hidden) return 0f;
            if (!done) return 1f;
            return kind == RoomKind.Treasure ? 0f : (v ?? new DoorSignValues()).DimShare;
        }

        /// <summary>A door shows when both its rooms do.</summary>
        public static bool DoorShown(MapShown a, MapShown b) => a != MapShown.Hidden && b != MapShown.Hidden;

        /// <summary>
        /// The map's size in the layout 720 high, for a floor whose rooms span <paramref name="width"/> by
        /// <paramref name="height"/> metres: as tall as <paramref name="most"/> high, as wide as its shape makes it, no wider
        /// than <paramref name="widest"/> (then shorter). The scale is metres to the layout's units.
        /// </summary>
        public static void Fit(double width, double height, double most, double widest, out double w, out double h, out double scale)
        {
            width = Math.Max(1e-6, width);
            height = Math.Max(1e-6, height);
            scale = Math.Min(most / height, widest / width);
            w = width * scale;
            h = height * scale;
        }
    }
}
