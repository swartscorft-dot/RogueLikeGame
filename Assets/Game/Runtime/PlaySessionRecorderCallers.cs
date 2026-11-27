using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>The caller (work order 137): the recorder's lines for a caller's calls and for its part in a fight.</summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>A caller's call (<see cref="SessionEventTypes.CallerCall"/>): "came", "cut" or "none".</summary>
        public void NoteCallerCall(string name, int floor, int room, int order, int caller, int call, float since, int asked, int came, int alive,
                                   int called, string ids)
        {
            Note(SessionEventTypes.CallerCall, name, "floor", Int(floor), "room", Int(room), "order", Int(order), "caller", Int(caller),
                 "call", Int(call), "since", Text(since), "asked", Int(asked), "came", Int(came), "alive", Int(alive), "called", Int(called),
                 "ids", ids ?? "");
        }

        /// <summary>
        /// A caller's part in a fight, once (<see cref="SessionEventTypes.CallerEnd"/>): "died" or "left". <paramref name="firstHit"/> and
        /// <paramref name="died"/> under 0 write "".
        /// </summary>
        public void NoteCallerEnd(string name, int floor, int room, int order, int caller, float firstHit, float died, int calls, int came, int fell,
                                  int gaveWay, string gaveWayKind)
        {
            Note(SessionEventTypes.CallerEnd, name, "floor", Int(floor), "room", Int(room), "order", Int(order), "caller", Int(caller),
                 "first_hit", firstHit >= 0f ? Text(firstHit) : "", "died", died >= 0f ? Text(died) : "", "calls", Int(calls), "came", Int(came),
                 "fell", Int(fell), "gave_way", Int(gaveWay), "gave_way_kind", gaveWayKind ?? "");
        }
    }
}
