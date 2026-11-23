using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The menus in the concept's style (work order 078, 2026-11-22: Kristian ticked "Menyene i konseptstilen"), as part of
    /// <see cref="RunHud"/>: the pause menu, the run's end and the screen between runs drawn in the language of work order
    /// 068's fight interface (<see cref="InterfaceStyle"/>'s plates, outlines, dots, slashes and type; no second style). What
    /// they say, and what their keys and clicks do, are the HUD's as before; only the drawing is new. With the skin on and
    /// <see cref="MenuValues.Concept"/> on; off, work order 068's panels as this morning.
    ///
    /// The pause menu: a veil over the game (it shows through), "PAUSED" in the heavy white italic on a red plate leaning as
    /// her health's does, the lines as slanted rows (the actions with their keys on white key caps, the settings after a gap
    /// with their values in the heavy italic, teal for on and grey for off, between small arrows), the selected row white
    /// with black words, a teal stroke and the red double slash; beside them her stats as a compact two-column list on a dark
    /// panel in the heavy outline (a value changed this run teal, with its change), and the keys as white key caps.
    ///
    /// Everything it draws is made when the menu opens or what it says changes (the lists, the rows' words, their measured
    /// widths: <see cref="PauseListsMade"/>), never in a frame that changes nothing.
    /// </summary>
    public sealed partial class RunHud
    {
        static readonly CultureInfo s_Inv078 = CultureInfo.InvariantCulture;

        /// <summary>The menus are drawn in the concept's style now (the skin's switch is the caller's).</summary>
        bool MenuConcept => InterfaceStyle.V.Menus != null && InterfaceStyle.V.Menus.Concept;

        /// <summary>The menus are drawn in the concept's style (the skin on and <see cref="MenuValues.Concept"/> on); false: this morning's. For tests.</summary>
        public bool MenusInConcept => InterfaceStyle.Kinetic && MenuConcept;

        // ---------------- Words drawn with a content each (work order 090) ----------------

        /// <summary>
        /// A content for each of the words the HUD and its menus draw, made once for them (work order 090: setting a content's
        /// words to other words makes 44 bytes of garbage in the engine, and the shared content of <see cref="InterfaceStyle.Label(Rect, string, GUIStyle, Color, bool)"/>
        /// was set anew for every label of every event: 131 times a layout and again a repaint of the pause menu). By the words'
        /// value; emptied when it holds <see cref="WordsMost"/> (the run box's clock makes one a second).
        /// </summary>
        static readonly Dictionary<string, GUIContent> s_Words = new Dictionary<string, GUIContent>(System.StringComparer.Ordinal);
        const int WordsMost = 512;

        /// <summary>The content of these words, made the first time they are drawn; <see cref="GUIContent.none"/> for none.</summary>
        static GUIContent WordsOf(string text)
        {
            if (string.IsNullOrEmpty(text)) return GUIContent.none;
            if (s_Words.TryGetValue(text, out GUIContent content)) return content;
            if (s_Words.Count >= WordsMost) s_Words.Clear();
            content = new GUIContent(text);
            s_Words[text] = content;
            return content;
        }

        /// <summary><see cref="InterfaceStyle.Label(Rect, string, GUIStyle, Color, bool)"/> with the words' own content: the same drawing, no garbage for the words.</summary>
        static void TextLabel(Rect r, string text, GUIStyle style, Color c, bool shadow = false)
        {
            if (string.IsNullOrEmpty(text)) return;
            InterfaceStyle.Label(r, WordsOf(text), style, c, shadow);
        }

        // ---------------- The pause menu's words, made with its lists ----------------

        readonly List<string> _rowWords = new List<string>();
        readonly List<string> _rowValues = new List<string>();
        readonly List<string> _rowKeys = new List<string>();
        readonly List<bool> _rowSetting = new List<bool>();
        readonly List<MenuKey> _keyCaps = new List<MenuKey>();
        readonly List<MenuStat> _stats = new List<MenuStat>();
        int _firstSetting = -1;
        bool _runBoxUnderScreen;

        /// <summary>The pause menu's rows: each one's words, its setting's value ("on", "100 %") or its key ("Esc"), as drawn (work order 078). For tests.</summary>
        public IReadOnlyList<string> PauseRowWords => _rowWords;
        public IReadOnlyList<string> PauseRowValues => _rowValues;
        public IReadOnlyList<string> PauseRowKeys => _rowKeys;
        /// <summary>Her stats read apart, as the panel draws them. For tests.</summary>
        public IReadOnlyList<MenuStat> PauseStats => _stats;
        /// <summary>The keys as key caps, as the panel draws them. For tests.</summary>
        public IReadOnlyList<MenuKey> PauseKeyCaps => _keyCaps;

        /// <summary>Puts the arrow keys' place on a line of the pause menu, as the up and down arrows move it (work order 078: for tests). False when not paused or not shown.</summary>
        public bool SelectMenuLine(MenuLine line)
        {
            Find();
            if (_levels == null || !_levels.Paused) return false;
            BuildMenu();
            int at = _menu.IndexOf(line);
            if (at < 0) return false;
            Selected = at;
            return true;
        }

        static bool IsSetting(MenuLine line) =>
            line == MenuLine.DashDirection || line == MenuLine.Volume || line == MenuLine.DamageNumbers || line == MenuLine.BetweenRuns
            || line == MenuLine.CameraKick || line == MenuLine.Attachments
            || line == MenuLine.CameraDistance   // work order 083, joined with the menus at the merge of 2026-11-22
            || line == MenuLine.Greybox          // work order 084, the same
            || line == MenuLine.Light;           // work order 085

        /// <summary>The rows' words and her stats read apart (work order 078): made with the pause screen's lists, when it opens and when a setting changes.</summary>
        void MakeMenuRows()
        {
            if (_levels != null && _levels.Paused) BuildMenu();
            _rowWords.Clear();
            _rowValues.Clear();
            _rowKeys.Clear();
            _rowSetting.Clear();
            _firstSetting = -1;
            for (int i = 0; i < _menu.Count; i++)
            {
                MenuLine line = _menu[i];
                bool setting = IsSetting(line);
                string text = MenuText(line);
                if (setting)
                {
                    MenuWords.Setting(text, out string name, out string value);
                    _rowWords.Add(name);
                    _rowValues.Add(value);
                    _rowKeys.Add("");
                    if (_firstSetting < 0) _firstSetting = i;
                }
                else
                {
                    MenuWords.Action(text, out string words, out string key);
                    _rowWords.Add(words);
                    _rowValues.Add("");
                    _rowKeys.Add(key);
                }
                _rowSetting.Add(setting);
            }
            _stats.Clear();
            if (_pauseStats != null)
                foreach (string line in _pauseStats) _stats.Add(MenuWords.Stat(line));
            _pauseLaidOutFor = -1;
        }

        // ---------------- The pause menu's layout, worked out once for its words and the screen ----------------

        PauseLayout _pl;
        float _pFit = 1f;
        int _pauseLaidOutFor = -1;
        Vector2Int _pauseLaidOutAt;
        float _statsH, _keysH, _colW;
        int _statLines, _keyLines;
        readonly List<float> _widths = new List<float>();
        readonly List<int> _statLine = new List<int>(), _statCol = new List<int>(), _keyLine = new List<int>(), _keyCol = new List<int>();
        readonly List<float> _statValueW = new List<float>(), _capW = new List<float>(), _rowValueW = new List<float>(), _rowKeyW = new List<float>();
        readonly float[] _hintW = new float[s_Hint.Length];
        static readonly string[] s_Hint = { "Up", "Down", "pick", "Enter", "use", "<", ">", "change", "Esc", "resume" };
        static readonly bool[] s_HintCap = { true, true, false, true, false, true, true, false, true, false };
        const string StatsHead = "HER STATS", StatsSub = "this run   ·   teal: changed since its start";
        const string KeysHead = "KEYS", KeysSub = "as they are bound now", PausedWord = "PAUSED";

        GUIStyle RowWordStyle(MenuValues m) => InterfaceStyle.Text(false, m.RowTextSize, TextAnchor.MiddleLeft);
        GUIStyle RowValueStyle(MenuValues m) => InterfaceStyle.Text(true, m.RowValueSize, TextAnchor.MiddleRight);
        GUIStyle CapStyle(MenuValues m) => InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter);

        /// <summary>The pause menu's place, its rows and panels, for the screen now (work order 078), in the 1080 layout before <see cref="PauseFit"/>.</summary>
        public PauseLayout PauseLayoutNow
        {
            get
            {
                LayOutPause();
                return _pl;
            }
        }

        /// <summary>The factor the pause menu is drawn at to fit the view (1 when it fits).</summary>
        public float PauseFit
        {
            get
            {
                LayOutPause();
                return _pFit;
            }
        }

        /// <summary>The view's width in the 1080 layout, the menu's own units (before the fit).</summary>
        static float LayoutViewWidth => InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);

        void LayOutPause()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_pauseLaidOutFor == PauseListsMade && _pauseLaidOutAt == at && _rowWords.Count == _menu.Count) return;
            if (_rowWords.Count != _menu.Count) MakeMenuRows();
            _pauseLaidOutFor = PauseListsMade;
            _pauseLaidOutAt = at;
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            float inner = m.SideWidth - 2f * m.PanelPad - 12f;
            _colW = (inner - m.ColumnInnerGap) * 0.5f;

            // Her stats: each line's width (name, change, value), two columns where they fit, else a whole line.
            GUIStyle name = InterfaceStyle.Text(false, m.StatNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, m.StatValueSize, TextAnchor.MiddleRight),
                     change = InterfaceStyle.Text(true, m.StatChangeSize, TextAnchor.MiddleRight);
            _widths.Clear();
            _statValueW.Clear();
            foreach (MenuStat s in _stats)
            {
                float vw = InterfaceStyle.Measure(s.Value, value).x;
                _statValueW.Add(vw);
                float cw = s.Changed && s.Change.Length > 0 ? InterfaceStyle.Measure(s.Change, change).x + 10f : 0f;
                _widths.Add(InterfaceStyle.Measure(s.Name, name).x + 16f + cw + vw);
            }
            _statLines = MenuLayout.TwoColumns(_widths, _colW, _statLine, _statCol);
            _statsH = _stats.Count == 0 ? 0f : 2f * m.PanelPad + m.PanelHeadHeight + _statLines * m.StatLine;

            // The keys: a cap as wide as its key, the words after it.
            GUIStyle cap = CapStyle(m), words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            _widths.Clear();
            _capW.Clear();
            foreach (MenuKey k in _keyCaps)
            {
                float w = Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(k.Key, cap).x + 18f);
                _capW.Add(w);
                _widths.Add(w + 12f + InterfaceStyle.Measure(k.Words, words).x);
            }
            _keyLines = MenuLayout.TwoColumns(_widths, _colW, _keyLine, _keyCol);
            _keysH = _keyCaps.Count == 0 ? 0f : 2f * m.PanelPad + m.PanelHeadHeight + _keyLines * m.KeyLine;

            // The rows: a setting's value, an action's key cap.
            _rowValueW.Clear();
            _rowKeyW.Clear();
            for (int i = 0; i < _rowWords.Count; i++)
            {
                _rowValueW.Add(_rowValues[i].Length > 0 ? InterfaceStyle.Measure(_rowValues[i], RowValueStyle(m)).x : 0f);
                _rowKeyW.Add(_rowKeys[i].Length > 0 ? Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(_rowKeys[i], cap).x + 18f) : 0f);
            }
            for (int i = 0; i < s_Hint.Length; i++)
                _hintW[i] = s_HintCap[i] ? Mathf.Max(m.KeyCapHeight * 1.2f, InterfaceStyle.Measure(s_Hint[i], cap).x + 16f)
                                         : InterfaceStyle.Measure(s_Hint[i], words).x;

            float side = _statsH + (_statsH > 0f && _keysH > 0f ? m.SideGap : 0f) + _keysH;
            float viewWidth = LayoutViewWidth;
            _pl = MenuLayout.Pause(viewWidth, _rowWords.Count, _firstSetting, side, m);
            _pFit = MenuLayout.Fit(_pl.Width, _pl.Height, viewWidth, v.LayoutHeight, m);
            if (_pFit < 1f) _pl = MenuLayout.Pause(viewWidth / _pFit, _rowWords.Count, _firstSetting, side, m);
        }

        /// <summary>Row <paramref name="index"/> of the pause menu as drawn, in the 1080 layout after the fit (the selected one moved out). For tests.</summary>
        public Rect PauseRowRect(int index)
        {
            LayOutPause();
            Rect r = RowRect(index);
            return new Rect(r.x * _pFit, r.y * _pFit, r.width * _pFit, r.height * _pFit);
        }

        /// <summary>The whole pause menu in the 1080 layout after the fit (the heading, the rows, the keys' line and the panels). For tests.</summary>
        public Rect PauseWhole
        {
            get
            {
                LayOutPause();
                float x = _pl.Rows.X, y = _pl.Heading.Y, r = Mathf.Max(_pl.Rows.Right + InterfaceStyle.V.Menus.SelectedShift + 32f, _pl.Side.Right),
                      b = Mathf.Max(_pl.Hint.Bottom, _pl.Side.Bottom);
                return new Rect(x * _pFit, y * _pFit, (r - x) * _pFit, (b - y) * _pFit);
            }
        }

        Rect RowRect(int i)
        {
            UiBox b = MenuLayout.Row(_pl.Rows, i, _firstSetting, InterfaceStyle.V.Menus);
            return new Rect(b.X + (i == Selected ? InterfaceStyle.V.Menus.SelectedShift : 0f), b.Y, b.W, b.H);
        }

        /// <summary>
        /// What of the pause menu's words does not fit inside its shape at the screen now, as words; empty when everything fits
        /// (work order 078: "text measures inside its shape"). For tests.
        /// </summary>
        public string PauseTextOutside()
        {
            LayOutPause();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            var problems = new List<string>();
            GUIStyle heading = InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft);
            Vector2 hs = InterfaceStyle.Measure(PausedWord, heading);
            if (HeadingPad(m) + hs.x + 16f > _pl.Heading.W) problems.Add("the heading is wider than its plate");
            if (hs.y > _pl.Heading.H * 1.2f) problems.Add("the heading is taller than its plate");
            float pad = RowPad(v, m);
            for (int i = 0; i < _rowWords.Count; i++)
            {
                Vector2 ws = InterfaceStyle.Measure(_rowWords[i], RowWordStyle(m));
                float right = _rowSetting[i] ? _rowValueW[i] + 2f * ChevronW + 22f : _rowKeyW[i] + 16f;
                if (pad + ws.x + 14f + right > m.RowsWidth - pad * 0.5f) problems.Add("row " + i + " (" + _rowWords[i] + ") is wider than its row");
                if (ws.y > m.RowHeight * 1.05f) problems.Add("row " + i + " is taller than its row");
            }
            for (int i = 0; i < _stats.Count; i++)
                if (_widths.Count > 0 && _statCol[i] == -1 && StatWidth(i, m) > _colW * 2f + m.ColumnInnerGap) problems.Add("stat " + _stats[i].Name + " is wider than its panel");
            for (int i = 0; i < _keyCaps.Count; i++)
            {
                float w = _capW[i] + 12f + InterfaceStyle.Measure(_keyCaps[i].Words, InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft)).x;
                if (w > (_keyCol[i] == -1 ? _colW * 2f + m.ColumnInnerGap : _colW)) problems.Add("key " + _keyCaps[i].Key + " is wider than its place");
            }
            float hint = 0f;
            for (int i = 0; i < s_Hint.Length; i++) hint += _hintW[i] + (s_HintCap[i] ? 6f : 18f);
            if (hint > _pl.Hint.W) problems.Add("the line of keys is wider than the rows");
            GUIStyle head = InterfaceStyle.Text(true, m.PanelHeadSize, TextAnchor.MiddleLeft), sub = InterfaceStyle.Text(false, m.PanelSubSize, TextAnchor.MiddleRight);
            if (InterfaceStyle.Measure(StatsHead, head).x + 24f + InterfaceStyle.Measure(StatsSub, sub).x + 50f > m.SideWidth - 2f * m.PanelPad) problems.Add("the stats' heading is wider than its panel");
            return string.Join("; ", problems);
        }

        float StatWidth(int i, MenuValues m)
        {
            MenuStat s = _stats[i];
            float cw = s.Changed && s.Change.Length > 0 ? InterfaceStyle.Measure(s.Change, InterfaceStyle.Text(true, m.StatChangeSize, TextAnchor.MiddleRight)).x + 10f : 0f;
            return InterfaceStyle.Measure(s.Name, InterfaceStyle.Text(false, m.StatNameSize, TextAnchor.MiddleLeft)).x + 16f + cw + _statValueW[i];
        }

        const float ChevronW = 22f;
        static float RowPad(InterfaceValues v, MenuValues m) => v.Slant * m.RowHeight * 0.5f + 18f;
        static float HeadingPad(MenuValues m) => m.HeadingSlant * m.HeadingHeight * 0.5f + 18f;
        static Rect R(UiBox b) => new Rect(b.X, b.Y, b.W, b.H);

        // ---------------- The pause menu, drawn ----------------

        void DrawPauseConcept()
        {
            PauseLines(); // made when it opens and when a setting changes (work order 055)
            LayOutPause();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            InterfaceStyle.Fill(new Rect(0f, 0f, viewWidth, v.LayoutHeight), new Color(0f, 0f, 0f, Mathf.Clamp01(m.Veil)));
            Matrix4x4 layout = GUI.matrix;
            if (_pFit < 1f) GUI.matrix = layout * Matrix4x4.Scale(new Vector3(_pFit, _pFit, 1f));

            DrawHeadingPlate(R(_pl.Heading), PausedWord, v, m);
            Event e = Event.current;
            for (int i = 0; i < _rowWords.Count && i < _menu.Count; i++)
            {
                Rect r = RowRect(i);
                bool hover = e != null && r.Contains(e.mousePosition);
                DrawMenuRow(i, r, i == Selected, hover, v, m);
                MenuLine line = _menu[i];
                if (_rowSetting[i])
                {
                    var less = new Rect(r.xMax - 2f * ChevronW - 30f - _rowValueW[i], r.y, ChevronW + 8f, r.height);
                    var more = new Rect(r.xMax - ChevronW - 14f, r.y, ChevronW + 14f, r.height);
                    if (GUI.Button(less, GUIContent.none, GUIStyle.none)) { Selected = i; ChangeSetting(line, -1); }
                    else if (GUI.Button(more, GUIContent.none, GUIStyle.none)) { Selected = i; ChangeSetting(line, 1); }
                    else if (GUI.Button(new Rect(r.x, r.y, less.x - r.x, r.height), GUIContent.none, GUIStyle.none)) { Selected = i; PickMenuLine(line); }
                }
                else if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    Selected = i;
                    PickMenuLine(line);
                    GUI.matrix = layout;
                    return; // the menu may be gone
                }
            }
            DrawHint(R(_pl.Hint), v, m);
            float top = _pl.Side.Y;
            if (_statsH > 0f)
            {
                DrawStats(new Rect(_pl.Side.X, top, _pl.Side.W, _statsH), v, m);
                top += _statsH + m.SideGap;
            }
            if (_keysH > 0f) DrawKeys(new Rect(_pl.Side.X, top, _pl.Side.W, _keysH), v, m);
            GUI.matrix = layout;
        }

        /// <summary>A heading on a red plate leaning as her health's does: the heavy outline with a white stroke outside, dark dots toward its right end, the words in the heavy white italic.</summary>
        static void DrawHeadingPlate(Rect h, string words, InterfaceValues v, MenuValues m)
        {
            float pivot = h.center.y;
            InterfaceStyle.InkedPlate(h, pivot, m.HeadingSlant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White));
            InterfaceStyle.Leaning(h, pivot, m.HeadingSlant, InterfaceStyle.C(v.Red));
            InterfaceStyle.DotsAlong(h, pivot, m.HeadingSlant, 1f - m.HeadingDots, InterfaceStyle.C(v.Black, 0.42f));
            TextLabel(new Rect(h.x + HeadingPad(m), h.y, h.width - HeadingPad(m), h.height), words,
                                 InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Slashes(h.xMax + h.height * m.HeadingSlant * 0.5f + 46f, h.y + 10f, h.height - 20f, 9f, 2, InterfaceStyle.C(v.Red));
        }

        void DrawMenuRow(int i, Rect r, bool selected, bool hover, InterfaceValues v, MenuValues m)
        {
            float pivot = r.center.y, slant = v.Slant;
            bool setting = _rowSetting[i];
            if (selected)
            {
                InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.Teal));
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.White));
                InterfaceStyle.Slashes(r.xMax + 34f, r.y + 5f, r.height - 10f, 6f, 2, InterfaceStyle.C(v.Red));
            }
            else
            {
                InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline * 0.75f, v.Depth, InterfaceStyle.C(v.White, hover ? 0.85f : 0.3f), 0.6f);
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.86f));
                InterfaceStyle.Leaning(new Rect(r.x, r.y, 5f, r.height), pivot, slant, setting ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Red));
            }
            Color ink = selected ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White);
            float pad = RowPad(v, m);
            TextLabel(new Rect(r.x + pad, r.y, r.width - pad, r.height), _rowWords[i], RowWordStyle(m), ink, !selected);
            if (setting)
            {
                string value = _rowValues[i];
                Color valueInk = MenuWords.IsOn(value) ? (selected ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.Teal))
                                 : MenuWords.IsOff(value) ? (selected ? InterfaceStyle.C(v.Black, 0.45f) : InterfaceStyle.C(v.Grey))
                                 : ink;
                float right = r.xMax - ChevronW - 18f;
                TextLabel(new Rect(right - _rowValueW[i] - 4f, r.y, _rowValueW[i] + 4f, r.height), value, RowValueStyle(m), valueInk, !selected);
                GUIStyle chevron = InterfaceStyle.Text(true, m.RowValueSize, TextAnchor.MiddleCenter);
                Color chevronInk = selected ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.White, 0.55f);
                TextLabel(new Rect(right - _rowValueW[i] - 12f - ChevronW, r.y, ChevronW, r.height), "<", chevron, chevronInk);
                TextLabel(new Rect(right + 4f, r.y, ChevronW, r.height), ">", chevron, chevronInk);
            }
            else if (_rowKeys[i].Length > 0)
            {
                float w = _rowKeyW[i], h = m.KeyCapHeight;
                var cap = new Rect(r.xMax - 18f - w, r.y + (r.height - h) * 0.5f, w, h);
                KeyCap(cap, _rowKeys[i], m, v, selected);
            }
        }

        /// <summary>A key cap: white with black letters in a thin black outline (black with white letters on a white row).</summary>
        static void KeyCap(Rect cap, string key, MenuValues m, InterfaceValues v, bool inverse = false)
        {
            InterfaceStyle.HeavyPlate(cap, cap.center.y, v.Slant, 2f, 0f);
            InterfaceStyle.Slanted(cap, inverse ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White));
            TextLabel(cap, key, InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter), inverse ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black));
        }

        void DrawHint(Rect r, InterfaceValues v, MenuValues m)
        {
            float x = r.x + 6f, h = m.KeyCapHeight;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < s_Hint.Length; i++)
            {
                if (s_HintCap[i])
                {
                    KeyCap(new Rect(x, r.y + (r.height - h) * 0.5f, _hintW[i], h), s_Hint[i], m, v);
                    x += _hintW[i] + 6f;
                }
                else
                {
                    TextLabel(new Rect(x + 2f, r.y, _hintW[i] + 6f, r.height), s_Hint[i], words, InterfaceStyle.C(v.White, 0.8f), true);
                    x += _hintW[i] + 18f;
                }
            }
        }

        /// <summary>A compact dark panel in the concept's outline: the heavy black edge with a stroke outside, a teal cut down its left, dots and the red slashes at its top right, its heading over a teal rule.</summary>
        static void DrawPanel(Rect r, string head, string sub, Color stroke, InterfaceValues v, MenuValues m)
        {
            float pivot = r.center.y, slant = m.PanelSlant;
            InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, stroke);
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.9f));
            InterfaceStyle.DotsAlong(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f, m.PanelHeadHeight + m.PanelPad), pivot, slant, 0.15f, InterfaceStyle.C(v.Teal, 0.2f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, 5f, r.height), pivot, slant, InterfaceStyle.C(v.Teal));
            float lean = (pivot - (r.y + m.PanelPad + m.PanelHeadHeight * 0.4f)) * slant;
            InterfaceStyle.Slashes(r.xMax + lean - 14f, r.y + m.PanelPad - 2f, 26f, 6f, 2, InterfaceStyle.C(v.Red));
            var headRect = new Rect(r.x + m.PanelPad + 8f + lean, r.y + m.PanelPad - 4f, r.width - 2f * m.PanelPad - 50f, m.PanelHeadHeight * 0.8f);
            TextLabel(headRect, head, InterfaceStyle.Text(true, m.PanelHeadSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            TextLabel(headRect, sub, InterfaceStyle.Text(false, m.PanelSubSize, TextAnchor.MiddleRight), InterfaceStyle.C(v.Grey), true);
            InterfaceStyle.Slanted(new Rect(headRect.x, r.y + m.PanelPad + m.PanelHeadHeight * 0.8f, r.width * 0.3f, 2f), InterfaceStyle.C(v.Teal));
        }

        void DrawStats(Rect r, InterfaceValues v, MenuValues m)
        {
            DrawPanel(r, StatsHead, StatsSub, InterfaceStyle.C(v.Teal), v, m);
            float pivot = r.center.y, x0 = r.x + m.PanelPad + 10f, top = r.y + m.PanelPad + m.PanelHeadHeight, inner = _colW * 2f + m.ColumnInnerGap;
            for (int line = 0; line < _statLines; line += 2)
            {
                float y = top + line * m.StatLine, lean = (pivot - (y + m.StatLine * 0.5f)) * m.PanelSlant;
                InterfaceStyle.Fill(new Rect(x0 + lean - 6f, y + 1f, inner + 12f, m.StatLine - 2f), InterfaceStyle.C(v.White, 0.045f));
            }
            GUIStyle name = InterfaceStyle.Text(false, m.StatNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, m.StatValueSize, TextAnchor.MiddleRight),
                     change = InterfaceStyle.Text(true, m.StatChangeSize, TextAnchor.MiddleRight);
            for (int i = 0; i < _stats.Count && i < _statLine.Count; i++)
            {
                MenuStat s = _stats[i];
                float y = top + _statLine[i] * m.StatLine, lean = (pivot - (y + m.StatLine * 0.5f)) * m.PanelSlant;
                float x = x0 + lean + (_statCol[i] == 1 ? _colW + m.ColumnInnerGap : 0f), w = _statCol[i] == -1 ? inner : _colW;
                TextLabel(new Rect(x, y, w, m.StatLine), s.Name, name, InterfaceStyle.C(v.White, 0.72f));
                TextLabel(new Rect(x, y, w, m.StatLine), s.Value, value, s.Changed ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White), true);
                if (s.Changed && s.Change.Length > 0)
                    TextLabel(new Rect(x, y, w - _statValueW[i] - 10f, m.StatLine), s.Change, change, InterfaceStyle.C(v.Teal, 0.8f));
            }
        }

        void DrawKeys(Rect r, InterfaceValues v, MenuValues m)
        {
            DrawPanel(r, KeysHead, KeysSub, InterfaceStyle.C(v.White), v, m);
            float pivot = r.center.y, x0 = r.x + m.PanelPad + 10f, top = r.y + m.PanelPad + m.PanelHeadHeight;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < _keyCaps.Count && i < _keyLine.Count; i++)
            {
                float y = top + _keyLine[i] * m.KeyLine, lean = (pivot - (y + m.KeyLine * 0.5f)) * m.PanelSlant;
                float x = x0 + lean + (_keyCol[i] == 1 ? _colW + m.ColumnInnerGap : 0f);
                KeyCap(new Rect(x + 4f, y + (m.KeyLine - m.KeyCapHeight) * 0.5f, _capW[i], m.KeyCapHeight), _keyCaps[i].Key, m, v);
                TextLabel(new Rect(x + _capW[i] + 16f, y, _colW * 2f, m.KeyLine), _keyCaps[i].Words, words, InterfaceStyle.C(v.White, 0.9f), true);
            }
        }

        // ---------------- The run's end ----------------

        /// <summary>How many times the end screen's lines were made (work order 078: when it opens and when what it says changes, not every frame). For tests.</summary>
        public int EndLinesMade { get; private set; }

        bool _endLinesWanted;
        GeneratedLevels.EndKind _endKeyEnding;
        RunRecord _endKeyRecord;
        int _endKeyKills = -1, _endKeyEarned = -1;
        bool _endKeyMeta, _endKeyAttach, _endKeyDev;

        /// <summary>Has what the end screen says changed since its lines were made (the end itself, its record, a kill counted just after, the trial's earnings)? Nothing made in asking.</summary>
        bool EndLinesStale(RunRecord record)
        {
            GeneratedLevels.EndKind ending = _levels != null ? _levels.Ending : GeneratedLevels.EndKind.None;
            int kills = record != null ? record.Kills : -1;
            RunEarnings earnings = _levels != null ? _levels.Earnings : null;
            int earned = earnings != null && earnings.Committed ? earnings.TotalAfter : -1;
            bool meta = _levels != null && _levels.MetaOn, attach = _levels != null && _levels.AttachmentsOn, dev = DeveloperKeys.On;
            if (ending == _endKeyEnding && ReferenceEquals(record, _endKeyRecord) && kills == _endKeyKills && earned == _endKeyEarned
                && meta == _endKeyMeta && attach == _endKeyAttach && dev == _endKeyDev && (ending == GeneratedLevels.EndKind.None || _endLines.Count > 0))
                return false;
            _endKeyEnding = ending;
            _endKeyRecord = record;
            _endKeyKills = kills;
            _endKeyEarned = earned;
            _endKeyMeta = meta;
            _endKeyAttach = attach;
            _endKeyDev = dev;
            return true;
        }

        // What the end screen draws, made once from the record when its lines are made.
        bool _endLaidOut;
        Vector2Int _endLaidOutAt;
        float _eFit = 1f, _eViewWidth;
        string _verdict = "", _where = "", _seed = "";
        bool _endDied;
        readonly List<string> _blockLabel = new List<string>(), _blockNumber = new List<string>();
        readonly List<string> _chipText = new List<string>();
        readonly List<UiInk> _chipInk = new List<UiInk>();
        readonly List<int> _chipKind = new List<int>(); // 0 a skill (a white plate), 1 an upgrade, 2 a skill's upgrade, 3 the focus choice, 4 none taken
        readonly List<float> _chipW = new List<float>(), _chipX = new List<float>();
        readonly List<int> _chipRow = new List<int>();
        int _chipRows;
        readonly List<string> _endMore = new List<string>();
        readonly string[] _buttonKey = new string[3], _buttonWords = new string[3];
        readonly float[] _buttonCapW = new float[3];
        float _chipsTop, _moreTop, _buttonsTop, _blocksTop, _verdictHalf;
        const string PicksHead = "THE RUN'S PICKS";

        /// <summary>The end screen's verdict as drawn ("DEAD", "CLEARED"), its chips (the run's picks) and the factor it is drawn at. For tests.</summary>
        public string EndVerdict
        {
            get
            {
                LayOutEnd();
                return _verdict;
            }
        }

        public IReadOnlyList<string> EndChips
        {
            get
            {
                LayOutEnd();
                return _chipText;
            }
        }

        public float EndFit
        {
            get
            {
                LayOutEnd();
                return _eFit;
            }
        }

        void LayOutEnd()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_endLaidOut && _endLaidOutAt == at) return;
            _endLaidOut = true;
            _endLaidOutAt = at;
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            RunRecord record = _levels != null ? _levels.Record : null;
            _endDied = _levels != null && _levels.Ending == GeneratedLevels.EndKind.Died;
            _verdict = _endDied ? m.DeadWord : m.ClearedWord;
            _verdictHalf = InterfaceStyle.Measure(_verdict, InterfaceStyle.Text(true, m.VerdictSize, TextAnchor.MiddleCenter)).x * 0.5f;
            _where = _endLines.Count > 1 ? _endLines[1] : "";
            _blockLabel.Clear();
            _blockNumber.Clear();
            _chipText.Clear();
            _chipInk.Clear();
            _chipKind.Clear();
            _endMore.Clear();
            _seed = _endLines.Count > 0 ? _endLines[_endLines.Count - 1] : "";
            if (record != null)
            {
                _blockLabel.Add("TIME");
                _blockNumber.Add(RunWords.Clock(record.Seconds, tenths: true));
                _blockLabel.Add("KILLS");
                _blockNumber.Add(record.Kills.ToString(s_Inv078));
                if (!_endDied)
                {
                    _blockLabel.Add("HEALTH LEFT");
                    _blockNumber.Add(record.HealthLeft.ToString(s_Inv078) + " / " + record.MaxHealth.ToString(s_Inv078));
                }
                for (int i = 0; i < record.Skills.Count; i++)
                {
                    UpgradeTier tier = record.SkillTierAt(i);
                    AddChip(record.SkillNamed(i), InterfaceRules.TierInk(tier != UpgradeTier.Common, tier), 0);
                }
                // The upgrades, the same ones (and tier) counted together in the order first taken, as the run box words them.
                for (int i = 0; i < record.Upgrades.Count; i++)
                {
                    string u = record.Upgrades[i];
                    UpgradeTier tier = record.UpgradeTierAt(i);
                    bool seen = false;
                    for (int j = 0; j < i && !seen; j++) seen = record.Upgrades[j] == u && record.UpgradeTierAt(j) == tier;
                    if (seen) continue;
                    int count = 0;
                    for (int j = i; j < record.Upgrades.Count; j++)
                        if (record.Upgrades[j] == u && record.UpgradeTierAt(j) == tier) count++;
                    AddChip(count > 1 ? u + "  x" + count.ToString(s_Inv078) : u, InterfaceRules.TierInk(tier != UpgradeTier.Common, tier), 1);
                }
                foreach (string s in record.SkillUpgrades) AddChip(s, UiInk.Teal, 2);
                if (record.FocusChoice.Length > 0) AddChip("FOCUS  " + record.FocusChoice, UiInk.Teal, 3);
                if (_chipText.Count == 0) AddChip("no picks this run", UiInk.Grey, 4);
                // The lines the end screen says after the picks (the trial's earnings, the weapon, feats): as its lines word them.
                int fixedLines = _endDied ? 8 : 9;
                for (int i = fixedLines; i < _endLines.Count - 1; i++) _endMore.Add(_endLines[i]);
            }
            for (int b = 0; b < 3; b++)
            {
                string text = b == 0 ? "Enter  new run" : b == 1 ? EndSameRunButton : EndPracticeButton;
                MenuWords.Button(text, out _buttonKey[b], out _buttonWords[b]);
                _buttonWords[b] = _buttonWords[b].ToUpperInvariant();
                _buttonCapW[b] = _buttonKey[b].Length > 0 ? Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(_buttonKey[b], CapStyle(m)).x + 18f) : 0f;
            }
            GUIStyle chip = InterfaceStyle.Text(true, m.ChipTextSize, TextAnchor.MiddleCenter);
            _chipW.Clear();
            foreach (string t in _chipText) _chipW.Add(InterfaceStyle.Measure(t, chip).x + 2f * m.ChipPad + v.Slant * m.ChipHeight);
            _chipRows = MenuLayout.Chips(_chipW, m.ChipsWidth, m.ChipGap, _chipX, _chipRow);

            // Down the screen: the band, where she got to, the blocks, the picks, the lines after them, the answers.
            float y = m.BandTop + m.BandHeight + 14f;
            y += m.WhereSize * 1.4f + 14f;
            _blocksTop = y;
            y += m.BlockHeight + 30f;
            _chipsTop = y + 34f;
            y = _chipsTop + _chipRows * (m.ChipHeight + m.ChipGap);
            _moreTop = y + 8f;
            y = _moreTop + (_endMore.Count + 1) * m.EndLine;
            _buttonsTop = y + m.ButtonsGap;
            float height = _buttonsTop + m.ButtonHeight + m.Margin;
            float width = Mathf.Max(m.ChipsWidth, 3f * m.ButtonWidth + 2f * m.ButtonGap);
            float viewWidth = LayoutViewWidth;
            _eFit = MenuLayout.Fit(width, height, viewWidth, v.LayoutHeight, m);
            _eViewWidth = viewWidth / _eFit;
        }

        void AddChip(string text, UiInk ink, int kind)
        {
            _chipText.Add(text);
            _chipInk.Add(ink);
            _chipKind.Add(kind);
        }

        /// <summary>What of the end screen's words does not fit inside its shape at the screen now, as words; empty when everything fits. For tests.</summary>
        public string EndTextOutside()
        {
            LayOutEnd();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            var problems = new List<string>();
            Vector2 vs = InterfaceStyle.Measure(_verdict, InterfaceStyle.Text(true, m.VerdictSize, TextAnchor.MiddleCenter));
            if (vs.x > _eViewWidth - 200f) problems.Add("the verdict is wider than the screen");
            if (vs.y > m.BandHeight * 1.15f) problems.Add("the verdict is taller than its band");
            if (InterfaceStyle.Measure(_where, InterfaceStyle.Text(true, m.WhereSize, TextAnchor.MiddleCenter)).x > m.ChipsWidth) problems.Add("where she got to is wider than the picks");
            GUIStyle number = InterfaceStyle.Text(true, m.BlockNumberSize, TextAnchor.MiddleCenter);
            for (int i = 0; i < _blockNumber.Count; i++)
                if (InterfaceStyle.Measure(_blockNumber[i], number).x > m.BlockWidth - v.Slant * m.BlockHeight - 16f) problems.Add("the block " + _blockLabel[i] + " is narrower than its number");
            for (int i = 0; i < _chipW.Count; i++)
                if (_chipW[i] > m.ChipsWidth) problems.Add("the chip " + _chipText[i] + " is wider than the picks");
            GUIStyle more = InterfaceStyle.Text(false, m.EndLineSize, TextAnchor.MiddleCenter);
            foreach (string line in _endMore)
                if (InterfaceStyle.Measure(line, more).x > _eViewWidth - 2f * m.Margin) problems.Add("the line " + line + " is wider than the screen");
            GUIStyle button = InterfaceStyle.Text(true, m.ButtonTextSize, TextAnchor.MiddleLeft);
            for (int b = 0; b < 3; b++)
                if (ButtonPad(v, m) + _buttonCapW[b] + 12f + InterfaceStyle.Measure(_buttonWords[b], button).x > m.ButtonWidth - ButtonPad(v, m) * 0.5f) problems.Add("the button " + _buttonWords[b] + " is narrower than its words");
            return string.Join("; ", problems);
        }

        static float ButtonPad(InterfaceValues v, MenuValues m) => v.Slant * m.ButtonHeight * 0.5f + 14f;

        /// <summary>The end screen's three answers in the 1080 layout after the fit: a new run, the same run, the practice room. For tests.</summary>
        public Rect EndButtonRect(int index)
        {
            LayOutEnd();
            Rect r = ButtonRect(index, InterfaceStyle.V.Menus);
            return new Rect(r.x * _eFit, r.y * _eFit, r.width * _eFit, r.height * _eFit);
        }

        Rect ButtonRect(int b, MenuValues m)
        {
            float total = 3f * m.ButtonWidth + 2f * m.ButtonGap, x = (_eViewWidth - total) * 0.5f + b * (m.ButtonWidth + m.ButtonGap);
            return new Rect(x, _buttonsTop, m.ButtonWidth, m.ButtonHeight);
        }

        void DrawEndConcept()
        {
            LayOutEnd();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            InterfaceStyle.Fill(new Rect(0f, 0f, viewWidth, v.LayoutHeight), new Color(0f, 0f, 0f, Mathf.Clamp01(m.Veil + 0.1f)));
            Matrix4x4 layout = GUI.matrix;
            if (_eFit < 1f) GUI.matrix = layout * Matrix4x4.Scale(new Vector3(_eFit, _eFit, 1f));
            float w = _eViewWidth, mid = w * 0.5f;
            Color hot = _endDied ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.Teal);

            // The band across the screen and the verdict on it, an arcade round's end: a stripe of the verdict's colour under
            // it, the band black in the heavy outline with a white stroke, dots of its colour toward both ends, slashes either side.
            float bandW = w * Mathf.Clamp01(m.BandShare);
            var band = new Rect(mid - bandW * 0.5f, m.BandTop, bandW, m.BandHeight);
            float pivot = band.center.y;
            InterfaceStyle.Leaning(new Rect(band.x, band.yMax + 6f, band.width, 14f), band.yMax + 13f, m.BandSlant, hot);
            InterfaceStyle.InkedPlate(band, pivot, m.BandSlant, v.HeavyOutline * 1.5f, v.Depth * 3f, InterfaceStyle.C(v.White), 1.4f);
            InterfaceStyle.Leaning(band, pivot, m.BandSlant, InterfaceStyle.C(v.Black, 0.94f));
            InterfaceStyle.DotsAlong(new Rect(band.x + band.width * 0.62f, band.y, band.width * 0.38f, band.height), pivot, m.BandSlant, 0.05f, new Color(hot.r, hot.g, hot.b, 0.3f));
            GUIStyle verdict = InterfaceStyle.Text(true, m.VerdictSize, TextAnchor.MiddleCenter);
            TextLabel(new Rect(band.x + 6f, band.y + 7f, band.width, band.height), _verdict, verdict, InterfaceStyle.C(v.Black), false);
            TextLabel(new Rect(band.x - 3f, band.y - 2f, band.width, band.height), _verdict, verdict, hot, false);
            float half = _verdictHalf;
            InterfaceStyle.Slashes(mid - half - 40f, band.y + 40f, band.height - 80f, 14f, 2, hot);
            InterfaceStyle.Slashes(mid + half + 94f, band.y + 40f, band.height - 80f, 14f, 2, InterfaceStyle.C(v.White));

            // Where she got to.
            float y = band.yMax + 14f + 10f;
            TextLabel(new Rect(0f, y, w, m.WhereSize * 1.4f), _where, InterfaceStyle.Text(true, m.WhereSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);

            // The blocks: the label teal over a large white numeral on a slanted dark plate.
            int n = _blockNumber.Count;
            float total = n * m.BlockWidth + (n - 1) * m.BlockGap, bx = mid - total * 0.5f;
            for (int i = 0; i < n; i++)
            {
                var block = new Rect(bx + i * (m.BlockWidth + m.BlockGap), _blocksTop, m.BlockWidth, m.BlockHeight);
                float bp = block.center.y;
                InterfaceStyle.InkedPlate(block, bp, v.Slant, v.HeavyOutline, v.Depth * 2f, i == 0 ? hot : InterfaceStyle.C(v.White, 0.6f), 0.8f);
                InterfaceStyle.Leaning(block, bp, v.Slant, InterfaceStyle.C(v.Black, 0.9f));
                InterfaceStyle.DotsAlong(block, bp, v.Slant, 0.6f, InterfaceStyle.C(v.White, 0.08f));
                TextLabel(new Rect(block.x + 20f, block.y + 4f, block.width - 30f, 24f), _blockLabel[i], InterfaceStyle.Text(true, m.BlockLabelSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal));
                TextLabel(new Rect(block.x, block.y + 22f, block.width - 8f, block.height - 22f), _blockNumber[i], InterfaceStyle.Text(true, m.BlockNumberSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);
            }

            // The picks as chips in the offers' language.
            float cx = mid - m.ChipsWidth * 0.5f;
            TextLabel(new Rect(cx, _chipsTop - 32f, m.ChipsWidth, 26f), PicksHead, InterfaceStyle.Text(true, 22, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Grey), true);
            GUIStyle chipStyle = InterfaceStyle.Text(true, m.ChipTextSize, TextAnchor.MiddleCenter);
            for (int i = 0; i < _chipText.Count; i++)
            {
                var c = new Rect(cx + _chipX[i], _chipsTop + _chipRow[i] * (m.ChipHeight + m.ChipGap), Mathf.Min(_chipW[i], m.ChipsWidth), m.ChipHeight);
                float cp = c.center.y;
                Color ink = InterfaceStyle.Ink(_chipInk[i]);
                bool plate = _chipKind[i] == 0;
                Color stroke = _chipInk[i] == UiInk.White ? InterfaceStyle.C(v.White, plate ? 0.9f : 0.45f) : ink;
                InterfaceStyle.InkedPlate(c, cp, v.Slant, v.HeavyOutline * 0.7f, v.Depth, stroke, 0.7f);
                InterfaceStyle.Leaning(c, cp, v.Slant, plate ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black, 0.9f));
                if (!plate) InterfaceStyle.Leaning(new Rect(c.x, c.y, 4f, c.height), cp, v.Slant, _chipInk[i] == UiInk.White ? InterfaceStyle.C(v.Red) : ink);
                Color words = plate ? InterfaceStyle.C(v.Black) : _chipKind[i] == 1 ? InterfaceStyle.C(v.White) : _chipKind[i] == 4 ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.Teal);
                TextLabel(c, _chipText[i], chipStyle, words, false);
            }

            // The lines after them, and the run's code small.
            GUIStyle more = InterfaceStyle.Text(false, m.EndLineSize, TextAnchor.MiddleCenter);
            for (int i = 0; i < _endMore.Count; i++)
                TextLabel(new Rect(0f, _moreTop + i * m.EndLine, w, m.EndLine), _endMore[i], more, InterfaceStyle.C(v.White, 0.85f), true);
            TextLabel(new Rect(0f, _moreTop + _endMore.Count * m.EndLine, w, m.EndLine), _seed, InterfaceStyle.Text(false, m.EndLineSize - 3, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Grey, 0.8f), true);

            // The answers: key caps on slanted buttons, the first (Enter, a new run) white.
            Event e = Event.current;
            GUIStyle button = InterfaceStyle.Text(true, m.ButtonTextSize, TextAnchor.MiddleLeft);
            for (int b = 0; b < 3; b++)
            {
                Rect r = ButtonRect(b, m);
                bool first = b == 0, hover = e != null && r.Contains(e.mousePosition);
                float rp = r.center.y;
                InterfaceStyle.InkedPlate(r, rp, v.Slant, v.HeavyOutline, v.Depth * 2f, first ? hot : InterfaceStyle.C(v.White, hover ? 0.9f : 0.4f), first ? 1f : 0.7f);
                InterfaceStyle.Leaning(r, rp, v.Slant, first ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black, 0.88f));
                float pad = ButtonPad(v, m), x = r.x + pad;
                if (_buttonCapW[b] > 0f)
                {
                    KeyCap(new Rect(x, r.y + (r.height - m.KeyCapHeight) * 0.5f, _buttonCapW[b], m.KeyCapHeight), _buttonKey[b], m, v, first);
                    x += _buttonCapW[b] + 12f;
                }
                TextLabel(new Rect(x, r.y, r.xMax - x, r.height), _buttonWords[b], button, first ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White), !first);
                if (first) InterfaceStyle.Slashes(r.xMax - 12f, r.y + 10f, r.height - 20f, 6f, 2, InterfaceStyle.C(v.Red));
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    GUI.matrix = layout;
                    _levels.AnswerEndScreen(b == 0 ? GeneratedLevels.EndAnswer.NewRun : b == 1 ? GeneratedLevels.EndAnswer.SameRun : GeneratedLevels.EndAnswer.PracticeRoom);
                    return;
                }
            }
            GUI.matrix = layout;
        }

        // ---------------- Between runs ----------------

        // What the screen between runs draws (work order 078): the pieces and the parts as cards in the offers' language, made
        // again only when the screen's lines say something new (compared, not made, each frame).
        readonly List<string> _cardSource = new List<string>();
        readonly List<string> _cardName = new List<string>(), _cardSub = new List<string>(), _cardState = new List<string>(), _cardText = new List<string>();
        readonly List<string> _cardChip = new List<string>(), _cardSubLine = new List<string>();
        readonly List<float> _cardChipW = new List<float>(), _cardNameW = new List<float>();
        static readonly string s_SpaceKey = BetweenRunsStartKey.ToString();
        readonly List<int> _cardStateKind = new List<int>(); // 0 equipped or fitted, 1 owned, 2 for sale, 3 out of reach
        readonly List<int> _cardSlotHead = new List<int>();   // the weapon's cards: the slot head before it, or -1
        readonly List<string> _slotHeads = new List<string>();
        int _rackCards;
        float _cardH, _bFit = 1f, _bViewWidth, _bStartCapW, _bSpaceCapW;
        Vector2Int _bLaidOutAt;
        bool _bLaidOut;
        string _bCurrency = "", _bCurrencyName = "", _bBudget = "", _bStart = "", _bNote = "", _bRun = "";
        const string BetweenWord = "BETWEEN RUNS", LoadoutHead = "LOADOUT", LoadoutSub = "fit before the run", TrialWord = "trial";

        /// <summary>The screen between runs' cards as drawn: the rack's pieces then the weapon's parts, by name, and their states. For tests.</summary>
        public IReadOnlyList<string> BetweenCardNames
        {
            get
            {
                LayOutBetween();
                return _cardName;
            }
        }

        public IReadOnlyList<string> BetweenCardStates
        {
            get
            {
                LayOutBetween();
                return _cardState;
            }
        }

        public float BetweenFit
        {
            get
            {
                LayOutBetween();
                return _bFit;
            }
        }

        bool BetweenStale()
        {
            int n = _betweenLines.Count + _weaponLines.Count;
            if (_cardSource.Count != n) return true;
            for (int i = 0; i < _betweenLines.Count; i++)
                if (!string.Equals(_betweenLines[i], _cardSource[i], System.StringComparison.Ordinal)) return true;
            for (int i = 0; i < _weaponLines.Count; i++)
                if (!string.Equals(_weaponLines[i], _cardSource[_betweenLines.Count + i], System.StringComparison.Ordinal)) return true;
            return false;
        }

        static string Part(string[] parts, int i) => i < parts.Length ? parts[i].Trim() : "";

        void LayOutBetween()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_bLaidOut && _bLaidOutAt == at && !BetweenStale()) return;
            _bLaidOut = true;
            _bLaidOutAt = at;
            _cardSource.Clear();
            _cardSource.AddRange(_betweenLines);
            _cardSource.AddRange(_weaponLines);
            _cardName.Clear();
            _cardSub.Clear();
            _cardState.Clear();
            _cardText.Clear();
            _cardStateKind.Clear();
            _cardSlotHead.Clear();
            _cardChip.Clear();
            _cardSubLine.Clear();
            _cardChipW.Clear();
            _cardNameW.Clear();
            _slotHeads.Clear();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            if (_betweenLines.Count < 4 || _levels == null) return;
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            _bCurrencyName = mv.CurrencyName.ToUpperInvariant();
            _bCurrency = s.Currency.ToString(s_Inv078);
            _bBudget = _betweenLines[2];
            MenuWords.Action(_betweenLines[3], out _bStart, out _);
            _bStart = _bStart.ToUpperInvariant();
            _bNote = _betweenLines[_betweenLines.Count - 1];
            _bRun = _levels.BetweenRunsSeed.HasValue ? LevelSeed.ToText(_levels.BetweenRunsSeed.Value) : "";
            MetaPiece[] pieces = Pieces;
            // The rack's pieces: "Thick Hide   stats, 2 points   equipped" over its line, as PieceHead and PieceLine word them.
            for (int i = 0; i < pieces.Length && 4 + i < _betweenLines.Count - 1; i++)
            {
                string[] lines = _betweenLines[4 + i].Split('\n');
                string[] head = lines[0].Split(new[] { "   " }, System.StringSplitOptions.RemoveEmptyEntries);
                MetaPiece p = pieces[i];
                string state = Part(head, 2);
                int kind = s.IsEquipped(p.Id) ? 0 : s.Owns(p.Id) ? 1 : p.Sold && MetaRack.CanBuy(s, mv, p.Id) == MetaBuy.Bought ? 2 : 3;
                AddCard(Part(head, 0), Part(head, 1), state, lines.Length > 1 ? lines[1].Trim() : "", kind, -1);
            }
            _rackCards = _cardName.Count;
            // The weapon's parts: each slot's head, then its parts ("Laser Sight   FITTED" over its line and cost).
            int slot = -1;
            for (int i = 2; i < _weaponLines.Count; i++)
            {
                WeaponAttachment p = i < _weaponRows.Count ? _weaponRows[i] : null;
                if (p == null)
                {
                    if (_weaponLines[i].IndexOf(':') < 0) // a slot's head ("SIGHT"), not the weapon's own line ("Pistols:  the basic shot")
                    {
                        _slotHeads.Add(_weaponLines[i]);
                        slot = _slotHeads.Count - 1;
                    }
                    continue;
                }
                string[] lines = _weaponLines[i].Split('\n');
                string[] head = lines[0].Split(new[] { "   " }, System.StringSplitOptions.RemoveEmptyEntries);
                int kind = WeaponRack.IsFitted(s, p.Id) ? 0 : WeaponRack.Owns(s, p.Id) ? 1 : s.Currency >= p.Price ? 2 : 3;
                string text = lines.Length > 1 ? lines[1].Trim() : "";
                if (lines.Length > 2) text += "  " + lines[2].Trim();
                AddCard(Part(head, 0), "", Part(head, 1), text, kind, slot);
                slot = -1;
            }

            // The cards' height: the tallest one's words at the card's width.
            GUIStyle words = InterfaceStyle.Text(false, m.CardTextSize, TextAnchor.UpperLeft, true);
            float textW = m.CardWidth - 2f * m.ChipPad - v.Slant * m.CardHeight;
            float most = 0f;
            foreach (string t in _cardText) most = Mathf.Max(most, InterfaceStyle.MeasureHeight(t, words, textW));
            _cardH = Mathf.Max(m.CardHeight, 12f + m.CardNameSize * 1.45f + 6f + m.CardTextSize * 1.4f + most + 12f);
            _bStartCapW = Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure("Enter", CapStyle(m)).x + 18f);
            _bSpaceCapW = Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(s_SpaceKey, CapStyle(m)).x + 18f);
            GUIStyle chipStyle = InterfaceStyle.Text(true, m.CardTextSize, TextAnchor.MiddleCenter);
            foreach (string c in _cardChip) _cardChipW.Add(c.Length > 0 ? InterfaceStyle.Measure(c, chipStyle).x + 18f : 0f);
            GUIStyle nameStyle = InterfaceStyle.Text(true, m.CardNameSize, TextAnchor.MiddleLeft);
            foreach (string n in _cardName) _cardNameW.Add(InterfaceStyle.Measure(n, nameStyle).x);

            float height = BetweenGridTop(m) + RackRows() * (_cardH + m.CardGap) + 3f * m.EndLine + m.Margin;
            float weaponH = BetweenGridTop(m) - m.ButtonHeight - 24f + WeaponPanelHeight(m);
            height = Mathf.Max(height, weaponH + m.Margin);
            float viewWidth = LayoutViewWidth;
            _bFit = MenuLayout.Fit(BetweenWidth(m), height, viewWidth, v.LayoutHeight, m);
            _bViewWidth = viewWidth / _bFit;
        }

        void AddCard(string name, string sub, string state, string text, int kind, int slotHead)
        {
            _cardName.Add(name);
            _cardSub.Add(sub);
            _cardState.Add(state);
            _cardText.Add(!ChipFits(state) && state.Length > 0 ? state + ".  " + text : text); // round 2: a long state on the sub line ran past the card
            _cardStateKind.Add(kind);
            _cardSlotHead.Add(slotHead);
            bool chip = ChipFits(state);
            _cardChip.Add(chip ? state.ToUpperInvariant() : "");
            _cardSubLine.Add(sub);
        }

        const int RackColumns = 3;
        int RackRows() => (_rackCards + RackColumns - 1) / RackColumns;
        float RackWidth(MenuValues m) => RackColumns * m.CardWidth + (RackColumns - 1) * m.CardGap;
        bool WeaponCards => _cardName.Count > _rackCards;
        float BetweenWidth(MenuValues m) => RackWidth(m) + (WeaponCards ? m.ColumnGap + m.CardWidth + 2f * m.PanelPad : 0f);
        float BetweenGridTop(MenuValues m) => m.BetweenTop + m.HeadingHeight + m.HeadingGap + m.ButtonHeight + 24f;
        float WeaponPanelHeight(MenuValues m) => 2f * m.PanelPad + m.PanelHeadHeight + _slotHeads.Count * 30f + (_cardName.Count - _rackCards) * (_cardH + m.CardGap);
        float BetweenLeft(MenuValues m) => (_bViewWidth - BetweenWidth(m)) * 0.5f;

        /// <summary>Card <paramref name="index"/> (the rack's pieces, then the weapon's parts) in the 1080 layout before the fit.</summary>
        Rect CardRect(int index, MenuValues m)
        {
            float x0 = BetweenLeft(m), top = BetweenGridTop(m);
            if (index < _rackCards)
                return new Rect(x0 + (index % RackColumns) * (m.CardWidth + m.CardGap), top + (index / RackColumns) * (_cardH + m.CardGap), m.CardWidth, _cardH);
            float x = x0 + RackWidth(m) + m.ColumnGap + m.PanelPad, y = top - m.ButtonHeight - 24f + m.PanelPad + m.PanelHeadHeight;
            for (int i = _rackCards; i <= index; i++)
            {
                if (_cardSlotHead[i] >= 0) y += 30f;
                if (i < index) y += _cardH + m.CardGap;
            }
            return new Rect(x, y, m.CardWidth, _cardH);
        }

        /// <summary>A card of the screen between runs in the 1080 layout after the fit. For tests.</summary>
        public Rect BetweenCardRect(int index)
        {
            LayOutBetween();
            Rect r = CardRect(index, InterfaceStyle.V.Menus);
            return new Rect(r.x * _bFit, r.y * _bFit, r.width * _bFit, r.height * _bFit);
        }

        /// <summary>What of the screen between runs' words does not fit inside its shape at the screen now; empty when everything fits. For tests.</summary>
        public string BetweenTextOutside()
        {
            LayOutBetween();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            var problems = new List<string>();
            GUIStyle name = InterfaceStyle.Text(true, m.CardNameSize, TextAnchor.MiddleLeft), chip = InterfaceStyle.Text(true, m.CardTextSize, TextAnchor.MiddleCenter);
            for (int i = 0; i < _cardName.Count; i++)
            {
                float stateW = _cardChipW[i];
                if (CardPad(v, m) + InterfaceStyle.Measure(_cardName[i], name).x + 12f + stateW > m.CardWidth) problems.Add("card " + _cardName[i] + ": its name and state are wider than the card");
                if (_cardSubLine[i].Length > 0 && CardPad(v, m) + InterfaceStyle.Measure(_cardSubLine[i], InterfaceStyle.Text(true, m.CardTextSize - 1, TextAnchor.MiddleLeft)).x > m.CardWidth - 10f)
                    problems.Add("card " + _cardName[i] + ": its kind and points are wider than the card");
            }
            if (HeadingPad(m) + InterfaceStyle.Measure(BetweenWord, InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft)).x + 16f > BetweenHeadingWidth(m)) problems.Add("the heading is wider than its plate");
            GUIStyle button = InterfaceStyle.Text(true, m.ButtonTextSize, TextAnchor.MiddleLeft);
            float budget = InterfaceStyle.Measure(_bBudget, InterfaceStyle.Text(false, m.EndLineSize, TextAnchor.MiddleRight)).x;
            if (2f * ButtonPad(v, m) + _bStartCapW + _bSpaceCapW + 30f + InterfaceStyle.Measure(_bStart, button).x + 20f + budget > RackWidth(m)) problems.Add("the start's words and the budget are wider than its button");
            return string.Join("; ", problems);
        }

        static bool ChipFits(string state) => state.Length > 0 && state.Length <= 22;
        static float CardPad(InterfaceValues v, MenuValues m) => v.Slant * m.CardHeight * 0.5f + 12f;
        static float BetweenHeadingWidth(MenuValues m) => m.HeadingWidth + 150f;

        void DrawBetweenRunsConcept(float viewWidth)
        {
            if (_betweenLines.Count < 4) return;
            LayOutBetween();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            float layoutWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            InterfaceStyle.Fill(new Rect(0f, 0f, layoutWidth, v.LayoutHeight), new Color(0f, 0f, 0f, Mathf.Clamp01(m.Veil + 0.1f)));
            Matrix4x4 layout = GUI.matrix;
            if (_bFit < 1f) GUI.matrix = layout * Matrix4x4.Scale(new Vector3(_bFit, _bFit, 1f));
            float x0 = BetweenLeft(m);

            // The heading, "trial" beside it, and the currency as a block of numbers.
            // Round 2: the heading in the middle across, clear of the fight's plate at the top left, so the screen can start higher.
            var heading = new Rect((_bViewWidth - BetweenHeadingWidth(m)) * 0.5f, m.BetweenTop, BetweenHeadingWidth(m), m.HeadingHeight);
            DrawHeadingPlate(heading, BetweenWord, v, m);
            TextLabel(new Rect(heading.x, heading.y, heading.width - 26f, heading.height - 8f), TrialWord, InterfaceStyle.Text(false, m.PanelSubSize + 3, TextAnchor.LowerRight), InterfaceStyle.C(v.Black, 0.7f));
            var block = new Rect(x0 + BetweenWidth(m) - m.BlockWidth, m.BetweenTop, m.BlockWidth, m.HeadingHeight);
            InterfaceStyle.InkedPlate(block, block.center.y, v.Slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.Gold), 0.8f);
            InterfaceStyle.Leaning(block, block.center.y, v.Slant, InterfaceStyle.C(v.Black, 0.9f));
            TextLabel(new Rect(block.x + 22f, block.y + 4f, block.width - 30f, 24f), _bCurrencyName, InterfaceStyle.Text(true, m.BlockLabelSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Gold));
            TextLabel(new Rect(block.x, block.y + 18f, block.width - 10f, block.height - 18f), _bCurrency, InterfaceStyle.Text(true, m.BlockNumberSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.White), true);

            // The start: a white button with its keys on caps.
            Event e = Event.current;
            var start = new Rect(x0, m.BetweenTop + m.HeadingHeight + m.HeadingGap, RackWidth(m), m.ButtonHeight);
            bool startOn = BetweenRunsSelected == 0;
            InterfaceStyle.InkedPlate(start, start.center.y, v.Slant, v.HeavyOutline, v.Depth * 2f, startOn ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White, 0.5f));
            InterfaceStyle.Leaning(start, start.center.y, v.Slant, InterfaceStyle.C(v.White));
            float sx = start.x + ButtonPad(v, m), capY = start.y + (start.height - m.KeyCapHeight) * 0.5f;
            KeyCap(new Rect(sx, capY, _bStartCapW, m.KeyCapHeight), "Enter", m, v, true);
            KeyCap(new Rect(sx + _bStartCapW + 8f, capY, _bSpaceCapW, m.KeyCapHeight), s_SpaceKey, m, v, true);
            TextLabel(new Rect(sx + _bStartCapW + _bSpaceCapW + 24f, start.y, start.width, start.height), _bStart, InterfaceStyle.Text(true, m.ButtonTextSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Black));
            TextLabel(new Rect(start.x, start.y, start.width - ButtonPad(v, m), start.height), _bBudget, InterfaceStyle.Text(false, m.EndLineSize, TextAnchor.MiddleRight), InterfaceStyle.C(v.Black, 0.75f));
            if (startOn) InterfaceStyle.Slashes(start.xMax + 34f, start.y + 8f, start.height - 16f, 7f, 2, InterfaceStyle.C(v.Red));
            if (GUI.Button(start, GUIContent.none, GUIStyle.none))
            {
                GUI.matrix = layout;
                PickBetweenRunsLine(0);
                return; // the screen is gone
            }

            // The weapon's panel behind its cards.
            if (WeaponCards)
            {
                var panel = new Rect(x0 + RackWidth(m) + m.ColumnGap, start.y, m.CardWidth + 2f * m.PanelPad, WeaponPanelHeight(m));
                DrawPanel(panel, LoadoutHead, LoadoutSub, InterfaceStyle.C(v.White), v, m);
            }

            // The cards.
            MetaState s = _levels.BetweenRunsState;
            MetaPiece[] pieces = Pieces;
            for (int i = 0; i < _cardName.Count; i++)
            {
                Rect r = CardRect(i, m);
                if (i >= _rackCards && _cardSlotHead[i] >= 0)
                    TextLabel(new Rect(r.x + 10f, r.y - 30f, r.width, 26f), _slotHeads[_cardSlotHead[i]], InterfaceStyle.Text(true, 20, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal), true);
                int row = i < _rackCards ? i + 1 : BetweenRunsRows + (i - _rackCards);
                bool selected = row == BetweenRunsSelected, hover = e != null && r.Contains(e.mousePosition);
                DrawCard(i, r, selected, hover, v, m);
                bool strike = i < _rackCards && i < pieces.Length && pieces[i].Effect == MetaEffect.StrikeOut && s.Owns(pieces[i].Id);
                if (strike)
                {
                    var less = new Rect(r.xMax - 70f, r.yMax - 34f, 28f, 28f);
                    var more = new Rect(r.xMax - 38f, r.yMax - 34f, 28f, 28f);
                    KeyCap(less, "<", m, v);
                    KeyCap(more, ">", m, v);
                    if (GUI.Button(less, GUIContent.none, GUIStyle.none)) { BetweenRunsSelected = row; _levels.ChangeStruckOut(-1); continue; }
                    if (GUI.Button(more, GUIContent.none, GUIStyle.none)) { BetweenRunsSelected = row; _levels.ChangeStruckOut(1); continue; }
                }
                if (GUI.Button(r, GUIContent.none, GUIStyle.none))
                {
                    if (i < _rackCards) PickBetweenRunsLine(row);
                    else PickWeaponLine(i - _rackCards);
                }
            }

            // The last thing tried, and the keys.
            float y = BetweenGridTop(m) + RackRows() * (_cardH + m.CardGap) + 4f;
            TextLabel(new Rect(x0, y, RackWidth(m), m.EndLine), _bNote, InterfaceStyle.Text(false, m.EndLineSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal), true);
            y += m.EndLine + 6f;
            float x = x0 + 6f;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < s_BetweenHint.Length; i++)
            {
                if (s_BetweenHintCap[i])
                {
                    float w = s_BetweenHint[i].Length > 2 ? 64f : 34f;
                    KeyCap(new Rect(x, y, w, m.KeyCapHeight), s_BetweenHint[i], m, v);
                    x += w + 6f;
                }
                else
                {
                    TextLabel(new Rect(x + 2f, y, 260f, m.KeyCapHeight), s_BetweenHint[i], words, InterfaceStyle.C(v.White, 0.8f), true);
                    x += s_BetweenHintW[i] + 20f;
                }
            }
            GUI.matrix = layout;
        }

        static readonly string[] s_BetweenHint = { "Up", "Down", "Enter", "buy, equip, take off", "<", ">", "the skill struck out", "Esc", "practice room" };
        static readonly bool[] s_BetweenHintCap = { true, true, true, false, true, true, false, true, false };
        static readonly float[] s_BetweenHintW = { 0f, 0f, 0f, 170f, 0f, 0f, 160f, 0f, 120f };

        /// <summary>A card in the offers' language: the heavy outline with a stroke of its state's colour, a black face, a teal band and dots at its top, the name on a white plate in black italic, its state on a chip, its words.</summary>
        void DrawCard(int i, Rect r, bool selected, bool hover, InterfaceValues v, MenuValues m)
        {
            float pivot = r.center.y, slant = v.Slant * 0.5f;
            int kind = _cardStateKind[i];
            Color stateInk = kind == 0 ? InterfaceStyle.C(v.Teal) : kind == 1 ? InterfaceStyle.C(v.White) : kind == 2 ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.Grey);
            Color stroke = selected ? InterfaceStyle.C(v.Teal) : kind == 0 ? InterfaceStyle.C(v.Teal, 0.9f) : InterfaceStyle.C(v.White, hover ? 0.9f : 0.35f);
            InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, stroke, selected ? 1.4f : 0.8f);
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.92f));
            InterfaceStyle.DotsAlong(new Rect(r.x + r.width * 0.45f, r.y, r.width * 0.55f, r.height * 0.4f), pivot, slant, 0.1f, InterfaceStyle.C(kind == 0 ? v.Teal : v.White, kind == 0 ? 0.25f : 0.1f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, r.width, 5f), pivot, slant, kind == 0 ? InterfaceStyle.C(v.Teal) : kind == 2 ? InterfaceStyle.C(v.Gold, 0.8f) : InterfaceStyle.C(v.White, 0.25f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, 5f, r.height), pivot, slant, kind == 3 ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.Teal));
            if (selected) InterfaceStyle.Slashes(r.xMax - 14f, r.yMax - 34f, 22f, 6f, 2, InterfaceStyle.C(v.Red));
            float lean = (pivot - (r.y + 12f + m.CardNameSize * 0.7f)) * slant, pad = CardPad(v, m) * 0.6f;
            var name = new Rect(r.x + pad + lean, r.y + 12f, r.width - 2f * pad, m.CardNameSize * 1.45f);
            GUIStyle nameStyle = InterfaceStyle.Text(true, m.CardNameSize, TextAnchor.MiddleLeft);
            InterfaceStyle.Slanted(new Rect(name.x - 4f, name.y + 2f, Mathf.Min(_cardNameW[i] + 22f, name.width), name.height - 4f), InterfaceStyle.C(v.White, kind == 3 ? 0.7f : 1f));
            TextLabel(new Rect(name.x + 6f, name.y, name.width, name.height), _cardName[i], nameStyle, InterfaceStyle.C(v.Black));
            float y = name.yMax + 4f;
            if (_cardChip[i].Length > 0)
            {
                GUIStyle chip = InterfaceStyle.Text(true, m.CardTextSize, TextAnchor.MiddleCenter);
                float w = Mathf.Min(r.width * 0.42f, _cardChipW[i]);
                var c = new Rect(r.xMax - pad - w + lean - 8f, name.y + 6f, w, name.height - 12f);
                InterfaceStyle.Slanted(c, kind == 0 ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Black));
                if (kind != 0) InterfaceStyle.SlantedFrame(c, stateInk, 2f);
                TextLabel(c, _cardChip[i], chip, kind == 0 ? InterfaceStyle.C(v.Black) : stateInk);
            }
            if (_cardSubLine[i].Length > 0)
            {
                TextLabel(new Rect(name.x + 2f, y, r.width - 2f * pad, m.CardTextSize * 1.4f), _cardSubLine[i], InterfaceStyle.Text(true, m.CardTextSize - 1, TextAnchor.MiddleLeft),
                                     kind == 3 ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.Teal), true);
                y += m.CardTextSize * 1.4f;
            }
            TextLabel(new Rect(name.x + 2f, y + 2f, r.width - 2f * pad - 10f, r.yMax - y - 8f), _cardText[i], InterfaceStyle.Text(false, m.CardTextSize, TextAnchor.UpperLeft, true),
                                 InterfaceStyle.C(v.White, kind == 3 ? 0.6f : 0.88f));
        }
    }
}
