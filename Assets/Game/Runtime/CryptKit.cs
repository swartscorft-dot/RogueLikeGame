using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;

namespace Roguelike
{
    /// <summary>The pieces of the crypt wall kit (work order 016), each a model under Resources/Environment/CryptKit/.</summary>
    public enum CryptPiece { Wall, WallNiche, LowWall, Pier, Post, Arch, Lantern }

    /// <summary>
    /// Makes the pieces of the crypt wall kit that dress a generated floor (work order 016, 2026-11-20), the way
    /// <see cref="PropFactory"/> makes the props: the model loaded by name from Resources at run time, its texture
    /// "_base_color" beside it on a URP Simple Lit material made here (white, or a faint tint, no smoothness and no
    /// highlights), and its "_emission" map on the same material in the game's cyan.
    ///
    /// A piece is only a picture: a mesh renderer, no collider, no <see cref="Cover"/> and no <see cref="Shelter"/>, on
    /// <see cref="Layer"/> (Ignore Raycast), which no physics query of the game asks with its default layers and which no
    /// cover stands on. Its meshes are the imported model's own (every piece of a model shares them); the import's
    /// GameObject is never copied, so no Animator or other component comes with it. A piece is placed by its pivot
    /// (the model's origin), turned about its up axis, the model's own turn and scale from the import kept under it.
    ///
    /// The floors: one Simple Lit material per room kind with that kind's floor texture, tinted faintly, its tiling set
    /// per floor box so that one copy covers <see cref="CryptKitValues.FloorTile"/> metres and lines up with the grid.
    /// </summary>
    public static class CryptKit
    {
        public const string Folder = "Environment/CryptKit/";
        /// <summary>The layer every piece is on: Unity's Ignore Raycast (2), which <see cref="Physics.DefaultRaycastLayers"/> leaves out.</summary>
        public const int Layer = 2;

        /// <summary>The model's name, which is its folder's and its file's.</summary>
        public static string NameOf(CryptPiece piece)
        {
            switch (piece)
            {
                case CryptPiece.Wall: return "Crypt_Wall_A";
                case CryptPiece.WallNiche: return "Crypt_Wall_B";
                case CryptPiece.LowWall: return "Crypt_WallLow_A";
                case CryptPiece.Pier: return "Crypt_WallPier_01";
                case CryptPiece.Post: return "Crypt_WallPost_01";
                case CryptPiece.Arch: return "Crypt_DoorArch_01";
                default: return "Crypt_WallLantern_01";
            }
        }

        public static string ResourcePath(CryptPiece piece) => Folder + NameOf(piece) + "/" + NameOf(piece);
        public static string BaseTexturePath(CryptPiece piece) => ResourcePath(piece) + "_base_color";
        public static string EmissionTexturePath(CryptPiece piece) => ResourcePath(piece) + "_emission";

        /// <summary>The floor texture of a room kind (work order 016): the calm A in the entrance and combat rooms, B in the exit, the ornate C in treasure rooms only.</summary>
        public static char FloorLetter(RoomKind kind) => kind == RoomKind.Treasure ? 'C' : kind == RoomKind.Exit ? 'B' : 'A';
        public static string FloorTexturePath(RoomKind kind) => Folder + "Crypt_Floor/Crypt_Floor_" + FloorLetter(kind) + "_base_color";

        /// <summary>A model as loaded: its meshes, where each sits in a piece's frame, and the bounds of them all there.</summary>
        public sealed class Model
        {
            public CryptPiece Piece;
            public string Name;
            public GameObject Source;
            public Mesh[] Meshes;
            public Matrix4x4[] Places;
            /// <summary>In the frame of a piece placed unturned at the origin: the model's length along x, its front +Z, up y.</summary>
            public Bounds Bounds;
            public long Triangles;
            /// <summary>One mesh with nothing between it and the piece: the piece itself carries it.</summary>
            public bool OnRoot;
        }

        static readonly Dictionary<CryptPiece, Model> s_models = new Dictionary<CryptPiece, Model>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_models.Clear();

