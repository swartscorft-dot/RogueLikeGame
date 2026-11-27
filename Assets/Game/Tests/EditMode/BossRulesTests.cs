using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 014 (2026-11-20): the act boss's rules with no engine in them. Which attack for a distance and that the
    /// cleave and the spin take turns, its anger's shorter windups, its two calls once each, how far a charge runs, what
    /// a rush passes over, its model's clips; and the fuller rooms of each floor over a thousand seeds.
    /// </summary>
    public class BossRulesTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static EnemyAttackDefinition Named(EnemyValues v, string name) => v.Attacks.First(a => a.Name == name);

        [Test]
        public void Choice_ByDistance_CleaveAndSpinTakeTurns_TheChargeNeedsSight()
        {
            var v = new BossValues();
            var t = new BossTactics();
            // Close, both reach: they take turns, the cleave first.
            Assert.That(t.Choose(3f, true, v.Enemy).Name, Is.EqualTo(BossValues.Cleave));
            Assert.That(t.Choose(3f, true, v.Enemy).Name, Is.EqualTo(BossValues.Spin));
            Assert.That(t.Choose(2f, true, v.Enemy).Name, Is.EqualTo(BossValues.Cleave));
            Assert.That(t.Choose(1f, false, v.Enemy).Name, Is.EqualTo(BossValues.Spin), "close, sight does not matter");
            // Only the cleave reaches from 4 to 5 m.
            Assert.That(t.Choose(4.5f, true, v.Enemy).Name, Is.EqualTo(BossValues.Cleave));
            Assert.That(t.Choose(4.5f, true, v.Enemy).Name, Is.EqualTo(BossValues.Cleave), "alone, it is chosen again");
            Assert.That(t.Choose(3f, true, v.Enemy).Name, Is.EqualTo(BossValues.Spin), "the turn goes on after it");
            // Between the bands nothing: it walks.
            Assert.That(t.Choose(6f, true, v.Enemy), Is.Null);
            // The charge from 7 to 16 m, only seeing her.
            Assert.That(t.Choose(7f, true, v.Enemy).Name, Is.EqualTo(BossValues.Charge));
            Assert.That(t.Choose(16f, true, v.Enemy).Name, Is.EqualTo(BossValues.Charge));
            Assert.That(t.Choose(10f, false, v.Enemy), Is.Null, "a wall between: it walks");
            Assert.That(t.Choose(16.5f, true, v.Enemy), Is.Null);
            Assert.That(t.LastClose, Is.EqualTo(BossValues.Spin), "a charge does not count in the cleave and spin's turns");
        }

        [Test]
        public void TheAttacks_AreTheTablesNumbers()
        {
            var v = new BossValues();
            EnemyAttackDefinition cleave = Named(v.Enemy, BossValues.Cleave), spin = Named(v.Enemy, BossValues.Spin), charge = Named(v.Enemy, BossValues.Charge);
            Assert.That((cleave.Shape, cleave.MaxDistance, cleave.Length, cleave.Angle, cleave.Windup, cleave.Recovery, cleave.Damage),
                        Is.EqualTo((EnemyAttackShape.Cone, 5f, 6.5f, 110f, 1.1f, 1.3f, 40)));
            Assert.That((spin.Shape, spin.MaxDistance, spin.Radius, spin.Windup, spin.Recovery, spin.Damage),
                        Is.EqualTo((EnemyAttackShape.Circle, 4f, 5.5f, 1.0f, 1.4f, 35)));
            Assert.That((charge.Shape, charge.MinDistance, charge.MaxDistance, charge.Length, charge.HalfWidth * 2f, charge.Windup, charge.Recovery, charge.Damage, charge.Rush),
                        Is.EqualTo((EnemyAttackShape.Line, 7f, 16f, 14f, 3f, 0.9f, 1.6f, 45, 12f)));
            Assert.That(cleave.Rush, Is.EqualTo(0f));
            Assert.That(new EnemyAttackDefinition().Rush, Is.EqualTo(0f), "every other enemy's attack strikes in place");
            Assert.That(v.Stagger.Max, Is.EqualTo(new StaggerValues().Max * 1.5f), "half again the guard's bar");
            Assert.That(new LevelValues().BossHealth, Is.EqualTo(18000));
            Assert.That(new LevelValues().HealthOf(EnemyKind.Boss), Is.EqualTo(18000));
            Assert.That(EnemyKinds.Word(EnemyKind.Boss), Is.EqualTo("boss"));
        }

        [Test]
        public void Angry_BelowHalfHealth_Once_EveryWindupIsShorter_TheValuesLeftAsTheyWere()
        {
            var v = new BossValues();
            var t = new BossTactics();
            Assert.That(t.BecomesAngry(0.6f, v), Is.False);
            Assert.That(t.BecomesAngry(0.5f, v), Is.False, "at half, not yet");
            Assert.That(t.BecomesAngry(0.49f, v), Is.True);
            Assert.That(t.BecomesAngry(0.2f, v), Is.False, "once");
            Assert.That(t.Angry, Is.True);
            EnemyValues angry = v.Angry();
            foreach (EnemyAttackDefinition a in v.Enemy.Attacks)
            {
                EnemyAttackDefinition b = Named(angry, a.Name);
                Assert.That(b, Is.Not.SameAs(a));
                Assert.That(b.Windup, Is.EqualTo(a.Windup * 0.8f).Within(1e-6f), a.Name);
                Assert.That((b.Recovery, b.Damage, b.Rush), Is.EqualTo((a.Recovery, a.Damage, a.Rush)), a.Name);
            }
            Assert.That(Named(v.Enemy, BossValues.Cleave).Windup, Is.EqualTo(1.1f), "the calm values are not changed");
            Assert.That(v.AngryCharges, Is.EqualTo(2));
        }

        [Test]
        public void Calls_AtTwoThirdsAndOneThird_OnceEach()
        {
            bool noPacks = BossValues.NoPacks;
            BossValues.NoPacks = false; // work order 140 (changed deliberately): this test pins the packs of before "Ingen flokker"
            try { CallsAsBefore(); }
            finally { BossValues.NoPacks = noPacks; }
        }

        static void CallsAsBefore()
        {
            var v = new BossValues();
            var t = new BossTactics();
            Assert.That(t.CallsDue(1f, v), Is.EqualTo(0));
            Assert.That(t.CallsDue(0.67f, v), Is.EqualTo(0));
            Assert.That(t.CallsDue(0.66f, v), Is.EqualTo(1));
            Assert.That(t.CallsDue(0.6f, v), Is.EqualTo(0), "once");
            Assert.That(t.CallsDue(0.34f, v), Is.EqualTo(0));
            Assert.That(t.CallsDue(0.33f, v), Is.EqualTo(1));
            Assert.That(t.CallsDue(0.01f, v), Is.EqualTo(0));
            Assert.That(t.Calls, Is.EqualTo(2));
            var big = new BossTactics();
            Assert.That(big.CallsDue(0.2f, v), Is.EqualTo(2), "one hit past both thresholds calls both");
            Assert.That(v.CallCount, Is.EqualTo(6));
        }

        [Test]
        public void TheCharge_IsCutAtTheFirstWallOrEdge_AndStopsShortOfIt()
        {
            Assert.That(BossTactics.ChargeLength(14f, 30f, 30f, 0.7f), Is.EqualTo(14f), "clear: its whole length");
            Assert.That(BossTactics.ChargeLength(14f, 14f, 30f, 0.7f), Is.EqualTo(14f), "a wall just at its end does not cut it");
            Assert.That(BossTactics.ChargeLength(14f, 6f, 30f, 0.7f), Is.EqualTo(5.3f).Within(1e-5f), "a wall at 6 m");
            Assert.That(BossTactics.ChargeLength(14f, 9f, 4f, 0.7f), Is.EqualTo(3.3f).Within(1e-5f), "the walkable ground ends first");
            Assert.That(BossTactics.ChargeLength(14f, 0.4f, 30f, 0.7f), Is.EqualTo(0f), "never below nothing");
        }

        [Test]
        public void ARush_HitsWhatItPassesOver_AndNothingBesideOrBeyond()
        {
            var from = new GroundPoint(0f, 0f);
            var to = new GroundPoint(5f, 0f);
            Assert.That(BossTactics.RushPasses(from, to, 1.5f, new GroundPoint(3f, 1f), 0.5f), Is.True);
            Assert.That(BossTactics.RushPasses(from, to, 1.5f, new GroundPoint(3f, 1.9f), 0.5f), Is.True, "the strip's edge with her radius");
            Assert.That(BossTactics.RushPasses(from, to, 1.5f, new GroundPoint(3f, 2.1f), 0.5f), Is.False, "beside it");
            Assert.That(BossTactics.RushPasses(from, to, 1.5f, new GroundPoint(7.5f, 0f), 0.5f), Is.False, "beyond where it got to");
            Assert.That(BossTactics.RushPasses(from, from, 1.5f, new GroundPoint(1f, 0f), 0.5f), Is.True, "standing, what it stands over");
        }

        [Test]
        public void TheModel_ClipsOfItsAttacks_TimedOnTheGamesStrike()
        {
            EnemyModelSpec spec = EnemyModelSpec.Of(EnemyKind.Boss);
            Assert.That(spec.Name, Is.EqualTo("Crypt_BossKnight_01"));
            Assert.That(spec.Height, Is.EqualTo(3.317), "as Unity imports it, the horns' tips");
            Assert.That(spec.HasStagger, Is.True);
            Assert.That(EnemyModelSpec.All, Does.Contain(spec));
            Assert.That(spec.Clip("Slam"), Is.Null, "its Slam is not played: the cleaver points straight up in it");
            Assert.That(spec.ClipForAttack(BossValues.Cleave).Name, Is.EqualTo("Attack"));
            Assert.That(spec.ClipForAttack(BossValues.Spin).Name, Is.EqualTo("Spin"));
            Assert.That(spec.ClipForAttack(BossValues.Charge), Is.Null, "the charge stands in Idle and runs in Run");
            var expected = new Dictionary<string, (double length, bool loop, double strike)>
            {
                ["Idle"] = (4.0, true, -1), ["Walk"] = (1.033, true, -1), ["Run"] = (0.633, true, -1),
                ["Attack"] = (2.8, false, 1.067), ["Spin"] = (2.5, false, 0.967), ["Stagger"] = (2.833, false, -1), ["Death"] = (2.967, false, -1),
            };
            Assert.That(spec.Clips.Select(c => c.Name), Is.EquivalentTo(expected.Keys));
            foreach (EnemyClipSpec c in spec.Clips)
                Assert.That((c.Length, c.Loop, c.Strike), Is.EqualTo(expected[c.Name]), c.Name);

            foreach (EnemyValues values in new[] { new BossValues().Enemy, new BossValues().Angry() })
                foreach (string name in new[] { BossValues.Cleave, BossValues.Spin })
                {
                    EnemyAttackDefinition a = Named(values, name);
                    EnemyClipSpec clip = spec.ClipForAttack(name);
                    AttackClipPlan plan = AttackClipPlan.For(a.Windup, a.Recovery, clip.Strike, clip.Length);
                    Assert.That(plan.InWindup(a.Windup), Is.EqualTo(clip.Strike).Within(1e-9), name + " lands on the strike");
                    Assert.That(plan.Hold, Is.EqualTo(0).Within(1e-9), name + " needs no hold");
                    Assert.That(plan.WindupSpeed, Is.InRange(0.5, 2.0));
                    TestContext.WriteLine(string.Format(s_Inv, "BOSSCLIP {0} windup {1:0.###} s: clip from {2:0.###} s at {3:0.###}x; recovery {4:0.###} s at {5:0.###}x",
                                                        name, a.Windup, plan.Start, plan.WindupSpeed, a.Recovery, plan.RecoverySpeed));
                }
        }

        [Test]
        public void TheValidator_WantsTheActBoss_OfTheBossKind()
        {
            LevelLayout level = LevelGenerator.Generate(42UL, 5);
            Assume.That(LevelValidator.Validate(level), Is.Empty);
            LevelRoom exit = level.Rooms.First(r => r.Kind == RoomKind.Exit);
            int at = exit.EnemyGroups.FindIndex(g => g.Boss);
            Assert.That(exit.EnemyGroups[at].Kind, Is.EqualTo(EnemyKind.Boss));
            Assert.That(exit.EnemyGroups[at].Letter, Is.EqualTo('G'), "G on the map, as before");
            exit.EnemyGroups[at] = new EnemyGroup(EnemyKind.Guard, 1, boss: true);
            Assert.That(LevelValidator.Validate(level).Any(f => f.Contains("is not one boss")));
        }

        // ---------------- The fuller rooms ----------------

        /// <summary>
        /// Work order 014: floor 1 eight to ten in a combat room, floor 5 twelve to sixteen, the floors between in even
        /// steps, the exit room half as many again; a thousand seeds each keep the placement rules (the validator), and
        /// the top of each range is reached, so the fullest rooms fit. Writes, for each floor, the enemies and their
        /// health in a combat room and in the exit room: lowest, typical (the median) and highest.
        /// </summary>
        [TestCase(1, 8, 10)]
        [TestCase(2, 9, 12)]
        [TestCase(3, 10, 13)]
        [TestCase(4, 11, 15)]
        [TestCase(5, 12, 16)]
        public void FullerRooms_AThousandSeeds_EveryRoomFits_AndTheTopIsReached(int floor, int fewest, int most)
        {
            var v = new LevelValues { BossAlone = false, LineUps = false }; // work order 130 (changed deliberately): the rooms as drawn, version 23's; the boss room alone is BossAloneTests'
            v.EnemyRange(RoomKind.Combat, floor, out int f, out int m);
            Assert.That((f, m), Is.EqualTo((fewest, most)));
            var combat = new List<int>();
            var exit = new List<int>();
            var combatHp = new List<long>();
            var exitHp = new List<long>();
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, v);
                List<string> faults = LevelValidator.Validate(level, v);
                Assert.That(faults, Is.Empty, "seed " + s + " floor " + floor);
                foreach (LevelRoom r in level.Rooms)
                {
                    if (r.Kind != RoomKind.Combat && r.Kind != RoomKind.Exit) continue;
                    // The elite of the elite room comes besides the room's own (work order 017): counted apart.
                    int n = r.EnemyGroups.Where(g => !g.Boss && !g.Elite).Sum(g => g.Count);
                    long hp = r.EnemyGroups.Where(g => !g.Boss && !g.Elite).Sum(g => (long)v.HealthOf(g.Kind) * g.Count);
                    (r.Kind == RoomKind.Combat ? combat : exit).Add(n);
                    (r.Kind == RoomKind.Combat ? combatHp : exitHp).Add(hp);
                }
            }
            v.EnemyRange(RoomKind.Exit, floor, out int ef, out int em);
            Assert.That(combat.Min(), Is.EqualTo(fewest));
            Assert.That(combat.Max(), Is.EqualTo(most), "the fullest combat room is made, and fits");
            Assert.That(exit.Min(), Is.EqualTo(ef));
            Assert.That(exit.Max(), Is.EqualTo(em), "the fullest exit room is made, and fits");
            string Line<T>(List<T> xs) where T : System.IComparable<T>
            {
                List<T> sorted = xs.OrderBy(x => x).ToList();
                return string.Format(s_Inv, "{0} / {1} / {2}", sorted[0], sorted[sorted.Count / 2], sorted[sorted.Count - 1]);
            }
            TestContext.WriteLine(string.Format(s_Inv,
                "ROOMS014 floor {0}: combat room enemies {1}, health {2}; exit room enemies {3}, health {4} (act boss apart; low / median / high)",
                floor, Line(combat), Line(combatHp), Line(exit), Line(exitHp)));
        }
    }
}
