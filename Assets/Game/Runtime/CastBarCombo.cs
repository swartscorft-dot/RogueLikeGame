using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The combo bar (work order 068, his words of the evening of 2026-11-21: "høyreklik er litt klunky ... de første er veldig
    /// utydlig når skal skje. må tydliggjøre den combo baren altså"), as part of <see cref="CastBar"/>: the concept's wide
    /// "BLADE TIMING" bar at the bottom middle, over the strip, THE place a recast chain, a charge, a hold and a cast are read.
    /// On its left a black plate with the skill's name and a red double slash; on its track:
    /// - a recast chain (Triple Tap, Backstep): the WHOLE chain from its first press, every coming press's window and its
    ///   clean part drawn ahead of time (<see cref="ComboLine"/>), each numbered; the blade sweeps toward them; the open one
    ///   lit, its clean part white exactly while a press now would be clean, a tag over it with "PRESS" and the key; a zone
    ///   hit flashes white and stays teal, one pressed outside its clean part goes plain, one missed goes grey; after the
    ///   last press or a run-out it stays a moment, then fades;
    /// - a charge (Long Shot): its perfect band, the blade at the charge; a hold: its startup dim, then how long it holds; a
    ///   cast: from the press to its hit.
    /// The press whose window is open is the character's (its window, its clean part, whether a press now is clean); the
    /// presses still to come are work order 069's (<see cref="RecastLine.Beat"/>, as judged), placed after the press before as if
    /// it came at its aim, and move when it comes. Worked out in <see cref="Tick"/> into arrays made once; nothing allocated
    /// in a frame (the skill's name and key are made when a chain starts).
    /// </summary>
    public sealed partial class CastBar
    {
        /// <summary>The combo bar is drawn now.</summary>
        public bool ComboShown { get; private set; }
        /// <summary>What it shows: a chain's line, a charge, a hold, a cast (None while it lingers on nothing).</summary>
        public CastBarKind ComboKind { get; private set; }
        /// <summary>Its bar and the track its zones run on, this frame (layout pixels of a view 1080 high).</summary>
        public Rect ComboBarRect { get; private set; }
        public Rect ComboTrackRect { get; private set; }
        /// <summary>The blade's place on the track, 0 to 1.</summary>
        public float ComboBlade { get; private set; }
        /// <summary>The chain's presses on the line (the first press, which has no window, counted), or 0.</summary>
        public int ComboPresses { get; private set; }
        /// <summary>The press (0 = the first) whose window is open now, or -1.</summary>
        public int ComboOpen { get; private set; } = -1;
        /// <summary>A press now would be clean: the open zone's clean part is lit.</summary>
        public bool ComboLit { get; private set; }
        /// <summary>The line's length in seconds, from the chain's first press to its last window's end.</summary>
        public float ComboLength { get; private set; }

        /// <summary>Press <paramref name="press"/>'s window on the track, and its clean part, as shares 0 to 1 (press 0 has none).</summary>
        public float ComboZoneFrom(int press) => Ok(press) ? ComboLine.Share(_start[press], ComboLength) : 0f;
        public float ComboZoneTo(int press) => Ok(press) ? ComboLine.Share(ZoneEnd(press), ComboLength) : 0f;

        /// <summary>Where press <paramref name="press"/>'s window is drawn to: its end, or the next press's window's start where that comes first (the next press takes the line on).</summary>
        float ZoneEnd(int press)
        {
            float end = _start[press] + _presses[press].Window;
            return press + 1 < ComboPresses ? Mathf.Min(end, Mathf.Max(_start[press], _start[press + 1])) : end;
        }
        public float ComboCleanFrom(int press) => Ok(press) && _presses[press].Clean.Any ? ComboLine.Share(_start[press] + _presses[press].Clean.From, ComboLength) : 0f;
        public float ComboCleanTo(int press) => Ok(press) && _presses[press].Clean.Any ? ComboLine.Share(_start[press] + _presses[press].Clean.To, ComboLength) : 0f;
        /// <summary>Press <paramref name="press"/>'s window's length and clean part as the bar has them (seconds after it opens).</summary>
        public float ComboWindow(int press) => Ok(press) ? _presses[press].Window : 0f;
        public CleanPart ComboClean(int press) => Ok(press) ? _presses[press].Clean : default;
        public ComboZoneState ComboState(int press) => Ok(press) ? _states[press] : ComboZoneState.Coming;

        bool Ok(int press) => press >= 1 && press < ComboPresses;

        readonly ComboPress[] _presses = new ComboPress[ComboLine.Most];
        readonly float[] _opened = new float[ComboLine.Most];
        readonly float[] _pressed = new float[ComboLine.Most];
        readonly float[] _start = new float[ComboLine.Most];
        readonly float[] _hitAt = new float[ComboLine.Most];
        readonly ComboZoneState[] _states = new ComboZoneState[ComboLine.Most];
        int _comboSlot = -1, _comboShots;
        float _comboStart, _endedAt = float.NegativeInfinity, _comboAlpha;
        bool _chainLive;
        string _comboName = "", _comboKey = "", _comboTag = "PRESS";
        readonly string[] _pressTags = new string[ComboLine.Most];
        /// <summary>The tag over press <paramref name="press"/>'s zone while its window is open ("2  PRESS RMB").</summary>
        public string ComboTagOf(int press) => Ok(press) && _pressTags[press] != null ? _pressTags[press] : "";
        CastBarKind _kindNow;
        CastBarView _comboView;
        bool _comboViewLit;

        /// <summary>The combo bar's part of the frame's work (from <see cref="Tick"/>).</summary>
        void TickCombo(CastBarKind kind)
        {
            InterfaceValues v = InterfaceStyle.V;
            float real = Time.unscaledTime;
            bool on = v.ComboBar && InterfaceStyle.Kinetic && Values.On && _player != null && (_health == null || _health.IsAlive);
            if (!on)
            {
                ComboShown = false;
                ComboKind = CastBarKind.None;
                if (_comboSlot >= 0) EndChain(real);
                return;
            }
            PlayerCharacter p = _player;
            float viewWidth = InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);
            UiBox bar = InterfacePlate.ComboBar(SkillLoadout.KeyCount, viewWidth, v);
            ComboBarRect = new Rect(bar.X, bar.Y, bar.W, bar.H);
            UiBox track = InterfacePlate.ComboTrack(bar, v);
            ComboTrackRect = new Rect(track.X, track.Y, track.W, track.H);

            int slot = p.RecastSlot;
            bool chain = slot >= 0 && p.RecastCount > 1 && slot < p.Skills.Count && p.Skills[slot] != null;
            if (chain && (slot != _comboSlot || p.RecastShots < _comboShots || !_chainLive)) StartChain(p, slot);
            else if (!chain && _chainLive) EndChain(real);

            if (_chainLive) TickChain(p, real);
            else if (Up && kind != CastBarKind.Recast && kind != CastBarKind.None)
            {
                _kindNow = kind;
                _comboView = View;
                _comboViewLit = Lit;
                ComboKind = kind;
                ComboBlade = Mathf.Clamp01(View.Marker);
                ComboLit = Lit;
                SetName(Slot);
                _endedAt = real;
            }
            else if (Flash != CastBarFlash.None && _kindNow != CastBarKind.None) { ComboKind = _kindNow; ComboLit = false; } // after a release: the bar flashes, as the blade did
            float since = real - _endedAt;
            _comboAlpha = _chainLive || (Up && kind != CastBarKind.Recast && kind != CastBarKind.None) ? 1f
                        : since < v.ComboLinger ? 1f
                        : v.ComboFade > 0f && since < v.ComboLinger + v.ComboFade ? 1f - (since - v.ComboLinger) / v.ComboFade : 0f;
            if (_comboAlpha <= 0f)
            {
                ComboKind = CastBarKind.None;
                ComboPresses = 0;
                _kindNow = CastBarKind.None;
            }
            ComboShown = _comboAlpha > 0f && ComboKind != CastBarKind.None && !PlayerSigns.ScreenUp;
        }

        void StartChain(PlayerCharacter p, int slot)
        {
            SkillDefinition skill = p.Skills[slot];
            RecastLine line = p.RecastLine;
            int count = Mathf.Clamp(line.Any ? line.Count : p.RecastCount, 2, ComboLine.Most);
            _comboSlot = slot;
            _comboShots = p.RecastShots;
            _chainLive = true;
            _comboStart = Time.time - Mathf.Max(0f, p.RecastShots <= 1 ? p.RecastSincePress : 0f);
            ComboPresses = count;
            ComboKind = CastBarKind.Recast;
            _kindNow = CastBarKind.Recast;
            for (int i = 0; i < ComboLine.Most; i++)
            {
                // Work order 069: every press's window and clean part from the chain's own line, as judged.
                _presses[i] = i >= 1 && i < count ? ComboLine.Of(line.Beat(i + 1), ComboLine.Lead(skill, p.Kit, i + 1)) : default;
                _opened[i] = float.NaN;
                _pressed[i] = i == 0 ? 0f : float.NaN;
                _start[i] = 0f;
                _hitAt[i] = float.NegativeInfinity;
                _states[i] = ComboZoneState.Coming;
            }
            for (int i = 1; i < p.RecastShots && i < count; i++) // a chain found already under way (the bar woke late): its presses before as pressed
            {
                _pressed[i] = Time.time - _comboStart;
                _states[i] = ComboLine.Of(line.Timing(i + 1)) ?? ComboZoneState.Plain;
            }
            SetName(slot);
            int key = p.KeyOfSkill(slot);
            InputBindings bindings = p.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            _comboKey = bindings != null && key >= 0 ? bindings.SkillLabel(key) : "";
            _comboTag = _comboKey.Length > 0 ? "PRESS " + _comboKey : "PRESS"; // round 2: the concept's "PRESS ZONE", with the key; made when a chain starts
            // Round 4's picture: the open zone's tag hid its number; the tag says it ("2  PRESS RMB"). Made when a chain starts.
            for (int i = 0; i < ComboLine.Most; i++) _pressTags[i] = i >= 1 && i < count ? InterfaceStyle.Number(i + 1) + "  " + _comboTag : "";
        }

        void SetName(int slot)
        {
            if (slot == _labelSlotForName && _comboName.Length > 0) return;
            _labelSlotForName = slot;
            string name = slot >= 0 && slot < _player.Skills.Count && _player.Skills[slot] != null ? _player.Skills[slot].Name : "";
            _comboName = name.ToUpperInvariant(); // made when the skill changes, not in a frame
        }

        int _labelSlotForName = -2;

        /// <summary>
        /// The chain's frame (work order 069's <see cref="RecastLine"/>): each press's window and clean part as judged, the press
        /// that is next and the seconds since the shot before it (the blade), how each press made fell; lit exactly when the
        /// judge would call a press now clean (<see cref="PlayerCharacter.RecastPressWouldBeClean"/>, for every press).
        /// </summary>
        void TickChain(PlayerCharacter p, float real)
        {
            float now = Time.time, lineNow = now - _comboStart;
            int count = ComboPresses;
            RecastLine line = p.RecastLine;
            SkillDefinition skill = _comboSlot >= 0 && _comboSlot < p.Skills.Count ? p.Skills[_comboSlot] : null;
            if (line.Any)
                for (int i = 1; i < count; i++) // as judged now (Wide window may have come with an upgrade): the beats, the leads kept
                    _presses[i] = ComboLine.Of(line.Beat(i + 1), ComboLine.Lead(skill, p.Kit, i + 1));
            int shots = p.RecastShots;
            _comboShots = shots;
            for (int i = 1; i < shots && i < count; i++)
            {
                if (float.IsNaN(_pressed[i])) _pressed[i] = lineNow; // a press the report did not tell (it always does): pressed by now
                ComboZoneState? fell = ComboLine.Of(line.Timing(i + 1));
                if (fell.HasValue) _states[i] = fell.Value;
                else if (_states[i] == ComboZoneState.Coming || _states[i] == ComboZoneState.Open) _states[i] = ComboZoneState.Plain;
            }
            int open = line.Next - 1; // the press waited for, as an index (press 2 is index 1); -1 when none waits
            ComboOpen = -1;
            ComboLit = false;
            float blade = lineNow;
            if (open >= 1 && open < count && line.SinceShotBefore >= 0f)
            {
                float since = line.SinceShotBefore, window = _presses[open].Window;
                if (float.IsNaN(_opened[open])) _opened[open] = lineNow - since;
                blade = _opened[open] + Mathf.Min(since, window);
                if (since <= window)
                {
                    ComboOpen = open;
                    _states[open] = ComboZoneState.Open;
                    ComboLit = p.RecastPressWouldBeClean;
                }
            }
            ComboLength = Mathf.Max(0.05f, ComboLine.Lay(_presses, count, _opened, _pressed, _start));
            ComboBlade = ComboLine.Share(blade, ComboLength);
            _endedAt = real;
        }

        void EndChain(float real)
        {
            // How each press fell, kept by the character until the next chain starts (work order 069); the rest missed.
            for (int i = 1; i < ComboPresses; i++)
            {
                ComboZoneState? fell = _player != null ? ComboLine.Of(_player.RecastTimingOf(i + 1)) : null;
                if (fell.HasValue && _states[i] != ComboZoneState.Hit) _states[i] = fell.Value;
                if (_states[i] == ComboZoneState.Coming || _states[i] == ComboZoneState.Open) _states[i] = ComboZoneState.Missed;
            }
            _chainLive = false;
            _comboSlot = -1;
            ComboOpen = -1;
            ComboLit = false;
            _endedAt = real;
        }

        /// <summary>A recast press came (from the report): its zone takes how it fell, as judged (work order 069's timing).</summary>
        void ComboPressed(PlayerReport r)
        {
            if (!_chainLive || r.Slot != _comboSlot || _player == null) return;
            int i = r.Shot - 1;
            if (i < 1 || i >= ComboPresses) return;
            _pressed[i] = Time.time - _comboStart;
            _states[i] = ComboLine.Of(_player.RecastTimingOf(r.Shot)) ?? (r.Clean ? ComboZoneState.Hit : ComboZoneState.Plain);
            _hitAt[i] = Time.unscaledTime;
            _comboShots = r.Shot;
        }
        // ---------------- Drawing ----------------

        void DrawCombo()
        {
            InterfaceValues v = InterfaceStyle.V;
            Color before = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float a = Mathf.Clamp01(_comboAlpha), s = v.ComboSlant, o = v.HeavyOutline;
            Rect bar = ComboBarRect, track = ComboTrackRect;
            float pivot = bar.center.y;
            UiBox lb = InterfacePlate.ComboLabel(new UiBox(bar.x, bar.y, bar.width, bar.height), v);
            var label = new Rect(lb.X, lb.Y, lb.W, lb.H);

            // The bar: its heavy plate, a dark track, a thin white inner edge along its top.
            InterfaceStyle.HeavyPlate(bar, pivot, s, o, v.Depth);
            InterfaceStyle.Leaning(bar, pivot, s, InterfaceStyle.C(v.Panel, a));
            InterfaceStyle.Leaning(new Rect(track.x, track.y, track.width, 2f), pivot, s, InterfaceStyle.C(v.White, 0.35f * a));

            if (ComboKind == CastBarKind.Recast) DrawChainZones(track, pivot, s, a, v);
            else DrawSingle(track, pivot, s, a, v);

            // The blade: a white sliver standing over and under the track, a faint trail behind it.
            float x = track.x + track.width * ComboBlade;
            InterfaceStyle.Leaning(new Rect(Mathf.Max(track.x, x - 46f), track.y + 3f, Mathf.Min(46f, x - track.x), track.height - 6f), pivot, s, InterfaceStyle.C(v.White, 0.16f * a));
            var blade = new Rect(x - 3f, track.y - 12f, 6f, track.height + 24f);
            InterfaceStyle.Leaning(new Rect(blade.x - 2f, blade.y - 2f, blade.width + 4f, blade.height + 4f), pivot, 0.35f, InterfaceStyle.C(v.Black, a));
            InterfaceStyle.Leaning(blade, pivot, 0.35f, InterfaceStyle.C(v.White, a));

            // The label: a black plate with a teal cut, the name in the heavy italic, a red double slash after it.
            float lp = label.center.y;
            InterfaceStyle.HeavyPlate(label, lp, s, o, v.Depth);
            InterfaceStyle.Leaning(label, lp, s, InterfaceStyle.C(v.Black, a));
            InterfaceStyle.Leaning(new Rect(label.x, label.y, 6f, label.height), lp, s, InterfaceStyle.C(v.Teal, a));
            InterfaceStyle.Slashes(label.xMax - 4f, label.y + 6f, label.height - 12f, 5f, 2, InterfaceStyle.C(v.Red, a));
            InterfaceStyle.Label(new Rect(label.x + 18f, label.y, label.width - 40f, label.height), _comboName,
                                 InterfaceStyle.Text(true, v.ComboLabelSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White, a));
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        void DrawChainZones(Rect track, float pivot, float s, float a, InterfaceValues v)
        {
            float real = Time.unscaledTime;
            GUIStyle tag = InterfaceStyle.Text(true, v.ComboTagSize, TextAnchor.MiddleCenter);
            // The morning of 2026-11-22 (round 3's pictures): the press numbers large, as the concept's numerals.
            GUIStyle number = InterfaceStyle.Text(true, v.ComboNumberSize, TextAnchor.LowerCenter);
            var trackBox = new UiBox(track.x, track.y, track.width, track.height);
            // The first press, where the line starts: a teal notch and its number, so the rhythm reads 1, 2 ... 3.
            InterfaceStyle.Leaning(new Rect(track.x, track.y, 5f, track.height), pivot, s, InterfaceStyle.C(v.Teal, a));
            UiBox first = InterfacePlate.ComboNumber(trackBox, 0f, v);
            InterfaceStyle.Label(new Rect(first.X, first.Y, first.W, first.H), InterfaceStyle.Number(1), number, InterfaceStyle.C(v.Teal, a), true);
            for (int i = 1; i < ComboPresses; i++)
            {
                ComboZoneState st = _states[i];
                float wf = ComboZoneFrom(i), wt = ComboZoneTo(i);
                var win = new Rect(track.x + track.width * wf, track.y + 4f, track.width * (wt - wf), track.height - 8f);
                // Round 3's picture: the windows read faintly on the dark track; a little brighter, the open one most.
                Color winInk = st == ComboZoneState.Open ? InterfaceStyle.C(v.White, 0.32f * a)
                             : st == ComboZoneState.Missed ? InterfaceStyle.C(v.Grey, 0.16f * a) : InterfaceStyle.C(v.White, 0.17f * a);
                InterfaceStyle.Leaning(win, pivot, s, winInk);
                if (_presses[i].Clean.Any)
                {
                    float cf = ComboCleanFrom(i), ct = ComboCleanTo(i);
                    var clean = new Rect(track.x + track.width * cf, track.y, track.width * (ct - cf), track.height);
                    float flash = real - _hitAt[i] < v.ComboHitFlash ? 1f - (real - _hitAt[i]) / Mathf.Max(0.01f, v.ComboHitFlash) : 0f;
                    Color ink;
                    switch (st)
                    {
                        case ComboZoneState.Open: ink = ComboLit ? InterfaceStyle.C(v.White, a) : InterfaceStyle.C(v.Teal, a); break;
                        case ComboZoneState.Hit: ink = Color.Lerp(InterfaceStyle.C(v.Teal, a), InterfaceStyle.C(v.White, a), flash); break;
                        case ComboZoneState.Plain: ink = InterfaceStyle.C(v.Grey, 0.7f * a); break;
                        case ComboZoneState.Early: ink = InterfaceStyle.C(v.Red, 0.75f * a); break; // work order 069's timing: pressed before its clean part
                        case ComboZoneState.Late: ink = InterfaceStyle.C(v.Late, 0.6f * a); break;
                        case ComboZoneState.Missed: ink = InterfaceStyle.C(v.Grey, 0.3f * a); break;
                        default: ink = InterfaceStyle.C(v.Teal, (_presses[i].Last ? 0.5f : 0.4f) * a); break;
                    }
                    InterfaceStyle.Leaning(clean, pivot, s, ink);
                    if (st == ComboZoneState.Open && ComboLit)
                        InterfaceStyle.LeaningFrame(new Rect(clean.x - 3f, clean.y - 3f, clean.width + 6f, clean.height + 6f), pivot, s, InterfaceStyle.C(v.Teal, a), 3f);
                    if (flash > 0f)
                        InterfaceStyle.LeaningFrame(new Rect(clean.x - 4f, clean.y - 4f, clean.width + 8f, clean.height + 8f), pivot, s, InterfaceStyle.C(v.White, flash * a), 3f);
                }
                // Its number over it; the open one's tag: "PRESS" and the key, on a teal plate.
                float mid = _presses[i].Clean.Any ? (ComboCleanFrom(i) + ComboCleanTo(i)) * 0.5f : (wf + wt) * 0.5f;
                if (st == ComboZoneState.Open)
                {
                    string words = _pressTags[i] != null && _pressTags[i].Length > 0 ? _pressTags[i] : _comboTag;
                    float w = v.ComboTagSize * 0.62f * words.Length + 18f;
                    UiBox tb = InterfacePlate.ComboTag(new UiBox(track.x, track.y, track.width, track.height), mid, w, v);
                    var t = new Rect(tb.X, tb.Y, tb.W, tb.H);
                    InterfaceStyle.HeavyPlate(t, t.center.y, s, 2f, 0f);
                    InterfaceStyle.Leaning(t, t.center.y, s, ComboLit ? InterfaceStyle.C(v.White, a) : InterfaceStyle.C(v.Teal, a));
                    InterfaceStyle.Label(t, words, tag, InterfaceStyle.C(v.Black, a));
                }
                else if (st == ComboZoneState.Early || st == ComboZoneState.Late)
                {
                    // How it fell, in the type sheet's words: EARLY red, LATE the quieter white.
                    string word = st == ComboZoneState.Early ? "EARLY" : "LATE";
                    UiBox tb = InterfacePlate.ComboTag(new UiBox(track.x, track.y, track.width, track.height), mid, v.ComboTagSize * 3.6f, v);
                    InterfaceStyle.Label(new Rect(tb.X, tb.Y, tb.W, tb.H), word, tag, st == ComboZoneState.Early ? InterfaceStyle.C(v.Red, a) : InterfaceStyle.C(v.Late, a), true);
                }
                else
                {
                    UiBox tb = InterfacePlate.ComboNumber(trackBox, mid, v);
                    Color num = st == ComboZoneState.Hit ? InterfaceStyle.C(v.Teal, a) : st == ComboZoneState.Coming ? InterfaceStyle.C(v.White, a) : InterfaceStyle.C(v.Grey, 0.8f * a);
                    InterfaceStyle.Label(new Rect(tb.X, tb.Y, tb.W, tb.H), InterfaceStyle.Number(i + 1), number, num, true);
                }
            }
        }

        void DrawSingle(Rect track, float pivot, float s, float a, InterfaceValues v)
        {
            CastBarView view = _comboView;
            float dim = Mathf.Clamp01(view.DimTo), marker = Mathf.Clamp01(view.Marker);
            if (dim > 0f) InterfaceStyle.Leaning(new Rect(track.x, track.y + 4f, track.width * dim, track.height - 8f), pivot, s, InterfaceStyle.C(v.Grey, 0.25f * a));
            InterfaceStyle.Leaning(new Rect(track.x, track.y + 6f, track.width * marker, track.height - 12f), pivot, s, InterfaceStyle.C(v.White, 0.3f * a));
            if (view.HasZone)
            {
                float zf = Mathf.Clamp01(view.ZoneFrom), zt = Mathf.Clamp01(view.ZoneTo);
                var zone = new Rect(track.x + track.width * zf, track.y, track.width * (zt - zf), track.height);
                bool lit = Up ? ComboLit : _comboViewLit && Flash == CastBarFlash.Clean;
                InterfaceStyle.Leaning(zone, pivot, s, lit ? InterfaceStyle.C(v.White, a) : InterfaceStyle.C(v.Teal, a));
                if (lit) InterfaceStyle.LeaningFrame(new Rect(zone.x - 3f, zone.y - 3f, zone.width + 6f, zone.height + 6f), pivot, s, InterfaceStyle.C(v.Teal, a), 3f);
                UiBox tb = InterfacePlate.ComboTag(new UiBox(track.x, track.y, track.width, track.height), (zf + zt) * 0.5f, v.ComboTagSize * 5.2f, v);
                var t = new Rect(tb.X, tb.Y, tb.W, tb.H);
                InterfaceStyle.Leaning(t, t.center.y, s, InterfaceStyle.C(v.Teal, a));
                InterfaceStyle.Label(t, "PERFECT", InterfaceStyle.Text(true, v.ComboTagSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black, a));
            }
            if (Flash != CastBarFlash.None)
            {
                Color c = Flash == CastBarFlash.Clean ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White);
                InterfaceStyle.Leaning(track, pivot, s, new Color(c.r, c.g, c.b, 0.5f * FlashAlpha * a));
            }
        }
    }
}
