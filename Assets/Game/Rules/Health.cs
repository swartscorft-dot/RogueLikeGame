using System;

namespace Roguelike.Rules
{
    public sealed class Health
    {
        public int Max { get; private set; }
        public int Current { get; private set; }
        public bool IsDead => Current <= 0;

        public Health(int max)
        {
            Max = max;
            Current = max;
        }

        /// <summary>Applies damage and returns the amount actually removed.</summary>
        public int TakeDamage(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int applied = amount > Current ? Current : amount;
            Current -= applied;
            return applied;
        }

        public void Reset() => Current = Max;

        /// <summary>A new maximum (work order 012: a run's upgrades raise it); what she has is kept, cut to the new maximum.</summary>
        public void SetMax(int max)
        {
            Max = Math.Max(1, max);
            if (Current > Max) Current = Max;
        }

        /// <summary>Gives back up to <paramref name="amount"/>, never over the maximum, never to the dead. Returns what was given.</summary>
        public int Heal(int amount)
        {
            if (amount <= 0 || IsDead) return 0;
            int given = Math.Min(amount, Max - Current);
            Current += given;
            return given;
        }

        /// <summary>Sets what she has, between 1 and the maximum (work order 012: health carried from floor to floor).</summary>
        public void Set(int current) => Current = Math.Max(1, Math.Min(Max, current));
    }
}
