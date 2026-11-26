using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 141's numbers for him ("the benchmark's floor 5 fight and a floor 1 fight with each upgrade alone at each tier and with
    /// the three strongest pairs: damage a second on one target and on a pack, marks made and marks spent a minute, against none"), from
    /// the rules alone, no play: a model of her basic shot only, through the same rules the game reads (LineShot, the five's and the
    /// four's rules, Ricochet's NextJump, marks with their cap and lifetime). Printed for the report; it pins nothing but that the model
    /// runs and that with none held it deals what the shot deals.
    ///
    /// The model, said plainly: her shot at a run's start (40 damage, every 0.225 s at 0.85 of the rate: 3.78 shots a second), her stats
    /// off (no crit, no flat damage), no skills (so no other marks and no spender but Tap Out), no focus window; 4 shots of 5 hit (his 80 %
    /// of 2026-11-25), the same shots in every run of the model (a stream of its own). One target: a body that never dies, 60 s. The pack:
    /// the enemies of the benchmark's room (BENCH032's floor 1 and floor 5, the fullest combat room, not the elite room), standing still
    /// where the generator places them, moved so their middle is 7 m in front of her; she shoots at the nearest living one, her shot
    /// meeting the first on its line; the fight ends when the last dies (at most 180 s). Damage a second on the pack is the damage that
    /// took health over the seconds to clear it. Nobody moves, nothing hits back, nothing stands in cover: it is the shot's arithmetic in
    /// a real room's spacing, not a fight.
    /// </summary>
    public class BasicShotUpgradeNumbersTests
    {
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        const int ShotDamage = Placeholders.BasicShotDamage;      // 40
        const float Interval = 0.225f / 0.85f;                    // s from shot to shot at a run's start (StatValues.StartAttackSpeed)
        const int HitPercent = 80;                                // his hit rate of 2026-11-25
        const float Range = 10f, HalfWidth = 0.5f;                // the feel's
        const float PackAhead = 7f;                               // m from her to the pack's middle
        const float StepInTo = 8f;                                // m she steps in to when the nearest is past her reach (at no cost of time)
        const float OneTargetSeconds = 60f, PackMostSeconds = 180f;
        const int MaxMarks = 5, KitPerMark = 5;
        const float MarkLifetime = 6f;

        sealed class Enemy
        {
            public GroundPoint At;
            public int Health;
            public readonly Marks Marks = new Marks();
            public int Hunt;
            public bool Alive => Health > 0;
        }

        sealed class Result
        {
            public float Seconds;
            public long Damage;
            public int MarksMade, MarksSpent, MarksPassed, Bounces, Heavy;
            public bool Cleared;
            public double Dps => Seconds > 0f ? Damage / (double)Seconds : 0.0;
            public double MadeAMinute => Seconds > 0f ? MarksMade * 60.0 / Seconds : 0.0;
            public double SpentAMinute => Seconds > 0f ? MarksSpent * 60.0 / Seconds : 0.0;
        }

        /// <summary>One fight of the model with these tiers held (kind to tier).</summary>
        static Result Fight(List<(GroundPoint at, int health)> pack, bool oneTarget, Dictionary<BasicShotUpgradeKind, int> tiers, int hitPercent = HitPercent)
        {
            var v = new BasicShotUpgradeValues();
            int T(BasicShotUpgradeKind k) => tiers != null && tiers.TryGetValue(k, out int t) ? t : 0;
            var enemies = oneTarget
                ? new List<Enemy> { new Enemy { At = new GroundPoint(PackAhead, 0f), Health = int.MaxValue / 2 } }
                : pack.Select(p => new Enemy { At = p.at, Health = p.health }).ToList();
            var r = new Result();
            var heat = new HotBarrelHeat();
            var kitMarks = new BasicShotMarks();
            int chamber = 0;
            LevelRandom hits = LevelRandom.Stream(0x141UL, "the model's hits", 0);
            var infos = new List<TargetInfo>();
            var pierced = new List<int>();
            var origin = new GroundPoint(0f, 0f);
            int stepsIn = 0;
            float most = oneTarget ? OneTargetSeconds : PackMostSeconds;
            float now = 0f;

            void Infos()
            {
                infos.Clear();
                foreach (Enemy e in enemies) infos.Add(new TargetInfo(e.At, 0.5f, e.Alive));
            }

            int Marked(Enemy e) => e.Marks.Count(now);

            void AddMarks(Enemy e, int n)
            {
                if (n <= 0 || !e.Alive) return;
                int before = Marked(e);
                int after = e.Marks.Add(n, now, MaxMarks, MarkLifetime);
                r.MarksMade += Math.Max(0, after - before);
            }

            // One hit of the shot on e: Tap Out, the damage, Pass It On on a kill. Returns the damage that took health.
            int Hit(int index, int damage)
            {
                Enemy e = enemies[index];
                int tap = T(BasicShotUpgradeKind.TapOut);
                if (BasicShotRules.TapsOut(Marked(e), tap, MaxMarks, v))
                {
                    int taken = e.Marks.TakeAll(now);
                    r.MarksSpent += taken;
                    damage = MarkRules.SpenderDamage(damage, taken, BasicShotRules.TapOutBonus(tap, v));
                }
                int applied = Math.Min(damage, e.Health);
                e.Health -= applied;
                r.Damage += applied;
                if (!e.Alive)
                {
                    int marks = e.Marks.Clear(now);
                    int pass = T(BasicShotUpgradeKind.PassItOn);
                    if (marks > 0 && BasicShotMoreRules.PassesOn(pass, true, v))
                    {
                        Infos();
                        infos[index] = new TargetInfo(e.At, 0.5f, false);
                        int to = BasicShotMoreRules.PassTo(infos, index, BasicShotMoreRules.PassReach(pass, v), null);
                        if (to >= 0)
                        {
                            int before = Marked(enemies[to]);
                            int after = enemies[to].Marks.Add(marks, now, MaxMarks, MarkLifetime);
                            r.MarksPassed += Math.Max(0, after - before);
                        }
                    }
                }
                return applied;
            }

            // The marks a basic hit on e makes by the count.
            void Count(Enemy e)
            {
                int hunt = T(BasicShotUpgradeKind.HuntersCount);
                if (hunt > 0)
                {
                    int c = e.Hunt;
                    AddMarks(e, BasicShotMoreRules.HunterHit(ref c, hunt, false, v));
                    e.Hunt = c;
                    return;
                }
                if (kitMarks.Hit(BasicShotRules.HitsPerMark(T(BasicShotUpgradeKind.MarkingRounds), KitPerMark, v), false)) AddMarks(e, 1);
            }

            for (int shot = 0; now < most; shot++, now += Interval)
            {
                if (!oneTarget && enemies.All(e => !e.Alive))
                {
                    r.Cleared = true;
                    break;
                }
                bool heavy = BasicShotMoreRules.ChamberShot(ref chamber, BasicShotMoreRules.ShotsPerHeavy(T(BasicShotUpgradeKind.SixthChamber), v));
                if (heavy) r.Heavy++;
                bool lands = hits.Below(100) < hitPercent;
                // Aim: the nearest living enemy's middle.
                int aimAt = -1;
                float best = float.PositiveInfinity;
                for (int i = 0; i < enemies.Count; i++)
                    if (enemies[i].Alive && origin.DistanceTo(enemies[i].At) < best)
                    {
                        best = origin.DistanceTo(enemies[i].At);
                        aimAt = i;
                    }
                if (aimAt < 0) break;
                if (best > Range - 0.5f)
                {
                    // The nearest is past her reach: she steps in along the line to it (the model's stand-in for the pack coming at her).
                    GroundPoint to = enemies[aimAt].At - origin;
                    origin = enemies[aimAt].At - to * (StepInTo / to.Length);
                    stepsIn++;
                }
                if (!lands)
                {
                    heat.Miss(now, v);
                    continue;
                }
                Infos();
                GroundPoint aim = enemies[aimAt].At;
                int first = LineShot.FirstHit(origin, aim, Range, HalfWidth, infos, float.PositiveInfinity);
                if (first < 0)
                {
                    heat.Miss(now, v);
                    continue;
                }
                int damage = ShotDamage;
                int hot = T(BasicShotUpgradeKind.HotBarrel);
                float share = heat.Share(now, hot, v);
                if (hot > 0 && share > 0f) damage = (int)Math.Round(damage * (1f + share));
                if (heavy) damage = BasicShotMoreRules.HeavyDamage(damage, BasicShotMoreRules.HeavyScale(T(BasicShotUpgradeKind.SixthChamber), v));
                int shotDamage = damage;
                int pierceTier = T(BasicShotUpgradeKind.PiercingRound);
                BasicShotRules.PiercedTargets(origin, aim, Range, HalfWidth, infos, float.PositiveInfinity, first,
                                              BasicShotMoreRules.PierceTargets(heavy, pierceTier, v), pierced);
                var shotHit = new List<int> { first };
                Hit(first, damage);
                Count(enemies[first]);
                if (hot > 0) heat.Hit(now, v);
                float pierceShare = BasicShotMoreRules.PierceShare(heavy, pierceTier, v);
                foreach (int p in pierced)
                {
                    if (!enemies[p].Alive) continue;
                    Hit(p, BasicShotRules.PierceDamage(shotDamage, pierceShare));
                    shotHit.Add(p);
                }
                // Chain Kill: from the first the shot killed.
                int chain = T(BasicShotUpgradeKind.ChainKill);
                int from = -1;
                if (chain > 0)
                    foreach (int i in shotHit)
                        if (!enemies[i].Alive)
                        {
                            from = i;
                            break;
                        }
                int bounceDamage = BasicShotMoreRules.BounceDamage(shotDamage, BasicShotMoreRules.BounceShare(chain, v));
                for (int b = 0; from >= 0 && b < BasicShotMoreRules.BounceMost(chain, v); b++)
                {
                    Infos();
                    int next = NewSkillRules.NextJump(infos, from, shotHit, BasicShotMoreRules.BounceReach(chain, v), null);
                    if (next < 0) break;
                    shotHit.Add(next);
                    Hit(next, bounceDamage);
                    Count(enemies[next]);
                    r.Bounces++;
                    if (enemies[next].Alive) break;
                    from = next;
                }
            }
            r.Seconds = Math.Min(now, most);
            return r;
        }

        /// <summary>The benchmark's room of a floor: BENCH032's floor, the fullest combat room (not the elite room), its enemies moved so their middle stands 7 m in front of her.</summary>
        static List<(GroundPoint at, int health)> Room(int floor, out string what)
        {
            ulong run = LevelSeed.TryParse("BENCH032", out ulong s) ? s : 32UL; // BenchmarkStage.RunSeed, as it reads it
            var values = new LevelValues();
            LevelLayout level = LevelGenerator.Generate(RunSeeds.FloorSeed(run, floor), floor, values);
            LevelRoom room = level.Rooms.Where(r => r.Kind == RoomKind.Combat && !r.Elite).OrderByDescending(r => r.EnemyCount).ThenBy(r => r.Order).First();
            var list = new List<(GroundPoint at, int health)>();
            for (int i = 0; i < room.EnemyPoints.Count; i++)
            {
                EnemyGroup g = room.EnemyGroups[i];
                int health = g.Elite ? values.EliteHealthOf(g.Kind) : values.HealthOf(g.Kind, g.Boss);
                foreach (LevelPoint p in room.PlacesAt(i)) list.Add((new GroundPoint((float)p.X, (float)p.Y), health));
            }
            float mx = list.Average(e => e.at.X), mz = list.Average(e => e.at.Z);
            what = "room " + room.Order + ": " + string.Join(", ", room.EnemyGroups.Select(g => g.ToString())) + " (" + list.Count + " enemies, " + list.Sum(e => e.health) + " health)";
            return list.Select(e => (new GroundPoint(e.at.X - mx + PackAhead, e.at.Z - mz), e.health)).ToList();
        }

        static string Pct(double now, double before) => before <= 0 ? "-" : ((now / before - 1.0) * 100.0).ToString("+0;-0;0", Inv) + " %";

        [Test, Timeout(600000)]
        public void TheNumbers_EachUpgradeAloneAtEachTier_AndTheThreeStrongestPairs_OnFloor1AndFloor5()
        {
            var sb = new StringBuilder();
            var floors = new[] { 1, 5 };
            var rooms = new Dictionary<int, List<(GroundPoint, int)>>();
            foreach (int f in floors)
            {
                rooms[f] = Room(f, out string what);
                sb.AppendLine("[141] floor " + f + " (BENCH032), " + what);
            }
            var none = new Dictionary<int, (Result one, Result pack)>();
            foreach (int f in floors) none[f] = (Fight(rooms[f], true, null), Fight(rooms[f], false, null));
            Result noneAllHit = Fight(rooms[1], true, null, 100); // one target with every shot hitting, as work order 138 reckoned Hot Barrel's +60 %
            Assert.That(none[1].one.Dps, Is.EqualTo(ShotDamage / Interval * HitPercent / 100.0).Within(ShotDamage / Interval * 0.05), "none held: the shot's own 40 at 3.78 a second, 80 % hitting");

            string Line(string name, Dictionary<BasicShotUpgradeKind, int> tiers)
            {
                var cells = new List<string> { name };
                foreach (int f in floors)
                {
                    Result one = Fight(rooms[f], true, tiers), pack = Fight(rooms[f], false, tiers), allHit = Fight(rooms[f], true, tiers, 100);
                    cells.Add(string.Format(Inv, "{0:0} ({1}); all hitting {2:0} ({3})", one.Dps, Pct(one.Dps, none[f].one.Dps), allHit.Dps, Pct(allHit.Dps, noneAllHit.Dps)));
                    cells.Add(string.Format(Inv, "{0:0} ({1}) in {2:0.#} s{3}", pack.Dps, Pct(pack.Dps, none[f].pack.Dps), pack.Seconds, pack.Cleared ? "" : " NOT CLEARED"));
                    cells.Add(string.Format(Inv, "{0:0.#} / {1:0.#}", one.MadeAMinute, one.SpentAMinute));
                    cells.Add(string.Format(Inv, "{0:0.#} / {1:0.#}{2}", pack.MadeAMinute, pack.SpentAMinute,
                                            pack.MarksPassed > 0 ? string.Format(Inv, ", {0} passed", pack.MarksPassed) : ""));
                }
                return "| " + string.Join(" | ", cells) + " |";
            }

            sb.AppendLine("[141] | held | F1 one target dps | F1 pack dps | F1 one marks made/spent a min | F1 pack marks made/spent a min | F5 one target dps | F5 pack dps | F5 one marks | F5 pack marks |");
            sb.AppendLine("[141] " + Line("none", null));
            foreach (BasicShotUpgradeKind k in RunBasicShotUpgrades.All)
                for (int t = 1; t <= 3; t++)
                    sb.AppendLine("[141] " + Line(BasicShotTexts.Short(k, t), new Dictionary<BasicShotUpgradeKind, int> { { k, t } }));

            // The three strongest pairs at tier III: by the mean of the four damage-a-second gains (one target and pack, floors 1 and 5).
            var pairs = new List<(double score, BasicShotUpgradeKind a, BasicShotUpgradeKind b)>();
            BasicShotUpgradeKind[] all = RunBasicShotUpgrades.All;
            for (int i = 0; i < all.Length; i++)
                for (int j = i + 1; j < all.Length; j++)
                {
                    if (BasicShotMoreRules.Excluded(all[j], Held(all[i]))) continue;
                    var tiers = new Dictionary<BasicShotUpgradeKind, int> { { all[i], 3 }, { all[j], 3 } };
                    double score = 0;
                    foreach (int f in floors)
                    {
                        score += Fight(rooms[f], true, tiers).Dps / none[f].one.Dps;
                        score += Fight(rooms[f], false, tiers).Dps / none[f].pack.Dps;
                    }
                    pairs.Add((score / 4.0, all[i], all[j]));
                }
            foreach ((double score, BasicShotUpgradeKind a, BasicShotUpgradeKind b) in pairs.OrderByDescending(p => p.score).Take(3))
                sb.AppendLine("[141] " + Line(BasicShotTexts.Short(a, 3) + " + " + BasicShotTexts.Short(b, 3),
                                              new Dictionary<BasicShotUpgradeKind, int> { { a, 3 }, { b, 3 } }));
            // And the marks' pairs, whose worth is in the marks (a spender's), not in the shot's own damage.
            foreach ((BasicShotUpgradeKind a, BasicShotUpgradeKind b) in new[]
                     {
                         (BasicShotUpgradeKind.HuntersCount, BasicShotUpgradeKind.TapOut), (BasicShotUpgradeKind.PassItOn, BasicShotUpgradeKind.TapOut),
                         (BasicShotUpgradeKind.ChainKill, BasicShotUpgradeKind.PassItOn),
                     })
                sb.AppendLine("[141] " + Line(BasicShotTexts.Short(a, 3) + " + " + BasicShotTexts.Short(b, 3),
                                              new Dictionary<BasicShotUpgradeKind, int> { { a, 3 }, { b, 3 } }));
            TestContext.WriteLine(sb.ToString());
        }

        static RunBasicShotUpgrades Held(BasicShotUpgradeKind k)
        {
            var h = new RunBasicShotUpgrades();
            h.Take(k);
            return h;
        }
    }
}
