using System.IO;
using Roguelike.Rules;
using UnityEditor;
using UnityEngine;

namespace Roguelike.EditorTools
{
    /// <summary>
    /// The nine weapon parts' models on the WEAPON tab's stage (work order 150; Kristian, 2026-11-26: "Ja, lag de seks og bygg
    /// inn"): the Blender files of 2026-11-26 and 27 (the three sights, the three barrels, the three grips; one mesh each, in the
    /// pistol's own units and axes, its origin at its mount, the materials Gunmetal, Bronze and Teal by name, the laser's
    /// LaserRed and the reflex's Glass, a vertex colour layer Wear), copied into <see cref="Folder"/>, outside Resources, as the
    /// pistol's model is (<see cref="WeaponModelImport"/>). On every import of one of them the settings their .meta files hold
    /// are kept (written here too, so a lost .meta comes back the same):
    /// - no animation, no rig, no cameras, lights, blend shapes or visibility curves: the files hold none, and none is wanted;
    /// - not readable on the CPU (the stage only draws the mesh and reads its bounds);
    /// - the scale and the axes as the pistol's (the file's units, scale 1, the axis turn on the root, not baked): so a part laid
    ///   as the pistol is sits on it as it was modelled;
    /// - the normals as exported (Blender's hard and smooth edges), tangents calculated (no normal map: harmless);
    /// - the vertex colours imported (the Wear layer, for a shader that may show it later; URP Lit does not);
    /// - the materials embedded in the model (not loose files): the stage reads only their names and draws its own.
    /// When missing, made here: the prefab of each model in <see cref="PrefabFolder"/>, which is what
    /// <c>Resources.Load(WeaponStageValues.PartModelPath(id))</c> finds, in a build too. Delete one to make it again.
    /// </summary>
    public sealed class WeaponPartsImport : AssetPostprocessor
    {
        public const string Folder = "Assets/Game/Art/Weapons/Parts/";
        public const string PrefabFolder = "Assets/Game/Resources/Weapon/Parts";

        /// <summary>The nine models' names, as <see cref="WeaponStageValues.PartModelName"/> gives them.</summary>
        public static readonly string[] Names =
        {
            "LaserSight", "GhostRing", "ReflexSight", "BullBarrel", "LongBarrel", "Compensator", "RunnersGrip", "WeightedGrip", "QuickDrawGrip",
        };

        static string Norm(string path) => path == null ? "" : path.Replace('\\', '/');
        static bool InFolder(string path) => Norm(path).StartsWith(Folder, System.StringComparison.OrdinalIgnoreCase);
        static string ModelPath(string name) => Folder + name + ".fbx";
        static string PrefabPath(string name) => PrefabFolder + "/" + name + ".prefab";

        void OnPreprocessModel()
        {
            if (!InFolder(assetPath)) return;
            var m = (ModelImporter)assetImporter;
            m.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            m.materialLocation = ModelImporterMaterialLocation.InPrefab;
            m.isReadable = false;
            m.importAnimation = false;
            m.animationType = ModelImporterAnimationType.None;
            m.importCameras = false;
            m.importLights = false;
            m.importBlendShapes = false;
            m.importVisibility = false;
            m.globalScale = 1f;
            m.useFileScale = true;
            m.bakeAxisConversion = false;
            m.importNormals = ModelImporterNormals.Import;
            m.importTangents = ModelImporterTangents.CalculateMikk;
            m.meshCompression = ModelImporterMeshCompression.Off;
            m.addCollider = false;
            m.generateSecondaryUV = false;
        }

        static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            bool ours = false;
            foreach (string p in imported) ours |= InFolder(p) || Norm(p).StartsWith(PrefabFolder + "/");
            foreach (string p in deleted) ours |= InFolder(p) || Norm(p).StartsWith(PrefabFolder + "/");
            if (ours) EditorApplication.delayCall += MakeWhenMissing;
        }

        /// <summary>After each script reload: what is missing, made.</summary>
        [InitializeOnLoadMethod]
        static void AfterReload() => EditorApplication.delayCall += MakeWhenMissing;

        static void MakeWhenMissing() => MakeAll();

        /// <summary>Each part's prefab, made when it is missing and its model is there. The number made.</summary>
        public static int MakeAll()
        {
            int made = 0;
            foreach (string name in Names)
            {
                if (AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath(name)) != null) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name));
                if (model == null) continue;
                if (!AssetDatabase.IsValidFolder(PrefabFolder))
                {
                    if (!AssetDatabase.IsValidFolder("Assets/Game/Resources/Weapon")) AssetDatabase.CreateFolder("Assets/Game/Resources", "Weapon");
                    AssetDatabase.CreateFolder("Assets/Game/Resources/Weapon", Path.GetFileName(PrefabFolder));
                }
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
                try
                {
                    instance.name = name;
                    PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath(name));
                    made++;
                    Debug.Log("WeaponPartsImport: made " + PrefabPath(name));
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                }
            }
            return made;
        }
    }
}
