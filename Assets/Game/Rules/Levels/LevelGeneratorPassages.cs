using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The generator's part of the passages between the rooms (work order 059, <see cref="Passages"/>; Kristian's words of
    /// 2026-11-20 and his choice of 2026-11-21, "Ganger mellom rommene"; the research report d's proposal C), kept apart from its
    /// other stages so the two can be read and merged alone. On a floor in a line with <see cref="LevelValues.Passages"/> on (and
    /// doors one cell wide) the layout is <see cref="TryLineApart"/> in place of <see cref="TryLine"/>: the rooms of the row are
    /// put apart (<see cref="PlaceChainApart"/>, the chain's own rules with a gap), then a passage is drawn into each gap on a
    /// stream of its own (<see cref="PassagesStage"/>), and the row becomes E P C P C ... P X. <see cref="TryLine"/>,
    /// <see cref="PlaceChain"/>, <see cref="Candidates"/>, <see cref="Fits"/> and <see cref="DoorBetween"/> are not changed: with
    /// the switch off no line of the old path runs differently.
    /// </summary>
    public static partial class LevelGenerator
    {
        public const string PassagesStage = Passages.Stage; // work order 059

        /// <summary>
        /// The tests' seam (work order 059, as <c>contentsWarmUp</c> is the contents'): this many draws thrown away at the start of
        /// the passages' stream of every attempt. A warm-up there moves the passages, their doors and what follows from them, and
        /// no room. 0 in the game.
        /// </summary>
        [ThreadStatic] internal static int PassagesWarmUpForTests;

        /// <summary>
        /// The floor in one row with its rooms apart (work order 059): the row's kinds as <see cref="TryLine"/> has them, the
        /// rooms' templates drawn as it draws them (the same stream, the same draws, so a seed's rooms take the templates they
        /// take with the passages off when the attempt is the same), the rooms put down by <see cref="PlaceChainApart"/>, the
        /// passages by <see cref="PlacePassage"/> on their own stream, the elite marked among the combat rooms as
        /// <see cref="TryLine"/> marks it; then the rooms in the order of the run with a passage between every two, their
        /// orders renumbered, the doors (each passage's two, one on the wall of each of its rooms), the floor marked as one with
        /// passages and its grid <see cref="PassageValues.GridCells"/>.
        /// </summary>
        static bool TryLineApart(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            LevelPlan plan = v.Plan;
            PassageValues pv = v.PassageRules ?? new PassageValues();
            level.GridCells = pv.GridCells;
            level.Passages = true;
            var kinds = new List<RoomKind> { RoomKind.Entrance };
            kinds.AddRange(plan.LineBetween());
            kinds.Add(RoomKind.Exit);

            var path = new List<CellRect>();
            var steps = new List<int>();
            var gaps = new List<int>();
            int elite = plan.EliteRoomNumber(plan.LineCombatRooms), combat = 0;
            // Work order 058's templates, as TryLine draws them: on their own stream, before the layout puts the rooms down.
            List<RoomTemplate> templates = null;
            List<bool> mirrors = null;
            List<TemplatePlacement> placed = null;
            if (v.Templates && v.Shapes)
            {
                RoomTemplates.Draw(kinds, elite, level.Floor, LevelRandom.Stream(level.Seed, TemplatesStage, level.Attempt - 1), v, out templates, out mirrors);
                placed = new List<TemplatePlacement>();
                level.Templates = true;
            }
            if (!PlaceChainApart(kinds, rng, v, pv, path, steps, gaps, templates, mirrors, placed)) return false;

            var rooms = new List<LevelRoom>();
            for (int i = 0; i < path.Count; i++)
            {
                if (kinds[i] == RoomKind.Combat) combat++;
                rooms.Add(new LevelRoom { Kind = kinds[i], Rect = path[i], Elite = kinds[i] == RoomKind.Combat && combat == elite, Template = placed?[i] });
            }

            // The passages, on their own stream, so how a passage is drawn can change without moving a room.
            LevelRandom drawn = LevelRandom.Stream(level.Seed, PassagesStage, level.Attempt - 1);
            for (int i = 0; i < PassagesWarmUpForTests; i++) drawn.NextBits();
            var passages = new List<LevelRoom>();
            var ends = new List<(LevelDoor first, LevelDoor second)>();
            for (int i = 1; i < rooms.Count; i++)
            {
                if (!PlacePassage(drawn, rooms[i - 1], rooms[i], steps[i - 1], gaps[i - 1], v, pv, out LevelRoom passage, out LevelDoor first, out LevelDoor second))
                    return false;
                passages.Add(passage);
                ends.Add((first, second));
            }

            // The row in the order of the run: a passage between every two rooms.
            for (int i = 0; i < rooms.Count; i++)
            {
                rooms[i].Order = level.Rooms.Count;
                level.Rooms.Add(rooms[i]);
                if (i >= passages.Count) continue;
                passages[i].Order = level.Rooms.Count;
                level.Rooms.Add(passages[i]);
            }
            for (int i = 0; i < passages.Count; i++)
            {
                (LevelDoor first, LevelDoor second) = ends[i];
                first.RoomA = rooms[i].Order;
                first.RoomB = passages[i].Order;
                second.RoomA = passages[i].Order;
                second.RoomB = rooms[i + 1].Order;
                level.Doors.Add(first);
                level.Doors.Add(second);
            }
            return true;
        }

        /// <summary>
        /// <see cref="PlaceChain"/>'s rules with the rooms apart (work order 059): each room put <c>gap</c> cells off the side of
        /// the room before it, the gap drawn on the layout stream (<see cref="PassageValues.Fewest"/> to
        /// <see cref="PassageValues.Most"/>) every time a room's place is looked for, so a room taken back draws again; the
        /// candidates <see cref="CandidatesApart"/>; the grid <see cref="PassageValues.GridCells"/>, the entrance in its middle;
        /// a place fits (<see cref="FitsApart"/>) inside the grid, touching no room and no band put down, its own band touching no
        /// room but its two and no other band; and the two must hold a passage's ends (<see cref="RoomTemplates.CanJoin"/>,
        /// widened: a room with a template only at its marks, any room only where a passage of the gap can be drawn). The way
        /// on, <see cref="LevelValues.MostInARow"/>, the templates' sizes, sides and mirrors (work order 058), the placements' cap
        /// and taking rooms back are <see cref="PlaceChain"/>'s, step for step. <paramref name="gaps"/>: gaps[i] the cells between
        /// room i and room i + 1.
        /// </summary>
        static bool PlaceChainApart(List<RoomKind> kinds, LevelRandom rng, LevelValues v, PassageValues pv, List<CellRect> path, List<int> steps,
                                    List<int> gaps, List<RoomTemplate> templates, List<bool> mirrors, List<TemplatePlacement> placed)
        {
            LevelPlan plan = v.Plan;
            int grid = pv.GridCells;
            int placements = 0;
            var bands = new List<CellRect>(); // bands[i]: the band between room i and room i + 1

            DrawSize(rng, plan.EntranceSize, out int sw, out int sh);
            path.Add(new CellRect((grid - sw) / 2, (grid - sh) / 2, sw, sh));
            placed?.Add(null);
            if (!FitsApart(path[0], new CellRect(), grid, path, 0, bands)) return false;

            bool TooStraight(int side)
            {
                if (steps.Count < v.MostInARow) return false;
                for (int k = steps.Count - v.MostInARow; k < steps.Count; k++)
                    if (steps[k] != side) return false;
                return true;
            }

            // A candidate keeps the rules of the fit and of the join; false stops nothing, true lets it be tried.
            bool Keeps(int i, Placement c, TemplatePlacement o, int gap, out CellRect band)
            {
                band = Passages.Band(path[i - 1], c.Rect, c.Side);
                if (TooStraight(c.Side)) return false;
                if (band.Width <= 0 || !FitsApart(c.Rect, band, grid, path, path.Count, bands)) return false;
                return RoomTemplates.CanJoin(path[i - 1], placed?[i - 1], c.Rect, o, v.DoorWidth, gap, c.Side, pv);
            }

            bool PlaceMain(int i)
            {
                if (i == kinds.Count) return true;
                int gap = rng.Range(pv.Fewest, pv.Most);
                int need = v.DoorWidth + 2 + (Passages.MustJog(gap, pv) ? pv.FaceMore : 0);
                RoomTemplate template = templates != null && i < templates.Count ? templates[i] : null; // work order 058
                int w = 0, h = 0;
                if (template == null) DrawSize(rng, plan.SizeOf(kinds[i]), out w, out h);
                // The way on (work order 012), as PlaceChain has it.
                List<int> sides;
                if (steps.Count == 0)
                {
                    sides = new List<int> { North, East, South, West };
                    rng.Shuffle(sides);
                }
                else
                {
                    int on = steps[steps.Count - 1];
                    var turns = new List<int> { (on + 1) % 4, (on + 3) % 4 };
                    rng.Shuffle(turns);
                    sides = new List<int> { on, turns[0], turns[1] };
                }
                if (template != null) return PlaceTemplate(i, template, sides, gap, need);
                foreach (Placement c in CandidatesApart(rng, path[i - 1], w, h, need, sides, gap))
                {
                    if (placements >= v.PlacementsPerAttempt) return false;
                    if (!Keeps(i, c, null, gap, out CellRect band)) continue;
                    placements++;
                    if (Try(i, c, null, gap, band)) return true;
                }
                return false;
            }

            // A room put down, then the rest of the chain after it; taken back when the rest fits nowhere.
            bool Try(int i, Placement c, TemplatePlacement o, int gap, CellRect band)
            {
                path.Add(c.Rect);
                steps.Add(c.Side);
                gaps.Add(gap);
                bands.Add(band);
                placed?.Add(o);
                if (PlaceMain(i + 1)) return true;
                path.RemoveAt(path.Count - 1);
                steps.RemoveAt(steps.Count - 1);
                gaps.RemoveAt(gaps.Count - 1);
                bands.RemoveAt(bands.Count - 1);
                placed?.RemoveAt(placed.Count - 1);
                return false;
            }

            // Work order 058's templated room, as PlaceChain puts it: its template's size turned so the door in is on the wall
            // it stands against, on each side in the order given, its drawn mirror first; here off the side by the gap.
            bool PlaceTemplate(int i, RoomTemplate template, List<int> sides, int gap, int need)
            {
                bool drawnMirror = mirrors != null && i < mirrors.Count && mirrors[i];
                foreach (int side in sides)
                {
                    if (side == South && template.FarNotSouth) continue; // its far wall, the throne's, would be the south one
                    foreach (bool mirror in new[] { drawnMirror, !drawnMirror })
                    {
                        TemplatePlacement o = template.Oriented(Opposite(side), mirror);
                        foreach (Placement c in CandidatesApart(rng, path[i - 1], o.Width, o.Height, need, new List<int> { side }, gap))
                        {
                            if (placements >= v.PlacementsPerAttempt) return false;
                            if (!Keeps(i, c, o, gap, out CellRect band)) continue;
                            placements++;
                            if (Try(i, c, o, gap, band)) return true;
                        }
                    }
                }
                return false;
            }

            return PlaceMain(1);
        }

        /// <summary>
        /// Every place for a room of w by h <paramref name="gap"/> cells off a side of <paramref name="a"/> (work order 059):
        /// <see cref="Candidates"/> with the rectangle moved out by the gap, facing <paramref name="a"/> over at least
        /// <paramref name="need"/> cells; the sides in the order given, the places along each side in a drawn order.
        /// </summary>
        static IEnumerable<Placement> CandidatesApart(LevelRandom rng, CellRect a, int w, int h, int need, List<int> sides, int gap)
        {
            var result = new List<Placement>();
            foreach (int side in sides)
            {
                var along = new List<Placement>();
                if (side == East || side == West)
                {
                    int x = side == East ? a.Right + gap : a.X - w - gap;
                    for (int y = a.Y - h + need; y <= a.Top - need; y++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                else
                {
                    int y = side == North ? a.Top + gap : a.Y - h - gap;
                    for (int x = a.X - w + need; x <= a.Right - need; x++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                rng.Shuffle(along);
                result.AddRange(along);
            }
            return result;
        }

        /// <summary>
        /// A room's place on a floor with passages (work order 059): inside the grid, touching (<see cref="CellRect.Touches"/>,
        /// a side or a corner) none of the first <paramref name="count"/> rooms and no band put down; and its own band
        /// (<paramref name="band"/>, empty for the entrance) touching no room but its two (the last put down and this one) and no
        /// other band.
        /// </summary>
        static bool FitsApart(CellRect r, CellRect band, int grid, List<CellRect> rooms, int count, List<CellRect> bands)
        {
            if (r.X < 0 || r.Y < 0 || r.Right > grid || r.Top > grid) return false;
            for (int i = 0; i < count; i++)
                if (r.Touches(rooms[i])) return false;
            foreach (CellRect b in bands)
                if (r.Touches(b)) return false;
            if (band.Width <= 0 || band.Height <= 0) return true;
            for (int i = 0; i < count - 1; i++)
                if (band.Touches(rooms[i])) return false;
            foreach (CellRect b in bands)
                if (band.Touches(b)) return false;
            return true;
        }

        static double PassageUnit(LevelRandom rng) => (rng.NextBits() >> 11) * (1.0 / (1UL << 53));

        /// <summary>
        /// A passage's two door cells (work order 059), the one method that chooses them, so a room's template has its say: on
        /// the stretch [<paramref name="lo"/>, <paramref name="hi"/>) where the two rooms face each other, <paramref name="x1"/> a
        /// cell of the first room's wall (an O of its template; any cell a cell in from the stretch's ends without one) from
        /// which a passage of the gap reaches the second, drawn evenly; whether it jogs: always when the gap is more than
        /// <see cref="PassageValues.StraightMost"/>, never when it is less than 3, else when a draw is below
        /// <see cref="PassageValues.JogShare"/> (and always when the second room has no door place straight across); a jog's
        /// <paramref name="x2"/> drawn evenly among the second room's door places (its I cells with a template) 1 to
        /// <see cref="PassageValues.JogMost"/> cells sideways, none (and no jog) when there is none; with no jog,
        /// <paramref name="x2"/> is <paramref name="x1"/>. False when no pair can be joined (the layout's join check rules that out).
        /// </summary>
        static bool PassageDoorCells(LevelRandom rng, LevelRoom first, LevelRoom second, int side, int lo, int hi, int gap, int width, PassageValues pv,
                                     out int x1, out int x2, out bool jog)
        {
            x1 = x2 = 0;
            jog = false;
            List<int> outs = RoomTemplates.DoorPlaces(first.Rect, first.Template, side, lo, hi, 'O', width);
            List<int> ins = RoomTemplates.DoorPlaces(second.Rect, second.Template, Opposite(side), lo, hi, 'I', width);
            List<int> starts = Passages.Starts(outs, ins, gap, pv);
            if (starts.Count == 0) return false;
            x1 = starts[rng.Below(starts.Count)];
            jog = Passages.MustJog(gap, pv);
            if (!jog && Passages.MayJog(gap)) jog = PassageUnit(rng) < pv.JogShare;
            if (!ins.Contains(x1)) jog = true; // only a jog reaches the room after from here
            List<int> options = jog ? Passages.JogsFrom(x1, ins, pv) : null;
            if (jog && options.Count == 0) jog = false; // only where it may run straight: Starts says so
            x2 = jog ? options[rng.Below(options.Count)] : x1;
            return true;
        }

        /// <summary>
        /// The passage between two rooms of the row (work order 059): its door cells (<see cref="PassageDoorCells"/>); with a jog
        /// its turn row drawn from 1 to the gap less 2, counted from the first room's wall, so it never turns in the row next to
        /// either room; the path straight out of the first room to the turn row, sideways to <c>x2</c>, straight on into the
        /// next; its rectangle the path's bounding box, every other cell of it solid; its door on the first room's wall and its
        /// door on the second's (their rooms' orders are filled in by the caller). East and west joins are the same with the
        /// axes turned.
        /// </summary>
        static bool PlacePassage(LevelRandom rng, LevelRoom first, LevelRoom second, int side, int gap, LevelValues v, PassageValues pv,
                                 out LevelRoom passage, out LevelDoor firstDoor, out LevelDoor secondDoor)
        {
            passage = null;
            firstDoor = secondDoor = null;
            if (!Passages.Facing(first.Rect, second.Rect, side, out int lo, out int hi)) return false;
            if (!PassageDoorCells(rng, first, second, side, lo, hi, gap, v.DoorWidth, pv, out int x1, out int x2, out bool jog)) return false;
            int turn = jog ? rng.Range(1, gap - 2) : -1;

            // Cells in the passage's frame: along (x for a north or south side, y for an east or west one) and depth, counted
            // from the first room's wall (0 next to it, gap - 1 next to the second).
            CellRect a = first.Rect;
            (int x, int y) Cell(int along, int depth)
            {
                switch (side)
                {
                    case North: return (along, a.Top + depth);
                    case South: return (along, a.Y - 1 - depth);
                    case East: return (a.Right + depth, along);
                    default: return (a.X - 1 - depth, along);
                }
            }
            var cells = new List<(int x, int y)>();
            for (int d = 0; d < gap; d++)
            {
                if (!jog || d < turn) cells.Add(Cell(x1, d));
                else if (d == turn)
                {
                    int step = x2 > x1 ? 1 : -1;
                    for (int c = x1; c != x2; c += step) cells.Add(Cell(c, d));
                    cells.Add(Cell(x2, d));
                }
                else cells.Add(Cell(x2, d));
            }
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach ((int x, int y) in cells)
            {
                minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
            }
            passage = new LevelRoom { Kind = RoomKind.Passage, Rect = new CellRect(minX, minY, maxX - minX + 1, maxY - minY + 1) };
            var open = new HashSet<(int, int)>(cells);
            for (int y = minY; y <= maxY; y++)
                for (int x = minX; x <= maxX; x++)
                    if (!open.Contains((x, y))) passage.SetSolid(x, y, true);

            bool vertical = side == East || side == West;
            int firstLine = side == North ? a.Top : side == South ? a.Y : side == East ? a.Right : a.X;
            CellRect b = second.Rect;
            int secondLine = side == North ? b.Y : side == South ? b.Top : side == East ? b.X : b.Right;
            firstDoor = new LevelDoor { Vertical = vertical, Line = firstLine, From = x1, Width = v.DoorWidth };
            secondDoor = new LevelDoor { Vertical = vertical, Line = secondLine, From = x2, Width = v.DoorWidth };
            return true;
        }
    }
}
