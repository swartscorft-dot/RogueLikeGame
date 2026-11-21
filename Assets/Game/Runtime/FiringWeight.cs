using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The weight of a shot of hers going off (work order 064, Kristian, 2026-11-21: "savner også litt mer umf når man fyrer
    /// av, kanskje litt lysflash og trøkk"): a warm point light at the muzzle for a moment (real lights, pooled, never more
    /// than <see cref="FiringWeightValues.MaxLights"/>), a short kick of the camera back along the shot's line and a fast
    /// return (in real time, never adding up past <see cref="FiringWeightValues.MostKick"/>), and, read by her where the flash is
    /// made, a larger and shorter muzzle flash. The heavy skills' longer hit stop is read where the hit stop is set
    /// (<see cref="ValuesNow"/>).
    ///
    /// The kick never moves where the cursor aims: the camera is moved only for the moment it draws (from the start of its
    /// rendering to its end) and stands at the rig's place for everything else in the frame, the cursor's ray and the
    /// interface's places among them. The pause menu's "Camera kick" turns it off (<see cref="GameSettings.CameraKick"/>).
    ///
    /// It installs itself when Play starts under the same rule as <see cref="CombatEffects"/> (never in a test run, so never
    /// on a pilot's flight); tests make one with <see cref="Ensure"/>. It is off while the effects are switched off
    /// (<see cref="CombatFxValues.On"/>) and with its own switch (<see cref="FiringWeightValues.On"/>): the morning's shots.
    /// Presentation only: nothing here changes a hit. Nothing is allocated per shot or per frame once its lights are made.
    /// </summary>
    public sealed class FiringWeight : MonoBehaviour
    {
        /// <summary>The one installed now, or null.</summary>
        public static FiringWeight Current { get; private set; }

        /// <summary>The one installed, if it is on and the effects are not switched off; null otherwise.</summary>
        public static FiringWeight Running =>
            Current != null && Current.isActiveAndEnabled && Current.Values.On && (CombatEffects.Current == null || CombatEffects.Current.Values.On) ? Current : null;

        /// <summary>The values the shots read now: the running one's, or null (every shot as this morning).</summary>
        public static FiringWeightValues ValuesNow => Running != null ? Running.Values : null;

        /// <summary>The numbers, defaults in code (work order 064: first guesses).</summary>
        public FiringWeightValues Values = new FiringWeightValues();

        /// <summary>True when it installed itself at the start of Play.</summary>
        public bool StartedByItself { get; private set; }

        struct LitLight
        {
            public Light Light;
            public float At;   // real time it was lit
            public float Peak;
        }

        readonly List<LitLight> _lights = new List<LitLight>();
        readonly CameraKicks _kicks = new CameraKicks();
        Camera _moved;
        Vector3 _movedFrom;

        /// <summary>Shots that asked for weight since it was made. For tests.</summary>
        public int Shots { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Current = null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void InstallByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Ensure().StartedByItself = true;
        }

        /// <summary>The one running, made if there is none. It lives across scene loads.</summary>
        public static FiringWeight Ensure()
        {
            if (Current != null) return Current;
            var go = new GameObject("FiringWeight");
            DontDestroyOnLoad(go);
            Current = go.AddComponent<FiringWeight>();
            return Current;
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            RenderPipelineManager.endCameraRendering += OnEndCamera;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= OnBeginCamera;
            RenderPipelineManager.endCameraRendering -= OnEndCamera;
            PutBack();
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i].Light != null) _lights[i].Light.enabled = false;
        }

        void OnDestroy()
        {
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i].Light != null) Destroy(_lights[i].Light.gameObject);
            _lights.Clear();
            if (Current == this) Current = null;
        }

        // ---------------- A shot ----------------

        /// <summary>A shot went off at the muzzle along a flat direction: its light, and the camera's kick.</summary>
        public void Fired(Vector3 muzzle, Vector3 flatDirection, ShotWeight weight)
        {
            if (!Values.On) return;
            Shots++;
            float now = Time.unscaledTime;
            float peak = FiringWeightRules.Light(weight, Values);
            if (peak > 0f && Values.LightSeconds > 0f && Values.MaxLights > 0) LightUp(muzzle, peak, now);
            if (GameSettings.CameraKick) _kicks.Add(now, flatDirection.x, flatDirection.z, FiringWeightRules.Kick(weight, Values));
        }

        void LightUp(Vector3 at, float peak, float now)
        {
            int pick = -1;
            for (int i = 0; i < _lights.Count; i++)
                if (_lights[i].Light != null && !_lights[i].Light.enabled) { pick = i; break; }
            if (pick < 0 && _lights.Count < Values.MaxLights)
            {
                var go = new GameObject("MuzzleLight");
                go.transform.SetParent(transform, false);
                var light = go.AddComponent<Light>();
                light.type = LightType.Point;
                light.shadows = LightShadows.None;
                light.enabled = false;
                _lights.Add(new LitLight { Light = light, At = float.NegativeInfinity });
                pick = _lights.Count - 1;
            }
            if (pick < 0)
            {
                pick = 0; // all lit: the oldest is taken
                for (int i = 1; i < _lights.Count; i++) if (_lights[i].At < _lights[pick].At) pick = i;
            }
            LitLight lit = _lights[pick];
            if (lit.Light == null) return;
            lit.At = now;
            lit.Peak = peak;
            lit.Light.transform.position = at;
            lit.Light.range = Values.LightRange;
            lit.Light.color = new Color(Values.LightR, Values.LightG, Values.LightB);
            lit.Light.intensity = peak;
            lit.Light.enabled = true;
            _lights[pick] = lit;
        }

        void Update()
        {
            float now = Time.unscaledTime;
            for (int i = 0; i < _lights.Count; i++)
            {
                LitLight lit = _lights[i];
                if (lit.Light == null || !lit.Light.enabled) continue;
                float intensity = FiringWeightRules.LightAt(now - lit.At, Values.LightSeconds, lit.Peak);
                if (intensity <= 0f || !Values.On) lit.Light.enabled = false;
                else lit.Light.intensity = intensity;
            }
        }

        // ---------------- What tests read ----------------

        /// <summary>Muzzle lights on now.</summary>
        public int LightsOn
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _lights.Count; i++) if (_lights[i].Light != null && _lights[i].Light.enabled) n++;
                return n;
            }
        }

        /// <summary>Muzzle lights made so far (the pool's size).</summary>
        public int LightsMade => _lights.Count;

        /// <summary>The camera's kick now, on the ground (m): what is added while it draws. Zero with the setting off.</summary>
        public Vector3 KickOffset
        {
            get
            {
                if (!GameSettings.CameraKick || !Values.On) return Vector3.zero;
                _kicks.Offset(Time.unscaledTime, Values.KickSeconds, Values.KickPeakShare, Values.MostKick, out float x, out float z);
                return new Vector3(x, 0f, z);
            }
        }

        /// <summary>Forgets every kick that runs (a new floor, a teleport, tests).</summary>
        public void ClearKicks() => _kicks.Clear();

        // ---------------- The camera, moved only while it draws ----------------

        void OnBeginCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == null || cam.cameraType != CameraType.Game || cam.GetComponent<CameraRig>() == null) return;
            Vector3 offset = Running != null ? KickOffset : Vector3.zero;
            if (offset.sqrMagnitude < 1e-10f) return;
            PutBack();
            _moved = cam;
            _movedFrom = cam.transform.position;
            cam.transform.position = _movedFrom + offset;
        }

        void OnEndCamera(ScriptableRenderContext context, Camera cam)
        {
            if (cam == _moved) PutBack();
        }

        void PutBack()
        {
            if (_moved == null) return;
            _moved.transform.position = _movedFrom;
            _moved = null;
        }
    }
}
