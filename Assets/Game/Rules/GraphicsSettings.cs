using System;
using System.Collections.Generic;
using System.Globalization;

namespace Roguelike.Rules
{
    /// <summary>
    /// The settings screen's switch (work order 128). Kristian, 2026-11-25: "kan du legge til settings og implimentere standard
    /// grafikk instillinger også? vet at det finnes en haug av de. hvilken er hensiktsmessig åta med?" and "også må du gjerne rydde
    /// opp i ESC menyen (fordele de på grafikk instillinger/lyd osv) trenger også en egen settings retning i start menuen".
    /// On: the Esc menu's top list is short (Resume, New run, Same run again, Settings, Leave, Quit) and every setting lives on a
    /// page of one settings screen (Graphics, Sound, Gameplay, Trials), reached from the Esc menu and from the start menu's
    /// SETTINGS line; the graphics settings are saved and applied. Off: the Esc menu is the long list of before, the start menu has
    /// no SETTINGS line, and nothing new is applied.
    /// </summary>
    public static class SettingsScreenValues
    {
        /// <summary>The switch. On by default (his words above).</summary>
        public static bool On = true;

        /// <summary>The screen's sizes and words.</summary>
        public static readonly SettingsScreenLook Look = new SettingsScreenLook();
    }

    /// <summary>The four pages of the settings screen, in the order shown (work order 128: "fordele de på grafikk instillinger/lyd osv").</summary>
    public enum SettingsPage { Graphics, Sound, Gameplay, Trials }

    /// <summary>
    /// Every line of the settings screen. The first eleven are the graphics settings (work order 128's table, the producer's choice
    /// of "which are sensible"); the rest are the Esc menu's settings as they were, moved to their pages with their saved values
    /// and keys unchanged.
    /// </summary>
    public enum SettingLine
    {
        DisplayMode, Resolution, VSync, FrameCap, Quality, AntiAliasing, Shadows, RenderScale, Glow, Brightness, ShowFrameRate,
        Volume, Music, MusicVolume,
        DashDirection, DamageNumbers, CameraKick, CameraDistance,
        Attachments, Greybox, Light, ToonLook, BetweenRuns, CameraAngle,
        TimingCue, TimingClick, // the merge of 2026-11-26: work order 134's two choices on the Trials page (TimingCueLines)
        Precision, // work order 143: Off, Inertia, Meter, Ghost on the Trials page (PrecisionLines), with its switch on
        SightOpening, // work order 144: the sight's opening, the brackets or the whole ring, on the Trials page (SightLines), with its switch on
        LineUps, // work order 135: the rooms' line-ups on or off, on the Trials page (RoomTrialLines); the merge of 2026-11-27 (W2) put it after 143's and 144's
        Waves, // work order 136: the rooms' waves on or off, on the Trials page (RoomTrialLines)
    }

    /// <summary>The quality presets (work order 128): each sets the four lines under it; a hand-made mix of them reads Custom.</summary>
    public enum QualityPreset { Low, Medium, High, Custom }

    /// <summary>The shadows' steps: none, the sun's shadow map smaller with fewer cascades, or today's.</summary>
    public enum ShadowsStep { Off, Low, High }

    /// <summary>
    /// The graphics settings chosen (work order 128), as numbers with no engine. A display line at <see cref="GraphicsSettingsValues.AsTheGameStarts"/>
    /// has never been chosen: the game leaves it as it starts.
    /// </summary>
    public struct GraphicsChoice
    {
        /// <summary>-1 as the game starts; else <see cref="GraphicsSettingsValues.Fullscreen"/> or <see cref="GraphicsSettingsValues.Windowed"/>.</summary>
        public int DisplayMode;
        /// <summary>The window's size; 0 by 0: the display's own.</summary>
        public int Width, Height;
        /// <summary>-1 as the game starts (today: off, the quality level's own 0); 0 off; 1 on.</summary>
        public int VSync;
        /// <summary>A step of <see cref="GraphicsSettingsValues.FrameCaps"/>.</summary>
        public int FrameCap;
        /// <summary>A step of <see cref="GraphicsSettingsValues.AntiAliasingSamples"/>.</summary>
        public int AntiAliasing;
        public ShadowsStep Shadows;
        /// <summary>A step of <see cref="GraphicsSettingsValues.RenderScalePercent"/>.</summary>
        public int RenderScale;
        public bool Glow;
        /// <summary>Steps of <see cref="GraphicsSettingsValues.BrightnessStepEv"/> from today's picture, -<see cref="GraphicsSettingsValues.BrightnessMostSteps"/> to +.</summary>
        public int Brightness;
        public bool ShowFrameRate;
    }

