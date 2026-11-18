using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    public class FeelValuesTests
    {
        /// <summary>The code's start values are the "Start value" column of docs/vision/feel-spec.md.</summary>
        [Test]
        public void DefaultsAreTheFeelSpecStartValues()
        {
            var feel = new FeelValues();
            Assert.That(feel.CameraPitch, Is.EqualTo(55f));
            Assert.That(feel.CameraDistance, Is.EqualTo(28f));
            Assert.That(feel.FieldOfView, Is.EqualTo(30f));
            Assert.That(feel.RunSpeed, Is.EqualTo(6f));
            Assert.That(feel.TimeToFullSpeed, Is.EqualTo(0f));
            Assert.That(feel.TurnTime, Is.EqualTo(0f));
            Assert.That(feel.DashCooldown, Is.EqualTo(10f));
            Assert.That(feel.DashDistance, Is.EqualTo(6f));
            Assert.That(feel.DashDuration, Is.EqualTo(0.2f));
            Assert.That(feel.DashInvulnerability, Is.EqualTo(0.1f));
            Assert.That(feel.DashCancelsSkill, Is.True);
            Assert.That(feel.FaceCursorOnCast, Is.True);
            Assert.That(feel.InputBuffer, Is.EqualTo(0.15f));
            Assert.That(feel.BasicShotStartup, Is.EqualTo(0.15f));
            Assert.That(feel.BasicShotActive, Is.EqualTo(0.05f));
            Assert.That(feel.BasicShotRecovery, Is.EqualTo(0.25f));
            Assert.That(feel.BasicAttackRange, Is.EqualTo(6f));
            Assert.That(feel.AttackMoveClickRadius, Is.EqualTo(2f));
            Assert.That(feel.ShotFrontAngle, Is.EqualTo(60f));
            Assert.That(feel.MovingShotSpeedShare, Is.EqualTo(0.5f));
            Assert.That(feel.MovingShotRecovery, Is.EqualTo(0.2f));
            Assert.That(feel.HitStop, Is.EqualTo(0.05f));
        }
    }

    public class UpperBodyAimTests
    {
        static readonly GroundPoint Ahead = new GroundPoint(0f, 1f);

        [Test]
        public void TheTwistIsMeasuredClockwise_RightIsPositive()
        {
            Assert.That(UpperBodyAim.TwistAngle(Ahead, new GroundPoint(1f, 1f)), Is.EqualTo(45f).Within(1e-3f));
            Assert.That(UpperBodyAim.TwistAngle(Ahead, new GroundPoint(-1f, 1f)), Is.EqualTo(-45f).Within(1e-3f));
            Assert.That(UpperBodyAim.TwistAngle(Ahead, new GroundPoint(0f, 5f)), Is.EqualTo(0f).Within(1e-3f));
            Assert.That(Mathf180(UpperBodyAim.TwistAngle(Ahead, new GroundPoint(0f, -1f))), Is.EqualTo(180f).Within(1e-3f));
        }

        [Test]
        public void ATargetWithinTheFrontAngle_DoesNotTurnTheBody()
        {
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(1f, 1f), 60f), Is.False, "45 degrees right");
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(-1f, 1f), 60f), Is.False, "45 degrees left");
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(0f, 3f), 60f), Is.False, "straight ahead");
        }

        [Test]
        public void ATargetOutsideTheFrontAngle_TurnsTheBody()
        {
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(1f, 0f), 60f), Is.True, "90 degrees right");
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(-1f, 0.2f), 60f), Is.True, "79 degrees left");
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(0f, -1f), 60f), Is.True, "behind");
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(1f, 1f), 30f), Is.True, "45 degrees with a 30 degree angle");
        }

        [Test]
        public void ATargetOnTheCharacter_NeverTurnsIt()
        {
            Assert.That(UpperBodyAim.BodyMustTurn(Ahead, new GroundPoint(0f, 0f), 60f), Is.False);
        }

        static float Mathf180(float a) => System.Math.Abs(a);
    }

    public class MovingShotTests
    {
        [Test]
        public void WhileTheShotWindsUp_TheSpeedIsTheSlowShare()
        {
            Assert.That(MovingShot.SpeedShare(true, -1f, 0.5f, 0.2f), Is.EqualTo(0.5f));
        }

        [Test]
        public void AfterTheShot_TheSpeedComesBackInAStraightLine_AndIsFullAfterTheRecovery()
        {
            Assert.That(MovingShot.SpeedShare(false, 0f, 0.5f, 0.2f), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(MovingShot.SpeedShare(false, 0.1f, 0.5f, 0.2f), Is.EqualTo(0.75f).Within(1e-5f));
            Assert.That(MovingShot.SpeedShare(false, 0.2f, 0.5f, 0.2f), Is.EqualTo(1f));
            Assert.That(MovingShot.SpeedShare(false, 3f, 0.5f, 0.2f), Is.EqualTo(1f));
        }

        [Test]
        public void WithoutAMovingShot_TheSpeedIsFull()
        {
            Assert.That(MovingShot.SpeedShare(false, -1f, 0.5f, 0.2f), Is.EqualTo(1f));
        }

        [Test]
        public void NoRecoveryTime_MeansFullSpeedTheMomentTheShotGoesOff()
        {
            Assert.That(MovingShot.SpeedShare(false, 0f, 0.5f, 0f), Is.EqualTo(1f));
        }
    }

    public class DashTests
    {
        static readonly FeelValues Feel = new FeelValues();

        [Test]
        public void CooldownBlocksSecondDashUntilItExpires()
        {
            var dash = new Dash();
            Assert.That(dash.TryStart(5f, Feel), Is.True);
            Assert.That(dash.TryStart(5.1f, Feel), Is.False);
            Assert.That(dash.TryStart(14.99f, Feel), Is.False);
            Assert.That(dash.CooldownRemaining(10f), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(dash.TryStart(15f, Feel), Is.True);
        }

        [Test]
        public void ANewDashIsReadyAtOnce()
        {
            var dash = new Dash();
            Assert.That(dash.CanStart(0f), Is.True);
            Assert.That(dash.CooldownRemaining(0f), Is.EqualTo(0f));
            Assert.That(dash.IsDashing(0f), Is.False);
            Assert.That(dash.IsInvulnerable(0f), Is.False);
        }

        [Test]
        public void DashLastsItsDurationAndProgressRuns0To1()
        {
            var dash = new Dash();
            dash.TryStart(1f, Feel);
            Assert.That(dash.IsDashing(1f), Is.True);
            Assert.That(dash.Progress(1.1f), Is.EqualTo(0.5f).Within(1e-4f));
            Assert.That(dash.IsDashing(1.19f), Is.True);
            Assert.That(dash.IsDashing(1.21f), Is.False);
            Assert.That(dash.Progress(3f), Is.EqualTo(1f));
        }

        [Test]
        public void InvulnerableOnlyAtTheStartOfTheDash()
        {
            var dash = new Dash();
            dash.TryStart(2f, Feel);
            Assert.That(dash.IsInvulnerable(2.05f), Is.True);
            Assert.That(dash.IsInvulnerable(2.11f), Is.False);
            Assert.That(dash.IsDashing(2.11f), Is.True);
        }

        [Test]
        public void TuningChangesTheNextDashNotTheOneInFlight()
        {
            var feel = new FeelValues();
            var dash = new Dash();
            dash.TryStart(0f, feel);
            feel.DashDuration = 1f;
            feel.DashCooldown = 1f;
            Assert.That(dash.IsDashing(0.3f), Is.False, "the dash in flight keeps its 0.2 s");
            Assert.That(dash.CanStart(5f), Is.False, "and its 10 s cooldown");
            Assert.That(dash.TryStart(10f, feel), Is.True);
            Assert.That(dash.IsDashing(10.9f), Is.True, "the next dash uses the tuned duration");
            Assert.That(dash.CanStart(11f), Is.True, "and the tuned cooldown");
        }
    }

    public class ActionTimelineTests
    {
        static ActionTimeline Started(float at)
        {
            var action = new ActionTimeline();
            action.Start(at, 0.15f, 0.05f, 0.25f);
            return action;
        }

        [Test]
        public void PhasesFollowStartupActiveRecovery()
        {
            ActionTimeline action = Started(10f);
            Assert.That(action.Phase(10f), Is.EqualTo(ActionPhase.Startup));
            Assert.That(action.Phase(10.14f), Is.EqualTo(ActionPhase.Startup));
            Assert.That(action.Phase(10.16f), Is.EqualTo(ActionPhase.Active));
            Assert.That(action.Phase(10.21f), Is.EqualTo(ActionPhase.Recovery));
            Assert.That(action.Phase(10.44f), Is.EqualTo(ActionPhase.Recovery));
            Assert.That(action.Phase(10.46f), Is.EqualTo(ActionPhase.None));
            Assert.That(action.IsRunning(10.3f), Is.True);
            Assert.That(action.IsRunning(10.46f), Is.False);
        }

        [Test]
        public void NotRunningBeforeItIsStarted()
        {
            var action = new ActionTimeline();
            Assert.That(action.Phase(0f), Is.EqualTo(ActionPhase.None));
            Assert.That(action.ConsumeHit(100f), Is.False);
        }

        [Test]
        public void HitResolvesOnceWhenActiveIsReached()
        {
            ActionTimeline action = Started(0f);
            Assert.That(action.ConsumeHit(0.1f), Is.False, "still in startup");
            Assert.That(action.ConsumeHit(0.16f), Is.True);
            Assert.That(action.ConsumeHit(0.17f), Is.False, "only once");
        }

        [Test]
        public void ALongFrameThatSkipsActiveStillResolvesTheHit()
        {
            ActionTimeline action = Started(0f);
            Assert.That(action.ConsumeHit(0.4f), Is.True);
        }

        [Test]
        public void ACancelledActionNeverHitsAndIsOverAtOnce()
        {
            ActionTimeline action = Started(0f);
            action.Cancel();
            Assert.That(action.IsRunning(0.05f), Is.False);
            Assert.That(action.ConsumeHit(0.2f), Is.False);
        }

        [Test]
        public void CanBeStartedAgainAfterItEnds()
        {
            ActionTimeline action = Started(0f);
            action.ConsumeHit(0.2f);
            action.Start(1f, 0.15f, 0.05f, 0.25f);
            Assert.That(action.Phase(1.01f), Is.EqualTo(ActionPhase.Startup));
            Assert.That(action.ConsumeHit(1.2f), Is.True, "a new action has its own hit");
        }
    }

    public class InputBufferTests
    {
        [Test]
        public void AFreshPressIsTaken()
        {
            var buffer = new InputBuffer<string>();
            buffer.Store("skill", 1f);
            Assert.That(buffer.HasItem, Is.True);
            Assert.That(buffer.TryTake(1.1f, 0.15f, out string item), Is.True);
            Assert.That(item, Is.EqualTo("skill"));
            Assert.That(buffer.HasItem, Is.False);
        }

        [Test]
        public void AnOldPressIsDropped()
        {
            var buffer = new InputBuffer<string>();
            buffer.Store("skill", 1f);
            Assert.That(buffer.TryTake(1.2f, 0.15f, out _), Is.False);
            Assert.That(buffer.HasItem, Is.False, "dropped, not kept for later");
        }

        [Test]
        public void TheNewestPressReplacesTheOlder()
        {
            var buffer = new InputBuffer<string>();
            buffer.Store("skill", 1f);
            buffer.Store("dash", 1.3f);
            Assert.That(buffer.TryTake(1.4f, 0.15f, out string item), Is.True);
            Assert.That(item, Is.EqualTo("dash"));
        }

        [Test]
        public void EmptyAndClearedBuffersGiveNothing()
        {
            var buffer = new InputBuffer<string>();
            Assert.That(buffer.TryTake(0f, 0.15f, out _), Is.False);
            buffer.Store("skill", 1f);
            buffer.Clear();
            Assert.That(buffer.TryTake(1f, 0.15f, out _), Is.False);
        }
    }

    public class HealthTests
    {
        [Test]
        public void DamageIsAppliedAndClampedAtZero()
        {
            var health = new Health(100);
            Assert.That(health.TakeDamage(30), Is.EqualTo(30));
            Assert.That(health.Current, Is.EqualTo(70));
            Assert.That(health.TakeDamage(500), Is.EqualTo(70));
            Assert.That(health.IsDead, Is.True);
            Assert.That(health.TakeDamage(10), Is.EqualTo(0), "the dead take no damage");
        }

        [Test]
        public void ZeroAndNegativeDamageDoNothing()
        {
            var health = new Health(100);
            Assert.That(health.TakeDamage(0), Is.EqualTo(0));
            Assert.That(health.TakeDamage(-5), Is.EqualTo(0));
            Assert.That(health.Current, Is.EqualTo(100));
        }

        [Test]
        public void ResetRefills()
        {
            var health = new Health(100);
            health.TakeDamage(100);
            health.Reset();
            Assert.That(health.Current, Is.EqualTo(100));
            Assert.That(health.IsDead, Is.False);
        }
    }

    public class LineShotTests
    {
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);
        static readonly GroundPoint AimUp = new GroundPoint(0f, 5f);

        static bool Hits(GroundPoint target, float radius = 0.5f) =>
            LineShot.Hits(Origin, AimUp, 12f, 0.3f, target, radius);

        [Test]
        public void HitsATargetOnTheLine() => Assert.That(Hits(new GroundPoint(0f, 8f)), Is.True);

        [Test]
        public void TheAimPointOnlySetsTheDirection() =>
            Assert.That(Hits(new GroundPoint(0f, 11f)), Is.True, "past the aim point, inside the range");

        [Test]
        public void WidthAndTargetRadiusAddUp()
        {
            Assert.That(Hits(new GroundPoint(0.79f, 6f)), Is.True, "0.79 < 0.3 + 0.5");
            Assert.That(Hits(new GroundPoint(0.81f, 6f)), Is.False, "0.81 > 0.3 + 0.5");
        }

        [Test]
        public void ReachesATargetWhoseEdgeIsInRange()
        {
            Assert.That(Hits(new GroundPoint(0f, 12.7f)), Is.True, "edge at 12.2, within range plus half-width");
            Assert.That(Hits(new GroundPoint(0f, 12.9f)), Is.False);
        }

        [Test]
        public void MissesATargetBehindTheShooter() => Assert.That(Hits(new GroundPoint(0f, -2f)), Is.False);

        [Test]
        public void AShotWithNoDirectionHitsNothing() =>
            Assert.That(LineShot.Hits(Origin, Origin, 12f, 0.3f, new GroundPoint(0f, 0.2f), 0.5f), Is.False);
    }

    public class TargetingTests
    {
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);

        static TargetInfo At(float x, float z, bool alive = true) => new TargetInfo(new GroundPoint(x, z), 0.5f, alive);

        [Test]
        public void GapIsMeasuredToTheEdge()
        {
            Assert.That(Targeting.Gap(Origin, new GroundPoint(3f, 4f), 0.5f), Is.EqualTo(4.5f).Within(1e-4f));
            Assert.That(Targeting.Gap(Origin, new GroundPoint(0.2f, 0f), 0.5f), Is.EqualTo(0f), "inside the target");
        }

        [Test]
        public void InRangeWhenTheEdgeIsInRange()
        {
            Assert.That(Targeting.InRange(Origin, new GroundPoint(0f, 6.4f), 0.5f, 6f), Is.True);
            Assert.That(Targeting.InRange(Origin, new GroundPoint(0f, 6.6f), 0.5f, 6f), Is.False);
        }

        [Test]
        public void PicksTheNearestLivingTargetInRange()
        {
            var targets = new List<TargetInfo> { At(0f, 5f), At(2f, 0f), At(0f, 1f, alive: false), At(0f, 20f) };
            Assert.That(Targeting.NearestInRange(Origin, targets, 6f), Is.EqualTo(1));
        }

        [Test]
        public void NothingInRangeGivesMinusOne()
        {
            var targets = new List<TargetInfo> { At(0f, 20f), At(0f, 1f, alive: false) };
            Assert.That(Targeting.NearestInRange(Origin, targets, 6f), Is.EqualTo(-1));
            Assert.That(Targeting.NearestInRange(Origin, new List<TargetInfo>(), 6f), Is.EqualTo(-1));
        }

        [Test]
        public void ATieGoesToTheFirst()
        {
            var targets = new List<TargetInfo> { At(3f, 0f), At(-3f, 0f) };
            Assert.That(Targeting.NearestInRange(Origin, targets, 6f), Is.EqualTo(0));
        }
    }

    public class ActionTimelineHitResolvedTests
    {
        [Test]
        public void HitResolved_IsTrueOnlyAfterTheHitWasTaken()
        {
            var action = new ActionTimeline();
            Assert.That(action.HitResolved, Is.False);
            action.Start(0f, 0.15f, 0.05f, 0.25f);
            Assert.That(action.HitResolved, Is.False, "in startup");
            Assert.That(action.ConsumeHit(0.16f), Is.True);
            Assert.That(action.HitResolved, Is.True, "the shot has gone off");

            action.Start(1f, 0.15f, 0.05f, 0.25f);
            Assert.That(action.HitResolved, Is.False, "a new action starts over");
            action.ConsumeHit(1.2f);
            action.Cancel();
            Assert.That(action.HitResolved, Is.False, "a cancelled action has nothing behind it");
        }
    }

    public class PoseRuleTests
    {
        [Test]
        public void StandingIsIdle_AndMovingIsRun()
        {
            Assert.That(PoseRule.Pick(false, false, false, 0f), Is.EqualTo(CharacterPose.Idle));
            Assert.That(PoseRule.Pick(false, false, false, PoseRule.MoveThreshold), Is.EqualTo(CharacterPose.Idle));
            Assert.That(PoseRule.Pick(false, false, false, 6f), Is.EqualTo(CharacterPose.Run));
        }

        [Test]
        public void AShotWinsOverMoving()
        {
            Assert.That(PoseRule.Pick(false, true, false, 6f), Is.EqualTo(CharacterPose.Shoot));
        }

        [Test]
        public void ADashWinsOverEverything()
        {
            Assert.That(PoseRule.Pick(true, true, false, 6f), Is.EqualTo(CharacterPose.Dash));
            Assert.That(PoseRule.Pick(true, false, false, 0f), Is.EqualTo(CharacterPose.Dash));
        }

        [Test]
        public void TheDashAnimationRunsOnAfterTheDash_UnlessAShotStarts()
        {
            Assert.That(PoseRule.Pick(false, false, true, 6f), Is.EqualTo(CharacterPose.Dash));
            Assert.That(PoseRule.Pick(false, false, true, 0f), Is.EqualTo(CharacterPose.Dash));
            Assert.That(PoseRule.Pick(false, true, true, 0f), Is.EqualTo(CharacterPose.Shoot));
        }
    }
}
