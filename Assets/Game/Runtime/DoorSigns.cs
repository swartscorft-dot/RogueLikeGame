using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The doors that say what is behind them (work order 041; Kristian's second session of 2026-11-20: "It is not quite
    /// intuitive where the chest rooms are; I end up double-checking several rooms for them", and his answers: marked doors,
    /// and opened chests and cleared rooms shown). On a generated floor with <see cref="LevelValues.DoorSigns"/> on, it sits
    /// on the level's root and goes with it. The rules are <see cref="DoorSignRules"/>.
    ///
    /// Over every doorway into a treasure room, the elite room or the exit room, a sign: a flat shape made in code (the
    /// chest, the horned helm, the skull, the crowned skull for the act boss), in the look of the shot effects and the mark
    /// pips (an outer shape in its colour, a lighter inner one, both glowing under the bloom, with the shot effects'
    /// material), turned to the camera, <see cref="DoorSignValues.OverDoor"/> over the arch's top (the wall's without the
    /// kit, so with <see cref="LevelValues.CryptKit"/> off it shows the same over the gap) and a little into the room before
    /// the door. Lit while she is in the room before the door and the room beyond is not done; dim (its colour darkened and
    /// greyed, no inner shape) otherwise, so the door she came in by is always dim, and so is the door to an opened treasure
    /// room or a cleared room. Each sign has its two meshes made once and is switched between them, never rebuilt.
    ///
    /// The light: one point light for the floor, in the lit sign's colour, without shadows, on her side of the lit doorway
    /// near the floor, so the colour spills from the doorway on to the floor she stands on; off while no doorway is lit. It
    /// is made the first time a doorway is lit (never on the entrance, whose door on leads to a combat room), so the lantern
    /// lights' budget (<see cref="CryptKitLights"/>: at most two a room, only her room's on) is joined by one light at most
    /// (<see cref="DoorSignValues.LightsMost"/>). Nothing has a collider; nothing is allocated in a frame.
    /// </summary>
    public sealed class DoorSigns : MonoBehaviour
    {
        /// <summary>One sign: its door, what it says, where it stands, and whether it is lit now.</summary>
        public sealed class Sign
        {
            public LevelDoor Door;
            public DoorSign Kind;
            /// <summary>The room it leads on to and the room it is the way on from, by their place in the run.</summary>
            public int Beyond, Before;
            public GameObject Object;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public Mesh LitMesh, DimMesh;
            public bool Lit;
            public bool BeyondDone;
            /// <summary>The sign's colour, lit, as picked.</summary>
            public Color Colour;
            /// <summary>Where the light stands when this doorway is lit, in the world.</summary>
            public Vector3 LightAt;
        }

        readonly List<Sign> _signs = new List<Sign>();
        readonly List<Mesh> _meshes = new List<Mesh>();
        GeneratedLevel _level;
        DoorSignValues _v;
        Transform _root;
        Light _light;
        Material _fallback;
        PlayerCharacter _player;
        float _lookedForPlayer = float.NegativeInfinity;

        /// <summary>The signs, in the order of the doors.</summary>
        public IReadOnlyList<Sign> Signs => _signs;
        /// <summary>The one light, once a doorway has been lit; else null.</summary>
        public Light DoorLight => _light;
        /// <summary>The room she was in at the last tick, by its place in the run; -1 for none.</summary>
        public int Here { get; private set; } = -1;
        /// <summary>The object every sign is under.</summary>
        public Transform Root => _root;

        /// <summary>The sign of the door into this room (by its place in the run), or null.</summary>
        public Sign SignInto(int room)
        {
            for (int i = 0; i < _signs.Count; i++)
                if (_signs[i].Beyond == room) return _signs[i];
            return null;
        }

        /// <summary>The signs of a generated floor, on its root.</summary>
        public static DoorSigns Add(GeneratedLevel level)
        {
            var signs = level.gameObject.AddComponent<DoorSigns>();
            signs.Build(level);
            return signs;
        }

        void Build(GeneratedLevel level)
        {
            _level = level;
            LevelValues values = level.Values;
            _v = values.Signs ?? new DoorSignValues();
            LevelLayout layout = level.Layout;
            float cs = layout.CellSize;
            var rootObject = new GameObject("DoorSigns");
            rootObject.transform.SetParent(level.transform, false);
            _root = rootObject.transform;

            // Over the arch's top with the kit, the wall's without it.
            float top = values.WallHeight;
            if (level.KitPlan != null)
            {
                CryptKit.Model arch = CryptKit.Load(CryptPiece.Arch);
                if (arch != null) top = Mathf.Max(top, arch.Bounds.max.y);
            }
            Material material = SignMaterial();
            var meshes = new Dictionary<(DoorSign, bool), Mesh>();
            foreach (LevelDoor door in layout.Doors)
            {
                int beyond = DoorSignRules.Beyond(door), before = DoorSignRules.Before(door);
                if (beyond < 0 || beyond >= layout.Rooms.Count || before < 0) continue;
                DoorSign kind = DoorSignRules.SignOf(layout.Rooms[beyond], layout.Floor, values.FloorsPerAct);
                if (kind == DoorSign.None) continue;

                door.Opening(cs, out LevelPoint a, out LevelPoint b);
                var middle = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
                LevelPoint room = layout.Rooms[before].Middle(cs);
                // Into the room before the door: across the door's line toward its middle.
                Vector3 into = door.Vertical ? new Vector3(Mathf.Sign((float)(room.X - middle.X)), 0f, 0f) : new Vector3(0f, 0f, Mathf.Sign((float)(room.Y - middle.Y)));
                Vector3 at = level.ToWorld(middle);
                Color colour = CryptKit.Picked(DoorSignRules.ColourOf(kind, _v));
                var sign = new Sign
                {
                    Door = door, Kind = kind, Beyond = beyond, Before = before, Colour = colour,
                    LightAt = at + into * _v.LightOut + Vector3.up * _v.LightHeight,
                };
                if (!meshes.TryGetValue((kind, true), out sign.LitMesh)) meshes[(kind, true)] = sign.LitMesh = MakeMesh(kind, true);
                if (!meshes.TryGetValue((kind, false), out sign.DimMesh)) meshes[(kind, false)] = sign.DimMesh = MakeMesh(kind, false);

                var go = new GameObject("DoorSign_" + door.RoomA + "-" + door.RoomB + "_" + kind);
                go.transform.SetParent(_root, false);
                go.transform.position = at + into * _v.OutFromWall + Vector3.up * (top + _v.OverDoor + _v.SignSize * 0.5f);
                sign.Object = go;
                sign.Filter = go.AddComponent<MeshFilter>();
                sign.Filter.sharedMesh = sign.DimMesh;
                sign.Renderer = go.AddComponent<MeshRenderer>();
                sign.Renderer.sharedMaterial = material;
                sign.Renderer.shadowCastingMode = ShadowCastingMode.Off;
                sign.Renderer.receiveShadows = false;
                _signs.Add(sign);
            }
            // The first tick is the frame's, once she has been put at the entrance.
        }

        /// <summary>The shot effects' material, off her; a plain one that shows vertex colours without her (destroyed with this).</summary>
        Material SignMaterial()
        {
            PlayerCharacter player = FindAnyObjectByType<PlayerCharacter>();
            if (player != null && player.ShotFxMaterial != null) return player.ShotFxMaterial;
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            _fallback = new Material(shader) { name = "DoorSigns" };
            return _fallback;
        }

        /// <summary>A sign's mesh in its local XY plane, seen along +Z, from both sides: lit, its outer shape and its lighter inner one; dim, the outer one darkened.</summary>
        Mesh MakeMesh(DoorSign kind, bool lit)
        {
            var verts = new List<Vector3>();
            var colours = new List<Color>();
            var tris = new List<int>();
            Rgb picked = DoorSignRules.ColourOf(kind, _v);
            Rgb rim = lit ? picked : DoorSignRules.DimOf(picked, _v);
            Rgb core = new Rgb((picked.R + 1f) * 0.5f, (picked.G + 1f) * 0.5f, (picked.B + 1f) * 0.5f);
            float half = _v.SignSize * 0.5f;
            List<double[]> parts = DoorSignRules.ShapeOf(kind);
            foreach (double[] p in parts) Piece(p, 0f, Drawn(rim, lit ? _v.RimGlow : 1f));
            if (lit)
                foreach (double[] p in parts) Piece(DoorSignRules.Shrunk(p, 0.55), -0.02f, Drawn(core, _v.CoreGlow));

            void Piece(double[] p, float z, Color c)
            {
                int b = verts.Count, n = p.Length / 2;
                for (int i = 0; i < n; i++)
                {
                    verts.Add(new Vector3((float)p[2 * i] * half, (float)p[2 * i + 1] * half, z));
                    colours.Add(c);
                }
                for (int i = 1; i + 1 < n; i++)
                {
                    tris.Add(b); tris.Add(b + i + 1); tris.Add(b + i);
                    tris.Add(b); tris.Add(b + i); tris.Add(b + i + 1);
                }
            }

            var mesh = new Mesh { name = "DoorSign_" + kind + (lit ? "_Lit" : "_Dim") };
            mesh.SetVertices(verts);
            mesh.SetColors(colours);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            _meshes.Add(mesh);
            return mesh;
        }

        /// <summary>The vertex colour for a picked colour, made brighter by its glow, as the shot effects and the mark pips do it.</summary>
        static Color Drawn(Rgb picked, float glow)
        {
            Color p = CryptKit.Picked(picked);
            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
            return new Color(c.r * glow, c.g * glow, c.b * glow, 1f);
        }

        void LateUpdate() => Tick();

        /// <summary>
        /// Works out each sign's state from where she is, the chests opened and the rooms cleared, switches the meshes that
        /// changed, turns every sign to the camera and puts the light at the lit doorway. Allocates nothing. For tests too.
        /// </summary>
        public void Tick()
        {
            if (_level == null) return;
            if (_player == null && Time.unscaledTime - _lookedForPlayer > 1f)
            {
                _lookedForPlayer = Time.unscaledTime;
                _player = FindAnyObjectByType<PlayerCharacter>();
            }
            LevelRoom room = _player != null ? _level.RoomAt(_player.Position) : null;
            Here = room != null ? room.Order : -1;
            GeneratedLevels levels = GeneratedLevels.Current;
            ICollection<int> opened = levels != null && levels.Level == _level ? levels.ChestsOpened as ICollection<int> : null;
            Camera cam = Camera.main;
            Sign litSign = null;
            for (int i = 0; i < _signs.Count; i++)
            {
                Sign s = _signs[i];
                LevelRoom beyond = _level.Layout.Rooms[s.Beyond];
                bool done = DoorSignRules.Done(beyond.Kind, opened != null && opened.Contains(s.Beyond), Cleared(s.Beyond));
                bool lit = DoorSignRules.Lit(s.Kind, s.Door, Here, done);
                s.BeyondDone = done;
                if (lit != s.Lit || s.Filter.sharedMesh != (lit ? s.LitMesh : s.DimMesh))
                {
                    s.Lit = lit;
                    s.Filter.sharedMesh = lit ? s.LitMesh : s.DimMesh;
                }
                if (cam != null) s.Object.transform.rotation = cam.transform.rotation;
                if (lit && litSign == null) litSign = s;
            }
            if (litSign != null && _light == null && _v.LightsMost > 0) _light = MakeLight();
            if (_light != null)
            {
                _light.enabled = litSign != null;
                if (litSign != null)
                {
                    _light.transform.position = litSign.LightAt;
                    _light.color = litSign.Colour;
                }
            }
        }

        /// <summary>True when the room held enemies and every one is dead.</summary>
        bool Cleared(int order)
        {
            if (!_level.EnemiesByRoom.TryGetValue(order, out List<Enemy> list) || list.Count == 0) return false;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead) return false;
            return true;
        }

        Light MakeLight()
        {
            var go = new GameObject("DoorSign_Light") { layer = CryptKit.Layer };
            go.transform.SetParent(_root, false);
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = _v.LightRange;
            light.intensity = _v.LightIntensity;
            light.shadows = LightShadows.None;
            light.enabled = false;
            return light;
        }

        void OnDestroy()
        {
            foreach (Mesh m in _meshes)
                if (m != null) Destroy(m);
            if (_fallback != null) Destroy(_fallback);
        }
    }
}