    /// <summary>
    /// The graphics settings' named values (work order 128): the steps, the defaults, the presets' table. The defaults are today's
    /// picture exactly ("High must be exactly today's picture"): 4 samples (the URP asset's own since work order 125), today's
    /// shadows, the full render scale, the glow on, today's exposure, V-Sync and the display as the game starts them, no cap.
    /// </summary>
    public static class GraphicsSettingsValues
    {
        public const int AsTheGameStarts = -1;
        public const int Fullscreen = 0;
        public const int Windowed = 1;

        /// <summary>The frame-rate caps offered; the last, -1, is none ("unlimited"): the engine's own default, today's.</summary>
        public static readonly int[] FrameCaps = { 30, 60, 120, 144, 240, -1 };
        public const int DefaultFrameCap = 5;

        /// <summary>Multisampling's samples a pixel: off, 2x, 4x, 8x (work order 125's line, grown from on and off).</summary>
        public static readonly int[] AntiAliasingSamples = { 1, 2, 4, 8 };
        public const int AntiAliasingOff = 0;
        /// <summary>4x: what work order 125's "on" was (<see cref="AntiAliasingValues.MsaaSamples"/>), the default.</summary>
        public const int DefaultAntiAliasing = 2;

        /// <summary>The render scale in percent, 50 to 100 in steps of 10; 100 % (today's) by default.</summary>
        public static readonly int[] RenderScalePercent = { 50, 60, 70, 80, 90, 100 };
        public const int DefaultRenderScale = 5;

        /// <summary>
        /// Brightness: steps of the picture's exposure (URP's post exposure, in stops) from today's. Today's is
        /// <see cref="TodayExposure"/>: the scene's profile (Assets/Game/Settings/PostProcessing.asset) sets none and the URP
        /// default profile's is 0. Three steps of 0.15 stop either way: a small range (about 37 % brighter or darker at the ends).
        /// </summary>
        public const float BrightnessStepEv = 0.15f;
        public const int BrightnessMostSteps = 3;
        public const float TodayExposure = 0f;

        /// <summary>
        /// Shadows "low": the sun's shadow map (the only light that casts one: every lamp of the floors casts none) at this size and
        /// this many cascades, where today's is the URP asset's 2048 and 4 (Assets/SourceFiles/Settings/PC_RPAsset.asset).
        /// </summary>
        public const int LowShadowResolution = 1024;
        public const int LowShadowCascades = 2;

        /// <summary>No window smaller than this is offered (work order 128: "never under 1280 by 720").</summary>
        public const int LeastWidth = 1280;
        public const int LeastHeight = 720;

        /// <summary>The frame-rate number in its corner: the frames counted over this long, then shown (twice a second).</summary>
        public const float FrameRateWindowSeconds = 0.5f;
        /// <summary>The highest number it shows (more shows as this).</summary>
        public const int FrameRateMost = 999;

        /// <summary>
        /// The presets' table (the producer's choice, told to him): each sets anti-aliasing, shadows, render scale and the glow.
        /// High is today's picture; Medium halves the multisampling and the sun's shadow map and draws at 90 %; Low takes the
        /// multisampling, the shadows and the glow away and draws at 70 %, scaled up.
        /// </summary>
        public static readonly PresetRow[] Presets =
        {
            new PresetRow(QualityPreset.Low, AntiAliasingOff, ShadowsStep.Off, 2, false),
            new PresetRow(QualityPreset.Medium, 1, ShadowsStep.Low, 4, true),
            new PresetRow(QualityPreset.High, DefaultAntiAliasing, ShadowsStep.High, DefaultRenderScale, true),
        };

