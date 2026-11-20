using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 008: the stagger bar, the plan of enemy kinds per room and how the generator deals it out, the
    /// validator's new checks, the brain's cone, its hold on attacking and its cancel, and the summary's numbers about
    /// the kinds of enemy and fights with several enemies.
    /// </summary>
    public class EnemyKindsTests
    {
        // ---------------- The stagger bar ----------------

        [Test]
        public void Stagger_TakesPoints_AndDoesNotRefillByItself()
        {
            var bar = new StaggerBar(new StaggerValues { Max = 100f, DazeSeconds = 4f });
            Assert.That(bar.Current, Is.EqualTo(100f));
            Assert.That(bar.Take(30f, 1f, out bool dazed), Is.EqualTo(30f));
            Assert.That(dazed, Is.False);
            Assert.That(bar.Take(0f, 1.5f, out _), Is.EqualTo(0f), "a hit with no stagger takes nothing");
            Assert.That(bar.Tick(100f), Is.False);
            Assert.That(bar.Current, Is.EqualTo(70f), "a long time later it is where it was");
            Assert.That(bar.Fill, Is.EqualTo(0.7f).Within(1e-5f));
        }

        [Test]
        public void Stagger_EmptyBar_Dazes_ForTheSetTime_ThenIsFull()
        {
            var values = new StaggerValues { Max = 100f, DazeSeconds = 4f, DazedDamageMultiplier = 1.5f };
            var bar = new StaggerBar(values);
            bar.Take(60f, 10f, out _);
            Assert.That(bar.Take(60f, 11f, out bool dazed), Is.EqualTo(40f), "no more than is left");
            Assert.That(dazed, Is.True);
            Assert.That(bar.Current, Is.EqualTo(0f));
            Assert.That(bar.IsDazed(11f), Is.True);
            Assert.That(bar.DazedUntil, Is.EqualTo(15f));
            Assert.That(bar.Take(30f, 12f, out bool again), Is.EqualTo(0f), "no stagger is taken while dazed");
            Assert.That(again, Is.False);
            Assert.That(bar.Tick(14.9f), Is.False);
            Assert.That(bar.IsDazed(14.9f), Is.True);
            Assert.That(bar.IsDazed(15f), Is.False, "over at the set time, even before a tick");
            Assert.That(bar.Tick(15f), Is.True, "the daze ends once");
            Assert.That(bar.Tick(15.1f), Is.False);
            Assert.That(bar.Current, Is.EqualTo(100f), "full again");
        }

        [Test]
        public void Stagger_DazedDamage_IsRaised_OnlyWhileDazed()
        {
            var bar = new StaggerBar(new StaggerValues { Max = 10f, DazeSeconds = 4f, DazedDamageMultiplier = 1.5f });
            Assert.That(bar.Scale(100, 0f), Is.EqualTo(100));
            bar.Take(10f, 1f, out _);
            Assert.That(bar.Scale(100, 1f), Is.EqualTo(150));
            Assert.That(bar.Scale(33, 2f), Is.EqualTo(50), "rounded half away from zero: 49.5");
            Assert.That(bar.Scale(100, 5f), Is.EqualTo(100), "the daze is over");
        }

        [Test]
        public void Stagger_Reset_FillsAndEndsADaze()
        {
            var bar = new StaggerBar(new StaggerValues { Max = 50f });
            bar.Take(50f, 0f, out _);
            bar.Reset();
            Assert.That(bar.IsDazed(0.1f), Is.False);
            Assert.That(bar.Current, Is.EqualTo(50f));
        }

        [Test]
        public void Stagger_StartValuesOfTheSkills_AreAsTheOrderSays()
        {
            Dictionary<string, float> want = new Dictionary<string, float>
            {
                ["Quick Shot"] = 3f, ["Triple Tap"] = 2f, ["Backstep"] = 5f, ["Scatter Blast"] = 30f,
                ["Fan Fire"] = 4f, ["Powder Keg"] = 20f, ["Long Shot"] = 15f, ["Heavy Volley"] = 40f,
            };
            SkillDefinition[] skills = SkillDefaults.Create();
            Assert.That(skills.Select(s => s.Name), Is.EquivalentTo(want.Keys));
            foreach (SkillDefinition s in skills) Assert.That(s.Stagger, Is.EqualTo(want[s.Name]), s.Name);
        }

        // ---------------- The brain ----------------

        [Test]
        public void Brain_ACone_HitsInsideItsAngleAndLength_Only()
        {
            var sweep = new EnemyAttackDefinition { Shape = EnemyAttackShape.Cone, Length = 6f, Angle = 120f };
            var origin = new GroundPoint(0f, 0f);
            var aim = new GroundPoint(0f, 5f);
            Assert.That(EnemyBrain.Hits(sweep, origin, aim, new GroundPoint(0f, 5f), 0.5f), Is.True, "straight ahead");
            Assert.That(EnemyBrain.Hits(sweep, origin, aim, new GroundPoint(3f, 4f), 0.1f), Is.True, "37 degrees off");
            Assert.That(EnemyBrain.Hits(sweep, origin, aim, new GroundPoint(4f, 1f), 0.1f), Is.False, "76 degrees off");
            Assert.That(EnemyBrain.Hits(sweep, origin, aim, new GroundPoint(0f, -3f), 0.5f), Is.False, "behind");
            Assert.That(EnemyBrain.Hits(sweep, origin, aim, new GroundPoint(0f, 8f), 0.5f), Is.False, "too far");
        }

        [Test]
        public void Brain_HeldFromAttacking_StaysInTheChase_AndCancelDropsTheWindup()
        {
            var values = new EnemyValues();
            var brain = new EnemyBrain();
            brain.Step(0f, 2f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            brain.Step(0.1f, 2f, true, false, values, mayAttack: false);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase), "an attack fits but it may not");
            brain.Step(0.2f, 2f, true, false, values);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Windup));
            brain.Cancel(0.3f);
            Assert.That(brain.State, Is.EqualTo(EnemyState.Chase));
            Assert.That(brain.Attack, Is.Null);
            Assert.That(brain.Step(5f, 20f, true, false, values), Is.False, "no strike comes of a dropped windup");
        }

        // ---------------- The plan of kinds ----------------

        static LevelLayout Make(ulong seed) => LevelGenerator.Generate(seed, new LevelValues());

        static string Sorted(IEnumerable<EnemyGroup> groups) => string.Join(", ", groups.Select(g => g.ToString()).OrderBy(s => s, StringComparer.Ordinal));

        // Work order 012 replaced the fixed plan per combat room (a pack; two brutes; ...) with a table per floor number,
        // drawn from the seed: these two tests were changed on purpose.
        [Test]
        public void Plan_TheFloorTableGivesEachRoomItsRange()
        {
            var v = new LevelValues();
            v.EnemyRange(RoomKind.Combat, 1, out int f, out int m);
            Assert.That((f, m), Is.EqualTo((6, 8)), "floor 1, six to eight");
            v.EnemyRange(RoomKind.Combat, 5, out f, out m);
            Assert.That((f, m), Is.EqualTo((10, 14)), "floor 5, ten to fourteen");
            v.EnemyRange(RoomKind.Exit, 1, out f, out m);
            Assert.That((f, m), Is.EqualTo((9, 12)), "the exit room half as many again");
            v.EnemyRange(RoomKind.Entrance, 3, out f, out m);
            Assert.That(m, Is.EqualTo(0));
            v.EnemyRange(RoomKind.Treasure, 3, out f, out m);
            Assert.That(m, Is.EqualTo(0));
        }

        [Test]
        public void Generator_EveryRoomsMix_IsDrawnFromTheSeed()
        {
            var mixes = new HashSet<string>();
            for (ulong s = 0; s < 200; s++)
            {
                int floor = 1 + (int)(s % 5); // floor 1's mixes are alike (mostly swarmers): every floor's
                LevelLayout level = LevelGenerator.Generate(s, floor);
                foreach (LevelRoom r in level.Rooms.Where(x => x.Kind == RoomKind.Combat))
                {
                    Assert.That(r.EnemyGroups.Count, Is.EqualTo(r.EnemyPoints.Count), "seed " + s + " room " + r.Order);
                    mixes.Add(Sorted(r.EnemyGroups));
                }
                Assert.That(LevelGenerator.Generate(s, floor).Describe(), Is.EqualTo(level.Describe()), "the same seed, the same kinds at the same points");
            }
            Assert.That(mixes.Count, Is.GreaterThan(10), "rooms hold different mixes");
        }

        // ---------------- The validator's new checks ----------------

        static void AssertFault(Action<LevelLayout> breakIt, string expected)
        {
            LevelLayout level = Make(42UL);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            breakIt(level);
            List<string> faults = LevelValidator.Validate(level);
            Assert.That(faults.Any(f => f.Contains(expected)), "expected a fault containing \"" + expected + "\", got:\n  " + string.Join("\n  ", faults));
        }

        static LevelRoom Combat(LevelLayout l, int n) => l.Rooms.Where(r => r.Kind == RoomKind.Combat).ElementAt(n);

        [Test]
        public void Validator_ReportsAPointWithoutAGroup() =>
            AssertFault(l => Combat(l, 1).EnemyGroups.RemoveAt(0), "enemy groups for");

        // Changed on purpose by work order 012: a room's kinds are checked against its floor's rules, not a fixed plan;
        // lines of brutes and shooters are allowed, but not too long; and every enemy of a pack keeps the wall's clearance.
        [Test]
        public void Validator_ReportsGuardsOnTheFirstFloor() =>
            AssertFault(l => Combat(l, 1).EnemyGroups[0] = new EnemyGroup(EnemyKind.Guard), "guards, floor 1 at most 0");

        [Test]
        public void Validator_ReportsALineTooLong() =>
            AssertFault(l => Combat(l, 1).EnemyGroups[0] = new EnemyGroup(EnemyKind.Brute, 3), "at most 2 stand together");

        [Test]
        public void Validator_ReportsAPackThatDoesNotFitItsRoom()
        {
            LevelLayout l = Make(42UL);
            LevelRoom r = l.Rooms.First(x => x.EnemyGroups.Any(g => g.IsPack));
            int i = r.EnemyGroups.FindIndex(g => g.IsPack);
            var values = new LevelValues();
            // 2 m from the west wall: the point is clear of it, its swarmers 1.2 m round it are not.
            var p = new LevelPoint(r.Rect.X * l.CellSize + 2.0, r.Middle(l.CellSize).Y);
            r.EnemyPoints[i] = p;
            r.EnemyPlaces[i] = LevelGeometry.MemberPlaces(l, r, p, r.EnemyGroups[i], values);
            Assert.That(LevelValidator.Validate(l, values).Any(f => f.Contains("enemy " + i + ".") && f.Contains("nearer a wall")),
                        string.Join("\n", LevelValidator.Validate(l, values)));
        }

        // ---------------- The summary ----------------

        static SessionEvent Ev(double t, string type, string name = null, string enemy = null, int? id = null, int? damage = null, string result = null) =>
            new SessionEvent(t, type, name) { Enemy = enemy, EnemyId = id, Damage = damage, Result = result };

        [Test]
        public void Summary_AFight_LastsUntilNoEnemyIsAwake()
        {
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake, enemy: "swarmer", id: 1),
                Ev(1.2, SessionEventTypes.EnemyWake, enemy: "swarmer", id: 2),
                Ev(3, SessionEventTypes.EnemyDeath, enemy: "swarmer", id: 1),
                Ev(5, SessionEventTypes.EnemyDeath, enemy: "swarmer", id: 2),
                Ev(10, SessionEventTypes.EnemyWake, enemy: "shooter", id: 3),
                Ev(12, SessionEventTypes.EnemyGaveUp, enemy: "shooter", id: 3, result: SessionWords.Removed),
                Ev(20, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Fights, Is.EqualTo(2));
            Assert.That(stats.FightSpans[0].Key, Is.EqualTo(1d));
            Assert.That(stats.FightSpans[0].Value, Is.EqualTo(5d), "until the second swarmer died");
            Assert.That(stats.FightSpans[1].Value, Is.EqualTo(12d), "removed counts as gone");
        }

        [Test]
        public void Summary_CountsKillsAndDamageByKind_AndTheGuardsDaze()
        {
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake, enemy: "guard", id: 7),
                Ev(2, SessionEventTypes.Hit, "Quick Shot", enemy: "guard", id: 7, damage: 100),
                Ev(3, SessionEventTypes.EnemyStrike, "Slam", enemy: "guard", id: 7, damage: 35, result: SessionWords.Landed),
                Ev(4, SessionEventTypes.EnemyStrike, "Sweep", enemy: "guard", id: 7, result: SessionWords.OutOfArea),
                Ev(6, SessionEventTypes.EnemyDazed, enemy: "guard", id: 7),
                Ev(7, SessionEventTypes.Hit, "Scatter Blast", enemy: "guard", id: 7, damage: 300),
                Ev(10, SessionEventTypes.DazeEnded, enemy: "guard", id: 7),
                Ev(11, SessionEventTypes.Hit, "Quick Shot", enemy: "guard", id: 7, damage: 100),
                Ev(12, SessionEventTypes.EnemyWake, enemy: "swarmer", id: 8),
                Ev(13, SessionEventTypes.EnemyStrike, "Lunge", enemy: "swarmer", id: 8, damage: 8, result: SessionWords.Landed),
                Ev(14, SessionEventTypes.EnemyStrike, "Lunge", enemy: "swarmer", id: 8, damage: 8, result: SessionWords.Landed),
                Ev(15, SessionEventTypes.EnemyDeath, enemy: "swarmer", id: 8),
                Ev(16, SessionEventTypes.EnemyDeath, enemy: "guard", id: 7),
                Ev(20, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            EnemyKindRow guard = stats.EnemyKinds.Single(r => r.Kind == "guard");
            EnemyKindRow swarmer = stats.EnemyKinds.Single(r => r.Kind == "swarmer");
            Assert.That(guard.Kills, Is.EqualTo(1));
            Assert.That(swarmer.Kills, Is.EqualTo(1));
            Assert.That(guard.DamageTaken, Is.EqualTo(35));
            Assert.That(guard.DamageByAttack, Is.EqualTo(new[] { new KeyValuePair<string, int>("Slam", 35) }));
            Assert.That(swarmer.DamageByAttack, Is.EqualTo(new[] { new KeyValuePair<string, int>("Lunge", 16) }));
            Assert.That(stats.GuardsHit, Is.EqualTo(1));
            Assert.That(stats.GuardsDazed, Is.EqualTo(1));
            Assert.That(stats.FirstHitToDazeMean, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(stats.GuardDamage, Is.EqualTo(500));
            Assert.That(stats.GuardDamageWhileDazed, Is.EqualTo(300));
            Assert.That(stats.Fights, Is.EqualTo(1));

            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Enemies by kind, whole session", text);
            StringAssert.Contains("First hit to first daze, mean", text);
            StringAssert.Contains("300 of 500 (60%)", text);
            TestContext.WriteLine(text);
        }

        [Test]
        public void Events_WriteAndReadTheEnemyFields()
        {
            var e = new SessionEvent(1.5, SessionEventTypes.StaggerTaken) { Enemy = "guard", EnemyId = 12, Stagger = 30f };
            e.AddExtra("bar", "70");
            SessionEvent back = SessionEvent.Parse(e.ToJson());
            Assert.That(back.Enemy, Is.EqualTo("guard"));
            Assert.That(back.EnemyId, Is.EqualTo(12));
            Assert.That(back.Stagger, Is.EqualTo(30f));
            Assert.That(back.ExtraValue("bar"), Is.EqualTo("70"));
        }
    }
}
