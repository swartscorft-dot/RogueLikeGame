using System.IO;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The import of the WEAPON tab's model (work order 139): her pistol remade from Meshy and fixed in Blender
    /// (models-incoming/pistol-2026-11-25/fixed, its notes.txt), the full model of 157,084 triangles (the owner asked for it "i høy
    /// res" on the stage), in <see cref="Folder"/>. On every import:
    /// - its textures as the notes say: the base colour and the emission sRGB, the normal map as a normal map, the metallic and
    ///   smoothness map (metallic in R, smoothness in A) linear; mip maps on, up to <see cref="MaxSize"/> (the files are 4096 and
    ///   2048; the stage draws the model up to about 1,900 pixels wide, so 4096 keeps it sharp when it is wheeled near);
    /// - the model with no materials of its own (the FBX does not carry the emission map: the material is made here) and not
    ///   readable on the CPU;
    /// - when the model and its textures are in, and the material or the prefab is missing, they are made: <see cref="MaterialPath"/>
    ///   (URP Lit with the four maps, emission white at 1 as exported) and <see cref="PrefabPath"/> (the model with that material),
    ///   which is what <c>Resources.Load("Weapon/Pistol")</c> finds. Made once; delete them to make them again.
    /// The pistol in her hands in a run stays the DesertEagle: this model is the tab's only.
    /// </summary>
    public sealed class WeaponModelImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/Resources/Weapon/";
        public const string ModelPath = Folder + "pistol_fixed.fbx";
        public const string BaseColorPath = Folder + "pistol_base_color.png";
        public const string NormalPath = Folder + "pistol_normal.png";
        public const string MetallicPath = Folder + "pistol_metallic_smoothness.png";
        public const string EmissionPath = Folder + "pistol_emission.png";
        public const string MaterialPath = Folder + "PistolStage.mat";
        public const string PrefabPath = Folder + "Pistol.prefab";
        public const int MaxSize = 4096;

        static bool Takes(string path) => path != null && path.Replace('\\', '/').StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase);

        void OnPreprocessTexture()
        {
            if (!Takes(assetPath)) return;
            var t = (TextureImporter)assetImporter;
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool normal = name.Contains("normal"), linear = normal || name.Contains("metallic");
            t.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            t.sRGBTexture = !linear;
            t.mipmapEnabled = true;
            t.alphaSource = TextureImporterAlphaSource.FromInput;
            t.alphaIsTransparency = false;
            t.isReadable = false;
            t.maxTextureSize = MaxSize;
            t.textureCompression = TextureImporterCompression.CompressedHQ;
            t.wrapMode = TextureWrapMode.Repeat;
            t.filterMode = FilterMode.Trilinear;
            t.anisoLevel = 4;
        }

        void OnPreprocessModel()
        {
            if (!Takes(assetPath)) return;
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
            foreach (string p in imported) ours |= Takes(p);
            foreach (string p in deleted) ours |= Takes(p); // the material or the prefab deleted: made again
            if (!ours) return;
            MakeMaterialAndPrefab();
        }

        /// <summary>After each script reload: the material and the prefab made if they are missing and their sources are there (an import of the model alone may come before the import rule's own code is in).</summary>
        [InitializeOnLoadMethod]
        static void MakeWhenMissing() => EditorApplication.delayCall += () =>
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null || AssetDatabase.LoadAssetAtPath<Material>(MaterialPath) == null) MakeMaterialAndPrefab();
        };

        /// <summary>The material and the prefab, made when missing (also for a test).</summary>
        public static void MakeMaterialAndPrefab()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
            var baseColor = AssetDatabase.LoadAssetAtPath<Texture2D>(BaseColorPath);
            if (model == null || baseColor == null) return; // not all in yet: the next import makes them
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit");
                if (lit == null) return;
                material = new Material(lit) { name = "PistolStage" };
                material.SetTexture("_BaseMap", baseColor);
                material.SetColor("_BaseColor", Color.white);
                var normal = AssetDatabase.LoadAssetAtPath<Texture2D>(NormalPath);
                if (normal != null)
                {
                    material.SetTexture("_BumpMap", normal);
                    material.SetFloat("_BumpScale", 1f);
                    material.EnableKeyword("_NORMALMAP");
                }
                var metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(MetallicPath);
                if (metallic != null)
                {
                    material.SetTexture("_MetallicGlossMap", metallic);
                    material.SetFloat("_Smoothness", 1f); // with the map: its alpha times this
                    material.SetFloat("_SmoothnessTextureChannel", 0f); // the metallic map's alpha
                    material.EnableKeyword("_METALLICSPECGLOSSMAP");
                }
                var emission = AssetDatabase.LoadAssetAtPath<Texture2D>(EmissionPath);
                if (emission != null)
                {
                    material.SetTexture("_EmissionMap", emission);
                    material.SetColor("_EmissionColor", Color.white); // the map's own teal, at 1 as exported
                    material.EnableKeyword("_EMISSION");
                    // URP keeps _EMISSION only on a material whose light flags say it gives light (BaseShaderGUI.SetupMaterialKeywords):
                    // with None its import took the keyword off and the teal strip did not glow (the first material, 2026-11-26).
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                AssetDatabase.CreateAsset(material, MaterialPath);
                Debug.Log("WeaponModelImport: made " + MaterialPath);
            }
            if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath) == null)
            {
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
        }
    }
}
