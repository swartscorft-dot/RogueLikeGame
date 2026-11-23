using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the rooms you can use (work order 088; Kristian, 2026-11-22: "Døra stenges bak deg, men vi bør balansere
    /// rommene med flere muligheter og, slik at man kan bruke miljøet til sin fordel", and of the duck, "Plasser dekning for den"):
    /// the joined floor (<see cref="JoinedFloor"/>), the chain of tall cover and the low cover for the duck (<see cref="FightCover"/>).
    /// First guesses from the numbers the order builds from (after work order 087): the shooter keeps 7 to 10 m and shoots from 11 m,
    /// her run is 6 m/s and her dash 6 m, the fight's line 5 m in, the duck needs a low prop within 1 m of her on the line from the bow.
    /// Lengths in metres. The generator reads them, so changing one changes the floors of version 19.
    /// </summary>
    [Serializable]
    public sealed class CoverValues
    {
        // The joined floor
        public float JoinRadius = 0.8f;   // a square of floor is free this far from walls, carved cells and props: the NavMesh agent's 0.5 and a wall's half thickness, with room for the NavMesh's rounding; a gap under twice this shuts what is behind it
        public float WayClear = 1.6f;     // the way through: cell middles this far from every prop (072's PlayMode test: a metre from the NavMesh's edge, plus the agent's 0.5 and a little)

        // The chain of tall cover (point 2): from the line, cover to cover, toward the ranged enemies
        public int ChainFewest = 2;       // tall covers at least, between the fight's line and the far band (the producer's starting point)
        public float FirstMost = 8f;      // the first one's middle at most this far from the line's middle: one dash (6 m) or about a second's run (6 m/s), and the step behind it
        public float StepMost = 10f;      // between one's middle and the next's, at most: under two seconds' run
        public float StepLeast = 3f;      // and at least this far from it (and nearer the ranged enemies): a step on, not a second pillar beside the first
        public float HideBehind = 0.75f;  // her middle this far behind a cover's edge where she hides: her 0.5 and a quarter metre
        public float HideWidth = 1f;      // the lines from a ranged enemy to both her sides this far apart all meet the cover: she is hidden whole
        public float FirstIdeal = 5.5f;   // the generator puts a first one this far from the line's middle toward the ranged enemies when it must: inside one dash
        public float StepIdeal = 7f;      // and a next one this far on from the last toward them

        // The low cover for the duck (point 3)
        public int LowFewest = 2;         // low props on the shooters' lines at least, in a room that holds shooters
        public float DuckSpot = 0.75f;    // her middle this far behind a low prop's edge on the shooter's line: within the duck's reach of 1 m (FeelValues.DuckReach)
        public float LowFromShooter = 7f; // her spot at least this far from the shooter's place: where it stops walking in (ShooterValues.KeepNear), so the prop stays between them
        public float LowIdeal = 9f;       // the generator tries her spot this far from a shooter first: between where it keeps (7 to 10 m) and its reach (11 m)
        public float AcrossLeast = 45f;   // a long prop's long side at least this many degrees off the shooter's line (the most a turn by 90 degrees can always give)

        // Placing them
        public int TallAddMost = 3;       // tall covers the generator may add to a room, at most
        public int LowAddMost = 2;        // low covers it may add, at most
        public float SouthClear = 2.5f;   // an added tall cover keeps this far north of the low south wall and of a carved cell's low north face (ThemeValues.OpenFromWalls): the camera looks over them at her
    }

    /// <summary>
    /// Where a room's fight is fought (work order 088): its door in, the line where it wakes (<see cref="RoomFightRules.PointDepth"/>)
    /// and the line's middle (straight in from the middle of the door in), the room's depth from that door's wall and where its far band
    /// begins (<see cref="LevelValues.FarBand"/> of it: the generator stands shooters there), and the places its ranged enemies stand
    /// (every shooter and caster, the elite one too, as the generator put them).
    /// </summary>
    public sealed class FightFrame
    {
        public LevelRoom Room;
        public LevelDoor Entry;
        public double LineDepth, Depth, FarDepth;
        public LevelPoint LineMiddle;
        public readonly List<LevelPoint> Ranged = new List<LevelPoint>();
        public readonly List<LevelPoint> Shooters = new List<LevelPoint>();

        public bool HasRanged => Ranged.Count > 0;
        public bool HasShooters => Shooters.Count > 0;

        /// <summary>Metres from the point to the nearest ranged enemy's place; infinity with none.</summary>
        public double ToRanged(LevelPoint p)
        {
            double best = double.PositiveInfinity;
            foreach (LevelPoint q in Ranged) best = Math.Min(best, p.DistanceTo(q));
            return best;
        }

        /// <summary>The nearest ranged enemy's place to the point.</summary>
        public LevelPoint NearestRanged(LevelPoint p)
        {
            LevelPoint best = p;
            double d = double.PositiveInfinity;
            foreach (LevelPoint q in Ranged)
                if (p.DistanceTo(q) < d)
                {
                    d = p.DistanceTo(q);
                    best = q;
                }
            return best;
        }
    }

    /// <summary>A thing that breaks a line (work order 088): a tall prop's footprint, or a carved cell of the room (a pier, a wall's corner) as a square.</summary>
    public struct CoverThing
    {
        public PropFootprint Shape;
        public LevelPoint Middle;
        /// <summary>The prop; null for a carved cell.</summary>
        public LevelProp Prop;
    }

    /// <summary>
    /// Rooms you can use (work order 088, points 2 and 3; the draft room rule 14: "the room is a puzzle with more than one answer:
    /// break a shooter's line behind something tall, or go down behind something low and stay there, or cross open floor fast").
    /// The rules, as numbers a validator checks over every seed (<see cref="CoverValues"/>):
    ///
    /// - The chain of tall cover. A room with a fight (a combat room, the elite room or the exit room) that holds shooters or
    ///   casters has a chain of at least <see cref="CoverValues.ChainFewest"/> tall covers (a tall prop by <see cref="PropHeights"/>,
    ///   or a carved cell of the room), each with its middle between the fight's line and the far band (straight in from the wall of
    ///   the door in), the first within <see cref="CoverValues.FirstMost"/> of the line's middle, each next within
    ///   <see cref="CoverValues.StepMost"/> of the one before, at least <see cref="CoverValues.StepLeast"/> from it and nearer the
    ///   ranged enemies, the last within StepMost of the far band;
    ///   and each hides her from at least one ranged enemy's place (<see cref="Hides"/>): a free spot
    ///   <see cref="CoverValues.HideBehind"/> behind it, on the line from that place through its middle, from which the lines to
    ///   her two sides <see cref="CoverValues.HideWidth"/> apart both meet it.
    /// - The low cover. A room that holds shooters has at least <see cref="CoverValues.LowFewest"/> low props (low by
    ///   <see cref="PropHeights"/>) on a shooter's line (<see cref="LowServes"/>): from a shooter's place through the prop's
    ///   middle, her spot <see cref="CoverValues.DuckSpot"/> behind its edge (within the duck's reach) is free floor, past the
    ///   fight's line, at least <see cref="CoverValues.LowFromShooter"/> from the shooter (where it stops walking in), with nothing
    ///   tall and no carved cell between the shooter and the prop (an arrow does come that way), and a long prop is turned across
    ///   the line (<see cref="CoverValues.AcrossLeast"/>). A low prop does not stop an orb: a room with casters and no shooters
    ///   gets the chain of tall cover and no low cover for the duck.
    /// - A room with neither shooters nor casters needs neither.
    /// </summary>
    public static class FightCover
    {
        const double Slack = 1e-9;

        /// <summary>The fight of a room: null for a room with no fight (not a combat or exit room, or no door in).</summary>
        public static FightFrame FrameOf(LevelLayout level, LevelRoom room, LevelValues v)
        {
            if (level == null || room == null || (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit)) return null;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            if (entry == null || LevelGeometry.WallOf(room.Rect, entry) < 0) return null;
            v = v ?? new LevelValues();
            double cs = level.CellSize;
            var f = new FightFrame { Room = room, Entry = entry };
            f.LineDepth = RoomFightRules.PointDepth(level, room, entry, v.Fights ?? new RoomFightValues());
            f.Depth = RoomFightRules.Depth(level, room, entry);
            f.FarDepth = f.Depth * v.FarBand;
            entry.Opening(cs, out LevelPoint a, out LevelPoint b);
            LevelPoint into = PropRules.Inward(LevelGeometry.WallOf(room.Rect, entry));
            f.LineMiddle = new LevelPoint((a.X + b.X) * 0.5 + into.X * f.LineDepth, (a.Y + b.Y) * 0.5 + into.Y * f.LineDepth);
            for (int i = 0; i < room.EnemyPoints.Count; i++)
            {
                EnemyGroup g = room.GroupAt(i);
                bool shooter = g.Kind == EnemyKind.Shooter, caster = g.Kind == EnemyKind.Caster;
                if (!shooter && !caster) continue;
                foreach (LevelPoint m in room.PlacesAt(i))
                {
                    f.Ranged.Add(m);
                    if (shooter) f.Shooters.Add(m);
                }
            }
            return f;
        }

        /// <summary>Metres the point stands in from the wall of the fight's door in.</summary>
        public static double DepthOf(LevelLayout level, FightFrame f, LevelPoint p) => RoomFightRules.DepthIn(level, f.Room, f.Entry, p);

        /// <summary>
        /// Can she stand at the point: on an open cell of the room, at least the walk radius (<see cref="PropValues.WalkRadius"/>)
        /// from its walls, its carved cells and every prop (and <paramref name="extra"/>)?
        /// </summary>
        public static bool FreeSpot(LevelLayout level, LevelRoom room, LevelPoint s, LevelValues v, LevelProp extra = null)
        {
            double cs = level.CellSize, r = (v?.PropRules ?? new PropValues()).WalkRadius;
            if (!room.IsOpen((int)Math.Floor(s.X / cs), (int)Math.Floor(s.Y / cs))) return false;
            if (RoomShapes.DistanceToWalls(room, s, cs) < r - Slack) return false;
            foreach (LevelProp p in room.Props)
                if (!PropSize.Of(p.Model).Walkable && p.Footprint.DistanceTo(s) < r - Slack) return false;
            return extra == null || extra.Footprint.DistanceTo(s) >= r - Slack;
        }

        /// <summary>How far the shape reaches from its middle along the unit <paramref name="u"/>.</summary>
        static double Reach(PropFootprint f, LevelPoint u)
        {
            if (f.Shape == PropShape.Circle) return f.Radius;
            double best = 0;
            foreach (LevelPoint c in f.Corners()) best = Math.Max(best, (c.X - f.Centre.X) * u.X + (c.Y - f.Centre.Y) * u.Y);
            return best;
        }

        static LevelPoint Unit(LevelPoint from, LevelPoint to)
        {
            double dx = to.X - from.X, dy = to.Y - from.Y, l = Math.Sqrt(dx * dx + dy * dy);
            return l < 1e-9 ? new LevelPoint(0, 0) : new LevelPoint(dx / l, dy / l);
        }

        /// <summary>
        /// Does the thing hide her from a ranged enemy at <paramref name="q"/>: the spot <see cref="CoverValues.HideBehind"/> behind
        /// its edge on the line from q through its middle is free floor (<see cref="FreeSpot"/>), and the lines from q to her two
        /// sides, <see cref="CoverValues.HideWidth"/> apart, both meet it?
        /// </summary>
        public static bool Hides(LevelLayout level, LevelRoom room, CoverThing t, LevelPoint q, LevelValues v, LevelProp extra = null)
        {
            CoverValues cv = v?.CoverRules ?? new CoverValues();
            LevelPoint u = Unit(q, t.Middle);
            if (u.X == 0 && u.Y == 0) return false;
            double out1 = Reach(t.Shape, u) + cv.HideBehind;
            var s = new LevelPoint(t.Middle.X + u.X * out1, t.Middle.Y + u.Y * out1);
            if (!FreeSpot(level, room, s, v, extra)) return false;
            double half = cv.HideWidth * 0.5;
            var side = new LevelPoint(-u.Y * half, u.X * half);
            return t.Shape.DistanceTo(q, new LevelPoint(s.X + side.X, s.Y + side.Y)) <= Slack
                && t.Shape.DistanceTo(q, new LevelPoint(s.X - side.X, s.Y - side.Y)) <= Slack;
        }

        /// <summary>A carved cell of the room as a thing that breaks a line.</summary>
        static CoverThing CellThing(int x, int y, double cs) => new CoverThing
        {
            Shape = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs), HalfWidth = cs * 0.5, HalfDepth = cs * 0.5 },
            Middle = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs),
        };

        /// <summary>A tall prop as a thing that breaks a line.</summary>
        public static CoverThing PropThing(LevelProp p) => new CoverThing { Shape = p.Footprint, Middle = p.Position, Prop = p };

        /// <summary>Is the prop tall cover: tall by the one table, and not slender.</summary>
        public static bool IsTall(LevelProp p) => p != null && !p.Slender && PropHeights.Tall(p.Model);

        /// <summary>Is the prop low cover: low by the one table, and not slender nor walked over.</summary>
        public static bool IsLow(LevelProp p) => p != null && !p.Slender && !PropSize.Of(p.Model).Walkable && PropHeights.Low(p.Model);

        /// <summary>
        /// The links a chain may be made of (see the class): the room's tall props and carved cells whose middles stand between the
        /// fight's line and the far band and that hide her from at least one ranged enemy; <paramref name="extra"/> among them when
        /// it is tall and does.
        /// </summary>
        public static List<CoverThing> Links(LevelLayout level, FightFrame f, LevelValues v, LevelProp extra = null)
        {
            var list = new List<CoverThing>();
            LevelRoom room = f.Room;
            double cs = level.CellSize;
            bool InBand(LevelPoint p)
            {
                double d = DepthOf(level, f, p);
                return d >= f.LineDepth - Slack && d <= f.FarDepth + Slack;
            }
            bool HidesAny(CoverThing t)
            {
                foreach (LevelPoint q in f.Ranged)
                    if (Hides(level, room, t, q, v, extra)) return true;
                return false;
            }
            foreach (LevelProp p in room.Props)
                if (IsTall(p) && InBand(p.Position))
                {
                    CoverThing t = PropThing(p);
                    if (HidesAny(t)) list.Add(t);
                }
            if (extra != null && IsTall(extra) && InBand(extra.Position))
            {
                CoverThing t = PropThing(extra);
                if (HidesAny(t)) list.Add(t);
            }
            foreach ((int x, int y) in room.SolidList)
            {
                CoverThing t = CellThing(x, y, cs);
                if (InBand(t.Middle) && HidesAny(t)) list.Add(t);
            }
            return list;
        }

        /// <summary>
        /// The longest chain the links make (see the class), and its last link (null with none; of two as long, the one nearer the
        /// ranged enemies); <paramref name="holds"/> when some chain of at least <see cref="CoverValues.ChainFewest"/> links reaches
        /// the far band.
        /// </summary>
        public static int Chain(LevelLayout level, FightFrame f, List<CoverThing> links, LevelValues v, out CoverThing? end, out bool holds)
        {
            int[] length = Lengths(level, f, links, v, out double[] near, out holds);
            end = null;
            int best = -1;
            for (int i = 0; i < links.Count; i++)
            {
                if (length[i] == 0) continue;
                if (best < 0 || length[i] > length[best] || (length[i] == length[best] && near[i] < near[best] - Slack)) best = i;
            }
            if (best < 0) return 0;
            end = links[best];
            return length[best];
        }

        /// <summary>
        /// The middles of every link that ends a chain from the line (see the class), the longest chain's first, of two as long the
        /// one nearer the ranged enemies: where the generator extends a chain from (<paramref name="holds"/> as <see cref="Chain"/>'s).
        /// </summary>
        public static List<LevelPoint> Ends(LevelLayout level, FightFrame f, List<CoverThing> links, LevelValues v, out bool holds)
        {
            int[] length = Lengths(level, f, links, v, out double[] near, out holds);
            return Enumerable.Range(0, links.Count).Where(i => length[i] > 0).OrderByDescending(i => length[i]).ThenBy(i => near[i]).ThenBy(i => i)
                             .Select(i => links[i].Middle).ToList();
        }

        /// <summary>For each link, the most links of a chain from the line that ends at it (0: none reaches it).</summary>
        static int[] Lengths(LevelLayout level, FightFrame f, List<CoverThing> links, LevelValues v, out double[] near, out bool holds)
        {
            CoverValues cv = v?.CoverRules ?? new CoverValues();
            holds = false;
            int n = links.Count;
            var to = new double[n];
            near = to;
            var length = new int[n];
            if (n == 0) return length;
            for (int i = 0; i < n; i++) to[i] = f.ToRanged(links[i].Middle);
            int[] order = Enumerable.Range(0, n).OrderByDescending(i => to[i]).ThenBy(i => i).ToArray();
            for (int i = 0; i < n; i++) length[i] = links[i].Middle.DistanceTo(f.LineMiddle) <= cv.FirstMost + Slack ? 1 : 0;
            for (int a = 0; a < n; a++)
            {
                int i = order[a];
                if (length[i] == 0) continue;
                for (int b = a + 1; b < n; b++)
                {
                    int j = order[b];
                    if (to[j] >= to[i] - Slack) continue;
                    double apart = links[i].Middle.DistanceTo(links[j].Middle);
                    if (apart > cv.StepMost + Slack || apart < cv.StepLeast - Slack) continue;
                    if (length[i] + 1 > length[j]) length[j] = length[i] + 1;
                }
            }
            for (int i = 0; i < n; i++)
                if (length[i] >= cv.ChainFewest && f.FarDepth - DepthOf(level, f, links[i].Middle) <= cv.StepMost + Slack) holds = true;
            return length;
        }

        /// <summary>The chain rule holds for the room's fight (see the class): true for a fight with no ranged enemy.</summary>
        public static bool ChainHolds(LevelLayout level, FightFrame f, LevelValues v)
        {
            if (f == null || !f.HasRanged) return true;
            Chain(level, f, Links(level, f, v), v, out _, out bool holds);
            return holds;
        }

        /// <summary>
        /// Does the low prop serve the duck against the shooter at <paramref name="q"/> (see the class)? <paramref name="extra"/>
        /// stands too (the generator's prop being tried, when it is not the low prop itself).
        /// </summary>
        public static bool LowServes(LevelLayout level, FightFrame f, LevelProp low, LevelPoint q, LevelValues v, LevelProp extra = null)
        {
            if (!IsLow(low)) return false;
            CoverValues cv = v?.CoverRules ?? new CoverValues();
            LevelRoom room = f.Room;
            double cs = level.CellSize;
            PropFootprint shape = low.Footprint;
            LevelPoint u = Unit(q, low.Position);
            if (u.X == 0 && u.Y == 0) return false;
            double reach = Reach(shape, u);
            var s = new LevelPoint(low.Position.X + u.X * (reach + cv.DuckSpot), low.Position.Y + u.Y * (reach + cv.DuckSpot));
            if (q.DistanceTo(s) < cv.LowFromShooter - Slack) return false;
            if (DepthOf(level, f, s) < f.LineDepth - Slack) return false;
            if (!FreeSpot(level, room, s, v, ReferenceEquals(extra, low) ? null : extra)) return false;
            if (shape.DistanceTo(q, s) > Slack) return false;
            // A long prop turned across the line.
            if (low.Shape == PropShape.Rectangle && Math.Max(low.Width, low.Depth) > Math.Min(low.Width, low.Depth) * 1.2)
            {
                double a = low.Rotation * Math.PI / 180.0;
                LevelPoint ax = low.Width >= low.Depth ? new LevelPoint(Math.Cos(a), -Math.Sin(a)) : new LevelPoint(Math.Sin(a), Math.Cos(a));
                double cos = Math.Abs(ax.X * u.X + ax.Y * u.Y), off = Math.Acos(Math.Min(1, cos)) * 180.0 / Math.PI;
                if (off < cv.AcrossLeast - 1e-6) return false;
            }
            // An arrow comes that way: nothing tall and no carved cell between the shooter and the prop's near edge.
            var near = new LevelPoint(low.Position.X - u.X * reach, low.Position.Y - u.Y * reach);
            foreach (LevelProp p in room.Props)
                if (!ReferenceEquals(p, low) && IsTall(p) && p.Footprint.DistanceTo(q, near) <= Slack) return false;
            if (extra != null && !ReferenceEquals(extra, low) && IsTall(extra) && extra.Footprint.DistanceTo(q, near) <= Slack) return false;
            double length = q.DistanceTo(near);
            for (double t = 0; t <= length; t += 0.25)
            {
                var p = new LevelPoint(q.X + u.X * t, q.Y + u.Y * t);
                if (room.IsSolid((int)Math.Floor(p.X / cs), (int)Math.Floor(p.Y / cs))) return false;
            }
            return true;
        }

        /// <summary>The low props of the room that serve the duck against at least one of its shooters (and <paramref name="extra"/>, when low and serving).</summary>
        public static int LowCount(LevelLayout level, FightFrame f, LevelValues v, LevelProp extra = null)
        {
            if (f == null || !f.HasShooters) return 0;
            int n = 0;
            IEnumerable<LevelProp> props = extra != null ? f.Room.Props.Concat(new[] { extra }) : f.Room.Props;
            foreach (LevelProp p in props)
            {
                if (!IsLow(p)) continue;
                foreach (LevelPoint q in f.Shooters)
                    if (LowServes(level, f, p, q, v, extra))
                    {
                        n++;
                        break;
                    }
            }
            return n;
        }

        /// <summary>The low rule holds for the room's fight (see the class): true for a fight with no shooter.</summary>
        public static bool LowHolds(LevelLayout level, FightFrame f, LevelValues v) =>
            f == null || !f.HasShooters || LowCount(level, f, v) >= (v?.CoverRules ?? new CoverValues()).LowFewest;

        /// <summary>
        /// What the rules of points 2 and 3 find against a room, in words, added to <paramref name="faults"/>: the measure the tests
        /// ask of every seed (the generator does not refuse a floor for them: a room it could not give its cover is counted).
        /// </summary>
        public static void Faults(LevelLayout level, LevelRoom room, LevelValues v, List<string> faults)
        {
            FightFrame f = FrameOf(level, room, v);
            if (f == null || !f.HasRanged) return;
            string name = room.Letter + room.Order.ToString(CultureInfo.InvariantCulture);
            CoverValues cv = v?.CoverRules ?? new CoverValues();
            int n = Chain(level, f, Links(level, f, v), v, out _, out bool holds);
            if (!holds)
                faults.Add($"{name} has no chain of {cv.ChainFewest} tall covers from its line to its far band (its longest is {n})");
            if (f.HasShooters)
            {
                int low = LowCount(level, f, v);
                if (low < cv.LowFewest) faults.Add($"{name} has {low} low props on its shooters' lines, and a room with shooters {cv.LowFewest} at least");
            }
        }

        // ---------------- What a room is given ----------------

        /// <summary>The tall model a room of this theme is given for its cover (work order 088): the ossuary its cage, every other a pillar.</summary>
        public static PropModel TallModel(RoomTheme theme) => theme == RoomTheme.Ossuary ? PropModel.Cage : PropModel.Pillar;

        /// <summary>
        /// The low model a room of this theme is given for the duck: the tomb hall a sarcophagus, every other rubble (the throne
        /// room's braziers and basins are taken by the mood's great braziers, and the shrine's basin by its great altar).
        /// </summary>
        public static PropModel LowModel(RoomTheme theme) => theme == RoomTheme.TombHall ? PropModel.Sarcophagus : PropModel.Rubble;

        /// <summary>The models a room may hold besides its theme's (or its kind's) with the rooms you can use on: its cover's.</summary>
        public static PropModel[] CoverModels(LevelRoom room) =>
            room == null || (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) ? new PropModel[0] : new[] { TallModel(room.Theme), LowModel(room.Theme) };

        /// <summary>The props a room may hold besides its theme's (or its kind's) most with the rooms you can use on: what the generator may add.</summary>
        public static int CoverMost(LevelRoom room, LevelValues v)
        {
            if (room == null || (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit)) return 0;
            CoverValues cv = v?.CoverRules ?? new CoverValues();
            return Math.Max(0, cv.TallAddMost) + Math.Max(0, cv.LowAddMost);
        }
    }
}
