using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Makes a floor from a seed and the floor's number in its act: the same seed and number always give the same floor
    /// (for the same <see cref="Version"/> and the same values), and every seed gives one that
    /// <see cref="LevelValidator"/> finds no fault in.
    ///
    /// Layout (its own stream, "layout"). The entrance is put in the middle of the grid. Each room of the main path
    /// (the combat rooms in order, then the exit room) is put against a side of the room before it, sharing a stretch
    /// of wall at least a door and a wall cell either side of it long: no corridors. Sizes are drawn per room. A room
    /// may not overlap another or leave the grid, the path may not go the same way more than
    /// <see cref="LevelValues.MostInARow"/> times running, and the exit room may not touch the entrance (so it does not
    /// fold back on itself). The way on reads (decision 0008, 2026-11-19, work order 012): a room's way out is never
    /// on the wall of its way in, and the opposite wall is tried first, the two side walls after it in a drawn order.
    /// When a room fits nowhere, the room before it is taken back and put in its next place, and so on back along the
    /// path. Then the treasure rooms: combat rooms are taken in a drawn order and each gets one against a side wall
    /// (one that holds neither its door in nor its door out), until there are enough. Last, each door is put at a drawn
    /// place on the stretch its two rooms share, with wall on both sides of it.
    ///
    /// When an attempt cannot be finished (no place for a room even after taking rooms back, too many rooms put down
    /// and taken back, no place for the treasure rooms, or no place for a room's enemies), the next attempt starts again
    /// from a new stream made from the seed and the attempt's number. The result is still a function of the seed and
    /// the floor's number alone.
    ///
    /// Contents (their own stream, "contents"; work order 012): the player's start in the middle of the entrance. Each
    /// combat room draws how many enemies it holds from its floor's range (<see cref="LevelValues.EnemiesOf"/>), the
    /// exit room <see cref="LevelValues.ExitRoomShare"/> as many, and a mix from its floor's table; the enemies are
    /// shared out into groups (packs of swarmers, lines of shooters, brutes side by side, guards alone), and each group
    /// gets a point: shooters at the far side of the room from the door in, guards and brutes between, swarmers
    /// anywhere, every enemy of every group clear of the walls, the doors and the way in, and of the other groups. On the
    /// act's last floor the exit room holds the act boss, at its middle where it can stand, with a full room of others.
    /// Nothing in the treasure rooms.
    ///
    /// Version 2 (work order 008) gave each enemy point a kind of enemy. Version 3 (work order 009) added the props on
    /// their own stream ("props") after the contents, unless <see cref="LevelValues.Props"/> is off; the props stage
    /// never makes an attempt fail (a prop with no place is left out). Version 4 (work order 012): the chain of rooms,
    /// two treasure rooms, the floor's number, and the enemies in groups. Version 6 (work order 017, decision 0008 as
    /// amended 2026-11-20): the middle combat room of the chain is the elite room (<see cref="LevelPlan.EliteRoomNumber"/>),
    /// marked in the layout; in the contents it draws its combat room's groups and then its elite's kind from
    /// <see cref="LevelValues.EliteKinds"/>, and the elite is placed first after the act boss, with
    /// <see cref="LevelValues.EliteRoomToStand"/> more room round it than any other enemy keeps. Version 7 (work order
    /// 030): work order 023's caps on brutes and shooters in a room's mix together with the elite room; the elite is
    /// added after the capped mix is drawn, so it never counts against the room's range or its caps.
    /// Work order 027 (version 7 on its own branch): casters from floor 2, as their floor's mixes say
    /// (<see cref="CastersOf"/>), each in place of a shooter or a brute with no draw of its own, placed alone after the
    /// shooters at the far side, <see cref="LevelValues.CasterSpacing"/> from any other caster; an elite may be one from
    /// <see cref="LevelValues.EliteCasterFromFloor"/>. Floor 1 is unchanged.
    /// Work order 028 (version 8 on that branch): with <see cref="LevelValues.Themes"/> on (the default), each room gets a
    /// theme on its own stream ("themes", <see cref="RoomThemes"/>) just before the props, which dress it by its theme's
    /// recipe from the seven new props and the eleven; the layout and the enemies of every seed are what they were. With
    /// it off the themes stage is not run and every seed's props are those the generator gives without themes.
    /// Version 9 (work order 034, 2026-11-20): the two merged. The casters are behind <see cref="LevelValues.Casters"/>,
    /// off by default (on from version 14): off, no room and no elite is a caster and every seed's rooms, enemies and elites are those of
    /// version 7 (work order 030); on, they come in after the caps on the mix, each in place of a shooter or a brute the
    /// capped room holds. The themes are on by default.
    /// Version 10 (work order 041, Kristian's second session of 2026-11-20: "It is not quite intuitive where the chest
    /// rooms are; I end up double-checking several rooms for them. I need the run to be more linear", and asked how
    /// linear, fully): with <see cref="LevelValues.InALine"/> on (the default) the floor is one row. The treasure rooms are
    /// rooms of the chain: the entrance, the combat rooms with the treasure rooms between them as
    /// <see cref="LevelPlan.LineBetween"/> says (with the plan's four combat rooms, combat, combat, treasure, elite,
    /// treasure, combat), then the exit room; each put against the room before it by the same rules as the chain (sizes,
    /// the way on, no folding back), so every room has one door in and one out but the entrance and the exit, and there
    /// is no side room. The rooms hold what the same seed's floor of version 9 holds, room for room: that floor is laid
    /// out and filled first, without its props (its attempts, streams and validator as they were), and each combat room
    /// of the row in order, then the exit room, gets the groups of its namesake, the elite with the elite room's; they are
    /// given their points on the row's contents stream. So a floor holds as many rooms and as many enemies, of the same
    /// kinds, as version 9's of that seed. The themes and props follow on their own streams. With it off the floor of
    /// every seed is version 9's, field for field, but for the version.
    /// Version 11 (work order 046, Kristian's third session of 2026-11-20: "Etasjene er litt korte", the floors are a little
    /// short): a floor in a line has <see cref="LevelPlan.MoreCombatRooms"/> more ordinary combat rooms (one: five, the
    /// elite room the third, C C T L C T C). The four of version 9's floor keep their groups, in their order; the further
    /// ones come right after the elite room (as many before it as moving the middle needs), each with groups drawn as a
    /// combat room's (<see cref="DrawGroups"/>) on a stream of their own (<see cref="MoreRoomsStage"/>) for the attempt
    /// version 9's floor was made on, so no other room's enemies change. The row is laid out longer, so the rooms after
    /// the first ones of the row stand elsewhere than in version 10, with other doors, points, themes and props. With
    /// <see cref="LevelPlan.MoreCombatRooms"/> 0 every seed's floor is version 10's, field for field, but for the version.
    /// Version 13 (work order 051, Kristian's words of 2026-11-20: "Now we have very clear squares per room ... not walking
    /// into large office landscapes with monsters in them"; the research report d's proposal A): with
    /// <see cref="LevelValues.Shapes"/> on (the default), right after the layout each room's rectangle is carved on its own
    /// stream ("shapes", <see cref="RoomShapes"/>): some of its cells become solid, and a gate stands inside its door in. The
    /// rooms' places, sizes, order and doors are the layout's, so they are what they were. The contents keep off the solid
    /// cells (<see cref="LevelGeometry.EnemyPlaceFault"/>); when a room's enemies find no place, its shape gives way to its
    /// gate alone, then to its plain rectangle, and the contents are drawn again from the start of their stream
    /// (<see cref="PlaceAgainPlainer"/>), so an attempt fails only where it failed without the shapes. With it off every
    /// floor is version 12's (046 and 044 merged), field for field, but for the version.
    /// Version 14 (work order 054; Kristian, 2026-11-21, asked whether the casters should be on or off in the floors:
    /// "Castere på ja"): <see cref="LevelValues.Casters"/> is on by default, so the casters come into the floors as work
    /// order 027 built them: from floor 2 each combat room and the exit room hold the casters their mix and their floor
    /// allow, each in place of a shooter or a brute of the capped mix, and from <see cref="LevelValues.EliteCasterFromFloor"/>
    /// an elite may be one. Nothing about how they are drawn, capped or placed changed. Floor 1 holds none and is what it
    /// was; from floor 2 old seeds give other enemies, and with them other floors. With it off every floor is
    /// version 13's, field for field, but for the version.
    /// Version 15 (work order 058; Kristian, 2026-11-20: "we must take more inspiration from Diablo and Path of Exile 2 in how
    /// their generators make and design levels. We need a bit of a dungeon-crawler feeling", and on 2026-11-21 he chose "Rom-maler
    /// slik Diablo og PoE gjør"; the research report d's proposal B, cut to a first version): with <see cref="LevelValues.Templates"/>
    /// on (the default, read with the shapes and the line on), before each attempt's layout the row's rooms draw their
    /// templates on a stream of their own ("templates", <see cref="RoomTemplates.Draw"/>): the elite room one of the library's
    /// elite rooms, the exit room an exit room, about three in four ordinary combat rooms a combat room, by weight, never one
    /// twice on a floor nor two of a family in a row. A templated room takes no drawn size: its template, turned so its bottom
    /// is the wall she comes in by (and mirrored as drawn, else the other way), gives it its size, and every room stands
    /// against the room before it only where an O of the earlier one's template meets an I of the later one's
    /// (<see cref="RoomTemplates.CanJoin"/>); the door is drawn among those places (<see cref="RoomTemplates.DoorAmong"/>). In
    /// the carve stage the template is stamped in place of a drawn shape (its # cells, its ? cells drawn, the gate;
    /// <see cref="RoomShape.Template"/>), and a stamp that breaks a rule gives way to the drawn shapes. The contents, the props
    /// and the mood try the template's marked places first. The enemies are what they were (<see cref="FightsOf"/> is
    /// untouched: every room holds the same seed's groups, room for room); from the first templated room on, the rooms stand
    /// elsewhere, with other sizes, places and doors. With it off every floor is version 14's, field for field, but for the
    /// version.
    /// Version 16 (work order 059; Kristian, 2026-11-20: "bedre bruk av mappet ... litt dungen crawler følelse trenger vi, ikke
    /// inn i store kontorlandskap med monstre i", and on 2026-11-21 he chose "Ganger mellom rommene"; the research report d's
    /// proposal C, its first order): with <see cref="LevelValues.Passages"/> on (the default, read on a floor in a line with
    /// doors a cell wide) the rooms of the row stand apart (<see cref="TryLineApart"/>): each is put a drawn gap of
    /// <see cref="PassageValues.Fewest"/> to <see cref="PassageValues.Most"/> cells off the side of the room before it, by the
    /// chain's rules otherwise (the way on, the templates' sizes and marks, taking rooms back), on a grid of
    /// <see cref="PassageValues.GridCells"/>, and no two rooms touch. Then on a stream of their own ("passages") a passage one
    /// cell wide is drawn into each gap, from a door in the one room's wall (an O of its template) to a door in the next's (an
    /// I), straight or with a jog sideways in its middle (<see cref="Passages"/>); it is a room of its own kind in the row,
    /// E P C P C ... P X, its rectangle's cells off its path solid, holding nothing. A room entered from a passage has no gate:
    /// the passage is the threshold. The enemies are what they were (<see cref="FightsOf"/> is untouched: every room holds the
    /// same seed's groups, room for room); the rooms stand elsewhere, with other places, doors, points, themes and props. With
    /// it off every floor is version 15's, field for field, but for the version.
    /// Version 17 (work order 072; Kristian, the evening of 2026-11-21, asked what more to build, ticked "Flere rom-maler": the
    /// eleven templates repeat quickly over five floors of eight combat rooms): with <see cref="LevelValues.MoreTemplates"/> on
    /// (read with the templates on; OFF by default until Kristian has played its rooms in the room lab, so the default floors
    /// are version 16's) the library's second set (<see cref="RoomTemplateLibrary.MoreSince"/>: eleven
    /// combat rooms, two elite rooms and two exit rooms, made for a room that is a fight and a cave) is drawn beside the first
    /// eleven, by the same weights, families and rules, after them in the library's order. The enemies are what they were
    /// (<see cref="FightsOf"/> is untouched: every room holds the same seed's groups, room for room); from the first room that
    /// draws another template on, the rooms stand elsewhere, with other sizes, places and doors. With it off every floor is
    /// version 16's, field for field, but for the version.
    /// Version 18 (work order 087; Kristian, 2026-11-22, asked whether each floor should bring one new thing: "Ja, én ny ting per
    /// etasje", and both new kinds into the floors): with <see cref="LevelValues.ActPlan"/> on (the default) the floors follow the
    /// act's plan: floor 2 brings the guard and no caster, floor 3 the caster (<see cref="LevelValues.CastersFromFloor"/>, in a room
    /// or as the elite; at most <see cref="LevelValues.PlanCastersMostInRoom"/> a room: one on floors 3 and 4, two on floor 5) and
    /// the warder (<see cref="LevelWarders"/>, from <see cref="WarderFloorValues.FromFloor"/>), floor 4 the bomber
    /// (<see cref="LevelBombers"/>, from <see cref="BomberFloorValues.FromFloor"/>) beside the shooter's fan, floor 5 the act boss.
    /// A caster that the plan holds back leaves in its room the shooter or brute it would have taken the place of, and a warder or
    /// a bomber takes the place of a swarmer, so no room's count of enemies changes. The casters come into version 9's floor of
    /// <see cref="FightsOf"/>, so from floor 2 its contents, and with them the rooms that stand after a changed one, differ; the
    /// warders and bombers are put into its groups after it, on their own streams. With it off every floor is version 17's,
    /// field for field, but for the version.
    /// Version 19 (work order 088; Kristian, 2026-11-22, of the door that closes behind her: "vi bør balansere rommene med flere
    /// muligheter og, slik at man kan bruke miljøet til sin fordel", and of the duck, "Plasser dekning for den"): with
    /// <see cref="LevelValues.UsableRooms"/> on (the default) the props stage keeps the joined floor (<see cref="JoinedFloor"/>: no
    /// prop shuts a piece of floor off from the doors, and a way of whole cells runs through every combat and exit room; a place it
    /// refuses moves the prop to its next place, or leaves it out), and after a room's own props gives the room's fight its cover
    /// (<see cref="FightCover"/>, drawing nothing): a chain of tall cover from the fight's line toward the shooters and casters, and
    /// low props on the shooters' lines to duck behind. The layout, the shapes, the enemies and the themes are version 18's; the
    /// props differ in the rooms where a rule moved, dropped or added one, and the mood follows them. With it off every floor is
    /// version 18's, field for field, but for the version.
    /// Version 20 (work order 103; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "tronrommet bare i etasje 5 og egne
    /// utgangsrom i etasje 1–4"): with <see cref="LevelValues.ExitRoomsOfTheirOwn"/> on (the default) the exit room of floors 1 to 4
    /// draws its template from work order 089's exit rooms of their own only (the gate hall, the stair head, the sealed door) and is
    /// an exit hall, and floor 5's from the throne rooms only, a throne room with the act boss as before. The draw is the templates
    /// stream's as before; in a run, a floor is also told the room the floor before ended in (<see cref="LevelLayout.ExitBefore"/>,
    /// <see cref="WayDowns"/>) and passes over it while another can stand, so a run does not show the same one twice running; a floor
    /// built alone is told none. The enemies are version 19's (<see cref="FightsOf"/> is untouched); the exit rooms of floors 1 to 4
    /// stand elsewhere, with other sizes, doors, themes and props. With it off every floor is version 19's, field for field, but for
    /// the version.
    /// Version 21 (work order 104; Kristian ticked "Etasjene får hver sin ting" on 2026-11-24: "én ny ting å lære per etasje, en
    /// dominerende fiendemiks og tema per etasje"): with <see cref="LevelValues.FloorCharacter"/> on (the default) a room's mix is
    /// drawn by weight (<see cref="FloorCharacterRules.PickMix"/>: the floor's dominant one twice as often as each other) from one
    /// number of the contents' stream where it was drawn evenly; a combat room's theme weighs the floor's theme three times
    /// (<see cref="FloorCharacterRules.ThemeWeight"/>); the elite is no guard on floor 2 and no caster on floor 3
    /// (<see cref="FloorCharacterRules.EliteKinds"/>); and two templates come a floor or two later
    /// (<see cref="FloorCharacterRules.FirstFloorOf"/>). How many enemies a room holds is drawn as before, so the counts are version
    /// 20's; the kinds and the rooms after a changed draw differ. With it off every floor is version 20's, field for field, but for
    /// the version.
    /// </summary>
    public static partial class LevelGenerator
    {
        /// <summary>Raise this whenever a change makes old seeds give other levels, and update the golden maps in the tests.</summary>
        // 5: work order 014, fuller rooms and the act boss its own kind; 6: two versions at once, work order 017 (the elite
        // room) on main and work order 023 (at most three brutes and three shooters a room) on its branch; 7: work
        // order 030, the two merged (the room mixes capped, and the elite room with its elite counted apart from the caps),
        // and on another branch at the same time work order 027 (the casters from floor 2); 8: work order 028, the room
        // themes and seven new props, on that branch; 9: work order 034, the two lines merged (the capped mixes, the
        // casters behind LevelValues.Casters, off by default until 14, and the themes on).
        // 10: work order 041, the floor in one row (LevelValues.InALine, on; off, every seed's floor is version 9's).
        // 11: two versions at once: work order 044, the rooms' mood (LevelValues.RoomMood, on): an ossuary's skull and bone
        // piles banked at its far wall, from the props' own draws; every other room of every seed is version 10's, and with
        // it off every floor is; and on another branch work order 046, a fifth combat room in the row
        // (LevelPlan.MoreCombatRooms, 1; 0, every seed's floor is version 10's).
        // 12: the two merged (the night of 2026-11-20 to 21): the fifth combat room and the mood. With MoreCombatRooms 0
        // every floor is 044's version 11's; with RoomMood off too, version 10's.
        // 13: work order 051 merged in (the night of 2026-11-20 to 21): rooms that are not boxes (LevelValues.Shapes, on:
        // carved outlines and a gate inside every door, on the stream "shapes"; off, every floor is version 12's).
        // 14: work order 054, the casters on in the floors (LevelValues.Casters, on by default; off, every floor is version
        // 13's).
        // 15: work order 058, rooms drawn by hand (LevelValues.Templates, on: the library's templates, joined at their door
        // marks, on the stream "templates"; off, every floor is version 14's).
        // 16: work order 059, passages between the rooms (LevelValues.Passages, on: the rooms of the row apart, a passage one
        // cell wide between every two, on the stream "passages"; off, every floor is version 15's).
        // 17: work order 072, more rooms drawn by hand (LevelValues.MoreTemplates, on: the library's second set of fifteen
        // templates drawn beside the first eleven; off, every floor is version 16's; off by default since 2026-11-22, so the
        // default floors are version 16's until Kristian has played the second set in the room lab).
        // 18: work order 087, the act's plan, one new thing a floor (LevelValues.ActPlan, on: no caster before floor 3 and one
        // a room on floors 3 and 4, the warders from floor 3 and the bombers from floor 4 in the floors; off, every floor is
        // version 17's).
        // 19: work order 088, rooms you can use (LevelValues.UsableRooms, on: no prop shuts floor off and a way of whole cells runs
        // through every room, a chain of tall cover and low cover for the duck in every fight with shooters or casters; off, every
        // floor is version 18's).
        // 20: work order 103, exit rooms of their own (LevelValues.ExitRoomsOfTheirOwn, on: floors 1 to 4 end in the gate hall, the
        // stair head or the sealed door, not the one the floor before in a run ended in; floor 5 in the throne room; off, every floor
        // is version 19's).
        // 21: work order 104, a dominant thing a floor (LevelValues.FloorCharacter, on: each floor's dominant mix twice as often, its
        // theme three times as likely, no kind's elite on the kind's own floor, the corner-long from floor 2 and the nave-apse from
        // floor 3; off, every floor is version 20's).
        public const int Version = 21;
        public const string MoreRoomsStage = "more rooms"; // work order 046: the enemies of the row's further combat rooms

        public const string ShapesStage = RoomShapes.Stage; // work order 051
        public const string LayoutStage = "layout";
        public const string ContentsStage = "contents";
        public const string PropsStage = "props";
        public const string ThemesStage = RoomThemes.Stage; // work order 028
        public const string TemplatesStage = RoomTemplates.Stage; // work order 058

        // The sides of a room, as the ways a path can step.
        public const int North = 0, East = 1, South = 2, West = 3;

        public static int Opposite(int side) => (side + 2) % 4;

        /// <summary>The first floor of an act, for callers that do not say.</summary>
        public static LevelLayout Generate(ulong seed, LevelValues values = null, int contentsWarmUp = 0, int propsWarmUp = 0) =>
            Generate(seed, 1, values, contentsWarmUp, propsWarmUp);

        /// <summary>
        /// The floor of this seed and number (1 to <see cref="LevelValues.FloorsPerAct"/>). <paramref name="contentsWarmUp"/>
        /// throws away that many draws at the start of the contents stage, and <paramref name="propsWarmUp"/> of the props
        /// stage; they are there for the tests that show a change to one stage leaves the others alone. Work order 103:
        /// <paramref name="exitBefore"/>, in a run, the exit room of its own the floor before ended in (<see cref="WayDowns.Shown"/>),
        /// which this floor's exit room passes over while another can stand; null (a floor alone, or a run's first) for none.
        /// </summary>
        public static LevelLayout Generate(ulong seed, int floor, LevelValues values = null, int contentsWarmUp = 0, int propsWarmUp = 0, string exitBefore = null)
        {
            values = values ?? new LevelValues();
            floor = Math.Max(1, Math.Min(values.FloorsPerAct, floor));
            // Work order 041: in a line, each room holds what the same seed's floor of version 9 holds there.
            List<List<EnemyGroup>> fights = values.InALine ? FightsOf(seed, floor, values) : null;
            if (values.BombersInFloors) LevelBombers.Put(fights, seed, floor, values); // work order 073; on with the act's plan (087)
            if (values.WardersInFloors) LevelWarders.Put(fights, seed, floor, values); // work order 079; on with the act's plan (087)
            List<string> faults = null;
            for (int attempt = 0; attempt < values.Attempts; attempt++)
            {
                var level = new LevelLayout
                {
                    Seed = seed, Version = Version, GridCells = values.GridCells, CellSize = values.CellSize, Attempt = attempt + 1,
                    Floor = floor, InALine = values.InALine, ExitBefore = exitBefore, // work order 103
                };
                LevelRandom layout = LevelRandom.Stream(seed, LayoutStage, attempt);
                if (!(values.InALine ? values.Passages && values.DoorWidth == 1 ? TryLineApart(level, layout, values) : TryLine(level, layout, values) // work order 059
                                     : TryLayout(level, layout, values))) continue;

                if (values.Shapes && level.InALine) RoomShapes.Carve(level, LevelRandom.Stream(seed, ShapesStage, attempt), values); // work order 051
                LevelRandom contents = LevelRandom.Stream(seed, ContentsStage, attempt);
                for (int i = 0; i < contentsWarmUp; i++) contents.NextBits();
                if (!(fights != null ? PlaceFights(level, fights, contents, values) : PlaceContents(level, contents, values))
                    && !PlaceAgainPlainer(level, fights, seed, attempt, contentsWarmUp, values)) continue; // work order 051

                faults = LevelValidator.Validate(level, values);
                if (faults.Count > 0) continue;
                if (!values.Props) return level;

                // The props never send the generator to another attempt: that would change the rooms of an old seed.
                // Work order 028: the rooms' themes, on a stream of their own, so the props stream starts where it did.
                if (values.Themes) RoomThemes.Assign(level, LevelRandom.Stream(seed, ThemesStage, attempt), values);
                LevelRandom props = LevelRandom.Stream(seed, PropsStage, attempt);
                for (int i = 0; i < propsWarmUp; i++) props.NextBits();
                PropPlacer.Place(level, props, values);
                faults = LevelValidator.Validate(level, values);
                if (faults.Count == 0) return level;
                throw new InvalidOperationException("The props of seed " + LevelSeed.ToText(seed) + " break a rule: " + string.Join("; ", faults));
            }
            throw new InvalidOperationException("No valid level for seed " + LevelSeed.ToText(seed) + " floor " + floor + " in " + values.Attempts + " attempts"
                                                + (faults != null ? ": " + string.Join("; ", faults) : ""));
        }

        // ---------------- Layout ----------------

        struct Placement
        {
            public int Side;
            public CellRect Rect;
        }

        static bool TryLayout(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            LevelPlan plan = v.Plan;
            int grid = v.GridCells;
            int need = v.DoorWidth + 2;

            var kinds = new List<RoomKind> { RoomKind.Entrance };
            for (int i = 0; i < plan.CombatRooms; i++) kinds.Add(RoomKind.Combat);
            kinds.Add(RoomKind.Exit);

            var path = new List<CellRect>();
            var steps = new List<int>(); // steps[i]: the side of room i that room i + 1 stands against
            if (!PlaceChain(kinds, rng, v, path, steps)) return false;

            // The treasure rooms, each against a side wall of a different combat room (path rooms 1 to CombatRooms).
            var taken = new List<CellRect>(path);
            var treasureOf = new Dictionary<int, CellRect>();
            var combats = Enumerable.Range(1, plan.CombatRooms).ToList();
            rng.Shuffle(combats);
            foreach (int c in combats)
            {
                if (treasureOf.Count == plan.TreasureRooms) break;
                DrawSize(rng, plan.TreasureSize, out int w, out int h);
                int wayIn = Opposite(steps[c - 1]), wayOut = steps[c];
                var sides = new List<int> { North, East, South, West }.Where(s => s != wayIn && s != wayOut).ToList();
                rng.Shuffle(sides);
                foreach (Placement p in Candidates(rng, path[c], w, h, need, sides))
                {
                    if (!Fits(p.Rect, grid, taken, taken.Count)) continue;
                    treasureOf[c] = p.Rect;
                    taken.Add(p.Rect);
                    break;
                }
            }
            if (treasureOf.Count < plan.TreasureRooms) return false;

            // The rooms in the order of the run: each treasure room right after its combat room.
            var parentOrder = new List<int>(); // for each room, the order of the room it is entered from (-1 for the entrance)
            int previousOnPath = -1;
            int elite = plan.EliteRoomNumber(plan.CombatRooms); // work order 017: path rooms 1 to CombatRooms are the combat rooms
            for (int i = 0; i < path.Count; i++)
            {
                int order = level.Rooms.Count;
                level.Rooms.Add(new LevelRoom { Kind = kinds[i], Rect = path[i], Order = order, Elite = kinds[i] == RoomKind.Combat && i == elite });
                parentOrder.Add(previousOnPath);
                previousOnPath = order;
                if (treasureOf.TryGetValue(i, out CellRect treasure))
                {
                    level.Rooms.Add(new LevelRoom { Kind = RoomKind.Treasure, Rect = treasure, Order = level.Rooms.Count });
                    parentOrder.Add(order);
                }
            }

            // The doors, in the order of the run.
            for (int r = 1; r < level.Rooms.Count; r++)
            {
                LevelDoor door = DoorBetween(rng, level.Rooms[parentOrder[r]], level.Rooms[r], v.DoorWidth);
                if (door == null) return false;
                level.Doors.Add(door);
            }
            return true;
        }

        /// <summary>
        /// The floor in one row (work order 041): the entrance, the rooms of <see cref="LevelPlan.LineBetween"/> (the combat
        /// rooms with the treasure rooms between them) and the exit room, each put against the room before it by the rules
        /// the chain of rooms keeps (<see cref="PlaceChain"/>), the plan's middle combat room the elite room; then the doors,
        /// each between a room and the next, in the order of the run.
        /// </summary>
        static bool TryLine(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            LevelPlan plan = v.Plan;
            var kinds = new List<RoomKind> { RoomKind.Entrance };
            kinds.AddRange(plan.LineBetween());
            kinds.Add(RoomKind.Exit);

            var path = new List<CellRect>();
            var steps = new List<int>();
            int elite = plan.EliteRoomNumber(plan.LineCombatRooms), combat = 0; // work order 046: the row's
            // Work order 058: the rooms' templates, on their own stream, before the layout puts the rooms down.
            List<RoomTemplate> templates = null;
            List<bool> mirrors = null;
            List<TemplatePlacement> placed = null;
            if (v.Templates && v.Shapes)
            {
                RoomTemplates.Draw(kinds, elite, level.Floor, LevelRandom.Stream(level.Seed, TemplatesStage, level.Attempt - 1), v, out templates, out mirrors, level.ExitBefore); // 103
                placed = new List<TemplatePlacement>();
                level.Templates = true;
            }
            if (!PlaceChain(kinds, rng, v, path, steps, templates, mirrors, placed)) return false;

            for (int i = 0; i < path.Count; i++)
            {
                if (kinds[i] == RoomKind.Combat) combat++;
                level.Rooms.Add(new LevelRoom { Kind = kinds[i], Rect = path[i], Order = i, Elite = kinds[i] == RoomKind.Combat && combat == elite, Template = placed?[i] });
            }
            for (int r = 1; r < level.Rooms.Count; r++)
            {
                LevelDoor door = level.Rooms[r - 1].Template != null || level.Rooms[r].Template != null
                    ? RoomTemplates.DoorAmong(rng, level.Rooms[r - 1], level.Rooms[r], v.DoorWidth) // work order 058: at their door marks
                    : DoorBetween(rng, level.Rooms[r - 1], level.Rooms[r], v.DoorWidth);
                if (door == null) return false;
                level.Doors.Add(door);
            }
            return true;
        }

        /// <summary>
        /// Puts the rooms of a chain, each of its kind's drawn size against a side of the room before it (the entrance in
        /// the middle of the grid), into <paramref name="path"/>, and the side each stands against into
        /// <paramref name="steps"/>; false when they fit nowhere. The rules of the class's summary: the way on, no more than
        /// <see cref="LevelValues.MostInARow"/> steps the same way, the exit room never touching the entrance, rooms taken
        /// back when the next fits nowhere. Work order 058: with <paramref name="templates"/> (null from <see cref="TryLayout"/> and
        /// with the templates off) a room with one takes its turned size, tried on each side for its drawn mirror first and the
        /// other after (never standing to the south of the room before when its far wall may not be south), and every room
        /// stands against the room before only where their door marks meet (<see cref="RoomTemplates.CanJoin"/>);
        /// <paramref name="placed"/> keeps each room's turned template (null for none) in step with <paramref name="path"/>.
        /// </summary>
        static bool PlaceChain(List<RoomKind> kinds, LevelRandom rng, LevelValues v, List<CellRect> path, List<int> steps,
                               List<RoomTemplate> templates = null, List<bool> mirrors = null, List<TemplatePlacement> placed = null)
        {
            LevelPlan plan = v.Plan;
            int grid = v.GridCells;
            int need = v.DoorWidth + 2;
            int placements = 0;

            DrawSize(rng, plan.EntranceSize, out int sw, out int sh);
            path.Add(new CellRect((grid - sw) / 2, (grid - sh) / 2, sw, sh));
            placed?.Add(null);
            if (!Fits(path[0], grid, path, 0)) return false;

            bool TooStraight(int side)
            {
                if (steps.Count < v.MostInARow) return false;
                for (int k = steps.Count - v.MostInARow; k < steps.Count; k++)
                    if (steps[k] != side) return false;
                return true;
            }

            bool PlaceMain(int i)
            {
                if (i == kinds.Count) return true;
                RoomTemplate template = templates != null && i < templates.Count ? templates[i] : null; // work order 058
                int w = 0, h = 0;
                if (template == null) DrawSize(rng, plan.SizeOf(kinds[i]), out w, out h);
                // The way on (work order 012): from the entrance any way; after it straight on first, then a turn to
                // either side in a drawn order, never back through the wall she came in by.
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
                if (template != null) return PlaceTemplate(i, template, sides);
                foreach (Placement c in Candidates(rng, path[i - 1], w, h, need, sides))
                {
                    if (placements >= v.PlacementsPerAttempt) return false;
                    if (TooStraight(c.Side)) continue;
                    if (!Fits(c.Rect, grid, path, path.Count)) continue;
                    if (kinds[i] == RoomKind.Exit && c.Rect.Touches(path[0])) continue;
                    if (placed != null && !RoomTemplates.CanJoin(path[i - 1], placed[i - 1], c.Rect, null, v.DoorWidth)) continue; // work order 058
                    placements++;
                    if (Try(i, c, null)) return true;
                }
                return false;
            }

            // A room put down, then the rest of the chain after it; taken back when the rest fits nowhere.
            bool Try(int i, Placement c, TemplatePlacement o)
            {
                path.Add(c.Rect);
                steps.Add(c.Side);
                placed?.Add(o);
                if (PlaceMain(i + 1)) return true;
                path.RemoveAt(path.Count - 1);
                steps.RemoveAt(steps.Count - 1);
                placed?.RemoveAt(placed.Count - 1);
                return false;
            }

            // Work order 058: a room with a template, its size its template's turned so the door in is on the wall it stands
            // against, on each side in the order given, its drawn mirror first.
            bool PlaceTemplate(int i, RoomTemplate template, List<int> sides)
            {
                bool drawn = mirrors != null && i < mirrors.Count && mirrors[i];
                foreach (int side in sides)
                {
                    if (side == South && template.FarNotSouth) continue; // its far wall, the throne's, would be the south one
                    foreach (bool mirror in new[] { drawn, !drawn })
                    {
                        TemplatePlacement o = template.Oriented(Opposite(side), mirror);
                        foreach (Placement c in Candidates(rng, path[i - 1], o.Width, o.Height, need, new List<int> { side }))
                        {
                            if (placements >= v.PlacementsPerAttempt) return false;
                            if (TooStraight(c.Side)) continue;
                            if (!Fits(c.Rect, grid, path, path.Count)) continue;
                            if (kinds[i] == RoomKind.Exit && c.Rect.Touches(path[0])) continue;
                            if (!RoomTemplates.CanJoin(path[i - 1], placed?[i - 1], c.Rect, o, v.DoorWidth)) continue;
                            placements++;
                            if (Try(i, c, o)) return true;
                        }
                    }
                }
                return false;
            }

            return PlaceMain(1);
        }

        static void DrawSize(LevelRandom rng, RoomSizeRange range, out int w, out int h)
        {
            w = rng.Range(range.Min, range.Max);
            h = rng.Range(range.Min, range.Max);
        }

        /// <summary>
        /// Every place for a room of w by h against a side of <paramref name="a"/>, sharing at least
        /// <paramref name="need"/> cells of wall with it: the sides in the order given, and the places along each side in
        /// a drawn order.
        /// </summary>
        static IEnumerable<Placement> Candidates(LevelRandom rng, CellRect a, int w, int h, int need, List<int> sides)
        {
            var result = new List<Placement>();
            foreach (int side in sides)
            {
                var along = new List<Placement>();
                if (side == East || side == West)
                {
                    int x = side == East ? a.Right : a.X - w;
                    for (int y = a.Y - h + need; y <= a.Top - need; y++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                else
                {
                    int y = side == North ? a.Top : a.Y - h;
                    for (int x = a.X - w + need; x <= a.Right - need; x++)
                        along.Add(new Placement { Side = side, Rect = new CellRect(x, y, w, h) });
                }
                rng.Shuffle(along);
                result.AddRange(along);
            }
            return result;
        }

        /// <summary>Inside the grid and over none of the first <paramref name="count"/> rooms.</summary>
        static bool Fits(CellRect r, int grid, List<CellRect> rooms, int count)
        {
            if (r.X < 0 || r.Y < 0 || r.Right > grid || r.Top > grid) return false;
            for (int i = 0; i < count; i++)
                if (r.Overlaps(rooms[i])) return false;
            return true;
        }

        /// <summary>A door at a drawn place on the stretch of wall the two rooms share, with a wall cell either side; null if they share too little.</summary>
        static LevelDoor DoorBetween(LevelRandom rng, LevelRoom first, LevelRoom second, int width)
        {
            if (!LevelGeometry.SharedWall(first.Rect, second.Rect, out bool vertical, out int line, out int lo, out int hi)) return null;
            if (hi - lo < width + 2) return null;
            return new LevelDoor
            {
                RoomA = Math.Min(first.Order, second.Order),
                RoomB = Math.Max(first.Order, second.Order),
                Vertical = vertical,
                Line = line,
                From = rng.Range(lo + 1, hi - 1 - width),
                Width = width,
            };
        }

        // ---------------- Contents ----------------

        static bool PlaceContents(LevelLayout level, LevelRandom rng, LevelValues v)
        {
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.Kind == RoomKind.Entrance)
                {
                    room.PlayerStart = room.Middle(v.CellSize);
                    continue;
                }
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                List<EnemyGroup> groups = DrawGroups(room.Kind, level.Floor, rng, v);
                // The elite room (work order 017): what a combat room holds, and one elite of a kind the floor can have.
                if (room.Elite)
                {
                    EnemyKind[] kinds = v.EliteKinds(level.Floor);
                    groups.Add(new EnemyGroup(kinds[rng.Below(kinds.Length)], 1, elite: true));
                }
                if (!PlaceGroups(level, room, groups, rng, v)) return false;
            }
            return true;
        }

        /// <summary>
        /// What each room of the chain holds on the same seed's floor of version 9 (work order 041: in a line the floor's
        /// rooms and enemies are as many as they were): its combat rooms in order and then its exit room, each as its groups,
        /// the elite with its room's. Its attempts are version 9's, layout, contents and validator, without the props.
        /// </summary>
        static List<List<EnemyGroup>> FightsOf(ulong seed, int floor, LevelValues v)
        {
            for (int attempt = 0; attempt < v.Attempts; attempt++)
            {
                var level = new LevelLayout
                {
                    Seed = seed, Version = Version, GridCells = v.GridCells, CellSize = v.CellSize, Attempt = attempt + 1, Floor = floor,
                };
                if (!TryLayout(level, LevelRandom.Stream(seed, LayoutStage, attempt), v)) continue;
                if (!PlaceContents(level, LevelRandom.Stream(seed, ContentsStage, attempt), v)) continue;
                if (LevelValidator.Check(level, v).Count > 0) continue;
                var fights = new List<List<EnemyGroup>>();
                foreach (LevelRoom room in LevelGeometry.MainPath(level))
                    if (room.Kind == RoomKind.Combat || room.Kind == RoomKind.Exit) fights.Add(room.EnemyGroups.Select(g => g.Copy()).ToList());
                AddMoreFights(fights, seed, floor, attempt, v);
                return fights;
            }
            throw new InvalidOperationException("No floor of version 9 for seed " + LevelSeed.ToText(seed) + " floor " + floor + " in " + v.Attempts + " attempts");
        }

        /// <summary>
        /// The row's further combat rooms (work order 046, <see cref="LevelPlan.MoreCombatRooms"/>): each one's groups drawn as
        /// a combat room's on the floor's stream for them, put among version 9's combat rooms' (<paramref name="fights"/>,
        /// the exit room's last) so that the middle one, the elite room's, stays the row's middle: right after it, or as
        /// many before it as the middle moves on.
        /// </summary>
        static void AddMoreFights(List<List<EnemyGroup>> fights, ulong seed, int floor, int attempt, LevelValues v)
        {
            LevelPlan plan = v.Plan;
            int more = Math.Max(0, plan.MoreCombatRooms), n = Math.Max(0, plan.CombatRooms);
            if (more == 0 || n == 0 || fights.Count < n) return;
            LevelRandom rng = LevelRandom.Stream(seed, MoreRoomsStage, attempt);
            var made = new List<List<EnemyGroup>>();
            for (int i = 0; i < more; i++) made.Add(DrawGroups(RoomKind.Combat, floor, rng, v));
            int oldMiddle = n / 2 + 1, newMiddle = (n + more) / 2 + 1;
            int before = Math.Max(0, Math.Min(more, newMiddle - oldMiddle));
            // Combat room oldMiddle is at index oldMiddle - 1; those before it go in front of it, the rest right after it.
            fights.InsertRange(oldMiddle, made.Skip(before));
            fights.InsertRange(oldMiddle - 1, made.Take(before));
        }

        /// <summary>
        /// The contents of a floor in a line (work order 041): the player's start in the middle of the entrance, and in each
        /// combat room in order and then the exit room the groups of <paramref name="fights"/> (<see cref="FightsOf"/>), each
        /// given its point by the rules of <see cref="PlaceGroups"/>, from this stream. Nothing in the treasure rooms.
        /// </summary>
        static bool PlaceFights(LevelLayout level, List<List<EnemyGroup>> fights, LevelRandom rng, LevelValues v)
        {
            int next = 0;
            foreach (LevelRoom room in level.Rooms)
            {
                if (room.Kind == RoomKind.Entrance)
                {
                    room.PlayerStart = room.Middle(v.CellSize);
                    continue;
                }
                if (room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) continue;
                if (next >= fights.Count) return false;
                List<EnemyGroup> groups = fights[next++].Select(g => g.Copy()).ToList();
                if (!PlaceGroups(level, room, groups, rng, v)) return false;
            }
            return true;
        }

        /// <summary>
        /// What a room of this kind holds on this floor (work order 012), as groups: how many, drawn from the floor's
        /// range; a mix drawn from the floor's table; guards (each alone) as the mix says, twice as many in an exit room,
        /// no more than the floor allows; the rest shared out by the mix's shares, then into packs of swarmers of
        /// <see cref="LevelValues.PackFewest"/> to <see cref="LevelValues.PackMost"/>, lines of shooters and brutes side by
        /// side. On the act's last floor the exit room has the act boss besides.
        /// </summary>
        public static List<EnemyGroup> DrawGroups(RoomKind kind, int floor, LevelRandom rng, LevelValues v)
        {
            var groups = new List<EnemyGroup>();
            v.EnemyRange(kind, floor, out int fewest, out int most);
            if (most <= 0) return groups;
            int total = rng.Range(fewest, most);
            FloorEnemies f = v.EnemiesOf(floor);
            EnemyMix mix = f.Mixes != null && f.Mixes.Length > 0 ? FloorCharacterRules.PickMix(f.Mixes, floor, rng, v) : new EnemyMix("brutes", 0f, 0f, 1f, 0); // work order 104: off, one of them evenly as before

            int guardRoom = v.GuardsMost(kind, floor);
            if (kind == RoomKind.Exit && floor >= v.FloorsPerAct)
            {
                groups.Add(new EnemyGroup(EnemyKind.Boss, 1, boss: true)); // a guard until work order 014
                guardRoom--;
            }
            int guards = Math.Max(0, Math.Min(guardRoom, mix.Guards * (kind == RoomKind.Exit ? 2 : 1)));
            guards = Math.Min(guards, Math.Max(0, total - 2)); // room for two other kinds
            for (int i = 0; i < guards; i++) groups.Add(new EnemyGroup(EnemyKind.Guard));

            int rest = total - guards;
            int[] counts = Shares(rest, new[] { mix.Swarmers, mix.Shooters, mix.Brutes });
            int swarmers = counts[0], shooters = counts[1], brutes = counts[2];
            // Work order 023: at most so many brutes and shooters a room (one more of each in an exit room), the rest
            // swarmers; the room holds as many as before.
            int more = kind == RoomKind.Exit ? v.ExitRoomKindMore : 0;
            if (v.BrutesMostInRoom > 0 && brutes > v.BrutesMostInRoom + more)
            {
                swarmers += brutes - (v.BrutesMostInRoom + more);
                brutes = v.BrutesMostInRoom + more;
            }
            if (v.ShootersMostInRoom > 0 && shooters > v.ShootersMostInRoom + more)
            {
                swarmers += shooters - (v.ShootersMostInRoom + more);
                shooters = v.ShootersMostInRoom + more;
            }
            // The casters (work order 027): out of the room's count, each in place of a shooter or a brute (of the two the
            // kind there are more of, the shooters when as many), a swarmer only when there is neither. No draw: a mix
            // without casters, or the casters switched off (LevelValues.Casters), gives what it gave before. Work order
            // 034: AFTER the caps, so a caster takes the place of a shooter or a brute the capped room really holds: the
            // room's brutes, shooters and casters together are never more than the balance passes' capped room's brutes
            // and shooters, and the caps still hold (a caster only ever lowers a count).
            int casters = CastersOf(mix, kind, floor, v);
            for (int i = 0; i < casters; i++)
            {
                if (shooters > 0 && shooters >= brutes) shooters--;
                else if (brutes > 0) brutes--;
                else if (swarmers > 1) swarmers--;
                else
                {
                    casters = i;
                    break;
                }
            }
            AddSplit(groups, EnemyKind.Shooter, shooters, Math.Max(1, v.ShooterLineMost));
            AddSplit(groups, EnemyKind.Brute, brutes, Math.Max(1, v.BruteLineMost));
            AddSplit(groups, EnemyKind.Swarmer, swarmers, Math.Max(1, v.PackMost));
            for (int i = 0; i < casters; i++) groups.Add(new EnemyGroup(EnemyKind.Caster)); // each alone
            return groups;
        }

        /// <summary>
        /// The casters a room of this kind holds with this mix (work order 027): the mix's, twice as many in an exit room,
        /// no more than the floor allows (<see cref="LevelValues.CastersMost"/>).
        /// </summary>
        public static int CastersOf(EnemyMix mix, RoomKind kind, int floor, LevelValues v) =>
            mix == null ? 0 : Math.Max(0, Math.Min(v.CastersMost(kind, floor), mix.Casters * (kind == RoomKind.Exit ? 2 : 1)));

        /// <summary>
        /// <paramref name="total"/> shared by the shares: each kind with a share gets at least one (while there are
        /// enough), the floor of its share otherwise; what is left over or short goes to the first kind with a share
        /// (the swarmers), then the others.
        /// </summary>
        public static int[] Shares(int total, float[] shares)
        {
            var c = new int[shares.Length];
            if (total <= 0) return c;
            float sum = shares.Where(s => s > 0f).Sum();
            if (sum <= 0f)
            {
                c[c.Length - 1] = total;
                return c;
            }
            for (int k = 0; k < shares.Length; k++)
                if (shares[k] > 0f) c[k] = Math.Max(1, (int)Math.Floor(shares[k] / sum * total + 1e-6));
            int first = Array.FindIndex(shares, s => s > 0f);
            int diff = total - c.Sum();
            if (diff > 0) c[first] += diff;
            // Too many: taken back down to one each, the first kind first; with fewer than one each, the last kinds go without.
            for (int k = 0; diff < 0 && k < c.Length; k++)
            {
                int index = (first + k) % c.Length;
                int take = Math.Min(-diff, Math.Max(0, c[index] - 1));
                c[index] -= take;
                diff += take;
            }
            for (int index = c.Length - 1; diff < 0 && index >= 0; index--)
            {
                int take = Math.Min(-diff, c[index]);
                c[index] -= take;
                diff += take;
            }
            return c;
        }

        /// <summary>Splits <paramref name="count"/> of a kind into as few groups as hold at most <paramref name="most"/> each, as even as they can be.</summary>
        static void AddSplit(List<EnemyGroup> groups, EnemyKind kind, int count, int most)
        {
            if (count <= 0) return;
            int n = (count + most - 1) / most;
            for (int i = 0; i < n; i++)
            {
                int size = count / n + (i < count % n ? 1 : 0);
                groups.Add(new EnemyGroup(kind, size));
            }
        }

        /// <summary>
        /// A point for each group (work order 012), from the places on a grid in the room (<see cref="LevelValues.PointStep"/>
        /// apart) in a drawn order: the act boss at the middle if it can stand there, then the shooters' lines at the far
        /// side from the door in, then guards and brutes between, then the swarmers anywhere; a group takes the first place
        /// where every one of its enemies keeps the rules, its band first and anywhere after. False when a group has none.
        /// </summary>
        static bool PlaceGroups(LevelLayout level, LevelRoom room, List<EnemyGroup> groups, LevelRandom rng, LevelValues v)
        {
            double cs = v.CellSize, clear = v.WallClearance, step = v.PointStep;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            List<LevelDoor> doors = level.DoorsOf(room.Order).ToList();

            var places = new List<LevelPoint>();
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1;
            int ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    places.Add(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step));
            rng.Shuffle(places);
            double Far(LevelPoint p) => entry != null ? LevelGeometry.DistanceToDoor(p, entry, cs) : 0d;
            double farthest = places.Count > 0 ? places.Max(Far) : 0d;

            bool Fits(EnemyGroup g, LevelPoint p, out LevelPoint[] members)
            {
                members = LevelGeometry.MemberPlaces(level, room, p, g, v);
                double extra = LevelGeometry.ExtraRoom(g, v);
                foreach (LevelPoint m in members)
                    if (LevelGeometry.EnemyPlaceFault(level, room, m, entry, doors, v, extra) != null) return false;
                for (int k = 0; k < room.EnemyPoints.Count; k++)
                {
                    double apart = Math.Max(extra, LevelGeometry.ExtraRoom(room.EnemyGroups[k], v));
                    if (room.EnemyPoints[k].DistanceTo(p) < v.EnemySpacing - 1e-9) return false;
                    // Two casters never stand near each other (work order 027).
                    if (g.Kind == EnemyKind.Caster && room.EnemyGroups[k].Kind == EnemyKind.Caster
                        && room.EnemyPoints[k].DistanceTo(p) < v.CasterSpacing - 1e-9) return false;
                    foreach (LevelPoint other in room.EnemyPlaces[k])
                        foreach (LevelPoint m in members)
                            if (other.DistanceTo(m) < v.MemberSpacing + apart - 1e-9) return false;
                }
                return true;
            }

            bool Put(EnemyGroup g, LevelPoint p, LevelPoint[] members)
            {
                room.EnemyPoints.Add(p);
                room.EnemyGroups.Add(g);
                room.EnemyPlaces.Add(members);
                return true;
            }

            bool Place(EnemyGroup g, double lo, double hi)
            {
                // Work order 058: the room's places on its template's mark for the group first (the elite's e, the shooters' and casters' h).
                char mark = RoomTemplates.MarkFor(room, g);
                if (mark != '\0')
                    foreach (LevelPoint p in places)
                        if (RoomTemplates.MarkAt(room, p, cs) == mark && Fits(g, p, out LevelPoint[] marked)) return Put(g, p, marked);
                foreach (bool banded in new[] { true, false })
                    foreach (LevelPoint p in places)
                    {
                        if (banded && entry != null)
                        {
                            double d = Far(p) / Math.Max(1e-9, farthest);
                            if (d < lo - 1e-9 || d > hi + 1e-9) continue;
                        }
                        if (Fits(g, p, out LevelPoint[] members)) return Put(g, p, members);
                    }
                return false;
            }

            // The casters (work order 027) after the shooters, at the far side as they are.
            int Rank(EnemyGroup g) => g.Boss ? 0 : g.Elite ? 1 : g.Kind == EnemyKind.Shooter ? 2 : g.Kind == EnemyKind.Caster ? 3
                                    : g.Kind == EnemyKind.Guard ? 4 : g.Kind == EnemyKind.Brute ? 5 : 6;
            foreach (EnemyGroup g in groups.OrderBy(Rank).ToList())
            {
                if (g.Boss && Fits(g, room.Middle(cs), out LevelPoint[] at))
                {
                    Put(g, room.Middle(cs), at);
                    continue;
                }
                bool done = g.Kind == EnemyKind.Shooter || g.Kind == EnemyKind.Caster || g.Kind == EnemyKind.Warder ? Place(g, v.FarBand, 1d) // 079: the warder at the back
                          : g.Kind == EnemyKind.Swarmer ? Place(g, 0d, 1d)
                          : Place(g, v.MiddleBandFrom, v.MiddleBandTo);
                if (!done) return false;
            }
            return true;
        }
    }

    /// <summary>Measures shared by the generator and the validator.</summary>
    public static partial class LevelGeometry
    {
        /// <summary>
        /// Whether two rooms meet along a side, and where: the wall line, whether it runs north and south (vertical),
        /// and the stretch of it they share, from cell <paramref name="lo"/> up to, not including, <paramref name="hi"/>.
        /// False when they do not meet along a side (apart, overlapping, or only at a corner).
        /// </summary>
        public static bool SharedWall(CellRect a, CellRect b, out bool vertical, out int line, out int lo, out int hi)
        {
            vertical = false;
            line = lo = hi = 0;
            if (a.Overlaps(b)) return false;
            if (a.Right == b.X || b.Right == a.X)
            {
                vertical = true;
                line = a.Right == b.X ? a.Right : a.X;
                lo = Math.Max(a.Y, b.Y);
                hi = Math.Min(a.Top, b.Top);
            }
            else if (a.Top == b.Y || b.Top == a.Y)
            {
                line = a.Top == b.Y ? a.Top : a.Y;
                lo = Math.Max(a.X, b.X);
                hi = Math.Min(a.Right, b.Right);
            }
            return hi > lo;
        }

        /// <summary>
        /// The wall of the room a door is on (work order 012): <see cref="LevelGenerator.North"/>, East, South or West;
        /// -1 when the door is on none of its walls.
        /// </summary>
        public static int WallOf(CellRect room, LevelDoor door)
        {
            if (door.Vertical) return door.Line == room.X ? LevelGenerator.West : door.Line == room.Right ? LevelGenerator.East : -1;
            return door.Line == room.Y ? LevelGenerator.South : door.Line == room.Top ? LevelGenerator.North : -1;
        }

        /// <summary>
        /// The rooms of the main path (all but the treasure rooms), in the order of the run; on a floor in a line (work order
        /// 041) every room, the treasure rooms being on the row.
        /// </summary>
        public static List<LevelRoom> MainPath(LevelLayout level) =>
            level.Rooms.Where(r => level.InALine || r.Kind != RoomKind.Treasure).OrderBy(r => r.Order).ToList();

        /// <summary>
        /// The door she comes into a room by: for a room of the main path, the door to the room before it on the path;
        /// for a treasure room off the side of a combat room, its one door. Null if there is none.
        /// </summary>
        public static LevelDoor EntryDoor(LevelLayout level, LevelRoom room)
        {
            if (room.Kind == RoomKind.Treasure && !level.InALine) return level.DoorsOf(room.Order).FirstOrDefault();
            List<LevelRoom> path = MainPath(level);
            int at = path.IndexOf(room);
            if (at <= 0) return null;
            int before = path[at - 1].Order;
            return level.Doors.FirstOrDefault(d => d.Joins(room.Order) && d.Other(room.Order) == before);
        }

        /// <summary>The door she leaves a room of the main path by, to the next room on the path; null for the exit room.</summary>
        public static LevelDoor ExitDoor(LevelLayout level, LevelRoom room)
        {
            List<LevelRoom> path = MainPath(level);
            int at = path.IndexOf(room);
            if (at < 0 || at + 1 >= path.Count) return null;
            int next = path[at + 1].Order;
            return level.Doors.FirstOrDefault(d => d.Joins(room.Order) && d.Other(room.Order) == next);
        }

        /// <summary>
        /// Where the enemies of a group at point <paramref name="p"/> stand (work order 012): one on the point; a pack of
        /// swarmers evenly round a circle of <see cref="LevelValues.PackRadius"/>, the first to the north; a line side by
        /// side, <see cref="LevelValues.LineSpacing"/> apart, along the wall of the door she comes in by (so it faces her
        /// from the far side), centred on the point.
        /// </summary>
        public static LevelPoint[] MemberPlaces(LevelLayout level, LevelRoom room, LevelPoint p, EnemyGroup g, LevelValues v)
        {
            int n = Math.Max(1, g.Count);
            var result = new LevelPoint[n];
            if (n == 1)
            {
                result[0] = p;
                return result;
            }
            if (g.IsPack)
            {
                for (int i = 0; i < n; i++)
                {
                    double a = i * Math.PI * 2.0 / n;
                    result[i] = new LevelPoint(p.X + Math.Sin(a) * v.PackRadius, p.Y + Math.Cos(a) * v.PackRadius);
                }
                return result;
            }
            LevelDoor entry = EntryDoor(level, room);
            bool alongY = entry != null && entry.Vertical;
            for (int i = 0; i < n; i++)
            {
                double off = (i - (n - 1) * 0.5) * v.LineSpacing;
                result[i] = alongY ? new LevelPoint(p.X, p.Y + off) : new LevelPoint(p.X + off, p.Y);
            }
            return result;
        }

        /// <summary>
        /// The room a group needs beyond what every enemy keeps (work order 017): <see cref="LevelValues.EliteRoomToStand"/>
        /// for an elite's larger body, else 0.
        /// </summary>
        public static double ExtraRoom(EnemyGroup g, LevelValues v) => g != null && g.Elite ? Math.Max(0f, v.EliteRoomToStand) : 0d;

        /// <summary>
        /// The first rule a place for one enemy breaks, in words, or null: inside its room, at least
        /// <see cref="LevelValues.WallClearance"/> (and <paramref name="extra"/> more: an elite's, work order 017) from its
        /// walls and from every door of the room, and at least <see cref="LevelValues.EnemyFromEntry"/> from the door she
        /// comes in by.
        /// </summary>
        public static string EnemyPlaceFault(LevelLayout level, LevelRoom room, LevelPoint p, LevelDoor entry, List<LevelDoor> doors, LevelValues v,
                                             double extra = 0d)
        {
            double cs = level.CellSize;
            const double slack = 1e-9;
            double clear = v.WallClearance + extra;
            if (!(p.X >= room.Rect.X * cs && p.X <= room.Rect.Right * cs && p.Y >= room.Rect.Y * cs && p.Y <= room.Rect.Top * cs)) return "is outside the room";
            if (DistanceToWalls(p, room.Rect, cs) < clear - slack) return "is nearer a wall than " + clear + " m";
            string carved = RoomShapes.PlaceFault(room, p, cs, clear); // work order 051: off the solid cells, measured to the outline
            if (carved != null) return carved;
            foreach (LevelDoor d in doors)
                if (DistanceToDoor(p, d, cs) < clear - slack) return "is nearer a door than " + clear + " m";
            if (entry != null && DistanceToDoor(p, entry, cs) < v.EnemyFromEntry - slack) return "is nearer the door she enters by than " + v.EnemyFromEntry + " m";
            return null;
        }

        /// <summary>Metres from a point to the nearest point of a door's opening.</summary>
        public static double DistanceToDoor(LevelPoint p, LevelDoor door, double cellSize)
        {
            door.Opening(cellSize, out LevelPoint a, out LevelPoint b);
            return DistanceToSegment(p, a, b);
        }

        public static double DistanceToSegment(LevelPoint p, LevelPoint a, LevelPoint b)
        {
            double dx = b.X - a.X, dy = b.Y - a.Y;
            double len2 = dx * dx + dy * dy;
            double t = len2 > 0 ? ((p.X - a.X) * dx + (p.Y - a.Y) * dy) / len2 : 0;
            t = Math.Max(0, Math.Min(1, t));
            return p.DistanceTo(new LevelPoint(a.X + t * dx, a.Y + t * dy));
        }

        /// <summary>Metres from a point inside a room to the nearest of its four walls.</summary>
        public static double DistanceToWalls(LevelPoint p, CellRect r, double cellSize) =>
            Math.Min(Math.Min(p.X - r.X * cellSize, r.Right * cellSize - p.X), Math.Min(p.Y - r.Y * cellSize, r.Top * cellSize - p.Y));
    }
}
