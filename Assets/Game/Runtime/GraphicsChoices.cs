using System;
using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// The graphics settings (work order 128, <see cref="GraphicsRules"/>): kept in PlayerPrefs beside <see cref="GameSettings"/>'
    /// (keys under <see cref="KeyPrefix"/>), each saved when it is changed and applied at once (no Apply button).
    ///
    /// What it applies itself: V-Sync (<see cref="QualitySettings.vSyncCount"/>), the frame-rate cap
    /// (<see cref="Application.targetFrameRate"/>), the display mode and the window's size (<see cref="Screen"/>: never in the
    /// Editor, where those two lines show and save only), and the frame-rate number (<see cref="FrameRateCounter"/>). The render
    /// pipeline's values (the multisampling, the shadows, the render scale, the glow, the brightness) are put on by
    /// <c>Assets/Game/Look/GraphicsLook.cs</c>, which reads <see cref="InUse"/> and is told by <see cref="Changed"/> and
    /// <see cref="Ended"/>. A line nobody has chosen is left as the game starts it: with nothing saved, nothing is touched.
    ///
    /// Nothing is read from PlayerPrefs until <see cref="Load"/>, which runs by itself when Play starts as the HUD does (never in a
    /// test run); in batch mode and under the benchmark (<see cref="Benchmark.Argument"/>) the saved values are never read, so
    /// their measurements and pictures stay comparable whatever this machine keeps. <see cref="End"/> (Play's end, and
    /// <see cref="Unload"/>) puts back what it changed. Anti-aliasing's on and off stay <see cref="AntiAliasingValues.On"/>
    /// (work order 125's switch, which <c>AntiAliasingLook</c> follows); this adds how many samples.
    /// </summary>
    public static class GraphicsChoices
    {
        public const string KeyPrefix = "Roguelike.Settings.Graphics.";
        public const string DisplayModeKey = KeyPrefix + "DisplayMode";
        public const string WidthKey = KeyPrefix + "Width";
        public const string HeightKey = KeyPrefix + "Height";
        public const string VSyncKey = KeyPrefix + "VSync";
        public const string FrameCapKey = KeyPrefix + "FrameCap";
        public const string AntiAliasingKey = KeyPrefix + "AntiAliasing";
        public const string ShadowsKey = KeyPrefix + "Shadows";
        public const string RenderScaleKey = KeyPrefix + "RenderScale";
        public const string GlowKey = KeyPrefix + "Glow";
        public const string BrightnessKey = KeyPrefix + "Brightness";
        public const string ShowFrameRateKey = KeyPrefix + "ShowFrameRate";

        /// <summary>Every key it may write, for tests that save and restore them.</summary>
        public static readonly string[] Keys =
        {
            DisplayModeKey, WidthKey, HeightKey, VSyncKey, FrameCapKey, AntiAliasingKey, ShadowsKey, RenderScaleKey, GlowKey, BrightnessKey, ShowFrameRateKey,
        };

        static GraphicsChoice s_choice = GraphicsSettingsValues.Defaults();
        static bool s_loaded, s_found, s_vSyncTouched, s_rateTouched;
        static int s_vSyncFound, s_rateFound;
        static readonly List<ScreenSize> s_sizes = new List<ScreenSize>();
        static readonly List<string> s_sizeWords = new List<string>();
        static ScreenSize s_own;

        /// <summary>A setting changed (raised after it is stored): the look puts the pipeline's values on at once.</summary>
        public static event Action Changed;
        /// <summary>Play ends or the settings are unloaded: the look puts the pipeline's values back.</summary>
        public static event Action Ended;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_choice = GraphicsSettingsValues.Defaults();
            s_loaded = s_found = s_vSyncTouched = s_rateTouched = false;
            s_sizes.Clear();
            s_sizeWords.Clear();
            Application.quitting -= End;
            Application.quitting += End;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LoadByItself()
        {
            if (!SessionRecordingRule.MayStartByItself(SceneManager.GetActiveScene().name, Application.isBatchMode)) return;
            Load();
            SavedMusicAndLook.Load(); // the music's two lines and the toon look, saved since the follow-up to work order 128
        }

        /// <summary>Loaded (by itself in play, or by a test).</summary>
        public static bool IsLoaded => s_loaded;

        /// <summary>
        /// The saved values are never read: in batch mode (the tests, the pictures) and under the benchmark, whose measurements
        /// must stay comparable (work order 128).
        /// </summary>
        public static bool IgnoresSaved =>
            (!TestReadsSaved && Application.isBatchMode)
            || Benchmark.Current != null || Array.IndexOf(Environment.GetCommandLineArgs(), Benchmark.Argument) >= 0
            || !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(Benchmark.EnvironmentSwitch));

        /// <summary>A test that saved values itself reads them back in batch mode (never his: it saves and restores the keys).</summary>
        public static bool TestReadsSaved;

        /// <summary>
        /// Reads the saved values and applies them (with the switch off, nothing). Under <see cref="IgnoresSaved"/> the defaults
        /// stand: today's picture, the display as the game starts it.
        /// </summary>
        public static void Load()
        {
            if (!SettingsScreenValues.On) return;
            Find();
            GraphicsChoice d = GraphicsSettingsValues.Defaults();
            d.AntiAliasing = GraphicsRules.AntiAliasingFromOldSwitch(AntiAliasingValues.On);
            if (!IgnoresSaved)
            {
                d.DisplayMode = Read(DisplayModeKey, GraphicsSettingsValues.AsTheGameStarts, 2);
                d.Width = Math.Max(0, PlayerPrefs.GetInt(WidthKey, 0));
                d.Height = Math.Max(0, PlayerPrefs.GetInt(HeightKey, 0));
                if (d.Width == 0 || d.Height == 0) d.Width = d.Height = 0;
                d.VSync = Read(VSyncKey, GraphicsSettingsValues.AsTheGameStarts, 2);
                d.FrameCap = GraphicsRules.Step(PlayerPrefs.GetInt(FrameCapKey, d.FrameCap), GraphicsSettingsValues.FrameCaps.Length, GraphicsSettingsValues.DefaultFrameCap);
                // Work order 125's line was never saved: with nothing saved here its on (every start) reads as 4x.
                d.AntiAliasing = GraphicsRules.Step(PlayerPrefs.GetInt(AntiAliasingKey, d.AntiAliasing), GraphicsSettingsValues.AntiAliasingSamples.Length, d.AntiAliasing);
                d.Shadows = (ShadowsStep)GraphicsRules.Step(PlayerPrefs.GetInt(ShadowsKey, (int)d.Shadows), 3, (int)ShadowsStep.High);
                d.RenderScale = GraphicsRules.Step(PlayerPrefs.GetInt(RenderScaleKey, d.RenderScale), GraphicsSettingsValues.RenderScalePercent.Length, GraphicsSettingsValues.DefaultRenderScale);
                d.Glow = PlayerPrefs.GetInt(GlowKey, 1) != 0;
                d.Brightness = Mathf.Clamp(PlayerPrefs.GetInt(BrightnessKey, 0), -GraphicsSettingsValues.BrightnessMostSteps, GraphicsSettingsValues.BrightnessMostSteps);
                d.ShowFrameRate = PlayerPrefs.GetInt(ShowFrameRateKey, 0) != 0;
            }
            s_choice = d;
            AntiAliasingValues.On = d.AntiAliasing != GraphicsSettingsValues.AntiAliasingOff;
            s_loaded = true;
            ApplyHere(true);
            Changed?.Invoke();
        }

        static int Read(string key, int fallback, int count)
        {
            int v = PlayerPrefs.GetInt(key, fallback);
            return v >= 0 && v < count ? v : fallback;
        }

        /// <summary>What the game had before anything was applied: V-Sync, the frame-rate target, the display's sizes.</summary>
        static void Find()
        {
            if (s_found) return;
            s_found = true;
            s_vSyncFound = QualitySettings.vSyncCount;
            s_rateFound = Application.targetFrameRate;
        }

        /// <summary>Play's end (and <see cref="Unload"/>): V-Sync and the frame-rate target as they were, the number gone, the pipeline's values put back.</summary>
        public static void End()
        {
            if (s_vSyncTouched && QualitySettings.vSyncCount != s_vSyncFound) QualitySettings.vSyncCount = s_vSyncFound;
            if (s_rateTouched && Application.targetFrameRate != s_rateFound) Application.targetFrameRate = s_rateFound;
            s_vSyncTouched = s_rateTouched = false;
            FrameRateCounter.Remove();
            Ended?.Invoke();
        }

        /// <summary>Forgets every choice until the next <see cref="Load"/>, after putting back what it changed. For tests and Play's end.</summary>
        public static void Unload()
        {
            End();
            s_choice = GraphicsSettingsValues.Defaults();
            s_loaded = false;
            s_found = false;
        }

        // ---------------- What is in use ----------------

        /// <summary>The choices as made (the display's lines as chosen; -1 where none was).</summary>
        public static GraphicsChoice Choice
        {
            get
            {
                GraphicsChoice c = s_choice;
                c.AntiAliasing = AntiAliasingStep;
                return c;
            }
        }

        /// <summary>
        /// What the look puts on: the choices, or the defaults (today's picture) under the benchmark and with the switch off.
        /// Anti-aliasing's on and off are work order 125's switch's in every case.
        /// </summary>
        public static GraphicsChoice InUse
        {
            get
            {
                if (!SettingsScreenValues.On || Benchmark.Current != null)
                {
                    GraphicsChoice d = GraphicsSettingsValues.Defaults();
                    d.AntiAliasing = GraphicsRules.AntiAliasingFromOldSwitch(AntiAliasingValues.On);
                    return d;
                }
                return Choice;
            }
        }

        /// <summary>The anti-aliasing step: off while work order 125's switch is off, else the samples chosen (4x if none).</summary>
        static int AntiAliasingStep =>
            !AntiAliasingValues.On ? GraphicsSettingsValues.AntiAliasingOff
            : s_choice.AntiAliasing == GraphicsSettingsValues.AntiAliasingOff ? GraphicsSettingsValues.DefaultAntiAliasing : s_choice.AntiAliasing;

        /// <summary>V-Sync is on now (as chosen, else as the game started).</summary>
        public static bool VSyncOn => s_choice.VSync == GraphicsSettingsValues.AsTheGameStarts ? QualitySettings.vSyncCount > 0 : s_choice.VSync == 1;

        /// <summary>The quality the four lines make now.</summary>
        public static QualityPreset Quality => GraphicsRules.QualityOf(Choice);

        /// <summary>The display mode now: as chosen, else as the game runs (borderless or exclusive full screen read as Fullscreen).</summary>
        public static int DisplayModeNow =>
            s_choice.DisplayMode != GraphicsSettingsValues.AsTheGameStarts ? s_choice.DisplayMode
            : Screen.fullScreenMode == FullScreenMode.Windowed ? GraphicsSettingsValues.Windowed : GraphicsSettingsValues.Fullscreen;

        /// <summary>The display and window lines act here: not in the Editor (work order 128: "there the lines show and save but do not act").</summary>
        public static bool DisplayActs => !Application.isEditor;

        /// <summary>The sizes offered (the display's list, cleaned: <see cref="GraphicsRules.Sizes"/>), made the first time they are asked for.</summary>
        public static IReadOnlyList<ScreenSize> Sizes
        {
            get
            {
                MakeSizes();
                return s_sizes;
            }
        }

        static void MakeSizes()
        {
            if (s_sizes.Count > 0) return;
            Resolution own = Screen.currentResolution;
            s_own = new ScreenSize(own.width, own.height);
            var display = new List<ScreenSize>();
            foreach (Resolution r in Screen.resolutions) display.Add(new ScreenSize(r.width, r.height));
            GraphicsRules.Sizes(display, s_own, s_sizes);
            if (s_sizes.Count == 0) s_sizes.Add(new ScreenSize(GraphicsSettingsValues.LeastWidth, GraphicsSettingsValues.LeastHeight)); // a display under 1280 by 720: the least offered
            s_sizeWords.Clear();
            foreach (ScreenSize s in s_sizes) s_sizeWords.Add(GraphicsRules.SizeWords(s));
        }

        static int SizeAt
        {
            get
            {
                MakeSizes();
                return GraphicsRules.SizeIndex(s_sizes, s_choice.Width, s_choice.Height, s_own);
            }
        }

        // ---------------- Changing ----------------

        /// <summary>A line is greyed now: the frame-rate cap while V-Sync is on. A greyed line does not change.</summary>
        public static bool Greyed(SettingLine line) => line == SettingLine.FrameCap && !GraphicsRules.CapActs(VSyncOn);

        /// <summary>
        /// A graphics line one step left (negative) or right (an on and off line flips either way); stored with
        /// <paramref name="keep"/> (a test may keep nothing) and applied at once. False for a line that is not a graphics line, a
        /// greyed one, or with the switch off.
        /// </summary>
        public static bool Change(SettingLine line, int delta, bool keep = true)
        {
            if (!SettingsScreenValues.On || !GraphicsRules.IsGraphics(line) || Greyed(line)) return false;
            if (delta == 0) delta = 1;
            Find();
            switch (line)
            {
                case SettingLine.DisplayMode:
                    s_choice.DisplayMode = DisplayModeNow == GraphicsSettingsValues.Fullscreen ? GraphicsSettingsValues.Windowed : GraphicsSettingsValues.Fullscreen;
                    Store(keep, DisplayModeKey, s_choice.DisplayMode);
                    break;
                case SettingLine.Resolution:
                {
                    int at = GraphicsRules.StepAlong(SizeAt, delta, s_sizes.Count);
                    s_choice.Width = s_sizes[at].Width;
                    s_choice.Height = s_sizes[at].Height;
                    Store(keep, WidthKey, s_choice.Width);
                    Store(keep, HeightKey, s_choice.Height);
                    break;
                }
                case SettingLine.VSync:
                    s_choice.VSync = VSyncOn ? 0 : 1;
                    Store(keep, VSyncKey, s_choice.VSync);
                    break;
                case SettingLine.FrameCap:
                    s_choice.FrameCap = GraphicsRules.StepAlong(s_choice.FrameCap, delta, GraphicsSettingsValues.FrameCaps.Length);
                    Store(keep, FrameCapKey, s_choice.FrameCap);
                    break;
                case SettingLine.Quality:
                {
                    GraphicsChoice c = GraphicsRules.WithPreset(Choice, GraphicsRules.StepQuality(Quality, delta));
                    SetAntiAliasing(c.AntiAliasing, keep);
                    s_choice.Shadows = c.Shadows;
                    s_choice.RenderScale = c.RenderScale;
                    s_choice.Glow = c.Glow;
                    Store(keep, ShadowsKey, (int)s_choice.Shadows);
                    Store(keep, RenderScaleKey, s_choice.RenderScale);
                    Store(keep, GlowKey, s_choice.Glow ? 1 : 0);
                    break;
                }
                case SettingLine.AntiAliasing:
                    SetAntiAliasing(GraphicsRules.StepAlong(AntiAliasingStep, delta, GraphicsSettingsValues.AntiAliasingSamples.Length), keep);
                    break;
                case SettingLine.Shadows:
                    s_choice.Shadows = (ShadowsStep)GraphicsRules.StepAlong((int)s_choice.Shadows, delta, 3);
                    Store(keep, ShadowsKey, (int)s_choice.Shadows);
                    break;
                case SettingLine.RenderScale:
                    s_choice.RenderScale = GraphicsRules.StepAlong(s_choice.RenderScale, delta, GraphicsSettingsValues.RenderScalePercent.Length);
                    Store(keep, RenderScaleKey, s_choice.RenderScale);
                    break;
                case SettingLine.Glow:
                    s_choice.Glow = !s_choice.Glow;
                    Store(keep, GlowKey, s_choice.Glow ? 1 : 0);
                    break;
                case SettingLine.Brightness:
                    s_choice.Brightness = Mathf.Clamp(s_choice.Brightness + Math.Sign(delta), -GraphicsSettingsValues.BrightnessMostSteps, GraphicsSettingsValues.BrightnessMostSteps);
                    Store(keep, BrightnessKey, s_choice.Brightness);
                    break;
                case SettingLine.ShowFrameRate:
                    s_choice.ShowFrameRate = !s_choice.ShowFrameRate;
                    Store(keep, ShowFrameRateKey, s_choice.ShowFrameRate ? 1 : 0);
                    break;
            }
            if (keep) PlayerPrefs.Save();
            ApplyHere(GraphicsRules.IsDisplay(line));
            Changed?.Invoke();
            return true;
        }

        static void SetAntiAliasing(int step, bool keep)
        {
            s_choice.AntiAliasing = step;
            AntiAliasingValues.On = step != GraphicsSettingsValues.AntiAliasingOff; // work order 125's switch, which the camera follows
            Store(keep, AntiAliasingKey, step);
        }

        static void Store(bool keep, string key, int value)
        {
            if (keep) PlayerPrefs.SetInt(key, value);
        }

        /// <summary>
        /// Puts on what is its own to apply: V-Sync and the cap where chosen, the display where chosen and it may act
        /// (<paramref name="display"/>: only when loading or when a display line changed), the frame-rate number.
        /// </summary>
        static void ApplyHere(bool display)
        {
            if (Benchmark.Current != null) return; // the benchmark sets these itself
            if (s_choice.VSync != GraphicsSettingsValues.AsTheGameStarts && QualitySettings.vSyncCount != s_choice.VSync)
            {
                QualitySettings.vSyncCount = s_choice.VSync;
                s_vSyncTouched = true;
            }
            // No cap chosen: the target as the game started (put back if a cap was chosen before); else the cap's, none under V-Sync.
            int rate = s_choice.FrameCap != GraphicsSettingsValues.DefaultFrameCap ? GraphicsRules.TargetFrameRate(s_choice, VSyncOn)
                       : s_rateTouched ? s_rateFound : Application.targetFrameRate;
            if (Application.targetFrameRate != rate)
            {
                Application.targetFrameRate = rate;
                s_rateTouched = true;
            }
            if (display && DisplayActs)
            {
                bool chosenMode = s_choice.DisplayMode != GraphicsSettingsValues.AsTheGameStarts, chosenSize = s_choice.Width > 0 && s_choice.Height > 0;
                FullScreenMode mode = !chosenMode ? Screen.fullScreenMode : s_choice.DisplayMode == GraphicsSettingsValues.Windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow;
                if (chosenSize) Screen.SetResolution(s_choice.Width, s_choice.Height, mode);
                else if (chosenMode && Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
            }
            if (s_choice.ShowFrameRate) FrameRateCounter.Ensure();
            else FrameRateCounter.Remove();
        }

        // ---------------- Words ----------------

        /// <summary>A graphics line's value as words (strings made once; the screen asks for them as it lays out).</summary>
        public static string ValueWords(SettingLine line)
        {
            GraphicsChoice c = Choice;
            switch (line)
            {
                case SettingLine.DisplayMode: return GraphicsRules.DisplayModeWords[DisplayModeNow];
                case SettingLine.Resolution:
                    MakeSizes();
                    return s_sizeWords[SizeAt];
                case SettingLine.VSync: return VSyncOn ? GraphicsRules.On : GraphicsRules.Off;
                case SettingLine.FrameCap: return GraphicsRules.FrameCapWords[GraphicsRules.Step(c.FrameCap, GraphicsSettingsValues.FrameCaps.Length, GraphicsSettingsValues.DefaultFrameCap)];
                case SettingLine.Quality: return GraphicsRules.QualityWords[(int)Quality];
                case SettingLine.AntiAliasing: return GraphicsRules.AntiAliasingWords[c.AntiAliasing];
                case SettingLine.Shadows: return GraphicsRules.ShadowsWords[(int)c.Shadows];
                case SettingLine.RenderScale: return GraphicsRules.RenderScaleWords[GraphicsRules.Step(c.RenderScale, GraphicsSettingsValues.RenderScalePercent.Length, GraphicsSettingsValues.DefaultRenderScale)];
                case SettingLine.Glow: return c.Glow ? GraphicsRules.On : GraphicsRules.Off;
                case SettingLine.Brightness: return GraphicsRules.BrightnessWord(c.Brightness);
                case SettingLine.ShowFrameRate: return c.ShowFrameRate ? GraphicsRules.On : GraphicsRules.Off;
                default: return "";
            }
        }
    }
}
