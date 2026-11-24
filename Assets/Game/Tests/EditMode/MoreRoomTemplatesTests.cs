using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// More rooms drawn by hand (work order 072; Kristian, the evening of 2026-11-21, asked what more to build, ticked "Flere
    /// rom-maler": eleven templates repeat quickly over five floors of eight combat rooms). The second set, each template drawn
    /// as text here: every one reads, and in all eight orientations, for every draw of its ? cells and every pair of its door
    /// marks, keeps the rules the eleven keep (work order 051's with no filling, its marks open, the area rule; work order 059's
    /// passages at both ends are PassagesTests', over the whole library). Over four hundred seeds of every floor number with the
    /// second set turned on (it is off by default since 2026-11-22, until Kristian has played its rooms): every floor valid, the second set's rooms on their marks, at their size, with their cells, falling back seldom,
    /// holding the same enemies as with the second set off. The draw: how often each of the whole set comes, on a floor and over a
    /// run of five floors. The measures: the kit's pieces (the plan's, on .NET) and the walk to the exit room's middle, against the
    /// second set off. With the switch off the floors are version 16's, field for field. Writes what it measures ("MORESHARES",
    /// "MORESTATS", "MORERUNS", "MOREKIT", "MOREWALK", "MOREOFFPRINT", "MORECASES").
    /// </summary>
    public class MoreRoomTemplatesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly int[] Walls = { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West };
        static readonly LevelValues SecondSetOff = new LevelValues { MoreTemplates = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 103: the throne rooms on floors 1 to 4, as this class measures
        static readonly LevelValues Version16 = new LevelValues { MoreTemplates = false, ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 087: nor the act's plan; 103: nor the exit rooms of their own
        // The second set turned on (off by default since 2026-11-22, until Kristian has played its rooms in the room lab).
        static LevelValues On => new LevelValues { MoreTemplates = true, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }; // work order 103: as SecondSetOff

        static List<RoomTemplate> More => RoomTemplateLibrary.All.Where(t => t.Since == RoomTemplateLibrary.MoreSince).ToList();
        static List<RoomTemplate> First => RoomTemplateLibrary.All.Where(t => t.Since < RoomTemplateLibrary.MoreSince).ToList();

        // ---------------- The second set, drawn as text ----------------

        // Each template of the second set as it must read (north at the top, she comes in from the bottom), its room, family
        // and first floor: the producer shows these to Kristian.
        static readonly (string name, RoomTemplateFor kind, string family, int from, string[] rows)[] Drawn =
        {
            ("ring", RoomTemplateFor.Combat, "ring", 1, new[]
            {
                ". O . . O .",
                ". . # # . .",
                ". h # # h .",
                ". . . . . .",
                "O . . . . O",
                ". . . c . .",
                ". . . . . .",
                ". . I I . .",
            }),
            ("broken-ring", RoomTemplateFor.Combat, "ring", 2, new[]
            {
                ". h . O . h .",
                ". # # . # # .",
                ". # . . . # .",
                "O . . . . . O",
                ". # . . . # .",
                ". # # . # # .",
                ". . . . . . .",
                ". . I I I . .",
            }),
            ("two-islands", RoomTemplateFor.Combat, "islands", 1, new[]
            {
                ". . . . O . . . .",
                "O h . . . . . h O",
                ". # # . . . # # .",
                ". # # . c . # # .",
                ". . . . . . . . .",
                ". . . I I I . . .",
            }),
            ("pillared-hall", RoomTemplateFor.Combat, "hall", 1, new[]
            {
                ". . . O . . .",
                "h # . . . # h",
                "O . . . . . O",
                ". # . . . # .",
                ". . . . . . .",
                ". # . . . # .",
                ". . . . . . .",
                ". . I I I . .",
            }),
            ("horseshoe", RoomTemplateFor.Combat, "horseshoe", 1, new[]
            {
                "# . . # # . . #",
                "O h . # # . h O",
                ". . . . . . . .",
                ". . . . . . . .",
                ". . . c . . . .",
                ". . . . . . . .",
                ". . . I I . . .",
            }),
            ("hourglass", RoomTemplateFor.Combat, "twin", 2, new[]
            {
                "# . . . # . O . #",
                ". c . . # . . . .",
                ". h . . . . . h O",
                ". . . . . . . . .",
                ". . . . . . . c .",
                "# . I . # . . . #",
            }),
            ("stepped", RoomTemplateFor.Combat, "stepped", 1, new[]
            {
                ". . O O . . # #",
                "O h . . . . . #",
                ". . . . c . . .",
                ". . . . . . . .",
                ". . . . . . . .",
                "# . . . . . . .",
                "# # . . . I . .",
            }),
            ("alcoves", RoomTemplateFor.Combat, "alcoves", 1, new[]
            {
                ". h # h . O h # h .",
                ". . # . . . . # . .",
                "O . . . . . . . . O",
                ". . . . . . . . . .",
                ". . . . I I . . . .",
            }),
            ("arena", RoomTemplateFor.Combat, "arena", 2, new[]
            {
                ". . . . O . . . .",
                ". # . h . h . # .",
                "O . . . . . . . O",
                ". # . . . . . # .",
                ". . . . I . . . .",
            }),
            ("dais", RoomTemplateFor.Combat, "pillars", 1, new[]
            {
                ". . # A A # . .",
                ". . . h h . . .",
                "O . # . . # . O",
                ". . . . . . . .",
                "# . . . . . . #",
                ". . . . . . . .",
                ". . . I I . . .",
            }),
            ("lopsided", RoomTemplateFor.Combat, "lopsided", 2, new[]
            {
                ". . . O . . . .",
                "O h . . . . # .",
                ". . # . . . # h",
                ". . . . . . . .",
                ". . . . . # # .",
                ". . . . . # # .",
                ". I I . . . . .",
            }),
            ("pit", RoomTemplateFor.Elite, "elite", 2, new[]
            {
                "# # . . A . . # #",
                "# . . h . h . . #",
                ". . # . . . # . .",
                "O . . . e . . . O",
                ". . # . . . # . .",
                "# . . . . . . . #",
                "# # . I . I . # #",
            }),
            ("gallery", RoomTemplateFor.Elite, "elite", 1, new[]
            {
                "# # . A . # #",
                ". . . e . . .",
                "O h . . . h O",
                "# # . . . # #",
                ". . . . . . .",
                ". . . . . . .",
                "# # . . . # #",
                ". . . . . . .",
                ". . . I . . .",
            }),
            ("throne-pillars", RoomTemplateFor.Exit, "throne", 1, new[]
            {
                "# . . . A . . . #",
                ". . . . . . . . .",
                ". # . . . . . # .",
                ". . . . . . . . .",
                ". # . . . . . # .",
                ". . . . . . . . .",
                ". # . . . . . # .",
                ". . . . . . . . .",
                "# . . . I . . . #",
            }),
            ("throne-wide", RoomTemplateFor.Exit, "throne", 1, new[]
            {
                "# # . . A A . . # #",
                "# . . . . . . . . #",
                ". . # . . . . # . .",
                ". . . . . . . . . .",
                ". . . . . . . . . .",
                ". . # . . . . # . .",
                ". . . . . . . . . .",
                "# . . . I I . . . #",
            }),
        };

        /// <summary>A template's grid as rows, north at the top, its cells separated by single spaces.</summary>
        static string[] Rows(RoomTemplate t)
        {
            var rows = new string[t.Height];
            for (int ay = t.Height - 1; ay >= 0; ay--)
                rows[t.Height - 1 - ay] = string.Join(" ", Enumerable.Range(0, t.Width).Select(ax => t.At(ax, ay).ToString()));
            return rows;
        }

        static int Count(RoomTemplate t, char mark)
        {
            int n = 0;
            for (int ay = 0; ay < t.Height; ay++)
                for (int ax = 0; ax < t.Width; ax++)
                    if (t.At(ax, ay) == mark) n++;
            return n;
        }

        /// <summary>
        /// The second set reads as drawn above, each name its own and none of the eleven's, after the eleven in the library's
        /// order; and every one of them, in all eight orientations, for every draw of its ? cells and every door-in and door-out
        /// pair, keeps every rule of <see cref="RoomTemplates.Faults"/> (work order 051's with no filling and the gate, its marks
        /// open, the share and the open-area rule), every fault of every template listed at once. Its blocks: no solid block more
        /// than two cells across either way (the camera loses at most two cells behind one, whichever way the room is turned).
        /// </summary>
        [Test]
        public void TheSecondSet_ReadsAsDrawn_AndKeepsEveryRule_InAllEightOrientations()
        {
            Assert.That(RoomTemplateLibrary.Errors, Is.Empty, "every grid reads");
            List<RoomTemplate> more = More;
            Assert.That(more.Select(t => t.Name), Is.EqualTo(Drawn.Select(d => d.name)), "the second set, in its order");
            Assert.That(RoomTemplateLibrary.All.Take(First.Count), Is.EqualTo(First), "the eleven first in the library's order");
            Assert.That(First.Count, Is.EqualTo(11));
            Assert.That(RoomTemplateLibrary.All.Select(t => t.Name).Distinct().Count(), Is.EqualTo(RoomTemplateLibrary.All.Count), "every name its own");
            LevelValues v = On;
            var faults = new List<string>();
            var lines = new List<string>();
            int cases = 0;
            foreach ((string name, RoomTemplateFor kind, string family, int from, string[] rows) in Drawn)
            {
                RoomTemplate t = more.First(x => x.Name == name);
                Assert.That(Rows(t), Is.EqualTo(rows), name + " reads as drawn");
                Assert.That((t.For, t.Family, t.FromFloor, t.Weight), Is.EqualTo((kind, family, from, 1f)), name + ": its room, family, first floor and weight 1, the eleven's");
                Assert.That(t.FarNotSouth, Is.EqualTo(kind == RoomTemplateFor.Exit), name);
                foreach (int wall in Walls)
                    foreach (bool mirror in new[] { false, true })
                        RoomTemplates.Faults(t, wall, mirror, v, faults);
                int ins = Count(t, 'I'), outs = kind == RoomTemplateFor.Exit ? 1 : Count(t, 'O');
                int each = (1 << t.Drawn.Count) * ins * outs;
                cases += 8 * each;
                int solid = Count(t, '#') + t.Drawn.Count;
                lines.Add(string.Format(Inv, "{0} {1}x{2} ({3}), {4} of {5} cells solid before the gate, {6} cases an orientation", name, t.Width, t.Height, kind, solid, t.Width * t.Height, each));
                Assert.That(LargestBlock(t), Is.LessThanOrEqualTo(2), name + ": no solid block more than two cells across");
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults.Take(80)));
            // Work order 089 (changed deliberately): the whole library but its third set, which has a switch of its own.
            Assert.That(RoomTemplateLibrary.Usable(v), Is.EquivalentTo(RoomTemplateLibrary.All.Where(t => t.Since != RoomTemplateLibrary.ExitSince)), "the whole library is usable");
            TestContext.WriteLine("MORECASES " + cases + " cases in all (eight orientations): " + string.Join("; ", lines));
        }

        /// <summary>The most cells across, either way, of any block of solid cells joined side to side (the # and ? cells).</summary>
        static int LargestBlock(RoomTemplate t)
        {
            var seen = new bool[t.Width * t.Height];
            int most = 0;
            for (int k = 0; k < seen.Length; k++)
            {
                int ax = k % t.Width, ay = k / t.Width;
                if (seen[k] || (t.At(ax, ay) != '#' && t.At(ax, ay) != '?')) continue;
                int x0 = ax, x1 = ax, y0 = ay, y1 = ay;
                var queue = new Queue<int>();
                seen[k] = true;
                queue.Enqueue(k);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue(), cx = c % t.Width, cy = c / t.Width;
                    x0 = Math.Min(x0, cx); x1 = Math.Max(x1, cx); y0 = Math.Min(y0, cy); y1 = Math.Max(y1, cy);
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, ny = cy + dy;
                        if (nx < 0 || ny < 0 || nx >= t.Width || ny >= t.Height) continue;
                        int n = ny * t.Width + nx;
                        if (seen[n] || (t.At(nx, ny) != '#' && t.At(nx, ny) != '?')) continue;
                        seen[n] = true;
                        queue.Enqueue(n);
                    }
                }
                most = Math.Max(most, Math.Max(x1 - x0 + 1, y1 - y0 + 1));
            }
            return most;
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_Off_LeavesTheSecondSetOut_On_DrawsItBesideTheEleven()
        {
            Assert.That(RoomTemplateLibrary.Usable(SecondSetOff), Is.EqualTo(First), "off: the eleven, in their order");
            // Work order 089 (changed deliberately): the whole library but its third set, which has a switch of its own.
            Assert.That(RoomTemplateLibrary.Usable(On), Is.EqualTo(RoomTemplateLibrary.All.Where(t => t.Since != RoomTemplateLibrary.ExitSince)), "on: the whole library, in its order");
            Assert.That(new LevelValues().MoreTemplates, Is.False, "off by default (2026-11-22: until Kristian has played the second set in the room lab)");
            Assert.That(RoomTemplateLibrary.Usable(new LevelValues()).Where(t => !ExitRooms.IsWayDown(t)), Is.EqualTo(First), "the defaults: the eleven (work order 103: and the third set, on since)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(22)); // work order 087; 088: 19; 103: 20; 104: 21; 118: 22
            int seen = 0;
            for (ulong s = 0; s < 40; s++)
            {
                LevelLayout off = LevelGenerator.Generate(s, 1 + (int)(s % 5), SecondSetOff);
                Assert.That(off.Rooms.All(r => r.Template == null || r.Template.Template.Since < RoomTemplateLibrary.MoreSince), "seed " + s);
                Assert.That(LevelGenerator.Generate(s, 1 + (int)(s % 5), new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false }).Describe(), Is.EqualTo(off.Describe()), "the defaults are the switch off, seed " + s); // work order 103: the exit rooms of their own named off on both sides
                seen += LevelGenerator.Generate(s, 1 + (int)(s % 5), On).Rooms.Count(r => r.Template != null && r.Template.Template.Since == RoomTemplateLibrary.MoreSince);
            }
            Assert.That(seen, Is.GreaterThan(40), "on, the second set is drawn");
        }

        // ---------------- Four hundred seeds a floor ----------------

        static string Groups(LevelRoom r) => string.Join(",", r.EnemyGroups.Select(g => g.ToString()));

        /// <summary>
        /// Four hundred seeds of each floor number with the second set on (the earlier sweeps' count): every floor valid, every room of
        /// the second set at its template's size, its doors on its marks, its cells its template's when stamped, falling back in
        /// fewer than 2 rooms of 100 (at the stamp or by <c>PlaceAgainPlainer</c>, when its enemies find no place: a template with
        /// too little floor for its room's enemies shows here); the 99th percentile of attempts at most 8; and for the first two
        /// hundred seeds the same enemies room for room as the floor with the second set off (<see cref="LevelGenerator.FightsOf"/>
        /// is untouched).
        /// </summary>
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void FourHundredSeeds_TheSecondSetsRooms_KeepTheirRules_AndHoldTheSameEnemies(int floor)
        {
            LevelValues on = On;
            const int seeds = 400, compared = 200;
            var share = new Dictionary<string, int>();
            var back = new Dictionary<string, int>();
            var attempts = new List<int>();
            int rooms = 0, moreRooms = 0;
            for (ulong s = 0; s < seeds; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, on);
                string where = "seed " + s + " floor " + floor;
                attempts.Add(level.Attempt);
                Assert.That(LevelValidator.Validate(level, on), Is.Empty, where + "\n" + LevelMap.Draw(level));
                foreach (LevelRoom room in level.Rooms)
                {
                    TemplatePlacement o = room.Template;
                    if (o == null) continue;
                    rooms++;
                    string key = o.Template.Name;
                    share[key] = share.TryGetValue(key, out int n) ? n + 1 : 1;
                    if (room.Shape != RoomShape.Template) back[key] = back.TryGetValue(key, out int b) ? b + 1 : 1;
                    if (o.Template.Since != RoomTemplateLibrary.MoreSince) continue;
                    moreRooms++;
                    string name = room.Letter + room.Order.ToString(Inv) + " (" + o + ") " + where;
                    Assert.That((room.Rect.Width, room.Rect.Height), Is.EqualTo((o.Width, o.Height)), name);
                    foreach ((LevelDoor d, char mark) in new[] { (LevelGeometry.EntryDoor(level, room), 'I'), (LevelGeometry.ExitDoor(level, room), 'O') })
                        if (d != null)
                            foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in d.CellPairs())
                            {
                                (int x, int y) c = room.Rect.Contains(pair.Key.x, pair.Key.y) ? pair.Key : pair.Value;
                                Assert.That(RoomTemplates.MarkAt(room, c.x, c.y), Is.EqualTo(mark), "a door on its " + mark + ": " + name);
                            }
                    var broken = new List<string>();
                    Assert.That(RoomShapes.Faults(level, room, on, broken), Is.EqualTo(0), name + ": " + string.Join("; ", broken));
                    if (room.Shape != RoomShape.Template) continue;
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                        {
                            char m = RoomTemplates.MarkAt(room, x, y);
                            if (m == '#') Assert.That(room.IsSolid(x, y), "its # solid: " + name);
                            else if (room.IsSolid(x, y)) Assert.That(m, Is.EqualTo('?'), "its solid cells its template's: " + name);
                        }
                }
                if (s >= compared) continue;
                LevelLayout plain = LevelGenerator.Generate(s, floor, SecondSetOff);
                List<LevelRoom> a = level.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList(), b2 = plain.Rooms.Where(r => r.Kind != RoomKind.Passage).ToList();
                Assert.That(a.Select(r => r.Kind.ToString() + r.Elite), Is.EqualTo(b2.Select(r => r.Kind.ToString() + r.Elite)), "the row, " + where);
                for (int i = 0; i < a.Count; i++) Assert.That(Groups(a[i]), Is.EqualTo(Groups(b2[i])), "the enemies of room " + i + ", " + where);
            }
            attempts.Sort();
            int p99 = attempts[Math.Min(attempts.Count - 1, (int)Math.Ceiling(0.99 * attempts.Count) - 1)];
            TestContext.WriteLine(string.Format(Inv, "MORESTATS floor {0} seeds 0-{1}: templated rooms {2}, of the second set {3} ({4:P1}); by template: {5}; fell back: {6}; attempts median {7}, 99th {8}, most {9}",
                floor, seeds - 1, rooms, moreRooms, moreRooms / (double)Math.Max(1, rooms),
                string.Join(", ", RoomTemplateLibrary.All.Where(t => share.ContainsKey(t.Name)).Select(t => t.Name + " " + share[t.Name])),
                back.Count == 0 ? "none" : string.Join(", ", back.Select(k => k.Key + " " + k.Value)), attempts[attempts.Count / 2], p99, attempts[attempts.Count - 1]));
            foreach (KeyValuePair<string, int> k in share)
                Assert.That((back.TryGetValue(k.Key, out int fell) ? fell : 0) / (double)k.Value, Is.LessThan(0.02), k.Key + " falls back in fewer than 2 rooms of 100");
            Assert.That(p99, Is.LessThanOrEqualTo(8), "the 99th percentile of attempts");
            Assert.That(moreRooms, Is.GreaterThan(0));
        }

        // ---------------- The draw ----------------

        /// <summary>How often each template is drawn and how often one comes back, over <paramref name="runs"/> runs of five floors.</summary>
        sealed class DrawCount
        {
            public readonly Dictionary<RoomTemplate, int> Rooms = new Dictionary<RoomTemplate, int>();
            public readonly Dictionary<RoomTemplateFor, int> RoomsOf = new Dictionary<RoomTemplateFor, int>();
            public int Floors, TwiceOnAFloor, RunsWithARepeat, Repeats, MostFloorsOfOne, Distinct, Runs;
            public double CombatPerFloor;
        }

        static DrawCount CountDraws(LevelValues values, int runs)
        {
            var d = new DrawCount { Runs = runs };
            int combat = 0;
            for (ulong run = 0; run < (ulong)runs; run++)
            {
                var inRun = new Dictionary<RoomTemplate, int>();
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout level = LevelGenerator.Generate(RunSeeds.FloorSeed(run, floor), floor, values);
                    d.Floors++;
                    var onFloor = new HashSet<RoomTemplate>();
                    foreach (LevelRoom r in level.Rooms)
                    {
                        if (r.Template == null) continue;
                        RoomTemplate t = r.Template.Template;
                        if (!onFloor.Add(t)) d.TwiceOnAFloor++;
                        d.Rooms[t] = d.Rooms.TryGetValue(t, out int n) ? n + 1 : 1;
                        d.RoomsOf[t.For] = d.RoomsOf.TryGetValue(t.For, out int k) ? k + 1 : 1;
                        if (t.For == RoomTemplateFor.Combat) combat++;
                    }
                    foreach (RoomTemplate t in onFloor) inRun[t] = inRun.TryGetValue(t, out int f) ? f + 1 : 1;
                }
                int repeats = inRun.Values.Sum(f => f - 1);
                d.Repeats += repeats;
                if (repeats > 0) d.RunsWithARepeat++;
                d.MostFloorsOfOne = Math.Max(d.MostFloorsOfOne, inRun.Values.DefaultIfEmpty(0).Max());
                d.Distinct += inRun.Count;
            }
            d.CombatPerFloor = combat / (double)d.Floors;
            return d;
        }

        static string Describe(string what, DrawCount d) =>
            string.Format(Inv, "{0}: {1} runs of five floors, {2:0.00} combat templates a floor; a template twice on a floor {3} times; " +
                               "distinct templates a run {4:0.0}; a template again on a later floor of the same run {5:0.00} times a run, in {6:P0} of the runs; the most floors of one run one template took {7}; by template (its share of its kind's rooms): {8}",
                          what, d.Runs, d.CombatPerFloor, d.TwiceOnAFloor, d.Distinct / (double)d.Runs, d.Repeats / (double)d.Runs, d.RunsWithARepeat / (double)d.Runs, d.MostFloorsOfOne,
                          string.Join(", ", RoomTemplateLibrary.All.Where(t => d.Rooms.ContainsKey(t)).Select(t => t.Name + " " + (d.Rooms[t] / (double)d.RoomsOf[t.For]).ToString("P1", Inv))));

        /// <summary>
        /// The draw over two hundred runs of five floors (each floor's seed from the run's, as a run makes them), with the second
        /// set on and off: never a template twice on a floor (the eleven's rule, kept); every template of the second set drawn;
        /// the second set's combat templates as often as the eleven's, by their mean share of the combat rooms on the floors from
        /// 2 (where every one may come: within a fifth of the first set's mean); fewer templates again on a later floor of the
        /// same run than with the second set off.
        /// </summary>
        [Test]
        public void TheDraw_TwoHundredRuns_TheSecondSetComesAsOftenAsTheFirst_AndARunRepeatsLess()
        {
            DrawCount on = CountDraws(On, 200), off = CountDraws(SecondSetOff, 200);
            TestContext.WriteLine("MORERUNS " + Describe("with the second set", on) + "\nMORERUNS " + Describe("without", off));
            Assert.That(on.TwiceOnAFloor + off.TwiceOnAFloor, Is.EqualTo(0), "never a template twice on a floor");
            foreach (RoomTemplate t in More) Assert.That(on.Rooms.ContainsKey(t), t.Name + " is drawn");
            Assert.That(on.Repeats, Is.LessThan(off.Repeats * 0.6), "fewer templates again on a later floor of a run");
            Assert.That(on.Distinct, Is.GreaterThan(off.Distinct), "more templates seen in a run");
            // As often as the old: the mean share of a combat room of the floors from 2, where every combat template may come.
            var share = new Dictionary<RoomTemplate, int>();
            int combat = 0;
            for (ulong s = 0; s < 400; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, 2 + (int)(s % 4), On);
                foreach (LevelRoom r in level.Rooms)
                {
                    if (r.Template == null || r.Template.Template.For != RoomTemplateFor.Combat) continue;
                    combat++;
                    share[r.Template.Template] = share.TryGetValue(r.Template.Template, out int n) ? n + 1 : 1;
                }
            }
            double Mean(IEnumerable<RoomTemplate> set) => set.Where(t => t.For == RoomTemplateFor.Combat).Average(t => share.TryGetValue(t, out int n) ? n / (double)combat : 0);
            double mFirst = Mean(First), mMore = Mean(More);
            TestContext.WriteLine(string.Format(Inv, "MORESHARES floors 2 to 5, 400 seeds: a combat template of the first set {0:P1} of the combat templated rooms on average, of the second set {1:P1}; {2}",
                mFirst, mMore, string.Join(", ", RoomTemplateLibrary.All.Where(t => t.For == RoomTemplateFor.Combat).Select(t => t.Name + " " + ((share.TryGetValue(t, out int n) ? n : 0) / (double)combat).ToString("P1", Inv)))));
            Assert.That(mMore, Is.InRange(mFirst * 0.8, mFirst * 1.2), "the second set's combat templates as often as the first's");
        }

        /// <summary>The draw over a thousand runs of five floors, with the second set on and off, written out (run by name; about a minute and a half on .NET).</summary>
        [Explicit, Test, Timeout(3600000)]
        public void Measure_TheDraw_AThousandRuns()
        {
            TestContext.WriteLine("MORERUNS " + Describe("with the second set", CountDraws(On, 1000)) + "\nMORERUNS " + Describe("without", CountDraws(SecondSetOff, 1000)));
        }

        // ---------------- The measures ----------------

        static int KitPieces(LevelLayout level)
        {
            CryptKitPlan plan = CryptKitPlan.Make(level, new CryptKitValues());
            return plan.Sections.Count + plan.Arches.Count + plan.Points.Count + plan.Lanterns.Count;
        }

        /// <summary>
        /// The kit's pieces as the plan puts them (<see cref="CryptKitPlan"/>: sections, arches, piers and posts, lanterns; the
        /// built floor places one for each, RoomTemplatesPlayTests counts them under Unity), against the templates off, on seed
        /// 42's floor 5 (work order 058's budget, 1.43 measured for the eleven, at most 1.6) and over a hundred seeds of floor 5;
        /// and the second set off beside them.
        /// </summary>
        [Test]
        public void TheKitsPieces_WithTheSecondSet_StayInTheTemplatesBudget()
        {
            var noTemplates = new LevelValues { Templates = false };
            double Ratio(ulong seed, LevelValues v) => KitPieces(LevelGenerator.Generate(seed, 5, v)) / (double)KitPieces(LevelGenerator.Generate(seed, 5, noTemplates));
            double on42 = Ratio(42UL, On), off42 = Ratio(42UL, SecondSetOff);
            double sumOn = 0, sumOff = 0, mostOn = 0;
            const int seeds = 100;
            for (ulong s = 0; s < seeds; s++)
            {
                double r = Ratio(s, On);
                sumOn += r;
                mostOn = Math.Max(mostOn, r);
                sumOff += Ratio(s, SecondSetOff);
            }
            TestContext.WriteLine(string.Format(Inv, "MOREKIT floor 5, the plan's pieces against the templates off: seed 42 {0:0.00} times with the second set, {1:0.00} without ({2} pieces, {3} without, {4} with no templates); over {5} seeds {6:0.00} times on average with it (the most {7:0.00}), {8:0.00} without",
                on42, off42, KitPieces(LevelGenerator.Generate(42UL, 5, On)), KitPieces(LevelGenerator.Generate(42UL, 5, SecondSetOff)), KitPieces(LevelGenerator.Generate(42UL, 5, noTemplates)),
                seeds, sumOn / seeds, mostOn, sumOff / seeds));
            Assert.That(on42, Is.LessThanOrEqualTo(1.6), "seed 42's floor 5 within the templates' budget");
            Assert.That(sumOn / seeds, Is.LessThanOrEqualTo(1.6), "on average within the templates' budget");
            // Measured on .NET (2026-11-22, with the alcoves and the hourglass as changed after the first Unity run): seed 42 1.29
            // times with the second set, 1.23 without; over the hundred seeds 1.27 on average with it, 1.15 without, the most
            // 1.61. Held to the range found, with a little room.
            Assert.That(sumOn / seeds, Is.LessThanOrEqualTo(sumOff / seeds * 1.15), "the second set costs the kit at most about a seventh more than the eleven");
        }

        /// <summary>
        /// The walk from her start to the exit room's middle (PassagesTests' measure, <see cref="PassagesTests.Walk"/>), over a
        /// hundred seeds of each floor, with the second set and without: at most 5 % longer (work order 072).
        /// </summary>
        [Test]
        public void TheWalk_WithTheSecondSet_IsAtMostFivePercentLonger()
        {
            MeasureWalk(100, out double on, out double off);
            Assert.That(on, Is.LessThanOrEqualTo(off * 1.05), "the walk at most 5 % longer");
        }

        /// <summary>The same over a thousand seeds a floor (run by name; about a minute on .NET).</summary>
        [Explicit, Test, Timeout(3600000)]
        public void Measure_TheWalk_AThousandSeedsAFloor() => MeasureWalk(1000, out _, out _);

        static void MeasureWalk(int seeds, out double allOn, out double allOff)
        {
            allOn = allOff = 0;
            var lines = new List<string>();
            for (int floor = 1; floor <= 5; floor++)
            {
                double on = 0, off = 0;
                for (ulong s = 0; s < (ulong)seeds; s++)
                {
                    on += PassagesTests.Walk(LevelGenerator.Generate(s, floor, On), out _);
                    off += PassagesTests.Walk(LevelGenerator.Generate(s, floor, SecondSetOff), out _);
                }
                on /= seeds;
                off /= seeds;
                allOn += on / 5;
                allOff += off / 5;
                lines.Add(string.Format(Inv, "floor {0}: {1:0.0} m with the second set, {2:0.0} m without ({3:+0.0%;-0.0%})", floor, on, off, on / off - 1));
            }
            TestContext.WriteLine(string.Format(Inv, "MOREWALK from her start to the exit room's middle, {0} seeds a floor: {1}; all floors {2:0.0} m and {3:0.0} m ({4:+0.0%;-0.0%})",
                seeds, string.Join("; ", lines), allOn, allOff, allOn / allOff - 1));
        }

        // ---------------- The switch off ----------------

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        /// <summary>
        /// Two thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the second set off, every field folded into
        /// one number (<see cref="FloorPrint"/>, each room's shape and solid cells, and its template), as PassagesTests folds version
        /// 15's. Unity's runtime folds its doubles to its own number, pinned from the first Unity run; on .NET the number from these
        /// floors and from version 16's generator (the code before work order 072, its defaults) is the same.
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloors_AreVersion16s_FieldForField()
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, Version16); // work order 087: the act's plan off too
                Assert.That(level.Rooms.All(r => r.Template == null || r.Template.Template.Since < RoomTemplateLibrary.MoreSince), "none of the second set, seed " + level.SeedText);
                h = FloorPrint.Of(level, h);
                foreach (LevelRoom r in level.Rooms)
                {
                    h = Mix(h, (ulong)r.Shape);
                    h = Mix(h, (ulong)r.SolidCount);
                    foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                    foreach (char c in r.Template?.ToString() ?? "-") h = Mix(h, c);
                }
            }
            // With the pin at 0 the test fails and says the number it found: the pin is taken from the first Unity run. Pinned
            // from the filtered Unity EditMode run of 2026-11-22 (work order 072); on .NET these floors fold to
            // 0xC1B46D643FD6BB04UL, as version 16's own code (fd83dfe) with its defaults does.
            const ulong pinned = 0x5852E0964A18277EUL;
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("MOREOFFPRINT " + found);
            if (pinned == 0UL) Assert.Fail("the pin is to be taken from the first Unity run: this run found " + found + " for the two thousand floors of version 16 (the second set off)");
            Assert.That(h, Is.EqualTo(pinned), "the two thousand floors of version 16, found " + found);
        }
    }
}
