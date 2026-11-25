using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The settings screen drawn (work order 128), in the start menu's style (work order 114, decision 0011) with
    /// <see cref="InterfaceStyle"/>'s own shapes and type, no second style. Its list of pages is the start menu's main list: the
    /// heavy italic words on thin slanted strokes, the chosen one on a teal plate standing out with the red double slash, under a
    /// red heading plate "SETTINGS". A page: the start menu's tabs at the top left (GRAPHICS, SOUND, GAMEPLAY, TRIALS), a red
    /// heading plate with the page's name, its lines as slanted dark rows with a teal cut (the chosen one white with black words,
    /// a teal stroke and the red double slash), the value in the heavy italic at the right between small arrows (teal for on,
    /// grey for off), a small grey note after a line's name where it needs one, a greyed line dim. The keys at the foot as white
    /// key caps. Opened from the Esc menu, the game shows through a dark veil; from the start menu, her picture under its veil.
    ///
    /// Its layout is worked out when the screen's size, the page or its rows change, never in a frame that changes nothing; every
    /// click goes through one hit test (<see cref="ClickAt"/>), the drawing's and the tests' alike.
    /// </summary>
    public sealed partial class SettingsScreen
    {
        /// <summary>What a click on a place does.</summary>
        public enum HitKind { Tab, Page, Row, Less, More }

        struct Hit
        {
            public Rect R;
            public HitKind Kind;
            public int Index;
        }

        readonly List<Hit> _hits = new List<Hit>();
        readonly List<float> _valueW = new List<float>(), _nameW = new List<float>(), _hintW = new List<float>();
        bool _laidOut;
        Vector2Int _laidAt;
        int _laidRows = -1;
        float _viewWidth, _headW;
        string[] _hint = s_ListHint;
        bool[] _hintCap = s_ListHintCap;
        Vector2 _mouse = new Vector2(float.NaN, float.NaN);

        static readonly string[] s_ListHint = { "Up", "Down", "choose", "Enter", "open", "Esc", "back" };
        static readonly bool[] s_ListHintCap = { true, true, false, true, false, true, false };
        static readonly string[] s_PageHint = { "Up", "Down", "choose", "<", ">", "change", "Esc", "back" };
        static readonly bool[] s_PageHintCap = { true, true, false, true, true, false, true, false };
        const string LessWord = "<", MoreWord = ">";

        static float ViewHeight => InterfaceStyle.V.LayoutHeight;

        // ---------------- The layout ----------------

        void LayOut()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_laidOut && _laidAt == at && _laidRows == RowsMade) return;
            _laidOut = true;
            _laidAt = at;
            _laidRows = RowsMade;
            _viewWidth = InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            _hits.Clear();
            _hint = OnPage ? s_PageHint : s_ListHint;
            _hintCap = OnPage ? s_PageHintCap : s_ListHintCap;
            GUIStyle cap = CapStyle(m), words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            _hintW.Clear();
            for (int i = 0; i < _hint.Length; i++)
                _hintW.Add(_hintCap[i] ? Mathf.Max(m.KeyCapHeight * 1.2f, InterfaceStyle.Measure(_hint[i], cap).x + 16f) : InterfaceStyle.Measure(_hint[i], words).x);
            string heading = OnPage ? SettingsPages.Words[(int)Page] : L.Heading;
            _headW = Mathf.Max(m.HeadingWidth, HeadingPad(m) + InterfaceStyle.Measure(heading, InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft)).x + 40f);
            _valueW.Clear();
            _nameW.Clear();
            GUIStyle value = ValueStyle(), name = NameStyle();
            for (int i = 0; i < _values.Count; i++) _valueW.Add(InterfaceStyle.Measure(_values[i], value).x);
            for (int i = 0; i < _names.Count; i++) _nameW.Add(InterfaceStyle.Measure(_names[i], name).x);

            if (OnPage)
            {
                for (int t = 0; t < SettingsPages.All.Length; t++) _hits.Add(new Hit { R = TabRect(t, s), Kind = HitKind.Tab, Index = t });
                for (int i = 0; i < _lines.Count; i++)
                {
                    Rect r = RowRect(i, false);
                    Rect less = LessRect(r, i), more = MoreRect(r);
                    _hits.Add(new Hit { R = less, Kind = HitKind.Less, Index = i });
                    _hits.Add(new Hit { R = more, Kind = HitKind.More, Index = i });
                    _hits.Add(new Hit { R = new Rect(r.x, r.y, r.width + L.SelectedShift, r.height), Kind = HitKind.Row, Index = i });
                }
            }
            else
                for (int i = 0; i < SettingsPages.All.Length; i++) _hits.Add(new Hit { R = ListRect(i, s), Kind = HitKind.Page, Index = i });
        }

        static GUIStyle CapStyle(MenuValues m) => InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter);
        static GUIStyle NameStyle() => InterfaceStyle.Text(false, L.NameSize, TextAnchor.MiddleLeft);
        static GUIStyle ValueStyle() => InterfaceStyle.Text(true, L.ValueSize, TextAnchor.MiddleRight);
        static GUIStyle NoteStyle() => InterfaceStyle.Text(false, L.NoteSize, TextAnchor.MiddleLeft);
        static float HeadingPad(MenuValues m) => m.HeadingSlant * m.HeadingHeight * 0.5f + 18f;
        static float RowPad(InterfaceValues v) => v.Slant * L.RowHeight * 0.5f + 18f;

        Rect TabRect(int t, StartMenuValues s) => new Rect(s.TabLeft + t * (s.TabWidth + s.TabGap), s.TabTop, s.TabWidth, s.TabHeight);
        Rect ListRect(int i, StartMenuValues s) => new Rect(s.MainLeft, s.MainTop + i * (s.MainRowHeight + s.MainRowGap), s.MainWidth, s.MainRowHeight);
        Rect HeadRect(MenuValues m, StartMenuValues s) => new Rect(s.TabLeft + m.HeadingHeight * m.HeadingSlant * 0.5f, L.HeadTop, _headW, m.HeadingHeight);
        Rect HintRect(StartMenuValues s) => new Rect(s.EchoesMargin, ViewHeight - s.HintBottom - s.HintHeight, _viewWidth - 2f * s.EchoesMargin, s.HintHeight);

        /// <summary>Row <paramref name="i"/> of the page; the chosen one moved out to the right when <paramref name="shifted"/>.</summary>
        Rect RowRect(int i, bool shifted) =>
            new Rect(L.RowsLeft + (shifted && i == Selected ? L.SelectedShift : 0f), L.RowsTop + i * (L.RowHeight + L.RowGap), L.RowsWidth, L.RowHeight);

        Rect LessRect(Rect r, int i) => new Rect(r.xMax - 2f * L.Chevron - 30f - ValueW(i), r.y, L.Chevron + 10f, r.height);
        Rect MoreRect(Rect r) => new Rect(r.xMax - L.Chevron - 16f, r.y, L.Chevron + 16f, r.height);
        float ValueW(int i) => i >= 0 && i < _valueW.Count ? _valueW[i] : 0f;

        // ---------------- The hit test ----------------

        /// <summary>The place under a point of the 1080 layout, as the drawing's clicks find it. False over nothing. For tests.</summary>
        public bool HitAt(Vector2 point, out HitKind kind, out int index)
        {
            LayOut();
            for (int i = 0; i < _hits.Count; i++)
                if (_hits[i].R.Contains(point))
                {
                    kind = _hits[i].Kind;
                    index = _hits[i].Index;
                    return true;
                }
            kind = HitKind.Row;
            index = -1;
            return false;
        }

        /// <summary>The rectangle of the first place of this kind and index in the 1080 layout (empty when there is none). For tests.</summary>
        public Rect HitRect(HitKind kind, int index)
        {
            LayOut();
            foreach (Hit h in _hits)
                if (h.Kind == kind && h.Index == index) return h.R;
            return Rect.zero;
        }

        /// <summary>A click at a point of the 1080 layout: what is under it is done (the drawing's buttons do the same). False over nothing.</summary>
        public bool ClickAt(Vector2 point) => Up && HitAt(point, out HitKind kind, out int index) && Act(kind, index);

        bool Act(HitKind kind, int index)
        {
            switch (kind)
            {
                case HitKind.Tab: return OpenPage(SettingsPages.All[index]);
                case HitKind.Page: return Pick(index);
                case HitKind.Row: return Pick(index);
                case HitKind.Less: return Select(index) && Change(-1);
                case HitKind.More: return Select(index) && Change(1);
            }
            return false;
        }

        /// <summary>The mouse over a line or a row moves the keys' place there (only when the mouse moved).</summary>
        void Hover(Vector2 point)
        {
            if (!HitAt(point, out HitKind kind, out int index)) return;
            if (kind == HitKind.Page || kind == HitKind.Row || kind == HitKind.Less || kind == HitKind.More) Selected = index;
        }

        /// <summary>The layout's work for a frame with the screen up (for the tests of a frame with nothing changing).</summary>
        public void LayOutForTests() => LayOut();

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (!Up) return;
            GUI.depth = L.Depth;
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 before);
            LayOut();
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseMove || e.type == EventType.Repaint) && e.mousePosition != _mouse)
            {
                bool first = float.IsNaN(_mouse.x);
                _mouse = e.mousePosition;
                if (!first) Hover(_mouse);
            }
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            // From the Esc menu the game shows through a veil; from the start menu its picture and veil are under this (StartMenuGui.cs).
            if (From == SettingsFrom.PauseMenu) InterfaceStyle.Fill(new Rect(0f, 0f, viewWidth, ViewHeight), InterfaceStyle.C(v.Black, L.GameVeil));
            DrawHeading(HeadRect(m, s), OnPage ? SettingsPages.Words[(int)Page] : L.Heading, v, m);
            if (OnPage)
            {
                DrawTabs(v, s);
                for (int i = 0; i < _lines.Count; i++) DrawRow(i, v);
            }
            else DrawList(v, s);
            DrawHint(HintRect(s), v, m);
            for (int i = 0; i < _hits.Count && Up; i++)
                if (GUI.Button(_hits[i].R, GUIContent.none, GUIStyle.none))
                {
                    Act(_hits[i].Kind, _hits[i].Index);
                    break;
                }
            GUI.matrix = before;
            GUI.color = colorBefore;
        }

        /// <summary>A heading on a red plate leaning as her health's does, as the pause menu's and the start menu's tabs' headings.</summary>
        static void DrawHeading(Rect h, string words, InterfaceValues v, MenuValues m)
        {
            float pivot = h.center.y;
            InterfaceStyle.InkedPlate(h, pivot, m.HeadingSlant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White));
            InterfaceStyle.Leaning(h, pivot, m.HeadingSlant, InterfaceStyle.C(v.Red));
            InterfaceStyle.DotsAlong(h, pivot, m.HeadingSlant, 1f - m.HeadingDots, InterfaceStyle.C(v.Black, 0.42f));
            Label(new Rect(h.x + HeadingPad(m), h.y, h.width - HeadingPad(m), h.height), words, InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Slashes(h.xMax + h.height * m.HeadingSlant * 0.5f + 46f, h.y + 10f, h.height - 20f, 9f, 2, InterfaceStyle.C(v.Red));
        }

        /// <summary>The list of pages, as the start menu's main list.</summary>
        void DrawList(InterfaceValues v, StartMenuValues s)
        {
            GUIStyle style = InterfaceStyle.Text(true, s.MainTextSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < SettingsPages.All.Length; i++)
            {
                Rect r = ListRect(i, s);
                string word = SettingsPages.Words[i];
                if (i == Selected)
                {
                    var plate = new Rect(r.x + s.MainShift, r.y + 6f, r.width, r.height - 12f);
                    float pivot = plate.center.y;
                    InterfaceStyle.InkedPlate(plate, pivot, v.Slant * 1.4f, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White));
                    InterfaceStyle.Leaning(plate, pivot, v.Slant * 1.4f, InterfaceStyle.C(v.Teal));
                    InterfaceStyle.DotsAlong(new Rect(plate.x, plate.y, plate.width * 0.22f, plate.height), pivot, v.Slant * 1.4f, 0f, InterfaceStyle.C(v.Black, 0.35f));
                    InterfaceStyle.Slashes(plate.xMax + 30f, plate.y + 8f, plate.height - 16f, 7f, 2, InterfaceStyle.C(v.Red));
                    Label(new Rect(plate.x + 40f, r.y, r.width, r.height), word, style, InterfaceStyle.C(v.Black), false);
                }
                else
                {
                    Label(new Rect(r.x + 12f, r.y, r.width, r.height), word, style, InterfaceStyle.C(v.White), true);
                    InterfaceStyle.Slanted(new Rect(r.x, r.yMax - s.MainStroke - 4f, r.width * 0.78f, s.MainStroke), InterfaceStyle.C(v.White, 0.85f));
                }
            }
        }

        /// <summary>The start menu's tabs, the open page's white with the teal stroke under it.</summary>
        void DrawTabs(InterfaceValues v, StartMenuValues s)
        {
            GUIStyle tab = InterfaceStyle.Text(true, s.TabTextSize, TextAnchor.MiddleCenter);
            for (int t = 0; t < SettingsPages.All.Length; t++)
            {
                Rect r = TabRect(t, s);
                bool here = t == (int)Page;
                InterfaceStyle.HeavyPlate(r, r.center.y, v.Slant, 2f, v.Depth);
                InterfaceStyle.Slanted(r, here ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black, 0.92f));
                Label(r, SettingsPages.Words[t], tab, here ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White, 0.9f), false);
                if (here) InterfaceStyle.Slanted(new Rect(r.x, r.yMax + 6f, r.width, 5f), InterfaceStyle.C(v.Teal));
            }
        }

        /// <summary>A row of the page, as the pause menu's rows in the concept's style: dark with a teal cut, the chosen one white.</summary>
        void DrawRow(int i, InterfaceValues v)
        {
            Rect r = RowRect(i, true);
            bool selected = i == Selected, greyed = _greyed[i];
            float pivot = r.center.y, slant = v.Slant, dim = greyed ? L.Greyed : 1f;
            if (selected)
            {
                InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.Teal));
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.White, greyed ? 0.7f : 1f));
                InterfaceStyle.Slashes(r.xMax + 34f, r.y + 6f, r.height - 12f, 6f, 2, InterfaceStyle.C(v.Red));
            }
            else
            {
                InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline * 0.75f, v.Depth, InterfaceStyle.C(v.White, 0.3f * dim), 0.6f);
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.86f));
                InterfaceStyle.Leaning(new Rect(r.x, r.y, 5f, r.height), pivot, slant, InterfaceStyle.C(v.Teal, dim));
            }
            Color ink = selected ? InterfaceStyle.C(v.Black, dim) : InterfaceStyle.C(v.White, dim);
            float pad = RowPad(v);
            GUIStyle name = NameStyle();
            Label(new Rect(r.x + pad, r.y, r.width - pad, r.height), _names[i], name, ink, !selected);
            if (_notes[i].Length > 0)
            {
                float after = r.x + pad + (i < _nameW.Count ? _nameW[i] : 0f) + 22f;
                Label(new Rect(after, r.y + 2f, r.xMax - L.ValueRoom - after, r.height), _notes[i], NoteStyle(), selected ? InterfaceStyle.C(v.Black, 0.6f) : InterfaceStyle.C(v.Grey), false);
            }
            string value = _values[i];
            Color valueInk = MenuWords.IsOn(value) ? (selected ? InterfaceStyle.C(v.Black, dim) : InterfaceStyle.C(v.Teal, dim))
                             : MenuWords.IsOff(value) ? (selected ? InterfaceStyle.C(v.Black, 0.45f * dim) : InterfaceStyle.C(v.Grey, dim))
                             : ink;
            float right = r.xMax - L.Chevron - 20f, w = ValueW(i);
            Label(new Rect(right - w - 4f, r.y, w + 4f, r.height), value, ValueStyle(), valueInk, !selected);
            GUIStyle chevron = InterfaceStyle.Text(true, L.ValueSize, TextAnchor.MiddleCenter);
            Color chevronInk = greyed ? InterfaceStyle.C(v.White, 0.15f) : selected ? InterfaceStyle.C(v.Red) : InterfaceStyle.C(v.White, 0.55f);
            Label(new Rect(right - w - 14f - L.Chevron, r.y, L.Chevron, r.height), LessWord, chevron, chevronInk, false);
            Label(new Rect(right + 4f, r.y, L.Chevron, r.height), MoreWord, chevron, chevronInk, false);
        }

        void DrawHint(Rect r, InterfaceValues v, MenuValues m)
        {
            float x = r.x, h = m.KeyCapHeight;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < _hint.Length && i < _hintW.Count; i++)
            {
                if (_hintCap[i])
                {
                    var cap = new Rect(x, r.y + (r.height - h) * 0.5f, _hintW[i], h);
                    InterfaceStyle.HeavyPlate(cap, cap.center.y, v.Slant, 2f, 0f);
                    InterfaceStyle.Slanted(cap, InterfaceStyle.C(v.White));
                    Label(cap, _hint[i], CapStyle(m), InterfaceStyle.C(v.Black), false);
                    x += _hintW[i] + 6f;
                }
                else
                {
                    Label(new Rect(x + 2f, r.y, _hintW[i] + 6f, r.height), _hint[i], words, InterfaceStyle.C(v.White, 0.8f), true);
                    x += _hintW[i] + 18f;
                }
            }
        }

        // ---------------- Words drawn with a content each (as the start menu's, work order 090) ----------------

        static readonly Dictionary<string, GUIContent> s_Words = new Dictionary<string, GUIContent>(System.StringComparer.Ordinal);

        static void Label(Rect r, string text, GUIStyle style, Color c, bool shadow)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (!s_Words.TryGetValue(text, out GUIContent content))
            {
                if (s_Words.Count >= 512) s_Words.Clear();
                content = new GUIContent(text);
                s_Words[text] = content;
            }
            InterfaceStyle.Label(r, content, style, c, shadow);
        }

        // ---------------- Measured ----------------

        /// <summary>
        /// What of the screen's words does not fit inside its shape at the screen now, as words; empty when everything fits (the
        /// order: "text cut off or overlapping, a value that does not fit"). For tests.
        /// </summary>
        public string TextOutside()
        {
            LayOut();
            var problems = new List<string>();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            void Fits(string what, float need, float room)
            {
                if (need > room) problems.Add(what + " (" + need.ToString("0") + " in " + room.ToString("0") + ")");
            }
            float hintTop = ViewHeight - s.HintBottom - s.HintHeight;
            Fits("the heading reaches the rows", HeadRect(m, s).yMax + 10f, OnPage ? L.RowsTop : s.MainTop);
            if (OnPage)
            {
                GUIStyle tab = InterfaceStyle.Text(true, s.TabTextSize, TextAnchor.MiddleCenter);
                foreach (string t in SettingsPages.Words) Fits("the tab " + t + " is wider than its plate", InterfaceStyle.Measure(t, tab).x + 20f, s.TabWidth);
                Fits("the tabs reach the heading", TabRect(0, s).yMax + 12f, L.HeadTop);
                GUIStyle name = NameStyle(), note = NoteStyle(), value = ValueStyle();
                float pad = RowPad(v);
                for (int i = 0; i < _lines.Count; i++)
                {
                    Vector2 n = InterfaceStyle.Measure(_names[i], name);
                    float noteW = _notes[i].Length > 0 ? 22f + InterfaceStyle.Measure(_notes[i], note).x : 0f;
                    Fits("the row " + _names[i] + "'s name and note reach its value", pad + n.x + noteW + 12f, L.RowsWidth - L.ValueRoom);
                    Fits("the row " + _names[i] + " is taller than its row", n.y, L.RowHeight * 1.05f);
                    Vector2 val = InterfaceStyle.Measure(_values[i], value);
                    Fits("the value " + _values[i] + " is wider than its room", val.x + 2f * L.Chevron + 50f, L.ValueRoom);
                    Fits("the value " + _values[i] + " is taller than its row", val.y, L.RowHeight * 1.1f);
                }
                if (_lines.Count > 0)
                {
                    Fits("the rows run into the keys' line", RowRect(_lines.Count - 1, false).yMax + 10f, hintTop);
                    Fits("the rows run past the view's right", L.RowsLeft + L.RowsWidth + L.SelectedShift + 60f, _viewWidth);
                }
            }
            else
            {
                GUIStyle style = InterfaceStyle.Text(true, s.MainTextSize, TextAnchor.MiddleLeft);
                foreach (string w in SettingsPages.Words)
                {
                    Vector2 size = InterfaceStyle.Measure(w, style);
                    Fits("the line " + w + " is wider than its plate", 40f + size.x + 30f, s.MainWidth);
                    Fits("the line " + w + " is taller than its row", size.y, s.MainRowHeight * 1.05f);
                }
                Fits("the lines run into the keys' line", ListRect(SettingsPages.All.Length - 1, s).yMax + 10f, hintTop);
            }
            GUIStyle heading = InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft);
            Fits("the heading is wider than its plate", HeadingPad(m) + InterfaceStyle.Measure(OnPage ? SettingsPages.Words[(int)Page] : L.Heading, heading).x + 16f, _headW);
            float hint = 0f;
            for (int i = 0; i < _hintW.Count; i++) hint += _hintW[i] + (_hintCap[i] ? 6f : 18f);
            Fits("the line of keys is wider than its place", hint, _viewWidth - 2f * s.EchoesMargin);
            return string.Join("; ", problems);
        }
    }
}
