using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 114: pictures of the start menu, taken without a window as work order 068's <see cref="InterfacePicturesTests"/>
    /// takes the interface's (the camera into a texture, every OnGUI called by hand, <see cref="ImguiByHand"/>), to compare with
    /// decision 0011 and the mock-ups. Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\114'
    ///   run-tests.ps1 PlayMode -Filter "StartMenuPicturesTests"
    ///
    /// Her picture's proposals stand in for the picture, loaded for the picture only from their folder
    /// (ROGUELIKE_MENU_PICTURES, else the producer's codex folder) and never added to the project. The trial's save and the
    /// records are staged in a temporary folder (the numbers on the pictures are staged, not his).
    /// </summary>
    public class StartMenuPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE", PicturesVariable = "ROGUELIKE_MENU_PICTURES";
        const string DefaultPictures = @"C:\Users\kriss\.claude-automation\runner\codex\images\menu";

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SkillBar _bar;
        string _folder, _saves;
        readonly List<string> _log = new List<string>();
        readonly List<Texture2D> _pictures = new List<Texture2D>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work orders 068 and 114)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _saves = Path.Combine(Path.GetTempPath(), "StartMenuPicturesTests-" + Guid.NewGuid().ToString("N"));
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
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _menu.ShowQuit = true; // the built player's main menu
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            foreach (Texture2D t in _pictures) if (t != null) Object.Destroy(t);
            _pictures.Clear();
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        Texture2D Proposal(string file)
        {
            string folder = Environment.GetEnvironmentVariable(PicturesVariable);
            string path = Path.Combine(string.IsNullOrEmpty(folder) ? DefaultPictures : folder, file);
            if (!File.Exists(path))
            {
                _log.Add("no proposal at " + path);
                return null;
            }
            // sRGB, as the import the producer will give it (round 1 read it as linear: it came out paler than the file).
            var t = new Texture2D(2, 2, TextureFormat.RGBA32, false, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = file };
            t.LoadImage(File.ReadAllBytes(path), false);
            _pictures.Add(t);
            return t;
        }

        void Stage(bool trialsOn)
        {
            if (!trialsOn)
            {
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
                return;
            }
            MetaValues v = _levels.MetaValues;
            var s = new MetaState { Currency = 135, EarnedInAll = 410, Runs = 9 };
            foreach (string name in new[] { "Thick Hide", "Steady Start", "Not That One" }) s.Owned.Add(v.Pieces.First(p => p.Name == name).Id);
            s.Equipped.Add(v.Pieces.First(p => p.Name == "Thick Hide").Id);
            s.Equipped.Add(v.Pieces.First(p => p.Name == "Steady Start").Id);
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            s.Ranks[v.Ranks[0].Id] = 2;
            s.Ranks[v.Ranks[1].Id] = 1;
            s.Feats.Add(MetaFeats.Id(MetaFeat.FirstFloor));
            s.Feats.Add(MetaFeats.Id(MetaFeat.ReachFloor3));
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            var r = new RunRecords { RunsStarted = 14, Deaths = 12, ActsCleared = 1, DeepestFloor = 5, Kills = 1083, LongestRunSeconds = 1311f };
            Assume.That(RecordsFile.Save(StartMenu.RecordsPath, r, out error), Is.True, error);
            v.Enabled = true;
            v.Attachments = true;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheStartMenu()
        {
            Stage(true);
            Texture2D a2 = Proposal(Path.Combine("round2", "char_bg_a2_kinetic.png"));
            Texture2D a = Proposal("char_bg_a_kinetic.png");
            Texture2D b = Proposal("char_bg_b_crypt.png");

            // The main menu: the flat backdrop, then the chosen picture.
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            yield return Picture("main-flat", 1920, 1080);
            _menu.PictureOverride = a2;
            yield return Picture("main-a2", 1920, 1080);
            yield return Picture("main-a2-720", 1280, 720);

            // Her character: the flat backdrop, then the chosen picture and the two first proposals standing in.
            _menu.PickMain(MainLine.StartRun);
            _menu.PictureOverride = null;
            yield return null;
            yield return Picture("character-flat", 1920, 1080);
            _menu.PictureOverride = a2;
            yield return Picture("character-a2", 1920, 1080);
            yield return Picture("character-a2-720", 1280, 720);
            _menu.PictureOverride = a;
            yield return Picture("character-a", 1920, 1080);
            _menu.PictureOverride = b;
            yield return Picture("character-b", 1920, 1080);
            _menu.PictureOverride = a2;
            _menu.ChooseSlot(2); // a dark slot refuses, and says so
            yield return Picture("character-a2-refused", 1920, 1080);

            // The loadout with the trials on, then UPGRADES and STATS.
            _menu.ChooseSlot(0);
            yield return null;
            yield return null;
            yield return Picture("loadout-on", 1920, 1080);
            yield return Picture("loadout-on-720", 1280, 720);
            _menu.OpenTab(1);
            yield return null;
            yield return null;
            yield return Picture("upgrades", 1920, 1080);
            _menu.OpenTab(2);
            yield return null;
            yield return Picture("stats", 1920, 1080);

            // The loadout with the trials off.
            _menu.Close();
            yield return null;
            Stage(false);
            Assume.That(_menu.Open(MenuScreen.Loadout), Is.True);
            yield return null;
            yield return Picture("loadout-off", 1920, 1080);
            _menu.Close();
            _levels.MetaValues.Enabled = true; // the rack on, the parts off
            Assume.That(_menu.Open(MenuScreen.Loadout), Is.True);
            yield return null;
            yield return null;
            yield return Picture("loadout-parts-off", 1920, 1080);
        }

        IEnumerator Picture(string name, int width, int height)
        {
            yield return null;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(width, height);
                cam.targetTexture = rt;
                if (_bar != null) _bar.Tick();
                _hud.Tick();
                _menu.Tick(null);
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                Save(rt, Path.Combine(_folder, name + ".png"));
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} screen {_menu.Screen} picture {(_menu.Picture != null ? _menu.Picture.name : "none (flat backdrop)")} fit {_hud.MenuFit:0.###} text outside: [{_menu.TextOutside()}] drawn: {drew}");
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

        static void Save(RenderTexture rt, string path)
        {
            RenderTexture was = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            RenderTexture.active = was;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
