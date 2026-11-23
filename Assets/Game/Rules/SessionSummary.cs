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

        // Taking cover (Kristian 2026-11-19, an experiment).
        /// <summary>Seconds she spent in cover in this scope, from taking it to leaving it.</summary>
        public float CoverSeconds;
        /// <summary>Times she took cover in this scope.</summary>
        public int CoversTaken;
        /// <summary>The share of this scope's time she spent in cover; NaN for a scope with no time.</summary>
        public float CoverShare => Seconds > 0f ? CoverSeconds / Seconds : float.NaN;
        /// <summary>Enemy strikes that what she was in cover behind stopped.</summary>
        public int StrikesStoppedByCover;
        /// <summary>Things that flew at her that a wall or a prop stopped (work order 036).</summary>
        public int StrikesStoppedByWall;

        // The quick duck (work order 043), in place of taking cover while its switch is on.
        /// <summary>Ducks she took; of them, those that stopped something; taps with no low prop in reach.</summary>
        public int Ducks;
        public int DucksThatStopped;
        public int DuckTapsWithNothing;
        /// <summary>Things a duck stopped: with the switch on, the strikes stopped by what she was behind.</summary>
        public int StoppedByDuck;
        /// <summary>Work order 086 (the held duck): the seconds she was down, over the ducks that ended in this scope, in all and the median (NaN with none).</summary>
        public float DuckSecondsTotal;
        public float DuckSecondsMedian = float.NaN;
        /// <summary>Work order 086: what a duck stopped, by the enemy's kind and its attack ("shooter Arrow"), most first.</summary>
        public readonly List<KeyValuePair<string, int>> StoppedByDuckWhat = new List<KeyValuePair<string, int>>();

        // Melee you read (work order 035): two things that happen to her.
        /// <summary>Times she was stunned, and times she was pulled in, in this scope.</summary>
        public int Stuns;
        public int Pulls;

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

        /// <summary>The runs of the session (work order 012), in the order they started.</summary>
        public readonly List<RunRow> Runs = new List<RunRow>();

        /// <summary>Each stretch in cover as (start, end), in seconds since the session started (an experiment, 2026-11-19).</summary>
        public readonly List<KeyValuePair<double, double>> CoverSpans = new List<KeyValuePair<double, double>>();
        /// <summary>True when the log has anything of taking cover: the summary then gives its lines.</summary>
        public bool HasCover;
        /// <summary>
        /// True when the quick duck's switch was on in the session (its start says so) or the log has a duck in it (work
        /// order 043): the summary then gives the duck's lines in place of taking cover's.
        /// </summary>
        public bool HasDuck;
        /// <summary>True when she was stunned or pulled in the session (work order 035): the summary then gives its line.</summary>
        public bool HasHeld;

        // The bombers (work order 073), whole session: their blasts, those that hurt her, and what they did to the others.
        public int BomberBlasts;
        public int BomberBlastsOnHer;
        public int BlastEnemiesHurt;
        /// <summary>Enemies killed by a bomber's blast.</summary>
        public int BlastKills;

        // The warders (work order 079), whole session: wards raised (on), wards broken and why, the damage the wards took off
        // her hits ("warded away"), warders killed.
        public int WardsRaised;
        public int WardsBroken;
        public int WardsBrokenByKill;
        public int WardsBrokenByStagger;
        public int WardsBrokenByDistance;
        public int WardedAway;
        public int WardersKilled;
        /// <summary>The frame times (work order 074): empty for a log from before them, and then the summary has no block of them.</summary>
        public FrameTimesStats Frames = new FrameTimesStats();
        /// <summary>The room lab's rooms (work order 084, <see cref="LabRoomsSummary"/>): empty for a session without the lab.</summary>
        public List<LabRoomRow> LabRooms = new List<LabRoomRow>();
    }

    /// <summary>One run of a session (work order 012): its seed, the floors it reached, how long each took, what it took, how it ended.</summary>
    public sealed class RunRow
    {
        public string Seed = "";
        public int FloorsReached;
        /// <summary>Seconds per finished floor, by the floor's number.</summary>
        public readonly SortedDictionary<int, float> FloorSeconds = new SortedDictionary<int, float>();
        public readonly List<string> Skills = new List<string>();
        /// <summary>
        /// Work order 057: what each of <see cref="Skills"/> came with, in the same order: "epic, Overcharge", "rare", or "" for a
        /// common (and for a log written before the skills had tiers).
        /// </summary>
        public readonly List<string> SkillsWith = new List<string>();
        public readonly List<string> Upgrades = new List<string>();
        /// <summary>The skill upgrades taken from elite rooms, "Quick Shot: Double Tap", in order (work order 017).</summary>
        public readonly List<string> SkillUpgrades = new List<string>();
        /// <summary>Elite rooms entered and cleared (work order 017), and the seconds each clear took from the first step in.</summary>
        public int ElitesEntered, ElitesCleared;
        public readonly List<float> EliteSeconds = new List<float>();
        /// <summary>The focus choice taken (work order 025), "" before; and, from then on, the gauge's spends and Hair Trigger's primings.</summary>
        public string FocusChoice = "";
        public int FocusSpends, PrimesUsed, PrimesLapsed;
        /// <summary>
        /// What lasts between runs (work order 029), only when the run started with it switched on: the pieces equipped,
        /// the currency earned and from what, in the order first earned, and the feats done for the first time.
        /// </summary>
        public bool HasMeta;
        public string MetaEquipped = "";
        /// <summary>Work order 060: her pistols' parts fitted ("Laser Sight, Bull Barrel", or "stock"); null when the run had no attachments on.</summary>
        public string MetaWeapon;
        public int MetaEarned;
        public readonly List<KeyValuePair<string, int>> MetaFrom = new List<KeyValuePair<string, int>>();
        public readonly List<string> MetaFeats = new List<string>();
        /// <summary>How it ended, in words; "still going" when the session ended first.</summary>
        public string Ended = "still going";
        /// <summary>
        /// Work order 050: the chest picks that had a tier, as (tier, short form) in the order taken; and her stats in one
        /// line, after her last pick or when the run ended, the later.
        /// </summary>
        public readonly List<KeyValuePair<string, string>> TieredPicks = new List<KeyValuePair<string, string>>();
        public string FinalStats = "";
        /// <summary>
        /// Work order 070: the rooms' fights that started, by the floor's number: at the point; inside the room short of it (one
        /// of them noticed her by its range, or was hurt, as she came in); or from outside the room (an enemy hurt, one that
        /// noticed her by its range, one provoked); and the most enemies that woke at once. Empty for a log from before, or with
        /// the switch off.
        /// </summary>
        public readonly SortedDictionary<int, RoomFightCounts> RoomFights = new SortedDictionary<int, RoomFightCounts>();
        /// <summary>Work order 088: each room's fight with its tall and low covers and what was stopped in it (<see cref="SessionSummary"/>'s cover block); empty for a log from before.</summary>
        public readonly List<FightCoverRow> FightCover = new List<FightCoverRow>();
    }

    /// <summary>The rooms' fights of one floor of a run (work order 070), by how they started and where she was.</summary>
    public sealed class RoomFightCounts
    {
        /// <summary>At the point.</summary>
        public int Point;
        /// <summary>Inside the room, short of the point: one of its enemies noticed her or was hurt first.</summary>
        public int Inside;
        /// <summary>From outside the room (the passage, the room before): by a hit, a sight by range, a provoke.</summary>
        public int Hit, Seen, Provoked;
        /// <summary>The most enemies that woke at once in one of them.</summary>
        public int MostWoke;
        public int FromOutside => Hit + Seen + Provoked;
        /// <summary>Work order 086: times a room's doors closed behind her, and the seconds from each closing to its opening (those that opened).</summary>
        public int DoorsClosed;
        public readonly List<float> DoorSeconds = new List<float>();
    }

    /// <summary>One kind of enemy in a session: its kills, and the damage it did her, by attack.</summary>
    public sealed class EnemyKindRow
    {
        public string Kind;
        public int Kills;
        public int DamageTaken;
        /// <summary>Damage it did her, by the attack's name, in the order the attacks first landed.</summary>
        public readonly List<KeyValuePair<string, int>> DamageByAttack = new List<KeyValuePair<string, int>>();
        /// <summary>
        /// Its strikes by the attack's name, in the order the attacks first struck (work order 035: the melee kinds' new
        /// attacks are listed by name with landed and avoided, those that do no damage, the Drag, too).
        /// </summary>
        public readonly List<AttackStrikes> StrikesByAttack = new List<AttackStrikes>();
    }

    /// <summary>One attack of a kind of enemy in a session (work order 035): its strikes that landed and those she avoided, by moving or the dash.</summary>
    public sealed class AttackStrikes
    {
        public string Name;
        public int Landed;
        public int Avoided;
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
    ///
    /// Taking cover (Kristian 2026-11-19, an experiment): a log with any cover in it gets two lines, the time in cover
    /// and its share of the time (of the fights' time in the "In fights" column), and under the defence the enemy shots
    /// that what she was in cover behind stopped. The quick duck (work order 043): in a log whose start has its switch on, or
    /// that has a duck in it, those two lines give way to the ducks and those that stopped something, the taps with no low
    /// prop in reach, and under the defence the things a duck stopped.
    /// </summary>
    public static partial class SessionSummary
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
            ComputeRuns(events, stats);
            ComputeFightCover(events, stats); // work order 088

            // Taking cover (2026-11-19, an experiment): from each cover_enter to the next cover_leave, or the session's end.
            double coverFrom = -1d;
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.CoverEnter && coverFrom < 0d) coverFrom = e.T;
                else if (e.Type == SessionEventTypes.CoverLeave && coverFrom >= 0d)
                {
                    stats.CoverSpans.Add(new KeyValuePair<double, double>(coverFrom, e.T));
                    coverFrom = -1d;
                }
            }
            if (coverFrom >= 0d) stats.CoverSpans.Add(new KeyValuePair<double, double>(coverFrom, Math.Max(coverFrom, length)));
            stats.HasCover = events.Any(e => e.Type == SessionEventTypes.CoverEnter
                                             || (e.Type == SessionEventTypes.EnemyStrike && e.Result == SessionWords.StoppedByCover));
            stats.HasHeld = events.Any(e => e.Type == SessionEventTypes.PlayerStunned || e.Type == SessionEventTypes.PlayerPulled);
            // The quick duck (work order 043): its switch on at the start (with taking cover's), or any duck in the log.
            stats.HasDuck = (start != null && start.ExtraValue("feel.QuickDuck") == "true" && start.ExtraValue("feel.TakeCover") != "false")
                            || events.Any(e => e.Type == SessionEventTypes.Duck || e.Type == SessionEventTypes.DuckEnd || e.Type == SessionEventTypes.DuckNothing);

            Kit kit = Kit.From(events);
            stats.Session = ComputeScope("Session", events, new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(0d, length) }, kit);
            stats.InFights = ComputeScope("In fights", events, stats.FightSpans, kit);
            stats.Session.CoverSeconds = (float)Overlap(stats.CoverSpans, new List<KeyValuePair<double, double>> { new KeyValuePair<double, double>(0d, length) });
            stats.InFights.CoverSeconds = (float)Overlap(stats.CoverSpans, stats.FightSpans);
            stats.Kit = ComputeKit(events, start, kit);
            ComputeBombers(events, stats); // work order 073
            ComputeWarders(events, stats); // work order 079
            stats.Frames = FrameTimesSummary.Compute(events); // work order 074
            stats.LabRooms = LabRoomsSummary.Compute(events); // work order 084
            return stats;
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
            // The runs (work order 012): a short block each.
            foreach (RunRow run in stats.Runs)
            {
                sb.Append("Run ").Append(Empty(run.Seed)).Append("   floors reached ").Append(run.FloorsReached)
                  .Append("   ended: ").Append(run.Ended).AppendLine();
                sb.Append("  time per floor   ").AppendLine(run.FloorSeconds.Count == 0 ? "-"
                    : string.Join("   ", run.FloorSeconds.Select(p => p.Key + ": " + Duration(p.Value))));
                sb.Append("  skills taken     ").AppendLine(run.Skills.Count == 0 ? "-"
                    : string.Join(", ", run.Skills.Select((s, i) => SkillTiers.Named(s, i < run.SkillsWith.Count ? run.SkillsWith[i] : "")))); // work order 057: with their tiers
                sb.Append("  upgrades taken   ").AppendLine(run.Upgrades.Count == 0 ? "-" : string.Join(", ", run.Upgrades));
                // Work order 017: the elite rooms, and the skill upgrades in the order taken.
                sb.Append("  elite rooms      ").Append("entered ").Append(run.ElitesEntered).Append(", cleared ").Append(run.ElitesCleared)
                  .AppendLine(run.EliteSeconds.Count == 0 ? "" : " (" + string.Join(", ", run.EliteSeconds.Select(Duration)) + ")");
                sb.Append("  skill upgrades   ").AppendLine(run.SkillUpgrades.Count == 0 ? "-" : string.Join(", ", run.SkillUpgrades));
                // Work order 070: the rooms' fights by floor, only when the run had any.
                if (run.RoomFights.Count > 0)
                    sb.Append("  room fights      ").AppendLine(string.Join(";  ", run.RoomFights.Select(p =>
                        "floor " + p.Key + ": " + p.Value.Point + " at the point, " + p.Value.Inside + " inside short of it, " + p.Value.FromOutside + " from outside (" + p.Value.Hit + " hit, "
                        + p.Value.Seen + " seen" + (p.Value.Provoked > 0 ? ", " + p.Value.Provoked + " provoked" : "") + "), most woke at once " + p.Value.MostWoke
                        + DoorsWords(p.Value)))); // work order 086: the doors closed, and how long they stood
                AppendFightCover(sb, run); // work order 088
                // Work order 025: the focus choice, and how the gauge was spent after it.
                sb.Append("  focus choice     ").AppendLine(run.FocusChoice.Length == 0 ? "-"
                    : run.FocusChoice + "   spent " + run.FocusSpends + (run.PrimesUsed + run.PrimesLapsed > 0
                        ? ", primed skills used " + run.PrimesUsed + ", lapsed " + run.PrimesLapsed : ""));
                // Work order 050: the picks by tier, and her final stats, only when the run had them.
                if (run.TieredPicks.Count > 0)
                    sb.Append("  picks by tier    ").AppendLine(string.Join(";  ", UpgradeTiers.All.Select(UpgradeTiers.Word).Select(tier =>
                    {
                        List<string> of = run.TieredPicks.Where(p => p.Key == tier).Select(p => p.Value).ToList();
                        return tier + " " + of.Count + (of.Count > 0 ? " (" + string.Join(", ", of) + ")" : "");
                    })));
                if (run.FinalStats.Length > 0) sb.Append("  final stats      ").AppendLine(run.FinalStats);
                // Work order 029: one line, only when the run started with what lasts between runs switched on.
                if (run.HasMeta)
                    sb.Append("  between runs     equipped ").Append(run.MetaEquipped.Length == 0 ? "nothing" : run.MetaEquipped)
                      .Append(run.MetaWeapon != null ? ";  weapon " + run.MetaWeapon : "") // work order 060
                      .Append(";  earned ").Append(run.MetaEarned)
                      .Append(run.MetaFrom.Count == 0 ? "" : " (" + string.Join(", ", run.MetaFrom.Select(p => p.Key + " " + p.Value)) + ")")
                      .Append(";  new feats ").AppendLine(run.MetaFeats.Count == 0 ? "-" : string.Join(", ", run.MetaFeats));
            }
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
            // The quick duck (work order 043) in place of taking cover while its switch is on; else taking cover
            // (2026-11-19, an experiment), only in a log that has any.
            if (stats.HasDuck)
            {
                t.Add("Quick ducks / that stopped something", a.Ducks + " / " + a.DucksThatStopped, fights ? f.Ducks + " / " + f.DucksThatStopped : "-");
                t.Add("  taps with no low prop in reach", Int(a.DuckTapsWithNothing), fights ? Int(f.DuckTapsWithNothing) : "-");
                // Work order 086, the held duck: how long she was down.
                t.Add("  seconds down: total, median", DuckSecondsCell(a), fights ? DuckSecondsCell(f) : "-");
            }
            else if (stats.HasCover)
                t.Add("Time in cover, share of the time", Duration(a.CoverSeconds) + ", " + Percent(a.CoverShare),
                      fights ? Duration(f.CoverSeconds) + ", " + Percent(f.CoverShare) : "-");
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
            if (stats.HasDuck) // work order 043
            {
                d.Add("  stopped by a quick duck", Int(a.StoppedByDuck), fights ? Int(f.StoppedByDuck) : "-");
                if (a.StoppedByDuckWhat.Count > 0) // work order 086: what it stopped while she was down
                    d.Add("    what it stopped", StoppedWhatCell(a), fights ? StoppedWhatCell(f) : "-");
            }
            else if (stats.HasCover)
                d.Add("  stopped by the cover she ducked behind", Int(a.StrikesStoppedByCover), fights ? Int(f.StrikesStoppedByCover) : "-");
            if (a.StrikesStoppedByWall > 0) // work order 036, only in a log that has any
                d.Add("  stopped by a wall or prop", Int(a.StrikesStoppedByWall), fights ? Int(f.StrikesStoppedByWall) : "-");
            d.Add("Damage taken", Int(a.DamageTaken), fights ? Int(f.DamageTaken) : "-");
            if (stats.HasHeld)
                d.Add("Stunned / pulled", a.Stuns + " / " + a.Pulls, fights ? f.Stuns + " / " + f.Pulls : "-");
            d.Add("Dashes / that avoided a strike", a.Dashes + " / " + Share(a.DashesThatAvoided, a.Dashes),
                  fights ? f.Dashes + " / " + Share(f.DashesThatAvoided, f.Dashes) : "-");
            d.Write(sb);

            // The kinds of enemy (work order 008): a line per kind, under it a line per attack that hurt her.
            if (stats.EnemyKinds.Count > 0)
            {
                sb.AppendLine();
                // Work order 035: a line per attack that struck, with its strikes landed and avoided, and its damage if any.
                var k2 = new Table("Enemies by kind, whole session", "Kills", "Damage to her", "Landed", "Avoided");
                foreach (EnemyKindRow r in stats.EnemyKinds)
                {
                    k2.Add(r.Kind, Int(r.Kills), Int(r.DamageTaken), Int(r.StrikesByAttack.Sum(a => a.Landed)), Int(r.StrikesByAttack.Sum(a => a.Avoided)));
                    foreach (AttackStrikes byAttack in r.StrikesByAttack)
                    {
                        int at = r.DamageByAttack.FindIndex(p => p.Key == byAttack.Name);
                        k2.Add("  " + byAttack.Name, "", at >= 0 ? Int(r.DamageByAttack[at].Value) : "", Int(byAttack.Landed), Int(byAttack.Avoided));
                    }
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
            WriteBombers(sb, stats); // work order 073
            WriteWarders(sb, stats); // work order 079
            LabRoomsSummary.Write(sb, stats.LabRooms); // work order 084: nothing without the lab
            FrameTimesSummary.Write(sb, stats.Frames); // work order 074: its own block, under every line before it
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
