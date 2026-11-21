using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The run's screens in the interface's kinetic skin (decision 0011; work order 045), as part of
    /// <see cref="GeneratedLevels"/>. What a choice offers, its keys, its guard and its picks are the run's as before; only
    /// the drawing is the skin's:
    /// - a choice (a chest's upgrades, the elite's skill upgrade, the floor's new skill, the focus choice) is slim tilted
    ///   cards at the right edge, one per option: a large italic numeral (01, 02, 03) and the number key that picks it on a
    ///   small plate, the title in the heavy italic, the text in the lighter one; dim while the guard holds, lit when they can
    ///   be picked, the one under the pointer framed teal; the choice's heading over them and its footer under them; a pick
    ///   flashes the card white as it goes;
    /// - the prompt over a chest in reach is a prompt in the skin (its key on a plate), a notice a line in the skin;
    /// - the developer's box at the top right keeps its text, on a flat dark panel in the lighter italic.
    /// The cards' texts are made once when a choice opens or its offer changes, not every frame.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>True when the choice on screen is drawn as the skin's cards (the switch on, a choice up).</summary>
        public bool ChoiceKinetic => InterfaceStyle.Kinetic && Choice != ChoiceKind.None;
        /// <summary>The cards of the choice on screen now: how many, and each one's place (layout pixels of a view 1080 high), title and text.</summary>
        public int CardCount => _cardTitles.Count;
        public Rect CardRect(int index) => index >= 0 && index < _cardRects.Count ? _cardRects[index] : default;
        public string CardTitle(int index) => index >= 0 && index < _cardTitles.Count ? _cardTitles[index] : "";
        public string CardText(int index) => index >= 0 && index < _cardTexts.Count ? _cardTexts[index] : "";
        /// <summary>The last pick: which card, and whether its flash shows now.</summary>
        public int LastPickedCard { get; private set; } = -1;
        public bool PickFlashing => InterfaceStyle.Kinetic && LastPickedCard >= 0 && Time.unscaledTime - _pickedAt < InterfaceStyle.V.PickFlashSeconds;

        readonly List<string> _cardTitles = new List<string>();
        readonly List<string> _cardTexts = new List<string>();
        readonly List<string> _cardKeys = new List<string>();
        readonly List<Rect> _cardRects = new List<Rect>();
        readonly List<object> _cardsOf = new List<object>();
        readonly List<int> _cardsSkills = new List<int>();
        ChoiceKind _cardsKind;
        float _cardsLiveFrom = float.NaN, _cardsWidth, _cardsHeight, _pickedAt = float.NegativeInfinity;
        string _cardsHeading = "", _cardsSub = "", _cardsFooter = "", _pickedTitle = "";
        Rect _pickedRect;
        GUIStyle _devKinetic;

        /// <summary>A card was picked (by its key, a click, or an intent): it flashes as the choice goes.</summary>
        void CardPicked(int index)
        {
            LastPickedCard = index;
            _pickedAt = Time.unscaledTime;
            if (!InterfaceStyle.Kinetic || FindPlayer() == null) return;
            KineticCards();
            _pickedTitle = CardTitle(index);
            _pickedRect = CardRect(index);
        }

        /// <summary>
        /// The cards of the choice on screen, made again only when a choice opens or its offer changes (Second Look). Returns
        /// how many. Public for tests, which count what it allocates.
        /// </summary>
        public int KineticCards()
        {
            if (Choice == ChoiceKind.None || _player == null)
            {
                if (_cardTitles.Count > 0) ClearCards();
                return 0;
            }
            InterfaceValues v = InterfaceStyle.V;
            float viewWidth = Screen.width / Mathf.Max(1e-4f, InterfaceStyle.Scale);
            int count = Choice == ChoiceKind.FloorReward ? _offeredSkills.Count : Choice == ChoiceKind.SkillUpgrade ? _offeredSkillUpgrades.Count
                      : Choice == ChoiceKind.FocusChoice ? FocusModeRules.Options.Length : _offeredUpgrades.Count;
            if (SameCards(count) && Mathf.Approximately(_cardsWidth, viewWidth)) return count;
            ClearCards();
            _cardsKind = Choice;
            _cardsLiveFrom = ChoiceLiveFrom;
            _cardsWidth = viewWidth;
            PlayerCharacter player = _player;
            InputBindings bindings = player != null && player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            float height = Choice == ChoiceKind.FocusChoice ? v.FocusCardHeight : Choice == ChoiceKind.Chest ? v.CardHeight * 0.75f : v.CardHeight;
            _cardsHeight = height;
            string heading;
            for (int i = 0; i < count; i++)
            {
                string title, text;
                switch (Choice)
                {
                    case ChoiceKind.FloorReward:
                    {
                        int slot = _offeredSkills[i];
                        _cardsSkills.Add(slot);
                        SkillDefinition s = player.Skills[slot];
                        title = s.Name;
                        text = string.Format(CultureInfo.InvariantCulture, "key {0}   cooldown {1:0.#} s   {2}\n{3}",
                                             bindings != null ? bindings.SkillLabel(player.FirstFreeKey) : "?", s.Cooldown, SkillDefaults.MarkRole(s), SkillDefaults.Line(s.Name));
                        break;
                    }
                    case ChoiceKind.SkillUpgrade:
                    {
                        SkillUpgradeDefinition u = _offeredSkillUpgrades[i];
                        _cardsOf.Add(u);
                        int key = player.KeyOfSkill(player.SlotOf(u.Skill));
                        title = u.Name.ToUpperInvariant();
                        text = string.Format(CultureInfo.InvariantCulture, "{0}   key {1}   [{2}]\n{3}", u.Skill,
                                             bindings != null && key >= 0 ? bindings.SkillLabel(key) : "-", u.KindWord, u.Line);
                        break;
                    }
                    case ChoiceKind.FocusChoice:
                    {
                        FocusMode mode = FocusModeRules.Options[i];
                        title = FocusModeRules.Name(mode).ToUpperInvariant();
                        text = string.Join("\n", FocusModeRules.Lines(mode, FocusValues, player != null ? player.Identity.CooldownCut : 0.4f));
                        break;
                    }
                    default:
                    {
                        Upgrade u = _offeredUpgrades[i];
                        _cardsOf.Add(u);
                        title = u.Text;
                        text = "";
                        break;
                    }
                }
                _cardTitles.Add(title);
                _cardTexts.Add(text);
                _cardKeys.Add(InterfaceStyle.Number(i + 1));
                UiBox b = InterfaceRules.Card(i, count, height, viewWidth, v);
                _cardRects.Add(new Rect(b.X, b.Y, b.W, b.H));
            }
            // The heading and the footer, the same words as the plain choice's.
            heading = Choice == ChoiceKind.FloorReward
                ? string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose a new skill (1-{1} or click)", Floor, count)
                : Choice == ChoiceKind.SkillUpgrade
                ? string.Format(CultureInfo.InvariantCulture, "ELITE ROOM CLEARED   choose a skill upgrade (1-{0} or click)", count)
                : Choice == ChoiceKind.FocusChoice
                ? string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose how focus works for the rest of the run (1-{1} or click)", Floor, count)
                : string.Format(CultureInfo.InvariantCulture, "CHEST   choose an upgrade (1-{0} or click)", count);
            int gap = heading.IndexOf("   ", System.StringComparison.Ordinal);
            _cardsHeading = gap > 0 ? heading.Substring(0, gap) : heading;
            _cardsSub = gap > 0 ? heading.Substring(gap).Trim() : "";
            _cardsFooter = "The game is paused.   F7 leaves the run." + (Choice == ChoiceKind.FocusChoice ? "" : RedrawWords());
            return count;
        }

        bool SameCards(int count)
        {
            if (_cardsKind != Choice || _cardTitles.Count != count || !(_cardsLiveFrom == ChoiceLiveFrom)) return false;
            for (int i = 0; i < count; i++)
            {
                switch (Choice)
                {
                    case ChoiceKind.FloorReward: if (i >= _cardsSkills.Count || _cardsSkills[i] != _offeredSkills[i]) return false; break;
                    case ChoiceKind.SkillUpgrade: if (i >= _cardsOf.Count || !ReferenceEquals(_cardsOf[i], _offeredSkillUpgrades[i])) return false; break;
                    case ChoiceKind.Chest: if (i >= _cardsOf.Count || !ReferenceEquals(_cardsOf[i], _offeredUpgrades[i])) return false; break;
                }
            }
            return true;
        }

        void ClearCards()
        {
            _cardTitles.Clear();
            _cardTexts.Clear();
            _cardKeys.Clear();
            _cardRects.Clear();
            _cardsOf.Clear();
            _cardsSkills.Clear();
            _cardsKind = ChoiceKind.None;
            _cardsLiveFrom = float.NaN;
        }

        // ---------------- Drawing ----------------

        /// <summary>The choice on screen as cards, guarded as the plain one is: deaf to clicks and dim while the guard runs.</summary>
        void DrawGuardedChoiceKinetic()
        {
            int count = KineticCards();
            if (count == 0) return;
            InterfaceValues v = InterfaceStyle.V;
            float now = Time.unscaledTime;
            bool live = _guard.Live(now);
            Event e = Event.current;
            if (!live && e != null && (e.type == EventType.MouseDown || e.type == EventType.MouseUp)) e.Use();
            bool enabledBefore = GUI.enabled;
            Color colorBefore = GUI.color;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            float light = live ? 1f : Mathf.Lerp(v.CardDim, 0.85f, _guard.Share(now));

            // The heading over the cards, right aligned to them, and the footer under them.
            Rect first = _cardRects[0], last = _cardRects[count - 1];
            float right = viewWidth - v.CardRight;
            float headW = Mathf.Min(viewWidth * 0.6f, 760f);
            InterfaceStyle.Label(new Rect(right - headW, first.y - v.ChoiceTitleSize * 2.6f, headW, v.ChoiceTitleSize * 1.3f), _cardsHeading,
                                 InterfaceStyle.Text(true, v.ChoiceTitleSize, TextAnchor.LowerRight), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Label(new Rect(right - headW, first.y - v.ChoiceTitleSize * 1.3f, headW, v.ChoiceTitleSize * 1.1f), _cardsSub,
                                 InterfaceStyle.Text(false, v.CardTextSize + 1, TextAnchor.UpperRight), InterfaceStyle.C(v.Teal), true);
            InterfaceStyle.Label(new Rect(right - headW, last.yMax + 8f, headW, v.CardTextSize * 2.6f), _cardsFooter,
                                 InterfaceStyle.Text(false, v.CardTextSize - 1, TextAnchor.UpperRight, true), InterfaceStyle.C(v.Grey), true);

            for (int i = 0; i < count; i++)
            {
                Rect r = _cardRects[i];
                bool hover = live && e != null && r.Contains(e.mousePosition);
                DrawCard(r, i, light, hover, v);
                GUI.enabled = live;
                bool clicked = GUI.Button(r, GUIContent.none, GUIStyle.none);
                GUI.enabled = enabledBefore;
                if (clicked && live)
                {
                    GUI.enabled = enabledBefore;
                    GUI.color = colorBefore;
                    GUI.matrix = matrixBefore;
                    if (Choice == ChoiceKind.FloorReward) PickReward(i);
                    else if (Choice == ChoiceKind.SkillUpgrade) PickSkillUpgrade(i);
                    else if (Choice == ChoiceKind.FocusChoice) PickFocusChoice(i);
                    else PickUpgrade(i);
                    return;
                }
            }
            GUI.enabled = enabledBefore;
            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        void DrawCard(Rect r, int i, float light, bool hover, InterfaceValues v)
        {
            float pivot = r.center.y;
            InterfaceStyle.Slanted(r, pivot, InterfaceStyle.C(v.Panel, light));
            // A teal edge down its left side; the whole card framed teal under the pointer.
            InterfaceStyle.Slanted(new Rect(r.x, r.y, 4f, r.height), pivot, InterfaceStyle.C(v.Teal, light));
            if (hover) InterfaceStyle.SlantedFrame(r, InterfaceStyle.C(v.Teal), 2f);
            Rect s = Shift(r, pivot, v);
            float numW = v.NumeralSize * 1.25f;
            InterfaceStyle.Label(new Rect(s.x + 12f, s.y, numW, Mathf.Min(r.height, v.NumeralSize * 1.4f)), InterfaceRules.Numeral(i),
                                 InterfaceStyle.Text(true, v.NumeralSize, TextAnchor.MiddleLeft), InterfaceStyle.C(v.White, light), true);
            float keyH = v.KeySize * 1.25f;
            var keyPlate = new Rect(s.x + 16f, s.y + v.NumeralSize * 1.4f - 2f, keyH * 1.2f, keyH);
            if (keyPlate.yMax < r.yMax - 2f)
            {
                InterfaceStyle.Slanted(keyPlate, InterfaceStyle.C(v.White, light));
                InterfaceStyle.Label(keyPlate, _cardKeys[i], InterfaceStyle.Text(true, v.KeySize, TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black, light));
            }
            float tx = s.x + 12f + numW + 6f, tw = r.width - (tx - s.x) - 10f;
            bool bodyless = _cardTexts[i].Length == 0;
            InterfaceStyle.Label(new Rect(tx, s.y + 6f, tw, bodyless ? r.height - 12f : v.CardTitleSize * 1.35f), _cardTitles[i],
                                 InterfaceStyle.Text(true, v.CardTitleSize, bodyless ? TextAnchor.MiddleLeft : TextAnchor.UpperLeft, true), InterfaceStyle.C(v.White, light), true);
            if (!bodyless)
                InterfaceStyle.Label(new Rect(tx, s.y + 6f + v.CardTitleSize * 1.35f, tw, r.height - 12f - v.CardTitleSize * 1.35f), _cardTexts[i],
                                     InterfaceStyle.Text(false, v.CardTextSize, TextAnchor.UpperLeft, true), InterfaceStyle.C(v.White, 0.85f * light));
            if (Choice == ChoiceKind.FocusChoice) DrawGaugeSketchKinetic(FocusModeRules.Options[i], new Rect(r.xMax - 110f, r.yMax - 18f, 90f, 7f), light, v);
        }

        /// <summary>The gauge as it will look (work order 025), in the skin: Hair Trigger's parts, Steady Hand's one bar in the window's colour.</summary>
        void DrawGaugeSketchKinetic(FocusMode mode, Rect r, float light, InterfaceValues v)
        {
            if (mode == FocusMode.HairTrigger)
            {
                int parts = Mathf.Max(1, FocusValues.Parts);
                float w = (r.width - v.PartGap * (parts - 1)) / parts;
                for (int p = 0; p < parts; p++)
                    InterfaceStyle.SlantedBar(new Rect(r.x + p * (w + v.PartGap), r.y, w, r.height), p < parts - 1 ? 1f : 0.4f, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.Teal, light));
            }
            else InterfaceStyle.SlantedBar(r, 0.7f, InterfaceStyle.C(v.Back), InterfaceStyle.C(v.White, light));
        }

        /// <summary>A pick's flash: the picked card white for a moment as the choice goes.</summary>
        void DrawPickFlash()
        {
            if (!PickFlashing || Choice != ChoiceKind.None) return;
            InterfaceValues v = InterfaceStyle.V;
            float a = 1f - (Time.unscaledTime - _pickedAt) / Mathf.Max(0.01f, v.PickFlashSeconds);
            Color colorBefore = GUI.color;
            InterfaceStyle.BeginLayout(out Matrix4x4 matrixBefore);
            InterfaceStyle.Slanted(_pickedRect, InterfaceStyle.C(v.White, a));
            InterfaceStyle.Label(Shift(_pickedRect, _pickedRect.center.y, v), _pickedTitle, InterfaceStyle.Text(true, v.CardTitleSize, TextAnchor.MiddleCenter, true), InterfaceStyle.C(v.Black, a));
            GUI.color = colorBefore;
            GUI.matrix = matrixBefore;
        }

        static Rect Shift(Rect r, float pivotY, InterfaceValues v) => new Rect(r.x + (pivotY - r.center.y) * v.Slant, r.y, r.width, r.height);

        /// <summary>The prompt over a chest in reach, in the skin.</summary>
        void DrawChestPromptKinetic()
        {
            GeneratedLevel.PlacedProp chest = ChestPrompt;
            Camera cam = Camera.main;
            if (chest == null || cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(chest.Object.transform.position + Vector3.up * 1.4f);
            if (screen.z <= 0f) return;
            Color colorBefore = GUI.color;
            Matrix4x4 before = GUI.matrix;
            InterfaceStyle.BeginLayout(out Matrix4x4 _);
            float scale = Mathf.Max(1e-4f, InterfaceStyle.Scale);
            InterfaceStyle.Prompt(screen.x / scale, (Screen.height - screen.y) / scale, InteractKeyLabel(), "open chest");
            GUI.color = colorBefore;
            GUI.matrix = before;
        }

        Key _interactLabelKey = Key.None;
        string _interactLabel = "";

        /// <summary>The interact key's words, made once for the key.</summary>
        string InteractKeyLabel()
        {
            Key key = InteractKey;
            if (key != _interactLabelKey || _interactLabel.Length == 0)
            {
                _interactLabelKey = key;
                _interactLabel = key.ToString();
            }
            return _interactLabel;
        }

        /// <summary>A notice in the skin: one line over a faint slanted back with a teal rule, where the plain one stood.</summary>
        void DrawNoticeKinetic()
        {
            Color colorBefore = GUI.color;
            Matrix4x4 before = GUI.matrix;
            float viewWidth = InterfaceStyle.BeginLayout(out Matrix4x4 _);
            InterfaceStyle.Line(viewWidth * 0.5f, InterfaceStyle.V.LayoutHeight * 0.3f, Notice);
            GUI.color = colorBefore;
            GUI.matrix = before;
        }

        /// <summary>The developer's box's style in the skin: the same text on a flat dark panel, in the lighter italic.</summary>
        GUIStyle DevBoxStyle()
        {
            if (!InterfaceStyle.Kinetic) return _style;
            if (_devKinetic == null)
            {
                _devKinetic = new GUIStyle(_style) { font = InterfaceStyle.Light, fontSize = 12 };
                var back = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "KineticDevBox" };
                back.SetPixel(0, 0, InterfaceStyle.C(InterfaceStyle.V.Panel));
                back.Apply();
                _devKinetic.normal.background = back;
                _devKinetic.normal.textColor = Color.white;
            }
            return _devKinetic;
        }
    }
}
