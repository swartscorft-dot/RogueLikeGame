using System.IO;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The WEAPON tab's model (work order 139): the high-detail original of the pistol she holds, the Meshy model of
    /// 2026-11-17 (470,344 triangles, its own textures and UV layout) that her in-hand copy (Art/Weapons/DesertEagle, 3,964
    /// triangles) was made from; the owner, 2026-11-26: "Vi lagde jo en egen model i meshy som karakteren holder i hånden.
    /// Hvorfor var ikke den igjenbrukt", and that the original has about 500,000 faces. Her pistol in a run stays the in-hand copy.
    ///
    /// The sources sit in <see cref="Folder"/>, outside Resources (everything inside Resources goes into a build, used or not; only
    /// what the prefab refers to goes in from here). On every import:
    /// - the textures: the base colour sRGB, the normal map as a normal map, the metallic and roughness maps and the packed map
    ///   linear; mip maps on, up to <see cref="MaxSize"/> (the files are 4096 and 2048: kept as they are);
    /// - the model with no materials of its own, not readable on the CPU;
    /// - when missing, made here: <see cref="MaskPath"/> (URP Lit's metallic and smoothness map: metallic in R, 1 - roughness in A,
    ///   from the two maps), <see cref="MaterialPath"/> (URP Lit with the colour, normal and packed maps, as her in-hand
    ///   material is made: metallic and smoothness from the map) and <see cref="PrefabPath"/> (the model with that material),
    ///   which is what <c>Resources.Load("Weapon/Pistol")</c> finds. Delete any of the three to make it again.
    /// </summary>
    public sealed class WeaponModelImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/Art/Weapons/PistolHighDetail/";
        public const string ModelPath = Folder + "PistolHighDetail.fbx";
        public const string BaseColorPath = Folder + "PistolHighDetail.png";
        public const string NormalPath = Folder + "PistolHighDetail_normal.png";
        public const string MetallicPath = Folder + "PistolHighDetail_metallic.png";
        public const string RoughnessPath = Folder + "PistolHighDetail_roughness.png";
        public const string MaskPath = Folder + "PistolHighDetail_urp_mask.png";
        public const string MaterialPath = Folder + "PistolHighDetail.mat";
        public const string PrefabFolder = "Assets/Game/Resources/Weapon";
        public const string PrefabPath = PrefabFolder + "/Pistol.prefab";
        public const int MaxSize = 4096;
        /// <summary>
        /// The material's smoothness over the map's (URP Lit multiplies them): a little under her in-hand material's 1, as the stage
        /// draws the model large and near and its painted light grey on the slide came out as chrome at 1 (the pictures of 2026-11-26).
        /// </summary>
        public const float Smoothness = 0.8f;

        static string Norm(string path) => path == null ? "" : path.Replace('\\', '/');
        static bool InFolder(string path) => Norm(path).StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!InFolder(assetPath)) return;
            var t = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool normal = name.EndsWith("_normal"), linear = normal || name.EndsWith("_metallic") || name.EndsWith("_roughness") || name.EndsWith("_urp_mask");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !linear;
            t.mipmapEnabled = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;
            t.isReadable = false;
            t.maxTextureSize = MaxSize;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
        }

        void OnPreprocessModel()
        {
            if (!InFolder(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.None;
            m.isReadable = false;
            m.importAnimation = false;
            m.importCameras = false;
            m.importLights = false;
            m.animationType = ModelImporterAnimationType.None;
            m.meshCompression = ModelImporterMeshCompression.Off;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool ours = false;
            foreach (string p in imported) ours |= InFolder(p) || Norm(p) == PrefabPath;
            foreach (string p in deleted) ours |= InFolder(p) || Norm(p) == PrefabPath;
            if (ours) EditorApplication.delayCall += MakeWhenMissing;
        }

        /// <summary>After each script reload: what is missing, made.</summary>
        [InitializeOnLoadMethod]
        static void AfterReload() => EditorApplication.delayCall += MakeWhenMissing;

        static void MakeWhenMissing()
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null
                || AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath) == null)
                MakeAll();
        }

        /// <summary>The packed map, the material and the prefab, each made when missing. False when the model or its colour is not there.</summary>
        public static bool MakeAll()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            if (model == null || baseColor == null) return false;
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath) == null && WriteMask())
                AssetDatabase.ImportAsset(MaskPath, ImportAssetOptions.ForceSynchronousImport);
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return false;
                material = new Material(lit) { name = "PistolHighDetail" };
                material.SetTexture("_BaseMap", baseColor);
                material.SetColor("_BaseColor", Color.white);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", 1f);
                    material.EnableKeyword("_NORMALMAP");
                }
                var mask = AssetDatabase.LoadAssetAtPath<Texture2D>(MaskPath);
                if (mask != null)
                {
                    material.SetTexture("_MetallicGlossMap", mask);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", Smoothness); // with the map: its alpha times this
                    material.SetFloat("_SmoothnessTextureChannel", 0f);
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                else material.SetFloat("_Smoothness", 0.3f);
                AssetDatabase.CreateAsset(material, MaterialPath);
                Debug.Log("WeaponModelImport: made " + MaterialPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
                if (!AssetDatabase.IsValidFolder(PrefabFolder)) AssetDatabase.CreateFolder("Assets/Game/Resources", "Weapon");
                var made = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    made.name = "Pistol";
                    foreach (Renderer r in made.GetComponentsInChildren<Renderer>(true))
                    {
                        var slots = new Material[Mathf.Max(1, r.sharedMaterials.Length)];
                        for (int i = 0; i < slots.Length; i++) slots[i] = material;
                        r.sharedMaterials = slots;
                    }
                    PrefabUtility.SaveAsPrefabAsset(made, PrefabPath);
                    Debug.Log("WeaponModelImport: made " + PrefabPath);
                }
                finally
                {
                    Object.DestroyImmediate(made);
                }
            }
            return true;
        }

        /// <summary>URP Lit's metallic and smoothness map from the two maps Meshy writes: metallic in R, 1 - roughness in A (read from the files, so their import settings do not matter).</summary>
        static bool WriteMask()
        {
            if (!File.Exists(MetallicPath) || !File.Exists(RoughnessPath)) return false;
            var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!metallic.LoadImage(File.ReadAllBytes(MetallicPath)) || !roughness.LoadImage(File.ReadAllBytes(RoughnessPath))) return false;
                if (metallic.width != roughness.width || metallic.height != roughness.height) return false;
                Color32[] m = metallic.GetPixels32(), r = roughness.GetPixels32();
                var px = new Color32[m.Length];
                for (int i = 0; i < px.Length; i++) px[i] = new Color32(m[i].r, 0, 0, (byte)(255 - r[i].r));
                var mask = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
                mask.SetPixels32(px);
                mask.Apply();
                File.WriteAllBytes(MaskPath, mask.EncodeToPNG());
                Object.DestroyImmediate(mask);
                return true;
            }
            finally
            {
                Object.DestroyImmediate(metallic);
                Object.DestroyImmediate(roughness);
            }
        }
    }
}
