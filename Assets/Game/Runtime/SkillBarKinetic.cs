using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>One key's slot on the kinetic strip this frame (work order 045): for the drawing, and for tests.</summary>
    public struct KineticSlot
    {
        /// <summary>The slot, in layout pixels of a view 1080 high (top left 0,0).</summary>
        public Rect Box;
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
        /// <summary>A recast chain's window over this key (decision 0005): its share left, its clean part, lit while a press is clean.</summary>
        public bool Recast;
        public float RecastLeft;
        public float CleanFrom;
        public float CleanShare;
        public bool CleanLit;
    }

    /// <summary>
    /// The skill bar in the interface's kinetic skin (decision 0011; work order 045), as part of <see cref="SkillBar"/>. With
    /// <see cref="InterfaceValues.Kinetic"/> on it draws, instead of its boxes, its health and focus row and its cast row:
    ///
    /// - the skills as one small slanted strip at the bottom centre, a parallelogram per key in key order: the skill's icon
    ///   tinted white (teal while it is in use or has a recast or a charge waiting, grey on cooldown with a small italic
    ///   number and a thin sweep), its key on a small white plate under it; a skill with no icon shows a short name with its
    ///   key; the recast window as a thin strip over its slot with its clean part; the run's upgrades as gold pips; a key
    ///   with no skill faint. At the strip's ends: the dash on the left with its charges, focus on the right, and the cover
    ///   tag beside the dash while she is in cover;
    /// - at the figure, two thin slanted strokes side by side just under her feet: health in red on a dark back, white where
    ///   it was just lost and fading, and focus in teal (in parts for Hair Trigger, glowing when full), with one small
    ///   italic number for health beside them, and the window's or the priming's seconds under focus while one runs.
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

        readonly KineticSlot[] _slots = new KineticSlot[SkillLoadout.KeyCount];
        readonly float[] _cooldownTotal = new float[SkillLoadout.KeyCount];
        readonly string[] _shortOf = new string[SkillLoadout.KeyCount];
        readonly string[] _nameOf = new string[SkillLoadout.KeyCount];
        readonly int[] _pipsVersion = new int[SkillLoadout.KeyCount];
        readonly int[] _pips = new int[SkillLoadout.KeyCount];
        static readonly Dictionary<int, string> s_Labels = new Dictionary<int, string>();

        float _lastHealth = -1f, _lostFrom, _lostAt = float.NegativeInfinity;
        float _dashTotal;
        int _tickedFrame = -1;
        Rect _left, _right, _cover;
        string _dashKey = "", _focusKey = "", _dashNumber = "", _stateNumber = "", _stateWord = "";
        int _dashCharges, _dashReady;
        float _dashSweep;
        bool _focusBright, _cooling;
        RunSkillUpgrades _pipsOf;

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
                SlotCount = 0;
                return;
            }
            if (Health == null) Health = Player.GetComponent<PlayerHealth>();
            if (_driver == null) _driver = Player.GetComponent<PlayerInputDriver>();
            InterfaceValues v = InterfaceStyle.V;
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            float viewWidth = Screen.width / scale;
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

            for (int key = 0; key < count; key++)
            {
                Rect box = R(InterfaceRules.Slot(key, count, viewWidth, v));
                int slot = Player.SkillOnKey(key);
                bool has = slot >= 0 && slot < skills.Count && skills[slot] != null && Player.IsEquipped(slot);
                var s = new KineticSlot { Box = box, Skill = has ? slot : -1, Key = bindings != null ? Label(bindings.Skills[key]) : "", Number = "" };
                if (!has)
                {
                    s.Look = SlotLook.Empty;
                    _cooldownTotal[key] = 0f;
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
                bool chain = slot == Player.RecastSlot && Player.IsRecastWindowOpen;
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
                if (chain)
                {
                    s.Recast = true;
                    s.RecastLeft = Mathf.Clamp01(Player.RecastWindowLeft);
                    s.CleanShare = Player.RecastCleanShare;
                    s.CleanFrom = CleanWindowSign.KeyBandFrom(Player.RecastCleanPart, Player.RecastWindowSeconds);
                    s.CleanLit = s.CleanShare > 0f && Player.RecastPressWouldBeClean;
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
            _left = R(InterfaceRules.LeftEnd(count, viewWidth, v));
            _right = R(InterfaceRules.RightEnd(count, viewWidth, v));
            _cover = new Rect(_left.x - v.EndGap - v.EndWidth * 1.3f, _left.y + _left.height * 0.3f, v.EndWidth * 1.3f, _left.height * 0.4f);
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

            // At the figure.
            HealthShare = Health != null && Health.Max > 0 ? Mathf.Clamp01(Health.Current / (float)Health.Max) : 0f;
            float now = Time.unscaledTime;
            if (_lastHealth < 0f) _lastHealth = HealthShare;
            float lost = InterfaceRules.LostFrom(_lostFrom, _lastHealth, HealthShare, now - _lostAt, v);
            if (HealthShare < _lastHealth - 1e-5f) _lostAt = now;
            _lostFrom = lost;
            _lastHealth = HealthShare;
            LostFrom = _lostFrom;
            LostAlpha = _lostFrom > HealthShare ? InterfaceRules.LostAlpha(now - _lostAt, v) : 0f;
            HealthNumber = Health != null ? InterfaceStyle.Number(Mathf.Max(0, Health.Current)) : "";
            FocusShare = Mathf.Clamp01(Player.FocusBarFill);
            _focusBright = Player.IsEmpowered || Player.IsFocusFull;
            _stateWord = Player.IsPrimed ? "PRIMED" : Player.IsSteadyHandWindow ? "STEADY HAND" : Player.IsEmpowered ? "EMPOWERED" : "";
            _stateNumber = Player.IsPrimed ? InterfaceStyle.Cooldown(Player.PrimeRemaining)
                         : _stateWord.Length > 0 ? InterfaceStyle.Cooldown(Player.EmpoweredRemaining) : "";

            Camera cam = Camera.main;
            StrokesShown = false;
            if (cam != null)
            {
                Vector3 feet = cam.WorldToScreenPoint(Player.Position);
                if (feet.z > 0f)
                {
                    float fx = feet.x / scale, fy = (Screen.height - feet.y) / scale;
                    HealthStroke = R(InterfaceRules.HealthStroke(fx, fy, v));
                    FocusStroke = R(InterfaceRules.FocusStroke(fx, fy, v));
                    StrokesShown = true;
                }
            }
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

            for (int key = 0; key < SlotCount; key++) DrawSlot(_slots[key], v);
            DrawEnds(v);
            if (StrokesShown) DrawStrokes(v);

            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void DrawSlot(KineticSlot s, InterfaceValues v)
        {
            Rect box = s.Box;
            float plateH = v.KeySize * 1.25f;
            var face = new Rect(box.x, box.y, box.width, box.height - plateH - 2f);
            var plate = new Rect(box.x, box.yMax - plateH, box.width, plateH); // under the same slant as the face: one column
            float pivot = box.center.y;
            if (s.Look == SlotLook.Empty)
            {
                InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back, 0.5f));
                InterfaceStyle.Slanted(plate, pivot, InterfaceStyle.C(v.White, 0.18f));
                InterfaceStyle.Label(Shifted(plate, pivot, v), s.Key, InterfaceStyle.Text(true, v.KeySize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White, 0.55f));
                return;
            }
            Color ink = s.Look == SlotLook.Live ? InterfaceStyle.C(v.Teal) : s.Look == SlotLook.Cooling ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White);
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back));
            if (s.Look == SlotLook.Live) InterfaceStyle.SlantedFrame(new Rect(face.x - 1f, face.y - 1f, face.width + 2f, face.height + 2f), pivot, InterfaceStyle.C(v.Teal, 0.9f), 1.5f);
            Rect faceShown = Shifted(face, pivot, v);
            if (s.Icon != null)
            {
                float size = Mathf.Min(v.IconSize, faceShown.height - 4f);
                GUI.color = ink;
                GUI.DrawTexture(new Rect(faceShown.center.x - size * 0.5f, faceShown.center.y - size * 0.5f, size, size), s.Icon);
            }
            else InterfaceStyle.Label(faceShown, s.ShortName, InterfaceStyle.Text(true, v.ShortNameSize, TextAnchor.MiddleCenter), ink, true);
            if (s.Look == SlotLook.Cooling && s.Number.Length > 0)
                InterfaceStyle.Label(faceShown, s.Number, InterfaceStyle.Text(true, v.CooldownSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);
            if (s.Sweep > 0f)
                InterfaceStyle.Slanted(new Rect(face.x, face.yMax - v.SweepHeight, face.width * s.Sweep, v.SweepHeight), pivot, InterfaceStyle.C(v.White, 0.75f));
            InterfaceStyle.Slanted(plate, pivot, s.Look == SlotLook.Cooling ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), s.Key, InterfaceStyle.Text(true, v.KeySize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));

            // The run's upgrades: gold pips in the top right; the charges ready as diamonds in the top left.
            for (int p = 0; p < s.Pips; p++)
                InterfaceStyle.Slanted(new Rect(face.xMax - 3f - (p + 1) * (v.PipSize + 2f), face.y + 3f, v.PipSize, v.PipSize), pivot, InterfaceStyle.C(v.Gold));
            if (s.Charges > 1)
            {
                float pip = v.PipSize + 1f, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
                var row = new Rect(faceShown.x + 4f, faceShown.y + 3f, s.Charges * step - (step - pip), pip);
                InterfaceStyle.PipRow(new Rect(row.x, row.y, s.ChargesReady * step - (s.ChargesReady > 0 ? step - pip : 0f), pip), s.ChargesReady, InterfaceStyle.C(v.Teal));
                InterfaceStyle.PipRow(new Rect(row.x + s.ChargesReady * step, row.y, (s.Charges - s.ChargesReady) * step - (step - pip), pip), s.Charges - s.ChargesReady, InterfaceStyle.C(v.White, 0.3f));
            }
            // The recast window over the slot (decision 0005): its clean part, and what is left of it.
            if (s.Recast)
            {
                var strip = new Rect(face.x, face.y - v.RecastHeight - 3f, face.width, v.RecastHeight);
                InterfaceStyle.Slanted(strip, pivot, InterfaceStyle.C(v.Back));
                if (s.CleanShare > 0f)
                    InterfaceStyle.Slanted(new Rect(strip.x + strip.width * Mathf.Clamp01(s.CleanFrom), strip.y, strip.width * s.CleanShare, strip.height), pivot, InterfaceStyle.C(v.Teal, 0.55f));
                InterfaceStyle.Slanted(new Rect(strip.x, strip.y + 1f, strip.width * s.RecastLeft, strip.height - 2f), pivot, s.CleanLit ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
            }
            if (s.Skill >= 0) PlayerSigns.DrawKeySignKinetic(face, pivot, s.Skill); // work order 037: the clean window on its key
        }

        /// <summary>Where a rectangle shows under the slant about <paramref name="pivotY"/>: moved right by its middle's lean, for what is not sheared (text, icons).</summary>
        static Rect Shifted(Rect r, float pivotY, InterfaceValues v) => new Rect(r.x + (pivotY - r.center.y) * v.Slant, r.y, r.width, r.height);

        void DrawEnds(InterfaceValues v)
        {
            float plateH = v.KeySize * 1.25f;
            // The dash, left.
            float pivot = _left.center.y;
            var face = new Rect(_left.x, _left.y, _left.width, _left.height - plateH - 2f);
            var plate = new Rect(_left.x, _left.yMax - plateH, _left.width, plateH);
            Color ink = _cooling ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White);
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back));
            Rect shown = Shifted(face, pivot, v);
            Texture2D dash = InterfaceStyle.Icon("Dash");
            float size = Mathf.Min(v.IconSize * 0.8f, shown.height - 6f);
            if (dash != null)
            {
                GUI.color = ink;
                GUI.DrawTexture(new Rect(shown.center.x - size * 0.5f, shown.center.y - size * 0.5f, size, size), dash);
            }
            else InterfaceStyle.Label(shown, "DASH", InterfaceStyle.Text(true, v.ShortNameSize, TextAnchor.MiddleCenter), ink, true);
            if (_dashNumber.Length > 0) InterfaceStyle.Label(shown, _dashNumber, InterfaceStyle.Text(true, v.CooldownSize - 3, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);
            if (_dashSweep > 0f) InterfaceStyle.Slanted(new Rect(face.x, face.yMax - v.SweepHeight, face.width * _dashSweep, v.SweepHeight), pivot, InterfaceStyle.C(v.White, 0.75f));
            if (_dashCharges > 1)
            {
                float pip = v.PipSize + 1f, step = pip * InterfaceStyle.PipCell / InterfaceStyle.PipLit;
                float x = shown.x + 4f, y = shown.y + 3f;
                InterfaceStyle.PipRow(new Rect(x, y, _dashReady * step - (_dashReady > 0 ? step - pip : 0f), pip), _dashReady, InterfaceStyle.C(v.Teal));
                InterfaceStyle.PipRow(new Rect(x + _dashReady * step, y, (_dashCharges - _dashReady) * step - (step - pip), pip), _dashCharges - _dashReady, InterfaceStyle.C(v.White, 0.3f));
            }
            InterfaceStyle.Slanted(plate, pivot, _cooling ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), _dashKey, InterfaceStyle.Text(true, v.KeySize - 2, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));

            // Taking cover (2026-11-19, an experiment): a small tag beside the dash while she is in cover, teal while ducked.
            if (Player.IsInCover)
            {
                bool ducked = Player.IsDucked;
                InterfaceStyle.Slanted(_cover, ducked ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Back));
                InterfaceStyle.Label(_cover, ducked ? (Player.CoverIsTall ? "COVER" : "LOW") : "UP",
                                     InterfaceStyle.Text(true, v.KeySize, TextAnchor.MiddleCenter), ducked ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White));
            }

            // Focus, right: teal and lit when it can be spent.
            pivot = _right.center.y;
            face = new Rect(_right.x, _right.y, _right.width, _right.height - plateH - 2f);
            plate = new Rect(_right.x, _right.yMax - plateH, _right.width, plateH);
            ink = _focusBright ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Grey);
            InterfaceStyle.Slanted(face, pivot, InterfaceStyle.C(v.Back));
            if (_focusBright) InterfaceStyle.SlantedFrame(new Rect(face.x - 1f, face.y - 1f, face.width + 2f, face.height + 2f), pivot, InterfaceStyle.C(v.Teal, 0.9f), 1.5f);
            shown = Shifted(face, pivot, v);
            Texture2D focus = InterfaceStyle.Icon("Focus");
            if (focus != null)
            {
                GUI.color = ink;
                GUI.DrawTexture(new Rect(shown.center.x - size * 0.5f, shown.center.y - size * 0.5f, size, size), focus);
            }
            else InterfaceStyle.Label(shown, "FOCUS", InterfaceStyle.Text(true, v.ShortNameSize, TextAnchor.MiddleCenter), ink, true);
            InterfaceStyle.Slanted(plate, pivot, _focusBright ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
            InterfaceStyle.Label(Shifted(plate, pivot, v), _focusKey, InterfaceStyle.Text(true, v.KeySize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
        }

        void DrawStrokes(InterfaceValues v)
        {
            Rect h = HealthStroke, f = FocusStroke;
            // Health: red on a dark back, white where it was just lost, fading.
            InterfaceStyle.SlantedBar(h, HealthShare, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Red));
            if (LostAlpha > 0f && LostFrom > HealthShare)
                InterfaceStyle.Slanted(new Rect(h.x + h.width * HealthShare, h.y, h.width * (Mathf.Clamp01(LostFrom) - HealthShare), h.height), h.center.y, InterfaceStyle.C(v.White, LostAlpha));
            // Focus: teal; Hair Trigger's in parts; full, it glows (and the full-focus sign plays, PlayerSigns).
            if (Player.FocusMode == FocusMode.HairTrigger) DrawParts(f, v);
            else InterfaceStyle.SlantedBar(f, FocusShare, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Teal));
            if (_focusBright)
            {
                float g = v.GlowSize;
                InterfaceStyle.SlantedFrame(new Rect(f.x - g, f.y - g, f.width + 2f * g, f.height + 2f * g), InterfaceStyle.C(v.Teal, 0.55f), g);
            }
            PlayerSigns.DrawFocusSignKinetic(f); // work order 037: the gauge's part of a full focus, pulsing
            // The one number: her health.
            UiBox n = InterfaceRules.HealthNumber(h.x + (2f * v.StrokeWidth + v.StrokeGap) * 0.5f, h.y - v.UnderFeet, v);
            InterfaceStyle.Label(R(n), HealthNumber, InterfaceStyle.Text(true, v.HealthNumberSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            // A window or a priming running: its word and seconds, small, under focus.
            if (_stateWord.Length > 0)
            {
                GUIStyle small = InterfaceStyle.Text(true, v.StateWordSize, TextAnchor.UpperLeft);
                var r = new Rect(f.x, f.yMax + 2f, f.width, v.StateWordSize * 1.3f);
                InterfaceStyle.Label(r, _stateWord, small, InterfaceStyle.C(v.Teal), true);
                float w = InterfaceStyle.Measure(_stateWord, small).x;
                InterfaceStyle.Label(new Rect(r.x + w + 4f, r.y, 40f, r.height), _stateNumber, small, InterfaceStyle.C(v.White), true);
            }
        }

        /// <summary>Hair Trigger's gauge (work order 025) as slanted parts on the stroke: each filled by the focus in it, bright when full; framed while primed.</summary>
        void DrawParts(Rect f, InterfaceValues v)
        {
            int parts = Mathf.Max(1, Player.FocusParts);
            float size = FocusGauge.PartSize(Player.Identity, parts);
            float w = (f.width - v.PartGap * (parts - 1)) / parts;
            for (int p = 0; p < parts; p++)
            {
                var part = new Rect(f.x + p * (w + v.PartGap), f.y, w, f.height);
                float fill = size > 0f ? Mathf.Clamp01((Player.Focus - p * size) / size) : 0f;
                Matrix4x4 b = GUI.matrix;
                GUI.matrix = b * InterfaceStyle.Shear(f.center.y, v.Slant);
                GUI.color = InterfaceStyle.C(v.Back);
                GUI.DrawTexture(part, Texture2D.whiteTexture);
                GUI.color = fill >= 1f ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal);
                if (fill > 0f) GUI.DrawTexture(new Rect(part.x, part.y, part.width * fill, part.height), Texture2D.whiteTexture);
                GUI.matrix = b;
            }
            if (Player.IsPrimed) InterfaceStyle.SlantedFrame(new Rect(f.x - 2f, f.y - 2f, f.width + 4f, f.height + 4f), InterfaceStyle.C(v.Teal), 1.5f);
        }
    }
}
