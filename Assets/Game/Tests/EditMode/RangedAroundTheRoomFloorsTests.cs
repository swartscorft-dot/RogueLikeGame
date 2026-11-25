using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Ranged enemies around the room (work order 124, version 23) over a thousand seeds a floor, the lancers named on and named off,
    /// beside the same seeds with the switch off (version 22): every floor valid, every room's count of enemies and of each kind as
    /// before, every ranged group stood by the rules and every post its pattern's (the validator), and the numbers the order asks for:
    /// by floor the share of combat rooms with a near flank, a pair, a nest, a ladder and a crossfire; the ranged posts' depth as a share
    /// of the room's depth before and after (median, quartiles, the share in the far band); the rooms with every ranged enemy in the far
    /// band before and after; the share of ranged posts in view at the waking; the warder in the pack; the near flank's low cover and the
    /// crossfire's tall cover. Prints "RANGEDFLOORS". And the room lab builds every preset, and where he sees each pattern
    /// ("RANGEDWHERE"). Rules only (.NET first): nobody has played it.
    /// </summary>
    public class RangedAroundTheRoomFloorsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static LevelValues After(bool lancers) => new LevelValues { Lancers = lancers };
        static LevelValues Before(bool lancers) => new LevelValues { Lancers = lancers, RangedAroundTheRoom = false };

        static bool IsRanged(EnemyGroup g) => g != null && !g.Boss && !g.Elite && (g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster);

        static double Quantile(List<double> xs, double q)
        {
            if (xs.Count == 0) return double.NaN;
            List<double> s = xs.OrderBy(x => x).ToList();
            double at = q * (s.Count - 1);
            int lo = (int)Math.Floor(at), hi = Math.Min(s.Count - 1, lo + 1);
            return s[lo] + (s[hi] - s[lo]) * (at - lo);
        }

        static Dictionary<EnemyKind, int> Kinds(LevelRoom r)
        {
            var d = new Dictionary<EnemyKind, int>();
            foreach (EnemyGroup g in r.EnemyGroups) d[g.Kind] = (d.TryGetValue(g.Kind, out int n) ? n : 0) + g.Count;
            return d;
        }

        sealed class Tally
        {
            public int Combat, Eligible, NearFlank, Pair, Nest, Ladder, Crossfire, CrossEligible, Floors, Reattempted;
            public int CombatNorthSouth, CombatSide, CrossNorthSouth, CrossSide, CrossAtBack; // work order 124's second round: by the way she comes in
            public int RangedRooms, AllFar, MoreThanOneFar, Posts, InView, Warders, InPack, Elsewhere;
            public int NearShooters, NearLow, CrossPosts, CrossCovered, DeepPosts, DeepHidden;
            public readonly List<double> Shares = new List<double>();
        }

        /// <summary>The share of a room's depth from the door she comes in by that a place stands at, as the generator measures its bands.</summary>
        static double Share(LevelLayout l, LevelRoom r, LevelDoor entry, LevelPoint p, double farthest) =>
            LevelGeometry.DistanceToDoor(p, entry, l.CellSize) / Math.Max(1e-9, farthest);

        static void Count(LevelLayout l, LevelValues v, Tally t, bool after)
        {
            RangedAroundValues r = v.RangedAround;
            foreach (LevelRoom room in LevelGeometry.MainPath(l))
            {
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                LevelDoor entry = LevelGeometry.EntryDoor(l, room);
                if (entry == null) continue;
                double farthest = LevelLancers.Depth(l, room, entry, v);
                LevelPoint line = LevelRanged.LineMiddle(l, room, entry, v);
                var posts = new List<(LevelPoint p, EnemyGroup g)>();
                for (int i = 0; i < room.EnemyGroups.Count; i++)
                    if (IsRanged(room.EnemyGroups[i]))
                        foreach (LevelPoint m in room.PlacesAt(i)) posts.Add((m, room.EnemyGroups[i]));
                int ranged = room.EnemyGroups.Where(IsRanged).Sum(g => g.Count);
                if (room.Kind == RoomKind.Combat)
                {
                    t.Combat++;
                    if (ranged >= r.NearFlankRangedAtLeast) t.Eligible++;
                    bool ns = LevelRanged.EnteredNorthOrSouth(room, entry);
                    if (l.Floor >= r.CrossfireFromFloor && ranged >= 2) t.CrossEligible++;
                    if (ns) t.CombatNorthSouth++;
                    else t.CombatSide++;
                    string words = LevelRanged.PatternsOf(room);
                    List<string> w = words.Split('+').ToList();
                    if (w.Contains(LevelRanged.NearFlankWord)) t.NearFlank++;
                    if (w.Contains(LevelRanged.PairWord)) t.Pair++;
                    if (w.Contains(LevelRanged.NestWord)) t.Nest++;
                    if (w.Contains(LevelRanged.LadderWord)) t.Ladder++;
                    if (w.Contains(LevelRanged.CrossfireWord))
                    {
                        t.Crossfire++;
                        if (ns) t.CrossNorthSouth++;
                        else t.CrossSide++;
                        double depth = LevelLancers.Depth(l, room, entry, v);
                        for (int i = 0; i < room.EnemyGroups.Count; i++)
                            if (room.EnemyGroups[i].Ranged?.Post == RangedPost.Crossfire && Share(l, room, entry, room.EnemyPoints[i], depth) >= v.FarBand - 1e-9)
                            {
                                t.CrossAtBack++;
                                break;
                            }
                    }
                }
                if (posts.Count > 0)
                {
                    t.RangedRooms++;
                    int far = 0;
                    var farGroups = new HashSet<int>();
                    foreach ((LevelPoint p, EnemyGroup g) in posts)
                    {
                        double s = Share(l, room, entry, p, farthest);
                        t.Shares.Add(s);
                        t.Posts++;
                        if (s >= v.FarBand - 1e-9) far++;
                        if (LevelRanged.InDesignView(line, p, r, r.ViewMargin)) t.InView++;
                        if (g.Ranged?.Post == RangedPost.Elsewhere) t.Elsewhere++;
                    }
                    if (far == posts.Count) t.AllFar++;
                    // Groups (a pair or a nest counted once) standing in the far band.
                    var seen = new HashSet<string>();
                    for (int i = 0; i < room.EnemyGroups.Count; i++)
                    {
                        EnemyGroup g = room.EnemyGroups[i];
                        if (!IsRanged(g) || Share(l, room, entry, room.EnemyPoints[i], farthest) < v.FarBand - 1e-9) continue;
                        seen.Add(g.Ranged != null && g.Ranged.Cluster > 0 ? "c" + g.Ranged.Cluster : "g" + i);
                    }
                    if (seen.Count > 1) t.MoreThanOneFar++;
                }
                // The warder.
                for (int i = 0; i < room.EnemyGroups.Count; i++)
                    if (room.EnemyGroups[i].Kind == EnemyKind.Warder)
                    {
                        t.Warders++;
                        if (room.EnemyGroups[i].Ranged?.Post == RangedPost.Pack) t.InPack++;
                    }
                if (!after) continue;
                // The cover for the new posts.
                FightFrame f = FightCover.FrameOf(l, room, v);
                List<CoverThing> links = f != null && f.HasRanged ? FightCover.Links(l, f, v) : new List<CoverThing>();
                var cross = new List<LevelPoint>();
                for (int i = 0; i < room.EnemyGroups.Count; i++)
                {
                    EnemyGroup g = room.EnemyGroups[i];
                    LevelPoint q = room.EnemyPoints[i];
                    if (g.Ranged?.Post == RangedPost.NearFlank && g.Kind == EnemyKind.Shooter && f != null)
                    {
                        t.NearShooters++;
                        if (room.Props.Any(p => FightCover.LowServes(l, f, p, q, v))) t.NearLow++;
                    }
                    if (g.Ranged?.Post == RangedPost.Crossfire) cross.Add(q);
                    if (IsRanged(g) && f != null && line.DistanceTo(q) > r.NearToLine + 1e-9)
                    {
                        t.DeepPosts++;
                        if (links.Any(k => FightCover.Hides(l, room, k, q, v))) t.DeepHidden++;
                    }
                }
                if (cross.Count == 2)
                    for (int k = 0; k < 2; k++)
                    {
                        t.CrossPosts++;
                        if (LevelRanged.CrossCovered(l, room, cross[k], cross[1 - k], r)) t.CrossCovered++;
                    }
            }
        }

        static string Spread(Tally t) => t.Shares.Count == 0 ? "none" : string.Format(Inv, "median {0:P0}, quartiles {1:P0} to {2:P0}, in the far band {3:P0}",
            Quantile(t.Shares, 0.5), Quantile(t.Shares, 0.25), Quantile(t.Shares, 0.75), t.Shares.Count(s => s >= 0.6 - 1e-9) / (double)t.Shares.Count);

        static string P(int a, int b) => b == 0 ? "-" : (a / (double)b).ToString("P0", Inv);

        /// <summary>
        /// A thousand seeds a floor, floors 1 to 5, version 23 beside version 22 (the switch off), the lancers named as asked: the rules
        /// as assertions, and the numbers printed.
        /// </summary>
        [TestCase(true), TestCase(false), Timeout(3600000)]
        public void AThousandSeedsAFloor_ThePatternsKeepTheirRules(bool lancers)
        {
            LevelValues after = After(lancers), before = Before(lancers);
            var lines = new List<string> { "RANGEDFLOORS a thousand seeds a floor (seeds k * 6151 + 124), the lancers " + (lancers ? "on" : "off") };
            for (int floor = 1; floor <= 5; floor++)
            {
                var a = new Tally();
                var b = new Tally();
                for (ulong k = 0; k < 1000; k++)
                {
                    ulong seed = k * 6151UL + 124UL;
                    LevelLayout la = LevelGenerator.Generate(seed, floor, after), lb = LevelGenerator.Generate(seed, floor, before);
                    string where = la.SeedText + " floor " + floor;
                    Assert.That(LevelValidator.Validate(la, after), Is.Empty, where + ": valid");
                    if (la.Attempt != lb.Attempt) a.Reattempted++;
                    List<LevelRoom> ra = LevelGeometry.MainPath(la).Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                    List<LevelRoom> rb = LevelGeometry.MainPath(lb).Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit).ToList();
                    Assert.That(ra.Count, Is.EqualTo(rb.Count), where + ": as many fights");
                    for (int i = 0; i < ra.Count; i++)
                    {
                        string room = where + " room " + ra[i].Letter + ra[i].Order;
                        Assert.That(ra[i].EnemyCount, Is.EqualTo(rb[i].EnemyCount), room + ": its count of enemies as before");
                        Dictionary<EnemyKind, int> ka = Kinds(ra[i]), kb = Kinds(rb[i]);
                        Assert.That(ka.OrderBy(kv => kv.Key).ToList(), Is.EqualTo(kb.OrderBy(kv => kv.Key).ToList()), room + ": every kind as before");
                        foreach (EnemyGroup g in ra[i].EnemyGroups)
                        {
                            if (IsRanged(g))
                            {
                                Assert.That(g.Ranged, Is.Not.Null, room + ": every ranged group stood by the rules");
                                if (g.Kind == EnemyKind.Shooter) Assert.That(g.Count, Is.EqualTo(1), room + ": the shooters singly");
                            }
                            if (g.Ranged?.Post == RangedPost.Crossfire)
                            {
                                Assert.That(floor, Is.GreaterThanOrEqualTo(4), room + ": no crossfire before floor 4");
                                Assert.That(ra[i].Kind, Is.EqualTo(RoomKind.Combat), room + ": a crossfire only in a combat room");
                            }
                        }
                        Assert.That(ra[i].EnemyGroups.Count(g => g.Ranged?.Post == RangedPost.NearFlank), Is.LessThanOrEqualTo(1), room + ": one near flank at most");
                        Assert.That(ra[i].EnemyGroups.Where(g => g.Ranged?.ClusterSize > 1).Select(g => g.Ranged.Cluster).Distinct().Count(), Is.LessThanOrEqualTo(1), room + ": one pair or nest at most");
                    }
                    a.Floors++;
                    Count(la, after, a, true);
                    Count(lb, before, b, false);
                }
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: crossfire by the way in: heading north or south {1} of {2} ({3}), from the side {4} of {5} ({6}); rooms whose crossfire has a post in the far band (the room's group at the back) {7}",
                    floor, a.CrossNorthSouth, a.CombatNorthSouth, P(a.CrossNorthSouth, a.CombatNorthSouth), a.CrossSide, a.CombatSide, P(a.CrossSide, a.CombatSide), a.CrossAtBack));
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: of {1} combat rooms ({2} with two or more ranged, {3} of these on floors 4 and 5): near flank {4} ({5} of those with two or more), pair {6}, nest {7}, ladder {8}, crossfire {9} ({10} of those that could)",
                    floor, a.Combat, a.Eligible, a.CrossEligible, P(a.NearFlank, a.Combat), P(a.NearFlank, a.Eligible), P(a.Pair, a.Combat), P(a.Nest, a.Combat),
                    P(a.Ladder, a.Combat), P(a.Crossfire, a.Combat), P(a.Crossfire, a.CrossEligible)));
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: depth share of the ranged posts BEFORE {1}; AFTER {2}", floor, Spread(b), Spread(a)));
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: rooms with every ranged enemy in the far band BEFORE {1} of {2} ({3}), AFTER {4} of {5} ({6}); rooms with two ranged groups or more in the far band BEFORE {7}, AFTER {8}",
                    floor, b.AllFar, b.RangedRooms, P(b.AllFar, b.RangedRooms), a.AllFar, a.RangedRooms, P(a.AllFar, a.RangedRooms), b.MoreThanOneFar, a.MoreThanOneFar));
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: ranged posts in view at the waking BEFORE {1} of {2} ({3}), AFTER {4} of {5} ({6}); posts no pattern found room for (elsewhere) {7}; warders in the pack {8} of {9}",
                    floor, b.InView, b.Posts, P(b.InView, b.Posts), a.InView, a.Posts, P(a.InView, a.Posts), a.Elsewhere, a.InPack, a.Warders));
                lines.Add(string.Format(Inv, "RANGEDFLOORS floor {0}: near flank shooters with a low prop on their line {1} of {2}; crossfire posts with tall cover before them {3} of {4}; ranged posts past 11 m from the line hidden by a tall cover of the chain {5} of {6}; floors made at another attempt than version 22's {7}",
                    floor, a.NearLow, a.NearShooters, a.CrossCovered, a.CrossPosts, a.DeepHidden, a.DeepPosts, a.Reattempted));
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        /// <summary>The room lab builds every room with every preset (three seeds each) with the switch on, each a room of a floor by its check (its posts' rules among them).</summary>
        [Test, Timeout(3600000)]
        public void TheRoomLab_BuildsEveryPreset()
        {
            var v = new LevelValues();
            int built = 0;
            foreach (LabEntry e in RoomLab.Entries(v))
                foreach (LabMix mix in RoomLab.Mixes)
                    for (ulong s = 1; s <= 3; s++)
                    {
                        LevelLayout l = RoomLab.Build(e, mix, s * 7777UL + 124UL, v);
                        Assert.That(RoomLab.Faults(l, mix, v), Is.Empty, RoomLab.Describe(e, mix, s));
                        built++;
                    }
            TestContext.WriteLine("RANGEDLAB " + built + " lab rooms built");
        }

        /// <summary>
        /// Where he sees each pattern quickly: in a run (its seed's name, the floor, the room by its letter and number), entered heading
        /// north (the door in on the room's south wall, the camera looking in) and heading south (the door in on its north wall); and in
        /// the room lab (always entered from the south), the share of seeds each preset shows it with, and a seed that does.
        /// </summary>
        [Test, Timeout(3600000)]
        public void WhereHeSeesEachPattern()
        {
            var v = new LevelValues();
            string[] patterns = { LevelRanged.NearFlankWord, LevelRanged.PairWord, LevelRanged.NestWord, LevelRanged.LadderWord, LevelRanged.CrossfireWord, LevelRanged.PackWord };
            var found = new Dictionary<string, string>();
            for (ulong run = 1; run < 400 && found.Count < patterns.Length * 4; run++)
            {
                ulong runSeed = run * 7919UL + 124UL;
                string before = null;
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(runSeed, floor), floor, v, exitBefore: before);
                    before = WayDowns.Shown(l);
                    int number = 0;
                    foreach (LevelRoom r in LevelGeometry.MainPath(l))
                    {
                        if (r.Kind != RoomKind.Combat && r.Kind != RoomKind.Exit) continue;
                        number++;
                        LevelDoor entry = LevelGeometry.EntryDoor(l, r);
                        int wall = entry != null ? LevelGeometry.WallOf(r.Rect, entry) : -1;
                        string heading = wall == LevelGenerator.South ? "north" : wall == LevelGenerator.North ? "south" : wall == LevelGenerator.West ? "east" : wall == LevelGenerator.East ? "west" : null;
                        if (heading == null) continue;
                        List<string> w = LevelRanged.PatternsOf(r).Split('+').ToList();
                        foreach (string p in patterns)
                        {
                            string key = p + " heading " + heading;
                            if (!w.Contains(p) || found.ContainsKey(key)) continue;
                            found[key] = string.Format(Inv, "run {0} floor {1} room {2}{3} (fight {4} of the floor, {5}; {6})", LevelSeed.ToText(runSeed), floor, r.Letter, r.Order,
                                                       number, r.Template != null ? r.Template.Template.Name : "drawn shape", LevelRanged.PatternsOf(r));
                        }
                    }
                }
            }
            var lines = new List<string>();
            foreach (string p in patterns)
                foreach (string h in new[] { "north", "south", "east", "west" })
                    lines.Add("RANGEDWHERE " + p + " heading " + h + ": " + (found.TryGetValue(p + " heading " + h, out string s) ? s : "none found"));
            // The lab: each preset's share of seeds with each pattern, over the ordinary rooms of the floors' templates and the drawn shape.
            foreach (LabMix mix in RoomLab.Mixes)
            {
                var counts = patterns.ToDictionary(p => p, p => 0);
                var example = new Dictionary<string, string>();
                int rooms = 0;
                foreach (LabEntry e in RoomLab.EntriesOf(LabKind.Ordinary, v).Where(x => x.InFloors))
                    for (ulong s = 1; s <= 20; s++)
                    {
                        ulong seed = s * 104729UL + 124UL;
                        LevelLayout l = RoomLab.Build(e, mix, seed, v);
                        rooms++;
                        List<string> w = LevelRanged.PatternsOf(l.Rooms[1]).Split('+').ToList();
                        foreach (string p in patterns)
                            if (w.Contains(p))
                            {
                                counts[p]++;
                                if (!example.ContainsKey(p)) example[p] = RoomLab.Describe(e, mix, seed);
                            }
                    }
                lines.Add("RANGEDWHERE lab " + mix.Name + " (" + rooms + " rooms): " + string.Join(", ", patterns.Select(p => p + " " + P(counts[p], rooms) + (example.TryGetValue(p, out string x) ? " e.g. " + x : ""))));
            }
            TestContext.WriteLine(string.Join("\n", lines));
            Assert.That(found.Keys.Count(k => k.StartsWith(LevelRanged.NearFlankWord, StringComparison.Ordinal)), Is.EqualTo(4), "a near flank from every side");
        }
    }
}
