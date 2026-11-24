using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The screen between runs with work order 110's nine parts (Kristian, 2026-11-24, yes to the sprint plan's row: "Tre valg
    /// per plass, hver med en buff og en kostnad"; the switch is <see cref="MetaValues.MoreParts"/>), as part of
    /// <see cref="RunHud"/>. The store shows the nine as it showed the four, in the same cards and rows; only where nine would
    /// not fit does this change the layout:
    /// - the concept's look: the weapon's cards stand in <see cref="MorePartsColumns"/> columns once there are more than
    ///   <see cref="OneColumnParts"/> (one column of nine cards is twice the view's height, and the screen's fit would draw
    ///   everything about two thirds the size); each slot's head over its first card, each slot from a new row. Four parts
    ///   keep 060's one column exactly.
    /// - this morning's look: where the weapon's panel would run past the view's foot, its parts' rows take the largest smaller
    ///   type that fits (down to <see cref="FlatSmallPartsFont"/>); four parts keep 060's rows exactly.
    /// Nobody has seen either: these are numbers measured in tests.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>A slot's head over its first card, in the 1080 layout (work order 078's 30).</summary>
        const float SlotHeadHeight = 30f;
        /// <summary>At most this many parts stand in the weapon's one column (work order 060's four); more go in <see cref="MorePartsColumns"/>.</summary>
        const int OneColumnParts = 4;
        /// <summary>The weapon's columns of cards with more parts than <see cref="OneColumnParts"/> (work order 110's first guess: two keeps the screen at about 0.82 of its size at 16:9, three at about 0.71).</summary>
        const int MorePartsColumns = 2;
        /// <summary>
        /// This morning's look: the least type of the parts' rows when 060's 13 would run the weapon's panel past the view's foot;
        /// the largest from 12 down to this that fits is taken (nine parts at 16:9: about 11, the producer's arithmetic).
        /// </summary>
        const int FlatSmallPartsFont = 10;
        /// <summary>060's type of a part's row.</summary>
        const int FlatPartsFont = 13;

        // The smaller types' styles, made once each, by size (index: FlatPartsFont - size - 1).
        readonly GUIStyle[] _pieceSmall = new GUIStyle[FlatPartsFont - FlatSmallPartsFont];
        readonly GUIStyle[] _pieceSmallOn = new GUIStyle[FlatPartsFont - FlatSmallPartsFont];

        /// <summary>True when this morning's look drew the weapon's parts in a smaller type (work order 110). For tests.</summary>
        public bool FlatPartsSmall { get; private set; }
        /// <summary>The type this morning's look drew the weapon's parts in, last time it drew them. For tests and the log.</summary>
        public int FlatPartsFontSize { get; private set; } = FlatPartsFont;

        /// <summary>
        /// This morning's look: the weapon's panel's height and its parts' rows' styles, 060's where its panel fits the view
        /// (between the tops <see cref="BetweenRunsLayout.LeastTop"/> keeps), else the largest smaller type that fits, at
        /// least <see cref="FlatSmallPartsFont"/>.
        /// </summary>
        float FlatWeaponFit(float width, float rowH, float gap, float headH, float keysH, out GUIStyle style, out GUIStyle on)
        {
            style = _pieceButton;
            on = _pieceOn;
            float room = SkillBar.LayoutHeight - 2f * BetweenRunsLayout.LeastTop;
            float h = FlatWeaponHeight(width, rowH, gap, headH, keysH, style);
            int size = FlatPartsFont;
            while (h > room && size > FlatSmallPartsFont)
            {
                size--;
                int k = FlatPartsFont - size - 1;
                if (_pieceSmall[k] == null)
                {
                    _pieceSmall[k] = new GUIStyle(_pieceButton) { fontSize = size };
                    _pieceSmallOn[k] = new GUIStyle(_pieceOn) { fontSize = size };
                }
                style = _pieceSmall[k];
                on = _pieceSmallOn[k];
                h = FlatWeaponHeight(width, rowH, gap, headH, keysH, style);
            }
            FlatPartsSmall = size != FlatPartsFont;
            FlatPartsFontSize = size;
            return h;
        }

        /// <summary>The weapon's columns of cards in the concept's look.</summary>
        int WeaponColumns => _weaponEnd - _rackCards > OneColumnParts ? MorePartsColumns : 1;

        /// <summary>The weapon's panel's width: its columns of cards and its padding (one column: 078's).</summary>
        float WeaponPanelWidth(MenuValues m) => WeaponColumns * m.CardWidth + (WeaponColumns - 1) * m.CardGap + 2f * m.PanelPad;

        /// <summary>What the weapon's panel takes across, with the gap before it (one column: <see cref="SideColumn"/>).</summary>
        float WeaponColumn(MenuValues m) => m.ColumnGap + WeaponPanelWidth(m);

        /// <summary>The rows the weapon's cards take: each slot from a new row, <see cref="WeaponColumns"/> a row.</summary>
        int WeaponCardRows()
        {
            int rows = 0, col = 0, cols = WeaponColumns;
            for (int i = _rackCards; i < _weaponEnd; i++)
            {
                if (col == 0 || (i < _cardSlotHead.Count && _cardSlotHead[i] >= 0) || col >= cols)
                {
                    rows++;
                    col = 0;
                }
                col++;
            }
            return rows;
        }

        /// <summary>
        /// The weapon's card <paramref name="index"/> in the 1080 layout before the fit, its panel's first card at
        /// (<paramref name="x"/>, <paramref name="y"/>): a slot's head takes <see cref="SlotHeadHeight"/> over its first card,
        /// which starts a new row; one column, exactly 078's places.
        /// </summary>
        Rect WeaponCardRect(int index, float x, float y, MenuValues m)
        {
            int col = 0, cols = WeaponColumns;
            for (int i = _rackCards; i <= index; i++)
            {
                bool head = i < _cardSlotHead.Count && _cardSlotHead[i] >= 0;
                if (head || col >= cols)
                {
                    if (col > 0) y += _cardH + m.CardGap;
                    col = 0;
                }
                if (head) y += SlotHeadHeight;
                if (i == index) break;
                col++;
            }
            return new Rect(x + col * (m.CardWidth + m.CardGap), y, m.CardWidth, _cardH);
        }

        /// <summary>This morning's look: the weapon's panel's height with its parts' rows in <paramref name="style"/> (060's sum).</summary>
        float FlatWeaponHeight(float width, float rowH, float gap, float headH, float keysH, GUIStyle style)
        {
            float h = 72f + 22f + keysH;
            for (int i = 2; i < _weaponLines.Count; i++)
                h += (_weaponRows[i] == null ? headH : RowHeight(_weaponLines[i], width - 40f, rowH, style)) + gap;
            return h;
        }
    }
}
