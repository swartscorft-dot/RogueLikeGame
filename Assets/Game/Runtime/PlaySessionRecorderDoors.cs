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
    }
}
