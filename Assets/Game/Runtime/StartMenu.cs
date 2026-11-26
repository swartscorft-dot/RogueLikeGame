using System.Collections.Generic;
using System.IO;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The start and main menu (work order 114, 2026-11-24; Kristian: "Hvi kan godt også lage en start og main menu i den ui
    /// stilen vi har valgt. Ser for meg at man har en start run. Så kommer man til charachter selection -&gt; run load out. En
    /// annen fane er upgrades. Stats og annet morro."), in decision 0011's style with <see cref="InterfaceStyle"/>'s shapes and
    /// type, behind <see cref="MenuValues.StartMenu"/> (on; off, nothing of it is installed).
    ///
    /// It comes up when the game starts (over the practice room, which waits under it: the game stands still, no key reaches her)
    /// and when a run's end screen is answered with Enter (<see cref="TakeEndScreen"/>); Enter in the practice room brings it
    /// back. Its screens (<see cref="MenuScreen"/>): the main menu (START RUN, UPGRADES, STATS, and QUIT in a built player);
    /// START RUN's first step, her character as a fighting game's select (her picture large, a small low panel of slots: hers,
    /// and dark slots with a question mark that refuse); its second step, the run's loadout (the rack's pieces she owns within
    /// the budget and her pistols' parts, a slot each: the screen between runs' cards and rules, reused, <see cref="RunHud"/>)
    /// and START; the UPGRADES tab (the ranks, the rack's pieces and the parts bought with Echoes, the same cards and rules);
    /// the STATS tab (her sheet at a run's start as she is dressed now, and his records, <see cref="RunRecords"/>). Escape goes
    /// back a step everywhere; on the main menu, to the practice room.
    ///
    /// It steps aside for everything that is not the menu: a floor built (a run by any way, F5, the room lab, the benchmark's
    /// floors), a run going on, the pause menu, an end screen, a choice. It never installs itself in a test run (the rule of
    /// <see cref="SessionRecordingRule"/>), so the tests and the pilots start their runs as before; the play start check, which
    /// starts the game as his Editor does, meets it and leaves it with Escape. <see cref="MenuValues.SkipInEditor"/> on: Play in
    /// the Editor goes straight into a run.
    ///
    /// The intents, for the drawing's clicks, the keys and the tests: <see cref="Open"/>, <see cref="Escape"/>,
    /// <see cref="PickMain"/>, <see cref="ChooseSlot"/>, <see cref="UseItem"/>, <see cref="StartTheRun"/>, <see cref="OpenTab"/>,
    /// <see cref="ClickAt"/>. Drawn in StartMenuGui.cs; the loadout's, the upgrades' and the stats' panels in RunHudStartMenu.cs.
    /// </summary>
    public sealed partial class StartMenu : MonoBehaviour
    {
        /// <summary>His records' file, beside between-runs.json under the game's data folder.</summary>
        public const string RecordsFileName = "records.json";

        /// <summary>The one running now, or null.</summary>
        public static StartMenu Current { get; private set; }
        /// <summary>True when the menu is up: the HUD and the run's keys step aside for it.</summary>
        public static bool Holds => Current != null && Current.Up;
        /// <summary>The menu came up or read a key this frame: the HUD reads none of this frame's keys after it (an Escape that closed the menu does not pause too).</summary>
        public static bool KeysTakenThisFrame => Current != null && Current._keysFrame == Time.frameCount;
        int _keysFrame = -1;

        /// <summary>
        /// Where his records are written: null for the game's own (only when the menu installed itself in play). Tests set a
        /// temporary one, and a menu a test made without one keeps its records in memory only: never his folder.
        /// </summary>
        public static string RecordsPath { get; set; }
        public static string DefaultRecordsPath => Path.Combine(Application.persistentDataPath, RecordsFileName);

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }
        /// <summary>The screen up, or <see cref="MenuScreen.None"/>.</summary>
        public MenuScreen Screen { get; private set; }
        public bool Up => Screen != MenuScreen.None;
        /// <summary>Where the arrow keys are on this screen: a main menu line, a slot, a loadout's or the upgrades' item.</summary>
        public int Selected { get; private set; }
        /// <summary>A short line about the last thing refused ("Not yet: only she is here."); empty when none.</summary>
        public string Note { get; private set; } = "";
        /// <summary>A test's keyboard, read in place of the real one while <see cref="PlayerInputDriver.ReadRealDevices"/> is false.</summary>
        public Keyboard TestKeyboard { get; set; }
        /// <summary>QUIT on the main menu: in a built player; in the Editor it is left out (as the pause menu leaves its Quit out). Pictures may show it.</summary>
        public bool ShowQuit = !Application.isEditor;
        /// <summary>A picture in place of the one in Resources (the pictures of the proposals; never added to the project); null: Resources'.</summary>
        public Texture2D PictureOverride;
        /// <summary>The main menu's lines now.</summary>
        public IReadOnlyList<MainLine> MainLines => _main;

        readonly List<MainLine> _main = new List<MainLine>();
        GeneratedLevels _levels;
        PlayerCharacter _player;
        float _scaleBefore = 1f;
        bool _startPending, _skipAtStart, _openedBetweenRuns;
        Texture2D _picture;
        bool _pictureTried;

        static StartMenuValues S => InterfaceStyle.V.Menus.Start;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            RecordsPath = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            if (!InterfaceStyle.V.Menus.StartMenu) return; // the switch off: the game as before
            StartMenu menu = Ensure();
            menu.StartedByItself = true;
            menu.ComeUpAtTheStart(Application.isEditor && InterfaceStyle.V.Menus.SkipInEditor);
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static StartMenu Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("StartMenu");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<StartMenu>();
            return Current;
        }

        /// <summary>
        /// The game's start: the menu comes up on the first frame with her in the practice room (or, with
        /// <paramref name="straightIntoARun"/>, a run starts there as START would start it). For the install and for tests.
        /// </summary>
        public void ComeUpAtTheStart(bool straightIntoARun)
        {
            _startPending = true;
            _skipAtStart = straightIntoARun;
        }

        void OnDestroy()
        {
            if (Up) Close();
            if (_shade != null) Destroy(_shade); // made in code (StartMenuGui.cs)
            if (Current == this) Current = null;
        }

        GeneratedLevels Levels => _levels != null ? _levels : (_levels = GeneratedLevels.Current);

        bool HasHer()
        {
            if (_player == null) _player = FindAnyObjectByType<PlayerCharacter>();
            return _player != null;
        }

        // ---------------- Opening, closing, the screens ----------------

        /// <summary>
        /// Brings the menu up on <paramref name="screen"/> (the main menu by default) over the practice room: false while a floor is
        /// built, a run goes on or another screen is up, without the game's floors or her.
        /// </summary>
        public bool Open(MenuScreen screen = MenuScreen.Main)
        {
            GeneratedLevels levels = Levels;
            if (screen == MenuScreen.None || levels == null || !HasHer() || MustStepAside(levels)) return false;
            if (!Up)
            {
                _scaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
                _keysFrame = Time.frameCount; // the key that opened it (the end screen's Enter) is not read again by it
                LoadPicture();
            }
            SetScreen(screen);
            return true;
        }

        /// <summary>The menu goes: the practice room is there under it, the game runs again.</summary>
        public void Close()
        {
            if (!Up) return;
            GeneratedLevels levels = Levels;
            if (levels != null && levels.BetweenRunsUp && _openedBetweenRuns && !levels.InRun) levels.LeaveBetweenRuns();
            _openedBetweenRuns = false;
            Screen = MenuScreen.None;
            Note = "";
            Time.timeScale = _scaleBefore;
            PlayerInputDriver.HeldByScreen = false;
            _laidOut = false;
        }

        void SetScreen(MenuScreen next)
        {
            GeneratedLevels levels = Levels;
            // The loadout and the upgrades are the screen between runs' panels: it is up under them, with the trial on.
            bool want = levels != null && StartMenuFlow.UsesBetweenRuns(next) && levels.MetaOn;
            if (want && !levels.BetweenRunsUp)
            {
                _openedBetweenRuns = levels.OpenBetweenRuns(null) || _openedBetweenRuns;
                RunHud.Current?.Tick(); // its lines now, so the cards are there in this frame
            }
            else if (!want && levels != null && levels.BetweenRunsUp)
            {
                levels.LeaveBetweenRuns();
                _openedBetweenRuns = false;
            }
            Screen = next;
            Note = "";
            Selected = 0;
            if (next == MenuScreen.Main) StartMenuFlow.MainLines(ShowQuit, SettingsScreenValues.On, _main); // work order 128: SETTINGS with its switch
            if (next == MenuScreen.Stats) RunHud.Current?.MakeMenuStats(this);
            _laidOut = false;
            Hold();
            _lastCount = ItemCount;
            if (next == MenuScreen.Loadout) Selected = Mathf.Max(0, _lastCount - 1); // START: "character, then straight to START"
        }

        int _lastCount;

        /// <summary>The items changed under the keys (a card bought, the lines come): START stays where it was chosen, the place within the list.</summary>
        void FollowItems()
        {
            if (Screen != MenuScreen.Loadout && Screen != MenuScreen.Upgrades) return;
            int count = ItemCount;
            if (count == _lastCount) return;
            if (Screen == MenuScreen.Loadout && Selected == _lastCount - 1) Selected = count - 1;
            _lastCount = count;
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, count - 1));
        }

        /// <summary>The menu steps aside for these: a floor built, a run, the pause menu, an end screen, a choice, the benchmark.</summary>
        static bool MustStepAside(GeneratedLevels levels) =>
            levels.IsInLevel || levels.InRun || levels.Paused || levels.Ending != GeneratedLevels.EndKind.None
            || levels.Choice != GeneratedLevels.ChoiceKind.None || Benchmark.Current != null;

        /// <summary>The game stands still under the menu and no key reaches her, as under the other screens.</summary>
        void Hold()
        {
            if (!Up) return;
            if (Time.timeScale != 0f) Time.timeScale = 0f;
            PlayerInputDriver.HeldByScreen = true;
        }

        // ---------------- The intents ----------------

        /// <summary>Escape: back a step; on the main menu the menu goes and she is in the practice room. False when it is not up.</summary>
        public bool Escape()
        {
            if (!Up) return false;
            if (SettingsScreen.Holds) return SettingsScreen.Current.Escape(); // work order 128: back one step on the settings screen over it
            MenuScreen back = StartMenuFlow.Back(Screen);
            if (back == MenuScreen.None) Close();
            else SetScreen(back);
            return true;
        }

        /// <summary>A main menu line picked (a click, or Enter on it): its screen, or QUIT. False when the main menu is not up or the line is not shown.</summary>
        public bool PickMain(MainLine line)
        {
            if (Screen != MenuScreen.Main || !_main.Contains(line)) return false;
            Selected = _main.IndexOf(line);
            if (line == MainLine.Settings) return SettingsScreen.Ensure().Open(SettingsFrom.StartMenu); // work order 128: the settings screen over the main menu
            if (line == MainLine.Quit)
            {
                Close();
                Application.Quit(); // a built player only: the line is not shown in the Editor
                return true;
            }
            SetScreen(StartMenuFlow.Pick(line));
            return true;
        }

        /// <summary>A tab clicked: START RUN's first step, UPGRADES or STATS. False when the menu is not up.</summary>
        public bool OpenTab(int tab)
        {
            if (!Up) return false;
            MenuScreen to = StartMenuFlow.TabScreen(tab);
            if (StartMenuFlow.TabOf(Screen) == tab && Screen != MenuScreen.Main) return true;
            SetScreen(to);
            return true;
        }

        /// <summary>A slot of the character's row chosen: hers goes on to the loadout; a dark one refuses and says so. False when refused or not up.</summary>
        public bool ChooseSlot(int slot)
        {
            if (Screen != MenuScreen.Character || slot < 0 || slot >= S.Slots) return false;
            Selected = slot;
            if (!StartMenuFlow.MayChoose(slot))
            {
                Note = NotYet;
                if (MenuHelpValues.On)
                {
                    Note = MenuHelpRules.RefusedSlot(S.Name); // the menus' help (StartMenuHelp.cs): a plain line, and the cursor back on her
                    Selected = StartMenuFlow.HerSlot;
                }
                return false;
            }
            SetScreen(MenuScreen.Loadout);
            return true;
        }

        public const string NotYet = "Not yet: only she is here.";
        public const string OwnedAlready = "Owned already: put it on in START RUN's loadout.";

        /// <summary>The loadout's or the upgrades' items: their cards (and the loadout's START, last).</summary>
        public int ItemCount => RunHud.Current != null && (Screen == MenuScreen.Loadout || Screen == MenuScreen.Upgrades) ? RunHud.Current.MenuItemCount(this) : (Screen == MenuScreen.Loadout ? 1 : 0);

        /// <summary>
        /// Enter or a click on an item of the loadout or the upgrades: the loadout's START starts the run; a card does what the
        /// screen between runs does with it (equip or take off, fit or take off; on UPGRADES buy). An owned card on UPGRADES
        /// refuses. False when nothing happened.
        /// </summary>
        public bool UseItem(int item)
        {
            if (Screen != MenuScreen.Loadout && Screen != MenuScreen.Upgrades) return false;
            int count = ItemCount;
            if (item < 0 || item >= count) return false;
            Selected = item;
            if (Screen == MenuScreen.Loadout && item == count - 1) return StartTheRun() != null;
            RunHud hud = RunHud.Current;
            if (hud == null) return false;
            bool done = hud.UseMenuItem(this, item, out string refused);
            Note = refused ?? "";
            _lastCount = ItemCount;
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, _lastCount - 1));
            return done;
        }

        /// <summary>
        /// START: the run begins as it begins today (the screen between runs' start with the trial on, a new run's start with it
        /// off), and the menu goes. Null when no run started.
        /// </summary>
        public GeneratedLevel StartTheRun()
        {
            GeneratedLevels levels = Levels;
            if (levels == null) return null;
            GeneratedLevel level = levels.BetweenRunsUp ? levels.StartFromBetweenRuns() : levels.StartNewRun();
            if (level == null && levels.BetweenRunsUp) level = levels.StartFromBetweenRuns();
            if (level != null || levels.InRun) Close();
            return level;
        }

        /// <summary>The struck-out skill of Not That One, one step either way (the left and right arrows on its card in the loadout).</summary>
        public bool ChangeStruckOut(int step)
        {
            GeneratedLevels levels = Levels;
            if (Screen != MenuScreen.Loadout || levels == null || !levels.BetweenRunsUp || RunHud.Current == null || !RunHud.Current.MenuItemStrikes(this, Selected)) return false;
            levels.ChangeStruckOut(step);
            return true;
        }

        /// <summary>
        /// The end screen answered with Enter (GeneratedLevels.AnswerEndScreen): with the menu installed, she goes back to the
        /// practice room and the main menu comes up. False without the menu: the answer is a new run, as before.
        /// </summary>
        public static bool TakeEndScreen(GeneratedLevels levels)
        {
            if (Current == null || levels == null) return false;
            levels.BackToPracticeRoom();
            return Current.Open(MenuScreen.Main);
        }

        /// <summary>The practice room may bring the menu back now (Enter): nothing else up, no floor, no run.</summary>
        bool MayOpenFromThePracticeRoom(GeneratedLevels levels) =>
            !Up && !MustStepAside(levels) && !levels.BetweenRunsUp && !levels.PickerOpen && (RunHud.Current == null || !RunHud.Current.SheetUp);

        // ---------------- Every frame ----------------

        void Update()
        {
            Keyboard keys = PlayerInputDriver.ReadRealDevices ? Keyboard.current : TestKeyboard;
            Tick(keys);
        }

        void LateUpdate()
        {
            GeneratedLevels levels = Levels;
            if (!Up || levels == null) return;
            if (MustStepAside(levels)) Close();
            else Hold();
        }

        /// <summary>A frame's work: the records, stepping aside, the start's opening, the keys (public for tests: a frame with nothing changing allocates nothing).</summary>
        public void Tick(Keyboard keys)
        {
            GeneratedLevels levels = Levels;
            if (levels == null) return;
            KeepRecords(levels);
            if (Up && MustStepAside(levels)) Close();
            if (!Up)
            {
                if (_startPending && HasHer() && !levels.IsInLevel && Benchmark.Current == null)
                {
                    _startPending = false;
                    if (_skipAtStart) StartTheRun();
                    else Open(MenuScreen.Main);
                }
                else if (keys != null && (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) && HasHer() && MayOpenFromThePracticeRoom(levels))
                    Open(MenuScreen.Main);
                return;
            }
            // F5 under the menu with the trial on opens the screen between runs: that is the run's loadout.
            if (levels.BetweenRunsUp && !StartMenuFlow.UsesBetweenRuns(Screen)) SetScreen(MenuScreen.Loadout);
            FollowItems();
            Hold();
            if (SettingsScreen.Holds || SettingsScreen.KeysTakenThisFrame) return; // work order 128: the settings screen over it reads its own keys
            if (keys != null && _keysFrame != Time.frameCount && keys.anyKey.wasPressedThisFrame)
            {
                _keysFrame = Time.frameCount;
                Keys(keys);
            }
        }

        void Keys(Keyboard k)
        {
            if (k.escapeKey.wasPressedThisFrame)
            {
                Escape();
                return;
            }
            bool up = k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame, down = k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame;
            bool left = k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame, right = k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame;
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame, space = k.spaceKey.wasPressedThisFrame;
            switch (Screen)
            {
                case MenuScreen.Main:
                    if (up || down) Selected = StartMenuFlow.Step(Selected, up ? -1 : 1, _main.Count);
                    else if ((enter || space) && Selected < _main.Count) PickMain(_main[Selected]);
                    break;
                case MenuScreen.Character:
                    if (left || right)
                    {
                        Selected = StartMenuFlow.Step(Selected, left ? -1 : 1, S.Slots);
                        Note = "";
                    }
                    else if (enter || space) ChooseSlot(Selected);
                    break;
                case MenuScreen.Loadout:
                case MenuScreen.Upgrades:
                    int count = ItemCount;
                    if (up || down) Selected = StartMenuFlow.Step(Selected, up ? -1 : 1, count);
                    else if ((left || right) && Screen == MenuScreen.Loadout) ChangeStruckOut(left ? -1 : 1);
                    else if (space && Screen == MenuScreen.Loadout) StartTheRun();
                    else if (enter || space) UseItem(Selected);
                    break;
            }
        }

        // ---------------- Her picture ----------------

        /// <summary>Her picture now: the override, else the one in Resources by its name (<see cref="StartMenuValues.PictureName"/>), else null (the flat backdrop).</summary>
        public Texture2D Picture => PictureOverride != null ? PictureOverride : _picture;
        /// <summary>The flat backdrop stands in: no picture by that name.</summary>
        public bool UsesFlatBackdrop => Picture == null;

        /// <summary>Loaded once, when the menu first comes up (never in a frame of play, never in Awake).</summary>
        void LoadPicture()
        {
            if (_pictureTried) return;
            _pictureTried = true;
            _picture = Resources.Load<Texture2D>(S.PictureName);
        }

        // ---------------- His records ----------------

        RunRecords _records;
        RunRecord _counting;

        /// <summary>His records, read from their file the first time they are asked for.</summary>
        public RunRecords Records
        {
            get
            {
                if (_records == null) LoadRecords();
                return _records;
            }
        }

        /// <summary>How the records' file was read.</summary>
        public RecordsLoad RecordsLoad { get; private set; }

        /// <summary>The file in use: the tests' path, the game's own when the menu installed itself, else none (kept in memory only).</summary>
        public string RecordsPathInUse => !string.IsNullOrEmpty(RecordsPath) ? RecordsPath : StartedByItself ? DefaultRecordsPath : null;

        void LoadRecords()
        {
            string path = RecordsPathInUse;
            _records = RecordsFile.Load(path, out RecordsLoad how, out string keptAs);
            RecordsLoad = how;
            if (how == RecordsLoad.Damaged) Debug.LogWarning("Records: " + path + " could not be read" + (keptAs.Length > 0 ? "; kept as " + keptAs : "") + ". Starting from zeros.");
            else if (how == RecordsLoad.Newer || how == RecordsLoad.Unreadable) Debug.LogWarning("Records: " + path + " is " + (how == RecordsLoad.Newer ? "a newer game's" : "not readable now") + ": shown as zeros and not written over.");
        }

        void SaveRecords()
        {
            string path = RecordsPathInUse;
            if (string.IsNullOrEmpty(path) || !RecordsFile.MayWrite(RecordsLoad)) return;
            if (!RecordsFile.Save(path, _records, out string error)) Debug.LogWarning("Records: could not be written: " + error);
        }

        /// <summary>Play stopped or the game quit in the middle of a run: what it reached is kept as a run left (no death, no act).</summary>
        void OnApplicationQuit()
        {
            if (_counting == null) return;
            RecordsRules.NoteEnded(Records, RunOutcome.Left, _counting.Floor, _counting.Kills, _counting.Seconds);
            _counting = null;
            SaveRecords();
        }

        /// <summary>A run started or ended (not the room lab's): the records are told and written. A bool and a reference compared in a frame that changes nothing.</summary>
        void KeepRecords(GeneratedLevels levels)
        {
            RunRecord now = levels.InRun && !levels.InLab ? levels.Record : null;
            if (ReferenceEquals(now, _counting)) return;
            RunRecords r = Records;
            if (_counting != null) RecordsRules.NoteEnded(r, _counting.Outcome, _counting.Floor, _counting.Kills, _counting.Seconds);
            if (now != null) RecordsRules.NoteStarted(r);
            _counting = now;
            SaveRecords();
        }
    }
}
