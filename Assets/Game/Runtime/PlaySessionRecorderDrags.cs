using System;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>One guard drags at a time (work order 093): the recorder's line for a guard's Drag held back by another's.</summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>
        /// A guard's Drag was held back by another's (<see cref="DragHold"/>), once per guard per hold: the guard as the log names
        /// it and its id, the guard that holds it (held_by), and the rule's seconds (seconds).
        /// </summary>
        public void NoteDragHeldBack(string enemy, int id, int heldBy, float seconds)
        {
            if (!_recording) return;
            try
            {
                var e = new SessionEvent(Now(), SessionEventTypes.DragHeldBack, MeleeAttacks.Drag) { Enemy = enemy, EnemyId = id };
                e.AddExtra("held_by", Int(heldBy));
                e.AddExtra("seconds", Text(seconds));
                Add(e);
            }
            catch (Exception x)
            {
                Fail(x);
            }
        }
    }
}
