using System.Collections.Generic;

namespace Roguelike.Rules
{
    /// <summary>
    /// The boss alone in its room (work order 130; Kristian, 2026-11-25 at about 18:50, after playing a run to the boss: "bossrommet.
    /// der tenker jeg bare bossen skal være"). With <see cref="LevelValues.BossAlone"/> on (the default; the generator's version 24) the
    /// exit room that holds the act boss, on the act's last floor, holds the boss and nobody else as she enters: no guard, no pack, no
    /// shooter, caster, brute, warder or bomber. The groups are drawn as before (the contents stream, the bombers', the warders' and
    /// the lancers' draws are version 23's) and only then taken out of that room's fight, so every room before it on the floor holds
    /// what it held; the boss stands where it stood (the room's middle), and what is laid round the room's enemies may stand
    /// otherwise. A floor without the act boss is version 23's, field for field. The swarmers the boss calls at two thirds and one third
    /// of its health are the boss's own attack (<see cref="BossValues.CallCount"/>) and are not touched. Only on a floor in one row, the
    /// game's floors (<see cref="LevelValues.InALine"/>); the floors before the line are as they were. Off: every floor is version 23's,
    /// field for field, but for the version.
    /// </summary>
    public static class LevelBossAlone
    {
        /// <summary>Does the rule hold for floors made with these values: its switch on, on a floor in one row.</summary>
        public static bool On(LevelValues v) => v != null && v.BossAlone && v.InALine;

        /// <summary>Does this list of groups hold the act boss?</summary>
        public static bool HoldsTheBoss(List<EnemyGroup> groups)
        {
            if (groups == null) return false;
            for (int i = 0; i < groups.Count; i++)
                if (groups[i] != null && groups[i].Boss) return true;
            return false;
        }

        /// <summary>
        /// Takes every group but the act boss out of the room of the fights that holds it (<paramref name="fights"/>: the floor's
        /// combat rooms in order, then its exit room), with the rule on; nothing otherwise. Returns what left, in the order it stood.
        /// </summary>
        public static List<EnemyGroup> Clear(List<List<EnemyGroup>> fights, LevelValues v)
        {
            var left = new List<EnemyGroup>();
            if (!On(v) || fights == null) return left;
            foreach (List<EnemyGroup> room in fights)
            {
                if (!HoldsTheBoss(room)) continue;
                foreach (EnemyGroup g in room)
                    if (g != null && !g.Boss) left.Add(g);
                room.RemoveAll(g => g == null || !g.Boss);
            }
            return left;
        }

        /// <summary>
        /// Does the validator ask this room to hold the boss alone: the rule on for the floor (its values, and the floor in one row),
        /// and the room the exit room of the act's last floor.
        /// </summary>
        public static bool Asks(LevelLayout level, LevelRoom r, LevelValues v) =>
            level != null && r != null && v != null && v.BossAlone && level.InALine && r.Kind == RoomKind.Exit && level.Floor >= v.FloorsPerAct;
    }
}
