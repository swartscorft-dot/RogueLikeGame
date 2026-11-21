using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 074, the frame times of a play session: the histogram and its percentiles from given frame times, the
    /// slowest frames, what counts as time in play, the log's events read back, and the summary's block with the causes
    /// of its hitches; a log without frame times has no block.
    /// </summary>
    public class FrameTimesTests
    {
        static FrameHistogram Of(params double[] ms)
        {
            var h = new FrameHistogram();
            for (int i = 0; i < ms.Length; i++) h.Add(ms[i], i, i * 0.5);
            return h;
        }

        static double[] OneToHundred() => Enumerable.Range(1, 100).Select(i => (double)(101 - i)).ToArray(); // not sorted

        // ---------------- The histogram ----------------

        [Test]
        public void TheBuckets_AreAQuarterMillisecond_UpToAHundred_AndOneForEverySlowerFrame()
        {
            Assert.That(FrameHistogram.BucketMs, Is.EqualTo(0.25));
            Assert.That(FrameHistogram.Buckets, Is.EqualTo((int)(FrameHistogram.TopMs / FrameHistogram.BucketMs)));
            Assert.That(FrameHistogram.BucketOf(0), Is.EqualTo(0));
            Assert.That(FrameHistogram.BucketOf(-3), Is.EqualTo(0));
            Assert.That(FrameHistogram.BucketOf(double.NaN), Is.EqualTo(0));
            Assert.That(FrameHistogram.BucketOf(0.24), Is.EqualTo(0));
            Assert.That(FrameHistogram.BucketOf(0.25), Is.EqualTo(1));
            Assert.That(FrameHistogram.BucketOf(16.7), Is.EqualTo(66));
            Assert.That(FrameHistogram.BucketOf(99.99), Is.EqualTo(399));
            Assert.That(FrameHistogram.BucketOf(100), Is.EqualTo(400));
            Assert.That(FrameHistogram.BucketOf(2500), Is.EqualTo(400));
        }

        [Test]
        public void FromGivenFrames_TheMean_TheFps_TheFastestAndSlowest_AndTheFramesOver16_7And33_3()
        {
            FrameHistogram h = Of(10, 16.6, 16.7, 20, 33.3, 33.4, 50);
            Assert.That(h.Frames, Is.EqualTo(7));
            Assert.That(h.SumMs, Is.EqualTo(180).Within(1e-9));
            Assert.That(h.MeanMs, Is.EqualTo(180 / 7.0).Within(1e-9));
            Assert.That(h.Fps, Is.EqualTo(7000 / 180.0).Within(1e-9));
            Assert.That(h.MinMs, Is.EqualTo(10));
            Assert.That(h.MaxMs, Is.EqualTo(50));
            Assert.That(h.Over60, Is.EqualTo(5), "over 1000/60 ms: 16.7, 20, 33.3, 33.4, 50; not 16.6");
            Assert.That(h.Over30, Is.EqualTo(2), "over 1000/30 ms: 33.4 and 50; not 33.3");
            Assert.That(Enumerable.Range(0, FrameHistogram.Buckets + 1).Sum(h.CountIn), Is.EqualTo(7), "every frame in a bucket");

            var none = new FrameHistogram();
            Assert.That(none.MeanMs, Is.NaN);
            Assert.That(none.Fps, Is.NaN);
            Assert.That(none.Percentile(0.95), Is.NaN);
            Assert.That(none.MainMeanMs, Is.NaN);
        }

        [Test]
        public void ThePercentiles_ByNearestRank_FromTheHistogram_ToAQuarterMillisecond_InOrder_AndInsideTheFrames()
        {
            FrameHistogram h = Of(OneToHundred());
            double p95 = h.Percentile(0.95), p99 = h.Percentile(0.99);
            Assert.That(p95, Is.GreaterThanOrEqualTo(95).And.LessThanOrEqualTo(95 + FrameHistogram.BucketMs), "the 95th frame of 100 is 95 ms");
            Assert.That(p99, Is.GreaterThanOrEqualTo(99).And.LessThanOrEqualTo(99 + FrameHistogram.BucketMs));
            Assert.That(p95, Is.LessThanOrEqualTo(p99));
            Assert.That(h.Percentile(1), Is.EqualTo(100), "the top is the slowest frame");
            Assert.That(h.Percentile(0), Is.EqualTo(1.25), "the bottom bucket's top");
            Assert.That(h.MeanMs, Is.EqualTo(50.5).Within(1e-9));

            // Frames all alike, as a test's are: no percentile above the slowest or below the fastest.
            FrameHistogram same = Of(Enumerable.Repeat(0.3, 50).ToArray());
            Assert.That(same.Percentile(0.95), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(same.Percentile(0.99), Is.EqualTo(0.3).Within(1e-12));
            Assert.That(same.MeanMs, Is.EqualTo(0.3).Within(1e-12));

            // Slower than the top bucket: the slowest frame.
            FrameHistogram slow = Of(Enumerable.Repeat(5.0, 99).Concat(new[] { 250.0 }).ToArray());
            Assert.That(slow.Percentile(0.99), Is.EqualTo(5.25));
            Assert.That(slow.Percentile(0.999), Is.EqualTo(250));
        }

        [Test]
        public void TheFiveSlowestFrames_SlowestFirst_WithWhenAndHowFarIn_AndOfTwoEqualTheEarlier()
        {
            FrameHistogram h = Of(3, 9, 1, 7, 5, 9, 2, 8, 6, 4); // seen at 0..9, half a second apart into the part
            Assert.That(h.Hitches, Is.EqualTo(FrameTimeEvents.HitchesPerPart));
            Assert.That(Enumerable.Range(0, h.Hitches).Select(h.HitchMs), Is.EqualTo(new double[] { 9, 9, 8, 7, 6 }));
            Assert.That(Enumerable.Range(0, h.Hitches).Select(h.HitchAt), Is.EqualTo(new double[] { 1, 5, 7, 3, 8 }));
            Assert.That(h.HitchInto(0), Is.EqualTo(0.5));
            Assert.That(h.HitchMs(0), Is.EqualTo(h.MaxMs), "the slowest is the first");

            FrameHistogram two = Of(4, 2);
            Assert.That(two.Hitches, Is.EqualTo(2), "a part of two frames has two");
        }

        [Test]
        public void Merged_IsAsOneHistogramOfAllTheFrames_AndClearedIsEmpty()
        {
            double[] a = { 4, 12, 7, 40, 2 }, b = { 17, 3, 90, 5 };
            FrameHistogram one = Of(a.Concat(b).ToArray());
            FrameHistogram merged = Of(a);
            merged.Merge(Of(b));
            Assert.That(merged.Frames, Is.EqualTo(one.Frames));
            Assert.That(merged.SumMs, Is.EqualTo(one.SumMs).Within(1e-9));
            Assert.That(merged.MinMs, Is.EqualTo(2));
            Assert.That(merged.MaxMs, Is.EqualTo(90));
            Assert.That(merged.Over60, Is.EqualTo(one.Over60));
            Assert.That(merged.Over30, Is.EqualTo(one.Over30));
            Assert.That(merged.Percentile(0.95), Is.EqualTo(one.Percentile(0.95)));
            Assert.That(merged.BucketsText(), Is.EqualTo(one.BucketsText()));
            Assert.That(Enumerable.Range(0, merged.Hitches).Select(merged.HitchMs), Is.EqualTo(new double[] { 90, 40, 17, 12, 7 }));

            merged.Clear();
            Assert.That(merged.Frames, Is.EqualTo(0));
            Assert.That(merged.Hitches, Is.EqualTo(0));
            Assert.That(merged.BucketsText(), Is.Empty);
            Assert.That(merged.MaxMs, Is.EqualTo(0));
            merged.Add(1);
            Assert.That(merged.MinMs, Is.EqualTo(1), "the fastest starts again");
        }

        [Test]
        public void TheMainThreadAndGpu_AreCountedOnlyWhereGiven()
        {
            var h = new FrameHistogram();
            h.AddTimings(4, 6);
            h.AddTimings(6, 0); // no GPU time that frame
            h.AddTimings(0, 0);
            Assert.That(h.MainFrames, Is.EqualTo(2));
            Assert.That(h.MainMeanMs, Is.EqualTo(5));
            Assert.That(h.GpuFrames, Is.EqualTo(1));
            Assert.That(h.GpuMeanMs, Is.EqualTo(6));
        }

        // ---------------- What counts as time in play ----------------

        [Test]
        public void TimeInPlay_IsNotThePause_AnOffer_AScreen_AnUnfocusedWindow_OrNoCharacter()
        {
            Assert.That(FramePlayRule.InPlay(true, true, false, false, false), Is.True);
            Assert.That(FramePlayRule.InPlay(true, true, true, false, false), Is.False, "the pause menu");
            Assert.That(FramePlayRule.InPlay(true, true, false, true, false), Is.False, "an offer: a reward, a chest, an upgrade, the focus choice");
            Assert.That(FramePlayRule.InPlay(true, true, false, false, true), Is.False, "the end screen, the screen between runs");
            Assert.That(FramePlayRule.InPlay(true, false, false, false, false), Is.False, "a window without focus");
            Assert.That(FramePlayRule.InPlay(false, true, false, false, false), Is.False, "no character");
        }

        [Test]
        public void AFrameCounts_WhenInPlayAsItBeganOrNow_ButNeverAcrossAGap()
        {
            Assert.That(FramePlayRule.Counts(true, true, false), Is.True);
            Assert.That(FramePlayRule.Counts(true, false, false), Is.True, "the frame that opened an offer");
            Assert.That(FramePlayRule.Counts(false, true, false), Is.True, "the frame that closed it and built the next floor");
            Assert.That(FramePlayRule.Counts(false, false, false), Is.False, "wholly under a screen");
            Assert.That(FramePlayRule.Counts(true, true, true), Is.False, "focus lost or back, the Editor paused");
        }

        // ---------------- The log ----------------

        [Test]
        public void APartsEvents_ReadBackFromTheLog_GiveTheSameNumbers()
        {
            FrameHistogram h = Of(16.2, 7.1, 7.3, 6.9, 41.25, 7.0, 120);
            h.AddTimings(5.5, 3.25);
            SessionEvent written = FrameTimeEvents.TimesEvent(12.5, h, FrameTimeEvents.PartFloor, 2, 3);
            SessionEvent read = SessionEvent.Parse(written.ToJson());
            Assert.That(read.Type, Is.EqualTo(FrameTimeEvents.Times));
            Assert.That(read.ExtraValue("part"), Is.EqualTo("floor"));
            FrameHistogram back = FrameTimeEvents.Read(read);
            Assert.That(back.Frames, Is.EqualTo(h.Frames));
            Assert.That(back.SumMs, Is.EqualTo(h.SumMs).Within(1e-3));
            Assert.That(back.MinMs, Is.EqualTo(h.MinMs).Within(1e-3));
            Assert.That(back.MaxMs, Is.EqualTo(120));
            Assert.That(back.Over60, Is.EqualTo(h.Over60));
            Assert.That(back.Over30, Is.EqualTo(h.Over30));
            Assert.That(back.MainMeanMs, Is.EqualTo(5.5));
            Assert.That(back.GpuMeanMs, Is.EqualTo(3.25));
            Assert.That(back.BucketsText(), Is.EqualTo(h.BucketsText()));
            Assert.That(back.Percentile(0.95), Is.EqualTo(h.Percentile(0.95)));
            Assert.That(h.BucketsText(), Is.EqualTo("27:1,28:2,29:1,64:1,165:1,400:1"));

            SessionEvent hitch = SessionEvent.Parse(FrameTimeEvents.HitchEvent(12.5, h, 0, FrameTimeEvents.PartFloor, 2, 3).ToJson());
            Assert.That(hitch.Type, Is.EqualTo(FrameTimeEvents.Hitch));
            Assert.That(hitch.ExtraValue("rank"), Is.EqualTo("1"));
            Assert.That(hitch.ExtraValue("ms"), Is.EqualTo("120"));
            Assert.That(hitch.ExtraValue("at"), Is.EqualTo("6.000"), "seen at the seventh frame's time");
            Assert.That(hitch.ExtraValue("into"), Is.EqualTo("3.000"));
            Assert.That(hitch.ExtraValue("run"), Is.EqualTo("2"));
            Assert.That(hitch.ExtraValue("floor"), Is.EqualTo("3"));
        }

        // ---------------- The summary's block ----------------

        static SessionEvent E(double t, string type, string name = null) => new SessionEvent(t, type, name);

        static SessionEvent Setup()
        {
            var e = E(0.02, FrameTimeEvents.Setup);
            e.AddExtra("ran_in", "editor");
            e.AddExtra("refresh_hz", "144");
            e.AddExtra("vsync", "0");
            e.AddExtra("target_fps", "-1");
            e.AddExtra("frame_timing", "true");
            e.AddExtra("bucket_ms", "0.25");
            return e;
        }

        static IEnumerable<SessionEvent> Part(double t, FrameHistogram h, string part, int run, int floor)
        {
            yield return FrameTimeEvents.TimesEvent(t, h, part, run, floor);
            for (int rank = 0; rank < h.Hitches; rank++) yield return FrameTimeEvents.HitchEvent(t, h, rank, part, run, floor);
        }

        /// <summary>A practice room, a floor of a run, the practice room again: frames, and slow ones where things happened.</summary>
        static List<SessionEvent> ASession()
        {
            var events = new List<SessionEvent> { E(0, SessionEventTypes.SessionStart), Setup() };
            var practice = new FrameHistogram();
            for (int i = 0; i < 600; i++) practice.Add(6.9, 0.1 + i * 0.007, 0.1 + i * 0.007);
            practice.Add(30, 2.0, 2.0); // nothing happened in the half second before it
            events.Add(E(4.0, SessionEventTypes.ShotStart, SessionWords.BasicShot));
            practice.Add(25, 4.1, 4.1); // the first shot of the session, a tenth of a second before
            events.Add(E(10, SessionEventTypes.RunStart, "R1"));
            events.AddRange(Part(10, practice, FrameTimeEvents.PartPracticeRoom, 0, 0));
            events.Add(E(10, SessionEventTypes.LevelBuilt, "S1"));
            events.Add(E(10, SessionEventTypes.FloorEnter, "S1"));

            var floor = new FrameHistogram();
            for (int i = 0; i < 1000; i++) floor.Add(i % 10 == 0 ? 18 : 8, 10.2 + i * 0.01, 0.2 + i * 0.01);
            floor.Add(64, 10.08, 0.08); // the run started, the floor built and the first room entered
            events.Add(E(10.02, SessionEventTypes.RoomEntered, "start"));
            for (int i = 0; i < 3; i++) events.Add(E(20 + i * 0.01, SessionEventTypes.EnemyWake));
            floor.Add(40, 20.3, 10.3);   // three enemies woke
            events.Add(E(30, SessionEventTypes.ShotStart, SessionWords.BasicShot));
            floor.Add(35, 30.1, 20.1);   // the first shot of this floor, not of the session
            events.Add(E(50, SessionEventTypes.ShotStart, SessionWords.BasicShot));
            floor.Add(34, 50.2, 40.2);   // a shot like any other: nothing
            events.AddRange(Part(60, floor, FrameTimeEvents.PartFloor, 1, 1));
            events.Add(new SessionEvent(60, SessionEventTypes.RunEnd) { Result = "death" });

            var again = new FrameHistogram();
            for (int i = 0; i < 100; i++) again.Add(7.5, 61 + i * 0.01, 1 + i * 0.01);
            again.Add(26, 61.5, 1.5);    // under the practice room's first stretch's slowest five, but above its third
            events.AddRange(Part(70, again, FrameTimeEvents.PartPracticeRoom, 0, 0));
            events.Add(E(70, SessionEventTypes.SessionEnd));
            return events;
        }

        [Test]
        public void TheSummary_HasARowPerPart_ThePracticeRoomAsOne_AndTheWholeSession()
        {
            FrameTimesStats s = SessionSummary.Compute(ASession()).Frames;
            Assert.That(s.Any, Is.True);
            Assert.That(s.Parts.Select(p => p.Label), Is.EqualTo(new[] { "Practice room", "Run 1, floor 1" }));
            Assert.That(s.RanIn, Is.EqualTo("editor"));
            Assert.That(s.RefreshHz, Is.EqualTo(144));
            Assert.That(s.VSync, Is.EqualTo(0));
            FramePartRow practice = s.Part("Practice room");
            Assert.That(practice.Frames.Frames, Is.EqualTo(602 + 101), "both stretches");
            Assert.That(s.Session.Frames, Is.EqualTo(602 + 101 + 1004));
            Assert.That(s.Session.MaxMs, Is.EqualTo(64));
            Assert.That(s.Session.Over60, Is.EqualTo(100 + 4 + 3), "the floor's tenth frames, its four slow ones, and three in the practice room");
            Assert.That(practice.Hitches.Select(h => h.Ms), Is.EqualTo(new double[] { 30, 26, 25, 7.5, 7.5 }), "the five slowest of both stretches");
            Assert.That(s.Session.MeanMs, Is.GreaterThan(s.Session.MinMs).And.LessThan(s.Session.MaxMs));
            Assert.That(s.Session.Percentile(0.95), Is.LessThanOrEqualTo(s.Session.Percentile(0.99)));
        }

        [Test]
        public void EachHitch_SaysWhatTheRecorderSawInTheHalfSecondBefore()
        {
            FrameTimesStats s = SessionSummary.Compute(ASession()).Frames;
            List<FrameHitchRow> floor = s.Part("Run 1, floor 1").Hitches;
            Assert.That(floor.Select(h => h.Ms), Is.EqualTo(new double[] { 64, 40, 35, 34, 18 }));
            Assert.That(floor[0].Causes, Is.EqualTo("run started; floor built; room entered (start)"));
            Assert.That(floor[0].Into, Is.EqualTo(0.08).Within(1e-9));
            Assert.That(floor[1].Causes, Is.EqualTo("enemy woke (x3)"), "woke 0.3 s before: inside the half second");
            Assert.That(floor[2].Causes, Is.EqualTo("first Basic shot on this floor"));
            Assert.That(floor[3].Causes, Is.Empty, "a shot like any other");
            List<FrameHitchRow> practice = s.Part("Practice room").Hitches;
            Assert.That(practice[0].Causes, Is.Empty, "the session started two seconds before");
            Assert.That(practice[2].Causes, Is.EqualTo("first Basic shot of the session"));
        }

        [Test]
        public void TheBlock_ReadsAsTheWorkOrderAsks_UnderEveryLineBefore_AndTheSameFromTheLogReadBack()
        {
            List<SessionEvent> events = ASession();
            string text = SessionSummary.Format(SessionSummary.Compute(events));
            TestContext.Out.WriteLine(text);
            int block = text.IndexOf("Frame times, in play only", System.StringComparison.Ordinal);
            Assert.That(block, Is.GreaterThan(0));
            Assert.That(text.IndexOf("Defence", System.StringComparison.Ordinal), Is.LessThan(block), "under the existing lines");
            string mine = text.Substring(block);
            StringAssert.Contains("ran in the Editor   display 144 Hz   vsync off   target fps default", mine);
            StringAssert.Contains("0.25 ms steps", mine);
            StringAssert.Contains("Frames", mine);
            StringAssert.Contains("Over 16.7", mine);
            StringAssert.Contains("Over 33.3", mine);
            StringAssert.Contains("Practice room", mine);
            StringAssert.Contains("Run 1, floor 1", mine);
            StringAssert.Contains("Whole session", mine);
            StringAssert.Contains("Hitches: the 5 slowest frames of each", mine);
            StringAssert.Contains("1. 64.00 ms, 0.1 s into it (log 10.080 s): run started; floor built; room entered (start)", mine);
            StringAssert.Contains("nothing the recorder saw", mine);
            string row = mine.Split('\n').First(l => l.StartsWith("Run 1, floor 1"));
            StringAssert.Contains(" 1004 ", row);
            StringAssert.Contains(" 64.00 ", row);

            List<SessionEvent> back = events.Select(e => SessionEvent.Parse(e.ToJson())).ToList();
            Assert.That(SessionSummary.Format(SessionSummary.Compute(back)), Is.EqualTo(text), "the same from the log's lines");
        }

        [Test]
        public void ALogWithoutFrameTimes_HasNoBlock()
        {
            var events = new List<SessionEvent> { E(0, SessionEventTypes.SessionStart), E(4, SessionEventTypes.ShotStart, SessionWords.BasicShot), E(9, SessionEventTypes.SessionEnd) };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Frames.Any, Is.False);
            string text = SessionSummary.Format(stats);
            StringAssert.DoesNotContain("Frame times", text);
            StringAssert.DoesNotContain("Hitches", text);
        }
    }
}
