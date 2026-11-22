using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The room as a cave on a generated floor (work order 076; Kristian, 2026-11-21 in the evening: "jeg liker heller ikke at
    /// man ser "utenfor" mappen ... jeg liker heller ikke at vi ser andre zoner når vi er i et rom. veggene må gå helt opp og
    /// føles at vi er i en hule"). With <see cref="LevelValues.Cave"/> on it sits on the level's root and goes with it; off, it
    /// is not there and the floor looks as before. Its numbers are <see cref="CaveValues"/>.
    ///
    /// 1. Nothing outside the room she is in (<see cref="CaveValues.HideOthers"/>): only the room or passage she is in and the
    /// ones a door from it are drawn (<see cref="CaveSight.Shown"/>), and the camera's background is the earth's black
    /// (<see cref="CaveValues.Earth"/>), put back when the floor goes. Every renderer of the floor and of its mood belongs to the
    /// rooms and passages it stands in (a floor box and a room's mood to their room; a wall piece, an arch, a pier or a sign on a
    /// line between two to both); an enemy to the one it stands in now, looked at every frame. What belongs to none drawn is not
    /// drawn: <see cref="Renderer.forceRenderingOff"/>, which nothing else in the game touches, so no component, object, light,
    /// collider, NavMesh or enemy's logic changes, and no particle system is switched off and on (work order 061's assert came
    /// from systems woken on an active object). Her own light and dust go with her and are always drawn.
    ///
    /// 2. The walls go all the way up (<see cref="CaveValues.TallWalls"/>): over every full wall of the kit that
    /// <see cref="CaveRise"/> lets rise, a face of rough rock from the wall's top to its height, facing the room, flat-shaded on
    /// the props' material (Simple Lit), its colour darkening to black at its top so the wall fades up into the earth. One
    /// mesh and one renderer for each room or passage, no collider, on the kit's layer, casting no shadow: the NavMesh,
    /// the colliders, cover and shelter are what they were.
    ///
    /// Its frame allocates nothing.
    /// </summary>
    public sealed class CaveView : MonoBehaviour
    {
        public const string RockRootName = "CaveRock";

        GeneratedLevel _level;
        LevelValues _values;
        bool[] _shown;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;
        int _moodBuilds = -1;
        bool _hiding;

        // The renderers it shows and hides, each with the rooms it belongs to (a run in _owners from _from, _count long).
        readonly List<Renderer> _renderers = new List<Renderer>();
        readonly List<int> _from = new List<int>(), _count = new List<int>(), _owners = new List<int>();
        int _levelRenderers; // the first this many are the level's own; the mood's after them (collected again when it is rebuilt)

        // The enemies, by their place in the level's list: -1 not looked at yet, 0 drawn, 1 not.
        readonly List<sbyte> _enemyHidden = new List<sbyte>();
        readonly List<int> _enemyRoom = new List<int>();
        readonly List<Renderer> _buffer = new List<Renderer>();

        // The rock.
        Transform _rockRoot;
        readonly List<MeshRenderer> _rock = new List<MeshRenderer>();
        readonly List<Object> _owned = new List<Object>();

        // The camera's background.
        Camera _camera;
        Color _background;
        bool _claimed;
        Rgb _earthApplied;

        static readonly CaveValues s_defaults = new CaveValues();
        CaveValues V => _values?.CaveRules ?? s_defaults;

        /// <summary>The look it was built with (work order 083): 076's rock, or the dark one.</summary>
        public CaveLook LookBuilt { get; private set; }

        /// <summary>The room or passage she was in at the last frame, by its place in the run; -1 before she was in one.</summary>
        public int Here { get; private set; } = -1;
        /// <summary>The rise of the walls the rock was built from; null with <see cref="CaveValues.TallWalls"/> off or without the kit.</summary>
        public CaveRise Rise { get; private set; }
        /// <summary>The rock's renderers, one for each room or passage that has rock.</summary>
        public IReadOnlyList<MeshRenderer> Rock => _rock;
        /// <summary>How many faces of rock were built (a full wall's side toward a room).</summary>
        public int RockFaces { get; private set; }
        /// <summary>The renderers it shows and hides (the level's and the mood's; not the enemies').</summary>
        public int Tracked => _renderers.Count;
        /// <summary>The kit's wall top the rock starts from, in metres (the wall model's height).</summary>
        public float WallTop { get; private set; }

        /// <summary>Is the room or passage (by its place in the run) drawn now?</summary>
        public bool IsShown(int order) => !_hiding || (order >= 0 && order < _shown.Length && _shown[order]);

        /// <summary>The rooms a renderer it tracks belongs to (for tests); empty for one it does not track.</summary>
        public List<int> OwnersOf(Renderer r)
        {
            var list = new List<int>();
            int i = _renderers.IndexOf(r);
            if (i < 0) return list;
            for (int k = 0; k < _count[i]; k++) list.Add(_owners[_from[i] + k]);
            return list;
        }

        /// <summary>Puts the cave on a built floor: the rock first (it is the floor's), then what it shows and hides; her start room shown.</summary>
        public static CaveView Add(GeneratedLevel level)
        {
            var cave = level.gameObject.AddComponent<CaveView>();
            cave._level = level;
            cave._values = level.Values;
            cave._shown = new bool[level.Layout.Rooms.Count];
            // Work order 083: the look. Rock is 076's (its faces and tops by their own switches); Dark the far walls tall, the
            // near walls a lip, a black cap on every wall and the picture falling to black at its edges.
            cave.LookBuilt = cave.V.Look;
            if (level.KitPlan != null)
            {
                if (cave.LookBuilt == CaveLook.Rock && (cave.V.TallWalls || cave.V.RockLids)) cave.BuildRock();
                else if (cave.LookBuilt == CaveLook.Dark) cave.BuildDark();
            }
            cave.CollectLevel();
            cave.CollectMood();
            cave.Claim();
            cave.Look(level.Layout.Rooms.Count > 0 ? 0 : -1);
            return cave;
        }

        // ---------------- Who owns what ----------------

        void Track(Renderer r, int a, int b = -1, int c = -1, int d = -1)
        {
            _renderers.Add(r);
            _from.Add(_owners.Count);
            int n = 0;
            foreach (int o in new[] { a, b, c, d })
                if (o >= 0 && !_owners.GetRange(_owners.Count - n, n).Contains(o))
                {
                    _owners.Add(o);
                    n++;
                }
            _count.Add(n);
        }

        /// <summary>The rooms whose rectangle, a little grown, holds the point (at most four: a corner of four).</summary>
        void OwnersAt(Vector3 world, out int a, out int b, out int c, out int d)
        {
            a = b = c = d = -1;
            LevelPoint p = _level.ToLevel(world);
            double cs = _level.Layout.CellSize, grow = 0.3;
            foreach (LevelRoom r in _level.Layout.Rooms)
            {
                if (p.X < r.Rect.X * cs - grow || p.X > r.Rect.Right * cs + grow || p.Y < r.Rect.Y * cs - grow || p.Y > r.Rect.Top * cs + grow) continue;
                if (a < 0) a = r.Order;
                else if (b < 0) b = r.Order;
                else if (c < 0) c = r.Order;
                else if (d < 0) d = r.Order;
            }
        }

        /// <summary>The rooms whose rectangle, a little grown, the renderer's ground footprint overlaps (a long grey wall box of the kit off).</summary>
        void OwnersOver(Bounds bounds, List<int> into)
        {
            into.Clear();
            double cs = _level.Layout.CellSize, grow = 0.3;
            LevelPoint lo = _level.ToLevel(bounds.min), hi = _level.ToLevel(bounds.max);
            foreach (LevelRoom r in _level.Layout.Rooms)
                if (lo.X < r.Rect.Right * cs + grow && hi.X > r.Rect.X * cs - grow && lo.Y < r.Rect.Top * cs + grow && hi.Y > r.Rect.Y * cs - grow) into.Add(r.Order);
        }

        void CollectLevel()
        {
            var floors = new HashSet<Renderer>();
            for (int i = 0; i < _level.FloorBoxes.Count; i++)
            {
                Renderer r = _level.FloorBoxes[i].GetComponent<Renderer>();
                if (r == null) continue;
                floors.Add(r);
                Track(r, _level.FloorRoomOf(i).Order);
            }
            var rock = new HashSet<Renderer>(_rock);
            for (int i = 0; i < _rock.Count; i++) Track(_rock[i], RockOwner[i]);
            for (int i = 0; i < _lids.Count; i++)
            {
                rock.Add(_lids[i]);
                int[] o = _lidOwners[i];
                Track(_lids[i], o[0], o.Length > 1 ? o[1] : -1, o.Length > 2 ? o[2] : -1, o.Length > 3 ? o[3] : -1);
            }
            for (int i = 0; i < _dark.Count; i++) // work order 083: the veils and the caps, each to the rooms it stands between
            {
                rock.Add(_dark[i]);
                int[] o = _darkOwners[i];
                Track(_dark[i], o.Length > 0 ? o[0] : -1, o.Length > 1 ? o[1] : -1, o.Length > 2 ? o[2] : -1, o.Length > 3 ? o[3] : -1);
            }
            var over = new List<int>();
            float big = _level.Layout.CellSize * 1.5f;
            foreach (Renderer r in _level.GetComponentsInChildren<Renderer>(true))
            {
                if (floors.Contains(r) || rock.Contains(r)) continue;
                if (r.GetComponentInParent<Enemy>(true) != null) continue; // an enemy is looked at where it stands, every frame
                Bounds b = r.bounds;
                if (b.size.x > big || b.size.z > big)
                {
                    OwnersOver(b, over);
                    _renderers.Add(r);
                    _from.Add(_owners.Count);
                    _owners.AddRange(over);
                    _count.Add(over.Count);
                    continue;
                }
                OwnersAt(b.center, out int a, out int bb, out int c, out int d);
                Track(r, a, bb, c, d);
            }
            _levelRenderers = _renderers.Count;
        }

        /// <summary>The mood's renderers: each room's parts to their room; her light and dust go with her and are not tracked.</summary>
        void CollectMood()
        {
            if (_renderers.Count > _levelRenderers)
            {
                _renderers.RemoveRange(_levelRenderers, _renderers.Count - _levelRenderers);
                int keep = _from.Count > _levelRenderers ? _from[_levelRenderers] : _owners.Count;
                _owners.RemoveRange(keep, _owners.Count - keep);
                _from.RemoveRange(_levelRenderers, _from.Count - _levelRenderers);
                _count.RemoveRange(_levelRenderers, _count.Count - _levelRenderers);
            }
            RoomMood mood = _level.Mood;
            _moodBuilds = mood != null ? mood.Builds : -1;
            if (mood == null) return;
            foreach (RoomMood.RoomParts p in mood.Rooms)
                if (p.Root != null)
                    foreach (Renderer r in p.Root.GetComponentsInChildren<Renderer>(true)) Track(r, p.Order);
        }

        // ---------------- Every frame ----------------

        void Update() => Tick();

        /// <summary>Looks at where she and every enemy stand and draws as the rule says; allocates nothing. For tests too.</summary>
        public void Tick()
        {
            if (_level == null || _values == null) return;
            bool hide = _values.Cave && V.HideOthers;
            if (!hide)
            {
                if (_hiding) ShowAll();
                if (_claimed) Release();
                return;
            }
            if (!_claimed) Claim();
            ApplyEarth();
            if (_level.Mood != null && _level.Mood.Builds != _moodBuilds)
            {
                CollectMood();
                Apply();
            }
            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            LevelRoom room = _player != null ? _level.RoomAt(_player.Position) : null;
            if (room != null && (room.Order != Here || !_hiding)) Look(room.Order);
            Enemies(false);
        }

        /// <summary>She is in this room or passage: what is drawn follows (for tests too).</summary>
        public void Look(int here)
        {
            if (here < 0) return;
            Here = here;
            CaveSight.Shown(_level.Layout, here, _shown);
            if (!(_values.Cave && V.HideOthers)) return;
            _hiding = true;
            Apply();
            Enemies(true);
        }

        void Apply()
        {
            for (int i = 0; i < _renderers.Count; i++)
            {
                Renderer r = _renderers[i];
                if (r == null) continue;
                bool on = _count[i] == 0; // belonging to no room: drawn as before
                for (int k = 0; k < _count[i] && !on; k++) on = _shown[_owners[_from[i] + k]];
                r.forceRenderingOff = !on;
            }
        }

        /// <summary>Each enemy drawn when the room or passage it stands in is (where it stands in none, as it was).</summary>
        void Enemies(bool all)
        {
            IReadOnlyList<Enemy> enemies = _level.Enemies;
            while (_enemyHidden.Count < enemies.Count)
            {
                _enemyHidden.Add(-1);
                _enemyRoom.Add(-1);
            }
            for (int i = 0; i < enemies.Count; i++)
            {
                Enemy e = enemies[i];
                if (e == null) continue;
                LevelRoom room = _level.RoomAt(e.transform.position);
                int order = room != null ? room.Order : _enemyRoom[i];
                _enemyRoom[i] = order;
                bool hidden = _hiding && order >= 0 && !_shown[order];
                sbyte now = hidden ? (sbyte)1 : (sbyte)0;
                if (!all && now == _enemyHidden[i]) continue;
                _enemyHidden[i] = now;
                e.GetComponentsInChildren(true, _buffer);
                for (int k = 0; k < _buffer.Count; k++) _buffer[k].forceRenderingOff = hidden;
            }
        }

        /// <summary>Is the enemy (by its place in the level's list) drawn now?</summary>
        public bool EnemyShown(int index) => index < 0 || index >= _enemyHidden.Count || _enemyHidden[index] != 1;

        void ShowAll()
        {
            _hiding = false;
            for (int i = 0; i < _renderers.Count; i++)
                if (_renderers[i] != null) _renderers[i].forceRenderingOff = false;
            Enemies(true);
        }

        // ---------------- The earth: the camera's background ----------------

        void Claim()
        {
            if (_claimed || !(_values.Cave && V.HideOthers)) return;
            _camera = Camera.main;
            if (_camera == null) return;
            _claimed = true;
            _background = _camera.backgroundColor;
            // The carved cells' dark tops (work order 051: "as dark as the outside of the floor") take the earth's colour too:
            // round 1's pictures showed them grey-blue against the black, as pits.
            // Round 2's pictures: a black URP Lit top still showed slate-blue (its smoothness reflects the scene), so the tops get
            // a Simple Lit material of the earth's colour while the cave is on, and their own back when it goes.
            if (_level.KitPlan != null && _level.SolidCaps.Count > 0)
            {
                if (_capMaterial == null && LookBuilt == CaveLook.Dark && V.BlockTop > 0f)
                {
                    // Work order 083, round 1's pictures: the black tops read as pits; in the dark look they are the kit's stone, dark.
                    _capMaterial = CryptKit.MakeMaterial(CryptPiece.Wall, _values.Crypt ?? new CryptKitValues());
                    _capMaterial.name = "Cave_BlockTop";
                    _capMaterial.DisableKeyword("_EMISSION");
                    if (_capMaterial.HasProperty("_EmissionColor")) _capMaterial.SetColor("_EmissionColor", Color.black);
                    Color stone = Color.white * Mathf.Clamp01(V.BlockTop);
                    stone.a = 1f;
                    _capMaterial.color = stone;
                    if (_capMaterial.HasProperty("_BaseColor")) _capMaterial.SetColor("_BaseColor", stone);
                    _capsStone = true;
                    _owned.Add(_capMaterial);
                }
                if (_capMaterial == null)
                {
                    _capMaterial = MoodShapes.MakeLit(null);
                    _capMaterial.name = "Cave_Earth";
                    _owned.Add(_capMaterial);
                }
                _capWas.Clear();
                foreach (GameObject cap in _level.SolidCaps)
                {
                    Renderer r = cap.GetComponent<Renderer>();
                    _capWas.Add(r.sharedMaterial);
                    r.sharedMaterial = _capMaterial;
                }
            }
            _earthApplied = new Rgb(-1f, -1f, -1f);
            ApplyEarth();
            if (LookBuilt == CaveLook.Dark) MakeFalloff(); // work order 083
        }

        Material _capMaterial;
        readonly List<Material> _capWas = new List<Material>();

        bool _capsStone; // work order 083: the dark look's block tops keep their stone colour

        void SetCaps(Color c)
        {
            if (_capMaterial == null || _capsStone) return;
            _capMaterial.color = c;
            if (_capMaterial.HasProperty("_BaseColor")) _capMaterial.SetColor("_BaseColor", c);
        }

        void PutCapsBack()
        {
            for (int i = 0; i < _capWas.Count && i < _level.SolidCaps.Count; i++)
                if (_level.SolidCaps[i] != null) _level.SolidCaps[i].GetComponent<Renderer>().sharedMaterial = _capWas[i];
            _capWas.Clear();
        }

        void ApplyEarth()
        {
            if (!_claimed || _camera == null) return;
            Rgb e = V.Earth;
            if (e.R == _earthApplied.R && e.G == _earthApplied.G && e.B == _earthApplied.B) return;
            _earthApplied = e;
            _camera.backgroundColor = new Color(e.R, e.G, e.B, 1f);
            SetCaps(new Color(e.R, e.G, e.B, 1f));
        }

        void Release()
        {
            if (!_claimed) return;
            _claimed = false;
            if (_camera != null) _camera.backgroundColor = _background;
            if (_level != null) PutCapsBack();
            DropFalloff();
            _camera = null;
        }

        void OnEnable()
        {
            if (_level != null) Claim();
        }

        void OnDisable() => Release();

        void OnDestroy()
        {
            Release();
            foreach (Object o in _owned)
                if (o != null) Destroy(o);
            _owned.Clear();
        }

        // ---------------- The rock ----------------

        /// <summary>The room each rock renderer (by its place in <see cref="Rock"/>) belongs to.</summary>
        public readonly List<int> RockOwner = new List<int>();

        static Vector3 SideOf(bool vertical, int side) => vertical ? new Vector3(side, 0f, 0f) : new Vector3(0f, 0f, side);

        sealed class RockBuild
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Vector3> N = new List<Vector3>();
            public readonly List<Vector2> U = new List<Vector2>();
            public readonly List<int> T = new List<int>();

            public void Tri(Vector3 a, Vector3 b, Vector3 c, float va, float vb, float vc, Vector3 into)
            {
                Vector3 n = Vector3.Cross(b - a, c - a);
                if (n.sqrMagnitude < 1e-10f) return;
                if (Vector3.Dot(n, into) < 0f)
                {
                    Vector3 t = b; b = c; c = t;
                    float tv = vb; vb = vc; vc = tv;
                    n = -n;
                }
                n.Normalize();
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c);
                N.Add(n); N.Add(n); N.Add(n);
                U.Add(new Vector2(0.5f, va)); U.Add(new Vector2(0.5f, vb)); U.Add(new Vector2(0.5f, vc));
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
            }
        }

        static float Hash(uint a, uint b, uint c, uint d)
        {
            uint h = a * 0x9E3779B1u ^ (b + 0x7F4A7C15u) * 0x85EBCA77u ^ (c + 0x165667B1u) * 0xC2B2AE3Du ^ d * 0x27D4EB2Fu;
            h ^= h >> 15; h *= 0x2C1B3C6Du; h ^= h >> 12; h *= 0x297A2D39u; h ^= h >> 15;
            return (h & 0xFFFFFF) / (float)0xFFFFFF;
        }

        /// <summary>The rock's colour up its height: <see cref="CaveValues.Rock"/> at its foot, black at its top (the caller destroys it).</summary>
        static Texture2D MakeFade(CaveValues v)
        {
            const int n = 64;
            var t = new Texture2D(2, n, TextureFormat.RGBA32, false) { name = "Cave_RockFade", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var px = new Color[2 * n];
            for (int y = 0; y < n; y++)
            {
                float k = Mathf.Pow(1f - y / (float)(n - 1), Mathf.Max(0.1f, v.RockFade));
                px[y * 2] = px[y * 2 + 1] = new Color(v.Rock.R * k, v.Rock.G * k, v.Rock.B * k, 1f);
            }
            t.SetPixels(px);
            t.Apply(false, true);
            return t;
        }

        void BuildRock()
        {
            CaveValues v = V;
            LevelLayout layout = _level.Layout;
            CryptKit.Model wall = CryptKit.Load(CryptPiece.Wall), low = CryptKit.Load(CryptPiece.LowWall);
            WallTop = wall != null ? wall.Bounds.max.y : _values.WallHeight;
            float lowTop = low != null ? low.Bounds.max.y : _values.NearWallHeight;
            CameraRig rig = FindAnyObjectByType<CameraRig>();
            FeelValues feel = rig != null && rig.Settings != null ? rig.Settings.Values : new FeelValues();
            Rise = CaveRise.Make(layout, _level.KitPlan, v, WallTop, lowTop, feel.CameraPitch, feel.CameraDistance);

            float cs = layout.CellSize, from = WallTop - 0.2f;
            var builds = new Dictionary<int, RockBuild>();
            uint seed = (uint)(layout.Seed ^ (layout.Seed >> 32)) ^ (uint)layout.Floor * 0x9E3779B1u;
            if (v.RockLids) BuildLids(seed);
            foreach (CryptKitSection s in _level.KitPlan.Sections)
            {
                if (s.Low || !v.TallWalls) continue;
                float top = Rise.RiseOf(s.Vertical, s.Line, s.Cell);
                if (top < WallTop + v.MinRise) continue;
                foreach (int side in new[] { -1, 1 })
                {
                    int room = s.RoomOn(side);
                    if (room < 0) continue;
                    if (!builds.TryGetValue(room, out RockBuild b)) builds[room] = b = new RockBuild();
                    // The face it stands on: the wall model's depth on that side of its line (as the lanterns hang).
                    float depth = wall == null ? _values.WallThickness * 0.5f : side == s.Facing ? wall.Bounds.max.z : -wall.Bounds.min.z;
                    Vector3 into = SideOf(s.Vertical, side);
                    Vector3 along = s.Vertical ? Vector3.forward : Vector3.right;
                    LevelPoint start = s.Vertical ? new LevelPoint(s.Line * cs, s.Cell * cs) : new LevelPoint(s.Cell * cs, s.Line * cs);
                    Vector3 o = _level.ToWorld(start) + into * depth - along * (depth + 0.05f);
                    float len = cs + 2f * (depth + 0.05f);
                    int nu = Mathf.Max(1, Mathf.RoundToInt(len / Mathf.Max(0.25f, v.RockStep)));
                    int nv = Mathf.Max(1, Mathf.RoundToInt((top - from) / Mathf.Max(0.25f, v.RockStep)));
                    var grid = new Vector3[(nu + 1) * (nv + 1)];
                    uint key = (uint)(s.Vertical ? 1 : 2) * 0x1000193u ^ (uint)s.Line * 0x100u ^ (uint)s.Cell ^ (uint)(side + 1) << 24;
                    for (int j = 0; j <= nv; j++)
                        for (int i = 0; i <= nu; i++)
                        {
                            float out1 = j == 0 ? 0f : v.RockJitter * Hash(seed, key, (uint)i, (uint)j);
                            float slide = i == 0 || i == nu || j == 0 || j == nv ? 0f : (Hash(seed ^ 0x5bd1e995u, key, (uint)i, (uint)j) - 0.5f) * 0.5f * len / nu;
                            float rise = j == 0 || j == nv ? 0f : (Hash(seed ^ 0x1b873593u, key, (uint)i, (uint)j) - 0.5f) * 0.4f * (top - from) / nv;
                            grid[j * (nu + 1) + i] = o + along * (len * i / nu + slide) + Vector3.up * (from + (top - from) * j / nv + rise) + into * out1;
                        }
                    for (int j = 0; j < nv; j++)
                        for (int i = 0; i < nu; i++)
                        {
                            Vector3 a = grid[j * (nu + 1) + i], bb = grid[j * (nu + 1) + i + 1], c = grid[(j + 1) * (nu + 1) + i + 1], d = grid[(j + 1) * (nu + 1) + i];
                            float va = (a.y - from) / (top - from), vb = (bb.y - from) / (top - from), vc = (c.y - from) / (top - from), vd = (d.y - from) / (top - from);
                            if (((i + j) & 1) == 0)
                            {
                                b.Tri(a, bb, c, va, vb, vc, into);
                                b.Tri(a, c, d, va, vc, vd, into);
                            }
                            else
                            {
                                b.Tri(a, bb, d, va, vb, vd, into);
                                b.Tri(bb, c, d, vb, vc, vd, into);
                            }
                        }
                    RockFaces++;
                }
            }
            if (builds.Count == 0) return;

            Material material = RockMaterial();
            Transform root = RockRoot();
            foreach (KeyValuePair<int, RockBuild> pair in builds)
            {
                LevelRoom room = layout.Rooms[pair.Key];
                var mesh = new Mesh { name = "Cave_Rock_" + room.Letter + room.Order };
                if (pair.Value.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(pair.Value.V);
                mesh.SetNormals(pair.Value.N);
                mesh.SetUVs(0, pair.Value.U);
                mesh.SetTriangles(pair.Value.T, 0);
                mesh.RecalculateBounds();
                _owned.Add(mesh);
                var go = new GameObject("CaveRock_" + room.Letter + room.Order) { layer = CryptKit.Layer };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                _rock.Add(r);
                RockOwner.Add(pair.Key);
            }
        }

        // ---------------- The rock round the room: its tops ----------------

        Material _rockMaterial;

        /// <summary>The rock's material: Simple Lit on its fade (shared by the faces and the tops).</summary>
        Material RockMaterial()
        {
            if (_rockMaterial != null) return _rockMaterial;
            Texture2D fade = MakeFade(V);
            _owned.Add(fade);
            _rockMaterial = MoodShapes.MakeLit(fade);
            _rockMaterial.name = "Cave_Rock";
            _owned.Add(_rockMaterial);
            return _rockMaterial;
        }

        Transform RockRoot()
        {
            if (_rockRoot != null) return _rockRoot;
            var root = new GameObject(RockRootName) { layer = CryptKit.Layer };
            root.transform.SetParent(transform, false);
            _rockRoot = root.transform;
            return _rockRoot;
        }

        readonly List<MeshRenderer> _lids = new List<MeshRenderer>();
        readonly List<int[]> _lidOwners = new List<int[]>();

        /// <summary>The rock tops' renderers, one for each set of rooms and passages near them (CaveValues.RockLids).</summary>
        public IReadOnlyList<MeshRenderer> Lids => _lids;
        /// <summary>How many cells have a rock top.</summary>
        public int LidCells { get; private set; }

        /// <summary>
        /// The rock round the room (the producer on round 2's pictures: "a room cut out of rock"): over every cell the rule gives
        /// a top (<see cref="CaveRise.LidOf"/>), a rough flat rock at its height, its facets' corners sunk a little at random, its
        /// colour darkening to the earth's black as it goes from the nearest floor; where it stands higher than its neighbour
        /// (a lower top, a cut one, or a wall lower than it), a face down to it. Each top is drawn while any room or passage
        /// within its reach is (the nearest four), grouped by those into one mesh each.
        /// </summary>
        void BuildLids(uint seed)
        {
            CaveValues v = V;
            CaveRise rise = Rise;
            LevelLayout layout = _level.Layout;
            float cs = layout.CellSize;
            int reach = Mathf.Max(1, v.LidReach);
            const int sub = 3;
            float ground = _level.ToWorld(new LevelPoint(0, 0)).y;
            var groups = new Dictionary<long, RockBuild>();
            var owners = new Dictionary<long, int[]>();
            var near = new List<(int space, int d)>();

            float Fade(float px, float py)
            {
                // From the nearest open floor cell, in metres, over the reach: the top's shade at the wall to black.
                int cx = Mathf.FloorToInt(px / cs), cy = Mathf.FloorToInt(py / cs);
                float best = float.MaxValue;
                for (int dy = -reach - 1; dy <= reach + 1; dy++)
                    for (int dx = -reach - 1; dx <= reach + 1; dx++)
                    {
                        int x = cx + dx, y = cy + dy;
                        if (rise.SpaceAt(x, y) < 0) continue;
                        float ex = Mathf.Max(0f, Mathf.Max(x * cs - px, px - (x + 1) * cs));
                        float ey = Mathf.Max(0f, Mathf.Max(y * cs - py, py - (y + 1) * cs));
                        best = Mathf.Min(best, Mathf.Sqrt(ex * ex + ey * ey));
                    }
                float k = Mathf.Clamp01(best / (reach * cs));
                return Mathf.Lerp(v.LidShade, 1f, k);
            }

            float Sink(int gx, int gy) => v.LidJitter * Hash(seed ^ 0x2545F491u, (uint)gx, (uint)gy, 7u);

            bool Within(int x, int y)
            {
                for (int dy = -reach; dy <= reach; dy++)
                    for (int dx = -reach; dx <= reach; dx++)
                        if (rise.SpaceAt(x + dx, y + dy) >= 0) return true;
                return false;
            }

            for (int y = rise.Y0; y < rise.Y0 + rise.Ny; y++)
                for (int x = rise.X0; x < rise.X0 + rise.Nx; x++)
                {
                    float top = rise.LidOf(x, y);
                    if (top <= 0f) continue;
                    top += 0.02f; // level with the walls' tops
                    // Over a carved cell walled in on every side, above its own dark top (at LevelValues.WallHeight) with every
                    // sunk corner: the final pictures showed the tops still black where that top stood over the rock.
                    if (rise.IsCapped(x, y)) top = Mathf.Max(top, _values.WallHeight + 0.02f) + v.LidJitter;
                    // Its rooms and passages: those within the reach, the nearest four.
                    near.Clear();
                    for (int dy = -reach; dy <= reach; dy++)
                        for (int dx = -reach; dx <= reach; dx++)
                        {
                            int s = rise.SpaceAt(x + dx, y + dy);
                            if (s < 0) continue;
                            int d = Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dy));
                            int at = near.FindIndex(n => n.space == s);
                            if (at < 0) near.Add((s, d));
                            else if (d < near[at].d) near[at] = (s, d);
                        }
                    if (near.Count == 0) continue;
                    near.Sort((p, q) => p.d != q.d ? p.d.CompareTo(q.d) : p.space.CompareTo(q.space));
                    int n4 = Mathf.Min(4, near.Count);
                    var set = new int[n4];
                    for (int i = 0; i < n4; i++) set[i] = near[i].space;
                    System.Array.Sort(set);
                    long key = 0;
                    for (int i = 0; i < n4; i++) key = key * 1024 + set[i] + 1;
                    if (!groups.TryGetValue(key, out RockBuild b))
                    {
                        groups[key] = b = new RockBuild();
                        owners[key] = set;
                    }
                    LidCells++;

                    // The top: sub by sub facets, corners sunk by a hash of their place on the floor (neighbours meet).
                    var corner = new Vector3[(sub + 1) * (sub + 1)];
                    var shade = new float[corner.Length];
                    for (int j = 0; j <= sub; j++)
                        for (int i = 0; i <= sub; i++)
                        {
                            float px = (x + i / (float)sub) * cs, py = (y + j / (float)sub) * cs;
                            corner[j * (sub + 1) + i] = _level.ToWorld(new LevelPoint(px, py)) + Vector3.up * (top - Sink(x * sub + i, y * sub + j));
                            shade[j * (sub + 1) + i] = Fade(px, py);
                        }
                    for (int j = 0; j < sub; j++)
                        for (int i = 0; i < sub; i++)
                        {
                            int a = j * (sub + 1) + i, bb = a + 1, c = a + sub + 2, d = a + sub + 1;
                            if (((i + j + x + y) & 1) == 0)
                            {
                                b.Tri(corner[a], corner[bb], corner[c], shade[a], shade[bb], shade[c], Vector3.up);
                                b.Tri(corner[a], corner[c], corner[d], shade[a], shade[c], shade[d], Vector3.up);
                            }
                            else
                            {
                                b.Tri(corner[a], corner[bb], corner[d], shade[a], shade[bb], shade[d], Vector3.up);
                                b.Tri(corner[bb], corner[c], corner[d], shade[bb], shade[c], shade[d], Vector3.up);
                            }
                        }

                    // A face down to each lower neighbour: a lower top, a cut cell within the reach (to the ground), a wall lower than it.
                    for (int side = 0; side < 4; side++)
                    {
                        int nx = x + (side == 0 ? 1 : side == 1 ? -1 : 0), ny = y + (side == 2 ? 1 : side == 3 ? -1 : 0);
                        float below;
                        if (rise.SpaceAt(nx, ny) >= 0)
                        {
                            bool vertical = side < 2;
                            int line = side == 0 ? x + 1 : side == 1 ? x : side == 2 ? y + 1 : y;
                            below = rise.TodayOf(vertical, line, vertical ? y : x);
                        }
                        else
                        {
                            float other = rise.LidOf(nx, ny);
                            if (other > 0f) below = other + 0.02f;
                            else if (Within(nx, ny)) below = 0f;
                            else continue; // past the reach: the earth's black
                        }
                        if (below >= top - 0.05f) continue;
                        Vector3 outward = side == 0 ? Vector3.right : side == 1 ? Vector3.left : side == 2 ? Vector3.forward : Vector3.back;
                        for (int k = 0; k < sub; k++)
                        {
                            int i0, i1;
                            if (side == 0) { i0 = k * (sub + 1) + sub; i1 = (k + 1) * (sub + 1) + sub; }
                            else if (side == 1) { i0 = k * (sub + 1); i1 = (k + 1) * (sub + 1); }
                            else if (side == 2) { i0 = sub * (sub + 1) + k; i1 = i0 + 1; }
                            else { i0 = k; i1 = k + 1; }
                            Vector3 t0 = corner[i0], t1 = corner[i1];
                            Vector3 b0 = new Vector3(t0.x, ground + below, t0.z), b1 = new Vector3(t1.x, ground + below, t1.z);
                            float s0 = shade[i0], s1 = shade[i1];
                            float sb0 = Mathf.Min(1f, s0 + 0.25f), sb1 = Mathf.Min(1f, s1 + 0.25f);
                            b.Tri(t0, t1, b1, s0, s1, sb1, outward);
                            b.Tri(t0, b1, b0, s0, sb1, sb0, outward);
                        }
                    }
                }
            if (groups.Count == 0) return;

            Material material = RockMaterial();
            Transform root = RockRoot();
            int g = 0;
            foreach (KeyValuePair<long, RockBuild> pair in groups)
            {
                var mesh = new Mesh { name = "Cave_RockTop_" + g };
                if (pair.Value.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(pair.Value.V);
                mesh.SetNormals(pair.Value.N);
                mesh.SetUVs(0, pair.Value.U);
                mesh.SetTriangles(pair.Value.T, 0);
                mesh.RecalculateBounds();
                _owned.Add(mesh);
                var go = new GameObject("CaveRockTop_" + string.Join("_", owners[pair.Key])) { layer = CryptKit.Layer };
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                _lids.Add(r);
                _lidOwners.Add(owners[pair.Key]);
                g++;
            }
        }

        // ---------------- The dark look (work order 083) ----------------
        //
        // Kristian, 2026-11-22, on 076's pictures: "det ser ikke bra ut. Hva er det poe2 og diablo gjør som får det til å føles
        // ut som at man traverserer igjennom et område/hule? Er kameraet mer zoomet inn og veggene høyere?" The producer's reading
        // of the genre: the far walls tall, a real face that takes light at its foot and goes dark upward; the near walls cut
        // away; the edge of the world darkness, not a surface; the wall tops a flat black cap that merges with it.
        //
        // Every far section of the kit (CaveWalls: the north and side walls for the game's camera) that CaveRise.MakeDark lets
        // rise gets the kit's own wall piece stacked on it, stretched up to its height, in the kit's stone a little darker and
        // without its glow; a veil in front of each face toward a room darkens it from CaveValues.FadeFrom to black at
        // FarHeight (a multiply, so the kit's stone and its lanterns stay what they are at the foot); a flat cap of the earth's
        // black lies on every wall's top, reaching back past it where nothing stands behind. The near sections are the kit's
        // low wall cut to the lip, with the same cap. Piers stack with their walls. Nothing has a collider or casts a shadow;
        // the kit, the boxes, the NavMesh and the colliders are what they were (only the low walls' and posts' drawn height
        // changes). The picture falls to black toward its edges (MakeFalloff).

        public const string DarkRootName = "CaveDark";
        Transform _darkRoot;
        readonly List<MeshRenderer> _dark = new List<MeshRenderer>();
        readonly List<int[]> _darkOwners = new List<int[]>();
        readonly List<GameObject> _stacked = new List<GameObject>();

        /// <summary>The veils and the caps of the dark look, one mesh each for every set of rooms they stand between.</summary>
        public IReadOnlyList<MeshRenderer> DarkMeshes => _dark;
        /// <summary>The kit pieces stacked over the far walls and their piers (the dark look).</summary>
        public IReadOnlyList<GameObject> Stacked => _stacked;
        /// <summary>Far sections raised over the kit's wall, far sections the sight rule held at the kit's height, near sections cut to the lip.</summary>
        public int Raised { get; private set; }
        public int Held { get; private set; }
        public int Lips { get; private set; }
        /// <summary>The share of its height the kit's low wall is drawn at in the dark look (the lip over the model's top).</summary>
        public float LipScale { get; private set; } = 1f;

        sealed class ShadeBuild
        {
            public readonly List<Vector3> V = new List<Vector3>();
            public readonly List<Color> C = new List<Color>();
            public readonly List<int> T = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color ca, Color cb, Color cc, Color cd)
            {
                int i = V.Count;
                V.Add(a); V.Add(b); V.Add(c); V.Add(d);
                C.Add(ca); C.Add(cb); C.Add(cc); C.Add(cd);
                T.Add(i); T.Add(i + 1); T.Add(i + 2);
                T.Add(i); T.Add(i + 2); T.Add(i + 3);
            }
        }

        static long OwnersKey(int a, int b, int c, int d, out int[] set)
        {
            var list = new List<int>(4);
            foreach (int o in new[] { a, b, c, d })
                if (o >= 0 && !list.Contains(o)) list.Add(o);
            list.Sort();
            set = list.ToArray();
            long key = 0;
            for (int i = 0; i < set.Length; i++) key = key * 1024 + set[i] + 1;
            return key;
        }

        /// <summary>The veil's multiply at a height: 0.5 (the stone as lit) up to FadeFrom, falling to 0 (black) at FarHeight.</summary>
        static float VeilShade(CaveValues v, float h)
        {
            float t = Mathf.Clamp01((h - v.FadeFrom) / Mathf.Max(0.1f, v.FarHeight - v.FadeFrom));
            return 0.5f * Mathf.Pow(1f - t, Mathf.Max(0.1f, v.FadePower));
        }

        Material UpperMaterial(CryptPiece piece, CryptKitValues kv)
        {
            Material m = CryptKit.MakeMaterial(piece, kv);
            m.name = "Cave_Upper_" + piece;
            m.DisableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", Color.black);
            Color tint = (piece == CryptPiece.Pier || piece == CryptPiece.Arch ? CryptKit.Picked(kv.PaleStoneTint) : Color.white) * Mathf.Clamp01(V.UpperTint);
            tint.a = 1f;
            m.color = tint;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            _owned.Add(m);
            return m;
        }

        static void NoShadows(GameObject go)
        {
            foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true)) r.shadowCastingMode = ShadowCastingMode.Off;
        }

        void BuildDark()
        {
            CaveValues v = V;
            LevelLayout layout = _level.Layout;
            CryptKitPlan plan = _level.KitPlan;
            CryptKitValues kv = _values.Crypt ?? new CryptKitValues();
            CryptKit.Model wall = CryptKit.Load(CryptPiece.Wall), low = CryptKit.Load(CryptPiece.LowWall);
            CryptKit.Model pier = CryptKit.Load(CryptPiece.Pier), post = CryptKit.Load(CryptPiece.Post);
            WallTop = wall != null ? wall.Bounds.max.y : _values.WallHeight;
            float lowTop = low != null ? low.Bounds.max.y : _values.NearWallHeight;
            CameraRig rig = FindAnyObjectByType<CameraRig>();
            FeelValues feel = rig != null && rig.Settings != null ? rig.Settings.Values : new FeelValues();
            // The distance in use when the floor is built (the pause menu's step, work order 083).
            Rise = CaveRise.MakeDark(layout, plan, v, WallTop, feel.CameraPitch, GameSettings.CameraDistance(feel.CameraDistance));

            var rootObject = new GameObject(DarkRootName) { layer = CryptKit.Layer };
            rootObject.transform.SetParent(transform, false);
            _darkRoot = rootObject.transform;

            // The kit's pieces by their place in the plan.
            var sectionPiece = new Dictionary<int, GameObject>();
            var pointPiece = new Dictionary<int, GameObject>();
            foreach (GeneratedLevel.KitPiece p in _level.KitPieces)
            {
                if (p.Object == null) continue;
                if (p.Kind == CryptPiece.Wall || p.Kind == CryptPiece.WallNiche || p.Kind == CryptPiece.LowWall) sectionPiece[p.Index] = p.Object;
                else if (p.Kind == CryptPiece.Pier || p.Kind == CryptPiece.Post) pointPiece[p.Index] = p.Object;
            }

            float cs = layout.CellSize;
            float wallFront = wall != null ? wall.Bounds.max.z : _values.WallThickness * 0.5f;
            float wallBack = wall != null ? -wall.Bounds.min.z : _values.WallThickness * 0.5f;
            float pierHalf = pier != null ? Mathf.Max(Mathf.Max(pier.Bounds.max.x, -pier.Bounds.min.x), Mathf.Max(pier.Bounds.max.z, -pier.Bounds.min.z)) : 0.3f;
            float pierTop = pier != null ? pier.Bounds.max.y : WallTop;
            float veilOut = Mathf.Max(wallFront, wallBack, pierHalf) + 0.03f;
            LipScale = lowTop > 1e-3f ? Mathf.Clamp(v.LipHeight / lowTop, 0.05f, 1f) : 1f;
            float lip = lowTop * LipScale;
            float ground = _level.ToWorld(new LevelPoint(0, 0)).y;

            var caps = new Dictionary<long, ShadeBuild>();
            var veils = new Dictionary<long, ShadeBuild>();
            var owners = new Dictionary<long, int[]>();
            var pointTop = new Dictionary<long, float>();
            Material upper = null, upperPier = null;
            Color black = Color.black;

            ShadeBuild Get(Dictionary<long, ShadeBuild> into, long key, int[] set)
            {
                if (!into.TryGetValue(key, out ShadeBuild b)) into[key] = b = new ShadeBuild();
                owners[key] = set;
                return b;
            }

            // A flat cap: centred on c, half its length along, reaching plus and minus across, at a height over the ground.
            void Cap(long key, int[] set, Vector3 c, Vector3 along, Vector3 across, float half, float plus, float minus, float height)
            {
                Vector3 y = Vector3.up * (height - c.y + ground);
                Get(caps, key, set).Quad(c + y - along * half - across * minus, c + y - along * half + across * plus,
                                         c + y + along * half + across * plus, c + y + along * half - across * minus, black, black, black, black);
            }

            // A veil in front of a face: from FadeFrom to the top, rows so the fade's curve shows, its multiply by the height.
            void Veil(long key, int[] set, Vector3 at, Vector3 along, float half, float top)
            {
                ShadeBuild b = Get(veils, key, set);
                const int rows = 6;
                float from = Mathf.Min(v.FadeFrom, top);
                for (int r = 0; r < rows; r++)
                {
                    float h0 = from + (top - from) * r / rows, h1 = from + (top - from) * (r + 1) / rows;
                    float s0 = VeilShade(v, h0), s1 = VeilShade(v, h1);
                    Color c0 = new Color(s0, s0, s0, 1f), c1 = new Color(s1, s1, s1, 1f);
                    Vector3 lo = at + Vector3.up * (h0 + ground - at.y), hi = at + Vector3.up * (h1 + ground - at.y);
                    b.Quad(lo - along * half, lo + along * half, hi + along * half, hi - along * half, c0, c0, c1, c1);
                }
            }

            void TouchPoint(int x, int y, float top)
            {
                long k = ((long)x << 32) ^ (uint)y;
                if (!pointTop.TryGetValue(k, out float was) || top > was) pointTop[k] = top;
            }

            for (int i = 0; i < plan.Sections.Count; i++)
            {
                CryptKitSection s = plan.Sections[i];
                LevelPoint mid = s.Vertical ? new LevelPoint(s.Line * cs, (s.Cell + 0.5) * cs) : new LevelPoint((s.Cell + 0.5) * cs, s.Line * cs);
                Vector3 c = _level.ToWorld(mid);
                Vector3 along = s.Vertical ? Vector3.forward : Vector3.right;
                Vector3 across = s.Vertical ? Vector3.right : Vector3.forward; // toward the plus side (east or north)
                long key = OwnersKey(s.RoomMinus, s.RoomPlus, -1, -1, out int[] set);
                if (CaveWalls.IsNear(s, CaveWalls.GameYaw))
                {
                    // A near wall: the kit's low wall cut to the lip, a black cap on it.
                    if (sectionPiece.TryGetValue(i, out GameObject go)) go.transform.localScale = new Vector3(1f, LipScale, 1f);
                    float front = low != null ? low.Bounds.max.z : _values.WallThickness * 0.5f, back = low != null ? -low.Bounds.min.z : _values.WallThickness * 0.5f;
                    float plus = (s.Facing > 0 ? front : back) + 0.03f, minus = (s.Facing > 0 ? back : front) + 0.03f;
                    Cap(key, set, c, along, across, cs * 0.5f + 0.03f, plus, minus, lip + 0.01f);
                    Lips++;
                    continue;
                }
                float top = Rise.RiseOf(s.Vertical, s.Line, s.Cell);
                bool raised = wall != null && top >= WallTop + v.MinRise;
                if (raised)
                {
                    float sy = (top - WallTop + 0.03f) / Mathf.Max(0.01f, wall.Bounds.size.y);
                    Vector3 at = c + Vector3.up * (WallTop - 0.03f - wall.Bounds.min.y * sy);
                    if (upper == null) upper = UpperMaterial(CryptPiece.Wall, kv);
                    GameObject go = CryptKit.Make(wall, upper, _darkRoot, "CaveUpper_" + (s.Vertical ? "V" : "H") + s.Line + "_" + s.Cell, at,
                                                  Quaternion.LookRotation(SideOf(s.Vertical, s.Facing)), new Vector3(1f, sy, 1f));
                    NoShadows(go);
                    _stacked.Add(go);
                    Raised++;
                }
                else
                {
                    top = WallTop;
                    Held++;
                }
                if (s.Vertical) { TouchPoint(s.Line, s.Cell, top); TouchPoint(s.Line, s.Cell + 1, top); }
                else { TouchPoint(s.Cell, s.Line, top); TouchPoint(s.Cell + 1, s.Line, top); }
                // The veils on its faces toward a room; the cap over it, reaching back where no room is behind.
                for (int side = -1; side <= 1; side += 2)
                    if (s.RoomOn(side) >= 0) Veil(key, set, c + across * (side * veilOut), along, cs * 0.5f + veilOut, top);
                float pd = (s.Facing > 0 ? wallFront : wallBack) + (s.RoomPlus < 0 ? v.CapBack : 0.03f);
                float md = (s.Facing > 0 ? wallBack : wallFront) + (s.RoomMinus < 0 ? v.CapBack : 0.03f);
                Cap(key, set, c, along, across, cs * 0.5f + 0.03f, pd, md, top + 0.01f);
            }

            // Piers and posts: a pier stacks with the tallest wall it holds and is capped; a post (only near walls meet there) is
            // cut to the lip and capped, but the two at the ends of a door in a near wall stand (CaveValues.DoorPostsStand).
            for (int j = 0; j < plan.Points.Count; j++)
            {
                CryptKitPoint p = plan.Points[j];
                Vector3 c = _level.ToWorld(new LevelPoint(p.X * cs, p.Y * cs));
                long key = OwnersKey(layout.RoomAtCell(p.X - 1, p.Y - 1)?.Order ?? -1, layout.RoomAtCell(p.X, p.Y - 1)?.Order ?? -1,
                                     layout.RoomAtCell(p.X - 1, p.Y)?.Order ?? -1, layout.RoomAtCell(p.X, p.Y)?.Order ?? -1, out int[] set);
                if (p.Pier)
                {
                    if (pier == null) continue;
                    float top = pointTop.TryGetValue(((long)p.X << 32) ^ (uint)p.Y, out float t) ? t : pierTop;
                    if (top > pierTop + 0.1f)
                    {
                        float sy = (top - pierTop + 0.03f) / Mathf.Max(0.01f, pier.Bounds.size.y);
                        if (upperPier == null) upperPier = UpperMaterial(CryptPiece.Pier, kv);
                        GameObject go = CryptKit.Make(pier, upperPier, _darkRoot, "CaveUpperPier_" + p.X + "_" + p.Y,
                                                      c + Vector3.up * (pierTop - 0.03f - pier.Bounds.min.y * sy), Quaternion.identity, new Vector3(1f, sy, 1f));
                        NoShadows(go);
                        _stacked.Add(go);
                    }
                    float h = Mathf.Max(top, pierTop);
                    Cap(key, set, c, Vector3.right, Vector3.forward, pierHalf + 0.03f, pierHalf + 0.03f, pierHalf + 0.03f, h + 0.012f);
                    continue;
                }
                if (v.DoorPostsStand && AtNearDoorEnd(layout, p.X, p.Y)) continue;
                if (pointPiece.TryGetValue(j, out GameObject postObject)) postObject.transform.localScale = new Vector3(1f, LipScale, 1f);
                if (post != null)
                {
                    float half = Mathf.Max(Mathf.Max(post.Bounds.max.x, -post.Bounds.min.x), Mathf.Max(post.Bounds.max.z, -post.Bounds.min.z)) + 0.03f;
                    Cap(key, set, c, Vector3.right, Vector3.forward, half, half, half, post.Bounds.max.y * LipScale + 0.012f);
                }
            }

            Material capMaterial = MoodShapes.MakeLit(null);
            capMaterial.name = "Cave_Cap";
            Rgb e = v.Earth;
            Color earth = new Color(e.R, e.G, e.B, 1f);
            capMaterial.color = earth;
            if (capMaterial.HasProperty("_BaseColor")) capMaterial.SetColor("_BaseColor", earth);
            _owned.Add(capMaterial);
            Material veilMaterial = MoodShapes.MakeUnlit("Cave_Veil", MoodShapes.Blend.Multiply2x, null, 3001);
            _owned.Add(veilMaterial);
            MakeMeshes(caps, owners, capMaterial, "CaveCap_");
            MakeMeshes(veils, owners, veilMaterial, "CaveVeil_");
        }

        /// <summary>Is the grid point one end of a door in a near (low) line?</summary>
        static bool AtNearDoorEnd(LevelLayout layout, int x, int y)
        {
            foreach (LevelDoor d in layout.Doors)
            {
                if (d.Vertical || d.Line != y) continue;
                if (layout.RoomAtCell(d.From, d.Line) == null) continue; // a full line's door (its arch stands)
                if (x == d.From || x == d.From + d.Width) return true;
            }
            return false;
        }

        void MakeMeshes(Dictionary<long, ShadeBuild> builds, Dictionary<long, int[]> owners, Material material, string prefix)
        {
            foreach (KeyValuePair<long, ShadeBuild> pair in builds)
            {
                int[] set = owners[pair.Key];
                var mesh = new Mesh { name = prefix + string.Join("_", set) };
                if (pair.Value.V.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
                mesh.SetVertices(pair.Value.V);
                mesh.SetColors(pair.Value.C);
                mesh.SetTriangles(pair.Value.T, 0);
                var normals = new Vector3[pair.Value.V.Count];
                for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
                mesh.normals = normals;
                mesh.RecalculateBounds();
                _owned.Add(mesh);
                var go = new GameObject(prefix + string.Join("_", set)) { layer = CryptKit.Layer };
                go.transform.SetParent(_darkRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
                _dark.Add(r);
                _darkOwners.Add(set);
            }
        }

        // ---------------- The falloff: the picture's edges to black (work order 083) ----------------

        GameObject _falloff;
        Mesh _falloffMesh;
        Material _falloffMaterial;
        float _falloffAspect = -1f, _falloffFov = -1f, _falloffNear = -1f;
        System.Action<ScriptableRenderContext, Camera> _onBeginCamera;

        /// <summary>The falloff's object (a child of the camera), while the dark look holds the camera; else null.</summary>
        public GameObject Falloff => _falloff;

        /// <summary>
        /// The falloff (CaveValues.Falloff, FalloffDark): a grid just past the camera's near plane, filling its view, that
        /// multiplies the picture by a share falling from 1 at the middle (out to the falloff's reach, a share of the
        /// half-diagonal) toward 1 - FalloffDark at the corners: the edge of the world goes to black. One draw of a few hundred
        /// triangles; it is sized when the camera begins to draw (no allocation).
        /// </summary>
        void MakeFalloff()
        {
            CaveValues v = V;
            if (_falloff != null || _camera == null || v.FalloffDark <= 0f || v.Falloff >= 1f) return;
            const int n = 16;
            if (_falloffMesh == null)
            {
                var verts = new List<Vector3>();
                var colours = new List<Color>();
                var tris = new List<int>();
                for (int y = 0; y <= n; y++)
                    for (int x = 0; x <= n; x++)
                    {
                        float u = x / (float)n * 2f - 1f, w = y / (float)n * 2f - 1f;
                        float r = Mathf.Sqrt(u * u + w * w) / Mathf.Sqrt(2f);
                        float t = Mathf.Clamp01((r - v.Falloff) / Mathf.Max(0.01f, 1f - v.Falloff));
                        t = t * t * (3f - 2f * t);
                        float s = 0.5f * (1f - Mathf.Clamp01(v.FalloffDark) * t);
                        verts.Add(new Vector3(u * 0.5f, w * 0.5f, 0f));
                        colours.Add(new Color(s, s, s, 1f));
                    }
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        int a = y * (n + 1) + x;
                        tris.Add(a); tris.Add(a + n + 1); tris.Add(a + 1);
                        tris.Add(a + 1); tris.Add(a + n + 1); tris.Add(a + n + 2);
                    }
                _falloffMesh = new Mesh { name = "Cave_Falloff" };
                _falloffMesh.SetVertices(verts);
                _falloffMesh.SetColors(colours);
                _falloffMesh.SetTriangles(tris, 0);
                _falloffMesh.bounds = new Bounds(Vector3.zero, new Vector3(1f, 1f, 0.1f));
                _owned.Add(_falloffMesh);
                _falloffMaterial = MoodShapes.MakeUnlit("Cave_Falloff", MoodShapes.Blend.Multiply2x, null, 3999);
                _owned.Add(_falloffMaterial);
            }
            _falloff = new GameObject("CaveFalloff");
            _falloff.transform.SetParent(_camera.transform, false);
            _falloff.AddComponent<MeshFilter>().sharedMesh = _falloffMesh;
            var mr = _falloff.AddComponent<MeshRenderer>();
            mr.sharedMaterial = _falloffMaterial;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _falloffAspect = _falloffFov = _falloffNear = -1f;
            SizeFalloff(_camera);
            if (_onBeginCamera == null) _onBeginCamera = OnBeginCamera;
            RenderPipelineManager.beginCameraRendering += _onBeginCamera;
        }

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == _camera && _falloff != null) SizeFalloff(cam);
        }

        void SizeFalloff(Camera cam)
        {
            float aspect = cam.pixelHeight > 0 ? cam.pixelWidth / (float)cam.pixelHeight : cam.aspect;
            if (aspect == _falloffAspect && cam.fieldOfView == _falloffFov && cam.nearClipPlane == _falloffNear) return;
            _falloffAspect = aspect;
            _falloffFov = cam.fieldOfView;
            _falloffNear = cam.nearClipPlane;
            float z = cam.nearClipPlane + 0.02f;
            float h = 2f * z * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.02f;
            _falloff.transform.localPosition = new Vector3(0f, 0f, z);
            _falloff.transform.localRotation = Quaternion.identity;
            _falloff.transform.localScale = new Vector3(h * aspect, h, 1f);
        }

        void DropFalloff()
        {
            if (_onBeginCamera != null) RenderPipelineManager.beginCameraRendering -= _onBeginCamera;
            if (_falloff != null) Destroy(_falloff);
            _falloff = null;
        }
    }
}
