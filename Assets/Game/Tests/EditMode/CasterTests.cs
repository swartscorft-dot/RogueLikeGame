using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 027 (Kristian's request for a fifth kind, 2026-11-20): the caster's rules. The cast order (two Bone
    /// Spikes, then a Grave Rain); where Grave Rain's circles go for her standing still, moving straight and turning,
    /// and their times; its model's clips and how they fit the game's timing; the mixes per floor with casters, the caps
    /// per room over a thousand seeds a floor, and floor 1 as it was; the validator's new checks; the summary with a
    /// caster in it.
    /// </summary>
    public class CasterTests
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        const float Her = Placeholders.PlayerRadius;

        // ---------------- The casts ----------------

        [Test]
        public void CastOrder_TwoBoneSpikes_ThenAGraveRain_AndOverAgain()
        {
            var v = new CasterValues();
            var tactics = new CasterTactics();
            var names = new List<string>();
            for (int i = 0; i < 6; i++) names.Add(tactics.Choose(10f, v).Name);
            Assert.That(names, Is.EqualTo(new[]
            {
                CasterValues.BoneSpikes, CasterValues.BoneSpikes, CasterValues.GraveRain,
                CasterValues.BoneSpikes, CasterValues.BoneSpikes, CasterValues.GraveRain,
            }));
            Assert.That(tactics.Casts, Is.EqualTo(6));
            Assert.That(CasterTactics.IsBigCast(3, 3) && CasterTactics.IsBigCast(6, 3) && !CasterTactics.IsBigCast(4, 3), Is.True);
        }

        [Test]
        public void ACast_OutOfItsBand_IsNotBegun_AndNotCounted()
        {
            var v = new CasterValues();
            var tactics = new CasterTactics();
            Assert.That(tactics.Choose(3f, v), Is.Null, "nearer than 4 m");
            Assert.That(tactics.Choose(16f, v), Is.Null, "farther than 15 m");
            Assert.That(tactics.Casts, Is.EqualTo(0));
            Assert.That(tactics.Choose(4f, v).Name, Is.EqualTo(CasterValues.BoneSpikes));
            Assert.That(tactics.Choose(15f, v).Name, Is.EqualTo(CasterValues.BoneSpikes));
            Assert.That(tactics.Next(v), Is.EqualTo(CasterValues.GraveRain));
        }

        [Test]
        public void TheCastersNumbers_AreAsTheOrderSays()
        {
            var v = new CasterValues();
            EnemyAttackDefinition spikes = CasterTactics.Find(v.Enemy, CasterValues.BoneSpikes);
            EnemyAttackDefinition rain = CasterTactics.Find(v.Enemy, CasterValues.GraveRain);
            Assert.That(v.Enemy.Health, Is.EqualTo(800));
            Assert.That(new LevelValues().HealthOf(EnemyKind.Caster), Is.EqualTo(500), "500 on a generated floor");
            Assert.That(v.Enemy.MoveSpeed, Is.EqualTo(2.6f));
            Assert.That((v.BodyRadius, v.BodyHeight), Is.EqualTo((0.35f, 1.9f)));
            Assert.That((v.BackOffUnder, v.KeepNear, v.KeepFar), Is.EqualTo((6f, 9f, 13f)));
            Assert.That((spikes.Radius, spikes.Windup, spikes.Recovery, spikes.Damage), Is.EqualTo((2.4f, 1.0f, 1.3f, 25)));
            Assert.That((spikes.MinDistance, spikes.MaxDistance), Is.EqualTo((4f, 15f)));
            Assert.That(PoolRules.IsCounterable(spikes), Is.False);
            Assert.That((rain.Radius, rain.Windup, rain.Recovery, rain.Damage), Is.EqualTo((2.2f, 1.4f, 1.6f, 20)));
            Assert.That(PoolRules.IsCounterable(rain), Is.True, "Grave Rain is its blue attack");
            Assert.That(EnemyKinds.Word(EnemyKind.Caster), Is.EqualTo("caster"));
            Assert.That(EnemyKinds.Letter(EnemyKind.Caster, 1), Is.EqualTo('c'));
        }

        [Test]
        public void GraveRain_ItsCircles_ArePlacedAndBurst_OnTheirTimes()
        {
            var v = new CasterValues();
            Assert.That(Enumerable.Range(0, 3).Select(i => CasterRules.RainPlacedAt(i, v)), Is.EqualTo(new[] { 0f, 0.5f, 1.0f }));
            float[] bursts = Enumerable.Range(0, 3).Select(i => CasterRules.RainBurstAt(i, v)).ToArray();
            Assert.That(bursts[0], Is.EqualTo(1.4f).Within(1e-5f), "the first at the end of the attack's windup");
            Assert.That(bursts[1], Is.EqualTo(1.5f).Within(1e-5f));
            Assert.That(bursts[2], Is.EqualTo(2.0f).Within(1e-5f));
            Assert.That(CasterRules.RainLastBurst(v), Is.EqualTo(2.0f).Within(1e-5f));
            Assert.That(CasterRules.RainWhole(v), Is.EqualTo(3.0f).Within(1e-5f), "windup and recovery: it casts again after 3 s");
        }

        static GroundPoint P(float x, float z) => new GroundPoint(x, z);

        [Test]
        public void GraveRain_SheStandsStill_AllThreeWhereSheStands_AllThreeHit()
        {
            var v = new CasterValues();
            var rain = CasterRules.Rain(t => P(2f, 3f), t => P(0f, 0f), v, Her);
            Assert.That(rain.Length, Is.EqualTo(3));
            foreach (var c in rain)
            {
                Assert.That(c.place.DistanceTo(P(2f, 3f)), Is.LessThan(1e-5f));
                Assert.That(c.hit, Is.True);
            }
        }

        // Work order 034 (Grave Rain's lead): a later circle is led by her velocity times LeadSeconds (0.8 s), at most
        // LeadMost (5 m); it was 1.5 m whatever her speed, and at her full run of 6 m/s a straight run escaped all three.
        // The brief's intent, at standing, 3 m/s and 6 m/s straight, and a turn: standing still takes three, a straight
        // run takes the second and third, a change of way avoids them.

        [Test]
        public void GraveRain_TheLead_IsHerVelocityTimesLeadSeconds_NoFartherThanLeadMost()
        {
            var v = new CasterValues();
            Assert.That((v.LeadSeconds, v.LeadMost), Is.EqualTo((0.8f, 5f)));
            Assert.That(CasterRules.Lead(P(0f, 0f), v).Length, Is.EqualTo(0f), "standing: none");
            Assert.That(CasterRules.Lead(P(0.4f, 0f), v).Length, Is.EqualTo(0f), "slower than MovingOver: none");
            Assert.That(CasterRules.Lead(P(3f, 0f), v).DistanceTo(P(2.4f, 0f)), Is.LessThan(1e-5f), "3 m/s: 2.4 m ahead");
            Assert.That(CasterRules.Lead(P(0f, -6f), v).DistanceTo(P(0f, -4.8f)), Is.LessThan(1e-5f), "6 m/s: 4.8 m ahead");
            Assert.That(CasterRules.Lead(P(10f, 0f), v).DistanceTo(P(5f, 0f)), Is.LessThan(1e-5f), "10 m/s (a dash): no farther than 5 m");
            Assert.That(CasterRules.RainPlace(0, P(1f, 1f), P(6f, 0f), v).DistanceTo(P(1f, 1f)), Is.LessThan(1e-5f), "the first circle is never led");
        }

        [TestCase(3f)]
        [TestCase(6f)]
        public void GraveRain_SheMovesStraight_TheLaterCirclesAreLed_AndTheSecondAndThirdHit(float speed)
        {
            var v = new CasterValues();
            // East at this speed: 3 m/s is hers shooting on the move (half her run), 6 m/s her full run.
            var rain = CasterRules.Rain(t => P(speed * t, 0f), t => P(speed, 0f), v, Her);
            float lead = speed * v.LeadSeconds;
            Assert.That(rain[0].place.DistanceTo(P(0f, 0f)), Is.LessThan(1e-5f), "the first where she stood");
            Assert.That(rain[1].place.DistanceTo(P(speed * 0.5f + lead, 0f)), Is.LessThan(1e-4f), "the second led from where she is at 0.5 s");
            Assert.That(rain[2].place.DistanceTo(P(speed * 1.0f + lead, 0f)), Is.LessThan(1e-4f), "the third led from where she is at 1 s");
            for (int i = 1; i < 3; i++)
            {
                float burst = CasterRules.RainBurstAt(i, v);
                TestContext.WriteLine(string.Format(s_Inv, "CASTERRAIN straight at {0} m/s: circle {1} placed {2:0.00} m ahead of her, bursts with her {3:0.00} m from its middle (radius {4})",
                    speed, i + 1, lead, rain[i].place.DistanceTo(P(speed * burst, 0f)), CasterTactics.Find(v.Enemy, CasterValues.GraveRain).Radius));
            }
            Assert.That(rain.Select(c => c.hit), Is.EqualTo(new[] { false, true, true }), "a straight run takes the second and third");
        }

        [TestCase(3f)]
        [TestCase(6f)]
        public void GraveRain_SheTurnsAfterEachCircle_TheSecondAndThirdMiss(float s)
        {
            var v = new CasterValues();
            // East until 0.6 s, north until 1.1 s, then west: a turn just after each later circle is placed.
            GroundPoint At(float t) =>
                t <= 0.6f ? P(s * t, 0f)
                : t <= 1.1f ? P(s * 0.6f, s * (t - 0.6f))
                : P(s * 0.6f - s * (t - 1.1f), s * 0.5f);
            GroundPoint Velocity(float t) => t < 0.6f ? P(s, 0f) : t < 1.1f ? P(0f, s) : P(-s, 0f);
            var rain = CasterRules.Rain(At, Velocity, v, Her);
            float lead = s * v.LeadSeconds;
            Assert.That(rain[1].place.DistanceTo(P(s * 0.5f + lead, 0f)), Is.LessThan(1e-4f), "led east");
            Assert.That(rain[2].place.DistanceTo(P(s * 0.6f, s * 0.4f + lead)), Is.LessThan(1e-4f), "led north");
            TestContext.WriteLine("CASTERRAIN turning at " + s + " m/s: hits " + string.Join(", ", rain.Select(c => c.hit)));
            Assert.That(rain[1].hit, Is.False, "turning avoids the second");
            Assert.That(rain[2].hit, Is.False, "and the third");
        }

        [Test]
        public void ACircle_HurtsHer_OnlyWhenSheTouchesIt()
        {
            Assert.That(CasterRules.Hits(P(0f, 0f), 2.4f, P(2.8f, 0f), Her), Is.True, "her edge inside");
            Assert.That(CasterRules.Hits(P(0f, 0f), 2.4f, P(3.0f, 0f), Her), Is.False, "outside");
        }

        // ---------------- The model ----------------

        [Test]
        public void TheModelSpec_HasEveryTake_WithItsLength_Loop_AndRelease()
        {
            EnemyModelSpec spec = EnemyModelSpec.Caster;
            Assert.That(EnemyModelSpec.Of(EnemyKind.Caster), Is.SameAs(spec));
            Assert.That(EnemyModelSpec.All, Has.Member(spec));
            Assert.That(spec.Name, Is.EqualTo("Crypt_BoneCaster_01"));
            Assert.That(spec.HasStagger, Is.True);
            Assert.That(spec.Glow, Is.GreaterThan(0), "its eyes, palms and amulet glow");
            var want = new (string name, double length, bool loop, double strike)[]
            {
                ("Idle", 3.967, true, -1), ("Walk", 1.000, true, -1), ("Run", 0.600, true, -1),
                ("Cast", 1.967, false, 0.600), ("CastBig", 2.667, false, 1.733), ("Stagger", 1.500, false, -1), ("Death", 1.533, false, -1),
            };
            Assert.That(spec.Clips.Length, Is.EqualTo(want.Length));
            foreach (var w in want)
            {
                EnemyClipSpec c = spec.Clip(w.name);
                Assert.That(c, Is.Not.Null, w.name);
                Assert.That((c.Length, c.Loop, c.Strike), Is.EqualTo((w.length, w.loop, w.strike)), w.name);
            }
            Assert.That(spec.ClipForAttack(CasterValues.BoneSpikes).Name, Is.EqualTo("Cast"));
            Assert.That(spec.ClipForAttack(CasterValues.GraveRain).Name, Is.EqualTo("CastBig"));
            // It walks at 2.6 m/s: its walk, not its run.
            Assert.That(EnemyGaits.For(new CasterValues().Enemy.MoveSpeed, EnemyGait.Walk, spec.WalkStride, spec.RunStride), Is.EqualTo(EnemyGait.Walk));
        }

        [Test]
        public void EachCastsClip_ReachesItsRelease_AsTheGamesStrikeLands_WithinTheSpeedLimits()
        {
            var v = new CasterValues();
            foreach (EnemyAttackDefinition a in v.Enemy.Attacks)
            {
                EnemyClipSpec clip = EnemyModelSpec.Caster.ClipForAttack(a.Name);
                AttackClipPlan plan = AttackClipPlan.For(a.Windup, a.Recovery, clip.Strike, clip.Length);
                Assert.That(plan.InWindup(a.Windup), Is.EqualTo(clip.Strike).Within(1e-9), a.Name);
                Assert.That(plan.WindupSpeed, Is.InRange(0.5, 2.0), a.Name);
                Assert.That(plan.RecoverySpeed, Is.InRange(0.5, 2.0), a.Name);
                Assert.That(plan.Hold, Is.EqualTo(0), a.Name);
                TestContext.WriteLine(string.Format(s_Inv, "CASTERCLIP {0}: clip {1} from {2:0.###} s at x{3:0.###} to its release {4:0.###} s at the game's strike ({5:0.###} s), then x{6:0.###}",
                    a.Name, clip.Name, plan.Start, plan.WindupSpeed, clip.Strike, a.Windup, plan.RecoverySpeed));
            }
        }

        // ---------------- The floors ----------------

        /// <summary>The casters switched on (work order 034: they were off by default; work order 054: on by default), the rest as the defaults.</summary>
        static LevelValues On() => new LevelValues { Casters = true };

        [Test]
        public void TheMixesPerFloor_HoldCastersFromFloor2_AsData_AndTheCapsAreAsTheOrderSays()
        {
            var v = On(); // work order 034: with the casters on; off, every cap is 0 (TheSwitchOff_...)
            int[][] want = { new[] { 0, 0, 0 }, new[] { 0, 0, 1 }, new[] { 0, 1, 1 }, new[] { 0, 1, 2 }, new[] { 1, 1, 2 } };
            for (int floor = 1; floor <= 5; floor++)
            {
                Assert.That(v.EnemiesOf(floor).Mixes.Select(m => m.Casters), Is.EqualTo(want[floor - 1]), "floor " + floor);
                TestContext.WriteLine("CASTERMIX floor " + floor + ": " + string.Join(", ", v.EnemiesOf(floor).Mixes.Select(m => m.Name + " " + m.Casters)));
            }
            int[] combat = { 0, 1, 1, 2, 2 }, exit = { 0, 2, 2, 3, 3 };
            for (int floor = 1; floor <= 5; floor++)
            {
                Assert.That(v.CastersMost(RoomKind.Combat, floor), Is.EqualTo(combat[floor - 1]), "combat room, floor " + floor);
                Assert.That(v.CastersMost(RoomKind.Exit, floor), Is.EqualTo(exit[floor - 1]), "exit room, floor " + floor);
                Assert.That(v.CastersMost(RoomKind.Entrance, floor) + v.CastersMost(RoomKind.Treasure, floor), Is.EqualTo(0));
            }
            Assert.That(v.EliteKinds(2), Has.No.Member(EnemyKind.Caster));
            Assert.That(v.EliteKinds(3), Has.Member(EnemyKind.Caster), "an elite may be a caster from floor 3");
            Assert.That(v.EliteHealthOf(EnemyKind.Caster), Is.EqualTo(4 * v.CasterHealth));
        }

        [Test]
        public void ACaster_TakesTheMixesPlaceOfAShooterOrABrute_TheRoomsCountStaysTheSame()
        {
            var with = On(); // work order 034: the casters on
            var without = NoCasters();
            for (ulong s = 0; s < 200; s++)
                foreach (RoomKind kind in new[] { RoomKind.Combat, RoomKind.Exit })
                    for (int floor = 2; floor <= 5; floor++)
                    {
                        List<EnemyGroup> a = LevelGenerator.DrawGroups(kind, floor, new LevelRandom(s * 7919 + 1), with);
                        List<EnemyGroup> b = LevelGenerator.DrawGroups(kind, floor, new LevelRandom(s * 7919 + 1), without);
                        int Of(List<EnemyGroup> g, EnemyKind k) => g.Where(x => x.Kind == k).Sum(x => x.Count);
                        string what = kind + " floor " + floor + " draw " + s;
                        Assert.That(a.Sum(g => g.Count), Is.EqualTo(b.Sum(g => g.Count)), what + ": the same count");
                        int casters = Of(a, EnemyKind.Caster);
                        Assert.That(casters, Is.LessThanOrEqualTo(with.CastersMost(kind, floor)), what);
                        Assert.That(Of(a, EnemyKind.Swarmer), Is.EqualTo(Of(b, EnemyKind.Swarmer)), what + ": the swarmers as they were");
                        Assert.That(Of(a, EnemyKind.Guard), Is.EqualTo(Of(b, EnemyKind.Guard)), what + ": the guards as they were");
                        Assert.That(Of(a, EnemyKind.Shooter) + Of(a, EnemyKind.Brute) + casters,
                                    Is.EqualTo(Of(b, EnemyKind.Shooter) + Of(b, EnemyKind.Brute)), what + ": in place of shooters and brutes");
                        Assert.That(a.Where(g => g.Kind == EnemyKind.Caster).All(g => g.Count == 1), what + ": each alone");
                        // Work order 034: after the caps, so the caps on brutes and shooters still hold.
                        int more = kind == RoomKind.Exit ? with.ExitRoomKindMore : 0;
                        Assert.That(Of(a, EnemyKind.Brute), Is.LessThanOrEqualTo(with.BrutesMostInRoom + more), what + ": the brutes' cap");
                        Assert.That(Of(a, EnemyKind.Shooter), Is.LessThanOrEqualTo(with.ShootersMostInRoom + more), what + ": the shooters' cap");
                    }
        }

        /// <summary>
        /// The merged rules with the caster code bypassed (work order 034): the switch on, but no mix holds one, no room may
        /// and no elite can be one. Before work order 034 the values as they were before work order 027.
        /// </summary>
        static LevelValues NoCasters()
        {
            var v = new LevelValues { Casters = true, CastersMostInRoom = new[] { 0 }, EliteCasterFromFloor = 99 };
            foreach (FloorEnemies f in v.Floors)
                foreach (EnemyMix m in f.Mixes) m.Casters = 0;
            return v;
        }

        // Work order 027: casters change what the seeds' floors hold from floor 2 on; floor 1 must not change at all. Its
        // golden maps (LevelGeneratorTests) are as they were but for the version in their first line, and here a thousand
        // seeds give the same floor 1 with the casters as with none at all.
        [Test]
        public void Floor1_IsUnchanged_ByTheCasters()
        {
            LevelValues none = NoCasters(), on = On(); // work order 034: with the casters on
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, 1, on);
                Assert.That(level.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Caster), Is.False, "no caster on floor 1, seed " + s);
                Assert.That(level.Describe(), Is.EqualTo(LevelGenerator.Generate(s, 1, none).Describe()), "seed " + s);
            }
        }

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_EveryRoomsCastersAreWithinItsCap_AtTheFarSide_AndApart(int floor)
        {
            var v = On(); // work order 034: with the casters on, and within both sets of caps (the casters' and main's)
            var perFloor = new List<int>();
            int rooms = 0, roomsWith = 0, eliteCasters = 0;
            double nearestApart = double.MaxValue;
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, v); // valid under these values, or it throws
                int onFloor = 0;
                foreach (LevelRoom r in level.Rooms)
                {
                    int casters = r.EnemyGroups.Where(g => g.Kind == EnemyKind.Caster && !g.Elite).Sum(g => g.Count);
                    Assert.That(casters, Is.LessThanOrEqualTo(v.CastersMost(r.Kind, floor)), "seed " + s + " room " + r.Order + " (" + r.Kind + ")");
                    int more = r.Kind == RoomKind.Exit ? v.ExitRoomKindMore : 0;
                    Assert.That(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Brute && !g.Elite).Sum(g => g.Count), Is.LessThanOrEqualTo(v.BrutesMostInRoom + more),
                                "the brutes' cap, seed " + s + " room " + r.Order);
                    Assert.That(r.EnemyGroups.Where(g => g.Kind == EnemyKind.Shooter && !g.Elite).Sum(g => g.Count), Is.LessThanOrEqualTo(v.ShootersMostInRoom + more),
                                "the shooters' cap, seed " + s + " room " + r.Order);
                    if (r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit)
                    {
                        v.EnemyRange(r.Kind, floor, out int fewest, out int most);
                        Assert.That(r.EnemyGroups.Where(g => !g.Elite && !g.Boss).Sum(g => g.Count), Is.InRange(fewest, most), "the room's count, seed " + s + " room " + r.Order);
                    }
                    if (r.Kind == RoomKind.Combat || r.Kind == RoomKind.Exit)
                    {
                        rooms++;
                        if (casters > 0) roomsWith++;
                    }
                    var points = new List<LevelPoint>();
                    for (int i = 0; i < r.EnemyGroups.Count; i++)
                    {
                        EnemyGroup g = r.EnemyGroups[i];
                        if (g.Kind != EnemyKind.Caster) continue;
                        Assert.That(g.Count, Is.EqualTo(1), "a caster stands alone");
                        if (g.Elite) eliteCasters++;
                        onFloor++;
                        foreach (LevelPoint other in points) nearestApart = Math.Min(nearestApart, other.DistanceTo(r.EnemyPoints[i]));
                        points.Add(r.EnemyPoints[i]);
                    }
                }
                perFloor.Add(onFloor);
            }
            perFloor.Sort();
            TestContext.WriteLine(string.Format(s_Inv,
                "CASTERS floor {0} seeds 0-999: casters on a floor lowest {1}, median {2}, highest {3}, mean {4:0.00}; rooms holding one {5} of {6}; " +
                "elite casters {7}; nearest two casters of a room {8:0.0} m",
                floor, perFloor[0], perFloor[perFloor.Count / 2], perFloor[perFloor.Count - 1], perFloor.Average(), roomsWith, rooms,
                eliteCasters, nearestApart == double.MaxValue ? 0 : nearestApart));
            if (floor == 1) Assert.That(perFloor[perFloor.Count - 1], Is.EqualTo(0), "none on floor 1");
            else Assert.That(perFloor[perFloor.Count - 1], Is.GreaterThan(0), "casters appear from floor 2");
            if (floor < 3) Assert.That(eliteCasters, Is.EqualTo(0), "no elite caster before floor 3");
            else Assert.That(eliteCasters, Is.GreaterThan(0), "an elite may be a caster from floor 3");
            if (nearestApart != double.MaxValue) Assert.That(nearestApart, Is.GreaterThanOrEqualTo(v.CasterSpacing - 1e-6));
        }

        [Test]
        public void Validator_ReportsTooManyCasters_AndTwoCastersNearEachOther()
        {
            LevelValues on = On(); // work order 034: with the casters on
            LevelLayout level = LevelGenerator.Generate(42UL, 1, on);
            Assume.That(LevelValidator.Validate(level, on), Is.Empty);
            LevelRoom room = level.Rooms.First(r => r.Kind == RoomKind.Combat && !r.Elite);
            int i = room.EnemyGroups.FindIndex(g => g.Count == 1 && g.Kind != EnemyKind.Guard);
            Assume.That(i, Is.GreaterThanOrEqualTo(0));
            room.EnemyGroups[i] = new EnemyGroup(EnemyKind.Caster);
            Assert.That(LevelValidator.Validate(level, on).Any(f => f.Contains("casters, floor 1 at most 0")), string.Join("\n", LevelValidator.Validate(level, on)));

            LevelLayout four = LevelGenerator.Generate(42UL, 4, on);
            Assume.That(LevelValidator.Validate(four, on), Is.Empty);
            // Two enemies standing alone in a room of floor 4 made casters, the nearest two such: nearer than 8 m, a fault.
            LevelRoom r4 = null;
            int a = -1, b = -1;
            double nearest = double.MaxValue;
            foreach (LevelRoom r in four.Rooms.Where(x => x.Kind == RoomKind.Combat && !x.Elite))
                for (int p = 0; p < r.EnemyGroups.Count; p++)
                    for (int q = p + 1; q < r.EnemyGroups.Count; q++)
                    {
                        if (r.EnemyGroups[p].Count != 1 || r.EnemyGroups[q].Count != 1) continue;
                        double d = r.EnemyPoints[p].DistanceTo(r.EnemyPoints[q]);
                        if (d < nearest) { nearest = d; r4 = r; a = p; b = q; }
                    }
            Assume.That(nearest, Is.LessThan(new LevelValues().CasterSpacing), "two enemies alone nearer than 8 m in a room of floor 4");
            for (int k = 0; k < r4.EnemyGroups.Count; k++)
                if (r4.EnemyGroups[k].Kind == EnemyKind.Caster) r4.EnemyGroups[k] = new EnemyGroup(EnemyKind.Brute);
            r4.EnemyGroups[a] = new EnemyGroup(EnemyKind.Caster);
            r4.EnemyGroups[b] = new EnemyGroup(EnemyKind.Caster);
            List<string> faults = LevelValidator.Validate(four, on);
            Assert.That(faults.Any(f => f.Contains("casters " + a + " and " + b + " are nearer")), string.Join("\n", faults));
        }

        // ---------------- Work order 034: the switch, the turns, the sounds and effects ----------------

        // Off (the default until work order 054, which set them on: Kristian, 2026-11-21, "Castere på ja"), no floor holds a
        // caster and no elite is one, and every seed's rooms, enemies and elites are
        // exactly what the merged rules give with the caster code bypassed: a thousand seeds a floor number, every field
        // but the props compared with the props on (their themes are drawn from their own stream), and with the themes off
        // the whole floor. (The golden maps of LevelGeneratorTests compare four of them with main's version 7 maps.)
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void TheSwitchOff_GivesNoCaster_AndTheFloorsOfTheRulesWithTheCasterCodeBypassed(int floor)
        {
            Assert.That(new LevelValues().Casters, Is.True, "on by default (work order 054)");
            var off = new LevelValues { Casters = false };
            LevelValues bypassed = NoCasters();
            for (int f = 1; f <= 5; f++)
            {
                Assert.That(off.CastersMost(RoomKind.Combat, f) + off.CastersMost(RoomKind.Exit, f), Is.EqualTo(0), "no room may hold one, floor " + f);
                Assert.That(off.EliteKinds(f), Has.No.Member(EnemyKind.Caster), "no elite may be one, floor " + f);
            }
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout level = LevelGenerator.Generate(s, floor, off);
                Assert.That(level.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Caster), Is.False, "no caster, seed " + s);
                Assert.That(level.Describe(props: false), Is.EqualTo(LevelGenerator.Generate(s, floor, bypassed).Describe(props: false)), "seed " + s);
                if (s % 10 == 0)
                {
                    var themesOff = new LevelValues { Themes = false, Casters = false };
                    var bypassedThemesOff = NoCasters();
                    bypassedThemesOff.Themes = false;
                    Assert.That(LevelGenerator.Generate(s, floor, themesOff).Describe(), Is.EqualTo(LevelGenerator.Generate(s, floor, bypassedThemesOff).Describe()),
                                "seed " + s + ", props too, with the themes off");
                }
            }
        }

        [Test]
        public void ACaster_IsRangedForTheTurnsToAttack()
        {
            Assert.That(AttackTurns.SortOf(EnemyKind.Caster), Is.EqualTo(TurnSort.Ranged), "it waits with the shooters");
            var turns = new AttackTurns();
            // Two ranged winding up (a shooter and a caster): a third ranged waits; light ones never do.
            Assert.That(turns.Ask(1, AttackTurns.SortOf(EnemyKind.Shooter), 0f), Is.True);
            turns.Began(1, TurnSort.Ranged, 0f, 1.4f);
            Assert.That(turns.Ask(2, AttackTurns.SortOf(EnemyKind.Caster), 1f), Is.True);
            turns.Began(2, TurnSort.Ranged, 1f, 1.4f);
            Assert.That(turns.Ask(3, AttackTurns.SortOf(EnemyKind.Caster), 1.2f), Is.False, "a caster waits its turn as a ranged enemy");
            Assert.That(turns.Ask(4, TurnSort.None, 1.2f), Is.True, "an elite takes no turn: of no sort, never held");
        }

        [Test]
        public void TheTurnsQuestion_DoesNotCountACast()
        {
            var v = new CasterValues();
            var tactics = new CasterTactics();
            for (int i = 0; i < 5; i++) Assert.That(tactics.Peek(10f, v).Name, Is.EqualTo(CasterValues.BoneSpikes));
            Assert.That(tactics.Casts, Is.EqualTo(0), "asking is not casting");
            Assert.That(tactics.Peek(20f, v), Is.Null, "out of its band");
            tactics.Choose(10f, v);
            tactics.Choose(10f, v);
            Assert.That(tactics.Peek(10f, v).Name, Is.EqualTo(CasterValues.GraveRain), "the cast order holds");
        }

        [Test]
        public void ItsSounds_AndItsSizeForTheEffects()
        {
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Caster, false), Is.EqualTo(PlaceholderSound.WindupRed), "Bone Spikes begins: red");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Caster, true), Is.EqualTo(PlaceholderSound.WindupBlue), "Grave Rain begins: blue");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.EnemyStruck, EnemyKind.Caster), Is.EqualTo(PlaceholderSound.Strike), "a circle bursts: the strike");
            var sizes = new FxSizes();
            Assert.That(sizes.KindScale(EnemyKind.Caster), Is.EqualTo(sizes.KindScale(EnemyKind.Shooter)), "a shooter's size");
            Assert.That(sizes.ShardCount(EnemyKind.Caster), Is.EqualTo(sizes.ShardCount(EnemyKind.Shooter)));
            Assert.That(CombatFxRules.StrikeShapes(CasterTactics.Find(new CasterValues().Enemy, CasterValues.BoneSpikes).Shape), Is.EqualTo(new[] { FxShape.Ring, FxShape.Spark }),
                        "its burst is a circle strike: no new shape");
        }

        // ---------------- The summary ----------------

        [Test]
        public void Summary_CountsTheCastersKillsAndDamage_AndItsOwnEventsDoNotBreakIt()
        {
            SessionEvent Ev(double t, string type, string name = null, int? damage = null, string result = null) =>
                new SessionEvent(t, type, name) { Enemy = "caster", EnemyId = 4, Damage = damage, Result = result };
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake),
                Ev(2, SessionEventTypes.EnemyAttack, CasterValues.BoneSpikes),
                Ev(2, SessionEventTypes.CasterCircle, CasterValues.BoneSpikes, result: SessionWords.Placed),
                Ev(3, SessionEventTypes.EnemyStrike, CasterValues.BoneSpikes, 25, SessionWords.Landed),
                Ev(6, SessionEventTypes.EnemyAttack, CasterValues.GraveRain),
                Ev(6, SessionEventTypes.CasterCircle, CasterValues.GraveRain, result: SessionWords.Placed),
                Ev(6.5, SessionEventTypes.CasterCircle, CasterValues.GraveRain, result: SessionWords.OffTheGround),
                Ev(6.8, SessionEventTypes.EnemyCountered, CasterValues.GraveRain),
                Ev(6.8, SessionEventTypes.EnemyDazed),
                Ev(6.8, SessionEventTypes.CasterCircleFaded, CasterValues.GraveRain),
                Ev(9, SessionEventTypes.EnemyStrike, CasterValues.GraveRain, 20, SessionWords.Landed),
                Ev(10, SessionEventTypes.EnemyDeath),
                new SessionEvent(12, SessionEventTypes.SessionEnd),
            };
            foreach (SessionEvent e in events) Assert.That(SessionEvent.Parse(e.ToJson()).Type, Is.EqualTo(e.Type));
            SessionStats stats = SessionSummary.Compute(events);
            EnemyKindRow caster = stats.EnemyKinds.Single(r => r.Kind == "caster");
            Assert.That(caster.Kills, Is.EqualTo(1));
            Assert.That(caster.DamageTaken, Is.EqualTo(45));
            Assert.That(caster.DamageByAttack, Is.EqualTo(new[]
            {
                new KeyValuePair<string, int>(CasterValues.BoneSpikes, 25), new KeyValuePair<string, int>(CasterValues.GraveRain, 20),
            }));
            string text = SessionSummary.Format(stats);
            StringAssert.Contains("caster", text);
            TestContext.WriteLine(text);
        }
    }
}
