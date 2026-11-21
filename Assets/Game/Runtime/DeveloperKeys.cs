using System;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The one place that says whether the developer's keys are on (work order 066, 2026-11-21, Kristian: "Bare
    /// utviklertastene av"): on in the Editor always; in a built player only when it was started with "-dev" (or the
    /// benchmark's "-benchmark"), read from the command line once (<see cref="DeveloperKeysRule"/>). Every reader of a key
    /// in <see cref="Keys"/> asks <see cref="On"/>, and every string on screen that names one is shown only while it is on.
    ///
    /// The developer's keys: F1 (the developer's text, <see cref="RunHud.DevTextKey"/>), F6 (the same run again,
    /// <see cref="GeneratedLevels.SameLevelKey"/>), F7 (the practice room, <see cref="GeneratedLevels.PracticeRoomKey"/>),
    /// F8 to F12 and C (the practice room's spawns and their clearing). Not the developer's, and working in every build:
    /// F5 (start a run, <see cref="GeneratedLevels.NewLevelKey"/>), Esc and the pause menu's lines, and everything of
    /// <see cref="InputBindings"/>; nor F2, F3 and F4 (the practice room's ways of the focus gauge, work order 025), which
    /// the friends' README of 2026-11-21 did not name as the developer's: they are left as they were.
    ///
    /// Tests run in the Editor, where it is on: <see cref="ForceForTests"/> turns it off (or on) and gives back what it
    /// was, to be put back afterwards, as the project's other switches are tested.
    /// </summary>
    public static class DeveloperKeys
    {
        /// <summary>The developer's keys (work order 066; the friends' README: "F1, F6 to F12 and C").</summary>
        public static readonly Key[] Keys =
        {
            RunHud.DevTextKey, GeneratedLevels.SameLevelKey, GeneratedLevels.PracticeRoomKey, GeneratedLevels.BossKey,
            GeneratedLevels.SwarmersKey, GeneratedLevels.ShooterKey, GeneratedLevels.GuardKey, GeneratedLevels.ClearSpawnsKey,
            GeneratedLevels.CasterKey,
        };

        static int s_fromStart = -1; // -1 not read yet, 0 off, 1 on
        static int s_forced = -1;    // -1 none, 0 off, 1 on

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_fromStart = -1;
            s_forced = -1;
        }

        /// <summary>True when the developer's keys do what they do, and the strings that name them show.</summary>
        public static bool On
        {
            get
            {
                if (s_forced >= 0) return s_forced == 1;
                if (s_fromStart < 0) s_fromStart = DeveloperKeysRule.On(Application.isEditor, Environment.GetCommandLineArgs()) ? 1 : 0;
                return s_fromStart == 1;
            }
        }

        /// <summary>True for a key in <see cref="Keys"/>.</summary>
        public static bool IsDeveloperKey(Key key) => Array.IndexOf(Keys, key) >= 0;

        /// <summary>
        /// For tests: forces the switch on or off (null: as the Editor or the command line says) and returns what was forced
        /// before (null when nothing was), for the test to put back.
        /// </summary>
        public static bool? ForceForTests(bool? on)
        {
            bool? before = s_forced < 0 ? (bool?)null : s_forced == 1;
            s_forced = on.HasValue ? (on.Value ? 1 : 0) : -1;
            return before;
        }
    }
}
