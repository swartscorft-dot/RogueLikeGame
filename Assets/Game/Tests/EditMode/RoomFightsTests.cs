using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// A room is a fight (work order 070, 2026-11-21, Kristian after his evening session: "nå rman kommer i et rom, så er man i
    /// en combat ... man trenger et punkt in i rommet der alle flyr på"; <see cref="RoomFightRules"/>, <see cref="RoomFight"/>).
    /// The point's place for small and large rooms and a door on each side; the door she came by; the rule that any waking
    /// of one wakes all, through the brain as the game steps it; the end of a fight and going home together; the switch's
    /// defaults, and the brain with no fight exactly as before; the recorder's event in the summary. And, explicit, what wakes
    /// at once on floors 1 to 5 over a thousand seeds a floor ("ROOMFIGHTSTATS").
    /// </summary>
    public class RoomFightsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float Cs = 4f;

        /// <summary>
        /// Two rooms in a line: the entrance, and a combat room <paramref name="w"/> by <paramref name="h"/> cells on its
        /// <paramref name="side"/> (the combat room's door in is on its wall facing the entrance), one door between.
        /// </summary>
        static LevelLayout TwoRooms(int side, int w, int h, out LevelRoom room, out LevelDoor door)
        {
            var l = new LevelLayout { Seed = 1UL, CellSize = Cs, GridCells = 48, Floor = 1, InALine = true };
            var entrance = new LevelRoom { Kind = RoomKind.Entrance, Order = 0 };
            room = new LevelRoom { Kind = RoomKind.Combat, Order = 1 };
            const int ox = 20, oy = 20; // the entrance, 4 by 4 cells
            entrance.Rect = new CellRect(ox, oy, 4, 4);
            switch (side)
            {
                case LevelGenerator.North: // the combat room north of the entrance: its door in on its south wall
                    room.Rect = new CellRect(ox, oy + 4, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy + 4, From = ox + 1, Width = 1 };
                    break;
                case LevelGenerator.South: // south: its door in on its north wall
                    room.Rect = new CellRect(ox, oy - h, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy, From = ox + 1, Width = 1 };
                    break;
                case LevelGenerator.East: // east: its door in on its west wall
                    room.Rect = new CellRect(ox + 4, oy, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox + 4, From = oy + 1, Width = 1 };
                    break;
                default: // west: its door in on its east wall
                    room.Rect = new CellRect(ox - w, oy, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox, From = oy + 1, Width = 1 };
                    break;
            }
            l.Rooms.Add(entrance);
            l.Rooms.Add(room);
            l.Doors.Add(door);
            return l;
        }

        /// <summary>A ground point <paramref name="depth"/> m straight in from the door's middle, into the room beyond it.</summary>
        static LevelPoint In(LevelLayout l, LevelRoom room, LevelDoor door, double depth)
        {
            door.Opening(l.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: return new LevelPoint(mx, my + depth);
                case LevelGenerator.North: return new LevelPoint(mx, my - depth);
                case LevelGenerator.West: return new LevelPoint(mx + depth, my);
                default: return new LevelPoint(mx - depth, my);
            }
        }

        static string SideWord(int side) => side == LevelGenerator.North ? "north" : side == LevelGenerator.South ? "south" : side == LevelGenerator.East ? "east" : "west";

        // ---------------- The point ----------------

        [TestCase(LevelGenerator.North)]
        [TestCase(LevelGenerator.East)]
        [TestCase(LevelGenerator.South)]
        [TestCase(LevelGenerator.West)]
        public void ThePoint_IsFiveMetresIn_InALargeRoom_AndAThirdOfASmallOnesDepth_FromADoorOnEachSide(int side)
        {
            var values = new RoomFightValues();
            Assert.That(values.PointIn, Is.EqualTo(5f), "the order's first guess");
            Assert.That(values.PointMostShare, Is.EqualTo(1f / 3f).Within(1e-6f));
            // A large room, 6 cells (24 m) deep from the door: the point is 5 m in; a small one, 3 cells (12 m) deep: 4 m in, a third.
            foreach ((int deep, double expected) in new[] { (6, 5.0), (8, 5.0), (3, 4.0), (2, 8.0 / 3.0) })
            {
                bool northSouth = side == LevelGenerator.North || side == LevelGenerator.South;
                LevelLayout l = TwoRooms(side, northSouth ? 5 : deep, northSouth ? deep : 5, out LevelRoom room, out LevelDoor door);
                string where = SideWord(side) + " of the entrance, " + deep + " cells deep";
                Assert.That(LevelGeometry.WallOf(room.Rect, door), Is.EqualTo(LevelGenerator.Opposite(side)), "the door in is on the wall facing the entrance, " + where);
                Assert.That(RoomFightRules.Depth(l, room, door), Is.EqualTo(deep * Cs).Within(1e-9), where);
                Assert.That(RoomFightRules.PointDepth(l, room, door, values), Is.EqualTo(expected).Within(1e-5), where); // a third in floats
                Assert.That(RoomFightRules.DepthIn(l, room, door, In(l, room, door, 1.25)), Is.EqualTo(1.25).Within(1e-9), where);
                Assert.That(RoomFightRules.PastThePoint(l, room, door, In(l, room, door, expected - 0.05), values), Is.False, "just short of the point, " + where);
                Assert.That(RoomFightRules.PastThePoint(l, room, door, In(l, room, door, expected + 0.05), values), Is.True, "just past it, " + where);
                Assert.That(RoomFightRules.PastThePoint(l, room, door, In(l, room, door, deep * Cs - 0.1), values), Is.True, "at the far wall, " + where);
                Assert.That(RoomFightRules.PastThePoint(l, room, door, In(l, room, door, -1.0), values), Is.False, "in the room before, " + where);
                // It is a line across the room: as far in at the side wall as at the door.
                LevelPoint atDoor = In(l, room, door, expected + 0.05);
                bool vertical = door.Vertical;
                double along = vertical ? room.Rect.Y * Cs + 0.3 - atDoor.Y : room.Rect.X * Cs + 0.3 - atDoor.X;
                var bySideWall = vertical ? new LevelPoint(atDoor.X, atDoor.Y + along) : new LevelPoint(atDoor.X + along, atDoor.Y);
                Assert.That(RoomFightRules.PastThePoint(l, room, door, bySideWall, values), Is.True, "past it beside a side wall too, " + where);
            }
        }

        [Test]
        public void ThePoint_ComesFromTheValues_AndNoDoorMeansAnywhereInTheRoom()
        {
            LevelLayout l = TwoRooms(LevelGenerator.North, 5, 6, out LevelRoom room, out LevelDoor door);
            var values = new RoomFightValues { PointIn = 9f };
            Assert.That(RoomFightRules.PointDepth(l, room, door, values), Is.EqualTo(8d).Within(1e-5), "9 m asked, a third of 24 m at most");
            values.PointMostShare = 1f;
            Assert.That(RoomFightRules.PointDepth(l, room, door, values), Is.EqualTo(9d).Within(1e-9));
            values.PointIn = 0f;
            Assert.That(RoomFightRules.PastThePoint(l, room, door, In(l, room, door, 0.01), values), Is.True, "0: at the door");
            Assert.That(RoomFightRules.PastThePoint(l, room, null, In(l, room, door, 0.01), new RoomFightValues()), Is.True, "no door: anywhere in the room");
            Assert.That(RoomFightRules.PastThePoint(l, room, door, new LevelPoint(-100, -100), new RoomFightValues()), Is.False, "not in the room");
        }

        [Test]
        public void TheDoorSheCameBy_IsTheRoomsDoorNearestHer()
        {
            LevelLayout l = TwoRooms(LevelGenerator.North, 5, 6, out LevelRoom room, out LevelDoor doorIn);
            // A door out on the north wall, to a room beyond.
            var beyond = new LevelRoom { Kind = RoomKind.Exit, Order = 2, Rect = new CellRect(room.Rect.X, room.Rect.Top, 5, 5) };
            var doorOut = new LevelDoor { RoomA = 1, RoomB = 2, Vertical = false, Line = room.Rect.Top, From = room.Rect.X + 3, Width = 1 };
            l.Rooms.Add(beyond);
            l.Doors.Add(doorOut);
            Assert.That(RoomFightRules.DoorSheCameBy(l, room, In(l, room, doorIn, 0.5)), Is.SameAs(doorIn));
            Assert.That(RoomFightRules.DoorSheCameBy(l, room, new LevelPoint((room.Rect.X + 3.5) * Cs, room.Rect.Top * Cs - 0.5)), Is.SameAs(doorOut));
            // Come in by the door out, the point is measured from the north wall: 5 m south of it.
            var values = new RoomFightValues();
            Assert.That(RoomFightRules.PastThePoint(l, room, doorOut, new LevelPoint((room.Rect.X + 3.5) * Cs, room.Rect.Top * Cs - 4.9), values), Is.False);
            Assert.That(RoomFightRules.PastThePoint(l, room, doorOut, new LevelPoint((room.Rect.X + 3.5) * Cs, room.Rect.Top * Cs - 5.1), values), Is.True);
        }

        [Test]
        public void ARoomHoldsAFight_OnlyWithEnemies_NotAPassage_AndOnlyOnAFloorInALine()
        {
            LevelLayout l = TwoRooms(LevelGenerator.North, 5, 6, out LevelRoom room, out _);
            Assert.That(RoomFightRules.HoldsAFight(l, room, 9), Is.True);
            Assert.That(RoomFightRules.HoldsAFight(l, room, 0), Is.False, "no enemies");
            Assert.That(RoomFightRules.HoldsAFight(l, new LevelRoom { Kind = RoomKind.Passage, Order = 3 }, 1), Is.False, "a passage (none holds enemies)");
            l.InALine = false;
            Assert.That(RoomFightRules.HoldsAFight(l, room, 9), Is.False, "a floor before the line (version 9 and older): as before");
        }

        [Test]
        public void OnFloorsWithPassages_EveryCombatAndExitRoom_HasItsPointAtFiveMetres_AndNoPassageHoldsAnEnemy()
        {
            var values = new LevelValues();
            Assert.That(values.RoomFights, Is.True, "on by default");
            foreach (int floor in new[] { 1, 3, 5 })
                for (ulong s = 0; s < 40; s++)
                {
                    LevelLayout level = LevelGenerator.Generate(s, floor, values);
                    foreach (LevelRoom r in level.Rooms)
                    {
                        string where = "seed " + s + " floor " + floor + " room " + r.Letter + r.Order;
                        if (r.Kind == RoomKind.Passage)
                        {
                            Assert.That(r.EnemyCount, Is.EqualTo(0), "no enemy in a passage, " + where);
                            continue;
                        }
                        if (r.EnemyCount == 0) continue;
                        Assert.That(r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit, where);
                        LevelDoor entry = LevelGeometry.EntryDoor(level, r);
                        Assert.That(entry, Is.Not.Null, where);
                        Assert.That(RoomFightRules.PointDepth(level, r, entry, values.Fights), Is.EqualTo(5d).Within(1e-9), where + " (" + RoomFightRules.Depth(level, r, entry) + " m deep)");
                        // The point is inside the room's own floor: a step past it straight in from the door is on it.
                        Assert.That(RoomFightRules.DepthIn(level, r, entry, In(level, r, entry, 5.5)), Is.EqualTo(5.5).Within(1e-9), where);
                    }
                }
        }

        // ---------------- Any waking wakes all ----------------

        /// <summary>The game's step of one enemy's brain, as Enemy.Act takes it: held by its fight, it stirs the fight when it wakes.</summary>
        static void Step(EnemyBrain brain, RoomFight fight, float now, float distance, bool provoked, EnemyValues values)
        {
            if (fight != null && provoked) fight.Stir(RoomFightStart.Hit, now); // a hurt stirs the fight at the hit itself
            bool held = fight != null && fight.On;
            EnemyState before = brain.State;
            brain.Step(now, distance, true, provoked, values, true, null, held);
            if (before == EnemyState.Idle && brain.State != EnemyState.Idle) fight?.Stir(RoomFightStart.Seen, now);
        }

        [Test]
        public void AnyWakingOfOne_WakesTheWholeRoom_FarOnesToo_WithNoRangeAsked()
        {
            var values = new EnemyValues { AggroRange = 9f, LeashRange = 22f, Attacks = new EnemyAttackDefinition[0] };
            // Three enemies of a room: one 12 m from her, one 20 m, one 40 m (past its leash): none within its range.
            float[] distances = { 12f, 20f, 40f };
            foreach (string how in new[] { "seen", "hit" })
            {
                var fight = new RoomFight(1);
                var brains = distances.Select(_ => new EnemyBrain()).ToArray();
                float now = 0f;
                // Before: she is 30 m and more from all of them: nobody wakes.
                for (int i = 0; i < brains.Length; i++) Step(brains[i], fight, now, distances[i] + 22f, false, values);
                Assert.That(brains.All(b => b.State == EnemyState.Idle) && !fight.On, how + ": nobody awake before");
                // One wakes: she comes within 8 m of the nearest and it notices her, or the 20 m one is hurt.
                now += 0.02f;
                for (int i = 0; i < brains.Length; i++) Step(brains[i], fight, now, how == "seen" && i == 0 ? 8f : distances[i], how == "hit" && i == 1, values);
                Assert.That(fight.On, how + ": the fight started");
                Assert.That(fight.StartedBy, Is.EqualTo(how == "hit" ? RoomFightStart.Hit : RoomFightStart.Seen));
                // The next step, every one of them is awake, the one 40 m away too.
                now += 0.02f;
                for (int i = 0; i < brains.Length; i++) Step(brains[i], fight, now, distances[i], false, values);
                Assert.That(brains.Select(b => b.State), Is.All.EqualTo(EnemyState.Chase), how + ": all awake, none left at home");
                // Held, none gives up for distance: 60 m.
                now += 0.02f;
                for (int i = 0; i < brains.Length; i++) Step(brains[i], fight, now, 60f, false, values);
                Assert.That(brains.Select(b => b.State), Is.All.EqualTo(EnemyState.Chase), how + ": none gives up while the fight holds them");
            }
        }

        [Test]
        public void TheFight_StartsOnce_IsSeenOnce_AndAfterItEnds_StartsAgain_AllOfThem()
        {
            var fight = new RoomFight(4);
            Assert.That(fight.On, Is.False);
            Assert.That(fight.TakeStarted(), Is.False);
            Assert.That(fight.Stir(RoomFightStart.Point, 3f), Is.True);
            Assert.That(fight.Stir(RoomFightStart.Hit, 3.1f), Is.False, "on already: the first start stands");
            Assert.That((fight.On, fight.StartedBy, fight.StartedAt, fight.Starts), Is.EqualTo((true, RoomFightStart.Point, 3f, 1)));
            Assert.That(fight.TakeStarted(), Is.True);
            Assert.That(fight.TakeStarted(), Is.False, "once a start");
            fight.End();
            Assert.That(fight.On, Is.False);

            // Its enemies go home together, and the next waking is all of them again.
            var values = new EnemyValues { AggroRange = 9f, LeashRange = 22f, Attacks = new EnemyAttackDefinition[0] };
            var brains = new[] { new EnemyBrain(), new EnemyBrain() };
            foreach (EnemyBrain b in brains) b.Step(0f, 30f, true, false, values, true, null, held: true);
            Assert.That(brains.Select(b => b.State), Is.All.EqualTo(EnemyState.Chase));
            foreach (EnemyBrain b in brains) b.GoHome(1f);
            Assert.That(brains.Select(b => (b.State, b.Attack)), Is.All.EqualTo((EnemyState.Idle, (EnemyAttackDefinition)null)), "home together");
            Assert.That(fight.Stir(RoomFightStart.Seen, 5f), Is.True, "woken again");
            Assert.That(fight.Starts, Is.EqualTo(2));
            foreach (EnemyBrain b in brains) b.Step(5f, 50f, true, false, values, true, null, held: fight.On);
            Assert.That(brains.Select(b => b.State), Is.All.EqualTo(EnemyState.Chase), "all of them, never some");
        }

        [Test]
        public void TheFightEnds_WhenSheDies_TheRoomIsCleared_OrSheIsOutsideItAndBeyondEveryLeash()
        {
            Assert.That(RoomFightRules.Ends(true, 5, true, false), Is.False, "she is in the room: never for distance");
            Assert.That(RoomFightRules.Ends(true, 5, false, true), Is.False, "outside it, one of them within its leash: they follow her");
            Assert.That(RoomFightRules.Ends(true, 5, false, false), Is.True, "outside it and beyond every leash: they all go home");
            Assert.That(RoomFightRules.Ends(true, 0, true, false), Is.True, "cleared");
            Assert.That(RoomFightRules.Ends(false, 5, true, true), Is.True, "she died");
            Assert.That(RoomFightRules.Word(RoomFightStart.Point) + RoomFightRules.Word(RoomFightStart.Seen) + RoomFightRules.Word(RoomFightStart.Hit)
                        + RoomFightRules.Word(RoomFightStart.Provoked), Is.EqualTo("pointseenhitprovoked"));
        }

        // ---------------- The switch off: today ----------------

        [Test]
        public void WithNoFight_TheBrainWakesAndGivesUp_ExactlyAsBefore()
        {
            var values = new EnemyValues { AggroRange = 9f, LeashRange = 22f, Attacks = new EnemyAttackDefinition[0] };
            // Today's rule, written out: from home it wakes within its leash when she is within its range or it is provoked;
            // in the chase it gives up past its leash or when she is dead.
            bool WakesToday(float d, bool alive, bool provoked) => alive && d <= values.LeashRange && (d <= values.AggroRange || provoked);
            foreach (float d in new[] { 0f, 5f, 8.99f, 9f, 9.01f, 15f, 21.99f, 22f, 22.01f, 40f })
                foreach (bool alive in new[] { true, false })
                    foreach (bool provoked in new[] { true, false })
                    {
                        var idle = new EnemyBrain();
                        idle.Step(0f, d, alive, provoked, values); // held left out: false
                        Assert.That(idle.State == EnemyState.Chase, Is.EqualTo(WakesToday(d, alive, provoked)), "from home at " + d + " m, alive " + alive + ", provoked " + provoked);
                        var chasing = new EnemyBrain();
                        chasing.Step(0f, 1f, true, true, values);
                        chasing.Step(0.1f, d, alive, provoked, values, true, null, held: false);
                        Assert.That(chasing.State == EnemyState.Idle, Is.EqualTo(!alive || d > values.LeashRange), "in the chase at " + d + " m, alive " + alive);
                    }
            // Held, only her death sends it home.
            var held = new EnemyBrain();
            held.Step(0f, 500f, true, false, values, true, null, held: true);
            Assert.That(held.State, Is.EqualTo(EnemyState.Chase));
            held.Step(0.1f, 500f, false, false, values, true, null, held: true);
            Assert.That(held.State, Is.EqualTo(EnemyState.Idle), "she died");
            var dead = new EnemyBrain();
            dead.Step(0f, 1f, false, true, values, true, null, held: true);
            Assert.That(dead.State, Is.EqualTo(EnemyState.Idle), "no waking while she is dead");
        }

        [Test]
        public void TheSwitch_IsOnByDefault_BesideThePassages_AndTheGeneratorNeverReadsIt()
        {
            var on = new LevelValues();
            var off = new LevelValues { RoomFights = false };
            Assert.That(on.RoomFights && !off.RoomFights);
            Assert.That(on.Fights.Cue, Is.True);
            Assert.That(on.Fights.CueSound, Is.EqualTo(PlaceholderSound.BossAnger));
            foreach (int floor in new[] { 1, 5 })
                for (ulong s = 0; s < 5; s++)
                    Assert.That(LevelGenerator.Generate(s, floor, off).Describe(), Is.EqualTo(LevelGenerator.Generate(s, floor, on).Describe()), "seed " + s + " floor " + floor);
        }

        // ---------------- The recorder and the summary ----------------

        static SessionEvent Fight(double t, string how, int floor, int room, string kind, int woke, bool inside = false)
        {
            var e = new SessionEvent(t, SessionEventTypes.RoomFight, how);
            e.AddExtra("floor", floor.ToString(Inv));
            e.AddExtra("room", room.ToString(Inv));
            e.AddExtra("order", (room * 2).ToString(Inv));
            e.AddExtra("kind", kind);
            e.AddExtra("inside", how == "point" || inside ? "true" : "false");
            e.AddExtra("woke", woke.ToString(Inv));
            return e;
        }

        [Test]
        public void TheSummary_CountsEachFloorsFights_AtThePointAndFromOutside()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                new SessionEvent(1, SessionEventTypes.RunStart, "ABCDEFGH"),
                new SessionEvent(1, SessionEventTypes.FloorEnter, "SEED0001") ,
                Fight(5, "point", 1, 2, "combat", 9),
                Fight(30, "hit", 1, 3, "combat", 10),
                Fight(60, "point", 1, 4, "elite", 11),
                Fight(80, "seen", 2, 2, "combat", 12),
                Fight(90, "point", 2, 3, "combat", 7),
                Fight(99, "seen", 2, 4, "exit", 16, inside: true),
            };
            events[2].AddExtra("floor", "1");
            SessionStats stats = SessionSummary.Compute(events);
            RunRow run = stats.Runs.Single();
            Assert.That(run.RoomFights.Keys, Is.EqualTo(new[] { 1, 2 }));
            RoomFightCounts one = run.RoomFights[1], two = run.RoomFights[2];
            Assert.That((one.Point, one.Inside, one.Hit, one.Seen, one.FromOutside, one.MostWoke), Is.EqualTo((2, 0, 1, 0, 1, 11)));
            Assert.That((two.Point, two.Inside, two.Hit, two.Seen, two.FromOutside, two.MostWoke), Is.EqualTo((1, 1, 0, 1, 1, 16)));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("  room fights      floor 1: 2 at the point, 0 inside short of it, 1 from outside (1 hit, 0 seen), most woke at once 11;  "
                                  + "floor 2: 1 at the point, 1 inside short of it, 1 from outside (0 hit, 1 seen), most woke at once 16", text);

            // A run with none (a log from before, or the switch off): no line.
            events.RemoveAll(e => e.Type == SessionEventTypes.RoomFight);
            StringAssert.DoesNotContain("room fights", SessionSummary.Format(SessionSummary.Compute(events)));
        }

        // ---------------- What it does to the difficulty (explicit: a measurement) ----------------

        static double Median(List<int> v)
        {
            if (v.Count == 0) return double.NaN;
            List<int> s = v.OrderBy(x => x).ToList();
            return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) * 0.5;
        }

        /// <summary>
        /// How many enemies wake at once when a room's fight starts (every enemy of the room, the act boss counted; the packs
        /// it calls later not), by floor and by room: an ordinary combat room, the elite room, the exit room. A thousand seeds
        /// a floor with the defaults. Written as "ROOMFIGHTSTATS" lines; it asserts only that every such room has some.
        /// </summary>
        [Test, Explicit("a measurement: a thousand seeds a floor")]
        public void Measure_EnemiesThatWakeAtOnce_ByFloorAndRoom_AThousandSeedsAFloor()
        {
            var values = new LevelValues();
            var lines = new List<string>();
            for (int floor = 1; floor <= values.FloorsPerAct; floor++)
            {
                var ordinary = new List<int>();
                var elite = new List<int>();
                var exit = new List<int>();
                var deepest = new List<double>();
                for (ulong s = 0; s < 1000; s++)
                {
                    LevelLayout level = LevelGenerator.Generate(s, floor, values);
                    foreach (LevelRoom r in level.Rooms)
                    {
                        if (!RoomFightRules.HoldsAFight(level, r, r.EnemyCount)) continue;
                        if (r.Kind == RoomKind.Exit) exit.Add(r.EnemyCount);
                        else if (r.Elite) elite.Add(r.EnemyCount);
                        else ordinary.Add(r.EnemyCount);
                        LevelDoor entry = LevelGeometry.EntryDoor(level, r);
                        if (entry != null) deepest.Add(RoomFightRules.Depth(level, r, entry));
                    }
                }
                string Cell(List<int> v) => v.Count == 0 ? "-" : string.Format(Inv, "most {0}, median {1}, fewest {2}", v.Max(), Median(v), v.Min());
                lines.Add(string.Format(Inv, "ROOMFIGHTSTATS floor {0}: ordinary room {1} ({2} rooms); elite room {3}; exit room {4}; rooms {5:0}-{6:0} m deep from the door in",
                                        floor, Cell(ordinary), ordinary.Count, Cell(elite), Cell(exit), deepest.Min(), deepest.Max()));
                Assert.That(ordinary.Count, Is.GreaterThan(0));
            }
            string all = string.Join("\n", lines);
            TestContext.WriteLine(all);
            Assert.Pass(all);
        }
    }
}
