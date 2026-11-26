using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The start menu's upgrades in three tabs of three forms (work order 139, <see cref="UpgradeTabsValues.On"/>; Kristian,
    /// 2026-11-25: "At alt er kort er ikke så kult ... Weapon upgrade som en egen fane, der man ser modellen til våpenet i høy res,
    /// kan dra og se rundt"), as part of <see cref="RunHud"/>, in place of the one screen of cards:
    /// - WEAPON: three short lists at the left (SIGHT, BARREL, GRIP: "none" and the slot's parts, each owned, fitted or for sale
    ///   with its price), her pistol large in the middle on its own stage (<see cref="WeaponStage"/>) with the fitted parts drawn
    ///   on it, under it a panel saying what the part under the cursor gives and costs, and her numbers at the right with the
    ///   change the part under the cursor would make (teal better, red worse). A line leads from the row to the part's place.
    /// - RANKS: a row a rank: its name, a row of steps filled as far as she has bought, the next step's gain, its price.
    /// - RACK: the rule pieces as a list at the left, one large panel at the right for the piece under the cursor.
    ///
    /// Every press is the screen between runs' own (<c>ClickBetweenCard</c>, by the card it would be), so the prices, what is
    /// bought and what is saved are as before, and the menus' help keeps working: what she cannot afford is dimmer, a try at it
    /// flashes its price and the Echoes plate red with a shake and says how many more are needed. With the attachments switched
    /// off, a press on a part that buys or fits it switches them on first (work order 139: fitting a part is what makes it count).
    /// Its words and numbers are made when what they show changes, never in a frame that changes nothing.
    /// </summary>
    public sealed partial class RunHud
    {
        static readonly CultureInfo s_Inv139 = CultureInfo.InvariantCulture;

        enum TabRow { None, Part, Rank, Piece }

        struct TabItem
        {
            public TabRow Kind;
            public WeaponSlot Slot;
            public WeaponAttachment Part;
            /// <summary>The part's place in the store's slot order, the rank's in the table, the piece's in the rack.</summary>
            public int Index;
            public string Name, State, Sub;
            /// <summary>0 fitted, equipped or at its last rank; 1 owned; 2 for sale and affordable; 3 out of reach.</summary>
            public int Look;
            /// <summary>Its state's chip's width, measured with the rows (never in a frame).</summary>
            public float ChipW;
        }

        readonly List<TabItem> _tabItems = new List<TabItem>();
        readonly List<int> _tabRowsPerSlot = new List<int>();
        readonly List<int> _tabSlotFirst = new List<int>();
        int _tabsMadeFor = int.MinValue;
        bool _tabsLocked;
        float _tabFit = 1f, _tabX0;
        Rect _tabStage;

        public const string WeaponPartsHead = "PARTS", WeaponPartsSub = "one a slot", NumbersHead = "HER PISTOLS", NumbersSub = "the basic shot";
        public const string RanksPageHead = "RANKS", RanksPageSub = "one press a step, no undo", RackPageHead = "RACK", RackPageSub = "pieces to own";
        public const string NoneName = "None", StockWord = "STOCK", FittedWord = "FITTED", OwnedWord = "OWNED", EquippedWord = "EQUIPPED", NotYetWord = "NOT YET";
        public const string AttachmentsOffLine = "Attachments are switched off: fitting a part here turns them on.";
        public const string StandInLine = "Stand-in: her pistol from her character model, until a model is put in Resources/Weapon/Pistol (Editor only).";

        /// <summary>Is the start menu's screen the upgrades in three tabs (work order 139)?</summary>
        static bool TabsFor(StartMenu menu) => UpgradeTabsValues.On && menu != null && menu.Screen == MenuScreen.Upgrades;

        /// <summary>What the tabs add to the menu's version: the switch, the page, the attachments' stored choice.</summary>
        static int TabsVersion(StartMenu menu) => !UpgradeTabsValues.On || menu == null ? 0 : 1 + 2 * (int)menu.Page + 8 * (AttachmentsSwitch.Stored + 2);

        /// <summary>The items of the page up (its rows), for the keys and the clicks.</summary>
        public int TabItemCount(StartMenu menu)
        {
            EnsureTabItems(menu);
            return _tabItems.Count;
        }

        /// <summary>The row of the page up an item is: its words (for tests).</summary>
        public string TabItemName(StartMenu menu, int item)
        {
            EnsureTabItems(menu);
            return item >= 0 && item < _tabItems.Count ? _tabItems[item].Name : null;
        }

        public string TabItemState(StartMenu menu, int item)
        {
            EnsureTabItems(menu);
            return item >= 0 && item < _tabItems.Count ? _tabItems[item].State : null;
        }

        /// <summary>The WEAPON page's item of this part (or of the slot's "none" when <paramref name="partId"/> is null); -1 when not shown.</summary>
        public int TabItemOf(StartMenu menu, WeaponSlot slot, string partId)
        {
            EnsureTabItems(menu);
            for (int i = 0; i < _tabItems.Count; i++)
            {
                TabItem t = _tabItems[i];
                if (t.Slot != slot || (t.Kind != TabRow.None && t.Kind != TabRow.Part)) continue;
                if (partId == null ? t.Kind == TabRow.None : t.Part != null && t.Part.Id == partId) return i;
            }
            return -1;
        }

        /// <summary>The stage's place in the 1080 layout as last laid out (for the drag's hit test and tests).</summary>
        public Rect TabStageRect => _tabStage;

        void EnsureTabItems(StartMenu menu)
        {
            Find();
            int version = MenuVersion(menu);
            if (version == _tabsMadeFor) return;
            _tabsMadeFor = version;
            _tabItems.Clear();
            _tabRowsPerSlot.Clear();
            _tabSlotFirst.Clear();
            _numbersFor = int.MinValue;
            GeneratedLevels levels = _levels;
            _tabsLocked = levels == null || !levels.MetaOn;
            if (_tabsLocked) return;
            if (levels.BetweenRunsUp && _betweenLines.Count >= 4) LayOutBetween(); // the screen between runs' cards: the presses and the menus' help go by them
            MetaState s = levels.BetweenRunsState;
            MetaValues mv = levels.MetaValues;
            string currency = mv.CurrencyName.ToUpperInvariant();
            switch (menu.Page)
            {
                case UpgradesPage.Weapon:
                    List<WeaponAttachment> parts = WeaponRack.InSlotOrder(mv);
                    foreach (WeaponSlot slot in WeaponAttachments.SlotsOf(WeaponKind.Pistols))
                    {
                        _tabSlotFirst.Add(_tabItems.Count);
                        WeaponAttachment fitted = FittedPart(s, mv, slot);
                        _tabItems.Add(new TabItem { Kind = TabRow.None, Slot = slot, Index = -1, Name = NoneName, State = fitted == null ? StockWord : "", Sub = "the stock gun", Look = fitted == null ? 0 : 1 });
                        int rows = 1;
                        for (int k = 0; k < parts.Count; k++)
                        {
                            WeaponAttachment p = parts[k];
                            if (p.Slot != slot) continue;
                            bool isFitted = WeaponRack.IsFitted(s, p.Id), owned = WeaponRack.Owns(s, p.Id);
                            string state = isFitted ? FittedWord : owned ? OwnedWord : p.Price.ToString(s_Inv139) + " " + currency;
                            _tabItems.Add(new TabItem { Kind = TabRow.Part, Slot = slot, Part = p, Index = k, Name = p.Name, State = state, Look = isFitted ? 0 : owned ? 1 : s.Currency >= p.Price ? 2 : 3 });
                            rows++;
                        }
                        _tabRowsPerSlot.Add(rows);
                    }
                    break;
                case UpgradesPage.Ranks:
                    MetaRank[] ranks = RankTable;
                    for (int r = 0; r < ranks.Length; r++)
                    {
                        MetaRank rk = ranks[r];
                        if (rk == null) continue;
                        bool last = MetaRanks.AtMost(s, rk);
                        int price = MetaRanks.NextPrice(s, mv, rk);
                        _tabItems.Add(new TabItem
                        {
                            Kind = TabRow.Rank, Index = r, Name = rk.Name, Sub = MetaRanks.RankWords(s, rk),
                            State = last ? "LAST RANK" : price.ToString(s_Inv139) + " " + currency,
                            Look = last ? 0 : s.Currency >= price ? 2 : 3,
                        });
                    }
                    break;
                default:
                    MetaPiece[] pieces = Pieces;
                    for (int i = 0; i < pieces.Length; i++)
                    {
                        MetaPiece p = pieces[i];
                        if (p == null) continue;
                        MetaBuy buy = MetaRack.CanBuy(s, mv, p.Id);
                        string state = s.IsEquipped(p.Id) ? EquippedWord : s.Owns(p.Id) ? OwnedWord : buy == MetaBuy.NotForSale ? NotYetWord
                                     : buy == MetaBuy.NeedsFirst ? "NEEDS " + (MetaRack.Find(mv, p.Needs)?.Name ?? "?").ToUpperInvariant()
                                     : p.Price.ToString(s_Inv139) + " " + currency;
                        int look = s.IsEquipped(p.Id) ? 0 : s.Owns(p.Id) ? 1 : buy == MetaBuy.Bought ? 2 : 3;
                        _tabItems.Add(new TabItem { Kind = TabRow.Piece, Index = i, Name = p.Name, Sub = p.KindWord + ", " + p.Points.ToString(s_Inv139) + (p.Points == 1 ? " point" : " points"), State = state, Look = look });
                    }
                    break;
            }
            // Measured once with the rows (measuring sets a content's words: garbage in a frame, work order 090).
            GUIStyle chip = InterfaceStyle.Text(true, InterfaceStyle.V.Menus.CardTextSize + 1, TextAnchor.MiddleCenter);
            for (int i = 0; i < _tabItems.Count; i++)
            {
                TabItem t = _tabItems[i];
                t.ChipW = t.State.Length > 0 ? InterfaceStyle.Measure(t.State, chip).x + 22f : 0f;
                _tabItems[i] = t;
            }
            _enterCapW = MenuCapWidth("Enter", InterfaceStyle.V.Menus);
        }

        float _enterCapW = 60f;

        /// <summary>The part fitted in a slot of her pistols (the store's), found without making anything.</summary>
        static WeaponAttachment FittedPart(MetaState s, MetaValues v, WeaponSlot slot)
        {
            if (s == null || v?.Parts == null) return null;
            for (int i = 0; i < s.AttachmentsFitted.Count; i++)
            {
                string id = s.AttachmentsFitted[i];
                for (int k = 0; k < v.Parts.Length; k++)
                {
                    WeaponAttachment p = v.Parts[k];
                    if (p != null && p.Id == id && p.Slot == slot && p.Weapon == WeaponKind.Pistols && WeaponRack.InStore(v, p)) return p;
                }
            }
            return null;
        }

        /// <summary>The card of the screen between runs an item's press is (the menus' help goes by it): the piece's, the part's or the rank's; -1 for "none".</summary>
        int TabCard(TabItem t) =>
            t.Kind == TabRow.Piece ? t.Index : t.Kind == TabRow.Part ? _rackCards + t.Index : t.Kind == TabRow.Rank ? _weaponEnd + t.Index : -1;

        /// <summary>The Echoes she still needs for an item: 0 when she can pay it, owns it, or it is not bought with Echoes now.</summary>
        int TabNeed(TabItem t)
        {
            if (_levels == null) return 0;
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            switch (t.Kind)
            {
                case TabRow.Part: return WeaponRack.Owns(s, t.Part.Id) ? 0 : MenuHelpRules.Need(t.Part.Price, s.Currency);
                case TabRow.Rank:
                    MetaRank r = RankTable[t.Index];
                    return MetaRanks.AtMost(s, r) ? 0 : MenuHelpRules.Need(MetaRanks.NextPrice(s, mv, r), s.Currency);
                case TabRow.Piece:
                    MetaPiece p = Pieces[t.Index];
                    return MetaRack.CanBuy(s, mv, p.Id) == MetaBuy.NotEnough ? MenuHelpRules.Need(p.Price, s.Currency) : 0;
                default: return 0;
            }
        }

        /// <summary>An item of the page up she cannot afford now (drawn dimmer with the menus' help on).</summary>
        public bool TabItemUnaffordable(StartMenu menu, int item)
        {
            EnsureTabItems(menu);
            return MenuHelpValues.On && item >= 0 && item < _tabItems.Count && TabNeed(_tabItems[item]) > 0;
        }

        /// <summary>
        /// A press on an item of the page up (Enter, a click): the screen between runs' own press for its card. A piece she owns
        /// refuses as on the cards' UPGRADES; "none" takes off what its slot holds; a part bought or fitted with the attachments
        /// switched off switches them on first. False when nothing happened (<paramref name="refused"/> says why, when it can).
        /// </summary>
        public bool UseTabItem(StartMenu menu, int item, out string refused)
        {
            refused = null;
            EnsureTabItems(menu);
            if (item < 0 || item >= _tabItems.Count || _levels == null || !_levels.BetweenRunsUp) return false;
            TabItem t = _tabItems[item];
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            if (t.Kind == TabRow.Piece && s.Owns(Pieces[t.Index].Id))
            {
                refused = StartMenu.OwnedAlready;
                return false;
            }
            if (t.Kind == TabRow.None)
            {
                WeaponAttachment on = FittedPart(s, mv, t.Slot);
                if (on == null) return false; // the stock gun already
                t.Part = on;
                t.Index = WeaponRack.InSlotOrder(mv).IndexOf(on);
                t.Kind = TabRow.Part;
            }
            if (t.Kind == TabRow.Part && !_levels.AttachmentsOn)
            {
                bool would = WeaponRack.Owns(s, t.Part.Id) || WeaponRack.CanBuy(s, mv, t.Part.Id) == MetaBuy.Bought;
                if (!would)
                {
                    NoteTabRefusal(_rackCards + t.Index, t.Part.Name, TabNeed(t));
                    refused = RefusalFor(_rackCards + t.Index);
                    return false;
                }
                // Work order 139: fitting a part on the WEAPON tab is what makes the parts count in the next run.
                AttachmentsSwitch.Set(true);
                Tick();
                LayOutBetween();
            }
            int card = TabCard(t);
            bool done = ClickBetweenCard(card);
            Tick(); // the screen's lines again now, so the rows say what changed
            if (!done) refused = RefusalFor(card);
            return done;
        }

        /// <summary>A try at a part with the attachments off and too few Echoes: answered as the menus' help answers any (the cards of the parts are not there).</summary>
        void NoteTabRefusal(int card, string name, int need)
        {
            if (!MenuHelpValues.On || need <= 0 || _levels == null) return;
            _refusedCard = card;
            _refusedAt = Time.unscaledTime;
            _refusedLine = MenuHelpRules.NotEnough(need, _levels.MetaValues.CurrencyName, name);
            EchoesRefusals++;
            if (PlaceholderSounds.Current != null) PlaceholderSounds.Current.AskFor(PlaceholderSound.OffTick, MenuHelpValues.RefusalSoundGain);
        }

        /// <summary>The WEAPON page's row the keys start on: the sight's fitted part, or its "none" (so the preview is what is fitted).</summary>
        public int TabRestRow(StartMenu menu)
        {
            EnsureTabItems(menu);
            if (menu.Page != UpgradesPage.Weapon || _levels == null) return 0;
            WeaponAttachment on = FittedPart(_levels.BetweenRunsState, _levels.MetaValues, WeaponSlot.Sight);
            int row = TabItemOf(menu, WeaponSlot.Sight, on != null ? on.Id : null);
            return row < 0 ? 0 : row;
        }

        // ---------------- The stage and the preview ----------------

        /// <summary>
        /// Every frame the WEAPON page is up: the stage kept, and on it what is fitted, with the slot under the cursor holding
        /// the part under it (ghosted when not owned) or nothing for "none". Nothing made unless something changed.
        /// </summary>
        public void KeepStage(StartMenu menu)
        {
            EnsureTabItems(menu);
            if (_tabsLocked || _levels == null) return;
            WeaponStage stage = WeaponStage.Ensure();
            stage.Keep();
            LayoutRect plan = UpgradeTabsLayout.Stage; // its size in the 1080 layout, fitted as the page is drawn
            stage.SetView(plan.Width * _tabFit, plan.Height * _tabFit);
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            int sel = menu.Selected;
            bool preview = sel >= 0 && sel < _tabItems.Count && (_tabItems[sel].Kind == TabRow.Part || _tabItems[sel].Kind == TabRow.None);
            for (int i = 0; i < 3; i++)
            {
                var slot = (WeaponSlot)i;
                if (preview && _tabItems[sel].Slot == slot)
                {
                    WeaponAttachment p = _tabItems[sel].Part;
                    stage.Show(slot, p?.Id, p != null && !WeaponRack.Owns(s, p.Id));
                }
                else
                {
                    WeaponAttachment on = FittedPart(s, mv, slot);
                    stage.Show(slot, on?.Id, false);
                }
            }
        }

        // ---------------- Her numbers ----------------

        readonly double[] _numNow = new double[WeaponNumbers.Count], _numThen = new double[WeaponNumbers.Count];
        readonly string[] _numValue = new string[WeaponNumbers.Count], _numChange = new string[WeaponNumbers.Count], _numName = new string[WeaponNumbers.Count];
        readonly int[] _numBetter = new int[WeaponNumbers.Count];
        int _numbersFor = int.MinValue, _numbersSelected = -1;
        RunModifiers _tabModifiers;
        OfferContext _tabContext;
        string _panelName = "", _panelSub = "", _panelGives = "", _panelCosts = "", _panelPress = "", _panelPressKey = "";
        bool _panelPressOn;

        /// <summary>The numbers as fitted and as they would be with the part under the cursor (for tests).</summary>
        public IReadOnlyList<double> TabNumbersNow => _numNow;
        public IReadOnlyList<double> TabNumbersPreview => _numThen;
        public IReadOnlyList<string> TabNumberChanges => _numChange;

        /// <summary>Her numbers and the part's panel's words, made again when what they show or the row under the cursor changed.</summary>
        public void MakeTabNumbers(StartMenu menu)
        {
            EnsureTabItems(menu);
            if (_numbersFor == _tabsMadeFor && _numbersSelected == menu.Selected) return;
            _numbersFor = _tabsMadeFor;
            _numbersSelected = menu.Selected;
            if (_tabsLocked || _levels == null) return;
            GeneratedLevels levels = _levels;
            MetaState s = levels.BetweenRunsState;
            MetaValues mv = levels.MetaValues;
            RunValues run = levels.RunValues;
            RunModifiers m = _tabModifiers ?? (_tabModifiers = new RunModifiers());
            m.Clear();
            m.CooldownFloor = run.CooldownFloor;
            m.StatsAndTiers = run.StatsAndTiers;
            m.StatValues = run.Stats;
            if (levels.BetweenRunsLoad != MetaLoad.Unreadable)
            {
                MetaRack.Apply(s, mv, m.Meta, levels.StruckOutSkill);
                MetaRanks.Apply(s, mv, m.Meta);
            }
            OfferContext c = _tabContext ?? (_tabContext = new OfferContext());
            c.Modifiers = m;
            c.Run = run;
            c.Feel = _player != null ? _player.Feel : new FeelValues();
            // As fitted (what the model shows), and as the row under the cursor would make it.
            WeaponRack.Apply(s, mv, m.Weapon, WeaponKind.Pistols);
            WeaponNumbers.Of(c, _numNow);
            int sel = menu.Selected;
            bool preview = menu.Page == UpgradesPage.Weapon && sel >= 0 && sel < _tabItems.Count && (_tabItems[sel].Kind == TabRow.Part || _tabItems[sel].Kind == TabRow.None);
            if (preview)
            {
                WeaponNumbers.Preview(s, mv, _tabItems[sel].Slot, _tabItems[sel].Part?.Id, m.Weapon);
                WeaponNumbers.Of(c, _numThen);
            }
            else System.Array.Copy(_numNow, _numThen, _numNow.Length);
            for (int i = 0; i < WeaponNumbers.Count; i++)
            {
                var n = (WeaponNumber)i;
                _numName[i] = WeaponNumbers.Name(n);
                _numValue[i] = WeaponNumbers.Words(n, _numNow[i]);
                _numChange[i] = WeaponNumbers.Change(n, _numNow[i], _numThen[i]);
                _numBetter[i] = WeaponNumbers.Better(n, _numNow[i], _numThen[i]);
            }
            MakePartPanel(menu, s, mv);
            MeasurePartPanel();
        }

        void MakePartPanel(StartMenu menu, MetaState s, MetaValues mv)
        {
            _panelName = _panelSub = _panelGives = _panelCosts = _panelPress = "";
            _panelPressKey = "Enter";
            _panelPressOn = false;
            int sel = menu.Selected;
            if (sel < 0 || sel >= _tabItems.Count) return;
            TabItem t = _tabItems[sel];
            string currency = mv.CurrencyName;
            if (t.Kind == TabRow.None)
            {
                WeaponAttachment on = FittedPart(s, mv, t.Slot);
                _panelName = WeaponAttachments.SlotWord(t.Slot) + ":  NONE";
                _panelSub = "the stock gun";
                _panelGives = "Her pistols' own " + WeaponAttachments.SlotWord(t.Slot).ToLowerInvariant() + ", as they come.";
                _panelCosts = "Nothing.";
                _panelPress = on != null ? "TAKE OFF " + on.Name.ToUpperInvariant() : ""; // nothing to press: the stock gun already
                _panelPressOn = on != null;
                return;
            }
            if (t.Kind != TabRow.Part) return;
            WeaponAttachment p = t.Part;
            bool fitted = WeaponRack.IsFitted(s, p.Id), owned = WeaponRack.Owns(s, p.Id);
            _panelName = p.Name.ToUpperInvariant();
            _panelSub = WeaponAttachments.SlotWord(p.Slot).ToLowerInvariant() + (fitted ? ", fitted" : owned ? ", owned" : ", " + p.Price.ToString(s_Inv139) + " " + currency);
            _panelGives = p.Line;
            _panelCosts = string.IsNullOrEmpty(p.CostLine) ? "Nothing." : p.CostLine;
            int need = MenuHelpRules.Need(p.Price, s.Currency);
            _panelPress = fitted ? "TAKE OFF" : owned ? "FIT" : need > 0 ? "BUY  ·  " + need.ToString(s_Inv139) + " MORE NEEDED" : "BUY  ·  " + p.Price.ToString(s_Inv139) + " " + currency.ToUpperInvariant();
            _panelPressOn = fitted || owned || need <= 0;
        }

        float _panelPressW;
        /// <summary>The room of the words GIVES and COSTS before their lines.</summary>
        const float PartWordW = 92f;
        float PartPressWidth => _enterCapW + 28f + _panelPressW + 30f;

        /// <summary>The part's press's words measured once they are made.</summary>
        void MeasurePartPanel() => _panelPressW = _panelPress.Length > 0 ? InterfaceStyle.Measure(_panelPress, InterfaceStyle.Text(true, 20, TextAnchor.MiddleLeft)).x : 0f;

        // ---------------- The layout ----------------

        /// <summary>A rectangle of the page's 1920-wide plan in the menu's 1080 layout: fitted into the view, its top kept under the tabs.</summary>
        Rect TabPlan(LayoutRect r) =>
            new Rect((r.X + _tabX0) * _tabFit, UpgradeTabsLayout.Top + (r.Y - UpgradeTabsLayout.Top) * _tabFit, r.Width * _tabFit, r.Height * _tabFit);

        /// <summary>The page's rows laid out for this view (the menu's hit test takes them as its items).</summary>
        public void LayOutTabs(StartMenu menu, float viewWidth)
        {
            EnsureTabItems(menu);
            _menuItemRects.Clear();
            _menuCardRaw.Clear();
            MenuStrikeLess = MenuStrikeMore = Rect.zero;
            MenuStrikeItem = -1;
            _tabFit = UpgradeTabsLayout.Fit(viewWidth, out _tabX0);
            _tabStage = Rect.zero;
            for (int i = 0; i < _tabItems.Count; i++) _menuItemRects.Add(TabPlan(PlanRow(menu, i)));
            if (menu.Page == UpgradesPage.Weapon && !_tabsLocked)
            {
                _tabStage = TabPlan(UpgradeTabsLayout.Stage);
            }
        }

        /// <summary>An item's row in the plan.</summary>
        LayoutRect PlanRow(StartMenu menu, int i)
        {
            TabItem t = _tabItems[i];
            switch (t.Kind)
            {
                case TabRow.None:
                case TabRow.Part:
                    int slot = (int)t.Slot;
                    return UpgradeTabsLayout.ListRowRect(slot, i - _tabSlotFirst[slot], _tabRowsPerSlot);
                case TabRow.Rank: return UpgradeTabsLayout.RankRowRect(i, _tabItems.Count);
                default: return UpgradeTabsLayout.RackRowRect(i);
            }
        }

        // ---------------- Drawn ----------------

        /// <summary>The page up drawn over the menu's backdrop (from the menu's OnGUI, its matrix the layout's).</summary>
        public void DrawTabs(StartMenu menu, InterfaceValues v, MenuValues m)
        {
            StartMenuValues s = m.Start;
            EnsureTabItems(menu);
            float viewH = v.LayoutHeight;
            if (_tabsLocked)
            {
                GUIStyle locked = InterfaceStyle.Text(false, s.LockedSize, TextAnchor.MiddleCenter);
                float w = InterfaceStyle.ScreenWidth / Mathf.Max(1e-4f, InterfaceStyle.Scale);
                TextLabel(new Rect(0f, viewH * 0.42f, w, s.LockedSize * 1.6f), LockedTrial1, locked, InterfaceStyle.C(v.White, 0.75f), true);
                TextLabel(new Rect(0f, viewH * 0.42f + s.LockedSize * 1.6f, w, s.LockedSize * 1.6f), LockedTrial2, locked, InterfaceStyle.C(v.White, 0.55f), true);
                return;
            }
            Matrix4x4 layout = GUI.matrix;
            if (_tabFit < 1f)
                GUI.matrix = layout * Matrix4x4.Translate(new Vector3(0f, UpgradeTabsLayout.Top * (1f - _tabFit), 0f)) * Matrix4x4.Scale(new Vector3(_tabFit, _tabFit, 1f));
            GUI.matrix = GUI.matrix * Matrix4x4.Translate(new Vector3(_tabX0, 0f, 0f));
            Event e = Event.current;
            Vector2 mouse = e != null ? e.mousePosition : new Vector2(-1f, -1f); // in the plan's space under this matrix
            switch (menu.Page)
            {
                case UpgradesPage.Weapon: DrawWeaponPage(menu, mouse, v, m); break;
                case UpgradesPage.Ranks: DrawRanksPage(menu, mouse, v, m); break;
                default: DrawRackPage(menu, mouse, v, m); break;
            }
            GUI.matrix = layout;
            // The last thing tried, at the foot over the keys (as the cards' screen says it).
            string note = menu.Note.Length > 0 ? menu.Note : "";
            if (note.Length > 0)
                TextLabel(new Rect(s.EchoesMargin, viewH - s.HintBottom - s.HintHeight - 40f, 1100f, 30f), note, InterfaceStyle.Text(false, s.NoteSize, TextAnchor.MiddleLeft), NoteInk(note, v), true);
        }

        static Rect R(LayoutRect r) => new Rect(r.X, r.Y, r.Width, r.Height);

        /// <summary>A row of a list: a dark slanted plate, the name, its state on a chip at its right; the cursor's in a teal frame with the red slashes.</summary>
        void DrawTabRow(int i, Rect r, bool selected, bool hover, int nameSize, InterfaceValues v, MenuValues m)
        {
            TabItem t = _tabItems[i];
            float pivot = r.center.y, slant = v.Slant * 0.5f;
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, selected ? 0.95f : 0.7f));
            if (t.Look == 0) InterfaceStyle.Leaning(new Rect(r.x, r.y, 6f, r.height), pivot, slant, InterfaceStyle.C(v.Teal));
            if (selected) InterfaceStyle.LeaningFrame(r, pivot, slant, InterfaceStyle.C(v.Teal), 3f);
            else if (hover) InterfaceStyle.LeaningFrame(r, pivot, slant, InterfaceStyle.C(v.White, 0.5f), 2f);
            Color nameInk = t.Kind == TabRow.None ? InterfaceStyle.C(v.White, 0.7f) : t.Look == 3 ? InterfaceStyle.C(v.White, 0.65f) : InterfaceStyle.C(v.White);
            TextLabel(new Rect(r.x + 22f, r.y, r.width - 40f, r.height), t.Name, InterfaceStyle.Text(true, nameSize, TextAnchor.MiddleLeft), nameInk, true);
            if (t.State.Length > 0)
            {
                GUIStyle chip = InterfaceStyle.Text(true, m.CardTextSize + 1, TextAnchor.MiddleCenter);
                float w = t.ChipW;
                RefusalMotion(TabCard(t), out float shake, out float flash);
                var c = new Rect(r.xMax - w - 14f + shake, r.y + 8f, w, r.height - 16f);
                Color ink = t.Look == 0 ? InterfaceStyle.C(v.Teal) : t.Look == 1 ? InterfaceStyle.C(v.White) : t.Look == 2 ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.Grey);
                ink = Color.Lerp(ink, InterfaceStyle.C(v.Red), flash);
                InterfaceStyle.Slanted(c, t.Look == 0 ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Black));
                if (t.Look != 0) InterfaceStyle.SlantedFrame(c, ink, 2f);
                TextLabel(c, t.State, chip, t.Look == 0 ? InterfaceStyle.C(v.Black) : ink);
            }
            if (selected) InterfaceStyle.Slashes(r.xMax + 26f, r.y + 8f, r.height - 16f, 5f, 2, InterfaceStyle.C(v.Red));
            if (MenuHelpValues.On && TabNeed(t) > 0)
            {
                RefusalMotion(TabCard(t), out float _, out float flash2);
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, MenuHelpValues.UnaffordableVeil * (1f - flash2)));
            }
        }

        void DrawWeaponPage(StartMenu menu, Vector2 mouse, InterfaceValues v, MenuValues m)
        {
            MakeTabNumbers(menu);
            // The lists.
            Rect lists = R(UpgradeTabsLayout.Lists);
            DrawPanel(lists, WeaponPartsHead, WeaponPartsSub, InterfaceStyle.C(v.Teal), v, m);
            GUIStyle head = InterfaceStyle.Text(true, 22, TextAnchor.MiddleLeft);
            for (int slot = 0; slot < _tabSlotFirst.Count; slot++)
            {
                Rect h = R(UpgradeTabsLayout.ListHeadRect(slot, _tabRowsPerSlot));
                TextLabel(new Rect(h.x + 4f, h.y + 2f, h.width, h.height - 6f), WeaponAttachments.SlotWord((WeaponSlot)slot), head, InterfaceStyle.C(v.Teal), true);
            }
            for (int i = 0; i < _tabItems.Count; i++)
            {
                Rect r = R(PlanRow(menu, i));
                DrawTabRow(i, r, i == menu.Selected, r.Contains(mouse), 22, v, m);
            }

            // The stage: her pistol, large.
            Rect stage = R(UpgradeTabsLayout.Stage);
            InterfaceStyle.InkedPlate(stage, stage.center.y, 0f, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.White, 0.45f), 0.8f);
            WeaponStage st = WeaponStage.Current;
            if (st != null && st.Texture != null)
            {
                GUI.color = Color.white;
                GUI.DrawTexture(stage, st.Texture, ScaleMode.StretchToFill, false);
            }
            else InterfaceStyle.Fill(stage, InterfaceStyle.C(v.Black, 0.9f));
            InterfaceStyle.Fill(new Rect(stage.x, stage.y, 6f, stage.height), InterfaceStyle.C(v.Teal));
            if (st != null && st.ModelSource != WeaponStage.Source.NamedPlace && Application.isEditor)
                TextLabel(new Rect(stage.x + 18f, stage.yMax - 34f, stage.width - 36f, 26f), StandInLine, InterfaceStyle.Text(false, 16, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Grey), true);

            // The line from the row under the cursor to its part's place on the model.
            int sel = menu.Selected;
            if (st != null && sel >= 0 && sel < _tabItems.Count && st.MountInPicture(_tabItems[sel].Slot, out Vector2 at))
            {
                Rect row = R(PlanRow(menu, sel));
                var from = new Vector2(row.xMax + 40f, row.center.y);
                var to = new Vector2(stage.x + at.x * stage.width, stage.y + at.y * stage.height);
                Wire(from, to, InterfaceStyle.C(v.Teal, 0.9f), 2f);
                InterfaceStyle.Fill(new Rect(to.x - 5f, to.y - 5f, 10f, 10f), InterfaceStyle.C(v.Teal));
                InterfaceStyle.Fill(new Rect(to.x - 2f, to.y - 2f, 4f, 4f), InterfaceStyle.C(v.Black));
                InterfaceStyle.Fill(new Rect(from.x - 4f, from.y - 4f, 8f, 8f), InterfaceStyle.C(v.Teal));
            }

            // The part's panel under the stage.
            Rect panel = R(UpgradeTabsLayout.PartPanel);
            DrawPanel(panel, _panelName, "", InterfaceStyle.C(v.White, 0.6f), v, m);
            float y = panel.y + m.PanelPad + m.PanelHeadHeight + 4f, x = panel.x + m.PanelPad + 12f;
            GUIStyle word = InterfaceStyle.Text(true, 20, TextAnchor.MiddleLeft), line = InterfaceStyle.Text(false, 22, TextAnchor.MiddleLeft);
            TextLabel(new Rect(x, y, PartWordW, 32f), "GIVES", word, InterfaceStyle.C(v.Teal), true);
            TextLabel(new Rect(x + PartWordW, y, panel.width - 2f * m.PanelPad - 24f - PartWordW, 32f), _panelGives, line, InterfaceStyle.C(v.White, 0.92f), true);
            TextLabel(new Rect(x, y + 38f, PartWordW, 32f), "COSTS", word, InterfaceStyle.C(v.Red), true);
            TextLabel(new Rect(x + PartWordW, y + 38f, panel.width - 2f * m.PanelPad - 24f - PartWordW, 32f), _panelCosts, line, InterfaceStyle.C(v.White, 0.92f), true);
            if (_panelPress.Length > 0)
            {
                // The press, in the head's row at its right (the red slashes stand past it).
                float capW = _enterCapW;
                GUIStyle press = InterfaceStyle.Text(true, 20, TextAnchor.MiddleLeft);
                float w = PartPressWidth;
                var b = new Rect(panel.xMax - m.PanelPad - 56f - w, panel.y + m.PanelPad - 4f, w, m.PanelHeadHeight * 0.82f);
                InterfaceStyle.Slanted(b, _panelPressOn ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Black, 0.9f));
                if (!_panelPressOn) InterfaceStyle.SlantedFrame(b, InterfaceStyle.C(v.Grey), 2f);
                KeyCap(new Rect(b.x + 16f, b.y + (b.height - m.KeyCapHeight) * 0.5f, capW, m.KeyCapHeight), _panelPressKey, m, v, _panelPressOn);
                TextLabel(new Rect(b.x + 16f + capW + 12f, b.y, w, b.height), _panelPress, press, _panelPressOn ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.Grey));
            }

            // Her numbers at the right.
            Rect nums = R(UpgradeTabsLayout.Numbers);
            DrawPanel(nums, NumbersHead, NumbersSub, InterfaceStyle.C(v.Gold), v, m);
            GUIStyle name = InterfaceStyle.Text(false, 19, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, 24, TextAnchor.MiddleRight), change = InterfaceStyle.Text(true, 20, TextAnchor.MiddleRight);
            for (int i = 0; i < WeaponNumbers.Count; i++)
            {
                Rect r = R(UpgradeTabsLayout.NumberRow(i));
                if (i % 2 == 0) InterfaceStyle.Fill(new Rect(r.x - 6f, r.y + 3f, r.width + 12f, r.height - 6f), InterfaceStyle.C(v.White, 0.045f));
                TextLabel(new Rect(r.x + 4f, r.y, r.width * 0.58f, r.height), _numName[i], name, InterfaceStyle.C(v.White, 0.78f));
                TextLabel(new Rect(r.x, r.y, r.width * 0.78f, r.height), _numValue[i], value, InterfaceStyle.C(v.White));
                if (_numChange[i] != null && _numChange[i].Length > 0)
                    TextLabel(new Rect(r.x, r.y, r.width - 2f, r.height), _numChange[i], change, _numBetter[i] > 0 ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Red));
            }
            if (_levels != null && !_levels.AttachmentsOn)
                TextLabel(new Rect(nums.x + 6f, nums.yMax + 14f, nums.width - 6f, 60f), AttachmentsOffLine, InterfaceStyle.Text(false, 18, TextAnchor.UpperLeft, true), InterfaceStyle.C(v.Gold), true);
        }

        /// <summary>A straight line of this thickness from one point to another (a filled bar turned about its start).</summary>
        static void Wire(Vector2 from, Vector2 to, Color c, float thick)
        {
            Vector2 d = to - from;
            float length = d.magnitude;
            if (length < 1f) return;
            Matrix4x4 before = GUI.matrix;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, from);
            GUI.color = c;
            GUI.DrawTexture(new Rect(from.x, from.y - thick * 0.5f, length, thick), Texture2D.whiteTexture);
            GUI.matrix = before;
        }

        void DrawRanksPage(StartMenu menu, Vector2 mouse, InterfaceValues v, MenuValues m)
        {
            Rect panel = R(UpgradeTabsLayout.RanksPanel(_tabItems.Count));
            DrawPanel(panel, RanksPageHead, RanksPageSub, InterfaceStyle.C(v.Gold), v, m);
            if (_levels == null) return;
            MetaState s = _levels.BetweenRunsState;
            MetaRank[] table = RankTable;
            GUIStyle name = InterfaceStyle.Text(true, 38, TextAnchor.MiddleLeft), sub = InterfaceStyle.Text(false, 20, TextAnchor.MiddleLeft);
            GUIStyle gain = InterfaceStyle.Text(false, 22, TextAnchor.UpperLeft, true), chip = InterfaceStyle.Text(true, 24, TextAnchor.MiddleCenter);
            for (int i = 0; i < _tabItems.Count; i++)
            {
                TabItem t = _tabItems[i];
                MetaRank rk = table[t.Index];
                LayoutRect lr = UpgradeTabsLayout.RankRowRect(i, _tabItems.Count);
                Rect r = R(lr);
                bool selected = i == menu.Selected, hover = r.Contains(mouse);
                float pivot = r.center.y, slant = v.Slant * 0.25f;
                InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, selected ? 0.95f : 0.72f));
                if (selected) InterfaceStyle.LeaningFrame(r, pivot, slant, InterfaceStyle.C(v.Teal), 3f);
                else if (hover) InterfaceStyle.LeaningFrame(r, pivot, slant, InterfaceStyle.C(v.White, 0.5f), 2f);
                TextLabel(new Rect(r.x + 26f, r.y + 14f, UpgradeTabsLayout.RankNameWidth - 30f, 52f), t.Name, name, InterfaceStyle.C(v.White), true);
                TextLabel(new Rect(r.x + 28f, r.y + 70f, UpgradeTabsLayout.RankNameWidth - 30f, 30f), t.Sub, sub, InterfaceStyle.C(v.Grey), true);
                // The steps: filled as far as she has bought; the next one outlined in its price's colour.
                int have = MetaRanks.RankOf(s, rk), most = Mathf.Max(0, rk.Most);
                for (int k = 0; k < most; k++)
                {
                    Rect c = R(UpgradeTabsLayout.StepRect(lr, k, most));
                    c.y = r.y + (r.height - c.height) * 0.5f;
                    if (k < have) InterfaceStyle.Slanted(c, InterfaceStyle.C(v.Teal));
                    else
                    {
                        InterfaceStyle.Slanted(c, InterfaceStyle.C(v.White, 0.06f));
                        InterfaceStyle.SlantedFrame(c, k == have ? (t.Look == 2 ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.White, 0.5f)) : InterfaceStyle.C(v.White, 0.25f), k == have ? 3f : 2f);
                    }
                }
                // The next step's gain, and its price.
                float gx = r.x + UpgradeTabsLayout.RankNameWidth + UpgradeTabsLayout.RankStepsWidth + 30f;
                float gw = r.xMax - UpgradeTabsLayout.RankPriceWidth - 30f - gx;
                TextLabel(new Rect(gx, r.y + 24f, gw, r.height - 36f), RankGainWords(i, s, rk), gain, InterfaceStyle.C(v.White, 0.9f), true);
                RefusalMotion(TabCard(t), out float shake, out float flash);
                var pc = new Rect(r.xMax - UpgradeTabsLayout.RankPriceWidth - 10f + shake, r.y + (r.height - 50f) * 0.5f, UpgradeTabsLayout.RankPriceWidth - 10f, 50f);
                Color ink = t.Look == 0 ? InterfaceStyle.C(v.Teal) : t.Look == 2 ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.Grey);
                ink = Color.Lerp(ink, InterfaceStyle.C(v.Red), flash);
                InterfaceStyle.Slanted(pc, t.Look == 0 ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.Black));
                if (t.Look != 0) InterfaceStyle.SlantedFrame(pc, ink, 2f);
                TextLabel(pc, t.State, chip, t.Look == 0 ? InterfaceStyle.C(v.Black) : ink);
                if (selected) InterfaceStyle.Slashes(r.xMax - 4f, r.y + 10f, 26f, 6f, 2, InterfaceStyle.C(v.Red));
                if (MenuHelpValues.On && TabNeed(t) > 0) InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, MenuHelpValues.UnaffordableVeil * 0.6f * (1f - flash)));
            }
        }

        readonly List<string> _rankGain = new List<string>();
        int _rankGainFor = int.MinValue;

        /// <summary>A rank row's gain in plain words ("Next: +10 % of her own health.  Now +30 %."), made with the rows.</summary>
        string RankGainWords(int i, MetaState s, MetaRank rk)
        {
            if (_rankGainFor != _tabsMadeFor)
            {
                _rankGainFor = _tabsMadeFor;
                _rankGain.Clear();
                MetaRank[] table = RankTable;
                foreach (TabItem t in _tabItems) _rankGain.Add(t.Kind == TabRow.Rank ? MetaRanks.NextWords(s, table[t.Index]).Replace("  Now ", "\nNow ") : ""); // what she has now on a line of its own
            }
            return i < _rankGain.Count ? _rankGain[i] : "";
        }

        void DrawRackPage(StartMenu menu, Vector2 mouse, InterfaceValues v, MenuValues m)
        {
            Rect list = R(UpgradeTabsLayout.RackList(_tabItems.Count));
            DrawPanel(list, RackPageHead, RackPageSub, InterfaceStyle.C(v.Teal), v, m);
            for (int i = 0; i < _tabItems.Count; i++)
            {
                Rect r = R(UpgradeTabsLayout.RackRowRect(i));
                DrawTabRow(i, r, i == menu.Selected, r.Contains(mouse), 23, v, m);
            }
            int sel = menu.Selected;
            if (sel < 0 || sel >= _tabItems.Count || _levels == null) return;
            MakeRackPanel(sel);
            Rect panel = R(UpgradeTabsLayout.RackPanel);
            TabItem t = _tabItems[sel];
            DrawPanel(panel, "THE PIECE", t.Sub, t.Look == 0 ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White, 0.6f), v, m);
            float x = panel.x + m.PanelPad + 24f, y = panel.y + m.PanelPad + m.PanelHeadHeight + 20f, w = panel.width - 2f * m.PanelPad - 60f;
            GUIStyle big = InterfaceStyle.Text(true, 60, TextAnchor.MiddleLeft);
            float nameW = _rackNameW;
            InterfaceStyle.Slanted(new Rect(x - 6f, y + 4f, Mathf.Min(nameW + 40f, w), 76f), InterfaceStyle.C(v.White, t.Look == 3 ? 0.75f : 1f));
            TextLabel(new Rect(x + 8f, y, w, 84f), t.Name, big, InterfaceStyle.C(v.Black));
            y += 104f;
            TextLabel(new Rect(x, y, w, 32f), _rackKind, InterfaceStyle.Text(true, 24, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal), true);
            y += 50f;
            GUIStyle text = InterfaceStyle.Text(false, 28, TextAnchor.UpperLeft, true);
            float th = _rackLineH;
            TextLabel(new Rect(x, y, w, th + 8f), _rackLine, text, InterfaceStyle.C(v.White, 0.94f), true);
            y += th + 28f;
            Color stateInk = t.Look == 0 ? InterfaceStyle.C(v.Teal) : t.Look == 1 ? InterfaceStyle.C(v.White) : t.Look == 2 ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.Grey);
            TextLabel(new Rect(x, y, w, 64f), _rackState, InterfaceStyle.Text(false, 24, TextAnchor.UpperLeft, true), stateInk, true);
            // The press, at the panel's foot.
            float capW = _enterCapW;
            GUIStyle press = InterfaceStyle.Text(true, 26, TextAnchor.MiddleLeft);
            float bw = capW + 28f + _rackPressW + 36f;
            var b = new Rect(panel.xMax - m.PanelPad - 30f - bw, panel.yMax - m.PanelPad - 66f, bw, 54f);
            RefusalMotion(TabCard(t), out float shake, out float flash);
            b.x += shake;
            InterfaceStyle.Slanted(b, _rackPressOn ? InterfaceStyle.C(v.Teal) : Color.Lerp(InterfaceStyle.C(v.Black, 0.9f), InterfaceStyle.C(v.Red), flash));
            if (!_rackPressOn) InterfaceStyle.SlantedFrame(b, InterfaceStyle.C(v.Grey), 2f);
            KeyCap(new Rect(b.x + 18f, b.y + (b.height - m.KeyCapHeight) * 0.5f, capW, m.KeyCapHeight), "Enter", m, v, _rackPressOn);
            TextLabel(new Rect(b.x + 18f + capW + 14f, b.y, bw, b.height), _rackPress, press, _rackPressOn ? InterfaceStyle.C(v.Black) : InterfaceStyle.C(v.Grey));
        }

        int _rackPanelFor = int.MinValue, _rackPanelSel = -1;
        string _rackKind = "", _rackLine = "", _rackState = "", _rackPress = "";
        bool _rackPressOn;
        float _rackNameW, _rackLineH, _rackPressW;

        /// <summary>The rack's large panel's words for the piece under the cursor, made when it or what it says changed.</summary>
        void MakeRackPanel(int sel)
        {
            if (_rackPanelFor == _tabsMadeFor && _rackPanelSel == sel) return;
            _rackPanelFor = _tabsMadeFor;
            _rackPanelSel = sel;
            MetaState s = _levels.BetweenRunsState;
            MetaValues mv = _levels.MetaValues;
            MetaPiece p = Pieces[_tabItems[sel].Index];
            string currency = mv.CurrencyName;
            _rackKind = (p.Kind == MetaPieceKind.Stats ? "A STATS PIECE" : "A RULE PIECE") + "  ·  " + p.Points.ToString(s_Inv139) + (p.Points == 1 ? " POINT" : " POINTS") + " OF THE BUDGET TO EQUIP";
            _rackLine = p.Effect == MetaEffect.StrikeOut ? p.Line + "  Struck out now: " + _levels.StruckOutSkill + " (changed in START RUN's loadout)." : p.Line;
            MetaBuy buy = MetaRack.CanBuy(s, mv, p.Id);
            _rackPressOn = false;
            if (s.IsEquipped(p.Id))
            {
                _rackState = "Owned and equipped: it goes into the next run.";
                _rackPress = "OWNED";
            }
            else if (s.Owns(p.Id))
            {
                _rackState = "Owned: put it on in START RUN's loadout.";
                _rackPress = "OWNED";
            }
            else if (buy == MetaBuy.NotForSale)
            {
                _rackState = "Not for sale: it comes with " + MetaFeats.Words(p.FromFeat) + ".";
                _rackPress = "NOT FOR SALE";
            }
            else if (buy == MetaBuy.NeedsFirst)
            {
                _rackState = "Buy " + (MetaRack.Find(mv, p.Needs)?.Name ?? "?") + " first, then it is " + p.Price.ToString(s_Inv139) + " " + currency + ".";
                _rackPress = "NEEDS " + (MetaRack.Find(mv, p.Needs)?.Name ?? "?").ToUpperInvariant() + " FIRST";
            }
            else if (buy == MetaBuy.NotEnough)
            {
                _rackState = "For sale: " + p.Price.ToString(s_Inv139) + " " + currency + ". " + MenuHelpRules.Need(p.Price, s.Currency).ToString(s_Inv139) + " more needed.";
                _rackPress = "BUY  ·  " + p.Price.ToString(s_Inv139) + " " + currency.ToUpperInvariant();
            }
            else
            {
                _rackState = "For sale: " + p.Price.ToString(s_Inv139) + " " + currency + ".";
                _rackPress = "BUY  ·  " + p.Price.ToString(s_Inv139) + " " + currency.ToUpperInvariant();
                _rackPressOn = true;
            }
            // Measured once with its words.
            MenuValues mm = InterfaceStyle.V.Menus;
            float w = UpgradeTabsLayout.RackPanel.Width - 2f * mm.PanelPad - 60f;
            _rackNameW = InterfaceStyle.Measure(_tabItems[sel].Name, InterfaceStyle.Text(true, 60, TextAnchor.MiddleLeft)).x;
            _rackLineH = InterfaceStyle.MeasureHeight(_rackLine, InterfaceStyle.Text(false, 28, TextAnchor.UpperLeft, true), w);
            _rackPressW = InterfaceStyle.Measure(_rackPress, InterfaceStyle.Text(true, 26, TextAnchor.MiddleLeft)).x;
        }

        // ---------------- Measured ----------------

        /// <summary>What of the page's words does not fit inside its shape; empty when all does. For tests.</summary>
        public string TabsTextOutside(StartMenu menu)
        {
            EnsureTabItems(menu);
            var problems = new List<string>();
            if (_tabsLocked) return "";
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            void Fits(string what, float need, float room)
            {
                if (need > room) problems.Add(what + " (" + need.ToString("0", s_Inv139) + " in " + room.ToString("0", s_Inv139) + ")");
            }
            GUIStyle chip = InterfaceStyle.Text(true, m.CardTextSize + 1, TextAnchor.MiddleCenter);
            switch (menu.Page)
            {
                case UpgradesPage.Weapon:
                    MakeTabNumbers(menu);
                    GUIStyle row = InterfaceStyle.Text(true, 22, TextAnchor.MiddleLeft);
                    for (int i = 0; i < _tabItems.Count; i++)
                    {
                        LayoutRect r = PlanRow(menu, i);
                        Fits("the row " + _tabItems[i].Name + " is wider than its place", 22f + InterfaceStyle.Measure(_tabItems[i].Name, row).x + 20f + (_tabItems[i].State.Length > 0 ? InterfaceStyle.Measure(_tabItems[i].State, chip).x + 36f : 0f), r.Width);
                    }
                    LayoutRect numbers = UpgradeTabsLayout.Numbers;
                    GUIStyle name = InterfaceStyle.Text(false, 19, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, 24, TextAnchor.MiddleRight), change = InterfaceStyle.Text(true, 20, TextAnchor.MiddleRight);
                    float nw = numbers.Width - 36f;
                    for (int i = 0; i < WeaponNumbers.Count; i++)
                    {
                        Fits("the number " + _numName[i] + "'s name runs into its value", 4f + InterfaceStyle.Measure(_numName[i], name).x + 8f + InterfaceStyle.Measure("0.000 s", value).x, nw * 0.78f);
                        Fits("the number " + _numName[i] + "'s value runs into its change", InterfaceStyle.Measure("-0.000 s", change).x + 8f, nw * 0.22f);
                    }
                    LayoutRect panel = UpgradeTabsLayout.PartPanel;
                    GUIStyle line = InterfaceStyle.Text(false, 22, TextAnchor.MiddleLeft);
                    float room = panel.Width - 2f * m.PanelPad - 24f - PartWordW;
                    foreach (WeaponAttachment p in _levels.MetaValues.Parts)
                    {
                        if (!WeaponRack.InStore(_levels.MetaValues, p)) continue;
                        Fits("the part " + p.Name + "'s gain is wider than its panel", InterfaceStyle.Measure(p.Line, line).x, room);
                        Fits("the part " + p.Name + "'s cost is wider than its panel", InterfaceStyle.Measure(p.CostLine, line).x, room);
                    }
                    if (_panelPress.Length > 0)
                        Fits("the part's press runs into its name", m.PanelPad + 8f + InterfaceStyle.Measure(_panelName, InterfaceStyle.Text(true, m.PanelHeadSize, TextAnchor.MiddleLeft)).x + 24f + PartPressWidth + 56f + m.PanelPad, panel.Width);
                    break;
                case UpgradesPage.Ranks:
                    GUIStyle rn = InterfaceStyle.Text(true, 38, TextAnchor.MiddleLeft), gain = InterfaceStyle.Text(false, 22, TextAnchor.UpperLeft, true), price = InterfaceStyle.Text(true, 24, TextAnchor.MiddleCenter);
                    LayoutRect rr = UpgradeTabsLayout.RankRowRect(0, _tabItems.Count);
                    float gw = rr.Width - UpgradeTabsLayout.RankNameWidth - UpgradeTabsLayout.RankStepsWidth - 30f - UpgradeTabsLayout.RankPriceWidth - 30f;
                    MetaRank[] table = RankTable;
                    for (int i = 0; i < _tabItems.Count; i++)
                    {
                        Fits("the rank " + _tabItems[i].Name + "'s name is wider than its place", InterfaceStyle.Measure(_tabItems[i].Name, rn).x, UpgradeTabsLayout.RankNameWidth - 30f);
                        Fits("the rank " + _tabItems[i].Name + "'s gain is taller than its row", InterfaceStyle.MeasureHeight(RankGainWords(i, _levels.BetweenRunsState, table[_tabItems[i].Index]), gain, gw), rr.Height - 36f);
                        Fits("the rank " + _tabItems[i].Name + "'s price is wider than its chip", InterfaceStyle.Measure(_tabItems[i].State, price).x + 16f, UpgradeTabsLayout.RankPriceWidth - 10f);
                    }
                    if (UpgradeTabsLayout.RanksPanel(_tabItems.Count).Bottom > UpgradeTabsLayout.Bottom) problems.Add("the ranks run into the foot");
                    break;
                default:
                    GUIStyle pr = InterfaceStyle.Text(true, 23, TextAnchor.MiddleLeft);
                    for (int i = 0; i < _tabItems.Count; i++)
                        Fits("the piece " + _tabItems[i].Name + " is wider than its row", 22f + InterfaceStyle.Measure(_tabItems[i].Name, pr).x + 20f + InterfaceStyle.Measure(_tabItems[i].State, chip).x + 36f, UpgradeTabsLayout.RackRowRect(i).Width);
                    if (UpgradeTabsLayout.RackList(_tabItems.Count).Bottom > UpgradeTabsLayout.Bottom) problems.Add("the rack's list runs into the foot");
                    LayoutRect big = UpgradeTabsLayout.RackPanel;
                    float w = big.Width - 2f * m.PanelPad - 60f;
                    GUIStyle text = InterfaceStyle.Text(false, 28, TextAnchor.UpperLeft, true);
                    for (int i = 0; i < _tabItems.Count; i++)
                    {
                        MakeRackPanel(i);
                        Fits("the piece " + _tabItems[i].Name + "'s name is wider than its panel", InterfaceStyle.Measure(_tabItems[i].Name, InterfaceStyle.Text(true, 60, TextAnchor.MiddleLeft)).x + 40f, w);
                        Fits("the piece " + _tabItems[i].Name + "'s kind is wider than its panel", InterfaceStyle.Measure(_rackKind, InterfaceStyle.Text(true, 24, TextAnchor.MiddleLeft)).x, w);
                        float textH = InterfaceStyle.MeasureHeight(_rackLine, text, w);
                        Fits("the piece " + _tabItems[i].Name + "'s words run into its press", m.PanelPad + m.PanelHeadHeight + 20f + 104f + 50f + textH + 28f + 64f, big.Height - m.PanelPad - 66f);
                    }
                    _rackPanelSel = -1;
                    break;
            }
            return string.Join("; ", problems);
        }
    }
}