        /// <summary>The settings as nobody has chosen any: today's picture and the display as the game starts it.</summary>
        public static GraphicsChoice Defaults() => new GraphicsChoice
        {
            DisplayMode = AsTheGameStarts,
            Width = 0,
            Height = 0,
            VSync = AsTheGameStarts,
            FrameCap = DefaultFrameCap,
            AntiAliasing = DefaultAntiAliasing,
            Shadows = ShadowsStep.High,
            RenderScale = DefaultRenderScale,
            Glow = true,
            Brightness = 0,
            ShowFrameRate = false,
        };
    }

    /// <summary>A preset's four values.</summary>
    public readonly struct PresetRow
    {
        public readonly QualityPreset Preset;
        public readonly int AntiAliasing;
        public readonly ShadowsStep Shadows;
        public readonly int RenderScale;
        public readonly bool Glow;

        public PresetRow(QualityPreset preset, int antiAliasing, ShadowsStep shadows, int renderScale, bool glow)
        {
            Preset = preset;
            AntiAliasing = antiAliasing;
            Shadows = shadows;
            RenderScale = renderScale;
            Glow = glow;
        }
    }

    /// <summary>A window size the display offers.</summary>
    public readonly struct ScreenSize : IEquatable<ScreenSize>
    {
        public readonly int Width, Height;

        public ScreenSize(int width, int height)
        {
            Width = width;
            Height = height;
        }

        public bool Equals(ScreenSize o) => Width == o.Width && Height == o.Height;
        public override bool Equals(object obj) => obj is ScreenSize o && Equals(o);
        public override int GetHashCode() => Width * 65536 + Height;
    }

    /// <summary>
    /// How the graphics settings step and read (work order 128), with no engine: each line's step left or right, the quality that
    /// the four lines make, a preset's values put on them, work order 125's old switch read as a step, the display's sizes cleaned
    /// into the list offered, and every value's words (made once: the screen asks for them every frame it is up).
    /// </summary>
    public static class GraphicsRules
    {
        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;

        static int Clamp(int v, int lo, int hi) => v < lo ? lo : v > hi ? hi : v;

        /// <summary>A stored step read back: within the list, else the default (a damaged or older value never throws).</summary>
        public static int Step(int stored, int count, int fallback) => stored >= 0 && stored < count ? stored : fallback;

        /// <summary>One step along a list of <paramref name="count"/>, stopping at either end (as the volume does).</summary>
        public static int StepAlong(int at, int delta, int count) => Clamp(Clamp(at, 0, count - 1) + Math.Sign(delta), 0, count - 1);

        /// <summary>
        /// Work order 125's line had only on and off, kept for the session in <see cref="AntiAliasingValues.On"/> (never saved): with
        /// nothing saved by this order yet, "on" reads as 4x and "off" as off.
        /// </summary>
        public static int AntiAliasingFromOldSwitch(bool on) => on ? GraphicsSettingsValues.DefaultAntiAliasing : GraphicsSettingsValues.AntiAliasingOff;

        /// <summary>The quality the four lines make: the preset whose values they all are, else Custom.</summary>
        public static QualityPreset QualityOf(int antiAliasing, ShadowsStep shadows, int renderScale, bool glow)
        {
            foreach (PresetRow p in GraphicsSettingsValues.Presets)
                if (p.AntiAliasing == antiAliasing && p.Shadows == shadows && p.RenderScale == renderScale && p.Glow == glow) return p.Preset;
            return QualityPreset.Custom;
        }

        public static QualityPreset QualityOf(GraphicsChoice c) => QualityOf(c.AntiAliasing, c.Shadows, c.RenderScale, c.Glow);

        /// <summary>
        /// The quality line stepped: Low, Medium, High, stopping at the ends. From Custom either arrow goes to High, today's picture
        /// (Custom is never chosen: it is what a hand-made mix reads).
        /// </summary>
        public static QualityPreset StepQuality(QualityPreset now, int delta)
        {
            if (now == QualityPreset.Custom) return QualityPreset.High;
            return (QualityPreset)Clamp((int)now + Math.Sign(delta), (int)QualityPreset.Low, (int)QualityPreset.High);
        }

        /// <summary>The choice with a preset's four values put on it (Custom changes nothing).</summary>
        public static GraphicsChoice WithPreset(GraphicsChoice c, QualityPreset preset)
        {
            foreach (PresetRow p in GraphicsSettingsValues.Presets)
                if (p.Preset == preset)
                {
                    c.AntiAliasing = p.AntiAliasing;
                    c.Shadows = p.Shadows;
                    c.RenderScale = p.RenderScale;
                    c.Glow = p.Glow;
                }
            return c;
        }

