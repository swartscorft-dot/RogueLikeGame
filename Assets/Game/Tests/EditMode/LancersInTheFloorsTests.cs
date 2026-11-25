using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The lancer into the floors (work order 118; Kristian, 2026-11-24, asked by name, said yes to the producer's "Lansereren (ny
    /// fiende)": "Etasje 4: stormer i rett linje langveisfra, varslet på kroppen, og blir ør når den treffer en vegg eller
    /// rekvisitt"), the generator's version 22: work order 108's lancer placed in real floors by <see cref="LevelLancers"/> from the
    /// generator, with its switch (<see cref="LevelValues.Lancers"/>) on by default. With it off every floor is version 21's, field for
    /// field (104's pin, its old values); version 22's own pin and golden maps; over a thousand seeds a floor the rule: none before
    /// floor 4, at most one a combat room on floor 4 and two on floor 5, none in the floor's first combat room, the exit room or the
    /// boss's, each in the place of a swarmer (the room's count of enemies as with the switch off), each standing in the room's far
    /// half with floor to charge across toward the door she comes in by, never on a prop. Prints "LANCERFLOORS" with the share of
    /// combat rooms that hold one and the median clear straight run from a lancer's place toward the door.
    /// </summary>
    public class LancersInTheFloorsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static LevelValues On => new LevelValues { Lancers = true, RangedAroundTheRoom = false }; // work order 124 (changed deliberately): version 22 is the ranged enemies around the room off; the lancers named on
        static LevelValues Off => new LevelValues { Lancers = false, RangedAroundTheRoom = false };

        /// <summary>
        /// The far half: a lancer stands at least this share of the room's farthest place from the door she comes in by (the order:
        /// "it stands in the room's far half"; <see cref="LancerFloorValues.FarHalf"/>). The generator stands it in
        /// <see cref="LevelValues.FarBand"/> (0.6) first.
        /// </summary>
        static double FarHalf => new LancerFloorValues().FarHalf;

        /// <summary>
        /// Floor to charge across: the room's own floor in a straight line from a lancer's place toward the middle of the door she
        /// comes in by, before a carved cell or a wall comes within its body's radius of the line (props not counted: a prop in its
        /// line is her cover), at least <see cref="LancerFloorValues.ChargeFloorAtLeast"/> (10 m, the nearest it charges from), less
        /// the generator's step, as this test walks the line in a finer step of its own.
        /// </summary>
        static double ChargeRoomAtLeast => new LancerFloorValues().ChargeFloorAtLeast - new LancerFloorValues().ChargeStep;

        /// <summary>The step (m) the clear run is walked in here (finer than the generator's).</summary>
        const double RunStep = 0.05;

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // As FloorCharacterTests and ExitRoomsInTheFloorsTests fold their versions' floors.
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

        static List<LevelLayout> Run(ulong runSeed, LevelValues v, int floors)
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

        /// <summary>Two thousand floors (seeds s * 7919 + 13, floors 1 to 5 in turn) and a hundred runs of five, folded: the very floors and fold of FloorCharacterTests' version 21 pin.</summary>
        static ulong Print(LevelValues v)
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++) h = Fold(LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, v), h);
            for (ulong r = 0; r < 100; r++)
                foreach (LevelLayout l in Run(r * 104729UL + 103UL, v, 5)) h = Fold(l, h);
            return h;
        }

        // ---------------- The switch and the pins ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_WithVersion22()
        {
            Assert.That(new LevelValues().Lancers, Is.True, "on by default (work order 108)");
            Assert.That(new LevelValues().LancersInFloors, Is.True, "and in the floors, with the act's plan");
            Assert.That(LevelGenerator.Version, Is.EqualTo(23), "work order 118's version, 22; work order 124's, 23");
            Assert.That(LevelGenerator.Generate(0x118UL, 4).Version, Is.EqualTo(23));
            Assert.That(LevelGenerator.Generate(0x118UL, 4, Off).Version, Is.EqualTo(23), "off: version 21's floor, but for the number");
        }

        /// <summary>
        /// With the lancers off every floor is version 21's, field for field: the same floors and runs as version 21's own pin
        /// (FloorCharacterTests.Version21_...), held to that pin's OLD numbers, taken by work order 104.
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloorsAndAHundredRuns_AreVersion21s_FieldForField()
        {
            ulong h = Print(Off);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x524BEFDC904EAF79UL; // version 21's, Unity's EditMode run (work order 104's filtered run, 2026-11-24)
#else
            const ulong pinned = 0x7220A3733724AE81UL; // version 21's, .NET (the .NET rules run of work order 104)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("LANCERSOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the lancers off: version 21's floors, found " + found);
        }

        /// <summary>Version 22's own pin: the same floors and runs with the defaults. Each runtime has its own number: .NET's from the .NET rules run of this code, Unity's from a filtered Unity EditMode run of it.</summary>
        [Test]
        public void Version22_TwoThousandFloorsAndAHundredRuns_FieldForField()
        {
            ulong h = Print(On);
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0xFE9160D84BC9D5EEUL; // Unity's EditMode run of this code (work order 118's filtered run, 2026-11-25)
#else
            const ulong pinned = 0x48C9546D2550F782UL; // .NET, this code (the .NET rules run of work order 118, 2026-11-25)
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("LANCERPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "version 22's floors, found " + found);
        }

        // ---------------- The rule over a thousand seeds a floor ----------------

        /// <summary>The door she comes into a room by, its opening's middle.</summary>
        static LevelPoint DoorMiddle(LevelDoor door, double cs)
        {
            door.Opening(cs, out LevelPoint a, out LevelPoint b);
            return new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
        }

        /// <summary>
        /// The room's farthest place from the door she comes in by, as the generator measures its bands (<c>PlaceGroups</c>: its grid
        /// of places <see cref="LevelValues.PointStep"/> apart, <see cref="LevelValues.WallClearance"/> in from its walls).
        /// </summary>
        static double Farthest(LevelRoom room, LevelDoor entry, LevelValues v)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep, far = 0;
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1;
            int ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    far = Math.Max(far, LevelGeometry.DistanceToDoor(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step), entry, cs));
            return far;
        }

        /// <summary>
        /// The clear straight run (m) from a lancer's place toward the middle of the door she comes in by: walked in <see cref="RunStep"/>
        /// until the lancer's body (<see cref="LancerValues.BodyRadius"/>) would meet a prop or a solid cell of the room, or the walls
        /// of its rectangle, or it is at the door. What its charge has to cross when she stands in the doorway.
        /// </summary>
        static double ClearRun(LevelLayout level, LevelRoom room, LevelPoint from, LevelDoor entry, double body, bool props, out string stop)
        {
            double cs = level.CellSize;
            LevelPoint to = DoorMiddle(entry, cs);
            double length = from.DistanceTo(to), dx = (to.X - from.X) / length, dy = (to.Y - from.Y) / length;
            stop = "door";
            for (double d = RunStep; d < length; d += RunStep)
            {
                var p = new LevelPoint(from.X + dx * d, from.Y + dy * d);
                if (LevelGeometry.DistanceToDoor(p, entry, cs) < body)
                {
                    stop = "door";
                    return length;
                }
                LevelProp hit = props ? room.Props.FirstOrDefault(q => q.Footprint.DistanceTo(p) < body) : null;
                if (hit != null)
                {
                    stop = hit.Fight ? "fight cover" : hit.IsCover(new PropValues()) ? "tall prop" : "low prop";
                    return d - RunStep;
                }
                stop = RoomShapes.DistanceToSolid(room, p, cs) < body ? "carved cell" : LevelGeometry.DistanceToWalls(p, room.Rect, cs) < body ? "wall" : null;
                if (stop != null) return d - RunStep;
            }
            stop = "door";
            return length;
        }

        sealed class Tally
        {
            public int CombatRooms, WithOne, Lancers, MostInOne, Fallback;
            public int BelowHalf, InElite, Reattempted;
            public readonly List<double> Runs = new List<double>(), FloorRuns = new List<double>();
            public readonly Dictionary<string, int> Stops = new Dictionary<string, int>();
            public readonly List<double> Shares = new List<double>();
            public readonly Dictionary<string, int[]> ByMix = new Dictionary<string, int[]>();
        }

        static Dictionary<EnemyKind, int> Kinds(LevelRoom r) =>
            r.EnemyGroups.GroupBy(g => g.Kind).ToDictionary(x => x.Key, x => x.Sum(g => g.Count));

        static double Median(List<double> xs)
        {
            if (xs.Count == 0) return 0;
            List<double> s = xs.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) * 0.5;
        }

        /// <summary>
        /// A thousand seeds a floor, floors 1 to 5, the defaults beside the switch off: no lancer before floor 4; at most one a combat
        /// room on floor 4 and two on floor 5; none in the floor's first combat room, in the exit room or beside the act boss; each
        /// alone and no elite; each in the place of a swarmer of the same room with the switch off (its count of enemies, and of
        /// every other kind, the same); each in the room's far half (<see cref="FarHalf"/>) with at least <see cref="ChargeRoomAtLeast"/>
        /// of clear straight floor toward the door she comes in by, and on no prop; every floor valid by the validator. Prints the
        /// share of the floors' combat rooms that hold one, and the median clear run.
        /// </summary>
        [Test, Timeout(3600000)]
        public void AThousandSeedsAFloor_TheLancersKeepTheirRule()
        {
            LevelValues on = On, off = Off;
            double body = new LancerValues().BodyRadius;
            var lines = new List<string> { "LANCERFLOORS a thousand seeds a floor (seeds k * 6151 + 118)" };
            var bad = new List<string>();
            for (int floor = 1; floor <= 5; floor++)
            {
                var t = new Tally();
                for (ulong k = 0; k < 1000; k++)
                {
                    ulong seed = k * 6151UL + 118UL;
                    LevelLayout a = LevelGenerator.Generate(seed, floor, on), b = LevelGenerator.Generate(seed, floor, off);
                    string where = a.SeedText + " floor " + floor;
                    Assert.That(LevelValidator.Validate(a, on), Is.Empty, where + ": valid");
                    Assert.That(b.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Lancer), Is.False, where + ": off, none");
                    List<LevelRoom> fightsA = LevelGeometry.MainPath(a).Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                    List<LevelRoom> fightsB = LevelGeometry.MainPath(b).Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                    Assert.That(fightsA.Count, Is.EqualTo(fightsB.Count), where + ": as many fights");
                    if (a.Attempt != b.Attempt) t.Reattempted++; // a floor its lancers sent to another attempt (the validator's rules for them)
                    LevelRoom first = fightsA.FirstOrDefault(r => r.Kind == RoomKind.Combat);
                    for (int i = 0; i < fightsA.Count; i++)
                    {
                        LevelRoom r = fightsA[i], was = fightsB[i];
                        string room = where + " room " + r.Letter + r.Order;
                        int lancers = LevelLancers.Count(r.EnemyGroups);
                        Dictionary<EnemyKind, int> now = Kinds(r), before = Kinds(was);
                        Assert.That(r.EnemyCount, Is.EqualTo(was.EnemyCount), room + ": the room's count of enemies unchanged");
                        foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
                        {
                            int n = now.TryGetValue(kind, out int x) ? x : 0, m = before.TryGetValue(kind, out int y) ? y : 0;
                            if (kind == EnemyKind.Swarmer) Assert.That(n + lancers, Is.EqualTo(m), room + ": each lancer in the place of a swarmer");
                            else if (kind != EnemyKind.Lancer) Assert.That(n, Is.EqualTo(m), room + ": " + kind + " as with the switch off");
                        }
                        if (r.Kind == RoomKind.Combat) t.CombatRooms++;
                        if (lancers == 0) continue;
                        Assert.That(floor, Is.GreaterThanOrEqualTo(4), room + ": none before floor 4");
                        Assert.That(r.Kind, Is.EqualTo(RoomKind.Combat), room + ": only in a combat room");
                        Assert.That(r, Is.Not.SameAs(first), room + ": never the floor's first combat room");
                        Assert.That(r.EnemyGroups.Any(g => g.Boss), Is.False, room + ": never beside the boss");
                        Assert.That(lancers, Is.LessThanOrEqualTo(floor == 4 ? 1 : 2), room + ": the floor's most");
                        t.WithOne++;
                        t.Lancers += lancers;
                        t.MostInOne = Math.Max(t.MostInOne, lancers);
                        LevelDoor entry = LevelGeometry.EntryDoor(a, r);
                        double farthest = Farthest(r, entry, on);
                        for (int g = 0; g < r.EnemyGroups.Count; g++)
                        {
                            EnemyGroup group = r.EnemyGroups[g];
                            if (group.Kind != EnemyKind.Lancer) continue;
                            Assert.That(group.Count == 1 && !group.Elite && !group.Boss, Is.True, room + ": alone, never an elite");
                            LevelPoint p = r.EnemyPoints[g];
                            double share = LevelGeometry.DistanceToDoor(p, entry, a.CellSize) / Math.Max(1e-9, farthest);
                            double run = ClearRun(a, r, p, entry, body, true, out string stop);
                            double floorRun = ClearRun(a, r, p, entry, body, false, out string floorStop);
                            t.Shares.Add(share);
                            t.Runs.Add(run);
                            t.FloorRuns.Add(floorRun);
                            if (share < FarHalf - 1e-9) t.BelowHalf++;
                            if (run < ChargeRoomAtLeast) t.Stops[stop] = (t.Stops.TryGetValue(stop, out int c) ? c : 0) + 1;
                            if (floorRun < ChargeRoomAtLeast) t.Stops["floor: " + floorStop] = (t.Stops.TryGetValue("floor: " + floorStop, out int c2) ? c2 : 0) + 1;
                            if (r.Elite) t.InElite++;
                            if (share < on.FarBand - 1e-9) t.Fallback++;
                            if (share < FarHalf - 1e-9) bad.Add(room + " stands at " + share.ToString("P0", Inv) + " of the room's depth");
                            if (floorRun < ChargeRoomAtLeast) bad.Add(room + " has " + floorRun.ToString("0.0", Inv) + " m of clear floor toward the door (" + floorStop + ")");
                            Assert.That(r.Props.All(q => q.Footprint.DistanceTo(p) >= body), Is.True, room + ": on no prop");
                        }
                    }
                }
                if (floor < 4) Assert.That(t.WithOne, Is.Zero, "floor " + floor + ": none");
                else
                {
                    Assert.That(t.WithOne, Is.GreaterThan(0), "floor " + floor + ": they come");
                    Assert.That(t.MostInOne, Is.EqualTo(floor == 4 ? 1 : 2), "floor " + floor + ": its most is reached");
                }
                lines.Add(string.Format(Inv,
                    "LANCERFLOORS floor {0}: {1} of {2} combat rooms hold one ({3:P1}), {4} lancers ({5:0.00} a floor), at most {6} in one; depth share min {7:P0} median {8:P0}, {9} below the far band; clear run toward the door min {10:0.0} m, median {11:0.0} m",
                    floor, t.WithOne, t.CombatRooms, t.CombatRooms == 0 ? 0 : t.WithOne / (double)t.CombatRooms, t.Lancers, t.Lancers / 1000.0, t.MostInOne,
                    t.Shares.Count == 0 ? 0 : t.Shares.Min(), Median(t.Shares), t.Fallback, t.Runs.Count == 0 ? 0 : t.Runs.Min(), Median(t.Runs))
                    + string.Format(Inv, "; below half the depth {0}; in the elite room {1}; the floor alone (no props) min {2:0.0} m median {3:0.0} m; short of {4} m by {5}; floors made at another attempt than with them off {6}",
                                    t.BelowHalf, t.InElite, t.FloorRuns.Count == 0 ? 0 : t.FloorRuns.Min(), Median(t.FloorRuns), ChargeRoomAtLeast,
                                    string.Join(", ", t.Stops.OrderByDescending(kv => kv.Value).Select(kv => kv.Key + " " + kv.Value)), t.Reattempted));
            }
            TestContext.WriteLine(string.Join("\n", lines));
            foreach (string s in bad.Take(20)) TestContext.WriteLine("LANCERFLOORS short: " + s);
            Assert.That(bad, Is.Empty, bad.Count + " lancers out of their place; the first: " + string.Join("; ", bad.Take(5)));
        }

        // ---------------- The validator ----------------

        /// <summary>The validator keeps the lancers' rules in a real floor: a lancer moved into the floor's first combat room, or into the exit room, is a fault.</summary>
        [Test]
        public void TheValidator_FaultsALancerInTheFirstCombatRoom_OrTheExitRoom()
        {
            LevelValues v = On;
            LevelLayout level = null;
            for (ulong s = 1; s < 200 && level == null; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s, 5, v);
                if (l.Rooms.Any(r => LevelLancers.Count(r.EnemyGroups) > 0)) level = l;
            }
            Assert.That(level, Is.Not.Null, "a floor 5 with a lancer");
            Assert.That(LevelValidator.Validate(level, v), Is.Empty);
            LevelRoom first = LevelGeometry.MainPath(level).First(r => r.Kind == RoomKind.Combat);
            int at = first.EnemyGroups.FindIndex(g => g.Kind == EnemyKind.Swarmer);
            Assume.That(at, Is.GreaterThanOrEqualTo(0), "its first combat room holds a pack");
            EnemyGroup was = first.EnemyGroups[at];
            first.EnemyGroups[at] = new EnemyGroup(EnemyKind.Lancer, was.Count);
            Assert.That(string.Join("; ", LevelValidator.Validate(level, v)), Does.Contain("lancers").And.Contain("the floor's first combat room"));
            Assert.That(string.Join("; ", LevelValidator.Validate(level, Off)), Does.Contain("lancers"), "off, none may stand");
            first.EnemyGroups[at] = was;
            LevelRoom exit = level.ExitRoom;
            int boss = exit.EnemyGroups.FindIndex(g => g.Kind == EnemyKind.Guard || g.Kind == EnemyKind.Swarmer);
            Assume.That(boss, Is.GreaterThanOrEqualTo(0));
            was = exit.EnemyGroups[boss];
            exit.EnemyGroups[boss] = new EnemyGroup(EnemyKind.Lancer);
            Assert.That(string.Join("; ", LevelValidator.Validate(level, v)), Does.Contain("lancers").And.Contain("Exit room"));
        }

        // ---------------- Floor 4's row of the act ----------------

        /// <summary>
        /// Floor 4's row in the act's table (work order 104's <see cref="FloorCharacterValues"/>): the lancer is floor 4's second new
        /// thing beside the bomber, both from floor 4; its name, its dominant mix and theme and every mix weight as work order 104 left
        /// them.
        /// </summary>
        [Test]
        public void Floor4sRow_TheLancerBesideTheBomber_TheNameAndTheMixesAsTheyWere()
        {
            LevelValues v = On;
            Assert.That(v.LancerFloors.FromFloor, Is.EqualTo(4), "the lancer from floor 4");
            Assert.That(v.BomberFloors.FromFloor, Is.EqualTo(4), "beside the bomber");
            Assert.That(FloorCharacterRules.NameOf(4, v), Is.EqualTo("The Ossuary"));
            Assert.That(FloorCharacterRules.DominantMix(4, v), Is.EqualTo("crossfire"));
            Assert.That(FloorCharacterRules.DominantTheme(4, v), Is.EqualTo(RoomTheme.Ossuary));
            Assert.That(v.Character.DominantMixWeight, Is.EqualTo(2f), "no mix weight changed");
            Assert.That(v.Character.DominantThemeWeight, Is.EqualTo(3f));
            for (int floor = 1; floor <= 5; floor++)
                foreach (EnemyMix m in v.EnemiesOf(floor).Mixes)
                    Assert.That(FloorCharacterRules.MixWeight(m, floor, v), Is.EqualTo(FloorCharacterRules.MixWeight(m, floor, Off)), "floor " + floor + " " + m.Name);
        }

        // ---------------- The golden maps of version 22 ----------------

        // Work order 118: these maps are what the generator of version 22 makes with its defaults, the lancers on (each golden seed's
        // floors 1, 4 and 5). Floor 1 holds none, so its maps are version 21's (LevelGeneratorTests' Golden) but for the version number.
        [TestCase("WALKTHRU", 1)]
        [TestCase("WALKTHRU", 4)]
        [TestCase("WALKTHRU", 5)]
        [TestCase("RUNNER42", 1)]
        [TestCase("RUNNER42", 4)]
        [TestCase("RUNNER42", 5)]
        public void GoldenSeeds_KeepVersion22sMaps(string seedText, int floor)
        {
            Assert.That(LevelSeed.TryParse(seedText, out ulong seed), Is.True);
            Assert.That(LevelGenerator.Version, Is.EqualTo(23), "the version was raised: update the golden maps and this number together");
            string map = LevelMap.Draw(LevelGenerator.Generate(seed, floor, On)).Replace("version " + LevelGenerator.Version + " ", "version 22 "); // work order 124 (changed deliberately): version 22's maps are the ranged enemies around the room off
            string key = seedText + "/" + floor;
            TestContext.WriteLine("GOLDEN22 " + key + "\n" + map);
            Assert.That(GoldenVersion22.ContainsKey(key), "no golden map for " + key + "\n" + map);
            Assert.That(map, Is.EqualTo(GoldenVersion22[key].Replace("\r\n", "\n")), "map of seed " + key + "\n" + map);
        }

        static readonly Dictionary<string, string> GoldenVersion22 = new Dictionary<string, string>
        {
            ["WALKTHRU/1"] = @"seed WALKTHRU  floor 1  version 22  17 rooms  57 enemies  148 x 88 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
                                                                +-+-+-+
                                                                |L8!  |
                                          +-+-+-+-+-+-+-+     +-+   p +-+
                                          |C1bik k     u|     |    e  [ |
                                          +             +   +-+         +-+
                                          | :           |   |   [ =   h   |
                                          +        p    +-+-+       #     +
                                          |      %       P       p    b# !|
                                          +         # +-+-+-+ +-+    #+-+ +
                                          |           |     | | |     | | |
                                          +       # +-+     + +-+ #   +-+ +
                                          |         |       |   [     =   |
                                          +       +-+       +&            +
                                          |   h   |         |!            |
                                          +     p +         +-+-+ +-+-+-+-+
                                          |       |             |P|
+-+-+-+-+-+-+-+-+-+-+       +-+-+-+-+-+   +-+-+ +-+             + +
|X16                |       |C14      |       |P|               | |
+  H+-+:  +-+       +   +-+-+  # b    +       + +               + +
|   | |   | |       |   |i    p       |       | |               | |
+-+ +-+   +-+ +-+   +   +Ao       #   +     +-+ +-+           +-+ +-+
  |           | |   |   |i h          |     |T12  |           |T6   |
+-+           +-+   +-+-+   # : :     +-+-+-+     +           +  $  +
|            B       P           #     P         k|           |    k|
+                   +-+-+-+-+   p     +-+-+-+ i$i +           +    i+
|      p            |       |         |     |     |           |u   k|
+-+  :        +-+   +       +-+-+-+-+-+     +-+-+-+           +-+ +-+
  |        p  | |   |                                           |P|
+-+ +-+   +-+ +-+   +               +-+-+-+ +-+-+ +-+-+         + +
|   | |   | |       |               |C2   | |   | |k  |         | |
+   +-+   +-+       +               +     +-+   +-+   +   +-+-+-+ +-+-+
|             b  %  |               |k                |   |C4         |
+-+-+-+-+-+-+-+-+-+-+     +-+-+     +               h +   +   +-+ +-+ +-+
                          |E0*|     |      %    : p   |   |   | | | |   |
                      +-+-+   +-+-+-+  :  #           +-+-+  #+-+ +-+#  +
                      |        P           b  p        P       # : # h *|
                      +   @   +-+-+-+     +-+#  +-+   +-+-+           oA+
                      |       |     |     | | u | |   |   |     b      *|
                      +-+    *+     +-+-+-+ +-+-+ +-+-+   +        p  p +
                        |     |                           |    #        |
                        +-+-+-+                           +   +-+ +-+   +
                                                          |   | | | |   |
                                                          +   +-+ +-+ +-+
                                                          |           |
                                                          +-+-+-+-+-+-+
",
            ["WALKTHRU/4"] = @"seed WALKTHRU  floor 4  version 22  17 rooms  84 enemies  116 x 136 m  themes C2 archive, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 stair-head
            +-+-+     +-+-+-+-+-+-+-+
            |E0*|     |C2           |
        +-+-+   +-+-+ +          p k+
        |        P  | | %     #     |
        +   @   +-+ +-+    p    b   +
        |       | |          B      |
        +-+    *+ +-+-+-+ #         +
          |     |       |      :    |
          +-+-+-+       +-+  :      +
                          |        k|
                          +-+  g  Hi+
                            |       |
                            +       +
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
                    |             |
                    +   p       g +
                    |     q       |
                    +-+ q     +-+-+
                      |       |
                      +-+     +
                        |   B |
                    +-+-+b    +-+-+
                    |             |
                    +~            +
                    |   c  :   H  |
                    +             +   +-+-+-+-+-+
                    |  ~~~~       |   |L8 !     |
                    +-+-+-+-+ +-+-+   +   +-+ x +-+
                            |P|       |b  | |     |
+-+-+-+-+-+-+-+-+-+-+       + +       +  [+-+=l =p+-+
|X16 c         H    |       | |       |             |
+   +-+-+c :+-+-+   +     +-+ +-+     +           # +
|   |   |   |   |   |     |T6   |     |         e   |
+ p +   +  x+   +:  +     +k    +-+-+-+     p  #   w+
|   |   |   |   |   |     |      P                 &|
+   +-+-+   +-+-+   +     +  $  +-+-+-+  [+-+    B+-+
| p            B    |     |   u |     |   | | H   |
+                   +     +-+-+-+     +   +-+   +-+
|  %  #    p     :  |                 | # ! g   |
+                   +                 +-+-+-+ +-+
|  b               u|                       |P|
+         #         +                       + +-+
|                   |                       |   |
+                   +                       +-+ +
|x                  |                         | |
+-+-+-+-+-+ +-+-+-+-+                   +-+-+-+ +-+-+-+-+-+
          |P|                           |C10        k ik u|
          + +                           +         %      p+
          | |                           |              :  |
          + +-+-+ +-+-+-+     +-+-+-+   +                 +
          |C14  | |     |     |T12u |   | # g# :    b     |
      +-+-+c B  +-+     +-+-+-+ i   +-+-+              c  +
      |i        x   #    P      $    P     H B  p         |
      +A o #  :         +-+-+-+ i   +-+-+   +-+     +-+   +
      |i    g      p    |     |     |   |   | |    l| |  u|
      +l   H            +     +-+-+-+   +-+-+ +-+-+-+ +-+-+
      |  #    #      #  |
      +-+-+    b+-+     +
          |     | |     |
          +-+-+-+ +-+-+-+
",
            ["WALKTHRU/5"] = @"seed WALKTHRU  floor 5  version 22  17 rooms  94 enemies  116 x 128 m  themes C2 archive, C4 shrine, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 throne room  templates C2 corner, C4 twin, L8 knights-apse, C10 transept, C14 nave-apse, X16 throne-nave
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
                    |        #    |
                    +   p  #    g +
                    |          #  |
                    +-+       +-+-+
                      |       |
                      +-+     +
                        |   B |
                    +-+-+b    +-+-+
                    |             |
    +-+-+-+-+       +    #        +
    |X16Y*  |       |   c o    H  |
+-+-+       +-+-+   +      *      +   +-+-+-+-+-+
| w c     c   # |   |  l *A       |   |L8 !x ! &|
+ +-+ H     +-+ +   +-+-+-+-+ +-+-+   +   +-+ x +-+
| | |    p  | | |           |P|       |b  | |     |
+ +-+b      +-+ +           + +       +  [+-+=c  p+-+
|   :         # |           | |       |            l|
+       G   p   +         +-+ +-+     +    #      # +
| B           x |         |T6   |     |         e   |
+ +-+x     g+-+ +         +i    +-+-+-+     p  #   w+
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
            ["RUNNER42/1"] = @"seed RUNNER42  floor 1  version 22  17 rooms  58 enemies  136 x 136 m  themes C2 shrine, C4 ossuary, T6 archive, L8 tomb hall, C10 archive, T12 vault, C14 shrine, X16 exit hall  templates C2 nave, C4 colonnade, L8 knights-apse, C10 corner, X16 gate-hall
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
      |C2       |
      +        b+
      |  #      |
      +      #  +                                   +-+-+ +-+-+ +-+-+
      |         |                                   |X16| |   | |   |
      +-+     +-+                                   +   +-+   +-+h  +
        |     |                                     |               |
      +-+     +-+                                   + +-+     : +-+ +
      |  :      |                                   | | |       | | |
      +         +                                   + +-+      :+-+ +
      |  #   #  |                                   |       p       |
      +-+     +-+                                   +    p          +
        |  p #|                                     |               |
      +-+     +-+                                   + +-+      b+-+ +
      |   h o   |                                   | | |       | | |
      +    *  p +                                   + +-+       +-+ +
      |     A   |                                   |               |
      +-+ +-+-+-+                                   +   +-+   +-+   +
        |P|                                         |   | |   | |   |
        + +                                         +   +-+   +-+   +
        | |                                         |           :   |
        + +                   +-+-+-+-+-+           +   %           +
        | |                   |L8       |           |               |
+-+-+-+-+ +-+-+               + # +-+   +-+         +-+-+-+-+ +-+-+-+
|C4           |               |   | |B    |                 |P|
+   p         +               +  [+-+     +-+             +-+ +
|             |               |         p   |             |   |
+ +-+    q+-+ +     +-+-+-+   +            #+             + +-+
| | |     | | |     |T6k k|   |     #       |             | |
+ +-+     +-+ +-+-+-+     +-+-+       #   e&+       +-+-+-+ +-+
|              P        $  P       =    p  &|       |C14Ai    |
+ +-+  p  +-+ +-+-+-+     +-+-+  =+-+[    +-+     +-+         +-+
| | |:    | | |     |   u |   |   | |  h  |       |   # o    #  |
+ +-+     +-+ +     +-+-+-+   +   +-+   +-+       +        h    +
|      b      |               |   =     |         |  b      p   |
+-+  h      +-+               +-+-+-+ +-+         +             +
  |   ~~~^  |                       |P|           |     :  :    |
  +-+-+-+-+-+                     +-+ +           + #    #    # +
                                  |   |           |          p  |
                                  + +-+           +           +-+
                                  | |             |           |
                                +-+ +-+           +-+-+ +-+-+-+
                                |C10  |               |P|
                                +     +-+             + +
                                |       |             | |
                                +    # #+           +-+ +-+
                                |       |           |T12 u|
                                +   b   +-+-+-+-+-+-+   i +
                                |    %         P        $ |
                                +  p          +-+-+-+   i +
                                |          :  |     |     |
                                +             +     +-+-+-+
                                |          h i|
                                +        p   k+
                                |k            |
                                +-+-+-+-+-+-+-+
",
            ["RUNNER42/4"] = @"seed RUNNER42  floor 4  version 22  17 rooms  88 enemies  144 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 shrine, T12 vault, C14 archive, X16 exit hall  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 gate-hall
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
                                      +i       b+
                                      |         |
                                      +  #      +
                                      |      p  |
                                      +-+B    +-+
                                        |     |
                                      +-+     +-+
                                      |# %   : k|
                +-+-+-+   +-+-+-+     +         +
                |C4  !|   |     |     |         |
    +-+-+-+     +     +   +#    + +-+-+   c  H  +
    |T6k  |     |  c  |   |   # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+    B          +-+-+   +     +
    |     |     |  =       p    |       |     |
    +-+ +-+     +             # +       +-+-+-+
      |P|       |               |
      + +       +  H [+-+=   p  +
      | |       |l    | |       |
    +-+ +-+     +     + +-+     +
    |L8   |     | !   |   |   b |
    +     +     +-+-+-+   +-+-+-+
    |   : |
+-+-+  q  +-+-+     +-+-+-+-+-+-+-+
|             |     |C10          |
+         p   +-+-+ +     g      B+
|   q :        P  | |             |
+  l    e     +-+ +-+       p     +
|     p  : B  | |        p        |
+-+-+     +-+-+ +-+-+-+#          +
    |H    |           |   #      *|
    +   c +           +-+-+b   o A+
    |^~w~~|               |      *|
    +-+-+-+               +       +
                          | #     |
                          +  w    +
                          |       |
                          +    H  +                 +-+-+-+-+-+-+-+-+-+-+
                          |       |                 |X16        :       |
                          + +-+-+-+                 +       +-+   +-+   +
                          |P|                       |      b| |   | |c  |
                          + +     +-+-+-+-+-+-+     +   +-+ +-+   +-+ +-+
                          | |     |C14       k|     |   | |           |
                        +-+ +-+   +   :   +-+ + +-+-+   +-+      c  g +-+
                        |T12  |   |     B | | | |P                      |
                        +     +-+-+       +-+ +-+ +-+                   +
                        |      P            b     | |     x         p   |
                        + i$i +-+-+    :  +-+ +-+-+ +   +-+           +-+
                        |     |   |  #  g | | |     |   | |  B       w|
                        +-+-+-+   +   p   +-+ +     +   +-+g+-+  %+-+ +-+
                                  |u     %  w |     |       | |   | |H  |
                                  +    #  H   +     + p     +-+  x+-+   +
                                  |           |     | u          :      |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+-+-+-+
",
            ["RUNNER42/5"] = @"seed RUNNER42  floor 5  version 22  17 rooms  96 enemies  136 x 136 m  themes C2 archive, C4 tomb hall, T6 archive, L8 ossuary, C10 tomb hall, T12 vault, C14 archive, X16 throne room  templates C2 nave-apse, C4 twin, L8 cross-hall, C10 corner-long, X16 throne-nave
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
                |C4  !|   |     |     |         |
    +-+-+-+     +&    +   +#    + +-+-+   c  H  +
    |T6k  |     |  c  |   |   # | |P            |
    +     +-+-+-+   [ +-+-+     +-+ +-+-+     +-+
    | $    P                 [      |   | p   |
    +     +-+-+-+l   B          +-+-+   +    c+
    |     |     |  =            |       |     |
    +-+ +-+     +      #        +       +-+-+-+
      |P|       |l              |
      + +       +  H  +-+=   p [+
      | |       |w   c| |       |
    +-+ +-+     +     + +-+  p  +
    |L8   |     | !   |   |     |
    +     +     +-+-+-+   +-+-+-+
    | q : |
+-+-+     +-+-+     +-+-+-+-+-+-+-+
|s     x  q x |     |C10!! !    &!|
+         b   +-+-+ +     g # =  c+
|   s :        P  | |   [         |
+       e     +-+ +-+     # b    &+
| l   B  : g  | |        p    [   |
+-+-+     +-+-+ +-+-+-+          #+
    |H    |           | =         |
    +   c +           +-+-+B  [ =!+
    |^~l~~|               |     # |
    +-+-+-+               +x = l  +
                          |      x|
                          +  p    +
                          |       |
                          +   #H  +                 +-+-+-+-+-+-+-+
                          |       |                 |X16  c       |
                          + +-+-+-+                 + # +-+   +-+#+
                          |P|                       |   | |   | | |
                          + +     +-+-+-+-+-+-+     + b +-+   +-+ +-+
                          | |     |C14   l    |     |         w     |
                        +-+ +-+   +%      +-+ +     +  p        c   +
                        |T12  |   |     B | | |     |         p   B |
                        +     +-+-+       +-+ +-+-+ +       G       +
                        |      P        :   c  P  | |              Y|
                        + i$i +-+-+  #    +-+ +-+ +-+         p    o+
                        |     |   |     g | | | |                H  |
                        +-+-+-+   +   p   +-+ + +-+-+   +-+  g+-+ +-+
                                  |     #   Hi|     |   | | # | | |
                                  +       b   +     + # +-+   +-+#+
                                  |u   : :   w|     |             |
                                  +-+-+-+-+-+-+     +-+-+-+-+-+-+-+
",
        };
    }
}
