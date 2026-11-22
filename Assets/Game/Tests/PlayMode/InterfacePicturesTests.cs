using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 068: pictures of the interface, taken without a window, so it can be compared with the concept
    /// (ui_b_kinetic_anime.png) round after round. Runs ONLY when the environment variable ROGUELIKE_CAPTURE names a folder
    /// outside the project (otherwise it is skipped: no ordinary suite takes a picture or leaves a file):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\068\round1'
    ///   run-tests.ps1 PlayMode -Filter "InterfacePicturesTests"
    ///
    /// How: the scene is staged through the game's intents (a run, a fight with an elite, Triple Tap's chain, Backstep, a
    /// full gauge, an offer, the pause menu); for each moment the main camera renders into a texture of 1920 by 1080 (or
    /// 1280 by 720), the interface is told that is the screen (<see cref="InterfaceStyle.ScreenForPictures"/>), every piece
    /// works out its frame, and every OnGUI in the scene is called by hand inside an IMGUI container (the engine's own way of
    /// running IMGUI outside a window, as UI Toolkit's IMGUIContainer does: a batch run sends no OnGUI events), a layout pass
    /// then a repaint pass in the pieces' depth order, drawing into that texture; then it is written as a PNG.
    /// </summary>
    public class InterfacePicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const ulong RunSeed = 0x0E117E17UL;

        sealed class OnGuiProbe : MonoBehaviour
        {
            public int Calls;

            void OnGUI() => Calls++;
        }

        PlayerCharacter _player;
        PlayerHealth _health;
        SkillBar _bar;
        GeneratedLevels _levels;
        RunHud _hud;
        CastBar _cast;
        EnemyBars _bars;
        FloorMap _map;
        TimingFeedback _feedback;
        PlayerSigns _signs;
        string _folder;
        readonly List<string> _log = new List<string>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 068)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            _cast = CastBar.Ensure();
            _bars = EnemyBars.Ensure();
            _map = FloorMap.Ensure();
            _feedback = TimingFeedback.Ensure();
            _signs = PlayerSigns.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_folder != null && _log.Count > 0) File.AppendAllLines(Path.Combine(_folder, "pictures.txt"), _log);
            _log.Clear();
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                _levels.BackToPracticeRoom();
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _signs, _feedback, _map, _bars, _cast, _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The moments ----------------

        [UnityTest, Timeout(600000)]
        public IEnumerator TheMoments()
        {
            var probe = new GameObject("probe").AddComponent<OnGuiProbe>();
            for (int i = 0; i < 5; i++) yield return null;
            _log.Add($"{DateTime.Now:HH:mm:ss} batch {Application.isBatchMode} device {SystemInfo.graphicsDeviceType} screen {Screen.width}x{Screen.height} OnGUI calls by the engine in 5 frames: {probe.Calls}");
            Object.Destroy(probe.gameObject);

            // A fight: an elite near her with marks and its bar, a hit she took, Quick Shot cooling, Long Shot charging.
            GeneratedLevel level = _levels.StartRun(RunSeed);
            yield return null;
            Enemy elite = level.Elite;
            Assume.That(elite, Is.Not.Null, "an elite on the floor");
            Near(elite.transform.position, 4.5f);
            yield return null;
            yield return null;
            foreach (List<Enemy> room in level.EnemiesByRoom.Values)
                foreach (Enemy e in room)
                    if (e != null && !e.IsDead && Vector3.Distance(e.transform.position, elite.transform.position) < 14f) e.Body.TakeHit(6);
            elite.Body.TakeHit(14);
            elite.Body.AddMarks(2, _player.Kit.MaxMarks, 60f);
            Hurt(28);
            int quick = Slot("Quick Shot");
            _player.SpendCooldownForTests(quick, 3.2f);
            yield return Charging(elite.transform.position, 0.45f);
            yield return Picture("fight", 1920, 1080);
            yield return Picture("fight-720", 1280, 720);
            if (Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_BEFORE") == "1")
            {
                // Work order 063's look, as he saw it at 20:20: no plate, no combo bar, the strokes as they were.
                InterfaceValues v = InterfaceStyle.Shared;
                v.TopPlate = false;
                v.ComboBar = false;
                v.StrokeShrink = 1f;
                v.StrokeNumber = true;
                yield return Picture("fight-063", 1920, 1080);
                v.TopPlate = v.ComboBar = true;
                v.StrokeShrink = 2f / 3f;
                v.StrokeNumber = false;
            }
            _player.ReleaseSkill(Slot("Long Shot"));

            // Triple Tap: the first press, the second's window open: the whole chain on the combo bar.
            int triple = Slot("Triple Tap");
            _player.EquipSkill(triple);
            yield return UntilFree();
            yield return ForRealTime(0.8f); // past Long Shot's recovery
            Assume.That(_player.PressSkill(triple, elite.transform.position), Is.EqualTo(IntentResult.Started).Or.EqualTo(IntentResult.Buffered), "Triple Tap's first press");
            yield return Until(() => _player.RecastSlot == triple, 3f);
            yield return Picture("triple-1-start", 1920, 1080); // the whole chain ahead, from the first shot (work order 069's rhythm)
            yield return Until(() => _player.RecastSlot == triple && _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.06f, 3f);
            yield return Picture("triple-2", 1920, 1080);
            // The second press in its clean part, then the last window open and a press now clean: lit.
            yield return Until(() => _player.IsRecastWindowOpen && _cast.ComboLit, 1.5f);
            _player.PressSkill(triple, elite.transform.position);
            yield return Until(() => _player.IsRecastWindowOpen && _player.RecastShots == 2 && _player.RecastPressWouldBeClean, 2f);
            yield return Picture("triple-3-lit", 1920, 1080);
            yield return Until(() => !_player.IsRecastWindowOpen, 2f);
            yield return ForRealTime(0.05f);
            yield return Picture("triple-after", 1920, 1080);

            // Backstep: the hop, then its one press's window.
            int back = Slot("Backstep");
            _player.EquipSkill(back);
            yield return UntilFree();
            yield return ForRealTime(0.7f);
            Assume.That(_player.PressSkill(back, elite.transform.position), Is.EqualTo(IntentResult.Started).Or.EqualTo(IntentResult.Buffered), "the hop");
            yield return Until(() => _player.RecastSlot == back && _player.IsRecastWindowOpen && _player.RecastSinceOpened > 0.03f, 3f);
            yield return Picture("backstep", 1920, 1080);

            // A full gauge, low health and a hit just taken.
            yield return Until(() => !_player.IsRecastWindowOpen, 2f);
            _player.FillFocus();
            Hurt(40);
            yield return null;
            yield return Picture("focus-full-hurt", 1920, 1080);

            // An offer: the floor's new skill.
            yield return UntilFree();
            _levels.FinishFloorForTests();
            yield return Until(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward, 3f);
            yield return ForRealTime(0.6f); // past the guard: the cards lit
            yield return Picture("offer", 1920, 1080);

            // The pause menu over it all (a choice refuses the pause: the first card picked first).
            _levels.PickReward(0);
            yield return ForRealTime(0.5f);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice)
            {
                yield return Picture("offer-focus", 1920, 1080); // the focus choice after the first floor
                _levels.PickFocusChoice(0);
                yield return ForRealTime(0.3f);
            }
            _levels.Pause();
            yield return null;
            yield return Picture("pause", 1920, 1080);
            _levels.Resume();
        }

        // ---------------- Staging ----------------

        int Slot(string name)
        {
            for (int i = 0; i < _player.Skills.Count; i++)
                if (_player.Skills[i].Name == name) return i;
            Assert.Fail("no skill " + name);
            return -1;
        }

        void Near(Vector3 at, float back)
        {
            Vector3 p = at + Vector3.back * back;
            if (NavMesh.SamplePosition(p, out NavMeshHit hit, 2f, NavMesh.AllAreas)) p = hit.position;
            _player.Teleport(new Vector3(p.x, 0f, p.z));
        }

        void Hurt(int damage)
        {
            _health.Untouchable = false;
            _health.TakeHit(damage);
            _health.Untouchable = true;
        }

        IEnumerator Charging(Vector3 toward, float seconds)
        {
            int slot = Slot("Long Shot");
            _player.EquipSkill(slot);
            yield return UntilFree();
            _player.PressSkill(slot, toward);
            yield return ForRealTime(seconds);
        }

        IEnumerator UntilFree() => Until(() => _player.Action == ActionKind.None && !HitStop.IsActive, 3f);

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        // ---------------- The picture ----------------

        /// <summary>The camera into a texture of this size, the interface drawn into it by hand, written as name.png.</summary>
        IEnumerator Picture(string name, int width, int height)
        {
            yield return null; // after this frame's LateUpdates (a batch run has no end-of-frame to wait for)
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            Camera cam = Camera.main;
            RenderTexture before = cam.targetTexture;
            try
            {
                InterfaceStyle.ScreenForPictures = new Vector2Int(width, height);
                cam.targetTexture = rt;
                // Every piece works out its frame for this screen, as its LateUpdate does.
                _bar.Tick();
                _cast.Tick();
                _bars.Tick();
                _map.Tick();
                _feedback.Tick();
                _signs.Tick();
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                Save(rt, Path.Combine(_folder, name + ".png"));
                _log.Add($"{name}: {width}x{height} {drew}");
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
