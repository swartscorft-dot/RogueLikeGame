using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The caller (work order 137; Kristian's tick of 2026-11-25, "Ja, fra etasje 2"): its numbers and switch, which rooms of a floor hold
    /// one (from floor 2, floor 2 from its third combat room, at most one a floor on floors 2 to 4 and two on floor 5, never the first
    /// combat room, the elite, exit or throne room, nor a room with a warder), where it stands (far in, in view from the room's line, apart
    /// from the room's enemies), what of the room gives way, how many a call brings, its kind's word, letter and model, its Trials line,
    /// and that the generator is untouched: on or off, every floor is the same, field for field. The rooms' shares for the report are in
    /// <see cref="HowOften_ARoomHoldsOne_ByFloor"/>.
    /// </summary>
    public class RoomCallersTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        static readonly CallerValues C = new CallerValues();

        static LevelLayout Floor(ulong seed, int floor, LevelValues v = null) => LevelGenerator.Generate(seed * 7919UL, floor, v ?? new LevelValues());

        // ---------------- The switch and the numbers ----------------

        [Test]
        public void TheSwitch_IsOn_WithHisTicksNumbers()
        {
            var v = new LevelValues();
            Assert.That(v.Callers, Is.True, "his tick: \"Ja, fra etasje 2\"");
            Assert.That(v.CallerRules, Is.Not.Null);
            Assert.That(C.FromFloor, Is.EqualTo(2), "from floor 2");
            Assert.That(C.Floor2FromCombatRoom, Is.EqualTo(3), "floor 2 from its third combat room");
            Assert.That(C.OnFloor, Is.EqualTo(new[] { 0, 0, 1, 1, 1, 2 }), "one a floor on 2 to 4, two on 5");
            Assert.That(C.CallEvery, Is.EqualTo(6f), "every 6 s");
            Assert.That(C.CallCount, Is.EqualTo(3), "three swarmers");
            Assert.That(C.MostAlive, Is.EqualTo(6), "at most six alive");
            Assert.That(C.TellSeconds, Is.EqualTo(1f), "a 1 s tell");
            Assert.That(C.FirstCallAfter, Is.EqualTo(3f), "research R3's A4: the first 3 s after the wake");
            Assert.That(C.Weight, Is.EqualTo(new LineUpValues().CasterWeight), "it weighs a caster's");
            Assert.That(C.PartnersKept, Is.EqualTo(1));
            Assert.That(RoomCallerRules.On(v), Is.True);
            Assert.That(RoomCallerRules.On(new LevelValues { Callers = false }), Is.False);
        }

        [Test]
        public void ItsCall_IsAnAttackOfNoDamage_ATellOfASecond_Counterable_AsTheCastersBigCast()
        {
            EnemyAttackDefinition a = C.CallAttack();
            Assert.That(a.Name, Is.EqualTo(CallerValues.Call));
            Assert.That(a.Damage, Is.EqualTo(0), "it hurts nobody itself");
            Assert.That(a.Windup, Is.EqualTo(C.TellSeconds), "its tell is the wind-up");
            Assert.That(a.Shape, Is.EqualTo(EnemyAttackShape.Circle), "a ring round it");
            Assert.That(a.Radius, Is.EqualTo(C.RingRadius));
            Assert.That(PoolRules.IsCounterable(a), Is.True, "Stopping Shot from its front cuts it, as the caster's Grave Rain and fan");
            Assert.That(PoolRules.IsCounterable(new CasterValues().Enemy.Attacks.FirstOrDefault(x => x.Name == CasterValues.GraveRain)), Is.True, "the caster's own big cast is so");
            Assert.That(new CallerValues { Counterable = false }.CallAttack().Counterable, Is.False);
            Assert.That(AttackTurns.SortOf(EnemyKind.Caller), Is.EqualTo(TurnSort.None), "it waits for no turn: its clock is its own");
        }

        [Test]
        public void TheKind_HasItsWord_ALetterOfItsOwn_AndTheCastersModel_ItsCallTheBigCast()
        {
            Assert.That(EnemyKinds.Word(EnemyKind.Caller), Is.EqualTo("caller"));
            var letters = new HashSet<char>();
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind))) Assert.That(letters.Add(EnemyKinds.Letter(k, 1)), Is.True, "a letter of its own: " + k);
            EnemyModelSpec spec = EnemyModelSpec.Of(EnemyKind.Caller);
            Assert.That(spec, Is.SameAs(EnemyModelSpec.Caller));
            Assert.That(EnemyModelSpec.All, Has.Member(spec));
            Assert.That(spec.Name, Is.EqualTo(EnemyModelSpec.Caster.Name), "the caster's model: no new model");
            Assert.That(spec.Height, Is.EqualTo(EnemyModelSpec.Caster.Height));
            Assert.That(spec.Clips, Is.SameAs(EnemyModelSpec.Caster.Clips), "the caster's clips");
            EnemyClipSpec clip = spec.ClipForAttack(CallerValues.Call);
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.Name, Is.EqualTo("CastBig"), "the caster's big cast");
            Assert.That(clip.Strike, Is.GreaterThan(0).And.LessThan(clip.Length));
        }

        [Test]
        public void ItsTint_IsApartFromTheCastersAndTheWarders()
        {
            Rgb t = C.Tint;
            // The caster's model is shown untinted (white over its texture); the warder's tint is green.
            double FromWhite() => Math.Sqrt((1 - t.R) * (1 - t.R) + (1 - t.G) * (1 - t.G) + (1 - t.B) * (1 - t.B));
            double From(double r, double g, double b) => Math.Sqrt((r - t.R) * (r - t.R) + (g - t.G) * (g - t.G) + (b - t.B) * (b - t.B));
            Assert.That(FromWhite(), Is.GreaterThan(0.5), "apart from the caster's untinted model");
            Assert.That(From(0.25, 0.75, 0.45), Is.GreaterThan(0.8), "apart from the warder's green");
            Assert.That(From(0.72, 0.18, 0.2), Is.GreaterThan(0.6), "apart from the lancer's blood red");
            Assert.That(From(0.78, 0.6, 1.0), Is.GreaterThan(0.3), "apart from the waves' pale violet discs");
        }

        // ---------------- The generator is untouched ----------------

        /// <summary>The caller is the fight's, not the generator's: on or off, every floor is the same, field for field, its map too.</summary>
        [Test]
        public void OnOrOff_EveryFloorIsTheSame_FieldForField()
        {
            for (ulong seed = 1; seed <= 30; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout on = Floor(seed, floor);
                    LevelLayout off = Floor(seed, floor, new LevelValues { Callers = false });
                    Assert.That(FloorPrint.Of(off, 14695981039346656037UL), Is.EqualTo(FloorPrint.Of(on, 14695981039346656037UL)), "seed " + seed + " floor " + floor);
                    Assert.That(LevelMap.Draw(off), Is.EqualTo(LevelMap.Draw(on)));
                    Assert.That(on.Version, Is.EqualTo(off.Version));
                    Assert.That(RoomCallerRules.Rooms(off, new LevelValues { Callers = false }), Is.Empty, "off: no caller");
                }
        }

        // ---------------- Which rooms ----------------

        [Test]
        public void WhichRooms_FromFloor2_AtMostItsFloorsCount_NeverTheFirst_NorTheElite_NorAWardersRoom_Floor2FromItsThird()
        {
            var v = new LevelValues();
            for (ulong seed = 1; seed <= 60; seed++)
                for (int floor = 1; floor <= 5; floor++)
                {
                    LevelLayout l = Floor(seed, floor);
                    List<LevelRoom> rooms = RoomCallerRules.Rooms(l, v);
                    List<LevelRoom> combat = RoomCallerRules.CombatRooms(l);
                    string where = "seed " + seed + " floor " + floor;
                    Assert.That(rooms.Count, Is.LessThanOrEqualTo(RoomCallerRules.OnFloor(floor, C)), where);
                    if (floor == 1) Assert.That(rooms, Is.Empty, where + ": none on floor 1");
                    Assert.That(rooms.Distinct().Count(), Is.EqualTo(rooms.Count), where + ": at most one a room");
                    foreach (LevelRoom r in rooms)
                    {
                        string room = where + " room " + r.Letter + r.Order;
                        Assert.That(r.Kind, Is.EqualTo(RoomKind.Combat), room);
                        Assert.That(r.Elite, Is.False, room + ": never the elite room");
                        Assert.That(r.EnemyGroups.Any(g => g.Kind == EnemyKind.Warder), Is.False, room + ": never with a warder");
                        Assert.That(r.EnemyGroups.Any(g => g.Boss), Is.False, room + ": never the throne room");
                        Assert.That(r.LineUp, Is.Not.Null, room);
                        Assert.That(r.LineUp.Full, Is.False, room + ": a line-up of two kinds, its partner gives way");
                        int at = combat.IndexOf(r);
                        Assert.That(at, Is.GreaterThan(0), room + ": never the first combat room");
                        if (floor == 2) Assert.That(at + 1, Is.GreaterThanOrEqualTo(3), room + ": floor 2 from its third combat room");
                        Assert.That(RoomCallerRules.RoomFault(l, r, C), Is.Null, room);
                        Assert.That(RoomCallerRules.Places(l, r, v), Is.Not.Empty, room + ": a place");
                    }
                    Assert.That(RoomCallerRules.Rooms(l, v).Select(r => r.Order), Is.EqualTo(rooms.Select(r => r.Order)), where + ": the same each time");
                }
        }

        [Test]
        public void RoomFault_SaysWhy()
        {
            var v = new LevelValues();
            LevelLayout l = Floor(3, 3);
            List<LevelRoom> combat = RoomCallerRules.CombatRooms(l);
            Assert.That(RoomCallerRules.RoomFault(l, combat[0], C), Is.EqualTo("is the floor's first combat room"));
            Assert.That(RoomCallerRules.RoomFault(l, l.ExitRoom, C), Is.EqualTo("is not a combat room"));
            LevelLayout one = Floor(3, 1);
            Assert.That(RoomCallerRules.RoomFault(one, RoomCallerRules.CombatRooms(one)[1], C), Does.StartWith("is on floor 1"));
            LevelRoom elite = l.Rooms.FirstOrDefault(r => r.Elite);
            if (elite != null) Assert.That(RoomCallerRules.RoomFault(l, elite, C), Is.EqualTo("is the elite room"));
        }

        // ---------------- Where it stands ----------------

        [Test]
        public void ItsPlaces_AreFarIn_InViewFromTheLine_ApartFromTheRoomsEnemies()
        {
            var v = new LevelValues();
            RangedAroundValues r = v.RangedAround;
            int rooms = 0;
            for (ulong seed = 1; seed <= 40; seed++)
                for (int floor = 2; floor <= 5; floor++)
                {
                    LevelLayout l = Floor(seed, floor);
                    foreach (LevelRoom room in RoomCallerRules.Rooms(l, v))
                    {
                        rooms++;
                        LevelDoor entry = LevelGeometry.EntryDoor(l, room);
                        LevelPoint line = LevelRanged.LineMiddle(l, room, entry, v);
                        double depth = LevelLancers.Depth(l, room, entry, v);
                        List<LevelPoint> places = RoomCallerRules.PlacesInOrder(l, room, v);
                        Assert.That(places, Is.EquivalentTo(RoomCallerRules.Places(l, room, v)), "the same places, drawn");
                        foreach (LevelPoint p in places)
                        {
                            Assert.That(LevelGeometry.DistanceToDoor(p, entry, l.CellSize) / depth, Is.GreaterThanOrEqualTo(Math.Min(C.FarShare, C.InViewShareLeast) - C.InViewBand - 1e-9), "far in (the far band, else the deepest in view)");
                            Assert.That(LevelRanged.InDesignView(line, p, r, C.ViewMargin), Is.True, "in view from the line's middle");
                            for (int i = 0; i < room.EnemyPoints.Count; i++)
                                foreach (LevelPoint o in room.PlacesAt(i))
                                    Assert.That(o.DistanceTo(p), Is.GreaterThanOrEqualTo(C.Apart - 1e-9), "apart from the room's enemies");
                        }
                    }
                }
            Assert.That(rooms, Is.GreaterThan(40), "callers were placed");
        }

        // ---------------- What gives way, and a call's size ----------------

        [Test]
        public void ThePartners_GiveWayByItsWeight_KeepingOne()
        {
            var w = new LineUpValues();
            Assert.That(RoomCallerRules.GiveWay(4, EnemyKind.Brute, C, w), Is.EqualTo(1), "one brute (6.3)");
            Assert.That(RoomCallerRules.GiveWay(6, EnemyKind.Shooter, C, w), Is.EqualTo(1), "one shooter (4.2): 6.1 / 4.2 is 1.45");
            Assert.That(RoomCallerRules.GiveWay(12, EnemyKind.Swarmer, C, w), Is.EqualTo(6), "six swarmers");
            Assert.That(RoomCallerRules.GiveWay(4, EnemyKind.Swarmer, C, w), Is.EqualTo(3), "never fewer than one left");
            Assert.That(RoomCallerRules.GiveWay(1, EnemyKind.Bomber, C, w), Is.EqualTo(0), "the one bomber stays");
            Assert.That(RoomCallerRules.GiveWay(1, EnemyKind.Brute, C, w), Is.EqualTo(0), "the line-up keeps its partner");
            Assert.That(RoomCallerRules.GiveWay(0, EnemyKind.Brute, C, w), Is.EqualTo(0));
        }

        [Test]
        public void ACall_BringsThree_NeverPastSixAlive()
        {
            Assert.That(RoomCallerRules.CallSize(0, C), Is.EqualTo(3));
            Assert.That(RoomCallerRules.CallSize(3, C), Is.EqualTo(3));
            Assert.That(RoomCallerRules.CallSize(4, C), Is.EqualTo(2), "fewer");
            Assert.That(RoomCallerRules.CallSize(5, C), Is.EqualTo(1));
            Assert.That(RoomCallerRules.CallSize(6, C), Is.EqualTo(0), "none");
            Assert.That(RoomCallerRules.CallSize(9, C), Is.EqualTo(0));
        }

        // ---------------- The Trials page ----------------

        [Test]
        public void TheTrialsPage_EndsWithTheCallersLine_AfterTheWaves()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines[lines.Count - 1], Is.EqualTo(SettingLine.Callers));
            Assert.That(lines[lines.Count - 2], Is.EqualTo(SettingLine.Waves));
            Assert.That(SettingsPages.PageOf(SettingLine.Callers), Is.EqualTo(SettingsPage.Trials));
            Assert.That(RoomTrialLines.Is(SettingLine.Callers), Is.True);
            Assert.That(RoomTrialLines.Name(SettingLine.Callers), Is.EqualTo("Callers"));
            Assert.That(RoomTrialLines.Name(SettingLine.Waves), Is.EqualTo("Waves"));
        }

        // ---------------- The numbers for the report ----------------

        /// <summary>
        /// For the report: on 300 seeds a floor, how many callers a floor holds, the share of its combat rooms that hold one, which
        /// line-ups and what gave way. Writes "CALLERS" lines; asserts only that floors 2 to 5 hold them.
        /// </summary>
        [Test]
        public void HowOften_ARoomHoldsOne_ByFloor()
        {
            var v = new LevelValues();
            const int Seeds = 300;
            for (int floor = 2; floor <= 5; floor++)
            {
                int combat = 0, with = 0, floorsFull = 0, gave = 0, places = 0;
                var lineUps = new Dictionary<string, int>();
                var why = new Dictionary<string, int>();
                var counts = new int[4];
                for (ulong seed = 1; seed <= Seeds; seed++)
                {
                    LevelLayout l = Floor(seed, floor);
                    combat += RoomCallerRules.CombatRooms(l).Count;
                    foreach (LevelRoom r in RoomCallerRules.CombatRooms(l))
                    {
                        string f = RoomCallerRules.RoomFault(l, r, C) ?? (RoomCallerRules.Places(l, r, v).Count == 0 ? "has no place far in and in view" : "may");
                        if (f.StartsWith("is floor 2's combat room", StringComparison.Ordinal)) f = "is before floor 2's third combat room";
                        why[f] = (why.TryGetValue(f, out int k) ? k : 0) + 1;
                    }
                    List<LevelRoom> rooms = RoomCallerRules.Rooms(l, v);
                    with += rooms.Count;
                    counts[Math.Min(3, rooms.Count)]++;
                    if (rooms.Count == RoomCallerRules.OnFloor(floor, C)) floorsFull++;
                    foreach (LevelRoom r in rooms)
                    {
                        lineUps[r.LineUp.Name] = (lineUps.TryGetValue(r.LineUp.Name, out int n) ? n : 0) + 1;
                        gave += RoomCallerRules.GiveWay(RoomCallerRules.Partners(r), r.LineUp.Partner, C, v.LineUpRules);
                        places += RoomCallerRules.Places(l, r, v).Count;
                    }
                }
                string line = string.Format(Inv,
                    "CALLERS floor {0}: {1} seeds; callers a floor 0/1/2: {2}/{3}/{4}; floors with their most ({5}): {6:0.0} %; of the combat rooms {7:0.0} % hold one ({8} of {9}); partners given way a caller {10:0.00}; places a caller's room {11:0.0}; line-ups: {12}; the combat rooms: {13}",
                    floor, Seeds, counts[0], counts[1], counts[2], RoomCallerRules.OnFloor(floor, C), 100.0 * floorsFull / Seeds, 100.0 * with / Math.Max(1, combat),
                    with, combat, with > 0 ? (double)gave / with : 0, with > 0 ? (double)places / with : 0,
                    string.Join(", ", lineUps.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value)),
                    string.Join(", ", why.OrderByDescending(p => p.Value).Select(p => p.Key + " " + p.Value)));
                TestContext.WriteLine(line);
                Console.WriteLine(line);
                Assert.That(with, Is.GreaterThan(0), "floor " + floor + " holds callers");
            }
        }
    }
}
