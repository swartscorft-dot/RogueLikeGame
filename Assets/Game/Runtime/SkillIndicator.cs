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
        const int ArcSegments = 32;
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
            bool show = skill != null && (phase == SkillPhase.Startup || phase == SkillPhase.Channel);
            if (_line.enabled != show) _line.enabled = show;
            if (!show) return;

            Vector3 origin = Ground(Player.Position);
            Vector3 toAim = Ground(Player.AimPoint) - origin;
            Vector3 dir = toAim.sqrMagnitude > 1e-6f ? toAim.normalized : Ground(Player.transform.forward).normalized;
            _line.widthMultiplier = BaseWidth * (skill.Input == SkillInput.Charge ? 1f + 2f * Player.SkillCharge : 1f);

            switch (skill.Shape)
            {
                case SkillShape.Circle:
                    GroundPoint c = SkillRules.CircleCenter(skill, Player.GroundPosition,
                                                            new GroundPoint(Player.AimPoint.x, Player.AimPoint.z));
                    DrawCircle(new Vector3(c.X, 0f, c.Z), skill.Radius);
                    break;
                case SkillShape.Cone:
                    DrawCone(origin, dir, skill.Range, skill.Angle);
                    break;
                default:
                    DrawStrip(origin, dir, skill.Range, skill.HalfWidth);
                    break;
            }
        }

        void DrawStrip(Vector3 origin, Vector3 dir, float range, float halfWidth)
        {
            Vector3 side = Vector3.Cross(Vector3.up, dir) * halfWidth;
            _line.positionCount = 4;
            _line.SetPosition(0, Lift(origin - side));
            _line.SetPosition(1, Lift(origin + side));
            _line.SetPosition(2, Lift(origin + dir * range + side));
            _line.SetPosition(3, Lift(origin + dir * range - side));
        }

        void DrawCircle(Vector3 center, float radius)
        {
            _line.positionCount = ArcSegments;
            for (int i = 0; i < ArcSegments; i++)
            {
                float a = i / (float)ArcSegments * Mathf.PI * 2f;
                _line.SetPosition(i, Lift(center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius));
            }
        }

        void DrawCone(Vector3 origin, Vector3 dir, float range, float angle)
        {
            const int arc = 12;
            _line.positionCount = arc + 2;
            _line.SetPosition(0, Lift(origin));
            for (int i = 0; i <= arc; i++)
            {
                float yaw = Mathf.Lerp(-angle * 0.5f, angle * 0.5f, i / (float)arc);
                _line.SetPosition(i + 1, Lift(origin + Quaternion.Euler(0f, yaw, 0f) * dir * range));
            }
        }

        static Vector3 Ground(Vector3 v) => new Vector3(v.x, 0f, v.z);
        static Vector3 Lift(Vector3 v) => new Vector3(v.x, HeightAboveGround, v.z);
    }
}
