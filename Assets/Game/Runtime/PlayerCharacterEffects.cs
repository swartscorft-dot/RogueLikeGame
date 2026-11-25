using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;

namespace Roguelike
{
    /// <summary>
    /// The effects of <see cref="PlayerCharacter"/>'s shots: their looks, sounds, muzzles, flashes, streaks, hits and kicks
    /// (work order 081 moved them here from PlayerCharacter.cs, code unchanged).
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        // ---------------- Effects: flat, bright shapes that show a shot, and a disc for an area ----------------
        // They are pictures only. The hit has landed when they appear, and nothing they do changes it.
        // How each shot looks is in the ShotFx settings asset, read at the moment of the shot.

        /// <summary>A look as it is used for one shot: scaled by the charge and by the empowered mode, with its glow.</summary>
        struct Look
        {
            public ShotLook Values;
            public float Scale;     // every size
            public float Width;     // streak width and flash size on top of Scale: the charge
            public ShotPaint Paint;
        }

        /// <summary>The look's sound, once, in this frame: its volume times the master, its pitch spread at random.</summary>
        void PlaySound(Look look)
        {
            ShotLook values = look.Values;
            if (Sounds == null || values == null || values.Sound == null) return;
            float master = ShotLooks != null ? ShotLooks.SoundVolume : 1f;
            float spread = Mathf.Max(0f, values.SoundPitchSpread);
            float pitch = values.SoundPitch * (1f + Random.Range(-spread, spread));
            if (IsFocusBoosted && ShotLooks != null) pitch *= ShotLooks.EmpoweredPitch; // work order 071: for as long as the gains run
            Sounds.Play(values.Sound, values.SoundVolume * master, pitch, values.SoundStart);
        }

        /// <summary>
        /// The basic shot's sound (work order 064: "blir veldig slitsomt for ørene å høre samme lyd gjenta seg"): one of its
        /// variations, never the same twice running (<see cref="ShotVariationPicker"/>), with its jitter and 3 dB quieter;
        /// with the variations off (<see cref="ShotVariationValues.On"/>), exactly as before.
        /// </summary>
        void PlayBasicShotSound(Look look)
        {
            ShotLook values = look.Values;
            if (ShotVariations == null || !ShotVariations.On || Sounds == null || values == null || values.Sound == null)
            {
                PlaySound(look);
                return;
            }
            float master = ShotLooks != null ? ShotLooks.SoundVolume : 1f;
            ShotVariation v = _shotVariations.Next(ShotVariations);
            AudioClip own = ShotVariationClips.For(ShotVariations, v.Index);
            AudioClip clip = own != null ? own : values.Sound;
            float pitch = values.SoundPitch * v.Pitch;
            if (IsFocusBoosted && ShotLooks != null) pitch *= ShotLooks.EmpoweredPitch; // work order 071: for as long as the gains run
            Sounds.Play(clip, values.SoundVolume * master * v.Volume, pitch, own != null ? 0f : values.SoundStart, v);
        }

        /// <summary>The basic shot's variations (work order 064): defaults in code, reachable in the Inspector while playing.</summary>
        public ShotVariationValues ShotVariations = new ShotVariationValues();
        readonly ShotVariationPicker _shotVariations = new ShotVariationPicker();
        /// <summary>The variations' stream starts again from this seed (tests).</summary>
        public void SeedShotVariations(ulong seed) => _shotVariations.Seed(seed);

        Look LookFor(SkillDefinition skill, float charge)
        {
            ShotLook values = skill == null
                ? (ShotLooks != null && ShotLooks.Basic != null ? ShotLooks.Basic : _fallbackBasicLook)
                : (ShotLooks != null ? ShotLooks.For(skill.Name) : null)
                  // Work order 024: a pool skill the asset has no look for takes its own from the defaults in memory; only
                  // with the effects switched off does it borrow its nearest's, sound and all, as before (work order 015).
                  ?? (CombatEffects.PoolLooksBorrowed && ShotLooks != null ? ShotLooks.For(ShotFxDefaults.Nearest(skill.Name)) : null)
                  ?? FallbackLook(skill.Name);
            // Work order 071: empowered for as long as the gains run (a Hair Trigger part's seconds too); with the focus feel
            // running, larger and in its teal (FocusFeel.ShotLookNow), else the asset's empowered look as before.
            bool empowered = IsFocusBoosted;
            FocusFeelValues felt = empowered ? FocusFeel.ShotLookNow : null;
            float empoweredScale = felt != null ? felt.ShotScale : ShotLooks != null ? ShotLooks.EmpoweredScale : ShotFxSettings.StartEmpoweredScale;
            Color rim = !empowered ? values.Rim
                      : felt != null ? new Color(felt.ShotRimR, felt.ShotRimG, felt.ShotRimB)
                      : (ShotLooks != null ? ShotLooks.EmpoweredRim : ShotFxSettings.StartEmpoweredRim);
            return new Look
            {
                Values = values,
                Scale = empowered ? empoweredScale : 1f,
                Width = skill != null && skill.Input == SkillInput.Charge ? Mathf.Lerp(1f, values.FullChargeScale, charge) : 1f,
                Paint = new ShotPaint(rim, values.Core,
                                      ShotLooks != null ? ShotLooks.RimGlow : ShotFxSettings.StartRimGlow,
                                      ShotLooks != null ? ShotLooks.CoreGlow : ShotFxSettings.StartCoreGlow),
            };
        }

