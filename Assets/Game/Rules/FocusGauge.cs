using System;

namespace Roguelike.Rules
{
    /// <summary>The numbers of the identity gauge. They live in the skill settings asset.</summary>
    [Serializable]
    public sealed class IdentityValues
    {
        public float MaxFocus = 100f;          // the gauge is full at this much focus
        public float FocusPerDamage = 0.005f;  // focus gained per point of damage dealt: a trickle (decision 0005)
        public float EmpoweredDuration = 6f;   // s the empowered mode lasts: the window in which every basic shot hit marks
        public float EmpoweredDamage = 1f;     // damage is multiplied by this while empowered (decision 0005: none extra)
        public float FocusPerMarkSpent = 5f;   // focus gained per mark a spender takes
        public float FocusPerCleanTap = 5f;    // focus gained per clean tap
        public float CooldownCut = 0.4f;       // spending a full gauge cuts every running cooldown by this share of what is left
    }

    /// <summary>
    /// The identity gauge of the first class: focus. Decision 0005 (2026-11-19): spent marks and clean taps fill it,
    /// and damage a little. When it is full it can be spent on the empowered mode: every running cooldown is cut, and
    /// for a while every basic shot hit marks (the cut and the marks are the character's; this is the gauge).
    /// No focus is gained while empowered. Time is passed in, in seconds.
    /// </summary>
    public sealed class FocusGauge
    {
        float _focus;
        float _empoweredUntil = float.NegativeInfinity;
        float _windowSeconds; // how long the window running now was opened for (work order 025: Steady Hand's is longer)

        public float Focus => _focus;

        public bool IsEmpowered(float now) => now < _empoweredUntil;
        public float EmpoweredRemaining(float now) => now < _empoweredUntil ? _empoweredUntil - now : 0f;
        public bool IsFull(IdentityValues values) => _focus >= values.MaxFocus;

        /// <summary>Focus for damage dealt. Nothing while empowered, and never more than a full gauge. Returns the focus gained.</summary>
        public float AddDamage(int damage, IdentityValues values, float now) =>
            damage <= 0 ? 0f : Add(damage * values.FocusPerDamage, values, now);

        /// <summary>Focus from any source. Nothing while empowered, and never more than a full gauge. Returns the focus gained.</summary>
        public float Add(float amount, IdentityValues values, float now)
        {
            if (amount <= 0f || IsEmpowered(now)) return 0f;
            float before = _focus;
            _focus = Math.Min(values.MaxFocus, _focus + amount);
            return _focus - before;
        }

        /// <summary>Spends a full gauge on the empowered mode. False, and nothing happens, if it is not full or already running.</summary>
        public bool TryActivate(float now, IdentityValues values) => TryActivate(now, values, values.EmpoweredDuration);

        /// <summary>
        /// Spends a full gauge on a window of <paramref name="seconds"/> (work order 025: Steady Hand's is longer than
        /// today's). False, and nothing happens, if it is not full or already running.
        /// </summary>
        public bool TryActivate(float now, IdentityValues values, float seconds)
        {
            if (IsEmpowered(now) || !IsFull(values)) return false;
            _focus = 0f;
            _windowSeconds = Math.Max(0f, seconds);
            _empoweredUntil = now + _windowSeconds;
            return true;
        }

        // ---------------- Hair Trigger (work order 025, 2026-11-20): the gauge in parts ----------------

        /// <summary>The focus one part holds when the gauge is in <paramref name="parts"/> parts.</summary>
        public static float PartSize(IdentityValues values, int parts) => parts <= 0 ? values.MaxFocus : values.MaxFocus / parts;

        /// <summary>How many whole parts are full, 0 to <paramref name="parts"/>.</summary>
        public int PartsFull(IdentityValues values, int parts)
        {
            float size = PartSize(values, parts);
            if (size <= 0f) return 0;
            return Math.Min(Math.Max(1, parts), (int)Math.Floor(_focus / size + 1e-4f));
        }

        /// <summary>
        /// Spends one full part: a part's worth of focus comes off, and what fills the next part stays. False, and
        /// nothing happens, with no part full. There is no window.
        /// </summary>
        public bool TrySpendPart(IdentityValues values, int parts)
        {
            if (PartsFull(values, parts) < 1) return false;
            _focus = Math.Max(0f, _focus - PartSize(values, parts));
            return true;
        }

        /// <summary>Fills the gauge. For tests and tools.</summary>
        public void Fill(IdentityValues values) => _focus = values.MaxFocus;

        /// <summary>Back to its start: an empty gauge and no window (work order 026: a run starts with a clean slate).</summary>
        public void Clear()
        {
            _focus = 0f;
            _empoweredUntil = float.NegativeInfinity;
            _windowSeconds = 0f;
        }

        public float DamageMultiplier(float now, IdentityValues values) => IsEmpowered(now) ? values.EmpoweredDamage : 1f;

        /// <summary>Damage after the empowered mode's multiplier, rounded to a whole number.</summary>
        public int Scale(int damage, float now, IdentityValues values) =>
            (int)Math.Round(damage * DamageMultiplier(now, values));

        /// <summary>
        /// For the bar, 0 to 1: how full the gauge is, or while empowered how much of the window is left, of the length it
        /// was opened for.
        /// </summary>
        public float BarFill(float now, IdentityValues values)
        {
            if (IsEmpowered(now))
                return _windowSeconds <= 0f ? 0f : EmpoweredRemaining(now) / _windowSeconds;
            return values.MaxFocus <= 0f ? 0f : Math.Min(1f, _focus / values.MaxFocus);
        }
    }
}
