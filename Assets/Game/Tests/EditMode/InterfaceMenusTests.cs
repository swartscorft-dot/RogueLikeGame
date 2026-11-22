using System.Collections.Generic;
using NUnit.Framework;

namespace Roguelike.Rules.Tests
{
    /// <summary>
    /// Work order 078 (2026-11-22, Kristian ticked "Menyene i konseptstilen"), as plain rules: the pause menu's lines read apart
    /// for the concept's drawing (a setting's name and value, an action's words and key, a stat's value, start and change)
    /// without changing what they say; the pause menu's place at 1920 by 1080 and at 1280 by 720 (on screen, the rows and the
    /// panels apart, drawn smaller only where it does not fit); the two columns and the chips; the switch that gives this
    /// morning's menus; an upgrade's tier kept by the run's record for the end screen.
    /// </summary>
    public class InterfaceMenusTests
    {
        static MenuValues M => new MenuValues();

        static float LayoutWidth(float w, float h) => w / InterfaceRules.Scale(h, new InterfaceValues());

        static bool Overlap(UiBox a, UiBox b) => a.X < b.Right && b.X < a.Right && a.Y < b.Bottom && b.Y < a.Bottom;

        // ---------------- The words read apart ----------------

        [Test]
        public void ASetting_IsItsNameAndItsValue_AndAnAction_ItsWordsAndItsKey()
        {
            MenuWords.Setting("Camera kick:  on", out string name, out string value);
            Assert.That(name, Is.EqualTo("Camera kick"));
            Assert.That(value, Is.EqualTo("on"));
            MenuWords.Setting("Volume:  100 %", out name, out value);
            Assert.That((name, value), Is.EqualTo(("Volume", "100 %")));
            MenuWords.Setting("Between runs (trial):  off", out name, out value);
            Assert.That((name, value), Is.EqualTo(("Between runs (trial)", "off")));
            MenuWords.Action("Resume   (Esc)", out string words, out string key);
            Assert.That((words, key), Is.EqualTo(("Resume", "Esc")));
            MenuWords.Action("Leave to the practice room   (F7)", out words, out key);
            Assert.That((words, key), Is.EqualTo(("Leave to the practice room", "F7")));
            MenuWords.Action("Same run again", out words, out key);
            Assert.That((words, key), Is.EqualTo(("Same run again", "")), "a line without its key (the developer's keys off) has none");
            MenuWords.Button("F7 / Esc  practice", out key, out words);
            Assert.That((key, words), Is.EqualTo(("F7 / Esc", "practice")));
            MenuWords.Button("Same run", out key, out words);
            Assert.That((key, words), Is.EqualTo(("", "Same run")));
            Assert.That(MenuWords.IsOn("on") && MenuWords.IsOff("off") && !MenuWords.IsOn("100 %"), Is.True);
        }

        [TestCase("Attack speed  4.4 shots/s  (3.8)", "Attack speed", "4.4 shots/s", "3.8", true, "+0.6")]
        [TestCase("Crit chance  15 %  (5 %)", "Crit chance", "15 %", "5 %", true, "+10 %")]
        [TestCase("Move speed  92 %  (100 %)", "Move speed", "92 %", "100 %", true, "-8 %")]
        [TestCase("Reach  13.5 m  (12 m)", "Reach", "13.5 m", "12 m", true, "+1.5 m")]
        [TestCase("Max health  120  (100)", "Max health", "120", "100", true, "+20")]
        [TestCase("Luck  0  (0)", "Luck", "0", "0", false, "")]
        [TestCase("Damage  +0 a shot, skills by weight  (+0)", "Damage", "+0 a shot, skills by weight", "+0", false, "")]
        [TestCase("Damage  +3 a shot, skills by weight  (+0)", "Damage", "+3 a shot, skills by weight", "+0", true, "+3")]
        [TestCase("Cooldowns  every skill -10 %  (none)", "Cooldowns", "every skill -10 %", "none", true, "")]
        [TestCase("Loadout  Pistols: stock", "Loadout", "Pistols: stock", "", false, "")]
        public void AStat_IsItsName_ItsValue_ItsStart_AndItsChange(string line, string name, string value, string start, bool changed, string change)
        {
            MenuStat s = MenuWords.Stat(line);
            Assert.That(s.Name, Is.EqualTo(name));
            Assert.That(s.Value, Is.EqualTo(value));
            Assert.That(s.Start, Is.EqualTo(start));
            Assert.That(s.Changed, Is.EqualTo(changed));
            Assert.That(s.Change, Is.EqualTo(change));
        }

