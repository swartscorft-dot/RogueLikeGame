using System;

namespace Roguelike.Rules
{
    /// <summary>
    /// The numbers of the HUD and the end of a run (work order 018, 2026-11-20), first guesses. Readable combat is the
    /// game's fourth pillar: a hit on her is never missed, so the edges of the screen flash red, stronger for a bigger
    /// hit, and under a quarter of her health a faint red edge stays. When the run ends, the game slows to a stop over
    /// about half a second before the end screen shows.
    /// </summary>
    public static class HudRules
    {
        /// <summary>Seconds of real time a hurt flash takes to fade.</summary>
        public const float FlashSeconds = 0.45f;
        /// <summary>The flash of the smallest hit, and of the biggest (0 to 1, the edge's opacity).</summary>
        public const float WeakestFlash = 0.35f;
        public const float StrongestFlash = 0.9f;
        /// <summary>How much the flash grows per share of her maximum health the hit took: a tenth of it flashes 0.6.</summary>
        public const float FlashPerShare = 2.5f;
        /// <summary>Under this share of her maximum health the low-health edge stays.</summary>
        public const float LowHealthShare = 0.25f;
        /// <summary>The low-health edge's opacity.</summary>
        public const float LowHealthEdge = 0.28f;
        /// <summary>Seconds of real time the game takes to slow to a stop when the run ends.</summary>
        public const float EndSlowSeconds = 0.5f;

        /// <summary>The flash a hit of <paramref name="damage"/> starts with, for a maximum of <paramref name="maxHealth"/>; 0 for no damage.</summary>
        public static float HurtFlash(int damage, int maxHealth)
        {
            if (damage <= 0) return 0f;
            float share = maxHealth > 0 ? damage / (float)maxHealth : 1f;
            return Math.Min(StrongestFlash, WeakestFlash + FlashPerShare * share);
        }

        /// <summary>A flash that started at <paramref name="strength"/>, <paramref name="since"/> seconds later: down in a straight line to 0.</summary>
        public static float FlashAfter(float strength, float since)
        {
            if (since < 0f) since = 0f;
            if (since >= FlashSeconds) return 0f;
            return strength * (1f - since / FlashSeconds);
        }

        /// <summary>The flash after a new hit: the stronger of what is left of the last and the new one.</summary>
        public static float Combine(float left, float fresh) => Math.Max(left, fresh);

        /// <summary>True while she is alive and under a quarter of her maximum.</summary>
        public static bool IsLow(int health, int maxHealth) => health > 0 && maxHealth > 0 && health < maxHealth * LowHealthShare;

        /// <summary>
        /// The share of the time scale the game runs at, <paramref name="since"/> real seconds after the run ended: from
        /// 1 down to 0 in a straight line over <see cref="EndSlowSeconds"/>, then 0.
        /// </summary>
        public static float EndSlowdown(float since)
        {
            if (since <= 0f) return 1f;
            if (since >= EndSlowSeconds) return 0f;
            return 1f - since / EndSlowSeconds;
        }

        /// <summary>The end screen shows once the game has stopped.</summary>
        public static bool EndScreenShown(float since) => since >= EndSlowSeconds;
    }

    /// <summary>
    /// The two player settings of the pause menu (work order 018), kept between sessions in PlayerPrefs by the game. They
    /// never change an asset or the settings objects the practice room's values come from: each is an override, read
    /// where the value is used. Stored as whole numbers: the dash direction -1 (none: the binding asset's own choice),
    /// 0 toward the cursor, 1 the way the move keys point; the volume in tenths, 0 to 10.
    /// </summary>
    public static class SettingsRules
    {
        public const int NoOverride = -1;
        public const int DashTowardCursor = 0;
        public const int DashTowardMovement = 1;
        public const int VolumeSteps = 10;
        public const int DefaultVolumeStep = 10;

        /// <summary>Does the dash go the way she moves? The stored override if it is one, else what the bindings say.</summary>
        public static bool DashGoesTheWaySheMoves(int stored, bool bindingsSay) =>
            stored == DashTowardMovement || (stored != DashTowardCursor && bindingsSay);

        /// <summary>The stored dash value for a choice.</summary>
        public static int StoreDash(bool towardMovement) => towardMovement ? DashTowardMovement : DashTowardCursor;

        /// <summary>A stored volume step made safe: 0 to 10.</summary>
        public static int VolumeStep(int stored) => Math.Max(0, Math.Min(VolumeSteps, stored));

        /// <summary>The volume step one step up or down (arrows, clicks), stopping at 0 and 10.</summary>
        public static int StepVolume(int step, int delta) => VolumeStep(VolumeStep(step) + Math.Sign(delta));

        /// <summary>The listener's volume for a step: 0 to 1.</summary>
        public static float Volume(int step) => VolumeStep(step) / (float)VolumeSteps;

        /// <summary>The step in per cent, for the menu: "70".</summary>
        public static int Percent(int step) => VolumeStep(step) * 100 / VolumeSteps;
    }
}
