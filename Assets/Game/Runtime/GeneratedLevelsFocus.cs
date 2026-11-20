using System.Globalization;
using System.Linq;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The focus choice (decision 0005: the choice is offered once, early, between two ways the gauge works for the rest of
    /// the run; his words of 2026-11-19, "Det kan vel kanskje drops/valg i runnet avgjøre?"; work order 025, 2026-11-20), as
    /// part of <see cref="GeneratedLevels"/>.
    ///
    /// When: once a run, right after floor 1's reward has been taken (the skill is picked, then this comes up, then she goes
    /// on to floor 2); with no skill left to offer on floor 1 it comes up in the reward's place. Paused like the other
    /// choices, under their rules (work order 026): one screen at a time, the pick never also fires a skill or a shot, an end
    /// screen closes it. Two options, always the same two in the same order (nothing is drawn for it, so the same run seed
    /// offers it at the same point): Hair Trigger on 1, Steady Hand on 2, picked with the key or a click. It cannot be
    /// skipped; F7 still leaves the run. What is taken is the run's (<see cref="FocusChoice"/>), put on the character as
    /// <see cref="PlayerCharacter.RunFocus"/> and cleared when a run starts or ends.
    ///
    /// The practice room's keys, read only while <see cref="PlayerInputDriver.ReadRealDevices"/> is true and she is in no
    /// floor: F2 Hair Trigger, F3 Steady Hand, F4 back to today's gauge (<see cref="SetPracticeFocus"/>). Only the
    /// character's practice way changes; nothing is written to a settings asset.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        public const Key HairTriggerKey = Key.F2;
        public const Key SteadyHandKey = Key.F3;
        public const Key TodaysFocusKey = Key.F4;

        static readonly FocusModeValues s_defaultFocusValues = new FocusModeValues();
        static readonly Color s_gaugeBack = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color s_gaugeFocus = new Color(0.25f, 0.6f, 1f, 1f);     // the skill bar's focus colour
        static readonly Color s_gaugeWindow = new Color(1f, 0.9f, 0.3f, 1f);     // the skill bar's empowered colour

        /// <summary>The run's focus choice: offered once, after floor 1's reward; today's gauge until it is taken.</summary>
        public RunFocusChoice FocusChoice { get; } = new RunFocusChoice();

        /// <summary>The two ways offered by the choice on screen, in the order shown.</summary>
        public FocusMode[] OfferedFocusModes => Choice == ChoiceKind.FocusChoice ? FocusModeRules.Options.ToArray() : new FocusMode[0];

        FocusModeValues FocusValues => _player != null ? _player.FocusModeValues : s_defaultFocusValues;

        /// <summary>True when the floor reward just taken is the one the focus choice follows, and it has not been offered yet.</summary>
        bool FocusChoiceDue => InRun && !FocusChoice.Offered && FocusModeRules.OfferedAfter(Floor, FocusValues);

        /// <summary>Puts the focus choice up, paused, in place of the floor reward if that is up.</summary>
        void OfferFocusChoice()
        {
            FocusChoice.Offer();
            _offeredSkills.Clear();
            PlaySessionRecorder.Current?.NoteFocusChoiceOffered(Floor, FocusModeRules.Options.Select(FocusModeRules.Name).ToList());
            OpenChoice(ChoiceKind.FocusChoice); // from the reward's screen it stays paused and held: no frame of play between
        }

        /// <summary>
        /// Takes offered way <paramref name="index"/> (0 Hair Trigger, 1 Steady Hand) for the rest of the run: the game goes
        /// on, and she is put at the next floor's entrance. False when the focus choice is not up.
        /// </summary>
        public bool PickFocusChoice(int index)
        {
            if (Choice != ChoiceKind.FocusChoice || index < 0 || index >= FocusModeRules.Options.Length) return false;
            FocusMode mode = FocusModeRules.Options[index];
            FocusChoice.Take(mode);
            string name = FocusModeRules.Name(mode);
            Record?.TakeFocusChoice(name);
            PlaySessionRecorder.Current?.NoteFocusChoiceTaken(Floor, name);
            Debug.Log("Focus choice: " + name);
            CloseChoice();
            EnterFloor(Floor + 1);
            return true;
        }

        /// <summary>
        /// The practice room's way of the gauge (F2, F3, F4): Hair Trigger, Steady Hand or today's. False in a run or a
        /// floor, without a character, or while the pause menu or an end screen is up.
        /// </summary>
        public bool SetPracticeFocus(FocusMode mode)
        {
            PlayerCharacter player = FindPlayer();
            if (player == null || InRun || _level != null || Paused || Ending != EndKind.None || Choice != ChoiceKind.None) return false;
            if (!player.SetPracticeFocusMode(mode)) return false;
            Say("PRACTICE   focus: " + FocusModeRules.Name(mode));
            return true;
        }

        void TryFocusInPractice(FocusMode mode) => SetPracticeFocus(mode);

        /// <summary>The practice room's line about its focus keys, with the way in use.</summary>
        string PracticeFocusLine()
        {
            FocusMode now = _player != null ? _player.PracticeFocusMode : FocusMode.Today;
            return HairTriggerKey + " Hair Trigger   " + SteadyHandKey + " Steady Hand   " + TodaysFocusKey + " today's   (now: "
                   + FocusModeRules.Name(now) + ")";
        }

        /// <summary>
        /// The focus choice on screen, drawn like the other choices: a title, and a button per way with its number, its
        /// name, three lines on what Z does from now on, and at the right a small drawing of the gauge as it will look.
        /// </summary>
        void DrawFocusChoice(float viewWidth)
        {
            const float width = 560f, rowHeight = 96f, gaugeWidth = 120f, gaugeHeight = 14f;
            int count = FocusModeRules.Options.Length;
            float height = 40f + count * (rowHeight + 6f) + 24f;
            float left = (viewWidth - width) * 0.5f, top = (SkillBar.LayoutHeight - height) * 0.5f;
            GUI.Box(new Rect(left, top, width, height),
                    string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose how focus works for the rest of the run (1-{1} or click)", Floor, count),
                    _style);
            float todayCut = _player != null ? _player.Identity.CooldownCut : 0.4f;
            for (int i = 0; i < count; i++)
            {
                FocusMode mode = FocusModeRules.Options[i];
                string[] lines = FocusModeRules.Lines(mode, FocusValues, todayCut);
                string text = (i + 1).ToString(CultureInfo.InvariantCulture) + "   " + FocusModeRules.Name(mode).ToUpperInvariant() + "\n" + string.Join("\n", lines);
                var row = new Rect(left + 10f, top + 32f + i * (rowHeight + 6f), width - 20f, rowHeight);
                if (GUI.Button(row, text, _choiceStyle))
                {
                    PickFocusChoice(i);
                    return;
                }
                DrawGaugeSketch(mode, new Rect(row.xMax - gaugeWidth - 12f, row.y + 10f, gaugeWidth, gaugeHeight));
            }
            GUI.Label(new Rect(left + 12f, top + height - 22f, width - 24f, 20f), "The game is paused.   F7 leaves the run.");
        }

        /// <summary>
        /// The gauge as it will look: Hair Trigger's three parts with gaps between, two full and the third filling;
        /// Steady Hand's one bar in the window's colour, the window's seconds on it.
        /// </summary>
        void DrawGaugeSketch(FocusMode mode, Rect r)
        {
            Color before = GUI.color;
            if (mode == FocusMode.HairTrigger)
            {
                int parts = Mathf.Max(1, FocusValues.Parts);
                const float gap = 4f;
                float w = (r.width - gap * (parts - 1)) / parts;
                for (int p = 0; p < parts; p++)
                {
                    var part = new Rect(r.x + p * (w + gap), r.y, w, r.height);
                    GUI.color = s_gaugeBack;
                    GUI.DrawTexture(part, Texture2D.whiteTexture);
                    float fill = p < parts - 1 ? 1f : 0.4f;
                    GUI.color = s_gaugeFocus;
                    GUI.DrawTexture(new Rect(part.x, part.y, part.width * fill, part.height), Texture2D.whiteTexture);
                }
            }
            else
            {
                GUI.color = s_gaugeBack;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = s_gaugeWindow;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * 0.7f, r.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x, r.yMax, r.width, 16f),
                          string.Format(CultureInfo.InvariantCulture, "{0:0} s window", FocusValues.WindowSeconds));
            }
            GUI.color = before;
        }
    }
}
