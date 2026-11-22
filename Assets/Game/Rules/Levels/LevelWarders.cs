using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where warders stand in the floors (work order 079), with <see cref="LevelValues.Warders"/> on; off by default, and then
    /// nothing here is read and every floor is what it was, field for field (no version of the generator is taken: the
    /// producer turns them on with a version of its own after the developer has met the warder in the practice room). Beside
    /// the bombers' rules (<see cref="LevelBombers"/>), and drawn after them.
    ///
    /// From <see cref="WarderFloorValues.FromFloor"/>, in a floor in a line (the default), never in the floor's first combat
    /// room, at most <see cref="WarderFloorValues.MostInRoom"/> (one) in a combat room or the exit room, and only in a room with
    /// at least <see cref="WarderFloorValues.OthersAtLeast"/> other enemies: it shields, and alone it has nothing to shield.
    /// Whether a room that may hold one does is drawn on a stream of its own (<see cref="Stage"/>), once a floor, after the
    /// rooms' groups are known: so a floor with them on holds the very groups it held with them off, but for the swarmer each
    /// takes the place of (of the largest pack, the first of them when two are as large), alone at its own point. The room's
    /// count of enemies is the same; a room with no swarmer gets none. The generator puts it at the far side of its room, as
    /// it does the shooters and the casters. Never an elite, never in a treasure room or a passage. On a text map it is w
    /// (<see cref="EnemyKinds.Letter"/>).
    /// </summary>
    public static class LevelWarders
    {
        /// <summary>The generator's stream for which rooms hold a warder.</summary>
        public const string Stage = "warders";

        /// <summary>
        /// The most warders a room may hold: none with the switch off, before <see cref="WarderFloorValues.FromFloor"/>, in the
        /// floor's first combat room, in a room that is neither a combat room nor the exit room, and in a room with fewer than
        /// <see cref="WarderFloorValues.OthersAtLeast"/> other enemies; else <see cref="WarderFloorValues.MostInRoom"/>.
        /// </summary>
        public static int Most(LevelValues v, RoomKind kind, int floor, bool firstCombatRoom, int others)
        {
            WarderFloorValues w = v?.WarderFloors;
            if (v == null || !v.Warders || w == null || floor < w.FromFloor || firstCombatRoom) return 0;
            if (kind != RoomKind.Combat && kind != RoomKind.Exit) return 0;
            if (others < w.OthersAtLeast) return 0;
            return Math.Max(0, w.MostInRoom);
        }

        /// <summary>
        /// Puts the warders into a floor's fights (<paramref name="fights"/>: its combat rooms in order and then its exit room,
        /// each as its groups), in place. Nothing with the switch off.
        /// </summary>
        public static void Put(List<List<EnemyGroup>> fights, ulong seed, int floor, LevelValues v)
        {
            if (fights == null || v == null || !v.Warders) return;
            LevelRandom rng = LevelRandom.Stream(seed, Stage, floor);
            for (int i = 0; i < fights.Count; i++)
            {
                RoomKind kind = i == fights.Count - 1 ? RoomKind.Exit : RoomKind.Combat;
                // The others it would have: the room's enemies, less the swarmer it takes the place of.
                int most = Most(v, kind, floor, i == 0, Count(fights[i]) - 1);
                if (most <= 0) continue;
                int n = rng.Range(0, most);
                Convert(fights[i], n);
            }
        }

        /// <summary>The enemies of these groups, counted one by one.</summary>
        public static int Count(IEnumerable<EnemyGroup> groups) => groups == null ? 0 : groups.Where(g => g != null).Sum(g => g.Count);

        /// <summary>
        /// Up to <paramref name="warders"/> swarmers of these groups become warders, each alone: from the largest pack (the first
        /// of the largest), a pack of one going with it. Returns how many did.
        /// </summary>
        public static int Convert(List<EnemyGroup> groups, int warders)
        {
            int done = 0;
            for (; done < warders; done++)
            {
                EnemyGroup pack = null;
                foreach (EnemyGroup g in groups)
                    if (g != null && g.Kind == EnemyKind.Swarmer && !g.Elite && !g.Boss && g.Count > 0 && (pack == null || g.Count > pack.Count)) pack = g;
                if (pack == null) break;
                pack.Count--;
                if (pack.Count == 0) groups.Remove(pack);
                groups.Add(new EnemyGroup(EnemyKind.Warder));
            }
            return done;
        }

        /// <summary>
        /// The validator's rules for them: none while the switch is off, none before their floor, none in the floor's first
        /// combat room, none in a room with fewer than <see cref="WarderFloorValues.OthersAtLeast"/> other enemies, no more in a
        /// room than <see cref="Most"/>, none an elite, each alone.
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues v, List<string> faults)
        {
            if (level == null || v == null || faults == null) return;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            LevelRoom first = null;
            foreach (LevelRoom r in level.Rooms)
            {
                List<EnemyGroup> warders = r.EnemyGroups.Where(g => g != null && g.Kind == EnemyKind.Warder).ToList();
                if (warders.Count == 0) continue;
                if (first == null) first = LevelGeometry.MainPath(level).FirstOrDefault(x => x.Kind == RoomKind.Combat);
                int count = warders.Sum(g => g.Count);
                int others = Count(r.EnemyGroups) - count;
                int most = level.InALine ? Most(v, r.Kind, level.Floor, r == first, others) : 0;
                if (count > most)
                    faults.Add($"{Name(r)} holds {count} warders with {others} other enemies, floor {level.Floor} at most {most} in a {r.Kind} room"
                               + (r == first ? " (the floor's first combat room)" : ""));
                foreach (EnemyGroup g in warders)
                {
                    if (g.Elite || g.Boss) faults.Add($"{Name(r)} has a warder as its elite or boss");
                    if (g.Count != 1) faults.Add($"{Name(r)} has {g.Count} warders at one point, and each stands alone");
                }
            }
        }
    }

    /// <summary>The warders' numbers in the floors (work order 079; first guesses, read only with <see cref="LevelValues.Warders"/> on).</summary>
    [Serializable]
    public sealed class WarderFloorValues
    {
        public int FromFloor = 2;       // none on floor 1: she meets it first in the practice room, then from floor 2
        public int MostInRoom = 1;      // at most one a room (work order 079)
        public int OthersAtLeast = 3;   // only in a room with at least three other enemies (work order 079)
    }
}