        [Test]
        public void HerStatsLines_ReadApart_EveryOneWithANameAValueAndAStart_NoneChanged_AtARunsStart_AndTheStepsTaken_Changed()
        {
            var m = new RunModifiers { StatsAndTiers = true };
            List<string> lines = m.Stats.Lines(0.26f, 12f, 100);
            Assert.That(lines.Count, Is.GreaterThan(10));
            foreach (string line in lines)
            {
                MenuStat s = MenuWords.Stat(line);
                Assert.That(s.Name.Length > 0 && s.Value.Length > 0 && s.Start.Length > 0, Is.True, line);
                Assert.That(s.Changed, Is.False, "at the run's start: " + line);
            }
            m.Take(new Upgrade { Kind = UpgradeKind.CritChance, Amount = 0.05f });
            m.Take(new Upgrade { Kind = UpgradeKind.MaxHealth, Amount = 20f });
            lines = m.Stats.Lines(0.26f, 12f, 100);
            var changed = new List<string>();
            foreach (string line in lines)
            {
                MenuStat s = MenuWords.Stat(line);
                if (s.Changed) changed.Add(s.Name + " " + s.Change);
            }
            Assert.That(changed, Has.Member("Crit chance +5 %"), string.Join(" | ", lines));
            Assert.That(changed, Has.Member("Max health +20"), string.Join(" | ", lines));
            Assert.That(changed.Count, Is.EqualTo(2), "only the stats the steps moved: " + string.Join(", ", changed));
        }

        // ---------------- The pause menu's place ----------------

        [TestCase(1920f, 1080f)]
        [TestCase(1280f, 720f)]
        public void ThePauseMenu_StandsOnScreen_ItsRowsAndPanelsApart_AtBothSizes(float w, float h)
        {
            MenuValues m = M;
            float view = LayoutWidth(w, h);
            const int rows = 11; // a run in the Editor: five actions with Quit in a built player, six settings
            const float side = 760f; // her stats and the keys, about as tall as a run's
            PauseLayout l = MenuLayout.Pause(view, rows, 4, side, m);
            float fit = MenuLayout.Fit(l.Width, l.Height, view, 1080f, m);
            Assert.That(fit, Is.EqualTo(1f), "it fits at 16:9 without being drawn smaller");
            Assert.That(l.Rows.X, Is.GreaterThanOrEqualTo(m.Margin));
            Assert.That(l.Side.Right, Is.LessThanOrEqualTo(view - m.Margin));
            Assert.That(l.Height, Is.LessThanOrEqualTo(1080f), "and down the screen");
            Assert.That(Overlap(l.Rows, l.Side) || Overlap(l.Hint, l.Side) || Overlap(l.Heading, l.Side), Is.False, "the rows, their keys and the heading beside the panels, not under them");
            Assert.That(l.Heading.Bottom, Is.LessThan(l.Rows.Y), "the heading over the rows");
            Assert.That(l.Hint.Y, Is.GreaterThan(l.Rows.Bottom), "the keys' line under them");
            UiBox last = MenuLayout.Row(l.Rows, rows - 1, 4, m);
            Assert.That(last.Bottom, Is.EqualTo(l.Rows.Bottom).Within(1e-3f), "the last row ends the column");
            UiBox setting = MenuLayout.Row(l.Rows, 4, 4, m), action = MenuLayout.Row(l.Rows, 3, 4, m);
            Assert.That(setting.Y - action.Bottom, Is.EqualTo(m.RowGap + m.SectionGap).Within(1e-3f), "a gap before the settings");
            Assert.That(l.Width, Is.LessThan(view * 0.75f), "compact panels, not a slab across the screen");
        }

