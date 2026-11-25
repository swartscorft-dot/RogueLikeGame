using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Roguelike.Look
{
    /// <summary>
    /// The anti-aliasing on the game's camera (work order 125; Kristian, 2026-11-25, of work order 119's pictures: "man ser mange
    /// fragmenterte pixler rundt omkring, det ser veldig dårlig ut. Kan en agent gå over det å få det se bra ut."; the game drew
    /// with none at all), set at the start of the camera's every frame from <see cref="AntiAliasingValues"/> (nothing kept,
    /// nothing allocated): on, the camera tagged MainCamera multisamples (the URP asset asks for
    /// <see cref="AntiAliasingValues.MsaaSamples"/>) or runs SMAA, by <see cref="AntiAliasingValues.Kind"/>; off, no multisampling
    /// (before work order 125 the asset asked for 1 sample, so the camera had none) and its own post anti-aliasing as it was when
    /// first seen (none). In its own assembly, as it needs URP's camera data, so the game's own assembly needs no new reference.
    /// </summary>
    public static class AntiAliasingLook
    {
        static Camera s_main;
        static AntialiasingMode s_mainMode;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void Install()
        {
            AntiAliasingValues.On = true;   // every Play starts on (not kept between starts)
            ToonLookValues.CleanInk = true; // the same for the clean line's switch
            s_main = null;
            RenderPipelineManager.beginCameraRendering -= Begin;
            RenderPipelineManager.beginCameraRendering += Begin;
        }

        static void Begin(ScriptableRenderContext context, Camera camera)
        {
            if (!Application.isPlaying || camera == null || camera.cameraType != CameraType.Game || !camera.CompareTag("MainCamera")) return;
            if (!camera.TryGetComponent(out UniversalAdditionalCameraData data)) return;
            if (camera != s_main)
            {
                s_main = camera;
                s_mainMode = data.antialiasing;
            }
            bool allow = AntiAliasingValues.MsaaNow;
            AntialiasingMode mode = AntiAliasingValues.On ? (AntiAliasingValues.SmaaNow ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.None) : s_mainMode;
            if (camera.allowMSAA != allow) camera.allowMSAA = allow;
            if (data.antialiasing != mode) data.antialiasing = mode;
            if (mode == AntialiasingMode.SubpixelMorphologicalAntiAliasing && data.antialiasingQuality != AntialiasingQuality.High) data.antialiasingQuality = AntialiasingQuality.High;
        }
    }
}
