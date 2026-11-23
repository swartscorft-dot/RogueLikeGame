using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>The smallest and largest side of a kind of room, in cells. Each side is drawn on its own.</summary>
    [Serializable]
    public sealed class RoomSizeRange
    {
        public int Min;
        public int Max;

        public RoomSizeRange() { }

        public RoomSizeRange(int min, int max)
        {
            Min = min;
            Max = max;
        }

        public bool Allows(int side) => side >= Min && side <= Max;
    }

    /// <summary>
    /// The plan of a floor (decision 0008, 2026-11-19; work order 012): how many rooms of each kind and how big. Data,
    /// not code, so other plans can be tried. The main path is the entrance, <see cref="CombatRooms"/> combat rooms and
    /// the exit room, one after another; the treasure rooms are short dead ends, each off a different combat room (with
    /// <see cref="LevelValues.InALine"/> off; on, work order 041, they are rooms of the row, <see cref="LineBetween"/>).
    /// Until work order 012: five combat rooms, four reward rooms and a boss room.
    /// Work order 017 (decision 0008 as amended 2026-11-20, Kristian: "one hard room in the middle of every floor"): one of
    /// the combat rooms is the elite room, <see cref="EliteRoomNumber"/>.
    /// </summary>
    [Serializable]
    public sealed class LevelPlan
    {
        public int CombatRooms = 4;
        public int TreasureRooms = 2;
        /// <summary>
        /// Ordinary combat rooms a floor in a line has besides <see cref="CombatRooms"/> (work order 046, Kristian's third
        /// session of 2026-11-20: "Etasjene er litt korte", the floors are a little short): 1, so five combat rooms, the
        /// elite room among them (<see cref="LineBetween"/>: C C T L C T C). Each one's enemies are drawn on a stream of their
        /// own, so the four of version 10 keep theirs (<see cref="LevelGenerator"/>, version 11). 0: version 10's four. With
        /// <see cref="LevelValues.InALine"/> off it is not read.
        /// </summary>
        public int MoreCombatRooms = 1;

        /// <summary>The combat rooms of a floor in a line: <see cref="CombatRooms"/> and <see cref="MoreCombatRooms"/> (work order 046).</summary>
        public int LineCombatRooms => Math.Max(0, CombatRooms) + Math.Max(0, MoreCombatRooms);

        public RoomSizeRange EntranceSize = new RoomSizeRange(4, 4);
        public RoomSizeRange CombatSize = new RoomSizeRange(5, 7);
        public RoomSizeRange TreasureSize = new RoomSizeRange(3, 3);
        public RoomSizeRange ExitSize = new RoomSizeRange(8, 8);   // the old boss room's
        public bool EliteRoom = true;           // work order 017: off, no floor has an elite room

        /// <summary>
        /// Which combat room of the chain is the elite room, counted from 1 (work order 017): the middle one, and of the two
        /// middle ones the later, so number <paramref name="combatRooms"/> / 2 + 1 (rounded down): with 1 combat room the
        /// first, 2 the second, 3 the second, 4 the third, 5 the third, 6 the fourth. 0: none (no combat room, or off).
        /// </summary>
        public int EliteRoomNumber(int combatRooms) => !EliteRoom || combatRooms <= 0 ? 0 : combatRooms / 2 + 1;

        /// <summary>
        /// The rooms between the entrance and the exit of a floor in a line (work order 041, Kristian's second session of
        /// 2026-11-20: "I need the run to be more linear", and asked how, every room of a floor in one row): the combat
        /// rooms in order with the treasure rooms between them. A treasure room stands in a gap between two combat rooms,
        /// one to a gap, so it is never first after the entrance, never last before the exit and never next to another.
        /// The gaps are taken in this order, counted from the middle combat room m (the elite room's,
        /// <see cref="EliteRoomNumber"/>, whether the floor has one or not), gap k being the one after combat room k: the
        /// one just before it (m - 1), the one after the combat room past it (m + 1), the one just after it (m), then
        /// outward, m - 2, m + 2, m - 3 and so on, each only while it is a gap. Four combat rooms (the plan's): C C T L T C;
        /// five: C C T L C T C (the order's first guess); three: C T L T C; six: C C C T L C T C. With fewer gaps than
        /// treasure rooms (fewer combat rooms than treasure rooms and one, no plan of this game) the rest stand after the
        /// last combat room.
        /// </summary>
        public List<RoomKind> LineBetween() => LineBetween(LineCombatRooms);

        /// <summary>The row as <see cref="LineBetween()"/> says, for <paramref name="combatRooms"/> combat rooms.</summary>
        public List<RoomKind> LineBetween(int combatRooms)
        {
            int n = Math.Max(0, combatRooms), treasures = Math.Max(0, TreasureRooms);
            int middle = n / 2 + 1;
            var gaps = new List<int>();
            void Take(int gap)
            {
                if (gap >= 1 && gap <= n - 1 && !gaps.Contains(gap)) gaps.Add(gap);
            }
            Take(middle - 1);
            Take(middle + 1);
            Take(middle);
            for (int d = 2; d <= n; d++)
            {
                Take(middle - d);
                Take(middle + d);
            }
            var after = new int[n + 1]; // treasure rooms after combat room k (0: before the first)
            for (int i = 0; i < treasures; i++)
            {
                if (i < gaps.Count) after[gaps[i]]++;
                else after[n]++;
            }
            var line = new List<RoomKind>();
            for (int k = 1; k <= n; k++)
            {
                line.Add(RoomKind.Combat);
                for (int i = 0; i < after[k]; i++) line.Add(RoomKind.Treasure);
            }
            if (n == 0)
                for (int i = 0; i < treasures; i++) line.Add(RoomKind.Treasure);
            return line;
        }

        public RoomSizeRange SizeOf(RoomKind kind) =>
            kind == RoomKind.Entrance ? EntranceSize
            : kind == RoomKind.Combat ? CombatSize
            : kind == RoomKind.Treasure ? TreasureSize
            : ExitSize;
    }

    /// <summary>
    /// One mix of enemies a room may hold (decision 0008, work order 012): the shares of the room's enemies, guards
    /// apart, that are swarmers, shooters and brutes, and how many guards stand with them. A kind with a share above 0
    /// gets at least one; the swarmers take what rounding leaves over. Work order 027: how many casters a combat room of
    /// the mix holds (twice as many in an exit room, never more than the floor allows,
    /// <see cref="LevelValues.CastersMost"/>); each takes the place of a shooter or a brute, so the room's count is the same.
    /// </summary>
    [Serializable]
    public sealed class EnemyMix
    {
        public string Name = "";
        public float Swarmers;
        public float Shooters;
        public float Brutes;
        public int Guards;
        public int Casters;

        public EnemyMix() { }

        public EnemyMix(string name, float swarmers, float shooters, float brutes, int guards, int casters = 0)
        {
            Name = name;
            Swarmers = swarmers;
            Shooters = shooters;
            Brutes = brutes;
            Guards = guards;
            Casters = casters;
        }
    }

    /// <summary>
    /// The enemies of the rooms of one floor number (work order 012): how many in a combat room (drawn from
    /// <see cref="Fewest"/> to <see cref="Most"/>; the exit room <see cref="LevelValues.ExitRoomShare"/> as many), and
    /// the mixes a room's are drawn from.
    /// </summary>
    [Serializable]
    public sealed class FloorEnemies
    {
        public int Fewest;
        public int Most;
        public EnemyMix[] Mixes = Array.Empty<EnemyMix>();

        public FloorEnemies() { }

        public FloorEnemies(int fewest, int most, params EnemyMix[] mixes)
        {
            Fewest = fewest;
            Most = most;
            Mixes = mixes;
        }
    }

    /// <summary>
    /// The numbers of the level generator and of the grey-box level built from it. All first guesses, kept together
    /// here with their defaults in code so they can be tuned later. Lengths are in metres unless they say cells.
    /// Changing a number that the generator reads changes the levels old seeds give.
    /// </summary>
    [Serializable]
    public sealed class LevelValues
    {
        // The grid
        public float CellSize = 4f;            // m on a side of one cell
        public int GridCells = 48;             // cells on a side of the square the level must fit in

        // Layout
        public int DoorWidth = 1;              // cells
        public int MostInARow = 2;             // main-path steps in the same direction, at most, before it must turn
        public int Attempts = 64;              // whole attempts before giving up (never reached: see the tests)
        public int PlacementsPerAttempt = 400; // rooms put down, counting those taken back, before an attempt gives up

        // What is in the rooms
        public float WallClearance = 1.5f;     // m from a wall or a door's opening to any start or enemy point
        public float EnemyFromEntry = 8f;      // m from the door she enters a combat room by to any of its enemies
        public float EnemySpacing = 4f;        // m between two enemy points of a room
        public float PointStep = 1f;           // m between the places considered for an enemy

        // The grey-box level
        public float WallHeight = 3f;          // m
        public float NearWallHeight = 1f;      // m, a wall between the camera and the room's floor
        public float WallThickness = 0.5f;     // m
        public float FloorThickness = 0.2f;    // m, under the ground
        public float OffsetX = 500f;           // m, where the start room's middle is put, far to one side of the practice room
        public float OffsetZ = 0f;

        // Enemies' health in a generated floor (work order 012: a room is 20 to 40 s of fighting for a kit that deals
        // about 250 damage a second). They override the kinds' own, which the practice room keeps.
        public int SwarmerHealth = 150;
        public int ShooterHealth = 400;
        public int EnemyHealth = 1200;         // a brute's (3000 until work order 012)
        public int GuardHealth = 3500;
        public int BossHealth = 18000;         // the act boss (work order 014: the crypt knight; 9000 when it was a guard)
        public int CasterHealth = 500;         // work order 027: weak, the first to reach or silence

        // The elite (work order 017, 2026-11-20): one in each elite room, besides what a combat room holds.
        public float EliteHealthScale = 4f;    // times its kind's health for the floor, for a kind with no share of its own below
        // The elite by its kind (work order 030, lever C): four times for every kind made an elite guard's room about four
        // times a combat room and an elite shooter's barely different (the test pilot's flights of 2026-11-20); these aim
        // at an elite room about twice a combat room whichever kind it holds. 4 each before. Flown (work order 030): at 4
        // an elite guard's room was 1.8 to 4.7 times the floor's combat rooms and killed a pilot in it four times in three
        // rounds, an elite shooter's mostly 1.1 to 1.5; after it 1.2 to 3.3 (no death there) and 1.0 to 2.5, the floor-1 elite
        // brute's 1.4 to 2.7.
        public float EliteBruteHealthScale = 3f;
        public float EliteShooterHealthScale = 5f;
        public float EliteGuardHealthScale = 2f;
        public float EliteDamageScale = 1.25f; // its attacks' damage, times this
        public float EliteSizeScale = 4f / 3f; // shown this many times as large, and its body as wide
        public float EliteStaggerShare = 0.5f; // a kind with no stagger bar gets one this share of the guard's
        public float EliteRoomToStand = 1f;    // m more than any enemy keeps from the walls, the doors and the other groups: its larger body
        public int EliteKindsFromFloor = 2;    // from this floor a brute, a shooter or a guard; before it a brute
        public int EliteCasterFromFloor = 3;   // work order 027: from this floor the elite may be a caster too (087: read with ActPlan off; on, CastersFromFloor)
        public float PackRadius = 1.2f;        // m from a pack's point to each of its swarmers
        public float LineSpacing = 2f;         // m between two enemies of a line, side by side (work order 012)
        public float MemberSpacing = 1.5f;     // m between two enemies of different groups (work order 012)

        // Groups (work order 012): enemies stand in groups, a point each.
        public int PackFewest = 3;             // swarmers in a pack, when there are this many to share out
        public int PackMost = 5;
        public int ShooterLineMost = 3;        // shooters in a line
        public int BruteLineMost = 2;          // brutes side by side
        public float FarBand = 0.6f;           // shooters stand at least this share of the room's farthest place from the door in
        public float MiddleBandFrom = 0.3f;    // brutes and guards between these shares
        public float MiddleBandTo = 0.85f;

        // The act (decision 0008, 2026-11-19, the producer's proposal: an act is five floors; floors 1 to 4 end with a new
        // skill, floor 5 with the act boss).
        public int FloorsPerAct = 5;
        public float ExitRoomShare = 1.5f;     // the exit room holds this many times a combat room's enemies
        public int GuardsMostInRoom = 1;       // from floor 2: one guard a combat room at most
        public int GuardsMostInExit = 2;       // two in an exit room, the act boss counted
        public int GuardsFromFloor = 2;
        // Work order 023 (2026-11-20, the test pilot's flights): the brute's Shockwave and the shooter's shot killed the
        // pilots on floors 3 to 5, in rooms of up to four of each. The room keeps its number of enemies (Kristian asked
        // for crowds); past these, the brutes and shooters drawn are swarmers instead. 0: no cap, as before.
        public int BrutesMostInRoom = 3;       // brutes in a combat room at most (no cap before)
        public int ShootersMostInRoom = 3;     // shooters in a combat room at most (no cap before)
        public int ExitRoomKindMore = 1;       // an exit room may hold one more of each
        // The casters in the floors (Kristian, 2026-11-21, asked whether they should be on or off in the floors: "Castere på
        // ja"; work order 054): on, as work order 027 built them, below: from floor 2 a caster may take the place of a
        // shooter or a brute in a combat or exit room, and from EliteCasterFromFloor an elite may be one; floor 1 holds none.
        // They came in switched off (work order 034, 2026-11-20), as the balance passes (work orders 023 and 030) had been
        // flown without them. Off: no room holds a caster and no elite is one, and every seed's floor is version 13's, field
        // for field, but for the version. The practice room's caster does not depend on it.
        public bool Casters = true;
        // The casters (work order 027): from floor 2, one a combat room at most on floors 2 and 3, two on floors 4 and 5,
        // and an exit room one more; they stand at the far side as the shooters do, never near each other. Since work order
        // 087 read only with ActPlan off (version 17's floors); on, PlanCastersMostInRoom from CastersFromFloor.
        public int[] CastersMostInRoom = { 0, 1, 1, 2, 2 }; // by floor number, from 1; past the end its last
        public int CastersMoreInExit = 1;
        public float CasterSpacing = 8f;       // m between two casters of a room
        // The bombers in the floors (work order 073, 2026-11-21): built OFF, as the casters were before work order 054. Work
        // order 087 (Kristian, 2026-11-22, asked which of the two new kinds should go into the floors: both, the bomber from
        // floor 4 with the shooter's fan): ON, from BomberFloors.FromFloor (4), by 073's own rule (never in the floor's first
        // combat room, each in place of a swarmer: LevelBombers), read only with ActPlan on. Off (or ActPlan off): no room
        // holds a bomber. The practice room's bomber does not depend on it.
        public bool Bombers = true;
        public int BomberHealth = 100;         // work order 073: a floor's bomber, as the practice room's (BomberValues)
        public BomberFloorValues BomberFloors = new BomberFloorValues();
        // The warders in the floors (work order 079, 2026-11-22): built OFF, as the bombers were. Work order 087 (Kristian,
        // 2026-11-22: both, the warder from floor 3, at most one a room): ON, from WarderFloors.FromFloor (3), by 079's own rule
        // (at most one a room, never in the floor's first combat room, only in a room with at least three other enemies, each
        // in place of a swarmer: LevelWarders), read only with ActPlan on. Off (or ActPlan off): no room holds a warder. The
        // practice room's warder does not depend on it.
        public bool Warders = true;
        public int WarderHealth = 220;         // work order 079: a floor's warder, as the practice room's (WarderValues)
        public WarderFloorValues WarderFloors = new WarderFloorValues();
        // The act's plan (work order 087; Kristian, 2026-11-22, asked whether each floor should bring one new thing, which makes
        // floor 2 easier and floor 3 harder: "Ja, én ny ting per etasje"; and both new kinds in): floor 1 the swarmer, the brute
        // and the shooter; floor 2 the guard (GuardsFromFloor); floor 3 the caster (CastersFromFloor) and the warder
        // (WarderFloors.FromFloor); floor 4 the bomber (BomberFloors.FromFloor) and the shooter's fan (ShooterValues.FanFromFloor);
        // floor 5 the act boss. On (the generator's version 18): the casters by CastersFromFloor and PlanCastersMostInRoom, the
        // bombers and the warders by their switches above. Off: every floor is version 17's, field for field, but for the version
        // (the casters by CastersMostInRoom and EliteCasterFromFloor, no bomber, no warder, whatever their switches say).
        public bool ActPlan = true;
        // With ActPlan on: no caster before this floor, in a room or as the elite (work order 087: floor 3, where the plan puts
        // it; the casters came from floor 2 before, and an elite one from floor 3). The one number to move the caster's floor by.
        public int CastersFromFloor = 3;
        // With ActPlan on, from CastersFromFloor: the most casters in a combat room by floor number, from 1 (past the end its
        // last), and an exit room CastersMoreInExit more (work order 087: one a room on floors 3 and 4, two on floor 5; with
        // CastersFromFloor 3 floors 1 and 2 are not read, and hold one each if the caster's floor is moved down to them).
        public int[] PlanCastersMostInRoom = { 1, 1, 1, 1, 2 };

        /// <summary>The bombers are put into the floors (work order 087): their switch, and the act's plan on.</summary>
        public bool BombersInFloors => ActPlan && Bombers;

        /// <summary>The warders are put into the floors (work order 087): their switch, and the act's plan on.</summary>
        public bool WardersInFloors => ActPlan && Warders;
        public FloorEnemies[] Floors = DefaultFloors();

        // Props (work order 009)
        public bool Props = true;              // off: levels are generated and built without props
        // Room themes (work order 028, Kristian 2026-11-19 and 2026-11-20: "good variety", and the same pieces everywhere
        // make the floors alike): off, every room is dressed as before work order 028, from the eleven old props, and every
        // seed's props are what they were; on, each room has a theme (ThemeRules). Layouts and enemies are the same
        // either way.
        public bool Themes = true;

        // The crypt wall kit (work order 016, 2026-11-20): how a floor looks, never what it is
        public bool CryptKit = true;           // off: the grey boxes as before, for comparing; the generator never reads it

        // A floor in a line (work order 041; Kristian's second session of 2026-11-20: "It is not quite intuitive where the
        // chest rooms are; I end up double-checking several rooms for them. I need the run to be more linear", and asked
        // how linear, fully): on, every room of a floor is in one row, the treasure rooms between the combat rooms
        // (LevelPlan.LineBetween), and each room holds the enemies the same seed's floor of version 9 holds there (the
        // generator's summary). Off: every seed's floor is version 9's, field for field.
        public bool InALine = true;
        // The doors that say what is behind them (work order 041, his answer: marked doors, and what is done shown): a sign
        // over the doorway on to a treasure room, the elite room or the exit room, and its light (DoorSignRules). Off, none.
        // The generator never reads it.
        public bool DoorSigns = true;
        // The small map in the corner (work order 041, his answer: a small map, and opened chests and cleared rooms shown):
        // FloorMapRules. Off, no map. The generator never reads it.
        public bool FloorMap = true;
        public DoorSignValues Signs = new DoorSignValues(); // the signs' and the map's numbers (work order 041)

        // Rooms with a mood (work order 044; Kristian, 2026-11-20: "The rooms must also feel more alive and create atmosphere
        // like Diablo and Path of Exile", and of the five concept pictures, "build toward them"): light and darkness, air, a
        // worn floor, one eye-catcher per room theme, a quiet room tone (RoomMoodPlan, and RoomMood in the game), each layer
        // with its own switch in Mood. Off: every floor is today's, object for object. The generator reads it in one place
        // only: with it and Mood.Compose on, an ossuary's skull and bone piles are banked at its far wall (the props stage,
        // from the props' own draws, so which props a room holds stays the same), where they stood in two clusters before.
        public bool RoomMood = true;
        public MoodValues Mood = new MoodValues();
        // The eight large props in the rooms' compositions (work order 052; the concept pictures, Kristian's choice of
        // 2026-11-20, "build toward them"): the knight in the tomb hall, the great altar in the shrine, the throne and the
        // vault's chest on the dais model, great braziers by the throne, coin heaps, a bone wall in the ossuary, banners on
        // free walls, candle clusters where the compositions had code-made candles (RoomMoodPlan). With RoomMood and
        // Mood.Compose; read when a floor is built. Off: every floor is as work order 044 left it. The generator never reads it.
        public bool LargeProps = true;

        // Rooms that are not boxes (work order 051; Kristian, 2026-11-20: "Now we have very clear squares per room. A room does
        // not have to be a square ... not walking into large office landscapes with monsters in them"; the research report d's
        // proposal A): on, each room's rectangle is carved (cut corners, a cross, an apse, alcoves, pillar aisles) and has a
        // gate inside its door in, on the generator's own stream "shapes" after the layout (RoomShapes), so a seed keeps its
        // rooms' places, sizes, order and doors. Off: every floor is version 12's (044 and 046 merged), field for field, but
        // for the version.
        public bool Shapes = true;
        public ShapeValues ShapeRules = new ShapeValues();
        // Rooms drawn by hand (work order 058; Kristian, 2026-11-20: "we must take more inspiration from Diablo and Path of
        // Exile 2 in how their generators make and design levels. We need a bit of a dungeon-crawler feeling", and on
        // 2026-11-21 he chose "Rom-maler slik Diablo og PoE gjør"; the research report d's proposal B): on, the elite room, the
        // exit room and about three in four of the other combat rooms take a template of the library (RoomTemplateLibrary),
        // which gives the room its size, its outline and where its doors may be, and is joined to the room before it at their
        // door marks (RoomTemplates, on the generator's own stream "templates"). Read only with Shapes and InALine on. Off:
        // every floor is version 14's, field for field, but for the version.
        public bool Templates = true;
        public TemplateValues TemplateRules = new TemplateValues();
        // Passages between the rooms (work order 059; Kristian, 2026-11-20: "bedre bruk av mappet ... litt dungen crawler følelse
        // trenger vi, ikke inn i store kontorlandskap med monstre i", and on 2026-11-21 he chose "Ganger mellom rommene"; the
        // research report d's proposal C): on, the rooms of the row stand apart, and a passage one cell wide joins each to the
        // next (Passages, PassageRules; the generator's own stream "passages"). Read only on a floor in a line (InALine) with
        // DoorWidth 1. Off: every floor is version 15's, field for field, but for the version.
        public bool Passages = true;
        public PassageValues PassageRules = new PassageValues();
        // The room is a cave (work order 076; Kristian, 2026-11-21 in the evening: "jeg liker heller ikke at vi ser andre soner
        // når vi er i et rom. veggene må gå helt opp og føles at vi er i en hule", and of the treasure room's gold, "litt
        // unødvendig"): on, only the room or passage she is in and those a door from it are drawn, against the earth's black;
        // rock rises over the full walls as far as it hides nothing today's walls leave seen; the vault's large coin heaps are
        // gone (CaveValues, CaveSight, CaveRise; CaveView in the game). Off: every floor looks as before, object for object.
        // The generator never reads it.
        public bool Cave = true;
        public CaveValues CaveRules = new CaveValues();
        // More rooms drawn by hand (work order 072; Kristian, the evening of 2026-11-21, asked what more to build, ticked "Flere
        // rom-maler": eleven repeat quickly over five floors of eight combat rooms): on, the library's second set of templates
        // (RoomTemplateLibrary, the ones Since 17: rings, islands, halls, alcoves, arenas and more, and larger elite and exit
        // rooms) is drawn beside the first eleven, by the same weights and rules. Read only with Templates on. Off: every floor
        // is version 16's, field for field, but for the version. OFF by default (the producer, 2026-11-22: a room's form is
        // judged by Kristian playing it in the greybox room lab, work order 084, before it goes into the floors; the second set
        // has not been played, and the first PlayMode runs found rooms of it with floor cut off by their props).
        public bool MoreTemplates = false;
        // Exit rooms of their own (work order 089; Kristian, 2026-11-22, asked whether the throne room should be the exit room only on
        // floor 5 and floors 1 to 4 get an exit room of their own: "Ja, tronen bare på etasje 5"): on, the exit room of floors 1 to 4
        // draws its template from the library's exit rooms of their own only (RoomTemplateLibrary's third set, ExitSince: the gate
        // hall, the stair head, the sealed door; no throne in them) and is an exit hall (RoomTheme.ExitHall), and the exit room of
        // floor 5 (FloorsPerAct) from the throne templates only, a throne room as before. Read by the templates' draw (with Templates
        // on) and the themes. Off: every floor is version 19's, field for field, with no new version (the library grew after its
        // other sets, and off they are never drawn). OFF by default: a room's form is judged by Kristian playing it in the room lab
        // (work order 084, where they are listed whatever this says) before a later order turns it on with a new version.
        public bool ExitRoomsOfTheirOwn = false;
        // A room is a fight (work order 070; Kristian after his evening session of 2026-11-21: "nå rman kommer i et rom, så er
        // man i en combat ... man trenger et punkt in i rommet der alle flyr på"): on, every room of a floor in a line that holds
        // enemies has a point a little way in from the door she came by (Fights, RoomFightRules); crossing it, or any waking of
        // one of its enemies before it, wakes every enemy of the room at once, and the room stays one fight until it is cleared
        // or she has got away from all of them. Here, beside the other switches of how a floor's rooms are built and played,
        // because it is a rule of a floor's rooms (it is measured on a room's cells and doors, and the practice room, which has
        // none, is not touched) and is read where a floor is built; RunValues holds what a run gives and takes. The generator
        // never reads it. Off: every enemy wakes on its own, by its range, a hurt or a pack, exactly as before.
        public bool RoomFights = true;
        public RoomFightValues Fights = new RoomFightValues();
        // Rooms you can use (work order 088; Kristian, 2026-11-22, of the door that closes behind her: "Døra stenges bak deg, men vi
        // bør balansere rommene med flere muligheter og, slik at man kan bruke miljøet til sin fordel", and of the duck: "Plasser
        // dekning for den"): on (the generator's version 19), the props never cut a room's floor into pockets and leave a way of
        // whole cells through it (JoinedFloor), every room with a fight that holds shooters or casters has a chain of tall cover from
        // where she stands when it wakes toward where they stand, and every room that holds shooters low props on their lines to
        // duck behind (FightCover; the numbers in CoverRules). Off: every floor is version 18's, field for field, but for the version.
        public bool UsableRooms = true;
        public CoverValues CoverRules = new CoverValues();

        public LevelPlan Plan = new LevelPlan();
        public PropValues PropRules = new PropValues();
        public ThemeValues ThemeRules = new ThemeValues();
        public CryptKitValues Crypt = new CryptKitValues();

        /// <summary>
        /// The enemies by floor number (work order 012, first guesses): mostly swarmers with a few brutes and a shooter on
        /// floor 1; each floor after it more, with more shooters and brutes; guards from floor 2. Work order 014
        /// (2026-11-20, Kristian: three to four times the first version's one to three): floor 1 eight to ten in a combat
        /// room, floor 5 twelve to sixteen, the floors between in even steps (six to eight and ten to fourteen before).
        /// Work order 027: casters from floor 2, in one mix of three there, two of three on floor 3, and two in a room of
        /// the crossfire on floors 4 and 5; floor 1 is as it was.
        /// </summary>
        public static FloorEnemies[] DefaultFloors() => new[]
        {
            new FloorEnemies(8, 10,
                new EnemyMix("swarm", 0.7f, 0.15f, 0.15f, 0),
                new EnemyMix("swarm and brutes", 0.6f, 0.15f, 0.25f, 0),
                new EnemyMix("swarm and a shooter", 0.65f, 0.2f, 0.15f, 0)),
            new FloorEnemies(9, 12,
                new EnemyMix("swarm", 0.55f, 0.2f, 0.25f, 0),
                new EnemyMix("guarded swarm", 0.55f, 0.2f, 0.25f, 1),
                new EnemyMix("crossfire", 0.45f, 0.35f, 0.2f, 0, casters: 1)),
            new FloorEnemies(10, 13,
                new EnemyMix("guarded", 0.45f, 0.25f, 0.3f, 1),
                new EnemyMix("crossfire", 0.4f, 0.35f, 0.25f, 0, casters: 1),
                new EnemyMix("heavy", 0.35f, 0.3f, 0.35f, 1, casters: 1)),
            new FloorEnemies(11, 15,
                new EnemyMix("guarded", 0.4f, 0.3f, 0.3f, 1),
                new EnemyMix("heavy", 0.3f, 0.35f, 0.35f, 1, casters: 1),
                new EnemyMix("crossfire", 0.45f, 0.35f, 0.2f, 0, casters: 2)),
            new FloorEnemies(12, 16,
                new EnemyMix("guarded", 0.35f, 0.35f, 0.3f, 1, casters: 1),
                new EnemyMix("heavy", 0.3f, 0.35f, 0.35f, 1, casters: 1),
                new EnemyMix("crossfire", 0.4f, 0.3f, 0.3f, 0, casters: 2)),
        };

        /// <summary>The enemies of this floor number; past the table's end its last row, before it its first.</summary>
        public FloorEnemies EnemiesOf(int floor)
        {
            if (Floors == null || Floors.Length == 0) return new FloorEnemies(1, 1, new EnemyMix("brute", 0f, 0f, 1f, 0));
            return Floors[Math.Max(0, Math.Min(Floors.Length - 1, floor - 1))];
        }

        // The floors' brutes' Shockwave (work order 023, 2026-11-20): the test pilot's flights found it the largest
        // source of her damage, used when she is 3 to 11 m away (a normal kiting distance) down a line 12 m long. Its
        // damage and timing stay. The enemy settings asset, and the practice brute, keep their own numbers.
        public float ShockwaveFarthest = 8f;   // m: a floor's brute uses it when she is at most this far (11 before)
        public float ShockwaveLength = 9f;     // m: its line (12 before)

        /// <summary>
        /// A generated floor's brute's numbers (work order 023): a copy of the asset's, its attacks copied, with the
        /// Shockwave's reach of the floors. The asset's own are not changed.
        /// </summary>
        public EnemyValues FloorBruteValues(EnemyValues asset)
        {
            if (asset == null) asset = new EnemyValues();
            var copy = new EnemyValues
            {
                Health = asset.Health, MoveSpeed = asset.MoveSpeed, AggroRange = asset.AggroRange, LeashRange = asset.LeashRange,
                RespawnDelay = asset.RespawnDelay, UseModels = asset.UseModels,
                Attacks = asset.Attacks == null ? null : Array.ConvertAll(asset.Attacks, a => a?.Copy()),
            };
            if (copy.Attacks != null)
                foreach (EnemyAttackDefinition a in copy.Attacks)
                {
                    if (a == null || a.Name != "Shockwave") continue;
                    a.MaxDistance = ShockwaveFarthest;
                    a.Length = ShockwaveLength;
                }
            return copy;
        }

        /// <summary>The fewest and most enemies a room of this kind holds on this floor; 0 for the entrance and treasure rooms.</summary>
        public void EnemyRange(RoomKind kind, int floor, out int fewest, out int most)
        {
            fewest = most = 0;
            FloorEnemies f = EnemiesOf(floor);
            if (kind == RoomKind.Combat)
            {
                fewest = f.Fewest;
                most = f.Most;
            }
            else if (kind == RoomKind.Exit)
            {
                fewest = (int)Math.Ceiling(f.Fewest * ExitRoomShare - 1e-6);
                most = (int)Math.Floor(f.Most * ExitRoomShare + 1e-6);
            }
        }

        /// <summary>
        /// The kinds an elite may be on this floor (work order 017): a brute on floor 1; a brute, a shooter or a guard after;
        /// from <see cref="EliteCasterFromFloor"/> a caster too (work order 027), while <see cref="Casters"/> is on (work
        /// order 034: off, the three, so the elite's draw is what it was). With <see cref="ActPlan"/> on (work order 087) the
        /// caster's floor is <see cref="CastersFromFloor"/>, the rooms' and the elite's alike (3, as the elite's was).
        /// </summary>
        public EnemyKind[] EliteKinds(int floor) =>
            floor < EliteKindsFromFloor ? new[] { EnemyKind.Brute }
            : floor < (ActPlan ? CastersFromFloor : EliteCasterFromFloor) || !Casters ? new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard }
            : new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster };

        /// <summary>
        /// The most casters a room of this kind may hold on this floor (work order 027): <see cref="CastersMostInRoom"/> for
        /// the floor in a combat room, <see cref="CastersMoreInExit"/> more in the exit room (none more on a floor with none);
        /// none elsewhere. The elite apart. None anywhere while <see cref="Casters"/> is off (work order 034). With
        /// <see cref="ActPlan"/> on (work order 087): none before <see cref="CastersFromFloor"/>, and from it
        /// <see cref="PlanCastersMostInRoom"/> in place of <see cref="CastersMostInRoom"/>.
        /// </summary>
        public int CastersMost(RoomKind kind, int floor)
        {
            if (!Casters) return 0;
            if (ActPlan && floor < CastersFromFloor) return 0;
            int[] table = ActPlan ? PlanCastersMostInRoom : CastersMostInRoom;
            if (table == null || table.Length == 0 || (kind != RoomKind.Combat && kind != RoomKind.Exit)) return 0;
            int room = Math.Max(0, table[Math.Max(0, Math.Min(table.Length - 1, floor - 1))]);
            return kind == RoomKind.Exit && room > 0 ? room + CastersMoreInExit : room;
        }

        /// <summary>An elite's health on a generated floor (work order 017): its kind's, times <see cref="EliteHealthScale"/>.</summary>
        public int EliteHealthOf(EnemyKind kind) => (int)Math.Round(HealthOf(kind) * (double)EliteHealthScaleOf(kind), MidpointRounding.AwayFromZero);

        /// <summary>
        /// Times its kind's health an elite of this kind has (work order 030, lever C); a kind with no share of its own, the
        /// caster (work order 034: four times, the fallback <see cref="EliteHealthScale"/>), the fallback.
        /// </summary>
        public float EliteHealthScaleOf(EnemyKind kind) =>
            kind == EnemyKind.Brute ? EliteBruteHealthScale
            : kind == EnemyKind.Shooter ? EliteShooterHealthScale
            : kind == EnemyKind.Guard ? EliteGuardHealthScale
            : EliteHealthScale;

        /// <summary>
        /// The danger rises with the floor (work order 030, lever B): what a floor's enemies deal to her, times this, by the
        /// floor's number from 1 (past the end, its last). The act boss is not scaled, and an elite's own scale multiplies
        /// with it. All 1 before: every floor dealt the same, and the pilots died on floors 2 and 3. Flown (work order 030,
        /// round 2, with the room heal): every Ace reached floor 3 (one of three before), the Kiter two of three (one).
        /// Work order 046 (the test pilot's flights of the night of 2026-11-20, with the enemies of work orders 035 and 036
        /// and five combat rooms a floor): 0.6, 0.6, 0.7, 0.8, 0.85 (0.7, 0.8, 0.9, 1, 1 before). With the brute's, the
        /// guard's and the shooter's own numbers eased in three rounds every pilot still died on floor 2 or 3, losing on
        /// floors 2 and 3 about twice what the rooms gave back; Kristian's third session, on floor 2 three times.
        /// </summary>
        public float[] FloorDamageScale = { 0.6f, 0.6f, 0.7f, 0.8f, 0.85f };

        /// <summary>The scale of what a floor's enemies deal (work order 030, lever B); 1 without a table.</summary>
        public float DamageScaleOf(int floor)
        {
            if (FloorDamageScale == null || FloorDamageScale.Length == 0) return 1f;
            return FloorDamageScale[Math.Max(0, Math.Min(FloorDamageScale.Length - 1, floor - 1))];
        }

        /// <summary>A generated floor's health for an enemy of this kind; the act boss's for the boss.</summary>
        public int HealthOf(EnemyKind kind, bool boss = false) =>
            boss || kind == EnemyKind.Boss ? BossHealth
            : kind == EnemyKind.Swarmer ? SwarmerHealth
            : kind == EnemyKind.Shooter ? ShooterHealth
            : kind == EnemyKind.Guard ? GuardHealth
            : kind == EnemyKind.Caster ? CasterHealth
            : kind == EnemyKind.Bomber ? BomberHealth // work order 073
            : kind == EnemyKind.Warder ? WarderHealth // work order 079
            : EnemyHealth;

        /// <summary>The most guards a room of this kind may hold on this floor, the act boss counted.</summary>
        public int GuardsMost(RoomKind kind, int floor) =>
            floor < GuardsFromFloor && !(kind == RoomKind.Exit && floor >= FloorsPerAct) ? 0
            : kind == RoomKind.Exit ? GuardsMostInExit
            : kind == RoomKind.Combat ? GuardsMostInRoom
            : 0;
    }

    /// <summary>
    /// Where a level's props go and the rules every prop keeps (work order 009). Data, so it can be changed; all first
    /// guesses. Lengths are in metres, measured from a prop's footprint (<see cref="PropSize.Crypt"/>).
    /// </summary>
    [Serializable]
    public sealed class PropValues
    {
        // Rules every prop keeps
        public float WallClearance = 0.5f;     // from a wall, for every prop but those that stand at a wall
        public float AtWallGap = 0.1f;         // between a wall and the back of a prop that stands at it
        public float AtWallMost = 0.5f;        // the farthest a prop that stands at a wall may be from it
        public float FromDoor = 3f;            // from a door's opening
        public float LaneWidth = 3f;           // a clear lane this wide from every door of a room to every other, and to its middle
        public float LaneGrid = 0.5f;          // the grid the lane is checked on
        public float FromPoints = 2f;          // from the player's start and from every enemy point
        public float FloorSpacing = 3f;        // from an open-floor prop to any other prop
        public float CoverSpacing = 2f;        // work order 088: from a cover put for the room's fight (LevelProp.Fight) to any other prop: the joined floor keeps the ways between them
        public float BossMiddleClear = 8f;     // round the boss room's middle, nothing
        public float WayDownClear = 1.5f;      // work order 089: in an exit room of its own, no tall prop this near the line from the door in to the way down (the far wall's A): she sees from the door where the room ends; half the lane's 3 m

        // Walls and wall pieces (work order 028)
        public float WallFace = 0.275f;        // a wall's inner face, from its line: half the kit wall's 0.55 m (the grey boxes' face is at 0.25)
        public float HandWidth = 0.1f;         // a wall piece's back is at most this far from the face
        public float WallPieceGap = 0.03f;     // between the face and a wall piece's back, where it is put
        public float SouthWallMostHeight = 1.3f; // a prop taller than this never stands at a room's south wall: the low one the camera looks over
        public float MinWalkable = 2f / 3f;    // a combat room keeps at least this share of its floor walkable
        public float WalkRadius = 0.5f;        // floor is walkable at least this far from a prop's footprint and the walls (about the NavMesh agent's radius)

        // Cover (Kristian, 2026-11-19: shots are stopped by walls and pillars)
        public float CoverHeight = 1.2f;       // a prop at least this tall stops her shots (altar, grave marker, pillars, statue); she shoots over a lower one. Read only with OneHeightTable off
        // Work order 088 (point 5, the draft room rule 13: "low is low and tall is tall"): on, what a prop does against a shot is the
        // one sorted table (PropHeights): the greybox, the duck and the shots all read it, whether the quick duck is on or off. Off:
        // the morning's rule, CoverHeight with the quick duck's sort laid on top (the altar, the grave marker and the broken pillar
        // stop shots with the duck off, and nothing with it on). With the duck on (the default) the two give the same floor.
        public bool OneHeightTable = true;
        public float Glow = 3f;                // work order 028: the props' emission maps in the game's cyan times this, as the kit's stone (CryptKitValues.StoneGlow)

        // Combat rooms
        public int CombatFewest = 4;           // props in the smallest combat room
        public int CombatMost = 8;             // props in the largest
        public int CornerBraziers = 2;         // braziers, each in a corner drawn from the four
        public float WallShare = 0.35f;        // of the rest, the share along the walls; the others in the open floor
        public float PlaceStep = 0.5f;         // between the places tried for a prop
        public int LaneTriesPerProp = 24;      // places a prop may try that pass every other rule, before it is left out
        public PropModel[] FloorModels = { PropModel.Pillar, PropModel.PillarBroken, PropModel.Sarcophagus, PropModel.Rubble, PropModel.Statue };
        public PropModel[] WallModels = { PropModel.GraveMarker, PropModel.Urns, PropModel.BonePile };

        // Set pieces
        public float SetSearch = 3f;           // how far from its wanted place a set piece may move to keep the rules
        public float ChestBack = 1.5f;         // how far past the reward room's middle, seen from its door, the chest stands: the middle stays walkable
        public float BesideGap = 0.6f;         // between the reward room's chest and each brazier beside it
        public float StatueGap = 0.8f;         // between the boss room's altar and each statue beside it
        public float BossPillarInset = 3.5f;   // from both walls of a corner to the middle of the boss room's pillar there

        /// <summary>A copy, its numbers its own (work order 052: the large props ask the same rules with one number changed).</summary>
        public PropValues Copy() => (PropValues)MemberwiseClone();
    }

    /// <summary>
    /// How a prop of a room's theme likes to stand (work order 028). The theme says what and suggests where; every
    /// place is still tried against every rule of <see cref="PropRules"/>, and a prop with no place that keeps them is
    /// left out. <see cref="RoomThemes"/> says what each does.
    /// </summary>
    public enum PropStance
    {
        Row, Cluster, CornerOrWall, AlongWall, OpenFloor, EdgeFloor, Ring,
        FarAltar, BehindAltar, FlankAltar, BeforeAltar, BesideShelf,
        Throne, FlankThrone, CornerPillars, Chest, FlankChest,
    }

    /// <summary>
    /// One line of a theme's recipe (work order 028): which models, how many (<see cref="Fewest"/> in the smallest room of
    /// its kind to <see cref="Most"/> in the largest, by floor area, as the combat rooms' counts were), and how they stand.
    /// Each prop's model is drawn from <see cref="Models"/>, or one for them all when <see cref="OneModel"/> (a pair).
    /// </summary>
    [Serializable]
    public sealed class ThemeProp
    {
        public PropModel[] Models = Array.Empty<PropModel>();
        public int Fewest;
        public int Most;
        public PropStance Stance;
        public bool OneModel;

        public ThemeProp() { }

        public ThemeProp(PropStance stance, int fewest, int most, params PropModel[] models)
        {
            Stance = stance;
            Fewest = fewest;
            Most = most;
            Models = models;
        }

        public ThemeProp Pair()
        {
            OneModel = true;
            return this;
        }
    }

    /// <summary>
    /// A room theme's recipe (work order 028): what stands in it and how, and for a combat theme how likely a room is to
    /// get it: <see cref="SmallWeight"/> in the smallest combat room to <see cref="LargeWeight"/> in the largest, times
    /// <see cref="EliteWeight"/> in the elite room.
    /// </summary>
    [Serializable]
    public sealed class ThemeRecipe
    {
        public RoomTheme Theme;
        public RoomKind Kind = RoomKind.Combat; // the kind of room it dresses: the archive has one for combat rooms and one for treasure rooms
        public ThemeProp[] Props = Array.Empty<ThemeProp>();
        public float SmallWeight = 1f;
        public float LargeWeight = 1f;
        public float EliteWeight = 1f;
        public float ClearMiddle;             // m round the room's middle where it puts nothing (the shrine's clear middle)

        public ThemeRecipe() { }

        public ThemeRecipe(RoomTheme theme, RoomKind kind, float small, float large, params ThemeProp[] props)
        {
            Theme = theme;
            Kind = kind;
            SmallWeight = small;
            LargeWeight = large;
            Props = props;
        }

        /// <summary>The most props a room of it may hold: the sum of its lines' most.</summary>
        public int MostProps
        {
            get
            {
                int n = 0;
                foreach (ThemeProp p in Props) n += Math.Max(0, p.Most);
                return n;
            }
        }
    }

    /// <summary>
    /// The room themes (work order 028; Kristian, 2026-11-19 and 2026-11-20: "good variety", and more of the crypt so one
    /// room differs from the next by what it is). Data, all first guesses. The combat rooms get a tomb hall, an ossuary, a
    /// shrine or an archive; the treasure rooms a vault or an archive; the exit room always the throne room (work order 089:
    /// with <see cref="LevelValues.ExitRoomsOfTheirOwn"/> on, the exit hall on floors 1 to 4); the entrance none (its braziers
    /// as before). Lengths in metres.
    /// </summary>
    [Serializable]
    public sealed class ThemeValues
    {
        public ThemeRecipe[] Recipes = DefaultRecipes();

        public float VaultShare = 0.6f;        // of the treasure rooms, about this share are vaults, the rest archives (never an archive off an archive)
        public float RowAcross = 0.3f;         // the tomb hall's two rows stand at this share and 1 less it across the room's short side
        public float ClusterFromWalls = 5f;    // the ossuary's two cluster middles at least this far from the walls
        public float ClusterBand = 1f;         // places round a cluster's middle are tried nearest first, in a drawn order within each band this wide
        public float ClusterReach = 7f;        // and no farther from it than this
        public float EdgeBand = 4.5f;          // the archive's cover stands at most this far from a wall: cover only at the edges
        public float OpenFromWalls = 2.5f;     // a prop of the open floor has its middle at least this far from the walls (one of the edge, EdgeFloor, may be nearer)
        public float RingShare = 0.3f;         // the shrine's ring of pillars: its radius, a share of the room's short side
        public float AltarStatueGap = 0.4f;    // between the shrine's statue at the wall and the altar before it
        public float BeforeAltar = 1.5f;       // between the altar's front and the basin
        public float FlankGap = 0.6f;          // between a set piece (altar, throne, chest) and each of the pair beside it
        public float ShelfCandleGap = 0.4f;    // between a bookshelf's end and the candelabra beside it

        /// <summary>The recipe of a theme for a kind of room; null when there is none.</summary>
        public ThemeRecipe Of(RoomTheme theme, RoomKind kind)
        {
            if (Recipes != null)
                foreach (ThemeRecipe r in Recipes)
                    if (r != null && r.Theme == theme && r.Kind == kind) return r;
            return null;
        }

        /// <summary>
        /// The first guesses (work order 028's table). Tomb hall: the most cover, coffins and sarcophagi in rows along the
        /// long axis, grave markers and a statue or two at the walls, pillars. Ossuary: mostly low, skull and bone piles in
        /// two loose clusters, cages in corners or at walls, urns or bone piles at the walls, rubble. Shrine (favoured for the elite room): an altar at
        /// the far end from the door in with a statue behind it, a pair of braziers or candelabra beside it and a basin
        /// before it, pillars in a ring or two rows, a clear middle. Archive: bookshelves at the walls, urns, a candelabra
        /// or two by the shelves, a broken pillar near a wall, rubble. Throne room: the bone throne at the north wall's
        /// middle facing south, a basin or a brazier either side, the pillars at the corners. Vault: the chest as before
        /// with a candelabra either side, a shelf or urns at a wall. The treasure room's archive keeps its chest.
        /// </summary>
        public static ThemeRecipe[] DefaultRecipes() => new[]
        {
            new ThemeRecipe(RoomTheme.TombHall, RoomKind.Combat, 1f, 3f,
                new ThemeProp(PropStance.Row, 3, 5, PropModel.Sarcophagus, PropModel.Coffin),
                new ThemeProp(PropStance.AlongWall, 3, 5, PropModel.GraveMarker),
                new ThemeProp(PropStance.AlongWall, 1, 2, PropModel.Statue),
                new ThemeProp(PropStance.OpenFloor, 2, 3, PropModel.Pillar)),
            new ThemeRecipe(RoomTheme.Ossuary, RoomKind.Combat, 2f, 2f,
                new ThemeProp(PropStance.Cluster, 4, 6, PropModel.SkullPile, PropModel.BonePile),
                new ThemeProp(PropStance.CornerOrWall, 1, 1, PropModel.Cage),
                new ThemeProp(PropStance.AlongWall, 1, 2, PropModel.Urns, PropModel.BonePile),
                new ThemeProp(PropStance.OpenFloor, 1, 1, PropModel.Rubble)),
            new ThemeRecipe(RoomTheme.Shrine, RoomKind.Combat, 1f, 2f,
                new ThemeProp(PropStance.FarAltar, 1, 1, PropModel.Altar),
                new ThemeProp(PropStance.BehindAltar, 1, 1, PropModel.Statue),
                new ThemeProp(PropStance.FlankAltar, 2, 2, PropModel.Brazier, PropModel.Candelabra).Pair(),
                new ThemeProp(PropStance.BeforeAltar, 1, 1, PropModel.Basin),
                new ThemeProp(PropStance.Ring, 4, 5, PropModel.Pillar)) { EliteWeight = 4f, ClearMiddle = 4f },
            new ThemeRecipe(RoomTheme.Archive, RoomKind.Combat, 3f, 1f,
                new ThemeProp(PropStance.AlongWall, 2, 3, PropModel.Bookshelf),
                new ThemeProp(PropStance.AlongWall, 1, 2, PropModel.Urns),
                new ThemeProp(PropStance.BesideShelf, 1, 2, PropModel.Candelabra),
                new ThemeProp(PropStance.EdgeFloor, 1, 1, PropModel.PillarBroken),
                new ThemeProp(PropStance.OpenFloor, 1, 1, PropModel.Rubble)),
            new ThemeRecipe(RoomTheme.ThroneRoom, RoomKind.Exit, 1f, 1f,
                new ThemeProp(PropStance.Throne, 1, 1, PropModel.BoneThrone),
                new ThemeProp(PropStance.FlankThrone, 2, 2, PropModel.Basin, PropModel.Brazier).Pair(),
                new ThemeProp(PropStance.CornerPillars, 4, 4, PropModel.Pillar)),
            new ThemeRecipe(RoomTheme.Vault, RoomKind.Treasure, 1f, 1f,
                new ThemeProp(PropStance.Chest, 1, 1, PropModel.Chest),
                new ThemeProp(PropStance.FlankChest, 2, 2, PropModel.Candelabra),
                new ThemeProp(PropStance.AlongWall, 1, 1, PropModel.Bookshelf, PropModel.Urns)),
            new ThemeRecipe(RoomTheme.Archive, RoomKind.Treasure, 1f, 1f,
                new ThemeProp(PropStance.Chest, 1, 1, PropModel.Chest),
                new ThemeProp(PropStance.AlongWall, 2, 2, PropModel.Bookshelf),
                new ThemeProp(PropStance.BesideShelf, 1, 1, PropModel.Candelabra),
                new ThemeProp(PropStance.AlongWall, 1, 1, PropModel.Urns)),
            // Work order 089: the exit hall, the exit room of floors 1 to 4 with LevelValues.ExitRoomsOfTheirOwn on (and the exit
            // rooms of their own in the room lab). A plain key built only from props that exist, low ones, so the room's shape and
            // its cover read in it; its look is a later order's. Urns at the walls, a broken pillar near one, rubble in the open
            // (the template's c marks first); the fight's cover is added to it as to every room (work order 088: pillars, rubble).
            new ThemeRecipe(RoomTheme.ExitHall, RoomKind.Exit, 1f, 1f,
                new ThemeProp(PropStance.AlongWall, 2, 3, PropModel.Urns),
                new ThemeProp(PropStance.EdgeFloor, 1, 1, PropModel.PillarBroken),
                new ThemeProp(PropStance.OpenFloor, 1, 2, PropModel.Rubble)),
        };
    }
}
