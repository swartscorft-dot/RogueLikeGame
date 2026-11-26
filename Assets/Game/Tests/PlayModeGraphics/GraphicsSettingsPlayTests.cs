using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Roguelike.Look;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 128: the graphics settings in the real pipeline. The defaults change nothing the game sets today; each line
    /// changes the value it names (the URP asset's samples, the sun's shadow map and cascades, the camera's shadows, the render
    /// scale, the glow's bloom, the exposure, V-Sync, the frame-rate target, the number in the corner); Play's end puts every one
    /// back and the asset is never marked to be saved; the quality presets set their four lines and a hand-made mix reads Custom;
    /// what is saved is read back, and work order 125's on reads as 4x; the display's lines do not act in the Editor; the saved
    /// values are never read in batch mode. His saved settings are kept before each test and put back after it.
    /// </summary>
    public class GraphicsSettingsPlayTests
    {
        PlayerCharacter _player;
        Dictionary<string, int?> _prefs;
        UniversalRenderPipelineAsset _asset;
        int _samples0, _resolution0, _cascades0, _vSync0, _rate0;
        float _scale0;
        bool _shadows0;
        FullScreenMode _mode0;
        int _width0, _height0;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            _prefs = SettingsPrefs.Save();
            SettingsScreenValues.On = true;
            AntiAliasingValues.On = true;
            AntiAliasingValues.Kind = AntiAliasingKind.Msaa4x;
            GraphicsChoices.TestReadsSaved = false;
            GraphicsChoices.Unload();
            PlayerInputDriver.ReadRealDevices = false;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null);
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _asset = UniversalRenderPipeline.asset;
            Assert.That(_asset, Is.Not.Null, "the URP asset in use");
            _samples0 = _asset.msaaSampleCount;
            _scale0 = _asset.renderScale;
            _resolution0 = _asset.mainLightShadowmapResolution;
            _cascades0 = _asset.shadowCascadeCount;
            _shadows0 = CameraData().renderShadows;
            _vSync0 = QualitySettings.vSyncCount;
            _rate0 = Application.targetFrameRate;
            _mode0 = Screen.fullScreenMode;
            _width0 = Screen.width;
            _height0 = Screen.height;
            Render(); // the pipeline and its volumes made, as a frame on a screen would
        }

        [TearDown]
        public void CleanUp()
        {
            GraphicsChoices.TestReadsSaved = false;
            GraphicsChoices.Unload();
            SettingsScreenValues.On = true;
            AntiAliasingValues.On = true;
            SettingsPrefs.Restore(_prefs);
            PlayerInputDriver.ReadRealDevices = true;
        }

        static UniversalAdditionalCameraData CameraData() => Camera.main.GetComponent<UniversalAdditionalCameraData>();

        /// <summary>
        /// The game's camera drawn once into a small texture (a batch run draws no frame to a screen, so neither the pipeline's
        /// per-camera callbacks nor its volume system run until a camera is drawn by hand).
        /// </summary>
        static void Render()
        {
            Camera cam = Camera.main;
            var rt = new RenderTexture(64, 36, 24, RenderTextureFormat.ARGB32);
            RenderTexture before = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = before;
            rt.Release();
            Object.Destroy(rt);
        }

        static IEnumerator Frames(int n)
        {
            for (int i = 0; i < n; i++) yield return null;
        }

        /// <summary>The blended volume stack for the game's camera now (as URP blends it for a frame).</summary>
        static VolumeStack Stack()
        {
            Camera cam = Camera.main;
            VolumeManager.instance.Update(cam.transform, CameraData().volumeLayerMask);
            return VolumeManager.instance.stack;
        }

        void AssertToday(string when)
        {
            Assert.That(_asset.msaaSampleCount, Is.EqualTo(_samples0), when + ": the asset's samples");
            Assert.That(_asset.renderScale, Is.EqualTo(_scale0), when + ": the render scale");
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(_resolution0), when + ": the sun's shadow map");
            Assert.That(_asset.shadowCascadeCount, Is.EqualTo(_cascades0), when + ": the cascades");
            Assert.That(CameraData().renderShadows, Is.EqualTo(_shadows0), when + ": the camera's shadows");
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(_vSync0), when + ": V-Sync");
            Assert.That(Application.targetFrameRate, Is.EqualTo(_rate0), when + ": the frame-rate target");
            Assert.That(GraphicsLook.SettingsVolume == null || !GraphicsLook.SettingsVolume.enabled, Is.True, when + ": no volume of its own");
            Assert.That(FrameRateCounter.Current, Is.Null, when + ": no number in the corner");
            Assert.That(Screen.fullScreenMode, Is.EqualTo(_mode0), when + ": the display mode");
            Assert.That(Screen.width == _width0 && Screen.height == _height0, Is.True, when + ": the window's size (a test resizes nothing)");
        }

        // ---------------- The defaults ----------------

        [UnityTest]
        public IEnumerator TheDefaults_ChangeNothingTheGameSetsToday()
        {
            Assert.That(_samples0, Is.EqualTo(AntiAliasingValues.MsaaSamples), "the asset asks for work order 125's 4 samples");
            Bloom bloom = Stack().GetComponent<Bloom>();
            float bloomToday = bloom.intensity.value;
            float exposureToday = Stack().GetComponent<ColorAdjustments>().postExposure.value;
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.IsLoaded, Is.True);
            Assert.That(GraphicsChoices.Quality, Is.EqualTo(QualityPreset.High));
            yield return Frames(3);
            Render();
            AssertToday("loaded with the defaults");
            Assert.That(AntiAliasingValues.On, Is.True, "work order 125's on, as it was");
            Assert.That(Camera.main.allowMSAA, Is.True);
            Assert.That(Stack().GetComponent<Bloom>().intensity.value, Is.EqualTo(bloomToday), "the glow as today");
            Assert.That(Stack().GetComponent<ColorAdjustments>().postExposure.value, Is.EqualTo(exposureToday), "the exposure as today");
            Assert.That(exposureToday, Is.EqualTo(GraphicsSettingsValues.TodayExposure), "today's exposure is the named one");
        }

        // ---------------- Each line ----------------

        [UnityTest]
        public IEnumerator EachGraphicsLine_ChangesTheValueItNames_AndPlaysEndPutsEveryOneBack()
        {
            GraphicsChoices.Load();
            yield return null;

            // Anti-aliasing: 8x, 2x, off (work order 125's switch), 4x.
            Assert.That(GraphicsChoices.Change(SettingLine.AntiAliasing, 1, keep: false), Is.True);
            Assert.That(_asset.msaaSampleCount, Is.EqualTo(8), "8x");
            GraphicsChoices.Change(SettingLine.AntiAliasing, -1, keep: false);
            GraphicsChoices.Change(SettingLine.AntiAliasing, -1, keep: false);
            Assert.That(_asset.msaaSampleCount, Is.EqualTo(2), "2x");
            GraphicsChoices.Change(SettingLine.AntiAliasing, -1, keep: false);
            Assert.That(AntiAliasingValues.On, Is.False, "off is work order 125's off");
            Render();
            Assert.That(Camera.main.allowMSAA, Is.False, "the camera multisamples no more");
            Assert.That(_asset.msaaSampleCount, Is.EqualTo(_samples0), "the asset left as it was");
            GraphicsChoices.Change(SettingLine.AntiAliasing, 1, keep: false);
            GraphicsChoices.Change(SettingLine.AntiAliasing, 1, keep: false);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.AntiAliasing), Is.EqualTo("4x"));
            Render();
            Assert.That(Camera.main.allowMSAA, Is.True);

            // Shadows: low, off.
            GraphicsChoices.Change(SettingLine.Shadows, -1, keep: false);
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(GraphicsSettingsValues.LowShadowResolution), "low: the sun's map smaller");
            Assert.That(_asset.shadowCascadeCount, Is.EqualTo(GraphicsSettingsValues.LowShadowCascades), "and fewer cascades");
            GraphicsChoices.Change(SettingLine.Shadows, -1, keep: false);
            Assert.That(CameraData().renderShadows, Is.False, "off: the camera renders none");
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(_resolution0), "off leaves the asset's map as it was");

            // Render scale.
            GraphicsChoices.Change(SettingLine.RenderScale, -1, keep: false);
            Assert.That(_asset.renderScale, Is.EqualTo(0.9f).Within(1e-4f));

            // Glow and brightness, by a volume of its own.
            GraphicsChoices.Change(SettingLine.Glow, 1, keep: false);
            Assert.That(GraphicsLook.SettingsVolume, Is.Not.Null);
            Assert.That(GraphicsLook.SettingsVolume.enabled, Is.True);
            Assert.That(Stack().GetComponent<Bloom>().IsActive(), Is.False, "the glow off: URP draws no bloom");
            GraphicsChoices.Change(SettingLine.Brightness, 1, keep: false);
            Assert.That(Stack().GetComponent<ColorAdjustments>().postExposure.value, Is.EqualTo(GraphicsSettingsValues.BrightnessStepEv).Within(1e-5f), "one step brighter");
            GraphicsChoices.Change(SettingLine.Glow, 1, keep: false);
            Assert.That(Stack().GetComponent<Bloom>().IsActive(), Is.True, "the glow on again");

            // V-Sync and the cap.
            bool vSyncWas = GraphicsChoices.VSyncOn;
            GraphicsChoices.Change(SettingLine.VSync, 1, keep: false);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(vSyncWas ? 0 : 1));
            if (!GraphicsChoices.VSyncOn) GraphicsChoices.Change(SettingLine.VSync, 1, keep: false);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(1), "V-Sync on");
            Assert.That(GraphicsChoices.Greyed(SettingLine.FrameCap), Is.True, "the cap greyed under V-Sync");
            Assert.That(GraphicsChoices.Change(SettingLine.FrameCap, -1, keep: false), Is.False, "and does not change");
            GraphicsChoices.Change(SettingLine.VSync, 1, keep: false);
            Assert.That(QualitySettings.vSyncCount, Is.EqualTo(0), "V-Sync off");
            Assert.That(GraphicsChoices.Change(SettingLine.FrameCap, -1, keep: false), Is.True);
            Assert.That(Application.targetFrameRate, Is.EqualTo(240), "a step down from unlimited: 240");

            // The number in the corner.
            GraphicsChoices.Change(SettingLine.ShowFrameRate, 1, keep: false);
            Assert.That(FrameRateCounter.Current, Is.Not.Null);
            yield return Frames(3);

