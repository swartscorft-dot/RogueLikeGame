using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// Two signs she cannot miss (work order 037, Kristian's session of 2026-11-20). In that session the last press of
    /// Triple Tap was clean 1 time of 79 and Backstep's 0 of 4, and asked about it he said "Jeg visste ikke at det fantes":
    /// he did not know a clean press existed, for the game said so only afterwards and only on success. And the focus
    /// gauge stood full for 79 s on average before he spent it; asked what he wanted, he chose a clear sign when it is full.
    /// It only shows and sounds what already happens: it reads the character (<see cref="PlayerCharacter.RecastSinceOpened"/>,
    /// <see cref="PlayerCharacter.RecastPressWouldBeClean"/>, <see cref="PlayerCharacter.IsFocusFull"/>) and changes nothing
    /// in a fight: not when a press is clean, what it gives, how focus fills or which key spends it.
    ///
    /// 1. The clean window (<see cref="PlayerSignValues.CleanWindow"/>). While a recast chain's next press can be clean
    /// (Triple Tap's last, Backstep's second), a thin ring in her colour lies on the floor round her and closes toward a
    /// fixed band (<see cref="CleanWindowSign"/>): the ring is inside the band exactly while a press is clean, and the band
    /// is lit exactly then, as the game judges that press. Drawn with the effects' own pieces (<see cref="CombatFx"/>),
    /// following her, on the chain's clock (it stands still in a pause, and runs on through a hit stop as the judge's
    /// window does: work order 038), and gone the moment the chain ends, however it ends. On the skill bar the key shows the same in small (<see cref="DrawKeySign"/>). A short,
    /// quiet tick (the placeholder sounds' tick, <see cref="PlaceholderSound.Menu"/>) when the clean part opens, so the
    /// rhythm can be learnt by ear; and the first time in a session, a line in the HUD's style: <see cref="CleanWindowSign.Line"/>.
    ///
    /// 2. Focus that is full (<see cref="PlayerSignValues.FocusFull"/>). The moment it fills: a flash on her in the full
    /// gauge's gold, and the word FOCUS with the key it is spent with, as the bindings have it, over her in the style of
    /// <see cref="TimingFeedback"/>'s words. Its sound is the one the placeholder sounds already play then
    /// (<see cref="PlaceholderSound.FocusFull"/>), unchanged. As long as it stays full: a slow, calm pulse round her
    /// (<see cref="FocusSign"/>), and the gauge in the HUD pulsing with the key beside it (<see cref="DrawFocusSign"/>).
    /// All of it stops the moment she spends it, and none of it shows while a choice, an end screen or the pause menu is up.
    ///
    /// The two switches are on by default and reachable in the Inspector while playing (<see cref="Values"/>), as the
    /// effects' is; off gives the game exactly as it was. The effects' switch (<see cref="CombatFxValues.On"/>) still rules
    /// what is drawn on the floor, and the sounds' (<see cref="PlaceholderSoundValues.On"/>) what is heard.
    ///
    /// It installs itself when Play starts under the same rule as <see cref="TimingFeedback"/> (never in a test run);
    /// tests make one with <see cref="Ensure"/>.
    /// </summary>
    public sealed class PlayerSigns : MonoBehaviour
    {
        const float GroundHeight = 0.06f;  // m, as the effects' bands on the ground
        const float RingLift = 0.075f;     // m: the ring lies over its band
        const float ThinWidth = 0.22f;     // m: the width CombatFx draws a thin ring at as made (its outline's)
        const float RingSize = 2.2f;       // m: the ring as made; it is scaled to where it is each frame
        const float PulseSize = 2f;        // m: the pulse ring as made, the same way
        const float HoldMost = 3600f;      // s: a piece held longer than this lets go by itself
        const float OverHead = 1.7f;       // m over the top of her: where the word goes (TimingFeedback's)
        const float Outline = 2f;          // px of dark outline round the word (TimingFeedback's)
        const float StripHeight = 5f;      // px: the key's strip, in the gap over its box
        const float BadgeWidth = 30f;      // px: the key beside the gauge
        const float LineTop = 526f;        // px of a view 720 high: over the HUD's own line over the skill bar
        const float LineHeight = 26f;

        // Her colours: the effects' own warm orange for the clean window (never an enemy's red or blue), the full gauge's
        // gold for focus.
        static readonly Color s_HerRim = new Color(1f, 0.6f, 0.15f);      // CombatFxValues.HerRim
        static readonly Color s_HerCore = new Color(1f, 1f, 0.92f);       // CombatFxValues.HerCore
        static readonly Color s_FocusRim = new Color(1f, 0.82f, 0.25f);
        static readonly Color s_FocusCore = new Color(1f, 0.97f, 0.75f);
        static readonly Color s_FocusHud = new Color(1f, 0.9f, 0.3f, 1f); // SkillBar's full gauge
        static readonly Color s_KeyDim = new Color(1f, 0.6f, 0.15f, 0.45f);
        static readonly Color s_KeyLit = new Color(1f, 0.85f, 0.45f, 1f);
        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.65f);      // RunHud's
        static readonly Color s_Accent = new Color(0.25f, 0.85f, 0.85f, 1f); // RunHud's
        static readonly Color s_Text = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Shadow = new Color(0f, 0f, 0f, 0.9f);

        // Glows (the shot effects' start values are 1.8 and 4): the ring plain, the band dim until it lights, the pulse low.
        const float RingRimGlow = 1.4f, RingCoreGlow = 2.2f;
        const float DimRimGlow = 0.5f, DimCoreGlow = 0.8f;
        static readonly float[] s_PulseRim = { 0.55f, 0.38f, 0.22f };
        static readonly float[] s_PulseCore = { 0.85f, 0.55f, 0.32f };

        const string FocusFullToday = "FOCUS FULL: press Z"; // SkillBar's words, as they were

        static readonly OncePerSession s_Line = new OncePerSession();
        static readonly FeedbackValues s_Look = new FeedbackValues(); // the timing feedback's words: their pop, rise and fade

        /// <summary>The one running now, or null.</summary>
        public static PlayerSigns Current { get; private set; }

        /// <summary>The switches and numbers, defaults in code (work order 037: first guesses).</summary>
        public PlayerSignValues Values = new PlayerSignValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        /// <summary>The clean window's tick was asked for. For tests; the sound itself goes through <see cref="PlaceholderSounds"/>.</summary>
        public event Action TickAsked;
        /// <summary>The gauge filled and its flash and word went up. For tests.</summary>
        public event Action FocusFilled;

        // ---------------- What it shows, worked out every frame (also without a window, for tests) ----------------

        /// <summary>True while the clean window's ring is up.</summary>
        public bool ChainSignUp { get; private set; }
        /// <summary>The slot of the chain it is up for, or -1.</summary>
        public int ChainSlot { get; private set; } = -1;
        /// <summary>The ring's outer edge now, in metres from her.</summary>
        public float RingRadius { get; private set; }
        /// <summary>True while the band is lit: a press now would be clean.</summary>
        public bool BandLit { get; private set; }
        /// <summary>The key's mark, 1 to 0, where the ring is; and its band, from 0 to here.</summary>
        public float KeyMark { get; private set; }
        public float KeyBand { get; private set; }
        /// <summary>Ticks asked for since it started.</summary>
        public int Ticks { get; private set; }
        /// <summary>The pieces on the floor: the ring, the band dim and lit; null when not drawn.</summary>
        public CombatFx Ring => _ring;
        public CombatFx Band => _band;
        public CombatFx LitBand => _litBand;
        /// <summary>True while the once-a-session line shows.</summary>
        public bool LineShowing => Values.CleanWindow && s_Line.Showing(Time.unscaledTime) && !ScreenUp;
        public string LineText => CleanWindowSign.Line;

        /// <summary>True while the focus sign is up: the gauge is full and the switch on.</summary>
        public bool FocusSignUp { get; private set; }
        /// <summary>Flashes since it started: one each time the gauge filled.</summary>
        public int Flashes { get; private set; }
        /// <summary>The word over her: its text, and whether it shows now.</summary>
        public string WordText { get; private set; } = "";
        public bool WordShowing { get; private set; }
        public float WordAge { get; private set; }
        /// <summary>The pulse: drawn now, its radius and brightness step (0 brightest).</summary>
        public bool PulseShown { get; private set; }
        public float PulseRadius { get; private set; }
        public int PulseLevel { get; private set; } = -1;
        /// <summary>The opacity of the gauge's frame and key now; true while they are drawn.</summary>
        public float GaugeAlpha { get; private set; }
        public bool GaugeSignShown => FocusSignUp && !ScreenUp;
        /// <summary>The pieces of the focus sign live now (flash and pulse).</summary>
        public int FocusPieces
        {
            get
            {
                int n = 0;
                foreach (CombatFx p in _pulse) if (p != null) n++;
                if (_flashRing != null) n++;
                if (_flashStar != null) n++;
                return n;
            }
        }
        /// <summary>The focus key as the bindings have it now.</summary>
        public string FocusKeyLabel => _focusLabel;

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        Func<bool> _holds;

        CombatFx _ring, _band, _litBand;
        MeshRenderer _bandRenderer, _litRenderer;
        bool _chainPiecesMade;

        readonly CombatFx[] _pulse = new CombatFx[3];
        readonly MeshRenderer[] _pulseRenderers = new MeshRenderer[3];
        bool _pulseMade;
        CombatFx _flashRing, _flashStar;
        bool _fullSeen;
        float _fullSince;
        float _wordBorn = float.NegativeInfinity;
        float _wordHeight = 3f;

        Key _focusKey = Key.None;
        string _focusLabel = "";
        string _focusFullText = FocusFullToday;

        GUIStyle _wordStyle;
        GUIStyle _lineStyle;
        GUIStyle _badgeStyle;
        readonly GUIContent _content = new GUIContent();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            Current = null;
            ForgetSessionLine();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static PlayerSigns Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("PlayerSigns");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<PlayerSigns>();
            return Current;
        }

        /// <summary>The line is shown once a session; this forgets it was, as at the start of one. For tests.</summary>
        public static void ForgetSessionLine()
        {
            s_Line.Forget();
            LinesShown = 0;
        }

        /// <summary>True once the line has been shown this session.</summary>
        public static bool SessionLineDone => s_Line.Done;
        /// <summary>Times the line was put up since Play started (or since <see cref="ForgetSessionLine"/>). For tests.</summary>
        public static int LinesShown { get; private set; }

        void Awake() => _holds = HoldsPieces;

        void OnDisable()
        {
            StopChain();
            StopFocus();
        }

        void OnDestroy()
        {
            StopChain();
            StopFocus();
            if (Current == this) Current = null;
        }

        bool HoldsPieces() => this != null && isActiveAndEnabled;

        /// <summary>A choice, an end screen, the pause menu or the screen between runs is up.</summary>
        static bool ScreenUp
        {
            get
            {
                if (PlayerInputDriver.HeldByChoice || PlayerInputDriver.HeldByScreen) return true;
                GeneratedLevels levels = GeneratedLevels.Current;
                return levels != null && (levels.Paused || levels.Choice != GeneratedLevels.ChoiceKind.None
                                          || levels.Ending != GeneratedLevels.EndKind.None || levels.BetweenRunsUp);
            }
        }

        // ---------------- Every frame ----------------

        void LateUpdate() => Tick();

        /// <summary>The frame's work: what both signs show now. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            if (_player == null) Find();
            ReadFocusKey();
            bool alive = _player != null && (_health == null || _health.IsAlive);
            bool held = ScreenUp;
            Chain(alive);
            Focus(alive, held);
        }

        void Find()
        {
            StopChain();
            StopFocus();
            _fullSeen = false;
            _player = FindAnyObjectByType<PlayerCharacter>();
            _health = _player != null ? _player.GetComponent<PlayerHealth>() : null;
            _driver = _player != null ? _player.GetComponent<PlayerInputDriver>() : null;
        }

        /// <summary>The focus key's label, made again only when the binding changes.</summary>
        void ReadFocusKey()
        {
            InputBindings b = _driver != null ? _driver.ActiveBindings : null;
            Key key = b != null ? b.Focus : Key.Z;
            if (key == _focusKey && _focusLabel.Length > 0) return;
            _focusKey = key;
            _focusLabel = key == Key.None ? "" : new InputBinding(key).Label;
            _focusFullText = "FOCUS FULL: press " + _focusLabel;
        }

        // ---------------- 1. The clean window ----------------

        void Chain(bool alive)
        {
            int slot = _player != null ? _player.RecastSlot : -1;
            float share = _player != null ? _player.RecastCleanShare : 0f;
            float window = _player != null ? _player.RecastWindowSeconds : 0f;
            float since = _player != null ? _player.RecastSinceOpened : -1f;
            bool up = Values.CleanWindow && alive && CleanWindowSign.Shows(slot >= 0, share, window, since);
            if (!up)
            {
                if (ChainSignUp) StopChain();
                return;
            }
            if (!ChainSignUp || slot != ChainSlot)
            {
                StopChain();
                ChainSignUp = true;
                ChainSlot = slot;
            }

            RingRadius = CleanWindowSign.RingRadius(since, window, share, Values);
            KeyMark = CleanWindowSign.MarkOf(RingRadius, share, Values);
            KeyBand = CleanWindowSign.MarkOf(Values.BandOuter, share, Values);
            bool lit = _player.RecastPressWouldBeClean;
            if (lit && !BandLit)
            {
                // The clean part opened: the tick, and the first time in a session the line.
                Ticks++;
                if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.Menu);
                TickAsked?.Invoke();
                if (s_Line.TryShow(Time.unscaledTime, Values.LineSeconds)) LinesShown++;
            }
            BandLit = lit;

            // On the floor, through the effects' pieces while they are on.
            CombatEffects fx = CombatEffects.Current;
            bool draw = fx != null && fx.Values.On;
            if (!draw && _chainPiecesMade) DestroyChainPieces();
            if (draw && !_chainPiecesMade) MakeChainPieces();
            if (_ring != null)
            {
                float k = RingRadius / RingSize;
                _ring.transform.localScale = new Vector3(k, 1f, k);
            }
            if (_bandRenderer != null) _bandRenderer.enabled = !lit;
            if (_litRenderer != null) _litRenderer.enabled = lit;
        }

        void MakeChainPieces()
        {
            _chainPiecesMade = true;
            // The band: one thin ring as CombatFx makes it, widened once to the band (its outer edge at BandOuter, its width
            // from BandInner); the ring closing over it is scaled to where it is every frame. Both are the keg's closing
            // ring (FxMoment.KegLying): the shape her own lying keg is drawn with, held still while the sign is up.
            float width = Mathf.Max(0.01f, Values.BandOuter - Values.BandInner);
            float k = width / ThinWidth;
            float size = Values.BandOuter / k;
            _band = Piece(FxShape.Closing, FxMoment.KegLying, size, GroundHeight, s_HerRim, s_HerRim, DimRimGlow, DimCoreGlow, "PlayerSign_Band");
            _litBand = Piece(FxShape.Closing, FxMoment.KegLying, size, GroundHeight, s_HerRim, s_HerCore, Glow(true), Glow(false), "PlayerSign_BandLit");
            if (_band != null) _band.transform.localScale = new Vector3(k, 1f, k);
            if (_litBand != null) _litBand.transform.localScale = new Vector3(k, 1f, k);
            _bandRenderer = _band != null ? _band.GetComponent<MeshRenderer>() : null;
            _litRenderer = _litBand != null ? _litBand.GetComponent<MeshRenderer>() : null;
            _ring = Piece(FxShape.Closing, FxMoment.KegLying, RingSize, RingLift, s_HerRim, s_HerCore, RingRimGlow, RingCoreGlow, "PlayerSign_Ring");
        }

        static float Glow(bool rim)
        {
            CombatEffects fx = CombatEffects.Current;
            if (fx == null) return rim ? ShotFxSettings.StartRimGlow : ShotFxSettings.StartCoreGlow;
            return rim ? fx.Values.RimGlow : fx.Values.CoreGlow;
        }

        void DestroyChainPieces()
        {
            Gone(ref _ring);
            Gone(ref _band);
            Gone(ref _litBand);
            _bandRenderer = null;
            _litRenderer = null;
            _chainPiecesMade = false;
        }

        void StopChain()
        {
            DestroyChainPieces();
            ChainSignUp = false;
            ChainSlot = -1;
            BandLit = false;
            RingRadius = 0f;
            KeyMark = 0f;
            KeyBand = 0f;
        }

        /// <summary>A held piece on the floor round her feet, following her; null when the effects do not draw it.</summary>
        CombatFx Piece(FxShape shape, FxMoment moment, float size, float lift, Color rim, Color core, float rimGlow, float coreGlow, string name)
        {
            CombatEffects fx = CombatEffects.Current;
            if (fx == null || !fx.Values.On || _player == null) return null;
            Vector3 feet = _player.Position;
            var p = new FxPiece
            {
                Shape = shape, Moment = moment, Tier = FxTier.Key, At = new Vector3(feet.x, lift, feet.z), Size = size, Seconds = 0.1f,
                Rim = rim, Core = core, RimGlow = rimGlow, CoreGlow = coreGlow,
                Follow = _player.transform, FollowOffset = Vector3.up * (lift - feet.y), Holds = _holds, HoldMost = HoldMost,
            };
            p.End = p.At;
            CombatFx made = fx.Put(p);
            if (made != null) made.gameObject.name = name;
            return made;
        }

        static void Gone(ref CombatFx piece)
        {
            if (piece != null)
            {
                piece.gameObject.SetActive(false);
                Destroy(piece.gameObject);
            }
            piece = null;
        }

        // ---------------- 2. Focus that is full ----------------

        void Focus(bool alive, bool held)
        {
            bool full = _player != null && _player.IsFocusFull;
            float now = Time.time;
            if (full && !_fullSeen)
            {
                _fullSince = now;
                if (Values.FocusFull && alive) Filled(now);
            }
            _fullSeen = full;
            bool up = Values.FocusFull && full && alive;
            if (!up)
            {
                if (FocusSignUp) StopFocus();
                return;
            }
            FocusSignUp = true;

            float since = now - _fullSince;
            float phase = FocusSign.Phase(since, Values);
            PulseLevel = FocusSign.Level(phase, Values);
            PulseShown = PulseLevel >= 0 && !held;
            PulseRadius = PulseLevel >= 0 ? FocusSign.RingRadius(phase, Values) : 0f;
            GaugeAlpha = FocusSign.GaugeAlpha(since, Values);
            WordAge = now - _wordBorn;
            WordShowing = WordAge >= 0f && WordAge < Values.WordSeconds && !held;

            CombatEffects fx = CombatEffects.Current;
            bool draw = fx != null && fx.Values.On;
            if (!draw && _pulseMade) DestroyPulse();
            if (draw && !_pulseMade) MakePulse();
            float k = PulseRadius / PulseSize;
            for (int i = 0; i < _pulse.Length; i++)
            {
                if (_pulse[i] == null) continue;
                if (_pulseRenderers[i] != null) _pulseRenderers[i].enabled = PulseShown && PulseLevel == i;
                if (PulseShown && PulseLevel == i) _pulse[i].transform.localScale = new Vector3(k, 1f, k);
            }
            // The flash plays out on its own time; under a screen it is not seen.
            if (_flashRing != null) _flashRing.gameObject.SetActive(!held);
            if (_flashStar != null) _flashStar.gameObject.SetActive(!held);
        }

        /// <summary>The moment it fills: the flash on her and the word over her. The sound is the placeholder sounds' own.</summary>
        void Filled(float now)
        {
            Flashes++;
            WordText = FocusSign.Word(_focusLabel);
            _wordBorn = now;
            _wordHeight = TopOfHer() + OverHead;
            CombatEffects fx = CombatEffects.Current;
            if (fx != null && fx.Values.On && _player != null)
            {
                // A ring bursting out at her feet and a star on her chest, as a perfect release's ring and spark are drawn
                // (FxMoment.PerfectRelease, her own moment of timing), in the full gauge's gold.
                Vector3 feet = _player.Position;
                var ring = new FxPiece
                {
                    Shape = FxShape.Ring, Moment = FxMoment.PerfectRelease, Tier = FxTier.Key, At = new Vector3(feet.x, GroundHeight, feet.z),
                    Size = Values.FlashRing, Seconds = Values.FlashRingSeconds, Rim = s_FocusRim, Core = s_FocusCore,
                    RimGlow = fx.Values.RimGlow, CoreGlow = Mathf.Lerp(1f, fx.Values.CoreGlow, 0.6f),
                    Follow = _player.transform, FollowOffset = Vector3.up * (GroundHeight - feet.y),
                };
                ring.End = ring.At;
                _flashRing = fx.Put(ring);
                if (_flashRing != null) _flashRing.gameObject.name = "PlayerSign_FocusFlash";
                Vector3 chest = feet + Vector3.up * fx.Values.HerChest;
                var star = new FxPiece
                {
                    Shape = FxShape.Spark, Moment = FxMoment.PerfectRelease, Tier = FxTier.Key, At = chest, End = chest,
                    Size = Values.FlashStar, Seconds = Values.FlashStarSeconds, Rim = s_FocusRim, Core = s_FocusCore,
                    RimGlow = fx.Values.RimGlow, CoreGlow = fx.Values.CoreGlow, TowardCamera = 0.6f,
                    Follow = _player.transform, FollowOffset = chest - feet,
                };
                _flashStar = fx.Put(star);
                if (_flashStar != null) _flashStar.gameObject.name = "PlayerSign_FocusStar";
            }
            FocusFilled?.Invoke();
        }

        void MakePulse()
        {
            _pulseMade = true;
            for (int i = 0; i < _pulse.Length; i++)
            {
                _pulse[i] = Piece(FxShape.Closing, FxMoment.KegLying, PulseSize, GroundHeight, s_FocusRim, s_FocusCore,
                                  s_PulseRim[i], s_PulseCore[i], "PlayerSign_FocusPulse" + i);
                _pulseRenderers[i] = _pulse[i] != null ? _pulse[i].GetComponent<MeshRenderer>() : null;
                if (_pulseRenderers[i] != null) _pulseRenderers[i].enabled = false;
            }
        }

        void DestroyPulse()
        {
            for (int i = 0; i < _pulse.Length; i++)
            {
                Gone(ref _pulse[i]);
                _pulseRenderers[i] = null;
            }
            _pulseMade = false;
        }

        void StopFocus()
        {
            DestroyPulse();
            Gone(ref _flashRing);
            Gone(ref _flashStar);
            FocusSignUp = false;
            PulseShown = false;
            PulseLevel = -1;
            PulseRadius = 0f;
            GaugeAlpha = 0f;
            WordShowing = false;
            _wordBorn = float.NegativeInfinity;
        }

        /// <summary>The height of the top of what is drawn for her over her feet, as the timing feedback finds it.</summary>
        float TopOfHer()
        {
            if (_player == null) return 2f;
            float top = float.NegativeInfinity;
            foreach (Renderer r in _player.GetComponentsInChildren<Renderer>())
            {
                if (r is LineRenderer || r.GetComponent<CombatFx>() != null) continue;
                top = Mathf.Max(top, r.bounds.max.y);
            }
            if (float.IsNegativeInfinity(top)) return 2f;
            return Mathf.Clamp(top - _player.Position.y, 0.5f, 6f);
        }

        // ---------------- The skill bar's part (SkillBar calls these where it draws) ----------------

        /// <summary>True while the key of this slot shows the clean window: its chain's sign is up.</summary>
        public bool KeySignShown(int slot) => Values.CleanWindow && ChainSignUp && slot == ChainSlot;

        /// <summary>
        /// The key's part of the clean window, over its box on the skill bar (work order 037): a thin strip whose band at the
        /// left end lights while a press is clean, a mark sweeping toward it where the ring is (as the recast bar runs out,
        /// from right to left), and the box framed lit while it is lit. Nothing when the sign is off or not up.
        /// </summary>
        public static void DrawKeySign(Rect box, int slot)
        {
            PlayerSigns s = Current;
            if (s == null || !s.KeySignShown(slot)) return;
            Color before = GUI.color;
            var strip = new Rect(box.x + 4f, box.y - StripHeight - 1f, box.width - 8f, StripHeight);
            Fill(strip, s_Back);
            Fill(new Rect(strip.x, strip.y, strip.width * Mathf.Clamp01(s.KeyBand), strip.height), s.BandLit ? s_KeyLit : s_KeyDim);
            float x = strip.x + strip.width * Mathf.Clamp01(s.KeyMark);
            Fill(new Rect(x - 1.5f, strip.y - 2f, 3f, strip.height + 4f), s.BandLit ? s_Text : s_KeyLit);
            if (s.BandLit) Frame(new Rect(box.x - 2f, box.y - 2f, box.width + 4f, box.height + 4f), s_KeyLit, 2f);
            GUI.color = before;
        }

        /// <summary>
        /// The gauge's part of a full focus (work order 037): a frame round the gauge and the focus key beside it, pulsing
        /// softly once a pulse. Nothing when the sign is off, the gauge not full, or a screen is up.
        /// </summary>
        public static void DrawFocusSign(Rect gauge)
        {
            PlayerSigns s = Current;
            if (s == null || !s.GaugeSignShown) return;
            Color before = GUI.color;
            float a = Mathf.Clamp01(s.GaugeAlpha);
            var c = new Color(s_FocusHud.r, s_FocusHud.g, s_FocusHud.b, a);
            Frame(new Rect(gauge.x - 3f, gauge.y - 3f, gauge.width + 6f, gauge.height + 6f), c, 2f);
            var badge = new Rect(gauge.xMax + 7f, gauge.y - 1f, BadgeWidth, gauge.height + 2f);
            Fill(badge, s_Back);
            Frame(badge, c, 2f);
            if (s._badgeStyle == null)
            {
                s._badgeStyle = new GUIStyle(GUI.skin.label) { fontSize = 13, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, clipping = TextClipping.Overflow };
                s._badgeStyle.normal.textColor = Color.white;
            }
            GUI.color = new Color(s_FocusHud.r, s_FocusHud.g, s_FocusHud.b, Mathf.Lerp(0.6f, 1f, a));
            s._content.text = s._focusLabel;
            GUI.Label(badge, s._content, s._badgeStyle);
            GUI.color = before;
        }

        /// <summary>
        /// The skill bar's words on a full gauge: the focus key as the bindings have it while the sign is on, and the
        /// words as they were ("press Z") while it is off.
        /// </summary>
        public static string FocusFullText
        {
            get
            {
                PlayerSigns s = Current;
                return s != null && s.Values.FocusFull && s._focusLabel.Length > 0 ? s._focusFullText : FocusFullToday;
            }
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
            Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
        }

        // ---------------- Drawing: the word over her and the line ----------------

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            bool word = WordShowing && WordText.Length > 0 && _player != null;
            bool line = LineShowing;
            if (!word && !line) return;
            GUI.depth = 1; // under the skill bar, the HUD and its screens, as the timing feedback's words
            float scale = Screen.height / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            Color before = GUI.color;
            if (word) DrawWord(scale);
            if (line) DrawLine(Screen.width / scale);
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        /// <summary>FOCUS and its key over her, as the timing feedback draws its words: popping, rising and fading, outlined.</summary>
        void DrawWord(float scale)
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            if (_wordStyle == null)
            {
                _wordStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, wordWrap = false, clipping = TextClipping.Overflow };
                _wordStyle.normal.textColor = Color.white;
            }
            FeedbackValues look = s_Look;
            float life = Mathf.Max(0.05f, Values.WordSeconds);
            float alpha = FeedbackRules.Alpha(WordAge, life, look.FadeFrom);
            if (alpha <= 0.001f) return;
            Vector3 p = cam.WorldToScreenPoint(_player.Position + Vector3.up * _wordHeight);
            if (p.z <= 0f) return;
            float rise = look.WordRise * Mathf.Clamp01(WordAge / life);
            float x = p.x / scale, y = (Screen.height - p.y) / scale - rise;
            _wordStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(look.BigWordSize * FeedbackRules.PopScale(WordAge, look.Pop, look.PopSeconds)));
            _content.text = WordText;
            Vector2 size = _wordStyle.CalcSize(_content);
            var r = new Rect(x - size.x * 0.5f, y - size.y * 0.5f, size.x, size.y);
            GUI.color = new Color(s_Shadow.r, s_Shadow.g, s_Shadow.b, s_Shadow.a * alpha);
            for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0) GUI.Label(new Rect(r.x + dx * Outline, r.y + dy * Outline, r.width, r.height), _content, _wordStyle);
            GUI.color = new Color(s_FocusHud.r, s_FocusHud.g, s_FocusHud.b, alpha);
            GUI.Label(r, _content, _wordStyle);
        }

        /// <summary>The once-a-session line, in the HUD's style: a dark box framed in its accent, over the skill bar.</summary>
        void DrawLine(float viewWidth)
        {
            if (_lineStyle == null)
            {
                _lineStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                _lineStyle.normal.textColor = s_Text;
            }
            _content.text = CleanWindowSign.Line;
            float w = _lineStyle.CalcSize(_content).x + 28f;
            var r = new Rect((viewWidth - w) * 0.5f, LineTop, w, LineHeight);
            Fill(r, s_Back);
            Frame(r, s_Accent, 1f);
            GUI.color = Color.white;
            GUI.Label(r, _content, _lineStyle);
        }
    }
}
