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
    /// Work order 114 (the start and main menu), in the ControlRoom scene through the menu's intents, a test keyboard and its one
    /// hit test: the flow by keys and by mouse (the main menu, her character, the loadout, a run started with that loadout; Escape
    /// back at each step); the question-mark slots refuse; with a trial off its part is locked and the run still starts; the end
    /// screen's Enter leads to the main menu; his records written to the test's own file (never his); her picture missing gives the
    /// flat backdrop; her sheet at a run's start is the run's own; every word inside its shape on every screen at 1920 by 1080 and
    /// 1280 by 720; a frame with the menu up and nothing changing allocates nothing, and during a run it costs nothing; the start
    /// check's start (F5's StartNewRun under the menu) and the tests' and pilots' start (StartRun, no menu) still work; the menu
    /// made before or after the game's floors and the HUD. The trial's switches are set by each test, whatever their default.
    /// </summary>
    public class StartMenuPlayTests
    {
        const int MostFrames = 600;
        static readonly Vector2Int[] Sizes = { new Vector2Int(1920, 1080), new Vector2Int(1280, 720) };
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        StartMenu _menu;
        Keyboard _keyboard;
        string _saves;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _saves = Path.Combine(Path.GetTempPath(), "StartMenuPlayTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_saves);
            GeneratedLevels.BetweenRunsSavePath = Path.Combine(_saves, GeneratedLevels.BetweenRunsFileName); // never the game's own save
            StartMenu.RecordsPath = Path.Combine(_saves, StartMenu.RecordsFileName); // never his records
            BetweenRunsSwitch.Unload(); // the switches as the tests set them, never a stored choice
            AttachmentsSwitch.Unload();
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads
            InterfaceStyle.Shared.Kinetic = true;
            InterfaceStyle.Shared.Menus.Concept = true;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.SkillTiers = false;
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
            InterfaceStyle.ScreenForPictures = Vector2Int.zero;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_menu != null) Object.Destroy(_menu.gameObject);
            if (_health != null) _health.Untouchable = false;
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

        /// <summary>The two trials on (the normal case since he ticked "Ja, på som standard"), a save with Echoes, Thick Hide and the Laser Sight owned.</summary>
        MetaPiece TrialsOn(int echoes = 500, bool attachments = true)
        {
            MetaPiece hide = _levels.MetaValues.Pieces.First(p => p.Name == "Thick Hide");
            var s = new MetaState { Currency = echoes };
            s.Owned.Add(hide.Id);
            s.AttachmentsOwned.Add(WeaponAttachments.LaserSight);
            Assert.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            _levels.MetaValues.Enabled = true;
            _levels.MetaValues.Attachments = attachments;
            return hide;
        }

        int ItemNamed(string name) => Enumerable.Range(0, _hud.MenuCards.Count).FirstOrDefault(i => _hud.BetweenCardNames[_hud.MenuCards[i]] == name) is int i && i < _hud.MenuCards.Count && _hud.BetweenCardNames[_hud.MenuCards[i]] == name ? i : -1;

        IEnumerator OpenOn(MenuScreen screen)
        {
            Assert.That(_menu.Open(screen), Is.True, "opened on " + screen);
            yield return null;
            yield return null;
        }

        // ---------------- The flow ----------------

        [UnityTest]
        public IEnumerator TheFlow_ByKeys_MainCharacterLoadout_ARunStartsWithThatLoadout_AndEscapeStepsBackEachStep()
        {
            MetaPiece hide = TrialsOn();
            yield return OpenOn(MenuScreen.Main);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            Assert.That(Time.timeScale, Is.Zero, "the game stands still under it");
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True, "no key reaches her");
            Assert.That(_menu.MainLines, Has.No.Member(MainLine.Quit), "QUIT is left out in the Editor");

            yield return Tap(Key.DownArrow);
            Assert.That(_menu.Selected, Is.EqualTo(1));
            yield return Tap(Key.W);
            Assert.That(_menu.Selected, Is.EqualTo(0), "W as the up arrow");
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character), "START RUN: her character");
            yield return Tap(Key.Escape);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "Escape: back to the main menu");
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout), "her slot chosen: the loadout");
            Assert.That(_levels.BetweenRunsUp, Is.True, "the screen between runs is up under it (its panels reused)");
            Assert.That(_menu.Selected, Is.EqualTo(_menu.ItemCount - 1), "straight to START");
            int hideItem = ItemNamed(hide.Name), sightItem = ItemNamed("Laser Sight");
            Assert.That(hideItem, Is.GreaterThanOrEqualTo(0), "the owned piece is on the loadout");
            Assert.That(sightItem, Is.GreaterThanOrEqualTo(0), "the owned part is on the loadout");
            Assert.That(_hud.MenuCards.Select(c => _hud.BetweenCardNames[c]), Does.Not.Contain("Heavy Hand"), "what he does not own is not on the loadout");

            while (_menu.Selected != sightItem) yield return Tap(Key.UpArrow);
            yield return Tap(Key.Enter);
            Assert.That(WeaponRack.IsFitted(_levels.BetweenRunsState, WeaponAttachments.LaserSight), Is.True, "Enter fits the part");
            while (_menu.Selected != hideItem) yield return Tap(Key.UpArrow);
            yield return Tap(Key.Enter);
            Assert.That(_levels.BetweenRunsState.IsEquipped(hide.Id), Is.True, "Enter equips the piece");
            yield return Tap(Key.Escape);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character), "Escape: back to her character");
            Assert.That(_levels.BetweenRunsUp, Is.False);
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            yield return Tap(Key.Space);
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun && _levels.IsInLevel, Is.True, "START: the run begins");
            Assert.That(_menu.Up, Is.False, "the menu has gone");
            Assert.That(_levels.Modifiers.Meta.Pieces, Does.Contain(hide.Name).Or.Contain(hide.Id), "the run starts with the piece equipped");
            Assert.That(_levels.Modifiers.Weapon.Parts, Does.Contain("Laser Sight"), "and the part fitted");
            Assert.That(Time.timeScale, Is.GreaterThan(0f), "the game runs");
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
        }

        [UnityTest]
        public IEnumerator TheFlow_ByMouse_MainCharacterLoadoutUpgrades_AndARunStarts()
        {
            MetaPiece hide = TrialsOn();
            yield return OpenOn(MenuScreen.Main);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.MainLine, 0).center), Is.True);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character));
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Back, 0).center), Is.True, "BACK");
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
            _menu.ClickAt(_menu.HitRect(StartMenu.HitKind.MainLine, 0).center);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Slot, 0).center), Is.True, "her slot");
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            int hideItem = ItemNamed(hide.Name);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Item, hideItem).center), Is.True, "a click on the piece's card");
            Assert.That(_levels.BetweenRunsState.IsEquipped(hide.Id), Is.True);

            // The UPGRADES tab: a rank bought with a click.
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 1).center), Is.True);
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Upgrades));
            int echoes = _levels.BetweenRunsState.Currency;
            int health = ItemNamed("Health");
            Assert.That(health, Is.GreaterThanOrEqualTo(0), "the ranks are on UPGRADES");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Item, health).center), Is.True, "a rank bought");
            Assert.That(_levels.BetweenRunsState.RankOf(_levels.MetaValues.Ranks.First(r => r.Name == "Health").Id), Is.EqualTo(1));
            Assert.That(_levels.BetweenRunsState.Currency, Is.LessThan(echoes), "with Echoes");
            int owned = ItemNamed(hide.Name);
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Item, owned).center), Is.False, "an owned piece refuses on UPGRADES");
            Assert.That(_menu.Note, Is.EqualTo(StartMenu.OwnedAlready));
            int heavy = ItemNamed("Heavy Hand");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Item, heavy).center), Is.True, "a piece bought");
            Assert.That(_levels.BetweenRunsState.Owns(_levels.MetaValues.Pieces.First(p => p.Name == "Heavy Hand").Id), Is.True);

            // Back to START RUN by its tab, on by RUN LOADOUT, and START with a click.
            _menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Tab, 0).center);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character));
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Next, 0).center), Is.True, "RUN LOADOUT");
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            Assert.That(ItemNamed("Heavy Hand"), Is.GreaterThanOrEqualTo(0), "bought on UPGRADES, it is on the loadout now");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Item, _menu.ItemCount - 1).center), Is.True, "START");
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_menu.Up, Is.False);
            Assert.That(_levels.Modifiers.Meta.AnyRanks, Is.True, "the rank bought goes with her");
        }

        [UnityTest]
        public IEnumerator TheQuestionMarkSlots_Refuse_ByKeysAndByMouse()
        {
            yield return OpenOn(MenuScreen.Character);
            yield return Tap(Key.RightArrow);
            Assert.That(_menu.Selected, Is.EqualTo(1));
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character), "a dark slot cannot be chosen");
            Assert.That(_menu.Note, Is.EqualTo(StartMenu.NotYet));
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Slot, 3).center), Is.False, "nor clicked");
            Assert.That(_menu.ClickAt(_menu.HitRect(StartMenu.HitKind.Next, 0).center), Is.False, "nor stepped on from");
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Character));
            yield return Tap(Key.LeftArrow);
            yield return Tap(Key.LeftArrow);
            yield return Tap(Key.LeftArrow);
            Assert.That(_menu.Selected, Is.EqualTo(0), "round to hers");
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
        }

        [UnityTest]
        public IEnumerator WithATrialOff_ItsPartIsALockedLine_AndTheRunStillStarts_CharacterThenStraightToStart()
        {
            _levels.MetaValues.Enabled = false;
            _levels.MetaValues.Attachments = false;
            yield return OpenOn(MenuScreen.Main);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout));
            Assert.That(_levels.BetweenRunsUp, Is.False, "the trial off: the screen between runs never comes up");
            Assert.That(_hud.MenuLockedLines, Is.EqualTo(new[] { RunHud.LockedTrial1, RunHud.LockedTrial2 }));
            Assert.That(_menu.ItemCount, Is.EqualTo(1), "START alone");
            Assert.That(_menu.Selected, Is.Zero);
            Assert.That(File.Exists(GeneratedLevels.BetweenRunsSavePathInUse), Is.False, "nothing of the trial is written");
            yield return Tap(Key.Escape);
            yield return Tap(Key.Escape);
            _menu.PickMain(MainLine.Upgrades);
            yield return null;
            Assert.That(_hud.MenuLockedLines, Is.EqualTo(new[] { RunHud.LockedTrial1, RunHud.LockedTrial2 }), "UPGRADES: the same locked line");
            Assert.That(_menu.ItemCount, Is.Zero);
            yield return Tap(Key.Escape);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            yield return Tap(Key.Enter);
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun, Is.True, "START RUN still works with the trial off");
            Assert.That(_levels.Modifiers.Meta.Any, Is.False);
        }

        [UnityTest]
        public IEnumerator WithTheAttachmentsOff_ThePartsAreALockedLine_AndTheRackStays()
        {
            TrialsOn(attachments: false);
            yield return OpenOn(MenuScreen.Loadout);
            Assert.That(_hud.MenuLockedLines, Is.EqualTo(new[] { RunHud.LockedParts1, RunHud.LockedParts2 }));
            Assert.That(ItemNamed("Thick Hide"), Is.GreaterThanOrEqualTo(0));
            Assert.That(ItemNamed("Laser Sight"), Is.EqualTo(-1));
            Assert.That(_menu.StartTheRun(), Is.Not.Null);
        }

        // ---------------- After a run ----------------

        [UnityTest]
        public IEnumerator AfterTheEndScreen_EnterGoesToTheMainMenu_EscapeToThePracticeRoom_AndEnterThereBringsItBack()
        {
            _levels.MetaValues.Enabled = false;
            yield return OpenOn(MenuScreen.Loadout);
            Assert.That(_menu.StartTheRun(), Is.Not.Null);
            yield return null;
            Assert.That(RunHud.EndNewRunButton, Is.EqualTo("Enter  main menu"), "the end screen's first answer says where it goes");
            _health.TakeHit(100000);
            yield return Frames(() => _levels.EndScreenShown);
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(_menu.Up, Is.False, "the menu stays out of the end screen's way");
            yield return Tap(Key.Enter);
            yield return Frames(() => _menu.Up);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "the end screen, then the main menu");
            Assert.That(_levels.IsInLevel || _levels.InRun, Is.False, "she is back in the practice room under it");
            Assert.That(_health.IsAlive, Is.True);
            yield return Tap(Key.Escape);
            Assert.That(_menu.Up, Is.False, "Escape on the main menu: the practice room");
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(_hud.PromptText, Is.EqualTo("Enter  main menu"));
            yield return Tap(Key.Enter);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "Enter in the practice room brings it back");
        }

        // ---------------- The records ----------------

        [UnityTest]
        public IEnumerator TheRecords_AreWrittenToTheTestsFile_AtARunsStartAndEnd_NeverTheGamesOwn()
        {
            string his = StartMenu.DefaultRecordsPath;
            bool hisThere = File.Exists(his);
            DateTime hisTime = hisThere ? File.GetLastWriteTimeUtc(his) : DateTime.MinValue;
            Assert.That(_menu.RecordsPathInUse, Is.EqualTo(StartMenu.RecordsPath));
            _levels.MetaValues.Enabled = false;
            yield return OpenOn(MenuScreen.Loadout);
            _menu.StartTheRun();
            yield return null;
            RunRecords started = RecordsFile.Load(StartMenu.RecordsPath, out RecordsLoad how, out _);
            Assert.That(how, Is.EqualTo(RecordsLoad.Read), "written at the run's start");
            Assert.That(started.RunsStarted, Is.EqualTo(1));
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending != GeneratedLevels.EndKind.None);
            yield return null;
            RunRecords ended = RecordsFile.Load(StartMenu.RecordsPath, out how, out _);
            Assert.That(ended.Deaths, Is.EqualTo(1));
            Assert.That(ended.DeepestFloor, Is.EqualTo(1));
            Assert.That(ended.LongestRunSeconds, Is.GreaterThan(0f));
            Assert.That(File.Exists(his), Is.EqualTo(hisThere), "his records' file neither made nor removed");
            if (hisThere) Assert.That(File.GetLastWriteTimeUtc(his), Is.EqualTo(hisTime), "nor written");

            // A menu a test makes without a path keeps its records in memory only.
            StartMenu.RecordsPath = null;
            Assert.That(_menu.RecordsPathInUse, Is.Null, "never his folder from a test");
        }

        // ---------------- The picture, the sheet ----------------

        [UnityTest]
        public IEnumerator HerPictureMissing_GivesTheFlatBackdrop_AndOneGiven_IsDrawnInstead()
        {
            yield return OpenOn(MenuScreen.Character);
            Assert.That(Resources.Load<Texture2D>(InterfaceStyle.V.Menus.Start.PictureName), Is.Null, "no picture is in the project");
            Assert.That(_menu.UsesFlatBackdrop, Is.True, "the flat backdrop stands in");
            var picture = new Texture2D(16, 9);
            try
            {
                _menu.PictureOverride = picture;
                Assert.That(_menu.UsesFlatBackdrop, Is.False);
                Assert.That(_menu.Picture, Is.SameAs(picture));
            }
            finally
            {
                _menu.PictureOverride = null;
                Object.Destroy(picture);
            }
        }

        [UnityTest]
        public IEnumerator TheStats_HerSheetAtARunsStart_IsTheRunsOwnSheet_AndTheRecordsAreListed()
        {
            MetaPiece hide = TrialsOn();
            MetaState s = MetaSaveFile.Load(GeneratedLevels.BetweenRunsSavePathInUse, _levels.MetaValues, out _, out _);
            s.Equipped.Add(hide.Id);
            s.AttachmentsFitted.Add(WeaponAttachments.LaserSight);
            s.Ranks[_levels.MetaValues.Ranks[0].Id] = 2;
            Assert.That(MetaSaveFile.Save(GeneratedLevels.BetweenRunsSavePathInUse, s, out string error), Is.True, error);
            yield return OpenOn(MenuScreen.Stats);
            var before = _hud.MenuSheetStats.ToDictionary(l => l.Name, l => l.Value);
            Assert.That(before.Count, Is.GreaterThan(5));
            Assert.That(before.ContainsKey("Loadout") && before["Loadout"].Contains("Laser Sight"), Is.True, "her pistols' part on the sheet");
            Assert.That(_hud.MenuRecordNames.Count, Is.EqualTo(8));
            Assert.That(_hud.MenuRecordValues[6], Is.EqualTo("0"), "Echoes earned in all, the trial's own");

            _menu.Open(MenuScreen.Loadout);
            yield return null;
            Assert.That(_menu.StartTheRun(), Is.Not.Null);
            yield return null;
            Assert.That(_hud.OpenSheet(), Is.True);
            yield return null;
            var run = _hud.SheetStats.ToDictionary(l => l.Name, l => l.Value);
            foreach (KeyValuePair<string, string> line in before)
            {
                Assert.That(run.ContainsKey(line.Key), Is.True, "the run's sheet has " + line.Key);
                Assert.That(run[line.Key], Is.EqualTo(line.Value), line.Key + ": the stats tab's start is the run's start");
            }
            _hud.CloseSheet();
        }

        // ---------------- Measured ----------------

        [UnityTest]
        public IEnumerator EveryWord_IsInsideItsShape_OnEveryScreen_AtBothSizes_TrialsOnAndOff()
        {
            foreach (bool on in new[] { true, false })
            {
                if (on) TrialsOn();
                else
                {
                    _levels.MetaValues.Enabled = false;
                    _levels.MetaValues.Attachments = false;
                }
                foreach (MenuScreen screen in new[] { MenuScreen.Main, MenuScreen.Character, MenuScreen.Loadout, MenuScreen.Upgrades, MenuScreen.Stats })
                {
                    yield return OpenOn(screen);
                    foreach (Vector2Int size in Sizes)
                    {
                        InterfaceStyle.ScreenForPictures = size;
                        string outside = _menu.TextOutside();
                        Assert.That(outside, Is.Empty, screen + " at " + size + " (trials " + (on ? "on" : "off") + ")");
                    }
                    InterfaceStyle.ScreenForPictures = Sizes[0];
                }
                _menu.Close();
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator AFrameWithTheMenuUp_AllocatesNothing_OnEachScreen_AndDuringARunItCostsNothing()
        {
            TrialsOn();
            foreach (MenuScreen screen in new[] { MenuScreen.Main, MenuScreen.Character, MenuScreen.Loadout, MenuScreen.Upgrades, MenuScreen.Stats })
            {
                yield return OpenOn(screen);
                for (int i = 0; i < 3; i++)
                {
                    _menu.Tick(null);
                    _hud.Tick();
                    _menu.LayOutForTests();
                    yield return null;
                }
                Assert.That(() => { for (int i = 0; i < 300; i++) { _menu.Tick(null); _hud.Tick(); _menu.LayOutForTests(); } }, NotAllocating, screen + ": nothing allocated in a frame that changes nothing");
            }
            _menu.Open(MenuScreen.Loadout);
            yield return null;
            Assert.That(_menu.StartTheRun(), Is.Not.Null);
            yield return null;
            yield return null;
            Assert.That(() => { for (int i = 0; i < 300; i++) _menu.Tick(null); }, NotAllocating, "in a run the menu's frame allocates nothing");
            Assert.That(StartMenu.Holds, Is.False, "and draws nothing");
        }

        // ---------------- Not in the way of the project's own starts ----------------

        [UnityTest]
        public IEnumerator TheStartChecksStart_F5sStartNewRunUnderTheMenu_StillGivesARun_TrialsOffAndOn()
        {
            // The play start check meets the menu (it starts the game as his Editor does) and starts with F5's intent.
            _levels.MetaValues.Enabled = false;
            yield return OpenOn(MenuScreen.Main);
            GeneratedLevel level = _levels.StartNewRun();
            Assert.That(level, Is.Not.Null, "trial off: a floor at once");
            yield return null;
            Assert.That(_menu.Up, Is.False, "the menu stepped aside for the run");
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            _levels.BackToPracticeRoom();
            yield return null;

            TrialsOn();
            yield return OpenOn(MenuScreen.Main);
            level = _levels.StartNewRun();
            Assert.That(level, Is.Null, "trial on: the screen between runs comes up, as before");
            yield return null;
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Loadout), "under the menu that is the run's loadout");
            level = _levels.StartFromBetweenRuns();
            Assert.That(level, Is.Not.Null, "its start begins the run, as the check presses it");
            yield return null;
            Assert.That(_menu.Up, Is.False);
            Assert.That(Time.timeScale, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator TheTestsAndPilotsStart_StartRunWithNoMenu_IsAsBefore_AndTheMenuNeverInstallsItselfInATestRun()
        {
            Object.Destroy(_menu.gameObject);
            _menu = null;
            yield return null;
            Assert.That(StartMenu.Current, Is.Null);
            Assert.That(SessionRecordingRule.MayStartByItself(SessionRecordingRule.TestRunnerScenePrefix + "1", true), Is.False, "the rule it installs itself under");
            GeneratedLevel level = _levels.StartRun(0x12345678UL);
            Assert.That(level, Is.Not.Null);
            Assert.That(RunHud.EndNewRunButton, Is.EqualTo("Enter  new run"), "without the menu the end screen is as before");
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.EndScreenShown);
            _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
            yield return null;
            Assert.That(_levels.InRun, Is.True, "without the menu Enter is a new run, as before");
        }

        [UnityTest]
        public IEnumerator AtTheGamesStart_TheMenuComesUp_OrWithSkipInEditor_ARunStarts()
        {
            _levels.MetaValues.Enabled = false;
            _menu.ComeUpAtTheStart(false);
            yield return Frames(() => _menu.Up);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main), "the game opens like a game");
            _menu.Close();
            _menu.ComeUpAtTheStart(true);
            yield return Frames(() => _levels.InRun);
            Assert.That(_levels.InRun, Is.True, "SkipInEditor: straight into a run");
            Assert.That(_menu.Up, Is.False);
        }

        // ---------------- Waking (in the manner of HerAwakeOrderTests) ----------------

        [Test]
        public void TheMenu_AskedBeforeItHasWoken_AnswersWithoutAsking()
        {
            var go = new GameObject("StartMenuBeforeAwake");
            go.SetActive(false); // nothing on it wakes
            try
            {
                StartMenu m = go.AddComponent<StartMenu>();
                Assert.That(m.Up, Is.False);
                Assert.That(m.Screen, Is.EqualTo(MenuScreen.None));
                Assert.That(m.UsesFlatBackdrop, Is.True, "no picture loaded before it comes up");
                Assert.That(m.Escape(), Is.False);
                Assert.That(m.ItemCount, Is.Zero);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [UnityTest]
        public IEnumerator TheMenu_MadeBeforeTheGamesFloorsAndTheHud_ComesUpOnceTheyAreThere()
        {
            // The other order (the menu after them) is every other test's.
            Object.Destroy(_menu.gameObject);
            Object.Destroy(_hud.gameObject);
            Object.Destroy(_levels.gameObject);
            yield return null;
            _menu = StartMenu.Ensure();
            _menu.ComeUpAtTheStart(false);
            for (int i = 0; i < 3; i++) yield return null;
            Assert.That(_menu.Up, Is.False, "nothing to come up over yet, and nothing thrown");
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false;
            _hud = RunHud.Ensure();
            yield return Frames(() => _menu.Up);
            Assert.That(_menu.Screen, Is.EqualTo(MenuScreen.Main));
        }
    }
}
