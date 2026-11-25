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
    /// Work order 133's follow-up: pictures of the first run's teaching room (floor 1's entrance of a fixed seed, its statues, its
    /// doors held), taken without a window as <see cref="PracticeTasksPicturesTests"/> takes the practice room's, with the HUD, the run
    /// box and the skill strip drawn as in play: the first plate with the doors shut, the shooting plate, the duck's plate with its
    /// low prop, and the last plate with the door open. Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project:
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\133'
    ///   run-tests.ps1 PlayMode -Filter "PracticeTasksFirstRunPicturesTests"
    ///
    /// The flag is the test's own (in memory): his PlayerPrefs are not read or written.
    /// </summary>
    public class PracticeTasksFirstRunPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const ulong Seed = 0x5EED1330UL;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        SkillBar _bar;
        PracticeTasks _tasks;
        string _folder, _saves;
        bool? _devBefore;
        readonly List<string> _log = new List<string>();

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(_folder)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 133)");
            Directory.CreateDirectory(_folder);
            _saves = Path.Combine(Path.GetTempPath(), "PracticeTasksFirstRunPictures-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            _devBefore = DeveloperKeys.ForceForTests(false);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _tasks = PracticeTasks.Ensure();
            _tasks.UsesSavedFlag = false;
            _tasks.DoneBefore = false;
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

        static IEnumerator Seconds(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        void SkipTo(PracticeTaskKind kind)
        {
            for (int i = 0; i < 40 && _tasks.Run != null && _tasks.Run.Current.Kind != kind; i++) _tasks.Skip();
        }

        [UnityTest, Timeout(900000)]
        public IEnumerator TheTeachingRoom()
        {
            Assume.That(_levels.StartRun(Seed), Is.Not.Null);
            yield return Seconds(1f); // the blocks risen
            Assume.That(_tasks.Teaching, Is.True);
            yield return Picture("run-01-Move-doors-shut", 1920, 1080);
            // Her by a door, so the shut block is in view.
            if (_tasks.TeachingDoors.Count > 0)
            {
                Vector3 d = _tasks.TeachingDoors[0].transform.position, her = _player.Position;
                Vector3 back = new Vector3(her.x - d.x, 0f, her.z - d.z).normalized;
                _player.Teleport(d + back * 4f);
                yield return Seconds(0.3f);
                yield return Picture("run-01-Move-by-the-shut-door", 1920, 1080);
            }
            SkipTo(PracticeTaskKind.Shoot);
            _player.Teleport(_levels.Level.PlayerStart);
            yield return Seconds(0.3f);
            yield return Picture("run-02-Shoot", 1920, 1080);
            SkipTo(PracticeTaskKind.SpendMarks);
            yield return Picture("run-10-SpendMarks", 1920, 1080);
            SkipTo(PracticeTaskKind.Duck);
            float end = Time.realtimeSinceStartup + 3f;
            while (_tasks.PracticeProp == null && Time.realtimeSinceStartup < end) yield return null;
            if (_tasks.PracticeProp != null)
            {
                Transform prop = _tasks.PracticeProp.transform;
                _player.Teleport(prop.position - prop.forward * 1.3f);
                yield return Seconds(0.3f);
            }
            yield return Picture("run-11-Duck", 1920, 1080);
            SkipTo(PracticeTaskKind.GoOn);
            if (_tasks.TeachingDoors.Count > 0)
            {
                Vector3 d = _tasks.TeachingDoors[0].transform.position, her = _player.Position;
                Vector3 back = new Vector3(her.x - d.x, 0f, her.z - d.z).normalized;
                _player.Teleport(d + back * 4f);
            }
            yield return Seconds(1.2f); // the blocks sunk
            yield return Picture("run-13-GoOn-door-open", 1920, 1080);
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
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} room {(_levels.CurrentRoom != null ? _levels.CurrentRoom.Kind + " " + _levels.CurrentRoom.Order : "none")} task {(t != null ? t.Kind + " \"" + t.Words + "\"" : "none")} plate {_tasks.PlateRect} targets {_tasks.Targets.Count} doors {_tasks.TeachingDoors.Count} open {_tasks.TeachingDoorOpen} clock {_levels.RunClockSeconds:0.00} text outside: [{_tasks.TextOutside()}] drawn: {drew}");
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
