using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Taking cover (Kristian, 2026-11-19, an experiment), as the rules see it: whether she is covered against a shot by
    /// the side and by ducked or standing, where she stands against a box and a circle, and the slide along it. The
    /// shapes are drawn here; the numbers are the feel values' first guesses.
    /// </summary>
    public class TakeCoverRulesTests
    {
        const float StandOff = 0.6f, MaxAngle = 60f;

        // A sarcophagus as a level stands it: 1.9 m along x, 1.1 m along z, its near face 0.6 m north of her.
        static readonly CoverShape Sarcophagus = CoverShape.Box(new GroundPoint(0f, 1.15f), 1.9f, 1.1f);
        static readonly GroundPoint Her = new GroundPoint(0f, 0f);

        static void Near(GroundPoint actual, GroundPoint expected, string what, float within = 1e-3f)
        {
            Assert.That(actual.X, Is.EqualTo(expected.X).Within(within), what + " x " + actual);
            Assert.That(actual.Z, Is.EqualTo(expected.Z).Within(within), what + " z " + actual);
        }

        // ---------------- Covered or not ----------------

        [Test]
        public void ALowProp_CoversHer_OnlyFromItsFarSide_AndOnlyWhileSheIsDucked()
        {
            var farSide = new GroundPoint(0f, 9f);
            Assert.That(TakeCoverRules.Shields(Sarcophagus, tall: false, ducked: true, Her, farSide), Is.True, "ducked, the shooter beyond it");
            Assert.That(TakeCoverRules.Shields(Sarcophagus, tall: false, ducked: false, Her, farSide), Is.False, "standing, she is seen over it");
            Assert.That(TakeCoverRules.Shields(Sarcophagus, tall: false, ducked: true, Her, new GroundPoint(2f, 9f)), Is.True, "beyond it at an angle");

            var herSide = new GroundPoint(0f, -8f);
            Assert.That(TakeCoverRules.Shields(Sarcophagus, tall: false, ducked: true, Her, herSide), Is.False, "a shooter on her side sees her");
            var round = new GroundPoint(8f, 2f); // walked round past its end: the line to her misses it
            Assert.That(TakeCoverRules.Shields(Sarcophagus, tall: false, ducked: true, Her, round), Is.False, "a shooter round its end sees her");
        }

        [Test]
        public void TallCover_CoversHer_StandingOrDucked_ButStillOnlyFromItsFarSide()
        {
            var pillar = CoverShape.Circle(new GroundPoint(0f, 1.2f), 0.6f);
            Assert.That(TakeCoverRules.Shields(pillar, tall: true, ducked: false, Her, new GroundPoint(0f, 10f)), Is.True, "standing behind a pillar");
            Assert.That(TakeCoverRules.Shields(pillar, tall: true, ducked: true, Her, new GroundPoint(0f, 10f)), Is.True);
            Assert.That(TakeCoverRules.Shields(pillar, tall: true, ducked: true, Her, new GroundPoint(10f, 0f)), Is.False, "from the side the pillar is not between");
            Assert.That(TakeCoverRules.Shields(pillar, tall: true, ducked: true, Her, new GroundPoint(0f, -10f)), Is.False, "nor from behind her");
        }

        [Test]
        public void ALine_ThatGrazesTheEdge_IsNotThrough()
        {
            var box = CoverShape.Box(new GroundPoint(0f, 0f), 2f, 2f);
            Assert.That(box.Crosses(new GroundPoint(-5f, 1f), new GroundPoint(5f, 1f)), Is.False, "along its north face");
            Assert.That(box.Crosses(new GroundPoint(-5f, 0.9f), new GroundPoint(5f, 0.9f)), Is.True);
            Assert.That(box.Crosses(new GroundPoint(-5f, 0f), new GroundPoint(-2f, 0f)), Is.False, "a segment that ends short of it");
        }

        [Test]
        public void TheShootersLine_EntersTheProp_AtItsFace()
        {
            Assert.That(Sarcophagus.Entry(new GroundPoint(0f, 9f), new GroundPoint(0f, -1f), 14f), Is.EqualTo(9f - 1.7f).Within(1e-3f));
            Assert.That(Sarcophagus.Entry(new GroundPoint(0f, 9f), new GroundPoint(0f, 1f), 14f), Is.EqualTo(14f), "pointed away: never");
            Assert.That(Sarcophagus.Entry(new GroundPoint(0f, 9f), new GroundPoint(0f, -1f), 5f), Is.EqualTo(5f), "not within its length");
            var pillar = CoverShape.Circle(new GroundPoint(0f, 0f), 0.6f);
            Assert.That(pillar.Entry(new GroundPoint(-5f, 0f), new GroundPoint(2f, 0f), 14f), Is.EqualTo(4.4f).Within(1e-3f));
            var turned = CoverShape.Box(new GroundPoint(0f, 0f), 2f, 1f, 90f); // its long side along z
            Assert.That(turned.Entry(new GroundPoint(5f, 0f), new GroundPoint(-1f, 0f), 14f), Is.EqualTo(4.5f).Within(1e-3f));
        }

        // ---------------- Where she stands ----------------

        [Test]
        public void AgainstABox_SheStandsOnHerSide_TheStandOffFromItsFace()
        {
            Near(TakeCoverRules.StandPlace(Sarcophagus, new GroundPoint(0.3f, -0.5f), StandOff), new GroundPoint(0.3f, 0f), "south of it");
            Near(TakeCoverRules.StandPlace(Sarcophagus, new GroundPoint(0.3f, 2.5f), StandOff), new GroundPoint(0.3f, 2.3f), "north of it");
            Near(TakeCoverRules.StandPlace(Sarcophagus, new GroundPoint(-2f, 1.2f), StandOff), new GroundPoint(-1.55f, 1.2f), "west of it");

            // Turned a quarter: 2 m along z, 1 m along x.
            var turned = CoverShape.Box(new GroundPoint(0f, 0f), 2f, 1f, 90f);
            Near(TakeCoverRules.StandPlace(turned, new GroundPoint(2f, 0.2f), StandOff), new GroundPoint(1.1f, 0.2f), "east of a turned box");
            Near(TakeCoverRules.StandPlace(turned, new GroundPoint(0.2f, 2f), StandOff), new GroundPoint(0.2f, 1.6f), "at its north end");

            // Off a corner: the stand-off from the corner, along the line from it to her.
            var box = CoverShape.Box(new GroundPoint(0f, 0f), 2f, 1f);
            GroundPoint corner = new GroundPoint(1f, 0.5f), her = new GroundPoint(2f, 3f);
            GroundPoint place = TakeCoverRules.StandPlace(box, her, StandOff);
            Assert.That(place.DistanceTo(corner), Is.EqualTo(StandOff).Within(1e-3f), "round the corner");
            GroundPoint toHer = her - corner, toPlace = place - corner;
            Assert.That(toHer.X * toPlace.Z - toHer.Z * toPlace.X, Is.EqualTo(0f).Within(1e-3f), "on the line from the corner to her");
        }

        [Test]
        public void AgainstACircle_SheStandsTheStandOffFromItsEdge_OnHerSide()
        {
            var pillar = CoverShape.Circle(new GroundPoint(0f, 0f), 0.6f);
            Near(TakeCoverRules.StandPlace(pillar, new GroundPoint(2f, 0f), StandOff), new GroundPoint(1.2f, 0f), "east");
            Near(TakeCoverRules.StandPlace(pillar, new GroundPoint(0f, -3f), StandOff), new GroundPoint(0f, -1.2f), "south");
            Near(TakeCoverRules.StandPlace(pillar, new GroundPoint(0.2f, 0.2f), StandOff), new GroundPoint(0.8485f, 0.8485f), "from inside it, out");
            Near(TakeCoverRules.StandPlace(Sarcophagus, new GroundPoint(0.3f, 0.8f), StandOff), new GroundPoint(0.3f, 0f), "from inside a box, out by its nearest face");
        }

        [Test]
        public void InReach_IsTheDistanceToItsEdge()
        {
            Assert.That(TakeCoverRules.InReach(Sarcophagus, new GroundPoint(0f, 0.6f - 1.4f), 1.5f), Is.True, "1.4 m from its face");
            Assert.That(TakeCoverRules.InReach(Sarcophagus, new GroundPoint(0f, 0.6f - 1.6f), 1.5f), Is.False, "1.6 m");
            Assert.That(Sarcophagus.Distance(new GroundPoint(0f, 1f)), Is.EqualTo(0f), "inside it");
        }

        // ---------------- The slide ----------------

        static readonly CoverShape Wall = CoverShape.Box(new GroundPoint(0f, 1f), 6f, 1f); // x from -3 to 3, z from 0.5 to 1.5
        static readonly GroundPoint AtWall = new GroundPoint(0f, -0.1f);

        static GroundPoint Slide(CoverShape shape, GroundPoint from, GroundPoint move, float step, int steps)
        {
            GroundPoint at = from;
            for (int i = 0; i < steps; i++) at = TakeCoverRules.Slide(shape, at, move, step, StandOff, MaxAngle);
            return at;
        }

        static GroundPoint Way(float degreesFromEast) =>
            new GroundPoint((float)Math.Cos(degreesFromEast * Math.PI / 180.0), (float)Math.Sin(degreesFromEast * Math.PI / 180.0));

        [Test]
        public void AlongTheFace_SheSlides_AndStaysAgainstIt()
        {
            Near(Slide(Wall, AtWall, new GroundPoint(1f, 0f), 0.1f, 10), new GroundPoint(1f, -0.1f), "east along the wall");
            Near(Slide(Wall, AtWall, new GroundPoint(-1f, 0f), 0.1f, 10), new GroundPoint(-1f, -0.1f), "west");
            Near(Slide(Wall, AtWall, Way(-45f), 0.1f, 10), new GroundPoint(1f, -0.1f), "45 degrees away from it still slides, along it, as fast");
            Near(Slide(Wall, AtWall, Way(-50f), 0.1f, 10), new GroundPoint(1f, -0.1f), "50 degrees away");
            Near(Slide(Wall, AtWall, Way(45f), 0.1f, 10), new GroundPoint(1f, -0.1f), "45 degrees into it");
        }

        [Test]
        public void AKeyFarOffTheFace_DoesNothing()
        {
            Near(Slide(Wall, AtWall, new GroundPoint(0f, -1f), 0.1f, 10), AtWall, "straight away from it");
            Near(Slide(Wall, AtWall, Way(-70f), 0.1f, 10), AtWall, "70 degrees away");
            Near(Slide(Wall, AtWall, new GroundPoint(0f, 1f), 0.1f, 10), AtWall, "straight into it");
            Near(Slide(Wall, AtWall, new GroundPoint(0f, 0f), 0.1f, 10), AtWall, "no key");
        }

        [Test]
        public void AtABoxsCorner_SheGoesRound_UntilTheKeyIsTooFarOffTheFace()
        {
            GroundPoint corner = new GroundPoint(3f, 0.5f);
            GroundPoint at = Slide(Wall, new GroundPoint(2.9f, -0.1f), new GroundPoint(1f, 0f), 0.005f, 600);
            Assert.That(at.DistanceTo(corner), Is.EqualTo(StandOff).Within(1e-3f), "against the corner");
            GroundPoint out_ = (at - corner) * (1f / StandOff);
            float round = (float)(Math.Atan2(out_.X, -out_.Z) * 180.0 / Math.PI); // 0 on the south face, 90 on the east one
            Assert.That(round, Is.EqualTo(MaxAngle).Within(1f), "she stops where the face is the slide angle off the key");
        }

        [Test]
        public void RoundAPillar_SheStaysTheStandOffFromItsEdge()
        {
            var pillar = CoverShape.Circle(new GroundPoint(0f, 0f), 0.6f);
            GroundPoint at = Slide(pillar, new GroundPoint(0f, -1.2f), new GroundPoint(1f, 0f), 0.01f, 40);
            Assert.That(at.Length, Is.EqualTo(1.2f).Within(1e-3f), "against it all the way");
            Assert.That(at.X, Is.GreaterThan(0.3f), "and round it the way of the key");
        }

        // ---------------- The feel values ----------------

        [Test]
        public void TheStartValues_AreTheOrdersFirstGuesses()
        {
            var feel = new FeelValues();
            Assert.That(feel.TakeCover, Is.True);
            Assert.That(feel.CoverReach, Is.EqualTo(1.5f));
            Assert.That(feel.CoverSnapTime, Is.EqualTo(0.15f));
            Assert.That(feel.CoverMoveShare, Is.EqualTo(0.4f));
            Assert.That(feel.CoverPeekTime, Is.EqualTo(0.25f));
            Assert.That(feel.CoverSlideAngle, Is.EqualTo(60f));
            Assert.That(feel.CoverStandOff, Is.GreaterThanOrEqualTo(Placeholders.PlayerRadius), "she stands clear of it");
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_GivesTheTimeInCover_ItsShareOfTheFights_AndTheShotsItStopped()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                new SessionEvent(10, SessionEventTypes.EnemyWake) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(12, SessionEventTypes.CoverEnter, SessionWords.Low),
                new SessionEvent(13, SessionEventTypes.EnemyStrike, "Shot") { EnemyId = 1, Enemy = "shooter", Result = SessionWords.StoppedByCover },
                new SessionEvent(15, SessionEventTypes.CoverLeave) { Result = SessionWords.Released },
                new SessionEvent(20, SessionEventTypes.EnemyDeath) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(25, SessionEventTypes.CoverEnter, SessionWords.Tall),
                new SessionEvent(30, SessionEventTypes.SessionEnd),
            };

            SessionStats stats = SessionSummary.Compute(events);

            Assert.That(stats.CoverSpans.Count, Is.EqualTo(2), "the last one runs to the session's end");
            Assert.That(stats.Session.CoverSeconds, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(stats.Session.CoversTaken, Is.EqualTo(2));
            Assert.That(stats.InFights.CoverSeconds, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(stats.InFights.CoverShare, Is.EqualTo(0.3f).Within(1e-4f), "3 s of the 10 s fight");
            Assert.That(stats.Session.StrikesStoppedByCover, Is.EqualTo(1));
            Assert.That(stats.InFights.StrikesStoppedByCover, Is.EqualTo(1));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Time in cover, share of the time", text);
            StringAssert.Contains("3.0 s, 30%", text);
            StringAssert.Contains("stopped by the cover she ducked behind", text);

            // A log written back and read again gives the same.
            var read = new List<SessionEvent>();
            foreach (SessionEvent e in events) read.Add(SessionEvent.Parse(e.ToJson()));
            Assert.That(SessionSummary.Format(SessionSummary.Compute(read)), Is.EqualTo(text));
        }

        [Test]
        public void ASessionWithoutCover_HasNoCoverLines()
        {
            var events = new List<SessionEvent> { new SessionEvent(0, SessionEventTypes.SessionStart), new SessionEvent(5, SessionEventTypes.SessionEnd) };
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            Assert.That(text, Does.Not.Contain("Time in cover"));
            Assert.That(text, Does.Not.Contain("stopped by the cover"));
        }
    }
}
