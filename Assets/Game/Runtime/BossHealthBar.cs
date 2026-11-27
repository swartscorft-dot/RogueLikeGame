using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The act boss's bar on screen (work order 014, 2026-11-20): while it is awake and alive, a wide bar at the top
    /// middle with its health, a thinner bar under it with its stagger, and its name over them (a placeholder,
    /// <see cref="Rules.BossValues.Title"/>). Drawn with OnGUI and plain rectangles, as the skill bar is, laid out for a
    /// view 720 pixels high and scaled to the real one. The stagger bar flashes while it is dazed; the health bar turns
    /// warmer when it is angry. Gone when it dies, and with it when she leaves the floor (the floor takes it away).
    /// Put on the boss by itself; a picture only.
    /// </summary>
    [RequireComponent(typeof(EnemyBoss))]
    public sealed class BossHealthBar : MonoBehaviour
    {
        const float Top = 30f;
        const float MostWidth = 560f;
        const float HealthHeight = 16f;
        const float StaggerHeight = 6f;
        const float Gap = 4f;
        const float FlashPeriod = 0.3f;
        const float CornerBoxes = 350f;

        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color s_Health = new Color(0.78f, 0.12f, 0.10f);
        static readonly Color s_AngryHealth = new Color(1f, 0.42f, 0.12f);
        static readonly Color s_Stagger = new Color(1f, 0.85f, 0.45f);
        static readonly Color s_Flash = new Color(0.55f, 0.75f, 1f);

        EnemyBoss _boss;
        GUIStyle _title;

        /// <summary>True while it is drawn: the boss awake and alive. For tests.</summary>
        public bool Showing => _boss != null && _boss.isActiveAndEnabled && _boss.IsAwake && !_boss.IsDead;
        /// <summary>The shares shown, 0 to 1. For tests.</summary>
        public float HealthShown => _boss != null ? _boss.HealthShare : 0f;
        public float StaggerShown => WardShown >= 0f ? WardShown : _boss != null && _boss.Stagger != null ? _boss.Stagger.Fill : 0f;

        void Awake() => _boss = GetComponent<EnemyBoss>();

        void OnGUI()
        {
            if (!Showing || Event.current.type != EventType.Repaint) return;
            GUI.depth = Rules.InterfaceDepth.Fight; // work order 068: under an offer and every screen
            if (InterfaceStyle.Kinetic)
            {
                DrawKinetic(); // work order 045
                return;
            }
            float scale = InterfaceStyle.ScreenHeight / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            Color colorBefore = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = InterfaceStyle.ScreenWidth / scale;
            // Clear of the readout boxes in the top corners (330 wide and a margin, GeneratedLevels), on a narrow view too.
            float width = Mathf.Max(160f, Mathf.Min(MostWidth, viewWidth * 0.6f, viewWidth - 2f * CornerBoxes));
            float x = (viewWidth - width) * 0.5f;

            if (_title == null)
                _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, normal = { textColor = Color.white } };
            GUI.color = Color.white;
            GUI.Label(new Rect(x, Top - 24f, width, 22f), _boss.Tuning.Title, _title);

            Bar(new Rect(x, Top, width, HealthHeight), HealthShown, _boss.InTransition ? s_Grey : _boss.IsAngry ? s_AngryHealth : s_Health);
            if (ShowsHalves) Halves(new Rect(x, Top, width, HealthHeight)); // work order 106
            bool flash = _boss.IsDazed && Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f;
            Bar(new Rect(x, Top + HealthHeight + Gap, width, StaggerHeight), StaggerShown, WardShown >= 0f ? s_Ward : flash ? s_Flash : s_Stagger, flash);
            if (WardShown >= 0f) WardBar(new Rect(x, Top + HealthHeight + Gap + StaggerHeight + Gap * 2f, width, HealthHeight), false); // work order 147

            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        /// <summary>True when it is drawn in the interface's kinetic skin (work order 045).</summary>
        public bool Kinetic => InterfaceStyle.Kinetic;

        /// <summary>
        /// The bar in the interface's kinetic skin (decision 0011; work order 045): one long slanted bar at the top middle in
        /// the skin's red (warmer when it is angry), a thin slanted stagger line under it flashing the daze blue, and its
        /// name over it in the heavy italic.
        /// </summary>
        void DrawKinetic()
        {
            Rules.InterfaceValues v = InterfaceStyle.V;
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float width = Mathf.Max(200f, Mathf.Min(v.BossWidth, viewWidth * 0.5f));
            var bar = new Rect((viewWidth - width) * 0.5f, v.BossTop, width, v.BossHeight);
            float h = v.BossNameSize * 1.3f;
            InterfaceStyle.Label(new Rect(bar.x, bar.y - h - 2f, bar.width, h), _boss.Tuning.Title,
                                 InterfaceStyle.Text(true, v.BossNameSize, TextAnchor.LowerLeft), InterfaceStyle.C(v.White), true);
            // Work order 068: the concept's language: a heavy black outline and its dark copy, halftone dots toward its end.
            InterfaceStyle.HeavyPlate(bar, bar.center.y, v.Slant, v.HeavyOutline * 0.75f, v.Depth);
            InterfaceStyle.SlantedBar(bar, HealthShown, InterfaceStyle.C(v.Back), _boss.InTransition ? s_Grey : _boss.IsAngry ? s_AngryHealth : InterfaceStyle.C(v.Red));
            InterfaceStyle.DotsAlong(bar, bar.center.y, v.Slant, 1f - v.PlateDots, InterfaceStyle.C(v.Black, 0.45f));
            if (ShowsHalves) Halves(bar); // work order 106
            bool flash = _boss.IsDazed && Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f;
            var stagger = new Rect(bar.x - (v.BossHeight * 0.5f + 3f + v.BossStaggerHeight * 0.5f) * v.Slant, bar.yMax + 3f, bar.width, v.BossStaggerHeight);
            InterfaceStyle.SlantedBar(stagger, StaggerShown, flash ? InterfaceStyle.C(v.Dazed) : InterfaceStyle.C(v.Back),
                                      WardShown >= 0f ? s_Ward : flash ? InterfaceStyle.C(v.Dazed) : InterfaceStyle.C(v.Stagger));
            if (WardShown >= 0f) WardBar(new Rect(bar.x, stagger.yMax + 6f, bar.width, bar.height), true); // work order 147
            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        // ---------------- The two halves (work order 106, BossValues.Finale) ----------------

        /// <summary>Pixels, in the layout of a view 720 high: the width of the mark that parts its bar into its two halves.</summary>
        const float HalfMarkWidth = 3f;
        static readonly Color s_HalfMark = new Color(0f, 0f, 0f, 0.85f);

        /// <summary>True when its bar shows the two halves (work order 106): with the finale on. Off, the bar as before.</summary>
        public bool ShowsHalves => Rules.BossValues.Finale && _boss != null;
        /// <summary>The share of the bar where the halves part: where the break comes (its anger's share, a half).</summary>
        public float HalfAt => _boss != null ? _boss.Tuning.AngerBelow : 0.5f;
        /// <summary>The mark's rectangle on a health bar drawn in <paramref name="bar"/>, in the bar's own layout; for tests too.</summary>
        public Rect HalfMark(Rect bar) => new Rect(bar.x + bar.width * Mathf.Clamp01(HalfAt) - HalfMarkWidth * 0.5f, bar.y - 2f, HalfMarkWidth, bar.height + 4f);

        /// <summary>A dark upright mark across the health bar where the break comes: the fight's two halves, read before it breaks.</summary>
        void Halves(Rect bar)
        {
            Color before = GUI.color;
            GUI.color = s_HalfMark;
            if (_boss.InTrial) // work order 147: a notch at each phase's line instead, 65 % and 30 %: light round dark, seen on the full and the empty bar
            {
                Notch(Mark(bar, _boss.TrialValues.PhaseTwoBelow));
                Notch(Mark(bar, _boss.TrialValues.PhaseThreeBelow));
            }
            else GUI.DrawTexture(HalfMark(bar), Texture2D.whiteTexture);
            GUI.color = before;
        }

        // ---------------- The trial fight (work order 147) ----------------

        static readonly Color s_Grey = new Color(0.55f, 0.55f, 0.58f);  // the bar through a transition: it takes no damage
        static readonly Color s_Ward = new Color(1f, 0.82f, 0.35f);     // the ward's gold, as its closing circle on the floor

        /// <summary>A notch's rectangle at a share of a health bar drawn in <paramref name="bar"/> (for tests too).</summary>
        public Rect Mark(Rect bar, float share) => new Rect(bar.x + bar.width * Mathf.Clamp01(share) - HalfMarkWidth * 0.5f, bar.y - 2f, HalfMarkWidth, bar.height + 4f);

        static readonly Color s_NotchLight = new Color(1f, 1f, 1f, 0.9f);

        static void Notch(Rect m)
        {
            GUI.color = s_NotchLight;
            GUI.DrawTexture(new Rect(m.x - NotchRim, m.y - NotchRim, m.width + 2f * NotchRim, m.height + 2f * NotchRim), Texture2D.whiteTexture);
            GUI.color = s_HalfMark;
            GUI.DrawTexture(m, Texture2D.whiteTexture);
        }
        /// <summary>Layout px of the light rim round each notch, so it reads on the dark empty bar as on the red.</summary>
        const float NotchRim = 1.5f;
        static readonly Color s_WardBack = new Color(0.12f, 0.09f, 0.02f, 0.85f);
        static readonly Color s_WardText = new Color(0.08f, 0.06f, 0.0f);
        GUIStyle _wardStyle;
        static string[] s_WardWords; // "WARD 8.0 s" to "WARD 0.0 s", made once (nothing in a frame)

        /// <summary>The words on the ward's bar for this many seconds left (tenths, made once).</summary>
        public static string WardWords(float seconds)
        {
            if (s_WardWords == null)
            {
                s_WardWords = new string[121];
                for (int i = 0; i < s_WardWords.Length; i++)
                    s_WardWords[i] = "WARD  " + (i / 10f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture) + " s";
            }
            return s_WardWords[Mathf.Clamp(Mathf.CeilToInt(seconds * 10f), 0, s_WardWords.Length - 1)];
        }

        /// <summary>
        /// Judgement's ward on a bar of its own under the health bar (the second round of work order 147: "not a thin gold line"): the
        /// health bar's own height, gold on dark, the ward left as its fill and the seconds left to break it written on it.
        /// </summary>
        void WardBar(Rect r, bool kinetic)
        {
            if (kinetic) InterfaceStyle.SlantedBar(r, WardShown, s_WardBack, s_Ward);
            else
            {
                GUI.color = s_WardBack;
                GUI.DrawTexture(new Rect(r.x - 2f, r.y - 2f, r.width + 4f, r.height + 4f), Texture2D.whiteTexture);
                GUI.color = s_Ward;
                GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(WardShown), r.height), Texture2D.whiteTexture);
            }
            if (_wardStyle == null)
                _wardStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.Max(10, Mathf.RoundToInt(r.height * 0.8f)), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            GUI.color = Color.white;
            _wardStyle.normal.textColor = WardShown > 0.5f ? s_WardText : Color.white;
            GUI.Label(r, WardWords(_boss.WardTimeLeft), _wardStyle);
        }

        /// <summary>While Judgement's ward stands, the share of it left, shown in the stagger's place in gold; else -1.</summary>
        public float WardShown => _boss != null && _boss.InTrial && _boss.Judgement == EnemyBoss.JudgementStage.Ward
            ? Mathf.Clamp01(1f - _boss.WardTaken / (float)Mathf.Max(1, _boss.TrialValues.Ward)) : -1f;

        static void Bar(Rect rect, float fill, Color color, bool flashBack = false)
        {
            GUI.color = flashBack ? s_Flash : s_Back;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
        }
    }
}
