using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// The developer's keys off in a built player (work order 066, 2026-11-21, Kristian: "Bare utviklertastene av"), in the
    /// ControlRoom scene. The tests run in the Editor, where the keys are on: the switch is forced off (and on) through
    /// <see cref="DeveloperKeys.ForceForTests"/> and put back after each test. The keys are pressed on a virtual keyboard
    /// that the level controller and the HUD read in place of the real one (<see cref="GeneratedLevels.TestKeyboard"/>,
    /// <see cref="RunHud.TestKeyboard"/>). With the switch off: F1, F6, F7, F8 to F12 and C change nothing in the practice
    /// room, in a floor or on the death screen; F5 starts a run; the pause menu's lines work; and no string on screen
    /// names a dead key. With it on: the keys do what they did.
    ///
    /// A frame without a window is about 0.3 ms: the waits count frames or real time, never game time.
    /// </summary>
    public class DeveloperKeysTests
    {
        const ulong RunSeed = 0x0660066UL;
        const int MostFrames = 600;
        static readonly float Stopped = HudRules.EndSlowSeconds + 0.25f; // real seconds until the end screen surely shows

        /// <summary>A string that names a developer key as a prompt: F1, F6 to F12, or the caster's C before its word.</summary>
        static readonly Regex DeadKey = new Regex(@"\bF(1|6|7|8|9|10|11|12)\b|\b" + GeneratedLevels.CasterKey + @" caster\b|\b" + GeneratedLevels.BomberKey + @" bomber\b|\b" + GeneratedLevels.WarderKey + @" warder\b"); // work order 073: the bomber's key too; 079: the warder's

        PlayerCharacter _player;
        PlayerHealth _health;
        GeneratedLevels _levels;
        RunHud _hud;
        Keyboard _keyboard;
        bool? _forcedBefore;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            Time.timeScale = 1f;
            PlayerInputDriver.ReadRealDevices = false; // before the scene loads: the first frame must not read them either
            _forcedBefore = DeveloperKeys.ForceForTests(null); // what was forced, if anything: put back now and after the test
            DeveloperKeys.ForceForTests(_forcedBefore);
            yield return SceneManager.LoadSceneAsync("ControlRoom", LoadSceneMode.Single);
            yield return null;
            _player = Object.FindAnyObjectByType<PlayerCharacter>();
            Assert.That(_player, Is.Not.Null, "PlayerCharacter in scene");
            _player.GetComponent<PlayerInputDriver>().InputSource = () => null;
            _health = _player.GetComponent<PlayerHealth>();
            _levels = GeneratedLevels.Ensure();
            _levels.MetaValues.Enabled = false; // work order 117: the trials are on in code now; this class's tests were written with them off and turn them on themselves
            _levels.MetaValues.Attachments = false;
            _levels.RunValues.SkillTiers = false;
            _hud = RunHud.Ensure();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _levels.TestKeyboard = _keyboard;
            _hud.TestKeyboard = _keyboard;
            yield return null;
        }

        [TearDown]
        public void CleanUp()
        {
            DeveloperKeys.ForceForTests(_forcedBefore);
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            _keyboard = null;
            if (_health != null) _health.Untouchable = false;
            if (_hud != null) Object.Destroy(_hud.gameObject);
            _hud = null;
            if (_levels != null) Object.Destroy(_levels.gameObject);
            _levels = null;
            GameSettings.Unload();
            AudioListener.volume = 1f;
            Time.timeScale = 1f;
            PlayerInputDriver.HeldByChoice = false;
            PlayerInputDriver.HeldByScreen = false;
            PlayerInputDriver.ReadRealDevices = true;
        }

        static IEnumerator Frames(Func<bool> condition)
        {
            for (int i = 0; i < MostFrames && !condition(); i++) yield return null;
        }

        /// <summary>Holds these keys down (none: lets every key go) and waits until the keyboard says so.</summary>
        IEnumerator Press(params Key[] down)
        {
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(down));
            yield return Frames(() => down.All(k => _keyboard[k].isPressed) && (down.Length > 0 || !_keyboard.anyKey.isPressed));
            yield return null;
        }

        /// <summary>Presses a key and lets it go.</summary>
        IEnumerator Tap(Key key)
        {
            yield return Press(key);
            yield return Press();
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

        // ---------------- The switch ----------------

        [Test]
        public void TheSwitch_IsOnInTheEditor_AndTheSeamForcesItAndGivesItBack()
        {
            Assert.That(Application.isEditor, Is.True);
            bool? before = DeveloperKeys.ForceForTests(null);
            try
            {
                Assert.That(DeveloperKeys.On, Is.True, "on in the Editor always");
                Assert.That(DeveloperKeys.ForceForTests(false), Is.Null, "nothing was forced");
                Assert.That(DeveloperKeys.On, Is.False);
                Assert.That(DeveloperKeys.ForceForTests(true), Is.False, "off was forced");
                Assert.That(DeveloperKeys.On, Is.True);
                Assert.That(DeveloperKeys.ForceForTests(null), Is.True, "on was forced");
                Assert.That(DeveloperKeys.On, Is.True, "back to the Editor's");
            }
            finally
            {
                DeveloperKeys.ForceForTests(before);
            }
        }

        [Test]
        public void TheDevelopersKeys_AreF1_F6ToF12_AndC_AndNotF5()
        {
            // Work order 073: and B, the practice room's bomber; 079: and V, the warder's (with Shift, its pack). Work order 084:
            // and G, the greybox view, and the room lab's L, N, . and ,. Changed deliberately (work order 108): and K, the lancer's.
            Assert.That(DeveloperKeys.Keys, Is.EquivalentTo(new[] { Key.F1, Key.F6, Key.F7, Key.F8, Key.F9, Key.F10, Key.F11, Key.F12, Key.C, Key.B, Key.V,
                                                                    Key.G, Key.L, Key.N, Key.Period, Key.Comma, Key.K }));
            Assert.That(DeveloperKeys.IsDeveloperKey(GeneratedLevels.NewLevelKey), Is.False, "F5 starts a run in every build");
            Assert.That(DeveloperKeys.IsDeveloperKey(RunHud.PauseKey), Is.False, "Esc");
            Assert.That(DeveloperKeys.IsDeveloperKey(RunHud.DevTextKey), Is.True);
            Assert.That(DeveloperKeys.IsDeveloperKey(GeneratedLevels.CasterKey), Is.True);
        }

        // ---------------- Off ----------------

        [UnityTest]
        public IEnumerator Off_InThePracticeRoom_EveryDeveloperKey_ChangesNothing_AndTheDevelopersTextIsHidden()
        {
            DeveloperKeys.ForceForTests(false);
            var readout = Object.FindAnyObjectByType<DebugReadout>(FindObjectsInactive.Include);
            Assert.That(readout, Is.Not.Null, "the scene's debug readout");
            int spawned = _levels.SpawnPractice(EnemyKind.Shooter).Count; // one there, for F12 to (not) clear
            Assume.That(spawned, Is.EqualTo(1));
            yield return null;
            yield return null;
            Assert.That(_hud.DevTextShown, Is.False, "the developer's text is hidden, also in the practice room");
            Assert.That(readout.enabled, Is.False, "the debug readout with it");
            Assert.That(_levels.ShowDevBox, Is.False, "and the practice room's box of keys");

            foreach (Key key in DeveloperKeys.Keys)
            {
                yield return Tap(key);
                Assert.That(_levels.Spawned.Count, Is.EqualTo(spawned), key + ": nothing spawned, nothing cleared");
                Assert.That(_levels.InRun, Is.False, key + ": no run");
                Assert.That(_levels.IsInLevel, Is.False, key + ": no floor");
                Assert.That(_levels.LastSeed, Is.Null, key + ": no floor built");
                Assert.That(_hud.DevTextShown, Is.False, key + ": no text shown");
                Assert.That(readout.enabled, Is.False, key.ToString());
            }
            // F1 did not flip the practice room's choice underneath either: on again, the text is there as by default.
            DeveloperKeys.ForceForTests(true);
            yield return null;
            Assert.That(_hud.DevTextShown, Is.True, "F1 changed nothing while off");
        }

        [UnityTest]
        public IEnumerator Off_InAFloor_EveryDeveloperKey_ChangesNothing()
        {
            _levels.StartRun(RunSeed);
            yield return null;
            yield return null;
            bool shownOn = _hud.DevTextShown; // work order 082 (review 6, finding 17): the floor's own default with the keys on
            DeveloperKeys.ForceForTests(false);
            yield return null;
            _health.Untouchable = true;
            GeneratedLevel level = _levels.Level;
            Assert.That(level, Is.Not.Null);
            int floor = _levels.Floor;

            foreach (Key key in DeveloperKeys.Keys)
            {
                yield return Tap(key);
                Assert.That(_levels.Level, Is.SameAs(level), key + ": the same floor, not built again");
                Assert.That(_levels.InRun, Is.True, key + ": the run goes on");
                Assert.That(_levels.Floor, Is.EqualTo(floor), key.ToString());
                Assert.That(_levels.RunSeed, Is.EqualTo(RunSeed), key.ToString());
                Assert.That(_levels.Spawned, Is.Empty, key.ToString());
                Assert.That(_hud.DevTextShown, Is.False, key + ": no text shown");
                Assert.That(_levels.ShowDevBox, Is.False, key.ToString());
            }
            // Work order 082 (review 6, finding 17): F1 did not flip the floor's choice underneath while off: on again, the
            // text is as it was with the keys on before the presses.
            DeveloperKeys.ForceForTests(true);
            yield return null;
            Assert.That(_hud.DevTextShown, Is.EqualTo(shownOn), "F1 changed nothing while off, in a floor");
        }

        /// <summary>
        /// Work order 082 (review 6, finding 13): F7 at a choice with the keys off (the friends' build): the choice stays up,
        /// the run goes on and the floor is the same (work order 066's leak: F7 at a chest threw the run away).
        /// </summary>
        [UnityTest]
        public IEnumerator Off_F7_AtAChoice_ChangesNothing_TheChoiceStaysUp_AndTheRunGoesOn()
        {
            DeveloperKeys.ForceForTests(false);
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            _levels.FinishFloorForTests();
            yield return Frames(() => _levels.Choice != GeneratedLevels.ChoiceKind.None);
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "the floor's reward is up");
            GeneratedLevel level = _levels.Level;
            int floor = _levels.Floor;
            yield return new WaitForSecondsRealtime(0.5f); // past the choice's guard, where a key is read
            yield return Tap(GeneratedLevels.PracticeRoomKey);
            yield return null;
            Assert.That(_levels.Choice, Is.EqualTo(GeneratedLevels.ChoiceKind.FloorReward), "F7: the choice still up");
            Assert.That(_levels.InRun, Is.True, "F7: the run goes on");
            Assert.That(_levels.IsInLevel, Is.True, "F7: still in the floor");
            Assert.That(_levels.Level, Is.SameAs(level), "F7: the same floor");
            Assert.That(_levels.Floor, Is.EqualTo(floor));
            Assert.That(_levels.RunSeed, Is.EqualTo(RunSeed));
            // On (the Editor's, the developer's build): F7 at a choice does take her to the practice room, as work order 066 meant.
            DeveloperKeys.ForceForTests(true);
            yield return Tap(GeneratedLevels.PracticeRoomKey);
            yield return Frames(() => !_levels.InRun);
            Assert.That(_levels.InRun, Is.False, "with the keys on, F7 at a choice ends the run");
        }

        [UnityTest]
        public IEnumerator Off_F5_StartsARun_FromThePracticeRoom_AndFromAFloor()
        {
            DeveloperKeys.ForceForTests(false);
            Assert.That(_hud.PromptText, Is.EqualTo("F5  start a run"), "the prompt names F5, which works");
            yield return Tap(GeneratedLevels.NewLevelKey);
            Assert.That(_levels.InRun, Is.True, "F5 started a run");
            Assert.That(_levels.IsInLevel, Is.True);
            Assert.That(_levels.Floor, Is.EqualTo(1));
            ulong first = _levels.RunSeed.Value;
            GeneratedLevel level = _levels.Level;

            yield return Tap(GeneratedLevels.NewLevelKey);
            Assert.That(_levels.InRun, Is.True);
            Assert.That(_levels.Level, Is.Not.SameAs(level), "F5 in a floor: a new run");
            Assert.That(_levels.RunSeed.Value, Is.Not.EqualTo(first), "with a new seed");
        }

        [UnityTest]
        public IEnumerator Off_ThePauseMenusLines_Work_SameRunLeaveAndNewRun_AndEscAndEnter()
        {
            DeveloperKeys.ForceForTests(false);
            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;

            yield return Tap(RunHud.PauseKey); // Esc, a key of every build
            Assert.That(_levels.Paused, Is.True, "Esc pauses");
            Assert.That(_hud.MenuLines, Has.Member(RunHud.MenuLine.SameRun).And.Member(RunHud.MenuLine.Leave));
            GeneratedLevel level = _levels.Level;
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.SameRun), Is.True);
            Assert.That(_levels.Paused, Is.False);
            Assert.That(_levels.RunSeed, Is.EqualTo(RunSeed), "the same run");
            Assert.That(_levels.Level, Is.Not.SameAs(level), "from floor 1 again");
            yield return null;

            yield return Tap(RunHud.PauseKey);
            Assert.That(_levels.Paused, Is.True);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Leave), Is.True);
            Assert.That(_levels.InRun, Is.False, "left the run");
            Assert.That(_levels.IsInLevel, Is.False, "in the practice room");
            yield return null;

            yield return Tap(RunHud.PauseKey);
            Assert.That(_levels.Paused, Is.True);
            Assert.That(_hud.PickMenuLine(RunHud.MenuLine.Restart), Is.True);
            Assert.That(_levels.InRun, Is.True, "Start a run");
            yield return null;

            yield return Tap(RunHud.PauseKey);
            Assert.That(_levels.Paused, Is.True);
            Assert.That(_hud.Selected, Is.EqualTo(0), "on Resume");
            yield return Tap(Key.Enter);
            Assert.That(_levels.Paused, Is.False, "Enter on Resume resumes");
        }

        [UnityTest]
        public IEnumerator Off_OnTheDeathScreen_F6AndF7_DoNothing_AndEscTakesHerToThePracticeRoom()
        {
            DeveloperKeys.ForceForTests(false);
            _levels.StartRun(RunSeed);
            yield return null;
            yield return Die();
            yield return Tap(GeneratedLevels.SameLevelKey);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "F6: the death screen stays");
            yield return Tap(GeneratedLevels.PracticeRoomKey);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.Died), "F7: the death screen stays");
            Assert.That(_hud.EndScreenLines, Is.Not.Empty);
            foreach (string line in _hud.EndScreenLines) Assert.That(DeadKey.IsMatch(line), Is.False, "the end screen: " + line);
            yield return Tap(RunHud.PauseKey);
            Assert.That(_levels.Ending, Is.EqualTo(GeneratedLevels.EndKind.None), "Esc answered it");
            Assert.That(_levels.IsInLevel, Is.False, "the practice room");
        }

        [UnityTest]
        public IEnumerator Off_NoStringOnScreen_NamesADeadKey()
        {
            DeveloperKeys.ForceForTests(false);
            var shown = new List<string>();
            yield return null;
            shown.Add(_hud.PromptText);
            shown.Add(GeneratedLevels.PausedFooter); // every choice's footer, plain, in the skin and in the pop-up
            shown.Add(RunHud.EndSameRunButton);
            shown.Add(RunHud.EndPracticeButton);
            _hud.Escape();
            yield return null;
            foreach (RunHud.MenuLine line in _hud.MenuLines) shown.Add(_hud.MenuText(line));
            _hud.Escape();
            yield return null;

            _levels.StartRun(RunSeed);
            yield return null;
            _health.Untouchable = true;
            shown.Add(_levels.StatusText());
            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuLines, Has.Member(RunHud.MenuLine.SameRun).And.Member(RunHud.MenuLine.Leave));
            foreach (RunHud.MenuLine line in _hud.MenuLines) shown.Add(_hud.MenuText(line));
            shown.AddRange(_hud.KeyLines());
            _hud.Escape();
            yield return null;

            foreach (string s in shown) Assert.That(DeadKey.IsMatch(s), Is.False, "names a dead key: \"" + s + "\"");
            Assert.That(GeneratedLevels.PausedFooter, Is.EqualTo("The game is paused."));
            Assert.That(RunHud.EndSameRunButton, Is.EqualTo("Same run"));
            Assert.That(RunHud.EndPracticeButton, Is.EqualTo("Esc  practice"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.SameRun), Is.EqualTo("Same run again"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Leave), Is.EqualTo("Leave to the practice room"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Restart), Does.Contain("(F5)"), "F5 is named: it works");
            Assert.That(_levels.ShowDevBox, Is.False, "the box with F5 to F12 and C is never drawn");
            Assert.That(_hud.DevTextShown, Is.False);
        }

        // ---------------- On, as today ----------------

        [UnityTest]
        public IEnumerator On_TheKeys_DoWhatTheyDid_AndTheStringsNameThem()
        {
            DeveloperKeys.ForceForTests(true);
            Assert.That(_hud.DevTextShown, Is.True, "on in the practice room by default");
            yield return Tap(RunHud.DevTextKey);
            Assert.That(_hud.DevTextShown, Is.False, "F1 hides it");
            yield return Tap(RunHud.DevTextKey);
            Assert.That(_hud.DevTextShown, Is.True, "and shows it");

            yield return Tap(GeneratedLevels.SwarmersKey);
            Assert.That(_levels.Spawned.Count, Is.GreaterThan(0), "F9 spawns swarmers");
            yield return Tap(GeneratedLevels.ClearSpawnsKey);
            Assert.That(_levels.Spawned, Is.Empty, "F12 clears them");
            yield return Tap(GeneratedLevels.CasterKey);
            Assert.That(_levels.Spawned.Count, Is.EqualTo(1), "C spawns a caster");

            Assert.That(GeneratedLevels.PausedFooter, Is.EqualTo("The game is paused.   F7 leaves the run."));
            Assert.That(RunHud.EndSameRunButton, Is.EqualTo("F6  same run"));
            Assert.That(RunHud.EndPracticeButton, Is.EqualTo("F7 / Esc  practice"));

            yield return Tap(GeneratedLevels.SameLevelKey);
            Assert.That(_levels.InRun, Is.True, "F6 starts a run (a new one, with none before)");
            _health.Untouchable = true;
            ulong seed = _levels.RunSeed.Value;
            GeneratedLevel level = _levels.Level;
            Assert.That(_levels.StatusText(), Does.Contain("F5 new run   F6 same run   F7 practice room"));
            _hud.Escape();
            yield return null;
            Assert.That(_hud.MenuText(RunHud.MenuLine.SameRun), Is.EqualTo("Same run again   (F6)"));
            Assert.That(_hud.MenuText(RunHud.MenuLine.Leave), Is.EqualTo("Leave to the practice room   (F7)"));
            _hud.Escape();
            yield return null;
            yield return Tap(GeneratedLevels.SameLevelKey);
            Assert.That(_levels.RunSeed, Is.EqualTo(seed), "F6: the same run");
            Assert.That(_levels.Level, Is.Not.SameAs(level), "again from floor 1");
            yield return Tap(GeneratedLevels.PracticeRoomKey);
            Assert.That(_levels.IsInLevel, Is.False, "F7: the practice room");
            Assert.That(_levels.InRun, Is.False);
        }
    }
}
