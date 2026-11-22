using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// The room as a cave (work order 076), its rules: what is shown where she stands (<see cref="CaveSight"/>), how high the
    /// rock over the walls rises and that it never hides her or a floor she can see where today's walls leave it seen
    /// (<see cref="CaveRise"/>), and the treasure room without the vault's large coin heaps, its chest's front reached.
    /// </summary>
    public class CavePlanTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly FeelValues Feel = new FeelValues();

        static CaveRise Rise(LevelLayout l, LevelValues v) =>
            CaveRise.Make(l, CryptKitPlan.Make(l, v.Crypt), v.CaveRules, v.WallHeight, v.NearWallHeight, Feel.CameraPitch, Feel.CameraDistance);

        [Test]
        public void Shown_InARoom_ItsPassages_InAPassage_TheRoomsAtBothEnds_AndNothingElse()
        {
            var v = new LevelValues();
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
            {
                LevelLayout l = LevelGenerator.Generate(seed, 1, v);
                var shown = new bool[l.Rooms.Count];
                foreach (LevelRoom r in l.Rooms)
                {
                    CaveSight.Shown(l, r.Order, shown);
                    var expect = new HashSet<int> { r.Order };
                    foreach (LevelDoor d in l.Doors.Where(d => d.Joins(r.Order))) expect.Add(d.Other(r.Order));
                    for (int o = 0; o < shown.Length; o++)
                        Assert.That(shown[o], Is.EqualTo(expect.Contains(o)), "seed " + seed + " in " + r.Letter + r.Order + ": " + l.Rooms[o].Letter + o);
                    if (r.Kind == RoomKind.Passage)
                    {
                        Assert.That(expect.Count, Is.EqualTo(3), "a passage and the rooms at its ends");
                        Assert.That(expect.Where(o => o != r.Order).All(o => l.Rooms[o].Kind != RoomKind.Passage));
                        Assert.That(shown[r.Order + 1], Is.True, "the room ahead, while she is still in the passage");
                    }
                    else Assert.That(expect.Where(o => o != r.Order).All(o => l.Rooms[o].Kind == RoomKind.Passage), "in a room, only the passages at its doors");
                }
                CaveSight.Shown(l, -1, shown);
                Assert.That(shown.Any(b => b), Is.False, "nothing for no room");
            }
        }

        /// <summary>Her places in a room for the sight test: inside the door she enters by, the middle, the four corners (each nudged to an open cell).</summary>
        static List<LevelPoint> Places(LevelLayout l, LevelRoom r)
        {
            double cs = l.CellSize;
            var list = new List<LevelPoint>();
            LevelPoint Open(LevelPoint p)
            {
                if (l.RoomAt(p.X, p.Y) == r) return p;
                LevelPoint best = p;
                double most = double.MaxValue;
                for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                    for (int x = r.Rect.X; x < r.Rect.Right; x++)
                    {
                        if (!r.IsOpen(x, y)) continue;
                        var c = new LevelPoint((x + 0.5) * cs, (y + 0.5) * cs);
                        double d = c.DistanceTo(p);
                        if (d < most) { most = d; best = c; }
                    }
                return best;
            }
            LevelPoint m = r.Middle(cs);
            LevelDoor door = LevelGeometry.EntryDoor(l, r);
            if (door != null)
            {
                door.Opening(cs, out LevelPoint a, out LevelPoint b);
                var dm = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                list.Add(Open(new LevelPoint(dm.X + (door.Vertical ? Math.Sign(m.X - dm.X) * 1.0 : 0), dm.Y + (door.Vertical ? 0 : Math.Sign(m.Y - dm.Y) * 1.0))));
            }
            list.Add(Open(m));
            double x0 = r.Rect.X * cs + 0.75, x1 = r.Rect.Right * cs - 0.75, y0 = r.Rect.Y * cs + 0.75, y1 = r.Rect.Top * cs - 0.75;
            foreach (LevelPoint c in new[] { new LevelPoint(x0, y0), new LevelPoint(x1, y0), new LevelPoint(x0, y1), new LevelPoint(x1, y1) }) list.Add(Open(c));
            return list;
        }

        [Test]
        public void TheRock_NeverHidesHer_NorAFloorSheSees_ThatTodaysWallsLeaveSeen_ThreeSeeds()
        {
            var v = new LevelValues();
            double[] fine = { 0.25, 0.75, 1.25, 1.75, 2.25, 2.75, 3.25, 3.75 };
            long asked = 0, rays = 0;
            var watch = Stopwatch.StartNew();
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
                foreach (int floor in new[] { 1, 5 })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    CaveRise rise = Rise(l, v);
                    rays += rise.Rays;
                    var shown = new bool[l.Rooms.Count];
                    var faults = new List<string>();
                    foreach (LevelRoom r in l.Rooms)
                    {
                        CaveSight.Shown(l, r.Order, shown);
                        // Her room's or passage's whole floor and the first cell past each of its doors (CaveValues.GuardNeighbours
                        // off, the morning of 2026-11-22), at eight points a side of every cell.
                        List<LevelPoint> floorPoints = rise.Guarded(r.Order, shown, fine);
                        Assert.That(floorPoints.Count, Is.GreaterThanOrEqualTo(rise.Points(r.Order, fine).Count + 64), "her floor and at least one door's cell");
                        List<LevelPoint> places = r.Kind == RoomKind.Passage ? rise.Points(r.Order, new[] { 2.0 }) : Places(l, r);
                        foreach (LevelPoint her in places)
                        {
                            Assert.That(l.RoomAt(her.X, her.Y), Is.SameAs(r), "her place is in the room");
                            foreach (LevelPoint t in floorPoints.Append(her))
                            {
                                if (!CaveRise.InView(her, t)) continue;
                                asked++;
                                if (rise.NewlyHidden(her, t, shown) && faults.Count < 10)
                                    faults.Add(string.Format(Inv, "{0}{1} her ({2:0.##}, {3:0.##}) hides ({4:0.##}, {5:0.##})", r.Letter, r.Order, her.X, her.Y, t.X, t.Y));
                            }
                        }
                    }
                    Assert.That(faults, Is.Empty, "seed " + seed + " floor " + floor + ": the rock hides what today's walls leave seen");
                }
            TestContext.WriteLine(string.Format(Inv, "CAVESIGHT 3 seeds x floors 1 and 5: {0} lines asked, none newly hidden; the rule asked {1} rays making the six floors' rises, {2:0} ms in all",
                                                asked, rays, watch.Elapsed.TotalMilliseconds));
        }

        [Test]
        public void TheRock_RisesOverTheFullWalls_ByTheValues_NeverOverALowWallOrADoor_AndOffIsToday()
        {
            var v = new LevelValues();
            var lines = new List<string>();
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL })
            {
                LevelLayout l = LevelGenerator.Generate(seed, 1, v);
                CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                var watch = Stopwatch.StartNew();
                CaveRise rise = Rise(l, v);
                double ms = watch.Elapsed.TotalMilliseconds;
                int full = 0, risen = 0, all = 0;
                double sum = 0;
                foreach (CryptKitSection s in kit.Sections)
                {
                    float h = rise.RiseOf(s.Vertical, s.Line, s.Cell), today = rise.TodayOf(s.Vertical, s.Line, s.Cell);
                    if (s.Low)
                    {
                        Assert.That(h, Is.EqualTo(v.NearWallHeight), "a low wall stays low");
                        continue;
                    }
                    full++;
                    Assert.That(today, Is.EqualTo(v.WallHeight));
                    Assert.That(h, Is.InRange(v.WallHeight, v.CaveRules.RiseHeight + 1e-4), "between today's and the most");
                    if (h >= v.CaveRules.RiseHeight - 1e-4) all++;
                    if (h > today + v.CaveRules.MinRise) { risen++; sum += h; }
                }
                foreach (CryptKitArch a in kit.Arches) Assert.That(rise.HasRock(a.Vertical, a.Line, a.Cell), Is.False, "nothing over a door");
                // Round 2's rule (the whole floor of the spaces shown with hers guarded) let 75 to 80 of about 210 rise; the
                // morning's (her own and the cells past her doors) lets more. Most of those left bare stand round a template's
                // carved cells (an inner corner or a pillar between the camera and her floor), beside a door, or along a passage.
                Assert.That(risen, Is.GreaterThan(full / 3), "many full walls rise, seed " + seed);
                var whole = new LevelValues();
                whole.CaveRules.GuardNeighbours = true;
                CaveRise before = Rise(l, whole);
                int risenBefore = kit.Sections.Count(s => !s.Low && before.RiseOf(s.Vertical, s.Line, s.Cell) > before.TodayOf(s.Vertical, s.Line, s.Cell) + v.CaveRules.MinRise);
                Assert.That(risenBefore, Is.GreaterThan(full / 4).And.LessThan(risen), "round 2's rule by its value, seed " + seed);
                lines.Add(string.Format(Inv, "seed {0}: {1} full sections, {2} rise ({3} to the most, {4:0.0} m on average; {8} with round 2's rule), {5} rays, {6} lowered, {7:0} ms",
                                        seed, full, risen, all, risen > 0 ? sum / risen : 0, rise.Rays, rise.Lowered, ms, risenBefore));

                var off = new LevelValues();
                off.CaveRules.TallWalls = false;
                CaveRise none = Rise(l, off);
                foreach (CryptKitSection s in kit.Sections) Assert.That(none.RiseOf(s.Vertical, s.Line, s.Cell), Is.EqualTo(none.TodayOf(s.Vertical, s.Line, s.Cell)));
            }
            TestContext.WriteLine("CAVERISE " + string.Join("; ", lines));
        }

        /// <summary>
        /// Is the chest's front reached from the door the room is entered by along floor that keeps <paramref name="clear"/> from
        /// every prop that blocks walking (the chest itself kept a walk's radius from), and a walk's radius from the walls and
        /// carved cells? On a grid of a quarter metre, side to side.
        /// </summary>
        static bool ChestReached(LevelLayout l, LevelRoom r, List<LevelProp> blocking, LevelProp chest, double clear, double walk)
        {
            double cs = l.CellSize, step = 0.25, x0 = r.Rect.X * cs, y0 = r.Rect.Y * cs;
            int nx = (int)Math.Round(r.Rect.Width * cs / step), ny = (int)Math.Round(r.Rect.Height * cs / step);
            var open = new bool[nx * ny];
            LevelPoint C(int i, int j) => new LevelPoint(x0 + (i + 0.5) * step, y0 + (j + 0.5) * step);
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                {
                    LevelPoint c = C(i, j);
                    if (l.RoomAt(c.X, c.Y) != r) continue;
                    // A walk's radius from the walls and the carved cells: every cell round it within the radius is the room's.
                    bool ok = true;
                    foreach ((double dx, double dy) in new[] { (walk, 0.0), (-walk, 0.0), (0.0, walk), (0.0, -walk), (walk, walk), (-walk, walk), (walk, -walk), (-walk, -walk) })
                        if (l.RoomAt(c.X + dx * 0.999, c.Y + dy * 0.999) != r && !l.Doors.Any(d => d.Joins(r.Order) && DoorNear(d, c, cs, walk))) ok = false;
                    if (!ok) continue;
                    if (chest.Footprint.DistanceTo(c) < walk) continue;
                    if (blocking.Any(p => p.Footprint.DistanceTo(c) < clear)) continue;
                    open[j * nx + i] = true;
                }
            LevelDoor entry = LevelGeometry.EntryDoor(l, r);
            if (entry == null) return false;
            entry.Opening(cs, out LevelPoint a, out LevelPoint b);
            var dm = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            LevelPoint front = new LevelPoint(chest.Position.X + chest.Front.X * (chest.Depth * 0.5 + 0.75), chest.Position.Y + chest.Front.Y * (chest.Depth * 0.5 + 0.75));
            var queue = new Queue<int>();
            var seen = new bool[nx * ny];
            for (int k = 0; k < open.Length; k++)
                if (open[k] && C(k % nx, k / nx).DistanceTo(dm) <= 1.5) { seen[k] = true; queue.Enqueue(k); }
            while (queue.Count > 0)
            {
                int k = queue.Dequeue(), i = k % nx, j = k / nx;
                if (C(i, j).DistanceTo(front) <= 0.5) return true;
                foreach ((int di, int dj) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    int ni = i + di, nj = j + dj;
                    if (ni < 0 || nj < 0 || ni >= nx || nj >= ny) continue;
                    int n = nj * nx + ni;
                    if (seen[n] || !open[n]) continue;
                    seen[n] = true;
                    queue.Enqueue(n);
                }
            }
            return false;
        }

        static bool DoorNear(LevelDoor d, LevelPoint c, double cs, double walk)
        {
            d.Opening(cs, out LevelPoint a, out LevelPoint b);
            return LevelGeometry.DistanceToSegment(c, a, b) <= walk + 1e-9 && (d.Vertical ? c.Y > a.Y + walk && c.Y < b.Y - walk : c.X > a.X + walk && c.X < b.X - walk);
        }

        [Test]
        public void TheTreasureRoom_WithoutTheLargeCoinHeaps_TheWayFromTheDoorToTheChestsFrontIsClear_400Seeds()
        {
            var on = new LevelValues();
            var heapsOn = new LevelValues();
            heapsOn.CaveRules.NoGoldHeaps = false;
            var off = new LevelValues { Cave = false };
            double cs = on.CellSize, walk = on.PropRules.WalkRadius, clear = cs * 0.5; // a cell's width between the props round the way
            int rooms = 0, vaults = 0, heapsBefore = 0, reached = 0, reachedBefore = 0, heapsNow = 0, seenNow = 0, seenBefore = 0;
            var faults = new List<string>();
            for (ulong s = 1; s <= 400; s++)
            {
                int floor = 1 + (int)(s % 5);
                LevelLayout l = LevelGenerator.Generate(s, floor, on);
                RoomMoodPlan plan = RoomMoodPlan.Make(l, on), before = RoomMoodPlan.Make(l, heapsOn);
                Assert.That(RoomMoodPlan.Make(l, off).Describe(), Is.EqualTo(before.Describe()), "the cave off is the heaps' plan: seed " + s);
                foreach (LevelRoom r in l.Rooms.Where(x => x.Kind == RoomKind.Treasure))
                {
                    LevelProp chest = r.Props.FirstOrDefault(p => p.Model == PropModel.Chest);
                    if (chest == null) continue;
                    rooms++;
                    MoodRoom mr = plan.Rooms.First(m => m.Room == r), mb = before.Rooms.First(m => m.Room == r);
                    heapsNow += mr.LargeProps.Count(p => p.Model == PropModel.CoinHeap);
                    int had = mb.LargeProps.Count(p => p.Model == PropModel.CoinHeap);
                    if (r.Theme == RoomTheme.Vault) vaults++;
                    heapsBefore += had;
                    // The chest, its dais and its gold light stay.
                    Assert.That(r.Props.Contains(chest) && !mr.Replaced.Contains(chest), "the chest stays: seed " + s);
                    Assert.That(mr.LargeProps.Count(p => p.Model == PropModel.Dais), Is.EqualTo(mb.LargeProps.Count(p => p.Model == PropModel.Dais)), "its dais stays: seed " + s);
                    Assert.That(mr.Sources.Count(x => x.Kind == MoodSourceKind.Gold), Is.EqualTo(mb.Sources.Count(x => x.Kind == MoodSourceKind.Gold)), "its gold light stays: seed " + s);
                    List<LevelProp> Blocking(MoodRoom m) => r.Props.Where(p => p != chest && !m.Replaced.Contains(p) && !m.Raised.Contains(p) && !PropSize.Of(p.Model).Walkable)
                                                                   .Concat(m.LargeProps.Where(p => !PropSize.Of(p.Model).Walkable)).ToList();
                    bool now = ChestReached(l, r, Blocking(mr), chest, clear, walk), then = ChestReached(l, r, Blocking(mb), chest, clear, walk);
                    if (now) reached++;
                    else if (faults.Count < 10) faults.Add("seed " + s + " floor " + floor + " " + r.Letter + r.Order + " (" + r.Theme + ")");
                    if (then) reachedBefore++;
                    // Seen from the camera (to the south): nothing that blocks walking stands within 3 m south of the chest, a metre to either side of it.
                    bool Seen(MoodRoom m) => !Blocking(m).Any(p =>
                    {
                        p.Footprint.Extent(out double x0, out double y0, out double x1, out double y1);
                        return x1 > chest.Position.X - 1.0 && x0 < chest.Position.X + 1.0 && y1 > chest.Position.Y - 3.0 && y0 < chest.Position.Y;
                    });
                    if (Seen(mr)) seenNow++;
                    else if (faults.Count < 10) faults.Add("seed " + s + " " + r.Letter + r.Order + ": something stands between the camera and the chest");
                    if (Seen(mb)) seenBefore++;
                }
            }
            TestContext.WriteLine(string.Format(Inv, "CAVECHEST 400 seeds: {0} treasure rooms ({1} vaults); large coin heaps {2} before, {3} now; the chest's front reached from the door with a cell's width clear round the way: {4} now, {5} with the heaps; nothing that blocks walking within 3 m south of the chest (between it and the camera): {6} now, {7} with the heaps",
                                                rooms, vaults, heapsBefore, heapsNow, reached, reachedBefore, seenNow, seenBefore));
            Assert.That(heapsNow, Is.EqualTo(0), "no large coin heap");
            Assert.That(heapsBefore, Is.GreaterThan(0), "there were some to take out");
            Assert.That(faults, Is.Empty, "the way to the chest's front is clear");
        }
    }
}
