using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The start menu's loadout, upgrades and stats (work order 114), as part of <see cref="RunHud"/>: the screen between runs'
    /// own cards and rules REUSED, laid out as these screens (the order: "call them; do not copy or rewrite them"). The cards are
    /// the ones work order 078 made of the screen's lines (<c>LayOutBetween</c>, drawn by <c>DrawCard</c>), and a card's press is
    /// that screen's (<c>ClickBetweenCard</c>: a piece equipped or taken off or bought, a part fitted or taken off or bought, a rank
    /// bought); the screen between runs is up under the menu while these show (<see cref="StartMenu"/> opens it, with the trial on).
    ///
    /// The loadout ("what she takes into this run, from what he owns"): the rack's pieces she owns, in a panel whose words say
    /// the budget's points used, and her pistols' parts she owns, a panel with each slot's heading; START at the foot's right.
    /// The upgrades: the ranks, every piece of the rack and every part, bought with Echoes; an owned one refuses here. With a
    /// trial off its part is a quiet locked line that says how to turn it on (the pause menu's lines). The stats: her sheet as it
    /// would be at a run's start with the present loadout (<see cref="HerSheet.Stats"/>, drawn by the sheet's own
    /// <c>StatGroups</c>), and his records (<see cref="RunRecords"/>).
    ///
    /// The end screen's first answer and the practice room's prompt say "main menu" while the menu is installed
    /// (<see cref="EndNewRunButton"/>, <see cref="StartPromptNow"/>); without it, as before.
    /// </summary>
    public sealed partial class RunHud
    {
        static readonly CultureInfo s_Inv114 = CultureInfo.InvariantCulture;
        static readonly string s_MenuPrompt = "Enter  main menu";

        /// <summary>The end screen's first answer: Enter goes to the main menu while the start menu is installed (work order 114), else a new run as before.</summary>
        public static string EndNewRunButton => StartMenu.Current != null ? "Enter  main menu" : "Enter  new run";

        /// <summary>The practice room's prompt: Enter brings the main menu back while the start menu is installed (work order 114), else F5's as before.</summary>
        string StartPromptNow => StartMenu.Current != null ? s_MenuPrompt : s_StartPrompt;

        const string RackHead = "RACK", PartsHead = "PISTOLS", PartsHeadShop = "PISTOLS", RanksHeadShop = "RANKS";
        const string PartsSub = "one part a slot", RanksSubShop = "one press, no undo", RackSubShop = "pieces to own", PartsSubShop = "parts to own";
        const string SheetHead = "HER SHEET", SheetSub = "a run's start, dressed as now", RecordsHead = "RECORDS", RecordsSub = "kept from 2026-11-24";
        const string NothingOwned = "Nothing owned yet: buy on UPGRADES.", StartWord = "START", LockedWord = "LOCKED";
        public const string LockedTrial1 = "The rack, the ranks and her pistols' parts: a trial, switched off.";
        public const string LockedTrial2 = "Turn it on in the pause menu (Esc in the practice room): \"Between runs (trial)\".";
        public const string LockedParts1 = "Her pistols' parts: a trial, switched off.";
        public const string LockedParts2 = "Turn it on in the pause menu (Esc in the practice room): \"Weapon attachments (trial)\".";
        const string LoadoutWord = "LOADOUT", UpgradesWord = "UPGRADES", StatsWord = "STATS";

        // ---------------- The items: which cards each screen shows ----------------

        struct MenuSection
        {
            public string Head, Sub;
            public int First, Count, Columns, Heads;
            public UiInk Stroke;
            public float X, W, H; // before the fit
        }

        readonly List<int> _menuCards = new List<int>();          // the cards shown, as items, in the panels' order
        readonly List<float> _menuCardHead = new List<float>();   // a slot heading's room above the card (one column only), else 0
        readonly List<string> _menuCardSlot = new List<string>(); // the slot heading's words, or null
        readonly List<MenuSection> _menuSections = new List<MenuSection>();
        readonly List<string> _menuLocked = new List<string>();
        readonly List<Rect> _menuItemRects = new List<Rect>();    // fitted, in the 1080 layout: the cards then START
        readonly List<Rect> _menuCardRaw = new List<Rect>();      // before the fit
        int _menuItemsFor = int.MinValue;
        MenuScreen _menuItemsScreen;
        float _mFit = 1f, _mTop, _menuX0;
        Rect _menuStart;
        string _menuBudget = "";

        /// <summary>The loadout's and the upgrades' items as drawn, in the 1080 layout (after the fit): the cards, then the loadout's START.</summary>
        public IReadOnlyList<Rect> MenuItemRects => _menuItemRects;
        /// <summary>Not That One's arrows on its card in the loadout (empty when not shown), and its item.</summary>
        public Rect MenuStrikeLess { get; private set; }
        public Rect MenuStrikeMore { get; private set; }
        public int MenuStrikeItem { get; private set; } = -1;
        /// <summary>The cards shown on the loadout or the upgrades, as items: their names (the screen between runs' cards). For tests.</summary>
        public IReadOnlyList<int> MenuCards => _menuCards;
        public IReadOnlyList<string> MenuLockedLines => _menuLocked;
        /// <summary>The fit the panels are drawn at (1 when they fit).</summary>
        public float MenuFit => _mFit;

        /// <summary>What the menu's panels are made from, as one number: the screen between runs' lines, the switches, the stats made, the screen.</summary>
        public int MenuVersion(StartMenu menu)
        {
            unchecked
            {
                int h = (int)menu.Screen;
                h = h * 31 + BetweenRunsLinesMade;
                h = h * 31 + WeaponListsMade;
                h = h * 31 + _menuStatsMade;
                GeneratedLevels levels = _levels != null ? _levels : GeneratedLevels.Current;
                h = h * 31 + (levels != null && levels.MetaOn ? 1 : 0) + (levels != null && levels.AttachmentsOn ? 2 : 0) + (levels != null && levels.BetweenRunsUp ? 4 : 0);
                h = h * 31 + TabsVersion(menu); // work order 139: the upgrades' page
                return h;
            }
        }

        void EnsureMenuItems(StartMenu menu)
        {
            Find();
            int version = MenuVersion(menu);
            if (version == _menuItemsFor && menu.Screen == _menuItemsScreen) return;
            _menuItemsFor = version;
            _menuItemsScreen = menu.Screen;
            _menuCards.Clear();
            _menuCardHead.Clear();
            _menuCardSlot.Clear();
            _menuSections.Clear();
            _menuLocked.Clear();
            GeneratedLevels levels = _levels;
            if (levels == null || (menu.Screen != MenuScreen.Loadout && menu.Screen != MenuScreen.Upgrades)) return;
            if (!levels.MetaOn)
            {
                _menuLocked.Add(LockedTrial1);
                _menuLocked.Add(LockedTrial2);
                return;
            }
            if (!levels.BetweenRunsUp || _betweenLines.Count < 4) return; // its lines come with the HUD's next Tick
            LayOutBetween();
            MetaState s = levels.BetweenRunsState;
            MetaValues mv = levels.MetaValues;
            bool parts = levels.AttachmentsOn;
            List<WeaponAttachment> inSlots = parts ? WeaponRack.InSlotOrder(mv) : null;
            bool loadout = menu.Screen == MenuScreen.Loadout;
            if (!loadout) AddSection(RanksHeadShop, RanksSubShop, UiInk.Gold, _weaponEnd, _cardName.Count, null, false);
            int budget = MetaRack.Budget(s, mv), used = MetaRack.PointsUsed(s, mv);
            _menuBudget = used.ToString(s_Inv114) + " of " + budget.ToString(s_Inv114) + " points";
            AddSection(RackHead, loadout ? _menuBudget : RackSubShop, UiInk.Teal, 0, _rackCards, null, loadout);
            if (parts) AddSection(loadout ? PartsHead : PartsHeadShop, loadout ? PartsSub : PartsSubShop, UiInk.White, _rackCards, _weaponEnd, inSlots, loadout);
            else
            {
                _menuLocked.Add(LockedParts1);
                _menuLocked.Add(LockedParts2);
            }
        }

        void AddSection(string head, string sub, UiInk stroke, int from, int to, List<WeaponAttachment> inSlots, bool ownedOnly)
        {
            var sec = new MenuSection { Head = head, Sub = sub, Stroke = stroke, First = _menuCards.Count };
            for (int i = from; i < to && i < _cardStateKind.Count; i++)
            {
                if (ownedOnly && _cardStateKind[i] != 0 && _cardStateKind[i] != 1) continue;
                _menuCards.Add(i);
            }
            sec.Count = _menuCards.Count - sec.First;
            sec.Columns = MenuPanels.Columns(sec.Count, InterfaceStyle.V.Menus.Start);
            // A part's slot heading where its slot changes (one column only).
            WeaponSlot? last = null;
            for (int k = sec.First; k < _menuCards.Count; k++)
            {
                string word = null;
                if (inSlots != null && sec.Columns == 1)
                {
                    int p = _menuCards[k] - _rackCards;
                    if (p >= 0 && p < inSlots.Count && inSlots[p] != null && inSlots[p].Slot != last)
                    {
                        last = inSlots[p].Slot;
                        word = WeaponAttachments.SlotWord(inSlots[p].Slot);
                        sec.Heads++;
                    }
                }
                _menuCardSlot.Add(word);
                _menuCardHead.Add(word != null ? InterfaceStyle.V.Menus.Start.SlotHead : 0f);
            }
            _menuSections.Add(sec);
        }

        /// <summary>The items of the loadout (its cards and START) or the upgrades (its cards).</summary>
        public int MenuItemCount(StartMenu menu)
        {
            if (TabsFor(menu)) return TabItemCount(menu); // work order 139: the page's rows
            EnsureMenuItems(menu);
            return _menuCards.Count + (menu.Screen == MenuScreen.Loadout ? 1 : 0);
        }

        /// <summary>Is this loadout item Not That One's card, owned (its arrows change the struck-out skill)?</summary>
        public bool MenuItemStrikes(StartMenu menu, int item)
        {
            EnsureMenuItems(menu);
            if (menu.Screen != MenuScreen.Loadout || item < 0 || item >= _menuCards.Count || _levels == null) return false;
            int card = _menuCards[item];
            MetaPiece[] pieces = Pieces;
            return card < _rackCards && card < pieces.Length && pieces[card].Effect == MetaEffect.StrikeOut && _levels.BetweenRunsState.Owns(pieces[card].Id);
        }

        /// <summary>A card of the loadout or the upgrades pressed: the screen between runs' press (<c>ClickBetweenCard</c>); on UPGRADES an owned piece or part refuses.</summary>
        public bool UseMenuItem(StartMenu menu, int item, out string refused)
        {
            refused = null;
            if (TabsFor(menu)) return UseTabItem(menu, item, out refused); // work order 139: the page's row, by its card
            EnsureMenuItems(menu);
            if (item < 0 || item >= _menuCards.Count) return false;
            int card = _menuCards[item];
            if (menu.Screen == MenuScreen.Upgrades && card < _weaponEnd && (_cardStateKind[card] == 0 || _cardStateKind[card] == 1))
            {
                refused = StartMenu.OwnedAlready;
                return false;
            }
            bool done = ClickBetweenCard(card);
            Tick(); // the screen's lines again now, so the cards say what changed
            if (!done) refused = RefusalFor(card); // the menus' help (RunHudMenuHelp.cs): how many more Echoes are needed
            return done;
        }

        // ---------------- The layout ----------------

        /// <summary>The panels of the screen up, laid out for this view (called by the menu's layout when what they show or the screen changes).</summary>
        public void LayOutMenuScreen(StartMenu menu, float viewWidth)
        {
            if (TabsFor(menu))
            {
                LayOutTabs(menu, viewWidth); // work order 139: the three pages
                return;
            }
            EnsureMenuItems(menu);
            _menuItemRects.Clear();
            _menuCardRaw.Clear();
            MenuStrikeLess = MenuStrikeMore = Rect.zero;
            MenuStrikeItem = -1;
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            float viewH = v.LayoutHeight;
            bool loadout = menu.Screen == MenuScreen.Loadout;
            float foot = s.HintBottom + s.HintHeight + 54f + (loadout ? s.StartHeight : 0f) + _menuLocked.Count * (s.LockedSize * 1.5f);
            _menuStart = loadout ? new Rect(viewWidth - s.EchoesMargin - s.StartWidth, viewH - s.SlotsBottom - s.StartHeight, s.StartWidth, s.StartHeight) : Rect.zero;
            if (menu.Screen == MenuScreen.Stats)
            {
                LayOutMenuStats(viewWidth, viewH - s.PanelsTop - foot, m, s);
                return;
            }
            float total = 0f, tallest = 0f;
            for (int k = 0; k < _menuSections.Count; k++)
            {
                MenuSection sec = _menuSections[k];
                int cols = Mathf.Max(1, sec.Columns);
                sec.W = cols * m.CardWidth + (cols - 1) * m.CardGap + 2f * m.PanelPad;
                float inner = sec.Count == 0 ? s.LockedSize * 2f : MenuPanels.InnerHeight(sec.Count, cols, sec.Heads, _cardH, m.CardGap, s);
                sec.H = 2f * m.PanelPad + m.PanelHeadHeight + inner;
                sec.X = total;
                total += sec.W + (k < _menuSections.Count - 1 ? s.PanelGap : 0f);
                tallest = Mathf.Max(tallest, sec.H);
                _menuSections[k] = sec;
            }
            float room = viewH - s.PanelsTop - foot;
            _mFit = total <= 0f ? 1f : Mathf.Clamp(Mathf.Min((viewWidth - 2f * m.Margin) / total, room / Mathf.Max(1f, tallest)), 0.1f, 1f);
            float x0 = (viewWidth / _mFit - total) * 0.5f;
            _menuX0 = x0;
            _mTop = s.PanelsTop / _mFit;
            for (int k = 0; k < _menuSections.Count; k++)
            {
                MenuSection sec = _menuSections[k];
                int cols = Mathf.Max(1, sec.Columns);
                float y = _mTop + m.PanelPad + m.PanelHeadHeight;
                for (int j = 0; j < sec.Count; j++)
                {
                    int item = sec.First + j;
                    float cx = x0 + sec.X + m.PanelPad + (j % cols) * (m.CardWidth + m.CardGap);
                    float cy;
                    if (cols == 1)
                    {
                        y += _menuCardHead[item];
                        cy = y;
                        y += _cardH + m.CardGap;
                    }
                    else cy = _mTop + m.PanelPad + m.PanelHeadHeight + (j / cols) * (_cardH + m.CardGap);
                    var raw = new Rect(cx, cy, m.CardWidth, _cardH);
                    _menuCardRaw.Add(raw);
                    _menuItemRects.Add(new Rect(raw.x * _mFit, raw.y * _mFit, raw.width * _mFit, raw.height * _mFit));
                    if (loadout && MenuItemStrikes(menu, item))
                    {
                        MenuStrikeItem = item;
                        MenuStrikeLess = new Rect((raw.xMax - 70f) * _mFit, (raw.yMax - 34f) * _mFit, 28f * _mFit, 28f * _mFit);
                        MenuStrikeMore = new Rect((raw.xMax - 38f) * _mFit, (raw.yMax - 34f) * _mFit, 28f * _mFit, 28f * _mFit);
                    }
                }
            }
            if (loadout) _menuItemRects.Add(_menuStart);
        }

        // ---------------- The stats ----------------

        readonly List<SheetStat> _menuSheet = new List<SheetStat>();
        readonly List<string> _recordNames = new List<string>(), _recordValues = new List<string>();
        readonly StatGroups _menuGroups = new StatGroups();
        RunModifiers _menuModifiers;
        int _menuStatsMade;
        float _sheetPanelH, _recordsPanelH, _statsX0, _menuColW;

        /// <summary>Her sheet at a run's start as she is dressed now, as the stats draw it. For tests.</summary>
        public IReadOnlyList<SheetStat> MenuSheetStats => _menuSheet;
        /// <summary>His records' lines, names and values. For tests.</summary>
        public IReadOnlyList<string> MenuRecordNames => _recordNames;
        public IReadOnlyList<string> MenuRecordValues => _recordValues;

        /// <summary>
        /// The stats' lines, made as the tab opens: her sheet with the modifiers a run would start with now (the run's own numbers,
        /// and with the trial on the rack's pieces equipped, the ranks and her pistols' parts, through the rules the run's start
        /// uses: <see cref="MetaRack.Apply"/>, <see cref="MetaRanks.Apply"/>, <see cref="WeaponRack.Apply"/>), and his records.
        /// </summary>
        public void MakeMenuStats(StartMenu menu)
        {
            Find();
            GeneratedLevels levels = _levels;
            RunValues run = levels != null ? levels.RunValues : new RunValues();
            RunModifiers m = _menuModifiers ?? (_menuModifiers = new RunModifiers());
            m.Clear();
            m.CooldownFloor = run.CooldownFloor;
            m.StatsAndTiers = run.StatsAndTiers;
            m.StatValues = run.Stats;
            MetaState meta = null;
            if (levels != null && levels.MetaOn)
            {
                meta = levels.BetweenRunsState;
                if (levels.BetweenRunsLoad != MetaLoad.Unreadable)
                {
                    MetaRack.Apply(meta, levels.MetaValues, m.Meta, levels.StruckOutSkill);
                    MetaRanks.Apply(meta, levels.MetaValues, m.Meta);
                    if (levels.AttachmentsOn) WeaponRack.Apply(meta, levels.MetaValues, m.Weapon, WeaponKind.Pistols);
                }
            }
            var c = new OfferContext
            {
                Modifiers = m,
                Run = run,
                Feel = _player != null ? _player.Feel : new FeelValues(),
                Kit = _player != null ? _player.Kit : new KitValues(),
                Identity = _player != null ? _player.Identity : new IdentityValues(),
                FocusValues = _player != null ? _player.FocusModeValues : new FocusModeValues(),
                FocusMode = FocusMode.Today, // a run starts with today's gauge (the choice comes after floor 1)
                OwnMaxHealth = _health != null ? _health.MaxHealth : Placeholders.PlayerHealth,
            };
            c.Health = c.MaxHealth; // a run starts with her health full
            string loadout = levels != null && (m.Weapon.Any || levels.AttachmentsOn)
                ? WeaponAttachments.WeaponName(WeaponKind.Pistols) + ": " + (m.Weapon.Any ? string.Join(", ", m.Weapon.Parts) : "stock")
                : null; // as the run's sheet words it (MakeSheetStats)
            _menuSheet.Clear();
            _menuSheet.AddRange(HerSheet.Stats(c, loadout));
            RecordsRules.Lines(menu.Records, meta, levels != null ? levels.MetaValues.CurrencyName : "Echoes", _recordNames, _recordValues);
            _menuStatsMade++;
        }

        void LayOutMenuStats(float viewWidth, float room, MenuValues m, StartMenuValues s)
        {
            _menuColW = (s.SheetWidth - 2f * m.PanelPad - 12f - m.ColumnInnerGap) * 0.5f;
            _menuGroups.LayOut(_menuSheet, _menuColW, m, m.Sheet);
            _sheetPanelH = 2f * m.PanelPad + m.PanelHeadHeight + _menuGroups.Height;
            _recordsPanelH = 2f * m.PanelPad + m.PanelHeadHeight + _recordNames.Count * s.RecordLine;
            float total = s.SheetWidth + s.PanelGap + s.RecordsWidth, tallest = Mathf.Max(_sheetPanelH, _recordsPanelH);
            _mFit = Mathf.Clamp(Mathf.Min((viewWidth - 2f * m.Margin) / total, room / Mathf.Max(1f, tallest)), 0.1f, 1f);
            _statsX0 = (viewWidth / _mFit - total) * 0.5f;
            _mTop = s.PanelsTop / _mFit;
        }

        // ---------------- Drawn ----------------

        /// <summary>The loadout, the upgrades or the stats drawn over the menu's backdrop (from the menu's OnGUI, its matrix the layout's).</summary>
        public void DrawMenuScreen(StartMenu menu, InterfaceValues v, MenuValues m)
        {
            if (TabsFor(menu))
            {
                DrawTabs(menu, v, m); // work order 139: the three pages
                return;
            }
            StartMenuValues s = m.Start;
            string word = menu.Screen == MenuScreen.Loadout ? LoadoutWord : menu.Screen == MenuScreen.Upgrades ? UpgradesWord : StatsWord;
            DrawHeadingPlate(new Rect(s.TabLeft + m.HeadingHeight * m.HeadingSlant * 0.5f, s.HeadTop, m.HeadingWidth, m.HeadingHeight), word, v, m);
            Matrix4x4 layout = GUI.matrix;
            if (_mFit < 1f) GUI.matrix = layout * Matrix4x4.Scale(new Vector3(_mFit, _mFit, 1f));
            Event e = Event.current;
            Vector2 mouse = e != null ? e.mousePosition : new Vector2(-1f, -1f); // in the fitted space under this matrix
            if (menu.Screen == MenuScreen.Stats) DrawMenuStats(v, m, s);
            else DrawMenuCards(menu, mouse, v, m, s);
            GUI.matrix = layout;

            // Under the panels: the locked lines, the last thing tried, START.
            float y = s.PanelsTop + (PanelsBottom(m) - _mTop) * _mFit + 24f;
            GUIStyle locked = InterfaceStyle.Text(false, s.LockedSize, TextAnchor.MiddleLeft);
            float lx = (InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale) - s.LockedWidth) * 0.5f;
            for (int i = 0; i < _menuLocked.Count; i++)
            {
                if (i % 2 == 0)
                {
                    var chip = new Rect(lx - 120f, y + 4f, 100f, s.LockedSize * 1.5f - 8f);
                    InterfaceStyle.Slanted(chip, InterfaceStyle.C(v.Black, 0.9f));
                    InterfaceStyle.SlantedFrame(chip, InterfaceStyle.C(v.Grey), 2f);
                    TextLabel(chip, LockedWord, InterfaceStyle.Text(true, s.LockedSize - 4, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Grey));
                }
                TextLabel(new Rect(lx, y, s.LockedWidth, s.LockedSize * 1.5f), _menuLocked[i], locked, InterfaceStyle.C(v.White, i % 2 == 0 ? 0.75f : 0.55f), true);
                y += s.LockedSize * 1.5f;
            }
            string note = menu.Note.Length > 0 ? menu.Note : _levels != null && _levels.BetweenRunsUp ? _levels.BetweenRunsNote : "";
            if (note.Length > 0)
                TextLabel(new Rect(s.EchoesMargin, v.LayoutHeight - s.HintBottom - s.HintHeight - 40f, 1100f, 30f), note, InterfaceStyle.Text(false, s.NoteSize, TextAnchor.MiddleLeft), NoteInk(note, v), true); // the menus' help: a refusal in red
            if (menu.Screen == MenuScreen.Loadout) DrawMenuStart(menu.Selected == _menuCards.Count, v, m, s);
        }

        float PanelsBottom(MenuValues m)
        {
            float bottom = _mTop;
            foreach (MenuSection sec in _menuSections) bottom = Mathf.Max(bottom, _mTop + sec.H);
            return bottom;
        }

        void DrawMenuCards(StartMenu menu, Vector2 mouse, InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            float x0 = _menuX0;
            foreach (MenuSection sec in _menuSections)
            {
                var panel = new Rect(x0 + sec.X, _mTop, sec.W, sec.H);
                DrawPanel(panel, sec.Head, sec.Sub, InterfaceStyle.Ink(sec.Stroke), v, m);
                if (sec.Count == 0)
                    TextLabel(new Rect(panel.x + m.PanelPad + 10f, panel.y + m.PanelPad + m.PanelHeadHeight, panel.width - 2f * m.PanelPad, s.LockedSize * 1.6f), NothingOwned,
                              InterfaceStyle.Text(false, s.LockedSize - 2, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Grey), true);
            }
            for (int item = 0; item < _menuCardRaw.Count && item < _menuCards.Count; item++)
            {
                Rect r = _menuCardRaw[item];
                if (_menuCardSlot[item] != null)
                    TextLabel(new Rect(r.x + 10f, r.y - s.SlotHead, r.width, s.SlotHead - 4f), _menuCardSlot[item], InterfaceStyle.Text(true, 20, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal), true);
                DrawCard(_menuCards[item], r, item == menu.Selected, r.Contains(mouse), v, m);
                if (item == MenuStrikeItem)
                {
                    KeyCap(new Rect(r.xMax - 70f, r.yMax - 34f, 28f, 28f), "<", m, v);
                    KeyCap(new Rect(r.xMax - 38f, r.yMax - 34f, 28f, 28f), ">", m, v);
                }
            }
        }

        void DrawMenuStart(bool selected, InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            Rect r = _menuStart;
            float pivot = r.center.y;
            InterfaceStyle.InkedPlate(r, pivot, v.Slant, v.HeavyOutline, v.Depth * 2f, selected ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.White, 0.5f), selected ? 1.3f : 0.8f);
            InterfaceStyle.Leaning(r, pivot, v.Slant, InterfaceStyle.C(v.Teal));
            float x = r.x + v.Slant * r.height * 0.5f + 14f, capW = MenuCapWidth("Space", m);
            KeyCap(new Rect(x, r.y + (r.height - m.KeyCapHeight) * 0.5f, capW, m.KeyCapHeight), "Space", m, v, true);
            TextLabel(new Rect(x + capW + 14f, r.y, r.width, r.height), StartWord, InterfaceStyle.Text(true, m.ButtonTextSize + 8, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Black));
            if (selected) InterfaceStyle.Slashes(r.xMax - 14f, r.y + 10f, r.height - 20f, 7f, 2, InterfaceStyle.C(v.Red));
        }

        float MenuCapWidth(string key, MenuValues m) => Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(key, CapStyle(m)).x + 18f);

        void DrawMenuStats(InterfaceValues v, MenuValues m, StartMenuValues s)
        {
            var sheet = new Rect(_statsX0, _mTop, s.SheetWidth, _sheetPanelH);
            DrawPanel(sheet, SheetHead, SheetSub, InterfaceStyle.C(v.Teal), v, m);
            _menuGroups.Draw(_menuSheet, sheet.x + m.PanelPad + 10f, sheet.y + m.PanelPad + m.PanelHeadHeight, sheet.center.y, m.PanelSlant, m.ColumnInnerGap, m, m.Sheet, v);
            var rec = new Rect(_statsX0 + s.SheetWidth + s.PanelGap, _mTop, s.RecordsWidth, _recordsPanelH);
            DrawPanel(rec, RecordsHead, RecordsSub, InterfaceStyle.C(v.Gold), v, m);
            GUIStyle name = InterfaceStyle.Text(false, s.RecordNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, s.RecordValueSize, TextAnchor.MiddleRight);
            float y = rec.y + m.PanelPad + m.PanelHeadHeight, x = rec.x + m.PanelPad + 10f, w = rec.width - 2f * m.PanelPad - 24f;
            for (int i = 0; i < _recordNames.Count; i++)
            {
                float lean = (rec.center.y - (y + s.RecordLine * 0.5f)) * m.PanelSlant;
                if (i % 2 == 0) InterfaceStyle.Fill(new Rect(x + lean - 6f, y, w + 12f, s.RecordLine), InterfaceStyle.C(v.White, 0.045f));
                TextLabel(new Rect(x + lean, y, w, s.RecordLine), _recordNames[i], name, InterfaceStyle.C(v.White, 0.75f));
                TextLabel(new Rect(x + lean, y, w, s.RecordLine), _recordValues[i], value, i >= 6 && _recordValues[i] == "trial off" ? InterfaceStyle.C(v.Grey) : InterfaceStyle.C(v.White));
                y += s.RecordLine;
            }
        }

        // ---------------- Measured ----------------

        /// <summary>What of the loadout's, the upgrades' or the stats' words does not fit inside its shape at the screen now; empty when all does. For tests.</summary>
        public string MenuTextOutside(StartMenu menu)
        {
            if (TabsFor(menu)) return TabsTextOutside(menu); // work order 139
            var problems = new List<string>();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            StartMenuValues s = m.Start;
            string word = menu.Screen == MenuScreen.Loadout ? LoadoutWord : menu.Screen == MenuScreen.Upgrades ? UpgradesWord : StatsWord;
            if (HeadingPad(m) + InterfaceStyle.Measure(word, InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft)).x + 16f > m.HeadingWidth) problems.Add("the heading " + word + " is wider than its plate");
            GUIStyle head = InterfaceStyle.Text(true, m.PanelHeadSize, TextAnchor.MiddleLeft), sub = InterfaceStyle.Text(false, m.PanelSubSize, TextAnchor.MiddleRight);
            if (menu.Screen == MenuScreen.Stats)
            {
                _menuGroups.Outside(_menuSheet, _menuColW * 2f + m.ColumnInnerGap, problems);
                if (InterfaceStyle.Measure(SheetHead, head).x + 24f + InterfaceStyle.Measure(SheetSub, sub).x + 50f > s.SheetWidth - 2f * m.PanelPad) problems.Add("the sheet's heading is wider than its panel");
                if (InterfaceStyle.Measure(RecordsHead, head).x + 24f + InterfaceStyle.Measure(RecordsSub, sub).x + 50f > s.RecordsWidth - 2f * m.PanelPad) problems.Add("the records' heading is wider than its panel");
                GUIStyle name = InterfaceStyle.Text(false, s.RecordNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, s.RecordValueSize, TextAnchor.MiddleRight);
                for (int i = 0; i < _recordNames.Count; i++)
                    if (InterfaceStyle.Measure(_recordNames[i], name).x + 20f + InterfaceStyle.Measure(_recordValues[i], value).x > s.RecordsWidth - 2f * m.PanelPad - 24f) problems.Add("the record " + _recordNames[i] + " is wider than its panel");
                return string.Join("; ", problems);
            }
            foreach (MenuSection sec in _menuSections)
                if (InterfaceStyle.Measure(sec.Head, head).x + 24f + InterfaceStyle.Measure(sec.Sub, sub).x + 50f > sec.W - 2f * m.PanelPad) problems.Add("the panel " + sec.Head + "'s heading is wider than its panel");
            if (_menuCards.Count > 0)
            {
                string cards = BetweenTextOutside(); // the cards' own measure (work order 078): every name and state inside its card
                if (cards.Length > 0) problems.Add(cards);
            }
            GUIStyle locked = InterfaceStyle.Text(false, s.LockedSize, TextAnchor.MiddleLeft);
            foreach (string l in _menuLocked)
                if (InterfaceStyle.Measure(l, locked).x > s.LockedWidth) problems.Add("the locked line \"" + l + "\" is wider than its place");
            if (_menuLocked.Count > 0 && InterfaceStyle.Measure(LockedWord, InterfaceStyle.Text(true, s.LockedSize - 4, TextAnchor.MiddleCenter)).x > 92f) problems.Add("LOCKED is wider than its chip");
            if (menu.Screen == MenuScreen.Loadout)
            {
                float need = v.Slant * s.StartHeight * 0.5f + 14f + MenuCapWidth("Space", m) + 14f + InterfaceStyle.Measure(StartWord, InterfaceStyle.Text(true, m.ButtonTextSize + 8, TextAnchor.MiddleLeft)).x + 40f;
                if (need > s.StartWidth) problems.Add("START is wider than its button");
                if (InterfaceStyle.Measure(NothingOwned, InterfaceStyle.Text(false, s.LockedSize - 2, TextAnchor.MiddleLeft)).x > m.CardWidth) problems.Add("the line for nothing owned is wider than a panel");
            }
            float bottom = s.PanelsTop + (PanelsBottom(m) - _mTop) * _mFit;
            if (bottom > v.LayoutHeight - s.HintBottom - s.HintHeight - 40f) problems.Add("the panels run into the foot (" + bottom.ToString("0") + ")");
            return string.Join("; ", problems);
        }
    }
}
