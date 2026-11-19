using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The bars at the bottom of the screen: one box per skill with its key, name and cooldown; above them
    /// the player's health and the focus gauge; and above those a bar for the cast, hold or charge of the
    /// skill in use. Grey-box UI, drawn with IMGUI.
    /// It is laid out for a view 720 pixels high and scaled to the real height.
    /// </summary>
    public sealed class SkillBar : MonoBehaviour
    {
        const float BoxWidth = 112f;
        const float BoxHeight = 60f;
        const float Gap = 6f;
        const float BarHeight = 18f;
        public const float LayoutHeight = 720f;

        static readonly CultureInfo s_Inv = CultureInfo.InvariantCulture;
        static readonly Color s_Ready = new Color(1f, 1f, 1f, 1f);
        static readonly Color s_Waiting = new Color(1f, 1f, 1f, 0.45f);
        static readonly Color s_InUse = new Color(1f, 0.75f, 0.2f, 1f);
        static readonly Color s_Back = new Color(0f, 0f, 0f, 0.6f);
        static readonly Color s_Health = new Color(0.85f, 0.2f, 0.2f, 1f);
        static readonly Color s_Focus = new Color(0.25f, 0.6f, 1f, 1f);
        static readonly Color s_Empowered = new Color(1f, 0.9f, 0.3f, 1f);
        static readonly Color s_DarkText = new Color(0.1f, 0.1f, 0.1f, 1f);
        static readonly Color s_Empty = new Color(1f, 1f, 1f, 0.18f);
        static readonly Color s_CleanZone = new Color(0.3f, 1f, 0.45f, 0.55f);
        static readonly Color s_Clean = new Color(0.4f, 1f, 0.5f, 1f);
        const float RecastBarHeight = 9f;

        public PlayerCharacter Player;
        public PlayerHealth Health;
        PlayerInputDriver _driver;

        GUIStyle _box;
        GUIStyle _label;

        void OnGUI()
        {
            if (Player == null) Player = FindAnyObjectByType<PlayerCharacter>();
            if (Player == null) return;
            if (Health == null) Health = Player.GetComponent<PlayerHealth>();
            if (_box == null)
            {
                _box = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                _label = new GUIStyle(GUI.skin.label) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            }

            float scale = Screen.height / LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float viewWidth = Screen.width / scale;
            Color before = GUI.color;

            IReadOnlyList<SkillDefinition> skills = Player.Skills;
            if (_driver == null) _driver = Player.GetComponent<PlayerInputDriver>();
            InputBindings bindings = _driver != null ? _driver.ActiveBindings : null;
            int count = bindings != null && bindings.Skills != null ? Mathf.Min(skills.Count, bindings.Skills.Length) : skills.Count;
            float total = count * BoxWidth + (count - 1) * Gap;
            float left = (viewWidth - total) * 0.5f;
            float top = LayoutHeight - BoxHeight - 14f;
            int active = Player.ActiveSkillSlot;
            int recovering = Player.FreeRecoverySlot; // decision 0004: its recovery runs, but she is free to shoot and move

            for (int slot = 0; slot < count; slot++)
            {
                var box = new Rect(left + slot * (BoxWidth + Gap), top, BoxWidth, BoxHeight);
                string key = bindings != null ? bindings.SkillLabel(slot) : "";
                // A slot with no skill yet (decision 0005) is an empty, dim box with its key.
                if (!Player.IsEquipped(slot))
                {
                    GUI.color = s_Empty;
                    GUI.Box(box, key + "\nempty", _box);
                    continue;
                }
                float cooldown = Player.SkillCooldownRemaining(slot);
                bool chain = slot == Player.RecastSlot;
                string state = slot == active ? Player.SkillPhase.ToString()
                    : chain && Player.IsRecastWindowOpen && skills[slot].OpeningMove ? "shoot" // Backstep after its hop (2026-11-19)
                    : chain && Player.IsRecastWindowOpen ? string.Format(s_Inv, "tap {0} / {1}", Player.RecastShots + 1, skills[slot].RecastCount)
                    : slot == recovering ? "Recovery (free)"
                    : cooldown > 0f ? string.Format(s_Inv, "{0:0.0} s", cooldown) : "ready";
                GUI.color = slot == active || slot == recovering || chain ? s_InUse : cooldown > 0f ? s_Waiting : s_Ready;
                GUI.Box(box, key + "  " + skills[slot].Name + "\n" + state, _box);
                if (chain && Player.IsRecastWindowOpen) RecastBar(new Rect(box.x + 4f, box.yMax - RecastBarHeight - 3f, box.width - 8f, RecastBarHeight));
            }

            // Health to the left, focus to the right.
            float rowTop = top - BarHeight - 6f;
            float half = (total - Gap) * 0.5f;
            if (Health != null)
            {
                string text = Health.IsAlive
                    ? string.Format(s_Inv, "HP {0} / {1}", Health.Current, Health.MaxHealth)
                    : "DEAD";
                Bar(new Rect(left, rowTop, half, BarHeight), Health.MaxHealth > 0 ? Health.Current / (float)Health.MaxHealth : 0f, s_Health, text);
            }
            IdentityValues identity = Player.Identity;
            string focusText = Player.IsEmpowered
                ? string.Format(s_Inv, "EMPOWERED {0:0.0} s", Player.EmpoweredRemaining)
                : Player.IsFocusFull ? "FOCUS FULL: press Z"
                : string.Format(s_Inv, "Focus {0:0} / {1:0}", Player.Focus, identity.MaxFocus);
            bool bright = Player.IsEmpowered || Player.IsFocusFull;
            Bar(new Rect(left + half + Gap, rowTop, half, BarHeight), Player.FocusBarFill,
                bright ? s_Empowered : s_Focus, focusText, bright ? s_DarkText : s_Ready);

            // The cast, hold or charge of the skill in use.
            SkillDefinition skill = Player.ActiveSkill;
            SkillPhase phase = Player.SkillPhase;
            if (skill != null && (phase == SkillPhase.Startup || phase == SkillPhase.Channel))
                Bar(new Rect(left, rowTop - BarHeight - 6f, total, BarHeight), Player.SkillProgress, s_InUse, BarText(skill, phase));

            GUI.color = before;
            GUI.matrix = matrixBefore;
        }

        /// <summary>
        /// The recast window over its skill's box (decision 0005): a bar that runs out from right to left. For the
        /// last press, its clean part is marked at the left end, where the bar runs out: press while the bar's end is
        /// inside the bright zone. The bar turns the clean colour once it is there.
        /// </summary>
        void RecastBar(Rect rect)
        {
            float left = Player.RecastWindowLeft;
            float clean = Player.RecastCleanShare;
            GUI.color = s_Back;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            if (clean > 0f)
            {
                GUI.color = s_CleanZone;
                GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * clean, rect.height), Texture2D.whiteTexture);
            }
            bool inClean = clean > 0f && left <= clean;
            GUI.color = inClean ? s_Clean : s_InUse;
            float inset = clean > 0f ? 2f : 0f; // the bar is drawn thinner over the zone, so the zone shows round it
            GUI.DrawTexture(new Rect(rect.x, rect.y + inset, rect.width * Mathf.Clamp01(left), rect.height - 2f * inset), Texture2D.whiteTexture);
            GUI.color = s_Ready;
        }

        void Bar(Rect rect, float fill, Color color, string text) => Bar(rect, fill, color, text, s_Ready);

        void Bar(Rect rect, float fill, Color color, string text, Color textColor)
        {
            GUI.color = s_Back;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = s_Ready;
            Color contentBefore = GUI.contentColor;
            GUI.contentColor = textColor;
            GUI.Label(rect, text, _label);
            GUI.contentColor = contentBefore;
        }

        static string BarText(SkillDefinition skill, SkillPhase phase)
        {
            if (phase == SkillPhase.Channel) return skill.Input == SkillInput.Charge ? "charging" : "holding";
            return skill.Input == SkillInput.Casting ? "casting" : skill.Name;
        }
    }
}
