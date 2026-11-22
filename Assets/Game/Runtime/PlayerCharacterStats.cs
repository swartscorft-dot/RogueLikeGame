using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// Her stats in a run, the crits and the damage dealt, of <see cref="PlayerCharacter"/> (work order 081 moved them here from
    /// PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- Her stats in a run (work order 050, decision 0012) ----------------

        /// <summary>The random stream crits come from: seeded by the run's seed when a run starts, and by tests (<see cref="SeedCrits"/>).</summary>
        LevelRandom _crits = LevelRandom.Stream(0UL, CritStage);
        bool _dealtCrit;
        public const string CritStage = "crit";

        /// <summary>The crits' stream starts again from this seed (a run's start, and tests).</summary>
        public void SeedCrits(ulong seed) => _crits = LevelRandom.Stream(seed, CritStage);
        /// <summary>Hits of hers that were crits, rolled or sure (a clean press, a perfect release).</summary>
        public int Crits { get; private set; }

        /// <summary>Her stats in the run (<see cref="HerStats"/>), or null outside a run.</summary>
        public HerStats Stats => Modifiers != null ? Modifiers.Stats : null;
        /// <summary>True in a run with the stats and tiers on: flat damage, attack speed and crits are read.</summary>
        bool StatsOn => Modifiers != null && Modifiers.StatsAndTiers;
        /// <summary>What her flat damage adds to a hit of this skill (null: the basic shot); 0 outside a run or with the switch off.</summary>
        int FlatFor(SkillDefinition skill) => StatsOn ? Modifiers.Stats.FlatFor(skill) : 0;
        /// <summary>Work order 065: her Clean press, Marked prey and Long focus (<see cref="HerStats"/>); 1 outside a run or with the switch off.</summary>
        float CleanPressNow => StatsOn ? Modifiers.Stats.CleanPress : 1f;
        float MarkedPreyNow => StatsOn ? Modifiers.Stats.MarkedPrey : 1f;
        float LongFocusNow => StatsOn ? Modifiers.Stats.LongFocus : 1f;
        /// <summary>
        /// Work order 067: her Wide window and Blast (<see cref="HerStats"/>); 0 outside a run or with the switch off. Wide window
        /// widens the clean part (<see cref="ChainCleanPart"/>) and, with Blast, lies on her skills as she has them (<see cref="Skills"/>).
        /// </summary>
        public float WideWindowNow => StatsOn ? Modifiers.Stats.WideWindow : 0f;
        public float BlastNow => StatsOn ? Modifiers.Stats.Blast : 0f;
        SkillStatLayer SkillLayerNow => StatsOn ? Modifiers.Stats.SkillLayer : default;
        /// <summary>
        /// The basic shot's recovery now: the feel's, or with the stats on shortened or lengthened by her attack speed. Work
        /// order 060: her pistols' parts first (a barrel's longer recovery), and her attack speed works on that as on the feel's.
        /// </summary>
        float BasicShotRecoveryNow(FeelValues f) =>
            FocusGains.Recovery(f.BasicShotStartup, f.BasicShotActive, StatsRecovery(f), FocusFireRate, Identity.EmpoweredLeastRecovery); // work order 071: the gains' faster fire
        /// <summary>The basic shot's recovery with her stats, before the gains of spent focus (work order 071): as before the order.</summary>
        float StatsRecovery(FeelValues f) =>
            Modifiers != null ? Modifiers.Stats.BasicShotRecovery(f.BasicShotStartup, f.BasicShotActive, WeaponRecovery(f)) : f.BasicShotRecovery;
        /// <summary>The basic shot's recovery as her pistols' parts make it, before her attack speed (work order 060): the feel's with none.</summary>
        float WeaponRecovery(FeelValues f) =>
            Modifiers != null ? Modifiers.Weapon.Recovery(f.BasicShotStartup, f.BasicShotActive, f.BasicShotRecovery) : f.BasicShotRecovery;
        /// <summary>
        /// The time from shot to shot before her attack speed (work order 060): the feel's 0.225 s, or her pistols' (a Bull
        /// Barrel's 35 % longer). The base the pause menu's stats and the recorder's stats line measure her rate against.
        /// </summary>
        public float BasicShotBaseInterval
        {
            get
            {
                FeelValues f = Feel;
                return f.BasicShotStartup + f.BasicShotActive + WeaponRecovery(f);
            }
        }
        /// <summary>The time from one basic shot to the next now (work order 050): the feel's 0.225 s, or with the stats on her attack speed's.</summary>
        public float BasicShotInterval
        {
            get
            {
                FeelValues f = Feel;
                return f.BasicShotStartup + f.BasicShotActive + BasicShotRecoveryNow(f);
            }
        }

        /// <summary>
        /// Deals damage to a target: times the empowered multiplier while empowered (1 since decision 0005) and, since work
        /// order 071, the gains' while they run (<see cref="FocusGauge.DamageMultiplier"/>, 1.35), and what
        /// was dealt trickles into the focus gauge. Returns the amount actually removed, and the focus gained.
        /// <paramref name="stagger"/> is what the hit takes off a stagger bar (work order 008); the basic shot's is 0.
        /// Work order 050: in a run with the stats on, the hit may crit (her crit chance, on the crits' stream), and one
        /// that is <paramref name="sureCrit"/> (a clean press, a perfect release) always does without a roll.
        /// </summary>
        int Deal(Damageable target, int damage, out float focus, float stagger = 0f, bool sureCrit = false, bool foldedCrit = false, bool marked = false)
        {
            IdentityValues identity = Identity;
            _dealtCrit = false;
            // Work order 065: her Marked prey on a target that carried marks when the hit came (1 off).
            if (marked && damage > 0 && StatsOn) damage = Mathf.RoundToInt(damage * MarkedPreyNow);
            if (StatsOn && damage > 0 && (sureCrit || CritRules.Rolls(_crits, Modifiers.Stats.CritChance)))
            {
                _dealtCrit = true;
                Crits++;
                // A clean spend's crit is folded (work order 055): at her start it deals what it dealt before the stats.
                damage = foldedCrit && sureCrit ? CritRules.ApplyFolded(damage, Modifiers.Stats.CritDamage, Modifiers.StatValues)
                                                : CritRules.Apply(damage, Modifiers.Stats.CritDamage);
            }
            if (Modifiers != null) damage = Modifiers.ScaleDamage(damage); // a run's upgrades (work order 012)
            damage = target.ScaleForTag(damage); // Tag Round's tag (work order 015)
            bool tagged = target.IsTagged;
            if (Modifiers != null) stagger *= Modifiers.StaggerShare; // the run's Heavy hits (work order 025)
            int applied = target.TakeHit(_focus.Scale(damage, Time.time, identity), stagger);
            // Work order 064: a crit's flash on the enemy is a hard white frame, with the shots' weight running.
            if (_dealtCrit && applied > 0 && FiringWeight.Running != null && target.TryGetComponent(out HitFlash flash)) flash.Crit();
            focus = applied > 0 ? GainFocus(applied * identity.FocusPerDamage) : 0f; // through the run's Keen focus (work order 025)
            if (tagged && !target.IsAlive) SpreadTag(target); // work order 017
            return applied;
        }
    }
}
