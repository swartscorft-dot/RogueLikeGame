using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>
    /// The greybox view of a generated floor (work order 084; Kristian sent a talk on level design on 2026-11-22, said it was
    /// "veldig fin", and ticked "Gråboks-bryter og romlab": level design is the function of a space in play, judged in greybox
    /// before its art). It sits on the level's root and goes with it; <see cref="Set"/> turns it on and off in the middle of a
    /// run, and the floor is never built again: only what is drawn is swapped.
    ///
    /// On: the floor boxes show one flat mid grey; the wall boxes (their true collision shapes, the low near walls too) show
    /// again, in one darker grey; every doorway gets an orange strip on the floor across its opening and an orange post at each
    /// end of the gap; every prop with a collider is drawn as a box of its collider's size in the colour of what it does
    /// (<see cref="GreyboxRules.PropPart"/>: high cover red, low cover blue, what only blocks walking purple), its model not
    /// drawn; a prop with no collider is not drawn at all; work order 070's line (<see cref="GreyboxRules.FightLine"/>) is a thin
    /// cyan line on the floor of every room that holds a fight, seen from its door in. The chests, the rewards on the ground,
    /// the door signs, the enemies and she are drawn as they are. Everything else of the floor is not drawn (the crypt kit's
    /// pieces, the carved cells' dark tops, the cave's rock, the props' models), the kit's lantern lights and the floor's other
    /// lights are off, the rooms' mood is switched off (its pieces, its lights, its air and its tone, and the scene's light is
    /// given back as the practice room has it: even and neutral), and so is the cave (nothing hidden, the camera's background
    /// as the scene has it). Off: every one of these as it was.
    ///
    /// Nothing it makes has a collider, and it never builds the NavMesh: the colliders, the NavMesh, where anything stands and
    /// what the enemies do are the same with it on and off. Its frame allocates nothing: it has no Update; the floors'
    /// controller asks it to follow the setting (<see cref="GameSettings.Greybox"/>) once a frame.
    /// </summary>
    public sealed class GreyboxView : MonoBehaviour
    {
        public const string RootName = "Greybox";

        GeneratedLevel _level;
        GreyboxValues _values;
        Transform _root;
        bool _on, _built;
        Mesh _cube;
        readonly List<Material> _materials = new List<Material>();
        readonly Dictionary<GreyboxPart, Material> _byPart = new Dictionary<GreyboxPart, Material>();
        readonly List<Renderer> _mine = new List<Renderer>();
        readonly HashSet<Renderer> _mineSet = new HashSet<Renderer>();
        readonly List<GreyboxPart> _propParts = new List<GreyboxPart>();

        // What was there before it was turned on, given back when it goes off.
        enum Done { Swapped, Kept, Hidden }
        struct Was
        {
            public Renderer Renderer;
            public Done Done;
            public bool Enabled, ForceOff;
            public Material Material;
        }
        readonly List<Was> _was = new List<Was>();
        readonly List<(Light light, bool enabled)> _lightsWas = new List<(Light, bool)>();
        readonly List<(Behaviour part, bool enabled)> _partsWas = new List<(Behaviour, bool)>();

        /// <summary>True while it is on.</summary>
        public bool On => _on;
        /// <summary>The level it draws.</summary>
        public GeneratedLevel Level => _level;
        /// <summary>The renderers it made (door strips and posts, prop boxes, the fight's lines); none before it was first on.</summary>
        public IReadOnlyList<Renderer> Made => _mine;
        /// <summary>The fight's line's pieces among them, and the doors' (strips and posts).</summary>
        public int FightLinePieces { get; private set; }
        public int DoorPieces { get; private set; }
        /// <summary>The part each prop box was drawn as, in the order of the level's props, then its large props (those with a box).</summary>
        public IReadOnlyList<GreyboxPart> PropParts => _propParts;
        /// <summary>How many times it was turned on (for tests).</summary>
        public int TimesOn { get; private set; }

        /// <summary>The greybox view of a built floor, off.</summary>
        public static GreyboxView Add(GeneratedLevel level, GreyboxValues values = null)
        {
            var view = level.gameObject.AddComponent<GreyboxView>();
            view._level = level;
            view._values = values ?? new GreyboxValues();
            return view;
        }

        /// <summary>Turns it on or off; nothing when it is so already. Allocates only when it changes.</summary>
        public void Set(bool on)
        {
            if (_level == null || on == _on) return;
            if (on) TurnOn();
            else TurnOff();
        }

        void OnDestroy()
        {
            // The floor goes with it: nothing is put back (the mood and the cave, switched off, let go of the scene's light and
            // the camera already; switching them on now would only wake the mood's air on a floor that is going).
            foreach (Material m in _materials)
                if (m != null) Destroy(m);
            _materials.Clear();
            if (_cube != null) Destroy(_cube);
        }

        // ---------------- On and off ----------------

        void TurnOn()
        {
            if (!_built) Build();
            _on = true;
            TimesOn++;
            _was.Clear();
            _lightsWas.Clear();
            _partsWas.Clear();

            var floors = new HashSet<Renderer>();
            foreach (GameObject f in _level.FloorBoxes)
                if (f != null && f.TryGetComponent(out Renderer r)) floors.Add(r);
            var walls = new HashSet<Renderer>();
            foreach (GameObject w in _level.WallBoxes)
                if (w != null && w.TryGetComponent(out Renderer r)) walls.Add(r);
            var keep = new HashSet<Transform>(); // drawn as it is: the chests and the door signs
            foreach (GeneratedLevel.PlacedProp p in _level.Props)
                if (p.Object != null && p.Prop.Model == PropModel.Chest) keep.Add(p.Object.transform);
            DoorSigns signs = _level.GetComponent<DoorSigns>();
            if (signs != null && signs.Root != null) keep.Add(signs.Root);

            foreach (Renderer r in _level.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null || _mineSet.Contains(r)) continue;
                var was = new Was { Renderer = r, Enabled = r.enabled, ForceOff = r.forceRenderingOff, Material = r.sharedMaterial };
                if (floors.Contains(r) || walls.Contains(r))
                {
                    was.Done = Done.Swapped;
                    r.sharedMaterial = _byPart[floors.Contains(r) ? GreyboxPart.Floor : GreyboxPart.Wall];
                    r.enabled = true;
                    r.forceRenderingOff = false;
                }
                else if (AsItIs(r, keep))
                {
                    was.Done = Done.Kept; // the cave may have hidden it; its own logic keeps its enabled
                    r.forceRenderingOff = false;
                }
                else
                {
                    was.Done = Done.Hidden;
                    r.enabled = false;
                }
                _was.Add(was);
            }
            foreach (Light l in _level.GetComponentsInChildren<Light>(true))
            {
                if (l == null || (signs != null && signs.Root != null && l.transform.IsChildOf(signs.Root)) || l.GetComponentInParent<Enemy>(true) != null) continue;
                _lightsWas.Add((l, l.enabled));
                l.enabled = false;
            }
            // The parts that would put back what it hid, or that are the mood and the cave themselves: off while it is on.
            Off(_level.KitLights);
            Off(_level.Mood);
            Off(_level.Cave);
            foreach (Renderer r in _mine) r.enabled = true;
        }

        void Off(Behaviour part)
        {
            if (part == null) return;
            _partsWas.Add((part, part.enabled));
            part.enabled = false;
        }

        static bool AsItIs(Renderer r, HashSet<Transform> keep)
        {
            if (r.GetComponentInParent<Enemy>(true) != null || r.GetComponentInParent<RewardDrop>(true) != null) return true;
            for (Transform t = r.transform; t != null; t = t.parent)
                if (keep.Contains(t)) return true;
            return false;
        }

        void TurnOff()
        {
            _on = false;
            foreach (Renderer r in _mine)
                if (r != null) r.enabled = false;
            // The mood, the cave and the lanterns back first, as they were; then every renderer and light as it was.
            for (int i = _partsWas.Count - 1; i >= 0; i--)
                if (_partsWas[i].part != null) _partsWas[i].part.enabled = _partsWas[i].enabled;
            foreach ((Light light, bool enabled) in _lightsWas)
                if (light != null) light.enabled = enabled;
            foreach (Was w in _was)
            {
                if (w.Renderer == null) continue;
                switch (w.Done)
                {
                    case Done.Swapped:
                        w.Renderer.sharedMaterial = w.Material;
                        w.Renderer.enabled = w.Enabled;
                        w.Renderer.forceRenderingOff = w.ForceOff;
                        break;
                    case Done.Kept: // what its own logic did with enabled meanwhile stays; the cave's hiding comes back
                        w.Renderer.forceRenderingOff = w.ForceOff;
                        break;
                    default:
                        w.Renderer.enabled = w.Enabled;
                        break;
                }
            }
            _was.Clear();
            _lightsWas.Clear();
            _partsWas.Clear();
        }

        // ---------------- What it draws ----------------

        void Build()
        {
            _built = true;
            _cube = MakeCube();
            foreach (GreyboxPart part in new[] { GreyboxPart.Floor, GreyboxPart.Wall, GreyboxPart.Door, GreyboxPart.HighCover, GreyboxPart.LowCover,
                                                 GreyboxPart.BlocksWalking, GreyboxPart.FightLine })
                _byPart[part] = Flat(GreyboxRules.ColourOf(part, _values), "Greybox_" + part);
            var root = new GameObject(RootName) { layer = CryptKit.Layer };
            root.transform.SetParent(_level.transform, false);
            _root = root.transform;

            LevelLayout layout = _level.Layout;
            LevelValues lv = _level.Values;
            float cs = layout.CellSize, t = lv.WallThickness, lift = _values.LineHeight;

            // The doors: a strip across each opening, and a post at each end of the gap the wall boxes leave.
            foreach (LevelDoor d in layout.Doors)
            {
                d.Opening(cs, out LevelPoint a, out LevelPoint b);
                Vector3 pa = _level.ToWorld(a), pb = _level.ToWorld(b);
                Vector3 middle = (pa + pb) * 0.5f;
                float gap = Mathf.Max(0.1f, d.Width * cs - t);
                // Every wall on a horizontal line between two rooms is a low near wall (GeneratedLevel.BuildWalls); a vertical one is full.
                float height = (d.Vertical ? lv.WallHeight : lv.NearWallHeight) + 0.02f;
                Vector3 along = d.Vertical ? Vector3.forward : Vector3.right;
                Vector3 strip = d.Vertical ? new Vector3(t + 2f * _values.DoorApron, lift, gap) : new Vector3(gap, lift, t + 2f * _values.DoorApron);
                Box("Greybox_Door_" + d.RoomA + "-" + d.RoomB, _root, middle + Vector3.up * (lift * 0.5f), Quaternion.identity, strip, GreyboxPart.Door, false);
                float post = t * 1.1f;
                for (int side = -1; side <= 1; side += 2)
                    Box("Greybox_DoorPost_" + d.RoomA + "-" + d.RoomB + (side < 0 ? "a" : "b"), _root,
                        middle + along * (side * (gap + t) * 0.5f) + Vector3.up * (height * 0.5f), Quaternion.identity, new Vector3(post, height, post), GreyboxPart.Door, true);
                DoorPieces += 3;
            }

            // The props: a box of each collider, in the colour of what it does now.
            void PropBox(GeneratedLevel.PlacedProp p)
            {
                if (p == null || p.Object == null) return;
                Collider c = p.Object.GetComponent<Collider>();
                bool chest = p.Prop.Model == PropModel.Chest;
                Cover cover = p.Object.GetComponent<Cover>();
                GreyboxPart part = GreyboxRules.PropPart(c != null, cover != null && cover.Stops, p.Object.GetComponent<Shelter>() != null, chest);
                if (part == GreyboxPart.Hidden || part == GreyboxPart.AsItIs) return;
                Vector3 centre, size;
                if (c is BoxCollider box)
                {
                    centre = box.center;
                    size = box.size;
                }
                else if (c is CapsuleCollider capsule)
                {
                    centre = capsule.center;
                    size = new Vector3(capsule.radius * 2f, capsule.height, capsule.radius * 2f);
                }
                else
                {
                    Bounds bounds = c.bounds;
                    centre = p.Object.transform.InverseTransformPoint(bounds.center);
                    size = bounds.size;
                }
                Transform at = p.Object.transform;
                Box("Greybox_" + p.Object.name, at, at.TransformPoint(centre), at.rotation, size, part, true);
                _propParts.Add(part);
            }
            foreach (GeneratedLevel.PlacedProp p in _level.Props) PropBox(p);
            foreach (GeneratedLevel.PlacedProp p in _level.LargeProps) PropBox(p);

            // Work order 070's line, in every room that holds a fight, from its door in.
            if (lv.RoomFights && lv.Fights != null)
                foreach (LevelRoom room in layout.Rooms)
                {
                    int enemies = _level.EnemiesByRoom.TryGetValue(room.Order, out List<Enemy> list) ? list.Count : 0;
                    if (!RoomFightRules.HoldsAFight(layout, room, enemies)) continue;
                    LevelDoor door = LevelGeometry.EntryDoor(layout, room);
                    foreach ((LevelPoint a, LevelPoint b) in GreyboxRules.FightLine(layout, room, door, lv.Fights))
                    {
                        Vector3 wa = _level.ToWorld(a), wb = _level.ToWorld(b);
                        bool alongX = Mathf.Abs(wb.x - wa.x) > Mathf.Abs(wb.z - wa.z);
                        float length = Vector3.Distance(wa, wb);
                        float w = _values.FightLineWidth;
                        Box("Greybox_FightLine_" + room.Letter + room.Order, _root, (wa + wb) * 0.5f + Vector3.up * (lift * 1.5f), Quaternion.identity,
                            alongX ? new Vector3(length, lift, w) : new Vector3(w, lift, length), GreyboxPart.FightLine, false);
                        FightLinePieces++;
                    }
                }
        }

        /// <summary>A box of its own (no collider), off until the view is on.</summary>
        void Box(string name, Transform parent, Vector3 worldCentre, Quaternion rotation, Vector3 size, GreyboxPart part, bool shadows)
        {
            var go = new GameObject(name) { layer = CryptKit.Layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(worldCentre, rotation);
            Vector3 parentScale = parent.lossyScale;
            go.transform.localScale = new Vector3(size.x / Mathf.Max(1e-4f, parentScale.x), size.y / Mathf.Max(1e-4f, parentScale.y), size.z / Mathf.Max(1e-4f, parentScale.z));
            go.AddComponent<MeshFilter>().sharedMesh = _cube;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = _byPart[part];
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.receiveShadows = true;
            r.enabled = false;
            _mine.Add(r);
            _mineSet.Add(r);
        }

        Material Flat(Rgb colour, string name)
        {
            Shader shader = Shader.Find(PropFactory.ShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name };
            Color c = CryptKit.Picked(colour);
            m.color = c;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            _materials.Add(m);
            return m;
        }

        /// <summary>A unit cube, centred, a face of four corners each (flat shaded): made here, destroyed with the view.</summary>
        static Mesh MakeCube()
        {
            var vertices = new List<Vector3>(24);
            var normals = new List<Vector3>(24);
            var triangles = new List<int>(36);
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                int i = vertices.Count;
                Vector3 c = n * 0.5f;
                vertices.Add(c - u * 0.5f - v * 0.5f);
                vertices.Add(c + u * 0.5f - v * 0.5f);
                vertices.Add(c + u * 0.5f + v * 0.5f);
                vertices.Add(c - u * 0.5f + v * 0.5f);
                for (int k = 0; k < 4; k++) normals.Add(n);
                triangles.Add(i); triangles.Add(i + 2); triangles.Add(i + 1);
                triangles.Add(i); triangles.Add(i + 3); triangles.Add(i + 2);
            }
            // Each face (n, u, v) with u x v = -n by the cross product's formula: u to the right and v up as seen from outside,
            // so its triangles go clockwise there, the engine's front.
            Face(Vector3.up, Vector3.right, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.back);
            Face(Vector3.right, Vector3.forward, Vector3.up);
            Face(Vector3.left, Vector3.back, Vector3.up);
            Face(Vector3.forward, Vector3.left, Vector3.up);
            Face(Vector3.back, Vector3.right, Vector3.up);
            var mesh = new Mesh { name = "Greybox_Cube" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
