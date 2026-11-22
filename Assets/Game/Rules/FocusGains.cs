using System;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// What spending focus makes her for its seconds (work order 071, Kristian, 2026-11-21: "videre, så må vi reworke focus,
    /// det føles ræva. glemmer å bruke den heller, fordi man ikke føler det. føler man må få mere attackdmg, attackspeed,
    /// cooldown osv."). Until then a spent gauge cut the running cooldowns and made basic shots mark for 6 s, and dealt no
    /// more (decision 0005): real, but nothing he felt in his hands. While the gains run (<see cref="FocusGauge.IsBoosted"/>):
    /// - damage: her basic shot and her skills deal <see cref="IdentityValues.EmpoweredDamageScale"/> times as much (1.35),
    ///   on the window's own multiplier (EmpoweredDamage, 1), read where every hit of hers is dealt;
    /// - attack speed: the basic shot fires <see cref="IdentityValues.EmpoweredFireRate"/> times as fast (1.4), on her attack
    ///   speed stat, the way that stat is carried (work order 050): only the recovery after the shot is cut, the startup (the
    ///   firing pose the Shoot clip lines up on) and the slow of a shot on the move stay; the recovery never under
    ///   <see cref="IdentityValues.EmpoweredLeastRecovery"/>;
    /// - cooldowns: every running cooldown, the dash's too, runs <see cref="IdentityValues.EmpoweredCooldownRate"/> times as
    ///   fast (1.6), beside the cut at the moment of spending, which stays (it is what is felt at once: the skills come
    ///   back as Z goes down); never so fast that a cooldown takes less than the run's floor of its own (work order 065's
    ///   rule, <see cref="RunValues.CooldownFloor"/>, 0.5);
    /// - she runs <see cref="IdentityValues.EmpoweredMoveShare"/> faster (+10 %), on her move speed stat.
    /// The gains multiply with her stats and the run's steps; none adds to another. Every one set to none (1, 1, 1, 0) is
    /// focus exactly as before the order (<see cref="IsNone"/>).
    /// </summary>
    public static class FocusGains
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Damage times this while the gains run; 1 otherwise. Never below 0.</summary>
        public static float DamageScale(bool boosted, IdentityValues v) => boosted && v != null ? Math.Max(0f, v.EmpoweredDamageScale) : 1f;

        /// <summary>The basic shot's rate times this while the gains run; 1 otherwise.</summary>
        public static float FireRate(bool boosted, IdentityValues v) => boosted && v != null ? Math.Max(0.05f, v.EmpoweredFireRate) : 1f;

        /// <summary>Her run speed times this while the gains run; 1 otherwise.</summary>
        public static float MoveScale(bool boosted, IdentityValues v) => boosted && v != null ? Math.Max(0.1f, 1f + v.EmpoweredMoveShare) : 1f;

        /// <summary>
        /// The basic shot's recovery at a rate <paramref name="rate"/> times the one it has: the time from shot to shot over the
        /// rate, the startup and the active part as they are, only the recovery taking the difference (as her attack speed
        /// stat does); never cut under <paramref name="least"/> (a recovery shorter than that already is left as it is).
        /// </summary>
        public static float Recovery(float startup, float active, float recovery, float rate, float least)
        {
            if (rate <= 0f || Math.Abs(rate - 1f) < 1e-6f) return recovery;
            float total = startup + active + recovery;
            float cut = total / rate - startup - active;
            return Math.Max(Math.Min(recovery, Math.Max(0f, least)), cut);
        }

        /// <summary>
        /// How many times as fast a cooldown runs while the gains run: the gains' rate, but never so fast that a cooldown
        /// started at <paramref name="share"/> of its own (her cuts to it, <see cref="HerStats.CooldownShare"/>) takes less
        /// than <paramref name="floor"/> of its own: at most share over floor. 1 outside the gains. A floor of 0: no cap.
        /// </summary>
        public static float CooldownRate(bool boosted, IdentityValues v, float share = 1f, float floor = 0f)
        {
            if (!boosted || v == null) return 1f;
            float rate = Math.Max(1f, v.EmpoweredCooldownRate);
            if (floor > 0f) rate = Math.Min(rate, Math.Max(1f, share / floor));
            return rate;
        }

        /// <summary>The seconds a cooldown running at <paramref name="rate"/> loses beyond plain time in <paramref name="seconds"/>.</summary>
        public static float ExtraCooldownSeconds(float seconds, float rate) => seconds > 0f && rate > 1f ? seconds * (rate - 1f) : 0f;

        /// <summary>True when no gain is set: focus is exactly what it was before work order 071.</summary>
        public static bool IsNone(IdentityValues v) =>
            v == null || (Math.Abs(v.EmpoweredDamageScale - 1f) < 1e-6f && Math.Abs(v.EmpoweredFireRate - 1f) < 1e-6f
                          && Math.Abs(v.EmpoweredCooldownRate - 1f) < 1e-6f && Math.Abs(v.EmpoweredMoveShare) < 1e-6f);

        public static bool Any(IdentityValues v) => !IsNone(v);

        /// <summary>A Hair Trigger part's seconds of gains (<see cref="FocusModeValues.PartBoostSeconds"/>), times the run's Long focus.</summary>
        public static float PartSeconds(FocusModeValues ways, float longFocus = 1f) =>
            Math.Max(0f, (ways ?? new FocusModeValues()).PartBoostSeconds) * Math.Max(0f, longFocus);

        // ---------------- In words ----------------

        static string Share(float x) => (x * 100f).ToString("0", Inv) + "%";

        /// <summary>The gains in one line of plain words, for the focus choice: "Powered up: +35% damage, 40% faster shots, ...".</summary>
        public static string Words(IdentityValues v)
        {
            v = v ?? new IdentityValues();
            var parts = new System.Collections.Generic.List<string>();
            if (Math.Abs(v.EmpoweredDamageScale - 1f) > 1e-6f) parts.Add((v.EmpoweredDamageScale >= 1f ? "+" : "") + Share(v.EmpoweredDamageScale - 1f) + " damage");
            if (Math.Abs(v.EmpoweredFireRate - 1f) > 1e-6f) parts.Add(Share(v.EmpoweredFireRate - 1f) + " faster shots");
            if (Math.Abs(v.EmpoweredCooldownRate - 1f) > 1e-6f) parts.Add("cooldowns " + Share(v.EmpoweredCooldownRate - 1f) + " faster");
            if (Math.Abs(v.EmpoweredMoveShare) > 1e-6f) parts.Add(Share(v.EmpoweredMoveShare) + " faster run");
            return parts.Count == 0 ? "" : "Powered up: " + string.Join(", ", parts) + ".";
        }
    }
}
