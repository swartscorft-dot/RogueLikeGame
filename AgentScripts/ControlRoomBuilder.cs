// Builds the control room, the one scene of G2, G3 and G4. Run through the Pipeline:
//   unity command script run -- --file AgentScripts/ControlRoomBuilder.cs --entry ControlRoomBuilder.Build
// Rebuilds Assets/Game/Scenes/ControlRoom.unity from nothing on every run, so the result does not
// depend on what the scene looked like before. Returns a hierarchy dump for comparison between runs.
//
// The feel, skill, enemy, grip and shot-effect settings assets are created only when they are missing.
// They hold Kristian's tuning and are never overwritten. An older skill asset gets the skills it lacks.
using System.Collections.Generic;
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
using UnityEngine.Rendering.Universal;

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
    const string GripPath = SettingsDir + "/Grip.asset";
    const string ShotFxPath = SettingsDir + "/ShotFx.asset";
    const string PostProcessingPath = SettingsDir + "/PostProcessing.asset";
    // The bullets' settings of 2026-11-18, replaced the same day by the shot effects. Never tuned; removed when found.
    const string OldBulletsPath = SettingsDir + "/Bullets.asset";
    const string PlayerArtDir = Root + "/Art/Player";

    // The player figure Kristian made in Meshy on 2026-11-17 (the Outrider), rigged through Meshy, and her weapons.
    const string RigDir = PlayerArtDir + "/Outrider";
    const string BakedDir = RigDir + "/Baked";      // clips baked for Kristian's rig by BakeForRig
    const string RiggedModelPath = RigDir + "/Outrider_Rigged.fbx";
    // Kristian's version of the same rigged figure, edited in Blender: a hand closed around a pistol.
    // The closed hand is a shape key at full weight, not baked into the mesh. The pistol he left in the hand
    // shows how the hand holds it.
    const string ClosedHandModelPath = RigDir + "/Rigged_lukket.fbx";
    // The same file with the right hand closed too (made in Blender on 2026-11-18 by mirroring the left hand's shape key).
    const string BothHandsClosedModelPath = RigDir + "/Rigged_lukket_begge.fbx";
    // Kristian's own rig (2026-11-18): the same mesh, his bone placement, finger bones, and both hands closed
    // around the pistols in the rest pose. The Meshy clips are retargeted onto it through Humanoid avatars.
    const string OwnRigModelPath = RigDir + "/Outrider_grip.fbx";
    // Which figure the builder uses: the first of these that is there. Drop a line to stop using that file.
    static readonly string[] PlayerModelPreference =
    {
        OwnRigModelPath,
        BothHandsClosedModelPath,
        ClosedHandModelPath,
        RiggedModelPath,
    };
    const string RunClipPath = RigDir + "/Outrider_Run.fbx";
    const string ActionClipsPath = RigDir + "/Outrider_Actions.fbx";   // Idle 1, and a long-gun stance kept for the shotgun
    const string ControllerPath = RigDir + "/Outrider.controller";
    const string PistolModelPath = Root + "/Art/Weapons/DesertEagle/DesertEagle.fbx";
    const string ShotgunModelPath = Root + "/Art/Weapons/Shotgun/Shotgun.fbx";

    // How the clips are used. The run clip is in place, with a 0.63 s cycle; on his rig a planted foot slides back at
    // 5.7 to 6.1 m/s (measured 2026-11-18), so 6 m/s fits.
    // There is no dash clip: the dash shows the run clip at speed. There is no shoot clip either: the body
    // keeps its idle stance and CharacterAnimation points the arms and the pistols at the target.
    const float RunClipSpeed = 6f;
    // The shooting gaits, bought 2026-11-18 for the Meshy rig: Run_and_Shoot (forward) and
    // Walk_Backward_While_Shooting_inplace (backward). Measured on his rig 2026-11-18: Run_and_Shoot carries its
    // hips 1.5 m in its 0.667 s (2.25 m/s, which the hips hold takes out) while a planted foot creeps 0.1 to 0.3 m/s
    // forward, so the feet pass under the body at about 2.1 m/s; the backward walk is in place, feet 0.76 to 0.93 m/s.
    const string ShootGaitsPath = RigDir + "/Outrider_ShootGaits.fbx";
    const float ShootRunClipSpeed = 2.1f;
    const float ShootBackClipSpeed = 0.85f;
    // The backward gait: his own backward jog, made on his rig (no bake). Two versions, the same jog built for
    // two ground speeds: v2_30 for 3.0 m/s, and v2_24 for 2.4 m/s, calmer, which pairs with a slower backpedal.
    // THE SETTING: false uses v2_30, true uses v2_24; the clip speed goes with it. Without the file, the Meshy
    // Walk_Backward_While_Shooting is baked instead.
    const bool UseCalmBackJog = false;
    static string OwnBackClipPath => RigDir + (UseCalmBackJog ? "/Outrider_grip@Jog_Backward_Shooting_v2_24.fbx" : "/Outrider_grip@Jog_Backward_Shooting_v2_30.fbx");
    static float OwnBackClipSpeed => UseCalmBackJog ? 2.4f : 3.0f;
    static readonly string[] OwnBackClipFiles =
        { RigDir + "/Outrider_grip@Jog_Backward_Shooting_v2_30.fbx", RigDir + "/Outrider_grip@Jog_Backward_Shooting_v2_24.fbx" };
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

        // Its script is gone, so it cannot be loaded; it is found by its file.
        bool oldBulletsRemoved = System.IO.File.Exists(OldBulletsPath) && AssetDatabase.DeleteAsset(OldBulletsPath);

        bool shotFxCreated = false;
        var shotFx = AssetDatabase.LoadAssetAtPath<ShotFxSettings>(ShotFxPath);
        if (shotFx == null)
        {
            shotFx = ScriptableObject.CreateInstance<ShotFxSettings>();
            AssetDatabase.CreateAsset(shotFx, ShotFxPath);
            shotFxCreated = true;
        }
        bool shotFxExtended = shotFx.EnsureLooks(skills.Skills.Where(s => s != null).Select(s => s.Name));
        if (shotFxExtended) EditorUtility.SetDirty(shotFx);
        string soundReport = SetUpShotSounds(shotFx);
        Material shotFxMat = ShotFxMaterial();
        VolumeProfile postProcessing = PostProcessingProfile(out bool postProcessingCreated);

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
        string riggedPath = PlayerModelPreference.FirstOrDefault(System.IO.File.Exists) ?? RiggedModelPath;
        WithoutCamerasAndLights(riggedPath);
        GameObject riggedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(riggedPath);
        GameObject player;
        string playerVisual;
        CharacterAnimation animation = null;
        if (riggedAsset != null)
        {
            player = new GameObject("Player");
            player.transform.position = new Vector3(0f, 1f, 0f);
            playerVisual = "player visual: " + riggedPath + "\n" + AttachRiggedModel(player, riggedAsset, riggedPath, out animation);
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
        character.ShotLooks = shotFx;
        // The shot sounds: a pool of 2D voices on an object of its own.
        character.Sounds = new GameObject("ShotSounds").AddComponent<ShotSounds>();
        character.ShotFxMaterial = shotFxMat;
        if (animation != null)
        {
            // Shots start at the barrel ends. Without the figure there are no muzzles, and they start at the character.
            character.RightMuzzle = Muzzle(animation.RightWeapon);
            character.LeftMuzzle = Muzzle(animation.LeftWeapon);
        }
        playerVisual += "  muzzles: " + (character.RightMuzzle != null ? "right" : "no right") + ", "
                      + (character.LeftMuzzle != null ? "left" : "no left") + "\n";
        var playerHealth = player.AddComponent<PlayerHealth>();
        player.AddComponent<PlayerInputDriver>();
        if (animation != null) animation.Player = character;

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
        camGo.AddComponent<AudioListener>(); // the one listener in the scene
        rig.Target = player.transform;
        rig.Settings = settings;
        Quaternion camRotation = Quaternion.Euler(settings.Values.CameraPitch, 0f, 0f);
        camGo.transform.SetPositionAndRotation(-(camRotation * Vector3.forward) * settings.Values.CameraDistance, camRotation);
        cam.fieldOfView = settings.Values.FieldOfView;
        cam.allowHDR = true;

        // Bloom, so that the shot effects glow. Only what is drawn brighter than white passes its threshold,
        // which in this scene is the effects (see ShotFxSettings.CoreGlow); the rest of the picture is as before.
        // To turn it off: untick Bloom in Settings/PostProcessing.asset, or the Volume on "PostProcessing".
        var cameraData = camGo.AddComponent<UniversalAdditionalCameraData>();
        cameraData.renderPostProcessing = true;
        cameraData.antialiasing = AntialiasingMode.None;
        var volume = new GameObject("PostProcessing").AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 1f;
        volume.sharedProfile = postProcessing;

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

        // The baked NavMesh lies a few centimetres over the floor (voxel rounding), and the agents stand on it,
        // so a figure placed for a floor at y = 0 would float that much. Lower the visible parts by it: the
        // player's model and the enemy's body. The roots, and so every gameplay position, stay where the agents put them.
        float lift = NavMeshLift(floor.transform.position);
        if (player.transform.Find("Visual") is Transform model) model.localPosition += Vector3.down * lift;
        GameObject brute = GameObject.Find("Enemy_Brute");
        if (brute != null)
            foreach (string part in new[] { "Body", "Facing" })
                if (brute.transform.Find(part) is Transform t) t.localPosition += Vector3.down * lift;
        string floorReport = string.Format(CultureInfo.InvariantCulture,
            "floor: the NavMesh lies {0:F3} m over it; the player's model and the enemy's body are lowered by that\n", lift);

        var scene = EditorSceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene, ScenePath);
        AssetDatabase.SaveAssets();

        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        return (settingsCreated ? "settings: created\n" : "settings: kept\n")
             + (skillsCreated ? "skills: created\n" : skillsExtended ? "skills: kept, missing ones added\n" : "skills: kept\n")
             + (enemyCreated ? "enemy: created\n" : "enemy: kept\n")
             + (shotFxCreated ? "shot effects: created\n" : shotFxExtended ? "shot effects: kept, missing looks added\n" : "shot effects: kept\n") + soundReport
             + (oldBulletsRemoved ? "old bullet settings: removed\n" : "")
             + (postProcessingCreated ? "post-processing profile: created\n" : "post-processing profile: kept\n") + floorReport + playerVisual + Dump();
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
    /// A "Muzzle" child of a weapon holder at the barrel end: at the farthest point of the mesh along the
    /// holder's +X (the barrel), across from the middle of the front face (the vertices within 2% of the
    /// weapon's length of that point).
    /// </summary>
    static Transform Muzzle(Transform holder)
    {
        if (holder == null) return null;
        float front = float.NegativeInfinity, back = float.PositiveInfinity;
        var points = new System.Collections.Generic.List<Vector3>();
        foreach (MeshFilter filter in holder.GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            foreach (Vector3 v in filter.sharedMesh.vertices)
            {
                Vector3 local = holder.InverseTransformPoint(filter.transform.TransformPoint(v));
                points.Add(local);
                front = Mathf.Max(front, local.x);
                back = Mathf.Min(back, local.x);
            }
        }
        if (points.Count == 0) return null;

        float slack = (front - back) * 0.02f;
        Vector3 sum = Vector3.zero;
        int n = 0;
        foreach (Vector3 p in points)
        {
            if (p.x < front - slack) continue;
            sum += p;
            n++;
        }
        Vector3 face = sum / n;

        var muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(holder, false);
        muzzle.localPosition = new Vector3(front, face.y, face.z);
        return muzzle;
    }

    /// <summary>Turns off the import of cameras and lights for a model file. A Blender export often carries both.</summary>
    static void WithoutCamerasAndLights(string modelPath)
    {
        var importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
        if (importer == null || (!importer.importCameras && !importer.importLights)) return;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();
    }

    /// <summary>
    /// Two pistols placed as the one in the model file lies: one where it is, in the hand it is nearest to, and
    /// one mirrored into the other hand (the figure is the same left and right). They are made as long as it is.
    /// The pistol mesh has its barrel along its own +X and its top along its own +Z.
    /// </summary>
    static void PistolsAsPlaced(Transform placed, Transform visual, Transform rightHand, Transform leftHand,
                                StringBuilder report, out Transform rightPistol, out Transform leftPistol)
    {
        Mesh mesh = placed.GetComponent<MeshFilter>().sharedMesh;
        Vector3 centre = visual.InverseTransformPoint(placed.TransformPoint(mesh.bounds.center));
        Vector3 barrel = visual.InverseTransformDirection(placed.TransformDirection(Vector3.right)).normalized;
        Vector3 top = visual.InverseTransformDirection(placed.TransformDirection(Vector3.forward)).normalized;
        Vector3 size = Vector3.Scale(mesh.bounds.size, placed.lossyScale);
        float length = Mathf.Max(size.x, size.y, size.z);
        bool inLeftHand = Vector3.Distance(placed.TransformPoint(mesh.bounds.center), leftHand.position)
                       <= Vector3.Distance(placed.TransformPoint(mesh.bounds.center), rightHand.position);

        Transform Place(string name, Transform hand, bool mirrored)
        {
            Transform holder = WeaponHolder(name, PistolModelPath, "DesertEagle", length, report);
            if (holder == null) return null;
            Vector3 flip = mirrored ? new Vector3(-1f, 1f, 1f) : Vector3.one;
            Vector3 b = visual.TransformDirection(Vector3.Scale(barrel, flip));
            Vector3 t = visual.TransformDirection(Vector3.Scale(top, flip));
            holder.SetPositionAndRotation(visual.TransformPoint(Vector3.Scale(centre, flip)),
                                          Quaternion.LookRotation(Vector3.Cross(b, t), t));
            holder.SetParent(hand, true);
            return holder;
        }

        leftPistol = Place("Pistol_L", leftHand, mirrored: !inLeftHand);
        rightPistol = Place("Pistol_R", rightHand, mirrored: inLeftHand);
        report.AppendFormat(CultureInfo.InvariantCulture, "  grip: taken from the pistol placed in the {0} hand in the model file, mirrored for the other\n",
                            inLeftHand ? "left" : "right");
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
    static string AttachRiggedModel(GameObject player, GameObject riggedAsset, string riggedPath, out CharacterAnimation animation)
    {
        // The clips were made for the Meshy rig and write its bones' local transforms. The Meshy-rigged files play
        // them as they are. Kristian's rig places and turns those bones differently, so for his rig each clip is
        // baked into a clip of its own (BakeForRig): every body bone turns from his rest exactly as the Meshy bone
        // turns from its rest. Everything is Generic; there are no avatars.
        bool ownRig = riggedPath == OwnRigModelPath;
        WithoutCamerasAndLights(ShootGaitsPath);
        string rigReport = "  generic: " + MakeGeneric(RunClipPath) + ", " + MakeGeneric(ActionClipsPath) + ", " + MakeGeneric(ShootGaitsPath)
                         + (ownRig ? ", " + MakeGeneric(riggedPath) : "") + "\n";
        riggedAsset = AssetDatabase.LoadAssetAtPath<GameObject>(riggedPath);
        ConfigureClips(RunClipPath, ("running", CharacterAnimation.RunState, true));
        ConfigureClips(ActionClipsPath, ("Idle_02", CharacterAnimation.IdleState, true));
        ConfigureClips(ShootGaitsPath, ("Run_and_Shoot", CharacterAnimation.ShootMoveState, true),
                       ("Walk_Backward_While_Shooting", CharacterAnimation.ShootBackState, true));
        AnimationClip idle = Clip(ActionClipsPath, CharacterAnimation.IdleState);
        AnimationClip run = Clip(RunClipPath, CharacterAnimation.RunState);
        AnimationClip gait = Clip(ShootGaitsPath, CharacterAnimation.ShootMoveState);
        AnimationClip back = Clip(ShootGaitsPath, CharacterAnimation.ShootBackState);
        bool ownBack = false;
        if (ownRig)
        {
            EnsureFolder(RigDir, "Baked");
            idle = BakeForRig(RiggedModelPath, idle, riggedPath, BakedDir + "/Outrider_grip_Idle.anim");
            run = BakeForRig(RiggedModelPath, run, riggedPath, BakedDir + "/Outrider_grip_Run.anim");
            gait = BakeForRig(RiggedModelPath, gait, riggedPath, BakedDir + "/Outrider_grip_ShootRun.anim");
            if (!System.IO.File.Exists(OwnBackClipPath))
                back = BakeForRig(RiggedModelPath, back, riggedPath, BakedDir + "/Outrider_grip_ShootBack.anim");
            foreach (string file in OwnBackClipFiles)
            {
                // Both jogs are set up the same way, so switching is the one setting above.
                if (!System.IO.File.Exists(file)) continue;
                MakeGeneric(file);
                WithoutCamerasAndLights(file);
                ConfigureClips(file, ("", CharacterAnimation.ShootBackState, true));
            }
            if (System.IO.File.Exists(OwnBackClipPath))
            {
                // His own backward jog: already for this rig, used as it is.
                ConfigureClips(OwnBackClipPath, ("", CharacterAnimation.ShootBackState, true));
                AnimationClip own = Clip(OwnBackClipPath, CharacterAnimation.ShootBackState);
                if (own != null) { back = own; ownBack = true; }
            }
            rigReport += string.Format(CultureInfo.InvariantCulture, "  clips baked for this rig from {0}: idle {1} keys, run {2} keys, shooting gaits {3} and {4} keys\n",
                                       System.IO.Path.GetFileName(RiggedModelPath), KeyCount(idle), KeyCount(run), KeyCount(gait), KeyCount(back));
        }

        var visual = (GameObject)PrefabUtility.InstantiatePrefab(riggedAsset);
        visual.name = "Visual";
        visual.transform.SetParent(player.transform, false);
        visual.transform.localPosition = new Vector3(0f, -player.transform.position.y, 0f);
        visual.transform.localRotation = Quaternion.Euler(0f, PlayerModelYaw, 0f);

        var report = new StringBuilder();
        Transform[] bones = visual.GetComponentsInChildren<Transform>();
        Transform Bone(string name) => bones.FirstOrDefault(t => t.name == name);

        // A loose, unskinned mesh in the model file is a pistol that was left in a hand in Blender.
        // Where it lies is how the hand was shaped to hold it, so that is the grip, mirrored for the other hand.
        Transform placed = bones.FirstOrDefault(t => t != visual.transform && t.GetComponent<MeshFilter>() != null);
        Transform rightPistol, leftPistol;
        if (placed != null && Bone("RightHand") != null && Bone("LeftHand") != null)
        {
            PistolsAsPlaced(placed, visual.transform, Bone("RightHand"), Bone("LeftHand"), report, out rightPistol, out leftPistol);
            placed.gameObject.SetActive(false);
        }
        else
        {
            rightPistol = PistolInHand("Pistol_R", Bone("RightForeArm"), Bone("RightHand"), report);
            leftPistol = PistolInHand("Pistol_L", Bone("LeftForeArm"), Bone("LeftHand"), report);
        }

        Renderer[] renderers = visual.GetComponentsInChildren<SkinnedMeshRenderer>();
        string look = ApplyLook(renderers, riggedPath, "Outrider");

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
        animator.avatar = null;
        animator.runtimeAnimatorController = BuildController(idle, run, idle, run, gait, back);
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        animation = visual.AddComponent<CharacterAnimation>();
        animation.Animator = animator;
        animation.Hips = Bone("Hips");
        animation.Spine = new[] { Bone("Spine02"), Bone("Spine01"), Bone("Spine") }.Where(b => b != null).ToArray();
        animation.Neck = Bone("neck");
        animation.Head = Bone("Head");
        animation.ShootFireTime = 0f;
        animation.ShootHoldEnd = 0f;
        animation.DashClipStart = 0f;
        animation.DashClipEnd = run != null ? run.length : 0f;
        animation.DashRecovery = DashRecovery;
        animation.RunClipSpeed = RunClipSpeed;
        animation.ShootRunClipSpeed = ShootRunClipSpeed;
        animation.ShootBackClipSpeed = ownBack ? OwnBackClipSpeed : ShootBackClipSpeed;
        animation.RightArm = Bone("RightArm");
        animation.RightForeArm = Bone("RightForeArm");
        animation.RightHand = Bone("RightHand");
        animation.RightWeapon = rightPistol;
        animation.LeftArm = Bone("LeftArm");
        animation.LeftForeArm = Bone("LeftForeArm");
        animation.LeftHand = Bone("LeftHand");
        animation.LeftWeapon = leftPistol;

        // With no pistol placed in the model file, where the pistols sit is tuned in an asset, like the feel
        // values. It is never overwritten. With one placed, the model decides and the asset is not used.
        if (placed == null)
        {
            var grip = AssetDatabase.LoadAssetAtPath<GripSettings>(GripPath);
            if (grip == null)
            {
                grip = ScriptableObject.CreateInstance<GripSettings>();
                AssetDatabase.CreateAsset(grip, GripPath);
            }
            animation.Grip = grip;
        }

        string Length(AnimationClip c) => c != null ? c.length.ToString("0.00", CultureInfo.InvariantCulture) + " s" : "MISSING";
        return string.Format(CultureInfo.InvariantCulture, "  {0} triangles, {1} bones, {2}\n  clips: idle {3}, run {4}\n",
                   TriangleCount(renderers.Where(r => r is SkinnedMeshRenderer).ToArray()),
                   renderers.OfType<SkinnedMeshRenderer>().Sum(r => r.bones.Length), look, Length(idle), Length(run))
             + rigReport + report;
    }


    /// <summary>
    /// The body bones of the Outrider, by their Meshy names, which Kristian's rig keeps. These are the bones a
    /// baked clip moves. The fingers, "headfront" and the end bones are left out on purpose: they keep their
    /// rest pose, which in his rig is the hand closed around the pistol.
    /// </summary>
    static readonly string[] BodyBones =
    {
        "Hips", "Spine02", "Spine01", "Spine", "neck", "Head",
        "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
        "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
    };

    /// <summary>
    /// Carries a clip made for one rig over to another rig of the same mesh, and saves it as a clip asset of its own
    /// (kept at its path so its identity stays across runs). Both models are posed in a preview scene, so the open
    /// scene is not touched. At every frame of the clip: each body bone of the target turns, in the figure's own
    /// space, from its rest exactly as the same bone of the source turns from its rest; and the hips move as the
    /// source's hips move from their rest. The other bones of the target get no curves and keep their rest.
    /// Because the mesh is the same mesh, the skin then moves as it did on the source rig, up to the small
    /// differences in where the two rigs put their joints.
    /// </summary>
    static AnimationClip BakeForRig(string sourceModelPath, AnimationClip source, string targetModelPath, string outPath)
    {
        if (source == null) return null;
        UnityEngine.SceneManagement.Scene scene = EditorSceneManager.NewPreviewScene();
        var clip = new AnimationClip { name = System.IO.Path.GetFileNameWithoutExtension(outPath), frameRate = source.frameRate };
        try
        {
            GameObject Spawn(string path)
            {
                var go = (GameObject)Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
                UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                return go;
            }
            GameObject from = Spawn(sourceModelPath), to = Spawn(targetModelPath);
            Transform[] fromAll = from.GetComponentsInChildren<Transform>(true), toAll = to.GetComponentsInChildren<Transform>(true);
            // Parents first, so a bone's parent is already posed when the bone is.
            var pairs = BodyBones
                .Select(n => (src: fromAll.FirstOrDefault(t => t.name == n), dst: toAll.FirstOrDefault(t => t.name == n)))
                .Where(p => p.src != null && p.dst != null)
                .OrderBy(p => Depth(p.dst))
                .ToArray();
            Quaternion[] srcRest = pairs.Select(p => Quaternion.Inverse(from.transform.rotation) * p.src.rotation).ToArray();
            Quaternion[] dstRest = pairs.Select(p => Quaternion.Inverse(to.transform.rotation) * p.dst.rotation).ToArray();
            Vector3[] srcRestPos = pairs.Select(p => from.transform.InverseTransformPoint(p.src.position)).ToArray();
            Vector3[] dstRestPos = pairs.Select(p => to.transform.InverseTransformPoint(p.dst.position)).ToArray();

            int frames = Mathf.Max(2, Mathf.RoundToInt(source.length * source.frameRate) + 1);
            var rot = pairs.Select(_ => new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve(), new AnimationCurve() }).ToArray();
            var pos = new[] { new AnimationCurve(), new AnimationCurve(), new AnimationCurve() };
            var last = new Quaternion[pairs.Length];
            for (int f = 0; f < frames; f++)
            {
                float t = Mathf.Min(source.length, f / source.frameRate);
                source.SampleAnimation(from, t);
                for (int i = 0; i < pairs.Length; i++)
                {
                    Quaternion now = Quaternion.Inverse(from.transform.rotation) * pairs[i].src.rotation;
                    pairs[i].dst.rotation = to.transform.rotation * (now * Quaternion.Inverse(srcRest[i]) * dstRest[i]);
                    if (pairs[i].src.name == "Hips")
                    {
                        Vector3 moved = from.transform.InverseTransformPoint(pairs[i].src.position) - srcRestPos[i];
                        pairs[i].dst.position = to.transform.TransformPoint(dstRestPos[i] + moved);
                        Vector3 lp = pairs[i].dst.localPosition;
                        pos[0].AddKey(t, lp.x); pos[1].AddKey(t, lp.y); pos[2].AddKey(t, lp.z);
                    }
                    Quaternion q = pairs[i].dst.localRotation;
                    if (f > 0 && Quaternion.Dot(q, last[i]) < 0f) q = new Quaternion(-q.x, -q.y, -q.z, -q.w);
                    last[i] = q;
                    rot[i][0].AddKey(t, q.x); rot[i][1].AddKey(t, q.y); rot[i][2].AddKey(t, q.z); rot[i][3].AddKey(t, q.w);
                }
            }
            for (int i = 0; i < pairs.Length; i++)
            {
                string path = AnimationUtility.CalculateTransformPath(pairs[i].dst, to.transform);
                string[] props = { "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w" };
                for (int c = 0; c < 4; c++) clip.SetCurve(path, typeof(Transform), props[c], rot[i][c]);
                if (pairs[i].dst.name == "Hips")
                {
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.x", pos[0]);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.y", pos[1]);
                    clip.SetCurve(path, typeof(Transform), "m_LocalPosition.z", pos[2]);
                }
            }
            clip.EnsureQuaternionContinuity();
            AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(source);
            AnimationUtility.SetAnimationClipSettings(clip, new AnimationClipSettings
            {
                loopTime = settings.loopTime, loopBlend = settings.loopBlend, startTime = 0f, stopTime = source.length,
            });
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }

        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(outPath);
        if (existing != null)
        {
            EditorUtility.CopySerialized(clip, existing);
            existing.name = clip.name;
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(clip);
            return existing;
        }
        AssetDatabase.CreateAsset(clip, outPath);
        return clip;
    }

    static int Depth(Transform t)
    {
        int d = 0;
        for (; t.parent != null; t = t.parent) d++;
        return d;
    }

    static int KeyCount(AnimationClip clip) =>
        clip == null ? 0 : AnimationUtility.GetCurveBindings(clip).Sum(b => AnimationUtility.GetEditorCurve(clip, b).length);

    /// <summary>Imports a model file as Generic without an avatar, as the Meshy files were. Reimports only when needed.</summary>
    static string MakeGeneric(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as ModelImporter;
        if (importer == null) return System.IO.Path.GetFileName(path) + " missing";
        if (importer.animationType != ModelImporterAnimationType.Generic || importer.avatarSetup != ModelImporterAvatarSetup.NoAvatar)
        {
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.NoAvatar;
            importer.SaveAndReimport();
        }
        return System.IO.Path.GetFileName(path) + " generic";
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
    static AnimatorController BuildController(AnimationClip idle, AnimationClip run, AnimationClip shoot, AnimationClip dash, AnimationClip shootRun, AnimationClip shootBack)
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
        machine.AddState(CharacterAnimation.ShootMoveState).motion = shootRun;
        machine.AddState(CharacterAnimation.ShootBackState).motion = shootBack;
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

    /// <summary>How far over the floor's top the baked NavMesh lies, measured at a point of the floor.</summary>
    static float NavMeshLift(Vector3 floorPoint)
    {
        return NavMesh.SamplePosition(floorPoint, out NavMeshHit hit, 1f, NavMesh.AllAreas) ? hit.position.y - floorPoint.y : 0f;
    }

    // ---------------- Shot sounds ----------------

    const string ShotSoundDir = Root + "/Audio/Shots";

    /// <summary>The start mapping of sounds to looks (2026-11-18): clip, share of the loudest peak to aim at, pitch, pitch spread.</summary>
    static readonly (string look, string clip, float level, float pitch, float spread)[] StartSounds =
    {
        ("Basic shot",    "gunshot_54",    0.55f, 1f,    0.05f), // Kristian's choice; a little below the skills
        ("Quick Shot",    "gunshot_42",    0.65f, 1f,    0f),
        ("Backstep",      "gunshot_42",    0.65f, 1f,    0f),
        ("Triple Tap",    "gunshot_42",    0.65f, 1f,    0.03f),
        ("Fan Fire",      "gunshot_6",     0.4f,  1f,    0.05f), // quieter: it ticks
        ("Long Shot",     "deagle_50ae",   0.65f, 1f,    0f),
        ("Heavy Volley",  "deagle_50ae",   0.65f, 1f,    0.03f),
        ("Scatter Blast", "shotgun_blast", 0.65f, 1f,    0f),
        ("Powder Keg",    "shotgun_blast", 0.65f, 0.85f, 0f),  // a stand-in for an explosion, pitched down
    };

    /// <summary>
    /// Sets up the shot sound files (decompressed on load, preloaded, not streamed), measures each one, and gives
    /// every look that has no sound yet its start sound. A look that already has one is left as it is.
    /// </summary>
    static string SetUpShotSounds(ShotFxSettings shotFx)
    {
        var sb = new StringBuilder("shot sounds:\n");
        var facts = new Dictionary<string, (AudioClip clip, float lead, float peak)>();
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ShotSoundDir }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;
            AudioImporterSampleSettings settings = importer.defaultSampleSettings;
            if (settings.loadType != AudioClipLoadType.DecompressOnLoad || settings.compressionFormat != AudioCompressionFormat.PCM
                || !settings.preloadAudioData || importer.loadInBackground)
            {
                settings.loadType = AudioClipLoadType.DecompressOnLoad;
                settings.compressionFormat = AudioCompressionFormat.PCM;
                settings.preloadAudioData = true;
                importer.defaultSampleSettings = settings;
                importer.loadInBackground = false;
                importer.SaveAndReimport();
            }
            var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            clip.LoadAudioData();
            var data = new float[clip.samples * clip.channels];
            clip.GetData(data, 0);
            float peak = 0f;
            int first = -1;
            for (int i = 0; i < data.Length; i++)
            {
                float a = Mathf.Abs(data[i]);
                if (a > peak) peak = a;
                if (first < 0 && a > 0.01f) first = i;
            }
            float lead = first < 0 ? 0f : (first / clip.channels) / (float)clip.frequency;
            facts[clip.name] = (clip, lead, peak);
            sb.AppendFormat(CultureInfo.InvariantCulture, "  {0}: {1:F2} s, {2}, {3} Hz, leading silence {4:F0} ms, peak {5:F2} ({6:F1} dBFS)\n",
                clip.name, clip.length, clip.channels == 1 ? "mono" : clip.channels + " channels", clip.frequency, lead * 1000f, peak,
                peak > 0f ? 20f * Mathf.Log10(peak) : -999f);
        }
        if (facts.Count == 0) return "shot sounds: none found in " + ShotSoundDir + "\n";

        bool changed = false;
        foreach ((string look, string clipName, float level, float pitch, float spread) in StartSounds)
        {
            ShotLook target = look == "Basic shot" ? shotFx.Basic : shotFx.For(look);
            if (target == null || target.Sound != null || !facts.TryGetValue(clipName, out var f)) continue;
            target.Sound = f.clip;
            target.SoundVolume = f.peak > 0f ? Mathf.Clamp01(level / f.peak) : level;
            target.SoundPitch = pitch;
            target.SoundPitchSpread = spread;
            target.SoundStart = Mathf.Max(0f, f.lead - 0.002f);
            changed = true;
            sb.AppendFormat(CultureInfo.InvariantCulture, "  {0} <- {1}, volume {2:F2}, pitch {3:F2} +/- {4:P0}, starts at {5:F3} s\n",
                look, clipName, target.SoundVolume, pitch, spread, target.SoundStart);
        }
        if (changed)
        {
            EditorUtility.SetDirty(shotFx);
            AssetDatabase.SaveAssetIfDirty(shotFx);
        }
        else sb.Append("  every look already has its sound: kept\n");
        return sb.ToString();
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

    /// <summary>
    /// The material of the shot effects: URP's unlit particle shader, opaque, drawn from both sides, white, so
    /// each effect shows the colours of its vertices as they are. Flat colour with hard edges, no glow.
    /// </summary>
    static Material ShotFxMaterial()
    {
        string path = $"{MaterialDir}/ShotFx.mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (mat == null)
        {
            mat = new Material(shader);
            AssetDatabase.CreateAsset(mat, path);
        }
        if (shader != null && mat.shader != shader) mat.shader = shader;
        mat.SetFloat("_Surface", 0f);
        mat.SetFloat("_Cull", 0f);
        mat.SetColor("_BaseColor", Color.white);
        mat.renderQueue = (int)RenderQueue.Geometry;
        EditorUtility.SetDirty(mat);
        return mat;
    }

    /// <summary>
    /// The scene's post-processing: a profile with Bloom, created when missing and never overwritten, so
    /// Kristian's tuning of it is kept. Threshold 1.2: only colours brighter than white pass, and the lit
    /// floor, dummies and figure stay below it. Intensity and scatter are start values, guesses.
    ///
    /// The render pipeline asset (PC_RPAsset) carries a volume profile of its own from the URP template,
    /// SampleSceneProfile, with Neutral tonemapping, a vignette and a weak bloom. It was never seen while the
    /// camera had post-processing off; now it would darken the bright reds and the corners. This profile
    /// overrides those back to nothing, so the picture is as before apart from the glow. The pipeline asset
    /// itself is left as it is.
    /// </summary>
    static VolumeProfile PostProcessingProfile(out bool created)
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PostProcessingPath);
        created = profile == null;
        if (created)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, PostProcessingPath);
        }
        if (!profile.Has<Bloom>())
        {
            Bloom bloom = AddTo<Bloom>(profile);
            bloom.threshold.Override(1.2f);
            bloom.intensity.Override(2f);
            bloom.scatter.Override(0.65f);
            bloom.highQualityFiltering.Override(true);
            bloom.tint.Override(Color.white);
            bloom.dirtIntensity.Override(0f);
        }
        if (!profile.Has<Tonemapping>()) AddTo<Tonemapping>(profile).mode.Override(TonemappingMode.None);
        if (!profile.Has<Vignette>()) AddTo<Vignette>(profile).intensity.Override(0f);
        return profile;
    }

    static T AddTo<T>(VolumeProfile profile) where T : VolumeComponent
    {
        T component = profile.Add<T>(true);
        component.name = typeof(T).Name;
        AssetDatabase.AddObjectToAsset(component, profile);
        EditorUtility.SetDirty(profile);
        return component;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder($"{parent}/{child}"))
            AssetDatabase.CreateFolder(parent, child);
    }
}
