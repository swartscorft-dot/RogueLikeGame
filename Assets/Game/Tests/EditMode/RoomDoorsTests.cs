using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The door closes behind her (work order 086; Kristian's answer of 2026-11-22: "Døra stenges bak deg";
    /// <see cref="RoomDoorRules"/>, the values in <see cref="RoomFightValues"/>). The switch and its values; "inside" for a door on
    /// each side; what the open doors do (stay open, close, wait, pull in) and when shut doors open; each of the wake cases walked
    /// through as the floor steps it (at the line, short of it by a hurt, from outside by a hurt, from outside with an enemy that
    /// followed her out); the wake by range off while the doors are on, through the brain as the enemy steps it, and on with them
    /// off; the block's place and size; its rise and sink; where an enemy is pulled in; on the floors, every enemy's place and the
    /// line's point inside the room for its doors, so the doors close in the frame the room wakes at its line; the summary's words.
    /// </summary>
    public class RoomDoorsTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float Cs = 4f;

        /// <summary>
        /// Three rooms in a line: the entrance, a combat room <paramref name="w"/> by <paramref name="h"/> cells on its
        /// <paramref name="side"/> (its door in on its wall facing the entrance), and a third room beyond it on the same side (its
        /// door out on the far wall). The combat room's doors: <paramref name="doorIn"/> and the way on.
        /// </summary>
        static LevelLayout Line(int side, int w, int h, out LevelRoom room, out LevelDoor doorIn, out LevelDoor doorOut)
        {
            var l = new LevelLayout { Seed = 1UL, CellSize = Cs, GridCells = 64, Floor = 1, InALine = true };
            var entrance = new LevelRoom { Kind = RoomKind.Entrance, Order = 0 };
            room = new LevelRoom { Kind = RoomKind.Combat, Order = 1 };
            var beyond = new LevelRoom { Kind = RoomKind.Combat, Order = 2 };
            const int ox = 24, oy = 24; // the entrance, 4 by 4 cells
            entrance.Rect = new CellRect(ox, oy, 4, 4);
            switch (side)
            {
                case LevelGenerator.North:
                    room.Rect = new CellRect(ox, oy + 4, w, h);
                    beyond.Rect = new CellRect(ox, oy + 4 + h, 4, 4);
                    doorIn = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy + 4, From = ox + 1, Width = 1 };
                    doorOut = new LevelDoor { RoomA = 1, RoomB = 2, Vertical = false, Line = oy + 4 + h, From = ox + 2, Width = 1 };
                    break;
                case LevelGenerator.South:
                    room.Rect = new CellRect(ox, oy - h, w, h);
                    beyond.Rect = new CellRect(ox, oy - h - 4, 4, 4);
                    doorIn = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy, From = ox + 1, Width = 1 };
                    doorOut = new LevelDoor { RoomA = 1, RoomB = 2, Vertical = false, Line = oy - h, From = ox + 2, Width = 1 };
                    break;
                case LevelGenerator.East:
                    room.Rect = new CellRect(ox + 4, oy, w, h);
                    beyond.Rect = new CellRect(ox + 4 + w, oy, 4, 4);
                    doorIn = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox + 4, From = oy + 1, Width = 1 };
                    doorOut = new LevelDoor { RoomA = 1, RoomB = 2, Vertical = true, Line = ox + 4 + w, From = oy + 2, Width = 1 };
                    break;
                default:
                    room.Rect = new CellRect(ox - w, oy, w, h);
                    beyond.Rect = new CellRect(ox - w - 4, oy, 4, 4);
                    doorIn = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox, From = oy + 1, Width = 1 };
                    doorOut = new LevelDoor { RoomA = 1, RoomB = 2, Vertical = true, Line = ox - w, From = oy + 2, Width = 1 };
                    break;
            }
            l.Rooms.Add(entrance);
            l.Rooms.Add(room);
            l.Rooms.Add(beyond);
            l.Doors.Add(doorIn);
            l.Doors.Add(doorOut);
            return l;
        }

        /// <summary>A ground point <paramref name="depth"/> m straight in from the door's middle, into the room (negative: back out through it).</summary>
        static LevelPoint In(LevelLayout l, LevelRoom room, LevelDoor door, double depth) => RoomDoorRules.PullInPlace(l, room, door, depth, 0d);

        static List<LevelDoor> Doors(LevelLayout l, LevelRoom room)
        {
            var list = new List<LevelDoor>();
            RoomDoorRules.DoorsOf(l, room, list);
            return list;
        }

        static readonly int[] Sides = { LevelGenerator.North, LevelGenerator.East, LevelGenerator.South, LevelGenerator.West };

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnByDefault_InTheFightsValues_WithItsFirstGuesses_AndTheGeneratorNeverReadsIt()
        {
            var v = new RoomFightValues();
            Assert.That(v.Doors, Is.True, "on by default");
            Assert.That(v.WakeByLineOrHurtOnly, Is.True);
            Assert.That((v.DoorClear, v.DoorPullAfter, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo((1.5f, 2f, 0.35f, 0.8f)));
            Assert.That(v.DoorCloseSound, Is.EqualTo(PlaceholderSound.Strike), "a sound that exists");
            Assert.That(RoomDoorRules.On(v) && RoomDoorRules.WakesOnlyByLineOrHurt(v));
            Assert.That(RoomDoorRules.On(null), Is.False);
            Assert.That(RoomDoorRules.WakesOnlyByLineOrHurt(new RoomFightValues { Doors = false }), Is.False, "the doors off: the wake by range as on main");
            Assert.That(new LevelValues().Fights.Doors, Is.True, "the floors' own");
            // The doors are the floor's, not the generator's: its floors are the same with them off.
            var on = new LevelValues();
            var off = new LevelValues();
            off.Fights.Doors = false;
            foreach (int floor in new[] { 1, 3, 5 })
                for (ulong s = 0; s < 4; s++)
                    Assert.That(LevelGenerator.Generate(s, floor, off).Describe(), Is.EqualTo(LevelGenerator.Generate(s, floor, on).Describe()), "seed " + s + " floor " + floor);
        }

        // ---------------- Inside ----------------

        [Test]
        public void Inside_IsOnTheRoomsFloor_AtLeastTheClearanceFromEveryDoor_ADoorOnEachSide([ValueSource(nameof(Sides))] int side)
        {
            LevelLayout l = Line(side, 7, 7, out LevelRoom room, out LevelDoor doorIn, out LevelDoor doorOut);
            List<LevelDoor> doors = Doors(l, room);
            Assert.That(doors, Is.EquivalentTo(new[] { doorIn, doorOut }), "the way in and the way on");
            double clear = new RoomFightValues().DoorClear;
            foreach ((double depth, bool inside) in new[] { (-3.0, false), (-0.1, false), (0.1, false), (1.0, false), (1.49, false), (1.5, true), (5.0, true), (12.0, true) })
                Assert.That(RoomDoorRules.IsInside(l, room, doors, In(l, room, doorIn, depth), clear), Is.EqualTo(inside), "from the door in, " + depth.ToString(Inv) + " m");
            // And the way on: in the room, but in its doorway.
            Assert.That(RoomDoorRules.IsInside(l, room, doors, In(l, room, doorOut, 1.0), clear), Is.False, "1 m in from the way on");
            Assert.That(RoomDoorRules.IsInside(l, room, doors, In(l, room, doorOut, 2.0), clear), Is.True, "2 m in from it");
            Assert.That(RoomDoorRules.IsInside(l, room, doors, In(l, room, doorOut, -2.0), clear), Is.False, "beyond it");
        }

        // ---------------- What the doors do ----------------

        [Test]
        public void TheOpenDoors_StayOpen_Close_Wait_OrPullIn_AndShutOnes_OpenWhenTheFightIsOver()
        {
            const float pull = 2f;
            Assert.That(RoomDoorRules.WhenOpen(false, true, true, true, 0, 0f, pull), Is.EqualTo(DoorStep.StayOpen), "the switch off");
            Assert.That(RoomDoorRules.WhenOpen(true, false, true, true, 0, 0f, pull), Is.EqualTo(DoorStep.StayOpen), "no fight on");
            Assert.That(RoomDoorRules.WhenOpen(true, true, false, true, 0, 0f, pull), Is.EqualTo(DoorStep.StayOpen), "she is dead");
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, false, 0, 9f, pull), Is.EqualTo(DoorStep.StayOpen), "she is not inside: never shut out");
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, true, 0, 0f, pull), Is.EqualTo(DoorStep.Close), "she and every one of them inside");
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, true, 1, 0f, pull), Is.EqualTo(DoorStep.Wait), "one of them not inside: they wait");
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, true, 2, 1.99f, pull), Is.EqualTo(DoorStep.Wait));
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, true, 2, 2f, pull), Is.EqualTo(DoorStep.PullIn), "after the wait: pulled in");

            Assert.That(RoomDoorRules.OpensNow(true, 3, true, true), Is.Null, "the fight on, she in it: they stay shut");
            Assert.That(RoomDoorRules.OpensNow(false, 0, true, true), Is.EqualTo(DoorOpened.Cleared), "the room cleared");
            Assert.That(RoomDoorRules.OpensNow(true, 0, true, true), Is.EqualTo(DoorOpened.Cleared), "the last one dead, the same frame");
            Assert.That(RoomDoorRules.OpensNow(false, 3, false, true), Is.EqualTo(DoorOpened.HerDeath), "she died");
            Assert.That(RoomDoorRules.OpensNow(true, 3, true, false), Is.EqualTo(DoorOpened.SheLeft), "she is found off its floor: never shut out");
            Assert.That(RoomDoorRules.Word(DoorOpened.Cleared) + "|" + RoomDoorRules.Word(DoorOpened.HerDeath) + "|" + RoomDoorRules.Word(DoorOpened.SheLeft)
                        + "|" + RoomDoorRules.Word(DoorOpened.EnemyOutside), Is.EqualTo("cleared|her death|she left|an enemy outside"));
        }

        [Test]
        public void APull_ClosesOnlyWithNoneLeftOutside_IsTriedAgainAfterItsRetry_AndOneOutsideShutDoors_OpensThemAfterItsTime()
        {
            var v = new RoomFightValues();
            Assert.That((v.DoorPullRetry, v.DoorCheckEvery, v.DoorOutsideMost), Is.EqualTo((0.5f, 0.25f, 1f)), "the second review's first guesses");
            Assert.That(RoomDoorRules.AfterPull(0), Is.EqualTo(DoorStep.Close), "every one inside after the pull: they close");
            Assert.That(RoomDoorRules.AfterPull(1), Is.EqualTo(DoorStep.Wait), "one left outside: they stay open, never close on a failed pull");
            Assert.That(RoomDoorRules.AfterPull(3), Is.EqualTo(DoorStep.Wait));
            Assert.That(RoomDoorRules.PullDue(10f, float.NegativeInfinity), Is.True, "the first pull");
            Assert.That(RoomDoorRules.PullDue(10.2f, 10f + v.DoorPullRetry), Is.False, "not again before its retry");
            Assert.That(RoomDoorRules.PullDue(10.5f, 10f + v.DoorPullRetry), Is.True, "again at its retry");
            Assert.That(RoomDoorRules.OpensForOneOutside(0, 9f, v.DoorOutsideMost), Is.False, "none outside: they stay shut");
            Assert.That(RoomDoorRules.OpensForOneOutside(1, 0.9f, v.DoorOutsideMost), Is.False, "one outside, not yet its time");
            Assert.That(RoomDoorRules.OpensForOneOutside(1, 1f, v.DoorOutsideMost), Is.True, "one outside a second, not put back: open");
            // The open doors' rule is as it was: the pull is the caller's to count again.
            Assert.That(RoomDoorRules.WhenOpen(true, true, true, true, 1, v.DoorPullAfter, v.DoorPullAfter), Is.EqualTo(DoorStep.PullIn));
        }

        /// <summary>One frame of the floor for one room: the line, the fight, then the doors, as GeneratedLevels steps them.</summary>
        sealed class Floor
        {
            public LevelLayout L;
            public LevelRoom Room;
            public LevelDoor DoorIn;
            public List<LevelDoor> Doors;
            public RoomFight Fight = new RoomFight(1);
            public RoomFightValues V = new RoomFightValues();
            public bool Shut;
            public float WaitingSince = -1f;
            public int Closings, Pulls;
            public float Now;

            /// <summary>She stands at a depth in from the door in; the enemies not inside; the time steps by <paramref name="dt"/>.</summary>
            public void Frame(double herDepth, int enemiesNotInside, float dt = 0.02f, int alive = 4)
            {
                Now += dt;
                LevelPoint her = In(L, Room, DoorIn, herDepth);
                if (L.RoomAt(her.X, her.Y) == Room && RoomFightRules.PastThePoint(L, Room, DoorIn, her, V)) Fight.Stir(RoomFightStart.Point, Now);
                if (RoomFightRules.Ends(true, alive, L.RoomAt(her.X, her.Y) == Room, true)) Fight.End();
                if (Shut)
                {
                    if (RoomDoorRules.OpensNow(Fight.On, alive, true, L.RoomAt(her.X, her.Y) == Room).HasValue) Shut = false;
                    return;
                }
                bool inside = RoomDoorRules.IsInside(L, Room, Doors, her, V.DoorClear);
                if (Fight.On && inside) { if (WaitingSince < 0f) WaitingSince = Now; }
                else WaitingSince = -1f;
                DoorStep step = RoomDoorRules.WhenOpen(V.Doors, Fight.On, true, inside, enemiesNotInside, WaitingSince >= 0f ? Now - WaitingSince : 0f, V.DoorPullAfter);
                if (step == DoorStep.PullIn) Pulls++;
                if (step == DoorStep.Close || step == DoorStep.PullIn)
                {
                    Shut = true;
                    Closings++;
                }
            }
        }

        static Floor FloorOf(int side)
        {
            var f = new Floor { L = Line(side, 7, 7, out LevelRoom room, out LevelDoor doorIn, out LevelDoor _) };
            f.Room = room;
            f.DoorIn = doorIn;
            f.Doors = Doors(f.L, room);
            return f;
        }

        [Test]
        public void TheWakeCases_AtTheLine_ShortOfItByAHurt_FromOutsideByAHurt_AndWithOneThatFollowedHerOut([ValueSource(nameof(Sides))] int side)
        {
            // At the line: she walks in from the passage; nothing at 4 m; at the line (5 m) the room wakes and the doors shut in that frame.
            Floor f = FloorOf(side);
            foreach (double d in new[] { -6.0, -2.0, 0.5, 1.5, 3.0, 4.9 }) f.Frame(d, 0);
            Assert.That(f.Fight.On || f.Shut, Is.False, "short of the line: asleep and open");
            f.Frame(5.0, 0);
            Assert.That(f.Fight.On && f.Shut, "at the line: awake, and shut in the same frame");
            f.Frame(1.0, 0);
            Assert.That(f.Shut, Is.True, "she walks back to the door: it stays shut");
            f.Frame(3.0, 0, alive: 0);
            Assert.That(f.Shut, Is.False, "cleared: open");

            // Short of the line, by a hurt (she shot one from 1 m in): open while she is in the doorway, shut once she is 1.5 m in.
            f = FloorOf(side);
            f.Frame(1.0, 0);
            f.Fight.Stir(RoomFightStart.Hit, f.Now);
            f.Frame(1.0, 0);
            Assert.That(f.Fight.On && !f.Shut, "awake, but she is in the doorway: open");
            f.Frame(1.6, 0);
            Assert.That(f.Shut, Is.True, "1.6 m in: shut");

            // From outside, by a hurt (a shot from the passage): open while she is out there, however long; shut once she is in.
            f = FloorOf(side);
            f.Frame(-4.0, 0);
            f.Fight.Stir(RoomFightStart.Hit, f.Now);
            for (int i = 0; i < 300; i++) f.Frame(-4.0, 0);
            Assert.That(f.Fight.On && !f.Shut, "awake, she in the passage: never shut out");
            f.Frame(2.0, 0);
            Assert.That(f.Shut, Is.True, "she steps in: shut behind her");

            // From outside, and two of them followed her out: she steps in, they wait for them, and pull them in after 2 s.
            f = FloorOf(side);
            f.Frame(-4.0, 0);
            f.Fight.Stir(RoomFightStart.Hit, f.Now);
            f.Frame(2.0, 2, 0.5f);
            Assert.That(f.Shut, Is.False, "two of them in the passage: the doors wait");
            f.Frame(2.0, 2, 1.0f);
            Assert.That(f.Shut, Is.False, "1 s");
            f.Frame(2.0, 1, 0.4f);
            Assert.That(f.Shut, Is.False, "one came in, 1.4 s");
            f.Frame(2.0, 1, 0.6f);
            Assert.That(f.Shut && f.Pulls == 1, "2 s: the last pulled in, and shut");
            Assert.That(f.Closings, Is.EqualTo(1));

            // The switch off: never shut, whatever she does.
            f = FloorOf(side);
            f.V.Doors = false;
            foreach (double d in new[] { -2.0, 3.0, 5.0, 8.0, 2.0 }) f.Frame(d, 0);
            Assert.That(f.Fight.On && !f.Shut, "off: the room wakes at its line, and nothing shuts");
        }

        // ---------------- The wake by range ----------------

        /// <summary>One step of an enemy's brain as Enemy.Act takes it since work order 086 (and 070's stirring).</summary>
        static void Step(EnemyBrain brain, RoomFight fight, float now, float distance, bool provoked, EnemyValues values)
        {
            if (fight != null && provoked) fight.Stir(RoomFightStart.Hit, now);
            bool held = fight != null && fight.On;
            float wakeDistance = brain.State == EnemyState.Idle && !held && !provoked && fight != null && fight.WakesOnlyByLineOrHurt ? float.PositiveInfinity : distance;
            EnemyState before = brain.State;
            brain.Step(now, wakeDistance, true, provoked, values, true, null, held);
            if (before == EnemyState.Idle && brain.State != EnemyState.Idle) fight?.Stir(RoomFightStart.Seen, now);
        }

        [Test]
        public void WithTheDoorsOn_ARoomsEnemyWakesByTheLineOrAHurt_NotByItsRange_AndWithThemOff_ByItsRangeAsBefore()
        {
            var values = new EnemyValues { AggroRange = 9f, LeashRange = 22f, Attacks = new EnemyAttackDefinition[0] };
            foreach (bool doors in new[] { true, false })
            {
                var fight = new RoomFight(1) { WakesOnlyByLineOrHurt = RoomDoorRules.WakesOnlyByLineOrHurt(new RoomFightValues { Doors = doors }) };
                var brain = new EnemyBrain();
                // She comes within its range (8 m, then 1 m): with the doors on it sleeps on, with them off it wakes the room.
                Step(brain, fight, 0f, 8f, false, values);
                Step(brain, fight, 0.02f, 1f, false, values);
                if (doors)
                {
                    Assert.That(brain.State == EnemyState.Idle && !fight.On, "the doors on: within its range, it sleeps on");
                    // The line: the floor stirs the fight; it wakes held, wherever she is.
                    fight.Stir(RoomFightStart.Point, 0.04f);
                    Step(brain, fight, 0.06f, 30f, false, values);
                    Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "the line wakes it, 30 m away");
                    // A hurt wakes another room's (a fresh one).
                    var other = new RoomFight(2) { WakesOnlyByLineOrHurt = true };
                    var hurt = new EnemyBrain();
                    Step(hurt, other, 0f, 15f, true, values);
                    Assert.That(hurt.State == EnemyState.Chase && other.On && other.StartedBy == RoomFightStart.Hit, "a hurt wakes it and its room");
                }
                else
                {
                    Assert.That(brain.State == EnemyState.Chase && fight.On && fight.StartedBy == RoomFightStart.Seen, "the doors off: it saw her by its range, as on main");
                }
            }
            // No fight (the practice room): by range, as ever.
            var lone = new EnemyBrain();
            Step(lone, null, 0f, 8f, false, values);
            Assert.That(lone.State, Is.EqualTo(EnemyState.Chase));
        }

        // ---------------- The block ----------------

        [Test]
        public void TheBlock_FillsTheOpening_TheWallsThickness_FullHighOnAVerticalLine_NearWallHighOnAHorizontalOne([ValueSource(nameof(Sides))] int side)
        {
            var v = new LevelValues();
            LevelLayout l = Line(side, 7, 7, out LevelRoom _, out LevelDoor doorIn, out LevelDoor _);
            DoorBlock b = RoomDoorRules.Block(l, doorIn, v.WallHeight, v.NearWallHeight, v.WallThickness);
            doorIn.Opening(Cs, out LevelPoint a, out LevelPoint e);
            Assert.That(b.Centre.X, Is.EqualTo((a.X + e.X) * 0.5).Within(1e-9));
            Assert.That(b.Centre.Y, Is.EqualTo((a.Y + e.Y) * 0.5).Within(1e-9));
            Assert.That(b.Vertical, Is.EqualTo(doorIn.Vertical));
            Assert.That(b.Length, Is.EqualTo(doorIn.Width * Cs + v.WallThickness).Within(1e-9), "the opening and half a wall into each end");
            Assert.That(b.Length, Is.GreaterThan(doorIn.Width * Cs - v.WallThickness), "wider than the gap the wall boxes leave");
            Assert.That(b.Thickness, Is.EqualTo(v.WallThickness).Within(1e-9));
            Assert.That(b.Height, Is.EqualTo(doorIn.Vertical ? v.WallHeight : v.NearWallHeight).Within(1e-9));
            Assert.That(b.Thickness * 0.5 + 0.6, Is.LessThan(new RoomFightValues().DoorClear), "half the block and her body are short of the clearance: never caught in it");
        }

        [Test]
        public void TheBlock_RisesOverItsSeconds_AndSinksOverItsOwn()
        {
            var v = new RoomFightValues();
            Assert.That(RoomDoorRules.Raised(true, 0f, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(0f));
            Assert.That(RoomDoorRules.Raised(true, v.DoorRiseSeconds * 0.5f, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(RoomDoorRules.Raised(true, 5f, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(1f));
            Assert.That(RoomDoorRules.Raised(false, 0f, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(1f), "opening: up still");
            Assert.That(RoomDoorRules.Raised(false, v.DoorSinkSeconds * 0.25f, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(RoomDoorRules.Raised(false, v.DoorSinkSeconds, v.DoorRiseSeconds, v.DoorSinkSeconds), Is.EqualTo(0f), "sunk");
            Assert.That(RoomDoorRules.Raised(true, 0f, 0f, 0f), Is.EqualTo(1f), "no time: at once");
            Assert.That(v.DoorSinkSeconds, Is.GreaterThan(v.DoorRiseSeconds), "the way on is seen opening, slower than it shut");
        }

        [Test]
        public void AnEnemyPulledIn_StandsInside_InFromTheDoorNearestIt([ValueSource(nameof(Sides))] int side)
        {
            var v = new RoomFightValues();
            LevelLayout l = Line(side, 7, 7, out LevelRoom room, out LevelDoor doorIn, out LevelDoor doorOut);
            List<LevelDoor> doors = Doors(l, room);
            Assert.That(RoomDoorRules.NearestOf(l, doors, In(l, room, doorIn, -3.0)), Is.SameAs(doorIn), "from the passage before");
            Assert.That(RoomDoorRules.NearestOf(l, doors, In(l, room, doorOut, -3.0)), Is.SameAs(doorOut), "from the one after");
            foreach (LevelDoor door in doors)
                foreach (double along in new[] { 0.0, 1.0, -1.0 })
                    Assert.That(RoomDoorRules.IsInside(l, room, doors, RoomDoorRules.PullInPlace(l, room, door, v.DoorClear + 1.0, along), v.DoorClear), Is.True,
                                "pulled in through " + (door == doorIn ? "the way in" : "the way on") + ", " + along.ToString(Inv) + " m along");
        }

        // ---------------- On the floors ----------------

        [Test]
        public void OnTheFloors_EveryEnemyStandsInsideForItsRoomsDoors_SoTheDoorsShutInTheFrameTheRoomWakesAtItsLine()
        {
            var v = new LevelValues();
            double clear = v.Fights.DoorClear;
            int rooms = 0, lines = 0, enemies = 0;
            foreach (int floor in new[] { 1, 2, 3, 4, 5 })
                for (ulong seed = 0; seed < 60; seed++)
                {
                    LevelLayout l = LevelGenerator.Generate(seed * 7919UL + 13UL, floor, v);
                    foreach (LevelRoom room in l.Rooms)
                    {
                        if (!RoomFightRules.HoldsAFight(l, room, room.EnemyCount)) continue;
                        rooms++;
                        List<LevelDoor> doors = Doors(l, room);
                        Assert.That(doors.Count, Is.GreaterThanOrEqualTo(1), "a room of a fight has a door");
                        for (int i = 0; i < room.EnemyPoints.Count; i++)
                            foreach (LevelPoint p in room.PlacesAt(i))
                            {
                                enemies++;
                                Assert.That(RoomDoorRules.NearestDoor(l, doors, p), Is.GreaterThanOrEqualTo(clear - 1e-6),
                                            "floor " + floor + " seed " + seed + " " + room.Letter + room.Order + ": an enemy " + RoomDoorRules.NearestDoor(l, doors, p).ToString("0.00", Inv) + " m from a door");
                            }
                        // Her at the line, straight in from the middle of the door in: 1.5 m from every door, where that is the room's floor.
                        LevelDoor entry = LevelGeometry.EntryDoor(l, room);
                        if (entry == null) continue;
                        LevelPoint at = In(l, room, entry, RoomFightRules.PointDepth(l, room, entry, v.Fights) + 0.01);
                        if (l.RoomAt(at.X, at.Y) != room) continue;
                        lines++;
                        Assert.That(RoomDoorRules.IsInside(l, room, doors, at, clear), Is.True, "floor " + floor + " seed " + seed + " " + room.Letter + room.Order + ": at the line she is inside");
                    }
                }
            Assert.That(rooms, Is.GreaterThan(500));
            Assert.That(lines, Is.GreaterThan(rooms / 2), "the line's point from the door's middle is on the floor in most rooms");
            TestContext.WriteLine("ROOMDOORS rooms " + rooms + ", their line's point on the floor " + lines + ", enemies " + enemies);
        }

        // ---------------- The summary ----------------

        static SessionEvent Door(double t, string name, int floor, int room, float seconds = -1f, string why = null)
        {
            var e = new SessionEvent(t, SessionEventTypes.RoomDoor, name);
            e.AddExtra("floor", floor.ToString(Inv));
            e.AddExtra("room", room.ToString(Inv));
            e.AddExtra("order", (room * 2).ToString(Inv));
            if (seconds >= 0f) e.AddExtra("seconds", seconds.ToString("0.####", Inv));
            if (why != null) e.AddExtra("why", why);
            return e;
        }

        static SessionEvent Fight(double t, int floor, int room)
        {
            var e = new SessionEvent(t, SessionEventTypes.RoomFight, "point");
            e.AddExtra("floor", floor.ToString(Inv));
            e.AddExtra("room", room.ToString(Inv));
            e.AddExtra("order", (room * 2).ToString(Inv));
            e.AddExtra("kind", "combat");
            e.AddExtra("inside", "true");
            e.AddExtra("woke", "5");
            return e;
        }

        [Test]
        public void TheSummary_GivesEachFloorsDoorsClosed_AndHowLongTheyStood_MeanAndLongest()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                new SessionEvent(1, SessionEventTypes.RunStart, "RUNSEED"),
                Fight(10, 1, 2), Door(10, "closed", 1, 2), Door(30, "opened", 1, 2, 20f, "cleared"),
                Fight(40, 1, 3), Door(40, "closed", 1, 3), Door(80, "opened", 1, 3, 40f, "cleared"),
                Fight(100, 2, 2), Door(100, "closed", 2, 2),
                new SessionEvent(120, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            RoomFightCounts one = stats.Runs[0].RoomFights[1], two = stats.Runs[0].RoomFights[2];
            Assert.That(one.DoorsClosed, Is.EqualTo(2));
            Assert.That(one.DoorSeconds, Is.EqualTo(new[] { 20f, 40f }));
            Assert.That(two.DoorsClosed, Is.EqualTo(1));
            Assert.That(two.DoorSeconds, Is.Empty);
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("floor 1: 2 at the point, 0 inside short of it, 0 from outside (0 hit, 0 seen), most woke at once 5, doors closed 2 (closing to opening mean 30.00 s, longest 40.00 s)", text);
            StringAssert.Contains("floor 2: 1 at the point, 0 inside short of it, 0 from outside (0 hit, 0 seen), most woke at once 5, doors closed 1 (none opened yet)", text);
            // A log with no door (from before, or the switch off): the line as it was.
            events.RemoveAll(e => e.Type == SessionEventTypes.RoomDoor);
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events)), Does.Not.Contain("doors closed"));
        }
    }
}
