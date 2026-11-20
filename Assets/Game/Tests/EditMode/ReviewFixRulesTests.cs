using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 026 (the review of 2026-11-20): what a run's clean slate puts back, as rules. The focus gauge and the
    /// dash's cooldown go back to their start: an empty gauge with no window, and a dash ready at once.
    /// </summary>
    public class ReviewFixRulesTests
    {
        [Test]
        public void TheFocusGauge_Cleared_IsEmpty_WithNoWindow_AndFillsAgainAsBefore()
        {
            var values = new IdentityValues();
            var gauge = new FocusGauge();
            gauge.Fill(values);
            Assert.That(gauge.TryActivate(10f, values), Is.True);
            gauge.Fill(values);
            Assert.That(gauge.IsEmpowered(11f), Is.True);

            gauge.Clear();
            Assert.That(gauge.Focus, Is.EqualTo(0f));
            Assert.That(gauge.IsEmpowered(11f), Is.False, "the window has ended");
            Assert.That(gauge.EmpoweredRemaining(11f), Is.EqualTo(0f));
            Assert.That(gauge.BarFill(11f, values), Is.EqualTo(0f));
            Assert.That(gauge.Add(5f, values, 11f), Is.EqualTo(5f), "it fills again at once: no window to wait out");
        }

        [Test]
        public void TheDash_Cleared_IsReadyAtOnce_AndNotDashing()
        {
            var feel = new FeelValues();
            var dash = new Dash();
            Assert.That(dash.TryStart(10f, feel), Is.True);
            Assert.That(dash.CooldownRemaining(10.01f), Is.GreaterThan(0f));

            dash.Clear();
            Assert.That(dash.CooldownRemaining(10.01f), Is.EqualTo(0f));
            Assert.That(dash.CanStart(10.01f), Is.True);
            Assert.That(dash.IsDashing(10.01f), Is.False);
            Assert.That(dash.IsInvulnerable(10.01f), Is.False);
            Assert.That(dash.TryStart(10.02f, feel), Is.True, "and it dashes again as before");
            Assert.That(dash.CooldownRemaining(10.03f), Is.EqualTo(feel.DashCooldown - 0.01f).Within(1e-4f));
        }
    }
}
