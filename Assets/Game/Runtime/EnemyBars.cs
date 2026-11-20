using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>What one small bar shows this frame, where (GUI pixels, top left 0,0). For the drawing and for tests.</summary>
    public struct EnemyBarView
    {
        public Enemy Enemy;
        public bool Elite;
        /// <summary>The health bar, its fill (0 to 1), and the dark back round the bars.</summary>
        public Rect Health;
        public float HealthFill;
        public Rect Back;
        /// <summary>The stagger bar under the health bar, for an enemy with one (a guard, an elite), and its fill.</summary>
        public bool HasStagger;
        public Rect Stagger;
        public float StaggerFill;
        public bool Dazed;
        /// <summary>The marks: pips in a row at the bar's end, the lit ones first, the rest empty up to the cap.</summary>
        public int Lit;
        public int Pips;
        public bool AtCap;
        public Rect LitRow;
        public Rect EmptyRow;
        /// <summary>Tag Round's tag on it: a green pip after the marks.</summary>
        public bool Tagged;
        public Rect Tag;
        /// <summary>The draw calls it costs.</summary>
        public int DrawCalls;
    }

    /// <summary>
    /// Small bars over the enemies (work order 042; Kristian's words of 2026-11-20: "We also need small health bars over the
    /// enemies. And these stacks can be beside them. What is there now is far too big."). One thin bar over every ordinary
    /// enemy and elite that is alive and on screen, from its first hit, mark or tag (<see cref="EnemyBarRules.Shows"/>),
    /// following its head: its health in red, an elite's a little wider with a gold edge (its tint,
    /// <see cref="EnemyFactory.EliteTint"/>); its marks as tiny pips at the bar's end in the marks' colour (red at the cap,
    /// as the large ones were), the free ones up to the cap as empty pips; Tag Round's tag as a green pip after them; and
    /// for one with a stagger bar (a guard, an elite) that bar directly under the health bar at the same width, flashing
    /// blue while dazed. This takes the place of the large marks (<see cref="MarkPips"/>) and the stagger bar
    /// (<see cref="StaggerBarView"/>) over those enemies; the large marks can be put back beside it
    /// (<see cref="EnemyBarValues.BigMarks"/>). The boss keeps its bar at the top of the screen, its stagger bar and its
    /// large marks, and gets no small one; a dummy keeps the large marks. Off (<see cref="EnemyBarValues.On"/>), as before.
    ///
    /// The cost: worked out once a frame (<see cref="Tick"/>, after the enemies have moved) into a list kept from frame to
    /// frame, nothing allocated; drawn with IMGUI on the repaint only, from one texture made in code (a pip and a gap,
    /// repeated along a row, its solid part for the bars): a bar is its back, its health, and a call per row of pips, so
    /// 2 to 7 draw calls (<see cref="EnemyBarRules.DrawCalls"/>, <see cref="DrawCalls"/>).
    ///
    /// It installs itself when Play starts under the same rule as <see cref="GeneratedLevels"/> (never in a test run);
    /// tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed class EnemyBars : MonoBehaviour
    {
        const int PipTexels = 7;       // the texture: 5 lit texels of pip and 2 clear of gap, repeated along a row
        const int PipLitTexels = 5;

        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.7f);
        static readonly Color s_Health = new Color(0.85f, 0.2f, 0.2f, 1f);       // the HUD's red
        static readonly Color s_Stagger = new Color(1f, 0.85f, 0.45f, 1f);       // the stagger bar's fill
        static readonly Color s_Dazed = new Color(0.55f, 0.75f, 1f, 1f);         // the stagger bar's flash
        static readonly Color s_Mark = new Color(1f, 0.55f, 0.1f, 1f);           // MarkPips' rim
        static readonly Color s_MarkFull = new Color(1f, 0.15f, 0.08f, 1f);      // MarkPips' rim at the cap
        static readonly Color s_Empty = new Color(1f, 0.55f, 0.1f, 0.3f);        // the marks' colour, faint: a mark still free
        static readonly Color s_Tag = new Color(0.2f, 1f, 0.45f, 1f);            // MarkPips' tag
        const float FlashPeriod = 0.3f;
        static readonly KitValues s_DefaultKit = new KitValues();

        /// <summary>The one running now, or null.</summary>
        public static EnemyBars Current { get; private set; }

        /// <summary>The numbers and the switches.</summary>
        public EnemyBarValues Values = new EnemyBarValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The bars shown this frame.</summary>
        public int Count { get; private set; }
        /// <summary>The draw calls they cost this frame.</summary>
        public int DrawCalls { get; private set; }

        EnemyBarView[] _bars = new EnemyBarView[128];
        Texture2D _texture;
        PlayerCharacter _player;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static EnemyBars Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("EnemyBars");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<EnemyBars>();
            return Current;
        }

        /// <summary>
        /// True when the small bars stand in for an enemy's stagger bar and marks: they are on, and it is one of theirs (an
        /// ordinary enemy or an elite, never the boss).
        /// </summary>
        public static bool Covers(Enemy enemy) =>
            enemy != null && !(enemy is EnemyBoss) && Current != null && Current.isActiveAndEnabled && Current.Values.On;

        /// <summary>True when an enemy's large marks are not drawn: the small bars show them, and the large ones are not wanted beside them.</summary>
        public static bool HidesBigMarks(Enemy enemy) => Covers(enemy) && !Current.Values.BigMarks;

        /// <summary>This frame's bar over an enemy, if it shows one.</summary>
        public bool TryGet(Enemy enemy, out EnemyBarView view)
        {
            for (int i = 0; i < Count; i++)
            {
                if (_bars[i].Enemy != enemy) continue;
                view = _bars[i];
                return true;
            }
            view = default;
            return false;
        }

        void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
            if (Current == this) Current = null;
        }

        void LateUpdate() => Tick();

        /// <summary>
        /// Works out this frame's bars: every ordinary enemy and elite alive, on screen, and touched, placed over its head.
        /// Nothing is allocated (the list grows only past its size, which a frame of 128 bars does not reach).
        /// </summary>
        public void Tick()
        {
            Count = 0;
            DrawCalls = 0;
            if (!Values.On) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_player == null) _player = FindAnyObjectByType<PlayerCharacter>();
            int cap = _player != null ? _player.Kit.MaxMarks : s_DefaultKit.MaxMarks;
            float scale = Screen.height / Mathf.Max(1f, Values.LayoutHeight);
            float width = Screen.width, height = Screen.height;
            float now = Time.time;
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || e is EnemyBoss) continue;
                Damageable body = e.Body;
                if (body == null || !body.IsAlive) continue;
                int marks = body.MarkCount;
                bool tagged = body.IsTagged;
                StaggerBar stagger = body.Stagger;
                bool staggerTaken = stagger != null && (stagger.Fill < 0.999f || body.IsDazed);
                if (!EnemyBarRules.Shows(true, body.CurrentHealth, body.MaxHealth, marks, tagged, staggerTaken)) continue;

                float head = e.ModelHeight > 0f ? e.ModelHeight : 2f;
                Vector3 s = cam.WorldToScreenPoint(e.transform.position + Vector3.up * (head + Values.OverHead));
                if (s.z <= 0f) continue;
                float barWidth = EnemyBarRules.Width(e.Kind, e.IsElite, Values) * scale;
                float x = s.x - barWidth * 0.5f, y = height - s.y; // the bottom of the stack, in GUI pixels
                bool hasStagger = stagger != null;
                float stack = EnemyBarRules.StackHeight(hasStagger, Values) * scale;
                int pips = EnemyBarRules.Pips(marks, cap), lit = EnemyBarRules.LitPips(marks, cap);
                float pipsWidth = EnemyBarRules.PipsWidth(pips, Values) * scale + (tagged ? (Values.Pip + Values.PipGap) * scale : 0f);
                // Off screen, with all it draws: nothing.
                if (x + barWidth + Values.PipsFromBar * scale + pipsWidth < 0f || x > width || y < 0f || y - stack > height) continue;

                if (Count == _bars.Length) System.Array.Resize(ref _bars, _bars.Length * 2);
                float edge = Values.Edge * scale;
                float top = y - stack;
                var view = new EnemyBarView
                {
                    Enemy = e,
                    Elite = e.IsElite,
                    Health = new Rect(x, top, barWidth, Values.Thickness * scale),
                    HealthFill = body.MaxHealth > 0 ? Mathf.Clamp01(body.CurrentHealth / (float)body.MaxHealth) : 0f,
                    Back = new Rect(x - edge, top - edge, barWidth + 2f * edge, stack + 2f * edge),
                    HasStagger = hasStagger,
                    Dazed = body.IsDazed,
                    Lit = lit,
                    Pips = pips,
                    AtCap = marks >= cap && cap > 0,
                    Tagged = tagged,
                };
                if (hasStagger)
                {
                    view.Stagger = new Rect(x, top + (Values.Thickness + Values.Edge) * scale, barWidth, Values.StaggerThickness * scale);
                    view.StaggerFill = Mathf.Clamp01(stagger.Fill);
                }
                float pip = Values.Pip * scale, step = (Values.Pip + Values.PipGap) * scale;
                float px = x + barWidth + Values.PipsFromBar * scale;
                float py = top + (Values.Thickness * scale - pip) * 0.5f;
                view.LitRow = new Rect(px, py, EnemyBarRules.PipsWidth(lit, Values) * scale, pip);
                view.EmptyRow = new Rect(px + lit * step, py, EnemyBarRules.PipsWidth(pips - lit, Values) * scale, pip);
                view.Tag = new Rect(px + pips * step, py, pip, pip);
                view.DrawCalls = EnemyBarRules.DrawCalls(view.Elite, hasStagger, lit, pips - lit, tagged);
                _bars[Count++] = view;
                DrawCalls += view.DrawCalls;
            }
            _flashLit = Mathf.Repeat(now, FlashPeriod) < FlashPeriod * 0.5f;
        }

        bool _flashLit;

        void OnGUI()
        {
            if (Count == 0 || Event.current.type != EventType.Repaint) return;
            if (_texture == null) _texture = MakeTexture();
            GUI.depth = 10; // under the HUD's boxes and the choices
            Color before = GUI.color;
            var solid = new Rect(0.5f / PipTexels, 0f, (PipLitTexels - 1f) / PipTexels, 1f);
            float scale = Screen.height / Mathf.Max(1f, Values.LayoutHeight);
            float eliteEdge = Values.EliteEdge * scale;
            for (int i = 0; i < Count; i++)
            {
                EnemyBarView b = _bars[i];
                if (b.Elite)
                {
                    GUI.color = EnemyFactory.EliteTint;
                    GUI.DrawTextureWithTexCoords(new Rect(b.Back.x - eliteEdge, b.Back.y - eliteEdge, b.Back.width + 2f * eliteEdge, b.Back.height + 2f * eliteEdge), _texture, solid);
                }
                GUI.color = s_Back;
                GUI.DrawTextureWithTexCoords(b.Back, _texture, solid);
                GUI.color = s_Health;
                GUI.DrawTextureWithTexCoords(new Rect(b.Health.x, b.Health.y, b.Health.width * b.HealthFill, b.Health.height), _texture, solid);
                if (b.HasStagger)
                {
                    bool flash = b.Dazed && _flashLit;
                    GUI.color = flash ? s_Dazed : s_Stagger;
                    float fill = b.Dazed ? 1f : b.StaggerFill; // dazed: the bar flashes whole, as the large one's frame did
                    if (!b.Dazed || flash) GUI.DrawTextureWithTexCoords(new Rect(b.Stagger.x, b.Stagger.y, b.Stagger.width * fill, b.Stagger.height), _texture, solid);
                }
                if (b.Lit > 0) Row(b.LitRow, b.Lit, b.AtCap ? s_MarkFull : s_Mark);
                if (b.Pips > b.Lit) Row(b.EmptyRow, b.Pips - b.Lit, s_Empty);
                if (b.Tagged) Row(b.Tag, 1, s_Tag);
            }
            GUI.color = before;
        }

        /// <summary>A row of pips in one call: the texture's pip and gap, repeated, the last gap cut off.</summary>
        void Row(Rect r, int count, Color color)
        {
            GUI.color = color;
            float u = count - (PipTexels - PipLitTexels) / (float)PipTexels;
            GUI.DrawTextureWithTexCoords(r, _texture, new Rect(0f, 0f, u, 1f));
        }

        /// <summary>The one texture, made in code: a row of 5 white texels and 2 clear ones, repeated across and drawn as it is.</summary>
        static Texture2D MakeTexture()
        {
            var tex = new Texture2D(PipTexels, 1, TextureFormat.RGBA32, false)
            {
                name = "EnemyBarPips", wrapModeU = TextureWrapMode.Repeat, wrapModeV = TextureWrapMode.Clamp, filterMode = FilterMode.Point,
            };
            var pixels = new Color32[PipTexels];
            for (int i = 0; i < PipTexels; i++) pixels[i] = i < PipLitTexels ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
            tex.SetPixels32(pixels);
            tex.Apply();
            return tex;
        }
    }
}
