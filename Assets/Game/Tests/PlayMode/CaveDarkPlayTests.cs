using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The cave, second attempt (work order 083; Kristian, 2026-11-22: "Hva er det poe2 og diablo gjør ... Er kameraet mer
    /// zoomet inn og veggene høyere?"), built in the ControlRoom scene through <see cref="GeneratedLevels"/>: each of the three
    /// looks has its objects and none of the others' (the dark look: the stacked pieces, the veils and caps, the low walls cut
    /// to the lip, the falloff on the camera; 076's rock: its faces and tops; before: no cave); the dark look's pieces without
    /// collider or shadow, as high as the values say, the kit, the colliders and the NavMesh as with the cave off; its frame
    /// allocates nothing; the camera's distance step is kept, the camera stands at it, and the pause menu has its line beside
    /// the kick. Writes what it costs ("CAVEDARKCOST").
    /// </summary>
    public class CaveDarkPlayTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x41414141UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        int? _distanceBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            // The developer's own setting is in the same PlayerPrefs: kept aside and put back.
            _distanceBefore = PlayerPrefs.HasKey(GameSettings.CameraDistanceKey) ? PlayerPrefs.GetInt(GameSettings.CameraDistanceKey) : (int?)null;
            GameSettings.SetCameraDistanceStep(0, false);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.SetCameraDistanceStep(0, false);
            if (_distanceBefore.HasValue) PlayerPrefs.SetInt(GameSettings.CameraDistanceKey, _distanceBefore.Value);
            else PlayerPrefs.DeleteKey(GameSettings.CameraDistanceKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel Build(ulong seed, int floor, Action<LevelValues> change = null)
        {
            _levels.Values = new LevelValues();
            change?.Invoke(_levels.Values);
            GeneratedLevel level = _levels.Build(seed, floor);
            Assert.That(level, Is.Not.Null, "a level was built");
            return level;
        }

        static IEnumerable<GeneratedLevel.KitPiece> Pieces(GeneratedLevel level, CryptPiece kind) => level.KitPieces.Where(p => p.Kind == kind);

        static Transform CameraFalloff() => Camera.main != null ? Camera.main.transform.Find("CaveFalloff") : null;

        [UnityTest]
        public IEnumerator TheThreeLooks_EachHasItsObjects_AndNoneOfTheOthers()
        {
            // The dark look (the default).
            GeneratedLevel dark = Build(42UL, 1);
            yield return Frames(3);
            CaveView cave = dark.Cave;
            CaveValues v = dark.Values.CaveRules;
            Assert.That(cave, Is.Not.Null);
            Assert.That(cave.LookBuilt, Is.EqualTo(CaveLook.Dark), "the default look");
            Assert.That(dark.transform.Find(CaveView.DarkRootName), Is.Not.Null);
            Assert.That(dark.transform.Find(CaveView.RockRootName), Is.Null, "no rock");
            Assert.That(cave.Rock.Count + cave.Lids.Count, Is.Zero, "076's faces and tops not built");
            Assert.That(cave.Raised, Is.GreaterThan(0), "far walls raised");
            Assert.That(cave.Lips, Is.EqualTo(dark.KitPlan.LowSections), "every near wall a lip");
            Assert.That(cave.Stacked.Count, Is.GreaterThanOrEqualTo(cave.Raised), "a stacked piece over every raised wall, and over its piers");
            Assert.That(cave.DarkMeshes.Count, Is.GreaterThan(0), "veils and caps");
            Assert.That(CameraFalloff(), Is.Not.Null, "the falloff on the camera");
            Assert.That(cave.Falloff, Is.SameAs(CameraFalloff().gameObject));
            Assert.That(cave.LipScale, Is.EqualTo(v.LipHeight / CryptKit.Load(CryptPiece.LowWall).Bounds.max.y).Within(1e-3f));
            foreach (GeneratedLevel.KitPiece p in Pieces(dark, CryptPiece.LowWall))
                Assert.That(p.Object.transform.localScale.y, Is.EqualTo(cave.LipScale).Within(1e-4f), "a near wall cut to the lip: " + p.Object.name);
            foreach (GameObject go in cave.Stacked)
                foreach (MeshRenderer r in go.GetComponentsInChildren<MeshRenderer>(true))
                {
                    Assert.That(r.GetComponentInParent<Collider>(), Is.Null, "no collider: " + go.name);
                    Assert.That(r.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off), "no shadow: " + go.name);
                    Assert.That(r.gameObject.layer, Is.EqualTo(CryptKit.Layer));
                    Assert.That(r.bounds.max.y, Is.LessThanOrEqualTo(v.FarHeight + 0.2f), "no higher than the far height: " + go.name);
                    Assert.That(r.bounds.min.y, Is.GreaterThan(cave.WallTop - 0.5f), "over the kit's wall: " + go.name);
                }
            Assert.That(cave.Stacked.Max(g => g.GetComponentsInChildren<Renderer>().Max(r => r.bounds.max.y)), Is.GreaterThan(v.FarHeight - 0.2f), "the tall ones reach the far height");
            foreach (MeshRenderer r in cave.DarkMeshes)
            {
                Assert.That(r.GetComponent<Collider>(), Is.Null);
                Assert.That(r.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
                Assert.That(r.bounds.max.y, Is.LessThanOrEqualTo(v.FarHeight + 0.1f), r.name);
            }
            string cost = string.Format(Inv, "CAVEDARKCOST seed 42 floor 1: far sections raised {0}, held {1}; near sections cut {2}; stacked pieces {3} ({4} renderers, {5} triangles); veil and cap meshes {6} ({7} triangles); kit pieces {8}",
                                        cave.Raised, cave.Held, cave.Lips, cave.Stacked.Count, cave.Stacked.Sum(g => g.GetComponentsInChildren<Renderer>().Length),
                                        cave.Stacked.Sum(g => g.GetComponentsInChildren<MeshFilter>().Sum(f => (long)f.sharedMesh.GetIndexCount(0) / 3)),
                                        cave.DarkMeshes.Count, cave.DarkMeshes.Sum(r => r.GetComponent<MeshFilter>().sharedMesh.triangles.Length / 3), dark.KitPieces.Count);
            Debug.Log(cost);
            TestContext.WriteLine(cost);

            // 076's rock.
            GeneratedLevel rock = Build(42UL, 1, x => x.CaveRules.Look = CaveLook.Rock);
            yield return Frames(3);
            Assert.That(rock.Cave, Is.Not.Null);
            Assert.That(rock.Cave.LookBuilt, Is.EqualTo(CaveLook.Rock));
            Assert.That(rock.Cave.Rock.Count, Is.GreaterThan(0), "076's faces");
            Assert.That(rock.Cave.Lids.Count, Is.GreaterThan(0), "076's tops");
            Assert.That(rock.transform.Find(CaveView.DarkRootName), Is.Null);
            Assert.That(rock.Cave.Stacked.Count + rock.Cave.DarkMeshes.Count, Is.Zero);
            Assert.That(CameraFalloff(), Is.Null, "no falloff");
            Assert.That(Pieces(rock, CryptPiece.LowWall).All(p => p.Object.transform.localScale == Vector3.one), "the low walls as the kit has them");

            // Before 076.
            GeneratedLevel before = Build(42UL, 1, x => x.CaveRules.Look = CaveLook.Before);
            yield return Frames(3);
            Assert.That(before.Cave, Is.Null);
            Assert.That(before.GetComponent<CaveView>(), Is.Null);
            Assert.That(before.transform.Find(CaveView.DarkRootName), Is.Null);
            Assert.That(before.transform.Find(CaveView.RockRootName), Is.Null);
            Assert.That(CameraFalloff(), Is.Null);
            Assert.That(Pieces(before, CryptPiece.LowWall).All(p => p.Object.transform.localScale == Vector3.one));
            Assert.That(Pieces(before, CryptPiece.Post).All(p => p.Object.transform.localScale == Vector3.one));
            Assert.That(before.GetComponentsInChildren<Renderer>(true).Any(r => r.forceRenderingOff), Is.False, "every renderer drawn");
        }

        [UnityTest]
        public IEnumerator TheDarkLook_TheKitTheCollidersAndTheNavMesh_AsWithTheCaveOff()
        {
            var pieces = new Dictionary<bool, int>();
            var colliders = new Dictionary<bool, string>();
            var navmesh = new Dictionary<bool, int>();
            foreach (bool on in new[] { false, true })
            {
                // The heaps kept for this comparison: their going changes the vault's colliders on purpose (076's chest test).
                GeneratedLevel level = Build(42UL, 5, v => { v.Cave = on; v.CaveRules.NoGoldHeaps = false; });
                yield return Frames(3);
                pieces[on] = level.KitPieces.Count;
                colliders[on] = string.Join(";", level.GetComponentsInChildren<Collider>(true).Where(c => c.GetComponentInParent<Enemy>(true) == null)
                                                     .Select(c => c.name + " " + c.bounds.center.ToString("F2") + " " + c.bounds.size.ToString("F2")).OrderBy(s => s, StringComparer.Ordinal));
                navmesh[on] = NavMesh.CalculateTriangulation().vertices.Length;
                if (on) Assert.That(level.Cave.LookBuilt, Is.EqualTo(CaveLook.Dark));
            }
            Assert.That(pieces[true], Is.EqualTo(pieces[false]), "no kit piece more: the stacked pieces are the cave's");
            Assert.That(colliders[true], Is.EqualTo(colliders[false]), "the colliders as with the cave off");
            Assert.That(navmesh[true], Is.EqualTo(navmesh[false]), "the NavMesh as with the cave off");
        }

        [UnityTest]
        public IEnumerator TheDarkLook_EveryRendererBelongsToARoom_AndItsFrameAllocatesNothing()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return Frames(2);
            CaveView cave = level.Cave;
            Assert.That(cave.LookBuilt, Is.EqualTo(CaveLook.Dark));
            foreach (GameObject go in cave.Stacked)
                foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
                    Assert.That(cave.OwnersOf(r), Is.Not.Empty, "a stacked piece belongs to a room: " + go.name);
            foreach (MeshRenderer r in cave.DarkMeshes) Assert.That(cave.OwnersOf(r), Is.Not.Empty, r.name);
            LevelRoom a = level.Layout.Rooms[2], b = level.Layout.Rooms[3];
            _player.Teleport(level.Middle(a));
            yield return Frames(3);
            cave.Tick();
            cave.Tick();
            Assert.That(() => { for (int i = 0; i < 300; i++) cave.Tick(); }, AllocExt.AllocatingGCMemory(Is.Not), "the cave's frame allocates nothing");
            Assert.That(() => { for (int i = 0; i < 50; i++) cave.Look(i % 2 == 0 ? b.Order : a.Order); }, AllocExt.AllocatingGCMemory(Is.Not), "a change of room allocates nothing");
            // The falloff is sized as the camera begins to draw: drawn again without a change, nothing is made.
            // Into a texture of its own (without a window there is no screen to draw to: drawn there, the engine falls over).
            Camera cam = Camera.main;
            var rt = new RenderTexture(320, 180, 24);
            RenderTexture was = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            Vector3 scale = cave.Falloff.transform.localScale;
            cam.Render();
            float aspect = cam.pixelWidth / (float)cam.pixelHeight;
            cam.targetTexture = was;
            rt.Release();
            Object.Destroy(rt);
            Assert.That(cave.Falloff.transform.localScale, Is.EqualTo(scale));
            Assert.That(scale.x / scale.y, Is.EqualTo(aspect).Within(1e-3f), "it fills the picture's shape");
            Assert.That(aspect, Is.EqualTo(320f / 180f).Within(1e-3f));
        }

        [UnityTest]
        public IEnumerator TheCameraDistance_IsKept_TheCameraStandsAtIt_AndThePauseMenuStepsIt()
        {
            Camera cam = Camera.main;
            CameraRig rig = cam.GetComponent<CameraRig>();
            float feel = rig.Settings != null ? rig.Settings.Values.CameraDistance : new FeelValues().CameraDistance;
            Assert.That(feel, Is.EqualTo(28f), "the default stays 28 m");
            float Distance()
            {
                Vector3 focus = new Vector3(rig.Target.position.x, 0f, rig.Target.position.z);
                return Vector3.Distance(cam.transform.position, focus);
            }
            yield return Frames(2);
            Assert.That(Distance(), Is.EqualTo(28f).Within(0.05f), "at step 0 the feel's own");

            GameSettings.SetCameraDistanceStep(2);
            Assert.That(PlayerPrefs.GetInt(GameSettings.CameraDistanceKey, -1), Is.EqualTo(2), "kept");
            yield return Frames(2);
            Assert.That(Distance(), Is.EqualTo(22f).Within(0.05f), "the camera at 22 m at once");
            GameSettings.Unload();
            Assert.That(GameSettings.CameraDistanceStep, Is.EqualTo(0), "forgotten until it is read");
            GameSettings.Load();
            Assert.That(GameSettings.CameraDistanceStep, Is.EqualTo(2), "read back from PlayerPrefs");

            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            // The pause menu: its line right after the camera's kick, stepping 22 to 28, 25, 22.
            RunHud hud = RunHud.Ensure();
            yield return null;
            hud.Escape();
            yield return null;
            List<RunHud.MenuLine> lines = hud.MenuLines.ToList();
            int kick = lines.IndexOf(RunHud.MenuLine.CameraKick);
            Assert.That(kick, Is.GreaterThanOrEqualTo(0));
            Assert.That(lines[kick + 1], Is.EqualTo(RunHud.MenuLine.CameraDistance), "beside the kick");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(hud.MenuText(RunHud.MenuLine.CameraDistance), Is.EqualTo("Camera distance:  22 m"));
            var seen = new List<string>();
            for (int i = 0; i < 3; i++)
            {
                Assert.That(hud.PickMenuLine(RunHud.MenuLine.CameraDistance), Is.True);
                seen.Add(hud.MenuText(RunHud.MenuLine.CameraDistance));
            }
            Assert.That(seen, Is.EqualTo(new[] { "Camera distance:  28 m (standard)", "Camera distance:  25 m", "Camera distance:  22 m" }));
            Assert.That(hud.ChangeSetting(RunHud.MenuLine.CameraDistance, -1), Is.True);
            Assert.That(GameSettings.CameraDistanceStep, Is.EqualTo(1), "the left arrow steps back");
            Assert.That(PlayerPrefs.GetInt(GameSettings.CameraDistanceKey, -1), Is.EqualTo(1));
            Assert.That(hud.MenuText(RunHud.MenuLine.CameraDistance), Is.EqualTo("Camera distance:  25 m")); // made once for the new step
            Assert.That(() => { hud.MenuText(RunHud.MenuLine.CameraDistance); }, AllocExt.AllocatingGCMemory(Is.Not), "the line's words are made once a step");
            hud.Escape();
            Object.Destroy(hud.gameObject);
            yield return null;
        }
    }
}
