using System;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 090: the real seconds a run stood still under a screen (the pause menu, a floor's reward, a chest, an elite room's
    /// upgrade, the focus choice), so the session summary can say where a run's time went (<see cref="SessionSummary"/>). The game's
    /// clock stops under them (the time scale is 0), and so does the log's, which counts game time: these seconds are the frames'
    /// real time, added up from the frame the screen came up to the frame it went, and told once as it goes
    /// (<see cref="SessionEventTypes.Screen"/>), with the run and the floor it came up on. Only in a run (not the practice room,
    /// not the room lab, not the end screen or the screen between runs, which come after a run). Nothing is allocated a frame:
    /// a few numbers kept, and the event made once when the screen goes.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        string _screen;          // what is up now, or null
        int _screenRun, _screenFloor;
        double _screenSeconds;

        /// <summary>Seconds of the screen up now so far (0 with none). For tests.</summary>
        public double ScreenSecondsSoFar => _screen != null ? _screenSeconds : 0d;

        /// <summary>What holds a run still now, in the log's words, or null (none, or not in a run).</summary>
        static string ScreenNow(GeneratedLevels levels)
        {
            if (levels == null || !levels.InRun || levels.InLab) return null;
            switch (levels.Choice)
            {
                case GeneratedLevels.ChoiceKind.FloorReward: return "floor reward";
                case GeneratedLevels.ChoiceKind.Chest: return "chest";
                case GeneratedLevels.ChoiceKind.SkillUpgrade: return "skill upgrade";
                case GeneratedLevels.ChoiceKind.FocusChoice: return "focus choice";
            }
            return levels.Paused ? "pause menu" : null;
        }

        /// <summary>
        /// Once a frame from <see cref="MeasureFrame(float, bool)"/>: the frame that just ended counted to the screen that was up
        /// (not a frame that spans a gap, the window's focus lost or the Editor's pause); the screen told as it goes.
        /// </summary>
        void MeasureScreen(GeneratedLevels levels, float unscaledSeconds, bool gap)
        {
            string now = ScreenNow(levels);
            if (_screen != null && !gap) _screenSeconds += unscaledSeconds;
            if (ReferenceEquals(now, _screen)) return;
            EndScreen();
            if (now == null) return;
            _screen = now;
            _screenRun = _frameRuns;
            _screenFloor = levels.Floor;
            _screenSeconds = 0d;
        }

        /// <summary>The screen up now, if any, told with its seconds (as it goes, and when the session ends under it).</summary>
        void EndScreen()
        {
            if (_screen == null) return;
            string what = _screen;
            _screen = null;
            if (!_recording || _writer == null) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.Screen, what);
                e.AddExtra("run", Int(_screenRun));
                e.AddExtra("floor", Int(_screenFloor));
                e.AddExtra("seconds", _screenSeconds.ToString("0.###", CultureInfo.InvariantCulture));
                Add(e);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }
    }
}
