using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using AllocExt = UnityEngine.TestTools.Constraints.ConstraintExtensions;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 074, the frame times in the play session's log and summary, in the ControlRoom scene: a short recorded
    /// session writes the block with sane numbers; the pause menu and an open offer are not counted; a floor of a run is
    /// a part of its own; measuring a frame allocates nothing; and nothing is left in a PlaySessions folder (the
    /// recorder writes into a temporary folder, removed after each test).
    ///
    /// Frames without a window are about 0.3 ms: no assertion here depends on a frame's length.
    /// </summary>
    public class FrameTimesPlayTests
    {
        const ulong RunSeed = 0x0740074UL;
        const int MostFrames = 600;

        PlayerCharacter _player;
        GeneratedLevels _levels;
        string _folder;
        PlaySessionRecorder _recorder;
        static byte[] s_junk;

        /// <summary>The test framework's measure of no GC allocation (its extension, called by name: its own Is would clash with NUnit's).</summary>
        static NUnit.Framework.Constraints.IResolveConstraint NotAllocating => AllocExt.AllocatingGCMemory(Is.Not);

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null; // the real mouse and keyboard must not steer a test
            _levels = GeneratedLevels.Ensure();
            _folder = Path.Combine(Path.GetTempPath(), "FrameTimesPlayTests-" + Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static List<SessionEvent> Of(IEnumerable<SessionEvent> events, string type) => events.Where(e => e.Type == type).ToList();

        [UnityTest]
        public IEnumerator AShortRecordedSession_WritesTheBlock_WithSaneNumbers_TheSameFromItsLog()
        {
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            Assert.That(_recorder.Player, Is.SameAs(_player));
            _player.Steer(Vector3.back);
            yield return Frames(40);
            _player.Stop();
            yield return Frames(20);
            Assert.That(_recorder.FramesCounted, Is.GreaterThan(0), "frames counted while recording");

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            Assert.That(events.Select(e => e.T), Is.Ordered, "the log stays in order of time");
            Assert.That(events[events.Count - 1].Type, Is.EqualTo(SessionEventTypes.SessionEnd), "the session's end is still the last line");
            SessionEvent setup = Of(events, FrameTimeEvents.Setup).Single();
            Assert.That(setup.ExtraValue("ran_in"), Is.EqualTo(Application.isEditor ? "editor" : "player"));
            Assert.That(setup.ExtraValue("vsync"), Is.EqualTo(QualitySettings.vSyncCount.ToString()));
            Assert.That(setup.ExtraValue("refresh_hz"), Is.Not.Null);
            Assert.That(setup.ExtraValue("bucket_ms"), Is.EqualTo("0.25"));

            SessionEvent times = Of(events, FrameTimeEvents.Times).Single();
            Assert.That(times.ExtraValue("part"), Is.EqualTo(FrameTimeEvents.PartPracticeRoom));
            FrameHistogram h = FrameTimeEvents.Read(times);
            Assert.That(h.Frames, Is.GreaterThan(0));
            Assert.That(h.Frames, Is.LessThanOrEqualTo(61), "no more than the frames that went by");
            Assert.That(h.MeanMs, Is.GreaterThanOrEqualTo(h.MinMs - 1e-3).And.LessThanOrEqualTo(h.MaxMs + 1e-3), "the mean between the fastest and the slowest");
            double p95 = h.Percentile(0.95), p99 = h.Percentile(0.99);
            Assert.That(p95, Is.LessThanOrEqualTo(p99), "the percentiles in order");
            Assert.That(p99, Is.LessThanOrEqualTo(h.MaxMs + 1e-3));
            Assert.That(p95, Is.GreaterThanOrEqualTo(h.MinMs - 1e-3));
            Assert.That(h.Over30, Is.LessThanOrEqualTo(h.Over60));

            List<SessionEvent> hitches = Of(events, FrameTimeEvents.Hitch);
            Assert.That(hitches.Count, Is.EqualTo(Math.Min(h.Frames, FrameTimeEvents.HitchesPerPart)));
            List<double> ms = hitches.Select(e => double.Parse(e.ExtraValue("ms"), System.Globalization.CultureInfo.InvariantCulture)).ToList();
            Assert.That(ms, Is.Ordered.Descending, "slowest first");
            Assert.That(ms[0], Is.EqualTo(h.MaxMs).Within(1e-3), "the first is the slowest frame");

            string summaryPath = SessionSummary.SummaryPathFor(log);
            string summary = File.ReadAllText(summaryPath);
            TestContext.Out.WriteLine(summary.Substring(Math.Max(0, summary.IndexOf("Frame times", StringComparison.Ordinal))));
            StringAssert.Contains("Frame times, in play only", summary);
            StringAssert.Contains(Application.isEditor ? "ran in the Editor" : "ran in a built player", summary);
            StringAssert.Contains("Practice room", summary);
            StringAssert.Contains("Whole session", summary);
            Assert.That(summary.IndexOf("Frame times", StringComparison.Ordinal), Is.GreaterThan(summary.IndexOf("Defence", StringComparison.Ordinal)),
                        "under every line that was there");
            Assert.That(SessionSummary.WriteFromLog(log), Is.EqualTo(summaryPath));
            Assert.That(File.ReadAllText(summaryPath), Is.EqualTo(summary), "recomputed from the log alone, the same");
        }

        [UnityTest]
        public IEnumerator ThePauseMenu_AndAnOpenOffer_AreNotCounted_AndAFloorIsAPartOfItsOwn()
        {
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return Frames(10);
            int before = _recorder.FramesCounted;
            Assume.That(before, Is.GreaterThan(0));

            Assert.That(_levels.Pause(), Is.True, "paused");
            int atPause = _recorder.FramesCounted;
            yield return Frames(30);
            Assert.That(_recorder.FramesCounted - atPause, Is.LessThanOrEqualTo(1), "only the frame the pause began in");
            Assert.That(_recorder.InPlayForFrames, Is.False);
            Assert.That(_levels.Resume(), Is.True);
            int atResume = _recorder.FramesCounted;
            yield return Frames(10);
            Assert.That(_recorder.FramesCounted - atResume, Is.GreaterThanOrEqualTo(8), "counted again");

            // A floor of a run: its own part; and the floor's reward offer is not counted.
            Assert.That(_levels.StartRun(RunSeed), Is.Not.Null);
            yield return Frames(10);
            Assert.That(_recorder.FramePart.Frames, Is.GreaterThan(0), "the floor's frames");
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
            Assume.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor reward is offered");
            yield return null;
            int atOffer = _recorder.FramesCounted;
            yield return Frames(30);
            Assert.That(_recorder.FramesCounted - atOffer, Is.LessThanOrEqualTo(1), "not while the offer is up");

            string log = _recorder.LogPath;
            _recorder.StopRecording();
            _recorder = null;

            List<SessionEvent> events = SessionSummary.ReadLog(log);
            List<SessionEvent> parts = Of(events, FrameTimeEvents.Times);
            Assert.That(parts.Select(e => e.ExtraValue("part")), Is.EqualTo(new[] { FrameTimeEvents.PartPracticeRoom, FrameTimeEvents.PartFloor }));
            Assert.That(parts[1].ExtraValue("run"), Is.EqualTo("1"));
            Assert.That(parts[1].ExtraValue("floor"), Is.EqualTo("1"));
            int counted = parts.Sum(e => FrameTimeEvents.Read(e).Frames);
            Assert.That(counted, Is.LessThan(10 + 30 + 10 + 10 + MostFrames + 30 + 5), "fewer than the frames that went by");
            Assert.That(Of(events, FrameTimeEvents.Hitch).Count(e => e.ExtraValue("part") == FrameTimeEvents.PartFloor),
                        Is.InRange(1, FrameTimeEvents.HitchesPerPart), "the floor's slowest frames");
            Assert.That(Of(events, FrameTimeEvents.Times).Last().T, Is.LessThan(Of(events, SessionEventTypes.SessionEnd).Single().T + 1e-6));
            StringAssert.Contains("Run 1, floor 1", File.ReadAllText(SessionSummary.SummaryPathFor(log)));
        }

        [UnityTest]
        public IEnumerator WhileRecording_MeasuringAFrameAllocatesNothing()
        {
            Assert.That(() => { s_junk = new byte[64]; }, AllocIs.AllocatingGCMemory(), "the measure sees an allocation");
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return Frames(3); // the frame setup's line is written once, at the first frame
            Assume.That(_recorder.InPlayForFrames, Is.True);
            int before = _recorder.FramesCounted;
            Assert.That(() => { for (int i = 0; i < 300; i++) _recorder.MeasureFrame(0.004f + (i % 9) * 0.003f, false); }, NotAllocating,
                        "a frame's measure allocates nothing");
            Assert.That(_recorder.FramesCounted - before, Is.EqualTo(300), "and they were counted");
            Assert.That(_recorder.FramePart.Hitches, Is.EqualTo(FrameTimeEvents.HitchesPerPart));
            Assert.That(_recorder.FramePart.MaxMs, Is.GreaterThanOrEqualTo(28.0 - 1e-3));
        }

        [UnityTest]
        public IEnumerator NothingIsWrittenIntoThePlaySessionsFolder()
        {
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return Frames(5);
            _recorder.StopRecording();
            _recorder = null;
            string folder = PlaySessionRecorder.DefaultFolder();
            if (Directory.Exists(folder))
            {
                DateTime since = System.Diagnostics.Process.GetCurrentProcess().StartTime;
                Assert.That(Directory.GetFiles(folder).Where(f => File.GetLastWriteTime(f) >= since), Is.Empty, "files written into " + folder);
            }
        }
    }
}
