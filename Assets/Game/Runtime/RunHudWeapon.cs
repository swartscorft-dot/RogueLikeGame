using System.Collections.Generic;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// Her pistols' parts on the HUD (work order 060, 2026-11-21; decision 0014, the producer's proposal, a draft Kristian has
    /// not ruled on), as part of <see cref="RunHud"/>. Nothing of it shows with the attachments off
    /// (<see cref="GeneratedLevels.AttachmentsOn"/>): the screen between runs, its lines, its rows, its keys and the end
    /// screen are then work order 029's, and the pause menu has only its line, reading "off".
    ///
    /// On, the screen between runs gets a second panel to the right of the rack's (where they fit side by side; narrower, the
    /// rack's narrows, <see cref="BetweenRunsLayout"/>), drawn with the same panel and row styles, so it is in the kinetic
    /// skin and in the old look alike: the loadout, put together there before the run (Kristian, 2026-11-21: "setter sammen
    /// et slags loadout før run"). The heading "Loadout (trial)", a line under it, then each weapon's section (in this order
    /// only "Pistols:  the basic shot"), each of its slots' heads (SIGHT, BARREL, GRIP) and a row per part: its name and what
    /// it is now ("FITTED", "owned", "price 30 Echoes", or "30 Echoes, 12 more"), its line, and what it costs, if anything. The arrow keys go on from the rack's last line into the weapon's
    /// rows in slot order, then round to the start line; Enter or a click buys a part (and fits it), fits it or takes it off
    /// (<see cref="GeneratedLevels.UseAttachment"/>). Space and Esc are as they were.
    ///
    /// The end screen adds, with the attachments on, the rooms among the run's Echoes (RunHudBetweenRuns.cs) and one line of
    /// what they buy now (<see cref="WeaponRack.NextBuy"/>); the pause menu's stats measure her start as the parts make it and
    /// name them.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The weapon's lines while the screen between runs is up with the attachments on: the heading, the line under it, each slot's head and a row per part. Else empty.</summary>
        public IReadOnlyList<string> WeaponLines => _weaponLines;
        public string WeaponText => string.Join("\n", _weaponLines);

        readonly List<string> _weaponLines = new List<string>();
        readonly List<WeaponAttachment> _weaponRows = new List<WeaponAttachment>(); // beside each line: its part, or null for a heading
        readonly List<WeaponAttachment> _weaponParts = new List<WeaponAttachment>(); // the parts in the order of their rows
        readonly List<float> _rackRowHeights = new List<float>();
        readonly GUIContent _measure = new GUIContent();

        /// <summary>Is the screen between runs up with her pistols' parts on.</summary>
        bool WeaponOn => _levels != null && _levels.BetweenRunsUp && _levels.AttachmentsOn;

        /// <summary>What the pause menu's line says: the attachments on (and so the trial too).</summary>
        bool AttachmentsLineOn
        {
            get
            {
                GeneratedLevels levels = _levels != null ? _levels : GeneratedLevels.Current;
                if (levels != null) return levels.AttachmentsOn;
                var v = new MetaValues();
                return BetweenRunsSwitch.IsOn(v.Enabled) && AttachmentsSwitch.IsOn(v.Attachments);
            }
        }

        /// <summary>The pause menu's line, either way it flips: on, the between-runs trial goes on with it, so one line gives the whole loop.</summary>
        void FlipAttachments()
        {
            bool on = !AttachmentsLineOn;
            AttachmentsSwitch.Set(on);
            if (on) BetweenRunsSwitch.Set(true);
        }

        /// <summary>
        /// The pause menu's stats in a run with the attachments on (or a loadout applied): one line more, what is fitted,
        /// "Loadout  Pistols: Laser Sight, Bull Barrel", or "Loadout  Pistols: stock". Nothing more with the attachments off.
        /// </summary>
        List<string> WithWeaponLine(List<string> lines, RunModifiers m)
        {
            if (m == null || _levels == null || !_levels.InRun || (!m.Weapon.Any && !_levels.AttachmentsOn)) return lines;
            lines.Add("Loadout  " + WeaponAttachments.WeaponName(WeaponKind.Pistols) + ": " + (m.Weapon.Any ? string.Join(", ", m.Weapon.Parts) : "stock"));
            return lines;
        }

        /// <summary>
        /// Enter or a click on one of the weapon's rows (<paramref name="part"/>: its place among the parts, in slot order):
        /// buys it (and fits it), fits it, or takes it off. False when the screen is not up, the attachments are off, or nothing happened.
        /// </summary>
        public bool PickWeaponLine(int part)
        {
            Find();
            if (_levels == null || !_levels.BetweenRunsUp || !_levels.AttachmentsOn) return false;
            List<WeaponAttachment> parts = WeaponRack.InSlotOrder(_levels.MetaValues);
            if (part < 0 || part >= parts.Count) return false;
            BetweenRunsSelected = BetweenRunsRows + part;
            return _levels.UseAttachment(parts[part].Id);
        }

        /// <summary>The screen's keys with the weapon's rows after the rack's: the arrows go round all of them; Enter picks the line it is on.</summary>
        void WeaponKeys(Keyboard keyboard, bool enter)
        {
            int rackRows = BetweenRunsRows, rows = rackRows + _weaponParts.Count;
            if (keyboard.upArrowKey.wasPressedThisFrame) BetweenRunsSelected = (BetweenRunsSelected + rows - 1) % rows;
            else if (keyboard.downArrowKey.wasPressedThisFrame) BetweenRunsSelected = (BetweenRunsSelected + 1) % rows;
            BetweenRunsSelected = Mathf.Clamp(BetweenRunsSelected, 0, rows - 1);
            bool strike = BetweenRunsSelected > 0 && BetweenRunsSelected < rackRows && Pieces[BetweenRunsSelected - 1].Effect == MetaEffect.StrikeOut;
            if (keyboard[BetweenRunsStartKey].wasPressedThisFrame) _levels.StartFromBetweenRuns();
            else if (strike && keyboard.leftArrowKey.wasPressedThisFrame) _levels.ChangeStruckOut(-1);
            else if (strike && keyboard.rightArrowKey.wasPressedThisFrame) _levels.ChangeStruckOut(1);
            else if (enter)
            {
                if (BetweenRunsSelected < rackRows) PickBetweenRunsLine(BetweenRunsSelected);
                else PickWeaponLine(BetweenRunsSelected - rackRows);
            }
        }

        /// <summary>How many times the weapon's lines were made (the fifth review of 2026-11-21, work order 077). For tests.</summary>
        public int WeaponListsMade { get; private set; }

        // What the weapon's lines were last made from (work order 077): the screen up, the save and the table, the currency, and
        // copies of the owned and fitted lists. The same, and the lines stand.
        bool _weaponWasUp;
        MetaState _weaponMadeFor;
        MetaValues _weaponMadeWith;
        int _weaponCurrency;
        readonly List<string> _weaponOwned = new List<string>();
        readonly List<string> _weaponFitted = new List<string>();

        /// <summary>
        /// The weapon's lines, made when the screen opens with the attachments on and again only when what they say changes (a
        /// part bought, fitted or taken off, the currency), not every frame it is up (the fifth review of 2026-11-21, work
        /// order 077, as <see cref="PauseLines"/> does for the pause screen); also without a window, for tests.
        /// </summary>
        void BuildWeaponLines()
        {
            bool on = WeaponOn;
            if (on && _weaponWasUp && !WeaponLinesStale()) return;
            _weaponWasUp = on;
            _weaponLines.Clear();
            _weaponRows.Clear();
            _weaponParts.Clear();
            if (!on) return;
            MetaValues v = _levels.MetaValues;
            MetaState made = _levels.BetweenRunsState;
            _weaponMadeFor = made;
            _weaponMadeWith = v;
            _weaponCurrency = made.Currency;
            _weaponOwned.Clear();
            _weaponOwned.AddRange(made.AttachmentsOwned);
            _weaponFitted.Clear();
            _weaponFitted.AddRange(made.AttachmentsFitted);
            WeaponListsMade++;
            _weaponLines.Add("Loadout (trial)");
            _weaponRows.Add(null);
            _weaponLines.Add("put together before the run: owned parts fit and come off freely");
            _weaponRows.Add(null);
            foreach (WeaponKind weapon in WeaponAttachments.Weapons)
            {
                // Each weapon its section: its name and what its parts act on, then its slots (Kristian, 2026-11-21: "samme til shotgunnet").
                _weaponLines.Add(WeaponAttachments.WeaponName(weapon) + ":  " + WeaponAttachments.WeaponActsOn(weapon));
                _weaponRows.Add(null);
                foreach (WeaponSlot slot in WeaponAttachments.SlotsOf(weapon))
                {
                    _weaponLines.Add(WeaponAttachments.SlotWord(slot));
                    _weaponRows.Add(null);
                    foreach (WeaponAttachment p in v.Parts ?? System.Array.Empty<WeaponAttachment>())
                    {
                        if (p == null || p.Weapon != weapon || p.Slot != slot) continue;
                        _weaponLines.Add(PartHead(p) + "\n" + p.Line + (string.IsNullOrEmpty(p.CostLine) ? "" : "\nCost:  " + p.CostLine));
                        _weaponRows.Add(p);
                        _weaponParts.Add(p);
                    }
                }
            }
        }

        /// <summary>Has what the weapon's lines say changed since they were made (work order 077)? Nothing made in asking.</summary>
        bool WeaponLinesStale()
        {
            MetaState s = _levels.BetweenRunsState;
            return s != _weaponMadeFor || _levels.MetaValues != _weaponMadeWith || s.Currency != _weaponCurrency
                   || !SameIds(s.AttachmentsOwned, _weaponOwned) || !SameIds(s.AttachmentsFitted, _weaponFitted);
        }

        static bool SameIds(List<string> now, List<string> then)
        {
            if (now.Count != then.Count) return false;
            for (int i = 0; i < now.Count; i++)
                if (!string.Equals(now[i], then[i], System.StringComparison.Ordinal)) return false;
            return true;
        }

        /// <summary>"Laser Sight   FITTED": the name and what it is to her now.</summary>
        string PartHead(WeaponAttachment p)
        {
            MetaState s = _levels.BetweenRunsState;
            MetaValues v = _levels.MetaValues;
            string state;
            if (WeaponRack.IsFitted(s, p.Id)) state = "FITTED";
            else if (WeaponRack.Owns(s, p.Id)) state = "owned";
            else if (s.Currency >= p.Price) state = "price " + p.Price.ToString(s_Inv) + " " + v.CurrencyName;
            else state = p.Price.ToString(s_Inv) + " " + v.CurrencyName + ", " + (p.Price - s.Currency).ToString(s_Inv) + " more";
            return p.Name + "   " + state;
        }

        /// <summary>The end screen's line about her pistols (with the attachments on): what the Echoes buy now, or what the next part needs.</summary>
        void AddWeaponEndLine()
        {
            if (_levels == null || !_levels.AttachmentsOn) return;
            _endLines.Add("Weapon  " + WeaponRack.NextBuy(_levels.BetweenRunsState, _levels.MetaValues).Words());
        }

        /// <summary>A row's height for its words at this width: at least <paramref name="least"/>.</summary>
        float RowHeight(string text, float width, float least)
        {
            _measure.text = text;
            return Mathf.Max(least, Mathf.Ceil(_pieceButton.CalcHeight(_measure, width)));
        }

        /// <summary>
        /// The screen between runs with the weapon's panel (work order 060): the rack's panel as 029 draws it, at the width the
        /// layout gives it (its rows as tall as their words need there, never less than 029's), and the weapon's to its right.
        /// </summary>
        void DrawBetweenRunsWithWeapon(float viewWidth)
        {
            if (_pieceButton == null)
            {
                _pieceButton = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 4, 4) };
                _pieceOn = new GUIStyle(_pieceButton) { fontStyle = FontStyle.Bold };
                _pieceOn.normal.textColor = s_Accent;
            }
            Fill(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), s_Dim);
            BetweenRunsLayout.Panels(viewWidth, true, out LayoutRect rackAt, out LayoutRect weaponAt);
            MetaPiece[] pieces = Pieces;
            MetaState s = _levels.BetweenRunsState;
            const float startH = 32f, rowH = 40f, gap = 4f, arrow = 30f, headH = 22f, keysH = 32f;
            float w = rackAt.Width;

            // The rack's rows, measured at this width.
            _rackRowHeights.Clear();
            float rowsH = 0f;
            for (int i = 0; i < pieces.Length; i++)
            {
                bool strike = pieces[i].Effect == MetaEffect.StrikeOut && s.Owns(pieces[i].Id);
                float h = RowHeight(_betweenLines[4 + i], w - 60f - (strike ? 2f * arrow + 8f : 0f), rowH);
                _rackRowHeights.Add(h);
                rowsH += h + gap;
            }
            float rackH = 50f + 44f + startH + gap + rowsH + 20f + 40f;

            // The weapon's rows, measured at its width.
            float ww = weaponAt.Width;
            float weaponH = 72f + 22f + keysH;
            for (int i = 2; i < _weaponLines.Count; i++)
                weaponH += (_weaponRows[i] == null ? headH : RowHeight(_weaponLines[i], ww - 40f, rowH)) + gap;

            float top = BetweenRunsLayout.Top(SkillBar.LayoutHeight, Mathf.Max(rackH, weaponH));

            // The rack, as 029 draws it.
            var r = new Rect(rackAt.X, top, w, rackH);
            Panel(r, s_Accent, r.y + 46f);
            GUI.Label(new Rect(r.x, r.y + 8f, w, 36f), _betweenLines[0], _pauseTitle);
            float y = r.y + 50f;
            GUI.Label(new Rect(r.x + 30f, y, w * 0.4f, 20f), _betweenLines[1], _big);
            GUI.Label(new Rect(r.x + 30f + w * 0.4f, y, w * 0.6f - 60f, 20f), _betweenLines[2], _label);
            y += 44f;
            for (int i = 0; i < 1 + pieces.Length; i++)
            {
                float rh = i == 0 ? startH : _rackRowHeights[i - 1];
                var row = new Rect(r.x + 30f, y, w - 60f, rh);
                if (i == BetweenRunsSelected) Frame(new Rect(row.x - 3f, row.y - 2f, row.width + 6f, row.height + 4f), s_Accent);
                if (i == 0)
                {
                    if (GUI.Button(row, _betweenLines[3], _button))
                    {
                        PickBetweenRunsLine(0);
                        return; // the screen is gone
                    }
                }
                else
                {
                    MetaPiece p = pieces[i - 1];
                    bool strike = p.Effect == MetaEffect.StrikeOut && s.Owns(p.Id);
                    var main = strike ? new Rect(row.x, row.y, row.width - 2f * arrow - 8f, rh) : row;
                    if (GUI.Button(main, _betweenLines[3 + i], s.IsEquipped(p.Id) ? _pieceOn : _pieceButton)) PickBetweenRunsLine(i);
                    if (strike)
                    {
                        if (GUI.Button(new Rect(row.xMax - 2f * arrow - 4f, row.y, arrow, rh), "<", _button)) { BetweenRunsSelected = i; _levels.ChangeStruckOut(-1); }
                        if (GUI.Button(new Rect(row.xMax - arrow, row.y, arrow, rh), ">", _button)) { BetweenRunsSelected = i; _levels.ChangeStruckOut(1); }
                    }
                }
                y += rh + gap;
            }
            GUI.Label(new Rect(r.x + 30f, y, w - 60f, 20f), _betweenLines[_betweenLines.Count - 1], _label);
            y += 22f;
            GUI.Label(new Rect(r.x + 30f, y, w - 60f, 32f),
                      "Arrows and Enter (or a click): buy, equip, take off; on down into the weapon.   Left and right on Not That One: the skill struck out.\n"
                      + BetweenRunsStartKey + " or Enter on the first line: start.   Esc: the practice room.   The game is paused.", _small);

            // The weapon, to its right.
            if (ww <= 0f || _weaponLines.Count < 2) return;
            var wr = new Rect(weaponAt.X, top, ww, weaponH);
            Panel(wr, s_Accent, wr.y + 46f);
            GUI.Label(new Rect(wr.x, wr.y + 8f, ww, 36f), _weaponLines[0], _pauseTitle);
            GUI.Label(new Rect(wr.x + 20f, wr.y + 50f, ww - 40f, 18f), _weaponLines[1], _small);
            y = wr.y + 72f;
            int part = -1;
            for (int i = 2; i < _weaponLines.Count; i++)
            {
                WeaponAttachment p = _weaponRows[i];
                if (p == null)
                {
                    GUI.Label(new Rect(wr.x + 20f, y, ww - 40f, headH), _weaponLines[i], _label);
                    y += headH + gap;
                    continue;
                }
                part++;
                float rh = RowHeight(_weaponLines[i], ww - 40f, rowH);
                var row = new Rect(wr.x + 20f, y, ww - 40f, rh);
                if (BetweenRunsRows + part == BetweenRunsSelected) Frame(new Rect(row.x - 3f, row.y - 2f, row.width + 6f, row.height + 4f), s_Accent);
                if (GUI.Button(row, _weaponLines[i], WeaponRack.IsFitted(s, p.Id) ? _pieceOn : _pieceButton)) PickWeaponLine(part);
                y += rh + gap;
            }
            GUI.Label(new Rect(wr.x + 20f, y + 4f, ww - 40f, keysH),
                      "Enter or a click: buy (it is fitted at once), fit, take off.\nOne part a slot; an empty slot is the stock gun.", _small);
        }
    }
}