        /// <summary>The model, loaded and measured once; null when it is not in the project or has no mesh.</summary>
        public static Model Load(CryptPiece piece)
        {
            if (s_models.TryGetValue(piece, out Model m) && m != null && m.Source != null) return m;
            m = null;
            GameObject source = Resources.Load<GameObject>(ResourcePath(piece));
            if (source != null)
            {
                Transform root = source.transform;
                Matrix4x4 fromRoot = Matrix4x4.TRS(Vector3.zero, root.localRotation, root.localScale) * root.worldToLocalMatrix;
                var meshes = new List<Mesh>();
                var places = new List<Matrix4x4>();
                foreach (MeshFilter f in source.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (f.sharedMesh == null) continue;
                    meshes.Add(f.sharedMesh);
                    places.Add(fromRoot * f.transform.localToWorldMatrix);
                }
                if (meshes.Count > 0)
                {
                    m = new Model { Piece = piece, Name = NameOf(piece), Source = source, Meshes = meshes.ToArray(), Places = places.ToArray() };
                    bool first = true;
                    for (int i = 0; i < meshes.Count; i++)
                    {
                        Bounds b = meshes[i].bounds;
                        for (int k = 0; k < 8; k++)
                        {
                            Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((k & 1) == 0 ? -1 : 1, (k & 2) == 0 ? -1 : 1, (k & 4) == 0 ? -1 : 1));
                            Vector3 p = places[i].MultiplyPoint3x4(corner);
                            if (first) m.Bounds = new Bounds(p, Vector3.zero);
                            else m.Bounds.Encapsulate(p);
                            first = false;
                        }
                        for (int s = 0; s < meshes[i].subMeshCount; s++) m.Triangles += meshes[i].GetIndexCount(s) / 3;
                    }
                    m.OnRoot = meshes.Count == 1 && IsIdentity(places[0]);
                }
            }
            s_models[piece] = m;
            return m;
        }

        static bool IsIdentity(Matrix4x4 m)
        {
            for (int r = 0; r < 4; r++)
                for (int c = 0; c < 4; c++)
                    if (Mathf.Abs(m[r, c] - (r == c ? 1f : 0f)) > 1e-4f) return false;
            return true;
        }

        /// <summary>The texture file by name, else the one the import's material has.</summary>
        public static Texture2D BaseTexture(CryptPiece piece)
        {
            Texture2D t = Resources.Load<Texture2D>(BaseTexturePath(piece));
            if (t != null) return t;
            Model m = Load(piece);
            Renderer r = m != null ? m.Source.GetComponentInChildren<Renderer>() : null;
            Material mat = r != null ? r.sharedMaterial : null;
            return mat == null ? null : (mat.HasProperty("_BaseMap") ? mat.GetTexture("_BaseMap") : mat.mainTexture) as Texture2D;
        }

        public static Texture2D EmissionTexture(CryptPiece piece) => Resources.Load<Texture2D>(EmissionTexturePath(piece));

        /// <summary>A colour as picked, for the engine.</summary>
        public static Color Picked(Rgb c) => new Color(c.R, c.G, c.B, 1f);

        /// <summary>The colour a shader is given for a picked colour times a glow: in linear light when the project is.</summary>
        public static Color Glowing(Rgb c, float glow)
        {
            Color p = Picked(c);
            Color l = QualitySettings.activeColorSpace == ColorSpace.Linear ? p.linear : p;
            float k = Mathf.Max(0f, glow);
            return new Color(l.r * k, l.g * k, l.b * k, 1f);
        }

