using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// What a thing of a floor is drawn as in the greybox view (work order 084; Kristian sent a talk on level design on
    /// 2026-11-22, "Denne videoen var veldig fin", and ticked "Gråboks-bryter og romlab": a space is judged in greybox, before
    /// its art). Hidden: drawn not at all (decoration with no collider). AsItIs: gameplay, drawn as it is (a chest, a reward
    /// on the ground, the enemies, she, the door signs).
    /// </summary>
    public enum GreyboxPart { Hidden, Floor, Wall, Door, HighCover, LowCover, BlocksWalking, FightLine, AsItIs }

    /// <summary>
    /// The greybox view's numbers (work order 084). Colours as picked (sRGB); first guesses, chosen to be told apart at a glance
    /// from the camera's height: the two kinds of cover a warm red and a cold blue, far apart in hue and in lightness; the
    /// floor and the walls two greys a step apart; the doors orange; what only blocks walking a muted purple; the fight's line
    /// the game's cyan. Lengths in metres.
    /// </summary>
    [Serializable]
    public sealed class GreyboxValues
    {
        public Rgb Floor = new Rgb(0.50f, 0.50f, 0.50f);         // one flat mid grey
        public Rgb Wall = new Rgb(0.27f, 0.27f, 0.29f);          // one darker grey, at the walls' true collision shapes
        public Rgb Door = new Rgb(0.96f, 0.56f, 0.10f);          // the doorway's strip on the floor and its two frame posts
        public Rgb HighCover = new Rgb(0.82f, 0.20f, 0.18f);     // stops shots; she cannot duck behind it and shoot over
        public Rgb LowCover = new Rgb(0.22f, 0.48f, 0.92f);      // she shoots over it; Left Ctrl ducks (or takes cover) behind it
        public Rgb BlocksWalking = new Rgb(0.58f, 0.40f, 0.70f); // blocks walking and nothing else (the candelabra)
        public Rgb FightLine = new Rgb(0.20f, 0.92f, 0.92f);     // work order 070's line, where the room wakes
        public float FightLineWidth = 0.15f;                     // m across the line on the floor: thin, read from 28 m up
        public float LineHeight = 0.03f;                         // m: the line's and the door strip's thickness over the floor
        public float DoorApron = 0.6f;                           // m the door's strip reaches past the wall on each side
    }

    /// <summary>
    /// The greybox view's rules (work order 084), no engine: which part a thing is from what it does, the part's colour and
    /// word, where the fight's line lies on a room's floor, and the room's words for the developer's box.
    /// </summary>
    public static class GreyboxRules
    {
        /// <summary>
        /// The part a prop of the floor is, from what it does in the game now: gameplay (a chest) as it is; no collider (the
        /// dais, the banner: walked over or under) not shown; a collider that stops shots (it carries cover, and the quick duck
        /// does not sort it low) high cover; one she can take cover or duck behind and shoot over (it carries shelter) low
        /// cover; any other collider (the candelabra: slender) blocks walking only.
        /// </summary>
        public static GreyboxPart PropPart(bool hasCollider, bool stopsShots, bool shelter, bool gameplay)
        {
            if (gameplay) return GreyboxPart.AsItIs;
            if (!hasCollider) return GreyboxPart.Hidden;
            if (stopsShots) return GreyboxPart.HighCover;
            return shelter ? GreyboxPart.LowCover : GreyboxPart.BlocksWalking;
        }

        /// <summary>The colour of a part; white for a part drawn as it is or not at all (it is never asked for those).</summary>
        public static Rgb ColourOf(GreyboxPart part, GreyboxValues v)
        {
            v = v ?? new GreyboxValues();
            switch (part)
            {
                case GreyboxPart.Floor: return v.Floor;
                case GreyboxPart.Wall: return v.Wall;
                case GreyboxPart.Door: return v.Door;
                case GreyboxPart.HighCover: return v.HighCover;
                case GreyboxPart.LowCover: return v.LowCover;
                case GreyboxPart.BlocksWalking: return v.BlocksWalking;
                case GreyboxPart.FightLine: return v.FightLine;
                default: return new Rgb(1f, 1f, 1f);
            }
        }

        /// <summary>The colour's name in the legend, with what the part is.</summary>
        public static string LegendWord(GreyboxPart part)
        {
            switch (part)
            {
                case GreyboxPart.Floor: return "mid grey: floor";
                case GreyboxPart.Wall: return "dark grey: wall";
                case GreyboxPart.Door: return "orange: door";
                case GreyboxPart.HighCover: return "red: high cover (stops shots)";
                case GreyboxPart.LowCover: return "blue: low cover (shoot over, Ctrl ducks)";
                case GreyboxPart.BlocksWalking: return "purple: blocks walking only";
                case GreyboxPart.FightLine: return "cyan line: the room wakes here";
                case GreyboxPart.Hidden: return "no collider: not shown";
                default: return "as it is: enemies, her, chests, drops, signs";
            }
        }

        /// <summary>
        /// The legend in the developer's box (330 layout pixels wide, 12-point text: each line short enough not to wrap), made
        /// once: seven lines.
        /// </summary>
        public static readonly string Legend =
            "GREYBOX   " + LegendWord(GreyboxPart.Floor) + "   " + LegendWord(GreyboxPart.Wall) + "\n"
            + "  " + LegendWord(GreyboxPart.Door) + "   " + LegendWord(GreyboxPart.FightLine) + "\n"
            + "  " + LegendWord(GreyboxPart.HighCover) + "\n"
            + "  " + LegendWord(GreyboxPart.LowCover) + "\n"
            + "  " + LegendWord(GreyboxPart.BlocksWalking) + "\n"
            + "  " + LegendWord(GreyboxPart.Hidden) + "\n"
            + "  " + LegendWord(GreyboxPart.AsItIs);

        /// <summary>The longest a line of the developer's box may be and not wrap at its 330 layout pixels (12-point text).</summary>
        public const int DevBoxLineMost = 48;

        /// <summary>How far apart two colours are: the straight distance of their red, green and blue (0 to about 1.73).</summary>
        public static double Distance(Rgb a, Rgb b)
        {
            double r = a.R - b.R, g = a.G - b.G, bl = a.B - b.B;
            return Math.Sqrt(r * r + g * g + bl * bl);
        }

        /// <summary>
        /// Work order 070's line on a room's floor, seen from the door she comes in by: the stretches of the room's open cells
        /// at <see cref="RoomFightRules.PointDepth"/> in from that door's wall, each from one cell edge to another along the
        /// wall, merged where open cells run on. A cell counts when the cell just past the line (deeper into the room) is open:
        /// the floor she stands on once she is past it. Empty for a room with no door in, and for a passage.
        /// </summary>
        public static List<(LevelPoint a, LevelPoint b)> FightLine(LevelLayout level, LevelRoom room, LevelDoor door, RoomFightValues values)
        {
            var list = new List<(LevelPoint, LevelPoint)>();
            if (level == null || room == null || door == null || values == null || room.Kind == RoomKind.Passage) return list;
            int wall = LevelGeometry.WallOf(room.Rect, door);
            if (wall < 0) return list;
            double cs = level.CellSize, depth = RoomFightRules.PointDepth(level, room, door, values);
            const double past = 1e-3;
            CellRect r = room.Rect;
            bool alongX = wall == LevelGenerator.North || wall == LevelGenerator.South;
            // The line's place across the wall, in metres, and the cell row (or column) just past it.
            double at = wall == LevelGenerator.South ? r.Y * cs + depth : wall == LevelGenerator.North ? r.Top * cs - depth
                      : wall == LevelGenerator.West ? r.X * cs + depth : r.Right * cs - depth;
            double deeper = wall == LevelGenerator.South || wall == LevelGenerator.West ? at + past : at - past;
            int row = (int)Math.Floor(deeper / cs);
            int from = alongX ? r.X : r.Y, to = alongX ? r.Right : r.Top;
            int runFrom = int.MinValue;
            for (int c = from; c <= to; c++)
            {
                bool open = c < to && (alongX ? room.IsOpen(c, row) : room.IsOpen(row, c));
                if (open && runFrom == int.MinValue) runFrom = c;
                if (!open && runFrom != int.MinValue)
                {
                    list.Add(alongX ? (new LevelPoint(runFrom * cs, at), new LevelPoint(c * cs, at)) : (new LevelPoint(at, runFrom * cs), new LevelPoint(at, c * cs)));
                    runFrom = int.MinValue;
                }
            }
            return list;
        }

        /// <summary>
        /// A room in the developer's box (work order 084): its template's name, or the shape it was carved to ("drawn: cross"),
        /// a template that fell back said so; and its size in metres, east to west by north to south.
        /// </summary>
        public static string RoomWords(LevelRoom room, double cellSize)
        {
            if (room == null) return "";
            string what = room.Kind == RoomKind.Passage ? "a passage"
                : room.Template != null && room.Shape == RoomShape.Template ? "template " + room.Template.Template.Name
                : room.Template != null ? "template " + room.Template.Template.Name + " (fell back: " + RoomShapes.Word(room.Shape) + ")"
                : "drawn: " + RoomShapes.Word(room.Shape);
            return string.Format(CultureInfo.InvariantCulture, "{0}, {1:0.#} x {2:0.#} m", what, room.Rect.Width * cellSize, room.Rect.Height * cellSize);
        }
    }
}
