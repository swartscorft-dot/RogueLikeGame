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
            Assert.That(feel.BasicShotRange, Is.EqualTo(10f));
            Assert.That(feel.BasicShotHalfWidth, Is.EqualTo(0.5f));
            Assert.That(feel.ShotFrontAngle, Is.EqualTo(120f));
            Assert.That(feel.ShotTurnTime, Is.EqualTo(0.2f));
            Assert.That(feel.ShotTurnHold, Is.EqualTo(0.5f));
            Assert.That(feel.BackpedalSpeedShare, Is.EqualTo(0.5f));
            Assert.That(feel.MovingShotSpeedShare, Is.EqualTo(0.5f));
            Assert.That(feel.MovingShotSlowdown, Is.EqualTo(0.15f));
            Assert.That(feel.MovingShotRecovery, Is.EqualTo(0.5f));
            Assert.That(feel.ShootGaitFade, Is.EqualTo(0.15f));
            Assert.That(feel.SkillRecoveryFree, Is.True);
            Assert.That(feel.SkillSlowdown, Is.EqualTo(0.3f));
            Assert.That(feel.SlideSteering, Is.True);
            Assert.That(feel.SkillSpeedRecovery, Is.EqualTo(0.25f));
            Assert.That(feel.AimHold, Is.EqualTo(0.3f));
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

    public class BasicShotLineTests
    {
        static readonly GroundPoint Origin = new GroundPoint(0f, 0f);
        static readonly GroundPoint North = new GroundPoint(0f, 1f);
        const float Range = 10f, HalfWidth = 0.5f;

        static TargetInfo At(float x, float z, bool alive = true) => new TargetInfo(new GroundPoint(x, z), 0.5f, alive);

        [Test]
        public void HitsTheNearestTargetOnTheLine_NotOneBehindIt()
        {
            var targets = new List<TargetInfo> { At(0f, 7f), At(0f, 3f), At(0f, 9f) };
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, targets), Is.EqualTo(1), "the nearer one along the line, whatever the list order");
        }

        [Test]
        public void ATargetJustInsideTheHalfWidthIsHit_OneJustOutsideIsMissed()
        {
            // A target touches the line when its centre is within half-width + its radius (1 m here) of it.
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, new List<TargetInfo> { At(0.98f, 5f) }), Is.EqualTo(0), "just inside");
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, new List<TargetInfo> { At(1.02f, 5f) }), Is.EqualTo(-1), "just outside");
        }

        [Test]
        public void BeyondTheRange_IsAMiss()
        {
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, new List<TargetInfo> { At(0f, 9.9f) }), Is.EqualTo(0), "its edge within the range");
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, new List<TargetInfo> { At(0f, Range + 1.1f) }), Is.EqualTo(-1), "past the range and the half-width");
        }

        [Test]
        public void DeadTargets_AndTargetsBehindOrBeside_AreNotHit()
        {
            var targets = new List<TargetInfo> { At(0f, 3f, alive: false), At(0f, -4f), At(4f, 2f), At(0f, 6f) };
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, targets), Is.EqualTo(3), "the dead one, the one behind and the one beside are passed over");
            Assert.That(LineShot.FirstHit(Origin, North, Range, HalfWidth, new List<TargetInfo>()), Is.EqualTo(-1), "nothing to hit");
        }

        [Test]
        public void TheDirection_IsTowardThePoint_OrWhereSheFacesWhenThePointIsOnHer()
        {
            GroundPoint d = LineShot.Direction(Origin, new GroundPoint(3f, 4f), North);
            Assert.That(d.X, Is.EqualTo(0.6f).Within(1e-4f));
            Assert.That(d.Z, Is.EqualTo(0.8f).Within(1e-4f));
            GroundPoint onHer = LineShot.Direction(Origin, new GroundPoint(0.02f, 0.03f), new GroundPoint(-2f, 0f));
            Assert.That(onHer.X, Is.EqualTo(-1f).Within(1e-4f), "a point on her: where she faces");
            Assert.That(onHer.Z, Is.EqualTo(0f).Within(1e-4f));
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

    public class ClickToShootRuleTests
    {
        const float NextShotAt = 0.225f;

        [Test]
        public void AClickWhenFree_FiresAtOnce_AndNothingWaits()
        {
            var c = new ClickToShoot();
            Assert.That(c.Click(false, 0.3f, NextShotAt), Is.True);
            Assert.That(c.Waiting, Is.False, "one click, one shot: nothing left to repeat");
            Assert.That(c.TakeWhenAllowed(false, 1f, NextShotAt), Is.False);
        }

        [Test]
        public void AClickDuringTheCycle_Waits_AndFiresWhenTheCycleAllows_NotSooner()
        {
            var c = new ClickToShoot();
            Assert.That(c.Click(false, 0.05f, NextShotAt), Is.False, "the last shot's time is not over");
            Assert.That(c.Waiting, Is.True);
            Assert.That(c.TakeWhenAllowed(false, 0.2f, NextShotAt), Is.False, "not sooner");
            Assert.That(c.TakeWhenAllowed(false, NextShotAt, NextShotAt), Is.True, "exactly when the cycle allows");
            Assert.That(c.Waiting, Is.False);
            Assert.That(c.TakeWhenAllowed(false, 1f, NextShotAt), Is.False, "and only once");
        }

        [Test]
        public void TenClicksDuringOneCycle_AreOneWaitingClick()
        {
            var c = new ClickToShoot();
            for (int i = 0; i < 10; i++) c.Click(true, 0.01f * i, NextShotAt);
            Assert.That(c.TakeWhenAllowed(false, 1f, NextShotAt), Is.True);
            Assert.That(c.TakeWhenAllowed(false, 1f, NextShotAt), Is.False, "ten clicks, one waiting shot");
        }

        [Test]
        public void WhileBusy_TheClickWaits_AndClearDropsIt()
        {
            var c = new ClickToShoot();
            Assert.That(c.Click(true, 5f, NextShotAt), Is.False, "busy: a shot, a skill or a dash is going");
            Assert.That(c.TakeWhenAllowed(true, 5f, NextShotAt), Is.False, "still busy");
            c.Clear();
            Assert.That(c.Waiting, Is.False, "a move order, a dash or a skill drops it");
            Assert.That(c.TakeWhenAllowed(false, 5f, NextShotAt), Is.False);
        }

        [Test]
        public void MustWait_WhileBusy_OrUntilTheShotTimeIsOver()
        {
            Assert.That(ClickToShoot.MustWait(true, 5f, 0f), Is.True);
            Assert.That(ClickToShoot.MustWait(false, 0.1f, NextShotAt), Is.True);
            Assert.That(ClickToShoot.MustWait(false, NextShotAt, NextShotAt), Is.False);
        }
    }

    public class ShotFacingTests
    {
        const float Hold = 0.5f;

        [Test]
        public void AShotThatTurnsTheBody_StartsTheHold_AShotThatDoesNot_LeavesItFree()
        {
            var f = new ShotFacing();
            f.ShotStarted(turnsBody: false, now: 0f, hold: Hold);
            Assert.That(f.State, Is.EqualTo(ShotFacing.Mode.Free), "a shot inside the front angle does not turn the body");
            f.ShotStarted(turnsBody: true, now: 1f, hold: Hold);
            Assert.That(f.State, Is.EqualTo(ShotFacing.Mode.Held));
            Assert.That(f.HoldUntil, Is.EqualTo(1.5f).Within(1e-5f));
        }

        [Test]
        public void TheHold_LastsItsTimeAfterTheLastShot_AndAShotWithinItRenewsIt()
        {
            var f = new ShotFacing();
            f.ShotStarted(true, 0f, Hold);
            Assert.That(f.Update(0.4f, true, false, false), Is.EqualTo(ShotFacing.Mode.Held));
            f.ShotStarted(false, 0.45f, Hold); // a rhythm click, even with the aim inside the front angle of the turned body
            Assert.That(f.Update(0.9f, true, false, false), Is.EqualTo(ShotFacing.Mode.Held), "renewed: still held 0.9 s after the first shot");
            Assert.That(f.Update(0.96f, true, false, false), Is.EqualTo(ShotFacing.Mode.TurningBack), "0.5 s after the last shot it turns back");
        }

        [Test]
        public void WhenTheHoldEnds_MovingSheTurnsBack_StandingSheStaysAsSheIs()
        {
            var moving = new ShotFacing();
            moving.ShotStarted(true, 0f, Hold);
            Assert.That(moving.Update(0.6f, true, false, false), Is.EqualTo(ShotFacing.Mode.TurningBack));
            Assert.That(moving.Update(0.65f, true, false, false), Is.EqualTo(ShotFacing.Mode.TurningBack), "until she faces her run");
            Assert.That(moving.Update(0.7f, true, false, true), Is.EqualTo(ShotFacing.Mode.Free), "facing the run: free");

            var standing = new ShotFacing();
            standing.ShotStarted(true, 0f, Hold);
            Assert.That(standing.Update(0.6f, false, false, false), Is.EqualTo(ShotFacing.Mode.Free), "no run to turn back to");
        }

        [Test]
        public void TheAimBackInsideTheFrontOfTheRun_EndsTheHold()
        {
            var f = new ShotFacing();
            f.ShotStarted(true, 0f, Hold);
            Assert.That(f.Update(0.1f, true, aimInsideRunFront: true, facingRun: false), Is.EqualTo(ShotFacing.Mode.TurningBack));
        }

        [Test]
        public void FromOutside_AMoveOrderTurnsBackAtTheRate_ADashOrSkillEndsItAtOnce()
        {
            var f = new ShotFacing();
            f.ShotStarted(true, 0f, Hold);
            f.End(turnBack: true);
            Assert.That(f.State, Is.EqualTo(ShotFacing.Mode.TurningBack), "a move order");
            f.ShotStarted(true, 0.1f, Hold);
            f.End(turnBack: false);
            Assert.That(f.State, Is.EqualTo(ShotFacing.Mode.Free), "a dash or a skill");
        }

        [Test]
        public void TheTurn_IsLimitedByTheRate_TheShorterWayRound()
        {
            Assert.That(ShotFacing.Turn(0f, 180f, 0.2f, 0.1f), Is.EqualTo(90f).Within(1e-3f), "half a half turn in half the time");
            Assert.That(ShotFacing.Turn(170f, -170f, 0.2f, 0.01f), Is.EqualTo(179f).Within(1e-3f), "through 180, not back through 0");
            Assert.That(ShotFacing.Turn(10f, 20f, 0.2f, 0.1f), Is.EqualTo(20f).Within(1e-3f), "never past the target");
            Assert.That(ShotFacing.Turn(0f, 135f, 0f, 0.001f), Is.EqualTo(135f).Within(1e-3f), "no turn time: at once");
            Assert.That(ShotFacing.DeltaAngle(350f, 10f), Is.EqualTo(20f).Within(1e-3f));
        }
    }

    public class BackpedalCapTests
    {
        [Test]
        public void Off_ItAllowsFullSpeed()
        {
            var c = new BackpedalCap();
            Assert.That(c.Update(false, 1f, 0.5f, 0.15f, 0.5f), Is.EqualTo(1f));
        }

        [Test]
        public void On_ItEasesDownToTheCap_NotInAJump()
        {
            var c = new BackpedalCap();
            c.Update(false, 0f, 0.5f, 0.15f, 0.5f);
            Assert.That(c.Update(true, 1f, 0.5f, 0.15f, 0.5f), Is.EqualTo(1f).Within(1e-4f), "at the moment it comes on: still full");
            float mid = c.Update(true, 1.075f, 0.5f, 0.15f, 0.5f);
            Assert.That(mid, Is.EqualTo(0.75f).Within(1e-3f), "half way in time, half way down");
            Assert.That(c.Update(true, 1.2f, 0.5f, 0.15f, 0.5f), Is.EqualTo(0.5f).Within(1e-4f), "then the cap");
        }

        [Test]
        public void Off_AgainItEasesBackUp_FromWhereItWas()
        {
            var c = new BackpedalCap();
            c.Update(true, 0f, 0.5f, 0.15f, 0.5f);
            c.Update(true, 1f, 0.5f, 0.15f, 0.5f);
            Assert.That(c.Update(false, 2f, 0.5f, 0.15f, 0.5f), Is.EqualTo(0.5f).Within(1e-4f), "at the moment it goes off: still capped");
            Assert.That(c.Update(false, 2.25f, 0.5f, 0.15f, 0.5f), Is.EqualTo(0.75f).Within(1e-3f));
            Assert.That(c.Update(false, 2.6f, 0.5f, 0.15f, 0.5f), Is.EqualTo(1f).Within(1e-4f));
        }

        [Test]
        public void Reset_TakesItOffAtOnce()
        {
            var c = new BackpedalCap();
            c.Update(true, 0f, 0.5f, 0f, 0.5f);
            Assert.That(c.Share, Is.EqualTo(0.5f));
            c.Reset();
            Assert.That(c.Share, Is.EqualTo(1f));
        }
    }
}
