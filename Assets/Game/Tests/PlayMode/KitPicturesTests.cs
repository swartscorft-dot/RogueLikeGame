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
    /// Work order 138: pictures of what it changed on screen, taken without a window in work order 068's way, to be judged at
    /// 100 %: the skill bar with six boxes, the floor reward of three kinds (with each third kind, and with six slots full),
    /// and the pause menu's stats with basic-shot upgrades held. Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the
    /// project (otherwise it is skipped):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\138'
    ///   run-tests.ps1 PlayMode -Filter "KitPicturesTests"
    /// </summary>
    public class KitPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const ulong FirstSeed = 0x13800100UL;

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

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work order 068)");
            Directory.CreateDirectory(f);
            _folder = f;
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
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
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

        static IEnumerator Frames(Func<bool> condition, int most = 600)
        {
            for (int i = 0; i < most && !condition(); i++) yield return null;
        }

        static IEnumerator ForRealTime(float seconds)
        {
            float end = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < end) yield return null;
        }

        IEnumerator FinishFloor()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            yield return ForRealTime(0.6f); // past the guard: the cards lit
        }

        string Cards() => string.Join(" / ", _levels.OfferedRewardCards.Select(c => c.ToString()))
                          + "   (scale " + _levels.OfferScale.ToString("0.###") + ")";

        [UnityTest, Timeout(900000)]
        public IEnumerator TheKit()
        {
            // The bar with six boxes, at the start of a run.
            _levels.StartRun(FirstSeed);
            yield return ForRealTime(0.5f);
            yield return Picture("bar-six", 1920, 1080);
            yield return Picture("bar-six-720", 1280, 720);

            // The reward of three kinds, one picture for each third kind (seed after seed until floor 1 shows it).
            var wanted = new List<FloorRewardKind> { FloorRewardKind.Focus, FloorRewardKind.SkillUpgrade, FloorRewardKind.Stat };
            for (int n = 0; n < 40 && wanted.Count > 0; n++)
            {
                _levels.StartRun(FirstSeed + (ulong)n);
                yield return null;
                yield return FinishFloor();
                FloorRewardKind third = _levels.OfferedRewardCards.Count > 2 ? _levels.OfferedRewardCards[2].Kind : FloorRewardKind.Skill;
                if (!wanted.Contains(third)) continue;
                wanted.Remove(third);
                string name = "reward-" + MixedFloorRewards.LogWord(third).Replace(' ', '-');
                _log.Add(name + ": " + Cards());
                yield return Picture(name, 1920, 1080);
                if (third == FloorRewardKind.Focus) yield return Picture(name + "-720", 1280, 720);
            }

            // Two skills taken: six full, no skill card; then a basic-shot card on floor 3 and the pause menu's stats.
            _levels.StartRun(FirstSeed);
            yield return null;
            for (int floor = 1; floor <= 2; floor++)
            {
                yield return FinishFloor();
                _levels.PickReward(0);
                yield return null;
            }
            yield return FinishFloor();
            _log.Add("reward-six-full: " + Cards());
            yield return Picture("reward-six-full", 1920, 1080);
            int shot = _levels.OfferedRewardCards.ToList().FindIndex(c => c.Kind == FloorRewardKind.BasicShot);
            _levels.PickReward(shot);
            yield return null;
            yield return FinishFloor();
            shot = _levels.OfferedRewardCards.ToList().FindIndex(c => c.Kind == FloorRewardKind.BasicShot);
            _log.Add("floor 4's reward: " + Cards());
            yield return Picture("reward-floor-4", 1920, 1080);
            _levels.PickReward(shot);
            yield return ForRealTime(0.3f);
            _levels.Pause();
            yield return null;
            yield return Picture("pause-stats", 1920, 1080);
            yield return Picture("pause-stats-720", 1280, 720);
            _log.Add("stats: " + string.Join(" | ", _hud.StatLines()));
            _levels.Resume();
            yield return null;
            Assume.That(_hud.OpenSheet(), Is.True, "her sheet up");
            yield return Picture("sheet", 1920, 1080);
            _hud.CloseSheet();
        }

        // ---------------- The picture (work order 068's way) ----------------

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
                _bar.Tick();
                _cast.Tick();
                _bars.Tick();
                _map.Tick();
                _feedback.Tick();
                _signs.Tick();
                _hud.Tick();
                cam.Render();
                string drew = ImguiByHand.Available ? ImguiByHand.Describe(ImguiByHand.Draw(rt)) : "the engine's IMGUI entry points were not found";
                Save(rt, Path.Combine(_folder, name + ".png"));
                string outside = _hud.SheetUp ? _hud.SheetTextOutside() : _levels.Paused ? _hud.PauseTextOutside() : "";
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} boxes {_bar.SlotCount} text outside: [{outside}] {drew}");
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
