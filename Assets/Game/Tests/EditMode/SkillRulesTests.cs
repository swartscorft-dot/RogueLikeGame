using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    public class SkillDefaultsTests
    {
        [Test]
        public void TheFirstFive_AreOneOfEachInputType()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            string[] firstFive = { "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Heavy Volley" };
            Assert.That(firstFive.Select(n => skills.Single(s => s.Name == n).Input), Is.EquivalentTo(new[]
            {
                SkillInput.Normal, SkillInput.Point, SkillInput.Holding, SkillInput.Charge, SkillInput.Casting,
            }));
        }

        [Test]
        public void EachCallGivesFreshCopies()
        {
            Assert.That(SkillDefaults.Create()[0], Is.Not.SameAs(SkillDefaults.Create()[0]));
        }
    }

    public class SkillCastTests
    {
        static SkillDefinition Normal() => new SkillDefinition { Input = SkillInput.Normal, Startup = 0.2f, Recovery = 0.3f };

        static SkillDefinition Holding() => new SkillDefinition
        {
            Input = SkillInput.Holding, Startup = 0.2f, Recovery = 0.3f, HoldMax = 1f, TickInterval = 0.25f,
        };

        static SkillDefinition Charge() => new SkillDefinition
        {
            Input = SkillInput.Charge, Startup = 0.1f, Recovery = 0.3f, HoldMax = 2f, ChargeTime = 1f,
        };

        [Test]
        public void NothingStarted_IsNotRunning()
        {
            var cast = new SkillCast();
            Assert.That(cast.IsRunning(0f), Is.False);
            Assert.That(cast.Skill, Is.Null);
            Assert.That(cast.TakeHits(100f), Is.EqualTo(0));
        }

        [Test]
        public void Normal_Startup_OneHit_Recovery()
        {
            var cast = new SkillCast();
            cast.Start(Normal(), 10f);

            Assert.That(cast.Phase(10.1f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(10.19f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(10.21f), Is.EqualTo(1), "the hit comes when the startup ends");
            Assert.That(cast.TakeHits(10.3f), Is.EqualTo(0), "and only once");
            Assert.That(cast.Phase(10.3f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.Phase(10.49f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.IsRunning(10.51f), Is.False);
        }

        [Test]
        public void Normal_IgnoresTheRelease()
        {
            var cast = new SkillCast();
            cast.Start(Normal(), 0f);
            cast.Release(0.05f);
            Assert.That(cast.Phase(0.1f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(0.21f), Is.EqualTo(1));
        }

        [Test]
        public void ALongFrame_StillGivesTheHit()
        {
            var cast = new SkillCast();
            cast.Start(Normal(), 0f);
            Assert.That(cast.TakeHits(5f), Is.EqualTo(1));
            Assert.That(cast.IsRunning(5f), Is.False);
        }

        [Test]
        public void Cancelled_NeverHits()
        {
            var cast = new SkillCast();
            cast.Start(Normal(), 0f);
            cast.Cancel();
            Assert.That(cast.TakeHits(1f), Is.EqualTo(0));
            Assert.That(cast.IsRunning(0.1f), Is.False);
            Assert.That(cast.Skill, Is.Null);
        }

        [Test]
        public void Casting_ProgressFillsOverTheCast()
        {
            var cast = new SkillCast();
            cast.Start(new SkillDefinition { Input = SkillInput.Casting, Startup = 1f, Recovery = 0.5f }, 0f);
            Assert.That(cast.Progress(0.25f), Is.EqualTo(0.25f).Within(1e-4f));
            Assert.That(cast.Progress(0.75f), Is.EqualTo(0.75f).Within(1e-4f));
            Assert.That(cast.TakeHits(0.99f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(1.01f), Is.EqualTo(1));
            Assert.That(cast.Progress(1.2f), Is.EqualTo(0f), "no bar in recovery");
        }

        [Test]
        public void Holding_HitsAtOnce_ThenEveryTick_UntilTheRelease()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f); // the channel starts at 0.2, ticks at 0.2, 0.45, 0.7, 0.95, 1.2

            Assert.That(cast.Phase(0.1f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(0.19f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(0.21f), Is.EqualTo(1), "the first hit comes when the startup ends");
            Assert.That(cast.Phase(0.3f), Is.EqualTo(SkillPhase.Channel));
            Assert.That(cast.TakeHits(0.44f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(0.46f), Is.EqualTo(1));

            cast.Release(0.6f);
            Assert.That(cast.TakeHits(0.9f), Is.EqualTo(0), "no hit after the release");
            Assert.That(cast.Phase(0.7f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.IsRunning(0.89f), Is.True);
            Assert.That(cast.IsRunning(0.91f), Is.False, "the recovery is counted from the release");
        }

        [Test]
        public void Holding_NeverReleased_EndsAtTheLongestHold()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f); // channel 0.2 to 1.2: ticks at 0.2, 0.45, 0.7, 0.95, 1.2
            Assert.That(cast.TakeHits(10f), Is.EqualTo(5));
            Assert.That(cast.Phase(1.19f), Is.EqualTo(SkillPhase.Channel));
            Assert.That(cast.Phase(1.21f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.IsRunning(1.51f), Is.False);
        }

        [Test]
        public void Holding_ReleasedDuringTheStartup_StillHitsOnce()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f);
            cast.Release(0.05f);
            Assert.That(cast.Phase(0.1f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(1f), Is.EqualTo(1));
            Assert.That(cast.IsRunning(0.49f), Is.True);
            Assert.That(cast.IsRunning(0.51f), Is.False);
        }

        [Test]
        public void Holding_ALongFrame_GivesEveryTickItSkipped()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f);
            Assert.That(cast.TakeHits(0.21f), Is.EqualTo(1));
            Assert.That(cast.TakeHits(0.96f), Is.EqualTo(3));
        }

        [Test]
        public void Holding_OnlyTheFirstReleaseCounts()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f);
            cast.Release(0.5f);
            cast.Release(0.9f);
            Assert.That(cast.TakeHits(5f), Is.EqualTo(2), "ticks at 0.2 and 0.45 only");
        }

        [Test]
        public void Charge_ChargesWhileHeld_AndFiresAfterTheRelease()
        {
            var cast = new SkillCast();
            cast.Start(Charge(), 0f);

            Assert.That(cast.Phase(0.4f), Is.EqualTo(SkillPhase.Channel));
            Assert.That(cast.Charge(0.4f), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(cast.Progress(0.4f), Is.EqualTo(0.4f).Within(1e-4f));
            Assert.That(cast.TakeHits(0.4f), Is.EqualTo(0));

            cast.Release(0.5f);
            Assert.That(cast.Phase(0.55f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(0.59f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(0.61f), Is.EqualTo(1), "it fires one startup after the release");
            Assert.That(cast.Charge(0.61f), Is.EqualTo(0.5f).Within(1e-4f), "the charge stopped at the release");
            Assert.That(cast.Phase(0.7f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.IsRunning(0.91f), Is.False);
        }

        [Test]
        public void Charge_StopsGrowingAtFullCharge()
        {
            var cast = new SkillCast();
            cast.Start(Charge(), 0f);
            Assert.That(cast.Charge(1f), Is.EqualTo(1f));
            Assert.That(cast.Charge(1.8f), Is.EqualTo(1f));
            Assert.That(cast.Phase(1.8f), Is.EqualTo(SkillPhase.Channel), "it can be kept at full charge");
        }

        [Test]
        public void Charge_NeverReleased_FiresByItselfAtTheLongestHold()
        {
            var cast = new SkillCast();
            cast.Start(Charge(), 0f);
            Assert.That(cast.TakeHits(2.09f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(2.11f), Is.EqualTo(1));
            Assert.That(cast.Charge(2.11f), Is.EqualTo(1f));
        }

        [Test]
        public void Charge_ReleasedAtOnce_FiresWithNoCharge()
        {
            var cast = new SkillCast();
            cast.Start(Charge(), 0f);
            cast.Release(0f);
            Assert.That(cast.TakeHits(0.11f), Is.EqualTo(1));
            Assert.That(cast.Charge(0.11f), Is.EqualTo(0f));
        }

        [Test]
        public void StartingAgain_ForgetsTheLastUse()
        {
            var cast = new SkillCast();
            cast.Start(Holding(), 0f);
            cast.Release(0.5f);
            cast.TakeHits(5f);

            cast.Start(Normal(), 10f);
            Assert.That(cast.Phase(10.1f), Is.EqualTo(SkillPhase.Startup));
            Assert.That(cast.TakeHits(10.21f), Is.EqualTo(1));
        }
    }

    public class CooldownTests
    {
        [Test]
        public void ReadyUntilStarted_ThenCountsDown()
        {
            var cooldown = new Cooldown();
            Assert.That(cooldown.IsReady(0f), Is.True);
            cooldown.Start(1f, 4f);
            Assert.That(cooldown.IsReady(4.9f), Is.False);
            Assert.That(cooldown.Remaining(3f), Is.EqualTo(2f).Within(1e-4f));
            Assert.That(cooldown.IsReady(5f), Is.True);
            Assert.That(cooldown.Remaining(6f), Is.EqualTo(0f));
        }
    }

    public class SkillAreaTests
    {
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);

        [Test]
        public void ClampToRange_KeepsANearPoint_AndPullsInAFarOne()
        {
            GroundPoint near = SkillRules.ClampToRange(Origin, new GroundPoint(3f, 4f), 8f);
            Assert.That(near.X, Is.EqualTo(3f));
            Assert.That(near.Z, Is.EqualTo(4f));

            GroundPoint far = SkillRules.ClampToRange(Origin, new GroundPoint(0f, 20f), 8f);
            Assert.That(far.X, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(far.Z, Is.EqualTo(8f).Within(1e-4f));
        }

        [Test]
        public void Circle_TouchingTheTargetsEdgeCounts()
        {
            var center = new GroundPoint(0f, 5f);
            Assert.That(SkillRules.InCircle(center, 2.5f, new GroundPoint(2.9f, 5f), 0.5f), Is.True);
            Assert.That(SkillRules.InCircle(center, 2.5f, new GroundPoint(3.1f, 5f), 0.5f), Is.False);
        }

        [Test]
        public void Cone_HitsInsideTheAngleAndRange()
        {
            var aim = new GroundPoint(0f, 1f);
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(0f, 5f), 0.5f), Is.True, "straight ahead");
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(2f, 5f), 0.5f), Is.True, "22 degrees off");
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(5f, 5f), 0.5f), Is.False, "45 degrees off");
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(0f, -5f), 0.5f), Is.False, "behind");
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(0f, 7.4f), 0.5f), Is.True, "its edge is in range");
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, new GroundPoint(0f, 7.6f), 0.5f), Is.False, "out of range");
        }

        [Test]
        public void Cone_ATargetJustOutsideTheAngleIsHitByItsEdge()
        {
            var aim = new GroundPoint(0f, 1f);
            // 30 degrees off the axis of a 50 degree cone, 2 m away: the 0.5 m radius covers the last 5 degrees.
            var target = new GroundPoint(1f, 1.732f);
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, target, 0.5f), Is.True);
            Assert.That(SkillRules.InCone(Origin, aim, 7f, 50f, target, 0.05f), Is.False);
        }

        [Test]
        public void Cone_WithNoDirection_HitsNothing()
        {
            Assert.That(SkillRules.InCone(Origin, Origin, 7f, 50f, new GroundPoint(0f, 1f), 0.5f), Is.False);
        }

        [Test]
        public void Hits_UsesTheSkillsShape()
        {
            var aim = new GroundPoint(0f, 20f);
            var target = new GroundPoint(0f, 10f);

            var line = new SkillDefinition { Shape = SkillShape.Line, Range = 12f, HalfWidth = 0.3f };
            var circle = new SkillDefinition { Shape = SkillShape.Circle, Range = 8f, Radius = 2.5f };
            var cone = new SkillDefinition { Shape = SkillShape.Cone, Range = 7f, Angle = 50f };

            Assert.That(SkillRules.Hits(line, Origin, aim, target, 0.5f), Is.True);
            Assert.That(SkillRules.Hits(circle, Origin, aim, target, 0.5f), Is.True, "the circle lands at 8 m and reaches 10 m");
            Assert.That(SkillRules.Hits(cone, Origin, aim, target, 0.5f), Is.False, "the cone is only 7 m long");

            var beside = new GroundPoint(3f, 8f);
            Assert.That(SkillRules.Hits(line, Origin, aim, beside, 0.5f), Is.False);
            Assert.That(SkillRules.Hits(circle, Origin, aim, beside, 0.5f), Is.True);
        }
    }

    public class SkillDamageTests
    {
        [Test]
        public void AChargeSkill_DealsMoreTheMoreItIsCharged()
        {
            var skill = new SkillDefinition { Input = SkillInput.Charge, Damage = 300, MinChargeDamage = 0.3f };
            Assert.That(SkillRules.Damage(skill, 0f), Is.EqualTo(90));
            Assert.That(SkillRules.Damage(skill, 0.5f), Is.EqualTo(195));
            Assert.That(SkillRules.Damage(skill, 1f), Is.EqualTo(300));
            Assert.That(SkillRules.Damage(skill, 5f), Is.EqualTo(300), "charge is capped at full");
        }

        [Test]
        public void OtherSkills_IgnoreTheCharge()
        {
            var skill = new SkillDefinition { Input = SkillInput.Normal, Damage = 100, MinChargeDamage = 0.3f };
            Assert.That(SkillRules.Damage(skill, 0f), Is.EqualTo(100));
        }
    }
}
