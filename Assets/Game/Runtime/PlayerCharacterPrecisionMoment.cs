using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 152, the shot's cone the same at every frame rate (<see cref="PrecisionValues.SameAtEveryFrameRate"/>; the grips'
    /// analyst's finding of 2026-11-27): her side of <see cref="PrecisionMoment"/>. A frame's work runs the law over the step after
    /// the frame's moment. With the switch on:
    /// - the law follows the speed the frame's limits give at each of its small steps (<see cref="CapCurveThisFrame"/>: the moving
    ///   shot's ease, a skill's slide, the rise after one, at the step's own moment), not the frame's value held through the step;
    /// - a basic shot's wind-up is watched on the law's time line (<see cref="_watch"/>, from <see cref="PrecisionShotStarted"/>):
    ///   the lowest share at every small step and at the very moment the wind-up ends, wherever it falls in a frame; the shot reads
    ///   that (<see cref="DeflectBasicShot"/>);
    /// - a counter-tap that empties the meter counts at its instant.
    /// Off, none of it: work order 143's frame, number for number. Nothing is allocated in a frame (she is the curve the law asks).
    /// </summary>
    public sealed partial class PlayerCharacter : IPrecisionCurve
    {
        WindUpWatch _watch;
        FeelValues _capFeel;
        float _capClock, _capShareNow;

        /// <summary>Work order 152's switch.</summary>
        static bool SameAtEveryFrameRate => PrecisionValues.SameAtEveryFrameRate;

        /// <summary>
        /// The law's curve for this frame's step: the cap's share at a moment in the step against the frame's own, when MoveAlong
        /// asked for a way this frame (its speed is the frame's cap); null otherwise (no key: she coasts, or the switch is off).
        /// </summary>
        IPrecisionCurve CapCurveThisFrame()
        {
            if (!SameAtEveryFrameRate || !_wishThisFrame) return null;
            _capFeel = Feel;
            _capClock = Time.time;
            _capShareNow = CurrentSpeedShare(_capFeel, _capClock);
            return this;
        }

        /// <summary>The share of the frame's cap the limits give <paramref name="seconds"/> into the step (1 when nothing eases; held when the frame's is none).</summary>
        float IPrecisionCurve.ShareAt(float seconds) =>
            _capShareNow > MinCapShare ? CurrentSpeedShare(_capFeel, _capClock + seconds) / _capShareNow : 1f;

        /// <summary>Under this share of run speed the frame's cap is held through its step (a ratio to nothing is no curve).</summary>
        const float MinCapShare = 1e-4f;

        /// <summary>
        /// A press: the wind-up is watched from the share she has for its startup's length. A press from the input (its driver runs
        /// before her frame) or from her own frame comes before the frame's step: the share is the one at the frame's moment. A press
        /// made after her frame (a test's or a tool's) finds the step already run: that step is already part of its wind-up.
        /// </summary>
        void WatchWindUp()
        {
            if (!SameAtEveryFrameRate)
            {
                _watch.Stop();
                return;
            }
            float alreadyRun = _lawSteppedAt == Time.frameCount ? Time.deltaTime : 0f;
            _watch.Start(_precisionShare, ActionLeadTime - alreadyRun);
        }

        /// <summary>The frame whose step the law last ran (PrecisionAfter).</summary>
        int _lawSteppedAt = -1;

        /// <summary>The share a basic shot that goes off now reads with the switch on: its wind-up's lowest; false when nothing was watched.</summary>
        bool WatchedShare(out float share)
        {
            share = _precisionShare;
            bool watched = SameAtEveryFrameRate && _watch.Open;
            if (watched) share = _watch.Reached ? _watch.Lowest : Mathf.Min(_watch.Lowest, _precisionShare);
            _watch.Stop();
            return watched;
        }
    }
}
