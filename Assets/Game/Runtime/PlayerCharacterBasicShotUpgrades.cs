using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The five basic-shot upgrades in play (work order 138, part 3; Kristian, 2026-11-25: "muligheter for å oppgradere
    /// autoattacks, med pierce og andre ting som feks on-hit, build up, oftere stacks osv"; his tick "Fem, én fra hver
    /// retning"), as part of <see cref="PlayerCharacter"/>: the run's <see cref="RunModifiers.BasicShot"/> read where the basic
    /// shot resolves (<see cref="ResolveBasicShot"/>). Each works only while it is held and its own switch is on; held by none,
    /// the shot is as before, field for field. Nothing here aims for her or picks a target: the pierced enemies are those on
    /// the line she shot, past the one it hit; every other rule works on the targets her own shot hit.
    ///
    /// How they meet the rest (the order asked for one line each):
    /// - crits: one roll for the whole shot; a pierced enemy takes a crit exactly when the first did (no roll of its own);
    /// - marks: the count to the next basic mark (5, or Marking Rounds') counts the first enemy only, once a shot; in the focus
    ///   window, where every basic hit marks, every enemy the shot hits is marked, pierced ones too;
    /// - Hot Barrel and a pierce: the shot's heat is on all it hits, and the shot adds one step, once; a skill's shot and the dash
    ///   neither add nor drop it; a basic shot that hits nothing drops it, as does <see cref="BasicShotUpgradeValues.HeatHoldSeconds"/>
    ///   with no basic hit;
    /// - Tap Out: on every enemy the shot hits at its threshold, the first and a pierced one alike; a hit that spends still counts
    ///   toward the next basic mark, so a fresh mark may land after the spend;
    /// - Quickening: once a shot that hits, not once an enemy; never the dash; never a recharge under the run's cooldown floor;
    /// - the focus window: all five work in it as outside, beside its gains (its cooldown rate, its marks);
    /// - hold to fire (work order 132, on another branch): every rule here is a shot's, so a held trigger's shots are judged one by
    ///   one as clicks are; nothing here reads the click.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>Her muzzle flash's rim at full heat (Hot Barrel): a hotter orange than the shot's own, reached step by step.</summary>
        static readonly Color s_HotRim = new Color(1f, 0.42f, 0.12f, 1f);

        readonly HotBarrelHeat _heat = new HotBarrelHeat();
        readonly BasicShotMarks _ownMarks = new BasicShotMarks(); // the kit's own count beside Marking Rounds', for what it added
        readonly List<int> _pierced = new List<int>(4);
        readonly List<float> _quickCut = new List<float>();
        readonly List<float> _quickReadyAt = new List<float>();
        static readonly BasicShotUpgradeValues s_ShotValues = new BasicShotUpgradeValues();

        /// <summary>What each of the five did since the log last read it (work order 138).</summary>
        public BasicShotUpgradeTally ShotTally { get; } = new BasicShotUpgradeTally();

        /// <summary>For tests and the log: enemies a basic shot pierced to, Tap Outs, and the seconds Quickening took off.</summary>
        public int PiercedHits { get; private set; }
        public int TapOuts { get; private set; }
        public float QuickenedSeconds { get; private set; }
        /// <summary>The last basic shot's muzzle warmth, 0 to 1 (Hot Barrel's step on her pistols).</summary>
        public float LastFlashWarmth { get; private set; }

        BasicShotUpgradeValues ShotValues => Modifiers != null && Modifiers.BasicShot.Values != null ? Modifiers.BasicShot.Values : s_ShotValues;

        /// <summary>The tier of a basic-shot upgrade that works now: held in the run with its switch on; 0 otherwise.</summary>
        public int ShotTier(BasicShotUpgradeKind kind) => Modifiers != null ? Modifiers.BasicShot.TierNow(kind) : 0;

        /// <summary>Hot Barrel's share on the next basic hit now, and its hits in a row (0 without it).</summary>
        public float HeatShare => _heat.Share(Time.time, ShotTier(BasicShotUpgradeKind.HotBarrel), ShotValues);
        public int HeatHits => ShotTier(BasicShotUpgradeKind.HotBarrel) > 0 ? _heat.Hits(Time.time, ShotValues) : 0;

        /// <summary>Every how many basic hits the basic shot marks now: Marking Rounds', or the kit's own.</summary>
        public int BasicHitsPerMark => BasicShotRules.HitsPerMark(ShotTier(BasicShotUpgradeKind.MarkingRounds), Kit.BasicShotsPerMark, ShotValues);

        /// <summary>A run's start or a reset: no heat, no count beside the kit's, no recharge tracked.</summary>
        void ResetShotUpgrades()
        {
            _heat.Reset();
            _ownMarks.Reset();
            _quickCut.Clear();
            _quickReadyAt.Clear();
        }

        // ---------------- Piercing Round ----------------

        /// <summary>
        /// The enemies past <paramref name="first"/> that the shot goes on to along her line (into <see cref="_pierced"/>), and
        /// the last enemy the shot reaches: the first itself when none. Reads the targets <see cref="BasicShotEnd"/> gathered.
        /// </summary>
        Damageable PiercedOnTheLine(Damageable first, GroundPoint origin, GroundPoint aim, float range, float halfWidth, float stop)
        {
            _pierced.Clear();
            int tier = ShotTier(BasicShotUpgradeKind.PiercingRound);
            if (first == null || tier <= 0) return first;
            IReadOnlyList<Damageable> all = Damageable.All;
            int at = -1;
            for (int i = 0; i < all.Count && i < _targetScratch.Count; i++)
                if (all[i] == first) at = i;
            if (at < 0) return first;
            BasicShotRules.PiercedTargets(origin, aim, range, halfWidth, _targetScratch, stop, at, BasicShotRules.PierceTargets(tier, ShotValues), _pierced);
            return _pierced.Count > 0 ? all[_pierced[_pierced.Count - 1]] : first;
        }

        /// <summary>The pierced enemies take their share of the shot's damage (<paramref name="shotDamage"/>, its heat in it), crit as the first did.</summary>
        void HitPierced(Look look, Vector3 muzzle, int shotDamage, float stagger, bool crit, bool empowered)
        {
            if (_pierced.Count == 0) return;
            float share = BasicShotRules.PierceShare(ShotTier(BasicShotUpgradeKind.PiercingRound), ShotValues);
            IReadOnlyList<Damageable> all = Damageable.All;
            for (int p = 0; p < _pierced.Count; p++)
            {
                int index = _pierced[p];
                if (index < 0 || index >= all.Count) continue;
                Damageable d = all[index];
                if (d == null || !d.IsAlive) continue;
                bool marked = d.MarkCount > 0;
                int damage = TapOutOn(d, BasicShotRules.PierceDamage(shotDamage, share), out int taken);
                int applied = Deal(d, damage, out float focus, stagger * share, sureCrit: crit, marked: marked, basicShot: true, noCritRoll: true);
                ReportTapOut(d, taken, applied);
                if (applied <= 0) continue;
                PiercedHits++;
                ShotTally.Note(BasicShotUpgradeKind.PiercingRound, applied);
                ReportHit(-1, d, applied, empowered, focus);
                SpawnHit(look, d, muzzle);
                if (empowered) AddMarks(d, 1, -1); // the focus window marks every basic hit: a pierced enemy too
            }
        }

        // ---------------- Quickening ----------------

        /// <summary>A basic hit: Quickening takes its seconds off every running skill cooldown, never a recharge under the run's floor.</summary>
        void Quicken()
        {
            int tier = ShotTier(BasicShotUpgradeKind.Quickening);
            if (tier <= 0) return;
            float seconds = BasicShotRules.QuickeningSeconds(tier, ShotValues);
            float now = Time.time, floor = Modifiers.CooldownFloor, total = 0f;
            IReadOnlyList<SkillDefinition> skills = Skills;
            while (_quickCut.Count < skills.Count)
            {
                _quickCut.Add(0f);
                _quickReadyAt.Add(float.NegativeInfinity);
            }
            for (int slot = 0; slot < skills.Count; slot++)
            {
                if (skills[slot] == null || !IsEquipped(slot)) continue;
                Cooldown cooldown = CooldownFor(slot);
                float left = cooldown.NextCharge(now);
                if (left <= 0f)
                {
                    _quickCut[slot] = 0f;
                    continue;
                }
                // A recharge later than where its last cut left it is a new one (a new use, or the next charge): its cut starts again.
                if (now + left > _quickReadyAt[slot] + 1e-3f) _quickCut[slot] = 0f;
                float cut = cooldown.CutSeconds(now, Mathf.Min(seconds, BasicShotRules.QuickeningRoom(CooldownOf(slot), floor, _quickCut[slot])));
                _quickCut[slot] += cut;
                float after = cooldown.NextCharge(now);
                _quickReadyAt[slot] = after > 0f ? now + after : float.NegativeInfinity;
                total += cut;
            }
            if (total <= 0f) return;
            QuickenedSeconds += total;
            ShotTally.Note(BasicShotUpgradeKind.Quickening, total);
        }

        // ---------------- Hot Barrel ----------------

        /// <summary>The shot's damage with Hot Barrel's heat on it, and its warmth on her muzzle flash's look.</summary>
        int HeatedDamage(int damage)
        {
            int tier = ShotTier(BasicShotUpgradeKind.HotBarrel);
            float share = _heat.Share(Time.time, tier, ShotValues);
            if (tier <= 0 || share <= 0f || damage <= 0) return damage;
            int heated = Mathf.RoundToInt(damage * (1f + share));
            ShotTally.Note(BasicShotUpgradeKind.HotBarrel, heated - damage);
            return heated;
        }

        /// <summary>
        /// The muzzle flash of a basic shot warmed by Hot Barrel's heat (the order: "it shows on her pistols only (the muzzle flash
        /// warming step by step, through work order 064's muzzle light): nothing at the sight, no number on screen"): larger and
        /// hotter in its rim by the heat's share of its most, and the muzzle light brighter with it. Without heat, the look as it is.
        /// </summary>
        Look WarmFlash(Look look)
        {
            int tier = ShotTier(BasicShotUpgradeKind.HotBarrel);
            float warmth = _heat.Warmth(Time.time, tier, ShotValues);
            LastFlashWarmth = warmth;
            BasicShotUpgradeValues v = ShotValues;
            if (FiringWeight.Running != null) FiringWeight.Running.NextWarmth = warmth * Mathf.Max(0f, v.LightGrowAtMost);
            if (warmth <= 0f) return look;
            look.Width *= 1f + warmth * Mathf.Max(0f, v.FlashGrowAtMost);
            look.Paint.Rim = Color.Lerp(look.Paint.Rim, s_HotRim, warmth);
            look.Paint.RimGlow *= 1f + warmth;
            return look;
        }

        void HeatHit()
        {
            if (ShotTier(BasicShotUpgradeKind.HotBarrel) > 0) _heat.Hit(Time.time, ShotValues);
        }

        void HeatMissed()
        {
            if (ShotTier(BasicShotUpgradeKind.HotBarrel) > 0) _heat.Miss(Time.time, ShotValues);
        }

        // ---------------- Marking Rounds ----------------

        /// <summary>A basic hit's mark by the count (Marking Rounds' or the kit's own); what Marking Rounds added beside the kit's own count is tallied.</summary>
        bool BasicHitMarks(bool empowered)
        {
            bool marks = _basicMarks.Hit(BasicHitsPerMark, empowered);
            if (ShotTier(BasicShotUpgradeKind.MarkingRounds) <= 0 || empowered) return marks;
            bool own = _ownMarks.Hit(Kit.BasicShotsPerMark, false);
            if (marks) ShotTally.Note(BasicShotUpgradeKind.MarkingRounds, 0f);
            ShotTally.NoteAdded(BasicShotUpgradeKind.MarkingRounds, (marks ? 1f : 0f) - (own ? 1f : 0f));
            return marks;
        }

        // ---------------- Tap Out ----------------

        /// <summary>
        /// Tap Out on a target the basic shot hits: at its threshold it takes every mark and the hit deals the spender's bonus
        /// a mark (<see cref="MarkRules.SpenderDamage"/>); <paramref name="taken"/> the marks taken, 0 when none.
        /// </summary>
        int TapOutOn(Damageable target, int damage, out int taken)
        {
            taken = 0;
            int tier = ShotTier(BasicShotUpgradeKind.TapOut);
            if (tier <= 0 || damage <= 0 || target == null || !BasicShotRules.TapsOut(target.MarkCount, tier, Kit.MaxMarks, ShotValues)) return damage;
            taken = target.TakeMarks();
            int dealt = MarkRules.SpenderDamage(damage, taken, BasicShotRules.TapOutBonus(tier, ShotValues));
            TapOuts++;
            ShotTally.Note(BasicShotUpgradeKind.TapOut, dealt - damage);
            return dealt;
        }

        /// <summary>The marks a Tap Out took: focus a mark as a spender's, and the report the log reads (marks spent by the basic shot).</summary>
        void ReportTapOut(Damageable target, int taken, int applied)
        {
            if (taken <= 0) return;
            float markFocus = GainFocus(taken * Identity.FocusPerMarkSpent);
            Report(new PlayerReport { Kind = PlayerReportKind.MarksSpent, Slot = -1, Target = target, Marks = taken, Damage = applied, Focus = markFocus });
        }
    }
}
