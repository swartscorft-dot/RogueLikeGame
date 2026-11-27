using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The start menu drawn (work order 114), in decision 0011's kinetic style with <see cref="InterfaceStyle"/>'s own shapes,
    /// outlines, dots, slashes and type (no second style), laid out from the two mock-ups of 2026-11-24
    /// (<c>menu_main_mock.png</c>, <c>menu_character_select_mock.png</c>). Her picture covers the screen, never stretched,
    /// cropped about its named anchor (<see cref="CoverCrop"/>); without it a flat backdrop in the style stands in: black, a pale
    /// slab, one teal and one red stripe leaning hard, a small patch of dots.
    ///
    /// The main menu: the lines at the left in the heavy italic, each on a thin slanted stroke; the one the keys are on on a teal
    /// plate standing out, black words, the red double slash. The character: her name plate at the left (what the documents
    /// call her), the line under it and three short lines; at the foot a small low panel with a row of slots, hers first in a
    /// teal outline with a crop of her picture, the rest dark with a white question mark; BACK at the foot's left, RUN LOADOUT at
    /// its right. The tabs (START RUN, UPGRADES, STATS) at the top left of every screen but the main menu, his Echoes small at the
    /// top right of every screen, the keys at the foot. The loadout's, the upgrades' and the stats' panels are the HUD's
    /// (RunHudStartMenu.cs).
    ///
    /// Its layout is worked out when the screen, the screen's size or what the panels say changes, never in a frame that changes
    /// nothing; every click goes through one hit test (<see cref="ClickAt"/>), the drawing's and the tests' alike.
    /// </summary>
    public sealed partial class StartMenu
    {
        /// <summary>What a click on a place does: its kind and its index.</summary>
        public enum HitKind { Tab, MainLine, Slot, Back, Next, Item, StrikeLess, StrikeMore }

        struct Hit
        {
            public Rect R;
            public HitKind Kind;
            public int Index;
        }

        readonly List<Hit> _hits = new List<Hit>();
        bool _laidOut;
        Vector2Int _laidAt;
        MenuScreen _laidScreen;
        int _laidVersion = -1, _laidMain = -1;
        float _viewWidth;
        Vector2 _mouse = new Vector2(float.NaN, float.NaN);

        // Measured once a layout.
        readonly List<float> _hintW = new List<float>();
        string[] _hint = s_MainHint;
        bool[] _hintCap = s_MainHintCap;
        float _backCapW, _nextCapW;

        static readonly string[] s_MainHint = { "Up", "Down", "choose", "Enter", "pick", "Esc", "practice room" };
        static readonly bool[] s_MainHintCap = { true, true, false, true, false, true, false };
        static readonly string[] s_LoadoutHint = { "Up", "Down", "choose", "Enter", "put on, take off", "<", ">", "the skill struck out", "Space", "start", "Esc", "back" };
        static readonly bool[] s_LoadoutHintCap = { true, true, false, true, false, true, true, false, true, false, true, false };
        static readonly string[] s_UpgradesHint = { "Up", "Down", "choose", "Enter", "buy", "Esc", "back" };
        static readonly bool[] s_UpgradesHintCap = { true, true, false, true, false, true, false };
        static readonly string[] s_StatsHint = { "Esc", "back" };
        static readonly bool[] s_StatsHintCap = { true, false };
        static readonly string[] s_CharacterHint = { "<", ">", "choose" };
        static readonly bool[] s_CharacterHintCap = { true, true, false };

        public static readonly string[] TabWords = { "START RUN", "UPGRADES", "STATS" };
        public static readonly string[] MainWords = { "START RUN", "UPGRADES", "STATS", "QUIT", "SETTINGS", "REHEARSAL" }; // SETTINGS: work order 128; REHEARSAL: work order 148 ("Rehearsal: the boss")
        public const string BackWords = "BACK", NextWords = "RUN LOADOUT  >", EchoesWord = "ECHOES", TrialOffWord = "trial off", QuestionMark = "?";

        /// <summary>The view's width in the layout (1080 high).</summary>
        static float ViewWidth => InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);
        static float ViewHeight => InterfaceStyle.V.LayoutHeight;

        // ---------------- The layout ----------------

        void LayOut()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            RunHud hud = RunHud.Current;
            int version = hud != null ? hud.MenuVersion(this) : 0;
            if (_laidOut && _laidAt == at && _laidScreen == Screen && _laidVersion == version && _laidMain == _main.Count) return;
            _laidOut = true;
            _laidAt = at;
            _laidScreen = Screen;
            _laidVersion = version;
            _laidMain = _main.Count;
            _viewWidth = ViewWidth;
            StartMenuValues s = S;
            MenuValues m = InterfaceStyle.V.Menus;
            _hits.Clear();

            switch (Screen)
            {
                case MenuScreen.Main: _hint = s_MainHint; _hintCap = s_MainHintCap; break;
                case MenuScreen.Character: _hint = MenuHelpValues.On ? CharacterHintHelp : s_CharacterHint; _hintCap = MenuHelpValues.On ? s_CharacterHintHelpCap : s_CharacterHintCap; break; // the menus' help (StartMenuHelp.cs)
                case MenuScreen.Loadout: _hint = MenuHelpValues.On ? s_LoadoutHintHelp : s_LoadoutHint; _hintCap = MenuHelpValues.On ? s_LoadoutHintHelpCap : s_LoadoutHintCap; break;
                case MenuScreen.Upgrades: _hint = UpgradesHint; _hintCap = UpgradesHintCap; break; // work order 139: the page's keys with the tabs on
                default: _hint = s_StatsHint; _hintCap = s_StatsHintCap; break;
            }
            GUIStyle cap = CapStyle(m), words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            _hintW.Clear();
            for (int i = 0; i < _hint.Length; i++)
                _hintW.Add(_hintCap[i] ? Mathf.Max(m.KeyCapHeight * 1.2f, InterfaceStyle.Measure(_hint[i], cap).x + 16f) : InterfaceStyle.Measure(_hint[i], words).x);
            _backCapW = Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure("Esc", cap).x + 18f);
            _nextCapW = Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure("Enter", cap).x + 18f);

            if (Screen != MenuScreen.Main)
                for (int t = 0; t < TabsNow.Length; t++) _hits.Add(new Hit { R = TabRect(t, s), Kind = HitKind.Tab, Index = t }); // work order 139: five tabs with its switch
            if (Screen == MenuScreen.Main)
                for (int i = 0; i < _main.Count; i++) _hits.Add(new Hit { R = MainRowRect(i, s), Kind = HitKind.MainLine, Index = i });
            else if (Screen == MenuScreen.Character)
            {
                for (int i = 0; i < s.Slots; i++) _hits.Add(new Hit { R = SlotRect(i, s), Kind = HitKind.Slot, Index = i });
                _hits.Add(new Hit { R = BackRect(s), Kind = HitKind.Back });
                _hits.Add(new Hit { R = NextRect(s), Kind = HitKind.Next });
                if (MenuHelpValues.On) _hits.Add(new Hit { R = NamePlateHit(s), Kind = HitKind.Next }); // the menus' help: a click on her name plate goes on as Enter does
            }
            else if (hud != null)
            {
                hud.LayOutMenuScreen(this, _viewWidth);
                for (int i = 0; i < hud.MenuItemRects.Count; i++) _hits.Add(new Hit { R = hud.MenuItemRects[i], Kind = HitKind.Item, Index = i });
                if (hud.MenuStrikeLess.width > 0f)
                {
                    _hits.Insert(0, new Hit { R = hud.MenuStrikeMore, Kind = HitKind.StrikeMore, Index = hud.MenuStrikeItem });
                    _hits.Insert(0, new Hit { R = hud.MenuStrikeLess, Kind = HitKind.StrikeLess, Index = hud.MenuStrikeItem });
                }
            }
        }

        Rect TabRect(int t, StartMenuValues s) => new Rect(s.TabLeft + t * (s.TabWidth + s.TabGap), s.TabTop, s.TabWidth, s.TabHeight);
        Rect MainRowRect(int i, StartMenuValues s) => new Rect(s.MainLeft, s.MainTop + i * (s.MainRowHeight + s.MainRowGap), s.MainWidth, s.MainRowHeight);
        Rect EchoesRect(StartMenuValues s) => new Rect(_viewWidth - s.EchoesMargin - s.EchoesWidth, s.TabTop, s.EchoesWidth, s.EchoesHeight);

        Rect SlotsPanelRect(StartMenuValues s)
        {
            float w = s.Slots * s.SlotSize + (s.Slots - 1) * s.SlotGap + 2f * s.SlotsPad;
            float h = s.SlotSize + 2f * s.SlotsPad;
            return new Rect((_viewWidth - w) * 0.5f, ViewHeight - s.SlotsBottom - h, w, h);
        }

        Rect SlotRect(int i, StartMenuValues s)
        {
            Rect p = SlotsPanelRect(s);
            return new Rect(p.x + s.SlotsPad + i * (s.SlotSize + s.SlotGap), p.y + s.SlotsPad, s.SlotSize, s.SlotSize);
        }

        Rect BackRect(StartMenuValues s) => new Rect(s.EchoesMargin, ViewHeight - s.SlotsBottom - s.FootButtonHeight, s.FootButtonWidth, s.FootButtonHeight);
        Rect NextRect(StartMenuValues s) => new Rect(_viewWidth - s.EchoesMargin - s.NextWidth, ViewHeight - s.SlotsBottom - s.FootButtonHeight, s.NextWidth, s.FootButtonHeight);
        Rect HintRect(StartMenuValues s) => new Rect(s.EchoesMargin, ViewHeight - s.HintBottom - s.HintHeight, _viewWidth * 0.62f, s.HintHeight);

        static GUIStyle CapStyle(MenuValues m) => InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter);

        // ---------------- The hit test ----------------

        /// <summary>The place under a point of the 1080 layout, as the drawing's clicks find it: its kind and index; false over nothing. For tests.</summary>
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
            kind = HitKind.Tab;
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
        public bool ClickAt(Vector2 point)
        {
            if (!Up || SettingsScreen.Holds || !HitAt(point, out HitKind kind, out int index)) return false; // work order 128: not under the settings screen
            return Act(kind, index);
        }

        bool Act(HitKind kind, int index)
        {
            switch (kind)
            {
                case HitKind.Tab: return OpenTab(index);
                case HitKind.MainLine: return index < _main.Count && PickMain(_main[index]);
                case HitKind.Slot: return ChooseSlot(index);
                case HitKind.Back: return Escape();
                case HitKind.Next: return GoOn(); // the menus' help (StartMenuHelp.cs): on with her wherever the cursor stands
                case HitKind.Item: return UseItem(index);
                case HitKind.StrikeLess:
                    Selected = index;
                    return ChangeStruckOut(-1);
                case HitKind.StrikeMore:
                    Selected = index;
                    return ChangeStruckOut(1);
            }
            return false;
        }

        /// <summary>The mouse over a line, a slot or an item moves the keys' place there (only when the mouse moved).</summary>
        void Hover(Vector2 point)
        {
            if (!HitAt(point, out HitKind kind, out int index)) return;
            if ((kind == HitKind.MainLine && Screen == MenuScreen.Main) || (kind == HitKind.Slot && Screen == MenuScreen.Character) || kind == HitKind.Item) Selected = index;
        }

        // ---------------- Drawing ----------------

        void OnGUI()
        {
            if (!Up) return;
            GUI.depth = S.Depth;
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 before);
            LayOut();
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseMove || e.type == EventType.Repaint) && e.mousePosition != _mouse)
            {
                bool first = float.IsNaN(_mouse.x);
                _mouse = e.mousePosition;
                if (!first && !SettingsScreen.Holds) Hover(_mouse); // work order 128: not under the settings screen
            }
            TabsMouse(e); // work order 139: the model dragged and wheeled on the WEAPON page
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            DrawBackdrop(viewWidth, v, s);
            if (SettingsScreen.Holds)
            {
                // Work order 128: the settings screen over the menu draws itself; her picture and its veil stay under it.
                GUI.matrix = before;
                GUI.color = colorBefore;
                return;
            }
            switch (Screen)
            {
                case MenuScreen.Main: DrawMain(v, m, s); break;
                case MenuScreen.Character: DrawCharacter(v, m, s); break;
                default: RunHud.Current?.DrawMenuScreen(this, v, m); break;
            }
            DrawFrame(v, m, s);
            // The clicks: one button a place, through the one hit test (the screen may change under a click: then nothing more this event).
            for (int i = 0; i < _hits.Count && Up; i++)
                if (GUI.Button(_hits[i].R, GUIContent.none, GUIStyle.none))
                {
                    Act(_hits[i].Kind, _hits[i].Index);
                    break;
                }
            GUI.matrix = before;
            GUI.color = colorBefore;
        }

        void DrawBackdrop(float w, InterfaceValues v, StartMenuValues s)
        {
            float h = ViewHeight;
            Texture2D picture = Picture;
            bool bare = (Screen == MenuScreen.Main || Screen == MenuScreen.Character) && !SettingsScreen.Holds; // work order 128: veiled under the settings screen
            if (picture != null)
            {
                InterfaceStyle.Fill(new Rect(0f, 0f, w, h), InterfaceStyle.C(v.Black));
                UvRect uv = CoverCrop.Of(picture.width, picture.height, w, h, s.PictureAnchorX, s.PictureAnchorY);
                GUI.color = Color.white;
                GUI.DrawTextureWithTexCoords(new Rect(0f, 0f, w, h), picture, new Rect(uv.X, uv.Y, uv.W, uv.H));
            }
            else FlatBackdrop(w, h, v);
            if (bare)
            {
                // The calm dark at the left the words stand on, fading out to the right (one texture made in code).
                GUI.color = InterfaceStyle.C(v.Black, s.LeftShade * (picture != null ? 1f : 0.5f));
                GUI.DrawTexture(new Rect(0f, 0f, s.LeftShadeWidth, h), Shade, ScaleMode.StretchToFill);
            }
            else InterfaceStyle.Fill(new Rect(0f, 0f, w, h), InterfaceStyle.C(v.Black, s.PanelVeil));
        }

        /// <summary>
        /// The backdrop under the settings screen opened from the Esc menu (the follow-up to work order 128: nothing of the run's
        /// HUD may read through it): her picture, or the flat backdrop without it, under the panels' veil, opaque, as the start menu's.
        /// </summary>
        public void DrawSettingsBackdrop(float w)
        {
            LoadPicture();
            DrawBackdrop(w, InterfaceStyle.V, S);
        }

        Texture2D _shade;

        /// <summary>A ramp from opaque at its left to clear at its right, eased (made once in code, not an asset).</summary>
        Texture2D Shade
        {
            get
            {
                if (_shade != null) return _shade;
                const int Width = 128;
                _shade = new Texture2D(Width, 1, TextureFormat.RGBA32, false) { name = "StartMenuShade", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[Width];
                for (int x = 0; x < Width; x++)
                {
                    float t = x / (Width - 1f), a = 1f - t * t * (3f - 2f * t);
                    px[x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
                _shade.SetPixels32(px);
                _shade.Apply();
                return _shade;
            }
        }

        /// <summary>The flat backdrop when her picture is missing: black, a pale slab and one teal and one red stripe leaning hard, a patch of dots; restrained.</summary>
        internal static void FlatBackdrop(float w, float h, InterfaceValues v)
        {
            float pivot = h * 0.5f;
            InterfaceStyle.Fill(new Rect(0f, 0f, w, h), InterfaceStyle.C(v.Black));
            InterfaceStyle.Leaning(new Rect(w * 0.56f, 0f, w * 0.7f, h * 0.84f), pivot, 0.62f, InterfaceStyle.C(v.White, 0.07f));
            InterfaceStyle.Leaning(new Rect(w * 0.64f, 0f, 22f, h * 0.84f), pivot, 0.62f, InterfaceStyle.C(v.Teal, 0.9f));
            InterfaceStyle.Leaning(new Rect(w * 0.71f, h * 0.2f, 10f, h * 0.64f), pivot, 0.62f, InterfaceStyle.C(v.Red, 0.85f));
            InterfaceStyle.DotsAlong(new Rect(w * 0.74f, h * 0.08f, w * 0.18f, h * 0.3f), pivot, 0.62f, 0.2f, InterfaceStyle.C(v.White, 0.16f));
            InterfaceStyle.Fill(new Rect(0f, h * 0.84f, w, h * 0.16f), InterfaceStyle.C(v.Black));
        }

        void DrawMain(InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            GUIStyle style = InterfaceStyle.Text(true, s.MainTextSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < _main.Count; i++)
            {
                Rect r = MainRowRect(i, s);
                bool on = i == Selected;
                string word = MainWords[(int)_main[i]];
                if (on)
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

        void DrawCharacter(InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            // Her name plate, running off the left edge as the mock-up's: black in the heavy outline with a white stroke, dots toward its right end.
            var plate = new Rect(s.PlateLeft, s.PlateTop, s.PlateWidth, s.PlateHeight);
            float pivot = plate.center.y, slant = m.HeadingSlant * 0.5f, pad = NamePad(s);
            InterfaceStyle.InkedPlate(plate, pivot, slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White));
            InterfaceStyle.Leaning(plate, pivot, slant, InterfaceStyle.C(v.Black, 0.94f));
            InterfaceStyle.DotsAlong(new Rect(plate.x + plate.width * 0.6f, plate.y, plate.width * 0.4f, plate.height), pivot, slant, 0.1f, InterfaceStyle.C(v.Teal, 0.22f));
            Label(new Rect(plate.x + pad, plate.y - 4f, plate.width - pad, plate.height - 18f), s.Name, InterfaceStyle.Text(true, s.NameSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Slanted(new Rect(plate.x + pad, plate.yMax - 24f, plate.width * 0.55f, 7f), InterfaceStyle.C(v.Teal));
            InterfaceStyle.Slashes(plate.xMax + plate.height * slant * 0.5f + 20f, plate.y + 30f, plate.height - 60f, 10f, 2, InterfaceStyle.C(v.Red));
            float y = plate.yMax + 22f;
            Label(new Rect(pad + plate.x, y, s.LinesWidth, s.SubSize * 1.5f), s.Sub, InterfaceStyle.Text(true, s.SubSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White), true);
            y += s.SubSize * 1.5f + 14f;
            GUIStyle line = InterfaceStyle.Text(false, s.LineSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < s.Lines.Length; i++)
            {
                InterfaceStyle.Slanted(new Rect(pad + plate.x, y + s.LineHeight * 0.5f - 2f, 14f, 4f), InterfaceStyle.C(v.Teal));
                Label(new Rect(pad + plate.x + 26f, y, s.LinesWidth, s.LineHeight), s.Lines[i], line, InterfaceStyle.C(v.White, 0.88f), true);
                y += s.LineHeight;
            }

            // The small low panel: the row of slots.
            Rect panel = SlotsPanelRect(s);
            InterfaceStyle.InkedPlate(panel, panel.center.y, v.Slant * 0.5f, v.HeavyOutline, v.Depth, InterfaceStyle.C(v.White, 0.35f), 0.6f);
            InterfaceStyle.Leaning(panel, panel.center.y, v.Slant * 0.5f, InterfaceStyle.C(v.Black, 0.88f));
            GUIStyle question = InterfaceStyle.Text(true, s.QuestionSize, TextAnchor.MiddleCenter);
            Texture2D picture = Picture;
            for (int i = 0; i < s.Slots; i++)
            {
                Rect r = SlotRect(i, s);
                bool hers = i == StartMenuFlow.HerSlot, cursor = i == Selected;
                if (hers)
                {
                    if (picture != null)
                    {
                        UvRect uv = CoverCrop.Portrait(picture.width, picture.height, r.width, r.height, s.PortraitX, s.PortraitY, s.PortraitShare);
                        GUI.color = Color.white;
                        GUI.DrawTextureWithTexCoords(r, picture, new Rect(uv.X, uv.Y, uv.W, uv.H));
                    }
                    else
                    {
                        InterfaceStyle.Fill(r, InterfaceStyle.C(v.White));
                        Label(r, s.Name, InterfaceStyle.Text(true, 19, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black), false);
                    }
                    InterfaceStyle.LeaningFrame(new Rect(r.x - 4f, r.y - 4f, r.width + 8f, r.height + 8f), r.center.y, 0f, InterfaceStyle.C(v.Teal), cursor ? 6f : 4f);
                    if (MenuHelpValues.On) DrawChosen(r, v); // the menus' help: she is the one chosen
                }
                else
                {
                    InterfaceStyle.Fill(r, InterfaceStyle.C(v.Black));
                    InterfaceStyle.LeaningFrame(r, r.center.y, 0f, InterfaceStyle.C(v.White, cursor ? 0.9f : 0.3f), cursor ? 4f : 2f);
                    Label(r, QuestionMark, question, InterfaceStyle.C(v.White, cursor ? 1f : 0.8f), false);
                }
                if (cursor) InterfaceStyle.Slashes(r.xMax + 2f, r.y - 26f, 20f, 5f, 2, InterfaceStyle.C(v.Red));
            }
            if (Note.Length > 0 && MenuHelpValues.On) DrawRefusedSlot(panel, v); // the menus' help: a plain line on a plate over the row
            else if (Note.Length > 0) InterfaceStyle.Line(panel.center.x, panel.y - 96f, Note);
            DrawHint(new Rect(panel.x + 6f, panel.y - 38f, panel.width, 30f), v, m);

            // BACK and the step on.
            FootButton(BackRect(s), "Esc", _backCapW, BackWords, false, v, m, s);
            FootButton(NextRect(s), "Enter", _nextCapW, NextWords, true, v, m, s);
        }

        static float NamePad(StartMenuValues s) => -s.PlateLeft + 64f;

        void FootButton(Rect r, string key, float capW, string words, bool hot, InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            float pivot = r.center.y;
            InterfaceStyle.InkedPlate(r, pivot, v.Slant, v.HeavyOutline, v.Depth * 2f, hot ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.White, 0.6f), hot ? 1f : 0.7f);
            InterfaceStyle.Leaning(r, pivot, v.Slant, hot ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Black, 0.9f));
            float x = r.x + FootPad(v, s);
            KeyCap(new Rect(x, r.y + (r.height - m.KeyCapHeight) * 0.5f, capW, m.KeyCapHeight), key, m, v, hot);
            Label(new Rect(x + capW + 12f, r.y, r.width, r.height), words, InterfaceStyle.Text(true, s.FootTextSize, TextAnchor.MiddleLeft), hot ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White), !hot);
            if (!hot) InterfaceStyle.Slashes(r.xMax - 10f, r.y + 10f, r.height - 20f, 6f, 2, InterfaceStyle.C(v.Red));
        }

        static float FootPad(InterfaceValues v, StartMenuValues s) => v.Slant * s.FootButtonHeight * 0.5f + 14f;

        /// <summary>A key cap: white with black letters (black with white on a hot plate), the menus' own.</summary>
        static void KeyCap(Rect cap, string key, MenuValues m, InterfaceValues v, bool inverse = false)
        {
            InterfaceStyle.HeavyPlate(cap, cap.center.y, v.Slant, 2f, 0f);
            InterfaceStyle.Slanted(cap, inverse ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White));
            Label(cap, key, CapStyle(m), inverse ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black), false);
        }

        void DrawFrame(InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            if (Screen != MenuScreen.Main)
            {
                int on = TabHere; // work order 139: five tabs with its switch
                string[] tabs = TabsNow;
                GUIStyle tab = InterfaceStyle.Text(true, s.TabTextSize, TextAnchor.MiddleCenter);
                for (int t = 0; t < tabs.Length; t++)
                {
                    Rect r = TabRect(t, s);
                    bool here = t == on;
                    InterfaceStyle.HeavyPlate(r, r.center.y, v.Slant, 2f, v.Depth);
                    InterfaceStyle.Slanted(r, here ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black, 0.92f));
                    Label(r, tabs[t], tab, here ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.White, 0.9f), false);
                    if (here) InterfaceStyle.Slanted(new Rect(r.x, r.yMax + 6f, r.width, 5f), InterfaceStyle.C(v.Teal));
                }
            }
            // His Echoes, small.
            Rect e = EchoesRect(s);
            float echoesFlash = 0f;
            if (RunHud.Current != null) e.x += RunHud.Current.EchoesShakeNow(out echoesFlash); // the menus' help: the Echoes flash red and shake at a refused try
            InterfaceStyle.InkedPlate(e, e.center.y, v.Slant, v.HeavyOutline * 0.7f, v.Depth, Color.Lerp(InterfaceStyle.C(v.White, 0.5f), InterfaceStyle.C(v.Red), echoesFlash), 0.6f);
            InterfaceStyle.Leaning(e, e.center.y, v.Slant, InterfaceStyle.C(v.Black, 0.94f));
            InterfaceStyle.PipRow(new Rect(e.x + 22f, e.center.y - 9f, 18f, 18f), 1, InterfaceStyle.C(v.Gold));
            bool trial = EchoesKnown(out int echoes);
            Label(new Rect(e.x + 48f, e.y, e.width, e.height), trial ? EchoesWord : EchoesWord + "  " + TrialOffWord, InterfaceStyle.Text(false, s.EchoesLabelSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Grey), false);
            if (trial) Label(new Rect(e.x, e.y, e.width - 22f, e.height), InterfaceStyle.Number(echoes), InterfaceStyle.Text(true, s.EchoesTextSize, TextAnchor.MiddleRight), InterfaceStyle.C(v.White), false);
            InterfaceStyle.Slashes(e.x - 6f, e.y + 10f, e.height - 20f, 5f, 2, InterfaceStyle.C(v.Red));
            if (Screen != MenuScreen.Character) DrawHint(HintRect(s), v, m);
        }

        /// <summary>His Echoes, with the between-runs trial on (its save is read only then); false with it off.</summary>
        bool EchoesKnown(out int echoes)
        {
            GeneratedLevels levels = Levels;
            echoes = 0;
            if (levels == null || !levels.MetaOn) return false;
            echoes = levels.BetweenRunsState.Currency;
            return true;
        }

        void DrawHint(Rect r, InterfaceValues v, MenuValues m)
        {
            float x = r.x, h = m.KeyCapHeight;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < _hint.Length && i < _hintW.Count; i++)
            {
                if (_hintCap[i])
                {
                    KeyCap(new Rect(x, r.y + (r.height - h) * 0.5f, _hintW[i], h), _hint[i], m, v);
                    x += _hintW[i] + 6f;
                }
                else
                {
                    Label(new Rect(x + 2f, r.y, _hintW[i] + 6f, r.height), _hint[i], words, InterfaceStyle.C(v.White, 0.8f), true);
                    x += _hintW[i] + 18f;
                }
            }
        }

        // ---------------- Words drawn with a content each (as the HUD's menus, work order 090) ----------------

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
        /// order: "every text measured inside its rectangle at 1920 by 1080 and 1280 by 720"). For tests.
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
            if (Screen == MenuScreen.Main)
            {
                GUIStyle style = InterfaceStyle.Text(true, s.MainTextSize, TextAnchor.MiddleLeft);
                foreach (MainLine l in _main)
                {
                    Vector2 size = InterfaceStyle.Measure(MainWords[(int)l], style);
                    Fits("the line " + MainWords[(int)l] + " is wider than its plate", 40f + size.x + 30f, s.MainWidth);
                    Fits("the line " + MainWords[(int)l] + " is taller than its row", size.y, s.MainRowHeight * 1.05f);
                }
                Fits("the lines run past the view's foot", MainRowRect(_main.Count - 1, s).yMax, ViewHeight - s.HintBottom - s.HintHeight - 10f);
            }
            else
            {
                GUIStyle tab = InterfaceStyle.Text(true, s.TabTextSize, TextAnchor.MiddleCenter);
                foreach (string t in TabsNow) Fits("the tab " + t + " is wider than its plate", InterfaceStyle.Measure(t, tab).x + 20f, s.TabWidth); // work order 139: five with its switch
                Fits("the tabs reach the Echoes", TabRect(TabsNow.Length - 1, s).xMax + 20f, EchoesRect(s).x);
            }
            if (Screen == MenuScreen.Character)
            {
                float pad = NamePad(s), room = s.PlateWidth + s.PlateLeft - 64f;
                Vector2 name = InterfaceStyle.Measure(s.Name, InterfaceStyle.Text(true, s.NameSize, TextAnchor.MiddleLeft));
                Fits("her name is wider than its plate", name.x, room);
                Fits("her name is taller than its plate", name.y, s.PlateHeight * 1.15f);
                Fits("the line under her name is wider than its place", InterfaceStyle.Measure(s.Sub, InterfaceStyle.Text(true, s.SubSize, TextAnchor.MiddleLeft)).x, s.LinesWidth);
                GUIStyle line = InterfaceStyle.Text(false, s.LineSize, TextAnchor.MiddleLeft);
                foreach (string l in s.Lines) Fits("the line \"" + l + "\" is wider than its place", InterfaceStyle.Measure(l, line).x + 26f, s.LinesWidth);
                Fits("the question mark is taller than its slot", InterfaceStyle.Measure(QuestionMark, InterfaceStyle.Text(true, s.QuestionSize, TextAnchor.MiddleCenter)).y, s.SlotSize);
                GUIStyle foot = InterfaceStyle.Text(true, s.FootTextSize, TextAnchor.MiddleLeft);
                Fits("BACK is wider than its button", FootPad(v, s) + _backCapW + 12f + InterfaceStyle.Measure(BackWords, foot).x + 30f, s.FootButtonWidth);
                Fits("RUN LOADOUT is wider than its button", FootPad(v, s) + _nextCapW + 12f + InterfaceStyle.Measure(NextWords, foot).x + 16f, s.NextWidth);
                Fits("the slots' panel reaches BACK", BackRect(s).xMax + 10f, SlotsPanelRect(s).x);
                Fits("the slots' panel reaches RUN LOADOUT", SlotsPanelRect(s).xMax + 10f, NextRect(s).x);
                Fits("her lines reach the slots", s.PlateTop + s.PlateHeight + 36f + s.SubSize * 1.5f + s.Lines.Length * s.LineHeight, SlotsPanelRect(s).y - 70f);
            }
            GUIStyle echo = InterfaceStyle.Text(true, s.EchoesTextSize, TextAnchor.MiddleRight);
            Fits("the Echoes' words are wider than their plate", 48f + InterfaceStyle.Measure(EchoesWord + "  " + TrialOffWord, InterfaceStyle.Text(false, s.EchoesLabelSize, TextAnchor.MiddleLeft)).x + 10f, s.EchoesWidth);
            Fits("the Echoes' number is wider than their plate", 48f + InterfaceStyle.Measure(EchoesWord, InterfaceStyle.Text(false, s.EchoesLabelSize, TextAnchor.MiddleLeft)).x + 12f + InterfaceStyle.Measure("99999", echo).x + 22f, s.EchoesWidth);
            float hint = 0f;
            for (int i = 0; i < _hintW.Count; i++) hint += _hintW[i] + (_hintCap[i] ? 6f : 18f);
            Fits("the line of keys is wider than its place", hint, Screen == MenuScreen.Character ? SlotsPanelRect(s).width : _viewWidth - 2f * s.EchoesMargin - (Screen == MenuScreen.Loadout ? s.StartWidth + 30f : 0f));
            HelpTextOutside(problems); // the menus' help
            string panels = RunHud.Current != null && Screen != MenuScreen.Main && Screen != MenuScreen.Character ? RunHud.Current.MenuTextOutside(this) : "";
            if (panels.Length > 0) problems.Add(panels);
            return string.Join("; ", problems);
        }

        /// <summary>The layout's work for a frame with the menu up (for the tests of a frame with nothing changing: it allocates nothing).</summary>
        public void LayOutForTests() => LayOut();
    }
}
