using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// An offer as a pop-up in the middle (work order 065, Kristian, 2026-11-21: "oppgraderingene trenger mer informasjon, de
    /// er ikke så forklarende. gjerne i midten som en større pop-up i det uiet"), as part of <see cref="GeneratedLevels"/>:
    /// every choice (a chest's upgrades, a floor's new skill, the elite room's skill upgrade, the focus choice) is larger cards
    /// side by side in the middle band of the screen, over a dark veil on the paused game, each saying its key large, its
    /// name, its tier, one plain sentence, its numbers before and after (<see cref="OfferTexts"/>, from her values now), notes
    /// and a cost in the red. Sizes are this order's own (<see cref="OfferValues"/>, <see cref="Offers"/>), in layout pixels of
    /// a view 1080 high. What is offered, its keys, its guard (work order 042) and the click on a card's leaning shape (work
    /// order 055) are as before. The cards are made and measured once when the offer opens (in
    /// <see cref="KineticCards"/>), never in a frame. Off (<see cref="OfferValues.PopUp"/>), this morning's cards at the right
    /// edge with this morning's words. The old look (the interface's switch off) draws the same words in its plain boxes in
    /// the middle (<see cref="DrawPlainPopUp"/>).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>The pop-up's numbers (work order 065); the switch <see cref="OfferValues.PopUp"/> brings back this morning's cards.</summary>
        public OfferValues Offers = new OfferValues();
        /// <summary>True when an offer is drawn as the pop-up in the middle.</summary>
        public bool OfferPopUp => Offers != null && Offers.PopUp;

        /// <summary>The card <paramref name="index"/> of the offer on screen as it reads (the pop-up's), or null.</summary>
        public OfferCard OfferCardAt(int index) => index >= 0 && index < _offerCards.Count ? _offerCards[index] : null;
        /// <summary>The parts of a card as drawn: how many, and each one's words, kind, rectangle (layout pixels) and style. For tests.</summary>
        public int CardPartCount(int index) => index >= 0 && index < _popParts.Count ? _popParts[index].Count : 0;
        public string CardPartText(int index, int part) => Part(index, part)?.Text ?? "";
        public OfferPartKind CardPartKind(int index, int part) => Part(index, part)?.Kind ?? OfferPartKind.Sentence;
        public Rect CardPartRect(int index, int part) => Part(index, part)?.Rect ?? default;
        public GUIStyle CardPartStyle(int index, int part) => Part(index, part)?.Style;
        /// <summary>Where a card's key is drawn, on its plate, and its name; the name's style.</summary>
        public Rect CardKeyRect(int index) => index >= 0 && index < _popKeys.Count ? _popKeys[index] : default;
        public Rect CardNameRect(int index) => index >= 0 && index < _popNames.Count ? _popNames[index] : default;
        public GUIStyle CardNameStyle => _popNameStyle;
        /// <summary>The pop-up's scale (1, or less when the row would be too wide) and its slant.</summary>
        public float OfferScale => _popScale;
        public float OfferSlant => Offers != null ? Offers.Slant : 0f;
        /// <summary>
        /// For tests: the view's width in layout pixels the cards are laid out for (1920 for a 1920 by 1080 or a 1280 by 720
        /// screen, 1440 for 4:3), where no window gives one; 0, the screen's.
        /// </summary>
        public float ViewWidthForTests { get; set; }

        sealed class PopPart
        {
            public OfferPartKind Kind;
            public string Text;
            public Rect Rect;
            public GUIStyle Style;
        }

        readonly List<OfferCard> _offerCards = new List<OfferCard>();
        readonly List<List<PopPart>> _popParts = new List<List<PopPart>>();
        readonly List<Rect> _popKeys = new List<Rect>();
        readonly List<Rect> _popNames = new List<Rect>();
        bool _cardsPopUp;
        float _popScale = 1f;
        GUIStyle _popNameStyle;

        PopPart Part(int index, int part) =>
            index >= 0 && index < _popParts.Count && part >= 0 && part < _popParts[index].Count ? _popParts[index][part] : null;

        void ClearPopUp()
        {
            _offerCards.Clear();
            _popParts.Clear();
            _popKeys.Clear();
            _popNames.Clear();
            _cardsPopUp = false;
        }

        // ---------------- The words ----------------

        /// <summary>Her values now, for the cards' numbers: the run's modifiers with her picks, her skills as she has them, her health.</summary>
        OfferContext OfferContextNow()
        {
            PlayerCharacter player = _player;
            var equipped = new List<SkillDefinition>();
            if (player != null)
                for (int slot = 0; slot < player.Skills.Count; slot++)
                    if (player.IsEquipped(slot) && player.Skills[slot] != null) equipped.Add(player.Skills[slot]);
            return new OfferContext
            {
                Modifiers = player != null && player.Modifiers != null ? player.Modifiers : Modifiers,
                Run = RunValues,
                Feel = player != null ? player.Feel : new FeelValues(),
                Kit = player != null ? player.Kit : new KitValues(),
                Identity = player != null ? player.Identity : new IdentityValues(),
                FocusValues = FocusValues,
                FocusMode = player != null ? player.FocusMode : FocusMode.Today,
                OwnMaxHealth = _health != null ? _health.MaxHealth : Placeholders.PlayerHealth,
                Health = _health != null ? _health.Current : Placeholders.PlayerHealth,
                Equipped = equipped,
            };
        }

        /// <summary>Card <paramref name="index"/> of the choice on screen as it reads (<see cref="OfferTexts"/>).</summary>
        OfferCard OfferCardFor(int index, OfferContext context, InputBindings bindings)
        {
            PlayerCharacter player = _player;
            switch (Choice)
            {
                case ChoiceKind.FloorReward:
                {
                    SkillDefinition s = player.Skills[_offeredSkills[index]];
                    return OfferTexts.ForSkill(s, OfferedTierAt(index), bindings != null ? bindings.SkillLabel(player.FirstFreeKey) : "?", context);
                }
                case ChoiceKind.SkillUpgrade:
                {
                    SkillUpgradeDefinition u = _offeredSkillUpgrades[index];
                    int slot = player.SlotOf(u.Skill), key = player.KeyOfSkill(slot);
                    SkillDefinition now = slot >= 0 && slot < player.Skills.Count ? player.Skills[slot] : null;
                    return OfferTexts.ForSkillUpgrade(u, now, bindings != null && key >= 0 ? bindings.SkillLabel(key) : "-", context);
                }
                case ChoiceKind.FocusChoice:
                    return OfferTexts.ForFocus(FocusModeRules.Options[index], context);
                default:
                    return OfferTexts.ForUpgrade(_offeredUpgrades[index], context);
            }
        }

        // ---------------- The layout, made once an offer ----------------

        /// <summary>
        /// The pop-up's cards measured and placed (layout pixels): every part of every card measured at the width it is drawn
        /// at, the cards of an offer all as high as the longest needs and never under <see cref="OfferValues.CardHeight"/>,
        /// the row centred in the middle band, scaled down once more when the row with its lean is wider than its share.
        /// </summary>
        void LayOutPopUp(int count, float viewWidth)
        {
            OfferValues o = Offers;
            float viewHeight = InterfaceStyle.V.LayoutHeight;
            float k = OfferLayout.Scale(count, o.CardHeight, viewWidth, o), height = o.CardHeight;
            for (int pass = 0; pass < 3; pass++)
            {
                height = Mathf.Max(o.CardHeight * k, Mathf.Ceil(Measure(count, k)));
                if (OfferLayout.RowWidth(count, height, k, o) <= o.MostRowShare * viewWidth + 0.01f) break;
                k = OfferLayout.Scale(count, height, viewWidth, o);
            }
            _popScale = k;
            _cardsHeight = height;
            float pad = o.Pad * k, plate = o.KeyPlate * k;
            for (int i = 0; i < count; i++)
            {
                UiBox b = OfferLayout.Card(i, count, height, k, viewWidth, viewHeight, o);
                var r = new Rect(b.X, b.Y, b.W, b.H);
                _cardRects.Add(r);
                _popKeys[i] = Lean(new Rect(r.x + pad, r.y + pad, plate, plate), r, o.Slant);
                Rect name = _popNames[i];
                _popNames[i] = Lean(new Rect(r.x + name.x, r.y + name.y, name.width, name.height), r, o.Slant);
                foreach (PopPart p in _popParts[i]) p.Rect = Lean(new Rect(r.x + p.Rect.x, r.y + p.Rect.y, p.Rect.width, p.Rect.height), r, o.Slant);
            }
        }

        /// <summary>
        /// Every card's parts at scale <paramref name="k"/>: their styles, heights and places inside the card (relative to its
        /// top left, before the lean); returns the height the tallest card needs.
        /// </summary>
        float Measure(int count, float k)
        {
            OfferValues o = Offers;
            float pad = o.Pad * k, plate = o.KeyPlate * k, gap = o.PartGap * k, textWidth = OfferLayout.TextWidth(k, o);
            float nameLeft = pad + plate + 12f * k, nameWidth = Mathf.Max(10f, textWidth - plate - 12f * k);
            _popNameStyle = InterfaceStyle.Text(true, OfferLayout.Size(o.NameSize, k), TextAnchor.MiddleLeft, true);
            _popParts.Clear();
            _popKeys.Clear();
            _popNames.Clear();
            float most = 0f;
            for (int i = 0; i < count; i++)
            {
                OfferCard card = _offerCards[i];
                float nameHeight = Mathf.Ceil(InterfaceStyle.MeasureHeight(card.Name, _popNameStyle, nameWidth));
                float head = Mathf.Max(plate, nameHeight);
                _popKeys.Add(default);
                _popNames.Add(new Rect(nameLeft, pad, nameWidth, head));
                float y = pad + head + gap;
                var parts = new List<PopPart>();
                foreach (OfferPart part in card.Parts())
                {
                    GUIStyle style = PartStyle(part.Kind, k);
                    float h = Mathf.Ceil(InterfaceStyle.MeasureHeight(part.Text, style, textWidth)) + 1f;
                    parts.Add(new PopPart { Kind = part.Kind, Text = part.Text, Style = style, Rect = new Rect(pad, y, textWidth, h) });
                    y += h + (part.Kind == OfferPartKind.Sentence || part.Kind == OfferPartKind.For || part.Kind == OfferPartKind.Tier ? gap * 1.6f : gap);
                }
                _popParts.Add(parts);
                most = Mathf.Max(most, y - gap + pad + (Choice == ChoiceKind.FocusChoice ? o.GaugeRoom * k : 0f));
            }
            return most;
        }

        /// <summary>A part's style at the pop-up's scale: the heavy italic for the tier and the numbers, the lighter for the rest.</summary>
        GUIStyle PartStyle(OfferPartKind kind, float k)
        {
            OfferValues o = Offers;
            switch (kind)
            {
                case OfferPartKind.Tier: return InterfaceStyle.Text(true, OfferLayout.Size(o.TierSize, k), TextAnchor.UpperLeft, true);
                case OfferPartKind.For: return InterfaceStyle.Text(false, OfferLayout.Size(o.ForSize, k), TextAnchor.UpperLeft, true);
                case OfferPartKind.Numbers: return InterfaceStyle.Text(true, OfferLayout.Size(o.NumberSize, k), TextAnchor.UpperLeft, true);
                case OfferPartKind.Note: return InterfaceStyle.Text(false, OfferLayout.Size(o.NoteSize, k), TextAnchor.UpperLeft, true);
                case OfferPartKind.Cost: return InterfaceStyle.Text(false, OfferLayout.Size(o.CostSize, k), TextAnchor.UpperLeft, true);
                default: return InterfaceStyle.Text(false, OfferLayout.Size(o.SentenceSize, k), TextAnchor.UpperLeft, true);
            }
        }

        /// <summary>A rectangle inside a card moved along the card's lean, by its own middle, so the words lean with the card.</summary>
        static Rect Lean(Rect part, Rect card, float slant) => new Rect(part.x + slant * (card.center.y - part.center.y), part.y, part.width, part.height);

        // ---------------- Drawing ----------------

        /// <summary>The veil over the paused game, the heading over the row and the footer under it.</summary>
        void DrawPopUpFrame(float viewWidth, float light)
        {
            OfferValues o = Offers;
            InterfaceValues v = InterfaceStyle.V;
            float k = _popScale;
            if (o.Veil > 0f) InterfaceStyle.Fill(new Rect(0f, 0f, viewWidth, v.LayoutHeight), new Color(0f, 0f, 0f, Mathf.Clamp01(o.Veil)));
            Rect first = _cardRects[0];
            float heading = OfferLayout.Size(o.HeadingSize, k), sub = OfferLayout.Size(o.SubSize, k);
            float top = first.y - heading * 1.3f - sub * 1.5f - 14f * k;
            InterfaceStyle.Label(new Rect(0f, top, viewWidth, heading * 1.3f), _cardsHeading, InterfaceStyle.Text(true, (int)heading, TextAnchor.LowerCenter), InterfaceStyle.C(v.White), true);
            InterfaceStyle.Label(new Rect(0f, top + heading * 1.3f + 2f, viewWidth, sub * 1.4f), _cardsSub, InterfaceStyle.Text(false, (int)sub, TextAnchor.UpperCenter), InterfaceStyle.C(v.Teal), true);
            int footer = OfferLayout.Size(o.FooterSize, k);
            InterfaceStyle.Label(new Rect(viewWidth * 0.1f, first.yMax + 14f * k, viewWidth * 0.8f, footer * 2.8f), _cardsFooter,
                                 InterfaceStyle.Text(false, footer, TextAnchor.UpperCenter, true), InterfaceStyle.C(v.Grey, light), true);
        }

        /// <summary>One card: its leaning panel, a teal edge, a tier's bar, its key large on a white plate, its name and its parts.</summary>
        void DrawPopUpCard(Rect r, int i, float light, bool hover)
        {
            InterfaceValues v = InterfaceStyle.V;
            OfferValues o = Offers;
            float k = _popScale, pivot = r.center.y, slant = o.Slant;
            Leaning(r, pivot, slant, InterfaceStyle.C(v.Panel, light));
            Leaning(new Rect(r.x, r.y, 5f * k, r.height), pivot, slant, InterfaceStyle.C(v.Teal, light));
            Color ink = Lit(InterfaceStyle.Ink(_cardInks[i]), light);
            if (_cardInks[i] != UiInk.White) Leaning(new Rect(r.x, r.y, r.width, 5f * k), pivot, slant, ink); // a rare's or an epic's bar
            if (hover) LeaningFrame(r, pivot, slant, InterfaceStyle.C(v.Teal), 3f * k);
            Rect plate = _popKeys[i];
            Leaning(plate, plate.center.y, slant, InterfaceStyle.C(v.White, light));
            InterfaceStyle.Label(plate, _cardKeys[i], InterfaceStyle.Text(true, OfferLayout.Size(o.KeySize, k), TextAnchor.MiddleCenter), InterfaceStyle.C(v.Black, light));
            InterfaceStyle.Label(_popNames[i], _cardTitles[i], _popNameStyle, InterfaceStyle.C(v.White, light), true);
            List<PopPart> parts = _popParts[i];
            for (int p = 0; p < parts.Count; p++)
            {
                PopPart part = parts[p];
                InterfaceStyle.Label(part.Rect, part.Text, part.Style, PartInk(part.Kind, ink, light, v), part.Kind == OfferPartKind.Numbers || part.Kind == OfferPartKind.Tier);
            }
            if (Choice == ChoiceKind.FocusChoice)
                DrawGaugeSketchKinetic(FocusModeRules.Options[i], new Rect(r.xMax - o.Pad * k - 110f * k, r.yMax - o.Pad * k - 8f * k, 100f * k, 8f * k), light, v);
        }

        static Color PartInk(OfferPartKind kind, Color tierInk, float light, InterfaceValues v)
        {
            switch (kind)
            {
                case OfferPartKind.Tier: return tierInk;
                case OfferPartKind.For: return InterfaceStyle.C(v.Teal, light);
                case OfferPartKind.Note: return InterfaceStyle.C(v.Gold, light);
                case OfferPartKind.Cost: return InterfaceStyle.C(v.Red, light);
                case OfferPartKind.Sentence: return InterfaceStyle.C(v.White, 0.92f * light);
                default: return InterfaceStyle.C(v.White, light);
            }
        }

        /// <summary>A rectangle leaning by the pop-up's own slant about the line <paramref name="pivotY"/> (InterfaceStyle's way, its slant).</summary>
        static void Leaning(Rect r, float pivotY, float slant, Color c)
        {
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before * InterfaceStyle.Shear(pivotY, slant);
            InterfaceStyle.Fill(r, c);
            GUI.matrix = before;
        }

        /// <summary>A leaning outline of four thin sides.</summary>
        static void LeaningFrame(Rect r, float pivotY, float slant, Color c, float t)
        {
            Matrix4x4 before = GUI.matrix;
            GUI.matrix = before * InterfaceStyle.Shear(pivotY, slant);
            InterfaceStyle.Fill(new Rect(r.x, r.y, r.width, t), c);
            InterfaceStyle.Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
            InterfaceStyle.Fill(new Rect(r.x, r.y, t, r.height), c);
            InterfaceStyle.Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
            GUI.matrix = before;
        }

        // ---------------- The old look: the same words in plain boxes, in the middle ----------------

        readonly List<string> _plainTexts = new List<string>();
        readonly List<Rect> _plainRects = new List<Rect>();
        readonly List<object> _plainOf = new List<object>();
        ChoiceKind _plainKind;
        float _plainLiveFrom = float.NaN, _plainWidth = float.NaN;
        bool _plainCanRedraw;
        string _plainTitle = "", _plainFooter = "";
        Rect _plainBox;
        GUIStyle _plainCardStyle;

        /// <summary>
        /// The plain boxes' texts, made again only when a choice opens or its offer changes (not in a frame). Returns how many.
        /// Public for tests; where the boxes stand is worked out where they are drawn (<see cref="PlainLayout"/>), with the skin's style.
        /// </summary>
        public int PlainCards()
        {
            int count = Choice == ChoiceKind.FloorReward ? _offeredSkills.Count : Choice == ChoiceKind.SkillUpgrade ? _offeredSkillUpgrades.Count
                      : Choice == ChoiceKind.FocusChoice ? FocusModeRules.Options.Length : _offeredUpgrades.Count;
            if (Choice == ChoiceKind.None || _player == null || count == 0)
            {
                _plainTexts.Clear();
                _plainRects.Clear();
                return 0;
            }
            if (_plainKind == Choice && _plainTexts.Count == count && _plainLiveFrom == ChoiceLiveFrom && SamePlain(count)) return count;
            _plainKind = Choice;
            _plainLiveFrom = ChoiceLiveFrom;
            _plainCanRedraw = CanRedraw;
            _plainWidth = float.NaN;
            _plainTexts.Clear();
            _plainRects.Clear();
            _plainOf.Clear();
            _plainSlots.Clear();
            OfferContext context = OfferContextNow();
            InputBindings bindings = _player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            for (int i = 0; i < count; i++)
            {
                OfferCard card = OfferCardFor(i, context, bindings);
                _plainOf.Add(OfferedOf(i));
                if (Choice == ChoiceKind.FloorReward) _plainSlots.Add(_offeredSkills[i]);
                _plainTexts.Add((i + 1).ToString(CultureInfo.InvariantCulture) + "   " + card.Name.ToUpperInvariant() + "\n" + card.Body);
            }
            _plainTitle = Choice == ChoiceKind.FloorReward
                ? string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose a new skill (1-{1} or click)", Floor, count)
                : Choice == ChoiceKind.SkillUpgrade
                ? string.Format(CultureInfo.InvariantCulture, "ELITE ROOM CLEARED   choose a skill upgrade (1-{0} or click)", count)
                : Choice == ChoiceKind.FocusChoice
                ? string.Format(CultureInfo.InvariantCulture, "FLOOR {0} CLEARED   choose how focus works for the rest of the run (1-{1} or click)", Floor, count)
                : string.Format(CultureInfo.InvariantCulture, "CHEST   choose an upgrade (1-{0} or click)", count);
            _plainFooter = "The game is paused.   F7 leaves the run." + (Choice == ChoiceKind.FocusChoice ? "" : RedrawWords());
            return count;
        }

        /// <summary>What card <paramref name="index"/> offers: the skill upgrade or the chest's upgrade itself, a floor reward's tier.</summary>
        object OfferedOf(int index) =>
            Choice == ChoiceKind.FloorReward ? (object)OfferedTierAt(index)
            : Choice == ChoiceKind.SkillUpgrade ? _offeredSkillUpgrades[index]
            : Choice == ChoiceKind.Chest ? _offeredUpgrades[index] : null;

        /// <summary>True while the plain texts are of the offer on screen (a Second Look that drew again makes them again).</summary>
        bool SamePlain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (i >= _plainOf.Count || !ReferenceEquals(OfferedOf(i), _plainOf[i])) return false;
                if (Choice == ChoiceKind.FloorReward && (i >= _plainSlots.Count || _plainSlots[i] != _offeredSkills[i])) return false;
            }
            return _plainCanRedraw == CanRedraw;
        }

        readonly List<int> _plainSlots = new List<int>();

        /// <summary>The plain boxes side by side in the middle (the old look's 720 layout), each as high as the longest text needs; once per offer and width.</summary>
        void PlainLayout(int count, float viewWidth)
        {
            if (_plainRects.Count == count && Mathf.Approximately(_plainWidth, viewWidth)) return;
            _plainWidth = viewWidth;
            _plainRects.Clear();
            if (_plainCardStyle == null)
                _plainCardStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            OfferValues o = Offers;
            float w = Mathf.Min(o.PlainCardWidth, (viewWidth * o.MostRowShare - (count - 1) * o.PlainCardGap) / count);
            float h = o.PlainLeastHeight;
            for (int i = 0; i < count; i++) h = Mathf.Max(h, Mathf.Ceil(_plainCardStyle.CalcHeight(new GUIContent(_plainTexts[i]), w)) + 2f);
            float total = count * w + (count - 1) * o.PlainCardGap;
            float left = (viewWidth - total) * 0.5f, top = Mathf.Max(34f, (SkillBar.LayoutHeight - h) * 0.5f);
            for (int i = 0; i < count; i++) _plainRects.Add(new Rect(left + i * (w + o.PlainCardGap), top, w, h));
            _plainBox = new Rect(left - 10f, top - 32f, total + 20f, h + 32f + 28f);
        }

        /// <summary>The plain box's words of card <paramref name="index"/> and where it stands (720 layout, once drawn). For tests.</summary>
        public string PlainCardText(int index) => index >= 0 && index < _plainTexts.Count ? _plainTexts[index] : "";
        public Rect PlainCardRect(int index) => index >= 0 && index < _plainRects.Count ? _plainRects[index] : default;
        public GUIStyle PlainCardStyle => _plainCardStyle;

        /// <summary>The old look's offer with the pop-up on: a dark veil, and the cards' words in plain boxes side by side in the middle.</summary>
        void DrawPlainPopUp(float viewWidth)
        {
            int count = PlainCards();
            if (count == 0) return;
            PlainLayout(count, viewWidth);
            Color before = GUI.color;
            if (Offers.Veil > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, Mathf.Clamp01(Offers.Veil) * before.a);
                GUI.DrawTexture(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), Texture2D.whiteTexture);
                GUI.color = before;
            }
            GUI.Box(_plainBox, _plainTitle, _style);
            for (int i = 0; i < count; i++)
            {
                Rect card = _plainRects[i];
                bool picked = GUI.Button(card, _plainTexts[i], _plainCardStyle);
                if (Choice == ChoiceKind.Chest && _offeredUpgrades[i].Tiered) TierEdge(card, _offeredUpgrades[i].Tier);
                else if (Choice == ChoiceKind.FloorReward && OfferedTierAt(i) is SkillTierOffer tier && tier.Tiered) TierEdge(card, tier.Tier);
                if (!picked) continue;
                if (Choice == ChoiceKind.FloorReward) PickReward(i);
                else if (Choice == ChoiceKind.SkillUpgrade) PickSkillUpgrade(i);
                else if (Choice == ChoiceKind.FocusChoice) PickFocusChoice(i);
                else PickUpgrade(i);
                return;
            }
            GUI.Label(new Rect(_plainBox.x + 12f, _plainBox.yMax - 24f, _plainBox.width - 24f, 20f), _plainFooter);
        }
    }
}
