using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The camera angle (work order 121) in the log: his sessions must say which angle he played. The session's start writes the
    /// feel's own camera (feel.CameraPitch and the like) whatever angle is chosen, so one line (<see cref="CameraAngleEvents.Change"/>)
    /// says the angle in use: once the log is open, and each time the angle or the distance step changes after, with the angle's
    /// name and its three numbers (and the distance step and the distance the camera stands at). Asked once a frame, as her
    /// basic shot's interval is: two numbers compared, nothing allocated while nothing changes. Nothing with the order's switch off.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        int _angleWritten = -1, _angleStepWritten = -1;
        PlayerCharacter _angleFor;

        /// <summary>Once a frame from Update: the camera angle written when it is not the one last written for her.</summary>
        void NoteCameraAngle()
        {
            if (!_recording || _player == null || !CameraAngles.On) return;
            int angle = CameraAngles.Step(GameSettings.CameraAngleStep), step = GameSettings.CameraDistanceStep;
            if (angle == _angleWritten && step == _angleStepWritten && _player == _angleFor) return;
            _angleWritten = angle;
            _angleStepWritten = step;
            _angleFor = _player;
            FeelValues feel = _player.Feel ?? new FeelValues();
            CameraAngle own = CameraAngles.InUse(angle, 0, feel.CameraPitch, feel.FieldOfView, feel.CameraDistance);
            CameraAngle now = GameSettings.CameraNow(feel);
            Note(CameraAngleEvents.Change, own.Name, "pitch", Text(own.Pitch), "field_of_view", Text(own.FieldOfView),
                 "distance", Text(own.Distance), "distance_step", step.ToString(CultureInfo.InvariantCulture),
                 "distance_in_use", Text(now.Distance));
        }
    }
}
