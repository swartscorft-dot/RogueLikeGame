using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The cooldown buffer for every skill (work order 066, 2026-11-21, Kristian: "Ja, alle ferdigheter"): a press at most
    /// the buffer before ready waits, earlier is refused, 0 refuses as this morning; it counts from the moment the skill
    /// is ready; it is dropped when she is held, dead, or a screen comes up.
    /// </summary>
    public class CooldownBufferRulesTests
    {
        [Test]
        public void TheValue_IsOneForAllSkills_025s_AndBackstepsOwnIsGone()
        {
            var kit = new KitValues();
            Assert.That(kit.CooldownBuffer, Is.EqualTo(0.25f));
            Assert.That(typeof(KitValues).GetField("OpeningMoveCooldownBuffer"), Is.Null, "Backstep's own value became the general one");
        }

        [Test]
        public void APress02sBeforeReady_Waits_03sBefore_IsRefused()
        {
            Assert.That(CooldownBufferRules.Waits(0.2f, 0.25f), Is.True);
            Assert.That(CooldownBufferRules.Waits(0.25f, 0.25f), Is.True, "the edge counts");
            Assert.That(CooldownBufferRules.Waits(0.001f, 0.25f), Is.True);
            Assert.That(CooldownBufferRules.Waits(0.3f, 0.25f), Is.False, "earlier: refused, as before");
            Assert.That(CooldownBufferRules.Waits(5f, 0.25f), Is.False);
        }

        [Test]
        public void ReadyIsNotWaiting_AndZeroRefusesAsThisMorning()
        {
            Assert.That(CooldownBufferRules.Waits(0f, 0.25f), Is.False, "ready (or a charge left): it starts, nothing waits");
            Assert.That(CooldownBufferRules.Waits(0.1f, 0f), Is.False, "0: refused");
            Assert.That(CooldownBufferRules.Waits(0.01f, 0f), Is.False);
            Assert.That(CooldownBufferRules.Waits(0.1f, -1f), Is.False);
        }

        [Test]
        public void AWaitingPress_CountsFromTheMomentTheSkillIsReady()
        {
            Assert.That(CooldownBufferRules.CountsFrom(10f, 10.2f), Is.EqualTo(10.2f));
            Assert.That(CooldownBufferRules.CountsFrom(10f, 9.9f), Is.EqualTo(10f), "never before the press");
        }

        [Test]
        public void AWaitingPress_IsDropped_WhenHeldDeadOrAScreenComesUp()
        {
            Assert.That(CooldownBufferRules.Kept(true, false, false, false), Is.True);
            Assert.That(CooldownBufferRules.Kept(false, false, false, false), Is.False, "dead");
            Assert.That(CooldownBufferRules.Kept(true, true, false, false), Is.False, "stunned or pulled");
            Assert.That(CooldownBufferRules.Kept(true, false, true, false), Is.False, "a choice");
            Assert.That(CooldownBufferRules.Kept(true, false, false, true), Is.False, "the pause menu or an end screen");
        }

        [Test]
        public void AWaitingPress_WaitsNoLongerThanTheBufferAndTheInputBuffer()
        {
            Assert.That(CooldownBufferRules.StillInTime(10f, 10.39f, 0.25f, 0.15f), Is.True);
            Assert.That(CooldownBufferRules.StillInTime(10f, 10.41f, 0.25f, 0.15f), Is.False);
        }
    }
}
