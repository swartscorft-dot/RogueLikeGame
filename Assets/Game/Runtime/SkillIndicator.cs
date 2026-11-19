using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// Draws the outline of the area the skill in use will hit, on the ground, from the press until its
    /// last hit: a strip, a circle or a wedge. It follows the aim, so a held or charged skill shows where
    /// it points. The line gets thicker as a Charge skill charges.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public sealed class SkillIndicator : MonoBehaviour
    {
        const float HeightAboveGround = 0.06f;
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
            bool show = skill != null && phase != SkillPhase.None && phase != SkillPhase.Recovery;
            if (_line.enabled != show) _line.enabled = show;
            if (!show) return;

            Vector3 origin = Ground(Player.Position);
            Vector3 toAim = Ground(Player.AimPoint) - origin;
            Vector3 dir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Ground(Player.transform.forward).normalized;
            _line.widthMultiplier = BaseWidth * (skill.Input == SkillInput.Charge ? 1f + 2f * Player.SkillCharge : 1f);

            switch (skill.Shape)
            {
                case SkillShape.Circle:
                    // Where it will land: short of cover in its way (Kristian, 2026-11-19).
                    GroundPoint c = CoverRules.CircleCenter(skill, Player.GroundPosition,
                                                            new GroundPoint(Player.AimPoint.x, Player.AimPoint.z), Player.CoverQuery);
                    GroundOutline.Circle(_line, new Vector3(c.X, 0f, c.Z), skill.Radius, HeightAboveGround);
                    break;
                case SkillShape.Cone:
                    GroundOutline.Cone(_line, origin, dir, skill.Range, skill.Angle, HeightAboveGround);
                    break;
                default:
                    GroundOutline.Strip(_line, origin, dir, skill.Range, skill.HalfWidth, HeightAboveGround);
                    break;
            }
        }

        static Vector3 Ground(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
