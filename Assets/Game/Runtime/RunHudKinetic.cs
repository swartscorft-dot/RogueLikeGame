using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The HUD and its screens in the interface's kinetic skin (decision 0011; work order 045), as part of
    /// <see cref="RunHud"/>. What they show and when is theirs as before; plain and legible first:
    /// - her health bar is not drawn (her health is the stroke at her feet, <see cref="SkillBar"/>);
    /// - the run box at the top right is its lines without a box, right aligned, the floor in the heavy italic over a thin
    ///   teal rule, the others in the lighter italic, the seed small and grey;
    /// - the practice room's line is a prompt in the skin, its key on a small white plate;
    /// - the pause menu, the end screens and the screen between runs keep their layout and their lines, in the skin's type
    ///   and palette: a flat dark panel with no frame, the heading in the heavy italic over a thin rule (teal, red for "You
    ///   died"), flat buttons in the lighter italic that light teal under the pointer.
    /// The red edge when she is hurt is as before.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>Her health bar of before is drawn now (the skin off, and no skill bar to show it).</summary>
        public bool HealthBarDrawn { get; private set; }
        /// <summary>True when the HUD and its screens are drawn in the kinetic skin.</summary>
        public bool Kinetic => InterfaceStyle.Kinetic;

        GUIStyle _plainBox, _plainLabel, _plainSmall, _plainBig, _plainTitle, _plainButton, _plainKeys, _plainCenter, _plainCenterBold, _plainPauseTitle, _plainPiece, _plainPieceOn;
        GUIStyle _kLabel, _kSmall, _kBig, _kTitle, _kButton, _kKeys, _kCenter, _kCenterBold, _kPauseTitle, _kPiece, _kPieceOn;
        bool _kineticStyles;
        string _promptKey = "", _promptWords = "", _promptFrom;

        /// <summary>Puts the style set of the skin, or the plain one, in the fields the drawing reads. True for the skin.</summary>
        bool UseStyles()
        {
            if (_plainBox == null)
            {
                _plainBox = _box; _plainLabel = _label; _plainSmall = _small; _plainBig = _big; _plainTitle = _title; _plainButton = _button;
                _plainKeys = _keys; _plainCenter = _center; _plainCenterBold = _centerBold; _plainPauseTitle = _pauseTitle;
                // The screen between runs' buttons (RunHudBetweenRuns.cs), made here as it made them, so both sets exist.
                _plainPiece = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 4, 4) };
                _plainPieceOn = new GUIStyle(_plainPiece) { fontStyle = FontStyle.Bold };
                _plainPieceOn.normal.textColor = s_Accent;
            }
            bool kinetic = InterfaceStyle.Kinetic;
            if (kinetic && _kLabel == null) MakeKineticStyles();
            if (kinetic == _kineticStyles && _pieceButton != null) return kinetic;
            _kineticStyles = kinetic;
            if (kinetic)
            {
                _label = _kLabel; _small = _kSmall; _big = _kBig; _title = _kTitle; _button = _kButton; _keys = _kKeys;
                _center = _kCenter; _centerBold = _kCenterBold; _pauseTitle = _kPauseTitle; _pieceButton = _kPiece; _pieceOn = _kPieceOn;
            }
            else
            {
                _box = _plainBox; _label = _plainLabel; _small = _plainSmall; _big = _plainBig; _title = _plainTitle; _button = _plainButton;
                _keys = _plainKeys; _center = _plainCenter; _centerBold = _plainCenterBold; _pauseTitle = _plainPauseTitle;
                _pieceButton = _plainPiece; _pieceOn = _plainPieceOn;
            }
            return kinetic;
        }

        /// <summary>A size of the skin's 1080 layout in the HUD's 720 one.</summary>
        static int In720(int size) => Mathf.Max(6, Mathf.RoundToInt(size * SkillBar.LayoutHeight / InterfaceStyle.V.LayoutHeight));

        void MakeKineticStyles()
        {
            InterfaceValues v = InterfaceStyle.V;
            Font light = InterfaceStyle.Light, heavy = InterfaceStyle.Heavy;
            _kLabel = new GUIStyle(_plainLabel) { font = light, fontSize = In720(v.TextSize) };
            _kLabel.normal.textColor = Color.white;
            _kSmall = new GUIStyle(_kLabel) { fontSize = In720(v.SmallTextSize) };
            _kSmall.normal.textColor = InterfaceStyle.C(v.Grey);
            _kBig = new GUIStyle(_kLabel) { font = heavy, fontSize = In720(v.RunFloorSize) + 2, fontStyle = FontStyle.Normal };
            _kBig.normal.textColor = InterfaceStyle.C(v.Teal);
            _kTitle = new GUIStyle(_kLabel) { font = heavy, fontSize = In720(v.HeadingSize), fontStyle = FontStyle.Normal, alignment = TextAnchor.MiddleCenter };
            _kKeys = new GUIStyle(_kLabel) { fontSize = In720(v.SmallTextSize), wordWrap = false };
            _kKeys.normal.textColor = InterfaceStyle.C(v.Grey);
            _kCenter = new GUIStyle(_kLabel) { alignment = TextAnchor.MiddleCenter };
            _kCenterBold = new GUIStyle(_kCenter) { font = heavy, fontStyle = FontStyle.Normal };
            _kPauseTitle = new GUIStyle(_kTitle) { fontSize = In720(v.HeadingSize) - 4 };
            _kPauseTitle.normal.textColor = InterfaceStyle.C(v.Teal);
            _kButton = new GUIStyle(InterfaceStyle.Button(In720(v.ButtonTextSize)));
            _kPiece = new GUIStyle(_kButton) { alignment = TextAnchor.UpperLeft, padding = new RectOffset(10, 10, 4, 4), fontSize = In720(v.ButtonTextSize) - 2 };
            _kPieceOn = new GUIStyle(_kPiece) { font = heavy };
            _kPieceOn.normal.textColor = _kPieceOn.hover.textColor = InterfaceStyle.C(v.Teal);
        }

        /// <summary>A screen's panel: the dark box and its frame as before; in the skin, a flat darker panel and one thin rule under the heading.</summary>
        void Panel(Rect r, Color accent, float ruleY)
        {
            if (!_kineticStyles)
            {
                Fill(r, s_Back);
                Frame(r, accent);
                return;
            }
            InterfaceValues v = InterfaceStyle.V;
            Fill(r, InterfaceStyle.C(v.Panel));
            Color rule = accent == s_Health ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.Teal);
            InterfaceStyle.Slanted(new Rect(r.x + r.width * 0.2f, ruleY, r.width * 0.6f, Mathf.Max(1f, v.RuleThickness * 0.67f)), rule);
        }

        /// <summary>The run box in the skin: its lines right aligned at the top right, no box; the floor heavy over a teal rule, the seed small.</summary>
        void DrawRunBoxKinetic(float viewWidth)
        {
            InterfaceValues v = InterfaceStyle.V;
            float right = viewWidth - 12f, w = RunBoxWidth;
            var head = new Rect(right - w, 8f, w, 26f);
            GUI.Label(head, _runBox[0], RightAligned(_big));
            InterfaceStyle.Slanted(new Rect(right - w * 0.55f, head.yMax + 1f, w * 0.55f, 1.5f), InterfaceStyle.C(v.Teal));
            float y = head.yMax + 5f;
            GUIStyle line = RightAligned(_label);
            for (int i = 1; i < _runBox.Count - 1; i++)
            {
                InterfaceStyle.Label(new Rect(right - w, y, w, 17f), _runBox[i], line, Color.white, true);
                y += 17f;
            }
            GUI.color = Color.white;
            GUI.Label(new Rect(right - w, y + 1f, w, 14f), _runBox[_runBox.Count - 1], RightAligned(_small));
        }

        GUIStyle _rightBig, _rightLabel, _rightSmall;

        GUIStyle RightAligned(GUIStyle style)
        {
            if (style == _big) return _rightBig ?? (_rightBig = new GUIStyle(_big) { alignment = TextAnchor.MiddleRight, wordWrap = false });
            if (style == _small) return _rightSmall ?? (_rightSmall = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight, wordWrap = false });
            return _rightLabel ?? (_rightLabel = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, wordWrap = false });
        }

        /// <summary>The practice room's line as a prompt in the skin: "F5" on its plate, "start a run" after it.</summary>
        void DrawPromptKinetic(float viewWidth)
        {
            if (!ReferenceEquals(_promptFrom, PromptText))
            {
                _promptFrom = PromptText;
                int gap = PromptText.IndexOf("  ", System.StringComparison.Ordinal);
                _promptKey = gap > 0 ? PromptText.Substring(0, gap) : "";
                _promptWords = gap > 0 ? PromptText.Substring(gap).Trim() : PromptText;
            }
            Matrix4x4 before = GUI.matrix;
            float layoutWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            float k = InterfaceStyle.V.LayoutHeight / SkillBar.LayoutHeight;
            InterfaceStyle.Prompt(layoutWidth * 0.5f, (HealthTop + 13f) * k, _promptKey, _promptWords);
            GUI.matrix = before;
        }
    }
}
