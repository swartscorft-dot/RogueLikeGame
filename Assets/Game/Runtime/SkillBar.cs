using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;

namespace Roguelike
{
    /// <summary>
    /// The bars at the bottom of the screen: one box per skill with its key, name and cooldown; above them
    /// the player's health and the focus gauge; and above those a bar for the cast, hold or charge of the
    /// skill in use. Left of the skills, a small COVER box while she is in cover (an experiment, 2026-11-19). A skill with
    /// upgrades taken this run shows a small gold pip per upgrade in its box's top right corner (work order 017). The focus
    /// gauge as the focus choice makes it (work order 025): Hair Trigger's in three parts with gaps between, each part
    /// bright when full, and green with "PRIMED" and the seconds left while a priming waits; Steady Hand's one bar, its
    /// window running down on it in the empowered colour with "STEADY HAND" and the seconds left.
    /// Grey-box UI, drawn with IMGUI.
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
        const float CoverBoxWidth = 90f;
        const float PipSize = 7f;
        static readonly Color s_Pip = new Color(1f, 0.86f, 0.22f, 1f); // the elite's gold (work order 017)

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
            // One box per key, in key order, with the skill on it (work order 015: the key and the skill are apart).
            int count = bindings != null && bindings.Skills != null ? Mathf.Min(Player.KeyCount, bindings.Skills.Length) : Player.KeyCount;
            float total = count * BoxWidth + (count - 1) * Gap;
            float left = (viewWidth - total) * 0.5f;
            float top = LayoutHeight - BoxHeight - 14f;
            int active = Player.ActiveSkillSlot;
            int recovering = Player.FreeRecoverySlot; // decision 0004: its recovery runs, but she is free to shoot and move

            for (int keyIndex = 0; keyIndex < count; keyIndex++)
            {
                var box = new Rect(left + keyIndex * (BoxWidth + Gap), top, BoxWidth, BoxHeight);
                string key = bindings != null ? bindings.SkillLabel(keyIndex) : "";
                int slot = Player.SkillOnKey(keyIndex);
                // A key with no skill yet (decision 0005) is an empty, dim box with its key.
                if (slot < 0 || slot >= skills.Count || !Player.IsEquipped(slot))
                {
                    GUI.color = s_Empty;
                    GUI.Box(box, key + "\nempty", _box);
                    continue;
                }
                float cooldown = Player.SkillCooldownRemaining(slot);
                bool chain = slot == Player.RecastSlot;
                int charges = skills[slot].Charges;
                string state = slot == active ? Player.SkillPhase.ToString()
                    : chain && Player.IsRecastWindowOpen && skills[slot].OpeningMove ? "shoot" // Backstep after its hop (2026-11-19)
                    : chain && Player.IsRecastWindowOpen ? string.Format(s_Inv, "tap {0} / {1}", Player.RecastShots + 1, Player.RecastCount) // the chain's own count (work order 025)
                    : slot == recovering ? "Recovery (free)"
                    // Charges (work order 015): how many are ready, and the next one's time while one comes back.
                    : charges > 1 ? string.Format(s_Inv, "{0} / {1}", Player.SkillChargesReady(slot), charges)
                                    + (Player.SkillNextCharge(slot) > 0f ? string.Format(s_Inv, "  {0:0.0} s", Player.SkillNextCharge(slot)) : "")
                    : cooldown > 0f ? string.Format(s_Inv, "{0:0.0} s", cooldown) : "ready";
                GUI.color = slot == active || slot == recovering || chain ? s_InUse : cooldown > 0f ? s_Waiting : s_Ready;
                GUI.Box(box, key + "  " + skills[slot].Name + "\n" + state, _box);
                if (chain && Player.IsRecastWindowOpen) RecastBar(new Rect(box.x + 4f, box.yMax - RecastBarHeight - 3f, box.width - 8f, RecastBarHeight));
                PlayerSigns.DrawKeySign(box, slot); // work order 037: the clean window on its key, while it is up
                // The run's upgrades of this skill (work order 017): a pip each.
                int pips = Player.UpgradesOn(slot);
                GUI.color = s_Pip;
                for (int p = 0; p < pips; p++)
                    GUI.DrawTexture(new Rect(box.xMax - 4f - (p + 1) * (PipSize + 2f), box.y + 4f, PipSize, PipSize), Texture2D.whiteTexture);
            }

            // Taking cover (Kristian 2026-11-19, an experiment): a small box left of the skills while she is in cover,
            // bright while she is ducked, dim while a shot or a skill has her up.
            if (Player.IsInCover)
            {
                GUI.color = Player.IsDucked ? s_InUse : s_Waiting;
                GUI.Box(new Rect(left - CoverBoxWidth - Gap, top, CoverBoxWidth, BoxHeight),
                        "COVER\n" + (Player.IsDucked ? (Player.CoverIsTall ? "ducked" : "ducked, low") : "up"), _box);
            }

