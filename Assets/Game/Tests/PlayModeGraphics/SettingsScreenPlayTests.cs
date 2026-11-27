using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 128: the settings screen. The Esc menu's short list and its Settings line; the start menu's SETTINGS between STATS
    /// and QUIT; the same screen and the same pages from both; a change made from the start menu read in the Esc menu; the keys
    /// (arrows or W and S, A and D, Enter, Esc one step back) and the mouse through the one hit test; every word inside its shape at
    /// 1920 by 1080 and 1280 by 720; a frame with the screen up allocating nothing; and the switch off: the long Esc menu, no
    /// SETTINGS, nothing opened. His saved settings are kept before each test and put back after it (<see cref="SettingsPrefs"/>).
    /// </summary>
    public class SettingsScreenPlayTests
    {
        const ulong RunSeed = 0x0128128UL;
        const int MostFrames = 60;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        SettingsScreen _screen;
        Keyboard _keyboard;
        string _saves;
        Dictionary<string, int?> _prefs;

        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _prefs = SettingsPrefs.Save();
            SettingsScreenValues.On = true;
            AntiAliasingValues.On = true;
            GraphicsChoices.Unload();
            _saves = Path.Combine(Path.GetTempPath(), "SettingsScreenPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // never the game's own save
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName); // never his records
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _player.GetComponent<PlayerHealth>().Untouchable = true;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _screen = SettingsScreen.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            _menu.TestKeyboard = _keyboard;
            _screen.TestKeyboard = _keyboard;
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            SettingsScreenValues.On = true;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
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
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        IEnumerator Press(params Key[] down)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));
            yield return Frames(() => down.All(k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed));
            yield return null;
        }

        IEnumerator Tap(Key key)
        {
            yield return Press(key);
            yield return Press();
        }

        IEnumerator PausedInARun()
        {
            Assume.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return null;
            Assert.That(_hud.Escape(), Is.True, "paused");
            yield return null;
        }

        static List<SettingLine> PageLines(SettingsPage page)
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(page, CameraAngles.On, lines);
            return lines;
        }

        /// <summary>Each page opened from the list, its lines the page's, its rows' words there, and back to the list by Esc.</summary>
        void EveryPage(string from)
        {
            foreach (SettingsPage page in SettingsPages.All)
            {
                Assert.That(_screen.OpenPage(page), Is.True, from + ": " + page);
                Assert.That(_screen.Lines, Is.EqualTo(PageLines(page)), from + ": " + page + "'s lines");
                Assert.That(_screen.RowNames.Count, Is.EqualTo(_screen.Lines.Count));
                Assert.That(_screen.RowValues.All(v => v.Length > 0), Is.True, from + ": " + page + ": every row has a value");
                Assert.That(_screen.TextOutside(), Is.Empty, from + ": " + page + ": every word inside its shape");
                Assert.That(_screen.Escape(), Is.True);
                Assert.That(_screen.Up && !_screen.OnPage, Is.True, from + ": back to the list of pages");
            }
        }

        // ---------------- The Esc menu ----------------

        [UnityTest]
        public IEnumerator TheEscMenu_IsShort_AndItsSettingsLine_OpensEachPage_AndEscGoesBackOneStepAtATime()
        {
            yield return PausedInARun();
            Assert.That(_hud.MenuLines, Is.EqualTo(new[] { RunHud.MenuLine.Resume, RunHud.MenuLine.Restart, RunHud.MenuLine.SameRun, RunHud.MenuLine.Settings, RunHud.MenuLine.Leave }),
                        "the actions only (no Quit in the Editor); the same-run line under New run, as it was");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Settings), Is.EqualTo("Settings"));
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.True);
            Assert.That(SettingsScreen.Holds, Is.True);
            Assert.That(_screen.From, Is.EqualTo(SettingsFrom.PauseMenu));
            Assert.That(_screen.OnPage, Is.False, "it opens on its list of pages");
            Assert.That(_screen.TextOutside(), Is.Empty, "the list's words inside their shapes");
            EveryPage("the Esc menu");
            Assert.That(_hud.Escape(), Is.True, "the HUD's Esc goes to the screen");
            Assert.That(SettingsScreen.Holds, Is.False, "the list: back to the Esc menu");
            Assert.That(_levels.Paused, Is.True, "still paused, on the Esc menu");
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.Paused, Is.False, "and Esc again resumes, as before");
        }

        /// <summary>
        /// The merge of 2026-11-26: work order 134's two choices stand last on the Trials page, "Timing cue" stepping through the four
        /// cues both ways and "Timing click" on and off, read and written through TimingCueSettings, kept in PlayerPrefs and read back.
        /// </summary>
        [UnityTest]
        public IEnumerator TheTrialsPage_HasTheTimingCueAndItsClick_LastOnThePage_ReadAndWrittenThroughTimingCueSettings()
        {
            yield return PausedInARun();
            if (!GameSettings.IsLoaded) GameSettings.Load(); // loaded first, so the known start below is not read over
            TimingCueSettings.SetCue(TimingCue.BarOnly, keep: false); // a known start, whatever this machine has stored
            TimingCueSettings.ForgetSound(keep: false);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.True);
            Assert.That(_screen.OpenPage(SettingsPage.Trials), Is.True);
            List<SettingLine> lines = _screen.Lines.ToList();
            int cue = lines.IndexOf(SettingLine.TimingCue), click = lines.IndexOf(SettingLine.TimingClick);
            // The page's order after the timing cue, each line found by its SettingLine and placed after the one before it (the
            // merge of 2026-11-27, W2, changed deliberately: not counted from the end, as more orders add lines after these):
            // the cue, its click, work order 143's precision and 144's sight opening (each with its switch on), then work
            // order 135's "Line-ups", work order 136's "Waves" and work order 137's "Callers" (all on, nothing kept here).
            int precision = lines.IndexOf(SettingLine.Precision), sight = lines.IndexOf(SettingLine.SightOpening);
            int lineUps = lines.IndexOf(SettingLine.LineUps), waves = lines.IndexOf(SettingLine.Waves);
            Assert.That(cue, Is.GreaterThan(0), "the cue on the page, after the attachments");
            Assert.That(click, Is.EqualTo(cue + 1), "the cue before its click");
            if (PrecisionValues.On) Assert.That(precision, Is.EqualTo(click + 1), "work order 143's line after them");
            else Assert.That(precision, Is.EqualTo(-1), "work order 143's switch off: no precision line");
            int beforeSight = PrecisionValues.On ? precision : click;
            if (SightValues.On) Assert.That(sight, Is.EqualTo(beforeSight + 1), "work order 144's line after 143's");
            else Assert.That(sight, Is.EqualTo(-1), "work order 144's switch off: no sight line");
            int beforeLineUps = SightValues.On ? sight : beforeSight;
            Assert.That(lineUps, Is.EqualTo(beforeLineUps + 1), "work order 135's line-ups after 144's line");
            Assert.That(_screen.RowNames[lineUps], Is.EqualTo("Line-ups"));
            Assert.That(_screen.RowValues[lineUps], Is.EqualTo(RoomTrialSettings.LineUpsChosen ? "on" : "off"));
            Assert.That(waves, Is.EqualTo(lineUps + 1), "work order 136's waves after the line-ups");
            Assert.That(_screen.RowNames[waves], Is.EqualTo("Waves"));
            Assert.That(_screen.RowValues[waves], Is.EqualTo(RoomTrialSettings.WavesChosen ? "on" : "off"));
            int callers = lines.IndexOf(SettingLine.Callers);
            Assert.That(callers, Is.EqualTo(waves + 1), "work order 137's callers after the waves");
            Assert.That(_screen.RowNames[callers], Is.EqualTo("Callers"));
            Assert.That(_screen.RowValues[callers], Is.EqualTo(RoomTrialSettings.CallersChosen ? "on" : "off"));
            int boss = lines.IndexOf(SettingLine.BossFight); // work order 147's line after the callers, with BossValues.Trial on
            if (BossValues.Trial) Assert.That(boss, Is.EqualTo(callers + 1), "work order 147's boss line after the callers");
            else Assert.That(boss, Is.EqualTo(-1), "work order 147's switch off: no boss line");
            int elites = lines.IndexOf(SettingLine.Elites);
            Assert.That(elites, Is.EqualTo((BossValues.Trial ? boss : callers) + 1), "work order 149's elites line after them");
            Assert.That(lines.IndexOf(SettingLine.Attachments), Is.EqualTo(0), "the weapon attachments' line where it was");
            Assert.That(_screen.RowNames[cue], Is.EqualTo("Timing cue"));
            Assert.That(_screen.RowNames[click], Is.EqualTo("Timing click"));
            Assert.That(_screen.RowValues[cue], Is.EqualTo("Bar only"));
            Assert.That(_screen.RowValues[click], Is.EqualTo("off"), "the bar only: no click by default");
            Assert.That(_screen.RowGreyed[cue] || _screen.RowGreyed[click], Is.False);
            Assert.That(_screen.TextOutside(), Is.Empty, "every word inside its shape");

            Assert.That(_screen.Select(cue), Is.True);
            Assert.That(_screen.Change(1), Is.True);
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.Cylinder));
            Assert.That(PlayerPrefs.GetInt(TimingCueSettings.CueKey, -1), Is.EqualTo((int)TimingCue.Cylinder), "kept");
            Assert.That(_screen.RowValues[cue], Is.EqualTo("Cylinder"));
            Assert.That(_screen.RowValues[click], Is.EqualTo("on"), "none stored: the click follows the cue");
            Assert.That(_screen.Change(-1), Is.True);
            Assert.That(_screen.RowValues[cue], Is.EqualTo("Bar only"), "a step back");
            Assert.That(_screen.Change(-1), Is.True);
            Assert.That(_screen.RowValues[cue], Is.EqualTo(SightValues.On ? "Sight" : "Hammer"), "round the other way (work order 144's sight last with its switch on)");
            Assert.That(_screen.Pick(cue), Is.True);
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly), "Enter: the next, round and back");
            Assert.That(_screen.Change(1) && _screen.Change(1), Is.True);
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.Ring));

            Assert.That(_screen.Select(click), Is.True);
            Assert.That(_screen.Change(1), Is.True);
            Assert.That(TimingCueSettings.SoundChosen, Is.False, "the click off");
            Assert.That(PlayerPrefs.GetInt(TimingCueSettings.SoundKey, -9), Is.EqualTo(TimingCues.SoundOff), "kept");
            Assert.That(_screen.RowValues[click], Is.EqualTo("off"));
            Assert.That(_screen.Pick(click), Is.True);
            Assert.That(TimingCueSettings.SoundNow, Is.True, "and on again");
            Assert.That(_screen.RowValues[click], Is.EqualTo("on"));

            GameSettings.Unload();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.BarOnly), "unloaded: the default (read between, as the cue does every frame)");
            GameSettings.Load();
            Assert.That(TimingCueSettings.CueChosen, Is.EqualTo(TimingCue.Ring), "read back");
            Assert.That(TimingCueSettings.SoundStored, Is.EqualTo(TimingCues.SoundOn), "read back");
            yield return null;
        }

        [UnityTest]
        public IEnumerator ASettingOnAPage_StillDoesWhatItDid_WhenPickedThroughTheEscMenusOldIntent()
        {
            yield return PausedInARun();
            bool kick = GameSettings.CameraKick;
            Assert.That(_hud.MenuLines, Has.No.Member(RunHud.MenuLine.CameraKick), "on the Gameplay page now");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.CameraKick), Is.True);
            Assert.That(GameSettings.CameraKick, Is.Not.EqualTo(kick), "flipped, and kept under its key as before");
            Assert.That(PlayerPrefs.GetInt(GameSettings.CameraKickKey, -1), Is.EqualTo(GameSettings.CameraKick ? 1 : 0));
        }

        // ---------------- The start menu ----------------

        [UnityTest]
        public IEnumerator TheStartMenu_HasSettingsBetweenStatsAndQuit_AndOpensTheSameScreen()
        {
            _menu.ShowQuit = true; // the built player's main menu
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.MainLines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Settings, MainLine.Quit }));
            Assert.That(StartMenu.MainWords[(int)MainLine.Settings], Is.EqualTo("SETTINGS"));
            Assert.That(_menu.TextOutside(), Is.Empty, "the five lines fit over the keys' line");
            Assert.That(_menu.PickMain(MainLine.Settings), Is.True);
            Assert.That(SettingsScreen.Holds, Is.True);
            Assert.That(_screen.From, Is.EqualTo(SettingsFrom.StartMenu));
            Assert.That(_menu.Up, Is.True, "the start menu waits under it");
            EveryPage("the start menu");
            Assert.That(_menu.Escape(), Is.True, "the start menu's Esc goes to the screen over it");
            Assert.That(SettingsScreen.Holds, Is.False);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "back on the main menu");
            Assert.That(_menu.Selected, Is.EqualTo(_menu.MainLines.ToList().IndexOf(MainLine.Settings)), "on SETTINGS");
        }

        [UnityTest]
        public IEnumerator AChangeMadeFromTheStartMenu_IsReadInTheEscMenu()
        {
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.PickMain(MainLine.Settings), Is.True);
            Assert.That(_screen.OpenPage(SettingsPage.Gameplay), Is.True);
            int kickRow = _screen.Lines.ToList().IndexOf(SettingLine.CameraKick);
            string kickBefore = _screen.RowValues[kickRow];
            Assert.That(_screen.Select(kickRow) && _screen.Change(1), Is.True);
            string kickAfter = _screen.RowValues[kickRow];
            Assert.That(kickAfter, Is.Not.EqualTo(kickBefore));
            Assert.That(_screen.OpenPage(SettingsPage.Graphics), Is.True);
            int shadowsRow = _screen.Lines.ToList().IndexOf(SettingLine.Shadows);
            Assert.That(_screen.Select(shadowsRow) && _screen.Change(-1), Is.True);
            Assert.That(_screen.RowValues[shadowsRow], Is.EqualTo("low"));
            Assert.That(PlayerPrefs.GetInt(GraphicsChoices.ShadowsKey, -1), Is.EqualTo((int)ShadowsStep.Low), "saved when changed");
            _screen.Escape();
            _screen.Escape();
            _menu.Close();
            yield return null;

            yield return PausedInARun();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.True);
            Assert.That(_screen.OpenPage(SettingsPage.Gameplay), Is.True);
            Assert.That(_screen.RowValues[kickRow], Is.EqualTo(kickAfter), "the Esc menu reads the start menu's change");
            Assert.That(_screen.OpenPage(SettingsPage.Graphics), Is.True);
            Assert.That(_screen.RowValues[shadowsRow], Is.EqualTo("low"));
        }

        // ---------------- Keys and mouse ----------------

        [UnityTest]
        public IEnumerator TheKeys_AreTheStartMenus_ArrowsOrWS_AD_Enter_AndEscOneStepBack()
        {
            yield return PausedInARun();
            int settings = _hud.MenuLines.ToList().IndexOf(RunHud.MenuLine.Settings);
            for (int i = 0; i < settings; i++) yield return Tap(Key.DownArrow);
            Assert.That(_hud.Selected, Is.EqualTo(settings));
            yield return Tap(Key.Enter);
            Assert.That(SettingsScreen.Holds, Is.True, "Enter on Settings opens the screen");
            Assert.That(_screen.OnPage, Is.False, "and the same Enter did not also open a page");
            yield return Tap(Key.S);
            yield return Tap(Key.S);
            Assert.That(_screen.Selected, Is.EqualTo((int)SettingsPage.Gameplay), "S moves down");
            yield return Tap(Key.W);
            Assert.That(_screen.Selected, Is.EqualTo((int)SettingsPage.Sound), "W moves up");
            yield return Tap(Key.DownArrow);
            yield return Tap(Key.Enter);
            Assert.That(_screen.OnPage && _screen.Page == SettingsPage.Gameplay, Is.True, "Enter opens the page");
            int kick = _screen.Lines.ToList().IndexOf(SettingLine.CameraKick);
            while (_screen.Selected != kick) yield return Tap(Key.DownArrow);
            bool before = GameSettings.CameraKick;
            yield return Tap(Key.D);
            Assert.That(GameSettings.CameraKick, Is.Not.EqualTo(before), "D changes the value");
            yield return Tap(Key.LeftArrow);
            Assert.That(GameSettings.CameraKick, Is.EqualTo(before), "the left arrow too");
            Assert.That(_levels.Paused, Is.True);
            yield return Tap(Key.Escape);
            Assert.That(SettingsScreen.Holds && !_screen.OnPage, Is.True, "Esc: back to the list");
            yield return Tap(Key.Escape);
            Assert.That(SettingsScreen.Holds, Is.False, "Esc: back to the Esc menu");
            Assert.That(_levels.Paused, Is.True, "that Esc did not also resume the game");
            yield return Tap(Key.Escape);
            Assert.That(_levels.Paused, Is.False, "the next Esc resumes, as before");
        }

        [UnityTest]
        public IEnumerator TheMouse_ClicksATab_AnArrow_AndAPage_ThroughTheOneHitTest()
        {
            yield return PausedInARun();
            _hud.PickMenuLine(RunHud.MenuLine.Settings);
            Rect sound = _screen.HitRect(SettingsScreen.HitKind.Page, (int)SettingsPage.Sound);
            Assert.That(sound.width, Is.GreaterThan(0f));
            Assert.That(_screen.ClickAt(sound.center), Is.True);
            Assert.That(_screen.OnPage && _screen.Page == SettingsPage.Sound, Is.True, "a click on a page's line opens it");
            Rect trials = _screen.HitRect(SettingsScreen.HitKind.Tab, (int)SettingsPage.Trials);
            Assert.That(_screen.ClickAt(trials.center), Is.True);
            Assert.That(_screen.Page, Is.EqualTo(SettingsPage.Trials), "a tab goes to its page");
            Assert.That(_screen.ClickAt(_screen.HitRect(SettingsScreen.HitKind.Tab, (int)SettingsPage.Graphics).center), Is.True);
            int glow = _screen.Lines.ToList().IndexOf(SettingLine.Glow);
            Rect less = _screen.HitRect(SettingsScreen.HitKind.Less, glow);
            Assert.That(_screen.ClickAt(less.center), Is.True);
            Assert.That(GraphicsChoices.Choice.Glow, Is.False, "the left arrow's place changes the value");
            Assert.That(_screen.Selected, Is.EqualTo(glow));
            Assert.That(_screen.ClickAt(_screen.HitRect(SettingsScreen.HitKind.More, glow).center), Is.True);
            Assert.That(GraphicsChoices.Choice.Glow, Is.True);
            Assert.That(_screen.HitAt(new Vector2(5f, 5f), out _, out _), Is.False, "nothing in the corner");
        }

        // ---------------- Fit and frames ----------------

        [UnityTest]
        public IEnumerator EveryWord_StandsInsideItsShape_AtBothSizes_FromBothPlaces()
        {
            yield return PausedInARun();
            _hud.PickMenuLine(RunHud.MenuLine.Settings);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_screen.TextOutside(), Is.Empty, size + ": the list");
                foreach (SettingsPage page in SettingsPages.All)
                {
                    _screen.OpenPage(page);
                    Assert.That(_screen.TextOutside(), Is.Empty, size + ": " + page);
                }
                _screen.Escape();
            }
            _screen.Close();
            _levels.Resume();
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            _menu.PickMain(MainLine.Settings);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_menu.TextOutside(), Is.Empty, size + ": the start menu");
                foreach (SettingsPage page in SettingsPages.All)
                {
                    _screen.OpenPage(page);
                    Assert.That(_screen.TextOutside(), Is.Empty, size + ": " + page + " from the start menu");
                }
                _screen.Escape();
            }
        }

        /// <summary>
        /// The merge of 2026-11-27, W2: a page whose lines would run into the keys' line draws its rows smaller by one share, the
        /// least that keeps SettingsScreenLook.KeysClear above it (the Trials page's twelve lines of work orders 134, 143, 144, 135
        /// and 136); a page that fits (the Graphics page's eleven) is drawn at its full size, as before.
        /// </summary>
        [UnityTest]
        public IEnumerator APageLongerThanTheRoom_DrawsItsRowsSmaller_APageThatFits_AtFullSize()
        {
            yield return PausedInARun();
            _hud.PickMenuLine(RunHud.MenuLine.Settings);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_screen.OpenPage(SettingsPage.Graphics), Is.True);
                Assert.That(_screen.RowScale, Is.EqualTo(1f), size + ": the Graphics page at its full size");
                Assert.That(_screen.OpenPage(SettingsPage.Trials), Is.True);
                int lines = _screen.Lines.Count;
                SettingsScreenLook look = SettingsScreenValues.Look;
                StartMenuValues s = InterfaceStyle.V.Menus.Start;
                float room = InterfaceStyle.V.LayoutHeight - s.HintBottom - s.HintHeight - look.KeysClear - look.RowsTop;
                float full = lines * (look.RowHeight + look.RowGap) - look.RowGap;
                if (full <= room) Assert.That(_screen.RowScale, Is.EqualTo(1f), size + ": " + lines + " lines fit");
                else
                {
                    Assert.That(_screen.RowScale, Is.LessThan(1f), size + ": " + lines + " lines do not fit at the full size");
                    Assert.That(_screen.RowScale * full, Is.LessThanOrEqualTo(room).And.GreaterThan(room - 1f), size + ": the least share that fits");
                }
                Assert.That(_screen.TextOutside(), Is.Empty, size + ": every word inside its shape");
                _screen.Escape();
            }
        }

        [UnityTest]
        public IEnumerator AFrameWithTheScreenUp_AndNothingChanging_AllocatesNothing_AndMakesNoRowsAgain()
        {
            yield return PausedInARun();
            _hud.PickMenuLine(RunHud.MenuLine.Settings);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                _screen.LayOutForTests();
                _hud.Tick();
                Assert.That(() => { for (int i = 0; i < 300; i++) { _screen.Tick(null); _screen.LayOutForTests(); _hud.Tick(); } }, NotAllocating, size + ": the list");
                _screen.OpenPage(SettingsPage.Graphics);
                int made = _screen.RowsMade;
                _screen.LayOutForTests();
                _hud.Tick();
                Assert.That(() => { for (int i = 0; i < 300; i++) { _screen.Tick(null); _screen.LayOutForTests(); _hud.Tick(); } }, NotAllocating, size + ": the Graphics page");
                Assert.That(_screen.RowsMade, Is.EqualTo(made), "no frame made the rows again");
                _screen.Escape();
            }
            for (int i = 0; i < 3; i++) yield return null;
        }

        [UnityTest]
        public IEnumerator TheScreen_GoesWithThePause_WhenThePauseEndsAnotherWay()
        {
            yield return PausedInARun();
            _hud.PickMenuLine(RunHud.MenuLine.Settings);
            Assert.That(SettingsScreen.Holds, Is.True);
            _levels.Resume();
            yield return null;
            yield return null;
            Assert.That(SettingsScreen.Holds, Is.False, "no settings screen over a game that runs");
        }

        // ---------------- The switch off ----------------

        [UnityTest]
        public IEnumerator TheSwitchOff_TheLongEscMenu_NoSettingsOnTheStartMenu_NothingOpens_NothingApplied()
        {
            SettingsScreenValues.On = false;
            yield return PausedInARun();
            List<RunHud.MenuLine> lines = _hud.MenuLines.ToList();
            Assert.That(lines, Has.No.Member(RunHud.MenuLine.Settings));
            Assert.That(lines, Has.Member(RunHud.MenuLine.Volume).And.Member(RunHud.MenuLine.DashDirection).And.Member(RunHud.MenuLine.AntiAliasing), "the settings in the list, as before");
            Assert.That(lines.Last(), Is.EqualTo(RunHud.MenuLine.Attachments), "the attachments' line last, as before");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.False);
            Assert.That(_screen.Open(SettingsFrom.PauseMenu), Is.False);
            Assert.That(SettingsScreen.Holds, Is.False);
            Assert.That(GraphicsChoices.Change(SettingLine.Shadows, -1, keep: false), Is.False, "nothing new changes");
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.IsLoaded, Is.False, "nothing loaded");
            Assert.That(_hud.MenuText(RunHud.MenuLine.AntiAliasing), Is.EqualTo("Anti-aliasing:  on"), "work order 125's line as it was");
            _levels.Resume();
            _levels.BackToPracticeRoom();
            yield return null;
            _menu.ShowQuit = true;
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            Assert.That(_menu.MainLines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Quit }), "the main menu as before");
            Assert.That(_menu.PickMain(MainLine.Settings), Is.False);
        }
    }
}
