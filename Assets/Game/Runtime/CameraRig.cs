using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>Fixed-angle follow camera with the feel-spec pitch, distance and field of view, read live.</summary>
    [RequireComponent(typeof(Camera))]
    public sealed class CameraRig : MonoBehaviour
    {
        public Transform Target;
        public FeelSettings Settings;
        /// <summary>Work order 130: while the boss's entrance plays it moves this rig's focus and distance (its pitch and field of view stay); null, the play camera.</summary>
        [System.NonSerialized] public ICameraShot Shot;

        readonly FeelValues _fallbackFeel = new FeelValues();
        Camera _camera;

        void Awake() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (Target == null) return;
            FeelValues feel = Settings != null ? Settings.Values : _fallbackFeel;

            // Work order 121: the pause menu's camera angle (ours, the default: the feel's own pitch and field of view, number for
            // number as before); work order 083: the distance the pause menu's step gives (the feel's own at step 0, the default).
            CameraAngle angle = GameSettings.CameraNow(feel);
            _camera.fieldOfView = angle.FieldOfView;
            Quaternion rotation = Quaternion.Euler(angle.Pitch, 0f, 0f);
            Vector3 focus = new Vector3(Target.position.x, 0f, Target.position.z);
            float distance = angle.Distance;
            Shot?.Frame(ref focus, ref distance); // work order 130
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * distance, rotation);
        }
    }
}
