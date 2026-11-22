using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's camera distance (work order 083; Kristian, 2026-11-22, on the cave's pictures: "Er kameraet mer zoomet
    /// inn og veggene høyere?"), as part of <see cref="RunHud"/>: a line beside the camera's kick that steps the distance
    /// 28, 25, 22 m and back (<see cref="CameraDistanceSteps"/>), kept in PlayerPrefs as the kick is
    /// (<see cref="GameSettings.CameraDistanceStep"/>), so that he can feel "more zoomed in" himself. The camera moves at once
    /// (<see cref="CameraRig"/> reads it every frame); how high a floor's far walls stand is worked out for the distance in use
    /// when the floor is built (<see cref="CaveView"/>). The default stays the feel's own 28 m.
    /// </summary>
    public sealed partial class RunHud
    {
        string _distanceText;
        int _distanceTextStep = -1;
        float _distanceTextFeel = -1f;
        CameraRig _distanceRig;

        /// <summary>The feel's own distance (the asset's, or the code's default): what step 0 is.</summary>
        float FeelDistance
        {
            get
            {
                if (_distanceRig == null) _distanceRig = FindAnyObjectByType<CameraRig>();
                return _distanceRig != null && _distanceRig.Settings != null ? _distanceRig.Settings.Values.CameraDistance : new FeelValues().CameraDistance;
            }
        }

        /// <summary>The line's words, made again only when the step or the feel's distance changes.</summary>
        string CameraDistanceText
        {
            get
            {
                int step = GameSettings.CameraDistanceStep;
                float feel = FeelDistance;
                if (_distanceText == null || step != _distanceTextStep || feel != _distanceTextFeel)
                {
                    _distanceTextStep = step;
                    _distanceTextFeel = feel;
                    float m = CameraDistanceSteps.Distance(step, feel);
                    _distanceText = string.Format(CultureInfo.InvariantCulture, "Camera distance:  {0:0.#} m{1}", m, step == 0 ? " (standard)" : "");
                }
                return _distanceText;
            }
        }

        /// <summary>One step on (Enter, a click, the right arrow) or back (the left arrow), and kept.</summary>
        void StepCameraDistance(int delta)
        {
            int step = GameSettings.CameraDistanceStep;
            GameSettings.SetCameraDistanceStep(delta < 0 ? CameraDistanceSteps.Previous(step) : CameraDistanceSteps.Next(step));
        }
    }
}
