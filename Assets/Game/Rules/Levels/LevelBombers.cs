using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where bombers stand in the floors (work order 073), with <see cref="LevelValues.Bombers"/> on; off by default, and
    /// then nothing here is read and every floor is what it was, field for field (no version of the generator is taken: the
    /// producer turns them on with a version of its own after the developer has met the bomber in the practice room).
    ///
    /// From <see cref="BomberFloorValues.FromFloor"/>, in a floor in a line (the default), never in the floor's first combat
    /// room (she comes in to her first fight without one), at most <see cref="BomberFloorValues.MostInRoom"/> of the floor in a
    /// combat room and <see cref="BomberFloorValues.MoreInExit"/> more in the exit room. How many a room holds is drawn on a
    /// stream of its own (<see cref="Stage"/>), from none to that most, once a floor, after the rooms' groups are known: so a
    /// floor with them on holds the very groups it held with them off, but for the swarmers they take the places of. Each
    /// takes the place of one swarmer (of the largest pack, the first of them when two are as large), alone at its own point,
    /// placed among the swarmers' anywhere in the room; a room with no swarmer left gets no more. The room's count of
    /// enemies is the same. Never an elite (<see cref="LevelValues.EliteKinds"/> has none), never in a treasure room or a
    /// passage. On a text map it is x (<see cref="EnemyKinds.Letter"/>).
    /// </summary>
    public static class LevelBombers
    {
        /// <summary>The generator's stream for how many bombers each room holds.</summary>
        public const string Stage = "bombers";

        /// <summary>
        /// The most bombers a room may hold: none with the switch off, before <see cref="BomberFloorValues.FromFloor"/>, in
        /// the floor's first combat room, and in a room that is neither a combat room nor the exit room; else the floor's
        /// most for a combat room, and <see cref="BomberFloorValues.MoreInExit"/> more for the exit room (none more on a floor
        /// with none).
        /// </summary>
        public static int Most(LevelValues v, RoomKind kind, int floor, bool firstCombatRoom)
        {
            BomberFloorValues b = v?.BomberFloors;
            if (v == null || !v.Bombers || b == null || floor < b.FromFloor || firstCombatRoom) return 0;
            if (kind != RoomKind.Combat && kind != RoomKind.Exit) return 0;
            if (b.MostInRoom == null || b.MostInRoom.Length == 0) return 0;
            int room = Math.Max(0, b.MostInRoom[Math.Max(0, Math.Min(b.MostInRoom.Length - 1, floor - 1))]);
            return kind == RoomKind.Exit && room > 0 ? room + Math.Max(0, b.MoreInExit) : room;
        }

        /// <summary>
        /// Puts the bombers into a floor's fights (<paramref name="fights"/>: its combat rooms in order and then its exit room,
        /// each as its groups, as the generator lays them into the row), in place. Nothing with the switch off.
        /// </summary>
        public static void Put(List<List<EnemyGroup>> fights, ulong seed, int floor, LevelValues v)
        {
            if (fights == null || v == null || !v.Bombers) return;
            LevelRandom rng = LevelRandom.Stream(seed, Stage, floor);
            for (int i = 0; i < fights.Count; i++)
            {
                RoomKind kind = i == fights.Count - 1 ? RoomKind.Exit : RoomKind.Combat;
                int most = Most(v, kind, floor, i == 0);
                if (most <= 0) continue;
                int n = rng.Range(0, most);
                Convert(fights[i], n);
            }
        }

        /// <summary>
        /// Up to <paramref name="bombers"/> swarmers of these groups become bombers, each alone: from the largest pack (the
        /// first of the largest), a pack of one going with it. Returns how many did.
        /// </summary>
        public static int Convert(List<EnemyGroup> groups, int bombers)
        {
            int done = 0;
            for (; done < bombers; done++)
            {
                EnemyGroup pack = null;
                foreach (EnemyGroup g in groups)
                    if (g != null && g.Kind == EnemyKind.Swarmer && !g.Elite && !g.Boss && g.Count > 0 && (pack == null || g.Count > pack.Count)) pack = g;
                if (pack == null) break;
                pack.Count--;
                if (pack.Count == 0) groups.Remove(pack);
                groups.Add(new EnemyGroup(EnemyKind.Bomber));
            }
            return done;
        }

        /// <summary>
        /// The validator's rules for them: none while the switch is off, none before their floor and none in the floor's first
        /// combat room, no more in a room than <see cref="Most"/>, none an elite, each alone.
        /// </summary>
        public static void Faults(LevelLayout level, LevelValues v, List<string> faults)
        {
            if (level == null || v == null || faults == null) return;
            string Name(LevelRoom r) => r.Letter + r.Order.ToString(CultureInfo.InvariantCulture);
            LevelRoom first = LevelGeometry.MainPath(level).FirstOrDefault(r => r.Kind == RoomKind.Combat);
            foreach (LevelRoom r in level.Rooms)
            {
                List<EnemyGroup> bombers = r.EnemyGroups.Where(g => g != null && g.Kind == EnemyKind.Bomber).ToList();
                if (bombers.Count == 0) continue;
                int count = bombers.Sum(g => g.Count);
                int most = level.InALine ? Most(v, r.Kind, level.Floor, r == first) : 0;
                if (count > most)
                    faults.Add($"{Name(r)} holds {count} bombers, floor {level.Floor} at most {most} in a {r.Kind} room" + (r == first ? " (the floor's first combat room)" : ""));
                foreach (EnemyGroup g in bombers)
                {
                    if (g.Elite || g.Boss) faults.Add($"{Name(r)} has a bomber as its elite or boss");
                    if (g.Count != 1) faults.Add($"{Name(r)} has {g.Count} bombers at one point, and each stands alone");
                }
            }
        }
    }

    /// <summary>The bombers' numbers in the floors (work order 073; first guesses, read only with <see cref="LevelValues.Bombers"/> on).</summary>
    [Serializable]
    public sealed class BomberFloorValues
    {
        public int FromFloor = 2;                  // none on floor 1: she meets them first in the practice room, then from floor 2
        public int[] MostInRoom = { 0, 1, 1, 2, 2 }; // by floor number, from 1; past the end its last: as the casters'
        public int MoreInExit = 1;                 // the exit room one more
    }
}
