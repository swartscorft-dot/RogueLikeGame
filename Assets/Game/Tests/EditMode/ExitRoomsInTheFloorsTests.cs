using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Exit rooms of their own in the floors (work order 103; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "tronrommet
    /// bare i etasje 5 og egne utgangsrom i etasje 1–4"), the generator's version 20: the switch (<see cref="LevelValues.ExitRoomsOfTheirOwn"/>)
    /// on by default; version 20's own pin; floors 1 to 4 of a thousand seeds a floor end in one of work order 089's three, an exit
    /// hall whose way down she can always walk to, floor 5 in the throne room with the act boss; a run never shows the same one twice
    /// running (<see cref="WayDowns"/>), and the room before changes nothing with the switch off. Version 19 with the switch off is
    /// pinned by <see cref="ExitRoomsTests.SwitchOff_ThreeThousandFloors_AreVersion19s_FieldForField"/> (the switch named off since
    /// this order). The numbers of the report are printed by the thousand-seed test ("EXITFLOORS").
    /// </summary>
    public class ExitRoomsInTheFloorsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly string[] Names = { "gate-hall", "stair-head", "sealed-door" };

        /// <summary>Version 20's own pin (<see cref="Version20_TwoThousandFloorsAndAHundredRuns_FieldForField"/>), one a runtime.</summary>
#if UNITY_5_3_OR_NEWER
        public const ulong Version20Pin = 0x624F2B3DB2D7B169UL; // Unity's EditMode run of this code (work order 103's first filtered run, 2026-11-24)
#else
        public const ulong Version20Pin = 0xF45BF5EE7244EDB3UL; // .NET, this code (the .NET rules run of work order 103)
#endif

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>A floor folded as the earlier versions' pins fold it (<see cref="FloorPrint"/>, each room's shape, solid cells, template and added cover).</summary>
        static ulong Fold(LevelLayout level, ulong h)
        {
            h = FloorPrint.Of(level, h);
            foreach (LevelRoom r in level.Rooms)
            {
                h = Mix(h, (ulong)r.Shape);
                h = Mix(h, (ulong)r.SolidCount);
                foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                foreach (char ch in r.Template?.ToString() ?? "-") h = Mix(h, ch);
                h = Mix(h, (ulong)r.CoverTallAdded);
                h = Mix(h, (ulong)r.CoverLowAdded);
            }
            return h;
        }

        /// <summary>The floors 1 to 4 of a run, each told the floor before's exit room as the game tells it (GeneratedLevels.EnterFloor).</summary>
        static List<LevelLayout> Run(ulong runSeed, LevelValues v, int floors = 4)
        {
            var list = new List<LevelLayout>();
            string before = null;
            for (int floor = 1; floor <= floors; floor++)
            {
                LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(runSeed, floor), floor, v, exitBefore: before);
                list.Add(l);
                before = WayDowns.Shown(l);
            }
            return list;
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion20()
        {
            Assert.That(new LevelValues().ExitRoomsOfTheirOwn, Is.True, "on by default (work order 103)");
            Assert.That(LevelGenerator.Version, Is.GreaterThanOrEqualTo(20), "20, and 21 since work order 104");
            Assert.That(RoomTemplateLibrary.Usable(new LevelValues()).Count(ExitRooms.IsWayDown), Is.EqualTo(3), "the three are drawn in the floors");
            LevelLayout one = LevelGenerator.Generate(0x103UL, 1), five = LevelGenerator.Generate(0x103UL, 5);
            Assert.That(one.Version, Is.EqualTo(LevelGenerator.Version));
            Assert.That(ExitRooms.IsWayDown(one.ExitRoom.Template?.Template), Is.True, "floor 1 ends in a room of its own");
            Assert.That(ExitRooms.IsThrone(five.ExitRoom.Template?.Template), Is.True, "floor 5 in the throne room");
        }

        /// <summary>
        /// Version 20's own pin: two thousand floors with version 20's defaults (since work order 104 the floors' character named off) (seeds s * 7919 + 13, floor numbers 1 to 5 in turn, each built
        /// alone) and the floors 1 to 5 of a hundred runs (each told the floor before's exit room), folded as the earlier versions'.
        /// Each runtime has its own number (Unity's Math folds the doubles otherwise): .NET's from the .NET rules run of this code,
        /// Unity's from a filtered Unity EditMode run of it (pin 0, read the number, pin it).
        /// </summary>
        [Test]
        public void Version20_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            var v = new LevelValues { FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 104 (changed deliberately): version 20 is the floors' character off
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = Fold(LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v), h);
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5)) h = Fold(l, h);
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("EXITFLOORSPRINT " + found);
            Assert.That(h, Is.EqualTo(Version20Pin), "version 20's floors, found " + found);
        }

        /// <summary>With the switch off the room before is never read: every floor is the floor built alone, field for field (version 19's).</summary>
        [Test]
        public void SwitchOff_TheRoomBefore_ChangesNothing()
        {
            var off = new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false };
            for (ulong s = 0; s < 60; s++)
                foreach (string before in Names)
                {
                    int floor = (int)(s % 5) + 1;
                    LevelLayout alone = LevelGenerator.Generate(s * 6151UL + 3UL, floor, off), told = LevelGenerator.Generate(s * 6151UL + 3UL, floor, off, exitBefore: before);
                    Assert.That(Fold(told, 0UL), Is.EqualTo(Fold(alone, 0UL)), "seed " + alone.SeedText + " floor " + floor + " told " + before);
                    Assert.That(ExitRooms.IsWayDown(alone.ExitRoom.Template?.Template), Is.False, "off: a throne room");
                }
        }

        /// <summary>
        /// The rule of the room before (<see cref="WayDowns.Avoid"/>): it is taken out while another of the three is left; kept where it
        /// cannot be helped; nothing with the switch off, on floor 5 or with no room before. <see cref="WayDowns.Shown"/> names a stamped
        /// room of its own only.
        /// </summary>
        [Test]
        public void TheRoomBefore_IsPassedOver_WhileAnotherIsLeft()
        {
            var on = new LevelValues();
            List<RoomTemplate> Three() => RoomTemplateLibrary.All.Where(ExitRooms.IsWayDown).ToList();
            var options = Three();
            WayDowns.Avoid(options, "stair-head", 2, on);
            Assert.That(options.Select(t => t.Name), Is.EqualTo(new[] { "gate-hall", "sealed-door" }));
            var last = Three().Where(t => t.Name == "stair-head").ToList();
            WayDowns.Avoid(last, "stair-head", 2, on);
            Assert.That(last.Select(t => t.Name), Is.EqualTo(new[] { "stair-head" }), "the only one left: kept");
            foreach ((string before, int floor, LevelValues v) in new[] { ((string)null, 2, on), ("stair-head", 5, on), ("stair-head", 2, new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }) })
            {
                var all = Three();
                WayDowns.Avoid(all, before, floor, v);
                Assert.That(all.Count, Is.EqualTo(3));
            }
            Assert.That(WayDowns.Shown(LevelGenerator.Generate(0x103UL, 5)), Is.Null, "a throne room is no room of its own");
            Assert.That(WayDowns.Shown(LevelGenerator.Generate(0x103UL, 2, new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false })), Is.Null);
            Assert.That(Names, Does.Contain(WayDowns.Shown(LevelGenerator.Generate(0x103UL, 2))));
        }

        /// <summary>
        /// A thousand runs: floors 1 to 4 each end in one of the three and never in the one the floor before ended in; the rooms before
        /// the exit room draw the very templates they draw with no room told (the templates' stream is drawn alike), and the enemies are
        /// the same. Prints how often each floor shows each, and how many runs show all three.
        /// </summary>
        [Test, Timeout(3600000)]
        public void AThousandRuns_NeverShowTheSameExitRoomTwiceRunning()
        {
            var v = new LevelValues();
            var seen = new int[4, 3];
            int allThree = 0, toldOther = 0;
            for (ulong r = 0; r < 1000; r++)
            {
                List<LevelLayout> run = Run(r * 7907UL + 1UL, v);
                for (int i = 0; i < run.Count; i++)
                {
                    LevelLayout l = run[i];
                    string name = WayDowns.Shown(l), where = "run " + r + " floor " + (i + 1) + " (" + l.SeedText + ")";
                    Assert.That(name, Is.Not.Null, where + ": a room of its own, stamped");
                    seen[i, Array.IndexOf(Names, name)]++;
                    if (i == 0) continue;
                    Assert.That(name, Is.Not.EqualTo(WayDowns.Shown(run[i - 1])), where + ": not the floor before's");
                    LevelLayout alone = LevelGenerator.Generate(l.Seed, l.Floor, v);
                    if (WayDowns.Shown(alone) != name) toldOther++;
                    if (alone.Attempt == l.Attempt)
                    {
                        string Combat(LevelLayout x) => string.Join(",", x.Rooms.Where(q => q.Kind == RoomKind.Combat).Select(q => q.Template?.Template.Name ?? "-"));
                        Assert.That(Combat(l), Is.EqualTo(Combat(alone)), where + ": the combat rooms' templates as alone");
                    }
                    string Enemies(LevelLayout x) => string.Join("|", x.Rooms.Where(q => q.Kind == RoomKind.Combat || q.Kind == RoomKind.Exit)
                                                                    .Select(q => string.Join(",", q.EnemyGroups.Select(g => g.ToString()).OrderBy(y => y, StringComparer.Ordinal))));
                    Assert.That(Enemies(l), Is.EqualTo(Enemies(alone)), where + ": the same enemies");
                }
                if (run.Select(WayDowns.Shown).Distinct().Count() == 3) allThree++;
            }
            var lines = new List<string>();
            for (int i = 0; i < 4; i++) lines.Add("floor " + (i + 1) + ": " + string.Join(", ", Names.Select((n, k) => n + " " + seen[i, k])));
            string line = "EXITRUNS a thousand runs, floors 1 to 4: " + string.Join("; ", lines) + "; runs showing all three " + allThree
                          + "; floors whose room differs from the floor built alone " + toldOther;
            TestContext.WriteLine(line);
            for (int i = 0; i < 4; i++)
                for (int k = 0; k < 3; k++)
                    Assert.That(seen[i, k], Is.InRange(200, 470), "each of the three about a third of floor " + (i + 1) + "'s (a third is 333)");
        }

        // ---------------- A thousand seeds a floor ----------------

        sealed class Tally
        {
            public int Rooms, Enemies, Most, Fewest = int.MaxValue, Groups, InBand, Reached;
            public double Walkable, Walk, Width, Height, Solid;
        }

        /// <summary>
        /// The walk from the middle of the door in to the way down's place (the A cells' middle) on a half-metre grid of the room's free
        /// floor (<see cref="FightCover.FreeSpot"/>: open cells, her walk radius from walls, carved cells and props), eight ways; -1 when
        /// the grid finds no way (as work order 089's measure walks it).
        /// </summary>
        static double Walk(LevelLayout level, LevelRoom room, LevelValues v, LevelPoint from, LevelPoint to)
        {
            double cs = level.CellSize, step = 0.5, x0 = room.Rect.X * cs, y0 = room.Rect.Y * cs;
            int nx = (int)Math.Round(room.Rect.Width * cs / step), ny = (int)Math.Round(room.Rect.Height * cs / step);
            var free = new bool[nx * ny];
            for (int j = 0; j < ny; j++)
                for (int i = 0; i < nx; i++)
                    free[j * nx + i] = FightCover.FreeSpot(level, room, new LevelPoint(x0 + (i + 0.5) * step, y0 + (j + 0.5) * step), v);
            int Nearest(LevelPoint p)
            {
                int best = -1;
                double bd = double.MaxValue;
                for (int k = 0; k < free.Length; k++)
                {
                    if (!free[k]) continue;
                    double d = new LevelPoint(x0 + (k % nx + 0.5) * step, y0 + (k / nx + 0.5) * step).DistanceTo(p);
                    if (d < bd) { bd = d; best = k; }
                }
                return best;
            }
            int s = Nearest(from), t = Nearest(to);
            if (s < 0 || t < 0) return -1;
            var dist = new double[free.Length];
            for (int k = 0; k < dist.Length; k++) dist[k] = double.MaxValue;
            dist[s] = 0;
            var queue = new SortedSet<(double d, int k)> { (0, s) };
            while (queue.Count > 0)
            {
                (double d, int k) = queue.Min;
                queue.Remove(queue.Min);
                if (k == t) return d;
                int i = k % nx, j = k / nx;
                for (int di = -1; di <= 1; di++)
                    for (int dj = -1; dj <= 1; dj++)
                    {
                        if (di == 0 && dj == 0) continue;
                        int ni = i + di, nj = j + dj;
                        if (ni < 0 || nj < 0 || ni >= nx || nj >= ny || !free[nj * nx + ni]) continue;
                        if (di != 0 && dj != 0 && (!free[j * nx + ni] || !free[nj * nx + i])) continue;
                        double nd = d + step * (di != 0 && dj != 0 ? Math.Sqrt(2) : 1);
                        int n = nj * nx + ni;
                        if (nd >= dist[n]) continue;
                        if (dist[n] < double.MaxValue) queue.Remove((dist[n], n));
                        dist[n] = nd;
                        queue.Add((nd, n));
                    }
            }
            return -1;
        }

        /// <summary>Did each group stand in its band (shooters, casters, warders the far band; brutes, guards, bombers the middle; swarmers anywhere; the boss at the middle)?</summary>
        static void Bands(LevelLayout level, LevelRoom room, LevelValues v, out int groups, out int inBand)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1, ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            double farthest = 0;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    farthest = Math.Max(farthest, LevelGeometry.DistanceToDoor(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step), entry, cs));
            groups = inBand = 0;
            for (int k = 0; k < room.EnemyPoints.Count; k++)
            {
                EnemyGroup g = room.GroupAt(k);
                groups++;
                if (g.Boss) { inBand++; continue; }
                double d = LevelGeometry.DistanceToDoor(room.EnemyPoints[k], entry, cs) / farthest;
                bool far = g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster || g.Kind == EnemyKind.Warder;
                double lo = far ? v.FarBand : g.Kind == EnemyKind.Swarmer ? 0 : v.MiddleBandFrom, hi = far || g.Kind == EnemyKind.Swarmer ? 1 : v.MiddleBandTo;
                if (d >= lo - 1e-9 && d <= hi + 1e-9) inBand++;
            }
        }

        /// <summary>
        /// A thousand seeds a floor, floors 1 to 5, the defaults, each floor built alone: every floor is made (the generator throws for one
        /// the validator finds a fault in: its props', the lane's, the joined floor's and the way down's line among them); floors 1 to 4
        /// end in one of the three, stamped, an exit hall with no throne and no boss, its enemies 1.5 times a combat room's
        /// (<see cref="LevelValues.EnemyRange"/>), every enemy placed, its one door the way in, and the way down's place reached on foot
        /// from the door in; floor 5 ends in a throne room with the act boss. Prints, for each of the three, its size, the share of its
        /// floor that is walkable, its enemies (mean, fewest, most) and the share of its groups standing in their band, and the walk to
        /// the way down ("EXITFLOORS").
        /// </summary>
        [Test, Timeout(3600000)]
        public void AThousandSeedsAFloor_FloorsOneToFourEndInARoomOfTheirOwn_TheWayDownReachable_FloorFiveInTheThrone()
        {
            var v = new LevelValues();
            var by = new Dictionary<string, Tally>();
            for (int floor = 1; floor <= 5; floor++)
                for (ulong s = 0; s < 1000; s++)
                {
                    LevelLayout l = LevelGenerator.Generate(s * 6151UL + 103UL, floor, v);
                    LevelRoom exit = l.ExitRoom;
                    string where = l.SeedText + " floor " + floor;
                    Assert.That(l.Rooms.Sum(r => r.EnemyGroups.Count(g => g.Boss)), Is.EqualTo(floor == 5 ? 1 : 0), where + ": the act boss on floor 5 only");
                    if (floor == 5)
                    {
                        Assert.That(ExitRooms.IsThrone(exit.Template?.Template), Is.True, where + ": the throne room");
                        Assert.That(exit.Theme, Is.EqualTo(RoomTheme.ThroneRoom), where);
                        continue;
                    }
                    Assert.That(exit.Shape, Is.EqualTo(RoomShape.Template), where + ": stamped");
                    string name = WayDowns.Shown(l);
                    Assert.That(Names, Does.Contain(name), where + ": one of the three");
                    Assert.That(exit.Theme, Is.EqualTo(RoomTheme.ExitHall), where);
                    Assert.That(exit.Props.Any(p => p.Model == PropModel.BoneThrone), Is.False, where + ": no throne");
                    Assert.That(l.DoorsOf(exit.Order).Count(), Is.EqualTo(1), where + ": one door, the way in");
                    v.EnemyRange(RoomKind.Exit, floor, out int fewest, out int most);
                    int enemies = exit.EnemyGroups.Where(g => !g.Boss).Sum(g => g.Count);
                    Assert.That(enemies, Is.InRange(fewest, most), where + ": 1.5 times a combat room's");
                    Assert.That(exit.EnemyPoints.Count, Is.EqualTo(exit.EnemyGroups.Count), where + ": every group placed");
                    Assert.That(JoinedFloor.FaultOf(l, exit, v), Is.Null, where + ": its floor joined");
                    Assert.That(ExitRooms.WayDownLine(l, exit, out LevelPoint door, out LevelPoint wayDown), Is.True, where);
                    double walk = Walk(l, exit, v, door, wayDown);
                    Assert.That(walk, Is.GreaterThan(0), where + ": the way down reached on foot from the door in");
                    Bands(l, exit, v, out int groups, out int inBand);
                    foreach (string key in new[] { "floor " + floor + " " + name, name })
                    {
                        if (!by.TryGetValue(key, out Tally t)) by[key] = t = new Tally();
                        t.Rooms++;
                        t.Enemies += enemies;
                        t.Most = Math.Max(t.Most, enemies);
                        t.Fewest = Math.Min(t.Fewest, enemies);
                        t.Groups += groups;
                        t.InBand += inBand;
                        t.Reached++;
                        t.Walkable += PropRules.WalkableShare(l, exit, v.PropRules);
                        t.Walk += walk;
                        t.Width += exit.Rect.Width * l.CellSize;
                        t.Height += exit.Rect.Height * l.CellSize;
                        t.Solid += exit.SolidCount / (double)(exit.Rect.Width * exit.Rect.Height);
                    }
                }
            var lines = new List<string> { "EXITFLOORS a thousand seeds a floor, floors 1 to 4, the exit room of its own: drawn, size, solid, walkable, enemies, in their band, the way down reached, walked" };
            foreach (string key in Names.Concat(by.Keys.Where(k => k.StartsWith("floor", StringComparison.Ordinal)).OrderBy(k => k, StringComparer.Ordinal)))
            {
                if (!by.TryGetValue(key, out Tally t)) continue;
                lines.Add(string.Format(Inv, "EXITFLOORS {0,-26} drawn {1,4}  {2:0} x {3:0} m  solid {4,5:P1}  walkable {5,5:P1}  enemies {6,4:0.0} ({7} to {8})  in band {9,5:P1}  reached {10,6:P1}  walk {11,4:0.0} m",
                                       key, t.Rooms, t.Width / t.Rooms, t.Height / t.Rooms, t.Solid / t.Rooms, t.Walkable / t.Rooms, t.Enemies / (double)t.Rooms, t.Fewest, t.Most,
                                       t.InBand / (double)Math.Max(1, t.Groups), t.Reached / (double)t.Rooms, t.Walk / t.Rooms));
            }
            TestContext.WriteLine(string.Join("\n", lines));
            foreach (string n in Names) Assert.That(by.ContainsKey(n), Is.True, n + " drawn");
        }

        // ---------------- The golden maps of version 19 ----------------

        // Work order 103: with the exit rooms of their own off the floors are version 19's, so the golden seeds keep version 19's maps
        // (LevelGeneratorTests' maps of version 19, as they were before this order), but for the version number.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_WithTheExitRoomsOff_KeepVersion19sMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor, new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }))
                         .Replace("version " + LevelGenerator.Version + " ", "version 19 ");
            string key = seedText + "/" + floor;
            Assert.That(map, Is.EqualTo(GoldenVersion19[key].Replace("\r\n", "\n")), "map of seed " + key + " with the exit rooms of their own off\n" + map);
        }

        // The maps of version 19 (LevelGeneratorTests' Golden before work order 103), copied unchanged.
        static readonly Dictionary<string, string> GoldenVersion19 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 19  17 rooms  57 enemies  176 x 68 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 shrine, T12 archive, C14 archive, X16 throne room  templates C2 nave-apse, C4 corner-long, L8 knights-apse, C10 colonnade, C14 nave, X16 throne-nave
    +-+-+-+-+
    |X16*   |
