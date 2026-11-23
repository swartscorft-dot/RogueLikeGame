using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The room lab's rules (work order 084; Kristian ticked "Gråboks-bryter og romlab" on 2026-11-22): every room of the
    /// library (and the generator's drawn shapes, and the treasure room) built alone with every mix and several seeds, each a
    /// room a floor's rules find nothing in; the presets the floors' own numbers (the very draw a floor's room makes, the
    /// floor's range, the act boss, the elite, the bombers by the floor's most); the lab room's form (a straight passage on
    /// its door in, her start in it, the door beside an I); the same seed the same room; the summary's table per lab room.
    /// "ROOMLABTIME" says how long a build takes here.
    /// </summary>
    public class RoomLabTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly ulong[] Seeds = { 0x1001UL, 0xBEEF84UL, 0x0840084UL };

        [Test]
        public void TheEntries_AreEveryUsableTemplate_InTheLibrarysOrder_ThenTheDrawnShapes_AndTheTreasureRoom()
        {
            // The default values: work order 072's second set off in the floors (since 2026-11-22), and in the lab all the same.
            var v = new LevelValues();
            Assume.That(v.MoreTemplates, Is.False, "the second set off in the floors by default");
            List<LabEntry> entries = RoomLab.Entries(v);
            // Work order 089 (changed deliberately): and the third set, the exit rooms of their own, whatever its switch says.
            IReadOnlyList<RoomTemplate> usable = RoomTemplateLibrary.Usable(new LevelValues { MoreTemplates = true, ExitRoomsOfTheirOwn = true });
            Assert.That(entries.Where(e => e.Template != null).Select(e => e.Template), Is.EqualTo(usable), "every template of the library the generator could draw, in its order, both sets");
            Assert.That(usable.Count(t => t.Since >= RoomTemplateLibrary.MoreSince), Is.GreaterThan(0), "work order 072's second set among them");
            Assert.That(RoomTemplateLibrary.Usable(v).All(t => t.Since < RoomTemplateLibrary.MoreSince), Is.True, "the floors' own list: the first eleven only");
            foreach (LabEntry e in entries.Where(x => x.Template != null))
            {
                Assert.That(e.Kind, Is.EqualTo(RoomLab.KindFor(e.Template.For)), e.Name + ": the room it is for");
                bool second = e.Template.Since >= RoomTemplateLibrary.MoreSince;
                Assert.That(e.InFloors, Is.EqualTo(!second), e.Name + ": in the floors only if of the first set");
                Assert.That(e.Label, Is.EqualTo(second ? e.Name + " (not in the floors yet)" : e.Name), "the picker's words");
            }
            Assert.That(v.MoreTemplates, Is.False, "the floors' values as they were");
            Assert.That(entries.Where(e => e.Template == null).Select(e => e.Kind), Is.EqualTo(new[] { LabKind.Ordinary, LabKind.Elite, LabKind.Exit, LabKind.Treasure }));
            Assert.That(entries.Where(e => e.Template == null).All(e => e.InFloors), Is.True);
            Assert.That(RoomLab.EntriesOf(LabKind.Treasure, v).Single().Name, Is.EqualTo("treasure room"));
            // With the second set on in the floors (work order 089: and the third): the same list, nothing marked.
            var on = new LevelValues { MoreTemplates = true, ExitRoomsOfTheirOwn = true };
            Assert.That(RoomLab.Entries(on).Select(e => e.Template), Is.EqualTo(entries.Select(e => e.Template)));
            Assert.That(RoomLab.Entries(on).All(e => e.InFloors), Is.True, "the second set on: nothing marked");
        }

        [Test]
        public void ASecondSetRoom_IsBuiltWithTheDefaultValues_AndTheFloorsStayWithoutTheSecondSet()
        {
            var v = new LevelValues();
            LabEntry second = RoomLab.Entries(v).First(e => e.Template != null && !e.InFloors);
            LevelLayout lab = RoomLab.Build(second, RoomLab.Mixes[2], 0x5EC0DUL, v); // floor 3 (work order 090: index 2 now, 1 before)
            Assert.That(lab.Rooms[1].Template.Template, Is.SameAs(second.Template));
            Assert.That(RoomLab.Faults(lab, RoomLab.Mixes[2], v), Is.Empty);
            Assert.That(v.MoreTemplates, Is.False, "the floors' values as they were");
            for (ulong seed = 1; seed <= 6; seed++)
                Assert.That(LevelGenerator.Generate(seed, 1 + (int)(seed % 5), v).Rooms.Where(r => r.Template != null).All(r => r.Template.Template.Since < RoomTemplateLibrary.MoreSince),
                            Is.True, "a floor of the default values holds none of the second set, lab room or not");
            var room = new LevelRoom { Kind = RoomKind.Combat, Template = second.Template.Oriented(LevelGenerator.South, false), Shape = RoomShape.Template };
            Assert.That(GreyboxRules.NotInTheFloors(room, v), Is.True, "the greybox box says it too");
            Assert.That(GreyboxRules.NotInTheFloors(room, new LevelValues { MoreTemplates = true }), Is.False);
        }

        [Test]
        public void ThePresets_AreTheFloorsOwnNumbers()
        {
            var v = new LevelValues();
            // Work order 090 (changed deliberately): one preset a floor, floor 1 to floor 5 in order, the floor alone saying what is in
            // it (before: 1, 3, 5, 4 with bombers, 2, 3 with a warder, 4 with a warder and bombers; the last three put the most).
            Assert.That(RoomLab.Mixes.Select(m => m.Floor), Is.EqualTo(new[] { 1, 2, 3, 4, 5 }));
            Assert.That(RoomLab.Mixes.Select(m => m.Name), Is.EqualTo(new[] { "floor 1", "floor 2", "floor 3", "floor 4", "floor 5" }));
            foreach (LabMix mix in RoomLab.Mixes)
                foreach (LabKind kind in new[] { LabKind.Ordinary, LabKind.Elite, LabKind.Exit })
                    for (ulong s = 1; s <= 40; s++)
                    {
                        RoomKind k = RoomLab.KindOf(kind);
                        List<EnemyGroup> lab = RoomLab.Groups(kind, mix, LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0), v);
                        // The floor's own code, from the same stream: its room's groups, the elite after them, then the floors' own
                        // bombers and warders (LevelBombers.Put, LevelWarders.Put) with the room as a floor's second fight.
                        LevelRandom rng = LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0);
                        List<EnemyGroup> floor = LevelGenerator.DrawGroups(k, mix.Floor, rng, v);
                        if (kind == LabKind.Elite)
                        {
                            EnemyKind[] kinds = v.EliteKinds(mix.Floor);
                            floor.Add(new EnemyGroup(kinds[rng.Below(kinds.Length)], 1, elite: true));
                        }
                        int bombersMost = LevelBombers.Most(v, k, mix.Floor, false);
                        int wardersMost = LevelWarders.Most(v, k, mix.Floor, false, LevelWarders.Count(floor) - 1);
                        if (bombersMost > 0 || wardersMost > 0)
                        {
                            ulong floorSeed = rng.NextBits();
                            var fights = new List<List<EnemyGroup>> { new List<EnemyGroup>(), floor };
                            if (k == RoomKind.Combat) fights.Add(new List<EnemyGroup>());
                            LevelBombers.Put(fights, floorSeed, mix.Floor, v);
                            LevelWarders.Put(fights, floorSeed, mix.Floor, v);
                        }
                        Assert.That(lab.Select(g => g.ToString()), Is.EqualTo(floor.Select(g => g.ToString())), mix + " " + kind + " seed " + s);

                        v.EnemyRange(k, mix.Floor, out int fewest, out int most);
                        int count = lab.Where(g => !g.Boss && !g.Elite).Sum(g => g.Count);
                        Assert.That(count, Is.InRange(fewest, most), "the floor's range");
                        Assert.That(lab.Count(g => g.Boss), Is.EqualTo(k == RoomKind.Exit && mix.Floor >= v.FloorsPerAct ? 1 : 0), "the act boss in the act's last exit room only");
                        Assert.That(lab.Count(g => g.Elite), Is.EqualTo(kind == LabKind.Elite ? 1 : 0));
                        Assert.That(lab.Where(g => g.Kind == EnemyKind.Bomber).Sum(g => g.Count), Is.InRange(0, bombersMost), "the floor's bombers, none to its most");
                        Assert.That(lab.Where(g => g.Kind == EnemyKind.Warder).Sum(g => g.Count), Is.InRange(0, Math.Max(0, wardersMost)), "the floor's warders, none to its most");
                        if (mix.Floor < v.WarderFloors.FromFloor) Assert.That(lab.Any(g => g.Kind == EnemyKind.Warder), Is.False, "no warder before its floor");
                        if (mix.Floor < v.BomberFloors.FromFloor) Assert.That(lab.Any(g => g.Kind == EnemyKind.Bomber), Is.False, "no bomber before its floor");
                    }
            // The bombers' most is the floors' (work order 073's table): floor 4, its first floor, one in a combat room, two in the exit.
            Assert.That(RoomLab.BombersIn(LabKind.Ordinary, RoomLab.Mixes[3], v), Is.EqualTo(LevelBombers.Most(v, RoomKind.Combat, 4, false)).And.EqualTo(1));
            Assert.That(RoomLab.BombersIn(LabKind.Exit, RoomLab.Mixes[3], v), Is.EqualTo(LevelBombers.Most(v, RoomKind.Exit, 4, false)).And.EqualTo(2));
            Assert.That(RoomLab.BombersIn(LabKind.Ordinary, RoomLab.Mixes[2], v), Is.Zero, "none on floor 3");
            Assert.That(RoomLab.Groups(LabKind.Treasure, RoomLab.Mixes[3], LevelRandom.Stream(1, "x"), v), Is.Empty, "nothing in a treasure room");
        }

        [Test]
        public void EveryRoomOfTheLibrary_WithEveryMix_IsBuiltAlone_AndIsARoomOfAFloor()
        {
            var v = new LevelValues();
            var times = new List<double>();
            var problems = new List<string>();
            int built = 0, bombersSeen = 0;
            foreach (LabEntry entry in RoomLab.Entries(v))
                foreach (LabMix mix in RoomLab.Mixes)
                    foreach (ulong seed in Seeds)
                    {
                        var watch = Stopwatch.StartNew();
                        LevelLayout lab;
                        try { lab = RoomLab.Build(entry, mix, seed, v); }
                        catch (InvalidOperationException e)
                        {
                            problems.Add(e.Message);
                            continue;
                        }
                        times.Add(watch.Elapsed.TotalMilliseconds);
                        built++;
                        List<string> faults = RoomLab.Faults(lab, mix, v);
                        if (faults.Count > 0) problems.Add(entry + " " + mix + " seed " + seed + ": " + string.Join("; ", faults));
                        LevelRoom room = lab.Rooms[1];
                        Assert.That(room.Kind, Is.EqualTo(RoomLab.KindOf(entry.Kind)), entry.ToString());
                        Assert.That(room.Elite, Is.EqualTo(entry.Kind == LabKind.Elite));
                        if (entry.Template != null)
                        {
                            Assert.That(room.Template.Template, Is.SameAs(entry.Template));
                            Assert.That(room.Shape, Is.EqualTo(RoomShape.Template), entry + ": stamped from its template");
                        }
                        bombersSeen += room.EnemyGroups.Count(g => g.Kind == EnemyKind.Bomber);
                    }
            times.Sort();
            TestContext.WriteLine(string.Format(Inv, "ROOMLABTIME {0} lab rooms built, median {1:0.0} ms, slowest {2:0.0} ms (.NET or the Editor, no engine build)",
                                                built, times[times.Count / 2], times[times.Count - 1]));
            Assert.That(problems, Is.Empty, "every lab room is a room of a floor");
            Assert.That(bombersSeen, Is.GreaterThan(0), "the bombers' mix holds bombers");
        }

        [Test]
        public void ALabRoom_IsAStraightPassageOnItsDoorIn_HerStartInIt_AndTheSameSeedIsTheSameRoom()
        {
            var v = new LevelValues();
            PassageValues pv = v.PassageRules;
            LabEntry nave = RoomLab.Entries(v).First(e => e.Template != null && e.Kind == LabKind.Ordinary);
            foreach (ulong seed in Seeds)
            {
                LevelLayout lab = RoomLab.Build(nave, RoomLab.Mixes[0], seed, v);
                Assert.That(lab.Rooms.Select(r => r.Kind), Is.EqualTo(new[] { RoomKind.Passage, RoomKind.Combat }), "a passage, then the room");
                Assert.That(lab.Doors, Has.Count.EqualTo(1), "one door: no neighbours");
                Assert.That(lab.InALine && lab.Passages && lab.Templates, Is.True, "read as a floor in a line with passages and templates");
                LevelRoom passage = lab.Rooms[0], room = lab.Rooms[1];
                LevelDoor door = lab.Doors[0];
                Assert.That(LevelGeometry.WallOf(room.Rect, door), Is.EqualTo(LevelGenerator.South), "the door in its south wall");
                Assert.That(room.Template.InWall, Is.EqualTo(LevelGenerator.South), "the template turned to come in from the south");
                Assert.That(RoomTemplates.MarkAt(room, door.From, room.Rect.Y), Is.EqualTo('I'), "the door beside an I");
                Assert.That(passage.Rect.Width, Is.EqualTo(1));
                Assert.That(passage.Rect.Height, Is.InRange(pv.Fewest, Math.Min(pv.Most, pv.StraightMost)), "straight, as deep as a passage");
                Assert.That(passage.Rect.Top, Is.EqualTo(room.Rect.Y), "against the room's south wall");
                Assert.That(passage.Rect.X, Is.EqualTo(door.From));
                Assert.That(passage.PlayerStart.HasValue, Is.True);
                Assert.That(lab.RoomAt(passage.PlayerStart.Value.X, passage.PlayerStart.Value.Y), Is.SameAs(passage), "her start in the passage");
                Assert.That(passage.PlayerStart.Value.Y, Is.LessThan((passage.Rect.Y + 1) * v.CellSize), "at its far end");
                Assert.That(RoomFightRules.HoldsAFight(lab, room, room.EnemyCount), Is.True, "the room holds a fight, its point measured from the passage's door");
                Assert.That(LevelGeometry.EntryDoor(lab, room), Is.SameAs(door));
                Assert.That(RoomShapes.GateCells(lab, room), Is.Empty, "no gate: the passage is the threshold");
                Assert.That(RoomLab.Build(nave, RoomLab.Mixes[0], seed, v).Describe(), Is.EqualTo(lab.Describe()), "the same seed: the same room, field for field");
            }
            Assert.That(Seeds.Select(s => RoomLab.Build(nave, RoomLab.Mixes[0], s, v).Describe()).Distinct().Count(), Is.GreaterThan(1), "other seeds: other rooms");
        }

        [Test]
        public void TheTreasureRoom_HoldsItsChest_AndNoEnemy()
        {
            var v = new LevelValues();
            LabEntry treasure = RoomLab.EntriesOf(LabKind.Treasure, v).Single();
            LevelLayout lab = RoomLab.Build(treasure, RoomLab.Mixes[2], Seeds[0], v);
            LevelRoom room = lab.Rooms[1];
            Assert.That(room.Kind, Is.EqualTo(RoomKind.Treasure));
            Assert.That(room.EnemyPoints, Is.Empty);
            Assert.That(room.Props.Count(p => p.Model == PropModel.Chest), Is.EqualTo(1), "its chest, by the props' rules");
        }

        [Test]
        public void TheSummary_HasATablePerLabRoom_TimeToClearDamageAndDeaths()
        {
            SessionEvent Room(double t, string name, string seed, float ms)
            {
                var e = new SessionEvent(t, LabRoomEvents.Room, name);
                e.AddExtra("kind", "ordinary");
                e.AddExtra("mix", "floor 1");
                e.AddExtra("floor", "1");
                e.AddExtra("seed", seed);
                e.AddExtra("ms", ms.ToString(Inv));
                return e;
            }
            SessionEvent Clear(double t, float seconds)
            {
                var e = new SessionEvent(t, LabRoomEvents.Clear);
                e.AddExtra("seconds", seconds.ToString(Inv));
                return e;
            }
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.PlayerDamaged) { Damage = 99 }, // before the lab: no room's
                Room(1, "nave", "AAAA", 40f),
                new SessionEvent(2, SessionEventTypes.PlayerDamaged) { Damage = 10 },
                new SessionEvent(3, SessionEventTypes.PlayerDeath),
                new SessionEvent(3.1, LabRoomEvents.End) { Result = "died" },
                Room(4, "nave", "AAAA", 50f), // the same room again: another try
                new SessionEvent(5, SessionEventTypes.PlayerDamaged) { Damage = 6 },
                Clear(9, 12.5f),
                new SessionEvent(10, LabRoomEvents.End) { Result = "new seed" },
                Room(11, "nave", "BBBB", 30f), // another seed: another room
                Clear(20, 8f),
                new SessionEvent(21, SessionEventTypes.RunStart, "RUNSEED"),
                new SessionEvent(22, SessionEventTypes.PlayerDamaged) { Damage = 50 }, // in a run: no lab room's
            };
            List<LabRoomRow> rows = LabRoomsSummary.Compute(events);
            Assert.That(rows, Has.Count.EqualTo(2));
            LabRoomRow a = rows[0], b = rows[1];
            Assert.That((a.Seed, a.Tries, a.Clears, a.Damage, a.Deaths), Is.EqualTo(("AAAA", 2, 1, 16, 1)));
            Assert.That(a.MeanClear, Is.EqualTo(12.5f));
            Assert.That((b.Seed, b.Tries, b.Clears, b.Damage, b.Deaths), Is.EqualTo(("BBBB", 1, 1, 0, 0)));
            var sb = new StringBuilder();
            LabRoomsSummary.Write(sb, rows);
            string text = sb.ToString();
            StringAssert.Contains("Room lab", text);
            StringAssert.Contains("12.5 s", text);
            StringAssert.Contains("Lab rooms built: 3, in 40 ms on average, 50 ms at most", text);
            var none = new StringBuilder();
            LabRoomsSummary.Write(none, LabRoomsSummary.Compute(new List<SessionEvent> { new SessionEvent(0, SessionEventTypes.PlayerDamaged) { Damage = 5 } }));
            Assert.That(none.Length, Is.Zero, "no lab: no table");
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events)), Does.Contain("Room lab (work order 084)"), "in the session's summary");
        }
    }
}
