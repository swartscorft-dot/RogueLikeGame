using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the room as a cave (work order 076; Kristian, 2026-11-21 in the evening: "jeg liker heller ikke at man
    /// ser "utenfor" mappen, skulle ønske at enten vi var litt mer zoomet inn eller løst på annet vis. jeg liker heller ikke at
    /// vi ser andre zoner når vi er i et rom. veggene må gå helt opp og føles at vi er i en hule", and of the treasure room,
    /// "chestrommet så blokker den gull-proppen. syntes den er litt unødvendig"). The master switch is
    /// <see cref="LevelValues.Cave"/>; each of the three parts has its own here. All first guesses, read when a floor is built
    /// (the earth's colour live); none is read by the generator, so every seed's layout, props and enemies are what they were.
    /// Lengths in metres, colours as picked.
    /// </summary>
    [Serializable]
    public sealed class CaveValues
    {
        // 1. Nothing outside the room she is in (CaveSight.Shown)
        public bool HideOthers = true;     // only her room or passage and the ones a door from it are drawn; the rest, and every enemy standing in it, are not (drawing only: nothing else changes)
        public Rgb Earth = new Rgb(0.016f, 0.015f, 0.014f); // the camera's background while a floor is up: the inside of the earth (the scene's own is 0.12, 0.13, 0.16, a grey-blue "outside")

        // 2. The walls go all the way up (CaveRise)
        public bool TallWalls = true;      // rock rises over the full walls, far and side, fading to the earth's black; the low near walls stay low
        public float RiseHeight = 9f;      // m over the floor the rock over a full wall reaches at most (the kit's wall is about 3 m); never so high that it hides what today's walls leave seen
        public float SightMargin = 0.75f;  // m the rock stays under any line from the camera to her or to a floor she can see that today's walls leave free
        public float RockReach = 0.75f;     // m from its wall's line into the room that the rock is taken to reach, for that rule: half the wall and the facets
        public float MinRise = 0.75f;      // m: rock lower than this over its wall is not built
        public bool GuardNeighbours = false; // what floor the sight rule keeps seen: false, the floor of her own room or passage and the first cell past each of its doors (where an enemy comes in); true, also the whole floor of every room and passage shown with hers (round 2's rule, the evening of 2026-11-21: its pictures showed two walls in three bare, since a side wall south of a door stands between the camera and the passage beyond it)
        public float RockStep = 0.9f;      // m between the rock's facets, along and up
        public float RockJitter = 0.45f;   // m, at most, a facet's corner stands out from the wall's face (round 1's pictures: 0.3 read as a smooth slab)
        public Rgb Rock = new Rgb(0.52f, 0.47f, 0.43f); // the rock's colour at its foot, about the kit's stone; it darkens to black at its top (round 1's pictures: 0.34 read as black)
        public float RockFade = 1.3f;      // how it darkens up its height: 1 evenly, more sooner (round 2's pictures: 1.1 read as a grey roof)

        // 3. The treasure room's gold
        public bool NoGoldHeaps = true;    // the vault's two large coin heaps beside the chest's dais (work order 052) are not placed; the chest, its dais, its gold light, the code-made heaps on the dais and the spilled coins stay
    }

    /// <summary>
    /// What is drawn of a floor while she is in one of its rooms or passages (work order 076): that space and every space one
    /// door from it. In a room: the room and the passages at its doors (she never walks into a passage that appears round
    /// her); in a passage: the passage and the rooms at both its ends (the room ahead is there while she is still in the
    /// passage; the room behind her goes when she has left the passage). On a floor without passages, the rooms at her room's
    /// doors. Plain data: no engine.
    /// </summary>
    public static class CaveSight
    {
        /// <summary>Fills <paramref name="shown"/> (one per room of the layout, by its place in the run) for her in <paramref name="here"/>; nothing for -1. No allocation.</summary>
        public static void Shown(LevelLayout layout, int here, bool[] shown)
        {
            Array.Clear(shown, 0, shown.Length);
            if (here < 0 || here >= shown.Length) return;
            shown[here] = true;
            List<LevelDoor> doors = layout.Doors;
            for (int i = 0; i < doors.Count; i++)
            {
                LevelDoor d = doors[i];
                if (!d.Joins(here)) continue;
                int o = d.Other(here);
                if (o >= 0 && o < shown.Length) shown[o] = true;
            }
        }
    }

    /// <summary>
    /// How high the rock over each full wall of a floor rises (work order 076), and the sight rule that holds it down. Every
    /// cell edge of the floor's grid has today's height (what the crypt kit draws there: a full wall, a low near wall, nothing
    /// at a door) and the cave's: over a full wall, rock up to <see cref="CaveValues.RiseHeight"/>, lowered (never below
    /// today's) wherever it would stand between the camera and her, or between the camera and any floor the rule guards
    /// (<see cref="Guarded"/>: her space's and the first cell past each of its doors), on a line that today's walls leave free; a margin of
    /// <see cref="CaveValues.SightMargin"/> under that line, the rock taken to reach <see cref="CaveValues.RockReach"/> into
    /// the room. The camera stands where <c>CameraRig</c> puts it: behind her to the south at its pitch and distance. Her places
    /// are sampled at the quarter points of every open cell of her space, the floor's at the sixths' (0.5, 2 and 3.5 m into
    /// the cell); only floor within the camera's view of her is asked about. The low near walls never rise. Plain data.
    /// </summary>
    public sealed class CaveRise
    {
        public readonly LevelLayout Layout;
        /// <summary>The grid: cells x from <see cref="X0"/>, <see cref="Nx"/> of them; y likewise. One cell round the rooms.</summary>
        public readonly int X0, Y0, Nx, Ny;
        public readonly float CameraHeight, CameraBack, WallTop, LowTop;
        readonly float[] _todayV, _todayH, _riseV, _riseH;
        readonly int[] _ownerA, _ownerB; // the spaces either side of each edge, vertical edges first, -1 for none
        readonly int[] _cell;            // the space of each cell, -1 for none or carved
        readonly CaveValues _v;

        /// <summary>Rays asked about while making it, and how many of them a rise was lowered for (for the tests and the report).</summary>
        public long Rays, Lowered;

        /// <summary>Rock is (or may be) on a full wall's edge; not at a door, a low wall, or where no wall stands.</summary>
        public bool HasRock(bool vertical, int line, int cell) => RiseOf(vertical, line, cell) > TodayOf(vertical, line, cell) + 1e-4f;

        public float TodayOf(bool vertical, int line, int cell)
        {
            int i = Index(vertical, line, cell);
            return i < 0 ? 0f : vertical ? _todayV[i] : _todayH[i];
        }

        /// <summary>How high the cave's wall stands on the edge: the rock's top over a full wall, else today's.</summary>
        public float RiseOf(bool vertical, int line, int cell)
        {
            int i = Index(vertical, line, cell);
            return i < 0 ? 0f : vertical ? _riseV[i] : _riseH[i];
        }

        int Index(bool vertical, int line, int cell)
        {
            if (vertical)
            {
                int x = line - X0, y = cell - Y0;
                return x < 0 || x > Nx || y < 0 || y >= Ny ? -1 : x * Ny + y;
            }
            int hy = line - Y0, hx = cell - X0;
            return hy < 0 || hy > Ny || hx < 0 || hx >= Nx ? -1 : hy * Nx + hx;
        }

        int SpaceAt(int x, int y)
        {
            int cx = x - X0, cy = y - Y0;
            return cx < 0 || cy < 0 || cx >= Nx || cy >= Ny ? -1 : _cell[cy * Nx + cx];
        }

        CaveRise(LevelLayout layout, CaveValues v, float wallTop, float lowTop, float pitch, float distance)
        {
            Layout = layout;
            _v = v;
            CellRect b = layout.Bounds();
            X0 = b.X - 1;
            Y0 = b.Y - 1;
            Nx = b.Width + 2;
            Ny = b.Height + 2;
            WallTop = wallTop;
            LowTop = lowTop;
            double p = pitch * Math.PI / 180.0;
            CameraHeight = (float)(distance * Math.Sin(p));
            CameraBack = (float)(distance * Math.Cos(p));
            _todayV = new float[(Nx + 1) * Ny];
            _riseV = new float[(Nx + 1) * Ny];
            _todayH = new float[(Ny + 1) * Nx];
            _riseH = new float[(Ny + 1) * Nx];
            _ownerA = new int[_todayV.Length + _todayH.Length];
            _ownerB = new int[_ownerA.Length];
            _cell = new int[Nx * Ny];
            for (int y = 0; y < Ny; y++)
                for (int x = 0; x < Nx; x++)
                    _cell[y * Nx + x] = layout.RoomAtCell(x + X0, y + Y0)?.Order ?? -1;
            for (int x = 0; x <= Nx; x++)
                for (int y = 0; y < Ny; y++)
                {
                    int i = x * Ny + y;
                    _ownerA[i] = SpaceAt(x + X0 - 1, y + Y0);
                    _ownerB[i] = SpaceAt(x + X0, y + Y0);
                }
            for (int y = 0; y <= Ny; y++)
                for (int x = 0; x < Nx; x++)
                {
                    int i = _todayV.Length + y * Nx + x;
                    _ownerA[i] = SpaceAt(x + X0, y + Y0 - 1);
                    _ownerB[i] = SpaceAt(x + X0, y + Y0);
                }
        }

        /// <summary>
        /// The rise of every full wall of the floor. <paramref name="wallTop"/> and <paramref name="lowTop"/> are how high the
        /// kit's full and low walls are drawn (the level passes its models' tops); <paramref name="pitch"/> and
        /// <paramref name="distance"/> the camera's (FeelValues). With <see cref="CaveValues.TallWalls"/> off every edge is today's.
        /// </summary>
        public static CaveRise Make(LevelLayout layout, CryptKitPlan kit, CaveValues v, float wallTop, float lowTop, float pitch, float distance)
        {
            v = v ?? new CaveValues();
            var r = new CaveRise(layout, v, wallTop, lowTop, pitch, distance);
            float rise = Math.Max(wallTop, v.RiseHeight);
            foreach (CryptKitSection s in kit.Sections)
            {
                int i = r.Index(s.Vertical, s.Line, s.Cell);
                if (i < 0) continue;
                float today = s.Low ? lowTop : wallTop;
                float cave = !s.Low && v.TallWalls ? rise : today;
                if (s.Vertical)
                {
                    r._todayV[i] = today;
                    r._riseV[i] = cave;
                }
                else
                {
                    r._todayH[i] = today;
                    r._riseH[i] = cave;
                }
            }
            if (v.TallWalls && rise > wallTop)
            {
                r.HoldDown();
                // A rock the sight rule had to lower at all stands where lines of sight graze it: it is left out (today's wall
                // alone), not cut to a height that the rule's samples only nearly bound.
                for (int i = 0; i < r._riseV.Length; i++)
                    if (r._riseV[i] < rise && r._riseV[i] > r._todayV[i]) r._riseV[i] = r._todayV[i];
                for (int i = 0; i < r._riseH.Length; i++)
                    if (r._riseH[i] < rise && r._riseH[i] > r._todayH[i]) r._riseH[i] = r._todayH[i];
            }
            return r;
        }

        static readonly double[] s_her = { 0.5, 1.5, 2.5, 3.5 };
        static readonly double[] s_floor = { 0.5, 2.0, 3.5 };
        // A cell past a door, to its edges: the floor beyond it is not guarded, so no other sample stands near its far side.
        static readonly double[] s_door = { 0.1, 1.0, 2.0, 3.0, 3.9 };

        /// <summary>The camera's view of the ground round her, generously: from this far behind her to this far ahead, and this wide.</summary>
        public const double ViewBehind = 9.5, ViewAhead = 13.0, ViewSide = 16.0, ViewSideGrowth = 0.6;

        public static bool InView(LevelPoint her, LevelPoint at)
        {
            double dy = at.Y - her.Y, dx = Math.Abs(at.X - her.X);
            return dy >= -ViewBehind && dy <= ViewAhead && dx <= ViewSide + ViewSideGrowth * Math.Max(0.0, dy);
        }

        /// <summary>The sample points of a space's open cells, at the given offsets into each cell.</summary>
        public List<LevelPoint> Points(int space, double[] offsets)
        {
            var list = new List<LevelPoint>();
            double cs = Layout.CellSize;
            for (int y = 0; y < Ny; y++)
                for (int x = 0; x < Nx; x++)
                {
                    if (_cell[y * Nx + x] != space) continue;
                    foreach (double oy in offsets)
                        foreach (double ox in offsets)
                            list.Add(new LevelPoint((x + X0) * cs + ox, (y + Y0) * cs + oy));
                }
            return list;
        }

        /// <summary>
        /// The floor the sight rule keeps seen while she is in <paramref name="space"/>, sampled at the offsets into each cell:
        /// the space's own open cells and, past each of its doors, the cell on the other side (where an enemy comes in); with
        /// <see cref="CaveValues.GuardNeighbours"/> the whole floor of every space shown with hers instead. It works from the
        /// cells and the doors alone, so a room of any outline (a template's, work order 072's new ones) is served alike.
        /// </summary>
        public List<LevelPoint> Guarded(int space, bool[] shown, double[] offsets) => Guarded(space, shown, offsets, offsets);

        /// <summary>As <see cref="Guarded(int, bool[], double[])"/>, the cells past the doors at their own offsets.</summary>
        public List<LevelPoint> Guarded(int space, bool[] shown, double[] offsets, double[] doorOffsets)
        {
            var list = Points(space, offsets);
            if (_v.GuardNeighbours)
            {
                for (int o = 0; o < shown.Length; o++)
                    if (shown[o] && o != space) list.AddRange(Points(o, offsets));
                return list;
            }
            double cs = Layout.CellSize;
            foreach (LevelDoor d in Layout.Doors)
            {
                if (!d.Joins(space)) continue;
                int o = d.Other(space);
                foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                    foreach ((int x, int y) c in new[] { pair.Key, pair.Value })
                    {
                        if (SpaceAt(c.x, c.y) != o) continue;
                        foreach (double oy in doorOffsets)
                            foreach (double ox in doorOffsets)
                                list.Add(new LevelPoint(c.x * cs + ox, c.y * cs + oy));
                    }
            }
            return list;
        }

        void HoldDown()
        {
            int n = Layout.Rooms.Count;
            var shown = new bool[n];
            var herBySpace = new List<LevelPoint>[n];
            for (int s = 0; s < n; s++) herBySpace[s] = Points(s, s_her);
            for (int s = 0; s < n; s++)
            {
                CaveSight.Shown(Layout, s, shown);
                List<LevelPoint> targets = Guarded(s, shown, s_floor, s_door);
                foreach (LevelPoint her in herBySpace[s])
                    foreach (LevelPoint t in targets)
                    {
                        if (!InView(her, t)) continue;
                        Rays++;
                        if (Ray(her, t, shown, true)) Lowered++;
                    }
            }
        }

        // A ray's crossings, nearest the floor point first: the edge's place in the arrays and how far from the point.
        readonly int[] _hitEdge = new int[64];
        readonly double[] _hitAt = new double[64];
        readonly bool[] _hitEnd = new bool[64]; // the edge beside the one crossed, within the rock's reach of its end: for the cave only

        void Hit(int edge, double d, bool end, ref int hits)
        {
            if (edge < 0 || hits >= _hitEdge.Length) return;
            _hitEdge[hits] = edge;
            _hitAt[hits] = d;
            _hitEnd[hits++] = end;
        }

        /// <summary>
        /// The line from the camera behind <paramref name="her"/> to the floor at <paramref name="t"/>. Today's walls (every
        /// one, shown or not: today all are drawn) hide it when one stands above the line where the line crosses it. When they
        /// do not: with <paramref name="lower"/>, every shown rock that would reach within the margin of the line is lowered to
        /// keep under it (true if any was); without, true when a shown rock or wall of the cave hides it.
        /// </summary>
        bool Ray(LevelPoint her, LevelPoint t, bool[] shown, bool lower)
        {
            double cx = her.X, cy = her.Y - CameraBack;
            double dx = cx - t.X, dy = cy - t.Y, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6) return false;
            double ux = dx / len, uy = dy / len, slope = CameraHeight / len;
            double reach = _v.RockReach, margin = _v.SightMargin;
            double far = Math.Min(len, (Math.Max(_v.RiseHeight, WallTop) + margin) / slope + reach);
            double cs = Layout.CellSize;
            int hits = 0;
            // Vertical lines.
            if (Math.Abs(ux) > 1e-9)
            {
                int step = ux > 0 ? 1 : -1;
                int k = ux > 0 ? (int)Math.Floor(t.X / cs) + 1 : (int)Math.Ceiling(t.X / cs) - 1;
                for (; hits < _hitEdge.Length; k += step)
                {
                    double d = (k * cs - t.X) / ux;
                    if (d > far) break;
                    double y = t.Y + uy * d;
                    int cell = (int)Math.Floor(y / cs);
                    double into = y - cell * cs;
                    Hit(Index(true, k, cell), d, false, ref hits);
                    if (into < reach) Hit(Index(true, k, cell - 1), d, true, ref hits);
                    else if (into > cs - reach) Hit(Index(true, k, cell + 1), d, true, ref hits);
                }
            }
            // Horizontal lines.
            if (Math.Abs(uy) > 1e-9)
            {
                int step = uy > 0 ? 1 : -1;
                int k = uy > 0 ? (int)Math.Floor(t.Y / cs) + 1 : (int)Math.Ceiling(t.Y / cs) - 1;
                for (; hits < _hitEdge.Length; k += step)
                {
                    double d = (k * cs - t.Y) / uy;
                    if (d > far) break;
                    double x = t.X + ux * d;
                    int cell = (int)Math.Floor(x / cs);
                    double into = x - cell * cs;
                    int i = Index(false, k, cell);
                    Hit(i < 0 ? -1 : _todayV.Length + i, d, false, ref hits);
                    int n = into < reach ? Index(false, k, cell - 1) : into > cs - reach ? Index(false, k, cell + 1) : -1;
                    if (n >= 0) Hit(_todayV.Length + n, d, true, ref hits);
                }
            }
            // Today: hidden by any wall standing above the line where it crosses.
            for (int h = 0; h < hits; h++)
            {
                if (_hitEnd[h]) continue;
                int e = _hitEdge[h];
                float today = e < _todayV.Length ? _todayV[e] : _todayH[e - _todayV.Length];
                if (today > slope * _hitAt[h]) return false;
            }
            bool any = false;
            for (int h = 0; h < hits; h++)
            {
                int e = _hitEdge[h];
                int a = _ownerA[e], b = _ownerB[e];
                if (!((a >= 0 && shown[a]) || (b >= 0 && shown[b]))) continue; // not drawn in the cave: hides nothing
                bool v = e < _todayV.Length;
                int j = v ? e : e - _todayV.Length;
                float today = v ? _todayV[j] : _todayH[j];
                float cave = v ? _riseV[j] : _riseH[j];
                if (cave <= today) continue;
                double keep = slope * Math.Max(0.0, _hitAt[h] - reach) - margin; // the rock's top must stay under this
                if (cave <= keep) continue;
                if (!lower) return true;
                float to = (float)Math.Max(today, keep);
                if (v) _riseV[j] = to;
                else _riseH[j] = to;
                any = true;
            }
            return any;
        }

        /// <summary>For a test's message: the edges a line crosses near the floor point, with their heights today and in the cave and the line's.</summary>
        public string Explain(LevelPoint her, LevelPoint t)
        {
            double cx = her.X, cy = her.Y - CameraBack;
            double dx = cx - t.X, dy = cy - t.Y, len = Math.Sqrt(dx * dx + dy * dy), slope = CameraHeight / len;
            var sb = new System.Text.StringBuilder();
            double cs = Layout.CellSize;
            for (double d = 0.05; d < Math.Min(len, 14); d += 0.05)
            {
                double x = t.X + dx / len * d, y = t.Y + dy / len * d;
                int kx = (int)Math.Round(x / cs), ky = (int)Math.Round(y / cs);
                if (Math.Abs(x - kx * cs) < 0.026) sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "V{0}_{1} at {2:0.00}: line {3:0.00}, today {4:0.0}, cave {5:0.0}; ", kx, (int)Math.Floor(y / cs), d, slope * d, TodayOf(true, kx, (int)Math.Floor(y / cs)), RiseOf(true, kx, (int)Math.Floor(y / cs)));
                if (Math.Abs(y - ky * cs) < 0.026) sb.AppendFormat(System.Globalization.CultureInfo.InvariantCulture, "H{0}_{1} at {2:0.00}: line {3:0.00}, today {4:0.0}, cave {5:0.0}; ", ky, (int)Math.Floor(x / cs), d, slope * d, TodayOf(false, ky, (int)Math.Floor(x / cs)), RiseOf(false, ky, (int)Math.Floor(x / cs)));
            }
            return sb.ToString();
        }

        /// <summary>
        /// For the tests: does the cave hide the floor at <paramref name="t"/> from the camera behind <paramref name="her"/>, in
        /// her space with <paramref name="shown"/> drawn, where today's walls leave it seen? (The rock taken at its reach, but with
        /// no margin: the rule's margin is what the test has in hand.)
        /// </summary>
        public bool NewlyHidden(LevelPoint her, LevelPoint t, bool[] shown)
        {
            double m = _v.SightMargin;
            _v.SightMargin = 0f;
            try { return Ray(her, t, shown, false); }
            finally { _v.SightMargin = (float)m; }
        }
    }
}
