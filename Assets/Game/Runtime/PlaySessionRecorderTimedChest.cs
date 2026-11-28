using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>The timed chest (work order 153): the recorder's one line a floor that held one (<see cref="TimedChestEvents.Chest"/>). Nothing is written a frame.</summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A floor's timed chest, as its reward was taken, she died in its fight, or the floor went (<paramref name="name"/>, one of
        /// <see cref="TimedChestEvents"/>' names): the room, whether it was opened and how long after the room was cleared, its wave's kinds
        /// and count, the seconds to clear it, the share of the reward left then, the Echoes it held whole and those taken, her damage taken
        /// in its fight. Seconds and the share under 0 write "".
        /// </summary>
        public void NoteTimedChest(string name, int floor, int room, int order, bool opened, float afterClear, string kinds, int count, float clear,
                                   double share, int whole, int echoes, int damage) =>
            Note(TimedChestEvents.Chest, name, "floor", Int(floor), "room", Int(room), "order", Int(order), "opened", opened ? "true" : "false",
                 "after_clear", TimedChestEvents.Seconds(afterClear), "kinds", kinds ?? "", "count", Int(count), "clear", TimedChestEvents.Seconds(clear),
                 "share", TimedChestEvents.Share(share), "whole", Int(whole), "echoes", Int(echoes), "damage", Int(damage));
    }
}
