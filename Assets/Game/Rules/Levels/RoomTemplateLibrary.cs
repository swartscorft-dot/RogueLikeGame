using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Roguelike.Rules.Tests")]

namespace Roguelike.Rules
{
    /// <summary>
    /// The rooms drawn by hand (work order 058; Kristian, 2026-11-21: "Rom-maler slik Diablo og PoE gjør"; the research report
    /// d's proposal B, cut to a first library): seven combat rooms of five families, two elite rooms, two exit rooms. Each grid
    /// is written as if she comes in from the bottom (<see cref="RoomTemplate"/> says the characters). The order of the list is
    /// part of the generator: changing it changes floors. Families, first floors, weights and the themes each suits are first
    /// guesses. To add a room: a grid here, then the library test (EditMode RoomTemplatesTests), which turns red for a grid that
    /// breaks a rule in any of its eight orientations. A template that fails its check is never drawn, so a bad grid fails a
    /// test, never a run, and changes no floor; one that cannot be read is left out the same way (<see cref="Errors"/>).
    /// </summary>
    public static class RoomTemplateLibrary
    {
        static readonly RoomTheme[] Shrine = { RoomTheme.Shrine, RoomTheme.TombHall };

        static readonly (string name, string family, RoomTemplateFor kind, int from, float weight, RoomTheme[] suits, bool farNotSouth, string grid)[] Grids =
        {
            // She comes in at one end, the shooters stand at the far end, the swarm comes down the length; the chapels'
            // buttresses are cover from the far end. The way on: the far end or a side door by it. 5 x 9 cells (20 x 36 m).
            ("nave", "nave", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.TombHall, RoomTheme.Shrine }, false, @"
                . O O O .
                O h . h O
                # . . . #
                . c . c .
                . . . . .
                # . c . #
                . . . . .
                . . . . .
                . I I I ."),
            // The same hall closed by an apse with its eye-catcher; the way on is a side door just before the apse, so the whole
            // hall must be crossed.
            ("nave-apse", "nave", RoomTemplateFor.Combat, 1, 1f, Shrine, false, @"
                # . A . #
                # . . . #
                O h . h O
                . . . . .
                . c . c .
                # . . . #
                . . . . .
                . . . . .
                . I I I ."),
            // An L with arms 16 m wide: in at the foot of one arm, out at the far end of the other; the shooters wait round the
            // corner, out of sight from the door.
            ("corner", "corner", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Archive, RoomTheme.Ossuary }, false, @"
                . . . . . . .
                O h . . . . .
                O . . . . . .
                O . . . c . .
                # # ? . . . .
                # # # . . . .
                # # # # I I ."),
            // A short arm turns into a bar 32 m long: the turn opens on a long field at once.
            ("corner-long", "corner", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Archive, RoomTheme.TombHall }, false, @"
                . . . . . . . .
                O h . . . . . .
                O . . . . . . .
                O . . . . c . .
                # # # # . . . .
                # # # # . . . .
                # # # # # I I ."),
            // Two chambers joined by a waist 12 m wide: the near one's swarm, the far one's shooters behind cover; the waist is
            // the place to hold, or the trap.
            ("twin", "twin", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.Ossuary, RoomTheme.Archive }, false, @"
                . . O O O . .
                O h . c . h O
                . . . . . . .
                # # . . . # #
                # ? . . . ? #
                . . . . . . .
                . . c . c . .
                . . I I I . ."),
            // Four great piers and four pillars round an open middle, an apse wall with its eye-catcher, side doors between the
            // piers: she circles, the shooters lose their line.
            ("colonnade", "pillars", RoomTemplateFor.Combat, 1, 1f, Shrine, false, @"
                # . . A . . #
                . . h . h . .
                . # c . c # .
                O . . . . . O
                . # c . c # .
                . . . . . . .
                . . I I I . ."),
            // She comes in at the middle of a long side, the eye-catcher straight ahead and enemies at both ends: a crossfire.
            // The way on is at one end.
            ("transept", "transept", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.TombHall, RoomTheme.Archive }, false, @"
                . . # . A . # . .
                O h . . . . . h O
                O . c . . . c . O
                O . . . . . . . O
                . . . I I I . . ."),
            // The elite waits in the mouth of an apse, before its altar, between its shoulders; two piers mid-room; side doors.
            ("knights-apse", "elite", RoomTemplateFor.Elite, 1, 1f, Shrine, false, @"
                # # . A . # #
                # . . e . . #
                . h . . . h .
                O . . . . . O
                . # . . . # .
                . . . . . . .
                . . I I I . ."),
            // A cross with arms 12 m wide: in by the foot, the elite at the crossing, shooters at the head; the solid corners
            // can be kited round. About 37 % solid.
            ("cross-hall", "elite", RoomTemplateFor.Elite, 3, 1f, new[] { RoomTheme.Shrine, RoomTheme.Ossuary }, false, @"
                # # . A . # #
                # # h . h # #
                . . . . . . .
                O . . e . . O
                . . . . . . .
                # # . . . # #
                # # . I . # #"),
            // An aisled hall, two rows of piers, the throne in a recess at the far wall; the act boss at the middle.
            ("throne-nave", "throne", RoomTemplateFor.Exit, 1, 1f, new[] { RoomTheme.ThroneRoom }, true, @"
                # # . A A . # #
                . . h . . h . .
                . # . . . . # .
                . . . . . . . .
                . . . . . . . .
                . # . . . . # .
                . . . . . . . .
                . . I I I I . ."),
            // A round hall entered through a slot in a solid front wall, two piers flanking the approach, the throne at the far
            // wall.
            ("throne-arcade", "throne", RoomTemplateFor.Exit, 1, 1f, new[] { RoomTheme.ThroneRoom }, true, @"
                # # . A A . # #
                # . . . . . . #
                . . . . . . . .
                . . . . . . . .
                . . . . . . . .
                . . # . . # . .
                # . . . . . . #
                # # # I I # # #"),
        };

        static readonly List<string> s_errors = new List<string>();

        /// <summary>The library, in its order: every grid above that could be read.</summary>
        public static readonly IReadOnlyList<RoomTemplate> All = Build();

        /// <summary>Why a grid of the library could not be read (none, while the library test is green).</summary>
        public static IReadOnlyList<string> Errors => s_errors;

        static List<RoomTemplate> Build()
        {
            var list = new List<RoomTemplate>();
            foreach (var g in Grids)
            {
                try
                {
                    list.Add(RoomTemplate.Parse(g.name, g.family, g.kind, g.from, g.weight, g.suits, g.farNotSouth, g.grid));
                }
                catch (FormatException e)
                {
                    s_errors.Add(e.Message);
                }
            }
            return list;
        }

        // The tests' seam: a library in place of All (a copy with a bad template added, to show it is never drawn).
        static IReadOnlyList<RoomTemplate> s_override;
        static int s_libraryVersion;
        static readonly Dictionary<string, IReadOnlyList<RoomTemplate>> s_usable = new Dictionary<string, IReadOnlyList<RoomTemplate>>();

        internal static IReadOnlyList<RoomTemplate> Override
        {
            get => s_override;
            set
            {
                lock (s_usable)
                {
                    s_override = value;
                    s_libraryVersion++;
                    s_usable.Clear();
                }
            }
        }

        /// <summary>The library the generator draws from: <see cref="All"/>, or the tests' copy.</summary>
        public static IReadOnlyList<RoomTemplate> Current => s_override ?? All;

        /// <summary>
        /// The templates whose check (<see cref="RoomTemplates.Faults"/>, turned as drawn: in from the south, not mirrored)
        /// finds nothing with these values, in the library's order. The rules do not change when a room is turned or mirrored,
        /// and the library test checks all eight. Kept by the numbers the check reads.
        /// </summary>
        public static IReadOnlyList<RoomTemplate> Usable(LevelValues v)
        {
            v = v ?? new LevelValues();
            string key = Key(v);
            lock (s_usable)
            {
                if (s_usable.TryGetValue(key, out IReadOnlyList<RoomTemplate> had)) return had;
                IReadOnlyList<RoomTemplate> list = Current.Where(t => RoomTemplates.Faults(t, LevelGenerator.South, false, v, null) == 0).ToList();
                s_usable[key] = list;
                return list;
            }
        }

        static string Key(LevelValues v)
        {
            ShapeValues s = v.ShapeRules ?? new ShapeValues();
            TemplateValues t = v.TemplateRules ?? new TemplateValues();
            RoomSizeRange c = (v.Plan ?? new LevelPlan()).CombatSize;
            return string.Join("|", new object[]
            {
                s_libraryVersion, v.CellSize, v.DoorWidth, v.GridCells, s.MiddleSquare, s.ExitMiddleSquare, s.TreasureMiddleSquare, s.DoorStraightIn,
                s.MostSolidShare, s.ExitMostSolidShare, t.MostSolidShare, t.MostDrawn, c.Min, c.Max,
            }.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)));
        }
    }
}
