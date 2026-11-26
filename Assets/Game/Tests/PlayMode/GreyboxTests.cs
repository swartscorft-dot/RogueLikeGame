using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The greybox view (work order 084; Kristian ticked "Gråboks-bryter og romlab" on 2026-11-22), in the ControlRoom scene
    /// through the intents of <see cref="GeneratedLevels"/> and <see cref="RunHud"/>: on and off in the middle of a run, the
    /// floor not built again, its colliders and its NavMesh the same; nothing of the kit, the mood or the cave drawn with it
    /// on, and all of it back as it was with it off; the props as boxes of their colliders in the colour of what they do;
    /// the doors and the fight's line; the pause menu's line beside the camera kick, the developer's G, and the setting kept in
    /// PlayerPrefs (put back as it was after each test); a new floor following it; the legend in the developer's box; its
    /// frame allocating nothing.
    ///
    /// Frames without a window are short (about 0.3 ms): the waits count frames, never a frame's length.
    /// </summary>
    public class GreyboxTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const ulong RunSeed = 0x08408408UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        bool _hadPref;
        int _pref;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            // The setting as this machine keeps it, put back after the test: a test keeps nothing.
            _hadPref = PlayerPrefs.HasKey(GameSettings.GreyboxKey);
            _pref = PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            PlayerHealth health = _player.GetComponent<PlayerHealth>();
            if (health != null) health.Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            GameSettings.SetGreybox(false, keep: false);
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true; // work order 128: the switch as it ships, whatever a test set
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_player != null)
            {
                _player.GetComponent<PlayerInputDriver>().InputSource = null;
                PlayerHealth health = _player.GetComponent<PlayerHealth>();
                if (health != null) health.Untouchable = false;
            }
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            if (_hadPref) PlayerPrefs.SetInt(GameSettings.GreyboxKey, _pref);
            else PlayerPrefs.DeleteKey(GameSettings.GreyboxKey);
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        GeneratedLevel StartRun()
        {
            _levels.Values = new LevelValues();
            GeneratedLevel level = _levels.StartRun(RunSeed);
            Assert.That(level, Is.Not.Null);
            return level;
        }

        IEnumerator Turn(bool on)
        {
            GameSettings.SetGreybox(on, keep: false);
            yield return Frames(2);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.EqualTo(on), "the floor follows the setting");
        }

        // ---------------- What does not change ----------------

        static string Colliders(GeneratedLevel level)
        {
            var lines = new List<string>();
            foreach (Collider c in level.GetComponentsInChildren<Collider>(true))
            {
                Bounds b = c.bounds;
                lines.Add(string.Format(Inv, "{0}|{1}|{2}|{3}|{4:F3},{5:F3},{6:F3}|{7:F3},{8:F3},{9:F3}|{10}", c.name, c.GetType().Name, c.enabled, c.isTrigger,
                                        b.center.x, b.center.y, b.center.z, b.size.x, b.size.y, b.size.z, c.gameObject.layer));
            }
            lines.Sort(StringComparer.Ordinal);
            return lines.Count + "\n" + string.Join("\n", lines);
        }

        static string NavMeshPrint()
        {
            NavMeshTriangulation t = NavMesh.CalculateTriangulation();
            double sx = 0, sy = 0, sz = 0;
            foreach (Vector3 v in t.vertices)
            {
                sx += v.x;
                sy += v.y;
                sz += v.z;
            }
            long idx = 0;
            foreach (int i in t.indices) idx = idx * 31 + i;
            return string.Format(Inv, "{0} vertices, {1} indices, sums {2:F2} {3:F2} {4:F2}, index print {5}", t.vertices.Length, t.indices.Length, sx, sy, sz, idx);
        }

        [UnityTest]
        public IEnumerator OnAndOff_MidRun_TheFloorIsNotBuiltAgain_AndItsCollidersAndNavMeshAreTheSame()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(3);
            string colliders = Colliders(level), navMesh = NavMeshPrint();
            Vector3[] enemiesAt = level.Enemies.Select(e => e.transform.position).ToArray();

            yield return Turn(true);
            Assert.That(_levels.Level, Is.SameAs(level), "the same floor: not built again");
            Assert.That(_levels.InRun, Is.True);
            Assert.That(Colliders(level), Is.EqualTo(colliders), "every collider of the floor as it was, the view on");
            Assert.That(NavMeshPrint(), Is.EqualTo(navMesh), "the NavMesh as it was, the view on");
            Assert.That(level.Enemies.Select(e => e.transform.position).ToArray(), Is.EqualTo(enemiesAt), "every enemy where it stood");
            Assert.That(_levels.Greybox.Made.All(r => r.GetComponent<Collider>() == null), Is.True, "nothing the view made has a collider");

            yield return Turn(false);
            Assert.That(_levels.Level, Is.SameAs(level));
            Assert.That(Colliders(level), Is.EqualTo(colliders), "the view off: the colliders as they were");
            Assert.That(NavMeshPrint(), Is.EqualTo(navMesh), "the view off: the NavMesh as it was");

            yield return Turn(true);
            yield return Turn(false);
            Assert.That(Colliders(level), Is.EqualTo(colliders), "on and off again: the same");
            Assert.That(_levels.Greybox.TimesOn, Is.EqualTo(2), "turned on twice, built once");
        }

        // ---------------- What is drawn ----------------

        [UnityTest]
        public IEnumerator On_NothingOfTheKitTheMoodOrTheCaveIsDrawn_AndOff_AllOfItIsBack()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(3);
            Assume.That(level.KitRoot, Is.Not.Null, "the crypt kit is on");
            Assume.That(level.Mood, Is.Not.Null, "the mood is on");
            Assume.That(level.Cave, Is.Not.Null, "the cave is on");
            var before = level.GetComponentsInChildren<Renderer>(true).ToDictionary(r => r, r => (r.enabled, r.forceRenderingOff, r.sharedMaterial));
            Camera cam = Camera.main;
            Color background = cam != null ? cam.backgroundColor : Color.clear;

            yield return Turn(true);
            GreyboxView view = _levels.Greybox;
            Renderer[] kit = level.KitRoot.GetComponentsInChildren<Renderer>(true);
            Assume.That(kit.Length, Is.GreaterThan(0));
            Assert.That(kit.Count(r => r.enabled), Is.EqualTo(0), "no piece of the kit is drawn");
            Assert.That(level.KitRoot.GetComponentsInChildren<Light>(true).Count(l => l.enabled), Is.EqualTo(0), "no lantern light is on");
            Assert.That(level.Mood.enabled, Is.False, "the mood is off");
            Assert.That(level.Mood.Root == null || !level.Mood.Root.activeInHierarchy, Is.True, "nothing of the mood is drawn: its lights, its air, its pieces");
            Assert.That(RoomMood.SceneLightTaken, Is.False, "the scene's light given back: even and neutral");
            Assert.That(level.Cave.enabled, Is.False, "the cave is off");
            Assert.That(level.Cave.Rock.Count(r => r != null && r.enabled), Is.EqualTo(0), "no cave rock is drawn");
            Assert.That(level.SolidCaps.Count(c => c.GetComponent<Renderer>().enabled), Is.EqualTo(0), "no carved cell's dark top");
            foreach (GameObject f in level.FloorBoxes)
            {
                Renderer r = f.GetComponent<Renderer>();
                Assert.That(r.enabled && !r.forceRenderingOff, Is.True, f.name + " is drawn");
                Assert.That(r.sharedMaterial.name, Is.EqualTo("Greybox_Floor"), f.name + " in the floor's grey");
            }
            foreach (GameObject w in level.WallBoxes)
            {
                Renderer r = w.GetComponent<Renderer>();
                Assert.That(r.enabled && !r.forceRenderingOff, Is.True, w.name + " is drawn: the wall's collision shape");
                Assert.That(r.sharedMaterial.name, Is.EqualTo("Greybox_Wall"));
            }
            foreach (Enemy e in level.Enemies)
                foreach (Renderer r in e.GetComponentsInChildren<Renderer>(true))
                    Assert.That(r.forceRenderingOff, Is.False, e.name + " is drawn as it is (the cave hides nothing)");
            Assert.That(view.DoorPieces, Is.EqualTo(level.Layout.Doors.Count * 3), "a strip and two posts at every door");
            Assert.That(view.FightLinePieces, Is.GreaterThan(0), "the fight's line is on the floor");
            // Every renderer of the floor not the view's: a floor or wall box, something drawn as it is, or not drawn at all.
            var mine = new HashSet<Renderer>(view.Made);
            var keep = new HashSet<Renderer>(level.FloorBoxes.Concat(level.WallBoxes).Select(g => g.GetComponent<Renderer>()));
            DoorSigns signs = level.GetComponent<DoorSigns>();
            foreach (Renderer r in level.GetComponentsInChildren<Renderer>(true))
            {
                if (mine.Contains(r) || keep.Contains(r) || !r.enabled) continue;
                bool asItIs = r.GetComponentInParent<Enemy>(true) != null || r.GetComponentInParent<RewardDrop>(true) != null
                              || (signs != null && signs.Root != null && r.transform.IsChildOf(signs.Root))
                              || level.Props.Any(p => p.Prop.Model == PropModel.Chest && p.Object != null && r.transform.IsChildOf(p.Object.transform));
                Assert.That(asItIs, Is.True, r.name + " is drawn with the view on, and is neither the view's nor gameplay");
            }

            yield return Turn(false);
            foreach (KeyValuePair<Renderer, (bool enabled, bool forceOff, Material material)> pair in before)
            {
                Renderer r = pair.Key;
                if (r == null || r.GetComponentInParent<Enemy>(true) != null) continue; // an enemy's own logic may have changed its own meanwhile
                Assert.That(r.enabled, Is.EqualTo(pair.Value.enabled), r.name + ": drawn as before");
                Assert.That(r.forceRenderingOff, Is.EqualTo(pair.Value.forceOff), r.name + ": the cave's hiding as before");
                Assert.That(r.sharedMaterial, Is.SameAs(pair.Value.material), r.name + ": its own material back");
            }
            Assert.That(level.Mood.enabled && level.Mood.Root.activeInHierarchy, Is.True, "the mood is back");
            Assert.That(level.Cave.enabled, Is.True, "the cave is back");
            Assert.That(view.Made.Count(r => r.enabled), Is.EqualTo(0), "nothing of the view is drawn");
            if (cam != null) Assert.That(cam.backgroundColor, Is.EqualTo(background), "the camera's background as the cave had it");
        }

        [UnityTest]
        public IEnumerator TheProps_AreBoxesOfTheirColliders_InTheColourOfWhatTheyDo()
        {
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            yield return Turn(true);
            GreyboxView view = _levels.Greybox;
            int boxes = 0, high = 0, low = 0;
            foreach (GeneratedLevel.PlacedProp p in level.Props.Concat(level.LargeProps))
            {
                if (p.Object == null) continue;
                Collider c = p.Object.GetComponent<Collider>();
                Renderer box = view.Made.FirstOrDefault(r => r.transform.parent == p.Object.transform);
                foreach (Renderer r in p.Object.GetComponentsInChildren<Renderer>(true))
                    if (r != box && p.Prop.Model != PropModel.Chest) Assert.That(r.enabled, Is.False, p.Object.name + ": its model is not drawn");
                if (p.Prop.Model == PropModel.Chest)
                {
                    Assert.That(box, Is.Null, "a chest is drawn as it is");
                    Assert.That(p.Object.GetComponentsInChildren<Renderer>(true).Any(r => r.enabled), Is.True, "the chest's model is drawn");
                    continue;
                }
                if (c == null)
                {
                    Assert.That(box, Is.Null, p.Object.name + " has no collider: not shown");
                    continue;
                }
                Assert.That(box, Is.Not.Null, p.Object.name + " is a box");
                boxes++;
                // In the prop's own frame (it is turned): a box collider's centre and size, a capsule's centre and its round's width.
                Vector3 centre = c is BoxCollider bc ? bc.center : c is CapsuleCollider cc ? cc.center : p.Object.transform.InverseTransformPoint(c.bounds.center);
                Vector3 size = c is BoxCollider bc2 ? bc2.size : c is CapsuleCollider cc2 ? new Vector3(cc2.radius * 2f, cc2.height, cc2.radius * 2f) : c.bounds.size;
                Assert.That(Vector3.Distance(box.transform.localPosition, centre), Is.LessThan(0.02f), p.Object.name + ": the box stands where its collider does");
                Assert.That(Vector3.Distance(box.transform.localScale, size), Is.LessThan(0.02f), p.Object.name + ": the box is its collider's size");
                Assert.That(Quaternion.Angle(box.transform.localRotation, Quaternion.identity), Is.LessThan(0.1f), p.Object.name + ": turned as the prop is");
                Cover cover = p.Object.GetComponent<Cover>();
                string want = cover != null && cover.Stops ? "Greybox_HighCover" : p.Object.GetComponent<Shelter>() != null ? "Greybox_LowCover" : "Greybox_BlocksWalking";
                Assert.That(box.sharedMaterial.name, Is.EqualTo(want), p.Object.name + " (" + p.Prop.Model + ")");
                if (want == "Greybox_HighCover") high++;
                if (want == "Greybox_LowCover") low++;
            }
            Debug.Log(string.Format(Inv, "GREYBOXPROPS {0} boxes, {1} high cover, {2} low cover", boxes, high, low));
            Assert.That(boxes, Is.GreaterThan(0));
            Assert.That(high + low, Is.GreaterThan(0), "a floor holds cover");
        }

        // ---------------- The switch ----------------

        IEnumerator Tap(Key key)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(key));
            for (int i = 0; i < 60 && !_keyboard[key].isPressed; i++) yield return null;
            yield return null;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            for (int i = 0; i < 60 && _keyboard.anyKey.isPressed; i++) yield return null;
            yield return null;
        }

        [UnityTest]
        public IEnumerator ThePauseMenusLine_BesideTheCameraKick_TheDevelopersG_AndTheSettingKept()
        {
            _hud = RunHud.Ensure(); // it loads the settings: this machine's, put back after the test
            yield return null;
            SettingsScreenValues.On = false; // work order 128: this test pins the long Esc menu of before (the switch off); the short one is SettingsScreenPlayTests'
            GameSettings.SetGreybox(false); // kept: off
            GeneratedLevel level = StartRun();
            yield return Frames(2);
            Assert.That(_levels.Pause(), Is.True);
            yield return null;
            List<RunHud.MenuLine> lines = _hud.MenuLines.ToList();
            Assert.That(lines.IndexOf(RunHud.MenuLine.Greybox), Is.EqualTo(lines.IndexOf(RunHud.MenuLine.CameraDistance) + 1), "its line, under the camera's two lines (the kick, then work order 083's distance)");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line still last");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Greybox), Is.EqualTo("Greybox view:  off"), "off by default");
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Greybox, 1), Is.True);
            Assert.That(_hud.MenuText(RunHud.MenuLine.Greybox), Is.EqualTo("Greybox view:  on"));
            yield return Frames(2);
            Assert.That(_levels.Greybox != null && _levels.Greybox.On, Is.True, "the floor follows it under the pause menu, at once");
            Assert.That(PlayerPrefs.GetInt(GameSettings.GreyboxKey, 0), Is.EqualTo(1), "kept in PlayerPrefs, as the camera kick's");
            _levels.Resume();
            yield return Frames(2);
            Assert.That(_levels.Level, Is.SameAs(level), "the run goes on, on the same floor");
            GameSettings.Unload();
            Assert.That(GameSettings.Greybox, Is.False, "unloaded: off, as for a test that never made the HUD");
            GameSettings.Load();
            Assert.That(GameSettings.Greybox, Is.True, "loaded again: on, as it was kept");

            // The developer's G (the Editor's developer keys are on): off, and kept so.
            Assume.That(DeveloperKeys.On, Is.True);
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            yield return Tap(GeneratedLevels.GreyboxKey);
            Assert.That(GameSettings.Greybox, Is.False, "G turned it off");
            Assert.That(_levels.Greybox.On, Is.False);
            Assert.That(PlayerPrefs.GetInt(GameSettings.GreyboxKey, 1), Is.EqualTo(0), "and kept that");
            Assert.That(_levels.Level, Is.SameAs(level), "not built again");
            yield return Tap(GeneratedLevels.GreyboxKey);
            Assert.That(_levels.Greybox.On, Is.True, "G again: on");
        }

        [UnityTest]
        public IEnumerator TheNextFloor_FollowsTheSetting_AndTheDevelopersBox_SaysWhatTheColoursMean()
        {
            GeneratedLevel first = StartRun();
            yield return Turn(true);
            LevelRoom templated = first.Layout.Rooms.First(r => r.Kind == RoomKind.Combat && r.Template != null && r.Shape == RoomShape.Template);
            (int x, int y) open = (templated.Rect.X, templated.Rect.Y);
            for (int y = templated.Rect.Y; y < templated.Rect.Top; y++)
                for (int x = templated.Rect.X; x < templated.Rect.Right; x++)
                    if (templated.IsOpen(x, y) && !templated.IsOpen(open.x, open.y)) open = (x, y);
            float cs = first.Layout.CellSize;
            _player.Teleport(first.ToWorld(new LevelPoint((open.x + 0.5) * cs, (open.y + 0.5) * cs)));
            yield return Frames(3);
            Assert.That(_levels.CurrentRoom, Is.SameAs(templated));
            string text = _levels.StatusText();
            StringAssert.Contains("GREYBOX", text);
            StringAssert.Contains("red: high cover", text);
            StringAssert.Contains("blue: low cover", text);
            StringAssert.Contains("here: template ", text, "the room's template's name");
            StringAssert.Contains(" m", text);
            GeneratedLevel second = _levels.Build(RunSeed + 1, 2);
            yield return Frames(2);
            Assert.That(second, Is.Not.SameAs(first));
            Assert.That(_levels.Greybox, Is.Not.Null.And.Property("Level").SameAs(second), "the new floor has its own view");
            Assert.That(_levels.Greybox.On, Is.True, "and follows the setting");
            GameSettings.SetGreybox(false, keep: false);
            yield return Frames(2);
            Assert.That(_levels.StatusText(), Does.Not.Contain("GREYBOX"), "off: no legend");
        }

        [UnityTest]
        public IEnumerator ItsFrame_AllocatesNothing_WhileNothingChanges()
        {
            StartRun();
            yield return Turn(true);
            for (int i = 0; i < 10; i++) _levels.FollowGreybox();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.FollowGreybox(); }, AllocExt.AllocatingGCMemory(Is.Not), "on: its frame allocates nothing");
            yield return Turn(false);
            for (int i = 0; i < 10; i++) _levels.FollowGreybox();
            Assert.That(() => { for (int i = 0; i < 300; i++) _levels.FollowGreybox(); }, AllocExt.AllocatingGCMemory(Is.Not), "off: nothing");
        }
    }
}
