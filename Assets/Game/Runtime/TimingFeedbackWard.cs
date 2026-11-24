using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The ward seen where she looks (work order 107, the warder that reads; Kristian's tick of 2026-11-24, "vokteren som ikke
    /// leses"), as part of <see cref="TimingFeedback"/>: the number is what she watches, so a hit on an enemy a warder that reads
    /// is warding says so on its number.
    ///
    /// - The number carries a small shield on its left in the ward's green (<see cref="WarderReadRules.InShield"/>, a texture made
    ///   once in code), and an ordinary hit's white number is green (<see cref="WarderReadRules.GreenInk"/>); a crit's and a clean
    ///   press's keep their own look under the shield. A number that summed several hits keeps the shield once one was warded.
    /// - The first warded hit of a run shows one short line by the target, "Warded: kill the warder"
    ///   (<see cref="WarderReadRules.HintText"/>), never again that run (<see cref="OncePerRun"/>, the run known by its record).
    ///   It shows whether or not damage numbers are on: it is a word, not a number.
    ///
    /// Whether a warder's ward does this is that warder's switch (<see cref="WarderValues.Reads"/> and its parts); how the mark
    /// and the line look is <see cref="WardLook"/>. Unwarded hits' numbers are drawn exactly as before. Nothing allocated.
    /// </summary>
    public sealed partial class TimingFeedback
    {
        /// <summary>How the shield on a warded number and the first time's line look (work order 107), defaults in code.</summary>
        public WarderReadValues WardLook = new WarderReadValues();

        static readonly GUIContent s_HintContent = new GUIContent(WarderReadRules.HintText);
        static Texture2D s_Shield;
        const int ShieldW = 28, ShieldH = 34, ShieldSamples = 4; // the mark's texture, white to tint, its edge smoothed

        readonly OncePerRun _hintOnce = new OncePerRun();
        Damageable _hintTarget;
        Vector3 _hintLast;
        float _hintHeight;
        float _hintAt = float.NegativeInfinity;

        /// <summary>Is the first time's line showing now?</summary>
        public bool WardHintShowing => Time.time - _hintAt < WardLook.HintSeconds;
        /// <summary>The target the line is by (or was last by), and the times it was shown, for tests.</summary>
        public Damageable WardHintTarget => _hintTarget;
        public int WardHintsShown => _hintOnce.Taken;

        /// <summary>
        /// A hit of hers landed on a target: the warder that reads whose ward is on it, or null. The first of a run whose warder
        /// tells it puts the line up by the target.
        /// </summary>
        EnemyWarder WardedHit(Damageable target)
        {
            EnemyWarder warder = EnemyWarder.ReaderWarding(target);
            if (warder == null || !WarderReadRules.Hints(warder.Tuning)) return warder;
            GeneratedLevels levels = GeneratedLevels.Current;
            object run = levels != null ? levels.Record : null;
            if (!_hintOnce.Take(run)) return warder;
            _hintTarget = target;
            _hintLast = target.transform.position;
            _hintHeight = TopOf(target.gameObject, target) + OverHead;
            _hintAt = Time.time;
            return warder;
        }

        static Color WardInk(float alpha)
        {
            Color c = EnemyWarder.WardColor;
            return new Color(c.r, c.g, c.b, alpha);
        }

        /// <summary>A number's own colour, the ward's green for a warded ordinary hit (work order 107).</summary>
        static Color NumberInkOf(FeedbackShown s, Color own) => s.Warded && WarderReadRules.GreenInk(s.Kind) ? WardInk(own.a) : own;

        /// <summary>The shield mark's texture, made once: a heater shield, white, its edge smoothed.</summary>
        static Texture2D Shield
        {
            get
            {
                if (s_Shield != null) return s_Shield;
                s_Shield = new Texture2D(ShieldW, ShieldH, TextureFormat.RGBA32, false)
                {
                    name = "WardShield", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.HideAndDontSave,
                };
                var px = new Color32[ShieldW * ShieldH];
                for (int y = 0; y < ShieldH; y++)
                    for (int x = 0; x < ShieldW; x++)
                    {
                        int inside = 0;
                        for (int sy = 0; sy < ShieldSamples; sy++)
                            for (int sx = 0; sx < ShieldSamples; sx++)
                            {
                                float u = ((x + (sx + 0.5f) / ShieldSamples) / ShieldW) * 2f - 1f;
                                float v = (y + (sy + 0.5f) / ShieldSamples) / ShieldH; // 0 at the top
                                if (WarderReadRules.InShield(u * 1.08f, v * 1.04f)) inside++;  // a hair of margin round it
                            }
                        byte a = (byte)(255 * inside / (ShieldSamples * ShieldSamples));
                        px[(ShieldH - 1 - y) * ShieldW + x] = new Color32(255, 255, 255, a); // a texture's rows run from the bottom
                    }
                s_Shield.SetPixels32(px);
                s_Shield.Apply();
                return s_Shield;
            }
        }

        /// <summary>The shield on a warded number's left: <paramref name="r"/> is the number's own rect, <paramref name="fontSize"/> its size.</summary>
        void DrawWardMark(Rect r, int fontSize, float alpha, float shadowOffset)
        {
            if (alpha <= 0.001f) return;
            float h = Mathf.Max(6f, fontSize * WardLook.MarkShare), w = h * WardLook.MarkAspect;
            var mark = new Rect(r.xMin - WardLook.MarkGap - w, r.center.y - h * 0.5f, w, h);
            Texture2D shield = Shield;
            GUI.color = new Color(0f, 0f, 0f, 0.85f * alpha);
            GUI.DrawTexture(new Rect(mark.x + shadowOffset, mark.y + shadowOffset, w, h), shield);
            GUI.color = WardInk(alpha);
            GUI.DrawTexture(mark, shield);
        }

        /// <summary>The first time's line by its target, in the skin's heavy italic, the ward's green, while it shows.</summary>
        void DrawWardHint(Camera cam)
        {
            if (!WardHintShowing || cam == null) return;
            float alpha = WarderReadRules.HintAlpha(Time.time - _hintAt, WardLook);
            if (alpha <= 0.001f) return;
            Vector3 at = _hintTarget != null ? _hintTarget.transform.position : _hintLast;
            _hintLast = at;
            Vector3 p = cam.WorldToScreenPoint(new Vector3(at.x, at.y + _hintHeight, at.z));
            if (p.z <= 0f) return;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            float x = p.x / scale, y = (InterfaceStyle.ScreenHeight - p.y) / scale + WardLook.HintBelow;
            GUIStyle style = InterfaceStyle.Text(true, Mathf.Max(8, WardLook.HintSize), TextAnchor.MiddleCenter);
            Vector2 m = style.CalcSize(s_HintContent);
            InterfaceStyle.Label(new Rect(x - m.x * 0.5f, y - m.y * 0.5f, m.x, m.y), s_HintContent, style, WardInk(alpha), true);
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }
    }
}
