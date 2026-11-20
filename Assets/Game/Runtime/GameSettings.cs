using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The pause menu's two settings (work order 018, 2026-11-20), kept between sessions in PlayerPrefs under
    /// <see cref="DashKey"/> and <see cref="VolumeKey"/>, and nowhere else: never in an asset, never in the binding asset
    /// or the other settings objects the practice room's values come from (<see cref="SettingsRules"/>).
    ///
    /// The dash direction is an override read where the dash is aimed (<see cref="PlayerInputDriver.DashPoint"/>) and in
    /// the help line: without one, the binding asset's own <see cref="InputBindings.DashDirection"/> rules. The volume is
    /// put on <see cref="AudioListener.volume"/>.
    ///
    /// Nothing is read until <see cref="Load"/> (the HUD does it when it starts), so a test that never makes the HUD
    /// plays with the asset's dash and full volume whatever this machine has stored; <see cref="Unload"/> (the HUD going)
    /// puts both back.
    /// </summary>
    public static class GameSettings
    {
        public const string DashKey = "Roguelike.Settings.DashDirection";
        public const string VolumeKey = "Roguelike.Settings.Volume";

        static bool s_loaded;
        static int s_dash = SettingsRules.NoOverride;
        static int s_volume = SettingsRules.DefaultVolumeStep;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_dash = SettingsRules.NoOverride;
            s_volume = SettingsRules.DefaultVolumeStep;
        }

        public static bool IsLoaded => s_loaded;
        /// <summary>The stored dash value: -1 none, 0 toward the cursor, 1 the way she moves.</summary>
        public static int DashStored => s_dash;
        /// <summary>The volume in tenths, 0 to 10.</summary>
        public static int VolumeStep => s_volume;

        /// <summary>Reads both from PlayerPrefs and puts the volume on the listener.</summary>
        public static void Load()
        {
            s_dash = PlayerPrefs.GetInt(DashKey, SettingsRules.NoOverride);
            if (s_dash != SettingsRules.DashTowardCursor && s_dash != SettingsRules.DashTowardMovement) s_dash = SettingsRules.NoOverride;
            s_volume = SettingsRules.VolumeStep(PlayerPrefs.GetInt(VolumeKey, SettingsRules.DefaultVolumeStep));
            s_loaded = true;
            AudioListener.volume = SettingsRules.Volume(s_volume);
        }

        /// <summary>Forgets both until the next <see cref="Load"/>: the asset's dash and full volume again.</summary>
        public static void Unload()
        {
            ResetStatics();
            AudioListener.volume = 1f;
        }

        /// <summary>The dash direction in use: the override if there is one, else the one the bindings give.</summary>
        public static DashDirection Dash(DashDirection fromBindings) =>
            SettingsRules.DashGoesTheWaySheMoves(s_dash, fromBindings == DashDirection.TowardMovement)
                ? DashDirection.TowardMovement : DashDirection.TowardCursor;

        /// <summary>Chooses the dash direction and keeps it.</summary>
        public static void SetDash(DashDirection direction)
        {
            s_dash = SettingsRules.StoreDash(direction == DashDirection.TowardMovement);
            PlayerPrefs.SetInt(DashKey, s_dash);
            PlayerPrefs.Save();
        }

        /// <summary>Sets the volume step (0 to 10), puts it on the listener and keeps it.</summary>
        public static void SetVolumeStep(int step)
        {
            s_volume = SettingsRules.VolumeStep(step);
            AudioListener.volume = SettingsRules.Volume(s_volume);
            PlayerPrefs.SetInt(VolumeKey, s_volume);
            PlayerPrefs.Save();
        }
    }
}
