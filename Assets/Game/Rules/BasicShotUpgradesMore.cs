using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The rules of the four basic-shot upgrades of work order 141 (Kristian, 2026-11-26: asked which of the four that work order
    /// 138 did not build he wants next, told that Chain Kill and Pass It On pick their next target themselves, only after her own
    /// aimed shot has killed, as Ricochet's jumps do, he ticked all four: Chain Kill, Pass It On, Sixth Chamber, Hunter's Count),
    /// with no engine in them: the draft kit-2026-11-26's A3, C2, D2 and D3, beside 138's five (<see cref="BasicShotRules"/>). Their
    /// numbers are <see cref="BasicShotUpgradeValues"/>' (each tier at its place in an array).
    ///
    /// Nothing here aims for her or moves her aim. The two that pick a target do so only after her own shot has killed (his tick is
    /// his yes to that, and to nothing else): Chain Kill's bounce and Pass It On's marks go from the enemy that died to the nearest
    /// living enemy near it with a clear line past cover, by the Ricochet skill's rule (<see cref="NewSkillRules.NextJump"/>).
    /// </summary>
    public static class BasicShotMoreRules
    {
        /// <summary>
        /// True when a card of this kind may not be offered because she holds the other of a pair that exclude each other: Hunter's
        /// Count and Marking Rounds (the draft's "It excludes D1, so one or the other"). The draw stays even among what may be
        /// offered (his tick "Nei, jevnt trukket").
        /// </summary>
        public static bool Excluded(BasicShotUpgradeKind kind, RunBasicShotUpgrades held)
        {
            if (held == null) return false;
            if (kind == BasicShotUpgradeKind.HuntersCount) return held.TierOf(BasicShotUpgradeKind.MarkingRounds) > 0;
            if (kind == BasicShotUpgradeKind.MarkingRounds) return held.TierOf(BasicShotUpgradeKind.HuntersCount) > 0;
            return false;
        }

        static int Rounded(double value) => (int)Math.Round(value, MidpointRounding.AwayFromZero);

        // ---------------- Chain Kill ----------------

        public static float BounceShare(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.BounceShare, tier));
        public static float BounceReach(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.BounceReach, tier));
        public static int BounceMost(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0 : Math.Max(0, BasicShotUpgradeValues.At(v.BounceMost, tier));

        /// <summary>A bounce's damage: the shot's damage times the share, rounded, at least 1 for a shot that dealt any.</summary>
        public static int BounceDamage(int shotDamage, float share) =>
            shotDamage <= 0 || share <= 0f ? 0 : Math.Max(1, Rounded(shotDamage * (double)share));

        /// <summary>
        /// The enemies a Chain Kill's bounces hit, in order, as far as the targets now say (for the numbers and the tests; the game
        /// asks <see cref="NewSkillRules.NextJump"/> again after each bounce, as it learns whether the bounce killed): from
        /// <paramref name="dead"/> (the enemy her shot killed) to the nearest living enemy within the reach past cover, never one in
        /// <paramref name="skip"/> or one already bounced to; each bounce that <paramref name="kills"/> goes on, at most
        /// <paramref name="most"/> bounces. Into <paramref name="into"/> (cleared first).
        /// </summary>
        public static void Bounces(IReadOnlyList<TargetInfo> targets, int dead, IReadOnlyList<int> skip, float reach, int most, CoverLine cover,
                                   Func<int, bool> kills, List<int> into)
        {
            into.Clear();
            if (targets == null || dead < 0 || dead >= targets.Count || most <= 0 || reach <= 0f) return;
            var hit = new List<int>();
            if (skip != null) hit.AddRange(skip);
            int from = dead;
            for (int b = 0; b < most; b++)
            {
                int next = NewSkillRules.NextJump(targets, from, hit, reach, cover);
                if (next < 0) return;
                into.Add(next);
                hit.Add(next);
                if (kills == null || !kills(next)) return;
                from = next;
            }
        }

        // ---------------- Sixth Chamber ----------------

        /// <summary>Every how many basic shots fired the heavy round comes; 0 for none.</summary>
        public static int ShotsPerHeavy(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0 : Math.Max(1, BasicShotUpgradeValues.At(v.ShotsPerHeavy, tier));
        public static float HeavyScale(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 1f : Math.Max(0f, BasicShotUpgradeValues.At(v.HeavyScale, tier));

        /// <summary>
        /// One basic shot fired, hit or miss: true when it is the heavy round (the <paramref name="rounds"/>-th since the last one).
        /// <paramref name="fired"/>, the shots since the last heavy round, goes up, and back to 0 on the heavy round.
        /// </summary>
        public static bool ChamberShot(ref int fired, int rounds)
        {
            if (rounds <= 0) return false;
            fired = Math.Max(0, fired) + 1;
            if (fired < rounds) return false;
            fired = 0;
            return true;
        }

        /// <summary>True when the next basic shot fired is the heavy round.</summary>
        public static bool NextIsHeavy(int fired, int rounds) => rounds > 0 && fired + 1 >= rounds;

        /// <summary>The heavy round's damage: the shot's times the tier's, rounded.</summary>
        public static int HeavyDamage(int damage, float scale) => damage <= 0 ? damage : Math.Max(1, Rounded(damage * (double)Math.Max(0f, scale)));

        /// <summary>
        /// How many enemies past the first a shot goes on to: Piercing Round's (<paramref name="pierceTier"/>), and for the heavy round
        /// its own one, the more of the two (the draft: "A1's pierce is shared with C2": one pierce, never the two added).
        /// </summary>
        public static int PierceTargets(bool heavy, int pierceTier, BasicShotUpgradeValues v)
        {
            int own = BasicShotRules.PierceTargets(pierceTier, v);
            return heavy ? Math.Max(own, Math.Max(0, v.HeavyPierce)) : own;
        }

        /// <summary>The share of the shot's damage each enemy past the first takes: Piercing Round's, and for the heavy round the larger of it and its own.</summary>
        public static float PierceShare(bool heavy, int pierceTier, BasicShotUpgradeValues v)
        {
            float own = BasicShotRules.PierceShare(pierceTier, v);
            return heavy && v.HeavyPierce > 0 ? Math.Max(own, Math.Max(0f, v.HeavyPierceShare)) : own;
        }

        // ---------------- Hunter's Count ----------------

        public static int HuntHits(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0 : Math.Max(1, BasicShotUpgradeValues.At(v.HuntHits, tier));
        public static int HuntMarks(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0 : Math.Max(0, BasicShotUpgradeValues.At(v.HuntMarks, tier));

        /// <summary>
        /// A basic hit on an enemy whose own count is <paramref name="count"/>: the marks it adds (0 until the count is reached, then the
        /// tier's), and the count after (back to 0 when it marked). In the focus window (<paramref name="everyHit"/>) every hit marks
        /// once, as the kit's own count does, and the count stands.
        /// </summary>
        public static int HunterHit(ref int count, int tier, bool everyHit, BasicShotUpgradeValues v)
        {
            if (tier <= 0) return 0;
            if (everyHit) return 1;
            count = Math.Max(0, count) + 1;
            if (count < HuntHits(tier, v)) return 0;
            count = 0;
            return HuntMarks(tier, v);
        }

        // ---------------- Pass It On ----------------

        public static float PassReach(int tier, BasicShotUpgradeValues v) => tier <= 0 ? 0f : Math.Max(0f, BasicShotUpgradeValues.At(v.PassReach, tier));

        /// <summary>True when a kill of hers passes the marks at this tier: a basic shot's kill (a bounce's too), and at its last tier any of hers.</summary>
        public static bool PassesOn(int tier, bool basicShot, BasicShotUpgradeValues v) =>
            tier > 0 && (basicShot || BasicShotUpgradeValues.At(v.PassOnAnyKill, tier));

        /// <summary>
        /// The living enemy that takes the marks of <paramref name="dead"/> (its place in <paramref name="targets"/>): the nearest within the
        /// tier's reach with a clear line past cover (<see cref="NewSkillRules.NextJump"/>); -1 for none.
        /// </summary>
        public static int PassTo(IReadOnlyList<TargetInfo> targets, int dead, float reach, CoverLine cover) =>
            NewSkillRules.NextJump(targets, dead, null, reach, cover);

        /// <summary>The marks that land on the one that takes them: up to the cap.</summary>
        public static int Passed(int marks, int receiverHas, int cap) => Math.Max(0, Math.Min(Math.Max(0, cap) - Math.Max(0, receiverHas), Math.Max(0, marks)));
    }

    /// <summary>What the four of work order 141 are called and say, for the cards, the pause menu's stats, her sheet and the log.</summary>
    public static class BasicShotMoreTexts
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static string Pct(double share) => (share * 100.0).ToString("0.#", s_Inv) + " %";
        static string Metres(float m) => m.ToString("0.#", s_Inv) + " m";
        static string Nth(int n) => n == 1 ? "every" : n == 2 ? "every 2nd" : n == 3 ? "every 3rd" : "every " + n.ToString(s_Inv) + "th";

        public static string Sentence(BasicShotUpgradeKind k)
        {
            switch (k)
            {
                case BasicShotUpgradeKind.ChainKill: return "A basic shot that kills bounces on to the nearest enemy beside it; a bounce that kills bounces again.";
                case BasicShotUpgradeKind.SixthChamber: return "Every sixth basic shot you fire, hit or miss, is a heavy round: more damage, through one enemy, and a stagger.";
                case BasicShotUpgradeKind.HuntersCount: return "Keep shooting the same enemy: your basic shot marks it sooner. Takes the place of Marking Rounds.";
                case BasicShotUpgradeKind.PassItOn: return "When your basic shot kills a marked enemy, its marks jump to the nearest enemy beside it.";
                default: return "";
            }
        }

        /// <summary>What tier <paramref name="tier"/> does in numbers; tier 0, what she has without it.</summary>
        public static string What(BasicShotUpgradeKind k, int tier, BasicShotUpgradeValues v, int kitPerMark = 5)
        {
            v = v ?? new BasicShotUpgradeValues();
            switch (k)
            {
                case BasicShotUpgradeKind.ChainKill:
                    if (tier <= 0) return "no bounce";
                    return "a kill bounces " + Pct(BasicShotMoreRules.BounceShare(tier, v)) + " within " + Metres(BasicShotMoreRules.BounceReach(tier, v))
                           + ", up to " + BasicShotMoreRules.BounceMost(tier, v).ToString(s_Inv) + " times";
                case BasicShotUpgradeKind.SixthChamber:
                    if (tier <= 0) return "no heavy round";
                    return Nth(BasicShotMoreRules.ShotsPerHeavy(tier, v)) + " shot x" + BasicShotMoreRules.HeavyScale(tier, v).ToString("0.#", s_Inv) + ", through "
                           + Math.Max(0, v.HeavyPierce).ToString(s_Inv) + ", +" + v.HeavyStagger.ToString("0.#", s_Inv) + " stagger";
                case BasicShotUpgradeKind.HuntersCount:
                {
                    if (tier <= 0) return "a mark " + Nth(kitPerMark) + " hit on anyone";
                    int marks = BasicShotMoreRules.HuntMarks(tier, v);
                    return (marks == 1 ? "a mark " : marks.ToString(s_Inv) + " marks ") + Nth(BasicShotMoreRules.HuntHits(tier, v)) + " hit on one enemy";
                }
                case BasicShotUpgradeKind.PassItOn:
                    if (tier <= 0) return "marks die with the enemy";
                    return "marks pass within " + Metres(BasicShotMoreRules.PassReach(tier, v)) + (BasicShotUpgradeValues.At(v.PassOnAnyKill, tier) ? ", on any kill" : ", on a basic kill");
                default: return "";
            }
        }

        /// <summary>
        /// Her sheet's value for one she holds: the tier, and for Sixth Chamber its count (the draft: "Without a visible count it is a
        /// random big hit"): "I  3 / 6", the shots fired toward the heavy round; "I  HEAVY NEXT" when the next shot is it.
        /// </summary>
        public static string SheetCount(BasicShotUpgradeKind k, RunBasicShotUpgrades held)
        {
            if (k != BasicShotUpgradeKind.SixthChamber || held == null || held.TierNow(k) <= 0) return "";
            int rounds = BasicShotMoreRules.ShotsPerHeavy(held.TierNow(k), held.Values);
            if (BasicShotMoreRules.NextIsHeavy(held.ChamberFired, rounds)) return "  HEAVY NEXT";
            return "  " + held.ChamberFired.ToString(s_Inv) + " / " + rounds.ToString(s_Inv);
        }
    }
}
