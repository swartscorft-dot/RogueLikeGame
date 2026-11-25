using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The first five upgrades of the basic shot (work order 138, part 3; Kristian, 2026-11-25: "Også muligheter for å
    /// oppgradere autoattacks, med pierce og andre ting som feks on-hit, build up, oftere stacks osv", and his tick "Fem, én
    /// fra hver retning"): one from each of the analyst's four families and one more (draft kit-2026-11-26, section 4: A1, B2,
    /// C1, D1, D4). Each has three tiers, taken one after the other: a card of one she has is its next tier.
    /// </summary>
    public enum BasicShotUpgradeKind
    {
        /// <summary>A1, his "pierce": the shot goes on along her line to the next enemy on it, at a share of its damage.</summary>
        PiercingRound,
        /// <summary>B2, his "on-hit": each basic hit takes a little off every running skill cooldown.</summary>
        Quickening,
        /// <summary>C1, his "build up": damage builds over basic hits in a row; a miss, or a while with no hit, drops it.</summary>
        HotBarrel,
        /// <summary>D1, his "oftere stacks": the basic shot marks more often than every fifth hit.</summary>
        MarkingRounds,
        /// <summary>D4, the analyst's own (taken by him with the five): a basic shot on an enemy at the cap of marks spends them.</summary>
        TapOut,
    }

    /// <summary>
    /// The five upgrades' switches and numbers (work order 138), each a first guess of the draft round which the order let the
    /// builder choose the tiers; he tunes them by playing. Each upgrade has its own switch, on by default (his tick "Fem, én
    /// fra hver retning ... each behind its own switch"): off, it is never offered and, held, does nothing. A tier's numbers
    /// are at its place in each array (tier I first).
    /// </summary>
    [Serializable]
    public sealed class BasicShotUpgradeValues
    {
        public bool PiercingRound = true;
        public bool Quickening = true;
        public bool HotBarrel = true;
        public bool MarkingRounds = true;
        public bool TapOut = true;

        /// <summary>Tiers of each: three (the order).</summary>
        public const int Tiers = 3;

        // Piercing Round (the draft's A1: 50 %; rare 80 %; epic two more at 80 %).
        public float[] PierceShare = { 0.5f, 0.8f, 0.8f }; // of the shot's damage, to each enemy past the first on her line
        public int[] PierceTargets = { 1, 1, 2 };          // enemies past the first it goes on to, in the order they stand on the line

        // Quickening (the draft's B2: 0.1 s; rare 0.15; epic 0.2), never past the run's cooldown floor (RunValues.CooldownFloor).
        public float[] QuickeningSeconds = { 0.1f, 0.15f, 0.2f }; // s off every running skill cooldown (not the dash) a basic hit

        // Hot Barrel (the draft's C1: +3 % a hit to +30 % at 10 hits; rare +4 %; epic up to 15 hits).
        public float[] HeatPerHit = { 0.03f, 0.04f, 0.04f }; // damage a hit in a row adds to the next
        public int[] HeatMostHits = { 10, 10, 15 };          // hits in a row it builds over: at most +30 %, +40 %, +60 %
        public float HeatHoldSeconds = 1.5f;                // s with no basic hit after which it drops (the draft's); 0: only a miss drops it
        public float FlashGrowAtMost = 0.6f;                // her muzzle flash at full heat is this share larger, warming step by step
        public float LightGrowAtMost = 1.2f;                // and its muzzle light (work order 064's) this share brighter

        // Marking Rounds (the draft's D1: every 4th; rare every 3rd; epic every 2nd), in place of KitValues.BasicShotsPerMark (5).
        public int[] HitsPerMark = { 4, 3, 2 };

        // Tap Out (the draft's D4: at the cap of 5, +20 % a mark; rare from 4 marks; epic +30 % a mark).
        public int[] TapOutBelowCap = { 0, 1, 1 };         // marks under the cap (KitValues.MaxMarks) from which a basic hit spends them: at the cap, then one under
        public float[] TapOutBonus = { 0.2f, 0.2f, 0.3f };  // damage a mark spent adds, as a spender's (MarkRules.SpenderDamage)

        /// <summary>The upgrade's own switch.</summary>
        public bool On(BasicShotUpgradeKind kind)
        {
            switch (kind)
            {
                case BasicShotUpgradeKind.PiercingRound: return PiercingRound;
                case BasicShotUpgradeKind.Quickening: return Quickening;
                case BasicShotUpgradeKind.HotBarrel: return HotBarrel;
                case BasicShotUpgradeKind.MarkingRounds: return MarkingRounds;
                default: return TapOut;
            }
        }

        /// <summary>The number of tier <paramref name="tier"/> (1 to 3) in a tier array; its last for a tier past it.</summary>
        public static T At<T>(T[] values, int tier) => values == null || values.Length == 0 ? default : values[Math.Max(0, Math.Min(values.Length - 1, tier - 1))];
    }

    /// <summary>
    /// The basic-shot upgrades a run holds, and at which tier (work order 138): held with the run's modifiers and cleared with
    /// them (<see cref="RunModifiers.BasicShot"/>). It never writes to the settings.
    /// </summary>
    public sealed class RunBasicShotUpgrades
    {
        readonly int[] _tier = new int[5];
        readonly List<BasicShotUpgradeKind> _order = new List<BasicShotUpgradeKind>();

        /// <summary>The numbers and switches read for the run (the run's <see cref="RunValues.BasicShot"/>, set as one is taken).</summary>
        public BasicShotUpgradeValues Values = new BasicShotUpgradeValues();
        /// <summary>Her kit's own count to a basic mark and cap of marks, for the words (set as one is taken; the kit's defaults before).</summary>
        public int KitPerMark = 5, MarkCap = 5;
        /// <summary>Goes up with every take, for what shows them.</summary>
        public int Version { get; private set; }

        public static readonly BasicShotUpgradeKind[] All =
        {
            BasicShotUpgradeKind.PiercingRound, BasicShotUpgradeKind.Quickening, BasicShotUpgradeKind.HotBarrel,
            BasicShotUpgradeKind.MarkingRounds, BasicShotUpgradeKind.TapOut,
        };

        /// <summary>The tier held, 0 for none.</summary>
        public int TierOf(BasicShotUpgradeKind kind) => _tier[(int)kind];
        /// <summary>The tier that works now: the tier held, or 0 with its switch off.</summary>
        public int TierNow(BasicShotUpgradeKind kind) => Values != null && Values.On(kind) ? _tier[(int)kind] : 0;
        /// <summary>The kinds held, in the order first taken.</summary>
        public IReadOnlyList<BasicShotUpgradeKind> Held => _order;
        public bool Any => _order.Count > 0;

        /// <summary>Takes the kind's next tier (the first, or one up, never past the third). Returns the tier held after.</summary>
        public int Take(BasicShotUpgradeKind kind)
        {
            int i = (int)kind;
            if (_tier[i] == 0) _order.Add(kind);
            _tier[i] = Math.Min(BasicShotUpgradeValues.Tiers, _tier[i] + 1);
            Version++;
            return _tier[i];
        }

        public void Clear()
        {
            Array.Clear(_tier, 0, _tier.Length);
            _order.Clear();
            Version++;
        }
    }

    /// <summary>One basic-shot upgrade as a card offers it: which, and the tier taking it gives.</summary>
    public sealed class BasicShotOffer
    {
        public BasicShotUpgradeKind Kind;
        public int Tier;

        public string Name => BasicShotTexts.Name(Kind);
        /// <summary>"Piercing Round II": the name and its tier, as the end screen and the log say it.</summary>
        public string Short => BasicShotTexts.Short(Kind, Tier);
        public override string ToString() => Short;
    }

    /// <summary>
    /// The rules of the five (work order 138), with no engine in them. Nothing here aims for her or picks a target: a pierced
    /// shot goes on along the line she shot, and every other rule works on the target her own shot hit.
    /// </summary>
    public static class BasicShotRules
    {
        public const string Stage = "basic shot upgrade";

        /// <summary>
        /// The kinds a card may offer now: its switch on and not yet at its last tier. Every one of them as likely as the next
        /// (his tick "Nei, jevnt trukket": no family drawn more often once she has one of it).
        /// </summary>
        public static List<BasicShotUpgradeKind> Open(RunBasicShotUpgrades held, BasicShotUpgradeValues values)
        {
            values = values ?? new BasicShotUpgradeValues();
            var open = new List<BasicShotUpgradeKind>();
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All)
                if (values.On(k) && (held == null || held.TierOf(k) < BasicShotUpgradeValues.Tiers)) open.Add(k);
            return open;
        }

        /// <summary>One card, drawn evenly from what is open (<see cref="Open"/>) on the stream given, at the tier taking it gives; null when none is open.</summary>
        public static BasicShotOffer Draw(LevelRandom rng, RunBasicShotUpgrades held, BasicShotUpgradeValues values)
        {
            List<BasicShotUpgradeKind> open = Open(held, values);
            if (open.Count == 0) return null;
            BasicShotUpgradeKind k = open[rng.Below(open.Count)];
            return new BasicShotOffer { Kind = k, Tier = (held != null ? held.TierOf(k) : 0) + 1 };
        }

        // ---------------- Piercing Round ----------------

        /// <summary>The share of the shot's damage each enemy past the first takes, and how many past the first it goes on to; 0 for none.</summary>
        public static float PierceShare(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.PierceShare, tier));
        public static int PierceTargets(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0 : Math.Max(0, BasicShotUpgradeValues.At(v.PierceTargets, tier));

        /// <summary>A pierced enemy's damage: the shot's damage times the share, rounded, at least 1 for a shot that dealt any.</summary>
        public static int PierceDamage(int damage, float share) =>
            damage <= 0 || share <= 0f ? 0 : Math.Max(1, (int)Math.Round(damage * (double)share, MidpointRounding.AwayFromZero));

        /// <summary>
        /// The living targets past the first that a pierced shot goes on to: those the shot's line touches (as
        /// <see cref="LineShot.Hits(GroundPoint, GroundPoint, float, float, GroundPoint, float, float)"/>, so cover past which nothing
        /// is hit still stops it), farther along the line than the first, nearest first, at most <paramref name="most"/>. Their
        /// places in <paramref name="targets"/> go into <paramref name="into"/> (cleared first). Along her line only: it picks no
        /// target off it. Nothing is allocated when <paramref name="into"/> has room.
        /// </summary>
        public static void PiercedTargets(GroundPoint origin, GroundPoint aim, float range, float halfWidth, IReadOnlyList<TargetInfo> targets,
                                          float stopAt, int first, int most, List<int> into)
        {
            into.Clear();
            if (most <= 0 || first < 0 || first >= targets.Count) return;
            GroundPoint toAim = aim - origin;
            float length = toAim.Length;
            if (length < 1e-5f) return;
            GroundPoint dir = toAim * (1f / length);
            float firstAlong = (targets[first].Position - origin).Dot(dir);
            for (int i = 0; i < targets.Count; i++)
            {
                if (i == first) continue;
                TargetInfo t = targets[i];
                if (!t.Alive || !LineShot.Hits(origin, aim, range, halfWidth, t.Position, t.Radius, stopAt)) continue;
                float along = (t.Position - origin).Dot(dir);
                if (along < firstAlong) continue; // nearer her than the first: the shot met the first before it
                // Into its place, nearest first (a short list: insertion).
                int at = into.Count;
                while (at > 0 && (targets[into[at - 1]].Position - origin).Dot(dir) > along) at--;
                into.Insert(at, i);
                if (into.Count > most) into.RemoveAt(into.Count - 1);
            }
        }

        // ---------------- Quickening ----------------

        /// <summary>Seconds a basic hit takes off every running skill cooldown; 0 for none.</summary>
        public static float QuickeningSeconds(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.QuickeningSeconds, tier));

        /// <summary>
        /// What Quickening may still take off one recharge of a skill: its cut never makes a recharge shorter than the run's
        /// cooldown floor's share of the skill's cooldown as it runs (<see cref="RunValues.CooldownFloor"/>, the draft's "cap it
        /// with the floor"). <paramref name="cutSoFar"/>: what it took off this recharge already.
        /// </summary>
        public static float QuickeningRoom(float cooldown, float floor, float cutSoFar) =>
            Math.Max(0f, cooldown * (1f - Math.Max(0f, Math.Min(1f, floor))) - Math.Max(0f, cutSoFar));

        // ---------------- Marking Rounds ----------------

        /// <summary>Every how many basic hits the basic shot marks: the tier's, or the kit's own (5) with none.</summary>
        public static int HitsPerMark(int tier, int kitOwn, BasicShotUpgradeValues v) =>
            tier <= 0 ? kitOwn : Math.Max(1, BasicShotUpgradeValues.At(v.HitsPerMark, tier));

        // ---------------- Tap Out ----------------

        /// <summary>The fewest marks a basic hit spends at this tier: the cap, or under it; int.MaxValue for none.</summary>
        public static int TapOutFrom(int tier, int cap, BasicShotUpgradeValues v) =>
            tier <= 0 ? int.MaxValue : Math.Max(1, cap - Math.Max(0, BasicShotUpgradeValues.At(v.TapOutBelowCap, tier)));

        public static float TapOutBonus(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.TapOutBonus, tier));

        /// <summary>True when a basic hit on a target with this many marks spends them.</summary>
        public static bool TapsOut(int marks, int tier, int cap, BasicShotUpgradeValues v) => marks > 0 && marks >= TapOutFrom(tier, cap, v);
    }

    /// <summary>
    /// Hot Barrel's heat (work order 138, C1): each basic hit in a row adds its step for the hits after it, up to its most; a
    /// basic shot that misses (nothing hit, or cover stopped it) drops it all, as does a while with no basic hit
    /// (<see cref="BasicShotUpgradeValues.HeatHoldSeconds"/>). A skill's shot and the dash in between neither add nor drop
    /// it. The heat of a shot is what the hits before it built: the first hit in a row deals +0 %. Time is passed in.
    /// </summary>
    public sealed class HotBarrelHeat
    {
        int _hits;
        float _lastHitAt = float.NegativeInfinity;

        /// <summary>Hits in a row counted now (none once it has lapsed).</summary>
        public int Hits(float now, BasicShotUpgradeValues v) => Lapsed(now, v) ? 0 : _hits;

        bool Lapsed(float now, BasicShotUpgradeValues v) => v != null && v.HeatHoldSeconds > 0f && now - _lastHitAt > v.HeatHoldSeconds + 1e-4f;

        /// <summary>The damage share it adds to the next basic hit now, at this tier: 0 with none.</summary>
        public float Share(float now, int tier, BasicShotUpgradeValues v)
        {
            if (tier <= 0 || v == null) return 0f;
            return Math.Min(Hits(now, v), Math.Max(0, BasicShotUpgradeValues.At(v.HeatMostHits, tier))) * Math.Max(0f, BasicShotUpgradeValues.At(v.HeatPerHit, tier));
        }

        /// <summary>Its most at this tier (+30 % at tier I).</summary>
        public static float Most(int tier, BasicShotUpgradeValues v) =>
            tier <= 0 || v == null ? 0f : Math.Max(0, BasicShotUpgradeValues.At(v.HeatMostHits, tier)) * Math.Max(0f, BasicShotUpgradeValues.At(v.HeatPerHit, tier));

        /// <summary>How warm it is now, 0 to 1 of its most: what the muzzle flash shows.</summary>
        public float Warmth(float now, int tier, BasicShotUpgradeValues v)
        {
            float most = Most(tier, v);
            return most > 0f ? Math.Min(1f, Share(now, tier, v) / most) : 0f;
        }

        /// <summary>A basic hit: one more in the row (past its most it holds there).</summary>
        public void Hit(float now, BasicShotUpgradeValues v)
        {
            if (Lapsed(now, v)) _hits = 0;
            if (_hits < 1000) _hits++;
            _lastHitAt = now;
        }

        /// <summary>A basic shot that hit nothing: the row is over. Returns the hits it had.</summary>
        public int Miss(float now, BasicShotUpgradeValues v)
        {
            int had = Hits(now, v);
            Reset();
            return had;
        }

        public void Reset()
        {
            _hits = 0;
            _lastHitAt = float.NegativeInfinity;
        }
    }

    /// <summary>
    /// What each of the five did in a stretch of play (work order 138: "logs its use in the session file (how often it fired,
    /// what it added)"): counted as it happens, and read and emptied for the log at a floor's reward and the run's end.
    /// </summary>
    public sealed class BasicShotUpgradeTally
    {
        readonly int[] _fired = new int[5];
        readonly float[] _added = new float[5];

        /// <summary>How often it fired since the last read, and what it added (damage for Piercing Round, Hot Barrel and Tap Out; seconds of cooldown for Quickening; marks for Marking Rounds and the marks Tap Out spent are not counted here).</summary>
        public int Fired(BasicShotUpgradeKind k) => _fired[(int)k];
        public float Added(BasicShotUpgradeKind k) => _added[(int)k];

        public void Note(BasicShotUpgradeKind k, float added)
        {
            _fired[(int)k]++;
            _added[(int)k] += added;
        }

        /// <summary>Marking Rounds counts the marks it added net: one more than the kit's own count would have made, or one fewer.</summary>
        public void NoteAdded(BasicShotUpgradeKind k, float added) => _added[(int)k] += added;

        public bool Any
        {
            get
            {
                for (int i = 0; i < _fired.Length; i++) if (_fired[i] != 0 || _added[i] != 0f) return true;
                return false;
            }
        }

        public void Clear()
        {
            Array.Clear(_fired, 0, _fired.Length);
            Array.Clear(_added, 0, _added.Length);
        }
    }

    /// <summary>What the five are called and say (work order 138), for the cards, the pause menu's stats, the end screen and the log.</summary>
    public static class BasicShotTexts
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        /// <summary>What every basic-shot card says it is, at a glance (the order: "the card says plainly what kind it is").</summary>
        public const string KindWord = "BASIC SHOT UPGRADE";

        public static string Name(BasicShotUpgradeKind k)
        {
            switch (k)
            {
                case BasicShotUpgradeKind.PiercingRound: return "Piercing Round";
                case BasicShotUpgradeKind.Quickening: return "Quickening";
                case BasicShotUpgradeKind.HotBarrel: return "Hot Barrel";
                case BasicShotUpgradeKind.MarkingRounds: return "Marking Rounds";
                default: return "Tap Out";
            }
        }

        /// <summary>The tier's numeral: I, II, III.</summary>
        public static string Numeral(int tier) => tier <= 1 ? "I" : tier == 2 ? "II" : "III";

        public static string Short(BasicShotUpgradeKind k, int tier) => Name(k) + " " + Numeral(tier);

        /// <summary>The tier's ink on the cards and the end screen: I as a common, II as a rare, III as an epic.</summary>
        public static UpgradeTier InkTier(int tier) => tier <= 1 ? UpgradeTier.Common : tier == 2 ? UpgradeTier.Rare : UpgradeTier.Epic;

        static string Pct(double share) => (share * 100.0).ToString("0.#", s_Inv) + " %";

        /// <summary>One plain sentence of what it does, for a player who has never seen it.</summary>
        public static string Sentence(BasicShotUpgradeKind k)
        {
            switch (k)
            {
                case BasicShotUpgradeKind.PiercingRound: return "Your basic shot goes on through the enemy it hits, to the next one on its line.";
                case BasicShotUpgradeKind.Quickening: return "Every basic shot that hits brings all your skills back a little sooner.";
                case BasicShotUpgradeKind.HotBarrel: return "Basic shots that hit in a row deal more and more; a miss cools the barrel.";
                case BasicShotUpgradeKind.MarkingRounds: return "Your basic shot marks more often.";
                default: return "A basic shot on an enemy full of marks spends them all for more damage.";
            }
        }

        /// <summary>What tier <paramref name="tier"/> does, in numbers: "50 % of its damage to 1 more enemy".</summary>
        public static string What(BasicShotUpgradeKind k, int tier, BasicShotUpgradeValues v, int kitPerMark = 5, int cap = 5)
        {
            v = v ?? new BasicShotUpgradeValues();
            if (tier <= 0)
            {
                switch (k)
                {
                    case BasicShotUpgradeKind.PiercingRound: return "stops at the first enemy";
                    case BasicShotUpgradeKind.Quickening: return "no cut";
                    case BasicShotUpgradeKind.HotBarrel: return "no build-up";
                    case BasicShotUpgradeKind.MarkingRounds: return Every(kitPerMark);
                    default: return "spends no marks";
                }
            }
            switch (k)
            {
                case BasicShotUpgradeKind.PiercingRound:
                {
                    int n = BasicShotRules.PierceTargets(tier, v);
                    return Pct(BasicShotRules.PierceShare(tier, v)) + " to " + n.ToString(s_Inv) + " more " + (n == 1 ? "enemy" : "enemies");
                }
                case BasicShotUpgradeKind.Quickening:
                    return "-" + BasicShotRules.QuickeningSeconds(tier, v).ToString("0.##", s_Inv) + " s every cooldown a hit";
                case BasicShotUpgradeKind.HotBarrel:
                    return "+" + Pct(BasicShotUpgradeValues.At(v.HeatPerHit, tier)) + " a hit, to +" + Pct(HotBarrelHeat.Most(tier, v));
                case BasicShotUpgradeKind.MarkingRounds:
                    return Every(BasicShotRules.HitsPerMark(tier, kitPerMark, v));
                default:
                {
                    int from = BasicShotRules.TapOutFrom(tier, cap, v);
                    return "from " + from.ToString(s_Inv) + " marks, +" + Pct(BasicShotRules.TapOutBonus(tier, v)) + " a mark";
                }
            }
        }

        static string Every(int n) => n <= 1 ? "a mark every hit" : "a mark every " + (n == 2 ? "2nd" : n == 3 ? "3rd" : n.ToString(s_Inv) + "th") + " hit";

        /// <summary>The pause menu's line for one she holds: "Piercing Round II  80 % to 1 more enemy".</summary>
        public static string StatLine(BasicShotUpgradeKind k, int tier, BasicShotUpgradeValues v, int kitPerMark = 5, int cap = 5, bool on = true) =>
            Short(k, tier) + "  " + What(k, tier, v, kitPerMark, cap) + (on ? "" : "  (switched off)");

        /// <summary>The card of a basic-shot upgrade on the floor reward: its kind, its name with its tier, its sentence and its numbers before and after.</summary>
        public static OfferCard Card(BasicShotOffer offer, BasicShotUpgradeValues v, int kitPerMark = 5, int cap = 5)
        {
            var card = new OfferCard { Name = Short(offer.Kind, offer.Tier), Tiered = offer.Tier > 1, Tier = InkTier(offer.Tier) };
            card.KindLine = KindWord;
            card.TierLine = "TIER " + Numeral(offer.Tier) + " OF " + Numeral(BasicShotUpgradeValues.Tiers);
            card.Sentence = Sentence(offer.Kind);
            card.Numbers.Add(OfferTexts.Line("", What(offer.Kind, offer.Tier - 1, v, kitPerMark, cap), What(offer.Kind, offer.Tier, v, kitPerMark, cap)));
            return card;
        }
    }
}
