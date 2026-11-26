using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The four basic-shot upgrades of work order 141 in play (Kristian, 2026-11-26, his tick of all four: Chain Kill, Pass It On,
    /// Sixth Chamber, Hunter's Count; the draft kit-2026-11-26's A3, C2, D2, D3), as part of <see cref="PlayerCharacter"/>, beside
    /// work order 138's five (PlayerCharacterBasicShotUpgrades.cs) and read where the basic shot resolves
    /// (<see cref="ResolveBasicShot"/>) and where every hit of hers is dealt (<see cref="Deal"/>, for Pass It On). Each works only
    /// while it is held and its own switch is on; held by none, the shot is as before, field for field.
    ///
    /// Nothing here aims for her or moves her aim. Chain Kill's bounces and Pass It On's marks go, only after her own shot has
    /// killed, from the enemy that died to the nearest living enemy near it with a clear line past cover (the Ricochet skill's
    /// rule, <see cref="NewSkillRules.NextJump"/>): his tick of the two is his yes to that.
    ///
    /// How they meet each other, the five and hold to fire (the order asked for each pair):
    /// - Chain Kill: one chain a shot, from the first enemy the shot killed (the one it hit, else the first it pierced that died);
    ///   a bounce is a basic hit: the shot's damage as it left the gun (its heat, a heavy round's weight) times the bounce's share,
    ///   a crit exactly when the shot's first hit was one, Marked prey, Tap Out on the enemy it hits, and it counts toward the next
    ///   basic mark (the kit's fifth, Marking Rounds', or the enemy's own with Hunter's Count, on the enemy it hits); it adds no
    ///   Hot Barrel step and no Quickening (once a shot), and never goes to an enemy the shot hit already;
    /// - Sixth Chamber: the count is of basic shots fired, hit or miss (a held stream's shots one by one, as clicks); its pierce is
    ///   Piercing Round's shared, never added: through the more enemies and at the larger share of the two; it is one basic hit for
    ///   the marks; its damage is the shot's (heat in it) times its scale, before Tap Out and the crit; a heavy round that kills
    ///   starts a chain with its weight;
    /// - Hunter's Count: in place of the kit's count and Marking Rounds' (held together only by a test: Hunter's Count wins); a
    ///   pierced enemy adds nothing to its count (the shot's marks count its first enemy only, as 138's), a bounce adds to the
    ///   count of the enemy it hits; in the focus window every basic hit marks once and the counts stand;
    /// - Pass It On: on every kill of a marked enemy by her basic shot, a pierced one's and a bounce's too, and at its last tier
    ///   by any of her hits; marks a spender or Tap Out took are spent, not passed; the passed marks are not counted again as
    ///   added nor as lost to the death in the log (its own basic_shot_use line counts them).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        readonly List<Damageable> _chainHit = new List<Damageable>(8);
        readonly List<TargetInfo> _chainTargets = new List<TargetInfo>();
        readonly List<int> _chainSkip = new List<int>(8);
        Damageable _passTo;
        bool _heavyRound;

        /// <summary>For tests and the log: bounces Chain Kill made, heavy rounds fired, marks Hunter's Count made, marks Pass It On passed.</summary>
        public int ChainBounces { get; private set; }
        public int HeavyRounds { get; private set; }
        public int HunterMarks { get; private set; }
        public int MarksPassedOn { get; private set; }
        /// <summary>True when the last basic shot fired was Sixth Chamber's heavy round.</summary>
        public bool LastShotHeavy { get; private set; }

        /// <summary>Sixth Chamber: every how many basic shots the heavy round comes now (0 without it), the shots fired since the last one, and whether the next is it.</summary>
        public int ChamberRounds => BasicShotMoreRules.ShotsPerHeavy(ShotTier(BasicShotUpgradeKind.SixthChamber), ShotValues);
        public int ChamberFired => Modifiers != null && ChamberRounds > 0 ? Modifiers.BasicShot.ChamberFired : 0;
        public bool ChamberNextHeavy => BasicShotMoreRules.NextIsHeavy(ChamberFired, ChamberRounds);

        /// <summary>A run's start or a reset (with 138's): no heavy round waiting from before, no pass under way.</summary>
        void ResetMoreShotUpgrades()
        {
            _heavyRound = false;
            _passTo = null;
            _chainHit.Clear();
            if (Modifiers != null) Modifiers.BasicShot.ChamberFired = 0;
        }

        // ---------------- Sixth Chamber ----------------

        /// <summary>A basic shot fired, hit or miss: counts it, and says whether it is the heavy round. False without Sixth Chamber.</summary>
        bool ChamberTurn()
        {
            int rounds = ChamberRounds;
            _heavyRound = rounds > 0 && BasicShotMoreRules.ChamberShot(ref Modifiers.BasicShot.ChamberFired, rounds);
            LastShotHeavy = _heavyRound;
            if (_heavyRound) HeavyRounds++;
            return _heavyRound;
        }

        /// <summary>The heavy round's damage (the shot's, its heat in it, times the tier's scale); the shot's own for any other.</summary>
        int HeavyDamage(int damage)
        {
            if (!_heavyRound || damage <= 0) return damage;
            int heavy = BasicShotMoreRules.HeavyDamage(damage, BasicShotMoreRules.HeavyScale(ShotTier(BasicShotUpgradeKind.SixthChamber), ShotValues));
            ShotTally.Note(BasicShotUpgradeKind.SixthChamber, heavy - damage);
            return heavy;
        }

        /// <summary>The stagger the heavy round adds to the shot's own; 0 for any other shot.</summary>
        float HeavyStagger => _heavyRound ? Mathf.Max(0f, ShotValues.HeavyStagger) : 0f;

        /// <summary>The heavy round's muzzle flash: the shot's own look, larger by its share (Hot Barrel's warmth stays on it).</summary>
        Look HeavyFlash(Look look)
        {
            if (_heavyRound) look.Width *= 1f + Mathf.Max(0f, ShotValues.HeavyFlashGrow);
            return look;
        }

        /// <summary>The heavy round's streak, this many times as wide; 1 for any other shot.</summary>
        float HeavyStreak => _heavyRound ? Mathf.Max(1f, ShotValues.HeavyStreakScale) : 1f;

        /// <summary>How many enemies past the first the shot goes on to, and at what share: Piercing Round's, the heavy round's shared with it.</summary>
        int PierceMostNow => BasicShotMoreRules.PierceTargets(_heavyRound, ShotTier(BasicShotUpgradeKind.PiercingRound), ShotValues);
        float PierceShareNow => BasicShotMoreRules.PierceShare(_heavyRound, ShotTier(BasicShotUpgradeKind.PiercingRound), ShotValues);

        // ---------------- Hunter's Count ----------------

        /// <summary>
        /// Hunter's Count on a basic hit of <paramref name="target"/>: the marks it adds by the enemy's own count, or -1 when it is not
        /// held (the kit's count, or Marking Rounds', decides then). What it added beside the kit's own count is tallied.
        /// </summary>
        int HunterHitOn(Damageable target, bool empowered)
        {
            int tier = ShotTier(BasicShotUpgradeKind.HuntersCount);
            if (tier <= 0 || target == null) return -1;
            int count = target.HunterHits;
            int marks = BasicShotMoreRules.HunterHit(ref count, tier, empowered, ShotValues);
            target.HunterHits = count;
            if (empowered) return marks;
            bool own = _ownMarks.Hit(Kit.BasicShotsPerMark, false);
            if (marks > 0)
            {
                HunterMarks += marks;
                ShotTally.Note(BasicShotUpgradeKind.HuntersCount, 0f);
            }
            ShotTally.NoteAdded(BasicShotUpgradeKind.HuntersCount, marks - (own ? 1f : 0f));
            return marks;
        }

        // ---------------- Chain Kill ----------------

        /// <summary>
        /// After the shot's hits: if it killed, Chain Kill's bounces, each from the enemy that died to the nearest living enemy within
        /// the tier's reach past cover, never one the shot hit; a bounce that kills goes on, at most the tier's bounces.
        /// <paramref name="shotDamage"/>: the shot's damage as it left the gun (heat and a heavy round's weight in it).
        /// </summary>
        void ChainKill(Look look, Damageable first, int shotDamage, float stagger, bool crit, bool empowered)
        {
            int tier = ShotTier(BasicShotUpgradeKind.ChainKill);
            if (tier <= 0 || first == null) return;
            BasicShotUpgradeValues v = ShotValues;
            IReadOnlyList<Damageable> all = Damageable.All;
            // The enemies the shot hit, in line order: the first, then those it pierced.
            _chainHit.Clear();
            _chainHit.Add(first);
            for (int p = 0; p < _pierced.Count; p++)
                if (_pierced[p] >= 0 && _pierced[p] < all.Count) _chainHit.Add(all[_pierced[p]]);
            Damageable from = null;
            for (int i = 0; i < _chainHit.Count && from == null; i++)
                if (_chainHit[i] != null && !_chainHit[i].IsAlive) from = _chainHit[i];
            if (from == null) return;

            int damage = BasicShotMoreRules.BounceDamage(shotDamage, BasicShotMoreRules.BounceShare(tier, v));
            float reach = BasicShotMoreRules.BounceReach(tier, v);
            int most = BasicShotMoreRules.BounceMost(tier, v);
            CoverLine cover = CoverQuery;
            float height = ShotHeight;
            for (int b = 0; b < most && damage > 0; b++)
            {
                Damageable d = NearestPast(from, reach, cover, _chainHit);
                if (d == null) return;
                _chainHit.Add(d);
                // The bounce: a thin streak from the one that died to the one it hits, at the shot's height, and Ricochet's ping.
                Vector3 at = new Vector3(from.transform.position.x, height, from.transform.position.z);
                Vector3 to = new Vector3(d.transform.position.x, height, d.transform.position.z);
                SpawnStreak(look, at, to, RicochetStreakScale);
                PlaceholderSounds.Current?.AskFor(SoundMoment.RicochetJumped);
                bool marked = d.MarkCount > 0;
                int dealt = TapOutOn(d, damage, out int taken);
                int applied = Deal(d, dealt, out float focus, stagger, sureCrit: crit, marked: marked, basicShot: true, noCritRoll: true);
                ReportTapOut(d, taken, applied);
                if (applied <= 0) return;
                ChainBounces++;
                ShotTally.Note(BasicShotUpgradeKind.ChainKill, applied);
                ReportHit(-1, d, applied, empowered, focus);
                SpawnHit(look, d, at);
                int marks = BasicHitMarks(empowered, d); // a bounce counts as a basic hit toward the next mark
                if (marks > 0) AddMarks(d, marks, -1);
                if (d.IsAlive) return; // a bounce that kills bounces again
                from = d;
            }
        }

        /// <summary>
        /// The nearest living enemy within <paramref name="reach"/> of <paramref name="from"/> with a clear line past cover
        /// (<see cref="NewSkillRules.NextJump"/>), never one of <paramref name="skip"/>; null for none. Nothing is allocated once
        /// the scratch lists have grown.
        /// </summary>
        Damageable NearestPast(Damageable from, float reach, CoverLine cover, List<Damageable> skip)
        {
            IReadOnlyList<Damageable> all = Damageable.All;
            _chainTargets.Clear();
            _chainSkip.Clear();
            int at = -1;
            for (int i = 0; i < all.Count; i++)
            {
                _chainTargets.Add(all[i].Info);
                if (all[i] == from) at = i;
                else if (skip != null && skip.Contains(all[i])) _chainSkip.Add(i);
            }
            if (at < 0)
            {
                // The one that died is gone from the list already: it is where it stood, dead.
                at = _chainTargets.Count;
                _chainTargets.Add(new TargetInfo(from.GroundPosition, from.Radius, false));
            }
            int next = NewSkillRules.NextJump(_chainTargets, at, _chainSkip, reach, cover);
            return next >= 0 && next < all.Count ? all[next] : null;
        }

        // ---------------- Pass It On ----------------

        /// <summary>
        /// Before one of her hits (<see cref="Deal"/>): when Pass It On would pass this target's marks if the hit kills it (a basic
        /// hit, or any of hers at its last tier), and a living enemy is near enough to take them, the target is told to keep them on
        /// its death for her (<see cref="Damageable.PassMarksOnDeath"/>) instead of losing them.
        /// </summary>
        void PassItOnBefore(Damageable target, bool basicShot)
        {
            _passTo = null;
            int tier = ShotTier(BasicShotUpgradeKind.PassItOn);
            if (target == null || !BasicShotMoreRules.PassesOn(tier, basicShot, ShotValues) || !target.IsAlive || target.MarkCount <= 0) return;
            _passTo = NearestPast(target, BasicShotMoreRules.PassReach(tier, ShotValues), CoverQuery, null);
            if (_passTo != null) target.PassMarksOnDeath = true;
        }

        /// <summary>After the hit: if it killed, the marks it kept go to the one found before, up to the cap, their lifetime renewed, along a thin thread.</summary>
        void PassItOnAfter(Damageable target)
        {
            if (target == null) return;
            target.PassMarksOnDeath = false;
            Damageable to = _passTo;
            _passTo = null;
            int marks = target.TakePassedMarks();
            if (to == null || marks <= 0 || !to.IsAlive) return;
            KitValues kit = Kit;
            int before = to.MarkCount;
            int after = to.AddMarks(marks, kit.MaxMarks, kit.MarkLifetime + (Modifiers != null ? Modifiers.MarkLifetimeBonus : 0f));
            MarkPips pips = MarkPips.For(to, ShotFxMaterial);
            if (pips != null) pips.Max = kit.MaxMarks;
            int landed = Mathf.Max(0, after - before);
            MarksPassedOn += landed;
            ShotTally.Note(BasicShotUpgradeKind.PassItOn, landed);
            // The thread (the draft: "Marks seem to move on their own, so it needs a visible thread"): the shot's own thin streak.
            float height = ShotHeight;
            SpawnStreak(LookFor(null, 0f), new Vector3(target.transform.position.x, height, target.transform.position.z),
                        new Vector3(to.transform.position.x, height, to.transform.position.z), RicochetStreakScale);
        }
    }
}
