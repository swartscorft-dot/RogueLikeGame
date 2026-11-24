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
    /// Work order 078: pictures of the menus (the pause menu with upgrades taken, the run's end after a death and after the act
    /// cleared, the screen between runs with a part owned and one fitted), taken without a window as work order 068's
    /// <see cref="InterfacePicturesTests"/> takes the fight's (the camera into a texture, every OnGUI called by hand,
    /// <see cref="ImguiByHand"/>), to compare with the concept. Runs ONLY when ROGUELIKE_CAPTURE names a folder outside the
    /// project (otherwise it is skipped: no ordinary suite takes a picture or leaves a file):
    ///
    ///   $env:ROGUELIKE_CAPTURE = 'C:\Users\kriss\.claude-automation\runner\captures\078\round1'
    ///   run-tests.ps1 PlayMode -Filter "MenuPicturesTests"
    ///
    /// ROGUELIKE_CAPTURE_BEFORE=1 takes this morning's menus too (MenuValues.Concept off), for the comparison. The trial's save
    /// is written only under a temporary folder (<see cref="GeneratedLevels.BetweenRunsSavePath"/>, set before anything).
    /// </summary>
    public class MenuPicturesTests
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
            if (string.IsNullOrEmpty(f)) Assert.Ignore("pictures are taken only when " + FolderVariable + " names a folder (work orders 068 and 078)");
            Directory.CreateDirectory(f);
            return f;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            _folder = Folder();
            _saves = Path.Combine(Path.GetTempPath(), "MenuPicturesTests-" + Guid.NewGuid().ToString("N"));
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
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _levels.DropValues.On = false; // a cleared room's choice comes up by itself
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
            InterfaceStyle.Shared.Menus.Concept = true;
            if (Camera.main != null) Camera.main.targetTexture = null;
            if (_health != null) _health.Untouchable = false;
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
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
        public IEnumerator TheMenus()
        {
            bool before = Environment.GetEnvironmentVariable("ROGUELIKE_CAPTURE_BEFORE") == "1";
            // A run: the first floor's skill and the focus choice taken, then three upgrades of the chests (a rare, two commons).
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            yield return ClearFloor();
            Take(UpgradeKind.AttackSpeed, 0.15f, UpgradeTier.Rare);
            Take(UpgradeKind.CritChance, 0.05f, UpgradeTier.Common);
            Take(UpgradeKind.MaxHealth, 20f, UpgradeTier.Epic);
            Take(UpgradeKind.CritChance, 0.05f, UpgradeTier.Common);
            yield return null;

            // The pause menu: the first row selected, then a setting.
            Assume.That(_hud.Escape(), Is.True, "paused");
            yield return null;
            yield return Picture("pause", 1920, 1080);
            yield return Picture("pause-720", 1280, 720);
            _hud.SelectMenuLine(RunHud.MenuLine.CameraKick);
            yield return null;
            yield return Picture("pause-setting", 1920, 1080);
            if (before)
            {
                InterfaceStyle.Shared.Menus.Concept = false;
                yield return Picture("pause-before", 1920, 1080);
                InterfaceStyle.Shared.Menus.Concept = true;
            }
            _levels.Resume();
            yield return null;

            // Her death: the end screen once the game has stopped.
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Until(() => _levels.EndScreenShown, 5f);
            yield return null;
            yield return Picture("end-dead", 1920, 1080);
            yield return Picture("end-dead-720", 1280, 720);
            if (before)
            {
                InterfaceStyle.Shared.Menus.Concept = false;
                yield return Picture("end-dead-before", 1920, 1080);
                InterfaceStyle.Shared.Menus.Concept = true;
            }

            // A new run to the act's end: every floor cleared, each offer's first card taken, an epic and a rare upgrade on the way.
            _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
            yield return null;
            yield return null;
            _health.Untouchable = true;
            Take(UpgradeKind.Damage, 0.1f, UpgradeTier.Epic);
            Take(UpgradeKind.RunSpeed, 0.08f, UpgradeTier.Rare);
            Take(UpgradeKind.MaxHealth, 10f, UpgradeTier.Common);
            float deadline = Time.realtimeSinceStartup + 120f;
            while (_levels.Ending == GeneratedLevels.EndKind.None && Time.realtimeSinceStartup < deadline) yield return ClearFloor();
            yield return Until(() => _levels.EndScreenShown, 5f);
            yield return null;
            _log.Add("the act: " + _levels.Ending + ", end screen shown " + _levels.EndScreenShown);
            yield return Picture("end-cleared", 1920, 1080);

            // Between runs: the trial and the attachments on, a save with two parts owned and one of them fitted.
            _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom);
            yield return null;
            var s = new MetaState { Currency = 45 };
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            s.AttachmentsOwned.Add(WeaponAttachments.BullBarrel);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = true;
            bool opened = _levels.OpenBetweenRuns();
            _log.Add("between runs opened " + opened + " (the trial's switch " + _levels.MetaOn + ", attachments " + _levels.AttachmentsOn + ")");
            yield return null;
            yield return null;
            yield return Picture("between", 1920, 1080);
            if (before)
            {
                InterfaceStyle.Shared.Menus.Concept = false;
                yield return Picture("between-before", 1920, 1080);
                InterfaceStyle.Shared.Menus.Concept = true;
            }
        }

        // ---------------- Staging ----------------

        void Take(UpgradeKind kind, float amount, UpgradeTier tier)
        {
            var u = new Upgrade { Kind = kind, Amount = amount, Tier = tier, Tiered = tier != UpgradeTier.Common };
            _levels.Modifiers.Take(u);
            _levels.Record?.TakeUpgrade(u.Short, u.Tiered ? u.Tier : UpgradeTier.Common);
            _health.RefreshMax();
        }

        /// <summary>The floor's exit room cleared and every offer that follows answered with its first card, until the next floor or the end.</summary>
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

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
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
                string outside = _levels.Paused ? _hud.PauseTextOutside() : _levels.EndScreenShown ? _hud.EndTextOutside() : "";
                _log.Add($"{DateTime.Now:HH:mm:ss} {name}: {width}x{height} fit {(_levels.Paused ? _hud.PauseFit : _hud.EndFit):0.###} text outside: [{outside}] {drew}");
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
