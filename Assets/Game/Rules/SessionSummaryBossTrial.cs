using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// Work order 147 in the log (section 7.9 of research R6): the trial boss's own lines and the extras on every boss line. On every
    /// line of the boss (its wake, its attacks, its strikes, its dazes) the phase; on its attack and strike lines her speed and her
    /// distance to it as the tell begins and as it strikes; on a dash-only attack's tell the seconds since her last dash press, when
    /// within 10 s. Its own lines: a phase begun, a charge's end (crashed or not), the seeds thrown and each one's end (shot or burst),
    /// the ward (broken or not, its time), Ruin, and once a fight the fight's line with its lengths, her hits and damage by attack, the
    /// share she stood, the share of the floor she used and what killed her.
    /// </summary>
    public static class BossTrialLog
    {
        public const string Fight = "boss_trial", Phase = "boss_phase", ChargeEnd = "boss_charge_end", SeedsThrown = "boss_seeds",
                            Seed = "boss_seed", Ward = "boss_ward", Ruin = "boss_ruin";
        public const string PhaseKey = "phase", HerSpeed = "her_speed", HerDistance = "her_distance", DashBefore = "dash_before";
        public const string HitsPrefix = "hits.", DamagePrefix = "damage.", TellsPrefix = "tells.";
    }

    /// <summary>One trial fight of a session (work order 147), as the boss wrote it when it ended.</summary>
    public sealed class BossTrialRow
    {
        public double Length, One, Two, Three, Stood, Floor, HealthLeft;
        public string Ended = "", KilledBy = "", Template = "";
        public int SeedsShot, SeedsBurst, Crashes, WardsBroken, WardsFailed;
        public readonly List<KeyValuePair<string, int>> Hits = new List<KeyValuePair<string, int>>();
        public readonly List<KeyValuePair<string, int>> Damage = new List<KeyValuePair<string, int>>();
        public readonly List<KeyValuePair<string, int>> Tells = new List<KeyValuePair<string, int>>();
    }

    public static partial class SessionSummary
    {
        /// <summary>The trial fights of a session, in the order they ended; none for a log without them.</summary>
        public static List<BossTrialRow> ComputeBossTrial(IReadOnlyList<SessionEvent> events)
        {
            var rows = new List<BossTrialRow>();
            if (events == null) return rows;
            double Num(SessionEvent e, string key) =>
                double.TryParse(e.ExtraValue(key), NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0d;
            int Whole(string text) => int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v) ? v : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type != BossTrialLog.Fight) continue;
                var r = new BossTrialRow
                {
                    Length = Num(e, "length"), One = Num(e, "one"), Two = Num(e, "two"), Three = Num(e, "three"),
                    Stood = Num(e, "stood"), Floor = Num(e, "floor"), HealthLeft = Num(e, "health_left"),
                    Ended = e.ExtraValue("ended") ?? "", KilledBy = e.ExtraValue("killed_by") ?? "", Template = e.ExtraValue("template") ?? "",
                    SeedsShot = Whole(e.ExtraValue("seeds_shot")), SeedsBurst = Whole(e.ExtraValue("seeds_burst")), Crashes = Whole(e.ExtraValue("crashes")),
                    WardsBroken = Whole(e.ExtraValue("wards_broken")), WardsFailed = Whole(e.ExtraValue("wards_failed")),
                };
                if (e.Extra != null)
                    foreach (KeyValuePair<string, string> x in e.Extra)
                    {
                        if (x.Key.StartsWith(BossTrialLog.HitsPrefix, StringComparison.Ordinal)) r.Hits.Add(new KeyValuePair<string, int>(x.Key.Substring(BossTrialLog.HitsPrefix.Length), Whole(x.Value)));
                        else if (x.Key.StartsWith(BossTrialLog.DamagePrefix, StringComparison.Ordinal)) r.Damage.Add(new KeyValuePair<string, int>(x.Key.Substring(BossTrialLog.DamagePrefix.Length), Whole(x.Value)));
                        else if (x.Key.StartsWith(BossTrialLog.TellsPrefix, StringComparison.Ordinal)) r.Tells.Add(new KeyValuePair<string, int>(x.Key.Substring(BossTrialLog.TellsPrefix.Length), Whole(x.Value)));
                    }
                rows.Add(r);
            }
            return rows;
        }

        /// <summary>Work order 147's block, "Boss (work order 147)": nothing in a log without a trial fight.</summary>
        static void WriteBossTrial(StringBuilder sb, List<BossTrialRow> fights)
        {
            if (fights == null || fights.Count == 0) return;
            sb.AppendLine();
            string S(double s) => Math.Max(0d, s).ToString("0.0", CultureInfo.InvariantCulture) + " s";
            string P(double share) => (share * 100d).ToString("0", CultureInfo.InvariantCulture) + "%";
            var t = new Table("Boss (work order 147), each trial fight", "");
            int n = 0;
            foreach (BossTrialRow f in fights)
            {
                n++;
                t.Add("Fight " + Int(n) + (f.Template.Length > 0 ? " (" + f.Template + ")" : ""),
                      S(f.Length) + ", " + f.Ended + (f.KilledBy.Length > 0 ? " by " + f.KilledBy : "") + (f.Ended == BossFightEvents.Died ? "" : ", its health left " + P(f.HealthLeft)));
                t.Add("  phases 1 / 2 / 3", S(f.One) + " / " + S(f.Two) + " / " + S(f.Three));
                int hits = f.Hits.Sum(h => h.Value), damage = f.Damage.Sum(h => h.Value);
                t.Add("  hits taken, damage", Int(hits) + ", " + Int(damage));
                foreach (KeyValuePair<string, int> h in f.Hits.OrderByDescending(h => h.Value))
                {
                    int d = f.Damage.Where(x => x.Key == h.Key).Sum(x => x.Value);
                    int told = f.Tells.Where(x => x.Key == h.Key).Sum(x => x.Value);
                    t.Add("    " + h.Key, Int(h.Value) + " hits, " + Int(d) + " damage" + (told > 0 ? " (" + Int(told) + " told)" : ""));
                }
                t.Add("  stood (under 35 % of run speed)", P(f.Stood));
                t.Add("  floor used", P(f.Floor));
                t.Add("  seeds shot / burst, crashes, wards broken / failed",
                      Int(f.SeedsShot) + " / " + Int(f.SeedsBurst) + ", " + Int(f.Crashes) + ", " + Int(f.WardsBroken) + " / " + Int(f.WardsFailed));
            }
            var deaths = fights.Where(f => f.Ended == BossFightEvents.HerDeath).GroupBy(f => f.KilledBy.Length > 0 ? f.KilledBy : "?")
                               .OrderByDescending(g => g.Count()).Select(g => g.Key + " " + Int(g.Count())).ToList();
            t.Add("Deaths by attack", deaths.Count > 0 ? string.Join(", ", deaths) : "none");
            t.Write(sb);
        }
    }
}
