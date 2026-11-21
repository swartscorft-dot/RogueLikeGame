using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 064 (Kristian, 2026-11-21), the feel of firing, its rules: Backstep's clean part after the hop moved early
    /// and its shot quicker; a clean tap that pays more, a full clean chain that pays most, folded with the stats exactly as
    /// work orders 050 and 055 have it; the weight of a shot (its kick, light and hit stop); the kinetic damage numbers
    /// (merging, the cap, lanes, sizes, lives); the basic shot's variations (never the same twice running, seeded).
    /// </summary>
    public class FiringFeelRulesTests
    {
        static SkillDefinition Skill(string name) => SkillDefaults.Create().Single(s => s.Name == name);

        // ---------------- 1. Backstep ----------------

        [Test]
        public void Backstep_TheCleanPartAfterItsHop_Is003To023_TheSameWidth_TheOtherChainsKeep010To030()
        {
            var kit = new KitValues();
            SkillDefinition step = Skill("Backstep");
            CleanPart afterHop = CleanPart.Of(kit, step.RecastWindow, afterOpeningMove: true);
            CleanPart other = CleanPart.Of(kit, step.RecastWindow, afterOpeningMove: false);
            Assert.That(afterHop.From, Is.EqualTo(0.03f).Within(1e-6f));
            Assert.That(afterHop.To, Is.EqualTo(0.23f).Within(1e-6f));
            Assert.That(other.From, Is.EqualTo(0.10f).Within(1e-6f), "Triple Tap's, as before");
            Assert.That(other.To, Is.EqualTo(0.30f).Within(1e-6f));
            Assert.That(afterHop.To - afterHop.From, Is.EqualTo(other.To - other.From).Within(1e-6f), "the same 0.20 s width");
            Assert.That(CleanPart.Of(kit, step.RecastWindow), Is.EqualTo(other), "the two-argument form is the old one");
            // The morning's, by its values.
            kit.OpeningMoveCleanFrom = -1f;
            kit.OpeningMoveCleanTo = -1f;
            Assert.That(CleanPart.Of(kit, step.RecastWindow, true), Is.EqualTo(other), "below 0: the kit's own");
        }

        [Test]
        public void Backstep_HisPressesOf1212_WouldBeClean16OfTwenty_AgainstSix()
        {
            // His second presses of 2026-11-21 12:12 (PlaySessions/2026-11-21_12-12-02.jsonl), seconds after the hop ended,
            // and whether each had waited in the buffer from the hop (never clean).
            var presses = new (float After, bool Buffered)[]
            {
                (0.114f, false), (0.212f, false), (0.035f, false), (0.072f, false), (0.034f, false), (0.095f, false), (0.066f, false),
                (0.110f, false), (0.031f, false), (0.065f, false), (0.105f, false), (0.000f, true), (0.000f, true), (0.065f, false),
                (0.037f, false), (0.188f, false), (0.000f, true), (0.095f, false), (0.000f, true), (0.128f, false),
            };
            var kit = new KitValues();
            var before = new KitValues { OpeningMoveCleanFrom = -1f, OpeningMoveCleanTo = -1f };
            int Clean(KitValues k)
            {
                int n = 0;
                foreach (var p in presses)
                {
                    var chain = new RecastChain();
                    chain.Start(6, 2, 0.6f, openingMove: true);
                    chain.ShotWentOff(10f);
                    if (chain.Judge(6, 10f + p.After, CleanPart.Of(k, 0.6f, true), true, p.Buffered) == RecastPress.Clean) n++;
                }
                return n;
            }
            Assert.That(Clean(before), Is.EqualTo(6), "as the log has it: 6 of 20");
            Assert.That(Clean(kit), Is.EqualTo(16), "with the clean part at 0.03 to 0.23 s");
        }

        [Test]
        public void Backstep_APressThatWaitedFromTheHop_IsNeverClean()
        {
            var chain = new RecastChain();
            chain.Start(6, 2, 0.6f, openingMove: true);
            chain.ShotWentOff(5f);
            CleanPart part = CleanPart.Of(new KitValues(), 0.6f, true);
            Assert.That(chain.Judge(6, 5.1f, part, true, buffered: true), Is.EqualTo(RecastPress.Shot), "mashing F is no clean press");
            Assert.That(chain.Judge(6, 5.1f, part, true, buffered: false), Is.EqualTo(RecastPress.Clean));
            Assert.That(chain.Judge(6, 5.01f, part, true), Is.EqualTo(RecastPress.Shot), "before 0.03 s: early");
            Assert.That(chain.Judge(6, 5.25f, part, true), Is.EqualTo(RecastPress.Shot), "after 0.23 s: late");
        }

        [Test]
        public void Backstep_ItsNumbers_TheShotQuicker_TheBuffers_TheHopAsItWas()
        {
            var kit = new KitValues();
            SkillDefinition step = Skill("Backstep");
            Assert.That(kit.OpeningMoveShotStartup, Is.EqualTo(0.08f), "the shot 0.08 s after the second press, down from 0.2");
            Assert.That(step.Startup, Is.EqualTo(0.2f), "the hop itself unchanged");
            Assert.That(step.RecastWindow, Is.EqualTo(0.6f), "the window unchanged");
            Assert.That(kit.CooldownBuffer, Is.EqualTo(0.25f));
            Assert.That(kit.OpeningMoveCutsRecovery, Is.True);
            var cast = new SkillCast();
            cast.Start(step, 0f, kit.OpeningMoveShotStartup);
            Assert.That(cast.HitsDue(0.079f), Is.EqualTo(0));
            Assert.That(cast.HitsDue(0.08f), Is.EqualTo(1), "it goes off at 0.08 s");
            Assert.That(cast.RecoveryEndsAt, Is.EqualTo(0.08f + step.Recovery).Within(1e-6f));
            var hop = new SkillCast();
            hop.Start(step, 0f);
            Assert.That(hop.StartupSeconds, Is.EqualTo(0.2f), "the two-argument start is the skill's own");
        }

        [Test]
        public void ACleanSpend_StaysFolded_80_120_200_AtTheStart()
        {
            // Work order 055, confirmed by Kristian 2026-11-21 ("Rett til 80"): untouched by this order.
            SkillDefinition step = Skill("Backstep");
            var v = new StatValues();
            int[] want = { 80, 120, 200 };
            int[] marks = { 0, 1, 3 };
            for (int i = 0; i < marks.Length; i++)
                Assert.That(CritRules.ApplyFolded(MarkRules.SpenderDamage(step.Damage, marks[i], MarkRules.BonusPerMark(step, true)), v.StartCritDamage, v), Is.EqualTo(want[i]));
        }

        // ---------------- 2. Triple Tap's clean tap ----------------

        /// <summary>A clean tap's damage as PlayerCharacter deals it: the scale folded with the stats on, then the sure crit.</summary>
        static int CleanTap(int damage, float scale, bool statsOn, float critDamage, StatValues v) =>
            statsOn ? CritRules.Apply((int)Math.Round(damage * CritRules.Folded(scale, v)), critDamage) : (int)Math.Round(damage * scale);

        [Test]
        public void ACleanTap_AtARunsStart_Is123_Before105_AndWithACritDamageStep135_Before115()
        {
            var kit = new KitValues();
            var morning = new KitValues { CleanDamage = 1.5f };
            var v = new StatValues();
            int tap = Skill("Triple Tap").Damage;
            float step = v.StartCritDamage + 0.15f; // one common crit damage step (RunRules: +15 %)
            Assert.That(kit.CleanDamage, Is.EqualTo(1.75f));
            Assert.That(MarkRules.CleanTapScale(kit, false), Is.EqualTo(1.75f));
            Assert.That(CleanTap(tap, MarkRules.CleanTapScale(morning, false), true, v.StartCritDamage, v), Is.EqualTo(105), "before, at the start");
            Assert.That(CleanTap(tap, MarkRules.CleanTapScale(morning, false), true, step, v), Is.EqualTo(115), "before, a crit damage step (70 times 1.65 is 115.4999 in floats)");
            Assert.That(CleanTap(tap, MarkRules.CleanTapScale(kit, false), true, v.StartCritDamage, v), Is.EqualTo(123), "now, at the start");
            Assert.That(CleanTap(tap, MarkRules.CleanTapScale(kit, false), true, step, v), Is.EqualTo(135), "now, a crit damage step");
            Assert.That(CleanTap(tap, MarkRules.CleanTapScale(kit, false), false, 0f, v), Is.EqualTo(122), "the stats off: 70 times 1.75, rounded to even");
        }

        [Test]
        public void AFullCleanChain_ItsLastTap_Deals245AtTheStart_AndAddsAMarkMore()
        {
            var kit = new KitValues();
            var v = new StatValues();
            SkillDefinition tap = Skill("Triple Tap");
            Assert.That(MarkRules.CleanTapScale(kit, true), Is.EqualTo(3.5f).Within(1e-6f), "twice a clean tap's 1.75");
            Assert.That(CleanTap(tap.Damage, MarkRules.CleanTapScale(kit, true), true, v.StartCritDamage, v), Is.EqualTo(245), "at the start");
            Assert.That(CleanTap(tap.Damage, MarkRules.CleanTapScale(kit, true), true, v.StartCritDamage + 0.15f, v), Is.EqualTo(269), "a crit damage step");
            Assert.That(CleanTap(tap.Damage, MarkRules.CleanTapScale(kit, true), false, 0f, v), Is.EqualTo(245), "the stats off");
            Assert.That(MarkRules.CleanTapMarks(tap, kit, false), Is.EqualTo(2));
            Assert.That(MarkRules.CleanTapMarks(tap, kit, true), Is.EqualTo(3), "a mark more");
            var off = new KitValues { FullChainScale = 1f, FullChainMarks = 0 };
            Assert.That(MarkRules.CleanTapScale(off, true), Is.EqualTo(off.CleanDamage), "1 and 0: the chain as it was");
            Assert.That(MarkRules.CleanTapMarks(tap, off, true), Is.EqualTo(2));
        }

        [Test]
        public void AFullCleanChain_EveryPressAfterTheFirst_InItsCleanPart_TheLastClean()
        {
            var kit = new KitValues();
            SkillDefinition tap = Skill("Triple Tap");
            CleanPart part = CleanPart.Of(kit, tap.RecastWindow);
            bool Chain(float second, float third, bool secondBuffered = false)
            {
                var chain = new RecastChain();
                chain.Start(7, 3, tap.RecastWindow);
                chain.ShotWentOff(1f);
                chain.Recast(chain.InCleanPart(7, 1f + second, part, secondBuffered));
                chain.ShotWentOff(2f);
                bool clean = chain.Judge(7, 2f + third, part, true) == RecastPress.Clean;
                chain.Recast(clean);
                return MarkRules.IsFullCleanChain(tap, clean, chain.Shots, chain.AllInRhythm);
            }
            Assert.That(Chain(0.2f, 0.2f), Is.True, "both in their clean parts");
            Assert.That(Chain(0.02f, 0.2f), Is.False, "the second early");
            Assert.That(Chain(0.2f, 0.02f), Is.False, "the last early");
            Assert.That(Chain(0.2f, 0.2f, secondBuffered: true), Is.False, "a second press that waited");
            SkillDefinition step = Skill("Backstep");
            Assert.That(MarkRules.IsFullCleanChain(step, true, 2, true), Is.False, "Backstep's clean press is a spend, and two presses");
        }

        // ---------------- 3. The weight of a shot ----------------

        [Test]
        public void AShotsWeight_TheHeavyThree_AndTheFullCleanTap_TakeTheLongerHitStop()
        {
            var v = new FiringWeightValues();
            Assert.That(FiringWeightRules.Of(Skill("Heavy Volley"), false, false), Is.EqualTo(ShotWeight.Heavy));
            Assert.That(FiringWeightRules.Of(Skill("Scatter Blast"), false, false), Is.EqualTo(ShotWeight.Heavy));
            Assert.That(FiringWeightRules.Of(Skill("Long Shot"), true, false), Is.EqualTo(ShotWeight.Heavy), "a perfect release");
            Assert.That(FiringWeightRules.Of(Skill("Long Shot"), false, false), Is.EqualTo(ShotWeight.Skill));
            Assert.That(FiringWeightRules.Of(Skill("Triple Tap"), false, true), Is.EqualTo(ShotWeight.FullClean));
            Assert.That(FiringWeightRules.Of(null, false, false), Is.EqualTo(ShotWeight.Basic));
            Assert.That(FiringWeightRules.HitStop(ShotWeight.Heavy, 0.05f, v), Is.EqualTo(0.075f));
            Assert.That(FiringWeightRules.HitStop(ShotWeight.Skill, 0.05f, v), Is.EqualTo(0.05f));
            Assert.That(FiringWeightRules.HitStop(ShotWeight.Heavy, 0.05f, null), Is.EqualTo(0.05f), "with none running, the feel's");
            Assert.That(v.HeavyHitStop, Is.LessThan(RecastClock.MostPerFrame), "inside what work order 038 made safe");
        }

        [Test]
        public void TheKick_ReturnsToNothingInItsTime_AndTwentyBasicShotsNeverAddUpPastTheMost()
        {
            var v = new FiringWeightValues();
            Assert.That(FiringWeightRules.KickShape(0f, v.KickSeconds, v.KickPeakShare), Is.EqualTo(0f));
            Assert.That(FiringWeightRules.KickShape(v.KickSeconds * v.KickPeakShare, v.KickSeconds, v.KickPeakShare), Is.EqualTo(1f).Within(1e-5f));
            Assert.That(FiringWeightRules.KickShape(v.KickSeconds, v.KickSeconds, v.KickPeakShare), Is.EqualTo(0f), "back at its end");
            var kicks = new CameraKicks();
            float most = 0f;
            for (int shot = 0; shot < 20; shot++)
            {
                float at = shot * 0.25f; // four a second
                kicks.Add(at, 1f, 0f, FiringWeightRules.Kick(ShotWeight.Basic, v));
                for (float t = at; t < at + 0.25f; t += 0.005f)
                {
                    kicks.Offset(t, v.KickSeconds, v.KickPeakShare, v.MostKick, out float x, out float z);
                    most = Math.Max(most, (float)Math.Sqrt(x * x + z * z));
                    Assert.That(x, Is.LessThanOrEqualTo(1e-6f), "back against the shot's line");
                }
            }
            Assert.That(most, Is.EqualTo(v.BasicKick).Within(1e-3f), "never more than one basic shot's kick");
            kicks.Offset(20 * 0.25f + v.KickSeconds, v.KickSeconds, v.KickPeakShare, v.MostKick, out float ex, out float ez);
            Assert.That(ex, Is.EqualTo(0f).Within(1e-6f), "and back where it was");
            // Heavy shots on top of each other: clamped.
            var heavy = new CameraKicks();
            for (int i = 0; i < 5; i++) heavy.Add(0.01f * i, 0f, 1f, v.HeavyKick);
            heavy.Offset(0.04f, v.KickSeconds, v.KickPeakShare, v.MostKick, out float hx, out float hz);
            Assert.That(Math.Sqrt(hx * hx + hz * hz), Is.LessThanOrEqualTo(v.MostKick + 1e-5f));
        }

        [Test]
        public void TheLight_IsOnForItsTime_FadingFromItsPeak_AndOffAfter()
        {
            var v = new FiringWeightValues();
            float peak = FiringWeightRules.Light(ShotWeight.Basic, v);
            Assert.That(FiringWeightRules.LightAt(0f, v.LightSeconds, peak), Is.EqualTo(peak));
            Assert.That(FiringWeightRules.LightAt(v.LightSeconds * 0.5f, v.LightSeconds, peak), Is.EqualTo(peak * 0.5f).Within(1e-5f));
            Assert.That(FiringWeightRules.LightAt(v.LightSeconds, v.LightSeconds, peak), Is.EqualTo(0f));
            Assert.That(FiringWeightRules.Light(ShotWeight.FullClean, v), Is.GreaterThan(FiringWeightRules.Light(ShotWeight.Heavy, v)));
            Assert.That(FiringWeightRules.Light(ShotWeight.Heavy, v), Is.GreaterThan(FiringWeightRules.Light(ShotWeight.Basic, v)));
            v.On = false;
            Assert.That(FiringWeightRules.Light(ShotWeight.Heavy, v), Is.EqualTo(0f), "off: none");
            Assert.That(FiringWeightRules.Kick(ShotWeight.Heavy, v), Is.EqualTo(0f));
        }

        // ---------------- 4. The numbers ----------------

        [Test]
        public void ANumbersKind_ACleanPressOrPerfectRelease_TheTop_ACritItsOwn_ThenMarks()
        {
            Assert.That(FeedbackRules.HitKind(true, true, 5, 5, true), Is.EqualTo(NumberKind.Perfect), "a clean press's hit, a crit as well");
            Assert.That(FeedbackRules.HitKind(false, true, 3, 5, false), Is.EqualTo(NumberKind.Crit), "a rolled crit that spent marks");
            Assert.That(FeedbackRules.HitKind(false, false, 3, 5, false), Is.EqualTo(NumberKind.Marks));
            Assert.That(FeedbackRules.HitKind(false, false, 0, 5, false), Is.EqualTo(NumberKind.Plain));
            Assert.That(HitNumberRules.LookOf(NumberKind.Plain), Is.EqualTo(HitNumberLook.Plain));
            Assert.That(HitNumberRules.LookOf(NumberKind.Crit), Is.EqualTo(HitNumberLook.Crit));
            Assert.That(HitNumberRules.LookOf(NumberKind.Perfect), Is.EqualTo(HitNumberLook.Top));
            Assert.That(HitNumberRules.LookOf(NumberKind.Hurt), Is.EqualTo(HitNumberLook.AsBefore));
            Assert.That(FeedbackRules.IsBig(NumberKind.Crit), Is.True);
        }

        [Test]
        public void ANumbersSize_AndLife_OrdinarySmall_CritLarger_TopLargest()
        {
            var v = new HitNumberValues();
            int plain = HitNumberRules.Size(NumberKind.Plain, v, 20), crit = HitNumberRules.Size(NumberKind.Crit, v, 20), top = HitNumberRules.Size(NumberKind.Perfect, v, 20);
            Assert.That(plain, Is.LessThanOrEqualTo(new InterfaceValues().NumberSize), "decision 0011: ordinary hits stay small");
            Assert.That(crit, Is.GreaterThan(plain));
            Assert.That(top, Is.GreaterThan(crit));
            Assert.That(HitNumberRules.Size(NumberKind.Marks, v, 25), Is.EqualTo(25), "marks as before");
            Assert.That(HitNumberRules.Life(NumberKind.Plain, v, 0.6f), Is.EqualTo(0.5f));
            Assert.That(HitNumberRules.Life(NumberKind.Crit, v, 0.6f), Is.GreaterThan(0.5f), "a crit a little longer");
            Assert.That(HitNumberRules.Life(NumberKind.Hurt, v, 0.6f), Is.EqualTo(0.6f));
            Assert.That(HitNumberRules.Pop(0f, v), Is.EqualTo(1.5f));
            Assert.That(HitNumberRules.Pop(0.08f, v), Is.EqualTo(1f), "snapped to size in 0.08 s");
            v.On = false;
            Assert.That(HitNumberRules.Size(NumberKind.Crit, v, 20), Is.EqualTo(20), "off: as this morning");
            Assert.That(HitNumberRules.Life(NumberKind.Plain, v, 0.6f), Is.EqualTo(0.6f));
        }

        [Test]
        public void ManyHitsInOneFrame_OnOneEnemy_AreOneNumber_ABlastsNumbers_TakeLanes_TheCapHolds()
        {
            var v = new HitNumberValues();
            var tally = new DamageTally();
            for (int i = 0; i < 5; i++) tally.Add(1, 30, NumberKind.Plain, 2f, 0.18f, 24, v.Together);
            Assert.That(tally.Numbers.Count, Is.EqualTo(1), "one number");
            Assert.That(tally.Numbers[0].Sum, Is.EqualTo(150), "their sum");
            for (int t = 2; t <= 6; t++) tally.Add(t, 40, NumberKind.Plain, 2f, 0.18f, 24, v.Together);
            Assert.That(tally.Numbers.Select(n => n.Lane), Is.EqualTo(new[] { 0, 1, 2, 3, 4, 5 }), "a blast's numbers each in a lane");
            var offsets = new HashSet<(float, float)>();
            foreach (TallyNumber n in tally.Numbers)
            {
                HitNumberRules.LaneOffset(n.Lane, v, out float dx, out float dy);
                offsets.Add(((float)Math.Round(dx, 3), (float)Math.Round(dy, 3)));
            }
            Assert.That(offsets.Count, Is.EqualTo(6), "no two on one point");
            tally.Add(9, 10, NumberKind.Plain, 3f, 0.18f, 24, v.Together);
            Assert.That(tally.Numbers.Last().Lane, Is.EqualTo(0), "a number later, alone: its own place");
            var capped = new DamageTally();
            for (int t = 1; t <= 30; t++) capped.Add(t, t, NumberKind.Plain, 0f, 0.18f, 24, v.Together);
            Assert.That(capped.Numbers.Count, Is.EqualTo(24), "24 at most");
            Assert.That(capped.Numbers[0].Target, Is.EqualTo(7), "the oldest went");
        }

        [Test]
        public void ANumber_Expires_ByItsKindsLife()
        {
            var v = new HitNumberValues();
            var tally = new DamageTally();
            tally.Add(1, 10, NumberKind.Plain, 0f, 0.18f, 24);
            tally.Add(2, 10, NumberKind.Crit, 0f, 0.18f, 24);
            tally.Add(3, 10, NumberKind.Perfect, 0f, 0.18f, 24);
            tally.Expire(0.55f, v, 0.6f);
            Assert.That(tally.Numbers.Select(n => n.Target), Is.EqualTo(new[] { 2, 3 }), "the ordinary one gone at 0.5 s");
            tally.Expire(0.7f, v, 0.6f);
            Assert.That(tally.Numbers.Select(n => n.Target), Is.EqualTo(new[] { 3 }));
            tally.Expire(0.8f, v, 0.6f);
            Assert.That(tally.Numbers, Is.Empty);
        }

        [Test]
        public void ANumber_DriftsAlongTheSlant_UpAndToTheRight()
        {
            var v = new HitNumberValues();
            HitNumberRules.DriftAt(v.PlainSeconds, v.PlainSeconds, v, out float dx, out float dy);
            Assert.That(dy, Is.LessThan(0f), "up");
            Assert.That(dx, Is.GreaterThan(0f), "and along the lean");
            Assert.That(Math.Sqrt(dx * dx + dy * dy), Is.EqualTo(v.Drift).Within(1e-3f), "a short way");
            HitNumberRules.DriftAt(0f, v.PlainSeconds, v, out float x0, out float y0);
            Assert.That(x0, Is.EqualTo(0f));
            Assert.That(y0, Is.EqualTo(0f));
        }

        // ---------------- 5. The basic shot's variations ----------------

        [Test]
        public void TheVariationPicker_NeverRepeats_AndIsSeeded()
        {
            var v = new ShotVariationValues();
            Assert.That(ShotVariationPicker.Count(v), Is.GreaterThanOrEqualTo(4), "at least four");
            var a = new ShotVariationPicker();
            var b = new ShotVariationPicker();
            a.Seed(42UL);
            b.Seed(42UL);
            var seen = new HashSet<int>();
            int last = -1;
            for (int i = 0; i < 500; i++)
            {
                ShotVariation x = a.Next(v), y = b.Next(v);
                Assert.That(x.Index, Is.Not.EqualTo(last), "never the same twice running");
                Assert.That(y.Index, Is.EqualTo(x.Index), "the same seed, the same picks");
                Assert.That(y.Pitch, Is.EqualTo(x.Pitch));
                last = x.Index;
                seen.Add(x.Index);
                Assert.That(x.Pitch / v.Pitch[x.Index], Is.InRange(1f - v.PitchJitter - 1e-5f, 1f + v.PitchJitter + 1e-5f), "pitch within 4 %");
                float db = 20f * (float)Math.Log10(x.Volume);
                Assert.That(db, Is.InRange(v.GainDb - v.VolumeJitterDb - 1e-3f, v.GainDb + v.VolumeJitterDb + 1e-3f), "-3 dB, within 1.5 dB");
            }
            Assert.That(seen.Count, Is.EqualTo(ShotVariationPicker.Count(v)), "every one comes up");
            var c = new ShotVariationPicker();
            c.Seed(43UL);
            var other = Enumerable.Range(0, 20).Select(_ => c.Next(v).Index).ToArray();
            a.Seed(42UL);
            var again = Enumerable.Range(0, 20).Select(_ => a.Next(v).Index).ToArray();
            Assert.That(other, Is.Not.EqualTo(again), "another seed, other picks");
        }

        [Test]
        public void TheVariations_DifferInBrightnessPitchAndTail()
        {
            var v = new ShotVariationValues();
            int n = ShotVariationPicker.Count(v);
            Assert.That(v.Brightness.Take(n).Distinct().Count(), Is.EqualTo(n), "each its own brightness");
            Assert.That(v.Pitch.Take(n).Distinct().Count(), Is.EqualTo(n), "pitch");
            Assert.That(v.Tail.Take(n).Distinct().Count(), Is.EqualTo(n), "tail");
            Assert.That(ShotVariationPicker.DbToGain(-3f), Is.EqualTo(0.708f).Within(1e-3f));
        }
    }
}
