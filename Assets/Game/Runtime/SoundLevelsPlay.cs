using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// A shot recording's gain as it is played (work order 131, <see cref="SoundLevels"/>): found by the clip's name, which is read
    /// from the engine once a clip (a name read is a new string each time) and kept, so a shot allocates nothing after a clip's first.
    /// </summary>
    public static class SoundLevelsPlay
    {
        static readonly Dictionary<AudioClip, string> s_names = new Dictionary<AudioClip, string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => s_names.Clear();

        /// <summary>The factor the clip's volume is played at: its gain with the switch on; 1 with it off or for no clip.</summary>
        public static float GainOf(AudioClip clip)
        {
            if (!SoundLevelsValues.On || clip == null) return 1f;
            if (!s_names.TryGetValue(clip, out string name))
            {
                name = clip.name;
                s_names[clip] = name;
            }
            return SoundLevels.ClipGainOf(name);
        }
    }
}
