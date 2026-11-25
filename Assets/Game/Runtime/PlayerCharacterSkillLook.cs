using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Work order 123 (2026-11-25, <see cref="SkillLookValues.EffectsNotShapes"/>) in <see cref="PlayerCharacter"/>: a cone's and a
    /// circle's effects with the switch on, drawn by what flies and what is thrown up instead of a flat wedge and rings on the
    /// ground, and what <see cref="SkillEffects"/> reads of her each frame (the kegs that lie, the hand a round is thrown from).
    /// Nothing here changes what a skill does: the hit has landed when these appear, as with every shot effect.
    /// </summary>
    public sealed partial class PlayerCharacter
    {
        /// <summary>Lying keg <paramref name="i"/> (of <see cref="KegsLying"/>): its middle on the floor, when it bursts by its fuse, its fuse.</summary>
        public bool LyingKegAt(int i, out Vector3 centre, out float burstAt, out float fuse)
        {
            if (i < 0 || i >= _kegs.Count)
            {
                centre = Vector3.zero;
                burstAt = fuse = 0f;
                return false;
            }
            LyingKeg k = _kegs[i];
            centre = new Vector3(k.Centre.X, 0f, k.Centre.Z);
            burstAt = k.BurstAt;
            fuse = k.Skill != null ? k.Skill.Fuse : 0f;
            return true;
        }

        /// <summary>Where a round she throws leaves from: her right pistol's hand, or over her at the old beams' height.</summary>
        public Vector3 ThrowHand => RightMuzzle != null ? RightMuzzle.position
                                  : LeftMuzzle != null ? LeftMuzzle.position
                                  : new Vector3(transform.position.x, FallbackMuzzleHeight, transform.position.z);

        /// <summary>
        /// A keg landed (work order 123, the switch on): no circle on the floor; the keg itself is drawn where it lies for as long as
        /// it lies (<see cref="SkillEffects"/>). Returns the circle it no longer makes: none.
        /// </summary>
        ShotFx SkillLookKegLanded()
        {
            SkillEffects.Ensure().Watch(this);
            return null;
        }

        /// <summary>
        /// A circle's blast with the switch on (Powder Keg's burst): the burst over its middle as before (the large spark, the room's
        /// light as the ring gave it), a ring of dust and sparks running out to exactly its radius over the blast's seconds and smoke
        /// left inside it after, in place of the ring on the ground and the outline at its edge.
        /// </summary>
        Vector3 SkillLookCircle(SkillDefinition skill, Look look, SkillArea area)
        {
            GroundPoint c = area.Centre;
            var centre = new Vector3(c.X, 0f, c.Z);
            float blast = look.Values.BlastSeconds;
            ShotFx.Spark(centre + Vector3.up * 1f, skill.Radius * 1.1f * look.Scale, blast, look.Paint, ShotFxMaterial,
                         towardCamera: skill.Radius + 1f);
            if (blast > 0f && skill.Radius > 0f) LamplitView.ShotWentOff(ShotFxKind.Ring, new Vector3(c.X, 0.06f, c.Z), Vector3.zero, skill.Radius, look.Paint.Core);
            SkillEffects.Ensure().Watch(this).Blast(centre, skill.Radius, blast);
            return centre + Vector3.up * 1f;
        }

        /// <summary>
        /// A cone's shot with the switch on (Scatter Blast, Fan Fire's every tick): its tracers from the gun out to the cone's reach
        /// (or the cover that stopped each), the first and the last on its two edges (<see cref="SkillLookRules.ConeTracerYaw"/>), gun
        /// smoke at the muzzle, and for a blast (not a hold's ticks) dust kicked up where each tracer ends, in place of the wedge; the
        /// room's light as the wedge gave it (none for a look with no blast, as before: Fan Fire's).
        /// </summary>
        Vector3 SkillLookCone(SkillDefinition skill, Look look, Vector3 muzzle, Vector3 level, Vector3 dir)
        {
            SkillLookValues v = SkillLookValues.Live;
            SkillEffects fx = SkillEffects.Ensure().Watch(this);
            int rays = Mathf.Max(1, look.Values.Rays);
            for (int i = 0; i < rays; i++)
            {
                // Unity's Random drawn exactly as often as before (once a tracer of a cone of fewer than five, as its old spread did):
                // the game's own draws that come after are the same.
                float yaw = SkillLookRules.ConeTracerYaw(i, rays, skill.Angle, rays >= 5 ? 0.5f : Random.value, v.ConeInnerJitter);
                Vector3 rayDir = Quaternion.Euler(0f, yaw, 0f) * dir;
                float reach = skill.Range;
                if (CoverQuery != null) reach = Cover.ClearDistance(level, rayDir, reach, Cover.RayHeight);
                Vector3 end = level + rayDir * reach;
                SpawnStreak(look, muzzle, end, 1f);
                if (skill.Input != SkillInput.Holding) fx.TracerEnd(end); // a blast's; a hold's ticks show by their tracers alone (quiet)
            }
            Vector3 apex = level + dir * Mathf.Max(0f, Vector3.Dot(muzzle - level, dir));
            if (look.Values.BlastSeconds > 0f)
                LamplitView.ShotWentOff(ShotFxKind.Wedge, apex, dir, skill.Range - Vector3.Distance(apex, level), look.Paint.Core);
            fx.MuzzleSmoke(muzzle, dir, skill.Input == SkillInput.Holding ? v.TickSmokePuffs : v.BlastSmokePuffs);
            return muzzle;
        }
    }
}
