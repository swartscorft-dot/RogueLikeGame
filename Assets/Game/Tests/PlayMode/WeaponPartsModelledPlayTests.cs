using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 150 (Kristian, 2026-11-26: "Ja, lag de seks og bygg inn"): the nine weapon parts on the WEAPON tab's stage as
    /// their Blender models, behind <see cref="WeaponStageValues.ModelledParts"/>. On the stage alone: each id gives its model's
    /// mesh at its mount, in the pistol's frame and on the right side of it (a sight above the rib, a barrel out of the muzzle,
    /// a grip below the slide); the shapes made in code with the switch off, child for child as before the order; a missing
    /// model falls back to the shape with one warning; the three barrels' reaches and the frame's shift; the ghost materials on
    /// a part not owned; every model found through Resources, as a built player finds it. Through the start menu: the WEAPON
    /// tab shows each of the nine in turn, owned and as a ghost, with nothing logged, and allocates nothing while it stands still.
    /// His saved things are never touched: the save and the records are the test's own.
    /// </summary>
    public class WeaponPartsModelledPlayTests
    {
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        static readonly string[] Nine =
        {
            WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.ReflexSight,
            WeaponAttachments.BullBarrel, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
            WeaponAttachments.RunnersGrip, WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
        };

        static WeaponSlot SlotOf(string id) => Array.IndexOf(Nine, id) / 3 == 0 ? WeaponSlot.Sight : Array.IndexOf(Nine, id) / 3 == 1 ? WeaponSlot.Barrel : WeaponSlot.Grip;

        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        string _saves;
        bool _hadAttachmentsKey;
        int _attachmentsKey;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _saves = Path.Combine(Path.GetTempPath(), "WeaponPartsModelledPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // never the game's own save
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName); // never his records
            _hadAttachmentsKey = PlayerPrefs.HasKey(AttachmentsSwitch.Key);
            _attachmentsKey = PlayerPrefs.GetInt(AttachmentsSwitch.Key, AttachmentsSwitch.NoOverride);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            UpgradeTabsValues.On = true;
            WeaponPartValues.GentlerCosts = true;
            MenuHelpValues.On = true;
            WeaponStage.LoadHook = null;
            WeaponStage.PartLoadHook = null;
            WeaponStageValues.ModelledParts = true;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<PlayerCharacter>().GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            WeaponStage.LoadHook = null;
            WeaponStage.PartLoadHook = null;
            WeaponStageValues.ModelledParts = true;
            if (WeaponStage.Current != null) Object.DestroyImmediate(WeaponStage.Current.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            if (_hadAttachmentsKey) PlayerPrefs.SetInt(AttachmentsSwitch.Key, _attachmentsKey);
            else PlayerPrefs.DeleteKey(AttachmentsSwitch.Key);
            PlayerPrefs.Save();
            AttachmentsSwitch.Unload();
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The stage alone ----------------

        /// <summary>A stage of its own, built now (kept this frame), at rest, nothing on it.</summary>
        static WeaponStage FreshStage()
        {
            if (WeaponStage.Current != null) Object.DestroyImmediate(WeaponStage.Current.gameObject);
            WeaponStage stage = WeaponStage.Ensure();
            stage.Keep();
            return stage;
        }

        static GameObject ModelAsset(string id) => Resources.Load<GameObject>(WeaponStageValues.PartModelPath(id));

        /// <summary>The part's corners' box in the turning model's space (the pivot the mounts stand on), from its meshes' own bounds.</summary>
        static Bounds BoxOnTheModel(WeaponStage stage, WeaponSlot slot)
        {
            Transform pivot = stage.MountOn(slot).parent;
            bool any = false;
            Bounds box = default;
            foreach (MeshFilter f in stage.PartOn(slot).GetComponentsInChildren<MeshFilter>())
            {
                Bounds b = f.sharedMesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = pivot.InverseTransformPoint(f.transform.TransformPoint(corner));
                    if (!any) box = new Bounds(p, Vector3.zero);
                    else box.Encapsulate(p);
                    any = true;
                }
            }
            return box;
        }

        static string[] MaterialNames(Transform part) =>
            part.GetComponentsInChildren<MeshRenderer>().SelectMany(r => r.sharedMaterials).Select(m => m != null ? m.name : "null").Distinct().OrderBy(n => n).ToArray();

        [Test]
        public void EveryPartsModel_IsFoundThroughResources_AsABuiltPlayerFindsIt_WithItsMaterialsByName()
        {
            foreach (string id in Nine)
            {
                string path = WeaponStageValues.PartModelPath(id);
                Assert.That(path, Does.StartWith(WeaponStageValues.PartModelFolder), id);
                GameObject model = ModelAsset(id);
                Assert.That(model, Is.Not.Null, id + ": Resources/" + path);
                MeshFilter[] filters = model.GetComponentsInChildren<MeshFilter>(true);
                Assert.That(filters.Length, Is.EqualTo(1), id + ": one mesh");
                Mesh mesh = filters[0].sharedMesh;
                Assert.That(mesh, Is.Not.Null, id);
                long tris = 0;
                for (int s = 0; s < mesh.subMeshCount; s++) tris += mesh.GetIndexCount(s) / 3;
                Assert.That(tris, Is.InRange(200, 40000), id + ": a small model (the Blender files: about 1,000 to 8,000 triangles)");
                Assert.That(mesh.isReadable, Is.False, id + ": not kept on the CPU");
                string[] names = filters[0].GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name).ToArray();
                Assert.That(names, Has.Some.StartsWith("Gunmetal").Or.Some.StartsWith("Bronze"), id + ": the materials keep their Blender names");
                Assert.That(names, Has.Some.StartsWith("Teal"), id + ": its teal line");
            }
            Assert.That(WeaponStageValues.PartModelPath("no-such-part"), Is.Null);
        }

        [Test]
        public void EachOfTheNine_WithTheSwitchOn_IsItsModelsMesh_AtItsMount_InThePistolsFrame_OnTheRightSideOfIt()
        {
            WeaponStage stage = FreshStage();
            Vector3 half = stage.Half;
            Assert.That(stage.ModelSource, Is.EqualTo(WeaponStage.Source.NamedPlace), "the high-detail pistol on the stage");
            Vector3 pistolScale = stage.GetComponentsInChildren<MeshFilter>().First(f => stage.ModelMeshes.Contains(f.sharedMesh)).transform.lossyScale;
            Assert.That(stage.Half.x * 2f, Is.EqualTo(WeaponStageValues.ModelLength).Within(1e-3f));
            foreach (string id in Nine)
            {
                WeaponSlot slot = SlotOf(id);
                stage.Show(slot, id, false);
                Transform part = stage.PartOn(slot), mount = stage.MountOn(slot);
                Assert.That(part, Is.Not.Null, id);
                Assert.That(part.parent, Is.SameAs(mount), id + ": at its mount");
                Assert.That(stage.PartModelled(slot), Is.True, id + ": its model");
                Transform holder = part.Find(WeaponStage.PartModelChild);
                Assert.That(Vector3.Distance(holder.position, mount.position), Is.LessThan(1e-5f), id + ": its origin on the mount");
                Assert.That(Quaternion.Angle(holder.rotation, mount.parent.rotation), Is.LessThan(0.01f), id + ": in the pistol's frame (the grip's lean is in its shape, not the mount's turn)");
                MeshFilter[] shown = part.GetComponentsInChildren<MeshFilter>();
                Assert.That(shown.Select(f => f.sharedMesh), Is.EqualTo(ModelAsset(id).GetComponentsInChildren<MeshFilter>(true).Select(f => f.sharedMesh)), id + ": the model's mesh, and nothing else");
                Assert.That(Vector3.Distance(shown[0].transform.lossyScale, pistolScale), Is.LessThan(1e-4f * pistolScale.magnitude),
                            id + ": at the pistol's scale (made in its units: the pistol 1.903 long there, drawn 1 long here)");
                Assert.That(MaterialNames(part), Is.SubsetOf(new[] { "WeaponStage_ModelGunmetal", "WeaponStage_ModelBronze", "WeaponStage_ModelTeal", "WeaponStage_ModelDot", "WeaponStage_ModelGlass" }), id + ": the stage's own materials");
                Assert.That(MaterialNames(part), Does.Contain("WeaponStage_ModelTeal"), id);
                Bounds box = BoxOnTheModel(stage, slot);
                Vector3 at = mount.localPosition;
                switch (slot)
                {
                    case WeaponSlot.Sight:
                        Assert.That(box.min.y, Is.GreaterThan(at.y - 0.03f), id + ": on the rib, not sunk into the slide (its base 0.018 into the rib's crown)");
                        Assert.That(box.max.y, Is.GreaterThan(at.y + 0.04f), id + ": standing up from it, not upside down");
                        Assert.That(box.max.y, Is.LessThan(at.y + 0.2f), id);
                        Assert.That(Mathf.Abs(box.center.z), Is.LessThan(0.01f), id + ": on the middle line");
                        break;
                    case WeaponSlot.Barrel:
                        Assert.That(box.min.x, Is.GreaterThan(at.x - 0.004f), id + ": nothing of it behind the muzzle's face (not into the gun)");
                        Assert.That(Mathf.Abs(box.center.y - at.y), Is.LessThan(0.01f), id + ": on the bore's line");
                        Assert.That(Mathf.Abs(box.center.z), Is.LessThan(0.01f), id);
                        break;
                    case WeaponSlot.Grip:
                        Assert.That(box.max.y, Is.LessThan(at.y + 0.1f), id + ": on the handle, below the slide");
                        Assert.That(box.min.y, Is.GreaterThan(-half.y - 0.12f), id + ": not hanging far under the foot");
                        Assert.That(box.center.x, Is.LessThan(0f), id + ": at the back half, where the handle is");
                        Assert.That(Mathf.Abs(box.center.z), Is.LessThan(0.01f), id + ": on the middle line");
                        break;
                }
            }
        }

        [Test]
        public void TheThreeBarrels_ReachTheirModelledLength_AndTheFrameShiftsBackByItsShare()
        {
            WeaponStage stage = FreshStage();
            var reach = new Dictionary<string, float>
            {
                { WeaponAttachments.BullBarrel, WeaponStageValues.BullBarrelReach },
                { WeaponAttachments.LongBarrel, WeaponStageValues.LongBarrelReach },
                { WeaponAttachments.Compensator, WeaponStageValues.CompensatorReach },
            };
            Assert.That(new[] { WeaponStageValues.BullBarrelReach, WeaponStageValues.LongBarrelReach, WeaponStageValues.CompensatorReach }, Is.EqualTo(new[] { 0.128f, 0.263f, 0.135f }), "the reaches measured in Blender");
            foreach (var r in reach)
            {
                stage.Show(WeaponSlot.Barrel, r.Key, false);
                Assert.That(stage.Shift.x, Is.EqualTo(-r.Value * WeaponStageValues.FrameShiftShare).Within(1e-5f), r.Key + ": the frame's shift by the model's reach");
                Bounds box = BoxOnTheModel(stage, WeaponSlot.Barrel);
                Assert.That(box.max.x - stage.MountOn(WeaponSlot.Barrel).localPosition.x, Is.EqualTo(r.Value).Within(0.006f), r.Key + ": the model reaches as far as the value says");
            }
            stage.Show(WeaponSlot.Barrel, null, false);
            Assert.That(stage.Shift, Is.EqualTo(Vector3.zero), "no barrel: no shift");
        }

        [Test]
        public void APartNotOwned_IsTheSameModel_InTheGhostMaterials()
        {
            WeaponStage stage = FreshStage();
            foreach (string id in Nine)
            {
                WeaponSlot slot = SlotOf(id);
                stage.Show(slot, id, true);
                Assert.That(stage.Ghosted(slot), Is.True);
                Assert.That(stage.PartModelled(slot), Is.True, id + ": the model as a ghost too");
                Assert.That(stage.PartOn(slot).GetComponentInChildren<MeshFilter>().sharedMesh, Is.SameAs(ModelAsset(id).GetComponentInChildren<MeshFilter>(true).sharedMesh), id);
                Assert.That(MaterialNames(stage.PartOn(slot)), Is.SubsetOf(new[] { "WeaponStage_GhostBody", "WeaponStage_GhostAccent", "WeaponStage_GhostDot" }), id + ": every face in a ghost material");
                foreach (MeshRenderer r in stage.PartOn(slot).GetComponentsInChildren<MeshRenderer>())
                    foreach (Material m in r.sharedMaterials)
                        Assert.That(m.renderQueue, Is.GreaterThanOrEqualTo((int)UnityEngine.Rendering.RenderQueue.Transparent), id + ": see-through");
                stage.Show(slot, id, false);
                Assert.That(MaterialNames(stage.PartOn(slot)), Has.None.StartsWith("WeaponStage_Ghost"), id + ": owned, solid again");
            }
        }

        [Test]
        public void AMissingModel_FallsBackToItsShapeMadeInCode_WithOneWarning_NeverAnEmptyMount()
        {
            var empty = new GameObject("NotAModel");
            try
            {
                WeaponStage.PartLoadHook = id => id == WeaponAttachments.LongBarrel ? null : id == WeaponAttachments.WeightedGrip ? empty : ModelAsset(id);
                WeaponStage stage = FreshStage();
                LogAssert.Expect(LogType.Warning, new Regex(@"\[WeaponStage\] no model at Resources/Weapon/Parts/LongBarrel"));
                stage.Show(WeaponSlot.Barrel, WeaponAttachments.LongBarrel, false);
                Assert.That(stage.PartOn(WeaponSlot.Barrel), Is.Not.Null, "never an empty mount");
                Assert.That(stage.PartModelled(WeaponSlot.Barrel), Is.False);
                Assert.That(stage.PartOn(WeaponSlot.Barrel).GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh.name), Is.EqualTo(new[] { "Cylinder", "WeaponStageRing", "WeaponStageRing" }), "its shape made in code");
                stage.Show(WeaponSlot.Barrel, WeaponAttachments.LongBarrel, true); // made again (as a ghost): no second warning
                stage.Show(WeaponSlot.Barrel, null, false);
                stage.Show(WeaponSlot.Barrel, WeaponAttachments.LongBarrel, false);
                Assert.That(stage.PartOn(WeaponSlot.Barrel).childCount, Is.EqualTo(3));
                LogAssert.Expect(LogType.Warning, new Regex(@"\[WeaponStage\] no model at Resources/Weapon/Parts/WeightedGrip"));
                stage.Show(WeaponSlot.Grip, WeaponAttachments.WeightedGrip, false); // a model with no mesh in it: the same
                Assert.That(stage.PartModelled(WeaponSlot.Grip), Is.False);
                Assert.That(stage.PartOn(WeaponSlot.Grip).Find("Foot"), Is.Not.Null, "its shape made in code");
                stage.Show(WeaponSlot.Sight, WeaponAttachments.LaserSight, false);
                Assert.That(stage.PartModelled(WeaponSlot.Sight), Is.True, "the others still their models");
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                WeaponStage.PartLoadHook = null;
                Object.Destroy(empty);
            }
        }

        // ---------------- The switch off: the shapes made in code, as before the order ----------------

        /// <summary>One shape of a part as the drawing of work order 139 makes it: its mesh, its material, where, how turned, how large.</summary>
        struct Shape
        {
            public string Mesh, Material;
            public Vector3 At, Turn, Size;

            public Shape(string mesh, string material, Vector3 at, Vector3 turn, Vector3 size)
            {
                Mesh = mesh;
                Material = material;
                At = at;
                Turn = turn;
                Size = size;
            }
        }

        static Shape Box(string m, Vector3 at, Vector3 size) => new Shape("Cube", m, at, Vector3.zero, size);
        static Shape Tube(string m, Vector3 at, float length, float radius) => new Shape("Cylinder", m, at, new Vector3(0f, 0f, 90f), new Vector3(radius * 2f, length * 0.5f, radius * 2f));
        static Shape Ring(string m, Vector3 at, float radius) => new Shape("WeaponStageRing", m, at, new Vector3(0f, 90f, 0f), Vector3.one * radius);
        static Shape Ball(string m, Vector3 at, float size) => new Shape("Sphere", m, at, Vector3.zero, Vector3.one * size);
        static Shape Band(string m, int k, float along) => new Shape("WeaponStageBand" + k, m, new Vector3(along, 0f, 0f), Vector3.zero, Vector3.one);

        const string Body = "WeaponStage_Body", Trim = "WeaponStage_Trim", Accent = "WeaponStage_Accent", Dot = "WeaponStage_Dot";

        /// <summary>Today's shapes (WeaponStage.MakePart before work order 150), restated: the part's own children, and its foot's.</summary>
        static Shape[] Todays(string id, Vector3 half, out Shape[] foot)
        {
            float spacing = WeaponStageValues.GripBandSpacing * WeaponStageValues.ModelLength;
            float fd = WeaponStageValues.FootDepth * WeaponStageValues.ModelLength, ft = Mathf.Max(0.04f, half.z * 2f * WeaponStageValues.FootThickness);
            const float Long = 0.26f;
            foot = null;
            switch (id)
            {
                case WeaponAttachments.LaserSight:
                    return new[] { Box(Trim, new Vector3(0f, 0.012f, 0f), new Vector3(0.1f, 0.024f, 0.036f)), Tube(Body, new Vector3(0f, 0.05f, 0f), 0.16f, 0.026f), Ring(Accent, new Vector3(-0.055f, 0.05f, 0f), 0.026f), Ball(Dot, new Vector3(0.082f, 0.05f, 0f), 0.034f) };
                case WeaponAttachments.GhostRing:
                    return new[] { Box(Body, new Vector3(0f, 0.028f, 0f), new Vector3(0.03f, 0.056f, 0.03f)), Ring(Trim, new Vector3(0f, 0.1f, 0f), 0.044f), Ball(Accent, new Vector3(0f, 0.148f, 0f), 0.016f) };
                case WeaponAttachments.ReflexSight:
                    return new[]
                    {
                        Box(Body, new Vector3(0f, 0.01f, 0f), new Vector3(0.11f, 0.02f, 0.056f)), Box(Trim, new Vector3(0.025f, 0.055f, 0.027f), new Vector3(0.022f, 0.074f, 0.008f)),
                        Box(Trim, new Vector3(0.025f, 0.055f, -0.027f), new Vector3(0.022f, 0.074f, 0.008f)), Box(Trim, new Vector3(0.025f, 0.094f, 0f), new Vector3(0.022f, 0.01f, 0.062f)),
                        Box(Accent, new Vector3(0.025f, 0.054f, 0f), new Vector3(0.006f, 0.068f, 0.046f)),
                    };
                case WeaponAttachments.BullBarrel:
                    return new[] { Tube(Body, new Vector3(0.04f, 0f, 0f), 0.15f, 0.07f), Ring(Trim, new Vector3(0.006f, 0f, 0f), 0.068f), Ring(Accent, new Vector3(0.112f, 0f, 0f), 0.066f) };
                case WeaponAttachments.LongBarrel:
                    return new[] { Tube(Body, new Vector3(Long * 0.5f, 0f, 0f), Long, 0.036f), Ring(Trim, new Vector3(0.012f, 0f, 0f), 0.04f), Ring(Accent, new Vector3(Long - 0.005f, 0f, 0f), 0.034f) };
                case WeaponAttachments.Compensator:
                    return new[] { Box(Body, new Vector3(0.065f, 0f, 0f), new Vector3(0.12f, 0.14f, 0.12f)), Box(Accent, new Vector3(0.045f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f)), Box(Accent, new Vector3(0.088f, 0.071f, 0f), new Vector3(0.014f, 0.006f, 0.09f)) };
                case WeaponAttachments.RunnersGrip:
                    return new[] { Band(Trim, 0, -spacing), Band(Accent, 1, 0f), Band(Trim, 2, spacing) };
                case WeaponAttachments.WeightedGrip:
                    foot = new[]
                    {
                        Box(Body, new Vector3(0f, -0.024f, 0f), new Vector3(fd * 1.04f, 0.05f, ft * 1.16f)),
                        Box(Accent, new Vector3(0f, -0.024f, -ft * 0.58f - 0.003f), new Vector3(fd * 0.7f, 0.026f, 0.006f)),
                        Box(Accent, new Vector3(0f, -0.024f, ft * 0.58f + 0.003f), new Vector3(fd * 0.7f, 0.026f, 0.006f)),
                    };
                    return new Shape[0];
                case WeaponAttachments.QuickDrawGrip:
                    foot = new[] { Box(Trim, new Vector3(0f, 0.012f, 0f), new Vector3(fd * 1.12f, 0.03f, ft * 1.22f)) };
                    float back = WeaponStageValues.HandleDepth * WeaponStageValues.ModelLength * 0.5f - 0.008f;
                    return new[] { Box(Accent, new Vector3(0f, -back, 0f), new Vector3(2.2f * spacing, 0.008f, 0.018f)) };
            }
            return null;
        }

        static void AssertShapes(Transform parent, IList<Shape> want, string what)
        {
            var got = new List<Transform>();
            foreach (Transform c in parent)
                if (c.name != "Foot") got.Add(c);
            Assert.That(got.Count, Is.EqualTo(want.Count), what + ": as many shapes as before");
            for (int i = 0; i < want.Count; i++)
            {
                Transform t = got[i];
                string w = what + " shape " + i;
                Assert.That(t.name, Is.EqualTo(want[i].Mesh), w);
                Assert.That(t.GetComponent<MeshFilter>().sharedMesh.name, Is.EqualTo(want[i].Mesh), w);
                Assert.That(t.GetComponent<MeshRenderer>().sharedMaterials.Select(m => m.name), Is.EqualTo(new[] { want[i].Material }), w);
                Assert.That(Vector3.Distance(t.localPosition, want[i].At), Is.LessThan(1e-6f), w + " where");
                Assert.That(Quaternion.Angle(t.localRotation, Quaternion.Euler(want[i].Turn)), Is.LessThan(1e-3f), w + " turned");
                Assert.That(Vector3.Distance(t.localScale, want[i].Size), Is.LessThan(1e-6f), w + " how large");
                Assert.That(t.childCount, Is.EqualTo(0), w);
            }
        }

        [Test]
        public void TheSwitchOff_EachPartIsTodaysShapeMadeInCode_ChildForChild_AndTheFrameShiftsByTodaysReaches()
        {
            WeaponStageValues.ModelledParts = false;
            WeaponStage.PartLoadHook = id => { Assert.Fail("the switch off: no model is looked for"); return null; };
            WeaponStage stage = FreshStage();
            Vector3 half = stage.Half;
            foreach (string id in Nine)
            {
                WeaponSlot slot = SlotOf(id);
                foreach (bool ghost in new[] { false, true })
                {
                    stage.Show(slot, id, ghost);
                    Transform part = stage.PartOn(slot);
                    Assert.That(part.name, Is.EqualTo(id));
                    Assert.That(part.parent, Is.SameAs(stage.MountOn(slot)));
                    Assert.That(stage.PartModelled(slot), Is.False, id);
                    Assert.That(part.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(part.localRotation, Is.EqualTo(Quaternion.identity));
                    Shape[] want = Todays(id, half, out Shape[] foot);
                    if (ghost)
                    {
                        Shape[] Ghosted(Shape[] s) => s?.Select(x => new Shape(x.Mesh, x.Material == Accent ? "WeaponStage_GhostAccent" : x.Material == Dot ? "WeaponStage_GhostDot" : "WeaponStage_GhostBody", x.At, x.Turn, x.Size)).ToArray();
                        want = Ghosted(want);
                        foot = Ghosted(foot);
                    }
                    AssertShapes(part, want, id + (ghost ? " (ghost)" : ""));
                    Transform f = part.Find("Foot");
                    Assert.That(f != null, Is.EqualTo(foot != null), id + ": a foot where it had one");
                    if (f != null)
                    {
                        Assert.That(part.GetChild(0), Is.SameAs(f), id + ": the foot first, as before");
                        Transform mount = stage.MountOn(slot);
                        Vector3 footAt = new Vector3(WeaponStageValues.FootAt.X * half.x, WeaponStageValues.FootAt.Y * half.y, WeaponStageValues.FootAt.Z * half.z) + stage.Shift;
                        Quaternion undo = Quaternion.Inverse(mount.localRotation);
                        Assert.That(Vector3.Distance(f.localPosition, undo * (footAt - mount.localPosition)), Is.LessThan(1e-5f), id + ": the foot where it was");
                        Assert.That(Quaternion.Angle(f.localRotation, undo), Is.LessThan(1e-3f), id + ": level");
                        AssertShapes(f, foot, id + "'s foot");
                    }
                }
                if (slot == WeaponSlot.Barrel)
                {
                    float today = id == WeaponAttachments.BullBarrel ? 0.115f : id == WeaponAttachments.LongBarrel ? 0.26f : 0.125f;
                    Assert.That(stage.Shift.x, Is.EqualTo(-today * WeaponStageValues.FrameShiftShare).Within(1e-6f), id + ": today's reach");
                }
            }
            Assert.That(stage.GetComponentsInChildren<Renderer>().SelectMany(r => r.sharedMaterials).Select(m => m.name), Has.None.StartsWith("WeaponStage_Model"), "none of the models' materials made");
        }

        [Test]
        public void TheSwitchTurned_RemountMakesThePartsAgain_ModelOrShape()
        {
            WeaponStage stage = FreshStage();
            stage.Show(WeaponSlot.Barrel, WeaponAttachments.LongBarrel, false);
            Assert.That(stage.PartModelled(WeaponSlot.Barrel), Is.True);
            WeaponStageValues.ModelledParts = false;
            stage.Remount();
            Assert.That(stage.PartModelled(WeaponSlot.Barrel), Is.False, "off: the shape");
            Assert.That(stage.Shift.x, Is.EqualTo(-0.26f * WeaponStageValues.FrameShiftShare).Within(1e-6f), "and today's reach");
            WeaponStageValues.ModelledParts = true;
            stage.Remount();
            Assert.That(stage.PartModelled(WeaponSlot.Barrel), Is.True, "on: the model");
            Assert.That(stage.Shift.x, Is.EqualTo(-WeaponStageValues.LongBarrelReach * WeaponStageValues.FrameShiftShare).Within(1e-6f));
        }

        // ---------------- Through the start menu's WEAPON tab ----------------

        void TrialsOn(int echoes, string[] owned, string[] fitted)
        {
            var s = new MetaState { Currency = echoes };
            if (owned != null) s.AttachmentsOwned.AddRange(owned);
            if (fitted != null) s.AttachmentsFitted.AddRange(fitted);
            Assert.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            // The save is read once: after that the state in memory is the one shown, so it is set too.
            MetaState live = _levels.BetweenRunsState;
            if (live != null && !ReferenceEquals(live, s))
            {
                live.Currency = s.Currency;
                live.AttachmentsOwned.Clear();
                live.AttachmentsOwned.AddRange(s.AttachmentsOwned);
                live.AttachmentsFitted.Clear();
                live.AttachmentsFitted.AddRange(s.AttachmentsFitted);
            }
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
        }

        IEnumerator OpenWeapon()
        {
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            yield return null;
            yield return null;
        }

        IEnumerator CloseMenu()
        {
            _menu.Close();
            for (int i = 0; i < 3; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator TheWeaponTab_ShowsEachOfTheNine_AsAGhost_ThenOwned_WithNoWarningOrError()
        {
            var logged = new List<string>();
            Application.LogCallback hear = (text, stack, type) =>
            {
                if (type != LogType.Log) logged.Add(type + ": " + text);
            };
            Application.logMessageReceived += hear;
            try
            {
            foreach (bool owned in new[] { false, true })
            {
                TrialsOn(0, owned ? Nine : null, null);
                yield return OpenWeapon();
                WeaponStage stage = _menu.Stage;
                Assert.That(stage, Is.Not.Null);
                foreach (string id in Nine)
                {
                    WeaponSlot slot = SlotOf(id);
                    int row = _hud.TabItemOf(_menu, slot, id);
                    Assert.That(row, Is.GreaterThanOrEqualTo(0), id + ": its row");
                    _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, row).center);
                    yield return null;
                    yield return null;
                    Assert.That(stage.Shown(slot), Is.EqualTo(id), id + ": shown on the model");
                    Assert.That(stage.Ghosted(slot), Is.EqualTo(!owned), id + (owned ? ": hers, solid" : ": not hers, a ghost"));
                    Assert.That(stage.PartModelled(slot), Is.True, id + ": its model");
                    Assert.That(stage.Texture, Is.Not.Null);
                }
                yield return CloseMenu();
            }
            }
            finally { Application.logMessageReceived -= hear; }
            Assert.That(logged, Is.Empty, "no warning or error logged while the nine were shown");
        }

        [UnityTest]
        public IEnumerator NothingAllocated_WhileTheWeaponTabStandsStill_WithAModelOnEachMount()
        {
            string[] three = { WeaponAttachments.ReflexSight, WeaponAttachments.LongBarrel, WeaponAttachments.QuickDrawGrip };
            TrialsOn(0, three, three);
            yield return OpenWeapon();
            for (int i = 0; i < 4; i++)
            {
                _menu.Tick(null);
                _hud.Tick();
                _menu.LayOutForTests();
                yield return null;
            }
            WeaponStage stage = WeaponStage.Current;
            Assert.That(stage, Is.Not.Null);
            foreach (WeaponSlot slot in new[] { WeaponSlot.Sight, WeaponSlot.Barrel, WeaponSlot.Grip })
                Assert.That(stage.PartModelled(slot), Is.True, slot + ": a model on it");
            stage.Turn.BeginDrag(); // held: it neither coasts nor turns by itself
            stage.DrawIfChanged();
            int draws = stage.Draws;
            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++)
                {
                    _menu.Tick(null);
                    _hud.Tick();
                    _menu.LayOutForTests();
                    stage.DrawIfChanged();
                }
            }, NotAllocating, "nothing allocated in a frame that changes nothing");
            Assert.That(stage.Draws, Is.EqualTo(draws), "and not drawn again");
            _menu.ReleaseModel();
        }
    }
}
