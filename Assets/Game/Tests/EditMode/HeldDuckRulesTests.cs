using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The held duck (work order 086; Kristian's answer of 2026-11-22: "vil at den skal vÃ¦re hold for duck. SÃ¥ kan man gjÃ¸re det
    /// sÃ¥ lenge man vil"; <see cref="HeldDuckRules"/>, the state <see cref="QuickDuck"/>). Its switch and values; down for as long
    /// as the key is held and up at once on release; the short wait after; a shot or a ready skill brings her up and the reward
    /// stays; the move keys do nothing; down again at the prop after her own shot with the key still held; the tap of work order
    /// 043 as before with it off; the summary's seconds down and what was stopped.
    /// </summary>
    public class HeldDuckRulesTests
    {
        static readonly FeelValues Feel = new FeelValues();

        [Test]
        public void TheSwitch_IsOnByDefault_WithTheShortWait_AndFollowsTheQuickDuck()
        {
            Assert.That(Feel.DuckHold, Is.True, "on by default");
            Assert.That(Feel.DuckHoldWait, Is.EqualTo(0.2f), "the producer's proposal: short, 0.2 s");
            Assert.That(Feel.DuckHoldDownAgain, Is.True);
            Assert.That(HeldDuckRules.Held(Feel), Is.True);
            Assert.That(HeldDuckRules.Held(new FeelValues { QuickDuck = false }), Is.False, "the quick duck off: taking cover, as before");
            Assert.That(HeldDuckRules.Held(new FeelValues { DuckHold = false }), Is.False, "off: the tap");
            Assert.That(HeldDuckRules.Held(null), Is.False);
            Assert.That(HeldDuckRules.Seconds(Feel), Is.EqualTo(float.PositiveInfinity), "no longest time");
            Assert.That(HeldDuckRules.Wait(Feel), Is.EqualTo(0.2f));
            var tap = new FeelValues { DuckHold = false };
            Assert.That((HeldDuckRules.Seconds(tap), HeldDuckRules.Wait(tap)), Is.EqualTo((tap.DuckSeconds, tap.DuckWait)), "the tap's 0.5 s and 1 s, as before");
            Assert.That((tap.DuckSeconds, tap.DuckWait), Is.EqualTo((0.5f, 1f)));
        }

        [Test]
        public void Held_SheStaysDownAsLongAsTheKeyIsHeld_IsUpAtOnceOnRelease_AndTheKeyWaitsTheShortWait()
        {
            var duck = new QuickDuck();
            Assert.That(duck.Tap(10f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Ducked), "pressed beside a low prop: down");
            foreach (float t in new[] { 10.5f, 11f, 60f, 3600f })
            {
                Assert.That(duck.RisesBy(t), Is.False, "still down at " + t + " s: no longest time");
                Assert.That(HeldDuckRules.Released(Feel, keyHeld: true), Is.False, "the key held");
            }
            Assert.That(duck.IsDown, Is.True);
            Assert.That(duck.Tap(20f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Down), "a press while down does nothing more");
            Assert.That(HeldDuckRules.Released(Feel, keyHeld: false), Is.True, "let go: up");
            Assert.That(duck.End(70f, HeldDuckRules.Wait(Feel)), Is.True);
            Assert.That(duck.IsDown, Is.False, "up at once");
            Assert.That(duck.RoseAt, Is.EqualTo(70f));
            Assert.That(duck.Tap(70.19f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Waiting), "0.19 s after: the key waits");
            Assert.That(duck.WaitLeft(70.1f), Is.EqualTo(0.1f).Within(1e-4f));
            Assert.That(duck.Tap(70.2f, true, true, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Ducked), "0.2 s after: down again");
            Assert.That(duck.Tap(80f, true, false, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.Down));
            duck.End(80f, HeldDuckRules.Wait(Feel));
            Assert.That(duck.Tap(81f, true, false, HeldDuckRules.Seconds(Feel)), Is.EqualTo(DuckTap.NothingInReach), "nothing in reach: nothing, as before");
            Assert.That(duck.Ready(81f), Is.True, "and no wait from it");
            Assert.That(HeldDuckRules.Released(new FeelValues { DuckHold = false }, keyHeld: false), Is.False, "the tap does not end at the key's release");
        }

        [Test]
        public void Held_AShotOrAReadySkill_BringsHerUpAndGoesOff_TheRewardStays_ASkillOnItsCooldownLeavesHerDown()
        {
            Assert.That(HeldDuckRules.ActEnds(Feel, down: true, wouldGoOff: true), Is.True, "a click, or a ready skill: up, and it goes off");
            Assert.That(HeldDuckRules.ActEnds(Feel, down: true, wouldGoOff: false), Is.False, "a skill on its cooldown: she stays down");
            Assert.That(HeldDuckRules.ActEnds(Feel, down: false, wouldGoOff: true), Is.False, "up already");
            Assert.That(HeldDuckRules.ActEnds(new FeelValues { DuckHold = false }, true, true), Is.False, "the tap: it waits until she rises, as before");

            // Down 3 s, an arrow stopped; a click at 13 s brings her up, and the shot that starts then is the timing's reward.
            var duck = new QuickDuck();
            duck.Tap(10f, true, true, HeldDuckRules.Seconds(Feel));
            duck.StoppedOne();
            duck.End(13f, HeldDuckRules.Wait(Feel));
            Assert.That(duck.StoppedLast, Is.EqualTo(1));
            Assert.That(duck.TakePeekShot(13f, Feel.DuckPeekWindow), Is.True, "the shot as she comes up staggers, as before");
            // One that stopped nothing gives none.
            duck.Tap(20f, true, true, HeldDuckRules.Seconds(Feel));
            duck.End(25f, HeldDuckRules.Wait(Feel));
            Assert.That(duck.TakePeekShot(25f, Feel.DuckPeekWindow), Is.False);
        }

        [Test]
        public void Held_TheMoveKeysDoNothing_AndSheGoesDownAgainAtThePropAfterHerOwnShot_WithTheKeyStillHeld()
        {
            Assert.That(HeldDuckRules.MoveMayEnd(Feel), Is.False, "held: she stays at the prop");
            Assert.That(HeldDuckRules.MoveMayEnd(new FeelValues { DuckHold = false }), Is.True, "the tap: a move key ends it, as before");
            Assert.That(HeldDuckRules.DownAgain(Feel, keyHeld: true, roseToAct: true, down: false, ready: true, free: true, lowPropInReach: true), Is.True, "down again");
            Assert.That(HeldDuckRules.DownAgain(Feel, false, true, false, true, true, true), Is.False, "the key let go");
            Assert.That(HeldDuckRules.DownAgain(Feel, true, false, false, true, true, true), Is.False, "not up to act: holding the key on and walking up to a prop is no press");
            Assert.That(HeldDuckRules.DownAgain(Feel, true, true, true, true, true, true), Is.False, "down already");
            Assert.That(HeldDuckRules.DownAgain(Feel, true, true, false, false, true, true), Is.False, "the wait not over");
            Assert.That(HeldDuckRules.DownAgain(Feel, true, true, false, true, false, true), Is.False, "her shot still going, or a click waiting");
            Assert.That(HeldDuckRules.DownAgain(Feel, true, true, false, true, true, false), Is.False, "the prop out of reach");
            Assert.That(HeldDuckRules.DownAgain(new FeelValues { DuckHoldDownAgain = false }, true, true, false, true, true, true), Is.False, "its switch off");
            Assert.That(HeldDuckRules.DownAgain(new FeelValues { DuckHold = false }, true, true, false, true, true, true), Is.False, "the tap");
        }

        [Test]
        public void TheSummary_GivesTheSecondsDown_TotalAndMedian_AndWhatTheDucksStopped()
        {
            var start = new SessionEvent(0, SessionEventTypes.SessionStart);
            start.AddExtra("feel.TakeCover", "true");
            start.AddExtra("feel.QuickDuck", "true");
            start.AddExtra("feel.DuckHold", "true");
            SessionEvent End(double t, string how, int stopped, float seconds)
            {
                var e = new SessionEvent(t, SessionEventTypes.DuckEnd) { Result = how };
                e.AddExtra("stopped", stopped.ToString(System.Globalization.CultureInfo.InvariantCulture));
                e.AddExtra("seconds", seconds.ToString("0.####", System.Globalization.CultureInfo.InvariantCulture));
                return e;
            }
            SessionEvent Stop(double t, string attack) =>
                new SessionEvent(t, SessionEventTypes.EnemyStrike, attack) { EnemyId = 1, Enemy = "shooter", Result = SessionWords.StoppedByCover };
            var events = new List<SessionEvent>
            {
                start,
                new SessionEvent(10, SessionEventTypes.EnemyWake) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(11, SessionEventTypes.Duck, "Prop_C1_0_Sarcophagus"),
                Stop(12, ShooterValues.Arrow), Stop(13, ShooterValues.ArrowFan), Stop(14, ShooterValues.Arrow),
                End(16, SessionWords.Acted, 3, 5f),
                new SessionEvent(17, SessionEventTypes.Duck, "Prop_C1_0_Sarcophagus"),
                End(18, SessionWords.Released, 0, 1f),
                new SessionEvent(19, SessionEventTypes.Duck, "Prop_C1_1_Coffin"),
                End(21, SessionWords.Released, 0, 2f),
                new SessionEvent(25, SessionEventTypes.EnemyDeath) { EnemyId = 1, Enemy = "shooter" },
                new SessionEvent(30, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Session.DuckSecondsTotal, Is.EqualTo(8f).Within(1e-4f));
            Assert.That(stats.Session.DuckSecondsMedian, Is.EqualTo(2f).Within(1e-4f));
            Assert.That(stats.Session.StoppedByDuckWhat, Is.EqualTo(new[]
            {
                new KeyValuePair<string, int>("shooter " + ShooterValues.Arrow, 2), new KeyValuePair<string, int>("shooter " + ShooterValues.ArrowFan, 1),
            }));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("seconds down: total, median", text);
            StringAssert.Contains("8.00 s, 2.00 s", text);
            StringAssert.Contains("what it stopped", text);
            StringAssert.Contains("shooter " + ShooterValues.Arrow + " 2, shooter " + ShooterValues.ArrowFan + " 1", text);
            // Written back and read again: the same.
            var read = new List<SessionEvent>();
            foreach (SessionEvent e in events) read.Add(SessionEvent.Parse(e.ToJson()));
            Assert.That(SessionSummary.Format(SessionSummary.Compute(read)), Is.EqualTo(text));
            // A log from before (no seconds on its ends): "-".
            foreach (SessionEvent e in events) e.Extra?.RemoveAll(p => p.Key == "seconds");
            Assert.That(float.IsNaN(SessionSummary.Compute(events).Session.DuckSecondsMedian), Is.True);
        }
    }
}
