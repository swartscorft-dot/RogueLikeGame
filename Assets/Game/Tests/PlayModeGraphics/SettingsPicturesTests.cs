using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    /// Work order 128: pictures of the settings screen and the short Esc menu, taken without a window as work order 114's
    /// <c>StartMenuPicturesTests</c> takes the start menu's (the camera into a texture at 1920 by 1080, every OnGUI called by hand,
    /// <see cref="ImguiByHand"/>), to compare with the start menu's concept; the picture pair for "High is today's picture" (the
    /// floor 5 fight drawn with the switch off and then on with the defaults, in the same frame, and the pixels counted); and the
    /// cost of Low, Medium and High in the benchmark's floor 5 fight (the game's camera drawn into a 1920 by 1080 texture and read
    /// back, a rough GPU time in the Editor without a window, not the built player's). Runs ONLY when ROGUELIKE_CAPTURE names a
    /// folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\128'
    ///   run-tests.ps1 PlayMode -Filter "SettingsPicturesTests"
    /// </summary>
    public class SettingsPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const ulong RunSeed = 0x0128128UL;
        const int W = 1920, H = 1080;
        /// <summary>The renders' own noise between two draws of one frame, as measured (see the pair's comment): pixels, and the largest step of 255.</summary>
        const int NoisePixels = 100, NoiseStep = 8;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SettingsScreen _screen;
        SkillBar _bar;
        string _folder, _saves;
        Dictionary<string, int?> _prefs;
        readonly List<string> _log = new List<string>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work orders 068, 114 and 128)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _prefs = SettingsPrefs.Save();
            SettingsScreenValues.On = true;
            AntiAliasingValues.On = true;
            GraphicsChoices.Unload();
            _saves = Path.Combine(Path.GetTempPath(), "SettingsPicturesTests-" + Guid.NewGuid().ToString("N"));
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
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _menu.ShowQuit = true; // the built player's main menu
            _screen = SettingsScreen.Ensure();
            GraphicsChoices.Load(); // batch mode: the defaults, never his saved values
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            ReleaseTargets();
            SettingsScreenValues.On = true;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_screen != null) Object.Destroy(_screen.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_player != null) _player.GetComponent<PlayerHealth>().Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            GraphicsChoices.Unload();
            GameSettings.Unload();
            AntiAliasingValues.On = true;
            SettingsPrefs.Restore(_prefs);
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The screens ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator A_TheShortEscMenu_AndEveryPage_FromTheEscMenu()
        {
            Assume.That(_levels.StartRun(RunSeed), Is.Not.Null);
            for (int i = 0; i < 5; i++) yield return null;
            Assume.That(_hud.Escape(), Is.True);
            yield return null;
            yield return Picture("esc-short", "the Esc menu's short list in a run");
            Assume.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.True);
            yield return Picture("esc-list", "the settings screen's list of pages, from the Esc menu");
            foreach (SettingsPage page in SettingsPages.All)
            {
                _screen.OpenPage(page);
                yield return Picture("esc-" + page.ToString().ToLowerInvariant(), page + " from the Esc menu");
            }
            // A greyed line: V-Sync on, the frame-rate cap greyed, the keys on it.
            _screen.OpenPage(SettingsPage.Graphics);
            if (!GraphicsChoices.VSyncOn) GraphicsChoices.Change(SettingLine.VSync, 1, keep: false);
            _screen.OpenPage(SettingsPage.Graphics);
            _screen.Select(_screen.Lines.ToList().IndexOf(SettingLine.FrameCap));
            yield return Picture("greyed", "V-Sync on: the frame-rate cap greyed (selected)");
            yield return Picture("greyed-720", "the same at 1280 by 720", 1280, 720);
            GraphicsChoices.Change(SettingLine.VSync, 1, keep: false);
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator B_TheStartMenuWithSettings_AndEveryPage_FromTheStartMenu()
        {
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            yield return Picture("start-main", "the start menu's main list with SETTINGS (START RUN chosen)");
            Assume.That(_menu.PickMain(MainLine.Settings), Is.True);
            yield return Picture("start-list", "the settings screen's list of pages, from the start menu");
            foreach (SettingsPage page in SettingsPages.All)
            {
                _screen.OpenPage(page);
                yield return Picture("start-" + page.ToString().ToLowerInvariant(), page + " from the start menu");
            }
            _screen.OpenPage(SettingsPage.Graphics);
            yield return Picture("start-graphics-720", "Graphics from the start menu at 1280 by 720", 1280, 720);
            _screen.Escape();
            _screen.Escape();
            yield return null;
            yield return Picture("start-main-settings", "back on the main list, SETTINGS chosen");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator C_TheFrameRateNumber_InARun()
        {
            Assume.That(_levels.StartRun(RunSeed), Is.Not.Null);
            GraphicsChoices.Change(SettingLine.ShowFrameRate, 1, keep: false);
            float until = Time.realtimeSinceStartup + 1.5f;
            while (Time.realtimeSinceStartup < until) yield return null;
            yield return Picture("framerate", "Show frame rate on: the number at the top left (without a window the frames are very short)");
        }

        // ---------------- The menus' help (the follow-up to work order 128) ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator E_TheWayIntoARun_WithTheHelp()
        {
            MenuHelpValues.On = true;
            Assume.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            _menu.PickMain(MainLine.StartRun);
            yield return Picture("help-character", "the character screen: her slot CHOSEN, the line of keys saying what goes on");
            _menu.ChooseSlot(3);
            yield return Picture("help-character-refused", "a dark slot chosen: the plain line on its plate, the cursor back on her");
            yield return Picture("help-character-refused-720", "the same at 1280 by 720", 1280, 720);
            _menu.ChooseSlot(0);
            yield return null;
            yield return Picture("help-loadout", "the loadout (the trials off): the line of keys saying first what starts the run");
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator F_WhatSheCannotAfford_BeforeAndAtTheTry()
        {
            MenuHelpValues.On = true;
            MetaValues v = _levels.MetaValues;
            var s = new MetaState { Currency = 45, EarnedInAll = 120, Runs = 3 };
            s.Owned.Add(v.Pieces.First(p => p.Name == "Steady Start").Id);
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            v.Enabled = true;
            v.Attachments = true;
            Assume.That(_menu.Open(MenuScreen.Upgrades), Is.True);
            yield return null;
            yield return null;
            yield return Picture("help-upgrades-before", "UPGRADES with 45 Echoes: what she cannot afford drawn dimmer, before she tries");
            int item = -1;
            for (int i = 0; i < _menu.ItemCount && item < 0; i++) if (_hud.MenuItemUnaffordable(_menu, i)) item = i;
            Assume.That(item, Is.GreaterThanOrEqualTo(0));
            _menu.UseItem(item);
            yield return Picture("help-upgrades-refused", "at the try (one frame in): its price and the Echoes plate red, the line saying how many more");
            _menu.Close();
            yield return null;
            v.Enabled = false;
            v.Attachments = false;
        }

        // ---------------- High is today's picture, and the cost ----------------

        [UnityTest, Timeout(1800000)]
        public IEnumerator D_HighIsTodaysPicture_AndLowAgainstHighInTheFloor5Fight()
        {
            BenchmarkStage stage = BenchmarkStage.Begin(BenchmarkScenario.Floor5Combat);
            Assert.That(stage, Is.Not.Null);
            float until = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                stage.Keep();
            }
            Camera cam = Camera.main;
            int enemies = stage.Fighting.Count(e => e != null && !e.IsDead);

            // The pair, in one frame: nothing of this order (the switch off), then the switch on with the defaults, twice over, so
            // that what two draws of the same frame differ by anyway (the renders' own noise) is measured beside it; then Low.
            SettingsScreenValues.On = false;
            Color32[] before = Draw(cam);
            SettingsScreenValues.On = true;
            GraphicsChoices.Unload();
            GraphicsChoices.Load();
            Color32[] after = Draw(cam);
            SettingsScreenValues.On = false;
            Color32[] before2 = Draw(cam);
            SettingsScreenValues.On = true;
            Color32[] after2 = Draw(cam);
            GraphicsChoices.Change(SettingLine.Quality, -1, keep: false);
            GraphicsChoices.Change(SettingLine.Quality, -1, keep: false);
            Color32[] low = Draw(cam);
            GraphicsChoices.Change(SettingLine.Quality, 1, keep: false);
            GraphicsChoices.Change(SettingLine.Quality, 1, keep: false);
            Save(before, "high-before");
            Save(after, "high-after");
            Save(low, "low-for-comparison");
            SaveDiffer(before, after, "high-differ");
            int differ = Differ(before, after), differ2 = Differ(before2, after2), noiseOff = Differ(before, before2), noiseOn = Differ(after, after2), differLow = Differ(before, low);
            _log.Add(string.Format(Inv, "{0:HH:mm:ss} HIGH IS TODAY'S: the floor 5 fight ({1} enemies), {2} by {3}: before (the switch off) and after (on, the defaults) differ in {4} pixels of {5} (again: {6}; largest step {7}); two draws with the switch off differ in {8}, two with it on in {9} (the renders' own noise; where they differ: high-differ.png, red); Low differs from before in {10}",
                                   DateTime.Now, enemies, W, H, differ, before.Length, differ2, Largest(before, after), noiseOff, noiseOn, differLow));
            // Two draws of the very same frame differ by themselves in a handful of pixels by a few steps (4 to 48 pixels of two
            // million, up to 5 of 255, in the runs of 2026-11-25): "no difference" is a difference of that size and kind (a visible
            // one, Low's, is a million pixels).
            int noise = Math.Max(noiseOff, noiseOn);
            Assert.That(differ, Is.LessThanOrEqualTo(Math.Max(2 * noise, NoisePixels)), "High with the defaults is today's picture: no more pixels differ than two draws of the same frame differ by");
            Assert.That(Largest(before, after), Is.LessThanOrEqualTo(NoiseStep), "and by no more than the renders' own noise in any pixel");
            Assert.That(differLow, Is.GreaterThan(100000), "the comparison sees a change when there is one");

            // The cost: High, Medium, Low, each in a block of draws (after a few to settle its textures), the blocks three times
            // over; the camera's draw and its read back.
            const int Blocks = 3, Settle = 5, Draws = 20;
            var times = new Dictionary<QualityPreset, List<double>> { { QualityPreset.High, new List<double>() }, { QualityPreset.Medium, new List<double>() }, { QualityPreset.Low, new List<double>() } };
            for (int block = 0; block < Blocks; block++)
                foreach (QualityPreset p in times.Keys.ToList())
                {
                    SetQuality(p);
                    for (int i = 0; i < Settle; i++) Draw(cam, readAll: false);
                    for (int i = 0; i < Draws; i++)
                    {
                        var watch = Stopwatch.StartNew();
                        Draw(cam, readAll: false);
                        watch.Stop();
                        times[p].Add(watch.Elapsed.TotalMilliseconds);
                    }
                    yield return null;
                    stage.Keep();
                }
            SetQuality(QualityPreset.High);
            ReleaseTargets();
            foreach (KeyValuePair<QualityPreset, List<double>> t in times)
            {
                List<double> s = t.Value.OrderBy(x => x).ToList();
                _log.Add(string.Format(Inv, "COST {0}: the floor 5 fight's frame drawn into {1} by {2} and read back, {3} draws in blocks: median {4:0.00} ms, fastest {5:0.00}, slowest {6:0.00} (Editor, batch mode, no window)",
                                       t.Key, W, H, s.Count, s[s.Count / 2], s[0], s[s.Count - 1]));
            }
            stage.End();
            _levels.BackToPracticeRoom();
        }

        void SetQuality(QualityPreset p)
        {
            for (int i = 0; i < 3 && GraphicsChoices.Quality != p; i++)
                GraphicsChoices.Change(SettingLine.Quality, GraphicsChoices.Quality == QualityPreset.Custom || (int)GraphicsChoices.Quality > (int)p ? -1 : 1, keep: false);
        }

        readonly Dictionary<int, RenderTexture> _targets = new Dictionary<int, RenderTexture>();
        RenderTexture _resolved;

        /// <summary>
        /// The game's camera drawn into a texture the size of the screen, multisampled as the screen would be (a camera with a
        /// target takes its samples from the target, URP's GetIntermediateTexturesMSAA), resolved and read back (all of it, or one
        /// pixel: enough to wait for the GPU). The textures are made once.
        /// </summary>
        Color32[] Draw(Camera cam, bool readAll = true)
        {
            int samples = cam.allowMSAA && AntiAliasingValues.MsaaNow ? Mathf.Max(1, UnityEngine.Rendering.Universal.UniversalRenderPipeline.asset.msaaSampleCount) : 1;
            if (!_targets.TryGetValue(samples, out RenderTexture rt)) _targets[samples] = rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32) { antiAliasing = samples };
            if (_resolved == null) _resolved = new RenderTexture(W, H, 0, RenderTextureFormat.ARGB32);
            RenderTexture before = cam.targetTexture, was = RenderTexture.active;
            var tex = new Texture2D(readAll ? W : 1, readAll ? H : 1, TextureFormat.RGBA32, false);
            try
            {
                cam.targetTexture = rt;
                cam.Render();
                Graphics.Blit(rt, _resolved);
                RenderTexture.active = _resolved;
                tex.ReadPixels(new Rect(0, 0, tex.width, tex.height), 0, 0);
                tex.Apply();
                return tex.GetPixels32();
            }
            finally
            {
                cam.targetTexture = before;
                RenderTexture.active = was;
                Object.Destroy(tex);
            }
        }

        void ReleaseTargets()
        {
            foreach (RenderTexture rt in _targets.Values)
                if (rt != null)
                {
                    rt.Release();
                    Object.Destroy(rt);
                }
            _targets.Clear();
            if (_resolved != null)
            {
                _resolved.Release();
                Object.Destroy(_resolved);
            }
            _resolved = null;
        }

        static int Largest(Color32[] a, Color32[] b)
        {
            int most = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                most = Math.Max(most, Math.Max(Math.Abs(a[i].r - b[i].r), Math.Max(Math.Abs(a[i].g - b[i].g), Math.Abs(a[i].b - b[i].b))));
            return most;
        }

        /// <summary>The first picture greyed with the pixels where the two differ in red, to see where.</summary>
        void SaveDiffer(Color32[] a, Color32[] b, string name)
        {
            var px = new Color32[a.Length];
            for (int i = 0; i < a.Length; i++)
            {
                bool d = a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b;
                byte g = (byte)((a[i].r + a[i].g + a[i].b) / 6);
                px[i] = d ? new Color32(255, 0, 0, 255) : new Color32(g, g, g, 255);
            }
            Save(px, name);
        }

        static int Differ(Color32[] a, Color32[] b)
        {
            int n = 0;
            for (int i = 0; i < a.Length && i < b.Length; i++)
                if (a[i].r != b[i].r || a[i].g != b[i].g || a[i].b != b[i].b) n++;
            return n;
        }

        void Save(Color32[] pixels, string name)
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        // ---------------- A picture of the screens ----------------

        IEnumerator Picture(string name, string what, int width = W, int height = H)
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
                _screen.Tick(null);
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                RenderTexture was = RenderTexture.active;
                RenderTexture.active = rt;
                var tex = new Texture2D(width, height, TextureFormat.RGBA32, false);
                tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                tex.Apply();
                RenderTexture.active = was;
                File.WriteAllBytes(Path.Combine(_folder, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
                string outside = SettingsScreen.Holds ? _screen.TextOutside() : StartMenu.Holds ? _menu.TextOutside() : _levels.Paused ? _hud.PauseTextOutside() : "";
                _log.Add(string.Format(Inv, "{0:HH:mm:ss} {1}: {2}x{3} {4}; text outside: [{5}]; drawn: {6}", DateTime.Now, name, width, height, what, outside, drew));
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
