using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the timed chest (work order 153; Kristian's tick of 2026-11-25, asked which odd room a floor should come first:
    /// "Tidskisten", the option's words "The timed chest: open it to fight, and the reward shrinks while you fight"; his words of that day
    /// on every room: "det er veldig likt hver gang ... vi trenger en mye større variasjon av utfordirnger i rommene"). Research R3's C2,
    /// from Hades' Infernal Trove (its reward starts at 100, begins to fall after 9 s, 1 a second, never under 10). First guesses, his to
    /// tune by playing. Held by <see cref="LevelValues.TimedChestRules"/>, read only with <see cref="LevelValues.TimedChests"/> on.
    /// </summary>
    [Serializable]
    public sealed class TimedChestValues
    {
        // ---- Which room (the order: from floor 2, one a floor, never the first combat room, the elite, exit or throne room, nor a caller's)
        /// <summary>The first floor a timed chest stands on (the order: "from floor 2").</summary>
        public int FromFloor = 2;

        // ---- Where it stands (the order: "at a corner of the room's open floor, on the NavMesh, at least a named distance from the doors
        // and from props, where the camera shows it when she stands in the room's middle")
        /// <summary>
        /// m from every door of its room the chest's middle stands at least: her reach to it (<see cref="RunValues.ChestReach"/>, 2 m)
        /// and the doors' own clearance (<see cref="RoomFightValues.DoorClear"/>, 1.5 m) and half a metre more, so she is inside the room
        /// for its doors wherever she stands to open it, and they can shut.
        /// </summary>
        public double FromDoors = 4.0;
        /// <summary>m from the room's walls (and its carved outline) the chest's middle stands at least: its half width (0.53 m) and a gap she can see.</summary>
        public double FromWalls = 1.0;
        /// <summary>m between the chest's footprint and every prop's of its room at least: room to walk round it.</summary>
        public double FromProps = 1.0;
        /// <summary>m from every place the floor put an enemy of the room at (they stand there until the room wakes): two bodies apart, as the waves' <see cref="RoomWaveValues.Apart"/>.</summary>
        public double FromEnemies = 2.0;
        /// <summary>
        /// m from a corner of the room's rectangle the chest's place is at most. Its place is the open floor's point nearest that corner
        /// that keeps every other rule (the corner of what she can see and walk on); a first measure at 4.5 m left half the floors without
        /// one, the rectangles' corners being carved away or out of the camera's view from the middle in the large rooms; at 8 m one floor 2 in twenty still had none, so 12 m (in practice: the nearest it can be).
        /// </summary>
        public double CornerMost = 12.0;
        /// <summary>m from the room's middle (where she fights) its place stands at least, so it is at the room's edge, never in its middle.</summary>
        public double MiddleLeast = 4.0;
        /// <summary>m between the points tried for its place (a grid over the room's open floor).</summary>
        public double PlaceStep = 0.5;
        /// <summary>
        /// m inside the edge of the default camera's view from the room's middle its place stands at least. 1 m (a chest is low) put it, in
        /// the first pictures, against the top edge of the screen with its number over it cut close; 2 m keeps it and its number inside.
        /// </summary>
        public double ViewMargin = 2.0;
        /// <summary>m: its place must be on the floor's NavMesh within this of where the rule put it (as an arrival's, <see cref="RoomWaveValues.OnNavMeshWithin"/>).</summary>
        public float OnNavMeshWithin = 0.3f;

        // ---- Its wave (the order: "one wave arrives of the room's own two kinds ... a named count by floor, weight about 60 % of the room's own fight")
        /// <summary>
        /// The share of the room's own lead and of its own partner (as the floor placed them) its wave brings, by the floor's number (index 0
        /// unused, 1 to 5; a floor past the table reads its last): 0.6 of each (the order's "about 60 %"), each kind rounded and at least one.
        /// So the count grows with the floor's rooms. Tune toward the order's target (a practised clear leaves 70 to 90 %), not the decay.
        /// </summary>
        public float[] WaveShare = { 0f, 0f, 0.6f, 0.6f, 0.6f, 0.6f };
        /// <summary>s from the opening to its wave's first discs (0: at once; each enemy still waits its 1 s disc, 136's rule).</summary>
        public float FirstDiscsAfter = 0f;

        // ---- Its reward (the order: Echoes, about a third of a floor's ordinary Echoes; Hades' trove's decay)
        /// <summary>
        /// The Echoes it holds whole, by the floor's number (index 0 unused, 1 to 5; a floor past the table reads its last): about a third of
        /// a floor's ordinary Echoes today (<see cref="MetaValues.PerFloor"/> 10, <see cref="MetaValues.PerElite"/> 15 and
        /// <see cref="MetaValues.PerRoom"/> 3 for each of its other combat rooms, 4 to 5 of them: 37 to 40 a floor, the act boss's 100 left out).
        /// </summary>
        public int[] Echoes = { 0, 0, 13, 13, 13, 13 };
        /// <summary>s after the opening that its reward begins to fall (Hades' <c>DecayStartDelay</c> 9, the order's 9 s).</summary>
        public float DecayAfter = 9f;
        /// <summary>The share of the whole it loses each <see cref="DecayEvery"/> after that (Hades' 1 of 100, the order's 1 % a second).</summary>
        public float DecayShare = 0.01f;
        /// <summary>s between its losses (Hades' <c>LootDecayInterval</c> 1.0): it falls in steps of a second.</summary>
        public float DecayEvery = 1f;
        /// <summary>The least share of the whole it keeps (Hades' <c>MinValue</c> 10 of 100, the order's 10 %).</summary>
        public float Least = 0.1f;

        // ---- How it looks (the order: "shut and dim" until it can be opened; "glows faintly once it can be opened"; nothing else points at it)
        /// <summary>Its colour's shade while shut (the room's fight not cleared) and once its reward is taken or lost: the model's own painted colour times this (0.45 in the first pictures was nearly black in a dark corner).</summary>
        public float DimShade = 0.6f;
        /// <summary>The colour of its faint glow while it can be opened and while its reward waits, as picked: the elite's warm gold, apart from the props' cyan glow.</summary>
        public Rgb GlowTint = new Rgb(1f, 0.78f, 0.3f);
        /// <summary>
        /// How strongly it glows then, its own painted picture lit in the tint (the chest's model has no emission map of its own). The first
        /// pictures, at 0.6 over the whole chest with no map, showed a flat cream block; with its picture as the map, 0.35.
        /// </summary>
        public float Glow = 0.35f;
        /// <summary>m over its middle the number and the prompt are drawn (the treasure room's chest's prompt stands 1.4 m over it).</summary>
        public float LabelHeight = 1.4f;

        /// <summary>Its prompt to open it (the order's words).</summary>
        public string OpenWords = "Open: a fight, the reward shrinks";
        /// <summary>Its prompt once its wave is dead, before the Echoes left ("Take 11 Echoes").</summary>
        public string TakeWords = "Take";
        /// <summary>The HUD's line before the Echoes it holds now ("TIMED CHEST   11 Echoes").</summary>
        public string HudWords = "TIMED CHEST";
    }

    /// <summary>
    /// The timed chest (work order 153): which room of a floor holds it, where it stands, what its wave brings, and how its reward falls. A
    /// rule read as the floor's fights are set up, with a seeded stream of its own (<see cref="Stage"/>), not of the generator: the floor
    /// and its pinned fingerprints are as work order 135 lays them (version 25). No engine.
    ///
    /// - A room may hold it (<see cref="RoomFault"/>): from floor <see cref="TimedChestValues.FromFloor"/>, a combat room that is not the
    ///   elite room (never the exit room nor the throne room), filled from a line-up of two kinds (its wave is of them), not the floor's
    ///   first combat room, not a room that holds a caller; and a place for it (<see cref="Places"/>). Where none may, the floor's full room
    ///   may, its wave of its two largest kinds (floor 5, whose two callers take most of its rooms of two kinds).
    /// - Which: one, drawn evenly from the stream <see cref="Stage"/> among those that may (<see cref="Room"/>).
    /// - Where: at a corner of the room's open floor (<see cref="Places"/>).
    /// - Its wave: <see cref="WaveCounts"/>. Its reward: <see cref="Whole"/>, <see cref="Share"/>, <see cref="Left"/>.
    /// </summary>
    public static class TimedChestRules
    {
        /// <summary>The stream of its draws (on the floor's seed; nothing of the generator reads it).</summary>
        public const string Stage = "timed chest";

        /// <summary>Its switch on, with its numbers.</summary>
        public static bool On(LevelValues v) => v != null && LevelValues.TimedChests && v.TimedChestRules != null;

        static T OfFloor<T>(T[] table, int floor) => table == null || table.Length == 0 ? default : table[Math.Max(0, Math.Min(table.Length - 1, floor))];

        /// <summary>The Echoes a chest of this floor holds whole (<see cref="TimedChestValues.Echoes"/>; 0 before <see cref="TimedChestValues.FromFloor"/>).</summary>
        public static int Whole(int floor, TimedChestValues c) => c == null || floor < c.FromFloor ? 0 : Math.Max(0, OfFloor(c.Echoes, floor));

        /// <summary>The share of the room's own kinds its wave brings on this floor (<see cref="TimedChestValues.WaveShare"/>).</summary>
        public static float WaveShareOf(int floor, TimedChestValues c) => c == null ? 0f : Math.Max(0f, OfFloor(c.WaveShare, floor));

        /// <summary>The room's own enemies of one kind as the floor placed them (no elite, no boss).</summary>
        public static int CountOf(LevelRoom room, EnemyKind kind) =>
            room == null ? 0 : room.EnemyGroups.Where(g => g != null && !g.Elite && !g.Boss && g.Kind == kind).Sum(g => g.Count);

        /// <summary>
        /// The two kinds of its wave: the room's line-up's lead and partner; in the floor's full room (<see cref="LineUp.Full"/>, which a floor
        /// gives its chest only when no room of two kinds may hold it: <see cref="Room"/>) the two kinds it holds most of, the most first (ties in
        /// the kinds' own order), as work order 136 takes them for the full room's first wave. False for a room with neither.
        /// </summary>
        public static bool WaveKinds(LevelRoom room, out EnemyKind lead, out EnemyKind partner)
        {
            lead = partner = default;
            if (room?.LineUp == null) return false;
            if (!room.LineUp.Full)
            {
                lead = room.LineUp.Lead;
                partner = room.LineUp.Partner;
                return true;
            }
            var counts = new Dictionary<EnemyKind, int>();
            foreach (EnemyGroup g in room.EnemyGroups)
                if (g != null && !g.Elite && !g.Boss) counts[g.Kind] = (counts.TryGetValue(g.Kind, out int n) ? n : 0) + g.Count;
            List<EnemyKind> order = counts.Keys.ToList();
            order.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : ((int)a).CompareTo((int)b));
            if (order.Count == 0) return false;
            lead = order[0];
            partner = order.Count > 1 ? order[1] : order[0];
            return true;
        }

        /// <summary>
        /// Its wave: its two kinds (<see cref="WaveKinds"/>), each <see cref="WaveShareOf"/> of the room's own count of it, rounded (half away
        /// from 0), at least one of each kind the room holds. (0, 0) for a room with no line-up.
        /// </summary>
        public static void WaveCounts(LevelLayout level, LevelRoom room, TimedChestValues c, out int lead, out int partner)
        {
            lead = partner = 0;
            if (level == null || c == null || !WaveKinds(room, out EnemyKind leadKind, out EnemyKind partnerKind)) return;
            float share = WaveShareOf(level.Floor, c);
            int Of(int n) => n <= 0 ? 0 : Math.Max(1, (int)Math.Round(n * share, MidpointRounding.AwayFromZero));
            lead = Of(CountOf(room, leadKind));
            partner = partnerKind == leadKind ? 0 : Of(CountOf(room, partnerKind));
        }

        /// <summary>
        /// The first reason this room may not hold it, in words, or null (its place asked apart: <see cref="Places"/>). <paramref name="callerRooms"/>:
        /// the rooms that hold a caller now. <paramref name="fullMay"/>: the floor's full room may (<see cref="Room"/>'s fallback).
        /// </summary>
        public static string RoomFault(LevelLayout level, LevelRoom room, TimedChestValues c, ICollection<LevelRoom> callerRooms, bool fullMay = false)
        {
            if (level == null || room == null || c == null) return "has no floor";
            if (level.Floor < c.FromFloor) return "is on floor " + level.Floor + ", before floor " + c.FromFloor;
            if (room.Kind != RoomKind.Combat) return "is not a combat room";
            if (room.Elite) return "is the elite room";
            if (room.EnemyGroups.Any(g => g != null && g.Boss)) return "holds the act boss";
            if (room.LineUp == null || (room.LineUp.Full && !fullMay)) return "is not filled from a line-up of two kinds";
            List<LevelRoom> combat = RoomCallerRules.CombatRooms(level);
            int at = combat.IndexOf(room);
            if (at < 0) return "is not on the floor's row";
            if (at == 0) return "is the floor's first combat room";
            if (callerRooms != null && callerRooms.Contains(room)) return "holds a caller";
            WaveCounts(level, room, c, out int lead, out int partner);
            if (lead + partner <= 0) return "has no enemy for its wave";
            return null;
        }

        /// <summary>
        /// The places of the room the chest may stand at, best first: points of a grid over the room's open floor
        /// (<see cref="TimedChestValues.PlaceStep"/>) at <see cref="TimedChestValues.FromWalls"/> from its walls and carved outline,
        /// <see cref="TimedChestValues.FromDoors"/> from every door, its footprint <see cref="TimedChestValues.FromProps"/> from every prop's,
        /// <see cref="TimedChestValues.FromEnemies"/> from every place an enemy of the room stands at, in the default camera's view from the
        /// room's middle (<see cref="TimedChestValues.ViewMargin"/> inside); of those, for each corner of the room's rectangle the one nearest
        /// it, within <see cref="TimedChestValues.CornerMost"/> of it. Empty when no corner has one.
        /// </summary>
        public static List<LevelPoint> Places(LevelLayout level, LevelRoom room, LevelValues v)
        {
            var kept = new List<LevelPoint>();
            if (level == null || room == null || v == null || v.TimedChestRules == null) return kept;
            TimedChestValues c = v.TimedChestRules;
            double cs = level.CellSize;
            var doors = new List<LevelDoor>(level.DoorsOf(room.Order));
            RangedAroundValues r = v.RangedAround ?? new RangedAroundValues();
            LevelPoint middle = room.Middle(cs);
            PropSize size = PropSize.Of(PropModel.Chest);
            var enemies = new List<LevelPoint>();
            for (int i = 0; i < room.EnemyPoints.Count; i++) enemies.AddRange(room.PlacesAt(i));
            double extra = Math.Max(0d, c.FromWalls - v.WallClearance);
            var good = new List<LevelPoint>();
            for (double y = room.Rect.Y * cs + c.PlaceStep * 0.5; y < room.Rect.Top * cs; y += c.PlaceStep)
                for (double x = room.Rect.X * cs + c.PlaceStep * 0.5; x < room.Rect.Right * cs; x += c.PlaceStep)
                {
                    var p = new LevelPoint(x, y);
                    if (PlaceFault(level, room, p, doors, v, c, r, middle, size, enemies, extra) == null) good.Add(p);
                }
            if (good.Count == 0) return kept;
            double x0 = room.Rect.X * cs, x1 = room.Rect.Right * cs, y0 = room.Rect.Y * cs, y1 = room.Rect.Top * cs;
            foreach (LevelPoint corner in new[] { new LevelPoint(x0, y0), new LevelPoint(x1, y0), new LevelPoint(x1, y1), new LevelPoint(x0, y1) })
            {
                LevelPoint best = default;
                double bestD = double.PositiveInfinity;
                foreach (LevelPoint p in good)
                {
                    double d = p.DistanceTo(corner);
                    if (d < bestD - 1e-9)
                    {
                        bestD = d;
                        best = p;
                    }
                }
                if (bestD <= c.CornerMost + 1e-9 && !kept.Any(k => k.DistanceTo(best) < 1e-6)) kept.Add(best);
            }
            return kept;
        }

        /// <summary>The first rule a point of the room breaks for the chest's middle, in words, or null (<see cref="Places"/>, before its corners): for the tests.</summary>
        public static string PlaceFault(LevelLayout level, LevelRoom room, LevelPoint p, LevelValues v)
        {
            TimedChestValues c = v?.TimedChestRules ?? new TimedChestValues();
            var enemies = new List<LevelPoint>();
            for (int i = 0; i < room.EnemyPoints.Count; i++) enemies.AddRange(room.PlacesAt(i));
            return PlaceFault(level, room, p, new List<LevelDoor>(level.DoorsOf(room.Order)), v, c, v.RangedAround ?? new RangedAroundValues(),
                              room.Middle(level.CellSize), PropSize.Of(PropModel.Chest), enemies, Math.Max(0d, c.FromWalls - v.WallClearance));
        }

        /// <summary>The first rule a point breaks for the chest's middle, in words, or null (<see cref="Places"/>).</summary>
        static string PlaceFault(LevelLayout level, LevelRoom room, LevelPoint p, List<LevelDoor> doors, LevelValues v, TimedChestValues c, RangedAroundValues r,
                                 LevelPoint middle, PropSize size, List<LevelPoint> enemies, double extra)
        {
            const double slack = 1e-9;
            string wall = LevelGeometry.EnemyPlaceFault(level, room, p, null, doors, v, extra);
            if (wall != null) return wall;
            foreach (LevelDoor d in doors)
                if (LevelGeometry.DistanceToDoor(p, d, level.CellSize) < c.FromDoors - slack) return "is nearer a door than " + c.FromDoors + " m";
            PropFootprint chest = PropFootprint.Of(size, p, 0);
            foreach (LevelProp prop in room.Props)
                if (prop.Footprint.DistanceTo(chest) < c.FromProps - slack) return "is nearer a prop than " + c.FromProps + " m";
            foreach (LevelPoint e in enemies)
                if (e.DistanceTo(p) < c.FromEnemies - slack) return "is nearer an enemy's place than " + c.FromEnemies + " m";
            if (p.DistanceTo(middle) < c.MiddleLeast - slack) return "is nearer the room's middle than " + c.MiddleLeast + " m";
            if (!LevelRanged.InDesignView(middle, p, r, c.ViewMargin)) return "is not in view from the room's middle";
            return null;
        }

        /// <summary>
        /// The room of the floor that holds its timed chest, or null: one drawn evenly from the stream <see cref="Stage"/> among those that
        /// may (<see cref="RoomFault"/> none, and a place). Null with the switch off, on floor 1, and where no room may.
        /// </summary>
        public static LevelRoom Room(LevelLayout level, LevelValues v, ICollection<LevelRoom> callerRooms)
        {
            if (level == null || !On(v) || Whole(level.Floor, v.TimedChestRules) <= 0) return null;
            List<LevelRoom> may = RoomCallerRules.CombatRooms(level)
                .Where(r => RoomFault(level, r, v.TimedChestRules, callerRooms) == null && Places(level, r, v).Count > 0).ToList();
            // Where no room of two kinds may (floor 5's two callers take them most often), the floor's full room may: every floor from 2 has one.
            if (may.Count == 0)
                may = RoomCallerRules.CombatRooms(level)
                    .Where(r => RoomFault(level, r, v.TimedChestRules, callerRooms, fullMay: true) == null && Places(level, r, v).Count > 0).ToList();
            if (may.Count == 0) return null;
            LevelRandom rng = LevelRandom.Stream(level.Seed, Stage, level.Floor);
            rng.Shuffle(may);
            return may[0];
        }

        /// <summary>The places of <see cref="Places"/> in the order the room's own draw tries them (the stream <see cref="Stage"/>, by the room's order): the first the floor's NavMesh holds is its place.</summary>
        public static List<LevelPoint> PlacesInOrder(LevelLayout level, LevelRoom room, LevelValues v)
        {
            List<LevelPoint> places = Places(level, room, v);
            if (level != null && room != null) LevelRandom.Stream(level.Seed, Stage, 1000 + room.Order).Shuffle(places);
            return places;
        }

        /// <summary>
        /// The share of its whole the reward holds <paramref name="seconds"/> after the opening: whole until
        /// <see cref="TimedChestValues.DecayAfter"/>, then <see cref="TimedChestValues.DecayShare"/> less at each
        /// <see cref="TimedChestValues.DecayEvery"/> after it, never under <see cref="TimedChestValues.Least"/>. The game asks it with the
        /// seconds frozen at the wave's last kill.
        /// </summary>
        public static double Share(double seconds, TimedChestValues c)
        {
            if (c == null) return 1d;
            double least = Math.Max(0d, Math.Min(1d, c.Least));
            if (seconds < c.DecayAfter) return 1d;
            double steps = c.DecayEvery > 0f ? Math.Floor((seconds - c.DecayAfter) / c.DecayEvery + 1e-9) : seconds - c.DecayAfter;
            return Math.Max(least, 1d - Math.Max(0d, c.DecayShare) * steps);
        }

        /// <summary>The Echoes a share of <paramref name="whole"/> is: rounded (half away from 0), never under the least share's own (rounded) nor 0.</summary>
        public static int Left(int whole, double share, TimedChestValues c)
        {
            if (whole <= 0) return 0;
            double least = c != null ? Math.Max(0d, Math.Min(1d, c.Least)) : 0d;
            int floorOf = (int)Math.Round(whole * least, MidpointRounding.AwayFromZero);
            int n = (int)Math.Round(whole * Math.Max(0d, Math.Min(1d, share)), MidpointRounding.AwayFromZero);
            return Math.Max(Math.Max(0, floorOf), Math.Min(whole, n));
        }
    }

    /// <summary>The timed chest's line on the settings screen's ROOMS page (work order 153): "Timed chest" on or off, after "Elites".</summary>
    public static class TimedChestLines
    {
        public const string Name = "Timed chest";

        /// <summary>The line's words: on or off.</summary>
        public static string Words(bool on) => on ? GraphicsRules.On : GraphicsRules.Off;
    }

    /// <summary>
    /// The timed chest in the log (work order 153): one line a floor that held one (<see cref="Chest"/>), as its reward was taken, as she
    /// died in its fight, or as the floor went. Name: "taken", "died", "not opened" or "left" (opened, and the floor went before its reward
    /// was taken): floor, room (as the run box counts), order, opened ("true"/"false"), after_clear (s from the room's clearing to the
    /// opening; "" not opened), kinds ("swarmer 4, brute 2", its wave), count, clear (s from the opening to its last kill; "" not cleared),
    /// share (of its whole left, 0 to 1; "" not cleared), whole and echoes (the Echoes it held whole, and those taken), damage (her damage
    /// taken from the opening to its end). Read by the session's summary (<see cref="SessionSummary.ComputeTimedChests"/>).
    /// </summary>
    public static class TimedChestEvents
    {
        public const string Chest = "timed_chest";
        public const string Taken = "taken", Died = "died", NotOpened = "not opened", Left = "left";

        public static string Seconds(float s) => s >= 0f ? s.ToString("0.##", CultureInfo.InvariantCulture) : "";
        public static string Share(double s) => s >= 0d ? s.ToString("0.###", CultureInfo.InvariantCulture) : "";
    }
}
