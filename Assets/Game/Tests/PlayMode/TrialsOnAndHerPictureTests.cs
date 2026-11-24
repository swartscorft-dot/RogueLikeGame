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
    /// Work order 117 (2026-11-24), on the day sprint's merged build. Kristian ticked "Ja, på som standard" for the rack, the ranks
    /// and the weapon parts ("Echoes, stativet, rangene og våpendelene blir en vanlig del av spillet. Bryterne i pausemenyen blir
    /// stående, så du kan slå dem av."), and chose "A2: plakaten" of the pictures of her for the character selection, enlarged to
    /// 3840 by 2160 ("Ja til installering" of an upscaler). Here: with a fresh save and the values as in code, the start menu's
    /// loadout and upgrades are open and a run earns Echoes and shows them; the pause menu's two lines still turn both off;
    /// her picture is in the project at its full size, drawn covering the screen unstretched, her slot's portrait on her face;
    /// and the loadout and the upgrades hold every one of the nine parts (work order 110) and eleven pieces (work order 106)
    /// inside the screen at 1920 by 1080 and 1280 by 720. Every save and record is a temporary file's, never his.
    ///
    /// Pictures of the character selection, the loadout and the upgrades are taken only when ROGUELIKE_CAPTURE names a folder
    /// (as <see cref="StartMenuPicturesTests"/> takes them):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\117'
    /// </summary>
    public class TrialsOnAndHerPictureTests
    {
        const int MostFrames = 600;
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        /// <summary>Her picture's file in the project, as the start menu loads it by name (work order 114).</summary>
        const string PicturePath = "Assets/Game/Resources/UI/Menu/Character_Gunslinger.png";
        const int PictureWidth = 3840, PictureHeight = 2160;
        /// <summary>
        /// Her face in the picture he chose, as shares of its width and of its height from the top, looked at by the producer's
        /// agent in the 1672-wide original and the 4K file (work order 117): her forehead under the hat's brim to her chin, her
        /// left cheek to the hair over her right eye.
        /// </summary>
        const float FaceLeft = 0.72f, FaceRight = 0.80f, FaceTop = 0.12f, FaceBottom = 0.27f;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
        static readonly string[] Keys = { BetweenRunsSwitch.Key, AttachmentsSwitch.Key };

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        string _saves;
        readonly Dictionary<string, int?> _prefsBefore = new Dictionary<string, int?>();

        string SavePath => Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _prefsBefore.Clear();
            foreach (string k in Keys) _prefsBefore[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            BetweenRunsSwitch.Unload(); // the values as in code, never a stored choice of this machine
            AttachmentsSwitch.Unload();
            _saves = Path.Combine(Path.GetTempPath(), "TrialsOnAndHerPictureTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = SavePath; // a fresh save: no file
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure(); // its values as in code: the trials on
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            InterfaceStyle.ScreenForPictures = Sizes[0];
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            foreach (KeyValuePair<string, int?> p in _prefsBefore)
                if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value);
            PlayerPrefs.Save();
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        IEnumerator OpenOn(MenuScreen screen)
        {
            Assert.That(_menu.Open(screen), Is.True, "opened on " + screen);
            yield return null;
            yield return null;
        }

        /// <summary>A save that owns every piece, every part and has Echoes and every feat (a budget of 8): every card on both screens.</summary>
        void OwnEverything()
        {
            MetaValues v = _levels.MetaValues;
            var s = new MetaState { Currency = 4321, EarnedInAll = 9000, Runs = 30 };
            foreach (MetaPiece p in v.Pieces) s.Owned.Add(p.Id);
            foreach (WeaponAttachment p in WeaponRack.InSlotOrder(v)) s.AttachmentsOwned.Add(p.Id);
            foreach (MetaFeat f in MetaFeats.All) s.Feats.Add(MetaFeats.Id(f));
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);
        }

        // ---------------- Part 1: the trials on by default ----------------

        [UnityTest]
        public IEnumerator TheValuesAsInCode_AFreshSave_TheMenusLoadoutAndUpgradesAreOpen_ARunEarnsEchoes_AndTheyAreShown()
        {
            Assert.That(new MetaValues().Enabled, Is.True, "the rack and the ranks on in code");
            Assert.That(new MetaValues().Attachments, Is.True, "her pistols' parts on in code");
            Assert.That(_levels.MetaOn && _levels.AttachmentsOn, Is.True, "both on, no line stored");
            Assert.That(File.Exists(SavePath), Is.False, "a fresh save: no file");

            yield return OpenOn(MenuScreen.Upgrades);
            Assert.That(_hud.MenuLockedLines, Is.Empty, "UPGRADES is open: no locked line");
            int parts = WeaponRack.InSlotOrder(_levels.MetaValues).Count;
            Assert.That(_hud.MenuCards.Count, Is.EqualTo(_levels.MetaValues.Ranks.Length + _levels.MetaValues.Pieces.Length + parts),
                        "every rank, piece and part to buy");
            yield return OpenOn(MenuScreen.Character);
            Assert.That(_menu.ChooseSlot(StartMenuFlow.HerSlot), Is.True);
            yield return null;
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            Assert.That(_hud.MenuLockedLines, Is.Empty, "the loadout is open: no locked line");

            Assert.That(_menu.StartTheRun(), Is.Not.Null, "START begins the run");
            yield return null;
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.Earnings, Is.Not.Null, "a run with the trial on earns");
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Earnings != null && _levels.Earnings.FloorsCleared > 0);
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.GreaterThanOrEqualTo(_levels.MetaValues.PerFloor), "floor 1 cleared pays");

            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
            int earned = _levels.Earnings.Total(_levels.MetaValues);
            StringAssert.Contains("Echoes  +" + earned + " this run", _hud.EndScreenText, "the end screen shows them");
            Assert.That(File.Exists(SavePath), Is.True, "the save is written at the run's end, to the test's file");
            Assert.That(MetaSaveFile.Load(SavePath, new MetaValues(), out _, out _).Currency, Is.EqualTo(earned), "kept");
            Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(earned), "the menu's Echoes plate reads this");
        }

        [UnityTest]
        public IEnumerator BothPauseMenuLinesTurnedOff_TheMenusPartsAreLockedLines_AndARunEarnsNothing()
        {
            Assert.That(_levels.MetaOn && _levels.AttachmentsOn, Is.True);
            Assert.That(_hud.Escape(), Is.True, "the pause menu, in the practice room");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Attachments), Is.True, "the attachments' line: off");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.BetweenRuns), Is.True, "the trial's line: off");
            Assert.That(_hud.MenuText(RunHud.MenuLine.BetweenRuns), Is.EqualTo("Between runs (trial):  off"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Attachments), Is.EqualTo("Weapon attachments (trial):  off"));
            Assert.That(_levels.MetaOn || _levels.AttachmentsOn, Is.False);
            Assert.That(_levels.MetaValues.Enabled && _levels.MetaValues.Attachments, Is.True, "the values are not written; the lines override them");
            Assert.That(_hud.Escape(), Is.True, "the pause menu closes");
            yield return null;

            yield return OpenOn(MenuScreen.Upgrades);
            Assert.That(_hud.MenuLockedLines, Is.EqualTo(new[] { RunHud.LockedTrial1, RunHud.LockedTrial2 }), "UPGRADES: a quiet locked line");
            Assert.That(_hud.MenuCards, Is.Empty);
            _menu.Close();
            yield return null;
            Assert.That(_levels.StartRun(0x12345678UL), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Earnings, Is.Null, "nothing earned");
            Assert.That(_levels.Modifiers.Meta.Any || _levels.Modifiers.Weapon.Any, Is.False);
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(File.Exists(SavePath), Is.False, "nothing written");

            Assert.That(_hud.Escape(), Is.True, "the pause menu again");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Attachments), Is.True, "the attachments' line on again turns both on");
            Assert.That(_levels.MetaOn && _levels.AttachmentsOn, Is.True);
            _hud.Escape();
        }

        // ---------------- Part 2: her picture ----------------

        [UnityTest]
        public IEnumerator HerPicture_IsInTheProjectAt3840By2160_ImportedWithNothingShrunkOrBlurred()
        {
            var picture = Resources.Load<Texture2D>(InterfaceStyle.V.Menus.Start.PictureName);
            Assert.That(picture, Is.Not.Null, "loaded by the name work order 114's menu asks for");
            Assert.That(picture.width, Is.EqualTo(PictureWidth));
            Assert.That(picture.height, Is.EqualTo(PictureHeight));
            Assert.That(picture.mipmapCount, Is.EqualTo(1), "no mip maps");
            Assert.That(picture.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(picture.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(picture.isReadable, Is.False, "Read/Write off");
            long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(picture);
            Debug.Log("Her picture: " + picture.width + " by " + picture.height + ", format " + picture.format + ", " + picture.mipmapCount
                      + " mip level(s), graphics format " + picture.graphicsFormat + ", in memory " + bytes + " bytes (" + (bytes / 1048576.0).ToString("0.0") + " MiB)");
#if UNITY_EDITOR
            var importer = UnityEditor.AssetImporter.GetAtPath(PicturePath) as UnityEditor.TextureImporter;
            Assert.That(importer, Is.Not.Null, PicturePath);
            Assert.That(importer.npotScale, Is.EqualTo(UnityEditor.TextureImporterNPOTScale.None), "Non-Power of 2: None, never rescaled");
            Assert.That(importer.maxTextureSize, Is.EqualTo(4096));
            Assert.That(importer.mipmapEnabled, Is.False);
            Assert.That(importer.wrapMode, Is.EqualTo(TextureWrapMode.Clamp));
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Bilinear));
            Assert.That(importer.sRGBTexture, Is.True);
            Assert.That(importer.isReadable, Is.False);
            Assert.That(importer.textureCompression, Is.EqualTo(UnityEditor.TextureImporterCompression.CompressedHQ));
            Assert.That(importer.textureType, Is.EqualTo(UnityEditor.TextureImporterType.Default));
            var source = new FileInfo(Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", PicturePath));
            Assert.That(source.Exists && source.Length > 1000000, Is.True, "the picture's own file, not an LFS pointer");
            Debug.Log("Her picture's file: " + source.Length + " bytes; importer: npot " + importer.npotScale + ", max " + importer.maxTextureSize
                      + ", mips " + importer.mipmapEnabled + ", " + importer.wrapMode + ", " + importer.filterMode + ", sRGB " + importer.sRGBTexture
                      + ", readable " + importer.isReadable + ", compression " + importer.textureCompression);
#endif
            yield return null;
        }

        [UnityTest]
        public IEnumerator TheCharacterSelection_DrawsHerCoveringTheScreen_OnTheRightHalf_Unstretched_AndHerSlotShowsHerFace_AtBothSizes()
        {
            yield return OpenOn(MenuScreen.Character);
            Texture2D picture = _menu.Picture;
            Assert.That(picture, Is.Not.Null, "her picture, from Resources");
            Assert.That(_menu.UsesFlatBackdrop, Is.False);
            StartMenuValues s = InterfaceStyle.V.Menus.Start;
            float pictureAspect = (float)picture.width / picture.height;
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                float viewW = InterfaceStyle.ScreenWidth / InterfaceStyle.Scale, viewH = InterfaceStyle.V.LayoutHeight;
                // The backdrop's own call (StartMenuGui.DrawBackdrop): the part of the picture drawn over the whole view.
                UvRect uv = CoverCrop.Of(picture.width, picture.height, viewW, viewH, s.PictureAnchorX, s.PictureAnchorY);
                string at = size.x + " by " + size.y;
                Assert.That(uv.X, Is.GreaterThanOrEqualTo(-1e-5f), at);
                Assert.That(uv.Y, Is.GreaterThanOrEqualTo(-1e-5f), at);
                Assert.That(uv.X + uv.W, Is.LessThanOrEqualTo(1f + 1e-5f), at);
                Assert.That(uv.Y + uv.H, Is.LessThanOrEqualTo(1f + 1e-5f), at);
                float drawnAspect = uv.W * picture.width / (uv.H * picture.height);
                Assert.That(drawnAspect, Is.EqualTo(viewW / viewH).Within(1e-3f), at + ": what is drawn has the screen's shape: it covers it");
                Assert.That(viewW / viewH, Is.EqualTo(pictureAspect).Within(2e-3f), at + ": a 16:9 screen shows the whole 16:9 picture, nothing cut or stretched");
                float faceOnScreen = ((FaceLeft + FaceRight) * 0.5f - uv.X) / uv.W;
                Assert.That(faceOnScreen, Is.GreaterThan(0.5f).And.LessThan(1f), at + ": her on the right half");

                // Her slot's portrait (StartMenuGui.DrawCharacter): a crop about her face of the slot's shape.
                Rect slot = _menu.HitRect(StartMenu.HitKind.Slot, StartMenuFlow.HerSlot);
                Assert.That(slot.width, Is.GreaterThan(0f), at);
                UvRect p = CoverCrop.Portrait(picture.width, picture.height, slot.width, slot.height, s.PortraitX, s.PortraitY, s.PortraitShare);
                Assert.That(p.W * picture.width / (p.H * picture.height), Is.EqualTo(slot.width / slot.height).Within(1e-3f), at + ": the portrait unstretched");
                float left = p.X, right = p.X + p.W, top = 1f - (p.Y + p.H), bottom = 1f - p.Y; // from the top, as the face's shares
                Assert.That(left <= FaceLeft && right >= FaceRight && top <= FaceTop && bottom >= FaceBottom, Is.True,
                            at + ": her face inside her slot's portrait (crop " + left.ToString("0.000") + ".." + right.ToString("0.000") + " by " + top.ToString("0.000") + ".." + bottom.ToString("0.000") + ")");
                Assert.That(_menu.TextOutside(), Is.Empty, at);
            }
        }

        // ---------------- The producer's check: nine parts and eleven pieces on the menu ----------------

        [UnityTest]
        public IEnumerator TheNinePartsAndElevenPieces_AllSitInsideTheScreen_OnTheLoadoutAndTheUpgrades_AtBothSizes()
        {
            OwnEverything();
            MetaValues v = _levels.MetaValues;
            int parts = WeaponRack.InSlotOrder(v).Count;
            Assert.That(parts, Is.EqualTo(9), "work order 110: three parts a slot");
            Assert.That(v.Pieces.Length, Is.EqualTo(11), "work order 106: Knight's Resolve at the rack's end");
            foreach (MenuScreen screen in new[] { MenuScreen.Upgrades, MenuScreen.Loadout })
            {
                yield return OpenOn(screen);
                int cards = screen == MenuScreen.Loadout ? v.Pieces.Length + parts : v.Ranks.Length + v.Pieces.Length + parts;
                Assert.That(_hud.MenuCards.Count, Is.EqualTo(cards), screen + ": every card");
                foreach (Vector2Int size in Sizes)
                {
                    InterfaceStyle.ScreenForPictures = size;
                    _menu.LayOutForTests();
                    float viewW = InterfaceStyle.ScreenWidth / InterfaceStyle.Scale, viewH = InterfaceStyle.V.LayoutHeight;
                    string at = screen + " at " + size.x + " by " + size.y + " (fit " + _hud.MenuFit.ToString("0.###") + ")";
                    Assert.That(_hud.MenuItemRects.Count, Is.EqualTo(cards + (screen == MenuScreen.Loadout ? 1 : 0)), at + ": the cards, and START");
                    for (int i = 0; i < _hud.MenuItemRects.Count; i++)
                    {
                        Rect r = _hud.MenuItemRects[i];
                        Assert.That(r.xMin >= 0f && r.yMin >= 0f && r.xMax <= viewW && r.yMax <= viewH, Is.True, at + ": item " + i + " " + r + " inside the " + viewW.ToString("0") + " by " + viewH.ToString("0") + " view");
                    }
                    for (int i = 0; i < _hud.MenuItemRects.Count; i++)
                        for (int j = i + 1; j < _hud.MenuItemRects.Count; j++)
                            Assert.That(_hud.MenuItemRects[i].Overlaps(_hud.MenuItemRects[j]), Is.False, at + ": items " + i + " and " + j + " apart");
                    Assert.That(_menu.TextOutside(), Is.Empty, at + ": every word inside its shape");
                    Debug.Log("Work order 117: " + at + ", " + _hud.MenuItemRects.Count + " items");
                }
                InterfaceStyle.ScreenForPictures = Sizes[0];
            }
        }

        // ---------------- Pictures (only with ROGUELIKE_CAPTURE) ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator Pictures_TheCharacterSelection_TheLoadout_AndTheUpgrades_WithEverythingOn()
        {
            string folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 117)");
            Directory.CreateDirectory(folder);
            var log = new List<string>();
            SkillBar bar = Object.FindAnyObjectByType<SkillBar>();
            _menu.ShowQuit = true;
            OwnEverything();
            MetaState s = MetaSaveFile.Load(SavePath, _levels.MetaValues, out _, out _);
            s.Equipped.Add(_levels.MetaValues.Pieces.First(p => p.Name == "Thick Hide").Id);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            s.Ranks[_levels.MetaValues.Ranks[0].Id] = 2;
            Assert.That(MetaSaveFile.Save(SavePath, s, out string error), Is.True, error);

            yield return OpenOn(MenuScreen.Character);
            yield return Picture(folder, "character", 1920, 1080, bar, log);
            yield return Picture(folder, "character-720", 1280, 720, bar, log);
            _menu.ChooseSlot(StartMenuFlow.HerSlot);
            yield return null;
            yield return null;
            yield return Picture(folder, "loadout", 1920, 1080, bar, log);
            yield return Picture(folder, "loadout-720", 1280, 720, bar, log);
            _menu.OpenTab(1);
            yield return null;
            yield return null;
            yield return Picture(folder, "upgrades", 1920, 1080, bar, log);
            yield return Picture(folder, "upgrades-720", 1280, 720, bar, log);
            File.AppendAllLines(Path.Combine(folder, "pictures-log.txt"), log);
        }

        IEnumerator Picture(string folder, string name, int width, int height, SkillBar bar, List<string> log)
        {
            yield return null;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(width, height);
                cam.targetTexture = rt;
                if (bar != null) bar.Tick();
                _hud.Tick();
                _menu.Tick(null);
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} screen {_menu.Screen} picture {(_menu.Picture != null ? _menu.Picture.name + " " + _menu.Picture.width + "x" + _menu.Picture.height : "none")} cards {_hud.MenuCards.Count} fit {_hud.MenuFit:0.###} text outside: [{_menu.TextOutside()}] drawn: {drew}");
            }
            catch (Exception e) { log.Add(name + " failed: " + e); }
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
