using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The quick duck (Kristian's answer of 2026-11-20 to the cover report, proposal C; work order 043), as the rules see it:
    /// its reach, its half second down, the key's wait after it, what ends it, what it stops and what not, the timing's
    /// reward; which props are low and which tall, every prop of <see cref="PropModel"/> by name; its word; and the summary's
    /// lines in place of taking cover's. The numbers are the feel values' first guesses, read from them.
    /// </summary>
    public class QuickDuckRulesTests
    {
        static readonly FeelValues Feel = new FeelValues();
        // A sarcophagus as a level stands it: 1.9 m along x, 1.1 m along z, its near face 0.6 m north of her.
        static readonly CoverShape Sarcophagus = CoverShape.Box(new GroundPoint(0f, 1.15f), 1.9f, 1.1f);
        static readonly GroundPoint Her = new GroundPoint(0f, 0f);

        // ---------------- The values ----------------

        [Test]
        public void TheStartValues_AreTheOrdersNumbers()
        {
            Assert.That(Feel.QuickDuck, Is.True, "on by default");
            Assert.That(Feel.DuckReach, Is.EqualTo(1f));
            Assert.That(Feel.DuckSeconds, Is.EqualTo(0.5f));
            Assert.That(Feel.DuckWait, Is.EqualTo(1f));
            Assert.That(Feel.DuckPeekShot, Is.True);
            Assert.That(Feel.DuckPeekWindow, Is.EqualTo(0.4f));
        }

        // ---------------- The duck ----------------

        [Test]
        public void ATap_WithALowPropInReach_DucksHerForTheDuckSeconds_AndSheRisesByHerself()
        {
            var duck = new QuickDuck();
            Assert.That(duck.Ready(10f), Is.True);
            Assert.That(duck.Tap(10f, mayDuck: true, lowPropInReach: true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Ducked));
            Assert.That(duck.IsDown, Is.True);
            Assert.That(duck.UpAt, Is.EqualTo(10f + Feel.DuckSeconds).Within(1e-5f));
            Assert.That(duck.RisesBy(10f + Feel.DuckSeconds - 0.01f), Is.False, "still down just before");
            Assert.That(duck.RisesBy(10f + Feel.DuckSeconds), Is.True, "up at its time");
            Assert.That(duck.Tap(10.2f, true, true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Down), "a tap while down does nothing");
        }

        [Test]
        public void TheKey_WaitsTheWaitFromTheMomentSheIsUp_HoweverTheDuckEnded()
        {
            var duck = new QuickDuck();
            duck.Tap(0f, true, true, Feel.DuckSeconds);
            Assert.That(duck.End(duck.UpAt, Feel.DuckWait), Is.True);
            float up = Feel.DuckSeconds;
            Assert.That(duck.RoseAt, Is.EqualTo(up).Within(1e-5f));
            Assert.That(duck.Tap(up + Feel.DuckWait - 0.01f, true, true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Waiting));
            Assert.That(duck.WaitLeft(up + 0.25f), Is.EqualTo(Feel.DuckWait - 0.25f).Within(1e-4f));
            Assert.That(duck.Tap(up + Feel.DuckWait, true, true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Ducked), "it works again after the wait");

            // Ended early by a move: the wait runs from then.
            duck.End(up + Feel.DuckWait + 0.1f, Feel.DuckWait);
            Assert.That(duck.Ready(up + Feel.DuckWait + 0.1f + Feel.DuckWait - 0.01f), Is.False);
            Assert.That(duck.Ready(up + Feel.DuckWait + 0.1f + Feel.DuckWait), Is.True);
            Assert.That(duck.End(100f, Feel.DuckWait), Is.False, "nothing to end when she is up");
        }

        [Test]
        public void ATap_WithNothingInReach_DoesNothing_AndStartsNoWait()
        {
            var duck = new QuickDuck();
            Assert.That(duck.Tap(5f, true, lowPropInReach: false, Feel.DuckSeconds), Is.EqualTo(DuckTap.NothingInReach));
            Assert.That(duck.IsDown, Is.False);
            Assert.That(duck.Ready(5f), Is.True, "no wait");
            Assert.That(duck.Tap(5.01f, true, true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Ducked), "a tap at once in reach ducks her");
        }

        [Test]
        public void SheCannotDuck_WhenSheMayNot()
        {
            var duck = new QuickDuck();
            Assert.That(duck.Tap(1f, mayDuck: false, lowPropInReach: true, Feel.DuckSeconds), Is.EqualTo(DuckTap.Busy));
            Assert.That(duck.IsDown, Is.False);
            Assert.That(duck.Ready(1f), Is.True, "and no wait starts");
        }

        [Test]
        public void TheReach_IsFromHerMiddleToTheEdge()
        {
            Assert.That(QuickDuckRules.InReach(Sarcophagus, Her, Feel.DuckReach), Is.True, "its face 0.6 m off");
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.39f), Feel.DuckReach), Is.True, "0.99 m");
            Assert.That(QuickDuckRules.InReach(Sarcophagus, new GroundPoint(0f, -0.45f), Feel.DuckReach), Is.False, "1.05 m");
        }

        [Test]
        public void AMoveKey_EndsIt_ButOneHeldThroughTheTapDoesNot()
        {
            GroundPoint none = default, east = new GroundPoint(1f, 0f), north = new GroundPoint(0f, 1f);
            Assert.That(QuickDuckRules.MoveEnds(none, east), Is.True, "no key at the tap, then one: she moves");
            Assert.That(QuickDuckRules.MoveEnds(east, east), Is.False, "the key she held at the tap");
            Assert.That(QuickDuckRules.MoveEnds(east, north), Is.True, "another way");
            Assert.That(QuickDuckRules.MoveEnds(east, none), Is.False, "letting go does not end it");
            Assert.That(QuickDuckRules.MoveEnds(none, none), Is.False);
        }

        // ---------------- What it stops ----------------

        [Test]
        public void Down_TheLowProp_StopsAnArrowFromItsFarSide_NotFromHerSide_AndNeverAnOrbOrTheWave()
        {
            var far = new GroundPoint(0f, 9f);
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, far, ProjectileKind.Arrow), Is.True, "an arrow loosed beyond it");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, new GroundPoint(2f, 9f), ProjectileKind.Arrow), Is.True, "at an angle");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, new GroundPoint(0f, -8f), ProjectileKind.Arrow), Is.False, "from her side");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, new GroundPoint(8f, 2f), ProjectileKind.Arrow), Is.False, "from round its end");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, far, ProjectileKind.Orb), Is.False, "an orb passes over");
            Assert.That(QuickDuckRules.Shields(Sarcophagus, Her, far, ProjectileKind.Wave), Is.False, "the wave runs under");
            Assert.That(QuickDuckRules.Stops(ProjectileKind.Arrow), Is.True);
            Assert.That(QuickDuckRules.Stops(ProjectileKind.Orb), Is.False);
            Assert.That(QuickDuckRules.Stops(ProjectileKind.Wave), Is.False);
        }

        [Test]
        public void WhatItStopped_IsCounted_AndTheFirstShotSoonAfterIsTheReward()
        {
            var duck = new QuickDuck();
            duck.Tap(0f, true, true, Feel.DuckSeconds);
            duck.StoppedOne();
            duck.StoppedOne();
            Assert.That(duck.Stopped, Is.EqualTo(2));
            duck.End(duck.UpAt, Feel.DuckWait);
            Assert.That(duck.StoppedLast, Is.EqualTo(2));
            Assert.That(duck.TakePeekShot(Feel.DuckSeconds + Feel.DuckPeekWindow - 0.01f, Feel.DuckPeekWindow), Is.True, "her first shot in the window");
            Assert.That(duck.TakePeekShot(Feel.DuckSeconds + 0.1f, Feel.DuckPeekWindow), Is.False, "only the first");

            duck.Clear();
            duck.Tap(10f, true, true, Feel.DuckSeconds);
            duck.StoppedOne();
            duck.End(duck.UpAt, Feel.DuckWait);
            Assert.That(duck.TakePeekShot(duck.RoseAt + Feel.DuckPeekWindow + 0.05f, Feel.DuckPeekWindow), Is.False, "too late");
            Assert.That(duck.TakePeekShot(duck.RoseAt + 0.1f, Feel.DuckPeekWindow), Is.False, "and the late one was her first");

            duck.Clear();
            duck.Tap(20f, true, true, Feel.DuckSeconds);
            duck.End(duck.UpAt, Feel.DuckWait);
            Assert.That(duck.TakePeekShot(duck.RoseAt + 0.1f, Feel.DuckPeekWindow), Is.False, "a duck that stopped nothing gives none");
            duck.StoppedOne();
            Assert.That(duck.Stopped, Is.EqualTo(0), "nothing is counted while she is up");
        }

        [Test]
        public void Clear_LeavesNoDuck_NoWait_AndNoReward()
        {
            var duck = new QuickDuck();
            duck.Tap(0f, true, true, Feel.DuckSeconds);
            duck.StoppedOne();
            duck.Clear();
            Assert.That(duck.IsDown, Is.False);
            Assert.That(duck.Ready(0.1f), Is.True);
            Assert.That(duck.TakePeekShot(0.6f, Feel.DuckPeekWindow), Is.False);
        }

        // ---------------- Which props ----------------

        [Test]
        public void EveryProp_IsLowOrTall_ByName()
        {
            var expected = new Dictionary<PropModel, PropHeightClass>
            {
                { PropModel.BonePile, PropHeightClass.Low },      // 0.6 m
                { PropModel.Rubble, PropHeightClass.Low },        // 0.8
                { PropModel.SkullPile, PropHeightClass.Low },     // 0.88
                { PropModel.Chest, PropHeightClass.Low },         // 0.9
                { PropModel.Sarcophagus, PropHeightClass.Low },   // 1.0
                { PropModel.Coffin, PropHeightClass.Low },        // 1.0
                { PropModel.Basin, PropHeightClass.Low },         // 1.07
                { PropModel.Brazier, PropHeightClass.Low },       // 1.1
                { PropModel.Urns, PropHeightClass.Low },          // 1.1
                { PropModel.PillarBroken, PropHeightClass.Low },  // 1.2, chosen by its look: a plinth and a thin stub
                { PropModel.Altar, PropHeightClass.Low },         // 1.3, chosen: a pedestal block
                { PropModel.GraveMarker, PropHeightClass.Low },   // 1.3, chosen: a headstone
                { PropModel.Candelabra, PropHeightClass.Slender },// 1.8, slender
                { PropModel.Cage, PropHeightClass.Tall },         // 2.0
                { PropModel.Bookshelf, PropHeightClass.Tall },    // 2.17
                { PropModel.Statue, PropHeightClass.Tall },       // 2.6
                { PropModel.BoneThrone, PropHeightClass.Tall },   // 2.6
                { PropModel.Pillar, PropHeightClass.Tall },       // 3.0
            };
            PropModel[] all = Enum.GetValues(typeof(PropModel)).Cast<PropModel>().ToArray();
            Assert.That(expected.Keys, Is.EquivalentTo(all), "every prop of the table is named here");
            foreach (PropModel m in all)
            {
                Assert.That(PropSort.Of(m), Is.EqualTo(expected[m]), m + " at " + PropSize.Of(m).Height + " m");
                double h = PropSize.Of(m).Height;
                if (h <= PropSort.LowMost + 1e-3) Assert.That(PropSort.Of(m), Is.EqualTo(PropHeightClass.Low), m + ": at most the low line");
                if (h >= PropSort.TallLeast - 1e-3 && !PropSize.Of(m).Slender) Assert.That(PropSort.Of(m), Is.EqualTo(PropHeightClass.Tall), m + ": at least the tall line");
            }
            Assert.That(all.Where(m => PropSort.LooksLow(m)), Is.EquivalentTo(new[] { PropModel.Altar, PropModel.GraveMarker, PropModel.PillarBroken }),
                        "the three between the lines, chosen by their look");
        }

        [Test]
        public void TheThreeBetweenTheLines_AreTheOnlyCoverThatTheDuckLowers()
        {
            var v = new PropValues();
            PropModel[] lowered = Enum.GetValues(typeof(PropModel)).Cast<PropModel>()
                                      .Where(m => PropSort.LowerUnderTheDuck(LevelProp.Make(m, new LevelPoint(0, 0), 0, PropPlace.Floor), v)).ToArray();
            Assert.That(lowered, Is.EquivalentTo(new[] { PropModel.Altar, PropModel.GraveMarker, PropModel.PillarBroken }));
            // Nothing low today turns tall: a prop the duck sorts tall is cover by its height already.
            foreach (PropModel m in Enum.GetValues(typeof(PropModel)))
                if (PropSort.Of(m) == PropHeightClass.Tall)
                    Assert.That(LevelProp.Make(m, new LevelPoint(0, 0), 0, PropPlace.Floor).IsCover(v), Is.True, m.ToString());
        }

        // ---------------- The word and the summary ----------------

        [Test]
        public void AStop_SaysDUCKED_InTheGreenOfTheTimings()
        {
            FeedbackWord w = FeedbackRules.Word(FeedbackMoment.Ducked);
            Assert.That(w.Text, Is.EqualTo("DUCKED"));
            Assert.That(w.Tint, Is.EqualTo(FeedbackTint.Clean));
        }

        [Test]
        public void TheSummary_WithTheSwitchOn_CountsTheDucks_InPlaceOfTheCoverLines()
        {
            var start = new SessionEvent(0, SessionEventTypes.SessionStart);
            start.AddExtra("feel.TakeCover", "true");
            start.AddExtra("feel.QuickDuck", "true");
            var end1 = new SessionEvent(12.5, SessionEventTypes.DuckEnd) { Result = SessionWords.Rose };
            end1.AddExtra("stopped", "2");
            var end2 = new SessionEvent(16.2, SessionEventTypes.DuckEnd) { Result = SessionWords.Moved };
            end2.AddExtra("stopped", "0");
            var events = new List<SessionEvent>
            {
                start,
                new SessionEvent(10, SessionEventTypes.EnemyWake) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(11, SessionEventTypes.DuckNothing),
                new SessionEvent(12, SessionEventTypes.Duck, "Prop_C1_0_Sarcophagus"),
                new SessionEvent(12.3, SessionEventTypes.EnemyStrike, ShooterValues.ArrowFan) { EnemyId = 1, Enemy = "shooter", Result = SessionWords.StoppedByCover },
                new SessionEvent(12.3, SessionEventTypes.EnemyStrike, ShooterValues.ArrowFan) { EnemyId = 1, Enemy = "shooter", Result = SessionWords.StoppedByCover },
                end1,
                new SessionEvent(16, SessionEventTypes.Duck, "Prop_C1_1_Coffin"),
                end2,
                new SessionEvent(20, SessionEventTypes.EnemyDeath) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(22, SessionEventTypes.DuckNothing),
                new SessionEvent(30, SessionEventTypes.SessionEnd),
            };

            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.HasDuck, Is.True);
            Assert.That(stats.Session.Ducks, Is.EqualTo(2));
            Assert.That(stats.Session.DucksThatStopped, Is.EqualTo(1));
            Assert.That(stats.Session.DuckTapsWithNothing, Is.EqualTo(2));
            Assert.That(stats.InFights.DuckTapsWithNothing, Is.EqualTo(1), "one in the fight");
            Assert.That(stats.Session.StoppedByDuck, Is.EqualTo(2));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Quick ducks / that stopped something", text);
            StringAssert.Contains("taps with no low prop in reach", text);
            StringAssert.Contains("stopped by a quick duck", text);
            Assert.That(text, Does.Not.Contain("Time in cover"));
            Assert.That(text, Does.Not.Contain("stopped by the cover she ducked behind"));

            // A log written back and read again gives the same.
            var read = new List<SessionEvent>();
            foreach (SessionEvent e in events) read.Add(SessionEvent.Parse(e.ToJson()));
            Assert.That(SessionSummary.Format(SessionSummary.Compute(read)), Is.EqualTo(text));
        }

        [Test]
        public void TheSummary_WithTheSwitchOff_KeepsTheCoverLines()
        {
            var start = new SessionEvent(0, SessionEventTypes.SessionStart);
            start.AddExtra("feel.TakeCover", "true");
            start.AddExtra("feel.QuickDuck", "false");
            var events = new List<SessionEvent>
            {
                start,
                new SessionEvent(12, SessionEventTypes.CoverEnter, SessionWords.Low),
                new SessionEvent(15, SessionEventTypes.CoverLeave) { Result = SessionWords.Released },
                new SessionEvent(30, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.HasDuck, Is.False);
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Time in cover, share of the time", text);
            Assert.That(text, Does.Not.Contain("Quick ducks"));
        }
    }
}
