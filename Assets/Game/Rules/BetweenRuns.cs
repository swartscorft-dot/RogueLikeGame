using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// What lasts between runs, a first version behind a switch (decision 0009, a proposal by the producer that Kristian
    /// has not ruled on; work order 029, 2026-11-20: built switched off so he can try it). With the switch off, which is
    /// the default (<see cref="MetaValues.Enabled"/>), none of this is used: nothing is earned, shown or saved.
    ///
    /// One currency (placeholder name "Echoes"), earned in a run and never lost: <see cref="MetaValues.PerFloor"/> for each
    /// floor cleared, <see cref="MetaValues.PerElite"/> for each elite room cleared, <see cref="MetaValues.PerBoss"/> for the
    /// act boss. A rack of pieces (the Arcana idea of 0009: she owns more than she can equip): a piece costs currency to
    /// buy, once, and points to equip; the budget starts at <see cref="MetaValues.StartBudget"/> and grows by one for each
    /// feat done, once each, to <see cref="MetaValues.MostBudget"/>. Equipped pieces apply to the next run through the
    /// run's modifiers (<see cref="RunModifiers.Meta"/>), never through a settings asset.
    ///
    /// All numbers are first guesses, kept here with their defaults in code.
    /// </summary>
    [Serializable]
    public sealed class MetaValues
    {
        /// <summary>The switch (work order 029): off by default. The pause menu's "Between runs (trial)" line overrides it.</summary>
        public bool Enabled = false;
        public string CurrencyName = "Echoes";

        public int PerFloor = 10;              // each floor of a run cleared, the last one too
        public int PerElite = 15;              // each elite room cleared
        public int PerBoss = 100;              // the act boss killed (the act cleared)

        public int StartBudget = 3;            // points to equip with no feat done
        public int MostBudget = 8;             // never more, whatever is done

        public MetaPiece[] Pieces = MetaPieces.Create();

        // Work order 060 (decision 0014, the producer's proposal of 2026-11-21, a draft he has not ruled on; Kristian's idea of
        // 2026-11-20: "attachments til våpen"): her pistols' parts, on this currency, save and screen (WeaponAttachments.cs).
        /// <summary>The attachments' switch: off by default. The pause menu's "Weapon attachments (trial)" line overrides it; it works only with <see cref="Enabled"/>'s trial on.</summary>
        public bool Attachments = false;
        /// <summary>Echoes for each ordinary combat room cleared (not the elite room, which keeps its own), only while the attachments are on (work order 060).</summary>
        public int PerRoom = 3;
        public WeaponAttachment[] Parts = WeaponAttachments.Create();

        // Work order 099 (Kristian 2026-11-23: "noen som bør være på rene stats og. så er det også en sink"; 2026-11-24 he ticked
        // "Rang som kjøpes mange ganger"): pure stats bought rank by rank on this currency, save and screen (BetweenRunsRanks.cs).
        // Only with the trial on, as everything here. An empty table gives the screen and every run as before.
        public MetaRank[] Ranks = MetaRanks.Create();
        /// <summary>A stat's first rank costs this (the producer's starting value, work order 099): a run that dies on floor 3 earns 50.</summary>
        public int FirstPrice = 40;
        /// <summary>Each next rank costs the one before times this, rounded to the nearest 5 (the producer's starting value): 40, 55, 80, ... 825.</summary>
        public float PriceGrowth = 1.4f;

        // Work order 110 (Kristian, 2026-11-24: he ticked "Hennes kit" ("flere våpendeler") and said yes to the sprint plan's row
        // "Tre valg per plass, hver med en buff og en kostnad. Det som synes av dem er lite og stille"): five more parts for her
        // pistols, so each slot (sight, barrel, grip) holds three (WeaponAttachments.Create, the rows marked MorePart).
        /// <summary>
        /// The five more parts' switch, on: the store holds nine. Off, it holds 060's four, and the screen, the end screen and a
        /// run are as before field for field; a save that owns one of the five keeps it, unshown and unused. Read only with the
        /// attachments on (<see cref="Attachments"/>), as everything of the parts.
        /// </summary>
        public bool MoreParts = true;
    }

    /// <summary>A piece changes a rule of the run, or is a raw stat step (0009: a few, large and capped).</summary>
    public enum MetaPieceKind { Rule, Stats }

    /// <summary>What a piece does to a run, each read in one place (<see cref="RunMetaEffects"/>).</summary>
    public enum MetaEffect
    {
        /// <summary>A run starts with this share of the focus gauge filled.</summary>
        StartFocus,
        /// <summary>A hit that marks an enemy with no marks gives this many marks more.</summary>
        FirstMark,
        /// <summary>This many times a run, a floor reward's skills can be drawn again.</summary>
        Redraw,
        /// <summary>The pool skill struck out on the screen is never offered in the run.</summary>
        StrikeOut,
        /// <summary>The dash holds Amount charges; each comes back in Amount2 times its cooldown.</summary>
        DashCharges,
        /// <summary>The first chest of every floor offers this many upgrades more.</summary>
        DeepChest,
        /// <summary>Maximum health up by the share Amount of her own.</summary>
        MaxHealth,
        /// <summary>Damage dealt up by the share Amount.</summary>
        Damage,
        /// <summary>Elite rooms offer this many skill upgrades more.</summary>
        EliteOffers,
        /// <summary>Work order 106, Knight's Resolve: Amount times a run, a blow that would kill her leaves her at 1 health and dazes the enemies within Amount2 m.</summary>
        LastStand,
    }

    /// <summary>
    /// One piece of the rack (work order 029): a line of data. A rule changer has one small hook where its effect is read;
    /// a stat step is a share. <see cref="Needs"/> names a piece that must be owned to buy this one and equipped to equip
    /// it (Thick Hide's second rank). <see cref="FromFeat"/> marks a piece that is not sold: it is owned once that feat is done.
    /// </summary>
    [Serializable]
    public sealed class MetaPiece
    {
        public string Id = "";
        public string Name = "";
        public MetaPieceKind Kind;
        public int Points = 1;
        public int Price;
        public MetaEffect Effect;
        public float Amount;
        public float Amount2 = 1f;
        public string Line = "";
        public string Needs = "";
        public bool Sold = true;
        public MetaFeat FromFeat;

        public MetaPiece() { }

        public MetaPiece(string id, string name, MetaPieceKind kind, int points, int price, MetaEffect effect, float amount, string line)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Points = points;
            Price = price;
            Effect = effect;
            Amount = amount;
            Line = line;
        }

        public string KindWord => Kind == MetaPieceKind.Stats ? "stats" : "rule";
        public override string ToString() => Name;
    }

    /// <summary>
    /// The first pieces (work order 029's table, in its order), all first guesses. Changed from the table: Marked Target
    /// counts any hit that marks (a skill's or a basic shot's every-few-hits mark); Second Look redraws the floor reward,
    /// the only offer of skills; Thick Hide's second rank is its own line that needs the first owned to buy and equipped
    /// to equip, and the two add: +40 % of her own health with both, for 3 points.
    /// </summary>
    public static class MetaPieces
    {
        public const string SteadyStart = "steady-start";
        public const string MarkedTarget = "marked-target";
        public const string SecondLook = "second-look";
        public const string NotThatOne = "not-that-one";
        public const string SpareStep = "spare-step";
        public const string DeepChests = "deep-chests";
        public const string ThickHide = "thick-hide";
        public const string ThickHide2 = "thick-hide-2";
        public const string HeavyHand = "heavy-hand";
        public const string KnightsTrophy = "knights-trophy";

        const MetaPieceKind R = MetaPieceKind.Rule, S = MetaPieceKind.Stats;

        public static MetaPiece[] Create() => new[]
        {
            new MetaPiece(SteadyStart, "Steady Start", R, 1, 40, MetaEffect.StartFocus, 0.5f, "A run starts with the focus gauge half full."),
            new MetaPiece(MarkedTarget, "Marked Target", R, 2, 60, MetaEffect.FirstMark, 1f, "A hit that marks an enemy with no marks gives two marks, not one."),
            new MetaPiece(SecondLook, "Second Look", R, 1, 60, MetaEffect.Redraw, 1f, "Once a run, a floor reward's skills can be drawn again (X on the offer)."), // work order 047: Tab is the map's
            new MetaPiece(NotThatOne, "Not That One", R, 1, 80, MetaEffect.StrikeOut, 0f, "One pool skill, chosen here, is never offered in the run."),
            new MetaPiece(SpareStep, "Spare Step", R, 2, 120, MetaEffect.DashCharges, 2f, "The dash holds two charges; each takes a third longer to come back.") { Amount2 = 4f / 3f },
            new MetaPiece(DeepChests, "Deep Chests", R, 1, 60, MetaEffect.DeepChest, 1f, "The first chest of every floor offers three upgrades, not two."),
            new MetaPiece(ThickHide, "Thick Hide", S, 2, 80, MetaEffect.MaxHealth, 0.2f, "Maximum health +20 %."),
            new MetaPiece(ThickHide2, "Thick Hide II", S, 1, 160, MetaEffect.MaxHealth, 0.2f, "Maximum health +20 % more, on top of Thick Hide.") { Needs = ThickHide },
            new MetaPiece(HeavyHand, "Heavy Hand", S, 3, 150, MetaEffect.Damage, 0.15f, "Damage +15 %."),
            new MetaPiece(KnightsTrophy, "Knight's Trophy", R, 2, 0, MetaEffect.EliteOffers, 1f, "Elite rooms offer four upgrades, not three.")
                { Sold = false, FromFeat = MetaFeat.ActBoss },
        };
    }

    /// <summary>The five feats (work order 029), each done once; each raises the budget by one.</summary>
    public enum MetaFeat { FirstFloor, EliteAboveHalf, ReachFloor3, ReachFloor5, ActBoss }

    public static class MetaFeats
    {
        public static readonly MetaFeat[] All = { MetaFeat.FirstFloor, MetaFeat.EliteAboveHalf, MetaFeat.ReachFloor3, MetaFeat.ReachFloor5, MetaFeat.ActBoss };

        /// <summary>The feat's name in the save.</summary>
        public static string Id(MetaFeat feat)
        {
            switch (feat)
            {
                case MetaFeat.FirstFloor: return "clear-floor-1";
                case MetaFeat.EliteAboveHalf: return "elite-above-half";
                case MetaFeat.ReachFloor3: return "reach-floor-3";
                case MetaFeat.ReachFloor5: return "reach-floor-5";
                default: return "kill-act-boss";
            }
        }

        public static bool TryParse(string id, out MetaFeat feat)
        {
            foreach (MetaFeat f in All)
                if (Id(f) == id)
                {
                    feat = f;
                    return true;
                }
            feat = MetaFeat.FirstFloor;
            return false;
        }

        /// <summary>The feat in plain words, for the end screen.</summary>
        public static string Words(MetaFeat feat)
        {
            switch (feat)
            {
                case MetaFeat.FirstFloor: return "Cleared floor 1";
                case MetaFeat.EliteAboveHalf: return "Cleared an elite room without dropping under half health";
                case MetaFeat.ReachFloor3: return "Reached floor 3";
                case MetaFeat.ReachFloor5: return "Reached floor 5";
                default: return "Killed the act boss";
            }
        }
    }

    /// <summary>
    /// What lasts (work order 029): the currency, what is owned, what is equipped, the feats done, the pool skill struck
    /// out, and the save's version. Plain data; the save file is its JSON (<see cref="MetaSaveFile"/>).
    /// </summary>
    public sealed class MetaState
    {
        /// <summary>
        /// The newest save this code reads and writes: 2 since work order 099 (the ranks). A save with no rank bought is still
        /// written as version 1 (<see cref="VersionWithoutRanks"/>), character for character as before, so every earlier build
        /// reads it; one with ranks is version 2, so an earlier build, which would pass the ranks over and write the save again
        /// without them, keeps it aside instead (its rule for a newer save) and nothing is lost.
        /// </summary>
        public const int CurrentVersion = 2;
        /// <summary>The version a save with no rank bought is written as (work order 029's, and 060's).</summary>
        public const int VersionWithoutRanks = 1;

        public int Version = CurrentVersion;
        public int Currency;
        /// <summary>Everything ever earned, spent or not.</summary>
        public int EarnedInAll;
        /// <summary>Runs ended with the switch on.</summary>
        public int Runs;
        public readonly List<string> Owned = new List<string>();
        public readonly List<string> Equipped = new List<string>();
        public readonly List<string> Feats = new List<string>();
        public string StruckOut = "";
        /// <summary>Her pistols' parts bought, and those fitted, one a slot (work order 060). Kept while the attachments are off, never applied then.</summary>
        public readonly List<string> AttachmentsOwned = new List<string>();
        public readonly List<string> AttachmentsFitted = new List<string>();
        /// <summary>The ranks bought, by the stat's id (work order 099); a save of before has none, which is rank 0 of everything.</summary>
        public readonly Dictionary<string, int> Ranks = new Dictionary<string, int>(StringComparer.Ordinal);

        /// <summary>The rank kept for this stat (0 when none); <see cref="MetaRanks.RankOf"/> is what counts against the table.</summary>
        public int RankOf(string id) => id != null && Ranks.TryGetValue(id, out int n) ? n : 0;
        /// <summary>Any rank bought (work order 099): the save is then written as version 2.</summary>
        public bool HasRanks
        {
            get
            {
                foreach (KeyValuePair<string, int> p in Ranks)
                    if (p.Value > 0) return true;
                return false;
            }
        }

        public bool Owns(string id) => Owned.Contains(id);
        public bool IsEquipped(string id) => Equipped.Contains(id);
        public bool HasFeat(MetaFeat feat) => Feats.Contains(MetaFeats.Id(feat));

        public MetaState Copy()
        {
            var c = new MetaState { Version = Version, Currency = Currency, EarnedInAll = EarnedInAll, Runs = Runs, StruckOut = StruckOut };
            c.Owned.AddRange(Owned);
            c.Equipped.AddRange(Equipped);
            c.Feats.AddRange(Feats);
            c.AttachmentsOwned.AddRange(AttachmentsOwned); // work order 060
            c.AttachmentsFitted.AddRange(AttachmentsFitted);
            foreach (KeyValuePair<string, int> p in Ranks) c.Ranks[p.Key] = p.Value; // work order 099
            return c;
        }
    }

    /// <summary>What a run has earned so far (work order 029), and the feats it has done, new or not.</summary>
    public sealed class RunEarnings
    {
        public int FloorsCleared;
        public int ElitesCleared;
        public bool BossKilled;
        /// <summary>The feats done in this run, in the order done, each once.</summary>
        public readonly List<MetaFeat> Feats = new List<MetaFeat>();
        /// <summary>After the run ended: the feats it did for the first time ever, and the total currency then.</summary>
        public readonly List<MetaFeat> NewFeats = new List<MetaFeat>();
        public int TotalAfter;
        public bool Committed;

        public void Do(MetaFeat feat)
        {
            if (!Feats.Contains(feat)) Feats.Add(feat);
        }

        public int FromFloors(MetaValues v) => FloorsCleared * v.PerFloor;
        public int FromElites(MetaValues v) => ElitesCleared * v.PerElite;
        public int FromBoss(MetaValues v) => BossKilled ? v.PerBoss : 0;
        /// <summary>Ordinary combat rooms cleared, counted only while the attachments are on (work order 060): 0 otherwise, so every total is 029's.</summary>
        public int RoomsCleared;
        public int FromRooms(MetaValues v) => RoomsCleared * v.PerRoom;
        public int Total(MetaValues v) => FromFloors(v) + FromElites(v) + FromBoss(v) + FromRooms(v);
    }

    /// <summary>
    /// The equipped pieces' effects on one run (work order 029), held by the run's modifiers and read where each is used:
    /// her maximum health and damage (with the chests' steps), the focus she starts with, the marks of a first hit, the
    /// floor reward's redraw and its struck-out skill, the dash's charges and recharge, the first chest's offer and the elite
    /// room's. Empty (<see cref="Clear"/>), every number is the one that changes nothing.
    /// </summary>
    public sealed class RunMetaEffects
    {
        public float MaxHealthShare;
        public float DamageShare;
        public float StartFocusShare;
        public int FirstMarkExtra;
        public int Redraws;
        /// <summary>Redraws not yet used this run.</summary>
        public int RedrawsLeft;
        public string StruckOut = "";
        public int DashCharges = 1;
        public float DashRechargeScale = 1f;
        public int FirstChestExtra;
        public int EliteOffersExtra;
        /// <summary>The names of the pieces applied, in rack order.</summary>
        public readonly List<string> Pieces = new List<string>();

        // Work order 099: the ranks bought between runs, put here beside the pieces (MetaRanks.Apply), each read where the
        // rack's share of the same stat is read (HerStats; the room's heal). 0 with none.
        /// <summary>Maximum health up by this share of her own, added to <see cref="MaxHealthShare"/>.</summary>
        public float RankHealthShare;
        /// <summary>Damage dealt up by this share, added to <see cref="DamageShare"/>.</summary>
        public float RankDamageShare;
        /// <summary>Crit chance up by this many points (0.03: 3), added to her start's and the chests' steps.</summary>
        public float RankCritChance;
        /// <summary>A cleared room heals this many points of her maximum more (0.02: 2), added to the room's own share.</summary>
        public float RankRoomHeal;
        /// <summary>The ranks applied, "Health 3", in the table's order.</summary>
        public readonly List<string> Ranks = new List<string>();

        // Work order 106: Knight's Resolve (KnightsResolve, BossFinale.cs), read where a blow lands on her (PlayerHealth). 0 without it.
        /// <summary>Times this run a blow that would kill her leaves her at 1 health.</summary>
        public int Resolves;
        /// <summary>Of those, not yet spent this run.</summary>
        public int ResolvesLeft;
        /// <summary>m from her within which the enemies are dazed when one is spent.</summary>
        public float ResolveReach;

        /// <summary>Any piece of the rack applied (the ranks are <see cref="AnyRanks"/>).</summary>
        public bool Any => Pieces.Count > 0;
        public bool AnyRanks => Ranks.Count > 0;

        public void Clear()
        {
            MaxHealthShare = 0f;
            DamageShare = 0f;
            StartFocusShare = 0f;
            FirstMarkExtra = 0;
            Redraws = 0;
            RedrawsLeft = 0;
            StruckOut = "";
            DashCharges = 1;
            DashRechargeScale = 1f;
            FirstChestExtra = 0;
            EliteOffersExtra = 0;
            Pieces.Clear();
            RankHealthShare = 0f; // work order 099
            RankDamageShare = 0f;
            RankCritChance = 0f;
            RankRoomHeal = 0f;
            Ranks.Clear();
            Resolves = 0; // work order 106
            ResolvesLeft = 0;
            ResolveReach = 0f;
        }

        /// <summary>The numbers in one line, for tests and the log; the ranks' at its end (work order 099).</summary>
        public override string ToString() =>
            string.Format(CultureInfo.InvariantCulture,
                          "hp+{0} dmg+{1} focus {2} mark+{3} redraws {4} struck '{5}' dash {6}x{7} chest+{8} elite+{9}",
                          MaxHealthShare, DamageShare, StartFocusShare, FirstMarkExtra, Redraws, StruckOut, DashCharges,
                          DashRechargeScale, FirstChestExtra, EliteOffersExtra)
            + string.Format(CultureInfo.InvariantCulture, " ranks hp+{0} dmg+{1} crit+{2} room heal+{3}", RankHealthShare, RankDamageShare, RankCritChance, RankRoomHeal);
    }

    public enum MetaBuy { Bought, AlreadyOwned, NotEnough, NeedsFirst, NotForSale, Unknown }
    public enum MetaEquip { Equipped, AlreadyEquipped, NotOwned, OverBudget, NeedsFirst, Unknown }

    /// <summary>
    /// The rack's rules (work order 029): the budget, buying, equipping and taking off, what a run's end adds, and what
    /// the equipped pieces do to the next run.
    /// </summary>
    public static class MetaRack
    {
        public static MetaPiece Find(MetaValues v, string id) =>
            v?.Pieces?.FirstOrDefault(p => p != null && p.Id == id);

        /// <summary>The points she may equip: the start budget, one more per feat done, never more than the most.</summary>
        public static int Budget(MetaState s, MetaValues v)
        {
            int feats = MetaFeats.All.Count(s.HasFeat);
            return Math.Min(v.MostBudget, v.StartBudget + feats);
        }

        public static int PointsUsed(MetaState s, MetaValues v) =>
            s.Equipped.Select(id => Find(v, id)).Where(p => p != null).Sum(p => p.Points);

        public static int PointsLeft(MetaState s, MetaValues v) => Budget(s, v) - PointsUsed(s, v);

        public static MetaBuy CanBuy(MetaState s, MetaValues v, string id)
        {
            MetaPiece p = Find(v, id);
            if (p == null) return MetaBuy.Unknown;
            if (s.Owns(id)) return MetaBuy.AlreadyOwned;
            if (!p.Sold) return MetaBuy.NotForSale;
            if (!string.IsNullOrEmpty(p.Needs) && !s.Owns(p.Needs)) return MetaBuy.NeedsFirst;
            if (s.Currency < p.Price) return MetaBuy.NotEnough;
            return MetaBuy.Bought;
        }

        /// <summary>Buys the piece if it may: its price comes off the currency, once. It is not equipped by buying.</summary>
        public static MetaBuy Buy(MetaState s, MetaValues v, string id)
        {
            MetaBuy r = CanBuy(s, v, id);
            if (r != MetaBuy.Bought) return r;
            s.Currency -= Find(v, id).Price;
            s.Owned.Add(id);
            return r;
        }

        public static MetaEquip CanEquip(MetaState s, MetaValues v, string id)
        {
            MetaPiece p = Find(v, id);
            if (p == null) return MetaEquip.Unknown;
            if (s.IsEquipped(id)) return MetaEquip.AlreadyEquipped;
            if (!s.Owns(id)) return MetaEquip.NotOwned;
            if (!string.IsNullOrEmpty(p.Needs) && !s.IsEquipped(p.Needs)) return MetaEquip.NeedsFirst;
            if (p.Points > PointsLeft(s, v)) return MetaEquip.OverBudget;
            return MetaEquip.Equipped;
        }

        public static MetaEquip Equip(MetaState s, MetaValues v, string id)
        {
            MetaEquip r = CanEquip(s, v, id);
            if (r == MetaEquip.Equipped) s.Equipped.Add(id);
            return r;
        }

        /// <summary>Takes the piece off, and any equipped piece that needs it. False when it was not equipped.</summary>
        public static bool TakeOff(MetaState s, MetaValues v, string id)
        {
            if (!s.IsEquipped(id)) return false;
            s.Equipped.Remove(id);
            foreach (MetaPiece p in v.Pieces)
                if (p != null && p.Needs == id) s.Equipped.Remove(p.Id);
            return true;
        }

        /// <summary>
        /// A save read back (or made new): every piece it does not know, or owns twice, is dropped; a piece not owned or
        /// whose need is not met comes off; the pieces given by a feat done are owned; while over the budget the last
        /// equipped come off; the currency is never under 0.
        /// </summary>
        public static void Tidy(MetaState s, MetaValues v)
        {
            Distinct(s.Owned, id => Find(v, id) != null);
            Distinct(s.Feats, id => MetaFeats.TryParse(id, out _));
            foreach (MetaPiece p in v.Pieces)
                if (p != null && !p.Sold && s.HasFeat(p.FromFeat) && !s.Owns(p.Id)) s.Owned.Add(p.Id);
            Distinct(s.Equipped, id => s.Owns(id));
            for (int i = s.Equipped.Count - 1; i >= 0; i--)
            {
                MetaPiece p = Find(v, s.Equipped[i]);
                if (!string.IsNullOrEmpty(p.Needs) && !s.Equipped.Contains(p.Needs)) s.Equipped.RemoveAt(i);
            }
            while (s.Equipped.Count > 0 && PointsUsed(s, v) > Budget(s, v)) TakeOff(s, v, s.Equipped[s.Equipped.Count - 1]);
            s.Currency = Math.Max(0, s.Currency);
            s.EarnedInAll = Math.Max(0, s.EarnedInAll);
            s.Runs = Math.Max(0, s.Runs);
            if (s.StruckOut == null) s.StruckOut = "";
            WeaponRack.Tidy(s, v); // work order 060: her pistols' parts, the one place a save's are tidied
            MetaRanks.Tidy(s); // work order 099: the ranks, kept as paid for
        }

        static void Distinct(List<string> list, Func<string, bool> keep)
        {
            var seen = new HashSet<string>();
            list.RemoveAll(id => id == null || !keep(id) || !seen.Add(id));
        }

        /// <summary>
        /// A run ended (work order 029): what it earned goes on the currency, never lost, and its feats done for the first
        /// time are kept (each raises the budget once); a piece given by a feat is owned from then. Returns the new feats,
        /// also kept in <paramref name="run"/>, with the total after. Only the first call counts.
        /// </summary>
        public static List<MetaFeat> Commit(MetaState s, MetaValues v, RunEarnings run)
        {
            if (run == null || run.Committed) return run?.NewFeats ?? new List<MetaFeat>();
            run.Committed = true;
            int earned = run.Total(v);
            s.Currency += earned;
            s.EarnedInAll += earned;
            s.Runs++;
            run.NewFeats.Clear();
            foreach (MetaFeat f in run.Feats)
            {
                if (s.HasFeat(f)) continue;
                s.Feats.Add(MetaFeats.Id(f));
                run.NewFeats.Add(f);
            }
            Tidy(s, v);
            run.TotalAfter = s.Currency;
            return run.NewFeats;
        }

        /// <summary>
        /// What the equipped pieces do to the next run, put on <paramref name="into"/> (cleared first). The struck-out skill
        /// is <paramref name="struckOut"/>, the one chosen on the screen. With nothing equipped it is left empty.
        /// </summary>
        public static void Apply(MetaState s, MetaValues v, RunMetaEffects into, string struckOut = null)
        {
            into.Clear();
            foreach (MetaPiece p in v.Pieces)
            {
                if (p == null || !s.IsEquipped(p.Id)) continue;
                into.Pieces.Add(p.Name);
                switch (p.Effect)
                {
                    case MetaEffect.StartFocus: into.StartFocusShare = Math.Max(into.StartFocusShare, p.Amount); break;
                    case MetaEffect.FirstMark: into.FirstMarkExtra += (int)Math.Round(p.Amount); break;
                    case MetaEffect.Redraw: into.Redraws += (int)Math.Round(p.Amount); break;
                    case MetaEffect.StrikeOut: into.StruckOut = struckOut ?? s.StruckOut ?? ""; break;
                    case MetaEffect.DashCharges:
                        into.DashCharges = Math.Max(into.DashCharges, (int)Math.Round(p.Amount));
                        into.DashRechargeScale *= p.Amount2;
                        break;
                    case MetaEffect.DeepChest: into.FirstChestExtra += (int)Math.Round(p.Amount); break;
                    case MetaEffect.MaxHealth: into.MaxHealthShare += p.Amount; break;
                    case MetaEffect.Damage: into.DamageShare += p.Amount; break;
                    case MetaEffect.EliteOffers: into.EliteOffersExtra += (int)Math.Round(p.Amount); break;
                    case MetaEffect.LastStand: // work order 106: Knight's Resolve
                        into.Resolves += (int)Math.Round(p.Amount);
                        into.ResolveReach = Math.Max(into.ResolveReach, p.Amount2);
                        break;
                }
            }
            into.RedrawsLeft = into.Redraws;
            into.ResolvesLeft = into.Resolves; // work order 106
        }

        /// <summary>
        /// The pool skill struck out (Not That One), from the pool's names: the one kept if it is in the pool, else the
        /// first; <paramref name="step"/> moves it along the pool, round. Empty with no pool.
        /// </summary>
        public static string StruckOut(string kept, IReadOnlyList<string> pool, int step = 0)
        {
            if (pool == null || pool.Count == 0) return "";
            int at = -1;
            for (int i = 0; i < pool.Count; i++)
                if (pool[i] == kept) at = i;
            if (at < 0) return pool[0];
            int n = pool.Count;
            return pool[((at + step) % n + n) % n];
        }

        /// <summary>The whole rack's price: what buys everything that is sold.</summary>
        public static int WholePrice(MetaValues v) => v.Pieces.Where(p => p != null && p.Sold).Sum(p => p.Price);
    }

    /// <summary>
    /// Second Look's redraw of a floor reward (work order 029): the skills she lacks, the ones on screen left out while
    /// enough others are left, drawn from the run's seed and the floor on a stream of its own, so the same run with the same
    /// picks redraws the same. Fewer others than the offer: the ones on screen fill it.
    /// </summary>
    public static class RewardRedraw
    {
        public const string Stage = "floor reward redraw";

        public static List<int> Offer(ulong runSeed, int floor, IReadOnlyList<bool> equipped, int count, IReadOnlyCollection<int> shown)
        {
            var others = new List<int>();
            var again = new List<int>();
            for (int slot = 0; slot < equipped.Count; slot++)
            {
                if (equipped[slot]) continue;
                if (shown != null && shown.Contains(slot)) again.Add(slot);
                else others.Add(slot);
            }
            LevelRandom rng = LevelRandom.Stream(runSeed, Stage, floor);
            rng.Shuffle(others);
            rng.Shuffle(again);
            others.AddRange(again);
            return others.Take(Math.Max(0, count)).ToList();
        }
    }
}
