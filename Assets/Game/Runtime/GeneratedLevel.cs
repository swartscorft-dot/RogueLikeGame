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
    /// Each room is a floor (a flat box with its top at the ground) tinted for its kind (the elite room for itself, work
    /// order 017), and walls stand along every
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
    /// look at cover. Every wall and every prop, tall or low, also carries <see cref="Shelter"/>: she can take cover
    /// against it (Kristian, 2026-11-19, an experiment). A slender prop (work order 028, the candelabra) carries neither,
    /// however tall: it only blocks walking. A prop with an emission map glows in the game's cyan
    /// (<see cref="PropValues.Glow"/>). Then the enemies of each enemy point stand there, of
    /// the point's kind (work order 008, <see cref="EnemyFactory"/>), and stay dead when killed; the elite of the elite
    /// room is made one (work order 017, <see cref="EnemyFactory.MakeElite"/>).
    ///
    /// The crypt kit (work order 016, 2026-11-20; <see cref="LevelValues.CryptKit"/>, on by default): once the NavMesh
    /// is built, the wall boxes stop showing and the kit's walls, low walls, piers, posts, door arches and lanterns are
    /// drawn in their place, and the floor boxes show stone floors (<see cref="DressInCryptKit"/>). Only what is drawn
    /// changes: the boxes keep their colliders, cover, shelter and layer, and no piece has a collider. Off, the grey
    /// boxes show as before.
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
        public static readonly Color EliteTint = new Color(0.62f, 0.52f, 0.28f); // work order 017: the elite room's, warm
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
        /// <summary>The elite of the elite room (work order 017), or null.</summary>
        public Enemy Elite { get; private set; }
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
        readonly List<GameObject> _floorBoxes = new List<GameObject>();
        readonly List<GameObject> _wallBoxes = new List<GameObject>();
        readonly List<KitPiece> _kitPieces = new List<KitPiece>();
        Vector3 _shift;

        /// <summary>The floor boxes, one per room in the order of the rooms.</summary>
        public IReadOnlyList<GameObject> FloorBoxes => _floorBoxes;
        /// <summary>The wall boxes, full and low, in the order they were built.</summary>
        public IReadOnlyList<GameObject> WallBoxes => _wallBoxes;
        /// <summary>Where the crypt kit's pieces go (work order 016); null with the kit off.</summary>
        public CryptKitPlan KitPlan { get; private set; }
        /// <summary>The object every kit piece is under; null with the kit off.</summary>
        public Transform KitRoot { get; private set; }
        /// <summary>The kit pieces built, sections first, then arches, piers and posts, lanterns.</summary>
        public IReadOnlyList<KitPiece> KitPieces => _kitPieces;
        /// <summary>The lanterns' lights, when they are on (<see cref="CryptKitValues.LanternLights"/>); else null.</summary>
        public CryptKitLights KitLights { get; private set; }

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

            // Work order 016: the kit is put in only once the NavMesh is built, so nothing of it can be in it.
            if (values.CryptKit) level.DressInCryptKit();

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
            Material elite = null;
            float cs = Layout.CellSize;
            foreach (LevelRoom room in Layout.Rooms)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Floor_" + room.Letter + room.Order;
                floor.transform.SetParent(transform, false);
                Vector3 middle = Middle(room);
                floor.transform.position = new Vector3(middle.x, -Values.FloorThickness * 0.5f, middle.z);
                floor.transform.localScale = new Vector3(room.Rect.Width * cs, Values.FloorThickness, room.Rect.Height * cs);
                floor.GetComponent<Renderer>().sharedMaterial = room.Elite
                    ? elite != null ? elite : elite = Tinted(baseMaterial, EliteTint, "LevelFloor_Elite")
                    : tints[room.Kind];
                _floorBoxes.Add(floor);
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
            wall.AddComponent<Shelter>(); // and she can take cover against it (Kristian, 2026-11-19, an experiment)
            WallLayer = wall.layer;
            _wallBoxes.Add(wall);
            Walls++;
            if (low) LowWalls++;
        }

        // ---------------- The crypt kit (work order 016, 2026-11-20) ----------------

        /// <summary>A kit piece as built: what it is, its place in its list of the plan, and the object.</summary>
        public sealed class KitPiece
        {
            public CryptPiece Kind;
            /// <summary>Into the plan's sections, arches, points or lanterns, by its kind.</summary>
            public int Index;
            public GameObject Object;
            public long Triangles;
        }

        static Vector3 SideOf(bool vertical, int side) => vertical ? new Vector3(side, 0f, 0f) : new Vector3(0f, 0f, side);

        /// <summary>
        /// Dresses the boxes in the crypt kit (work order 016): only what is drawn changes. Every wall box keeps its
        /// collider, <see cref="Cover"/>, <see cref="Shelter"/> and layer and only stops showing; the kit's pieces stand
        /// where <see cref="CryptKitPlan"/> puts them, under <see cref="KitRoot"/>, without colliders, on
        /// <see cref="CryptKit.Layer"/>. Each floor box shows its room kind's floor texture, one copy a cell. One material
        /// per model and per room kind, made here and destroyed with the level.
        ///
        /// A section stands on its cell edge, its length along the line, its front (+Z) to the side the plan says; the
        /// niche's model turned the other way when its niche is on its -Z face. An arch stands in the middle of its door
        /// cell, stretched along the wall (<see cref="CryptKitValues.ArchToGap"/>) so its clear opening is no narrower
        /// than the gap the wall boxes leave. Piers and posts stand on their grid points. A lantern hangs on its
        /// section's face at <see cref="CryptKitValues.LanternHeight"/>, its back on the face (read from the wall model's
        /// depth), reaching into the room whichever way its model reaches.
        /// </summary>
        void DressInCryptKit()
        {
            CryptKitValues v = Values.Crypt ?? new CryptKitValues();
            KitPlan = CryptKitPlan.Make(Layout, v);
            var kitRoot = new GameObject("CryptKit") { layer = CryptKit.Layer };
            kitRoot.transform.SetParent(transform, false);
            KitRoot = kitRoot.transform;
            float cs = Layout.CellSize;

            var materials = new Dictionary<CryptPiece, Material>();
            KitPiece Place(CryptPiece kind, int index, string name, LevelPoint at, float height, Quaternion rotation, Vector3 scale)
            {
                CryptKit.Model model = CryptKit.Load(kind);
                if (model == null) return null;
                if (!materials.TryGetValue(kind, out Material m))
                {
                    materials[kind] = m = CryptKit.MakeMaterial(kind, v);
                    _materials.Add(m);
                }
                GameObject go = CryptKit.Make(model, m, KitRoot, name, ToWorld(at) + Vector3.up * height, rotation, scale);
                var piece = new KitPiece { Kind = kind, Index = index, Object = go, Triangles = model.Triangles };
                _kitPieces.Add(piece);
                return piece;
            }
            LevelPoint EdgeMiddle(bool vertical, int line, int cell) =>
                vertical ? new LevelPoint(line * cs, (cell + 0.5) * cs) : new LevelPoint((cell + 0.5) * cs, line * cs);
            string EdgeName(bool vertical, int line, int cell) => (vertical ? "V" : "H") + line + "_" + cell;

            // Sections.
            for (int i = 0; i < KitPlan.Sections.Count; i++)
            {
                CryptKitSection s = KitPlan.Sections[i];
                CryptPiece kind = s.Low ? CryptPiece.LowWall : s.Niche ? CryptPiece.WallNiche : CryptPiece.Wall;
                Quaternion turn = Quaternion.LookRotation(SideOf(s.Vertical, s.Facing));
                if (kind == CryptPiece.WallNiche && !v.NicheFacesPlusZ) turn *= Quaternion.Euler(0f, 180f, 0f);
                string prefix = kind == CryptPiece.LowWall ? "Kit_Low_" : kind == CryptPiece.WallNiche ? "Kit_Niche_" : "Kit_Wall_";
                Place(kind, i, prefix + EdgeName(s.Vertical, s.Line, s.Cell), EdgeMiddle(s.Vertical, s.Line, s.Cell), 0f, turn, Vector3.one);
            }

            // Arches, stretched so the opening is at least the boxes' gap.
            CryptKit.Model arch = CryptKit.Load(CryptPiece.Arch);
            float stretch = 1f;
            if (arch != null && v.ArchToGap)
            {
                float opening = arch.Bounds.size.x * v.ArchOpeningShare;
                float gap = cs - Values.WallThickness;
                if (opening > 1e-3f && opening < gap) stretch = gap / opening;
            }
            ArchStretch = stretch;
            for (int i = 0; i < KitPlan.Arches.Count; i++)
            {
                CryptKitArch a = KitPlan.Arches[i];
                Place(CryptPiece.Arch, i, "Kit_Arch_" + EdgeName(a.Vertical, a.Line, a.Cell), EdgeMiddle(a.Vertical, a.Line, a.Cell), 0f,
                      Quaternion.LookRotation(SideOf(a.Vertical, 1)), new Vector3(stretch, 1f, 1f));
            }

            // Piers and posts.
            for (int i = 0; i < KitPlan.Points.Count; i++)
            {
                CryptKitPoint p = KitPlan.Points[i];
                Place(p.Pier ? CryptPiece.Pier : CryptPiece.Post, i, (p.Pier ? "Kit_Pier_" : "Kit_Post_") + p.X + "_" + p.Y,
                      new LevelPoint(p.X * cs, p.Y * cs), 0f, Quaternion.identity, Vector3.one);
            }

            // Lanterns, on the faces of plain sections.
            CryptKit.Model wall = CryptKit.Load(CryptPiece.Wall);
            CryptKit.Model lantern = CryptKit.Load(CryptPiece.Lantern);
            bool reachesMinusZ = lantern != null && lantern.Bounds.center.z < 0f;
            var lightsByRoom = new Dictionary<int, int>();
            for (int i = 0; i < KitPlan.Lanterns.Count; i++)
            {
                CryptKitLantern l = KitPlan.Lanterns[i];
                CryptKitSection s = KitPlan.Sections[l.Section];
                Vector3 into = SideOf(s.Vertical, l.Side);
                // How far the face it hangs on is from the line: the wall model's depth on that side of its pivot.
                float face = wall == null ? Values.WallThickness * 0.5f : l.Side == s.Facing ? wall.Bounds.max.z : -wall.Bounds.min.z;
                LevelPoint middle = EdgeMiddle(s.Vertical, s.Line, s.Cell);
                LevelPoint at = new LevelPoint(middle.X + into.x * face, middle.Y + into.z * face);
                Quaternion turn = Quaternion.LookRotation(into);
                if (reachesMinusZ) turn *= Quaternion.Euler(0f, 180f, 0f);
                KitPiece piece = Place(CryptPiece.Lantern, i, "Kit_Lantern_" + EdgeName(s.Vertical, s.Line, s.Cell) + (l.Side > 0 ? "+" : "-"),
                                       at, v.LanternHeight, turn, Vector3.one);

                // Lights (optional): the first of each room's lanterns, as many as allowed, a little out from the wall.
                if (!v.LanternLights || piece == null) continue;
                lightsByRoom.TryGetValue(l.Room, out int lit);
                if (lit >= v.LightsPerRoom) continue;
                lightsByRoom[l.Room] = lit + 1;
                if (KitLights == null) KitLights = gameObject.AddComponent<CryptKitLights>();
                float reach = lantern != null ? lantern.Bounds.size.z : 0f;
                Vector3 lightAt = piece.Object.transform.position + into * (reach + v.LightOut);
                KitLights.Add(l.Room, lightAt, v, KitRoot);
            }

            // Floors: each kind's texture, a copy a cell, lined up with the grid (a room starts on a cell's edge).
            // The elite room's floor has its own faint tint (work order 017), its kind's texture.
            var floorMaterials = new Dictionary<(RoomKind, bool), Material>();
            var block = new MaterialPropertyBlock();
            for (int i = 0; i < _floorBoxes.Count && i < Layout.Rooms.Count; i++)
            {
                LevelRoom room = Layout.Rooms[i];
                if (!floorMaterials.TryGetValue((room.Kind, room.Elite), out Material m))
                {
                    floorMaterials[(room.Kind, room.Elite)] = m = CryptKit.MakeFloorMaterial(room.Kind, v, room.Elite);
                    _materials.Add(m);
                }
                Renderer r = _floorBoxes[i].GetComponent<Renderer>();
                r.sharedMaterial = m;
                float tile = v.FloorTile > 1e-3f ? v.FloorTile : cs;
                float w = room.Rect.Width * cs / tile, d = room.Rect.Height * cs / tile;
                bool uAlongX = CryptKit.TopFaceUAlongX(_floorBoxes[i].GetComponent<MeshFilter>().sharedMesh);
                block.Clear();
                block.SetVector("_BaseMap_ST", uAlongX ? new Vector4(w, d, 0f, 0f) : new Vector4(d, w, 0f, 0f));
                r.SetPropertyBlock(block);
            }

            // The boxes stop showing; their colliders, cover and shelter stay.
            foreach (GameObject box in _wallBoxes) box.GetComponent<Renderer>().enabled = false;
        }

        /// <summary>How much each arch is stretched along its wall, 1 for none (work order 016).</summary>
        public float ArchStretch { get; private set; } = 1f;

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
                        CryptKitValues kit = Values.Crypt ?? new CryptKitValues();
                        materials[prop.Model] = m = PropFactory.MakeMaterial(prop.Model, kit.Cyan, Values.PropRules.Glow);
                        _materials.Add(m);
                    }
                    string name = "Prop_" + room.Letter + room.Order + "_" + i + "_" + prop.Model;
                    GameObject go = PropFactory.Make(prop, ToWorld(prop.Position), transform, WallLayer, m, name, LowestPropCollider);
                    bool cover = prop.IsCover(Values.PropRules);
                    if (cover) go.AddComponent<Cover>(); // a tall prop stops shots; she shoots over a low one (Kristian, 2026-11-19)
                    // Work order 043: the altar, the grave marker and the broken pillar are low under the quick duck's sort.
                    if (cover) go.GetComponent<Cover>().LowUnderQuickDuck = PropSort.LowerUnderTheDuck(prop, Values.PropRules);
                    // She can take cover behind every prop, tall or low (Kristian, 2026-11-19, an experiment), but a slender
                    // one (work order 028, the candelabra): it hides nothing.
                    if (!prop.Slender) go.AddComponent<Shelter>();
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
            /// <summary>
            /// It stops shots: it is at least <see cref="PropValues.CoverHeight"/> tall and carries <see cref="Cover"/>. With
            /// the quick duck's switch on (work order 043) the three it sorts low stop nothing all the same
            /// (<see cref="Cover.Stops"/>).
            /// </summary>
            public bool IsCover;
        }

        // ---------------- Enemies ----------------

        /// <summary>
        /// What stands at each enemy point (work order 008): the group's enemies of its kind, each where the description
        /// puts it (work order 012: one on the point, a pack round it, a line side by side across it). Every kind has
        /// the floor's health for it (<see cref="LevelValues.HealthOf"/>), the act boss <see cref="LevelValues.BossHealth"/>;
        /// the practice room's enemies keep their own. Work order 030: what each deals is scaled by the floor
        /// (<see cref="LevelValues.DamageScaleOf"/>; an elite's own scale times it, the act boss not at all), and an elite
        /// takes no turns to attack.
        /// </summary>
        void PlaceEnemies(EnemyBrute template)
        {
            Enemy.Turns.Floor = Layout.Floor; // work order 030: the shared limit of the turns is by floor
            float floorScale = Values.DamageScaleOf(Layout.Floor); // work order 030, lever B
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
                        // Work order 023: a floor's enemies take turns to attack; an elite does not (work order 030).
                        enemy.TakesTurns = !group.Elite;
                        // Work order 023: a floor's brute has the floors' Shockwave reach, from a copy of the asset's numbers.
                        if (enemy is EnemyBrute brute) brute.FloorValues = Values.FloorBruteValues(template.Values);
                        // The elite (work order 017): its kind's health for the floor times the elite's share, and the rest.
                        if (group.Elite && !group.Boss)
                        {
                            EnemyFactory.MakeElite(enemy, Values.EliteHealthOf(group.Kind), Values.EliteDamageScale, Values.EliteSizeScale, Values.EliteStaggerShare);
                            if (Elite == null) Elite = enemy;
                        }
                        // Work order 030, lever B: what a floor's enemies deal rises with the floor; an elite's own scale
                        // multiplies with it; the act boss is not scaled.
                        if (!group.Boss && enemy.Kind != EnemyKind.Boss) enemy.DamageScale *= floorScale;
                        placed.Members.Add(enemy);
                        if (!_enemiesByRoom.TryGetValue(room.Order, out List<Enemy> list)) _enemiesByRoom[room.Order] = list = new List<Enemy>();
                        list.Add(enemy);
                        _enemies.Add(enemy);
                        if (group.Boss && Boss == null) Boss = enemy;
                        if (enemy is EnemyBoss boss)
                        {
                            // Work order 014: it wakes as she enters its room, and its packs come along the room's walls.
                            LevelRoom home = room;
                            boss.InRoom = p => RoomAt(p) == home;
                            boss.PackCaller = (b, count) => CallPack(b, home, count);
                        }
                    }
                    _groups.Add(placed);
                }
            }
        }

        /// <summary>
        /// The swarmers the act boss called (work order 014). They are among <see cref="Enemies"/>, and not among their
        /// room's: the floor is finished when the boss and the room's own enemies are dead, as before.
        /// </summary>
        public IReadOnlyList<Enemy> Called => _called;
        readonly List<Enemy> _called = new List<Enemy>();
        /// <summary>The called swarmers by the room they were called into, for the room's "enemies left" (work order 026).</summary>
        public IReadOnlyDictionary<int, List<Enemy>> CalledByRoom => _calledByRoom;
        readonly Dictionary<int, List<Enemy>> _calledByRoom = new Dictionary<int, List<Enemy>>();
        /// <summary>Raised for each swarmer the boss calls, as it is made: the run's record counts its death (work order 026).</summary>
        public event System.Action<Enemy> EnemyCalled;

        /// <summary>
        /// A pack the boss calls (work order 014): <paramref name="count"/> swarmers of the floor's health, at places
        /// spread round its room along the walls (<see cref="BossTactics.WallPlaces"/>: the level's rules for an enemy's
        /// place, a step in from the clearance it keeps from walls), on the NavMesh, so not in a prop. Each later call
        /// takes the places half a step round from the one before. They stay dead when killed.
        /// </summary>
        List<Enemy> CallPack(EnemyBoss boss, LevelRoom room, int count)
        {
            var made = new List<Enemy>();
            if (boss.Template == null || count <= 0) return made;
            var places = new List<Vector3>();
            foreach (LevelPoint p in BossTactics.WallPlaces(Layout, room, Values, Values.WallClearance + 0.5, Values.PointStep))
                if (NavMesh.SamplePosition(ToWorld(p), out NavMeshHit hit, 0.3f, NavMesh.AllAreas)) places.Add(hit.position);
            if (places.Count == 0) return made;
            int shift = Mathf.Max(0, boss.PacksCalled - 1) * places.Count / (2 * count);
            var turned = new List<Vector3>(places.Count);
            for (int i = 0; i < places.Count; i++) turned.Add(places[(i + shift) % places.Count]);
            List<Vector3> picked = BossTactics.Spread(turned, count);
            for (int i = 0; i < picked.Count; i++)
            {
                Enemy e = EnemyFactory.Make(EnemyKind.Swarmer, picked[i], transform, boss.Template,
                                            "Called_" + room.Letter + room.Order + "_" + boss.PacksCalled + "_" + i);
                if (e == null) continue;
                e.HealthOverride = Values.HealthOf(EnemyKind.Swarmer);
                e.TakesTurns = true; // light: never held back, but one of the floor's (work order 023)
                e.DamageScale = Values.DamageScaleOf(Layout.Floor); // work order 030, lever B: as the floor's own
                _enemies.Add(e);
                _called.Add(e);
                if (!_calledByRoom.TryGetValue(room.Order, out List<Enemy> inRoom)) _calledByRoom[room.Order] = inRoom = new List<Enemy>();
                inRoom.Add(e);
                EnemyCalled?.Invoke(e);
                made.Add(e);
            }
            return made;
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

        /// <summary>The enemies of the elite room (work order 017), the elite among them; empty without one.</summary>
        public IReadOnlyList<Enemy> EliteRoomEnemies
        {
            get
            {
                LevelRoom room = Layout.EliteRoom;
                return room != null && _enemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list : (IReadOnlyList<Enemy>)System.Array.Empty<Enemy>();
            }
        }

        /// <summary>True once every enemy of the elite room is dead (work order 017); false while there is none to kill.</summary>
        public bool EliteRoomCleared
        {
            get
            {
                IReadOnlyList<Enemy> list = EliteRoomEnemies;
                if (list.Count == 0) return false;
                for (int i = 0; i < list.Count; i++)
                    if (list[i] != null && !list[i].IsDead) return false;
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
