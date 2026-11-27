using System;
using System.Collections.Generic;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the caller (work order 137; Kristian's tick of 2026-11-25 to "the caller (a caster's body in another tint, far in,
    /// calling three swarmers every 6 s until it dies, its called ones falling with it)": "Ja, fra etasje 2"; his words of that day on every
    /// room, "skyter de små først, så cahrgerne, og så ranged": the caller is the one far enemy she must go to first). Research R3's A4 and
    /// its table "Mix by floor". First guesses, his to tune by playing. Held by <see cref="LevelValues.CallerRules"/>, read only with
    /// <see cref="LevelValues.Callers"/> on.
    /// </summary>
    [Serializable]
    public sealed class CallerValues
    {
        // ---- Where it stands (his tick "fra etasje 2"; the order: at most one a room, never with a warder, never the first combat room)
        /// <summary>The first floor a caller stands on (his tick: "fra etasje 2").</summary>
        public int FromFloor = 2;
        /// <summary>On floor 2, the first combat room (counted from 1 along the row) that may hold one: the third (research R3's table, its Caller column).</summary>
        public int Floor2FromCombatRoom = 3;
        /// <summary>
        /// Callers a floor, by its number (index 0 unused, 1 to 5): none on floor 1, one on floors 2, 3 and 4, two on floor 5 (research R3's
        /// table: "at most one a floor", "at most two a floor"). The order leaves open whether a floor always has its most: the smallest
        /// reading of "at most" that still lets him meet it, so each floor holds as many as it may where rooms fit. A floor past the table reads its last.
        /// </summary>
        public int[] OnFloor = { 0, 0, 1, 1, 1, 2 };
        /// <summary>
        /// The share of the room's depth from the door in (its farthest place, as the generator measures its bands) a caller stands at least:
        /// the far band (research R3's A4: "0.6 to 1.0 of the depth"; the shooters' <see cref="LevelValues.FarBand"/>).
        /// </summary>
        public double FarShare = 0.6;
        /// <summary>
        /// Where no point of the far band is in view from the line (a deep room: the camera shows about 11 m ahead of her), the deepest
        /// points that are: those within this share of the room's depth of the deepest point in view (the producer's reading of the order's
        /// "far in, but in view": the view wins). 0.1: a band about 2 m deep in a 20 m room.
        /// </summary>
        public double InViewBand = 0.1;
        /// <summary>The share of the room's depth such a deepest point in view must reach at least, or the room holds no caller: 0.4, past its middle band's start.</summary>
        public double InViewShareLeast = 0.4;
        /// <summary>
        /// m inside the edge of the default camera's view from the middle of the fight's line it stands at least (the order: "in view of the
        /// camera at the room's line", so he sees it as he enters): a body 1.9 m tall seen at the camera's 55 degrees reaches about 1.3 m
        /// up the screen past its feet, so 1.5 m keeps its head on screen at the far edge. The ranged rule's own margin is 0.5 m (its feet).
        /// </summary>
        public double ViewMargin = 1.5;
        /// <summary>m from every enemy the floor placed in its room it stands at least (the waves' <see cref="RoomWaveValues.Apart"/>: two bodies apart).</summary>
        public double Apart = 2.0;

        // ---- What it costs the room (the order: "the room is not harder by a whole enemy: its line-up's partner count gives way")
        /// <summary>
        /// Its weight in swarmers, the line-ups' measure (<see cref="LineUpValues.WeightOf(EnemyKind)"/>): a caster's (<see cref="LineUpValues.CasterWeight"/>,
        /// 6.1), whose body and health it has. As many of the room's partner kind give way as come nearest this weight: one brute (6.3), one
        /// shooter (4.2), six swarmers, the bomber none (one only, and the line-up keeps its partner).
        /// </summary>
        public float Weight = 6.1f;
        /// <summary>The partners a room keeps at least, so its line-up keeps its two kinds (the smallest choice the order leaves open).</summary>
        public int PartnersKept = 1;

        // ---- Its call (his tick: "three swarmers every 6 s until it dies, its called ones falling with it")
        /// <summary>s after the room wakes that its first call begins (research R3's A4: "the first 3 s after the wake").</summary>
        public float FirstCallAfter = 3f;
        /// <summary>s from the start of one call to the start of the next (his tick's 6 s); a call cut short or brought by none counts as made.</summary>
        public float CallEvery = 6f;
        /// <summary>Swarmers a call brings (his tick's three).</summary>
        public int CallCount = 3;
        /// <summary>Called swarmers alive at once at most (the order's six): a call that would pass it brings fewer, or none and no tell.</summary>
        public int MostAlive = 6;
        /// <summary>s of the tell on the caller (the caster's big cast and the wind-up's ring round it) while its discs stand on the ground; then the swarmers stand there (the order's 1 s).</summary>
        public float TellSeconds = 1f;
        /// <summary>s it stands after a call before it may begin the next (its recovery; the next is still <see cref="CallEvery"/> after the last began).</summary>
        public float RestSeconds = 1f;
        /// <summary>m: the radius of the ring of its tell round its body (the game's wind-up outline, blue as every counterable wind-up).</summary>
        public float RingRadius = 1.2f;
        /// <summary>
        /// Its call can be cut as a caster's big cast can today (the order: "keep it consistent"): the caster's Grave Rain and its fan of
        /// orbs, in the same pose (CastBig), are counterable, Stopping Shot from its front drops them and dazes it. True: so is the call,
        /// its ring drawn blue. A daze from anything drops it as well, as every wind-up.
        /// </summary>
        public bool Counterable = true;

        /// <summary>
        /// Its tint over the caster's model it wears (the order: "a named colour clearly apart from the caster's and the warder's"): a hot
        /// magenta. The caster's model is untinted (bone, a dark robe, cyan glow), the warder's green (0.25, 0.75, 0.45); far too from the
        /// lancer's blood red (0.72, 0.18, 0.2), the bomber's ember, the elite's gold, the daze's pale blue and the waves' pale violet discs.
        /// </summary>
        public Rgb Tint = new Rgb(1f, 0.3f, 0.85f);

        /// <summary>The name of its one attack, the call (the recorder's attack word; its model plays the caster's CastBig for it).</summary>
        public const string Call = "Call";

        /// <summary>Its call as an attack of the brain: no damage, a ring round it for its wind-up (<see cref="TellSeconds"/>), then its rest.</summary>
        public EnemyAttackDefinition CallAttack() => new EnemyAttackDefinition
        {
            Name = Call, Shape = EnemyAttackShape.Circle, MinDistance = 0f, MaxDistance = 1000f,
            Windup = Math.Max(0.05f, TellSeconds), Recovery = Math.Max(0f, RestSeconds), Damage = 0, Radius = RingRadius, Counterable = Counterable,
        };
    }

    /// <summary>
    /// The caller (work order 137): which rooms of a floor hold one, where it stands, what of the room gives way for it, and how many a
    /// call brings. A rule of the fight read as the floor's fights are set up (from each room's place on the floor and its line-up), with a
    /// seeded stream of its own (<see cref="Stage"/>), not of the generator: the floor and its pinned fingerprints are as work order 135
    /// lays them (version 25). No engine.
    ///
    /// - A room may hold one (<see cref="RoomFault"/>): from floor <see cref="CallerValues.FromFloor"/>, a combat room that is not the
    ///   elite room (never the exit room nor the throne room), filled from a line-up of two kinds (its partner gives way), with no warder,
    ///   not the floor's first combat room and, on floor 2, not before its third; and a place for it (<see cref="Places"/>).
    /// - How many a floor: <see cref="CallerValues.OnFloor"/>, drawn among the rooms that may.
    /// - Where: a point of the room's open floor in its far band (<see cref="CallerValues.FarShare"/> of its depth from the door in), in the
    ///   default camera's view from the middle of the fight's line (<see cref="CallerValues.ViewMargin"/> inside), apart from the room's
    ///   enemies; one drawn.
    /// - What gives way: <see cref="GiveWay"/> of the room's partners.
    /// - A call: <see cref="CallSize"/>.
    /// </summary>
    public static class RoomCallerRules
    {
        /// <summary>The stream of its draws (on the floor's seed; nothing of the generator reads it).</summary>
        public const string Stage = "callers";

        /// <summary>Its switch on, with its numbers.</summary>
        public static bool On(LevelValues v) => v != null && v.Callers && v.CallerRules != null;

        /// <summary>Callers on a floor of this number (<see cref="CallerValues.OnFloor"/>; 0 before <see cref="CallerValues.FromFloor"/>).</summary>
        public static int OnFloor(int floor, CallerValues c)
        {
            if (c == null || c.OnFloor == null || c.OnFloor.Length == 0 || floor < c.FromFloor) return 0;
            return Math.Max(0, c.OnFloor[Math.Max(0, Math.Min(c.OnFloor.Length - 1, floor))]);
        }

        /// <summary>The floor's combat rooms along its row, in the order she meets them.</summary>
        public static List<LevelRoom> CombatRooms(LevelLayout level) =>
            level == null ? new List<LevelRoom>() : LevelGeometry.MainPath(level).Where(r => r.Kind == RoomKind.Combat).ToList();

        /// <summary>The room's partners, as the floor placed them (not the elite).</summary>
        public static int Partners(LevelRoom room) =>
            room?.LineUp == null ? 0 : room.EnemyGroups.Where(g => g != null && !g.Elite && !g.Boss && g.Kind == room.LineUp.Partner).Sum(g => g.Count);

        /// <summary>The first reason this room may not hold a caller, in words, or null (its place asked apart: <see cref="Places"/>).</summary>
        public static string RoomFault(LevelLayout level, LevelRoom room, CallerValues c)
        {
            if (level == null || room == null || c == null) return "has no floor";
            if (level.Floor < c.FromFloor) return "is on floor " + level.Floor + ", before floor " + c.FromFloor;
            if (room.Kind != RoomKind.Combat) return "is not a combat room";
            if (room.Elite) return "is the elite room";
            if (room.LineUp == null || room.LineUp.Full) return "is not filled from a line-up of two kinds";
            if (room.EnemyGroups.Any(g => g != null && g.Kind == EnemyKind.Warder)) return "holds a warder";
            if (room.EnemyGroups.Any(g => g != null && g.Boss)) return "holds the act boss";
            List<LevelRoom> combat = CombatRooms(level);
            int at = combat.IndexOf(room);
            if (at < 0) return "is not on the floor's row";
            if (at == 0) return "is the floor's first combat room";
            if (level.Floor == 2 && at + 1 < c.Floor2FromCombatRoom) return "is floor 2's combat room " + (at + 1) + ", before its " + c.Floor2FromCombatRoom;
            if (Partners(room) <= 0) return "has no partner to give way";
            return null;
        }

        /// <summary>
        /// The points of the room a caller may stand at (<see cref="RoomWaveRules.RoomPoints"/>, the floor's place rules): in its far band
        /// from the door in, in the default camera's view from the middle of the fight's line, and apart from every enemy the floor placed in
        /// it; where none of the far band is in view, the deepest in view (<see cref="CallerValues.InViewBand"/>, from
        /// <see cref="CallerValues.InViewShareLeast"/>). Empty for a room with no door in.
        /// </summary>
        public static List<LevelPoint> Places(LevelLayout level, LevelRoom room, LevelValues v)
        {
            var kept = new List<LevelPoint>();
            if (level == null || room == null || v == null || v.CallerRules == null) return kept;
            CallerValues c = v.CallerRules;
            LevelDoor entry = LevelGeometry.EntryDoor(level, room);
            if (entry == null) return kept;
            RangedAroundValues r = v.RangedAround ?? new RangedAroundValues();
            LevelPoint line = LevelRanged.LineMiddle(level, room, entry, v);
            double depth = Math.Max(1e-9, LevelLancers.Depth(level, room, entry, v));
            var others = new List<LevelPoint>();
            for (int i = 0; i < room.EnemyPoints.Count; i++) others.AddRange(room.PlacesAt(i));
            var seen = new List<(LevelPoint p, double share)>();
            foreach (LevelPoint p in RoomWaveRules.RoomPoints(level, room, v, v.PointStep))
            {
                if (!LevelRanged.InDesignView(line, p, r, c.ViewMargin)) continue;
                if (others.Any(o => o.DistanceTo(p) < c.Apart - 1e-9)) continue;
                seen.Add((p, LevelGeometry.DistanceToDoor(p, entry, level.CellSize) / depth));
            }
            foreach ((LevelPoint p, double share) in seen) if (share >= c.FarShare - 1e-9) kept.Add(p);
            if (kept.Count > 0 || seen.Count == 0) return kept;
            double deepest = seen.Max(x => x.share);
            if (deepest < c.InViewShareLeast - 1e-9) return kept;
            foreach ((LevelPoint p, double share) in seen) if (share >= deepest - c.InViewBand - 1e-9) kept.Add(p);
            return kept;
        }

        /// <summary>
        /// The rooms of the floor that hold a caller, in the order of the rooms: <see cref="OnFloor"/> of them, drawn evenly from the stream
        /// <see cref="Stage"/> among those that may (<see cref="RoomFault"/> none, and a place). Empty with the switch off.
        /// </summary>
        public static List<LevelRoom> Rooms(LevelLayout level, LevelValues v)
        {
            var chosen = new List<LevelRoom>();
            if (level == null || !On(v)) return chosen;
            int n = OnFloor(level.Floor, v.CallerRules);
            if (n <= 0) return chosen;
            List<LevelRoom> may = CombatRooms(level).Where(r => RoomFault(level, r, v.CallerRules) == null && Places(level, r, v).Count > 0).ToList();
            LevelRandom rng = LevelRandom.Stream(level.Seed, Stage, level.Floor);
            rng.Shuffle(may);
            chosen.AddRange(may.Take(n));
            chosen.Sort((a, b) => a.Order.CompareTo(b.Order));
            return chosen;
        }

        /// <summary>The places of <see cref="Places"/> in the order the room's own draw tries them (the stream <see cref="Stage"/>, by the room's order): the first that the floor's NavMesh holds is its place.</summary>
        public static List<LevelPoint> PlacesInOrder(LevelLayout level, LevelRoom room, LevelValues v)
        {
            List<LevelPoint> places = Places(level, room, v);
            if (level != null && room != null) LevelRandom.Stream(level.Seed, Stage, 1000 + room.Order).Shuffle(places);
            return places;
        }

        /// <summary>
        /// How many of the room's <paramref name="partners"/> (of kind <paramref name="partner"/>) give way for its caller: as many as come
        /// nearest its weight (<see cref="CallerValues.Weight"/> over the partner's), never so many that fewer than
        /// <see cref="CallerValues.PartnersKept"/> are left.
        /// </summary>
        public static int GiveWay(int partners, EnemyKind partner, CallerValues c, LineUpValues w)
        {
            if (c == null || w == null || partners <= 0) return 0;
            float each = w.WeightOf(partner);
            if (each <= 0f) return 0;
            int n = (int)Math.Round(Math.Max(0f, c.Weight) / each, MidpointRounding.AwayFromZero);
            return Math.Max(0, Math.Min(n, partners - Math.Max(0, c.PartnersKept)));
        }

        /// <summary>How many a call brings with <paramref name="alive"/> of its called alive: <see cref="CallerValues.CallCount"/>, fewer where it would pass <see cref="CallerValues.MostAlive"/>, 0 at it.</summary>
        public static int CallSize(int alive, CallerValues c) =>
            c == null ? 0 : Math.Max(0, Math.Min(Math.Max(0, c.CallCount), Math.Max(0, c.MostAlive) - Math.Max(0, alive)));
    }
}
