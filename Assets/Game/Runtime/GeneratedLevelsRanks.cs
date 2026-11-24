using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The ranks bought between runs (work order 099, Kristian 2026-11-24: "Rang som kjøpes mange ganger"), as part of
    /// <see cref="GeneratedLevels"/>: the screen's intent that buys one. The rules are <see cref="MetaRanks"/>'; the screen is
    /// drawn by the HUD (RunHudRanks.cs); a run started with the trial on gets them beside the rack's pieces
    /// (<see cref="MetaRanks.Apply"/> in the run's start, GeneratedLevelsBetweenRuns.cs). With the trial off none of it is
    /// reached: the screen never comes up, so nothing is bought, applied, shown or saved.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Buys the next rank of a stat on the screen between runs, if it may be bought: its price comes off the Echoes, the save
        /// is written, and the recorder notes it. It cannot be undone. At its last rank nothing happens (the line says so).
        /// </summary>
        public MetaRankBuy BuyRank(string id)
        {
            if (!BetweenRunsUp) return MetaRankBuy.Unknown;
            MetaState s = BetweenRunsState;
            MetaRank r = MetaRanks.Find(MetaValues, id);
            int price = MetaRanks.NextPrice(s, MetaValues, r);
            MetaRankBuy b = MetaRanks.Buy(s, MetaValues, id);
            if (b == MetaRankBuy.AtMost || b == MetaRankBuy.Unknown) return b; // the last rank's line takes no press
            int rank = MetaRanks.RankOf(s, r);
            BetweenRunsNote = b == MetaRankBuy.Bought
                ? "Bought " + r.Name + " rank " + rank.ToString(CultureInfo.InvariantCulture) + " for " + price.ToString(CultureInfo.InvariantCulture) + " " + MetaValues.CurrencyName + "."
                : "Not enough " + MetaValues.CurrencyName + " for " + r.Name + " rank " + (rank + 1).ToString(CultureInfo.InvariantCulture) + " (" + price.ToString(CultureInfo.InvariantCulture) + ").";
            if (b != MetaRankBuy.Bought) return b;
            PlaySessionRecorder.Current?.NoteBetweenRunsRank(r.Name, rank, price, s.Currency);
            RackChanged("bought " + r.Name + " rank " + rank.ToString(CultureInfo.InvariantCulture) + " for " + price.ToString(CultureInfo.InvariantCulture) + ", "
                        + s.Currency.ToString(CultureInfo.InvariantCulture) + " " + MetaValues.CurrencyName + " left");
            return b;
        }
    }
}
