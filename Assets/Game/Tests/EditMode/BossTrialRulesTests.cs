using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Tests
{
    /// <summary>
    /// Work order 147 (Kristian, 2026-11-27: "bossen skal være veldig vanskelig. masse mechanics og ting å dodge"): the trial fight's
    /// rules with no engine: its switches, its phases and their wind-ups, the chooser (the teaching order, eligibility, the dash rule,
    /// the overlap rule, phase 2's opening), the shapes and their timing, the ward, the crash, the hurt guard, and its block of the
    /// session summary.
    /// </summary>
    public class BossTrialRulesTests
    {
        static readonly BossTrialValues V = new BossTrialValues();

        [TearDown]
        public void PutBack() => BossTrialValues.AllOn();

        static BossTrialSituation At(float now, int phase = 1, float distance = 5f, bool chargeFits = true, bool slamFits = true, bool ground = false,
                                     float lastEnded = -100f, float lastBig = -100f, float lastDashOnly = -100f) =>
            new BossTrialSituation
            {
                Now = now, Phase = phase, WokeAt = 0f, LastEnded = lastEnded, Distance = distance, OnScreen = true, ChargeFits = chargeFits,
                SlamFits = slamFits, GroundLive = ground, LastBigEnded = lastBig, LastDashOnly = lastDashOnly,
            };

        // ---------------- The switches and the frame ----------------

        [Test]
        public void TheSwitches_AreOn_AndTheFramesNumbers_AreTheOrders()
        {
            Assert.That(BossValues.Trial, Is.True, "on, with his words");
            foreach (BossTrialAttack a in new[] { BossTrialAttack.Cleave, BossTrialAttack.Charge, BossTrialAttack.GraveMarks, BossTrialAttack.Spokes,
                                                  BossTrialAttack.Slam, BossTrialAttack.Seeds, BossTrialAttack.Judgement })
                Assert.That(BossTrialValues.IsOn(a), Is.True, a + "'s own switch");
            Assert.That(BossTrialValues.GraveMarksLead, Is.True, "the research's first version: led");
            Assert.That(BossTrialValues.SeedsCanBeShot, Is.True, "the research's first version: shootable");
            Assert.That(V.Health, Is.EqualTo(52000), "7.1's 45,000, raised by the bots (the second round)");
            Assert.That(V.PhaseTwoBelow, Is.EqualTo(0.65f));
            Assert.That(V.PhaseThreeBelow, Is.EqualTo(0.30f));
            Assert.That(V.TransitionSeconds, Is.EqualTo(2.5f));
            Assert.That(new[] { V.DamageLight, V.DamageStandard, V.DamageHeavy, V.DamageFailedCheck }, Is.EqualTo(new[] { 15, 25, 35, 50 }));
            Assert.That(V.FirstAttackAfter, Is.EqualTo(2f));
            Assert.That(V.Gap.Skip(1), Is.EqualTo(new[] { 0.9f, 0.7f, 0.45f }));
            Assert.That(V.DashOnlyEvery, Is.EqualTo(10f));
            Assert.That(V.HurtGuardSeconds, Is.EqualTo(0.8f));
            Assert.That(V.JudgementAt, Is.EqualTo(new[] { 0.30f, 0.15f }));
            Assert.That(V.Ward, Is.EqualTo(4000));
            Assert.That(V.WardSeconds, Is.EqualTo(8f));
        }

        [Test]
        public void TheOldBoss_IsUntouched_FieldForField()
        {
            // Work order 146's boss: the values this order leaves as they were.
            var b = new BossValues();
            Assert.That(b.Enemy.Health, Is.EqualTo(18000));
            Assert.That(b.Enemy.Attacks.Select(a => a.Name), Is.EqualTo(new[] { BossValues.Cleave, BossValues.Spin, BossValues.Charge }));
            Assert.That(b.Enemy.Attacks.Select(a => a.Windup), Is.EqualTo(new[] { 1.1f, 1.0f, 0.9f }));
            Assert.That(b.Enemy.Attacks.Select(a => a.Damage), Is.EqualTo(new[] { 40, 35, 45 }));
            Assert.That(b.Enemy.Attacks.Select(a => a.MaxDistance), Is.EqualTo(new[] { 5f, 4f, 16f }));
            Assert.That(b.AngerBelow, Is.EqualTo(0.5f));
            Assert.That(b.AngerWindup, Is.EqualTo(0.8f));
            Assert.That(b.PracticeHealthShare, Is.EqualTo(1f / 3f));
            Assert.That(b.Phases.Slam.Windup, Is.EqualTo(1.0f));
            Assert.That(b.Phases.Slam.Damage, Is.EqualTo(40));
            Assert.That(b.WaveAttack.Damage, Is.EqualTo(20));
            Assert.That(BossValues.NeverParked && BossValues.NoPacks && BossValues.Finale, Is.True);
        }

        [Test]
        public void ThePhases_ByItsHealth_AndTheWindups_ByPhase_NeverUnderTheirFloors()
        {
            Assert.That(BossTrialRules.PhaseOf(1f, V), Is.EqualTo(1));
            Assert.That(BossTrialRules.PhaseOf(0.6501f, V), Is.EqualTo(1));
            Assert.That(BossTrialRules.PhaseOf(0.6499f, V), Is.EqualTo(2));
            Assert.That(BossTrialRules.PhaseOf(0.30f, V), Is.EqualTo(2));
            Assert.That(BossTrialRules.PhaseOf(0.2999f, V), Is.EqualTo(3));
            Assert.That(BossTrialRules.PhaseOf(0f, V), Is.EqualTo(3));
            Assert.That(BossTrialRules.Windup(1.1f, 0.9f, 1, V), Is.EqualTo(1.1f));
            Assert.That(BossTrialRules.Windup(1.1f, 0.9f, 2, V), Is.EqualTo(0.99f).Within(1e-5f));
            Assert.That(BossTrialRules.Windup(1.1f, 0.9f, 3, V), Is.EqualTo(0.935f).Within(1e-5f));
            Assert.That(BossTrialRules.Windup(1.0f, 0.9f, 3, V), Is.EqualTo(0.9f), "its floor");
            Assert.That(BossTrialRules.Windup(1.3f, 1.1f, 3, V), Is.EqualTo(1.105f).Within(1e-5f), "the slam's");
            Assert.That(BossTrialRules.Gap(1, V), Is.EqualTo(0.9f));
            Assert.That(BossTrialRules.Gap(3, V), Is.EqualTo(0.45f));
        }

        [Test]
        public void EveryWalkedAnswer_FitsItsTimeInEveryPhase_WithAQuarterSecondToReact()
        {
            // The order: "every hit he takes was told to him in time". The walk out of each shape at 6 m/s from its worst place, with
            // 0.25 s to react, against its wind-up in phase 3 (the shortest).
            const float Run = 6f, React = 0.25f, Her = Placeholders.PlayerRadius;
            float Need(float metres) => React + metres / Run;
            Assert.That(BossTrialRules.Windup(V.MarkBurst, V.MarkBurstFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.MarkRadius + Her)), "out of a Grave Mark from its middle");
            Assert.That(BossTrialRules.Windup(V.ChargeWindup, V.ChargeWindupFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.ChargeHalfWidth + Her)), "a sidestep out of the Charge's strip");
            Assert.That(BossTrialRules.Windup(V.SpokeFirstFill, V.SpokeFillFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.SpokeHalfWidth + Her)), "into a gap of set 1");
            Assert.That(BossTrialRules.Windup(V.SpokeSecondFill, V.SpokeFillFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.SpokeHalfWidth + Her)), "back out of set 2");
            Assert.That(BossTrialRules.Windup(V.SlamWindup, V.SlamWindupFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.SlamRadius + Her)), "out of the landing circle from where she stood");
            Assert.That(BossTrialRules.Windup(V.SeedFill, V.SeedFillFloor, 3, V), Is.GreaterThanOrEqualTo(Need(V.SeedCircle + Her)), "out of a seed's circle");
            // Section 7.2's own 1.0 s slam leaves no margin: this is why it was lengthened.
            Assert.That(1.0f, Is.LessThanOrEqualTo(Need(V.SlamRadius + Her) + 1e-4f), "7.2's 1.0 s is exactly the walk out, no margin");
        }

        // ---------------- The chooser ----------------

        [Test]
        public void NothingBegins_BeforeTwoSecondsAfterTheWaking_WithinTheGap_OrOffHisScreen()
        {
            var c = new BossTrialChooser(1);
            Assert.That(c.Choose(At(1.9f, distance: 10f), V), Is.EqualTo(BossTrialAttack.None), "1.9 s after the waking");
            BossTrialSituation s = At(2.5f, distance: 10f, lastEnded: 2.0f);
            Assert.That(c.Choose(s, V), Is.EqualTo(BossTrialAttack.None), "0.5 s after a recovery, under phase 1's 0.9 s gap");
            s = At(3f, distance: 10f);
            s.OnScreen = false;
            Assert.That(c.Choose(s, V), Is.EqualTo(BossTrialAttack.None), "its body off his screen");
            Assert.That(c.Choose(At(3f, distance: 10f), V), Is.EqualTo(BossTrialAttack.Charge), "then the first of the teaching order");
        }

        [Test]
        public void PhaseOne_OpensWithTheTeachingOrder_ThenDraws()
        {
            var c = new BossTrialChooser(7);
            var got = new List<BossTrialAttack>();
            float t = 3f;
            // She far for the Charges, near for the Cleaves; each asked where it fits.
            foreach (BossTrialAttack want in V.Teaching)
            {
                float d = want == BossTrialAttack.Charge ? 10f : 4f;
                got.Add(c.Choose(At(t, distance: d), V));
                t += 7f;
            }
            Assert.That(got, Is.EqualTo(V.Teaching), "Charge, Cleave, Grave Marks, Cleave, Charge");
            Assert.That(c.Taught, Is.EqualTo(V.Teaching.Length));
            BossTrialAttack next = c.Choose(At(t, distance: 4f), V);
            Assert.That(next, Is.Not.EqualTo(BossTrialAttack.None), "then the draws");
            Assert.That(BossTrialRules.FirstPhase(next), Is.EqualTo(1), "phase 1's attacks only");
        }

        [Test]
        public void ATaughtAttackThatWillNotFit_IsPassedOver_AfterItsWait()
        {
            var c = new BossTrialChooser(3);
            Assert.That(c.Choose(At(3f, distance: 3f, chargeFits: true), V), Is.EqualTo(BossTrialAttack.None), "she at its feet: the Charge does not fit, it walks");
            Assert.That(c.Choose(At(3f + V.TeachWaitMost - 0.1f, distance: 3f), V), Is.EqualTo(BossTrialAttack.None));
            Assert.That(c.Choose(At(3f + V.TeachWaitMost + 0.01f, distance: 3f), V), Is.EqualTo(BossTrialAttack.Cleave), "passed over: the next taught, the Cleave");
        }

        [Test]
        public void TheDraws_KeepEveryRule_OverAThousandChoices_AndAreTheSameForTheSameSeed()
        {
            foreach (int seed in new[] { 1, 2, 3 })
            {
                var a = new BossTrialChooser(seed);
                var b = new BossTrialChooser(seed);
                a.EnterPhase(2);
                b.EnterPhase(2);
                var rng = new Random(seed);
                float t = 3f;
                BossTrialAttack last = BossTrialAttack.None;
                int run = 0;
                var since = new Dictionary<BossTrialAttack, int>();
                float lastSlam = -100f, lastCharge = -100f, lastMarks = -100f;
                var seen = new HashSet<BossTrialAttack>();
                for (int i = 0; i < 1000; i++)
                {
                    BossTrialSituation s = At(t, phase: 2, distance: (float)(rng.NextDouble() * 16.0), lastBig: t - 4f, lastDashOnly: lastSlam);
                    BossTrialAttack x = a.Choose(s, V), y = b.Choose(s, V);
                    Assert.That(y, Is.EqualTo(x), "the same seed, the same draws");
                    t += 3f;
                    if (x == BossTrialAttack.None) continue;
                    seen.Add(x);
                    if (x == BossTrialAttack.Cleave) Assert.That(s.Distance, Is.LessThanOrEqualTo(V.CleaveWithin), "the Cleave's band");
                    if (x == BossTrialAttack.Charge)
                    {
                        Assert.That(s.Distance, Is.InRange(V.ChargeFrom, V.ChargeTo), "the Charge's band");
                        Assert.That(s.Now - lastCharge, Is.GreaterThanOrEqualTo(V.ChargeEvery - 1e-3f));
                        lastCharge = s.Now;
                    }
                    if (x == BossTrialAttack.GraveMarks)
                    {
                        Assert.That(s.Now - lastMarks, Is.GreaterThanOrEqualTo(V.MarksEvery - 1e-3f));
                        lastMarks = s.Now;
                    }
                    if (x == BossTrialAttack.Slam)
                    {
                        Assert.That(s.Now - lastSlam, Is.GreaterThanOrEqualTo(V.DashOnlyEvery - 1e-3f), "at most one dash-only attack in 10 s");
                        lastSlam = s.Now;
                    }
                    if (x == BossTrialAttack.Spokes || x == BossTrialAttack.Seeds || x == BossTrialAttack.Slam)
                        if (since.TryGetValue(x, out int n)) Assert.That(n, Is.GreaterThanOrEqualTo(V.OthersBetween), x + ": three others between");
                    foreach (BossTrialAttack k in since.Keys.ToList()) since[k]++;
                    since[x] = 0;
                    run = x == last ? run + 1 : 1;
                    last = x;
                    Assert.That(run, Is.LessThanOrEqualTo(x == BossTrialAttack.Cleave ? V.CleaveInARowMost : V.InARowMost), x + " in a row");
                }
                Assert.That(seen, Is.SupersetOf(new[] { BossTrialAttack.Cleave, BossTrialAttack.Charge, BossTrialAttack.GraveMarks, BossTrialAttack.Spokes, BossTrialAttack.Slam, BossTrialAttack.Seeds }),
                            "every attack of phase 2 comes");
            }
        }

        [Test]
        public void TheOverlapRule_WithAPatternOnTheGround_OnlyABodyAttackMayBegin()
        {
            var c = new BossTrialChooser(5);
            c.EnterPhase(3);
            for (int i = 0; i < 200; i++)
            {
                BossTrialAttack a = c.Choose(At(3f + i * 3f, phase: 3, distance: i % 2 == 0 ? 4f : 10f, ground: true), V);
                Assert.That(a == BossTrialAttack.None || BossTrialRules.IsBody(a), Is.True, a + " began over a pattern on the ground");
            }
        }

        [Test]
        public void TheDashRule_NoSlamWithinTenSecondsOfTheLast_NorWithinThreeOfABigPattern()
        {
            var c = new BossTrialChooser(9);
            c.EnterPhase(3);
            Assert.That(c.Fits(BossTrialAttack.Slam, At(20f, phase: 3, lastDashOnly: 11f), V, false), Is.False, "9 s after the last");
            Assert.That(c.Fits(BossTrialAttack.Slam, At(20f, phase: 3, lastDashOnly: 10f), V, false), Is.True, "10 s after");
            Assert.That(c.Fits(BossTrialAttack.Slam, At(20f, phase: 3, lastBig: 18f), V, false), Is.False, "2 s after Spokes ended");
            Assert.That(c.Fits(BossTrialAttack.Slam, At(20f, phase: 3, slamFits: false), V, false), Is.False, "out of its reach");
        }

        [Test]
        public void PhaseTwo_OpensWithSpokesAlone_ThenSeeds_ThenTheSlam_ThreeSecondsAfterEachHasEnded()
        {
            var c = new BossTrialChooser(11);
            c.EnterPhase(2);
            Assert.That(c.Choose(At(10f, phase: 2, distance: 8f), V), Is.EqualTo(BossTrialAttack.Spokes), "its first new attack at once");
            // Spokes ended at 12.4 s: until 15.4 s only phase 1's attacks.
            for (float t = 13.5f; t < 15.3f; t += 0.9f)
            {
                BossTrialAttack a = c.Choose(At(t, phase: 2, distance: 4f, lastBig: 12.4f), V);
                Assert.That(BossTrialRules.FirstPhase(a), Is.EqualTo(1), "nothing new for 3 s: " + a);
            }
            Assert.That(c.Choose(At(16f, phase: 2, distance: 8f, lastBig: 12.4f), V), Is.EqualTo(BossTrialAttack.Seeds), "then Seeds, alone");
            Assert.That(c.Choose(At(18f, phase: 2, distance: 8f, ground: true, lastBig: 12.4f), V), Is.Not.EqualTo(BossTrialAttack.Slam), "not over the seeds");
            Assert.That(c.Choose(At(24f, phase: 2, distance: 8f, lastBig: 21f), V), Is.EqualTo(BossTrialAttack.Slam), "then the Slam");
            Assert.That(c.Opened, Is.EqualTo(3));
        }

        [Test]
        public void EachAttacksSwitch_TakesItOut_AndNoOther()
        {
            BossTrialValues.SpokesOn = false;
            var c = new BossTrialChooser(2);
            c.EnterPhase(2);
            var seen = new HashSet<BossTrialAttack>();
            for (int i = 0; i < 400; i++)
            {
                float t = 3f + i * 5f;
                BossTrialAttack a = c.Choose(At(t, phase: 2, distance: i % 3 == 0 ? 3f : 9f, lastBig: t - 4f), V);
                if (a != BossTrialAttack.None) seen.Add(a);
            }
            Assert.That(seen, Has.No.Member(BossTrialAttack.Spokes));
            Assert.That(seen, Is.SupersetOf(new[] { BossTrialAttack.Cleave, BossTrialAttack.Charge, BossTrialAttack.Seeds, BossTrialAttack.Slam, BossTrialAttack.GraveMarks }));
        }

        // ---------------- The shapes ----------------

        [Test]
        public void GraveMarks_LeadHerMotion_AtMostThreeMetres_NoneWhenSheStands_NoneWithTheSwitchOff()
        {
            var her = new GroundPoint(0f, 0f);
            Assert.That(BossTrialRules.MarkPlace(her, new GroundPoint(6f, 0f), true, V).X, Is.EqualTo(3f).Within(1e-4f), "6 m/s x 0.6 s = 3.6, at most 3 m");
            Assert.That(BossTrialRules.MarkPlace(her, new GroundPoint(2f, 0f), true, V).X, Is.EqualTo(1.2f).Within(1e-4f));
            Assert.That(BossTrialRules.MarkPlace(her, new GroundPoint(0.1f, 0f), true, V).X, Is.EqualTo(0f), "standing");
            Assert.That(BossTrialRules.MarkPlace(her, new GroundPoint(6f, 0f), false, V).X, Is.EqualTo(0f), "off: at her");
            Assert.That(BossTrialRules.MarkPlacedAt(3, V), Is.EqualTo(1.35f).Within(1e-5f), "four, 0.45 s apart");
            Assert.That(BossTrialRules.MarksWhole(1f, V), Is.EqualTo(2.35f).Within(1e-5f));
        }

        [Test]
        public void Spokes_SixSixtyDegreesApart_TheSecondSetHalfAGapTurned_StandingStillIsAlwaysHitOnce()
        {
            var aim = new GroundPoint(1f, 0f);
            GroundPoint a0 = BossTrialRules.SpokeDirection(aim, 0, 0, V), a1 = BossTrialRules.SpokeDirection(aim, 0, 1, V), b0 = BossTrialRules.SpokeDirection(aim, 1, 0, V);
            Assert.That(Math.Acos(a0.Dot(a1)) * 180.0 / Math.PI, Is.EqualTo(60.0).Within(1e-3));
            Assert.That(Math.Acos(a0.Dot(b0)) * 180.0 / Math.PI, Is.EqualTo(30.0).Within(1e-3));
            var o = new GroundPoint(0f, 0f);
            // Every place from 2 to 7.5 m round it is in a strip of set 1 or of set 2 (beyond about 7.7 m the twelve strips, 30 degrees
            // apart and 3 m wide, leave narrow gaps between them: 7.2's "always hit once" holds within that).
            for (int deg = 0; deg < 360; deg += 3)
                for (float r = 2f; r <= 7.5f; r += 0.5f)
                {
                    var p = new GroundPoint((float)Math.Cos(deg * Math.PI / 180) * r, (float)Math.Sin(deg * Math.PI / 180) * r);
                    bool hit = false;
                    for (int set = 0; set < 2 && !hit; set++)
                        for (int i = 0; i < 6 && !hit; i++)
                            hit = BossTrialRules.SpokeHits(o, BossTrialRules.SpokeDirection(aim, set, i, V), 30f, V.SpokeHalfWidth, p, Placeholders.PlayerRadius);
                    Assert.That(hit, Is.True, "standing at " + deg + " degrees, " + r + " m: hit by one set");
                }
            // 8 m out a gap of set 1 is wide enough to stand in, as 7.2 says (about 5.4 m between the strips' edges).
            float gap = 2f * 8f * (float)Math.Sin(30.0 * Math.PI / 180.0) - 2f * V.SpokeHalfWidth;
            Assert.That(gap, Is.EqualTo(5.0f).Within(0.5f));
        }

        [Test]
        public void BandsThatMove_TouchHerOnce_Swept_AndThePillarsTableShelters()
        {
            Assert.That(BossTrialRules.BandTouches(4f, 4.1f, 0.3f, 6f, 0.5f), Is.False, "not yet");
            Assert.That(BossTrialRules.BandTouches(5.0f, 5.3f, 0.3f, 6f, 0.5f), Is.True, "its front at her edge");
            Assert.That(BossTrialRules.BandTouches(2f, 10f, 0.3f, 6f, 0.5f), Is.True, "a long step does not pass over her");
            Assert.That(BossTrialRules.RingRadius(0f, V), Is.EqualTo(4f));
            Assert.That(BossTrialRules.RingRadius(1f, V), Is.EqualTo(11f));
            Assert.That(BossTrialRules.RingSeconds(V), Is.EqualTo(12f / 7f).Within(1e-5f));
            var clear = new float[128];
            for (int i = 0; i < clear.Length; i++) clear[i] = 20f;
            clear[0] = 5f; // a pillar 5 m along +x
            Assert.That(BossTrialRules.Sheltered(clear, new GroundPoint(0f, 0f), new GroundPoint(8f, 0f)), Is.True, "behind it");
            Assert.That(BossTrialRules.Sheltered(clear, new GroundPoint(0f, 0f), new GroundPoint(4f, 0f)), Is.False, "before it");
            Assert.That(BossTrialRules.Sheltered(clear, new GroundPoint(0f, 0f), new GroundPoint(0f, 8f)), Is.False, "elsewhere");
            Assert.That(BossTrialRules.SampleOf(0f, -1f, 4), Is.EqualTo(3));
        }

        [Test]
        public void Seeds_ThreeAThirdOfATurnApart_AndAShotsLineTouchesOne()
        {
            var her = new GroundPoint(0f, 0f);
            GroundPoint a = BossTrialRules.SeedPlace(her, 0, 3, 0.0, 5f), b = BossTrialRules.SeedPlace(her, 1, 3, 0.0, 5f);
            Assert.That(a.DistanceTo(her), Is.EqualTo(5f).Within(1e-4f));
            Assert.That(a.DistanceTo(b), Is.EqualTo(5f * Math.Sqrt(3)).Within(1e-3));
            Assert.That(BossTrialRules.ShotTouchesSeed(her, new GroundPoint(10f, 0f), 20f, 0.1f, new GroundPoint(5f, 0.3f), 0.25f), Is.True);
            Assert.That(BossTrialRules.ShotTouchesSeed(her, new GroundPoint(10f, 0f), 20f, 0.1f, new GroundPoint(5f, 0.5f), 0.25f), Is.False, "beside it");
            Assert.That(BossTrialRules.ShotTouchesSeed(her, new GroundPoint(10f, 0f), 4f, 0.1f, new GroundPoint(5f, 0f), 0.25f), Is.False, "the shot ended before it");
        }

        [Test]
        public void Judgement_AtThirtyAndFifteen_EachOnce_TheCircleCloses_TwoMarksNotThree()
        {
            Assert.That(BossTrialRules.JudgementDue(0.31f, 0, V), Is.EqualTo(-1));
            Assert.That(BossTrialRules.JudgementDue(0.30f, 0, V), Is.EqualTo(0));
            Assert.That(BossTrialRules.JudgementDue(0.20f, 1, V), Is.EqualTo(-1));
            Assert.That(BossTrialRules.JudgementDue(0.10f, 1, V), Is.EqualTo(1));
            Assert.That(BossTrialRules.JudgementDue(0.05f, 2, V), Is.EqualTo(-1), "twice at the most");
            Assert.That(BossTrialRules.WardRadius(0f, V), Is.EqualTo(V.WardCircleFrom));
            Assert.That(BossTrialRules.WardRadius(V.WardSeconds, V), Is.EqualTo(V.WardCircleTo).Within(1e-4f));
            Assert.That(BossTrialRules.WardRadius(V.WardSeconds * 0.5f, V), Is.EqualTo((V.WardCircleFrom + V.WardCircleTo) * 0.5f).Within(1e-4f));
            Assert.That(BossTrialRules.WardMarks(V), Is.EqualTo(2), "at 2.5 and 5.0 s; none bursting as she runs for a shadow");
        }

        [Test]
        public void TheCrash_OnlyACutCharge_WithAWallWithinItsBodyInFront()
        {
            const float body = 1.2f;
            Assert.That(BossTrialRules.Crashed(14f, 14f, 0.5f, body, V), Is.False, "it ran its whole length");
            Assert.That(BossTrialRules.Crashed(6f, 14f, 1.3f, body, V), Is.True, "cut, the pier 1.3 m in front of its middle");
            Assert.That(BossTrialRules.Crashed(6f, 14f, 1.2f + V.CrashSlack + 0.1f, body, V), Is.False, "cut by the end of the walkable ground, nothing tall before it");
        }

        [Test]
        public void TheStaggerBarsDaze_ForSeconds_RaisesHerDamage_AndEndsFull()
        {
            var bar = new StaggerBar(new StaggerValues { Max = 150f });
            bar.DazeFor(2f, 10f);
            Assert.That(bar.IsDazed(11.9f), Is.True);
            Assert.That(bar.Scale(100, 11f), Is.EqualTo(150), "1.5 times");
            Assert.That(bar.IsDazed(12.01f), Is.False);
            Assert.That(bar.Tick(12.01f), Is.True);
            Assert.That(bar.Current, Is.EqualTo(150f));
            bar.DazeFor(4f, 20f);
            bar.DazeFor(1f, 21f);
            Assert.That(bar.DazedUntil, Is.EqualTo(24f), "a longer daze running is kept");
        }

        [Test]
        public void TheHurtGuard_HoldsForPointEightSeconds_OnlyInTheTrialFight_AndFlickers()
        {
            var g = new HurtGuardRule();
            g.Took(1f, V.HurtGuardSeconds);
            Assert.That(g.Holds(1.1f), Is.False, "not active: no guard outside the trial fight");
            g.Active = true;
            g.Took(1f, V.HurtGuardSeconds);
            Assert.That(g.Holds(1.79f), Is.True);
            Assert.That(g.Holds(1.81f), Is.False);
            Assert.That(BossTrialRules.FlickerHidden(1.01f, 1f, V), Is.False, "shown first");
            Assert.That(BossTrialRules.FlickerHidden(1.08f, 1f, V), Is.True, "then hidden");
            Assert.That(BossTrialRules.FlickerHidden(1.15f, 1f, V), Is.False);
            Assert.That(BossTrialRules.FlickerHidden(1.81f, 1f, V), Is.False, "shown after it");
            g.Clear();
            Assert.That(g.Holds(1.5f), Is.False);
        }

        // ---------------- The summary ----------------

        [Test]
        public void TheSummary_HasItsBlock_WithLengthsHitsByAttackStoodFloorAndDeathsByAttack()
        {
            var e = new SessionEvent(100.0, BossTrialLog.Fight);
            foreach (string[] kv in new[]
            {
                new[] { "length", "151.2" }, new[] { "one", "70.1" }, new[] { "two", "50.0" }, new[] { "three", "31.1" }, new[] { "ended", BossFightEvents.HerDeath },
                new[] { "killed_by", BossTrialValues.Ruin }, new[] { "stood", "0.41" }, new[] { "floor", "0.38" }, new[] { "template", "throne-nave" },
                new[] { "hits.Cleave", "2" }, new[] { "damage.Cleave", "50" }, new[] { "hits.Ruin", "1" }, new[] { "damage.Ruin", "50" }, new[] { "tells.Cleave", "9" },
            }) e.AddExtra(kv[0], kv[1]);
            List<BossTrialRow> rows = SessionSummary.ComputeBossTrial(new[] { e });
            Assert.That(rows, Has.Count.EqualTo(1));
            Assert.That(rows[0].Length, Is.EqualTo(151.2).Within(1e-6));
            Assert.That(rows[0].Hits.Single(h => h.Key == "Cleave").Value, Is.EqualTo(2));
            string text = SessionSummary.Format(SessionSummary.Compute(new List<SessionEvent> { new SessionEvent(0.0, "session_start"), e, new SessionEvent(160.0, "session_end") }));
            StringAssert.Contains("Boss (work order 147)", text);
            StringAssert.Contains("151.2 s", text);
            StringAssert.Contains("Deaths by attack", text);
            StringAssert.Contains("Ruin 1", text);
            StringAssert.Contains("41%", text);
        }
    }
}
