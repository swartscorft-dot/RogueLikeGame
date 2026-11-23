using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// Her sheet on Tab (work order 095, 2026-11-23, Kristian after three runs: "tab knappen eller escape viser alle stats som
    /// er relevant å vise. også når man hovrer spells i tab/escape. så ser man stats"; and "har vi scaling på spells?": he did not
    /// know a skill takes her flat damage by its weight), as part of <see cref="RunHud"/>, drawn in work order 078's menus'
    /// language (<see cref="InterfaceStyle"/>'s plates, outlines, dots, slashes and type; no second style).
    ///
    /// The sheet key (<see cref="InputBindings.Sheet"/>, Tab) opens it, and it or Esc closes it. The game stands still under it
    /// through the pause menu's own path (<see cref="GeneratedLevels.Pause"/>: the time scale, the held input, the recorder's
    /// screen time, which counts it as "sheet"); from the pause menu the key turns the menu into the sheet. It is not offered
    /// under a choice, the end screen or the screen between runs. A veil over the game and compact panels: "STATS" on the red
    /// plate, her stats in their four groups on a dark panel (<see cref="HerSheet.Stats"/>: the value now, its change since the
    /// run's start in teal, MAX in gold at a cap), and beside them her skills as tiles in the bar's order with their key caps,
    /// her basic shot first; hovering one (or stepping to it with the arrow keys) opens its panel under the row
    /// (<see cref="HerSheet.Skill"/>: its numbers as she has them now, the weight said plainly).
    ///
    /// Its lists are made when it opens and when the hovered skill changes (<see cref="SheetListsMade"/>,
    /// <see cref="SheetPanelsMade"/>), its layout when they or the screen change; a frame with it open and the mouse still makes
    /// nothing. The pause menu's "HER STATS" panel draws the same lines with the same code (<see cref="StatGroups"/>).
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The sheet is up now (the game paused under it).</summary>
        public bool SheetUp { get; private set; }
        /// <summary>The skill hovered or stepped to on the sheet's row: 0 her basic shot, then her skills in the bar's order; -1 none.</summary>
        public int SheetSelected { get; private set; } = -1;
        /// <summary>How many times the sheet's lists were made (when it opens). For tests.</summary>
        public int SheetListsMade { get; private set; }
        /// <summary>How many times a skill's panel was made (when the hovered skill changes). For tests.</summary>
        public int SheetPanelsMade { get; private set; }
        /// <summary>Her stats as the sheet and the pause menu's panel draw them. For tests.</summary>
        public IReadOnlyList<SheetStat> SheetStats => _sheetStats;
        /// <summary>The row's tiles: their skills' names and their keys, her basic shot first. For tests.</summary>
        public IReadOnlyList<string> SheetTileNames => _tileName;
        public IReadOnlyList<string> SheetTileKeys => _tileKey;
        /// <summary>The panel of the skill hovered, or null with none. For tests.</summary>
        public SheetSkill SheetPanel => _panel;

        readonly List<SheetStat> _sheetStats = new List<SheetStat>();
        readonly List<string> _tileName = new List<string>(), _tileKey = new List<string>(), _tileShort = new List<string>();
        readonly List<int> _tileSlot = new List<int>(); // the skill's place in her list; -1 the basic shot
        SheetSkill _panel;
        RunModifiers _practiceModifiers;
        bool _sheetInRun;

        // ---------------- Intents ----------------

        /// <summary>The sheet may open now: a character, and no choice, end screen or screen between runs up.</summary>
        bool SheetOffered => _levels != null && _player != null && _levels.Choice == GeneratedLevels.ChoiceKind.None
                             && _levels.Ending == GeneratedLevels.EndKind.None && !_levels.BetweenRunsUp;

        /// <summary>Opens the sheet: pauses the game as the pause menu does (or turns the pause menu into the sheet). False when it may not open.</summary>
        public bool OpenSheet()
        {
            Find();
            if (SheetUp || !SheetOffered) return false;
            if (!_levels.Paused && !_levels.Pause()) return false;
            SheetUp = true;
            SheetSelected = -1;
            MakeSheetLists();
            return true;
        }

        /// <summary>Closes the sheet and gives the game back. False when it is not up.</summary>
        public bool CloseSheet()
        {
            if (!SheetUp) return false;
            SheetUp = false;
            if (_levels != null && _levels.Paused) _levels.Resume();
            return true;
        }

        /// <summary>The sheet key: opens it, or closes it when up.</summary>
        public bool ToggleSheet() => SheetUp ? CloseSheet() : OpenSheet();

        /// <summary>Hovers a skill of the row (0 her basic shot): its panel is made if it changed. False when the sheet is not up or there is no such tile.</summary>
        public bool HoverSheetSkill(int index)
        {
            if (!SheetUp || index < -1 || index >= _tileName.Count) return false;
            if (index == SheetSelected) return true;
            SheetSelected = index;
            MakeSheetPanel();
            return true;
        }

        /// <summary>The arrow keys on the sheet: the next tile along the row (round at its ends); from none, the first or the last.</summary>
        public bool StepSheetSkill(int delta)
        {
            if (!SheetUp || _tileName.Count == 0) return false;
            int n = _tileName.Count;
            int next = SheetSelected < 0 ? (delta >= 0 ? 0 : n - 1) : ((SheetSelected + delta) % n + n) % n;
            return HoverSheetSkill(next);
        }

        /// <summary>The sheet's keys in a frame with it up: the arrows step along the row (the sheet key and Esc are read before).</summary>
        void SheetKeys(Keyboard keyboard)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.upArrowKey.wasPressedThisFrame) StepSheetSkill(-1);
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.downArrowKey.wasPressedThisFrame) StepSheetSkill(1);
        }

        /// <summary>The key that opens the sheet as bound now (Tab by default; an asset made before it has none written and gets Tab).</summary>
        Key SheetKey => _driver != null && _driver.ActiveBindings != null ? _driver.ActiveBindings.Sheet : Key.Tab;

        // ---------------- The lists, made when it opens and when the hovered skill changes ----------------

        /// <summary>
        /// Her values now for the sheet's numbers: the run's modifiers with her picks (outside a run, a run's start: a bare list
        /// with the run's switch and numbers), her feel, her kit, her focus, her health.
        /// </summary>
        OfferContext SheetContext()
        {
            RunModifiers m = _player != null ? _player.Modifiers : null;
            if (m == null)
            {
                RunValues run = _levels != null ? _levels.RunValues : new RunValues();
                if (_practiceModifiers == null) _practiceModifiers = new RunModifiers();
                _practiceModifiers.StatsAndTiers = run.StatsAndTiers;
                _practiceModifiers.StatValues = run.Stats;
                _practiceModifiers.CooldownFloor = run.CooldownFloor;
                m = _practiceModifiers;
            }
            return new OfferContext
            {
                Modifiers = m,
                Run = _levels != null ? _levels.RunValues : new RunValues(),
                Feel = _player != null ? _player.Feel : new FeelValues(),
                Kit = _player != null ? _player.Kit : new KitValues(),
                Identity = _player != null ? _player.Identity : new IdentityValues(),
                FocusValues = _player != null ? _player.FocusModeValues : new FocusModeValues(),
                FocusMode = _player != null ? _player.FocusMode : FocusMode.Today,
                OwnMaxHealth = _health != null ? _health.MaxHealth : Placeholders.PlayerHealth,
                Health = _health != null ? _health.Current : Placeholders.PlayerHealth,
            };
        }

        /// <summary>Her stats' lines, the sheet's and the pause menu's one list (<see cref="HerSheet.Stats"/>), with her pistols' parts' line in a run where they show.</summary>
        void MakeSheetStats()
        {
            _sheetStats.Clear();
            if (_player == null) return;
            RunModifiers m = _player.Modifiers;
            string loadout = m != null && _levels != null && _levels.InRun && (m.Weapon.Any || _levels.AttachmentsOn)
                ? WeaponAttachments.WeaponName(WeaponKind.Pistols) + ": " + (m.Weapon.Any ? string.Join(", ", m.Weapon.Parts) : "stock")
                : null; // work order 060's line, as the pause menu's list had it
            _sheetStats.AddRange(HerSheet.Stats(SheetContext(), loadout));
            _sheetInRun = _levels != null && _levels.InRun;
        }

        void MakeSheetLists()
        {
            MakeSheetStats();
            _tileName.Clear();
            _tileKey.Clear();
            _tileShort.Clear();
            _tileSlot.Clear();
            InputBindings b = _driver != null ? _driver.ActiveBindings : null;
            AddTile(HerSheet.BasicShotName, b != null ? b.Shoot.Label : "LMB", -1);
            IReadOnlyList<SkillDefinition> skills = _player != null ? _player.Skills : null;
            int count = skills != null && b != null && b.Skills != null ? Mathf.Min(_player.KeyCount, b.Skills.Length) : 0;
            for (int key = 0; key < count; key++)
            {
                int slot = _player.SkillOnKey(key);
                if (slot < 0 || slot >= skills.Count || skills[slot] == null) continue; // an empty key has no tile
                AddTile(skills[slot].Name, b.SkillLabel(key), slot);
            }
            if (SheetSelected >= _tileName.Count) SheetSelected = -1;
            SheetListsMade++;
            MakeSheetPanel();
        }

        void AddTile(string name, string key, int slot)
        {
            _tileName.Add(name);
            _tileKey.Add(key);
            _tileShort.Add(name.Length <= 6 ? name.ToUpperInvariant() : name.Substring(0, 5).ToUpperInvariant());
            _tileSlot.Add(slot);
        }

        void MakeSheetPanel()
        {
            _panel = null;
            _sheetLaidOutFor = -1;
            if (SheetSelected < 0 || SheetSelected >= _tileName.Count || _player == null) return;
            OfferContext c = SheetContext();
            int slot = _tileSlot[SheetSelected];
            if (slot < 0) _panel = HerSheet.Skill(null, _tileKey[SheetSelected], c, HerSheet.TakenFor(null, _player.Modifiers));
            else
            {
                SkillDefinition skill = _player.Skills[slot];
                RunSkillUpgrades ups = _player.SkillUpgrades;
                _panel = HerSheet.Skill(skill, _tileKey[SheetSelected], c, HerSheet.TakenFor(skill.Name, _player.Modifiers, ups != null ? ups.NamesFor(skill.Name) : null),
                                        ups != null ? ups.TierOf(skill.Name) : UpgradeTier.Common);
            }
            SheetPanelsMade++;
        }

        // ---------------- Her stats in their groups: one code for the sheet and the pause menu ----------------

        /// <summary>
        /// Her stats laid out in two columns of groups (<see cref="HerSheet.ColumnOf"/>): each group's heading, then its lines;
        /// a line too wide for a column on a whole line after both. Worked out when the lines or the screen change; drawn from
        /// what it worked out. The sheet and the pause menu each keep one, at their own column's width.
        /// </summary>
        sealed class StatGroups
        {
            public readonly List<int> Col = new List<int>();
            public readonly List<float> Y = new List<float>(), ValueW = new List<float>(), Width = new List<float>();
            public readonly List<SheetGroup> HeadGroup = new List<SheetGroup>();
            public readonly List<int> HeadCol = new List<int>();
            public readonly List<float> HeadY = new List<float>();
            public float Height, ColW, CapSlot, CapW;

            public void LayOut(IReadOnlyList<SheetStat> stats, float colW, MenuValues m, SheetValues sv)
            {
                ColW = colW;
                Col.Clear();
                Y.Clear();
                ValueW.Clear();
                Width.Clear();
                HeadGroup.Clear();
                HeadCol.Clear();
                HeadY.Clear();
                Height = 0f;
                if (stats == null || stats.Count == 0) return;
                GUIStyle name = InterfaceStyle.Text(false, m.StatNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, m.StatValueSize, TextAnchor.MiddleRight),
                         change = InterfaceStyle.Text(true, m.StatChangeSize, TextAnchor.MiddleRight);
                bool anyCap = false;
                foreach (SheetStat s in stats) anyCap |= s.AtCap;
                CapW = InterfaceStyle.Measure(HerSheet.CapWord, InterfaceStyle.Text(true, sv.CapSize, TextAnchor.MiddleCenter)).x + 14f;
                CapSlot = anyCap ? CapW + 8f : 0f;
                foreach (SheetStat s in stats)
                {
                    float vw = InterfaceStyle.Measure(s.Value, value).x;
                    ValueW.Add(vw);
                    float cw = ShowsChange(s) ? InterfaceStyle.Measure(s.Change, change).x + 10f : 0f;
                    Width.Add(InterfaceStyle.Measure(s.Name, name).x + 16f + cw + vw + CapSlot);
                    Col.Add(0);
                    Y.Add(0f);
                }
                float y0 = 0f, y1 = 0f;
                foreach (SheetGroup g in HerSheet.Order)
                {
                    int col = HerSheet.ColumnOf(g);
                    bool head = false;
                    for (int i = 0; i < stats.Count; i++)
                    {
                        if (stats[i].Group != g || Width[i] > colW) continue;
                        float y = col == 0 ? y0 : y1;
                        if (!head)
                        {
                            HeadGroup.Add(g);
                            HeadCol.Add(col);
                            HeadY.Add(y);
                            y += sv.GroupHead;
                            head = true;
                        }
                        Col[i] = col;
                        Y[i] = y;
                        y += m.StatLine;
                        if (col == 0) y0 = y;
                        else y1 = y;
                    }
                }
                float whole = Mathf.Max(y0, y1);
                for (int i = 0; i < stats.Count; i++)
                {
                    if (Width[i] <= colW) continue;
                    Col[i] = -1;
                    Y[i] = whole;
                    whole += m.StatLine;
                }
                Height = whole;
            }

            /// <summary>A change is written beside its value unless it says the same (round 1: "Damage  +9  +9"); the value is teal either way.</summary>
            static bool ShowsChange(SheetStat s) => s.Changed && s.Change.Length > 0 && s.Change != s.Value;

            /// <summary>What does not fit in its place, as words; empty when all does.</summary>
            public void Outside(IReadOnlyList<SheetStat> stats, float inner, List<string> problems)
            {
                for (int i = 0; i < stats.Count && i < Width.Count; i++)
                    if (Width[i] > (Col[i] == -1 ? inner : ColW)) problems.Add("stat " + stats[i].Name + " is wider than its place");
            }

            /// <summary>The groups and lines from (<paramref name="x0"/>, <paramref name="top"/>) in a panel leaning by <paramref name="slant"/> about <paramref name="pivot"/>.</summary>
            public void Draw(IReadOnlyList<SheetStat> stats, float x0, float top, float pivot, float slant, float gap, MenuValues m, SheetValues sv, InterfaceValues v)
            {
                GUIStyle name = InterfaceStyle.Text(false, m.StatNameSize, TextAnchor.MiddleLeft), value = InterfaceStyle.Text(true, m.StatValueSize, TextAnchor.MiddleRight),
                         change = InterfaceStyle.Text(true, m.StatChangeSize, TextAnchor.MiddleRight), headStyle = InterfaceStyle.Text(true, sv.GroupHeadSize, TextAnchor.MiddleLeft),
                         cap = InterfaceStyle.Text(true, sv.CapSize, TextAnchor.MiddleCenter);
                float inner = ColW * 2f + gap;
                for (int h = 0; h < HeadGroup.Count; h++)
                {
                    float y = top + HeadY[h], lean = (pivot - (y + sv.GroupHead * 0.5f)) * slant;
                    float x = x0 + lean + (HeadCol[h] == 1 ? ColW + gap : 0f);
                    string word = HerSheet.GroupWord(HeadGroup[h]);
                    TextLabel(new Rect(x, y + 2f, ColW, sv.GroupHead - 4f), word, headStyle, InterfaceStyle.C(v.Teal));
                    InterfaceStyle.Slanted(new Rect(x, y + sv.GroupHead - 5f, ColW, 2f), InterfaceStyle.C(v.Teal, 0.35f));
                }
                for (int i = 0; i < stats.Count && i < Y.Count; i++)
                {
                    SheetStat s = stats[i];
                    float y = top + Y[i], lean = (pivot - (y + m.StatLine * 0.5f)) * slant;
                    float x = x0 + lean + (Col[i] == 1 ? ColW + gap : 0f), w = (Col[i] == -1 ? inner : ColW) - CapSlot;
                    TextLabel(new Rect(x, y, w, m.StatLine), s.Name, name, InterfaceStyle.C(v.White, 0.72f));
                    // No dark copy under the value (round 2: with the sheet's 24 lines the pause menu's frame came to 8919 B against
                    // its ceiling of 9000, the engine's own 36 B a label drawn; the panel is dark enough for the heavy white).
                    TextLabel(new Rect(x, y, w, m.StatLine), s.Value, value, s.Changed ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
                    if (ShowsChange(s))
                        TextLabel(new Rect(x, y, w - ValueW[i] - 10f, m.StatLine), s.Change, change, InterfaceStyle.C(v.Teal, 0.8f));
                    if (s.AtCap)
                    {
                        var c = new Rect(x + w + 8f, y + 5f, CapW, m.StatLine - 10f);
                        InterfaceStyle.Slanted(c, InterfaceStyle.C(v.Gold));
                        TextLabel(c, HerSheet.CapWord, cap, InterfaceStyle.C(v.Black));
                    }
                }
            }
        }

        // ---------------- The sheet's layout, worked out once for its lists and the screen ----------------

        readonly StatGroups _sheetGroups = new StatGroups();
        int _sheetLaidOutFor = -1;
        Vector2Int _sheetLaidOutAt;
        float _sFit = 1f, _sViewWidth, _sX0, _sStatsH, _sSkillsH, _sPanelH, _sColW;
        readonly List<float> _sLineH = new List<float>(), _sKeyW = new List<float>();
        float _sSentenceH, _sTakenH, _sNameW, _sTierW, _sKeyCapW;
        string _sTaken = "";
        readonly float[] _sHintW = new float[s_SheetHint.Length];
        static readonly string[] s_SheetHint = { "Tab", "Esc", "close", "<", ">", "pick a skill", "or hover one with the mouse" };
        static readonly bool[] s_SheetHintCap = { true, true, false, true, true, false, false };
        const string SheetStatsSub = "this run   ·   teal: changed since its start", SheetStatsSubStart = "a run's start   ·   no run going";
        const string SkillsHead = "HER SKILLS", SkillsSub = "hover one for its numbers", TakenHead = "TAKEN FOR IT";

        int SheetVersion => SheetListsMade * 4096 + SheetPanelsMade;

        GUIStyle SheetValueStyle(SheetValues sv) => InterfaceStyle.Text(false, sv.ValueSize, TextAnchor.UpperLeft, true);
        GUIStyle SheetSentenceStyle(SheetValues sv) => InterfaceStyle.Text(false, sv.SentenceSize, TextAnchor.UpperLeft, true);

        float PanelValueWidth(MenuValues m, SheetValues sv) => sv.SkillsWidth - 2f * m.PanelPad - 24f - sv.LabelWidth;
        float PanelInnerWidth(MenuValues m, SheetValues sv) => sv.SkillsWidth - 2f * m.PanelPad - 24f;

        void LayOutSheet()
        {
            var at = new Vector2Int(InterfaceStyle.ScreenWidth, InterfaceStyle.ScreenHeight);
            if (_sheetLaidOutFor == SheetVersion && _sheetLaidOutAt == at) return;
            _sheetLaidOutFor = SheetVersion;
            _sheetLaidOutAt = at;
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            SheetValues sv = m.Sheet;
            _sColW = (sv.StatsWidth - 2f * m.PanelPad - 12f - m.ColumnInnerGap) * 0.5f;
            _sheetGroups.LayOut(_sheetStats, _sColW, m, sv);
            _sStatsH = 2f * m.PanelPad + m.PanelHeadHeight + _sheetGroups.Height;
            _sSkillsH = 2f * m.PanelPad + m.PanelHeadHeight + sv.Tile + sv.TileCap + 10f;
            GUIStyle cap = CapStyle(m);
            _sKeyW.Clear();
            foreach (string k in _tileKey) _sKeyW.Add(Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(k, cap).x + 18f));
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < s_SheetHint.Length; i++)
                _sHintW[i] = s_SheetHintCap[i] ? Mathf.Max(m.KeyCapHeight * 1.2f, InterfaceStyle.Measure(s_SheetHint[i], cap).x + 16f) : InterfaceStyle.Measure(s_SheetHint[i], words).x;

            // The panel of the skill hovered: each line as tall as its value wraps to.
            _sLineH.Clear();
            _sPanelH = 0f;
            if (_panel != null)
            {
                float vw = PanelValueWidth(m, sv), inner = PanelInnerWidth(m, sv);
                _sNameW = InterfaceStyle.Measure(_panel.Name, InterfaceStyle.Text(true, sv.NameSize, TextAnchor.MiddleLeft)).x;
                _sTierW = _panel.Tier.Length > 0 ? InterfaceStyle.Measure(_panel.Tier.ToUpperInvariant(), InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter)).x + 20f : 0f;
                _sKeyCapW = _panel.Key.Length > 0 ? Mathf.Max(m.KeyCapHeight * 1.35f, InterfaceStyle.Measure(_panel.Key, cap).x + 18f) : 0f;
                _sSentenceH = InterfaceStyle.MeasureHeight(_panel.Sentence, SheetSentenceStyle(sv), inner) + 8f;
                float h = m.PanelPad + sv.NameSize * 1.5f + 8f + _sSentenceH + 6f;
                foreach (SheetLine l in _panel.Lines)
                {
                    float lh = Mathf.Max(sv.ValueSize * 1.45f, InterfaceStyle.MeasureHeight(l.Value, SheetValueStyle(sv), vw) + 4f);
                    _sLineH.Add(lh);
                    h += lh;
                }
                _sTaken = _panel.Taken.Count > 0 ? string.Join(",  ", _panel.Taken) : "nothing yet";
                _sTakenH = sv.GroupHead + InterfaceStyle.MeasureHeight(_sTaken, SheetValueStyle(sv), inner) + 4f;
                h += 10f + _sTakenH + m.PanelPad;
                _sPanelH = h;
            }

            float total = sv.StatsWidth + sv.ColumnGap + sv.SkillsWidth;
            float viewWidth = LayoutViewWidth;
            float top = SheetTop(m);
            float height = Mathf.Max(top + _sStatsH + 18f + sv.HintHeight, top + _sSkillsH + sv.PanelGap + _sPanelH) + m.Margin;
            _sFit = MenuLayout.Fit(total, height, viewWidth, v.LayoutHeight, m);
            _sViewWidth = viewWidth / _sFit;
            _sX0 = (_sViewWidth - total) * 0.5f;
        }

        static float SheetTop(MenuValues m) => m.Top + m.HeadingHeight + m.HeadingGap;

        Rect SheetHeadingRect(MenuValues m) => new Rect(_sX0 + m.HeadingHeight * m.HeadingSlant * 0.5f, m.Top, m.HeadingWidth, m.HeadingHeight);
        Rect SheetStatsRect(MenuValues m) => new Rect(_sX0, SheetTop(m), m.Sheet.StatsWidth, _sStatsH);
        Rect SheetHintRect(MenuValues m) => new Rect(_sX0, SheetTop(m) + _sStatsH + 18f, m.Sheet.StatsWidth, m.Sheet.HintHeight);
        Rect SheetSkillsRect(MenuValues m) => new Rect(_sX0 + m.Sheet.StatsWidth + m.Sheet.ColumnGap, SheetTop(m), m.Sheet.SkillsWidth, _sSkillsH);
        Rect SheetPanelRectRaw(MenuValues m) => new Rect(_sX0 + m.Sheet.StatsWidth + m.Sheet.ColumnGap, SheetTop(m) + _sSkillsH + m.Sheet.PanelGap, m.Sheet.SkillsWidth, _sPanelH);

        Rect TileRect(int i, MenuValues m)
        {
            SheetValues sv = m.Sheet;
            Rect r = SheetSkillsRect(m);
            float x = r.x + m.PanelPad + 14f + i * (sv.Tile + sv.TileGap), y = r.y + m.PanelPad + m.PanelHeadHeight;
            return new Rect(x, y, sv.Tile, sv.Tile);
        }

        Rect Fitted(Rect r) => new Rect(r.x * _sFit, r.y * _sFit, r.width * _sFit, r.height * _sFit);

        /// <summary>The factor the sheet is drawn at to fit the view (1 when it fits).</summary>
        public float SheetFit
        {
            get
            {
                LayOutSheet();
                return _sFit;
            }
        }

        /// <summary>Tile <paramref name="index"/> of the row in the 1080 layout after the fit. For tests.</summary>
        public Rect SheetTileRect(int index)
        {
            LayOutSheet();
            return Fitted(TileRect(index, InterfaceStyle.V.Menus));
        }

        /// <summary>The tile under a point of the sheet's own layout (before the fit: where the mouse is under its matrix), or -1.</summary>
        int TileAtRaw(Vector2 point, MenuValues m)
        {
            for (int i = 0; i < _tileName.Count; i++)
                if (TileRect(i, m).Contains(point)) return i;
            return -1;
        }

        /// <summary>The tile under a point of the 1080 layout after the fit (as <see cref="SheetTileRect"/> gives them), or -1: the mouse's test in <see cref="DrawSheet"/>. For tests.</summary>
        public int SheetTileAt(Vector2 point)
        {
            LayOutSheet();
            return TileAtRaw(point / Mathf.Max(1e-4f, _sFit), InterfaceStyle.V.Menus);
        }

        /// <summary>The panel of the skill hovered in the 1080 layout after the fit (empty with none). For tests.</summary>
        public Rect SheetPanelRect
        {
            get
            {
                LayOutSheet();
                return _panel == null ? Rect.zero : Fitted(SheetPanelRectRaw(InterfaceStyle.V.Menus));
            }
        }

        /// <summary>The whole sheet in the 1080 layout after the fit (the heading, the stats and their keys' line, the skills and the panel). For tests.</summary>
        public Rect SheetWhole
        {
            get
            {
                LayOutSheet();
                MenuValues m = InterfaceStyle.V.Menus;
                Rect s = SheetStatsRect(m), h = SheetHintRect(m), k = SheetSkillsRect(m), p = SheetPanelRectRaw(m);
                float bottom = Mathf.Max(h.yMax, _panel != null ? p.yMax : k.yMax);
                return Fitted(Rect.MinMaxRect(s.x, m.Top, k.xMax, bottom));
            }
        }

        /// <summary>The stats' panel in the 1080 layout after the fit. For tests.</summary>
        public Rect SheetStatsPanelRect
        {
            get
            {
                LayOutSheet();
                return Fitted(SheetStatsRect(InterfaceStyle.V.Menus));
            }
        }

        /// <summary>
        /// What of the sheet's words does not fit inside its shape at the screen now, as words; empty when everything fits
        /// (work order 095: "text inside its shapes at both sizes"). For tests.
        /// </summary>
        public string SheetTextOutside()
        {
            LayOutSheet();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            SheetValues sv = m.Sheet;
            var problems = new List<string>();
            Vector2 hs = InterfaceStyle.Measure(sv.HeadingWord, InterfaceStyle.Text(true, m.HeadingSize, TextAnchor.MiddleLeft));
            if (HeadingPad(m) + hs.x + 16f > m.HeadingWidth) problems.Add("the heading is wider than its plate");
            _sheetGroups.Outside(_sheetStats, _sColW * 2f + m.ColumnInnerGap, problems);
            GUIStyle head = InterfaceStyle.Text(true, m.PanelHeadSize, TextAnchor.MiddleLeft), sub = InterfaceStyle.Text(false, m.PanelSubSize, TextAnchor.MiddleRight);
            if (InterfaceStyle.Measure(StatsHead, head).x + 24f + InterfaceStyle.Measure(SheetStatsSub, sub).x + 50f > sv.StatsWidth - 2f * m.PanelPad) problems.Add("the stats' heading is wider than its panel");
            if (InterfaceStyle.Measure(SkillsHead, head).x + 24f + InterfaceStyle.Measure(SkillsSub, sub).x + 50f > sv.SkillsWidth - 2f * m.PanelPad) problems.Add("the skills' heading is wider than its panel");
            for (int i = 0; i < _tileName.Count; i++)
            {
                if (TileRect(i, m).xMax > SheetSkillsRect(m).xMax - m.PanelPad) problems.Add("tile " + _tileName[i] + " is past the panel's end");
                if (_sKeyW[i] > sv.Tile + sv.TileGap) problems.Add("the key cap " + _tileKey[i] + " is wider than its tile");
            }
            float hint = 0f;
            for (int i = 0; i < s_SheetHint.Length; i++) hint += _sHintW[i] + (s_SheetHintCap[i] ? 6f : 18f);
            if (hint > sv.StatsWidth) problems.Add("the line of keys is wider than the stats");
            if (_panel != null)
            {
                float inner = PanelInnerWidth(m, sv);
                if (_sNameW + 16f + _sTierW + _sKeyCapW + 20f > inner) problems.Add("the skill's name, tier and key are wider than its panel");
                GUIStyle label = InterfaceStyle.Text(false, sv.LabelSize, TextAnchor.UpperLeft);
                foreach (SheetLine l in _panel.Lines)
                    if (InterfaceStyle.Measure(l.Label, label).x > sv.LabelWidth - 8f) problems.Add("the label " + l.Label + " is wider than its column");
                Rect p = SheetPanelRectRaw(m);
                if (p.yMax * _sFit > v.LayoutHeight - 4f) problems.Add("the skill's panel runs past the view's foot (" + (p.yMax * _sFit).ToString("0") + ")");
            }
            return string.Join("; ", problems);
        }

        // ---------------- The sheet, drawn ----------------

        Vector2 _sheetMouse = new Vector2(float.NaN, float.NaN);

        void DrawSheet()
        {
            LayOutSheet();
            InterfaceValues v = InterfaceStyle.V;
            MenuValues m = v.Menus;
            SheetValues sv = m.Sheet;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            InterfaceStyle.Fill(new Rect(0f, 0f, viewWidth, v.LayoutHeight), new Color(0f, 0f, 0f, Mathf.Clamp01(m.Veil)));
            Matrix4x4 layout = GUI.matrix;
            if (_sFit < 1f) GUI.matrix = layout * Matrix4x4.Scale(new Vector3(_sFit, _sFit, 1f));

            DrawHeadingPlate(SheetHeadingRect(m), sv.HeadingWord, v, m);

            // Her stats in their groups.
            Rect stats = SheetStatsRect(m);
            DrawPanel(stats, StatsHead, _sheetInRun ? SheetStatsSub : SheetStatsSubStart, InterfaceStyle.C(v.Teal), v, m);
            _sheetGroups.Draw(_sheetStats, stats.x + m.PanelPad + 10f, stats.y + m.PanelPad + m.PanelHeadHeight, stats.center.y, m.PanelSlant, m.ColumnInnerGap, m, sv, v);
            DrawSheetHint(SheetHintRect(m), v, m);

            // Her skills as tiles; the mouse hovering one picks it (only when the mouse moved: a still mouse changes nothing).
            Rect skills = SheetSkillsRect(m);
            DrawPanel(skills, SkillsHead, SkillsSub, InterfaceStyle.C(v.White), v, m);
            Event e = Event.current;
            if (e != null && (e.type == EventType.MouseMove || e.type == EventType.Repaint || e.type == EventType.MouseDown) && e.mousePosition != _sheetMouse)
            {
                _sheetMouse = e.mousePosition;
                int under = TileAtRaw(_sheetMouse, m);
                if (under >= 0)
                {
                    HoverSheetSkill(under);
                    LayOutSheet(); // the panel changed: its height
                }
            }
            for (int i = 0; i < _tileName.Count; i++) DrawTile(i, TileRect(i, m), i == SheetSelected, v, m);
            if (_panel != null) DrawSkillPanel(SheetPanelRectRaw(m), TileRect(SheetSelected, m), v, m, sv);
            GUI.matrix = layout;
        }

        void DrawTile(int i, Rect r, bool selected, InterfaceValues v, MenuValues m)
        {
            SheetValues sv = m.Sheet;
            float pivot = r.center.y, slant = v.Slant * 0.5f;
            InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline * 0.8f, v.Depth, selected ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White, 0.45f), selected ? 1.2f : 0.7f);
            InterfaceStyle.Leaning(r, pivot, slant, selected ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Black, 0.9f));
            Color ink = selected ? InterfaceStyle.C(v.Black) : _tileSlot[i] < 0 ? InterfaceStyle.C(v.White) : InterfaceStyle.C(v.Teal);
            Texture2D icon = _tileSlot[i] >= 0 ? InterfaceStyle.Icon(_tileName[i]) : null;
            if (icon != null)
            {
                GUI.color = ink;
                float pad = r.width * 0.16f;
                GUI.DrawTexture(new Rect(r.x + pad, r.y + pad, r.width - 2f * pad, r.height - 2f * pad), icon, ScaleMode.ScaleToFit);
            }
            else TextLabel(r, _tileShort[i], InterfaceStyle.Text(true, 19, TextAnchor.MiddleCenter), ink);
            float w = _sKeyW[i];
            KeyCap(new Rect(r.center.x - w * 0.5f, r.yMax + 6f, w, Mathf.Min(m.KeyCapHeight, sv.TileCap)), _tileKey[i], m, v); // round 1: a black cap under the white tile vanished on the dark
            if (GUI.Button(r, GUIContent.none, GUIStyle.none)) HoverSheetSkill(i);
        }

        void DrawSkillPanel(Rect r, Rect tile, InterfaceValues v, MenuValues m, SheetValues sv)
        {
            // The teal stem from the tile hovered down to its panel: the panel is that skill's.
            InterfaceStyle.Fill(new Rect(tile.center.x - 3f, tile.yMax + m.KeyCapHeight + 8f, 6f, r.y - tile.yMax - m.KeyCapHeight - 8f), InterfaceStyle.C(v.Teal));
            float pivot = r.center.y, slant = m.PanelSlant;
            InterfaceStyle.InkedPlate(r, pivot, slant, v.HeavyOutline, v.Depth * 2f, InterfaceStyle.C(v.Teal));
            InterfaceStyle.Leaning(r, pivot, slant, InterfaceStyle.C(v.Black, 0.92f));
            InterfaceStyle.DotsAlong(new Rect(r.x + r.width * 0.5f, r.y, r.width * 0.5f, sv.NameSize * 1.5f + m.PanelPad), pivot, slant, 0.1f, InterfaceStyle.C(v.Teal, 0.22f));
            InterfaceStyle.Leaning(new Rect(r.x, r.y, 5f, r.height), pivot, slant, InterfaceStyle.C(v.Teal));
            float Lean(float y) => (pivot - y) * slant;

            // The name on a white plate in black, its tier and its key beside it.
            float x = r.x + m.PanelPad + 10f, y = r.y + m.PanelPad, nameH = sv.NameSize * 1.5f;
            float lean = Lean(y + nameH * 0.5f);
            InterfaceStyle.Slanted(new Rect(x + lean - 4f, y + 2f, _sNameW + 24f, nameH - 4f), InterfaceStyle.C(v.White));
            TextLabel(new Rect(x + lean + 8f, y, _sNameW + 10f, nameH), _panel.Name, InterfaceStyle.Text(true, sv.NameSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Black));
            float after = x + lean + _sNameW + 36f;
            if (_sTierW > 0f)
            {
                var chip = new Rect(after, y + 10f, _sTierW, nameH - 20f);
                InterfaceStyle.Slanted(chip, _panel.Tier == "epic" ? InterfaceStyle.C(v.Gold) : InterfaceStyle.C(v.Teal));
                TextLabel(chip, _panel.Tier.ToUpperInvariant(), InterfaceStyle.Text(true, m.KeyCapSize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black));
                after += _sTierW + 10f;
            }
            if (_sKeyCapW > 0f) KeyCap(new Rect(after, y + (nameH - m.KeyCapHeight) * 0.5f, _sKeyCapW, m.KeyCapHeight), _panel.Key, m, v);
            InterfaceStyle.Slashes(r.xMax + Lean(y + 20f) - 14f, y + 4f, 26f, 6f, 2, InterfaceStyle.C(v.Red));
            y += nameH + 8f;

            // What it does, in the card's words.
            float inner = PanelInnerWidth(m, sv);
            TextLabel(new Rect(x + Lean(y + 12f), y, inner, _sSentenceH), _panel.Sentence, SheetSentenceStyle(sv), InterfaceStyle.C(v.White, 0.9f));
            y += _sSentenceH + 6f;

            // Its numbers: the label in grey, the value in white (the weight's line in teal: what he asked about).
            GUIStyle label = InterfaceStyle.Text(false, sv.LabelSize, TextAnchor.UpperLeft), value = SheetValueStyle(sv);
            float vw = PanelValueWidth(m, sv);
            for (int i = 0; i < _panel.Lines.Count && i < _sLineH.Count; i++)
            {
                SheetLine l = _panel.Lines[i];
                float h = _sLineH[i], ln = Lean(y + h * 0.5f);
                if (i % 2 == 0) InterfaceStyle.Fill(new Rect(x + ln - 6f, y, inner + 12f, h), InterfaceStyle.C(v.White, 0.045f));
                TextLabel(new Rect(x + ln, y + 3f, sv.LabelWidth, h), l.Label, label, InterfaceStyle.C(v.Grey));
                bool weight = l.Label == "your damage";
                TextLabel(new Rect(x + ln + sv.LabelWidth, y + 1f, vw, h), l.Value, value, weight ? InterfaceStyle.C(v.Teal) : InterfaceStyle.C(v.White));
                y += h;
            }

            // What she took for it.
            y += 10f;
            float lt = Lean(y + sv.GroupHead * 0.5f);
            TextLabel(new Rect(x + lt, y, inner, sv.GroupHead), TakenHead, InterfaceStyle.Text(true, sv.GroupHeadSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.Teal));
            y += sv.GroupHead;
            TextLabel(new Rect(x + Lean(y + 12f), y, inner, _sTakenH - sv.GroupHead), _sTaken, value, _panel.Taken.Count > 0 ? InterfaceStyle.C(v.White, 0.9f) : InterfaceStyle.C(v.Grey));
        }

        void DrawSheetHint(Rect r, InterfaceValues v, MenuValues m)
        {
            float x = r.x + 6f, h = m.KeyCapHeight;
            GUIStyle words = InterfaceStyle.Text(false, m.KeyWordSize, TextAnchor.MiddleLeft);
            for (int i = 0; i < s_SheetHint.Length; i++)
            {
                if (s_SheetHintCap[i])
                {
                    KeyCap(new Rect(x, r.y + (r.height - h) * 0.5f, _sHintW[i], h), s_SheetHint[i], m, v);
                    x += _sHintW[i] + 6f;
                }
                else
                {
                    TextLabel(new Rect(x + 2f, r.y, _sHintW[i] + 6f, r.height), s_SheetHint[i], words, InterfaceStyle.C(v.White, 0.8f), true);
                    x += _sHintW[i] + 18f;
                }
            }
        }
    }
}