+-+-+       +-+-+                             +-+-+-+-+-+
| # B         # |                             |L8   [   |
+ +-+ H  :  +-+ +                           +-+   +-+   +               +-+-+-+-+-+-+-+-+
| | |       | | |                           |     | |  #|               |C4    iAi      |
+ +-+       +-+ +                         +-+     +-+   +               +               +
|  :            |                         |   =         |               |   h   o       |
+           #   +                         + #   B       +   +-+-+-+     +           #   +
|      p        |                         |  e      =   |   |T6u k|     |   #  :b       |
+ +-+      p+-+ +                         +      p      +-+-+     +-+-+-+    p          +
| | |       | | |                         |   p          P    $    P               #    |
+ +-+       +-+ +                         +-+     +-+   +-+-+     +-+-+-+-+-+-+-+ p     +
|               |                           | h # | |   |   |     |             |       |
+ #           # +                           +-+   +-+ # +   +-+-+-+             +#   #  +
|               |                             |     [   |                       |       |
+-+-+-+-+-+ +-+-+                             +-+ +-+-+-+                       +-+     +
          |P|                                   |P|                               |     |
        +-+ +                                   + +                               + +-+-+
        |   |                                   | |                               |P|
        + +-+                           +-+-+-+-+ +-+-+                           + +
        | |                             |C10          |                           | |
      +-+ + +-+-+ +-+-+-+               +             +               +-+-+-+ +-+-+ +
      |C14| |  i| |     |               |          #  |               |C2i  | |     |
      +   +-+   +-+    u+     +-+-+-+   + +-+     +-+ +               +     +-+     +-+-+
      |k                |     |T12ik|   | | |# p #| | |               |                 |
      +                 +-+-+-+     +-+-+ +-+     +-+ +     +-+-+     +                 +
      |     B    : p     P      $    P     B          |     |E0*|     |  :      :   p   |
      +                 +-+-+-+     +-+-+ +-+     +-+ + +-+-+   +-+-+-+                 +
      |      %     #    |     |    u|   | | |# : #| | | |        P         # B %   h    |
      +  h+-+   +-+     +     +-+-+-+   + +-+     +-+ + +   @   +-+-+-+     +-+ #   +-+-+
      |   | |   | |    u|               |      o      | |       |     |     | |    u|
      +-+-+ +-+-+ +-+-+-+               +-+ h     p +-+ +-+    *+     +-+-+-+ +-+-+-+
                                          |   iAi   |     |     |
                                          +-+-+-+-+-+     +-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 19  17 rooms  94 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p  +
        |        P  | | %           |
        +   @   +-+ +-+    p  # B   +
        |       | |          g      |
        +-+    *+ +-+-+-+ #         +
          |     |       |      :    |
          +-+-+-+       +-+  :      +
                          |     b  k|
                          +-+     Hi+
                            |       |
                            +   c  k+
                            |       |
                            + +-+-+-+
                            |P|
                          +-+ +
                          |   |
                          + +-+
                          | |
                    +-+-+-+ +-+-+-+
                    |C4           |
                    +             +
                    |    #   #    |
                    +           B +
                    |          #  |
                    +-+       +-+-+
                      |       |
                      +-+     +
                        |   p |
                    +-+-+     +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       |   c o    H  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| w c     c   # |   | p  *A      c|   |L8 !x   &|
+ +-+ H     +-+ +   +-+-+-+-+ +-+-+   +   +-+ p +-+
| | |    p  | | |           |P|       |b  | |     |
+ +-+b      +-+ +           + +       +  [+-+=c  x+-+
|  :          # |           | |       |             |
+       G   p   +         +-+ +-+     +    #  #    &+
|c            x |         |T6   |     |         e   |
+ +-+x     B+-+ +         +i    +-+-+-+     p  #   w+
| | |   p   | | |         |k     P           =      |
+ +-+       +-+ +         +  $  +-+-+-+  [+-+    B+-+
|               |         |  u  |     |   | | H   |
+ #           # +         +-+-+-+     +   +-+   +-+
|               |                     |     g   |
+-+-+-+-+-+ +-+-+                     +-+-+-+ +-+
          |P|                               |P|
          + +-+                             + +-+
          |   |                             |   |
          +-+ +                             +-+ +
            | |                               | |
            + +-+-+ +-+-+-+             +-+-+-+ +-+-+-+-+-+
            |C14  | |     |             |C10            ki|
        +-+-+c B  +-+     +             +i                +
        |             #   |             |   :             |
        +*  #             +   +-+-+-+   +%      :        k+
        |A o  g      p    |   |T12 u|   | # g#      b     |
        +*   H  p         +-+-+ i   +-+-+              c  +
        |  #      #    #   P    $    P     H B  p p       |
        +-+-+    b+-+     +-+-+ i   +-+-+   +-+     +-+   +
            |     | |     |   |     |   |   | |   u | |   |
            +-+-+-+ +-+-+-+   +-+-+-+   +-+-+ +-+-+-+ +-+-+
",
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 19  17 rooms  58 enemies  132 x 108 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 nave-apse, C4 corner-long, L8 knights-apse, C10 corner, X16 throne-nave
                                        +-+-+-+
                                        |L8^~~|
                                      +-+ e   +-+
                                      |     p   |
                  +-+-+-+-+-+-+-+   +-+         +-+
                  |C10       uki|   |       :   h |
                  +k            +-+-+             +
                  |i  :    B     P           :   u|
                  +             +-+-+ +-+     +-+ +
                  |k     %  #   |   | | | p   | | |
                  +   p     # +-+   + +-+    q+-+ +
                  |           |     |             |
                  +       +-+-+     +             +       +-+-+
                  |    p  |         |q          b |       |E0 |
                  + h     +         +-+-+-+ +-+-+-+     +-+   +
                  |       |               |P|           |     |
                  +       +               + +           +   @ +-+
                  |u      |               | |           |       |
                  +-+-+-+ +             +-+ +-+         +       +
                        |P|             |T6  k|         |*    * |
                        + +             +  $ k+         +-+ +-+-+
                        | |             |     |           |P|
                      +-+ +-+           +     +           + +
                      |T12  |           |     |           | |
                      +    k+           +-+ +-+         +-+ +-+-+-+
                      |     |             |P|           |C2    k  |
                      + i$i +             + +           +i       b+
                      |     |             | |           |         |
                      +-+ +-+       +-+-+-+ +           +         +
                        |P|         |C4 !   |           |   #  p  |
                        + +         +       +           +-+     +-+
                        | |         |    #  |             |     |
                      +-+ +-+-+-+   +! h   =+           +-+     +-+
                      |C14      |   |       |           |# %   : k|
                      +         +-+ +   [ = +           +         +
                      |     #     | |  p    |           |   p     |
                      +           + +       +-+-+   +-+-+      h  +
                      |        #  | |   [ b   = |   |P            |
  +-+-+-+-+-+-+-+     +  #  b   p + +           +-+-+ +-+-+     +-+
  |X16          |     |           | |      =  [       |   |     |
  +#+-+   +-+ # + +-+-+     p :   + +             +-+-+   +     +
  | | |   | |   | |P              | |    #        |       |     |
+-+h+-+ # +-+   +-+ +-+   #   #   + +! p     # # &+       +-+-+-+
|                   | |  : o      | |             |
+      b        +-+-+ +-+    H  +-+ +-+-+-+-+-+-+-+
|*  :           |       | iAi   |
+Y    B p       +       +-+-+-+-+
|*              |
+        p      +
|               |
+-+ +-+   +-+   +
  | | |   | |   |
  +#+-+   +-+ # +
  |             |
  +-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 19  17 rooms  96 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 shrine, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
                                        +-+-+
                                        |E0 |
                                      +-+   +
                                      |     |
                                      +   @ +-+
                                      |       |
                                      +       +
                                      |*    * |
                                      +-+ +-+-+
                                        |P|
                                        + +
                                        | |
                                      +-+ +-+-+-+
                                      |C2    k  |
                                      +i        +
                                      |         |
                                      +  #      +
                                      |      p  |
                                      +-+B    +-+
                                        |     |
                                      +-+     +-+
                                      |# %   : k|
                +-+-+-+   +-+-+-+     +         +
                |C4  !|   |   ! |     |         |
    +-+-+-+     +&    +   +#    + +-+-+   c  H  +
    |T6k  |     |  c  |   |   # | |P            |
    +     +-+-+-+    [+-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    g          +-+-+   +    c+
    |     |     |  =       p    |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |          p    |
      + +       +  H  +-+=   B [+
      | |       |    w| |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |   b |
    +     +     +-+-+-+   +-+-+-+
    | q : |
+-+-+     +-+-+     +-+-+-+-+-+-+-+
|      x  q x |     |C10          |
+         b   +-+-+ +     g      c+
| p p :        P  | |             |
+       e     +-+ +-+     # b#    +
|     B    g  | |        p #      |
+-+-+     +-+-+ +-+-+-+           +
    |H    |           |          *|
    +   c +           +-+-+B   o A+
    | ^~~ |               |      *|
    +-+-+-+               +x      +
                          |   #  x|
                          +  p    +
                          |       |
                          +    H  +                 +-+-+-+-+-+-+-+
                          |       |                 |X16  c       |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     + b +-+   +-+ +-+
                          | |     |C14       k|     |         w     |
                        +-+ +-+   +   :   +-+ +     +  p        c   +
                        |T12  |   |     p | | |     |         p   B |
                        +     +-+-+       +-+ +-+-+ +       G       +
                        |      P            c  P  | |              Y|
                        + i$i +-+-+  #    +-+ +-+ +-+         p    o+
                        |     |   |     B | | | |                H  |
                        +-+-+-+   +   p   +-+ + +-+-+   +-+  g+-+ +-+
                                  |u     %  H |     |   | | # | | |
                                  +    #  c   +     + # +-+   +-+#+
                                  |      :   w|     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };
    }
}
