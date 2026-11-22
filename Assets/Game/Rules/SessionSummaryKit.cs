using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The block of the marks and recasts in the session summary (decision 0005): what is counted (work order 081 moved it here
    /// from SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
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
    }
}
