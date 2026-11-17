using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The asset Kristian tunes. Everything reads <see cref="Values"/> live, so a change in the
    /// Inspector applies while playing, and because this is an asset the change is kept after Play mode.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Feel Settings", fileName = "FeelSettings")]
    public sealed class FeelSettings : ScriptableObject
    {
        public FeelValues Values = new FeelValues();

        [ContextMenu("Reset to feel-spec start values")]
        void ResetToStartValues() => Values = new FeelValues();
    }
}
