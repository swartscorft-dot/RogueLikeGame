using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// How one kind of shot looks: the basic shot, or one skill. Flat, bright shapes with hard edges:
    /// a flash at the muzzle, a tapered streak along the shot's path, a spark on every target it hurts,
    /// and for some skills a blast that shows the area. Sizes are in metres, times in seconds.
    /// A size or a time of 0 leaves that part out. All start values are guesses.
    /// </summary>
    [Serializable]
    public sealed class ShotLook
    {
        [Tooltip("The basic shot, or the name of the skill this look belongs to.")]
        public string Name = "Shot";
        [Tooltip("The colour of the outer shape of every part.")]
        public Color Rim = new Color(1f, 0.6f, 0.15f);
        [Tooltip("The colour of the bright inner shape.")]
        public Color Core = new Color(1f, 1f, 0.92f);

        [Header("Muzzle flash")]
        [Tooltip("Metres from the muzzle to the tip of the flash's long spike.")]
        public float FlashSize = 0.6f;
        public float FlashSeconds = 0.1f;

        [Header("Streak along the shot's path")]
        [Tooltip("Metres across the streak at its widest, near the end it points to.")]
        public float StreakWidth = 0.2f;
        [Tooltip("Seconds the streak takes to pull into its end and thin out. Longer lingers.")]
        public float StreakSeconds = 0.14f;
        [Tooltip("Cones only: how many streaks. Five or more are spread evenly from edge to edge; fewer go at random angles inside the cone.")]
        public int Rays = 5;
        [Tooltip("Charge skills only: how many times wider the streak and the flash are at full charge than with none.")]
        public float FullChargeScale = 1f;

        [Header("Hit spark on every target that takes damage")]
        public float HitSize = 0.8f;
        public float HitSeconds = 0.14f;

        [Header("Blast that shows the area (cones and circles)")]
        [Tooltip("Seconds of the blast: a wedge over the cone, or a ring and a burst on the circle. 0 means none.")]
        public float BlastSeconds;

        [Header("Sound")]
        [Tooltip("The sound of this shot: once per shot, per burst hit and per hold tick, on hits and misses alike. None: silent.")]
        public AudioClip Sound;
        [Tooltip("0 to 1, before the master volume in the settings.")]
        [Range(0f, 1f)] public float SoundVolume = 1f;
        public float SoundPitch = 1f;
        [Tooltip("Each shot's pitch moves by up to this share up or down at random (0.05 = 5 %), so quick shots do not sound like a machine.")]
        public float SoundPitchSpread;
        [Tooltip("Seconds into the clip to start: past its leading silence, so the bang lands with the flash. The builder sets it from the clip.")]
        public float SoundStart;

        [Header("Recoil of the pistol that fired")]
        [Tooltip("How hard the pistol kicks, as a share of the base kick in the settings (angle and pull-back). 0: no kick. A charge skill kicks harder with the charge, as its flash grows.")]
        public float Recoil = 1f;

        public ShotLook Copy() => (ShotLook)MemberwiseClone();
    }

    /// <summary>
    /// The looks of all the shots, in one asset: the basic shot and one per skill, found by the skill's name.
    /// Effects read their look when they start, so a change in the Inspector shows from the next shot while
    /// playing, and is kept. The builder creates the asset when it is missing, adds looks for skills that have
    /// none, and never overwrites what is there.
    /// </summary>
    [CreateAssetMenu(menuName = "Roguelike/Shot Effects", fileName = "ShotFx")]
    public sealed class ShotFxSettings : ScriptableObject
    {
        public const float StartCoreGlow = 4f;
        public const float StartRimGlow = 1.8f;
        public const float StartEmpoweredScale = 1.25f;
        public const float StartRecoilAngle = 16f;
        public const float StartRecoilPullBack = 0.07f;
        public const float StartRecoilKickSeconds = 0.04f;
        public const float StartRecoilSettleSeconds = 0.16f;
        public static readonly Color StartEmpoweredRim = new Color(1f, 0.2f, 0.6f);

        public ShotLook Basic = ShotFxDefaults.Basic();
        public ShotLook[] Skills = ShotFxDefaults.AllSkills();

        [Header("Glow under the camera's bloom")]
        [Tooltip("How many times brighter than white the inner shapes are drawn. Above the bloom threshold (1.2) they glow. 1 means no glow. Start value 4, a guess.")]
        public float CoreGlow = StartCoreGlow;
        [Tooltip("How many times brighter the outer, coloured shapes are drawn. A little above the threshold gives a halo in their own colour. 1 means no glow. Start value 1.8, a guess.")]
        public float RimGlow = StartRimGlow;

        [Header("Recoil: the base kick, scaled by each look's Recoil (start values, guesses)")]
        [Tooltip("Degrees the muzzle of the pistol that fired flips up at the peak of the kick.")]
        public float RecoilAngle = StartRecoilAngle;
        [Tooltip("Metres the hand comes back toward the shoulder at the peak, the elbow giving.")]
        public float RecoilPullBack = StartRecoilPullBack;
        [Tooltip("Seconds from the shot to the peak of the kick.")]
        public float RecoilKickSeconds = StartRecoilKickSeconds;
        [Tooltip("Seconds from the peak back onto the target.")]
        public float RecoilSettleSeconds = StartRecoilSettleSeconds;

        [Header("Sound")]
        [Tooltip("Master volume of all the shot sounds, 0 to 1.")]
        [Range(0f, 1f)] public float SoundVolume = 1f;
        [Tooltip("Empowered shots are pitched by this factor: below 1 is deeper.")]
        public float EmpoweredPitch = 0.94f;

        [Header("Empowered mode (focus spent)")]
        [Tooltip("Every part is this many times larger while empowered. 1 means no change.")]
        public float EmpoweredScale = StartEmpoweredScale;
        [Tooltip("The rim colour of every part while empowered.")]
        public Color EmpoweredRim = StartEmpoweredRim;

        /// <summary>The look for a skill, by name, or null.</summary>
        public ShotLook For(string skillName) => Skills?.FirstOrDefault(l => l != null && l.Name == skillName);

        /// <summary>Adds a look with its start values for every skill that has none. True if any was added.</summary>
        public bool EnsureLooks(IEnumerable<string> skillNames)
        {
            var list = (Skills ?? new ShotLook[0]).Where(l => l != null).ToList();
            bool changed = Basic == null || list.Count != (Skills?.Length ?? -1);
            if (Basic == null) Basic = ShotFxDefaults.Basic();
            foreach (string name in skillNames)
            {
                if (list.Any(l => l.Name == name)) continue;
                list.Add(ShotFxDefaults.For(name));
                changed = true;
            }
            if (changed) Skills = list.ToArray();
            return changed;
        }

        [ContextMenu("Reset to the start values")]
        void ResetToStartValues()
        {
            Basic = ShotFxDefaults.Basic();
            Skills = ShotFxDefaults.AllSkills();
            CoreGlow = StartCoreGlow;
            RimGlow = StartRimGlow;
            EmpoweredScale = StartEmpoweredScale;
            EmpoweredRim = StartEmpoweredRim;
            RecoilAngle = StartRecoilAngle;
            RecoilPullBack = StartRecoilPullBack;
            RecoilKickSeconds = StartRecoilKickSeconds;
            RecoilSettleSeconds = StartRecoilSettleSeconds;
        }
    }

    /// <summary>The start values of the looks. All guesses, chosen by looking at captures from the game camera.</summary>
    public static class ShotFxDefaults
    {
        static ShotLook Look(string name, Color rim, float flash, float flashSeconds, float streak, float streakSeconds,
                             float hit, float hitSeconds, int rays = 5, float blast = 0f, float charge = 1f, float recoil = 1f) =>
            new ShotLook
            {
                Name = name, Rim = rim, FlashSize = flash, FlashSeconds = flashSeconds, StreakWidth = streak,
                StreakSeconds = streakSeconds, HitSize = hit, HitSeconds = hitSeconds, Rays = rays,
                BlastSeconds = blast, FullChargeScale = charge, Recoil = recoil,
            };

        static ShotLook Burst(ShotLook look, Color core)
        {
            look.Core = core;
            return look;
        }

        public static ShotLook Basic() =>
            Look("Basic shot", new Color(1f, 0.85f, 0.3f), 0.45f, 0.07f, 0.12f, 0.1f, 0.6f, 0.12f);

        static readonly string[] Names =
            { "Quick Shot", "Powder Keg", "Fan Fire", "Long Shot", "Scatter Blast", "Heavy Volley", "Backstep", "Triple Tap" };

        public static ShotLook[] AllSkills() => Names.Select(For).ToArray();

        /// <summary>
        /// The existing skill whose look and sound a pool skill of work order 015 (2026-11-20) borrowed while the ShotFx
        /// asset had none of its own: the nearest in weapon and shape. "" for any other name. Since work order 024 it has a
        /// look of its own (<see cref="For"/>) and borrows only the sound, or all of it with the effects switched off
        /// (<see cref="Borrowed"/>).
        /// </summary>
        public static string Nearest(string name)
        {
            switch (name)
            {
                case "Tag Round": return "Quick Shot";        // a light pistol line
                case "Breaker Slug": return "Scatter Blast";  // the shotgun
                case "Stopping Shot": return "Backstep";      // a pistol line with a heavier kick
                case "Slide": return "Backstep";              // it fires nothing; the nearest move
                default: return "";
            }
        }

        /// <summary>A pool skill's look as it was before work order 024: its nearest's, under its own name.</summary>
        public static ShotLook Borrowed(string name)
        {
            string nearest = Nearest(name);
            if (nearest.Length == 0) return For(name);
            ShotLook borrowed = For(nearest);
            borrowed.Name = name;
            return borrowed;
        }

        /// <summary>Gives a look the sound of another, all its settings (work order 024: a pool skill's own look keeps its nearest's sound).</summary>
        public static void TakeSound(ShotLook look, ShotLook from)
        {
            if (look == null || from == null) return;
            look.Sound = from.Sound;
            look.SoundVolume = from.SoundVolume;
            look.SoundPitch = from.SoundPitch;
            look.SoundPitchSpread = from.SoundPitchSpread;
            look.SoundStart = from.SoundStart;
        }

        public static ShotLook For(string name)
        {
            switch (name)
            {
                case "Quick Shot":    return Look(name, new Color(1f, 0.5f, 0.1f), 1f, 0.1f, 0.4f, 0.16f, 1f, 0.16f, recoil: 1.5f);
                case "Powder Keg":    return Burst(Look(name, new Color(0.9f, 0.15f, 0.05f), 0f, 0f, 0f, 0f, 0.8f, 0.15f, blast: 0.35f, recoil: 0f), new Color(1f, 0.95f, 0.5f));
                case "Fan Fire":      return Look(name, new Color(1f, 0.85f, 0.2f), 0.7f, 0.08f, 0.22f, 0.11f, 0.6f, 0.1f, rays: 4, recoil: 0.5f);
                case "Long Shot":     return Look(name, new Color(0.15f, 0.3f, 1f), 1.1f, 0.12f, 0.45f, 0.4f, 1.3f, 0.2f, charge: 2f, recoil: 1.3f);
                case "Scatter Blast": return Look(name, new Color(1f, 0.65f, 0.1f), 1.3f, 0.1f, 0.22f, 0.12f, 1f, 0.15f, rays: 5, blast: 0.22f, recoil: 2f);
                case "Heavy Volley":  return Look(name, new Color(1f, 0.25f, 0.15f), 2f, 0.14f, 0.4f, 0.22f, 1.3f, 0.2f, recoil: 2.2f);
                case "Backstep":      return Look(name, new Color(0.2f, 0.95f, 0.95f), 1f, 0.1f, 0.36f, 0.16f, 1f, 0.16f, recoil: 1.4f);
                case "Triple Tap":    return Look(name, new Color(0.65f, 0.4f, 1f), 0.85f, 0.08f, 0.32f, 0.1f, 0.9f, 0.12f);
                // The pool skills of work order 015, with looks of their own since work order 024 (2026-11-20): Tag Round a
                // thin sharp streak with a green-tinted core; Breaker Slug a short, very thick streak with a wide heavy
                // flash; Stopping Shot a clean pale streak; Slide fires nothing (its dust and trail are CombatEffects').
                case "Tag Round":     return Burst(Look(name, new Color(1f, 0.72f, 0.2f), 0.75f, 0.08f, 0.14f, 0.13f, 0.8f, 0.13f, recoil: 1.2f), new Color(0.82f, 1f, 0.62f));
                case "Breaker Slug":  return Look(name, new Color(1f, 0.42f, 0.08f), 1.9f, 0.12f, 0.75f, 0.12f, 1.3f, 0.17f, recoil: 2.4f);
                case "Stopping Shot": return Burst(Look(name, new Color(0.96f, 0.92f, 0.82f), 0.95f, 0.09f, 0.3f, 0.15f, 1f, 0.15f, recoil: 1.5f), Color.white);
                case "Slide":         return Look(name, new Color(1f, 0.6f, 0.2f), 0f, 0f, 0f, 0f, 0f, 0f, recoil: 0f);
                default:              return Look(name, new Color(1f, 0.6f, 0.15f), 0.6f, 0.1f, 0.2f, 0.14f, 0.8f, 0.14f);
            }
        }
    }
}
