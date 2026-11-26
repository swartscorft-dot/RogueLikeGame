using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The boss's intro cannot be skipped in its first 0.3 s (work order 140, part 4; the producer's choice, told to Kristian: with no
    /// grace a click as she walked in threw the intro away): the switch is on, the grace is 0.3 s of real time, and off it is 0, as work
    /// order 130 built it. Its play (a press inside the grace does nothing and is not remembered) is BossEntrancePlayTests'.
    /// </summary>
    public class BossEntranceGraceTests
    {
        [Test]
        public void On_TheGraceIsThreeTenths_Off_ItIsNone()
        {
            bool before = BossEntranceValues.GraceOn;
            try
            {
                Assert.That(before, Is.True, "on by default");
                var v = new BossEntranceValues();
                Assert.That(v.SkipGraceSeconds, Is.EqualTo(0.3f));
                Assert.That(v.SkipGrace, Is.EqualTo(0.3f));
                Assert.That(v.SkipGrace, Is.LessThan(v.TravelInSeconds), "over before the camera reaches the boss");
                BossEntranceValues.GraceOn = false;
                Assert.That(v.SkipGrace, Is.EqualTo(0f), "off: at once, as before");
                BossEntranceValues.GraceOn = true;
                v.SkipGraceSeconds = -1f;
                Assert.That(v.SkipGrace, Is.EqualTo(0f), "never below none");
            }
            finally
            {
                BossEntranceValues.GraceOn = before;
            }
        }
    }
}
