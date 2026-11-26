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

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The follow-up to work order 128, from a friend's first run: the way from START RUN into a run (where a first-time player got
    /// stuck, then the help: her slot chosen, RUN LOADOUT and her name plate going on with her, a dark slot answering plainly, the
    /// lines of keys saying what goes on and what starts the run), what she cannot afford wherever Echoes buy (drawn dimmer, a try
    /// answered with how many more are needed, nothing bought), the help's switch off, the music's lines and the toon look saved,
    /// and the settings screen opaque over a run. His saved settings and the trial's save are never touched: a temporary save,
    /// PlayerPrefs kept and put back.
    /// </summary>
    public class MenuHelpPlayTests
    {
        const int MostFrames = 60;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };

        PlayerCharacter _player;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        Keyboard _keyboard;
        string _saves;
        Dictionary<string, int?> _prefs;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _prefs = SettingsPrefs.Save();
            MenuHelpValues.On = true;
            SettingsScreenValues.On = true;
            _saves = Path.Combine(Path.GetTempPath(), "MenuHelpPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName);
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName);
            BetweenRunsSwitch.Unload();
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false;
            UpgradeTabsValues.On = false; // work order 139: this class pins the one screen of cards of UPGRADES and its three tabs (the three pages: UpgradeTabsPlayTests)
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            _menu = StartMenu.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            _menu.TestKeyboard = _keyboard;
            InterfaceStyle.ScreenForPictures = new Vector2Int(1920, 1080);
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            MenuHelpValues.On = true;
            SettingsScreenValues.On = true;
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (SettingsScreen.Current != null) Object.Destroy(SettingsScreen.Current.gameObject);
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_levels != null)
            {
                if (_levels.Paused) _levels.Resume();
                if (_levels.BetweenRunsUp) _levels.LeaveBetweenRuns();
                _levels.BackToPracticeRoom();
                _levels.MetaValues.Enabled = false;
                _levels.MetaValues.Attachments = false;
            }
            foreach (MonoBehaviour m in new MonoBehaviour[] { _hud, _levels })
                if (m != null) Object.Destroy(m.gameObject);
            GameSettings.Unload();
            GraphicsChoices.Unload();
            MusicPlayer.Values = new MusicValues();
            ToonLookValues.On = true;
            SavedMusicAndLook.Unload();
            SettingsPrefs.Restore(_prefs);
            GeneratedLevels.BetweenRunsSavePath = null;
            StartMenu.RecordsPath = null;
            if (_saves != null && Directory.Exists(_saves)) Directory.Delete(_saves, true);
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
            UpgradeTabsValues.On = true; // as it ships
        }

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

        IEnumerator ToTheCharacter()
        {
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            Assert.That(_menu.PickMain(MainLine.StartRun), Is.True);
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character));
        }

        /// <summary>The trial's save staged with few Echoes: most of the rack, the parts and the ranks out of her reach.</summary>
        void StagePoor(int echoes)
        {
            MetaValues v = _levels.MetaValues;
            var s = new MetaState { Currency = echoes, EarnedInAll = 40, Runs = 2 };
            Assume.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            v.Enabled = true;
            v.Attachments = true;
        }

        // ---------------- Item 3: where a first-time player gets stuck, and the help ----------------

        [UnityTest]
        public IEnumerator WhatWasFound_TheMouseCrossingTheRow_MovedTheChoiceOntoADarkSlot_AndRunLoadoutRefused_AndTheKeysLineNeverSaidEnter()
        {
            MenuHelpValues.On = false; // the menus as they were
            yield return ToTheCharacter();
            Assert.That(_menu.Selected, Is.EqualTo(StartMenuFlow.HerSlot), "her slot is under the cursor at first");
            Assert.That(_menu.HintWords, Is.EqualTo(new[] { "<", ">", "choose" }), "the line of keys names no Enter, nothing that goes on");
            // The way from her slot to RUN LOADOUT (at the foot's right) crosses the row's dark slots: the last one passed holds the cursor.
            for (int i = 1; i < 6; i++) _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Slot, i).center);
            Assert.That(_menu.Selected, Is.EqualTo(5), "the passing mouse moved the choice onto a dark slot");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Next, 0).center), Is.False, "RUN LOADOUT then refuses");
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character), "still on the character screen");
            Assert.That(_menu.Note, Is.EqualTo(StartMenu.NotYet), "with a small line high over the picture");
        }

        [UnityTest]
        public IEnumerator WithTheHelp_RunLoadout_GoesOnWithHer_WhereverTheMouseLeftTheCursor()
        {
            yield return ToTheCharacter();
            for (int i = 1; i < 6; i++) _menu.HoverForTests(_menu.HitRect(StartMenu.HitKind.Slot, i).center);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Next, 0).center), Is.True);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout), "on to her loadout");
        }

        [UnityTest]
        public IEnumerator WithTheHelp_ADarkSlotAnswersPlainly_TheCursorComesBack_EnterGoesOn_AndSpaceStartsTheRun()
        {
            yield return ToTheCharacter();
            yield return Tap(Key.RightArrow);
            Assert.That(_menu.Selected, Is.EqualTo(1));
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character), "a dark slot refuses");
            Assert.That(_menu.Note, Is.EqualTo(MenuHelpRules.RefusedSlot(InterfaceStyle.V.Menus.Start.Name)));
            StringAssert.Contains("characters to come", _menu.Note);
            Assert.That(_menu.Selected, Is.EqualTo(StartMenuFlow.HerSlot), "and the cursor is back on her");
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout), "the next Enter goes on with her");
            Assert.That(_menu.HintWords.Take(2), Is.EqualTo(new[] { "Space", "start the run" }), "the loadout's line of keys says first what starts the run");
            yield return Tap(Key.Space);
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun, Is.True, "Space started the run");
            Assert.That(_menu.Up, Is.False);
        }

        [UnityTest]
        public IEnumerator WithTheHelp_AClickWorksWhereEnterDoes_ADarkSlotByClickAnswers_HerNamePlateGoesOn_StartStartsTheRun()
        {
            yield return ToTheCharacter();
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Slot, 3).center), Is.False, "a dark slot clicked refuses");
            Assert.That(_menu.Note, Is.EqualTo(MenuHelpRules.RefusedSlot(InterfaceStyle.V.Menus.Start.Name)), "with the plain line");
            Assert.That(_menu.Selected, Is.EqualTo(StartMenuFlow.HerSlot));
            var plate = new Vector2(400f, InterfaceStyle.V.Menus.Start.PlateTop + 60f);
            Assert.That(_menu.HitAt(plate, out StartMenu.HitKind kind, out _), Is.True);
            Assert.That(kind, Is.EqualTo(StartMenu.HitKind.Next), "her name plate is a place to click");
            Assert.That(_menu.ClickAt(plate), Is.True);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            Rect start = _menu.HitRect(StartMenu.HitKind.Item, _menu.ItemCount - 1);
            Assert.That(start.width, Is.GreaterThan(0f), "START is a place to click");
            Assert.That(_menu.ClickAt(start.center), Is.True);
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun, Is.True, "a click on START started the run, as Enter on it does");
        }

        [UnityTest]
        public IEnumerator WithTheHelp_TheLinesOfKeysSayWhatGoesOn_AndEveryWordFits_AtBothSizes()
        {
            yield return ToTheCharacter();
            Assert.That(_menu.HintWords[0], Is.EqualTo("Enter"));
            StringAssert.StartsWith("go on with the Gunslinger", _menu.HintWords[1]);
            _menu.ChooseSlot(2); // the refused line up too
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_menu.TextOutside(), Is.Empty, size + ": the character screen with its line and the refused slot's");
            }
            _menu.ChooseSlot(0);
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_menu.TextOutside(), Is.Empty, size + ": the loadout");
            }
            _menu.Close();
            yield return null;
            StagePoor(400);
            Assert.That(_menu.Open(MenuScreen.Loadout), Is.True);
            yield return null;
            yield return null;
            foreach (Vector2Int size in Sizes)
            {
                InterfaceStyle.ScreenForPictures = size;
                Assert.That(_menu.TextOutside(), Is.Empty, size + ": the loadout with the trials on");
            }
        }

        // ---------------- Item 4: what she cannot afford ----------------

        [UnityTest]
        public IEnumerator WithTheHelp_WhatSheCannotAfford_IsDimmer_AndATryIsAnswered_WithHowManyMore_AndNothingIsBought()
        {
            StagePoor(10);
            Assert.That(_menu.Open(MenuScreen.Upgrades), Is.True);
            yield return null;
            yield return null;
            int count = _menu.ItemCount, item = -1;
            for (int i = 0; i < count && item < 0; i++) if (_hud.MenuItemUnaffordable(_menu, i)) item = i;
            Assert.That(item, Is.GreaterThanOrEqualTo(0), "something on UPGRADES is out of her reach, drawn dimmer before she tries");
            int echoes = _levels.BetweenRunsState.Currency, owned = _levels.BetweenRunsState.Owned.Count, refusals = _hud.EchoesRefusals;
            Assert.That(_menu.UseItem(item), Is.False, "nothing bought");
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 1), "the try answered");
            Assert.That(_hud.EchoesRefusalAge, Is.GreaterThanOrEqualTo(0f).And.LessThan(MenuHelpValues.RefusalSeconds), "its flash and shake run now");
            StringAssert.Contains("more needed for", _menu.Note, "the line says how many more");
            StringAssert.StartsWith("Not enough Echoes: ", _menu.Note);
            Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(echoes), "no Echoes spent");
            Assert.That(_levels.BetweenRunsState.Owned.Count, Is.EqualTo(owned), "nothing owned more");
            Assert.That(MenuHelpRules.Flash(0f), Is.EqualTo(1f));
            Assert.That(MenuHelpRules.Shake(MenuHelpValues.RefusalSeconds), Is.Zero, "one shake, dying away");

            // The screen between runs: the same cards, the same answer, by a click and by the keys' press alike.
            _menu.Close();
            yield return null;
            Assert.That(_levels.OpenBetweenRuns(null), Is.True);
            _hud.Tick();
            int card = -1;
            for (int i = 0; i < _hud.BetweenCardNames.Count && card < 0; i++) if (_hud.BetweenCardUnaffordable(i)) card = i;
            Assert.That(card, Is.GreaterThanOrEqualTo(0));
            Assert.That(_hud.ClickBetweenCard(card), Is.False);
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals + 2));
            Assert.That(_hud.EchoesRefusedCard, Is.EqualTo(card));
            Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(echoes));
        }

        [UnityTest]
        public IEnumerator TheHelpOff_TheMenusAsTheyWere_NoDimming_NoAnswer_AndTheCharactersLineAsBefore()
        {
            MenuHelpValues.On = false;
            StagePoor(10);
            Assert.That(_menu.Open(MenuScreen.Upgrades), Is.True);
            yield return null;
            yield return null;
            int refusals = _hud.EchoesRefusals;
            for (int i = 0; i < _menu.ItemCount; i++)
            {
                Assert.That(_hud.MenuItemUnaffordable(_menu, i), Is.False, "nothing drawn dimmer");
                _menu.UseItem(i);
            }
            Assert.That(_hud.EchoesRefusals, Is.EqualTo(refusals), "no try answered");
            Assert.That(_menu.Note, Does.Not.Contain("more needed"));
            _menu.Close();
            yield return null;
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            yield return ToTheCharacter();
            Assert.That(_menu.HintWords, Is.EqualTo(new[] { "<", ">", "choose" }));
            _menu.ChooseSlot(4);
            Assert.That(_menu.Note, Is.EqualTo(StartMenu.NotYet), "the old small line");
            Assert.That(_menu.Selected, Is.EqualTo(4), "and the cursor where it was");
        }

        // ---------------- Item 2: the music's lines and the toon look saved ----------------

        [UnityTest]
        public IEnumerator TheMusicsLinesAndTheToonLook_AreSaved_AndReadBack_AndTheirRowsSayNothingAboutTheGamesEnd()
        {
            foreach (string k in SavedMusicAndLook.Keys) PlayerPrefs.DeleteKey(k);
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Music, 1), Is.True);
            Assert.That(PlayerPrefs.HasKey(SavedMusicAndLook.MusicKey), Is.False, "never read (a test run): a change writes nothing, so other tests never write his");
            _hud.ChangeSetting(RunHud.MenuLine.Music, 1);
            MusicPlayer.Values = new MusicValues();
            SavedMusicAndLook.Load();
            Assert.That(MusicPlayer.Values.On, Is.True, "nothing saved: today's defaults");
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(SettingsRules.DefaultVolumeStep));
            Assert.That(ToonLookValues.On, Is.True);
            Assert.That(SavedMusicAndLook.IsLoaded, Is.True, "read: from now on a change writes");
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Music, 1), Is.True);
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.MusicVolume, -1), Is.True);
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.ToonLook, 1), Is.True);
            Assert.That(PlayerPrefs.GetInt(SavedMusicAndLook.MusicKey, -1), Is.EqualTo(0), "saved when changed");
            Assert.That(PlayerPrefs.GetInt(SavedMusicAndLook.MusicVolumeKey, -1), Is.EqualTo(SettingsRules.DefaultVolumeStep - 1));
            Assert.That(PlayerPrefs.GetInt(SavedMusicAndLook.ToonLookKey, -1), Is.EqualTo(0));
            MusicPlayer.Values = new MusicValues(); // a new start
            ToonLookValues.On = true;
            SavedMusicAndLook.Load();
            Assert.That(MusicPlayer.Values.On, Is.False, "read back");
            Assert.That(MusicPlayer.Values.VolumeStep, Is.EqualTo(SettingsRules.DefaultVolumeStep - 1));
            Assert.That(ToonLookValues.On, Is.False);
            SettingsScreen screen = SettingsScreen.Ensure();
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            yield return null;
            _menu.PickMain(MainLine.Settings);
            screen.OpenPage(SettingsPage.Sound);
            Assert.That(screen.RowNotes.All(n => n.Length == 0), Is.True, "no \"until the game stops\"");
            screen.OpenPage(SettingsPage.Trials);
            Assert.That(screen.RowNotes.All(n => n.Length == 0), Is.True);
        }

        // ---------------- Item 1: nothing of the HUD through the settings screen ----------------

        [UnityTest]
        public IEnumerator TheSettingsScreenOverARun_HasAnOpaqueBackdrop()
        {
            Assume.That(_levels.StartRun(0x0128128UL), Is.Not.Null);
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Settings), Is.True);
            Assert.That(SettingsScreen.Current.BackdropOpaque, Is.True, "opened from the Esc menu: nothing of the HUD reads through");
            _hud.Escape();
            _levels.Resume();
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_menu.Open(MenuScreen.Main), Is.True);
            _menu.PickMain(MainLine.Settings);
            Assert.That(SettingsScreen.Current.BackdropOpaque, Is.False, "from the start menu its own backdrop is under it");
        }
    }
}
