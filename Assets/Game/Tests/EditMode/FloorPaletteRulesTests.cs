using System;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// Work order 105, its table and rules (no engine): a palette a floor, floor 1 today's exactly, each floor down a step colder
    /// (the stone bluer, the lamps paler and less orange but warm, the base light bluer, the fog bluer and thicker); the switch off
    /// gives none; no light gives back less off the stone than today; and work order 091's measure, carried to colour and to the
    /// stone, finds no walkable point darker than today on any floor (floor 1 equal, value for value), the darkest of each floor
    /// written out.
    /// </summary>
    public class FloorPaletteRulesTests
    {
        /// <summary>Work order 091's run seed (226K2223), whose floors work orders 091 and 100 pictured.</summary>
        const ulong RunSeed = 0x09100001UL;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static float Luma(Rgb c) => FloorPaletteRules.Luma(c);
        static float BlueOverRed(Rgb c) => c.B / c.R;

        [Test]
        public void TheTable_FiveRows_FloorOneToday_TheSwitchOn_AndOffGivesNone()
        {
            var v = new FloorPaletteValues();
            Assert.That(v.On, Is.True, "the order's switch is on");
            Assert.That(v.Rows.Length, Is.EqualTo(5));
            Assert.That(v.Floor1.IsToday, Is.True, "floor 1 as today");
            for (int f = 2; f <= 5; f++) Assert.That(v.Rows[f - 1].IsToday, Is.False, "floor " + f + " has a palette of its own");
            for (int f = 1; f <= 5; f++) Assert.That(v.Of(f), Is.SameAs(v.Rows[f - 1]), "floor " + f);
            Assert.That(v.Of(0), Is.SameAs(v.Floor1), "under 1: floor 1's");
            Assert.That(v.Of(8), Is.SameAs(v.Floor5), "past the act's fifth: floor 5's");
            Assert.That(v.Reads, Is.EqualTo(7), "each look-up counted");
            v.On = false;
            for (int f = 1; f <= 5; f++) Assert.That(v.Of(f), Is.Null, "off: no palette on floor " + f);
        }

        [Test]
        public void EachFloorDown_IsAStepColder_TheLampsStayWarm_AndTheFogThickens()
        {
            var v = new FloorPaletteValues();
            var lamps = new LampValues();
            var mood = new MoodValues();
            FloorPalette[] rows = v.Rows;
            for (int f = 2; f <= 5; f++)
            {
                FloorPalette a = rows[f - 2], b = rows[f - 1];
                string at = "floor " + (f - 1) + " to " + f;
                Assert.That(BlueOverRed(b.Stone), Is.GreaterThan(BlueOverRed(a.Stone) + 0.05f), at + ": the stone bluer by a step");
                Assert.That(BlueOverRed(b.Lamps), Is.GreaterThan(BlueOverRed(a.Lamps)), at + ": the lamps paler");
                Assert.That(BlueOverRed(b.Base), Is.GreaterThan(BlueOverRed(a.Base)), at + ": the base light colder");
                Assert.That(BlueOverRed(b.Fog), Is.GreaterThan(BlueOverRed(a.Fog)), at + ": the fog bluer");
                Assert.That(b.FogShare, Is.GreaterThan(a.FogShare), at + ": the fog thicker");
                Rgb sa = FloorPaletteRules.StoneTint(a, lamps, mood), sb = FloorPaletteRules.StoneTint(b, lamps, mood);
                Assert.That(Math.Abs(sb.B - sa.B) + Math.Abs(sb.R - sa.R), Is.GreaterThan(0.1f), at + ": the stone's tint as given moves by a step one can see " + sa + " " + sb);
            }
            foreach (FloorPalette p in rows)
                foreach (Rgb warm in new[] { lamps.Warm, lamps.EntranceBrazier, lamps.Gold })
                {
                    Rgb c = FloorPaletteRules.Times(warm, p.Lamps);
                    Assert.That(c.R > c.G && c.G > c.B, p.Name + ": a warm lamp stays warm, red over green over blue: " + c);
                }
            Assert.That(rows.All(p => p.FogShare <= 1.5f), "the fog stays see-through: at most half as bright again (at lamplit C its grey is half the as-before fog's)");
        }

        [Test]
        public void NoLight_GivesBackLessOffTheStone_ThanToday_OnAnyFloor()
        {
            var v = new FloorPaletteValues();
            var lamps = new LampValues();
            var mood = new MoodValues();
            foreach (FloorPalette p in v.Rows)
            {
                Rgb stone = FloorPaletteRules.StoneTint(p, lamps, mood);
                float lift = FloorPaletteRules.StoneLift(p, lamps, mood);
                Assert.That(lift, Is.GreaterThanOrEqualTo(1f));
                TestContext.WriteLine(string.Format(Inv, "{0}: stone tint {1} lifted by {2:0.000} to {3}", p.Name, p.Stone, lift, stone));
                foreach ((string what, Rgb today, Rgb now) in FloorPaletteRules.Lights(p, lamps, mood))
                {
                    float before = Luma(today), after = Luma(FloorPaletteRules.Times(stone, now));
                    TestContext.WriteLine(string.Format(Inv, "  {0}: today {1:0.0000}, now {2:0.0000} ({3:+0.0;-0.0} %)", what, before, after, 100.0 * (after - before) / before));
                    Assert.That(after, Is.GreaterThanOrEqualTo(before * 0.99999f), p.Name + ": " + what);
                }
            }
            // The teal lamps keep the game's cyan; the palette has no say over the door's orange or the wards' green (no value of it reaches them).
            foreach (FloorPalette p in v.Rows)
            {
                Assert.That(FloorPaletteRules.LampColour(p, new Lamp { Kind = LampKind.Lantern }, lamps), Is.EqualTo(lamps.Teal), p.Name);
                Assert.That(FloorPaletteRules.LampColour(p, new Lamp { Kind = LampKind.Brazier }, lamps), Is.EqualTo(lamps.Teal), p.Name);
            }
        }

        static LampPlan PlanOf(LevelLayout layout, LevelValues values, LampValues lamps) =>
            LampPlan.Make(layout, CryptKitPlan.Make(layout, values.Crypt), RoomMoodPlan.Make(layout, values), lamps);

        /// <summary>
        /// The darkest-point measure a floor: on each of the five floors of work order 091's run and of four more runs, at every
        /// square metre of every room's open floor, at each lamplit level, the light seen with the floor's palette is no less than
        /// today's; on floor 1 it is today's exactly. The darkest walkable point of each floor of 091's run at lamplit C (the
        /// default) is written out, today and with the palette.
        /// </summary>
        [Test]
        public void TheDarkestPointMeasure_NoWalkablePointDarkerThanToday_OnEveryFloor_FloorOneEqual()
        {
            var values = new LevelValues();
            var lamps = new LampValues();
            var palettes = new FloorPaletteValues();
            MoodValues mood = values.Mood ?? new MoodValues();
            int points = 0;
            for (int run = 0; run < 5; run++)
            {
                ulong seed = RunSeed + (ulong)run * 7919UL;
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout layout = LevelGenerator.Generate(RunSeeds.FloorSeed(seed, floor), floor, values);
                    LampPlan plan = PlanOf(layout, values, lamps);
                    FloorPalette p = palettes.Of(floor);
                    var shown = new bool[layout.Rooms.Count];
                    for (int step = 1; step < LightSteps.Count; step++)
                    {
                        LamplitValues at = LightSteps.Of(step);
                        float darkToday = float.PositiveInfinity, darkNow = float.PositiveInfinity;
                        string darkWhere = "";
                        foreach (LevelRoom room in layout.Rooms)
                            foreach (LevelPoint pt in LampPlan.FloorPoints(layout, room))
                            {
                                FloorPaletteRules.LightAt(layout, plan, lamps, at, mood, p, room, pt, shown, out float today, out float now);
                                points++;
                                if (floor == 1) Assert.That(now, Is.EqualTo(today), "floor 1 as today, value for value");
                                else Assert.That(now, Is.GreaterThanOrEqualTo(today * 0.99999f), string.Format(Inv, "run {0} floor {1} {2} room {3}{4} at ({5:0.0}, {6:0.0})", seed, floor, at.Name, room.Letter, room.Order, pt.X, pt.Y));
                                if (today < darkToday)
                                {
                                    darkToday = today;
                                    darkNow = now;
                                    darkWhere = string.Format(Inv, "{0}{1} at ({2:0.0}, {3:0.0})", room.Letter, room.Order, pt.X, pt.Y);
                                }
                            }
                        if (run == 0 && at == LightSteps.Of(LightSteps.Default))
                            TestContext.WriteLine(string.Format(Inv, "run {0} floor {1} ({2}), {3}: the darkest walkable point {4}: light seen today {5:0.0000}, with the palette {6:0.0000} ({7:+0.0;-0.0} %)",
                                                                LevelSeed.ToText(seed), floor, p.Name, at.Name, darkWhere, darkToday, darkNow, 100.0 * (darkNow - darkToday) / darkToday));
                    }
                }
            }
            Assert.That(points, Is.GreaterThan(10000), "the measure looked at the floors");
        }
    }
}
