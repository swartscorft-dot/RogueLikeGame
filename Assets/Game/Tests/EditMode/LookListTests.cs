using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// Work order 112, the look list of the light check, its rules: the switch off is every plan as before, value for value; on,
    /// only the ossuary gets candles (at its props nearest its middle: its middle lighter, its darkest point by work order 091's
    /// measure no darker) and only the passages get lamps (a warm sconce on a far wall, their lanterns turned down); the
    /// generator never reads it. And the numbers for the report: lamp counts a room and the darkest-point measure before and
    /// after, on 091's run (226K2223).
    /// </summary>
    public class LookListTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        /// <summary>Work order 091's run seed (226K2223): the pictures' floors.</summary>
        public const ulong RunSeed = 0x09100001UL;

        [SetUp]
        public void Fresh() => LookListValues.Reset();

        [TearDown]
        public void Back() => LookListValues.Reset();

        static float LevelC => LightSteps.Of(3).LampLevel;

        sealed class Plans
        {
            public LevelLayout Layout;
            public RoomMoodPlan Mood;
            public CryptKitPlan Kit;
            public LampPlan Lamps;
        }

        static Plans Make(LevelLayout layout, LevelValues v)
        {
            var p = new Plans { Layout = layout };
            p.Mood = RoomMoodPlan.Make(layout, v);
            p.Kit = CryptKitPlan.Make(layout, v.Crypt ?? new CryptKitValues());
            p.Lamps = LampPlan.Make(layout, p.Kit, p.Mood, new LampValues());
            return p;
        }

        static string LampText(LampPlan plan, Lamp l, LampValues v) =>
            string.Format(Inv, "{0} {1} {2} h {3:R} src {4} lan {5} sec {6}/{7} prop {8} carried {9} carries {10} warm {11} share {12:R} along {13:R} added {14} I {15:R}",
                          l.Room, l.Kind, l.At, l.Height, l.Source, l.Lantern, l.Section, l.Side, l.Prop, l.CarriedBy == null ? -1 : plan.Lamps.IndexOf(l.CarriedBy), l.Carries, l.Warm,
                          l.Share, l.Along, l.ByLookList, l.Intensity(v));

        static string LampsText(LampPlan plan)
        {
            var v = new LampValues();
            var sb = new StringBuilder();
            foreach (Lamp l in plan.Lamps) sb.Append(LampText(plan, l, v)).Append('\n');
            foreach (KeyValuePair<int, double> c in plan.CoverBefore.OrderBy(k => k.Key)) sb.Append("cover ").Append(c.Key).Append(' ').Append(c.Value.ToString("R", Inv)).Append(' ').Append(plan.CoverAfter[c.Key].ToString("R", Inv)).Append('\n');
            return sb.ToString();
        }

        static string SourcesText(RoomMoodPlan mood) =>
            string.Join("\n", mood.Rooms.SelectMany(r => r.Sources.Select(s => r.Room.Order + " " + s.Kind + " " + s.At + " " + s.Height.ToString("R", Inv) + " " + s.Flame + " " + s.OfProp + " " + s.ByLookList)));

        static IEnumerable<LevelLayout> Floors(int seeds, LevelValues values = null)
        {
            var v = values ?? new LevelValues();
            for (int f = 1; f <= 5; f++) yield return LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, f), f, v);
            for (ulong s = 0; s < (ulong)seeds; s++)
                for (int f = 1; f <= 5; f++) yield return LevelGenerator.Generate(s, f, v);
        }

        // ---------------- Off is as before ----------------

        [Test]
        public void SwitchOff_EveryPlanIsAsWithEachOfTheThreeOff_ValueForValue_AndNothingOfTheOrderIsInIt_AndTheGeneratorNeverReadsIt()
        {
            int floors = 0;
            foreach (LevelLayout layout in Floors(40))
            {
                string where = "floor " + layout.Floor + " seed " + layout.SeedText;
                LookListValues.On = false;
                var v = new LevelValues();
                string layoutOff = layout.Describe();
                Plans off = Make(layout, v);
                LookListValues.On = true;
                LookListValues.LitTops = LookListValues.OssuaryCandles = LookListValues.WarmPassages = false;
                Plans none = Make(layout, v);
                Assert.That(none.Mood.Describe(), Is.EqualTo(off.Mood.Describe()), "the mood's plan: " + where);
                Assert.That(SourcesText(none.Mood), Is.EqualTo(SourcesText(off.Mood)), "its sources: " + where);
                Assert.That(LampsText(none.Lamps), Is.EqualTo(LampsText(off.Lamps)), "the lamps, field for field: " + where);
                foreach (Lamp l in off.Lamps.Lamps)
                {
                    Assert.That(l.Share, Is.EqualTo(1f), "off: every lamp at its kind's light: " + where);
                    Assert.That(l.Along, Is.EqualTo(0d), "off: every sconce at its section's middle: " + where);
                    Assert.That(l.ByLookList, Is.False, where);
                }
                Assert.That(off.Mood.Rooms.SelectMany(r => r.Sources).Any(s => s.ByLookList), Is.False, "off: no candles of the order: " + where);
                LookListValues.Reset();
                Assert.That(LevelGenerator.Generate(layout.Seed, layout.Floor, new LevelValues()).Describe(), Is.EqualTo(layoutOff), "the generator never reads the switch: " + where);
                floors++;
            }
            TestContext.WriteLine("LOOKLIST off = each of the three off, value for value, on " + floors + " floors");
        }

        // ---------------- On: only its own things change ----------------

        [Test]
        public void On_OnlyTheOssuariesGetCandles_AndOnlyThePassagesLamps_EveryOtherRoomAsBefore()
        {
            foreach (LevelLayout layout in Floors(40))
            {
                string where = "floor " + layout.Floor + " seed " + layout.SeedText;
                var v = new LevelValues();
                LookListValues.On = false;
                Plans off = Make(layout, v);
                LookListValues.On = true;
                Plans on = Make(layout, v);
                var lv = new LampValues();
                for (int i = 0; i < on.Mood.Rooms.Count; i++)
                {
                    MoodRoom a = on.Mood.Rooms[i], b = off.Mood.Rooms[i];
                    Assert.That(a.Wear.Count, Is.EqualTo(b.Wear.Count), "the wear as before: " + where);
                    Assert.That(a.LargePropPlace, Is.EqualTo(b.LargePropPlace), where);
                    var mine = a.Sources.Where(s => s.ByLookList).ToList();
                    if (a.Room.Theme != RoomTheme.Ossuary) Assert.That(mine, Is.Empty, "no other theme changes: " + where + " " + a.Room.Theme);
                    Assert.That(a.Sources.Where(s => !s.ByLookList).Select(s => s.Kind + " " + s.At), Is.EqualTo(b.Sources.Select(s => s.Kind + " " + s.At)), "the room's own sources as before: " + where);
                    Assert.That(a.Pieces.Count - b.Pieces.Count, Is.EqualTo(mine.Count), "a set of candles for each of the order's sources: " + where);
                    foreach (MoodSource s in mine)
                    {
                        Assert.That(s.Kind, Is.EqualTo(MoodSourceKind.Candle));
                        Assert.That(RoomShapes.DistanceToWalls(a.Room, s.At, layout.CellSize), Is.GreaterThanOrEqualTo(0.35), "clear of the walls: " + where);
                        foreach (LevelProp p in a.Room.Props) Assert.That(p.Footprint.DistanceTo(s.At), Is.GreaterThan(0.2), "on no prop: " + where);
                        foreach (LevelDoor d in layout.DoorsOf(a.Room.Order)) Assert.That(LevelGeometry.DistanceToDoor(s.At, d, layout.CellSize), Is.GreaterThanOrEqualTo(LookListValues.CandleFromDoors - 1e-9), "away from its doors: " + where);
                    }
                }
                // The lamps: the rooms' as before (bar the ossuary's own candles); a passage's the same lamps, its lanterns turned down, and its warm sconces.
                var offText = off.Lamps.Lamps.Select(l => LampText(off.Lamps, l, lv)).ToList();
                foreach (LevelRoom room in layout.Rooms)
                {
                    var before = off.Lamps.Lamps.Where(l => l.Room == room.Order).ToList();
                    var after = on.Lamps.Lamps.Where(l => l.Room == room.Order && (room.Kind == RoomKind.Passage || !l.ByLookList)).ToList(); // the ossuary's candles apart
                    if (room.Kind != RoomKind.Passage)
                    {
                        Assert.That(after.Select(l => l.Kind + " " + l.At + " " + l.Height.ToString("R", Inv)), Is.EqualTo(before.Select(l => l.Kind + " " + l.At + " " + l.Height.ToString("R", Inv))),
                                    "a room's lamps and its sconces as before: " + where + " " + room.Letter + room.Order);
                        continue;
                    }
                    var kept = after.Take(before.Count).ToList();
                    Assert.That(kept.Select(l => l.Kind + " " + l.At), Is.EqualTo(before.Select(l => l.Kind + " " + l.At)), "a passage keeps its lamps: " + where);
                    foreach (Lamp l in kept) Assert.That(l.Share, Is.EqualTo(l.Kind == LampKind.Lantern ? LookListValues.LanternShare : 1f), "its lanterns turned down, nothing else: " + where);
                    foreach (Lamp l in after.Skip(before.Count))
                    {
                        Assert.That(l.Kind, Is.EqualTo(LampKind.Sconce), "a passage gets only sconces: " + where);
                        Assert.That(l.ByLookList, Is.True, "marked as the order's: " + where);
                        CryptKitSection s = on.Kit.Sections[l.Section];
                        Assert.That(s.Low || s.Niche || s.AtDoor || CaveWalls.IsNear(s, CaveWalls.GameYaw), Is.False, "on a far full section, not by a door, not a niche: " + where);
                        Assert.That(s.RoomOn(l.Side), Is.EqualTo(room.Order), "on the face toward the passage: " + where);
                        Assert.That(Math.Abs(l.Along), Is.LessThan(layout.CellSize * 0.5 - 0.3), "inside its section: " + where);
                    }
                }
            }
        }

        // ---------------- The ossuary ----------------

        [Test]
        public void TheOssuary_ItsMiddleGetsLighter_ItsDarkestPointNoDarker_ByWorkOrder091sMeasure_AndNoOtherRoomDarker()
        {
            int rooms = 0, lit = 0, sets = 0, others = 0, otherDarker = 0, ossuaryDarkerWithAll = 0;
            double middleBefore = 0, middleAfter = 0;
            var lines = new List<string>();
            // Work order 135 (changed deliberately): measured on version 24's floors, the line-ups named off, as it was written (87 %); with them on
            // the ossuaries with a prop near their middle were 612, under the 646 asked: the props are laid round other enemies.
            foreach (LevelLayout layout in Floors(150, new LevelValues { LineUps = false }))
            {
                var v = new LevelValues();
                LookListValues.On = false;
                Plans off = Make(layout, v);
                // The ossuary's own item alone (the passages' light at its doors is the third item's, measured with it).
                LookListValues.On = true;
                LookListValues.WarmPassages = false;
                Plans on = Make(layout, v);
                LookListValues.WarmPassages = true;
                Plans all = Make(layout, v);
                foreach (LevelRoom room in layout.Rooms)
                {
                    if (room.Kind == RoomKind.Passage) continue;
                    LookListLamps.Darkest(layout, off.Lamps, new LampValues(), LevelC, room, out double d0, out double n0, out double m0);
                    LookListLamps.Darkest(layout, on.Lamps, new LampValues(), LevelC, room, out double d1, out double n1, out double m1);
                    string where = "floor " + layout.Floor + " seed " + layout.SeedText + " " + room.Letter + room.Order.ToString(Inv) + " " + room.Theme;
                    LookListLamps.Darkest(layout, all.Lamps, new LampValues(), LevelC, room, out double d2, out double n2, out double m2);
                    if (room.Theme != RoomTheme.Ossuary)
                    {
                        Assert.That(d1, Is.EqualTo(d0), "another room's light as before with the ossuary's item: " + where);
                        // With the passages' item too, another room changes only by the light that comes in at its doors from a
                        // passage (its lanterns turned down, a sconce added): counted, not asserted.
                        if (d2 < d0 - 1e-9 && d0 >= LookListLamps.NoLampLight) otherDarker++;
                        others++;
                        continue;
                    }
                    // 091's measure: the least lamp light, and among points with none (under NoLampLight) the one farthest from every lamp.
                    if (d0 < LookListLamps.NoLampLight && d1 < LookListLamps.NoLampLight) Assert.That(n1, Is.LessThanOrEqualTo(n0 + 1e-9), "the darkest point (no lamp light) no farther from a lamp: " + where);
                    else Assert.That(d1, Is.GreaterThanOrEqualTo(d0 - 1e-9), "the darkest point no darker: " + where);
                    if (d0 < LookListLamps.NoLampLight && d2 < LookListLamps.NoLampLight ? n2 > n0 + 1e-9 : d2 < d0 - 1e-9) ossuaryDarkerWithAll++;
                    rooms++;
                    int n = on.Mood.Of(room.Order).Sources.Count(s => s.ByLookList);
                    sets += n;
                    if (n > 0)
                    {
                        lit++;
                        Assert.That(m1, Is.GreaterThan(m0), "the middle lighter where it got candles: " + where);
                    }
                    middleBefore += m0;
                    middleAfter += m1;
                    if (layout.Seed == RunSeeds.FloorSeed(RunSeed, layout.Floor))
                        lines.Add(string.Format(Inv, "091's run, {0}: {1} sets of candles; middle {2:0.000} -> {3:0.000}; darkest {4:0.000} ({5:0.0} m from a lamp) -> {6:0.000} ({7:0.0} m); with all three on: middle {8:0.000}, darkest {9:0.000} ({10:0.0} m)", where, n, m0, m1, d0, n0, d1, n1, m2, d2, n2));
                }
            }
            foreach (string l in lines) TestContext.WriteLine("LOOKLIST " + l);
            TestContext.WriteLine(string.Format(Inv, "LOOKLIST ossuaries {0} (091's run and seeds 0-149, floors 1-5): {1} got candles ({2:0%}), {3:0.0} sets each; their middle by 091's measure at C {4:0.000} -> {5:0.000} on average",
                                                rooms, lit, lit / (double)Math.Max(1, rooms), sets / (double)Math.Max(1, rooms), middleBefore / Math.Max(1, rooms), middleAfter / Math.Max(1, rooms)));
            TestContext.WriteLine(string.Format(Inv, "LOOKLIST the other rooms: {0}, of them {1} with a darkest point that has lamp light and gets darker with all three on (by the passages' light at their doors); ossuaries whose darkest point is darker with all three on: {2}", others, otherDarker, ossuaryDarkerWithAll));
            Assert.That(rooms, Is.GreaterThan(50));
            Assert.That(lit, Is.GreaterThanOrEqualTo(rooms * 8 / 10), "most ossuaries get candles at a prop near their middle (87 % when written: in the others every prop stands farther than the reach from the middle)");
        }

        // ---------------- The passages ----------------

        [Test]
        public void ThePassages_EachGetsAWarmSconce_TheirLanternsTurnedDown_TheirMeanLightUp()
        {
            int passages = 0, warmed = 0, darker = 0;
            double meanBefore = 0, meanAfter = 0, darkBefore = 0, darkAfter = 0;
            var lines = new List<string>();
            foreach (LevelLayout layout in Floors(150))
            {
                var v = new LevelValues();
                LookListValues.On = false;
                Plans off = Make(layout, v);
                LookListValues.On = true;
                Plans on = Make(layout, v);
                var shown = new bool[layout.Rooms.Count];
                foreach (LevelRoom room in layout.Rooms)
                {
                    if (room.Kind != RoomKind.Passage) continue;
                    passages++;
                    if (on.Lamps.Of(room.Order).Any(l => l.Kind == LampKind.Sconce)) warmed++;
                    LookListLamps.Darkest(layout, off.Lamps, new LampValues(), LevelC, room, out double d0, out double n0, out _);
                    LookListLamps.Darkest(layout, on.Lamps, new LampValues(), LevelC, room, out double d1, out double n1, out _);
                    if (d1 < d0 - 1e-9) darker++;
                    darkBefore += d0;
                    darkAfter += d1;
                    double s0 = 0, s1 = 0;
                    List<LevelPoint> floor = LampPlan.FloorPoints(layout, room);
                    foreach (LevelPoint p in floor)
                    {
                        s0 += LookListLamps.LampLight(layout, off.Lamps, new LampValues(), LevelC, room.Order, p, shown, out _);
                        s1 += LookListLamps.LampLight(layout, on.Lamps, new LampValues(), LevelC, room.Order, p, shown, out _);
                    }
                    meanBefore += s0 / Math.Max(1, floor.Count);
                    meanAfter += s1 / Math.Max(1, floor.Count);
                    if (layout.Seed == RunSeeds.FloorSeed(RunSeed, layout.Floor) && room.Order == 1)
                        lines.Add(string.Format(Inv, "091's run, floor {0} seed {1}, the first passage {2}{3}: lamps {4} -> {5}; mean {6:0.000} -> {7:0.000}; darkest {8:0.000} ({9:0.0} m from a lamp) -> {10:0.000} ({11:0.0} m)",
                                                layout.Floor, layout.SeedText, room.Letter, room.Order, Counts(off.Lamps, room), Counts(on.Lamps, room), s0 / floor.Count, s1 / floor.Count, d0, n0, d1, n1));
                }
            }
            foreach (string l in lines) TestContext.WriteLine("LOOKLIST " + l);
            TestContext.WriteLine(string.Format(Inv, "LOOKLIST passages {0} (091's run and seeds 0-149, floors 1-5): {1} with a warm sconce ({2:0%}); mean lamp light {3:0.000} -> {4:0.000}; darkest point on average {5:0.000} -> {6:0.000}, darker in {7} ({8:0.0%})",
                                                passages, warmed, warmed / (double)Math.Max(1, passages), meanBefore / passages, meanAfter / passages, darkBefore / passages, darkAfter / passages, darker, darker / (double)Math.Max(1, passages)));
            Assert.That(warmed, Is.GreaterThanOrEqualTo(passages * 95 / 100), "nearly every passage has a warm sconce");
            Assert.That(meanAfter, Is.GreaterThan(meanBefore), "the passages' floor lighter on the whole");
        }

        static string Counts(LampPlan plan, LevelRoom room)
        {
            var kinds = plan.Of(room.Order).GroupBy(l => l.Kind).OrderBy(g => g.Key).Select(g => g.Count() + " " + g.Key.ToString().ToLowerInvariant());
            return string.Join(", ", kinds) + " (" + plan.LightsOf(room.Order) + " real lights)";
        }

        // ---------------- The report's numbers ----------------

        [Test]
        public void TheLampsARoom_BeforeAndAfter_On091sRun()
        {
            var v = new LevelValues();
            for (int f = 1; f <= 5; f++)
            {
                LevelLayout layout = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, f), f, v);
                LookListValues.On = false;
                Plans off = Make(layout, v);
                LookListValues.On = true;
                Plans on = Make(layout, v);
                var changed = new List<string>();
                foreach (LevelRoom room in layout.Rooms)
                {
                    string a = Counts(off.Lamps, room), b = Counts(on.Lamps, room);
                    if (a != b) changed.Add("" + room.Letter + room.Order + (room.Theme != RoomTheme.None ? " " + RoomThemes.Word(room.Theme) : "") + ": " + a + " -> " + b);
                }
                TestContext.WriteLine("LOOKLIST lamps, floor " + f + " (seed " + layout.SeedText + "), the rooms that change: " + string.Join("; ", changed));
                Assert.That(off.Lamps.Lamps.Count, Is.LessThanOrEqualTo(on.Lamps.Lamps.Count), "the order only adds lamps");
            }
        }
    }
}