            // Health to the left, focus to the right.
            float rowTop = top - BarHeight - 6f;
            float half = (total - Gap) * 0.5f;
            if (Health != null)
            {
                string text = Health.IsAlive
                    ? string.Format(s_Inv, "HP {0} / {1}", Health.Current, Health.Max) // the run's upgrades included (work order 012)
                    : "DEAD";
                Bar(new Rect(left, rowTop, half, BarHeight), Health.Max > 0 ? Health.Current / (float)Health.Max : 0f, s_Health, text);
            }
            IdentityValues identity = Player.Identity;
            var focusRect = new Rect(left + half + Gap, rowTop, half, BarHeight);
            if (Player.FocusMode == FocusMode.HairTrigger) PartsGauge(focusRect, identity);
            else
            {
                string focusText = Player.IsEmpowered
                    ? string.Format(s_Inv, Player.IsSteadyHandWindow ? "STEADY HAND {0:0.0} s" : "EMPOWERED {0:0.0} s", Player.EmpoweredRemaining)
                    : Player.IsFocusFull ? PlayerSigns.FocusFullText // work order 037: the bound key ("press Z" with the sign off)
                    : string.Format(s_Inv, "Focus {0:0} / {1:0}", Player.Focus, identity.MaxFocus);
                bool bright = Player.IsEmpowered || Player.IsFocusFull;
                Bar(focusRect, Player.FocusBarFill, bright ? s_Empowered : s_Focus, focusText, bright ? s_DarkText : s_Ready);
            }
            PlayerSigns.DrawFocusSign(focusRect); // work order 037: a full gauge pulses, its key beside it

            // The cast, hold or charge of the skill in use.
            SkillDefinition skill = Player.ActiveSkill;
            SkillPhase phase = Player.SkillPhase;
            if (skill != null && (phase == SkillPhase.Startup || phase == SkillPhase.Channel))
            {
                var rect = new Rect(left, rowTop - BarHeight - 6f, total, BarHeight);
                // Long Shot's perfect band (work order 015), marked on the charge as Triple Tap's clean zone is on its
                // window: green, the bar drawn thinner over it, and the bar green while the charge is inside it.
                bool band = phase == SkillPhase.Channel && skill.Input == SkillInput.Charge && skill.PerfectTo > skill.PerfectFrom && skill.ChargeTime > 0f;
                if (band)
                {
                    float from = Mathf.Clamp01(skill.PerfectFrom / skill.ChargeTime), to = Mathf.Clamp01(skill.PerfectTo / skill.ChargeTime);
                    // Primed by Hair Trigger (work order 025): perfect from half a charge on.
                    if (Player.ActivePrime == PrimeUse.PerfectRelease)
                    {
                        from = Mathf.Clamp01(Player.FocusModeValues.PrimedChargeShare);
                        to = 1f;
                    }
                    GUI.color = s_Back;
                    GUI.DrawTexture(rect, Texture2D.whiteTexture);
                    GUI.color = s_CleanZone;
                    GUI.DrawTexture(new Rect(rect.x + rect.width * from, rect.y, rect.width * (to - from), rect.height), Texture2D.whiteTexture);
                    GUI.color = Player.InPerfectBand ? s_Clean : s_InUse;
                    GUI.DrawTexture(new Rect(rect.x, rect.y + 3f, rect.width * Mathf.Clamp01(Player.SkillProgress), rect.height - 6f), Texture2D.whiteTexture);
                    GUI.color = s_Ready;
                    GUI.Label(rect, Player.InPerfectBand ? "PERFECT: let go" : BarText(skill, phase), _label);
                }
                else Bar(rect, Player.SkillProgress, s_InUse, BarText(skill, phase));
            }

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

        /// <summary>
        /// Hair Trigger's gauge (work order 025): its parts side by side with a gap between, each filled by the focus in
        /// it and bright when full; while a priming waits the whole gauge is framed green and says so, with the seconds left.
        /// </summary>
        void PartsGauge(Rect rect, IdentityValues identity)
        {
            int parts = Mathf.Max(1, Player.FocusParts);
            float size = FocusGauge.PartSize(identity, parts);
            const float gap = 4f;
            float w = (rect.width - gap * (parts - 1)) / parts;
            bool primed = Player.IsPrimed;
            if (primed)
            {
                GUI.color = s_Clean;
                GUI.DrawTexture(new Rect(rect.x - 2f, rect.y - 2f, rect.width + 4f, rect.height + 4f), Texture2D.whiteTexture);
            }
            for (int p = 0; p < parts; p++)
            {
                var part = new Rect(rect.x + p * (w + gap), rect.y, w, rect.height);
                GUI.color = s_Back;
                GUI.DrawTexture(part, Texture2D.whiteTexture);
                float fill = size > 0f ? Mathf.Clamp01((Player.Focus - p * size) / size) : 0f;
                GUI.color = fill >= 1f ? s_Empowered : s_Focus;
                GUI.DrawTexture(new Rect(part.x, part.y, part.width * fill, part.height), Texture2D.whiteTexture);
            }
            int full = Player.FocusPartsFull;
            string text = primed ? string.Format(s_Inv, "PRIMED {0:0.0} s: next skill", Player.PrimeRemaining)
                        : full > 0 ? string.Format(s_Inv, "{0} / {1} parts: press Z", full, parts)
                        : string.Format(s_Inv, "Focus {0:0} / {1:0}", Player.Focus, identity.MaxFocus);
            GUI.color = s_Ready;
            GUI.Label(rect, text, _label);
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
