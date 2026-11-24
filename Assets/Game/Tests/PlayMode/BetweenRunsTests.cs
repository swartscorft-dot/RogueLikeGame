using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// What lasts between runs (work order 029, 2026-11-20; decision 0009, a proposal he has not ruled on), in the
    /// ControlRoom scene, through the intents. With the switch off, a whole run earns nothing, shows no screen, writes no
    /// file and leaves PlayerPrefs as they were. With it on: a run that dies on floor 2 earns what the table says and the
    /// end screen shows it; the screen between runs comes up where it should and starts a run; each piece changes what it
    /// says in a run, and only when equipped; a feat raises the budget once; the save survives making the components
    /// again; the practice room's own values are untouched after a run with pieces equipped; the pause menu's line turns
    /// it on; the recorder writes its block.
    ///
    /// The save is written only under a temporary folder of each test (<see cref="GeneratedLevels.BetweenRunsSavePath"/>),
    /// and the game's own save file is checked untouched at the end. The PlayerPrefs keys of the settings are cleared
    /// before each test and after it, and what this machine had under them is put back. A paused game has a time scale of
    /// 0, and a frame without a window is about 0.3 ms: the waits count frames or real time, never game time.
    /// </summary>
    public class BetweenRunsTests
    {
        const ulong RunSeed = 0x0E117E17UL; // an elite brute on floor 1 (SkillUpgradeTests)
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f;
        static readonly Vector3 NearA = new Vector3(2f, 0f, 1f); // Dummy_A (4, 3) is 2.8 m away here
        static readonly string[] Keys = { BetweenRunsSwitch.Key, GameSettings.DashKey, GameSettings.VolumeKey, GameSettings.DamageNumbersKey };

        // The game's own save, as it was before these tests (OneTimeSetUp) and never touched by them.
        static string s_realPath;
        static bool s_realExisted;
        static long s_realLength;
        static DateTime s_realWritten;

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        string _folder;
        string _recordings;
        PlaySessionRecorder _recorder;
        readonly Dictionary<string, int?> _prefsBefore = new Dictionary<string, int?>();

        string SavePath => Path.Combine(_folder, GeneratedLevels.BetweenRunsFileName);

        [OneTimeSetUp]
        public void RememberTheRealSave()
        {
            s_realPath = GeneratedLevels.DefaultBetweenRunsSavePath;
            s_realExisted = File.Exists(s_realPath);
            s_realLength = s_realExisted ? new FileInfo(s_realPath).Length : -1;
            s_realWritten = s_realExisted ? File.GetLastWriteTimeUtc(s_realPath) : DateTime.MinValue;
        }

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _prefsBefore.Clear();
            foreach (string k in Keys) _prefsBefore[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            ClearKeys();
            BetweenRunsSwitch.Unload();
            _folder = Path.Combine(Path.GetTempPath(), "BetweenRunsTests-" + Guid.NewGuid().ToString("N"));
            GeneratedLevels.BetweenRunsSavePath = SavePath;
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.RunValues.StatsAndTiers = false; // work order 050: a chest's health step of today (+10) on top of Thick Hide
            _levels.MetaValues.Enabled = false; // work order 117: on in code now; these tests start from off, as written, and turn it on themselves (On)
            _levels.MetaValues.Attachments = false;
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(GeneratedLevels.BetweenRunsSavePathInUse, Is.EqualTo(SavePath), "never the game's own save");
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_recordings != null && Directory.Exists(_recordings)) Directory.Delete(_recordings, true);
            _recordings = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            if (_player != null) _player.Modifiers = null;
            GameSettings.Unload();
            BetweenRunsSwitch.Unload();
            ClearKeys();
            foreach (KeyValuePair<string, int?> p in _prefsBefore)
                if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value);
            PlayerPrefs.Save();
            GeneratedLevels.BetweenRunsSavePath = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static void ClearKeys()
        {
            foreach (string k in Keys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.Save();
        }

        // ---------------- Helpers ----------------

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static IEnumerator Until(Func<bool> condition, float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!condition() && Time.realtimeSinceStartup < deadline) yield return null;
        }

        /// <summary>The switch on, through the values (the pause menu's line is its own test).</summary>
        void On() => _levels.MetaValues.Enabled = true;

        /// <summary>A save with currency to spend and every feat done (a budget of 8), as if after many runs.</summary>
        void Rich(int currency = 5000)
        {
            MetaState s = _levels.BetweenRunsState;
            s.Currency = currency;
            foreach (MetaFeat f in MetaFeats.All)
                if (!s.HasFeat(f)) s.Feats.Add(MetaFeats.Id(f));
            MetaRack.Tidy(s, _levels.MetaValues);
        }

        void BuyAndEquip(params string[] ids)
        {
            foreach (string id in ids)
            {
                if (!_levels.BetweenRunsState.Owns(id)) Assert.That(_levels.BuyPiece(id), Is.EqualTo(MetaBuy.Bought), id);
                Assert.That(_levels.EquipPiece(id), Is.EqualTo(MetaEquip.Equipped), id);
            }
        }

        /// <summary>Opens the screen from the practice room (F5), with these pieces equipped, and starts the run.</summary>
        IEnumerator RunWith(ulong? seed, params string[] ids)
        {
            Assert.That(_levels.OpenBetweenRuns(seed), Is.True, "the screen comes up");
            foreach (string id in _levels.BetweenRunsState.Equipped.ToList()) _levels.TakeOffPiece(id);
            if (ids.Length > 0) BuyAndEquip(ids);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null, "the run starts");
            yield return null;
            Assert.That(_levels.InRun, Is.True);
        }

        IEnumerator ClearTheEliteRoom()
        {
            _levels.FinishEliteRoomForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade || _levels.EliteRoomDone);
        }

        IEnumerator FinishTheFloor()
        {
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward || _levels.ActCleared);
        }

        IEnumerator Die()
        {
            _health.Untouchable = false;
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen is coming");
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
        }

        Dictionary<string, int?> Prefs() => Keys.ToDictionary(k => k, k => PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null);

        // ---------------- The switch off: no trace ----------------

        [UnityTest]
        public IEnumerator SwitchOff_AWholeRunThroughTheIntents_EarnsNothing_ShowsNoScreen_WritesNoFile_AndLeavesPlayerPrefsAsTheyWere()
        {
            Dictionary<string, int?> prefs = Prefs();
            _recordings = Path.Combine(Path.GetTempPath(), "BetweenRunsTests-rec-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_recordings);
            // Changed deliberately (work order 117): it pinned "off in code"; the trial is on in code now and this class sets it off.
            Assert.That(new MetaValues().Enabled, Is.True, "on in code since work order 117");
            Assert.That(_levels.MetaValues.Enabled, Is.False, "off, as this class sets it");
            Assert.That(_levels.MetaOn, Is.False);

            Assert.That(_levels.StartNewRun(), Is.Not.Null, "F5 starts a run at once, as before");
            Assert.That(_levels.BetweenRunsUp, Is.False);
            yield return null;
            Assert.That(_levels.Modifiers.Meta.Any, Is.False);
            Assert.That(_health.Max, Is.EqualTo(_health.MaxHealth));
            Assert.That(_player.Focus, Is.Zero);
            Assert.That(_player.DashMaxCharges, Is.EqualTo(1));

            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Assert.That(_levels.OpenChest(chest), Is.True);
            Assert.That(_levels.OfferedUpgrades.Count, Is.EqualTo(_levels.RunValues.UpgradesOffered), "the first chest as always");
            _levels.PickUpgrade(0);
            for (int floor = 1; floor <= 5; floor++)
            {
                yield return ClearTheEliteRoom();
                if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade)
                {
                    Assert.That(_levels.OfferedSkillUpgrades.Count, Is.LessThanOrEqualTo(_levels.UpgradeValues.Offered), "floor " + floor);
                    _levels.PickSkillUpgrade(0);
                }
                yield return null;
                yield return FinishTheFloor();
                if (floor < 5)
                {
                    Assert.That(_levels.CanRedraw, Is.False);
                    Assert.That(_levels.RedrawOffer(), Is.False, "no redraw");
                    Assert.That(_levels.PickReward(0), Is.True, "floor " + floor);
                    if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
                    yield return null;
                }
            }
            Assert.That(_levels.ActCleared, Is.True);
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(_levels.Earnings, Is.Null, "nothing earned");
            Assert.That(_hud.EndScreenText, Does.Not.Contain("Echoes").And.Not.Contain("Feat"));
            Assert.That(_hud.EndScreenLines.Last(), Does.StartWith("Run seed"));

            // Enter: a new run at once, no screen; then death, and F6: the same run at once.
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.False);
            yield return null;
            yield return Die();
            Assert.That(_hud.EndScreenText, Does.Not.Contain("Echoes"));
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun), Is.True);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.False);
            yield return null;
            Assert.That(_hud.BetweenRunsLines, Is.Empty);

            // The pause menu says the trial is off; the practice room as before.
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_hud.MenuText(RunHud.MenuLine.BetweenRuns), Is.EqualTo("Between runs (trial):  off"));
            _hud.Escape();
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.OpenBetweenRuns(), Is.False, "no screen with the switch off");
            Assert.That(_levels.StartNewRun(), Is.Not.Null);
            _levels.BackToPracticeRoom();
            yield return null;

            Assert.That(File.Exists(SavePath), Is.False, "no save written");
            Assert.That(Directory.Exists(_folder), Is.False, "not even its folder");
            Assert.That(Prefs(), Is.EqualTo(prefs), "PlayerPrefs as they were");
            Assert.That(BetweenRunsSwitch.Stored, Is.EqualTo(BetweenRunsSwitch.NoOverride));
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Missing), "the save was never read either");
            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            Assert.That(events.Any(e => e.Type == SessionEventTypes.MetaLoadout || e.Type == SessionEventTypes.MetaEarn || e.Type == SessionEventTypes.MetaFeat),
                        Is.False, "nothing in the recorder");
            Assert.That(SessionSummary.Format(SessionSummary.Compute(events)), Does.Not.Contain("between runs"));
        }

        // ---------------- The switch on ----------------

        [UnityTest]
        public IEnumerator SwitchOn_ARunThatDiesOnFloor2_EarnsWhatTheTableSays_AndTheEndScreenShowsIt()
        {
            On();
            Assert.That(_levels.StartNewRun(), Is.Null, "F5 brings up the screen, not the run");
            Assert.That(_levels.BetweenRunsUp, Is.True);
            yield return null;
            Assert.That(_hud.BetweenRunsLines[0], Is.EqualTo("Between runs (trial)"));
            Assert.That(_hud.BetweenRunsLines[1], Is.EqualTo("Echoes  0"));
            Assert.That(_hud.BetweenRunsLines[2], Is.EqualTo("Budget  3 points:  0 used,  3 left"));
            Assert.That(_hud.PickBetweenRunsLine(0), Is.True, "the start line starts the run");
            Assert.That(_levels.InRun, Is.True);
            yield return null;
            _health.Untouchable = true; // nothing on floor 1 takes her under half while the test clears it

            yield return ClearTheEliteRoom();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            _levels.PickSkillUpgrade(0);
            yield return null;
            yield return FinishTheFloor();
            Assert.That(_levels.PickReward(0), Is.True);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
            yield return null;
            Assert.That(_levels.Floor, Is.EqualTo(2));
            Assert.That(_levels.Earnings.Total(_levels.MetaValues), Is.EqualTo(25), "10 for the floor, 15 for its elite room");
            Assert.That(File.Exists(SavePath), Is.False, "written when the run ends");

            yield return Die();
            RunEarnings e = _levels.Earnings;
            Assert.That(e.FloorsCleared, Is.EqualTo(1));
            Assert.That(e.ElitesCleared, Is.EqualTo(1));
            Assert.That(e.BossKilled, Is.False);
            Assert.That(e.TotalAfter, Is.EqualTo(25), "kept: a run that dies keeps what it earned");
            Assert.That(e.NewFeats, Is.EquivalentTo(new[] { MetaFeat.FirstFloor, MetaFeat.EliteAboveHalf }));
            StringAssert.Contains("Echoes  +25 this run  (floors 10, elite rooms 15),  25 in all", _hud.EndScreenText);
            StringAssert.Contains("Feat  Cleared floor 1:  budget +1", _hud.EndScreenText);
            StringAssert.Contains("Feat  Cleared an elite room without dropping under half health:  budget +1", _hud.EndScreenText);
            Assert.That(_hud.EndScreenLines.Last(), Does.StartWith("Run seed"), "the seed stays last");
            Assert.That(File.Exists(SavePath), Is.True, "the save is written");
            MetaState saved = MetaSaveFile.Load(SavePath, new MetaValues(), out MetaLoad how, out _);
            Assert.That(how, Is.EqualTo(MetaLoad.Read));
            Assert.That(saved.Currency, Is.EqualTo(25));
            Assert.That(saved.Feats.Count, Is.EqualTo(2));
            Assert.That(saved.Runs, Is.EqualTo(1));

            // Enter: the screen, not the run; she is alive in the practice room under it.
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True);
            Assert.That(_levels.InRun, Is.False);
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_levels.BetweenRunsSeed, Is.Null, "a new run");
            yield return null;
            Assert.That(_hud.BetweenRunsLines[1], Is.EqualTo("Echoes  25"));
            Assert.That(_hud.BetweenRunsLines[2], Is.EqualTo("Budget  5 points:  0 used,  5 left"), "two feats, two points");

            // Esc: the practice room.
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            Assert.That(_health.Untouchable, Is.False);
        }

        [UnityTest]
        public IEnumerator TheScreen_ComesUpWhereItShould_PausesTheGame_AndStartsARun()
        {
            On();
            // From the pause menu's "Start a run" in the practice room.
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True, "the screen, not the run");
            Assert.That(_levels.Paused, Is.False, "the pause menu is closed under it");
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True, "nothing reaches her");
            Assert.That(_health.Untouchable, Is.True);
            Assert.That(_levels.Pause(), Is.False, "no pause over it");
            float t = Time.time;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Time.time, Is.EqualTo(t), "no game time passes");
            Assert.That(Time.timeScale, Is.EqualTo(0f), "held");
            Assert.That(_levels.OpenBetweenRuns(), Is.False, "already up");

            // Its start: a run from floor 1, the game going again, the press of that frame dropped.
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            Assert.That(PlayerInputDriver.LetGoThisFrame, Is.True, "the key that started it reaches nothing");
            Assert.That(_levels.BetweenRunsUp, Is.False);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(_health.Untouchable, Is.False);
            ulong first = _levels.RunSeed.Value;
            yield return null;

            // In a run, F5 is a new run at once, as before.
            Assert.That(_levels.StartNewRun(), Is.Not.Null);
            Assert.That(_levels.BetweenRunsUp, Is.False);
            Assert.That(_levels.RunSeed.Value, Is.Not.EqualTo(first));
            ulong second = _levels.RunSeed.Value;
            yield return null;

            // F6 on the death screen: the screen, for the same run, and its start starts that seed.
            yield return Die();
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True);
            Assert.That(_levels.BetweenRunsSeed, Is.EqualTo(second));
            yield return null;
            StringAssert.StartsWith("Start the same run again", _hud.BetweenRunsLines[3]);
            Assert.That(_hud.PickBetweenRunsLine(0), Is.True);
            Assert.That(_levels.RunSeed, Is.EqualTo(second), "the same run");
            Assert.That(_levels.Floor, Is.EqualTo(1));
            yield return null;

            // F6 and F7 from the practice room: the screen, and F7 (the practice room) closes it.
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_levels.RestartRun(), Is.Null);
            Assert.That(_levels.BetweenRunsUp, Is.True);
            Assert.That(_levels.BetweenRunsSeed, Is.EqualTo(second));
            _levels.BackToPracticeRoom();
            Assert.That(_levels.BetweenRunsUp, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // F5 on the screen starts a new run, as the screen's own start does.
            Assert.That(_levels.StartNewRun(), Is.Null);
            Assert.That(_levels.StartNewRun(), Is.Not.Null);
            Assert.That(_levels.RunSeed.Value, Is.Not.EqualTo(second));
        }

        // ---------------- The pieces, each in a run and only when equipped ----------------

        [UnityTest]
        public IEnumerator SteadyStart_ThickHide_HeavyHand_InARun_OnlyWhenEquipped()
        {
            On();
            Rich();
            yield return RunWith(RunSeed);
            Assert.That(_player.Focus, Is.Zero, "nothing equipped: an empty gauge");
            Assert.That(_health.Max, Is.EqualTo(_health.MaxHealth));
            Assert.That(_levels.Modifiers.DamageShare, Is.EqualTo(1f));
            Assert.That(_levels.Modifiers.Meta.Any, Is.False);
            _levels.BackToPracticeRoom();
            yield return null;

            yield return RunWith(RunSeed, MetaPieces.SteadyStart, MetaPieces.ThickHide, MetaPieces.ThickHide2, MetaPieces.HeavyHand);
            Assert.That(_player.Focus, Is.EqualTo(_player.Identity.MaxFocus * 0.5f).Within(1e-3f), "half the gauge");
            Assert.That(_player.IsFocusFull, Is.False);
            Assert.That(_health.Max, Is.EqualTo(Mathf.RoundToInt(_health.MaxHealth * 1.4f)), "+20 % and +20 % more");
            Assert.That(_health.Current, Is.EqualTo(_health.Max), "full at the start");
            Assert.That(_levels.Modifiers.DamageShare, Is.EqualTo(1.15f).Within(1e-5f));
            Assert.That(_levels.Modifiers.Meta.Pieces, Is.EqualTo(new[] { "Steady Start", "Thick Hide", "Thick Hide II", "Heavy Hand" }));

            // A chest's step still adds on top.
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            Assert.That(_levels.OpenChest(chest), Is.True);
            int pick = _levels.OfferedUpgrades.ToList().FindIndex(u => u.Kind == UpgradeKind.MaxHealth);
            if (pick >= 0)
            {
                _levels.PickUpgrade(pick);
                Assert.That(_health.Max, Is.EqualTo(Mathf.RoundToInt(_health.MaxHealth * 1.4f) + 10));
            }
            else _levels.PickUpgrade(0);
        }

        [UnityTest]
        public IEnumerator SpareStep_TwoDashesInARow_InARun_OnlyWhenEquipped()
        {
            On();
            Rich();
            yield return RunWith(RunSeed);
            Vector3 ahead = _player.Position + Vector3.forward * 4f;
            Assert.That(_player.DashToward(ahead), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(ahead), Is.EqualTo(IntentResult.Rejected), "one charge without it");
            _levels.BackToPracticeRoom();
            yield return null;

            yield return RunWith(RunSeed, MetaPieces.SpareStep);
            Assert.That(_player.DashMaxCharges, Is.EqualTo(2));
            Assert.That(_player.DashChargesReady, Is.EqualTo(2));
            float from = Time.time;
            Assert.That(_player.DashToward(ahead), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(ahead), Is.EqualTo(IntentResult.Started), "the second charge");
            yield return Until(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(ahead), Is.EqualTo(IntentResult.Rejected), "not a third");
            float expected = _player.Feel.DashCooldown * 4f / 3f - (Time.time - from);
            Assert.That(_player.DashCooldownRemaining, Is.EqualTo(expected).Within(0.05f), "a third longer to come back");

            // Back in the practice room: one charge again.
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_player.DashMaxCharges, Is.EqualTo(1));
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Rejected));
        }

        [UnityTest]
        public IEnumerator DeepChests_AndKnightsTrophy_OneMore_InARun_OnlyWhenEquipped()
        {
            On();
            Rich();
            Assert.That(_levels.BetweenRunsState.Owns(MetaPieces.KnightsTrophy), Is.True, "the boss feat gave it");
            yield return RunWith(RunSeed);
            List<GeneratedLevel.PlacedProp> chests = _levels.Chests();
            Assert.That(_levels.OpenChest(chests[0]), Is.True);
            Assert.That(_levels.OfferedUpgrades.Count, Is.EqualTo(2), "without: two");
            _levels.PickUpgrade(0);
            yield return ClearTheEliteRoom();
            Assert.That(_levels.OfferedSkillUpgrades.Count, Is.EqualTo(3), "without: three");
            _levels.PickSkillUpgrade(0);
            _levels.BackToPracticeRoom();
            yield return null;

            yield return RunWith(RunSeed, MetaPieces.DeepChests, MetaPieces.KnightsTrophy);
            chests = _levels.Chests();
            Assert.That(_levels.OpenChest(chests[0]), Is.True);
            Assert.That(_levels.OfferedUpgrades.Count, Is.EqualTo(3), "the first chest of the floor: three");
            _levels.PickUpgrade(2);
            Assert.That(_levels.Record.Upgrades.Count, Is.EqualTo(1));
            if (chests.Count > 1)
            {
                Assert.That(_levels.OpenChest(chests[1]), Is.True);
                Assert.That(_levels.OfferedUpgrades.Count, Is.EqualTo(2), "the second: two");
                _levels.PickUpgrade(0);
            }
            yield return ClearTheEliteRoom();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.SkillUpgrade));
            Assert.That(_levels.OfferedSkillUpgrades.Count, Is.EqualTo(4), "four with the trophy");
            SkillUpgradeDefinition fourth = _levels.OfferedSkillUpgrades[3];
            Assert.That(_levels.PickSkillUpgrade(3), Is.True, "the fourth can be taken");
            Assert.That(_levels.SkillUpgrades.Taken, Does.Contain(fourth));
        }

        [UnityTest]
        public IEnumerator SecondLook_AndNotThatOne_OnTheFloorReward_InARun_OnlyWhenEquipped()
        {
            On();
            Rich();
            yield return RunWith(RunSeed);
            yield return FinishTheFloor();
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
            List<string> plain = _levels.OfferedSkills.Select(s => _player.Skills[s].Name).ToList();
            Assert.That(_levels.CanRedraw, Is.False);
            Assert.That(_levels.RedrawOffer(), Is.False, "no redraw without it");
            _levels.PickReward(0);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
            _levels.BackToPracticeRoom();
            yield return null;

            // Strike out the first skill the plain offer had, on the screen.
            Assert.That(_levels.OpenBetweenRuns(RunSeed), Is.True);
            BuyAndEquip(MetaPieces.SecondLook, MetaPieces.NotThatOne);
            for (int i = 0; i < 20 && _levels.StruckOutSkill != plain[0]; i++) _levels.ChangeStruckOut(1);
            Assert.That(_levels.StruckOutSkill, Is.EqualTo(plain[0]));
            yield return null;
            StringAssert.Contains("Struck out:  " + plain[0], _hud.BetweenRunsText);
            Assert.That(_levels.StartFromBetweenRuns(), Is.Not.Null);
            yield return null;
            Assert.That(_levels.Modifiers.Meta.StruckOut, Is.EqualTo(plain[0]));

            for (int floor = 1; floor <= 4; floor++)
            {
                yield return FinishTheFloor();
                Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward));
                List<string> offered = _levels.OfferedSkills.Select(s => _player.Skills[s].Name).ToList();
                Assert.That(offered, Does.Not.Contain(plain[0]), "struck out, floor " + floor);
                if (floor == 1)
                {
                    Assert.That(_levels.CanRedraw, Is.True);
                    Assert.That(_levels.RedrawOffer(), Is.True, "Second Look");
                    List<string> again = _levels.OfferedSkills.Select(s => _player.Skills[s].Name).ToList();
                    Assert.That(again, Is.Not.EquivalentTo(offered), "drawn again");
                    Assert.That(again, Does.Not.Contain(plain[0]), "still struck out");
                    Assert.That(again.Count, Is.EqualTo(_levels.RunValues.SkillsOffered));
                    Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the offer is still up");
                    Assert.That(Time.timeScale, Is.EqualTo(0f));
                    Assert.That(_levels.RedrawOffer(), Is.False, "once a run");
                }
                Assert.That(_levels.PickReward(0), Is.True);
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
                yield return null;
            }
            Assert.That(_levels.SkillsTaken, Does.Not.Contain(plain[0]));
        }

        [UnityTest]
        public IEnumerator MarkedTarget_AndHeavyHand_OnAHit_ThroughTheRunsModifiers_OnlyWhenEquipped()
        {
            On();
            Rich();
            // In a run, the numbers are on the run's modifiers only when equipped.
            yield return RunWith(RunSeed);
            Assert.That(_levels.Modifiers.Meta.FirstMarkExtra, Is.Zero);
            _levels.BackToPracticeRoom();
            yield return null;
            yield return RunWith(RunSeed, MetaPieces.MarkedTarget, MetaPieces.HeavyHand);
            Assert.That(_levels.Modifiers.Meta.FirstMarkExtra, Is.EqualTo(1));
            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_player.Modifiers, Is.Null, "out of the run: none");

            // The hit itself, on the practice dummy, through the same mechanism a run uses (SkillUpgradeTests does so too).
            Damageable a = GameObject.Find("Dummy_A").GetComponent<Damageable>();
            var dealt = new List<int>();
            a.Damaged += d => dealt.Add(d);
            int q = _player.SlotOf("Quick Shot");
            _player.Teleport(NearA);
            yield return null;
            _player.Modifiers = new RunModifiers(); // a run with nothing equipped
            Assert.That(_player.PressSkill(q, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => dealt.Count > 0, 3f);
            Assert.That(a.MarkCount, Is.EqualTo(1), "one mark without it");
            int plain = dealt[0];
            a.TakeMarks();
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None, 3f);

            var m = new RunModifiers();
            MetaRack.Apply(_levels.BetweenRunsState, _levels.MetaValues, m.Meta); // Marked Target and Heavy Hand are equipped
            _player.Modifiers = m;
            _player.StartFresh();
            Assert.That(_player.PressSkill(q, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => dealt.Count > 1, 3f);
            Assert.That(a.MarkCount, Is.EqualTo(2), "two on a target with none");
            Assert.That(dealt[1], Is.EqualTo((int)Math.Round(plain * 1.15f, MidpointRounding.AwayFromZero)), "15 % more damage");
            yield return Until(() => !_player.IsLocked && _player.Action == ActionKind.None, 3f);
            _player.StartFresh();
            Assert.That(_player.PressSkill(q, a.transform.position), Is.EqualTo(IntentResult.Started));
            yield return Until(() => dealt.Count > 2, 3f);
            Assert.That(a.MarkCount, Is.EqualTo(3), "a target with marks gets the one of the skill");
            _player.Modifiers = null;
        }

        // ---------------- Feats, the save, the practice room ----------------

        [UnityTest]
        public IEnumerator AFeat_RaisesTheBudgetOnce()
        {
            On();
            for (int run = 1; run <= 2; run++)
            {
                yield return RunWith(RunSeed);
                yield return FinishTheFloor();
                Assert.That(_levels.PickReward(0), Is.True);
                if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
                yield return null;
                _levels.BackToPracticeRoom(); // leaving keeps what was earned too
                yield return null;
                Assert.That(_levels.Earnings.NewFeats, run == 1 ? Is.EqualTo(new[] { MetaFeat.FirstFloor }) : Is.Empty, "run " + run);
                Assert.That(MetaRack.Budget(_levels.BetweenRunsState, _levels.MetaValues), Is.EqualTo(4), "run " + run);
                Assert.That(_levels.BetweenRunsState.Currency, Is.EqualTo(10 * run));
            }
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            Assert.That(_hud.BetweenRunsLines[2], Does.StartWith("Budget  4 points"));
        }

        [UnityTest]
        public IEnumerator TheSave_SurvivesMakingTheComponentsAgain()
        {
            On();
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            _levels.BetweenRunsState.Currency = 200;
            Assert.That(_levels.BuyPiece(MetaPieces.HeavyHand), Is.EqualTo(MetaBuy.Bought));
            Assert.That(_levels.EquipPiece(MetaPieces.HeavyHand), Is.EqualTo(MetaEquip.Equipped));
            Assert.That(_levels.BuyPiece(MetaPieces.SpareStep), Is.EqualTo(MetaBuy.NotEnough), "50 left");
            yield return null;
            StringAssert.Contains("Not enough Echoes", _hud.BetweenRunsText);
            Assert.That(File.Exists(SavePath), Is.True, "written when the rack changes");
            _levels.LeaveBetweenRuns();

            Object.Destroy(_hud.gameObject);
            Object.Destroy(_levels.gameObject);
            yield return null;
            Assert.That(GeneratedLevels.Current, Is.Null);
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            On();
            yield return null;
            MetaState back = _levels.BetweenRunsState;
            Assert.That(_levels.BetweenRunsLoad, Is.EqualTo(MetaLoad.Read));
            Assert.That(back.Currency, Is.EqualTo(50));
            Assert.That(back.Owned, Is.EqualTo(new[] { MetaPieces.HeavyHand }));
            Assert.That(back.Equipped, Is.EqualTo(new[] { MetaPieces.HeavyHand }));
            yield return RunWith(null, MetaPieces.HeavyHand);
            Assert.That(_levels.Modifiers.DamageShare, Is.EqualTo(1.15f).Within(1e-5f));
        }

        [UnityTest]
        public IEnumerator ThePracticeRoomsOwnValues_AreUntouched_AfterARunWithPiecesEquipped()
        {
            FeelValues feel = _player.Feel;
            float dashCooldown = feel.DashCooldown;
            float maxFocus = _player.Identity.MaxFocus;
            int maxMarks = _player.Kit.MaxMarks;
            int ownMax = _health.MaxHealth;
            string skills = string.Join("|", _player.SettingsSkills.Select(s => s.Name + s.Damage + s.Cooldown + s.MarksAdded));
            On();
            Rich();
            yield return RunWith(RunSeed, MetaPieces.SteadyStart, MetaPieces.SpareStep, MetaPieces.ThickHide, MetaPieces.HeavyHand);
            Assert.That(_health.Max, Is.GreaterThan(ownMax));
            Assert.That(_player.DashToward(_player.Position + Vector3.forward * 3f), Is.EqualTo(IntentResult.Started));
            yield return Until(() => !_player.IsDashing, 2f);
            _health.TakeHit(5);
            _levels.BackToPracticeRoom();
            yield return null;

            Assert.That(_player.Modifiers, Is.Null);
            Assert.That(_health.Max, Is.EqualTo(ownMax), "her own maximum again");
            Assert.That(_health.MaxHealth, Is.EqualTo(ownMax));
            Assert.That(_player.Focus, Is.Zero, "no focus carried out of the run");
            Assert.That(_player.DashMaxCharges, Is.EqualTo(1));
            Assert.That(feel.DashCooldown, Is.EqualTo(dashCooldown), "the feel settings are never written");
            Assert.That(_player.Identity.MaxFocus, Is.EqualTo(maxFocus));
            Assert.That(_player.Kit.MaxMarks, Is.EqualTo(maxMarks));
            Assert.That(string.Join("|", _player.SettingsSkills.Select(s => s.Name + s.Damage + s.Cooldown + s.MarksAdded)), Is.EqualTo(skills));
            Assert.That(_levels.Modifiers.Meta.Any, Is.False, "cleared when the run ended");
        }

        [UnityTest]
        public IEnumerator ThePauseMenusLine_TurnsItOnAndOff_AndKeepsItInPlayerPrefs()
        {
            Assert.That(_hud.Escape(), Is.True);
            yield return null;
            Assert.That(_hud.MenuLines, Has.Member(RunHud.MenuLine.BetweenRuns));
            Assert.That(_hud.MenuText(RunHud.MenuLine.BetweenRuns), Is.EqualTo("Between runs (trial):  off"));
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.BetweenRuns, 1), Is.True);
            Assert.That(_hud.MenuText(RunHud.MenuLine.BetweenRuns), Is.EqualTo("Between runs (trial):  on"));
            Assert.That(PlayerPrefs.GetInt(BetweenRunsSwitch.Key, -1), Is.EqualTo(1), "kept in PlayerPrefs");
            Assert.That(_levels.MetaOn, Is.True);
            Assert.That(_levels.MetaValues.Enabled, Is.False, "the values are not written");
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.BetweenRunsUp, Is.True, "on: the screen");
            _hud.Escape();
            Assert.That(_levels.BetweenRunsUp, Is.False);

            _hud.Escape();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.BetweenRuns), Is.True);
            Assert.That(PlayerPrefs.GetInt(BetweenRunsSwitch.Key, -1), Is.EqualTo(0));
            Assert.That(_levels.MetaOn, Is.False);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.InRun, Is.True, "off: a run at once");
            Assert.That(_levels.BetweenRunsUp, Is.False);
        }

        [UnityTest]
        public IEnumerator TheRecorder_WritesWhatWasEquipped_EarnedAndDone_InOneLine()
        {
            _recordings = Path.Combine(Path.GetTempPath(), "BetweenRunsTests-rec-" + Guid.NewGuid().ToString("N"));
            _recorder = PlaySessionRecorder.StartRecording(_recordings);
            On();
            Rich(100);
            _levels.BetweenRunsState.Feats.Clear();
            yield return RunWith(RunSeed, MetaPieces.SteadyStart);
            yield return ClearTheEliteRoom();
            if (_levels.Choice == GeneratedLevels.ChoiceKind.SkillUpgrade) _levels.PickSkillUpgrade(0);
            yield return null;
            yield return FinishTheFloor();
            _levels.PickReward(0);
            if (_levels.Choice == GeneratedLevels.ChoiceKind.FocusChoice) _levels.PickFocusChoice(0); // after floor 1 the focus choice comes up (work order 025)
            yield return null;
            _levels.BackToPracticeRoom();
            yield return null;
            _recorder.StopRecording();
            IReadOnlyList<SessionEvent> events = _recorder.Events;
            SessionEvent loadout = events.Single(e => e.Type == SessionEventTypes.MetaLoadout);
            Assert.That(loadout.Name, Is.EqualTo("Steady Start"));
            Assert.That(events.Where(e => e.Type == SessionEventTypes.MetaEarn).Select(e => e.Name), Is.EqualTo(new[] { "elite room", "floor" }));
            Assert.That(events.Where(e => e.Type == SessionEventTypes.MetaFeat).Select(e => e.Name), Does.Contain("clear-floor-1"));
            SessionStats stats = SessionSummary.Compute(events);
            RunRow row = stats.Runs.Last();
            Assert.That(row.HasMeta, Is.True);
            Assert.That(row.MetaEarned, Is.EqualTo(25));
            StringAssert.Contains("between runs     equipped Steady Start;  earned 25 (elite room 15, floor 10)", SessionSummary.Format(stats));
        }

        /// <summary>
        /// Work order 081 (noted by work order 077): the screen's lines are made when it opens and when what they say changes,
        /// not every frame it is up: thirty frames with nothing changed make none; a piece bought, a change to the save made
        /// outside the screen, and a turn of the struck-out skill each make them once more; they say what changed; closed, none.
        /// </summary>
        [UnityTest]
        public IEnumerator TheScreensLines_AreMadeAsItOpensAndOncePerChange_NotEveryFrame()
        {
            On();
            Rich(500);
            Assert.That(_levels.OpenBetweenRuns(), Is.True);
            yield return null;
            int made = _hud.BetweenRunsLinesMade;
            Assert.That(made, Is.GreaterThan(0), "made as the screen opened");
            Assert.That(_hud.BetweenRunsLines[1], Is.EqualTo("Echoes  500"));
            string text = _hud.BetweenRunsText;
            for (int i = 0; i < 30; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(made), "thirty frames with nothing changed: not made again");
            Assert.That(_hud.BetweenRunsText, Is.EqualTo(text));

            Assert.That(_levels.BuyPiece(MetaPieces.HeavyHand), Is.EqualTo(MetaBuy.Bought));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(made + 1), "a piece bought: made once");
            Assert.That(_hud.BetweenRunsText, Is.Not.EqualTo(text), "and they say so");
            Assert.That(_hud.BetweenRunsLines[1], Is.Not.EqualTo("Echoes  500"), "the currency spent");

            _levels.BetweenRunsState.Currency = 4321; // a change to the save the screen did not make
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(made + 2), "the save changed: made once");
            Assert.That(_hud.BetweenRunsLines[1], Is.EqualTo("Echoes  4321"));

            BuyAndEquip(MetaPieces.SecondLook, MetaPieces.NotThatOne);
            yield return null;
            int afterEquip = _hud.BetweenRunsLinesMade;
            string struck = _levels.StruckOutSkill;
            string next = _levels.ChangeStruckOut(1);
            Assume.That(next, Is.Not.EqualTo(struck), "the pool has more than one skill");
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(afterEquip + 1), "the struck-out skill turned: made once");
            StringAssert.Contains("Struck out:  " + next, _hud.BetweenRunsText);

            Assert.That(_levels.LeaveBetweenRuns(), Is.True);
            yield return null;
            Assert.That(_hud.BetweenRunsLines, Is.Empty, "closed: no lines");
            int closed = _hud.BetweenRunsLinesMade;
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(_hud.BetweenRunsLinesMade, Is.EqualTo(closed), "none made while it is closed");
        }

        /// <summary>Last by name: after every other test of the class, the game's own save is as it was.</summary>
        [Test]
        public void Z_TheGamesOwnSave_IsNeverTouchedByTheseTests()
        {
            Assert.That(File.Exists(s_realPath), Is.EqualTo(s_realExisted), s_realPath);
            if (!s_realExisted) return;
            Assert.That(new FileInfo(s_realPath).Length, Is.EqualTo(s_realLength));
            Assert.That(File.GetLastWriteTimeUtc(s_realPath), Is.EqualTo(s_realWritten));
        }
    }
}
