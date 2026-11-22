using System;
using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>How a room's fight started (work order 070): what the recorder writes and the summary counts.</summary>
    public enum RoomFightStart
    {
        None,
        /// <summary>She crossed the room's point, a little way in from the door she came by.</summary>
        Point,
        /// <summary>One of its enemies noticed her by its own range (<see cref="EnemyValues.AggroRange"/>) before she was at the point.</summary>
        Seen,
        /// <summary>One of its enemies was hurt (a shot from the passage, through the door, a burst).</summary>
        Hit,
        /// <summary>One of its enemies was woken by something else as a hurt would wake it (<c>Enemy.Provoke</c>: the benchmark's staging).</summary>
        Provoked,
    }

    /// <summary>
    /// The numbers of a room's fight (work order 070, 2026-11-21, Kristian after his evening session: "jeg vil også at nå rman
    /// kommer i et rom, så er man i en combat. det skal ikke være sånn at man skal lete og agrroe folk på avstand, da blir det
    /// altofr enkelt å bare agroe en og en. så man trenger et punkt in i rommet der alle flyr på. så får vi balansere deretter
    /// senere"). First guesses of the work order; he tunes them by playing. Held by <see cref="LevelValues.Fights"/>.
    /// </summary>
    [Serializable]
    public sealed class RoomFightValues
    {
        /// <summary>m inside the room's own floor, measured straight in from the wall of the door she came by, where the room wakes (the order's first guess).</summary>
        public float PointIn = 5f;
        /// <summary>
        /// ...but never further in than this share of the room's depth from that wall (the order: "never further in than a
        /// third of the room's depth, so that in a small room it is still inside the door and in a large one she is well in").
        /// The rooms that hold enemies are 20 to 36 m deep from their door in (floors 1 to 5, a thousand seeds each: the
        /// measurement in RoomFightsTests), so on today's floors the point is always at PointIn.
        /// </summary>
        public float PointMostShare = 1f / 3f;
        /// <summary>
        /// The room turning on her is heard (the order: "one small shared cue from what the game already has"): one play of
        /// <see cref="CueSound"/> as the room wakes, whatever woke it. Off: no sound; the enemies still turn to her at once.
        /// </summary>
        public bool Cue = true;
        /// <summary>
        /// The placeholder the room's waking plays: the boss's growl (a low rough tone and noise wobbling together), the one
        /// sound the game has of an enemy noticing her and turning on her. No new sound: it is synthesised as the boss's is.
        /// </summary>
        public PlaceholderSound CueSound = PlaceholderSound.BossAnger;
        /// <summary>Times the growl's own volume (0.5 of the basic shot's): quieter than the boss's own, as it comes in every room.</summary>
        public float CueGain = 0.6f;
    }

    /// <summary>
    /// A room is a fight (work order 070): the rules of where a room's point lies and when a room's fight ends. No engine.
    ///
    /// The rule in three sentences. Every room that holds enemies has a point, a line a little way in from the door she came
    /// by: when she crosses it, every enemy of the room wakes at once and comes at her, with no range and no sight asked.
    /// Before that, any waking of one of them (it notices her by its own range, it is hurt, something provokes it) wakes the
    /// whole room the same way, so there is no way to wake fewer than all. Once woken the room stays one fight, every enemy
    /// held awake past its own leash, until all of them are dead, she is dead, or she is outside the room and farther than
    /// its own leash from every one still alive: then all of them go home together, and the next waking is all of them again.
    /// </summary>
    public static class RoomFightRules
    {
        /// <summary>
        /// The metres from the wall of <paramref name="door"/> to the room's opposite side: its depth seen from that door. The
        /// room's longer side for a door on none of its walls (a hand-made layout).
        /// </summary>
        public static double Depth(LevelLayout level, LevelRoom room, LevelDoor door)
        {
            double cs = level.CellSize;
            int wall = door != null ? LevelGeometry.WallOf(room.Rect, door) : -1;
            if (wall == LevelGenerator.North || wall == LevelGenerator.South) return room.Rect.Height * cs;
            if (wall == LevelGenerator.East || wall == LevelGenerator.West) return room.Rect.Width * cs;
            return Math.Max(room.Rect.Width, room.Rect.Height) * cs;
        }

        /// <summary>
        /// How far in from the wall of <paramref name="door"/> the room's point lies: <see cref="RoomFightValues.PointIn"/>,
        /// but no more than <see cref="RoomFightValues.PointMostShare"/> of the room's depth from that wall. 0 at least.
        /// </summary>
        public static double PointDepth(LevelLayout level, LevelRoom room, LevelDoor door, RoomFightValues values)
        {
            double most = Depth(level, room, door) * Math.Max(0f, values.PointMostShare);
            return Math.Max(0d, Math.Min(values.PointIn, most));
        }

        /// <summary>
        /// Metres a ground point stands in from the wall of <paramref name="door"/>, straight across it (negative on the far
        /// side of the wall line); the distance to the door's opening for a door on none of the room's walls.
        /// </summary>
        public static double DepthIn(LevelLayout level, LevelRoom room, LevelDoor door, LevelPoint p)
        {
            double cs = level.CellSize;
            CellRect r = room.Rect;
            switch (LevelGeometry.WallOf(r, door))
            {
                case LevelGenerator.South: return p.Y - r.Y * cs;
                case LevelGenerator.North: return r.Top * cs - p.Y;
                case LevelGenerator.West: return p.X - r.X * cs;
                case LevelGenerator.East: return r.Right * cs - p.X;
                default: return LevelGeometry.DistanceToDoor(p, door, cs);
            }
        }

        /// <summary>
        /// Is she, standing at <paramref name="her"/> in <paramref name="room"/>, at or past its point measured from
        /// <paramref name="door"/>, the door she came in by? Without a door (a room with none) anywhere in the room is past it.
        /// False when she is not on the room's own floor.
        /// </summary>
        public static bool PastThePoint(LevelLayout level, LevelRoom room, LevelDoor door, LevelPoint her, RoomFightValues values)
        {
            if (level == null || room == null || values == null || level.RoomAt(her.X, her.Y) != room) return false;
            if (door == null) return true;
            return DepthIn(level, room, door, her) >= PointDepth(level, room, door, values) - 1e-9;
        }

        /// <summary>
        /// The door of the room she came in by, standing at <paramref name="her"/> as she has just come in: the room's door
        /// nearest her; null for a room with none. Usually its door in; from the room after it, its door out.
        /// </summary>
        public static LevelDoor DoorSheCameBy(LevelLayout level, LevelRoom room, LevelPoint her)
        {
            if (level == null || room == null) return null;
            LevelDoor best = null;
            double bestDistance = double.PositiveInfinity;
            List<LevelDoor> doors = level.Doors;
            for (int i = 0; i < doors.Count; i++) // every frame she changes room: no LINQ, nothing made
            {
                LevelDoor d = doors[i];
                if (!d.Joins(room.Order)) continue;
                double distance = LevelGeometry.DistanceToDoor(her, d, level.CellSize);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = d;
                }
            }
            return best;
        }

        /// <summary>
        /// Does a room of this floor hold a fight of its own (work order 070)? Every room that is not a passage and that holds
        /// enemies (ordinary combat rooms, the elite room, the exit room and the boss's), on a floor in a line (work order 041
        /// and later); an old floor laid out before the line (version 9 and older pins) has none and wakes as before.
        /// </summary>
        public static bool HoldsAFight(LevelLayout level, LevelRoom room, int enemies) =>
            level != null && room != null && level.InALine && room.Kind != RoomKind.Passage && enemies > 0;

        /// <summary>
        /// Does a room's fight end now? When she is dead, when none of its enemies is alive (cleared), or when she is outside
        /// the room and no living enemy of it is within its own leash (<see cref="EnemyValues.LeashRange"/>) of her.
        /// </summary>
        public static bool Ends(bool herAlive, int alive, bool herInRoom, bool anyWithinLeash) =>
            !herAlive || alive <= 0 || (!herInRoom && !anyWithinLeash);

        /// <summary>The word the recorder writes for how a fight started: "point", "seen", "hit", "provoked".</summary>
        public static string Word(RoomFightStart how) =>
            how == RoomFightStart.Point ? "point" : how == RoomFightStart.Seen ? "seen" : how == RoomFightStart.Hit ? "hit"
            : how == RoomFightStart.Provoked ? "provoked" : "none";
    }

    /// <summary>
    /// The fight of one room of a floor (work order 070): asleep or on. Any waking of one of its enemies (<see cref="Stir"/>)
    /// puts it on, which holds every enemy of the room awake; the floor ends it (<see cref="End"/>) by
    /// <see cref="RoomFightRules.Ends"/>, and every enemy of the room then goes home. Woken again, it is all of them again.
    /// </summary>
    public sealed class RoomFight
    {
        bool _unseen;

        /// <summary>The room's place in the run (<see cref="LevelRoom.Order"/>).</summary>
        public readonly int Room;

        public RoomFight(int room) => Room = room;

        /// <summary>True from a waking until the floor ends it: every enemy of the room is held awake.</summary>
        public bool On { get; private set; }
        /// <summary>How the last start came about; None before the first.</summary>
        public RoomFightStart StartedBy { get; private set; }
        /// <summary>The game time of the last start.</summary>
        public float StartedAt { get; private set; } = float.NegativeInfinity;
        /// <summary>How many times it has started on this floor (once, unless she got away and it woke again).</summary>
        public int Starts { get; private set; }

        /// <summary>
        /// One of its enemies stirs, or she crossed its point: the fight starts, unless it is on already. True when it
        /// started now.
        /// </summary>
        public bool Stir(RoomFightStart how, float now)
        {
            if (On) return false;
            On = true;
            StartedBy = how;
            StartedAt = now;
            Starts++;
            _unseen = true;
            return true;
        }

        /// <summary>True once after each start: for the floor, to sound it and to tell the recorder.</summary>
        public bool TakeStarted()
        {
            if (!_unseen) return false;
            _unseen = false;
            return true;
        }

        /// <summary>The fight is over (cleared, she died, or she got away): its enemies are no longer held.</summary>
        public void End() => On = false;
    }
}