        /// <summary>Does the frame-rate cap act now? Not while V-Sync is on (V-Sync paces the frames; the line is greyed).</summary>
        public static bool CapActs(bool vSyncOn) => !vSyncOn;

        /// <summary>The frames a second the engine is asked for: the cap's, or -1 (none) while V-Sync is on or with no cap.</summary>
        public static int TargetFrameRate(GraphicsChoice c, bool vSyncOn) =>
            CapActs(vSyncOn) ? GraphicsSettingsValues.FrameCaps[Step(c.FrameCap, GraphicsSettingsValues.FrameCaps.Length, GraphicsSettingsValues.DefaultFrameCap)] : -1;

        public static int Samples(int step) => GraphicsSettingsValues.AntiAliasingSamples[Step(step, GraphicsSettingsValues.AntiAliasingSamples.Length, GraphicsSettingsValues.DefaultAntiAliasing)];
        public static float RenderScale(int step) => GraphicsSettingsValues.RenderScalePercent[Step(step, GraphicsSettingsValues.RenderScalePercent.Length, GraphicsSettingsValues.DefaultRenderScale)] / 100f;
        public static float Exposure(int brightness) => GraphicsSettingsValues.TodayExposure + Clamp(brightness, -GraphicsSettingsValues.BrightnessMostSteps, GraphicsSettingsValues.BrightnessMostSteps) * GraphicsSettingsValues.BrightnessStepEv;

        /// <summary>
        /// The window sizes offered: the display's list (it names each size once a refresh rate) with each size once, none under
        /// 1280 by 720, smallest first, and the display's own size always in it.
        /// </summary>
        public static void Sizes(IEnumerable<ScreenSize> display, ScreenSize own, List<ScreenSize> into)
        {
            into.Clear();
            void Add(ScreenSize s)
            {
                if (s.Width < GraphicsSettingsValues.LeastWidth || s.Height < GraphicsSettingsValues.LeastHeight || into.Contains(s)) return;
                into.Add(s);
            }
            if (display != null) foreach (ScreenSize s in display) Add(s);
            Add(own);
            into.Sort((a, b) => a.Width != b.Width ? a.Width.CompareTo(b.Width) : a.Height.CompareTo(b.Height));
        }

        /// <summary>Where a size stands in the list offered: its place, else the display's own's, else the last.</summary>
        public static int SizeIndex(List<ScreenSize> sizes, int width, int height, ScreenSize own)
        {
            int at = sizes.IndexOf(width > 0 && height > 0 ? new ScreenSize(width, height) : own);
            if (at < 0) at = sizes.IndexOf(own);
            return at >= 0 ? at : sizes.Count - 1;
        }

        // ---------------- Words, made once ----------------

        public const string On = "on", Off = "off";
        public static readonly string[] DisplayModeWords = { "Fullscreen", "Windowed" };
        public static readonly string[] QualityWords = { "Low", "Medium", "High", "Custom" };
        public static readonly string[] ShadowsWords = { "off", "low", "high" };
        public static readonly string[] AntiAliasingWords = { "off", "2x", "4x", "8x" };
        public static readonly string[] FrameCapWords = MakeCapWords();
        public static readonly string[] RenderScaleWords = MakeScaleWords();
        public static readonly string[] BrightnessWords = MakeBrightnessWords();

        static string[] MakeCapWords()
        {
            var w = new string[GraphicsSettingsValues.FrameCaps.Length];
            for (int i = 0; i < w.Length; i++) w[i] = GraphicsSettingsValues.FrameCaps[i] < 0 ? "unlimited" : GraphicsSettingsValues.FrameCaps[i].ToString(s_Inv);
            return w;
        }

        static string[] MakeScaleWords()
        {
            var w = new string[GraphicsSettingsValues.RenderScalePercent.Length];
            for (int i = 0; i < w.Length; i++) w[i] = GraphicsSettingsValues.RenderScalePercent[i].ToString(s_Inv) + " %";
            return w;
        }

        static string[] MakeBrightnessWords()
        {
            int most = GraphicsSettingsValues.BrightnessMostSteps;
            var w = new string[2 * most + 1];
            for (int i = -most; i <= most; i++) w[i + most] = i == 0 ? "0" : (i > 0 ? "+" : "-") + Math.Abs(i).ToString(s_Inv);
            return w;
        }

