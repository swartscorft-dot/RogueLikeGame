using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Set-up shared by every PlayMode test in this assembly, once around the whole run.
    ///
    /// Virtual devices read whether or not the game has focus. Some tests add a keyboard and a mouse and queue state
    /// on them. With the Input System's default settings, a game without focus hands keyboard and pointer events to
    /// the editor updates instead of the game's, so the state the game sees never changes and those tests read
    /// nothing. Without a window (-batchmode) the game never has focus. For the run, the settings send all device
    /// input to the game regardless of focus; the old values come back afterwards.
    ///
    /// The two values are changed on the settings object in use rather than by assigning a new one:
    /// assigning destroys the one in use when it is the Input System's own temporary default (no settings asset,
    /// as in this project), and there would be nothing left to put back.
    ///
    /// The real mouse and keyboard still steer no test: every test class sets
    /// <see cref="PlayerInputDriver.ReadRealDevices"/> to false before it loads the scene, and the tests that read
    /// devices read their own virtual ones.
    ///
    /// A run starts with four skills equipped (decision 0005, 2026-11-19). The tests were written for all eight, so every
    /// scene that loads during the run has all eight equipped on its character, through <see cref="PlayerCharacter.EquipSkill"/>,
    /// before its first frame. A test about the start four puts them back with <see cref="PlayerCharacter.EquipStartSkills"/>.
    /// </summary>
    [SetUpFixture]
    public class PlayModeTestSetUp
    {
        InputSettings _settings;
        InputSettings.BackgroundBehavior _backgroundBefore;
        InputSettings.EditorInputBehaviorInPlayMode _editorBefore;

        [OneTimeSetUp]
        public void EquipEverySkillOnLoad() => SceneManager.sceneLoaded += EquipEverySkill;

        [OneTimeTearDown]
        public void StopEquipping() => SceneManager.sceneLoaded -= EquipEverySkill;

        static void EquipEverySkill(Scene scene, LoadSceneMode mode)
        {
            foreach (PlayerCharacter player in Object.FindObjectsByType<PlayerCharacter>(FindObjectsSortMode.None))
                for (int slot = 0; slot < player.Skills.Count; slot++) player.EquipSkill(slot);
        }

        [OneTimeSetUp]
        public void InputRegardlessOfFocus()
        {
            _settings = InputSystem.settings;
            _backgroundBefore = _settings.backgroundBehavior;
            _editorBefore = _settings.editorInputBehaviorInPlayMode;
            _settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        }

        [OneTimeTearDown]
        public void RestoreInputSettings()
        {
            if (_settings == null) return;
            _settings.backgroundBehavior = _backgroundBefore;
            _settings.editorInputBehaviorInPlayMode = _editorBefore;
        }
    }
}
