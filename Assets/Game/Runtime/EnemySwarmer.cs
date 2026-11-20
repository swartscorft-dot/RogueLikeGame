using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The swarmer (work order 008): many, weak and fast, a small pale capsule. Can you clear a crowd?
    ///
    /// It runs at her and, near enough, winds up one short strike on a circle around it (<see cref="SwarmerValues"/>).
    /// They come in packs: when one wakes, every swarmer within <see cref="SwarmerValues.PackWakeRange"/> of it wakes
    /// too. They do not stack on one point: each frame a swarmer nearer another than
    /// <see cref="SwarmerValues.Spacing"/> steps half the shortfall away from it, so a crowd stands spread round her.
    ///
    /// Melee you read (decision 0010; work order 035, 2026-11-20; <see cref="MeleeEnemy"/>): with the switch on it bites
    /// twice a second as it clings, never stopping to wind up, and leaps the last few metres to her
    /// (<see cref="SwarmerValues.Melee"/>); off, the one short strike above, as before.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemySwarmer : MeleeEnemy
    {
        public SwarmerValues Tuning = new SwarmerValues();

        public override EnemyKind Kind => EnemyKind.Swarmer;
        public override EnemyValues Values => Tuning.Enemy;
        protected override EnemyAttackDefinition[] MeleeSet => Tuning.Melee;
        protected override bool BitesOnTheMove => true;

        protected override void Start()
        {
            base.Start();
            _agent.stoppingDistance = Tuning.StopShort;
        }

        protected override void OnWoke()
        {
            base.OnWoke();
            Vector3 here = transform.position;
            foreach (Enemy e in All)
            {
                if (e == this || !(e is EnemySwarmer) || e.IsDead) continue;
                if (Flat(e.transform.position - here).magnitude <= Tuning.PackWakeRange) e.Provoke();
            }
        }

        protected override void AfterMove()
        {
            base.AfterMove(); // a leap's hop (work order 035)
            if (!_agent.isOnNavMesh) return;
            Vector3 here = transform.position;
            Vector3 push = Vector3.zero;
            float spacing = Tuning.Spacing;
            foreach (Enemy e in All)
            {
                if (e == this || !(e is EnemySwarmer) || e.IsDead) continue;
                Vector3 away = Flat(here - e.transform.position);
                float d = away.magnitude;
                if (d >= spacing) continue;
                // Two on the very same point part along a way that differs for each pair.
                Vector3 dir = d > 1e-4f ? away / d : Quaternion.Euler(0f, (Id * 137 + e.Id * 59) % 360, 0f) * Vector3.forward;
                push += dir * ((spacing - d) * 0.5f);
            }
            if (push.sqrMagnitude > 1e-8f) _agent.Move(push);
        }
    }
}
