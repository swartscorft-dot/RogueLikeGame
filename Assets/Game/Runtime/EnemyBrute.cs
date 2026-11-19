using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The enemy of G4, the brute: it walks up to the player and strikes with two telegraphed attacks, a slam around it
    /// and a shockwave down a line. Since work order 008 it is one kind of <see cref="Enemy"/>, which does the walking,
    /// the telegraph, the hit, dying and coming back; the brute gives only its numbers, from the enemy settings asset.
    /// The practice enemy in the scene is a brute, and generated levels copy it for their brutes.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyBrute : Enemy
    {
        public EnemySettings Settings;

        readonly EnemyValues _fallback = new EnemyValues();

        public override EnemyKind Kind => EnemyKind.Brute;
        public override EnemyValues Values => Settings != null ? Settings.Values : _fallback;
    }
}
