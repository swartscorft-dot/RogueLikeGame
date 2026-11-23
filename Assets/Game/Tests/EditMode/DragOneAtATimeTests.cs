using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// One guard drags at a time (work order 093; Kristian 2026-11-23: "Én drar om gangen"), the rules: with two and with three
    /// guards in reach and every Drag ready only one commits; the next cannot begin until <see cref="MeleeAttacks.DragOneAtATime"/>
    /// after the first ended, whichever way it ended; the same guard still waits its own cooldown; a guard held back chooses as
    /// today's rules do without its Drag; the value at 0 gives today's choice; the hold clears; and what the session summary says.
    /// The guards here are their tactics (<see cref="MeleeTactics"/>) asked with the hold, as <c>MeleeEnemy</c> asks them.
    /// </summary>
    public class DragOneAtATimeTests
    {
        static EnemyAttackDefinition[] Set => MeleeAttacks.Guard();
        static EnemyAttackDefinition DragOf(EnemyAttackDefinition[] set) => MeleeAttacks.Find(set, MeleeAttacks.Drag);
        static float Longest(EnemyAttackDefinition drag) => drag.Windup + drag.MoveSeconds;

        /// <summary>What guard <paramref name="id"/> chooses now at that distance, the hold asked as the game asks it.</summary>
        static EnemyAttackDefinition Choose(MeleeTactics t, DragHold hold, int id, float now, float distance, EnemyAttackDefinition[] set) =>
            t.Choose(now, distance, set, a => hold.Allows(id, a, now));

        /// <summary>It begins what it chose: its cooldown from now and, for a Drag, the hold.</summary>
        static void Begin(MeleeTactics t, DragHold hold, int id, EnemyAttackDefinition a, float now)
        {
            t.Began(a, now);
            if (a.Move == MeleeMove.Drag) hold.Began(id, now, Longest(a));
        }

        [Test]
        public void TheValue_Is4Seconds_BesideTheDrag_AndTheHoldStartsWithIt()
        {
            Assert.That(MeleeAttacks.DragOneAtATime, Is.EqualTo(4f));
            Assert.That(new DragHold().Gap, Is.EqualTo(MeleeAttacks.DragOneAtATime));
            EnemyAttackDefinition drag = DragOf(Set);
            Assert.That(drag.Cooldown, Is.EqualTo(9f), "the guard's own numbers are not this order's");
            Assert.That(drag.Windup, Is.EqualTo(0.9f));
            Assert.That(drag.MoveSeconds, Is.EqualTo(0.3f));
        }

        [TestCase(2)]
        [TestCase(3)]
        public void GuardsInReach_EveryDragReady_OnlyOneCommits_TheFirstToAsk(int guards)
        {
            var hold = new DragHold();
            EnemyAttackDefinition[] set = Set;
            var tactics = Enumerable.Range(0, guards).Select(_ => new MeleeTactics()).ToList();
            const float now = 10f, distance = 6f; // in every guard's Drag band, and in nothing else's
            var chosen = new List<EnemyAttackDefinition>();
            for (int i = 0; i < guards; i++)
            {
                // One after another in one frame, as the game steps them: each asks and commits in its own step.
                EnemyAttackDefinition a = Choose(tactics[i], hold, i + 1, now, distance, set);
                chosen.Add(a);
                if (a != null) Begin(tactics[i], hold, i + 1, a, now);
            }
            Assert.That(chosen[0]?.Name, Is.EqualTo(MeleeAttacks.Drag), "the first to ask drags");
            Assert.That(chosen.Count(a => a != null && a.Move == MeleeMove.Drag), Is.EqualTo(1), "only one commits");
            Assert.That(chosen.Skip(1), Is.All.Null, "the others have nothing else at 6 m: they keep coming in the chase");
            Assert.That(hold.Holder, Is.EqualTo(1));
            Assert.That(tactics.Skip(1).All(t => t.IsReady(DragOf(set), now)), Is.True, "their Drags are not spent: still ready by their own cooldowns");
        }

        /// <summary>How the first guard's Drag ends, in seconds after its commit: and whether the game says so, or it runs to its latest end.</summary>
        public enum Ending { Landed, Missed, BrokenByCover, AvoidedByTheDash, StaggeredOutOfIt, Died, NeverSaid }

        [TestCase(Ending.Landed)]
        [TestCase(Ending.Missed)]
        [TestCase(Ending.BrokenByCover)]
        [TestCase(Ending.AvoidedByTheDash)]
        [TestCase(Ending.StaggeredOutOfIt)]
        [TestCase(Ending.Died)]
        [TestCase(Ending.NeverSaid)]
        public void TheNext_CannotBeginUntil4sAfterTheFirstEnded_WhicheverWayItEnded(Ending ending)
        {
            var hold = new DragHold();
            EnemyAttackDefinition drag = DragOf(Set);
            const float t0 = 20f;
            hold.Began(1, t0, Longest(drag));
            Assert.That(hold.Running(t0), Is.True);
            // The strike is at the wind-up's end; a landed Drag ends with her pull; a daze or a death drops it mid-wind-up.
            float strike = t0 + drag.Windup;
            float ended = ending == Ending.Landed ? strike + drag.MoveSeconds
                        : ending == Ending.StaggeredOutOfIt || ending == Ending.Died ? t0 + 0.4f
                        : ending == Ending.NeverSaid ? t0 + Longest(drag)
                        : strike;
            if (ending != Ending.NeverSaid) hold.Ended(1, ended);

            Assert.That(hold.MayBegin(2, t0 + 0.1f), Is.False, "never while it winds up: two never wind up a Drag together");
            Assert.That(hold.MayBegin(2, ended + 0.01f), Is.False, ending + ": not just after it ended");
            Assert.That(hold.MayBegin(3, ended + MeleeAttacks.DragOneAtATime - 0.01f), Is.False, ending + ": not before 4 s");
            Assert.That(hold.MayBegin(2, ended + MeleeAttacks.DragOneAtATime), Is.True, ending + ": at 4 s after it ended");
            Assert.That(hold.MayBegin(3, ended + MeleeAttacks.DragOneAtATime), Is.True);
            Assert.That(hold.FreeAt, Is.EqualTo(ended + MeleeAttacks.DragOneAtATime).Within(1e-4f));
        }

        [Test]
        public void TheSameGuard_StillWaitsItsOwn9s()
        {
            var hold = new DragHold();
            EnemyAttackDefinition[] set = Set;
            EnemyAttackDefinition drag = DragOf(set);
            var mine = new MeleeTactics();
            Assert.That(Choose(mine, hold, 1, 0f, 6f, set), Is.SameAs(drag));
            Begin(mine, hold, 1, drag, 0f);
            hold.Ended(1, drag.Windup + drag.MoveSeconds);
            Assert.That(hold.MayBegin(1, 1.5f), Is.True, "the hold never holds back the guard that holds it");
            // Its own cooldown does: no sooner than 9 s after its last began, with the hold free long before.
            Assert.That(Choose(mine, hold, 1, 5.3f, 6f, set), Is.Null, "free of the hold, still on its own cooldown");
            Assert.That(Choose(mine, hold, 1, 8.99f, 6f, set), Is.Null);
            Assert.That(Choose(mine, hold, 1, 9f, 6f, set), Is.SameAs(drag), "at its own 9 s");
            // Another guard, ready, may at 4 s after the first ended; not before.
            var other = new MeleeTactics();
            Assert.That(Choose(other, hold, 2, 5.19f, 6f, set), Is.Null);
            Assert.That(Choose(other, hold, 2, 5.2f, 6f, set), Is.SameAs(drag));
        }

        /// <summary>
        /// A guard held back chooses as today's rules choose for it with no Drag allowed: in reach its Fist, or its Crush when that is
        /// ready (as any skill is, with no Drag before it); in the Drag's band nothing, and it keeps coming. With the value at 0 it
        /// chooses exactly as before work order 093, at every distance and whatever it did last.
        /// </summary>
        [Test]
        public void AHeldBackGuard_ChoosesItsOtherAttacksByTodaysRules_AndTheValueAt0_GivesTodaysChoice()
        {
            EnemyAttackDefinition[] set = Set;
            var held = new DragHold();
            held.Began(9, 0f, 1.2f); // another guard holds it
            var off = new DragHold { Gap = 0f };
            off.Began(9, 0f, 1.2f);
            var lines = new List<string>();
            for (int history = 0; history < 4; history++)
                for (float d = 0f; d <= 10.01f; d += 0.25f)
                {
                    var a = new MeleeTactics();
                    var b = new MeleeTactics();
                    var today = new MeleeTactics();
                    var noDrag = new MeleeTactics();
                    foreach (MeleeTactics t in new[] { a, b, today, noDrag })
                    {
                        // What it did before: nothing; a Fist; a Crush just now (on its cooldown); a Drag 1 s ago (the Crush follows).
                        if (history == 1) t.Began(MeleeAttacks.Find(set, MeleeAttacks.Fist), -0.5f);
                        if (history == 2) t.Began(MeleeAttacks.Find(set, MeleeAttacks.Crush), -0.5f);
                        if (history == 3) t.Began(DragOf(set), -1f);
                    }
                    EnemyAttackDefinition withHold = Choose(a, held, 1, 0.5f, d, set);
                    EnemyAttackDefinition withOff = Choose(b, off, 1, 0.5f, d, set);
                    EnemyAttackDefinition before = today.Choose(0.5f, d, set);
                    // Today's choice with no Drag allowed: what a held-back guard must choose.
                    EnemyAttackDefinition withoutDrag = noDrag.Choose(0.5f, d, set, x => x.Move != MeleeMove.Drag);

                    Assert.That(withOff, Is.SameAs(before), "the value at 0: today's choice at " + d + " m, history " + history);
                    Assert.That(withHold, Is.SameAs(withoutDrag), "held back: today's choice without its Drag at " + d + " m, history " + history);
                    Assert.That(withHold?.Move ?? MeleeMove.None, Is.Not.EqualTo(MeleeMove.Drag));
                    if (before != null && before.Move == MeleeMove.Drag) lines.Add(d + " m: " + (withHold?.Name ?? "keeps coming"));
                }
            Assert.That(lines, Is.Not.Empty, "the Drag would have been chosen somewhere");
            TestContext.WriteLine("[093] where today's guard drags, one held back does: " + string.Join("; ", lines.Distinct()));
            // In reach, the Fist (the Crush when it is ready), as today.
            Assert.That(Choose(new MeleeTactics(), held, 1, 0.5f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Crush), "a ready Crush in its reach, as today");
            var afterCrush = new MeleeTactics();
            afterCrush.Began(MeleeAttacks.Find(set, MeleeAttacks.Crush), 0f);
            Assert.That(Choose(afterCrush, held, 1, 0.5f, 2f, set).Name, Is.EqualTo(MeleeAttacks.Fist));
        }

        /// <summary>
        /// Three guards standing 6 m from her for 30 s, each stepped in turn every 20 ms as the game steps them, every Drag landing:
        /// no two Drags land closer than the value and a wind-up; every guard gets its turn; with the value at 0, they stack as
        /// they did on floor 3.
        /// </summary>
        [TestCase(4f)]
        [TestCase(0f)]
        public void ThreeGuardsFor30s_NoTwoDragsLandCloserThanTheValue(float gap)
        {
            var hold = new DragHold { Gap = gap };
            EnemyAttackDefinition[] set = Set;
            EnemyAttackDefinition drag = DragOf(set);
            var tactics = new[] { new MeleeTactics(), new MeleeTactics(), new MeleeTactics() };
            var windupFrom = new float?[3];
            var recoverFrom = new float?[3];
            var landings = new List<KeyValuePair<float, int>>();
            for (int step = 0; step <= 1500; step++)
            {
                float now = step * 0.02f;
                for (int i = 0; i < 3; i++)
                {
                    int id = i + 1;
                    if (windupFrom[i].HasValue && now - windupFrom[i].Value >= drag.Windup)
                    {
                        landings.Add(new KeyValuePair<float, int>(now, id));
                        hold.Ended(id, now + drag.MoveSeconds); // it landed: it ends with her pull
                        windupFrom[i] = null;
                        recoverFrom[i] = now;
                    }
                    if (recoverFrom[i].HasValue && now - recoverFrom[i].Value >= drag.Recovery) recoverFrom[i] = null;
                    if (windupFrom[i].HasValue || recoverFrom[i].HasValue) continue;
                    EnemyAttackDefinition a = Choose(tactics[i], hold, id, now, 6f, set);
                    if (a == null) continue;
                    Begin(tactics[i], hold, id, a, now);
                    windupFrom[i] = now;
                }
            }
            List<float> gaps = landings.Zip(landings.Skip(1), (x, y) => y.Key - x.Key).ToList();
            TestContext.WriteLine("[093] gap " + gap + ": " + landings.Count + " Drags landed, at " + string.Join(", ", landings.Select(p => p.Key.ToString("0.00") + " (" + p.Value + ")")));
            if (gap > 0f)
            {
                Assert.That(gaps.Min(), Is.GreaterThanOrEqualTo(gap + drag.MoveSeconds + drag.Windup - 1e-3f), "none closer than the hold and the next wind-up");
                // The first ready guard to ask takes it: here guards 1 and 2 by turns, each on its own 9 s; guard 3, asking last,
                // fights with its Fist and Crush meanwhile (in the game, the order the enemies are stepped in).
                Assert.That(landings.Select(p => p.Value).Distinct().Count(), Is.GreaterThanOrEqualTo(2), "more than one guard drags");
                Assert.That(landings.Count, Is.InRange(5, 7), "about one every 5.2 s");
            }
            else
            {
                Assert.That(gaps.Min(), Is.LessThan(MeleeAttacks.DragOneAtATime), "off: the pulls stack as before");
                Assert.That(landings.Count, Is.InRange(11, 12), "each guard every 9 s, as its own cooldown lets it");
            }
        }

        [Test]
        public void TheHold_Clears_OnClear_AndWithTheGuardThatHoldsIt_ItsNumberRunsOn()
        {
            var hold = new DragHold();
            hold.Began(4, 1f, 1.2f);
            int number = hold.Number;
            Assert.That(hold.MayBegin(5, 2f), Is.False);
            hold.Clear(); // a new floor, her death, a new run
            Assert.That(hold.MayBegin(5, 2f), Is.True);
            Assert.That(hold.Holder, Is.EqualTo(0));
            Assert.That(hold.Number, Is.EqualTo(number), "the number runs on: a guard noted held back is noted again by the next hold");

            hold.Began(4, 3f, 1.2f);
            Assert.That(hold.Number, Is.EqualTo(number + 1));
            hold.Forget(5);
            Assert.That(hold.MayBegin(5, 3.5f), Is.False, "another one gone: the hold stays");
            hold.Forget(4);
            Assert.That(hold.MayBegin(5, 3.5f), Is.True, "the one that holds it gone with its level: the hold goes with it");
        }

        [Test]
        public void OnlyTheHoldersEnd_Counts_Once_AndNeverLaterThanItsLatest()
        {
            var hold = new DragHold();
            hold.Began(1, 0f, 1.2f);
            hold.Ended(2, 0.1f); // not the holder's
            Assert.That(hold.Running(0.5f), Is.True);
            hold.Ended(1, 5f); // later than it can last
            Assert.That(hold.EndsAt, Is.EqualTo(1.2f).Within(1e-5f));
            hold.Ended(1, 0.3f); // ended already
            Assert.That(hold.EndsAt, Is.EqualTo(1.2f).Within(1e-5f));
            Assert.That(hold.Running(0.5f), Is.False);
            Assert.That(hold.MayBegin(2, 5.19f), Is.False);
            Assert.That(hold.MayBegin(2, 5.2f), Is.True);
            Assert.That(new DragHold { Gap = 0f }.FreeAt, Is.EqualTo(float.NegativeInfinity));
            var off = new DragHold { Gap = 0f };
            off.Began(1, 0f, 1.2f);
            Assert.That(off.MayBegin(2, 0.1f), Is.True, "the value at 0: no hold at all");
            Assert.That(hold.Allows(2, MeleeAttacks.Find(Set, MeleeAttacks.Fist), 2f), Is.True, "only a Drag is held");
            Assert.That(hold.Allows(2, MeleeAttacks.Find(Set, MeleeAttacks.Crush), 2f), Is.True);
            Assert.That(hold.Allows(2, null, 2f), Is.True);
        }

        // ---------------- The session summary ----------------

        [Test]
        public void TheSummary_GivesTheDragsLanded_HeldBack_AndTheShortestGap_BesideThePulls()
        {
            SessionEvent Pull(double t) => new SessionEvent(t, SessionEventTypes.PlayerPulled, MeleeAttacks.Drag);
            SessionEvent HeldBack(double t, int id, int by)
            {
                var e = new SessionEvent(t, SessionEventTypes.DragHeldBack, MeleeAttacks.Drag) { Enemy = "guard", EnemyId = id };
                e.AddExtra("held_by", by.ToString());
                e.AddExtra("seconds", "4");
                return e;
            }
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                new SessionEvent(5, SessionEventTypes.EnemyWake) { EnemyId = 1, Enemy = "guard" },
                Pull(10),
                HeldBack(10.2, 2, 1),
                HeldBack(10.3, 3, 1),
                Pull(15.5),
                new SessionEvent(16, SessionEventTypes.PlayerPulled, "test"), // not a Drag's: a pull, not a Drag landed
                Pull(30),
                new SessionEvent(40, SessionEventTypes.EnemyDeath) { EnemyId = 1, Enemy = "guard" },
                new SessionEvent(50, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Session.Pulls, Is.EqualTo(4));
            Assert.That(stats.Session.DragsLanded, Is.EqualTo(3));
            Assert.That(stats.Session.DragsHeldBack, Is.EqualTo(2));
            Assert.That(stats.Session.DragGapShortest, Is.EqualTo(5.5f).Within(1e-4f));
            string text = SessionSummary.Format(stats);
            TestContext.WriteLine(text);
            StringAssert.Contains("Stunned / pulled", text);
            StringAssert.Contains("Drags landed / held back by the rule", text);
            StringAssert.Contains("3 / 2", text);
            StringAssert.Contains("shortest between two Drags landing", text);
            StringAssert.Contains("5.50 s", text);

            // Written and read back, the same summary.
            var read = events.Select(e => SessionEvent.Parse(e.ToJson())).ToList();
            Assert.That(SessionSummary.Format(SessionSummary.Compute(read)), Is.EqualTo(text));

            // One Drag: no gap; a log with no pull: no lines of them.
            SessionStats one = SessionSummary.Compute(events.Where(e => e.Type != SessionEventTypes.PlayerPulled || e.T == 10).ToList());
            Assert.That(float.IsNaN(one.Session.DragGapShortest), Is.True);
            string none = SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Type != SessionEventTypes.PlayerPulled && e.Type != SessionEventTypes.DragHeldBack).ToList()));
            Assert.That(none, Does.Not.Contain("Drags landed"));
        }
    }
}
