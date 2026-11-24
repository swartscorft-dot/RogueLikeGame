using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Knight's Resolve in a run (work order 106; the rule is <see cref="KnightsResolve"/>): asked by <see cref="PlayerHealth"/> for
    /// every blow on her. When the run has a resolve left (the piece equipped, with the between-runs trial on) and the blow would
    /// kill her, it is cut so that she keeps 1 health, and every living enemy within the piece's reach of her (its body's edge) is
    /// dazed for <see cref="KnightsResolve.DazeSeconds"/>: stopped where it stands, what it was winding up dropped, as a counter
    /// dazes. The boss is not dazed while it roars (it cannot be staggered then). Without the piece, nothing: the blow as it is.
    /// </summary>
    public static class KnightsResolveBlow
    {
        /// <summary>The blow as it lands on her: cut to leave her 1 health once a run with the piece, else as it is.</summary>
        public static int Spare(PlayerCharacter player, int damage, int current)
        {
            RunMetaEffects meta = player != null && player.Modifiers != null ? player.Modifiers.Meta : null;
            int cut = KnightsResolve.Spare(damage, current, meta, out bool spared);
            if (!spared) return damage;
            int dazed = Stagger(player.Position, meta.ResolveReach > 0f ? meta.ResolveReach : KnightsResolve.Reach);
            PlaySessionRecorder.Current?.NoteKnightsResolve(damage, dazed);
            Debug.Log("Between runs: Knight's Resolve kept her at 1 health (a blow of " + damage + "); " + dazed + " enemies staggered");
            return cut;
        }

        /// <summary>Dazes every living enemy whose body's edge is within <paramref name="reach"/> m of her; returns how many.</summary>
        public static int Stagger(Vector3 her, float reach)
        {
            var at = new GroundPoint(her.x, her.z);
            var dazed = new List<Damageable>();
            IReadOnlyList<Enemy> all = Enemy.All;
            for (int i = 0; i < all.Count; i++)
            {
                Enemy e = all[i];
                if (e == null || e.IsDead || e.Body == null || (e is EnemyBoss boss && boss.IsRoaring)) continue;
                if (!SkillRules.InCircle(at, reach, e.Body.GroundPosition, e.Body.Radius)) continue;
                dazed.Add(e.Body);
            }
            foreach (Damageable d in dazed) d.DazeFor(KnightsResolve.DazeSeconds); // after the look: a daze may change the list
            return dazed.Count;
        }
    }
}
