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

    /// <summary>
    /// Work order 047 (the third review of 2026-11-20), as rules: a projectile's last partial step and the time it reports;
    /// the recast clock through a hitch in a hit stop; and where a floor's reward can land, over a thousand seeds.
    /// </summary>
    public class ReviewFix3RulesTests
    {
        static Projectile ArrowAt(float x, float flown)
        {
            Projectile p = Projectile.Make(ProjectileKind.Arrow, new GroundPoint(0f, 0f), new GroundPoint(1f, 0f), 10f, 0.1f, 0f, 10f);
            p.At = new GroundPoint(x, 0f);
            p.Flown = flown;
            return p;
        }

        [Test]
        public void AProjectilesLastPartialStep_SweepsHerOnlyAsFarAsItFlies()
        {
            // 3 cm of flight left in a 16 ms step at 10 m/s: it flies for 3 ms of the step. She comes down onto its end over
            // the whole step and is there only at its end, long after it ended.
            Projectile p = ArrowAt(9.97f, 9.97f);
            ProjectileStep step = ProjectileRules.Step(ref p, 0.016f, new GroundPoint(10f, 3f), new GroundPoint(10f, 0f), 0.5f, null);
            Assert.That(step.Hit, Is.Not.EqualTo(ProjectileHit.MetHer), "she arrived after it ended");
            Assert.That(step.Hit, Is.EqualTo(ProjectileHit.FlightOver));
        }

        [Test]
        public void AProjectilesLastPartialStep_TellsWhenItMetHer_InTheWholeStepsTime()
        {
            // 10 cm left in a 20 ms step at 10 m/s: it flies for the first half of the step. She stands 0.6 m past the
            // point 5 cm on: it meets her halfway through its flight, a quarter of the way through the step.
            Projectile p = ArrowAt(9.9f, 9.9f);
            var her = new GroundPoint(10.55f, 0f);
            ProjectileStep step = ProjectileRules.Step(ref p, 0.02f, her, her, 0.5f, null);
            Assert.That(step.Hit, Is.EqualTo(ProjectileHit.MetHer));
            Assert.That(step.Fraction, Is.EqualTo(0.25f).Within(2e-3f), "the share of the whole step, as the caller takes it");
        }

        [Test]
        public void TheRecastClock_ThroughAHitchInAHitStop_AddsNoMoreThanATenthOfASecond()
        {
            Assert.That(RecastClock.HitStopSeconds(true, 0f, 0.016f, true, false, 1f), Is.EqualTo(0.016f).Within(1e-6f), "an ordinary frame as before");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, 2f, true, false, 1f), Is.EqualTo(0.1f).Within(1e-6f), "a hitch of 2 s adds 0.1 s");
            Assert.That(RecastClock.HitStopSeconds(true, 0f, 2f, true, false, 0.5f), Is.EqualTo(0.05f).Within(1e-6f), "at the pace it found");
            Assert.That(RecastClock.HitStopSeconds(false, 0f, 2f, true, false, 1f), Is.EqualTo(0f), "off, nothing");
        }

        [Test]
        public void AMoveKey_GoingDown_IsANewPress_OneLetGoIsNot()
        {
            Assert.That(QuickDuckRules.MoveKeyWentDown(1f, 1f, 0f, 1f), Is.False, "D let go, W held");
            Assert.That(QuickDuckRules.MoveKeyWentDown(0f, 1f, 1f, 1f), Is.True, "D down again");
            Assert.That(QuickDuckRules.MoveKeyWentDown(0f, 0f, 0f, 1f), Is.True, "W from nothing");
            Assert.That(QuickDuckRules.MoveKeyWentDown(1f, 0f, -1f, 0f), Is.True, "D let go and A down in one frame");
            Assert.That(QuickDuckRules.MoveKeyWentDown(1f, 1f, 1f, 1f), Is.False, "held on");
            Assert.That(QuickDuckRules.MoveKeyWentDown(1f, 1f, 0f, 0f), Is.False, "all let go");
        }

        [Test]
        public void AFloorsReward_AlwaysHasGroundToLandOn_TheExitRoomsMiddleIsClear_OverAThousandSeeds()
        {
            // The drop starts from where the last enemy fell, or from the room's middle; only when the middle cannot be
            // stood on does GeneratedLevels fall back to the nearest walkable ground unchecked. The middle stands clear of
            // every prop and wall by the drop's clearance and her own radius (the NavMesh's edge lies about that far in).
            float need = new RewardDropValues().Clearance + Placeholders.PlayerRadius;
            double worstExit = double.MaxValue, worstElite = double.MaxValue;
            string worstExitAt = "", worstEliteAt = "";
            for (ulong s = 0; s < 1000; s++)
            {
                int floor = 1 + (int)(s % 5);
                LevelLayout level = LevelGenerator.Generate(s, floor, new LevelValues());
                foreach (LevelRoom room in new[] { level.ExitRoom, level.EliteRoom })
                {
                    if (room == null) continue;
                    LevelPoint middle = room.Middle(level.CellSize);
                    double clear = LevelGeometry.DistanceToWalls(middle, room.Rect, level.CellSize);
                    foreach (LevelProp prop in room.Props) clear = System.Math.Min(clear, prop.Footprint.DistanceTo(middle));
                    string at = "seed " + s + " floor " + floor + ": " + clear.ToString("0.00");
                    if (room == level.ExitRoom && clear < worstExit) { worstExit = clear; worstExitAt = at; }
                    if (room == level.EliteRoom && clear < worstElite) { worstElite = clear; worstEliteAt = at; }
                }
            }
            TestContext.WriteLine("DROPGROUND the exit room's middle, least clear: " + worstExitAt + "; the elite room's: " + worstEliteAt);
            Assert.That(worstExit, Is.GreaterThanOrEqualTo(need), "the exit room's middle, " + worstExitAt);
            Assert.That(worstElite, Is.GreaterThanOrEqualTo(need), "the elite room's middle, " + worstEliteAt);
        }
    }
}
