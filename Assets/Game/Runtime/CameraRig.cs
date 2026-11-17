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

        readonly FeelValues _fallbackFeel = new FeelValues();
        Camera _camera;

        void Awake() => _camera = GetComponent<Camera>();

        void LateUpdate()
        {
            if (Target == null) return;
            FeelValues feel = Settings != null ? Settings.Values : _fallbackFeel;

            _camera.fieldOfView = feel.FieldOfView;
            Quaternion rotation = Quaternion.Euler(feel.CameraPitch, 0f, 0f);
            Vector3 focus = new Vector3(Target.position.x, 0f, Target.position.z);
            transform.SetPositionAndRotation(focus - rotation * Vector3.forward * feel.CameraDistance, rotation);
        }
    }
}
