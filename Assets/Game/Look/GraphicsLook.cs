using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roguelike.Look
{
    /// <summary>
    /// The graphics settings' part in the render pipeline (work order 128, <see cref="GraphicsChoices"/>), put on when a setting
    /// changes and checked at the start of the game's camera's every frame (a few reads; written only when they differ):
    ///
    /// - Anti-aliasing: the URP asset's multisampling samples (<see cref="UniversalRenderPipelineAsset.msaaSampleCount"/>) while
    ///   work order 125's switch is on and multisampling is its kind; off is work order 125's own (the camera allows none,
    ///   <c>AntiAliasingLook</c>).
    /// - Render scale: <see cref="UniversalRenderPipelineAsset.renderScale"/>.
    /// - Shadows: off, the camera renders none (<see cref="UniversalAdditionalCameraData.renderShadows"/>, the camera's own, not
    ///   the asset's); low, the sun's shadow map (<see cref="UniversalRenderPipelineAsset.mainLightShadowmapResolution"/>) and its
    ///   cascades (<see cref="UniversalRenderPipelineAsset.shadowCascadeCount"/>) smaller; high, as they were.
    /// - Glow and brightness: a volume of its own made in code (never the scene's profile), global and above the scene's, whose
    ///   bloom's intensity is 0 while the glow is off and whose post exposure is today's plus the brightness's steps; it exists
    ///   only while one of the two is off today's.
    ///
    /// At the defaults (High, brightness 0) nothing is written at all. What it changed on the asset is remembered as it first
    /// touches it and put back when the value returns to today's, when the settings end (<see cref="GraphicsChoices.Ended"/>,
    /// raised at Play's end) and when Play ends (<see cref="Application.quitting"/>), so the asset is never left changed in the
    /// Editor; its setters mark nothing to be saved, so nothing reaches the disk. In its own assembly, as it needs URP's types.
    /// </summary>
    public static class GraphicsLook
    {
        /// <summary>The volume's priority: above the scene's one volume (priority 1 in ControlRoom).</summary>
        public const float VolumePriority = 100f;

        static UniversalRenderPipelineAsset s_asset;
        static int s_msaa0, s_shadowResolution0, s_cascades0;
        static float s_scale0;
        static Camera s_camera;
        static UniversalAdditionalCameraData s_cameraData;
        static bool s_cameraShadows0;
        static GameObject s_volumeObject;
        static Volume s_volume;
        static VolumeProfile s_profile;
        static Bloom s_bloom;
        static ColorAdjustments s_color;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            s_asset = null;
            s_camera = null;
            s_cameraData = null;
            s_volumeObject = null;
            s_volume = null;
            s_profile = null;
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.beginCameraRendering += Begin;
            GraphicsChoices.Changed -= ApplyNow;
            GraphicsChoices.Changed += ApplyNow;
            GraphicsChoices.Ended -= Restore;
            GraphicsChoices.Ended += Restore;
            Application.quitting -= Restore;
            Application.quitting += Restore;
        }

        /// <summary>The volume made for the glow and the brightness, or null while both are today's. For tests.</summary>
        public static Volume SettingsVolume => s_volume;
        /// <summary>The asset's values as they were before anything was put on (for tests): samples, render scale, the sun's shadow map and cascades.</summary>
        public static bool TookAsset => s_asset != null;
        public static int OriginalSamples => s_msaa0;
        public static float OriginalScale => s_scale0;
        public static int OriginalShadowResolution => s_shadowResolution0;
        public static int OriginalCascades => s_cascades0;

        static void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying || camera == null || camera.cameraType != CameraType.Game || !camera.CompareTag("MainCamera")) return;
            Apply(camera);
        }

        /// <summary>Puts the settings on now: on the asset at once, and on the game's camera if there is one.</summary>
        public static void ApplyNow()
        {
            if (!Application.isPlaying) return;
            Apply(Camera.main);
        }

        static void Apply(Camera camera)
        {
            if (!SettingsScreenValues.On)
            {
                if (s_asset != null || s_volume != null || s_camera != null) Restore();
                return;
            }
            GraphicsChoice c = GraphicsChoices.InUse;
            UniversalRenderPipelineAsset asset = UniversalRenderPipeline.asset;
            if (asset != s_asset)
            {
                RestoreAsset();
                if (asset != null)
                {
                    s_asset = asset;
                    s_msaa0 = asset.msaaSampleCount;
                    s_scale0 = asset.renderScale;
                    s_shadowResolution0 = asset.mainLightShadowmapResolution;
                    s_cascades0 = asset.shadowCascadeCount;
                }
            }
            if (s_asset != null)
            {
                int samples = AntiAliasingValues.MsaaNow && c.AntiAliasing != GraphicsSettingsValues.DefaultAntiAliasing ? GraphicsRules.Samples(c.AntiAliasing) : s_msaa0;
                if (s_asset.msaaSampleCount != samples) s_asset.msaaSampleCount = samples;
                float scale = c.RenderScale == GraphicsSettingsValues.DefaultRenderScale ? s_scale0 : GraphicsRules.RenderScale(c.RenderScale);
                if (!Mathf.Approximately(s_asset.renderScale, scale)) s_asset.renderScale = scale;
                bool low = c.Shadows == ShadowsStep.Low;
                int resolution = low ? GraphicsSettingsValues.LowShadowResolution : s_shadowResolution0, cascades = low ? GraphicsSettingsValues.LowShadowCascades : s_cascades0;
                if (s_asset.mainLightShadowmapResolution != resolution) s_asset.mainLightShadowmapResolution = resolution;
                if (s_asset.shadowCascadeCount != cascades) s_asset.shadowCascadeCount = cascades;
            }
            if (camera != null && camera != s_camera)
            {
                RestoreCamera();
                if (camera.TryGetComponent(out UniversalAdditionalCameraData data))
                {
                    s_camera = camera;
                    s_cameraData = data;
                    s_cameraShadows0 = data.renderShadows;
                }
            }
            if (s_cameraData != null)
            {
                bool shadows = c.Shadows != ShadowsStep.Off && s_cameraShadows0;
                if (s_cameraData.renderShadows != shadows) s_cameraData.renderShadows = shadows;
            }
            bool glowOff = !c.Glow, bright = c.Brightness != 0;
            if (glowOff || bright)
            {
                MakeVolume();
                if (s_bloom.active != glowOff) s_bloom.active = glowOff;
                if (s_color.active != bright) s_color.active = bright;
                float exposure = GraphicsRules.Exposure(c.Brightness);
                if (s_color.postExposure.value != exposure) s_color.postExposure.Override(exposure);
                if (!s_volume.enabled) s_volume.enabled = true;
            }
            else if (s_volume != null && s_volume.enabled) s_volume.enabled = false;
        }

        static void MakeVolume()
        {
            if (s_volume != null) return;
            s_volumeObject = new GameObject("GraphicsSettingsVolume") { hideFlags = HideFlags.DontSave };
            Object.DontDestroyOnLoad(s_volumeObject);
            s_profile = ScriptableObject.CreateInstance<VolumeProfile>();
            s_profile.name = "GraphicsSettingsProfile";
            s_profile.hideFlags = HideFlags.DontSave;
            s_bloom = s_profile.Add<Bloom>(false);
            s_bloom.intensity.Override(0f); // the glow off: URP draws no bloom at intensity 0 (Bloom.IsActive)
            s_color = s_profile.Add<ColorAdjustments>(false);
            s_color.postExposure.Override(GraphicsSettingsValues.TodayExposure);
            s_volume = s_volumeObject.AddComponent<Volume>();
            s_volume.isGlobal = true;
            s_volume.priority = VolumePriority;
            s_volume.sharedProfile = s_profile;
        }

        static void RestoreAsset()
        {
            if (s_asset == null) return;
            if (s_asset.msaaSampleCount != s_msaa0) s_asset.msaaSampleCount = s_msaa0;
            if (!Mathf.Approximately(s_asset.renderScale, s_scale0)) s_asset.renderScale = s_scale0;
            if (s_asset.mainLightShadowmapResolution != s_shadowResolution0) s_asset.mainLightShadowmapResolution = s_shadowResolution0;
            if (s_asset.shadowCascadeCount != s_cascades0) s_asset.shadowCascadeCount = s_cascades0;
            s_asset = null;
        }

        static void RestoreCamera()
        {
            if (s_cameraData != null && s_cameraData.renderShadows != s_cameraShadows0) s_cameraData.renderShadows = s_cameraShadows0;
            s_camera = null;
            s_cameraData = null;
        }

        /// <summary>Everything put back as it was: the asset's values, the camera's shadows, the volume gone. Play's end, and the settings' end.</summary>
        public static void Restore()
        {
            RestoreAsset();
            RestoreCamera();
            if (s_volumeObject != null)
            {
                if (Application.isPlaying) Object.Destroy(s_volumeObject);
                else Object.DestroyImmediate(s_volumeObject);
            }
            if (s_profile != null)
            {
                if (Application.isPlaying) Object.Destroy(s_profile);
                else Object.DestroyImmediate(s_profile);
            }
            s_volumeObject = null;
            s_volume = null;
            s_profile = null;
            s_bloom = null;
            s_color = null;
        }
    }
}