        /// <summary>A size's words, "1920 x 1080".</summary>
        public static string SizeWords(ScreenSize s) => s.Width.ToString(s_Inv) + " x " + s.Height.ToString(s_Inv);

        public static string BrightnessWord(int steps) => BrightnessWords[Clamp(steps, -GraphicsSettingsValues.BrightnessMostSteps, GraphicsSettingsValues.BrightnessMostSteps) + GraphicsSettingsValues.BrightnessMostSteps];

        /// <summary>A graphics line's name as the screen shows it.</summary>
        public static string Name(SettingLine line)
        {
            switch (line)
            {
                case SettingLine.DisplayMode: return "Display mode";
                case SettingLine.Resolution: return "Resolution";
                case SettingLine.VSync: return "V-Sync";
                case SettingLine.FrameCap: return "Frame-rate cap";
                case SettingLine.Quality: return "Quality";
                case SettingLine.AntiAliasing: return "Anti-aliasing";
                case SettingLine.Shadows: return "Shadows";
                case SettingLine.RenderScale: return "Render scale";
                case SettingLine.Glow: return "Glow";
                case SettingLine.Brightness: return "Brightness";
                case SettingLine.ShowFrameRate: return "Show frame rate";
                default: return line.ToString();
            }
        }

        /// <summary>The graphics lines, the first eleven of <see cref="SettingLine"/>.</summary>
        public static bool IsGraphics(SettingLine line) => line <= SettingLine.ShowFrameRate;

        /// <summary>The lines whose value reaches the display (they cannot act in the Editor: there they show and save only).</summary>
        public static bool IsDisplay(SettingLine line) => line == SettingLine.DisplayMode || line == SettingLine.Resolution;
    }

    /// <summary>Which lines stand on which page, in order (work order 128's list).</summary>
    public static class SettingsPages
    {
        public static readonly SettingsPage[] All = { SettingsPage.Graphics, SettingsPage.Sound, SettingsPage.Gameplay, SettingsPage.Trials };
        public static readonly string[] Words = { "GRAPHICS", "SOUND", "GAMEPLAY", "TRIALS" };

        /// <summary>
        /// A page's lines. Sound: what exists today (the master volume, the music on or off and its volume; no effects volume: no
        /// one place sets every effect's loudness). Trials: the trials and the developer's tools, and work order 121's camera angle
        /// (he likes ours best: off the main list, the other three still here) while its switch is on, then work order 134's timing
        /// cue and its click (the merge of 2026-11-26).
        /// </summary>
        public static void Lines(SettingsPage page, bool cameraAngles, List<SettingLine> into)
        {
            into.Clear();
            switch (page)
            {
                case SettingsPage.Graphics:
                    for (SettingLine l = SettingLine.DisplayMode; l <= SettingLine.ShowFrameRate; l++) into.Add(l);
                    break;
                case SettingsPage.Sound:
                    into.Add(SettingLine.Volume);
                    into.Add(SettingLine.Music);
                    into.Add(SettingLine.MusicVolume);
                    break;
                case SettingsPage.Gameplay:
                    into.Add(SettingLine.DashDirection);
                    into.Add(SettingLine.DamageNumbers);
                    into.Add(SettingLine.CameraKick);
                    into.Add(SettingLine.CameraDistance);
                    break;
                default:
                    into.Add(SettingLine.Attachments);
                    into.Add(SettingLine.Greybox);
                    into.Add(SettingLine.Light);
                    into.Add(SettingLine.ToonLook);
                    into.Add(SettingLine.BetweenRuns);
                    if (cameraAngles) into.Add(SettingLine.CameraAngle);
                    into.Add(SettingLine.TimingCue);   // the merge of 2026-11-26: work order 134's cue at the sight,
                    into.Add(SettingLine.TimingClick); // and its cocking click, last on the page (TimingCueLines)
                    if (PrecisionValues.On) into.Add(SettingLine.Precision); // work order 143, after them
                    if (SightValues.On) into.Add(SettingLine.SightOpening); // work order 144, after it
                    into.Add(SettingLine.LineUps);     // work order 135: the rooms' line-ups (RoomTrialLines), after them (the merge of 2026-11-27, W2)
                    into.Add(SettingLine.Waves);       // work order 136: the rooms' waves (RoomTrialLines), last
                    break;
            }
        }

