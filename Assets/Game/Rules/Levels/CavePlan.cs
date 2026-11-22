using System;
using System.Collections.Generic;
using System.Threading.Tasks;

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
        // The rock round the room (the producer on round 2's pictures, the morning of 2026-11-22: "a lit floor floating in flat
        // black, not a room cut out of rock"; the carved cells' dark-blue tops "look like a pit or water: in a cave those are
        // rock too"): every cell that is no floor within LidReach cells of one gets a rough rock top at the full walls' height,
        // lowered to the low walls' where the sight rule says (in front of a floor, toward the camera), darkening to the earth's
        // black outwards. It covers the carved cells' tops too.
        public bool RockLids = true;
        public int LidReach = 2;           // cells from the nearest floor that get a rock top; past them, the earth's black
        public float LidJitter = 0.3f;     // m, at most, a corner of the top's facets sinks under its height
        public float LidShade = 0.3f;      // how far down the rock's fade the top starts at the wall (0 the rock's foot colour, 1 black)
        // What the sight rule guards besides her room's floor (work order 083; 076's rule is both false, the cells past the doors guarded):
        public bool GuardDoorCells = true;  // the first cell past each of her room's doors (076: where an enemy comes in); the dark look leaves it to the tall walls ("another zone")
        public bool GuardHerAndDoors = false; // her body, feet and head, wherever she stands, and the openings of her room's doors

        // 3. The treasure room's gold
        public bool NoGoldHeaps = true;    // the vault's two large coin heaps beside the chest's dais (work order 052) are not placed; the chest, its dais, its gold light, the code-made heaps on the dais and the spilled coins stay

        // The look (work order 083, "the cave, second attempt"; Kristian, 2026-11-22, on 076's pictures: "når jeg ser
        // skjermbildene av nye "hule" følelsen så ser det ikke bra ut. Hva er det poe2 og diablo gjør ... Er kameraet mer zoomet
        // inn og veggene høyere?"). Before: the floor as it was before 076 (no CaveView; the gold heaps rule above still holds).
        // Rock: 076's look (the rock faces and tops above, by their own switches). Dark: the far walls tall, the near walls a
        // lip, a black cap on every wall, nothing outside the walls but the earth's black, and the picture falling to black
        // toward its edges. Read when a floor is built.
        public CaveLook Look = CaveLook.Dark;
        public float HerHeight = 1.8f;     // m, her model's height (CombatEffects' fallback for her top): the heights below are in it
        public float FarHeight = 5.4f;     // m a far wall's face rises to (the order's first guess: three times her height; try 2.5 to 4 times); the kit's wall is about 3 m
        public float LipHeight = 0.45f;    // m a near wall is cut to (the order's first guess: a quarter of her height); the kit's low wall is about 1 m
        public bool DoorPostsStand = true; // the posts at the two ends of a door in a near wall keep the kit's height, so that the door reads (the order: "the door's frame may stand taller than the lip")
        public float FarSightMargin = 0.2f; // m a far wall stays under any line from the camera to her own room's floor, her body or her doors that today's walls leave free; held, it stays at the kit's height
        public float FarReach = 0.35f;     // m from its line into the room that a far wall is taken to reach, for that rule: the kit's wall and its stacked piece are about 0.3 m either side
        public float UpperTint = 0.85f;    // the stacked stone over the kit's wall: the kit's own texture times this, no glow (round 1's pictures: 0.6 under the veil read as black)
        public float FadeFrom = 2.4f;      // m: up to here a far wall is as lit as the kit's; from here it darkens to black at its top (round 1's pictures: from 1.6 the face read as a black band, not as stone)
        public float FadePower = 1f;       // how it darkens up its height: 1 evenly, more sooner (round 1: 1.4)
        public float BlockTop = 0.5f;      // the tops of the carved cells walled in on every side (work order 051's caps) in the kit's stone times this, not the earth's black (round 1's pictures: black, they read as pits; round 2: 0.3 still read dark as a hole); 0 black
        public float CapBack = 0.6f;       // m the black cap reaches past a wall's back where nothing stands behind it ("a flat black cap that runs back into the darkness")
        public float Falloff = 0.62f;      // the falloff's reach: how far out from the picture's middle it stays as lit as the game draws it, a share of the half-diagonal (1: no falloff)
        public float FalloffDark = 0.85f;  // how dark the picture's corners go (0 not at all, 1 black)
    }

    /// <summary>Which look the cave has (work order 083): before 076, 076's rock, or the far walls tall and the edge dark.</summary>
    public enum CaveLook { Before, Rock, Dark }

    /// <summary>
    /// Which wall faces are far and which near for the game's camera (work order 083). The camera looks along its yaw (0: north,
    /// as <c>CameraRig</c> stands it) and down at its pitch. A wall face toward a room is NEAR when it looks the way the
    /// camera looks (it stands between the camera and the room: the room's south side for yaw 0), and FAR otherwise: the
    /// north side, whose face is toward the camera, and the east and west sides, which the camera sees from inside the room
    /// and whose tops lean out of the picture away from her. A section is near if it is near for any room it bounds, so she is
    /// never behind stone; for yaw 0 that is the crypt kit's own low walls (<see cref="CryptKitSection.Low"/>). Plain data.
    /// </summary>
    public static class CaveWalls
    {
        /// <summary>The game's camera yaw in degrees: CameraRig turns it only by its pitch.</summary>
        public const float GameYaw = 0f;
        /// <summary>A face is near when its normal and the camera's look on the ground are closer than this (cosine of 60 degrees).</summary>
        public const double NearCosine = 0.5;

        /// <summary>Is the face of a wall on a cell edge toward the room on side <paramref name="side"/> (+1 east or north, -1 west or south) near?</summary>
        public static bool IsNearFace(bool vertical, int side, float yawDegrees)
        {
            double yaw = yawDegrees * Math.PI / 180.0;
            double fx = Math.Sin(yaw), fz = Math.Cos(yaw);
            double nx = vertical ? side : 0, nz = vertical ? 0 : side;
            return nx * fx + nz * fz > NearCosine;
        }

        /// <summary>Is the section near (for any room it bounds) for a camera at this yaw?</summary>
        public static bool IsNear(CryptKitSection s, float yawDegrees) =>
            (s.RoomMinus >= 0 && IsNearFace(s.Vertical, -1, yawDegrees)) || (s.RoomPlus >= 0 && IsNearFace(s.Vertical, 1, yawDegrees));

        /// <summary>Far: a full section of the kit that is near for no room it bounds.</summary>
        public static bool IsFar(CryptKitSection s, float yawDegrees) => !IsNear(s, yawDegrees);
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

        /// <summary>The space (room or passage, by its place in the run) whose open cell this is; -1 for none, a carved cell or off the grid.</summary>
        public int SpaceAt(int x, int y)
        {
            int cx = x - X0, cy = y - Y0;
            return cx < 0 || cy < 0 || cx >= Nx || cy >= Ny ? -1 : _cell[cy * Nx + cx];
        }

        // The rock tops (CaveValues.RockLids): the height of each cell's, 0 for none (a floor cell, or too far from one).
        readonly float[] _lid;

        /// <summary>How high the rock top over the cell stands, in metres; 0 where there is none (a floor, or past the reach).</summary>
        public float LidOf(int x, int y)
        {
            int cx = x - X0, cy = y - Y0;
            return _lid == null || cx < 0 || cy < 0 || cx >= Nx || cy >= Ny ? 0f : _lid[cy * Nx + cx];
        }

        readonly bool[] _capped; // a carved cell walled in on every side: its top stays at the full walls' height

        /// <summary>The lowest a rock top goes where even the low walls' height would stand in a line of sight: a rock floor, in metres.</summary>
        public const float GroundTop = 0.05f;

        /// <summary>Is the cell a carved one walled in on every side (it has a dark top today, and its rock top is never lowered)?</summary>
        public bool IsCapped(int x, int y)
        {
            int cx = x - X0, cy = y - Y0;
            return _capped != null && cx >= 0 && cy >= 0 && cx < Nx && cy < Ny && _capped[cy * Nx + cx];
        }

        /// <summary>The cells a rock top may stand over: the grid reaches this far round the rooms.</summary>
        public int Margin => _margin;
        readonly int _margin;

        CaveRise(LevelLayout layout, CaveValues v, float wallTop, float lowTop, float pitch, float distance)
        {
            Layout = layout;
            _v = v;
            CellRect b = layout.Bounds();
            _margin = 1 + Math.Max(0, v.LidReach);
            X0 = b.X - _margin;
            Y0 = b.Y - _margin;
            Nx = b.Width + 2 * _margin;
            Ny = b.Height + 2 * _margin;
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
            if (!v.RockLids || v.LidReach < 1) return;
            // Every cell that is no open floor, within the reach of one, gets a top at the full walls' height to begin with.
            _lid = new float[Nx * Ny];
            // The carved cells walled in on every side carry a dark top at the walls' height today (work order 051's caps, as
            // GeneratedLevel.BuildSolidCaps finds them): their rock top stays at that height whatever the sight rule says, since it
            // hides nothing they do not (the producer on round 3's pictures: lowered, they read "as bottomless holes in the floor").
            _capped = new bool[Nx * Ny];
            var seen = new bool[Nx * Ny];
            var group = new List<int>();
            var queue = new Queue<int>();
            for (int start = 0; start < _cell.Length; start++)
            {
                int sx = start % Nx + X0, sy = start / Nx + Y0;
                if (seen[start] || !layout.IsSolidCell(sx, sy)) continue;
                group.Clear();
                bool open = false;
                seen[start] = true;
                queue.Enqueue(start);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue();
                    group.Add(c);
                    int cx = c % Nx + X0, cy = c / Nx + Y0;
                    for (int k = 0; k < 4; k++)
                    {
                        int nx = cx + (k == 0 ? 1 : k == 1 ? -1 : 0), ny = cy + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (layout.RoomRectAtCell(nx, ny) == null) { open = true; continue; }
                        int gx = nx - X0, gy = ny - Y0;
                        if (gx < 0 || gy < 0 || gx >= Nx || gy >= Ny) { open = true; continue; }
                        int n = gy * Nx + gx;
                        if (!seen[n] && layout.IsSolidCell(nx, ny)) { seen[n] = true; queue.Enqueue(n); }
                    }
                }
                if (!open) foreach (int c in group) _capped[c] = true;
            }
            int reach = v.LidReach;
            for (int y = 0; y < Ny; y++)
                for (int x = 0; x < Nx; x++)
                {
                    if (_cell[y * Nx + x] >= 0) continue;
                    bool near = false;
                    for (int dy = -reach; dy <= reach && !near; dy++)
                        for (int dx = -reach; dx <= reach && !near; dx++)
                            near = SpaceAt(x + X0 + dx, y + Y0 + dy) >= 0;
                    if (near) _lid[y * Nx + x] = wallTop;
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
            bool walls = v.TallWalls && rise > wallTop;
            if (walls || r._lid != null) r.HoldDown();
            if (walls)
            {
                // A rock the sight rule had to lower at all stands where lines of sight graze it: it is left out (today's wall
                // alone), not cut to a height that the rule's samples only nearly bound.
                for (int i = 0; i < r._riseV.Length; i++)
                    if (r._riseV[i] < rise && r._riseV[i] > r._todayV[i]) r._riseV[i] = r._todayV[i];
                for (int i = 0; i < r._riseH.Length; i++)
                    if (r._riseH[i] < rise && r._riseH[i] > r._todayH[i]) r._riseH[i] = r._todayH[i];
            }
            return r;
        }

        // Her places: four a side of every cell (three a side let the rock hide floor from two places of the sight test's).
        static readonly double[] s_her = { 0.5, 1.5, 2.5, 3.5 };
        static readonly double[] s_floor = { 0.5, 2.0, 3.5 };
        static readonly double[] s_floorFine = { 0.1, 0.8, 1.6, 2.4, 3.2, 3.9 }; // work order 083
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
            if (_v.GuardHerAndDoors)
            {
                // Work order 083: the openings of her space's doors, along the door's line, are never covered.
                foreach (LevelDoor d in Layout.Doors)
                {
                    if (!d.Joins(space)) continue;
                    d.Opening(cs, out LevelPoint a, out LevelPoint b);
                    for (int k = 0; k <= 4; k++)
                        list.Add(new LevelPoint(a.X + (b.X - a.X) * (0.1 + 0.2 * k), a.Y + (b.Y - a.Y) * (0.1 + 0.2 * k)));
                }
            }
            if (!_v.GuardDoorCells) return list;
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

        /// <summary>
        /// Work order 081 (cleanup and speed): the sight rule's rays are asked about on several threads, a space's rays all at
        /// once against the rise as it stands when the space begins (<see cref="HoldDownAtOnce"/>); only the rays that could
        /// lower anything are then run, on one thread, in the order the one-thread rule runs them. The rise, the rays counted
        /// and the rays that lowered are the same field for field (FloorPlansStayTests). False, or with rock tops (076's rock
        /// look, whose tops are lowered on one thread as before), the rule runs on one thread as it did.
        /// </summary>
        public static bool Threads = true;

        void HoldDown()
        {
            int n = Layout.Rooms.Count;
            var shown = new bool[n];
            var herBySpace = new List<LevelPoint>[n];
            for (int s = 0; s < n; s++) herBySpace[s] = Points(s, s_her);
            bool threads = Threads && _lid == null;
            for (int s = 0; s < n; s++)
            {
                CaveSight.Shown(Layout, s, shown);
                // The dark look samples its floor finer (six a side): its walls stand on her room's own outline, and a floor
                // point a quarter metre from a pier's corner is where a tall wall's end grazes a line of sight.
                List<LevelPoint> targets = Guarded(s, shown, _v.GuardHerAndDoors ? s_floorFine : s_floor, s_door);
                if (threads)
                {
                    HoldDownAtOnce(herBySpace[s], targets, shown); // work order 081
                    continue;
                }
                foreach (LevelPoint her in herBySpace[s])
                {
                    foreach (LevelPoint t in targets)
                    {
                        if (!InView(her, t)) continue;
                        Rays++;
                        if (Ray(her, t, shown, true)) Lowered++;
                    }
                    if (!_v.GuardHerAndDoors) continue;
                    // Work order 083: her body where she stands, her feet and her head.
                    Rays += 2;
                    if (Ray(her, her, shown, true)) Lowered++;
                    if (Ray(her, her, shown, true, _v.HerHeight)) Lowered++;
                }
            }
        }

        // ---------------- The sight rule on several threads (work order 081) ----------------

        /// <summary>
        /// One thread's buffers: a ray's crossings, as <see cref="Ray"/>'s own, and what the thread kept of its rays that could
        /// lower a rise, in the order it asked them: for each edge such a ray would lower, the ray's number, the edge and the
        /// height it must keep under. They grow by doubling and are written only by their thread: no allocation per ray (a
        /// runtime whose allocator takes a lock, as Unity's, would make the threads wait on each other).
        /// </summary>
        sealed class RayBuffers
        {
            public readonly int[] Edge = new int[64];
            public readonly double[] At = new double[64];
            public readonly bool[] End = new bool[64];
            public int[] KeptRay = new int[1024];
            public int[] KeptEdge = new int[1024];
            public double[] KeptKeep = new double[1024];
            public int Kept;

            public void Keep(int ray, int edge, double keep)
            {
                if (Kept == KeptRay.Length)
                {
                    Array.Resize(ref KeptRay, Kept * 2);
                    Array.Resize(ref KeptEdge, Kept * 2);
                    Array.Resize(ref KeptKeep, Kept * 2);
                }
                KeptRay[Kept] = ray;
                KeptEdge[Kept] = edge;
                KeptKeep[Kept++] = keep;
            }
        }

        /// <summary>One of her places: the rays asked from it, and where in its thread's buffers its kept rays lie.</summary>
        struct HerRays
        {
            public RayBuffers Of;
            public int From, To;
            public long Count;
        }

        /// <summary>
        /// One space's rays, as the one-thread rule asks them (for each of her places in order, the guarded floor in view in
        /// order, then with work order 083's guard her feet and her head), asked about at once on several threads against the
        /// rise as it stands now; each ray today's walls leave free that crosses a shown rock above the height it must keep
        /// under is kept (<see cref="Consider"/>). Then, on this thread, those rays are run in the one-thread rule's order on
        /// the rise as it is lowered: a ray left out could lower nothing, as the rise only ever goes down, so the rise and the
        /// counts are the one-thread rule's.
        /// </summary>
        void HoldDownAtOnce(List<LevelPoint> hers, List<LevelPoint> targets, bool[] shown)
        {
            int m = hers.Count;
            var found = new HerRays[m];
            Parallel.For(0, m, () => new RayBuffers(), (i, state, b) =>
            {
                LevelPoint her = hers[i];
                int from = b.Kept, ray = 0;
                for (int k = 0; k < targets.Count; k++)
                {
                    LevelPoint t = targets[k];
                    if (!InView(her, t)) continue;
                    Consider(her, t, shown, b, ray++);
                }
                if (_v.GuardHerAndDoors)
                {
                    Consider(her, her, shown, b, ray++);
                    Consider(her, her, shown, b, ray++, _v.HerHeight);
                }
                found[i] = new HerRays { Of = b, From = from, To = b.Kept, Count = ray };
                return b;
            }, b => { });
            for (int i = 0; i < m; i++)
            {
                HerRays r = found[i];
                Rays += r.Count;
                for (int k = r.From; k < r.To;)
                {
                    int ray = r.Of.KeptRay[k];
                    bool any = false;
                    for (; k < r.To && r.Of.KeptRay[k] == ray; k++)
                        if (Lower(r.Of.KeptEdge[k], r.Of.KeptKeep[k])) any = true;
                    if (any) Lowered++;
                }
            }
        }

        /// <summary>
        /// <see cref="Ray"/>'s crossings and its test of today's walls, with the same arithmetic, into one thread's buffers; when
        /// today's walls leave the ray free, each shown rock it crosses that stands, as the rise is now, above the height it must
        /// keep under is kept in the buffers under the ray's number <paramref name="ray"/>, in the order Ray asks them. Reads
        /// the rise, never writes it.
        /// </summary>
        void Consider(LevelPoint her, LevelPoint t, bool[] shown, RayBuffers b, int ray, double th = 0.0)
        {
            double cx = her.X, cy = her.Y - CameraBack;
            double dx = cx - t.X, dy = cy - t.Y, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6 || th >= CameraHeight) return;
            double ux = dx / len, uy = dy / len, slope = (CameraHeight - th) / len;
            double reach = _v.RockReach, margin = _v.SightMargin;
            double far = Math.Min(len, Math.Max(0.0, Math.Max(_v.RiseHeight, WallTop) + margin - th) / slope + reach);
            double cs = Layout.CellSize;
            int[] hitEdge = b.Edge;
            double[] hitAt = b.At;
            bool[] hitEnd = b.End;
            int hits = 0;
            void Hit(int edge, double d, bool end)
            {
                if (edge < 0 || hits >= hitEdge.Length) return;
                hitEdge[hits] = edge;
                hitAt[hits] = d;
                hitEnd[hits++] = end;
            }
            // Vertical lines.
            if (Math.Abs(ux) > 1e-9)
            {
                int step = ux > 0 ? 1 : -1;
                int k = ux > 0 ? (int)Math.Floor(t.X / cs) + 1 : (int)Math.Ceiling(t.X / cs) - 1;
                for (; hits < hitEdge.Length; k += step)
                {
                    double d = (k * cs - t.X) / ux;
                    if (d > far) break;
                    double y = t.Y + uy * d;
                    int cell = (int)Math.Floor(y / cs);
                    double into = y - cell * cs;
                    Hit(Index(true, k, cell), d, false);
                    if (into < reach) Hit(Index(true, k, cell - 1), d, true);
                    else if (into > cs - reach) Hit(Index(true, k, cell + 1), d, true);
                }
            }
            // Horizontal lines.
            if (Math.Abs(uy) > 1e-9)
            {
                int step = uy > 0 ? 1 : -1;
                int k = uy > 0 ? (int)Math.Floor(t.Y / cs) + 1 : (int)Math.Ceiling(t.Y / cs) - 1;
                for (; hits < hitEdge.Length; k += step)
                {
                    double d = (k * cs - t.Y) / uy;
                    if (d > far) break;
                    double x = t.X + ux * d;
                    int cell = (int)Math.Floor(x / cs);
                    double into = x - cell * cs;
                    int i = Index(false, k, cell);
                    Hit(i < 0 ? -1 : _todayV.Length + i, d, false);
                    int n = into < reach ? Index(false, k, cell - 1) : into > cs - reach ? Index(false, k, cell + 1) : -1;
                    if (n >= 0) Hit(_todayV.Length + n, d, true);
                }
            }
            // Today: hidden by any wall standing above the line where it crosses.
            for (int h = 0; h < hits; h++)
            {
                if (hitEnd[h]) continue;
                int e = hitEdge[h];
                float today = e < _todayV.Length ? _todayV[e] : _todayH[e - _todayV.Length];
                if (today > th + slope * hitAt[h]) return;
            }
            for (int h = 0; h < hits; h++)
            {
                int e = hitEdge[h];
                int a = _ownerA[e], o = _ownerB[e];
                if (!((a >= 0 && shown[a]) || (o >= 0 && shown[o]))) continue; // not drawn in the cave: hides nothing
                bool v = e < _todayV.Length;
                int j = v ? e : e - _todayV.Length;
                float today = v ? _todayV[j] : _todayH[j];
                float cave = v ? _riseV[j] : _riseH[j];
                if (cave <= today) continue;
                double keep = th + slope * Math.Max(0.0, hitAt[h] - reach) - margin; // the rock's top must stay under this
                if (cave <= keep) continue;
                b.Keep(ray, e, keep);
            }
        }

        /// <summary>
        /// The lowering of <see cref="Ray"/> for one edge of a ray <see cref="Consider"/> kept, on the rise as it stands now:
        /// true if it was lowered.
        /// </summary>
        bool Lower(int e, double keep)
        {
            bool v = e < _todayV.Length;
            int j = v ? e : e - _todayV.Length;
            float today = v ? _todayV[j] : _todayH[j];
            float cave = v ? _riseV[j] : _riseH[j];
            if (cave <= today) return false;
            if (cave <= keep) return false;
            float to = (float)Math.Max(today, keep);
            if (v) _riseV[j] = to;
            else _riseH[j] = to;
            return true;
        }

        /// <summary>
        /// The far walls of the dark look (work order 083): every full section that is far for the game's camera
        /// (<see cref="CaveWalls"/>) rises to <see cref="CaveValues.FarHeight"/>, held at the kit's height wherever it would
        /// stand between the camera and her, her room's floor or the openings of its doors on a line that the look's other
        /// walls leave free (its near walls are <see cref="CaveValues.LipHeight"/>); the cells past the doors and the rooms
        /// behind are not guarded: a tall wall covering them is wanted ("another zone"). No rock tops. Plain data.
        /// </summary>
        public static CaveRise MakeDark(LevelLayout layout, CryptKitPlan kit, CaveValues v, float wallTop, float pitch, float distance)
        {
            v = v ?? new CaveValues();
            return Make(layout, kit, DarkRules(v), wallTop, Math.Min(wallTop, v.LipHeight), pitch, distance);
        }

        /// <summary>The values the dark look's rule runs on: 076's rule with the far height, its own margin and reach, and its own guards.</summary>
        public static CaveValues DarkRules(CaveValues v) => new CaveValues
        {
            TallWalls = true,
            RiseHeight = v.FarHeight,
            SightMargin = v.FarSightMargin,
            RockReach = v.FarReach,
            MinRise = v.MinRise,
            GuardNeighbours = false,
            GuardDoorCells = false,
            GuardHerAndDoors = true,
            HerHeight = v.HerHeight,
            RockLids = false,
            LidReach = 0,
            FarHeight = v.FarHeight,
            LipHeight = v.LipHeight,
            Look = CaveLook.Dark,
        };

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
        bool Ray(LevelPoint her, LevelPoint t, bool[] shown, bool lower, double th = 0.0)
        {
            double cx = her.X, cy = her.Y - CameraBack;
            double dx = cx - t.X, dy = cy - t.Y, len = Math.Sqrt(dx * dx + dy * dy);
            if (len < 1e-6 || th >= CameraHeight) return false;
            // The line from the point, th over the floor (0 the floor, her head for work order 083), up to the camera.
            double ux = dx / len, uy = dy / len, slope = (CameraHeight - th) / len;
            double reach = _v.RockReach, margin = _v.SightMargin;
            double far = Math.Min(len, Math.Max(0.0, Math.Max(_v.RiseHeight, WallTop) + margin - th) / slope + reach);
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
                if (today > th + slope * _hitAt[h]) return false;
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
                double keep = th + slope * Math.Max(0.0, _hitAt[h] - reach) - margin; // the rock's top must stay under this
                if (cave <= keep) continue;
                if (!lower) return true;
                float to = (float)Math.Max(today, keep);
                if (v) _riseV[j] = to;
                else _riseH[j] = to;
                any = true;
            }
            // The rock tops over the cells the line passes over near the floor (every one taken as drawn), cell by cell from the
            // floor point, each at the line's height where it enters the cell (its lowest over it): one that would stand within
            // the margin of the line is lowered to the low walls' height, or to a rock floor if even that would.
            if (_lid != null && th <= 0.0)
            {
                double lidFar = Math.Min(len, (WallTop + margin) / slope);
                double px = t.X / cs, py = t.Y / cs;
                int gx = (int)Math.Floor(px), gy = (int)Math.Floor(py);
                int sx = ux > 0 ? 1 : -1, sy = uy > 0 ? 1 : -1;
                double ax = Math.Abs(ux), ay = Math.Abs(uy);
                double nextX = ax > 1e-12 ? (ux > 0 ? gx + 1 - px : px - gx) * cs / ax : double.MaxValue;
                double nextY = ay > 1e-12 ? (uy > 0 ? gy + 1 - py : py - gy) * cs / ay : double.MaxValue;
                double stepX = ax > 1e-12 ? cs / ax : double.MaxValue, stepY = ay > 1e-12 ? cs / ay : double.MaxValue;
                for (double d = 0.0; d < lidFar;)
                {
                    int x = gx - X0, y = gy - Y0;
                    if (nextX < nextY) { d = nextX; nextX += stepX; gx += sx; }
                    else { d = nextY; nextY += stepY; gy += sy; }
                    if (d >= lidFar) break;
                    x = gx - X0;
                    y = gy - Y0;
                    if (x < 0 || y < 0 || x >= Nx || y >= Ny) continue;
                    int i = y * Nx + x;
                    float top = _lid[i];
                    if (top <= GroundTop + 1e-4f || _capped[i]) continue; // a rock floor hides nothing; a capped cell hides no more than today
                    double line = slope * d;
                    // A top at the low walls' height keeps only a tenth of a metre under the line: it stands where they stand,
                    // and the full margin would take away every top behind a low wall that a line passes just over.
                    double lowMargin = Math.Min(margin, 0.1);
                    if (top + (top <= LowTop + 1e-4f ? lowMargin : margin) <= line) continue;
                    if (!lower) return true;
                    _lid[i] = LowTop + lowMargin <= line ? Math.Min(top, LowTop) : GroundTop; // round 3's pictures: taken away, they read as chasms
                    any = true;
                }
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
        public bool NewlyHidden(LevelPoint her, LevelPoint t, bool[] shown) => NewlyHidden(her, t, shown, 0.0);

        /// <summary>As <see cref="NewlyHidden(LevelPoint, LevelPoint, bool[])"/>, for a point <paramref name="height"/> over the floor (her head, work order 083).</summary>
        public bool NewlyHidden(LevelPoint her, LevelPoint t, bool[] shown, double height)
        {
            double m = _v.SightMargin;
            _v.SightMargin = 0f;
            try { return Ray(her, t, shown, false, height); }
            finally { _v.SightMargin = (float)m; }
        }

        /// <summary>The values the rule ran on (for the dark look, <see cref="DarkRules"/>).</summary>
        public CaveValues Rules => _v;
    }
}
