using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// One cast bar in the HUD (work order 040, Kristian's second session of 2026-11-20: "I do not find the F and
    /// right-click spells intuitive to time. Rather a casting bar that shows start to end, and where to press."), the one he
    /// looks at instead of the ring on the floor. Fixed on the screen, centred, a little below the middle (she is near the
    /// middle), about a fifth of the screen wide, clear of the skill bar. It shows the newest timed thing of hers, the whole
    /// of it left to right (<see cref="CastBarRules"/>):
    /// - a recast chain's next press (Triple Tap on the right button, Backstep on F): from the press before to the end of
    ///   the window, the part before the window opens dim (a press there only waits), the clean part a bright, framed zone,
    ///   and a marker that sweeps on the chain's own clock (it stands still in a pause and runs on through a hit stop, as the
    ///   window does); above it the skill's name and which press it is ("Triple Tap 3 / 3");
    /// - a charge (Long Shot): from the start of the charge to where it fires by itself, the perfect band the zone;
    /// - a hold (Fan Fire): its startup dim, then how long it can be held;
    /// - a cast (Heavy Volley): from the press to its hit.
    /// When the press (the release, the end of the hold or the cast) comes, the bar flashes in the colour of what it was,
    /// clean or plain, for a moment and goes. It reads the character and changes nothing in a fight.
    ///
    /// The switch is <see cref="CastBarValues.On"/> (in <see cref="Values"/>, reachable in the Inspector while playing), on by
    /// default; off, nothing is drawn. It hides while she is dead and under a choice, an end screen or the pause menu.
    /// Nothing is allocated per frame while it is up: its words are made again only when what it shows changes.
    ///
    /// With the interface's kinetic skin on (work order 045) the same bar is a short slanted blade just over her head
    /// (CastBarKinetic.cs), and the bar under the middle of the screen is not drawn.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="PlayerSigns"/> (never in a test run); tests make
    /// one with <see cref="Ensure"/>.
    /// </summary>
    // After the camera's move in its LateUpdate (CameraRig, of no order), so the blade over her head is placed from this
    // frame's camera (the fourth review of 2026-11-21, work order 055; as EnemyBars, 047).
    [DefaultExecutionOrder(100)]
    public sealed partial class CastBar : MonoBehaviour
    {
        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);         // the HUD's
        static readonly Color s_Dim = new Color(0.45f, 0.45f, 0.5f, 0.35f);  // before the window opens: a press only waits
        static readonly Color s_Live = new Color(0.8f, 0.8f, 0.85f, 0.35f);  // the window, the charge, the hold
        static readonly Color s_Fill = new Color(1f, 0.6f, 0.15f, 0.55f);    // her orange, up to the marker (CombatFxValues.HerRim)
        static readonly Color s_Zone = new Color(0.3f, 1f, 0.45f, 0.6f);     // SkillBar's clean zone
        static readonly Color s_ZoneLit = new Color(0.4f, 1f, 0.5f, 1f);     // SkillBar's clean colour
        static readonly Color s_Marker = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Frame = new Color(1f, 1f, 1f, 0.5f);
        static readonly Color s_Plain = new Color(1f, 0.75f, 0.2f, 1f);      // SkillBar's in-use colour
        static readonly Color s_Text = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Shadow = new Color(0f, 0f, 0f, 0.9f);

        /// <summary>The one running now, or null.</summary>
        public static CastBar Current { get; private set; }

        /// <summary>The switch and numbers, defaults in code (work order 040: first guesses).</summary>
        public CastBarValues Values = new CastBarValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The bar flashed: what the press was. For tests.</summary>
        public event Action<CastBarFlash> Flashed;

        // ---------------- What it shows, worked out every frame (also without a window, for tests) ----------------

        /// <summary>True while a timed thing of hers is up and the switch is on (whether or not a screen hides it).</summary>
        public bool Up { get; private set; }
        /// <summary>The bar now: what it spans, its dim part, its zone and its marker. The last one shown while it flashes after.</summary>
        public CastBarView View { get; private set; }
        /// <summary>True while a press (a release) now would be clean (perfect): the zone is lit.</summary>
        public bool Lit { get; private set; }
        /// <summary>The slot of the skill it is up for, or -1.</summary>
        public int Slot { get; private set; } = -1;
        /// <summary>For a recast chain: which press the bar is for, and of how many; 0 otherwise.</summary>
        public int Press { get; private set; }
        public int Count { get; private set; }
        /// <summary>The words over it: the skill's name, and for a chain which press it is ("Triple Tap 3 / 3").</summary>
        public string Label => _label;
        /// <summary>The flash showing now (None when none), and its strength, 1 to 0.</summary>
        public CastBarFlash Flash { get; private set; }
        public float FlashAlpha { get; private set; }
        /// <summary>Flashes since it started, and what the last one was.</summary>
        public int Flashes { get; private set; }
        public CastBarFlash LastFlash { get; private set; }
        /// <summary>True when it would be drawn now: on, she is alive, no screen up, and a timed thing is up or it flashes.</summary>
        public bool Showing { get; private set; }

        PlayerCharacter _player;
        PlayerHealth _health;
        string _label = "";
        CastBarKind _labelKind;
        int _labelSlot = -1, _labelPress = -1, _labelCount = -1;
        float _flashAt = float.NegativeInfinity;
        CastBarFlash _flashKind;
        CastBarView _flashView;
        string _flashLabel = "";
        bool _flashLit;

        GUIStyle _labelStyle;
        readonly GUIContent _content = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static CastBar Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("CastBar");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<CastBar>();
            return Current;
        }

        void OnDestroy()
        {
            Forget();
            if (Current == this) Current = null;
        }

        // ---------------- Every frame ----------------

        void LateUpdate() => Tick();

        /// <summary>The frame's work: what the bar shows now. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            if (_player == null) Find();
            bool alive = _player != null && (_health == null || _health.IsAlive);
            CastBarView view = default;
            bool lit = false;
            int slot = -1, press = 0, count = 0;
            CastBarKind kind = CastBarKind.None;
            bool up = Values.On && alive && Read(out view, out lit, out slot, out press, out count, out kind);
            Up = up;
            if (up)
            {
                View = view;
                Lit = lit;
                Slot = slot;
                Press = press;
                Count = count;
                MakeLabel(kind, slot, press, count);
            }
            else
            {
                Lit = false;
                Slot = -1;
                Press = Count = 0;
            }
            FlashAlpha = Values.On ? CastBarRules.FlashAlpha(Time.unscaledTime - _flashAt, Values.FlashSeconds) : 0f;
            Flash = FlashAlpha > 0f ? _flashKind : CastBarFlash.None;
            if (!up && Flash != CastBarFlash.None) View = _flashView;
            Showing = Values.On && alive && !PlayerSigns.ScreenUp && (up || Flash != CastBarFlash.None);
            PlaceBlade(); // work order 045: in the interface's skin, a blade over her head (CastBarKinetic.cs)
        }

        /// <summary>The newest timed thing of hers, if one is up: a recast chain's next press, else the skill in use's charge, hold or cast.</summary>
        bool Read(out CastBarView view, out bool lit, out int slot, out int press, out int count, out CastBarKind kind)
        {
            view = default;
            lit = false;
            press = count = 0;
            kind = CastBarKind.None;
            PlayerCharacter p = _player;
            // A chain: a press waits for it, and its window has not run out. Another skill starting ends it, so it is the newest.
            slot = p.RecastSlot;
            if (slot >= 0 && p.RecastShots < p.RecastCount)
            {
                float window = p.RecastWindowSeconds, since = p.RecastSinceOpened;
                if (window > 0f && since <= window)
                {
                    view = CastBarRules.Recast(p.RecastLead, window, p.RecastSincePress, since, p.RecastCleanPart);
                    lit = p.RecastPressWouldBeClean;
                    press = p.RecastShots + 1;
                    count = p.RecastCount;
                    kind = CastBarKind.Recast;
                    return true;
                }
            }
            SkillDefinition skill = p.ActiveSkill;
            slot = p.ActiveSkillSlot;
            if (skill == null) return false;
            SkillPhase phase = p.SkillPhase;
            float progress = p.SkillProgress;
            switch (skill.Input)
            {
                case SkillInput.Charge when !skill.NoCharge && (phase == SkillPhase.Channel || phase == SkillPhase.Startup):
                    // From the start of the charge to where it fires by itself; after the release it holds where it was let go
                    // until the shot goes off, and flashes then.
                    CastBarRules.PerfectBand(skill, p.ActivePrime == PrimeUse.PerfectRelease, p.FocusModeValues, out float from, out float to);
                    view = CastBarRules.Charge(p.SkillChargedSeconds, skill.HoldMax, from, to);
                    lit = p.InPerfectBand;
                    kind = CastBarKind.Charge;
                    return true;
                case SkillInput.Holding when phase == SkillPhase.Startup || phase == SkillPhase.Channel:
                    float startup = Mathf.Max(0f, skill.Startup);
                    float since = phase == SkillPhase.Startup ? progress * startup : startup + progress * Mathf.Max(0f, skill.HoldMax);
                    view = CastBarRules.Hold(since, startup, skill.HoldMax);
                    kind = CastBarKind.Hold;
                    return true;
                case SkillInput.Casting when phase == SkillPhase.Startup:
                    view = CastBarRules.Cast(progress * skill.Startup, skill.Startup);
                    kind = CastBarKind.Cast;
                    return true;
            }
            return false;
        }

        /// <summary>The words over the bar, made again only when what it shows changes.</summary>
        void MakeLabel(CastBarKind kind, int slot, int press, int count)
        {
            if (kind == _labelKind && slot == _labelSlot && press == _labelPress && count == _labelCount) return;
            _labelKind = kind;
            _labelSlot = slot;
            _labelPress = press;
            _labelCount = count;
            string name = slot >= 0 && slot < _player.Skills.Count && _player.Skills[slot] != null ? _player.Skills[slot].Name : "";
            _label = kind == CastBarKind.Recast ? name + " " + press + " / " + count : name;
        }

        void Find()
        {
            Forget();
            _player = FindAnyObjectByType<PlayerCharacter>();
            _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            if (_player != null) _player.Reported += OnReport;
            _herTop = TopOfHer(); // work order 045: where the blade goes, found once with her
        }

        void Forget()
        {
            if (_player != null) _player.Reported -= OnReport;
            _player = null;
            _health = null;
        }

        /// <summary>The press came: a recast press, a charge's shot going off, the end of a hold, a cast's hit. The bar flashes.</summary>
        void OnReport(PlayerReport r)
        {
            if (!Values.On || _player == null) return;
            SkillDefinition skill = r.Slot >= 0 && r.Slot < _player.Skills.Count ? _player.Skills[r.Slot] : null;
            switch (r.Kind)
            {
                case PlayerReportKind.RecastPressed:
                    StartFlash(CastBarRules.OfPress(r.Clean));
                    break;
                case PlayerReportKind.SkillFired when skill != null && skill.Input == SkillInput.Charge && !skill.NoCharge:
                    StartFlash(r.Clean ? CastBarFlash.Clean : CastBarFlash.Plain); // Clean: a perfect release
                    break;
                case PlayerReportKind.SkillFired when skill != null && skill.Input == SkillInput.Casting:
                    StartFlash(CastBarFlash.Plain);
                    break;
                case PlayerReportKind.SkillEnded when skill != null && skill.Input == SkillInput.Holding && r.Ending == SkillEnding.RanOut:
                    StartFlash(CastBarFlash.Plain);
                    break;
            }
        }

        void StartFlash(CastBarFlash kind)
        {
            _flashAt = Time.unscaledTime;
            _flashKind = kind;
            _flashView = View;
            _flashLabel = _label;
            _flashLit = Lit;
            Flashes++;
            LastFlash = kind;
            Flashed?.Invoke(kind);
        }

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || !Showing) return;
            GUI.depth = 1; // under the skill bar, the HUD and its screens, as the signs' words
            if (Kinetic)
            {
                DrawBlade(); // work order 045: the blade over her head instead of the bar under the middle
                return;
            }
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Color before = GUI.color;
            float viewWidth = Screen.width / scale;
            float w = Mathf.Max(Values.LeastWidth, viewWidth * Mathf.Clamp01(Values.WidthShare));
            var bar = new Rect((viewWidth - w) * 0.5f, SkillBar.LayoutHeight * 0.5f + Values.BelowMiddle, w, Mathf.Max(4f, Values.Height));
            bool flashing = Flash != CastBarFlash.None;
            Draw(bar, View, Up ? Lit : _flashLit, Up ? _label : _flashLabel);
            if (flashing)
            {
                Color c = Flash == CastBarFlash.Clean ? s_ZoneLit : s_Plain;
                float a = FlashAlpha;
                Fill(bar, new Color(c.r, c.g, c.b, 0.55f * a));
                Frame(new Rect(bar.x - 3f, bar.y - 3f, bar.width + 6f, bar.height + 6f), new Color(c.r, c.g, c.b, a), 3f);
            }
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void Draw(Rect bar, CastBarView v, bool lit, string label)
        {
            Fill(bar, s_Back);
            float dim = Mathf.Clamp01(v.DimTo);
            if (dim > 0f) Fill(new Rect(bar.x, bar.y, bar.width * dim, bar.height), s_Dim);
            Fill(new Rect(bar.x + bar.width * dim, bar.y, bar.width * (1f - dim), bar.height), s_Live);
            float marker = Mathf.Clamp01(v.Marker);
            Fill(new Rect(bar.x, bar.y + 3f, bar.width * marker, bar.height - 6f), s_Fill);
            if (v.HasZone)
            {
                var zone = new Rect(bar.x + bar.width * Mathf.Clamp01(v.ZoneFrom), bar.y - 2f, bar.width * Mathf.Clamp01(v.ZoneTo - v.ZoneFrom), bar.height + 4f);
                Fill(zone, lit ? s_ZoneLit : s_Zone);
                Frame(zone, lit ? s_Marker : s_ZoneLit, lit ? 2f : 1f);
            }
            Frame(bar, s_Frame, 1f);
            Fill(new Rect(bar.x + bar.width * marker - 1.5f, bar.y - 4f, 3f, bar.height + 8f), s_Marker);

            if (string.IsNullOrEmpty(label)) return;
            if (_labelStyle == null)
            {
                _labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.LowerCenter, wordWrap = false, clipping = TextClipping.Overflow };
                _labelStyle.normal.textColor = Color.white;
            }
            _content.text = label;
            var r = new Rect(bar.x, bar.y - Values.LabelHeight - 4f, bar.width, Values.LabelHeight);
            GUI.color = s_Shadow;
            GUI.Label(new Rect(r.x + 1f, r.y + 1f, r.width, r.height), _content, _labelStyle);
            GUI.color = s_Text;
            GUI.Label(r, _content, _labelStyle);
        }

        static void Fill(Rect r, Color c)
        {
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
        }

        static void Frame(Rect r, Color c, float t)
        {
            Fill(new Rect(r.x, r.y, r.width, t), c);
            Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            Fill(new Rect(r.x, r.y, t, r.height), c);
            Fill(new Rect(r.x + r.width - t, r.y, t, r.height), c);
        }
    }
}
