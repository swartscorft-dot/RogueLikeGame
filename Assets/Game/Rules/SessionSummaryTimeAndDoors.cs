using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>Where a floor's time went (work order 090), in a run of the session summary.</summary>
    public sealed class FloorTimeRow
    {
        public int Floor;
        /// <summary>Game seconds on the floor: from her coming onto it to the next floor, the run's end, or the session's end.</summary>
        public double Seconds;
        /// <summary>Game seconds of it in a fight (an enemy awake: a room's waking to its clearing, or her death), wherever she stood.</summary>
        public double Fights;
        /// <summary>Game seconds of it in a passage outside a fight.</summary>
        public double Passages;
        /// <summary>Game seconds of it in a room (the entrance, a room cleared or not yet woken, a treasure room) outside a fight: what is left.</summary>
        public double Rooms;
        /// <summary>Real seconds a screen held the game still on it (the pause menu, its choices): not in <see cref="Seconds"/>, the game's clock stood.</summary>
        public double Screens;
        public int ScreensShown;
    }

    /// <summary>Why a run's room doors opened, and the pulls of enemies of a fight that were not inside (work order 090, from work order 086's events).</summary>
    public sealed class DoorReasons
    {
        public int Cleared, HerDeath, SheLeft, EnemyOutside, Other;
        /// <summary>Pulls before the doors shut (they waited for an enemy in the passage or a doorway) and while they stood shut (one found outside them).</summary>
        public int Pulls, PullsWhileShut;
        /// <summary>Pulls that put at least one enemy inside, and pulls that left one outside.</summary>
        public int PullsThatPutIn, PullsFailed;
        /// <summary>Enemies put inside, in all.</summary>
        public int PutIn;
        /// <summary>Any door event in the run at all.</summary>
        public bool Any;

        /// <summary>
        /// The rare lock case of work order 086's second review: the doors opened because an enemy of the fight was left outside them,
        /// or a pull could not put one inside.
        /// </summary>
        public bool LockCase => EnemyOutside > 0 || PullsFailed > 0;
    }

    /// <summary>
    /// Work order 090 in the session summary: for each run, where its time went, per floor and for the run (seconds in fights, in
    /// rooms outside a fight, in passages, all game time and adding up to the floor's time; and the real seconds under the screens
    /// and menus, which hold the game's clock still and so are beside it, not in it), and why its doors opened (cleared, her death,
    /// she left, an enemy outside) with the pulls that put an enemy inside and those that failed, a line that says at a glance
    /// whether the rare lock case happened.
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>Fills each run's <see cref="RunRow.FloorTimes"/> and <see cref="RunRow.Doors"/>, from the events (sorted by time) and the fights' spans.</summary>
        static void ComputeTimeAndDoors(List<SessionEvent> events, SessionStats stats)
        {
            int runIndex = -1;
            RunRow run = null;         // the run whose floor is going on, null between runs
            RunRow lastRun = null;     // the last run started: a door that opens after its end (her death) is still its
            bool inLab = false;
            FloorTimeRow floor = null;
            double floorFrom = 0d, placeFrom = 0d;
            bool inPassage = false;
            var passages = new List<KeyValuePair<double, double>>();
            double end = stats.Length;

            void EndPlace(double t)
            {
                if (floor != null && inPassage && t > placeFrom) passages.Add(new KeyValuePair<double, double>(placeFrom, t));
                placeFrom = t;
            }
            void EndFloor(double t)
            {
                if (floor == null) return;
                EndPlace(t);
                var span = new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(floorFrom, Math.Max(floorFrom, t)) };
                floor.Seconds += Math.Max(0d, t - floorFrom);
                double fights = Overlap(stats.FightSpans, span);
                double passageFights = Overlap(passages, stats.FightSpans);
                double inPassages = passages.Sum(p => p.Value - p.Key);
                floor.Fights += fights;
                floor.Passages += Math.Max(0d, inPassages - passageFights);
                floor.Rooms = Math.Max(0d, floor.Seconds - floor.Fights - floor.Passages);
                passages.Clear();
                floor = null;
                inPassage = false;
            }

            foreach (SessionEvent e in events)
            {
                switch (e.Type)
                {
                    case SessionEventTypes.RunStart:
                        EndFloor(e.T);
                        runIndex++;
                        run = lastRun = runIndex < stats.Runs.Count ? stats.Runs[runIndex] : null;
                        inLab = false;
                        break;
                    case SessionEventTypes.FloorEnter:
                    {
                        EndFloor(e.T);
                        if (run == null) break;
                        int n = int.TryParse(e.ExtraValue("floor"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int f) ? f : 0;
                        floor = TimeOf(run, n);
                        floorFrom = placeFrom = e.T;
                        inPassage = false;
                        break;
                    }
                    case SessionEventTypes.RoomEntered:
                        if (floor == null) break;
                        EndPlace(e.T);
                        inPassage = e.Name == "passage";
                        break;
                    case SessionEventTypes.RunEnd:
                        EndFloor(e.T);
                        run = null;
                        break;
                    case LabRoomEvents.Room:
                        EndFloor(e.T);
                        run = null;
                        inLab = true;
                        break;
                    case SessionEventTypes.RoomDoor:
                        if (lastRun != null && !inLab) CountDoorReason(lastRun.Doors, e);
                        break;
                    case SessionEventTypes.Screen:
                    {
                        int r = int.TryParse(e.ExtraValue("run"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rn) ? rn : 0;
                        int f = int.TryParse(e.ExtraValue("floor"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int fn) ? fn : 0;
                        if (r < 1 || r > stats.Runs.Count || f < 1) break;
                        if (!double.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out double s)) break;
                        FloorTimeRow row = TimeOf(stats.Runs[r - 1], f);
                        row.Screens += Math.Max(0d, s);
                        row.ScreensShown++;
                        break;
                    }
                }
            }
            EndFloor(end);
        }

        static FloorTimeRow TimeOf(RunRow run, int floor)
        {
            if (!run.FloorTimes.TryGetValue(floor, out FloorTimeRow row)) run.FloorTimes[floor] = row = new FloorTimeRow { Floor = floor };
            return row;
        }

        /// <summary>A room_door event into a run's reasons: an opening by its why, a pull by its counts.</summary>
        static void CountDoorReason(DoorReasons d, SessionEvent e)
        {
            d.Any = true;
            if (e.Name == "opened")
            {
                string why = e.ExtraValue("why");
                if (why == RoomDoorRules.Word(DoorOpened.Cleared)) d.Cleared++;
                else if (why == RoomDoorRules.Word(DoorOpened.HerDeath)) d.HerDeath++;
                else if (why == RoomDoorRules.Word(DoorOpened.SheLeft)) d.SheLeft++;
                else if (why == RoomDoorRules.Word(DoorOpened.EnemyOutside)) d.EnemyOutside++;
                else d.Other++;
            }
            else if (e.Name == "pull")
            {
                int putIn = int.TryParse(e.ExtraValue("in"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int i) ? i : 0;
                int left = int.TryParse(e.ExtraValue("left"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int l) ? l : 0;
                d.Pulls++;
                if (e.ExtraValue("shut") == "true") d.PullsWhileShut++;
                if (putIn > 0) d.PullsThatPutIn++;
                if (left > 0) d.PullsFailed++;
                d.PutIn += Math.Max(0, putIn);
            }
        }

        // ---------------- Writing ----------------

        const string TimeIndent = "                   "; // under the run block's labels (19 characters)

        /// <summary>
        /// A run's "where time went" lines: the run's, then a line a floor. "run 6:20 = fights 3:01 + rooms 1:10 + passages 2:09;
        /// screens and menus 1:02 more (real time, the game stood still)". Nothing for a run with no floor.
        /// </summary>
        static void AppendWhereTimeWent(StringBuilder sb, RunRow run)
        {
            if (run.FloorTimes.Count == 0) return;
            var all = new FloorTimeRow
            {
                Seconds = run.FloorTimes.Values.Sum(f => f.Seconds), Fights = run.FloorTimes.Values.Sum(f => f.Fights),
                Rooms = run.FloorTimes.Values.Sum(f => f.Rooms), Passages = run.FloorTimes.Values.Sum(f => f.Passages),
                Screens = run.FloorTimes.Values.Sum(f => f.Screens), ScreensShown = run.FloorTimes.Values.Sum(f => f.ScreensShown),
            };
            sb.Append("  where time went  ").Append("run ").AppendLine(TimeWords(all, true));
            foreach (FloorTimeRow f in run.FloorTimes.Values)
                sb.Append(TimeIndent).Append("floor ").Append(f.Floor.ToString(CultureInfo.InvariantCulture)).Append(' ').AppendLine(TimeWords(f, false));
        }

        /// <summary>"3:10 = fights 1:35 + rooms 0:38 + passages 0:57; screens 0:41 more (3 shown)".</summary>
        public static string TimeWords(FloorTimeRow f, bool wholeRun)
        {
            string words = Duration((float)f.Seconds) + " = fights " + Duration((float)f.Fights) + " + rooms " + Duration((float)f.Rooms)
                           + " + passages " + Duration((float)f.Passages);
            if (f.ScreensShown > 0)
                words += ";  " + (wholeRun ? "screens and menus " : "screens ") + Duration((float)f.Screens) + " more"
                         + (wholeRun ? " (real time, the game stood still; " + f.ScreensShown.ToString(CultureInfo.InvariantCulture) + " shown)" : "");
            return words;
        }

        /// <summary>
        /// A run's "doors opened" line, only when its doors did anything: "cleared 9, her death 1, she left 0, an enemy outside 0;
        /// pulls 3 (0 while shut): 3 put an enemy inside, 0 failed;  the rare lock case: no".
        /// </summary>
        static void AppendDoorReasons(StringBuilder sb, RunRow run)
        {
            DoorReasons d = run.Doors;
            if (!d.Any) return;
            sb.Append("  doors opened     ").AppendLine(DoorReasonWords(d));
        }

        public static string DoorReasonWords(DoorReasons d)
        {
            string Int(int n) => n.ToString(CultureInfo.InvariantCulture);
            string words = "cleared " + Int(d.Cleared) + ", her death " + Int(d.HerDeath) + ", she left " + Int(d.SheLeft) + ", an enemy outside " + Int(d.EnemyOutside)
                           + (d.Other > 0 ? ", other " + Int(d.Other) : "")
                           + ";  pulls " + Int(d.Pulls) + " (" + Int(d.PullsWhileShut) + " while shut): " + Int(d.PullsThatPutIn) + " put an enemy inside, "
                           + Int(d.PullsFailed) + " failed;  ";
            return words + (d.LockCase ? "THE RARE LOCK CASE HAPPENED (an enemy outside the shut doors, or a pull that failed)" : "the rare lock case: no");
        }
    }
}
