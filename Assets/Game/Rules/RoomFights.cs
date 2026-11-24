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

        // ---------------- The door closes behind her (work order 086) ----------------
        // Kristian's answer of 2026-11-22 (answers file, row 4): "Døra stenges bak deg, men vi bør balansere rommene med flere
        // muligheter og, slik at man kan bruke miljøet til sin fordel". The rules are in RoomDoorRules (RoomDoors.cs). First
        // guesses; he tunes them by playing. Off (Doors false): every door stays open and every enemy wakes as on main 9a79acf.

        /// <summary>
        /// The switch: while a room's fight is on and she is inside it, every door of the room is shut by a block until the fight
        /// ends (the room cleared, or she dies). Off: as on main, field for field (no door is ever made, and the wake by range below
        /// is not changed).
        /// </summary>
        public bool Doors = true;
        /// <summary>
        /// With <see cref="Doors"/> on: an enemy of a room's fight that has not woken wakes only by her crossing the room's line or
        /// by being hurt (or provoked, as the benchmark does), not by her coming within its own range (its aggro range, 9 m for the
        /// melee kinds, 14 m for the ranged, reached through the open door from the passage, since enemies stand at least 8 m from
        /// the door in). The producer's proposal 4 of metrics.md, which his answer of row 4 reads as: "the room must still wake at
        /// the line, not from outside". Off: they wake by range as before.
        /// </summary>
        public bool WakeByLineOrHurtOnly = true;
        /// <summary>
        /// m from every door's opening of the room (her middle, or an enemy's, to the nearest point of the opening) that counts as
        /// "inside": the doors close only while she is on the room's own floor at least this far from each of its doors, and every
        /// living enemy of the fight is too. 1.5 m: the door's block is 0.5 m thick (the wall's), half of it 0.25 m on her side, and
        /// her body and the NavMesh's agent radius are under 0.6 m: so she is never in the block's footprint, and the line, 5 m in,
        /// is well past it. The same as the level's clearance of every enemy point from a door (<see cref="LevelValues.WallClearance"/>).
        /// </summary>
        public float DoorClear = 1.5f;
        /// <summary>
        /// s the doors wait, she inside and the fight on, for enemies of the fight that are not inside (in the passage, or in a
        /// doorway) before those are pulled in: each is put at once 1 m past <see cref="DoorClear"/> inside the room, in from the
        /// door nearest it, and the doors close. So she is never shut in with one of them left on the other side.
        /// </summary>
        public float DoorPullAfter = 2f;
        /// <summary>s the block takes to rise out of the floor into the doorway when the doors close. It stops her, the enemies and the shots from the first frame.</summary>
        public float DoorRiseSeconds = 0.35f;
        /// <summary>s the block takes to sink back into the floor when the doors open (the room cleared): slower than it rose, so the way on is seen opening.</summary>
        public float DoorSinkSeconds = 0.8f;
        /// <summary>
        /// s between two tries to pull in an enemy of the fight that could not be put inside (its agent off, no NavMesh where it was
        /// to go): the doors stay OPEN meanwhile, and close only once every one is inside (the second review of work order 086).
        /// </summary>
        public float DoorPullRetry = 0.5f;
        /// <summary>s between two looks, while the doors are shut, for an enemy of the fight that is not inside (put out by anything): it is put back in.</summary>
        public float DoorCheckEvery = 0.25f;
        /// <summary>
        /// s an enemy of the fight may be found outside shut doors, and not be put back in, before the doors open rather than hold
        /// her shut in with no way to it.
        /// </summary>
        public float DoorOutsideMost = 1f;
        /// <summary>
        /// m on the ground from her that a place an enemy is put inside at must be (the third review of work order 086): never on
        /// her. 3 m: the room's waking gives her the same, the nearest enemy at least 3 m from her at the line (8 m from the door
        /// less the line's 5), which a swarmer covers in about 0.6 s; nearer, a melee enemy would strike as it lands.
        /// </summary>
        public float DoorPutClearOfHer = 3f;
        /// <summary>m from every other living enemy that such a place must be: two never stand on one point (a body is about 0.5 m round).</summary>
        public float DoorPutClearOfOthers = 1f;
        /// <summary>The placeholder the doors closing plays: the heavy low impact (an enemy's strike landing, the duck's stop). Opening is heard by the room's own "way opens" (or the floor's cleared) that plays as it is cleared.</summary>
        public PlaceholderSound DoorCloseSound = PlaceholderSound.Strike;
        /// <summary>Times that sound's own volume.</summary>
        public float DoorCloseGain = 0.9f;
        /// <summary>
        /// The block glows in its own colour (the greybox view's door orange) by this share, so it reads in the dark of the cave
        /// and of the lamplit light from across the room; 0 is the plain lit colour. Since work order 100 the greybox view's block
        /// only (and the game's with <see cref="DoorDarkFace"/> off): in the game the orange glows along its edges.
        /// </summary>
        public float DoorGlow = 0.5f;
        /// <summary>
        /// Work order 100 (Kristian, 2026-11-24, asked what the flat glowing orange plate should be: "Mørk stein eller jern, glød i
        /// kantene"; of the door shutting, 2026-11-23: "føles veldig riktig at den lukker seg"): in the game's view the block is a dark
        /// mass that belongs to the wall beside it, lit and shaded by the lamps as the walls are, with the door orange glowing only in
        /// a band along every edge of its faces (the top's too: the camera looks down on it), so that it still reads as shut at a
        /// glance. The greybox view keeps the whole block the flat door orange glowing by <see cref="DoorGlow"/>, as before. Off: the
        /// whole block the door orange in the game too (work order 086's look, kept to compare).
        /// </summary>
        public bool DoorDarkFace = true;
        /// <summary>
        /// The block's body in the game's view, as picked (Simple Lit, no texture, no shine): the wall kit's dark stones. The wall's
        /// painted texture (Crypt_Wall_A_base_color, drawn white) has its darkest tenth of stone at about (0.30, 0.24, 0.23) and its
        /// middle at (0.38, 0.29, 0.25); the body sits at the dark end, a little greyer, between stone and iron.
        /// </summary>
        public Rgb DoorBody = new Rgb(0.29f, 0.24f, 0.22f);
        /// <summary>
        /// m: the band of door orange along each edge of each face of the block in the game's view (the block is the wall's 0.5 m
        /// thick). The producer's start was about 0.08 m; the pictures of work order 100 (whole nave pulled back to 72 m) showed
        /// 0.08 as lines of one pixel, 0.10 a little firmer at no cost near: 0.10.
        /// </summary>
        public float DoorEdgeWidth = 0.10f;
        /// <summary>
        /// The band glows in the door orange by this share of its colour (as <see cref="DoorGlow"/> does for the whole block in the
        /// greybox view). The pictures: 1.0 keeps the line the door orange (on screen about (254, 211, 91)); 1.2 and above tip it
        /// toward yellow-white (1.6: (254, 254, 113)), 0.8 is dimmer for the far view.
        /// </summary>
        public float DoorEdgeGlow = 1.0f;
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
        /// Work order 086 (<see cref="RoomFightValues.WakeByLineOrHurtOnly"/>, with the doors on): set by the floor. True, its
        /// enemies do not wake by their own range before the fight is on: only her crossing the line, a hurt or a provoke wakes it.
        /// </summary>
        public bool WakesOnlyByLineOrHurt { get; set; }

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