        ShotLook FallbackLook(string skillName)
        {
            bool borrowed = CombatEffects.PoolLooksBorrowed;
            Dictionary<string, ShotLook> looks = borrowed ? _borrowedLooks : _fallbackLooks;
            if (!looks.TryGetValue(skillName, out ShotLook look))
                looks[skillName] = look = borrowed ? ShotFxDefaults.Borrowed(skillName) : ShotFxDefaults.For(skillName);
            // Its own look keeps the sound of its nearest in the asset until the asset has a look of its own for it (work order 024).
            ShotLook nearest = !borrowed && ShotLooks != null ? ShotLooks.For(ShotFxDefaults.Nearest(skillName)) : null;
            if (nearest != null) ShotFxDefaults.TakeSound(look, nearest);
            return look;
        }

        /// <summary>How many streaks go side by side for a line of this width: one, or one per spacing across it.</summary>
        public static int StreaksAcross(float width) => Mathf.Max(1, Mathf.FloorToInt(width / LineStreakSpacing + 1e-4f));

        /// <summary>
        /// The barrel end the next shot fires from, taking turns between the hands. Without pistols, a point
        /// over the character's ground point at the height the old beams were drawn at.
        /// </summary>
        Vector3 NextMuzzle(out bool leftHand)
        {
            Transform muzzle = _leftHandNext ? LeftMuzzle : RightMuzzle;
            if (muzzle == null) muzzle = _leftHandNext ? RightMuzzle : LeftMuzzle;
            leftHand = muzzle != null && muzzle == LeftMuzzle;
            _leftHandNext = !_leftHandNext;
            if (muzzle != null) return muzzle.position;
            Vector3 p = transform.position;
            return new Vector3(p.x, FallbackMuzzleHeight, p.z);
        }