        /// <summary>The page a line stands on.</summary>
        public static SettingsPage PageOf(SettingLine line) =>
            line <= SettingLine.ShowFrameRate ? SettingsPage.Graphics
            : line <= SettingLine.MusicVolume ? SettingsPage.Sound
            : line <= SettingLine.CameraDistance ? SettingsPage.Gameplay
            : SettingsPage.Trials;
    }

    /// <summary>Where the settings screen was opened from: Esc goes back there from its list of pages.</summary>
    public enum SettingsFrom { PauseMenu, StartMenu }

    /// <summary>
    /// The settings screen's flow (work order 128), with no engine: it opens on its list of pages; Enter opens a page; Esc goes back
    /// one step, from a page to the list and from the list to where it was opened.
    /// </summary>
    public static class SettingsFlow
    {
        /// <summary>Esc on a page (<paramref name="onPage"/>) goes to the list (true); on the list it closes the screen (false).</summary>
        public static bool BackStaysOpen(bool onPage) => onPage;
    }

    /// <summary>
    /// The settings screen's sizes and words (work order 128), in the interface's 1080-high layout. Its list of pages is the start
    /// menu's main list (<see cref="StartMenuValues.MainLeft"/> and the rest) and its tabs the start menu's; the pages' rows are
    /// these. First guesses; at 1280 by 720 the whole is drawn smaller in the same shape.
    /// </summary>
    [Serializable]
    public sealed class SettingsScreenLook
    {
        /// <summary>The draw order: over the start menu and the HUD's screens.</summary>
        public int Depth = InterfaceDepth.Screens - 2;
        /// <summary>The veil over the flat backdrop under the pages opened from the Esc menu when no start menu is installed (with it, its own backdrop and veil, <see cref="StartMenuValues.PanelVeil"/>); the backdrop is opaque either way.</summary>
        public float GameVeil = 0.88f;
        /// <summary>The page's heading plate: its top and width (its height the pause menu's, <see cref="MenuValues.HeadingHeight"/>).</summary>
        public float HeadTop = 104f;
        public float HeadWidth = 560f;
        /// <summary>The rows of a page: the first one's place, a row's size and the gap.</summary>
        public float RowsLeft = 120f;
        public float RowsTop = 250f;
        public float RowsWidth = 1180f;
        public float RowHeight = 58f;
        public float RowGap = 9f;
        /// <summary>
        /// Layout pixels kept between a page's last row and the keys' line at the foot. A page with more lines than fit above it at
        /// these sizes draws its rows, their gaps and their words smaller by one share, the least that keeps this room (the merge of
        /// 2026-11-27, W2: the Trials page's twelve lines, work orders 134, 143, 144, 135 and 136, ran 41 px into the keys' line;
        /// eleven lines fit at the full size, as the Graphics page's eleven do, and every page had at most that until then).
        /// </summary>
        public float KeysClear = 10f;
        /// <summary>How far the chosen row stands out to the right (the pause menu's is <see cref="MenuValues.SelectedShift"/>).</summary>
        public float SelectedShift = 18f;
        /// <summary>The words: a line's name, its value (heavy), and the small note after the name.</summary>
        public int NameSize = 30;
        public int ValueSize = 32;
        public int NoteSize = 19;
        /// <summary>The room at a row's right for its value between its two arrows, and an arrow's width.</summary>
        public float ValueRoom = 390f;
        public float Chevron = 28f;
        /// <summary>A greyed line's words and plate (the frame-rate cap while V-Sync is on).</summary>
        public float Greyed = 0.38f;

        public string Heading = "SETTINGS";
        /// <summary>The note on the display's lines in the Editor (they show and save there but cannot act).</summary>
        public string EditorNote = "in the Editor: saved, applied in the built game";
        /// <summary>The note on the frame-rate cap while V-Sync is on.</summary>
        public string GreyedNote = "V-Sync paces the frames";
        /// <summary>The Esc menu's "Volume" as the Sound page names it, beside the music's own volume (the same line, its key as before).</summary>
        public string MasterVolumeName = "Master volume";
    }
}
