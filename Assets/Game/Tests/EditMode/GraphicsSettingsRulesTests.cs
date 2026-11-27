using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 128's rules: the presets' table and Custom, the steps of each graphics line and where they stop, work order 125's
    /// old switch read as a step, the display's sizes cleaned into the list offered, the pages' lines, the words made once, and the
    /// switch (on by default).
    /// </summary>
    public class GraphicsSettingsRulesTests
    {
        [Test]
        public void TheSwitch_IsOnByDefault()
        {
            Assert.That(SettingsScreenValues.On, Is.True);
        }

        [Test]
        public void TheDefaults_AreTodaysPicture_AndReadAsHigh()
        {
            GraphicsChoice d = GraphicsSettingsValues.Defaults();
            Assert.That(GraphicsRules.Samples(d.AntiAliasing), Is.EqualTo(AntiAliasingValues.MsaaSamples), "4 samples: the URP asset's own since work order 125");
            Assert.That(d.Shadows, Is.EqualTo(ShadowsStep.High));
            Assert.That(GraphicsRules.RenderScale(d.RenderScale), Is.EqualTo(1f), "the full render scale");
            Assert.That(d.Glow, Is.True);
            Assert.That(GraphicsRules.Exposure(d.Brightness), Is.EqualTo(GraphicsSettingsValues.TodayExposure), "today's exposure");
            Assert.That(d.DisplayMode, Is.EqualTo(GraphicsSettingsValues.AsTheGameStarts), "the display as the game starts it");
            Assert.That(d.VSync, Is.EqualTo(GraphicsSettingsValues.AsTheGameStarts));
            Assert.That(d.Width, Is.Zero, "the display's own size");
            Assert.That(GraphicsRules.TargetFrameRate(d, false), Is.EqualTo(-1), "no cap: the engine's own default");
            Assert.That(d.ShowFrameRate, Is.False);
            Assert.That(GraphicsRules.QualityOf(d), Is.EqualTo(QualityPreset.High), "High is today's look");
        }

        [Test]
        public void ThePresets_SetTheirFourLines_AndEachReadsAsItself()
        {
            GraphicsChoice c = GraphicsSettingsValues.Defaults();
            c = GraphicsRules.WithPreset(c, QualityPreset.Low);
            Assert.That(c.AntiAliasing, Is.EqualTo(GraphicsSettingsValues.AntiAliasingOff));
            Assert.That(c.Shadows, Is.EqualTo(ShadowsStep.Off));
            Assert.That(GraphicsRules.RenderScale(c.RenderScale), Is.EqualTo(0.7f).Within(1e-5f));
            Assert.That(c.Glow, Is.False);
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.Low));
            c = GraphicsRules.WithPreset(c, QualityPreset.Medium);
            Assert.That(GraphicsRules.Samples(c.AntiAliasing), Is.EqualTo(2));
            Assert.That(c.Shadows, Is.EqualTo(ShadowsStep.Low));
            Assert.That(GraphicsRules.RenderScale(c.RenderScale), Is.EqualTo(0.9f).Within(1e-5f));
            Assert.That(c.Glow, Is.True);
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.Medium));
            c = GraphicsRules.WithPreset(c, QualityPreset.High);
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.High));
            GraphicsChoice same = GraphicsRules.WithPreset(c, QualityPreset.Custom);
            Assert.That(same, Is.EqualTo(c), "Custom changes nothing");
        }

        [Test]
        public void TheDisplayLines_AreNotTouchedByAPreset()
        {
            GraphicsChoice c = GraphicsSettingsValues.Defaults();
            c.VSync = 1;
            c.FrameCap = 1;
            c.Brightness = 2;
            c.ShowFrameRate = true;
            c.DisplayMode = GraphicsSettingsValues.Windowed;
            GraphicsChoice low = GraphicsRules.WithPreset(c, QualityPreset.Low);
            Assert.That(low.VSync, Is.EqualTo(1));
            Assert.That(low.FrameCap, Is.EqualTo(1));
            Assert.That(low.Brightness, Is.EqualTo(2));
            Assert.That(low.ShowFrameRate, Is.True);
            Assert.That(low.DisplayMode, Is.EqualTo(GraphicsSettingsValues.Windowed));
        }

        [Test]
        public void ChangingOneOfTheFourByHand_ReadsCustom_AndTheQualityLineStepsBackToAPreset()
        {
            GraphicsChoice c = GraphicsSettingsValues.Defaults();
            c.RenderScale = 3;
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.Custom));
            c = GraphicsSettingsValues.Defaults();
            c.Glow = false;
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.Custom));
            c = GraphicsRules.WithPreset(GraphicsSettingsValues.Defaults(), QualityPreset.Low);
            c.AntiAliasing = 3;
            Assert.That(GraphicsRules.QualityOf(c), Is.EqualTo(QualityPreset.Custom));
            Assert.That(GraphicsRules.StepQuality(QualityPreset.Custom, 1), Is.EqualTo(QualityPreset.High), "from Custom: today's look");
            Assert.That(GraphicsRules.StepQuality(QualityPreset.Custom, -1), Is.EqualTo(QualityPreset.High));
            Assert.That(GraphicsRules.StepQuality(QualityPreset.High, 1), Is.EqualTo(QualityPreset.High), "stops at High");
            Assert.That(GraphicsRules.StepQuality(QualityPreset.High, -1), Is.EqualTo(QualityPreset.Medium));
            Assert.That(GraphicsRules.StepQuality(QualityPreset.Low, -1), Is.EqualTo(QualityPreset.Low), "stops at Low");
        }

        [Test]
        public void TheSteps_AreTheTables_AndStopAtTheirEnds()
        {
            Assert.That(GraphicsSettingsValues.FrameCaps, Is.EqualTo(new[] { 30, 60, 120, 144, 240, -1 }));
            Assert.That(GraphicsSettingsValues.AntiAliasingSamples, Is.EqualTo(new[] { 1, 2, 4, 8 }));
            Assert.That(GraphicsSettingsValues.RenderScalePercent, Is.EqualTo(new[] { 50, 60, 70, 80, 90, 100 }), "50 to 100 % in steps of 10");
            Assert.That(GraphicsRules.StepAlong(5, 1, 6), Is.EqualTo(5));
            Assert.That(GraphicsRules.StepAlong(0, -1, 6), Is.EqualTo(0));
            Assert.That(GraphicsRules.StepAlong(2, 1, 6), Is.EqualTo(3));
            Assert.That(GraphicsRules.StepAlong(2, -7, 6), Is.EqualTo(1), "one step whatever the size of the push");
            Assert.That(GraphicsRules.Exposure(GraphicsSettingsValues.BrightnessMostSteps + 5), Is.EqualTo(GraphicsSettingsValues.BrightnessMostSteps * GraphicsSettingsValues.BrightnessStepEv).Within(1e-5f), "a small range");
            Assert.That(GraphicsRules.Exposure(-1), Is.EqualTo(-GraphicsSettingsValues.BrightnessStepEv).Within(1e-5f));
            Assert.That(GraphicsRules.Step(17, 4, 2), Is.EqualTo(2), "a damaged stored step reads as the default");
            Assert.That(GraphicsRules.Step(-1, 4, 2), Is.EqualTo(2));
            Assert.That(GraphicsRules.Step(3, 4, 2), Is.EqualTo(3));
        }

        [Test]
        public void TheCap_IsGreyedWhileVSyncIsOn()
        {
            GraphicsChoice c = GraphicsSettingsValues.Defaults();
            c.FrameCap = 1;
            Assert.That(GraphicsRules.TargetFrameRate(c, false), Is.EqualTo(60));
            Assert.That(GraphicsRules.CapActs(true), Is.False);
            Assert.That(GraphicsRules.TargetFrameRate(c, true), Is.EqualTo(-1), "V-Sync paces the frames: no cap asked for");
        }

        [Test]
        public void WorkOrder125sSwitch_OnReadsAs4x_OffAsOff()
        {
            Assert.That(GraphicsRules.Samples(GraphicsRules.AntiAliasingFromOldSwitch(true)), Is.EqualTo(4));
            Assert.That(GraphicsRules.AntiAliasingFromOldSwitch(false), Is.EqualTo(GraphicsSettingsValues.AntiAliasingOff));
            Assert.That(GraphicsRules.Samples(GraphicsSettingsValues.AntiAliasingOff), Is.EqualTo(1), "off: one sample, as before work order 125");
        }

        [Test]
        public void TheSizes_AreTheDisplaysList_EachOnce_NoneUnder1280By720_SmallestFirst_WithItsOwn()
        {
            var display = new List<ScreenSize>
            {
                new ScreenSize(1920, 1080), new ScreenSize(1920, 1080), new ScreenSize(800, 600), new ScreenSize(1280, 720),
                new ScreenSize(1280, 1024), new ScreenSize(1600, 900), new ScreenSize(1024, 768), new ScreenSize(1280, 600),
            };
            var sizes = new List<ScreenSize>();
            GraphicsRules.Sizes(display, new ScreenSize(2560, 1440), sizes);
            Assert.That(sizes, Is.EqualTo(new[]
            {
                new ScreenSize(1280, 720), new ScreenSize(1280, 1024), new ScreenSize(1600, 900), new ScreenSize(1920, 1080), new ScreenSize(2560, 1440),
            }));
            Assert.That(GraphicsRules.SizeIndex(sizes, 0, 0, new ScreenSize(2560, 1440)), Is.EqualTo(4), "none chosen: the display's own");
            Assert.That(GraphicsRules.SizeIndex(sizes, 1600, 900, new ScreenSize(2560, 1440)), Is.EqualTo(2));
            Assert.That(GraphicsRules.SizeIndex(sizes, 1366, 768, new ScreenSize(2560, 1440)), Is.EqualTo(4), "a size no longer offered: the display's own");
            Assert.That(GraphicsRules.SizeWords(new ScreenSize(1920, 1080)), Is.EqualTo("1920 x 1080"));
        }

        [Test]
        public void ThePages_HoldTheOrdersLists()
        {
            var lines = new List<SettingLine>();
            SettingsPages.Lines(SettingsPage.Graphics, true, lines);
            Assert.That(lines, Is.EqualTo(new[]
            {
                SettingLine.DisplayMode, SettingLine.Resolution, SettingLine.VSync, SettingLine.FrameCap, SettingLine.Quality,
                SettingLine.AntiAliasing, SettingLine.Shadows, SettingLine.RenderScale, SettingLine.Glow, SettingLine.Brightness, SettingLine.ShowFrameRate,
            }));
            SettingsPages.Lines(SettingsPage.Sound, true, lines);
            Assert.That(lines, Is.EqualTo(new[] { SettingLine.Volume, SettingLine.Music, SettingLine.MusicVolume }));
            SettingsPages.Lines(SettingsPage.Gameplay, true, lines);
            Assert.That(lines, Is.EqualTo(new[] { SettingLine.DashDirection, SettingLine.DamageNumbers, SettingLine.CameraKick, SettingLine.CameraDistance }));
            SettingsPages.Lines(SettingsPage.Trials, true, lines);
            Assert.That(lines, Is.EqualTo(new[]
            {
                SettingLine.Attachments, SettingLine.Greybox, SettingLine.Light, SettingLine.ToonLook, SettingLine.BetweenRuns, SettingLine.CameraAngle,
                SettingLine.TimingCue, SettingLine.TimingClick, // the merge of 2026-11-26: work order 134's two lines
                SettingLine.Precision, // work order 143's line (with its switch on)
                SettingLine.SightOpening, // work order 144's line (with its switch on)
                SettingLine.LineUps, // work order 135 (changed deliberately): the rooms' line-ups, after 143's and 144's (the merge of 2026-11-27, W2)
                SettingLine.Waves, // work order 136 (changed deliberately): the rooms' waves, last
            }));
            // With the camera angle off the page is the same list without that one line, every other line in its order
            // (the merge of 2026-11-27, W2: named by the list, not by a count from the end, as more orders add lines after these).
            var withAngle = new List<SettingLine>(lines);
            withAngle.Remove(SettingLine.CameraAngle);
            SettingsPages.Lines(SettingsPage.Trials, false, lines);
            Assert.That(lines, Is.EqualTo(withAngle), "with the camera angle off, every other line in its place");
            Assert.That(lines, Has.No.Member(SettingLine.CameraAngle), "work order 121's switch off: no camera angle line");
            foreach (SettingsPage page in SettingsPages.All)
            {
                SettingsPages.Lines(page, true, lines);
                foreach (SettingLine l in lines) Assert.That(SettingsPages.PageOf(l), Is.EqualTo(page), l + " on its page");
            }
        }

        [Test]
        public void EveryLine_StandsOnOnePage()
        {
            var seen = new HashSet<SettingLine>();
            var lines = new List<SettingLine>();
            foreach (SettingsPage page in SettingsPages.All)
            {
                SettingsPages.Lines(page, true, lines);
                foreach (SettingLine l in lines) Assert.That(seen.Add(l), Is.True, l + " once");
            }
            Assert.That(seen.Count, Is.EqualTo(System.Enum.GetValues(typeof(SettingLine)).Length), "every line somewhere");
        }

        [Test]
        public void TheWords_AreMadeOnce()
        {
            Assert.That(GraphicsRules.FrameCapWords, Is.EqualTo(new[] { "30", "60", "120", "144", "240", "unlimited" }));
            Assert.That(GraphicsRules.RenderScaleWords[0], Is.EqualTo("50 %"));
            Assert.That(GraphicsRules.BrightnessWord(0), Is.EqualTo("0"));
            Assert.That(GraphicsRules.BrightnessWord(2), Is.EqualTo("+2"));
            Assert.That(GraphicsRules.BrightnessWord(-3), Is.EqualTo("-3"));
            Assert.That(GraphicsRules.BrightnessWord(-3), Is.SameAs(GraphicsRules.BrightnessWord(-3)), "the same string each time");
            Assert.That(GraphicsRules.Name(SettingLine.FrameCap), Is.EqualTo("Frame-rate cap"));
            Assert.That(GraphicsRules.IsGraphics(SettingLine.ShowFrameRate), Is.True);
            Assert.That(GraphicsRules.IsGraphics(SettingLine.Volume), Is.False);
            Assert.That(GraphicsRules.IsDisplay(SettingLine.Resolution), Is.True);
            Assert.That(GraphicsRules.IsDisplay(SettingLine.VSync), Is.False);
        }

        [Test]
        public void TheStartMenusMainLines_TakeSettingsBetweenStatsAndQuit_OnlyWhenAsked()
        {
            var lines = new List<MainLine>();
            StartMenuFlow.MainLines(true, true, lines);
            Assert.That(lines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Settings, MainLine.Quit }));
            StartMenuFlow.MainLines(false, true, lines);
            Assert.That(lines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Settings }));
            StartMenuFlow.MainLines(true, false, lines);
            Assert.That(lines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Quit }), "the switch off: as before");
            Assert.That(StartMenuFlow.Pick(MainLine.Settings), Is.EqualTo(MenuScreen.None), "the settings screen is its own, over the main menu");
        }

        [Test]
        public void Esc_GoesBackOneStep()
        {
            Assert.That(SettingsFlow.BackStaysOpen(true), Is.True, "a page: back to the list of pages");
            Assert.That(SettingsFlow.BackStaysOpen(false), Is.False, "the list: back to where it was opened");
        }
    }
}
