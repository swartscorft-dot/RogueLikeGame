using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// Work order 100, its values and rules (no engine): the shut door's look in the game (a dark body in the wall kit's range,
    /// a thin edge band glowing in the door orange; the greybox view's glow as before), and the entrance room's braziers burning
    /// warm by their named value on floor 1 and floor 2 of work order 091's run seed, while every other room's braziers and
    /// every lantern stay teal, and the entrance's floor is the combat rooms'.
    /// </summary>
    public class ShutDoorAndWarmEntranceTests
    {
        /// <summary>Work order 091's run seed (226K2223), whose pictures showed the cold entrance.</summary>
        const ulong RunSeed = 0x09100001UL;

        static float Luma(Rgb c) => 0.2126f * c.R + 0.7152f * c.G + 0.0722f * c.B;

        [Test]
        public void TheShutDoor_IsADarkBodyInTheWallKitsRange_WithAThinGlowingEdge_AndTheGreyboxGlowAsBefore()
        {
            var v = new RoomFightValues();
            Rgb orange = new GreyboxValues().Door;
            Assert.That(v.DoorDarkFace, Is.True, "the dark face is the game's look");
            Assert.That(v.DoorGlow, Is.EqualTo(0.5f), "the greybox view's glow as before");
            Assert.That(v.DoorEdgeWidth, Is.GreaterThan(0f).And.LessThanOrEqualTo(0.2f), "a thin band (the producer's start: about 0.08 m)");
            Assert.That(v.DoorEdgeGlow, Is.GreaterThan(0f), "the edge glows");
            Assert.That(Luma(v.DoorBody), Is.LessThan(0.3f), "dark");
            Assert.That(Luma(v.DoorBody), Is.LessThan(Luma(orange) * 0.5f), "far darker than the door orange");
            // The wall kit's stone (Crypt_Wall_A's painted texture): from its darkest tenth (0.30, 0.24, 0.23) to its middle (0.38, 0.29, 0.25).
            foreach (float c in new[] { v.DoorBody.R, v.DoorBody.G, v.DoorBody.B }) Assert.That(c, Is.InRange(0.18f, 0.40f), "in the wall's range of value");
            Assert.That(v.DoorBody.R, Is.GreaterThanOrEqualTo(v.DoorBody.B), "warm-grey as the stone, not blue");
        }

        static LampPlan PlanOf(LevelLayout layout, LevelValues values, LampValues lamps) =>
            LampPlan.Make(layout, CryptKitPlan.Make(layout, values.Crypt), RoomMoodPlan.Make(layout, values), lamps);

        [Test]
        public void TheEntrancesBraziers_BurnWarm_OnFloorOneAndTwo_EveryOtherBrazierAndEveryLanternStaysTeal_AndOffTheyAreTealAsBefore()
        {
            var values = new LevelValues();
            var lamps = new LampValues();
            Assert.That(lamps.WarmEntrance, Is.True);
            Assert.That(new MoodValues().WarmEntrance, Is.True, "the mood's flames and pools follow");
            Rgb warm = lamps.EntranceBrazier;
            Assert.That(warm.R > warm.G && warm.G > warm.B, "warm: red over green over blue " + warm);
            Assert.That(Luma(warm), Is.GreaterThanOrEqualTo(Luma(lamps.Teal)), "no darker than the teal it takes over from, so no point of the room gets darker");
            int others = 0;
            foreach (int floor in new[] { 1, 2 })
            {
                LevelLayout layout = LevelGenerator.Generate(RunSeeds.FloorSeed(RunSeed, floor), floor, values);
                LevelRoom entrance = layout.Rooms.First(r => r.Kind == RoomKind.Entrance);
                LampPlan plan = PlanOf(layout, values, lamps);
                List<Lamp> main = plan.Lamps.Where(l => l.Room == entrance.Order && l.Kind == LampKind.Brazier).ToList();
                Assert.That(main.Count, Is.EqualTo(2), "floor " + floor + ": the entrance's two braziers, its main light");
                foreach (Lamp l in main)
                {
                    Assert.That(l.Warm, Is.True, "floor " + floor);
                    Assert.That(lamps.ColourOf(l), Is.EqualTo(warm), "floor " + floor + ": by its named value");
                }
                foreach (Lamp l in plan.Lamps.Where(l => l.Room != entrance.Order && l.Kind == LampKind.Brazier))
                {
                    others++;
                    Assert.That(l.Warm, Is.False);
                    Assert.That(lamps.ColourOf(l), Is.EqualTo(lamps.Teal), "floor " + floor + ": another room's brazier stays teal");
                }
                foreach (Lamp l in plan.Lamps.Where(l => l.Kind == LampKind.Lantern))
                    Assert.That(lamps.ColourOf(l), Is.EqualTo(lamps.Teal), "every lantern stays teal, the entrance's too");

                // Off: as before, every brazier teal, the same lamps in the same places.
                LampPlan before = PlanOf(layout, values, new LampValues { WarmEntrance = false });
                Assert.That(before.Lamps.Where(l => l.Room == entrance.Order && l.Kind == LampKind.Brazier).All(l => !l.Warm && new LampValues().ColourOf(l).Equals(lamps.Teal)), "off: teal");
                Assert.That(before.Lamps.Select(l => (l.Room, l.Kind, l.At.X, l.At.Y)), Is.EqualTo(plan.Lamps.Select(l => (l.Room, l.Kind, l.At.X, l.At.Y))), "the same lamps in the same places");
            }
            Assert.That(others, Is.GreaterThan(0), "the two floors hold braziers in other rooms (the treasure rooms' beside their chests)");
        }

        [Test]
        public void TheEntrancesFloor_IsTheCombatRoomsFloor()
        {
            var crypt = new CryptKitValues();
            Assert.That(crypt.EntranceFloorTint, Is.EqualTo(crypt.CombatFloorTint), "the old grey-box green taken off the entrance's floor");
        }
    }
}
