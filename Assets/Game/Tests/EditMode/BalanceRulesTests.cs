using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 023 (2026-11-20), the first balance pass: the turns to attack (the caps, the gap, the one that has
    /// waited longest goes first, light enemies and the boss never held, off gives everyone a turn at once) and the
    /// floor heal's amount. Work order 030 (2026-11-20), the second pass: the levers it made, each with its tests below.
    /// </summary>
    public class BalanceRulesTests
    {
        const float Frame = 1f / 60f;

        /// <summary>The first pass's rule: a limit and a gap for each sort (work order 030 shares them, lever D; it has its own tests).</summary>
        static AttackTurns FirstPass() => new AttackTurns { Values = new TurnValues { Shared = false } };

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
            var turns = FirstPass();
            List<(int id, float at)> heavy = Crowd(turns, TurnSort.Heavy, 6, 1.1f);
            Assert.That(MostAtOnce(heavy, 1.1f), Is.EqualTo(2), "two heavies at once at most, and two do");
            var turns2 = FirstPass();
            List<(int id, float at)> ranged = Crowd(turns2, TurnSort.Ranged, 6, 0.9f);
            Assert.That(MostAtOnce(ranged, 0.9f), Is.EqualTo(2), "two ranged at once at most, and two do");
        }

        [Test]
        public void Turns_TheSortsAreCountedApart()
        {
            var turns = FirstPass();
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
            var turns = FirstPass();
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
            var turns = FirstPass();
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
            var turns = FirstPass();
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
            var turns = FirstPass();
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
            var turns = FirstPass();
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

        // ---------------- The room heal (work order 030, lever A) ----------------

        [Test]
        public void RoomHeal_EightPercentAClearedRoom_TwentyTheEliteRoom_OncePerRoom()
        {
            var v = new RunValues();
            Assert.That(v.RoomHeal, Is.EqualTo(0.08f));
            Assert.That(v.EliteRoomHeal, Is.EqualTo(0.20f));
            Assert.That(v.FloorHeal, Is.EqualTo(0.35f), "the floor reward's heal stays");
            var heals = new RoomHeals();
            Assert.That(heals.Heal(1, false, 100, v), Is.EqualTo(8), "a combat room: 8 of 100");
            Assert.That(heals.Heal(1, false, 100, v), Is.EqualTo(0), "once per room");
            Assert.That(heals.Healed(1), Is.True);
            Assert.That(heals.Heal(3, true, 100, v), Is.EqualTo(20), "the elite room: 20 of 100");
            Assert.That(heals.Heal(3, true, 100, v), Is.EqualTo(0), "once");
            Assert.That(heals.Heal(4, false, 130, v), Is.EqualTo(10), "a share of her maximum, rounded: 10.4 is 10");
            Assert.That(heals.Healed(5), Is.False);
            heals.Clear();
            Assert.That(heals.Healed(1), Is.False, "a new floor");
            Assert.That(heals.Heal(1, false, 100, v), Is.EqualTo(8), "and room 1 of it heals again");
            Assert.That(new RoomHeals().Heal(1, false, 100, new RunValues { RoomHeal = 0f }), Is.EqualTo(0), "a share of 0 heals nothing");
        }

        // ---------------- The danger by floor (work order 030, lever B) ----------------

        [Test]
        public void DamageScale_RisesWithTheFloor_AndMultipliesWithAnElites()
        {
            // Work order 046's flights: 0.6, 0.6, 0.7, 0.8, 0.85 (0.7, 0.8, 0.9, 1, 1 before).
            var v = new LevelValues();
            Assert.That(v.DamageScaleOf(1), Is.EqualTo(0.6f));
            Assert.That(v.DamageScaleOf(2), Is.EqualTo(0.6f));
            Assert.That(v.DamageScaleOf(3), Is.EqualTo(0.7f));
            Assert.That(v.DamageScaleOf(4), Is.EqualTo(0.8f));
            Assert.That(v.DamageScaleOf(5), Is.EqualTo(0.85f));
            Assert.That(v.DamageScaleOf(9), Is.EqualTo(0.85f), "past the table, its last");
            Assert.That(v.DamageScaleOf(0), Is.EqualTo(0.6f), "before it, its first");
            Assert.That(new LevelValues { FloorDamageScale = null }.DamageScaleOf(2), Is.EqualTo(1f), "no table: 1");
            // An elite's own scale multiplies with the floor's: a floor-1 elite deals 1.25 x 0.6 of an attack.
            Assert.That(v.EliteDamageScale * v.DamageScaleOf(1), Is.EqualTo(0.75f).Within(1e-6f));
            Assert.That(v.EliteDamageScale * v.DamageScaleOf(4), Is.EqualTo(1f).Within(1e-6f));
        }

        // ---------------- The elite by its kind (work order 030, lever C) ----------------

        [Test]
        public void EliteHealth_ByItsKind_BruteThree_ShooterFive_GuardTwice()
        {
            var v = new LevelValues();
            Assert.That(v.EliteHealthOf(EnemyKind.Brute), Is.EqualTo(3 * v.EnemyHealth));
            Assert.That(v.EliteHealthOf(EnemyKind.Shooter), Is.EqualTo(5 * v.ShooterHealth));
            Assert.That(v.EliteHealthOf(EnemyKind.Guard), Is.EqualTo(2 * v.GuardHealth));
            Assert.That(v.EliteHealthScaleOf(EnemyKind.Swarmer), Is.EqualTo(v.EliteHealthScale), "a kind with no share of its own: the old four");
            Assert.That(v.EliteHealthScale, Is.EqualTo(4f));
        }

        // ---------------- One crowd, one queue (work order 030, lever D) ----------------

        /// <summary>The turns with the shared queue on (it is off by default), on this floor.</summary>
        static AttackTurns Shared(int floor) => new AttackTurns { Floor = floor, Values = new TurnValues { Shared = true } };

        /// <summary>Heavies and ranged together ask every frame; each begins a wind-up of this length when it may.</summary>
        static List<(int id, TurnSort sort, float at)> Mixed(AttackTurns turns, int heavies, int ranged, float windup, float seconds = 12f, float recovery = 1f)
        {
            var began = new List<(int, TurnSort, float)>();
            int count = heavies + ranged;
            var busyUntil = new float[count];
            for (float now = 0f; now < seconds; now += Frame)
            {
                for (int id = 1; id <= count; id++)
                {
                    TurnSort sort = id <= heavies ? TurnSort.Heavy : TurnSort.Ranged;
                    if (now < busyUntil[id - 1]) continue;
                    if (!turns.Ask(id, sort, now)) continue;
                    turns.Began(id, sort, now, windup);
                    busyUntil[id - 1] = now + windup + recovery;
                    began.Add((id, sort, now));
                }
            }
            return began;
        }

        static int MostTogether(List<(int id, TurnSort sort, float at)> began, float windup)
        {
            int most = 0;
            foreach ((int _, TurnSort _, float at) in began)
                most = System.Math.Max(most, began.Count(b => b.at <= at && at < b.at + windup));
            return most;
        }

        [Test]
        public void SharedQueue_TheNumbers()
        {
            var v = new TurnValues();
            Assert.That(v.Shared, Is.False, "built, and off: the fourth round of flights saw no gain from it");
            Assert.That(v.SharedAtOnce, Is.EqualTo(2));
            Assert.That(v.SharedAtOnceLate, Is.EqualTo(3));
            Assert.That(v.SharedLateFromFloor, Is.EqualTo(4));
            Assert.That(v.SharedGap, Is.EqualTo(0.7f));
            var turns = Shared(1);
            Assert.That(turns.SharedCap(1), Is.EqualTo(2));
            Assert.That(turns.SharedCap(3), Is.EqualTo(2));
            Assert.That(turns.SharedCap(4), Is.EqualTo(3));
            Assert.That(turns.SharedCap(5), Is.EqualTo(3));
        }

        [TestCase(1, 2)]
        [TestCase(3, 2)]
        [TestCase(4, 3)]
        [TestCase(5, 3)]
        public void SharedQueue_HeavyAndRangedTogether_NeverMoreThanTheFloorsLimit_AndEveryoneGetsATurn(int floor, int limit)
        {
            var turns = Shared(floor);
            List<(int id, TurnSort sort, float at)> began = Mixed(turns, 4, 4, 2.5f, 20f);
            Assert.That(MostTogether(began, 2.5f), Is.EqualTo(limit), "heavy and ranged share one limit on floor " + floor);
            for (int id = 1; id <= 8; id++) Assert.That(began.Count(b => b.id == id), Is.GreaterThanOrEqualTo(2), "enemy " + id + " attacked, and again");
        }

        [Test]
        public void SharedQueue_AGapOfPoint7_BetweenAnyTwoWindUps_OfEitherSort()
        {
            var turns = Shared(1);
            List<(int id, TurnSort sort, float at)> began = Mixed(turns, 4, 4, 0.2f, 10f, 0.1f);
            var times = began.Select(b => b.at).OrderBy(t => t).ToList();
            for (int i = 1; i < times.Count; i++)
                Assert.That(times[i] - times[i - 1], Is.GreaterThanOrEqualTo(0.7f - 1e-4f), "a wind-up began " + (times[i] - times[i - 1]) + " s after the one before");
            Assert.That(began.Any(b => b.sort == TurnSort.Heavy) && began.Any(b => b.sort == TurnSort.Ranged), Is.True, "both sorts get turns");
        }

        [Test]
        public void SharedQueue_Off_GivesTheFirstPassesRule()
        {
            var turns = new AttackTurns { Floor = 1, Values = new TurnValues { Shared = false } };
            List<(int id, TurnSort sort, float at)> began = Mixed(turns, 4, 4, 2.5f, 20f);
            var heavy = began.Where(b => b.sort == TurnSort.Heavy).Select(b => (b.id, b.at)).ToList();
            var ranged = began.Where(b => b.sort == TurnSort.Ranged).Select(b => (b.id, b.at)).ToList();
            Assert.That(MostAtOnce(heavy, 2.5f), Is.EqualTo(2), "two heavies at once");
            Assert.That(MostAtOnce(ranged, 2.5f), Is.EqualTo(2), "and two ranged at once besides");
            Assert.That(MostTogether(began, 2.5f), Is.EqualTo(4), "four together, as before");
        }

        [Test]
        public void SharedQueue_LightAndNone_NeverHeld()
        {
            var turns = Shared(1);
            turns.Began(1, TurnSort.Heavy, 0f, 10f);
            turns.Began(2, TurnSort.Ranged, 1f, 10f);
            Assert.That(turns.Ask(3, TurnSort.Heavy, 2f), Is.False, "the shared limit is reached");
            Assert.That(turns.Ask(4, TurnSort.Light, 2f), Is.True, "a light enemy is never held");
            Assert.That(turns.Ask(5, TurnSort.None, 2f), Is.True, "nor the boss or an elite");
        }
    }
}
