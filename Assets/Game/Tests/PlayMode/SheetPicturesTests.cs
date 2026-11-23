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
    /// Work order 095: pictures of her sheet (early in a run, late with a dozen steps and some at their cap, a heavy skill, a
    /// multi-shot skill and the basic shot hovered, the pause menu with the sheet's stats; the late sheet with a hover at 1280 by
    /// 720 too), taken without a window as work order 078's <see cref="MenuPicturesTests"/> takes the menus', to compare with the
    /// concept and with 078's menus. Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the project (otherwise skipped):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\095\round1'
    ///   run-tests.ps1 PlayMode -Filter "SheetPicturesTests"
    /// </summary>
    public class SheetPicturesTests
    {
        const string FolderVariable = "ROGUELIKE_CAPTURE";
        const ulong RunSeed = 0x0E117E17UL;

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
        string _folder, _saves;
        readonly List<string> _log = new List<string>();

        static string Folder()
        {
            string f = Environment.GetEnvironmentVariable(FolderVariable);
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work orders 068, 078 and 095)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _saves = Path.Combine(Path.GetTempPath(), "SheetPicturesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // before anything
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false;
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _health.Untouchable = true;
            _bar = Object.FindAnyObjectByType<SkillBar>();
            _levels = GeneratedLevels.Ensure();
            _levels.DropValues.On = false;
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
            if (_player != null) _player.Modifiers = null;
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- The moments ----------------

        [UnityTest, Timeout(900000)]
        public IEnumerator TheSheet()
        {
            // Early: a run just started, no steps.
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            Assume.That(_hud.OpenSheet(), Is.True, "the sheet up");
            yield return Picture("sheet-early", 1920, 1080);
            _hud.CloseSheet();
            yield return null;

            // Late: the first floor's skill taken, then a dozen steps, some at their cap.
            yield return ClearFloor();
            Take(UpgradeKind.FlatDamage, 3f, UpgradeTier.Common);
            Take(UpgradeKind.FlatDamage, 6f, UpgradeTier.Rare);
            Take(UpgradeKind.AttackSpeed, 0.6f, UpgradeTier.Epic);   // past its cap
            Take(UpgradeKind.CritChance, 0.08f, UpgradeTier.Rare);
            Take(UpgradeKind.CritDamage, 0.15f, UpgradeTier.Common);
            Take(UpgradeKind.MaxHealth, 20f, UpgradeTier.Rare);
            Take(UpgradeKind.Blast, 0.4f, UpgradeTier.Epic);          // at its cap
            Take(UpgradeKind.WideWindow, 0.1f, UpgradeTier.Common);
            Take(UpgradeKind.Cooldown, 0.05f, UpgradeTier.Common, "Scatter Blast");
            Take(UpgradeKind.AllCooldowns, 0.2f, UpgradeTier.Epic);   // at its cap
            Take(UpgradeKind.Luck, 1f, UpgradeTier.Common);
            Take(UpgradeKind.RunSpeed, 0.05f, UpgradeTier.Common);
            Take(UpgradeKind.Mending, 3f, UpgradeTier.Common);
            yield return null;
            Assume.That(_hud.OpenSheet(), Is.True, "the sheet up, late");
            yield return Picture("sheet-late", 1920, 1080);
            Hover("Scatter Blast");
            yield return Picture("hover-heavy", 1920, 1080);
            yield return Picture("hover-heavy-720", 1280, 720);
            Hover("Triple Tap");
            yield return Picture("hover-multi", 1920, 1080);
            Hover(HerSheet.BasicShotName);
            yield return Picture("hover-basic", 1920, 1080);
            _hud.CloseSheet();
            yield return null;

            // The pause menu with the sheet's stats.
            Assume.That(_hud.Escape(), Is.True, "paused");
            yield return null;
            yield return Picture("pause", 1920, 1080);
            yield return Picture("pause-720", 1280, 720);
            _levels.Resume();
            yield return null;
        }

        void Hover(string name)
        {
            int at = new List<string>(_hud.SheetTileNames).IndexOf(name);
            _log.Add("hover " + name + ": tile " + at + (at < 0 ? " (not on her bar: tiles " + string.Join(", ", _hud.SheetTileNames) + ")" : ""));
            _hud.HoverSheetSkill(at);
        }

        // ---------------- Staging ----------------

        void Take(UpgradeKind kind, float amount, UpgradeTier tier, string skill = "")
        {
            var u = new Upgrade { Kind = kind, Amount = amount, Tier = tier, Tiered = true, Skill = skill };
            _levels.Modifiers.Take(u);
            _levels.Record?.TakeUpgrade(u.Short, u.Tier);
            _health.RefreshMax();
        }

        IEnumerator ClearFloor()
        {
            int floor = _levels.Floor;
            _levels.FinishFloorForTests();
            float deadline = Time.realtimeSinceStartup + 10f;
            while (Time.realtimeSinceStartup < deadline && _levels.Ending == GeneratedLevels.EndKind.None)
            {
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FloorReward) _levels.PickReward(0);
                else if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0);
                else if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
                else if (_levels.Choice == GeneratedLevels.ChoiceKind.Chest) _levels.PickUpgrade(0);
                else if (_levels.Floor != floor && _levels.IsInLevel) break;
                yield return null;
            }
            for (int i = 0; i < 3; i++) yield return null;
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
                float fit = _hud.SheetUp ? _hud.SheetFit : _hud.PauseFit;
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} fit {fit:0.###} text outside: [{outside}] {drew}");
                if (_hud.SheetUp && _hud.SheetPanel != null) _log.Add("  panel: " + _hud.SheetPanel.ToString().Replace("\n", " | "));
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
