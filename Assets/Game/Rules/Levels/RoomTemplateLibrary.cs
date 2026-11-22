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

        /// <summary>The generator's version that brought the second set (work order 072): drawn only with <see cref="LevelValues.MoreTemplates"/> on.</summary>
        public const int MoreSince = 17;

        // The second set (work order 072; Kristian, the evening of 2026-11-21, ticked "Flere rom-maler": eleven repeat quickly over
        // five floors of eight combat rooms). Since that evening a room is a fight (work order 070: she steps in and the whole
        // room comes at her at once) and a cave (work order 076: its walls go all the way up, a pillar hides what is behind it),
        // so each is an arena for a whole pack entered at one door: cover to break a line of sight with, room to kite a loop in,
        // places a shooter stands that she must go and get, no dead end inside the door, and no solid block more than two cells
        // across, so the camera (from the south, whichever way the room is turned) never loses more than two cells behind one.
        // Every room keeps work order 051's open square of 12 m round its middle, so a ring's island stands beside the middle,
        // never on it. Weights 1, as the eleven's; families, first floors and themes are first guesses. After the eleven in the
        // list, so with the second set off the draws are version 16's.
        static readonly (string name, string family, RoomTemplateFor kind, int from, float weight, RoomTheme[] suits, bool farNotSouth, string grid)[] MoreGrids =
        {
            // A loop round a 2 x 2 island just past the middle: she kites round it, the shooters behind it lose her line; a
            // lane 4 m wide runs behind the island to the far doors. 6 x 8 cells (24 x 32 m).
            ("ring", "ring", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Shrine, RoomTheme.Ossuary }, false, @"
                . O . . O .
                . . # # . .
                . h # # h .
                . . . . . .
                O . . . . O
                . . . c . .
                . . . . . .
                . . I I . ."),
            // Four L-shaped piers round an open chamber at the middle, a gap in each side: she fights in the chamber and steps
            // out through a gap to break a line, the shooters stand outside the ring at the far corners. 7 x 8.
            ("broken-ring", "ring", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.Ossuary, RoomTheme.TombHall }, false, @"
                . h . O . h .
                . # # . # # .
                . # . . . # .
                O . . . . . O
                . # . . . # .
                . # # . # # .
                . . . . . . .
                . . I I I . ."),
            // Two islands 2 x 2 either side of the middle: a figure of eight to kite round, a shooter behind each island at the
            // side doors. 9 x 6.
            ("two-islands", "islands", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Archive, RoomTheme.Ossuary }, false, @"
                . . . . O . . . .
                O h . . . . . h O
                . # # . . . # # .
                . # # . c . # # .
                . . . . . . . . .
                . . . I I I . . ."),
            // A hall of free-standing pillars in two rows, a clear aisle 12 m wide down the middle and a side aisle behind each
            // row: up one side, down the other; the shooters stand behind the far pillars. 7 x 8.
            ("pillared-hall", "hall", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.TombHall, RoomTheme.Shrine }, false, @"
                . . . O . . .
                h # . . . # h
                O . . . . . O
                . # . . . # .
                . . . . . . .
                . # . . . # .
                . . . . . . .
                . . I I I . ."),
            // A tongue of rock 2 x 2 from the far wall splits the far end into two bays, a shooter in each and a way on at the end
            // of each: whichever she goes for, the other has her back. 8 x 7.
            ("horseshoe", "horseshoe", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Ossuary, RoomTheme.Archive }, false, @"
                # . . # # . . #
                O h . # # . h O
                . . . . . . . .
                . . . . . . . .
                . . . c . . . .
                . . . . . . . .
                . . . I I . . ."),
            // Two chambers side by side and a neck three cells (12 m) wide between them at the middle (work order 051's open
            // square round the middle needs the three): she comes in to the one, the way on is from the other, and the neck is
            // where the pack meets her. Its cover marks draw the floor's props to the chambers' outer sides, away from the neck's
            // mouths, and the tomb hall is not a theme it suits (the first Unity run of 2026-11-22 found a tomb hall's coffin row
            // and two pillars closing the neck to a squeeze). 9 x 6.
            ("hourglass", "twin", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.Archive, RoomTheme.Ossuary }, false, @"
                # . . . # . O . #
                . c . . # . . . .
                . h . . . . . h O
                . . . . . . . . .
                . . . . . . . c .
                # . I . # . . . #"),
            // Two corners stepped, so the floor runs on a slant from the door in to the far corner: the pack comes down the slant,
            // the shooter waits at its top. 8 x 7.
            ("stepped", "stepped", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Archive, RoomTheme.TombHall }, false, @"
                . . O O . . # #
                O h . . . . . #
                . . . . c . . .
                . . . . . . . .
                . . . . . . . .
                # . . . . . . .
                # # . . . I . ."),
            // Three bays 8 m deep along the far wall between piers, a shooter in each: she has to go into them, one by one, with
            // the rest of the pack at her back. The side bays are 8 m wide, the middle one 16 m, so the eye-catcher at the far
            // wall's middle (a shrine's altar, basin and candles) has room round it (the first Unity run of 2026-11-22 found the
            // shrine's set in a middle bay 8 m wide leaving a pocket of floor no one could reach). 10 x 5 (40 x 20 m).
            ("alcoves", "alcoves", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Archive, RoomTheme.Shrine }, false, @"
                . h # h . O h # h .
                . . # . . . . # . .
                O . . . . . . . . O
                . . . . . . . . . .
                . . . . I I . . . ."),
            // A wide, shallow arena with a pillar near each corner: open ground to kite in, four pillars to duck behind. 9 x 5.
            ("arena", "arena", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.TombHall, RoomTheme.Ossuary }, false, @"
                . . . . O . . . .
                . # . h . h . # .
                O . . . . . . . O
                . # . . . . . # .
                . . . . I . . . ."),
            // A dais at the far wall marked only by four pillars, its eye-catcher between the back two and the shooters on it:
            // she must go up into it; a pier on each side wall halfway. 8 x 7.
            ("dais", "pillars", RoomTemplateFor.Combat, 1, 1f, new[] { RoomTheme.Shrine, RoomTheme.TombHall }, false, @"
                . . # A A # . .
                . . . h h . . .
                O . # . . # . O
                . . . . . . . .
                # . . . . . . #
                . . . . . . . .
                . . . I I . . ."),
            // Nothing alike on its two sides: a block 2 x 2 ahead on the right, a stub of wall with a shooter in the lane behind
            // it far right, a lone pier far left. 8 x 7.
            ("lopsided", "lopsided", RoomTemplateFor.Combat, 2, 1f, new[] { RoomTheme.Ossuary, RoomTheme.Archive }, false, @"
                . . . O . . . .
                O h . . . . # .
                . . # . . . # h
                . . . . . . . .
                . . . . . # # .
                . . . . . # # .
                . I I . . . . ."),
            // An arena for the elite: cut corners, four pillars round the middle where the elite stands, shooters before the far
            // wall's eye-catcher; she circles the pillars round it. Larger than the first two (36 x 28 m). 9 x 7.
            ("pit", "elite", RoomTemplateFor.Elite, 2, 1f, new[] { RoomTheme.Ossuary, RoomTheme.TombHall }, false, @"
                # # . . A . . # #
                # . . h . h . . #
                . . # . . . # . .
                O . . . e . . . O
                . . # . . . # . .
                # . . . . . . . #
                # # . I . I . # #"),
            // A long gallery for the elite: bays 8 m square between piers down both side walls, shooters in the far ones, the
            // elite before the eye-catcher at the far end; side doors at the far end. 7 x 9 (28 x 36 m).
            ("gallery", "elite", RoomTemplateFor.Elite, 1, 1f, new[] { RoomTheme.TombHall, RoomTheme.Shrine }, false, @"
                # # . A . # #
                . . . e . . .
                O h . . . h O
                # # . . . # #
                . . . . . . .
                . . . . . . .
                # # . . . # #
                . . . . . . .
                . . . I . . ."),
            // A great throne hall 36 m square: three pillars down each side, the corners cut, the throne at the far wall and the
            // act boss's ground open round the middle. 9 x 9.
            ("throne-pillars", "throne", RoomTemplateFor.Exit, 1, 1f, new[] { RoomTheme.ThroneRoom }, true, @"
                # . . . A . . . #
                . . . . . . . . .
                . # . . . . . # .
                . . . . . . . . .
                . # . . . . . # .
                . . . . . . . . .
                . # . . . . . # .
                . . . . . . . . .
                # . . . I . . . #"),
            // A wide throne hall, 40 m across, entered on its long side: two pillars either side of the boss's ground, the far
            // corners cut round the throne. 10 x 8.
            ("throne-wide", "throne", RoomTemplateFor.Exit, 1, 1f, new[] { RoomTheme.ThroneRoom }, true, @"
                # # . . A A . . # #
                # . . . . . . . . #
                . . # . . . . # . .
                . . . . . . . . . .
                . . . . . . . . . .
                . . # . . . . # . .
                . . . . . . . . . .
                # . . . I I . . . #"),
        };

        static readonly List<string> s_errors = new List<string>();

        /// <summary>The library, in its order: every grid above that could be read.</summary>
        public static readonly IReadOnlyList<RoomTemplate> All = Build();

        /// <summary>Why a grid of the library could not be read (none, while the library test is green).</summary>
        public static IReadOnlyList<string> Errors => s_errors;

        static List<RoomTemplate> Build()
        {
            var list = new List<RoomTemplate>();
            void Add(IEnumerable<(string name, string family, RoomTemplateFor kind, int from, float weight, RoomTheme[] suits, bool farNotSouth, string grid)> grids, int since)
            {
                foreach (var g in grids)
                {
                    try
                    {
                        RoomTemplate t = RoomTemplate.Parse(g.name, g.family, g.kind, g.from, g.weight, g.suits, g.farNotSouth, g.grid);
                        t.Since = since;
                        list.Add(t);
                    }
                    catch (FormatException e)
                    {
                        s_errors.Add(e.Message);
                    }
                }
            }
            Add(Grids, 15);
            Add(MoreGrids, MoreSince); // work order 072: after the eleven, so with the second set off the draws are version 16's
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
                // Work order 072: the second set only with LevelValues.MoreTemplates on; off, the list is the first eleven's, so the
                // draws are version 16's.
                IReadOnlyList<RoomTemplate> list = Current.Where(t => (v.MoreTemplates || t.Since < MoreSince) && RoomTemplates.Faults(t, LevelGenerator.South, false, v, null) == 0).ToList();
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
                s_libraryVersion, v.MoreTemplates, v.CellSize, v.DoorWidth, v.GridCells, s.MiddleSquare, s.ExitMiddleSquare, s.TreasureMiddleSquare, s.DoorStraightIn,
                s.MostSolidShare, s.ExitMostSolidShare, t.MostSolidShare, t.MostDrawn, c.Min, c.Max,
            }.Select(o => Convert.ToString(o, CultureInfo.InvariantCulture)));
        }
    }
}
