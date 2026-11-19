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
    /// walls by their colliders, and the props' (work order 009: each prop of the description stands at its place on
    /// the walls' layer with a box or capsule collider of its footprint and height, never lower than a little over the
    /// agent's step, so a low prop is not a step to walk over), and is built once they are there.
    /// Cover (Kristian, 2026-11-19: shots are stopped by walls and pillars): every wall, and every prop at least
    /// <see cref="PropValues.CoverHeight"/> tall, carries <see cref="Cover"/>, which stops her shots and skills and the
    /// shooter's sight and line; she and the shooter shoot over a lower prop. The enemies' strikes on the ground do not
    /// look at cover. Then the enemies of each enemy point stand there, of
    /// the point's kind (work order 008, <see cref="EnemyFactory"/>), and stay dead when killed.
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
        /// <summary>The enemies of each room, by the room's place in the run, in the order of its points (a pack's together).</summary>
        public IReadOnlyDictionary<int, List<Enemy>> EnemiesByRoom => _enemiesByRoom;
        public IReadOnlyList<Enemy> Enemies => _enemies;
        /// <summary>What stands at each enemy point of the level, in the order of the rooms and their points.</summary>
        public IReadOnlyList<PlacedGroup> Groups => _groups;
        /// <summary>The act boss (work order 012: only on the act's last floor), or null.</summary>
        public Enemy Boss { get; private set; }
        public int Floors { get; private set; }
        public int Walls { get; private set; }
        public int LowWalls { get; private set; }
        /// <summary>The layer the walls are on (work order 009): the props are put on it too.</summary>
        public int WallLayer { get; private set; }
        /// <summary>The props built, in the order of the rooms and of each room's props.</summary>
        public IReadOnlyList<PlacedProp> Props => _props;
        /// <summary>The lowest a prop's collider is made, in metres: the NavMesh agent's step height and <see cref="LowPropMargin"/>.</summary>
        public float LowestPropCollider { get; private set; }
        public const float LowPropMargin = 0.25f;

        readonly List<PlacedProp> _props = new List<PlacedProp>();
        readonly Dictionary<int, List<Enemy>> _enemiesByRoom = new Dictionary<int, List<Enemy>>();
        readonly List<Enemy> _enemies = new List<Enemy>();
        readonly List<PlacedGroup> _groups = new List<PlacedGroup>();
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
            level.BuildProps();

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
                [RoomKind.Entrance] = Tinted(baseMaterial, StartTint, "LevelFloor_Entrance"),
                [RoomKind.Combat] = Tinted(baseMaterial, CombatTint, "LevelFloor_Combat"),
                [RoomKind.Treasure] = Tinted(baseMaterial, RewardTint, "LevelFloor_Treasure"),
                [RoomKind.Exit] = Tinted(baseMaterial, BossTint, "LevelFloor_Exit"),
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
            wall.AddComponent<Cover>(); // every wall, the low near ones too, stops shots (Kristian, 2026-11-19)
            WallLayer = wall.layer;
            Walls++;
            if (low) LowWalls++;
        }

        // ---------------- Props (work order 009) ----------------

        /// <summary>
        /// Each prop of the description at its place, turned, under the root, on the walls' layer, with a collider of
        /// its footprint and height (<see cref="PropFactory"/>): the NavMesh is built round it, she cannot walk through
        /// it and enemies path round it. One material per model, made here and destroyed with the level.
        /// </summary>
        void BuildProps()
        {
            var materials = new Dictionary<PropModel, Material>();
            // Low props block movement like the rest: their colliders stand over the height the agent steps up.
            LowestPropCollider = NavMesh.GetSettingsByID(0).agentClimb + LowPropMargin;
            foreach (LevelRoom room in Layout.Rooms)
            {
                for (int i = 0; i < room.Props.Count; i++)
                {
                    LevelProp prop = room.Props[i];
                    if (!materials.TryGetValue(prop.Model, out Material m))
                    {
                        materials[prop.Model] = m = PropFactory.MakeMaterial(prop.Model);
                        _materials.Add(m);
                    }
                    string name = "Prop_" + room.Letter + room.Order + "_" + i + "_" + prop.Model;
                    GameObject go = PropFactory.Make(prop, ToWorld(prop.Position), transform, WallLayer, m, name, LowestPropCollider);
                    bool cover = prop.IsCover(Values.PropRules);
                    if (cover) go.AddComponent<Cover>(); // a tall prop stops shots; she shoots over a low one (Kristian, 2026-11-19)
                    _props.Add(new PlacedProp { Room = room, Prop = prop, Object = go, IsCover = cover });
                }
            }
        }

        /// <summary>A prop as built: its room, its description, and the object standing there.</summary>
        public sealed class PlacedProp
        {
            public LevelRoom Room;
            public LevelProp Prop;
            public GameObject Object;
            /// <summary>It stops shots: it is at least <see cref="PropValues.CoverHeight"/> tall and carries <see cref="Cover"/>.</summary>
            public bool IsCover;
        }

        // ---------------- Enemies ----------------

        /// <summary>
        /// What stands at each enemy point (work order 008): the group's enemies of its kind, each where the description
        /// puts it (work order 012: one on the point, a pack round it, a line side by side across it). Every kind has
        /// the floor's health for it (<see cref="LevelValues.HealthOf"/>), the act boss <see cref="LevelValues.BossHealth"/>;
        /// the practice room's enemies keep their own.
        /// </summary>
        void PlaceEnemies(EnemyBrute template)
        {
            foreach (LevelRoom room in Layout.Rooms)
            {
                for (int i = 0; i < room.EnemyPoints.Count; i++)
                {
                    EnemyGroup group = room.GroupAt(i);
                    var placed = new PlacedGroup { Room = room, Point = i, Group = group };
                    LevelPoint[] places = room.PlacesAt(i);
                    for (int m = 0; m < places.Length; m++)
                    {
                        string name = "Enemy_" + room.Letter + room.Order + "_" + i + (places.Length > 1 ? "_" + m : "");
                        Enemy enemy = EnemyFactory.Make(group.Kind, ToWorld(places[m]), transform, template, name);
                        if (enemy == null) continue;
                        enemy.HealthOverride = Values.HealthOf(group.Kind, group.Boss);
                        placed.Members.Add(enemy);
                        if (!_enemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) _enemiesByRoom[room.Order] = list = new List<Enemy>();
                        list.Add(enemy);
                        _enemies.Add(enemy);
                        if (group.Boss && Boss == null) Boss = enemy;
                    }
                    _groups.Add(placed);
                }
            }
        }

        /// <summary>The enemies of the exit room (work order 012): when every one is dead, the floor is finished.</summary>
        public IReadOnlyList<Enemy> ExitEnemies
        {
            get
            {
                LevelRoom exit = Layout.ExitRoom;
                return exit != null && _enemiesByRoom.TryGetValue(exit.Order, out List<Enemy> list) ? list : (IReadOnlyList<Enemy>)System.Array.Empty<Enemy>();
            }
        }

        /// <summary>True once every enemy of the exit room is dead; false while there is none to kill (no enemy to copy).</summary>
        public bool ExitRoomCleared
        {
            get
            {
                IReadOnlyList<Enemy> exit = ExitEnemies;
                if (exit.Count == 0) return false;
                for (int i = 0; i < exit.Count; i++)
                    if (exit[i] != null && !exit[i].IsDead) return false;
                return true;
            }
        }

        /// <summary>The enemies that stand at one enemy point: the point's room, its number in the room, what the plan put there.</summary>
        public sealed class PlacedGroup
        {
            public LevelRoom Room;
            public int Point;
            public EnemyGroup Group;
            public readonly List<Enemy> Members = new List<Enemy>();
        }
    }
}
