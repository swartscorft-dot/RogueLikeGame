using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The four enemy models as Unity imports them (work order 011): each loads by name from Resources, is as high as
    /// the table says within 5 %, stands on the floor, faces a quarter turn the code can put right, has one skinned
    /// mesh on its 24 bones and a texture for each mesh, and has every clip of the table at its length within a frame.
    /// Writes what Unity made of each ("ENEMYIMPORT"): the import's animation type, the clips' names and lengths, the
    /// meshes and where they hang, the facing, and the stride speeds of the walk and run measured from the feet and
    /// the direction the left arm points at the archer's release.
    /// </summary>
    public class EnemyModelImportTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly string[] s_Bones =
        {
            "Hips", "Spine02", "Spine01", "Spine", "neck", "Head", "head_end", "headfront",
            "LeftShoulder", "LeftArm", "LeftForeArm", "LeftHand", "RightShoulder", "RightArm", "RightForeArm", "RightHand",
            "LeftUpLeg", "LeftLeg", "LeftFoot", "LeftToeBase", "RightUpLeg", "RightLeg", "RightFoot", "RightToeBase",
        };

        readonly List<Object> _made = new List<Object>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            foreach (Object o in _made)
                if (o != null) Object.Destroy(o);
            _made.Clear();
            PlayerInputDriver.ReadRealDevices = true;
        }

        static string F(double v) => v.ToString("0.###", s_Inv);

        [Test]
        public void EveryModel_Loads_IsItsHeight_StandsOnTheFloor_HasItsBonesTexturesAndClips()
        {
            var faults = new List<string>();
            foreach (EnemyModelSpec spec in EnemyModelSpec.All)
            {
                var report = new StringBuilder();
                GameObject source = EnemyLook.Source(spec);
                if (source == null)
                {
                    faults.Add(spec.Name + " does not load from Resources/" + spec.ResourcePath);
                    continue;
                }
                report.Append("ENEMYIMPORT ").Append(spec.Name).Append(" (").Append(spec.Kind).Append("): ");
#if UNITY_EDITOR
                var importer = UnityEditor.AssetImporter.GetAtPath("Assets/Game/Resources/" + spec.ResourcePath + ".fbx") as UnityEditor.ModelImporter;
                if (importer != null)
                    report.Append("animation type ").Append(importer.animationType).Append(", scale factor ").Append(F(importer.globalScale))
                          .Append(", materials ").Append(importer.materialImportMode).Append("; ");
#endif
                report.Append("root scale ").Append(source.transform.localScale.ToString("0.###"))
                      .Append(", root rotation ").Append(source.transform.localEulerAngles.ToString("0.#")).Append("; ");

                // Under a root of its own at the origin, as imported.
                var root = new GameObject("Import " + spec.Name);
                _made.Add(root);
                GameObject model = Object.Instantiate(source, root.transform, false);
                SkinnedMeshRenderer[] skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
                SkinnedMeshRenderer skin = skins.OrderByDescending(s => s.sharedMesh != null ? s.sharedMesh.vertexCount : 0).FirstOrDefault();
                if (skin == null)
                {
                    faults.Add(spec.Name + " has no skinned mesh");
                    continue;
                }

                // Height, floor and facing.
                // The height as made is the mesh as bound to its bones; the file's own pose stands lower.
                EnemyLook.SkinBounds(skin, root.transform, out Vector3 lo, out Vector3 hi);
                float posed = hi.y - lo.y;
                float height = EnemyLook.BindHeight(skin, root.transform);
                float facing = EnemyLook.FacingOf(model, root.transform);
                report.Append("height as bound ").Append(F(height)).Append(" m (table ").Append(F(spec.Height)).Append("), in the file's pose ")
                      .Append(F(posed)).Append(" m, bottom ").Append(F(lo.y)).Append(", head faces ").Append(F(facing))
                      .Append(" deg from +Z, hips and shoulders ").Append(F(EnemyLook.FacingOf(model, root.transform, false))).Append(" deg; ");
                if (Math.Abs(height - spec.Height) > spec.Height * 0.05) faults.Add(spec.Name + " is " + F(height) + " m high, not " + F(spec.Height));
                if (Mathf.Abs(lo.y) > (float)spec.FloorShare * (float)spec.Height) faults.Add(spec.Name + " does not stand on the floor: bottom " + F(lo.y));
                float quarter = Mathf.Abs(Mathf.DeltaAngle(facing, Mathf.Round(facing / 90f) * 90f));
                if (quarter > 40f) faults.Add(spec.Name + " faces " + F(facing) + " deg, not near a quarter turn");

                // Skin and bones.
                report.Append("skinned meshes ").Append(skins.Length).Append(", body '").Append(skin.name).Append("' on ").Append(skin.bones.Length).Append(" bones; ");
                List<string> missing = s_Bones.Where(b => EnemyLook.Find(model.transform, b) == null).ToList();
                if (missing.Count > 0) faults.Add(spec.Name + " lacks the bones " + string.Join(", ", missing));
                if (skin.bones.Length < 20) faults.Add(spec.Name + "'s skin has only " + skin.bones.Length + " bones");

                // Meshes and their textures.
                foreach (Renderer r in renderers)
                {
                    Material m = r.sharedMaterial;
                    Texture imported = m == null ? null : m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null ? m.GetTexture("_BaseMap") : m.mainTexture;
                    Texture2D beside = Resources.Load<Texture2D>(spec.TexturePath(r.name));
                    report.Append("mesh '").Append(r.name).Append("' ").Append(r.GetType().Name).Append(" under '")
                          .Append(r.transform.parent != null ? r.transform.parent.name : "-").Append("' local rotation ")
                          .Append(r.transform.localEulerAngles.ToString("0.#")).Append(", material '").Append(m != null ? m.name : "none")
                          .Append("' with texture ").Append(imported != null ? imported.name : "none").Append(", texture by name ")
                          .Append(beside != null ? beside.name : "none").Append("; ");
                    if (imported == null && beside == null) faults.Add(spec.Name + ": no texture for " + r.name);
                }

                // Clips.
                AnimationClip[] clips = EnemyLook.Clips(spec);
                report.Append("takes: ").Append(string.Join(", ", clips.Select(c =>
                    c.name + " " + F(c.length) + " s" + (c.isLooping ? " loop" : "") + (c.legacy ? " legacy" : "") + " " + F(c.frameRate) + " fps"))).Append("; ");
                foreach (EnemyClipSpec c in spec.Clips)
                {
                    AnimationClip clip = EnemyLook.FindClip(spec, c.Name);
                    if (clip == null) faults.Add(spec.Name + " has no clip " + c.Name);
                    else if (Math.Abs(clip.length - c.Length) > 1.0 / EnemyModelSpec.FramesPerSecond + 1e-4)
                        faults.Add(spec.Name + " " + c.Name + " is " + F(clip.length) + " s, not " + F(c.Length));
                    else if (clip.legacy) faults.Add(spec.Name + " " + c.Name + " is a legacy clip, which a Playables graph cannot play");
                }

                // Measured from the clips: the stride speeds, and where the archer's arm points at its release.
                report.Append("stride speeds as imported: ")
                      .Append(spec.Walk).Append(" ").Append(F(StrideSpeed(model, spec, spec.Walk))).Append(" m/s (table ").Append(F(spec.WalkStride)).Append("), ")
                      .Append("Run ").Append(F(StrideSpeed(model, spec, "Run"))).Append(" m/s (table ").Append(F(spec.RunStride)).Append(")");
                if (spec.ShotLeftOfFacing != 0)
                {
                    EnemyClipSpec shoot = spec.Clip("Shoot");
                    float arm = ArmYawAt(model, spec, "Shoot", shoot.Strike, facing);
                    report.Append("; at the release the arrow (right hand through left) points ").Append(F(-arm)).Append(" deg left of the facing (table ")
                          .Append(F(spec.ShotLeftOfFacing)).Append(")");
                }
                Debug.Log(report.ToString());
            }
            Assert.That(faults, Is.Empty, string.Join("\n", faults));
        }

        /// <summary>Plays one clip on the model at a time and calls back after each frame of 1/30 s is posed.</summary>
        static void Sample(GameObject model, AnimationClip clip, Action<double> posed)
        {
            if (!model.TryGetComponent(out Animator animator)) animator = model.AddComponent<Animator>();
            PlayableGraph graph = PlayableGraph.Create("Sample");
            try
            {
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                var output = AnimationPlayableOutput.Create(graph, "Animation", animator);
                var playable = AnimationClipPlayable.Create(graph, clip);
                playable.SetSpeed(0);
                output.SetSourcePlayable(playable);
                graph.Play();
                int frames = Mathf.CeilToInt(clip.length * (float)EnemyModelSpec.FramesPerSecond);
                for (int i = 0; i <= frames; i++)
                {
                    double t = Math.Min(clip.length, i / EnemyModelSpec.FramesPerSecond);
                    playable.SetTime(t);
                    graph.Evaluate(0f);
                    posed(t);
                }
            }
            finally
            {
                graph.Destroy();
            }
        }

        /// <summary>
        /// Metres a second the clip's stride covers at its own speed, as imported: each foot sweeps back under the body
        /// by a step, and a cycle is two steps.
        /// </summary>
        static float StrideSpeed(GameObject model, EnemyModelSpec spec, string name)
        {
            AnimationClip clip = EnemyLook.FindClip(spec, name);
            if (clip == null || clip.length <= 0f) return 0f;
            Transform hips = EnemyLook.Find(model.transform, "Hips");
            float total = 0f;
            foreach (string side in new[] { "LeftFoot", "RightFoot" })
            {
                Transform foot = EnemyLook.Find(model.transform, side);
                if (foot == null || hips == null) return 0f;
                float lo = float.PositiveInfinity, hi = float.NegativeInfinity;
                Vector3 forward = Quaternion.Euler(0f, EnemyLook.FacingOf(model, model.transform.parent), 0f) * Vector3.forward;
                Sample(model, clip, _ =>
                {
                    float along = Vector3.Dot(foot.position - hips.position, forward);
                    lo = Mathf.Min(lo, along);
                    hi = Mathf.Max(hi, along);
                });
                total += hi - lo;
            }
            return total / clip.length; // two steps a cycle, each the sweep of one foot: the mean sweep times two
        }

        /// <summary>Degrees to the right of the facing that an arrow on the bow points, flat, at that time into the clip.</summary>
        static float ArmYawAt(GameObject model, EnemyModelSpec spec, string name, double time, float facing)
        {
            AnimationClip clip = EnemyLook.FindClip(spec, name);
            Transform arm = EnemyLook.Find(model.transform, "RightHand"), hand = EnemyLook.Find(model.transform, "LeftHand");
            if (clip == null || arm == null || hand == null) return 0f;
            float yaw = 0f;
            Sample(model, clip, t =>
            {
                if (Math.Abs(t - time) > 0.5 / EnemyModelSpec.FramesPerSecond) return;
                Vector3 d = hand.position - arm.position;
                yaw = Mathf.DeltaAngle(facing, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            });
            return yaw;
        }
    }
}
