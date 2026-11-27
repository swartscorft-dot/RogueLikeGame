using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The settings screen (work order 128; Kristian, 2026-11-25: "kan du legge til settings og implimentere standard grafikk
    /// instillinger også?" and "også må du gjerne rydde opp i ESC menyen (fordele de på grafikk instillinger/lyd osv) trenger også
    /// en egen settings retning i start menuen"), behind <see cref="SettingsScreenValues.On"/>.
    ///
    /// One screen with the same pages, reached from the Esc menu's "Settings" line (<see cref="RunHud"/>) and from the start menu's
    /// SETTINGS line (<see cref="StartMenu"/>): it opens on its list of pages (GRAPHICS, SOUND, GAMEPLAY, TRIALS, as the start
    /// menu's main list); Enter or a click opens a page, whose lines are slanted rows with their values between two arrows, and
    /// the tabs at the top left go from page to page. Keys as the start menu's: the arrows or W and S move, A and D or left and
    /// right change a value, Enter or Space changes it a step or opens a page, Esc goes back one step (a page to the list, the list
    /// to where it was opened). Every value is saved when it changes and applied at once: the graphics lines through
    /// <see cref="GraphicsChoices"/>, the others through the HUD's own <see cref="RunHud.ChangeSetting"/> with their keys as before,
    /// so a change made here is the same wherever it is read.
    ///
    /// While it is up the HUD and the start menu read no keys and draw nothing of theirs over it (the start menu still draws her
    /// picture under it); the game stands still as under the screen it was opened from. It goes by itself when that screen goes
    /// (a pause ended another way, the start menu stepping aside). Drawn in SettingsScreenGui.cs in the start menu's style.
    /// </summary>
    public sealed partial class SettingsScreen : MonoBehaviour
    {
        /// <summary>The one running now, or null.</summary>
        public static SettingsScreen Current { get; private set; }
        /// <summary>The screen is up: the HUD and the start menu step aside for it.</summary>
        public static bool Holds => Current != null && Current.Up;
        /// <summary>It came up or read a key this frame: no other screen reads this frame's keys after it.</summary>
        public static bool KeysTakenThisFrame => Current != null && Current._keysFrame == Time.frameCount;

        public bool Up { get; private set; }
        public SettingsFrom From { get; private set; }
        /// <summary>A page is open (else the list of pages).</summary>
        public bool OnPage { get; private set; }
        public SettingsPage Page { get; private set; }
        /// <summary>Where the keys are: a page of the list, or a row of the page.</summary>
        public int Selected { get; private set; }
        /// <summary>A test's keyboard, read in place of the real one while <see cref="PlayerInputDriver.ReadRealDevices"/> is false.</summary>
        public Keyboard TestKeyboard { get; set; }

        /// <summary>The open page's lines, its rows' words (name, value, the small note) and which are greyed. For the drawing and tests.</summary>
        public IReadOnlyList<SettingLine> Lines => _lines;
        public IReadOnlyList<string> RowNames => _names;
        public IReadOnlyList<string> RowValues => _values;
        public IReadOnlyList<string> RowNotes => _notes;
        public IReadOnlyList<bool> RowGreyed => _greyed;
        /// <summary>How many times the rows' words were made (when a page opens and when a value changes, never in a frame that changes nothing). For tests.</summary>
        public int RowsMade { get; private set; }

        readonly List<SettingLine> _lines = new List<SettingLine>();
        readonly List<string> _names = new List<string>(), _values = new List<string>(), _notes = new List<string>();
        readonly List<bool> _greyed = new List<bool>();
        int _keysFrame = -1;

        static SettingsScreenLook L => SettingsScreenValues.Look;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static SettingsScreen Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("SettingsScreen");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<SettingsScreen>();
            return Current;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        // ---------------- The intents ----------------

        /// <summary>Brings the screen up on its list of pages. False with the switch off.</summary>
        public bool Open(SettingsFrom from)
        {
            if (!SettingsScreenValues.On) return false;
            Up = true;
            From = from;
            OnPage = false;
            Selected = 0;
            _keysFrame = Time.frameCount; // the key that opened it is not read again by it
            _lines.Clear();
            MakeRows();
            return true;
        }

        /// <summary>The screen goes: back to where it was opened.</summary>
        public void Close()
        {
            if (!Up) return;
            Up = false;
            OnPage = false;
            RunHud.Current?.SettingsClosed(); // the Esc menu's lists made again: a value it shows may have changed
            _laidOut = false;
        }

        /// <summary>Esc: back one step, a page to the list of pages, the list to where it was opened. False when it is not up.</summary>
        public bool Escape()
        {
            if (!Up) return false;
            if (SettingsFlow.BackStaysOpen(OnPage))
            {
                OnPage = false;
                Selected = (int)Page;
                _laidOut = false;
            }
            else Close();
            return true;
        }

        /// <summary>A page opened (from the list, a tab). False when the screen is not up.</summary>
        public bool OpenPage(SettingsPage page)
        {
            if (!Up) return false;
            OnPage = true;
            Page = page;
            Selected = 0;
            MakeRows();
            return true;
        }

        /// <summary>The keys' place moved to a row of the page (or a page of the list). False out of range.</summary>
        public bool Select(int index)
        {
            int count = OnPage ? _lines.Count : SettingsPages.All.Length;
            if (!Up || index < 0 || index >= count) return false;
            Selected = index;
            return true;
        }

        /// <summary>Enter or a click: on the list, its page opens; on a page, the row's value goes a step on. False when nothing happened.</summary>
        public bool Pick(int index)
        {
            if (!Up) return false;
            if (!OnPage) return index >= 0 && index < SettingsPages.All.Length && OpenPage(SettingsPages.All[index]);
            if (!Select(index)) return false;
            return ChangeLine(_lines[index], 1);
        }

        /// <summary>The left or right arrow on the selected row. False on the list, or when the row does not change.</summary>
        public bool Change(int delta) => Up && OnPage && Selected >= 0 && Selected < _lines.Count && ChangeLine(_lines[Selected], delta);

        /// <summary>
        /// A line changed one step, wherever it stands (a test may change one that is not on the open page): a graphics line by
        /// <see cref="GraphicsChoices.Change"/>, the timing cue's two by <see cref="TimingCueSettings.ChangeLine"/> (the merge of
        /// 2026-11-26), any other by the HUD's <see cref="RunHud.ChangeSetting"/>, saved as it always was.
        /// False when it did not change (a greyed line, no HUD).
        /// </summary>
        public bool ChangeLine(SettingLine line, int delta)
        {
            bool done;
            if (GraphicsRules.IsGraphics(line)) done = GraphicsChoices.Change(line, delta);
            else if (TimingCueLines.Is(line)) done = TimingCueSettings.ChangeLine(line, delta);
            else if (PrecisionSettings.Is(line)) done = PrecisionSettings.ChangeLine(line, delta); // work order 143
            else if (SightSettings.Is(line)) done = SightSettings.ChangeLine(line, delta); // work order 144
            else if (RoomTrialLines.Is(line)) done = RoomTrialSettings.ChangeLine(line, delta); // work order 135: the rooms' line-ups (and 136's waves)
            else if (BossTrialSettings.Is(line)) done = BossTrialSettings.ChangeLine(line, delta); // work order 147
            else if (EliteSettings.Is(line)) done = EliteSettings.ChangeLine(line, delta); // work order 149
            else if (TimedChestSettings.Is(line)) done = TimedChestSettings.ChangeLine(line, delta); // work order 153
            else done = RunHud.Current != null && RunHud.MenuLineOf(line, out RunHud.MenuLine menuLine) && RunHud.Current.ChangeSetting(menuLine, delta);
            if (done) MakeRows();
            return done;
        }

        // ---------------- The rows' words ----------------

        /// <summary>The open page's lines and their words, made when a page opens and when a value changes.</summary>
        void MakeRows()
        {
            _names.Clear();
            _values.Clear();
            _notes.Clear();
            _greyed.Clear();
            if (OnPage)
            {
                SettingsPages.Lines(Page, CameraAngles.On, _lines);
                RunHud hud = RunHud.Current;
                for (int i = 0; i < _lines.Count; i++)
                {
                    SettingLine line = _lines[i];
                    if (GraphicsRules.IsGraphics(line))
                    {
                        bool greyed = GraphicsChoices.Greyed(line);
                        _names.Add(GraphicsRules.Name(line));
                        _values.Add(GraphicsChoices.ValueWords(line));
                        _notes.Add(greyed ? L.GreyedNote : GraphicsRules.IsDisplay(line) && !GraphicsChoices.DisplayActs ? L.EditorNote : "");
                        _greyed.Add(greyed);
                    }
                    else if (TimingCueLines.Is(line)) // the merge of 2026-11-26: work order 134's two lines, read through TimingCueSettings
                    {
                        _names.Add(TimingCueLines.Name(line));
                        _values.Add(TimingCueSettings.LineWords(line));
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (PrecisionSettings.Is(line)) // work order 143's line, read through PrecisionSettings
                    {
                        _names.Add(PrecisionLines.Name);
                        _values.Add(PrecisionSettings.LineWords);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (SightSettings.Is(line)) // work order 144's line, read through SightSettings
                    {
                        _names.Add(SightLines.Name);
                        _values.Add(SightSettings.LineWords);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (RoomTrialLines.Is(line)) // work order 135: the rooms' line-ups, read through RoomTrialSettings
                    {
                        _names.Add(RoomTrialLines.Name(line));
                        _values.Add(RoomTrialSettings.LineWords(line));
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (BossTrialSettings.Is(line)) // work order 147's line, read through BossTrialSettings
                    {
                        _names.Add(BossTrialSettings.Name);
                        _values.Add(BossTrialSettings.LineWords);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (EliteSettings.Is(line)) // work order 149's line, read through EliteSettings
                    {
                        _names.Add(EliteLines.Name);
                        _values.Add(EliteSettings.LineWords);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (TimedChestSettings.Is(line)) // work order 153's line, read through TimedChestSettings
                    {
                        _names.Add(TimedChestLines.Name);
                        _values.Add(TimedChestSettings.LineWords);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else if (hud != null && RunHud.MenuLineOf(line, out RunHud.MenuLine menuLine))
                    {
                        MenuWords.Setting(hud.MenuText(menuLine), out string name, out string value);
                        _names.Add(line == SettingLine.Volume ? L.MasterVolumeName : name); // the order's "master volume" beside the music's own
                        _values.Add(value);
                        _notes.Add("");
                        _greyed.Add(false);
                    }
                    else
                    {
                        _lines.RemoveAt(i); // no HUD to change it: not shown
                        i--;
                    }
                }
            }
            else _lines.Clear();
            Selected = Mathf.Clamp(Selected, 0, Mathf.Max(0, (OnPage ? _lines.Count : SettingsPages.All.Length) - 1));
            RowsMade++;
            _laidOut = false;
        }

        // ---------------- Every frame ----------------

        void Update() => Tick(PlayerInputDriver.ReadRealDevices ? Keyboard.current : TestKeyboard);

        /// <summary>A frame's work: going with the screen it was opened from, and the keys (public for tests: a frame with nothing changing allocates nothing).</summary>
        public void Tick(Keyboard keys)
        {
            if (!Up) return;
            if (!StillWanted())
            {
                Close();
                return;
            }
            if (keys != null && _keysFrame != Time.frameCount && keys.anyKey.wasPressedThisFrame)
            {
                _keysFrame = Time.frameCount;
                Keys(keys);
            }
        }

        /// <summary>The screen it was opened from is still there: the pause (with no run's end over it), or the start menu.</summary>
        bool StillWanted()
        {
            if (From == SettingsFrom.StartMenu) return StartMenu.Holds;
            GeneratedLevels levels = GeneratedLevels.Current;
            return levels != null && levels.Paused && levels.Ending == GeneratedLevels.EndKind.None;
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
            bool enter = k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame;
            int count = OnPage ? _lines.Count : SettingsPages.All.Length;
            if (up || down) Selected = StartMenuFlow.Step(Selected, up ? -1 : 1, count);
            else if ((left || right) && OnPage) Change(left ? -1 : 1);
            else if (enter) Pick(Selected);
        }
    }
}
