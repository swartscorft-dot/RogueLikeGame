using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 023 (2026-11-20), change 3: at most <see cref="LevelValues.BrutesMostInRoom"/> brutes and
    /// <see cref="LevelValues.ShootersMostInRoom"/> shooters in a combat room on any floor (one more of each in an exit
    /// room), the rest swarmers; a room holds as many enemies as before, and its guards and the boss are as before. Over
    /// a thousand draws a floor and kind of room, the same draws with the caps off.
    /// </summary>
    public class RoomMixCapTests
    {
        static int Count(List<EnemyGroup> groups, EnemyKind kind) => groups.Where(g => g.Kind == kind).Sum(g => g.Count);

        [Test]
        public void TheCapsHold_AndTheCountsAreUnchanged_OverAThousandDrawsAFloor()
        {
            var capped = new LevelValues();
            var before = new LevelValues { BrutesMostInRoom = 0, ShootersMostInRoom = 0 };
            Assert.That(capped.BrutesMostInRoom, Is.EqualTo(3));
            Assert.That(capped.ShootersMostInRoom, Is.EqualTo(3));
            Assert.That(capped.ExitRoomKindMore, Is.EqualTo(1));
            for (int floor = 1; floor <= capped.FloorsPerAct; floor++)
            {
                foreach (RoomKind kind in new[] { RoomKind.Combat, RoomKind.Exit })
                {
                    int most = 3 + (kind == RoomKind.Exit ? 1 : 0), changed = 0;
                    for (ulong seed = 1; seed <= 1000; seed++)
                    {
                        List<EnemyGroup> a = LevelGenerator.DrawGroups(kind, floor, LevelRandom.Stream(seed, "mix", floor), capped);
                        List<EnemyGroup> b = LevelGenerator.DrawGroups(kind, floor, LevelRandom.Stream(seed, "mix", floor), before);
                        string where = "floor " + floor + " " + kind + " seed " + seed;
                        Assert.That(Count(a, EnemyKind.Brute), Is.LessThanOrEqualTo(most), where);
                        Assert.That(Count(a, EnemyKind.Shooter), Is.LessThanOrEqualTo(most), where);
                        Assert.That(a.Sum(g => g.Count), Is.EqualTo(b.Sum(g => g.Count)), "as many enemies, " + where);
                        Assert.That(Count(a, EnemyKind.Guard), Is.EqualTo(Count(b, EnemyKind.Guard)), "the guards, " + where);
                        Assert.That(Count(a, EnemyKind.Boss), Is.EqualTo(Count(b, EnemyKind.Boss)), "the boss, " + where);
                        int over = System.Math.Max(0, Count(b, EnemyKind.Brute) - most) + System.Math.Max(0, Count(b, EnemyKind.Shooter) - most);
                        Assert.That(Count(a, EnemyKind.Swarmer), Is.EqualTo(Count(b, EnemyKind.Swarmer) + over), "the rest are swarmers, " + where);
                        if (over > 0) changed++;
                    }
                    if (floor == 1) Assert.That(changed, Is.EqualTo(0), "floor 1 is unchanged: " + kind);
                    TestContext.WriteLine("floor " + floor + " " + kind + ": " + changed + " of 1000 rooms changed by the caps");
                }
            }
        }
    }
}
