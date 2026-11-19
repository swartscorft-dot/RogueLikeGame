using System.Collections.Generic;
using Roguelike.Rules;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// A generated level built in grey boxes at run time, on the root object it sits on. Everything it makes is a child
    /// of that root and goes when the root goes: the materials it made go with it too.
    ///
    /// Each room is a floor (a flat box with its top at the ground) tinted for its kind, and walls stand along every
    /// edge between a room and anything else, with gaps at the doors: <see cref="LevelValues.WallHeight"/> high and
    /// <see cref="LevelValues.WallThickness"/> thick, centred on the edge. The camera looks north from the south
    /// (<see cref="CameraRig"/>), so a wall on a room's south side stands between the camera and that room's floor; it
    /// is built only <see cref="LevelValues.NearWallHeight"/> high. A NavMeshSurface on the root collects the floors and
    /// walls by their colliders and is built once they are there. Then a copy of the practice enemy stands at each
    /// enemy point, with the level's health, and stays dead when killed.
    ///
    /// Positions: the level's ground point (x, y) in metres is the world point (x, 0, y) moved so that the start
    /// room's middle lands on (<see cref="LevelValues.OffsetX"/>, 0, <see cref="LevelValues.OffsetZ"/>).
    /// </summary>
    public sealed class GeneratedLevel : MonoBehaviour
    {
        public static readonly Color StartTint = new Color(0.36f, 0.52f, 0.40f);
        public static readonly Color CombatTint = new Color(0.50f, 0.47f, 0.44f);
        public static readonly Color RewardTint = new Color(0.36f, 0.46f, 0.66f);
        public static readonly Color BossTint = new Color(0.58f, 0.30f, 0.30f);
        public static readonly Color WallTint = new Color(0.72f, 0.72f, 0.74f);

        public LevelLayout Layout { get; private set; }
        public LevelValues Values { get; private set; }
        public NavMeshSurface Surface { get; private set; }
        /// <summary>The enemies of each room, by the room's place in the run.</summary>
        public IReadOnlyDictionary<int, List<EnemyBrute>> EnemiesByRoom => _enemiesByRoom;
        public IReadOnlyList<EnemyBrute> Enemies => _enemies;
        /// <summary>The boss room's enemy, or null when there was no enemy to copy.</summary>
        public EnemyBrute Boss { get; private set; }
        public int Floors { get; private set; }
        public int Walls { get; private set; }
        public int LowWalls { get; private set; }

        readonly Dictionary<int, List<EnemyBrute>> _enemiesByRoom = new Dictionary<int, List<EnemyBrute>>();
        readonly List<EnemyBrute> _enemies = new List<EnemyBrute>();
        readonly List<Material> _materials = new List<Material>();
        Vector3 _shift;

        /// <summary>
        /// Builds the level under a new root object in the active scene. <paramref name="enemyTemplate"/> is copied
        /// for every enemy point (none are made without it); <paramref name="baseMaterial"/> is copied and tinted for
        /// the floors and walls (a URP Lit material is made without it).
        /// </summary>
        public static GeneratedLevel Build(LevelLayout layout, LevelValues values, EnemyBrute enemyTemplate, Material baseMaterial)
        {
            var root = new GameObject("GeneratedLevel_" + layout.SeedText);
            var level = root.AddComponent<GeneratedLevel>();
            level.Layout = layout;
            level.Values = values;
            LevelPoint start = layout.Rooms[0].PlayerStart ?? layout.Rooms[0].Middle(layout.CellSize);
            level._shift = new Vector3(values.OffsetX - (float)start.X, 0f, values.OffsetZ - (float)start.Y);

            level.BuildFloors(baseMaterial);
            level.BuildWalls(baseMaterial);

            level.Surface = root.AddComponent<NavMeshSurface>();
            level.Surface.collectObjects = CollectObjects.Children;
            level.Surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            level.Surface.BuildNavMesh();

            if (enemyTemplate != null) level.PlaceEnemies(enemyTemplate);
            return level;
        }

        public Vector3 ToWorld(LevelPoint p) => new Vector3((float)p.X, 0f, (float)p.Y) + _shift;

        public LevelPoint ToLevel(Vector3 world) => new LevelPoint(world.x - _shift.x, world.z - _shift.z);

        /// <summary>Where the player starts, on the ground.</summary>
        public Vector3 PlayerStart => ToWorld(Layout.Rooms[0].PlayerStart ?? Layout.Rooms[0].Middle(Layout.CellSize));

        /// <summary>The middle of a room, on the ground.</summary>
        public Vector3 Middle(LevelRoom room) => ToWorld(room.Middle(Layout.CellSize));

        /// <summary>The room under a world point, or null.</summary>
        public LevelRoom RoomAt(Vector3 world)
        {
            LevelPoint p = ToLevel(world);
            return Layout.RoomAt(p.X, p.Y);
        }

        void OnDestroy()
        {
            foreach (Material m in _materials)
                if (m != null) Destroy(m);
        }

        // ---------------- Floors and walls ----------------

        Material Tinted(Material source, Color tint, string name)
        {
            Material m = source != null ? new Material(source) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.name = name;
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            _materials.Add(m);
            return m;
        }

        void BuildFloors(Material baseMaterial)
        {
            var tints = new Dictionary<RoomKind, Material>
            {
                [RoomKind.Start] = Tinted(baseMaterial, StartTint, "LevelFloor_Start"),
                [RoomKind.Combat] = Tinted(baseMaterial, CombatTint, "LevelFloor_Combat"),
                [RoomKind.Reward] = Tinted(baseMaterial, RewardTint, "LevelFloor_Reward"),
                [RoomKind.Boss] = Tinted(baseMaterial, BossTint, "LevelFloor_Boss"),
            };
            float cs = Layout.CellSize;
            foreach (LevelRoom room in Layout.Rooms)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor_" + room.Letter + room.Order;
                floor.transform.SetParent(transform, false);
                Vector3 middle = Middle(room);
                floor.transform.position = new Vector3(middle.x, -Values.FloorThickness * 0.5f, middle.z);
                floor.transform.localScale = new Vector3(room.Rect.Width * cs, Values.FloorThickness, room.Rect.Height * cs);
                floor.GetComponent<Renderer>().sharedMaterial = tints[room.Kind];
                Floors++;
            }
        }

        /// <summary>
        /// Walls along every cell edge that has a room on one side and something else (another room, or nothing) on
        /// the other, except at doors. Edges in a row of the same kind are joined into one wall.
        /// </summary>
        void BuildWalls(Material baseMaterial)
        {
            Material wallMaterial = Tinted(baseMaterial, WallTint, "LevelWall");
            CellRect box = Layout.Bounds();
            int Owner(int x, int y) => Layout.RoomAtCell(x, y)?.Order ?? -1;
            bool IsDoor(bool vertical, int line, int cell)
            {
                foreach (LevelDoor d in Layout.Doors)
                    if (d.Vertical == vertical && d.Line == line && cell >= d.From && cell < d.From + d.Width) return true;
                return false;
            }

            // Vertical lines (north to south): always full height.
            for (int x = box.X; x <= box.Right; x++)
            {
                int runFrom = int.MinValue;
                for (int y = box.Y; y <= box.Top; y++)
                {
                    bool wall = y < box.Top && Owner(x - 1, y) != Owner(x, y) && !IsDoor(true, x, y);
                    if (wall && runFrom == int.MinValue) runFrom = y;
                    if (!wall && runFrom != int.MinValue)
                    {
                        Wall(true, x, runFrom, y, Values.WallHeight, wallMaterial);
                        runFrom = int.MinValue;
                    }
                }
            }
            // Horizontal lines (west to east): low where the room is to the north, so the wall is its south side.
            for (int y = box.Y; y <= box.Top; y++)
            {
                int runFrom = int.MinValue;
                bool runLow = false;
                for (int x = box.X; x <= box.Right; x++)
                {
                    bool wall = x < box.Right && Owner(x, y - 1) != Owner(x, y) && !IsDoor(false, y, x);
                    bool low = wall && Owner(x, y) >= 0;
                    if (runFrom != int.MinValue && (!wall || low != runLow))
                    {
                        Wall(false, y, runFrom, x, runLow ? Values.NearWallHeight : Values.WallHeight, wallMaterial);
                        runFrom = int.MinValue;
                    }
                    if (wall && runFrom == int.MinValue)
                    {
                        runFrom = x;
                        runLow = low;
                    }
                }
            }
        }

        /// <summary>A wall on a line from one cell boundary to another, a little longer at both ends so corners close.</summary>
        void Wall(bool vertical, int line, int from, int to, float height, Material material)
        {
            float cs = Layout.CellSize, t = Values.WallThickness;
            float along = (to - from) * cs + t;
            float mid = (from + to) * 0.5f * cs;
            LevelPoint centre = vertical ? new LevelPoint(line * cs, mid) : new LevelPoint(mid, line * cs);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bool low = height < Values.WallHeight;
            wall.name = (low ? "LowWall_" : "Wall_") + (vertical ? "V" : "H") + line + "_" + from + "-" + to;
            wall.transform.SetParent(transform, false);
            wall.transform.position = ToWorld(centre) + Vector3.up * (height * 0.5f);
            wall.transform.localScale = vertical ? new Vector3(t, height, along) : new Vector3(along, height, t);
            wall.GetComponent<Renderer>().sharedMaterial = material;
            Walls++;
            if (low) LowWalls++;
        }

        // ---------------- Enemies ----------------

        void PlaceEnemies(EnemyBrute template)
        {
            foreach (LevelRoom room in Layout.Rooms)
            {
                for (int i = 0; i < room.EnemyPoints.Count; i++)
                {
                    Vector3 at = ToWorld(room.EnemyPoints[i]);
                    // Made where it will stand, so that is its home (EnemyBrute reads it in Awake).
                    GameObject copy = Instantiate(template.gameObject, new Vector3(at.x, template.transform.position.y, at.z),
                                                  Quaternion.LookRotation(Vector3.back), transform);
                    copy.name = "Enemy_" + room.Letter + room.Order + "_" + i;
                    var brute = copy.GetComponent<EnemyBrute>();
                    bool boss = room.Kind == RoomKind.Boss;
                    brute.HealthOverride = boss ? Values.BossHealth : Values.EnemyHealth;
                    brute.ComesBack = false;
                    // The practice enemy may be dead and hidden at this moment; its copy starts alive and seen.
                    foreach (Renderer r in copy.GetComponentsInChildren<Renderer>(true))
                        if (!(r is LineRenderer)) r.enabled = true;
                    if (!_enemiesByRoom.TryGetValue(room.Order, out List<EnemyBrute> list)) _enemiesByRoom[room.Order] = list = new List<EnemyBrute>();
                    list.Add(brute);
                    _enemies.Add(brute);
                    if (boss) Boss = brute;
                }
            }
        }
    }
}
