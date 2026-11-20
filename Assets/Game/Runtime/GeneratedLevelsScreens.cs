using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The end of a run, the pause and the run's record (work order 018, 2026-11-20), as part of <see cref="GeneratedLevels"/>:
    /// the one place that pauses the game for a choice also pauses it for the menu and slows it to a stop at the end, so
    /// the three never fight over the time scale. What is drawn, and the keys that answer, are the HUD's (<see cref="RunHud"/>).
    ///
    /// The pause (<see cref="Pause"/>, <see cref="Resume"/>): time scale 0, kept there every frame as a choice keeps it
    /// (a hit stop that ends meanwhile puts back the scale it found), and the time scale from before given back on
    /// resume, never a hit stop's 0. No pause while a choice or an end screen is up. Starting a run, F7 and the end
    /// screens' answers close it. Game time stops, so the run's time, the floor times and the play session recorder's
    /// clock do not count it, as they do not count a choice.
    ///
    /// The end (<see cref="Ending"/>): when she dies in a run, and when the act is cleared, the run ends as before
    /// (the upgrades come off, the recorder is told), but she is not moved: the game slows from its time scale to 0 over
    /// <see cref="HudRules.EndSlowSeconds"/> of real time, and then the screen shows. She stays on the floor until it is
    /// answered (<see cref="AnswerEndScreen"/>): a new run, the same run again, or the practice room. F5, F6 and F7 do the
    /// same from anywhere, as before.
    ///
    /// The record (<see cref="Record"/>): a new <see cref="RunRecord"/> when a run starts, filled as it goes; the last
    /// one stays after the run ends, for the end screen.
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        public enum EndKind { None, Died, ActCleared }
        public enum EndAnswer { NewRun, SameRun, PracticeRoom }

        /// <summary>The record of the run going on, or of the last one; null before the first run.</summary>
        public RunRecord Record { get; private set; }
        /// <summary>The end screen up, if any: from the moment the run ended until it is answered.</summary>
        public EndKind Ending { get; private set; }
        /// <summary>True while the pause menu is up.</summary>
        public bool Paused { get; private set; }
        /// <summary>Real seconds since the run ended, while an end screen is up.</summary>
        public float SecondsSinceEnd => Ending != EndKind.None ? Time.unscaledTime - _endedAt : 0f;
        /// <summary>True once the game has stopped after the end and the screen shows.</summary>
        public bool EndScreenShown => Ending != EndKind.None && HudRules.EndScreenShown(SecondsSinceEnd);
        /// <summary>Enemies still alive in the room she is in; 0 outside a room.</summary>
        public int EnemiesLeftInRoom => AliveIn(CurrentRoom);

        /// <summary>The developer's box at the top right (the old status text): drawn or not, and its top. The HUD sets them (F1).</summary>
        public bool ShowDevBox { get; set; } = true;
        public float DevBoxTop { get; set; } = 10f;

        float _endedAt;
        float _screenScaleBefore = 1f;

        /// <summary>
        /// Pauses the game and opens the pause menu: false while a choice or an end screen is up, when already paused,
        /// or without a character.
        /// </summary>
        public bool Pause()
        {
            if (Paused || Choice != ChoiceKind.None || Ending != EndKind.None || FindPlayer() == null) return false;
            _screenScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f; // a hit stop's 0 is not kept
            Paused = true;
            Time.timeScale = 0f;
            PlayerInputDriver.HeldByScreen = true;
            return true;
        }

        /// <summary>Closes the pause menu and gives the time scale back. False when not paused.</summary>
        public bool Resume()
        {
            if (!Paused) return false;
            Paused = false;
            Time.timeScale = _screenScaleBefore;
            PlayerInputDriver.HeldByScreen = false;
            return true;
        }

        /// <summary>
        /// Answers the end screen: Enter's new run, F6's same run again, or F7's and Esc's practice room. False when no
        /// end screen is up.
        /// </summary>
        public bool AnswerEndScreen(EndAnswer answer)
        {
            if (Ending == EndKind.None) return false;
            switch (answer)
            {
                case EndAnswer.NewRun: StartNewRun(); break;
                case EndAnswer.SameRun: RestartRun(); break;
                default: BackToPracticeRoom(); break;
            }
            return true;
        }

        void OpenEnding(EndKind kind)
        {
            if (Ending != EndKind.None || kind == EndKind.None) return;
            if (Paused) Paused = false; // the scale from before the pause is the one to come back to
            else _screenScaleBefore = Time.timeScale > 0f ? Time.timeScale : 1f;
            Ending = kind;
            _endedAt = Time.unscaledTime;
            PlayerInputDriver.HeldByScreen = true;
            Debug.Log("End screen: " + kind + " on " + (Record != null ? Record.FloorText + ", " + Record.RoomText : "?"));
        }

        /// <summary>Closes the pause menu and the end screen, if up, and gives the time scale back.</summary>
        void CloseScreens()
        {
            if (!Paused && Ending == EndKind.None) return;
            Paused = false;
            Ending = EndKind.None;
            Time.timeScale = _screenScaleBefore;
            PlayerInputDriver.HeldByScreen = false;
        }

        /// <summary>Every frame (and again late, after a hit stop that ended): the pause holds 0, the end slows to 0.</summary>
        void HoldScreens()
        {
            if (Paused) Time.timeScale = 0f;
            else if (Ending != EndKind.None) Time.timeScale = _screenScaleBefore * HudRules.EndSlowdown(SecondsSinceEnd);
        }

        /// <summary>A floor of the run was built: the record is told, and counts the deaths of its enemies as kills.</summary>
        void RecordFloor(GeneratedLevel level)
        {
            RunRecord record = Record;
            if (!InRun || record == null || level == null) return;
            record.EnterFloor(Floor, level.Layout.Rooms.Count);
            if (CurrentRoom != null) record.EnterRoom(CurrentRoom.Order, CurrentRoom.Kind, CurrentRoom.Elite);
            // The same report the play session recorder counts a kill from (EnemyReportKind.Died), once per enemy.
            foreach (Enemy enemy in level.Enemies)
                if (enemy != null) enemy.Reported += r => { if (r.Kind == EnemyReportKind.Died) record.Kill(); };
        }

        /// <summary>The run ended: how, with the time and the health she has (her maximum still with the run's upgrades).</summary>
        void RecordEnd(string how)
        {
            RunOutcome outcome = how == "death" ? RunOutcome.Died : how == "act cleared" ? RunOutcome.ActCleared : RunOutcome.Left;
            Record?.End(outcome, Time.time - _runStartedAt, _health != null ? _health.Current : 0, _health != null ? _health.Max : 0);
        }
    }
}
