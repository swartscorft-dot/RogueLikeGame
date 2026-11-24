using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>The stat a rank raises (work order 099).</summary>
    public enum MetaRankStat { Health, Damage, CritChance, RoomHeal }

    /// <summary>
    /// One stat of the ranks bought between runs (work order 099, Kristian 2026-11-23: "noen som bør være på rene stats og. så er
    /// det også en sink"; asked on 2026-11-24 he ticked "Rang som kjøpes mange ganger"): a line of data. A rank is bought once
    /// with the currency, never equipped or taken off, takes no budget point, and always counts once bought; <see cref="Most"/>
    /// is its last rank (the pillars: "raw stats in noticeable, capped steps").
    /// </summary>
    [Serializable]
    public sealed class MetaRank
    {
        public string Id = "";
        public string Name = "";
        public MetaRankStat Stat;
        /// <summary>What one rank gives, as a share: 0.10 is +10 %; for crit chance and room heal, 0.03 is 3 points.</summary>
        public float Step;
        /// <summary>The last rank.</summary>
        public int Most = 1;

        public MetaRank() { }

        public MetaRank(string id, string name, MetaRankStat stat, float step, int most)
        {
            Id = id;
            Name = name;
            Stat = stat;
            Step = step;
            Most = most;
        }

        public override string ToString() => Name;
    }

    /// <summary>How a rank's purchase went (work order 099).</summary>
    public enum MetaRankBuy { Bought, NotEnough, AtMost, Unknown }

    /// <summary>
    /// The ranks bought between runs (work order 099): the table, the price, buying, what they do to a run, and their words.
    ///
    /// The price is one rule for every stat: the first rank costs <see cref="MetaValues.FirstPrice"/>, each next one the one
    /// before times <see cref="MetaValues.PriceGrowth"/>, rounded to the nearest 5 (the rounding is of the price alone, never
    /// carried on: 40, 55, 80, 110, 155, 215, 300, 420, 590, 825 with 40 and 1.4, the order's own list).
    ///
    /// What a rank does adds to the same stat's other sources by the rule <see cref="HerStats"/> already has for them: health
    /// and damage are shares added to the rack's (Thick Hide's and Heavy Hand's), as two of the rack's pieces add to each
    /// other, then her own health is multiplied by that sum and the chests' health is added after, and her damage by that sum
    /// times the chests' share; crit chance points add to her start's and the chests' steps (to 100 %); room heal points add to
    /// the share of her maximum a cleared room heals (the ordinary rooms' 8 % and the elite room's 20 %), Mending's health after.
    /// </summary>
    public static class MetaRanks
    {
        public const string Health = "health";
        public const string Damage = "damage";
        public const string CritChance = "crit-chance";
        public const string RoomHeal = "room-heal";

        /// <summary>
        /// The producer's starting set (work order 099's table), his to tune; the principle is "a step she feels", so no rank
        /// under these sizes: health +10 % of her own a rank, 10 ranks; damage +6 %, 10; crit chance +3 points, 8; room heal +2
        /// points of the share healed when a room is cleared, 5.
        /// </summary>
        public static MetaRank[] Create() => new[]
        {
            new MetaRank(Health, "Health", MetaRankStat.Health, 0.10f, 10),
            new MetaRank(Damage, "Damage", MetaRankStat.Damage, 0.06f, 10),
            new MetaRank(CritChance, "Crit chance", MetaRankStat.CritChance, 0.03f, 8),
            new MetaRank(RoomHeal, "Room heal", MetaRankStat.RoomHeal, 0.02f, 5),
        };

        public static MetaRank Find(MetaValues v, string id)
        {
            if (v?.Ranks == null || id == null) return null;
            foreach (MetaRank r in v.Ranks)
                if (r != null && r.Id == id) return r;
            return null;
        }

        /// <summary>The rank she has of this stat now, never more than its last (a save can hold more after a retune: kept, not applied).</summary>
        public static int RankOf(MetaState s, MetaRank r) => r == null || s == null ? 0 : Math.Max(0, Math.Min(Math.Max(0, r.Most), s.RankOf(r.Id)));

        public static bool AtMost(MetaState s, MetaRank r) => r != null && RankOf(s, r) >= Math.Max(0, r.Most);

        /// <summary>The price of rank number <paramref name="rank"/> (1 is the first): the first price times the growth to the rank's power, rounded to the nearest 5.</summary>
        public static int Price(MetaValues v, int rank)
        {
            if (v == null || rank < 1) return 0;
            double raw = v.FirstPrice * Math.Pow(Math.Max(0.0, v.PriceGrowth), rank - 1);
            if (double.IsNaN(raw) || double.IsInfinity(raw) || raw >= int.MaxValue) return int.MaxValue;
            return (int)Math.Max(0.0, Math.Round(raw / 5.0, MidpointRounding.AwayFromZero) * 5.0);
        }

        /// <summary>The price of her next rank of this stat; 0 at its last.</summary>
        public static int NextPrice(MetaState s, MetaValues v, MetaRank r) => r == null || AtMost(s, r) ? 0 : Price(v, RankOf(s, r) + 1);

        /// <summary>Every rank of this stat, added up.</summary>
        public static int WholePrice(MetaValues v, MetaRank r)
        {
            int sum = 0;
            for (int k = 1; r != null && k <= r.Most; k++) sum += Price(v, k);
            return sum;
        }

        /// <summary>Every rank of every stat, added up: what the whole store takes.</summary>
        public static int WholePrice(MetaValues v)
        {
            int sum = 0;
            if (v?.Ranks != null)
                foreach (MetaRank r in v.Ranks) sum += WholePrice(v, r);
            return sum;
        }

        public static MetaRankBuy CanBuy(MetaState s, MetaValues v, string id)
        {
            MetaRank r = Find(v, id);
            if (r == null || s == null) return MetaRankBuy.Unknown;
            if (AtMost(s, r)) return MetaRankBuy.AtMost;
            if (s.Currency < NextPrice(s, v, r)) return MetaRankBuy.NotEnough;
            return MetaRankBuy.Bought;
        }

        /// <summary>Buys the next rank if it may: its price comes off the currency, and it counts from now on. It cannot be undone.</summary>
        public static MetaRankBuy Buy(MetaState s, MetaValues v, string id)
        {
            MetaRankBuy b = CanBuy(s, v, id);
            if (b != MetaRankBuy.Bought) return b;
            MetaRank r = Find(v, id);
            s.Currency -= NextPrice(s, v, r);
            s.Ranks[r.Id] = RankOf(s, r) + 1;
            return b;
        }

        /// <summary>
        /// A save read back: a rank under 1 is dropped. A rank of a stat the table does not know, or over its last, is kept as
        /// it is (Echoes were paid for it; a retune may bring it back) and only what the table allows is applied and shown.
        /// </summary>
        public static void Tidy(MetaState s)
        {
            if (s == null || s.Ranks.Count == 0) return;
            List<string> drop = null;
            foreach (KeyValuePair<string, int> p in s.Ranks)
                if (string.IsNullOrEmpty(p.Key) || p.Value < 1) (drop ?? (drop = new List<string>())).Add(p.Key);
            if (drop != null)
                foreach (string k in drop) s.Ranks.Remove(k);
        }

        /// <summary>
        /// What the ranks do to the next run, put on <paramref name="into"/> beside the rack's pieces (called after
        /// <see cref="MetaRack.Apply"/>, which clears it): nothing is cleared here. With no rank bought it is left as it was.
        /// </summary>
        public static void Apply(MetaState s, MetaValues v, RunMetaEffects into)
        {
            if (s == null || v?.Ranks == null || into == null) return;
            foreach (MetaRank r in v.Ranks)
            {
                int n = RankOf(s, r);
                if (n <= 0) continue;
                float share = n * r.Step;
                switch (r.Stat)
                {
                    case MetaRankStat.Health: into.RankHealthShare += share; break;
                    case MetaRankStat.Damage: into.RankDamageShare += share; break;
                    case MetaRankStat.CritChance: into.RankCritChance += share; break;
                    case MetaRankStat.RoomHeal: into.RankRoomHeal += share; break;
                }
                into.Ranks.Add(r.Name + " " + n.ToString(CultureInfo.InvariantCulture));
            }
        }

        /// <summary>The ranks held in one line, for the log: "Health 3, Damage 1"; null with none.</summary>
        public static string Held(MetaState s, MetaValues v)
        {
            if (s == null || v?.Ranks == null) return null;
            var sb = new StringBuilder();
            foreach (MetaRank r in v.Ranks)
            {
                int n = RankOf(s, r);
                if (n <= 0) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(r.Name).Append(' ').Append(n.ToString(CultureInfo.InvariantCulture));
            }
            return sb.Length > 0 ? sb.ToString() : null;
        }

        // ---------------- In words ----------------

        static string Pct(double share) => (share * 100.0).ToString("0.#", CultureInfo.InvariantCulture);

        /// <summary>What this many ranks give: "+30 % of her own health", "+12 % damage", "+6 points crit chance", "+4 points room heal".</summary>
        public static string Gives(MetaRank r, int ranks)
        {
            if (r == null) return "";
            string n = Pct(ranks * (double)r.Step);
            switch (r.Stat)
            {
                case MetaRankStat.Health: return "+" + n + " % of her own health";
                case MetaRankStat.Damage: return "+" + n + " % damage";
                case MetaRankStat.CritChance: return "+" + n + " points crit chance";
                default: return "+" + n + " points room heal";
            }
        }

        /// <summary>
        /// The line under a stat on the screen: what the next rank gives and what she has now ("Next rank: +10 % of her own
        /// health.  Now +30 %."), or at its last what all of them give.
        /// </summary>
        public static string NextWords(MetaState s, MetaRank r)
        {
            if (r == null) return "";
            int n = RankOf(s, r);
            if (AtMost(s, r)) return "Every rank bought: " + Gives(r, n) + ".";
            return "Next rank: " + Gives(r, 1) + "." + (n > 0 ? "  Now " + ShortNow(r, n) + "." : "");
        }

        static string ShortNow(MetaRank r, int n)
        {
            string x = Pct(n * (double)r.Step);
            return r.Stat == MetaRankStat.Health || r.Stat == MetaRankStat.Damage ? "+" + x + " %" : "+" + x + " points";
        }

        /// <summary>Its rank out of its most: "rank 3 of 10".</summary>
        public static string RankWords(MetaState s, MetaRank r) =>
            r == null ? "" : "rank " + RankOf(s, r).ToString(CultureInfo.InvariantCulture) + " of " + Math.Max(0, r.Most).ToString(CultureInfo.InvariantCulture);

        /// <summary>What it is to her now: "last rank", "price 80 Echoes" when she can pay, "80 Echoes, 20 more" when not.</summary>
        public static string StateWords(MetaState s, MetaValues v, MetaRank r)
        {
            if (r == null) return "";
            if (AtMost(s, r)) return "last rank";
            int price = NextPrice(s, v, r);
            string name = v?.CurrencyName ?? "";
            if (s.Currency >= price) return "price " + price.ToString(CultureInfo.InvariantCulture) + " " + name;
            return price.ToString(CultureInfo.InvariantCulture) + " " + name + ", " + (price - s.Currency).ToString(CultureInfo.InvariantCulture) + " more";
        }
    }

    /// <summary>
    /// Where the screen between runs puts its panels with the ranks (work order 099): beside work order 060's rule, as a rule
    /// EditMode can test. The ranks' panel stands to the right of the others ([rack][ranks], or [rack][weapon][ranks] with
    /// the attachments on), all centred where they fit; narrower, the rack's narrows first (never under
    /// <see cref="LeastRack"/>), then the ranks' (never under <see cref="LeastRanks"/>), then the weapon's, so none overlaps
    /// another or leaves the view. The rack stays the largest. With no rank in the table it is work order 060's rule exactly.
    /// </summary>
    public static partial class BetweenRunsLayout
    {
        /// <summary>The ranks' panel at its width (work order 099; a first guess: the rack's 680 stays the larger part).</summary>
        public const float RanksWidth = 360f;
        /// <summary>The ranks' panel is never narrower than this while it fits at all.</summary>
        public const float LeastRanks = 300f;

        public static void Panels(float viewWidth, bool weapon, bool ranks, out LayoutRect rack, out LayoutRect weaponPanel, out LayoutRect ranksPanel)
        {
            if (!ranks)
            {
                Panels(viewWidth, weapon, out rack, out weaponPanel);
                ranksPanel = new LayoutRect(0f, 0f, 0f, 0f);
                return;
            }
            float room = viewWidth - 2f * Margin;
            float rackW = RackWidth, weaponW = weapon ? WeaponWidth : 0f, ranksW = RanksWidth;
            float gaps = Gap * (weapon ? 2f : 1f);
            if (rackW + weaponW + ranksW + gaps > room) rackW = Math.Max(LeastRack, room - gaps - weaponW - ranksW);
            if (rackW + weaponW + ranksW + gaps > room) ranksW = Math.Max(LeastRanks, room - gaps - weaponW - rackW);
            if (weapon && rackW + weaponW + ranksW + gaps > room) weaponW = Math.Max(0f, room - gaps - rackW - ranksW);
            if (rackW + weaponW + ranksW + gaps > room) ranksW = Math.Max(0f, room - gaps - weaponW - rackW);
            float x = Math.Max(Margin, (viewWidth - (rackW + weaponW + ranksW + gaps)) * 0.5f);
            rack = new LayoutRect(x, 0f, rackW, 0f);
            x += rackW + Gap;
            if (weapon)
            {
                weaponPanel = new LayoutRect(x, 0f, weaponW, 0f);
                x += weaponW + Gap;
            }
            else weaponPanel = new LayoutRect(0f, 0f, 0f, 0f);
            ranksPanel = new LayoutRect(x, 0f, ranksW, 0f);
        }
    }
}
