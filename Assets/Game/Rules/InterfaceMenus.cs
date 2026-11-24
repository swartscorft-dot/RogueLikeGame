using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The menus in the concept's style (work order 078, 2026-11-22: asked what next, Kristian ticked "Menyene i
    /// konseptstilen": the pause menu, the run's end and the screen between runs in the language of the fight's interface of
    /// work order 068). There is no concept picture of a menu: the fight concept's language (ui_b_kinetic_anime.png) carried
    /// over: thick black outlines with a white or teal stroke outside, slanted plates, compact dark panels over a veil instead
    /// of a black slab, red, white and teal on black, halftone dots toward a shape's end, heavy white italic headings and
    /// numerals, red double slashes, white key caps with black letters. Layout pixels of a view 1080 high, as
    /// <see cref="InterfaceValues"/>; first guesses set by looking at pictures of the game (captures\078), tuned by him.
    /// <see cref="Concept"/> off gives this morning's menus (work order 068's) exactly.
    /// </summary>
    [Serializable]
    public sealed class MenuValues
    {
        /// <summary>The menus in the concept's style; off: work order 068's panels, as this morning.</summary>
        public bool Concept = true;
        /// <summary>
        /// The veil over the game under a menu: the game shows through (his "not a black slab"). Round 1's 0.55 left the fight's
        /// plate and strip reading as clearly as the menu: 0.64.
        /// </summary>
        public float Veil = 0.64f;
        /// <summary>The least room round a menu at the view's edges; a menu larger than the view is drawn smaller to fit.</summary>
        public float Margin = 28f;

        // ---------------- The pause menu ----------------

        /// <summary>
        /// The heading plate's top, and its width and height (a red plate as her health's, leaning as hard). The top under the
        /// fight's plate at the top left (round 1's 58 laid "PAUSED" over its number).
        /// </summary>
        public float Top = 150f;
        public float HeadingWidth = 430f;
        public float HeadingHeight = 92f;
        /// <summary>"PAUSED" in the heavy italic, white.</summary>
        public int HeadingSize = 80;
        /// <summary>The heading plate's lean (the top plate's <see cref="InterfaceValues.PlateSlant"/>) and the share of it, from its right end, the halftone dots fade across.</summary>
        public float HeadingSlant = 0.6f;
        public float HeadingDots = 0.45f;
        /// <summary>The gap under the heading before the rows.</summary>
        public float HeadingGap = 26f;
        /// <summary>The rows' column: its width, a row's height, the gap between rows, and the gap before the settings.</summary>
        public float RowsWidth = 600f;
        public float RowHeight = 44f;
        public float RowGap = 8f;
        public float SectionGap = 16f;
        /// <summary>A row's words (the lighter italic) and a setting's value (the heavy italic).</summary>
        public int RowTextSize = 26;
        public int RowValueSize = 27;
        /// <summary>The selected row stands out this far to the right (white, a teal stroke, the red slashes).</summary>
        public float SelectedShift = 16f;
        /// <summary>The line of keys under the rows: its height, and its key caps' height.</summary>
        public float HintHeight = 30f;
        /// <summary>The gap between the rows' column and the panels beside it, and the panels' width.</summary>
        public float ColumnGap = 84f; // round 1: at 60 the selected row's slashes touched the panels
        public float SideWidth = 680f;
        /// <summary>A panel's padding inside its outline, its lean (a tall panel leans little), and the gap between two panels.</summary>
        public float PanelPad = 18f;
        public float PanelSlant = 0.05f;
        public float SideGap = 22f;
        /// <summary>A panel's heading ("HER STATS", "KEYS") and its line under it.</summary>
        public int PanelHeadSize = 30;
        public int PanelSubSize = 17;
        public float PanelHeadHeight = 46f;
        /// <summary>Her stats: a line's height, the name (lighter), the value (heavy) and its change since the run's start (heavy, small).</summary>
        public float StatLine = 28f;
        public int StatNameSize = 20;
        public int StatValueSize = 22;
        public int StatChangeSize = 17;
        /// <summary>The keys: a line's height, a key cap's height and its letters, the words after it.</summary>
        public float KeyLine = 34f;
        public float KeyCapHeight = 27f;
        public int KeyCapSize = 18;
        public int KeyWordSize = 20;
        /// <summary>The gap between the two columns of a panel.</summary>
        public float ColumnInnerGap = 26f;

        // ---------------- The run's end ----------------

        /// <summary>The verdict's words: an arcade round's end, DEAD in red, CLEARED in teal.</summary>
        public string DeadWord = "DEAD";
        public string ClearedWord = "CLEARED";
        /// <summary>The band across the screen the verdict stands on: its top, height and lean; the verdict's size.</summary>
        public float BandTop = 120f;
        public float BandHeight = 190f;
        public float BandSlant = 0.22f;
        public int VerdictSize = 170;
        /// <summary>The line under it ("Floor 3 of 5,  Room 5 of 9").</summary>
        public int WhereSize = 36;
        /// <summary>The blocks of numbers (time, kills, health left): width, height, gap; the label and the numeral.</summary>
        public float BlockWidth = 270f;
        public float BlockHeight = 96f;
        public float BlockGap = 30f;
        public int BlockLabelSize = 20;
        public int BlockNumberSize = 58;
        /// <summary>The run's picks as chips: the area's width, a chip's height, the gaps, the words.</summary>
        public float ChipsWidth = 1240f;
        public float ChipHeight = 38f;
        public float ChipGap = 12f;
        public float ChipPad = 16f;
        public int ChipTextSize = 21;
        /// <summary>The lines after the chips (what the trial earned, the weapon, feats, the run's code).</summary>
        public int EndLineSize = 20;
        public float EndLine = 26f;
        /// <summary>The answers under the rest: a button's width, height and gap, its words, and the gap over them (round 1's at the view's foot stood on the strip).</summary>
        public float ButtonWidth = 300f;
        public float ButtonHeight = 54f;
        public float ButtonGap = 26f;
        public int ButtonTextSize = 26;
        public float ButtonsGap = 44f;
        /// <summary>The band's width as a share of the view: its leaning ends show (round 1's ran off both sides and read flat).</summary>
        public float BandShare = 0.84f;

        // ---------------- Between runs ----------------

        /// <summary>The screen between runs' top: its heading in the middle across, clear of the fight's plate at the top left.</summary>
        public float BetweenTop = 58f;
        /// <summary>A part's or a piece's card: its width and least height, the gap, the name plate's words and the text.</summary>
        public float CardWidth = 330f;
        public float CardHeight = 112f;
        public float CardGap = 18f;
        public int CardNameSize = 24;
        public int CardTextSize = 17;

        /// <summary>Her sheet on Tab (work order 095): its own numbers; the rest it shares with the pause menu above.</summary>
        public SheetValues Sheet = new SheetValues();

        /// <summary>
        /// Work order 114's switch, the start and main menu (Kristian 2026-11-24: "Hvi kan godt også lage en start og main menu i den
        /// ui stilen vi har valgt"): on, the menu comes up when the game starts and after a run's end screen. Off, nothing of it is
        /// installed and the game starts in the practice room as before.
        /// </summary>
        public bool StartMenu = true;
        /// <summary>Work order 114: in the Editor, Play goes straight into a run (no menu at the start) when he wants to iterate. Off by default; read when Play starts.</summary>
        public bool SkipInEditor = false;
        /// <summary>Work order 114: the start menu's numbers and words.</summary>
        public StartMenuValues Start = new StartMenuValues();
    }

    /// <summary>A key of the pause menu's list as a key cap and the words after it (work order 078): "Space", "Dash  (toward the cursor)".</summary>
    public struct MenuKey
    {
        public string Key, Words;
    }

    /// <summary>A stat line of the pause menu read apart (work order 078): its name, the value now, the value at the run's start, and whether and by how much it changed.</summary>
    public struct MenuStat
    {
        public string Name, Value, Start, Change;
        public bool Changed;
    }

    /// <summary>
    /// The pause menu's and the end screen's words read apart for the concept's drawing (work order 078): what the lines SAY
    /// is theirs as before (<c>RunHud.MenuText</c>, <c>HerStats.Lines</c>); this only splits them into the parts drawn apart.
    /// </summary>
    public static class MenuWords
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>A setting's line, "Camera kick:  on", as its name and its value ("Camera kick", "on"); a line without ":  " is all name.</summary>
        public static void Setting(string line, out string name, out string value)
        {
            line = line ?? "";
            int at = line.IndexOf(":  ", StringComparison.Ordinal);
            if (at < 0)
            {
                name = line.Trim();
                value = "";
                return;
            }
            name = line.Substring(0, at).Trim();
            value = line.Substring(at + 3).Trim();
        }

        /// <summary>An action's line, "Resume   (Esc)", as its words and its key ("Resume", "Esc"); a line without a key in brackets at its end has none.</summary>
        public static void Action(string line, out string words, out string key)
        {
            line = (line ?? "").Trim();
            int open = line.LastIndexOf(" (", StringComparison.Ordinal);
            if (open > 0 && line.EndsWith(")", StringComparison.Ordinal))
            {
                words = line.Substring(0, open).Trim();
                key = line.Substring(open + 2, line.Length - open - 3).Trim();
                return;
            }
            words = line;
            key = "";
        }

        /// <summary>A button's words, "F6  same run", as its key and words ("F6", "same run"); "Same run" has no key.</summary>
        public static void Button(string line, out string key, out string words)
        {
            line = (line ?? "").Trim();
            int gap = line.IndexOf("  ", StringComparison.Ordinal);
            if (gap > 0)
            {
                key = line.Substring(0, gap).Trim();
                words = line.Substring(gap).Trim();
                return;
            }
            key = "";
            words = line;
        }

        /// <summary>A setting's value that says off ("off").</summary>
        public static bool IsOff(string value) => string.Equals(value, "off", StringComparison.Ordinal);

        /// <summary>A setting's value that says on ("on").</summary>
        public static bool IsOn(string value) => string.Equals(value, "on", StringComparison.Ordinal);

        /// <summary>
        /// A line of her stats, "Attack speed  4.2 shots/s  (3.8)": its name (before the first double space), the value now,
        /// the value at the run's start (in the brackets at its end), changed when the first word of the two differ, and the
        /// change as a signed number with the value's unit where both are numbers ("+0.4", "+3 %", "+1 m"), else empty. A line
        /// with no brackets ("Loadout  Pistols: stock") has no start and is not changed.
        /// </summary>
        public static MenuStat Stat(string line)
        {
            line = line ?? "";
            var s = new MenuStat { Name = line.Trim(), Value = "", Start = "", Change = "" };
            int gap = line.IndexOf("  ", StringComparison.Ordinal);
            if (gap < 0) return s;
            s.Name = line.Substring(0, gap).Trim();
            string rest = line.Substring(gap).Trim();
            int open = rest.LastIndexOf("  (", StringComparison.Ordinal);
            if (open < 0 || !rest.EndsWith(")", StringComparison.Ordinal))
            {
                s.Value = rest;
                return s;
            }
            s.Value = rest.Substring(0, open).Trim();
            s.Start = rest.Substring(open + 3, rest.Length - open - 4).Trim();
            string now = FirstWord(s.Value), then = FirstWord(s.Start);
            s.Changed = !string.Equals(now, then, StringComparison.Ordinal);
            if (s.Changed && Number(now, out double a, out int da) && Number(then, out double b, out int db))
            {
                int decimals = Math.Max(da, db);
                double d = Math.Round(a - b, decimals);
                string unit = Unit(s.Value);
                s.Change = (d >= 0 ? "+" : "-") + Math.Abs(d).ToString(decimals > 0 ? "0." + new string('0', decimals) : "0", s_Inv) + (unit.Length > 0 ? " " + unit : "");
            }
            return s;
        }

        static string FirstWord(string text)
        {
            text = (text ?? "").Trim();
            int space = text.IndexOf(' ');
            return space < 0 ? text : text.Substring(0, space);
        }

        /// <summary>The unit after a value's number, where it is one the stats use ("%", "m"); else empty.</summary>
        static string Unit(string value)
        {
            string[] words = (value ?? "").Trim().Split(' ');
            return words.Length > 1 && (words[1] == "%" || words[1] == "m") ? words[1] : "";
        }

        /// <summary>A word read as a number ("+5", "3.8", "-10%"), with its decimals; false for words that are not.</summary>
        static bool Number(string word, out double value, out int decimals)
        {
            decimals = 0;
            string w = (word ?? "").Trim().TrimEnd('%');
            if (w.StartsWith("+", StringComparison.Ordinal)) w = w.Substring(1);
            int dot = w.IndexOf('.');
            if (dot >= 0) decimals = w.Length - dot - 1;
            return double.TryParse(w, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, s_Inv, out value);
        }
    }

    /// <summary>Where the pause menu's parts stand (work order 078), in layout pixels of a view 1080 high, before it is drawn smaller to fit (<see cref="MenuLayout.Fit"/>).</summary>
    public struct PauseLayout
    {
        /// <summary>The heading plate, the rows' column, the line of keys under it, and the panels beside it (her stats over the keys).</summary>
        public UiBox Heading, Rows, Hint, Side;
        /// <summary>The whole menu's width and height, from the view's top (its top margin in).</summary>
        public float Width, Height;
    }

    /// <summary>The menus' geometry (work order 078): plain arithmetic, so it is tested without the engine.</summary>
    public static class MenuLayout
    {
        /// <summary>
        /// The pause menu in a view <paramref name="viewWidth"/> wide: the heading at the top of the rows' column, the rows
        /// under it (the settings after a gap from <paramref name="firstSetting"/> on), the keys' line under the rows, and the
        /// panels beside them, <paramref name="sideHeight"/> tall; the whole centred across.
        /// </summary>
        public static PauseLayout Pause(float viewWidth, int rows, int firstSetting, float sideHeight, MenuValues m)
        {
            var l = new PauseLayout();
            float side = sideHeight > 0f ? m.ColumnGap + m.SideWidth : 0f;
            l.Width = m.RowsWidth + side;
            float x = (viewWidth - l.Width) * 0.5f;
            l.Heading = new UiBox(x + m.HeadingHeight * m.HeadingSlant * 0.5f, m.Top, m.HeadingWidth, m.HeadingHeight);
            float top = m.Top + m.HeadingHeight + m.HeadingGap;
            float rowsHeight = RowsHeight(rows, firstSetting, m);
            l.Rows = new UiBox(x, top, m.RowsWidth, rowsHeight);
            l.Hint = new UiBox(x, top + rowsHeight + 18f, m.RowsWidth, m.HintHeight);
            l.Side = new UiBox(x + m.RowsWidth + m.ColumnGap, top, sideHeight > 0f ? m.SideWidth : 0f, Math.Max(0f, sideHeight));
            l.Height = Math.Max(l.Hint.Bottom, l.Side.Bottom) + m.Margin;
            return l;
        }

        /// <summary>The rows' column's height: every row and the gaps between, and the section's gap before the settings.</summary>
        public static float RowsHeight(int rows, int firstSetting, MenuValues m)
        {
            if (rows <= 0) return 0f;
            return rows * m.RowHeight + (rows - 1) * m.RowGap + (firstSetting > 0 && firstSetting < rows ? m.SectionGap : 0f);
        }

        /// <summary>Row <paramref name="index"/> of the column at <paramref name="rows"/> (not yet moved out when selected).</summary>
        public static UiBox Row(UiBox rows, int index, int firstSetting, MenuValues m) =>
            new UiBox(rows.X, rows.Y + index * (m.RowHeight + m.RowGap) + (firstSetting > 0 && index >= firstSetting ? m.SectionGap : 0f), rows.W, m.RowHeight);

        /// <summary>
        /// The factor a menu <paramref name="width"/> by <paramref name="height"/> (its top margin in) is drawn at to fit a view
        /// of this size with <see cref="MenuValues.Margin"/> at its sides: 1 when it fits, never more.
        /// </summary>
        public static float Fit(float width, float height, float viewWidth, float viewHeight, MenuValues m)
        {
            if (width <= 0f || height <= 0f) return 1f;
            float across = (viewWidth - 2f * m.Margin) / width, down = viewHeight / height;
            return Math.Max(0.1f, Math.Min(1f, Math.Min(across, down)));
        }

        /// <summary>
        /// Two columns of entries, top to bottom then the second column, each entry that fits in a column (its width given in
        /// <paramref name="widths"/>) placed there and the ones that do not after them, each on a whole line. Fills, for each
        /// entry, its line and its column (0, 1, or -1 for a whole line); returns the lines used.
        /// </summary>
        public static int TwoColumns(IList<float> widths, float columnWidth, List<int> lineOf, List<int> columnOf)
        {
            lineOf.Clear();
            columnOf.Clear();
            int fitting = 0;
            for (int i = 0; i < widths.Count; i++)
                if (widths[i] <= columnWidth) fitting++;
            int lines = (fitting + 1) / 2, k = 0, whole = 0;
            for (int i = 0; i < widths.Count; i++)
            {
                if (widths[i] <= columnWidth)
                {
                    lineOf.Add(k < lines ? k : k - lines);
                    columnOf.Add(k < lines ? 0 : 1);
                    k++;
                }
                else
                {
                    lineOf.Add(lines + whole);
                    columnOf.Add(-1);
                    whole++;
                }
            }
            return lines + whole;
        }

        /// <summary>
        /// Chips in rows no wider than <paramref name="width"/>, each row centred: their left edges and rows, from their
        /// <paramref name="widths"/> and the gap between. Returns the rows used.
        /// </summary>
        public static int Chips(IList<float> widths, float width, float gap, List<float> xOf, List<int> rowOf)
        {
            xOf.Clear();
            rowOf.Clear();
            int row = 0, first = 0;
            float used = 0f;
            for (int i = 0; i < widths.Count; i++)
            {
                float w = Math.Min(widths[i], width);
                if (i > first && used + gap + w > width)
                {
                    Centre(xOf, first, i, used, width);
                    row++;
                    first = i;
                    used = 0f;
                }
                xOf.Add(i == first ? 0f : used + gap);
                rowOf.Add(row);
                used = i == first ? w : used + gap + w;
            }
            if (widths.Count > first) Centre(xOf, first, widths.Count, used, width);
            return widths.Count == 0 ? 0 : row + 1;
        }

        static void Centre(List<float> xOf, int from, int to, float used, float width)
        {
            float shift = (width - used) * 0.5f;
            for (int i = from; i < to; i++) xOf[i] += shift;
        }
    }
}