        [Test]
        public void AMenuTooLargeForTheView_IsDrawnSmallerToFit_NeverLarger()
        {
            MenuValues m = M;
            Assert.That(MenuLayout.Fit(1000f, 900f, 1920f, 1080f, m), Is.EqualTo(1f));
            float f = MenuLayout.Fit(1400f, 1200f, 1440f, 1080f, m);
            Assert.That(f, Is.EqualTo(System.Math.Min((1440f - 2f * m.Margin) / 1400f, 1080f / 1200f)).Within(1e-5f));
            Assert.That(1400f * f, Is.LessThanOrEqualTo(1440f - 2f * m.Margin + 1e-3f));
            Assert.That(1200f * f, Is.LessThanOrEqualTo(1080f + 1e-3f));
        }

        [Test]
        public void TwoColumns_TopToBottom_ThenTheSecond_AndALineTooWide_OnAWholeLineAfter()
        {
            var lineOf = new List<int>();
            var columnOf = new List<int>();
            int lines = MenuLayout.TwoColumns(new List<float> { 100f, 400f, 90f, 120f, 80f, 110f }, 200f, lineOf, columnOf);
            // Five fit: three lines in the first column, two in the second; the wide one after them on a line of its own.
            Assert.That(lines, Is.EqualTo(4));
            Assert.That(columnOf, Is.EqualTo(new List<int> { 0, -1, 0, 0, 1, 1 }));
            Assert.That(lineOf, Is.EqualTo(new List<int> { 0, 3, 1, 2, 0, 1 }));
            Assert.That(MenuLayout.TwoColumns(new List<float>(), 200f, lineOf, columnOf), Is.EqualTo(0));
        }

        [Test]
        public void Chips_FillRowsNoWiderThanTheirArea_EachRowCentred()
        {
            var x = new List<float>();
            var row = new List<int>();
            int rows = MenuLayout.Chips(new List<float> { 300f, 300f, 300f, 200f }, 1000f, 10f, x, row);
            Assert.That(rows, Is.EqualTo(2));
            Assert.That(row, Is.EqualTo(new List<int> { 0, 0, 0, 1 }));
            Assert.That(x[0], Is.EqualTo((1000f - 920f) * 0.5f).Within(1e-3f), "the first row centred");
            Assert.That(x[1] - x[0], Is.EqualTo(310f).Within(1e-3f));
            Assert.That(x[3], Is.EqualTo(400f).Within(1e-3f), "the second row centred");
            Assert.That(MenuLayout.Chips(new List<float>(), 1000f, 10f, x, row), Is.EqualTo(0));
        }

        // ---------------- This morning's look, and the record ----------------

        [Test]
        public void TheConceptsMenus_AreOnByDefault_AndOneSwitchGivesThisMorningsMenus()
        {
            var v = new InterfaceValues();
            Assert.That(v.Menus, Is.Not.Null);
            Assert.That(v.Menus.Concept, Is.True, "on: the menus in the concept's style");
            Assert.That(v.Menus.DeadWord, Is.EqualTo("DEAD"));
            Assert.That(v.Menus.ClearedWord, Is.EqualTo("CLEARED"));
            Assert.That(v.Menus.Veil, Is.LessThan(0.8f), "a veil the game shows through, not a black slab");
        }

        [Test]
        public void TheRecord_KeepsEachPicksTier_ForTheEndScreensColours_AndItsWordsAsBefore()
        {
            var r = new RunRecord("ABCDEFGH", 5);
            r.TakeSkill("Long Shot", UpgradeTier.Epic, "Overcharge");
            r.TakeSkill("Slide");
            r.TakeUpgrade("+15% atk speed", UpgradeTier.Rare);
            r.TakeUpgrade("+5% crit");
            Assert.That(r.SkillTierAt(0), Is.EqualTo(UpgradeTier.Epic));
            Assert.That(r.SkillTierAt(1), Is.EqualTo(UpgradeTier.Common));
            Assert.That(r.SkillNamed(0), Is.EqualTo("Long Shot (epic, Overcharge)"));
            Assert.That(r.UpgradeTierAt(0), Is.EqualTo(UpgradeTier.Rare));
            Assert.That(r.UpgradeTierAt(1), Is.EqualTo(UpgradeTier.Common));
            Assert.That(r.UpgradeTierAt(5), Is.EqualTo(UpgradeTier.Common), "past the list: common");
            Assert.That(r.UpgradesText, Is.EqualTo("+15% atk speed, +5% crit"), "the words as before");
            Assert.That(r.SkillsText, Is.EqualTo("Long Shot (epic, Overcharge), Slide"));
        }
    }
}
