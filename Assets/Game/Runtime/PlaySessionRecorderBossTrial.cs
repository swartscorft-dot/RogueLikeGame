using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;

namespace Roguelike
{
    /// <summary>
    /// The trial boss in the log (work order 147, <see cref="BossTrialLog"/>): its own lines, told by the boss; and on each line of a
    /// trial boss (<see cref="BossTrialLine"/>, from the enemy lines' one maker) the phase, and on its tells and strikes her speed and
    /// distance to it then, and on a dash-only attack's tell the seconds since her last dash press within 10 s. Off (a boss that is
    /// not the trial): nothing. Nothing is written a frame.
    /// </summary>
    public sealed partial class PlaySessionRecorder
    {
        /// <summary>One of the trial boss's own lines, with its extras in pairs.</summary>
        public void NoteBossTrial(string type, string name, string[] extras) => Note(type, name, extras);

        /// <summary>The extras of a trial boss's line (<paramref name="r"/> is its report); the line itself when it is no trial boss's.</summary>
        SessionEvent BossTrialLine(SessionEvent e, EnemyReport r)
        {
            if (r.EnemyKind != EnemyKind.Boss) return e;
            EnemyBoss boss = null;
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
                if (all[i] != null && all[i].Id == r.Id) { boss = all[i] as EnemyBoss; break; }
            if (boss == null || !boss.InTrial) return e;
            e.AddExtra(BossTrialLog.PhaseKey, boss.TrialPhase.ToString(CultureInfo.InvariantCulture));
            if ((r.Kind == EnemyReportKind.AttackStarted || r.Kind == EnemyReportKind.Struck) && boss.Player != null)
            {
                UnityEngine.Vector3 v = boss.Player.Velocity, d = boss.Player.Position - boss.transform.position;
                v.y = 0f;
                d.y = 0f;
                e.AddExtra(BossTrialLog.HerSpeed, v.magnitude.ToString("0.00", CultureInfo.InvariantCulture));
                e.AddExtra(BossTrialLog.HerDistance, d.magnitude.ToString("0.00", CultureInfo.InvariantCulture));
                float dash = boss.TrialSecondsSinceDash;
                if (r.Kind == EnemyReportKind.AttackStarted && r.Attack == BossTrialValues.Slam && dash >= 0f && dash <= boss.TrialValues.DashBeforeWithin)
                    e.AddExtra(BossTrialLog.DashBefore, dash.ToString("0.00", CultureInfo.InvariantCulture));
            }
            return e;
        }
    }
}
