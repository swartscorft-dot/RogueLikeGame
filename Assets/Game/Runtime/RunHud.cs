using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// What a run shows on screen besides the skill bar (work order 018, 2026-11-20): the HUD, the pause menu and the
    /// end screens, drawn with IMGUI like the skill bar, laid out for a view 720 pixels high and scaled. Plain boxes,
    /// bars and text; one accent colour, the cyan-teal of the game's art, and red for health. The pause and the end
    /// themselves (the time scale, what each answer does) are <see cref="GeneratedLevels"/>'; this draws them and reads
    /// their keys.
    ///
    /// The HUD, always: her health as a red bar with the numbers on it at the bottom left, over the skill bar's rows so
    /// it is never under them. The skill bar keeps its own health and focus bars (it is not this order's to change):
    /// the focus gauge shows there well, so it is not shown twice. In a run, a box at the top right: the floor ("Floor 2
    /// of 5"), the room (<see cref="RunWords.Room"/>), the enemies left in it, the run's time, the upgrades taken in
    /// short form, and the run seed small underneath. When she is hurt the edges of the screen flash red, stronger for
    /// a bigger hit (<see cref="HudRules"/>); under a quarter of her health a faint red edge stays. In the practice room,
    /// one line over the skill bar: "F5  start a run".
    ///
    /// The developer's text: F1 shows or hides the debug readout at the top left and the old status box at the top right
    /// (under the run box when both show). Hidden by default in a floor, shown by default in the practice room; F1
    /// changes the one for where she is, and each is kept for the session.
    ///
    /// The pause menu: Esc pauses (not while a choice is up, when Esc does nothing) and Esc again resumes. Its lines,
    /// picked with a click or the arrow keys and Enter: Resume; New run (F5's new seed; "Start a run" in the practice
    /// room); in a run, Same run again (F6's same seed from floor 1; work order 026); Leave to the practice room (F7, only in a floor); Quit (only in a built player); and the
    /// settings of <see cref="GameSettings"/>, changed with a click or the left and right arrows: the dash direction, the
    /// volume, and damage numbers on or off (work order 019, 2026-11-20); and "Between runs (trial)", the switch of what
    /// lasts between runs (work order 029, RunHudBetweenRuns.cs, with its screen). Under it, the keys as they are bound now
    /// (<see cref="InputBindings"/>).
    ///
    /// The end screens, once the game has stopped: "You died" or "Act cleared", the floor and the room she reached, the
    /// run's time, enemies killed, the skills taken in order, the upgrades taken, the focus choice (work order 025), her
    /// health left (act cleared), the run seed; and the keys: Enter a new run, F6 the same run again, F7 or Esc the practice room (F5 a new run too).
    ///
    /// Keys are read only while <see cref="PlayerInputDriver.ReadRealDevices"/> is true; tests use the intents
    /// (<see cref="Escape"/>, <see cref="PickMenuLine"/>, <see cref="ChangeSetting"/>, <see cref="ToggleDevText"/>,
    /// <see cref="AnswerEndScreen"/>). It installs itself when Play starts under the same rule as
    /// <see cref="GeneratedLevels"/> (never in a test run); tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed partial class RunHud : MonoBehaviour
    {
        public const Key PauseKey = Key.Escape;
        public const Key DevTextKey = Key.F1;

        /// <summary>The lines of the pause menu, in the order shown. BetweenRuns (work order 029): the trial's switch, last.</summary>
        public enum MenuLine { Resume, Restart, Leave, Quit, DashDirection, Volume, DamageNumbers, SameRun, BetweenRuns }

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Color s_Accent = new Color(0.25f, 0.85f, 0.85f, 1f);
        static readonly Color s_Health = new Color(0.85f, 0.2f, 0.2f, 1f);
        static readonly Color s_Hurt = new Color(1f, 0.08f, 0.05f, 1f);
        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color s_Dim = new Color(0f, 0f, 0f, 0.55f);
        static readonly Color s_Text = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Faint = new Color(1f, 1f, 1f, 0.6f);

        const float Margin = 16f;
        const float HealthWidth = 300f;
        const float HealthHeight = 30f;
        /// <summary>The health bar's top: over the skill bar's boxes, its health and focus row and its cast bar.</summary>
        const float HealthTop = SkillBar.LayoutHeight - 14f - 60f - 6f - 18f - 6f - 18f - 8f - HealthHeight;
        const float RunBoxWidth = 260f;

        /// <summary>The one running now, or null.</summary>
        public static RunHud Current { get; private set; }

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        // ---------------- What it shows, worked out every frame (also without a window, for tests) ----------------

        public int ShownHealth { get; private set; }
        public int ShownMaxHealth { get; private set; }
        /// <summary>The health bar's fill, 0 to 1.</summary>
        public float HealthFill => ShownMaxHealth > 0 ? Mathf.Clamp01(ShownHealth / (float)ShownMaxHealth) : 0f;
        /// <summary>The numbers on the health bar: "85 / 100".</summary>
        public string HealthText => string.Format(s_Inv, "{0} / {1}", ShownHealth, ShownMaxHealth);
        /// <summary>The red edge's opacity now: the hurt flash, or the low-health edge, the stronger.</summary>
        public float EdgeAlpha { get; private set; }
        /// <summary>What is left of the last hurt flash now.</summary>
        public float HurtFlashNow => HudRules.FlashAfter(_flashStrength, Time.unscaledTime - _flashAt);
        /// <summary>The run box's lines (empty outside a run); the last is the seed.</summary>
        public IReadOnlyList<string> RunBoxLines => _runBox;
        public string RunBoxText => string.Join("\n", _runBox);
        /// <summary>The line over the skill bar in the practice room, or empty.</summary>
        public string PromptText { get; private set; } = "";
        /// <summary>The developer's text (the debug readout and the old status box) is shown now.</summary>
        public bool DevTextShown => InFloor ? _devInFloor : _devInPractice;
        /// <summary>The end screen's lines while one is up (title first), else empty.</summary>
        public IReadOnlyList<string> EndScreenLines => _endLines;
        public string EndScreenText => string.Join("\n", _endLines);
        /// <summary>The pause menu's lines shown now, in order.</summary>
        public IReadOnlyList<MenuLine> MenuLines => _menu;
        /// <summary>The line the arrow keys are on.</summary>
        public int Selected { get; private set; }

        GeneratedLevels _levels;
        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerHealth _listening;
        PlayerInputDriver _driver;
        DebugReadout _readout;
        bool _devInFloor;
        bool _devInPractice = true;
        float _flashStrength;
        float _flashAt = -100f;
        readonly List<string> _runBox = new List<string>();
        readonly List<string> _endLines = new List<string>();
        readonly List<MenuLine> _menu = new List<MenuLine>();
        Texture2D _edge;
        GUIStyle _box;
        GUIStyle _label;
        GUIStyle _small;
        GUIStyle _big;
        GUIStyle _title;
        GUIStyle _button;
        GUIStyle _keys;
        GUIStyle _center;
        GUIStyle _centerBold;
        GUIStyle _pauseTitle;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static RunHud Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("RunHud");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<RunHud>();
            return Current;
        }

        void Awake() => GameSettings.Load();

        void OnDestroy()
        {
            if (_listening != null) _listening.Damaged -= OnHurt;
            if (_readout != null) _readout.enabled = true;
            if (_levels != null)
            {
                _levels.ShowDevBox = true;
                _levels.DevBoxTop = 10f;
            }
            if (_edge != null) Destroy(_edge);
            GameSettings.Unload();
            if (Current == this) Current = null;
        }

        bool InFloor => _levels != null && _levels.IsInLevel;

        void Find()
        {
            if (_levels == null) _levels = GeneratedLevels.Current;
            if (_player == null)
            {
                _player = FindAnyObjectByType<PlayerCharacter>();
                _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
                _driver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
                // The debug readout is in the character's scene: looked for once with her.
                if (_player != null) _readout = FindAnyObjectByType<DebugReadout>(FindObjectsInactive.Include);
            }
            if (_health != _listening)
            {
                if (_listening != null) _listening.Damaged -= OnHurt;
                _listening = _health;
                if (_listening != null) _listening.Damaged += OnHurt;
            }
        }

        void OnHurt(int amount)
        {
            float now = Time.unscaledTime;
            _flashStrength = HudRules.Combine(HudRules.FlashAfter(_flashStrength, now - _flashAt),
                                              HudRules.HurtFlash(amount, _health != null ? _health.Max : 0));
            _flashAt = now;
        }

        // ---------------- Intents ----------------

        /// <summary>
        /// The Esc key: nothing while a choice is up; on an end screen that shows, the practice room; else pauses, or
        /// resumes when paused. True when it did something.
        /// </summary>
        public bool Escape()
        {
            Find();
            if (_levels == null || _levels.Choice != GeneratedLevels.ChoiceKind.None) return false;
            if (_levels.BetweenRunsUp) return _levels.LeaveBetweenRuns(); // work order 029: to the practice room
            if (_levels.Ending != GeneratedLevels.EndKind.None)
                return _levels.EndScreenShown && _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom);
            if (_levels.Paused) return _levels.Resume();
            if (!_levels.Pause()) return false;
            Selected = 0;
            BuildMenu();
            return true;
        }

        /// <summary>Picks a line of the pause menu, as a click or Enter on it does. False when not paused or the line is not shown.</summary>
        public bool PickMenuLine(MenuLine line)
        {
            Find();
            if (_levels == null || !_levels.Paused) return false;
            BuildMenu();
            if (!_menu.Contains(line)) return false;
            switch (line)
            {
                case MenuLine.Resume: _levels.Resume(); break;
                case MenuLine.Restart: _levels.StartNewRun(); break;
                case MenuLine.SameRun: _levels.RestartRun(); break;
                case MenuLine.Leave: _levels.BackToPracticeRoom(); break;
                case MenuLine.Quit:
                    _levels.Resume();
                    _levels.EndRunToQuit(); // work order 033: what the trial's run earned reaches the save first
                    Application.Quit();
                    break;
                case MenuLine.DashDirection: ChangeSetting(line, 1); break;
                case MenuLine.Volume:
                    // Enter or a click on the line itself goes up a step, and round to 0 after 100 %.
                    GameSettings.SetVolumeStep(GameSettings.VolumeStep >= SettingsRules.VolumeSteps ? 0 : GameSettings.VolumeStep + 1);
                    break;
                case MenuLine.DamageNumbers: ChangeSetting(line, 1); break;
                case MenuLine.BetweenRuns: ChangeSetting(line, 1); break;
            }
            return true;
        }

        /// <summary>
        /// Changes a setting one step, as the left and right arrows or its arrow buttons do: the dash direction flips
        /// either way, the volume goes up or down a tenth and stops at 0 and 100 %. False for a line that is not a setting.
        /// </summary>
        public bool ChangeSetting(MenuLine line, int delta)
        {
            if (line == MenuLine.DashDirection)
            {
                GameSettings.SetDash(DashNow == DashDirection.TowardMovement ? DashDirection.TowardCursor : DashDirection.TowardMovement);
                return true;
            }
            if (line == MenuLine.Volume)
            {
                GameSettings.SetVolumeStep(SettingsRules.StepVolume(GameSettings.VolumeStep, delta));
                return true;
            }
            if (line == MenuLine.DamageNumbers)
            {
                GameSettings.SetDamageNumbers(!TimingFeedback.NumbersOn); // either way it flips (work order 019)
                return true;
            }
            if (line == MenuLine.BetweenRuns)
            {
                BetweenRunsSwitch.Set(!BetweenRunsOn); // either way it flips (work order 029)
                return true;
            }
            return false;
        }

        /// <summary>The F1 key: shows or hides the developer's text for where she is (a floor, or the practice room).</summary>
        public void ToggleDevText()
        {
            Find();
            if (InFloor) _devInFloor = !_devInFloor;
            else _devInPractice = !_devInPractice;
        }

        /// <summary>The end screen's keys: Enter a new run, F6 the same run, F7 or Esc the practice room. False with none up.</summary>
        public bool AnswerEndScreen(GeneratedLevels.EndAnswer answer)
        {
            Find();
            return _levels != null && _levels.AnswerEndScreen(answer);
        }

        DashDirection DashNow => GameSettings.Dash(_driver != null ? _driver.ActiveBindings.DashDirection : DashDirection.TowardCursor);

        // ---------------- Every frame ----------------

        void Update()
        {
            Find();
            if (_levels == null || !PlayerInputDriver.ReadRealDevices || !(Keyboard.current is Keyboard keyboard)) return;
            if (keyboard[DevTextKey].wasPressedThisFrame) ToggleDevText();
            if (keyboard[PauseKey].wasPressedThisFrame)
            {
                Escape();
                return;
            }
            bool enter = keyboard.enterKey.wasPressedThisFrame || keyboard.numpadEnterKey.wasPressedThisFrame;
            if (_levels.BetweenRunsUp)
            {
                BetweenRunsKeys(keyboard, enter); // work order 029
                return;
            }
            if (_levels.Ending != GeneratedLevels.EndKind.None)
            {
                if (enter && _levels.EndScreenShown) _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
                return; // F5, F6 and F7 are the run's own keys (GeneratedLevels)
            }
            if (!_levels.Paused) return;
            BuildMenu();
            if (_menu.Count == 0) return;
            if (keyboard.upArrowKey.wasPressedThisFrame) Selected = (Selected + _menu.Count - 1) % _menu.Count;
            else if (keyboard.downArrowKey.wasPressedThisFrame) Selected = (Selected + 1) % _menu.Count;
            Selected = Mathf.Clamp(Selected, 0, _menu.Count - 1);
            MenuLine line = _menu[Selected];
            if (keyboard.leftArrowKey.wasPressedThisFrame) ChangeSetting(line, -1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame) ChangeSetting(line, 1);
            else if (enter) PickMenuLine(line);
        }

        void LateUpdate()
        {
            Find();
            // Health.
            ShownHealth = _health != null ? Mathf.Max(0, _health.Current) : 0;
            ShownMaxHealth = _health != null ? _health.Max : 0;
            bool low = HudRules.IsLow(ShownHealth, ShownMaxHealth);
            EdgeAlpha = Mathf.Max(HurtFlashNow, low ? HudRules.LowHealthEdge : 0f);

            // The developer's text.
            bool dev = DevTextShown;
            if (_readout != null && _readout.enabled != dev) _readout.enabled = dev;

            // The run box.
            _runBox.Clear();
            RunRecord record = _levels != null ? _levels.Record : null;
            if (_levels != null && _levels.InRun && record != null)
            {
                _runBox.Add(record.FloorText);
                if (_levels.CurrentRoom != null)
                {
                    _runBox.Add(record.RoomText);
                    _runBox.Add(RunWords.EnemiesLeft(_levels.EnemiesLeftInRoom));
                }
                else _runBox.Add("Between rooms");
                _runBox.Add("Time " + RunWords.Clock(record.Seconds));
                // The heal between floors (work order 023) is said in the line of what was taken.
                _runBox.Add("Upgrades: " + record.UpgradesText + (record.FloorHealText.Length > 0 ? "   " + record.FloorHealText : ""));
                _runBox.Add("run " + record.Seed);
            }
            if (_levels != null)
            {
                _levels.ShowDevBox = dev;
                _levels.DevBoxTop = _runBox.Count > 0 ? 10f + RunBoxHeight() + 8f : 10f;
            }

            PromptText = _levels != null && !_levels.IsInLevel && _levels.Ending == GeneratedLevels.EndKind.None
                ? GeneratedLevels.NewLevelKey + "  start a run" : "";

            // The end screen.
            _endLines.Clear();
            if (_levels != null && _levels.Ending != GeneratedLevels.EndKind.None && record != null)
            {
                bool died = _levels.Ending == GeneratedLevels.EndKind.Died;
                _endLines.Add(died ? "You died" : "Act cleared");
                _endLines.Add(record.FloorText + (record.RoomText.Length > 0 ? ",  " + record.RoomText : ""));
                _endLines.Add("Time  " + RunWords.Clock(record.Seconds, tenths: true));
                _endLines.Add("Enemies killed  " + record.Kills.ToString(s_Inv));
                _endLines.Add("Skills taken  " + record.SkillsText);
                _endLines.Add("Upgrades  " + record.UpgradesText);
                _endLines.Add("Skill upgrades  " + record.SkillUpgradesText); // work order 017
                _endLines.Add("Focus  " + record.FocusChoiceText); // work order 025
                if (!died) _endLines.Add(string.Format(s_Inv, "Health left  {0} / {1}", record.HealthLeft, record.MaxHealth));
                AddBetweenRunsEndLines(); // work order 029: nothing with the switch off
                _endLines.Add("Run seed  " + record.Seed);
            }
            if (_levels != null && _levels.Paused) BuildMenu();
            BuildBetweenRunsLines(); // work order 029
        }

        void BuildMenu()
        {
            _menu.Clear();
            _menu.Add(MenuLine.Resume);
            _menu.Add(MenuLine.Restart);
            if (_levels != null && _levels.InRun) _menu.Add(MenuLine.SameRun);
            if (InFloor) _menu.Add(MenuLine.Leave);
            if (!Application.isEditor) _menu.Add(MenuLine.Quit);
            _menu.Add(MenuLine.DashDirection);
            _menu.Add(MenuLine.Volume);
            _menu.Add(MenuLine.DamageNumbers);
            _menu.Add(MenuLine.BetweenRuns); // work order 029: the trial's switch
            if (Selected >= _menu.Count) Selected = _menu.Count - 1;
        }

        /// <summary>The words of a menu line.</summary>
        public string MenuText(MenuLine line)
        {
            switch (line)
            {
                case MenuLine.Resume: return "Resume   (Esc)";
                case MenuLine.Restart: return (_levels != null && _levels.InRun ? "New run" : "Start a run") + "   (" + GeneratedLevels.NewLevelKey + ")";
                case MenuLine.SameRun: return "Same run again   (" + GeneratedLevels.SameLevelKey + ")";
                case MenuLine.Leave: return "Leave to the practice room   (" + GeneratedLevels.PracticeRoomKey + ")";
                case MenuLine.Quit: return "Quit";
                case MenuLine.DashDirection: return "Dash direction:  " + (DashNow == DashDirection.TowardMovement ? "the way you move" : "toward the cursor");
                case MenuLine.DamageNumbers: return "Damage numbers:  " + (TimingFeedback.NumbersOn ? "on" : "off");
                case MenuLine.BetweenRuns: return "Between runs (trial):  " + (BetweenRunsOn ? "on" : "off");
                default: return string.Format(s_Inv, "Volume:  {0} %", SettingsRules.Percent(GameSettings.VolumeStep));
            }
        }

        /// <summary>The keys as they are bound now, a line each: move, shoot, dash, each skill's key with its skill, focus, cover, interact.</summary>
        public List<string> KeyLines()
        {
            var lines = new List<string>();
            InputBindings b = _driver != null ? _driver.ActiveBindings : null;
            if (b == null) return lines;
            string K(Key k) => k == Key.None ? "-" : new InputBinding(k).Label;
            lines.Add("Move  " + K(b.MoveUp) + " " + K(b.MoveLeft) + " " + K(b.MoveDown) + " " + K(b.MoveRight));
            lines.Add("Shoot  " + b.Shoot.Label);
            lines.Add("Dash  " + K(b.Dash) + (DashNow == DashDirection.TowardMovement ? "  (the way you move)" : "  (toward the cursor)"));
            // Work order 025: the way the gauge works, once one is picked (the run's choice, or the practice room's).
            FocusMode focus = _player != null ? _player.FocusMode : FocusMode.Today;
            lines.Add("Focus  " + K(b.Focus) + (focus != FocusMode.Today ? "  (" + FocusModeRules.Name(focus) + ")" : ""));
            lines.Add("Cover  " + K(b.TakeCover) + "  (hold)");
            lines.Add("Interact  " + K(b.Interact));
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            // A key and the skill on it are two things since work order 015: each key shows what sits on it now.
            int count = skills != null && b.Skills != null ? Mathf.Min(_player.KeyCount, b.Skills.Length) : 0;
            for (int key = 0; key < count; key++)
            {
                int slot = _player.SkillOnKey(key);
                // Work order 017: the run's upgrades of the skill beside it.
                List<string> upgrades = slot >= 0 && slot < skills.Count && _player.SkillUpgrades != null ? _player.SkillUpgrades.NamesFor(skills[slot].Name) : null;
                lines.Add(b.SkillLabel(key) + "  " + (slot >= 0 && slot < skills.Count ? skills[slot].Name : "(empty)")
                          + (upgrades != null && upgrades.Count > 0 ? "  (" + string.Join(", ", upgrades) + ")" : ""));
            }
            return lines;
        }

        // ---------------- Drawing ----------------

        float RunBoxHeight() => 14f + 24f + (_runBox.Count - 2) * 18f + 16f;

        void Styles()
        {
            if (_box != null) return;
            _box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 8, 8) };
            _label = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleLeft, wordWrap = true };
            _label.normal.textColor = s_Text;
            _small = new GUIStyle(_label) { fontSize = 11 };
            _small.normal.textColor = s_Faint;
            _big = new GUIStyle(_label) { fontSize = 19, fontStyle = FontStyle.Bold };
            _big.normal.textColor = s_Accent;
            _title = new GUIStyle(_label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4) };
            _keys = new GUIStyle(_label) { fontSize = 12, wordWrap = false };
            _keys.normal.textColor = s_Faint;
            _center = new GUIStyle(_label) { alignment = TextAnchor.MiddleCenter };
            _centerBold = new GUIStyle(_center) { fontStyle = FontStyle.Bold };
            _pauseTitle = new GUIStyle(_title) { fontSize = 26 };
            _pauseTitle.normal.textColor = s_Accent;
            _edge = MakeEdge();
        }

        /// <summary>A texture made in code (not an asset): clear in the middle, opaque toward the edges, for the red edge.</summary>
        static Texture2D MakeEdge()
        {
            const int size = 64;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "HudEdge" };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y)) / (size * 0.5f); // 0 at the edge, 1 in the middle
                float a = Mathf.Clamp01(1f - d / 0.4f);
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * a * 255f));
            }
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }

        void OnGUI()
        {
            if (_player == null) return;
            Styles();
            GUI.depth = -10; // over the skill bar and the run's boxes: the screens dim them
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;
            Color before = GUI.color;

            if (EdgeAlpha > 0.001f)
            {
                GUI.color = new Color(s_Hurt.r, s_Hurt.g, s_Hurt.b, EdgeAlpha);
                GUI.DrawTexture(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), _edge, ScaleMode.StretchToFill);
                GUI.color = before;
            }
            // The skill bar already shows her health beside focus: the HUD draws its own bar only where no skill bar
            // shows, so health is on screen once (producer, 2026-11-20, when work orders 015 and 018 met).
            if (!_lookedForSkillBar) { _skillBar = FindAnyObjectByType<SkillBar>(); _lookedForSkillBar = true; }
            if (_skillBar == null || !_skillBar.isActiveAndEnabled) DrawHealth();
            if (_runBox.Count > 0) DrawRunBox(viewWidth);
            if (PromptText.Length > 0) DrawPrompt(viewWidth);
            if (_levels != null && _levels.EndScreenShown && _endLines.Count > 0) DrawEndScreen(viewWidth);
            else if (_levels != null && _levels.Paused) DrawPause(viewWidth);
            else if (_levels != null && _levels.BetweenRunsUp) DrawBetweenRuns(viewWidth); // work order 029

            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void Fill(Rect r, Color c)
        {
            Color before = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = before;
        }

        void Frame(Rect r, Color c, float t = 2f)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        SkillBar _skillBar;
        bool _lookedForSkillBar;

        void DrawHealth()
        {
            var r = new Rect(Margin, HealthTop, HealthWidth, HealthHeight);
            Fill(r, s_Back);
            Fill(new Rect(r.x, r.y, r.width * HealthFill, r.height), s_Health);
            Frame(r, new Color(s_Accent.r, s_Accent.g, s_Accent.b, 0.5f), 1f);
            GUI.Label(new Rect(r.x + 8f, r.y, 40f, r.height), "HP", _label);
            GUI.Label(r, HealthText, _centerBold);
        }

        void DrawRunBox(float viewWidth)
        {
            float h = RunBoxHeight();
            var r = new Rect(viewWidth - RunBoxWidth - 10f, 10f, RunBoxWidth, h);
            Fill(r, s_Back);
            Fill(new Rect(r.x, r.y, 3f, r.height), s_Accent);
            float y = r.y + 7f;
            GUI.Label(new Rect(r.x + 12f, y, r.width - 18f, 24f), _runBox[0], _big);
            y += 26f;
            for (int i = 1; i < _runBox.Count - 1; i++)
            {
                GUI.Label(new Rect(r.x + 12f, y, r.width - 18f, 18f), _runBox[i], _label);
                y += 18f;
            }
            GUI.Label(new Rect(r.x + 12f, y + 1f, r.width - 18f, 14f), _runBox[_runBox.Count - 1], _small);
        }

        void DrawPrompt(float viewWidth)
        {
            const float w = 220f, h = 26f;
            var r = new Rect((viewWidth - w) * 0.5f, HealthTop, w, h);
            Fill(r, s_Back);
            Frame(r, s_Accent, 1f);
            GUI.Label(r, PromptText, _center);
        }

        void DrawEndScreen(float viewWidth)
        {
            Fill(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), s_Dim);
            const float w = 520f;
            float h = 90f + (_endLines.Count - 1) * 24f + 70f;
            var r = new Rect((viewWidth - w) * 0.5f, (SkillBar.LayoutHeight - h) * 0.5f, w, h);
            Fill(r, s_Back);
            bool died = _levels.Ending == GeneratedLevels.EndKind.Died;
            Frame(r, died ? s_Health : s_Accent);
            _title.normal.textColor = died ? s_Health : s_Accent;
            GUI.Label(new Rect(r.x, r.y + 14f, w, 50f), _endLines[0], _title);
            float y = r.y + 76f;
            for (int i = 1; i < _endLines.Count; i++)
            {
                GUI.Label(new Rect(r.x + 30f, y, w - 60f, 24f), _endLines[i], i == _endLines.Count - 1 ? _small : _label);
                y += 24f;
            }
            y += 12f;
            float bw = (w - 60f - 20f) / 3f;
            if (GUI.Button(new Rect(r.x + 30f, y, bw, 34f), "Enter  new run", _button)) _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun);
            else if (GUI.Button(new Rect(r.x + 40f + bw, y, bw, 34f), GeneratedLevels.SameLevelKey + "  same run", _button)) _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun);
            else if (GUI.Button(new Rect(r.x + 50f + 2f * bw, y, bw, 34f), GeneratedLevels.PracticeRoomKey + " / Esc  practice", _button)) _levels.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom);
        }

        void DrawPause(float viewWidth)
        {
            Fill(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), s_Dim);
            List<string> keys = KeyLines();
            const float w = 560f, rowH = 34f, gap = 6f;
            int keyRows = (keys.Count + 1) / 2;
            float h = 64f + _menu.Count * (rowH + gap) + 18f + keyRows * 17f + 20f;
            var r = new Rect((viewWidth - w) * 0.5f, Mathf.Max(10f, (SkillBar.LayoutHeight - h) * 0.5f), w, h);
            Fill(r, s_Back);
            Frame(r, s_Accent);
            GUI.Label(new Rect(r.x, r.y + 10f, w, 40f), "Paused", _pauseTitle);
            float y = r.y + 58f;
            for (int i = 0; i < _menu.Count; i++)
            {
                MenuLine line = _menu[i];
                var row = new Rect(r.x + 30f, y, w - 60f, rowH);
                if (i == Selected) Frame(new Rect(row.x - 3f, row.y - 3f, row.width + 6f, row.height + 6f), s_Accent);
                bool setting = line == MenuLine.DashDirection || line == MenuLine.Volume || line == MenuLine.DamageNumbers || line == MenuLine.BetweenRuns;
                if (setting)
                {
                    const float arrow = 36f;
                    if (GUI.Button(new Rect(row.x, row.y, arrow, rowH), "<", _button)) { Selected = i; ChangeSetting(line, -1); }
                    if (GUI.Button(new Rect(row.x + arrow + 4f, row.y, row.width - 2f * arrow - 8f, rowH), MenuText(line), _button)) { Selected = i; PickMenuLine(line); }
                    if (GUI.Button(new Rect(row.xMax - arrow, row.y, arrow, rowH), ">", _button)) { Selected = i; ChangeSetting(line, 1); }
                }
                else if (GUI.Button(row, MenuText(line), _button))
                {
                    Selected = i;
                    PickMenuLine(line);
                    return; // the menu may be gone
                }
                y += rowH + gap;
            }
            y += 10f;
            GUI.Label(new Rect(r.x + 30f, y, w - 60f, 16f), "Keys   (arrows and Enter pick, left and right change a setting)", _small);
            y += 18f;
            float colW = (w - 60f) * 0.5f;
            for (int i = 0; i < keys.Count; i++)
            {
                int col = i < keyRows ? 0 : 1, rowIndex = i < keyRows ? i : i - keyRows;
                GUI.Label(new Rect(r.x + 30f + col * colW, y + rowIndex * 17f, colW, 17f), keys[i], _keys);
            }
        }
    }
}
