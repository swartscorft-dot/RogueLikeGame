namespace Roguelike.Rules
{
    /// <summary>
    /// Numbers the control room needs that are not feel values and not skill values.
    /// They are listed in docs/slices/g2-control.md.
    /// </summary>
    public static class Placeholders
    {
        public const int BasicShotDamage = 40;


        public const int DummyHealth = 1000;
        public const float DummyRefillDelay = 2f;
        public const int PlayerHealth = 100;
        /// <summary>The player's footprint on the ground, for what an enemy's attack touches.</summary>
        public const float PlayerRadius = 0.5f;
        /// <summary>Seconds from the player's death until it stands at the start again. The control room is for practice.</summary>
        public const float PlayerRespawnDelay = 2f;

        /// <summary>How far outside a target's edge a click still counts as a click on it.</summary>
        public const float CursorPickSlack = 0.5f;
    }
}
