using System;

namespace Roguelike.Rules
{
    /// <summary>How a skill is played with its key.</summary>
    public enum SkillInput
    {
        /// <summary>Press: it fires after its startup.</summary>
        Normal,
        /// <summary>Press: it lands on the ground point under the cursor, no farther away than its range.</summary>
        Point,
        /// <summary>Hold: it hits again and again while the key is down, up to a longest time.</summary>
        Holding,
        /// <summary>Hold to charge, release to fire. A longer charge hits harder.</summary>
        Charge,
        /// <summary>Press: a long cast with a bar, then one heavy hit.</summary>
        Casting,
    }

    /// <summary>The area a skill hits.</summary>
    public enum SkillShape
    {
        /// <summary>A straight strip from the character toward the aim: Range long, 2 * HalfWidth wide.</summary>
        Line,
        /// <summary>A circle of Radius around the aimed ground point, placed at most Range away.</summary>
        Circle,
        /// <summary>A wedge from the character toward the aim: Range long, Angle degrees wide.</summary>
        Cone,
    }

    /// <summary>
    /// One skill, as numbers. The instances live in the skill settings asset, where Kristian tunes them.
    /// Which fields a skill uses depends on its input type and shape; the rest are ignored.
    /// </summary>
    [Serializable]
    public sealed class SkillDefinition
    {
        public string Name = "Skill";
        public SkillInput Input = SkillInput.Normal;
        public SkillShape Shape = SkillShape.Line;

        public float Cooldown = 4f;        // s, counted from the moment the skill starts
        public int Damage = 100;           // per hit, per target. Charge: at full charge

        // Timing
        public float Startup = 0.15f;      // s before the first hit. Casting: the cast time. Charge: from release to the shot
        public float Recovery = 0.3f;      // s locked after the last hit
        public int HitCount = 1;           // hits in a burst, for every input type but Holding
        public float HitInterval = 0.1f;   // s between the hits of a burst

        // Area
        public float Range = 12f;          // m
        public float HalfWidth = 0.3f;     // m, Line
        public float Radius = 2.5f;        // m, Circle
        public float Angle = 50f;          // degrees, Cone

        // Movement
        public float MoveDistance = 0f;    // m the character moves during the startup: forward along the aim, or backward if negative

        // Holding and Charge
        public float HoldMax = 2f;         // s the key can be held before the skill ends (Holding) or fires by itself (Charge)
        public float TickInterval = 0.15f; // s between hits, Holding
        public float ChargeTime = 1f;      // s to full charge, Charge
        public float MinChargeDamage = 0.3f; // share of Damage dealt with no charge at all, Charge
    }

    /// <summary>
    /// The eight skills of the first class, in key order: Q W E R, S D F G.
    /// The first five came in G3, one per input type. Scatter Blast, Backstep and Triple Tap came in G4.
    /// See docs/slices/g3-skills.md and docs/slices/g4-kit.md.
    /// </summary>
    public static class SkillDefaults
    {
        public static SkillDefinition[] Create() => new[]
        {
            new SkillDefinition
            {
                Name = "Quick Shot", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 4f, Damage = 100, Startup = 0.15f, Recovery = 0.3f, Range = 12f, HalfWidth = 0.3f,
            },
            new SkillDefinition
            {
                Name = "Powder Keg", Input = SkillInput.Point, Shape = SkillShape.Circle,
                Cooldown = 8f, Damage = 180, Startup = 0.5f, Recovery = 0.2f, Range = 8f, Radius = 2.5f,
            },
            new SkillDefinition
            {
                Name = "Fan Fire", Input = SkillInput.Holding, Shape = SkillShape.Cone,
                Cooldown = 10f, Damage = 30, Startup = 0.2f, Recovery = 0.3f, Range = 7f, Angle = 50f,
                HoldMax = 2f, TickInterval = 0.15f,
            },
            new SkillDefinition
            {
                Name = "Long Shot", Input = SkillInput.Charge, Shape = SkillShape.Line,
                Cooldown = 12f, Damage = 300, Startup = 0.1f, Recovery = 0.4f, Range = 16f, HalfWidth = 0.25f,
                HoldMax = 2.5f, ChargeTime = 1f, MinChargeDamage = 0.3f,
            },
            new SkillDefinition
            {
                Name = "Scatter Blast", Input = SkillInput.Normal, Shape = SkillShape.Cone,
                Cooldown = 9f, Damage = 220, Startup = 0.2f, Recovery = 0.35f, Range = 5f, Angle = 70f,
            },
            new SkillDefinition
            {
                Name = "Heavy Volley", Input = SkillInput.Casting, Shape = SkillShape.Line,
                Cooldown = 16f, Damage = 400, Startup = 1f, Recovery = 0.5f, Range = 14f, HalfWidth = 1f,
            },
            new SkillDefinition
            {
                Name = "Backstep", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 12f, Damage = 80, Startup = 0.2f, Recovery = 0.2f, Range = 10f, HalfWidth = 0.4f,
                MoveDistance = -5f,
            },
            new SkillDefinition
            {
                Name = "Triple Tap", Input = SkillInput.Normal, Shape = SkillShape.Line,
                Cooldown = 7f, Damage = 70, Startup = 0.15f, Recovery = 0.3f, Range = 12f, HalfWidth = 0.3f,
                HitCount = 3, HitInterval = 0.12f,
            },
        };
    }
}
