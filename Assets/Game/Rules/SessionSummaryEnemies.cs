using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace Roguelike.Rules
{
    /// <summary>
    /// The block of the enemies by kind and their attacks in the session summary (work order 008, 035): what is counted (work
    /// order 081 moved it here from SessionSummary.cs, code unchanged).
    /// </summary>
    public static partial class SessionSummary
    {
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
                    case SessionEventTypes.EnemyStrike:
                    {
                        // Every strike by its attack, landed or avoided (work order 035); then, as before, the damage of those that hurt.
                        EnemyKindRow row = Row(e.Enemy);
                        string name = e.Name ?? "?";
                        AttackStrikes strikes = row.StrikesByAttack.FirstOrDefault(a => a.Name == name);
                        if (strikes == null) row.StrikesByAttack.Add(strikes = new AttackStrikes { Name = name });
                        if (e.Result == SessionWords.Landed) strikes.Landed++;
                        else if (e.Result == SessionWords.OutOfArea || e.Result == SessionWords.AvoidedByDash) strikes.Avoided++;
                        if (e.Result != SessionWords.Landed || !(e.Damage > 0)) break;
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

        /// <summary>
        /// What a floor reward's skill came with (work order 057): its tier and an epic's change from the reward_take's extras
        /// ("epic, Overcharge", "rare"); "" for a common and for a log without them.
        /// </summary>
        static string TakenWith(SessionEvent e)
        {
            string tier = e.ExtraValue("tier"), with = e.ExtraValue("with");
            if (string.IsNullOrEmpty(tier) || tier == UpgradeTiers.Word(UpgradeTier.Common)) return "";
            return tier + (string.IsNullOrEmpty(with) ? "" : ", " + with);
        }
    }
}
