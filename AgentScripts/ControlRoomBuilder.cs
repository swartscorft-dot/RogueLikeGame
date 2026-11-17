// Builds the control room, the one scene of G2, G3 and G4. Run through the Pipeline:
//   unity command script run -- --file AgentScripts/ControlRoomBuilder.cs --entry ControlRoomBuilder.Build
// Rebuilds Assets/Game/Scenes/ControlRoom.unity from nothing on every run, so the result does not
// depend on what the scene looked like before. Returns a hierarchy dump for comparison between runs.
//
// The feel, skill and enemy settings assets are created only when they are missing.
// They hold Kristian's tuning and are never overwritten. An older skill asset gets the skills it lacks.
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

public static class ControlRoomBuilder
{
    const string Root = "Assets/Game";
    const string SceneDir = Root + "/Scenes";
    const string MaterialDir = Root + "/Materials";
    const string SettingsDir = Root + "/Settings";
    const string ScenePath = SceneDir + "/ControlRoom.unity";
    const string NavMeshPath = SceneDir + "/ControlRoom_NavMesh.asset";
    const string SettingsPath = SettingsDir + "/FeelSettings.asset";
    const string SkillsPath = SettingsDir + "/Skills.asset";
    const string EnemyPath = SettingsDir + "/Enemy.asset";
    const string PlayerArtDir = Root + "/Art/Player";

    // The player figure Kristian made in Meshy on 2026-11-17 (the Outrider), rigged through Meshy, and her weapons.
    const string RigDir = PlayerArtDir + "/Outrider";
    const string RiggedModelPath = RigDir + "/Outrider_Rigged.fbx";
    const string RunClipPath = RigDir + "/Outrider_Run.fbx";
    const string ActionClipsPath = RigDir + "/Outrider_Actions.fbx";   // Idle 1, and a long-gun stance kept for the shotgun
    const string ControllerPath = RigDir + "/Outrider.controller";
    const string PistolModelPath = Root + "/Art/Weapons/DesertEagle/DesertEagle.fbx";
    const string ShotgunModelPath = Root + "/Art/Weapons/Shotgun/Shotgun.fbx";

    // How the clips are used. The run clip is in place, with a 0.63 s cycle; its fit to 6 m/s is a guess.
    // There is no dash clip: the dash shows the run clip at speed. There is no shoot clip either: the body
    // keeps its idle stance and CharacterAnimation points the arms and the pistols at the target.
    const float RunClipSpeed = 6f;
    const float DashRecovery = 0.05f;
    const float PistolLength = 0.38f;
    const float ShotgunLength = 1.0f;

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
        Material aimMat = MaterialAsset("SkillAim", new Color(1f, 0.45f, 0.1f), unlit: true);
        Material enemyMat = MaterialAsset("Enemy", new Color(0.4f, 0.12f, 0.45f), unlit: false);
        Material telegraphMat = MaterialAsset("Telegraph", new Color(1f, 0.1f, 0.1f), unlit: true);

