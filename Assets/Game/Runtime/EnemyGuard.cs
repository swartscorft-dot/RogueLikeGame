using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The guard (work order 008): big, slow, a large dark red capsule with a stagger bar. Do you use the right skill
    /// at the right time?
    ///
    /// It fights as the brute does, heavier and slower: a slam on a circle around it and a sweep in a wide cone in
    /// front of it (<see cref="GuardValues"/>). Its <see cref="Damageable"/> carries a <see cref="StaggerBar"/>, shown
    /// over it (<see cref="StaggerBarView"/>): hits take stagger off it by their skill, and when it is empty the guard
    /// is dazed (<see cref="Enemy"/> says what that does) and takes more damage until the bar is full again.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyGuard : Enemy
    {
        public GuardValues Tuning = new GuardValues();

        public override EnemyKind Kind => EnemyKind.Guard;
        public override EnemyValues Values => Tuning.Enemy;
        public StaggerBar Stagger => _self != null ? _self.Stagger : null;

        protected override void Awake()
        {
            base.Awake();
            _self.Stagger = new StaggerBar(Tuning.Stagger);
        }
    }
}
