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
        /// <summary>
        /// The IMGUI depth it draws at (work order 068): a screen's (the pause menu, an end screen, the screen between runs)
        /// while one is up, over an offer and everything else; else the fight's, under an offer (its run box, its prompt).
        /// </summary>
        public int GuiDepth => InterfaceDepth.Hud(_levels != null && (_levels.EndScreenShown || _levels.Paused || _levels.BetweenRunsUp));

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
            // Work order 068 (the concept's plates): a heavy black outline and its dark copy, a teal cut down its left edge, a red
            // double slash at its top right and halftone dots fading in under it. (The HUD's 720 layout: the 1080 values made 720.)
            float k = SkillBar.LayoutHeight / v.LayoutHeight, o = v.HeavyOutline * k;
            Fill(new Rect(r.x - o + v.Depth * k * 2f, r.y - o + v.Depth * k * 2f, r.width + 2f * o, r.height + 2f * o), InterfaceStyle.C(v.Black, 0.55f));
            Fill(new Rect(r.x - o, r.y - o, r.width + 2f * o, r.height + 2f * o), InterfaceStyle.C(v.Black));
            Fill(r, InterfaceStyle.C(v.Panel));
            InterfaceStyle.DotsAlong(new Rect(r.x + r.width * 0.55f, r.y, r.width * 0.45f, Mathf.Min(70f, r.height * 0.3f)), r.y, 0f, 0.05f, InterfaceStyle.C(v.Teal, 0.18f));
            Fill(new Rect(r.x, r.y, 4f, r.height), accent == s_Health ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.Teal));
            InterfaceStyle.Slashes(r.xMax - 12f, r.y + 10f, 26f, 5f, 2, InterfaceStyle.C(v.Red));
            Color rule = accent == s_Health ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.Teal);
            InterfaceStyle.Slanted(new Rect(r.x + r.width * 0.2f, ruleY, r.width * 0.6f, Mathf.Max(1f, v.RuleThickness * 0.67f)), rule);
        }

        /// <summary>The run box in the skin: its lines right aligned at the top right, no box; the floor heavy over a teal rule, the seed small.</summary>
        void DrawRunBoxKinetic(float viewWidth)
        {
            InterfaceValues v = InterfaceStyle.V;
            // Work order 063: grown with the master scale of a fight times its own factor (1 in all by default: the small map
            // stands beside it), about the view's top right corner.
            Matrix4x4 before = GUI.matrix;
            float k = RunBoxFactor(v);
            if (Mathf.Abs(k - 1f) > 1e-4f)
                GUI.matrix = before * Matrix4x4.Translate(new Vector3(viewWidth, 0f, 0f)) * Matrix4x4.Scale(new Vector3(k, k, 1f)) * Matrix4x4.Translate(new Vector3(-viewWidth, 0f, 0f));
            DrawRunBoxLines(viewWidth, v);
            GUI.matrix = before;
        }

        /// <summary>
        /// The run box's height in the HUD's 720 layout with <paramref name="lines"/> lines, as <see cref="DrawRunBoxLines"/>
        /// draws them: all on the plate (work order 063), or since the morning of 2026-11-22 the head and
        /// <see cref="InterfaceValues.RunPlateLines"/> on a compact plate and the rest smaller under it.
        /// </summary>
        static float RunBoxHeight(int lines, InterfaceValues v)
        {
            int middle = Mathf.Max(0, lines - 2), onPlate = Mathf.Min(middle, Mathf.Max(0, v.RunPlateLines));
            if (onPlate >= middle) return 8f + 26f + 5f + middle * 17f + 15f;
            float k = SkillBar.LayoutHeight / v.LayoutHeight, o = v.HeavyOutline * k, t = Mathf.Max(0f, v.OuterStroke) * k;
            return 3f + 8f + 26f + 5f + onPlate * 17f + 6f + o + t + 3f + (middle - onPlate) * 14f + 14f + 2f;
        }

        /// <summary>The run box's place in the HUD's 720 layout with <paramref name="lines"/> lines, grown about the top right corner (work order 063): for the overlap checks.</summary>
        public static Rect RunBoxKinetic(float viewWidth, int lines)
        {
            float k = RunBoxFactor(InterfaceStyle.V);
            float h = RunBoxHeight(lines, InterfaceStyle.V);
            float w = RunBoxWidth + 12f;
            return new Rect(viewWidth - w * k, 0f, w * k, h * k);
        }

        /// <summary>The run box's scale: the master scale times its own (<see cref="InterfaceValues.RunBoxScale"/>).</summary>
        static float RunBoxFactor(InterfaceValues v) => InterfaceRules.Fight(v) * Mathf.Max(0.1f, v.RunBoxScale);

        void DrawRunBoxLines(float viewWidth, InterfaceValues v)
        {
            float right = viewWidth - 12f, w = RunBoxWidth;
            // Work order 068 (the concept's top right): the lines on a dark slanted plate in the heavy black outline, a teal cut
            // down its left edge; inside the run box's own width, clear of the map beside it.
            // The morning of 2026-11-22 (the producer, from round 4: "a large black area with small thin text"): the plate is
            // compact, behind the head and RunPlateLines lines only, a white line outside its black outline; the lesser lines
            // after it smaller, on no plate.
            float k = SkillBar.LayoutHeight / v.LayoutHeight, o = v.HeavyOutline * k, t = Mathf.Max(0f, v.OuterStroke) * k;
            int middle = Mathf.Max(0, _runBox.Count - 2), onPlate = Mathf.Min(middle, Mathf.Max(0, v.RunPlateLines));
            var plate = new Rect(right - w + 4f, 3f, w + 30f, 8f + 26f + 5f + onPlate * 17f + (onPlate < middle ? 6f : 15f));
            if (t > 0f) InterfaceStyle.Slanted(new Rect(plate.x - o - t, plate.y - o - t, plate.width + 2f * (o + t), plate.height + 2f * (o + t)), InterfaceStyle.C(v.White));
            InterfaceStyle.Slanted(new Rect(plate.x - o, plate.y - o, plate.width + 2f * o, plate.height + 2f * o), InterfaceStyle.C(v.Black));
            InterfaceStyle.Slanted(plate, InterfaceStyle.C(v.Black, 0.72f));
            InterfaceStyle.Slanted(new Rect(plate.x, plate.y, 3f, plate.height), InterfaceStyle.C(v.Teal));
            var head = new Rect(right - w, 8f, w, 26f);
            GUI.Label(head, _runBox[0], RightAligned(_big));
            InterfaceStyle.Slanted(new Rect(right - w * 0.55f, head.yMax + 1f, w * 0.55f, 1.5f), InterfaceStyle.C(v.Teal));
            float y = head.yMax + 5f;
            GUIStyle line = RightAligned(_label), small = RightAligned(_small);
            for (int i = 1; i < _runBox.Count - 1; i++)
            {
                bool lesser = i > onPlate;
                if (lesser && i == onPlate + 1) y = plate.yMax + o + t + 3f;
                float h = lesser ? 14f : 17f;
                InterfaceStyle.Label(new Rect(right - w, y, w, h), _runBox[i], lesser ? small : line, lesser ? new Color(1f, 1f, 1f, 0.85f) : Color.white, true);
                y += h;
            }
            if (onPlate >= middle) y += 1f;
            InterfaceStyle.Label(new Rect(right - w, y, w, 14f), _runBox[_runBox.Count - 1], small, new Color(1f, 1f, 1f, 0.6f), true);
        }

        GUIStyle _rightBig, _rightLabel, _rightSmall;

        GUIStyle RightAligned(GUIStyle style)
        {
            // Work order 068 (round 4's picture: "Upgrades" read "Uparades", the g's tail cut at its 17-pixel line): not clipped.
            if (style == _big) return _rightBig ?? (_rightBig = new GUIStyle(_big) { alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow });
            if (style == _small) return _rightSmall ?? (_rightSmall = new GUIStyle(_small) { alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow });
            return _rightLabel ?? (_rightLabel = new GUIStyle(_label) { alignment = TextAnchor.MiddleRight, wordWrap = false, clipping = TextClipping.Overflow });
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
            InterfaceValues v = InterfaceStyle.V;
            float k = v.LayoutHeight / SkillBar.LayoutHeight;
            // Work order 063: never down on the strip, which is larger and higher now: its middle a clear gap over the strip's top.
            float stripTop = InterfaceRules.Slot(0, SkillLoadout.KeyCount, layoutWidth, v).Y;
            float y = Mathf.Min((HealthTop + 13f) * k, stripTop - v.PromptSize * 1.45f * 0.5f - 24f * InterfaceRules.Fight(v));
            InterfaceStyle.Prompt(layoutWidth * 0.5f, y, _promptKey, _promptWords);
            GUI.matrix = before;
        }
    }
}
