using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>The door closes behind her (work order 086): the recorder's line for a room's doors closing and opening.</summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A room's doors closed: the floor, the room as the run box counts it, its place in the run.</summary>
        public void NoteRoomDoorsClosed(int floor, int room, int order) =>
            Note(SessionEventTypes.RoomDoor, "closed", "floor", Int(floor), "room", Int(room), "order", Int(order));

        /// <summary>A room's doors opened: as above, the seconds since they closed, and why (<see cref="RoomDoorRules.Word"/>).</summary>
        public void NoteRoomDoorsOpened(int floor, int room, int order, float seconds, string why) =>
            Note(SessionEventTypes.RoomDoor, "opened", "floor", Int(floor), "room", Int(room), "order", Int(order), "seconds", Text(seconds), "why", why);

        /// <summary>
        /// Work order 090: a pull of the enemies of the fight not inside (work order 086's second review): before the doors shut
        /// (they waited for them), or while they stood shut (one was found outside them); how many were put inside, how many were left.
        /// </summary>
        public void NoteRoomDoorsPull(int floor, int room, int order, bool shut, int putIn, int left) =>
            Note(SessionEventTypes.RoomDoor, "pull", "floor", Int(floor), "room", Int(room), "order", Int(order), "shut", shut ? "true" : "false",
                 "in", Int(putIn), "left", Int(left));
    }
}
