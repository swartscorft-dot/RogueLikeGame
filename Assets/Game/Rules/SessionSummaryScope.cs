using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The blocks of the tempo, the per-action tables, what follows what, the focus gauge, the held and charged skills and the
    /// defence in the session summary, for the whole session and in fights: what is counted (work order 081 moved it here from
    /// SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
        static ScopeStats ComputeScope(string label, List<SessionEvent> all, List<KeyValuePair<double, double>> spans, Kit kit)
        {
            var s = new ScopeStats { Label = label };
            s.Seconds = (float)spans.Sum(p => Math.Max(0d, p.Value - p.Key));
            bool In(SessionEvent e) => spans.Any(p => e.T >= p.Key && e.T <= p.Value);
            List<SessionEvent> events = all.Where(In).ToList();

            // Started actions, per span, in order.
            var startedPerSpan = new List<List<KeyValuePair<float, string>>>();
            var pressesPerSpan = new List<List<float>>();
            foreach (KeyValuePair<double, double> span in spans)
            {
                List<SessionEvent> inSpan = all.Where(e => e.T >= span.Key && e.T <= span.Value).ToList();
                startedPerSpan.Add(inSpan.Where(IsStart).Select(e => new KeyValuePair<float, string>((float)e.T, ActionName(e))).ToList());
                pressesPerSpan.Add(inSpan.Where(IsPress).Select(e => (float)e.T).ToList());
            }
            s.StartedActions = startedPerSpan.Sum(l => l.Count);
            s.Presses = pressesPerSpan.Sum(l => l.Count);
            if (s.Seconds > 0f)
            {
                s.Apm = s.StartedActions / (s.Seconds / 60f);
                s.ApmWithRejected = s.Presses / (s.Seconds / 60f);
            }
            // The busiest ten seconds of any span; a span shorter than that is taken whole.
            for (int i = 0; i < spans.Count; i++)
            {
                float window = (float)Math.Min(BusiestWindow, spans[i].Value - spans[i].Key);
                if (window <= 0f) continue;
                s.BusiestApm = Math.Max(s.BusiestApm, Busiest(startedPerSpan[i].Select(p => p.Key).ToList()) * 60f / window);
                s.BusiestApmWithRejected = Math.Max(s.BusiestApmWithRejected, Busiest(pressesPerSpan[i]) * 60f / window);
            }

            // The rows.
            s.TotalDamage = events.Where(e => e.Type == SessionEventTypes.Hit).Sum(e => e.Damage ?? 0);
            foreach (SessionEvent skill in kit.Skills)
            {
                string name = skill.Name;
                var row = new ActionRow
                {
                    Name = name,
                    Input = skill.Input ?? "",
                    Cooldown = skill.Cooldown ?? 0f,
                    Pressed = events.Count(e => e.Type == SessionEventTypes.SkillPress && e.Name == name),
                    Started = events.Count(e => e.Type == SessionEventTypes.SkillStart && e.Name == name),
                    RejectedOnCooldown = events.Count(e => e.Type == SessionEventTypes.SkillPress && e.Name == name
                                                           && e.Result == SessionWords.Rejected && e.Reason == SessionWords.Cooldown),
                    Hits = events.Count(e => e.Type == SessionEventTypes.SkillFired && e.Name == name && e.Hit == true),
                    Misses = events.Count(e => e.Type == SessionEventTypes.SkillFired && e.Name == name && e.Hit != true),
                };
                FillDamageAndUse(row, events, spans, s.TotalDamage);
                // A recast shot starts the skill again but spends no cooldown (decision 0005): the cooldown use counts
                // only the first press of each chain.
                int recasts = events.Count(e => e.Type == SessionEventTypes.RecastPress && e.Name == name);
                if (recasts > 0 && row.Started > 0 && !float.IsNaN(row.CooldownUse))
                    row.CooldownUse *= Math.Max(0, row.Started - recasts) / (float)row.Started;
                s.Rows.Add(row);
                // A skill that was not on the bar could not be used: it is not counted as never used.
                if (row.Started == 0 && skill.ExtraValue("equipped") != "false") s.NeverUsed.Add(name);
            }
            var shot = new ActionRow
            {
                Name = SessionWords.BasicShot,
                Cooldown = kit.ShotTime,
                Pressed = events.Count(e => e.Type == SessionEventTypes.Click),
                Started = events.Count(e => e.Type == SessionEventTypes.ShotStart),
                Hits = events.Count(e => e.Type == SessionEventTypes.ShotFired && e.Hit == true),
                Misses = events.Count(e => e.Type == SessionEventTypes.ShotFired && e.Hit != true),
            };
            FillDamageAndUse(shot, events, spans, s.TotalDamage);
            ShotUseByInterval(shot, all, spans, kit.ShotTime); // work order 055: against her real interval, when the log has it
            s.Rows.Add(shot);
            var dash = new ActionRow
            {
                Name = SessionWords.Dash,
                Cooldown = kit.DashCooldown,
                Pressed = events.Count(e => e.Type == SessionEventTypes.DashPress),
                Started = events.Count(e => e.Type == SessionEventTypes.DashStart),
                RejectedOnCooldown = events.Count(e => e.Type == SessionEventTypes.DashPress && e.Result == SessionWords.Rejected
                                                       && e.Reason == SessionWords.Cooldown),
            };
            FillDamageAndUse(dash, events, spans, s.TotalDamage);
            s.Rows.Add(dash);

            // The gaps and the pairs, never across two spans. A gap is from one start to the next: the rhythm. The long
            // ones are counted on the time she was free in it: a skill is busy until it lets her go, so holding Fan
            // Fire or casting Heavy Volley is not time spent doing nothing.
            var gaps = new List<float>();
            var pairs = new Dictionary<string, int>();
            var pairOrder = new List<string>();
            List<SessionEvent> skillEnds = all.Where(e => e.Type == SessionEventTypes.SkillEnd).ToList();
            foreach (List<KeyValuePair<float, string>> started in startedPerSpan)
            {
                for (int i = 1; i < started.Count; i++)
                {
                    float from = started[i - 1].Key, to = started[i].Key;
                    float gap = to - from;
                    gaps.Add(gap);
                    string earlier = started[i - 1].Value;
                    SessionEvent end = skillEnds.FirstOrDefault(e => e.Name == earlier && e.T >= from - 1e-4 && e.T <= to + 1e-4);
                    float free = end != null ? Math.Max(0f, to - (float)end.T) : gap;
                    if (free > 1f) { s.GapsOver1++; s.GapsOver1Seconds += free; }
                    if (free > 2f) { s.GapsOver2++; s.GapsOver2Seconds += free; }
                    string pair = started[i - 1].Value + " then " + started[i].Value;
                    if (!pairs.ContainsKey(pair)) { pairs[pair] = 0; pairOrder.Add(pair); }
                    pairs[pair]++;
                }
            }
            s.GapMedian = Percentile(gaps, 0.5f);
            s.GapP90 = Percentile(gaps, 0.9f);
            // Most common first; ties in the order they first happened.
            foreach (string pair in pairOrder.OrderByDescending(p => pairs[p]).Take(PairsShown))
                s.Pairs.Add(new KeyValuePair<string, int>(pair, pairs[pair]));

            // The gauge. Each fill and each wait is paired over the whole session, and counted in the scope it ends in.
            var fills = new List<float>();
            var fullWaits = new List<float>();
            double fillFrom = 0d;
            double fullAt = -1d;
            foreach (SessionEvent e in all)
            {
                if (e.Type == SessionEventTypes.FocusFull)
                {
                    if (In(e)) { s.FocusFilled++; fills.Add((float)(e.T - fillFrom)); }
                    fullAt = e.T;
                }
                else if (e.Type == SessionEventTypes.FocusSpent)
                {
                    if (In(e))
                    {
                        s.FocusSpent++;
                        if (fullAt >= 0d) fullWaits.Add((float)(e.T - fullAt));
                    }
                    fullAt = -1d;
                }
                else if (e.Type == SessionEventTypes.EmpoweredEnd)
                {
                    fillFrom = e.T; // no focus is gained while empowered: the next fill starts here
                }
            }
            if (fills.Count > 0) s.FillSecondsMean = fills.Average();
            if (fullWaits.Count > 0) s.FullBeforeSpentMean = fullWaits.Average();
            s.EmpoweredDamage = events.Where(e => e.Type == SessionEventTypes.Hit && e.Empowered == true).Sum(e => e.Damage ?? 0);

            // Holding and Charge skills.
            foreach (SessionEvent skill in kit.Skills)
            {
                string input = skill.Input ?? "";
                if (input != "Holding" && input != "Charge") continue;
                List<SessionEvent> ends = events.Where(e => e.Type == SessionEventTypes.SkillEnd && e.Name == skill.Name).ToList();
                var row = new HoldRow { Name = skill.Name, Input = input, Uses = ends.Count };
                row.HeldMedian = Percentile(ends.Where(e => e.Held.HasValue).Select(e => e.Held.Value).ToList(), 0.5f);
                List<SessionEvent> fired = ends.Where(e => e.Hit == true).ToList();
                row.Fired = fired.Count;
                if (input == "Charge")
                {
                    List<float> charges = fired.Where(e => e.Charge.HasValue).Select(e => e.Charge.Value).ToList();
                    row.ChargeMedian = Percentile(charges, 0.5f);
                    row.AtLowestCharge = charges.Count(c => c < LowestCharge);
                }
                s.Holds.Add(row);
            }

            // Defence.
            s.EnemyAttacks = events.Count(e => e.Type == SessionEventTypes.EnemyAttack);
            s.StrikesLanded = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.Landed);
            s.StrikesOutOfArea = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.OutOfArea);
            s.StrikesAvoidedByDash = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.AvoidedByDash);
            s.DamageTaken = events.Where(e => e.Type == SessionEventTypes.PlayerDamaged).Sum(e => e.Damage ?? 0);
            List<SessionEvent> dashes = events.Where(e => e.Type == SessionEventTypes.DashStart).ToList();
            List<SessionEvent> avoided = all.Where(e => e.Type == SessionEventTypes.EnemyStrike
                                                        && (e.Result == SessionWords.OutOfArea || e.Result == SessionWords.AvoidedByDash)).ToList();
            s.Dashes = dashes.Count;
            s.DashesThatAvoided = dashes.Count(d => avoided.Any(a => a.T >= d.T && a.T <= d.T + DashAvoidWindow));

            // Movement.
            List<SessionEvent> samples = events.Where(e => e.Type == SessionEventTypes.Move).ToList();
            if (samples.Count > 0) s.MovingShare = samples.Count(e => e.Moving == true) / (float)samples.Count;

            // Taking cover (2026-11-19, an experiment); the time in cover is added by Compute, which has the stretches.
            s.CoversTaken = events.Count(e => e.Type == SessionEventTypes.CoverEnter);
            s.StrikesStoppedByCover = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.StoppedByCover);
            s.StrikesStoppedByWall = events.Count(e => e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.StoppedByWall);

            // The quick duck (work order 043). With its switch on nothing else stops a strike "by cover", so those are the duck's.
            s.Ducks = events.Count(e => e.Type == SessionEventTypes.Duck);
            s.DucksThatStopped = events.Count(e => e.Type == SessionEventTypes.DuckEnd
                                                   && int.TryParse(e.ExtraValue("stopped"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) && n > 0);
            s.DuckTapsWithNothing = events.Count(e => e.Type == SessionEventTypes.DuckNothing);
            s.StoppedByDuck = s.StrikesStoppedByCover;
            ComputeHeldDuck(s, events); // work order 086: the seconds she was down, and what was stopped (SessionSummaryDoorsAndDuck.cs)

            // Stunned and pulled (work order 035).
            s.Stuns = events.Count(e => e.Type == SessionEventTypes.PlayerStunned);
            s.Pulls = events.Count(e => e.Type == SessionEventTypes.PlayerPulled);
            ComputeDrags(s, events); // work order 093: Drags landed, held back, the shortest gap (SessionSummaryDrags.cs)
            return s;
        }

        /// <summary>Seconds where the stretches of <paramref name="a"/> and those of <paramref name="b"/> overlap; neither list overlaps itself.</summary>
        static double Overlap(List<KeyValuePair<double, double>> a, List<KeyValuePair<double, double>> b)
        {
            double total = 0d;
            foreach (KeyValuePair<double, double> x in a)
            foreach (KeyValuePair<double, double> y in b)
                total += Math.Max(0d, Math.Min(x.Value, y.Value) - Math.Max(x.Key, y.Key));
            return total;
        }

        static void FillDamageAndUse(ActionRow row, List<SessionEvent> events, List<KeyValuePair<double, double>> spans, int total)
        {
            row.Damage = events.Where(e => e.Type == SessionEventTypes.Hit && e.Name == row.Name).Sum(e => e.Damage ?? 0);
            row.DamageShare = total > 0 ? row.Damage / (float)total : 0f;
            if (row.Cooldown <= 0f || spans.Count == 0) return;
            // Each span allows a start at its beginning and one more every cooldown after.
            int allowed = spans.Sum(p => 1 + (int)Math.Floor(Math.Max(0d, p.Value - p.Key) / row.Cooldown + 1e-4));
            row.CooldownUse = allowed > 0 ? row.Started / (float)allowed : float.NaN;
        }

        /// <summary>
        /// The basic shot's cooldown use against her real interval (the fourth review of 2026-11-21, work order 055): the
        /// feel's 0.225 s is not hers with the stats on (a run starts 15 % slower, attack speed shortens it), so with
        /// <see cref="SessionEventTypes.ShotInterval"/> in the log each span allows a start at its beginning and one more
        /// each interval after, the interval as it was at each moment. An old log without it is read as before.
        /// </summary>
        static void ShotUseByInterval(ActionRow shot, List<SessionEvent> all, List<KeyValuePair<double, double>> spans, float feelTime)
        {
            List<SessionEvent> changes = all.Where(e => e.Type == SessionEventTypes.ShotInterval && IntervalOf(e) > 0f).ToList();
            if (changes.Count == 0 || spans.Count == 0) return;
            float At(double t)
            {
                float now = feelTime > 0f ? feelTime : IntervalOf(changes[0]);
                foreach (SessionEvent c in changes)
                {
                    if (c.T > t) break;
                    now = IntervalOf(c);
                }
                return now;
            }
            double allowed = 0d;
            foreach (KeyValuePair<double, double> span in spans)
            {
                double from = span.Key, starts = 0d;
                float interval = At(from);
                foreach (SessionEvent c in changes)
                {
                    if (c.T <= span.Key || c.T >= span.Value) continue;
                    starts += (c.T - from) / interval;
                    from = c.T;
                    interval = IntervalOf(c);
                }
                starts += Math.Max(0d, span.Value - from) / interval;
                allowed += 1 + Math.Floor(starts + 1e-4);
            }
            shot.Cooldown = At(spans[0].Key);
            shot.CooldownUse = allowed > 0d ? shot.Started / (float)allowed : float.NaN;
        }

        static float IntervalOf(SessionEvent e)
        {
            string s = e?.ExtraValue("seconds");
            return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
        }

        static bool IsStart(SessionEvent e) =>
            e.Type == SessionEventTypes.SkillStart || e.Type == SessionEventTypes.ShotStart || e.Type == SessionEventTypes.DashStart;

        static bool IsPress(SessionEvent e) =>
            e.Type == SessionEventTypes.SkillPress || e.Type == SessionEventTypes.Click || e.Type == SessionEventTypes.DashPress;

        static string ActionName(SessionEvent e) =>
            e.Type == SessionEventTypes.ShotStart ? SessionWords.BasicShot
            : e.Type == SessionEventTypes.DashStart ? SessionWords.Dash
            : e.Name ?? "?";

        /// <summary>The most times that fall in any window of <see cref="BusiestWindow"/> seconds. The times are in order.</summary>
        public static int Busiest(List<float> times)
        {
            int best = 0;
            for (int from = 0, to = 0; to < times.Count; to++)
            {
                while (times[to] - times[from] > BusiestWindow + 1e-4f) from++;
                best = Math.Max(best, to - from + 1);
            }
            return best;
        }

        /// <summary>The value below which this share of the values lie, by linear interpolation; NaN with no values.</summary>
        public static float Percentile(List<float> values, float share)
        {
            if (values == null || values.Count == 0) return float.NaN;
            List<float> sorted = values.OrderBy(v => v).ToList();
            float rank = share * (sorted.Count - 1);
            int below = (int)Math.Floor(rank);
            int above = Math.Min(sorted.Count - 1, below + 1);
            return sorted[below] + (sorted[above] - sorted[below]) * (rank - below);
        }
    }
}
