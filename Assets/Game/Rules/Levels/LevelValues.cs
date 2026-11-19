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

        /// <summary>Enemy points in the first combat room and in the last; the rooms between climb in steps.</summary>
        public int FewestEnemies = 1;
        public int MostEnemies = 3;

        public RoomSizeRange SizeOf(RoomKind kind) =>
            kind == RoomKind.Start ? StartSize
            : kind == RoomKind.Combat ? CombatSize
            : kind == RoomKind.Reward ? RewardSize
            : BossSize;

        /// <summary>
        /// Enemy points in the combat room with this number on the path (0 for the first): from
        /// <see cref="FewestEnemies"/> in the first, rising in even steps, to <see cref="MostEnemies"/> in the last.
        /// With the start values: 1, 1, 2, 2, 3.
        /// </summary>
        public int EnemiesInCombatRoom(int combatNumber)
        {
            int steps = MostEnemies - FewestEnemies + 1;
            int n = FewestEnemies + steps * combatNumber / Math.Max(1, CombatRooms);
            return Math.Min(MostEnemies, Math.Max(FewestEnemies, n));
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
        public int EnemyHealth = 3000;
        public int BossHealth = 9000;

        public LevelPlan Plan = new LevelPlan();
    }
}
