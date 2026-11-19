using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    public enum RoomKind { Start, Combat, Reward, Boss }

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
    /// What stands at one enemy point (work order 008): a kind, and how many. More than one is a pack, which only
    /// swarmers come in: they stand around the point.
    /// </summary>
    [Serializable]
    public sealed class EnemyGroup
    {
        public EnemyKind Kind;
        public int Count = 1;

        public EnemyGroup() { }

        public EnemyGroup(EnemyKind kind, int count = 1)
        {
            Kind = kind;
            Count = count;
        }

        public bool IsPack => Count > 1;
        public EnemyGroup Copy() => new EnemyGroup(Kind, Count);
        public char Letter => EnemyKinds.Letter(Kind, Count);
        public override string ToString() => EnemyKinds.Word(Kind) + (Count > 1 ? " x" + Count.ToString(CultureInfo.InvariantCulture) : "");
    }

    /// <summary>One room: its kind, its cells and its place in the order of the run, and what stands in it.</summary>
    public sealed class LevelRoom
    {
        public RoomKind Kind;
        public CellRect Rect;
        /// <summary>Its place in the run, from 0 (the start room); the level's list of rooms is in this order.</summary>
        public int Order;
        /// <summary>Where the player starts. Only in the start room.</summary>
        public LevelPoint? PlayerStart;
        public readonly List<LevelPoint> EnemyPoints = new List<LevelPoint>();
        /// <summary>What stands at each enemy point, in the same order as <see cref="EnemyPoints"/> (work order 008).</summary>
        public readonly List<EnemyGroup> EnemyGroups = new List<EnemyGroup>();
        /// <summary>The props that stand in it (work order 009), in the order they were placed.</summary>
        public readonly List<LevelProp> Props = new List<LevelProp>();
        /// <summary>The generator's notes, not part of the level: how many props it meant to put here, and how many of those it left out with a place refused by the clear lane.</summary>
        public int PropsWanted, PropsLeftOutByLane;

        /// <summary>What stands at enemy point <paramref name="i"/>; a single brute when the room says nothing (a hand-made layout).</summary>
        public EnemyGroup GroupAt(int i) => i >= 0 && i < EnemyGroups.Count && EnemyGroups[i] != null ? EnemyGroups[i] : new EnemyGroup(EnemyKind.Brute);

        /// <summary>The letter for its kind on a map.</summary>
        public char Letter => KindLetter(Kind);

        public static char KindLetter(RoomKind kind) =>
            kind == RoomKind.Start ? 'S' : kind == RoomKind.Combat ? 'C' : kind == RoomKind.Reward ? 'R' : 'B';

        public LevelPoint Middle(double cellSize) =>
            new LevelPoint((Rect.X + Rect.Width * 0.5) * cellSize, (Rect.Y + Rect.Height * 0.5) * cellSize);
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
        public readonly List<LevelRoom> Rooms = new List<LevelRoom>();
        public readonly List<LevelDoor> Doors = new List<LevelDoor>();

        public string SeedText => LevelSeed.ToText(Seed);

        /// <summary>The room that has this cell, or null.</summary>
        public LevelRoom RoomAtCell(int x, int y)
        {
            foreach (LevelRoom r in Rooms)
                if (r.Rect.Contains(x, y)) return r;
            return null;
        }

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
        /// <paramref name="props"/> it leaves the props out (work order 009), for comparing the rest.
        /// </summary>
        public string Describe(bool props = true)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(CultureInfo.InvariantCulture, "seed {0} ({1}) version {2} grid {3} cell {4:R} attempt {5}\n",
                            Seed, SeedText, Version, GridCells, CellSize, Attempt);
            foreach (LevelRoom r in Rooms)
            {
                sb.Append(r.Order).Append(' ').Append(r.Kind).Append(' ').Append(r.Rect);
                if (r.PlayerStart.HasValue) sb.Append(" start ").Append(r.PlayerStart.Value);
                for (int i = 0; i < r.EnemyPoints.Count; i++) sb.Append(" enemy ").Append(r.EnemyPoints[i]).Append(' ').Append(r.GroupAt(i));
                sb.Append('\n');
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
