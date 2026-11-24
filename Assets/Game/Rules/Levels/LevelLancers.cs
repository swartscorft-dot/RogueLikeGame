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
    /// NOT CALLED BY THE GENERATOR IN THIS ORDER. The sprint's rule 3 (2026-11-24): the generator's version and every pinned floor
    /// fingerprint belong to work orders 103 and 104; with the lancer on by default, a call of <see cref="Put"/> from
    /// <c>LevelGenerator.Generate</c> beside the warders' would change the default floors 4 and 5 (a swarmer each in some rooms
    /// becomes a lancer, and the rooms placed after it move), and with them the pin of
    /// <c>ExitRoomsTests.SwitchOff_ThreeThousandFloors_AreVersion19s_FieldForField</c>, which folds the default floors; and the
    /// generator's own placing puts a group of a kind it does not know in the middle band, not the far half, without a line of
    /// its own. So the floors hold no lancer until the producer puts the two lines into the generator with a version of its own
    /// (the report of work order 108 says which). What is here is whole and tested on the floors' groups as the generator draws
    /// them; the room lab stands one in a room through its own preset (<see cref="RoomLab.Mixes"/>).
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
        /// each alone. (Not called by the validator in this order: see the class.)
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

        /// <summary>The most lancers in a combat room of this floor: 0 before <see cref="FromFloor"/>, else <see cref="MostInRoom"/> counted from it.</summary>
        public int MostOnFloor(int floor)
        {
            if (floor < FromFloor || MostInRoom == null || MostInRoom.Length == 0) return 0;
            return Math.Max(0, MostInRoom[Math.Min(MostInRoom.Length - 1, floor - FromFloor)]);
        }
    }
}
