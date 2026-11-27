using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Rooms you can use (work order 088; Kristian, 2026-11-22: "Døra stenges bak deg, men vi bør balansere rommene med flere
    /// muligheter og, slik at man kan bruke miljøet til sin fordel", and "Plasser dekning for den"): the one sorted table of what a
    /// prop does against a shot (<see cref="PropHeights"/>); the joined floor (<see cref="JoinedFloor"/>) on a hand-made room, on
    /// every template of the library in the room lab, and on the floors of both template sets; each cover rule as the rules state
    /// it (<see cref="FightCover"/>) on a hand-made room and over the floors; the room still hers (the door's straight run, the
    /// south wall, how much is added); version 18 behind the switch, field for field, and the enemies the same either way; the
    /// lab's rooms given their cover as a floor's are; and the session summary's cover in each room's fight. The measures for the
    /// report are the explicit tests at the end (a thousand seeds a floor, before and after).
    /// </summary>
    public class UsableRoomsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static LevelValues On => new LevelValues { ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, LineUps = false }; // work order 103: version 19's floors
        static LevelValues Off => new LevelValues { UsableRooms = false, ExitRoomsOfTheirOwn = false, FloorCharacter = false, Lancers = false, RangedAroundTheRoom = false, BossAlone = false, LineUps = false }; // 103: version 18's
        static LevelValues SecondSet => new LevelValues { MoreTemplates = true };

        static ulong Mix(ulong h, ulong v)
        {
            for (int i = 0; i < 8; i++)
            {
                h ^= (v >> (8 * i)) & 0xFF;
                h *= 1099511628211UL;
            }
            return h;
        }

        // ---------------- The one table (point 5) ----------------

        [Test]
        public void TheHeights_AreOneSortedTable_EveryModelOnce_LowestFirst_AsTheDuckSortsThem()
        {
            var seen = new HashSet<PropModel>();
            double last = double.NegativeInfinity;
            var lines = new List<string> { "HEIGHTS model  height  what it does  why" };
            foreach (PropHeights.Row r in PropHeights.Table)
            {
                Assert.That(seen.Add(r.Model), Is.True, r.Model + " once");
                Assert.That(r.Height, Is.GreaterThanOrEqualTo(last - 1e-9), r.Model + " in the order of its height");
                last = r.Height;
                Assert.That(r.Height, Is.EqualTo(PropSize.Of(r.Model).Height), r.Model + "'s height is its table entry's: no size changed");
                Assert.That(r.Sort, Is.EqualTo(PropSort.Of(r.Model)), r.Model + ": the duck's sort (QuickDuckRules' PropSort) and the table agree");
                Assert.That(PropHeights.Of(r.Model), Is.EqualTo(r.Sort));
                if (PropSize.Of(r.Model).Slender || PropSize.Of(r.Model).Walkable) Assert.That(r.Sort, Is.EqualTo(PropHeightClass.Slender), r.Model + ": slender or walked over stops nothing");
                lines.Add(string.Format(Inv, "HEIGHTS {0,-14} {1,5:0.00} m  {2,-7}  {3}", r.Model, r.Height, r.Sort == PropHeightClass.Slender ? "neither" : r.Sort.ToString().ToLowerInvariant(), r.Why));
            }
            Assert.That(seen.Count, Is.EqualTo(Enum.GetValues(typeof(PropModel)).Length), "every model");
            TestContext.WriteLine(string.Join("\n", lines));
        }

        [Test]
        public void TheTable_SaysWhatStopsShots_WithItOn_AndTheMorningsRuleWithItOff()
        {
            var on = new PropValues();
            var off = new PropValues { OneHeightTable = false };
            Assert.That(on.OneHeightTable, Is.True, "on by default (work order 088)");
            foreach (PropModel m in Enum.GetValues(typeof(PropModel)))
            {
                LevelProp p = LevelProp.Make(m, new LevelPoint(0, 0), 0, PropPlace.Floor);
                Assert.That(PropHeights.StopsShots(p, on), Is.EqualTo(PropHeights.Of(m) == PropHeightClass.Tall), m + " with the table");
                Assert.That(PropHeights.StopsShots(p, off), Is.EqualTo(p.IsCover(off)), m + " with the morning's rule");
                // With the quick duck on (the default) the two give the same floor: the morning's cover less the three the duck sorts low.
                bool morningWithTheDuck = p.IsCover(off) && !PropSort.LowerUnderTheDuck(p, off);
                Assert.That(PropHeights.StopsShots(p, on), Is.EqualTo(morningWithTheDuck), m + ": with the duck on, the morning's floor");
            }
        }

        [Test]
        public void TheLookAndTheRule_Disagree_ForTheFourPropsBetweenTheLines()
        {
            List<PropHeights.Row> rows = PropHeights.Disagreeing();
            TestContext.WriteLine("DISAGREE " + string.Join("; ", rows.Select(r => string.Format(Inv, "{0} {1:0.00} m, {2}: {3}", r.Model, r.Height, r.Sort.ToString().ToLowerInvariant(), r.Why))));
            Assert.That(rows.Select(r => r.Model), Is.EquivalentTo(new[] { PropModel.PillarBroken, PropModel.Altar, PropModel.GraveMarker, PropModel.GreatBrazier }));
        }

        // ---------------- A hand-made room ----------------

        /// <summary>
        /// An entrance (3 x 3 at 11,7) and a combat room 6 x 8 cells (24 x 32 m, x 40 to 64, y 40 to 72) north of it, a door at
        /// cell 12 of their shared wall (x 48 to 52): she comes in from the south; the line's middle is at (50, 45).
        /// </summary>
        static LevelLayout HandMade(out LevelRoom room)
        {
            var level = new LevelLayout { Seed = 1, Version = LevelGenerator.Version, GridCells = 48, CellSize = 4f, Attempt = 1, Floor = 1, InALine = true, UsableRooms = true };
            level.Rooms.Add(new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(11, 7, 3, 3), Order = 0, PlayerStart = new LevelPoint(50, 34) });
            room = new LevelRoom { Kind = RoomKind.Combat, Rect = new CellRect(10, 10, 6, 8), Order = 1 };
            level.Rooms.Add(room);
            level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = 10, From = 12, Width = 1 });
            return level;
        }

        static void Shooter(LevelRoom room, LevelPoint at)
        {
            room.EnemyPoints.Add(at);
            room.EnemyGroups.Add(new EnemyGroup(EnemyKind.Shooter));
            room.EnemyPlaces.Add(new[] { at });
        }

        [Test]
        public void TheFight_IsFramedFromTheDoorIn_TheLineFiveMetresIn_TheFarBandAtSixTenths()
        {
            LevelLayout level = HandMade(out LevelRoom room);
            Shooter(room, new LevelPoint(50, 66));
            FightFrame f = FightCover.FrameOf(level, room, On);
            Assert.That(f, Is.Not.Null);
            Assert.That(f.LineDepth, Is.EqualTo(5).Within(1e-9));
            Assert.That(f.Depth, Is.EqualTo(32).Within(1e-9));
            Assert.That(f.FarDepth, Is.EqualTo(32 * 0.6).Within(1e-4));
            Assert.That(f.LineMiddle.X, Is.EqualTo(50).Within(1e-9));
            Assert.That(f.LineMiddle.Y, Is.EqualTo(45).Within(1e-9));
            Assert.That(f.Shooters.Count, Is.EqualTo(1));
            Assert.That(FightCover.FrameOf(level, level.Rooms[0], On), Is.Null, "the entrance has no fight");
        }

        [Test]
        public void ATallCover_HidesHer_FromAShooter_WhenTheSpotBehindItIsFree_AndALowOneNever()
        {
            LevelLayout level = HandMade(out LevelRoom room);
            var q = new LevelPoint(50, 66);
            Shooter(room, q);
            LevelProp pillar = LevelProp.Make(PropModel.Pillar, new LevelPoint(50, 55), 0, PropPlace.Floor);
            room.Props.Add(pillar);
            Assert.That(FightCover.Hides(level, room, FightCover.PropThing(pillar), q, On), Is.True, "her two sides behind a 1.2 m pillar");
            // A sarcophagus on the spot behind it: nowhere to stand there.
            room.Props.Add(LevelProp.Make(PropModel.Sarcophagus, new LevelPoint(50, 53.4), 0, PropPlace.Floor));
            Assert.That(FightCover.Hides(level, room, FightCover.PropThing(pillar), q, On), Is.False, "the spot behind it is taken");
            Assert.That(FightCover.IsTall(pillar), Is.True);
            Assert.That(FightCover.IsTall(LevelProp.Make(PropModel.PillarBroken, new LevelPoint(0, 0), 0, PropPlace.Floor)), Is.False, "the broken pillar is low");
            Assert.That(FightCover.IsTall(LevelProp.Make(PropModel.Candelabra, new LevelPoint(0, 0), 0, PropPlace.Floor)), Is.False, "the candelabra is slender");
        }

        [Test]
        public void TheChain_TwoTallCovers_TheFirstWithinEightMetresOfTheLine_TheNextWithinTen_ThreeApart_NearerTheShooter()
        {
            LevelLayout level = HandMade(out LevelRoom room);
            Shooter(room, new LevelPoint(50, 66));
            FightFrame f = FightCover.FrameOf(level, room, On);
            Assert.That(FightCover.ChainHolds(level, f, On), Is.False, "an empty room");
            LevelProp a = LevelProp.Make(PropModel.Pillar, new LevelPoint(50, 52), 0, PropPlace.Floor); // 7 m from the line's middle
            room.Props.Add(a);
            Assert.That(FightCover.ChainHolds(level, f, On), Is.False, "one is not a chain");
            LevelProp b = LevelProp.Make(PropModel.Pillar, new LevelPoint(53, 59), 0, PropPlace.Floor); // 7.6 m on, nearer, depth 19 (far band 19.2)
            room.Props.Add(b);
            Assert.That(FightCover.ChainHolds(level, f, On), Is.True, "two, from the line to the far band");
            // The first too far from the line: no chain.
            a.Position = new LevelPoint(50, 53.5);
            Assert.That(a.Position.DistanceTo(f.LineMiddle), Is.GreaterThan(8));
            Assert.That(FightCover.ChainHolds(level, f, On), Is.False, "the first more than 8 m from the line's middle");
            // A second beside the first (under 3 m apart) is no step on.
            a.Position = new LevelPoint(50, 52);
            b.Position = new LevelPoint(52, 53.5);
            Assert.That(FightCover.ChainHolds(level, f, On), Is.False, "the next under 3 m from the one before");
            // A next more than 10 m on.
            room.Props.Remove(b);
            LevelProp c = LevelProp.Make(PropModel.Pillar, new LevelPoint(58, 59), 0, PropPlace.Floor);
            room.Props.Add(c);
            Assert.That(c.Position.DistanceTo(a.Position), Is.GreaterThan(10));
            Assert.That(FightCover.ChainHolds(level, f, On), Is.False, "the next more than 10 m from the one before");
            // A room with no ranged enemy needs none.
            room.EnemyPoints.Clear();
            room.EnemyGroups.Clear();
            room.EnemyPlaces.Clear();
            Assert.That(FightCover.ChainHolds(level, FightCover.FrameOf(level, room, On), On), Is.True);
        }

        [Test]
        public void ALowProp_ServesTheDuck_OnTheShootersLine_TurnedAcross_SevenMetresOrMoreFromIt_WithNothingTallBetween()
        {
            LevelLayout level = HandMade(out LevelRoom room);
            var q = new LevelPoint(50, 66);
            Shooter(room, q);
            FightFrame f = FightCover.FrameOf(level, room, On);
            LevelProp low = LevelProp.Make(PropModel.Sarcophagus, new LevelPoint(50, 56), 0, PropPlace.Floor); // its long side along x: across the line
            room.Props.Add(low);
            Assert.That(FightCover.LowServes(level, f, low, q, On), Is.True, "her spot 0.75 m behind it, 11.3 m from the shooter");
            Assert.That(new CoverValues().DuckSpot, Is.LessThanOrEqualTo(new FeelValues().DuckReach), "her spot within the duck's reach");
            low.Rotation = 90;
            Assert.That(FightCover.LowServes(level, f, low, q, On), Is.False, "turned along the line");
            low.Rotation = 0;
            room.Props.Add(LevelProp.Make(PropModel.Pillar, new LevelPoint(50, 61), 0, PropPlace.Floor));
            Assert.That(FightCover.LowServes(level, f, low, q, On), Is.False, "a pillar between: no arrow comes that way");
            room.Props.RemoveAt(room.Props.Count - 1);
            low.Position = new LevelPoint(50, 61);
            Assert.That(FightCover.LowServes(level, f, low, q, On), Is.False, "her spot nearer the shooter than 7 m");
            low.Position = new LevelPoint(50, 46);
            Assert.That(FightCover.LowServes(level, f, low, q, On), Is.False, "her spot short of the fight's line");
            Assert.That(FightCover.LowServes(level, f, LevelProp.Make(PropModel.Pillar, new LevelPoint(50, 56), 0, PropPlace.Floor), q, On), Is.False, "a tall prop is no duck");
        }

        [Test]
        public void TheJoinedFloor_RefusesAPropAcrossABaysMouth_AndKeepsOneInTheOpen()
        {
            // The north row of the room: solid cells at x 12 and 14, so the cell 13 between them is a bay 4 m wide open to the south.
            LevelLayout level = HandMade(out LevelRoom room);
            room.SetSolid(12, 17, true);
            room.SetSolid(14, 17, true);
            var floor = new JoinedFloor(level, room, On);
            Assert.That(floor.Fault(), Is.Null, "the bare room");
            LevelProp across = LevelProp.Make(PropModel.Coffin, new LevelPoint(54, 68), 0, PropPlace.Floor); // 2.15 m across the 4 m mouth
            Assert.That(floor.Keeps(across), Is.False, "it shuts the bay off: gaps under 1.6 m either side");
            LevelProp open = LevelProp.Make(PropModel.Coffin, new LevelPoint(50, 56), 0, PropPlace.Floor);
            Assert.That(floor.Keeps(open), Is.True, "in the open floor");
            Assert.That(JoinedFloor.FaultOf(level, room, On, new[] { across }), Does.Contain("shut off"));
            Assert.That(JoinedFloor.FaultOf(level, room, On, new[] { open }), Is.Null);
            // The way of whole cells from the door to the middle (a room with one door): a pillar on the middle of the cell inside
            // the door closes it, there being no other cell to step on from the door.
            LevelProp onTheWay = LevelProp.Make(PropModel.Pillar, new LevelPoint(50, 42), 0, PropPlace.Floor);
            Assert.That(JoinedFloor.FaultOf(level, room, On, new[] { onTheWay }), Does.Contain("no way of whole cells"));
        }

        // ---------------- The joined floor over the library and the floors (point 1) ----------------

        /// <summary>
        /// Every template of the library, both sets (as the room lab offers them), as a lab room of every mix and four seeds: built by
        /// the generator's own stages (<see cref="LevelGenerator.LabRoom"/>, which tries the next attempt for a room that breaks a
        /// rule), and no prop shuts floor off or closes the way through (the lab's check asks it, and it is asked here again).
        /// </summary>
        [Test]
        public void EveryTemplateOfTheLibrary_BothSets_AsALabRoom_KeepsTheJoinedFloor()
        {
            int rooms = 0;
            var v = SecondSet;
            foreach (LabEntry e in RoomLab.Entries(v))
                foreach (LabMix mix in RoomLab.Mixes)
                    for (ulong s = 1; s <= 4; s++)
                    {
                        LevelLayout l = RoomLab.Build(e, mix, s * 7919UL, v);
                        LevelRoom room = l.Rooms[1];
                        Assert.That(l.UsableRooms, Is.True);
                        Assert.That(JoinedFloor.FaultOf(l, room, v), Is.Null, (e.Template?.Name ?? RoomLab.Word(e.Kind)) + " " + mix + " seed " + s);
                        rooms++;
                    }
            TestContext.WriteLine("JOINEDLAB " + rooms + " lab rooms of " + RoomLab.Entries(v).Count + " entries, every one joined");
        }

        static IEnumerable<LevelLayout> Floors(int seedsAFloor, LevelValues v, ulong salt = 0)
        {
            for (int floor = 1; floor <= 5; floor++)
                for (ulong s = 0; s < (ulong)seedsAFloor; s++)
                    yield return LevelGenerator.Generate(s * 6151UL + salt, floor, v);
        }

        /// <summary>
        /// A hundred seeds a floor with each template set: every floor is made (the validator asks the joined floor of every room),
        /// every room's floor joined again here, and every template of the set seen stamped.
        /// </summary>
        [TestCase(false)]
        [TestCase(true)]
        public void AHundredSeedsAFloor_EveryRoomsFloorIsJoined(bool secondSet)
        {
            LevelValues v = secondSet ? SecondSet : On;
            var seen = new HashSet<string>();
            int rooms = 0, moved = 0, dropped = 0;
            foreach (LevelLayout l in Floors(100, v))
            {
                Assert.That(l.UsableRooms, Is.True);
                foreach (LevelRoom r in l.Rooms)
                {
                    if (!JoinedFloor.Asks(r)) continue;
                    Assert.That(JoinedFloor.FaultOf(l, r, v), Is.Null, l.SeedText + " floor " + l.Floor + " " + r.Letter + r.Order);
                    rooms++;
                    moved += r.PropsMovedForFloor;
                    dropped += r.PropsDroppedForFloor;
                    if (r.Template != null && r.Shape == RoomShape.Template) seen.Add(r.Template.Template.Name);
                }
            }
            // Work order 089 (changed deliberately): not the third set, off in these floors (ExitRoomsTests asks its rooms the same).
            var wanted = RoomTemplateLibrary.All.Where(t => t.Since != RoomTemplateLibrary.ExitSince && (secondSet || t.Since < RoomTemplateLibrary.MoreSince)).Select(t => t.Name).ToList();
            TestContext.WriteLine($"JOINED {(secondSet ? "both sets" : "the first set")}: {rooms} rooms joined, {moved} props moved and {dropped} dropped for it; templates seen {seen.Count} of {wanted.Count}");
            Assert.That(wanted.Except(seen), Is.Empty, "every template of the set stamped on these floors");
        }

        // ---------------- The cover over the floors (points 2, 3 and 4) ----------------

        /// <summary>The share of fight rooms the chain and the low rule hold in, over a hundred seeds a floor (the measure below gives a thousand).</summary>
        [Test]
        public void AHundredSeedsAFloor_TheCoverRulesHold_InNearlyEveryRoom_AndNothingIsAddedWhereNothingIsAsked()
        {
            int ranged = 0, chain = 0, shooters = 0, low = 0, castersOnly = 0;
            foreach (LevelLayout l in Floors(100, On, 17))
                foreach (LevelRoom r in l.Rooms)
                {
                    FightFrame f = FightCover.FrameOf(l, r, On);
                    if (f == null || !f.HasRanged)
                    {
                        Assert.That(r.CoverTallAdded + r.CoverLowAdded, Is.EqualTo(0), "no cover is added where no ranged enemy stands");
                        Assert.That(r.Props.Count(p => p.Fight), Is.EqualTo(0));
                        continue;
                    }
                    ranged++;
                    if (FightCover.ChainHolds(l, f, On)) chain++;
                    if (f.HasShooters)
                    {
                        shooters++;
                        if (FightCover.LowHolds(l, f, On)) low++;
                    }
                    else
                    {
                        castersOnly++;
                        Assert.That(r.CoverLowAdded, Is.EqualTo(0), "a room with casters and no shooters gets no low cover: a low prop does not stop an orb");
                    }
                    Assert.That(r.CoverTallAdded, Is.LessThanOrEqualTo(On.CoverRules.TallAddMost));
                    Assert.That(r.CoverLowAdded, Is.LessThanOrEqualTo(On.CoverRules.LowAddMost));
                    Assert.That(r.Props.Count(p => p.Fight), Is.EqualTo(r.CoverTallAdded + r.CoverLowAdded));
                }
            double chainShare = chain / (double)ranged, lowShare = low / (double)Math.Max(1, shooters);
            TestContext.WriteLine(string.Format(Inv, "COVERHOLDS {0} rooms with ranged enemies ({1} with shooters, {2} casters only): the chain holds in {3:P1}, the low cover in {4:P1} of those with shooters",
                                                ranged, shooters, castersOnly, chainShare, lowShare));
            // Held to about the range the measure below found on .NET, with a little room.
            Assert.That(chainShare, Is.GreaterThanOrEqualTo(0.95), "the chain of tall cover");
            Assert.That(lowShare, Is.GreaterThanOrEqualTo(0.93), "the low cover for the duck");
        }

        /// <summary>
        /// Still her room (point 4): every added cover is a set piece of its theme's cover model, out of every door's straight run
        /// (6 m in) and, a tall one, 2.5 m north of the low south wall; no room holds more than its theme's most and five besides.
        /// The lane and the walkable share are the validator's, which made every floor.
        /// </summary>
        [Test]
        public void TheAddedCover_KeepsTheDoorsStraightRun_TheSouthWall_AndTheRoomsMost()
        {
            var sv = new ShapeValues();
            foreach (LevelLayout l in Floors(40, On, 29))
                foreach (LevelRoom r in l.Rooms)
                {
                    Assert.That(r.Props.Count, Is.LessThanOrEqualTo(PropRules.MostIn(r, On)));
                    foreach (LevelProp p in r.Props.Where(p => p.Fight))
                    {
                        Assert.That(p.Place, Is.EqualTo(PropPlace.Set), "a set piece of the fight");
                        Assert.That(FightCover.IsTall(p) || FightCover.IsLow(p), Is.True);
                        Assert.That(p.Model, Is.EqualTo(FightCover.IsTall(p) ? FightCover.TallModel(r.Theme) : FightCover.LowModel(r.Theme)), "its theme's cover");
                        foreach (LevelDoor d in l.DoorsOf(r.Order))
                        {
                            int wall = LevelGeometry.WallOf(r.Rect, d);
                            if (wall < 0) continue;
                            d.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
                            LevelPoint into = PropRules.Inward(wall);
                            // The straight run: its opening swept 6 m in.
                            for (double t = 0.05; t < 1; t += 0.1)
                                for (double k = 0; k <= sv.DoorStraightIn; k += 0.25)
                                {
                                    var c = new LevelPoint(a.X + (b.X - a.X) * t + into.X * k, a.Y + (b.Y - a.Y) * t + into.Y * k);
                                    Assert.That(p.Footprint.DistanceTo(c), Is.GreaterThan(0), "out of the straight run behind door " + d.RoomA + "-" + d.RoomB);
                                }
                        }
                        if (FightCover.IsTall(p))
                            Assert.That(p.Footprint.DistanceToSide(r.Rect, l.CellSize, LevelGenerator.South), Is.GreaterThanOrEqualTo(On.CoverRules.SouthClear - 1e-9), "north of the low south wall");
                    }
                }
        }

        // ---------------- Version 18 behind the switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_AndTheGeneratorIsVersion19()
        {
            Assert.That(new LevelValues().UsableRooms, Is.True, "on by default (work order 088)");
            Assert.That(LevelGenerator.Version, Is.EqualTo(25)); // work order 103: 20; 104: 21; 118: 22; 124: 23; 130: 24; 135: 25
            LevelLayout off = LevelGenerator.Generate(42UL, 3, Off);
            Assert.That(off.UsableRooms, Is.False);
            Assert.That(off.Rooms.SelectMany(r => r.Props).Any(p => p.Fight), Is.False, "off: no cover added");
            Assert.That(off.Rooms.Sum(r => r.PropsMovedForFloor + r.PropsDroppedForFloor), Is.EqualTo(0), "off: no prop moved for the joined floor");
        }

        /// <summary>
        /// Two thousand floors (seeds s * 7919 + 13, floor numbers 1 to 5 in turn) with the switch off, folded as the earlier versions'
        /// (<see cref="FloorPrint"/>, each room's shape and solid cells, and its template). Each runtime has its own number (Unity's
        /// Math folds the doubles otherwise); both were taken from version 18's own code (feca7b3, before this order) with its
        /// defaults: Unity's from a filtered Unity EditMode run of this test on that code, .NET's from the .NET rules run of it.
        /// </summary>
        [Test]
        public void SwitchOff_TwoThousandFloors_AreVersion18s_FieldForField()
        {
            ulong h = 14695981039346656037UL;
            for (ulong s = 0; s < 2000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s * 7919UL + 13UL, (int)(s % 5) + 1, Off);
                h = FloorPrint.Of(level, h);
                foreach (LevelRoom r in level.Rooms)
                {
                    h = Mix(h, (ulong)r.Shape);
                    h = Mix(h, (ulong)r.SolidCount);
                    foreach ((int x, int y) in r.SolidList) h = Mix(h, (ulong)(x * 64 + y));
                    foreach (char ch in r.Template?.ToString() ?? "-") h = Mix(h, ch);
                }
            }
