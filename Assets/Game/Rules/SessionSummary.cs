using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>One row of the per-action table: a skill, the basic shot or the dash.</summary>
    public sealed class ActionRow
    {
        public string Name;
        public string Input;               // the skill's input type; empty for the basic shot and the dash
        public float Cooldown;             // s, or the basic shot's whole shot time; 0 when not known
        public int Pressed;
        public int Started;
        public int RejectedOnCooldown;
        public int Damage;
        public float DamageShare;          // 0 to 1 of all damage in the scope
        public int Hits;                   // hits that went off and hit something
        public int Misses;                 // hits that went off and hit nothing
        /// <summary>Starts divided by the starts the cooldown would have allowed in the scope's time; NaN when not known.</summary>
        public float CooldownUse = float.NaN;
    }

    /// <summary>How long the key of a Holding or Charge skill was held, and the charge it fired with.</summary>
    public sealed class HoldRow
    {
        public string Name;
        public string Input;
        public int Uses;
        public float HeldMedian = float.NaN;
        public int Fired;                  // uses that fired at least one hit
        public float ChargeMedian = float.NaN; // Charge only, over the uses that fired
        public int AtLowestCharge;         // Charge only: fired under SessionSummary.LowestCharge
    }

    /// <summary>The numbers of one scope: the whole session, or the time in fights.</summary>
    public sealed class ScopeStats
    {
        public string Label;
        public float Seconds;
        public int StartedActions;
        public int Presses;                // every press of a skill key, a click and a dash key, whatever became of it
        public float Apm = float.NaN;
        public float ApmWithRejected = float.NaN;
        public float BusiestApm;
        public float BusiestApmWithRejected;

        public readonly List<ActionRow> Rows = new List<ActionRow>();
        public readonly List<string> NeverUsed = new List<string>();
        public int TotalDamage;

        public float GapMedian = float.NaN;
        public float GapP90 = float.NaN;
        // The long gaps: counted on the time she was free between two actions, after a skill had let her go.
        public int GapsOver1;
        public float GapsOver1Seconds;
        public int GapsOver2;
        public float GapsOver2Seconds;

        public readonly List<KeyValuePair<string, int>> Pairs = new List<KeyValuePair<string, int>>();

        public int FocusFilled;
        public int FocusSpent;
        public float FillSecondsMean = float.NaN;
        public float FullBeforeSpentMean = float.NaN;
        public int EmpoweredDamage;

        public readonly List<HoldRow> Holds = new List<HoldRow>();

        public int EnemyAttacks;
        public int StrikesLanded;
        public int StrikesOutOfArea;
        public int StrikesAvoidedByDash;
        public int DamageTaken;
        public int Dashes;
        public int DashesThatAvoided;

        public float MovingShare = float.NaN;

        public ActionRow Row(string name) => Rows.FirstOrDefault(r => r.Name == name);
    }

    /// <summary>
    /// One skill's part of the kit's numbers: its spender hits, its recast chains and its clean presses. Since
    /// 2026-11-19 two skills spend marks (Scatter Blast, and Backstep on a clean press) and two are recast (Triple Tap,
    /// Backstep), so the summary gives them a line each.
    /// </summary>
    public sealed class KitSkillRow
    {
        public string Name;
        public bool CleanSpend;            // its clean press spends the marks (the log's kit says so)
        public int SpenderHits;
        public float MarksPerSpenderHit = float.NaN;
        public int SpenderHitsAtFive;
        public readonly SortedDictionary<int, int> ChainsByLength = new SortedDictionary<int, int>(); // presses, opening move included
        public int LastPresses;
        public int CleanPresses;
    }

    /// <summary>The kit of decision 0005 (2026-11-19) over the whole session: marks, recast chains, where focus came from.</summary>
    public sealed class KitStats
    {
        /// <summary>The skills that spent marks, ended a chain or pressed a last press, in slot order.</summary>
        public readonly List<KitSkillRow> Skills = new List<KitSkillRow>();
        public KitSkillRow Skill(string name) => Skills.FirstOrDefault(r => r.Name == name);
        public int MarksAdded;
        public int SpenderHits;            // spender hits that landed on a target, with or without marks
        public float MarksPerSpenderHit = float.NaN;
        public int SpenderHitsAtFive;      // hits that took the full cap (the highest count the log's kit allows)
        public int MarksFellOff;           // marks that fell off unused
        public int MarksLostOtherwise;     // marks on a target that died or was reset
        public readonly SortedDictionary<int, int> ChainsByLength = new SortedDictionary<int, int>();
        public int LastPresses;            // presses that fired the last shot of a chain
        public int CleanPresses;
        public float FocusFromMarks;
        public float FocusFromCleanTaps;
        public float FocusFromDamage;
        public int CooldownCuts;
        public int NotEquippedPresses;
    }

    /// <summary>All the numbers of one play session.</summary>
    public sealed class SessionStats
    {
        public KitStats Kit = new KitStats();
        public string Date = "";
        public string Commit = "";
        public string Branch = "";
        public string Unity = "";
        public string Scene = "";
        public float Length;
        public int Fights;
        public int Kills;
        public int Deaths;
        /// <summary>Each fight as (start, end), in seconds since the session started.</summary>
        /// <remarks>Kept in the same type as the events' times, so an event in the fight's first or last millisecond is in it.</remarks>
        public readonly List<KeyValuePair<double, double>> FightSpans = new List<KeyValuePair<double, double>>();
        /// <summary>Generated levels built in the session; the last one's seed, its rooms, and how many of them she entered.</summary>
        public int LevelsBuilt;
        public string LevelSeedText = "";
        public int LevelRooms;
        public int LevelRoomsEntered;
        public ScopeStats Session;
        public ScopeStats InFights;

        // The kinds of enemy (work order 008), whole session.
        /// <summary>A row per kind of enemy met (woke, struck, died or was hit), in the order first met.</summary>
        public readonly List<EnemyKindRow> EnemyKinds = new List<EnemyKindRow>();
        /// <summary>Guards hit at least once, and of them those dazed at least once.</summary>
        public int GuardsHit;
        public int GuardsDazed;
        public int Dazes;
        /// <summary>From a guard's first hit taken to its first daze, the mean over the guards dazed; NaN with none.</summary>
        public float FirstHitToDazeMean = float.NaN;
        /// <summary>Damage dealt to guards, and of it the part dealt while the guard was dazed.</summary>
        public int GuardDamage;
        public int GuardDamageWhileDazed;
    }

    /// <summary>One kind of enemy in a session: its kills, and the damage it did her, by attack.</summary>
    public sealed class EnemyKindRow
    {
        public string Kind;
        public int Kills;
        public int DamageTaken;
        /// <summary>Damage it did her, by the attack's name, in the order the attacks first landed.</summary>
        public readonly List<KeyValuePair<string, int>> DamageByAttack = new List<KeyValuePair<string, int>>();
    }

    /// <summary>
    /// The summary of a play session, computed from its list of events (<see cref="SessionEvent"/>), and written as
    /// plain text in aligned columns. The same numbers come out of a log file read back
    /// (<see cref="WriteFromLog"/>), so a summary can be recomputed later, or after the summary itself changes.
    ///
    /// A fight runs from an enemy waking until no enemy is awake (each has died, given up and gone home, or been
    /// removed) or she dies; a session that ends in a fight ends that fight. With one enemy that is as before work
    /// order 008. Tempo counts actions that started something (a skill, a basic shot, a dash), at the
    /// moment they started, so a buffered press or a waiting click counts when it fires. The second tempo counts every
    /// press of a skill key, a click and a dash key, whatever became of it.
    /// </summary>
    public static class SessionSummary
    {
        /// <summary>A Charge skill fired under this charge counts as fired at the lowest charge.</summary>
        public const float LowestCharge = 0.1f;
        /// <summary>A dash counts as avoiding a strike when a strike that did not land comes this soon after it starts.</summary>
        public const float DashAvoidWindow = 1f;
        public const float BusiestWindow = 10f;
        public const int PairsShown = 10;

        // ---------------- Computing ----------------

        public static SessionStats Compute(IReadOnlyList<SessionEvent> input)
        {
            List<SessionEvent> events = (input ?? Array.Empty<SessionEvent>()).Where(e => e != null && e.Type != null)
                                                                               .OrderBy(e => e.T).ToList();
            var stats = new SessionStats();
            SessionEvent start = events.FirstOrDefault(e => e.Type == SessionEventTypes.SessionStart);
            if (start != null)
            {
                stats.Date = start.ExtraValue("date") ?? "";
                stats.Commit = start.ExtraValue("commit") ?? "";
                stats.Branch = start.ExtraValue("branch") ?? "";
                stats.Unity = start.ExtraValue("unity") ?? "";
                stats.Scene = start.ExtraValue("scene") ?? "";
            }
            SessionEvent end = events.LastOrDefault(e => e.Type == SessionEventTypes.SessionEnd);
            double length = Math.Max(0d, end != null ? end.T : events.Count > 0 ? events[events.Count - 1].T : 0d);
            stats.Length = (float)length;

            // The fights: one starts when an enemy wakes and none was awake, and lasts until none is awake (each has died,
            // given up or been removed) or she dies. An enemy is told apart by its id; a log from before work order 008
            // has none, and then they all count as one enemy, as they did.
            double fightStart = -1d;
            var awake = new HashSet<int>();
            foreach (SessionEvent e in events)
            {
                int id = e.EnemyId ?? -1;
                if (e.Type == SessionEventTypes.EnemyWake)
                {
                    if (awake.Count == 0) fightStart = e.T;
                    awake.Add(id);
                }
                else if (awake.Count > 0 && (e.Type == SessionEventTypes.EnemyDeath || e.Type == SessionEventTypes.EnemyGaveUp
                                              || e.Type == SessionEventTypes.PlayerDeath))
                {
                    if (e.Type == SessionEventTypes.PlayerDeath) awake.Clear();
                    else awake.Remove(id);
                    if (awake.Count > 0) continue;
                    stats.FightSpans.Add(new KeyValuePair<double, double>(fightStart, e.T));
                    fightStart = -1d;
                }
            }
            if (fightStart >= 0d) stats.FightSpans.Add(new KeyValuePair<double, double>(fightStart, Math.Max(fightStart, length)));
            stats.Fights = stats.FightSpans.Count;
            stats.Kills = events.Count(e => e.Type == SessionEventTypes.EnemyDeath);
            ComputeEnemyKinds(events, stats);
            stats.Deaths = events.Count(e => e.Type == SessionEventTypes.PlayerDeath);

            // The generated levels: the last one built, and the rooms of it she entered.
            var entered = new HashSet<string>();
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.LevelBuilt)
                {
                    stats.LevelsBuilt++;
                    stats.LevelSeedText = e.Name ?? "";
                    stats.LevelRooms = int.TryParse(e.ExtraValue("rooms"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int rooms) ? rooms : 0;
                    entered.Clear();
                }
                else if (e.Type == SessionEventTypes.RoomEntered && stats.LevelsBuilt > 0)
                {
                    entered.Add(e.ExtraValue("room") ?? "");
                }
            }
            stats.LevelRoomsEntered = entered.Count;

            Kit kit = Kit.From(events);
            stats.Session = ComputeScope("Session", events, new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(0d, length) }, kit);
            stats.InFights = ComputeScope("In fights", events, stats.FightSpans, kit);
            stats.Kit = ComputeKit(events, start, kit);
            return stats;
        }

        /// <summary>
        /// The kinds of enemy (work order 008), whole session: kills and damage done to her by kind and attack, and for
        /// guards the time from the first hit each took to its first daze and the share of their damage dealt while dazed.
        /// A log from before has no kinds: its enemies count as "enemy".
        /// </summary>
        static void ComputeEnemyKinds(List<SessionEvent> events, SessionStats stats)
        {
            EnemyKindRow Row(string kind)
            {
                kind = string.IsNullOrEmpty(kind) ? SessionWords.Enemy : kind;
                EnemyKindRow row = stats.EnemyKinds.FirstOrDefault(r => r.Kind == kind);
                if (row == null) stats.EnemyKinds.Add(row = new EnemyKindRow { Kind = kind });
                return row;
            }

            const string Guard = "guard";
            var firstHit = new Dictionary<int, double>();
            var firstDaze = new Dictionary<int, double>();
            var dazedSince = new Dictionary<int, double>(); // the guards dazed now
            foreach (SessionEvent e in events)
            {
                switch (e.Type)
                {
                    case SessionEventTypes.EnemyWake:
                        Row(e.Enemy);
                        break;
                    case SessionEventTypes.EnemyDeath:
                        Row(e.Enemy).Kills++;
                        if (e.EnemyId.HasValue) dazedSince.Remove(e.EnemyId.Value);
                        break;
                    case SessionEventTypes.EnemyStrike when e.Result == SessionWords.Landed && e.Damage > 0:
                    {
                        EnemyKindRow row = Row(e.Enemy);
                        row.DamageTaken += e.Damage.Value;
                        string attack = e.Name ?? "?";
                        int at = row.DamageByAttack.FindIndex(p => p.Key == attack);
                        if (at < 0) row.DamageByAttack.Add(new KeyValuePair<string, int>(attack, e.Damage.Value));
                        else row.DamageByAttack[at] = new KeyValuePair<string, int>(attack, row.DamageByAttack[at].Value + e.Damage.Value);
                        break;
                    }
                    case SessionEventTypes.Hit when e.Enemy == Guard && e.EnemyId.HasValue:
                    {
                        int id = e.EnemyId.Value;
                        if (!firstHit.ContainsKey(id)) firstHit[id] = e.T;
                        int damage = e.Damage ?? 0;
                        stats.GuardDamage += damage;
                        if (dazedSince.ContainsKey(id)) stats.GuardDamageWhileDazed += damage;
                        break;
                    }
                    case SessionEventTypes.EnemyDazed when e.EnemyId.HasValue:
                        stats.Dazes++;
                        dazedSince[e.EnemyId.Value] = e.T;
                        if (!firstDaze.ContainsKey(e.EnemyId.Value)) firstDaze[e.EnemyId.Value] = e.T;
                        break;
                    case SessionEventTypes.DazeEnded when e.EnemyId.HasValue:
                        dazedSince.Remove(e.EnemyId.Value);
                        break;
                }
            }
            stats.GuardsHit = firstHit.Count;
            List<double> spans = firstDaze.Where(d => firstHit.ContainsKey(d.Key)).Select(d => d.Value - firstHit[d.Key]).ToList();
            stats.GuardsDazed = spans.Count;
            if (spans.Count > 0) stats.FirstHitToDazeMean = (float)spans.Average();
        }

        /// <summary>The kit's numbers (decision 0005), over the whole session, in all and per skill.</summary>
        static KitStats ComputeKit(List<SessionEvent> events, SessionEvent start, Kit kit)
        {
            var k = new KitStats();
            int cap = 5;
            if (start != null && int.TryParse(start.ExtraValue("kit.MaxMarks"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int c)) cap = c;
            var chainCounts = new Dictionary<string, int>();
            foreach (SessionEvent e in events.Where(e => e.Type == SessionEventTypes.Skill))
                if (int.TryParse(e.ExtraValue("recast_count"), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)) chainCounts[e.Name] = n;

            // A row per skill, made when the skill first shows up in the kit's events; put in slot order at the end.
            var rows = new Dictionary<string, KitSkillRow>();
            var made = new List<KitSkillRow>(); // in the order they were made
            var takenBySkill = new Dictionary<string, List<int>>();
            KitSkillRow Row(string name)
            {
                name = name ?? "?";
                if (!rows.TryGetValue(name, out KitSkillRow row))
                {
                    row = new KitSkillRow
                    {
                        Name = name,
                        CleanSpend = kit.Skills.FirstOrDefault(s => s.Name == name)?.ExtraValue("clean_mark_bonus") != null,
                    };
                    rows[name] = row;
                    made.Add(row);
                    takenBySkill[name] = new List<int>();
                }
                return row;
            }

            var taken = new List<int>();
            foreach (SessionEvent e in events)
            {
                switch (e.Type)
                {
                    case SessionEventTypes.MarkAdd:
                        int added;
                        k.MarksAdded += int.TryParse(e.ExtraValue("added"), NumberStyles.Integer, CultureInfo.InvariantCulture, out added) ? added : 1;
                        break;
                    case SessionEventTypes.MarksSpent:
                    {
                        int marks = e.Marks ?? 0;
                        taken.Add(marks);
                        KitSkillRow row = Row(e.Name);
                        takenBySkill[row.Name].Add(marks);
                        if (marks >= cap)
                        {
                            k.SpenderHitsAtFive++;
                            row.SpenderHitsAtFive++;
                        }
                        k.FocusFromMarks += e.Focus ?? 0f;
                        break;
                    }
                    case SessionEventTypes.MarksLost:
                        if (e.Result == SessionWords.FellOff) k.MarksFellOff += e.Marks ?? 0;
                        else k.MarksLostOtherwise += e.Marks ?? 0;
                        break;
                    case SessionEventTypes.ChainEnd:
                    {
                        int shots = e.Shot ?? 0;
                        k.ChainsByLength[shots] = k.ChainsByLength.TryGetValue(shots, out int had) ? had + 1 : 1;
                        SortedDictionary<int, int> mine = Row(e.Name).ChainsByLength;
                        mine[shots] = mine.TryGetValue(shots, out int hadMine) ? hadMine + 1 : 1;
                        break;
                    }
                    case SessionEventTypes.RecastPress:
                        // The last press of its chain (the kit at the start says how long a chain is).
                        if (chainCounts.TryGetValue(e.Name ?? "", out int count) && e.Shot == count)
                        {
                            KitSkillRow row = Row(e.Name);
                            k.LastPresses++;
                            row.LastPresses++;
                            if (e.Clean == true)
                            {
                                k.CleanPresses++;
                                row.CleanPresses++;
                            }
                        }
                        k.FocusFromCleanTaps += e.Focus ?? 0f;
                        break;
                    case SessionEventTypes.Hit:
                        k.FocusFromDamage += e.Focus ?? 0f;
                        break;
                    case SessionEventTypes.CooldownCut:
                        k.CooldownCuts++;
                        break;
                    case SessionEventTypes.SkillPress:
                        if (e.Reason == SessionWords.NotEquipped) k.NotEquippedPresses++;
                        break;
                }
            }
            k.SpenderHits = taken.Count;
            if (taken.Count > 0) k.MarksPerSpenderHit = (float)taken.Average();
            foreach (KitSkillRow row in made)
            {
                List<int> mine = takenBySkill[row.Name];
                row.SpenderHits = mine.Count;
                if (mine.Count > 0) row.MarksPerSpenderHit = (float)mine.Average();
            }
            // Slot order as the log's kit gives it; a skill missing from it goes last, in the order it first showed up.
            List<string> order = kit.Skills.Select(s => s.Name).ToList();
            int Place(KitSkillRow r) => order.IndexOf(r.Name) is int i && i >= 0 ? i : int.MaxValue;
            k.Skills.AddRange(made.Select((r, i) => (r, i)).OrderBy(x => Place(x.r)).ThenBy(x => x.i).Select(x => x.r));
            return k;
        }

        /// <summary>The kit as the log's start describes it: the skills in slot order, and the timing values the summary needs.</summary>
        sealed class Kit
        {
            public readonly List<SessionEvent> Skills = new List<SessionEvent>();
            public float DashCooldown;
            public float ShotTime;

            public static Kit From(List<SessionEvent> events)
            {
                var kit = new Kit();
                foreach (SessionEvent e in events)
                    if (e.Type == SessionEventTypes.Skill && kit.Skills.All(s => s.Name != e.Name)) kit.Skills.Add(e);
                kit.Skills.Sort((a, b) => (a.Slot ?? 99).CompareTo(b.Slot ?? 99));
                // Skills used in the log but missing from its start (a hand-written log, or an older one) still get a row.
                foreach (SessionEvent e in events)
                {
                    if ((e.Type == SessionEventTypes.SkillPress || e.Type == SessionEventTypes.SkillStart) && e.Name != null
                        && kit.Skills.All(s => s.Name != e.Name))
                        kit.Skills.Add(new SessionEvent(0d, SessionEventTypes.Skill, e.Name) { Slot = e.Slot });
                }
                SessionEvent start = events.FirstOrDefault(e => e.Type == SessionEventTypes.SessionStart);
                kit.DashCooldown = Number(start, "feel.DashCooldown");
                kit.ShotTime = Number(start, "feel.BasicShotStartup") + Number(start, "feel.BasicShotActive")
                               + Number(start, "feel.BasicShotRecovery");
                return kit;
            }

            static float Number(SessionEvent e, string key)
            {
                string s = e?.ExtraValue(key);
                return s != null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v) ? v : 0f;
            }

            public string InputOf(string name) => Skills.FirstOrDefault(s => s.Name == name)?.Input ?? "";
        }

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
            return s;
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

        // ---------------- Writing ----------------

        /// <summary>The summary as text: about a hundred lines of aligned columns, for a terminal.</summary>
        public static string Format(SessionStats stats, string logName = null)
        {
            var sb = new StringBuilder();
            ScopeStats a = stats.Session, f = stats.InFights;
            bool fights = stats.Fights > 0;

            sb.Append("Play session ").Append(Empty(stats.Date));
            if (stats.Commit.Length > 0)
                sb.Append("   commit ").Append(stats.Commit.Length > 10 ? stats.Commit.Substring(0, 10) : stats.Commit);
            if (stats.Branch.Length > 0) sb.Append(" (").Append(stats.Branch).Append(')');
            sb.AppendLine();
            sb.Append("Unity ").Append(Empty(stats.Unity)).Append("   scene ").Append(Empty(stats.Scene));
            if (!string.IsNullOrEmpty(logName)) sb.Append("   log ").Append(logName);
            sb.AppendLine();
            sb.Append("Length ").Append(Duration(stats.Length)).Append("   fights ").Append(stats.Fights)
              .Append(" (").Append(Duration(f.Seconds)).Append(")   kills ").Append(stats.Kills)
              .Append("   deaths ").Append(stats.Deaths).AppendLine();
            if (stats.LevelsBuilt > 0)
                sb.Append("Generated level ").Append(Empty(stats.LevelSeedText)).Append("   rooms entered ").Append(stats.LevelRoomsEntered)
                  .Append(" of ").Append(stats.LevelRooms).Append("   levels built ").Append(stats.LevelsBuilt).AppendLine();
            sb.AppendLine();

            var t = new Table("", "Session", "In fights");
            t.Add("Actions per minute", Num(a.Apm, 1), FightNum(fights, f.Apm, 1));
            t.Add("  with rejected and dropped presses", Num(a.ApmWithRejected, 1), FightNum(fights, f.ApmWithRejected, 1));
            t.Add("Busiest 10 s, actions per minute", Num(a.BusiestApm, 0), FightNum(fights, f.BusiestApm, 0));
            t.Add("  with rejected and dropped presses", Num(a.BusiestApmWithRejected, 0), FightNum(fights, f.BusiestApmWithRejected, 0));
            t.Add("Actions started / presses", a.StartedActions + " / " + a.Presses, fights ? f.StartedActions + " / " + f.Presses : "-");
            t.Add("Gap between actions, median", Seconds(a.GapMedian), fights ? Seconds(f.GapMedian) : "-");
            t.Add("Gap between actions, 90th percentile", Seconds(a.GapP90), fights ? Seconds(f.GapP90) : "-");
            t.Add("Free over 1 s: count, total", a.GapsOver1 + ", " + Seconds(a.GapsOver1Seconds), fights ? f.GapsOver1 + ", " + Seconds(f.GapsOver1Seconds) : "-");
            t.Add("Free over 2 s: count, total", a.GapsOver2 + ", " + Seconds(a.GapsOver2Seconds), fights ? f.GapsOver2 + ", " + Seconds(f.GapsOver2Seconds) : "-");
            t.Add("Time on the move", Percent(a.MovingShare), fights ? Percent(f.MovingShare) : "-");
            t.Add("Damage dealt", a.TotalDamage.ToString(CultureInfo.InvariantCulture), fights ? f.TotalDamage.ToString(CultureInfo.InvariantCulture) : "-");
            t.Write(sb);
            sb.AppendLine();

            WriteRows(sb, a, "Per action, whole session");
            if (fights) WriteRows(sb, f, "Per action, in fights");
            else sb.AppendLine("Per action, in fights: no fight in this session.").AppendLine();

            sb.AppendLine("What follows what (started actions in a row)");
            var p = new Table("Whole session", "", "In fights", "");
            int pairRows = Math.Max(a.Pairs.Count, fights ? f.Pairs.Count : 0);
            if (pairRows == 0) sb.AppendLine("  fewer than two actions.");
            for (int i = 0; i < pairRows; i++)
            {
                p.Add(i < a.Pairs.Count ? a.Pairs[i].Key : "", i < a.Pairs.Count ? a.Pairs[i].Value.ToString(CultureInfo.InvariantCulture) : "",
                      fights && i < f.Pairs.Count ? f.Pairs[i].Key : "", fights && i < f.Pairs.Count ? f.Pairs[i].Value.ToString(CultureInfo.InvariantCulture) : "");
            }
            if (pairRows > 0) p.Write(sb);
            sb.AppendLine();

            var g = new Table("Focus gauge", "Session", "In fights");
            g.Add("Times filled / spent", a.FocusFilled + " / " + a.FocusSpent, fights ? f.FocusFilled + " / " + f.FocusSpent : "-");
            g.Add("Seconds from empty to full, mean", Seconds(a.FillSecondsMean), fights ? Seconds(f.FillSecondsMean) : "-");
            g.Add("Seconds full before spent, mean", Seconds(a.FullBeforeSpentMean), fights ? Seconds(f.FullBeforeSpentMean) : "-");
            g.Add("Damage while empowered", a.EmpoweredDamage.ToString(CultureInfo.InvariantCulture), fights ? f.EmpoweredDamage.ToString(CultureInfo.InvariantCulture) : "-");
            g.Write(sb);
            sb.AppendLine();

            // The kit of decision 0005, whole session.
            KitStats k = stats.Kit ?? new KitStats();
            var m = new Table("Marks and recasts, whole session", "");
            m.Add("Marks added / fell off unused / lost to a death or reset", k.MarksAdded + " / " + k.MarksFellOff + " / " + k.MarksLostOtherwise);
            // Since 2026-11-19 two skills spend marks and two are recast: a line each, under the line for them all.
            // Chains are not added up across skills: Triple Tap's are three presses long, Backstep's two.
            m.Add("Spender hits: hits, marks taken mean / at the cap", k.SpenderHits == 0 ? "-" : SpenderCell(k.SpenderHits, k.MarksPerSpenderHit, k.SpenderHitsAtFive));
            foreach (KitSkillRow r in k.Skills.Where(x => x.SpenderHits > 0))
                m.Add("  " + r.Name + (r.CleanSpend ? " (clean presses)" : ""), SpenderCell(r.SpenderHits, r.MarksPerSpenderHit, r.SpenderHitsAtFive));
            m.Add("Recast chains by length in presses", k.ChainsByLength.Count == 0 ? "-" : "");
            foreach (KitSkillRow r in k.Skills.Where(x => x.ChainsByLength.Count > 0))
                m.Add("  " + r.Name, string.Join("   ", r.ChainsByLength.Select(p => p.Key + ": " + p.Value)));
            m.Add("Last presses that were clean", k.LastPresses == 0 ? "-" : CleanCell(k.CleanPresses, k.LastPresses));
            foreach (KitSkillRow r in k.Skills.Where(x => x.LastPresses > 0))
                m.Add("  " + r.Name, CleanCell(r.CleanPresses, r.LastPresses));
            m.Add("Focus from marks / clean presses / damage", Num(k.FocusFromMarks, 0) + " / " + Num(k.FocusFromCleanTaps, 0) + " / " + Num(k.FocusFromDamage, 0));
            m.Add("Focus spends that cut cooldowns", Int(k.CooldownCuts));
            m.Add("Presses on an empty slot", Int(k.NotEquippedPresses));
            m.Write(sb);
            sb.AppendLine();

            if (a.Holds.Count > 0)
            {
                var h = new Table("Held and charged, whole session", "Uses", "Held, median", "Fired", "Charge, median", "Under " + Percent(LowestCharge));
                foreach (HoldRow r in a.Holds)
                {
                    bool charge = r.Input == "Charge";
                    h.Add(r.Name + " (" + r.Input + ")", r.Uses.ToString(CultureInfo.InvariantCulture), Seconds(r.HeldMedian),
                          r.Fired.ToString(CultureInfo.InvariantCulture), charge ? Percent(r.ChargeMedian) : "-",
                          charge ? r.AtLowestCharge.ToString(CultureInfo.InvariantCulture) : "-");
                }
                h.Write(sb);
                sb.AppendLine();
            }

            var d = new Table("Defence", "Session", "In fights");
            d.Add("Enemy attacks started", Int(a.EnemyAttacks), fights ? Int(f.EnemyAttacks) : "-");
            d.Add("  landed", Int(a.StrikesLanded), fights ? Int(f.StrikesLanded) : "-");
            d.Add("  avoided by moving out of the area", Int(a.StrikesOutOfArea), fights ? Int(f.StrikesOutOfArea) : "-");
            d.Add("  avoided by the dash", Int(a.StrikesAvoidedByDash), fights ? Int(f.StrikesAvoidedByDash) : "-");
            d.Add("Damage taken", Int(a.DamageTaken), fights ? Int(f.DamageTaken) : "-");
            d.Add("Dashes / that avoided a strike", a.Dashes + " / " + Share(a.DashesThatAvoided, a.Dashes),
                  fights ? f.Dashes + " / " + Share(f.DashesThatAvoided, f.Dashes) : "-");
            d.Write(sb);

            // The kinds of enemy (work order 008): a line per kind, under it a line per attack that hurt her.
            if (stats.EnemyKinds.Count > 0)
            {
                sb.AppendLine();
                var k2 = new Table("Enemies by kind, whole session", "Kills", "Damage to her");
                foreach (EnemyKindRow r in stats.EnemyKinds)
                {
                    k2.Add(r.Kind, Int(r.Kills), Int(r.DamageTaken));
                    foreach (KeyValuePair<string, int> byAttack in r.DamageByAttack) k2.Add("  " + byAttack.Key, "", Int(byAttack.Value));
                }
                k2.Write(sb);
            }
            if (stats.GuardsHit > 0)
            {
                sb.AppendLine();
                var gd = new Table("Guards, whole session", "");
                gd.Add("Hit / dazed / dazes", stats.GuardsHit + " / " + stats.GuardsDazed + " / " + stats.Dazes);
                gd.Add("First hit to first daze, mean", Seconds(stats.FirstHitToDazeMean));
                gd.Add("Damage dealt while dazed", stats.GuardDamageWhileDazed + " of " + stats.GuardDamage + " ("
                       + (stats.GuardDamage > 0 ? Percent(stats.GuardDamageWhileDazed / (float)stats.GuardDamage) : "-") + ")");
                gd.Write(sb);
            }
            return sb.ToString();
        }

        static void WriteRows(StringBuilder sb, ScopeStats s, string title)
        {
            sb.Append(title).Append(" (").Append(Duration(s.Seconds)).AppendLine(")");
            var t = new Table("", "Pressed", "Started", "On cd", "Damage", "Share", "Hits", "Misses", "Cd use");
            foreach (ActionRow r in s.Rows)
            {
                bool dash = r.Name == SessionWords.Dash;
                bool shot = r.Name == SessionWords.BasicShot;
                t.Add(r.Name, Int(r.Pressed), Int(r.Started), shot ? "-" : Int(r.RejectedOnCooldown),
                      dash ? "-" : Int(r.Damage), dash ? "-" : Percent(r.DamageShare),
                      dash ? "-" : Int(r.Hits), dash ? "-" : Int(r.Misses), Percent(r.CooldownUse));
            }
            t.Write(sb);
            sb.Append("Never used: ").AppendLine(s.NeverUsed.Count == 0 ? "none" : string.Join(", ", s.NeverUsed));
            sb.AppendLine();
        }

        /// <summary>Columns: the first left-aligned, the rest right-aligned, two spaces apart.</summary>
        sealed class Table
        {
            readonly List<string[]> _rows = new List<string[]>();

            public Table(params string[] header) => _rows.Add(header);

            public void Add(params string[] cells) => _rows.Add(cells);

            public void Write(StringBuilder sb)
            {
                int columns = _rows.Max(r => r.Length);
                var widths = new int[columns];
                foreach (string[] row in _rows)
                    for (int c = 0; c < row.Length; c++) widths[c] = Math.Max(widths[c], (row[c] ?? "").Length);
                foreach (string[] row in _rows)
                {
                    var line = new StringBuilder();
                    for (int c = 0; c < columns; c++)
                    {
                        string cell = c < row.Length ? row[c] ?? "" : "";
                        if (c == 0) line.Append(cell.PadRight(widths[c]));
                        else line.Append("  ").Append(cell.PadLeft(widths[c]));
                    }
                    sb.AppendLine(line.ToString().TrimEnd());
                }
            }
        }

        static string Empty(string s) => string.IsNullOrEmpty(s) ? "?" : s;
        static string Int(int v) => v.ToString(CultureInfo.InvariantCulture);
        static string Num(float v, int decimals) =>
            float.IsNaN(v) ? "-" : v.ToString(decimals == 0 ? "0" : "0." + new string('0', decimals), CultureInfo.InvariantCulture);
        static string FightNum(bool fights, float v, int decimals) => fights ? Num(v, decimals) : "-";
        static string Seconds(float v) => float.IsNaN(v) ? "-" : v.ToString("0.00", CultureInfo.InvariantCulture) + " s";
        static string Percent(float share) => float.IsNaN(share) ? "-" : (share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Share(int part, int whole) => whole > 0 ? Percent(part / (float)whole) : "-";
        static string SpenderCell(int hits, float mean, int atCap) => Int(hits) + ", " + Num(mean, 1) + " / " + Share(atCap, hits);
        static string CleanCell(int clean, int last) => clean + " of " + last + " (" + Share(clean, last) + ")";

        /// <summary>m:ss.s, or s.s s under a minute.</summary>
        public static string Duration(float seconds)
        {
            if (float.IsNaN(seconds) || seconds < 0f) seconds = 0f;
            if (seconds < 60f) return seconds.ToString("0.0", CultureInfo.InvariantCulture) + " s";
            int minutes = (int)(seconds / 60f);
            return minutes.ToString(CultureInfo.InvariantCulture) + ":" + (seconds - minutes * 60f).ToString("00.0", CultureInfo.InvariantCulture);
        }

        // ---------------- Files ----------------

        /// <summary>The summary's path for a log: next to it, the same name with "-summary", as text.</summary>
        public static string SummaryPathFor(string logPath)
        {
            string folder = Path.GetDirectoryName(logPath) ?? "";
            return Path.Combine(folder, Path.GetFileNameWithoutExtension(logPath) + "-summary.txt");
        }

        /// <summary>Reads a log file. Lines that are not events are skipped. The file may still be open for writing.</summary>
        public static List<SessionEvent> ReadLog(string logPath)
        {
            var events = new List<SessionEvent>();
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    SessionEvent e = SessionEvent.Parse(line);
                    if (e != null) events.Add(e);
                }
            }
            return events;
        }

        /// <summary>
        /// Recomputes the summary of a log file without playing, and writes it next to the log
        /// (<see cref="SummaryPathFor"/>), replacing one that is there. Returns the summary's path.
        /// </summary>
        public static string WriteFromLog(string logPath)
        {
            string text = Format(Compute(ReadLog(logPath)), Path.GetFileName(logPath));
            string summaryPath = SummaryPathFor(logPath);
            File.WriteAllText(summaryPath, text, new UTF8Encoding(false));
            return summaryPath;
        }
    }
}
