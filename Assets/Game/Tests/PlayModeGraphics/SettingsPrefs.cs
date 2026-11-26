using System.Collections.Generic;
using UnityEngine;

namespace Roguelike.PlayModeTests
{
    /// <summary>
    /// Work order 128: every saved setting a settings test may write, kept before the test and put back after it. The Editor's
    /// tests share his saved values (PlayerPrefs), so a test never leaves one changed: a key he had comes back with its value, a
    /// key he had not is deleted again.
    /// </summary>
    public static class SettingsPrefs
    {
        static IEnumerable<string> AllKeys()
        {
            yield return GameSettings.DashKey;
            yield return GameSettings.VolumeKey;
            yield return GameSettings.DamageNumbersKey;
            yield return GameSettings.CameraKickKey;
            yield return GameSettings.GreyboxKey;
            yield return GameSettings.CameraDistanceKey;
            yield return GameSettings.LightKey;
            yield return GameSettings.CameraAngleKey;
            yield return BetweenRunsSwitch.Key;
            yield return AttachmentsSwitch.Key;
            foreach (string k in GraphicsChoices.Keys) yield return k;
            foreach (string k in SavedMusicAndLook.Keys) yield return k; // the follow-up to work order 128
        }

        /// <summary>His values now: each key's value, or null where he has none.</summary>
        public static Dictionary<string, int?> Save()
        {
            var kept = new Dictionary<string, int?>();
            foreach (string k in AllKeys()) kept[k] = PlayerPrefs.HasKey(k) ? PlayerPrefs.GetInt(k) : (int?)null;
            return kept;
        }

        /// <summary>His values put back as <see cref="Save"/> found them.</summary>
        public static void Restore(Dictionary<string, int?> kept)
        {
            if (kept == null) return;
            foreach (KeyValuePair<string, int?> p in kept)
            {
                if (p.Value.HasValue) PlayerPrefs.SetInt(p.Key, p.Value.Value);
                else PlayerPrefs.DeleteKey(p.Key);
            }
            PlayerPrefs.Save();
        }

        /// <summary>The keys as they are now, to compare with what <see cref="Save"/> found.</summary>
        public static Dictionary<string, int?> Now() => Save();
    }
}
