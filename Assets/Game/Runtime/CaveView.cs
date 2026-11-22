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
            if (cave.V.TallWalls && level.KitPlan != null) cave.BuildRock();
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
        }

        Material _capMaterial;
        readonly List<Material> _capWas = new List<Material>();

        void SetCaps(Color c)
        {
            if (_capMaterial == null) return;
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
            foreach (CryptKitSection s in _level.KitPlan.Sections)
            {
                if (s.Low) continue;
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

            Texture2D fade = MakeFade(v);
            _owned.Add(fade);
            Material material = MoodShapes.MakeLit(fade);
            material.name = "Cave_Rock";
            _owned.Add(material);
            var root = new GameObject(RockRootName) { layer = CryptKit.Layer };
            root.transform.SetParent(transform, false);
            _rockRoot = root.transform;
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
                go.transform.SetParent(_rockRoot, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = true;
                _rock.Add(r);
                RockOwner.Add(pair.Key);
            }
        }
    }
}
