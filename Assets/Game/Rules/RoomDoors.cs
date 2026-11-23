using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>What a room's open doors do this frame (work order 086, <see cref="RoomDoorRules.WhenOpen"/>).</summary>
    public enum DoorStep
    {
        /// <summary>They stay open: the switch off, no fight on, she dead, or she not inside the room.</summary>
        StayOpen,
        /// <summary>They close now: the fight is on, she is inside, and so is every living enemy of the fight.</summary>
        Close,
        /// <summary>She is inside and the fight on, but an enemy of it is not inside yet: they wait for it.</summary>
        Wait,
        /// <summary>They have waited <see cref="RoomFightValues.DoorPullAfter"/>: the enemies not inside are pulled in, and they close.</summary>
        PullIn,
    }

    /// <summary>Why a room's closed doors opened (work order 086): the recorder's word for it.</summary>
    public enum DoorOpened
    {
        /// <summary>Every enemy of the fight is dead.</summary>
        Cleared,
        /// <summary>She died (the run's end).</summary>
        HerDeath,
        /// <summary>She was found off the room's floor (a teleport, a new floor, the benchmark's staging): the doors never stand with her outside.</summary>
        SheLeft,
        /// <summary>An enemy of the fight was outside them and could not be put back in for <see cref="RoomFightValues.DoorOutsideMost"/> (the second review of work order 086).</summary>
        EnemyOutside,
    }

    /// <summary>One door's block, in the level's metres: its middle on the ground, along which line, and its size.</summary>
    public struct DoorBlock
    {
        public LevelPoint Centre;
        /// <summary>On a vertical line (running north and south): its length along the world's z; else along x.</summary>
        public bool Vertical;
        public double Length, Thickness, Height;
    }

    /// <summary>
    /// The door closes behind her (work order 086; Kristian's answer of 2026-11-22: "Døra stenges bak deg, men vi bør balansere
    /// rommene med flere muligheter og, slik at man kan bruke miljøet til sin fordel"). No engine.
    ///
    /// The rule. While a room's fight is on (work order 070) and she is inside the room, every door of the room (the way she came
    /// in, and the way on, which today stands open through the fight) is shut by a block, and stays shut until the fight ends:
    /// every enemy of it dead (the room cleared), or she dead. "Inside" (<see cref="IsInside"/>): on the room's own floor and at
    /// least <see cref="RoomFightValues.DoorClear"/> (1.5 m) from every door's opening of the room. The doors close only when
    /// she is inside and so is every living enemy of the fight; while one is not (in the passage, or in a doorway) they wait,
    /// and after <see cref="RoomFightValues.DoorPullAfter"/> (2 s) the ones not inside are pulled in, and they close. So she is
    /// never shut out (she must be inside), never caught in the doorway (1.5 m from it), and never shut in with an enemy of the
    /// fight on the other side. If she is ever found off the room's floor while its doors stand (a teleport), they open.
    ///
    /// The wake cases, with <see cref="RoomFightValues.WakeByLineOrHurtOnly"/> (on with the doors): the room wakes by her line or
    /// by a hurt, never by an enemy's range. At the line (5 m in) she is inside: the doors close in the same frame. Woken short of
    /// the line by a hurt from inside: they close once she is 1.5 m in. Woken from outside (a shot from the passage): they stay
    /// open while she is outside, the room comes at her into the passage as on main, and they close behind her when she steps in
    /// (waiting for, then pulling in, the enemies that followed her out).
    /// </summary>
    public static class RoomDoorRules
    {
        const double Slack = 1e-9;

        /// <summary>Are the doors on (<see cref="RoomFightValues.Doors"/>)?</summary>
        public static bool On(RoomFightValues v) => v != null && v.Doors;

        /// <summary>Does a room's fight wake only by the line or a hurt (the doors on, and <see cref="RoomFightValues.WakeByLineOrHurtOnly"/>)?</summary>
        public static bool WakesOnlyByLineOrHurt(RoomFightValues v) => On(v) && v.WakeByLineOrHurtOnly;

        /// <summary>The doors of a room (each door that joins it), into <paramref name="into"/>, cleared first, in the level's order.</summary>
        public static void DoorsOf(LevelLayout level, LevelRoom room, List<LevelDoor> into)
        {
            into.Clear();
            if (level == null || room == null) return;
            List<LevelDoor> doors = level.Doors;
            for (int i = 0; i < doors.Count; i++)
                if (doors[i].Joins(room.Order)) into.Add(doors[i]);
        }

        /// <summary>Metres from a ground point to the nearest point of the nearest of <paramref name="doors"/>' openings; infinity with none.</summary>
        public static double NearestDoor(LevelLayout level, IReadOnlyList<LevelDoor> doors, LevelPoint p)
        {
            double best = double.PositiveInfinity;
            if (level == null || doors == null) return best;
            for (int i = 0; i < doors.Count; i++) best = Math.Min(best, LevelGeometry.DistanceToDoor(p, doors[i], level.CellSize));
            return best;
        }

        /// <summary>
        /// Is a ground point inside the room for its doors: on the room's own floor (<see cref="LevelLayout.RoomAt"/>), and at
        /// least <paramref name="clear"/> from every one of <paramref name="doors"/>' openings? For her and for each enemy alike.
        /// </summary>
        public static bool IsInside(LevelLayout level, LevelRoom room, IReadOnlyList<LevelDoor> doors, LevelPoint p, double clear) =>
            level != null && room != null && level.RoomAt(p.X, p.Y) == room && NearestDoor(level, doors, p) >= clear - Slack;

        /// <summary>
        /// What a room's open doors do now: stay open with the switch off, no fight on, she dead or not inside; close when every
        /// living enemy of the fight is inside too (<paramref name="enemiesNotInside"/> 0); else wait, and pull those in once they
        /// have waited <paramref name="pullAfter"/> (<paramref name="waited"/>: seconds since she was inside with the fight on).
        /// </summary>
        public static DoorStep WhenOpen(bool on, bool fightOn, bool herAlive, bool herInside, int enemiesNotInside, float waited, float pullAfter)
        {
            if (!on || !fightOn || !herAlive || !herInside) return DoorStep.StayOpen;
            if (enemiesNotInside <= 0) return DoorStep.Close;
            return waited >= pullAfter - 1e-6f ? DoorStep.PullIn : DoorStep.Wait;
        }

        /// <summary>
        /// Do a room's closed doors open now: its fight over (cleared, or her death ended it), she dead, or she not on its floor?
        /// Null when they stay shut; else why they opened.
        /// </summary>
        public static DoorOpened? OpensNow(bool fightOn, int alive, bool herAlive, bool herOnTheRoomsFloor)
        {
            if (!herAlive) return DoorOpened.HerDeath;
            if (alive <= 0) return DoorOpened.Cleared;
            if (!fightOn) return DoorOpened.Cleared; // a fight that ended otherwise (none does while she is shut in): as cleared
            if (!herOnTheRoomsFloor) return DoorOpened.SheLeft;
            return null;
        }

        /// <summary>
        /// A door's block: across its whole opening and half a wall's thickness into the wall at each end (the wall boxes stop a
        /// half thickness short of the opening's ends), the wall's thickness, and the wall's height on that line: full on a
        /// vertical line, the near wall's on a horizontal one (every wall on a horizontal line between two rooms is a near wall,
        /// so the camera sees over it: <c>GeneratedLevel.BuildWalls</c>).
        /// </summary>
        public static DoorBlock Block(LevelLayout level, LevelDoor door, double wallHeight, double nearWallHeight, double thickness)
        {
            double cs = level.CellSize;
            door.Opening(cs, out LevelPoint a, out LevelPoint b);
            return new DoorBlock
            {
                Centre = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5),
                Vertical = door.Vertical,
                Length = door.Width * cs + thickness,
                Thickness = thickness,
                Height = door.Vertical ? wallHeight : nearWallHeight,
            };
        }

        /// <summary>
        /// How far the block stands out of the floor, 0 (sunk, unseen) to 1 (up): rising over <paramref name="riseSeconds"/>
        /// from the closing, sinking over <paramref name="sinkSeconds"/> from the opening. <paramref name="since"/> is the seconds
        /// since the last of the two; a time of 0 or less is at once.
        /// </summary>
        public static float Raised(bool closed, float since, float riseSeconds, float sinkSeconds)
        {
            float seconds = closed ? riseSeconds : sinkSeconds;
            float k = seconds > 0f ? Math.Max(0f, Math.Min(1f, since / seconds)) : 1f;
            return closed ? k : 1f - k;
        }

        /// <summary>
        /// Where an enemy is pulled in through <paramref name="door"/>: straight in from the door's middle, <paramref name="depth"/>
        /// into the room, moved along the door by <paramref name="along"/> (several pulled through one door stand apart).
        /// </summary>
        public static LevelPoint PullInPlace(LevelLayout level, LevelRoom room, LevelDoor door, double depth, double along)
        {
            door.Opening(level.CellSize, out LevelPoint a, out LevelPoint b);
            double mx = (a.X + b.X) * 0.5, my = (a.Y + b.Y) * 0.5;
            switch (LevelGeometry.WallOf(room.Rect, door))
            {
                case LevelGenerator.South: return new LevelPoint(mx + along, my + depth);
                case LevelGenerator.North: return new LevelPoint(mx + along, my - depth);
                case LevelGenerator.West: return new LevelPoint(mx + depth, my + along);
                case LevelGenerator.East: return new LevelPoint(mx - depth, my + along);
                default: return new LevelPoint(mx, my);
            }
        }

        /// <summary>The door of <paramref name="doors"/> nearest a ground point; null with none.</summary>
        public static LevelDoor NearestOf(LevelLayout level, IReadOnlyList<LevelDoor> doors, LevelPoint p)
        {
            LevelDoor best = null;
            double bestDistance = double.PositiveInfinity;
            if (level == null || doors == null) return null;
            for (int i = 0; i < doors.Count; i++)
            {
                double d = LevelGeometry.DistanceToDoor(p, doors[i], level.CellSize);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = doors[i];
                }
            }
            return best;
        }

        /// <summary>
        /// The second review of work order 086: after a pull, the doors close only when no living enemy of the fight is left outside
        /// (<paramref name="stillNotInside"/>, counted again); else they stay open, and the pull is tried again
        /// <see cref="RoomFightValues.DoorPullRetry"/> later. Never close on a pull that failed.
        /// </summary>
        public static DoorStep AfterPull(int stillNotInside) => stillNotInside <= 0 ? DoorStep.Close : DoorStep.Wait;

        /// <summary>Is a pull due: its first, or its retry time come?</summary>
        public static bool PullDue(float now, float nextPullAt) => now >= nextPullAt - 1e-6f;

        /// <summary>
        /// Shut doors, and an enemy of the fight outside them that could not be put back in: they open once it has been so for
        /// <paramref name="most"/> (<paramref name="outsideFor"/> seconds now), rather than hold her shut in with no way to it.
        /// </summary>
        public static bool OpensForOneOutside(int stillNotInside, float outsideFor, float most) => stillNotInside > 0 && outsideFor >= most - 1e-6f;

        /// <summary>
        /// The third review of work order 086: is a place clear to put an enemy at, at least <paramref name="clearOfHer"/> from her and
        /// at least <paramref name="clearOfOthers"/> from each of <paramref name="others"/> (the other living enemies), on the ground?
        /// Allocates nothing.
        /// </summary>
        public static bool PlaceClear(LevelPoint p, LevelPoint her, IReadOnlyList<LevelPoint> others, double clearOfHer, double clearOfOthers)
        {
            if (p.DistanceTo(her) < clearOfHer - Slack) return false;
            if (others != null)
                for (int i = 0; i < others.Count; i++)
                    if (p.DistanceTo(others[i]) < clearOfOthers - Slack) return false;
            return true;
        }

        /// <summary>How many points <see cref="RingPoint"/> gives: three rings round the room's middle, eight points each.</summary>
        public const int RingPoints = 24;

        /// <summary>
        /// The <paramref name="k"/>-th point of the ring round <paramref name="middle"/> tried after the five places (the third review):
        /// rings of 2, 4 and 6 m, eight points each, the nearest ring first, from north round to the east.
        /// </summary>
        public static LevelPoint RingPoint(LevelPoint middle, int k)
        {
            double radius = 2.0 * (1 + k / 8);
            double a = (k % 8) * Math.PI / 4.0;
            return new LevelPoint(middle.X + Math.Sin(a) * radius, middle.Y + Math.Cos(a) * radius);
        }

        /// <summary>The recorder's word for why the doors opened: "cleared", "her death", "she left", "an enemy outside".</summary>
        public static string Word(DoorOpened why) =>
            why == DoorOpened.Cleared ? "cleared" : why == DoorOpened.HerDeath ? "her death" : why == DoorOpened.SheLeft ? "she left" : "an enemy outside";
    }
}
