using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Line-ups on a thousand seeds a floor (work order 135), before (the switch off: version 24's floors) and after (on: version 25's):
    /// every floor valid; only the line-up's two kinds in a line-up room; one full room a floor, never the first combat room nor the elite
    /// room; neighbours never of one line-up; and the producer's numbers for him, written as "LINEUPS" lines: the share of combat rooms
    /// lacking one of the three core kinds, each line-up's draws, the rooms' weight before and after (mean and spread), enemies a room, the
    /// share of rooms with swarmers, and how often neighbouring rooms hold the same kinds.
    /// </summary>
    public class LineUpsFloorsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static LevelValues On => new LevelValues();
        static LevelValues Off => new LevelValues { LineUps = false };

        static readonly EnemyKind[] Core = { EnemyKind.Swarmer, EnemyKind.Brute, EnemyKind.Shooter };

        sealed class Tally
        {
            public readonly List<double> Weight = new List<double>(), LineUpWeight = new List<double>(), LineUpTarget = new List<double>();
            public readonly List<int> Enemies = new List<int>();
            public int Rooms, LackingCore, WithSwarmers, NeighbourPairs, NeighbourSameKinds, Within;
            public readonly Dictionary<string, int> Drawn = new Dictionary<string, int>();
            public readonly HashSet<string> KindSets = new HashSet<string>();
            public readonly Dictionary<string, List<double>> ByLineUp = new Dictionary<string, List<double>>();
            public readonly Dictionary<string, List<int>> CountByLineUp = new Dictionary<string, List<int>>();
        }

        static string KindSet(LevelRoom r) =>
            string.Join("+", r.EnemyGroups.Where(g => g != null && !g.Boss).Select(g => EnemyKinds.Word(g.Kind)).Distinct().OrderBy(k => k));

        static double Mean(List<double> v) => v.Count == 0 ? double.NaN : v.Average();
        static double Sd(List<double> v)
        {
            if (v.Count < 2) return 0;
            double m = v.Average();
            return Math.Sqrt(v.Sum(x => (x - m) * (x - m)) / (v.Count - 1));
        }

        static void Count(Tally t, LevelLayout l, LevelLayout today, LineUpValues w)
        {
            List<LevelRoom> chain = LevelGeometry.MainPath(l).Where(r => r.Kind == RoomKind.Combat).ToList();
            List<LevelRoom> was = today == null ? null : LevelGeometry.MainPath(today).Where(r => r.Kind == RoomKind.Combat).ToList();
            for (int i = 0; i < chain.Count; i++)
            {
                LevelRoom r = chain[i];
                t.Rooms++;
                var kinds = new HashSet<EnemyKind>(r.EnemyGroups.Where(g => g != null && !g.Boss).Select(g => g.Kind));
                if (Core.Any(k => !kinds.Contains(k))) t.LackingCore++;
                if (kinds.Contains(EnemyKind.Swarmer)) t.WithSwarmers++;
                double weight = w.WeightOf(r.EnemyGroups);
                t.Weight.Add(weight);
                t.Enemies.Add(r.EnemyGroups.Where(g => g != null && !g.Elite).Sum(g => g.Count));
                t.KindSets.Add(KindSet(r));
                string name = r.LineUp?.Name ?? "-";
                t.Drawn[name] = (t.Drawn.TryGetValue(name, out int n) ? n : 0) + 1;
                if (!t.ByLineUp.ContainsKey(name)) { t.ByLineUp[name] = new List<double>(); t.CountByLineUp[name] = new List<int>(); }
                t.ByLineUp[name].Add(weight);
                t.CountByLineUp[name].Add(r.EnemyGroups.Where(g => g != null && !g.Elite).Sum(g => g.Count));
                if (LevelLineUps.Fills(r) && was != null && i < was.Count)
                {
                    double target = w.WeightOf(was[i].EnemyGroups);
                    t.LineUpWeight.Add(weight);
                    t.LineUpTarget.Add(target);
                    if (Math.Abs(weight - target) <= w.WeightTolerance * target + 1e-9) t.Within++;
                }
                if (i > 0)
                {
                    t.NeighbourPairs++;
                    if (KindSet(chain[i - 1]) == KindSet(r)) t.NeighbourSameKinds++;
                }
            }
        }

        static string Lines(string label, int floor, Tally t)
        {
            var sb = new StringBuilder();
            sb.AppendFormat(Inv, "LINEUPS {0} floor {1}: {2} combat rooms; lacking a core kind {3:P1}; with swarmers {4:P1}; weight {5:0.0} (sd {6:0.0}); enemies {7:0.0} (sd {8:0.0}); kind sets {9} ({10:0.0} per 100 rooms); neighbours of the same kinds {11:P1}",
                            label, floor, t.Rooms, t.LackingCore / (double)t.Rooms, t.WithSwarmers / (double)t.Rooms, Mean(t.Weight), Sd(t.Weight),
                            t.Enemies.Average(), Sd(t.Enemies.Select(x => (double)x).ToList()), t.KindSets.Count, 100.0 * t.KindSets.Count / t.Rooms,
                            t.NeighbourSameKinds / (double)Math.Max(1, t.NeighbourPairs));
            if (t.LineUpWeight.Count > 0)
            {
                List<double> off = t.LineUpWeight.Zip(t.LineUpTarget, (a, b) => (a - b) / b).ToList();
                sb.AppendFormat(Inv, "\nLINEUPS {0} floor {1}: line-up rooms {2}, their weight {3:0.0} (sd {4:0.0}) against today's same rooms {5:0.0} (sd {6:0.0}); off by {7:+0.0%;-0.0%} on average (sd {8:0.0%}); within the tolerance {9:P1}",
                                label, floor, t.LineUpWeight.Count, Mean(t.LineUpWeight), Sd(t.LineUpWeight), Mean(t.LineUpTarget), Sd(t.LineUpTarget),
                                Mean(off), Sd(off), t.Within / (double)t.LineUpWeight.Count);
            }
            foreach (KeyValuePair<string, int> kv in t.Drawn.OrderByDescending(kv => kv.Value))
                sb.AppendFormat(Inv, "\nLINEUPS {0} floor {1}:   {2,-22} {3,5} rooms ({4:P1}), weight {5:0.0} (sd {6:0.0}), enemies {7:0.0} ({8} to {9})", label, floor, kv.Key, kv.Value,
                                kv.Value / (double)t.Rooms, Mean(t.ByLineUp[kv.Key]), Sd(t.ByLineUp[kv.Key]), t.CountByLineUp[kv.Key].Average(),
                                t.CountByLineUp[kv.Key].Min(), t.CountByLineUp[kv.Key].Max());
            return sb.ToString();
        }

        /// <summary>The numbers for him, a given number of seeds a floor (a thousand for the report).</summary>
        static void Measure(int seeds)
        {
            LineUpValues w = new LineUpValues();
            LevelValues On = LineUpsFloorsTests.On;
            string tag = "";
            var all = new List<string>();
            int lacking = 0, rooms = 0, lackingBefore = 0, roomsBefore = 0;
            for (int floor = 1; floor <= 5; floor++)
            {
                var before = new Tally();
                var after = new Tally();
                for (ulong s = 0; s < (ulong)seeds; s++)
                {
                    LevelLayout off = LevelGenerator.Generate(s, floor, Off), on = LevelGenerator.Generate(s, floor, On);
                    Assert.That(LevelValidator.Validate(on, On), Is.Empty, "seed " + s + " floor " + floor);
                    Count(before, off, null, w);
                    Count(after, on, off, w);
                }
                all.Add(Lines("before", floor, before));
                all.Add(Lines("after" + tag, floor, after));
                // Work order 136's wave 1 is the lead: what the lead's top count weighs, against the heaviest room of version 24 on the floor.
                double heaviest = before.Weight.Max();
                foreach (LineUp u in On.LineUpRules.Of(floor))
                {
                    LevelLineUps.Counts(u, floor, false, On, out _, out int leadHi, out _, out int partnerHi);
                    double wave1 = leadHi * w.WeightOf(u.Lead);
                    all.Add(string.Format(Inv, "LINEUPS wave 1 floor {0}: {1,-22} the lead at most {2} {3}s, weight {4:0.0} (the partner at most {5}); the heaviest room of version 24 {6:0.0}{7}",
                                          floor, u.Name, leadHi, EnemyKinds.Word(u.Lead), wave1, partnerHi, heaviest,
                                          wave1 > heaviest * (1 + w.WeightTolerance) ? "  PAST IT BY MORE THAN THE TOLERANCE" : ""));
                }
                lacking += after.LackingCore;
                rooms += after.Rooms;
                lackingBefore += before.LackingCore;
                roomsBefore += before.Rooms;
            }
            all.Add(string.Format(Inv, "LINEUPS all floors: combat rooms lacking at least one of the three core kinds, before {0:P1} of {1}, after {2:P1} of {3}",
                                  lackingBefore / (double)roomsBefore, roomsBefore, lacking / (double)rooms, rooms));
            // As a session counts it (research R3's fourth number: 13 kind sets in his 135 fights): runs of five floors, the different sets
            // of kinds among each hundred combat rooms in the order she meets them (four runs), the elite room counted, the exit room not.
            foreach ((string label, LevelValues v) in new[] { ("before", Off), ("after" + tag, On) })
            {
                var sets = new List<int>();
                var window = new HashSet<string>();
                int inWindow = 0;
                for (ulong r = 0; r < (ulong)Math.Max(4, seeds / 5); r++)
                {
                    string exitBefore = null;
                    for (int floor = 1; floor <= 5; floor++)
                    {
                        LevelLayout l = LevelGenerator.Generate(RunSeeds.FloorSeed(r * 104729UL + 103UL, floor), floor, v, exitBefore: exitBefore);
                        exitBefore = WayDowns.Shown(l);
                        foreach (LevelRoom c in LevelGeometry.MainPath(l).Where(x => x.Kind == RoomKind.Combat))
                        {
                            window.Add(KindSet(c));
                            if (++inWindow < 100) continue;
                            sets.Add(window.Count);
                            window.Clear();
                            inWindow = 0;
                        }
                    }
                }
                all.Add(string.Format(Inv, "LINEUPS runs {0}: different sets of kinds a hundred combat rooms, {1:0.0} (from {2} to {3}, {4} hundreds)",
                                      label, sets.Average(), sets.Min(), sets.Max(), sets.Count));
            }
            string text = string.Join("\n", all);
            TestContext.WriteLine(text);
        }

        [Test, Explicit("a measurement: a thousand seeds a floor, for the producer's report"), Timeout(3600000)]
        public void Measure_AThousandSeedsAFloor_BeforeAndAfter() => Measure(1000);

        [Test, Explicit("a measurement while tuning: two hundred seeds a floor"), Timeout(3600000)]
        public void Measure_TwoHundredSeedsAFloor_BeforeAndAfter() => Measure(200);


    }
}
