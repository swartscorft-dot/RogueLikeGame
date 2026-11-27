using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 150's values (Kristian, 2026-11-26: "Ja, lag de seks og bygg inn"): the switch on, each of the nine ids a model
    /// of its own at one named place, the modelled barrels' reaches as measured in Blender (each its length past the muzzle over
    /// the pistol's 1.903), the teal's glow the producer's 0.05.
    /// </summary>
    public class WeaponPartsModelledTests
    {
        static readonly string[] Nine =
        {
            WeaponAttachments.LaserSight, WeaponAttachments.GhostRing, WeaponAttachments.ReflexSight,
            WeaponAttachments.BullBarrel, WeaponAttachments.LongBarrel, WeaponAttachments.Compensator,
            WeaponAttachments.RunnersGrip, WeaponAttachments.WeightedGrip, WeaponAttachments.QuickDrawGrip,
        };

        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(WeaponStageValues.ModelledParts, Is.True);
        }

        [Test]
        public void EachOfTheNine_HasAModelOfItsOwn_AtOneNamedPlace()
        {
            string[] names = Nine.Select(WeaponStageValues.PartModelName).ToArray();
            Assert.That(names, Has.None.Null);
            Assert.That(names, Is.Unique);
            Assert.That(names, Is.EqualTo(new[] { "LaserSight", "GhostRing", "ReflexSight", "BullBarrel", "LongBarrel", "Compensator", "RunnersGrip", "WeightedGrip", "QuickDrawGrip" }), "the Blender files' names");
            foreach (string id in Nine) Assert.That(WeaponStageValues.PartModelPath(id), Is.EqualTo("Weapon/Parts/" + WeaponStageValues.PartModelName(id)), id);
            Assert.That(WeaponStageValues.PartModelPath(null), Is.Null);
            Assert.That(WeaponStageValues.PartModelPath("not-a-part"), Is.Null);
        }

        [Test]
        public void TheModelledBarrels_Reach_AsMeasuredInBlender()
        {
            const float PistolLength = 1.903f; // the pistol's length in its own units (the Blender files' frame)
            Assert.That(WeaponStageValues.BullBarrelReach, Is.EqualTo(0.243f / PistolLength).Within(0.001f));
            Assert.That(WeaponStageValues.LongBarrelReach, Is.EqualTo(0.50f / PistolLength).Within(0.001f));
            Assert.That(WeaponStageValues.CompensatorReach, Is.EqualTo(0.26f / PistolLength).Within(0.002f));
            Assert.That(WeaponStageValues.PartTealGlow, Is.EqualTo(0.05f));
        }
    }
}
