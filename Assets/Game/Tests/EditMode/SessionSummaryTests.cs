using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The play session summary's arithmetic, on small hand-written event lists: the tempo, a row per skill, the gaps,
    /// the pairs, the fight boundaries, the gauge, held and charged skills, defence, movement, an empty session; and
    /// a log written, read back and summarised from its file.
    /// </summary>
    public class SessionSummaryTests
    {
        const string Quick = "Quick Shot", Volley = "Heavy Volley", Fan = "Fan Fire", Long = "Long Shot";

        static SessionEvent E(double t, string type, string name = null) => new SessionEvent(t, type, name);
        static SessionEvent Kit(int slot, string name, string input, float cooldown) =>
            new SessionEvent(0d, SessionEventTypes.Skill, name) { Slot = slot, Input = input, Cooldown = cooldown };
        static SessionEvent SkillStart(double t, string name) => E(t, SessionEventTypes.SkillStart, name);
        static SessionEvent ShotStart(double t) => E(t, SessionEventTypes.ShotStart, SessionWords.BasicShot);
        static SessionEvent DashStart(double t) => E(t, SessionEventTypes.DashStart, SessionWords.Dash);
        static SessionEvent End(double t) => E(t, SessionEventTypes.SessionEnd);
        static SessionEvent Press(double t, string name, string result, string reason = null) =>
            new SessionEvent(t, SessionEventTypes.SkillPress, name) { Result = result, Reason = reason };
        static SessionEvent Hit(double t, string name, int damage, bool empowered = false) =>
            new SessionEvent(t, SessionEventTypes.Hit, name) { Damage = damage, Target = SessionWords.Dummy, Empowered = empowered };

        // ---------------- Tempo ----------------

        [Test]
        public void Tempo_CountsStartedActions_AndSeparatelyEveryPress_OverTheSessionAndTheBusiestTenSeconds()
        {
            var events = new List<SessionEvent>();
            // Ten shots in five seconds, clicked, then two more far apart; three skill presses rejected in between.
            for (int i = 0; i < 10; i++) { events.Add(E(10 + i * 0.5, SessionEventTypes.Click)); events.Add(ShotStart(10 + i * 0.5)); }
            events.Add(E(40, SessionEventTypes.Click)); events.Add(ShotStart(40));
            events.Add(E(50, SessionEventTypes.Click)); events.Add(ShotStart(50));
            for (int i = 1; i <= 3; i++) events.Add(Press(40 + i, Quick, SessionWords.Rejected, SessionWords.Cooldown));
            events.Add(End(60));

            ScopeStats s = SessionSummary.Compute(events).Session;

            Assert.That(s.StartedActions, Is.EqualTo(12));
            Assert.That(s.Presses, Is.EqualTo(15));
            Assert.That(s.Apm, Is.EqualTo(12f).Within(1e-4f), "12 actions in one minute");
            Assert.That(s.ApmWithRejected, Is.EqualTo(15f).Within(1e-4f));
            Assert.That(s.BusiestApm, Is.EqualTo(60f).Within(1e-4f), "ten in the busiest ten seconds");
            Assert.That(s.BusiestApmWithRejected, Is.EqualTo(60f).Within(1e-4f));
        }

        // ---------------- Per skill ----------------

        [Test]
        public void SkillRow_CountsPressesStartsCooldownRejectionsDamageHitsAndCooldownUse()
        {
            var events = new List<SessionEvent>
            {
                Kit(0, Quick, "Normal", 4f),
                Kit(5, Volley, "Casting", 16f),
                Press(1, Quick, SessionWords.Started), SkillStart(1, Quick),
                new SessionEvent(1.15, SessionEventTypes.SkillFired, Quick) { Hit = true }, Hit(1.15, Quick, 100),
                new SessionEvent(2, SessionEventTypes.SkillPress, Quick) { Result = SessionWords.Rejected, Reason = SessionWords.Cooldown, CooldownLeft = 3f },
                Press(5.5, Quick, SessionWords.Started), SkillStart(5.5, Quick),
                new SessionEvent(5.65, SessionEventTypes.SkillFired, Quick) { Hit = false },
                Press(9, Quick, SessionWords.Started), SkillStart(9, Quick),
                new SessionEvent(9.15, SessionEventTypes.SkillFired, Quick) { Hit = true }, Hit(9.15, Quick, 100),
                ShotStart(12), new SessionEvent(12.1, SessionEventTypes.ShotFired, SessionWords.BasicShot) { Hit = true }, Hit(12.1, SessionWords.BasicShot, 50),
                End(20),
            };

            ScopeStats s = SessionSummary.Compute(events).Session;
            ActionRow q = s.Row(Quick);

            Assert.That(q.Pressed, Is.EqualTo(4));
            Assert.That(q.Started, Is.EqualTo(3));
            Assert.That(q.RejectedOnCooldown, Is.EqualTo(1));
            Assert.That(q.Damage, Is.EqualTo(200));
            Assert.That(q.DamageShare, Is.EqualTo(0.8f).Within(1e-4f), "200 of 250");
            Assert.That(q.Hits, Is.EqualTo(2));
            Assert.That(q.Misses, Is.EqualTo(1));
            Assert.That(q.CooldownUse, Is.EqualTo(0.5f).Within(1e-4f), "3 starts of the 6 a 4 s cooldown allows in 20 s");
            Assert.That(s.Row(SessionWords.BasicShot).Damage, Is.EqualTo(50));
            Assert.That(s.Row(SessionWords.BasicShot).Hits, Is.EqualTo(1));
            Assert.That(s.NeverUsed, Is.EqualTo(new[] { Volley }));
            Assert.That(s.Rows.Select(r => r.Name), Is.EqualTo(new[] { Quick, Volley, SessionWords.BasicShot, SessionWords.Dash }),
                        "the skills in slot order, then the basic shot and the dash");
        }

        // ---------------- Gaps and pairs ----------------

        [Test]
        public void Gaps_MedianNinetiethPercentileAndTheLongOnes_InFights()
        {
            var events = new List<SessionEvent>
            {
                E(0, SessionEventTypes.EnemyWake),
                ShotStart(0), ShotStart(0.5), ShotStart(1.0), ShotStart(2.5), ShotStart(5.0),
                E(5, SessionEventTypes.EnemyDeath),
                End(10),
            };

            ScopeStats f = SessionSummary.Compute(events).InFights;

            // Gaps 0.5, 0.5, 1.5, 2.5.
            Assert.That(f.GapMedian, Is.EqualTo(1.0f).Within(1e-4f));
            Assert.That(f.GapP90, Is.EqualTo(2.2f).Within(1e-4f));
            Assert.That(f.GapsOver1, Is.EqualTo(2));
            Assert.That(f.GapsOver1Seconds, Is.EqualTo(4.0f).Within(1e-4f));
            Assert.That(f.GapsOver2, Is.EqualTo(1));
            Assert.That(f.GapsOver2Seconds, Is.EqualTo(2.5f).Within(1e-4f));
        }

        /// <summary>
        /// Holding or casting a skill is not time doing nothing: the long gaps are counted on the time she was free,
        /// from the moment the skill let her go. The rhythm (start to start) is unchanged.
        /// </summary>
        [Test]
        public void Gaps_TheLongOnes_AreCountedFromWhereASkillLetHerGo()
        {
            var events = new List<SessionEvent>
            {
                // Held for 1.6 s, then a shot 0.2 s after it let her go: 1.8 s start to start, 0.2 s free.
                SkillStart(1, Fan), new SessionEvent(2.6, SessionEventTypes.SkillEnd, Fan) { Held = 1.6f, Hit = true, Result = SessionWords.RanOut },
                ShotStart(2.8),
                // A quick skill that let her go at once, then nothing for 1.5 s: free for 1.4 s.
                SkillStart(3.0, Quick), new SessionEvent(3.1, SessionEventTypes.SkillEnd, Quick) { Hit = true, Result = SessionWords.RanOut },
                ShotStart(4.5),
                End(5),
            };

            ScopeStats s = SessionSummary.Compute(events).Session;

            // Gaps start to start: 1.8, 0.2, 1.5.
            Assert.That(s.GapMedian, Is.EqualTo(1.5f).Within(1e-4f), "the rhythm still counts the hold");
            Assert.That(s.GapsOver1, Is.EqualTo(1), "only the time after the quick skill was free for over a second");
            Assert.That(s.GapsOver1Seconds, Is.EqualTo(1.4f).Within(1e-4f));
        }

        [Test]
        public void Pairs_CountWhatFollowsWhat_MostCommonFirst()
        {
            var events = new List<SessionEvent>
            {
                SkillStart(1, Quick), ShotStart(1.5), SkillStart(6, Quick), ShotStart(6.5), DashStart(7), End(10),
            };

            List<KeyValuePair<string, int>> pairs = SessionSummary.Compute(events).Session.Pairs;

            Assert.That(pairs[0].Key, Is.EqualTo("Quick Shot then Basic shot"));
            Assert.That(pairs[0].Value, Is.EqualTo(2));
            Assert.That(pairs.Select(p => p.Key), Is.EquivalentTo(new[]
            {
                "Quick Shot then Basic shot", "Basic shot then Quick Shot", "Basic shot then Dash",
            }));
        }

        // ---------------- Fights ----------------

        [Test]
        public void Fights_RunFromTheWakeUntilAKillADeathOrTheEnemyGivesUp_AndTheSessionsEnd()
        {
            var events = new List<SessionEvent>
            {
                E(10, SessionEventTypes.EnemyWake), ShotStart(19), E(20, SessionEventTypes.EnemyDeath),
                ShotStart(25),
                E(30, SessionEventTypes.EnemyWake), ShotStart(31), E(35, SessionEventTypes.EnemyGaveUp),
                E(50, SessionEventTypes.EnemyWake), E(52, SessionEventTypes.PlayerDeath),
                E(53, SessionEventTypes.EnemyGaveUp), // after her death: no fight to end
                E(60, SessionEventTypes.EnemyWake),
                End(70),
            };

            SessionStats stats = SessionSummary.Compute(events);

            Assert.That(stats.Fights, Is.EqualTo(4));
            Assert.That(stats.FightSpans.Select(p => p.Key), Is.EqualTo(new[] { 10d, 30d, 50d, 60d }));
            Assert.That(stats.FightSpans.Select(p => p.Value), Is.EqualTo(new[] { 20d, 35d, 52d, 70d }), "the last ends with the session");
            Assert.That(stats.InFights.Seconds, Is.EqualTo(27f).Within(1e-4f));
            Assert.That(stats.Kills, Is.EqualTo(1));
            Assert.That(stats.Deaths, Is.EqualTo(1));
            Assert.That(stats.InFights.StartedActions, Is.EqualTo(2), "the shot between fights is not in one");
            Assert.That(stats.InFights.GapMedian, Is.NaN, "no gap is measured from one fight into the next");
            Assert.That(stats.Session.StartedActions, Is.EqualTo(3));
            Assert.That(stats.Session.GapsOver2, Is.EqualTo(2));
        }

        [Test]
        public void Fights_HoldTheEventsInTheirFirstAndLastMillisecond()
        {
            // Without a window frames are a fraction of a millisecond: the wake and the attack, the strike and her
            // death, can share their time in the log. (float)0.004 is a hair over 0.004: kept as floats, the bounds lost them.
            var events = new List<SessionEvent>
            {
                E(0.004, SessionEventTypes.EnemyWake), E(0.004, SessionEventTypes.EnemyAttack, "Slam"),
                new SessionEvent(0.905, SessionEventTypes.EnemyStrike, "Slam") { Result = SessionWords.Landed, Damage = 20 },
                new SessionEvent(0.905, SessionEventTypes.PlayerDamaged) { Damage = 20 },
                E(0.905, SessionEventTypes.PlayerDeath),
                End(1),
            };

            ScopeStats f = SessionSummary.Compute(events).InFights;

            Assert.That(f.EnemyAttacks, Is.EqualTo(1));
            Assert.That(f.StrikesLanded, Is.EqualTo(1));
            Assert.That(f.DamageTaken, Is.EqualTo(20));
        }

        [Test]
        public void EmptySession_StillGivesASummary()
        {
            SessionStats nothing = SessionSummary.Compute(new List<SessionEvent>());
            Assert.That(nothing.Length, Is.EqualTo(0f));
            Assert.That(nothing.Fights, Is.EqualTo(0));
            Assert.That(SessionSummary.Format(nothing), Does.Contain("no fight"));

            var events = new List<SessionEvent> { E(0, SessionEventTypes.SessionStart), Kit(0, Quick, "Normal", 4f), Kit(5, Volley, "Casting", 16f), End(30) };
            SessionStats quiet = SessionSummary.Compute(events);
            Assert.That(quiet.Length, Is.EqualTo(30f));
            Assert.That(quiet.Session.Apm, Is.EqualTo(0f));
            Assert.That(quiet.Session.NeverUsed, Is.EqualTo(new[] { Quick, Volley }));
            string text = SessionSummary.Format(quiet);
            Assert.That(text, Does.Contain("Never used: Quick Shot, Heavy Volley"));
            Assert.That(text, Does.Contain("no fight"));
        }

        // ---------------- Gauge, held and charged skills, defence, movement ----------------

        [Test]
        public void Gauge_FillsSpendsAndTheDamageWhileEmpowered()
        {
            var events = new List<SessionEvent>
            {
                E(10, SessionEventTypes.FocusFull), E(13, SessionEventTypes.FocusSpent),
                Hit(14, Quick, 150, empowered: true), Hit(15, Quick, 100),
                E(21, SessionEventTypes.EmpoweredEnd), E(41, SessionEventTypes.FocusFull),
                End(50),
            };

            ScopeStats s = SessionSummary.Compute(events).Session;

            Assert.That(s.FocusFilled, Is.EqualTo(2));
            Assert.That(s.FocusSpent, Is.EqualTo(1));
            Assert.That(s.FillSecondsMean, Is.EqualTo(15f).Within(1e-4f), "10 s from the start, 20 s from the end of the empowered mode");
            Assert.That(s.FullBeforeSpentMean, Is.EqualTo(3f).Within(1e-4f));
            Assert.That(s.EmpoweredDamage, Is.EqualTo(150));
        }

        [Test]
        public void HeldAndCharged_MedianHoldMedianChargeAndTheLowestCharges()
        {
            var events = new List<SessionEvent>
            {
                Kit(2, Fan, "Holding", 10f), Kit(3, Long, "Charge", 12f),
                new SessionEvent(5, SessionEventTypes.SkillEnd, Fan) { Held = 1.2f, Hit = true, Result = SessionWords.RanOut },
                new SessionEvent(20, SessionEventTypes.SkillEnd, Fan) { Held = 0.4f, Hit = true, Result = SessionWords.RanOut },
                new SessionEvent(8, SessionEventTypes.SkillEnd, Long) { Held = 0.05f, Charge = 0.05f, Hit = true, Result = SessionWords.RanOut },
                new SessionEvent(25, SessionEventTypes.SkillEnd, Long) { Held = 0.8f, Charge = 0.8f, Hit = true, Result = SessionWords.RanOut },
                new SessionEvent(40, SessionEventTypes.SkillEnd, Long) { Held = 0.3f, Charge = 0.3f, Hit = false, Result = SessionWords.CancelledByDash },
                End(50),
            };

            List<HoldRow> holds = SessionSummary.Compute(events).Session.Holds;
            HoldRow fan = holds.Single(h => h.Name == Fan);
            HoldRow lng = holds.Single(h => h.Name == Long);

            Assert.That(fan.Uses, Is.EqualTo(2));
            Assert.That(fan.HeldMedian, Is.EqualTo(0.8f).Within(1e-4f));
            Assert.That(lng.Uses, Is.EqualTo(3));
            Assert.That(lng.Fired, Is.EqualTo(2), "the one cancelled before it fired does not count");
            Assert.That(lng.ChargeMedian, Is.EqualTo(0.425f).Within(1e-4f));
            Assert.That(lng.AtLowestCharge, Is.EqualTo(1));
        }

        [Test]
        public void Defence_StrikesByOutcome_DamageTaken_AndDashesThatAvoidedOne()
        {
            var events = new List<SessionEvent>
            {
                E(4, SessionEventTypes.EnemyAttack, "Slam"), DashStart(5),
                new SessionEvent(5.05, SessionEventTypes.EnemyStrike, "Slam") { Result = SessionWords.AvoidedByDash },
                E(10, SessionEventTypes.EnemyAttack, "Slam"),
                new SessionEvent(11, SessionEventTypes.EnemyStrike, "Slam") { Result = SessionWords.Landed, Damage = 20 },
                new SessionEvent(11, SessionEventTypes.PlayerDamaged) { Damage = 20 },
                E(19, SessionEventTypes.EnemyAttack, "Shockwave"),
                new SessionEvent(20, SessionEventTypes.EnemyStrike, "Shockwave") { Result = SessionWords.OutOfArea },
                DashStart(30),
                End(40),
            };

            ScopeStats s = SessionSummary.Compute(events).Session;

            Assert.That(s.EnemyAttacks, Is.EqualTo(3));
            Assert.That(s.StrikesLanded, Is.EqualTo(1));
            Assert.That(s.StrikesAvoidedByDash, Is.EqualTo(1));
            Assert.That(s.StrikesOutOfArea, Is.EqualTo(1));
            Assert.That(s.DamageTaken, Is.EqualTo(20));
            Assert.That(s.Dashes, Is.EqualTo(2));
            Assert.That(s.DashesThatAvoided, Is.EqualTo(1));
        }

        [Test]
        public void Movement_IsTheShareOfSamplesOnTheMove()
        {
            var events = new List<SessionEvent>();
            for (int i = 0; i < 8; i++) events.Add(new SessionEvent(i * 0.25, SessionEventTypes.Move) { Moving = i % 2 == 0 });
            events.Add(End(2));
            Assert.That(SessionSummary.Compute(events).Session.MovingShare, Is.EqualTo(0.5f).Within(1e-4f));
        }

        // ---------------- The kit: two recast skills, two spenders (amended 2026-11-19) ----------------

        [Test]
        public void Kit_ChainsCleanPressesAndSpenderHits_ArePerSkill_TripleTapAndBackstep()
        {
            const string Blast = "Scatter Blast", Step = "Backstep", Tap = "Triple Tap";
            var start = E(0, SessionEventTypes.SessionStart);
            start.AddExtra("kit.MaxMarks", "5");
            SessionEvent blastKit = Kit(4, Blast, "Normal", 6f);
            blastKit.AddExtra("mark_bonus", "0.25");
            SessionEvent stepKit = Kit(6, Step, "Normal", 10f);
            stepKit.AddExtra("recast_count", "2");
            stepKit.AddExtra("opening_move", "true");
            stepKit.AddExtra("clean_mark_bonus", "0.5");
            SessionEvent tapKit = Kit(7, Tap, "Normal", 5f);
            tapKit.AddExtra("recast_count", "3");
            SessionEvent Recast(double t, string name, int shot, bool clean) =>
                new SessionEvent(t, SessionEventTypes.RecastPress, name) { Shot = shot, Clean = clean, Focus = clean ? 5f : (float?)null };
            SessionEvent ChainEnd(double t, string name, int presses, bool early) =>
                new SessionEvent(t, SessionEventTypes.ChainEnd, name) { Shot = presses, Result = early ? SessionWords.Early : SessionWords.Complete };
            SessionEvent Spent(double t, string name, int marks) =>
                new SessionEvent(t, SessionEventTypes.MarksSpent, name) { Marks = marks, Damage = 100, Focus = marks * 5f };

            var events = new List<SessionEvent>
            {
                start, blastKit, stepKit, tapKit,
                // Triple Tap: one chain of three, the last press clean.
                SkillStart(1, Tap), Recast(1.5, Tap, 2, false), SkillStart(1.5, Tap), Recast(2.2, Tap, 3, true), SkillStart(2.2, Tap), ChainEnd(2.4, Tap, 3, false),
                // Backstep: the hop and no second press; then the hop and a clean shot that took five marks; then an early shot.
                SkillStart(3, Step), ChainEnd(3.8, Step, 1, true),
                SkillStart(13, Step), Recast(13.6, Step, 2, true), SkillStart(13.6, Step), Spent(13.8, Step, 5), ChainEnd(13.8, Step, 2, false),
                SkillStart(23, Step), Recast(23.3, Step, 2, false), SkillStart(23.3, Step), ChainEnd(23.5, Step, 2, false),
                // Scatter Blast: two hits, three and five marks.
                SkillStart(5, Blast), Spent(5.2, Blast, 3), SkillStart(12, Blast), Spent(12.2, Blast, 5),
                End(30),
            };

            KitStats k = SessionSummary.Compute(events).Kit;

            Assert.That(k.Skills.Select(r => r.Name), Is.EqualTo(new[] { Blast, Step, Tap }), "in slot order");
            KitSkillRow tap = k.Skill(Tap), step = k.Skill(Step), blast = k.Skill(Blast);
            Assert.That(tap.ChainsByLength.Select(p => p.Key + ":" + p.Value), Is.EqualTo(new[] { "3:1" }));
            Assert.That(step.ChainsByLength.Select(p => p.Key + ":" + p.Value), Is.EqualTo(new[] { "1:1", "2:2" }), "one hop alone, two hops and a shot");
            Assert.That(tap.LastPresses, Is.EqualTo(1));
            Assert.That(tap.CleanPresses, Is.EqualTo(1));
            Assert.That(step.LastPresses, Is.EqualTo(2), "the second press is Backstep's last");
            Assert.That(step.CleanPresses, Is.EqualTo(1));
            Assert.That(step.SpenderHits, Is.EqualTo(1));
            Assert.That(step.MarksPerSpenderHit, Is.EqualTo(5f));
            Assert.That(step.SpenderHitsAtFive, Is.EqualTo(1));
            Assert.That(step.CleanSpend, Is.True);
            Assert.That(blast.SpenderHits, Is.EqualTo(2));
            Assert.That(blast.MarksPerSpenderHit, Is.EqualTo(4f));
            Assert.That(blast.SpenderHitsAtFive, Is.EqualTo(1));
            Assert.That(blast.CleanSpend, Is.False);
            Assert.That(k.SpenderHits, Is.EqualTo(3), "in all");
            Assert.That(k.LastPresses, Is.EqualTo(3));
            Assert.That(k.CleanPresses, Is.EqualTo(2));
            Assert.That(k.FocusFromMarks, Is.EqualTo(65f).Within(1e-4f));
            Assert.That(k.FocusFromCleanTaps, Is.EqualTo(10f).Within(1e-4f), "both clean presses");

            string text = SessionSummary.Format(SessionSummary.Compute(events));
            TestContext.Out.WriteLine(text);
            Assert.That(text, Does.Contain("  Backstep (clean presses)"));
            Assert.That(text, Does.Match(@"\n  Backstep +1: 1   2: 2\r?\n"), "Backstep's chains on a line of their own");
            Assert.That(text, Does.Match(@"\n  Triple Tap +3: 1\r?\n"));
            Assert.That(text, Does.Match(@"\n  Backstep +1 of 2 \(50%\)\r?\n"));
            Assert.That(text, Does.Match(@"\n  Scatter Blast +2, 4\.0 / 50%\r?\n"));
        }

        // ---------------- The log ----------------

        [Test]
        public void Event_WrittenAsJson_ReadsBackTheSame()
        {
            var e = new SessionEvent(12.3456, SessionEventTypes.SkillPress, "Say \"hi\"\\")
            {
                Slot = 3, Result = SessionWords.Rejected, Reason = SessionWords.Cooldown, CooldownLeft = 2.5f, Damage = 7,
                Target = SessionWords.Enemy, Empowered = false, Hit = true, Moving = false, Held = 0.75f, Charge = 0.5f,
                Cooldown = 12f, Input = "Charge",
            };
            e.AddExtra("feel.RunSpeed", "6");

            SessionEvent back = SessionEvent.Parse(e.ToJson());

            Assert.That(back, Is.Not.Null, e.ToJson());
            Assert.That(back.T, Is.EqualTo(12.346).Within(1e-9), "written to the millisecond");
            Assert.That(back.Type, Is.EqualTo(e.Type));
            Assert.That(back.Name, Is.EqualTo(e.Name));
            Assert.That(back.Slot, Is.EqualTo(3));
            Assert.That(back.Result, Is.EqualTo(e.Result));
            Assert.That(back.Reason, Is.EqualTo(e.Reason));
            Assert.That(back.CooldownLeft, Is.EqualTo(2.5f));
            Assert.That(back.Damage, Is.EqualTo(7));
            Assert.That(back.Target, Is.EqualTo(e.Target));
            Assert.That(back.Empowered, Is.False);
            Assert.That(back.Hit, Is.True);
            Assert.That(back.Moving, Is.False);
            Assert.That(back.Held, Is.EqualTo(0.75f));
            Assert.That(back.Charge, Is.EqualTo(0.5f));
            Assert.That(back.Cooldown, Is.EqualTo(12f));
            Assert.That(back.Input, Is.EqualTo("Charge"));
            Assert.That(back.ExtraValue("feel.RunSpeed"), Is.EqualTo("6"));
            Assert.That(new SessionEvent(1, SessionEventTypes.Move).ToJson(), Is.EqualTo("{\"t\":1.000,\"type\":\"move\"}"), "unset fields are not written");
            Assert.That(SessionEvent.Parse("not json"), Is.Null);
            Assert.That(SessionEvent.Parse(""), Is.Null);
        }

        [Test]
        public void WriteFromLog_RecomputesTheSummaryNextToTheLog()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SessionSummaryTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            try
            {
                string log = Path.Combine(folder, "2026-11-19_10-00-00.jsonl");
                var start = E(0, SessionEventTypes.SessionStart);
                start.AddExtra("date", "2026-11-19 10:00:00");
                start.AddExtra("commit", "abcdef1234567890");
                var events = new List<SessionEvent>
                {
                    start, Kit(0, Quick, "Normal", 4f), Kit(5, Volley, "Casting", 16f),
                    Press(1, Quick, SessionWords.Started), SkillStart(1, Quick), Hit(1.2, Quick, 100),
                    E(2, SessionEventTypes.Click, SessionWords.BasicShot), ShotStart(2), End(30),
                };
                File.WriteAllLines(log, events.Select(e => e.ToJson()).Concat(new[] { "", "a line that is not an event" }));

                string summary = SessionSummary.WriteFromLog(log);

                Assert.That(summary, Is.EqualTo(Path.Combine(folder, "2026-11-19_10-00-00-summary.txt")));
                string text = File.ReadAllText(summary);
                Assert.That(text, Does.Contain("2026-11-19 10:00:00"));
                Assert.That(text, Does.Contain("abcdef1234"));
                Assert.That(text, Does.Contain("Quick Shot"));
                Assert.That(text, Does.Contain("Never used: Heavy Volley"));
                Assert.That(text, Is.EqualTo(SessionSummary.Format(SessionSummary.Compute(events), Path.GetFileName(log))),
                            "the file gives the same summary as the events it was written from");
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }

        // ---------------- When it may start by itself ----------------

        [Test]
        public void Rule_NeverInBatchMode_NorInTheTestRunnersScene_NorForACharacterShutOffFromTheDevices()
        {
            Assert.That(SessionRecordingRule.MayStartByItself("ControlRoom", batchMode: false), Is.True);
            Assert.That(SessionRecordingRule.MayStartByItself("ControlRoom", batchMode: true), Is.False);
            Assert.That(SessionRecordingRule.MayStartByItself("InitTestScene638672345678901234", batchMode: false), Is.False);
            Assert.That(SessionRecordingRule.MayRecordCharacter(readsRealDevices: false), Is.False);
            Assert.That(SessionRecordingRule.MayRecordCharacter(readsRealDevices: true), Is.True);
        }
    }
}
