using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of a room's waves (work order 136; Kristian's ticks of 2026-11-25, asked with the warning that the first rush gets
    /// smaller: waves "Ja, i de fleste rom", and the next wave "Når halve første er død"; on 2026-11-27, after a run without them: "jeg
    /// trodde vi hadde implimentert hades fremgangsmåten i hvert rom"). First guesses, his to tune by playing. Held by
    /// <see cref="LevelValues.WaveRules"/>, read only with <see cref="LevelValues.Waves"/> on.
    /// </summary>
    [Serializable]
    public sealed class RoomWaveValues
    {
        /// <summary>Waves of every combat room with a line-up (the order: "two waves"), the floor's full room too.</summary>
        public int Waves = 2;
        /// <summary>Waves of each floor's last combat room before its exit (the order: "three waves"; research R3's Hades exit rooms).</summary>
        public int LastRoomWaves = 3;
        /// <summary>
        /// The share of the wave before that must be dead before the next wave starts, of its count, rounded up (his tick "Når halve
        /// første er død"; the order: "half of its count, rounded up").
        /// </summary>
        public float TriggerShare = 0.5f;

        // ---- Where a wave's enemy arrives (the order's rule; research R3's B1: "7 to 11 m from her, in view, ... at least 3 m from her")
        /// <summary>m from her that an arrival's point is at the nearest (the order's 7).</summary>
        public float Nearest = 7f;
        /// <summary>m from her that an arrival's point is at the farthest (the order's 11).</summary>
        public float Farthest = 11f;
        /// <summary>m from her an arrival never stands nearer than, whatever else gives way (the order's 3; the doors' pull keeps the same, <see cref="RoomFightValues.DoorPutClearOfHer"/>).</summary>
        public float NeverNearer = 3f;
        /// <summary>
        /// m an arrival's point stands at least from every other arrival's of the same moment and from every living enemy of the room in
        /// play (the order's "a named distance from each other"; the producer's first guess: a brute's body is about 1.4 m across, so two
        /// metres keep two bodies and their discs apart).
        /// </summary>
        public float Apart = 2f;
        /// <summary>
        /// m: an arrival's point must be on the floor's NavMesh within this of where it was asked (a prop's footprint is off the NavMesh, so
        /// a point in a prop is refused, never moved beside it); the boss's calls sample as near (<c>GeneratedLevel.CallPack</c>).
        /// </summary>
        public float OnNavMeshWithin = 0.3f;
        /// <summary>m between the points of a room's floor tried for an arrival (a grid over its open floor, each at the level's clearance from walls and doors).</summary>
        public float PointStep = 1f;
        /// <summary>
        /// s an enemy of a wave that found no good point waits before it is tried again by the same rule (the order: "the rest wait and
        /// arrive one named interval later"; the producer's first guess, half a second: about the time she takes to walk 3 m).
        /// </summary>
        public float WaitSeconds = 0.5f;
        /// <summary>
        /// The most enemies of a wave that begin their discs at one moment (the producer's ruling of 2026-11-27, after work order 135 made
        /// the waves larger: "a large wave arrives in groups one named interval apart, each with its own 1 s discs"); the rest wait, and
        /// the next group begins <see cref="WaitSeconds"/> after the group before has stood, by the same rule. Counted in enemies, not in
        /// a kind's weight: the cases measured unfair were counts of one kind (seven brutes, twelve to fourteen swarmers), and a count is
        /// what he sees. 5: in the test bot's measure on the merged floors every wave of five or fewer took at most about 1.5 times its
        /// floor's version-24 opening damage, and a wave of seven brutes 2.1 times. 0: no limit (every one at once, as before the ruling).
        /// </summary>
        public int MostAtOnce = 5;

        // ---- The tell (research R3's B1: "a flat glowing disc of 1 m radius", Hades' 1.0 s)
        /// <summary>s the disc stands on the ground before its enemy stands there awake (the order's 1.0 s; Hades waits 1.0 s, research R3).</summary>
        public float DiscSeconds = 1f;
        /// <summary>m: the disc's radius (research R3's B1).</summary>
        public float DiscRadius = 1f;
        /// <summary>
        /// The disc's colour, as picked: a pale violet. Not an attack's: an enemy's wind-up is drawn red, a counterable one blue (0.15,
        /// 0.45, 1), the bomber's sparks orange; nor the warder's ward green (0.3, 1, 0.55), the shut doors' orange nor the elite's gold.
        /// </summary>
        public Rgb DiscTint = new Rgb(0.78f, 0.6f, 1f);
    }

    /// <summary>
    /// Two waves (work order 136): which rooms bring their enemies in waves, who comes in which wave, when the next wave starts, and where
    /// its enemies may arrive. A rule of the fight read at the floor's set-up from the room's place on the floor, not of the generator:
    /// the floor and its pinned fingerprints are as work order 135 lays them, and the waves divide a room's enemies, they add none. No
    /// engine: the game hands in her place, the camera's view of the ground (<see cref="ScreenGround"/>) and the room's points.
    ///
    /// - Which rooms: every combat room with a line-up (<see cref="LevelRoom.LineUp"/>) has <see cref="RoomWaveValues.Waves"/> (two),
    ///   but the first combat room of floor 1 (one, as before) and each floor's last combat room before its exit
    ///   (<see cref="RoomWaveValues.LastRoomWaves"/>, three). The floor's full room has two as well. The elite room, the exit room, floor
    ///   5's throne room and a room without a line-up are as before (one).
    /// - Who: wave 1 is the lead kind (placed and woken as before), wave 2 the partner; with three, the lead split in two halves by count
    ///   (the larger first) as waves 1 and 3 with the partner between; in the full room the first wave its two kinds with the most
    ///   enemies, the second the rest. An empty wave is dropped.
    /// - When: the next wave starts the moment <see cref="RoomWaveValues.TriggerShare"/> (half) of the wave before is dead, counted from
    ///   its count and rounded up, and never while a wave is still arriving.
    /// - Where: <see cref="ArrivalFault"/>.
    /// </summary>
    public static class RoomWaveRules
    {
        /// <summary>The stream of the arrivals' draws (on the floor's seed; nothing of the generator reads it).</summary>
        public const string Stage = "waves";

        /// <summary>How many waves the room brings, from its place on its floor (1: as before).</summary>
        public static int WavesOf(LevelLayout level, LevelRoom room, RoomWaveValues w)
        {
            if (level == null || room == null || w == null || room.Kind != RoomKind.Combat || room.Elite || room.LineUp == null) return 1;
            LevelRoom first = null, last = null;
            List<LevelRoom> rooms = level.Rooms;
            for (int i = 0; i < rooms.Count; i++)
            {
                LevelRoom r = rooms[i];
                if (r.Kind != RoomKind.Combat) continue;
                if (first == null || r.Order < first.Order) first = r;
                if (last == null || r.Order > last.Order) last = r;
            }
            if (room.LineUp.Full) return Math.Max(1, Math.Min(2, w.Waves));
            if (room == first && level.Floor <= 1) return 1;
            if (room == last) return Math.Max(1, w.LastRoomWaves);
            return Math.Max(1, w.Waves);
        }

        /// <summary>
        /// The wave (from 1) of each of a room's enemies, given in the order the floor placed them (<paramref name="kinds"/>, with
        /// <paramref name="elites"/> true for the elite), for a room of <paramref name="waves"/> waves filled from <paramref name="lineUp"/>.
        /// Empty waves are dropped, so the numbers run 1, 2 (, 3) with none missing; one wave: all 1.
        /// </summary>
        public static int[] Split(IReadOnlyList<EnemyKind> kinds, IReadOnlyList<bool> elites, LineUp lineUp, int waves)
        {
            int n = kinds != null ? kinds.Count : 0;
            var wave = new int[n];
            for (int i = 0; i < n; i++) wave[i] = 1;
            if (n == 0 || waves <= 1 || lineUp == null) return wave;
            bool Elite(int i) => elites != null && i < elites.Count && elites[i];
            if (lineUp.Full)
            {
                // The full room: its kinds by how many of each it holds, the most first (ties in the kinds' own order); the first two are
                // wave 1, the rest wave 2. With only two kinds, the second is wave 2.
                var counts = new Dictionary<EnemyKind, int>();
                for (int i = 0; i < n; i++)
                    if (!Elite(i)) counts[kinds[i]] = (counts.TryGetValue(kinds[i], out int c) ? c : 0) + 1;
                var order = new List<EnemyKind>(counts.Keys);
                order.Sort((a, b) => counts[b] != counts[a] ? counts[b].CompareTo(counts[a]) : ((int)a).CompareTo((int)b));
                int firstWaveKinds = order.Count >= 3 ? 2 : 1;
                var firstWave = new HashSet<EnemyKind>();
                for (int k = 0; k < order.Count && k < firstWaveKinds; k++) firstWave.Add(order[k]);
                for (int i = 0; i < n; i++) wave[i] = Elite(i) || firstWave.Contains(kinds[i]) ? 1 : 2;
                return Compact(wave);
            }
            int leads = 0;
            for (int i = 0; i < n; i++) if (!Elite(i) && kinds[i] == lineUp.Lead) leads++;
            int leadFirst = waves >= 3 ? (leads + 1) / 2 : leads; // three waves: the lead's larger half first
            int seen = 0;
            for (int i = 0; i < n; i++)
            {
                if (Elite(i)) continue;
                if (kinds[i] == lineUp.Lead) wave[i] = seen++ < leadFirst ? 1 : 3;
                else if (kinds[i] == lineUp.Partner) wave[i] = 2;
            }
            return Compact(wave);
        }

        /// <summary>The waves renumbered so that none is empty: 1, 3 becomes 1, 2.</summary>
        static int[] Compact(int[] wave)
        {
            int most = 0;
            foreach (int w in wave) most = Math.Max(most, w);
            var map = new int[most + 1];
            int next = 0;
            for (int w = 1; w <= most; w++)
                if (Array.IndexOf(wave, w) >= 0) map[w] = ++next;
            for (int i = 0; i < wave.Length; i++) wave[i] = map[wave[i]];
            return wave;
        }

        /// <summary>The number of waves in a split (its largest number); 1 for none.</summary>
        public static int Count(int[] split)
        {
            int most = 1;
            if (split != null) foreach (int w in split) most = Math.Max(most, w);
            return most;
        }

        /// <summary>How many of a wave of <paramref name="count"/> must be dead before the next starts: the share of its count, rounded up (half of 5 is 3), at least 1.</summary>
        public static int DeadFor(int count, RoomWaveValues w)
        {
            if (count <= 0) return 0;
            double share = w != null ? Math.Max(0f, Math.Min(1f, w.TriggerShare)) : 0.5;
            return Math.Max(1, Math.Min(count, (int)Math.Ceiling(count * share - 1e-9)));
        }

        /// <summary>
        /// Does the next wave start now: the wave before (<paramref name="count"/> enemies, <paramref name="dead"/> of them dead) has lost
        /// its share, and no wave is still arriving (<paramref name="arriving"/>: one of its enemies waits for a point or stands in its disc).
        /// </summary>
        public static bool NextDue(int count, int dead, bool arriving, RoomWaveValues w) => !arriving && dead >= DeadFor(count, w);

        /// <summary>
        /// The first rule a point of the room's floor breaks for an arrival now, in words, or null. <paramref name="p"/> stands on the room's
        /// open floor (the floor gives only such points); she stands at <paramref name="her"/>; <paramref name="view"/> is what the camera
        /// shows of the ground and <paramref name="margin"/> how far inside it (<see cref="RangedAroundValues.ScreenMargin"/>, the ranged
        /// rule's); (<paramref name="upX"/>, <paramref name="upZ"/>) is the way up his screen along the ground (the camera's forward, flat).
        /// <paramref name="taken"/> are the points of the arrivals of the moment and of the living enemies in play.
        /// </summary>
        public static string ArrivalFault(LevelPoint p, LevelPoint her, GroundQuad view, double margin, double upX, double upZ,
                                          IReadOnlyList<LevelPoint> taken, RoomWaveValues w)
        {
            double d = p.DistanceTo(her);
            if (d < w.NeverNearer - 1e-9) return "is nearer her than " + Text(w.NeverNearer) + " m";
            if (d < w.Nearest - 1e-9) return "is nearer her than " + Text(w.Nearest) + " m";
            if (d > w.Farthest + 1e-9) return "is farther from her than " + Text(w.Farthest) + " m";
            if (Below(p, her, upX, upZ)) return "is below her on his screen";
            if (!ScreenGround.Shows(view, p.X, p.Y, margin)) return "is not on his screen " + Text(margin) + " m inside its edge";
            if (taken != null)
                for (int i = 0; i < taken.Count; i++)
                    if (taken[i].DistanceTo(p) < w.Apart - 1e-9) return "is nearer another enemy than " + Text(w.Apart) + " m";
            return null;
        }

        /// <summary>
        /// Does the point stand below her on his screen: behind her along the way up the screen (the camera's forward, flat)? A camera
        /// without a turn of its picture shows the ground's lines across that way as its rows, so this is "below her row".
        /// </summary>
        public static bool Below(LevelPoint p, LevelPoint her, double upX, double upZ)
        {
            double l = Math.Sqrt(upX * upX + upZ * upZ);
            if (l < 1e-9) return false;
            return ((p.X - her.X) * upX + (p.Y - her.Y) * upZ) / l < -1e-9;
        }

        /// <summary>
        /// The points for as many of a wave's waiting enemies as can arrive now (at most <paramref name="wanted"/>): the room's
        /// <paramref name="points"/> in an order drawn from <paramref name="rng"/>, each kept when it breaks no rule
        /// (<see cref="ArrivalFault"/>) with those kept before it among <paramref name="taken"/> (which grows), and when
        /// <paramref name="reachable"/> says the floor lets an enemy stand and walk there (on the NavMesh, a path to her); the indices of the
        /// points kept, in the order kept.
        /// </summary>
        public static List<int> Pick(IReadOnlyList<LevelPoint> points, int wanted, LevelPoint her, GroundQuad view, double margin, double upX, double upZ,
                                     List<LevelPoint> taken, RoomWaveValues w, LevelRandom rng, Func<int, bool> reachable)
        {
            var kept = new List<int>();
            if (points == null || wanted <= 0) return kept;
            var order = new List<int>(points.Count);
            for (int i = 0; i < points.Count; i++) order.Add(i);
            rng?.Shuffle(order);
            foreach (int i in order)
            {
                if (kept.Count >= wanted) break;
                if (ArrivalFault(points[i], her, view, margin, upX, upZ, taken, w) != null) continue;
                if (reachable != null && !reachable(i)) continue;
                kept.Add(i);
                taken.Add(points[i]);
            }
            return kept;
        }

        /// <summary>
        /// The room's points an arrival may be tried at (<paramref name="step"/> apart, on a grid from its south-west corner): each on its open
        /// floor at the level's clearance from its walls, its carved cells and every door of it (<see cref="LevelGeometry.EnemyPlaceFault"/>
        /// with no door in: an arrival comes near her, wherever she is).
        /// </summary>
        public static List<LevelPoint> RoomPoints(LevelLayout level, LevelRoom room, LevelValues v, double step)
        {
            var points = new List<LevelPoint>();
            if (level == null || room == null || v == null || step <= 0) return points;
            double cs = level.CellSize;
            var doors = new List<LevelDoor>(level.DoorsOf(room.Order));
            for (double y = room.Rect.Y * cs + step * 0.5; y < room.Rect.Top * cs; y += step)
                for (double x = room.Rect.X * cs + step * 0.5; x < room.Rect.Right * cs; x += step)
                {
                    var p = new LevelPoint(x, y);
                    if (LevelGeometry.EnemyPlaceFault(level, room, p, null, doors, v) == null) points.Add(p);
                }
            return points;
        }

        /// <summary>A wave's kinds and counts as the recorder writes them: "swarmer 6, brute 2", the kinds in the order they first come.</summary>
        public static string KindsWords(IReadOnlyList<EnemyKind> kinds)
        {
            if (kinds == null || kinds.Count == 0) return "";
            var order = new List<EnemyKind>();
            var counts = new Dictionary<EnemyKind, int>();
            foreach (EnemyKind k in kinds)
            {
                if (!counts.ContainsKey(k))
                {
                    order.Add(k);
                    counts[k] = 0;
                }
                counts[k]++;
            }
            var sb = new StringBuilder();
            foreach (EnemyKind k in order)
            {
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(EnemyKinds.Word(k)).Append(' ').Append(counts[k].ToString(CultureInfo.InvariantCulture));
            }
            return sb.ToString();
        }

        static string Text(double v) => v.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
