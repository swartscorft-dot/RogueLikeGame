// G2 scene builder. Run through the Pipeline:
//   unity command script run -- --file AgentScripts/G2SceneBuilder.cs --entry G2SceneBuilder.Build
// Rebuilds Assets/Game/Scenes/ControlRoom.unity from nothing on every run, so the result does not
// depend on what the scene looked like before. Returns a hierarchy dump for comparison between runs.
//
// The feel settings asset is created only when it is missing. It holds Kristian's tuning and is never overwritten.
using System.Globalization;
using System.Linq;
using System.Text;
using Roguelike;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

public static class G2SceneBuilder
{
    const string Root = "Assets/Game";
    const string SceneDir = Root + "/Scenes";
    const string MaterialDir = Root + "/Materials";
    const string SettingsDir = Root + "/Settings";
    const string ScenePath = SceneDir + "/ControlRoom.unity";
    const string NavMeshPath = SceneDir + "/ControlRoom_NavMesh.asset";
    const string SettingsPath = SettingsDir + "/FeelSettings.asset";

    public static string Build()
    {
        EnsureFolder(Root, "Scenes");
        EnsureFolder(Root, "Materials");
        EnsureFolder(Root, "Settings");

        Material floorMat = MaterialAsset("Floor", new Color(0.45f, 0.45f, 0.47f), unlit: false);
        Material playerMat = MaterialAsset("Player", new Color(0.15f, 0.45f, 0.95f), unlit: false);
        Material dummyMat = MaterialAsset("Dummy", new Color(0.85f, 0.2f, 0.15f), unlit: false);
        Material basicShotMat = MaterialAsset("BasicShot", new Color(1f, 0.95f, 0.6f), unlit: true);
        Material skillMat = MaterialAsset("Skill", new Color(1f, 0.6f, 0.1f), unlit: true);
        Material ringMat = MaterialAsset("RangeRing", new Color(0.95f, 0.9f, 0.2f), unlit: true);

        bool settingsCreated = false;
        var settings = AssetDatabase.LoadAssetAtPath<FeelSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<FeelSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            settingsCreated = true;
        }

        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Light
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.shadows = LightShadows.Soft;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

        // Floor: a 40 x 40 m plane with its top at y = 0, carrying the NavMeshSurface.
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Floor";
        floor.transform.localScale = new Vector3(4f, 1f, 4f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMat;
        var surface = floor.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children; // the floor only; actors are not walkable geometry
        surface.useGeometry = NavMeshCollectGeometry.RenderMeshes;

        // Player
        GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        player.name = "Player";
        player.transform.position = new Vector3(0f, 1f, 0f);
        player.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(player.GetComponent<Collider>());
        var agent = player.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 1f;
        var character = player.AddComponent<PlayerCharacter>();
        character.Settings = settings;
        character.BasicShotMaterial = basicShotMat;
        character.SkillMaterial = skillMat;
        var driver = player.AddComponent<PlayerInputDriver>();

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Facing";
        nose.transform.SetParent(player.transform, false);
        nose.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
        nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.4f);
        nose.GetComponent<Renderer>().sharedMaterial = playerMat;
        Object.DestroyImmediate(nose.GetComponent<Collider>());

        // Range ring, drawn in world space around the player while aiming an attack-move.
        var ringGo = new GameObject("RangeRing");
        var line = ringGo.AddComponent<LineRenderer>();
        line.sharedMaterial = ringMat;
        line.widthMultiplier = 0.08f;
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;
        line.enabled = false;
        var ring = ringGo.AddComponent<RangeIndicator>();
        ring.Player = character;
        driver.RangeRing = ring;

        // Training dummies: A and B are in basic-attack range of the start position, C is not.
        Dummy("Dummy_A", new Vector3(4f, 1f, 3f), dummyMat);
        Dummy("Dummy_B", new Vector3(-4f, 1f, 3f), dummyMat);
        Dummy("Dummy_C", new Vector3(0f, 1f, 10f), dummyMat);

        // Camera
        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGo.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.12f, 0.13f, 0.16f);
        var rig = camGo.AddComponent<CameraRig>();
        rig.Target = player.transform;
        rig.Settings = settings;
        Quaternion camRotation = Quaternion.Euler(settings.Values.CameraPitch, 0f, 0f);
        camGo.transform.SetPositionAndRotation(-(camRotation * Vector3.forward) * settings.Values.CameraDistance, camRotation);
        cam.fieldOfView = settings.Values.FieldOfView;

        // Debug readout
        new GameObject("DebugReadout").AddComponent<DebugReadout>();

        // Bake and persist the NavMesh. Reuse the existing asset so its GUID stays stable across runs.
        surface.BuildNavMesh();
        var existing = AssetDatabase.LoadAssetAtPath<NavMeshData>(NavMeshPath);
        if (existing != null)
        {
            surface.RemoveData();
            EditorUtility.CopySerialized(surface.navMeshData, existing);
            surface.navMeshData = existing;
            surface.AddData();
            EditorUtility.SetDirty(existing);
        }
        else
        {
            AssetDatabase.CreateAsset(surface.navMeshData, NavMeshPath);
        }

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        return (settingsCreated ? "settings: created\n" : "settings: kept\n") + Dump();
    }

    static void Dummy(string name, Vector3 position, Material material)
    {
        GameObject dummy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        dummy.name = name;
        dummy.transform.position = position;
        dummy.GetComponent<Renderer>().sharedMaterial = material;
        Object.DestroyImmediate(dummy.GetComponent<Collider>());
        dummy.AddComponent<Damageable>().Radius = 0.5f;
        dummy.AddComponent<TrainingDummy>();
        var obstacle = dummy.AddComponent<NavMeshObstacle>();
        obstacle.shape = NavMeshObstacleShape.Capsule;
        obstacle.radius = 0.5f;
        obstacle.height = 2f;
        obstacle.carving = true;
    }

    /// <summary>Hierarchy of the active scene: names, positions and component types, in order.</summary>
    public static string Dump()
    {
        var sb = new StringBuilder();
        foreach (GameObject go in EditorSceneManager.GetActiveScene().GetRootGameObjects())
            DumpRecursive(go.transform, 0, sb);
        return sb.ToString();
    }

    static void DumpRecursive(Transform t, int depth, StringBuilder sb)
    {
        string comps = string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name));
        Vector3 p = t.position;
        sb.Append(' ', depth * 2).Append(t.name)
          .Append(string.Format(CultureInfo.InvariantCulture, " ({0:0.##}, {1:0.##}, {2:0.##}) [", p.x, p.y, p.z))
          .Append(comps).Append("]\n");
        for (int i = 0; i < t.childCount; i++) DumpRecursive(t.GetChild(i), depth + 1, sb);
    }

    static Material MaterialAsset(string name, Color color, bool unlit)
    {
        string path = $"{MaterialDir}/{name}.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat == null)
        {
            Shader shader = unlit ? Shader.Find("Universal Render Pipeline/Unlit") : null;
            if (shader == null)
            {
                shader = GraphicsSettings.currentRenderPipeline != null
                    ? GraphicsSettings.currentRenderPipeline.defaultMaterial.shader
                    : Shader.Find("Standard");
            }
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (mat.color != color)
        {
            mat.color = color;
            EditorUtility.SetDirty(mat);
        }
        return mat;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
