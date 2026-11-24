using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The ranks on the screen between runs (work order 099, Kristian 2026-11-24: "Rang som kjøpes mange ganger"), as part of
    /// <see cref="RunHud"/>. Nothing of it shows with the trial off (the screen never comes up), nor with an empty table
    /// (<see cref="MetaValues.Ranks"/>): the screen is then work order 029's and 060's as before.
    ///
    /// A panel of its own to the right of the rack's (and of the weapon's, with the attachments on), smaller than the rack's,
    /// in the screen's two looks: in the concept's (work order 078) a panel "RANKS" with a card per stat, as the weapon's
    /// parts are; in this morning's (029) a panel "Ranks (trial)" with a row per stat. Each says the stat, its rank out of its
    /// most, what the next rank gives and what she has now, its price, and whether she can pay it; at its last rank it says
    /// so and takes no press. The panel's heading says buying is one press and cannot be undone.
    ///
    /// Its keys are the screen's: the up and down arrows go on from the rack's last line (and the weapon's) into the ranks'
    /// rows and round to the start; Enter or a click buys the next rank (<see cref="GeneratedLevels.BuyRank"/>). Its lines are
    /// made with the screen's (<see cref="BetweenRunsLinesMade"/>), when it opens and when what they say changes, never in a
    /// frame that changes nothing.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The panel's heading and the line under it in this morning's look.</summary>
        public const string RanksTitle = "Ranks (trial)";
        public const string RanksSubFlat = "One press buys a rank; it cannot be undone.";
        /// <summary>The panel's heading and the words beside it in the concept's look.</summary>
        const string RanksHead = "RANKS", RanksSub = "one press, no undo";
        /// <summary>A rank's row in this morning's look: never lower than a rack row (029's 40), the gap between rows.</summary>
        const float RankRowH = 40f, RankGap = 4f;

        /// <summary>A line per stat of the ranks' table while the screen is up: "Health   rank 3 of 10   price 80 Echoes", then what the next rank gives. Else empty.</summary>
        public IReadOnlyList<string> BetweenRunsRankLines => _rankLines;

        readonly List<string> _rankLines = new List<string>();
        static readonly MetaRank[] s_NoRanks = new MetaRank[0];

        MetaRank[] RankTable => _levels != null && _levels.MetaValues.Ranks != null ? _levels.MetaValues.Ranks : s_NoRanks;
        int RankRows => RankTable.Length;

        /// <summary>The screen's line the first rank is on, for the arrow keys: after the rack's lines and the weapon's parts.</summary>
        public int RankRowFirst => BetweenRunsRows + (WeaponOn ? _weaponParts.Count : 0);

        /// <summary>
        /// Enter or a click on a rank's line (<paramref name="rank"/>: its place in the table): buys its next rank. False when
        /// the screen is not up, at its last rank (the line takes no press), or when nothing was bought.
        /// </summary>
        public bool PickRankLine(int rank)
        {
            Find();
            if (_levels == null || !_levels.BetweenRunsUp || rank < 0 || rank >= RankRows) return false;
            MetaRank r = RankTable[rank];
            if (r == null) return false;
            BetweenRunsSelected = RankRowFirst + rank;
            if (MetaRanks.AtMost(_levels.BetweenRunsState, r)) return false;
            return _levels.BuyRank(r.Id) == MetaRankBuy.Bought;
        }

        /// <summary>The ranks' lines, made with the screen's other lines (BuildBetweenRunsLines).</summary>
        void BuildRankLines()
        {
            _rankLines.Clear();
            MetaState s = _levels.BetweenRunsState;
            MetaValues v = _levels.MetaValues;
            foreach (MetaRank r in RankTable)
                _rankLines.Add(r == null ? "" : r.Name + "   " + MetaRanks.RankWords(s, r) + "   " + MetaRanks.StateWords(s, v, r) + "\n" + MetaRanks.NextWords(s, r));
        }

        /// <summary>The words under a rank's card in the concept's look: what the next rank gives, and whether she can pay for it.</summary>
        static string RankCardText(MetaState s, MetaValues v, MetaRank r)
        {
            if (MetaRanks.AtMost(s, r)) return MetaRanks.NextWords(s, r);
            int price = MetaRanks.NextPrice(s, v, r);
            return MetaRanks.NextWords(s, r) + "  " + (s.Currency >= price ? "You can buy it."
                                                         : (price - s.Currency).ToString(CultureInfo.InvariantCulture) + " more " + v.CurrencyName + " needed.");
        }

        // ---------------- This morning's look (029's panels) ----------------

        /// <summary>The panels as last drawn in this morning's look, in the HUD's layout pixels (720 high); for tests. Zero-sized when not drawn.</summary>
        public Rect BetweenFlatRack { get; private set; }
        public Rect BetweenFlatWeapon { get; private set; }
        public Rect BetweenFlatRanks { get; private set; }
        /// <summary>The ranks' panel's heading and line under it fit its width, as last drawn in this morning's look; for tests.</summary>
        public bool BetweenFlatRanksWordsFit { get; private set; }

        float RanksPanelHeight(float width)
        {
            float h = 50f + 22f;
            for (int i = 0; i < _rankLines.Count; i++) h += RowHeight(_rankLines[i], width - 40f, RankRowH) + RankGap;
            return h + 12f;
        }

        /// <summary>The ranks' panel in this morning's look at <paramref name="at"/>'s place and width, from <paramref name="top"/>.</summary>
        void DrawRanksFlat(LayoutRect at, float top, float height)
        {
            var r = new Rect(at.X, top, at.Width, height);
            BetweenFlatRanks = r;
            Panel(r, s_Accent, r.y + 46f);
            GUI.Label(new Rect(r.x, r.y + 8f, r.width, 36f), RanksTitle, _pauseTitle);
            var sub = new Rect(r.x + 20f, r.y + 50f, r.width - 40f, 18f);
            GUI.Label(sub, RanksSubFlat, _small);
            _measure.text = RanksTitle;
            bool fits = _pauseTitle.CalcSize(_measure).x <= r.width;
            _measure.text = RanksSubFlat;
            BetweenFlatRanksWordsFit = fits && _small.CalcSize(_measure).x <= sub.width;
            float y = r.y + 72f;
            MetaState s = _levels.BetweenRunsState;
            MetaRank[] table = RankTable;
            int first = RankRowFirst;
            for (int i = 0; i < _rankLines.Count && i < table.Length; i++)
            {
                float rh = RowHeight(_rankLines[i], r.width - 40f, RankRowH);
                var row = new Rect(r.x + 20f, y, r.width - 40f, rh);
                if (first + i == BetweenRunsSelected) Frame(new Rect(row.x - 3f, row.y - 2f, row.width + 6f, row.height + 4f), s_Accent);
                bool most = table[i] != null && MetaRanks.AtMost(s, table[i]);
                if (GUI.Button(row, _rankLines[i], most ? _pieceOn : _pieceButton) && !most) PickRankLine(i);
                y += rh + RankGap;
            }
        }
    }
}
