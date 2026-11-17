using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The skill bar at the bottom of the screen: one box per skill with its key, name and cooldown,
    /// and a bar above it for the cast, hold or charge of the skill in use. Grey-box UI, drawn with IMGUI.
    /// It is laid out for a view 720 pixels high and scaled to the real height.
    /// </summary>
    public sealed class SkillBar : MonoBehaviour
    {
        const float BoxWidth = 130f;
        const float BoxHeight = 64f;
        const float Gap = 8f;
        const float BarHeight = 16f;
        public const float LayoutHeight = 720f;

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Color s_Ready = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Waiting = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color s_InUse = new Color(1f, 0.75f, 0.2f, 1f);

        public PlayerCharacter Player;

        GUIStyle _box;
        GUIStyle _label;

        void OnGUI()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCharacter>();
            if (Player == null) return;
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
            }

            float scale = Screen.height / LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;

            IReadOnlyList<SkillDefinition> skills = Player.Skills;
            int count = Mathf.Min(skills.Count, PlayerInputDriver.SkillKeys.Length);
            float total = count * BoxWidth + (count - 1) * Gap;
            float left = (viewWidth - total) * 0.5f;
            float top = LayoutHeight - BoxHeight - 16f;
            int active = Player.ActiveSkillSlot;
            Color before = GUI.color;

            for (int slot = 0; slot < count; slot++)
            {
                float cooldown = Player.SkillCooldownRemaining(slot);
                string state = slot == active ? Player.SkillPhase.ToString()
                    : cooldown > 0f ? string.Format(s_Inv, "{0:0.0} s", cooldown) : "ready";
                GUI.color = slot == active ? s_InUse : cooldown > 0f ? s_Waiting : s_Ready;
                GUI.Box(new Rect(left + slot * (BoxWidth + Gap), top, BoxWidth, BoxHeight),
                        PlayerInputDriver.SkillKeys[slot] + "  " + skills[slot].Name + "\n" + state, _box);
            }

            // The cast, hold or charge of the skill in use.
            SkillDefinition skill = Player.ActiveSkill;
            SkillPhase phase = Player.SkillPhase;
            if (skill != null && (phase == SkillPhase.Startup || phase == SkillPhase.Channel))
            {
                var bar = new Rect(left, top - BarHeight - 6f, total, BarHeight);
                GUI.color = new Color(0f, 0f, 0f, 0.6f);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = s_InUse;
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Player.SkillProgress, bar.height), Texture2D.whiteTexture);
                GUI.color = s_Ready;
                GUI.Label(bar, BarText(skill, phase), _label);
            }
            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        static string BarText(SkillDefinition skill, SkillPhase phase)
        {
            if (phase == SkillPhase.Channel) return skill.Input == SkillInput.Charge ? "charging" : "holding";
            return skill.Input == SkillInput.Casting ? "casting" : skill.Name;
        }
    }
}
