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
        public float StaggerShown => _boss != null && _boss.Stagger != null ? _boss.Stagger.Fill : 0f;

        void Awake() => _boss = GetComponent<EnemyBoss>();

        void OnGUI()
        {
            if (!Showing || Event.current.type != EventType.Repaint) return;
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            Color colorBefore = GUI.color;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;
            // Clear of the readout boxes in the top corners (330 wide and a margin, GeneratedLevels), on a narrow view too.
            float width = Mathf.Max(160f, Mathf.Min(MostWidth, viewWidth * 0.6f, viewWidth - 2f * CornerBoxes));
            float x = (viewWidth - width) * 0.5f;

            if (_title == null)
                _title = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, normal = { textColor = Color.white } };
            GUI.color = Color.white;
            GUI.Label(new Rect(x, Top - 24f, width, 22f), _boss.Tuning.Title, _title);

            Bar(new Rect(x, Top, width, HealthHeight), HealthShown, _boss.IsAngry ? s_AngryHealth : s_Health);
            bool flash = _boss.IsDazed && Mathf.Repeat(Time.time, FlashPeriod) < FlashPeriod * 0.5f;
            Bar(new Rect(x, Top + HealthHeight + Gap, width, StaggerHeight), StaggerShown, flash ? s_Flash : s_Stagger, flash);

            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
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
