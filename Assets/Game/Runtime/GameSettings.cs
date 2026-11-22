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
    ///
    /// A third (work order 019, 2026-11-20): damage numbers on or off, under <see cref="DamageNumbersKey"/>, the same way:
    /// an override read where the numbers are made (<see cref="TimingFeedback"/>); without one they show
    /// (<see cref="FeedbackValues.DamageNumbers"/>).
    /// </summary>
    public static class GameSettings
    {
        public const string DashKey = "Roguelike.Settings.DashDirection";
        public const string VolumeKey = "Roguelike.Settings.Volume";
        public const string DamageNumbersKey = "Roguelike.Settings.DamageNumbers";
        public const string CameraKickKey = "Roguelike.Settings.CameraKick"; // work order 064: 1 on (the default), 0 off
        public const string GreyboxKey = "Roguelike.Settings.Greybox"; // work order 084: 1 on, 0 off (the default)

        static bool s_loaded;
        static int s_dash = SettingsRules.NoOverride;
        static int s_volume = SettingsRules.DefaultVolumeStep;
        static int s_numbers = FeedbackRules.NoOverride;
        static bool s_kick = true;
        static bool s_greybox; // work order 084

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            s_loaded = false;
            s_dash = SettingsRules.NoOverride;
            s_volume = SettingsRules.DefaultVolumeStep;
            s_numbers = FeedbackRules.NoOverride;
            s_kick = true;
            s_greybox = false; // work order 084
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
            s_numbers = FeedbackRules.StoredNumbers(PlayerPrefs.GetInt(DamageNumbersKey, FeedbackRules.NoOverride));
            s_kick = PlayerPrefs.GetInt(CameraKickKey, 1) != 0;
            s_greybox = PlayerPrefs.GetInt(GreyboxKey, 0) != 0; // work order 084
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

        /// <summary>The stored damage numbers value: -1 none, 0 off, 1 on (work order 019).</summary>
        public static int DamageNumbersStored => s_numbers;

        /// <summary>Are damage numbers shown? The override if there is one, else <paramref name="fallback"/>.</summary>
        public static bool DamageNumbers(bool fallback) => FeedbackRules.ShowsDamageNumbers(s_numbers, fallback);

        /// <summary>
        /// The camera's kick when a shot goes off (work order 064: "trøkk"), on unless turned off in the pause menu: some people
        /// are made ill by a camera that moves.
        /// </summary>
        public static bool CameraKick => s_kick;

        /// <summary>Turns the camera's kick on or off and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void SetCameraKick(bool on, bool keep = true)
        {
            s_kick = on;
            if (!keep) return;
            PlayerPrefs.SetInt(CameraKickKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// The greybox view (work order 084, <see cref="GreyboxView"/>): a generated floor drawn as plain blocks, so its space is
        /// judged before its art. Off unless turned on in the pause menu or with the developer's G.
        /// </summary>
        public static bool Greybox => s_greybox;

        /// <summary>Turns the greybox view on or off and, with <paramref name="keep"/>, keeps it (a test keeps nothing).</summary>
        public static void SetGreybox(bool on, bool keep = true)
        {
            s_greybox = on;
            if (!keep) return;
            PlayerPrefs.SetInt(GreyboxKey, on ? 1 : 0);
            PlayerPrefs.Save();
        }

        /// <summary>Turns damage numbers on or off and keeps it.</summary>
        public static void SetDamageNumbers(bool on)
        {
            s_numbers = on ? FeedbackRules.NumbersOn : FeedbackRules.NumbersOff;
            PlayerPrefs.SetInt(DamageNumbersKey, s_numbers);
            PlayerPrefs.Save();
        }
    }
}
