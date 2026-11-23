using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The recorder's event of the rooms you can use (work order 088), kept with its summary here as the room lab's are
    /// (<see cref="LabRoomEvents"/>): when a floor of a run is entered, one for every room of it with a fight.
    /// </summary>
    public static class FightCoverEvents
    {
        /// <summary>A room with a fight as built: floor, order (its place in the floor), tall (its tall covers), low (its low covers), by the one table (<see cref="PropHeights"/>).</summary>
        public const string Room = "room_cover";
    }

    /// <summary>One room's fight of a run, with its cover (work order 088): the room's tall and low covers, and what was stopped while it was fought.</summary>
    public sealed class FightCoverRow
    {
        public int Floor;
        /// <summary>The room counted from 1 without the passages, as the run box and the room fights count it.</summary>
        public int Room;
        public int Order;
        public string Kind = "";
        public int Tall, Low;
        /// <summary>Enemy strikes the low prop she was ducked behind stopped ("stopped by cover").</summary>
        public int StoppedDucked;
        /// <summary>Things that flew at her that a wall or a tall prop stopped ("stopped by a wall"; the log does not tell the two apart).</summary>
        public int StoppedStanding;
    }

    public static partial class SessionSummary
    {
        /// <summary>
        /// The rooms' fights of each run with their cover (work order 088): each room_fight of a run is a row, with the tall and low
        /// covers its room_cover event said its room had, and the strikes stopped from it until the next room's fight starts, the
        /// next floor is entered or the run ends. Rows only for a log with room_cover events (from version 19's floors on).
        /// </summary>
        static void ComputeFightCover(List<SessionEvent> events, SessionStats stats)
        {
            int Number(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            int runIndex = -1;
            var cover = new Dictionary<(int floor, int order), (int tall, int low)>();
            FightCoverRow current = null;
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.RunStart)
                {
                    runIndex++;
                    cover.Clear();
                    current = null;
                    continue;
                }
                if (runIndex < 0 || runIndex >= stats.Runs.Count) continue;
                RunRow run = stats.Runs[runIndex];
                if (e.Type == FightCoverEvents.Room)
                    cover[(Number(e, "floor"), Number(e, "order"))] = (Number(e, "tall"), Number(e, "low"));
                else if (e.Type == SessionEventTypes.RoomFight)
                {
                    current = null;
                    if (!cover.TryGetValue((Number(e, "floor"), Number(e, "order")), out (int tall, int low) c)) continue;
                    current = new FightCoverRow
                    {
                        Floor = Number(e, "floor"), Room = Number(e, "room"), Order = Number(e, "order"), Kind = e.ExtraValue("kind") ?? "", Tall = c.tall, Low = c.low,
                    };
                    run.FightCover.Add(current);
                }
                else if (e.Type == SessionEventTypes.FloorEnter || e.Type == SessionEventTypes.RunEnd) current = null;
                else if (current != null && e.Type == SessionEventTypes.EnemyStrike)
                {
                    if (e.Result == SessionWords.StoppedByCover) current.StoppedDucked++;
                    else if (e.Result == SessionWords.StoppedByWall) current.StoppedStanding++;
                }
            }
        }

        /// <summary>The run's rooms' fights with their cover (work order 088), a line each under one heading; nothing for a run with none.</summary>
        static void AppendFightCover(StringBuilder sb, RunRow run)
        {
            if (run.FightCover.Count == 0) return;
            sb.Append("  cover in fights  ").AppendLine(string.Join(";  ", run.FightCover.Select(r =>
                string.Format(CultureInfo.InvariantCulture, "floor {0} room {1} ({2}): {3} tall, {4} low, stopped {5} ducked, {6} by a wall or tall cover",
                              r.Floor, r.Room, r.Kind.Length == 0 ? "?" : r.Kind, r.Tall, r.Low, r.StoppedDucked, r.StoppedStanding))));
        }
    }
}
