using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The timing of a clean press shown where she is already looking (work order 134; a friend of Kristian's who played the
    /// shared build, 2026-11-25: "Jeg skulle gjerne hatt den visuelle hjelpen på når jeg skal trykke på siktet mitt. Så slipper jeg
    /// å flytte blikket fra det jeg skyter på til den baren i bunn av skjermen"; Kristian: "Vi bør prøve noe kreativt her, noe
    /// timing basert på våpen mekanikker eller noe"). Three cues to compare, one at a time, chosen by the setting
    /// (<see cref="TimingCueSettings"/>), and a click that goes with any of them or alone:
    /// 1. the cylinder (<see cref="CylinderCue"/>): a ring of chambers round the sight; the next chamber turns into a notch at the
    ///    top exactly while the press is clean; a clean press locks it, lit, with a short hard stop; one in the window but not
    ///    clean passes dull; a miss slips back;
    /// 2. the ring (<see cref="RingCue"/>): a band round the sight marks the clean part, a ring closes in and is inside it exactly
    ///    while the press is clean; one ring a press;
    /// 3. the hammer (<see cref="HammerCue"/>): nothing at the sight; her muzzles' light steps up as the window runs, brightest
    ///    exactly while the press is clean (her pistols are one mesh each: no hammer to turn);
    /// 4. the click (<see cref="TimingClick"/>): as the clean part opens, a cocking click, a step higher press by press.
    ///
    /// Every cue reads the SAME windows, from the one place the rules have them: her running chain's clean part
    /// (<see cref="PlayerCharacter.RecastCleanPart"/>, the ChainCleanPart the judge reads), its clock
    /// (<see cref="PlayerCharacter.RecastSinceOpened"/>) and whether a press now would be clean
    /// (<see cref="PlayerCharacter.RecastPressWouldBeClean"/>), so what is shown is what is judged, to the frame. It changes no
    /// window, damage or key; it never moves her aim: drawn round the cursor's point and following it, and it reads no target.
    /// Small and quiet, nothing filled in the middle, and gone when no window is open or coming (after a short linger that shows
    /// how the last press fell). The combo bar and the skill box's light are untouched with every cue.
    ///
    /// The order's switch: <see cref="TimingAtTheSightValues.On"/>; off, nothing of this is drawn or sounded and the setting is
    /// ignored. It installs itself when Play starts under the same rule as the other pieces (never in a test run); tests make one
    /// with <see cref="Ensure"/>. Nothing is allocated in a frame once its pieces are made.
    /// </summary>
    [DefaultExecutionOrder(Order)]
    public sealed partial class TimingAtTheSight : MonoBehaviour
    {
        /// <summary>Its place in the frame: after her arms are posed, the camera has moved and the Laser Sight is drawn.</summary>
        public const int Order = LaserSightLate.Order + 10;

        /// <summary>
        /// The temporary key (work order 134; since the merge of 2026-11-26 the settings screen's Trials page has the two lines as
        /// well): Y steps through the cues, Bar only, Cylinder, Ring, Hammer, the cue's name shown at the sight for a second; Shift
        /// and Y turns the click on or off. It was T until the merge: work order 133's teaching in the first run takes T (skip a
        /// task) and H (end the teaching). Y is bound to nothing else (her bindings, the developer's keys, the room lab's, the run's
        /// keys, and 133's). It works in every build, not only the developer's.
        /// </summary>
        public const Key StepKey = Key.Y;

        /// <summary>The one running now, or null.</summary>
        public static TimingAtTheSight Current { get; private set; }

        /// <summary>The numbers (reachable in the Inspector while playing).</summary>
        public TimingAtTheSightValues Values;

        TimingAtTheSightValues V => Values ?? TimingAtTheSightValues.Live;

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>A keyboard a test presses the temporary key on (with the real devices shut out).</summary>
        public Keyboard TestKeyboard { get; set; }

        /// <summary>A click was played: its pitch. For tests; the sound itself goes to its own voice.</summary>
        public event Action<float> Clicked;

        // ---------------- What it shows, worked out every frame (also without a window, for tests) ----------------

        /// <summary>The cue in use this frame (<see cref="TimingCueSettings.CueNow"/>).</summary>
        public TimingCue Cue { get; private set; }
        /// <summary>True while a chain with a clean press is followed: its window open, or coming (the shot before it in its startup).</summary>
        public bool ChainUp => _live;
        /// <summary>True while anything of the cue in use is drawn this frame: at the sight, or at her muzzles.</summary>
        public bool Drawn { get; private set; }
        /// <summary>The cue's opacity now, 0 to 1 (it comes up, and lingers after the chain).</summary>
        public float Alpha { get; private set; }
        /// <summary>The sight's point, in the interface's screen pixels from the top left: where the cursor is.</summary>
        public Vector2 Sight { get; private set; }
        /// <summary>The press waited for (2 on), the window's length, its clean part, the judge's clock, the cues' clock.</summary>
        public int Press { get; private set; }
        public float Window { get; private set; }
        public CleanPart Clean { get; private set; }
        public float SinceOpened { get; private set; } = -1f;
        public float Since { get; private set; } = -1f;
        /// <summary>True while the window for the press is open.</summary>
        public bool WindowOpen { get; private set; }
        /// <summary>True while the judge would call a press now clean (<see cref="PlayerCharacter.RecastPressWouldBeClean"/>): the cue's light.</summary>
        public bool Lit { get; private set; }
        /// <summary>
        /// True while the cue in use, as drawn, says a press now is clean: the chamber in the notch, the ring inside the band, the
        /// muzzles at their brightest. False with the bar only.
        /// </summary>
        public bool SaysClean { get; private set; }

        /// <summary>The cylinder: its chambers, their spacing, the notch's width (degrees), the turn as drawn, the chamber waited for and where it stands.</summary>
        public int Chambers { get; private set; }
        public float Spacing { get; private set; }
        public float Notch { get; private set; }
        public float Turn { get; private set; }
        public int LiveChamber { get; private set; } = -1;
        public float LiveAngle => CylinderCue.AngleOf(LiveChamber, Turn, Spacing);
        /// <summary>True while the chamber waited for is in the notch, as drawn (whichever cue is in use).</summary>
        public bool CylinderSays { get; private set; }
        /// <summary>The ring: its radius now in layout pixels (before scaling), and whether it is inside the band (whichever cue is in use).</summary>
        public float RingRadius { get; private set; }
        public bool RingSays { get; private set; }
        /// <summary>The hammer: its light now, 0 to 1 of the brightest, and whether it is at the brightest (whichever cue is in use).</summary>
        public float HammerLevel { get; private set; }
        public bool HammerSays { get; private set; }
        /// <summary>Work order 144: the sight's arm has its catch under the teeth zone, the window open (whichever cue is in use; <see cref="SightArm"/>).</summary>
        public bool SightSays { get; private set; }

        /// <summary>Clicks played since it started, and the latest's pitch.</summary>
        public int Clicks { get; private set; }
        public float LastClickPitch { get; private set; }
        /// <summary>True while the cue's name shows at the sight (after the temporary key).</summary>
        public bool NameShowing => Time.unscaledTime - _nameAt < V.NameSeconds;
        /// <summary>The words that show.</summary>
        public string NameText => _nameContent != null ? _nameContent.text : "";

        /// <summary>
        /// True while the click is on and the cue running: the clean window's old tick (work order 037, <see cref="PlayerSigns"/>)
        /// stands aside, so the moment has one sound, not two. With the bar only and no click stored it is false: the tick as today.
        /// </summary>
        public static bool TakesTheTick => Current != null && Current.isActiveAndEnabled && TimingCueSettings.SoundNow;

        PlayerCharacter _player;
        PlayerHealth _health;
        bool _live;
        int _slot = -1, _shots, _count;
        bool _openingMove;
        bool _litBefore;
        float _startAt = float.NegativeInfinity, _endedAt = float.NegativeInfinity;
        bool _endMissed;
        int _endPress;
        // The cylinder's turn between windows (game time): from where it stood at a press to where the next chamber's turn begins.
        bool _turning;
        float _fromTurn, _pressAt = float.NegativeInfinity, _lockFor;
        float _flashAt = float.NegativeInfinity;
        int _flashChamber = -1;
        // The ring that was pressed, fading.
        float _goneRadius, _goneAt = float.NegativeInfinity;
        bool _goneClean;
        // The name after the temporary key.
        float _nameAt = float.NegativeInfinity;
        GUIContent _nameContent;
        static readonly GUIContent[] s_Names = MakeNames();
        Action<PlayerReport> _onReport;
        PlayerCharacter _listening;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static TimingAtTheSight Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("TimingAtTheSight");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<TimingAtTheSight>();
            return Current;
        }

        static GUIContent[] MakeNames()
        {
            var names = new GUIContent[TimingCues.Count * 2];
            for (int c = 0; c < TimingCues.Count; c++)
                for (int s = 0; s < 2; s++)
                    names[c * 2 + s] = new GUIContent("TIMING: " + TimingCues.Names[c].ToUpperInvariant() + (s == 1 ? "  +  CLICK" : "  (NO CLICK)"));
            return names;
        }

        void Awake()
        {
            if (Values == null) Values = TimingAtTheSightValues.Live;
            _onReport = OnReport;
        }

        void OnDisable()
        {
            HideHammer();
            Drawn = false;
        }

        void OnDestroy()
        {
            Listen(null);
            DisposeHammer();
            DisposeClick();
            if (Current == this) Current = null;
        }

        void LateUpdate() => Tick();

        /// <summary>The frame's work: what the cue shows now. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            float real = Time.time; // the cue's own moments on game time: a hit stop holds a lock still, a pause holds it all
            if (_player == null) Find();
            ReadKey();
            TimingAtTheSightValues v = V;
            Cue = TimingCueSettings.CueNow;
            bool alive = _player != null && (_health == null || _health.IsAlive);
            bool on = TimingAtTheSightValues.On && alive;
            PlayerCharacter p = _player;
            int slot = on ? p.RecastSlot : -1;
            bool chain = slot >= 0 && p.RecastCount > 1 && slot < p.Skills.Count && p.Skills[slot] != null && RecastChain.HasCleanPress(p.Skills[slot]);
            if (chain && (!_live || slot != _slot || p.RecastShots < _shots)) StartChain(p, slot, real);
            else if (!chain && _live) EndChain(p, real);
            if (_live) Follow(p, real, v);
            else Linger(real, v);

            // The click: as the clean part opens, judged as the press would be; never while a screen holds the game.
            bool held = PlayerSigns.ScreenUp;
            bool lit = _live && Lit;
            if (lit && !_litBefore && !held && TimingCueSettings.SoundNow) Click(Press, v);
            _litBefore = lit;

            Alpha = _live ? (v.FadeInSeconds > 0f ? Mathf.Clamp01((real - _startAt) / v.FadeInSeconds) : 1f)
                  : v.LingerSeconds > 0f ? Mathf.Clamp01(1f - (real - _endedAt) / v.LingerSeconds) : 0f;
            bool shows = on && !held && Alpha > 0f && Cue != TimingCue.BarOnly;
            SaysClean = Cue == TimingCue.Cylinder ? CylinderSays : Cue == TimingCue.Ring ? RingSays : Cue == TimingCue.Hammer ? HammerSays : Cue == TimingCue.Sight && SightSays;
            if ((shows && Cue != TimingCue.Hammer) || NameShowing) Sight = SightNow(p);
            bool hammer = shows && Cue == TimingCue.Hammer && HammerLevel > 0f;
            if (hammer) DrawHammer(p, HammerLevel, v);
            else HideHammer();
            Drawn = shows && (Cue != TimingCue.Hammer || hammer);
        }

        void Find()
        {
            _player = FindAnyObjectByType<PlayerCharacter>();
            _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            Listen(_player);
            _live = false;
            _slot = -1;
        }

        void Listen(PlayerCharacter p)
        {
            if (_listening != null) _listening.Reported -= _onReport;
            _listening = p;
            if (p != null) p.Reported += _onReport;
        }

        // ---------------- The chain ----------------

        void StartChain(PlayerCharacter p, int slot, float real)
        {
            TimingAtTheSightValues v = V;
            SkillDefinition skill = p.Skills[slot];
            RecastLine line = p.RecastLine;
            _live = true;
            _slot = slot;
            _shots = p.RecastShots;
            _count = Mathf.Max(2, line.Any ? line.Count : p.RecastCount);
            _openingMove = skill.OpeningMove;
            _startAt = real;
            _litBefore = false;
            Chambers = CylinderCue.Chambers(_count, _openingMove);
            Spacing = CylinderCue.Spacing(Chambers, v);
            // The notch, fixed for the chain: narrow enough for the press that must begin its turn farthest back (work order 069's beats).
            float reach = 0f;
            for (int press = 2; press <= _count; press++)
            {
                RecastBeat beat = line.Beat(press);
                if (beat.Any && beat.Clean.Any) reach = Mathf.Max(reach, CylinderCue.Reach(beat.Clean, ComboLine.Lead(skill, p.Kit, press)));
            }
            Notch = CylinderCue.Notch(Spacing, reach, v);
            // A chain that opens with a shot: its first chamber under the hammer, the cylinder turning on to the next as the shot goes.
            _turning = !_openingMove && _shots <= 1;
            _fromTurn = 0f;
            Turn = 0f;
            _pressAt = real;
            _lockFor = 0f;
            _flashAt = float.NegativeInfinity;
            _flashChamber = -1;
            _goneAt = float.NegativeInfinity;
            Follow(p, real, v);
        }

        void EndChain(PlayerCharacter p, float real)
        {
            _live = false;
            _slot = -1;
            _endedAt = real;
            _endPress = Press;
            _endMissed = p != null && p.RecastTimingOf(Press) == RecastTiming.Missed;
            _fromTurn = Turn;
            WindowOpen = Lit = false;
            CylinderSays = RingSays = HammerSays = SightSays = false;
            HammerLevel = 0f;
            SinceOpened = Since = -1f;
        }

        void Follow(PlayerCharacter p, float real, TimingAtTheSightValues v)
        {
            _shots = p.RecastShots;
            if (_shots >= _count)
            {
                // Every press made, the last shot still to go off: nothing waits. The cylinder stays where the press left it.
                Press = 0;
                Clean = default;
                Window = 0f;
                SinceOpened = Since = -1f;
                WindowOpen = Lit = false;
                LiveChamber = -1;
                Turn = Eased(_fromTurn, real, v);
                CylinderSays = RingSays = HammerSays = SightSays = false;
                RingRadius = v.RingMost;
                HammerLevel = 0f;
                return;
            }
            Press = _shots + 1;
            Clean = p.RecastCleanPart;
            Window = p.RecastWindowSeconds;
            SinceOpened = p.RecastSinceOpened;
            Since = TimingCueClock.Since(SinceOpened, p.RecastSincePress, p.RecastLead);
            WindowOpen = p.IsRecastWindowOpen;
            Lit = WindowOpen && p.RecastPressWouldBeClean;
            CleanPart clean = Clean;

            // The cylinder: the chamber waited for at its angle, the others a spacing apart; between windows its quick turn.
            LiveChamber = CylinderCue.ChamberOf(Press, _openingMove);
            float live = clean.Any ? CylinderCue.LiveAngle(Since, clean, Notch) : -Spacing * 0.5f;
            float target = CylinderCue.Turn(live, LiveChamber, Spacing);
            Turn = Eased(target, real, v);
            CylinderSays = WindowOpen && clean.Any && CylinderCue.InNotch(LiveAngle, Notch, CylinderCue.Speed(clean, Notch));

            // The ring.
            RingRadius = RingCue.Radius(Since, clean, v);
            RingSays = WindowOpen && clean.Any && RingCue.InBand(RingRadius, RingCue.Speed(clean, v), v);

            // The hammer: on the judge's clock alone (dark until the window opens).
            HammerLevel = WindowOpen ? HammerCue.Level(SinceOpened, Window, clean, v) : 0f;
            HammerSays = HammerLevel >= HammerCue.Brightest;

            // Work order 144: the sight's arm, on the same clock (SightArm).
            SightSays = WindowOpen && clean.Any && SightArm.SaysClean(SinceOpened, Window, clean, p.RecastLead, SightValues.Live);
        }

        /// <summary>
        /// The cylinder's turn as drawn: where the rules put it whenever a window is open (to the frame); between windows, from
        /// where it stood at the press, held still a moment after a clean one, then a quick turn to the rules' place.
        /// </summary>
        float Eased(float target, float real, TimingAtTheSightValues v)
        {
            if (!_turning || SinceOpened >= 0f) { _turning = false; return target; }
            float t = real - _pressAt;
            if (t < _lockFor) return _fromTurn;
            float k = v.IndexSeconds > 0f ? (t - _lockFor) / v.IndexSeconds : 1f;
            if (k >= 1f) { _turning = false; return target; }
            float e = 1f - (1f - k) * (1f - k) * (1f - k); // eases out: fast, then settling
            return _fromTurn + (target - _fromTurn) * e;
        }

        /// <summary>After the chain: what the last press was stays a moment; a miss slips back.</summary>
        void Linger(float real, TimingAtTheSightValues v)
        {
            if (!_endMissed || v.LingerSeconds <= 0f) return;
            float k = Mathf.Clamp01((real - _endedAt) / v.LingerSeconds);
            Turn = _fromTurn - v.SlipBack * (1f - (1f - k) * (1f - k));
        }

        void OnReport(PlayerReport r)
        {
            if (r.Kind != PlayerReportKind.RecastPressed || !_live || r.Slot != _slot) return;
            float real = Time.time;
            bool clean = r.Clean || r.Timing == RecastTiming.Clean;
            // The cylinder: turns on from where it was drawn; a clean press locks its chamber in the notch first, lit.
            _fromTurn = Turn;
            _pressAt = real;
            _lockFor = clean ? V.LockSeconds : 0f;
            _turning = true;
            if (clean)
            {
                _flashAt = real;
                _flashChamber = CylinderCue.ChamberOf(r.Shot, _openingMove);
            }
            // The ring: the one pressed fades where it was, lit if clean; the next one begins.
            _goneRadius = RingRadius;
            _goneAt = real;
            _goneClean = clean;
        }

        // ---------------- The sight ----------------

        /// <summary>Where the cursor is, in the interface's pixels from the top left: the mouse itself, or (no real mouse read) her cursor's ray through the camera.</summary>
        Vector2 SightNow(PlayerCharacter p)
        {
            float height = InterfaceStyle.ScreenHeight;
            if (PlayerInputDriver.ReadRealDevices && Mouse.current != null && InterfaceStyle.ScreenForPictures.x <= 0)
            {
                Vector2 m = Mouse.current.position.ReadValue();
                return new Vector2(m.x, height - m.y);
            }
            Camera cam = Camera.main;
            if (cam == null || p == null) return Sight;
            Vector3 s = cam.WorldToScreenPoint(p.CursorOnItsRay);
            if (s.z <= 0f) return Sight;
            return new Vector2(s.x, height - s.y);
        }

        // ---------------- The temporary key ----------------

        void ReadKey()
        {
            if (!TimingAtTheSightValues.On || PlayerInputDriver.HeldByChoice || PlayerInputDriver.HeldByScreen) return;
            Keyboard k = PlayerInputDriver.ReadRealDevices ? Keyboard.current : TestKeyboard;
            if (k == null || !k[StepKey].wasPressedThisFrame) return;
            PressStepKey(k.shiftKey.isPressed);
        }

        /// <summary>
        /// The temporary key (<see cref="StepKey"/>): the next cue, kept as the setting is; with Shift the click on or off. The cue's
        /// name shows at the sight for a second. Public for tests.
        /// </summary>
        public void PressStepKey(bool shift)
        {
            if (!TimingAtTheSightValues.On) return;
            if (shift) TimingCueSettings.SetSound(!TimingCueSettings.SoundNow);
            else TimingCueSettings.SetCue(TimingCues.Next(TimingCueSettings.CueChosen));
            TimingCue cue = TimingCueSettings.CueNow;
            _nameContent = s_Names[(int)cue * 2 + (TimingCueSettings.SoundNow ? 1 : 0)];
            _nameAt = Time.unscaledTime;
            if (_player != null) Sight = SightNow(_player);
        }
    }
}
