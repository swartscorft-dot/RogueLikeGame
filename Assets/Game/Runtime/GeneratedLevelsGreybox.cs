using System.Text;
using Roguelike.Rules;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The greybox view on the floors (work order 084, <see cref="GreyboxView"/>), as part of <see cref="GeneratedLevels"/>:
    /// every floor she is on follows the setting (<see cref="GameSettings.Greybox"/>: the pause menu's line, beside the camera
    /// kick, and the developer's <see cref="GreyboxKey"/>), checked once a frame after her room is tracked, so it can be turned
    /// on and off in the middle of a run without the floor being built again. While it is on, the developer's box says what
    /// each colour means and the room she is in: its template's name and its size in metres.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Work order 084: the greybox view on and off. No F-key is free: G, for greybox, which no binding of
        /// <see cref="InputBindings"/> uses. A developer's key (work order 066, <see cref="DeveloperKeys"/>); the pause menu's
        /// line works in every build.
        /// </summary>
        public const Key GreyboxKey = Key.G;

        GreyboxView _greybox;
        GeneratedLevel _greyboxOf;

        /// <summary>The greybox view of the floor she is on, once the setting was on there; else null.</summary>
        public GreyboxView Greybox => _greybox != null && _greyboxOf == _level ? _greybox : null;

        /// <summary>Once a frame (and for tests): the floor she is on drawn as the setting says. Allocates only when it changes.</summary>
        public void FollowGreybox()
        {
            if (_level == null) return;
            if (_greyboxOf != _level)
            {
                _greyboxOf = _level;
                _greybox = null;
            }
            bool on = GameSettings.Greybox;
            if (_greybox == null)
            {
                if (!on) return;
                _greybox = GreyboxView.Add(_level);
            }
            _greybox.Set(on);
        }

        /// <summary>
        /// The keys of work order 084's tools, read with the developer's keys on, before the run's own (true: the key was
        /// one of theirs, and nothing else reads this frame's keys). Not while the pause menu, an end screen or the screen
        /// between runs is up.
        /// </summary>
        bool ToolKeys(Keyboard keyboard)
        {
            if (Paused || Ending != EndKind.None || BetweenRunsUp) return false;
            return GreyboxKeys(keyboard) || LabKeys(keyboard); // the lab's: GeneratedLevelsLab.cs
        }

        /// <summary>Once a frame, after her room is tracked and the room's fight looked at (work order 084).</summary>
        void TickTools()
        {
            FollowGreybox();
            LabTick();
        }

        /// <summary>The developer's box's lines of work order 084's tools (the floor's box; the practice room's is <see cref="ToolsPracticeLine"/>).</summary>
        void AppendTools(StringBuilder text)
        {
            AppendLab(text);
            AppendGreybox(text);
        }

        /// <summary>The practice room's box's line of work order 084's tools.</summary>
        static readonly string ToolsPracticeLine = "\n" + LabKey + " room lab   " + GreyboxKey + " greybox";

        /// <summary>The developer's G: the greybox view on or off, kept as the pause menu's line keeps it.</summary>
        bool GreyboxKeys(Keyboard keyboard)
        {
            if (!keyboard[GreyboxKey].wasPressedThisFrame) return false;
            GameSettings.SetGreybox(!GameSettings.Greybox);
            FollowGreybox();
            return true;
        }

        /// <summary>The developer's box, with the greybox view on: the legend, and the room she is in.</summary>
        void AppendGreybox(StringBuilder text)
        {
            if (!GameSettings.Greybox || _level == null) return;
            text.Append('\n').Append(GreyboxRules.Legend);
            if (CurrentRoom != null) text.Append("\n  here: ").Append(GreyboxRules.RoomWords(CurrentRoom, _level.Layout.CellSize));
        }
    }
}
