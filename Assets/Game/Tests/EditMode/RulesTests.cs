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
            Assert.That(feel.BasicShotStartup, Is.EqualTo(0.08f));
            Assert.That(feel.BasicShotActive, Is.EqualTo(0.02f));
            Assert.That(feel.BasicShotRecovery, Is.EqualTo(0.125f));
            Assert.That(feel.BasicAttackRange, Is.EqualTo(6f));
            Assert.That(feel.AttackMoveClickRadius, Is.EqualTo(2f));
            Assert.That(feel.ShotFrontAngle, Is.EqualTo(60f));
            Assert.That(feel.MovingShotSpeedShare, Is.EqualTo(0.5f));
            Assert.That(feel.MovingShotSlowdown, Is.EqualTo(0.15f));
            Assert.That(feel.MovingShotRecovery, Is.EqualTo(0.5f));
            Assert.That(feel.ShootGaitFade, Is.EqualTo(0.15f));
            Assert.That(feel.HitStop, Is.EqualTo(0.05f));
            Assert.That(feel.BasicShotHitStop, Is.EqualTo(0.025f));
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
        const float Slow = 0.5f, Down = 0.15f, Up = 0.5f;

        static float Share(float from, float t, float firedAfter) => MovingShot.SpeedShare(from, t, firedAfter, Slow, Down, Up);

        [Test]
        public void TheSlowDown_StartsAtTheCurrentShare_AndReachesTheSlowShareAtTheSlowDownTime_NotBefore()
        {
            Assert.That(Share(1f, 0f, -1f), Is.EqualTo(1f).Within(1e-5f), "it starts where the speed was");
            Assert.That(Share(1f, Down * 0.5f, -1f), Is.EqualTo(0.75f).Within(1e-5f), "halfway in time, halfway down (smoothstep)");
            Assert.That(Share(1f, Down * 0.9f, -1f), Is.GreaterThan(Slow + 1e-4f), "not there before the slow-down time");
            Assert.That(Share(1f, Down, -1f), Is.EqualTo(Slow).Within(1e-5f), "there at the slow-down time");
        }

        [Test]
        public void TheSpeedUp_WaitsForTheShotToGoOff_AndForTheSlowDownToFinish()
        {
            // The shot goes off early (0.08 s), before the slow-down is over: no climb until 0.15 s.
            Assert.That(Share(1f, 0.12f, 0.08f), Is.GreaterThan(Slow), "still coming down");
            Assert.That(Share(1f, Down, 0.08f), Is.EqualTo(Slow).Within(1e-5f));
            // The shot has not gone off: it stays slow however long that takes.
            Assert.That(Share(1f, 1f, -1f), Is.EqualTo(Slow).Within(1e-5f));
            // The shot goes off late (0.3 s): the climb starts then.
            Assert.That(Share(1f, 0.29f, 0.3f), Is.EqualTo(Slow).Within(1e-5f));
            Assert.That(Share(1f, 0.31f, 0.3f), Is.GreaterThan(Slow));
        }

        [Test]
        public void TheSpeedUp_ReachesFullSpeedAfterTheRecoveryTime()
        {
            Assert.That(Share(1f, Down + Up * 0.5f, 0.08f), Is.EqualTo(0.75f).Within(1e-5f), "halfway in time, halfway up");
            Assert.That(Share(1f, Down + Up * 0.95f, 0.08f), Is.LessThan(1f));
            Assert.That(Share(1f, Down + Up, 0.08f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(Share(1f, 5f, 0.08f), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(MovingShot.IsOver(Down + Up + 1e-4f, 0.08f, Down, Up), Is.True);
            Assert.That(MovingShot.IsOver(Down + Up - 0.01f, 0.08f, Down, Up), Is.False);
        }

        [Test]
        public void TheCurve_GoesOneWayAtATime_WithoutJumps()
        {
            float last = Share(1f, 0f, 0.08f);
            bool rising = false;
            for (int i = 1; i <= 1000; i++)
            {
                float t = i * 0.001f;
                float s = Share(1f, t, 0.08f);
                Assert.That(Mathf.Abs(s - last), Is.LessThan(0.01f), $"no jump at {t:F3} s");
                if (s > last + 1e-6f) rising = true;
                if (rising) Assert.That(s, Is.GreaterThanOrEqualTo(last - 1e-6f), $"once rising it does not fall again, at {t:F3} s");
                else Assert.That(s, Is.LessThanOrEqualTo(last + 1e-6f), $"falling until it rises, at {t:F3} s");
                last = s;
            }
        }

        [Test]
        public void AChainedShot_EasesDownFromTheShareItHasThen()
        {
            float midClimb = Share(1f, Down + Up * 0.5f, 0.08f); // 0.75
            Assert.That(Share(midClimb, 0f, -1f), Is.EqualTo(midClimb).Within(1e-5f), "no jump at the new shot");
            Assert.That(Share(midClimb, Down * 0.5f, -1f), Is.EqualTo((midClimb + Slow) * 0.5f).Within(1e-5f));
            Assert.That(Share(midClimb, Down, -1f), Is.EqualTo(Slow).Within(1e-5f));
        }

        static class Mathf
        {
            public static float Abs(float x) => System.Math.Abs(x);
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

        [Test]
        public void WalkingWhileSlowedByAShot_ShowsTheShootingGait_ButADashStillWins()
        {
            Assert.That(PoseRule.Pick(false, true, false, 3f, shootingGait: true), Is.EqualTo(CharacterPose.ShootMove));
            Assert.That(PoseRule.Pick(false, false, false, 4f, shootingGait: true), Is.EqualTo(CharacterPose.ShootMove), "also between chained shots");
            Assert.That(PoseRule.Pick(true, false, false, 4f, shootingGait: true), Is.EqualTo(CharacterPose.Dash));
            Assert.That(PoseRule.Pick(false, true, false, 0f, shootingGait: true), Is.EqualTo(CharacterPose.Shoot), "standing still it is the shot");
        }

        [Test]
        public void MovingBackwardWhileSlowedByAShot_ShowsTheBackwardGait()
        {
            Assert.That(PoseRule.Pick(false, true, false, 3f, shootingGait: true, movingBackward: true), Is.EqualTo(CharacterPose.ShootBack));
            Assert.That(PoseRule.Pick(false, true, false, 3f, shootingGait: true, movingBackward: false), Is.EqualTo(CharacterPose.ShootMove));
            Assert.That(PoseRule.Pick(true, false, false, 4f, shootingGait: true, movingBackward: true), Is.EqualTo(CharacterPose.Dash), "a dash still wins");
            Assert.That(PoseRule.Pick(false, false, false, 4f, shootingGait: false, movingBackward: true), Is.EqualTo(CharacterPose.Run), "without a shot it is the run");
        }
    }

    public class ShotCommandsTests
    {
        [Test]
        public void OneCommand_IsAnsweredByOneShot_ThenNothingIsOwed()
        {
            var c = new ShotCommands();
            Assert.That(c.Owed, Is.False);
            c.Command();
            Assert.That(c.Owed, Is.True);
            c.ShotStarted(forTheCommand: true);
            Assert.That(c.ShotWentOff(), Is.True, "the shot answered the command");
            Assert.That(c.Owed, Is.False, "so nothing more is owed: no repeat");
        }

        [Test]
        public void ACommandDuringAShot_IsNotAnsweredByThatShot_ButWaitsForTheNext()
        {
            var c = new ShotCommands();
            c.Command();
            c.ShotStarted(true);
            c.Command(); // a click while the shot is going
            Assert.That(c.ShotWentOff(), Is.False, "the shot in progress was for the earlier click");
            Assert.That(c.Owed, Is.True, "the later click still waits for a shot of its own");
            c.ShotStarted(true);
            Assert.That(c.ShotWentOff(), Is.True);
            Assert.That(c.Owed, Is.False);
        }

        [Test]
        public void TenCommandsDuringOneShot_AreOneWaitingCommand()
        {
            var c = new ShotCommands();
            c.Command();
            c.ShotStarted(true);
            for (int i = 0; i < 10; i++) c.Command();
            Assert.That(c.ShotWentOff(), Is.False);
            c.ShotStarted(true);
            Assert.That(c.ShotWentOff(), Is.True, "one more shot answers all ten");
            Assert.That(c.Owed, Is.False);
        }

        [Test]
        public void ACancelledShot_AnswersNothing_AndClearDropsTheCommand()
        {
            var c = new ShotCommands();
            c.Command();
            c.ShotStarted(true);
            c.ShotCancelled();
            Assert.That(c.ShotWentOff(), Is.False, "a cancelled shot answers nothing");
            Assert.That(c.Owed, Is.True, "the command still waits");
            c.Clear();
            Assert.That(c.Owed, Is.False, "a move order, a dash or a skill drops it");
            c.ShotStarted(true);
            Assert.That(c.ShotWentOff(), Is.False, "and a later shot with no command answers nothing");
        }

        [Test]
        public void AShotNotStartedForTheCommand_DoesNotAnswerIt()
        {
            var c = new ShotCommands();
            c.Command();
            c.ShotStarted(forTheCommand: false); // a shot on the move
            Assert.That(c.ShotWentOff(), Is.False);
            Assert.That(c.Owed, Is.True);
        }

        [Test]
        public void ACommandMustWait_WhileAShotIsGoing_OrItsShotTimeIsNotOver()
        {
            Assert.That(ShotCommands.MustWait(true, 5f, 0f), Is.True, "a shot is going");
            Assert.That(ShotCommands.MustWait(false, 0.1f, 0.225f), Is.True, "the last shot's time is not over");
            Assert.That(ShotCommands.MustWait(false, 0.225f, 0.225f), Is.False, "exactly when it is over, the next may start");
            Assert.That(ShotCommands.MustWait(false, 1f, 0.225f), Is.False);
        }

        [Test]
        public void PendingOne_HoldsOne_ANewerReplacesIt_AndItNeverExpires()
        {
            var p = new PendingOne<string>();
            Assert.That(p.HasItem, Is.False);
            p.Store("A");
            p.Store("B");
            Assert.That(p.Item, Is.EqualTo("B"), "the newer one replaces the older");
            Assert.That(p.Take(), Is.EqualTo("B"));
            Assert.That(p.HasItem, Is.False, "taking it empties the slot");
            Assert.That(p.Take(), Is.Null);
            p.Store("C");
            p.Clear();
            Assert.That(p.HasItem, Is.False);
        }
    }
}
