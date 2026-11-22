using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The greybox view's rules (work order 084): which part a thing is from what it does (high cover, low cover, what only
    /// blocks walking, no collider not shown, a chest as it is), the colours told apart, the legend's lines fitting the
    /// developer's box, work order 070's line on the floor where the room wakes (from each wall, a small room and a large one,
    /// round a carved cell), and the room's words.
    /// </summary>
    public class GreyboxRulesTests
    {
        const float Cs = 4f;

        [Test]
        public void APropsPart_IsWhatItDoes()
        {
            Assert.That(GreyboxRules.PropPart(true, true, true, false), Is.EqualTo(GreyboxPart.HighCover), "stops shots: high cover");
            Assert.That(GreyboxRules.PropPart(true, true, false, false), Is.EqualTo(GreyboxPart.HighCover), "stops shots, no shelter: high cover still");
            Assert.That(GreyboxRules.PropPart(true, false, true, false), Is.EqualTo(GreyboxPart.LowCover), "shot over, ducked behind: low cover");
            Assert.That(GreyboxRules.PropPart(true, false, false, false), Is.EqualTo(GreyboxPart.BlocksWalking), "neither (the candelabra): blocks walking only");
            Assert.That(GreyboxRules.PropPart(false, false, false, false), Is.EqualTo(GreyboxPart.Hidden), "no collider (the dais, the banner): not shown");
            Assert.That(GreyboxRules.PropPart(false, true, true, false), Is.EqualTo(GreyboxPart.Hidden), "no collider is never shown, whatever else");
            Assert.That(GreyboxRules.PropPart(true, false, true, true), Is.EqualTo(GreyboxPart.AsItIs), "a chest is gameplay: as it is");
        }

        [Test]
        public void TheColours_AreToldApart_TheTwoCoversMostOfAll()
        {
            var v = new GreyboxValues();
            GreyboxPart[] parts = { GreyboxPart.Floor, GreyboxPart.Wall, GreyboxPart.Door, GreyboxPart.HighCover, GreyboxPart.LowCover, GreyboxPart.BlocksWalking, GreyboxPart.FightLine };
            for (int i = 0; i < parts.Length; i++)
                for (int j = i + 1; j < parts.Length; j++)
                    Assert.That(GreyboxRules.Distance(GreyboxRules.ColourOf(parts[i], v), GreyboxRules.ColourOf(parts[j], v)), Is.GreaterThan(0.2),
                                parts[i] + " and " + parts[j] + " are told apart");
            Assert.That(GreyboxRules.Distance(v.HighCover, v.LowCover), Is.GreaterThan(0.8), "high and low cover clearly different");
            Rgb f = v.Floor, w = v.Wall;
            Assert.That(f.R, Is.EqualTo(f.G).And.EqualTo(f.B), "the floor one flat grey");
            Assert.That(w.R + w.G + w.B, Is.LessThan(f.R + f.G + f.B), "the walls a darker grey than the floor");
            Assert.That(GreyboxRules.ColourOf(GreyboxPart.LowCover, v), Is.EqualTo(v.LowCover));
        }

        [Test]
        public void TheLegend_NamesEveryColour_AndEachLineFitsTheDevelopersBox()
        {
            foreach (GreyboxPart p in new[] { GreyboxPart.Floor, GreyboxPart.Wall, GreyboxPart.Door, GreyboxPart.HighCover, GreyboxPart.LowCover,
                                              GreyboxPart.BlocksWalking, GreyboxPart.FightLine, GreyboxPart.Hidden, GreyboxPart.AsItIs })
                StringAssert.Contains(GreyboxRules.LegendWord(p), GreyboxRules.Legend);
            foreach (string line in GreyboxRules.Legend.Split('\n'))
                Assert.That(line.Length, Is.LessThanOrEqualTo(GreyboxRules.DevBoxLineMost), "\"" + line + "\" does not wrap");
            StringAssert.Contains("Ctrl", GreyboxRules.LegendWord(GreyboxPart.LowCover), "low cover: she can duck behind it on Left Ctrl");
        }

        /// <summary>A passage south of a combat room of w by h cells, its door in on the room's south wall at cell x = 22.</summary>
        static LevelLayout RoomFromTheSouth(int w, int h, out LevelRoom room, out LevelDoor door)
        {
            var l = new LevelLayout { Seed = 1UL, CellSize = Cs, GridCells = 64, Floor = 1, InALine = true, Passages = true };
            var passage = new LevelRoom { Kind = RoomKind.Passage, Order = 0, Rect = new CellRect(22, 17, 1, 3) };
            room = new LevelRoom { Kind = RoomKind.Combat, Order = 1, Rect = new CellRect(20, 20, w, h) };
            door = new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = 20, From = 22, Width = 1 };
            l.Rooms.Add(passage);
            l.Rooms.Add(room);
            l.Doors.Add(door);
            return l;
        }

        [Test]
        public void TheFightsLine_LiesFiveMetresInFromTheDoorsWall_AcrossTheRoom()
        {
            LevelLayout l = RoomFromTheSouth(7, 6, out LevelRoom room, out LevelDoor door);
            var f = new RoomFightValues();
            List<(LevelPoint a, LevelPoint b)> line = GreyboxRules.FightLine(l, room, door, f);
            Assert.That(line, Has.Count.EqualTo(1), "one stretch across an open room");
            Assert.That(line[0].a.Y, Is.EqualTo(20 * Cs + 5.0).Within(1e-9), "5 m in from the south wall (the order: 5 m in from the door)");
            Assert.That(line[0].b.Y, Is.EqualTo(line[0].a.Y));
            Assert.That(line[0].a.X, Is.EqualTo(20 * Cs).Within(1e-9));
            Assert.That(line[0].b.X, Is.EqualTo(27 * Cs).Within(1e-9), "wall to wall");
            // Standing on the line she is past the point, a hair short of it she is not: the line is the rule's.
            var on = new LevelPoint(23.5 * Cs, line[0].a.Y);
            var before = new LevelPoint(23.5 * Cs, line[0].a.Y - 0.01);
            Assert.That(RoomFightRules.PastThePoint(l, room, door, on, f), Is.True);
            Assert.That(RoomFightRules.PastThePoint(l, room, door, before, f), Is.False);
        }

        [Test]
        public void TheFightsLine_InASmallRoom_IsAtAThirdOfItsDepth_AndFromEachWall()
        {
            var f = new RoomFightValues();
            LevelLayout small = RoomFromTheSouth(5, 3, out LevelRoom room, out LevelDoor door); // 12 m deep: a third is 4 m
            List<(LevelPoint a, LevelPoint b)> line = GreyboxRules.FightLine(small, room, door, f);
            Assert.That(line[0].a.Y, Is.EqualTo(20 * Cs + 4.0).Within(1e-5), "no further in than a third of the room's depth (a float's third)");

            // A door on each of the other walls: the line is always the same depth in from that wall.
            foreach (int wall in new[] { LevelGenerator.North, LevelGenerator.East, LevelGenerator.West })
            {
                LevelLayout l = RoomFromTheSouth(7, 7, out LevelRoom r, out LevelDoor d);
                d.Vertical = wall != LevelGenerator.North;
                d.Line = wall == LevelGenerator.North ? r.Rect.Top : wall == LevelGenerator.East ? r.Rect.Right : r.Rect.X;
                d.From = d.Vertical ? r.Rect.Y + 2 : r.Rect.X + 2;
                List<(LevelPoint a, LevelPoint b)> ln = GreyboxRules.FightLine(l, r, d, f);
                Assert.That(ln, Has.Count.EqualTo(1), LevelGenerator.Opposite(wall) + "");
                double depth = RoomFightRules.DepthIn(l, r, d, ln[0].a);
                Assert.That(depth, Is.EqualTo(5.0).Within(1e-9), "5 m in from the " + RoomTemplates.WallWord(wall) + " wall");
                Assert.That(RoomFightRules.DepthIn(l, r, d, ln[0].b), Is.EqualTo(5.0).Within(1e-9));
            }
        }

        [Test]
        public void TheFightsLine_StopsAtACarvedCell_AndIsNoneWithoutADoorOrInAPassage()
        {
            LevelLayout l = RoomFromTheSouth(7, 6, out LevelRoom room, out LevelDoor door);
            room.SetSolid(23, 21, true); // the cell the line crosses (y 21: 5 m in lies in the second row)
            List<(LevelPoint a, LevelPoint b)> line = GreyboxRules.FightLine(l, room, door, new RoomFightValues());
            Assert.That(line, Has.Count.EqualTo(2), "two stretches either side of the carved cell");
            Assert.That(line[0].b.X, Is.EqualTo(23 * Cs).Within(1e-9));
            Assert.That(line[1].a.X, Is.EqualTo(24 * Cs).Within(1e-9));
            Assert.That(GreyboxRules.FightLine(l, room, null, new RoomFightValues()), Is.Empty, "no door: no line");
            Assert.That(GreyboxRules.FightLine(l, l.Rooms[0], door, new RoomFightValues()), Is.Empty, "a passage: no line");
        }

        [Test]
        public void TheRoomsWords_SayItsTemplateOrShape_AndItsSizeInMetres()
        {
            LevelLayout l = RoomFromTheSouth(7, 6, out LevelRoom room, out _);
            room.Shape = RoomShape.Cross;
            Assert.That(GreyboxRules.RoomWords(room, Cs), Is.EqualTo("drawn: cross, 28 x 24 m"));
            RoomTemplate t = RoomTemplateLibrary.All.First(x => x.For == RoomTemplateFor.Combat);
            room.Template = t.Oriented(LevelGenerator.South, false);
            room.Rect = new CellRect(20, 20, t.Width, t.Height);
            room.Shape = RoomShape.Template;
            Assert.That(GreyboxRules.RoomWords(room, Cs), Does.StartWith("template " + t.Name + ", ").And.EndWith(t.Width * 4 + " x " + t.Height * 4 + " m"));
            room.Shape = RoomShape.Plain;
            Assert.That(GreyboxRules.RoomWords(room, Cs), Does.Contain("fell back: plain"));
            Assert.That(GreyboxRules.RoomWords(l.Rooms[0], Cs), Does.StartWith("a passage"));
        }
    }
}