        bool settingsCreated = false;
        var settings = AssetDatabase.LoadAssetAtPath<FeelSettings>(SettingsPath);
        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<FeelSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath);
            settingsCreated = true;
        }

        bool skillsCreated = false;
        var skills = AssetDatabase.LoadAssetAtPath<SkillSettings>(SkillsPath);
        if (skills == null)
        {
            skills = ScriptableObject.CreateInstance<SkillSettings>();
            AssetDatabase.CreateAsset(skills, SkillsPath);
            skillsCreated = true;
        }

        bool skillsExtended = skills.EnsureStartSkills();
        if (skillsExtended) EditorUtility.SetDirty(skills);

        bool enemyCreated = false;
        var enemySettings = AssetDatabase.LoadAssetAtPath<EnemySettings>(EnemyPath);
        if (enemySettings == null)
        {
            enemySettings = ScriptableObject.CreateInstance<EnemySettings>();
            AssetDatabase.CreateAsset(enemySettings, EnemyPath);
            enemyCreated = true;
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

        // Player: the rigged Outrider with her weapons if she is there, else a grey-box capsule.
        // Either way the root stays 1 m above the ground, so control, camera and tests do not depend on the visual.
        GameObject riggedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedModelPath);
        GameObject player;
        string playerVisual;
        CharacterAnimation animation = null;
        if (riggedAsset != null)
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            playerVisual = "player visual: " + RiggedModelPath + "\n" + AttachRiggedModel(player, riggedAsset, out animation);
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
        character.SkillSet = skills;
        character.BasicShotMaterial = basicShotMat;
        character.SkillMaterial = skillMat;
        var playerHealth = player.AddComponent<PlayerHealth>();
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

        // Skill aim: the outline of the area the skill in use will hit.
        var aimGo = new GameObject("SkillIndicator");
        var aimLine = aimGo.AddComponent<LineRenderer>();
        aimLine.sharedMaterial = aimMat;
        aimLine.shadowCastingMode = ShadowCastingMode.Off;
        aimLine.receiveShadows = false;
        aimLine.enabled = false;
        aimGo.AddComponent<SkillIndicator>().Player = character;

        // Training dummies: A and B are in basic-attack range of the start position, C is not.
        Dummy("Dummy_A", new Vector3(4f, 1f, 3f), dummyMat);
        Dummy("Dummy_B", new Vector3(-4f, 1f, 3f), dummyMat);
        Dummy("Dummy_C", new Vector3(0f, 1f, 10f), dummyMat);
        // A tight group, for skills that hit an area. It stands clear of the three above.
        Dummy("Dummy_D", new Vector3(-10f, 1f, 6f), dummyMat);
        Dummy("Dummy_E", new Vector3(-11.2f, 1f, 6.6f), dummyMat);
        Dummy("Dummy_F", new Vector3(-10.4f, 1f, 7.4f), dummyMat);

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
        var bar = new GameObject("SkillBar").AddComponent<SkillBar>();
        bar.Player = character;
        bar.Health = playerHealth;

        // The enemy stands far to the right and back, out of its aggro range of the start and of every dummy.
        Enemy(new Vector3(15f, 0f, -10f), enemySettings, character, playerHealth, enemyMat, telegraphMat);

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

        return (settingsCreated ? "settings: created\n" : "settings: kept\n")
             + (skillsCreated ? "skills: created\n" : skillsExtended ? "skills: kept, missing ones added\n" : "skills: kept\n")
             + (enemyCreated ? "enemy: created\n" : "enemy: kept\n") + playerVisual + Dump();
    }

    /// <summary>
    /// A weapon in a holder: its model centred in the holder and the holder scaled so the weapon is
    /// <paramref name="length"/> m long. The models lie with the barrel along +X and the top along +Y,
    /// so those are the holder's axes too. The caller places the holder and gives it a parent.
    /// </summary>
    static Transform WeaponHolder(string name, string modelPath, string materialName, float length, StringBuilder report)
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        if (asset == null)
        {
            report.Append("  ").Append(name).Append(": no model at ").Append(modelPath).Append('\n');
            return null;
        }

        var holder = new GameObject(name);
        var model = (GameObject)PrefabUtility.InstantiatePrefab(asset);
        model.name = "Model";
        model.transform.SetParent(holder.transform, false);

        Renderer[] renderers = model.GetComponentsInChildren<Renderer>();
        string look = ApplyLook(renderers, modelPath, materialName);
        Bounds bounds = renderers[0].bounds;
        foreach (Renderer r in renderers) bounds.Encapsulate(r.bounds);
        model.transform.position -= bounds.center;
        holder.transform.localScale = Vector3.one * (length / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z));

        report.AppendFormat(CultureInfo.InvariantCulture, "  {0}: {1} triangles, {2:0.00} m long, {3}\n",
            name, TriangleCount(renderers), length, look);
        return holder.transform;
    }

    /// <summary>
    /// A pistol in a hand. The barrel lies along the forearm and the top of the gun faces forward while the
    /// arm hangs, which is how a pistol sits in a lowered hand; raised, it points where the arm points.
    /// The grip is put in the palm, a little past the wrist.
    /// </summary>
    static Transform PistolInHand(string name, Transform foreArm, Transform hand, StringBuilder report)
    {
        Transform holder = WeaponHolder(name, PistolModelPath, "DesertEagle", PistolLength, report);
        if (holder == null || foreArm == null || hand == null) return holder;

        Vector3 along = (hand.position - foreArm.position).normalized;
        Vector3 top = Vector3.forward - Vector3.Dot(Vector3.forward, along) * along;
        top = top.sqrMagnitude > 1e-6f ? top.normalized : Vector3.up;
        // The grip is behind and below the middle of the gun: 0.29 and 0.16 of its length.
        Vector3 palm = hand.position + along * 0.08f;
        holder.SetPositionAndRotation(palm + along * (0.29f * PistolLength) + top * (0.16f * PistolLength),
                                      Quaternion.LookRotation(Vector3.Cross(along, top), top));
        holder.SetParent(hand, true);
        return holder;
    }

    /// <summary>
    /// Puts the rigged model under the player as "Visual", with an Animator and the component that drives it.
    /// The rig's origin is between its feet, so it stands on the player's ground point as it is.
    /// </summary>
    static string AttachRiggedModel(GameObject player, GameObject riggedAsset, out CharacterAnimation animation)
    {
        ConfigureClips(RunClipPath, ("running", CharacterAnimation.RunState, true));
        ConfigureClips(ActionClipsPath, ("Idle_02", CharacterAnimation.IdleState, true));
        AnimationClip idle = Clip(ActionClipsPath, CharacterAnimation.IdleState);
        AnimationClip run = Clip(RunClipPath, CharacterAnimation.RunState);

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(riggedAsset);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, -player.transform.position.y, 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, PlayerModelYaw, 0f);

        Renderer[] renderers = visual.GetComponentsInChildren<Renderer>();
        string look = ApplyLook(renderers, RiggedModelPath, "Outrider");
        var report = new StringBuilder();

        // The weapons hang on the bones while the figure still stands in the pose it was rigged in.
        Transform[] bones = visual.GetComponentsInChildren<Transform>();
        Transform Bone(string name) => bones.FirstOrDefault(t => t.name == name);
        Transform rightPistol = PistolInHand("Pistol_R", Bone("RightForeArm"), Bone("RightHand"), report);
        Transform leftPistol = PistolInHand("Pistol_L", Bone("LeftForeArm"), Bone("LeftHand"), report);
        Transform shotgun = WeaponHolder("Shotgun_Back", ShotgunModelPath, "Shotgun", ShotgunLength, report);
        if (shotgun != null)
        {
            // Across the back: barrel up to her left, stock down to her right, flat against her.
            shotgun.SetPositionAndRotation(player.transform.position + new Vector3(0f, 0.25f, -0.17f), Quaternion.Euler(0f, 0f, 120f));
            shotgun.SetParent(Bone("Spine") ?? visual.transform, true);
        }

        // The body's stance while shooting is the idle; the dash shows the run. See the constants at the top.
        Animator animator = visual.GetComponent<Animator>();
        if (animator == null) animator = visual.AddComponent<Animator>();
        animator.runtimeAnimatorController = BuildController(idle, run, idle, run);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        animation = visual.AddComponent<CharacterAnimation>();
        animation.Animator = animator;
        animation.Hips = Bone("Hips");
        animation.ShootFireTime = 0f;
        animation.ShootHoldEnd = 0f;
        animation.DashClipStart = 0f;
        animation.DashClipEnd = run != null ? run.length : 0f;
        animation.DashRecovery = DashRecovery;
        animation.RunClipSpeed = RunClipSpeed;
        animation.RightArm = Bone("RightArm");
        animation.RightForeArm = Bone("RightForeArm");
        animation.RightHand = Bone("RightHand");
        animation.RightWeapon = rightPistol;
        animation.LeftArm = Bone("LeftArm");
        animation.LeftForeArm = Bone("LeftForeArm");
        animation.LeftHand = Bone("LeftHand");
        animation.LeftWeapon = leftPistol;

        string Length(AnimationClip c) => c != null ? c.length.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "MISSING";
        return string.Format(CultureInfo.InvariantCulture, "  {0} triangles, {1} bones, {2}\n  clips: idle {3}, run {4}\n",
                   TriangleCount(renderers.Where(r => r is SkinnedMeshRenderer).ToArray()),
                   renderers.OfType<SkinnedMeshRenderer>().Sum(r => r.bones.Length), look, Length(idle), Length(run))
             + report;
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
    /// Gives the model its look and says which: a material built from the texture files next to it, else its own
    /// materials if they came in with textures, else one flat material (also when it has no UVs).
    /// The built material comes first because the importer's own came out glossy, which breaks a drawn style.
    /// </summary>
    static string ApplyLook(Renderer[] renderers, string modelPath, string materialName)
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
        else
        {
            // Meshy puts the textures next to the FBX but does not always link them in it.
            replacement = TexturedMaterial(modelPath, materialName, out look);
            if (replacement == null && importedWithTextures)
            {
                look = "the model's own materials";
            }
            else if (replacement == null)
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
    static Material TexturedMaterial(string modelPath, string materialName, out string look)
    {
        string dir = System.IO.Path.GetDirectoryName(modelPath).Replace('\\', '/');
        string name = System.IO.Path.GetFileNameWithoutExtension(modelPath);
        // Meshy's own export names the textures after the model ("name.png", "name_normal.png").
        // Textures that Unity extracted from an FBX are named after its material ("BakedMaterial_baseColor.png").
        string colourPath = $"{dir}/{name}.png";
        string prefix = $"{dir}/{name}";
        if (!System.IO.File.Exists(colourPath))
        {
            colourPath = System.IO.Directory.Exists(dir)
                ? System.IO.Directory.GetFiles(dir, "*_baseColor.png").Select(p => p.Replace('\\', '/')).OrderBy(p => p, System.StringComparer.Ordinal).FirstOrDefault()
                : null;
            if (colourPath == null) { look = null; return null; }
            prefix = colourPath.Substring(0, colourPath.Length - "_baseColor.png".Length);
        }
        string normalPath = prefix + "_normal.png";
        string metallicPath = prefix + "_metallic.png";
        string roughnessPath = prefix + "_roughness.png";
        string maskPath = prefix + "_urp_mask.png";
        look = null;
        if (!System.IO.File.Exists(colourPath)) return null;

        bool hasNormal = System.IO.File.Exists(normalPath);
        bool hasMask = WriteMask(metallicPath, roughnessPath, maskPath);

        ConfigureTexture(colourPath, TextureImporterType.Default, srgb: true);
        if (hasNormal) ConfigureTexture(normalPath, TextureImporterType.NormalMap, srgb: false);
        if (hasMask) ConfigureTexture(maskPath, TextureImporterType.Default, srgb: false);

        Material mat = MaterialAsset(materialName, Color.white, unlit: false);
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

    /// <summary>
    /// The enemy: an unscaled root on the ground with the agent and the logic, a scaled body, a nose that
    /// shows where it faces, and a line for its telegraph.
    /// </summary>
    static void Enemy(Vector3 position, EnemySettings settings, PlayerCharacter player, PlayerHealth playerHealth,
                      Material bodyMaterial, Material telegraphMaterial)
    {
        const float radius = 0.8f;
        const float height = 3.2f;

        var root = new GameObject("Enemy_Brute");
        root.transform.position = position;

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
        body.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        body.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        Object.DestroyImmediate(body.GetComponent<Collider>());

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Facing";
        nose.transform.SetParent(root.transform, false);
        nose.transform.localPosition = new Vector3(0f, height * 0.7f, radius);
        nose.transform.localScale = new Vector3(0.5f, 0.5f, 0.7f);
        nose.GetComponent<Renderer>().sharedMaterial = bodyMaterial;
        Object.DestroyImmediate(nose.GetComponent<Collider>());

        var telegraphGo = new GameObject("Telegraph");
        telegraphGo.transform.SetParent(root.transform, false);
        var telegraph = telegraphGo.AddComponent<LineRenderer>();
        telegraph.sharedMaterial = telegraphMaterial;
        telegraph.shadowCastingMode = ShadowCastingMode.Off;
        telegraph.receiveShadows = false;
        telegraph.enabled = false;

        var damageable = root.AddComponent<Damageable>();
        damageable.Radius = radius;
        damageable.MaxHealth = settings.Values.Health;
        root.AddComponent<HitFlash>().Target = body.GetComponent<Renderer>();

        var agent = root.AddComponent<NavMeshAgent>();
        agent.radius = radius;
        agent.height = height;
        agent.baseOffset = 0f;
        agent.avoidancePriority = 60; // it walks around the player, not the other way

        var brute = root.AddComponent<EnemyBrute>();
        brute.Settings = settings;
        brute.Player = player;
        brute.PlayerHealth = playerHealth;
        brute.Telegraph = telegraph;
        brute.StrikeMaterial = telegraphMaterial;
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
