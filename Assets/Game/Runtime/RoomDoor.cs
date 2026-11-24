using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// One door's block of a room whose fight shuts its doors (work order 086, <see cref="RoomDoorRules"/>): a plain box in the
    /// doorway, across the whole opening, the wall's thickness and the wall's height there, on the walls' layer, under the level's
    /// root (it goes with the floor). Made the first time its room's doors close, then kept for the floor.
    ///
    /// Shut (<see cref="Shut"/>): its box collider with <see cref="Cover"/> stops her shots and skills and the enemies' sight and
    /// what they loose (every query of cover is the physics ray of <see cref="Cover"/>), and its <see cref="NavMeshObstacle"/>
    /// carves the doorway out of the NavMesh at once (not only once it has stood still), so nobody walks or paths through it,
    /// her dash included (she moves by the NavMesh agent). All three from the first frame; what is seen rises out of the floor
    /// over <see cref="RoomFightValues.DoorRiseSeconds"/>. Open: all three off at once, and what is seen sinks back into the floor
    /// over <see cref="RoomFightValues.DoorSinkSeconds"/> and is then not drawn. It has no <see cref="Shelter"/>: no duck or cover
    /// at a door.
    ///
    /// Its look (work order 100; Kristian, 2026-11-24: "Mørk stein eller jern, glød i kantene"): in the game a dark mass that
    /// belongs to the wall (<see cref="RoomFightValues.DoorBody"/>, lit and shaded by the lamps as the walls are) with the door
    /// orange glowing only in a band <see cref="RoomFightValues.DoorEdgeWidth"/> wide along every edge of its faces, the top's
    /// too (<see cref="RoomFightValues.DoorEdgeGlow"/>). What is seen is one mesh made here in metres, the block's own size, its
    /// faces' inner parts one submesh and their rims the other, never scaled: so the band is the same width on every face and
    /// cannot stretch or swim while the block rises or sinks (it moves whole). In the greybox view (<see cref="SetGreyboxLook"/>,
    /// told by the view as it goes on and off, and asked of it when the block is made) both parts are the greybox view's door
    /// orange (<see cref="GreyboxValues.Door"/>), lit and glowing by <see cref="RoomFightValues.DoorGlow"/>, as before; so is the
    /// game's with <see cref="RoomFightValues.DoorDarkFace"/> off. Its materials and its mesh are its own, destroyed with it. Its
    /// frame allocates nothing; the floor's controller moves what is seen while it rises or sinks (<see cref="Show"/>).
    /// </summary>
    public sealed class RoomDoor : MonoBehaviour
    {
        /// <summary>The door of the layout it shuts.</summary>
        public LevelDoor Door { get; private set; }
        /// <summary>The room whose fight shuts it, by its place in the run.</summary>
        public int Room { get; private set; }
        public BoxCollider Box { get; private set; }
        public Cover CoverPart { get; private set; }
        public NavMeshObstacle Obstacle { get; private set; }
        /// <summary>What is seen: a cube the block's size, risen to stand in the doorway or sunk under the floor.</summary>
        public Transform Block { get; private set; }
        public Renderer BlockRenderer { get; private set; }
        /// <summary>The block's height, in metres.</summary>
        public float Height { get; private set; }
        /// <summary>True while it is shut: collider, cover and obstacle on.</summary>
        public bool Shut { get; private set; }
        /// <summary>How far what is seen stands out of the floor now, 0 to 1.</summary>
        public float Raised { get; private set; } = -1f;
        /// <summary>True while it wears the greybox view's look (the door orange all over).</summary>
        public bool GreyboxLook { get; private set; }
        /// <summary>The greybox view's door orange (and the game's with the dark face off): today's material of work order 086.</summary>
        public Material DoorOrange => _material;
        /// <summary>The game's dark body and its glowing edge (work order 100); null with <see cref="RoomFightValues.DoorDarkFace"/> off.</summary>
        public Material Body => _body;
        public Material Edge => _edge;
        /// <summary>What is seen: the block's mesh in metres, submesh 0 the faces' inner parts, submesh 1 their rims (the edge band).</summary>
        public Mesh Mesh => _mesh;
        /// <summary>m: the edge band's width as built (<see cref="RoomFightValues.DoorEdgeWidth"/>, at most 0.45 of a face's short side).</summary>
        public float EdgeWidth { get; private set; }

        Material _material, _body, _edge;
        Mesh _mesh;
        Material[] _gameLook, _greyboxLook;

        /// <summary>
        /// A door's block for <paramref name="door"/> of <paramref name="room"/>, open (all its parts off, nothing seen), under the
        /// level's root on the walls' layer.
        /// </summary>
        public static RoomDoor Make(GeneratedLevel level, int room, LevelDoor door, RoomFightValues values, GreyboxValues looks = null)
        {
            LevelValues lv = level.Values;
            DoorBlock b = RoomDoorRules.Block(level.Layout, door, lv.WallHeight, lv.NearWallHeight, lv.WallThickness);
            var go = new GameObject("RoomDoor_" + room + "_" + door.RoomA + "-" + door.RoomB) { layer = level.WallLayer };
            go.transform.SetParent(level.transform, false);
            Vector3 at = level.ToWorld(b.Centre);
            float height = (float)b.Height;
            go.transform.position = new Vector3(at.x, height * 0.5f, at.z);
            Vector3 size = b.Vertical ? new Vector3((float)b.Thickness, height, (float)b.Length) : new Vector3((float)b.Length, height, (float)b.Thickness);

            var d = go.AddComponent<RoomDoor>();
            d.Door = door;
            d.Room = room;
            d.Height = height;
            d.Box = go.AddComponent<BoxCollider>();
            d.Box.size = size;
            d.CoverPart = go.AddComponent<Cover>();
            d.Obstacle = go.AddComponent<NavMeshObstacle>();
            d.Obstacle.shape = NavMeshObstacleShape.Box;
            d.Obstacle.size = size;
            d.Obstacle.center = Vector3.zero;
            d.Obstacle.carving = true;
            d.Obstacle.carveOnlyStationary = false; // carved from its first frame, not after it has stood still
            d.Obstacle.carvingMoveThreshold = 0.1f;

            // What is seen has no collider (the root's box is the door): a mesh of the block's size in metres, never scaled (work order 100).
            var block = new GameObject("Block") { layer = level.WallLayer };
            block.transform.SetParent(go.transform, false);
            d.EdgeWidth = EdgeBand(size, values != null ? values.DoorEdgeWidth : 0f);
            d._mesh = MakeMesh(size, d.EdgeWidth);
            block.AddComponent<MeshFilter>().sharedMesh = d._mesh;
            d.Block = block.transform;
            d.BlockRenderer = block.AddComponent<MeshRenderer>();
            Rgb orange = (looks ?? new GreyboxValues()).Door;
            d._material = MakeMaterial(orange, values != null ? values.DoorGlow : 0f, "RoomDoor");
            d._greyboxLook = new[] { d._material, d._material };
            if (values != null && values.DoorDarkFace)
            {
                d._body = MakeMaterial(values.DoorBody, 0f, "RoomDoor_Body");
                d._edge = MakeMaterial(orange, values.DoorEdgeGlow, "RoomDoor_Edge");
                d._gameLook = new[] { d._body, d._edge };
            }
            else d._gameLook = d._greyboxLook;
            GreyboxView greybox = level.GetComponent<GreyboxView>();
            d.GreyboxLook = !(greybox != null && greybox.On); // so that the call below sets it
            d.SetGreyboxLook(greybox != null && greybox.On);
            d.SetShut(false);
            d.Show(0f);
            if (level.Cave != null) level.Cave.TrackDoor(d.BlockRenderer, door.RoomA, door.RoomB); // drawn with its rooms, as the floor's pieces are
            return d;
        }

        /// <summary>
        /// The greybox view's look (the door orange all over, as before) or the game's (the dark body, the glowing edge). Told by the
        /// greybox view as it goes on and off; nothing when it is so already.
        /// </summary>
        public void SetGreyboxLook(bool on)
        {
            if (on == GreyboxLook || BlockRenderer == null) return;
            GreyboxLook = on;
            BlockRenderer.sharedMaterials = on ? _greyboxLook : _gameLook;
        }

        /// <summary>Shut or open: the collider, its cover and the NavMesh's obstacle on or off together, at once.</summary>
        public void SetShut(bool shut)
        {
            Shut = shut;
            Box.enabled = shut;
            CoverPart.enabled = shut;
            Obstacle.enabled = shut;
        }

        /// <summary>
        /// What is seen, <paramref name="raised"/> (0 to 1) out of the floor: risen fully it fills the doorway; at 0 it is under
        /// the floor and not drawn. Nothing when it is so already (no allocation either way).
        /// </summary>
        public void Show(float raised)
        {
            if (Mathf.Approximately(raised, Raised)) return;
            Raised = raised;
            Block.localPosition = new Vector3(0f, -Height * (1f - raised), 0f);
            BlockRenderer.enabled = raised > 0f;
        }

        void OnDestroy()
        {
            if (_material != null) Destroy(_material);
            if (_body != null) Destroy(_body);
            if (_edge != null) Destroy(_edge);
            if (_mesh != null) Destroy(_mesh);
        }

        /// <summary>m: the band asked for, at most 0.45 of the block's shortest side (so every face keeps an inner part), never below 0.</summary>
        public static float EdgeBand(Vector3 size, float asked) =>
            Mathf.Clamp(asked, 0f, 0.45f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));

        /// <summary>
        /// The block's mesh (work order 100): a box of <paramref name="size"/> metres about its middle, a face of flat normals each.
        /// Every face but the bottom (never seen: it stands on the floor or under it) is cut into its inner part (submesh 0, the
        /// body) and four strips <paramref name="band"/> wide along its rim (submesh 1, the edge); the bottom is all body. With no
        /// band every face is body only. Texture coordinates in metres along the face.
        /// </summary>
        public static Mesh MakeMesh(Vector3 size, float band)
        {
            var vertices = new List<Vector3>(124);
            var normals = new List<Vector3>(124);
            var uv = new List<Vector2>(124);
            var body = new List<int>(72);
            var edge = new List<int>(120);
            void Quad(Vector3 n, Vector3 u, Vector3 v, float hn, float a0, float a1, float b0, float b1, List<int> into)
            {
                int i = vertices.Count;
                Vector3 c = n * hn;
                vertices.Add(c + u * a0 + v * b0);
                vertices.Add(c + u * a1 + v * b0);
                vertices.Add(c + u * a1 + v * b1);
                vertices.Add(c + u * a0 + v * b1);
                for (int k = 0; k < 4; k++) normals.Add(n);
                uv.Add(new Vector2(a0, b0));
                uv.Add(new Vector2(a1, b0));
                uv.Add(new Vector2(a1, b1));
                uv.Add(new Vector2(a0, b1));
                into.Add(i); into.Add(i + 2); into.Add(i + 1);
                into.Add(i); into.Add(i + 3); into.Add(i + 2);
            }
            void Face(Vector3 n, Vector3 u, Vector3 v, bool rim)
            {
                float hn = Mathf.Abs(Vector3.Dot(n, size)) * 0.5f, hu = Mathf.Abs(Vector3.Dot(u, size)) * 0.5f, hv = Mathf.Abs(Vector3.Dot(v, size)) * 0.5f;
                float w = rim ? Mathf.Min(band, 0.45f * 2f * Mathf.Min(hu, hv)) : 0f;
                if (w <= 0f)
                {
                    Quad(n, u, v, hn, -hu, hu, -hv, hv, body);
                    return;
                }
                Quad(n, u, v, hn, -hu + w, hu - w, -hv + w, hv - w, body); // the inner part
                Quad(n, u, v, hn, -hu, hu, -hv, -hv + w, edge);            // the rim: along the low side, the high side, then the two ends between
                Quad(n, u, v, hn, -hu, hu, hv - w, hv, edge);
                Quad(n, u, v, hn, -hu, -hu + w, -hv + w, hv - w, edge);
                Quad(n, u, v, hn, hu - w, hu, -hv + w, hv - w, edge);
            }
            // Each face (n, u, v) with u x v = -n (as GreyboxView's cube): its triangles go clockwise seen from outside, the engine's front.
            Face(Vector3.up, Vector3.right, Vector3.forward, true);
            Face(Vector3.down, Vector3.right, Vector3.back, false);
            Face(Vector3.right, Vector3.forward, Vector3.up, true);
            Face(Vector3.left, Vector3.back, Vector3.up, true);
            Face(Vector3.forward, Vector3.left, Vector3.up, true);
            Face(Vector3.back, Vector3.right, Vector3.up, true);
            var mesh = new Mesh { name = "RoomDoor_Block" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = 2;
            mesh.SetTriangles(body, 0);
            mesh.SetTriangles(edge, 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A lit colour (Simple Lit, as the props), no shine, glowing by <paramref name="glow"/> of its own colour (none at 0): the door orange of the greybox view, the body, the edge.</summary>
        static Material MakeMaterial(Rgb colour, float glow, string name)
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
            if (glow > 0f && m.HasProperty("_EmissionColor"))
            {
                Color lit = new Color(c.r * glow, c.g * glow, c.b * glow, 1f);
                m.SetColor("_EmissionColor", lit);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }
    }
}
