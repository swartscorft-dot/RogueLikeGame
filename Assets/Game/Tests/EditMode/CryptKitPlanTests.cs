using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Where the crypt kit's pieces go (work order 016, 2026-11-20), as plain data from a layout: a section on every wall
    /// edge, full or low by the grey boxes' rule; an arch in every door cell of a full line and none on a low line; one
    /// pier or post on every grid point a section or a door ends at, a pier where a full-height piece touches it; niches
    /// toward a room; lanterns only on plain full sections, on a face toward a room, never next to a door; the same
    /// layout gives the same plan; and the plan reads nothing the generator draws.
    /// </summary>
    public class CryptKitPlanTests
    {
        /// <summary>
        /// Three rooms: the entrance (0,0 3x3), a combat room east of it (3,0 3x3) through a door on the vertical line
        /// x = 3 at y = 1, and a treasure room north of the entrance (0,3 3x2) through a door on the horizontal line
        /// y = 3 at x = 1.
        /// </summary>
        static LevelLayout ThreeRooms(int verticalDoorFrom = 1, int verticalDoorWidth = 1)
        {
            var l = new LevelLayout { Seed = 5UL, CellSize = 4f, GridCells = 48, Floor = 1 };
            l.Rooms.Add(new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(0, 0, 3, 3), Order = 0 });
            l.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = new CellRect(3, 0, 3, 3), Order = 1 });
            l.Rooms.Add(new LevelRoom { Kind = RoomKind.Treasure, Rect = new CellRect(0, 3, 3, 2), Order = 2 });
            l.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = 3, From = verticalDoorFrom, Width = verticalDoorWidth });
            l.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 2, Vertical = false, Line = 3, From = 1, Width = 1 });
            return l;
        }

        static CryptKitSection Section(CryptKitPlan p, bool vertical, int line, int cell) =>
            p.Sections.SingleOrDefault(s => s.Vertical == vertical && s.Line == line && s.Cell == cell);

        static CryptKitPoint? Point(CryptKitPlan p, int x, int y)
        {
            List<CryptKitPoint> at = p.Points.Where(q => q.X == x && q.Y == y).ToList();
            Assert.That(at.Count, Is.LessThanOrEqualTo(1), "one piece a point at " + x + "," + y);
            return at.Count == 1 ? at[0] : (CryptKitPoint?)null;
        }

        [Test]
        public void Sections_StandOnEveryWallEdge_FullOrLowByTheBoxesRule()
        {
            CryptKitPlan p = CryptKitPlan.Make(ThreeRooms(), new CryptKitValues());
            // Vertical: x = 0 (5 cells), x = 3 (cells 0 and 2 between the rooms, 3 and 4 the treasure room's east side), x = 6 (3).
            Assert.That(p.Sections.Count(s => s.Vertical), Is.EqualTo(12));
            Assert.That(p.Sections.Where(s => s.Vertical).All(s => !s.Low), "a vertical wall is always full height");
            // Horizontal: y = 0 low (6), y = 3 low under the treasure room (2, the door between) and full over the combat room (3), y = 5 full (3).
            Assert.That(p.Sections.Count(s => !s.Vertical), Is.EqualTo(14));
            Assert.That(p.LowSections, Is.EqualTo(8));
            Assert.That(Section(p, false, 0, 4).Low, Is.True, "a room's south wall is low");
            Assert.That(Section(p, false, 3, 0).Low, Is.True, "the wall between a room and the one north of it is low");
            Assert.That(Section(p, false, 3, 4).Low, Is.False, "a room's north wall with nothing beyond is full");
            Assert.That(Section(p, false, 5, 1).Low, Is.False);
            Assert.That(Section(p, true, 3, 1), Is.Null, "no wall in a door");
            Assert.That(Section(p, false, 3, 1), Is.Null);
            Assert.That(Section(p, true, 1, 1), Is.Null, "none inside a room");
            Assert.That(p.Sections.Select(s => (s.Vertical, s.Line, s.Cell)).Distinct().Count(), Is.EqualTo(p.Sections.Count), "one a cell edge");

            CryptKitSection between = Section(p, true, 3, 0);
            Assert.That(between.RoomMinus, Is.EqualTo(0));
            Assert.That(between.RoomPlus, Is.EqualTo(1));
            Assert.That(between.AtDoor, Is.True);
            Assert.That(Section(p, true, 0, 1).Facing, Is.EqualTo(1), "an outer wall faces its room");
            Assert.That(Section(p, true, 6, 1).Facing, Is.EqualTo(-1));
            Assert.That(Section(p, false, 5, 1).Facing, Is.EqualTo(-1), "a north wall faces south, into its room");
            Assert.That(p.Sections.Where(s => s.Low).All(s => s.Facing == -1), "a low wall's front to the camera");
        }

        [Test]
        public void Arches_OnlyInTheDoorsOfAFullLine()
        {
            CryptKitPlan p = CryptKitPlan.Make(ThreeRooms(), new CryptKitValues());
            Assert.That(p.Arches.Count, Is.EqualTo(1));
            Assert.That(p.Arches[0].Vertical, Is.True);
            Assert.That(p.Arches[0].Line, Is.EqualTo(3));
            Assert.That(p.Arches[0].Cell, Is.EqualTo(1));
            Assert.That(p.Arches[0].Door, Is.EqualTo(0));

            CryptKitPlan wide = CryptKitPlan.Make(ThreeRooms(0, 2), new CryptKitValues());
            Assert.That(wide.Arches.Select(a => a.Cell), Is.EqualTo(new[] { 0, 1 }), "an arch a door cell");
            Assert.That(Point(wide, 3, 1), Is.Null, "no pier inside a door two cells wide");
            Assert.That(Point(wide, 3, 0)?.Pier, Is.True);
            Assert.That(Point(wide, 3, 2)?.Pier, Is.True);
        }

        [Test]
        public void PiersAndPosts_OnePerPoint_APierWhereAFullPieceTouches()
        {
            LevelLayout l = ThreeRooms();
            CryptKitPlan p = CryptKitPlan.Make(l, new CryptKitValues());
            Assert.That(p.Points.Select(q => (q.X, q.Y)).Distinct().Count(), Is.EqualTo(p.Points.Count), "never two on one point");
            Assert.That(Point(p, 0, 0)?.Pier, Is.True, "a full and a low wall meet: a pier");
            Assert.That(Point(p, 1, 0)?.Pier, Is.False, "only low walls: a post");
            Assert.That(Point(p, 4, 0)?.Pier, Is.False);
            Assert.That(Point(p, 3, 1)?.Pier, Is.True, "the ends of a door on a full line");
            Assert.That(Point(p, 3, 2)?.Pier, Is.True);
            Assert.That(Point(p, 1, 3)?.Pier, Is.False, "the ends of a door on a low line: posts");
            Assert.That(Point(p, 2, 3)?.Pier, Is.False);
            Assert.That(Point(p, 3, 3)?.Pier, Is.True);
            Assert.That(Point(p, 4, 3)?.Pier, Is.True);
            Assert.That(Point(p, 6, 3)?.Pier, Is.True, "a corner");
            Assert.That(Point(p, 1, 1), Is.Null, "nothing inside a room");

            // Every end of every section has its piece, a pier when the section is full.
            foreach (CryptKitSection s in p.Sections)
                foreach (int end in new[] { s.Cell, s.Cell + 1 })
                {
                    CryptKitPoint? q = s.Vertical ? Point(p, s.Line, end) : Point(p, end, s.Line);
                    Assert.That(q, Is.Not.Null, "a piece at the end of " + (s.Vertical ? "V" : "H") + s.Line + "_" + s.Cell);
                    if (!s.Low) Assert.That(q.Value.Pier, Is.True);
                }
            // And no point no section or door ends at.
            foreach (CryptKitPoint q in p.Points)
            {
                bool ends = p.Sections.Any(s => s.Vertical ? s.Line == q.X && (s.Cell == q.Y || s.Cell + 1 == q.Y) : s.Line == q.Y && (s.Cell == q.X || s.Cell + 1 == q.X))
                            || l.Doors.Any(d => d.Vertical ? d.Line == q.X && (d.From == q.Y || d.From + d.Width == q.Y) : d.Line == q.Y && (d.From == q.X || d.From + d.Width == q.X));
                Assert.That(ends, Is.True, "a piece at " + q.X + "," + q.Y + " where nothing ends");
            }
        }

        [Test]
        public void Niches_FaceARoom_AndOnlyOnFullSections()
        {
            CryptKitPlan all = CryptKitPlan.Make(ThreeRooms(), new CryptKitValues { NicheShare = 1f, LanternShare = 1f });
            Assert.That(all.Niches, Is.EqualTo(all.Sections.Count(s => !s.Low)), "every full section, with the share at 1");
            foreach (CryptKitSection s in all.Sections)
            {
                if (s.Low) Assert.That(s.Niche, Is.False, "a low wall has no niche");
                else Assert.That(s.RoomOn(s.Facing), Is.GreaterThanOrEqualTo(0), "the niche faces a room");
            }
            Assert.That(all.Lanterns, Is.Empty, "never a lantern on a niche section");

            CryptKitPlan none = CryptKitPlan.Make(ThreeRooms(), new CryptKitValues { NicheShare = 0f, LanternShare = 0f });
            Assert.That(none.Niches, Is.EqualTo(0));
            Assert.That(none.Lanterns, Is.Empty);
        }

        [Test]
        public void Lanterns_OnPlainFullSections_TowardARoom_NeverNextToADoor()
        {
            CryptKitPlan p = CryptKitPlan.Make(ThreeRooms(), new CryptKitValues { NicheShare = 0f, LanternShare = 1f });
            // Every face toward a room of a full section not next to a door: x = 0 (5), x = 3 cells 3 and 4 (2), x = 6 (3), y = 3 over the combat room (3), y = 5 (3).
            Assert.That(p.Lanterns.Count, Is.EqualTo(16));
            foreach (CryptKitLantern l in p.Lanterns)
            {
                CryptKitSection s = p.Sections[l.Section];
                Assert.That(s.Low, Is.False);
                Assert.That(s.Niche, Is.False);
                Assert.That(s.AtDoor, Is.False);
                Assert.That(s.RoomOn(l.Side), Is.EqualTo(l.Room));
                Assert.That(l.Room, Is.GreaterThanOrEqualTo(0), "on the face toward a room");
            }
            Assert.That(p.Lanterns.Select(l => (l.Section, l.Side)).Distinct().Count(), Is.EqualTo(p.Lanterns.Count), "one a face");
        }

        [Test]
        public void AWallBetweenTwoRooms_CanHoldALanternOnEachFace_EachForItsRoom()
        {
            // Two rooms side by side along a long shared wall, the door at one end: the far cells of the wall hold one on each face.
            var l = new LevelLayout { Seed = 9UL, CellSize = 4f, GridCells = 48, Floor = 1 };
            l.Rooms.Add(new LevelRoom { Kind = RoomKind.Entrance, Rect = new CellRect(0, 0, 3, 6), Order = 0 });
            l.Rooms.Add(new LevelRoom { Kind = RoomKind.Combat, Rect = new CellRect(3, 0, 3, 6), Order = 1 });
            l.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = true, Line = 3, From = 0, Width = 1 });
            CryptKitPlan p = CryptKitPlan.Make(l, new CryptKitValues { NicheShare = 0f, LanternShare = 1f });
            List<CryptKitLantern> shared = p.Lanterns.Where(x => p.Sections[x.Section].Vertical && p.Sections[x.Section].Line == 3).ToList();
            Assert.That(shared.Count, Is.EqualTo(2 * 4), "cells 2 to 5 of the shared wall, both faces (cell 1 is next to the door)");
            Assert.That(shared.Where(x => x.Side < 0).All(x => x.Room == 0), Is.True);
            Assert.That(shared.Where(x => x.Side > 0).All(x => x.Room == 1), Is.True);
        }

        [Test]
        public void TheSameLayout_GivesTheSamePlan_AndTheGeneratorNeverReadsTheKit()
        {
            foreach (ulong seed in new[] { 1UL, 42UL, 12345UL })
                foreach (int floor in new[] { 1, 5 })
                {
                    LevelLayout a = LevelGenerator.Generate(seed, floor, new LevelValues());
                    string before = a.Describe();
                    CryptKitPlan pa = CryptKitPlan.Make(a, new CryptKitValues());
                    Assert.That(a.Describe(), Is.EqualTo(before), "making the plan changes nothing of the layout");
                    CryptKitPlan pb = CryptKitPlan.Make(LevelGenerator.Generate(seed, floor, new LevelValues()), new CryptKitValues());
                    Assert.That(pb.Describe(), Is.EqualTo(pa.Describe()), "the same seed, the same niches and lanterns: " + seed + " floor " + floor);

                    var off = new LevelValues { CryptKit = false };
                    off.Crypt.NicheShare = 0.9f;
                    Assert.That(LevelGenerator.Generate(seed, floor, off).Describe(), Is.EqualTo(before), "the kit's values move nothing of the floor");
                }
            string one = CryptKitPlan.Make(LevelGenerator.Generate(42UL, new LevelValues()), new CryptKitValues()).Describe();
            string other = CryptKitPlan.Make(LevelGenerator.Generate(43UL, new LevelValues()), new CryptKitValues()).Describe();
            Assert.That(other, Is.Not.EqualTo(one));
        }

        [Test]
        public void OverManySeeds_TheSharesAreAboutTheirValues()
        {
            int full = 0, niches = 0, faces = 0, lanterns = 0;
            for (ulong seed = 1; seed <= 60; seed++)
            {
                LevelLayout l = LevelGenerator.Generate(seed, (int)(seed % 5) + 1, new LevelValues());
                CryptKitPlan p = CryptKitPlan.Make(l, new CryptKitValues());
                Assert.That(p.Arches.Count, Is.EqualTo(l.Doors.Count(d => d.Vertical) /* every door of DoorWidth 1 */), "seed " + seed);
                Assert.That(p.Arches.All(a => a.Vertical), "no horizontal door is on a full line: a door's north side is a room");
                foreach (CryptKitSection s in p.Sections.Where(s => !s.Low))
                {
                    full++;
                    if (s.Niche) niches++;
                    else if (!s.AtDoor) faces += (s.RoomMinus >= 0 ? 1 : 0) + (s.RoomPlus >= 0 ? 1 : 0);
                }
                lanterns += p.Lanterns.Count;
            }
            Assert.That(niches / (double)full, Is.EqualTo(0.2).Within(0.03), niches + " of " + full);
            Assert.That(lanterns / (double)faces, Is.EqualTo(1.0 / 3.0).Within(0.04), lanterns + " of " + faces);
        }

        [Test]
        public void TheKit_IsOnByDefault()
        {
            var v = new LevelValues();
            Assert.That(v.CryptKit, Is.True);
            Assert.That(v.Crypt, Is.Not.Null);
            Assert.That(v.Crypt.LanternLights, Is.False, "the lights are left off");
        }
    }
}
