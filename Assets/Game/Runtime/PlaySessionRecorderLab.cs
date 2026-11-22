using System;
using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The room lab's events (work order 084, <see cref="LabRoomEvents"/>), as part of the play session recorder: which room each
    /// lab room was (its template, kind, enemy mix, floor and seed) and how long it took to build, when it was cleared, and how
    /// it was left. With her damage and deaths in the log beside them, the summary gives a table per lab room
    /// (<see cref="LabRoomsSummary"/>), so two versions of a room can be compared.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A lab room was built: its room's name, kind, mix, floor and seed, and the real milliseconds the build took.</summary>
        public void NoteLabRoom(string name, string kind, string mix, int floor, string seed, float ms) =>
            Note(LabRoomEvents.Room, name, "kind", kind, "mix", mix, "floor", Int(floor), "seed", seed, "ms", ms.ToString("0", CultureInfo.InvariantCulture));

        /// <summary>The lab room was cleared: the game seconds from its fight's start (a treasure room: from its build to its chest).</summary>
        public void NoteLabCleared(float seconds) => Note(LabRoomEvents.Clear, null, "seconds", Text(seconds));

        /// <summary>The lab room was left: built again ("again", "new seed", "next", "previous", "picked", "died") or the lab left ("left", "death").</summary>
        public void NoteLabLeft(string how)
        {
            if (!_recording) return;
            try { Add(new SessionEvent(Now(), LabRoomEvents.End) { Result = how }); }
            catch (Exception e) { Fail(e); }
        }
    }
}
