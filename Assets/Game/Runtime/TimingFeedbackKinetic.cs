using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The words and the damage numbers (work order 019) in the interface's kinetic skin (decision 0011, the type sheet
    /// "but calmer", Kristian's words of 2026-11-20; work order 045), as part of <see cref="TimingFeedback"/>. What shows,
    /// where and for how long is the feedback's as before; only the drawing is the skin's: every word and number in the
    /// heavy condensed italic with one dark copy under it; a slanted plate behind a word only for PERFECT (teal) and
    /// COUNTER (white), the word on it black; CLEAN, EARLY, LATE, DUCKED, STUNNED, PULLED and the rest bare in their
    /// colours (<see cref="InterfaceRules.WordInk"/>); the numbers smaller than on the sheet and never on a plate: an
    /// ordinary hit white and small, a strong one teal and a little larger, a perfect hit white over a small red slash, what
    /// she takes red (<see cref="InterfaceRules.NumberInk"/>).
    /// </summary>
    public sealed partial class TimingFeedback
    {
        /// <summary>True when the words and numbers are drawn in the kinetic skin.</summary>
        public bool Kinetic => InterfaceStyle.Kinetic;

        /// <summary>The words' and numbers' rise and their numbers' places aside, in the old layout's 720 pixels made 1080.</summary>
        const float OldToLayout = 1.5f;

        void DrawKinetic(Camera cam)
        {
            GUI.depth = 1; // under the skill bar, the HUD and its screens
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            for (int i = 0; i < _shownWords.Count; i++)
            {
                FeedbackShown w = _shownWords[i];
                DrawKineticOne(cam, scale, v, w, 0f, Values.WordRise * OldToLayout * Mathf.Clamp01(w.Age / Values.WordSeconds));
            }
            for (int i = 0; i < _shownNumbers.Count; i++)
            {
                FeedbackShown n = _shownNumbers[i];
                float side = n.Kind == NumberKind.Heal ? -HerNumberSide : n.OverHer ? HerNumberSide : NumberSide + (n.Id % 3) * 10f;
                DrawKineticOne(cam, scale, v, n, side * OldToLayout, Values.NumberRise * OldToLayout * Mathf.Clamp01(n.Age / Values.NumberSeconds));
            }
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void DrawKineticOne(Camera cam, float scale, InterfaceValues v, FeedbackShown s, float side, float rise)
        {
            if (s.Alpha <= 0.001f || string.IsNullOrEmpty(s.Text)) return;
            Vector3 p = cam.WorldToScreenPoint(s.World);
            if (p.z <= 0f) return;
            float x = p.x / scale + side, y = (Screen.height - p.y) / scale - rise;
            int baseSize = s.IsWord ? InterfaceRules.WordSize(s.Size, v) : InterfaceRules.NumberSize(s.Kind, v);
            int size = Mathf.Max(8, Mathf.RoundToInt(baseSize * FeedbackRules.PopScale(s.Age, Values.Pop, Values.PopSeconds)));
            GUIStyle style = InterfaceStyle.Text(true, size, TextAnchor.MiddleCenter);
            Vector2 m = InterfaceStyle.Measure(s.Text, style);
            var r = new Rect(x - m.x * 0.5f, y - m.y * 0.5f, m.x, m.y);
            if (s.IsWord)
            {
                if (InterfaceRules.HasPlate(s.Moment))
                {
                    float padX = v.PlatePadX * size / (float)v.WordSize, padY = v.PlatePadY;
                    var plate = new Rect(r.x - padX, r.y + m.y * 0.12f - padY, r.width + 2f * padX, m.y * 0.8f + 2f * padY);
                    InterfaceStyle.Slanted(Offset(plate, v.ShadowOffset), InterfaceStyle.C(v.Shadow, s.Alpha * 0.6f));
                    Color plateInk = InterfaceStyle.Ink(InterfaceRules.PlateInk(s.Moment));
                    InterfaceStyle.Slanted(plate, new Color(plateInk.r, plateInk.g, plateInk.b, s.Alpha));
                    Color ink = InterfaceStyle.Ink(UiInk.Black);
                    InterfaceStyle.Label(r, s.Text, style, new Color(ink.r, ink.g, ink.b, s.Alpha));
                    return;
                }
                Color word = InterfaceStyle.Ink(InterfaceRules.WordInk(s.Moment, s.Tint == FeedbackTint.MarksFull));
                InterfaceStyle.Label(r, s.Text, style, new Color(word.r, word.g, word.b, s.Alpha), true);
                return;
            }
            if (InterfaceRules.NumberSlash(s.Kind))
            {
                // A small red slash behind it, leaning hard: a cut, not a plate.
                float h = m.y * v.SlashShare, t = v.SlashThickness * size / (float)v.StrongNumberSize;
                var slash = new Rect(r.center.x - t * 0.5f, r.center.y - h * 0.5f, t, h);
                Matrix4x4 b = GUI.matrix;
                GUI.matrix = b * InterfaceStyle.Shear(slash.center.y, 0.9f);
                Color red = InterfaceStyle.C(v.Red);
                GUI.color = new Color(red.r, red.g, red.b, s.Alpha);
                GUI.DrawTexture(slash, Texture2D.whiteTexture);
                GUI.matrix = b;
            }
            Color c = InterfaceStyle.Ink(InterfaceRules.NumberInk(s.Kind));
            InterfaceStyle.Label(r, s.Text, style, new Color(c.r, c.g, c.b, s.Alpha), true);
        }

        static Rect Offset(Rect r, float by) => new Rect(r.x + by, r.y + by, r.width, r.height);
    }
}
