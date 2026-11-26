using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Draws the outline of the area the skill in use will hit, on the ground, from the press until its
    /// last hit: a strip, a circle or a wedge. It follows the aim, so a held or charged skill shows where
    /// it points. The line gets thicker as a Charge skill charges.
    ///
    /// A strip or a wedge is drawn at the gun's height (Kristian, 2026-11-23, after work order 094: "nå følger siktet autoattack
    /// riktig, men ser at spells alltid går litt under", and on the choice: "I pistol høyde blir vel det beste"): such a skill is
    /// aimed, as the shot is, at the point under the cursor's ray at the gun's height (<see cref="PlayerCharacter.ShotHeight"/>),
    /// and the camera looks down at a slant, so the same line drawn on the floor lay under the cursor on the screen. At the gun's
    /// height it passes through the cursor, as the shot and the Laser Sight do. It is ONE height (<see cref="LineHeight"/>), not
    /// the gun's of the moment: her pistols go between about 1.06 m and 1.49 m as her arms move and her hands take turns, and an
    /// outline that followed them would jump; at the middle it is at most a quarter of a metre from the height the cursor was
    /// met at. A circle lands on the floor under the cursor and is drawn on it. With <see cref="FeelValues.AimAtShotHeight"/>
    /// off everything is on the floor, as before.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SkillIndicator : MonoBehaviour
    {
        const float HeightAboveGround = 0.06f;
        /// <summary>The height a strip's or a wedge's outline is drawn at: the middle of where her pistols are held (m).</summary>
        public const float LineHeight = 1.25f;
        const float BaseWidth = 0.06f;

        public PlayerCharacter Player;

        LineRenderer _line;

        public bool IsVisible => _line != null && _line.enabled;

        void Awake()
        {
            _line = GetComponent<LineRenderer>();
            _line.loop = true;
            _line.useWorldSpace = true;
            _line.enabled = false;
        }

        void LateUpdate()
        {
            SkillDefinition skill = Player != null ? Player.ActiveSkill : null;
            SkillPhase phase = Player != null ? Player.SkillPhase : SkillPhase.None;
            // Work order 123: with SkillLookValues.EffectsNotShapes on, only while a key is held to aim before it fires (a Charge skill
            // charging); a round thrown over its startup is drawn in the air instead (SkillEffects). Off, as before.
            // Work order 140 (SkillLookValues.OutlineInCastAndHold, "Vis under kast og hold"): also while a skill is cast or its key is held.
            bool show = skill != null && SkillLookRules.OutlineShows(SkillLookValues.EffectsNotShapes, SkillLookValues.OutlineInCastAndHold, skill.Input, phase);
            if (SkillLookValues.EffectsNotShapes && skill != null && skill.Input == SkillInput.Point && phase == SkillPhase.Startup) SkillEffects.Ensure().Watch(Player);
            if (_line.enabled != show) _line.enabled = show;
            if (!show) return;

            Vector3 origin = Ground(Player.Position);
            Vector3 toAim = Ground(Player.AimPoint) - origin;
            Vector3 dir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Ground(Player.transform.forward).normalized;
            _line.widthMultiplier = BaseWidth * (skill.Input == SkillInput.Charge ? 1f + 2f * Player.SkillCharge : 1f);
            float lineHeight = Player.Feel.AimAtShotHeight ? LineHeight : HeightAboveGround;

            // Work order 140: Deadeye's is the circle of its reach round her, where it picks the marked enemies it shoots as its cast ends.
            if (SkillLookRules.OutlineIsReach(skill, SkillLookValues.EffectsNotShapes, SkillLookValues.OutlineInCastAndHold))
            {
                GroundOutline.Circle(_line, origin, skill.Range, HeightAboveGround);
                return;
            }

            switch (skill.Shape)
            {
                case SkillShape.Circle:
                    // Where it will land: short of cover in its way (Kristian, 2026-11-19).
                    GroundPoint c = CoverRules.CircleCenter(skill, Player.GroundPosition,
                                                            new GroundPoint(Player.AimPoint.x, Player.AimPoint.z), Player.CoverQuery);
                    GroundOutline.Circle(_line, new Vector3(c.X, 0f, c.Z), skill.Radius, HeightAboveGround);
                    break;
                case SkillShape.Cone:
                    GroundOutline.Cone(_line, origin, dir, skill.Range, skill.Angle, lineHeight);
                    break;
                default:
                    GroundOutline.Strip(_line, origin, dir, skill.Range, skill.HalfWidth, lineHeight);
                    break;
            }
        }

        static Vector3 Ground(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
