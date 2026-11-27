using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The lancer's warning mark on the screen (work order 145; Kristian, 2026-11-27: "Utenomfra, men et lite varsel ikon kan være
    /// nice. Et utropstegn"; the rules, the switch and the numbers are <see cref="LancerWarningMark"/>). Each frame it looks at
    /// the lancers (<see cref="Enemy.All"/>): one that begins its charge's wind-up while its feet stand off his screen (the real
    /// camera's view of the ground, <see cref="RangedOpening.OnScreen"/>, with <see cref="LancerWarningMark.ScreenMargin"/>) gets a
    /// plain "!" at the screen's edge on the line from her to it, until it is on screen, its charge is over or it dies. It reads
    /// the lancers and the camera and changes nothing of them: not when or where a lancer charges, nothing of her aim, the
    /// cursor or the sight.
    ///
    /// Drawn as the sight's cues are (<see cref="TimingAtTheSight"/>): the engine's own rounded rectangles, a bar and a dot in
    /// the lancer's blue, each on a dark one a little larger, at the depth of the words and signs (under the skill strip).
    /// Hidden while a choice, an end screen or the pause menu is up. It installs itself when Play starts under the same rule as
    /// the other pieces (never in a test run); tests make one with <see cref="Ensure"/>. A fixed pool of
    /// <see cref="LancerWarningMark.Pool"/> tracks: nothing is allocated in a frame.
    /// </summary>
    [DefaultExecutionOrder(Order)]
    public sealed class LancerWarningMarks : MonoBehaviour
    {
        /// <summary>Its place in the frame: after the enemies' Update (their wind-up and charge for the frame) and the camera's LateUpdate (order 0).</summary>
        public const int Order = 200;
        /// <summary>m along the ground from her toward the lancer the second point is taken at, for the line's way on the screen (near her: always in front of the camera).</summary>
        const float WayProbe = 1f;
        /// <summary>The layout's height in pixels: the values are given for a view this high.</summary>
        const float LayoutHeight = 1080f;

        /// <summary>The one running now, or null.</summary>
        public static LancerWarningMarks Current { get; private set; }

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        readonly EnemyLancer[] _lancers = new EnemyLancer[LancerWarningMark.Pool];
        readonly LancerWarningMark.Track[] _tracks = new LancerWarningMark.Track[LancerWarningMark.Pool];
        readonly bool[] _shown = new bool[LancerWarningMark.Pool];
        readonly Vector2[] _at = new Vector2[LancerWarningMark.Pool];
        PlayerCharacter _player;

        /// <summary>How many marks are up this frame.</summary>
        public int Shown { get; private set; }
        /// <summary>How many marks the last repaint drew (for tests: with the switch off, never one).</summary>
        public int LastDrawn { get; private set; }
        /// <summary>How many lancers it follows through a charge now (begun on screen or off).</summary>
        public int Followed { get; private set; }

        /// <summary>Is mark <paramref name="i"/> of the pool up, its lancer, and where its middle stands (layout px of a view 1080 high, y down).</summary>
        public bool IsShown(int i) => i >= 0 && i < _shown.Length && _shown[i];
        public EnemyLancer LancerOf(int i) => i >= 0 && i < _lancers.Length ? _lancers[i] : null;
        public Vector2 At(int i) => i >= 0 && i < _at.Length ? _at[i] : Vector2.zero;

        /// <summary>The mark up for this lancer: its place in the pool, or -1.</summary>
        public int MarkOf(EnemyLancer l)
        {
            for (int i = 0; i < _lancers.Length; i++)
                if (_shown[i] && _lancers[i] == l && l != null) return i;
            return -1;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static LancerWarningMarks Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("LancerWarningMarks");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<LancerWarningMarks>();
            return Current;
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
        }

        void OnDisable()
        {
            for (int i = 0; i < _lancers.Length; i++) Free(i);
            Shown = Followed = 0;
        }

        void LateUpdate() => Tick();

        /// <summary>A lancer in its charge: the wind-up of its charge (an attack that rushes), or the run itself.</summary>
        static bool InCharge(EnemyLancer l) =>
            l.IsCharging || (l.State == EnemyState.Windup && l.Attack != null && l.Attack.Rush > 0f);

        static bool OnScreen(EnemyLancer l) => RangedOpening.OnScreen(l.transform.position, LancerWarningMark.ScreenMargin);

        void Free(int i)
        {
            _lancers[i] = null;
            _tracks[i] = default;
            _shown[i] = false;
        }

        /// <summary>The frame's work: which lancers have a mark, and where. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            bool on = LancerWarningMark.On;
            if (on && _player == null) _player = FindAnyObjectByType<PlayerCharacter>();

            // The lancers followed: on through their charge, or let go.
            for (int i = 0; i < _lancers.Length; i++)
            {
                EnemyLancer l = _lancers[i];
                if (l == null) { Free(i); continue; }
                bool inCharge = InCharge(l);
                _shown[i] = LancerWarningMark.Step(ref _tracks[i], on, !l.IsDead, inCharge, inCharge && OnScreen(l));
                if (!_tracks[i].InCharge) Free(i);
            }

            // A lancer whose charge begins this frame: followed from now (a mark if it stands off screen).
            if (on)
            {
                var all = Enemy.All;
                for (int k = 0; k < all.Count; k++)
                {
                    if (!(all[k] is EnemyLancer l) || l == null || l.IsDead || !InCharge(l) || Follows(l)) continue;
                    int free = FreeSlot();
                    if (free < 0) break;
                    _lancers[free] = l;
                    _tracks[free] = default;
                    _shown[free] = LancerWarningMark.Step(ref _tracks[free], true, true, true, OnScreen(l));
                }
            }

            int shown = 0, followed = 0;
            for (int i = 0; i < _lancers.Length; i++)
            {
                if (_lancers[i] != null) followed++;
                if (!_shown[i]) continue;
                if (Placed(_lancers[i], out Vector2 at)) { _at[i] = at; shown++; }
                else _shown[i] = false; // no way to it on the screen this frame: nothing drawn
            }
            Shown = shown;
            Followed = followed;
        }

        bool Follows(EnemyLancer l)
        {
            for (int i = 0; i < _lancers.Length; i++)
                if (_lancers[i] == l) return true;
            return false;
        }

        int FreeSlot()
        {
            for (int i = 0; i < _lancers.Length; i++)
                if (_lancers[i] == null) return i;
            return -1;
        }

        /// <summary>
        /// Where the mark for this lancer stands: her ground point and a point <see cref="WayProbe"/> from it toward the lancer, seen
        /// through the camera (a straight line on the ground is a straight line on the screen), give the line's way on the screen;
        /// the mark is where that line meets the insets (<see cref="LancerWarningMark.Place"/>). Layout px, y down.
        /// </summary>
        bool Placed(EnemyLancer l, out Vector2 at)
        {
            at = Vector2.zero;
            Camera cam = Camera.main;
            if (cam == null || _player == null || l == null) return false;
            Vector3 her = _player.Position;
            Vector3 to = l.transform.position - her;
            to.y = 0f;
            float d = to.magnitude;
            if (d < 1e-3f) return false;
            Vector3 a = cam.WorldToScreenPoint(her), b = cam.WorldToScreenPoint(her + to * (WayProbe / d));
            if (a.z <= 0f || b.z <= 0f) return false;
            float height = InterfaceStyle.ScreenHeight, scale = Mathf.Max(1e-4f, height / LayoutHeight);
            InterfaceValues v = InterfaceStyle.V;
            UiBox plate = v.TopPlate ? InterfacePlate.Whole(v) : default; // the top plate is drawn over the signs: kept off it
            if (!LancerWarningMark.Place(a.x / scale, (height - a.y) / scale, (b.x - a.x) / scale, (a.y - b.y) / scale,
                                         InterfaceStyle.ScreenWidth / scale, LayoutHeight, plate, out float x, out float y)) return false;
            at = new Vector2(x, y);
            return true;
        }

        // ---------------- The drawing ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            LastDrawn = 0;
            if (Shown == 0 || !LancerWarningMark.On || PlayerSigns.ScreenUp) return;
            GUI.depth = InterfaceDepth.Words; // with the words and signs: under the skill strip, the HUD and its screens
            Color before = GUI.color;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.identity;
            float s = InterfaceStyle.ScreenHeight / LayoutHeight;
            Color ink = InterfaceStyle.C(LancerWarningMark.Colour);
            var dark = new Color(0f, 0f, 0f, LancerWarningMark.OutlineAlpha);
            int drawn = 0;
            for (int i = 0; i < _shown.Length; i++)
            {
                if (!_shown[i]) continue;
                DrawMark(_at[i] * s, s, ink, dark);
                drawn++;
            }
            LastDrawn = drawn;
            GUI.matrix = matrixBefore;
            GUI.color = before;
        }

        /// <summary>One "!" round its middle <paramref name="c"/> (real px): a bar and a dot, each on a dark one larger by the outline.</summary>
        static void DrawMark(Vector2 c, float s, Color ink, Color dark)
        {
            float w = LancerWarningMark.Width * s, o = LancerWarningMark.Outline * s;
            float bar = LancerWarningMark.BarHeight * s, gap = LancerWarningMark.DotGap * s;
            float top = c.y - LancerWarningMark.Height * 0.5f * s;
            var barRect = new Rect(c.x - w * 0.5f, top, w, bar);
            var dotRect = new Rect(c.x - w * 0.5f, top + bar + gap, w, w);
            Pill(Grown(barRect, o), dark);
            Pill(Grown(dotRect, o), dark);
            Pill(barRect, ink);
            Pill(dotRect, ink);
        }

        static Rect Grown(Rect r, float by) => new Rect(r.x - by, r.y - by, r.width + 2f * by, r.height + 2f * by);

        /// <summary>A filled rectangle with fully rounded ends (a disc when it is square).</summary>
        static void Pill(Rect r, Color col)
        {
            GUI.color = Color.white;
            GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, col, 0f, Mathf.Min(r.width, r.height) * 0.5f);
        }
    }
}