#if UNITY_EDITOR
            Assert.That(UnityEditor.EditorUtility.IsDirty(_asset), Is.False, "the URP asset is never marked to be saved: nothing reaches the disk");
#endif
            // Play's end (Application.quitting calls this, and no frame follows it): everything back at once.
            GraphicsChoices.End();
            AssertToday("after Play's end");
            GraphicsChoices.Unload(); // the choices forgotten too, so the frames after this test's end put nothing on again
            yield return Frames(2);
            AssertToday("the frames after");
            Assert.That(Stack().GetComponent<Bloom>().IsActive(), Is.True);
        }

        [UnityTest]
        public IEnumerator TheQualityPresets_SetTheirFourLines_AHandMadeMixReadsCustom_AndHighIsToday()
        {
            GraphicsChoices.Load();
            yield return null;
            GraphicsChoices.Change(SettingLine.Quality, -1, keep: false);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.Quality), Is.EqualTo("Medium"));
            Assert.That(_asset.msaaSampleCount, Is.EqualTo(2));
            Assert.That(_asset.mainLightShadowmapResolution, Is.EqualTo(GraphicsSettingsValues.LowShadowResolution));
            Assert.That(_asset.renderScale, Is.EqualTo(0.9f).Within(1e-4f));
            GraphicsChoices.Change(SettingLine.Quality, -1, keep: false);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.Quality), Is.EqualTo("Low"));
            Assert.That(AntiAliasingValues.On, Is.False);
            Assert.That(CameraData().renderShadows, Is.False);
            Assert.That(_asset.renderScale, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(Stack().GetComponent<Bloom>().IsActive(), Is.False);
            GraphicsChoices.Change(SettingLine.Glow, 1, keep: false);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.Quality), Is.EqualTo("Custom"), "a line changed by hand");
            GraphicsChoices.Change(SettingLine.Quality, -1, keep: false);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.Quality), Is.EqualTo("High"), "from Custom: today's look");
            yield return Frames(2);
            AssertToday("back on High");
            Assert.That(AntiAliasingValues.On, Is.True);
        }

        // ---------------- Saved ----------------

        [UnityTest]
        public IEnumerator WhatIsSaved_IsReadBack_AndWorkOrder125sOn_ReadsAs4x_AndBatchModeReadsNothing()
        {
            foreach (string k in GraphicsChoices.Keys) PlayerPrefs.DeleteKey(k);
            Assume.That(Application.isBatchMode, Is.True, "the test runs as the runner runs it");
            Assert.That(GraphicsChoices.IgnoresSaved, Is.True, "batch mode: the saved values are never read");
            GraphicsChoices.TestReadsSaved = true; // this test reads what it saved itself
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.Choice.AntiAliasing, Is.EqualTo(GraphicsSettingsValues.DefaultAntiAliasing), "nothing saved, work order 125 on: 4x");
            GraphicsChoices.Unload();
            AntiAliasingValues.On = false;
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.Choice.AntiAliasing, Is.EqualTo(GraphicsSettingsValues.AntiAliasingOff), "work order 125 off: off");
            AntiAliasingValues.On = true;
            GraphicsChoices.Unload();
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.Change(SettingLine.RenderScale, -1), Is.True);
            Assert.That(GraphicsChoices.Change(SettingLine.Brightness, -1), Is.True);
            Assert.That(PlayerPrefs.GetInt(GraphicsChoices.RenderScaleKey, -1), Is.EqualTo(4), "saved when changed");
            GraphicsChoices.Unload();
            yield return null;
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.ValueWords(SettingLine.RenderScale), Is.EqualTo("90 %"), "read back");
            Assert.That(GraphicsChoices.ValueWords(SettingLine.Brightness), Is.EqualTo("-1"));
            Assert.That(GraphicsChoices.Quality, Is.EqualTo(QualityPreset.Custom));
            PlayerPrefs.SetInt(GraphicsChoices.ShadowsKey, 17); // a damaged value
            GraphicsChoices.Unload();
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.Choice.Shadows, Is.EqualTo(ShadowsStep.High), "a damaged value reads as the default");
            GraphicsChoices.TestReadsSaved = false;
            GraphicsChoices.Unload();
            GraphicsChoices.Load();
            Assert.That(GraphicsChoices.Choice.RenderScale, Is.EqualTo(GraphicsSettingsValues.DefaultRenderScale), "in batch mode the saved 90 % is not read");
        }

        // ---------------- The display in the Editor ----------------

        [UnityTest]
        public IEnumerator TheDisplaysLines_ShowAndSave_ButDoNotActInTheEditor()
        {
            Assume.That(Application.isEditor, Is.True);
            GraphicsChoices.Load();
            yield return null;
            Assert.That(GraphicsChoices.DisplayActs, Is.False);
            string mode = GraphicsChoices.ValueWords(SettingLine.DisplayMode);
            Assert.That(GraphicsChoices.Change(SettingLine.DisplayMode, 1, keep: false), Is.True);
            Assert.That(GraphicsChoices.ValueWords(SettingLine.DisplayMode), Is.Not.EqualTo(mode), "the line shows the choice");
            Assert.That(GraphicsChoices.Sizes.Count, Is.GreaterThan(0));
            Assert.That(GraphicsChoices.Sizes.All(s => s.Width >= GraphicsSettingsValues.LeastWidth && s.Height >= GraphicsSettingsValues.LeastHeight), Is.True, "none under 1280 by 720");
            GraphicsChoices.Change(SettingLine.Resolution, -1, keep: false);
            yield return Frames(2);
            Assert.That(Screen.fullScreenMode, Is.EqualTo(_mode0), "but the display is not touched");
            Assert.That(Screen.width == _width0 && Screen.height == _height0, Is.True, "nor the window's size");
        }

        // ---------------- The number ----------------

        [UnityTest]
        public IEnumerator TheFrameRateNumber_CountsFramesOverHalfASecond()
        {
            FrameRateCounter c = FrameRateCounter.Ensure();
            c.Restart();
            c.Count(10f);
            for (int i = 1; i <= 30; i++) c.Count(10f + i / 60f);
            Assert.That(c.Shown, Is.EqualTo(60), "30 frames in half a second: 60 a second");
            FrameRateCounter.Remove();
            yield return null;
            Assert.That(FrameRateCounter.Current, Is.Null);
        }
    }
}
