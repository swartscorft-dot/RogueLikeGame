using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 114's rules (the start and main menu): how the screens follow each other and step back, the main menu's lines,
    /// that only her slot may be chosen, how her picture covers a screen without stretching about its named anchor, the panels'
    /// columns; and his records: what a run adds, their file written and read back, a missing or older file read as zeros, a
    /// damaged one kept aside, a newer one never written over. Every file in a temporary folder of the test's own.
    /// </summary>
    public class StartMenuRulesTests
    {
        string _folder;

        [SetUp]
        public void MakeFolder()
        {
            _folder = Path.Combine(Path.GetTempPath(), "StartMenuRulesTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);
        }

        [TearDown]
        public void RemoveFolder()
        {
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
        }

        // ---------------- The flow ----------------

        [Test]
        public void TheMainMenu_HasStartRunUpgradesStats_AndQuitOnlyWhereOffered()
        {
            var lines = new List<MainLine>();
            StartMenuFlow.MainLines(false, lines);
            Assert.That(lines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats }));
            StartMenuFlow.MainLines(true, lines);
            Assert.That(lines, Is.EqualTo(new[] { MainLine.StartRun, MainLine.Upgrades, MainLine.Stats, MainLine.Quit }));
        }

        [Test]
        public void StartRun_LeadsToTheCharacter_TheTabsToTheirScreens_AndEscapeStepsBackOneAtATime()
        {
            Assert.That(StartMenuFlow.Pick(MainLine.StartRun), Is.EqualTo(MenuScreen.Character));
            Assert.That(StartMenuFlow.Pick(MainLine.Upgrades), Is.EqualTo(MenuScreen.Upgrades));
            Assert.That(StartMenuFlow.Pick(MainLine.Stats), Is.EqualTo(MenuScreen.Stats));
            Assert.That(StartMenuFlow.Pick(MainLine.Quit), Is.EqualTo(MenuScreen.None));
            Assert.That(StartMenuFlow.Back(MenuScreen.Loadout), Is.EqualTo(MenuScreen.Character));
            Assert.That(StartMenuFlow.Back(MenuScreen.Character), Is.EqualTo(MenuScreen.Main));
            Assert.That(StartMenuFlow.Back(MenuScreen.Upgrades), Is.EqualTo(MenuScreen.Main));
            Assert.That(StartMenuFlow.Back(MenuScreen.Stats), Is.EqualTo(MenuScreen.Main));
            Assert.That(StartMenuFlow.Back(MenuScreen.Main), Is.EqualTo(MenuScreen.None), "the main menu's Escape: the practice room");
            Assert.That(StartMenuFlow.TabOf(MenuScreen.Loadout), Is.EqualTo(0));
            Assert.That(StartMenuFlow.TabScreen(1), Is.EqualTo(MenuScreen.Upgrades));
            Assert.That(StartMenuFlow.UsesBetweenRuns(MenuScreen.Loadout) && StartMenuFlow.UsesBetweenRuns(MenuScreen.Upgrades), Is.True);
            Assert.That(StartMenuFlow.UsesBetweenRuns(MenuScreen.Stats), Is.False);
        }

        [Test]
        public void TheArrows_GoRoundTheList_AndOnlyHerSlotMayBeChosen()
        {
            Assert.That(StartMenuFlow.Step(0, -1, 4), Is.EqualTo(3));
            Assert.That(StartMenuFlow.Step(3, 1, 4), Is.EqualTo(0));
            Assert.That(StartMenuFlow.Step(1, 1, 4), Is.EqualTo(2));
            Assert.That(StartMenuFlow.Step(5, 1, 0), Is.EqualTo(0));
            Assert.That(StartMenuFlow.MayChoose(0), Is.True, "hers");
            for (int slot = 1; slot < new StartMenuValues().Slots; slot++) Assert.That(StartMenuFlow.MayChoose(slot), Is.False, "a dark slot refuses: " + slot);
        }

        [Test]
        public void HerWords_AreTheDocuments_AndTheSwitchIsOn_AndTheEditorsSkipOff()
        {
            var s = new StartMenuValues();
            Assert.That(s.Name, Is.EqualTo("GUNSLINGER"), "what her picture calls her, his tick of 2026-11-25 (\"Bytt SHOOTER til Gunslinger\"; it said SHOOTER before); the documents give her no name of her own");
            Assert.That(s.Lines.Length, Is.InRange(2, 3));
            Assert.That(s.PictureName, Is.EqualTo("UI/Menu/Character_Gunslinger"));
            var m = new MenuValues();
            Assert.That(m.StartMenu, Is.True);
            Assert.That(m.SkipInEditor, Is.False);
        }

        // ---------------- The picture covers the screen ----------------

        [Test]
        public void ThePicture_CoversTheScreen_WithoutStretching_CroppedAboutItsAnchor()
        {
            // A picture of the proposals' shape on a 16:9 screen: nearly all of it.
            UvRect same = CoverCrop.Of(1672, 941, 1920, 1080, 0.72f, 0.3f);
            Assert.That(same.W * 1672f / (same.H * 941f), Is.EqualTo(1920f / 1080f).Within(1e-3f), "the part shown has the screen's shape: no stretch");
            // A square screen: the sides are cut, about the anchor (her figure right of the middle).
            UvRect square = CoverCrop.Of(1600, 900, 1000, 1000, 0.62f, 0.3f);
            Assert.That(square.H, Is.EqualTo(1f));
            Assert.That(square.W, Is.EqualTo(900f / 1600f).Within(1e-4f));
            Assert.That(square.X + square.W * 0.5f, Is.EqualTo(0.62f).Within(1e-4f), "kept about the anchor");
            // An anchor at the edge never shows past the picture.
            UvRect edge = CoverCrop.Of(1600, 900, 1000, 1000, 1f, 0.3f);
            Assert.That(edge.X + edge.W, Is.EqualTo(1f).Within(1e-5f));
            // A picture taller than the screen: its top and foot are cut, about the anchor's height (from the top).
            UvRect tall = CoverCrop.Of(900, 1600, 1000, 1000, 0.5f, 0.3f);
            Assert.That(tall.W, Is.EqualTo(1f));
            Assert.That(tall.W * 900f / (tall.H * 1600f), Is.EqualTo(1f).Within(1e-3f));
            Assert.That(1f - tall.Y - tall.H * 0.5f, Is.EqualTo(0.3f).Within(1e-4f), "kept about the anchor's height");
            Assert.That(tall.Y, Is.GreaterThanOrEqualTo(0f));
            Assert.That(tall.Y + tall.H, Is.LessThanOrEqualTo(1f + 1e-5f));
            // Her slot's portrait: the slot's shape, inside the picture.
            UvRect p = CoverCrop.Portrait(1672, 941, 108, 108, 0.68f, 0.2f, 0.34f);
            Assert.That(p.W * 1672f, Is.EqualTo(p.H * 941f).Within(0.5f), "square, as the slot");
            Assert.That(p.X >= 0f && p.X + p.W <= 1f && p.Y >= 0f && p.Y + p.H <= 1f + 1e-5f, Is.True);
        }

        [Test]
        public void APanel_TakesColumnsToKeepItsRowsFew()
        {
            var s = new StartMenuValues { MostRows = 4, MostColumns = 3 };
            Assert.That(MenuPanels.Columns(0, s), Is.EqualTo(1));
            Assert.That(MenuPanels.Columns(4, s), Is.EqualTo(1));
            Assert.That(MenuPanels.Columns(5, s), Is.EqualTo(2));
            Assert.That(MenuPanels.Columns(10, s), Is.EqualTo(3));
            Assert.That(MenuPanels.Columns(30, s), Is.EqualTo(3), "never more than the most");
            Assert.That(MenuPanels.InnerHeight(4, 1, 3, 100f, 10f, s), Is.EqualTo(4 * 110f - 10f + 3 * s.SlotHead));
            Assert.That(MenuPanels.InnerHeight(5, 2, 3, 100f, 10f, s), Is.EqualTo(3 * 110f - 10f), "headings only in one column");
        }

        // ---------------- The records ----------------

        [Test]
        public void ARun_AddsToTheRecords()
        {
            var r = new RunRecords();
            RecordsRules.NoteStarted(r);
            RecordsRules.NoteEnded(r, RunOutcome.Died, 3, 41, 300f);
            RecordsRules.NoteStarted(r);
            RecordsRules.NoteEnded(r, RunOutcome.ActCleared, 5, 120, 900.5f);
            RecordsRules.NoteStarted(r);
            RecordsRules.NoteEnded(r, RunOutcome.Left, 2, 7, 60f);
            Assert.That(r.RunsStarted, Is.EqualTo(3));
            Assert.That(r.Deaths, Is.EqualTo(1));
            Assert.That(r.ActsCleared, Is.EqualTo(1));
            Assert.That(r.DeepestFloor, Is.EqualTo(5));
            Assert.That(r.Kills, Is.EqualTo(168));
            Assert.That(r.LongestRunSeconds, Is.EqualTo(900.5f));
            var names = new List<string>();
            var values = new List<string>();
            RecordsRules.Lines(r, null, "Echoes", names, values);
            Assert.That(names, Is.EqualTo(new[] { "Runs started", "Deaths", "Acts cleared", "Deepest floor", "Kills", "Longest run", "Echoes earned in all", "Feats" }));
            Assert.That(values[3], Is.EqualTo("floor 5"));
            Assert.That(values[6], Is.EqualTo("trial off"), "the trial's own record, not kept twice");
            var meta = new MetaState { EarnedInAll = 230 };
            meta.Feats.Add("first-floor");
            RecordsRules.Lines(r, meta, "Echoes", names, values);
            Assert.That(values[6], Is.EqualTo("230"));
            Assert.That(values[7], Does.StartWith("1 of "));
        }

        [Test]
        public void TheRecordsFile_IsWrittenAndReadBack_AndAMissingOrOlderFileReadsAsZeros()
        {
            string path = Path.Combine(_folder, "records.json");
            RunRecords missing = RecordsFile.Load(path, out RecordsLoad how, out _);
            Assert.That(how, Is.EqualTo(RecordsLoad.Missing));
            Assert.That(missing.RunsStarted + missing.Deaths + missing.Kills + missing.DeepestFloor, Is.Zero);

            var r = new RunRecords { RunsStarted = 4, Deaths = 3, ActsCleared = 1, DeepestFloor = 5, Kills = 210, LongestRunSeconds = 812.25f };
            Assert.That(RecordsFile.Save(path, r, out string error), Is.True, error);
            RunRecords back = RecordsFile.Load(path, out how, out _);
            Assert.That(how, Is.EqualTo(RecordsLoad.Read));
            Assert.That(back.RunsStarted, Is.EqualTo(4));
            Assert.That(back.Deaths, Is.EqualTo(3));
            Assert.That(back.ActsCleared, Is.EqualTo(1));
            Assert.That(back.DeepestFloor, Is.EqualTo(5));
            Assert.That(back.Kills, Is.EqualTo(210));
            Assert.That(back.LongestRunSeconds, Is.EqualTo(812.25f).Within(1e-3f));
            Assert.That(File.Exists(path + ".tmp"), Is.False, "the temporary file went into place");

            // An older file, written before some of the records were kept: what it lacks is zero.
            File.WriteAllText(path, "{ \"runsStarted\": 2, \"someLaterWords\": \"x\" }");
            RunRecords older = RecordsFile.Load(path, out how, out _);
            Assert.That(how, Is.EqualTo(RecordsLoad.Read));
            Assert.That(older.RunsStarted, Is.EqualTo(2));
            Assert.That(older.Deaths + older.ActsCleared + older.DeepestFloor + older.Kills, Is.Zero);
            Assert.That(older.LongestRunSeconds, Is.Zero);
            Assert.That(RecordsFile.MayWrite(how), Is.True);
        }

        [Test]
        public void ADamagedRecordsFile_IsKeptAside_AndANewerOneIsNeverWrittenOver()
        {
            string path = Path.Combine(_folder, "records.json");
            File.WriteAllText(path, "not json at all");
            RunRecords damaged = RecordsFile.Load(path, out RecordsLoad how, out string keptAs);
            Assert.That(how, Is.EqualTo(RecordsLoad.Damaged));
            Assert.That(damaged.RunsStarted, Is.Zero);
            Assert.That(keptAs, Is.Not.Empty);
            Assert.That(File.Exists(keptAs), Is.True, "kept, never deleted");
            Assert.That(File.Exists(path), Is.False);
            Assert.That(RecordsFile.MayWrite(how), Is.True);

            File.WriteAllText(path, "{ \"version\": 99, \"runsStarted\": 7 }");
            RecordsFile.Load(path, out how, out _);
            Assert.That(how, Is.EqualTo(RecordsLoad.Newer));
            Assert.That(RecordsFile.MayWrite(how), Is.False, "a newer game's file is left as it is");
        }
    }
}
