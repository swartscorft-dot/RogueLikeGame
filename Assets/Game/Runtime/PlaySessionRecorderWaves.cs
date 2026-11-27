using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>Two waves (work order 136): the recorder's lines for a room's waves, and its room_fight with the number of waves.</summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A room's fight started on a floor set up with waves (work order 136): its room_fight as <see cref="NoteRoomFight"/> writes it,
        /// with the room's number of waves after (1 for a room that wakes whole, as before) and, in "woke", only the enemies that woke
        /// (the later waves' are not in the room yet).
        /// </summary>
        public void NoteRoomFightWithWaves(int floor, int room, int order, string kind, string how, bool inside, int woke, string lineUp, int waves)
        {
            if (lineUp == null)
                Note(SessionEventTypes.RoomFight, how, "floor", Int(floor), "room", Int(room), "order", Int(order), "kind", kind,
                     "inside", inside ? "true" : "false", "woke", Int(woke), "waves", Int(waves));
            else
                Note(SessionEventTypes.RoomFight, how, "floor", Int(floor), "room", Int(room), "order", Int(order), "kind", kind,
                     "inside", inside ? "true" : "false", "woke", Int(woke), "line_up", lineUp, "waves", Int(waves));
        }

        /// <summary>
        /// A wave of a room's fight started (<paramref name="name"/> "start"), or some of its enemies came after waiting for a point
        /// ("arrival"): see <see cref="SessionEventTypes.RoomWave"/>. <paramref name="beforeAlive"/> under 0 writes "" (wave 1 has none before).
        /// </summary>
        public void NoteRoomWave(string name, int floor, int room, int order, int wave, int waves, string kinds, int count, float since,
                                 int beforeAlive, string arrivals, int waiting, float waited)
        {
            if (name == "arrival")
                Note(SessionEventTypes.RoomWave, name, "floor", Int(floor), "room", Int(room), "order", Int(order), "wave", Int(wave), "waves", Int(waves),
                     "since", Text(since), "waited", Text(waited), "arrivals", arrivals ?? "", "waiting", Int(waiting));
            else
                Note(SessionEventTypes.RoomWave, name, "floor", Int(floor), "room", Int(room), "order", Int(order), "wave", Int(wave), "waves", Int(waves),
                     "kinds", kinds ?? "", "count", Int(count), "since", Text(since), "before_alive", beforeAlive >= 0 ? Int(beforeAlive) : "",
                     "arrivals", arrivals ?? "", "waiting", Int(waiting));
        }
    }
}
