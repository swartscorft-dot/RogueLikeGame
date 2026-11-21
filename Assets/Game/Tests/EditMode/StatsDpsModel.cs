using System;
using System.Collections.Generic;
using System.Linq;
using Roguelike.Rules;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 050's measurement at the dummy, as a model of the rules (no engine): her damage per second with the basic
    /// shot alone, and with the whole rotation (the four start skills each used as it comes off cooldown, the basic shot in
    /// every gap), on one target that never moves or dies, over ten minutes. Crits are counted as their expected value. The
    /// same model measures today (the switch off) and this order (on), so the two compare.
    ///
    /// How the skills are played: in the order they come ready (by the list when two do at once); Triple Tap's three
    /// presses each 0.15 s into its window, the last one clean; Backstep's shot 0.15 s into its window, clean, so it spends
    /// the marks. Busy for a skill: from its press to the end of its recovery (the free recovery of decision 0004 is not
    /// used, in both states alike). Marks: as the kit adds and spends them (cap 5, 6 s, every fifth basic hit marks).
    /// </summary>
    public static class StatsDpsModel
    {
        public struct Dps
        {
            public double Basic;
            public double Rotation;
        }

        const double Seconds = 600.0;
        const double PressInWindow = 0.15;

        public static Dps Measure(RunModifiers m, IReadOnlyList<SkillDefinition> skills = null, FeelValues feel = null, KitValues kit = null)
        {
            skills = skills ?? SkillDefaults.Create();
            feel = feel ?? new FeelValues();
            kit = kit ?? new KitValues();
            HerStats stats = m.Stats;
            double interval = stats.BasicShotInterval(feel.BasicShotStartup, feel.BasicShotActive, feel.BasicShotRecovery);
            double share = m.DamageShare;
            double p = stats.CritChance, cd = stats.CritDamage;
            double expected = 1.0 + p * cd; // a hit that may crit, on average
            double sure = 1.0 + cd;          // a crit
            double basicHit = (Placeholders.BasicShotDamage + stats.FlatFor(null)) * share * expected;
            // Work order 065: a clean press and a perfect release times her Clean press, a hit on a marked target times her
            // Marked prey (both 1 at her start and with the switch off).
            double clean = stats.CleanPress, marked = stats.MarkedPrey;

            var r = new Dps { Basic = basicHit / interval };

            List<SkillDefinition> start = skills.Where(s => s != null && s.StartEquipped).ToList();
            var readyAt = new double[start.Count];
            double t = 0.0, dealt = 0.0;
            int marks = 0, basicHits = 0;
            double marksFall = double.NegativeInfinity;
            double lifetime = kit.MarkLifetime + m.MarkLifetimeBonus;

            int MarksNow(double now) => now < marksFall ? marks : 0;
            void AddMarks(int n, double now, string skill)
            {
                if (n <= 0) return;
                int had = MarksNow(now);
                if (had == 0 && skill != null) n += m.FirstMarkBonus(skill);
                marks = Math.Min(kit.MaxMarks, had + n);
                marksFall = now + lifetime;
            }
            int TakeMarks(double now)
            {
                int had = MarksNow(now);
                marks = 0;
                marksFall = double.NegativeInfinity;
                return had;
            }

            while (t < Seconds)
            {
                int use = -1;
                for (int i = 0; i < start.Count; i++)
                    if (readyAt[i] <= t + 1e-9 && (use < 0 || readyAt[i] < readyAt[use] - 1e-9)) use = i;
                if (use < 0)
                {
                    // A basic shot: it goes off after its startup.
                    dealt += basicHit * (MarksNow(t + feel.BasicShotStartup) > 0 ? marked : 1.0);
                    basicHits++;
                    if (basicHits % Math.Max(1, kit.BasicShotsPerMark) == 0) AddMarks(1, t + feel.BasicShotStartup, null);
                    t += interval;
                    continue;
                }
                SkillDefinition s = start[use];
                readyAt[use] = t + s.Cooldown * m.CooldownShare(s.Name);
                int flat = stats.FlatFor(s);
                double busy;
                if (s.RecastCount > 1 && s.OpeningMove)
                {
                    // Backstep: the hop, then the shot pressed clean in its window; a clean spend.
                    double shotAt = t + s.Startup + PressInWindow + s.Startup;
                    double hit = s.Damage + flat;
                    int taken = TakeMarks(shotAt);
                    hit = hit * (1.0 + s.CleanMarkDamageBonus * taken) * (taken > 0 ? marked : 1.0);
                    hit *= stats.On ? sure * clean : 1.0;
                    dealt += hit * share;
                    busy = s.Startup + PressInWindow + s.Startup + s.Recovery;
                }
                else if (s.RecastCount > 1)
                {
                    // Triple Tap: a press each, the last clean (a clean tap).
                    double at = t;
                    for (int shot = 1; shot <= s.RecastCount; shot++)
                    {
                        if (shot > 1) at += PressInWindow;
                        double shotAt = at + s.Startup;
                        bool cleanTap = shot == s.RecastCount && s.CleanTapMarks > 0;
                        double hit = (s.Damage + flat) * (MarksNow(shotAt) > 0 ? marked : 1.0);
                        if (cleanTap) hit *= stats.On ? CritRules.Folded(kit.CleanDamage, m.StatValues) * sure * clean : kit.CleanDamage;
                        else hit *= expected;
                        dealt += hit * share;
                        AddMarks(cleanTap ? s.CleanTapMarks : s.MarksAdded, shotAt, s.Name);
                        at = shotAt;
                    }
                    busy = at - t + s.Recovery;
                }
                else
                {
                    double shotAt = t + s.Startup;
                    double hit = (s.Damage + flat) * (MarksNow(shotAt) > 0 ? marked : 1.0);
                    if (s.MarkDamageBonus > 0f) hit *= 1.0 + s.MarkDamageBonus * TakeMarks(shotAt);
                    dealt += hit * expected * share;
                    AddMarks(s.MarksAdded, shotAt, s.Name);
                    busy = s.Startup + s.Recovery;
                }
                t += busy;
            }
            r.Rotation = dealt / t;
            return r;
        }

        /// <summary>A run's modifiers at its start, the switch on or off, as a run starts them.</summary>
        public static RunModifiers Start(RunValues values)
        {
            var m = new RunModifiers { CooldownFloor = values.CooldownFloor, StatsAndTiers = values.StatsAndTiers, StatValues = values.Stats };
            return m;
        }

        static readonly string[] StartSkills = { "Quick Shot", "Scatter Blast", "Backstep", "Triple Tap" };

        /// <summary>
        /// A run of <paramref name="picks"/> chest picks from this seed: two chests a floor, each one's first card taken (as
        /// the test pilot takes it: the offer's order is drawn, so the first card is an average pick), her hurt.
        /// </summary>
        public static RunModifiers AfterPicks(RunValues values, ulong seed, int picks)
        {
            RunModifiers m = Start(values);
            for (int k = 0; k < picks; k++)
            {
                List<Upgrade> offer = ChestUpgrades.Offer(seed, 1 + k / 2, 2 + 3 * (k % 2), StartSkills, values, m, false);
                if (offer.Count > 0) m.Take(offer[0]);
            }
            return m;
        }

        public struct Spread
        {
            public double Mean, Deviation, Min, Max;
            public override string ToString() =>
                string.Format(System.Globalization.CultureInfo.InvariantCulture, "mean {0:0.0}, sd {1:0.0}, min {2:0.0}, max {3:0.0}", Mean, Deviation, Min, Max);
        }

        public static Spread SpreadOf(IReadOnlyList<double> xs)
        {
            double mean = xs.Average();
            double sd = Math.Sqrt(xs.Sum(x => (x - mean) * (x - mean)) / Math.Max(1, xs.Count));
            return new Spread { Mean = mean, Deviation = sd, Min = xs.Min(), Max = xs.Max() };
        }

        /// <summary>The basic and the rotation's damage per second after six picks, over this many seeded runs.</summary>
        public static (Spread basic, Spread rotation) AfterSixPicks(RunValues values, int runs = 1000)
        {
            var basic = new List<double>();
            var rotation = new List<double>();
            for (int i = 1; i <= runs; i++)
            {
                Dps d = Measure(AfterPicks(values, (ulong)i * 7919UL, 6));
                basic.Add(d.Basic);
                rotation.Add(d.Rotation);
            }
            return (SpreadOf(basic), SpreadOf(rotation));
        }
    }
}