#if UNITY_5_3_OR_NEWER
            const ulong pinned = 0x827C8EC8B365DF6CUL; // Unity's EditMode run of version 18's code (feca7b3), its defaults, before this order
#else
            const ulong pinned = 0x352E60103E4AC34EUL; // .NET, version 18's code (feca7b3), its defaults, before this order
#endif
            string found = "0x" + h.ToString("X16") + "UL";
            TestContext.WriteLine("USABLEOFFPRINT " + found);
            Assert.That(h, Is.EqualTo(pinned), "the two thousand floors of version 18, found " + found);
        }

        /// <summary>The enemies, the rooms, the doors, the shapes and the themes of every floor are version 18's with the switch on: only the props differ.</summary>
        [Test]
        public void TheEnemiesAndEverythingButTheProps_AreVersion18s_WithTheSwitchOn()
        {
            foreach (int floor in new[] { 1, 2, 3, 4, 5 })
                for (ulong s = 0; s < 60; s++)
                {
                    LevelLayout on = LevelGenerator.Generate(s * 104729UL + 5UL, floor, On), off = LevelGenerator.Generate(s * 104729UL + 5UL, floor, Off);
                    Assert.That(on.Describe(props: false), Is.EqualTo(off.Describe(props: false)), "seed " + on.SeedText + " floor " + floor);
                    Assert.That(on.Rooms.Select(r => r.Theme), Is.EqualTo(off.Rooms.Select(r => r.Theme)), "the themes");
                    Assert.That(on.Rooms.Select(r => r.EnemyCount), Is.EqualTo(off.Rooms.Select(r => r.EnemyCount)), "the count of every room's enemies");
                }
        }

        // ---------------- The room lab ----------------

        /// <summary>The lab's rooms are given their cover by the props stage as a floor's rooms are: the same rules hold in about as many.</summary>
        [Test]
        public void TheLabsRooms_AreGivenTheirCoverAsAFloorsRoomsAre()
        {
            int ranged = 0, chain = 0, shooters = 0, low = 0, added = 0;
            var v = On;
            foreach (LabEntry e in RoomLab.Entries(v).Where(x => x.Kind != LabKind.Treasure))
                foreach (LabMix mix in RoomLab.Mixes)
                    for (ulong s = 1; s <= 3; s++)
                    {
                        LevelLayout l = RoomLab.Build(e, mix, s * 31UL, v);
                        LevelRoom room = l.Rooms[1];
                        added += room.CoverTallAdded + room.CoverLowAdded;
                        FightFrame f = FightCover.FrameOf(l, room, v);
                        if (f == null || !f.HasRanged) continue;
                        ranged++;
                        if (FightCover.ChainHolds(l, f, v)) chain++;
                        if (!f.HasShooters) continue;
                        shooters++;
                        if (FightCover.LowHolds(l, f, v)) low++;
                    }
            TestContext.WriteLine(string.Format(Inv, "COVERLAB {0} lab rooms with ranged enemies: the chain holds in {1:P1}, the low cover in {2:P1} of {3} with shooters; {4} covers added",
                                                ranged, chain / (double)Math.Max(1, ranged), low / (double)Math.Max(1, shooters), shooters, added));
            Assert.That(added, Is.GreaterThan(0), "the lab's rooms are given cover");
            Assert.That(chain / (double)Math.Max(1, ranged), Is.GreaterThanOrEqualTo(0.85));
            Assert.That(low / (double)Math.Max(1, shooters), Is.GreaterThanOrEqualTo(0.85));
        }

        // ---------------- The session summary ----------------

        static SessionEvent E(double t, string type, string name = null, string result = null, params string[] extras)
        {
            var e = new SessionEvent(t, type, name) { Result = result };
            for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
            return e;
        }

        [Test]
        public void TheSummary_GivesEachRoomsFight_ItsTallAndLowCovers_AndWhatWasStoppedInIt()
        {
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.SessionStart),
                E(1, SessionEventTypes.RunStart, "RUNSEED1"),
                E(2, SessionEventTypes.FloorEnter, "SEED", null, "floor", "1"),
                E(2, FightCoverEvents.Room, null, null, "floor", "1", "order", "2", "tall", "3", "low", "2"),
                E(2, FightCoverEvents.Room, null, null, "floor", "1", "order", "4", "tall", "2", "low", "0"),
                E(5, SessionEventTypes.RoomFight, "point", null, "floor", "1", "room", "1", "order", "2", "kind", "combat", "inside", "true", "woke", "9"),
                E(6, SessionEventTypes.EnemyStrike, "Arrow", SessionWords.StoppedByCover),
                E(7, SessionEventTypes.EnemyStrike, "Arrow", SessionWords.StoppedByCover),
                E(8, SessionEventTypes.EnemyStrike, "Arrow", SessionWords.StoppedByWall),
                E(9, SessionEventTypes.RoomFight, "seen", null, "floor", "1", "room", "2", "order", "4", "kind", "elite", "inside", "false", "woke", "11"),
                E(10, SessionEventTypes.EnemyStrike, "Arrow", SessionWords.StoppedByWall),
                E(11, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs.Count, Is.EqualTo(1));
            List<FightCoverRow> rows = stats.Runs[0].FightCover;
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That((rows[0].Tall, rows[0].Low, rows[0].StoppedDucked, rows[0].StoppedStanding), Is.EqualTo((3, 2, 2, 1)));
            Assert.That((rows[1].Tall, rows[1].Low, rows[1].StoppedDucked, rows[1].StoppedStanding), Is.EqualTo((2, 0, 0, 1)));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("cover in fights  floor 1 room 1 (combat): 3 tall, 2 low, stopped 2 ducked, 1 by a wall or tall cover;  floor 1 room 2 (elite): 2 tall, 0 low, stopped 0 ducked, 1 by a wall or tall cover", text);
            // A log from before: no room_cover events, no line.
            events.RemoveAll(e => e.Type == FightCoverEvents.Room);
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events)), Does.Not.Contain("cover in fights"));
        }

        // ---------------- The measures for the report ----------------

        sealed class Tally
        {
            public int Rooms, Ranged, Chain, Shooters, Low, Moved, Dropped, RoomsMovedOrDropped, TallAdded, LowAdded;
            public int TallMost = int.MinValue, TallFewest = int.MaxValue, LowMost = int.MinValue, LowFewest = int.MaxValue;
            public long TallSum, LowSum;
            public double Open;

            public void Add(int tall, int low)
            {
                Rooms++;
                TallSum += tall;
                LowSum += low;
                TallMost = Math.Max(TallMost, tall);
                TallFewest = Math.Min(TallFewest, tall);
                LowMost = Math.Max(LowMost, low);
                LowFewest = Math.Min(LowFewest, low);
            }

            public string Line(string name) => Rooms == 0 ? name + ": -" : string.Format(Inv,
                "{0,-22} {1,5} rooms  tall {2,4:0.0} ({3}-{4})  low {5,4:0.0} ({6}-{7})  chain {8,6:P1}  low rule {9,6:P1}  open {10,6:P1}  moved/dropped in {11,6:P1} ({12} moved, {13} dropped)  added {14} tall, {15} low",
                name, Rooms, TallSum / (double)Rooms, TallFewest, TallMost, LowSum / (double)Rooms, LowFewest, LowMost,
                Chain / (double)Math.Max(1, Ranged), Low / (double)Math.Max(1, Shooters), Open / Rooms, RoomsMovedOrDropped / (double)Rooms, Moved, Dropped, TallAdded, LowAdded);
        }

        static void Count(LevelValues v, int seeds, Dictionary<string, Tally> by)
        {
            PropValues pv = v.PropRules;
            foreach (LevelLayout l in Floors(seeds, v))
                foreach (LevelRoom r in l.Rooms)
                {
                    if (r.Kind != RoomKind.Combat && r.Kind != RoomKind.Exit) continue;
                    string name = r.Template != null && r.Shape == RoomShape.Template ? r.Template.Template.Name : "drawn: " + RoomShapes.Word(r.Shape);
                    FightFrame f = FightCover.FrameOf(l, r, v);
                    bool ranged = f != null && f.HasRanged, chain = ranged && FightCover.ChainHolds(l, f, v);
                    bool shooters = ranged && f.HasShooters, low = shooters && FightCover.LowHolds(l, f, v);
                    double open = PropRules.WalkableShare(l, r, pv);
                    foreach (string key in new[] { "all", name })
                    {
                        if (!by.TryGetValue(key, out Tally t)) by[key] = t = new Tally();
                        t.Add(r.Props.Count(FightCover.IsTall), r.Props.Count(FightCover.IsLow));
                        t.Open += open;
                        t.Moved += r.PropsMovedForFloor;
                        t.Dropped += r.PropsDroppedForFloor;
                        if (r.PropsMovedForFloor + r.PropsDroppedForFloor > 0) t.RoomsMovedOrDropped++;
                        t.TallAdded += r.CoverTallAdded;
                        t.LowAdded += r.CoverLowAdded;
                        if (ranged) t.Ranged++;
                        if (chain) t.Chain++;
                        if (shooters) t.Shooters++;
                        if (low) t.Low++;
                    }
                }
        }

        /// <summary>
        /// The tables of the report (run by name; some minutes on .NET): a thousand seeds a floor, floors 1 to 5, before (version
        /// 18: the switch off) and after (version 19), the combat, elite and exit rooms: per template (and the drawn shapes) and over
        /// all, the tall and low covers a room holds (mean, fewest, most), the share where the chain of tall cover holds and where
        /// the low cover for the duck holds, the share of its floor that is walkable (open), the rooms where a prop was moved or
        /// dropped for the joined floor, and the covers added. The rules are measured on the floors before as on those after.
        /// </summary>
        [Explicit, Test, Timeout(7200000)]
        public void Measure_AThousandSeedsAFloor_BeforeAndAfter()
        {
            var before = new Dictionary<string, Tally>();
            var after = new Dictionary<string, Tally>();
            Count(Off, 1000, before);
            Count(On, 1000, after);
            var lines = new List<string> { "COVERTABLE a thousand seeds a floor, floors 1 to 5, the combat, elite and exit rooms; before = version 18 (UsableRooms off), after = version 19" };
            foreach (string key in new[] { "all" }.Concat(after.Keys.Where(k => k != "all").OrderBy(k => k, StringComparer.Ordinal)))
            {
                lines.Add("COVERTABLE before " + (before.TryGetValue(key, out Tally b) ? b.Line(key) : key + ": -"));
                lines.Add("COVERTABLE after  " + after[key].Line(key));
            }
            TestContext.WriteLine(string.Join("\n", lines));
        }

        /// <summary>
        /// The floors MoreRoomTemplatesPlayTests builds (run by name): few floors of the defaults with the second set on whose stamped
        /// rooms hold every template of the library, both sets, chosen greedily (the floor that adds the most templates first).
        /// </summary>
        [Explicit, Test, Timeout(3600000)]
        public void Search_FloorsThatHoldEveryTemplate_BothSets()
        {
            var all = new HashSet<string>(RoomTemplateLibrary.All.Where(t => t.Since != RoomTemplateLibrary.ExitSince).Select(t => t.Name)); // work order 089: not the third set, off in these floors
            var floors = new List<(ulong seed, int floor, HashSet<string> names)>();
            for (ulong s = 1; s <= 150; s++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = LevelGenerator.Generate(s, floor, SecondSet);
                    floors.Add((s, floor, new HashSet<string>(l.Rooms.Where(r => r.Template != null && r.Shape == RoomShape.Template).Select(r => r.Template.Template.Name))));
                }
            var left = new HashSet<string>(all);
            var chosen = new List<string>();
            while (left.Count > 0)
            {
                var best = floors.OrderByDescending(f => f.names.Count(left.Contains)).ThenBy(f => f.seed).ThenBy(f => f.floor).First();
                if (best.names.Count(left.Contains) == 0) break;
                chosen.Add("(" + best.seed + "UL, " + best.floor + ")");
                left.ExceptWith(best.names);
            }
            TestContext.WriteLine("FLOORS " + string.Join(", ", chosen) + (left.Count > 0 ? "  missing " + string.Join(", ", left) : ""));
            Assert.That(left, Is.Empty);
        }

        /// <summary>The joined floor over a thousand seeds a floor with both template sets (run by name): every room of every floor.</summary>
        [Explicit, Test, Timeout(7200000)]
        public void Measure_TheJoinedFloor_AThousandSeedsAFloor_BothSets()
        {
            int rooms = 0, moved = 0, dropped = 0, roomsWith = 0;
            foreach (LevelLayout l in Floors(1000, SecondSet, 3))
                foreach (LevelRoom r in l.Rooms)
                {
                    if (!JoinedFloor.Asks(r)) continue;
                    Assert.That(JoinedFloor.FaultOf(l, r, SecondSet), Is.Null, l.SeedText + " floor " + l.Floor + " " + r.Letter + r.Order);
                    rooms++;
                    moved += r.PropsMovedForFloor;
                    dropped += r.PropsDroppedForFloor;
                    if (r.PropsMovedForFloor + r.PropsDroppedForFloor > 0) roomsWith++;
                }
            TestContext.WriteLine($"JOINEDALL both sets, a thousand seeds a floor: {rooms} rooms joined; {roomsWith} rooms had a prop moved or dropped for it ({moved} moved, {dropped} dropped)");
        }
    }
}
