using System;
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
    ///
    /// A generated floor's brute (work order 023) fights with its own copy of those numbers, with the Shockwave's reach
    /// of the floors (<see cref="LevelValues.FloorBruteValues"/>): <see cref="FloorValues"/>. The asset, and the practice
    /// brute that reads it, keep their own.
    ///
    /// Melee you read (decision 0010; work order 035, 2026-11-20; <see cref="MeleeEnemy"/>): with the switch on it fights
    /// with <see cref="Melee"/>, a Stroke in reach about once a second and a Shield bash that stuns her, and nothing that
    /// reaches her from afar; off, with the slam and the shockwave above, as before.
    /// </summary>
    [RequireComponent(typeof(Damageable), typeof(NavMeshAgent))]
    public sealed class EnemyBrute : MeleeEnemy
    {
        public EnemySettings Settings;
        /// <summary>Its attacks with melee you read on (work order 035, <see cref="MeleeAttacks.Brute"/>), on the practice brute and copied to every brute made from it.</summary>
        public EnemyAttackDefinition[] Melee = MeleeAttacks.Brute();

        /// <summary>For a generated floor's brute (work order 023): the numbers it fights with instead of the asset's; null for the practice brute.</summary>
        [NonSerialized] public EnemyValues FloorValues;

        readonly EnemyValues _fallback = new EnemyValues();

        public override EnemyKind Kind => EnemyKind.Brute;
        public override EnemyValues Values => FloorValues ?? (Settings != null ? Settings.Values : _fallback);
        protected override EnemyAttackDefinition[] MeleeSet => Melee;
    }
}
