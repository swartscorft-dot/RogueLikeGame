using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.EditModeTests
{
    /// <summary>
    /// Light from the lamps (work order 085), its rules: the pause menu's four steps and their words; the three lamplit levels'
    /// values and their order (A lighter than B lighter than C, A darker than as before, the base never zero); and where the
    /// lamps stand (<see cref="LampPlan"/>): every thing that burns, the sconces only on far full walls toward their room and never
    /// in a doorway or a niche, spaced, and how much of the style room's floor (the nave of the room lab, floor 1, seed
    /// KCT7B24U) lies within a pool before and after the rule adds them.
    /// </summary>
    public class LamplitTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        public const string StyleSeed = "KCT7B24U";

        /// <summary>The style room: the nave in the room lab, ordinary, the mix "floor 1", seed KCT7B24U.</summary>
        public static LevelLayout StyleRoom(LevelValues v)
        {
            LabEntry nave = RoomLab.Entries(v).First(e => e.Kind == LabKind.Ordinary && e.Template != null && e.Template.Name == "nave");
            Assert.That(LevelSeed.TryParse(StyleSeed, out ulong seed), Is.True);
            return RoomLab.Build(nave, RoomLab.Mixes[0], seed, v);
        }

        [Test]
        public void TheSteps_AsBeforeFirst_ThenABC_RoundBothWays_AndAnythingElseIsAsBefore()
        {
            Assert.That(LightSteps.Count, Is.EqualTo(4));
            Assert.That(LightSteps.Names, Is.EqualTo(new[] { "as before", "lamplit A", "lamplit B", "lamplit C" }));
            Assert.That(LightSteps.Of(LightSteps.AsBefore), Is.Null, "as before has no lamplit values: nothing changes");
            Assert.That(LightSteps.Of(1).Name, Is.EqualTo("lamplit A"));
            Assert.That(LightSteps.Of(2).Name, Is.EqualTo("lamplit B"));
            Assert.That(LightSteps.Of(3).Name, Is.EqualTo("lamplit C"));
            Assert.That(LightSteps.Of(2), Is.SameAs(LightSteps.Of(2)), "one object a level, for the whole game");
            Assert.That(LightSteps.Next(0), Is.EqualTo(1));
            Assert.That(LightSteps.Next(3), Is.EqualTo(0), "round to as before after C");
            Assert.That(LightSteps.Previous(0), Is.EqualTo(3), "back from as before is C");
            Assert.That(LightSteps.Previous(1), Is.EqualTo(0));
            foreach (int bad in new[] { -1, 4, 99, int.MinValue }) Assert.That(LightSteps.Step(bad), Is.EqualTo(LightSteps.AsBefore), "stored " + bad);
        }

        [Test]
        public void TheLevels_ALighterThanBLighterThanC_ADarkerThanAsBefore_TheBaseNeverZero_HerLightAlwaysThere()
        {
            var before = new MoodValues();
            LamplitValues a = LightSteps.Of(1), b = LightSteps.Of(2), c = LightSteps.Of(3);
            Assert.That(a.AmbientLevel, Is.LessThan(before.AmbientLevel), "A is clearly darker than as before: the ambient");
            Assert.That(a.KeyLevel, Is.LessThan(before.KeyLevel), "and the key light");
            Assert.That(a.AmbientLevel, Is.GreaterThan(b.AmbientLevel));
            Assert.That(b.AmbientLevel, Is.GreaterThan(c.AmbientLevel));
            Assert.That(a.KeyLevel, Is.GreaterThan(b.KeyLevel));
            Assert.That(b.KeyLevel, Is.GreaterThan(c.KeyLevel));
            foreach (LamplitValues l in LightSteps.Presets)
            {
                Assert.That(l.AmbientLevel, Is.GreaterThan(0f), l.Name + ": the base light is never zero (his condition: never pitch black)");
                Assert.That(l.KeyLevel, Is.GreaterThan(0f), l.Name + ": the key light neither");
                Assert.That(l.HerLightIntensity, Is.GreaterThan(0f), l.Name + ": she has her own light");
                Assert.That(l.HerLightRange, Is.GreaterThan(2f), l.Name + ": reaching past her feet");
                Assert.That(l.EnemyLightFloor, Is.GreaterThanOrEqualTo(before.EnemyLightFloor), l.Name + ": an enemy is never drawn darker than as before allows");
                Assert.That(l.LampLevel, Is.GreaterThan(0f));
                Assert.That(l.FlashLevel, Is.GreaterThanOrEqualTo(1f), l.Name + ": her shots light at least as much as at level A");
            }
            Assert.That(c.LampLevel, Is.GreaterThanOrEqualTo(b.LampLevel), "the darker the level, the more the lamps carry");
            Assert.That(b.LampLevel, Is.GreaterThanOrEqualTo(a.LampLevel));
            Assert.That(c.FlashLevel, Is.GreaterThanOrEqualTo(b.FlashLevel));
            Assert.That(b.FlashLevel, Is.GreaterThanOrEqualTo(a.FlashLevel));
            Assert.That(c.EnemyLightFloor, Is.GreaterThanOrEqualTo(b.EnemyLightFloor));
            Assert.That(b.EnemyLightFloor, Is.GreaterThanOrEqualTo(a.EnemyLightFloor));
        }

        [Test]
        public void TheFlashes_BlastsLightMoreThanAShot_ShortLived_AndThePoolIsSmall()
        {
            var v = new LampValues();
            Assert.That(v.BlastLight, Is.GreaterThan(v.MuzzleLight), "a blast lights more than a single shot's muzzle");
            Assert.That(v.BlastLight, Is.GreaterThan(v.HitLight), "and more than a hit");
            Assert.That(v.BlastSeconds, Is.GreaterThan(v.MuzzleSeconds));
            foreach (float s in new[] { v.MuzzleSeconds, v.HitSeconds, v.BlastSeconds }) Assert.That(s, Is.InRange(0.02f, 0.4f), "a flash, not a lamp");
            Assert.That(v.MostFlashes, Is.InRange(4, 16), "pooled: a few lights, made once");
            Assert.That(v.BlastSparkSize, Is.GreaterThan(new LampValues().MuzzleSizeRef * 2f), "a hit's spark (about 0.8 m) is never taken for a blast's");
        }

        [Test]
        public void TheStyleRoom_TheNave_SaysHowMuchOfItsFloorIsInAPool_AndTheRuleBringsItToTheWantedShare()
        {
            var v = new LevelValues();
            LevelLayout l = StyleRoom(v);
            LevelRoom nave = l.Rooms[1];
            CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
            RoomMoodPlan mood = RoomMoodPlan.Make(l, v);
            var lamps = new LampValues();
            LampPlan plan = LampPlan.Make(l, kit, mood, lamps);
            TestContext.WriteLine("LAMPLIT style room: " + RoomLab.Describe(RoomLab.Entries(v).First(e => e.Template != null && e.Template.Name == "nave"), RoomLab.Mixes[0], l.Seed));
            TestContext.WriteLine("LAMPLIT " + plan.Describe(nave, "the nave (" + nave.Rect.Width * l.CellSize + " x " + nave.Rect.Height * l.CellSize + " m)"));
            TestContext.WriteLine("LAMPLIT " + plan.Describe(l.Rooms[0], "its passage"));
            foreach (Lamp lamp in plan.Of(nave.Order))
                TestContext.WriteLine(string.Format(Inv, "LAMPLIT   {0} at ({1:0.0}, {2:0.0}) m, {3:0.0} m up", lamp.Kind, lamp.At.X, lamp.At.Y, lamp.Height));
            Assert.That(plan.CoverBefore[nave.Order], Is.LessThan(lamps.WantedCover), "the nave has too few things that burn for its size (that is why the rule exists)");
            Assert.That(plan.CoverAfter[nave.Order], Is.GreaterThanOrEqualTo(lamps.WantedCover), "the sconces bring it to the wanted share");
            Assert.That(plan.Of(nave.Order).Count(x => x.Kind == LampKind.Sconce), Is.InRange(1, lamps.MostSconces));
            Assert.That(plan.LightsOf(nave.Order), Is.LessThan(plan.Of(nave.Order).Count()), "the eye-catcher's candles share a light");
            Assert.That(plan.LightsOf(nave.Order), Is.LessThanOrEqualTo(20), "a number of real lights Forward+ draws without dropping one");
        }

        [Test]
        public void CloseLamps_ShareALight_TheOneThatCarriesIsStronger_UpToItsMost()
        {
            var v = new LampValues();
            var a = new Lamp { Kind = LampKind.Candle };
            Assert.That(a.Intensity(v), Is.EqualTo(v.CandleLight).Within(1e-6));
            a.Carries = 2;
            Assert.That(a.Intensity(v), Is.EqualTo(v.CandleLight * (1f + 2f * v.JoinShare)).Within(1e-6));
            a.Carries = 50;
            Assert.That(a.Intensity(v), Is.EqualTo(v.CandleLight * v.JoinMost).Within(1e-6), "never past its most");
            Assert.That(new Lamp { Kind = LampKind.Candle, CarriedBy = a }.Intensity(v), Is.EqualTo(0f), "a carried lamp has no light of its own");
        }

        [Test]
        public void TheSconces_OnlyOnFarFullWallsTowardTheirRoom_NeverByADoorOrInANicheOrUnderALantern_Spaced_InManyFloors()
        {
            var v = new LevelValues();
            var lamps = new LampValues();
            int sconces = 0, rooms = 0, covered = 0, wanting = 0;
            foreach (ulong seed in new[] { 42UL, 7UL, 1234UL, 0x085085UL })
                foreach (int floor in new[] { 1, 3, 5 })
                {
                    LevelLayout l = LevelGenerator.Generate(seed, floor, v);
                    CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                    LampPlan plan = LampPlan.Make(l, kit, RoomMoodPlan.Make(l, v), lamps);
                    string at = "seed " + seed + " floor " + floor;
                    foreach (Lamp lamp in plan.Lamps)
                    {
                        if (lamp.Kind != LampKind.Sconce) continue;
                        sconces++;
                        CryptKitSection s = kit.Sections[lamp.Section];
                        Assert.That(s.Low, Is.False, at + ": never on a low near wall");
                        Assert.That(s.Niche, Is.False, at + ": never in a niche");
                        Assert.That(s.AtDoor, Is.False, at + ": never beside a door");
                        Assert.That(CaveWalls.IsFar(s, CaveWalls.GameYaw), Is.True, at + ": only on a far wall");
                        Assert.That(s.RoomOn(lamp.Side), Is.EqualTo(lamp.Room), at + ": on its room's side");
                        Assert.That(kit.Lanterns.Any(x => x.Section == lamp.Section && x.Side == lamp.Side), Is.False, at + ": never where a lantern hangs");
                        foreach (Lamp other in plan.Of(lamp.Room))
                            if (other != lamp) Assert.That(other.At.DistanceTo(lamp.At), Is.GreaterThanOrEqualTo(lamps.SconceSpacing - 1e-9), at + ": spaced from every other lamp of its room");
                    }
                    foreach (LevelRoom room in l.Rooms)
                    {
                        rooms++;
                        Assert.That(plan.CoverAfter[room.Order], Is.GreaterThanOrEqualTo(plan.CoverBefore[room.Order]), at + ": the rule only adds");
                        Assert.That(plan.Of(room.Order).Count(x => x.Kind == LampKind.Sconce), Is.LessThanOrEqualTo(lamps.MostSconces));
                        if (plan.CoverBefore[room.Order] < lamps.WantedCover) wanting++;
                        if (plan.CoverAfter[room.Order] >= lamps.WantedCover) covered++;
                    }
                }
            Assert.That(sconces, Is.GreaterThan(0));
            TestContext.WriteLine(string.Format(Inv, "LAMPLIT 4 seeds x 3 floors: {0} rooms and passages, {1} short of {2:0} % of their floor in a pool with the things that burn alone; with {3} sconces added, {4} reach it",
                                                rooms, wanting, 100.0 * lamps.WantedCover, sconces, covered));
        }

        [Test]
        public void TheLamps_AreEveryThingThatBurns_EverySourceOfTheMood_EveryLantern_AChestLightUnlessTheGoldHasOne()
        {
            var v = new LevelValues();
            foreach (ulong seed in new[] { 42UL, 99UL })
            {
                LevelLayout l = LevelGenerator.Generate(seed, 3, v);
                CryptKitPlan kit = CryptKitPlan.Make(l, v.Crypt);
                RoomMoodPlan mood = RoomMoodPlan.Make(l, v);
                LampPlan plan = LampPlan.Make(l, kit, mood, new LampValues());
                int sources = mood.Rooms.Sum(r => r.Sources.Count);
                Assert.That(plan.Lamps.Count(x => x.Source >= 0), Is.EqualTo(sources), "every source of the mood");
                Assert.That(plan.Lamps.Count(x => x.Kind == LampKind.Lantern), Is.EqualTo(kit.Lanterns.Count), "every lantern of the kit");
                int chests = l.Rooms.Sum(r => r.Props.Count(p => p.Model == PropModel.Chest));
                int gold = plan.Lamps.Count(x => x.Kind == LampKind.Gold);
                Assert.That(plan.Lamps.Count(x => x.Kind == LampKind.Chest), Is.InRange(chests - gold, chests), "a light over every chest the gold does not light already");
                Assert.That(LampPlan.Make(l, kit, mood, new LampValues()).Lamps.Count, Is.EqualTo(plan.Lamps.Count), "the same plan from the same floor");
            }
        }
    }
}
