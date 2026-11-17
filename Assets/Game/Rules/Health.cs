namespace Roguelike.Rules
{
    public sealed class Health
    {
        public int Max { get; }
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
    }
}
