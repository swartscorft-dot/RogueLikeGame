using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The rooms you can use (work order 088), as part of the play session recorder: for each room of a floor with a fight, how many
    /// tall and low covers it had as built (<see cref="FightCoverEvents"/>). With the room's fight and the strikes that were stopped
    /// beside them in the log, the summary gives each room's fight its cover and what the cover stopped.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A room of the floor with a fight: the floor, its place in the floor, and its tall and low covers by the one table.</summary>
        public void NoteRoomCover(int floor, int order, int tall, int low) =>
            Note(FightCoverEvents.Room, null, "floor", Int(floor), "order", Int(order), "tall", Int(tall), "low", Int(low));
    }
}
