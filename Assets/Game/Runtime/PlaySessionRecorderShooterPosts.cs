using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// Shooters that hold their place (work order 098) in the log: one line a shooter a room's fight at most
    /// (<see cref="ShooterPostEvents"/>), told by the shooter as its part in the fight ends; since work order 101 the casters' the same
    /// (<see cref="CasterPostEvents"/>). Nothing is written a frame.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A shooter's part in a room's fight ended: its floor, its room's place in the floor, the game time the fight woke, whether it
        /// held a post, and the seconds from the waking until she first came within its reach and until its death (under 0: never).
        /// </summary>
        public void NoteShooterPost(int floor, int order, float wokeAt, bool held, float reach, float died) =>
            Note(ShooterPostEvents.Post, null, "floor", Int(floor), "order", Int(order),
                 "woke", ((double)(wokeAt - _startTime)).ToString("0.000", CultureInfo.InvariantCulture), "held", held ? "1" : "0",
                 "reach", reach >= 0f ? Seconds(reach) : "-", "died", died >= 0f ? Seconds(died) : "-");

        /// <summary>
        /// A caster's part in a room's fight ended (work order 101): the shooter's line with the same fields (<see cref="CasterPostEvents"/>),
        /// one a caster a fight at most, told by the caster as its part ends.
        /// </summary>
        public void NoteCasterPost(int floor, int order, float wokeAt, bool held, float reach, float died) =>
            Note(CasterPostEvents.Post, null, "floor", Int(floor), "order", Int(order),
                 "woke", ((double)(wokeAt - _startTime)).ToString("0.000", CultureInfo.InvariantCulture), "held", held ? "1" : "0",
                 "reach", reach >= 0f ? Seconds(reach) : "-", "died", died >= 0f ? Seconds(died) : "-");

        static string Seconds(float s) => s.ToString("0.00", CultureInfo.InvariantCulture);
    }
}
