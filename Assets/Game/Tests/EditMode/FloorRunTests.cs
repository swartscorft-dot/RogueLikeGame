using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// The run's rules (work order 012, decision 0008): the enemies of a floor by its number over a thousand seeds each,
    /// the floor seeds a run seed gives, the floor reward's offer, the chests' upgrades and the run's modifiers, health
    /// that a run can raise, and the summary's block per run.
    /// </summary>
    public class FloorRunTests
    {
        // ---------------- The enemies, per floor number ----------------

        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        [TestCase(4)]
        [TestCase(5)]
        public void Enemies_OfAThousandSeeds_KeepTheFloorsNumbersAndMixes(int floor)
        {
            var v = new LevelValues();
            int rooms = 0, shootersFarther = 0, roomsWithShootersAndOthers = 0;
            var counts = new SortedDictionary<int, int>();
            for (ulong s = 0; s < 1000; s++)
            {
                LevelLayout l = LevelGenerator.Generate(s + 7000UL, floor, v);
                foreach (LevelRoom r in l.Rooms.Where(x => x.Kind == RoomKind.Combat || x.Kind == RoomKind.Exit))
                {
                    string where = "seed " + (s + 7000UL) + " floor " + floor + " room " + r.Order;
                    // The elite of the elite room comes besides the room's own (work order 017): counted apart.
                    List<EnemyGroup> others = r.EnemyGroups.Where(g => !g.Boss && !g.Elite).ToList();
                    int n = others.Sum(g => g.Count);
                    v.EnemyRange(r.Kind, floor, out int fewest, out int most);
                    Assert.That(n, Is.InRange(fewest, most), where);
                    Assert.That(others.Select(g => g.Kind).Distinct().Count(), Is.GreaterThanOrEqualTo(2), "at least two kinds: " + where);
                    int guards = r.EnemyGroups.Where(g => g.Kind == EnemyKind.Guard && !g.Elite).Sum(g => g.Count);
                    if (floor == 1) Assert.That(guards, Is.EqualTo(0), "no guard on floor 1: " + where);
                    Assert.That(guards, Is.LessThanOrEqualTo(r.Kind == RoomKind.Exit ? 2 : 1), where);
                    if (floor == 1)
                        Assert.That(others.Where(g => g.Kind == EnemyKind.Swarmer).Sum(g => g.Count), Is.GreaterThanOrEqualTo(n / 2), "floor 1 mostly swarmers: " + where);
                    Assert.That(r.EnemyGroups.Count(g => g.Boss), Is.EqualTo(r.Kind == RoomKind.Exit && floor == 5 ? 1 : 0), where);
                    if (r.Kind == RoomKind.Combat)
                    {
                        rooms++;
                        counts[n] = counts.TryGetValue(n, out int c) ? c + 1 : 1;
                    }

                    // Shooters at the far side from the door in, as a rule: further than the room's other groups on the mean.
                    LevelDoor entry = LevelGeometry.EntryDoor(l, r);
                    var shooters = new List<double>();
                    var rest = new List<double>();
                    for (int i = 0; i < r.EnemyPoints.Count; i++)
                    {
                        double d = LevelGeometry.DistanceToDoor(r.EnemyPoints[i], entry, l.CellSize);
                        if (r.EnemyGroups[i].Kind == EnemyKind.Shooter) shooters.Add(d);
                        else if (!r.EnemyGroups[i].Boss) rest.Add(d);
                    }
                    if (shooters.Count > 0 && rest.Count > 0)
                    {
                        roomsWithShootersAndOthers++;
                        if (shooters.Average() > rest.Average()) shootersFarther++;
                    }
                }
            }
            TestContext.WriteLine("ENEMIES floor " + floor + ": combat rooms by enemies " + string.Join(", ", counts.Select(p => p.Key + ": " + p.Value))
                                  + "; shooters farther than the rest in " + shootersFarther + " of " + roomsWithShootersAndOthers + " rooms");
            Assert.That(shootersFarther, Is.GreaterThan(roomsWithShootersAndOthers * 7 / 10), "shooters stand at the far side");
        }

        [Test]
        public void Shares_GiveEveryKindWithAShareOne_AndAddUp()
        {
            for (int total = 2; total < 25; total++)
            {
                int[] c = LevelGenerator.Shares(total, new[] { 0.7f, 0.15f, 0.15f });
                Assert.That(c.Sum(), Is.EqualTo(total));
                if (total >= 3) Assert.That(c.All(x => x >= 1), "total " + total);
            }
            Assert.That(LevelGenerator.Shares(10, new[] { 0.5f, 0f, 0.5f })[1], Is.EqualTo(0));
        }

        // ---------------- Seeds ----------------

        [Test]
        public void FloorSeeds_OfARunSeed_AreStable_EightCharacters_AndDifferPerFloor()
        {
            for (ulong e = 0; e < 200; e++)
            {
                ulong run = LevelSeed.Fresh(e * 104729UL);
                var seeds = new HashSet<ulong>();
                for (int f = 1; f <= 5; f++)
                {
                    ulong s = RunSeeds.FloorSeed(run, f);
                    Assert.That(RunSeeds.FloorSeed(run, f), Is.EqualTo(s), "the same twice");
                    Assert.That(LevelSeed.ToText(s).Length, Is.EqualTo(8));
                    seeds.Add(s);
                }
                Assert.That(seeds.Count, Is.EqualTo(5), "five floors, five seeds");
                Assert.That(RunSeeds.FloorSeed(run + 1, 1), Is.Not.EqualTo(RunSeeds.FloorSeed(run, 1)));
            }
        }

        // ---------------- The floor reward ----------------

        static readonly bool[] StartFour = { true, false, false, false, true, false, true, true };

        [Test]
        public void RewardOffer_IsTheSameForTheSameRunSeed_AndNeverASkillSheHas()
        {
            var offers = new HashSet<string>();
            for (ulong run = 0; run < 300; run++)
            {
                for (int floor = 1; floor <= 4; floor++)
                {
                    List<int> offer = FloorRewards.Offer(run, floor, StartFour, 3);
                    Assert.That(FloorRewards.Offer(run, floor, StartFour, 3), Is.EqualTo(offer), "the same run, the same offer");
                    Assert.That(offer.Count, Is.EqualTo(3));
                    Assert.That(offer.Distinct().Count(), Is.EqualTo(3));
                    Assert.That(offer.All(slot => !StartFour[slot]), "never one she has: " + string.Join(",", offer));
                    offers.Add(string.Join(",", offer));
                }
            }
            Assert.That(offers.Count, Is.GreaterThan(10), "the offer is drawn");

            // Fewer left than three: all that are left; none left: nothing.
            var allButOne = new[] { true, true, true, true, true, true, true, false };
            Assert.That(FloorRewards.Offer(5, 4, allButOne, 3), Is.EqualTo(new[] { 7 }));
            Assert.That(FloorRewards.Offer(5, 4, Enumerable.Repeat(true, 8).ToArray(), 3), Is.Empty);
        }

        [Test]
        public void EverySkill_HasALine_AndAMarkRole()
        {
            foreach (SkillDefinition s in SkillDefaults.Create())
            {
                Assert.That(SkillDefaults.Line(s.Name), Is.Not.Empty, s.Name);
                Assert.That(SkillDefaults.MarkRole(s), Is.Not.Empty, s.Name);
            }
            SkillDefinition[] all = SkillDefaults.Create();
            Assert.That(SkillDefaults.MarkRole(all[0]), Is.EqualTo("builds marks"), "Quick Shot");
            Assert.That(SkillDefaults.MarkRole(all[4]), Is.EqualTo("spends marks"), "Scatter Blast");
            Assert.That(SkillDefaults.MarkRole(all[1]), Is.EqualTo("no marks"), "Powder Keg");
        }

        // ---------------- Chests and the run's modifiers ----------------

        [Test]
        public void ChestOffer_IsTwoDifferentUpgrades_TheSameForTheSameRun_AndNamesASkillSheHas()
        {
            var v = new RunValues();
            var equipped = new List<string> { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };
            var kinds = new HashSet<UpgradeKind>();
            for (ulong run = 0; run < 200; run++)
            {
                List<Upgrade> offer = ChestUpgrades.Offer(run, 2, 5, equipped, v);
                Assert.That(offer.Count, Is.EqualTo(2));
                Assert.That(offer.Select(u => u.Kind).Distinct().Count(), Is.EqualTo(2), "two different rows");
                Assert.That(ChestUpgrades.Offer(run, 2, 5, equipped, v).Select(u => u.Short), Is.EqualTo(offer.Select(u => u.Short)));
                foreach (Upgrade u in offer.Where(u => u.Kind == UpgradeKind.Cooldown)) Assert.That(equipped, Does.Contain(u.Skill));
                foreach (Upgrade u in offer)
                {
                    kinds.Add(u.Kind);
                    Assert.That(u.Text, Is.Not.Empty);
                    Assert.That(u.Short, Is.Not.Empty);
                }
            }
            Assert.That(kinds.Count, Is.EqualTo(6), "every row comes up"); // 5 until work order 017 added the dash's step
        }

        [Test]
        public void RunModifiers_ApplyEachKind_StackOverARun_AndClear()
        {
            var m = new RunModifiers();
            Assert.That(m.BonusMaxHealth, Is.EqualTo(0));
            Assert.That(m.ScaleDamage(100), Is.EqualTo(100));
            Assert.That(m.CooldownShare("Quick Shot"), Is.EqualTo(1f));
            Assert.That(m.RunSpeedShare, Is.EqualTo(1f));
            Assert.That(m.FirstMarkBonus("Quick Shot"), Is.EqualTo(0));

            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10, Heal = 10 });
            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 10, Heal = 10 });
            m.Take(new Upgrade { Kind = UpgradeKind.Damage, Amount = 0.05f });
            m.Take(new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.05f, Skill = "Backstep" });
            m.Take(new Upgrade { Kind = UpgradeKind.RunSpeed, Amount = 0.05f });
            m.Take(new Upgrade { Kind = UpgradeKind.FirstMark, Amount = 1, Skill = "Quick Shot" });

            Assert.That(m.BonusMaxHealth, Is.EqualTo(20), "two +10s stack");
            Assert.That(m.ScaleDamage(100), Is.EqualTo(105));
            Assert.That(m.ScaleDamage(220), Is.EqualTo(231));
            Assert.That(m.CooldownShare("Backstep"), Is.EqualTo(0.95f).Within(1e-6f));
            Assert.That(m.CooldownShare("Quick Shot"), Is.EqualTo(1f), "only the named skill");
            Assert.That(m.RunSpeedShare, Is.EqualTo(1.05f).Within(1e-6f));
            Assert.That(m.FirstMarkBonus("Quick Shot"), Is.EqualTo(1));
            Assert.That(m.FirstMarkBonus("Triple Tap"), Is.EqualTo(0));
            Assert.That(m.ShortText(), Is.EqualTo("+10 hp x2, +5% dmg, -5% cd Backstep, +5% speed, +1 first mark Quick Shot"));

            for (int i = 0; i < 20; i++) m.Take(new Upgrade { Kind = UpgradeKind.Cooldown, Amount = 0.05f, Skill = "Backstep" });
            Assert.That(m.CooldownShare("Backstep"), Is.EqualTo(m.CooldownFloor), "never under the floor");

            m.Clear();
            Assert.That(m.Count, Is.EqualTo(0));
            Assert.That(m.BonusMaxHealth, Is.EqualTo(0));
            Assert.That(m.ScaleDamage(100), Is.EqualTo(100));
            Assert.That(m.CooldownShare("Backstep"), Is.EqualTo(1f));
            Assert.That(m.RunSpeedShare, Is.EqualTo(1f));
            Assert.That(m.FirstMarkBonus("Quick Shot"), Is.EqualTo(0));
            Assert.That(m.ShortText(), Is.EqualTo("none"));
        }

        [Test]
        public void Health_CanBeRaised_Healed_AndSet()
        {
            var h = new Health(100);
            h.TakeDamage(40);
            h.SetMax(110);
            Assert.That((h.Current, h.Max), Is.EqualTo((60, 110)), "what she has is kept");
            Assert.That(h.Heal(10), Is.EqualTo(10));
            Assert.That(h.Heal(1000), Is.EqualTo(40), "never over the maximum");
            h.SetMax(100);
            Assert.That(h.Current, Is.EqualTo(100), "cut to a lower maximum");
            h.Set(30);
            Assert.That(h.Current, Is.EqualTo(30));
        }

        // ---------------- The summary (part 6) ----------------

        static SessionEvent Ev(double t, string type, string name = null, string result = null, params string[] extras)
        {
            var e = new SessionEvent(t, type, name) { Result = result };
            for (int i = 0; i + 1 < extras.Length; i += 2) e.AddExtra(extras[i], extras[i + 1]);
            return e;
        }

        [Test]
        public void Summary_HasABlockPerRun()
        {
            var events = new List<SessionEvent>
            {
                Ev(0, SessionEventTypes.SessionStart),
                Ev(1, SessionEventTypes.RunStart, "ABCDEFGH"),
                Ev(1, SessionEventTypes.FloorEnter, "22222222", null, "floor", "1"),
                Ev(61, SessionEventTypes.FloorFinish, null, null, "floor", "1", "seconds", "60", "health", "80"),
                Ev(61, SessionEventTypes.RewardOffer, null, null, "floor", "1", "offered", "Long Shot | Fan Fire | Powder Keg"),
                Ev(61, SessionEventTypes.RewardTake, "Long Shot", null, "floor", "1"),
                Ev(61, SessionEventTypes.FloorEnter, "33333333", null, "floor", "2"),
                Ev(70, SessionEventTypes.ChestOpen, "+10 hp", null, "floor", "2", "room", "3", "offered", "+10 hp | +5% dmg"),
                Ev(100, SessionEventTypes.RunEnd, null, "death", "floor", "2", "room", "4", "kind", "combat", "seconds", "99"),
                Ev(110, SessionEventTypes.RunStart, "JKLMNPQR"),
                Ev(111, SessionEventTypes.FloorEnter, "44444444", null, "floor", "1"),
                Ev(120, SessionEventTypes.SessionEnd),
            };
            SessionStats stats = SessionSummary.Compute(events);
            Assert.That(stats.Runs.Count, Is.EqualTo(2));
            RunRow first = stats.Runs[0];
            Assert.That(first.Seed, Is.EqualTo("ABCDEFGH"));
            Assert.That(first.FloorsReached, Is.EqualTo(2));
            Assert.That(first.FloorSeconds[1], Is.EqualTo(60f));
            Assert.That(first.Skills, Is.EqualTo(new[] { "Long Shot" }));
            Assert.That(first.Upgrades, Is.EqualTo(new[] { "+10 hp" }));
            Assert.That(first.Ended, Is.EqualTo("death on floor 2, room 4 (combat) after 1:39.0"));
            Assert.That(stats.Runs[1].Ended, Is.EqualTo("still going"));

            string text = SessionSummary.Format(stats);
            StringAssert.Contains("Run ABCDEFGH   floors reached 2   ended: death on floor 2, room 4 (combat) after 1:39.0", text);
            StringAssert.Contains("  time per floor   1: 1:00.0", text);
            StringAssert.Contains("  skills taken     Long Shot", text);
            StringAssert.Contains("  upgrades taken   +10 hp", text);
            StringAssert.Contains("Run JKLMNPQR   floors reached 1   ended: still going", text);
        }
    }
}