        static Material SimpleLit(string name, Texture texture, Color tint, Shader fallback)
        {
            Shader shader = Shader.Find(PropFactory.ShaderName);
            if (shader == null) shader = fallback != null ? fallback : Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(shader) { name = name };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", texture);
            m.mainTexture = texture;
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            m.color = tint;
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0f);
            if (m.HasProperty("_SpecularHighlights")) m.SetFloat("_SpecularHighlights", 0f);
            m.DisableKeyword("_SPECULAR_COLOR");
            m.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            return m;
        }

        /// <summary>
        /// A new material for a piece (the caller destroys it): its painted texture, the pale-stone tint on the pier and
        /// the arch, and its emission map in the game's cyan at the stone's glow, or the lantern's.
        /// </summary>
        public static Material MakeMaterial(CryptPiece piece, CryptKitValues values)
        {
            Model model = Load(piece);
            Renderer r = model != null ? model.Source.GetComponentInChildren<Renderer>() : null;
            Shader fallback = r != null && r.sharedMaterial != null ? r.sharedMaterial.shader : null;
            bool pale = piece == CryptPiece.Pier || piece == CryptPiece.Arch;
            Material m = SimpleLit("CryptKit_" + NameOf(piece), BaseTexture(piece), pale ? Picked(values.PaleStoneTint) : Color.white, fallback);
            Texture2D emission = EmissionTexture(piece);
            if (emission != null && m.HasProperty("_EmissionColor"))
            {
                float glow = piece == CryptPiece.Lantern ? values.LanternGlow : values.StoneGlow;
                m.SetTexture("_EmissionMap", emission);
                // SetColor takes a colour as picked and turns it into linear light itself, so it is given the one that turns into the glow.
                Color lit = Glowing(values.Cyan, glow);
                m.SetColor("_EmissionColor", QualitySettings.activeColorSpace == ColorSpace.Linear ? lit.gamma : lit);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            }
            return m;
        }

        /// <summary>
        /// A new floor material for a room kind (the caller destroys it): its floor texture, its faint tint; the elite room
        /// its kind's texture with a faint tint of its own (work order 017).
        /// </summary>
        public static Material MakeFloorMaterial(RoomKind kind, CryptKitValues values, bool elite = false)
        {
            Rgb tint = elite ? values.EliteFloorTint
                : kind == RoomKind.Entrance ? values.EntranceFloorTint
                : kind == RoomKind.Combat || kind == RoomKind.Passage ? values.CombatFloorTint // work order 059: a passage the combat rooms' floor
                : kind == RoomKind.Treasure ? values.TreasureFloorTint
                : values.ExitFloorTint;
            Texture2D texture = Resources.Load<Texture2D>(FloorTexturePath(kind));
            if (texture != null)
            {
                // Seen at a flat angle (work order 026): filtered on the loaded texture; its import settings stay as they are.
                texture.anisoLevel = values.FloorAnisoLevel;
                if (values.FloorTrilinear) texture.filterMode = FilterMode.Trilinear;
            }
            return SimpleLit("CryptKit_Floor_" + kind + (elite ? "_Elite" : ""), texture, Picked(tint), null);
        }

        static int s_uAlongX; // 0 not known yet, 1 yes, -1 no

        /// <summary>
        /// True when the top face of Unity's cube (the floor box) has its texture's u along the cube's x; then a floor
        /// box's tiling is its width and its depth in tiles, else the two the other way round. Read from the mesh once.
        /// </summary>
        public static bool TopFaceUAlongX(Mesh cube)
        {
            if (s_uAlongX != 0) return s_uAlongX > 0;
            bool along = true;
            if (cube != null && cube.isReadable)
            {
                Vector3[] v = cube.vertices;
                Vector3[] n = cube.normals;
                Vector2[] uv = cube.uv;
                int first = -1;
                for (int i = 0; i < v.Length && i < n.Length && i < uv.Length; i++)
                {
                    if (n[i].y < 0.9f) continue;
                    if (first < 0)
                    {
                        first = i;
                        continue;
                    }
                    if (Mathf.Abs(v[i].z - v[first].z) > 1e-4f || Mathf.Abs(v[i].x - v[first].x) < 0.5f) continue;
                    float dx = v[i].x - v[first].x;
                    along = Mathf.Abs((uv[i].x - uv[first].x) / dx) >= Mathf.Abs((uv[i].y - uv[first].y) / dx);
                    break;
                }
            }
            s_uAlongX = along ? 1 : -1;
            return along;
        }

        /// <summary>
        /// A piece under <paramref name="parent"/>, at a world point and turn, scaled in its own frame by
        /// <paramref name="scale"/>, with <paramref name="material"/> on every mesh, on <see cref="Layer"/>; no collider.
        /// </summary>
        public static GameObject Make(Model model, Material material, Transform parent, string name, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var go = new GameObject(name) { layer = Layer };
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(position, rotation);
            go.transform.localScale = scale;
            for (int i = 0; i < model.Meshes.Length; i++)
            {
                GameObject holder = go;
                if (!model.OnRoot)
                {
                    holder = new GameObject("Model") { layer = Layer };
                    holder.transform.SetParent(go.transform, false);
                    Matrix4x4 p = model.Places[i];
                    holder.transform.localPosition = p.GetColumn(3);
                    holder.transform.localRotation = p.rotation;
                    holder.transform.localScale = p.lossyScale;
                }
                holder.AddComponent<MeshFilter>().sharedMesh = model.Meshes[i];
                var r = holder.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }
    }
}
