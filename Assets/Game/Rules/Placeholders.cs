namespace Roguelike.Rules
{
    /// <summary>
    /// Numbers G2 needs that are not feel values. They stand in until G3 designs real skills
    /// and are listed in docs/slices/g2-control.md.
    /// </summary>
    public static class Placeholders
    {
        public const int BasicShotDamage = 40;

        public const float SkillRange = 12f;
        public const float SkillHalfWidth = 0.3f;
        public const int SkillDamage = 100;

        public const int DummyHealth = 1000;
        public const float DummyRefillDelay = 2f;

        /// <summary>How far outside a target's edge a click still counts as a click on it.</summary>
        public const float CursorPickSlack = 0.5f;
    }
}
