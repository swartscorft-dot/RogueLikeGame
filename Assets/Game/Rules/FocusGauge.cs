using System;

namespace Roguelike.Rules
{
    /// <summary>The numbers of the identity gauge. They live in the skill settings asset.</summary>
    [Serializable]
    public sealed class IdentityValues
    {
        public float MaxFocus = 100f;          // the gauge is full at this much focus
        public float FocusPerDamage = 0.05f;   // focus gained per point of damage dealt
        public float EmpoweredDuration = 8f;   // s the empowered mode lasts
        public float EmpoweredDamage = 1.5f;   // damage is multiplied by this while empowered
    }

    /// <summary>
    /// The identity gauge of the first class: focus. Dealing damage fills it. When it is full it can be spent
    /// on the empowered mode, in which every shot and skill deals more damage for a while.
    /// No focus is gained while empowered. Time is passed in, in seconds.
    /// </summary>
    public sealed class FocusGauge
    {
        float _focus;
        float _empoweredUntil = float.NegativeInfinity;

        public float Focus => _focus;

        public bool IsEmpowered(float now) => now < _empoweredUntil;
        public float EmpoweredRemaining(float now) => now < _empoweredUntil ? _empoweredUntil - now : 0f;
        public bool IsFull(IdentityValues values) => _focus >= values.MaxFocus;

        /// <summary>Focus for damage dealt. Nothing while empowered, and never more than a full gauge.</summary>
        public void AddDamage(int damage, IdentityValues values, float now)
        {
            if (damage <= 0 || IsEmpowered(now)) return;
            _focus = Math.Min(values.MaxFocus, _focus + damage * values.FocusPerDamage);
        }

        /// <summary>Spends a full gauge on the empowered mode. False, and nothing happens, if it is not full or already running.</summary>
        public bool TryActivate(float now, IdentityValues values)
        {
            if (IsEmpowered(now) || !IsFull(values)) return false;
            _focus = 0f;
            _empoweredUntil = now + Math.Max(0f, values.EmpoweredDuration);
            return true;
        }

        /// <summary>Fills the gauge. For tests and tools.</summary>
        public void Fill(IdentityValues values) => _focus = values.MaxFocus;

        public float DamageMultiplier(float now, IdentityValues values) => IsEmpowered(now) ? values.EmpoweredDamage : 1f;

        /// <summary>Damage after the empowered mode's multiplier, rounded to a whole number.</summary>
        public int Scale(int damage, float now, IdentityValues values) =>
            (int)Math.Round(damage * DamageMultiplier(now, values));

        /// <summary>For the bar, 0 to 1: how full the gauge is, or while empowered how much of the mode is left.</summary>
        public float BarFill(float now, IdentityValues values)
        {
            if (IsEmpowered(now))
                return values.EmpoweredDuration <= 0f ? 0f : EmpoweredRemaining(now) / values.EmpoweredDuration;
            return values.MaxFocus <= 0f ? 0f : Math.Min(1f, _focus / values.MaxFocus);
        }
    }
}
