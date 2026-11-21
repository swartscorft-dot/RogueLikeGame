using System.Collections.Generic;
using System.Globalization;
using Roguelike.Rules;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Roguelike
{
    /// <summary>
    /// The screen between runs and the end screens' lines about it (work order 029, 2026-11-20; decision 0009, a proposal
    /// he has not ruled on), as part of <see cref="RunHud"/>, drawn the way the pause menu is. Nothing of it shows with the
    /// switch off (<see cref="GeneratedLevels.MetaOn"/>).
    ///
    /// The screen, in the middle over the dimmed game: the title "Between runs (trial)"; the currency she has; the budget,
    /// its points used and left; a first line that starts the run ("Start the run", or "Start the same run again"); and a
    /// line per piece of the rack, in the table's order: its name, its kind, its points to equip, and what it is to her now
    /// (equipped, owned, its price, what it needs, or the feat it comes from), with one line of plain words under it. Not
    /// That One's line says which pool skill is struck out, with arrows to change it. Under them, a line about the last
    /// thing tried, and the keys.
    ///
    /// Its keys, read only while <see cref="PlayerInputDriver.ReadRealDevices"/> is true: the up and down arrows move along
    /// the lines; Enter on the first line starts the run, on a piece buys it, equips it or takes it off
    /// (<see cref="GeneratedLevels.UsePiece"/>); the left and right arrows on Not That One change the skill struck out;
    /// Space starts the run from any line; Esc leaves to the practice room. A click does what Enter does. F5 starts a new
    /// run and F6 the same run, F7 goes to the practice room, as everywhere.
    ///
    /// The end screens, with the switch on, add before the seed: what the run earned and from what, the total, and each
    /// feat done for the first time.
    /// </summary>
    public sealed partial class RunHud
    {
        /// <summary>The key that starts the run from any line of the screen between runs.</summary>
        public const Key BetweenRunsStartKey = Key.Space;

        /// <summary>The line of the screen between runs the arrow keys are on: 0 the start, then the pieces in order.</summary>
        public int BetweenRunsSelected { get; private set; }

        /// <summary>The screen's lines while it is up: the title, the currency, the budget, the start, a line per piece, the note. Else empty.</summary>
        public IReadOnlyList<string> BetweenRunsLines => _betweenLines;
        public string BetweenRunsText => string.Join("\n", _betweenLines);

        readonly List<string> _betweenLines = new List<string>();
        bool _betweenWasUp;
        GUIStyle _pieceButton;
        GUIStyle _pieceOn;

        /// <summary>Is what lasts between runs on: the pause menu's line says this.</summary>
        bool BetweenRunsOn
        {
            get
            {
                GeneratedLevels levels = _levels != null ? _levels : GeneratedLevels.Current;
                return levels != null ? levels.MetaOn : BetweenRunsSwitch.IsOn(new MetaValues().Enabled);
            }
        }

        MetaPiece[] Pieces => _levels != null && _levels.MetaValues.Pieces != null ? _levels.MetaValues.Pieces : s_NoPieces;
        static readonly MetaPiece[] s_NoPieces = new MetaPiece[0];
        int BetweenRunsRows => 1 + Pieces.Length;

        /// <summary>
        /// Enter or a click on a line of the screen between runs: the first starts the run, a piece is bought, equipped or
        /// taken off. False when the screen is not up or nothing happened.
        /// </summary>
        public bool PickBetweenRunsLine(int line)
        {
            Find();
            if (_levels == null || !_levels.BetweenRunsUp || line < 0 || line >= BetweenRunsRows) return false;
            BetweenRunsSelected = line;
            if (line == 0) return _levels.StartFromBetweenRuns() != null;
            return _levels.UsePiece(Pieces[line - 1].Id);
        }

        void BetweenRunsKeys(Keyboard keyboard, bool enter)
        {
            if (WeaponOn)
            {
                WeaponKeys(keyboard, enter); // work order 060: the arrows go on into the weapon's lines (RunHudWeapon.cs)
                return;
            }
            int rows = BetweenRunsRows;
            if (keyboard.upArrowKey.wasPressedThisFrame) BetweenRunsSelected = (BetweenRunsSelected + rows - 1) % rows;
            else if (keyboard.downArrowKey.wasPressedThisFrame) BetweenRunsSelected = (BetweenRunsSelected + 1) % rows;
            BetweenRunsSelected = Mathf.Clamp(BetweenRunsSelected, 0, rows - 1);
            bool strike = BetweenRunsSelected > 0 && Pieces[BetweenRunsSelected - 1].Effect == MetaEffect.StrikeOut;
            if (keyboard[BetweenRunsStartKey].wasPressedThisFrame) _levels.StartFromBetweenRuns();
            else if (strike && keyboard.leftArrowKey.wasPressedThisFrame) _levels.ChangeStruckOut(-1);
            else if (strike && keyboard.rightArrowKey.wasPressedThisFrame) _levels.ChangeStruckOut(1);
            else if (enter) PickBetweenRunsLine(BetweenRunsSelected);
        }

        /// <summary>The screen's lines, worked out every frame it is up (also without a window, for tests).</summary>
        void BuildBetweenRunsLines()
        {
            _betweenLines.Clear();
            bool up = _levels != null && _levels.BetweenRunsUp;
            if (up && !_betweenWasUp) BetweenRunsSelected = 0;
            _betweenWasUp = up;
            if (!up) return;
            MetaState s = _levels.BetweenRunsState;
            MetaValues v = _levels.MetaValues;
            _betweenLines.Add("Between runs (trial)");
            _betweenLines.Add(v.CurrencyName + "  " + s.Currency.ToString(s_Inv));
            int budget = MetaRack.Budget(s, v), used = MetaRack.PointsUsed(s, v);
            _betweenLines.Add(string.Format(s_Inv, "Budget  {0} points:  {1} used,  {2} left", budget, used, budget - used));
            _betweenLines.Add(StartWords());
            foreach (MetaPiece p in Pieces) _betweenLines.Add(PieceHead(p) + "\n" + PieceLine(p));
            _betweenLines.Add(_levels.BetweenRunsNote);
        }

        string StartWords() =>
            (_levels.BetweenRunsSeed.HasValue ? "Start the same run again  (" + LevelSeed.ToText(_levels.BetweenRunsSeed.Value) + ")" : "Start the run")
            + "   (Enter, " + BetweenRunsStartKey + ")";

        /// <summary>"Thick Hide   stats, 2 points   equipped": the name, the kind, the points, and what it is to her now.</summary>
        string PieceHead(MetaPiece p)
        {
            MetaState s = _levels.BetweenRunsState;
            MetaValues v = _levels.MetaValues;
            string state;
            if (s.IsEquipped(p.Id)) state = "EQUIPPED";
            else if (s.Owns(p.Id))
                state = MetaRack.CanEquip(s, v, p.Id) == MetaEquip.NeedsFirst ? "owned, equip " + (MetaRack.Find(v, p.Needs)?.Name ?? "?") + " first" : "owned";
            else if (!p.Sold) state = "not sold: from the first kill of the act boss";
            else if (MetaRack.CanBuy(s, v, p.Id) == MetaBuy.NeedsFirst) state = "buy " + (MetaRack.Find(v, p.Needs)?.Name ?? "?") + " first,  then " + p.Price.ToString(s_Inv) + " " + v.CurrencyName;
            else state = "price " + p.Price.ToString(s_Inv) + " " + v.CurrencyName;
            return string.Format(s_Inv, "{0}   {1}, {2} point{3}   {4}", p.Name, p.KindWord, p.Points, p.Points == 1 ? "" : "s", state);
        }

        string PieceLine(MetaPiece p) =>
            p.Effect == MetaEffect.StrikeOut ? p.Line + "   Struck out:  " + _levels.StruckOutSkill + "  (left and right)" : p.Line;

        /// <summary>The end screen's lines about the run's earnings and feats, with the switch on: before the seed.</summary>
        void AddBetweenRunsEndLines()
        {
            RunEarnings e = _levels.Earnings;
            if (e == null || !e.Committed) return;
            MetaValues v = _levels.MetaValues;
            var from = new List<string>();
            if (e.FloorsCleared > 0) from.Add("floors " + e.FromFloors(v).ToString(s_Inv));
            if (e.RoomsCleared > 0) from.Add("rooms " + e.FromRooms(v).ToString(s_Inv)); // work order 060: never with the attachments off
            if (e.ElitesCleared > 0) from.Add("elite rooms " + e.FromElites(v).ToString(s_Inv));
            if (e.BossKilled) from.Add("act boss " + e.FromBoss(v).ToString(s_Inv));
            _endLines.Add(string.Format(s_Inv, "{0}  +{1} this run{2},  {3} in all", v.CurrencyName, e.Total(v),
                                        from.Count > 0 ? "  (" + string.Join(", ", from) + ")" : "", e.TotalAfter));
            AddWeaponEndLine(); // work order 060: what the Echoes buy for her pistols, with the attachments on (RunHudWeapon.cs)
            foreach (MetaFeat f in e.NewFeats)
            {
                _endLines.Add("Feat  " + MetaFeats.Words(f) + ":  budget +1");
                foreach (MetaPiece p in Pieces)
                    if (p != null && !p.Sold && p.FromFeat == f) _endLines.Add("New piece  " + p.Name + ",  " + p.Line);
            }
        }

        void DrawBetweenRuns(float viewWidth)
        {
            if (_betweenLines.Count < 4) return;
            if (WeaponOn)
            {
                DrawBetweenRunsWithWeapon(viewWidth); // work order 060: the rack and the weapon side by side (RunHudWeapon.cs)
                return;
            }
            if (_pieceButton == null)
            {
                _pieceButton = new GUIStyle(GUI.skin.button) { fontSize = 13, alignment = TextAnchor.UpperLeft, wordWrap = true, padding = new RectOffset(10, 10, 4, 4) };
                _pieceOn = new GUIStyle(_pieceButton) { fontStyle = FontStyle.Bold };
                _pieceOn.normal.textColor = s_Accent;
            }
            Fill(new Rect(0f, 0f, viewWidth, SkillBar.LayoutHeight), s_Dim);
            MetaPiece[] pieces = Pieces;
            const float w = 680f, startH = 32f, rowH = 40f, gap = 4f;
            float h = 50f + 44f + startH + gap + pieces.Length * (rowH + gap) + 20f + 40f;
            var r = new Rect((viewWidth - w) * 0.5f, Mathf.Max(6f, (SkillBar.LayoutHeight - h) * 0.5f), w, h);
            Panel(r, s_Accent, r.y + 46f); // work order 045: in the skin, a flat panel and a rule under the title
            GUI.Label(new Rect(r.x, r.y + 8f, w, 36f), _betweenLines[0], _pauseTitle);
            float y = r.y + 50f;
            GUI.Label(new Rect(r.x + 30f, y, w * 0.4f, 20f), _betweenLines[1], _big);
            GUI.Label(new Rect(r.x + 30f + w * 0.4f, y, w * 0.6f - 60f, 20f), _betweenLines[2], _label);
            y += 44f;
            MetaState s = _levels.BetweenRunsState;
            for (int i = 0; i < 1 + pieces.Length; i++)
            {
                float rh = i == 0 ? startH : rowH;
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
                    const float arrow = 30f;
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
                      "Arrows and Enter (or a click): buy, equip, take off.   Left and right on Not That One: the skill struck out.\n"
                      + BetweenRunsStartKey + " or Enter on the first line: start.   Esc: the practice room.   The game is paused.", _small);
        }
    }
}
