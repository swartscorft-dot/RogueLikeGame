using System;
using System.Collections;
using System.Collections.Generic;
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
    /// Work order 133: pictures of the practice room's task plates, taken without a window as work orders 068 and 114 take the
    /// interface's (the camera into a texture, every OnGUI called by hand, <see cref="ImguiByHand"/>), with the HUD and the skill strip
    /// drawn as in play, to be looked at for a plate over her or the strip, text cut off, a key shown wrong or a foreign style. Every
    /// task's plate, a ticked one, the last plate and the small line that starts them again, at 1920 by 1080 (one at 1280 by 720, one
    /// with the developer's text on). Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\133'
    ///   run-tests.ps1 PlayMode -Filter "PracticeTasksPicturesTests"
    ///
    /// The flag is the test's own (in memory): his PlayerPrefs are not read or written.
    /// </summary>
    public class PracticeTasksPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SkillBar _bar;
        PracticeTasks _tasks;
        string _folder, _saves;
        bool? _devBefore;
        readonly List<string> _log = new List<string>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work orders 068, 114 and 133)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _saves = Path.Combine(Path.GetTempPath(), "PracticeTasksPicturesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            _devBefore = DeveloperKeys.ForceForTests(false); // the friends' build: no developer's text
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _tasks = PracticeTasks.Ensure();
            _tasks.UsesSavedFlag = false;
            _tasks.DoneBefore = true; // begun by the test, not by itself
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures-log.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null)
            {
                Camera.main.targetTexture = null;
                Camera.main.ResetAspect();
            }
            if (_tasks != null) Object.Destroy(_tasks.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null) _levels.BackToPracticeRoom();
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            DeveloperKeys.ForceForTests(_devBefore);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator ThePlates()
        {
            // The small line in the room, before they run.
            yield return Picture("00-again", 1920, 1080);
            Assume.That(_tasks.Begin(), Is.True);
            yield return null;
            int n = 1;
            while (_tasks.Run != null && n < 30)
            {
                PracticeTask t = _tasks.Run.Current;
                string name = n.ToString("00") + "-" + t.Kind + (t.Slot >= 0 ? "-" + _player.Skills[t.Slot].Name.Replace(" ", "") : "");
                if (t.Kind == PracticeTaskKind.Duck)
                {
                    float end = Time.realtimeSinceStartup + 3f;
                    while (_tasks.PracticeProp == null && Time.realtimeSinceStartup < end) yield return null;
                    yield return null;
                    if (_tasks.PracticeProp != null)
                    {
                        Transform prop = _tasks.PracticeProp.transform;
                        _player.Teleport(prop.position - prop.forward * 1.3f); // in reach: the orange edge lit
                        for (int i = 0; i < 5; i++) yield return null;
                    }
                }
                yield return Picture(name, 1920, 1080);
                if (n == 1)
                {
                    yield return Picture(name + "-720", 1280, 720);
                    // A ticked plate: the first task done as the game would tell it.
                    PracticeEvent walked = PracticeEvent.Of(PracticeEventKind.Walked);
                    walked.Metres = 10f;
                    _tasks.Run.Note(walked, Time.time);
                    yield return Picture(name + "-ticked", 1920, 1080);
                    DeveloperKeys.ForceForTests(true);
                    yield return Picture(name + "-ticked-devtext", 1920, 1080);
                    DeveloperKeys.ForceForTests(false);
                    float end = Time.realtimeSinceStartup + 3f;
                    while (_tasks.Run != null && _tasks.Run.Index == 0 && Time.realtimeSinceStartup < end) yield return null;
                }
                else _tasks.Skip();
                yield return null;
                n++;
            }
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
                cam.aspect = width / (float)height;
                if (_bar != null) _bar.Tick();
                _hud.Tick();
                _tasks.LayOutForTests();
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                Save(rt, Path.Combine(_folder, name + ".png"));
                PracticeTask t = _tasks.Run != null ? _tasks.Run.Current : null;
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} task {(t != null ? t.Kind + " \"" + t.Words + "\" keys [" + string.Join(" ", t.Keys) + "]" : "none")} plate {_tasks.PlateRect} again {_tasks.AgainShown} text outside: [{(_tasks.Run != null || _tasks.LastRun != null ? _tasks.TextOutside() : "")}] drawn: {drew}");
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
