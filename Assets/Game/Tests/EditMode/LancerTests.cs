using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 108 (2026-11-24, "Lansereren (ny fiende)": "Etasje 4: stormer i rett linje langveisfra, varslet på kroppen, og
    /// blir ør når den treffer en vegg eller rekvisitt"), its rules: the numbers as the order gives them; the line fixed at the
    /// wind-up's end and never turned; each of the three endings (it meets her, a wall or a prop stops it, it runs its length);
    /// the daze of the crash (its bar emptied: its time and its extra damage); the counter (blue, and from its front all through
    /// the wind-up); the caps a floor by the bombers' pattern (none before floor 4, one a combat room on floor 4, two on floor 5,
    /// never in the first combat room, the exit room or the boss's room, each in place of a swarmer); with the switch off no lancer
    /// in a thousand seeds and the floors field for field as before (and the generator does not place them yet in any case); the
    /// room lab's preset; the summary's block; the sounds.
    /// </summary>
    public class LancerTests
    {
        static readonly LancerValues V = new LancerValues();
        static EnemyAttackDefinition ChargeOf(LancerValues v) => v.Enemy.Attacks.Single(a => a.Rush > 0f);
        static GroundPoint P(float x, float z) => new GroundPoint(x, z);

        // ---------------- The numbers ----------------

        [Test]
        public void TheCharge_IsTheOrdersFirstGuesses_Blue_AndItsBodyBetweenAShootersAndABrutes()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            Assert.That(c.Name, Is.EqualTo(LancerValues.Charge));
            Assert.That(c.Shape, Is.EqualTo(EnemyAttackShape.Line));
            Assert.That(c.Windup, Is.EqualTo(1.1f), "a wind-up of about 1.1 s");
            Assert.That(c.Rush, Is.EqualTo(16f), "about 16 m/s");
            Assert.That(c.Length, Is.EqualTo(18f), "up to about 18 m");
            Assert.That(c.HalfWidth, Is.EqualTo(0.6f), "about 0.6 m to a side");
            Assert.That(c.Damage, Is.EqualTo(MeleeAttacks.Find(MeleeAttacks.Brute(), MeleeAttacks.ShieldBash).Damage), "a brute's Shield bash");
            Assert.That(c.Recovery, Is.EqualTo(1f), "it recovers about 1 s");
            Assert.That(PoolRules.IsCounterable(c), Is.True, "Stopping Shot counters it, as the boss's Charge");
            Assert.That(MeleeYouReadRules.GlintIsBlue(c) && MeleeYouReadRules.GlintGrows(c), Is.True, "its glint blue, growing through the wind-up");
            Assert.That(V.KeepNear, Is.EqualTo(12f));
            Assert.That(V.KeepFar, Is.EqualTo(16f));
            Assert.That(c.MinDistance, Is.LessThanOrEqualTo(V.KeepNear));
            Assert.That(c.MaxDistance, Is.GreaterThanOrEqualTo(V.KeepFar));
            Assert.That(V.CrashDaze, Is.EqualTo(1.5f), "dazed about 1.5 s");
            Assert.That(V.Stagger.DazeSeconds, Is.EqualTo(V.CrashDaze), "the crash's daze is its bar's");
            Assert.That(V.Stagger.DazedDamageMultiplier, Is.EqualTo(new GuardValues().Stagger.DazedDamageMultiplier), "and its extra damage the stagger's");

            var lv = new LevelValues();
            Assert.That(lv.HealthOf(EnemyKind.Lancer), Is.EqualTo(lv.LancerHealth));
            Assert.That(lv.LancerHealth, Is.GreaterThan(lv.ShooterHealth).And.LessThan(lv.HealthOf(EnemyKind.Brute)), "a floor's: between a shooter's and a brute's");
            Assert.That(V.Enemy.Health, Is.GreaterThan(new ShooterValues().Enemy.Health), "the practice room's: over a shooter's (the practice brute's is the asset's, 12000)");
            Assert.That(lv.EliteKinds(5), Has.No.Member(EnemyKind.Lancer), "no elite form");

            Assert.That(EnemyKinds.Word(EnemyKind.Lancer), Is.EqualTo("lancer"));
            Assert.That(EnemyKinds.Letter(EnemyKind.Lancer, 1), Is.EqualTo('l'));
            var letters = new HashSet<char>();
            foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind))) Assert.That(letters.Add(EnemyKinds.Letter(k, 1)), Is.True, "a letter of its own: " + k);
            EnemyModelSpec spec = EnemyModelSpec.Of(EnemyKind.Lancer);
            Assert.That(spec, Is.SameAs(EnemyModelSpec.Lancer));
            Assert.That(spec.Name, Is.EqualTo(EnemyModelSpec.Brute.Name), "a skeleton model the project has: the warrior's");
            Assert.That(EnemyModelSpec.All, Has.Member(spec));
            Assert.That(spec.ClipForAttack(LancerValues.Charge), Is.Null, "no clip for its charge: it stands, then runs");
            Assert.That((int)EnemyKind.Lancer, Is.EqualTo(8), "at the end of the kinds");
        }

        // ---------------- The line ----------------

        [Test]
        public void TheLine_FollowsHerThroughTheWindup_IsFixedAtItsEnd_AndNeverTurns()
        {
            GroundPoint me = P(0f, 0f), faced = P(0f, 1f);
            // Through the wind-up it faces her wherever she goes.
            foreach (GroundPoint her in new[] { P(0f, 14f), P(3f, 13f), P(-5f, 12f) })
            {
                GroundPoint f = LancerRules.Facing(me, her, faced);
                Assert.That(f.X * her.Z - f.Z * her.X, Is.EqualTo(0f).Within(1e-4f), "at her " + her);
                Assert.That(f.Dot(her), Is.GreaterThan(0f));
                faced = f;
            }
            Assert.That(LancerRules.Facing(me, me, P(0f, 2f)).Z, Is.EqualTo(1f).Within(1e-5f), "on it: the way it faced");

            // At the end of the wind-up she is at (-5, 12): the line is there, and stays there whatever she does after.
            GroundPoint endHer = P(-5f, 12f);
            GroundPoint line = LancerRules.ChargeLine(me, endHer, faced);
            var charge = new LancerCharge();
            EnemyAttackDefinition c = ChargeOf(V);
            charge.Begin(me, line, c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 10f);
            GroundPoint before = me;
            for (float t = 10.05f; t < 12f && charge.Running; t += 0.05f)
            {
                GroundPoint at = charge.At(t);
                GroundPoint fromStart = at - me;
                Assert.That(fromStart.X * line.Z - fromStart.Z * line.X, Is.EqualTo(0f).Within(1e-3f), "on the line it had, at " + t);
                // She steps well aside after the wind-up: it does not follow her.
                charge.Step(before, at, t, P(6f, 12f), Placeholders.PlayerRadius, c.HalfWidth, true);
                before = at;
            }
            Assert.That(charge.End, Is.EqualTo(LancerEnd.RanOut));
            Assert.That(charge.EndedAfter, Is.EqualTo(c.Length).Within(1e-3f), "its whole length");
        }

        // ---------------- The three endings, and the daze ----------------

        static LancerEnd RunIt(LancerCharge charge, GroundPoint her, bool herThere = true, float from = 0f)
        {
            EnemyAttackDefinition c = ChargeOf(V);
            GroundPoint before = charge.From;
            for (float t = from + 1f / 60f; t < from + 3f; t += 1f / 60f)
            {
                GroundPoint at = charge.At(t);
                LancerEnd end = charge.Step(before, at, t, her, Placeholders.PlayerRadius, c.HalfWidth, herThere);
                if (end != LancerEnd.None) return end;
                before = at;
            }
            return LancerEnd.None;
        }

        [Test]
        public void ItMeetsHer_StandingInItsLine_AndStops_ThereNotAtItsEnd()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            var charge = new LancerCharge();
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 0f);
            Assert.That(RunIt(charge, P(0f, 13f)), Is.EqualTo(LancerEnd.MetHer));
            Assert.That(charge.Running, Is.False);
            Assert.That(charge.EndedAfter, Is.LessThan(13f).And.GreaterThan(13f - c.HalfWidth - Placeholders.PlayerRadius - c.Rush / 60f - 1e-3f), "it stops at her");
            Assert.That(LancerRules.StandsAfter(LancerEnd.MetHer, V, c), Is.EqualTo(c.Recovery), "then it recovers");
            // Her edge just in its line, at its side: met; a step more aside: not.
            float edge = c.HalfWidth + Placeholders.PlayerRadius;
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 0f);
            Assert.That(RunIt(charge, P(edge - 0.05f, 13f)), Is.EqualTo(LancerEnd.MetHer), "her edge in its line");
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 0f);
            Assert.That(RunIt(charge, P(edge + 0.05f, 13f)), Is.EqualTo(LancerEnd.RanOut), "a step aside and it passes her");
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 0f);
            Assert.That(RunIt(charge, P(0f, 13f), herThere: false), Is.EqualTo(LancerEnd.RanOut), "dead, she is not met");
        }

        [Test]
        public void AWallOrAProp_StopsIt_ShortOfIt_AndItIsDazed_ByItsBarEmptied()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            var charge = new LancerCharge();
            // A low prop 8 m along its line, she behind it at 13 m.
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, 8f, V.StopShort, LancerStopper.LowProp, 0f);
            Assert.That(charge.Cut, Is.True);
            Assert.That(charge.Length, Is.EqualTo(8f - V.StopShort).Within(1e-5f), "it stops short of the prop, its front at it");
            Assert.That(RunIt(charge, P(0f, 13f)), Is.EqualTo(LancerEnd.Crashed));
            Assert.That(charge.Stopper, Is.EqualTo(LancerStopper.LowProp));
            Assert.That(charge.EndedAfter, Is.EqualTo(8f - V.StopShort).Within(1e-3f));
            Assert.That(LancerRules.CoverStoppedIt(charge.From, charge.Direction, charge.EndedAfter, c.HalfWidth, P(0f, 13f), Placeholders.PlayerRadius),
                        Is.True, "what stopped it stood between them: stopped by cover");
            Assert.That(LancerRules.CoverStoppedIt(charge.From, charge.Direction, charge.EndedAfter, c.HalfWidth, P(3f, 13f), Placeholders.PlayerRadius),
                        Is.False, "she had stepped aside: avoided, though it crashed");
            Assert.That(LancerRules.CoverStoppedIt(charge.From, charge.Direction, charge.EndedAfter, c.HalfWidth, P(0f, 5f), Placeholders.PlayerRadius),
                        Is.False, "she was before the prop: not stopped by cover (it would have met her first)");
            // She stands before the prop: it meets her first.
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, 8f, V.StopShort, LancerStopper.Wall, 0f);
            Assert.That(RunIt(charge, P(0f, 4f)), Is.EqualTo(LancerEnd.MetHer));
            // A wall past its length: not cut, it runs out.
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, c.Length + 1f, V.StopShort, LancerStopper.Wall, 0f);
            Assert.That(charge.Cut, Is.False);
            Assert.That(charge.Stopper, Is.EqualTo(LancerStopper.None));
            Assert.That(RunIt(charge, P(5f, 5f)), Is.EqualTo(LancerEnd.RanOut));
            // The walkable ground ends under it: a crash where it is.
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 0f);
            Assert.That(charge.Step(P(0f, 0f), P(0f, 0.1f), 0.1f, P(5f, 5f), 0.5f, c.HalfWidth, true, blocked: true), Is.EqualTo(LancerEnd.Crashed));
            Assert.That(charge.Stopper, Is.EqualTo(LancerStopper.Edge));
            // At once against a wall: no run at all, a crash.
            charge.Begin(P(0f, 0f), P(0f, 1f), c.Rush, c.Length, 0.2f, V.StopShort, LancerStopper.Wall, 0f);
            Assert.That(charge.Length, Is.EqualTo(0f));
            Assert.That(RunIt(charge, P(5f, 5f)), Is.EqualTo(LancerEnd.Crashed));

            // The daze: its bar emptied at once, dazed for CrashDaze, the stagger's extra damage meanwhile, then full again.
            Assert.That(LancerRules.StandsAfter(LancerEnd.Crashed, V, c), Is.EqualTo(V.CrashDaze));
            var bar = new StaggerBar(V.Stagger);
            bar.Take(bar.Current, 20f, out bool dazed);
            Assert.That(dazed, Is.True);
            Assert.That(bar.IsDazed(20f + V.CrashDaze - 0.01f), Is.True);
            Assert.That(bar.Scale(100, 20.5f), Is.EqualTo(150), "it takes more damage while dazed, as a stagger gives");
            Assert.That(bar.IsDazed(20f + V.CrashDaze + 0.01f), Is.False);
            Assert.That(bar.Tick(20f + V.CrashDaze + 0.01f), Is.True);
            Assert.That(bar.Current, Is.EqualTo(V.Stagger.Max));
        }

        [Test]
        public void ItRunsItsLength_MeetingNothing_AndRecovers()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            var charge = new LancerCharge();
            charge.Begin(P(2f, 3f), P(1f, 1f), c.Rush, c.Length, float.PositiveInfinity, V.StopShort, LancerStopper.None, 5f);
            Assert.That(charge.Direction.Length, Is.EqualTo(1f).Within(1e-5f));
            Assert.That(charge.At(5f + c.Length / c.Rush * 0.5f).DistanceTo(P(2f, 3f)), Is.EqualTo(c.Length * 0.5f).Within(1e-3f), "at its speed");
            Assert.That(charge.At(100f).DistanceTo(P(2f, 3f)), Is.EqualTo(c.Length).Within(1e-3f), "never past its length");
            Assert.That(RunIt(charge, P(-10f, 10f), from: 5f), Is.EqualTo(LancerEnd.RanOut));
            Assert.That(LancerRules.StandsAfter(LancerEnd.RanOut, V, c), Is.EqualTo(c.Recovery));
            Assert.That(LancerRules.StandsAfter(LancerEnd.None, V, c), Is.EqualTo(0f));
        }

        // ---------------- The counter, and what it does between charges ----------------

        [Test]
        public void StoppingShot_FromItsFront_ThroughTheWholeWindup_CountersIt_FromBehindNot()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            float half = new SkillDefinition().CounterHalfAngle; // Stopping Shot's front (60 degrees either side)
            GroundPoint me = P(0f, 0f), faced = P(0f, 1f);
            // Wherever she goes through the wind-up it faces her, so she is always in its front: it can be countered all through.
            foreach (GroundPoint her in new[] { P(0f, 12f), P(8f, 9f), P(-11f, 2f), P(0f, -13f) })
            {
                faced = LancerRules.Facing(me, her, faced);
                Assert.That(PoolRules.InFront(me, faced, her, half), Is.True, "from her at " + her);
                Assert.That(PoolRules.InFront(me, faced, me - (her - me), half), Is.False, "from behind it");
            }
            Assert.That(PoolRules.IsCounterable(c), Is.True);
        }

        [Test]
        public void BetweenCharges_ItKeepsTwelveToSixteenMetres_WithAClearLine()
        {
            EnemyAttackDefinition c = ChargeOf(V);
            Assert.That(LancerRules.Move(6f, true, false, V), Is.EqualTo(LancerMove.Away), "near: away");
            Assert.That(LancerRules.Move(11f, true, true, V), Is.EqualTo(LancerMove.Away), "backing off, on to 12 m");
            Assert.That(LancerRules.Move(11f, true, false, V), Is.EqualTo(LancerMove.Hold), "not backing off, inside its band: it holds");
            Assert.That(LancerRules.Move(14f, true, false, V), Is.EqualTo(LancerMove.Hold));
            Assert.That(LancerRules.Move(14f, false, false, V), Is.EqualTo(LancerMove.Closer), "something in its line: it walks round to her");
            Assert.That(LancerRules.Move(20f, true, false, V), Is.EqualTo(LancerMove.Closer), "far: closer");
            Assert.That(LancerRules.MayCharge(14f, true, c), Is.True);
            Assert.That(LancerRules.MayCharge(14f, false, c), Is.False, "no charge without a clear line");
            Assert.That(LancerRules.MayCharge(6f, true, c), Is.False, "not near");
            Assert.That(LancerRules.MayCharge(20f, true, c), Is.False, "not far");
            // Its pose: lowered over the first part of the wind-up and held.
            Assert.That(LancerRules.Lowered(0f, V), Is.EqualTo(0f));
            Assert.That(LancerRules.Lowered(V.LowerShare, V), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(LancerRules.Lowered(1f, V), Is.EqualTo(1f));
            Assert.That(LancerRules.LanceDegrees(0f, V), Is.EqualTo(V.LanceRestDegrees));
            Assert.That(LancerRules.LanceDegrees(1f, V), Is.EqualTo(V.LanceDownDegrees));
            Assert.That(LancerRules.LeanDegrees(1f, V), Is.EqualTo(V.LeanDegrees));
        }

        // ---------------- The floors: the caps, by the bombers' pattern ----------------

        [Test]
        public void TheCaps_NoneBeforeFloor4_OneAComBatRoomOnFloor4_TwoOnFloor5_NeverTheFirstTheExitOrTheBoss()
        {
            var v = new LevelValues();
            for (int f = 1; f <= 6; f++)
            {
                int want = f < 4 ? 0 : f == 4 ? 1 : 2;
                Assert.That(LevelLancers.Most(v, RoomKind.Combat, f, false), Is.EqualTo(want), "a combat room, floor " + f);
                Assert.That(LevelLancers.Most(v, RoomKind.Combat, f, true), Is.Zero, "never the floor's first combat room");
                Assert.That(LevelLancers.Most(v, RoomKind.Exit, f, false), Is.Zero, "never the exit room (the boss's on floor 5)");
                Assert.That(LevelLancers.Most(v, RoomKind.Combat, f, false, bossRoom: true), Is.Zero, "never beside the boss");
                foreach (RoomKind k in new[] { RoomKind.Entrance, RoomKind.Treasure, RoomKind.Passage })
                    Assert.That(LevelLancers.Most(v, k, f, false), Is.Zero, k.ToString());
            }
            Assert.That(LevelLancers.Most(new LevelValues { Lancers = false }, RoomKind.Combat, 5, false), Is.Zero, "the switch off");
            Assert.That(LevelLancers.Most(new LevelValues { ActPlan = false }, RoomKind.Combat, 5, false), Is.Zero, "the act's plan off, as the bombers");

            var groups = new List<EnemyGroup> { new EnemyGroup(EnemyKind.Brute, 2), new EnemyGroup(EnemyKind.Swarmer, 3), new EnemyGroup(EnemyKind.Swarmer, 4) };
            Assert.That(LevelLancers.Convert(groups, 1), Is.EqualTo(1));
            Assert.That(groups.Select(g => g.ToString()), Is.EqualTo(new[] { "brute x2", "swarmer x3", "swarmer x3", "lancer" }), "in place of a swarmer of the largest pack");
            Assert.That(LevelLancers.Convert(new List<EnemyGroup> { new EnemyGroup(EnemyKind.Brute, 3) }, 2), Is.Zero, "no swarmer, no lancer");
        }

        /// <summary>
        /// A floor's fights as the generator draws them (its combat rooms then its exit room, each by <see cref="LevelGenerator.DrawGroups"/>
        /// of the same stream in the row's order), with its bombers and warders put in, then the lancers: over a thousand seeds a
        /// floor, every floor's caps keep, a lancer each takes a swarmer's place and no other count moves, and they come.
        /// </summary>
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void AThousandSeeds_TheFloorsFights_KeepTheCaps_EachInPlaceOfASwarmer(int floor)
        {
            var v = new LevelValues();
            int rooms = 0;
            int with = 0, most = 0;
            for (ulong s = 0; s < 1000; s++)
            {
                var rng = LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0);
                var fights = new List<List<EnemyGroup>>();
                for (int i = 0; i < 5; i++) fights.Add(LevelGenerator.DrawGroups(RoomKind.Combat, floor, rng, v));
                fights.Add(LevelGenerator.DrawGroups(RoomKind.Exit, floor, rng, v));
                LevelBombers.Put(fights, s, floor, v);
                LevelWarders.Put(fights, s, floor, v);
                List<string> before = fights.Select(f => string.Join(",", f.Select(g => g.ToString()))).ToList();
                List<Dictionary<EnemyKind, int>> kinds = fights.Select(Kinds).ToList();
                LevelLancers.Put(fights, s, floor, v);
                for (int i = 0; i < fights.Count; i++)
                {
                    rooms++;
                    Dictionary<EnemyKind, int> now = Kinds(fights[i]);
                    int lancers = LevelLancers.Count(fights[i]);
                    bool exit = i == fights.Count - 1;
                    int cap = exit || i == 0 ? 0 : LevelLancers.Most(v, RoomKind.Combat, floor, false);
                    Assert.That(lancers, Is.LessThanOrEqualTo(cap), "seed " + s + " room " + i);
                    Assert.That(fights[i].Where(g => g.Kind == EnemyKind.Lancer).All(g => g.Count == 1 && !g.Elite && !g.Boss), Is.True, "each alone, never an elite");
                    foreach (EnemyKind k in Enum.GetValues(typeof(EnemyKind)))
                    {
                        int was = kinds[i].TryGetValue(k, out int w) ? w : 0, has = now.TryGetValue(k, out int h) ? h : 0;
                        if (k == EnemyKind.Swarmer) Assert.That(has + lancers, Is.EqualTo(was), "in place of a swarmer, seed " + s);
                        else if (k != EnemyKind.Lancer) Assert.That(has, Is.EqualTo(was), k + " as before, seed " + s);
                    }
                    if (lancers == 0) Assert.That(string.Join(",", fights[i].Select(g => g.ToString())), Is.EqualTo(before[i]), "a room with none is as it was");
                    if (lancers > 0) with++;
                    most = Math.Max(most, lancers);
                }
            }
            TestContext.WriteLine("LANCERFLOOR floor " + floor + ": " + with + " of " + rooms + " fights with a lancer, at most " + most + " in one");
            if (floor < 4) Assert.That(with, Is.Zero, "none before floor 4");
            else
            {
                Assert.That(with, Is.GreaterThan(0), "they come");
                Assert.That(most, Is.EqualTo(floor == 4 ? 1 : 2), "the floor's most is reached");
            }
        }

        static Dictionary<EnemyKind, int> Kinds(List<EnemyGroup> groups) =>
            groups.GroupBy(g => g.Kind).ToDictionary(x => x.Key, x => x.Sum(g => g.Count));

        [Test]
        public void TheSwitchOff_PutsNone_AndAThousandFloors_AreTheFloorsOfBefore_FieldForField_WithNoLancer()
        {
            var fights = new List<List<EnemyGroup>> { new List<EnemyGroup>(), new List<EnemyGroup> { new EnemyGroup(EnemyKind.Swarmer, 5) }, new List<EnemyGroup>() };
            for (ulong s = 0; s < 50; s++) LevelLancers.Put(fights, s, 5, new LevelValues { Lancers = false });
            Assert.That(fights[1].Single().ToString(), Is.EqualTo("swarmer x5"), "the switch off: none");

            var off = new LevelValues { Lancers = false };
            var on = new LevelValues();
            for (ulong s = 0; s < 1000; s++)
            {
                int floor = (int)(s % 5) + 1;
                LevelLayout a = LevelGenerator.Generate(s * 7919UL + 108UL, floor, off), b = LevelGenerator.Generate(s * 7919UL + 108UL, floor, on);
                Assert.That(a.Rooms.SelectMany(r => r.EnemyGroups).Any(g => g.Kind == EnemyKind.Lancer), Is.False, "off: no lancer, seed " + s);
                Assert.That(b.Describe(), Is.EqualTo(a.Describe()), "on and off the same floor, field for field (the generator does not place them in this order), seed " + s);
                Assert.That(a.Version, Is.EqualTo(19), "the generator's version as before");
            }
        }

        // ---------------- The room lab's preset ----------------

        [Test]
        public void TheLabsPreset_StandsOneLancerInACombatRoom_InPlaceOfASwarmer_AndOffIsTheFloor4Room()
        {
            var v = new LevelValues();
            LabMix mix = RoomLab.Mixes.Last();
            Assert.That(mix.Name, Is.EqualTo("floor 4 with a lancer"));
            Assert.That(mix.Floor, Is.EqualTo(4));
            Assert.That(mix.Lancers, Is.EqualTo(1));
            Assert.That(RoomLab.Mixes.Take(5).All(m => m.Lancers == 0), Is.True, "the floors' own presets hold none");
            LabMix floor4 = RoomLab.Mixes[3];
            int withOne = 0;
            for (ulong s = 1; s <= 60; s++)
                foreach (LabKind kind in new[] { LabKind.Ordinary, LabKind.Elite, LabKind.Exit })
                {
                    List<EnemyGroup> lab = RoomLab.Groups(kind, mix, LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0), v);
                    List<EnemyGroup> plain = RoomLab.Groups(kind, floor4, LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0), v);
                    int lancers = LevelLancers.Count(lab);
                    bool swarmer = plain.Any(g => g.Kind == EnemyKind.Swarmer && !g.Elite);
                    Assert.That(lancers, Is.EqualTo(kind != LabKind.Exit && swarmer ? 1 : 0), kind + " seed " + s);
                    Assert.That(lab.Sum(g => g.Count), Is.EqualTo(plain.Sum(g => g.Count)), "the room's count the same");
                    Assert.That(lab.Where(g => g.Kind == EnemyKind.Swarmer).Sum(g => g.Count) + lancers, Is.EqualTo(plain.Where(g => g.Kind == EnemyKind.Swarmer).Sum(g => g.Count)));
                    List<EnemyGroup> off = RoomLab.Groups(kind, mix, LevelRandom.Stream(s, LevelGenerator.ContentsStage, 0), new LevelValues { Lancers = false });
                    Assert.That(off.Select(g => g.ToString()), Is.EqualTo(plain.Select(g => g.ToString())), "off: the floor 4 room, field for field");
                    if (lancers > 0) withOne++;
                }
            Assert.That(withOne, Is.GreaterThan(0));

            // Built, a room of the preset keeps the lab's rules, its lancer among them.
            int built = 0;
            foreach (LabEntry entry in RoomLab.Entries(v).Where(e => e.Kind == LabKind.Ordinary).Take(4))
                foreach (ulong seed in new ulong[] { 3, 108 })
                {
                    LevelLayout lab = RoomLab.Build(entry, mix, seed, v);
                    Assert.That(RoomLab.Faults(lab, mix, v), Is.Empty, entry + " seed " + seed);
                    built++;
                }
            Assert.That(built, Is.EqualTo(8));
            List<string> faults = RoomLab.Faults(RoomLab.Build(RoomLab.Entries(v).First(e => e.Kind == LabKind.Ordinary), floor4, 3, v), floor4, v);
            Assert.That(faults, Is.Empty);
        }

        // ---------------- The summary, the log's words, the sounds ----------------

        static SessionEvent Ev(double t, string type, string name = null, string result = null, string by = null)
        {
            var e = new SessionEvent(t, type, name) { Enemy = "lancer", EnemyId = 3, Result = result };
            if (by != null) e.AddExtra(SessionSummary.LancerCrashExtra, by);
            return e;
        }

        [Test]
        public void TheSummary_CountsItsCharges_Landed_Avoided_StoppedByCover_Countered_AndCrashes()
        {
            var events = new List<SessionEvent>
            {
                new SessionEvent(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.EnemyWake),
                Ev(2, SessionEventTypes.EnemyAttack, LancerValues.Charge),
                Ev(3, SessionEventTypes.EnemyStrike, LancerValues.Charge, SessionWords.Landed),
                Ev(5, SessionEventTypes.EnemyAttack, LancerValues.Charge),
                Ev(6, SessionEventTypes.EnemyStrike, LancerValues.Charge, SessionWords.OutOfArea),
                Ev(8, SessionEventTypes.EnemyAttack, LancerValues.Charge),
                Ev(9, SessionEventTypes.EnemyStrike, LancerValues.Charge, SessionWords.StoppedByWall, LancerRules.StopperWord(LancerStopper.LowProp)),
                Ev(11, SessionEventTypes.EnemyAttack, LancerValues.Charge),
                Ev(12, SessionEventTypes.EnemyStrike, LancerValues.Charge, SessionWords.OutOfArea, LancerRules.StopperWord(LancerStopper.Wall)),
                Ev(14, SessionEventTypes.EnemyAttack, LancerValues.Charge),
                Ev(14.5, SessionEventTypes.EnemyCountered, LancerValues.Charge),
                Ev(20, SessionEventTypes.EnemyDeath),
                new SessionEvent(21, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            LancerStats l = stats.Lancers;
            Assert.That(l.Charges, Is.EqualTo(5));
            Assert.That(l.Landed, Is.EqualTo(1));
            Assert.That(l.Avoided, Is.EqualTo(2), "it ran past her, and it crashed with her stepped aside");
            Assert.That(l.StoppedByCover, Is.EqualTo(1));
            Assert.That(l.Countered, Is.EqualTo(1));
            Assert.That(l.Crashes, Is.EqualTo(2));
            Assert.That(l.CrashedOn, Is.EqualTo(new[] { new KeyValuePair<string, int>("low prop", 1), new KeyValuePair<string, int>("wall", 1) }));
            EnemyKindRow row = stats.EnemyKinds.Single(r => r.Kind == "lancer");
            AttackStrikes charge = row.StrikesByAttack.Single(a => a.Name == LancerValues.Charge);
            Assert.That(charge.Landed, Is.EqualTo(1), "the enemies by kind: its Charge landed");
            Assert.That(charge.Avoided, Is.EqualTo(2), "and avoided");
            Assert.That(row.Kills, Is.EqualTo(1));

            string text = SessionSummary.Format(stats);
            TestContext.WriteLine(text);
            StringAssert.Contains("Lancers, whole session", text);
            StringAssert.Contains("5 / 1", text);
            StringAssert.Contains("1 / 2 / 1", text);
            StringAssert.Contains("2 (low prop 1, wall 1)", text);
            StringAssert.DoesNotContain("Lancers, whole session", SessionSummary.Format(SessionSummary.Compute(events.Where(e => e.Name != LancerValues.Charge).ToList())), "none without a charge");
            Assert.That(new[] { LancerStopper.Wall, LancerStopper.Door, LancerStopper.TallProp, LancerStopper.LowProp, LancerStopper.Edge }.Select(LancerRules.StopperWord),
                        Is.EqualTo(new[] { "wall", "door", "tall prop", "low prop", "edge" }));
        }

        [Test]
        public void ItsSounds_AreItsOwnThree_AtTheEndOfTheSounds()
        {
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Lancer, counterable: true), Is.EqualTo(PlaceholderSound.LancerWindup));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.WindupStarted, EnemyKind.Brute, counterable: true), Is.EqualTo(PlaceholderSound.WindupBlue), "the others' as before");
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.LancerCharged), Is.EqualTo(PlaceholderSound.LancerCharge));
            Assert.That(PlaceholderSoundRules.SoundFor(SoundMoment.LancerCrashed), Is.EqualTo(PlaceholderSound.LancerCrash));
            PlaceholderSound[] all = PlaceholderSoundRules.All;
            Assert.That(all.Skip(all.Length - 3), Is.EqualTo(new[] { PlaceholderSound.LancerWindup, PlaceholderSound.LancerCharge, PlaceholderSound.LancerCrash }), "at the end");
            Assert.That((int)PlaceholderSound.OffTick, Is.EqualTo(all.Length - 4), "every sound before them where it was");
            var values = new PlaceholderSoundValues();
            foreach (PlaceholderSound s in all.Skip(all.Length - 3))
            {
                Assert.That(values.Volume(s), Is.InRange(0.2f, 0.5f), s.ToString());
                Assert.That(values.Sounds.Count(x => x.Sound == s), Is.EqualTo(1), s + ": its line, once");
            }
        }
    }
}
