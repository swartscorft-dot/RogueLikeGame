using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The runs' block of the session summary (work order 012 and after): what is counted of each run (work order 081 moved it
    /// here from SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
        /// <summary>The runs (work order 012): a row per run started, filled from its floors, rewards, chests and end.</summary>
        static void ComputeRuns(List<SessionEvent> events, SessionStats stats)
        {
            RunRow run = null;
            int Number(SessionEvent e, string key) =>
                int.TryParse(e.ExtraValue(key), NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : 0;
            foreach (SessionEvent e in events)
            {
                if (e.Type == SessionEventTypes.RunStart)
                {
                    run = new RunRow { Seed = e.Name ?? "" };
                    stats.Runs.Add(run);
                    continue;
                }
                if (run == null) continue;
                switch (e.Type)
                {
                    case SessionEventTypes.FloorEnter:
                        run.FloorsReached = Math.Max(run.FloorsReached, Number(e, "floor"));
                        break;
                    case SessionEventTypes.FloorFinish:
                        if (float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float s))
                            run.FloorSeconds[Number(e, "floor")] = s;
                        break;
                    case SessionEventTypes.RewardTake:
                        run.Skills.Add(e.Name ?? "?");
                        run.SkillsWith.Add(TakenWith(e)); // work order 057
                        break;
                    case SessionEventTypes.ChestOpen:
                        run.Upgrades.Add(e.Name ?? "?");
                        // Work order 050: a pick with its tier, and her stats after it.
                        if (!string.IsNullOrEmpty(e.ExtraValue("tier"))) run.TieredPicks.Add(new KeyValuePair<string, string>(e.ExtraValue("tier"), e.Name ?? "?"));
                        if (!string.IsNullOrEmpty(e.ExtraValue("stats"))) run.FinalStats = e.ExtraValue("stats");
                        break;
                    // Work order 017.
                    case SessionEventTypes.EliteEnter:
                        run.ElitesEntered++;
                        break;
                    // Work order 070.
                    case SessionEventTypes.RoomFight:
                    {
                        int floor = Number(e, "floor");
                        if (!run.RoomFights.TryGetValue(floor, out RoomFightCounts counts)) run.RoomFights[floor] = counts = new RoomFightCounts();
                        if (e.Name == "point") counts.Point++;
                        else if (e.ExtraValue("inside") == "true") counts.Inside++;
                        else if (e.Name == "hit") counts.Hit++;
                        else if (e.Name == "seen") counts.Seen++;
                        else counts.Provoked++;
                        counts.MostWoke = Math.Max(counts.MostWoke, Number(e, "woke"));
                        break;
                    }
                    case SessionEventTypes.EliteClear:
                        run.ElitesCleared++;
                        if (float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float es)) run.EliteSeconds.Add(es);
                        break;
                    case SessionEventTypes.SkillUpgradeTake:
                        run.SkillUpgrades.Add(e.Name ?? "?");
                        break;
                    // Work order 025.
                    case SessionEventTypes.FocusChoiceTake:
                        run.FocusChoice = e.Name ?? "?";
                        break;
                    case SessionEventTypes.FocusSpent:
                        if (run.FocusChoice.Length > 0) run.FocusSpends++;
                        break;
                    case SessionEventTypes.PrimeUsed:
                        run.PrimesUsed++;
                        break;
                    case SessionEventTypes.PrimeLapsed:
                        run.PrimesLapsed++;
                        break;
                    // Work order 029.
                    case SessionEventTypes.MetaLoadout:
                        run.HasMeta = true;
                        run.MetaEquipped = string.IsNullOrEmpty(e.Name) ? "" : e.Name.Replace(" | ", ", ");
                        run.MetaWeapon = e.ExtraValue("weapon")?.Replace(" | ", ", "); // work order 060: only when the attachments were on
                        break;
                    case SessionEventTypes.MetaEarn:
                    {
                        run.HasMeta = true;
                        int amount = Number(e, "amount");
                        run.MetaEarned += amount;
                        string from = e.Name ?? "?";
                        int at = run.MetaFrom.FindIndex(p => p.Key == from);
                        if (at < 0) run.MetaFrom.Add(new KeyValuePair<string, int>(from, amount));
                        else run.MetaFrom[at] = new KeyValuePair<string, int>(from, run.MetaFrom[at].Value + amount);
                        break;
                    }
                    case SessionEventTypes.MetaFeat:
                        run.HasMeta = true;
                        run.MetaFeats.Add(e.Name ?? "?");
                        break;
                    case SessionEventTypes.RunEnd:
                    {
                        string kind = e.ExtraValue("kind");
                        int room = Number(e, "room");
                        string where = "floor " + Number(e, "floor") + (room > 0 ? ", room " + room + (string.IsNullOrEmpty(kind) ? "" : " (" + kind + ")") : "");
                        float after = float.TryParse(e.ExtraValue("seconds"), NumberStyles.Float, CultureInfo.InvariantCulture, out float t) ? t : 0f;
                        run.Ended = (e.Result == "act cleared" ? "act cleared" : (e.Result ?? "?") + " on " + where) + " after " + Duration(after);
                        if (!string.IsNullOrEmpty(e.ExtraValue("stats"))) run.FinalStats = e.ExtraValue("stats"); // work order 050
                        run = null;
                        break;
                    }
                }
            }
        }
    }
}
