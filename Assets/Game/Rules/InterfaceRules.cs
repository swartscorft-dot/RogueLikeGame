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

        // ---------------- 8. Larger, higher, under her feet (work order 063) ----------------
        // His words of 2026-11-21 after two runs: "uiet er ikke helt på plass. må være litt større. vanskelig å se
        // coolddowns nede. så litt høyere opp og en del større"; "hp indikator og focus er for høyt på karakteren, må være
        // mer under"; "gjerne en liten visual på spell uiet når noe er av cd". His new word overrides decision 0011's "a
        // notch smaller". Every value here at 1 (a scale), 0 (a lift, a gap in metres) or off gives this morning's look.

        /// <summary>
        /// The master scale of the interface in a fight: the strip, the strokes at her feet, the cast blade, the run box and
        /// the bars over enemies all grow with it, so the whole is turned up or down with one number. 1 is this morning's.
        /// </summary>
        public float FightScale = 1.25f;
        /// <summary>The strip's own factor on top of <see cref="FightScale"/>: 1.25 × 1.28 = 1.6 times this morning's boxes (work order 063: "about 1.6").</summary>
        public float StripScale = 1.28f;
        /// <summary>
        /// The strip's bottom edge this share of the view's height over the view's bottom (9 %: 97 layout pixels at 1080,
        /// this morning 22, 2 %), so a cooldown is read in the corner of the eye; never closer than <see cref="StripBottom"/>.
        /// 0 is this morning's.
        /// </summary>
        public float StripLift = 0.09f;
        /// <summary>The cooldown's number on its own factor over the strip's, the largest thing in a cooling box: 21 × 1.6 × 1.25 = 42 (its digits about 29 layout pixels high at 1080). 1 with the scales at 1 is this morning's 21.</summary>
        public float CooldownGrow = 1.25f;
        /// <summary>A cooling box's dark shade over the share of the cooldown still to run, from its top down, so "not ready" reads without the number. Clear is this morning's.</summary>
        public Rgba CoolingShade = new Rgba(0.02f, 0.02f, 0.03f, 0.6f);
        /// <summary>A cooling skill's icon this opaque (this morning 1: grey only).</summary>
        public float CoolingIconAlpha = 0.5f;

        /// <summary>A skill coming off cooldown kicks its box along the strip's slant and flashes its edge teal, once (work order 063); off is this morning's.</summary>
        public bool ReadyFlash = true;
        /// <summary>How long the kick and the flash last, in real seconds.</summary>
        public float ReadyFlashSeconds = 0.25f;
        /// <summary>How far the box jumps along the slant, in layout pixels at the strip's scale of 1 (grows with the strip), before it settles.</summary>
        public float ReadyKick = 6f;
        /// <summary>The share of <see cref="ReadyFlashSeconds"/> the jump takes; the rest it settles back.</summary>
        public float ReadyKickRise = 0.15f;
        /// <summary>The flash's edge round the box, in layout pixels at the strip's scale of 1.</summary>
        public float ReadyEdge = 3f;

        /// <summary>
        /// The strokes' top this far under the lowest point of her boots as the camera sees it, in metres in the camera's
        /// picture at her depth (converted to pixels every frame, so it holds at every resolution): 0.30 m is about 21.6
        /// layout pixels at 1080 with the game's camera (28 m, 30°). 0 is this morning's <see cref="UnderFeet"/>: 16 pixels
        /// under her point. Her point (the character's position) stands about 1.05 m over her soles (measured 2026-11-21),
        /// so this morning's strokes, 16 pixels under it, lay over her shins: that was "for høyt på karakteren".
        /// </summary>
        public float UnderFeetMetres = 0.30f;
        /// <summary>
        /// How far her boots reach from the point under her middle toward the camera, on the floor, in metres: the lowest a
        /// boot shows on the screen. Measured 2026-11-21 standing, facing four ways: up to 0.41 m (a boot's toe facing the
        /// camera); 0.45 keeps the strokes clear by the gap while she stands, steady as she turns and runs (a constant, not
        /// her animated feet, so the strokes do not bob with her steps).
        /// </summary>
        public float BootReach = 0.45f;
        /// <summary>
        /// How far under her point (the character's position) her soles are, in metres: measured 2026-11-21 in the
        /// ControlRoom from her foot and toe bones (1.01 to 1.05 m) and her body's lowest vertex (1.09 m). A named value, not
        /// read from her renderers each run: their bounds are not kept up to date where nothing is drawn (a test run gave
        /// 1.23 m).
        /// </summary>
        public float SoleDrop = 1.05f;

        // The recast window on the skill's own box (work order 063, item 2b: Backstep's clean press fell from 16 of 17 to 6
        // of 20 when the cast bar under the middle became the blade over her head that hops with her).
        /// <summary>A box whose recast window is open grows this much about its middle (1: this morning's).</summary>
        public float RecastGrow = 1.12f;
        /// <summary>The window's bar across the lit box, in layout pixels at the strip's scale of 1.</summary>
        public float RecastBarHeight = 7f;
        /// <summary>The key's label on the lit box's face this many times the key's size (where the old bar wrote "shoot").</summary>
        public float RecastKeyGrow = 2.2f;
        /// <summary>
        /// The run box's own factor on top of <see cref="FightScale"/>. 0.8 (1.25 × 0.8 = 1: this morning's size) because the
        /// small map stands at a fixed 278 pixels of the 720 layout left of the view's right edge (FloorMap, not this order's)
        /// and a larger run box would run into it; raise it with the map moved.
        /// </summary>
        public float RunBoxScale = 0.8f;
        /// <summary>The cast blade's own factor on top of <see cref="FightScale"/> (the producer's first guess of 2026-11-21: 1.5). 1 is this morning's.</summary>
        public float BladeScale = 1.5f;

        // ---------------- 9. Less flat, nearer the first kinetic picture (work order 063, item 4) ----------------
        // His words of 2026-11-21: "også er den ikke helt lik som på konseptet ... mer av konsept uiet vi først gikk for,
        // syntes denne var for flat sammenlignet med den" (ui_b_kinetic_anime.png, and ui_kinetic_small_1.png). Each at 0 or
        // off gives this morning's flat look.

        /// <summary>
        /// The dark plate behind a slanted box (a strip box and its ends, the strokes, the blade, an enemy's bar): moved down
        /// and right by this many layout pixels at a scale of 1 (it grows with its piece), so the box stands off the floor.
        /// 0: flat.
        /// </summary>
        public float Depth = 3f;
        /// <summary>That plate this much larger than its box on every side at a scale of 1: a hard black outline. 0: none.</summary>
        public float Outline = 1.5f;
        /// <summary>The plate's ink: the palette's black, nearly opaque.</summary>
        public Rgba DepthInk = new Rgba(0.02f, 0.02f, 0.03f, 0.92f);
        /// <summary>One dark slanted plate behind the whole strip, edged in a thin white line, as in both pictures; off: the boxes float apart.</summary>
        public bool StripPlate = true;
        /// <summary>The strip's plate this far round its boxes, and its white edge this thick, at the strip's scale of 1.</summary>
        public float StripPlatePad = 5f;
        public float StripEdge = 1f;
        /// <summary>A thin white line round each box's face on the strip's plate, this opaque, so the boxes read apart on it as in the pictures. 0: none.</summary>
        public float CellEdge = 0.35f;
        /// <summary>The hot stripes along a cut edge (teal at the strip's left end, red at its right; two red slashes before the blade), this wide at a scale of 1. 0: none.</summary>
        public float Slash = 4f;
        /// <summary>The halftone patch at the strip's top left corner (teal dots shrinking along the cut), this opaque. 0: none.</summary>
        public float Halftone = 0.6f;
        /// <summary>The halftone patch's size, at the strip's scale of 1.</summary>
        public float HalftoneSize = 40f;

        // ---------------- 10. The concept's style, built by looking at it (work order 068) ----------------
        // His words of the evening of 2026-11-21: "healthbar og focuc er øvert, så upgrade kommer under de. syntes de også er
        // for store. fortsatt ikke samme stil som konsept UIet (de to barene altså). skuffet. det føles fortsatt litt flat, ikke
        // nok anime slik som i konseptet"; "må tydliggjøre den combo baren"; asked, "Begge: plate oppe, små under". The target
        // is ui_b_kinetic_anime.png; these numbers were set by looking at pictures of the game beside it (decision 0011's note
        // on taking pictures). Layout pixels of a view 1080 high.

        /// <summary>The plate at the top left (her health as a number on a red plate, focus under it): on; off, only the strokes at her feet, as this morning.</summary>
        public bool TopPlate = true;
        /// <summary>The top plate's own lean (the concept's plates lean much harder than the strip's boxes: about 0.6 of their height).</summary>
        public float PlateSlant = 0.6f;
        /// <summary>
        /// The dark wedge in the screen's corner where a portrait would stand (none is drawn; room is kept): its width and
        /// height. The morning of 2026-11-22 (the producer, from round 4: "a large black wedge that covers the screen's
        /// corner"; the concept's dark shapes are compact): 120 by 150, the plate moved left with it (round 4's: 190 by 176,
        /// the plate at 214, the bar at 300).
        /// </summary>
        public float CornerWidth = 120f;
        public float CornerHeight = 150f;
        /// <summary>The red health plate: its left edge (at its middle line), top, width and height.</summary>
        public float HealthPlateX = 160f;
        public float HealthPlateY = 40f;
        public float HealthPlateWidth = 330f;
        public float HealthPlateHeight = 62f;
        /// <summary>The number on it ("72 / 100"), the heavy italic.</summary>
        public int PlateNumberSize = 54;
        /// <summary>The black outline round every plate of the concept's style, at a scale of 1 (much heavier than <see cref="Outline"/>).</summary>
        public float HeavyOutline = 4f;
        /// <summary>
        /// The stroke outside the black outline (white or teal), at a scale of 1: the concept's panels have a heavy black edge
        /// with a light line outside it (the producer, from round 4: "line weight is most of what anime means here"). 0: the
        /// black outline alone, as round 4.
        /// </summary>
        public float OuterStroke = 2.5f;
        /// <summary>The share of the red plate, from its right end, the halftone dots fade across.</summary>
        public float PlateDots = 0.42f;
        /// <summary>The word FOCUS and the bar under the red plate: the word's size, the bar's left (at its middle line), top, width and height.</summary>
        public int FocusWordSize = 26;
        public float FocusBarX = 246f;
        public float FocusBarY = 116f;
        public float FocusBarWidth = 230f;
        public float FocusBarHeight = 20f;
        /// <summary>A full gauge's pulse: how fast (per second) and how far (0 to 1) its white edge breathes.</summary>
        public float FullPulseRate = 3.2f;
        public float FullPulse = 0.5f;
        /// <summary>A hit she takes: the plate flashes white and shakes for this many real seconds, this many layout pixels.</summary>
        public float HitFlashSeconds = 0.22f;
        public float HitShake = 7f;

        /// <summary>
        /// The strokes at her feet this many times work order 063's width ("syntes de også er for store"; his choice: small
        /// strokes under her as well as the plate): 2/3, no taller; 1 is 063's. No number beside them (<see cref="StrokeNumber"/>).
        /// </summary>
        public float StrokeShrink = 2f / 3f;
        /// <summary>The health number beside the strokes (063's); off with the plate on: the number is on the plate.</summary>
        public bool StrokeNumber = false;
        /// <summary>
        /// The strokes drawn in the plate's language (work order 068, the morning of 2026-11-22: round 3's picture showed them
        /// as two thin flat lines, the focus one black and gone while the gauge was empty): leaning by <see cref="StrokeSlant"/>,
        /// the heavy black outline <see cref="StrokeOutline"/> (at the strip's scale) round each, red and teal with a white
        /// inner edge, an empty gauge's slot a dim teal. Off: work order 063's drawing.
        /// </summary>
        public bool StrokeConcept = true;
        public float StrokeSlant = 0.5f;
        public float StrokeOutline = 2f;

        /// <summary>
        /// The combo bar (the concept's "BLADE TIMING" bar): THE place a recast chain, a charge, a hold and a cast are read, wide
        /// at the bottom middle over the strip; on. Off: none, and the blade over her head as before.
        /// </summary>
        public bool ComboBar = true;
        /// <summary>The combo bar's width and height, and its bottom this far over the strip's plate.</summary>
        public float ComboWidth = 860f;
        public float ComboHeight = 30f;
        public float ComboOverStrip = 34f;
        /// <summary>The plate on its left with the skill's name, its width, and the name's size.</summary>
        public float ComboLabelWidth = 210f;
        public int ComboLabelSize = 26;
        /// <summary>The press numbers over the zones, and the tag over the open one ("PRESS" and its key).</summary>
        public int ComboTagSize = 19;
        /// <summary>
        /// The press numbers ("1", "2", "3") over the combo bar's zones, the heavy italic (work order 068, the morning of
        /// 2026-11-22: in round 3's pictures, at the tag's 19, they read as specks; the concept's numerals are large).
        /// </summary>
        public int ComboNumberSize = 30;
        /// <summary>The combo bar's own lean.</summary>
        public float ComboSlant = 0.5f;
        /// <summary>How long it stays after the last press or the run-out, showing what was hit and missed, and its fade, in real seconds.</summary>
        public float ComboLinger = 0.6f;
        public float ComboFade = 0.25f;
        /// <summary>How long a hit zone flashes white, in real seconds.</summary>
        public float ComboHitFlash = 0.3f;
        /// <summary>The blade over her head (work order 045) while the combo bar is on: off (one place to read); on, both.</summary>
        public bool BladeWithCombo = false;

        /// <summary>The skill strip's boxes outlined in the heavy black (<see cref="HeavyOutline"/>) and its key plates white under each, as in the concept.</summary>
        public float StripOutline = 2.5f;
        /// <summary>
        /// The icon in a strip box this share of the face's height (the concept's icons fill their box); 063's was 34 px. The
        /// morning of 2026-11-22: 1.05 (round 4's 0.86 still read small: the icons' own art has a margin), never wider than
        /// 0.95 of the face.
        /// </summary>
        public float IconFill = 1.05f;
        /// <summary>The key on its white plate under each strip box this many times 063's (the concept's keys are large, bold letters); 1 is 063's.</summary>
        public float StripKeyGrow = 1.25f;
        /// <summary>
        /// The strip as the concept's (the producer, from round 4: "every box FULL of a large cyan icon on black, a bright white
        /// key cap under it, white outlines between the boxes; a box on cooldown grey with the number large; ours: small thin
        /// white icons in mostly black boxes, empty slots black voids"): a ready skill's icon teal (off: white, as round 4);
        /// the white line outside each box's black outline this thick at the strip's scale (0: none); a box cooling on a grey
        /// face this opaque; an empty slot a dim outlined box, its outline white this opaque.
        /// </summary>
        public bool IconTeal = true;
        public float StripWhiteEdge = 2f;
        public float CoolingFace = 0.32f;
        public float EmptyEdge = 0.35f;
        /// <summary>
        /// The run box at the top right as a compact plate (the producer, from round 4: "a large black area with small thin
        /// text"): its dark plate behind the head and this many lines under it only; the lines after (the time, the upgrades,
        /// the run's code) smaller, on no plate. A large number: the whole box on its plate, as round 4.
        /// </summary>
        public int RunPlateLines = 2;

        /// <summary>
        /// The cards of an offer in the concept's style: the name on a white plate in black italic, the numeral white on black in
        /// a teal frame. (The concept's numerals as tall as half the card would move the offer's layout of work order 065: not done.)
        /// </summary>
        public bool CardNamePlate = true;
        /// <summary>The bar over an enemy: a thicker black outline (layout pixels of the bars' own 720 layout).</summary>
        public float EnemyOutline = 1.5f;

        /// <summary>The menus (pause, the run's end, between runs) in the concept's style: work order 078's values (InterfaceMenus.cs).</summary>
        public MenuValues Menus = new MenuValues();
    }

    /// <summary>
    /// Which piece of the interface is drawn over which (work order 068, item 1: "healthbar og focuc er øvert, så upgrade
    /// kommer under de"): the IMGUI depths, said in one place. A lower depth is drawn later, over a higher one. Everything of
    /// the fight's interface (the strip, the strokes, the plate, the combo bar, the enemies' bars, the numbers and words, the
    /// signs, the run box, the map) is under an offer, and an offer is under the pause menu and every screen.
    /// </summary>
    public static class InterfaceDepth
    {
        /// <summary>
        /// The teal frame at the screen's edges while she is empowered (work order 071's FocusEdge, which draws at 20): the
        /// deepest of all, under every piece of the interface.
        /// </summary>
        public const int FocusEdge = 20;
        /// <summary>The bars over the enemies: under everything else of hers.</summary>
        public const int EnemyBars = 10;
        /// <summary>The words, the numbers, the signs at her figure, the cast blade and the combo bar.</summary>
        public const int Words = 2;
        /// <summary>The skill strip, the strokes and the top plate, the map, the run box, a prompt.</summary>
        public const int Fight = 1;
        /// <summary>An offer (a chest's, a floor's, the elite's, the focus choice) and the notices with it.</summary>
        public const int Offer = -5;
        /// <summary>The pause menu, the end screens and the screen between runs.</summary>
        public const int Screens = -10;

        /// <summary>The run's screens' piece (GeneratedLevels): an offer when one is up, else the fight's (its chest prompt, a notice).</summary>
        public static int Levels(bool offerUp) => offerUp ? Offer : Fight;

        /// <summary>The HUD's piece (RunHud): a screen when one is up, else the fight's (its run box and prompt).</summary>
        public static int Hud(bool screenUp) => screenUp ? Screens : Fight;
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

        // ---------------- The scales of a fight (work order 063) ----------------

        /// <summary>The master scale of the interface in a fight (<see cref="InterfaceValues.FightScale"/>), never under 0.1.</summary>
        public static float Fight(InterfaceValues v) => Math.Max(0.1f, v.FightScale);

        /// <summary>The strip's scale: the master scale times the strip's own factor.</summary>
        public static float Strip(InterfaceValues v) => Fight(v) * Math.Max(0.1f, v.StripScale);

        /// <summary>The cast blade's scale: the master scale times the blade's own factor.</summary>
        public static float BladeFactor(InterfaceValues v) => Fight(v) * Math.Max(0.1f, v.BladeScale);

        /// <summary>A font size grown by a scale, at least 1.</summary>
        public static int Size(int size, float scale) => Math.Max(1, (int)Math.Round(size * scale));

        /// <summary>The cooldown's number's font size on the strip: the largest thing in a cooling box.</summary>
        public static int CooldownFont(InterfaceValues v) => Size(v.CooldownSize, Strip(v) * Math.Max(0.1f, v.CooldownGrow));

        /// <summary>The key's label on the strip: readable but small, with the master scale only.</summary>
        public static int KeyFont(InterfaceValues v) => Size(v.KeySize, Fight(v) * Math.Max(0.1f, v.StripKeyGrow));

        /// <summary>The height of the white plate a key sits on, under each box's face.</summary>
        public static float KeyPlateHeight(InterfaceValues v) => KeyFont(v) * 1.25f;

        /// <summary>The distance from the view's bottom to the strip's bottom edge, in layout pixels: the lift's share of the height, never under <see cref="InterfaceValues.StripBottom"/>.</summary>
        public static float StripBottomEdge(InterfaceValues v) => Math.Max(v.StripBottom, Math.Max(0f, v.StripLift) * v.LayoutHeight);

        // ---------------- 1. At the figure ----------------

        /// <summary>
        /// The two strokes under her feet, in layout pixels: side by side, centred under <paramref name="feetX"/>, their top
        /// <see cref="InterfaceValues.UnderFeet"/> (grown with the master scale) under <paramref name="feetY"/> (both layout
        /// pixels, top left 0,0). Health left, focus right.
        /// </summary>
        public static UiBox HealthStroke(float feetX, float feetY, InterfaceValues v) => HealthStroke(feetX, feetY, v.UnderFeet * Fight(v), v);

        /// <summary>
        /// The two strokes with their top <paramref name="under"/> layout pixels under her feet's point (work order 063: the
        /// gap worked out every frame from <see cref="InterfaceValues.UnderFeetMetres"/> through the camera).
        /// </summary>
        public static UiBox HealthStroke(float feetX, float feetY, float under, InterfaceValues v)
        {
            float s = Fight(v);
            float w = v.StrokeWidth * s * StrokeShrink(v), total = 2f * w + v.StrokeGap * s; // work order 068: 2/3 of 063's width, no taller
            return new UiBox(feetX - total * 0.5f, feetY + under, w, v.StrokeHeight * s);
        }

        /// <summary>The strokes' width factor over work order 063's (<see cref="InterfaceValues.StrokeShrink"/>), 0.1 to 4.</summary>
        public static float StrokeShrink(InterfaceValues v) => Math.Max(0.1f, Math.Min(4f, v.StrokeShrink));

        public static UiBox FocusStroke(float feetX, float feetY, InterfaceValues v) => FocusStroke(feetX, feetY, v.UnderFeet * Fight(v), v);

        public static UiBox FocusStroke(float feetX, float feetY, float under, InterfaceValues v)
        {
            UiBox h = HealthStroke(feetX, feetY, under, v);
            return new UiBox(h.Right + v.StrokeGap * Fight(v), h.Y, h.W, h.H);
        }

        /// <summary>The health number's box: right of the focus stroke, its middle on the strokes' middle.</summary>
        public static UiBox HealthNumber(float feetX, float feetY, InterfaceValues v) => HealthNumber(feetX, feetY, v.UnderFeet * Fight(v), v);

        public static UiBox HealthNumber(float feetX, float feetY, float under, InterfaceValues v)
        {
            UiBox f = FocusStroke(feetX, feetY, under, v);
            float size = Size(v.HealthNumberSize, Fight(v));
            float h = size * 1.3f;
            return new UiBox(f.Right + v.NumberGap * Fight(v) + Lean(f.H, v) * 0.5f, f.CenterY - h * 0.5f, size * 3f, h);
        }

        /// <summary>
        /// The strokes' gap under her feet in layout pixels, from her feet's point and the point <see cref="InterfaceValues.UnderFeetMetres"/>
        /// under it in the camera's picture, both on the screen in layout pixels (y down); this morning's pixels when the metres are 0.
        /// </summary>
        public static float UnderFeetGap(float feetY, float metresDownY, InterfaceValues v) =>
            v.UnderFeetMetres > 0f ? Math.Max(0f, metresDownY - feetY) : v.UnderFeet * Fight(v);

        /// <summary>The cast blade: centred over <paramref name="headX"/>, its bottom <see cref="InterfaceValues.OverHead"/> over <paramref name="headY"/>, grown with its scale.</summary>
        public static UiBox Blade(float headX, float headY, InterfaceValues v)
        {
            float b = BladeFactor(v), w = v.BladeWidth * b, h = v.BladeHeight * b;
            return new UiBox(headX - w * 0.5f, headY - v.OverHead * Fight(v) - h, w, h);
        }

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
            float s = Strip(v);
            float w = v.SlotWidth * s, h = v.SlotHeight * s, gap = v.SlotGap * s;
            float total = count * w + Math.Max(0, count - 1) * gap;
            float left = (viewWidth - total) * 0.5f;
            float top = v.LayoutHeight - StripBottomEdge(v) - h;
            return new UiBox(left + key * (w + gap), top, w, h);
        }

        /// <summary>The strip's left end (the dash) and right end (focus), outside the slots.</summary>
        public static UiBox LeftEnd(int count, float viewWidth, InterfaceValues v)
        {
            UiBox first = Slot(0, count, viewWidth, v);
            float s = Strip(v);
            return new UiBox(first.X - v.EndGap * s - v.EndWidth * s, first.Y, v.EndWidth * s, first.H);
        }

        public static UiBox RightEnd(int count, float viewWidth, InterfaceValues v)
        {
            UiBox last = Slot(Math.Max(0, count - 1), count, viewWidth, v);
            float s = Strip(v);
            return new UiBox(last.Right + v.EndGap * s, last.Y, v.EndWidth * s, last.H);
        }

        /// <summary>The cover tag beside the dash (2026-11-19): left of the left end, a little lower than its middle.</summary>
        public static UiBox CoverTag(int count, float viewWidth, InterfaceValues v)
        {
            UiBox left = LeftEnd(count, viewWidth, v);
            float s = Strip(v), w = v.EndWidth * s * 1.3f;
            return new UiBox(left.X - v.EndGap * s - w, left.Y + left.H * 0.3f, w, left.H * 0.4f);
        }

        /// <summary>The whole strip from the cover tag's left to focus's right, for the overlap checks (work order 063).</summary>
        public static UiBox WholeStrip(int count, float viewWidth, InterfaceValues v)
        {
            UiBox cover = CoverTag(count, viewWidth, v), right = RightEnd(count, viewWidth, v);
            float lean = Lean(right.H, v) * 0.5f; // a slanted box leans half its height's lean past each side
            return new UiBox(cover.X - lean, right.Y, right.Right + lean - (cover.X - lean), right.H);
        }

        /// <summary>
        /// The dark plate behind the whole strip (work order 063, item 4): round the dash's box to focus's, the pad's width
        /// round them (inside the gap to the cover tag). Its lean is its own, about its middle.
        /// </summary>
        public static UiBox StripPlate(int count, float viewWidth, InterfaceValues v)
        {
            UiBox left = LeftEnd(count, viewWidth, v), right = RightEnd(count, viewWidth, v);
            float pad = Math.Max(0f, v.StripPlatePad) * Strip(v);
            return new UiBox(left.X - pad, left.Y - pad, right.Right + pad - (left.X - pad), left.H + 2f * pad);
        }

        /// <summary>A box's face (over its key's plate) and its plate, from its box.</summary>
        public static UiBox Face(UiBox box, InterfaceValues v) => new UiBox(box.X, box.Y, box.W, box.H - KeyPlateHeight(v) - 2f * Strip(v));

        public static UiBox Plate(UiBox box, InterfaceValues v)
        {
            float h = KeyPlateHeight(v);
            return new UiBox(box.X, box.Bottom - h, box.W, h);
        }

        /// <summary>A box grown by <paramref name="factor"/> about its middle (a lit recast box, work order 063 item 2b).</summary>
        public static UiBox Grown(UiBox b, float factor)
        {
            factor = Math.Max(0.1f, factor);
            float w = b.W * factor, h = b.H * factor;
            return new UiBox(b.CenterX - w * 0.5f, b.CenterY - h * 0.5f, w, h);
        }

        // ---------------- 3b. A skill off cooldown (work order 063, item 2) ----------------

        /// <summary>
        /// A skill came off its cooldown this frame and its box shows it: it waited before and does not now, the same skill
        /// is on the key, and nothing quiets it (a pause, a choice open, a run starting or a floor changing, a frame the
        /// box was not worked out in).
        /// </summary>
        public static bool CameReady(bool waitedBefore, bool waitsNow, bool sameSkill, bool quiet) => waitedBefore && !waitsNow && sameSkill && !quiet;

        /// <summary>The ready flash's strength, 1 at once to 0 after <see cref="InterfaceValues.ReadyFlashSeconds"/> of real time; 0 off.</summary>
        public static float ReadyFlashAlpha(float since, InterfaceValues v)
        {
            if (!v.ReadyFlash || since < 0f || v.ReadyFlashSeconds <= 0f || since >= v.ReadyFlashSeconds) return 0f;
            return 1f - since / v.ReadyFlashSeconds;
        }

        /// <summary>
        /// The kick: how far along the strip's slant the box stands, in layout pixels: up to <see cref="InterfaceValues.ReadyKick"/>
        /// (grown with the strip) in the first <see cref="InterfaceValues.ReadyKickRise"/> of the flash, then settling back, eased.
        /// </summary>
        public static float ReadyKickNow(float since, InterfaceValues v)
        {
            if (ReadyFlashAlpha(since, v) <= 0f) return 0f;
            float p = since / v.ReadyFlashSeconds, rise = Math.Min(0.9f, Math.Max(0.01f, v.ReadyKickRise));
            float most = v.ReadyKick * Strip(v);
            if (p < rise) return most * p / rise;
            float back = 1f - (p - rise) / (1f - rise);
            return most * back * back;
        }

        // ---------------- 3c. The recast window on its box (work order 063, item 2b) ----------------

        /// <summary>
        /// The open recast window as the box shows it, as shares of the window: the "now" mark (seconds since it opened over
        /// its length) and the clean part's start and end. False when no window is open (before it opens, after it ran out).
        /// </summary>
        public static bool RecastShares(float window, float sinceOpened, float cleanFrom, float cleanTo, out float now, out float bandFrom, out float bandTo)
        {
            now = bandFrom = bandTo = 0f;
            if (window <= 0f || sinceOpened < 0f || sinceOpened > window) return false;
            now = Clamp01(sinceOpened / window);
            if (cleanTo > cleanFrom)
            {
                bandFrom = Clamp01(cleanFrom / window);
                bandTo = Clamp01(cleanTo / window);
            }
            return true;
        }

        /// <summary>The window's bar across a lit box's face: the lower part of the face, inset from its sides.</summary>
        public static UiBox RecastBar(UiBox face, InterfaceValues v)
        {
            float s = Strip(v), h = v.RecastBarHeight * s, inset = 4f * s;
            return new UiBox(face.X + inset, face.Bottom - h - 3f * s, Math.Max(1f, face.W - 2f * inset), h);
        }

        /// <summary>The clean part's band on that bar.</summary>
        public static UiBox RecastBand(UiBox bar, float bandFrom, float bandTo) =>
            new UiBox(bar.X + bar.W * Clamp01(bandFrom), bar.Y, bar.W * Math.Max(0f, Clamp01(bandTo) - Clamp01(bandFrom)), bar.H);

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

        /// <summary>
        /// A chest card's tier (work order 050, drawn in the skin by work order 045): its numeral in the tier's colour, white
        /// common, teal rare, gold epic, and nothing else. A card with no tier has a white numeral.
        /// </summary>
        public static UiInk TierInk(bool tiered, UpgradeTier tier) =>
            !tiered ? UiInk.White : tier == UpgradeTier.Epic ? UiInk.Gold : tier == UpgradeTier.Rare ? UiInk.Teal : UiInk.White;

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
