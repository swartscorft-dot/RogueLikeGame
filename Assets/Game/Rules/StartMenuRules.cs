using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>The screens of the start menu (work order 114): none up, the main menu, START RUN's two steps, and the two tabs.</summary>
    public enum MenuScreen { None, Main, Character, Loadout, Upgrades, Stats }

    /// <summary>The main menu's lines, in the order shown (work order 114, his words: "start run ... En annen fane er upgrades. Stats og annet morro").</summary>
    public enum MainLine { StartRun, Upgrades, Stats, Quit, Settings } // Settings: work order 128 (shown between STATS and QUIT)

    /// <summary>
    /// The start menu's numbers and words (work order 114, 2026-11-24). Kristian: "Hvi kan godt også lage en start og main menu i
    /// den ui stilen vi har valgt. Ser for meg at man har en start run. Så kommer man til charachter selection -&gt; run load out. En
    /// annen fane er upgrades. Stats og annet morro. Siden vi ikke har andre karakterer, ser jeg for meg at det bare er
    /// spørsmålstegn for nå. Karakter seleksjonen skal være litt som smash bros, et lite panel neders, med et fancy bilde av
    /// karakteren i veldig høy res." Sizes are in the interface's 1080-high layout (<see cref="InterfaceValues.LayoutHeight"/>);
    /// first guesses, laid out from the two mock-ups of 2026-11-24 and held to decision 0011 where they differ.
    /// </summary>
    [Serializable]
    public sealed class StartMenuValues
    {
        // ---------------- The picture ----------------

        /// <summary>
        /// Her picture, loaded by name from Resources (the producer's start; no picture is in the project until he has chosen
        /// one of the proposals). Missing, a flat backdrop in the interface's style stands in.
        /// </summary>
        public string PictureName = "UI/Menu/Character_Gunslinger";
        /// <summary>
        /// The named anchor the picture is cropped about when the screen's shape differs from the picture's (it covers the
        /// screen, never stretched): the share of its width and of its height (from the top) kept in the middle of what shows.
        /// Her figure stands right of the middle in the picture he chose (proposal A redrawn, 2026-11-24), her face in the upper third.
        /// </summary>
        public float PictureAnchorX = 0.72f;
        public float PictureAnchorY = 0.3f;
        /// <summary>Her slot's portrait: a crop of the same picture about her face (shares of its width and height, from the top), this share of its height tall; her face in proposal A2 is at about (0.75, 0.2).</summary>
        public float PortraitX = 0.74f;
        public float PortraitY = 0.2f;
        public float PortraitShare = 0.34f;
        /// <summary>A dark veil over the picture under the loadout, the upgrades and the stats (their panels read on it); the main menu and the character's screen show it bare.</summary>
        public float PanelVeil = 0.78f;
        /// <summary>The dark gradient from the left edge over the picture on the main menu and the character's screen, so the words stand on calm dark (the mock-up's "left third calm and dark").</summary>
        public float LeftShade = 0.82f;
        public float LeftShadeWidth = 900f;

        // ---------------- Every screen ----------------

        /// <summary>The menu's draw order: over the HUD's screens (<see cref="InterfaceDepth.Screens"/>), drawn after them.</summary>
        public int Depth = InterfaceDepth.Screens - 1;
        /// <summary>The tabs at the top left of START RUN's steps and the two tabs (the character mock-up's): the first's place, a tab's size, the gap, the words.</summary>
        public float TabLeft = 48f;
        public float TabTop = 34f;
        public float TabWidth = 210f;
        public float TabHeight = 44f;
        public float TabGap = 16f;
        public int TabTextSize = 25;
        /// <summary>His Echoes, small, on every screen: a slanted plate at the top right, its size and its number's size.</summary>
        public float EchoesWidth = 236f;
        public float EchoesHeight = 52f;
        public float EchoesMargin = 44f;
        public int EchoesTextSize = 30;
        public int EchoesLabelSize = 16;
        /// <summary>The line of keys at the foot of each screen: its distance from the foot and its height.</summary>
        public float HintBottom = 34f;
        public float HintHeight = 32f;
        /// <summary>A short line about the last thing refused (a dark slot, an owned card on UPGRADES), at the foot over the keys.</summary>
        public int NoteSize = 21;

        // ---------------- The main menu ----------------

        /// <summary>The main menu's lines at the left: the first one's place, a line's width, height and gap, the words (heavy, large: the loudest thing after her).</summary>
        public float MainLeft = 120f;
        public float MainTop = 360f;
        public float MainWidth = 540f;
        public float MainRowHeight = 86f;
        public float MainRowGap = 16f;
        public int MainTextSize = 64;
        /// <summary>The thin slanted stroke under each line, and how far the chosen line stands out to the right.</summary>
        public float MainStroke = 4f;
        public float MainShift = 18f;

        // ---------------- START RUN, step one: the character ----------------

        /// <summary>Her name plate at the left: its place and size (it runs off the left edge, as the mock-up's), her name's size, the line under it, the lines about her.</summary>
        public float PlateLeft = -60f;
        public float PlateTop = 250f;
        public float PlateWidth = 820f;
        public float PlateHeight = 168f;
        public int NameSize = 132;
        public int SubSize = 30;
        public int LineSize = 25;
        public float LinesWidth = 640f;
        public float LineHeight = 38f;
        /// <summary>The small low panel at the foot: a row of slots, hers first and the rest dark with a question mark (his words).</summary>
        public int Slots = 6;
        public float SlotSize = 108f;
        public float SlotGap = 16f;
        public float SlotsBottom = 34f;
        public float SlotsPad = 14f;
        public int QuestionSize = 66;
        /// <summary>BACK at the foot's left and the step on (to the loadout) at its right: their size and words.</summary>
        public float FootButtonWidth = 230f;
        public float FootButtonHeight = 58f;
        public float NextWidth = 360f;
        public int FootTextSize = 28;

        // ---------------- START RUN, step two, and the two tabs ----------------

        /// <summary>The heading plate's top on the loadout, the upgrades and the stats (under the tabs; its size the pause menu's, <see cref="MenuValues.HeadingHeight"/>), and where the panels start under it.</summary>
        public float HeadTop = 104f;
        public float PanelsTop = 236f;
        /// <summary>The gap between panels side by side, and the most rows of cards a panel holds before it takes another column (up to <see cref="MostColumns"/>).</summary>
        public float PanelGap = 54f;
        public int MostRows = 4;
        public int MostColumns = 3;
        /// <summary>A part's slot's heading over its first card in the loadout's and the upgrades' parts panel.</summary>
        public float SlotHead = 30f;
        /// <summary>START at the loadout's foot right: its size.</summary>
        public float StartWidth = 380f;
        public float StartHeight = 64f;
        /// <summary>The quiet locked line, the budget's and a panel's words.</summary>
        public int LockedSize = 22;
        public float LockedWidth = 1100f;

        // ---------------- The stats ----------------

        /// <summary>The two panels of the stats: her sheet at a run's start, and his records; their widths.</summary>
        public float SheetWidth = 760f;
        public float RecordsWidth = 620f;
        public float RecordLine = 40f;
        public int RecordNameSize = 22;
        public int RecordValueSize = 28;

        // ---------------- Her words ----------------

        /// <summary>
        /// Her name plate: the documents give her no name. What they call her: "First class: shooter with skill shots"
        /// (docs/vision/first-class.md); the glossary names none. It said SHOOTER until 2026-11-25, when Kristian, asked
        /// whether to name her now, ticked "Bytt SHOOTER til Gunslinger": what her picture and its file already call her
        /// (Character_Gunslinger), in the menu's capitals, until he gives her a name of her own (work order 120).
        /// </summary>
        public string Name = "GUNSLINGER";
        public string Sub = "WITH SKILL SHOTS  ·  FIRST CLASS";
        /// <summary>Three short lines about her, from first-class.md's table (the basic attack, the dash, the loadout).</summary>
        public string[] Lines =
        {
            "Ranged: one shot a click, aimed at the cursor.",
            "One dash on a long cooldown, a moment untouchable.",
            "Four skills at a run's start, four more chosen on the way.",
        };
    }

    /// <summary>
    /// How the start menu goes from screen to screen (work order 114), with no engine: the main menu's lines, what each leads
    /// to, Escape's step back, the arrows' step round a list, and which slot of the character's row may be chosen.
    /// </summary>
    public static class StartMenuFlow
    {
        /// <summary>Her slot: the first. The rest are characters that do not exist (his words: "bare spørsmålstegn for nå").</summary>
        public const int HerSlot = 0;

        /// <summary>The main menu's lines: START RUN, UPGRADES, STATS, and QUIT only where it is offered (a built player).</summary>
        public static void MainLines(bool quit, List<MainLine> into)
        {
            into.Clear();
            into.Add(MainLine.StartRun);
            into.Add(MainLine.Upgrades);
            into.Add(MainLine.Stats);
            if (quit) into.Add(MainLine.Quit);
        }

        /// <summary>
        /// The main menu's lines with work order 128's SETTINGS between STATS and QUIT when <paramref name="settings"/> (the
        /// settings screen's switch, <see cref="SettingsScreenValues.On"/>); without it, as before.
        /// </summary>
        public static void MainLines(bool quit, bool settings, List<MainLine> into)
        {
            MainLines(false, into);
            if (settings) into.Add(MainLine.Settings);
            if (quit) into.Add(MainLine.Quit);
        }

        /// <summary>The screen a main menu line leads to; QUIT leads to none (the game quits).</summary>
        public static MenuScreen Pick(MainLine line) =>
            line == MainLine.StartRun ? MenuScreen.Character : line == MainLine.Upgrades ? MenuScreen.Upgrades : line == MainLine.Stats ? MenuScreen.Stats : MenuScreen.None;

        /// <summary>Escape's step back: the loadout to the character, the character and the tabs to the main menu, the main menu to none (the practice room).</summary>
        public static MenuScreen Back(MenuScreen s) =>
            s == MenuScreen.Loadout ? MenuScreen.Character
            : s == MenuScreen.Character || s == MenuScreen.Upgrades || s == MenuScreen.Stats ? MenuScreen.Main
            : MenuScreen.None;

        /// <summary>The tab a screen sits under: START RUN's two steps under the first.</summary>
        public static int TabOf(MenuScreen s) => s == MenuScreen.Upgrades ? 1 : s == MenuScreen.Stats ? 2 : 0;

        /// <summary>The screen a tab opens: START RUN's first step, UPGRADES, STATS.</summary>
        public static MenuScreen TabScreen(int tab) => tab == 1 ? MenuScreen.Upgrades : tab == 2 ? MenuScreen.Stats : MenuScreen.Character;

        /// <summary>The between-runs screen is wanted under these (its panels are reused there): the loadout and the upgrades.</summary>
        public static bool UsesBetweenRuns(MenuScreen s) => s == MenuScreen.Loadout || s == MenuScreen.Upgrades;

        /// <summary>One step round a list of <paramref name="count"/>: past either end to the other.</summary>
        public static int Step(int at, int delta, int count)
        {
            if (count <= 0) return 0;
            int n = ((at + delta) % count + count) % count;
            return n;
        }

        /// <summary>May the slot be chosen: hers only; a dark slot refuses.</summary>
        public static bool MayChoose(int slot) => slot == HerSlot;
    }

    /// <summary>A rectangle of a texture in its own coordinates, 0 to 1, from its bottom left (as the engine's texture coordinates are).</summary>
    public struct UvRect
    {
        public float X, Y, W, H;

        public UvRect(float x, float y, float w, float h)
        {
            X = x;
            Y = y;
            W = w;
            H = h;
        }
    }

    /// <summary>
    /// A picture drawn to cover a view without stretching (work order 114): the part of it that shows, cropped about a named
    /// anchor (shares of its width and of its height from the top); never past its edges.
    /// </summary>
    public static class CoverCrop
    {
        /// <summary>The part of a <paramref name="pictureW"/> by <paramref name="pictureH"/> picture that covers a view of this size.</summary>
        public static UvRect Of(float pictureW, float pictureH, float viewW, float viewH, float anchorX, float anchorY)
        {
            if (pictureW <= 0f || pictureH <= 0f || viewW <= 0f || viewH <= 0f) return new UvRect(0f, 0f, 1f, 1f);
            float picture = pictureW / pictureH, view = viewW / viewH;
            float w = 1f, h = 1f;
            if (picture > view) w = view / picture; // wider than the view: its sides are cut
            else h = picture / view;                // taller: its top and foot are cut
            return Around(anchorX, anchorY, w, h);
        }

        /// <summary>
        /// A crop of a picture of this shape, <paramref name="heightShare"/> of its height tall and of the shape of a slot
        /// <paramref name="slotW"/> by <paramref name="slotH"/>, about a point (shares, from the top): her slot's portrait.
        /// </summary>
        public static UvRect Portrait(float pictureW, float pictureH, float slotW, float slotH, float x, float y, float heightShare)
        {
            if (pictureW <= 0f || pictureH <= 0f || slotW <= 0f || slotH <= 0f) return new UvRect(0f, 0f, 1f, 1f);
            float h = Math.Max(0.01f, Math.Min(1f, heightShare));
            float w = Math.Min(1f, h * pictureH * (slotW / slotH) / pictureW);
            return Around(x, y, w, h);
        }

        static UvRect Around(float anchorX, float anchorY, float w, float h)
        {
            float left = Clamp(anchorX - w * 0.5f, 0f, 1f - w);
            float top = Clamp(anchorY - h * 0.5f, 0f, 1f - h);
            return new UvRect(left, 1f - top - h, w, h);
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>
    /// The panels of the loadout and the upgrades laid out side by side (work order 114): each panel's cards in as many columns
    /// as keep it to <see cref="StartMenuValues.MostRows"/> rows (up to <see cref="StartMenuValues.MostColumns"/>), its slot
    /// headings where a part's slot changes, the panels in a row from the left; the whole row's width and height for the fit.
    /// </summary>
    public static class MenuPanels
    {
        /// <summary>The columns a panel of <paramref name="cards"/> cards takes.</summary>
        public static int Columns(int cards, StartMenuValues s)
        {
            if (cards <= 0) return 1;
            int most = Math.Max(1, s.MostColumns), rows = Math.Max(1, s.MostRows);
            return Math.Min(most, (cards + rows - 1) / rows);
        }

        /// <summary>A panel's inner height for its cards in this many columns: rows of cards, and the slot headings (one column only).</summary>
        public static float InnerHeight(int cards, int columns, int heads, float cardH, float cardGap, StartMenuValues s)
        {
            if (cards <= 0) return 0f;
            int rows = (cards + columns - 1) / Math.Max(1, columns);
            return rows * (cardH + cardGap) - cardGap + (columns == 1 ? heads * s.SlotHead : 0f);
        }
    }
}
