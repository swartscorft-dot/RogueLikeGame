using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 150, a look order's proof: the WEAPON tab's stage with the nine parts as their Blender models, taken off screen
    /// in batch mode with the stage's own camera, lights and size (its texture as the tab makes it at 1920 by 1080), and the
    /// tab's whole screen at 1920 by 1080 (the interface drawn by hand, <see cref="ImguiByHand"/>). The bare pistol; each of the
    /// nine alone at rest, owned and as a ghost, each beside the same view with <see cref="WeaponStageValues.ModelledParts"/> off
    /// (the shape made in code), and the model near, from the other side and from above; the three fullest sets (one sight, one
    /// barrel, one grip; the three show all nine), the same four ways and near. Contact sheets: <c>sheet-&lt;part&gt;.png</c>
    /// (top: model, shape; bottom: model as a ghost, shape as a ghost) and, when ROGUELIKE_PARTS_BLENDER names the folder of the
    /// Blender pictures, <c>blender-&lt;part&gt;.png</c> (left the Blender picture, right the stage: at rest, the other side,
    /// from above). Nothing here judges them: they are looked at.
    ///
    /// Runs ONLY when ROGUELIKE_CAPTURE_150 names a folder outside the project (otherwise ignored):
    ///   $env:ROGUELIKE_CAPTURE_150 = 'C:\Users\kriss\.claude-automation\runner\captures\150'
    ///   $env:ROGUELIKE_PARTS_BLENDER = 'E:\repos\A-Roguelike-Game\models-incoming\weapon-parts-2026-11-26\pictures'
    ///   run-tests.ps1 PlayMode -Filter "WeaponPartsModelledPicturesTests"
    /// </summary>
    public class WeaponPartsModelledPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE_150";
        const string BlenderVariable = "ROGUELIKE_PARTS_BLENDER";
        const int W = 1920, H = 1080;
        /// <summary>The Blender pictures' other side and from above, in the stage's turn: the rest's yaw mirrored, and tipped to 62 degrees.</summary>
        const float OtherSideYaw = 180f - WeaponStageValues.RestYaw, AbovePitch = 62f;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        static readonly string[] Nine =
        {
            WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.ReflexSight,
            WeaponAttachments.BullBarrel, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
            WeaponAttachments.RunnersGrip, WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
        };

        static readonly string[][] Sets =
        {
            new[] { WeaponAttachments.LaserSight, WeaponAttachments.BullBarrel, WeaponAttachments.RunnersGrip },
            new[] { WeaponAttachments.ReflexSight, WeaponAttachments.LongBarrel, WeaponAttachments.WeightedGrip },
            new[] { WeaponAttachments.GhostRing, WeaponAttachments.Compensator, WeaponAttachments.QuickDrawGrip },
        };

        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SkillBar _bar;
        string _folder, _saves;
        Dictionary<string, int?> _prefs;
        Vector2 _view; // the stage's size in layout pixels, as the tab sets it
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 150)");
            Directory.CreateDirectory(_folder);
            _prefs = SettingsPrefs.Save();
            UpgradeTabsValues.On = true;
            WeaponPartValues.GentlerCosts = true;
            MenuHelpValues.On = true;
            WeaponStage.LoadHook = null;
            WeaponStage.PartLoadHook = null;
            WeaponStageValues.ModelledParts = true;
            _saves = Path.Combine(Path.GetTempPath(), "WeaponPartsModelledPicturesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            Object.FindAnyObjectByType<PlayerCharacter>().GetComponent<PlayerInputDriver>().InputSource = () => null;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (WeaponStage.Current != null) Object.DestroyImmediate(WeaponStage.Current.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            AttachmentsSwitch.Unload();
            WeaponStageValues.ModelledParts = true;
            WeaponStage.PartLoadHook = null;
            SettingsPrefs.Restore(_prefs);
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        void Save(int echoes, string[] owned, string[] fitted)
        {
            var s = new MetaState { Currency = echoes, EarnedInAll = 400, Runs = 6 };
            if (owned != null) s.AttachmentsOwned.AddRange(owned);
            if (fitted != null) s.AttachmentsFitted.AddRange(fitted);
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
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
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assume.That(_menu.PickMain(MainLine.Upgrades), Is.True);
            for (int i = 0; i < 4; i++) yield return null;
        }

        IEnumerator Close()
        {
            _menu.Close();
            for (int i = 0; i < 3; i++) yield return null;
        }

        static string FileName(string id) => WeaponStageValues.PartModelName(id);

        // ---------------- The tab's whole screen ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator A_TheWeaponTab_WholeScreen_ASetFitted_AndAGhostPreview_ModelsAndShapes()
        {
            foreach (bool modelled in new[] { true, false })
            {
                WeaponStageValues.ModelledParts = modelled;
                string tag = modelled ? "model" : "shape";
                Save(20, Sets[1], Sets[1]);
                yield return OpenWeapon();
                yield return HeldAtRest();
                yield return Picture("tab-" + tag + "-set2", "the WEAPON tab, " + string.Join(", ", Sets[1]) + " fitted, at rest, parts as " + (modelled ? "models" : "shapes made in code"));
                int row = _hud.TabItemOf(_menu, WeaponSlot.Barrel, WeaponAttachments.BullBarrel);
                Assume.That(row, Is.GreaterThanOrEqualTo(0));
                _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Item, row).center);
                yield return null;
                yield return Picture("tab-" + tag + "-ghost-bull", "the cursor on the Bull Barrel, not owned: a ghost on the model (" + tag + ")");
                yield return Close();
            }
        }

        /// <summary>The tab's model held at rest (as a drag holds it: it neither coasts nor turns by itself while the pictures are taken).</summary>
        IEnumerator HeldAtRest()
        {
            WeaponStage stage = _menu.Stage;
            Assume.That(stage, Is.Not.Null);
            stage.Turn.BeginDrag();
            stage.Turn.Yaw = WeaponStageValues.RestYaw;
            stage.Turn.Pitch = WeaponStageValues.RestPitch;
            yield return null;
            yield return null;
        }

        // ---------------- The stage's own pictures ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator B_TheStage_ThePistol_EachPartAlone_AndTheThreeSets_ModelBesideShape_OwnedAndGhost()
        {
            // The stage's size as the tab makes it at this screen.
            Save(0, null, null);
            yield return OpenWeapon();
            WeaponStage tabStage = _menu.Stage;
            Assume.That(tabStage != null && tabStage.Texture != null, Is.True);
            float scale = WeaponStageValues.Supersample * Mathf.Max(1f, Screen.height / 1080f);
            _view = new Vector2(tabStage.Texture.width / scale, tabStage.Texture.height / scale);
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} STAGE the tab's stage texture {1}x{2} ({3} samples), screen {4}x{5}: pictures taken at that size", DateTime.Now, tabStage.Texture.width, tabStage.Texture.height, tabStage.Texture.antiAliasing, Screen.width, Screen.height));
            yield return Close();
            yield return null;

            string blender = Environment.GetEnvironmentVariable(BlenderVariable);
            float rest = WeaponStageValues.RestYaw, tip = WeaponStageValues.RestPitch, far = WeaponStageValues.RestDistance, near = WeaponStageValues.NearestDistance;

            Texture2D bare = Shot("pistol-bare", new string[3], false, true, rest, tip, far, "the bare pistol at rest");
            Object.Destroy(bare);
            Object.Destroy(Shot("pistol-bare-near", new string[3], false, true, rest, tip, near, "the bare pistol at rest, the wheel at its nearest"));
            yield return null;

            foreach (string id in Nine)
            {
                var on = new string[3];
                on[(int)SlotOf(id)] = id;
                string n = FileName(id);
                Texture2D mOwned = Shot(n + "-model", on, false, true, rest, tip, far, id + " alone, its model, owned, at rest");
                Texture2D sOwned = Shot(n + "-shape", on, false, false, rest, tip, far, id + " alone, its shape made in code (the switch off), owned, at rest");
                Texture2D mGhost = Shot(n + "-model-ghost", on, true, true, rest, tip, far, id + " alone, its model as a ghost (not owned), at rest");
                Texture2D sGhost = Shot(n + "-shape-ghost", on, true, false, rest, tip, far, id + " alone, its shape as a ghost (the switch off), at rest");
                Sheet("sheet-" + n, new[] { mOwned, sOwned, mGhost, sGhost }, 2, id + ": top the model and the shape made in code, owned; bottom the same as ghosts");
                Texture2D mNear = Shot(n + "-model-near", on, false, true, rest, tip, near, id + " alone, its model, the wheel at its nearest");
                Texture2D mOther = Shot(n + "-model-other-side", on, false, true, OtherSideYaw, tip, far, id + " alone, its model, from the other side");
                Texture2D mAbove = Shot(n + "-model-above", on, false, true, rest, AbovePitch, far, id + " alone, its model, from above");
                if (!string.IsNullOrEmpty(blender))
                {
                    var b = new[] { LoadPicture(Path.Combine(blender, n + "-rest.png")), LoadPicture(Path.Combine(blender, n + "-other-side.png")), LoadPicture(Path.Combine(blender, n + "-above.png")) };
                    Sheet("blender-" + n, new[] { b[0], mOwned, b[1], mOther, b[2], mAbove }, 2, id + ": left the Blender picture the producer judged, right the stage (at rest, the other side, from above)");
                    foreach (Texture2D t in b) if (t != null) Object.Destroy(t);
                }
                foreach (Texture2D t in new[] { mOwned, sOwned, mGhost, sGhost, mNear, mOther, mAbove }) Object.Destroy(t);
                yield return null;
            }

            for (int k = 0; k < Sets.Length; k++)
            {
                string n = "set" + (k + 1);
                string names = string.Join(", ", Sets[k]);
                Texture2D mOwned = Shot(n + "-model", Sets[k], false, true, rest, tip, far, names + ": the models, owned, at rest");
                Texture2D sOwned = Shot(n + "-shape", Sets[k], false, false, rest, tip, far, names + ": the shapes made in code, owned, at rest");
                Texture2D mGhost = Shot(n + "-model-ghost", Sets[k], true, true, rest, tip, far, names + ": the models as ghosts, at rest");
                Texture2D sGhost = Shot(n + "-shape-ghost", Sets[k], true, false, rest, tip, far, names + ": the shapes as ghosts, at rest");
                Sheet("sheet-" + n, new[] { mOwned, sOwned, mGhost, sGhost }, 2, names + ": top the models and the shapes, owned; bottom as ghosts");
                Texture2D mNear = Shot(n + "-model-near", Sets[k], false, true, rest, tip, near, names + ": the models, the wheel at its nearest");
                Texture2D mOther = Shot(n + "-model-other-side", Sets[k], false, true, OtherSideYaw, tip, far, names + ": the models from the other side");
                foreach (Texture2D t in new[] { mOwned, sOwned, mGhost, sGhost, mNear, mOther }) Object.Destroy(t);
                yield return null;
            }
        }

        static WeaponSlot SlotOf(string id) => Array.IndexOf(Nine, id) / 3 == 0 ? WeaponSlot.Sight : Array.IndexOf(Nine, id) / 3 == 1 ? WeaponSlot.Barrel : WeaponSlot.Grip;

        /// <summary>
        /// A stage of its own, built for this picture (as the tab builds it: the same camera, lights and size), these parts on it,
        /// turned and as near as asked, drawn once; written as a file and handed back (the caller frees it).
        /// </summary>
        Texture2D Shot(string name, string[] parts, bool ghost, bool modelled, float yaw, float pitch, float distance, string what)
        {
            WeaponStageValues.ModelledParts = modelled;
            if (WeaponStage.Current != null) Object.DestroyImmediate(WeaponStage.Current.gameObject);
            WeaponStage stage = WeaponStage.Ensure();
            stage.SetView(_view.x, _view.y);
            stage.Keep();
            for (int s = 0; s < 3; s++) stage.Show((WeaponSlot)s, parts[s], ghost && parts[s] != null);
            stage.Turn.BeginDrag();
            stage.Turn.Yaw = yaw;
            stage.Turn.Pitch = pitch;
            stage.Turn.Distance = distance;
            stage.DrawIfChanged();
            RenderTexture rt = stage.Texture;
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
            var words = new List<string>();
            for (int s = 0; s < 3; s++)
                if (parts[s] != null) words.Add(parts[s] + (stage.PartModelled((WeaponSlot)s) ? " model" : " shape") + (stage.Ghosted((WeaponSlot)s) ? " ghost" : ""));
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}; yaw {5:0} pitch {6:0} distance {7:0.00}; shift {8:0.000}; on it: [{9}]", DateTime.Now, name, rt.width, rt.height, what, yaw, pitch, distance, stage.Shift.x, string.Join(", ", words)));
            WeaponStageValues.ModelledParts = true;
            return tex;
        }

        Texture2D LoadPicture(string path)
        {
            if (!File.Exists(path)) return null;
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (t.LoadImage(File.ReadAllBytes(path))) return t;
            Object.Destroy(t);
            return null;
        }

        /// <summary>Pictures in a grid of <paramref name="columns"/>, each at half the stage's size, kept to its shape (an empty tile for a missing one), as one file.</summary>
        void Sheet(string name, Texture2D[] tiles, int columns, string what)
        {
            int tw = Mathf.RoundToInt(_view.x), th = Mathf.RoundToInt(_view.y); // half the stage texture (it is twice the layout)
            int rows = (tiles.Length + columns - 1) / columns;
            var sheet = new RenderTexture(tw * columns, th * rows, 0, RenderTextureFormat.ARGB32);
            try
            {
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = sheet;
                GL.Clear(true, true, Color.black);
                RenderTexture.active = was;
                for (int i = 0; i < tiles.Length; i++)
                    if (tiles[i] != null) DrawInto(sheet, tiles[i], new Rect((i % columns) * tw, (i / columns) * th, tw, th));
                RenderTexture.active = sheet;
                var tex = new Texture2D(sheet.width, sheet.height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, sheet.width, sheet.height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}", DateTime.Now, name, sheet.width, sheet.height, what));
            }
            finally
            {
                sheet.Release();
                Object.Destroy(sheet);
            }
        }

        /// <summary>A texture drawn into a rectangle of <paramref name="target"/> (pixels from its top left), kept to its own shape (fitted inside it, centred).</summary>
        static void DrawInto(RenderTexture target, Texture source, Rect into)
        {
            float aspectIn = into.width / into.height, aspectSource = source.width / (float)source.height;
            if (aspectSource > aspectIn) into = new Rect(into.x, into.center.y - into.width / aspectSource * 0.5f, into.width, into.width / aspectSource);
            else into = new Rect(into.center.x - into.height * aspectSource * 0.5f, into.y, into.height * aspectSource, into.height);
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = target;
            GL.PushMatrix();
            GL.LoadPixelMatrix(0f, target.width, target.height, 0f);
            Graphics.DrawTexture(into, source, new Rect(0f, 0f, 1f, 1f), 0, 0, 0, 0);
            GL.PopMatrix();
            RenderTexture.active = was;
        }

        // ---------------- A picture of the whole screen ----------------

        IEnumerator Picture(string name, string what)
        {
            yield return null;
            var rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(W, H);
                cam.targetTexture = rt;
                if (_bar != null) _bar.Tick();
                _hud.Tick();
                _menu.Tick(null);
                if (WeaponStage.Current != null) WeaponStage.Current.DrawIfChanged();
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                WeaponStage stage = WeaponStage.Current;
                string stageWords = stage != null && stage.Texture != null ? string.Format(Inv, "; stage {0}x{1}, yaw {2:0} pitch {3:0}", stage.Texture.width, stage.Texture.height, stage.Turn.Yaw, stage.Turn.Pitch) : "";
                _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}; text outside: [{5}]{6}; drawn: {7}", DateTime.Now, name, W, H, what, StartMenu.Holds ? _menu.TextOutside() : "", stageWords, drew));
            }
            catch (Exception e) { _log.Add(name + " failed: " + e); }
            finally
            {
                cam.targetTexture = before;
                InterfaceStyle.ScreenForPictures = Vector2Int.zero;
                rt.Release();
                Object.Destroy(rt);
            }
        }
    }
}
