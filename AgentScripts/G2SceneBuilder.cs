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
using UnityEditor.Animations;
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
    const string PlayerArtDir = Root + "/Art/Player";

    // The rigged player model and its clips, as downloaded from Meshy (rig task, animation task).
    const string RigDir = PlayerArtDir + "/Gunslinger";
    const string RiggedModelPath = RigDir + "/Gunslinger_Rigged.fbx";
    const string RunClipPath = RigDir + "/Gunslinger_Run.fbx";
    const string ActionClipsPath = RigDir + "/Gunslinger_Actions.fbx";
    const string ControllerPath = RigDir + "/Gunslinger.controller";

    // Timings read off these clips by sampling them (see docs/playtests/g2-control-agent.md).
    const float ShootFireTime = 2.4f;   // the draw ends and the hands are in front of the chest
    const float DashClipStart = 0.6f;   // the roll proper starts after a 0.6 s wind-up
    const float DashClipEnd = 1.83f;
    const float RunClipSpeed = 6f;      // a guess: the run cycle is 0.63 s

    /// <summary>Turn of the player model around the up axis, in degrees, so that its front points along the player's forward.</summary>
    const float PlayerModelYaw = 0f;

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

        // Player: the rigged model if it is there, else the newest static model in Assets/Game/Art/Player,
        // else a grey-box capsule. Either way the root stays 1 m above the ground, so control, camera and
        // tests do not depend on the visual.
        GameObject riggedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedModelPath);
        string modelPath = null;
        GameObject modelAsset = riggedAsset == null ? NewestPlayerModel(out modelPath) : null;
        GameObject player;
        string playerVisual;
        CharacterAnimation animation = null;
        if (riggedAsset != null)
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            playerVisual = "player visual: " + RiggedModelPath + "\n" + AttachRiggedModel(player, riggedAsset, out animation);
        }
        else if (modelAsset != null)
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            playerVisual = "player visual: " + modelPath + "\n" + AttachModel(player, modelAsset, modelPath);
        }
        else
        {
            player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0f, 1f, 0f);
            player.GetComponent<Renderer>().sharedMaterial = playerMat;
            Object.DestroyImmediate(player.GetComponent<Collider>());

            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Facing";
            nose.transform.SetParent(player.transform, false);
            nose.transform.localPosition = new Vector3(0f, 0.4f, 0.5f);
            nose.transform.localScale = new Vector3(0.3f, 0.3f, 0.4f);
            nose.GetComponent<Renderer>().sharedMaterial = playerMat;
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            playerVisual = "player visual: capsule\n";
        }
        var agent = player.AddComponent<NavMeshAgent>();
        agent.radius = 0.5f;
        agent.height = 2f;
        agent.baseOffset = 1f;
        var character = player.AddComponent<PlayerCharacter>();
        character.Settings = settings;
        character.BasicShotMaterial = basicShotMat;
        character.SkillMaterial = skillMat;
        var driver = player.AddComponent<PlayerInputDriver>();
        if (animation != null) animation.Player = character;

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

        return (settingsCreated ? "settings: created\n" : "settings: kept\n") + playerVisual + Dump();
    }

    /// <summary>The most recently written model file in the player art folder, or null when there is none.</summary>
    static GameObject NewestPlayerModel(out string path)
    {
        path = null;
        if (!AssetDatabase.IsValidFolder(PlayerArtDir)) return null;
        path = AssetDatabase.FindAssets("t:Model", new[] { PlayerArtDir })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderByDescending(p => System.IO.File.GetLastWriteTimeUtc(p))
            .ThenBy(p => p, System.StringComparer.Ordinal)
            .FirstOrDefault();
        return path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    /// <summary>
    /// Puts the model under the player as "Visual": feet on the ground, centred on the player, facing the
    /// player's forward. A model with no UVs cannot carry a texture, so it gets one flat material.
    /// </summary>
    static string AttachModel(GameObject player, GameObject modelAsset, string modelPath)
    {
        var visual = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, true);
        visual.transform.rotation = Quaternion.Euler(0f, PlayerModelYaw, 0f) * visual.transform.rotation;

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) return "  no renderer in the model\n";
        string look = ApplyLook(renderers, modelPath);

        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        Vector3 p = player.transform.position;
        visual.transform.position += new Vector3(p.x - bounds.center.x, -bounds.min.y, p.z - bounds.center.z);

        return string.Format(CultureInfo.InvariantCulture, "  size {0:0.00} x {1:0.00} x {2:0.00} m, {3} triangles, {4}\n",
            bounds.size.x, bounds.size.y, bounds.size.z, TriangleCount(renderers), look);
    }

    /// <summary>
    /// Puts the rigged model under the player as "Visual", with an Animator and the component that drives it.
    /// The rig's origin is between its feet, so it stands on the player's ground point as it is.
    /// </summary>
    static string AttachRiggedModel(GameObject player, GameObject riggedAsset, out CharacterAnimation animation)
    {
        ConfigureClips(RunClipPath, ("running", CharacterAnimation.RunState, true));
        ConfigureClips(ActionClipsPath,
            ("Idle", CharacterAnimation.IdleState, true),
            ("Cowboy_Quick_Draw_Shooting", CharacterAnimation.ShootState, false),
            ("Roll_Dodge", CharacterAnimation.DashState, false));
        AnimationClip idle = Clip(ActionClipsPath, CharacterAnimation.IdleState);
        AnimationClip run = Clip(RunClipPath, CharacterAnimation.RunState);
        AnimationClip shoot = Clip(ActionClipsPath, CharacterAnimation.ShootState);
        AnimationClip dash = Clip(ActionClipsPath, CharacterAnimation.DashState);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(riggedAsset);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, -player.transform.position.y, 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, PlayerModelYaw, 0f);

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        string look = ApplyLook(renderers, RiggedModelPath);

        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildController(idle, run, shoot, dash);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        animation = visual.AddComponent<CharacterAnimation>();
        animation.Animator = animator;
        animation.Hips = visual.GetComponentsInChildren<Transform>().FirstOrDefault(t => t.name == "Hips");
        animation.ShootFireTime = ShootFireTime;
        animation.DashClipStart = DashClipStart;
        animation.DashClipEnd = DashClipEnd;
        animation.RunClipSpeed = RunClipSpeed;

        string Length(AnimationClip c) => c != null ? c.length.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "MISSING";
        return string.Format(CultureInfo.InvariantCulture,
            "  {0} triangles, {1} bones, {2}\n  clips: idle {3}, run {4}, shoot {5}, dash {6}\n",
            TriangleCount(renderers), renderers.OfType<SkinnedMeshRenderer>().Sum(r => r.bones.Length), look,
            Length(idle), Length(run), Length(shoot), Length(dash));
    }

    /// <summary>Names the clips of a model file and sets which of them loop. Each rule: part of the take name, clip name, loop.</summary>
    static void ConfigureClips(string path, params (string take, string name, bool loop)[] rules)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return;
        bool changed = importer.clipAnimations.Length == 0;
        ModelImporterClipAnimation[] clips = changed ? importer.defaultClipAnimations : importer.clipAnimations;
        foreach (ModelImporterClipAnimation clip in clips)
        {
            foreach ((string take, string name, bool loop) in rules)
            {
                if (!clip.takeName.Contains(take)) continue;
                if (clip.name != name) { clip.name = name; changed = true; }
                if (clip.loopTime != loop) { clip.loopTime = loop; changed = true; }
                break;
            }
        }
        if (!changed) return;
        importer.clipAnimations = clips;
        importer.SaveAndReimport();
    }

    static AnimationClip Clip(string path, string name) =>
        AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>().FirstOrDefault(c => c.name == name);

    /// <summary>
    /// The Animator controller: one state per pose and no transitions, because CharacterAnimation
    /// cross-fades between the states itself. The asset is reused so its GUID stays stable across runs.
    /// </summary>
    static AnimatorController BuildController(AnimationClip idle, AnimationClip run, AnimationClip shoot, AnimationClip dash)
    {
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        foreach (ChildAnimatorState child in machine.states) machine.RemoveState(child.state);

        AnimatorState idleState = machine.AddState(CharacterAnimation.IdleState);
        idleState.motion = idle;
        machine.defaultState = idleState;
        machine.AddState(CharacterAnimation.RunState).motion = run;
        machine.AddState(CharacterAnimation.ShootState).motion = shoot;
        machine.AddState(CharacterAnimation.DashState).motion = dash;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static long TriangleCount(Renderer[] renderers)
    {
        long triangles = 0;
        foreach (Renderer r in renderers)
        {
            Mesh mesh = MeshOf(r);
            if (mesh == null) continue;
            for (int s = 0; s < mesh.subMeshCount; s++) triangles += mesh.GetIndexCount(s) / 3;
        }
        return triangles;
    }

    /// <summary>
    /// Gives the model its look and says which: its own materials when they came in with textures, a material
    /// built from the texture files next to it, or one flat material when it has no UVs or no textures.
    /// </summary>
    static string ApplyLook(Renderer[] renderers, string modelPath)
    {
        bool hasUvs = renderers.All(r => MeshOf(r) != null && MeshOf(r).HasVertexAttribute(VertexAttribute.TexCoord0));
        bool importedWithTextures = renderers.All(r => r.sharedMaterials.All(m => m != null && m.mainTexture != null));
        string look;
        Material replacement = null;
        if (!hasUvs)
        {
            replacement = MaterialAsset("PlayerModel", new Color(0.82f, 0.74f, 0.62f), unlit: false);
            look = "no UVs: flat material";
        }
        else if (importedWithTextures)
        {
            look = "the model's own materials";
        }
        else
        {
            // Meshy puts the textures next to the FBX but does not link them in it.
            replacement = TexturedMaterial(modelPath, out look);
            if (replacement == null)
            {
                replacement = MaterialAsset("PlayerModel", new Color(0.82f, 0.74f, 0.62f), unlit: false);
                look = "no texture files next to the model: flat material";
            }
        }
        if (replacement != null)
        {
            foreach (Renderer r in renderers)
                r.sharedMaterials = Enumerable.Repeat(replacement, r.sharedMaterials.Length).ToArray();
        }
        return look;
    }

    /// <summary>
    /// A URP Lit material from the texture files Meshy writes next to the FBX: "name.png" (colour),
    /// "name_normal.png", "name_metallic.png" and "name_roughness.png". URP wants metallic in the red
    /// channel and smoothness (1 - roughness) in alpha of one texture, so that mask is generated here.
    /// </summary>
    static Material TexturedMaterial(string modelPath, out string look)
    {
        string dir = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string name = System.IO.Path.GetFileNameWithoutExtension(modelPath);
        // Meshy's own export names the colour texture after the model; one extracted from an FBX is named after its material.
        string colourPath = new[] { $"{dir}/{name}.png", $"{dir}/{name}_baseColor.png", $"{dir}/Material_0_baseColor.png" }
            .FirstOrDefault(System.IO.File.Exists) ?? $"{dir}/{name}.png";
        string normalPath = $"{dir}/{name}_normal.png";
        string metallicPath = $"{dir}/{name}_metallic.png";
        string roughnessPath = $"{dir}/{name}_roughness.png";
        string maskPath = $"{dir}/{name}_urp_mask.png";
        look = null;
        if (!System.IO.File.Exists(colourPath)) return null;

        bool hasNormal = System.IO.File.Exists(normalPath);
        bool hasMask = WriteMask(metallicPath, roughnessPath, maskPath);

        ConfigureTexture(colourPath, TextureImporterType.Default, srgb: true);
        if (hasNormal) ConfigureTexture(normalPath, TextureImporterType.NormalMap, srgb: false);
        if (hasMask) ConfigureTexture(maskPath, TextureImporterType.Default, srgb: false);

        Material mat = MaterialAsset("PlayerModelTextured", Color.white, unlit: false);
        mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(colourPath));
        mat.SetTexture("_BumpMap", hasNormal ? AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath) : null);
        SetKeyword(mat, "_NORMALMAP", hasNormal);
        mat.SetTexture("_MetallicGlossMap", hasMask ? AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) : null);
        SetKeyword(mat, "_METALLICSPECGLOSSMAP", hasMask);
        mat.SetFloat("_Metallic", 0f);
        mat.SetFloat("_Smoothness", hasMask ? 1f : 0.2f); // with a mask this scales the mask's alpha
        EditorUtility.SetDirty(mat);

        look = "textures: colour" + (hasNormal ? ", normal" : "") + (hasMask ? ", metallic and smoothness" : "");
        return mat;
    }

    /// <summary>Writes the URP mask (R = metallic, A = 1 - roughness) when it is missing or older than its sources.</summary>
    static bool WriteMask(string metallicPath, string roughnessPath, string maskPath)
    {
        if (!System.IO.File.Exists(metallicPath) || !System.IO.File.Exists(roughnessPath)) return false;
        System.DateTime sources = System.IO.File.GetLastWriteTimeUtc(metallicPath);
        if (System.IO.File.GetLastWriteTimeUtc(roughnessPath) > sources) sources = System.IO.File.GetLastWriteTimeUtc(roughnessPath);
        if (System.IO.File.Exists(maskPath) && System.IO.File.GetLastWriteTimeUtc(maskPath) >= sources) return true;

        var metallic = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        var roughness = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
        try
        {
            metallic.LoadImage(System.IO.File.ReadAllBytes(metallicPath));
            roughness.LoadImage(System.IO.File.ReadAllBytes(roughnessPath));
            if (metallic.width != roughness.width || metallic.height != roughness.height) return false;

            Color32[] m = metallic.GetPixels32();
            Color32[] r = roughness.GetPixels32();
            for (int i = 0; i < m.Length; i++)
                m[i] = new Color32(m[i].r, 0, 0, (byte)(255 - r[i].r));
            var mask = new Texture2D(metallic.width, metallic.height, TextureFormat.RGBA32, false, true);
            mask.SetPixels32(m);
            System.IO.File.WriteAllBytes(maskPath, mask.EncodeToPNG());
            Object.DestroyImmediate(mask);
        }
        finally
        {
            Object.DestroyImmediate(metallic);
            Object.DestroyImmediate(roughness);
        }
        AssetDatabase.ImportAsset(maskPath);
        return true;
    }

    /// <summary>Import settings for a model texture. 2048 is plenty for a figure the camera sees from 14 m.</summary>
    static void ConfigureTexture(string path, TextureImporterType type, bool srgb)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        bool changed = false;
        if (importer.textureType != type) { importer.textureType = type; changed = true; }
        if (type == TextureImporterType.Default && importer.sRGBTexture != srgb) { importer.sRGBTexture = srgb; changed = true; }
        if (importer.maxTextureSize != 2048) { importer.maxTextureSize = 2048; changed = true; }
        if (changed) importer.SaveAndReimport();
    }

    static void SetKeyword(Material mat, string keyword, bool on)
    {
        if (on) mat.EnableKeyword(keyword);
        else mat.DisableKeyword(keyword);
    }

    static Mesh MeshOf(Renderer r)
    {
        if (r is SkinnedMeshRenderer skinned) return skinned.sharedMesh;
        var filter = r.GetComponent<MeshFilter>();
        return filter != null ? filter.sharedMesh : null;
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
