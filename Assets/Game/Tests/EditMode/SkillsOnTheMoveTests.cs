using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>Decision 0004: the slide when a skill is pressed on the move, and the ease back up after it.</summary>
    public class SkillSlideTests
    {
        const float Slowdown = 0.3f, Recovery = 0.25f, Run = 6f;

        [Test]
        public void TheSlide_StartsAtTheShareSheHad_AndEndsAtAStand()
        {
            Assert.That(SkillSlide.SlideShare(1f, 0f, Slowdown), Is.EqualTo(1f));
            Assert.That(SkillSlide.SlideShare(0.6f, 0f, Slowdown), Is.EqualTo(0.6f));
            Assert.That(SkillSlide.SlideShare(1f, Slowdown, Slowdown), Is.EqualTo(0f), "from full speed: standing after the slowdown");
            Assert.That(SkillSlide.SlideShare(1f, Slowdown * 3f, Slowdown), Is.EqualTo(0f), "and stays standing");
            Assert.That(SkillSlide.SlideShare(1f, Slowdown * 0.5f, Slowdown), Is.EqualTo(0.5f).Within(1e-5f), "half way down at half the time");
        }

        [Test]
        public void TheSlide_FallsEveryStep_WithNoKinkAtEitherEnd()
        {
            float before = SkillSlide.SlideShare(1f, 0f, Slowdown);
            const int Steps = 300;
            float dt = Slowdown / Steps;
            for (int i = 1; i <= Steps; i++)
            {
                float now = SkillSlide.SlideShare(1f, i * dt, Slowdown);
                Assert.That(now, Is.LessThanOrEqualTo(before), "never speeds up");
                before = now;
            }
            // Smoothstep: the slope is zero at the press and at the stand, so the first and last steps are tiny.
            Assert.That(1f - SkillSlide.SlideShare(1f, dt, Slowdown), Is.LessThan(0.001f), "no jump at the press");
            Assert.That(SkillSlide.SlideShare(1f, Slowdown - dt, Slowdown), Is.LessThan(0.001f), "no jump at the stand");
        }

        [Test]
        public void TheSlide_TakesLessTimeFromALowerSpeed()
        {
            Assert.That(SkillSlide.DownTime(1f, Slowdown), Is.EqualTo(Slowdown).Within(1e-6f));
            Assert.That(SkillSlide.DownTime(0.5f, Slowdown), Is.EqualTo(Slowdown * 0.5f).Within(1e-6f), "from half speed, half the time");
            Assert.That(SkillSlide.SlideShare(0.5f, Slowdown * 0.5f, Slowdown), Is.EqualTo(0f), "standing at half the slowdown");
            Assert.That(SkillSlide.SlideShare(0.5f, Slowdown * 0.25f, Slowdown), Is.EqualTo(0.25f).Within(1e-5f), "half way down at half its time");
            Assert.That(SkillSlide.SlideShare(0f, 0.1f, Slowdown), Is.EqualTo(0f), "standing stays standing");
        }

        [Test]
        public void ASlowdownOfZero_IsTheOldDeadStop()
        {
            Assert.That(SkillSlide.SlideShare(1f, 0f, 0f), Is.EqualTo(0f));
            Assert.That(SkillSlide.SlideShare(1f, 0.05f, 0f), Is.EqualTo(0f));
            Assert.That(SkillSlide.SlideDistance(1f, 1f, 0f, Run), Is.EqualTo(0f));
        }

        [Test]
        public void TheDistanceSlid_IsTheAreaUnderTheSlide()
        {
            Assert.That(SkillSlide.SlideDistance(1f, Slowdown, Slowdown, Run), Is.EqualTo(Run * Slowdown * 0.5f).Within(1e-5f),
                        "full speed to a stand: half of run speed times the slowdown");
            Assert.That(SkillSlide.SlideDistance(1f, 5f, Slowdown, Run), Is.EqualTo(Run * Slowdown * 0.5f).Within(1e-5f), "nothing after the stand");
            Assert.That(SkillSlide.SlideDistance(0.5f, 5f, Slowdown, Run), Is.EqualTo(Run * 0.5f * Slowdown * 0.5f * 0.5f).Within(1e-5f),
                        "from half speed: a quarter of the way");
            // Summed in small steps, the curve gives the same distance part way down.
            float sum = 0f, t = 0.15f;
            const int Steps = 3000;
            for (int i = 0; i < Steps; i++) sum += Run * SkillSlide.SlideShare(1f, (i + 0.5f) * t / Steps, Slowdown) * t / Steps;
            Assert.That(SkillSlide.SlideDistance(1f, t, Slowdown, Run), Is.EqualTo(sum).Within(1e-3f));
        }

        [Test]
        public void TheRise_EasesBackToFull_FasterFromAHigherSpeed()
        {
            Assert.That(SkillSlide.RiseShare(0f, 0f, Recovery), Is.EqualTo(0f));
            Assert.That(SkillSlide.RiseShare(0f, Recovery * 0.5f, Recovery), Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(SkillSlide.RiseShare(0f, Recovery, Recovery), Is.EqualTo(1f));
            Assert.That(SkillSlide.UpTime(0.5f, Recovery), Is.EqualTo(Recovery * 0.5f).Within(1e-6f), "from half speed, half the time");
            Assert.That(SkillSlide.RiseShare(0.5f, Recovery * 0.5f, Recovery), Is.EqualTo(1f));
            Assert.That(SkillSlide.IsRisen(0.5f, Recovery * 0.5f, Recovery), Is.True);
            Assert.That(SkillSlide.IsRisen(0f, Recovery * 0.5f, Recovery), Is.False);
            Assert.That(SkillSlide.RiseShare(0.2f, 0.1f, 0f), Is.EqualTo(1f), "a recovery of 0: full speed at once");
            float before = 0f;
            for (int i = 0; i <= 100; i++)
            {
                float now = SkillSlide.RiseShare(0f, i * Recovery / 100f, Recovery);
                Assert.That(now, Is.GreaterThanOrEqualTo(before), "never slows down");
                before = now;
            }
            Assert.That(SkillSlide.RiseShare(0f, Recovery / 100f, Recovery), Is.LessThan(0.001f), "no jump when she is let go");
        }
    }

    public class ArmHoldTests
    {
        const float Hold = 0.3f;

        [Test]
        public void AfterAShot_TheArmsStayUpForTheHold_ThenComeDown()
        {
            var hold = new ArmHold();
            Assert.That(hold.Update(0f, acting: true, Hold, cursorInFront: true, dashing: false), Is.False, "no hold while the shot goes");
            Assert.That(hold.Update(0.2f, acting: true, Hold, true, false), Is.False);
            Assert.That(hold.Update(0.25f, false, Hold, true, false), Is.True, "the shot ended after 0.2: held from there");
            Assert.That(hold.Update(0.49f, false, Hold, true, false), Is.True);
            Assert.That(hold.Update(0.51f, false, Hold, true, false), Is.False, "over a hold after the last frame of the shot");
        }

        [Test]
        public void ANewShotRenewsTheHold()
        {
            var hold = new ArmHold();
            hold.Update(0f, true, Hold, true, false);
            hold.Update(0.25f, false, Hold, true, false);
            hold.Update(0.28f, true, Hold, true, false);
            Assert.That(hold.Update(0.5f, false, Hold, true, false), Is.True, "counted from the second shot");
        }

        [Test]
        public void TheCursorLeavingTheFront_OrADash_EndsIt()
        {
            var hold = new ArmHold();
            hold.Update(0f, true, Hold, true, false);
            Assert.That(hold.Update(0.05f, false, Hold, cursorInFront: false, dashing: false), Is.False);
            Assert.That(hold.Update(0.1f, false, Hold, cursorInFront: true, dashing: false), Is.False, "and stays ended when it comes back");

            hold.Update(1f, true, Hold, true, false);
            Assert.That(hold.Update(1.05f, false, Hold, true, dashing: true), Is.False);
            Assert.That(hold.Update(1.1f, false, Hold, true, false), Is.False, "a dash ends it for good");
        }
    }

    public class FreeRecoveryRuleTests
    {
        [Test]
        public void ASkillIsCommittedUntilItsLastHit_AndItsRecoveryEndIsKnown()
        {
            var skill = new SkillDefinition { Input = SkillInput.Normal, Startup = 0.2f, Recovery = 0.3f, HitCount = 3, HitInterval = 0.1f };
            var cast = new SkillCast();
            Assert.That(cast.IsCommitted(0f), Is.False, "nothing runs");
            Assert.That(cast.RecoveryEndsAt, Is.EqualTo(float.NegativeInfinity));
            cast.Start(skill, 1f);
            Assert.That(cast.IsCommitted(1.1f), Is.True, "startup");
            Assert.That(cast.IsCommitted(1.3f), Is.True, "between the hits of the burst");
            Assert.That(cast.IsCommitted(1.41f), Is.False, "the last hit is at 1.4: the recovery is not committed");
            Assert.That(cast.IsRunning(1.41f), Is.True, "though the skill still runs");
            Assert.That(cast.RecoveryEndsAt, Is.EqualTo(1.7f).Within(1e-5f));
        }

        [Test]
        public void AHoldingSkill_IsCommittedForTheHold()
        {
            var skill = new SkillDefinition { Input = SkillInput.Holding, Startup = 0.2f, Recovery = 0.3f, HoldMax = 2f, TickInterval = 0.15f };
            var cast = new SkillCast();
            cast.Start(skill, 0f);
            Assert.That(cast.IsCommitted(1f), Is.True, "holding");
            cast.Release(1f);
            Assert.That(cast.IsCommitted(1.01f), Is.False, "let go: recovery");
            Assert.That(cast.RecoveryEndsAt, Is.EqualTo(1.3f).Within(1e-5f));
        }

        [Test]
        public void TheBuffer_CanBeLookedIntoWithoutTakingThePress()
        {
            var buffer = new InputBuffer<int>();
            Assert.That(buffer.TryPeek(out _), Is.False);
            buffer.Store(7, 0f);
            Assert.That(buffer.TryPeek(out int seen), Is.True);
            Assert.That(seen, Is.EqualTo(7));
            Assert.That(buffer.HasItem, Is.True, "still there");
            Assert.That(buffer.TryTake(0.1f, 0.15f, out int taken), Is.True);
            Assert.That(taken, Is.EqualTo(7));
        }

        [Test]
        public void TheBuffer_SaysWhetherThePressIsStillFresh_ExactlyAsTakingItWould()
        {
            var buffer = new InputBuffer<int>();
            Assert.That(buffer.IsFresh(0f, 0.15f), Is.False, "empty");
            buffer.Store(3, 1f);
            Assert.That(buffer.IsFresh(1.1f, 0.15f), Is.True);
            Assert.That(buffer.IsFresh(1.14f, 0.15f), Is.True, "just inside the window it still fires");
            Assert.That(buffer.IsFresh(1.2f, 0.15f), Is.False, "older than the window");
            Assert.That(buffer.HasItem, Is.True, "looking does not take it");
            Assert.That(buffer.TryTake(1.2f, 0.15f, out _), Is.False, "and taking it then drops it, as IsFresh said");
        }
    }
}
