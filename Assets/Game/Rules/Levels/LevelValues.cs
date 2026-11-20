using System;

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
    /// the exit room, one after another; the treasure rooms are short dead ends, each off a different combat room.
    /// Until work order 012: five combat rooms, four reward rooms and a boss room.
    /// Work order 017 (decision 0008 as amended 2026-11-20, Kristian: "one hard room in the middle of every floor"): one of
    /// the combat rooms is the elite room, <see cref="EliteRoomNumber"/>.
    /// </summary>
    [Serializable]
    public sealed class LevelPlan
    {
        public int CombatRooms = 4;
        public int TreasureRooms = 2;

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
        public float EliteHealthScale = 4f;    // times its kind's health for the floor
        public float EliteDamageScale = 1.25f; // its attacks' damage, times this
        public float EliteSizeScale = 4f / 3f; // shown this many times as large, and its body as wide
        public float EliteStaggerShare = 0.5f; // a kind with no stagger bar gets one this share of the guard's
        public float EliteRoomToStand = 1f;    // m more than any enemy keeps from the walls, the doors and the other groups: its larger body
        public int EliteKindsFromFloor = 2;    // from this floor a brute, a shooter or a guard; before it a brute
        public int EliteCasterFromFloor = 3;   // work order 027: from this floor the elite may be a caster too
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
        // The casters (work order 027): from floor 2, one a combat room at most on floors 2 and 3, two on floors 4 and 5,
        // and an exit room one more; they stand at the far side as the shooters do, never near each other.
        public int[] CastersMostInRoom = { 0, 1, 1, 2, 2 }; // by floor number, from 1; past the end its last
        public int CastersMoreInExit = 1;
        public float CasterSpacing = 8f;       // m between two casters of a room
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
        /// from <see cref="EliteCasterFromFloor"/> a caster too (work order 027).
        /// </summary>
        public EnemyKind[] EliteKinds(int floor) =>
            floor < EliteKindsFromFloor ? new[] { EnemyKind.Brute }
            : floor < EliteCasterFromFloor ? new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard }
            : new[] { EnemyKind.Brute, EnemyKind.Shooter, EnemyKind.Guard, EnemyKind.Caster };

        /// <summary>
        /// The most casters a room of this kind may hold on this floor (work order 027): <see cref="CastersMostInRoom"/> for
        /// the floor in a combat room, <see cref="CastersMoreInExit"/> more in the exit room (none more on a floor with none);
        /// none elsewhere. The elite apart.
        /// </summary>
        public int CastersMost(RoomKind kind, int floor)
        {
            if (CastersMostInRoom == null || CastersMostInRoom.Length == 0 || (kind != RoomKind.Combat && kind != RoomKind.Exit)) return 0;
            int room = Math.Max(0, CastersMostInRoom[Math.Max(0, Math.Min(CastersMostInRoom.Length - 1, floor - 1))]);
            return kind == RoomKind.Exit && room > 0 ? room + CastersMoreInExit : room;
        }

        /// <summary>An elite's health on a generated floor (work order 017): its kind's, times <see cref="EliteHealthScale"/>.</summary>
        public int EliteHealthOf(EnemyKind kind) => (int)Math.Round(HealthOf(kind) * (double)EliteHealthScale, MidpointRounding.AwayFromZero);

        /// <summary>A generated floor's health for an enemy of this kind; the act boss's for the boss.</summary>
        public int HealthOf(EnemyKind kind, bool boss = false) =>
            boss || kind == EnemyKind.Boss ? BossHealth
            : kind == EnemyKind.Swarmer ? SwarmerHealth
            : kind == EnemyKind.Shooter ? ShooterHealth
            : kind == EnemyKind.Guard ? GuardHealth
            : kind == EnemyKind.Caster ? CasterHealth
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
        public float BossMiddleClear = 8f;     // round the boss room's middle, nothing

        // Walls and wall pieces (work order 028)
        public float WallFace = 0.275f;        // a wall's inner face, from its line: half the kit wall's 0.55 m (the grey boxes' face is at 0.25)
        public float HandWidth = 0.1f;         // a wall piece's back is at most this far from the face
        public float WallPieceGap = 0.03f;     // between the face and a wall piece's back, where it is put
        public float SouthWallMostHeight = 1.3f; // a prop taller than this never stands at a room's south wall: the low one the camera looks over
        public float MinWalkable = 2f / 3f;    // a combat room keeps at least this share of its floor walkable
        public float WalkRadius = 0.5f;        // floor is walkable at least this far from a prop's footprint and the walls (about the NavMesh agent's radius)

        // Cover (Kristian, 2026-11-19: shots are stopped by walls and pillars)
        public float CoverHeight = 1.2f;       // a prop at least this tall stops her shots (altar, grave marker, pillars, statue); she shoots over a lower one
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
    /// shrine or an archive; the treasure rooms a vault or an archive; the exit room always the throne room; the entrance
    /// none (its braziers as before). Lengths in metres.
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
        };
    }
}
