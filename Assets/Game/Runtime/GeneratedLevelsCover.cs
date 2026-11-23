using System.Collections.Generic;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The rooms you can use (work order 088): when a floor of a run is entered, the recorder is told, for every room of it with a
    /// fight (a combat, elite or exit room that holds enemies), how many tall and low covers it has as built: its props and the
    /// mood's large props that stand (those a large prop took the place of not counted), sorted by the one table
    /// (<see cref="PropHeights"/>: tall stops every shot, low she shoots over and ducks behind).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        void NoteRoomCovers(GeneratedLevel level, int floor)
        {
            PlaySessionRecorder recorder = PlaySessionRecorder.Current;
            if (recorder == null || level == null || level.Layout == null) return;
            var tall = new Dictionary<int, int>();
            var low = new Dictionary<int, int>();
            void Count(IReadOnlyList<GeneratedLevel.PlacedProp> props)
            {
                if (props == null) return;
                foreach (GeneratedLevel.PlacedProp p in props)
                {
                    if (p?.Room == null || p.Prop == null || p.Object == null) continue;
                    if (FightCover.IsTall(p.Prop)) tall[p.Room.Order] = (tall.TryGetValue(p.Room.Order, out int t) ? t : 0) + 1;
                    else if (FightCover.IsLow(p.Prop)) low[p.Room.Order] = (low.TryGetValue(p.Room.Order, out int l) ? l : 0) + 1;
                }
            }
            Count(level.Props);
            Count(level.LargeProps);
            foreach (LevelRoom room in level.Layout.Rooms)
            {
                if ((room.Kind != RoomKind.Combat && room.Kind != RoomKind.Exit) || room.EnemyPoints.Count == 0) continue;
                recorder.NoteRoomCover(floor, room.Order, tall.TryGetValue(room.Order, out int t) ? t : 0, low.TryGetValue(room.Order, out int l) ? l : 0);
            }
        }
    }
}