        /// <summary>The effects of one hit of a skill. Returns the height and place the hit sparks go from.</summary>
        Vector3 SpawnSkillFx(SkillDefinition skill, Look look, SkillArea area)
        {
            GroundPoint origin = area.Origin, aim = area.Aim;
            Vector3 dir = new Vector3(aim.X - origin.X, 0f, aim.Z - origin.Z);
            if (dir.sqrMagnitude > 1e-8f) dir.Normalize();

            if (skill.Shape == SkillShape.Circle)
            {
                if (SkillLookValues.EffectsNotShapes) return SkillLookCircle(skill, look, area); // work order 123: no rings on the ground
                // The area hit is a thin ring at the circle's edge. The bang is a big burst over the middle,
                // drawn in front of whatever stands in it, and a shockwave that runs out to the edge. It is where the
                // circle landed: short of cover in its way (Kristian, 2026-11-19).
                GroundPoint c = area.Centre;
                var centre = new Vector3(c.X, 0f, c.Z);
                float blast = look.Values.BlastSeconds;
                ShotFx.Outline(centre, skill.Radius, blast * 1.6f, look.Paint, ShotFxMaterial);
                ShotFx.Ring(centre, skill.Radius, blast, look.Paint, ShotFxMaterial);
                ShotFx.Spark(centre + Vector3.up * 1f, skill.Radius * 1.1f * look.Scale, blast, look.Paint, ShotFxMaterial,
                             towardCamera: skill.Radius + 1f);
                return centre + Vector3.up * 1f;
            }

            // Streaks run level, at the height of the muzzle, and end where the area ends: the range is
            // measured from the character, as the hit is.
            Vector3 muzzle = NextMuzzle(out bool leftHand);
            Kick(leftHand, look);
            Vector3 level = new Vector3(origin.X, muzzle.y, origin.Z);
            SpawnFlash(look, muzzle, dir);
            if (skill.Shape == SkillShape.Cone)
            {
                if (SkillLookValues.EffectsNotShapes) return SkillLookCone(skill, look, muzzle, level, dir); // work order 123: no wedge
                int rays = Mathf.Max(1, look.Values.Rays);
                for (int i = 0; i < rays; i++)
                {
                    // Five or more spread from edge to edge; fewer, as in rapid fire, go anywhere in the cone.
                    float yaw = rays >= 5
                        ? Mathf.Lerp(-skill.Angle * 0.5f, skill.Angle * 0.5f, i / (rays - 1f))
                        : Random.Range(-skill.Angle * 0.5f, skill.Angle * 0.5f);
                    // A streak that meets cover ends at it, as the hit does (Kristian, 2026-11-19).
                    Vector3 rayDir = Quaternion.Euler(0f, yaw, 0f) * dir;
                    float reach = skill.Range;
                    if (CoverQuery != null) reach = Cover.ClearDistance(level, rayDir, reach, Cover.RayHeight);
                    SpawnStreak(look, muzzle, level + rayDir * reach, 1f);
                }
                // The blast comes out of the gun, level with the streaks, so it reads as one with them from the
                // camera. It reaches as far as the cone does.
                Vector3 apex = level + dir * Mathf.Max(0f, Vector3.Dot(muzzle - level, dir));
                // A wide flat shape glowing as hard as a thin streak would bloom into a white blob: its edge glows less.
                ShotPaint blast = look.Paint;
                blast.CoreGlow = Mathf.Lerp(1f, blast.CoreGlow, 0.4f);
                ShotFx.Wedge(apex, dir, skill.Angle, skill.Range - Vector3.Distance(apex, level), look.Values.BlastSeconds,
                             blast, ShotFxMaterial);
                return muzzle;
            }

            // A line pierces everything in its strip, so its streaks run its whole range, or to the cover that stopped
            // it, with a hit spark there (Kristian, 2026-11-19). A wide one sends several side by side, spread evenly
            // across its width, all ending where its centre line ended.
            float width = skill.HalfWidth * 2f;
            int count = StreaksAcross(width);
            Vector3 across = new Vector3(dir.z, 0f, -dir.x);
            float length = area.Stop;
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = across * ((i + 0.5f) / count - 0.5f) * width;
                SpawnStreak(look, muzzle + offset, level + dir * length + offset, 1f);
            }
            if (area.Stopped)
            {
                Vector3 stop = NoteCoverStop(origin, aim, length);
                SpawnCoverHit(look, new Vector3(stop.x, muzzle.y, stop.z));
            }
            return muzzle;
        }

        /// <summary>Tells the animation that a pistol fired, which hand and how hard it kicks (the look's recoil, scaled like its flash).</summary>
        void Kick(bool leftHand, Look look)
        {
            KickLeftHand = leftHand;
            KickStrength = look.Values.Recoil * look.Scale * look.Width;
            KickCount++;
        }

        void SpawnFlash(Look look, Vector3 muzzle, Vector3 direction)
        {
            Vector3 dir = Flat(direction).sqrMagnitude > 1e-8f ? Flat(direction).normalized : Flat(transform.forward).normalized;
            // Work order 064: with the shot's weight running (never in a test or a flight unless made), a larger, shorter
            // flash, a warm light at the muzzle and a kick of the camera back along the line.
            FiringWeight weight = FiringWeight.Running;
            float size = weight != null ? weight.Values.FlashSizeScale : 1f, seconds = weight != null ? weight.Values.FlashSecondsScale : 1f;
            ShotFx.Flash(muzzle, dir, look.Values.FlashSize * look.Scale * look.Width * size, look.Values.FlashSeconds * seconds, look.Paint, ShotFxMaterial);
            if (weight != null) weight.Fired(muzzle, dir, _shotWeight);
        }

        ShotWeight _shotWeight; // the weight of the shot whose effects are being made (work order 064)

        void SpawnStreak(Look look, Vector3 from, Vector3 to, float widthScale)
        {
            ShotFx.Streak(from, to, look.Values.StreakWidth * look.Scale * look.Width * widthScale, look.Values.StreakSeconds,
                          look.Paint, ShotFxMaterial);
        }

        /// <summary>A spark on the side of a hit target that faces where the shot came from, at the shot's height.</summary>
        void SpawnHit(Look look, Damageable target, Vector3 from)
        {
            Vector3 centre = new Vector3(target.transform.position.x, from.y, target.transform.position.z);
            Vector3 back = Flat(from - centre);
            Vector3 at = back.sqrMagnitude > 1e-6f ? centre + back.normalized * Mathf.Min(target.Radius, back.magnitude) : centre;
            ShotFx.Spark(at, look.Values.HitSize * look.Scale, look.Values.HitSeconds, look.Paint, ShotFxMaterial, target.Radius + 0.2f);
        }

        /// <summary>Counts a line shot that cover stopped and returns where, on the ground, <paramref name="stop"/> metres along it.</summary>
        Vector3 NoteCoverStop(GroundPoint origin, GroundPoint aim, float stop)
        {
            GroundPoint at = CoverStopPoint(origin, aim, stop);
            ShotsStoppedByCover++;
            LastCoverStop = new Vector3(at.X, 0f, at.Z);
            return LastCoverStop;
        }

        /// <summary>The point on the ground <paramref name="stop"/> metres along a line shot: where cover stops it (the Laser Sight's line's end too, work order 060).</summary>
        GroundPoint CoverStopPoint(GroundPoint origin, GroundPoint aim, float stop) =>
            origin + LineShot.Direction(origin, aim, new GroundPoint(transform.forward.x, transform.forward.z), 1e-4f) * stop;

        /// <summary>The hit spark, on the cover that stopped a line shot (Kristian, 2026-11-19): a blocked shot reads as blocked.</summary>
        void SpawnCoverHit(Look look, Vector3 at)
        {
            ShotFx.Spark(at, look.Values.HitSize * look.Scale, look.Values.HitSeconds, look.Paint, ShotFxMaterial, 0.2f);
        }
    }
}
