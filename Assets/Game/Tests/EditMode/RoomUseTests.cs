using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// How much of the room she uses (work order 097, Kristian the morning of 2026-11-23: "skulle ønske man fikk brukt mer av rommet. nå
    /// blir det bare at man kiter det rundt seg"; <see cref="RoomUseRules"/>, <see cref="RoomSpace"/>, SessionSummaryRoomUse.cs). The
    /// room's own metres from a door on each wall; the room's walkable floor on its 2 m grid, its props and covers, and its line in the
    /// log read back; the sample rule, at its bounds and with a float's sum; each measure from made-up places (a spot, a ring, a line to
    /// the far wall, the whole floor), the covers and the deaths; the summary's table, its medians, its one line and its maps, the same
    /// from the log's lines; and nothing new for a log without them.
    /// </summary>
    public class RoomUseTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const float Cs = 4f;

        /// <summary>The entrance and a combat room <paramref name="w"/> by <paramref name="h"/> cells on its <paramref name="side"/>, one door between (as RoomFightsTests).</summary>
        static LevelLayout TwoRooms(int side, int w, int h, out LevelRoom room, out LevelDoor door)
        {
            var l = new LevelLayout { Seed = 1UL, CellSize = Cs, GridCells = 48, Floor = 1, InALine = true };
            var entrance = new LevelRoom { Kind = RoomKind.Entrance, Order = 0 };
            room = new LevelRoom { Kind = RoomKind.Combat, Order = 1 };
            const int ox = 20, oy = 20;
            entrance.Rect = new CellRect(ox, oy, 4, 4);
            switch (side)
            {
                case LevelGenerator.North:
                    room.Rect = new CellRect(ox, oy + 4, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy + 4, From = ox + 1, Width = 1 };
                    break;
                case LevelGenerator.South:
                    room.Rect = new CellRect(ox, oy - h, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = oy, From = ox + 1, Width = 1 };
                    break;
                case LevelGenerator.East:
                    room.Rect = new CellRect(ox + 4, oy, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox + 4, From = oy + 1, Width = 1 };
                    break;
                default:
                    room.Rect = new CellRect(ox - w, oy, w, h);
                    door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = ox, From = oy + 1, Width = 1 };
                    break;
            }
            l.Rooms.Add(entrance);
            l.Rooms.Add(room);
            l.Doors.Add(door);
            return l;
        }

        /// <summary>A plain room in its own metres: <paramref name="rows"/> by <paramref name="cols"/> cells, all floor, the door 4 m wide in the middle.</summary>
        static RoomSpace Plain(int rows = 10, int cols = 10)
        {
            var s = new RoomSpace { Rows = rows, Cols = cols, Depth = rows * 2, Left = -cols, DoorWidth = 4, Walkable = new bool[rows * cols] };
            for (int i = 0; i < s.Walkable.Length; i++) s.Walkable[i] = true;
            return s;
        }

        /// <summary>Places every 0.25 s from <paramref name="from"/> to <paramref name="to"/> s along a path of time.</summary>
        static List<RoomPlace> Along(double from, double to, Func<double, (double d, double s)> path)
        {
            var list = new List<RoomPlace>();
            for (double t = from; t <= to + 1e-9; t += 0.25)
            {
                (double d, double s) = path(t - from);
                list.Add(new RoomPlace(t, d, s));
            }
            return list;
        }

        static string SideWord(int side) => side == LevelGenerator.North ? "north" : side == LevelGenerator.South ? "south" : side == LevelGenerator.East ? "east" : "west";

        // ---------------- The room's own metres ----------------

        [TestCase(LevelGenerator.North)]
        [TestCase(LevelGenerator.East)]
        [TestCase(LevelGenerator.South)]
        [TestCase(LevelGenerator.West)]
        public void TheFrame_FromADoorOnEachWall_StraightInIsDepth_SideIsToHerRight_AndBackAgain(int side)
        {
            LevelLayout l = TwoRooms(side, 6, 5, out LevelRoom room, out LevelDoor door);
            RoomFrame f = RoomUseRules.FrameOf(l, room, door);
            string what = "the room " + SideWord(side) + " of the entrance";
            door.Opening(Cs, out LevelPoint a, out LevelPoint b);
            Assert.That((f.OriginX, f.OriginY), Is.EqualTo(((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5)), "the origin is the door's middle: " + what);
            // Straight in: depth, no side; and the point is in the room.
            LevelPoint inside = f.FromFrame(3, 0);
            Assert.That(l.RoomAt(inside.X, inside.Y), Is.SameAs(room), "3 m in is in the room: " + what);
            Assert.That(RoomFightRules.DepthIn(l, room, door, inside), Is.EqualTo(3).Within(1e-9), "as the room's fight measures depth: " + what);
            // Her right as she faces in: the way in turned a quarter clockwise; never a mirror.
            Assert.That((f.RightX, f.RightY), Is.EqualTo((f.InY, -f.InX)), "to her right: " + what);
            foreach ((double d, double s) in new[] { (0.0, 0.0), (5.5, -3.25), (17.0, 9.0) })
            {
                LevelPoint g = f.FromFrame(d, s);
                f.ToFrame(g.X, g.Y, out double d2, out double s2);
                Assert.That(d2, Is.EqualTo(d).Within(1e-9), what);
                Assert.That(s2, Is.EqualTo(s).Within(1e-9), what);
            }
            // Moved to another origin, the same place moves with it.
            RoomFrame moved = f.MovedTo(f.OriginX + 100, f.OriginY - 50);
            moved.ToFrame(inside.X + 100, inside.Y - 50, out double dm, out double sm);
            Assert.That(dm, Is.EqualTo(3).Within(1e-9));
            Assert.That(sm, Is.EqualTo(0).Within(1e-9));
        }

        // ---------------- The room as she meets it ----------------

        [TestCase(LevelGenerator.North)]
        [TestCase(LevelGenerator.East)]
        [TestCase(LevelGenerator.South)]
        [TestCase(LevelGenerator.West)]
        public void ThePlainRoom_IsAllFloor_OnA2mGrid_ItsRowsFromTheDoorIn_AndItsLeftWallLeftOfTheDoor(int side)
        {
            LevelLayout l = TwoRooms(side, 6, 5, out LevelRoom room, out LevelDoor door);
            RoomSpace s = RoomSpace.Make(l, room, door, RoomUseRules.FrameOf(l, room, door), new List<LevelProp>());
            double depth = RoomFightRules.Depth(l, room, door);
            double across = (side == LevelGenerator.North || side == LevelGenerator.South ? 6 : 5) * Cs;
            Assert.That(s.Depth, Is.EqualTo(depth).Within(1e-9));
            Assert.That((s.Rows, s.Cols), Is.EqualTo(((int)(depth / 2), (int)(across / 2))), SideWord(side));
            Assert.That(s.Left, Is.LessThan(0).And.GreaterThan(-across), "the door is in the wall, its left wall to her left");
            Assert.That(s.WalkableCount, Is.EqualTo(s.Rows * s.Cols), "all floor");
            Assert.That(s.FloorDepth, Is.EqualTo(depth).Within(1e-9));
            Assert.That(s.DoorWidth, Is.EqualTo(4).Within(1e-9));
            Assert.That(s.Covers, Is.Empty);
        }

        [TestCase(LevelGenerator.North)]
        [TestCase(LevelGenerator.West)]
        public void AWideProp_BlocksItsCell_ASmallOneDoesNot_OneWalkedOverBlocksNothing_AndTheCoversAreListedInTheFrame(int side)
        {
            LevelLayout l = TwoRooms(side, 6, 5, out LevelRoom room, out LevelDoor door);
            RoomFrame f = RoomUseRules.FrameOf(l, room, door);
            RoomSpace plain = RoomSpace.Make(l, room, door, f, new List<LevelProp>());
            // A coin heap (2 m by 1.85 m, low) in the middle of row 3, column 2; a statue (tall, about 1 m) in row 6, column 4; a dais walked over.
            LevelPoint heapAt = f.FromFrame(3 * 2 + 1, plain.Left + 2 * 2 + 1), statueAt = f.FromFrame(6 * 2 + 1, plain.Left + 4 * 2 + 1);
            var props = new List<LevelProp>
            {
                LevelProp.Make(PropModel.CoinHeap, heapAt, 0, PropPlace.Floor),
                LevelProp.Make(PropModel.Statue, statueAt, 0, PropPlace.Floor),
                LevelProp.Make(PropModel.Dais, f.FromFrame(1 * 2 + 1, plain.Left + 1 * 2 + 1), 0, PropPlace.Floor),
            };
            RoomSpace s = RoomSpace.Make(l, room, door, f, props);
            Assert.That(s.IsWalkable(3, 2), Is.False, "the coin heap fills its cell");
            Assert.That(s.IsWalkable(6, 4), Is.True, "the statue leaves most of its cell");
            Assert.That(s.IsWalkable(1, 1), Is.True, "the dais is walked over");
            Assert.That(s.WalkableCount, Is.EqualTo(s.Rows * s.Cols - 1));
            Assert.That(s.Covers.Select(c => c.Tall), Is.EquivalentTo(new[] { false, true }), "the heap low, the statue tall; the dais none");
            RoomCover heap = s.Covers.Single(c => !c.Tall);
            Assert.That(heap.Footprint.Centre.Y, Is.EqualTo(7).Within(1e-9), "its depth");
            Assert.That(heap.Footprint.Centre.X, Is.EqualTo(plain.Left + 5).Within(1e-9), "its side");
            Assert.That(s.CellOf(heap.Footprint.Centre.Y, heap.Footprint.Centre.X, out int row, out int col) && row == 3 && col == 2, Is.True);
        }

        [Test]
        public void AProp_TurnedWithTheFrame_KeepsItsLongSideAlongTheSameGround()
        {
            // A sarcophagus 1.9 m long east-west, unturned; the door on the room's west wall: east is the way in, so its long side runs in depth.
            LevelLayout l = TwoRooms(LevelGenerator.East, 6, 5, out LevelRoom room, out LevelDoor door);
            RoomFrame f = RoomUseRules.FrameOf(l, room, door);
            LevelPoint at = f.FromFrame(10, 2);
            var props = new List<LevelProp> { LevelProp.Make(PropModel.Sarcophagus, at, 0, PropPlace.Floor) };
            RoomCover c = RoomSpace.Make(l, room, door, f, props).Covers.Single();
            Assert.That(c.Footprint.DistanceTo(new LevelPoint(2, 10 + 0.9)), Is.EqualTo(0).Within(1e-9), "0.9 m deeper is inside it");
            Assert.That(c.Footprint.DistanceTo(new LevelPoint(2 + 0.9, 10)), Is.GreaterThan(0.3), "0.9 m to her right is outside it");
            // And the ground's own: the same two points on the ground, by the prop's own footprint.
            Assert.That(props[0].Footprint.DistanceTo(f.FromFrame(10 + 0.9, 2)), Is.EqualTo(0).Within(1e-9));
            Assert.That(props[0].Footprint.DistanceTo(f.FromFrame(10, 2 + 0.9)), Is.GreaterThan(0.3));
        }

        [Test]
        public void TheRoomsLine_WrittenAndReadBack_IsTheSameRoom()
        {
            LevelLayout l = TwoRooms(LevelGenerator.West, 6, 5, out LevelRoom room, out LevelDoor door);
            RoomFrame f = RoomUseRules.FrameOf(l, room, door);
            var props = new List<LevelProp>
            {
                LevelProp.Make(PropModel.CoinHeap, f.FromFrame(7, 1), 30, PropPlace.Floor),
                LevelProp.Make(PropModel.Pillar, f.FromFrame(13, -3), 0, PropPlace.Floor),
            };
            RoomSpace s = RoomSpace.Make(l, room, door, f, props);
            var e = new SessionEvent(12.5, RoomUseEvents.Room);
            s.WriteTo(e);
            SessionEvent back = SessionEvent.Parse(e.ToJson());
            RoomSpace r = RoomSpace.From(back);
            Assert.That(r, Is.Not.Null);
            Assert.That((r.Rows, r.Cols, r.Depth, r.Left, r.DoorWidth), Is.EqualTo((s.Rows, s.Cols, s.Depth, s.Left, s.DoorWidth)));
            Assert.That(r.Walkable, Is.EqualTo(s.Walkable));
            Assert.That(r.Covers.Count, Is.EqualTo(2));
            for (int i = 0; i < 2; i++)
            {
                PropFootprint a = s.Covers[i].Footprint, b = r.Covers[i].Footprint;
                Assert.That(r.Covers[i].Tall, Is.EqualTo(s.Covers[i].Tall));
                Assert.That(b.Shape, Is.EqualTo(a.Shape));
                Assert.That(b.Centre.X, Is.EqualTo(a.Centre.X).Within(0.01));
                Assert.That(b.Centre.Y, Is.EqualTo(a.Centre.Y).Within(0.01));
                Assert.That(b.HalfWidth, Is.EqualTo(a.HalfWidth).Within(0.01));
                Assert.That(b.HalfDepth, Is.EqualTo(a.HalfDepth).Within(0.01));
                Assert.That(b.Radius, Is.EqualTo(a.Radius).Within(0.01));
                if (a.Shape == PropShape.Rectangle) Assert.That(((b.Rotation % 360) + 360) % 360, Is.EqualTo(((a.Rotation % 360) + 360) % 360), "a rectangle's turn");
            }
            Assert.That(RoomSpace.From(new SessionEvent(1, RoomUseEvents.Place)), Is.Null, "another event holds no room");
            TestContext.WriteLine("ROOMUSE a room's line: " + e.ToJson().Length + " characters (" + s.Rows + " by " + s.Cols + " cells, 2 covers)");
        }

        // ---------------- The sample rule ----------------

        [Test]
        public void TheSampleRule_AQuarterSecondAndMoreThanHalfAMetre_BothAsked_AndTheBoundsCountWithTheSlack()
        {
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 10.3, 5.6, 0), Is.True, "0.3 s and 0.6 m");
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 10.2, 7.0, 0), Is.False, "too soon, however far");
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 30.0, 5.4, 0), Is.False, "too near, however long");
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 10.3, 5.3, 0.39), Is.False, "under 0.5 m, by both axes");
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 10.3, 5.3, 0.41), Is.True, "0.5 m and more, by both axes");
            Assert.That(RoomUseRules.ShouldWrite(10, 5, 0, 10.25, 5.5, 0), Is.True, "the bounds themselves count");
            // A time summed as floats (Unity's runtime adds them wider): the quarter second after 0.1 s, whichever way it is added.
            float last = 0.1f, now = last + 0.25f;
            Assert.That(RoomUseRules.ShouldWrite(last, 0, 0, now, 0.5f, 0), Is.True, "a float's sum at the bound");
            Assert.That(RoomUseRules.ShouldWrite((double)last, 0, 0, (double)last + (double)0.25f, 0.5, 0), Is.True, "and a double's");
        }

        // ---------------- The measures ----------------

        [Test]
        public void ASpot_OneCell_ASmallCircle_LittleTravel()
        {
            RoomSpace s = Plain();
            // 20 s within 0.4 m of the middle of row 5, column 5 (depth 11, side 1).
            List<RoomPlace> places = Along(0, 20, t => (11 + 0.4 * Math.Sin(t * 3), 1 + 0.4 * Math.Cos(t * 2)));
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, 20, null);
            Assert.That(m.Seconds, Is.EqualTo(20));
            Assert.That((m.CellsUsed, m.CellsWalkable), Is.EqualTo((1, 100)));
            Assert.That(m.UsedShare, Is.EqualTo(0.01).Within(1e-9));
            Assert.That(m.Circle, Is.LessThan(0.6), "the circle round a spot");
            Assert.That(m.MedianDepth, Is.EqualTo(11).Within(0.4));
            Assert.That(m.Farthest, Is.EqualTo(11.4).Within(0.05));
            Assert.That(m.FarthestShare, Is.EqualTo(11.4 / 20).Within(0.01));
            Assert.That(m.Deaths, Is.EqualTo(0));
            Assert.That(double.IsNaN(m.DeathsDepth), Is.True, "no death, no depth");
            TestContext.WriteLine(string.Format(Inv, "ROOMUSE a spot: used {0:P0}, circle {1:0.00} m, travel {2:0.0} m", m.UsedShare, m.Circle, m.Travel));
        }

        [Test]
        public void ARing_ItsRadiusIsTheCircle_ALongTravel_AndTheCellsOfTheRing()
        {
            RoomSpace s = Plain();
            const double r = 4, speed = 6; // m, m/s: kiting round a spot at a run
            List<RoomPlace> places = Along(0, 20, t => (11 + r * Math.Sin(t * speed / r), 1 + r * Math.Cos(t * speed / r)));
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, 20, null);
            Assert.That(m.Circle, Is.EqualTo(r).Within(0.2), "the circle is the ring's");
            Assert.That(m.Travel, Is.EqualTo(speed * 20).Within(1.5), "20 s at 6 m/s (the chords a quarter second apart, a little short)");
            Assert.That(m.MedianDepth, Is.EqualTo(11).Within(0.5));
            Assert.That(m.FarthestShare, Is.EqualTo(15.0 / 20).Within(0.01));
            // The ring of radius 4 crosses about 2 pi 4 / 2 m cells and some: between 12 and 20 of the hundred, none in its middle.
            Assert.That(m.CellsUsed, Is.InRange(12, 20));
            Assert.That(s.CellOf(11, 1, out int row, out int col) && !m.CellUsed[row * s.Cols + col], Is.True, "not its middle");
            TestContext.WriteLine(string.Format(Inv, "ROOMUSE a ring of 4 m: used {0:P0} ({1} cells), circle {2:0.00} m, travel {3:0.0} m", m.UsedShare, m.CellsUsed, m.Circle, m.Travel));
        }

        [Test]
        public void ALineToTheFarWall_FarthestIsTheWall_TheMedianItsMiddle_AndTheCircleNinetyPercentOfItsHalf()
        {
            RoomSpace s = Plain();
            const double speed = 6;
            List<RoomPlace> places = Along(0, 3, t => (1 + speed * t, 1)); // from 1 m in to 19 m in, at side 1: column 5
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, 3, null);
            Assert.That(m.Farthest, Is.EqualTo(19).Within(1e-9));
            Assert.That(m.FarthestShare, Is.EqualTo(0.95).Within(1e-9));
            // A place holds until the next (she may stand there): at 6 m/s that is 1.5 m behind her at most, within a cell.
            Assert.That(m.MedianDepth, Is.EqualTo(10).Within(2), "the middle, within a cell");
            Assert.That(m.MedianDepthShare, Is.EqualTo(0.5).Within(0.1));
            Assert.That(m.Circle, Is.EqualTo(0.9 * 9).Within(2), "90 % of the time within 8.1 m of the middle, within a cell");
            Assert.That(m.Travel, Is.EqualTo(18).Within(1e-6));
            Assert.That(m.CellsUsed, Is.EqualTo(10), "the ten cells of column 5");
            Assert.That(Enumerable.Range(0, 10).All(rw => m.CellUsed[rw * s.Cols + 5]), Is.True);
        }

        [Test]
        public void TheWholeFloor_WalkedRowByRow_IsAllOfIt_AndACellThatIsNotFloorIsNotCounted()
        {
            RoomSpace s = Plain();
            s.Walkable[4 * s.Cols + 7] = false; // a cell a prop fills
            var places = new List<RoomPlace>();
            double t = 0;
            for (int row = 0; row < s.Rows; row++)
            {
                double side = row % 2 == 0 ? -9 : 9, to = -side;
                places.Add(new RoomPlace(t, row * 2 + 1, side));
                t += 3;
                places.Add(new RoomPlace(t, row * 2 + 1, to));
                t += 0.5;
            }
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, t, null);
            Assert.That(m.CellsWalkable, Is.EqualTo(99));
            Assert.That(m.CellsUsed, Is.EqualTo(99), "every cell of floor, the one not floor not counted");
            Assert.That(m.UsedShare, Is.EqualTo(1).Within(1e-9));
            Assert.That(m.FarthestShare, Is.EqualTo(19.0 / 20).Within(1e-9));
        }

        [Test]
        public void ACoverSheStoodAtForTwoSeconds_IsUsed_OnePassedInHalfASecondIsNot_NorOneShewasNeverNear()
        {
            RoomSpace s = Plain();
            s.Covers.Add(new RoomCover { Tall = true, Footprint = new PropFootprint { Shape = PropShape.Circle, Centre = new LevelPoint(0, 10), Radius = 0.5 } });
            s.Covers.Add(new RoomCover { Tall = false, Footprint = new PropFootprint { Shape = PropShape.Circle, Centre = new LevelPoint(-6, 4), Radius = 0.5 } });
            s.Covers.Add(new RoomCover { Tall = false, Footprint = new PropFootprint { Shape = PropShape.Rectangle, Centre = new LevelPoint(7, 16), HalfWidth = 1, HalfDepth = 0.5 } });
            var places = new List<RoomPlace>
            {
                new RoomPlace(0, 10, 1.5),   // 1 m from the first's edge: 2 s
                new RoomPlace(2, 4, -4.5),   // 1 m from the second's edge: 0.5 s
                new RoomPlace(2.5, 12, 4),   // in the open, far from the third
                new RoomPlace(5, 12, 4.5),
            };
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, 6, null);
            Assert.That((m.CoversUsed, m.Covers), Is.EqualTo((1, 3)));
        }

        [Test]
        public void TheDeaths_TheirMedianDepth_AndItsShareOfTheRoom()
        {
            RoomSpace s = Plain();
            var deaths = new List<RoomPlace> { new RoomPlace(3, 2, 0), new RoomPlace(4, 4, 1), new RoomPlace(5, 18, -2) };
            RoomUseMeasures m = RoomUseRules.Measure(s, new List<RoomPlace> { new RoomPlace(0, 3, 0) }, 0, 6, deaths);
            Assert.That(m.Deaths, Is.EqualTo(3));
            Assert.That(m.DeathsDepth, Is.EqualTo(4));
            Assert.That(m.DeathsDepthShare, Is.EqualTo(0.2).Within(1e-9));
        }

        // ---------------- The summary ----------------

        /// <summary>A fight's events: its room_fight and fight_room at <paramref name="t"/>, a ring of places, two deaths, its end.</summary>
        static List<SessionEvent> AFight(double t, int floor, int room, string kind, double seconds, double ringRadius)
        {
            var events = new List<SessionEvent>();
            var fight = new SessionEvent(t, SessionEventTypes.RoomFight, "point");
            fight.AddExtra("floor", floor.ToString(Inv));
            fight.AddExtra("room", room.ToString(Inv));
            fight.AddExtra("order", (2 * room).ToString(Inv));
            fight.AddExtra("kind", kind);
            fight.AddExtra("inside", "true");
            fight.AddExtra("woke", "7");
            events.Add(fight);
            var r = new SessionEvent(t, RoomUseEvents.Room);
            foreach (string k in new[] { "floor", "room", "order", "kind" }) r.AddExtra(k, fight.ExtraValue(k));
            r.AddExtra("template", "nave");
            RoomSpace space = Plain();
            space.Covers.Add(new RoomCover { Tall = true, Footprint = new PropFootprint { Shape = PropShape.Circle, Centre = new LevelPoint(1, 7), Radius = 0.5 } });
            space.WriteTo(r);
            events.Add(r);
            foreach (RoomPlace p in Along(t, t + seconds, x => (11 + ringRadius * Math.Sin(x * 1.5), 1 + ringRadius * Math.Cos(x * 1.5))))
            {
                var e = new SessionEvent(p.T, RoomUseEvents.Place);
                e.AddExtra("d", p.Depth.ToString("0.0", Inv));
                e.AddExtra("s", p.Side.ToString("0.0", Inv));
                events.Add(e);
            }
            for (int i = 0; i < 2; i++)
            {
                var d = new SessionEvent(t + 1 + i, RoomUseEvents.Death) { EnemyId = 100 + i };
                d.AddExtra("d", (13 + i).ToString(Inv));
                d.AddExtra("s", "1.0");
                events.Add(d);
            }
            var end = new SessionEvent(t + seconds, RoomUseEvents.End) { Result = "cleared" };
            end.AddExtra("d", "11.0");
            end.AddExtra("s", (1 + ringRadius).ToString("0.0", Inv));
            events.Add(end);
            return events;
        }

        static List<SessionEvent> ASession()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                new SessionEvent(1, SessionEventTypes.RunStart, "ABCDEFGH"),
                new SessionEvent(1, SessionEventTypes.FloorEnter, "SEED0001"),
            };
            events[2].AddExtra("floor", "1");
            events.AddRange(AFight(5, 1, 2, "combat", 20, 4));
            events.AddRange(AFight(40, 1, 3, "combat", 30, 2));
            events.AddRange(AFight(80, 1, 5, "elite", 10, 6));
            // A sprint of 2 s that ends a second into the first fight, and one jump in it; a jump outside every fight.
            var sprint = new SessionEvent(6, SessionEventTypes.SprintEnd) { Result = "released" };
            sprint.AddExtra("seconds", "2");
            events.Add(sprint);
            events.Add(new SessionEvent(5.5, SessionEventTypes.SprintJump, "Coffin"));
            events.Add(new SessionEvent(35, SessionEventTypes.SprintJump, "Coffin"));
            events.Add(new SessionEvent(100, SessionEventTypes.RunEnd) { Result = "left" });
            events.Add(new SessionEvent(101, SessionEventTypes.SessionEnd));
            return events.OrderBy(e => e.T).ToList();
        }

        [Test]
        public void TheSummary_ATableARun_TheMediansByKind_TheOneLine_AndTheMapsOfTheLongest_TheSameFromTheLogsLines()
        {
            List<SessionEvent> events = ASession();
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.RoomUse.Count, Is.EqualTo(3));
            RoomUseRow first = stats.RoomUse[0];
            Assert.That((first.Run, first.Floor, first.Room, first.Kind, first.Template, first.Enemies, first.Ended), Is.EqualTo((1, 1, 2, "combat", "nave", 7, "cleared")));
            Assert.That(first.Measures.Seconds, Is.EqualTo(20).Within(1e-9));
            Assert.That(first.Measures.SprintSeconds, Is.EqualTo(1).Within(1e-9), "the second of the sprint inside the fight");
            Assert.That(first.Measures.Jumps, Is.EqualTo(1));
            Assert.That(first.Measures.Deaths, Is.EqualTo(2));
            Assert.That(first.Measures.DeathsDepth, Is.EqualTo(13));
            Assert.That(first.Measures.Circle, Is.EqualTo(4).Within(0.2));
            Assert.That(stats.RoomUse[1].Measures.Circle, Is.EqualTo(2).Within(0.2));

            List<RoomUseMedians> kinds = SessionSummary.RoomUseByKind(stats.RoomUse);
            Assert.That(kinds.Select(k => (k.Kind, k.Fights)), Is.EqualTo(new[] { ("combat", 2), ("elite", 1) }));
            Assert.That(kinds[0].Circle, Is.EqualTo(3).Within(0.2), "the median of 4 and 2");
            string line = SessionSummary.OneLine(stats.RoomUse);
            StringAssert.StartsWith("used a median of ", line);
            StringAssert.Contains("% of a combat room's floor; 90 % of a fight inside a circle of 3.", line);
            StringAssert.Contains(" m; farthest depth ", line);
            StringAssert.EndsWith("% of the room (2 combat room fights)", line);

            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Room use " + line, text);
            StringAssert.Contains("Room use in fights (work order 097)", text);
            StringAssert.Contains("Run ABCDEFGH", text);
            StringAssert.Contains("Medians by kind of room, whole session", text);
            StringAssert.Contains("The 3 longest fights, drawn", text);
            string[] lines = text.Replace("\r", "").Split('\n');
            Assert.That(lines.Count(l => l.StartsWith("    |", StringComparison.Ordinal)), Is.EqualTo(30), "three maps of ten rows");
            Assert.That(lines.Count(l => l.StartsWith("    +--", StringComparison.Ordinal) && l.Contains("^^")), Is.EqualTo(3), "each with its door in");
            Assert.That(lines.First(l => l.StartsWith("    +", StringComparison.Ordinal) && l.Contains("^^")).Trim(), Is.EqualTo("+--------^^^^--------+"), "the door 4 m wide in the middle of 20 m");
            Assert.That(lines.Any(l => l.StartsWith("    |", StringComparison.Ordinal) && l.Contains("x")), Is.True, "where they died");
            Assert.That(lines.Any(l => l.StartsWith("    |", StringComparison.Ordinal) && l.Contains("T")), Is.True, "the tall cover");
            Assert.That(lines.Any(l => l.StartsWith("    |", StringComparison.Ordinal) && l.Contains("@")), Is.True, "her heaviest cells");
            TestContext.WriteLine(text.Substring(text.IndexOf("Room use in fights", StringComparison.Ordinal)));

            // The same from the log's lines.
            List<SessionEvent> back = events.Select(e => SessionEvent.Parse(e.ToJson())).ToList();
            Assert.That(SessionSummary.Format(SessionSummary.Compute(back)), Is.EqualTo(text));
        }

        [Test]
        public void TheMap_TheFarWallOnTop_TheDoorBelow_HerTimeInThreeLevels_TheDeathsAndTheCovers()
        {
            RoomSpace s = Plain(3, 4); // 6 m deep, 8 m across, the door 4 m wide in the middle
            s.Walkable[2 * 4 + 3] = false;
            s.Covers.Add(new RoomCover { Tall = false, Footprint = new PropFootprint { Shape = PropShape.Circle, Centre = new LevelPoint(-3, 5), Radius = 0.4 } });
            var places = new List<RoomPlace> { new RoomPlace(0, 1, -1), new RoomPlace(8, 3, 1), new RoomPlace(9.5, 5, 1), new RoomPlace(9.9, 5, 3) };
            var deaths = new List<RoomPlace> { new RoomPlace(2, 1, -1), new RoomPlace(3, 1.5, -0.5), new RoomPlace(4, 3, 3) };
            RoomUseMeasures m = RoomUseRules.Measure(s, places, 0, 10, deaths);
            List<string> map = RoomUseRules.Map(s, m, deaths);
            Assert.That(map, Is.EqualTo(new[]
            {
                "+--------+",
                "|L . o # |", // the far band: the low cover's middle, floor she never stood in, 0.4 s (4 %), not floor
                "|. . @ .x|", // 1.5 s (15 %); an enemy died at the right
                "|. @X. . |", // 8 s (80 %) where two died; her way between passed no other cell
                "+--^^^^--+", // the door, 4 m wide, in the middle of 8 m
            }));
            TestContext.WriteLine(string.Join("\n", map));
        }
    }
}
