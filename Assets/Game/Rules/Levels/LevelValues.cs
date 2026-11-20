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

        public RoomSizeRange SizeOf(RoomKind kind) =>
            kind == RoomKind.Entrance ? EntranceSize
            : kind == RoomKind.Combat ? CombatSize
            : kind == RoomKind.Treasure ? TreasureSize
            : ExitSize;
    }

    /// <summary>
    /// One mix of enemies a room may hold (decision 0008, work order 012): the shares of the room's enemies, guards
    /// apart, that are swarmers, shooters and brutes, and how many guards stand with them. A kind with a share above 0
    /// gets at least one; the swarmers take what rounding leaves over.
    /// </summary>
    [Serializable]
    public sealed class EnemyMix
    {
        public string Name = "";
        public float Swarmers;
        public float Shooters;
        public float Brutes;
        public int Guards;

        public EnemyMix() { }

        public EnemyMix(string name, float swarmers, float shooters, float brutes, int guards)
        {
            Name = name;
            Swarmers = swarmers;
            Shooters = shooters;
            Brutes = brutes;
            Guards = guards;
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
        public int BossHealth = 9000;          // the act boss: a guard, until the boss model comes
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
        public FloorEnemies[] Floors = DefaultFloors();

        // Props (work order 009)
        public bool Props = true;              // off: levels are generated and built without props

        // The crypt wall kit (work order 016, 2026-11-20): how a floor looks, never what it is
        public bool CryptKit = true;           // off: the grey boxes as before, for comparing; the generator never reads it

        public LevelPlan Plan = new LevelPlan();
        public PropValues PropRules = new PropValues();
        public CryptKitValues Crypt = new CryptKitValues();

        /// <summary>
        /// The enemies by floor number (work order 012, first guesses): floor 1 six to eight in a combat room, mostly
        /// swarmers with a few brutes and a shooter; each floor after it more, with more shooters and brutes; guards from
        /// floor 2; floor 5 ten to fourteen.
        /// </summary>
        public static FloorEnemies[] DefaultFloors() => new[]
        {
            new FloorEnemies(6, 8,
                new EnemyMix("swarm", 0.7f, 0.15f, 0.15f, 0),
                new EnemyMix("swarm and brutes", 0.6f, 0.15f, 0.25f, 0),
                new EnemyMix("swarm and a shooter", 0.65f, 0.2f, 0.15f, 0)),
            new FloorEnemies(7, 9,
                new EnemyMix("swarm", 0.55f, 0.2f, 0.25f, 0),
                new EnemyMix("guarded swarm", 0.55f, 0.2f, 0.25f, 1),
                new EnemyMix("crossfire", 0.45f, 0.35f, 0.2f, 0)),
            new FloorEnemies(8, 11,
                new EnemyMix("guarded", 0.45f, 0.25f, 0.3f, 1),
                new EnemyMix("crossfire", 0.4f, 0.35f, 0.25f, 0),
                new EnemyMix("heavy", 0.35f, 0.3f, 0.35f, 1)),
            new FloorEnemies(9, 12,
                new EnemyMix("guarded", 0.4f, 0.3f, 0.3f, 1),
                new EnemyMix("heavy", 0.3f, 0.35f, 0.35f, 1),
                new EnemyMix("crossfire", 0.45f, 0.35f, 0.2f, 0)),
            new FloorEnemies(10, 14,
                new EnemyMix("guarded", 0.35f, 0.35f, 0.3f, 1),
                new EnemyMix("heavy", 0.3f, 0.35f, 0.35f, 1),
                new EnemyMix("crossfire", 0.4f, 0.3f, 0.3f, 0)),
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

        /// <summary>A generated floor's health for an enemy of this kind; the act boss's for the boss.</summary>
        public int HealthOf(EnemyKind kind, bool boss = false) =>
            boss ? BossHealth
            : kind == EnemyKind.Swarmer ? SwarmerHealth
            : kind == EnemyKind.Shooter ? ShooterHealth
            : kind == EnemyKind.Guard ? GuardHealth
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

        // Cover (Kristian, 2026-11-19: shots are stopped by walls and pillars)
        public float CoverHeight = 1.2f;       // a prop at least this tall stops her shots (altar, grave marker, pillars, statue); she shoots over a lower one

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
}
