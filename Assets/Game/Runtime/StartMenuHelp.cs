using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The start menu's help (<see cref="MenuHelpValues"/>; a friend, 2026-11-25: "man må selecte en character, men hvis man bare har
    /// et valg så kunne kanskje den bare være valgt? UI sier ikke noe om hva man må gjøre når man prøver å starte spill"), as part
    /// of <see cref="StartMenu"/>. What was found: on the character screen the mouse's passing over the row of slots moved the
    /// choice onto a dark slot (RUN LOADOUT lies to the right of the row, so the way there crosses it), and RUN LOADOUT then went
    /// on with the slot under the cursor, so it refused with a small line high over the picture; the keys' line said only "&lt; &gt;
    /// choose", never Enter; her slot looked the same chosen or not; on the loadout the line of keys named Space (the start) last.
    ///
    /// With the help on: her slot is the chosen one (the tag CHOSEN over it) and RUN LOADOUT, Enter on her slot and a click on her
    /// name plate go on with her wherever the cursor stands; a dark slot chosen by Enter or a click answers with a plain line on a
    /// plate over the row and the cursor comes back to her; the character screen's and the loadout's lines of keys say first, in
    /// plain words, what goes on and what starts the run. The screen and its dark slots stay (characters to come).
    /// </summary>
    public sealed partial class StartMenu
    {
        static string[] s_CharacterHintHelp;
        static readonly bool[] s_CharacterHintHelpCap = { true, false, true, true, false };
        static readonly string[] s_LoadoutHintHelp = { "Space", "start the run", "Up", "Down", "choose", "Enter", "put on, take off (on START: start)", "<", ">", "the skill struck out", "Esc", "back" };
        static readonly bool[] s_LoadoutHintHelpCap = { true, false, true, true, false, true, false, true, true, false, true, false };

        /// <summary>The character screen's line of keys with the help: "Enter  go on with the Gunslinger   &lt; &gt;  look along the row".</summary>
        static string[] CharacterHintHelp =>
            s_CharacterHintHelp ?? (s_CharacterHintHelp = new[] { "Enter", "go on with the " + MenuHelpRules.Title(S.Name), "<", ">", "look along the row" });

        /// <summary>The line of keys the screen shows now, word by word (for tests).</summary>
        public System.Collections.Generic.IReadOnlyList<string> HintWords
        {
            get
            {
                LayOut();
                return _hint;
            }
        }

        /// <summary>The mouse over a point of the 1080 layout, as a real mouse's move does (for tests).</summary>
        public void HoverForTests(Vector2 point) => Hover(point);

        /// <summary>RUN LOADOUT (and a click on her name plate): with the help on, on with her wherever the cursor stands; off, the slot under the cursor as before.</summary>
        bool GoOn() => ChooseSlot(MenuHelpValues.On ? StartMenuFlow.HerSlot : Selected);

        /// <summary>Her name plate as a place to click (the help on): it goes on as Enter on her slot does.</summary>
        Rect NamePlateHit(StartMenuValues s) => new Rect(Mathf.Max(0f, s.PlateLeft), s.PlateTop, s.PlateWidth + Mathf.Min(0f, s.PlateLeft), s.PlateHeight);

        /// <summary>The refused dark slot's line on a plate over the row of slots, red-edged (the help on).</summary>
        void DrawRefusedSlot(Rect panel, InterfaceValues v)
        {
            GUIStyle style = InterfaceStyle.Text(true, MenuHelpValues.RefusedSlotSize, TextAnchor.MiddleCenter);
            float w = InterfaceStyle.Measure(Note, style).x + 80f;
            var r = new Rect(panel.center.x - w * 0.5f, panel.y - MenuHelpValues.RefusedSlotAbove, w, MenuHelpValues.RefusedSlotHeight);
            InterfaceStyle.InkedPlate(r, r.center.y, v.Slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.Red));
            InterfaceStyle.Leaning(r, r.center.y, v.Slant, InterfaceStyle.C(v.Black, 0.94f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, 6f, r.height), r.center.y, v.Slant, InterfaceStyle.C(v.Red));
            Label(r, Note, style, InterfaceStyle.C(v.White), true);
        }

        /// <summary>The tag CHOSEN over her slot (the help on).</summary>
        static void DrawChosen(Rect slot, InterfaceValues v)
        {
            GUIStyle style = InterfaceStyle.Text(true, MenuHelpValues.ChosenSize, TextAnchor.MiddleCenter);
            var r = new Rect(slot.x, slot.yMax - 24f, slot.width, 24f); // across her portrait's foot (the line of keys stands over the row)
            InterfaceStyle.Slanted(r, InterfaceStyle.C(v.Teal));
            Label(r, MenuHelpValues.ChosenWord, style, InterfaceStyle.C(v.Black), false);
        }

        /// <summary>What of the help's words does not fit (the refused line inside the view).</summary>
        void HelpTextOutside(System.Collections.Generic.List<string> problems)
        {
            if (!MenuHelpValues.On || Screen != MenuScreen.Character) return;
            float w = InterfaceStyle.Measure(MenuHelpRules.RefusedSlot(S.Name), InterfaceStyle.Text(true, MenuHelpValues.RefusedSlotSize, TextAnchor.MiddleCenter)).x + 80f;
            if (w > _viewWidth - 2f * S.EchoesMargin) problems.Add("the refused slot's line is wider than the view (" + w.ToString("0") + ")");
        }
    }
}
