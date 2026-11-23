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
    /// Its look is the greybox view's door orange (<see cref="GreyboxValues.Door"/>), lit and glowing a little in it, in the
    /// game and in the greybox view alike (which draws it as it is). Its material is its own, destroyed with it. Its frame
    /// allocates nothing; the floor's controller moves what is seen while it rises or sinks (<see cref="Show"/>).
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

        Material _material;

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

            GameObject block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            DestroyImmediate(block.GetComponent<Collider>()); // what is seen has no collider: the root's box is the door
            block.name = "Block";
            block.layer = level.WallLayer;
            block.transform.SetParent(go.transform, false);
            block.transform.localScale = size;
            d.Block = block.transform;
            d.BlockRenderer = block.GetComponent<Renderer>();
            d._material = MakeMaterial((looks ?? new GreyboxValues()).Door, values != null ? values.DoorGlow : 0f);
            d.BlockRenderer.sharedMaterial = d._material;
            d.SetShut(false);
            d.Show(0f);
            if (level.Cave != null) level.Cave.TrackDoor(d.BlockRenderer, door.RoomA, door.RoomB); // drawn with its rooms, as the floor's pieces are
            return d;
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
        }

        /// <summary>The door orange of the greybox view, lit (Simple Lit, as the props), glowing by <paramref name="glow"/> of its own colour.</summary>
        static Material MakeMaterial(Rgb colour, float glow)
        {
            Shader shader = Shader.Find(PropFactory.ShaderName) ?? Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = "RoomDoor" };
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
