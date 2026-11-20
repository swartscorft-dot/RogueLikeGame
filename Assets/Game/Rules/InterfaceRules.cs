using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>A colour with its opacity, as picked (sRGB), for the interface's palette (work order 045).</summary>
    [Serializable]
    public struct Rgba
    {
        public float R, G, B, A;

        public Rgba(float r, float g, float b, float a = 1f)
        {
            R = r;
            G = g;
            B = b;
            A = a;
        }

        public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.###}, {1:0.###}, {2:0.###}, {3:0.###})", R, G, B, A);
    }

    /// <summary>
    /// The interface's one skin (decision 0011, Kristian's choices of 2026-11-20 over four rounds of pictures; work order
    /// 045): kinetic, small, at the figure. Slanted shapes with hard diagonal cuts, the heavy condensed italic for the words
    /// and numbers, flat white, black, one hot teal and one red; gold and red keep their meanings for treasure and elite,
    /// the exit keeps its colour. Every size, offset and colour of it is here, with its default in code: no asset holds
    /// them, and the Inspector reaches them while playing (InterfaceStyle). Sizes are pixels of a view 1080 high, scaled
    /// to the real one, taken from the first small kinetic picture at 1080 and made about 0.85 of it ("hakket mindre enn 1").
    /// First guesses: nobody has seen them in the game.
    /// </summary>
    [Serializable]
    public sealed class InterfaceValues
    {
        /// <summary>The master switch: on, the whole interface in the kinetic skin; off, every piece drawn exactly as before.</summary>
        public bool Kinetic = true;
        /// <summary>The height the sizes are laid out for.</summary>
        public float LayoutHeight = 1080f;
        /// <summary>How far a slanted shape leans: its top this share of its height to the right of its bottom.</summary>
        public float Slant = 0.32f;

        // ---------------- The palette (the type sheet's: white, hot teal, red, black) ----------------
        public Rgba White = new Rgba(1f, 1f, 1f);
        public Rgba Teal = new Rgba(0.05f, 0.95f, 0.88f);
        public Rgba Red = new Rgba(0.96f, 0.14f, 0.24f);
        public Rgba Black = new Rgba(0.02f, 0.02f, 0.03f);
        /// <summary>The dark back under a stroke, a bar, a slot.</summary>
        public Rgba Back = new Rgba(0.02f, 0.02f, 0.03f, 0.62f);
        /// <summary>A screen's panel behind its text (pause, end, between runs, a choice's heading): darker, still flat.</summary>
        public Rgba Panel = new Rgba(0.02f, 0.02f, 0.03f, 0.82f);
        /// <summary>A skill waiting for its cooldown, and quiet words.</summary>
        public Rgba Grey = new Rgba(0.58f, 0.6f, 0.62f);
        /// <summary>A word's shadow, one offset copy under it so it reads over a bright floor.</summary>
        public Rgba Shadow = new Rgba(0f, 0f, 0f, 0.85f);
        /// <summary>Treasure's gold (the elite's tint and a skill's upgrade pips too: gold keeps its meaning).</summary>
        public Rgba Gold = new Rgba(1f, 0.86f, 0.22f);
        /// <summary>The mark pips over an enemy: the marks' own orange (the cap is <see cref="Red"/>), a free one faint, Tag Round's green.</summary>
        public Rgba Marks = new Rgba(1f, 0.55f, 0.1f);
        public Rgba MarkFree = new Rgba(1f, 1f, 1f, 0.28f);
        public Rgba Tag = new Rgba(0.2f, 1f, 0.45f);
        /// <summary>A stagger line, and its flash while dazed (the daze tint's light blue keeps its meaning).</summary>
        public Rgba Stagger = new Rgba(1f, 0.85f, 0.45f);
        public Rgba Dazed = new Rgba(0.55f, 0.75f, 1f);
        /// <summary>LATE: a miss, quieter than CLEAN's white.</summary>
        public Rgba Late = new Rgba(0.78f, 0.8f, 0.82f);

        // ---------------- The fonts (Barlow Condensed, SIL Open Font License, in Resources/UI/Fonts) ----------------
        public string HeavyFont = "UI/Fonts/BarlowCondensed-ExtraBoldItalic";
        public string LightFont = "UI/Fonts/BarlowCondensed-SemiBoldItalic";
        /// <summary>The skill icons: Resources/UI/Icons/&lt;the skill's name without spaces&gt; (PNG bytes), made masks this many pixels square.</summary>
        public string IconFolder = "UI/Icons/";
        public int IconMaskSize = 64;

        // ---------------- 1. At the figure: health and focus under her feet (the second picture) ----------------
        public float StrokeWidth = 70f;
        public float StrokeHeight = 7f;
        public float StrokeGap = 8f;
        /// <summary>The strokes' top this far under the point her feet stand on.</summary>
        public float UnderFeet = 16f;
        /// <summary>The one number for health, right of the strokes.</summary>
        public int HealthNumberSize = 17;
        public float NumberGap = 6f;
        /// <summary>The white of health just lost, fading over this many seconds of real time.</summary>
        public float LostSeconds = 0.7f;
        /// <summary>A full focus stroke's glow round it, and Hair Trigger's gap between its parts.</summary>
        public float GlowSize = 2f;
        public float PartGap = 3f;
        /// <summary>The small words under the focus stroke while a window or a priming runs ("PRIMED 2.1").</summary>
        public int StateWordSize = 13;

        // ---------------- 1. At the figure: the cast blade over her head ----------------
        public float BladeWidth = 150f;
        public float BladeHeight = 10f;
        /// <summary>The blade's bottom this far over the top of her head.</summary>
        public float OverHead = 18f;
        public int BladeNameSize = 15;
        public float MarkerWidth = 3f;

        // ---------------- 2. Over enemies (the bars' sizes are order 042's EnemyBarValues) and the boss ----------------
        public float BossWidth = 620f;
        public float BossHeight = 13f;
        public float BossStaggerHeight = 4f;
        public float BossTop = 58f;
        public int BossNameSize = 26;

        // ---------------- 3. The skills: the strip at the bottom centre ----------------
        public float SlotWidth = 56f;
        public float SlotHeight = 54f;
        public float SlotGap = 5f;
        /// <summary>The strip's bottom this far over the bottom of the view.</summary>
        public float StripBottom = 22f;
        public float IconSize = 34f;
        public int KeySize = 13;
        public int CooldownSize = 21;
        public int ShortNameSize = 12;
        /// <summary>The cooldown's thin sweep along the slot's foot, the recast's strip over the slot, a skill upgrade's pip.</summary>
        public float SweepHeight = 3f;
        public float RecastHeight = 4f;
        public float PipSize = 5f;
        /// <summary>The ends of the strip: the dash with its charges on the left, focus on the right, cover beside the dash.</summary>
        public float EndWidth = 44f;
        public float EndGap = 9f;

        // ---------------- 4. The map (order 041's place and size; drawn in thin outlines) ----------------
        /// <summary>The outlines' thickness, in pixels of the map's own 720-high layout.</summary>
        public float MapLine = 1f;
        public float MapHereFill = 0.55f;
        public float MapEnteredFill = 0.12f;
        public float MapFrameLine = 1.5f;

        // ---------------- 5. A choice: slim tilted cards at the right edge ----------------
        public float CardWidth = 300f;
        public float CardHeight = 100f;
        public float FocusCardHeight = 150f;
        public float CardGap = 12f;
        /// <summary>The cards' right edge this far from the view's right edge.</summary>
        public float CardRight = 24f;
        public int NumeralSize = 50;
        public int CardTitleSize = 22;
        public int CardTextSize = 15;
        public int ChoiceTitleSize = 24;
        /// <summary>A card dim while the guard holds; and how long a pick flashes, in real seconds.</summary>
        public float CardDim = 0.4f;
        public float PickFlashSeconds = 0.3f;

        // ---------------- 6. Words and numbers (the type sheet, calmer) ----------------
        public int WordSize = 30;
        public int BigWordSize = 36;
        public int SmallWordSize = 22;
        public int NumberSize = 20;
        public int StrongNumberSize = 25;
        public int HurtNumberSize = 22;
        /// <summary>The plate behind PERFECT and COUNTER: this much wider and taller than the word.</summary>
        public float PlatePadX = 12f;
        public float PlatePadY = 0f;
        /// <summary>The small red slash behind a perfect hit's number: its height against the number's and its thickness.</summary>
        public float SlashShare = 0.7f;
        public float SlashThickness = 5f;
        public float ShadowOffset = 2f;

        // ---------------- 7. Screens and prompts ----------------
        /// <summary>The screens' heading, running text, small text and buttons (pause, end, between runs, a choice's footer).</summary>
        public int HeadingSize = 46;
        public int TextSize = 23;
        public int SmallTextSize = 18;
        public int ButtonTextSize = 23;
        public float RuleThickness = 2f;
        public int RunFloorSize = 24;
        public int RunLineSize = 17;
        public int PromptSize = 18;
        public float PromptKeyWidth = 26f;
    }

    /// <summary>A rectangle of the interface, in pixels with 0,0 at the top left (a GUI rectangle without the engine).</summary>
    public struct UiBox
    {
        public float X, Y, W, H;

        public UiBox(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }

        public float Right => X + W;
        public float Bottom => Y + H;
        public float CenterX => X + W * 0.5f;
        public float CenterY => Y + H * 0.5f;
    }

    /// <summary>The colours of the palette a piece asks for by name (work order 045).</summary>
    public enum UiInk { White, Teal, Red, Black, Grey, Gold, Marks, Dazed, Late }

    /// <summary>How a skill's slot on the strip shows (work order 045).</summary>
    public enum SlotLook
    {
        /// <summary>A key with no skill on it yet: its key only, faint.</summary>
        Empty,
        /// <summary>Ready: the icon white.</summary>
        Ready,
        /// <summary>In use, or with a recast or a charge waiting: the icon teal.</summary>
        Live,
        /// <summary>On cooldown: the icon grey, a small number and a sweep.</summary>
        Cooling,
    }

    /// <summary>
    /// The rules of the interface's skin (decision 0011; work order 045), with no engine in them: where the pieces at the
    /// figure sit, how big things are at a given height, which words get a plate, which colour a number gets, how a skill's
    /// slot shows, the icons' names and masks.
    /// </summary>
    public static class InterfaceRules
    {
        // ---------------- Sizes ----------------

        /// <summary>Real pixels per layout pixel for a view <paramref name="screenHeight"/> high: 1 at 1080, 2/3 at 720.</summary>
        public static float Scale(float screenHeight, InterfaceValues v) => screenHeight / Math.Max(1f, (v ?? new InterfaceValues()).LayoutHeight);

        /// <summary>A size of the layout in real pixels for a view <paramref name="screenHeight"/> high.</summary>
        public static float Px(float layoutPixels, float screenHeight, InterfaceValues v) => layoutPixels * Scale(screenHeight, v);

        /// <summary>How far a slanted shape <paramref name="height"/> high leans over its height.</summary>
        public static float Lean(float height, InterfaceValues v) => height * v.Slant;

        // ---------------- 1. At the figure ----------------

        /// <summary>
        /// The two strokes under her feet, in layout pixels: side by side, centred under <paramref name="feetX"/>, their top
        /// <see cref="InterfaceValues.UnderFeet"/> under <paramref name="feetY"/> (both layout pixels, top left 0,0). Health
        /// left, focus right.
        /// </summary>
        public static UiBox HealthStroke(float feetX, float feetY, InterfaceValues v)
        {
            float total = 2f * v.StrokeWidth + v.StrokeGap;
            return new UiBox(feetX - total * 0.5f, feetY + v.UnderFeet, v.StrokeWidth, v.StrokeHeight);
        }

        public static UiBox FocusStroke(float feetX, float feetY, InterfaceValues v)
        {
            UiBox h = HealthStroke(feetX, feetY, v);
            return new UiBox(h.Right + v.StrokeGap, h.Y, v.StrokeWidth, v.StrokeHeight);
        }

        /// <summary>The health number's box: right of the focus stroke, its middle on the strokes' middle.</summary>
        public static UiBox HealthNumber(float feetX, float feetY, InterfaceValues v)
        {
            UiBox f = FocusStroke(feetX, feetY, v);
            float h = v.HealthNumberSize * 1.3f;
            return new UiBox(f.Right + v.NumberGap + Lean(v.StrokeHeight, v) * 0.5f, f.CenterY - h * 0.5f, v.HealthNumberSize * 3f, h);
        }

        /// <summary>The cast blade: centred over <paramref name="headX"/>, its bottom <see cref="InterfaceValues.OverHead"/> over <paramref name="headY"/>.</summary>
        public static UiBox Blade(float headX, float headY, InterfaceValues v) =>
            new UiBox(headX - v.BladeWidth * 0.5f, headY - v.OverHead - v.BladeHeight, v.BladeWidth, v.BladeHeight);

        /// <summary>
        /// The white of health just lost: its share of the stroke from the health now up to where it was, and its opacity,
        /// fading from 1 to 0 over <see cref="InterfaceValues.LostSeconds"/> after the last loss. Nothing once it has faded.
        /// </summary>
        public static float LostAlpha(float sinceLoss, InterfaceValues v)
        {
            if (sinceLoss < 0f || v.LostSeconds <= 0f || sinceLoss >= v.LostSeconds) return 0f;
            return 1f - sinceLoss / v.LostSeconds;
        }

        /// <summary>
        /// Where the white of lost health reaches from after a frame: on a loss, up to the higher of where it was shown (if it
        /// still shows) and the health before; it is let go once it has faded or health is back over it.
        /// </summary>
        public static float LostFrom(float lostFrom, float fillBefore, float fillNow, float sinceLoss, InterfaceValues v)
        {
            if (fillNow < fillBefore - 1e-5f) return Math.Max(LostAlpha(sinceLoss, v) > 0f ? lostFrom : 0f, fillBefore);
            if (LostAlpha(sinceLoss, v) <= 0f || fillNow >= lostFrom) return fillNow;
            return lostFrom;
        }

        // ---------------- 3. The strip ----------------

        /// <summary>The slot of key <paramref name="key"/> of <paramref name="count"/>, in layout pixels, for a view <paramref name="viewWidth"/> wide.</summary>
        public static UiBox Slot(int key, int count, float viewWidth, InterfaceValues v)
        {
            float total = count * v.SlotWidth + Math.Max(0, count - 1) * v.SlotGap;
            float left = (viewWidth - total) * 0.5f;
            float top = v.LayoutHeight - v.StripBottom - v.SlotHeight;
            return new UiBox(left + key * (v.SlotWidth + v.SlotGap), top, v.SlotWidth, v.SlotHeight);
        }

        /// <summary>The strip's left end (the dash) and right end (focus), outside the slots.</summary>
        public static UiBox LeftEnd(int count, float viewWidth, InterfaceValues v)
        {
            UiBox first = Slot(0, count, viewWidth, v);
            return new UiBox(first.X - v.EndGap - v.EndWidth, first.Y, v.EndWidth, first.H);
        }

        public static UiBox RightEnd(int count, float viewWidth, InterfaceValues v)
        {
            UiBox last = Slot(Math.Max(0, count - 1), count, viewWidth, v);
            return new UiBox(last.Right + v.EndGap, last.Y, v.EndWidth, last.H);
        }

        /// <summary>How a skill's slot shows: empty, in use or with something waiting (teal), on cooldown (grey), or ready (white).</summary>
        public static SlotLook Look(bool hasSkill, bool inUse, bool waiting, float cooldown) =>
            !hasSkill ? SlotLook.Empty : inUse || waiting ? SlotLook.Live : cooldown > 0f ? SlotLook.Cooling : SlotLook.Ready;

        /// <summary>The cooldown's sweep: the share still to run, 1 just used to 0 ready.</summary>
        public static float Sweep(float remaining, float total) => total <= 0f ? 0f : Clamp01(remaining / total);

        /// <summary>
        /// The cooldown's number, as on the type sheet: tenths under ten seconds ("3.2"), whole seconds from ten ("12"),
        /// rounded up so it never says 0.0 while it is not ready. Empty when ready.
        /// </summary>
        public static string CooldownText(float seconds)
        {
            if (seconds <= 0f) return "";
            int tenths = CooldownTenths(seconds);
            return tenths < 100
                ? (tenths / 10).ToString(CultureInfo.InvariantCulture) + "." + (tenths % 10).ToString(CultureInfo.InvariantCulture)
                : (tenths / 10).ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>The cooldown in tenths as it is shown (whole seconds from ten), for a table of the texts made once; 0 when ready.</summary>
        public static int CooldownTenths(float seconds)
        {
            if (seconds <= 0f) return 0;
            int tenths = (int)Math.Ceiling(seconds * 10f - 1e-4f);
            if (tenths < 1) tenths = 1;
            if (tenths >= 100) tenths = (int)Math.Ceiling(seconds - 1e-4f) * 10;
            return tenths;
        }

        // ---------------- 5. A choice ----------------

        /// <summary>
        /// Card <paramref name="index"/> of <paramref name="count"/> at the right edge, in layout pixels: stacked down, the
        /// stack's middle a little over the view's, each <paramref name="height"/> high.
        /// </summary>
        public static UiBox Card(int index, int count, float height, float viewWidth, InterfaceValues v)
        {
            float total = count * height + Math.Max(0, count - 1) * v.CardGap;
            float top = Math.Max(v.LayoutHeight * 0.12f, (v.LayoutHeight - total) * 0.5f - v.LayoutHeight * 0.04f);
            float x = viewWidth - v.CardRight - v.CardWidth - Lean(height, v);
            return new UiBox(x, top + index * (height + v.CardGap), v.CardWidth, height);
        }

        static readonly string[] s_Numerals = { "01", "02", "03", "04", "05", "06", "07", "08", "09" };

        /// <summary>A card's large numeral: 01, 02, 03.</summary>
        public static string Numeral(int index) => index >= 0 && index < s_Numerals.Length ? s_Numerals[index] : (index + 1).ToString(CultureInfo.InvariantCulture);

        // ---------------- 6. Words and numbers ----------------

        /// <summary>A plate behind the word, only for the two that matter most: PERFECT and COUNTER (decision 0011, "roligere").</summary>
        public static bool HasPlate(FeedbackMoment moment) => moment == FeedbackMoment.Perfect || moment == FeedbackMoment.Counter;

        /// <summary>The plate's colour: PERFECT teal, COUNTER white, as on the type sheet. The word on it is black.</summary>
        public static UiInk PlateInk(FeedbackMoment moment) => moment == FeedbackMoment.Perfect ? UiInk.Teal : UiInk.White;

        /// <summary>
        /// A word's colour, from the type sheet: CLEAN white, EARLY red, LATE a quieter white, DUCKED teal, STUNNED and PULLED
        /// red; the marks spent white, red at the cap; BOOM white; DAZED the daze tint's blue; on a plate, black.
        /// </summary>
        public static UiInk WordInk(FeedbackMoment moment, bool atCap)
        {
            switch (moment)
            {
                case FeedbackMoment.Perfect:
                case FeedbackMoment.Counter: return UiInk.Black;
                case FeedbackMoment.Early:
                case FeedbackMoment.Stunned:
                case FeedbackMoment.Pulled: return UiInk.Red;
                case FeedbackMoment.Late: return UiInk.Late;
                case FeedbackMoment.Ducked: return UiInk.Teal;
                case FeedbackMoment.Dazed: return UiInk.Dazed;
                case FeedbackMoment.MarksSpent: return atCap ? UiInk.Red : UiInk.White;
                default: return UiInk.White; // CLEAN, BOOM
            }
        }

        /// <summary>A word's size: PERFECT and the marks at the cap large, EARLY and LATE small, the rest between.</summary>
        public static int WordSize(FeedbackSize size, InterfaceValues v) =>
            size == FeedbackSize.Large ? v.BigWordSize : size == FeedbackSize.Small ? v.SmallWordSize : v.WordSize;

        /// <summary>
        /// A damage number's colour (smaller than the sheet's, never on a plate): an ordinary hit white, a strong one (marks
        /// spent, a dazed target) teal, a perfect hit white over its red slash, damage she takes red, health back teal.
        /// </summary>
        public static UiInk NumberInk(NumberKind kind)
        {
            switch (kind)
            {
                case NumberKind.Dazed:
                case NumberKind.Marks:
                case NumberKind.MarksFull:
                case NumberKind.Heal: return UiInk.Teal;
                case NumberKind.Hurt: return UiInk.Red;
                default: return UiInk.White; // Plain, Perfect
            }
        }

        /// <summary>The small red slash behind a number: a perfect hit's only.</summary>
        public static bool NumberSlash(NumberKind kind) => kind == NumberKind.Perfect;

        /// <summary>A number's size: ordinary small, strong and perfect a little larger, hers between.</summary>
        public static int NumberSize(NumberKind kind, InterfaceValues v) =>
            kind == NumberKind.Hurt ? v.HurtNumberSize
            : kind == NumberKind.Plain || kind == NumberKind.Heal ? v.NumberSize
            : v.StrongNumberSize;

        // ---------------- The icons ----------------

        /// <summary>The name an icon is found by: the skill's name without its spaces ("Triple Tap" is TripleTap).</summary>
        public static string IconName(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return "";
            var chars = new char[skillName.Length];
            int n = 0;
            foreach (char c in skillName)
                if (!char.IsWhiteSpace(c)) chars[n++] = c;
            return new string(chars, 0, n);
        }

        /// <summary>The short name a slot shows with its key when the skill has no icon: its first word in capitals, at most eight letters.</summary>
        public static string ShortName(string skillName)
        {
            if (string.IsNullOrEmpty(skillName)) return "";
            string first = skillName.Trim();
            int space = first.IndexOf(' ');
            if (space > 0) first = first.Substring(0, space);
            if (first.Length > 8) first = first.Substring(0, 8);
            return first.ToUpperInvariant();
        }

        /// <summary>A pixel's brightness, 0 to 255: what its alpha becomes in the icon's mask (Rec. 601 weights).</summary>
        public static byte Brightness(byte r, byte g, byte b) => (byte)Math.Min(255, (int)Math.Round(0.299 * r + 0.587 * g + 0.114 * b));

        /// <summary>
        /// The mask of an icon: its brightness (<paramref name="grey"/>, <paramref name="width"/> by <paramref name="height"/>,
        /// rows from the bottom as the engine gives them) averaged down into <paramref name="size"/> by <paramref name="size"/>,
        /// each cell the mean of the pixels it covers. The result is the mask's alpha, on white.
        /// </summary>
        public static byte[] Mask(byte[] grey, int width, int height, int size)
        {
            size = Math.Max(1, size);
            var mask = new byte[size * size];
            if (grey == null || width <= 0 || height <= 0 || grey.Length < width * height) return mask;
            for (int y = 0; y < size; y++)
            {
                int y0 = y * height / size, y1 = Math.Max(y0 + 1, (y + 1) * height / size);
                for (int x = 0; x < size; x++)
                {
                    int x0 = x * width / size, x1 = Math.Max(x0 + 1, (x + 1) * width / size);
                    long sum = 0;
                    int count = 0;
                    for (int sy = y0; sy < y1 && sy < height; sy++)
                    {
                        int row = sy * width;
                        for (int sx = x0; sx < x1 && sx < width; sx++)
                        {
                            sum += grey[row + sx];
                            count++;
                        }
                    }
                    mask[y * size + x] = (byte)(count > 0 ? (sum + count / 2) / count : 0);
                }
            }
            return mask;
        }

        static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;
    }
}
