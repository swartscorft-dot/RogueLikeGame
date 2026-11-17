using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The numbers of the enemy, in one asset. The game reads them live, so a change in the Inspector
    /// applies to its next attack and is kept. Its health is read when it stands up.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Enemy Settings", fileName = "Enemy")]
    public sealed class EnemySettings : ScriptableObject
    {
        public EnemyValues Values = new EnemyValues();

        [ContextMenu("Reset to the G4 start values")]
        void ResetToStartValues() => Values = new EnemyValues();
    }
}
