using System;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 019 (2026-11-20), the timing feedback: which word a moment gives, the caps on the words (one per target,
    /// eight in all, the newest win), the timing misses that say EARLY or LATE, the rule that sums hits on a target within
    /// the window and starts a new number after it, the damage numbers setting, and the tones made in code.
    /// </summary>
    public class FeedbackRulesTests
    {
        static SkillDefinition Skill(string name) => SkillDefaults.Create().Single(s => s.Name == name);
        static readonly FeedbackValues V = new FeedbackValues();
        static float Window => V.MergeSeconds + V.MergeGrace;

        // ---------------- The words ----------------

        [TestCase(FeedbackMoment.Clean, 0, 0, "CLEAN", FeedbackTint.Clean, FeedbackSize.Normal)]
        [TestCase(FeedbackMoment.Perfect, 0, 0, "PERFECT", FeedbackTint.Clean, FeedbackSize.Large)]
        [TestCase(FeedbackMoment.Counter, 0, 0, "COUNTER", FeedbackTint.Counter, FeedbackSize.Normal)]
        [TestCase(FeedbackMoment.MarksSpent, 3, 5, "x3", FeedbackTint.Marks, FeedbackSize.Normal)]
        [TestCase(FeedbackMoment.MarksSpent, 5, 5, "x5", FeedbackTint.MarksFull, FeedbackSize.Large)]
        [TestCase(FeedbackMoment.Boom, 0, 0, "BOOM", FeedbackTint.Boom, FeedbackSize.Normal)]
        [TestCase(FeedbackMoment.Dazed, 0, 0, "DAZED", FeedbackTint.Dazed, FeedbackSize.Normal)]
        [TestCase(FeedbackMoment.Early, 0, 0, "EARLY", FeedbackTint.Miss, FeedbackSize.Small)]
        [TestCase(FeedbackMoment.Late, 0, 0, "LATE", FeedbackTint.Miss, FeedbackSize.Small)]
        public void EachMoment_GivesItsWord(FeedbackMoment moment, int marks, int cap, string text, FeedbackTint tint, FeedbackSize size)
        {
            FeedbackWord w = FeedbackRules.Word(moment, marks, cap);
            Assert.That(w.Text, Is.EqualTo(text));
            Assert.That(w.Tint, Is.EqualTo(tint));
            Assert.That(w.Size, Is.EqualTo(size));
        }

        [Test]
        public void TheMissWords_ComeFromTheMiss_AndNoMissGivesNone()
        {
            Assert.That(FeedbackRules.Word(TimingMiss.Early), Is.EqualTo(FeedbackMoment.Early));
            Assert.That(FeedbackRules.Word(TimingMiss.Late), Is.EqualTo(FeedbackMoment.Late));
            Assert.That(FeedbackRules.Word(TimingMiss.None), Is.Null);
        }

        [Test]
        public void InOneMoment_TheRareWordOutranksTheCommonOne()
        {
            int R(FeedbackMoment m) => FeedbackRules.Word(m, 2, 5).Rank;
            Assert.That(R(FeedbackMoment.Counter), Is.GreaterThan(R(FeedbackMoment.Perfect)));
            Assert.That(R(FeedbackMoment.Perfect), Is.GreaterThan(R(FeedbackMoment.Clean)));
            Assert.That(R(FeedbackMoment.Clean), Is.GreaterThan(R(FeedbackMoment.Dazed)));
            Assert.That(R(FeedbackMoment.Dazed), Is.GreaterThan(R(FeedbackMoment.MarksSpent)));
            Assert.That(R(FeedbackMoment.MarksSpent), Is.GreaterThan(R(FeedbackMoment.Boom)));
            Assert.That(R(FeedbackMoment.Boom), Is.GreaterThan(R(FeedbackMoment.Early)));
        }

        // ---------------- The caps ----------------

        static FeedbackWord W(FeedbackMoment m) => FeedbackRules.Word(m, 3, 5);

        [Test]
        public void OneWordPerTarget_ANewOneReplacesTheOld()
        {
            var board = new WordBoard();
            board.Add(1, W(FeedbackMoment.MarksSpent), 0f, 1, V.MaxWords);
            board.Add(2, W(FeedbackMoment.Boom), 0f, 1, V.MaxWords);
            Assert.That(board.Add(1, W(FeedbackMoment.Early), 0.1f, 2, V.MaxWords), Is.GreaterThan(0), "a later moment always replaces");
            Assert.That(board.Words.Count(w => w.Target == 1), Is.EqualTo(1));
            Assert.That(board.Words.Single(w => w.Target == 1).Word.Text, Is.EqualTo("EARLY"));
            Assert.That(board.Words.Count, Is.EqualTo(2));
        }

        [Test]
        public void InTheSameMoment_AHigherRankKeepsThePlace()
        {
            var board = new WordBoard();
            board.Add(1, W(FeedbackMoment.Dazed), 0f, 7, V.MaxWords);
            Assert.That(board.Add(1, W(FeedbackMoment.MarksSpent), 0f, 7, V.MaxWords), Is.EqualTo(0), "x3 in the frame DAZED came: dropped");
            Assert.That(board.Words.Single().Word.Text, Is.EqualTo("DAZED"));
            Assert.That(board.Add(1, W(FeedbackMoment.Clean), 0f, 7, V.MaxWords), Is.GreaterThan(0), "CLEAN outranks it");
            Assert.That(board.Words.Single().Word.Text, Is.EqualTo("CLEAN"));
        }

        [Test]
        public void AtMostEightWords_TheOldestGo_SoTheNewestWin()
        {
            var board = new WordBoard();
            for (int t = 1; t <= 11; t++) board.Add(t, W(FeedbackMoment.Boom), t * 0.01f, t, V.MaxWords);
            Assert.That(V.MaxWords, Is.EqualTo(8));
            Assert.That(board.Words.Count, Is.EqualTo(8));
            Assert.That(board.Words.Select(w => w.Target), Is.EqualTo(Enumerable.Range(4, 8)), "targets 4 to 11, the newest");
        }

        [Test]
        public void AWord_LivesItsTime_AndFades()
        {
            var board = new WordBoard();
            board.Add(1, W(FeedbackMoment.Clean), 1f, 1, V.MaxWords);
            board.Expire(1f + V.WordSeconds - 0.01f, V.WordSeconds);
            Assert.That(board.Words.Count, Is.EqualTo(1));
            board.Expire(1f + V.WordSeconds + 1e-3f, V.WordSeconds);
            Assert.That(board.Words.Count, Is.EqualTo(0));
            Assert.That(FeedbackRules.Alpha(0f, 0.7f, 0.55f), Is.EqualTo(1f));
            Assert.That(FeedbackRules.Alpha(0.35f, 0.7f, 0.55f), Is.EqualTo(1f));
            Assert.That(FeedbackRules.Alpha(0.6f, 0.7f, 0.55f), Is.InRange(0.01f, 0.99f));
            Assert.That(FeedbackRules.Alpha(0.7f, 0.7f, 0.55f), Is.EqualTo(0f));
            Assert.That(FeedbackRules.PopScale(0f, 1.3f, 0.08f), Is.EqualTo(1.3f).Within(1e-5f));
            Assert.That(FeedbackRules.PopScale(0.08f, 1.3f, 0.08f), Is.EqualTo(1f));
        }

        // ---------------- The timing misses ----------------

        [Test]
        public void TripleTap_OnlyItsLastPressNotClean_IsEarly()
        {
            SkillDefinition tap = Skill("Triple Tap");
            Assert.That(FeedbackRules.RecastMiss(tap, 3, clean: false), Is.EqualTo(TimingMiss.Early), "the third press, before the clean part");
            Assert.That(FeedbackRules.RecastMiss(tap, 3, clean: true), Is.EqualTo(TimingMiss.None), "a clean one");
            Assert.That(FeedbackRules.RecastMiss(tap, 2, clean: false), Is.EqualTo(TimingMiss.None), "the second press has no timing");
        }

        [Test]
        public void Backstep_ItsShotNotClean_IsEarly_AndASkillWithNoCleanPressNever()
        {
            SkillDefinition step = Skill("Backstep");
            Assert.That(FeedbackRules.RecastMiss(step, 2, clean: false), Is.EqualTo(TimingMiss.Early));
            Assert.That(FeedbackRules.RecastMiss(step, 2, clean: true), Is.EqualTo(TimingMiss.None));
            SkillDefinition plain = Skill("Triple Tap");
            plain.CleanTapMarks = 0;
            Assert.That(FeedbackRules.RecastMiss(plain, 3, clean: false), Is.EqualTo(TimingMiss.None), "no clean press, no timing to miss");
            Assert.That(FeedbackRules.RecastMiss(Skill("Quick Shot"), 1, clean: false), Is.EqualTo(TimingMiss.None), "no chain");
            Assert.That(FeedbackRules.RecastMiss(null, 3, false), Is.EqualTo(TimingMiss.None));
        }

        [TestCase(0.1f, TimingMiss.None)]   // a tap: a choice
        [TestCase(0.45f, TimingMiss.None)]  // under half the charge
        [TestCase(0.5f, TimingMiss.Early)]  // half way
        [TestCase(0.79f, TimingMiss.Early)]
        [TestCase(0.8f, TimingMiss.None)]   // the band's edges are inside it
        [TestCase(0.9f, TimingMiss.None)]
        [TestCase(1f, TimingMiss.None)]
        [TestCase(1.05f, TimingMiss.Late)]
        [TestCase(2.5f, TimingMiss.Late)]   // held until it fired by itself
        public void LongShot_LetGoOutsideItsBand_AfterHalfACharge_IsEarlyOrLate(float charged, TimingMiss want)
        {
            SkillDefinition shot = Skill("Long Shot");
            Assume.That(shot.PerfectFrom, Is.EqualTo(0.8f));
            Assume.That(shot.PerfectTo, Is.EqualTo(1f));
            Assume.That(shot.ChargeTime, Is.EqualTo(1f));
            Assert.That(FeedbackRules.ChargeMiss(shot, charged, perfect: false, V.MissFromCharge), Is.EqualTo(want));
        }

        [Test]
        public void ChargeMiss_NeverForAPerfectRelease_NorASkillWithNoBand()
        {
            Assert.That(FeedbackRules.ChargeMiss(Skill("Long Shot"), 1.5f, perfect: true, 0.5f), Is.EqualTo(TimingMiss.None));
            SkillDefinition noBand = Skill("Long Shot");
            noBand.PerfectFrom = noBand.PerfectTo = 0f;
            Assert.That(FeedbackRules.ChargeMiss(noBand, 1.5f, false, 0.5f), Is.EqualTo(TimingMiss.None));
            Assert.That(FeedbackRules.ChargeMiss(Skill("Quick Shot"), 1.5f, false, 0.5f), Is.EqualTo(TimingMiss.None), "not a charge");
        }

        // ---------------- The damage numbers ----------------

        [Test]
        public void HitsOnATarget_WithinTheWindow_AreOneNumber_TheirSum()
        {
            var tally = new DamageTally();
            Assert.That(tally.Add(1, 70, NumberKind.Plain, 10f, Window, V.MaxNumbers), Is.False, "the first starts a number");
            Assert.That(tally.Add(1, 30, NumberKind.Plain, 10.1f, Window, V.MaxNumbers), Is.True, "0.1 s later: joins it");
            Assert.That(tally.Add(1, 30, NumberKind.Plain, 10.25f, Window, V.MaxNumbers), Is.True, "0.15 s after the latest: still");
            Assert.That(tally.Numbers.Count, Is.EqualTo(1));
            Assert.That(tally.Numbers[0].Sum, Is.EqualTo(130));
            Assert.That(tally.Numbers[0].Hits, Is.EqualTo(3));
        }

        [Test]
        public void AHitAfterTheWindow_StartsANewNumber()
        {
            var tally = new DamageTally();
            tally.Add(1, 70, NumberKind.Plain, 10f, Window, V.MaxNumbers);
            Assert.That(tally.Add(1, 40, NumberKind.Plain, 10f + Window + 0.01f, Window, V.MaxNumbers), Is.False);
            Assert.That(tally.Numbers.Select(n => n.Sum), Is.EqualTo(new[] { 70, 40 }));
        }

        [Test]
        public void TheWindow_Is015sAndAFramesGrace()
        {
            Assert.That(V.MergeSeconds, Is.EqualTo(0.15f));
            Assert.That(V.MergeGrace, Is.InRange(0f, 0.05f));
            var tally = new DamageTally();
            tally.Add(1, 10, NumberKind.Plain, 0f, Window, V.MaxNumbers);
            Assert.That(tally.Add(1, 10, NumberKind.Plain, 0.2f, Window, V.MaxNumbers), Is.False, "0.2 s apart: two numbers");
        }

        [Test]
        public void OtherTargets_HaveTheirOwnNumbers_AndTheSameTimeJoins()
        {
            var tally = new DamageTally();
            tally.Add(1, 180, NumberKind.Plain, 5f, Window, V.MaxNumbers);
            tally.Add(2, 180, NumberKind.Plain, 5f, Window, V.MaxNumbers);
            Assert.That(tally.Add(1, 20, NumberKind.Plain, 5f, Window, V.MaxNumbers), Is.True, "a keg and the shot that set it off: one number");
            Assert.That(tally.Numbers.Select(n => n.Target + ":" + n.Sum), Is.EqualTo(new[] { "1:200", "2:180" }));
        }

        [Test]
        public void ANumber_ShowsTheStrongestKindOfItsHits_AndLivesFromItsLatestHit()
        {
            var tally = new DamageTally();
            tally.Add(1, 10, NumberKind.Plain, 0f, Window, V.MaxNumbers);
            tally.Add(1, 10, NumberKind.Marks, 0.1f, Window, V.MaxNumbers);
            tally.Add(1, 10, NumberKind.Dazed, 0.2f, Window, V.MaxNumbers);
            Assert.That(tally.Numbers.Single().Kind, Is.EqualTo(NumberKind.Marks));
            tally.Expire(0.2f + V.NumberSeconds - 0.01f, V.NumberSeconds);
            Assert.That(tally.Numbers.Count, Is.EqualTo(1));
            tally.Expire(0.2f + V.NumberSeconds + 1e-3f, V.NumberSeconds);
            Assert.That(tally.Numbers.Count, Is.EqualTo(0));
        }

        [Test]
        public void NoDamage_NoNumber_AndTheNewestWinPastTheCap()
        {
            var tally = new DamageTally();
            Assert.That(tally.Add(1, 0, NumberKind.Plain, 0f, Window, 3), Is.False);
            Assert.That(tally.Numbers, Is.Empty);
            for (int t = 1; t <= 5; t++) tally.Add(t, t, NumberKind.Plain, 0f, Window, 3);
            Assert.That(tally.Numbers.Select(n => n.Target), Is.EqualTo(new[] { 3, 4, 5 }));
        }

        [TestCase(false, 0, false, NumberKind.Plain)]
        [TestCase(false, 0, true, NumberKind.Dazed)]
        [TestCase(false, 3, false, NumberKind.Marks)]
        [TestCase(false, 5, true, NumberKind.MarksFull)]
        [TestCase(true, 0, false, NumberKind.Perfect)]
        public void AHit_StandsOutForMarksSpent_APerfectRelease_OrADazedTarget(bool perfect, int marks, bool dazed, NumberKind want)
        {
            Assert.That(FeedbackRules.HitKind(perfect, marks, 5, dazed), Is.EqualTo(want));
            Assert.That(FeedbackRules.IsBig(want), Is.EqualTo(want != NumberKind.Plain));
        }

        [Test]
        public void ANumber_IsInTheColourOfItsWord()
        {
            Assert.That(FeedbackRules.NumberTint(NumberKind.Perfect), Is.EqualTo(FeedbackRules.Word(FeedbackMoment.Perfect).Tint));
            Assert.That(FeedbackRules.NumberTint(NumberKind.Marks), Is.EqualTo(FeedbackRules.Word(FeedbackMoment.MarksSpent, 3, 5).Tint));
            Assert.That(FeedbackRules.NumberTint(NumberKind.MarksFull), Is.EqualTo(FeedbackRules.Word(FeedbackMoment.MarksSpent, 5, 5).Tint));
            Assert.That(FeedbackRules.NumberTint(NumberKind.Dazed), Is.EqualTo(FeedbackRules.Word(FeedbackMoment.Dazed).Tint));
            Assert.That(FeedbackRules.NumberTint(NumberKind.Hurt), Is.EqualTo(FeedbackTint.Hurt));
            Assert.That(FeedbackRules.NumberTint(NumberKind.Plain), Is.EqualTo(FeedbackTint.Plain));
            Assert.That(FeedbackRules.IsBig(NumberKind.Hurt), Is.False, "hers has its own size");
        }

        // ---------------- The setting ----------------

        [Test]
        public void DamageNumbers_OnByDefault_TheStoredChoiceOverrides()
        {
            Assert.That(V.DamageNumbers, Is.True, "on by default");
            Assert.That(FeedbackRules.ShowsDamageNumbers(FeedbackRules.NoOverride, true), Is.True);
            Assert.That(FeedbackRules.ShowsDamageNumbers(FeedbackRules.NumbersOff, true), Is.False);
            Assert.That(FeedbackRules.ShowsDamageNumbers(FeedbackRules.NumbersOn, false), Is.True);
            Assert.That(FeedbackRules.StoredNumbers(7), Is.EqualTo(FeedbackRules.NoOverride), "a stored value made safe");
            Assert.That(FeedbackRules.StoredNumbers(0), Is.EqualTo(FeedbackRules.NumbersOff));
        }

        // ---------------- The tones ----------------

        [TestCase(FeedbackTone.Clean)]
        [TestCase(FeedbackTone.Perfect)]
        [TestCase(FeedbackTone.Counter)]
        public void EachTone_IsShort_StartsFast_EndsSilent_AndPeaksAtItsPeak(FeedbackTone tone)
        {
            float[] s = ToneRules.Samples(tone, ToneRules.SampleRate);
            float seconds = s.Length / (float)ToneRules.SampleRate;
            Assert.That(seconds, Is.LessThan(0.2f), "under 0.2 s");
            Assert.That(seconds, Is.EqualTo(ToneRules.Seconds(tone)).Within(1e-3f));
            Assert.That(s.Max(v => Math.Abs(v)), Is.EqualTo(ToneRules.Peak).Within(1e-4f));
            Assert.That(Math.Abs(s[0]), Is.LessThan(0.01f), "from silence");
            Assert.That(Math.Abs(s[s.Length - 1]), Is.LessThan(1e-4f), "to silence: no click");
            int attack = (int)(0.01f * ToneRules.SampleRate);
            Assert.That(s.Take(attack).Max(v => Math.Abs(v)), Is.GreaterThan(0.3f * ToneRules.Peak), "loud within 10 ms: a fast attack");
            int tail = (int)(0.02f * ToneRules.SampleRate);
            Assert.That(s.Skip(s.Length - tail).Max(v => Math.Abs(v)), Is.LessThan(0.35f * ToneRules.Peak), "decayed by the end");
        }

        [Test]
        public void ThePerfectTone_IsTwoRisingNotes_AndTheCounterIsLower()
        {
            float[] p = ToneRules.Samples(FeedbackTone.Perfect, ToneRules.SampleRate);
            int half = (int)(0.075f * ToneRules.SampleRate);
            Assert.That(Crossings(p, 0, half) / 0.075f, Is.EqualTo(2f * ToneRules.PerfectLow).Within(0.1f * ToneRules.PerfectLow), "the first note");
            Assert.That(Crossings(p, half, half + (int)(0.05f * ToneRules.SampleRate)) / 0.05f,
                        Is.GreaterThan(2f * ToneRules.PerfectLow * 1.2f), "the second is higher");
            Assert.That(ToneRules.CounterFrequency, Is.LessThan(ToneRules.CleanFrequency / 2f));
        }

        static int Crossings(float[] s, int from, int to)
        {
            int n = 0;
            for (int i = Math.Max(1, from); i < Math.Min(to, s.Length); i++)
                if ((s[i - 1] < 0f) != (s[i] < 0f)) n++;
            return n;
        }
    }
}
