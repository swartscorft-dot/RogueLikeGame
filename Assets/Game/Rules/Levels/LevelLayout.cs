using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The kinds of room of a floor (decision 0008, 2026-11-19; work order 012 renamed them from Start, Reward and Boss):
    /// the entrance, the combat rooms, the treasure rooms (small rooms, a chest in each: off the sides of combat rooms
    /// until work order 041, in the row of rooms since) and the exit room (the floor's hardest fight; on the act's last
    /// floor it holds the act boss). Work order 059 (Kristian's words of 2026-11-20, "bedre bruk av mappet ... litt dungen
    /// crawler følelse", and his choice of 2026-11-21, "Ganger mellom rommene"; the research report d's proposal C): a passage,
    /// one cell wide, between two rooms of the row that stand apart (<see cref="Passages"/>), at the end so the numbers of the
    /// others stay.
    /// </summary>
    public enum RoomKind { Entrance, Combat, Treasure, Exit, Passage }

    /// <summary>A rectangle of whole cells: X and Y are its lowest cell (west and south), Width and Height its size.</summary>
    public struct CellRect : IEquatable<CellRect>
    {
        public int X, Y, Width, Height;

        public CellRect(int x, int y, int width, int height)
        {
            X = x;
            Y = y;
            Width = width;
            Height = height;
        }

        /// <summary>The line just past its east side.</summary>
        public int Right => X + Width;
        /// <summary>The line just past its north side.</summary>
        public int Top => Y + Height;

        public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Top;
        public bool Overlaps(CellRect o) => X < o.Right && o.X < Right && Y < o.Top && o.Y < Top;
        /// <summary>True when the two overlap or meet, along a side or at a corner.</summary>
        public bool Touches(CellRect o) => X <= o.Right && o.X <= Right && Y <= o.Top && o.Y <= Top;

        public bool Equals(CellRect o) => X == o.X && Y == o.Y && Width == o.Width && Height == o.Height;
        public override bool Equals(object obj) => obj is CellRect o && Equals(o);
        public override int GetHashCode() => ((X * 397 ^ Y) * 397 ^ Width) * 397 ^ Height;
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0},{1} {2}x{3})", X, Y, Width, Height);
    }

    /// <summary>A point on the level's ground, in metres from the grid's south-west corner: X east, Y north.</summary>
    public struct LevelPoint
    {
        public double X, Y;

        public LevelPoint(double x, double y)
        {
            X = x;
            Y = y;
        }

        public double DistanceTo(LevelPoint o) => Math.Sqrt((X - o.X) * (X - o.X) + (Y - o.Y) * (Y - o.Y));
        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:R},{1:R})", X, Y);
    }

    /// <summary>
    /// What stands at one enemy point (work order 008): a kind, and how many. Several swarmers are a pack and stand
    /// round the point; since work order 012 several shooters or brutes are a line and stand side by side across it
    /// (<see cref="LevelGeometry.MemberPlaces"/>). <see cref="Boss"/> marks the act boss (since work order 014 of the
    /// kind <see cref="EnemyKind.Boss"/>, a guard before, with <see cref="LevelValues.BossHealth"/>), in the exit room of
    /// the act's last floor. <see cref="Elite"/> marks the elite of an elite room (work order 017): one enemy of a kind the
    /// floor can have, tougher and larger (<see cref="LevelValues.EliteHealthScale"/> and the rest), e on a map.
    /// </summary>
    [Serializable]
    public sealed class EnemyGroup
    {
        public EnemyKind Kind;
        public int Count = 1;
        public bool Boss;
        public bool Elite;

        public EnemyGroup() { }

        public EnemyGroup(EnemyKind kind, int count = 1, bool boss = false, bool elite = false)
        {
            Kind = kind;
            Count = count;
            Boss = boss;
            Elite = elite;
        }

        /// <summary>Several swarmers round the point.</summary>
        public bool IsPack => Count > 1 && Kind == EnemyKind.Swarmer;
        /// <summary>Several of another kind side by side across the point (work order 012).</summary>
        public bool IsLine => Count > 1 && Kind != EnemyKind.Swarmer;
        public EnemyGroup Copy() => new EnemyGroup(Kind, Count, Boss, Elite);
        /// <summary>On a map: the kind's small letter (<see cref="EnemyKinds.Letter"/>, p for a pack); a line its capital (H shooters, B brutes); the act boss G; an elite e (work order 017).</summary>
        public char Letter => Boss ? 'G' : Elite ? 'e' : IsLine ? char.ToUpperInvariant(EnemyKinds.Letter(Kind, 1)) : EnemyKinds.Letter(Kind, Count);
        public override string ToString() =>
            (Boss ? "act boss " : "") + (Elite ? "elite " : "") + EnemyKinds.Word(Kind) + (Count > 1 ? " x" + Count.ToString(CultureInfo.InvariantCulture) : "");
    }

    /// <summary>One room: its kind, its cells and its place in the order of the run, and what stands in it.</summary>
    public sealed class LevelRoom
    {
        public RoomKind Kind;
        public CellRect Rect;
        /// <summary>Its place in the run, from 0 (the start room); the level's list of rooms is in this order.</summary>
        public int Order;
        /// <summary>
        /// The floor's elite room (work order 017): a combat room, the middle one of the chain (<see cref="LevelPlan.EliteRoomNumber"/>),
        /// that holds what a combat room holds and an elite besides. L on a map.
        /// </summary>
        public bool Elite;
        /// <summary>
        /// What the room is (work order 028, <see cref="RoomThemes"/>): which props it favours and where they stand. None for
        /// the entrance, and for every room with <see cref="LevelValues.Themes"/> or <see cref="LevelValues.Props"/> off.
        /// </summary>
        public RoomTheme Theme;
        /// <summary>Where the player starts. Only in the entrance.</summary>
        public LevelPoint? PlayerStart;
        public readonly List<LevelPoint> EnemyPoints = new List<LevelPoint>();
        /// <summary>What stands at each enemy point, in the same order as <see cref="EnemyPoints"/> (work order 008).</summary>
        public readonly List<EnemyGroup> EnemyGroups = new List<EnemyGroup>();
        /// <summary>
        /// Where each enemy of each point stands (work order 012), in the same order as <see cref="EnemyPoints"/>: one
        /// place for one enemy, a ring for a pack, a row for a line (<see cref="LevelGeometry.MemberPlaces"/>).
        /// </summary>
        public readonly List<LevelPoint[]> EnemyPlaces = new List<LevelPoint[]>();

        /// <summary>Where the enemies of point <paramref name="i"/> stand; the point itself when the room says nothing (a hand-made layout).</summary>
        public LevelPoint[] PlacesAt(int i) =>
            i >= 0 && i < EnemyPlaces.Count && EnemyPlaces[i] != null ? EnemyPlaces[i] : new[] { EnemyPoints[i] };

        /// <summary>The enemies in the room, every group counted whole.</summary>
        public int EnemyCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < EnemyPoints.Count; i++) n += GroupAt(i).Count;
                return n;
            }
        }
        /// <summary>The props that stand in it (work order 009), in the order they were placed.</summary>
        public readonly List<LevelProp> Props = new List<LevelProp>();
        /// <summary>The generator's notes, not part of the level: how many props it meant to put here, and how many of those it left out with a place refused by the clear lane.</summary>
        public int PropsWanted, PropsLeftOutByLane;
        /// <summary>
        /// The generator's notes, not part of the level (work order 088): props that took a later place because the joined floor
        /// refused an earlier one (moved), props left out with a place it refused (dropped), and the tall and low covers it added for
        /// the room's fight.
        /// </summary>
        public int PropsMovedForFloor, PropsDroppedForFloor, CoverTallAdded, CoverLowAdded;

        /// <summary>What stands at enemy point <paramref name="i"/>; a single brute when the room says nothing (a hand-made layout).</summary>
        public EnemyGroup GroupAt(int i) => i >= 0 && i < EnemyGroups.Count && EnemyGroups[i] != null ? EnemyGroups[i] : new EnemyGroup(EnemyKind.Brute);

        /// <summary>The letter for its kind on a map; L for the elite room (work order 017).</summary>
        public char Letter => Elite ? 'L' : KindLetter(Kind);

        /// <summary>E entrance, C combat, T treasure, X exit (work order 012; S, C, R and B before), P a passage (work order 059). The elite room, a combat room, is L.</summary>
        public static char KindLetter(RoomKind kind) =>
            kind == RoomKind.Entrance ? 'E' : kind == RoomKind.Combat ? 'C' : kind == RoomKind.Treasure ? 'T' : kind == RoomKind.Passage ? 'P' : 'X';

        public LevelPoint Middle(double cellSize) =>
            new LevelPoint((Rect.X + Rect.Width * 0.5) * cellSize, (Rect.Y + Rect.Height * 0.5) * cellSize);

        /// <summary>
        /// The outline its rectangle is carved to (work order 051, <see cref="RoomShapes"/>): None for a room no shape was drawn
        /// for (every room with <see cref="LevelValues.Shapes"/> off, and a hand-made layout). Its solid cells are
        /// <see cref="IsSolid"/>; <see cref="LevelLayout.RoomAtCell"/> answers no room for them.
        /// </summary>
        public RoomShape Shape;
        /// <summary>The generator's notes, not part of the level (work order 051): draws of its shape that broke a rule, and whether it fell back to its gate alone.</summary>
        public int ShapeRedraws;
        public bool ShapeFellBack;
        /// <summary>
        /// The template the room was drawn by, turned and mirrored (work order 058, <see cref="RoomTemplates"/>): it gave the room
        /// its size and where its doors are, and the carve stage stamps it (<see cref="RoomShape.Template"/>). Null for a room
        /// without one (every room with <see cref="LevelValues.Templates"/> off).
        /// </summary>
        public TemplatePlacement Template;
        /// <summary>The generator's note, not part of the level (work order 058): its template's stamp broke a rule, and it was carved by work order 051's draws instead.</summary>
        public bool TemplateFellBack;

        bool[] _solid;
        int _solidCount;
        (int x, int y)[] _solidList;
        CellRect _solidFor; // the rectangle the cells were marked in
        /// <summary>What <see cref="RoomShapes"/> worked out of its solid cells (its carved faces), dropped when they change.</summary>
        internal object ShapeCache;

        /// <summary>The cell is in its rectangle and solid: carved out of the room (work order 051).</summary>
        public bool IsSolid(int x, int y) =>
            _solid != null && Rect.Contains(x, y) && _solidFor.Contains(x, y) && _solid[(y - _solidFor.Y) * _solidFor.Width + (x - _solidFor.X)];

        /// <summary>The cell is in its rectangle and not solid: the room's floor.</summary>
        public bool IsOpen(int x, int y) => Rect.Contains(x, y) && !IsSolid(x, y);

        /// <summary>How many of its cells are solid.</summary>
        public int SolidCount => _solidCount;

        /// <summary>Its solid cells, south to north and west to east in each row.</summary>
        public (int x, int y)[] SolidList
        {
            get
            {
                if (_solidList != null) return _solidList;
                var list = new List<(int, int)>();
                if (_solid != null)
                    for (int y = Rect.Y; y < Rect.Top; y++)
                        for (int x = Rect.X; x < Rect.Right; x++)
                            if (IsSolid(x, y)) list.Add((x, y));
                return _solidList = list.ToArray();
            }
        }

        /// <summary>Makes a cell of its rectangle solid or open again (work order 051).</summary>
        public void SetSolid(int x, int y, bool solid)
        {
            if (!Rect.Contains(x, y) || IsSolid(x, y) == solid) return;
            if (_solid != null && !_solidFor.Equals(Rect))
            {
                // The rectangle was changed after its cells were marked: the marks are kept where it still has them.
                bool[] old = _solid;
                CellRect was = _solidFor;
                _solid = new bool[Rect.Width * Rect.Height];
                _solidFor = Rect;
                _solidCount = 0;
                for (int j = was.Y; j < was.Top; j++)
                    for (int i = was.X; i < was.Right; i++)
                        if (old[(j - was.Y) * was.Width + (i - was.X)] && Rect.Contains(i, j))
                        {
                            _solid[(j - Rect.Y) * Rect.Width + (i - Rect.X)] = true;
                            _solidCount++;
                        }
            }
            if (_solid == null)
            {
                _solid = new bool[Rect.Width * Rect.Height];
                _solidFor = Rect;
            }
            _solid[(y - _solidFor.Y) * _solidFor.Width + (x - _solidFor.X)] = solid;
            _solidCount += solid ? 1 : -1;
            _solidList = null;
            ShapeCache = null;
        }

        /// <summary>Every cell of its rectangle open again.</summary>
        public void ClearSolid()
        {
            _solid = null;
            _solidCount = 0;
            _solidList = null;
            ShapeCache = null;
        }
    }

    /// <summary>
    /// A door between two rooms that touch: an opening in the wall line they share. On a vertical line (running north
    /// and south, at x = <see cref="Line"/>) it spans the cells y = From to From + Width - 1; on a horizontal line (at
    /// y = Line) the cells x = From to From + Width - 1. The cells on both sides of it belong to the two rooms.
    /// </summary>
    public sealed class LevelDoor
    {
        /// <summary>The two rooms by their place in the run; A is the earlier one.</summary>
        public int RoomA, RoomB;
        public bool Vertical;
        public int Line;
        public int From;
        public int Width;

        /// <summary>The cells either side of the opening, as (x, y): the west or south one first.</summary>
        public IEnumerable<KeyValuePair<(int x, int y), (int x, int y)>> CellPairs()
        {
            for (int i = From; i < From + Width; i++)
                yield return Vertical
                    ? new KeyValuePair<(int, int), (int, int)>((Line - 1, i), (Line, i))
                    : new KeyValuePair<(int, int), (int, int)>((i, Line - 1), (i, Line));
        }

        /// <summary>The opening as a stretch of ground, in metres: its two ends.</summary>
        public void Opening(double cellSize, out LevelPoint a, out LevelPoint b)
        {
            if (Vertical)
            {
                a = new LevelPoint(Line * cellSize, From * cellSize);
                b = new LevelPoint(Line * cellSize, (From + Width) * cellSize);
            }
            else
            {
                a = new LevelPoint(From * cellSize, Line * cellSize);
                b = new LevelPoint((From + Width) * cellSize, Line * cellSize);
            }
        }

        public bool Joins(int room) => RoomA == room || RoomB == room;
        public int Other(int room) => RoomA == room ? RoomB : RoomA;
    }

    /// <summary>
    /// A level as the generator describes it, in whole cells: the seed and the generator's version, the rooms in the
    /// order of the run, the doors between them, and what stands in each room. No engine: the grey-box level is built
    /// from this, and the tests read it.
    /// </summary>
    public sealed class LevelLayout
    {
        public ulong Seed;
        public int Version;
        public int GridCells;
        public float CellSize;
        /// <summary>Which attempt of the generator gave it, from 1. More than 1 when an earlier attempt could not be finished.</summary>
        public int Attempt;
        /// <summary>The floor's number in its act, 1 to <see cref="LevelValues.FloorsPerAct"/> (work order 012): it decides the enemies, and the last holds the act boss.</summary>
        public int Floor = 1;
        /// <summary>
        /// Every room in one row, the treasure rooms on it (work order 041, <see cref="LevelValues.InALine"/>): the rooms
        /// in the order of the run are the row, each joined to the next. False: the chain with the treasure rooms off its
        /// sides, as before. The validator reads the rules a floor keeps from it.
        /// </summary>
        public bool InALine;
        /// <summary>
        /// The rooms' outlines were carved (work order 051, <see cref="LevelValues.Shapes"/>, <see cref="RoomShapes"/>): each room
        /// with a shape has its gate. False: every room is its rectangle, as before.
        /// </summary>
        public bool Shapes;
        /// <summary>The rooms were drawn with the templates on (work order 058, <see cref="LevelValues.Templates"/>): some have a <see cref="LevelRoom.Template"/>.</summary>
        public bool Templates;
        /// <summary>
        /// The rooms of the row stand apart, a passage between every two (work order 059, <see cref="LevelValues.Passages"/>,
        /// <see cref="Rules.Passages"/>): the rooms in the order of the run are E P C P C ... P X. False: each room against the
        /// one before it, as before.
        /// </summary>
        public bool Passages;
        /// <summary>
        /// The props were put by the rules of the rooms you can use (work order 088, <see cref="LevelValues.UsableRooms"/>): no prop
        /// shuts floor off and a way of whole cells runs through each room (<see cref="JoinedFloor"/>), and the fights' rooms were
        /// given their cover (<see cref="FightCover"/>). The validator asks the joined floor of a floor that says so.
        /// </summary>
        public bool UsableRooms;
        public readonly List<LevelRoom> Rooms = new List<LevelRoom>();
        public readonly List<LevelDoor> Doors = new List<LevelDoor>();
        /// <summary>
        /// The exit room of its own the floor before ended in, in the run that asked for this floor (work order 103,
        /// <see cref="WayDowns"/>): its template's name, which the exit room's draw passes over while another can stand; null for a
        /// floor built alone or the first of a run.
        /// </summary>
        public string ExitBefore;

        public string SeedText => LevelSeed.ToText(Seed);

        /// <summary>The exit room: the last room of the main path.</summary>
        public LevelRoom ExitRoom
        {
            get
            {
                foreach (LevelRoom r in Rooms)
                    if (r.Kind == RoomKind.Exit) return r;
                return null;
            }
        }

        /// <summary>The elite room (work order 017), or null on a floor without one.</summary>
        public LevelRoom EliteRoom
        {
            get
            {
                foreach (LevelRoom r in Rooms)
                    if (r.Elite) return r;
                return null;
            }
        }

        /// <summary>
        /// The room that has this cell, or null: what is at a cell. Work order 051: null too for a cell carved out of a room
        /// (<see cref="LevelRoom.IsSolid"/>), so whatever asks this (the walls, the kit, the text map, the places of everything)
        /// follows the room's outline, not its rectangle.
        /// </summary>
        public LevelRoom RoomAtCell(int x, int y)
        {
            foreach (LevelRoom r in Rooms)
                if (r.Rect.Contains(x, y)) return r.IsSolid(x, y) ? null : r;
            return null;
        }

        /// <summary>The room whose rectangle has this cell, solid or open, or null (work order 051).</summary>
        public LevelRoom RoomRectAtCell(int x, int y)
        {
            foreach (LevelRoom r in Rooms)
                if (r.Rect.Contains(x, y)) return r;
            return null;
        }

        /// <summary>The cell is carved out of a room (work order 051).</summary>
        public bool IsSolidCell(int x, int y) => RoomRectAtCell(x, y)?.IsSolid(x, y) ?? false;

        /// <summary>The room under this ground point (metres from the grid's corner), or null.</summary>
        public LevelRoom RoomAt(double x, double y) =>
            RoomAtCell((int)Math.Floor(x / CellSize), (int)Math.Floor(y / CellSize));

        public IEnumerable<LevelDoor> DoorsOf(int room)
        {
            foreach (LevelDoor d in Doors)
                if (d.Joins(room)) yield return d;
        }

        /// <summary>The cells the rooms cover, as the smallest rectangle around them all.</summary>
        public CellRect Bounds()
        {
            if (Rooms.Count == 0) return new CellRect();
            int x0 = int.MaxValue, y0 = int.MaxValue, x1 = int.MinValue, y1 = int.MinValue;
            foreach (LevelRoom r in Rooms)
            {
                x0 = Math.Min(x0, r.Rect.X);
                y0 = Math.Min(y0, r.Rect.Y);
                x1 = Math.Max(x1, r.Rect.Right);
                y1 = Math.Max(y1, r.Rect.Top);
            }
            return new CellRect(x0, y0, x1 - x0, y1 - y0);
        }

        /// <summary>The rooms and doors only, as text: two levels with the same text have the same layout.</summary>
        public string DescribeLayout()
        {
            var sb = new StringBuilder();
            foreach (LevelRoom r in Rooms) sb.Append(r.Letter).Append(r.Order).Append(r.Rect).Append(';');
            foreach (LevelDoor d in Doors)
                sb.AppendFormat(CultureInfo.InvariantCulture, "{0}-{1}{2}{3}@{4}+{5};", d.RoomA, d.RoomB, d.Vertical ? 'V' : 'H', d.Line, d.From, d.Width);
            return sb.ToString();
        }

        /// <summary>
        /// Every field, as text: two levels with the same text are the same level, field for field. Without
        /// <paramref name="props"/> it leaves the props out (work order 009), for comparing the rest, and the rooms' themes
        /// with them (work order 028: a theme is what a room's props are, said in a line before them).
        /// </summary>
        public string Describe(bool props = true)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "seed {0} ({1}) floor {6} version {2} grid {3} cell {4:R} attempt {5}{7}\n",
                            Seed, SeedText, Version, GridCells, CellSize, Attempt, Floor,
                            (Shapes ? " carved" : "") + (Templates ? " templated" : "") + (InALine ? " in a line" : "") + (Passages ? " with passages" : "")); // work orders 051, 058, 041 and 059
            foreach (LevelRoom r in Rooms)
            {
                sb.Append(r.Order).Append(' ').Append(r.Kind).Append(r.Elite ? " elite" : "").Append(' ').Append(r.Rect);
                sb.Append(RoomShapes.Describe(r)); // work order 051: nothing for a room with no shape
                if (r.PlayerStart.HasValue) sb.Append(" start ").Append(r.PlayerStart.Value);
                for (int i = 0; i < r.EnemyPoints.Count; i++) sb.Append(" enemy ").Append(r.EnemyPoints[i]).Append(' ').Append(r.GroupAt(i));
                sb.Append('\n');
                if (props && r.Theme != RoomTheme.None) sb.Append("  theme ").Append(RoomThemes.Word(r.Theme)).Append('\n');
                if (props)
                    foreach (LevelProp p in r.Props) sb.Append("  prop ").Append(p).Append('\n');
            }
            foreach (LevelDoor d in Doors)
                sb.AppendFormat(CultureInfo.InvariantCulture, "door {0}-{1} {2} line {3} from {4} width {5}\n",
                                d.RoomA, d.RoomB, d.Vertical ? "vertical" : "horizontal", d.Line, d.From, d.Width);
            return sb.ToString();
        }
    }
}
