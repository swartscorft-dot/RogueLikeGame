using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The Grave Ram's bar on screen (work order 149: "The Grave Ram" on a bar at the top of the screen as the boss's bar is drawn,
    /// while its fight is on): the boss's bar (<see cref="BossHealthBar"/>) with its name, its health, its stagger under it flashing the
    /// daze, and the mark that parts its two phases. Drawn with OnGUI as the boss's, in the interface's kinetic skin or the plain one.
    /// Put on the Ram by itself; a picture only, nothing allocated in a frame.
    /// </summary>
    [RequireComponent(typeof(EnemyGraveRam))]
    public sealed class GraveRamBar : MonoBehaviour
    {
        const float Top = 30f;
        const float MostWidth = 560f;
        const float HealthHeight = 16f;
        const float StaggerHeight = 6f;
        const float Gap = 4f;
        const float FlashPeriod = 0.3f;
        const float CornerBoxes = 350f;
        const float HalfMarkWidth = 3f;

        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);
        static readonly Color s_Health = new Color(0.78f, 0.12f, 0.10f);
        static readonly Color s_Stagger = new Color(1f, 0.85f, 0.45f);
        static readonly Color s_Flash = new Color(0.55f, 0.75f, 1f);
        static readonly Color s_HalfMark = new Color(0f, 0f, 0f, 0.85f);

        EnemyGraveRam _ram;
        GUIStyle _title;

        /// <summary>True while it is drawn: the Ram awake (its fight on) and alive. For tests.</summary>
        public bool Showing => _ram != null && _ram.isActiveAndEnabled && _ram.IsAwake && !_ram.IsDead;
        public float HealthShown => _ram != null ? _ram.HealthShare : 0f;
        public float StaggerShown => _ram != null && _ram.Stagger != null ? _ram.Stagger.Fill : 0f;
        /// <summary>
        /// Round 2: while it is dazed, its stagger line is the daze's time still to come, draining from full to empty in the daze's
        /// blue, steady (not flashing): it is open now, for this long. Negative when no daze with a known end runs.
        /// </summary>
        public float DazeShown => _ram != null ? _ram.DazeLeftShare : -1f;
        /// <summary>The name over it.</summary>
        public string Title => Rules.GraveRamValues.Title;
        /// <summary>The share of the bar where its phases part.</summary>
        public float HalfAt => _ram != null ? _ram.Tuning.PhaseTwoBelow : 0.5f;

        void Awake() => _ram = GetComponent<EnemyGraveRam>();

        void OnGUI()
        {
            if (!Showing || Event.current.type != EventType.Repaint) return;
            GUI.depth = Rules.InterfaceDepth.Fight; // under an offer and every screen, as the boss's
            if (InterfaceStyle.Kinetic)
            {
                DrawKinetic();
                return;
            }
            float scale = InterfaceStyle.ScreenHeight / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            Color colorBefore = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = InterfaceStyle.ScreenWidth / scale;
            float width = Mathf.Max(160f, Mathf.Min(MostWidth, viewWidth * 0.6f, viewWidth - 2f * CornerBoxes));
            float x = (viewWidth - width) * 0.5f;
            if (_title == null)
                _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, normal = { textColor = Color.white } };
            GUI.color = Color.white;
            GUI.Label(new Rect(x, Top - 24f, width, 22f), Title, _title);
            var health = new Rect(x, Top, width, HealthHeight);
            Bar(health, HealthShown, s_Health);
            Halves(health);
            float daze = DazeShown;
            bool flash = daze < 0f && _ram.IsDazed && Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f;
            Bar(new Rect(x, Top + HealthHeight + Gap, width, StaggerHeight), daze >= 0f ? daze : StaggerShown, daze >= 0f || flash ? s_Flash : s_Stagger, flash);
            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        /// <summary>True when it is drawn in the interface's kinetic skin.</summary>
        public bool Kinetic => InterfaceStyle.Kinetic;

        /// <summary>The bar in the kinetic skin, as the boss's (work order 045): its name over a long slanted bar, the stagger line under it.</summary>
        void DrawKinetic()
        {
            Rules.InterfaceValues v = InterfaceStyle.V;
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float width = Mathf.Max(200f, Mathf.Min(v.BossWidth, viewWidth * 0.5f));
            var bar = new Rect((viewWidth - width) * 0.5f, v.BossTop, width, v.BossHeight);
            float h = v.BossNameSize * 1.3f;
            InterfaceStyle.Label(new Rect(bar.x, bar.y - h - 2f, bar.width, h), Title,
                                 InterfaceStyle.Text(true, v.BossNameSize, TextAnchor.LowerLeft), InterfaceStyle.C(v.White), true);
            InterfaceStyle.HeavyPlate(bar, bar.center.y, v.Slant, v.HeavyOutline * 0.75f, v.Depth);
            InterfaceStyle.SlantedBar(bar, HealthShown, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Red));
            InterfaceStyle.DotsAlong(bar, bar.center.y, v.Slant, 1f - v.PlateDots, InterfaceStyle.C(v.Black, 0.45f));
            Halves(bar);
            float daze = DazeShown;
            bool flash = daze < 0f && _ram.IsDazed && Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f;
            var stagger = new Rect(bar.x - (v.BossHeight * 0.5f + 3f + v.BossStaggerHeight * 0.5f) * v.Slant, bar.yMax + 3f, bar.width, v.BossStaggerHeight);
            if (daze >= 0f) InterfaceStyle.SlantedBar(stagger, daze, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Dazed)); // round 2: the daze's time left
            else InterfaceStyle.SlantedBar(stagger, StaggerShown, flash ? InterfaceStyle.C(v.Dazed) : InterfaceStyle.C(v.Back),
                                           flash ? InterfaceStyle.C(v.Dazed) : InterfaceStyle.C(v.Stagger));
            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        /// <summary>The mark's rectangle on a health bar drawn in <paramref name="bar"/>: where phase 2 begins.</summary>
        public Rect HalfMark(Rect bar) => new Rect(bar.x + bar.width * Mathf.Clamp01(HalfAt) - HalfMarkWidth * 0.5f, bar.y - 2f, HalfMarkWidth, bar.height + 4f);

        void Halves(Rect bar)
        {
            Color before = GUI.color;
            GUI.color = s_HalfMark;
            GUI.DrawTexture(HalfMark(bar), Texture2D.whiteTexture);
            GUI.color = before;
        }

        static void Bar(Rect rect, float fill, Color color, bool flashBack = false)
        {
            GUI.color = flashBack ? s_Flash : s_Back;
            GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
        }
    }
}
