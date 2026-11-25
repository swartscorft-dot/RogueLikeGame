using NUnit.Framework;
using UnityEngine.InputSystem;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 128's test assembly: input reaches the game without the window's focus, as the PlayMode assembly's own
    /// <c>PlayModeTestSetUp</c> makes it for its tests (a set-up fixture holds for its own assembly only, and a batch run has no
    /// focus: without this a test's keyboard presses never arrive).
    /// </summary>
    [SetUpFixture]
    public class GraphicsTestsSetUp
    {
        InputSettings _settings;
        InputSettings.BackgroundBehavior _backgroundBefore;
        InputSettings.EditorInputBehaviorInPlayMode _editorBefore;

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
