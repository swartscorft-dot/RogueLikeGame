using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    public class KitDefaultsTests
    {
        [Test]
        public void EightSkills_CoveringFiveInputTypes()
        {
            SkillDefinition[] skills = SkillDefaults.Create();
            Assert.That(skills.Length, Is.EqualTo(8), "the loadout of docs/vision/first-class.md");
            Assert.That(skills.Select(s => s.Input).Distinct().Count(), Is.GreaterThanOrEqualTo(5));
            Assert.That(skills.Select(s => s.Name).Distinct().Count(), Is.EqualTo(8), "names are unique");
        }
    }

    public class SkillBurstTests
    {
        static SkillDefinition Burst() => new SkillDefinition
        {
            Input = SkillInput.Normal, Startup = 0.2f, Recovery = 0.3f, HitCount = 3, HitInterval = 0.1f,
        };

        [Test]
        public void ABurst_HitsHitCountTimes_HitIntervalApart()
        {
            var cast = new SkillCast();
            cast.Start(Burst(), 0f); // hits at 0.2, 0.3, 0.4; recovery to 0.7

            Assert.That(cast.TakeHits(0.19f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(0.21f), Is.EqualTo(1));
            Assert.That(cast.Phase(0.25f), Is.EqualTo(SkillPhase.Active), "between the hits of the burst");
            Assert.That(cast.TakeHits(0.29f), Is.EqualTo(0));
            Assert.That(cast.TakeHits(0.31f), Is.EqualTo(1));
            Assert.That(cast.TakeHits(0.41f), Is.EqualTo(1));
            Assert.That(cast.TakeHits(0.6f), Is.EqualTo(0), "three and no more");
            Assert.That(cast.Phase(0.5f), Is.EqualTo(SkillPhase.Recovery));
            Assert.That(cast.IsRunning(0.69f), Is.True, "the recovery is counted from the last hit");
            Assert.That(cast.IsRunning(0.71f), Is.False);
        }

        [Test]
        public void ABurst_ALongFrameGivesEveryHit()
        {
            var cast = new SkillCast();
            cast.Start(Burst(), 0f);
            Assert.That(cast.TakeHits(5f), Is.EqualTo(3));
        }

        [Test]
        public void ABurst_CancelledPartWay_StopsHitting()
        {
            var cast = new SkillCast();
            cast.Start(Burst(), 0f);
            Assert.That(cast.TakeHits(0.21f), Is.EqualTo(1));
            cast.Cancel();
            Assert.That(cast.TakeHits(1f), Is.EqualTo(0));
        }

        [Test]
        public void HitCountOfOne_IsASingleHit_AndZeroCountsAsOne()
        {
            var cast = new SkillCast();
            cast.Start(new SkillDefinition { Input = SkillInput.Normal, Startup = 0.2f, HitCount = 0 }, 0f);
            Assert.That(cast.TakeHits(5f), Is.EqualTo(1));
        }

        [Test]
        public void StartupProgress_RunsFromZeroToOneOverTheStartup_AndStaysAtOne()
        {
            var cast = new SkillCast();
            Assert.That(cast.StartupProgress(0f), Is.EqualTo(0f), "nothing started");
            cast.Start(Burst(), 1f);
            Assert.That(cast.StartupProgress(1f), Is.EqualTo(0f));
            Assert.That(cast.StartupProgress(1.1f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(cast.StartupProgress(1.25f), Is.EqualTo(1f));
            Assert.That(cast.StartupProgress(1.6f), Is.EqualTo(1f), "it does not fall back in the burst or the recovery");
        }

        [Test]
        public void StartupProgress_OfAChargeSkill_BeginsAtTheRelease()
        {
            var cast = new SkillCast();
            cast.Start(new SkillDefinition { Input = SkillInput.Charge, Startup = 0.2f, HoldMax = 2f, ChargeTime = 1f }, 0f);
            Assert.That(cast.StartupProgress(0.5f), Is.EqualTo(0f), "still charging");
            cast.Release(1f);
            Assert.That(cast.StartupProgress(1.1f), Is.EqualTo(0.5f).Within(1e-3f));
        }
    }

    public class FocusGaugeTests
    {
        static readonly IdentityValues Values = new IdentityValues
        {
            MaxFocus = 100f, FocusPerDamage = 0.05f, EmpoweredDuration = 8f, EmpoweredDamage = 1.5f,
        };

        [Test]
        public void DamageDealtFillsIt_UpToFull()
        {
            var gauge = new FocusGauge();
            gauge.AddDamage(100, Values, 0f);
            Assert.That(gauge.Focus, Is.EqualTo(5f).Within(1e-4f));
            Assert.That(gauge.IsFull(Values), Is.False);
            gauge.AddDamage(5000, Values, 0f);
            Assert.That(gauge.Focus, Is.EqualTo(100f));
            Assert.That(gauge.IsFull(Values), Is.True);
        }

        [Test]
        public void ItCanOnlyBeSpentWhenFull()
        {
            var gauge = new FocusGauge();
            gauge.AddDamage(1900, Values, 0f);
            Assert.That(gauge.TryActivate(1f, Values), Is.False, "95 of 100");
            Assert.That(gauge.Focus, Is.EqualTo(95f).Within(1e-3f), "a refused press spends nothing");
            gauge.AddDamage(100, Values, 0f);
            Assert.That(gauge.TryActivate(1f, Values), Is.True);
            Assert.That(gauge.Focus, Is.EqualTo(0f), "the whole gauge is spent");
        }

        [Test]
        public void Empowered_MultipliesDamage_ForItsDuration()
        {
            var gauge = new FocusGauge();
            gauge.Fill(Values);
            Assert.That(gauge.Scale(100, 0f, Values), Is.EqualTo(100));

            Assert.That(gauge.TryActivate(10f, Values), Is.True);
            Assert.That(gauge.IsEmpowered(10f), Is.True);
            Assert.That(gauge.Scale(100, 12f, Values), Is.EqualTo(150));
            Assert.That(gauge.Scale(30, 12f, Values), Is.EqualTo(45));
            Assert.That(gauge.EmpoweredRemaining(12f), Is.EqualTo(6f).Within(1e-4f));
            Assert.That(gauge.IsEmpowered(17.9f), Is.True);
            Assert.That(gauge.IsEmpowered(18.1f), Is.False);
            Assert.That(gauge.Scale(100, 18.1f, Values), Is.EqualTo(100));
        }

        [Test]
        public void NoFocusIsGainedWhileEmpowered_AndItCannotBeStartedTwice()
        {
            var gauge = new FocusGauge();
            gauge.Fill(Values);
            gauge.TryActivate(0f, Values);
            gauge.AddDamage(5000, Values, 1f);
            Assert.That(gauge.Focus, Is.EqualTo(0f));
            Assert.That(gauge.TryActivate(1f, Values), Is.False);

            gauge.AddDamage(200, Values, 9f);
            Assert.That(gauge.Focus, Is.EqualTo(10f).Within(1e-4f), "it fills again when the mode is over");
        }

        [Test]
        public void TheBarShowsTheGauge_OrWhatIsLeftOfTheMode()
        {
            var gauge = new FocusGauge();
            gauge.AddDamage(1000, Values, 0f);
            Assert.That(gauge.BarFill(0f, Values), Is.EqualTo(0.5f).Within(1e-4f));
            gauge.Fill(Values);
            gauge.TryActivate(0f, Values);
            Assert.That(gauge.BarFill(2f, Values), Is.EqualTo(0.75f).Within(1e-4f));
        }
    }

    public class EnemyBrainTests
    {
        static EnemyValues Values() => new EnemyValues(); // Slam at 0 to 3 m, Shockwave at 3 to 11 m, aggro 9 m, leash 22 m

        [Test]
        public void ItStandsStill_UntilThePlayerComesNear()
        {
            var brain = new EnemyBrain();
            EnemyValues values = Values();
            Assert.That(brain.Step(0f, 15f, true, false, values), Is.False);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            brain.Step(1f, 8.5f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
        }

        [Test]
        public void BeingHurtWakesIt_FromBeyondItsAggroRange_ButNotFromBeyondItsLeash()
        {
            EnemyValues values = Values();
            var brain = new EnemyBrain();
            brain.Step(0f, 15f, true, true, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));

            var far = new EnemyBrain();
            far.Step(0f, 30f, true, true, values);
            Assert.That(far.State, Is.EqualTo(EnemyState.Idle));
        }

        [Test]
        public void ItPicksTheAttackThatFitsTheDistance()
        {
            EnemyValues values = Values();
            Assert.That(EnemyBrain.AttackFor(2f, values).Name, Is.EqualTo("Slam"));
            Assert.That(EnemyBrain.AttackFor(7f, values).Name, Is.EqualTo("Shockwave"));
            Assert.That(EnemyBrain.AttackFor(15f, values), Is.Null, "too far for either: it keeps walking");
        }

        [Test]
        public void AnAttack_IsAWindup_OneStrike_AndARecovery()
        {
            EnemyValues values = Values();
            var brain = new EnemyBrain();
            brain.Step(0f, 2f, true, false, values);            // wakes
            brain.Step(0.1f, 2f, true, false, values);          // picks Slam: windup 0.9, recovery 1.0
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));
            Assert.That(brain.Attack.Name, Is.EqualTo("Slam"));
            Assert.That(brain.WindupProgress(0.55f), Is.EqualTo(0.5f).Within(1e-3f));
            Assert.That(brain.WindupRemaining(0.55f), Is.EqualTo(0.45f).Within(1e-3f));

            Assert.That(brain.Step(0.9f, 2f, true, false, values), Is.False, "the telegraph is still showing");
            Assert.That(brain.Step(1.01f, 2f, true, false, values), Is.True, "the strike");
            Assert.That(brain.State, Is.EqualTo(EnemyState.Recover));
            Assert.That(brain.Step(1.5f, 2f, true, false, values), Is.False, "one strike only");
            Assert.That(brain.State, Is.EqualTo(EnemyState.Recover));

            brain.Step(2.02f, 2f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "then it comes again");
            Assert.That(brain.Attack, Is.Null);
        }

        [Test]
        public void TheStrikeComes_EvenIfThePlayerHasLeft()
        {
            EnemyValues values = Values();
            var brain = new EnemyBrain();
            brain.Step(0f, 2f, true, false, values);
            brain.Step(0.1f, 2f, true, false, values);
            Assert.That(brain.Step(1.01f, 40f, true, false, values), Is.True, "a windup is committed");
        }

        [Test]
        public void ItGivesUp_WhenThePlayerIsBeyondTheLeash_OrDead()
        {
            EnemyValues values = Values();
            var brain = new EnemyBrain();
            brain.Step(0f, 8f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            brain.Step(1f, 25f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));

            brain.Step(2f, 8f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            brain.Step(3f, 15f, false, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle), "nobody left to fight");
            brain.Step(4f, 2f, false, true, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle), "a dead player does not wake it");
        }

        [Test]
        public void Reset_PutsItBackAtHome()
        {
            EnemyValues values = Values();
            var brain = new EnemyBrain();
            brain.Step(0f, 2f, true, false, values);
            brain.Step(0.1f, 2f, true, false, values);
            brain.Reset();
            Assert.That(brain.State, Is.EqualTo(EnemyState.Idle));
            Assert.That(brain.Attack, Is.Null);
            Assert.That(brain.WindupProgress(0.5f), Is.EqualTo(0f));
        }

        [Test]
        public void TheCircleHitsAroundTheEnemy_AndTheLineAlongItsLockedAim()
        {
            EnemyValues values = Values();
            EnemyAttackDefinition slam = values.Attacks[0];      // radius 3
            EnemyAttackDefinition wave = values.Attacks[1];      // 12 m long, 2.4 m wide
            var origin = new GroundPoint(0f, 0f);
            var aim = new GroundPoint(0f, 5f);

            Assert.That(EnemyBrain.Hits(slam, origin, aim, new GroundPoint(-3.4f, 0f), 0.5f), Is.True, "any side, edge counts");
            Assert.That(EnemyBrain.Hits(slam, origin, aim, new GroundPoint(0f, 3.6f), 0.5f), Is.False);

            Assert.That(EnemyBrain.Hits(wave, origin, aim, new GroundPoint(0f, 9f), 0.5f), Is.True, "down the line");
            Assert.That(EnemyBrain.Hits(wave, origin, aim, new GroundPoint(1.6f, 9f), 0.5f), Is.True, "its edge is in the strip");
            Assert.That(EnemyBrain.Hits(wave, origin, aim, new GroundPoint(1.8f, 9f), 0.5f), Is.False, "a step to the side is enough");
            Assert.That(EnemyBrain.Hits(wave, origin, aim, new GroundPoint(0f, -3f), 0.5f), Is.False, "behind it");
        }
    }
}
