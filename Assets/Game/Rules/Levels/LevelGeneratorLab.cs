using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The generator's part of the room lab (work order 084, <see cref="RoomLab"/>), kept apart from its other stages: ONE room
    /// built alone by the generator's own stages, with a short passage on its door in. Nothing of a floor's making changes
    /// here (no version, no stream of a floor is moved): it is a way into <see cref="PlaceGroups"/>, <see cref="DrawSize"/>
    /// and the stages' public parts, for a layout that is not a floor.
    /// </summary>
    public static partial class LevelGenerator
    {
        /// <summary>
        /// The lab room of a template (null: the generator's drawn shapes), a kind, a mix and a seed, through the stages a floor
        /// is made by, each on its own stream of the seed and the attempt:
        /// the layout ("layout"): the room's size (its template's, turned to come in from the south and mirrored as the
        /// "templates" stream draws; without one drawn from the plan's size for its kind), in the middle of a grid of
        /// <see cref="PassageValues.GridCells"/>; the passage's depth (<see cref="PassageValues.Fewest"/> to the lesser of
        /// <see cref="PassageValues.Most"/> and <see cref="PassageValues.StraightMost"/>: straight) and its door in its south wall,
        /// beside an I of its template (any place with a wall cell either side without one), the passage straight south from it,
        /// her start in its far cell; the carve ("shapes", <see cref="RoomShapes.Carve"/>: the template stamped, its ? cells drawn,
        /// or a shape drawn; no gate, the passage is the threshold); the enemies ("contents", <see cref="RoomLab.Groups"/>, each
        /// group given its point by <see cref="PlaceGroups"/>); the theme ("themes", <see cref="RoomThemes.Assign"/>) and the props
        /// ("props", <see cref="PropPlacer.Place"/>), with the values' switches. An attempt whose room breaks a rule of
        /// <see cref="RoomLab.Faults"/> is drawn again on the next attempt's streams; with none left it throws, naming the faults.
        /// </summary>
        public static LevelLayout LabRoom(RoomTemplate template, LabKind kind, LabMix mix, ulong seed, LevelValues v)
        {
            v = v ?? new LevelValues();
            mix = mix ?? RoomLab.Mixes[0];
            PassageValues pv = v.PassageRules ?? new PassageValues();
            RoomKind roomKind = RoomLab.KindOf(kind);
            int floor = Math.Max(1, Math.Min(v.FloorsPerAct, mix.Floor));
            int width = Math.Max(1, v.DoorWidth);
            List<string> faults = null;
            for (int attempt = 0; attempt < Math.Max(1, v.Attempts); attempt++)
            {
                var level = new LevelLayout
                {
                    Seed = seed, Version = Version, GridCells = pv.GridCells, CellSize = v.CellSize, Attempt = attempt + 1, Floor = floor,
                    InALine = true, Passages = true, Templates = template != null,
                };
                LevelRandom layout = LevelRandom.Stream(seed, LayoutStage, attempt);
                TemplatePlacement o = null;
                int w, h;
                if (template != null)
                {
                    bool mirror = LevelRandom.Stream(seed, TemplatesStage, attempt).Below(2) == 1;
                    o = template.Oriented(South, mirror);
                    w = o.Width;
                    h = o.Height;
                }
                else DrawSize(layout, v.Plan.SizeOf(roomKind), out w, out h);
                int gap = layout.Range(pv.Fewest, Math.Max(pv.Fewest, Math.Min(pv.Most, pv.StraightMost)));
                int grid = pv.GridCells;
                var rect = new CellRect((grid - w) / 2, (grid - h + gap) / 2, w, h);
                List<int> places = RoomTemplates.DoorPlaces(rect, o, South, rect.X, rect.Right, 'I', width);
                if (places.Count == 0)
                {
                    faults = new List<string> { "no place for its door in on its south wall" };
                    continue;
                }
                int from = places[layout.Below(places.Count)];
                var passage = new LevelRoom { Kind = RoomKind.Passage, Order = 0, Rect = new CellRect(from, rect.Y - gap, width, gap) };
                passage.PlayerStart = new LevelPoint((from + width * 0.5) * v.CellSize, (rect.Y - gap + 0.5) * v.CellSize);
                var room = new LevelRoom { Kind = roomKind, Order = 1, Rect = rect, Elite = RoomLab.EliteOf(kind), Template = o };
                level.Rooms.Add(passage);
                level.Rooms.Add(room);
                level.Doors.Add(new LevelDoor { RoomA = 0, RoomB = 1, Vertical = false, Line = rect.Y, From = from, Width = width });

                if (v.Shapes || template != null) RoomShapes.Carve(level, LevelRandom.Stream(seed, ShapesStage, attempt), v);
                LevelRandom contents = LevelRandom.Stream(seed, ContentsStage, attempt);
                List<EnemyGroup> groups = RoomLab.Groups(kind, mix, contents, v);
                if (groups.Count > 0 && !PlaceGroups(level, room, groups, contents, v))
                {
                    faults = new List<string> { "no place in it for its enemies" };
                    continue;
                }
                if (v.Props)
                {
                    if (v.Themes) RoomThemes.Assign(level, LevelRandom.Stream(seed, ThemesStage, attempt), v);
                    PropPlacer.Place(level, LevelRandom.Stream(seed, PropsStage, attempt), v);
                }
                faults = RoomLab.Faults(level, mix, v);
                if (faults.Count == 0) return level;
            }
            throw new InvalidOperationException("No lab room " + (template != null ? template.Name : "(drawn shape)") + " (" + RoomLab.Word(kind) + ", " + mix.Name
                                                + ") for seed " + LevelSeed.ToText(seed) + " in " + v.Attempts + " attempts"
                                                + (faults != null && faults.Count > 0 ? ": " + string.Join("; ", faults) : ""));
        }
    }
}
