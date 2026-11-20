using System;
using System.Collections;
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
    /// The HUD, the pause menu and the end of a run (work order 018, 2026-11-20) in the ControlRoom scene, through the
    /// intents of <see cref="RunHud"/> and <see cref="GeneratedLevels"/>: the HUD's health follows hers and a hit flashes
    /// the edge; the run box says where she is; F1 switches the developer's text; Esc pauses and resumes, also after a
    /// choice and never over one; the menu's lines do what they say; a setting changes what the game does and survives
    /// making the HUD again; death slows the game to a stop and shows the death screen with the right floor and room,
    /// leaves her on the floor until a key, and each key does what it says; clearing the act shows its screen; and the
    /// play session recorder's clock does not run while paused.
    ///
    /// A paused or stopped game has a time scale of 0, and a frame without a window is about 0.3 ms: the waits here count
    /// frames or real time, never game time. The PlayerPrefs keys of the settings are cleared before each test and
    /// after it, and what this machine had under them is put back, so a test run leaves nothing of its own behind.
    /// </summary>
    public class RunHudTests
    {
        const ulong RunSeed = 0x12345678UL;
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f; // real seconds until the end screen surely shows

        PlayerCharacter _player;
        PlayerHealth _health;
        PlayerInputDriver _driver;
        GeneratedLevels _levels;
        RunHud _hud;
        string _folder;
        PlaySessionRecorder _recorder;
        int? _dashBefore;
        int? _volumeBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _dashBefore = PlayerPrefs.HasKey(GameSettings.DashKey) ? PlayerPrefs.GetInt(GameSettings.DashKey) : (int?)null;
            _volumeBefore = PlayerPrefs.HasKey(GameSettings.VolumeKey) ? PlayerPrefs.GetInt(GameSettings.VolumeKey) : (int?)null;
            ClearSettingKeys();
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _driver = _player.GetComponent<PlayerInputDriver>();
            _driver.InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _hud = RunHud.Ensure();
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            if (_recorder != null) _recorder.StopRecording();
            _recorder = null;
            if (_folder != null && Directory.Exists(_folder)) Directory.Delete(_folder, true);
            _folder = null;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            ClearSettingKeys();
            if (_dashBefore.HasValue) PlayerPrefs.SetInt(GameSettings.DashKey, _dashBefore.Value);
            if (_volumeBefore.HasValue) PlayerPrefs.SetInt(GameSettings.VolumeKey, _volumeBefore.Value);
            PlayerPrefs.Save();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static void ClearSettingKeys()
        {
            PlayerPrefs.DeleteKey(GameSettings.DashKey);
            PlayerPrefs.DeleteKey(GameSettings.VolumeKey);
            PlayerPrefs.Save();
        }

        /// <summary>Frames until the condition holds or <see cref="MostFrames"/> have gone: frames, so a paused game still counts.</summary>
        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        IEnumerator StandBy(GeneratedLevel.PlacedProp chest)
        {
            Vector3 c = chest.Object.transform.position;
            Vector3 middle = _levels.Level.Middle(chest.Room);
            Vector3 toward = new Vector3(middle.x - c.x, 0f, middle.z - c.z);
            if (toward.sqrMagnitude < 1e-4f) toward = Vector3.forward;
            _player.Teleport(c + toward.normalized * 1.4f);
            yield return null;
            yield return null;
        }

        IEnumerator Die()
        {
            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "the death screen is coming");
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
        }

        // ---------------- The HUD ----------------

        [UnityTest]
        public IEnumerator TheHudsHealth_FollowsHerHealth_AndAHitFlashesTheEdge_StrongerForABiggerOne()
        {
            Assert.That(_hud.ShownHealth, Is.EqualTo(_health.Current));
            Assert.That(_hud.ShownMaxHealth, Is.EqualTo(_health.Max));
            Assert.That(_hud.HealthText, Is.EqualTo(_health.Max + " / " + _health.Max));
            Assert.That(_hud.EdgeAlpha, Is.EqualTo(0f), "no hit, full health: no edge");

            _health.TakeHit(5);
            float small = _hud.HurtFlashNow;
            yield return null;
            Assert.That(_hud.ShownHealth, Is.EqualTo(_health.Max - 5));
            Assert.That(_hud.HealthFill, Is.EqualTo((_health.Max - 5) / (float)_health.Max).Within(1e-4f));
            Assert.That(small, Is.EqualTo(HudRules.HurtFlash(5, _health.Max)).Within(0.05f));
            Assert.That(_hud.EdgeAlpha, Is.GreaterThan(0f), "the edge flashes");

            yield return new WaitForSecondsRealtime(HudRules.FlashSeconds + 0.1f);
            yield return null;
            Assert.That(_hud.EdgeAlpha, Is.EqualTo(0f), "and fades");

            _health.TakeHit(30);
            float big = _hud.HurtFlashNow;
            Assert.That(big, Is.GreaterThan(small), "stronger for a bigger hit");
            yield return null;
            Assert.That(_hud.ShownHealth, Is.EqualTo(_health.Max - 35));

            // Under a quarter: a faint edge stays once the flash has gone.
            _health.TakeHit(_health.Current - _health.Max / 5);
            yield return new WaitForSecondsRealtime(HudRules.FlashSeconds + 0.1f);
            yield return null;
            Assert.That(_hud.ShownHealth, Is.EqualTo(_health.Max / 5));
            Assert.That(_hud.EdgeAlpha, Is.EqualTo(HudRules.LowHealthEdge).Within(1e-4f), "the low-health edge stays");
        }

        [UnityTest]
        public IEnumerator TheRunBox_SaysTheFloorTheRoomTheEnemiesTheTimeAndTheUpgrades_AndThePracticeRoomHasItsPrompt()
        {
            Assert.That(_hud.PromptText, Is.EqualTo("F5  start a run"));
            Assert.That(_hud.RunBoxLines, Is.Empty, "no run box in the practice room");

            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            Assert.That(_hud.PromptText, Is.EqualTo(""));
            Assert.That(_hud.RunBoxLines[0], Is.EqualTo("Floor 1 of 5"));
            Assert.That(_hud.RunBoxLines[1], Is.EqualTo("Entrance"));
            Assert.That(_hud.RunBoxLines[2], Is.EqualTo(RunWords.EnemiesLeft(_levels.EnemiesLeftInRoom)));
            StringAssert.StartsWith("Time 0:0", _hud.RunBoxLines[3]);
            Assert.That(_hud.RunBoxLines[4], Is.EqualTo("Upgrades: none"));
            Assert.That(_hud.RunBoxLines.Last(), Is.EqualTo("run " + LevelSeed.ToText(RunSeed)), "the seed underneath");

            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            yield return StandBy(chest);
            Assert.That(_levels.OpenChest(), Is.True);
            string taken = _levels.OfferedUpgrades[0].Short;
            _levels.PickUpgrade(0);
            yield return null;
            Assert.That(_hud.RunBoxLines[1], Is.EqualTo("Treasure room"));
            Assert.That(_hud.RunBoxLines[4], Is.EqualTo("Upgrades: " + taken));
            Assert.That(_levels.Record.Upgrades, Is.EqualTo(new[] { taken }));
        }

        [UnityTest]
        public IEnumerator F1_SwitchesTheDeveloperText_OnInThePracticeRoom_OffInARun()
        {
            var readout = Object.FindAnyObjectByType<DebugReadout>(FindObjectsInactive.Include);
            Assert.That(readout, Is.Not.Null, "the scene's debug readout");
            Assert.That(_hud.DevTextShown, Is.True, "on in the practice room");
            Assert.That(readout.enabled, Is.True);
            Assert.That(_levels.ShowDevBox, Is.True);

            _levels.StartRun(RunSeed);
            yield return null;
            Assert.That(_hud.DevTextShown, Is.False, "off in a run");
            Assert.That(readout.enabled, Is.False);
            Assert.That(_levels.ShowDevBox, Is.False);

            _hud.ToggleDevText();
            yield return null;
            Assert.That(_hud.DevTextShown, Is.True);
            Assert.That(readout.enabled, Is.True);
            Assert.That(_levels.ShowDevBox, Is.True);
            Assert.That(_levels.DevBoxTop, Is.GreaterThan(10f), "under the run box");

            _levels.BackToPracticeRoom();
            yield return null;
            Assert.That(_hud.DevTextShown, Is.True, "the practice room's own");
            _hud.ToggleDevText();
            yield return null;
            Assert.That(readout.enabled, Is.False);
            Assert.That(_levels.ShowDevBox, Is.False);
        }

        // ---------------- Pause ----------------

        [UnityTest]
        public IEnumerator Esc_PausesAndResumes_AlsoAfterAChoice_AndDoesNothingOverOne()
        {
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.Paused, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True, "nothing reaches her");
            float t = Time.time;
            for (int i = 0; i < 10; i++) yield return null;
            Assert.That(Time.time, Is.EqualTo(t), "no game time passes");
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f), "time comes back");
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);

            // A choice up: Esc does nothing, and the choice keeps the game paused.
            _levels.StartRun(RunSeed);
            yield return null;
            yield return StandBy(_levels.Chests().First());
            Assert.That(_levels.OpenChest(), Is.True);
            Assert.That(_hud.Escape(), Is.False);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.Chest));
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            _levels.PickUpgrade(0);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // After the choice: pause and resume give the time back, not the choice's 0.
            yield return null;
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(0f), "held");
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator TheMenusLines_DoWhatTheySay()
        {
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Resume), Is.False, "no menu, not paused");
            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuLines, Has.No.Member(RunHud.MenuLine.Leave), "nothing to leave in the practice room");
            Assert.That(_hud.MenuLines, Has.No.Member(RunHud.MenuLine.Quit), "no Quit in the Editor");
            Assert.That(_hud.MenuText(RunHud.MenuLine.Restart), Does.StartWith("Start a run"));
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.InRun, Is.True, "a run started");
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            ulong first = _levels.RunSeed.Value;
            yield return null;

            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuLines, Has.Member(RunHud.MenuLine.Leave));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Restart), Does.StartWith("New run")); // work order 026: says what it does
            string keys = string.Join("\n", _hud.KeyLines());
            StringAssert.Contains("Move  W A S D", keys);
            StringAssert.Contains("Dash  Space", keys);
            StringAssert.Contains("Q  " + _player.Skills[0].Name, keys);
            StringAssert.Contains("Interact  E", keys);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.RunSeed.Value, Is.Not.EqualTo(first), "F5's new run");
            Assert.That(_levels.Floor, Is.EqualTo(1));
            yield return null;

            _hud.Escape();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Leave), Is.True);
            Assert.That(_levels.IsInLevel, Is.False, "back in the practice room");
            Assert.That(_levels.InRun, Is.False);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            _hud.Escape();
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Resume), Is.True);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }

        [UnityTest]
        public IEnumerator ASetting_ChangesWhatTheGameDoes_AndSurvivesMakingTheHudAgain()
        {
            // The asset says toward the cursor (a copy, so the project's asset is never touched).
            InputBindings bindings = Object.Instantiate(_driver.ActiveBindings);
            bindings.DashDirection = DashDirection.TowardCursor;
            _driver.Bindings = bindings;
            // The cursor behind her: no move key is held, so the way she moves is the way she faces.
            Vector3 facing = new Vector3(_player.transform.forward.x, 0f, _player.transform.forward.z).normalized;
            Vector3 cursor = _player.Position - facing * 6f;
            Vector3 faced = _player.Position + facing * Mathf.Max(1f, _player.Feel.DashDistance);
            Assert.That(_driver.DashPoint(cursor), Is.EqualTo(cursor), "toward the cursor");
            Assert.That(AudioListener.volume, Is.EqualTo(1f), "full volume with nothing stored");

            _hud.Escape();
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.DashDirection, 1), Is.True);
            Assert.That(PlayerPrefs.GetInt(GameSettings.DashKey, -1), Is.EqualTo(SettingsRules.DashTowardMovement), "kept in PlayerPrefs");
            Assert.That(Flat(_driver.DashPoint(cursor), faced), Is.LessThan(0.01f), "the dash goes the way she faces now, not to the cursor");
            Assert.That(bindings.DashDirection, Is.EqualTo(DashDirection.TowardCursor), "the asset is not written");
            StringAssert.Contains("dash the way you move", bindings.HelpLine());
            StringAssert.Contains("the way you move", _hud.MenuText(RunHud.MenuLine.DashDirection));

            for (int i = 0; i < 3; i++) _hud.ChangeSetting(RunHud.MenuLine.Volume, -1);
            Assert.That(AudioListener.volume, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(PlayerPrefs.GetInt(GameSettings.VolumeKey, -1), Is.EqualTo(7));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Volume), Does.Contain("70 %"));
            Assert.That(_hud.ChangeSetting(RunHud.MenuLine.Resume, 1), Is.False, "not a setting");
            _hud.Escape();

            // Gone: the asset's own dash and full volume again.
            Object.Destroy(_hud.gameObject);
            yield return null;
            Assert.That(RunHud.Current, Is.Null);
            Assert.That(_driver.DashPoint(cursor), Is.EqualTo(cursor));
            Assert.That(AudioListener.volume, Is.EqualTo(1f));

            // Made again: read back from PlayerPrefs.
            _hud = RunHud.Ensure();
            yield return null;
            Assert.That(Flat(_driver.DashPoint(cursor), faced), Is.LessThan(0.01f), "the way she moves, still");
            Assert.That(AudioListener.volume, Is.EqualTo(0.7f).Within(1e-4f));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Volume), Does.Contain("70 %"));

            // And back to the cursor.
            _hud.ChangeSetting(RunHud.MenuLine.DashDirection, -1);
            Assert.That(_driver.DashPoint(cursor), Is.EqualTo(cursor));
        }

        [UnityTest]
        public IEnumerator ThePlaySessionRecordersClock_StandsStillWhilePaused()
        {
            _folder = Path.Combine(Path.GetTempPath(), "RunHudTests-" + Guid.NewGuid().ToString("N"));
            float realFrom = Time.realtimeSinceStartup;
            _recorder = PlaySessionRecorder.StartRecording(_folder);
            yield return new WaitForSeconds(0.5f);
            Assert.That(_hud.Escape(), Is.True);
            int movesBefore = _recorder.Events.Count(e => e.Type == SessionEventTypes.Move);
            yield return new WaitForSecondsRealtime(1f);
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.Move), Is.EqualTo(movesBefore), "no samples while paused");
            _hud.Escape();
            yield return new WaitForSeconds(0.5f);
            Assert.That(_recorder.Events.Count(e => e.Type == SessionEventTypes.Move), Is.GreaterThan(movesBefore), "samples again after");
            _recorder.StopRecording();
            float real = Time.realtimeSinceStartup - realFrom;
            SessionStats stats = SessionSummary.Compute(_recorder.Events);
            Assert.That(stats.Length, Is.LessThan(real - 0.8f), "the paused second is not in the session's length");
            Assert.That(stats.Length, Is.GreaterThan(0.9f), "the second of play is");
        }

        // ---------------- The end of a run ----------------

        [UnityTest]
        public IEnumerator Death_ShowsTheScreen_WithTheFloorAndRoom_LeavesHerOnTheFloorUntilAKey_AndEachKeyDoesWhatItSays()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            // Into the first treasure room, and two kills.
            GeneratedLevel.PlacedProp chest = _levels.Chests().First();
            yield return StandBy(chest);
            Assert.That(_levels.CurrentRoom, Is.SameAs(chest.Room));
            foreach (Enemy e in _levels.Level.Enemies.Take(2)) e.Body.TakeHit(1000000);
            yield return Frames(() => _levels.Record.Kills >= 2);
            Assert.That(_levels.Record.Kills, Is.EqualTo(2), "two kills");
            Vector3 fell = _player.Position;

            _health.TakeHit(100000);
            yield return Frames(() => _levels.Ending == GeneratedLevels.EndKind.Died);
            Assert.That(_levels.InRun, Is.False, "death ends the run");
            Assert.That(_levels.Modifiers.Count, Is.EqualTo(0));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.True);
            Assert.That(_hud.Escape(), Is.False, "the screen is not up yet: the game is still slowing");
            yield return new WaitForSecondsRealtime(Stopped);
            yield return null;
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f), "stopped");
            Assert.That(_hud.EndScreenLines[0], Is.EqualTo("You died"));
            Assert.That(_hud.EndScreenLines[1], Is.EqualTo("Floor 1 of 5,  Treasure room"));
            Assert.That(_levels.Record.Floor, Is.EqualTo(1));
            Assert.That(_levels.Record.Room, Is.EqualTo(chest.Room.Order));
            Assert.That(_levels.Record.Outcome, Is.EqualTo(RunOutcome.Died));
            StringAssert.Contains("Enemies killed  2", _hud.EndScreenText);
            StringAssert.Contains("Skills taken  none", _hud.EndScreenText);
            StringAssert.Contains("Upgrades  none", _hud.EndScreenText);
            StringAssert.Contains("Run seed  " + LevelSeed.ToText(RunSeed), _hud.EndScreenText);
            StringAssert.Contains("Time  0:0", _hud.EndScreenText);

            // She stays where she fell, dead, as long as no key is pressed.
            Vector3 shown = _player.Position;
            Assert.That(Flat(shown, fell), Is.LessThan(1f), "not moved to the practice room");
            yield return new WaitForSecondsRealtime(0.5f);
            Assert.That(_levels.IsInLevel, Is.True, "still on the floor");
            Assert.That(Flat(_player.Position, shown), Is.LessThan(0.01f));
            Assert.That(_levels.Level.RoomAt(_player.Position), Is.SameAs(chest.Room));
            Assert.That(_health.IsAlive, Is.False);

            // F6: the same run again.
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.SameRun), Is.True);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.None));
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.RunSeed, Is.EqualTo(RunSeed));
            Assert.That(_levels.Floor, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(PlayerInputDriver.HeldByScreen, Is.False);
            Assert.That(_health.IsAlive, Is.True);
            Assert.That(_levels.Record.Kills, Is.EqualTo(0), "a new record");
            yield return null;

            // Enter: a new run.
            yield return Die();
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.NewRun), Is.True);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.RunSeed, Is.Not.EqualTo(RunSeed), "a new seed");
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            yield return null;

            // Esc: the practice room.
            yield return Die();
            Assert.That(_hud.Escape(), Is.True);
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.None));
            Assert.That(Time.timeScale, Is.EqualTo(1f));

            // F7 too.
            _levels.StartRun(RunSeed);
            yield return null;
            yield return Die();
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.False, "nothing to answer now");
        }

        [UnityTest]
        public IEnumerator ClearingTheAct_ShowsItsScreen()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            int exitKills = 0;
            for (int floor = 1; floor <= 4; floor++)
            {
                exitKills += _levels.Level.ExitEnemies.Count;
                _levels.FinishFloorForTests();
                yield return Frames(() => _levels.Choice == GeneratedLevels.ChoiceKind.FloorReward);
                Assert.That(_levels.PickReward(0), Is.True, "floor " + floor);
                if (floor == 1) Assert.That(_levels.PickFocusChoice(0), Is.True, "the focus choice"); // Changed deliberately (work order 025): after floor 1's reward the focus choice comes up before floor 2.
                yield return null;
            }
            exitKills += _levels.Level.ExitEnemies.Count;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.ActCleared);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.ActCleared));
            yield return new WaitForSecondsRealtime(Stopped);
            yield return Frames(() => _levels.Record.Kills >= exitKills);
            Assert.That(_levels.EndScreenShown, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(0f));
            RunRecord record = _levels.Record;
            Assert.That(record.Outcome, Is.EqualTo(RunOutcome.ActCleared));
            Assert.That(record.Kills, Is.EqualTo(exitKills), "every exit room's enemies, and no others");
            Assert.That(record.Skills, Is.EqualTo(_levels.SkillsTaken), "the skills taken, in order");
            Assert.That(record.Floor, Is.EqualTo(5));
            Assert.That(_hud.EndScreenLines[0], Is.EqualTo("Act cleared"));
            StringAssert.StartsWith("Floor 5 of 5", _hud.EndScreenLines[1]);
            StringAssert.Contains("Health left  " + _health.Current + " / " + record.MaxHealth, _hud.EndScreenText);
            StringAssert.Contains("Skills taken  " + string.Join(", ", _levels.SkillsTaken), _hud.EndScreenText);
            StringAssert.Contains("Enemies killed  " + exitKills, _hud.EndScreenText);

            Assert.That(_hud.AnswerEndScreen(GeneratedLevels.EndAnswer.PracticeRoom), Is.True);
            Assert.That(_levels.IsInLevel, Is.False);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
        }
    }
}
