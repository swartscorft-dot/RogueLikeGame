using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Roguelike
{
    /// <summary>
    /// What <see cref="GeneratedLevels"/> draws: the box of the run's status, the chest's prompt and the choice on screen (work
    /// order 081 moved them here from GeneratedLevels.cs, code unchanged).
    /// </summary>
    public sealed partial class GeneratedLevels
    {
        /// <summary>What the box on screen says, line by line; empty when she is not in a floor.</summary>
        public string StatusText()
        {
            if (_level == null) return "";
            LevelLayout layout = _level.Layout;
            _text.Clear();
            if (InRun || ActCleared)
                _text.Append(string.Format(s_Inv, "run {0}   floor {1} of {2}   (floor seed {3})", RunSeedText, Floor, Values.FloorsPerAct, layout.SeedText)).Append('\n');
            else
                _text.Append(string.Format(s_Inv, "level {0}   floor {1}   generator v{2}", layout.SeedText, layout.Floor, layout.Version)).Append('\n');
            // The fifth review (work order 077): the rooms counted without the passages, as the end screen's record.
            _text.Append(CurrentRoom == null ? string.Format(s_Inv, "outside the level ({0} rooms)", LevelGeometry.RoomCount(layout))
                : CurrentRoom.Kind == RoomKind.Passage ? string.Format(s_Inv, "in a passage ({0} rooms)", LevelGeometry.RoomCount(layout))
                : string.Format(s_Inv, "room {0} of {1} ({2}), {3} enemies left in it", LevelGeometry.RoomNumber(layout, CurrentRoom) + 1, LevelGeometry.RoomCount(layout),
                                CurrentRoom.Kind.ToString().ToLowerInvariant(), AliveIn(CurrentRoom))).Append('\n');
            _text.Append(string.Format(s_Inv, "rooms entered {0} of {1}   exit room enemies left {2}   v{3}", _entered.Count, layout.Rooms.Count,
                                       AliveIn(layout.ExitRoom), layout.Version)).Append('\n');
            if (InRun)
            {
                _text.Append("skills taken: ").Append(Record != null ? Record.SkillsText : _skillsTaken.Count == 0 ? "none" : string.Join(", ", _skillsTaken)); // work order 057: with their tiers
                string heal = Record != null ? Record.FloorHealText : "";
                if (heal.Length > 0) _text.Append("   (").Append(heal).Append(')'); // work order 023
                _text.Append('\n');
                _text.Append("upgrades: ").Append(Modifiers.ShortText()).Append('\n');
                _text.Append("skill upgrades: ").Append(SkillUpgrades.Text()).Append('\n');
                _text.Append("focus: ").Append(FocusChoice.Taken ? FocusModeRules.Name(FocusChoice.Mode) : "today's, until the choice").Append('\n'); // work order 025
            }
            if (ActCleared) _text.Append(LastRunLine).Append('\n');
            else if (Cleared) _text.Append("FLOOR CLEARED   seed ").Append(layout.SeedText).Append('\n');
            _text.Append(DeveloperKeys.On ? "F5 new run   F6 same run   F7 practice room" : "F5 new run").Append(InRun ? "   E open a chest" : ""); // work order 066: F6 and F7 only while they work
            AppendTools(_text); // work order 084: the greybox view's legend and the room lab's lines
            return _text.ToString();
        }

        // The practice room's box as last made, and what it was made of (work order 081).
        string _practiceText;
        ulong? _practiceSeed;
        FocusMode _practiceFocus;
        string _practiceLastRun;
        int _practiceLines;

        void OnGUI()
        {
            if (_player == null) return;
            bool inLevel = _level != null;
            if (!inLevel && !StartedByItself && Choice == ChoiceKind.None && LastRunLine.Length == 0) return;
            GUI.depth = GuiDepth; // work order 068: an offer over everything of the fight's interface, under the screens
            if (_style == null) _style = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.UpperLeft, wordWrap = true };

            // Laid out for a view 720 pixels high and scaled to the real height, like the readout in the other corner.
            float scale = InterfaceStyle.ScreenHeight / SkillBar.LayoutHeight;
            Matrix4x4 matrixBefore = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            const float width = 330f;
            float viewWidth = InterfaceStyle.ScreenWidth / scale;
            float x = viewWidth - width - 10f;
            // The developer's box: shown or not, and how far down, as the HUD says (work order 018: F1); as before without one.
            bool kinetic = InterfaceStyle.Kinetic; // work order 045: in the interface's skin (GeneratedLevelsKinetic.cs)
            if (inLevel)
            {
                if (ShowDevBox) // the status text is built only when it is shown (work order 026)
                {
                    string text = StatusText();
                    int lines = text.Split('\n').Length;
                    GUI.Box(new Rect(x, DevBoxTop, width, 12 + lines * 15), text, DevBoxStyle());
                }
                if (kinetic) DrawChestPromptKinetic();
                else DrawChestPrompt(scale);
            }
            else if (ShowDevBox)
            {
                // Work order 081: the practice room's box is made again only when what it says changes (the run seed, the focus
                // way she tries, the last run's line), not at every event of every frame; its lines counted once with it.
                FocusMode focusNow = _player != null ? _player.PracticeFocusMode : FocusMode.Today;
                if (_practiceText == null || _practiceSeed != RunSeed || _practiceFocus != focusNow || !ReferenceEquals(_practiceLastRun, LastRunLine))
                {
                    _practiceSeed = RunSeed;
                    _practiceFocus = focusNow;
                    _practiceLastRun = LastRunLine;
                    _practiceText = "F5 new run" + (RunSeed.HasValue ? "   F6 run " + RunSeedText + " again" : "")
                                + "\nF8 boss   F9 swarmers   F10 shooter   F11 guard   F12 clear"
                                + "\n" + CasterKey + " caster   " + BomberKey + " bomber   " + WarderKey + " warder (Shift: with a pack)" // work order 034: its own line, the line above being full; 073: the bomber; 079: the warder
                                + "   " + LancerKey + " lancer" // work order 108
                                + "\n" + PracticeFocusLine() // work order 025
                                + ToolsPracticeLine // work order 084
                                + (LastRunLine.Length > 0 ? "\n" + LastRunLine : "");
                    _practiceLines = _practiceText.Split('\n').Length;
                }
                GUI.Box(new Rect(x, DevBoxTop, width, 12 + _practiceLines * 15), _practiceText, DevBoxStyle());
            }
            if (Choice != ChoiceKind.None && kinetic) DrawGuardedChoiceKinetic(); // work order 045: slim tilted cards at the right edge; 065: the pop-up in the middle
            else if (Choice != ChoiceKind.None) DrawGuardedChoice(viewWidth); // work order 042: dim, and deaf to clicks, for a moment
            else if (Notice.Length > 0 && kinetic) DrawNoticeKinetic();
            else if (Notice.Length > 0)
            {
                if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
                GUI.Box(new Rect((viewWidth - 460f) * 0.5f, SkillBar.LayoutHeight * 0.3f, 460f, 30f), Notice, _promptStyle);
            }
            if (kinetic) DrawPickFlash();
            GUI.matrix = matrixBefore;
        }

        /// <summary>A card's tier as its coloured edge (work order 050): white common, teal rare, gold epic.</summary>
        static void TierEdge(Rect r, UpgradeTier tier)
        {
            float[] c = UpgradeTiers.Rgb(tier);
            Color before = GUI.color;
            GUI.color = new Color(c[0], c[1], c[2], before.a);
            const float t = 3f;
            GUI.DrawTexture(new Rect(r.x, r.y, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.yMax - t, r.width, t), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.x, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(r.xMax - t, r.y, t, r.height), Texture2D.whiteTexture);
            GUI.color = before;
        }

        /// <summary>A small prompt over the chest she can open (work order 012).</summary>
        void DrawChestPrompt(float scale)
        {
            GeneratedLevel.PlacedProp chest = ChestPrompt;
            Camera cam = Camera.main;
            if (chest == null || cam == null) return;
            Vector3 screen = cam.WorldToScreenPoint(chest.Object.transform.position + Vector3.up * 1.4f);
            if (screen.z <= 0f) return;
            if (_promptStyle == null) _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 13, alignment = TextAnchor.MiddleCenter };
            float px = screen.x / scale, py = (InterfaceStyle.ScreenHeight - screen.y) / scale;
            GUI.Box(new Rect(px - 60f, py - 14f, 120f, 26f), InteractKey + "  open chest", _promptStyle);
        }

        /// <summary>
        /// The choice on screen (work order 012), in the middle, drawn like the other boxes: a title, and a button per
        /// option with its number; a click or the number's key picks it.
        /// </summary>
        void DrawChoice(float viewWidth)
        {
            if (_choiceStyle == null) _choiceStyle = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 8, 8) };
            if (OfferPopUp)
            {
                DrawPlainPopUp(viewWidth); // work order 065: the pop-up's words in plain boxes, side by side in the middle
                return;
            }
            if (Choice == ChoiceKind.FocusChoice)
            {
                DrawFocusChoice(viewWidth); // work order 025
                return;
            }
            PlayerCharacter player = _player;
            bool reward = Choice == ChoiceKind.FloorReward;
            bool skillUpgrade = Choice == ChoiceKind.SkillUpgrade; // work order 017
            int count = reward ? _offeredSkills.Count : skillUpgrade ? _offeredSkillUpgrades.Count : _offeredUpgrades.Count;
            float width = 520f, rowHeight = reward ? RewardRowHeight(width - 20f, _choiceStyle) : skillUpgrade ? 74f : 44f; // work order 057: a tiered card's longer text
            float height = 40f + count * (rowHeight + 6f) + 24f;
            float left = (viewWidth - width) * 0.5f, top = (SkillBar.LayoutHeight - height) * 0.5f;
            string title = reward
                ? string.Format(s_Inv, "FLOOR {0} CLEARED   choose a new skill (1-{1} or click)", Floor, count)
                : skillUpgrade
                ? string.Format(s_Inv, "ELITE ROOM CLEARED   choose a skill upgrade (1-{0} or click)", count)
                : string.Format(s_Inv, "CHEST   choose an upgrade (1-{0} or click)", count);
            GUI.Box(new Rect(left, top, width, height), title, _style);
            InputBindings bindings = player != null && player.GetComponent<PlayerInputDriver>() is PlayerInputDriver d ? d.ActiveBindings : null;
            for (int i = 0; i < count; i++)
            {
                string text;
                if (reward) text = RewardChoiceText(i); // today's text; work order 057: a rare or epic card's tier in it (GeneratedLevelsSkillTiers.cs)
                else if (skillUpgrade)
                {
                    // The skill's name and key, the upgrade's name, its kind and its line (work order 017).
                    SkillUpgradeDefinition u = _offeredSkillUpgrades[i];
                    int key = player.KeyOfSkill(player.SlotOf(u.Skill));
                    text = string.Format(s_Inv, "{0}   {1}   key {2}   {3}   [{4}]\n{5}", i + 1, u.Skill,
                                         bindings != null && key >= 0 ? bindings.SkillLabel(key) : "-", u.Name.ToUpperInvariant(), u.KindWord, u.Line);
                }
                else
                {
                    Upgrade up = _offeredUpgrades[i]; // work order 050: a tiered card says its tier first
                    text = string.Format(s_Inv, "{0}   {1}", i + 1, up.Tiered ? up.TierWord.ToUpperInvariant() + "   " + up.Text : up.Text);
                }
                var card = new Rect(left + 10f, top + 32f + i * (rowHeight + 6f), width - 20f, rowHeight);
                bool picked = GUI.Button(card, text, _choiceStyle);
                if (!reward && !skillUpgrade && _offeredUpgrades[i].Tiered) TierEdge(card, _offeredUpgrades[i].Tier); // work order 050
                else if (reward && OfferedTierAt(i) is SkillTierOffer tier && tier.Tiered) TierEdge(card, tier.Tier); // work order 057
                if (picked)
                {
                    if (reward) PickReward(i);
                    else if (skillUpgrade) PickSkillUpgrade(i);
                    else PickUpgrade(i);
                    return;
                }
            }
            GUI.Label(new Rect(left + 12f, top + height - 22f, width - 24f, 20f), PausedFooter + RedrawWords());
        }

        /// <summary>
        /// The first words of every choice's footer: "The game is paused.", and "F7 leaves the run." after it only while the
        /// developer's keys are on (work order 066: F7 is one of them, and nothing on screen names a key that is dead).
        /// </summary>
        public static string PausedFooter => DeveloperKeys.On ? "The game is paused.   F7 leaves the run." : "The game is paused.";
    }
}
