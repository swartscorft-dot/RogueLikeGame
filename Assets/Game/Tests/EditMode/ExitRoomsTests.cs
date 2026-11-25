using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Exit rooms of their own (work order 089; Kristian, 2026-11-22: "Ja, tronen bare på etasje 5"): the library's third set (the
    /// gate hall, the stair head, the sealed door) drawn as text and held to every rule in every orientation; the way down's line
    /// from the door in to the far wall's A, with no solid cell on it and no tall prop near it; the switch
    /// (<see cref="LevelValues.ExitRoomsOfTheirOwn"/>) off by default and off every floor version 19's, field for field; on, floors 1
    /// to 4 end in a room of the third set, an exit hall, and floor 5 in a throne room with the act boss, every floor made and every
    /// room joined; the room lab lists the three under their names with the exit mixes of floors 1 to 4. The table of the report
    /// is the explicit measure at the end.
    /// </summary>
    public class ExitRoomsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static LevelValues On => new LevelValues { ExitRoomsOfTheirOwn = true, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 104: the floors of version 20
        static LevelValues Off => new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }; // work order 103 (changed deliberately): on by default since; off named
        static readonly string[] Names = { "gate-hall", "stair-head", "sealed-door" };

        static List<RoomTemplate> Third => RoomTemplateLibrary.All.Where(t => t.Since == RoomTemplateLibrary.ExitSince).ToList();
        static List<RoomTemplate> Thrones => RoomTemplateLibrary.All.Where(ExitRooms.IsThrone).ToList();

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // ---------------- The third set as drawn ----------------

        [Test]
        public void TheThirdSet_IsTheGateHall_TheStairHead_AndTheSealedDoor_ExitRoomsWithNoThrone()
        {
            Assert.That(RoomTemplateLibrary.Errors, Is.Empty);
            Assert.That(Third.Select(t => t.Name), Is.EqualTo(Names), "the three, in the library's order");
            Assert.That(RoomTemplateLibrary.All.Skip(RoomTemplateLibrary.All.Count - Third.Count), Is.EqualTo(Third), "after both other sets");
            foreach (RoomTemplate t in Third)
            {
                TestContext.WriteLine("EXITROOM " + t.Name + " " + t.Width * 4 + " x " + t.Height * 4 + " m" + t.Grid.Replace("                ", "    "));
                Assert.That(t.For, Is.EqualTo(RoomTemplateFor.Exit), t.Name);
                Assert.That(ExitRooms.IsWayDown(t) && !ExitRooms.IsThrone(t), Is.True, t.Name);
                Assert.That(t.Family, Is.Not.EqualTo(RoomTemplateLibrary.ThroneFamily));
                Assert.That(t.FromFloor, Is.EqualTo(1));
                Assert.That(t.Weight, Is.EqualTo(1f));
                Assert.That(t.FarNotSouth, Is.True, t.Name + ": the way down's far wall never the low south one");
                Assert.That(t.Suits, Is.EqualTo(new[] { RoomTheme.ExitHall }));
                Assert.That(t.Width, Is.InRange(8, 10), t.Name + ": 32 to 40 m, the exit room's sizes");
                Assert.That(t.Height, Is.InRange(8, 10), t.Name);
                Assert.That(t.Drawn, Is.Empty, t.Name + ": no drawn cells, so the way down's line holds in every room");
                int ins = 0, firstI = -1, lastI = -1, a = 0;
                for (int ay = 0; ay < t.Height; ay++)
                    for (int ax = 0; ax < t.Width; ax++)
                    {
                        char m = t.At(ax, ay);
                        Assert.That(m, Is.Not.EqualTo('O').And.Not.EqualTo('e'), t.Name + ": no way out, no elite mark");
                        if (m == 'I') { ins++; if (firstI < 0) firstI = ax; lastI = ax; }
                        if (m == 'A') a++;
                    }
                Assert.That(ins, Is.GreaterThan(0));
                Assert.That(lastI - firstI, Is.LessThanOrEqualTo(2), t.Name + ": its way in on one stretch of the near wall");
                Assert.That(a, Is.InRange(1, 2), t.Name + ": the way down's place");
                Assert.That(LargestBlock(t), Is.LessThanOrEqualTo(2), t.Name + ": no solid block more than two cells across (the camera, work order 072)");
            }
        }

        /// <summary>The most cells across, either way, of any block of solid cells joined side to side (as work order 072's test measures it).</summary>
        static int LargestBlock(RoomTemplate t)
        {
            var seen = new bool[t.Width * t.Height];
            int most = 0;
            for (int k = 0; k < seen.Length; k++)
            {
                if (seen[k] || t.At(k % t.Width, k / t.Width) != '#') continue;
                int x0 = k % t.Width, x1 = x0, y0 = k / t.Width, y1 = y0;
                var queue = new Queue<int>();
                seen[k] = true;
                queue.Enqueue(k);
                while (queue.Count > 0)
                {
                    int c = queue.Dequeue(), cx = c % t.Width, cy = c / t.Width;
                    x0 = Math.Min(x0, cx); x1 = Math.Max(x1, cx); y0 = Math.Min(y0, cy); y1 = Math.Max(y1, cy);
                    foreach ((int dx, int dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        int nx = cx + dx, ny = cy + dy, n = ny * t.Width + nx;
                        if (nx < 0 || ny < 0 || nx >= t.Width || ny >= t.Height || seen[n] || t.At(nx, ny) != '#') continue;
                        seen[n] = true;
                        queue.Enqueue(n);
                    }
                }
                most = Math.Max(most, Math.Max(x1 - x0 + 1, y1 - y0 + 1));
            }
            return most;
        }

        [Test]
        public void EveryOne_KeepsEveryRuleOfATemplate_InEveryOrientation_AndIsUsableWithTheSwitchOn()
        {
            var faults = new List<string>();
            foreach (RoomTemplate t in Third)
                foreach (int wall in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                    foreach (bool mirror in new[] { false, true })
                        RoomTemplates.Faults(t, wall, mirror, On, faults);
            Assert.That(faults, Is.Empty);
            Assert.That(RoomTemplateLibrary.Usable(On).Where(t => t.Since == RoomTemplateLibrary.ExitSince), Is.EqualTo(Third), "usable with the switch on");
        }

        /// <summary>
        /// The way down reads at a glance (point 2): in every orientation and from every place the door in may open, the line from the
        /// door's middle to the middle of the A cells crosses no solid cell, runs along the room's long line (the A straight across, at
        /// most half a cell to the side of the door's middle), and the room's open middle square lies on it.
        /// </summary>
        [Test]
        public void TheWayDown_IsAcrossFromTheDoor_OnTheLongLine_WithNoSolidCellBetween()
        {
            foreach (RoomTemplate t in Third)
                foreach (int wall in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West })
                    foreach (bool mirror in new[] { false, true })
                    {
                        TemplatePlacement o = t.Oriented(wall, mirror);
                        double sx = 0, sy = 0;
                        int n = 0;
                        for (int y = 0; y < o.Height; y++)
                            for (int x = 0; x < o.Width; x++)
                                if (o.MarkAt(x, y) == 'A') { sx += x + 0.5; sy += y + 0.5; n++; }
                        var to = new LevelPoint(sx / n, sy / n);
                        for (int along = 0; along < (wall == LevelGenerator.North || wall == LevelGenerator.South ? o.Width : o.Height); along++)
                        {
                            int lx = wall == LevelGenerator.East ? o.Width - 1 : wall == LevelGenerator.West ? 0 : along;
                            int ly = wall == LevelGenerator.North ? o.Height - 1 : wall == LevelGenerator.South ? 0 : along;
                            if (o.MarkAt(lx, ly) != 'I') continue;
                            var from = new LevelPoint(wall == LevelGenerator.East ? o.Width : wall == LevelGenerator.West ? 0 : lx + 0.5,
                                                      wall == LevelGenerator.North ? o.Height : wall == LevelGenerator.South ? 0 : ly + 0.5);
                            string where = t.Name + " in " + RoomTemplates.WallWord(wall) + (mirror ? " mirrored" : "") + " door at " + along;
                            bool acrossX = wall == LevelGenerator.North || wall == LevelGenerator.South;
                            Assert.That(Math.Abs(acrossX ? to.X - from.X : to.Y - from.Y), Is.LessThanOrEqualTo(1.5 + 1e-9), where + ": the A straight across, on the long line");
                            double length = from.DistanceTo(to);
                            for (double k = 0; k <= length; k += 0.05)
                            {
                                double px = from.X + (to.X - from.X) * k / length, py = from.Y + (to.Y - from.Y) * k / length;
                                char m = o.MarkAt(Math.Min(o.Width - 1, (int)Math.Floor(px)), Math.Min(o.Height - 1, (int)Math.Floor(py)));
                                Assert.That(m, Is.Not.EqualTo('#'), where + ": a solid cell on the way down's line at " + px.ToString("0.0", Inv) + ", " + py.ToString("0.0", Inv));
                            }
                        }
                    }
        }

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOffByDefault_TheFloorsUsableListLeavesTheThirdSetOut_WhateverTheSecondSetSays()
        {
            // Work order 103 (changed deliberately): on by default since, with the generator's version 20.
            Assert.That(new LevelValues().ExitRoomsOfTheirOwn, Is.True, "on by default since work order 103");
            Assert.That(LevelGenerator.Version, Is.EqualTo(24), "work order 103's version, 20; 104's, 21; 118's, 22; 124's, 23; 130's, 24");
            foreach (bool more in new[] { false, true })
            {
                Assert.That(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = more, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }).Any(ExitRooms.IsWayDown), Is.False, "off: never drawn");
                Assert.That(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = more, ExitRoomsOfTheirOwn = true }).Count(ExitRooms.IsWayDown), Is.EqualTo(3));
                // On, the other sets' lists are as they were, in their order: the third set after them.
                Assert.That(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = more, ExitRoomsOfTheirOwn = true }).Where(t => !ExitRooms.IsWayDown(t)),
                            Is.EqualTo(RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = more, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false })));
            }
            foreach (int floor in new[] { 1, 4, 5 })
            {
                Assert.That(ExitRooms.ThemeOf(new LevelRoom { Kind = RoomKind.Exit }, floor, Off), Is.EqualTo(RoomTheme.ThroneRoom), "off: the throne room, floor " + floor);
                Assert.That(ExitRooms.ThemeOf(new LevelRoom { Kind = RoomKind.Exit }, floor, On), Is.EqualTo(floor < 5 ? RoomTheme.ExitHall : RoomTheme.ThroneRoom), "on, floor " + floor);
            }
        }

        /// <summary>
        /// Three thousand floors with the switch off (seeds s * 7919 + 89, floor numbers 1 to 5 in turn; two thousand with the defaults
        /// and a thousand with work order 072's second set on), folded with every room's shape, solid cells, template and added cover.
        /// Each runtime has its own number (Unity's Math folds the doubles otherwise). .NET's was taken from version 19's own code
        /// (3c1f75f, before this order, the order's files put back for the run) and this code gives the same; Unity's from a filtered
        /// Unity EditMode run of this test on this code, with the switch off (work order 072's way: pin 0, read the number, pin it).
        /// </summary>
        [Test]
        public void SwitchOff_ThreeThousandFloors_AreVersion19s_FieldForField()
        {
            ulong h = 14695981039346656037UL;
            foreach (bool more in new[] { false, true })
                for (ulong s = 0; s < (more ? 1000UL : 2000UL); s++)
                {
                    LevelLayout level = LevelGenerator.Generate(s * 7919UL + 89UL, (int)(s % 5) + 1, new LevelValues { MoreTemplates = more, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false }); // work order 103: the switch off named
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
                }
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x2E1FD118BE87BD61UL; // Unity's EditMode run of this code (f02a9d2), the switch off, 2026-11-23
#else
            const ulong pinned = 0x8854E2990E1346B7UL; // .NET, version 19's code (3c1f75f), its defaults, before this order; this code the same
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("EXITOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the three thousand floors of version 19, found " + found);
        }

        static IEnumerable<LevelLayout> Floors(int seedsAFloor, LevelValues v, IEnumerable<int> floors, ulong salt = 0)
        {
            foreach (int floor in floors)
                for (ulong s = 0; s < (ulong)seedsAFloor; s++)
                    yield return LevelGenerator.Generate(s * 6151UL + salt, floor, v);
        }

        /// <summary>
        /// With the switch on, a thousand seeds a floor, floors 1 to 5: every floor is made (the generator throws for one the validator
        /// finds a fault in, its props', the lane's, the joined floor's and the way down's line among them), floors 1 to 4 end in a
        /// room of the third set and never a throne room, an exit hall; floor 5 in a throne room, a throne room; the act boss on floor 5
        /// only; no room of the third set anywhere but an exit room of floors 1 to 4; every room's floor joined again here; and each of
        /// the three seen stamped.
        /// </summary>
        [Test, Timeout(3600000)]
        public void SwitchOn_AThousandSeedsAFloor_FloorsOneToFourEndInARoomOfTheirOwn_FloorFiveInTheThroneRoom_TheBossOnFiveOnly()
        {
            LevelValues v = On;
            var seen = new Dictionary<string, int>();
            int floors = 0, fellBack = 0;
            foreach (LevelLayout l in Floors(1000, v, new[] { 1, 2, 3, 4, 5 }, 3))
            {
                floors++;
                string where = l.SeedText + " floor " + l.Floor;
                LevelRoom exit = l.ExitRoom;
                bool last = l.Floor >= v.FloorsPerAct;
                Assert.That(exit.Template, Is.Not.Null, where + ": the exit room takes a template");
                if (last) Assert.That(ExitRooms.IsThrone(exit.Template.Template), Is.True, where + ": a throne room on the act's last floor");
                else Assert.That(ExitRooms.IsWayDown(exit.Template.Template), Is.True, where + ": a room of its own on floors 1 to 4");
                if (exit.Shape != RoomShape.Template) fellBack++;
                else seen[exit.Template.Template.Name] = seen.TryGetValue(exit.Template.Template.Name, out int c) ? c + 1 : 1;
                Assert.That(exit.Theme, Is.EqualTo(last ? RoomTheme.ThroneRoom : RoomTheme.ExitHall), where);
                Assert.That(l.Rooms.Sum(r => r.EnemyGroups.Count(g => g.Boss)), Is.EqualTo(last ? 1 : 0), where + ": the act boss on floor 5 only");
                if (!last) Assert.That(exit.Props.Any(p => p.Model == PropModel.BoneThrone), Is.False, where + ": no throne on floors 1 to 4"); // floor 5's throne room as before (its throne is a set piece that may find no place)
                foreach (LevelRoom r in l.Rooms)
                {
                    if (r != exit) Assert.That(ExitRooms.IsWayDown(r.Template?.Template), Is.False, where + ": the third set only in the exit room");
                    if (JoinedFloor.Asks(r)) Assert.That(JoinedFloor.FaultOf(l, r, v), Is.Null, where + " " + r.Letter + r.Order);
                }
            }
            TestContext.WriteLine("EXITON " + floors + " floors made; floors 1 to 4's exit rooms: " + string.Join(", ", Names.Select(n => n + " " + (seen.TryGetValue(n, out int c) ? c : 0)))
                                  + "; fell back to a drawn shape " + fellBack + "; floor 5's: " + string.Join(", ", seen.Where(kv => !Names.Contains(kv.Key)).Select(kv => kv.Key + " " + kv.Value)));
            Assert.That(Names.All(seen.ContainsKey), Is.True, "each of the three stamped");
            Assert.That(fellBack, Is.Zero, "no exit room's stamp fell back");
        }

        /// <summary>
        /// With the switch on, the rooms before the exit room are drawn as they were: the templates' stream gives the combat and elite
        /// rooms the very templates and mirrors it gave with it off (only the exit room's pick is limited), and every floor's enemies
        /// are the same groups (the fights are drawn before the layout).
        /// </summary>
        [Test]
        public void SwitchOn_TheOtherRoomsTemplatesAndTheFloorsEnemies_AreDrawnAsTheyWere()
        {
            foreach (int floor in new[] { 1, 2, 3, 4, 5 })
                for (ulong s = 0; s < 40; s++)
                {
                    LevelLayout on = LevelGenerator.Generate(s * 104729UL + 7UL, floor, On), off = LevelGenerator.Generate(s * 104729UL + 7UL, floor, Off);
                    string where = "seed " + on.SeedText + " floor " + floor;
                    if (on.Attempt == off.Attempt)
                    {
                        string Combat(LevelLayout l) => string.Join(",", l.Rooms.Where(r => r.Kind == RoomKind.Combat).Select(r => r.Template?.ToString() ?? "-"));
                        Assert.That(Combat(on).Split(',').Select(x => x.Split(' ')[0]), Is.EqualTo(Combat(off).Split(',').Select(x => x.Split(' ')[0])), where + ": the combat rooms' templates");
                    }
                    string Enemies(LevelLayout l) => string.Join("|", l.Rooms.Where(r => r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit)
                                                                    .Select(r => string.Join(",", r.EnemyGroups.Select(g => g.ToString()).OrderBy(x => x, StringComparer.Ordinal))));
                    Assert.That(Enemies(on), Is.EqualTo(Enemies(off)), where + ": the same enemies, room for room");
                }
        }

        /// <summary>The way down's line keeps every tall prop off it: the validator finds a pillar stood on it, and none of the floors' own.</summary>
        [Test]
        public void ATallPropOnTheWayDownsLine_IsAFault_ALowOneIsNot()
        {
            LevelLayout l = LevelGenerator.Generate(0x89089UL, 2, On);
            LevelRoom exit = l.ExitRoom;
            Assert.That(ExitRooms.WayDownLine(l, exit, out LevelPoint a, out LevelPoint b), Is.True);
            Assert.That(LevelValidator.Validate(l, On), Is.Empty);
            foreach (LevelProp p in exit.Props.Where(FightCover.IsTall))
                Assert.That(p.Footprint.DistanceTo(a, b), Is.GreaterThanOrEqualTo(On.PropRules.WayDownClear - 1e-9), "every tall prop off the line");
            // A pillar on the line, at two fifths of the way: the rule says so (other rules may too).
            var at = new LevelPoint(a.X + (b.X - a.X) * 0.4, a.Y + (b.Y - a.Y) * 0.4);
            var faults = new List<string>();
            PropRules.Fault(l, exit, LevelProp.Make(PropModel.Pillar, at, 0, PropPlace.Floor), exit.Props, On.PropRules, faults);
            Assert.That(faults, Has.Some.Contains("way down's line"));
            faults.Clear();
            PropRules.Fault(l, exit, LevelProp.Make(PropModel.Rubble, at, 0, PropPlace.Floor), exit.Props, On.PropRules, faults);
            Assert.That(faults, Has.None.Contains("way down's line"), "a low prop may stand on it");
            // A throne room has no such line.
            LevelLayout five = LevelGenerator.Generate(0x89089UL, 5, On);
            Assert.That(ExitRooms.WayDownLine(five, five.ExitRoom, out _, out _), Is.False);
        }

        // ---------------- The room lab ----------------

        [Test]
        public void TheLab_ListsEachUnderItsName_NotInTheFloorsYet_AndBuildsEachWithTheExitMixesOfFloorsOneToFour()
        {
            var v = Off;
            List<LabEntry> exits = RoomLab.EntriesOf(LabKind.Exit, v);
            Assert.That(exits.Where(e => ExitRooms.IsWayDown(e.Template)).Select(e => e.Name), Is.EqualTo(Names));
            foreach (LabEntry e in exits.Where(x => ExitRooms.IsWayDown(x.Template)))
            {
                Assert.That(e.InFloors, Is.False);
                Assert.That(e.Label, Is.EqualTo(e.Name + " (not in the floors yet)"));
            }
            Assert.That(RoomLab.EntriesOf(LabKind.Exit, On).Where(e => ExitRooms.IsWayDown(e.Template)).All(e => e.InFloors), Is.True, "on: in the floors");
            Assert.That(RoomLab.Mixes.Select(m => m.Floor).Where(f => f <= 4).Distinct().OrderBy(f => f), Is.EqualTo(new[] { 1, 2, 3, 4 }), "a preset for each of floors 1 to 4");
            int warders = 0, bombers = 0;
            foreach (LabEntry e in exits.Where(x => ExitRooms.IsWayDown(x.Template)))
                foreach (LabMix mix in RoomLab.Mixes)
                    for (ulong s = 1; s <= 3; s++)
                    {
                        LevelLayout lab = RoomLab.Build(e, mix, s * 8089UL, v);
                        string where = e.Name + " " + mix + " seed " + s;
                        Assert.That(RoomLab.Faults(lab, mix, v), Is.Empty, where);
                        LevelRoom room = lab.Rooms[1];
                        Assert.That(room.Shape, Is.EqualTo(RoomShape.Template), where);
                        Assert.That(room.Theme, Is.EqualTo(RoomTheme.ExitHall), where + ": an exit hall, not a throne room");
                        Assert.That(room.Props.Any(p => p.Model == PropModel.BoneThrone), Is.False, where + ": no throne");
                        Assert.That(ExitRooms.WayDownLine(lab, room, out _, out _), Is.True, where);
                        warders += room.EnemyGroups.Count(g => g.Kind == EnemyKind.Warder);
                        bombers += room.EnemyGroups.Count(g => g.Kind == EnemyKind.Bomber);
                        // Work order 090 (changed deliberately): the preset is the floor's, so no warder before the warder's floor.
                        if (mix.Floor < v.WarderFloors.FromFloor) Assert.That(room.EnemyGroups.Any(g => g.Kind == EnemyKind.Warder), Is.False, where);
                    }
            Assert.That(warders, Is.GreaterThan(0), "the warders' presets hold warders");
            Assert.That(bombers, Is.GreaterThan(0));
        }

        // ---------------- The numbers for the report ----------------

        sealed class Tally
        {
            public int Rooms, Ranged, Chain, Shooters, Low, Enemies, EnemiesMost, Groups, InBand, TallSum, LowSum, Piers;
            public double Open, Walk, Straight, FarPlaces;

            public string Line(string name, int ofFloors) => Rooms == 0 ? name + ": -" : string.Format(Inv,
                "{0,-15} drawn {1,6:P1}  enemies {2,4:0.0} (most {3,2})  standing in their band {4,6:P1}  far-band places {5,4:0.0}  chain {6,6:P1}  low rule {7,6:P1}  tall {8,3:0.0} low {9,3:0.0} piers {10,3:0.0}  open {11,6:P1}  door to the way down {12,4:0.0} m straight, {13,4:0.0} m walked = {14,3:0.0} s",
                name, Rooms / (double)ofFloors, Enemies / (double)Rooms, EnemiesMost, InBand / (double)Math.Max(1, Groups), FarPlaces / Rooms,
                Chain / (double)Math.Max(1, Ranged), Low / (double)Math.Max(1, Shooters), TallSum / (double)Rooms, LowSum / (double)Rooms, Piers / (double)Rooms,
                Open / Rooms, Straight / Rooms, Walk / Rooms, Walk / Rooms / new FeelValues().RunSpeed);
        }

        /// <summary>
        /// The places in the room's far band (<see cref="LevelValues.FarBand"/> of its farthest place from the door in, as the generator
        /// measures it) where an enemy may stand, taken greedily on the generator's 1 m grid at least <see cref="LevelValues.EnemySpacing"/>
        /// apart: how many groups the far band holds standing apart, before any enemy is put (the shooters, casters and warders stand there).
        /// </summary>
        static int FarBandPlaces(LevelLayout level, LevelRoom room, LevelValues v)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();
            var places = new List<LevelPoint>();
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1, ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    places.Add(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step));
            double farthest = places.Max(p => LevelGeometry.DistanceToDoor(p, entry, cs));
            var taken = new List<LevelPoint>();
            foreach (LevelPoint p in places)
            {
                if (LevelGeometry.DistanceToDoor(p, entry, cs) / farthest < v.FarBand - 1e-9) continue;
                if (LevelGeometry.EnemyPlaceFault(level, room, p, entry, doors, v) != null) continue;
                if (taken.Any(q => q.DistanceTo(p) < v.EnemySpacing - 1e-9)) continue;
                taken.Add(p);
            }
            return taken.Count;
        }

        /// <summary>Did each group stand in its band (as the generator tries it first: shooters, casters, warders the far band; brutes, guards, bombers the middle; swarmers anywhere; the boss at the middle)?</summary>
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
        /// The walk from the door in to the way down's place (the A cells' middle), on a half-metre grid of the room's free floor
        /// (<see cref="FightCover.FreeSpot"/>: open cells, the walk radius from walls, carved cells and props), eight ways; the straight
        /// line's length when the grid finds no way (it always did).
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
            if (s < 0 || t < 0) return from.DistanceTo(to);
            var dist = new double[free.Length];
            for (int k = 0; k < dist.Length; k++) dist[k] = double.MaxValue;
            dist[s] = 0;
            var queue = new SortedSet<(double d, int k)> { (0, s) };
            while (queue.Count > 0)
            {
                (double d, int k) = queue.Min;
                queue.Remove(queue.Min);
                if (k == t) return d + from.DistanceTo(new LevelPoint(x0 + (s % nx + 0.5) * step, y0 + (s / nx + 0.5) * step));
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
            return from.DistanceTo(to);
        }

        static void Count(LevelValues v, int seeds, Dictionary<string, Tally> by, out int floors)
        {
            floors = 0;
            foreach (LevelLayout l in Floors(seeds, v, new[] { 1, 2, 3, 4 }, 5))
            {
                floors++;
                LevelRoom r = l.ExitRoom;
                string name = r.Template != null && r.Shape == RoomShape.Template ? r.Template.Template.Name : "drawn: " + RoomShapes.Word(r.Shape);
                FightFrame f = FightCover.FrameOf(l, r, v);
                bool ranged = f != null && f.HasRanged, chain = ranged && FightCover.ChainHolds(l, f, v);
                bool shooters = ranged && f.HasShooters, low = shooters && FightCover.LowHolds(l, f, v);
                Bands(l, r, v, out int groups, out int inBand);
                int enemies = r.EnemyGroups.Sum(g => g.Count);
                // The way down's place: the A cells' middle (the throne's in a throne room), from the door in's middle.
                LevelDoor entry = LevelGeometry.EntryDoor(l, r);
                entry.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                var door = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                double sx = 0, sy = 0;
                int n = 0;
                for (int y = r.Rect.Y; y < r.Rect.Top; y++)
                    for (int x = r.Rect.X; x < r.Rect.Right; x++)
                        if (RoomTemplates.MarkAt(r, x, y) == 'A') { sx += (x + 0.5) * l.CellSize; sy += (y + 0.5) * l.CellSize; n++; }
                LevelPoint place = n > 0 ? new LevelPoint(sx / n, sy / n) : r.Middle(l.CellSize);
                foreach (string key in new[] { "all", name })
                {
                    if (!by.TryGetValue(key, out Tally t)) by[key] = t = new Tally();
                    t.Rooms++;
                    t.Enemies += enemies;
                    t.EnemiesMost = Math.Max(t.EnemiesMost, enemies);
                    t.Groups += groups;
                    t.InBand += inBand;
                    t.TallSum += r.Props.Count(FightCover.IsTall);
                    t.LowSum += r.Props.Count(FightCover.IsLow);
                    t.Piers += r.SolidCount;
                    t.Open += PropRules.WalkableShare(l, r, v.PropRules);
                    t.Straight += door.DistanceTo(place);
                    t.Walk += Walk(l, r, v, door, place);
                    t.FarPlaces += FarBandPlaces(l, r, v);
                    if (ranged) t.Ranged++;
                    if (chain) t.Chain++;
                    if (shooters) t.Shooters++;
                    if (low) t.Low++;
                }
            }
        }

        /// <summary>
        /// The table of the report (run by name; some minutes on .NET): a thousand seeds a floor, floors 1 to 4, the exit room. With the
        /// switch on (the third set) beside the switch off with work order 072's second set on (the four throne rooms, as floors 1 to 4
        /// end today with both sets): per template, how often it is drawn, its enemies (mean, most) and the share of its groups that
        /// stood in their band, the far band's places for groups standing apart, the share where the chain of tall cover and the low
        /// cover for the duck hold, the tall and low props and the solid cells, the share of its floor that is walkable, and the door
        /// to the way down (its A: the throne's in a throne room) straight and walked, and the seconds to run it at her run.
        /// </summary>
        [Explicit, Test, Timeout(7200000)]
        public void Measure_AThousandSeedsAFloor_FloorsOneToFour_TheThirdSetBesideTheThrones()
        {
            var mine = new Dictionary<string, Tally>();
            var thrones = new Dictionary<string, Tally>();
            Count(On, 1000, mine, out int floorsOn);
            Count(new LevelValues { MoreTemplates = true }, 1000, thrones, out int floorsOff);
            var lines = new List<string> { "EXITTABLE a thousand seeds a floor, floors 1 to 4, the exit room; the switch on (the third set) and, beside it, off with the second set on (the four throne rooms)" };
            foreach (string key in new[] { "all" }.Concat(mine.Keys.Where(k => k != "all").OrderBy(k => k, StringComparer.Ordinal)))
                lines.Add("EXITTABLE on   " + mine[key].Line(key, floorsOn));
            foreach (string key in new[] { "all" }.Concat(thrones.Keys.Where(k => k != "all").OrderBy(k => k, StringComparer.Ordinal)))
                lines.Add("EXITTABLE off  " + thrones[key].Line(key, floorsOff));
            TestContext.WriteLine(string.Join("\n", lines));
        }
    }
}
