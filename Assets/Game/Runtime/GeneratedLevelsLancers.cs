using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The lancer in the practice room (work order 108, 2026-11-24), as part of <see cref="GeneratedLevels"/>: K spawns one lancer
    /// about <see cref="GeneratedLevels.SpawnDistance"/> from her, as the other kinds' keys do (12 m: inside its band, so it winds
    /// up at once where its line to her is clear). A developer key (work order 066, <see cref="DeveloperKeys"/>).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>
        /// Work order 108: a lancer in the practice room. K: no binding of <see cref="InputBindings"/> uses it, nor the developer's
        /// other keys (L is the room lab's). A developer's key (work order 066, <see cref="DeveloperKeys"/>).
        /// </summary>
        public const Key LancerKey = Key.K;

        /// <summary>The lancer's key: one lancer, as the practice room's other spawns (<see cref="SpawnPractice"/>).</summary>
        public List<Enemy> SpawnLancer() => SpawnPractice(EnemyKind.Lancer);
    }
}
