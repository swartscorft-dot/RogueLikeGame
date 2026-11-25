using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The card of the boss's entrance (work order 130): while the camera holds on the boss, the boss's name large on a slanted plate
    /// in the interface's style (the start menu's name plate: black in the heavy outline with a white stroke, halftone dots toward its
    /// right end, a teal rule under the name, the red double slash after it; Barlow Condensed, the heavy italic in capitals) and under
    /// it one dry line in the lighter italic, small. It cuts in from the left and out again by <see cref="GeneratedLevels.EntranceCardShows"/>,
    /// at full strength while it shows, as the interface's pieces do. Laid out in layout pixels of a view 1080 high
    /// (<see cref="BossEntranceRules.NamePlate"/>, <see cref="BossEntranceRules.LineBox"/>). Nothing is made in a frame: the words'
    /// contents are set only when they change. A picture only; put on <see cref="GeneratedLevels"/> by its entrance.
    /// </summary>
    public sealed class BossEntranceCard : MonoBehaviour
    {
        /// <summary>Layout pixels the card stands to the left of its place as it begins to show (it cuts in to its place).</summary>
        const float CutIn = 120f;

        [System.NonSerialized] public GeneratedLevels Levels;

        readonly GUIContent _name = new GUIContent();
        readonly GUIContent _line = new GUIContent();
        string _nameText, _lineText;
        float _lineWidth;

        /// <summary>True while it draws (for tests and pictures).</summary>
        public bool Showing => Levels != null && Levels.EntranceCardShows > 0f;

        void OnGUI()
        {
            if (!Showing || Event.current.type != EventType.Repaint) return;
            GUI.depth = InterfaceDepth.Offer; // over the fight's pieces, under the screens
            InterfaceValues v = InterfaceStyle.V;
            BossEntranceValues e = Levels.Entrance;
            if (!ReferenceEquals(_nameText, Levels.EntranceName))
            {
                _nameText = Levels.EntranceName;
                _name.text = _nameText;
            }
            GUIStyle lineStyle = InterfaceStyle.Text(false, e.LineSize, TextAnchor.MiddleCenter);
            if (!ReferenceEquals(_lineText, Levels.EntranceLine))
            {
                _lineText = Levels.EntranceLine;
                _line.text = _lineText;
                _lineWidth = lineStyle.CalcSize(_line).x;
            }
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float shift = -(1f - Mathf.Clamp01(Levels.EntranceCardShows)) * CutIn;

            UiBox p = BossEntranceRules.NamePlate(viewWidth, e);
            var plate = new Rect(p.X + shift, p.Y, p.W, p.H);
            float pivot = plate.center.y, slant = e.CardSlant;
            InterfaceStyle.InkedPlate(plate, pivot, slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White));
            InterfaceStyle.Leaning(plate, pivot, slant, InterfaceStyle.C(v.Black, 0.94f));
            InterfaceStyle.DotsAlong(new Rect(plate.x + plate.width * 0.6f, plate.y, plate.width * 0.4f, plate.height), pivot, slant, 0.1f, InterfaceStyle.C(v.Teal, 0.22f));
            InterfaceStyle.Label(new Rect(plate.x + e.NamePad, plate.y - 4f, plate.width - e.NamePad, plate.height - 16f), _name,
                                 InterfaceStyle.Text(true, e.NameSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Slanted(new Rect(plate.x + e.NamePad, plate.yMax - 22f, plate.width * 0.5f, 7f), InterfaceStyle.C(v.Teal));
            InterfaceStyle.Slashes(plate.xMax + plate.height * slant * 0.5f + 18f, plate.y + 24f, plate.height - 48f, 9f, 2, InterfaceStyle.C(v.Red));

            UiBox l = BossEntranceRules.LineBox(viewWidth, e);
            float backWidth = Mathf.Min(l.W, _lineWidth + 48f);
            var back = new Rect(l.X + (l.W - backWidth) * 0.5f - shift, l.Y, backWidth, l.H);
            InterfaceStyle.Slanted(back, InterfaceStyle.C(v.Back));
            InterfaceStyle.Label(new Rect(l.X - shift, l.Y, l.W, l.H), _line, lineStyle, InterfaceStyle.C(v.White), true);

            GUI.matrix = matrixBefore;
            GUI.color = colorBefore;
        }
    }
}
