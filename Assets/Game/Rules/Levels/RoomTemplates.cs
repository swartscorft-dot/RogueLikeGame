using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>The room a template is drawn for (work order 058): an ordinary combat room, the elite room, the exit room.</summary>
    public enum RoomTemplateFor { Combat, Elite, Exit }

    /// <summary>
    /// The numbers of the rooms drawn by hand (work order 058; Kristian, 2026-11-20: "we must take more inspiration from
    /// Diablo and Path of Exile 2 in how their generators make and design levels", and on 2026-11-21 he chose "Rom-maler slik
    /// Diablo og PoE gjør", room templates the way Diablo and Path of Exile do it; the research report d's proposal B). Data,
    /// all first guesses. The generator reads them, so changing one changes the floors of old seeds (with the templates on).
    /// </summary>
    [Serializable]
    public sealed class TemplateValues
    {
        public float CombatShare = 0.75f;   // of the row's ordinary combat rooms, about this share take a template; the others keep work order 051's drawn shapes, so the library shows less often (the report's "template fatigue")
        public float MostSolidShare = 0.4f; // of a combat or elite template room's cells at most this share solid, its gate counted (051's drawn shapes keep ShapeValues.MostSolidShare, 0.25; a cross hall is about 37 %, and the open-area rule keeps such a room from playing small); an exit template keeps ShapeValues.ExitMostSolidShare
        public float SuitsWeight = 3f;      // a theme the room's template suits is this many times as likely when the themes are drawn
        public int MostDrawn = 3;           // the most ? cells a template may have: each one doubles the cases the check runs
    }

    /// <summary>
    /// One room drawn by hand (work order 058): its name, its family (no two combat rooms in a row share one), the room it is
    /// for, the first floor of an act it is drawn on, its weight, the themes it suits, whether its far wall may never be the
    /// south one (an exit template: the throne at the far wall would turn its back to the camera), and its grid.
    ///
    /// The grid is written as if she comes in from the bottom, north at the top, one row a line, the cells separated by single
    /// spaces: <c>.</c> open floor; <c>#</c> solid (a lone one is a pier); <c>?</c> solid or open, drawn when the room is
    /// carved (Spelunky's "2"); <c>I</c> open, the door in may open in the wall beside it (bottom row only, never a corner);
    /// <c>O</c> open, the door out may open in the wall beside it (top row or the left or right column, never the bottom row,
    /// never a corner); <c>h</c> open, a perch: shooters and casters are tried here first; <c>e</c> open, the elite is tried
    /// here first (elite templates); <c>A</c> open, the eye-catcher, top row only, at the far wall: the altar, the throne and
    /// the mood's large-prop place stand here first (two side by side mean their middle); <c>c</c> open, cover: a theme's floor
    /// prop is tried here first. Authored cells are (ax, ay), ay = 0 on the bottom row.
    /// </summary>
    public sealed class RoomTemplate
    {
        public readonly string Name;
        public readonly string Family;
        public readonly RoomTemplateFor For;
        public readonly int FromFloor;
        public readonly float Weight;
        public readonly RoomTheme[] Suits;
        public readonly bool FarNotSouth;
        public readonly string Grid;
        public readonly int Width, Height;
        readonly char[] _cells;
        readonly List<(int ax, int ay)> _drawn = new List<(int, int)>();

        /// <summary>The marks a grid may hold.</summary>
        public const string Characters = ".#?IOheAc";

        RoomTemplate(string name, string family, RoomTemplateFor kind, int fromFloor, float weight, RoomTheme[] suits, bool farNotSouth, string grid,
                     int width, int height, char[] cells)
        {
            Name = name;
            Family = family;
            For = kind;
            FromFloor = fromFloor;
            Weight = weight;
            Suits = suits ?? new RoomTheme[0];
            FarNotSouth = farNotSouth;
            Grid = grid;
            Width = width;
            Height = height;
            _cells = cells;
            // The ? cells in the authored order: row by row from the top, west to east.
            for (int ay = height - 1; ay >= 0; ay--)
                for (int ax = 0; ax < width; ax++)
                    if (At(ax, ay) == '?') _drawn.Add((ax, ay));
        }

        /// <summary>The authored cell (ax, ay), ay = 0 the bottom row; '\0' outside the grid.</summary>
        public char At(int ax, int ay) => ax >= 0 && ay >= 0 && ax < Width && ay < Height ? _cells[ay * Width + ax] : '\0';

        /// <summary>The ? cells, in the order they are drawn: row by row from the top, west to east.</summary>
        public IReadOnlyList<(int ax, int ay)> Drawn => _drawn;

        public bool SuitsTheme(RoomTheme theme) => Array.IndexOf(Suits, theme) >= 0;

        /// <summary>The template turned so its bottom is the wall of the door in, mirrored first when asked (see <see cref="TemplatePlacement"/>).</summary>
        public TemplatePlacement Oriented(int inWall, bool mirror) => new TemplatePlacement(this, inWall, mirror);

        public override string ToString() => Name;

        /// <summary>
        /// A template from its grid (see the class); throws a <see cref="FormatException"/> naming the template and the cell
        /// for ragged rows, an unknown character, a mark out of its place, no I, no O in a combat or elite template, an O in an
        /// exit template, or more than <paramref name="mostDrawn"/> ? cells (<see cref="TemplateValues.MostDrawn"/>'s by default).
        /// </summary>
        public static RoomTemplate Parse(string name, string family, RoomTemplateFor kind, int fromFloor, float weight, RoomTheme[] suits, bool farNotSouth,
                                         string grid, int mostDrawn = -1)
        {
            if (mostDrawn < 0) mostDrawn = new TemplateValues().MostDrawn;
            string Fail(string what) => "template " + name + ": " + what;
            var rows = new List<string>();
            foreach (string raw in (grid ?? "").Replace("\r", "").Split('\n'))
            {
                string line = raw.Trim();
                if (line.Length == 0) continue;
                for (int i = 1; i < line.Length; i += 2)
                    if (line[i] != ' ') throw new FormatException(Fail($"row {rows.Count + 1} from the top: its cells are not separated by single spaces at '{line[i]}'"));
                rows.Add(line.Replace(" ", ""));
            }
            if (rows.Count == 0) throw new FormatException(Fail("no rows"));
            int width = rows[0].Length, height = rows.Count;
            for (int r = 1; r < height; r++)
                if (rows[r].Length != width)
                    throw new FormatException(Fail($"row {r + 1} from the top has {rows[r].Length} cells, the first row {width}: ragged rows"));
            var cells = new char[width * height];
            int ins = 0, outs = 0, drawn = 0;
            for (int r = 0; r < height; r++)
                for (int ax = 0; ax < width; ax++)
                {
                    char ch = rows[r][ax];
                    int ay = height - 1 - r;
                    string at = $"cell ({ax},{ay}) (row {r + 1} from the top, column {ax + 1})";
                    if (Characters.IndexOf(ch) < 0) throw new FormatException(Fail($"{at} is '{ch}', an unknown character"));
                    bool corner = (ax == 0 || ax == width - 1) && (ay == 0 || ay == height - 1);
                    switch (ch)
                    {
                        case 'I':
                            if (ay != 0 || corner) throw new FormatException(Fail($"{at}: an I is on the bottom row only, never a corner"));
                            ins++;
                            break;
                        case 'O':
                            if (ay == 0 || corner || !(ay == height - 1 || ax == 0 || ax == width - 1))
                                throw new FormatException(Fail($"{at}: an O is on the top row or the left or right column, never the bottom row, never a corner"));
                            if (kind == RoomTemplateFor.Exit) throw new FormatException(Fail($"{at}: an O in an exit template, which has no door out"));
                            outs++;
                            break;
                        case 'A':
                            if (ay != height - 1) throw new FormatException(Fail($"{at}: an A is on the top row only, at the far wall"));
                            break;
                        case 'e':
                            if (kind != RoomTemplateFor.Elite) throw new FormatException(Fail($"{at}: an e in a template that is not an elite room's"));
                            break;
                        case '?':
                            drawn++;
                            break;
                    }
                    cells[ay * width + ax] = ch;
                }
            if (ins == 0) throw new FormatException(Fail("no I: no place for the door in"));
            if (outs == 0 && kind != RoomTemplateFor.Exit) throw new FormatException(Fail("no O: no place for the door out"));
            if (drawn > mostDrawn) throw new FormatException(Fail($"{drawn} ? cells, more than {mostDrawn}"));
            return new RoomTemplate(name, family, kind, fromFloor, weight, suits, farNotSouth, grid, width, height, cells);
        }
    }

    /// <summary>
    /// A template turned and mirrored for one room (work order 058): the template, the wall of the door in and the mirror. Its
    /// cells are local, measured from the room's south-west corner. Mirrored first (ax becomes W - 1 - ax, the door in stays at
    /// the bottom), then turned so the bottom is the door-in wall: South (ax, ay), W x H; North (W-1-ax, H-1-ay), W x H; West
    /// (ay, W-1-ax), H x W (a quarter turn clockwise); East (H-1-ay, ax), H x W. Its far wall (its A) is the wall across from
    /// where she comes in, the far side the mood and the shrine already use.
    /// </summary>
    public sealed class TemplatePlacement
    {
        public readonly RoomTemplate Template;
        public readonly int InWall;
        public readonly bool Mirror;

        public TemplatePlacement(RoomTemplate template, int inWall, bool mirror)
        {
            Template = template;
            InWall = inWall;
            Mirror = mirror;
        }

        bool Across => InWall == LevelGenerator.East || InWall == LevelGenerator.West;
        public int Width => Across ? Template.Height : Template.Width;
        public int Height => Across ? Template.Width : Template.Height;
        /// <summary>The wall across from the door in: the far wall, its A's.</summary>
        public int FarWall => LevelGenerator.Opposite(InWall);

        /// <summary>The local cell of an authored one.</summary>
        public void Local(int ax, int ay, out int lx, out int ly)
        {
            int w = Template.Width, h = Template.Height;
            int mx = Mirror ? w - 1 - ax : ax;
            switch (InWall)
            {
                case LevelGenerator.North: lx = w - 1 - mx; ly = h - 1 - ay; break;
                case LevelGenerator.West: lx = ay; ly = w - 1 - mx; break;
                case LevelGenerator.East: lx = h - 1 - ay; ly = mx; break;
                default: lx = mx; ly = ay; break;
            }
        }

        /// <summary>The authored cell of a local one.</summary>
        public void Authored(int lx, int ly, out int ax, out int ay)
        {
            int w = Template.Width, h = Template.Height, mx;
            switch (InWall)
            {
                case LevelGenerator.North: mx = w - 1 - lx; ay = h - 1 - ly; break;
                case LevelGenerator.West: ay = lx; mx = w - 1 - ly; break;
                case LevelGenerator.East: ay = h - 1 - lx; mx = ly; break;
                default: mx = lx; ay = ly; break;
            }
            ax = Mirror ? w - 1 - mx : mx;
        }

        /// <summary>The template's character at the local cell (from the room's south-west corner); '\0' outside it.</summary>
        public char MarkAt(int localX, int localY)
        {
            if (localX < 0 || localY < 0 || localX >= Width || localY >= Height) return '\0';
            Authored(localX, localY, out int ax, out int ay);
            return Template.At(ax, ay);
        }

        public override string ToString() => Template.Name + " " + RoomTemplates.WallWord(InWall) + " " + (Mirror ? "mirrored" : "as drawn");
    }

    /// <summary>
    /// Rooms drawn by hand, joined at their doors (work order 058; Kristian's words of 2026-11-20 and 2026-11-21; the research
    /// report d's proposal B, cut to a first version). A template IS a carved shape (<see cref="RoomShape.Template"/>): its solid
    /// cells are the room's, so the walls, the kit, the colliders, the NavMesh, the maps and the fog follow by themselves.
    ///
    /// The choice (<see cref="Draw"/>, on the stream <see cref="Stage"/> of each attempt, before the layout puts the row's
    /// rooms down): the elite room an elite template, the exit room an exit template, and each ordinary combat room one draw of
    /// a number, below <see cref="TemplateValues.CombatShare"/> a combat template; by weight among the usable ones of its kind
    /// from its floor on, never one drawn already on the floor, never the family of the combat room before it on the row's
    /// chain of combat rooms; then a mirror. The join (<see cref="CanJoin"/>, <see cref="DoorAmong"/>): a templated room has its
    /// template's size and stands against the room before it only where an O of the earlier room's template meets an I of the
    /// later one's, and the door is drawn among those places. The stamp (<see cref="Stamp"/>): in the carve stage the template's
    /// # cells, its drawn ? cells and the gate; a stamp that breaks a rule of <see cref="RoomShapes.Faults"/> gives way to work
    /// order 051's carve (<see cref="LevelRoom.TemplateFellBack"/>). The marks (<see cref="MarkAt"/>) are wishes the contents,
    /// the props and the mood try first; nothing ever stands against a rule for them.
    /// </summary>
    public static class RoomTemplates
    {
        public const string Stage = "templates";

        public static string WallWord(int side) =>
            side == LevelGenerator.North ? "north" : side == LevelGenerator.East ? "east" : side == LevelGenerator.South ? "south" : "west";

        static double Unit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));

        // ---------------- The choice ----------------

        /// <summary>
        /// The templates of a row's rooms (see the class), one for each of <paramref name="kinds"/> (null for none: the entrance,
        /// the treasure rooms, a combat room that drew no template or found none left), and a mirror for each. The elite room is
        /// combat room number <paramref name="eliteNumber"/>. The draws are made in the same order every time.
        /// </summary>
        public static void Draw(List<RoomKind> kinds, int eliteNumber, int floor, LevelRandom rng, LevelValues v,
                                out List<RoomTemplate> templates, out List<bool> mirrors)
        {
            TemplateValues tv = v.TemplateRules ?? new TemplateValues();
            IReadOnlyList<RoomTemplate> usable = RoomTemplateLibrary.Usable(v);
            templates = new List<RoomTemplate>();
            mirrors = new List<bool>();
            var drawn = new HashSet<RoomTemplate>();
            string familyBefore = null;
            int combat = 0;
            foreach (RoomKind kind in kinds)
            {
                RoomTemplateFor? want = null;
                if (kind == RoomKind.Combat)
                {
                    combat++;
                    if (combat == eliteNumber) want = RoomTemplateFor.Elite;
                    else if (Unit(rng) < tv.CombatShare) want = RoomTemplateFor.Combat;
                }
                else if (kind == RoomKind.Exit) want = RoomTemplateFor.Exit;
                RoomTemplate t = want.HasValue ? Pick(usable, want.Value, floor, drawn, kind == RoomKind.Combat ? familyBefore : null, rng) : null;
                bool mirror = false;
                if (t != null)
                {
                    drawn.Add(t);
                    mirror = rng.Below(2) == 1;
                }
                if (kind == RoomKind.Combat) familyBefore = t?.Family;
                templates.Add(t);
                mirrors.Add(mirror);
            }
        }

        static RoomTemplate Pick(IReadOnlyList<RoomTemplate> usable, RoomTemplateFor want, int floor, HashSet<RoomTemplate> drawn, string familyBefore, LevelRandom rng)
        {
            var options = usable.Where(t => t.For == want && t.FromFloor <= floor && t.Weight > 0f && !drawn.Contains(t)
                                            && (familyBefore == null || t.Family != familyBefore)).ToList();
            if (options.Count == 0) return null;
            double total = options.Sum(t => (double)t.Weight), pick = Unit(rng) * total;
            foreach (RoomTemplate t in options)
            {
                if (pick < t.Weight) return t;
                pick -= t.Weight;
            }
            return options[options.Count - 1];
        }

        // ---------------- The join ----------------

        /// <summary>
        /// The places a door may open between an earlier room and the next (work order 058): each From on the stretch the two
        /// rectangles share, a wall cell either side (as <c>LevelGenerator.DoorBetween</c> keeps), where every cell of the
        /// opening on the earlier room's side is an O of its template and on the later room's an I of its (any cell of a room
        /// without one). Empty when they share no wall.
        /// </summary>
        public static List<int> JoinPlaces(CellRect earlier, TemplatePlacement earlierTemplate, CellRect later, TemplatePlacement laterTemplate, int width)
        {
            var list = new List<int>();
            if (!LevelGeometry.SharedWall(earlier, later, out bool vertical, out int line, out int lo, out int hi)) return list;
            for (int from = lo + 1; from <= hi - 1 - width; from++)
            {
                var door = new LevelDoor { Vertical = vertical, Line = line, From = from, Width = width };
                bool ok = true;
                foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in door.CellPairs())
                {
                    bool keyEarlier = earlier.Contains(pair.Key.x, pair.Key.y);
                    (int x, int y) a = keyEarlier ? pair.Key : pair.Value, b = keyEarlier ? pair.Value : pair.Key;
                    if (earlierTemplate != null && earlierTemplate.MarkAt(a.x - earlier.X, a.y - earlier.Y) != 'O') ok = false;
                    if (laterTemplate != null && laterTemplate.MarkAt(b.x - later.X, b.y - later.Y) != 'I') ok = false;
                    if (!ok) break;
                }
                if (ok) list.Add(from);
            }
            return list;
        }

        /// <summary>
        /// The join check (work order 058): some place on the shared stretch where the earlier room's O meets the later one's I
        /// (see <see cref="JoinPlaces"/>). Work order 059 widened it to rooms that stand apart: with <paramref name="gap"/> above
        /// 0 the later room stands that many cells off the earlier one's side (<paramref name="side"/>, the earlier room's wall
        /// toward it), and the two must hold a passage's two ends: a door place of the earlier room's O cells on that wall and one
        /// of the later room's I cells on the wall across, on the stretch where they face each other (<see cref="DoorPlaces"/>),
        /// that a passage of that depth can join (<see cref="Passages.Joinable"/>: the same place when it may run straight, one
        /// to <see cref="PassageValues.JogMost"/> apart when it may jog).
        /// </summary>
        public static bool CanJoin(CellRect earlier, TemplatePlacement earlierTemplate, CellRect later, TemplatePlacement laterTemplate, int width,
                                   int gap = 0, int side = -1, PassageValues passages = null)
        {
            if (gap > 0)
            {
                if (!Passages.Facing(earlier, later, side, out int lo, out int hi)) return false;
                return Passages.Joinable(DoorPlaces(earlier, earlierTemplate, side, lo, hi, 'O', width),
                                         DoorPlaces(later, laterTemplate, LevelGenerator.Opposite(side), lo, hi, 'I', width), gap, passages);
            }
            if (earlierTemplate == null && laterTemplate == null)
                return LevelGeometry.SharedWall(earlier, later, out _, out _, out int lo, out int hi) && hi - lo >= width + 2;
            return JoinPlaces(earlier, earlierTemplate, later, laterTemplate, width).Count > 0;
        }

        /// <summary>
        /// The places a door may open in one room's wall (work order 059, the join of work order 058 widened for the passages):
        /// each From on the stretch [<paramref name="lo"/>, <paramref name="hi"/>) of its wall <paramref name="side"/> with a
        /// wall cell either side of the opening (as <c>LevelGenerator.DoorBetween</c> keeps), where every cell of the opening
        /// inside the room is <paramref name="mark"/> of its template (<see cref="DoorOnMark"/>; any cell of a room without one).
        /// In increasing order.
        /// </summary>
        public static List<int> DoorPlaces(CellRect room, TemplatePlacement template, int side, int lo, int hi, char mark, int width)
        {
            var list = new List<int>();
            for (int from = lo + 1; from <= hi - 1 - width; from++)
                if (DoorOnMark(room, template, side, from, width, mark)) list.Add(from);
            return list;
        }

        /// <summary>Every cell of an opening at <paramref name="from"/> in the room's wall of this side, on the room's side, is the mark of its template; true without a template (work order 059).</summary>
        public static bool DoorOnMark(CellRect room, TemplatePlacement template, int side, int from, int width, char mark)
        {
            if (template == null) return true;
            for (int k = 0; k < width; k++)
            {
                int c = from + k, x, y;
                switch (side)
                {
                    case LevelGenerator.North: x = c; y = room.Top - 1; break;
                    case LevelGenerator.South: x = c; y = room.Y; break;
                    case LevelGenerator.East: x = room.Right - 1; y = c; break;
                    default: x = room.X; y = c; break;
                }
                if (template.MarkAt(x - room.X, y - room.Y) != mark) return false;
            }
            return true;
        }

        /// <summary>A door between a room and the next drawn among the places the join allows (the same draw as <c>DoorBetween</c>'s); null when there is none.</summary>
        public static LevelDoor DoorAmong(LevelRandom rng, LevelRoom first, LevelRoom second, int width)
        {
            List<int> places = JoinPlaces(first.Rect, first.Template, second.Rect, second.Template, width);
            if (places.Count == 0) return null;
            LevelGeometry.SharedWall(first.Rect, second.Rect, out bool vertical, out int line, out _, out _);
            return new LevelDoor
            {
                RoomA = Math.Min(first.Order, second.Order),
                RoomB = Math.Max(first.Order, second.Order),
                Vertical = vertical,
                Line = line,
                From = places[rng.Below(places.Count)],
                Width = width,
            };
        }

        // ---------------- The stamp ----------------

        /// <summary>
        /// Stamps a room's template in place of a drawn shape (work order 058): its cells cleared, every # solid, each ? drawn
        /// in the template's authored order (solid on 0 of <c>rng.Below(2)</c>), the gate (<see cref="RoomShapes.Gate"/>). True
        /// when <see cref="RoomShapes.Faults"/> finds nothing (its shape is then <see cref="RoomShape.Template"/>); else its cells
        /// are cleared, it is marked <see cref="LevelRoom.TemplateFellBack"/> and false: work order 051's carve follows.
        /// </summary>
        public static bool Stamp(LevelLayout level, LevelRoom room, LevelRandom rng, LevelValues values)
        {
            TemplatePlacement o = room.Template;
            room.ClearSolid();
            for (int ly = 0; ly < o.Height; ly++)
                for (int lx = 0; lx < o.Width; lx++)
                    if (o.MarkAt(lx, ly) == '#') room.SetSolid(room.Rect.X + lx, room.Rect.Y + ly, true);
            foreach ((int ax, int ay) in o.Template.Drawn)
            {
                if (rng.Below(2) != 0) continue;
                o.Local(ax, ay, out int lx, out int ly);
                room.SetSolid(room.Rect.X + lx, room.Rect.Y + ly, true);
            }
            RoomShapes.Gate(level, room);
            room.Shape = RoomShape.Template;
            if (RoomShapes.Faults(level, room, values, null) == 0) return true;
            room.ClearSolid();
            room.Shape = RoomShape.None;
            room.TemplateFellBack = true;
            return false;
        }

        // ---------------- The rules of a template room ----------------

        /// <summary>The share of a template room's cells that may be solid (work order 058): <see cref="TemplateValues.MostSolidShare"/>, the exit room's <see cref="ShapeValues.ExitMostSolidShare"/>.</summary>
        public static float ShareOf(LevelRoom room, LevelValues values) =>
            room.Kind == RoomKind.Exit ? (values?.ShapeRules ?? new ShapeValues()).ExitMostSolidShare : (values?.TemplateRules ?? new TemplateValues()).MostSolidShare;

        /// <summary>
        /// The open-area rule of a combat or elite template room (work order 058), or null: it keeps between the plan's smallest
        /// and largest combat room's cells open (<see cref="LevelPlan.CombatSize"/>, 25 to 49), so it plays no smaller and no
        /// larger than the combat rooms of before. Work order 059: <paramref name="mouth"/> open cells not counted, the cells beside
        /// the door in that a gate takes on a floor without passages and a passage's mouth leaves open on a floor with them.
        /// </summary>
        public static string AreaFault(LevelRoom room, LevelValues values, int mouth = 0)
        {
            if (room.Shape != RoomShape.Template || room.Kind == RoomKind.Exit) return null;
            RoomSizeRange size = (values?.Plan ?? new LevelPlan()).CombatSize;
            int open = room.Rect.Width * room.Rect.Height - room.SolidCount - Math.Max(0, mouth), fewest = size.Min * size.Min, most = size.Max * size.Max;
            return open < fewest || open > most ? $"keeps {open} open cells, and a template room {fewest} to {most}" : null;
        }

        /// <summary>
        /// The floor's template rooms against the rules of work order 058, added to <paramref name="faults"/>: each rectangle its
        /// template's turned size; its kind the template's; its door in beside an I and its door out beside an O; stamped
        /// (<see cref="RoomShape.Template"/>), every # solid and every solid cell a #, a ? or a cell of its gate; no two
        /// neighbouring combat rooms of the row of one family; no template twice on a floor.
        /// </summary>
        public static void Check(LevelLayout level, List<string> faults)
        {
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            var seen = new Dictionary<RoomTemplate, LevelRoom>();
            foreach (LevelRoom r in level.Rooms)
            {
                TemplatePlacement o = r.Template;
                if (o == null) continue;
                string name = Name(r) + " (template " + o.Template.Name + ")";
                if (seen.TryGetValue(o.Template, out LevelRoom first)) faults.Add($"{name} takes the template of {Name(first)} again");
                else seen[o.Template] = r;
                if (r.Rect.Width != o.Width || r.Rect.Height != o.Height) faults.Add($"{name} is {r.Rect.Width}x{r.Rect.Height}, and its template turned {o.Width}x{o.Height}");
                bool kindOk = o.Template.For == RoomTemplateFor.Exit ? r.Kind == RoomKind.Exit
                            : r.Kind == RoomKind.Combat && r.Elite == (o.Template.For == RoomTemplateFor.Elite);
                if (!kindOk) faults.Add($"{name} is a {(r.Elite ? "elite" : r.Kind.ToString())} room, and its template is for a {o.Template.For} room");
                void MarkBeside(LevelDoor d, char mark, string what)
                {
                    if (d == null) return;
                    foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                    {
                        (int x, int y) c = r.Rect.Contains(pair.Key.x, pair.Key.y) ? pair.Key : pair.Value;
                        if (o.MarkAt(c.x - r.Rect.X, c.y - r.Rect.Y) != mark) faults.Add($"{name}'s {what} at {c} is not beside an {mark} of its template");
                    }
                }
                MarkBeside(LevelGeometry.EntryDoor(level, r), 'I', "door in");
                MarkBeside(LevelGeometry.ExitDoor(level, r), 'O', "door out");
                if (r.Shape == RoomShape.Template)
                {
                    var gate = new HashSet<(int, int)>(RoomShapes.GateCells(level, r));
                    for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                        for (int x = r.Rect.X; x < r.Rect.Right; x++)
                        {
                            char m = o.MarkAt(x - r.Rect.X, y - r.Rect.Y);
                            bool solid = r.IsSolid(x, y);
                            if (m == '#' && !solid) faults.Add($"{name}'s cell ({x},{y}) is a # of its template and open");
                            if (solid && m != '#' && m != '?' && !gate.Contains((x, y))) faults.Add($"{name}'s cell ({x},{y}) is solid, and a '{m}' of its template, not its gate's");
                        }
                }
            }
            List<LevelRoom> chain = LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();
            for (int i = 1; i < chain.Count; i++)
                if (chain[i].Template != null && chain[i - 1].Template != null && chain[i].Template.Template.Family == chain[i - 1].Template.Template.Family)
                    faults.Add($"{Name(chain[i - 1])} and {Name(chain[i])}, next to each other on the chain, are both of the template family {chain[i].Template.Template.Family}");
        }

        // ---------------- The check of one template ----------------

        /// <summary>
        /// Every rule a template breaks turned to <paramref name="inWall"/> and mirrored or not (work order 058), added to
        /// <paramref name="faults"/> (when given); the count. It builds a small floor round the room with nothing else in it, a
        /// 3 x 3 room before it against the wall of a door-in mark and one after it against the wall of a door-out mark (none
        /// after an exit template), each with a wall cell on either side of the door, stamps the template with the gate and runs
        /// <see cref="RoomShapes.Faults"/> on it with its shape <see cref="RoomShape.Template"/>, with no filling: for every I,
        /// every O and every draw of the ? cells. Besides, every h, e, A and c mark and the two door marks used stand on cells
        /// open after the gate (the I marks beside the one used are its gate), and no more than <see cref="TemplateValues.MostDrawn"/>
        /// ? cells.
        /// </summary>
        public static int Faults(RoomTemplate t, int inWall, bool mirror, LevelValues v, List<string> faults)
        {
            v = v ?? new LevelValues();
            int count = 0;
            string label = t.Name + " (in " + WallWord(inWall) + (mirror ? ", mirrored" : "") + ")";
            void Add(string f)
            {
                count++;
                faults?.Add(label + " " + f);
            }
            int mostDrawn = (v.TemplateRules ?? new TemplateValues()).MostDrawn;
            if (t.Drawn.Count > mostDrawn) Add($"has {t.Drawn.Count} ? cells, more than {mostDrawn}");
            int width = Math.Max(1, v.DoorWidth);
            TemplatePlacement o = t.Oriented(inWall, mirror);
            var rect = new CellRect(10, 10, o.Width, o.Height);

            // The door marks: each place along a wall where the door's cells are all the mark.
            List<(int side, int along)> Doors(char mark, bool onInWall)
            {
                var list = new List<(int, int)>();
                foreach (int side in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                {
                    if ((side == inWall) != onInWall) continue;
                    int length = side == LevelGenerator.North || side == LevelGenerator.South ? o.Width : o.Height;
                    for (int a = 0; a + width <= length; a++)
                    {
                        bool all = true;
                        for (int k = 0; k < width && all; k++)
                        {
                            Cell(side, a + k, out int lx, out int ly);
                            all = o.MarkAt(lx, ly) == mark;
                        }
                        if (all) list.Add((side, a));
                    }
                }
                return list;
            }
            void Cell(int side, int along, out int lx, out int ly)
            {
                switch (side)
                {
                    case LevelGenerator.North: lx = along; ly = o.Height - 1; break;
                    case LevelGenerator.South: lx = along; ly = 0; break;
                    case LevelGenerator.East: lx = o.Width - 1; ly = along; break;
                    default: lx = 0; ly = along; break;
                }
            }
            List<(int side, int along)> ins = Doors('I', true), outs = Doors('O', false);
            if (ins.Count == 0) Add("has no door-in mark on the wall she comes in by");
            if (outs.Count == 0 && t.For != RoomTemplateFor.Exit) Add("has no door-out mark");
            var outList = t.For == RoomTemplateFor.Exit ? new List<(int side, int along)?> { null } : outs.Select(x => ((int, int)?)x).ToList();
            int draws = 1 << Math.Min(16, t.Drawn.Count);
            for (int bits = 0; bits < draws; bits++)
                foreach ((int side, int along) i in ins)
                    foreach ((int side, int along)? x in outList)
                    {
                        var level = new LevelLayout
                        {
                            Seed = 0, Version = LevelGenerator.Version, GridCells = v.GridCells, CellSize = v.CellSize, Attempt = 1, Floor = 1,
                            InALine = true, Shapes = true, Templates = true,
                        };
                        level.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = Beside(rect, i.side, i.along, width), Order = 0 });
                        var room = new LevelRoom
                        {
                            Kind = t.For == RoomTemplateFor.Exit ? RoomKind.Exit : RoomKind.Combat, Elite = t.For == RoomTemplateFor.Elite,
                            Rect = rect, Order = 1, Template = o,
                        };
                        level.Rooms.Add(room);
                        level.Doors.Add(DoorOn(rect, i.side, i.along, width, 0, 1));
                        if (x.HasValue)
                        {
                            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = Beside(rect, x.Value.side, x.Value.along, width), Order = 2 });
                            level.Doors.Add(DoorOn(rect, x.Value.side, x.Value.along, width, 1, 2));
                        }
                        for (int ly = 0; ly < o.Height; ly++)
                            for (int lx = 0; lx < o.Width; lx++)
                                if (o.MarkAt(lx, ly) == '#') room.SetSolid(rect.X + lx, rect.Y + ly, true);
                        for (int q = 0; q < t.Drawn.Count; q++)
                        {
                            if ((bits & (1 << q)) == 0) continue;
                            o.Local(t.Drawn[q].ax, t.Drawn[q].ay, out int lx, out int ly);
                            room.SetSolid(rect.X + lx, rect.Y + ly, true);
                        }
                        RoomShapes.Gate(level, room);
                        room.Shape = RoomShape.Template;
                        string drawn = t.Drawn.Count == 0 ? "" : " ? drawn " + Convert.ToString(bits, 2).PadLeft(t.Drawn.Count, '0') + " (1 solid),";
                        string where = $"with the door in at {WallWord(i.side)} {i.along}" + (x.HasValue ? $" and out at {WallWord(x.Value.side)} {x.Value.along}" : "") + "," + drawn;
                        var broken = new List<string>();
                        RoomShapes.Faults(level, room, v, broken);
                        foreach (string f in broken) Add(where + " " + f);
                        for (int ly = 0; ly < o.Height; ly++)
                            for (int lx = 0; lx < o.Width; lx++)
                            {
                                char m = o.MarkAt(lx, ly);
                                if ("heAc".IndexOf(m) >= 0 && room.IsSolid(rect.X + lx, rect.Y + ly))
                                    Add($"{where} has its mark {m} at local cell ({lx},{ly}) on a solid cell");
                            }
                        foreach ((int side, int along) d in x.HasValue ? new[] { i, x.Value } : new[] { i })
                            for (int k = 0; k < width; k++)
                            {
                                Cell(d.side, d.along + k, out int lx, out int ly);
                                if (room.IsSolid(rect.X + lx, rect.Y + ly)) Add($"{where} has its door's mark at local cell ({lx},{ly}) on a solid cell");
                            }
                    }
            return count;
        }

        /// <summary>A 3 x 3 room (as wide as the door and a wall cell either side) against a wall of the rectangle, centred on the door at <paramref name="along"/>.</summary>
        static CellRect Beside(CellRect r, int side, int along, int width)
        {
            int n = width + 2;
            switch (side)
            {
                case LevelGenerator.North: return new CellRect(r.X + along - 1, r.Top, n, 3);
                case LevelGenerator.South: return new CellRect(r.X + along - 1, r.Y - 3, n, 3);
                case LevelGenerator.East: return new CellRect(r.Right, r.Y + along - 1, 3, n);
                default: return new CellRect(r.X - 3, r.Y + along - 1, 3, n);
            }
        }

        static LevelDoor DoorOn(CellRect r, int side, int along, int width, int a, int b)
        {
            bool vertical = side == LevelGenerator.East || side == LevelGenerator.West;
            int line = side == LevelGenerator.North ? r.Top : side == LevelGenerator.South ? r.Y : side == LevelGenerator.East ? r.Right : r.X;
            return new LevelDoor { RoomA = a, RoomB = b, Vertical = vertical, Line = line, From = (vertical ? r.Y : r.X) + along, Width = width };
        }

        // ---------------- The marks ----------------

        /// <summary>The template's character at a world cell of a templated room; '\0' for a room without one or a cell outside it.</summary>
        public static char MarkAt(LevelRoom room, int x, int y) =>
            room?.Template == null || !room.Rect.Contains(x, y) ? '\0' : room.Template.MarkAt(x - room.Rect.X, y - room.Rect.Y);

        /// <summary>The mark at the cell under a point.</summary>
        public static char MarkAt(LevelRoom room, LevelPoint p, double cs) => MarkAt(room, (int)Math.Floor(p.X / cs), (int)Math.Floor(p.Y / cs));

        /// <summary>The mark a group is tried at first (work order 058): the elite e, shooters and casters h; '\0' for others and in a room without a template.</summary>
        public static char MarkFor(LevelRoom room, EnemyGroup g)
        {
            if (room?.Template == null || g == null || g.Boss) return '\0';
            if (g.Elite) return 'e';
            return g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster ? 'h' : '\0';
        }

        /// <summary>The middles of a templated room's c cells, south to north and west to east (work order 058: cover a theme's floor props try first); empty without a template.</summary>
        public static List<LevelPoint> CoverPlaces(LevelRoom room, double cs)
        {
            var list = new List<LevelPoint>();
            if (room?.Template == null) return list;
            for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                for (int x = room.Rect.X; x < room.Rect.Right; x++)
                    if (MarkAt(room, x, y) == 'c' && room.IsOpen(x, y)) list.Add(new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs));
            return list;
        }

        /// <summary>
        /// Where along its far wall a templated room's eye-catcher stands (work order 058): the middle of its A cells, in metres
        /// along the wall (x for a north or south wall, y for an east or west one); false without a template or an A.
        /// </summary>
        public static bool FarMark(LevelRoom room, double cs, out int side, out double along)
        {
            side = -1;
            along = 0;
            if (room?.Template == null) return false;
            side = room.Template.FarWall;
            double sum = 0;
            int n = 0;
            for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                for (int x = room.Rect.X; x < room.Rect.Right; x++)
                    if (MarkAt(room, x, y) == 'A')
                    {
                        sum += side == LevelGenerator.North || side == LevelGenerator.South ? (x + 0.5) * cs : (y + 0.5) * cs;
                        n++;
                    }
            if (n == 0) return false;
            along = sum / n;
            return true;
        }

        /// <summary>The far mark's place along <paramref name="side"/>, when that is the templated room's far wall; else null.</summary>
        public static double? FarMarkAlong(LevelRoom room, int side, double cs) =>
            FarMark(room, cs, out int far, out double along) && far == side ? along : (double?)null;

        /// <summary>A theme's weight in a room (work order 058): times <see cref="TemplateValues.SuitsWeight"/> when its template suits it, else 1.</summary>
        public static float SuitsFactor(LevelRoom room, RoomTheme theme, LevelValues v) =>
            room?.Template != null && room.Template.Template.SuitsTheme(theme) ? (v?.TemplateRules ?? new TemplateValues()).SuitsWeight : 1f;

        /// <summary>A templated room's template in words, for the level's description: " template name in-wall mirror"; empty for others.</summary>
        public static string Describe(LevelRoom room) =>
            room?.Template == null ? "" : " template " + room.Template.Template.Name + " " + WallWord(room.Template.InWall) + " " + (room.Template.Mirror ? "mirrored" : "as-drawn");

        /// <summary>The map's legend of the templates (work order 058): "C2 nave, L4 cross-hall"; empty when no room shows one.</summary>
        public static string Legend(LevelLayout level)
        {
            var sb = new StringBuilder();
            foreach (LevelRoom r in level.Rooms)
            {
                if (r.Template == null || r.Shape != RoomShape.Template) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(r.Letter).Append(r.Order.ToString(CultureInfo.InvariantCulture)).Append(' ').Append(r.Template.Template.Name);
            }
            return sb.ToString();
        }
    }
}
