using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 023 (2026-11-20), the first balance pass: the turns to attack (the caps, the gap, the one that has
    /// waited longest goes first, light enemies and the boss never held, off gives everyone a turn at once) and the
    /// floor heal's amount.
    /// </summary>
    public class BalanceRulesTests
    {
        const float Frame = 1f / 60f;

        // ---------------- The turns to attack ----------------

        [Test]
        public void Turns_TheSorts_ByKind()
        {
            Assert.That(AttackTurns.SortOf(EnemyKind.Brute), Is.EqualTo(TurnSort.Heavy));
            Assert.That(AttackTurns.SortOf(EnemyKind.Guard), Is.EqualTo(TurnSort.Heavy));
            Assert.That(AttackTurns.SortOf(EnemyKind.Shooter), Is.EqualTo(TurnSort.Ranged));
            Assert.That(AttackTurns.SortOf(EnemyKind.Swarmer), Is.EqualTo(TurnSort.Light));
            Assert.That(AttackTurns.SortOf(EnemyKind.Boss), Is.EqualTo(TurnSort.None));
            var v = new TurnValues();
            Assert.That(v.On, Is.True);
            Assert.That(v.HeavyAtOnce, Is.EqualTo(2));
            Assert.That(v.RangedAtOnce, Is.EqualTo(2));
            Assert.That(v.Gap, Is.EqualTo(0.5f));
        }

        /// <summary>Ten of a sort ask every frame for ten seconds; each begins a wind-up of this length when it may.</summary>
        static List<(int id, float at)> Crowd(AttackTurns turns, TurnSort sort, int count, float windup, float seconds = 10f, float recovery = 1f)
        {
            var began = new List<(int, float)>();
            var busyUntil = new float[count];
            for (float now = 0f; now < seconds; now += Frame)
            {
                for (int id = 1; id <= count; id++)
                {
                    if (now < busyUntil[id - 1]) continue;
                    if (!turns.Ask(id, sort, now)) continue;
                    turns.Began(id, sort, now, windup);
                    busyUntil[id - 1] = now + windup + recovery;
                    began.Add((id, now));
                }
            }
            return began;
        }

        static int MostAtOnce(List<(int id, float at)> began, float windup)
        {
            int most = 0;
            foreach ((int _, float at) in began)
                most = System.Math.Max(most, began.Count(b => b.at <= at && at < b.at + windup));
            return most;
        }

        [Test]
        public void Turns_AtMostTwoHeaviesAndTwoRanged_WindUpAtOnce()
        {
            var turns = new AttackTurns();
            List<(int id, float at)> heavy = Crowd(turns, TurnSort.Heavy, 6, 1.1f);
            Assert.That(MostAtOnce(heavy, 1.1f), Is.EqualTo(2), "two heavies at once at most, and two do");
            var turns2 = new AttackTurns();
            List<(int id, float at)> ranged = Crowd(turns2, TurnSort.Ranged, 6, 0.9f);
            Assert.That(MostAtOnce(ranged, 0.9f), Is.EqualTo(2), "two ranged at once at most, and two do");
        }

        [Test]
        public void Turns_TheSortsAreCountedApart()
        {
            var turns = new AttackTurns();
            Assert.That(turns.Ask(1, TurnSort.Heavy, 0f), Is.True);
            turns.Began(1, TurnSort.Heavy, 0f, 1f);
            Assert.That(turns.Ask(2, TurnSort.Ranged, 0f), Is.True, "a heavy's gap does not hold a ranged one");
            turns.Began(2, TurnSort.Ranged, 0f, 1f);
            Assert.That(turns.WindingUp(TurnSort.Heavy, 0.1f), Is.EqualTo(1));
            Assert.That(turns.WindingUp(TurnSort.Ranged, 0.1f), Is.EqualTo(1));
        }

        [Test]
        public void Turns_ANewWindupOfASort_NoSoonerThanTheGap()
        {
            var turns = new AttackTurns();
            List<(int id, float at)> began = Crowd(turns, TurnSort.Heavy, 6, 1.1f);
            for (int i = 1; i < began.Count; i++)
                Assert.That(began[i].at - began[i - 1].at, Is.GreaterThanOrEqualTo(0.5f - 1e-4f), "the gap between two beginnings");
            Assert.That(turns.Ask(1, TurnSort.Heavy, 100f), Is.True);
            turns.Began(1, TurnSort.Heavy, 100f, 0.1f);
            Assert.That(turns.Ask(2, TurnSort.Heavy, 100.3f), Is.False, "the first's wind-up is over, but the gap is not");
            Assert.That(turns.Ask(2, TurnSort.Heavy, 100.5f), Is.True, "the gap is over");
        }

        [Test]
        public void Turns_TheOneThatHasWaitedLongest_GoesFirst()
        {
            var turns = new AttackTurns();
            turns.Began(9, TurnSort.Heavy, 0f, 2f);
            turns.Began(8, TurnSort.Heavy, 0f, 2f);
            // Three wait, from different times; each asks every frame, the youngest first in a frame.
            float t = 0f;
            for (; t < 1.0f; t += Frame)
            {
                if (t >= 0.6f) Assert.That(turns.Ask(3, TurnSort.Heavy, t), Is.False);
                if (t >= 0.3f) Assert.That(turns.Ask(2, TurnSort.Heavy, t), Is.False);
                Assert.That(turns.Ask(1, TurnSort.Heavy, t), Is.False, "both turns are taken");
            }
            turns.Ended(9); // a turn is free: one ends early (a daze)
            Assert.That(turns.Ask(3, TurnSort.Heavy, t), Is.False, "the youngest waits");
            Assert.That(turns.Ask(2, TurnSort.Heavy, t), Is.False, "and so does the middle one");
            Assert.That(turns.Ask(1, TurnSort.Heavy, t), Is.True, "the one that has waited longest goes first");
            turns.Began(1, TurnSort.Heavy, t, 2f);
            Assert.That(turns.IsWaiting(1, t), Is.False, "it waits no more");
            Assert.That(turns.WaitingSince(2, t), Is.EqualTo(0.3f).Within(Frame));
        }

        [Test]
        public void Turns_OneThatStopsAsking_HoldsNoTurnBack()
        {
            var turns = new AttackTurns();
            turns.Began(9, TurnSort.Ranged, 0f, 1f);
            turns.Began(8, TurnSort.Ranged, 0f, 1f);
            // 1 waits from 0.1 s and stops asking at 0.9 s (it lost sight of her); 2 waits from 0.2 s and goes on asking.
            float t = 0.1f;
            for (; t < 0.9f; t += Frame)
            {
                Assert.That(turns.Ask(1, TurnSort.Ranged, t), Is.False);
                if (t >= 0.2f) Assert.That(turns.Ask(2, TurnSort.Ranged, t), Is.False);
            }
            float stopped = t - Frame;
            // The wind-ups are over at 1 s: 1 still counts as waiting for a moment, then no more.
            Assert.That(turns.Ask(2, TurnSort.Ranged, 1.0f), Is.False, "1 asked a moment ago and is older");
            float later = stopped + new TurnValues().WaitingFor + 0.05f;
            Assert.That(turns.IsWaiting(1, later), Is.False);
            Assert.That(turns.Ask(2, TurnSort.Ranged, later), Is.True, "1 holds no turn back once it has stopped asking");
        }

        [Test]
        public void Turns_LightEnemiesAndTheBoss_AreNeverHeldBack()
        {
            var turns = new AttackTurns();
            for (int id = 1; id <= 20; id++)
            {
                Assert.That(turns.Ask(id, TurnSort.Light, 0f), Is.True, "swarmer " + id);
                turns.Began(id, TurnSort.Light, 0f, 0.4f);
            }
            Assert.That(turns.Ask(100, TurnSort.None, 0f), Is.True, "the boss");
            turns.Began(100, TurnSort.None, 0f, 1f);
            Assert.That(turns.Holds(TurnSort.Light), Is.False);
            Assert.That(turns.Holds(TurnSort.None), Is.False);
            Assert.That(turns.Ask(30, TurnSort.Heavy, 0f), Is.True, "and they take no heavy's turn");
        }

        [Test]
        public void Turns_Off_GivesEveryoneATurnAtOnce()
        {
            var turns = new AttackTurns { Values = new TurnValues { On = false } };
            List<(int id, float at)> heavy = Crowd(turns, TurnSort.Heavy, 6, 1.1f, seconds: 0.5f);
            Assert.That(heavy.Count, Is.EqualTo(6), "each began");
            Assert.That(heavy.All(b => b.at == 0f), Is.True, "all on the first frame");
            Assert.That(MostAtOnce(heavy, 1.1f), Is.EqualTo(6));
        }

        [Test]
        public void Turns_NoneWaitsForEver()
        {
            var turns = new AttackTurns();
            List<(int id, float at)> began = Crowd(turns, TurnSort.Heavy, 6, 1.1f, seconds: 20f);
            for (int id = 1; id <= 6; id++)
            {
                List<float> times = began.Where(b => b.id == id).Select(b => b.at).ToList();
                Assert.That(times.Count, Is.GreaterThanOrEqualTo(3), "each of six attacks again and again: " + id);
                Assert.That(times[0], Is.LessThan(3.5f), "each one's first within 3.5 s: " + id);
            }
        }

        // ---------------- The floors' brutes' Shockwave ----------------

        [Test]
        public void FloorBrutes_HaveTheShorterShockwave_AndTheAssetsNumbersAreUntouched()
        {
            var asset = new EnemyValues(); // the brute's numbers as the enemy settings asset has them by default
            EnemyValues floor = new LevelValues().FloorBruteValues(asset);
            EnemyAttackDefinition wave = floor.Attacks.Single(a => a.Name == "Shockwave");
            EnemyAttackDefinition was = asset.Attacks.Single(a => a.Name == "Shockwave");
            Assert.That(wave.MaxDistance, Is.EqualTo(8f), "used when she is 3 to 8 m away");
            Assert.That(wave.Length, Is.EqualTo(9f), "its line is 9 m");
            Assert.That((wave.MinDistance, wave.Damage, wave.Windup, wave.Recovery, wave.HalfWidth, wave.Counterable),
                        Is.EqualTo((was.MinDistance, was.Damage, was.Windup, was.Recovery, was.HalfWidth, was.Counterable)), "its damage and timing stay");
            Assert.That(was.MaxDistance, Is.EqualTo(11f), "the asset's own are not changed");
            Assert.That(was.Length, Is.EqualTo(12f));
            Assert.That(wave, Is.Not.SameAs(was), "a copy");
            EnemyAttackDefinition slam = floor.Attacks.Single(a => a.Name == "Slam");
            Assert.That((slam.MaxDistance, slam.Radius, slam.Damage), Is.EqualTo((3f, 3f, 20)), "the Slam as it was");
            Assert.That((floor.Health, floor.MoveSpeed, floor.AggroRange, floor.LeashRange), Is.EqualTo((asset.Health, asset.MoveSpeed, asset.AggroRange, asset.LeashRange)));
        }

        // ---------------- The floor heal ----------------

        [Test]
        public void FloorHeal_IsAShareOfHerMaximum()
        {
            var v = new RunValues();
            Assert.That(v.FloorHeal, Is.EqualTo(0.35f));
            Assert.That(FloorHeals.Amount(100, v.FloorHeal), Is.EqualTo(35));
            Assert.That(FloorHeals.Amount(120, v.FloorHeal), Is.EqualTo(42));
            Assert.That(FloorHeals.Amount(140, v.FloorHeal), Is.EqualTo(49));
            Assert.That(FloorHeals.Amount(100, 0f), Is.EqualTo(0));
            Assert.That(FloorHeals.Amount(0, 0.35f), Is.EqualTo(0));
        }
    }
}
