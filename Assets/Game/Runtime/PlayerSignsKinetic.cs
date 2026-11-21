using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The two signs (work order 037) in the interface's kinetic skin (decision 0011; work order 045), as part of
    /// <see cref="PlayerSigns"/>: what they show and when is theirs as before, only the drawing is the skin's. The key's
    /// clean window is a thin slanted strip over its slot on the strip, its band teal and the slot framed teal while a
    /// press is clean; a full gauge glows round the focus stroke at her feet with its key on a small plate, pulsing; FOCUS
    /// and its key over her is a bare word in the heavy italic, teal; the once-a-session line is a line in the skin.
    /// </summary>
    public sealed partial class PlayerSigns
    {
        /// <summary>The key's part of the clean window over its slot on the kinetic strip (layout pixels, slanted about <paramref name="pivotY"/>).</summary>
        public static void DrawKeySignKinetic(Rect face, float pivotY, int slot)
        {
            PlayerSigns s = Current;
            if (s == null || !s.KeySignShown(slot)) return;
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            float k = InterfaceRules.Strip(v); // work order 063: grown with the strip
            var strip = new Rect(face.x, face.y - (v.RecastHeight + 3f + StripHeight + 2f) * k, face.width, StripHeight * 0.8f * k);
            InterfaceStyle.Slanted(strip, pivotY, InterfaceStyle.C(v.Back));
            InterfaceStyle.Slanted(new Rect(strip.x + strip.width * Mathf.Clamp01(s.KeyBandFrom), strip.y, strip.width * Mathf.Clamp01(s.KeyBand), strip.height), pivotY,
                                   s.BandLit ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Teal, 0.4f));
            float x = strip.x + strip.width * Mathf.Clamp01(s.KeyMark);
            InterfaceStyle.Slanted(new Rect(x - 1.5f * k, strip.y - 2f * k, 3f * k, strip.height + 4f * k), pivotY, InterfaceStyle.C(v.White));
            if (s.BandLit) InterfaceStyle.SlantedFrame(new Rect(face.x - 2f, face.y - 2f, face.width + 4f, face.height + 4f), pivotY, InterfaceStyle.C(v.Teal), 2f);
            GUI.color = before;
        }

        /// <summary>The gauge's part of a full focus round the focus stroke at her feet: a teal glow pulsing, and its key on a small plate under the stroke's end (over it until work order 063).</summary>
        public static void DrawFocusSignKinetic(Rect stroke)
        {
            PlayerSigns s = Current;
            if (s == null || !s.GaugeSignShown) return;
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            float a = Mathf.Clamp01(s.GaugeAlpha);
            float g = v.GlowSize * 2f * InterfaceRules.Fight(v); // work order 063: grown with the master scale
            int key = InterfaceRules.KeyFont(v);
            InterfaceStyle.SlantedFrame(new Rect(stroke.x - g, stroke.y - g, stroke.width + 2f * g, stroke.height + 2f * g), InterfaceStyle.C(v.Teal, a), v.GlowSize * InterfaceRules.Fight(v));
            if (s._focusLabel.Length > 0)
            {
                float h = key * 1.2f, w = Mathf.Max(h * 1.2f, key * 0.7f * s._focusLabel.Length + 6f);
                // Work order 063: under the stroke's end, not over it, so nothing stands between the strokes and her feet.
                var plate = new Rect(stroke.xMax - w, stroke.yMax + g + 2f, w, h);
                InterfaceStyle.Slanted(plate, InterfaceStyle.C(v.Teal, Mathf.Lerp(0.6f, 1f, a)));
                InterfaceStyle.Label(plate, s._focusLabel, InterfaceStyle.Text(true, Mathf.Max(1, key - 1), TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
            }
            GUI.color = before;
        }

        /// <summary>FOCUS and its key over her, and the line, in the skin. Called with the matrix of the old layout set; it sets its own.</summary>
        void DrawKinetic(bool word, bool line)
        {
            InterfaceValues v = InterfaceStyle.V;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            if (word && Camera.main is Camera cam)
            {
                FeedbackValues look = s_Look;
                float life = Mathf.Max(0.05f, Values.WordSeconds);
                float alpha = FeedbackRules.Alpha(WordAge, life, look.FadeFrom);
                Vector3 p = cam.WorldToScreenPoint(_player.Position + Vector3.up * _wordHeight);
                if (alpha > 0.001f && p.z > 0f)
                {
                    float rise = look.WordRise * 1.5f * Mathf.Clamp01(WordAge / life);
                    float x = p.x / scale, y = (Screen.height - p.y) / scale - rise;
                    int size = Mathf.Max(8, Mathf.RoundToInt(v.WordSize * FeedbackRules.PopScale(WordAge, look.Pop, look.PopSeconds)));
                    GUIStyle style = InterfaceStyle.Text(true, size, TextAnchor.MiddleCenter);
                    Vector2 m = InterfaceStyle.Measure(WordText, style);
                    InterfaceStyle.Label(new Rect(x - m.x * 0.5f, y - m.y * 0.5f, m.x, m.y), WordText, style, InterfaceStyle.C(v.Teal, alpha), true);
                }
            }
            if (line) InterfaceStyle.Line(viewWidth * 0.5f, LineTop * v.LayoutHeight / SkillBar.LayoutHeight, CleanWindowSign.Line);
        }
    }
}
