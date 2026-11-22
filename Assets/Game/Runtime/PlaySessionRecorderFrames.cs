using System;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The frame times of a play session (work order 074, 2026-11-21: "Hva ligger fps på i play?"), as part of the
    /// recorder: every frame's unscaled time while she is in play (<see cref="FramePlayRule"/>: not under the pause menu,
    /// an offer, the end screen or the screen between runs, not in a window without focus), into a fixed
    /// <see cref="FrameHistogram"/> of the part she is in, a floor of a run or the practice room. Nothing is allocated per
    /// frame. When the part ends (a floor entered, the run ended, the session ended) its numbers and its five slowest
    /// frames go to the log (<see cref="FrameTimeEvents"/>), and the summary's block is computed from them
    /// (<see cref="FrameTimesSummary"/>). The main thread's and the GPU's times are the FrameTimingManager's, as the
    /// benchmark reads them, where it gives them. A session with the recorder off has none of this.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        readonly FrameHistogram _framePart = new FrameHistogram();
        readonly FrameTiming[] _frameTimings = new FrameTiming[1];
        bool _frameSetupWritten;
        bool _frameTimingOn;
        bool _inPlayBefore;
        int _lastFrameSeen = int.MinValue;
        double _framePartStart;           // the log's clock (seconds since the session started) when the part began
        string _framePartKind = FrameTimeEvents.PartPracticeRoom;
        int _framePartRun, _framePartFloor;
        int _frameRuns;                   // runs started in this session, so a floor's part says which run
        int _framesCounted;               // every part's, the whole session
        /// <summary>The window's focus changed, or the Editor's pause: the next frame's length is not the game's.</summary>
        static bool s_frameGap;

        /// <summary>The frames of the part going on (work order 074), not yet written.</summary>
        public FrameHistogram FramePart => _framePart;
        /// <summary>Frames counted in the whole session so far.</summary>
        public int FramesCounted => _framesCounted;
        /// <summary>Whether the game was in play at the last frame measured.</summary>
        public bool InPlayForFrames => _inPlayBefore;

        void OnApplicationFocus(bool focused) => s_frameGap = true;
        void OnApplicationPause(bool paused) => s_frameGap = true;

#if UNITY_EDITOR
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void WatchTheEditorsPause()
        {
            s_frameGap = false;
            UnityEditor.EditorApplication.pauseStateChanged -= OnEditorPause;
            UnityEditor.EditorApplication.pauseStateChanged += OnEditorPause;
        }

        static void OnEditorPause(UnityEditor.PauseState state) => s_frameGap = true;
#endif

        /// <summary>Once a frame, from Update, while a character is recorded: the frame that just ended, if it counts.</summary>
        void MeasureFrame()
        {
            int frame = Time.frameCount;
            bool gap = s_frameGap || frame != _lastFrameSeen + 1; // frames not seen (no character, a failed look) are a gap too
            _lastFrameSeen = frame;
            s_frameGap = false;
            MeasureFrame(Time.unscaledDeltaTime, gap);
        }

        /// <summary>
        /// A frame of <paramref name="unscaledSeconds"/> that just ended, counted when the game was in play as it began or
        /// is now and it does not span a <paramref name="gap"/>. Allocates nothing (but the frame setup's line, once).
        /// Public for the tests, which give it frames of their own.
        /// </summary>
        public void MeasureFrame(float unscaledSeconds, bool gap)
        {
            if (!_recording || _writer == null) return;
            if (!_frameSetupWritten) WriteFrameSetup();
            if (_frameTimingOn) FrameTimingManager.CaptureFrameTimings(); // every frame, as the benchmark does, to keep its history going

            GeneratedLevels levels = GeneratedLevels.Current;
            bool there = levels != null;
            bool inPlay = FramePlayRule.InPlay(_player != null, Application.isFocused || Application.isBatchMode,
                                               there && levels.Paused,
                                               there && levels.Choice != GeneratedLevels.ChoiceKind.None,
                                               there && (levels.BetweenRunsUp || levels.Ending != GeneratedLevels.EndKind.None));
            if (FramePlayRule.Counts(_inPlayBefore, inPlay, gap))
            {
                double at = (double)(Time.time - _startTime); // the log's clock, as Now() reads it, to the microsecond
                _framePart.Add(unscaledSeconds * 1000.0, at, at - _framePartStart);
                _framesCounted++;
                if (_frameTimingOn && FrameTimingManager.GetLatestTimings(1, _frameTimings) > 0)
                    _framePart.AddTimings(_frameTimings[0].cpuMainThreadFrameTime, _frameTimings[0].gpuFrameTime);
            }
            _inPlayBefore = inPlay;
        }

        /// <summary>Once, at the first frame: where it runs, the display, vsync, and whether the main thread's and GPU's times are given.</summary>
        void WriteFrameSetup()
        {
            _frameSetupWritten = true;
            _frameTimingOn = FrameTimingManager.IsFeatureEnabled();
            var e = new SessionEvent(Now(), FrameTimeEvents.Setup);
            e.AddExtra("ran_in", Application.isEditor ? "editor" : "player");
            e.AddExtra("refresh_hz", Screen.currentResolution.refreshRateRatio.value.ToString("0.##", CultureInfo.InvariantCulture));
            e.AddExtra("vsync", QualitySettings.vSyncCount.ToString(CultureInfo.InvariantCulture));
            e.AddExtra("target_fps", Application.targetFrameRate.ToString(CultureInfo.InvariantCulture));
            e.AddExtra("frame_timing", _frameTimingOn ? "true" : "false");
            e.AddExtra("bucket_ms", FrameHistogram.BucketMs.ToString("0.##", CultureInfo.InvariantCulture));
            Add(e);
            // A recorder started during a run (a test's, a tool's) begins on a floor whose run it did not see start.
            GeneratedLevels levels = GeneratedLevels.Current;
            if (levels != null && levels.InRun && _framePart.Frames == 0)
            {
                _framePartKind = FrameTimeEvents.PartFloor;
                _framePartFloor = levels.Floor;
            }
        }

        /// <summary>
        /// The part going on ends: its frames and its slowest frames to the log (nothing for a part with no frame in play),
        /// and the next part begins, now.
        /// </summary>
        void EndFramePart(string nextKind, int nextRun, int nextFloor)
        {
            if (_recording && _writer != null && _framePart.Frames > 0)
            {
                try
                {
                    double t = Now();
                    Add(FrameTimeEvents.TimesEvent(t, _framePart, _framePartKind, _framePartRun, _framePartFloor));
                    for (int rank = 0; rank < _framePart.Hitches; rank++)
                        Add(FrameTimeEvents.HitchEvent(t, _framePart, rank, _framePartKind, _framePartRun, _framePartFloor));
                }
                catch (Exception e)
                {
                    Fail(e);
                }
            }
            _framePart.Clear();
            _framePartKind = nextKind;
            _framePartRun = nextRun;
            _framePartFloor = nextFloor;
            _framePartStart = (double)(Time.time - _startTime);
        }
    }
}
