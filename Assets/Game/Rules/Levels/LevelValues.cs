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
    /// The plan of a run: how many rooms of each kind and how big. Data, not code, so other plans can be tried.
    /// The main path is the start room, <see cref="CombatRooms"/> combat rooms and the boss room; the reward rooms
    /// are small side rooms, each off a different combat room.
    /// </summary>
    [Serializable]
    public sealed class LevelPlan
    {
        public int CombatRooms = 5;
        public int RewardRooms = 4;

        public RoomSizeRange StartSize = new RoomSizeRange(4, 4);
        public RoomSizeRange CombatSize = new RoomSizeRange(5, 7);
        public RoomSizeRange RewardSize = new RoomSizeRange(3, 3);
        public RoomSizeRange BossSize = new RoomSizeRange(8, 8);

        /// <summary>
        /// What stands in each combat room, in the order of the path (work order 008): one entry per enemy point. A
        /// combat room past the end of the list gets the last entry. Which point gets which entry is drawn from the
        /// seed. With the start values: a pack of four swarmers; two brutes; two shooters and a brute; a pack of four
        /// swarmers and a shooter; a guard and two swarmers.
        /// </summary>
        public RoomEnemies[] CombatEnemies = DefaultCombatEnemies();
        /// <summary>What stands in the boss room, at its middle: a guard, with <see cref="LevelValues.BossHealth"/>.</summary>
        public RoomEnemies BossEnemies = new RoomEnemies(new EnemyGroup(EnemyKind.Guard));

        public static RoomEnemies[] DefaultCombatEnemies() => new[]
        {
            new RoomEnemies(new EnemyGroup(EnemyKind.Swarmer, 4)),
            new RoomEnemies(new EnemyGroup(EnemyKind.Brute), new EnemyGroup(EnemyKind.Brute)),
            new RoomEnemies(new EnemyGroup(EnemyKind.Shooter), new EnemyGroup(EnemyKind.Shooter), new EnemyGroup(EnemyKind.Brute)),
            new RoomEnemies(new EnemyGroup(EnemyKind.Swarmer, 4), new EnemyGroup(EnemyKind.Shooter)),
            new RoomEnemies(new EnemyGroup(EnemyKind.Guard), new EnemyGroup(EnemyKind.Swarmer), new EnemyGroup(EnemyKind.Swarmer)),
        };

        public RoomSizeRange SizeOf(RoomKind kind) =>
            kind == RoomKind.Start ? StartSize
            : kind == RoomKind.Combat ? CombatSize
            : kind == RoomKind.Reward ? RewardSize
            : BossSize;

        /// <summary>What the combat room with this number on the path (0 for the first) holds, one group per enemy point.</summary>
        public EnemyGroup[] CombatGroups(int combatNumber)
        {
            if (CombatEnemies == null || CombatEnemies.Length == 0) return new[] { new EnemyGroup(EnemyKind.Brute) };
            RoomEnemies room = CombatEnemies[Math.Max(0, Math.Min(CombatEnemies.Length - 1, combatNumber))];
            return room?.Groups ?? Array.Empty<EnemyGroup>();
        }

        /// <summary>What a room of this kind holds; <paramref name="combatNumber"/> counts the combat rooms only. Empty for the start and reward rooms.</summary>
        public EnemyGroup[] GroupsFor(RoomKind kind, int combatNumber) =>
            kind == RoomKind.Combat ? CombatGroups(combatNumber)
            : kind == RoomKind.Boss ? BossEnemies?.Groups ?? Array.Empty<EnemyGroup>()
            : Array.Empty<EnemyGroup>();

        /// <summary>Enemy points in the combat room with this number on the path (0 for the first): one per group of its plan.</summary>
        public int EnemiesInCombatRoom(int combatNumber) => CombatGroups(combatNumber).Length;
    }

    /// <summary>What one room holds: a group per enemy point (work order 008).</summary>
    [Serializable]
    public sealed class RoomEnemies
    {
        public EnemyGroup[] Groups = Array.Empty<EnemyGroup>();

        public RoomEnemies() { }

        public RoomEnemies(params EnemyGroup[] groups) => Groups = groups;
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
        public float EnemySpacing = 4f;        // m between two enemies of a room
        public float PointStep = 1f;           // m between the places considered for an enemy

        // The grey-box level
        public float WallHeight = 3f;          // m
        public float NearWallHeight = 1f;      // m, a wall between the camera and the room's floor
        public float WallThickness = 0.5f;     // m
        public float FloorThickness = 0.2f;    // m, under the ground
        public float OffsetX = 500f;           // m, where the start room's middle is put, far to one side of the practice room
        public float OffsetZ = 0f;

        // Enemies
        public int EnemyHealth = 3000;         // a brute's; the other kinds have their own (work order 008)
        public int BossHealth = 9000;          // the boss room's enemy, whatever its kind
        public float PackRadius = 1.2f;        // m from a pack's point to each of its swarmers; under WallClearance

        public LevelPlan Plan = new LevelPlan();
    }
}
