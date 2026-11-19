using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 011: the table of the enemies' models, how an attack clip is timed against the game's windup and
    /// recovery, and which clip the legs play for a ground speed.
    /// </summary>
    public class EnemyModelsTests
    {
        const double Frame = 1.0 / EnemyModelSpec.FramesPerSecond;

        static IEnumerable<(EnemyKind kind, EnemyAttackDefinition attack)> EveryAttack()
        {
            foreach (EnemyAttackDefinition a in EnemyValues.DefaultAttacks()) yield return (EnemyKind.Brute, a);
            foreach (EnemyAttackDefinition a in new SwarmerValues().Enemy.Attacks) yield return (EnemyKind.Swarmer, a);
            foreach (EnemyAttackDefinition a in new ShooterValues().Enemy.Attacks) yield return (EnemyKind.Shooter, a);
            foreach (EnemyAttackDefinition a in new GuardValues().Enemy.Attacks) yield return (EnemyKind.Guard, a);
        }

        // ---------------- The table ----------------

        [Test]
        public void EveryAttackOfEveryKind_HasAClipWithAStrike()
        {
            var expected = new Dictionary<string, string>
            {
                ["Brute Slam"] = "Slam", ["Brute Shockwave"] = "Attack", ["Swarmer Lunge"] = "Attack",
                ["Shooter Shot"] = "Shoot", ["Guard Slam"] = "Slam", ["Guard Sweep"] = "Sweep",
            };
            foreach ((EnemyKind kind, EnemyAttackDefinition attack) in EveryAttack())
            {
                EnemyClipSpec clip = EnemyModelSpec.Of(kind).ClipForAttack(attack.Name);
                Assert.That(clip, Is.Not.Null, kind + " " + attack.Name);
                Assert.That(clip.Name, Is.EqualTo(expected[kind + " " + attack.Name]));
                Assert.That(clip.Strike, Is.GreaterThan(0).And.LessThan(clip.Length));
                Assert.That(clip.Loop, Is.False);
            }
        }

        [Test]
        public void EveryModel_HasIdle_ItsWalk_Run_AndDeath()
        {
            foreach (EnemyModelSpec spec in EnemyModelSpec.All)
            {
                foreach (string clip in new[] { "Idle", spec.Walk, "Run", "Death" })
                    Assert.That(spec.Clip(clip), Is.Not.Null, spec.Name + " " + clip);
                Assert.That(spec.WalkStride, Is.GreaterThan(0).And.LessThan(spec.RunStride));
                Assert.That(spec.Clip("Stagger") != null, Is.EqualTo(spec.HasStagger));
            }
            Assert.That(EnemyModelSpec.Swarmer.Walk, Is.EqualTo("CrouchWalk"));
            Assert.That(EnemyModelSpec.Shooter.ShotLeftOfFacing, Is.EqualTo(98));
        }

        [Test]
        public void TakeNames_AreMatchedByTheirWholeLastPart()
        {
            Assert.That(EnemyModelSpec.TakeName("Armature|Idle"), Is.EqualTo("Idle"));
            Assert.That(EnemyModelSpec.IsTake("Armature|Walk", "Walk"), Is.True);
            Assert.That(EnemyModelSpec.IsTake("Armature|CrouchWalk", "Walk"), Is.False, "the end of the name is its whole last part");
            Assert.That(EnemyModelSpec.IsTake("Armature|ShootFull", "Shoot"), Is.False);
            Assert.That(EnemyModelSpec.IsTake("Death", "Death"), Is.True);
            Assert.That(EnemyModelSpec.IsTake("mixamorig:LeftHand", "LeftHand"), Is.True);
        }

        // ---------------- The attack clip against the game's timing ----------------

        [Test]
        public void EveryAttack_ReachesItsClipsStrike_JustAsTheWindupEnds()
        {
            foreach ((EnemyKind kind, EnemyAttackDefinition attack) in EveryAttack())
            {
                EnemyClipSpec clip = EnemyModelSpec.Of(kind).ClipForAttack(attack.Name);
                AttackClipPlan plan = AttackClipPlan.For(attack.Windup, attack.Recovery, clip.Strike, clip.Length);
                string what = kind + " " + attack.Name;
                Assert.That(plan.InWindup(attack.Windup), Is.EqualTo(clip.Strike).Within(1e-9), what);
                Assert.That(plan.InWindup(0), Is.EqualTo(plan.Start).Within(1e-9), what);
                Assert.That(plan.Start, Is.InRange(0, clip.Strike), what);
                Assert.That(plan.WindupSpeed, Is.InRange(0.5, 2.0), what);
                Assert.That(plan.RecoverySpeed, Is.InRange(0.5, 2.0), what);
                Assert.That(plan.AfterStrike(0), Is.EqualTo(clip.Strike).Within(1e-9), what);
                // A frame before the end of the windup, the clip is a frame's worth of its own time before its strike, or less.
                Assert.That(clip.Strike - plan.InWindup(attack.Windup - 1.0 / 60), Is.LessThanOrEqualTo(Frame + 1e-9), what);
            }
        }

        [Test]
        public void AShortWindup_StartsTheClipPartWayIn_AtDoubleSpeed()
        {
            // The swarmer's lunge: 0.4 s to a strike 1.233 s into the clip.
            AttackClipPlan plan = AttackClipPlan.For(0.4, 0.6, 1.233, 2.467);
            Assert.That(plan.WindupSpeed, Is.EqualTo(2.0));
            Assert.That(plan.Start, Is.EqualTo(1.233 - 0.8).Within(1e-9));
            Assert.That(plan.Hold, Is.EqualTo(0));
            Assert.That(plan.InWindup(0.2), Is.EqualTo(1.233 - 0.4).Within(1e-9));
            Assert.That(plan.RecoverySpeed, Is.EqualTo(2.0), "1.234 s of clip in 0.6 s would be faster than double");
        }

        [Test]
        public void AWindupThatFitsTheClip_PlaysItFromTheStart_AtTheSpeedThatLandsTheStrike()
        {
            // The brute's slam: 0.9 s to a strike 1.667 s in, and 1 s of recovery for the 1.333 s after it.
            AttackClipPlan plan = AttackClipPlan.For(0.9, 1.0, 1.667, 3.0);
            Assert.That(plan.Start, Is.EqualTo(0));
            Assert.That(plan.WindupSpeed, Is.EqualTo(1.667 / 0.9).Within(1e-9));
            Assert.That(plan.RecoverySpeed, Is.EqualTo(1.333).Within(1e-9));
            Assert.That(plan.AfterStrike(1.0), Is.EqualTo(3.0).Within(1e-9), "the clip ends with the recovery");
            Assert.That(plan.AfterStrike(5.0), Is.EqualTo(3.0), "and stays on its last frame");
        }

        [Test]
        public void ALongWindup_HoldsTheFirstFrame_ThenPlaysAtHalfSpeed()
        {
            AttackClipPlan plan = AttackClipPlan.For(3.0, 4.0, 0.5, 1.0);
            Assert.That(plan.WindupSpeed, Is.EqualTo(0.5));
            Assert.That(plan.Start, Is.EqualTo(0));
            Assert.That(plan.Hold, Is.EqualTo(2.0).Within(1e-9));
            Assert.That(plan.InWindup(1.0), Is.EqualTo(0));
            Assert.That(plan.InWindup(2.5), Is.EqualTo(0.25).Within(1e-9));
            Assert.That(plan.InWindup(3.0), Is.EqualTo(0.5).Within(1e-9));
            Assert.That(plan.RecoverySpeed, Is.EqualTo(0.5), "0.5 s of clip over 4 s would be slower than half");
        }

        [Test]
        public void NoWindup_StartsOnTheStrike()
        {
            AttackClipPlan plan = AttackClipPlan.For(0, 0, 1.0, 2.0);
            Assert.That(plan.InWindup(0), Is.EqualTo(1.0));
            Assert.That(plan.RecoverySpeed, Is.EqualTo(2.0));
        }

        // ---------------- The legs ----------------

        [Test]
        public void Gait_StandsWalksAndRuns_WithAnOverlapAtEachChange()
        {
            const double walk = 1.5, run = 4.0;
            Assert.That(EnemyGaits.For(0.0, EnemyGait.Stand, walk, run), Is.EqualTo(EnemyGait.Stand));
            Assert.That(EnemyGaits.For(0.2, EnemyGait.Stand, walk, run), Is.EqualTo(EnemyGait.Stand), "a standing one does not shuffle");
            Assert.That(EnemyGaits.For(0.2, EnemyGait.Walk, walk, run), Is.EqualTo(EnemyGait.Walk), "a walking one does not stop on a slow frame");
            Assert.That(EnemyGaits.For(0.1, EnemyGait.Walk, walk, run), Is.EqualTo(EnemyGait.Stand));
            Assert.That(EnemyGaits.For(1.5, EnemyGait.Stand, walk, run), Is.EqualTo(EnemyGait.Walk));
            Assert.That(EnemyGaits.For(2.5, EnemyGait.Walk, walk, run), Is.EqualTo(EnemyGait.Walk), "up to 1.7 times its stride it walks");
            Assert.That(EnemyGaits.For(2.6, EnemyGait.Walk, walk, run), Is.EqualTo(EnemyGait.Run));
            Assert.That(EnemyGaits.For(2.3, EnemyGait.Run, walk, run), Is.EqualTo(EnemyGait.Run), "running, it walks again under 1.5 times");
            Assert.That(EnemyGaits.For(2.2, EnemyGait.Run, walk, run), Is.EqualTo(EnemyGait.Walk));
            Assert.That(EnemyGaits.For(5.0, EnemyGait.Walk, walk, 0), Is.EqualTo(EnemyGait.Walk), "without a run it walks");
        }

        [Test]
        public void Gait_PlaysAtTheSpeedOfItsStride_BetweenHalfAndThreeTimes()
        {
            Assert.That(EnemyGaits.Rate(3.0, 1.5), Is.EqualTo(2.0));
            Assert.That(EnemyGaits.Rate(1.2, 1.5), Is.EqualTo(0.8).Within(1e-9));
            Assert.That(EnemyGaits.Rate(0.1, 1.5), Is.EqualTo(0.5));
            Assert.That(EnemyGaits.Rate(9.0, 1.5), Is.EqualTo(3.0));
        }

        [Test]
        public void EveryKind_GoesAtItsSpeed_InAGaitThatSuitsIt()
        {
            var kinds = new (EnemyKind kind, double speed, EnemyGait gait)[]
            {
                (EnemyKind.Brute, new EnemyValues().MoveSpeed, EnemyGait.Run),
                (EnemyKind.Swarmer, new SwarmerValues().Enemy.MoveSpeed, EnemyGait.Run),
                (EnemyKind.Shooter, new ShooterValues().Enemy.MoveSpeed, EnemyGait.Run),
                (EnemyKind.Guard, new GuardValues().Enemy.MoveSpeed, EnemyGait.Walk),
            };
            foreach ((EnemyKind kind, double speed, EnemyGait gait) in kinds)
            {
                EnemyModelSpec spec = EnemyModelSpec.Of(kind);
                Assert.That(EnemyGaits.For(speed, EnemyGait.Walk, spec.WalkStride, spec.RunStride), Is.EqualTo(gait), kind.ToString());
            }
        }
    }
}
