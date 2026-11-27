using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 142: a recast press is answered as clean by its judged timing, the rewarded press always; the switch off, by
    /// the rewarded press alone, as before. The cast bar's flash reads it (<see cref="CastBarRules.OfPress"/>).
    /// </summary>
    public class CleanPressAnswersTests
    {
        [TearDown]
        public void PutBack() => CleanPressAnswers.On = true;

        [Test]
        public void AQuickPressInItsCleanPart_IsAnsweredClean_ThoughNotRewarded()
        {
            Assert.That(CleanPressAnswers.AnsweredClean(false, RecastTiming.Clean), Is.True, "Triple Tap's second press, timed clean");
            Assert.That(CastBarRules.OfPress(CleanPressAnswers.AnsweredClean(false, RecastTiming.Clean)), Is.EqualTo(CastBarFlash.Clean));
        }

        [Test]
        public void TheRewardedPress_IsAnsweredClean_AndNoPressOffItsCleanPartIs()
        {
            Assert.That(CleanPressAnswers.AnsweredClean(true, RecastTiming.Clean), Is.True);
            foreach (RecastTiming t in new[] { RecastTiming.Early, RecastTiming.Late, RecastTiming.Waited, RecastTiming.Plain, RecastTiming.None, RecastTiming.Missed })
                Assert.That(CleanPressAnswers.AnsweredClean(false, t), Is.False, t.ToString());
        }

        [Test]
        public void TheSwitchOff_OnlyTheRewardedPress_AsBefore()
        {
            CleanPressAnswers.On = false;
            Assert.That(CleanPressAnswers.AnsweredClean(false, RecastTiming.Clean), Is.False);
            Assert.That(CleanPressAnswers.AnsweredClean(true, RecastTiming.Clean), Is.True);
        }
    }
}
