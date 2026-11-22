using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>One key's slot on the kinetic strip this frame (work order 045): for the drawing, and for tests.</summary>
    public struct KineticSlot
    {
        /// <summary>The slot, in layout pixels of a view 1080 high (top left 0,0), where it stands (grown while its recast window is open, moved by its kick).</summary>
        public Rect Box;
        /// <summary>The slot's place on the strip, before any growth or kick (work order 063).</summary>
        public Rect Home;
        /// <summary>The box's face, over its key's plate, as drawn (work order 063).</summary>
        public Rect Face;
        public SlotLook Look;
        /// <summary>The skill's slot in her list on this key, or -1.</summary>
        public int Skill;
        /// <summary>The skill's icon (a white mask), or null: then <see cref="ShortName"/> shows with the key.</summary>
        public Texture2D Icon;
        public string Key;
        public string ShortName;
        /// <summary>The cooldown's number ("3.2"), empty when ready; and its sweep, 1 to 0.</summary>
        public string Number;
        public float Sweep;
        /// <summary>Gold pips: the run's upgrades of this skill (work order 017).</summary>
        public int Pips;
        /// <summary>Charges: how many, and how many are ready (work order 015); 0 for a skill of one.</summary>
        public int Charges;
        public int ChargesReady;
        /// <summary>
        /// A recast chain's window open on this key (decision 0005; work order 063 item 2b: the box is lit): where it is now
        /// (0 opened to 1 run out), its clean part's start and end as shares of the window, lit while a press now is clean.
        /// </summary>
        public bool Recast;
        public float RecastNow;
        public float RecastLeft;
        public float CleanFrom;
        public float CleanTo;
        public float CleanShare;
        public bool CleanLit;
        /// <summary>The window's bar across the lit box's face, and the clean part's band on it (layout pixels, before the slant).</summary>
        public Rect RecastBar;
        public Rect Band;
        /// <summary>Its ready flash (work order 063 item 2): the flash's strength, 1 to 0, and how far the box stands kicked along the slant.</summary>
        public float Flash;
        public float Kick;
    }

    /// <summary>
    /// The skill bar in the interface's kinetic skin (decision 0011; work order 045), as part of <see cref="SkillBar"/>. With
    /// <see cref="InterfaceValues.Kinetic"/> on it draws, instead of its boxes, its health and focus row and its cast row:
    ///
    /// - the skills as one slanted strip at the bottom centre, a parallelogram per key in key order: the skill's icon
    ///   tinted white (teal while it is in use or has a recast or a charge waiting, grey on cooldown with its number and a
    ///   sweep), its key on a small white plate under it; a skill with no icon shows a short name with its key; the run's
    ///   upgrades as gold pips; a key with no skill faint. At the strip's ends: the dash on the left with its charges, focus
    ///   on the right, and the cover tag beside the dash while she is in cover;
    /// - at the figure, two thin slanted strokes side by side under her feet: health in red on a dark back, white where it
    ///   was just lost and fading, and focus in teal (in parts for Hair Trigger, glowing when full), with one small italic
    ///   number for health beside them, and the window's or the priming's seconds under focus while one runs.
    ///
    /// Work order 063 (his words of 2026-11-21: larger, higher, under her feet, a little visual when a skill is off
    /// cooldown, and the recast window read on the skill's own box): the strip grows with the master scale and its own
    /// factor and stands lifted from the view's bottom; a cooling box is shaded dark over the share still to run under its
    /// large number; a skill coming off cooldown kicks its box along the slant and flashes its edge teal, once, in real
    /// time, never while paused or a choice is open, never for a reset; a box whose recast window is open is lit, larger,
    /// shows the window running out across it with the clean part as a band that turns white exactly while a press would
    /// be clean, and its key large on its face; the strokes stand a gap in metres under her feet's point.
    ///
    /// The cast bar's blade over her head is the cast bar's own (<see cref="CastBar"/>). What it shows and when is the skill
    /// bar's as before; it is worked out in <see cref="Tick"/> (LateUpdate, and tests) into a list made once, with texts
    /// made once or when what they show changes, and drawn from it on the repaint only.
    /// </summary>
    public sealed partial class SkillBar
    {
        /// <summary>True while it draws in the kinetic skin (the switch is on and she is there); the old boxes and rows are not drawn then.</summary>
        public bool Kinetic { get; private set; }
        /// <summary>The boxes, the health and focus row and the cast row of before are drawn: the skin is off.</summary>
        public bool OldBarsDrawn => Player != null && !Kinetic;
        /// <summary>The strokes under her feet show this frame (she is on screen), and where (layout pixels).</summary>
        public bool StrokesShown { get; private set; }
        public Rect HealthStroke { get; private set; }
        public Rect FocusStroke { get; private set; }
        /// <summary>The strokes' gap under her point (the character's position) this frame, in layout pixels (work order 063).</summary>
        public float StrokesUnder { get; private set; }
        /// <summary>
        /// The lowest her boots show, as the strokes reckon it (the floor under her, <see cref="InterfaceValues.BootReach"/>
        /// toward the camera, <see cref="InterfaceValues.SoleDrop"/> under her point): the world point, and how far under her
        /// point it is on the screen in layout pixels (work order 063's follow-up).
        /// </summary>
        public Vector3 BootsPoint { get; private set; }
        public float BootsUnder { get; private set; }
        /// <summary>The health number's box beside the strokes (layout pixels).</summary>
        public Rect HealthNumberBox { get; private set; }
        public float HealthShare { get; private set; }
        public float FocusShare { get; private set; }
        /// <summary>The white of health just lost: from <see cref="HealthShare"/> up to this, at this opacity.</summary>
        public float LostFrom { get; private set; }
        public float LostAlpha { get; private set; }
        /// <summary>The one number beside the strokes: her health.</summary>
        public string HealthNumber { get; private set; } = "";
        /// <summary>The keys' slots this frame, in key order.</summary>
        public int SlotCount { get; private set; }
        public KineticSlot SlotOf(int key) => key >= 0 && key < SlotCount ? _slots[key] : default;
        /// <summary>The strip's ends this frame (layout pixels): the dash, focus.</summary>
        public Rect LeftEnd => _left;
        public Rect RightEnd => _right;
        /// <summary>The dark plate behind the whole strip this frame (work order 063, item 4; layout pixels), drawn while <see cref="InterfaceValues.StripPlate"/> is on.</summary>
        public Rect StripPlateRect => _plate;
        /// <summary>Ready flashes started since the bar woke (work order 063), and on one key (the dash's is key <see cref="SkillLoadout.KeyCount"/>).</summary>
        public int ReadyFlashes { get; private set; }
        public int ReadyFlashesOn(int key) => key >= 0 && key < _flashCount.Length ? _flashCount[key] : 0;
        /// <summary>The dash's ready flash now, 1 to 0.</summary>
        public float DashFlash { get; private set; }

        readonly KineticSlot[] _slots = new KineticSlot[SkillLoadout.KeyCount];
        readonly float[] _cooldownTotal = new float[SkillLoadout.KeyCount];
        readonly string[] _shortOf = new string[SkillLoadout.KeyCount];
        readonly string[] _nameOf = new string[SkillLoadout.KeyCount];
        readonly int[] _pipsVersion = new int[SkillLoadout.KeyCount];
        readonly int[] _pips = new int[SkillLoadout.KeyCount];
        static readonly Dictionary<int, string> s_Labels = new Dictionary<int, string>();

        // The ready flashes (work order 063): per key and the dash (the last), whether it waited at the last tick and for
        // which skill, when its flash began (real time) and how many it has had.
        readonly bool[] _waited = new bool[SkillLoadout.KeyCount + 1];
        readonly int[] _waitedSkill = new int[SkillLoadout.KeyCount + 1];
        readonly float[] _flashAt = NegativeInfinities(SkillLoadout.KeyCount + 1);
        readonly int[] _flashCount = new int[SkillLoadout.KeyCount + 1];
        int _trackedFrame = -1;
        object _runWas;
        bool _inRunWas;
        int _floorWas = -1;
        int _endingWas;

        float _lastHealth = -1f, _lostFrom, _lostAt = float.NegativeInfinity;
        float _dashTotal;
        int _tickedFrame = -1;
        Rect _left, _right, _cover, _plate;
        string _dashKey = "", _focusKey = "", _dashNumber = "", _stateNumber = "", _stateWord = "";
        int _dashCharges, _dashReady;
        float _dashSweep, _dashKick;
        bool _focusBright, _cooling;
        RunSkillUpgrades _pipsOf;
        // The frame's sizes, grown with the scales (work order 063).
        int _keyFont, _cooldownFont, _shortFont, _numberFont, _stateFont, _bigKeyFont;
        float _plateH, _iconSize, _sweepH, _pipSize, _strip, _fight;

        static float[] NegativeInfinities(int n)
        {
            var a = new float[n];
            for (int i = 0; i < n; i++) a[i] = float.NegativeInfinity;
            return a;
        }

        // Every icon decoded as the bar wakes, not in a frame of play (the fourth review of 2026-11-21, work order 055).
        void Awake() => InterfaceStyle.LoadIcons();

        void LateUpdate() => Tick();

        /// <summary>The frame's work for the kinetic skin: what the strip and the strokes show now. Public for tests, which count what it allocates.</summary>
        public void Tick()
        {
            _tickedFrame = Time.frameCount;
            if (Player == null) Player = FindAnyObjectByType<PlayerCharacter>();
            Kinetic = InterfaceStyle.Kinetic && Player != null;
            if (!Kinetic)
            {
                StrokesShown = false;
                PlateShown = false; // work order 068
                SlotCount = 0;
                _trackedFrame = -1; // its boxes are not drawn: what comes off cooldown now shows nothing later
                return;
            }
            if (Health == null) Health = Player.GetComponent<PlayerHealth>();
            if (_driver == null) _driver = Player.GetComponent<PlayerInputDriver>();
            InterfaceValues v = InterfaceStyle.V;
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            float viewWidth = InterfaceStyle.ScreenWidth / scale;
            Sizes(v);
            InputBindings bindings = _driver != null ? _driver.ActiveBindings : null;
            IReadOnlyList<SkillDefinition> skills = Player.Skills;
            int count = bindings != null && bindings.Skills != null ? Mathf.Min(Player.KeyCount, bindings.Skills.Length) : Player.KeyCount;
            count = Mathf.Clamp(count, 0, _slots.Length);
            SlotCount = count;
            int active = Player.ActiveSkillSlot, recovering = Player.FreeRecoverySlot;
            if (_pipsOf != Player.SkillUpgrades)
            {
                _pipsOf = Player.SkillUpgrades;
                for (int i = 0; i < _pipsVersion.Length; i++) _pipsVersion[i] = int.MinValue;
            }
            bool quiet = Quiet(out bool fresh);
            float now = Time.unscaledTime;

            for (int key = 0; key < count; key++)
            {
                UiBox home = InterfaceRules.Slot(key, count, viewWidth, v);
                int slot = Player.SkillOnKey(key);
                bool has = slot >= 0 && slot < skills.Count && skills[slot] != null && Player.IsEquipped(slot);
                var s = new KineticSlot { Home = R(home), Box = R(home), Skill = has ? slot : -1, Key = bindings != null ? Label(bindings.Skills[key]) : "", Number = "" };
                if (!has)
                {
                    s.Look = SlotLook.Empty;
                    s.Face = R(InterfaceRules.Face(home, v));
                    _cooldownTotal[key] = 0f;
                    Track(key, false, -1, quiet, fresh, now);
                    _slots[key] = s;
                    continue;
                }
                SkillDefinition skill = skills[slot];
                if (!ReferenceEquals(_nameOf[key], skill.Name))
                {
                    _nameOf[key] = skill.Name;
                    _shortOf[key] = InterfaceRules.ShortName(skill.Name);
                    _pipsVersion[key] = int.MinValue;
                }
                s.ShortName = _shortOf[key];
                s.Icon = InterfaceStyle.Icon(skill.Name);
                bool chain = RecastOnBox(slot, out float recastNow, out float cleanFrom, out float cleanTo, out bool cleanNow);
                float cooldown = Player.SkillCooldownRemaining(slot);
                int charges = skill.Charges;
                bool chargeWaiting = false;
                float wait = cooldown;
                if (charges > 1)
                {
                    s.Charges = charges;
                    s.ChargesReady = Player.SkillChargesReady(slot);
                    float next = Player.SkillNextCharge(slot);
                    chargeWaiting = s.ChargesReady > 0 && next > 0f;
                    wait = s.ChargesReady > 0 ? 0f : next;
                    cooldown = s.ChargesReady > 0 ? 0f : next;
                }
                s.Look = InterfaceRules.Look(true, slot == active || slot == recovering, chain || chargeWaiting, cooldown);
                if (wait > _cooldownTotal[key]) _cooldownTotal[key] = wait;
                if (wait <= 0f) _cooldownTotal[key] = 0f;
                if (s.Look == SlotLook.Cooling)
                {
                    s.Number = InterfaceStyle.Cooldown(wait);
                    s.Sweep = InterfaceRules.Sweep(wait, _cooldownTotal[key]);
                }
                // Work order 063 item 2: off cooldown, once, its box kicks and flashes.
                Track(key, wait > 0f, slot, quiet, fresh, now);
                float since = now - _flashAt[key];
                s.Flash = InterfaceRules.ReadyFlashAlpha(since, v);
                s.Kick = InterfaceRules.ReadyKickNow(since, v);
                UiBox box = home;
                if (chain)
                {
                    // Work order 063 item 2b: the open window on the box, lit and a little larger.
                    s.Recast = true;
                    s.RecastNow = recastNow;
                    s.RecastLeft = 1f - recastNow;
                    s.CleanFrom = cleanFrom;
                    s.CleanTo = cleanTo;
                    s.CleanShare = cleanTo - cleanFrom;
                    s.CleanLit = cleanNow && s.CleanShare > 0f;
                    box = InterfaceRules.Grown(home, v.RecastGrow);
                }
                if (s.Kick != 0f) box = Kicked(box, s.Kick, v);
                s.Box = R(box);
                UiBox face = InterfaceRules.Face(box, v);
                s.Face = R(face);
                if (chain)
                {
                    UiBox bar = InterfaceRules.RecastBar(face, v);
                    s.RecastBar = R(bar);
                    s.Band = R(InterfaceRules.RecastBand(bar, cleanFrom, cleanTo));
                }
                int version = Player.SkillUpgrades != null ? Player.SkillUpgrades.Version : -1;
                if (_pipsVersion[key] != version)
                {
                    _pipsVersion[key] = version;
                    _pips[key] = Player.UpgradesOn(slot);
                }
                s.Pips = _pips[key];
                _slots[key] = s;
            }

            // The strip's ends: the dash and its charges, focus, and the cover tag.
            _dashKey = bindings != null ? Label(new InputBinding(bindings.Dash)) : "";
            _focusKey = bindings != null ? Label(new InputBinding(bindings.Focus)) : "";
            _dashCharges = Player.DashMaxCharges;
            _dashReady = Player.DashChargesReady;
            float dash = Player.DashCooldownRemaining;
            if (dash > _dashTotal) _dashTotal = dash;
            if (dash <= 0f) _dashTotal = 0f;
            _cooling = _dashReady <= 0 && dash > 0f;
            _dashNumber = _cooling ? InterfaceStyle.Cooldown(dash) : "";
            _dashSweep = InterfaceRules.Sweep(dash, _dashTotal);
            int dashKey = SkillLoadout.KeyCount;
            Track(dashKey, _cooling, 0, quiet, fresh, now);
            DashFlash = InterfaceRules.ReadyFlashAlpha(now - _flashAt[dashKey], v);
            _dashKick = InterfaceRules.ReadyKickNow(now - _flashAt[dashKey], v);
            UiBox left = InterfaceRules.LeftEnd(count, viewWidth, v);
            _left = R(_dashKick != 0f ? Kicked(left, _dashKick, v) : left);
            _right = R(InterfaceRules.RightEnd(count, viewWidth, v));
            _cover = R(InterfaceRules.CoverTag(count, viewWidth, v));
            _plate = R(InterfaceRules.StripPlate(count, viewWidth, v));

            // At the figure.
            HealthShare = Health != null && Health.Max > 0 ? Mathf.Clamp01(Health.Current / (float)Health.Max) : 0f;
            float game = Time.unscaledTime;
            if (_lastHealth < 0f) _lastHealth = HealthShare;
            float lost = InterfaceRules.LostFrom(_lostFrom, _lastHealth, HealthShare, game - _lostAt, v);
            if (HealthShare < _lastHealth - 1e-5f) _lostAt = game;
            _lostFrom = lost;
            _lastHealth = HealthShare;
            LostFrom = _lostFrom;
            LostAlpha = _lostFrom > HealthShare ? InterfaceRules.LostAlpha(game - _lostAt, v) : 0f;
            HealthNumber = Health != null ? InterfaceStyle.Number(Mathf.Max(0, Health.Current)) : "";
            FocusShare = Mathf.Clamp01(Player.FocusBarFill);
            _focusBright = Player.IsEmpowered || Player.IsFocusFull;
            _stateWord = Player.IsPrimed ? "PRIMED" : Player.IsSteadyHandWindow ? "STEADY HAND" : Player.IsEmpowered ? "EMPOWERED" : "";
            _stateNumber = Player.IsPrimed ? InterfaceStyle.Cooldown(Player.PrimeRemaining)
                         : _stateWord.Length > 0 ? InterfaceStyle.Cooldown(Player.EmpoweredRemaining) : "";

            TickPlate(v); // work order 068: the plate at the top left (SkillBarPlate.cs)

            Camera cam = Camera.main;
            StrokesShown = false;
            if (cam != null)
            {
                Vector3 feet = cam.WorldToScreenPoint(Player.Position);
                if (feet.z > 0f)
                {
                    float fx = feet.x / scale, fy = (InterfaceStyle.ScreenHeight - feet.y) / scale;
                    // Work order 063 item 3 (and its follow-up): her point stands about a metre over her soles. The lowest
                    // her boots show is the floor under her, reached toward the camera by BootReach; the strokes' top stands
                    // the gap in metres of the camera's picture under that, made pixels this frame.
                    Vector3 toward = -cam.transform.forward;
                    toward.y = 0f;
                    toward = toward.sqrMagnitude > 1e-6f ? toward.normalized : Vector3.zero;
                    BootsPoint = Player.Position + Vector3.down * Mathf.Max(0f, v.SoleDrop) + toward * Mathf.Max(0f, v.BootReach);
                    Vector3 boots = cam.WorldToScreenPoint(BootsPoint);
                    BootsUnder = (InterfaceStyle.ScreenHeight - boots.y) / scale - fy;
                    float downY = fy;
                    if (v.UnderFeetMetres > 0f)
                    {
                        Vector3 down = cam.WorldToScreenPoint(BootsPoint - cam.transform.up * v.UnderFeetMetres);
                        downY = (InterfaceStyle.ScreenHeight - down.y) / scale;
                    }
                    float under = InterfaceRules.UnderFeetGap(fy, downY, v);
                    StrokesUnder = under;
                    HealthStroke = R(InterfaceRules.HealthStroke(fx, fy, under, v));
                    FocusStroke = R(InterfaceRules.FocusStroke(fx, fy, under, v));
                    HealthNumberBox = R(InterfaceRules.HealthNumber(fx, fy, under, v));
                    StrokesShown = true;
                }
            }
        }

        /// <summary>The frame's sizes from the values and the scales (work order 063).</summary>
        void Sizes(InterfaceValues v)
        {
            _fight = InterfaceRules.Fight(v);
            _strip = InterfaceRules.Strip(v);
            _keyFont = InterfaceRules.KeyFont(v);
            _plateH = InterfaceRules.KeyPlateHeight(v);
            _cooldownFont = InterfaceRules.CooldownFont(v);
            _shortFont = InterfaceRules.Size(v.ShortNameSize, _strip);
            _numberFont = InterfaceRules.Size(v.HealthNumberSize, _fight);
            _stateFont = InterfaceRules.Size(v.StateWordSize, _fight);
            _bigKeyFont = InterfaceRules.Size(_keyFont, Mathf.Max(1f, v.RecastKeyGrow));
            _iconSize = v.IconSize * _strip;
            _sweepH = v.SweepHeight * _strip;
            _pipSize = v.PipSize * _strip;
        }

        /// <summary>
        /// True when no ready flash may start or show now: the game paused, a choice or a screen open (work order 063). And
        /// through <paramref name="fresh"/>: a run started or ended, the floor changed, or the last frame was not worked out
        /// (the box was not drawn): what came off cooldown then is not shown.
        /// </summary>
        bool Quiet(out bool fresh)
        {
            GeneratedLevels levels = GeneratedLevels.Current;
            fresh = _trackedFrame < 0 || Time.frameCount - _trackedFrame > 1;
            _trackedFrame = Time.frameCount;
            bool quiet = Time.timeScale <= 0f || PlayerInputDriver.HeldByChoice || PlayerInputDriver.HeldByScreen;
            if (levels != null)
            {
                quiet |= levels.Paused || levels.Choice != GeneratedLevels.ChoiceKind.None;
                object run = levels.Record;
                int ending = (int)levels.Ending;
                if (!ReferenceEquals(run, _runWas) || levels.InRun != _inRunWas || levels.Floor != _floorWas || ending != _endingWas) fresh = true;
                _runWas = run;
                _inRunWas = levels.InRun;
                _floorWas = levels.Floor;
                _endingWas = ending;
            }
            return quiet;
        }

        /// <summary>
        /// One box's ready flash: started when its skill came off cooldown this frame (<see cref="InterfaceRules.CameReady"/>).
        /// A run's start, a floor's change or a frame not worked out (<paramref name="fresh"/>) only keep one from starting; a
        /// pause or a choice (<paramref name="quiet"/>) also put out one that runs.
        /// </summary>
        void Track(int key, bool waits, int skill, bool quiet, bool fresh, float now)
        {
            if (InterfaceRules.CameReady(_waited[key], waits, _waitedSkill[key] == skill, quiet || fresh) && InterfaceStyle.V.ReadyFlash)
            {
                _flashAt[key] = now;
                _flashCount[key]++;
                ReadyFlashes++;
            }
            _waited[key] = waits;
            _waitedSkill[key] = skill;
            if (quiet) _flashAt[key] = float.NegativeInfinity;
        }

        /// <summary>
        /// The recast window open on the box of <paramref name="slot"/> (work order 063 item 2b), read through this one method
        /// so it can be pointed elsewhere: whether one is open now, where it is (0 opened to 1 run out), its clean part's
        /// start and end as shares of it, and whether a press now would be clean. From the same properties and clock as the
        /// judging and the cast blade (<see cref="CastBar"/>): the chain's window on its own clock (it runs on through a hit
        /// stop, work order 038) and <see cref="PlayerCharacter.RecastPressWouldBeClean"/>.
        /// </summary>
        public bool RecastOnBox(int slot, out float now, out float cleanFrom, out float cleanTo, out bool cleanNow)
        {
            now = cleanFrom = cleanTo = 0f;
            cleanNow = false;
            PlayerCharacter p = Player;
            if (p == null || slot < 0 || p.RecastSlot != slot || p.RecastShots >= p.RecastCount) return false;
            CleanPart clean = p.RecastCleanPart;
            if (!InterfaceRules.RecastShares(p.RecastWindowSeconds, p.RecastSinceOpened, clean.From, clean.To, out now, out cleanFrom, out cleanTo)) return false;
            cleanNow = p.RecastPressWouldBeClean;
            return true;
        }

        /// <summary>A box moved <paramref name="kick"/> layout pixels along the strip's slant: up, and right by the slant.</summary>
        static UiBox Kicked(UiBox b, float kick, InterfaceValues v)
        {
            float len = Mathf.Sqrt(1f + v.Slant * v.Slant);
            return new UiBox(b.X + kick * v.Slant / len, b.Y - kick / len, b.W, b.H);
        }

        static Rect R(UiBox b) => new Rect(b.X, b.Y, b.W, b.H);

        /// <summary>A binding's label, made once for each key or button.</summary>
        static string Label(InputBinding b)
        {
            int id = ((int)b.Source << 16) | (int)b.Key;
            if (!s_Labels.TryGetValue(id, out string label))
            {
                label = b.Label;
                s_Labels[id] = label;
            }
            return label;
        }

        // ---------------- Drawing ----------------

        void DrawKinetic()
        {
            if (_tickedFrame != Time.frameCount) Tick();
            if (!Kinetic || Event.current.type != EventType.Repaint) return;
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);

            if (v.StripPlate) DrawStripPlate(v);
            for (int key = 0; key < SlotCount; key++)
                if (!_slots[key].Recast) DrawSlot(_slots[key], v);
            for (int key = 0; key < SlotCount; key++)
                if (_slots[key].Recast) DrawSlot(_slots[key], v); // a lit box over its neighbours
            DrawEnds(v);
            if (StrokesShown) DrawStrokes(v);
            if (PlateShown) DrawTopPlate(v); // work order 068: her health and focus at the top left, as in the concept

            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        /// <summary>
        /// The strip's one plate (work order 063, item 4, from both pictures): its dark plate offset behind it, the plate in
        /// the screens' darker panel ink edged in thin white, a teal stripe along its left cut edge and a red one along its
        /// right, and a teal halftone patch over its top left corner.
        /// </summary>
        void DrawStripPlate(InterfaceValues v)
        {
            Rect p = _plate;
            float pivot = p.center.y;
            InterfaceStyle.Plate(p, pivot, v.Depth * _strip, v.Outline * _strip);
            InterfaceStyle.Slanted(p, pivot, InterfaceStyle.C(v.Panel));
            if (v.StripEdge > 0f) InterfaceStyle.SlantedFrame(p, pivot, InterfaceStyle.C(v.White, 0.85f), v.StripEdge * _strip);
            if (v.Slash > 0f)
            {
                float w = Mathf.Min(v.Slash * _strip, v.StripPlatePad * _strip);
                InterfaceStyle.Slanted(new Rect(p.x, p.y, w, p.height), pivot, InterfaceStyle.C(v.Teal));
                InterfaceStyle.Slanted(new Rect(p.xMax - w, p.y, w, p.height), pivot, InterfaceStyle.C(v.Red));
            }
            if (v.Halftone > 0f)
            {
                float size = v.HalftoneSize * _strip;
                // Over the corner, half outside it: the lean puts the plate's top left corner right of its x.
                var r = new Rect(p.x + v.Slant * p.height * 0.5f - size * 0.35f, p.y - size * 0.45f, size, size);
                GUI.color = InterfaceStyle.C(v.Teal, v.Halftone);
                GUI.DrawTexture(r, InterfaceStyle.Halftone);
            }
        }

        void DrawSlot(KineticSlot s, InterfaceValues v)
        {
            Rect box = s.Box;
            float plateH = _plateH;
            Rect face = s.Face;
            var plate = new Rect(box.x, box.yMax - plateH, box.width, plateH); // under the same slant as the face: one column
            float pivot = box.center.y;
            // Work order 063, item 4: the box's own dark plate where no strip plate carries the depth, and always under a lit box.
            if (!v.StripPlate || s.Recast) InterfaceStyle.Plate(box, pivot, v.Depth * _strip, v.Outline * _strip);
            if (s.Look == SlotLook.Empty)
            {
                InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back, 0.5f));
                InterfaceStyle.Slanted(plate, pivot, InterfaceStyle.C(v.White, 0.18f));
                InterfaceStyle.Label(Shifted(plate, pivot, v), s.Key, InterfaceStyle.Text(true, _keyFont, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White, 0.55f));
                return;
            }
            bool cooling = s.Look == SlotLook.Cooling;
            Color ink = s.Look == SlotLook.Live ? InterfaceStyle.C(v.Teal) : cooling ? InterfaceStyle.C(v.Grey, v.CoolingIconAlpha) : InterfaceStyle.C(v.White);
            // Work order 068 (the concept's strip): each box in the heavy black outline, its face near black.
            bool heavy = v.StripOutline > 0f;
            if (heavy) InterfaceStyle.HeavyPlate(box, pivot, v.Slant, v.StripOutline * _fight, 0f);
            InterfaceStyle.Slanted(face, pivot, heavy ? InterfaceStyle.C(v.Black, 0.86f) : InterfaceStyle.C(v.Back));
            CellEdge(face, pivot, v);
            if (s.Look == SlotLook.Live && !s.Recast) InterfaceStyle.SlantedFrame(new Rect(face.x - 1f, face.y - 1f, face.width + 2f, face.height + 2f), pivot, InterfaceStyle.C(v.Teal, 0.9f), 1.5f * _fight);
            Rect faceShown = Shifted(face, pivot, v);
            if (s.Recast)
            {
                // Work order 063 item 2b: the key large on the face, where the old bar wrote "shoot".
                InterfaceStyle.Label(faceShown, s.Key, InterfaceStyle.Text(true, _bigKeyFont, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Teal), true);
            }
            else if (s.Icon != null)
            {
                // Work order 068: the icon fills its box, as in the concept (063's: 34 pixels at the strip's scale).
                float size = v.IconFill > 0f ? Mathf.Min(faceShown.height * v.IconFill, faceShown.width * 0.9f) : Mathf.Min(_iconSize, faceShown.height - 4f);
                GUI.color = ink;
                GUI.DrawTexture(new Rect(faceShown.center.x - size * 0.5f, faceShown.center.y - size * 0.5f, size, size), s.Icon);
            }
            else InterfaceStyle.Label(faceShown, s.ShortName, InterfaceStyle.Text(true, _shortFont, TextAnchor.MiddleCenter), ink, true);
            if (cooling && s.Sweep > 0f && v.CoolingShade.A > 0f)
                InterfaceStyle.Slanted(new Rect(face.x, face.y, face.width, face.height * s.Sweep), pivot, InterfaceStyle.C(v.CoolingShade)); // the share still to run, dark
            if (cooling && s.Number.Length > 0)
                InterfaceStyle.Label(faceShown, s.Number, InterfaceStyle.Text(true, _cooldownFont, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);
            if (s.Sweep > 0f)
                InterfaceStyle.Slanted(new Rect(face.x, face.yMax - _sweepH, face.width * s.Sweep, _sweepH), pivot, InterfaceStyle.C(v.White, 0.75f));
            InterfaceStyle.Slanted(plate, pivot, cooling ? InterfaceStyle.C(v.Grey) : s.Recast ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), s.Key, InterfaceStyle.Text(true, _keyFont, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));

            // The run's upgrades: gold pips in the top right; the charges ready as diamonds in the top left.
            for (int p = 0; p < s.Pips; p++)
                InterfaceStyle.Slanted(new Rect(face.xMax - 3f * _strip - (p + 1) * (_pipSize + 2f), face.y + 3f * _strip, _pipSize, _pipSize), pivot, InterfaceStyle.C(v.Gold));
            if (s.Charges > 1)
            {
                float pip = _pipSize + 1f, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
                var row = new Rect(faceShown.x + 4f * _strip, faceShown.y + 3f * _strip, s.Charges * step - (step - pip), pip);
                InterfaceStyle.PipRow(new Rect(row.x, row.y, s.ChargesReady * step - (s.ChargesReady > 0 ? step - pip : 0f), pip), s.ChargesReady, InterfaceStyle.C(v.Teal));
                InterfaceStyle.PipRow(new Rect(row.x + s.ChargesReady * step, row.y, (s.Charges - s.ChargesReady) * step - (step - pip), pip), s.Charges - s.ChargesReady, InterfaceStyle.C(v.White, 0.3f));
            }
            if (s.Recast) DrawRecast(s, pivot, v);
            if (s.Flash > 0f) DrawReadyFlash(face, pivot, s.Flash, v);
            if (s.Skill >= 0) PlayerSigns.DrawKeySignKinetic(face, pivot, s.Skill); // work order 037: the clean window on its key
        }

        /// <summary>
        /// The open recast window on its box (decision 0005; work order 063 item 2b): the box's hot teal edge; the window as a
        /// bar across the face, what is left of it bright from the "now" mark to its end; the clean part a clear band on it,
        /// white exactly while a press now would be clean; the "now" mark white, standing out over and under the bar.
        /// </summary>
        void DrawRecast(KineticSlot s, float pivot, InterfaceValues v)
        {
            Rect face = s.Face, bar = s.RecastBar;
            float edge = 2.5f * _fight;
            InterfaceStyle.SlantedFrame(new Rect(face.x - edge, face.y - edge, face.width + 2f * edge, s.Box.height + 2f * edge), pivot, InterfaceStyle.C(v.Teal), edge);
            InterfaceStyle.Slanted(bar, pivot, InterfaceStyle.C(v.Black, 0.9f));
            float nowX = bar.x + bar.width * Mathf.Clamp01(s.RecastNow);
            InterfaceStyle.Slanted(new Rect(nowX, bar.y + bar.height * 0.25f, bar.xMax - nowX, bar.height * 0.5f), pivot, InterfaceStyle.C(v.White, 0.55f));
            if (s.CleanShare > 0f)
            {
                Rect band = s.Band;
                InterfaceStyle.Slanted(new Rect(band.x, band.y - 1f, band.width, band.height + 2f), pivot, s.CleanLit ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal));
                if (s.CleanLit) InterfaceStyle.SlantedFrame(new Rect(band.x - 2f, band.y - 3f, band.width + 4f, band.height + 6f), pivot, InterfaceStyle.C(v.Teal), 1.5f * _fight);
            }
            float mark = Mathf.Max(2f, 1.5f * _strip);
            InterfaceStyle.Slanted(new Rect(nowX - mark * 0.5f, bar.y - 3f * _strip, mark, bar.height + 6f * _strip), pivot, InterfaceStyle.C(v.White));
        }

        /// <summary>The ready flash (work order 063 item 2): the box's edge bright teal and its face lit, fading over a quarter second.</summary>
        void DrawReadyFlash(Rect face, float pivot, float flash, InterfaceValues v)
        {
            float edge = v.ReadyEdge * _strip;
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Teal, 0.35f * flash));
            InterfaceStyle.SlantedFrame(new Rect(face.x - edge, face.y - edge, face.width + 2f * edge, face.height + 2f * edge), pivot, InterfaceStyle.C(v.Teal, flash), edge);
        }

        /// <summary>The thin white line round a box's face (work order 063, item 4), so the boxes read apart on the strip's plate.</summary>
        void CellEdge(Rect face, float pivot, InterfaceValues v)
        {
            if (v.CellEdge > 0f) InterfaceStyle.SlantedFrame(face, pivot, InterfaceStyle.C(v.White, v.CellEdge), Mathf.Max(1f, _fight));
        }

        /// <summary>Where a rectangle shows under the slant about <paramref name="pivotY"/>: moved right by its middle's lean, for what is not sheared (text, icons).</summary>
        static Rect Shifted(Rect r, float pivotY, InterfaceValues v) => new Rect(r.x + (pivotY - r.center.y) * v.Slant, r.y, r.width, r.height);

        void DrawEnds(InterfaceValues v)
        {
            float plateH = _plateH;
            // The dash, left.
            float pivot = _left.center.y;
            var face = new Rect(_left.x, _left.y, _left.width, _left.height - plateH - 2f * _strip);
            var plate = new Rect(_left.x, _left.yMax - plateH, _left.width, plateH);
            Color ink = _cooling ? InterfaceStyle.C(v.Grey, v.CoolingIconAlpha) : InterfaceStyle.C(v.White);
            if (!v.StripPlate) InterfaceStyle.Plate(_left, pivot, v.Depth * _strip, v.Outline * _strip); // work order 063, item 4
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back));
            CellEdge(face, pivot, v);
            Rect shown = Shifted(face, pivot, v);
            Texture2D dash = InterfaceStyle.Icon("Dash");
            float size = Mathf.Min(_iconSize * 0.8f, shown.height - 6f);
            if (dash != null)
            {
                GUI.color = ink;
                GUI.DrawTexture(new Rect(shown.center.x - size * 0.5f, shown.center.y - size * 0.5f, size, size), dash);
            }
            else InterfaceStyle.Label(shown, "DASH", InterfaceStyle.Text(true, _shortFont, TextAnchor.MiddleCenter), ink, true);
            if (_cooling && _dashSweep > 0f && v.CoolingShade.A > 0f)
                InterfaceStyle.Slanted(new Rect(face.x, face.y, face.width, face.height * _dashSweep), pivot, InterfaceStyle.C(v.CoolingShade));
            if (_dashNumber.Length > 0) InterfaceStyle.Label(shown, _dashNumber, InterfaceStyle.Text(true, DashNumberFont(v), TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);
            if (_dashSweep > 0f) InterfaceStyle.Slanted(new Rect(face.x, face.yMax - _sweepH, face.width * _dashSweep, _sweepH), pivot, InterfaceStyle.C(v.White, 0.75f));
            if (_dashCharges > 1)
            {
                float pip = _pipSize + 1f, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
                float x = shown.x + 4f * _strip, y = shown.y + 3f * _strip;
                InterfaceStyle.PipRow(new Rect(x, y, _dashReady * step - (_dashReady > 0 ? step - pip : 0f), pip), _dashReady, InterfaceStyle.C(v.Teal));
                InterfaceStyle.PipRow(new Rect(x + _dashReady * step, y, (_dashCharges - _dashReady) * step - (step - pip), pip), _dashCharges - _dashReady, InterfaceStyle.C(v.White, 0.3f));
            }
            InterfaceStyle.Slanted(plate, pivot, _cooling ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), _dashKey, InterfaceStyle.Text(true, Mathf.Max(1, _keyFont - 2), TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
            if (DashFlash > 0f) DrawReadyFlash(face, pivot, DashFlash, v);

            // Taking cover (2026-11-19, an experiment): a small tag beside the dash while she is in cover, teal while ducked.
            if (Player.IsInCover)
            {
                bool ducked = Player.IsDucked;
                InterfaceStyle.Slanted(_cover, ducked ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Back));
                InterfaceStyle.Label(_cover, ducked ? (Player.CoverIsTall ? "COVER" : "LOW") : "UP",
                                     InterfaceStyle.Text(true, _keyFont, TextAnchor.MiddleCenter), ducked ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White));
            }

            // Focus, right: teal and lit when it can be spent.
            pivot = _right.center.y;
            face = new Rect(_right.x, _right.y, _right.width, _right.height - plateH - 2f * _strip);
            plate = new Rect(_right.x, _right.yMax - plateH, _right.width, plateH);
            ink = _focusBright ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Grey);
            if (!v.StripPlate) InterfaceStyle.Plate(_right, pivot, v.Depth * _strip, v.Outline * _strip);
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back));
            CellEdge(face, pivot, v);
            if (_focusBright) InterfaceStyle.SlantedFrame(new Rect(face.x - 1f, face.y - 1f, face.width + 2f, face.height + 2f), pivot, InterfaceStyle.C(v.Teal, 0.9f), 1.5f * _fight);
            shown = Shifted(face, pivot, v);
            Texture2D focus = InterfaceStyle.Icon("Focus");
            if (focus != null)
            {
                GUI.color = ink;
                GUI.DrawTexture(new Rect(shown.center.x - size * 0.5f, shown.center.y - size * 0.5f, size, size), focus);
            }
            else InterfaceStyle.Label(shown, "FOCUS", InterfaceStyle.Text(true, _shortFont, TextAnchor.MiddleCenter), ink, true);
            InterfaceStyle.Slanted(plate, pivot, _focusBright ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), _focusKey, InterfaceStyle.Text(true, _keyFont, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
        }

        /// <summary>The dash's cooldown number: as the slots' (its box is narrower, so a little smaller: 0.8 of it).</summary>
        int DashNumberFont(InterfaceValues v) => Mathf.Max(1, Mathf.RoundToInt(_cooldownFont * 0.8f));

        void DrawStrokes(InterfaceValues v)
        {
            Rect h = HealthStroke, f = FocusStroke;
            // Work order 063, item 4: each on its dark plate, offset, a hard outline round it; work order 068: the concept's
            // heavier black outline, in the plate's language.
            if (v.StrokeConcept) StrokesInThePlatesLanguage(h, f, v);
            else
            {
                float depth = v.Depth * _fight, outline = Mathf.Max(v.Outline, v.HeavyOutline * 0.5f) * _fight;
                InterfaceStyle.Plate(h, h.center.y, depth, outline);
                InterfaceStyle.Plate(f, f.center.y, depth, outline);
                // Health: red on a dark back, white where it was just lost, fading.
                InterfaceStyle.SlantedBar(h, HealthShare, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Red));
                if (LostAlpha > 0f && LostFrom > HealthShare)
                    InterfaceStyle.Slanted(new Rect(h.x + h.width * HealthShare, h.y, h.width * (Mathf.Clamp01(LostFrom) - HealthShare), h.height), h.center.y, InterfaceStyle.C(v.White, LostAlpha));
                // Focus: teal; Hair Trigger's in parts; full, it glows (and the full-focus sign plays, PlayerSigns).
                if (Player.FocusMode == FocusMode.HairTrigger) DrawParts(f, v);
                else InterfaceStyle.SlantedBar(f, FocusShare, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Teal));
                if (_focusBright)
                {
                    float g = v.GlowSize * _fight;
                    InterfaceStyle.SlantedFrame(new Rect(f.x - g, f.y - g, f.width + 2f * g, f.height + 2f * g), InterfaceStyle.C(v.Teal, 0.55f), g);
                }
            }
            if (HitFlash > 0f) // work order 068: a hit she takes shows on the strokes as on the plate
            {
                if (v.StrokeConcept)
                {
                    InterfaceStyle.Leaning(h, h.center.y, v.StrokeSlant, InterfaceStyle.C(v.White, 0.7f * HitFlash));
                    InterfaceStyle.Leaning(f, f.center.y, v.StrokeSlant, InterfaceStyle.C(v.White, 0.5f * HitFlash));
                }
                else
                {
                    InterfaceStyle.Slanted(h, InterfaceStyle.C(v.White, 0.7f * HitFlash));
                    InterfaceStyle.Slanted(f, InterfaceStyle.C(v.White, 0.5f * HitFlash));
                }
            }
            // Work order 068: with the plate on, the gauge's sign, the number and the state's words are on the plate; the strokes say nothing.
            bool plate = PlateShown;
            if (!plate) PlayerSigns.DrawFocusSignKinetic(f); // work order 037: the gauge's part of a full focus, pulsing
            // The one number: her health (work order 068: off by default, it is on the plate).
            if (v.StrokeNumber || !plate)
                InterfaceStyle.Label(HealthNumberBox, HealthNumber, InterfaceStyle.Text(true, _numberFont, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            // A window or a priming running: its word and seconds, small, under focus.
            if (_stateWord.Length > 0 && !plate)
            {
                GUIStyle small = InterfaceStyle.Text(true, _stateFont, TextAnchor.UpperLeft);
                var r = new Rect(f.x, f.yMax + 2f, f.width, _stateFont * 1.3f);
                InterfaceStyle.Label(r, _stateWord, small, InterfaceStyle.C(v.Teal), true);
                float w = InterfaceStyle.Measure(_stateWord, small).x;
                InterfaceStyle.Label(new Rect(r.x + w + 4f, r.y, 40f * _fight, r.height), _stateNumber, small, InterfaceStyle.C(v.White), true);
            }
        }

        /// <summary>
        /// The strokes in the top plate's language (work order 068, the morning of 2026-11-22, <see cref="InterfaceValues.StrokeConcept"/>):
        /// each leaning hard on the heavy black outline with its dark offset copy; health red over the dark plate, white where
        /// it was just lost, a white inner edge along its top; focus teal over a dim teal slot (so an empty gauge still shows),
        /// white-hot and framed teal when full; Hair Trigger's parts as before.
        /// </summary>
        void StrokesInThePlatesLanguage(Rect h, Rect f, InterfaceValues v)
        {
            float s = v.StrokeSlant, o = Mathf.Max(0f, v.StrokeOutline) * _fight, d = v.Depth * _fight * 0.6f;
            float edge = Mathf.Max(1f, h.height * 0.22f);
            float hp = h.center.y, fp = f.center.y;
            InterfaceStyle.HeavyPlate(h, hp, s, o, d);
            InterfaceStyle.HeavyPlate(f, fp, s, o, d);
            InterfaceStyle.Leaning(h, hp, s, InterfaceStyle.C(v.Panel));
            InterfaceStyle.Leaning(new Rect(h.x, h.y, h.width * HealthShare, h.height), hp, s, InterfaceStyle.C(v.Red));
            if (LostAlpha > 0f && LostFrom > HealthShare)
                InterfaceStyle.Leaning(new Rect(h.x + h.width * HealthShare, h.y, h.width * (Mathf.Clamp01(LostFrom) - HealthShare), h.height), hp, s, InterfaceStyle.C(v.White, LostAlpha));
            InterfaceStyle.Leaning(new Rect(h.x, h.y, h.width, edge), hp, s, InterfaceStyle.C(v.White, 0.55f));
            InterfaceStyle.Leaning(f, fp, s, InterfaceStyle.C(v.Teal, 0.22f));
            if (Player.FocusMode == FocusMode.HairTrigger) DrawParts(f, v);
            else InterfaceStyle.Leaning(new Rect(f.x, f.y, f.width * FocusShare, f.height), fp, s, _focusBright ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal));
            InterfaceStyle.Leaning(new Rect(f.x, f.y, f.width, edge), fp, s, InterfaceStyle.C(v.White, 0.4f));
            if (_focusBright)
            {
                float g = Mathf.Max(1f, v.GlowSize * _fight);
                InterfaceStyle.LeaningFrame(new Rect(f.x - o - g, f.y - o - g, f.width + 2f * (o + g), f.height + 2f * (o + g)), fp, s, InterfaceStyle.C(v.Teal, 0.85f), g);
            }
        }

        /// <summary>Hair Trigger's gauge (work order 025) as slanted parts on the stroke: each filled by the focus in it, bright when full; framed while primed.</summary>
        void DrawParts(Rect f, InterfaceValues v)
        {
            int parts = Mathf.Max(1, Player.FocusParts);
            float size = FocusGauge.PartSize(Player.Identity, parts);
            float gap = v.PartGap * _fight;
            float w = (f.width - gap * (parts - 1)) / parts;
            for (int p = 0; p < parts; p++)
            {
                var part = new Rect(f.x + p * (w + gap), f.y, w, f.height);
                float fill = size > 0f ? Mathf.Clamp01((Player.Focus - p * size) / size) : 0f;
                Matrix4x4 b = GUI.matrix;
                GUI.matrix = b * InterfaceStyle.Shear(f.center.y, v.Slant);
                GUI.color = InterfaceStyle.C(v.Back);
                GUI.DrawTexture(part, Texture2D.whiteTexture);
                GUI.color = fill >= 1f ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal);
                if (fill > 0f) GUI.DrawTexture(new Rect(part.x, part.y, part.width * fill, part.height), Texture2D.whiteTexture);
                GUI.matrix = b;
            }
            if (Player.IsPrimed) InterfaceStyle.SlantedFrame(new Rect(f.x - 2f, f.y - 2f, f.width + 4f, f.height + 4f), InterfaceStyle.C(v.Teal), 1.5f * _fight);
        }
    }
}
