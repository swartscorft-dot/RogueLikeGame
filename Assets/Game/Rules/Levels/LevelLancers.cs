using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where lancers would stand in the floors (work order 108), by the bombers' and the warders' pattern
    /// (<see cref="LevelBombers"/>, <see cref="LevelWarders"/>), with <see cref="LevelValues.Lancers"/> on and the act's plan on
    /// (<see cref="LevelValues.LancersInFloors"/>): from <see cref="LancerFloorValues.FromFloor"/> (4), in a floor in a line, in a
    /// combat room only (never in the exit room, so never in the act boss's room), never in the floor's first combat room (she
    /// comes in to her first fight without one, as the bombers' rule has it), at most <see cref="LancerFloorValues.MostInRoom"/>
    /// of the floor (one on floor 4, two on floor 5). How many a room holds is drawn on a stream of its own (<see cref="Stage"/>),
    /// from none to that most, once a floor, after the bombers and the warders: so a floor with them on would hold the very groups
    /// it holds with them off, but for the swarmers they take the places of. Each takes the place of one swarmer (of the largest
    /// pack, the first of them when two are as large), alone at its own point; the room's count of enemies is the same; a room
    /// with no swarmer left gets none. Never an elite. On a text map it is l (<see cref="EnemyKinds.Letter"/>).
    ///
    /// Work order 108 built it outside the generator (the sprint's rule 3: the generator's version and its pins were work orders
    /// 103's and 104's). Work order 118 put it into the floors as the generator's version 22: <c>LevelGenerator.Generate</c> calls
    /// <see cref="Put"/> after the warders, the generator's placing stands a lancer in the far band with the shooters, the casters
    /// and the warders, and <c>LevelValidator</c> calls <see cref="Faults"/>. The room lab stands one in a room through its own
    /// preset (<see cref="RoomLab.Mixes"/>).
    /// </summary>
    public static class LevelLancers
    {
        /// <summary>The generator's stream for how many lancers each room holds.</summary>
        public const string Stage = "lancers";

        /// <summary>
        /// The most lancers a room may hold: none with the switch off (or the act's plan off), before
        /// <see cref="LancerFloorValues.FromFloor"/>, in the floor's first combat room, in a room that holds the act boss, and in a
        /// room that is not a combat room; else the floor's most.
        /// </summary>
        public static int Most(LevelValues v, RoomKind kind, int floor, bool firstCombatRoom, bool bossRoom = false)
        {
            LancerFloorValues l = v?.LancerFloors;
            if (v == null || !v.LancersInFloors || l == null || floor < l.FromFloor || firstCombatRoom || bossRoom) return 0;
            if (kind != RoomKind.Combat) return 0;
            return l.MostOnFloor(floor);
        }

        /// <summary>
        /// Puts the lancers into a floor's fights (<paramref name="fights"/>: its combat rooms in order and then its exit room,
        /// each as its groups, as the generator lays them into the row), in place. Nothing with the switch off.
        /// </summary>
        public static void Put(List<List<EnemyGroup>> fights, ulong seed, int floor, LevelValues v)
        {
            if (fights == null || v == null || !v.LancersInFloors) return;
            LevelRandom rng = LevelRandom.Stream(seed, Stage, floor);
            for (int i = 0; i < fights.Count; i++)
            {
                RoomKind kind = i == fights.Count - 1 ? RoomKind.Exit : RoomKind.Combat;
                bool boss = fights[i] != null && fights[i].Any(g => g != null && g.Boss);
                int most = Most(v, kind, floor, i == 0, boss);
                if (most <= 0) continue;
                int n = rng.Range(0, most);
                Convert(fights[i], n);
            }
        }

        /// <summary>
        /// Work order 118 ("it stands in the room's far half with floor to charge across"): metres of the room's own floor in a straight
        /// line from <paramref name="from"/> toward the middle of the door she comes in by, before a carved cell or a wall of the room
        /// comes within <see cref="LancerFloorValues.ChargeHalfWidth"/> of the line, walked in <see cref="LancerFloorValues.ChargeStep"/>,
        /// looked along no farther than <paramref name="upTo"/>; the whole way when the door is reached first. Props are not counted:
        /// a prop in its line is the cover she uses on purpose.
        /// </summary>
        public static double FloorToCharge(LevelLayout level, LevelRoom room, LevelPoint from, LevelDoor entry, LancerFloorValues l, double upTo = double.PositiveInfinity)
        {
            if (level == null || room == null || entry == null || l == null) return 0d;
            double cs = level.CellSize, half = l.ChargeHalfWidth, step = Math.Max(1e-3, l.ChargeStep);
            entry.Opening(cs, out LevelPoint a, out LevelPoint b);
            var to = new LevelPoint((a.X + b.X) * 0.5, (a.Y + b.Y) * 0.5);
            double length = from.DistanceTo(to);
            if (length < 1e-9) return 0d;
            double dx = (to.X - from.X) / length, dy = (to.Y - from.Y) / length;
            for (double d = step; d < length; d += step)
            {
                if (d > upTo) return d;
                var p = new LevelPoint(from.X + dx * d, from.Y + dy * d);
                if (LevelGeometry.DistanceToDoor(p, entry, cs) < half) return length;
                if (RoomShapes.DistanceToSolid(room, p, cs) < half || LevelGeometry.DistanceToWalls(p, room.Rect, cs) < half) return d - step;
            }
            return length;
        }

        /// <summary>Whether a lancer at <paramref name="p"/> has <see cref="LancerFloorValues.ChargeFloorAtLeast"/> of floor to charge across (<see cref="FloorToCharge"/>).</summary>
        public static bool RoomToCharge(LevelLayout level, LevelRoom room, LevelPoint p, LevelDoor entry, LevelValues v)
        {
            LancerFloorValues l = v?.LancerFloors;
            return l != null && FloorToCharge(level, room, p, entry, l, l.ChargeFloorAtLeast) >= l.ChargeFloorAtLeast - 1e-9;
        }

        /// <summary>
        /// The room's depth from the door she comes in by (work order 118): its farthest place from it, as the generator measures its
        /// bands (its grid of places <see cref="LevelValues.PointStep"/> apart, <see cref="LevelValues.WallClearance"/> in from its walls).
        /// </summary>
        public static double Depth(LevelLayout level, LevelRoom room, LevelDoor entry, LevelValues v)
        {
            double cs = level.CellSize, clear = v.WallClearance, step = v.PointStep, far = 0d;
            int nx = (int)Math.Floor((room.Rect.Width * cs - 2 * clear) / step + 1e-9) + 1;
            int ny = (int)Math.Floor((room.Rect.Height * cs - 2 * clear) / step + 1e-9) + 1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                    far = Math.Max(far, LevelGeometry.DistanceToDoor(new LevelPoint(room.Rect.X * cs + clear + i * step, room.Rect.Y * cs + clear + j * step), entry, cs));
            return far;
        }

        /// <summary>The lancers of these groups, counted one by one.</summary>
        public static int Count(IEnumerable<EnemyGroup> groups) =>
            groups == null ? 0 : groups.Where(g => g != null && g.Kind == EnemyKind.Lancer).Sum(g => g.Count);

        /// <summary>
        /// Up to <paramref name="lancers"/> swarmers of these groups become lancers, each alone: from the largest pack (the first
        /// of the largest), a pack of one going with it. Returns how many did.
        /// </summary>
        public static int Convert(List<EnemyGroup> groups, int lancers)
        {
            int done = 0;
            if (groups == null) return 0;
            for (; done < lancers; done++)
            {
                EnemyGroup pack = null;
                foreach (EnemyGroup g in groups)
                    if (g != null && g.Kind == EnemyKind.Swarmer && !g.Elite && !g.Boss && g.Count > 0 && (pack == null || g.Count > pack.Count)) pack = g;
                if (pack == null) break;
                pack.Count--;
                if (pack.Count == 0) groups.Remove(pack);
                groups.Add(new EnemyGroup(EnemyKind.Lancer));
            }
            return done;
        }

        /// <summary>
        /// The validator's rules for them: none while the switch is off, none before their floor, none in the floor's first combat
        /// room, none outside a combat room, none beside the act boss, no more in a room than <see cref="Most"/>, none an elite,
        /// each alone. (Called by the validator since work order 118.)
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues v, List<string> faults)
        {
            if (level == null || v == null || faults == null) return;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            LevelRoom first = null;
            foreach (LevelRoom r in level.Rooms)
            {
                List<EnemyGroup> lancers = r.EnemyGroups.Where(g => g != null && g.Kind == EnemyKind.Lancer).ToList();
                if (lancers.Count == 0) continue;
                if (first == null) first = LevelGeometry.MainPath(level).FirstOrDefault(x => x.Kind == RoomKind.Combat);
                int count = lancers.Sum(g => g.Count);
                bool boss = r.EnemyGroups.Any(g => g != null && g.Boss);
                int most = level.InALine ? Most(v, r.Kind, level.Floor, r == first, boss) : 0;
                if (count > most)
                    faults.Add($"{Name(r)} holds {count} lancers, floor {level.Floor} at most {most} in a {r.Kind} room"
                               + (r == first ? " (the floor's first combat room)" : "") + (boss ? " (the act boss's room)" : ""));
                foreach (EnemyGroup g in lancers)
                {
                    if (g.Elite || g.Boss) faults.Add($"{Name(r)} has a lancer as its elite or boss");
                    if (g.Count != 1) faults.Add($"{Name(r)} has {g.Count} lancers at one point, and each stands alone");
                }
                // Work order 118: where each stands in a floor in a line, the far half with floor to charge across.
                LevelDoor entry = level.InALine && v.LancerFloors != null ? LevelGeometry.EntryDoor(level, r) : null;
                if (entry == null) continue;
                double depth = Depth(level, r, entry, v);
                for (int i = 0; i < r.EnemyGroups.Count && i < r.EnemyPoints.Count; i++)
                {
                    if (r.EnemyGroups[i] == null || r.EnemyGroups[i].Kind != EnemyKind.Lancer) continue;
                    LevelPoint p = r.EnemyPoints[i];
                    double share = LevelGeometry.DistanceToDoor(p, entry, level.CellSize) / Math.Max(1e-9, depth);
                    if (share < v.LancerFloors.FarHalf - 1e-9)
                        faults.Add($"{Name(r)}'s lancer at {p} stands at {share.ToString("P0", CultureInfo.InvariantCulture)} of the room's depth, short of its far half");
                    if (!RoomToCharge(level, r, p, entry, v))
                        faults.Add($"{Name(r)}'s lancer at {p} has less than {v.LancerFloors.ChargeFloorAtLeast.ToString(CultureInfo.InvariantCulture)} m of floor to charge across toward the door in");
                }
            }
        }
    }

    /// <summary>The lancers' numbers in the floors (work order 108; the producer's first guesses, read only with <see cref="LevelValues.Lancers"/> on).</summary>
    [Serializable]
    public sealed class LancerFloorValues
    {
        // Work order 108 (2026-11-24, his yes to "Etasje 4: stormer i rett linje langveisfra"): from floor 4.
        public int FromFloor = 4;
        // The most lancers in a combat room, counted from FromFloor (as the bombers' table, BomberFloorValues.MostInRoom): one on the
        // floor that brings it, two from the floor after (the order: at most 1 in a combat room on floor 4 and 2 on floor 5).
        public int[] MostInRoom = { 1, 2 };

        // Work order 118 (the generator's version 22): where it stands in a room. The generator stands it in LevelValues.FarBand of
        // the room's depth from the door she comes in by (with the shooters, the casters and the warders), and there first at a place
        // with ChargeFloorAtLeast of the room's floor in a straight line toward that door (LevelLancers.FloorToCharge: no carved cell
        // and no wall within ChargeHalfWidth of the line); then at such a place in the room's far half (FarHalf); only then where it
        // fits in its band, or anywhere, as every kind. All first guesses of the producer's.
        public double ChargeFloorAtLeast = 10.0; // m: the nearest its charge is chosen from (LancerValues' MinDistance, 10 m)
        public double ChargeHalfWidth = 0.35;    // m to a side of the line kept clear: its body's radius (LancerValues.BodyRadius)
        public double ChargeStep = 0.1;          // m the line is walked in
        public double FarHalf = 0.5;             // the far half: this share of the room's farthest place from the door in, or more

        /// <summary>The most lancers in a combat room of this floor: 0 before <see cref="FromFloor"/>, else <see cref="MostInRoom"/> counted from it.</summary>
        public int MostOnFloor(int floor)
        {
            if (floor < FromFloor || MostInRoom == null || MostInRoom.Length == 0) return 0;
            return Math.Max(0, MostInRoom[Math.Min(MostInRoom.Length - 1, floor - FromFloor)]);
        }
    }
}
