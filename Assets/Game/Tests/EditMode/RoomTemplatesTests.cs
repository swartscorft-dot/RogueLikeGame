using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Rooms drawn by hand, joined at their doors (work order 058; Kristian, 2026-11-20: "we must take more inspiration from
    /// Diablo and Path of Exile 2 in how their generators make and design levels. We need a bit of a dungeon-crawler feeling",
    /// and on 2026-11-21 "Rom-maler slik Diablo og PoE gjør"; the research report d's proposal B). The library: every template
    /// reads, and in all eight orientations, for every draw of its ? cells and every pair of its door marks, keeps work order
    /// 051's rules with no filling and its marks open. A bad template is caught and never drawn. The turns. Over a thousand
    /// seeds of every floor number with the defaults: every floor valid, the template rooms on their marks, at their size, with
    /// their cells, no family twice in a row, no template twice a floor, the share of templated combat rooms, how often each
    /// fell back, the attempts and the time against the templates off, and how often each mark held what it is for. The themes
    /// follow the template. With the switch off the floors are version 14's, field for field. The text map. Writes what it
    /// measures ("TEMPLATESTATS", "TEMPLATEMARKS", "TEMPLATETHEMES", "TEMPLATECASES").
    /// </summary>
    public class RoomTemplatesTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly int[] Walls = { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West };

        // ---------------- The library ----------------

        [Test]
        public void TheLibrary_EveryTemplateReads_AndKeepsTheRules_InAllEightOrientations()
        {
            Assert.That(RoomTemplateLibrary.Errors, Is.Empty, "every grid reads");
            IReadOnlyList<RoomTemplate> all = RoomTemplateLibrary.All;
            // Work order 072: the first eleven (seven combat, two elite, two exit) and the second set's fifteen (eleven combat,
            // two elite, two exit), 26 in all; work order 089 (changed deliberately): and the third set's three exit rooms, 29.
            Assert.That(all.Count(t => t.Since < RoomTemplateLibrary.MoreSince), Is.EqualTo(11), "seven combat, two elite, two exit");
            Assert.That(all.Count, Is.EqualTo(29), "the eleven, work order 072's fifteen and work order 089's three");
            Assert.That(all.Count(t => t.For == RoomTemplateFor.Combat), Is.EqualTo(7 + 11));
            Assert.That(all.Count(t => t.For == RoomTemplateFor.Elite), Is.EqualTo(2 + 2));
            Assert.That(all.Count(t => t.For == RoomTemplateFor.Exit), Is.EqualTo(2 + 2 + 3));
            Assert.That(all.Select(t => t.Name).Distinct().Count(), Is.EqualTo(all.Count), "every name its own");
            var v = new LevelValues();
            int cases = 0;
            var lines = new List<string>();
            foreach (RoomTemplate t in all)
            {
                int ins = Count(t, 'I'), outs = t.For == RoomTemplateFor.Exit ? 1 : Count(t, 'O');
                int each = (1 << t.Drawn.Count) * ins * outs;
                int solid = Count(t, '#') + t.Drawn.Count;
                foreach (int wall in Walls)
                    foreach (bool mirror in new[] { false, true })
                    {
                        var faults = new List<string>();
                        Assert.That(RoomTemplates.Faults(t, wall, mirror, v, faults), Is.EqualTo(0), string.Join("\n", faults));
                        cases += each;
                    }
                lines.Add(string.Format(Inv, "{0} {1}x{2} ({3}), {4} cases an orientation, at most {5} of {6} cells solid before the gate", t.Name, t.Width, t.Height, t.For, each, solid, t.Width * t.Height));
            }
            // Work order 072: the second set only with its switch on (off by default since 2026-11-22).
            // Work order 089: the third set only with its own switch on (off by default).
            Assert.That(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = true, ExitRoomsOfTheirOwn = true }), Is.EquivalentTo(all), "the library is usable");
            Assert.That(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = true, ExitRoomsOfTheirOwn = false }), Is.EquivalentTo(all.Where(t => t.Since != RoomTemplateLibrary.ExitSince)), "the second set on, the third off"); // work order 103: the third set named off
            Assert.That(RoomTemplateLibrary.Usable(v), Is.EquivalentTo(all.Where(t => t.Since < RoomTemplateLibrary.MoreSince || ExitRooms.IsWayDown(t))), "the defaults: the eleven, and since work order 103 the third set");
            TestContext.WriteLine("TEMPLATECASES " + cases + " cases in all (eight orientations): " + string.Join("; ", lines));
        }

        static int Count(RoomTemplate t, char mark)
        {
            int n = 0;
            for (int ay = 0; ay < t.Height; ay++)
                for (int ax = 0; ax < t.Width; ax++)
                    if (t.At(ax, ay) == mark) n++;
            return n;
        }

        // A plain 7 x 7 room, door in at the bottom's middle and out at the top's: every rule kept.
        static readonly string[] Plain =
        {
            ". . . O . . .",
            ". . . . . . .",
            ". . . . . . .",
            ". . . . . . .",
            ". . . . . . .",
            ". . . . . . .",
            ". . . I . . .",
        };

        /// <summary>The plain grid with some of its cells (authored, ay = 0 the bottom row) changed.</summary>
        static string Grid(params (int ax, int ay, char ch)[] changes)
        {
            char[][] rows = Plain.Select(r => r.Replace(" ", "").ToCharArray()).ToArray();
            foreach ((int ax, int ay, char ch) in changes) rows[rows.Length - 1 - ay][ax] = ch;
            return string.Join("\n", rows.Select(r => string.Join(" ", r.Select(c => c.ToString()))));
        }

        static RoomTemplate Make(string name, string grid, float weight = 1f) =>
            RoomTemplate.Parse(name, name, RoomTemplateFor.Combat, 1, weight, new[] { RoomTheme.Archive }, false, grid);

        [Test]
        public void ABadTemplate_IsCaught_WithItsRuleNamed_AndNeverDrawn()
        {
            var v = new LevelValues();
            List<string> FaultsOf(RoomTemplate t)
            {
                var list = new List<string>();
                foreach (int wall in Walls)
                    foreach (bool mirror in new[] { false, true })
                        RoomTemplates.Faults(t, wall, mirror, v, list);
                return list;
            }
            RoomTemplate control = Make("control", Grid(), 50f);
            Assert.That(FaultsOf(control), Is.Empty, "the plain room keeps every rule");

            // A pocket walled off from the rest: the north-east corner cell behind two solid cells.
            RoomTemplate pocket = Make("pocket", Grid((5, 6, '#'), (6, 5, '#'), (5, 5, '#')));
            Assert.That(FaultsOf(pocket), Has.Some.Contains("more than one area"));
            // A door mark's straight-in cell solid: the cell behind the O.
            RoomTemplate blocked = Make("blocked", Grid((3, 5, '#')));
            Assert.That(FaultsOf(blocked), Has.Some.Contains("straight in behind door"));
            // A thin corner: two solid cells touching only at a corner.
            RoomTemplate thin = Make("thin", Grid((1, 1, '#'), (2, 2, '#')));
            Assert.That(FaultsOf(thin), Has.Some.Contains("touching only at the corner"));
            // A ? that opens a dead-end slot when drawn open: a cell at the west wall between two solid ones.
            RoomTemplate slot = Make("slot", Grid((0, 4, '#'), (0, 3, '?'), (0, 2, '#')));
            List<string> slotFaults = FaultsOf(slot);
            Assert.That(slotFaults, Has.Some.Contains("dead-end slot"));
            Assert.That(slotFaults.Where(f => f.Contains("dead-end slot")), Has.All.Contains("? drawn 0"), "only when the ? is drawn open");
            // A mark out of its place: an I on the top row; the grid is refused when it is read, naming the template and the cell.
            var e = Assert.Throws<FormatException>(() => Make("misplaced", Grid((1, 6, 'I'))));
            Assert.That(e.Message, Does.Contain("misplaced").And.Contain("(1,6)").And.Contain("bottom row"));
            Assert.Throws<FormatException>(() => Make("ragged", ". . O .\n. . .\n. I . ."));
            Assert.Throws<FormatException>(() => Make("unknown", Grid((2, 2, 'x'))));
            Assert.Throws<FormatException>(() => Make("no-out", Grid((3, 6, '.'))));
            Assert.Throws<FormatException>(() => Make("drawn", Grid((0, 1, '?'), (0, 3, '?'), (6, 1, '?'), (6, 3, '?'))));
            Assert.Throws<FormatException>(() => RoomTemplate.Parse("exit-out", "x", RoomTemplateFor.Exit, 1, 1f, null, true, Grid()));

            // Added to a copy of the library with a great weight, the bad ones are never drawn, the control is.
            var bad = new[] { pocket, blocked, thin, slot };
            RoomTemplateLibrary.Override = RoomTemplateLibrary.All.Concat(bad).Concat(new[] { control }).ToList();
            try
            {
                IReadOnlyList<RoomTemplate> usable = RoomTemplateLibrary.Usable(v);
                Assert.That(usable, Does.Contain(control));
                Assert.That(usable.Intersect(bad), Is.Empty, "a template that fails its check is not usable");
                int controls = 0;
                for (ulong s = 0; s < 100; s++)
                {
                    LevelLayout level = LevelGenerator.Generate(s, 1 + (int)(s % 5), v);
                    Assert.That(level.Rooms.Where(r => r.Template != null).Select(r => r.Template.Template).Intersect(bad), Is.Empty, "seed " + s);
                    controls += level.Rooms.Count(r => r.Template?.Template == control);
                }
                Assert.That(controls, Is.GreaterThan(50), "the control, weighted so, is drawn");
            }
            finally
            {
                RoomTemplateLibrary.Override = null;
            }
        }

        [Test]
        public void TheValidator_ReportsATemplateRoomsFaults()
        {
            var v = new LevelValues();
            void Expect(Action<LevelLayout, LevelRoom> breakIt, string expected)
            {
                LevelLayout level = LevelGenerator.Generate(42UL, 1, v);
                Assume.That(LevelValidator.Validate(level, v), Is.Empty);
                LevelRoom room = level.Rooms.First(r => r.Shape == RoomShape.Template && r.Kind == RoomKind.Combat && !r.Elite);
                breakIt(level, room);
                List<string> faults = LevelValidator.Validate(level, v);
                Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  " + string.Join("\n  ", faults));
            }
            // One of its # cells open.
            Expect((l, r) =>
            {
                for (int x = r.Rect.X; x < r.Rect.Right; x++)
                    for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                        if (RoomTemplates.MarkAt(r, x, y) == '#')
                        {
                            r.SetSolid(x, y, false);
                            return;
                        }
            }, "is a # of its template and open");
            // A solid cell that is no #, ? or gate cell of its template.
            Expect((l, r) =>
            {
                for (int x = r.Rect.X; x < r.Rect.Right; x++)
                    for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                        if (RoomTemplates.MarkAt(r, x, y) == '.' && r.IsOpen(x, y))
                        {
                            r.SetSolid(x, y, true);
                            return;
                        }
            }, "not its gate's");
            // Another size than its template's.
            Expect((l, r) => r.Rect = new CellRect(r.Rect.X, r.Rect.Y, r.Rect.Width, r.Rect.Height + 1), "its template turned");
            // Its template for another kind of room.
            Expect((l, r) => r.Template = l.ExitRoom.Template, "and its template is for a Exit room");
            // The same template, so the same family, in the next combat room of the row; and twice on the floor.
            Expect((l, r) =>
            {
                List<LevelRoom> chain = LevelGeometry.MainPath(l).Where(x => x.Kind == RoomKind.Combat).ToList();
                int at = chain.IndexOf(r);
                LevelRoom next = at + 1 < chain.Count ? chain[at + 1] : chain[at - 1];
                next.Template = r.Template;
            }, "takes the template of");
        }

        // ---------------- Turning ----------------

        /// <summary>A placement's cells as rows, north at the top.</summary>
        static string[] Cells(TemplatePlacement o)
        {
            var rows = new string[o.Height];
            for (int ly = 0; ly < o.Height; ly++)
            {
                var row = new char[o.Width];
                for (int lx = 0; lx < o.Width; lx++) row[lx] = o.MarkAt(lx, ly);
                rows[o.Height - 1 - ly] = new string(row);
            }
            return rows;
        }

        /// <summary>A quarter turn clockwise of rows (north at the top): the west column becomes the north row.</summary>
        static string[] Clockwise(string[] rows)
        {
            int h = rows.Length, w = rows[0].Length;
            var result = new string[w];
            for (int r = 0; r < w; r++)
            {
                var row = new char[h];
                for (int c = 0; c < h; c++) row[c] = rows[h - 1 - c][r];
                result[r] = new string(row);
            }
            return result;
        }

        static string[] Mirrored(string[] rows) => rows.Select(r => new string(r.Reverse().ToArray())).ToArray();

        [Test]
        public void Turning_FourQuarterTurnsAndTwoMirrors_GiveTheTemplateBack_AndTheDoorInIsOnItsWall()
        {
            foreach (RoomTemplate t in RoomTemplateLibrary.All)
            {
                string[] drawn = Cells(t.Oriented(LevelGenerator.South, false));
                Assert.That(Clockwise(Clockwise(Clockwise(Clockwise(drawn)))), Is.EqualTo(drawn), t.Name + ": four quarter turns");
                Assert.That(Mirrored(Mirrored(drawn)), Is.EqualTo(drawn), t.Name + ": two mirrors");
                Assert.That(Cells(t.Oriented(LevelGenerator.West, false)), Is.EqualTo(Clockwise(drawn)), t.Name + ": in from the west, a quarter turn clockwise");
                Assert.That(Cells(t.Oriented(LevelGenerator.North, false)), Is.EqualTo(Clockwise(Clockwise(drawn))), t.Name + ": in from the north, a half turn");
                Assert.That(Cells(t.Oriented(LevelGenerator.East, false)), Is.EqualTo(Clockwise(Clockwise(Clockwise(drawn)))), t.Name + ": in from the east");
                Assert.That(Cells(t.Oriented(LevelGenerator.South, true)), Is.EqualTo(Mirrored(drawn)), t.Name + ": mirrored");
                foreach (int wall in Walls)
                    foreach (bool mirror in new[] { false, true })
                    {
                        TemplatePlacement o = t.Oriented(wall, mirror);
                        bool across = wall == LevelGenerator.East || wall == LevelGenerator.West;
                        Assert.That((o.Width, o.Height), Is.EqualTo(across ? (t.Height, t.Width) : (t.Width, t.Height)), t.Name + " in from " + wall);
                        for (int lx = 0; lx < o.Width; lx++)
                            for (int ly = 0; ly < o.Height; ly++)
                            {
                                o.Authored(lx, ly, out int ax, out int ay);
                                o.Local(ax, ay, out int bx, out int by);
                                Assert.That((bx, by), Is.EqualTo((lx, ly)), "the two ways round agree");
                                if (o.MarkAt(lx, ly) != 'I') continue;
                                bool onWall = wall == LevelGenerator.South ? ly == 0 : wall == LevelGenerator.North ? ly == o.Height - 1
                                            : wall == LevelGenerator.West ? lx == 0 : lx == o.Width - 1;
                                Assert.That(onWall, t.Name + ": its I on the wall of the door in, " + wall + (mirror ? " mirrored" : ""));
                            }
                    }
            }
        }

        // ---------------- A thousand seeds a floor ----------------

        static double Percentile(List<int> sorted, double p) => sorted[Math.Min(sorted.Count - 1, (int)Math.Ceiling(p * sorted.Count) - 1)];

        static string Attempts(List<int> a)
        {
            a.Sort();
            return string.Format(Inv, "median {0}, 95th {1}, 99th {2}, most {3}", Percentile(a, 0.5), Percentile(a, 0.95), Percentile(a, 0.99), a[a.Count - 1]);
        }

        static bool IsShooter(EnemyGroup g) => !g.Elite && !g.Boss && (g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster);

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_TheTemplateRooms_KeepTheirRules_TheirMarksAndTheirShare(int floor)
        {
            var on = new LevelValues { FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false }; // work order 104 (changed deliberately): 058's share, with the first set on every floor; with the floors' character floor 1 has three combat templates and its share is lower
            var off = new LevelValues { Templates = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false };
            TemplateValues tv = on.TemplateRules;
            PropValues pv = on.PropRules;
            const int seeds = 1000; // a thousand, not the order's two thousand: the EditMode suite is long already (work order 058's report)
            var share = new Dictionary<string, int>();
            var stampBack = new Dictionary<string, int>();
            var plainerBack = new Dictionary<string, int>();
            var attemptsOn = new List<int>();
            var attemptsOff = new List<int>();
            var timeOn = new List<double>();
            var timeOff = new List<double>();
            var timer = new Stopwatch();
            int ordinary = 0, ordinaryTemplated = 0, templatedRooms = 0, stamped = 0;
            // The marks: rooms and cells that held what they are for.
            int hRooms = 0, hRoomsHeld = 0, hCells = 0, hCellsHeld = 0;
            int eRooms = 0, eHeld = 0;
            int aRooms = 0, aHeld = 0, aLargeRooms = 0, aLargeHeld = 0, aRefusedByEnemy = 0, aOffRooms = 0, aOffHeld = 0;
            int cCells = 0, cHeld = 0;
            // The themes: rooms of each template whose theme it suits, and rooms without a template whose theme that template suits.
            var suited = new Dictionary<RoomTemplate, (int rooms, int suits)>();
            var withoutTemplate = new List<RoomTheme>();
            LevelGenerator.Generate(424242UL, floor, on);
            LevelGenerator.Generate(424242UL, floor, off);
            for (ulong s = 0; s < seeds; s++)
            {
                // Every fourth seed with the templates off too, in turns, so a busy moment of the machine falls on both alike (the
                // generator's own validator has passed every floor it gives; it is asked again here every tenth).
                bool both = s % 4 == 0;
                if (both)
                {
                    timer.Restart();
                    LevelLayout plain = LevelGenerator.Generate(s, floor, off);
                    timer.Stop();
                    timeOff.Add(timer.Elapsed.TotalMilliseconds);
                    attemptsOff.Add(plain.Attempt);
                    Assert.That(plain.Rooms.All(r => r.Template == null), "seed " + s);
                }
                timer.Restart();
                LevelLayout level = LevelGenerator.Generate(s, floor, on);
                timer.Stop();
                if (both) timeOn.Add(timer.Elapsed.TotalMilliseconds);
                attemptsOn.Add(level.Attempt);
                string where = "seed " + s + " floor " + floor;
                Assert.That(level.Templates, Is.True, where);
                if (s % 10 == 0) Assert.That(LevelValidator.Validate(level, on), Is.Empty, where + "\n" + LevelMap.Draw(level));
                double cs = level.CellSize;

                var names = new HashSet<string>();
                string familyBefore = null;
                RoomMoodPlan mood = null;
                foreach (LevelRoom room in LevelGeometry.MainPath(level))
                {
                    string name = room.Letter + room.Order.ToString(Inv) + " " + where;
                    TemplatePlacement o = room.Template;
                    if (room.Kind == RoomKind.Combat && !room.Elite)
                    {
                        ordinary++;
                        if (o != null) ordinaryTemplated++;
                    }
                    if (room.Elite || room.Kind == RoomKind.Exit) Assert.That(o, Is.Not.Null, "the elite and the exit room take a template: " + name);
                    if (room.Kind == RoomKind.Entrance || room.Kind == RoomKind.Treasure) Assert.That(o, Is.Null, name);
                    if (room.Kind == RoomKind.Combat)
                    {
                        if (o != null) Assert.That(o.Template.Family, Is.Not.EqualTo(familyBefore), "no family twice in a row: " + name);
                        familyBefore = o?.Template.Family;
                        if (o == null && !room.Elite) withoutTemplate.Add(room.Theme);
                    }
                    if (o == null) continue;
                    templatedRooms++;
                    string key = o.Template.Name;
                    share[key] = share.TryGetValue(key, out int n) ? n + 1 : 1;
                    Assert.That(names.Add(key), "no template twice a floor: " + name);
                    Assert.That((room.Rect.Width, room.Rect.Height), Is.EqualTo((o.Width, o.Height)), "its template's size: " + name);
                    if (room.TemplateFellBack) stampBack[key] = stampBack.TryGetValue(key, out int b) ? b + 1 : 1;
                    else if (room.Shape != RoomShape.Template) plainerBack[key] = plainerBack.TryGetValue(key, out int p) ? p + 1 : 1;
                    LevelDoor wayIn = LevelGeometry.EntryDoor(level, room), wayOut = LevelGeometry.ExitDoor(level, room);
                    foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in wayIn.CellPairs())
                    {
                        (int x, int y) c = room.Rect.Contains(pair.Key.x, pair.Key.y) ? pair.Key : pair.Value;
                        Assert.That(RoomTemplates.MarkAt(room, c.x, c.y), Is.EqualTo('I'), "the door in on an I: " + name);
                    }
                    if (wayOut != null)
                        foreach (KeyValuePair<(int x, int y), (int x, int y)> pair in wayOut.CellPairs())
                        {
                            (int x, int y) c = room.Rect.Contains(pair.Key.x, pair.Key.y) ? pair.Key : pair.Value;
                            Assert.That(RoomTemplates.MarkAt(room, c.x, c.y), Is.EqualTo('O'), "the door out on an O: " + name);
                        }
                    if (room.Kind == RoomKind.Combat && room.Theme != RoomTheme.None)
                    {
                        suited.TryGetValue(o.Template, out (int rooms, int suits) had);
                        suited[o.Template] = (had.rooms + 1, had.suits + (o.Template.SuitsTheme(room.Theme) ? 1 : 0));
                    }
                    if (room.Shape != RoomShape.Template) continue;
                    stamped++;
                    HashSet<(int, int)> gate = new HashSet<(int, int)>(RoomShapes.GateCells(level, room));
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                        {
                            char m = RoomTemplates.MarkAt(room, x, y);
                            if (m == '#') Assert.That(room.IsSolid(x, y), "its # solid: " + name);
                            else if (room.IsSolid(x, y)) Assert.That(m == '?' || gate.Contains((x, y)), "its solid cells its template's: " + name);
                        }

                    // The marks.
                    char At(LevelPoint q) => RoomTemplates.MarkAt(room, q, cs);
                    int hHere = 0;
                    for (int x = room.Rect.X; x < room.Rect.Right; x++)
                        for (int y = room.Rect.Y; y < room.Rect.Top; y++)
                        {
                            char m = RoomTemplates.MarkAt(room, x, y);
                            if (m == 'h') hHere++;
                            if (m == 'c' && room.IsOpen(x, y))
                            {
                                cCells++;
                                if (room.Props.Any(q => (int)Math.Floor(q.Position.X / cs) == x && (int)Math.Floor(q.Position.Y / cs) == y && q.Place == PropPlace.Floor)) cHeld++;
                            }
                        }
                    List<int> shooters = Enumerable.Range(0, room.EnemyPoints.Count).Where(k => IsShooter(room.GroupAt(k))).ToList();
                    if (hHere > 0 && shooters.Count > 0)
                    {
                        hRooms++;
                        hCells += hHere;
                        var held = new HashSet<(int, int)>();
                        foreach (int k in shooters)
                            if (At(room.EnemyPoints[k]) == 'h') held.Add(((int)Math.Floor(room.EnemyPoints[k].X / cs), (int)Math.Floor(room.EnemyPoints[k].Y / cs)));
                        hCellsHeld += held.Count;
                        if (held.Count > 0) hRoomsHeld++;
                    }
                    int elite = -1;
                    for (int k = 0; k < room.EnemyPoints.Count; k++)
                        if (room.GroupAt(k).Elite) elite = k;
                    if (elite >= 0 && Enumerable.Range(0, o.Width * o.Height).Any(k => o.MarkAt(k % o.Width, k / o.Width) == 'e'))
                    {
                        eRooms++;
                        if (At(room.EnemyPoints[elite]) == 'e') eHeld++;
                    }
                    if (RoomTemplates.FarMark(room, cs, out int far, out double along))
                    {
                        LevelProp piece = room.Props.FirstOrDefault(q => q.Model == PropModel.Altar || q.Model == PropModel.BoneThrone);
                        // Work order 082: what the altar, the throne's neighbours and the mood's large-prop place are asked for is the
                        // middle of the A cells on the far wall (FarMarkAlong; null would send them to the wall's middle).
                        Assert.That(RoomTemplates.FarMarkAlong(room, far, cs), Is.EqualTo(along).Within(1e-9), "the A's place along its far wall: " + name);
                        // An A away from its wall's middle, where the altar's place from the mark differs from the wall's middle it
                        // falls back to. On 2026-11-22 there was none (9642 rooms; 8767 with work order 072's second set): every template's A is at its wall's middle, so
                        // the sweep cannot tell the mark from the fallback (said in work order 082's report).
                        double wallMiddle = far == LevelGenerator.North || far == LevelGenerator.South ? (room.Rect.X + room.Rect.Right) * 0.5 * cs : (room.Rect.Y + room.Rect.Top) * 0.5 * cs;
                        bool offMiddle = Math.Abs(along - wallMiddle) > cs;
                        if (piece != null)
                        {
                            aRooms++;
                            if (offMiddle) aOffRooms++;
                            if (At(piece.Position) == 'A' && offMiddle) aOffHeld++;
                            if (At(piece.Position) == 'A') aHeld++;
                            else
                            {
                                // Was the A's own place refused for an enemy standing there (a caster or a shooter at the far wall)?
                                PropSize size = PropSize.Of(piece.Model);
                                double depth = piece.Model == PropModel.Altar
                                    ? pv.AtWallGap + PropSize.Of(PropModel.Statue).Depth + on.ThemeRules.AltarStatueGap + size.Depth * 0.5
                                    : pv.WallFace + pv.WallPieceGap + size.Depth * 0.5;
                                LevelPoint into = PropRules.Inward(far);
                                double x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
                                var wanted = far == LevelGenerator.North ? new LevelPoint(along, y1 - depth) : far == LevelGenerator.South ? new LevelPoint(along, y0 + depth)
                                           : far == LevelGenerator.East ? new LevelPoint(x1 - depth, along) : new LevelPoint(x0 + depth, along);
                                var why = new List<string>();
                                PropRules.Fault(level, room, LevelProp.Make(piece.Model, wanted, (PropRules.Facing(into.X, into.Y) + size.FrontTurn) % 360,
                                                                            piece.Model == PropModel.Altar ? PropPlace.Set : PropPlace.Wall),
                                                room.Props.Where(q => q != piece).ToList(), pv, why);
                                if (why.Any(f => f.Contains("enemy point"))) aRefusedByEnemy++;
                            }
                        }
                        // The mood's large-prop place, where no altar holds the A (every fourth seed: the mood is a plan of its own).
                        bool altarOnA = piece != null && At(piece.Position) == 'A';
                        if (both && room.Theme != RoomTheme.ThroneRoom && !altarOnA)
                        {
                            mood = mood ?? RoomMoodPlan.Make(level, on);
                            MoodRoom mr = mood.Rooms.First(m => m.Room == room); // work order 059: a passage has no plan, so the plan is not by order
                            aLargeRooms++;
                            if (mr.LargePropPlace.HasValue && At(mr.LargePropPlace.Value) == 'A') aLargeHeld++;
                        }
                    }
                }
            }
            string Shares(Dictionary<string, int> d) => string.Join(", ", RoomTemplateLibrary.All.Where(t => d.ContainsKey(t.Name)).Select(t => t.Name + " " + d[t.Name]));
            double combatShare = ordinaryTemplated / (double)ordinary;
            timeOn.Sort();
            timeOff.Sort();
            double mOn = timeOn[timeOn.Count / 2], mOff = timeOff[timeOff.Count / 2];
            TestContext.WriteLine(string.Format(Inv,
                "TEMPLATESTATS floor {0} seeds 0-{1}: templated rooms {2} ({3} stamped); ordinary combat rooms templated {4} of {5} ({6:P1}); " +
                "by template: {7}; fell back at the stamp: {8}; by PlaceAgainPlainer: {9}; attempts with templates {10}, without {11}; " +
                "median generation {12:0.000} ms with, {13:0.000} ms without ({14:0.00} times)",
                floor, seeds - 1, templatedRooms, stamped, ordinaryTemplated, ordinary, combatShare, Shares(share), Shares(stampBack), Shares(plainerBack),
                Attempts(new List<int>(attemptsOn)), Attempts(new List<int>(attemptsOff)), mOn, mOff, mOn / mOff));
            TestContext.WriteLine(string.Format(Inv,
                "TEMPLATEMARKS floor {0}: h a shooter or caster in {1} of {2} rooms with both ({3} of {4} h cells); e the elite {5} of {6}; " +
                "A the altar or throne {7} of {8} (of the others, the A's own place refused for an enemy point {9}), A the mood's large-prop place {10} of {11} where no altar holds it (every fourth seed); c a floor prop {12} of {13} c cells",
                floor, hRoomsHeld, hRooms, hCellsHeld, hCells, eHeld, eRooms, aHeld, aRooms, aRefusedByEnemy, aLargeHeld, aLargeRooms, cHeld, cCells));
            TestContext.WriteLine(string.Format(Inv, "TEMPLATEMARKS floor {0}: an A more than a cell off its wall's middle in {1} of the {2} altar or throne rooms, held there {3}", floor, aOffRooms, aRooms, aOffHeld));
            var themeLines = new List<string>();
            foreach (KeyValuePair<RoomTemplate, (int rooms, int suits)> pair in suited.OrderBy(p => p.Key.Name))
            {
                double inTemplate = pair.Value.suits / (double)pair.Value.rooms;
                double elsewhere = withoutTemplate.Count(th => pair.Key.SuitsTheme(th)) / (double)Math.Max(1, withoutTemplate.Count);
                themeLines.Add(string.Format(Inv, "{0} {1:P0} of {2} rooms, {3:P0} without a template", pair.Key.Name, inTemplate, pair.Value.rooms, elsewhere));
                if (pair.Value.rooms >= 50 && withoutTemplate.Count >= 50)
                    Assert.That(inTemplate, Is.GreaterThan(elsewhere), "a template's suited themes are more common in its rooms: " + pair.Key.Name);
            }
            TestContext.WriteLine("TEMPLATETHEMES floor " + floor + ": " + string.Join("; ", themeLines));

            Assert.That(Math.Abs(combatShare - tv.CombatShare), Is.LessThanOrEqualTo(0.05), "the share of templated ordinary combat rooms");
            foreach (string t in share.Keys)
            {
                int back = (stampBack.TryGetValue(t, out int a) ? a : 0) + (plainerBack.TryGetValue(t, out int b) ? b : 0);
                Assert.That(back / (double)share[t], Is.LessThan(0.02), t + " falls back in fewer than 2 rooms of 100");
            }
            attemptsOn.Sort();
            Assert.That(Percentile(attemptsOn, 0.99), Is.LessThanOrEqualTo(8), "the 99th percentile of attempts");
            Assert.That(mOn, Is.LessThanOrEqualTo(mOff * 2), "the median generation at most twice that without the templates");
            // Work order 082 (review 6, finding 3): every mark asserted, not only counted. Each bound is the share seen on .NET on
            // 2026-11-22 (floors 1-5) less a margin: h 100 % of the rooms (57-99.8 % of the cells), e 100 %, A 88-93 % held
            // and 97-98 % held or refused for an enemy point, the mood's large-prop place 53-59 %, c 52-59 %.
            Assert.That(eRooms, Is.GreaterThan(100), "elite rooms with an e mark");
            Assert.That(eHeld / (double)eRooms, Is.GreaterThanOrEqualTo(0.9), "the e mark holds the elite");
            Assert.That(hRooms, Is.GreaterThan(100), "rooms with an h mark and a shooter or caster");
            Assert.That(hRoomsHeld / (double)hRooms, Is.GreaterThanOrEqualTo(0.95), "the h marks hold a shooter or caster: " + hRoomsHeld + " of " + hRooms + " rooms");
            Assert.That(hCellsHeld / (double)hCells, Is.GreaterThanOrEqualTo(0.45), "the h cells held: " + hCellsHeld + " of " + hCells);
            Assert.That(cCells, Is.GreaterThan(100), "open c cells");
            Assert.That(cHeld / (double)cCells, Is.GreaterThanOrEqualTo(0.45), "the c marks hold a floor prop (cover placed first): " + cHeld + " of " + cCells);
            Assert.That(aRooms, Is.GreaterThan(100), "rooms with an A mark and an altar or throne");
            Assert.That(aHeld / (double)aRooms, Is.GreaterThanOrEqualTo(0.8), "the A mark holds the altar or throne: " + aHeld + " of " + aRooms);
            Assert.That((aHeld + aRefusedByEnemy) / (double)aRooms, Is.GreaterThanOrEqualTo(0.93), "held, or refused only for an enemy standing there");
            Assert.That(aLargeRooms, Is.GreaterThan(50), "rooms whose A is left to the mood's large-prop place");
            Assert.That(aLargeHeld / (double)aLargeRooms, Is.GreaterThanOrEqualTo(0.45), "the mood's large-prop place on the A: " + aLargeHeld + " of " + aLargeRooms);
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
        /// Two thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the templates off, every field folded into
        /// one number (<see cref="FloorPrint"/>, and each room's shape and solid cells). On .NET 9 the number from these floors
        /// with the templates off and from version 14's generator (the code before work order 058, with its defaults) was the
        /// same, 0x156E15D493126A5A; Unity's runtime folds its doubles to the number pinned here.
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloors_AreVersion14s_FieldForField()
        {
            var off = new LevelValues { Templates = false, Passages = false, MoreTemplates = false, ActPlan = false, UsableRooms = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false }; // work order 103: nor exit rooms of their own; 059: version 14 had no passages; 072: nor the second set; 087: nor the act's plan
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, off);
                Assert.That(level.Templates, Is.False);
                Assert.That(level.Rooms.All(r => r.Template == null && r.Shape != RoomShape.Template), "no template, seed " + level.SeedText);
                Assert.That(level.Describe(), Does.Not.Contain("template"));
                h = FloorPrint.Of(level, h);
                foreach (LevelRoom r in level.Rooms)
                {
                    h = Mix(h, (ulong)r.Shape);
                    h = Mix(h, (ulong)r.SolidCount);
                    foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                }
            }
            // Pinned from the first Unity run (EditMode, filtered, on runner/058-room-templates, 2026-11-21). With the pin at 0
            // the test fails and says the number it found.
            const ulong pinned = 0xC50934C7B2490031UL;
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("TEMPLATESOFFPRINT " + found);
            if (pinned == 0UL) Assert.Fail("the pin is to be taken from the first Unity run: this run found " + found + " for the two thousand floors of version 14 (the templates off)");
            Assert.That(h, Is.EqualTo(pinned), "the two thousand floors of version 14, found " + found);
        }

        // ---------------- The text map ----------------

        [Test]
        public void TheTextMap_ShowsTheTemplatesLegend_AndAStampedTemplatesOutline()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 1);
            LevelRoom room = level.Rooms.First(r => r.Shape == RoomShape.Template && r.SolidCount > 0);
            string map = LevelMap.Draw(level);
            string[] lines = map.Split('\n');
            string label = room.Letter + room.Order.ToString(Inv);
            Assert.That(lines[0], Does.Contain("  templates ").And.Contain(label + " " + room.Template.Template.Name), map);
            Assert.That(level.Describe(), Does.Contain(" template " + room.Template.Template.Name + " "));
            Assert.That(level.Describe().Split('\n')[0], Does.Contain(" templated"));
            CellRect box = level.Bounds();
            int rows = 2 * box.Height + 1;
            char At(int gx, int gy) { string l = lines[1 + rows - 1 - gy]; return gx < l.Length ? l[gx] : ' '; }
            int walled = 0;
            foreach ((int x, int y) in room.SolidList)
            {
                int cx = 2 * (x - box.X) + 1, cy = 2 * (y - box.Y) + 1;
                Assert.That(At(cx, cy), Is.EqualTo(' '), "a solid cell blank inside, " + (x, y) + "\n" + map);
                foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                {
                    if (!room.IsOpen(x + dx, y + dy)) continue;
                    Assert.That(At(cx + dx, cy + dy), Is.EqualTo(dx != 0 ? '|' : '-'), "a wall between solid cell " + (x, y) + " and the open cell beside it\n" + map);
                    walled++;
                }
            }
            Assert.That(walled, Is.GreaterThan(0));
            TestContext.WriteLine("TEMPLATEMAP\n" + map);
        }
    }
}
