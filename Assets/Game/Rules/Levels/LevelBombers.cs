using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Roguelike.Rules
{
    /// <summary>
    /// Where bombers stand in the floors (work order 073), with <see cref="LevelValues.Bombers"/> on; built off, and turned on
    /// by work order 087 with the act's plan (<see cref="LevelValues.ActPlan"/>, the generator's version 18), from floor 4:
    /// with either off nothing here is read (<see cref="LevelValues.BombersInFloors"/>).
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
            if (v == null || !v.BombersInFloors || b == null || floor < b.FromFloor || firstCombatRoom) return 0; // work order 087: and the act's plan on
            if (kind != RoomKind.Combat && kind != RoomKind.Exit) return 0;
            int room = b.MostOnFloor(floor);
            return kind == RoomKind.Exit && room > 0 ? room + Math.Max(0, b.MoreInExit) : room;
        }

        /// <summary>
        /// Puts the bombers into a floor's fights (<paramref name="fights"/>: its combat rooms in order and then its exit room,
        /// each as its groups, as the generator lays them into the row), in place. Nothing with the switch off.
        /// </summary>
        public static void Put(List<List<EnemyGroup>> fights, ulong seed, int floor, LevelValues v)
        {
            if (fights == null || v == null || !v.BombersInFloors) return;
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
        // Work order 087 (Kristian, 2026-11-22: the bomber from floor 4, with the shooter's fan; the producer's proposal, his to
        // move after meeting it in a run): floor 4. 2 before (073: she met them first in the practice room, then from floor 2).
        public int FromFloor = 4;
        // The most bombers in a combat room, counted from FromFloor: its first entry for the floor that brings the bomber, the next
        // for the floor after, and so on; past the end its last (work order 087's follow-up, the producer for Kristian, to be
        // corrected by him: at most 1 a room on the floor that introduces it, 2 from the floor after; so floor 4 one, floor 5
        // two). Counted from its first floor so that moving FromFloor moves the caps with it. Before: { 0, 1, 1, 2, 2 } by floor
        // number from 1 (work order 073, written for a bomber from floor 2: one on its first two floors, two after).
        public int[] MostInRoom = { 1, 2 };
        public int MoreInExit = 1;                 // the exit room one more

        /// <summary>The most bombers in a combat room of this floor: 0 before <see cref="FromFloor"/>, else <see cref="MostInRoom"/> counted from it.</summary>
        public int MostOnFloor(int floor)
        {
            if (floor < FromFloor || MostInRoom == null || MostInRoom.Length == 0) return 0;
            return Math.Max(0, MostInRoom[Math.Min(MostInRoom.Length - 1, floor - FromFloor)]);
        }
    }
}
